namespace GrillSort
{
    /// <summary>Small deterministic RNG so level N is always the same puzzle on every device.</summary>
    public sealed class Mulberry32
    {
        uint _a;
        public Mulberry32(uint seed) { _a = seed; }

        public double Next()
        {
            unchecked
            {
                _a += 0x6D2B79F5;
                uint t = _a;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Range(int n) => (int)(Next() * n);

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
