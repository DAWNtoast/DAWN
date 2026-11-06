using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardPackOwnershipMigrationTests
    {
        private static readonly NavalBattleStage[] Stages =
            { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };

        [Test]
        public void NewProfileNeverReceivesSchoolCardsAtAnyPirateLevel()
        {
            var session = new CoreVoyageSession();
            for (int level = 1; level <= session.MaximumPirateLevel; level++)
            {
                session.RestoreTreasureProgress(new CoreTreasureProgress
                    { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(level) });
                foreach (int index in session.GetCardPackPool("neutral"))
                {
                    CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                    Assert.That(session.GetBaseBattleCardOwnedCopies(index), Is.EqualTo(card.SchoolId.Length == 0 ? 2 : 0),
                        level + ": " + card.Id);
                    Assert.That(session.IsBattleCardUnlocked(card), Is.EqualTo(card.SchoolId.Length == 0), card.Id);
                }
                Assert.That(session.CaptureCardPackProgress().cards, Is.Empty);
            }
            foreach (CoreSpecialCardDefinition source in CoreSpecialCardCatalog.All)
                Assert.That(session.IsBattleCardUnlocked(source.Card), Is.True,
                    "Recruitment and equipment source progression is independent of pack ownership.");
        }

        [Test]
        public void VersionOneMigrationAddsOnlyPreviouslyGrantedCopiesAndVersionTwoNeverAddsThemAgain()
        {
            var session = new CoreVoyageSession();
            int fire = Card("fire"), blood = Card("blood"), neutral = Card(string.Empty);
            var old = new CoreCardPackProgress
            {
                version = 1,
                packs = new[] { new CoreCardPackCountSave { id = "fire", count = 4 } },
                cards = new[]
                {
                    Copy(fire, 1), Copy(blood, 3), Copy(neutral, 1),
                },
                randomState = 87654321u,
            };
            string input = JsonUtility.ToJson(old);
            // The caller's saved level, not the now higher in-memory level, is authoritative.
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(old, 3), Is.True);
            Assert.That(session.GetBaseBattleCardOwnedCopies(fire), Is.EqualTo(3));
            Assert.That(session.GetBaseBattleCardOwnedCopies(blood), Is.EqualTo(3));
            Assert.That(session.GetBaseBattleCardOwnedCopies(neutral), Is.EqualTo(3));
            Assert.That(session.GetBaseBattleCardOwnedCopies(Card("arcane")), Is.EqualTo(2));
            foreach (string school in new[] { "curse", "tide", "thunder" })
                Assert.That(session.GetBaseBattleCardOwnedCopies(Card(school)), Is.Zero, school);
            Assert.That(session.GetCardPackCount("fire"), Is.EqualTo(4));
            Assert.That(session.CaptureCardPackProgress().randomState, Is.EqualTo(old.randomState));
            Assert.That(JsonUtility.ToJson(old), Is.EqualTo(input), "Migration must not mutate its source DTO.");

            CoreCardPackProgress migrated = session.CaptureCardPackProgress();
            Assert.That(migrated.version, Is.EqualTo(2));
            string expected = JsonUtility.ToJson(migrated);
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(restored.RestoreCardPackProgress(migrated, 10), Is.True);
            Assert.That(JsonUtility.ToJson(restored.CaptureCardPackProgress()), Is.EqualTo(expected));
            Assert.That(restored.RestoreCardPackProgress(restored.CaptureCardPackProgress(), 10), Is.True);
            Assert.That(JsonUtility.ToJson(restored.CaptureCardPackProgress()), Is.EqualTo(expected));
            Assert.That(restored.GetBaseBattleCardOwnedCopies(Card("tide")), Is.Zero);
        }

        [Test]
        public void MissingOldPackRecordNeedsExplicitLegacyContextAndKeepsHistoricalBaseInventory()
        {
            var fresh = new CoreVoyageSession();
            fresh.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(fresh.RestoreCardPackProgress(null), Is.True);
            Assert.That(fresh.GetBaseBattleCardOwnedCopies(Card("fire")), Is.Zero);
            Assert.That(fresh.RestoreCardPackProgress(null, 4), Is.True);
            for (int i = 0; i < CoreVoyageSession.CardLibrary.Count; i++)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[i];
                if (i != CoreVoyageSession.CanonicalBattleCardIndex(i) || card.AllowedStages == NavalBattleStages.None ||
                    card.RequiredCrew.Length > 0 || card.RequiredComponent.Length > 0) continue;
                CoreSchoolUnlockDefinition school = CoreSchoolUnlockCatalog.Find(card.SchoolId);
                int expected = card.SchoolId.Length == 0 || school != null && school.RequiredLevel <= 4 ? 2 : 0;
                Assert.That(fresh.GetBaseBattleCardOwnedCopies(i), Is.EqualTo(expected), card.Id);
            }
            Assert.That(fresh.CaptureCardPackProgress().version, Is.EqualTo(2));
            Assert.That(fresh.CaptureCardPackProgress().packs, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldVoyageWithMissingOrVersionOneRecordKeepsSchoolDecksIncludingLegacySlots(bool hasV1)
        {
            var source = new CoreVoyageSession();
            source.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(4) });
            Assert.That(source.RestoreCardPackProgress(null, 4), Is.True);
            foreach (NavalBattleStage stage in Stages)
            {
                int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(index =>
                {
                    CoreBattleCard value = CoreVoyageSession.CardLibrary[index];
                    return value.SchoolId == "fire" && value.IsAllowedInStage(stage) &&
                        source.GetBaseBattleCardOwnedCopies(index) == 2 &&
                        (stage != NavalBattleStage.Boarding || CoreBoardingCardCatalog.IsLegacyAlias(value));
                });
                List<int> deck = source.GetBattleDeck(stage).ToList();
                deck[0] = card;
                Assert.That(source.SetBattleDeck(stage, deck), Is.True, stage.ToString());
            }
            int[][] expected = Stages.Select(stage => source.GetBattleDeck(stage).ToArray()).ToArray();
            source.StartVoyage();
            CoreVoyageCheckpoint old = source.CaptureVoyageCheckpoint();
            old.cardPackProgress = hasV1 ? new CoreCardPackProgress { version = 1, randomState = 1234u } : null;
            var resumed = new CoreVoyageSession();
            Assert.That(resumed.TryRestoreVoyageCheckpoint(old, out string reason), Is.True, reason);
            foreach (NavalBattleStage stage in Stages)
                CollectionAssert.AreEqual(expected[(int)stage], resumed.GetBattleDeck(stage), stage.ToString());
            Assert.That(resumed.GetBaseBattleCardOwnedCopies(Card("fire")), Is.EqualTo(2));
            Assert.That(resumed.GetBaseBattleCardOwnedCopies(Card("tide")), Is.Zero);
            CoreVoyageCheckpoint upgraded = resumed.CaptureVoyageCheckpoint();
            Assert.That(upgraded.cardPackProgress.version, Is.EqualTo(2));
            var again = new CoreVoyageSession();
            Assert.That(again.TryRestoreVoyageCheckpoint(upgraded, out reason), Is.True, reason);
            Assert.That(JsonUtility.ToJson(again.CaptureCardPackProgress()),
                Is.EqualTo(JsonUtility.ToJson(resumed.CaptureCardPackProgress())));
        }

        [Test]
        public void InvalidOrOverflowingMigrationLeavesInventoryAndRandomStateUnchanged()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.GrantCardPacks("tide", 3), Is.True);
            string before = JsonUtility.ToJson(session.CaptureCardPackProgress());
            var old = new CoreCardPackProgress { version = 1 };
            Assert.That(session.RestoreCardPackProgress(old), Is.False, "A v1 restore must provide the saved pirate level.");
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            old.cards = new[] { Copy(Card("fire"), int.MaxValue) };
            Assert.That(session.RestoreCardPackProgress(old, 2), Is.False);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            old.version = 3;
            Assert.That(session.RestoreCardPackProgress(old, 2), Is.False);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
        }

        private static int Card(string school) => new CoreVoyageSession().GetCardPackPool("neutral")
            .First(index => CoreVoyageSession.CardLibrary[index].SchoolId == school);
        private static CoreCardCopySave Copy(int index, int count) =>
            new CoreCardCopySave { id = CoreVoyageSession.CardLibrary[index].Id, count = count };
    }

    public sealed class CoreCardPackOddsTests
    {
        [TestCase("neutral")]
        [TestCase("fire")] [TestCase("arcane")] [TestCase("curse")]
        [TestCase("tide")] [TestCase("thunder")] [TestCase("blood")]
        public void PublishedOddsUseConfirmedCategoryWeightsAndSumToOneAcrossEveryEligibleCard(string pack)
        {
            var session = new CoreVoyageSession();
            IReadOnlyList<CoreCardPackSchoolOdds> odds = session.GetCardPackOdds(pack);
            Assert.That(odds.Count, Is.EqualTo(pack == "neutral" ? 7 : 2));
            Assert.That(odds.Sum(rate => rate.Weight), Is.EqualTo(100));
            Assert.That(odds.Sum(rate => rate.Probability), Is.EqualTo(1d).Within(1e-12));
            Assert.That(odds.Sum(rate => rate.CardProbability * rate.CardCount), Is.EqualTo(1d).Within(1e-12));
            foreach (CoreCardPackSchoolOdds rate in odds)
            {
                int expectedWeight = pack == "neutral" ? rate.SchoolId == "neutral" ? 28 : 12 :
                    rate.SchoolId == "neutral" ? 30 : 70;
                Assert.That(rate.Weight, Is.EqualTo(expectedWeight), rate.SchoolId);
                Assert.That(rate.Probability, Is.EqualTo(expectedWeight / 100d).Within(1e-12));
                int poolCount = session.GetCardPackPool(pack).Count(index => School(index) == rate.SchoolId);
                Assert.That(rate.CardCount, Is.EqualTo(poolCount).And.GreaterThan(0));
                Assert.That(rate.CardProbability, Is.EqualTo(expectedWeight / 100d / poolCount).Within(1e-12));
                Assert.That(CoreCardPackCatalog.Find(pack).DropRates.Single(item => item.SchoolId == rate.SchoolId).Weight,
                    Is.EqualTo(rate.Weight));
            }
            Assert.That(session.GetCardPackOdds("unknown"), Is.Empty);
        }

        [TestCase("neutral")]
        [TestCase("fire")] [TestCase("arcane")] [TestCase("curse")]
        [TestCase("tide")] [TestCase("thunder")] [TestCase("blood")]
        public void FullyCollectedPacksRetainPublishedBaseCategoryAndWithinCategoryProbabilities(string pack)
        {
            const int packs = 4000;
            var session = new CoreVoyageSession();
            CoreCardCopySave[] collected = session.GetCardPackPool("neutral")
                .Where(index => CoreVoyageSession.CardLibrary[index].SchoolId.Length > 0)
                .Select(index => new CoreCardCopySave
                    { id = CoreVoyageSession.CardLibrary[index].Id, count = CoreVoyageSession.BaseBattleCardCopies }).ToArray();
            Assert.That(session.RestoreCardPackProgress(new CoreCardPackProgress
            {
                packs = new[] { new CoreCardPackCountSave { id = pack, count = packs } },
                cards = collected,
                randomState = 0xa57f904bu,
            }), Is.True);
            var counts = session.GetCardPackPool(pack).ToDictionary(index => index, index => 0);
            for (int opened = 0; opened < packs; opened++)
            {
                Assert.That(session.TryOpenCardPack(pack, out int[] cards, out string reason), Is.True, reason);
                Assert.That(cards.Length, Is.EqualTo(3));
                foreach (int index in cards) counts[index]++;
            }
            int total = packs * CoreCardPackCatalog.CardsPerPack;
            foreach (CoreCardPackSchoolOdds rate in session.GetCardPackOdds(pack))
            {
                int actual = counts.Where(pair => School(pair.Key) == rate.SchoolId).Sum(pair => pair.Value);
                Assert.That((double)actual / total, Is.EqualTo(rate.Probability).Within(.025), pack + "/" + rate.SchoolId);
                foreach (var pair in counts.Where(pair => School(pair.Key) == rate.SchoolId))
                {
                    double expected = total * rate.CardProbability;
                    double deviation = 6d * Math.Sqrt(expected * (1d - rate.CardProbability)) + 1d;
                    Assert.That(pair.Value, Is.EqualTo(expected).Within(deviation),
                        pack + "/" + CoreVoyageSession.CardLibrary[pair.Key].Id + " must be uniform inside its category");
                }
            }
            Assert.That(counts.Values.Sum(), Is.EqualTo(total));
            Assert.That(session.GetCardPackCount(pack), Is.Zero);
            Assert.That(session.CaptureCardPackProgress().cards.Sum(card => card.count),
                Is.EqualTo(total + collected.Sum(card => card.count)));
        }

        private static string School(int index) => CoreVoyageSession.CardLibrary[index].SchoolId.Length == 0
            ? "neutral" : CoreVoyageSession.CardLibrary[index].SchoolId;
    }
}

