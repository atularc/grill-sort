using GrillSort;
var gc = UnityEngine.Object.FindFirstObjectByType<GameController>();
var f = typeof(GameController).GetField("_board", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var b = (Board)f.GetValue(gc);
var sw = System.Diagnostics.Stopwatch.StartNew();
var mv = Solver.NextMove(b, MoveRule.AnyWithAbove);
string s = "board: " + b + " | next: " + (mv.HasValue ? mv.Value.ToString() : "null") + " in " + sw.ElapsedMilliseconds + "ms";
if (mv.HasValue)
{
    var m = mv.Value;
    bool ok = b.TryMakeMove(MoveRule.AnyWithAbove, m.From, m.Index, m.To, out var mm);
    s += " | legal: " + ok + " " + mm + " locked(from)=" + b.IsLocked(m.From) + " hidden-in-group=" + b.HasHidden(m.From, m.Index, m.Count) + " toCount=" + b.Count(m.To);
}
var r = Solver.Solve(b.Clone(), MoveRule.AnyWithAbove, 150000, 700000);
s += " | full solve: " + r.Solvable + " par " + r.Par;
return s;
