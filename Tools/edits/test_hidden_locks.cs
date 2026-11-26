using System;
using System.Text;
using GrillSort;

// mode 0 = plain, 1 = wrapped food only, 2 = lock only (lock a customer tray by another order's food)
string Run(int mode)
{
    var sb = new StringBuilder();
    var rng = new System.Random(7);
    int tested = 0, solvedN = 0, replayOk = 0;
    for (int lvN = 4; lvN <= 24; lvN += 4)
    {
        var lv = LevelGenerator.Generate(lvN, 0, MoveRule.AnyWithAbove);
        if (lv == null) { sb.Append("L" + lvN + ":GEN-FAIL "); continue; }
        if (mode == 1)
        {
            lv.hidden = new int[lv.cells.Length];
            for (int i = 0; i < lv.cells.Length; i++) if (lv.cells[i] >= 0 && rng.NextDouble() < 0.2) lv.hidden[i] = 1;
        }
        if (mode == 2)
        {
            lv.locks = new int[lv.skewers];
            for (int s = 0; s < lv.skewers; s++) lv.locks[s] = -1;
            // lock the last customer tray, unlocked by serving the first customer's order
            int first = -1, last = -1;
            for (int s = 0; s < lv.skewers; s++) if (lv.orders[s] >= 0) { if (first < 0) first = s; last = s; }
            if (first >= 0 && last > first) lv.locks[last] = lv.orders[first];
        }
        tested++;
        var r = Solver.Solve(lv.ToBoard(), lv.rule, 600000, 300000);
        if (!r.Solvable) { sb.Append("L" + lvN + ":unsolvable" + (r.Exhausted ? "(budget) " : " ")); continue; }
        solvedN++;
        var b = lv.ToBoard();
        bool ok = true;
        foreach (var m in r.Moves)
        {
            Move mm;
            if (!b.TryMakeMove(lv.rule, m.From, m.Index, m.To, out mm) || mm.Index != m.Index || mm.Count != m.Count)
            { ok = false; sb.Append("L" + lvN + ":illegal " + m + " "); break; }
            b.Apply(mm);
        }
        if (ok && b.IsSolved()) replayOk++;
        else if (ok) sb.Append("L" + lvN + ":not-solved-after-replay ");
        sb.Append("L" + lvN + "=" + r.Par + (r.Optimal ? "" : "~") + " ");
    }
    return "mode " + mode + ": tested " + tested + " solved " + solvedN + " replay-ok " + replayOk + " | " + sb + "\n";
}

return Run(0) + Run(1) + Run(2);
