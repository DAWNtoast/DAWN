using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardSchoolUnlockTests
    {
        [TestCase(1, 0)] [TestCase(2, 100)] [TestCase(3, 260)] [TestCase(4, 520)]
        [TestCase(5, 900)] [TestCase(6, 1400)] [TestCase(7, 2100)]
        [TestCase(8, 3000)] [TestCase(9, 4100)] [TestCase(10, 5400)]
        public void PirateLevels_PreserveOldThresholdsAndExtendToTen(int level, int experience)
        {
            var session = Profile(experience);
            Assert.That(CoreVoyageSession.PirateExperienceForLevel(level), Is.EqualTo(experience));
            Assert.That(session.PirateLevel, Is.EqualTo(level));
            Assert.That(session.MaximumPirateLevel, Is.EqualTo(10));
            Assert.That(Profile(Math.Max(0, experience - 1)).PirateLevel, Is.EqualTo(Math.Max(1, level - 1)));
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(restored.PirateExperience, Is.EqualTo(experience));
            Assert.That(restored.PirateLevel, Is.EqualTo(level));
        }

        [Test]
        public void PirateExperienceAboveFinalThreshold_IsPreservedWithNoNextLevel()
        {
            var session = Profile(9000);
            Assert.That(session.PirateLevel, Is.EqualTo(10));
            Assert.That(session.PirateExperience, Is.EqualTo(9000));
            Assert.That(session.NextPirateLevelExperience, Is.Zero);
            Assert.That(session.PirateExperienceToNextLevel, Is.Zero);
        }

        [TestCase("fire", 2)] [TestCase("arcane", 3)] [TestCase("curse", 4)]
        [TestCase("tide", 5)] [TestCase("thunder", 7)] [TestCase("blood", 10)]
        public void EachSchool_UnlocksExactlyTenBasicCardsAtItsLevel(string schoolId, int level)
        {
            CoreBattleCard[] basics = CoreVoyageSession.CardLibrary.Where(card => card.SchoolId == schoolId &&
                card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0).ToArray();
            Assert.That(basics.Length, Is.EqualTo(10));
            var locked = Profile(CoreVoyageSession.PirateExperienceForLevel(level) - 1);
            var unlocked = Profile(CoreVoyageSession.PirateExperienceForLevel(level));
            foreach (CoreBattleCard card in basics)
            {
                Assert.That(locked.IsBattleCardUnlocked(card), Is.False, card.Id);
                Assert.That(locked.GetBattleCardUnlockReason(card), Does.Contain(level.ToString()), card.Id);
                Assert.That(unlocked.IsBattleCardUnlocked(card), Is.True, card.Id);
                int index = Index(card.Id);
                foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                {
                    Assert.That(locked.GetAvailableBattleCards(stage), Has.No.Member(index), card.Id);
                    Assert.That(unlocked.GetAvailableBattleCards(stage).Contains(index), Is.EqualTo(card.IsAllowedInStage(stage)), card.Id);
                }
            }
        }

        [Test]
        public void LockedBasicCard_CannotEnterDeckOrBePurchasedAsAnUpgrade()
        {
            var session = Profile(0);
            CoreBattleCard card = CoreVoyageSession.CardLibrary.First(value => value.SchoolId == "fire" &&
                value.RequiredCrew.Length == 0 && value.RequiredComponent.Length == 0);
            NavalBattleStage stage = new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding }
                .First(card.IsAllowedInStage);
            int index = Index(card.Id);
            var before = session.GetBattleDeck(stage).ToArray();
            var proposed = new List<int>(before);
            proposed[0] = index;
            Assert.That(session.SetBattleDeck(stage, proposed), Is.False);
            CollectionAssert.AreEqual(before, session.GetBattleDeck(stage));
            Assert.That(session.BattleCardUpgradeUnavailableReason(index), Does.Contain("等级"));
        }

        [TestCase(1, 3)] [TestCase(2, 5)]
        public void LockedRoutes_RejectDirectSelectionAndChartLoadingWithoutChangingSelection(int routeIndex, int level)
        {
            var locked = Profile(CoreVoyageSession.PirateExperienceForLevel(level) - 1);
            string seed = CoreVoyageSession.CreateChartSeed(routeIndex, locked.CurrentPort,
                CoreVoyageSession.DestinationPorts.First(port => port != locked.CurrentPort));
            string previous = locked.SelectedSeed;
            decimal gold = locked.ProfileGold;
            Assert.That(locked.CanSelectRoute(routeIndex, out string reason), Is.False);
            Assert.That(reason, Does.Contain(level.ToString()));
            locked.SelectRoute(routeIndex);
            Assert.That(locked.SelectedRouteIndex, Is.Zero);
            Assert.That(locked.TrySelectChartSeed(seed, out reason), Is.False);
            Assert.That(reason, Does.Contain("等级"));
            Assert.That(locked.SelectedSeed, Is.EqualTo(previous));
            Assert.That(locked.ProfileGold, Is.EqualTo(gold));
            locked.SetSeed(seed);
            Assert.Throws<InvalidOperationException>(() => locked.StartVoyage());
            Assert.That(locked.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(locked.ProfileGold, Is.EqualTo(gold));

            var unlocked = Profile(CoreVoyageSession.PirateExperienceForLevel(level));
            Assert.That(unlocked.TrySelectChartSeed(seed, out reason), Is.True, reason);
            Assert.That(unlocked.SelectedRouteIndex, Is.EqualTo(routeIndex));
            Assert.That(unlocked.CanStartOrdinaryVoyage(out reason), Is.True, reason);
        }

        [Test]
        public void SourceCatalog_UsesOnePrimaryCardPerIdentityAndKeepsHistoricalLibrarySlots()
        {
            Assert.That(CoreSpecialCardCatalog.All.Count, Is.EqualTo(126));
            Assert.That(CoreSpecialCardCatalog.All.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Crew), Is.EqualTo(36));
            Assert.That(CoreSpecialCardCatalog.All.Count(value => value.SourceKind == CoreSpecialCardSourceKind.Component), Is.EqualTo(90));
            Assert.That(CoreSpecialCardCatalog.All.Select(value => value.SourceId).Distinct().Count(), Is.EqualTo(126));
            Assert.That(CoreSpecialCardCatalog.All.Count(value => value.Card.SchoolId.Length > 0), Is.EqualTo(72));
            Assert.That(CoreVoyageSession.CardLibrary.Count, Is.EqualTo(221));
            Assert.That(CoreVoyageSession.CardLibrary[138].Id, Is.EqualTo("card_special_crew_gray_gull_cook_pepe"));
            Assert.That(CoreVoyageSession.CardLibrary[139].Id, Is.EqualTo("card_special_crew_broken_anchor_cook_tam"));
            Assert.That(CoreVoyageSession.CardLibrary.Take(139).Any(card => card.Id.StartsWith("card_school_", StringComparison.Ordinal)), Is.False);
            Assert.That(CoreVoyageSession.CardLibrary.Skip(159).Take(60).Count(card => card.SchoolId.Length > 0), Is.EqualTo(60));
            Assert.That(CoreVoyageSession.CardLibrary.Skip(219).Select(card => card.Id),
                Is.EqualTo(new[] { "card_energy_order", "card_tactical_draw" }));
            var session = Profile(0);
            foreach (CoreSpecialCardDefinition source in CoreSpecialCardCatalog.All)
            {
                Assert.That(source.Card.Id, Is.EqualTo("card_special_" + source.SourceId));
                Assert.That(session.IsBattleCardUnlocked(source.Card), Is.True, "Source cards depend on recruitment/equipment, not pirate level.");
            }
        }

        [Test]
        public void ReplacingSchoolEffects_PreservesStableIdentityExperienceAndActiveSourceRules()
        {
            const string sourceId = "crew_twilight_tide_gunner_yilan";
            string id = "card_special_" + sourceId;
            var session = Profile(0);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { [id] = 360 });
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(360));
            Assert.That(CoreSpecialCardCatalog.ForCrew(sourceId).Card.SchoolId, Is.EqualTo("fire"));
            session.ConfigureSpecialCardSources(new[] { sourceId }, Array.Empty<string>());
            Assert.That(session.AcquiredSpecialCardIds, Does.Contain(id));
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(360));
            Assert.That(new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding }
                .SelectMany(session.GetBonusBattleCards).Contains(Index(id)), Is.True);
            session.ConfigureSpecialCardSources(Array.Empty<string>(), Array.Empty<string>());
            Assert.That(new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding }
                .SelectMany(session.GetBonusBattleCards).Contains(Index(id)), Is.False);
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(360));
        }

        private static CoreVoyageSession Profile(int experience)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = experience });
            return session;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .First(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
