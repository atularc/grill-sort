using System;
using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Solves a <see cref="Board"/> with A* on a compact, order-independent state.
    ///
    /// Heuristic h = (runs of identical adjacent pieces over all skewers) − (food types still on the board).
    /// It is admissible and consistent for every <see cref="MoveRule"/>: a move lifts a block off the source
    /// (which can only keep or add one run: the block may split a run) and drops it on the target, where its bottom
    /// run can merge with the target's top run (−1 at most). Clearing a completed skewer removes one run and one food
    /// type together (Δh = 0). So h drops by at most 1 per move, h = 0 only on the empty board, and A* gives the
    /// true minimum (par). When the exact search runs out of budget, a weighted search (g + 3h) still finds a
    /// solution (not proven minimal).
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

        /// <summary>Largest board the compact search supports (the scene has 10 grills).</summary>
        public const int MaxSkewers = 12;
        const int MaxFoods = 15; // 4-bit local food ids, 0 = empty slot

        /// <summary>
        /// Solves <paramref name="start"/>. <paramref name="exactBudget"/> = node limit for the exact A* (minimal par);
        /// <paramref name="fallbackBudget"/> = node limit for the weighted fallback that runs when A* gives up.
        /// </summary>
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
                // don't keep hundreds of MB alive after a big (bake-time) search
                if (s.Capacity > 1 << 20) _ctx = null;
            }
        }

        /// <summary>
        /// Next move toward a solution (Hint booster, idle hint, autoplay), or null when none is found. Cheap: a small
        /// exact search first (best hint), then a greedy-weighted one.
        /// </summary>
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
            public ulong A, B, C;
            public bool Same(in Key o) => A == o.A && B == o.B && C == o.C;
            public int Hash()
            {
                unchecked
                {
                    ulong h = A * 0x9E3779B97F4A7C15UL;
                    h ^= (B + 0x632BE59BD9B4E019UL) * 0xC2B2AE3D27D4EB4FUL;
                    h ^= (C + 0x165667B19E3779F9UL) * 0x94D049BB133111EBUL;
                    h ^= h >> 31;
                    return (int)h ^ (int)(h >> 32);
                }
            }
        }

        sealed class Search
        {
            // problem
            int _n, _cap;
            MoveRule _rule;
            readonly int[] _foodMap = new int[64];
            int _foodCount;
            Key _startKey;
            int _startH;

            // nodes
            Key[] _keys = new Key[1 << 14];
            int[] _parent = new int[1 << 14];
            short[] _g = new short[1 << 14];
            byte[] _h = new byte[1 << 14];
            bool[] _closed = new bool[1 << 14];
            public int NodeCount;
            public int Capacity => _keys.Length;

            // open-addressing hash: slot -> node index + 1
            int[] _table = new int[1 << 15];
            int _mask = (1 << 15) - 1;

            // bucket queue by priority, LIFO inside a bucket (prefers the deepest, most recent node)
            int[][] _buckets = new int[64][];
            int[] _bucketCount = new int[64];
            int _minF;
            /// <summary>Priority = f·HS + h: lowest f first, then the node closest to the goal.</summary>
            const int HS = 64;

            // scratch board (local food ids, 0 = empty)
            readonly int[] _cells = new int[MaxSkewers * 4];
            readonly int[] _cnt = new int[MaxSkewers];
            readonly int[] _codes = new int[MaxSkewers];

            public bool Init(Board b, MoveRule rule)
            {
                _n = b.SkewerCount; _cap = b.Capacity; _rule = rule;
                if (_n > MaxSkewers || _cap != 4) return false;
                for (int i = 0; i < _foodMap.Length; i++) _foodMap[i] = 0;
                _foodCount = 0;
                for (int s = 0; s < _n; s++)
                {
                    _cnt[s] = b.Count(s);
                    for (int i = 0; i < _cnt[s]; i++)
                    {
                        int f = b.Get(s, i);
                        if (f < 0 || f >= _foodMap.Length) return false;
                        if (_foodMap[f] == 0) { if (_foodCount == MaxFoods) return false; _foodMap[f] = ++_foodCount; }
                        _cells[s * 4 + i] = _foodMap[f];
                    }
                    if (b.IsComplete(s)) _cnt[s] = 0; // already complete = served
                }
                _startKey = Encode();
                _startH = H();
                return true;
            }

            /// <summary>Best-first search from the start with priority g + w·h. Returns the goal node or -1.</summary>
            public int Run(int budget, int w, out bool exhausted)
            {
                exhausted = false;
                Reset();
                int root = AddNode(_startKey, -1, 0, _startH);
                if (_startH == 0) return root; // nothing left to sort
                _minF = 0;
                Push((w * _startH) * HS + _startH, root);
                while (true)
                {
                    // lowest non-empty bucket
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
                        int fb = from * 4;
                        int lo;
                        if (_rule == MoveRule.AnyWithAbove) lo = 0;
                        else if (_rule == MoveRule.SingleTop) lo = n - 1;
                        else { lo = n - 1; while (lo > 0 && _cells[fb + lo - 1] == _cells[fb + n - 1]) lo--; }
                        for (int start = n - 1; start >= lo; start--)
                        {
                            int food = _cells[fb + start];
                            int group = n - start;
                            bool triedEmpty = false;
                            for (int to = 0; to < _n; to++)
                            {
                                if (to == from) continue;
                                int ct = _cnt[to];
                                int count;
                                if (ct == 0)
                                {
                                    // all empty skewers are the same; moving a whole skewer into one changes nothing
                                    if (triedEmpty || start == 0) continue;
                                    triedEmpty = true;
                                    count = _rule == MoveRule.SingleTop ? 1 : group;
                                }
                                else
                                {
                                    if (ct == 4 || _cells[to * 4 + ct - 1] != food) continue;
                                    int room = 4 - ct;
                                    if (_rule == MoveRule.AnyWithAbove) { if (group > room) continue; count = group; }
                                    else if (_rule == MoveRule.TopGroup) count = Math.Min(group, room);
                                    else count = 1;
                                }
                                // apply
                                int tb = to * 4;
                                int s0 = _cells[tb], s1 = _cells[tb + 1], s2 = _cells[tb + 2], s3 = _cells[tb + 3];
                                for (int k = 0; k < count; k++) _cells[tb + ct + k] = _cells[fb + n - count + k];
                                _cnt[from] = n - count;
                                _cnt[to] = ct + count;
                                if (ct + count == 4 && _cells[tb] == _cells[tb + 1] && _cells[tb] == _cells[tb + 2] && _cells[tb] == _cells[tb + 3])
                                    _cnt[to] = 0; // complete: served and cleared
                                var key = Encode();
                                int h = H();
                                // undo
                                _cells[tb] = s0; _cells[tb + 1] = s1; _cells[tb + 2] = s2; _cells[tb + 3] = s3;
                                _cnt[from] = n; _cnt[to] = ct;

                                int idx = Find(key);
                                if (idx >= 0)
                                {
                                    if (_closed[idx] || _g[idx] <= g1) continue;
                                    _g[idx] = (short)g1; _parent[idx] = cur;
                                }
                                else
                                {
                                    if (NodeCount >= budget) { exhausted = true; return -1; }
                                    idx = AddNode(key, cur, g1, h);
                                    // h == 0 only on the empty board; with a consistent h the first goal reached is optimal
                                    if (h == 0) return idx;
                                }
                                Push((g1 + w * h) * HS + h, idx);
                            }
                        }
                    }
                }
            }

            /// <summary>Moves (in the caller's skewer indexes) from the root to node <paramref name="goal"/>.</summary>
            public List<Move> Path(int goal, Board start, int maxMoves = int.MaxValue)
            {
                var chain = new List<Key>();
                for (int i = goal; _parent[i] >= 0; i = _parent[i]) chain.Add(_keys[i]);
                chain.Reverse();
                var moves = new List<Move>();
                var b = start.Clone();
                var legal = new List<Move>();
                foreach (var target in chain)
                {
                    if (moves.Count >= maxMoves) break;
                    b.LegalMoves(_rule, legal, false);
                    bool ok = false;
                    foreach (var m in legal)
                    {
                        var nb = b.Clone();
                        nb.Apply(m);
                        if (!EncodeBoard(nb).Same(target)) continue;
                        moves.Add(m); b = nb; ok = true;
                        break;
                    }
                    if (!ok) break; // should not happen
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
                int runs = 0, mask = 0;
                for (int s = 0; s < _n; s++)
                {
                    int c = _cnt[s];
                    if (c == 0) continue;
                    int b = s * 4;
                    runs++;
                    mask |= 1 << _cells[b];
                    for (int i = 1; i < c; i++)
                        if (_cells[b + i] != _cells[b + i - 1]) { runs++; mask |= 1 << _cells[b + i]; }
                }
                int types = 0;
                while (mask != 0) { mask &= mask - 1; types++; }
                int h = runs - types;
                // no move can merge a block onto a matching top right now: the next move cannot lower h (still consistent)
                // and if no single move even sets one up, the next two moves cannot (still consistent)
                if (h > 0 && !HasMergeMove()) h += AnyChildHasMerge() ? 1 : 2;
                return h;
            }

            /// <summary>True when some move drops a block onto a matching top without splitting a run (the only kind that lowers h).</summary>
            bool HasMergeMove()
            {
                for (int to = 0; to < _n; to++)
                {
                    int ct = _cnt[to];
                    if (ct == 0 || ct == 4) continue;
                    int top = _cells[to * 4 + ct - 1], room = 4 - ct;
                    for (int from = 0; from < _n; from++)
                    {
                        if (from == to) continue;
                        int n = _cnt[from], fb = from * 4;
                        if (n == 0) continue;
                        if (_rule == MoveRule.AnyWithAbove)
                        {
                            for (int start = n - 1; start >= 0 && n - start <= room; start--)
                                if (_cells[fb + start] == top && (start == 0 || _cells[fb + start - 1] != top)) return true;
                        }
                        else
                        {
                            if (_cells[fb + n - 1] != top) continue;
                            int lo = n - 1;
                            while (lo > 0 && _cells[fb + lo - 1] == top) lo--;
                            int group = n - lo;
                            if (_rule == MoveRule.SingleTop ? group == 1 : group <= room) return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>True when some legal move leads to a board that has a merge move. Leaves the board unchanged.</summary>
            bool AnyChildHasMerge()
            {
                for (int from = 0; from < _n; from++)
                {
                    int n = _cnt[from];
                    if (n == 0) continue;
                    int fb = from * 4;
                    int lo;
                    if (_rule == MoveRule.AnyWithAbove) lo = 0;
                    else if (_rule == MoveRule.SingleTop) lo = n - 1;
                    else { lo = n - 1; while (lo > 0 && _cells[fb + lo - 1] == _cells[fb + n - 1]) lo--; }
                    for (int start = n - 1; start >= lo; start--)
                    {
                        int food = _cells[fb + start];
                        int group = n - start;
                        bool triedEmpty = false;
                        for (int to = 0; to < _n; to++)
                        {
                            if (to == from) continue;
                            int ct = _cnt[to], count;
                            if (ct == 0)
                            {
                                if (triedEmpty || start == 0) continue;
                                triedEmpty = true;
                                count = _rule == MoveRule.SingleTop ? 1 : group;
                            }
                            else
                            {
                                if (ct == 4 || _cells[to * 4 + ct - 1] != food) continue;
                                int room = 4 - ct;
                                if (_rule == MoveRule.AnyWithAbove) { if (group > room) continue; count = group; }
                                else if (_rule == MoveRule.TopGroup) count = Math.Min(group, room);
                                else count = 1;
                            }
                            int tb = to * 4;
                            int s0 = _cells[tb], s1 = _cells[tb + 1], s2 = _cells[tb + 2], s3 = _cells[tb + 3];
                            for (int k = 0; k < count; k++) _cells[tb + ct + k] = _cells[fb + n - count + k];
                            _cnt[from] = n - count; _cnt[to] = ct + count;
                            if (ct + count == 4 && _cells[tb] == _cells[tb + 1] && _cells[tb] == _cells[tb + 2] && _cells[tb] == _cells[tb + 3])
                                _cnt[to] = 0;
                            bool ok = HasMergeMove();
                            // the caller may keep pieces in slots above a count (its own pending undo): restore them all
                            _cells[tb] = s0; _cells[tb + 1] = s1; _cells[tb + 2] = s2; _cells[tb + 3] = s3;
                            _cnt[from] = n; _cnt[to] = ct;
                            if (ok) return true;
                        }
                    }
                }
                return false;
            }

            Key Encode()
            {
                for (int s = 0; s < _n; s++)
                {
                    int c = _cnt[s], b = s * 4, code = 0;
                    for (int i = 0; i < c; i++) code |= _cells[b + i] << (i * 4);
                    // insertion sort, descending (skewer order does not matter)
                    int j = s;
                    while (j > 0 && _codes[j - 1] < code) { _codes[j] = _codes[j - 1]; j--; }
                    _codes[j] = code;
                }
                return Pack();
            }

            Key EncodeBoard(Board bd)
            {
                for (int s = 0; s < _n; s++)
                {
                    int code = 0;
                    if (!bd.IsComplete(s))
                        for (int i = 0; i < bd.Count(s); i++) code |= _foodMap[bd.Get(s, i)] << (i * 4);
                    int j = s;
                    while (j > 0 && _codes[j - 1] < code) { _codes[j] = _codes[j - 1]; j--; }
                    _codes[j] = code;
                }
                return Pack();
            }

            Key Pack()
            {
                Key k = default;
                for (int s = 0; s < _n; s++)
                {
                    ulong v = (ulong)_codes[s] << ((s & 3) * 16);
                    if (s < 4) k.A |= v; else if (s < 8) k.B |= v; else k.C |= v;
                }
                return k;
            }

            void Decode(in Key k)
            {
                for (int s = 0; s < _n; s++)
                {
                    ulong word = s < 4 ? k.A : s < 8 ? k.B : k.C;
                    int code = (int)((word >> ((s & 3) * 16)) & 0xFFFF);
                    int b = s * 4, c = 0;
                    while (c < 4 && ((code >> (c * 4)) & 0xF) != 0) { _cells[b + c] = (code >> (c * 4)) & 0xF; c++; }
                    _cnt[s] = c;
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

            int AddNode(in Key k, int parent, int g, int h)
            {
                if (NodeCount == _keys.Length)
                {
                    int sz = _keys.Length * 2;
                    Array.Resize(ref _keys, sz); Array.Resize(ref _parent, sz);
                    Array.Resize(ref _g, sz); Array.Resize(ref _h, sz); Array.Resize(ref _closed, sz);
                }
                int idx = NodeCount++;
                _keys[idx] = k; _parent[idx] = parent; _g[idx] = (short)g; _h[idx] = (byte)h; _closed[idx] = false;
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
