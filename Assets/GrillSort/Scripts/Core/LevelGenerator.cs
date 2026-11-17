using System;
using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Deterministic level generator. Level N always gives the same puzzle (seeded), every level is solved before it
    /// is accepted, and the chosen candidate's par keeps rising from level to level (no plateau).
    /// </summary>
    public static class LevelGenerator
    {
        public const int Capacity = 4;
        /// <summary>Most skewers a level starts with (the scene has one more grill for the Extra skewer booster).</summary>
        public const int MaxSkewers = 9;

        public struct Tier
        {
            public int Types, Empties, MaxPerLookAlikeGroup;
            public override string ToString() => $"{Types} foods + {Empties} empty, look-alikes/group {MaxPerLookAlikeGroup}";
        }

        /// <summary>Difficulty knobs per level.</summary>
        public static Tier TierFor(int level)
        {
            if (level <= 1) return new Tier { Types = 2, Empties = 1, MaxPerLookAlikeGroup = 1 };
            if (level <= 3) return new Tier { Types = 3, Empties = 2, MaxPerLookAlikeGroup = 1 };
            if (level <= 6) return new Tier { Types = 4, Empties = 2, MaxPerLookAlikeGroup = 1 };
            if (level <= 10) return new Tier { Types = 5, Empties = 2, MaxPerLookAlikeGroup = 1 };
            if (level <= 14) return new Tier { Types = 5, Empties = 1, MaxPerLookAlikeGroup = 2 };
            if (level <= 19) return new Tier { Types = 6, Empties = 2, MaxPerLookAlikeGroup = 2 };
            if (level <= 24) return new Tier { Types = 6, Empties = 1, MaxPerLookAlikeGroup = 2 };
            if (level <= 30) return new Tier { Types = 7, Empties = 2, MaxPerLookAlikeGroup = 3 };
            if (level <= 45) return new Tier { Types = 7, Empties = 1, MaxPerLookAlikeGroup = 3 };
            return new Tier { Types = 8, Empties = 1, MaxPerLookAlikeGroup = 4 };
        }

        public static uint SeedFor(int level) => unchecked((uint)(level * 7919 + 12345));

        /// <summary>
        /// Generates level <paramref name="level"/>. <paramref name="prevPar"/> is the previous level's par: the pick is the
        /// candidate with the smallest par above it, so difficulty climbs steadily.
        /// </summary>
        public static LevelData Generate(int level, int prevPar, MoveRule rule, int candidates = -1, int bfsBudget = 200000)
        {
            var tier = TierFor(level);
            if (candidates <= 0) candidates = tier.Types + tier.Empties >= 9 ? 10 : 24;
            var rng = new Mulberry32(SeedFor(level));
            LevelData best = null;
            int target = prevPar + 1;
            int attempts = 0;
            var pool = new List<LevelData>();
            while (pool.Count < candidates && attempts < candidates * 20)
            {
                attempts++;
                var lv = Candidate(level, tier, rng, rule);
                if (lv == null) continue;
                var r = Solver.Solve(lv.ToBoard(), rule, bfsBudget, 300000);
                if (!r.Solvable) continue;
                lv.par = r.Par; lv.parOptimal = r.Optimal;
                pool.Add(lv);
            }
            // smallest par >= target (prefer exact pars), else the hardest one we found
            foreach (var lv in pool)
            {
                if (lv.par < target) continue;
                if (best == null || lv.par < best.par || (lv.par == best.par && lv.parOptimal && !best.parOptimal)) best = lv;
            }
            if (best == null)
                foreach (var lv in pool)
                    if (best == null || lv.par > best.par) best = lv;
            return best;
        }

        static LevelData Candidate(int level, Tier tier, Mulberry32 rng, MoveRule rule)
        {
            var foods = PickFoods(tier, rng, out int pairs);
            if (foods == null) return null;
            var pieces = new List<int>();
            foreach (var f in foods) for (int k = 0; k < Capacity; k++) pieces.Add(f);
            rng.Shuffle(pieces);

            var b = new Board(Capacity);
            for (int s = 0; s < tier.Types; s++) b.AddSkewer(pieces.GetRange(s * Capacity, Capacity));
            for (int e = 0; e < tier.Empties; e++) b.AddSkewer();

            // reject boards that start with a finished skewer or a 3-of-a-kind on top (too easy)
            for (int s = 0; s < b.SkewerCount; s++)
            {
                if (b.IsComplete(s)) return null;
                if (level > 1 && b.TopGroup(s) >= 3) return null;
            }

            var orders = new List<int>(foods);
            rng.Shuffle(orders);
            return new LevelData
            {
                number = level,
                capacity = Capacity,
                skewers = b.SkewerCount,
                cells = LevelData.Cells(b),
                orders = orders.ToArray(),
                foodTypes = tier.Types,
                empties = tier.Empties,
                lookAlikePairs = pairs,
                seed = SeedFor(level),
                rule = rule,
            };
        }

        static List<int> PickFoods(Tier tier, Mulberry32 rng, out int lookAlikePairs)
        {
            var all = new List<int>();
            for (int i = 0; i < FoodCatalog.Count; i++) all.Add(i);
            rng.Shuffle(all);
            var perGroup = new int[FoodCatalog.LookAlikes.Length];
            var pick = new List<int>();
            foreach (var f in all)
            {
                int g = FoodCatalog.GroupOf(f);
                if (g >= 0 && perGroup[g] >= tier.MaxPerLookAlikeGroup) continue;
                if (g >= 0) perGroup[g]++;
                pick.Add(f);
                if (pick.Count == tier.Types) break;
            }
            lookAlikePairs = 0;
            foreach (var c in perGroup) lookAlikePairs += c * (c - 1) / 2;
            return pick.Count == tier.Types ? pick : null;
        }
    }
}
