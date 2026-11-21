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
        /// <summary>Order of each tray: the food its customer wants (-1 = free tray). One customer stands above each tray.</summary>
        public int[] orders;
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
                for (int i = 0; i < capacity; i++)
                {
                    int f = cells[s * capacity + i];
                    if (f >= 0) stack.Add(f);
                }
                b.AddSkewer(stack, orders != null && s < orders.Length ? orders[s] : -1);
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
