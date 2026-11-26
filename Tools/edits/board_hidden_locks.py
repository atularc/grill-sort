import io, os

CORE = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GrillSort", "Scripts", "Core")


def edit(name, pairs):
    p = os.path.join(CORE, name)
    s = io.open(p, encoding="utf-8", newline="").read()
    crlf = "\r\n" in s
    s = s.replace("\r\n", "\n")
    for a, b in pairs:
        assert a in s, (name, a[:70])
        s = s.replace(a, b, 1)
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(p, "w", encoding="utf-8", newline="").write(s)


edit("Board.cs", [
("""        readonly List<int[]> _queueCells = new List<int[]>();
        readonly List<int> _queueTarget = new List<int>();
        int _queueNext;""",
"""        readonly List<int[]> _queueCells = new List<int[]>();
        readonly List<int> _queueTarget = new List<int>();
        int _queueNext;
        // wrapped "?" food (Sort Streak mystery flowers): hidden until it becomes a tray's bottom (free end) piece
        readonly List<bool[]> _hid = new List<bool[]>();
        readonly List<bool[]> _queueHid = new List<bool[]>();
        // locked trays (Sort Streak locked flowers): unusable until an order of _lock[s] is served; -1 = not locked
        readonly List<int> _lock = new List<int>();
        readonly List<int> _queueLock = new List<int>();"""),
("""        public void AddQueued(IList<int> bottomToTop, int target)
        {
            var arr = new int[bottomToTop.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = bottomToTop[i];
            _queueCells.Add(arr); _queueTarget.Add(target);
        }""",
"""        public void AddQueued(IList<int> bottomToTop, int target, IList<bool> hidden = null, int lockFood = -1)
        {
            var arr = new int[bottomToTop.Count];
            var hid = new bool[bottomToTop.Count];
            for (int i = 0; i < arr.Length; i++) { arr[i] = bottomToTop[i]; hid[i] = hidden != null && i < hidden.Count && hidden[i]; }
            _queueCells.Add(arr); _queueTarget.Add(target); _queueHid.Add(hid); _queueLock.Add(lockFood);
        }

        public bool[] QueueHidden(int i) => (bool[])_queueHid[i].Clone();
        public int QueueLock(int i) => _queueLock[i];"""),
("""        public int Target(int s) => _target[s];

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
        }""",
"""        public int Target(int s) => _target[s];
        /// <summary>Piece i of tray s is wrapped (its food is not shown yet).</summary>
        public bool IsHidden(int s, int i) => _hid[s][i];
        /// <summary>Food whose served order unlocks tray s, or -1.</summary>
        public int Lock(int s) => _lock[s];
        public bool IsLocked(int s) => _lock[s] >= 0;

        public int AddSkewer(IList<int> bottomToTop = null, int target = -1, IList<bool> hidden = null, int lockFood = -1)
        {
            var arr = new int[Capacity];
            var hid = new bool[Capacity];
            for (int i = 0; i < Capacity; i++) arr[i] = -1;
            int n = 0;
            if (bottomToTop != null) foreach (var f in bottomToTop) { hid[n] = hidden != null && n < hidden.Count && hidden[n]; arr[n++] = f; }
            _cells.Add(arr); _count.Add(n); _target.Add(target); _hid.Add(hid); _lock.Add(lockFood);
            RevealBottom(_cells.Count - 1);
            return _cells.Count - 1;
        }

        /// <summary>The bottom (free-end) piece is always visible: reveals it. Returns true if it was wrapped.</summary>
        public bool RevealBottom(int s)
        {
            int n = _count[s];
            if (n == 0 || !_hid[s][n - 1]) return false;
            _hid[s][n - 1] = false;
            return true;
        }

        public Board Clone()
        {
            var b = new Board(Capacity, Pieces);
            for (int s = 0; s < _cells.Count; s++)
            {
                b._cells.Add((int[])_cells[s].Clone()); b._count.Add(_count[s]); b._target.Add(_target[s]);
                b._hid.Add((bool[])_hid[s].Clone()); b._lock.Add(_lock[s]);
            }
            for (int q = 0; q < _queueCells.Count; q++) { b._queueCells.Add(_queueCells[q]); b._queueTarget.Add(_queueTarget[q]); b._queueHid.Add(_queueHid[q]); b._queueLock.Add(_queueLock[q]); }
            b._queueNext = _queueNext;
            return b;
        }"""),
("""            int t = _target[s];
            return t >= 0 && _count[s] == Pieces && Settled(s) == Pieces;""",
"""            int t = _target[s];
            return t >= 0 && _lock[s] < 0 && _count[s] == Pieces && Settled(s) == Pieces;"""),
("""            if (from == to || from < 0 || to < 0 || from >= _cells.Count || to >= _cells.Count) return false;
            int n = _count[from];
            if (n == 0) return false;""",
"""            if (from == to || from < 0 || to < 0 || from >= _cells.Count || to >= _cells.Count) return false;
            if (_lock[from] >= 0 || _lock[to] >= 0) return false;   // a locked tray can't be used until its order is served
            int n = _count[from];
            if (n == 0) return false;"""),
("""            if (!FillsUniform(from, n - count, count, to)) return false;
            move = new Move(from, to, n - count, count);""",
"""            if (!FillsUniform(from, n - count, count, to)) return false;
            if (_count[to] > 0 && HasHidden(from, n - count, count)) return false;   // wrapped food only onto an empty tray
            move = new Move(from, to, n - count, count);"""),
("""        public bool FillsUniform(int from, int index, int count, int to)""",
"""        /// <summary>Any wrapped piece among <paramref name="count"/> pieces of tray s from <paramref name="index"/>.</summary>
        public bool HasHidden(int s, int index, int count)
        {
            for (int k = 0; k < count; k++) if (_hid[s][index + k]) return true;
            return false;
        }

        public bool FillsUniform(int from, int index, int count, int to)"""),
("""            var src = _cells[m.From]; var dst = _cells[m.To];
            for (int k = 0; k < m.Count; k++) dst[_count[m.To]++] = src[m.Index + k];
            for (int k = m.Index; k < m.Index + m.Count; k++) src[k] = -1;
            _count[m.From] = m.Index;""",
"""            var src = _cells[m.From]; var dst = _cells[m.To];
            var sh = _hid[m.From]; var dh = _hid[m.To];
            for (int k = 0; k < m.Count; k++) { dh[_count[m.To]] = sh[m.Index + k]; dst[_count[m.To]++] = src[m.Index + k]; }
            for (int k = m.Index; k < m.Index + m.Count; k++) { src[k] = -1; sh[k] = false; }
            _count[m.From] = m.Index;
            RevealBottom(m.From); RevealBottom(m.To);"""),
("""            var e = new Served { Slot = s, Food = _target[s] };
            _cells.RemoveAt(s); _count.RemoveAt(s); _target.RemoveAt(s);
            if (_queueNext < _queueCells.Count)
            {
                AddSkewer(_queueCells[_queueNext], _queueTarget[_queueNext]);""",
"""            var e = new Served { Slot = s, Food = _target[s] };
            _cells.RemoveAt(s); _count.RemoveAt(s); _target.RemoveAt(s); _hid.RemoveAt(s); _lock.RemoveAt(s);
            // serving an order opens every tray locked by that food
            for (int t = 0; t < _lock.Count; t++) if (_lock[t] == e.Food) { _lock[t] = -1; e.Unlocked++; }
            if (_queueNext < _queueCells.Count)
            {
                AddSkewer(_queueCells[_queueNext], _queueTarget[_queueNext], _queueHid[_queueNext], _queueLock[_queueNext]);"""),
("""        public int Slot, Food;
        public bool Arrived;""",
"""        public int Slot, Food;
        public bool Arrived;
        /// <summary>How many locked trays this serve opened.</summary>
        public int Unlocked;"""),
("""            for (int i = 0; i < Capacity; i++) _cells[s][i] = -1;
            _count[s] = 0;""",
"""            for (int i = 0; i < Capacity; i++) { _cells[s][i] = -1; _hid[s][i] = false; }
            _count[s] = 0;"""),
])

edit("LevelData.cs", [
("""        public int[] queueOrders = new int[0];
        public int[] queueCells = new int[0];""",
"""        public int[] queueOrders = new int[0];
        public int[] queueCells = new int[0];
        /// <summary>Wrapped "?" food: same layout as cells / queueCells, 1 = hidden until it becomes the tray's bottom piece.</summary>
        public int[] hidden = new int[0];
        public int[] queueHidden = new int[0];
        /// <summary>Locked trays: per row tray / queued tray, the food whose served order unlocks it, or -1.</summary>
        public int[] locks = new int[0];
        public int[] queueLocks = new int[0];
        /// <summary>"Hard" level (Sort Streak: every 5th level): label, start card, x2 coins.</summary>
        public bool hard;
        /// <summary>Tier name shown in the 5-level demo build ("Easy" ... "Super Hard"), empty otherwise.</summary>
        public string tier = "";"""),
("""                var stack = new System.Collections.Generic.List<int>();
                for (int i = 0; i < capacity; i++)
                {
                    int f = cells[s * capacity + i];
                    if (f >= 0) stack.Add(f);
                }
                b.AddSkewer(stack, orders != null && s < orders.Length ? orders[s] : -1);""",
"""                var stack = new System.Collections.Generic.List<int>();
                var hid = new System.Collections.Generic.List<bool>();
                for (int i = 0; i < capacity; i++)
                {
                    int f = cells[s * capacity + i];
                    if (f >= 0) { stack.Add(f); hid.Add(hidden != null && s * capacity + i < hidden.Length && hidden[s * capacity + i] != 0); }
                }
                b.AddSkewer(stack, orders != null && s < orders.Length ? orders[s] : -1, hid,
                    locks != null && s < locks.Length ? locks[s] : -1);"""),
("""                    var stack = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < capacity; i++) { int f = queueCells[q * capacity + i]; if (f >= 0) stack.Add(f); }
                    b.AddQueued(stack, queueOrders[q]);""",
"""                    var stack = new System.Collections.Generic.List<int>();
                    var hid = new System.Collections.Generic.List<bool>();
                    for (int i = 0; i < capacity; i++)
                    {
                        int f = queueCells[q * capacity + i];
                        if (f >= 0) { stack.Add(f); hid.Add(queueHidden != null && q * capacity + i < queueHidden.Length && queueHidden[q * capacity + i] != 0); }
                    }
                    b.AddQueued(stack, queueOrders[q], hid, queueLocks != null && q < queueLocks.Length ? queueLocks[q] : -1);"""),
])
print("ok")
