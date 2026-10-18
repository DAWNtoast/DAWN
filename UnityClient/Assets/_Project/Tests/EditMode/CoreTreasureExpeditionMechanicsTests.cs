using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTreasureExpeditionMechanicsTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void WeatheredToolsUnlockMultipleSitesAndCannotBeClaimedTwice()
        {
            var s = Start("weathered");
            Enter(s, "treasure_core"); LockedWithoutCost(s, 0);
            Enter(s, "weathered_winch"); LockedWithoutCost(s, 0);
            Assert.That(s.IsChoiceAvailable(2), Is.True, "Slow repair prevents a material/crew softlock.");
            Assert.That(s.SkipEncounter(), Is.True);
            Assert.That(s.Map.Nodes.Single(n => n.Encounter.Id == "weathered_winch").Visited, Is.False);
            Choose(s, "weathered_camp", 0);
            Assert.That(new[] { s.Cargo.Wood, s.Cargo.Iron, s.Cargo.Cloth }, Is.EqualTo(new[] { 6, 8, 3 }));
            Choose(s, "weathered_winch", 0);
            Enter(s, "weathered_winch"); LockedWithoutCost(s, 2);
            Enter(s, "treasure_core"); LockedWithoutCost(s, 0);
            Choose(s, "weathered_bell", 0);
            Assert.That(s.Cargo.Iron, Is.Zero); Assert.That(s.Cargo.Cloth, Is.Zero);
            Choose(s, "weathered_chain_gate", 0);
            Assert.That(s.SailingBoostHoursRemaining, Is.EqualTo(18));
            Choose(s, "weathered_glass_dome", 0);
            Assert.That(s.SurveyBoostHoursRemaining, Is.EqualTo(18));
            Assert.That(s.HasTreasureMark("weathered_winch") && s.HasTreasureMark("weathered_bell"), Is.True,
                "Using tools must not consume them.");
            Enter(s, "treasure_core"); Assert.That(s.IsChoiceAvailable(0), Is.True);
        }

        [Test]
        public void WeatheredResupplyCampStillAllowsBothToolsThroughSlowRepair()
        {
            var s = Start("weathered");
            Choose(s, "weathered_camp", 1);
            Enter(s, "weathered_camp"); LockedWithoutCost(s, 0);
            Choose(s, "weathered_winch", 2); Choose(s, "weathered_bell", 2);
            Assert.That(s.Cargo.Wood + s.Cargo.Iron + s.Cargo.Cloth, Is.Zero);
            Enter(s, "treasure_core"); Assert.That(s.IsChoiceAvailable(0), Is.True);
        }

        [Test]
        public void GildedCreditSpentOnSuppliesMustBeEarnedAgainBeforeLegalInheritance()
        {
            var s = Start("gilded");
            Enter(s, "gilded_bonded_vault"); LockedWithoutCost(s, 0);
            Choose(s, "gilded_manifest", 1);
            Assert.That(s.TreasureExpeditionCredit, Is.EqualTo(3)); Assert.That(s.TreasureExpeditionHeat, Is.EqualTo(2));
            Enter(s, "treasure_core"); LockedWithoutCost(s, 0);
            Assert.That(s.ChoiceUnavailableReason(0), Does.Contain("通缉"));
            Choose(s, "gilded_customs", 0);
            Assert.That(s.TreasureExpeditionHeat, Is.Zero);
            Enter(s, "treasure_core"); Assert.That(s.IsChoiceAvailable(0), Is.True);
            Choose(s, "gilded_bonded_vault", 0);
            Assert.That(s.TreasureExpeditionCredit, Is.EqualTo(1)); Assert.That(s.Cargo.Medicine, Is.EqualTo(3));
            Enter(s, "treasure_core"); LockedWithoutCost(s, 0);
            Assert.That(s.ChoiceUnavailableReason(0), Does.Contain("信用"));
            Choose(s, "gilded_witness", 1);
            Enter(s, "gilded_witness"); LockedWithoutCost(s, 1);
            Enter(s, "treasure_core"); Assert.That(s.IsChoiceAvailable(0), Is.True);
            Assert.That(s.ResolveChoice(0), Is.True);
            Assert.That(s.TreasureExpeditionCredit, Is.Zero); Assert.That(s.Cargo.Curse, Is.Zero);
            Assert.That(s.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
        }

        [Test]
        public void GildedSmugglingUsesWitnessAndPaysDamageAndCurseInsteadOfCredit()
        {
            var s = Start("gilded");
            Enter(s, "treasure_core"); LockedWithoutCost(s, 1);
            Choose(s, "gilded_witness", 1);
            Enter(s, "treasure_core");
            int hull = s.Hull, credit = s.TreasureExpeditionCredit;
            Assert.That(s.ResolveChoice(1), Is.True);
            Assert.That(s.Hull, Is.EqualTo(hull - 18)); Assert.That(s.Cargo.Curse, Is.EqualTo(3));
            Assert.That(s.TreasureExpeditionHeat, Is.EqualTo(3));
            Assert.That(s.TreasureExpeditionCredit, Is.EqualTo(credit));
            Assert.That(s.Cargo.BlackPearl, Is.EqualTo(1));
        }

        [Test]
        public void MoonlitThreeDistinctRitualsAndPurificationAreNeededForCleanReward()
        {
            var s = Start("moonlit");
            foreach (string id in new[] { "moonlit_dawn", "moonlit_dusk", "moonlit_midnight" })
            {
                Choose(s, id, 1);
                Enter(s, id); LockedWithoutCost(s, 1);
            }
            Assert.That(s.TreasureExpeditionResonance, Is.EqualTo(3)); Assert.That(s.Cargo.Curse, Is.EqualTo(3));
            Enter(s, "treasure_core"); LockedWithoutCost(s, 0);
            Assert.That(s.IsChoiceAvailable(1), Is.True, "Risky recovery remains an alternative to cleansing.");
            Choose(s, "moonlit_cleanse", 0);
            Assert.That(s.Cargo.Curse, Is.Zero);
            Choose(s, "moonlit_star_current", 0);
            Assert.That(s.SailingBoostHoursRemaining, Is.EqualTo(24)); Assert.That(s.SurveyBoostHoursRemaining, Is.EqualTo(12));
            Enter(s, "treasure_core"); Assert.That(s.ResolveChoice(0), Is.True);
            Assert.That(s.TreasureExpeditionResonance, Is.EqualTo(3));
            Assert.That(s.Cargo.Relic, Is.EqualTo(3)); Assert.That(s.Cargo.AncientCompass, Is.EqualTo(1));
        }

        [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
        [TestCase(1, 1)] [TestCase(2, 1)] [TestCase(5, 1)]
        public void MoonlitCleanseRemovesUpToItsLimitWithoutRequiringMoreCurse(int curse, int choice)
        {
            var s = Start("moonlit"); s.Cargo.Curse = curse; s.Cargo.Lantern = 1;
            Choose(s, "moonlit_cleanse", choice);
            Assert.That(s.Cargo.Curse, Is.Zero);
            Assert.That(s.Cargo.Lantern, Is.EqualTo(choice == 1 ? 0 : 1));
        }

        [Test]
        public void MoonlitChoirCanRewardAnAlreadyCleanCrew()
        {
            var s = Start("moonlit", 9); // 17:00, the visible dusk window.
            Choose(s, "moonlit_dusk", 0);
            Assert.That(s.Cargo.Curse, Is.Zero);
            Enter(s, "moonlit_choir"); Assert.That(s.ResolveChoice(0), Is.True);
            Assert.That(s.Cargo.Curse, Is.Zero);
        }

        [TestCase("moonlit_dawn")]
        [TestCase("moonlit_dusk")]
        [TestCase("moonlit_midnight")]
        public void MoonlitThreeHourWaitCanReachEveryRitualWindowFromEveryStartingHour(string altar)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                var s = Start("moonlit", hour + .25);
                bool reached = false;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Enter(s, altar);
                    if (s.IsChoiceAvailable(0)) { reached = true; break; }
                    double before = s.TotalHours;
                    Assert.That(s.ResolveChoice(2), Is.True);
                    Assert.That(s.TotalHours - before, Is.EqualTo(3));
                    Assert.That(s.TreasureExpeditionResonance, Is.Zero); Assert.That(s.Cargo.Curse, Is.Zero);
                    Assert.That(s.Map.Nodes.Single(n => n.Encounter.Id == altar).Visited, Is.False);
                }
                Assert.That(reached, Is.True, altar + " from elapsed hour " + hour);
                Assert.That(s.ResolveChoice(0), Is.True);
                Assert.That(s.TreasureExpeditionResonance, Is.EqualTo(1)); Assert.That(s.Cargo.Curse, Is.Zero);
            }
        }

        [TestCase("weathered", 1)] [TestCase("gilded", 2)] [TestCase("moonlit", 2)]
        public void EarlyWithdrawalRemainsPossibleAndNextVoyageHasNoExpeditionState(string rank, int fallback)
        {
            var s = Start(rank); Enter(s, "treasure_core");
            LockedWithoutCost(s, 0);
            Assert.That(s.ResolveChoice(fallback), Is.True);
            Assert.That(s.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
            s.ReturnToPortAfterSettlement();
            Assert.That(s.TreasureExpeditionStatusSummary, Is.Empty);
            Assert.That(s.TreasureExpeditionCredit + s.TreasureExpeditionHeat + s.TreasureExpeditionResonance, Is.Zero);
        }

        private static CoreVoyageSession Start(string rank, double clockHours = 0)
        {
            var s = new CoreVoyageSession();
            Assert.That(s.RestoreGameClock(clockHours), Is.True);
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = rank,
                maps = new[] { new CoreTreasureMapSave { id = rank, seed = "MECHANICS-" + rank, rankId = rank, contentVersion = 1 } } });
            s.Cargo.Water = s.Cargo.Food = 240;
            s.Cargo.Wood = s.Cargo.Iron = s.Cargo.Cloth = s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Medicine = s.Cargo.Beer = 0;
            s.ConfigureCrewCapabilities(true, true, false, false, false, false);
            Assert.That(s.TryStartTreasureVoyage(out string reason), Is.True, reason);
            return s;
        }

        // Core rule tests enter authored events directly; graphical PlayMode owns real sailing and buttons.
        private static void Enter(CoreVoyageSession s, string id)
        {
            var encounter = CoreTreasureExpeditionCatalog.CreateEncounters(s.ActiveTreasureMap.Rank).Single(e => e.Id == id);
            var node = s.Map.Nodes.SingleOrDefault(n => n.Encounter.Id == id);
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(s, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(s, encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(s, CoreDemoPhase.Encounter);
        }
        private static void Choose(CoreVoyageSession s, string id, int choice)
        {
            Enter(s, id); Assert.That(s.ResolveChoice(choice), Is.True, id + ": " + s.ChoiceUnavailableReason(choice));
        }
        private static void LockedWithoutCost(CoreVoyageSession s, int choice)
        {
            double hours = s.TotalHours;
            int[] before = { s.Cargo.Food, s.Cargo.Water, s.Cargo.Gold, s.Cargo.Wood, s.Cargo.Iron,
                s.Cargo.Cloth, s.Cargo.Curse, s.TreasureExpeditionCredit, s.TreasureExpeditionHeat, s.TreasureExpeditionResonance };
            Assert.That(s.IsChoiceAvailable(choice), Is.False); Assert.That(s.ResolveChoice(choice), Is.False);
            Assert.That(s.TotalHours, Is.EqualTo(hours));
            Assert.That(new[] { s.Cargo.Food, s.Cargo.Water, s.Cargo.Gold, s.Cargo.Wood, s.Cargo.Iron,
                s.Cargo.Cloth, s.Cargo.Curse, s.TreasureExpeditionCredit, s.TreasureExpeditionHeat, s.TreasureExpeditionResonance }, Is.EqualTo(before));
        }
    }
}
