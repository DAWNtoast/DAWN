using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalBattleBalanceSimulationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AuditFixture_UsesTheSameShipCrewAndAuthoredLegalDecks(bool focused)
        {
            CoreVoyageSession session = NavalBattleBalanceSimulator.CreateSession("AUDIT-FIXTURE", 2, focused);
            Assert.That(session.SelectedShip.Id, Is.EqualTo("patrol"));
            Assert.That(session.MaxHull, Is.EqualTo(155));
            Assert.That(session.Firepower, Is.EqualTo(22));
            Assert.That(session.Armor, Is.EqualTo(10));
            Assert.That(session.CrewCountValue, Is.EqualTo(22));
            Assert.That(session.HasNavigator && session.HasCarpenter, Is.True);
            Assert.That(session.HasDoctor || session.HasGunner || session.HasCook || session.HasDiver, Is.False);
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(40));
            Assert.That(session.Cargo.Powder, Is.EqualTo(25));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                var deck = session.GetBattleDeck(stage);
                Assert.That(deck.Count, Is.InRange(CoreVoyageSession.MinimumBaseBattleDeckSize, CoreVoyageSession.MaximumBaseBattleDeckSize));
                Assert.That(deck.GroupBy(index => index).Max(group => group.Count()), Is.LessThanOrEqualTo(CoreVoyageSession.BaseBattleCardCopies));
                Assert.That(deck.All(index => session.GetBattleCard(index).IsAllowedInStage(stage)), Is.True);
                Assert.That(deck.All(index => session.GetBattleCard(index).UpgradeLevel == 2), Is.True);
            }
            string[] expected = NavalBattleBalanceSimulator.FixtureCrewIds.Select(id => CoreSpecialCardCatalog.ForCrew(id).Card.Id)
                .Concat(NavalBattleBalanceSimulator.FixtureComponentIds.Select(id => CoreSpecialCardCatalog.ForComponent(id).Card.Id)).ToArray();
            string[] injected = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .SelectMany(stage => session.GetBonusBattleCards(stage)).Distinct().Select(index => session.GetBattleCard(index).Id).ToArray();
            Assert.That(injected, Is.EquivalentTo(expected));
            Assert.That(injected.Length, Is.EqualTo(7));
            Assert.That(injected.All(id => id.StartsWith("card_special_", StringComparison.Ordinal)), Is.True);
            Assert.That(session.CardUpgradeLevels.Keys.All(id => !CoreVoyageSession.IsSpecialBattleCard(id)), Is.True);
        }

        [TestCase(0, 0)]
        [TestCase(1, 120)]
        [TestCase(2, 360)]
        [TestCase(3, 840)]
        public void AuditFixture_TrainsSpecialCardsWithSavedVoyageExperienceNotPaidUpgrades(int level, int experience)
        {
            CoreVoyageSession session = NavalBattleBalanceSimulator.CreateSession("AUDIT-SPECIAL-XP", level, false);
            Assert.That(session.TotalDistanceNm, Is.Zero, "Fixture only restores already-earned experience; it does not invent sailed distance.");
            foreach (string id in session.AcquiredSpecialCardIds)
            {
                Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(experience));
                Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(level));
                Assert.That(session.CardUpgradeLevels.ContainsKey(id), Is.False);
            }
            Assert.That(session.GetSpecialCardExperience("card_navigator_course"), Is.Zero, "No legacy paid-card migration is used by this audit.");
            Assert.That(session.GetSpecialCardExperience("card_component_cannon"), Is.Zero);
        }

        [TestCase(0, NavalBattleBalanceSimulator.Tactic.Cannon)]
        [TestCase(1, NavalBattleBalanceSimulator.Tactic.Cannon)]
        [TestCase(2, NavalBattleBalanceSimulator.Tactic.Cannon)]
        [TestCase(3, NavalBattleBalanceSimulator.Tactic.Cannon)]
        [TestCase(0, NavalBattleBalanceSimulator.Tactic.Boarding)]
        [TestCase(1, NavalBattleBalanceSimulator.Tactic.Boarding)]
        [TestCase(2, NavalBattleBalanceSimulator.Tactic.Boarding)]
        [TestCase(3, NavalBattleBalanceSimulator.Tactic.Boarding)]
        public void AuditDecisions_AreDeterministicAndReportActualOutcomes(int level, NavalBattleBalanceSimulator.Tactic tactic)
        {
            const string boss = "boss_route_moon_tide_ruins";
            var first = NavalBattleBalanceSimulator.Run("AUDIT-REPEAT", boss, level, tactic, false);
            var repeated = NavalBattleBalanceSimulator.Run("AUDIT-REPEAT", boss, level, tactic, false);
            Assert.That(repeated.Csv(), Is.EqualTo(first.Csv()));
            Assert.That(first.Cards, Is.GreaterThan(0));
            Assert.That(first.Turns, Is.InRange(1, NavalBattleBalanceSimulator.TurnWatchdog));
            Assert.That(first.CannonballsUsed, Is.InRange(0, 40));
            Assert.That(first.PowderUsed, Is.InRange(0, 25));
            if (first.Won)
            {
                Assert.That(first.Hull, Is.GreaterThan(0));
                Assert.That(first.EnemyHull == 0 || first.EnemyCrew == 0, Is.True,
                    "A win must come from real hull sinking or crew defeat, never from a scoring shortcut.");
            }
            else Assert.That(first.Outcome, Is.EqualTo("loss").Or.EqualTo("action-watchdog").Or.EqualTo("turn-watchdog"));
        }

        [TestCase(NavalBattleBalanceSimulator.Loadout.Starter, 10, 0)]
        [TestCase(NavalBattleBalanceSimulator.Loadout.Patrol, 20, 2)]
        [TestCase(NavalBattleBalanceSimulator.Loadout.Gunship, 26, 2)]
        public void CalibrationFixture_CountsOrdinarySailorsSeparatelyFromSpecialists(
            NavalBattleBalanceSimulator.Loadout loadout, int ordinary, int specialists)
        {
            var session = NavalBattleBalanceSimulator.CreateSession("CALIBRATION-CREW", 3, false, loadout, 2);
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(specialists));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));
            Assert.That(session.AcquiredSpecialCardIds.Count, Is.EqualTo(specialists == 0 ? 0 : 7));
        }

        [Test]
        public void CalibrationScenarios_IncludeTutorialThreeOrdinaryThreeRegionalVaultAndFiveLegends()
        {
            var scenarios = NavalBattleBalanceSimulator.CalibrationScenarios;
            Assert.That(scenarios.Count, Is.EqualTo(13));
            Assert.That(scenarios.Select(x => x.EnemyId).Distinct().Count(), Is.EqualTo(13));
            Assert.That(scenarios.Count(x => x.Tutorial), Is.EqualTo(1));
            Assert.That(scenarios.Count(x => x.EnemyId.StartsWith("boss_route_")), Is.EqualTo(3));
            Assert.That(scenarios.Count(x => x.EnemyId.StartsWith("legendary_pirate_")), Is.EqualTo(5));
            foreach (var scenario in scenarios)
            {
                var session = NavalBattleBalanceSimulator.CreateSession("CALIBRATION-CATALOG", 0, false,
                    NavalBattleBalanceSimulator.Loadout.Starter, scenario.Route, scenario.Tutorial);
                Assert.That(session.Map.Nodes.Count(x => x.Encounter.Id == scenario.EnemyId), Is.EqualTo(1), scenario.EnemyId);
            }
        }

        [TestCase(NavalBattleBalanceSimulator.Loadout.Starter, NavalBattleBalanceSimulator.Tactic.Cannon)]
        [TestCase(NavalBattleBalanceSimulator.Loadout.Starter, NavalBattleBalanceSimulator.Tactic.Boarding)]
        [TestCase(NavalBattleBalanceSimulator.Loadout.Gunship, NavalBattleBalanceSimulator.Tactic.Cannon)]
        [TestCase(NavalBattleBalanceSimulator.Loadout.Gunship, NavalBattleBalanceSimulator.Tactic.Boarding)]
        public void MasteredLegendaryStrategy_TerminatesWithLegalCostsInsteadOfCyclingForever(
            NavalBattleBalanceSimulator.Loadout loadout, NavalBattleBalanceSimulator.Tactic tactic)
        {
            var scenario = NavalBattleBalanceSimulator.CalibrationScenarios.Single(x => x.EnemyId == "legendary_pirate_iron_oath");
            var result = NavalBattleBalanceSimulator.Run("FINITE-LEGENDARY", scenario, 3, tactic, false, loadout);
            Assert.That(result.Outcome, Is.EqualTo("win").Or.EqualTo("loss"));
            Assert.That(result.MaxCombo, Is.LessThan(NavalBattleBalanceSimulator.ActionWatchdog));
            Assert.That(result.EnergySpent, Is.GreaterThan(0));
            Assert.That(result.ZeroCostCards, Is.LessThan(result.Cards));
            Assert.That(result.InitialCrewHealth, Is.EqualTo(20 + 4 * result.OrdinaryCrew));
            Assert.That(result.CannonballsUsed, Is.InRange(0, 40));
            Assert.That(result.PowderUsed, Is.InRange(0, 25));
        }

        [Test]
        public void AuditUsesAllSevenAuthoredThirdChartBosses()
        {
            string[] ids = NavalBattleBalanceSimulator.BossIds();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(7));
            Assert.That(ids.Count(id => id.StartsWith("boss_", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(ids.Count(id => id.StartsWith("treasure_boss_", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(ids.Count(id => id.StartsWith("legendary_", StringComparison.Ordinal)), Is.EqualTo(5));
        }
    }
}
