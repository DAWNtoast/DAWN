using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageEventConditionTests
    {
        [Test]
        public void ChoiceConditionReason_UsesCampaignTimeAndShipType()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.CurrentVoyageTimeBand, Is.EqualTo(CoreVoyageTimeBand.Day));
            Assert.That(session.CurrentVoyageWeather, Is.EqualTo(CoreVoyageWeather.Calm));

            var effect = new VoyageEffect();
            var night = new CoreEncounterChoice("夜航", "", 0, effect,
                requiredTime: CoreVoyageTimeBand.Night);
            Assert.That(session.EncounterChoiceConditionReason(night), Does.Contain("仅限夜航"));

            var salvage = new CoreEncounterChoice("打捞", "", 0, effect,
                requiredShip: "salvage");
            Assert.That(session.EncounterChoiceConditionReason(salvage), Does.Contain("打捞船"));
            Assert.That(session.SelectShip(4), Is.True);
            Assert.That(session.EncounterChoiceConditionReason(salvage), Is.Empty);
        }

        [Test]
        public void TreasureChoices_DeclareTimeTerrainAndCurseRequirements()
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.Ranks[0];
            var encounters = CoreTreasureHuntCatalog.CreateEncounters(rank);
            CoreEncounter star = encounters.Single(encounter => encounter.Id == "treasure_clue_star");
            CoreEncounter reef = encounters.Single(encounter => encounter.Id == "treasure_reef_snare");
            CoreEncounter falseCache = encounters.Single(encounter => encounter.Id == "treasure_false_cache");
            CoreEncounter siren = encounters.Single(encounter => encounter.Id == "treasure_siren_conch");

            Assert.That(star.Choices[0].RequiredTime, Is.EqualTo(CoreVoyageTimeBand.Night));
            Assert.That(reef.Choices[0].RequiredTerrain, Is.EqualTo(EventTerrainConstraint.Reef));
            Assert.That(falseCache.Choices[0].MaximumCurse, Is.EqualTo(9));
            Assert.That(siren.Choices[3].RequiredWeather, Is.EqualTo(CoreVoyageWeather.Fog));
            Assert.That(siren.Choices[3].MinimumCurse, Is.EqualTo(1));
            Assert.That(siren.Choices[3].Effect.Curse, Is.EqualTo(-1));
        }

        [Test]
        public void AuthoredNodes_KeepTerrainConsistentWithTheirEvents()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[2].UnlockLevel) });
            session.SelectRoute(0);
            session.StartVoyage();
            Assert.That(session.Map.Nodes.Single(node => node.Encounter.Id == "common_storm_debris").TerrainConstraint,
                Is.EqualTo(EventTerrainConstraint.Storm));
            Assert.That(session.Map.Nodes.Single(node => node.Encounter.Id == "common_reef_divers").TerrainConstraint,
                Is.EqualTo(EventTerrainConstraint.Reef));

            session.SafeReturn();
            session.ReturnToPortAfterSettlement();
            session.SelectRoute(2);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(2));
            session.StartVoyage();
            Assert.That(session.Map.Nodes.Single(node => node.Encounter.Id == "moon_obelisk").TerrainConstraint,
                Is.EqualTo(EventTerrainConstraint.Beach));
        }

        [Test]
        public void HighValueChoices_AreBoundToTheShipsThatCanExploitThem()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[1].UnlockLevel) });
            session.SelectRoute(0);
            session.StartVoyage();
            CoreEncounter reefCache = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_hidden_cache").Encounter;
            CoreEncounter coral = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_coral_ambush").Encounter;
            Assert.That(reefCache.Choices[1].RequiredShip, Is.EqualTo("salvage"));
            Assert.That(coral.Choices[1].RequiredShip, Does.Contain("gunship").And.Contain("privateer"));

            session.SafeReturn();
            session.ReturnToPortAfterSettlement();
            session.SelectRoute(1);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(1));
            session.StartVoyage();
            CoreEncounter wreck = session.Map.Nodes.Single(node => node.Encounter.Id == "gold_wreck").Encounter;
            Assert.That(wreck.Choices[0].RequiredShip, Is.EqualTo("salvage"));
        }
    }
}
