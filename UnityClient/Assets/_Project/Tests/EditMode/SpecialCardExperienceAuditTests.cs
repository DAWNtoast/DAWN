using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class SpecialCardExperienceAuditTests
    {
        private const string FirstCrew = "crew_broken_anchor_navigator_lorne";
        private const string OtherCrew = "crew_white_shell_navigator_saina";
        private const string ThreeRankCrew = "crew_gray_gull_navigator_evelyn";

        [Test]
        public void EmptySuppliesCannotFarmExperienceThroughRepeatedShortFailedReturns()
        {
            var session = new CoreVoyageSession();
            CoreSpecialCardDefinition card = CoreSpecialCardCatalog.ForCrew(FirstCrew);
            session.ConfigureSpecialCardSources(new[] { FirstCrew }, null);
            session.SetSeed("SPECIAL-XP-SHORT-RETURN-AUDIT");
            session.Cargo.Water = 0;
            session.Cargo.Food = 0;
            session.Cargo.Gold = 0;
            decimal gold = session.ProfileGold;
            for (int trip = 0; trip < 6; trip++)
            {
                session.StartVoyage();
                PrepareLegalSailing(session, CoreVoyageSession.MinimumDistanceNm);
                Assert.That(session.GetForecast().WaterCost, Is.GreaterThan(0), "Even the first short leg consumes provisions.");
                Assert.That(session.Sail(), Is.False, "Zero-provision departure must not become a free training voyage.");
                Assert.That(session.TotalDistanceNm, Is.Zero);
                Assert.That(session.GetSpecialCardExperience(card.Card.Id), Is.Zero);
                Assert.That(session.SafeReturn(), Is.True);
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                session.ReturnToPortAfterSettlement();
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.Cargo.Food, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
            }
        }

        [Test]
        public void ActualPaidSailingKeepsEarnedExperienceAfterFailureWithoutASecondSettlementAward()
        {
            var session = new CoreVoyageSession();
            CoreSpecialCardDefinition card = CoreSpecialCardCatalog.ForCrew(FirstCrew);
            session.ConfigureSpecialCardSources(new[] { FirstCrew }, null);
            session.SetSeed("SPECIAL-XP-PAID-FAILURE-AUDIT");
            int startingWater = session.Cargo.Water;
            int startingFood = session.Cargo.Food;
            session.StartVoyage();
            PrepareLegalSailing(session, 70);
            Assert.That(session.Sail(), Is.True);
            int earned = session.GetSpecialCardExperience(card.Card.Id);
            Assert.That(earned, Is.GreaterThan(0));
            Assert.That(earned, Is.EqualTo((int)Math.Floor(session.TotalDistanceNm * CoreVoyageSession.SpecialCardExperiencePerNauticalMile)).Within(1));
            Assert.That(session.Cargo.Water, Is.LessThan(startingWater));
            Assert.That(session.Cargo.Food, Is.LessThan(startingFood));
            // Exhaustion is an explicit boundary fixture after paid sailing;
            // failure must neither erase learned distance nor multiply it.
            session.Cargo.Water = 0;
            session.Cargo.Food = 0;
            session.SafeReturn();
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.GetSpecialCardExperience(card.Card.Id), Is.EqualTo(earned));
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.GetSpecialCardExperience(card.Card.Id), Is.EqualTo(earned));
            var restored = new CoreVoyageSession();
            restored.RestoreSpecialCardExperience(session.SpecialCardExperience);
            restored.RestoreSpecialCardExperience(session.SpecialCardExperience);
            Assert.That(restored.GetSpecialCardExperience(card.Card.Id), Is.EqualTo(earned));
        }

        [Test]
        public void RepeatedSourcesAndMultipleBattleStagesAwardDistanceOnlyOncePerIdentity()
        {
            var session = new CoreVoyageSession();
            CoreSpecialCardDefinition crew = CoreSpecialCardCatalog.ForCrew(FirstCrew);
            CoreSpecialCardDefinition part = CoreSpecialCardCatalog.All.First(definition => definition.SourceKind == CoreSpecialCardSourceKind.Component);
            Assert.That(Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Count(stage => crew.Card.IsAllowedInStage(stage)), Is.GreaterThan(1));
            session.ConfigureSpecialCardSources(new[] { FirstCrew, FirstCrew, FirstCrew },
                new[] { part.SourceId, part.SourceId });
            session.SetSeed("SPECIAL-XP-DUPLICATE-SOURCE-AUDIT");
            session.StartVoyage();
            PrepareLegalSailing(session, 70);
            Assert.That(session.Sail(), Is.True);
            int earned = (int)Math.Floor(session.TotalDistanceNm * CoreVoyageSession.SpecialCardExperiencePerNauticalMile);
            Assert.That(session.GetSpecialCardExperience(crew.Card.Id), Is.EqualTo(earned).Within(1));
            Assert.That(session.GetSpecialCardExperience(part.Card.Id), Is.EqualTo(earned).Within(1));
            Assert.That(session.AcquiredSpecialCardIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void SwitchingTheDepartureSourceLeavesThePreviousCrewExperienceUnchanged()
        {
            var session = new CoreVoyageSession();
            CoreSpecialCardDefinition first = CoreSpecialCardCatalog.ForCrew(FirstCrew);
            CoreSpecialCardDefinition second = CoreSpecialCardCatalog.ForCrew(OtherCrew);
            session.ConfigureSpecialCardSources(new[] { FirstCrew }, null);
            session.SetSeed("SPECIAL-XP-SOURCE-SWITCH-AUDIT");
            session.StartVoyage();
            PrepareLegalSailing(session, 70);
            Assert.That(session.Sail(), Is.True);
            session.SafeReturn();
            session.ReturnToPortAfterSettlement();
            int firstEarned = session.GetSpecialCardExperience(first.Card.Id);
            session.ConfigureSpecialCardSources(new[] { OtherCrew }, null);
            session.StartVoyage();
            PrepareLegalSailing(session, 70);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.GetSpecialCardExperience(first.Card.Id), Is.EqualTo(firstEarned));
            Assert.That(session.GetSpecialCardExperience(second.Card.Id), Is.EqualTo((int)Math.Floor(session.TotalDistanceNm * CoreVoyageSession.SpecialCardExperiencePerNauticalMile)).Within(1));
            Assert.That(session.AcquiredSpecialCardIds, Is.EquivalentTo(new[] { first.Card.Id, second.Card.Id }));
        }

        [TestCase(480f, 120, 1)]
        [TestCase(1440f, 360, 2)]
        [TestCase(2000f, 500, 2)]
        [TestCase(3360f, 840, 3)]
        public void ActualSailingDistanceCanReachEveryRankWithinOneVoyage(float distance, int experience, int rank)
        {
            var session = new CoreVoyageSession();
            CoreSpecialCardDefinition card = CoreSpecialCardCatalog.ForCrew(ThreeRankCrew);
            session.ConfigureSpecialCardSources(new[] { ThreeRankCrew }, null);
            Assert.That(session.GetCardMaximumUpgradeLevel(card.Card.Id), Is.EqualTo(3));
            session.StartVoyage();

            Award(session, distance);

            Assert.That(session.GetSpecialCardExperience(card.Card.Id), Is.EqualTo(experience));
            Assert.That(session.GetCardUpgradeLevel(card.Card.Id), Is.EqualTo(rank),
                "Current progression has no separate voyage-count or card-use requirement.");
        }
        [TestCase(120, 1)]
        [TestCase(360, 2)]
        [TestCase(840, 3)]
        public void EveryAuthoredSpecialCardUsesItsEffectLimitsAndCannotSpendGold(int experience, int level)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(100000, session.CurrentPort);
            session.RestoreSpecialCardExperience(CoreSpecialCardCatalog.All.ToDictionary(definition => definition.Card.Id, definition => experience));
            decimal gold = session.ProfileGold;
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                CoreBattleCard original = CoreBoardingCardCatalog.ReplaceLegacyBoarding(definition.Card);
                int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .First(value => CoreVoyageSession.CardLibrary[value].Id == original.Id);
                CoreBattleCard grown = session.GetBattleCard(index);
                int maximum = session.GetCardMaximumUpgradeLevel(index);
                CoreCardUpgradeProfile profile = CoreCardUpgradeDesign.Find(original);
                Assert.That(profile, Is.Not.Null, original.Id);
                Assert.That(maximum, Is.EqualTo(profile.MaximumLevel), original.Id);
                int effectiveLevel = Math.Min(level, maximum);
                Assert.That(grown.UpgradeLevel, Is.EqualTo(effectiveLevel), original.Id);
                Assert.That(session.GetSpecialCardExperience(original.Id), Is.EqualTo(experience), "Effect caps never erase earned source experience.");
                int nextThreshold = effectiveLevel >= maximum ? 0 : effectiveLevel == 0 ? 120 : effectiveLevel == 1 ? 360 : 840;
                Assert.That(session.GetSpecialCardNextThreshold(original.Id), Is.EqualTo(nextThreshold), original.Id);
                Assert.That(grown.Damage, Is.EqualTo((int)Math.Round(original.Damage * profile.Multiplier(effectiveLevel), 0, MidpointRounding.AwayFromZero)), original.Id);
                Assert.That(grown.Block, Is.EqualTo((int)Math.Round(original.Block * profile.BlockMultiplier(effectiveLevel), 0, MidpointRounding.AwayFromZero)), original.Id);
                Assert.That(grown.Repair, Is.EqualTo((int)Math.Round(original.Repair * profile.RepairMultiplier(effectiveLevel), 0, MidpointRounding.AwayFromZero)), original.Id);
                Assert.That(grown.Energy, Is.EqualTo(original.Energy), original.Id);
                int drawBonus = profile.DrawBonus(effectiveLevel);
                Assert.That(grown.Draw, Is.EqualTo(original.Draw + (original.Draw > 0 ? drawBonus : 0)), original.Id);
                Assert.That(session.GetCardUpgradeCost(index), Is.Zero, original.Id);
                Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False, original.Id);
            }
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [TestCase(119, 1)]
        [TestCase(359, 2)]
        [TestCase(839, 3)]
        public void RealThresholdCrossingUpdatesTheCardImmediatelyAndLogsTheNewRankOnce(int experience, int level)
        {
            var session = new CoreVoyageSession();
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(ThreeRankCrew);
            session.ConfigureSpecialCardSources(new[] { ThreeRankCrew }, null);
            Assert.That(session.GetCardMaximumUpgradeLevel(definition.Card.Id), Is.EqualTo(3));
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { definition.Card.Id, experience } });
            session.SetSeed("SPECIAL-XP-THRESHOLD-FEEDBACK-AUDIT");
            session.StartVoyage();
            PrepareLegalSailing(session, CoreVoyageSession.MinimumDistanceNm);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.GetCardUpgradeLevel(definition.Card.Id), Is.EqualTo(level));
            PrepareLegalSailing(session, CoreVoyageSession.MinimumDistanceNm);
            Assert.That(session.Sail(), Is.True);
            string[] growthMessages = session.LogEntries.Where(message => message.Contains("航行历练") &&
                message.Contains(definition.Card.Name)).ToArray();
            Assert.That(growthMessages.Length, Is.EqualTo(1), "Continuing at the same rank must not repeat the level-up reward or notification.");
            Assert.That(growthMessages[0], Does.Contain("达到 " + level + " 阶"));
            int cardIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .First(index => CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id);
            Assert.That(session.GetBattleCard(cardIndex).UpgradeLevel, Is.EqualTo(level));
        }

        private static void PrepareLegalSailing(CoreVoyageSession session, int requestedDistance)
        {
            // Remove encounter randomness from this XP/resource audit while
            // keeping the actual path, forecast, movement, and consumption.
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, true);
            session.SetDistance(requestedDistance);
            foreach (SailingDirection heading in Enum.GetValues(typeof(SailingDirection)))
            {
                session.SetHeading(heading);
                SailingForecast forecast = session.GetForecast();
                if (forecast != null && !forecast.Blocked && forecast.ActualDistanceNm >= 1f) return;
            }
            Assert.Fail("Expected a legal sailing direction from the seeded departure point.");
        }

        private static void Award(CoreVoyageSession session, float distance)
        {
            typeof(CoreVoyageSession)
                .GetMethod("AwardSpecialCardVoyageDistance", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, new object[] { distance });
        }
    }
}
