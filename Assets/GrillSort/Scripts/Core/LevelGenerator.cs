using System;
using System.Collections.Generic;

namespace GrillSort
{
    /// <summary>
    /// Deterministic level generator, Sort Streak style (Aaron 2026-11-19): one tray per customer that holds exactly
    /// that customer's order (4 or 5 pieces), plus 2 helper trays without customers, one on each side, for moving
    /// pieces around. Level N always gives the same puzzle (seeded), every level is solved before it is accepted, and
    /// the chosen candidate's par keeps rising from level to level.
    /// Tray order on the board (= on screen): helper, customer trays..., helper.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Most customer trays a level has (= customers at the counter).</summary>
        public const int MaxSkewers = 5;
        /// <summary>Helper trays per level (one each side).</summary>
        public const int Helpers = 2;
        /// <summary>Most grills a level can show: customers + helpers + 1 for the Extra tray booster.</summary>
        public const int MaxGrills = MaxSkewers + Helpers + 1;
        /// <summary>Biggest order (= tray size); the scene authors this many food slots per grill.</summary>
        public const int MaxCapacity = 6;

        public struct Tier
        {
            /// <summary>Customers = customer trays = food types.</summary>
            public int Types;
            /// <summary>Pieces per order = slots per tray.</summary>
            public int Pieces;
            public int MaxPerLookAlikeGroup;
            /// <summary>Par (minimum moves) the generator aims for.</summary>
            public int TargetPar;
            public override string ToString() => $"{Types} customers x {Pieces} pieces + {Helpers} helpers, look-alikes/group {MaxPerLookAlikeGroup}, target par {TargetPar}";
        }

        // Difficulty tiers: first level, customers, pieces per order, look-alikes per group, target par range (climbs
        // from lo at the tier's first level to hi at its last). Par ranges measured 2026-11-19 (40 random boards each,
        // 2 helpers, AnyWithAbove), min-max: 2x4 3-7, 3x4 5-12, 4x4 10-17, 5x4 14-20, 4x5 12-20, 5x5 18-25.
        static readonly (int start, int types, int pieces, int look, int lo, int hi)[] Tiers =
        {
            (1, 2, 4, 1, 4, 4),     // tutorial
            (2, 3, 4, 1, 6, 9),
            (4, 4, 4, 1, 10, 15),
            (9, 5, 4, 1, 15, 18),
            (16, 5, 4, 2, 18, 20),  // look-alikes start
            (24, 5, 5, 2, 20, 23),  // bigger orders (5 pieces)
            (38, 5, 5, 3, 23, 25),
        };

        // Target par for levels 1..50 (declared after Tiers, which it reads)
        static readonly int[] TargetPars = BuildTargets();

        static int TierIndex(int level)
        {
            int i = 0;
            while (i + 1 < Tiers.Length && Tiers[i + 1].start <= level) i++;
            return i;
        }

        static int[] BuildTargets()
        {
            var t = new int[51];
            for (int lv = 1; lv <= 50; lv++)
            {
                int i = TierIndex(lv);
                int first = Tiers[i].start, last = i + 1 < Tiers.Length ? Tiers[i + 1].start - 1 : 50;
                float k = last == first ? 1f : (lv - first) / (float)(last - first);
                t[lv] = (int)Math.Round(Tiers[i].lo + (Tiers[i].hi - Tiers[i].lo) * k);
            }
            for (int lv = 2; lv <= 50; lv++) t[lv] = Math.Max(t[lv], t[lv - 1]); // never easier than the level before
            return t;
        }

        static Tier KnobsFor(int level)
        {
            var r = Tiers[TierIndex(level)];
            return new Tier { Types = r.types, Pieces = r.pieces, MaxPerLookAlikeGroup = r.look };
        }

        /// <summary>Difficulty knobs per level.</summary>
        public static Tier TierFor(int level)
        {
            var t = KnobsFor(level);
            // past 50 the board is as big as the counter allows; par stays near the top of its range
            t.TargetPar = level < TargetPars.Length ? TargetPars[level] : TargetPars[50] - 1 + level % 3;
            return t;
        }

        public static uint SeedFor(int level) => unchecked((uint)(level * 7919 + 12345));

        /// <summary>
        /// Generates level <paramref name="level"/>: solves seeded candidates and keeps the one whose par is closest to
        /// the tier's target (never below <paramref name="prevPar"/> when possible), preferring proven-minimal pars.
        /// </summary>
        public static LevelData Generate(int level, int prevPar, MoveRule rule, int candidates = -1, int exactBudget = 2000000, int fallbackBudget = 200000)
        {
            var tier = TierFor(level);
            if (candidates <= 0) candidates = 24;
            var rng = new Mulberry32(SeedFor(level));
            int desired = Math.Max(tier.TargetPar, prevPar);
            int attempts = 0, found = 0;
            LevelData best = null;
            while (found < candidates && attempts < candidates * 100) // small boards are often stuck: try more
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

        public static LevelData Candidate(int level, Tier tier, Mulberry32 rng, MoveRule rule)
        {
            var foods = PickFoods(tier, rng, out int pairs);
            if (foods == null) return null;
            int n = tier.Types, p = tier.Pieces, trays = n + Helpers;
            var pieces = new List<int>();
            foreach (var f in foods) for (int k = 0; k < p; k++) pieces.Add(f);
            rng.Shuffle(pieces);

            // tray heights: customer trays start full, helpers empty; then pieces move around so the free room is
            // spread over the board (some customer trays start part-empty, helpers may start with food)
            var counts = new int[trays];
            for (int s = 1; s <= n; s++) counts[s] = p;
            int shifts = Helpers * p + rng.Range(Helpers * p + 1);
            for (int k = 0; k < shifts; k++)
            {
                int a = rng.Range(trays), b = rng.Range(trays);
                if (a == b || counts[a] == 0 || counts[b] >= p) continue;
                counts[a]--; counts[b]++;
            }

            // tray 0 and the last tray are helpers (no customer); tray s in between belongs to the customer above it
            var orders = new int[trays];
            var shuffled = new List<int>(foods);
            rng.Shuffle(shuffled);
            orders[0] = orders[trays - 1] = -1;
            for (int s = 1; s <= n; s++) orders[s] = shuffled[s - 1];

            var b2 = new Board(p, p);
            int at = 0;
            for (int s = 0; s < trays; s++) { b2.AddSkewer(pieces.GetRange(at, counts[s]), orders[s]); at += counts[s]; }

            // too easy: an order already done, or most of it already sitting in place
            for (int s = 0; s < trays; s++)
            {
                if (b2.IsComplete(s)) return null;
                if (level > 1 && b2.Settled(s) >= p - 1) return null;
                if (level > 1 && b2.TopGroup(s) >= p - 1) return null;
            }

            return new LevelData
            {
                number = level,
                capacity = p,
                pieces = p,
                skewers = trays,
                cells = LevelData.Cells(b2),
                orders = orders,
                foodTypes = n,
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
