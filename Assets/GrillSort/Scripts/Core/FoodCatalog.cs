namespace GrillSort
{
    /// <summary>Food ids are indexes into <see cref="Names"/> (same order as GameArt.foods).</summary>
    public static class FoodCatalog
    {
        public static readonly string[] Names =
        {
            "bacon", "beef", "broccoli", "chicken", "corn", "halloumi", "meatball", "mushroom", "onion",
            "pepper", "pineapple", "potato", "salmon", "sausage", "shrimp", "squid", "tomato", "zucchini",
        };

        public static int Count => Names.Length;

        /// <summary>Look-alike groups: foods in the same group are easy to confuse.</summary>
        public static readonly string[][] LookAlikes =
        {
            new[] { "corn", "pineapple", "potato", "halloumi" },  // yellow
            new[] { "beef", "sausage", "meatball", "bacon" },     // brown-red
            new[] { "salmon", "shrimp", "squid" },                // orange
            new[] { "pepper", "zucchini", "broccoli" },           // green
        };

        public static int Id(string name) => System.Array.IndexOf(Names, name);

        /// <summary>Look-alike group index of a food, or -1.</summary>
        public static int GroupOf(int id)
        {
            var n = Names[id];
            for (int g = 0; g < LookAlikes.Length; g++)
                if (System.Array.IndexOf(LookAlikes[g], n) >= 0) return g;
            return -1;
        }
    }
}
