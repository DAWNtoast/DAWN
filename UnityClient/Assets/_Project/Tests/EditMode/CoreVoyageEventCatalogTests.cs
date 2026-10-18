using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageEventCatalogTests
    {
        private static readonly string[] Routes = {
            "route_shattered_sail_reef", "route_golden_tide_trade", "route_moon_tide_ruins" };
        private static readonly string[] SupportedCrew = { "", "navigator", "carpenter", "doctor", "gunner", "cook", "diver" };

        [Test]
        public void RoutePools_ContainDistinctAuthoredContentAndKnownDiscoveryRules()
        {
            var allIds = Routes.SelectMany(route => CoreVoyageEventCatalog.RandomEventIds(route)
                .Concat(CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route).Select(e => e.Id))).ToArray();
            Assert.That(allIds, Has.Length.EqualTo(114));
            Assert.That(allIds.Distinct().Count(), Is.EqualTo(allIds.Length));
            foreach (string route in Routes)
            {
                Assert.That(CoreVoyageEventCatalog.RandomEventIds(route).Count, Is.EqualTo(22));
                Assert.That(LegacyIds(route).Count, Is.EqualTo(10));
                Assert.That(CoreVoyageEventCatalog.CreateFixedEncounters(route).Count, Is.EqualTo(8), "Freeze the SS4 pool.");
                var fixedEvents = CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route);
                Assert.That(fixedEvents.Count, Is.EqualTo(16));
                CollectionAssert.IsSubsetOf(CoreVoyageEventCatalog.CreateFixedEncounters(route).Select(e => e.Id), fixedEvents.Select(e => e.Id));
                CollectionAssert.IsSubsetOf(LegacyIds(route), CoreVoyageEventCatalog.RandomEventIds(route));
                Assert.That(fixedEvents.Count(e => e.CanDiscoverBySight &&
                    CoreVoyageEventCatalog.TryGetFixedTerrain(e.Id, out var t) && t == EventTerrainConstraint.OpenSea),
                    Is.GreaterThanOrEqualTo(2), route + " needs light visible placements along its sailing corridor");
                foreach (CoreEncounter e in fixedEvents)
                {
                    Assert.That(CoreEncounterDiscoveryRules.TryClassify(e.Id, out var kind), Is.True, e.Id);
                    Assert.That(kind, Is.EqualTo(e.Hidden ? EncounterDiscoveryKind.HiddenLocation : EncounterDiscoveryKind.SightedLocation), e.Id);
                    Assert.That(CoreVoyageEventCatalog.TryGetFixedTerrain(e.Id, out var terrain), Is.True, e.Id);
                    Assert.That(terrain, Is.Not.EqualTo(EventTerrainConstraint.Any), e.Id);
                    Assert.That(CoreVoyageEventCatalog.IsRandomEventId(e.Id), Is.False, e.Id);
                }
                foreach (string id in CoreVoyageEventCatalog.RandomEventIds(route))
                {
                    var e = CoreVoyageEventCatalog.CreateRandomEncounter(id, 95);
                    Assert.That(e, Is.Not.Null, id);
                    Assert.That(e.Id, Is.EqualTo(id));
                    Assert.That(e.DiscoveryKind, Is.EqualTo(EncounterDiscoveryKind.TransientEncounter), id);
                    Assert.That(e.HasChartLocation, Is.False, id);
                    Assert.That(e.RepeatPolicy, Is.EqualTo(CoreEncounterRepeatPolicy.RandomVoyage), id);
                }
            }
        }

        [Test]
        public void MandatoryEvents_HaveAZeroSupplyFallbackAndChoicesUseImplementedCrew()
        {
            foreach (string route in Routes)
            foreach (CoreEncounter e in CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route).Concat(
                CoreVoyageEventCatalog.RandomEventIds(route).Select(id => CoreVoyageEventCatalog.CreateRandomEncounter(id, 100))))
            {
                Assert.That(e.Choices.Count, Is.GreaterThanOrEqualTo(3), e.Id);
                foreach (var choice in e.Choices)
                {
                    Assert.That(SupportedCrew, Does.Contain(choice.RequiredCrew), e.Id);
                    Assert.That(choice.Hours, Is.GreaterThanOrEqualTo(0), e.Id);
                    Assert.That(choice.Detail, Is.Not.Empty, e.Id);
                    Assert.That(choice.Effect.SailingBoostHours, Is.InRange(0, 24), e.Id);
                    Assert.That(choice.Effect.SurveyBoostHours, Is.InRange(0, 24), e.Id);
                    if (!string.IsNullOrEmpty(choice.RequiredDiscovery))
                        Assert.That(new[] { "reef_dock_chart", "trade_rescue_seal", "moon_star_chart" }, Does.Contain(choice.RequiredDiscovery), e.Id);
                    if (!string.IsNullOrEmpty(choice.Effect.GrantDiscovery))
                        Assert.That(new[] { "reef_dock_chart", "trade_rescue_seal", "moon_star_chart" }, Does.Contain(choice.Effect.GrantDiscovery), e.Id);
                    if (choice.Effect.Curse > 0) Assert.That(choice.MaximumCurse, Is.EqualTo(9), e.Id);
                    if (choice.Effect.Curse < 0) Assert.That(choice.MinimumCurse, Is.GreaterThanOrEqualTo(1), e.Id);
                }
                if (e.Mandatory) Assert.That(e.Choices.Any(c => IsDepletedCargoFallback(c) &&
                    (!e.Id.Contains("_v2_") || c.Effect.Hull >= 0)), Is.True,
                    e.Id + " must be resolvable without cargo, crew, elapsed rations, or combat");
            }
        }

        private static bool IsDepletedCargoFallback(CoreEncounterChoice c)
        {
            var e = c.Effect;
            return c.Hours == 0 && c.RequiredCrew == "" && c.RequiredShip == "" && !c.StartsBattle &&
                c.RequiredTime == CoreVoyageTimeBand.Any && c.RequiredWeather == CoreVoyageWeather.Any &&
                c.RequiredTerrain == EventTerrainConstraint.Any && c.MinimumCurse == 0 &&
                c.MaximumCurse == int.MaxValue && c.RequiredTreasureClues == 0 && c.RequiredDiscovery == "" &&
                e.Water == 0 && e.Food == 0 && e.Beer == 0 && e.Medicine == 0 && e.Wood == 0 &&
                e.Iron == 0 && e.Cloth == 0 && e.Powder == 0 && e.Cannonballs == 0 && e.Gold == 0 &&
                e.Fame == 0 && e.Gem == 0 && e.AncientMaterial == 0 && e.Relic == 0 && e.BlackPearl == 0 &&
                e.AncientCompass == 0 && e.Lantern == 0 && e.MapFragments == 0 && e.TreasureClues == 0 && e.Curse == 0;
        }

        [Test]
        public void UnknownIds_AreRejectedWithoutAccidentallyCreatingAmbushes()
        {
            foreach (string id in new[] { null, "", "unknown", "random_tailwind", "event_reef_old_cannon" })
            {
                Assert.That(CoreVoyageEventCatalog.IsRandomEventId(id), Is.False);
                Assert.That(CoreVoyageEventCatalog.CreateRandomEncounter(id, 95), Is.Null);
            }
            Assert.That(CoreVoyageEventCatalog.RandomEventIds("unknown"), Is.Empty);
            Assert.That(CoreVoyageEventCatalog.CreateFixedEncounters("unknown"), Is.Empty);
            Assert.That(CoreVoyageEventCatalog.CreateExpandedFixedEncounters("unknown"), Is.Empty);
            Assert.That(LegacyIds("unknown"), Is.Empty);
            Assert.That(CoreVoyageEventCatalog.TryGetFixedTerrain(null, out _), Is.False);
        }

        private static IReadOnlyList<string> LegacyIds(string route) => (IReadOnlyList<string>)typeof(CoreVoyageEventCatalog)
            .GetMethod("LegacyRandomEventIds", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { route });
    }
}
