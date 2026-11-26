using System;
using System.Collections.Generic;
using System.Text;

namespace GrillSort
{
    /// <summary>Which pieces move when a tray is picked up.</summary>
    public enum MoveRule
    {
        /// <summary>Classic Water Sort: only the top run of identical pieces moves.</summary>
        TopGroup,
        /// <summary>Grab any piece; everything above it comes along (default, Sort Streak style).</summary>
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

    /// <summary>One order served by a move: the tray at <see cref="Slot"/> left the row (the trays after it moved one
    /// slot left) and, when <see cref="Arrived"/>, the next queued tray was appended at the end of the row.</summary>
    public struct Served
    {
        public int Slot, Food;
        public bool Arrived;
    }

    /// <summary>
    /// Pure puzzle state, Sort Streak style with an order queue (user 2026-11-22, from the Sort Streak "rows" video):
    /// a ROW of trays (the ones on screen), each a stack of food ids (index 0 = fixed end) with room for
    /// <see cref="Capacity"/> pieces and the customer's order (target food) above it; plus a QUEUE of trays still to
    /// come. A tray holding exactly <see cref="Pieces"/> pieces of its own target food (nothing else) is served: it
    /// LEAVES the row, the trays after it move one slot left, and the next queued tray (new customer + mixed food)
    /// joins at the right end. With no queue left the row just gets shorter. Every tray has a customer; free room is
    /// the spare slot each tray has (Capacity = Pieces + 1) and part-empty trays. Trays without a target (the Extra
    /// tray booster) are plain free space and never leave.
    /// </summary>
    public sealed class Board
    {
        /// <summary>Slots per tray.</summary>
        public readonly int Capacity;
        /// <summary>Pieces of each food (= pieces needed to serve an order).</summary>
        public readonly int Pieces;
        readonly List<int[]> _cells = new List<int[]>();
        readonly List<int> _count = new List<int>();
        readonly List<int> _target = new List<int>();
        readonly List<int[]> _queueCells = new List<int[]>();
        readonly List<int> _queueTarget = new List<int>();
        int _queueNext;

        public Board(int capacity, int pieces = 4) { Capacity = capacity; Pieces = pieces; }

        /// <summary>Adds a tray to the queue (it joins the row when an order is served).</summary>
        public void AddQueued(IList<int> bottomToTop, int target)
        {
            var arr = new int[bottomToTop.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = bottomToTop[i];
            _queueCells.Add(arr); _queueTarget.Add(target);
        }

        public int QueueCount => _queueCells.Count;
        /// <summary>Index of the next queued tray to arrive.</summary>
        public int QueueNext => _queueNext;
        public int QueueLeft => _queueCells.Count - _queueNext;
        public int QueueTarget(int i) => _queueTarget[i];
        public int[] QueueStack(int i) => (int[])_queueCells[i].Clone();

        /// <summary>Orders this level has in total (row + queue), served or not.</summary>
        public int OrdersLeft
        {
            get
            {
                int n = QueueLeft;
                foreach (var t in _target) if (t >= 0) n++;
                return n;
            }
        }

        public int SkewerCount => _cells.Count;
        public int Count(int s) => _count[s];
        public int Get(int s, int i) => _cells[s][i];
        public int Top(int s) => _count[s] == 0 ? -1 : _cells[s][_count[s] - 1];
        public bool IsEmpty(int s) => _count[s] == 0;
        /// <summary>Food this tray's customer ordered, or -1 (served / extra tray).</summary>
        public int Target(int s) => _target[s];

        public int AddSkewer(IList<int> bottomToTop = null, int target = -1)
        {
            var arr = new int[Capacity];
            for (int i = 0; i < Capacity; i++) arr[i] = -1;
            int n = 0;
            if (bottomToTop != null) foreach (var f in bottomToTop) arr[n++] = f;
            _cells.Add(arr); _count.Add(n); _target.Add(target);
            return _cells.Count - 1;
        }

        public Board Clone()
        {
            var b = new Board(Capacity, Pieces);
            for (int s = 0; s < _cells.Count; s++) { b._cells.Add((int[])_cells[s].Clone()); b._count.Add(_count[s]); b._target.Add(_target[s]); }
            for (int q = 0; q < _queueCells.Count; q++) { b._queueCells.Add(_queueCells[q]); b._queueTarget.Add(_queueTarget[q]); }
            b._queueNext = _queueNext;
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

        /// <summary>Pieces of the tray's own target food sitting at its bottom (already in place).</summary>
        public int Settled(int s)
        {
            int t = _target[s];
            if (t < 0) return 0;
            int k = 0;
            while (k < _count[s] && _cells[s][k] == t) k++;
            return k;
        }

        public bool IsComplete(int s)
        {
            int t = _target[s];
            return t >= 0 && _count[s] == Pieces && Settled(s) == Pieces;
        }

        public bool IsSolved()
        {
            if (QueueLeft > 0) return false;
            for (int s = 0; s < _cells.Count; s++) if (_count[s] != 0 || _target[s] >= 0) return false;
            return true;
        }

        /// <summary>
        /// Builds the move from tray <paramref name="from"/> (pieces starting at <paramref name="index"/>, or the
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
            else { if (group > room) return false; count = group; }        // the whole stack above must fit
            if (!FillsUniform(from, n - count, count, to)) return false;
            move = new Move(from, to, n - count, count);
            return true;
        }

        /// <summary>
        /// Sort Streak rule (research 2026-11-26, Research/SortStreak/Report.md): a tray may only be filled to the top
        /// with ONE food. A move that would fill the target tray completely is legal only if every piece on it
        /// afterwards (the ones there and the ones arriving) is the same food. Stops dead ends built from full,
        /// mixed trays.
        /// </summary>
        public bool FillsUniform(int from, int index, int count, int to)
        {
            if (_count[to] + count < Capacity) return true;
            int food = _cells[from][index];
            for (int k = 0; k < count; k++) if (_cells[from][index + k] != food) return false;
            for (int k = 0; k < _count[to]; k++) if (_cells[to][k] != food) return false;
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

        /// <summary>
        /// Applies a move and serves whatever it completed. The source tray is checked first (lifting the last wrong
        /// piece off a tray can leave it holding exactly its order), then the destination. Each serve removes the tray
        /// from the row (later trays shift one slot left) and appends the next queued tray. Returns the serves in the
        /// order they happened; each Slot is the row index at that moment. The Solver applies moves the same way.
        /// </summary>
        public List<Served> Apply(Move m)
        {
            var src = _cells[m.From]; var dst = _cells[m.To];
            for (int k = 0; k < m.Count; k++) dst[_count[m.To]++] = src[m.Index + k];
            for (int k = m.Index; k < m.Index + m.Count; k++) src[k] = -1;
            _count[m.From] = m.Index;
            var r = new List<Served>(2);
            int to = m.To;
            if (IsComplete(m.From))
            {
                r.Add(ServeAt(m.From));
                if (to > m.From) to--;
            }
            if (IsComplete(to)) r.Add(ServeAt(to));
            return r;
        }

        Served ServeAt(int s)
        {
            var e = new Served { Slot = s, Food = _target[s] };
            _cells.RemoveAt(s); _count.RemoveAt(s); _target.RemoveAt(s);
            if (_queueNext < _queueCells.Count)
            {
                AddSkewer(_queueCells[_queueNext], _queueTarget[_queueNext]);
                _queueNext++;
                e.Arrived = true;
            }
            return e;
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
                    bool triedFree = false;
                    for (int to = 0; to < sk; to++)
                    {
                        if (to == from) continue;
                        bool freeEmpty = _count[to] == 0 && _target[to] < 0;
                        if (freeEmpty)
                        {
                            if (triedFree) continue;                                  // all empty free trays are the same
                            if (prune && start == 0 && _target[from] < 0) continue;   // free tray to free tray: pointless
                        }
                        if (!TryMakeMove(rule, from, start, to, out var m)) continue;
                        if (rule == MoveRule.AnyWithAbove && m.Index != start) continue;
                        if (freeEmpty) triedFree = true;
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

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int s = 0; s < _cells.Count; s++)
            {
                sb.Append(_target[s] >= 0 ? $"{_target[s]}:[" : "-:[");
                for (int i = 0; i < _count[s]; i++) { if (i > 0) sb.Append(','); sb.Append(_cells[s][i]); }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
