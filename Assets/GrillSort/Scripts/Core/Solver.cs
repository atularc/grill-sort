using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Solves a <see cref="Board"/>. Breadth-first first (gives the shortest solution = par), and when the node
    /// budget runs out falls back to a depth-first search that still proves solvability.
    /// </summary>
    public static class Solver
    {
        public sealed class Result
        {
            public bool Solvable;
            /// <summary>True when the search ran out of budget before deciding.</summary>
            public bool Exhausted;
            /// <summary>True when <see cref="Moves"/> is a shortest solution.</summary>
            public bool Optimal;
            public List<Move> Moves = new List<Move>();
            public int Nodes;
            public int Par => Moves.Count;
        }

        struct Node { public int Parent; public Move Move; }

        public static Result Solve(Board start, MoveRule rule, int bfsBudget = 250000, int dfsBudget = 400000)
        {
            var r = new Result();
            if (start.IsSolved()) { r.Solvable = true; r.Optimal = true; return r; }
            if (Bfs(start, rule, bfsBudget, r)) return r;
            Dfs(start, rule, dfsBudget, r);
            return r;
        }

        /// <summary>Returns true when BFS decided the answer (solved or proved unsolvable).</summary>
        static bool Bfs(Board start, MoveRule rule, int budget, Result r)
        {
            var seen = new HashSet<string> { start.Key() };
            var nodes = new List<Node> { new Node { Parent = -1 } };
            var boards = new List<Board> { start.Clone() };
            var moves = new List<Move>();
            int head = 0;
            while (head < boards.Count)
            {
                var b = boards[head];
                boards[head] = null; // free memory as we go
                b.LegalMoves(rule, moves, true);
                foreach (var m in moves)
                {
                    var nb = b.Clone();
                    nb.Apply(m);
                    var key = nb.Key();
                    if (!seen.Add(key)) continue;
                    nodes.Add(new Node { Parent = head, Move = m });
                    if (nb.IsSolved())
                    {
                        r.Solvable = true; r.Optimal = true; r.Nodes = nodes.Count;
                        var path = new List<Move>();
                        for (int i = nodes.Count - 1; nodes[i].Parent >= 0; i = nodes[i].Parent) path.Add(nodes[i].Move);
                        path.Reverse();
                        r.Moves = path;
                        return true;
                    }
                    boards.Add(nb);
                    if (nodes.Count > budget) { r.Nodes = nodes.Count; return false; }
                }
                head++;
            }
            r.Solvable = false; r.Nodes = nodes.Count;
            return true;
        }

        static void Dfs(Board start, MoveRule rule, int budget, Result r)
        {
            var seen = new HashSet<string>();
            var path = new List<Move>();
            int nodes = 0;
            bool exhausted = false;

            bool Rec(Board b, int depth)
            {
                if (b.IsSolved()) return true;
                if (depth > 400) return false;
                if (++nodes > budget) { exhausted = true; return false; }
                if (!seen.Add(b.Key())) return false;
                var moves = new List<Move>();
                b.LegalMoves(rule, moves, true);
                // try moves that stack onto a matching food first, then into empties
                moves.Sort((x, y) => Score(b, y).CompareTo(Score(b, x)));
                foreach (var m in moves)
                {
                    var nb = b.Clone();
                    nb.Apply(m);
                    path.Add(m);
                    if (Rec(nb, depth + 1)) return true;
                    path.RemoveAt(path.Count - 1);
                    if (exhausted) return false;
                }
                return false;
            }

            bool ok = Rec(start.Clone(), 0);
            r.Nodes += nodes;
            r.Solvable = ok;
            r.Exhausted = !ok && exhausted;
            r.Optimal = false;
            if (ok) r.Moves = new List<Move>(path);
        }

        static int Score(Board b, Move m)
        {
            int s = 0;
            if (!b.IsEmpty(m.To)) s += 10 + b.Count(m.To);
            if (m.Index == 0) s += 5; // empties the source
            return s;
        }

        /// <summary>Next move toward a solution (for the Hint booster), or null when none is found.</summary>
        public static Move? NextMove(Board b, MoveRule rule)
        {
            var r = Solve(b, rule, 40000, 120000);
            if (r.Solvable && r.Moves.Count > 0) return r.Moves[0];
            return null;
        }
    }
}
