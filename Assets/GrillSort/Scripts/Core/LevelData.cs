using System;

namespace GrillSort
{
    /// <summary>One verified level: the skewer stacks, the orders queue and the par (shortest solution).</summary>
    [Serializable]
    public sealed class LevelData
    {
        public int number;
        public int capacity = 4;
        public int skewers;
        /// <summary>skewers * capacity food ids, bottom to top per skewer, -1 = empty slot.</summary>
        public int[] cells;
        /// <summary>Food ids in the order customers arrive.</summary>
        public int[] orders;
        public int foodTypes;
        public int empties;
        public int par;
        public bool parOptimal;
        public int lookAlikePairs;
        public uint seed;
        public MoveRule rule;

        public Board ToBoard()
        {
            var b = new Board(capacity);
            for (int s = 0; s < skewers; s++)
            {
                var stack = new System.Collections.Generic.List<int>();
                for (int i = 0; i < capacity; i++)
                {
                    int f = cells[s * capacity + i];
                    if (f >= 0) stack.Add(f);
                }
                b.AddSkewer(stack);
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
