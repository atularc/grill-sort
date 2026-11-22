using System;
using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Deterministic level generator, Sort Streak style with an order queue (user 2026-11-22, from the Sort Streak "rows"
    /// video): a row of up to <see cref="MaxSkewers"/> trays on screen, every tray with a customer above it, and more
    /// orders waiting in a queue. Serving an order removes its tray; the next queued tray (new customer + mixed food)
    /// slides in from the right. Trays hold <see cref="Capacity"/> = order size + 1, so every tray has a spare slot
    /// (that, and part-empty trays, is the free room: there are no helper trays any more).
    /// Level N always gives the same puzzle (seeded); every candidate is solved before it is accepted, and the chosen
    /// one's par keeps rising. Difficulty grows with the number of orders, food types and look-alikes, never with the
    /// number of trays on screen (so the food never shrinks).
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Most trays (= customers) on screen at once.</summary>
        public const int MaxSkewers = 5;
        /// <summary>No helper trays any more (kept for older callers).</summary>
        public const int Helpers = 0;
        /// <summary>Most grills a level can show: the row + 1 for the Extra tray booster.</summary>
        public const int MaxGrills = MaxSkewers + 1;
        /// <summary>Biggest tray; the scene authors this many food slots per grill.</summary>
        public const int MaxCapacity = 6;
        /// <summary>Pieces per order.</summary>
        public const int Pieces = 4;
        /// <summary>Slots per tray = order size: a served tray is completely full, no empty grill under it (user 2026-11-22).
        /// Free room comes from the helper tray(s), like Sort Streak's white ropes, which slide along with the row.</summary>
        public const int Capacity = Pieces;
        /// <summary>Helper trays (no customer) in every row.</summary>
        public const int RowHelpers = 1;

        public struct Tier
        {
            /// <summary>Trays (= customers) on screen.</summary>
            public int Row;
            /// <summary>Orders in the level (row + queue).</summary>
            public int Orders;
            /// <summary>Different foods the level uses (orders may repeat a food, never two at once on screen).</summary>
            public int Types;
            public int MaxPerLookAlikeGroup;
            /// <summary>Extra empty room in the starting row beyond each tray's spare slot (more = easier).</summary>
            public int ExtraRoom;
            /// <summary>Par (minimum moves) the generator aims for.</summary>
            public int TargetPar;
            /// <summary>Spare slots per tray (tray capacity = order size + Spare).</summary>
            public int Spare;
            /// <summary>Free trays (no customer, never leave) in the row, at the ends.</summary>
            public int FreeTrays;
            public int Pieces => LevelGenerator.Pieces;
            public override string ToString() => $"{Row} on screen, {Orders} orders, {Types} foods, look-alikes/group {MaxPerLookAlikeGroup}, extra room {ExtraRoom}, target par {TargetPar}";
        }

        // first level, trays on screen, orders, food types, look-alikes per group, extra room, target par range (lo at the
        // tier's first level -> hi at its last)
        // first level, trays on screen (customers + 1 helper), orders, food types, look-alikes per group, (unused), target
        // par range (lo at the tier's first level -> hi at its last). Pars measured 2026-11-22 with 4-slot trays + 1 helper.
        static readonly (int start, int row, int orders, int types, int look, int room, int lo, int hi)[] Tiers =
        {
            (1, 3, 2, 2, 1, 0, 3, 3),       // tutorial (hand-made)
            (2, 3, 3, 3, 1, 0, 5, 7),
            (4, 4, 4, 4, 1, 0, 8, 10),
            (6, 4, 6, 5, 1, 0, 11, 15),
            (10, 5, 7, 6, 1, 0, 15, 18),
            (16, 5, 9, 7, 2, 0, 18, 22),    // look-alikes start
            (24, 5, 11, 8, 2, 0, 22, 27),
            (34, 5, 13, 9, 3, 0, 27, 32),
            (44, 5, 15, 10, 3, 0, 32, 36),
        };

        static readonly int[] TargetPars = BuildTargets();

        static int TierIndex(int level)
        {
            int i = 0;
            while (i + 1 < Tiers.Length && Tiers[i + 1].start <= level) i++;
            return i;
        }

        static int[] BuildTargets()
        {
            var t = new int[51];
            for (int lv = 1; lv <= 50; lv++)
            {
                int i = TierIndex(lv);
                int first = Tiers[i].start, last = i + 1 < Tiers.Length ? Tiers[i + 1].start - 1 : 50;
                float k = last == first ? 1f : (lv - first) / (float)(last - first);
                t[lv] = (int)Math.Round(Tiers[i].lo + (Tiers[i].hi - Tiers[i].lo) * k);
            }
            for (int lv = 2; lv <= 50; lv++) t[lv] = Math.Max(t[lv], t[lv - 1]);
            return t;
        }

        /// <summary>Difficulty knobs per level. Past 50: the last tier with a few more orders every 10 levels (cap 20).</summary>
        public static Tier TierFor(int level)
        {
            var r = Tiers[TierIndex(level)];
            var t = new Tier { Row = r.row, Orders = r.orders, Types = r.types, MaxPerLookAlikeGroup = r.look, ExtraRoom = r.room, Spare = DefaultSpare, FreeTrays = DefaultFreeTrays };
            if (level <= 50) t.TargetPar = TargetPars[level];
            else
            {
                int extra = Math.Min(5, (level - 50) / 10);
                t.Orders = Math.Min(20, r.orders + extra);
                t.Types = Math.Min(12, r.types + extra / 2);
                t.TargetPar = TargetPars[50] + extra * 2 - 1 + level % 3;
            }
            return t;
        }

        public static int DefaultSpare = 0, DefaultFreeTrays = RowHelpers;
        /// <summary>Where an order's food may start: trays (WindowBefore) before its own up to WindowAfter after it.</summary>
        public static int WindowBeforeFixed = 1, WindowAfter = 1;   // measured 2026-11-22: tighter windows solve far more often
        static int WindowBefore(int k) => WindowBeforeFixed >= 0 ? WindowBeforeFixed : k;
        /// <summary>Most own-order pieces a tray starts with at its base.</summary>
        public static int BaseMax = 2;

        public static uint SeedFor(int level) => unchecked((uint)(level * 7919 + 12345));

        /// <summary>Solves seeded candidates and keeps the one whose par is closest to the target (not below prevPar when possible).</summary>
        public static LevelData Generate(int level, int prevPar, MoveRule rule, int candidates = -1, int exactBudget = 400000, int fallbackBudget = 120000)
        {
            if (level == 1) return Tutorial(rule);
            var tier = TierFor(level);
            if (candidates <= 0) candidates = 8;
            var rng = new Mulberry32(SeedFor(level));
            int desired = Math.Max(tier.TargetPar, prevPar);
            int attempts = 0, found = 0;
            LevelData best = null;
            while (found < candidates && attempts < candidates * 150)
            {
                attempts++;
                var lv = Candidate(level, tier, rng, rule);
                if (lv == null) continue;
                var r = Solver.Solve(lv.ToBoard(), rule, exactBudget, fallbackBudget);
                if (!r.Solvable) continue;
                found++;
                lv.par = r.Par; lv.parOptimal = r.Optimal;
                if (best == null || Better(lv, best, desired, prevPar)) best = lv;
                if (best.parOptimal && best.par == desired) break;
            }
            return best;
        }

        /// <summary>
        /// Level 1, hand-made: corn tray [corn x3, pepper], pepper tray [pepper x3, corn], an empty helper tray. Park the pepper on
        /// the helper, drag the corn home (served), drag the pepper home (served): 3 moves, teaches the helper tray.
        /// </summary>
        static LevelData Tutorial(MoveRule rule)
        {
            int corn = Array.IndexOf(FoodCatalog.Names, "corn"), pepper = Array.IndexOf(FoodCatalog.Names, "pepper");
            int C = Capacity;
            var lv = new LevelData
            {
                number = 1, capacity = C, pieces = Pieces, skewers = 3, foodTypes = 2, seed = SeedFor(1), rule = rule,
                orders = new[] { corn, pepper, -1 },
                cells = new int[3 * C],
            };
            for (int i = 0; i < lv.cells.Length; i++) lv.cells[i] = -1;
            int[] a = { corn, corn, corn, pepper }, b = { pepper, pepper, pepper, corn };
            for (int i = 0; i < a.Length; i++) lv.cells[i] = a[i];
            for (int i = 0; i < b.Length; i++) lv.cells[C + i] = b[i];
            var r = Solver.Solve(lv.ToBoard(), rule);
            lv.par = r.Par; lv.parOptimal = r.Optimal;
            return r.Solvable ? lv : null;
        }

        static bool Better(LevelData a, LevelData b, int desired, int prevPar)
        {
            bool aOk = a.par >= prevPar, bOk = b.par >= prevPar;
            if (aOk != bOk) return aOk;
            int da = Math.Abs(a.par - desired), db = Math.Abs(b.par - desired);
            if (da != db) return da < db;
            if (a.parOptimal != b.parOptimal) return a.parOptimal;
            return a.par > b.par;
        }

        public static LevelData Candidate(int level, Tier tier, Mulberry32 rng, MoveRule rule)
        {
            int K = tier.Row - tier.FreeTrays, T = tier.Orders, P = Pieces, C = Pieces + tier.Spare, H = tier.FreeTrays;
            if (K < 1 || T < K) return null;
            var foods = PickFoods(tier, rng, out int pairs);
            if (foods == null) return null;

            // order sequence: every food at least once, never the same food twice among the K orders on screen together
            var orders = new int[T];
            var bag = new List<int>(foods);
            rng.Shuffle(bag);
            for (int j = 0; j < T; j++)
            {
                int pick = -1;
                for (int tries = 0; tries < 40 && pick < 0; tries++)
                {
                    int f = j < bag.Count ? bag[j] : foods[rng.Range(foods.Count)];
                    bool clash = false;
                    for (int d = 1; d < K && j - d >= 0; d++) if (orders[j - d] == f) clash = true;
                    if (!clash) pick = f;
                    else if (j < bag.Count) { int sw = j + 1 + rng.Range(Math.Max(1, bag.Count - j - 1)); if (sw < bag.Count) { (bag[j], bag[sw]) = (bag[sw], bag[j]); } }
                }
                if (pick < 0) return null;
                orders[j] = pick;
            }

            // pieces per tray: the starting customer trays hold K*P - ExtraRoom (never more than all the food); the queued
            // trays share the rest evenly (each at most C)
            var counts = new int[T];
            int rowPieces = Math.Min(T * P, K * P - (T > K ? tier.ExtraRoom : 0));
            for (int s2 = 0; s2 < K; s2++) counts[s2] = rowPieces / K + (s2 < rowPieces % K ? 1 : 0);
            for (int k = 0; k < K; k++)   // some trays part-empty, some fuller
            {
                int a2 = rng.Range(K), b2 = rng.Range(K);
                if (a2 != b2 && counts[a2] > 1 && counts[b2] < C) { counts[a2]--; counts[b2]++; }
            }
            int queuePieces = T * P - rowPieces, qn = T - K;
            if (qn == 0 && queuePieces > 0) return null;
            for (int q = 0; q < qn; q++) counts[K + q] = queuePieces / qn + (q < queuePieces % qn ? 1 : 0);
            for (int q = 0; q < qn; q++) if (counts[K + q] > C) return null;

            // place the pieces. Each tray starts with 1..BaseMax pieces of its OWN order at its fixed end (a half-started
            // order): a wrong piece at the base can only be dug out by lifting the whole stack, which needs room this
            // layout rarely has. The rest of the food goes into trays near its order (WindowBefore .. WindowAfter), mixed.
            var trayFood = new List<int>[T];
            var baseN = new int[T];
            var need = new int[T];
            for (int j = 0; j < T; j++) need[j] = P;
            for (int t = 0; t < T; t++)
            {
                trayFood[t] = new List<int>();
                int bmax = Math.Min(BaseMax, Math.Min(counts[t], P - 2));
                int bn = bmax <= 0 ? 0 : 1 + rng.Range(bmax);
                for (int k = 0; k < bn; k++) trayFood[t].Add(orders[t]);
                need[t] -= bn; baseN[t] = bn;
            }
            var pieces = new List<(int food, int order)>();
            for (int j = 0; j < T; j++) for (int k = 0; k < need[j]; k++) pieces.Add((orders[j], j));
            rng.Shuffle(pieces);
            foreach (var (food, j) in pieces)
            {
                int lo = Math.Max(0, j - WindowBefore(K)), hi = Math.Min(T - 1, j + WindowAfter);
                int t = -1;
                for (int tries = 0; tries < 12 && t < 0; tries++)
                {
                    int c = lo + rng.Range(hi - lo + 1);
                    if (c != j && trayFood[c].Count < counts[c]) t = c;
                }
                if (t < 0) for (int c = 0; c < T && t < 0; c++) if (c != j && trayFood[c].Count < counts[c]) t = c;
                if (t < 0) for (int c = 0; c < T && t < 0; c++) if (trayFood[c].Count < counts[c]) t = c;
                if (t < 0) return null;
                trayFood[t].Add(food);
            }
            // shuffle only above the base
            for (int t = 0; t < T; t++)
            {
                var top = trayFood[t].GetRange(baseN[t], trayFood[t].Count - baseN[t]);
                rng.Shuffle(top);
                for (int i = 0; i < top.Count; i++) trayFood[t][baseN[t] + i] = top[i];
            }


            int rowN = K + H;
            var lv = new LevelData
            {
                number = level,
                capacity = C,
                pieces = P,
                skewers = rowN,
                orders = new int[rowN],
                cells = new int[rowN * C],
                queueOrders = new int[T - K],
                queueCells = new int[(T - K) * C],
                foodTypes = foods.Count,
                lookAlikePairs = pairs,
                seed = SeedFor(level),
                rule = rule,
            };
            for (int i = 0; i < lv.cells.Length; i++) lv.cells[i] = -1;
            // row: free trays split between the two ends, customer trays in between
            int left = H / 2, slot = 0;
            for (int f = 0; f < left; f++) lv.orders[slot++] = -1;
            for (int t = 0; t < T; t++)
            {
                int[] dst; int at;
                if (t < K) { dst = lv.cells; at = slot * C; lv.orders[slot++] = orders[t]; }
                else { dst = lv.queueCells; at = (t - K) * C; lv.queueOrders[t - K] = orders[t]; }
                for (int i = 0; i < C; i++) dst[at + i] = i < trayFood[t].Count ? trayFood[t][i] : -1;
                int settled = 0;
                while (settled < trayFood[t].Count && trayFood[t][settled] == orders[t]) settled++;
                if (settled >= P) return null;
                if (level > 1 && settled >= P - 1) return null;
            }
            while (slot < rowN) lv.orders[slot++] = -1;
            return lv;
        }

        static List<int> PickFoods(Tier tier, Mulberry32 rng, out int lookAlikePairs)
        {
            var all = new List<int>();
            for (int i = 0; i < FoodCatalog.Count; i++) all.Add(i);
            rng.Shuffle(all);
            var perGroup = new int[FoodCatalog.LookAlikes.Length];
            var pick = new List<int>();
            int want = Math.Min(tier.Types, tier.Orders);
            foreach (var f in all)
            {
                int g = FoodCatalog.GroupOf(f);
                if (g >= 0 && perGroup[g] >= tier.MaxPerLookAlikeGroup) continue;
                if (g >= 0) perGroup[g]++;
                pick.Add(f);
                if (pick.Count == want) break;
            }
            lookAlikePairs = 0;
            foreach (var c in perGroup) lookAlikePairs += c * (c - 1) / 2;
            return pick.Count == want ? pick : null;
        }
    }
}
