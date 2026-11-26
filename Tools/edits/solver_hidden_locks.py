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


edit("Solver.cs", [
# ---- fields
("""            int[] _qTgt = new int[64];
            Key _startKey;""",
"""            int[] _qTgt = new int[64];
            bool[][] _qHid = new bool[64][];
            int[] _qLock = new int[64];
            Key _startKey;"""),
("""            readonly int[] _tgt = new int[MaxSkewers];
            int _n, _qi;
            // the expanded node's row, restored before every move
            readonly int[] _bCells = new int[MaxSkewers * MaxCapacity];
            readonly int[] _bCnt = new int[MaxSkewers];
            readonly int[] _bTgt = new int[MaxSkewers];
            int _bN, _bQi;""",
"""            readonly int[] _tgt = new int[MaxSkewers];
            // wrapped pieces (hidden until they become a tray's bottom) and tray locks (local food id that unlocks, 0 = none)
            readonly bool[] _hid = new bool[MaxSkewers * MaxCapacity];
            readonly int[] _lock = new int[MaxSkewers];
            int _n, _qi;
            // the expanded node's row, restored before every move
            readonly int[] _bCells = new int[MaxSkewers * MaxCapacity];
            readonly int[] _bCnt = new int[MaxSkewers];
            readonly int[] _bTgt = new int[MaxSkewers];
            readonly bool[] _bHid = new bool[MaxSkewers * MaxCapacity];
            readonly int[] _bLock = new int[MaxSkewers];
            int _bN, _bQi;"""),
# ---- init
("""                Array.Clear(_cells, 0, _cells.Length);
                for (int s = 0; s < _n; s++)
                {
                    _cnt[s] = b.Count(s);
                    for (int i = 0; i < _cnt[s]; i++)
                    {
                        int f = Local(b.Get(s, i));
                        if (f == 0) return false;
                        _cells[s * MaxCapacity + i] = f;
                    }
                    int t = b.Target(s);
                    _tgt[s] = t >= 0 ? Local(t) : 0;
                    if (t >= 0 && _tgt[s] == 0) return false;
                }""",
"""                Array.Clear(_cells, 0, _cells.Length);
                Array.Clear(_hid, 0, _hid.Length);
                bool anyLock = false;
                for (int s = 0; s < _n; s++)
                {
                    _cnt[s] = b.Count(s);
                    for (int i = 0; i < _cnt[s]; i++)
                    {
                        int f = Local(b.Get(s, i));
                        if (f == 0) return false;
                        _cells[s * MaxCapacity + i] = f;
                        _hid[s * MaxCapacity + i] = b.IsHidden(s, i);
                    }
                    int t = b.Target(s);
                    _tgt[s] = t >= 0 ? Local(t) : 0;
                    if (t >= 0 && _tgt[s] == 0) return false;
                    int lk = b.Lock(s);
                    _lock[s] = lk >= 0 ? Local(lk) : 0;
                    if (lk >= 0) { anyLock = true; if (_lock[s] == 0) return false; }
                }"""),
("""                    if (_qLen == _qCells.Length) { Array.Resize(ref _qCells, _qLen * 2); Array.Resize(ref _qCnt, _qLen * 2); Array.Resize(ref _qTgt, _qLen * 2); }""",
"""                    if (_qLen == _qCells.Length) { Array.Resize(ref _qCells, _qLen * 2); Array.Resize(ref _qCnt, _qLen * 2); Array.Resize(ref _qTgt, _qLen * 2); Array.Resize(ref _qHid, _qLen * 2); Array.Resize(ref _qLock, _qLen * 2); }"""),
("""                    int t = b.QueueTarget(q);
                    _qTgt[_qLen] = t >= 0 ? Local(t) : 0;
                    _qLen++;
                }
                if (_qLen > 255) return false;""",
"""                    int t = b.QueueTarget(q);
                    _qTgt[_qLen] = t >= 0 ? Local(t) : 0;
                    var qh = _qHid[_qLen] ?? (_qHid[_qLen] = new bool[MaxCapacity]);
                    var bh = b.QueueHidden(q);
                    for (int i = 0; i < MaxCapacity; i++) qh[i] = i < bh.Length && bh[i];
                    int qlk = b.QueueLock(q);
                    _qLock[_qLen] = qlk >= 0 ? Local(qlk) : 0;
                    if (qlk >= 0) anyLock = true;
                    _qLen++;
                }
                if (_qLen > 255) return false;
                if (anyLock && _cap > 6) return false;   // the lock is packed into the 7th cell's bits of a tray code"""),
# ---- save / load
("""                Array.Copy(_cells, _bCells, _n * MaxCapacity);
                Array.Copy(_cnt, _bCnt, _n); Array.Copy(_tgt, _bTgt, _n);
                _bN = _n; _bQi = _qi;""",
"""                Array.Copy(_cells, _bCells, _n * MaxCapacity);
                Array.Copy(_hid, _bHid, _n * MaxCapacity);
                Array.Copy(_cnt, _bCnt, _n); Array.Copy(_tgt, _bTgt, _n); Array.Copy(_lock, _bLock, _n);
                _bN = _n; _bQi = _qi;"""),
("""                Array.Copy(_bCells, _cells, _bN * MaxCapacity);
                Array.Copy(_bCnt, _cnt, _bN); Array.Copy(_bTgt, _tgt, _bN);
                for (int s = _bN; s < _n; s++) { _cnt[s] = 0; _tgt[s] = 0; }""",
"""                Array.Copy(_bCells, _cells, _bN * MaxCapacity);
                Array.Copy(_bHid, _hid, _bN * MaxCapacity);
                Array.Copy(_bCnt, _cnt, _bN); Array.Copy(_bTgt, _tgt, _bN); Array.Copy(_bLock, _lock, _bN);
                for (int s = _bN; s < _n; s++) { _cnt[s] = 0; _tgt[s] = 0; _lock[s] = 0; }"""),
("""            bool Complete(int s) => _tgt[s] != 0 && _cnt[s] == _pieces && Settled(s) == _pieces;""",
"""            bool Complete(int s) => _tgt[s] != 0 && _lock[s] == 0 && _cnt[s] == _pieces && Settled(s) == _pieces;

            void RevealBottom(int s) { if (_cnt[s] > 0) _hid[s * MaxCapacity + _cnt[s] - 1] = false; }"""),
# ---- serve
("""            void ServeAt(int s)
            {
                for (int t = s; t < _n - 1; t++)
                {
                    Array.Copy(_cells, (t + 1) * MaxCapacity, _cells, t * MaxCapacity, MaxCapacity);
                    _cnt[t] = _cnt[t + 1]; _tgt[t] = _tgt[t + 1];
                }
                _n--;
                int lb = _n * MaxCapacity;
                for (int i = 0; i < MaxCapacity; i++) _cells[lb + i] = 0;
                _cnt[_n] = 0; _tgt[_n] = 0;
                if (_qi < _qLen)
                {
                    var q = _qCells[_qi];
                    for (int i = 0; i < _qCnt[_qi]; i++) _cells[lb + i] = q[i];
                    _cnt[_n] = _qCnt[_qi]; _tgt[_n] = _qTgt[_qi];
                    _n++; _qi++;
                }
            }""",
"""            void ServeAt(int s)
            {
                int food = _tgt[s];
                for (int t = s; t < _n - 1; t++)
                {
                    Array.Copy(_cells, (t + 1) * MaxCapacity, _cells, t * MaxCapacity, MaxCapacity);
                    Array.Copy(_hid, (t + 1) * MaxCapacity, _hid, t * MaxCapacity, MaxCapacity);
                    _cnt[t] = _cnt[t + 1]; _tgt[t] = _tgt[t + 1]; _lock[t] = _lock[t + 1];
                }
                _n--;
                int lb = _n * MaxCapacity;
                for (int i = 0; i < MaxCapacity; i++) { _cells[lb + i] = 0; _hid[lb + i] = false; }
                _cnt[_n] = 0; _tgt[_n] = 0; _lock[_n] = 0;
                // serving this food opens every tray locked by it
                for (int t = 0; t < _n; t++) if (_lock[t] == food) _lock[t] = 0;
                if (_qi < _qLen)
                {
                    var q = _qCells[_qi]; var qh = _qHid[_qi];
                    for (int i = 0; i < _qCnt[_qi]; i++) { _cells[lb + i] = q[i]; _hid[lb + i] = qh[i]; }
                    _cnt[_n] = _qCnt[_qi]; _tgt[_n] = _qTgt[_qi]; _lock[_n] = _qLock[_qi];
                    RevealBottom(_n);
                    _n++; _qi++;
                }
            }"""),
# ---- move loop
("""                        int n = _bCnt[from];
                        if (n == 0) continue;
                        int fb = from * MaxCapacity;
                        int lo = LowStart(from, n);""",
"""                        int n = _bCnt[from];
                        if (n == 0 || _bLock[from] != 0) continue;
                        int fb = from * MaxCapacity;
                        int lo = LowStart(from, n);"""),
("""                                if (to == from) continue;
                                int count = Count(from, start, n, food, group, to, ref triedFree);
                                if (count <= 0) continue;
                                if (!FillsUniform(fb, n - count, count, to)) continue;   // full tray = one food (Sort Streak)
                                // apply on the working row (it holds the base row here)
                                int ct = _cnt[to], tb = to * MaxCapacity;
                                for (int k = 0; k < count; k++) _cells[tb + ct + k] = _cells[fb + n - count + k];
                                for (int k = n - count; k < n; k++) _cells[fb + k] = 0;
                                _cnt[from] = n - count; _cnt[to] = ct + count;""",
"""                                if (to == from || _lock[to] != 0) continue;
                                int count = Count(from, start, n, food, group, to, ref triedFree);
                                if (count <= 0) continue;
                                if (!FillsUniform(fb, n - count, count, to)) continue;   // full tray = one food (Sort Streak)
                                if (_cnt[to] > 0 && HiddenIn(fb + n - count, count)) continue;   // wrapped food only onto an empty tray
                                // apply on the working row (it holds the base row here)
                                int ct = _cnt[to], tb = to * MaxCapacity;
                                for (int k = 0; k < count; k++) { _cells[tb + ct + k] = _cells[fb + n - count + k]; _hid[tb + ct + k] = _hid[fb + n - count + k]; }
                                for (int k = n - count; k < n; k++) { _cells[fb + k] = 0; _hid[fb + k] = false; }
                                _cnt[from] = n - count; _cnt[to] = ct + count;
                                RevealBottom(from); RevealBottom(to);"""),
("""            /// <summary>Board.FillsUniform on the working row: a move that fills the target tray must leave it one food.</summary>""",
"""            bool HiddenIn(int at, int count)
            {
                for (int k = 0; k < count; k++) if (_hid[at + k]) return true;
                return false;
            }

            /// <summary>Board.FillsUniform on the working row: a move that fills the target tray must leave it one food.</summary>"""),
# ---- reducing-move check respects locks (keeps h admissible either way)
("""                    int n = _cnt[from];
                    if (n == 0) continue;
                    int fb = from * MaxCapacity;
                    int settled = Settled(from);""",
"""                    int n = _cnt[from];
                    if (n == 0 || _lock[from] != 0) continue;
                    int fb = from * MaxCapacity;
                    int settled = Settled(from);"""),
("""                            if (to == from) continue;
                            int ct = _cnt[to];
                            int room = _cap - ct;""",
"""                            if (to == from || _lock[to] != 0) continue;
                            int ct = _cnt[to];
                            int room = _cap - ct;"""),
# ---- encode / decode: lock in bits 24..27 of the tray code (cap <= 6), hidden flags in D bits 16+
("""                    for (int i = 0; i < _cnt[s]; i++) code |= (ulong)_cells[b + i] << (i * 4);
                    code |= (ulong)_tgt[s] << 28;""",
"""                    for (int i = 0; i < _cnt[s]; i++) code |= (ulong)_cells[b + i] << (i * 4);
                    code |= (ulong)_lock[s] << 24;
                    code |= (ulong)_tgt[s] << 28;"""),
("""                k.D = (ulong)_n | ((ulong)_qi << 8);
                return k;""",
"""                ulong d = (ulong)_n | ((ulong)_qi << 8);
                for (int s = 0; s < _n; s++)
                    for (int i = 0; i < _cnt[s] && i < 7; i++)
                        if (_hid[s * MaxCapacity + i]) d |= 1UL << (16 + s * 7 + i);
                k.D = d;
                return k;"""),
("""                _n = (int)(k.D & 0xFF); _qi = (int)(k.D >> 8);""",
"""                _n = (int)(k.D & 0xFF); _qi = (int)((k.D >> 8) & 0xFF);"""),
("""                    while (c < _cap && ((code >> (c * 4)) & 0xF) != 0) { _cells[b + c] = (int)((code >> (c * 4)) & 0xF); c++; }
                    for (int i = c; i < MaxCapacity; i++) _cells[b + i] = 0;
                    _cnt[s] = c;
                    _tgt[s] = (int)((code >> 28) & 0xF);""",
"""                    int lim = Math.Min(_cap, 6);
                    while (c < lim && ((code >> (c * 4)) & 0xF) != 0) { _cells[b + c] = (int)((code >> (c * 4)) & 0xF); c++; }
                    if (_cap > 6 && c == 6 && ((code >> 24) & 0xF) != 0) { _cells[b + 6] = (int)((code >> 24) & 0xF); c = 7; }
                    for (int i = c; i < MaxCapacity; i++) _cells[b + i] = 0;
                    _cnt[s] = c;
                    _tgt[s] = (int)((code >> 28) & 0xF);
                    _lock[s] = _cap > 6 ? 0 : (int)((code >> 24) & 0xF);
                    for (int i = 0; i < MaxCapacity; i++) _hid[b + i] = i < c && i < 7 && ((k.D >> (16 + s * 7 + i)) & 1) != 0;"""),
])
print("ok")
