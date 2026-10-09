using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreGuidedTutorialVoyageTests
    {
        private static CoreVoyageSession Start(int leg, bool navigator, int? water = null)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
            if (navigator) session.ConfigureSpecialCardSources(new[] { "crew_broken_anchor_navigator_lorne" }, Array.Empty<string>());
            Assert.That(session.ConfigureGuidedTutorialVoyage(leg), Is.True);
            if (water.HasValue) session.Cargo.Water = water.Value;
            session.StartVoyage();
            return session;
        }

        private static void SailTo(CoreVoyageSession session, string id)
        {
            session.SetHeading(SailingDirection.East);
            session.SetDistance(CoreGuidedTutorialContent.PlannedDistanceNm);
            for (int attempt = 0; attempt < 3 && session.Phase == CoreDemoPhase.Sailing; attempt++)
                Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(id));
        }

        [Test]
        public void Configuration_IsPortOnlyAndDoesNotChangeNormalCharts()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.ConfigureGuidedTutorialVoyage(-1), Is.False);
            Assert.That(session.ConfigureGuidedTutorialVoyage(4), Is.False);
            Assert.That(session.ConfigureGuidedTutorialVoyage(1), Is.True);
            Assert.That(session.ConfigureGuidedTutorialVoyage(0), Is.True);
            session.SetSeed("NORMAL-AFTER-GUIDED");
            session.StartVoyage();
            Assert.That(session.Map.Seed, Is.EqualTo("NORMAL-AFTER-GUIDED"));
            Assert.That(session.Map.Route.NauticalMilesPerMapUnit, Is.EqualTo(27f));
            Assert.That(session.Map.Nodes.Count, Is.GreaterThan(3));
            Assert.That(session.ConfigureGuidedTutorialVoyage(2), Is.False);
        }

        [Test]
        public void ThreeTrips_ShareDetailedGeography_AndClearEastboundCourse()
        {
            CoreVoyageMap first = Start(1, false).Map;
            for (int leg = 1; leg <= 3; leg++)
            {
                CoreVoyageSession session = Start(leg, leg > 1);
                Assert.That(session.Map.TerrainCells, Is.EqualTo(first.TerrainCells));
                Assert.That(session.Map.TerrainVariants, Is.EqualTo(first.TerrainVariants));
                Assert.That(session.Map.TerrainCells.Distinct().Count(), Is.GreaterThan(2));
                for (float x = 8f; x <= 55f; x += 0.5f)
                    Assert.That(session.Map.IsPassable(session.Map.PointToCell(new VoyagePoint(x, 33f)).Column,
                        session.Map.PointToCell(new VoyagePoint(x, 33f)).Row), Is.True);
                Assert.That(session.Map.EventFootprints.All(footprint => session.Map.Nodes.Any(node => node.Id == footprint.NodeId)), Is.True);
            }
        }

        [Test]
        public void FirstTrip_UsesRealMandatorySkipCrewGateReturnAndSettlement()
        {
            CoreVoyageSession session = Start(1, false);
            int food = session.Cargo.Food;
            SailTo(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.SkipEncounter(), Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.EqualTo(1));
            SailTo(session, CoreGuidedTutorialContent.OptionalEventId);
            Assert.That(session.SkipEncounter(), Is.True);
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.EqualTo(2));
            SailTo(session, CoreGuidedTutorialContent.CrewRequirementEventId);
            Assert.That(session.ActiveEncounter.HasChartLocation, Is.False);
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.EqualTo(3));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(360f).Within(0.05f));
            Assert.That(session.Cargo.Food, Is.LessThan(food));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.ChartSaved, Is.False);
            decimal gold = session.ProfileGold;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.ProfileGold, Is.GreaterThanOrEqualTo(gold));
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.EqualTo(3));
            decimal banked = session.ProfileGold;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(banked));
        }

        [Test]
        public void SecondTrip_ContinuesAfterNavigatorAndAvoidsAmbushWithoutReturning()
        {
            CoreVoyageSession session = Start(2, true);
            Assert.That(session.Map.Nodes.Count, Is.EqualTo(2));
            Assert.That(session.Map.Nodes.Any(node => node.Encounter.Id == CoreGuidedTutorialContent.RandomEventId ||
                node.Encounter.Id == CoreGuidedTutorialContent.AmbushEventId), Is.False);
            SailTo(session, CoreGuidedTutorialContent.NavigatorEventId);
            Assert.That(session.IsChoiceAvailable(0), Is.True);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            SailTo(session, CoreGuidedTutorialContent.AmbushEventId);
            Assert.That(session.ActiveEncounter.Choices[0].StartsBattle, Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
            Assert.That(session.GuidedTutorialRandomEventTriggered, Is.False);
            Assert.That(session.GuidedTutorialAmbushTriggered, Is.True);
            Assert.That(session.LastBattleRetreated, Is.False);
            SailTo(session, CoreGuidedTutorialContent.BossEventId);
        }

        [TestCase(false, 12, VoyageOutcome.SafeReturn)]
        [TestCase(true, 0, VoyageOutcome.Failure)]
        public void NavigatorFallback_DoesNotCompleteTheLessonOrArriveAtAnotherPort(bool navigator, int rations, VoyageOutcome outcome)
        {
            CoreVoyageSession session = Start(CoreGuidedTutorialContent.NavigatorVoyageLeg, navigator);
            SailTo(session, CoreGuidedTutorialContent.NavigatorEventId);
            session.Cargo.Water = rations;
            session.Cargo.Food = rations;
            string departure = session.CurrentPort;
            int gold = session.Cargo.Gold;
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.GuidedTutorialCompletedEncounterIds, Does.Not.Contain(CoreGuidedTutorialContent.NavigatorEventId),
                "Returning without interpreting the tide marks must not unlock the boss voyage.");
            Assert.That(session.Map.Nodes.Single(node => node.Encounter.Id == CoreGuidedTutorialContent.NavigatorEventId).Visited, Is.False);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(outcome));
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold), "The fallback awards no interpretation reward.");
            Assert.That(session.Cargo.Water, Is.EqualTo(Math.Max(0, rations - (outcome == VoyageOutcome.SafeReturn ? 2 : 0))));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CurrentPort, Is.EqualTo(departure));
        }

        [Test]
        public void BossTemplate_FixedLessonAndOneNavigator_CanWinAndChart()
        {
            CoreVoyageSession session = Start(CoreGuidedTutorialContent.BossVoyageLeg, true);
            SailTo(session, CoreGuidedTutorialContent.BossEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Battle.Profile, Is.SameAs(CoreGuidedTutorialContent.BossProfile));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(CoreGuidedBossScriptTests.PlayToCompletion(session), Is.EqualTo(21));
            Assert.That(session.BossDefeated, Is.True);
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.EqualTo(1));
            Assert.That(session.Map.Nodes, Is.Empty, "The defeated chart boss disappears immediately.");
            Assert.That(session.CompletedEventCount(), Is.EqualTo(1));
            int earned = session.Cargo.Gold;
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(earned));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.ChartSaved, Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            Assert.That(session.ConfigureGuidedTutorialVoyage(0), Is.True);
            session.StartVoyage();
            Assert.That(session.GuidedTutorialBossDefeated, Is.False);
        }

        [Test]
        public void Retreat_IsUnavailableAfterBoardingOrWithInsufficientReturnRations()
        {
            CoreVoyageSession session = Start(3, true);
            SailTo(session, CoreGuidedTutorialContent.BossEventId);
            session.ResolveChoice(0);
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(session.CanRetreatBattle, Is.False);
            Assert.That(session.RetreatFromBattle(), Is.False);
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Cargo.Water = 0;
            Assert.That(session.CanRetreatBattle, Is.False);
            Assert.That(session.RetreatFromBattle(), Is.False);
        }

        [Test]
        public void MandatoryEvent_ExhaustedArrivalCanTowWithoutCompletingLesson()
        {
            CoreVoyageSession session = Start(1, false, water: 4);
            SailTo(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.Cargo.Water, Is.Zero);
            // The smaller crew may still have prepaid fractional rations after
            // the last whole unit is debited. Exhaust that real coverage first.
            typeof(CoreVoyageSession).GetMethod("ConsumeHours", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { session.WaterSupplyHours });
            Assert.That(session.WaterSupplyHours, Is.EqualTo(0d).Within(1e-8d));
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.CanRequestEmergencyTow(), Is.True);
            Assert.That(session.RequestEmergencyTow(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.ProtectionRatio, Is.EqualTo(.35f));
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.Zero);
            Assert.That(session.LastBattleRetreated, Is.False);
            Assert.That(session.Cargo.Water, Is.Zero);
            Assert.That(session.RequestEmergencyTow(), Is.False);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
        }

        [Test]
        public void NavigatorChoiceUsesOnlyTheUnpaidPartOfItsCumulativeRations()
        {
            CoreVoyageSession session = Start(2, true, water: 4);
            SailTo(session, CoreGuidedTutorialContent.NavigatorEventId);
            int charged = (int)Math.Ceiling(session.TotalHours * session.CrewCountValue / 24d - 1e-9d);
            int extra = (int)Math.Ceiling((session.TotalHours + 1d) * session.CrewCountValue / 24d - 1e-9d) - charged;
            Console.WriteLine("Navigator low-supply boundary: hours=" + session.TotalHours.ToString("F6") +
                "; charged=" + charged + "; water=" + session.Cargo.Water + "; event extra=" + extra + ".");
            Assert.That(session.Cargo.Water, Is.EqualTo(4 - charged));
            Assert.That(session.IsChoiceAvailable(0), Is.EqualTo(session.Cargo.Water >= extra));
            Assert.That(session.IsChoiceAvailable(1), Is.True, "Zero-time return remains a valid Core fallback.");
            Assert.That(session.CanRequestEmergencyTow(), Is.False);
            int gold = session.Cargo.Gold;
            bool canInterpret = session.IsChoiceAvailable(0);
            Assert.That(session.ResolveChoice(canInterpret ? 0 : 1), Is.True);
            if (canInterpret)
            {
                Assert.That(session.Cargo.Gold, Is.EqualTo(gold + 24));
                Assert.That(session.Settlement, Is.Null);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            }
            else
            {
                Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
                Assert.That(session.Settlement.BankedGold, Is.Zero);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            }
            Assert.That(session.Cargo.Water, Is.Zero);
            Assert.That(session.GuidedTutorialCompletedEncounterIds.Contains(CoreGuidedTutorialContent.NavigatorEventId), Is.EqualTo(canInterpret));
        }

        [Test]
        public void BattleTow_IsUnavailableWithAffordableRetreatOrAfterBoarding()
        {
            CoreVoyageSession session = Start(3, true);
            SailTo(session, CoreGuidedTutorialContent.BossEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.CanRetreatBattle, Is.False, "The authored lesson does not permit ordinary battle retreat.");
            Assert.That(session.CanRequestEmergencyTow(), Is.False);
            Assert.That(session.RequestEmergencyTow(), Is.False);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Cargo.Water = 0;
            Assert.That(session.CanRetreatBattle, Is.False);
            Assert.That(session.CanRequestEmergencyTow(), Is.False);
            Assert.That(session.RequestEmergencyTow(), Is.False);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
        }

        [Test]
        public void AllGuidedVoyages_StartingSuppliesAndPaid105GoldRecruit_AreSufficient()
        {
            CoreVoyageSession session = Start(1, false);
            SailTo(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            SailTo(session, CoreGuidedTutorialContent.OptionalEventId);
            Assert.That(session.SkipEncounter(), Is.True);
            SailTo(session, CoreGuidedTutorialContent.CrewRequirementEventId);
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TrySpendProfileGold(105, "招募测潮师 洛恩"), Is.True);
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.ConfigureSpecialCardSources(new[] { "crew_broken_anchor_navigator_lorne" }, Array.Empty<string>());
            Assert.That(session.ConfigureGuidedTutorialVoyage(2), Is.True);
            session.StartVoyage();
            SailTo(session, CoreGuidedTutorialContent.NavigatorEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            SailTo(session, CoreGuidedTutorialContent.AmbushEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            SailTo(session, CoreGuidedTutorialContent.BossEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            CoreGuidedBossScriptTests.PlayToCompletion(session);
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.ChartSaved, Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Cargo.Water, Is.GreaterThan(0));
            Assert.That(session.Cargo.Food, Is.GreaterThan(0));
            Assert.That(session.ProfileGold, Is.GreaterThan(0));
            Console.WriteLine("All guided trips without water/food refills: gold=" + session.ProfileGold +
                "; water=" + session.Cargo.Water + "; food=" + session.Cargo.Food +
                "; powder=" + session.Cargo.Powder + "; cannonballs=" + session.Cargo.Cannonballs + ".");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyBossEntry_PaidNavigatorAndSavedDecks_CanWinWithoutVoyageRationRefills(bool salvageOptional)
        {
            CoreVoyageSession session = Start(1, false);
            int water = session.Cargo.Water, food = session.Cargo.Food;
            SailTo(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            SailTo(session, CoreGuidedTutorialContent.OptionalEventId);
            Assert.That(salvageOptional ? session.ResolveChoice(0) : session.SkipEncounter(), Is.True);
            SailTo(session, CoreGuidedTutorialContent.CrewRequirementEventId);
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();

            decimal goldBeforeRecruit = session.ProfileGold;
            Assert.That(session.TrySpendProfileGold(105, "招募测潮师 洛恩"), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(goldBeforeRecruit - 105));
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.ConfigureSpecialCardSources(new[] { "crew_broken_anchor_navigator_lorne" }, Array.Empty<string>());
            Assert.That(session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg), Is.True);
            session.StartVoyage();
            Assert.That(session.Map.Nodes.Single().Encounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
            SailTo(session, CoreGuidedTutorialContent.BossEventId);
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.Zero,
                "The second voyage reaches the boss directly, without an ambush or deck-editing lesson.");
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.CardUpgradeLevels, Is.Empty, "No free card upgrades are needed for the boss.");
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Preparation), Is.Not.Empty,
                "The normally recruited navigator still contributes their special cards.");

            int actions = CoreGuidedBossScriptTests.PlayToCompletion(session);
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            Assert.That(session.Hull, Is.GreaterThan(0));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.ChartSaved, Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Cargo.Water, Is.InRange(1, water - 1));
            Assert.That(session.Cargo.Food, Is.InRange(1, food - 1));
            Assert.That(session.ProfileGold, Is.GreaterThan(0));
            Assert.That(session.CardUpgradeLevels, Is.Empty);
            Console.WriteLine("Two real voyages / optional salvage=" + salvageOptional + ": boss actions=" + actions +
                "; hull=" + session.Hull + "; gold=" + session.ProfileGold + "; water=" + session.Cargo.Water +
                "; food=" + session.Cargo.Food + "; powder=" + session.Cargo.Powder + "; cannonballs=" + session.Cargo.Cannonballs + ".");
        }

        [Test]
        public void PreviouslyEditedDecks_TutorialUsesDefaultsAndPreservesCustomDecksForNormalBattles()
        {
            foreach (int sourceExperience in new[] { 0, 360 })
            {
                var session = new CoreVoyageSession();
                session.ConfigureCrewCapabilities(true, false, false, false, false, false);
                session.ConfigureSpecialCardSources(new[] { "crew_broken_anchor_navigator_lorne" }, Array.Empty<string>());
                session.RestoreSpecialCardExperience(session.AcquiredSpecialCardIds.ToDictionary(id => id, id => sourceExperience));
                var defaults = new CoreVoyageSession();
                var customDecks = new Dictionary<NavalBattleStage, int[]>();
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                {
                    customDecks[stage] = session.GetBattleDeck(stage).Skip(1).ToArray();
                    Assert.That(session.SetBattleDeck(stage, customDecks[stage]), Is.True);
                }
                session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
                session.StartVoyage();
                SailTo(session, CoreGuidedTutorialContent.BossEventId);
                Assert.That(session.ResolveChoice(0), Is.True);
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                {
                    Assert.That(session.GetBattleDeck(stage), Is.EqualTo(customDecks[stage]),
                        "Using teaching decks must not overwrite an older save's customized loadout.");

                }
                CollectionAssert.AreEqual(new[] { "card_retreat", "card_load_shot", "card_brace" },
                    session.Battle.Hand.Select(index => session.GetBattleCard(index).Id));
                int actions = CoreGuidedBossScriptTests.PlayToCompletion(session);
                Assert.That(session.GuidedTutorialBossDefeated, Is.True);
                Console.WriteLine("Fixed teaching hand with preserved custom decks / source XP " + sourceExperience + ": " + actions + " actions; hull " + session.Hull + ".");
                Assert.That(session.SafeReturn(), Is.True);
                session.ReturnToPortAfterSettlement();
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                    Assert.That(session.GetBattleDeck(stage), Is.EqualTo(customDecks[stage]));

                Assert.That(session.ConfigureGuidedTutorialVoyage(0), Is.True);
                session.StartVoyage();
                var normalEncounter = new CoreEncounter("custom-deck-regression", "普通拦路船", "海战", "检查正常牌组", true, false, 1, 40);
                typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { normalEncounter, false });
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                    AssertFreshBattlePile(session, stage, customDecks[stage]);
            }
        }

        private static void AssertFreshBattlePile(CoreVoyageSession session, NavalBattleStage stage, IReadOnlyList<int> expectedBaseDeck)
        {
            var pile = (IEnumerable<int>)typeof(CoreBattleState).GetMethod("DrawPileFor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { stage });
            IEnumerable<int> cards = session.Battle.Stage == stage ? pile.Concat(session.Battle.Hand) : pile;
            CollectionAssert.AreEquivalent(expectedBaseDeck.Concat(session.GetBonusBattleCards(stage)), cards,
                "Fresh " + stage + " pile must contain its expected base deck and normal carried-source bonus cards.");
        }
    }
}
