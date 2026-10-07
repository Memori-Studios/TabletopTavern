using System;
using System.Collections.Generic;
using System.Linq;

namespace TJ.Prestige
{
    /// <summary>The Prestige III trait deal: which traits the picker offers, and when a Fateshine reroll can change them.</summary>
    public static class PrestigeTraitOffer
    {
        public const int OfferSize = 3;

        // A pool no bigger than one offer would deal the same traits again.
        public static bool CanReroll(int poolCount) => poolCount > OfferSize;

        /// <summary>
        /// Draws up to OfferSize traits from the pool, avoiding the excluded ones while enough others remain, then topping up
        /// from the excluded ones. pick(n) returns an index in [0, n).
        /// </summary>
        public static List<UnitAttribute> Deal(IReadOnlyList<UnitAttribute> pool, IReadOnlyCollection<UnitAttribute> exclude, Func<int, int> pick)
        {
            var fresh = new List<UnitAttribute>();
            var repeat = new List<UnitAttribute>();
            foreach (UnitAttribute trait in pool)
            {
                if (fresh.Contains(trait) || repeat.Contains(trait)) continue;
                if (exclude != null && exclude.Contains(trait)) repeat.Add(trait);
                else fresh.Add(trait);
            }

            var offer = new List<UnitAttribute>();
            Draw(fresh, offer, pick);
            Draw(repeat, offer, pick);
            return offer;
        }

        /// <summary>
        /// The deal's seed, built only from values a quit to the menu cannot change, so Continue deals the same cards.
        /// earlierPicks counts this unit's squads that already chose a trait, so a second gold squad gets its own deal.
        /// </summary>
        public static int Seed(int runSeed, UnitName unit, int earlierPicks, IReadOnlyCollection<UnitAttribute> exclude)
        {
            unchecked
            {
                int hash = runSeed;
                hash = hash * 31 + (int)unit;
                hash = hash * 31 + earlierPicks;
                // Summed, so the shown cards give the same reroll in any order.
                int excluded = 0;
                if (exclude != null)
                    foreach (UnitAttribute trait in exclude) excluded += MathUtilities.MixSeed((int)trait + 1);
                hash = hash * 31 + excluded;
                return MathUtilities.MixSeed(hash);
            }
        }

        /// <summary>True when a saved offer still fits the pool: the right size, no repeats and every trait still eligible.</summary>
        public static bool IsValid(IReadOnlyList<UnitAttribute> offer, IReadOnlyList<UnitAttribute> pool)
        {
            if (offer == null || pool == null) return false;
            if (offer.Count != Math.Min(OfferSize, pool.Count)) return false;
            for (int i = 0; i < offer.Count; i++)
            {
                if (!Contains(pool, offer[i])) return false;
                for (int j = 0; j < i; j++)
                    if (offer[j] == offer[i]) return false;
            }
            return true;
        }

        private static void Draw(List<UnitAttribute> source, List<UnitAttribute> offer, Func<int, int> pick)
        {
            while (offer.Count < OfferSize && source.Count > 0)
            {
                int index = pick(source.Count);
                offer.Add(source[index]);
                source.RemoveAt(index);
            }
        }

        private static bool Contains(IReadOnlyList<UnitAttribute> list, UnitAttribute trait)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == trait) return true;
            return false;
        }
    }
}
