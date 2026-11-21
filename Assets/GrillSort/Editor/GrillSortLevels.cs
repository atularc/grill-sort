using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace GrillSort.EditorTools
{
    /// <summary>
    /// "Grill Sort > Bake And Verify Levels": generates the first <see cref="LevelCount"/> levels with the seeded
    /// generator, re-solves every one independently (replaying the solution on a fresh board), and writes the
    /// LevelBank asset plus Docs/LevelReport.md with the difficulty curve.
    /// </summary>
    public static class GrillSortLevels
    {
        public const int LevelCount = 50; // Aaron's timeline: 50-level build handover 14/12
        /// <summary>Rule the levels are baked for. Client decision 2026-11-18: grab any piece, everything above comes along.</summary>
        public const MoveRule BakeRule = MoveRule.AnyWithAbove;

        [MenuItem("Grill Sort/Bake And Verify Levels", priority = 1)]
        public static void BakeMenu() => Bake(LevelCount);

        public static string Bake(int count)
        {
            var bank = GrillSortSetup.MakeLevelBank();
            bank.rule = BakeRule;
            var rule = bank.rule;
            bank.levels.Clear();
            var report = new StringBuilder();
            report.AppendLine("# Grill Sort level report");
            report.AppendLine();
            report.AppendLine($"Move rule: **{rule}** (Sort Streak style: food hangs from the top of each tray, drag any piece and the pieces below it come along, drop only where the bottom piece matches). One tray per customer holding exactly that order (4 or 5 pieces) + {LevelGenerator.Helpers} helper trays without customers. Every level was solved by the A* solver (admissible heuristic, so par is the true minimum unless marked `~`), and the solution was replayed on a fresh board to confirm it ends with every order served.");
            report.AppendLine();
            report.AppendLine("| Level | Customers | Pieces per order | Look-alike pairs | Target par | Par (min moves) | Bar |");
            report.AppendLine("|---|---|---|---|---|---|---|");
            var sw = Stopwatch.StartNew();
            int prevPar = 0, failures = 0;
            try
            {
                for (int n = 1; n <= count; n++)
                {
                    EditorUtility.DisplayProgressBar("Grill Sort", $"Baking level {n}/{count}", (float)n / count);
                    var lv = LevelGenerator.Generate(n, prevPar, rule);
                    if (lv == null) { failures++; Debug.LogError($"[Grill Sort] Level {n}: no solvable candidate"); continue; }
                    // independent verification: re-solve and replay
                    var r = Solver.Solve(lv.ToBoard(), rule);
                    bool ok = r.Solvable && Replay(lv, r, rule) && (!lv.parOptimal || (r.Optimal && r.Par == lv.par));
                    if (!ok) { failures++; Debug.LogError($"[Grill Sort] Level {n} failed verification"); continue; }
                    bank.levels.Add(lv);
                    prevPar = lv.par;
                    report.AppendLine($"| {n} | {lv.foodTypes} | {lv.pieces} | {lv.lookAlikePairs} | {LevelGenerator.TierFor(n).TargetPar} | {lv.par}{(lv.parOptimal ? "" : "~")} | {new string('#', lv.par)} |");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            report.AppendLine();
            report.AppendLine($"Baked {bank.levels.Count} levels in {sw.Elapsed.TotalSeconds:0.0} s, {failures} failures. Levels past {count} are generated at runtime with the same seeded scheme (and still solver-checked).");
            report.AppendLine("`~` = par from the weighted fallback search (an upper bound, not proven minimal).");
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/LevelReport.md", report.ToString());
            string summary = $"[Grill Sort] Baked {bank.levels.Count}/{count} levels ({failures} failures) in {sw.Elapsed.TotalSeconds:0.0}s. Report: Docs/LevelReport.md";
            if (failures > 0) Debug.LogError(summary); else Debug.Log(summary);
            return summary;
        }

        static bool Replay(LevelData lv, Solver.Result r, MoveRule rule)
        {
            var b = lv.ToBoard();
            int served = 0;
            foreach (var m in r.Moves)
            {
                if (!b.TryMakeMove(rule, m.From, rule == MoveRule.AnyWithAbove ? m.Index : -1, m.To, out var mm)) return false;
                if (mm.Index != m.Index || mm.Count != m.Count) return false;
                if (b.Apply(mm, out int fromServed) >= 0) served++;
                if (fromServed >= 0) served++;
            }
            int orders = 0;
            foreach (var o in lv.orders) if (o >= 0) orders++;
            return b.IsSolved() && served == orders;
        }
    }
}
