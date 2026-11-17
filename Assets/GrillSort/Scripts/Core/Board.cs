using System;
using System.Collections.Generic;
using System.Text;

namespace GrillSort
{
    /// <summary>Which pieces move when a skewer is picked up.</summary>
    public enum MoveRule
    {
        /// <summary>Classic Water Sort: only the top run of identical pieces moves (default).</summary>
        TopGroup,
        /// <summary>Grab any piece; everything above it comes along.</summary>
        AnyWithAbove,
        /// <summary>Only the single top piece moves, even if the one below is the same food.</summary>
        SingleTop,
    }

    /// <summary>One move: pieces from <see cref="From"/> starting at stack index <see cref="Index"/> go to <see cref="To"/>.</summary>
    public struct Move
    {
        public int From, To, Index, Count;
        public Move(int from, int to, int index, int count) { From = from; To = to; Index = index; Count = count; }
        public override string ToString() => $"{From}[{Index}]x{Count}->{To}";
    }

    /// <summary>
    /// Pure puzzle state: N skewers, each a bottom-to-top stack of food ids (0..), capacity C.
    /// A skewer that becomes C identical pieces is complete: it is served and emptied (freed).
    /// </summary>
    public sealed class Board
    {
        public readonly int Capacity;
        readonly List<int[]> _cells = new List<int[]>();
        readonly List<int> _count = new List<int>();

        public Board(int capacity) { Capacity = capacity; }

        public int SkewerCount => _cells.Count;
        public int Count(int s) => _count[s];
        public int Get(int s, int i) => _cells[s][i];
        public int Top(int s) => _count[s] == 0 ? -1 : _cells[s][_count[s] - 1];
        public bool IsEmpty(int s) => _count[s] == 0;

        public int AddSkewer(IList<int> bottomToTop = null)
        {
            var arr = new int[Capacity];
            for (int i = 0; i < Capacity; i++) arr[i] = -1;
            int n = 0;
            if (bottomToTop != null) foreach (var f in bottomToTop) arr[n++] = f;
            _cells.Add(arr); _count.Add(n);
            return _cells.Count - 1;
        }

        public Board Clone()
        {
            var b = new Board(Capacity);
            for (int s = 0; s < _cells.Count; s++) { b._cells.Add((int[])_cells[s].Clone()); b._count.Add(_count[s]); }
            return b;
        }

        public int[] Stack(int s)
        {
            var r = new int[_count[s]];
            Array.Copy(_cells[s], r, r.Length);
            return r;
        }

        /// <summary>Length of the top run of identical pieces.</summary>
        public int TopGroup(int s)
        {
            int n = _count[s];
            if (n == 0) return 0;
            int f = _cells[s][n - 1], k = 1;
            while (k < n && _cells[s][n - 1 - k] == f) k++;
            return k;
        }

        public bool IsComplete(int s)
        {
            if (_count[s] != Capacity) return false;
            int f = _cells[s][0];
            for (int i = 1; i < Capacity; i++) if (_cells[s][i] != f) return false;
            return true;
        }

        public bool IsSolved()
        {
            for (int s = 0; s < _cells.Count; s++) if (_count[s] != 0) return false;
            return true;
        }

        /// <summary>
        /// Builds the move from skewer <paramref name="from"/> (pieces starting at <paramref name="index"/>, or the
        /// rule's default when index &lt; 0) to <paramref name="to"/>. Returns false if it is not legal.
        /// </summary>
        public bool TryMakeMove(MoveRule rule, int from, int index, int to, out Move move)
        {
            move = default;
            if (from == to || from < 0 || to < 0 || from >= _cells.Count || to >= _cells.Count) return false;
            int n = _count[from];
            if (n == 0) return false;
            int start = MoveStart(rule, from, index);
            int moving = _cells[from][start];
            int room = Capacity - _count[to];
            if (room <= 0) return false;
            if (_count[to] > 0 && Top(to) != moving) return false;
            int group = n - start;
            int count;
            if (rule == MoveRule.SingleTop) count = 1;
            else if (rule == MoveRule.TopGroup) count = Math.Min(group, room);   // as many as fit
            else { if (group > room) return false; count = group; }        // the whole hanging stack must fit
            move = new Move(from, to, n - count, count);
            return true;
        }

        /// <summary>Index of the lowest piece that would be picked up.</summary>
        public int MoveStart(MoveRule rule, int from, int index)
        {
            int n = _count[from];
            if (rule == MoveRule.SingleTop) return n - 1;
            if (rule == MoveRule.TopGroup || index < 0 || index >= n) return n - TopGroup(from);
            return index;
        }

        /// <summary>Applies a move. Returns the food id of a skewer that just completed (and was emptied), or -1.</summary>
        public int Apply(Move m)
        {
            var src = _cells[m.From]; var dst = _cells[m.To];
            for (int k = 0; k < m.Count; k++)
            {
                dst[_count[m.To]++] = src[m.Index + k];
            }
            for (int k = m.Index; k < m.Index + m.Count; k++) src[k] = -1;
            // anything above the moved block (AnyWithAbove moves the block, but block always ends at top)
            _count[m.From] = m.Index;
            if (IsComplete(m.To))
            {
                int f = dst[0];
                Clear(m.To);
                return f;
            }
            return -1;
        }

        public void Clear(int s)
        {
            for (int i = 0; i < Capacity; i++) _cells[s][i] = -1;
            _count[s] = 0;
        }

        public void SetStack(int s, IList<int> bottomToTop)
        {
            Clear(s);
            foreach (var f in bottomToTop) _cells[s][_count[s]++] = f;
        }

        /// <summary>All legal moves under the rule (with obviously useless ones pruned when <paramref name="prune"/>).</summary>
        public void LegalMoves(MoveRule rule, List<Move> into, bool prune)
        {
            into.Clear();
            int sk = _cells.Count;
            for (int from = 0; from < sk; from++)
            {
                int n = _count[from];
                if (n == 0) continue;
                int lo = rule == MoveRule.AnyWithAbove ? 0 : MoveStart(rule, from, -1);
                for (int start = n - 1; start >= lo; start--)
                {
                    if (rule != MoveRule.AnyWithAbove && start != lo) continue;
                    bool triedEmpty = false;
                    for (int to = 0; to < sk; to++)
                    {
                        if (to == from) continue;
                        bool empty = _count[to] == 0;
                        if (empty)
                        {
                            if (triedEmpty) continue;        // all empty skewers are equivalent
                            if (prune && start == 0) continue; // moving a whole skewer into an empty one is pointless
                        }
                        if (!TryMakeMove(rule, from, start, to, out var m)) continue;
                        if (rule == MoveRule.AnyWithAbove && m.Index != start) continue;
                        if (empty) triedEmpty = true;
                        into.Add(m);
                    }
                }
            }
        }

        public bool HasAnyMove(MoveRule rule)
        {
            var l = new List<Move>();
            LegalMoves(rule, l, false);
            return l.Count > 0;
        }

        /// <summary>Order-independent key (skewer order does not matter for solving).</summary>
        public string Key()
        {
            var parts = new string[_cells.Count];
            var sb = new StringBuilder(Capacity);
            for (int s = 0; s < _cells.Count; s++)
            {
                sb.Clear();
                for (int i = 0; i < _count[s]; i++) sb.Append((char)('A' + _cells[s][i]));
                parts[s] = sb.ToString();
            }
            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int s = 0; s < _cells.Count; s++)
            {
                sb.Append('[');
                for (int i = 0; i < _count[s]; i++) { if (i > 0) sb.Append(','); sb.Append(_cells[s][i]); }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
