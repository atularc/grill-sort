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
            /// <summary>Par (minimum moves) the generator aims for.</summary>
            public int TargetPar;
            public override string ToString() => $"{Types} foods + {Empties} empty, look-alikes/group {MaxPerLookAlikeGroup}, target par {TargetPar}";
        }

        // Target par for levels 1..30 under AnyWithAbove. Measured true pars of random boards (A*, 40 samples each):
        // 3 foods 6-10, 4 foods 7-13, 5 foods 11-17, 6 foods 14-20, 7 foods 16-23, 8 foods 19-29 (one empty skewer is
        // as good as two for par, but two is friendlier). Each food type adds ~3.5 moves, so the curve climbs about one
        // move per level; where par repeats, the level drops an empty skewer or adds look-alikes instead.
        static readonly int[] TargetPars =
        {
            0,
            3, 6, 7, 8, 9, 10, 11, 12, 13, 14,      // 1-10
            15, 15, 16, 17, 18, 19, 19, 20, 21, 22, // 11-20
            22, 23, 23, 24, 24, 25, 25, 26, 26, 27, // 21-30
        };

        /// <summary>Difficulty knobs per level.</summary>
        public static Tier TierFor(int level)
        {
            Tier t;
            if (level <= 1) t = new Tier { Types = 2, Empties = 1, MaxPerLookAlikeGroup = 1 };
            else if (level <= 3) t = new Tier { Types = 3, Empties = 2, MaxPerLookAlikeGroup = 1 };
            else if (level <= 6) t = new Tier { Types = 4, Empties = 2, MaxPerLookAlikeGroup = 1 };
            else if (level <= 7) t = new Tier { Types = 4, Empties = 1, MaxPerLookAlikeGroup = 1 };
            else if (level <= 9) t = new Tier { Types = 5, Empties = 2, MaxPerLookAlikeGroup = 1 };
            else if (level <= 11) t = new Tier { Types = 5, Empties = 1, MaxPerLookAlikeGroup = 2 };
            else if (level <= 13) t = new Tier { Types = 6, Empties = 2, MaxPerLookAlikeGroup = 2 };
            else if (level <= 16) t = new Tier { Types = 6, Empties = 1, MaxPerLookAlikeGroup = 2 };
            else if (level <= 18) t = new Tier { Types = 7, Empties = 2, MaxPerLookAlikeGroup = 2 };
            else if (level <= 22) t = new Tier { Types = 7, Empties = 1, MaxPerLookAlikeGroup = 3 };
            else if (level <= 26) t = new Tier { Types = 8, Empties = 1, MaxPerLookAlikeGroup = 3 };
            else t = new Tier { Types = 8, Empties = 1, MaxPerLookAlikeGroup = 4 };
            // past 30: 8 foods + 1 empty fill the 9 grills; par stays near the top of what that board size allows
            t.TargetPar = level < TargetPars.Length ? TargetPars[level] : 26 + (level % 3);
            return t;
        }

        public static uint SeedFor(int level) => unchecked((uint)(level * 7919 + 12345));

        /// <summary>
        /// Generates level <paramref name="level"/>: solves seeded candidates and keeps the one whose par is closest to
        /// the tier's target (never below <paramref name="prevPar"/> when possible), preferring proven-minimal pars.
        /// <paramref name="exactBudget"/> is the A* node budget per candidate (smaller on device); a candidate the exact
        /// search can't finish is still verified by the weighted fallback, but its par is only an upper bound.
        /// </summary>
        public static LevelData Generate(int level, int prevPar, MoveRule rule, int candidates = -1, int exactBudget = 2000000, int fallbackBudget = 200000)
        {
            var tier = TierFor(level);
            if (candidates <= 0) candidates = tier.Types >= 8 ? 48 : 24;
            var rng = new Mulberry32(SeedFor(level));
            int desired = Math.Max(tier.TargetPar, prevPar);
            int attempts = 0, found = 0;
            LevelData best = null;
            while (found < candidates && attempts < candidates * 20)
            {
                attempts++;
                var lv = Candidate(level, tier, rng, rule);
                if (lv == null) continue;
                var r = Solver.Solve(lv.ToBoard(), rule, exactBudget, fallbackBudget);
                if (!r.Solvable) continue;
                found++;
                lv.par = r.Par; lv.parOptimal = r.Optimal;
                if (best == null || Better(lv, best, desired, prevPar)) best = lv;
                if (best.parOptimal && best.par == desired) break; // can't do better; stops early (still deterministic)
            }
            return best;
        }

        /// <summary>Proven par first, then not easier than the previous level, then closest to the target (ties: harder).</summary>
        static bool Better(LevelData a, LevelData b, int desired, int prevPar)
        {
            if (a.parOptimal != b.parOptimal) return a.parOptimal;
            bool aOk = a.par >= prevPar, bOk = b.par >= prevPar;
            if (aOk != bOk) return aOk;
            int da = Math.Abs(a.par - desired), db = Math.Abs(b.par - desired);
            if (da != db) return da < db;
            return a.par > b.par;
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
