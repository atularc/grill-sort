using System;
using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Solves a <see cref="Board"/> (one tray per order, Sort Streak style) with A* on a compact state.
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

        /// <summary>Largest board the compact search supports (5 order trays + 2 helper trays + 1 extra tray).</summary>
        public const int MaxSkewers = 8;
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

        /// <summary>Next move toward a solution (Hint booster, idle hint, autoplay), or null when none is found.</summary>
        public static Move? NextMove(Board b, MoveRule rule)
        {
            if (b.IsSolved()) return null;
            var s = Ctx;
            if (!s.Init(b, rule)) return null;
            int found = s.Run(15000, 1, out bool exhausted);
            if (found < 0 && exhausted) found = s.Run(30000, 4, out exhausted);
            if (found < 0) return null;
            var p = s.Path(found, b, 1);
            return p.Count > 0 ? p[0] : (Move?)null;
        }

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
            int _n, _cap, _pieces;
            MoveRule _rule;
            readonly int[] _foodMap = new int[64];
            readonly int[] _foodBack = new int[16];
            int _foodCount;
            readonly int[] _goal = new int[MaxSkewers];   // local target food per tray (0 = none)
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

            // scratch board (local food ids, 0 = empty); _tgt[s] = local target or 0 when served / free
            readonly int[] _cells = new int[MaxSkewers * MaxCapacity];
            readonly int[] _cnt = new int[MaxSkewers];
            readonly int[] _tgt = new int[MaxSkewers];

            public bool Init(Board b, MoveRule rule)
            {
                _n = b.SkewerCount; _cap = b.Capacity; _pieces = b.Pieces; _rule = rule;
                if (_n > MaxSkewers || _cap > MaxCapacity || _pieces > _cap) return false;
                for (int i = 0; i < _foodMap.Length; i++) _foodMap[i] = 0;
                _foodCount = 0;
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
                    _goal[s] = t >= 0 ? Local(t) : 0;
                    if (t >= 0 && _goal[s] == 0) return false;
                    _tgt[s] = _goal[s];
                }
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
                    int g1 = _g[cur] + 1;
                    for (int from = 0; from < _n; from++)
                    {
                        int n = _cnt[from];
                        if (n == 0) continue;
                        int fb = from * MaxCapacity;
                        int lo = LowStart(from, n);
                        for (int start = n - 1; start >= lo; start--)
                        {
                            int food = _cells[fb + start];
                            int group = n - start;
                            bool triedFree = false;
                            for (int to = 0; to < _n; to++)
                            {
                                if (to == from) continue;
                                int count = Count(from, start, n, food, group, to, ref triedFree);
                                if (count <= 0) continue;
                                int ct = _cnt[to], tb = to * MaxCapacity, tg = _tgt[to], fg = _tgt[from];
                                for (int k = 0; k < count; k++) _cells[tb + ct + k] = _cells[fb + n - count + k];
                                _cnt[from] = n - count; _cnt[to] = ct + count;
                                // served: the tray holds exactly its full order (either end of the move can complete)
                                if (tg != 0 && ct + count == _pieces && Settled(to) == _pieces) { _cnt[to] = 0; _tgt[to] = 0; }
                                if (fg != 0 && n - count == _pieces && Settled(from) == _pieces) { _cnt[from] = 0; _tgt[from] = 0; }
                                var key = Encode();
                                int h = H();
                                for (int k = 0; k < count; k++) _cells[tb + ct + k] = 0;
                                _cnt[from] = n; _cnt[to] = ct; _tgt[to] = tg; _tgt[from] = fg;

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
                                    if (h == 0) return idx; // consistent h: the first goal reached is optimal
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
                if (runs > 0 && !HasReducingMove()) runs++;
                return runs;
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
                    if (n == 0) continue;
                    int fb = from * MaxCapacity;
                    int settled = Settled(from);
                    int lo = LowStart(from, n);
                    for (int start = n - 1; start >= lo; start--)
                    {
                        int food = _cells[fb + start];
                        // lifting from inside a run splits it (unless that part was settled: then it costs a run too)
                        if (start > 0 && _cells[fb + start - 1] == food) continue;
                        if (start < settled) continue; // moving settled pieces never helps the count
                        int group = n - start;
                        for (int to = 0; to < _n; to++)
                        {
                            if (to == from) continue;
                            int ct = _cnt[to];
                            int room = _cap - ct;
                            int count = _rule == MoveRule.SingleTop ? 1 : _rule == MoveRule.TopGroup ? Math.Min(group, room) : group;
                            if (count > room || count <= 0) continue;
                            if (_rule == MoveRule.TopGroup && count < group) continue; // partial move splits the run
                            if (_rule == MoveRule.SingleTop && group > 1 && _cells[fb + n - 2] == food) continue;
                            if (ct == 0) { if (_tgt[to] == food) return true; continue; }
                            if (_cells[to * MaxCapacity + ct - 1] != food) continue;
                            return true; // merges onto a matching top (settled or not: either lowers the count)
                        }
                    }
                }
                return false;
            }

            Key Encode()
            {
                Key k = default;
                for (int s = 0; s < _n; s++)
                {
                    ulong code = 0;
                    int b = s * MaxCapacity;
                    for (int i = 0; i < _cnt[s]; i++) code |= (ulong)_cells[b + i] << (i * 4);
                    if (_tgt[s] == 0 && _goal[s] != 0) code |= 1UL << 28; // served
                    ulong v = code << ((s & 1) * 32);
                    if (s < 2) k.A |= v; else if (s < 4) k.B |= v; else if (s < 6) k.C |= v; else k.D |= v;
                }
                return k;
            }

            void Decode(in Key k)
            {
                for (int s = 0; s < _n; s++)
                {
                    ulong word = s < 2 ? k.A : s < 4 ? k.B : s < 6 ? k.C : k.D;
                    uint code = (uint)(word >> ((s & 1) * 32));
                    int b = s * MaxCapacity, c = 0;
                    while (c < _cap && ((code >> (c * 4)) & 0xF) != 0) { _cells[b + c] = (int)((code >> (c * 4)) & 0xF); c++; }
                    for (int i = c; i < _cap; i++) _cells[b + i] = 0;
                    _cnt[s] = c;
                    _tgt[s] = (code & (1u << 28)) != 0 ? 0 : _goal[s];
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
