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
        public const int LevelCount = 30;
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
            report.AppendLine($"Move rule: **{rule}**. Capacity 4. Every level was solved by the A* solver (admissible heuristic, so par is the true minimum unless marked `~`), and the solution was replayed on a fresh board to confirm it ends with every order served.");
            report.AppendLine();
            report.AppendLine("| Level | Foods | Empty | Skewers | Look-alike pairs | Target par | Par (min moves) | Also solvable with other rule | Bar |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|");
            var sw = Stopwatch.StartNew();
            int prevPar = 0, failures = 0;
            var other = rule == MoveRule.TopGroup ? MoveRule.AnyWithAbove : MoveRule.TopGroup;
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
                    var r2 = Solver.Solve(lv.ToBoard(), other, 100000, 200000);
                    bank.levels.Add(lv);
                    prevPar = lv.par;
                    report.AppendLine($"| {n} | {lv.foodTypes} | {lv.empties} | {lv.skewers} | {lv.lookAlikePairs} | {LevelGenerator.TierFor(n).TargetPar} | {lv.par}{(lv.parOptimal ? "" : "~")} | {(r2.Solvable ? "yes" : "no")} | {new string('#', lv.par)} |");
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
            foreach (var m in r.Moves)
            {
                if (!b.TryMakeMove(rule, m.From, rule == MoveRule.AnyWithAbove ? m.Index : -1, m.To, out var mm)) return false;
                if (mm.Index != m.Index || mm.Count != m.Count) return false;
                b.Apply(mm);
            }
            return b.IsSolved();
        }
    }
}
