using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private static Dictionary<string, int> StrictTreasureSupplyTargets(int recommendedDays, double daily, bool diver)
        {
            int rations = (int)Math.Ceiling(daily * (recommendedDays * 2 + 2));
            return new Dictionary<string, int>
            {
                { "water", rations }, { "food", rations },
                { "powder", diver ? 24 : 27 }, { "cannonballs", 20 },
                { "wood", 10 }, { "medicine", 3 }
            };
        }

        private static double StrictTreasurePlanVolume(double loadedVolume, Func<string, int> cargo,
            Dictionary<string, int> targets)
        {
            double volume = loadedVolume;
            foreach (var target in targets)
            {
                int missing = Math.Max(0, target.Value - cargo(target.Key));
                PortMarketItem item = PortMarketCatalog.AllItems.Single(value => PortMarketCatalog.Key(value) == target.Key);
                volume += missing * (double)PortMarketCatalog.CargoVolume(item);
            }
            return volume;
        }

        [TestCase(1, true, 130.12d)]
        [TestCase(6, true, 130.12d)]
        [TestCase(2, false, 162.92d)]
        [TestCase(7, false, 195.72d)]
        public void StrictTreasureProvision_CandidateFullCrewMustFitItsCompleteLoad(int shipIndex, bool fits, double expectedVolume)
        {
            var session = new CoreVoyageSession();
            CoreShipDefinition candidate = CoreVoyageSession.Ships[shipIndex];
            // Quoting the candidate's crew on the current sloop would clamp it
            // to ten. Production consumption is linear in the per-person quote.
            double daily = session.EstimateDailyRationConsumption(1, true) * candidate.CrewCount;
            var switched = new CoreVoyageSession();
            Assert.That(switched.SelectShip(shipIndex), Is.True);
            Assert.That(daily, Is.EqualTo(switched.EstimateDailyRationConsumption(switched.EffectiveCrewCount, true)));
            var targets = StrictTreasureSupplyTargets(CoreTreasureHuntCatalog.FindRank("weathered").RecommendedVoyageDays, daily, true);
            double volume = StrictTreasurePlanVolume(4d * PortMarketCatalog.CargoVolume(PortMarketItem.Beer), _ => 0, targets);
            Assert.That(volume, Is.EqualTo(expectedVolume).Within(.00002d));
            Assert.That(volume <= candidate.CargoCapacity + .001d, Is.EqualTo(fits));
        }

        [Test]
        public void StrictTreasureProvision_ExistingExtraCargoCanDisqualifyAnOtherwiseSuitableShip()
        {
            var session = new CoreVoyageSession();
            CoreShipDefinition merchant = CoreVoyageSession.Ships[1];
            var targets = StrictTreasureSupplyTargets(18,
                session.EstimateDailyRationConsumption(1, true) * merchant.CrewCount, true);
            double extraIron = 11d * PortMarketCatalog.CargoVolume(PortMarketItem.Iron);
            double volume = StrictTreasurePlanVolume(extraIron, _ => 0, targets);
            Assert.That(volume, Is.GreaterThan(merchant.CargoCapacity));
        }

        [Test]
        public void StrictTreasureProvision_SuppliesAboveTargetCannotCreateNegativeMissingVolume()
        {
            var targets = StrictTreasureSupplyTargets(18, 9d, true);
            var carried = targets.ToDictionary(pair => pair.Key, pair => pair.Value + 5);
            double loadedVolume = carried.Sum(pair => pair.Value * (double)PortMarketCatalog.CargoVolume(
                PortMarketCatalog.AllItems.Single(item => PortMarketCatalog.Key(item) == pair.Key)));
            Assert.That(StrictTreasurePlanVolume(loadedVolume, key => carried[key], targets), Is.EqualTo(loadedVolume));
        }

        [TestCase("weathered", true, 342, 24)]
        [TestCase("gilded", false, 414, 27)]
        [TestCase("moonlit", true, 486, 24)]
        public void StrictTreasureProvision_UsesActualRankAndDiverRequirement(string rankId, bool diver, int rations, int powder)
        {
            var targets = StrictTreasureSupplyTargets(CoreTreasureHuntCatalog.FindRank(rankId).RecommendedVoyageDays, 9d, diver);
            Assert.That(targets["water"], Is.EqualTo(rations));
            Assert.That(targets["food"], Is.EqualTo(rations));
            Assert.That(targets["powder"], Is.EqualTo(powder));
            Assert.That(targets["cannonballs"], Is.EqualTo(20));
            Assert.That(targets["wood"], Is.EqualTo(10));
            Assert.That(targets["medicine"], Is.EqualTo(3));
        }
    }
}
