using System;

namespace GrillSort
{
    /// <summary>One verified level: the tray stacks, each tray's order (target food) and the par (shortest solution).</summary>
    [Serializable]
    public sealed class LevelData
    {
        public int number;
        /// <summary>Slots per tray.</summary>
        public int capacity = 5;
        /// <summary>Pieces of each food (= pieces that serve one order).</summary>
        public int pieces = 4;
        public int skewers;
        /// <summary>skewers * capacity food ids, bottom to top per tray, -1 = empty slot.</summary>
        public int[] cells;
        /// <summary>Orders in the whole level (row + queue).</summary>
        public int TotalOrders
        {
            get { int n = queueOrders != null ? queueOrders.Length : 0; foreach (var o in orders) if (o >= 0) n++; return n; }
        }
        /// <summary>Order of each tray: the food its customer wants (-1 = free tray). One customer stands above each tray.</summary>
        public int[] orders;
        /// <summary>Trays waiting to join the row (Sort Streak queue): queueOrders[i] = order of queued tray i,
        /// queueCells = queueOrders.Length * capacity food ids, -1 = empty slot.</summary>
        public int[] queueOrders = new int[0];
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
        public string tier = "";
        public int foodTypes;
        public int par;
        public bool parOptimal;
        public int lookAlikePairs;
        public uint seed;
        public MoveRule rule;

        public Board ToBoard()
        {
            var b = new Board(capacity, pieces);
            for (int s = 0; s < skewers; s++)
            {
                var stack = new System.Collections.Generic.List<int>();
                var hid = new System.Collections.Generic.List<bool>();
                for (int i = 0; i < capacity; i++)
                {
                    int f = cells[s * capacity + i];
                    if (f >= 0) { stack.Add(f); hid.Add(hidden != null && s * capacity + i < hidden.Length && hidden[s * capacity + i] != 0); }
                }
                b.AddSkewer(stack, orders != null && s < orders.Length ? orders[s] : -1, hid,
                    locks != null && s < locks.Length ? locks[s] : -1);
            }
            if (queueOrders != null)
                for (int q = 0; q < queueOrders.Length; q++)
                {
                    var stack = new System.Collections.Generic.List<int>();
                    var hid = new System.Collections.Generic.List<bool>();
                    for (int i = 0; i < capacity; i++)
                    {
                        int f = queueCells[q * capacity + i];
                        if (f >= 0) { stack.Add(f); hid.Add(queueHidden != null && q * capacity + i < queueHidden.Length && queueHidden[q * capacity + i] != 0); }
                    }
                    b.AddQueued(stack, queueOrders[q], hid, queueLocks != null && q < queueLocks.Length ? queueLocks[q] : -1);
                }
            return b;
        }

        public static int[] Cells(Board b)
        {
            var c = new int[b.SkewerCount * b.Capacity];
            for (int s = 0; s < b.SkewerCount; s++)
                for (int i = 0; i < b.Capacity; i++)
                    c[s * b.Capacity + i] = i < b.Count(s) ? b.Get(s, i) : -1;
            return c;
        }
    }
}
