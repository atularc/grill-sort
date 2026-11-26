using System;
using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Solves a <see cref="Board"/> (a row of order trays plus a queue of trays still to come, Sort Streak style) with A*
    /// on a compact state. Serving a tray removes it, shifts the row left and appends the next queued tray, exactly as
    /// <see cref="Board.Apply"/> does, so solution moves replay on the Board.
    ///
    /// A piece is "settled" when it sits in the bottom run of its own order's tray. Heuristic
    /// h = number of runs (maximal blocks of the same food) among the unsettled pieces, +1 when no single move can lower
    /// that count. Each unsettled run needs at least one move, and one move lowers the count by at most one: the
    /// moved block's bottom run can either settle on its tray or merge onto a matching top, never both, and lifting
    /// a block can only split a run (+1). Serving a full tray removes settled pieces only (no change). So h is
    /// admissible and consistent, h = 0 only when every order is served, and A* gives the true minimum (par).
    /// When the exact search runs out of budget a weighted search (g + 3h) still finds a solution (not proven minimal).
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

        /// <summary>Largest row the compact search supports (5 order trays + 1 extra tray); any number of queued trays.</summary>
        public const int MaxSkewers = 6;
        /// <summary>Most slots per tray the compact search supports.</summary>
        public const int MaxCapacity = 7;
        const int MaxFoods = 15; // 4-bit local food ids, 0 = empty slot

        public static Result Solve(Board start, MoveRule rule, int exactBudget = 3000000, int fallbackBudget = 400000)
        {
            var r = new Result();
            if (start.IsSolved()) { r.Solvable = true; r.Optimal = true; return r; }
            var s = Ctx;
            try
            {
                if (!s.Init(start, rule)) { r.Exhausted = true; return r; }
                int found = s.Run(exactBudget, 1, out bool exhausted);
                r.Nodes = s.NodeCount;
                if (found >= 0) { r.Solvable = true; r.Optimal = true; r.Moves = s.Path(found, start); return r; }
                if (!exhausted) return r; // proved unsolvable
                if (fallbackBudget > 0)
                {
                    found = s.Run(fallbackBudget, 3, out exhausted);
                    r.Nodes += s.NodeCount;
                    if (found >= 0) { r.Solvable = true; r.Moves = s.Path(found, start); return r; }
                    if (!exhausted) return r;
                }
                r.Exhausted = true;
                return r;
            }
            finally
            {
                if (s.Capacity > 1 << 20) _ctx = null; // don't keep hundreds of MB alive after a big (bake-time) search
            }
        }

        /// <summary>
        /// True only when the board is PROVEN stuck: no legal move at all, or the search ran to the end without a solution
        /// (every line leads to a dead end). Running out of budget is not "stuck" (no false alarms on hard boards).
        /// Thread-safe (own search context per thread), so the game runs it in the background after each move.
        /// </summary>
        public static bool IsStuck(Board b, MoveRule rule, int budget = 120000)
        {
            if (b.IsSolved()) return false;
            if (!b.HasAnyMove(rule)) return true;
            var r = Solve(b, rule, budget, 0);
            if (r.Solvable) return false;
            if (!r.Exhausted) return true;
            // A* ran out of budget (typical with the Extra tray): settle it depth-first (a win is usually found fast;
            // a dead board is proven by visiting every position, up to DepthFirstCap)
            DepthFirst(b, rule, DepthFirstCap, out bool dead);
            return dead;
        }

        /// <summary>Next move toward a solution (Hint booster, idle hint, autoplay), or null when none is found.</summary>
        public static Move? NextMove(Board b, MoveRule rule) => NextMove(b, rule, out _);

        /// <summary>
        /// Next move toward a solution. <paramref name="provenDead"/> is true only when a search ran to the end without a
        /// solution (the board really can't be won). Null with provenDead false = not found within the budget.
        /// Audit 2026-11-23: the old budgets (15k exact, then 30k at weight 4) gave no hint on ~35% of WINNABLE
        /// positions (even the start of levels 22-30), and the Hint booster then showed the stuck popup. Now it escalates
        /// to greedier, bigger searches (all of levels 20-50 found, worst ~0.5 s in the Editor); the game runs it on a
        /// worker thread.
        /// </summary>
        public static Move? NextMove(Board b, MoveRule rule, out bool provenDead)
        {
            provenDead = false;
            if (b.IsSolved()) return null;
            var s = Ctx;
            if (!s.Init(b, rule)) return null;
            // 1) a quick exact search on the real board
            int found = s.Run(HintSearches[0].budget, HintSearches[0].weight, out bool exhausted);
            if (found >= 0) return First(s, found, b);
            if (!exhausted) { provenDead = true; return null; }
            // 2) an empty tray without a customer (the Extra tray, an emptied helper) multiplies the positions to search
            //    (with it, 23 of 50 level starts found no hint). A solution that leaves one such tray alone is also a
            //    solution on the real board, and the search without it is far smaller: try that first.
            for (int r = b.SkewerCount - 1; r >= 0; r--)
            {
                if (b.Count(r) != 0 || b.Target(r) >= 0) continue;
                var reduced = Without(b, r);
                if (!s.Init(reduced, rule)) continue;
                for (int i = 0; i < HintSearches.Length - 1; i++)
                {
                    int f = s.Run(HintSearches[i].budget, HintSearches[i].weight, out bool ex);
                    if (f >= 0)
                    {
                        var m = First(s, f, reduced);
                        if (!m.HasValue) break;
                        var mv = m.Value;
                        if (mv.From >= r) mv.From++;
                        if (mv.To >= r) mv.To++;
                        return mv;
                    }
                    if (!ex) break;   // no solution without that tray: it is needed
                }
            }
            // 3) the real board with bigger, greedier searches
            if (!s.Init(b, rule)) return null;
            for (int i = 1; i < HintSearches.Length; i++)
            {
                found = s.Run(HintSearches[i].budget, HintSearches[i].weight, out exhausted);
                if (found >= 0) return First(s, found, b);
                if (!exhausted) { provenDead = true; return null; }
            }
            // 4) depth-first with promising moves first: A* drowns in the positions an extra empty tray creates (48 of
            //    102 winnable positions after Extra Tray got no hint), a dive down one good line finds a win at once
            return DepthFirst(b, rule, DepthFirstCap, out provenDead);
        }

        /// <summary>States the depth-first fallback may visit (it runs on a worker thread in the game).</summary>
        public static int DepthFirstCap = 400000;

        /// <summary>
        /// Any winning line by depth-first search (moves that serve first, then moves onto the same food, then the rest).
        /// Returns its first move, or null: <paramref name="provenDead"/> = every reachable position was visited and
        /// none wins. Not shortest; used only when A* runs out of budget.
        /// </summary>
        public static Move? DepthFirst(Board start, MoveRule rule, int cap, out bool provenDead)
        {
            provenDead = false;
            if (start.IsSolved()) return null;
            string Key(Board x) => x.ToString() + "|" + x.QueueNext;
            var seen = new HashSet<string> { Key(start) };
            var stack = new List<(Board b, List<Move> moves, int next, Move first)>();
            stack.Add((start, Ordered(start, rule), 0, default));
            while (stack.Count > 0)
            {
                var top = stack[stack.Count - 1];
                if (top.next >= top.moves.Count) { stack.RemoveAt(stack.Count - 1); continue; }
                var m = top.moves[top.next];
                stack[stack.Count - 1] = (top.b, top.moves, top.next + 1, top.first);
                var nb = top.b.Clone();
                nb.Apply(m);
                var first = stack.Count == 1 ? m : top.first;
                if (nb.IsSolved()) return first;
                if (!seen.Add(Key(nb))) continue;
                if (seen.Count > cap) return null;
                stack.Add((nb, Ordered(nb, rule), 0, first));
            }
            provenDead = true;
            return null;
        }

        static List<Move> Ordered(Board b, MoveRule rule)
        {
            var list = new List<Move>();
            b.LegalMoves(rule, list, true);
            var scored = new List<(Move m, int sc)>(list.Count);
            foreach (var m in list)
            {
                int sc = 0;
                int food = b.Get(m.From, m.Index);
                if (b.Count(m.To) > 0) sc += 10;                                    // joins the same food
                if (b.Target(m.To) == food && b.Settled(m.To) == b.Count(m.To)) sc += 20;   // lands on its own order
                if (b.Count(m.To) + m.Count == b.Pieces && b.Target(m.To) == food && b.Settled(m.To) == b.Count(m.To)) sc += 100;   // serves
                if (m.Index == 0 && b.Target(m.From) < 0) sc -= 5;                  // emptying a helper into another tray
                scored.Add((m, sc));
            }
            scored.Sort((x, y) => y.sc.CompareTo(x.sc));
            for (int i = 0; i < scored.Count; i++) list[i] = scored[i].m;
            return list;
        }

        static Move? First(Search s, int found, Board b)
        {
            var p = s.Path(found, b, 1);
            return p.Count > 0 ? p[0] : (Move?)null;
        }

        /// <summary>The same board without tray <paramref name="r"/> (and the same queue still to come).</summary>
        static Board Without(Board b, int r)
        {
            return b.Without(r);
        }

        /// <summary>Escalating (node budget, weight) searches for a hint; public so tests can try other settings.</summary>
        public static (int budget, int weight)[] HintSearches = { (15000, 1), (120000, 3) };   // then depth-first (a bigger A* stage only cost time: 2 s worst vs the dive)

        // ------------------------------------------------------------------ search internals

        [ThreadStatic] static Search _ctx;
        static Search Ctx => _ctx ?? (_ctx = new Search());

        struct Key
        {
            public ulong A, B, C, D;
            public bool Same(in Key o) => A == o.A && B == o.B && C == o.C && D == o.D;
            public int Hash()
            {
                unchecked
                {
                    ulong h = A * 0x9E3779B97F4A7C15UL;
                    h ^= (B + 0x632BE59BD9B4E019UL) * 0xC2B2AE3D27D4EB4FUL;
                    h ^= (C + 0x165667B19E3779F9UL) * 0x94D049BB133111EBUL;
                    h ^= (D + 0x27D4EB2F165667C5UL) * 0xFF51AFD7ED558CCDUL;
                    h ^= h >> 31;
                    return (int)h ^ (int)(h >> 32);
                }
            }
        }

        sealed class Search
        {
            // problem
            int _cap, _pieces;
            MoveRule _rule;
            readonly int[] _foodMap = new int[64];
            readonly int[] _foodBack = new int[16];
            int _foodCount;
            // queue (local food ids): trays that join the row when an order is served
            int _qLen;
            int[][] _qCells = new int[64][];
            int[] _qCnt = new int[64];
            int[] _qTgt = new int[64];
            bool[][] _qHid = new bool[64][];
            int[] _qLock = new int[64];
            Key _startKey;
            int _startH;

            // nodes
            Key[] _keys = new Key[1 << 14];
            int[] _parent = new int[1 << 14];
            int[] _move = new int[1 << 14];                // packed move that reached the node
            short[] _g = new short[1 << 14];
            byte[] _h = new byte[1 << 14];
            bool[] _closed = new bool[1 << 14];
            public int NodeCount;
            public int Capacity => _keys.Length;

            int[] _table = new int[1 << 15];
            int _mask = (1 << 15) - 1;

            int[][] _buckets = new int[64][];
            int[] _bucketCount = new int[64];
            int _minF;
            const int HS = 64;

            // working row (local food ids, 0 = empty): _n trays; _tgt[s] = local target or 0 (free tray); _qi = next queued
            readonly int[] _cells = new int[MaxSkewers * MaxCapacity];
            readonly int[] _cnt = new int[MaxSkewers];
            readonly int[] _tgt = new int[MaxSkewers];
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
            int _bN, _bQi;

            public bool Init(Board b, MoveRule rule)
            {
                _n = b.SkewerCount; _cap = b.Capacity; _pieces = b.Pieces; _rule = rule; _qi = 0;
                if (_n > MaxSkewers || _cap > MaxCapacity || _pieces > _cap) return false;
                for (int i = 0; i < _foodMap.Length; i++) _foodMap[i] = 0;
                _foodCount = 0;
                Array.Clear(_cells, 0, _cells.Length);
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
                }
                _qLen = 0;
                for (int q = b.QueueNext; q < b.QueueCount; q++)
                {
                    if (_qLen == _qCells.Length) { Array.Resize(ref _qCells, _qLen * 2); Array.Resize(ref _qCnt, _qLen * 2); Array.Resize(ref _qTgt, _qLen * 2); Array.Resize(ref _qHid, _qLen * 2); Array.Resize(ref _qLock, _qLen * 2); }
                    var st = b.QueueStack(q);
                    if (st.Length > _cap) return false;
                    var arr = _qCells[_qLen] ?? (_qCells[_qLen] = new int[MaxCapacity]);
                    for (int i = 0; i < st.Length; i++) { arr[i] = Local(st[i]); if (arr[i] == 0) return false; }
                    _qCnt[_qLen] = st.Length;
                    int t = b.QueueTarget(q);
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
                if (anyLock && _cap > 6) return false;   // the lock is packed into the 7th cell's bits of a tray code
                _startKey = Encode();
                _startH = H();
                return true;
            }

            int Local(int f)
            {
                if (f < 0 || f >= _foodMap.Length) return 0;
                if (_foodMap[f] == 0)
                {
                    if (_foodCount == MaxFoods) return 0;
                    _foodMap[f] = ++_foodCount;
                    _foodBack[_foodCount] = f;
                }
                return _foodMap[f];
            }

            void SaveBase()
            {
                Array.Copy(_cells, _bCells, _n * MaxCapacity);
                Array.Copy(_hid, _bHid, _n * MaxCapacity);
                Array.Copy(_cnt, _bCnt, _n); Array.Copy(_tgt, _bTgt, _n); Array.Copy(_lock, _bLock, _n);
                _bN = _n; _bQi = _qi;
            }

            void LoadBase()
            {
                Array.Copy(_bCells, _cells, _bN * MaxCapacity);
                Array.Copy(_bHid, _hid, _bN * MaxCapacity);
                Array.Copy(_bCnt, _cnt, _bN); Array.Copy(_bTgt, _tgt, _bN); Array.Copy(_bLock, _lock, _bN);
                for (int s = _bN; s < _n; s++) { _cnt[s] = 0; _tgt[s] = 0; _lock[s] = 0; }
                _n = _bN; _qi = _bQi;
            }

            bool Complete(int s) => _tgt[s] != 0 && _lock[s] == 0 && _cnt[s] == _pieces && Settled(s) == _pieces;

            void RevealBottom(int s) { if (_cnt[s] > 0) _hid[s * MaxCapacity + _cnt[s] - 1] = false; }

            /// <summary>Serves tray s: it leaves, later trays shift left, the next queued tray joins at the end.</summary>
            void ServeAt(int s)
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
            }

            bool IsGoal()
            {
                if (_qi < _qLen) return false;
                for (int s = 0; s < _n; s++) if (_tgt[s] != 0 || _cnt[s] != 0) return false;
                return true;
            }

            public int Run(int budget, int w, out bool exhausted)
            {
                exhausted = false;
                Reset();
                int root = AddNode(_startKey, -1, -1, 0, _startH);
                if (_startH == 0) return root;
                _minF = 0;
                Push((w * _startH) * HS + _startH, root);
                while (true)
                {
                    while (_minF < _buckets.Length && _bucketCount[_minF] == 0) _minF++;
                    if (_minF >= _buckets.Length) return -1; // open list empty: unsolvable
                    int f = _minF;
                    int cur = _buckets[f][--_bucketCount[f]];
                    if (_closed[cur] || (_g[cur] + w * _h[cur]) * HS + _h[cur] != f) continue; // stale entry
                    _closed[cur] = true;
                    Decode(_keys[cur]);
                    SaveBase();
                    int g1 = _g[cur] + 1;
                    int rowN = _bN;
                    for (int from = 0; from < rowN; from++)
                    {
                        int n = _bCnt[from];
                        if (n == 0 || _bLock[from] != 0) continue;
                        int fb = from * MaxCapacity;
                        int lo = LowStart(from, n);
                        for (int start = n - 1; start >= lo; start--)
                        {
                            int food = _bCells[fb + start];
                            int group = n - start;
                            bool triedFree = false;
                            for (int to = 0; to < rowN; to++)
                            {
                                if (to == from || _lock[to] != 0) continue;
                                int count = Count(from, start, n, food, group, to, ref triedFree);
                                if (count <= 0) continue;
                                if (_cnt[to] > 0 && HiddenIn(fb + n - count, count)) continue;   // wrapped food only onto an empty tray
                                // apply on the working row (it holds the base row here)
                                int ct = _cnt[to], tb = to * MaxCapacity;
                                for (int k = 0; k < count; k++) { _cells[tb + ct + k] = _cells[fb + n - count + k]; _hid[tb + ct + k] = _hid[fb + n - count + k]; }
                                for (int k = n - count; k < n; k++) { _cells[fb + k] = 0; _hid[fb + k] = false; }
                                _cnt[from] = n - count; _cnt[to] = ct + count;
                                RevealBottom(from); RevealBottom(to);
                                int t2 = to;
                                if (Complete(from)) { ServeAt(from); if (t2 > from) t2--; }
                                if (Complete(t2)) ServeAt(t2);
                                var key = Encode();
                                int h = H();
                                LoadBase();

                                int mv = from | (to << 4) | ((n - count) << 8) | (count << 12);
                                int idx = Find(key);
                                if (idx >= 0)
                                {
                                    if (_closed[idx] || _g[idx] <= g1) continue;
                                    _g[idx] = (short)g1; _parent[idx] = cur; _move[idx] = mv;
                                }
                                else
                                {
                                    if (NodeCount >= budget) { exhausted = true; return -1; }
                                    idx = AddNode(key, cur, mv, g1, h);
                                    if (h == 0) return idx; // h = 0 only at the goal; consistent h: the first goal is optimal
                                }
                                Push((g1 + w * h) * HS + h, idx);
                            }
                        }
                    }
                }
            }

            int LowStart(int from, int n)
            {
                if (_rule == MoveRule.AnyWithAbove) return 0;
                if (_rule == MoveRule.SingleTop) return n - 1;
                int fb = from * MaxCapacity, lo = n - 1;
                while (lo > 0 && _cells[fb + lo - 1] == _cells[fb + n - 1]) lo--;
                return lo;
            }

            bool HiddenIn(int at, int count)
            {
                for (int k = 0; k < count; k++) if (_hid[at + k]) return true;
                return false;
            }

            /// <summary>Board.FillsUniform on the working row: a move that fills the target tray must leave it one food.</summary>
            bool FillsUniform(int fb, int index, int count, int to)
            {
                int ct = _cnt[to];
                if (ct + count < _cap) return true;
                int food = _cells[fb + index];
                for (int k = 0; k < count; k++) if (_cells[fb + index + k] != food) return false;
                int tb = to * MaxCapacity;
                for (int k = 0; k < ct; k++) if (_cells[tb + k] != food) return false;
                return true;
            }

            /// <summary>Pieces that move from <paramref name="from"/> (block starting at <paramref name="start"/>) to <paramref name="to"/>, or 0 if illegal / pruned.</summary>
            int Count(int from, int start, int n, int food, int group, int to, ref bool triedFree)
            {
                int ct = _cnt[to];
                if (ct == 0)
                {
                    if (_tgt[to] == 0)
                    {
                        if (triedFree) return 0;                      // all empty free trays are the same
                        if (start == 0 && _tgt[from] == 0) return 0;  // free tray to free tray changes nothing
                        triedFree = true;
                    }
                    if (_rule == MoveRule.SingleTop) return 1;
                    if (_rule == MoveRule.TopGroup) return Math.Min(group, _cap);
                    return group <= _cap ? group : 0;
                }
                if (ct >= _cap || _cells[to * MaxCapacity + ct - 1] != food) return 0;
                int room = _cap - ct;
                if (_rule == MoveRule.AnyWithAbove) return group <= room ? group : 0;
                if (_rule == MoveRule.TopGroup) return Math.Min(group, room);
                return 1;
            }

            int Settled(int s)
            {
                int t = _tgt[s];
                if (t == 0) return 0;
                int b = s * MaxCapacity, k = 0;
                while (k < _cnt[s] && _cells[b + k] == t) k++;
                return k;
            }

            public List<Move> Path(int goal, Board start, int maxMoves = int.MaxValue)
            {
                var chain = new List<int>();
                for (int i = goal; _parent[i] >= 0; i = _parent[i]) chain.Add(_move[i]);
                chain.Reverse();
                var moves = new List<Move>();
                foreach (var mv in chain)
                {
                    if (moves.Count >= maxMoves) break;
                    moves.Add(new Move(mv & 15, (mv >> 4) & 15, (mv >> 8) & 15, (mv >> 12) & 15));
                }
                return moves;
            }

            // -------------------------------------------------- helpers

            void Reset()
            {
                Array.Clear(_table, 0, _table.Length);
                for (int i = 0; i < _bucketCount.Length; i++) _bucketCount[i] = 0;
                NodeCount = 0;
            }

            /// <summary>
            /// Unsettled runs on the row (+1 when no single move lowers that count). Queued trays are ignored, so it stays a
            /// lower bound; serving only removes settled pieces and an arriving tray only adds runs, so it stays consistent.
            /// When nothing is unsettled but orders are still open (waiting for queued food), at least one move is left: 1.
            /// </summary>
            int H()
            {
                int runs = 0;
                for (int s = 0; s < _n; s++)
                {
                    int c = _cnt[s];
                    int i = Settled(s);
                    if (i >= c) continue;
                    int b = s * MaxCapacity;
                    runs++;
                    for (i = i + 1; i < c; i++) if (_cells[b + i] != _cells[b + i - 1]) runs++;
                }
                if (runs == 0) return IsGoal() ? 0 : 1;
                if (!HasReducingMove()) runs++;
                return Math.Min(runs, 255);
            }

            /// <summary>
            /// True when some legal move lowers the unsettled-run count: its block's bottom run settles on its own tray
            /// or lands on a matching top, and lifting it does not split an unsettled run.
            /// </summary>
            bool HasReducingMove()
            {
                for (int from = 0; from < _n; from++)
                {
                    int n = _cnt[from];
                    if (n == 0 || _lock[from] != 0) continue;
                    int fb = from * MaxCapacity;
                    int settled = Settled(from);
                    int lo = LowStart(from, n);
                    for (int start = n - 1; start >= lo; start--)
                    {
                        int food = _cells[fb + start];
                        if (start > 0 && _cells[fb + start - 1] == food) continue;
                        if (start < settled) continue;
                        int group = n - start;
                        for (int to = 0; to < _n; to++)
                        {
                            if (to == from || _lock[to] != 0) continue;
                            int ct = _cnt[to];
                            int room = _cap - ct;
                            int count = _rule == MoveRule.SingleTop ? 1 : _rule == MoveRule.TopGroup ? Math.Min(group, room) : group;
                            if (count > room || count <= 0) continue;
                            if (_rule == MoveRule.TopGroup && count < group) continue;
                            if (_rule == MoveRule.SingleTop && group > 1 && _cells[fb + n - 2] == food) continue;
                            if (ct == 0) { if (_tgt[to] == food) return true; continue; }
                            if (_cells[to * MaxCapacity + ct - 1] != food) continue;
                            return true;
                        }
                    }
                }
                return false;
            }

            // row slot s (up to 6) -> 32 bits: cells (4 bits each, up to 7) + target (4 bits); D = row size | queue index << 8
            Key Encode()
            {
                Key k = default;
                for (int s = 0; s < _n; s++)
                {
                    ulong code = 0;
                    int b = s * MaxCapacity;
                    for (int i = 0; i < _cnt[s]; i++) code |= (ulong)_cells[b + i] << (i * 4);
                    code |= (ulong)_lock[s] << 24;
                    code |= (ulong)_tgt[s] << 28;
                    ulong v = code << ((s & 1) * 32);
                    if (s < 2) k.A |= v; else if (s < 4) k.B |= v; else k.C |= v;
                }
                ulong d = (ulong)_n | ((ulong)_qi << 8);
                for (int s = 0; s < _n; s++)
                    for (int i = 0; i < _cnt[s] && i < 7; i++)
                        if (_hid[s * MaxCapacity + i]) d |= 1UL << (16 + s * 7 + i);
                k.D = d;
                return k;
            }

            void Decode(in Key k)
            {
                _n = (int)(k.D & 0xFF); _qi = (int)((k.D >> 8) & 0xFF);
                for (int s = 0; s < _n; s++)
                {
                    ulong word = s < 2 ? k.A : s < 4 ? k.B : k.C;
                    uint code = (uint)(word >> ((s & 1) * 32));
                    int b = s * MaxCapacity, c = 0;
                    int lim = Math.Min(_cap, 6);
                    while (c < lim && ((code >> (c * 4)) & 0xF) != 0) { _cells[b + c] = (int)((code >> (c * 4)) & 0xF); c++; }
                    if (_cap > 6 && c == 6 && ((code >> 24) & 0xF) != 0) { _cells[b + 6] = (int)((code >> 24) & 0xF); c = 7; }
                    for (int i = c; i < MaxCapacity; i++) _cells[b + i] = 0;
                    _cnt[s] = c;
                    _tgt[s] = (int)((code >> 28) & 0xF);
                    _lock[s] = _cap > 6 ? 0 : (int)((code >> 24) & 0xF);
                    for (int i = 0; i < MaxCapacity; i++) _hid[b + i] = i < c && i < 7 && ((k.D >> (16 + s * 7 + i)) & 1) != 0;
                }
            }

            int Find(in Key k)
            {
                int i = k.Hash() & _mask;
                while (true)
                {
                    int v = _table[i];
                    if (v == 0) return -1;
                    if (_keys[v - 1].Same(k)) return v - 1;
                    i = (i + 1) & _mask;
                }
            }

            int AddNode(in Key k, int parent, int move, int g, int h)
            {
                if (NodeCount == _keys.Length)
                {
                    int sz = _keys.Length * 2;
                    Array.Resize(ref _keys, sz); Array.Resize(ref _parent, sz); Array.Resize(ref _move, sz);
                    Array.Resize(ref _g, sz); Array.Resize(ref _h, sz); Array.Resize(ref _closed, sz);
                }
                int idx = NodeCount++;
                _keys[idx] = k; _parent[idx] = parent; _move[idx] = move; _g[idx] = (short)g; _h[idx] = (byte)h; _closed[idx] = false;
                if (NodeCount * 2 > _table.Length) Rehash();
                else Insert(idx);
                return idx;
            }

            void Insert(int idx)
            {
                int i = _keys[idx].Hash() & _mask;
                while (_table[i] != 0) i = (i + 1) & _mask;
                _table[i] = idx + 1;
            }

            void Rehash()
            {
                _table = new int[_table.Length * 2];
                _mask = _table.Length - 1;
                for (int i = 0; i < NodeCount; i++) Insert(i);
            }

            void Push(int f, int idx)
            {
                if (f >= _buckets.Length)
                {
                    int sz = Math.Max(f + 1, _buckets.Length * 2);
                    Array.Resize(ref _buckets, sz); Array.Resize(ref _bucketCount, sz);
                }
                var arr = _buckets[f];
                if (arr == null) _buckets[f] = arr = new int[256];
                int c = _bucketCount[f];
                if (c == arr.Length) { Array.Resize(ref arr, c * 2); _buckets[f] = arr; }
                arr[c] = idx;
                _bucketCount[f] = c + 1;
                if (f < _minF) _minF = f;
            }
        }
    }
}
