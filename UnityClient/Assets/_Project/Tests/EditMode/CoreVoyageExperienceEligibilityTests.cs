using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageExperienceEligibilityTests
    {
        private const string ShortChart = "SS2.0.0.1.0.2.72533DBC.00000000.4858B60F";

        [Test]
        public void FortyFivePaidStarvationReturns_FromFreshCampaign_DoNotEarnExperience()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureDepartureCrew(4, 0);
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.TryDepositTravelCash(), Is.True);
            Assert.That(session.TrySelectChartSeed(ShortChart, out string reason), Is.True, reason);
            decimal startingGold = session.ProfileGold;
            decimal cost = 0m;
            string initialWarehouse = session.PortWarehouse.Serialize();
            for (int trip = 0; trip < 45; trip++)
            {
                var order = new VoyageSupplyOrder { Water = 1, Food = 1 };
                cost += session.QuoteSupplyPurchase(order).Cost;
                Assert.That(session.TryPurchaseSupplies(order), Is.True, "Trip " + trip);
                session.StartVoyage();
                SailTwentyMiles(session);
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.Cargo.Food, Is.Zero);
                Assert.That(session.SafeReturn(), Is.True);
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                Assert.That(session.Settlement.BankedPirateExperience, Is.Zero, "Trip " + trip);
                session.ReturnToPortAfterSettlement();
                session.ReturnToPortAfterSettlement();
                Assert.That(session.PirateExperience, Is.Zero, "Trip " + trip);
                Assert.That(session.PirateLevel, Is.EqualTo(1));
            }
            Assert.That(cost, Is.EqualTo(5.76m));
            Assert.That(session.ProfileGold, Is.EqualTo(startingGold - cost));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(initialWarehouse));
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void ActualEmptyTreasureStarvationReturn_DoesNotAwardExperience(string rank)
        {
            var session = TreasureProfile(rank);
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureDepartureCrew(4, 0);
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.TryPurchaseSupplies(new VoyageSupplyOrder { Water = 4, Food = 4 }), Is.True);
            int initialXp = session.PirateExperience;
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            SailTwentyMiles(session);
            // Treasure departures require a full day of rations. Discarding the
            // remainder is a normal player action and must not bypass eligibility.
            Assert.That(session.TryDiscardVoyageCargo("water", session.Cargo.Water), Is.True);
            Assert.That(session.TryDiscardVoyageCargo("food", session.Cargo.Food), Is.True);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedPirateExperience, Is.Zero);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.PirateExperience, Is.EqualTo(initialXp));
            Assert.That(session.TreasureMaps, Is.Empty, "Failure must not refund the used chart.");
        }

        [TestCase(false, 0f, false)]
        [TestCase(false, 20f, false)]
        [TestCase(false, 2700f, false)]
        [TestCase(false, 2700f, true)]
        [TestCase(true, 20f, false)]
        [TestCase(true, 2400f, false)]
        [TestCase(true, 2400f, true)]
        public void EmptyFailure_RejectsMinimumAndDistanceExperience(bool treasure, float distance, bool skipped)
        {
            var session = treasure ? TreasureProfile("weathered") : new CoreVoyageSession();
            session.StartVoyage();
            Set(session, "TotalDistanceNm", distance);
            if (skipped) { session.Map.Nodes[0].Visited = true; session.Map.Nodes[0].Skipped = true; }
            session.Cargo.Water = 0;
            Assert.That(session.CompletedEventCount(), Is.Zero);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedPirateExperience, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompletedAuthoredEvent_StillEarnsExperienceOnFailure(bool treasure)
        {
            var session = treasure ? TreasureProfile("weathered") : new CoreVoyageSession();
            session.StartVoyage();
            string eventId = treasure ? "weathered_camp" : "event_reef_sharp_current";
            CoreMapNode node = session.Map.Nodes.Single(n => n.Encounter.Id == eventId);
            Enter(session, node);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.CompletedEventCount(), Is.EqualTo(1));
            Set(session, "TotalDistanceNm", 100f * session.Map.Route.NauticalMilesPerMapUnit);
            session.Cargo.Water = 0;
            int initialXp = session.PirateExperience;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(175));
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.PirateExperience, Is.EqualTo(initialXp + 175));
        }

        [TestCase(false, 0)]
        [TestCase(true, 165)]
        public void EmptySafeReturn_PreservesExistingOrdinaryAndTreasureRules(bool treasure, int expected)
        {
            var session = treasure ? TreasureProfile("weathered") : new CoreVoyageSession();
            session.StartVoyage();
            Set(session, "TotalDistanceNm", 100f * session.Map.Route.NauticalMilesPerMapUnit);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(expected));
        }

        [TestCase(VoyageOutcome.ArrivedAtPort)]
        [TestCase(VoyageOutcome.TreasureRecovered)]
        public void SuccessfulCompletion_RetainsDistanceAndCompletionBonus(VoyageOutcome outcome)
        {
            var session = new CoreVoyageSession(); session.StartVoyage();
            Set(session, "TotalDistanceNm", 100f * session.Map.Route.NauticalMilesPerMapUnit);
            typeof(CoreVoyageSession).GetMethod("FinishVoyage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { outcome });
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(225));
        }

        [TestCase(1, false, 20)]
        [TestCase(1, true, 20)]
        [TestCase(CoreGuidedTutorialContent.NavigatorVoyageLeg, false, 20)]
        [TestCase(CoreGuidedTutorialContent.NavigatorVoyageLeg, true, 20)]
        [TestCase(CoreGuidedTutorialContent.BossVoyageLeg, false, 80)]
        [TestCase(CoreGuidedTutorialContent.BossVoyageLeg, true, 80)]
        public void GuidedReturn_RetainsSeparateTutorialRewards(int leg, bool failure, int expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.ConfigureGuidedTutorialVoyage(leg), Is.True);
            session.StartVoyage();
            SailTwentyMiles(session);
            if (failure) session.Cargo.Water = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(failure ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn));
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(expected));
        }

        private static CoreVoyageSession TreasureProfile(string rank)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 1400,
                maps = new[] { new CoreTreasureMapSave { id = "xp-test", seed = "XP-STARVATION-" + rank, rankId = rank } },
                selectedMapId = "xp-test",
            });
            return session;
        }

        private static void SailTwentyMiles(CoreVoyageSession session)
        {
            session.SetHeading(SailingDirection.East); session.SetDistance(20);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(20f).Within(.001f));
            Assert.That(session.CompletedEventCount(), Is.Zero);
        }

        // Eligibility fixtures resolve an actual authored choice; navigation is covered by the paid loop above.
        private static void Enter(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter); Set(session, "Phase", CoreDemoPhase.Encounter);
        }
        private static void Set(CoreVoyageSession session, string property, object value) =>
            typeof(CoreVoyageSession).GetProperty(property).SetValue(session, value);
    }
}
