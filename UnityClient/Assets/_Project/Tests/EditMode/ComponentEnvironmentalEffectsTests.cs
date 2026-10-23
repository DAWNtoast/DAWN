using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComponentEnvironmentalEffectsTests
    {
        [Test]
        public void ExpandedCatalog_AddsFiveContextAwareSidegradesPerSlot()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();

            Assert.That(catalog.Entries.Count, Is.EqualTo(ComponentArtCatalog.ExpectedEntryCount));
            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            Assert.That(catalog.ExpandedEntries.Select(entry => entry.Id).Distinct().Count(),
                Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            Assert.That(catalog.ExpandedIsAvailable, Is.True);
            Assert.That(catalog.ExpandedEntries.Skip(ComponentArtCatalog.ExpectedEntryCount)
                .Select(entry => entry.Slot).Distinct().Count(), Is.EqualTo(ComponentArtCatalog.SlotCount));
        }

        [Test]
        public void MatchingTimeRouteWeather_ProducesAConditionalSidegrade()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry entry = catalog.GetById("bow_freewake_lance");
            ComponentContextEffect none = entry.Evaluate(new ComponentVoyageContext(
                ComponentTimeBand.Night, ComponentRouteType.DeepSea, ComponentWeatherType.Fog));
            ComponentContextEffect match = entry.Evaluate(new ComponentVoyageContext(
                ComponentTimeBand.Dusk, ComponentRouteType.OpenSea, ComponentWeatherType.Storm));

            Assert.That(none.IsActive, Is.False);
            Assert.That(match.IsActive, Is.True);
            Assert.That(match.SpeedBonus, Is.GreaterThan(0f));
            Assert.That(match.ArmorBonus, Is.GreaterThan(0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LocalTerrain_TakesPriorityOverTheSelectedVoyageLane(int routeIndex)
        {
            CoreVoyageSession session = CreateVoyage(routeIndex);
            SeaTerrainType[] terrains = { SeaTerrainType.Beach, SeaTerrainType.Island,
                SeaTerrainType.Strait, SeaTerrainType.Reef, SeaTerrainType.Shoal, SeaTerrainType.KelpField };
            ComponentRouteType[] expected = { ComponentRouteType.Coastal, ComponentRouteType.Coastal,
                ComponentRouteType.Strait, ComponentRouteType.Reef, ComponentRouteType.Reef, ComponentRouteType.Reef };
            int cell = session.Map.CellIndex(session.Position);

            for (int index = 0; index < terrains.Length; index++)
            {
                session.Map.TerrainCells[cell] = terrains[index];
                Assert.That(ComponentVoyageContextResolver.FromSession(session).Route, Is.EqualTo(expected[index]),
                    "Terrain " + terrains[index] + " must take priority over voyage lane " + routeIndex);
            }
        }

        [TestCase(0, ComponentRouteType.OpenSea)]
        [TestCase(1, ComponentRouteType.Trade)]
        [TestCase(2, ComponentRouteType.DeepSea)]
        public void OpenWaterAndWeather_UseTheVoyageLaneWithoutLosingWeather(int routeIndex, ComponentRouteType expected)
        {
            CoreVoyageSession session = CreateVoyage(routeIndex);
            int cell = session.Map.CellIndex(session.Position);
            session.Map.TerrainCells[cell] = SeaTerrainType.OpenWater;
            Assert.That(ComponentVoyageContextResolver.FromSession(session).Route, Is.EqualTo(expected));

            session.Map.TerrainCells[cell] = SeaTerrainType.FogBank;
            ComponentVoyageContext fog = ComponentVoyageContextResolver.FromSession(session);
            Assert.That(fog.Route, Is.EqualTo(expected));
            Assert.That(fog.Weather, Is.EqualTo(ComponentWeatherType.Fog));

            session.Map.TerrainCells[cell] = SeaTerrainType.StormFront;
            ComponentVoyageContext storm = ComponentVoyageContextResolver.FromSession(session);
            Assert.That(storm.Route, Is.EqualTo(expected));
            Assert.That(storm.Weather, Is.EqualTo(ComponentWeatherType.Storm));
        }

        [Test]
        public void ActiveEncounterTerrain_TakesPriorityAndClearsBackToTheCurrentMapCell()
        {
            CoreVoyageSession session = CreateVoyage(1);
            session.Map.TerrainCells[session.Map.CellIndex(session.Position)] = SeaTerrainType.Beach;
            FieldInfo activeNode = typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic);
            activeNode.SetValue(session, new CoreMapNode { TerrainConstraint = EventTerrainConstraint.Strait });

            Assert.That(ComponentVoyageContextResolver.FromSession(session).Route, Is.EqualTo(ComponentRouteType.Strait));
            activeNode.SetValue(session, null);
            Assert.That(ComponentVoyageContextResolver.FromSession(session).Route, Is.EqualTo(ComponentRouteType.Coastal));
        }

        [Test]
        public void SailingToAnotherCell_ReevaluatesTheLocationInsteadOfKeepingTheDepartureRoute()
        {
            CoreVoyageSession session = CreateVoyage(0);
            session.Map.Nodes.Clear();
            for (int index = 0; index < session.Map.CellCount; index++)
            {
                session.Map.TerrainCells[index] = SeaTerrainType.OpenWater;
                session.Map.CurrentStrengths[index] = 0f;
            }
            session.Map.GlobalWindStrength = 0f;
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(50f, 32f));
            session.SetHeading(SailingDirection.East);
            session.SetDistance(80);
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast.Blocked, Is.False);
            int startCell = session.Map.CellIndex(session.Position);
            int targetCell = session.Map.CellIndex(forecast.Target);
            Assert.That(targetCell, Is.Not.EqualTo(startCell));
            session.Map.TerrainCells[targetCell] = SeaTerrainType.Beach;
            Assert.That(ComponentVoyageContextResolver.FromSession(session).Route, Is.EqualTo(ComponentRouteType.OpenSea));

            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Map.CellIndex(session.Position), Is.EqualTo(targetCell));
            Assert.That(ComponentVoyageContextResolver.FromSession(session).Route, Is.EqualTo(ComponentRouteType.Coastal));
        }

        private static CoreVoyageSession CreateVoyage(int routeIndex)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(session.MaximumPirateLevel) });
            session.SelectRoute(routeIndex);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(routeIndex));
            session.SetSeed("COMPONENT-ENVIRONMENT-20261022");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            return session;
        }
    }
}
