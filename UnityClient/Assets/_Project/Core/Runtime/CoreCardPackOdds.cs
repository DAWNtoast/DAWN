using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreCardPackSchoolRate
    {
        internal CoreCardPackSchoolRate(string schoolId, int weight) { SchoolId = schoolId; Weight = weight; }
        public string SchoolId { get; }
        public int Weight { get; }
    }

    public sealed class CoreCardPackSchoolOdds
    {
        internal CoreCardPackSchoolOdds(CoreCardPackSchoolRate rate, int cards, int totalWeight)
        {
            SchoolId = rate.SchoolId; Weight = rate.Weight; CardCount = cards;
            Probability = (double)rate.Weight / totalWeight;
        }
        public string SchoolId { get; }
        public int Weight { get; }
        public int CardCount { get; }
        public double Probability { get; }
        public double CardProbability => CardCount > 0 ? Probability / CardCount : 0d;
    }

    public static partial class CoreCardPackCatalog
    {
        // The sole authored probability table: category weights, then uniform
        // selection among legal cards in that category. UI reads the same data.
        private static readonly IReadOnlyList<CoreCardPackSchoolRate> neutralRates = Array.AsReadOnly(new[]
        {
            new CoreCardPackSchoolRate("neutral", 28),
            new CoreCardPackSchoolRate("fire", 12),
            new CoreCardPackSchoolRate("arcane", 12),
            new CoreCardPackSchoolRate("curse", 12),
            new CoreCardPackSchoolRate("tide", 12),
            new CoreCardPackSchoolRate("thunder", 12),
            new CoreCardPackSchoolRate("blood", 12),
        });
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<CoreCardPackSchoolRate>> schoolRates =
            new Dictionary<string, IReadOnlyList<CoreCardPackSchoolRate>>(StringComparer.Ordinal)
            {
                ["fire"] = SchoolRates("fire"), ["arcane"] = SchoolRates("arcane"),
                ["curse"] = SchoolRates("curse"), ["tide"] = SchoolRates("tide"),
                ["thunder"] = SchoolRates("thunder"), ["blood"] = SchoolRates("blood"),
            };
        private static IReadOnlyList<CoreCardPackSchoolRate> SchoolRates(string school) => Array.AsReadOnly(new[]
        {
            new CoreCardPackSchoolRate(school, 70), new CoreCardPackSchoolRate("neutral", 30),
        });
        internal static IReadOnlyList<CoreCardPackSchoolRate> DropRatesFor(string id) => id == "neutral"
            ? neutralRates : id != null && schoolRates.TryGetValue(id, out var rates)
                ? rates : Array.Empty<CoreCardPackSchoolRate>();
    }

    public sealed partial class CoreVoyageSession
    {
        private sealed class CardPackDrawBucket
        {
            internal CoreCardPackSchoolRate Rate;
            internal List<int> Cards;
        }

        public IReadOnlyList<CoreCardPackSchoolOdds> GetCardPackOdds(string id)
        {
            List<CardPackDrawBucket> buckets = CardPackDrawBuckets(id);
            int totalWeight = buckets.Sum(bucket => bucket.Rate.Weight);
            return buckets.Select(bucket => new CoreCardPackSchoolOdds(bucket.Rate, bucket.Cards.Count, totalWeight))
                .ToList().AsReadOnly();
        }

        private List<CardPackDrawBucket> CardPackDrawBuckets(string id)
        {
            CoreCardPackDefinition pack = CoreCardPackCatalog.Find(id);
            if (pack == null) return new List<CardPackDrawBucket>();
            IReadOnlyList<int> pool = GetCardPackPool(id);
            return pack.DropRates.Select(rate => new CardPackDrawBucket
            {
                Rate = rate,
                Cards = pool.Where(index =>
                    (cardLibrary[index].SchoolId.Length == 0 ? "neutral" : cardLibrary[index].SchoolId) == rate.SchoolId).ToList(),
            }).ToList();
        }

        private static int CardPackRandomIndex(Mulberry32Random random, int count)
        {
            // Rejection sampling avoids modulo bias in both independent draws.
            ulong range = 1UL << 32;
            ulong limit = range - range % (uint)count;
            uint roll;
            do { roll = random.NextUInt(); } while ((ulong)roll >= limit);
            return (int)(roll % (uint)count);
        }
    }
}

