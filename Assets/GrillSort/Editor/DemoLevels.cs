using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GrillSort.EditorTools
{
    /// <summary>
    /// The 5-tier demo for Aaron (2026-11-26), designed on the Sort Streak model (Research/SortStreak/Report.md).
    /// Each tier is a RECIPE (tray size, orders, the exact foods, wrapped "?" food, a lock) chosen by us. For each recipe
    /// many candidate boards are built, every one is PROVEN winnable by the solver, and a simple bot plays each 200 times
    /// to measure how hard it really is; the board closest to the tier's target difficulty is kept.
    /// Writes Data/DemoLevelBank.asset and Docs/DemoLevels.md.
    /// </summary>
    public static class DemoLevels
    {
        sealed class Recipe
        {
            public string Tier;
            public bool Hard;
            public int P, Row, Orders, ExtraRoom, Free = 1;
            public string[] Foods;
            public int RowHidden, QueueHiddenPerTray;
            public bool LockHelperByQueueFood;
            /// <summary>Locks to add, in order: "spare-screen" = a spare tray opened by a food already on screen (gentle);
            /// "customer-queue" = a customer's tray opened by a food that only arrives later (plan ahead).</summary>
            public string[] Locks = new string[0];
            /// <summary>Fallback lock setups tried in order when <see cref="Locks"/> makes a board unwinnable.</summary>
            public string[][] LockFallbacks = new string[0][];
            public float WinLo, WinHi;   // target bot win rate
            public int ParLo, ParHi;
        }

        static readonly Recipe[] Recipes =
        {
            new Recipe { Tier = "Easy", P = 4, Row = 5, Free = 2, Orders = 8,
                Foods = new[] { "corn", "tomato", "sausage", "mushroom", "pepper" },
                WinLo = .55f, WinHi = 1f, ParLo = 10, ParHi = 20 },
            new Recipe { Tier = "Medium", P = 5, Row = 5, Free = 2, Orders = 9,
                Foods = new[] { "corn", "tomato", "sausage", "mushroom", "shrimp", "onion" },
                RowHidden = 3, QueueHiddenPerTray = 1,
                WinLo = .25f, WinHi = .55f, ParLo = 16, ParHi = 40 },
            new Recipe { Tier = "Hard", Hard = true, P = 5, Row = 6, Free = 2, Orders = 10, ExtraRoom = 0,
                Foods = new[] { "corn", "tomato", "sausage", "beef", "mushroom", "shrimp", "onion" },
                RowHidden = 5, QueueHiddenPerTray = 2, Locks = new[] { "spare-screen" },
                WinLo = .08f, WinHi = .25f, ParLo = 22, ParHi = 55 },
            new Recipe { Tier = "Very Hard", Hard = true, P = 5, Row = 6, Free = 2, Orders = 11, ExtraRoom = 0,
                Foods = new[] { "corn", "pineapple", "sausage", "beef", "mushroom", "shrimp", "tomato" },
                RowHidden = 4, QueueHiddenPerTray = 2, Locks = new[] { "customer-queue" },
                WinLo = .03f, WinHi = .15f, ParLo = 26, ParHi = 65 },
            new Recipe { Tier = "Super Hard", Hard = true, P = 6, Row = 6, Free = 2, Orders = 10, ExtraRoom = 0,
                Foods = new[] { "corn", "pineapple", "sausage", "beef", "pepper", "zucchini", "tomato" },
                RowHidden = 4, QueueHiddenPerTray = 2, Locks = new[] { "customer-screen" },
                LockFallbacks = new[] { new[] { "spare-screen" } },
                WinLo = .005f, WinHi = .05f, ParLo = 28, ParHi = 90 },
        };

        const int Candidates = 200, Playouts = 200;
        /// <summary>Tiers before this index are kept from the existing DemoLevelBank (re-design only the rest).</summary>
        public static int KeepFirst = 4;

        [MenuItem("Grill Sort/Design Demo Levels (5 tiers)")]
        public static void Design()
        {
            var bank = ScriptableObject.CreateInstance<LevelBank>();
            bank.rule = MoveRule.AnyWithAbove;
            var report = new StringBuilder();
            report.AppendLine("# 5-tier demo levels (designed 2026-11-26)\n");
            report.AppendLine("Built by `Grill Sort > Design Demo Levels (5 tiers)` (Editor/DemoLevels.cs). Each tier is a recipe we chose (Sort Streak model); every kept board is proven winnable by the solver; \"bot win\" = how often a simple bot that plays sensible random moves wins (lower = harder).\n");
            report.AppendLine("| Demo level | Tier | Tray size | Trays on screen | Orders | Foods | Wrapped | Lock | Par (min moves) | Bot win |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            var prev = AssetDatabase.LoadAssetAtPath<LevelBank>("Assets/GrillSort/Data/DemoLevelBank.asset");
            for (int i = 0; i < Recipes.Length; i++)
            {
                var r = Recipes[i];
                LevelData lv; float win; int tried = 0, solvable = 0;
                if (i < KeepFirst && prev != null && i < prev.levels.Count && prev.levels[i].tier == r.Tier)
                {
                    lv = prev.levels[i]; win = WinRate(lv, Playouts, 1);   // keep an already designed tier
                }
                else lv = Pick(i + 1, r, out win, out tried, out solvable);
                if (lv == null) { Debug.LogError($"[Demo] {r.Tier}: no solvable candidate out of {tried}"); continue; }
                bank.levels.Add(lv);
                int wrapped = 0; foreach (var h in lv.hidden) wrapped += h; foreach (var h in lv.queueHidden) wrapped += h;
                string lockTxt = "none";
                var lockParts = new List<string>();
                for (int s = 0; s < lv.locks.Length; s++) if (lv.locks[s] >= 0) lockParts.Add((lv.orders[s] < 0 ? "spare tray" : "customer tray") + " opens on " + FoodCatalog.Names[lv.locks[s]]);
                if (lockParts.Count > 0) lockTxt = string.Join("; ", lockParts);
                report.AppendLine($"| {i + 1} | {r.Tier}{(r.Hard ? " (Hard label, x2 coins)" : "")} | {r.P} | {r.Row} | {lv.TotalOrders} | {string.Join(", ", r.Foods)} | {wrapped} | {lockTxt} | {lv.par}{(lv.parOptimal ? "" : "~")} | {win:P0} |");
                Debug.Log($"[Demo] {r.Tier}: par {lv.par} win {win:P0} ({solvable}/{tried} candidates solvable)");
            }
            report.AppendLine("\n`~` = a solution found by the fallback search (not proven shortest).\n");
            report.AppendLine("## Boards (top of each tray first; `*` = wrapped; `[H]` = spare tray; `lock:x` = opens when x is served)\n");
            foreach (var lv in bank.levels) report.AppendLine(Describe(lv));
            const string path = "Assets/GrillSort/Data/DemoLevelBank.asset";
            var old = AssetDatabase.LoadAssetAtPath<LevelBank>(path);
            if (old != null) { old.levels = bank.levels; old.rule = bank.rule; EditorUtility.SetDirty(old); }
            else AssetDatabase.CreateAsset(bank, path);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Application.dataPath, "../Docs/DemoLevels.md"), report.ToString());
            Debug.Log("[Demo] wrote " + path + " and Docs/DemoLevels.md");
        }

        static int Food(string name) => Array.IndexOf(FoodCatalog.Names, name);

        static LevelData Pick(int number, Recipe r, out float bestWin, out int tried, out int solvable)
        {
            var foods = new int[r.Foods.Length];
            for (int i = 0; i < foods.Length; i++) { foods[i] = Food(r.Foods[i]); if (foods[i] < 0) throw new Exception("unknown food " + r.Foods[i]); }
            var tier = new LevelGenerator.Tier { Row = r.Row, Orders = r.Orders, Types = foods.Length, MaxPerLookAlikeGroup = 9, ExtraRoom = r.ExtraRoom, Spare = 0, FreeTrays = r.Free, P = r.P, Foods = foods };
            float target = (r.WinLo + r.WinHi) * .5f;
            LevelData best = null; bestWin = -1f; float bestScore = float.MaxValue;
            tried = 0; solvable = 0;
            for (int c = 0; c < Candidates; c++)
            {
                var rng = new Mulberry32((uint)(number * 100003 + c * 7919 + 17));
                var lv = LevelGenerator.Candidate(number, tier, rng, MoveRule.AnyWithAbove);
                if (lv == null) continue;
                tried++;
                AddWrapped(lv, r, rng);
                var setups = new List<string[]> { r.Locks };
                setups.AddRange(r.LockFallbacks);
                Solver.Result res = null;
                foreach (var setup in setups)
                {
                    AddLock(lv, setup);
                    res = Solver.Solve(lv.ToBoard(), lv.rule, r.P >= 6 ? 300000 : 150000, r.P >= 6 ? 1500000 : 700000);
                    if (res.Solvable) break;
                }
                if (res == null || !res.Solvable) continue;
                solvable++;
                lv.par = res.Par; lv.parOptimal = res.Optimal;
                float win = WinRate(lv, Playouts, (uint)(c + 1));
                bool inBand = win >= r.WinLo && win <= r.WinHi && lv.par >= r.ParLo && lv.par <= r.ParHi;
                float score = Mathf.Abs(win - target) + (inBand ? 0f : 1f);
                if (score < bestScore) { bestScore = score; best = lv; bestWin = win; }
                if (inBand) break;   // first board inside the target band is good enough (keeps the run short)
            }
            if (best != null)
            {
                best.number = number;
                best.tier = r.Tier;
                best.hard = r.Hard;
            }
            return best;
        }

        /// <summary>Wraps pieces that are not a tray's bottom piece (that one is always visible).</summary>
        static void AddWrapped(LevelData lv, Recipe r, Mulberry32 rng)
        {
            int C = lv.capacity;
            lv.hidden = new int[lv.cells.Length];
            lv.queueHidden = new int[lv.queueCells.Length];
            var spots = new List<int>();
            for (int s = 0; s < lv.skewers; s++)
            {
                int n = 0; while (n < C && lv.cells[s * C + n] >= 0) n++;
                for (int i = 0; i < n - 1; i++) spots.Add(s * C + i);
            }
            rng.Shuffle(spots);
            for (int k = 0; k < r.RowHidden && k < spots.Count; k++) lv.hidden[spots[k]] = 1;
            int qn = lv.queueOrders.Length;
            for (int q = 0; q < qn; q++)
            {
                int n = 0; while (n < C && lv.queueCells[q * C + n] >= 0) n++;
                var qs = new List<int>();
                for (int i = 0; i < n - 1; i++) qs.Add(q * C + i);
                rng.Shuffle(qs);
                for (int k = 0; k < r.QueueHiddenPerTray && k < qs.Count; k++) lv.queueHidden[qs[k]] = 1;
            }
        }

        /// <summary>Very Hard: the spare (white) tray is locked by a food that is not on screen yet, only in the queue.</summary>
        static void AddLock(LevelData lv, string[] specs)
        {
            lv.locks = new int[lv.skewers];
            for (int s = 0; s < lv.skewers; s++) lv.locks[s] = -1;
            lv.queueLocks = new int[lv.queueOrders.Length];
            for (int q = 0; q < lv.queueLocks.Length; q++) lv.queueLocks[q] = -1;
            var onScreen = new List<int>();
            foreach (var o in lv.orders) if (o >= 0) onScreen.Add(o);
            foreach (var spec in specs)
            {
                if (spec == "customer-queue")
                {
                    // Sort Streak's vine (their L19): a customer's tray (with its food) stays locked until a food that is
                    // still in the queue gets served, so the player has to plan around it
                    int lockFood = -1;
                    foreach (var o in lv.queueOrders) if (!onScreen.Contains(o)) { lockFood = o; break; }
                    if (lockFood < 0 && lv.queueOrders.Length > 0) lockFood = lv.queueOrders[0];
                    for (int s = lv.skewers - 1; s >= 0; s--) if (lv.orders[s] >= 0 && lv.locks[s] < 0) { lv.locks[s] = lockFood; break; }
                }
                else if (spec == "customer-screen")
                {
                    // a customer's tray opened by the FIRST order on screen (served early, gentler)
                    int lockFood = onScreen.Count > 0 ? onScreen[0] : -1;
                    for (int s = lv.skewers - 1; s >= 0; s--) if (lv.orders[s] >= 0 && lv.locks[s] < 0 && lv.orders[s] != lockFood) { lv.locks[s] = lockFood; break; }
                }
                else if (spec == "spare-screen")
                {
                    // gentle lock (their L11): a spare tray opens once a food already on screen is served
                    int lockFood = onScreen.Count > 1 ? onScreen[1] : onScreen.Count > 0 ? onScreen[0] : -1;
                    for (int s = 0; s < lv.skewers; s++) if (lv.orders[s] < 0 && lv.locks[s] < 0) { lv.locks[s] = lockFood; break; }
                }
            }
        }

        /// <summary>
        /// How often a simple bot wins: each turn it serves if it can, otherwise plays a random legal move (it never
        /// undoes). Plays with full knowledge, so it is a rough "how many traps does this board have" measure.
        /// </summary>
        static float WinRate(LevelData lv, int runs, uint seed)
        {
            var rng = new Mulberry32(seed * 2654435761u);
            int wins = 0;
            var moves = new List<Move>();
            for (int run = 0; run < runs; run++)
            {
                var b = lv.ToBoard();
                for (int step = 0; step < 400; step++)
                {
                    if (b.IsSolved()) { wins++; break; }
                    b.LegalMoves(lv.rule, moves, true);
                    if (moves.Count == 0) break;
                    Move pick = moves[rng.Range(moves.Count)];
                    foreach (var m in moves)
                    {
                        // prefer a move that completes an order
                        int to = m.To, t = b.Target(to);
                        if (t < 0 || b.IsLocked(to)) continue;
                        if (b.Count(to) + m.Count != b.Pieces) continue;
                        bool all = true;
                        for (int i = 0; i < b.Count(to) && all; i++) all = b.Get(to, i) == t;
                        for (int k = 0; k < m.Count && all; k++) all = b.Get(m.From, m.Index + k) == t;
                        if (all) { pick = m; break; }
                    }
                    b.Apply(pick);
                }
            }
            return wins / (float)runs;
        }

        static string Describe(LevelData lv)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### Demo level {lv.number}: {lv.tier}{(lv.hard ? " (Hard)" : "")}, par {lv.par}");
            int C = lv.capacity;
            sb.AppendLine("On screen:");
            for (int s = 0; s < lv.skewers; s++) sb.AppendLine("- " + Tray(lv.cells, lv.hidden, s, C, lv.orders[s], lv.locks != null && s < lv.locks.Length ? lv.locks[s] : -1));
            sb.AppendLine("Queue (arrives in this order):");
            for (int q = 0; q < lv.queueOrders.Length; q++) sb.AppendLine($"- {q + 1}. " + Tray(lv.queueCells, lv.queueHidden, q, C, lv.queueOrders[q], -1));
            return sb.ToString();
        }

        static string Tray(int[] cells, int[] hid, int t, int C, int order, int lockFood)
        {
            var parts = new List<string>();
            for (int i = 0; i < C; i++)
            {
                int f = cells[t * C + i];
                if (f < 0) break;
                parts.Add(FoodCatalog.Names[f] + (hid != null && t * C + i < hid.Length && hid[t * C + i] != 0 ? "*" : ""));
            }
            string who = order < 0 ? "[H]" : FoodCatalog.Names[order] + " order";
            return $"{who}{(lockFood >= 0 ? " lock:" + FoodCatalog.Names[lockFood] : "")}: " + (parts.Count == 0 ? "(empty)" : string.Join(", ", parts));
        }
    }
}
