using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardPackTests
    {
        private static readonly string[] PackIds = { "neutral", "fire", "arcane", "curse", "tide", "thunder", "blood" };

        [Test]
        public void SevenPoolsContainEveryLegalBasicCardExactlyOnceWithoutLevelFiltering()
        {
            var session = new CoreVoyageSession();
            CollectionAssert.AreEqual(PackIds, CoreCardPackCatalog.All.Select(pack => pack.Id));
            int[] all = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Where(index =>
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                return index == CoreVoyageSession.CanonicalBattleCardIndex(index) &&
                    card.AllowedStages != NavalBattleStages.None &&
                    card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 &&
                    !CoreVoyageSession.IsSpecialBattleCard(card.Id) &&
                    (card.IsAllowedInStage(NavalBattleStage.Preparation) || card.IsAllowedInStage(NavalBattleStage.Naval) ||
                     card.IsAllowedInStage(NavalBattleStage.Boarding) && !CoreBoardingCardCatalog.IsLegacyAlias(card));
            }).ToArray();
            foreach (string pack in PackIds)
            {
                int[] expected = all.Where(index => pack == "neutral" ||
                    CoreVoyageSession.CardLibrary[index].SchoolId.Length == 0 ||
                    CoreVoyageSession.CardLibrary[index].SchoolId == pack).ToArray();
                CollectionAssert.AreEqual(expected, session.GetCardPackPool(pack), pack);
                Assert.That(expected, Is.Not.Empty, pack);
                Assert.That(expected.Distinct().Count(), Is.EqualTo(expected.Length));
            }
            Assert.That(session.GetCardPackPool("neutral").Any(index =>
                !session.IsBattleCardUnlocked(CoreVoyageSession.CardLibrary[index])), Is.True,
                "Opening a pack must be able to discover every school without a level grant.");
            Assert.That(session.GetCardPackPool("missing"), Is.Empty);
        }

        [TestCaseSource(nameof(PackIds))]
        public void OpeningSpendsOnePackAndAddsExactlyThreeIndependentCopiesWithoutChangingOtherAssets(string pack)
        {
            CoreVoyageSession session = Seeded(pack, 2, 42u);
            decimal gold = session.ProfileGold;
            int[][] decks = Stages.Select(stage => session.GetBattleDeck(stage).ToArray()).ToArray();
            Dictionary<int, int> owned = session.GetCardPackPool(pack).ToDictionary(index => index, session.GetBaseBattleCardOwnedCopies);
            Assert.That(session.TryOpenCardPack(pack, out int[] cards, out string reason), Is.True, reason);
            Assert.That(cards, Has.Length.EqualTo(3));
            Assert.That(session.GetCardPackCount(pack), Is.EqualTo(1));
            foreach (int card in cards) Assert.That(session.GetCardPackPool(pack), Has.Member(card));
            foreach (var pair in owned)
                Assert.That(session.GetBaseBattleCardOwnedCopies(pair.Key), Is.EqualTo(pair.Value + cards.Count(index => index == pair.Key)));
            Assert.That(session.CaptureCardPackProgress().cards.Sum(item => item.count), Is.EqualTo(3));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            for (int stage = 0; stage < 3; stage++) CollectionAssert.AreEqual(decks[stage], session.GetBattleDeck(Stages[stage]));
        }

        [Test]
        public void ARepeatedDrawKeepsAllThreeRewardSlotsAndCreditsEveryCopy()
        {
            var session = new CoreVoyageSession();
            bool duplicate = false;
            for (uint seed = 0; seed < 2048 && !duplicate; seed++)
            {
                Assert.That(session.RestoreCardPackProgress(Progress("fire", 1, seed)), Is.True);
                Assert.That(session.TryOpenCardPack("fire", out int[] cards, out _), Is.True);
                duplicate = cards.Distinct().Count() < cards.Length;
                if (!duplicate) continue;
                Assert.That(cards, Has.Length.EqualTo(3));
                foreach (IGrouping<int, int> group in cards.GroupBy(index => index))
                    Assert.That(session.CaptureCardPackProgress().cards.Single(item =>
                        item.id == CoreVoyageSession.CardLibrary[group.Key].Id).count, Is.EqualTo(group.Count()));
            }
            Assert.That(duplicate, Is.True, "Independent draws with replacement permit repeats within one pack.");
        }

        [TestCase("fire")] [TestCase("arcane")] [TestCase("curse")]
        [TestCase("tide")] [TestCase("thunder")] [TestCase("blood")]
        public void OneEarnedLockedCardIsUsableWithoutGrantingItsWholeSchoolOrAnUnownedSecondCopy(string school)
        {
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i =>
                CoreVoyageSession.CardLibrary[i].SchoolId == school &&
                session.GetCardPackPool(school).Contains(i) && CoreVoyageSession.CardLibrary[i].IsAllowedInStage(NavalBattleStage.Naval));
            CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
            Assert.That(session.GetBaseBattleCardOwnedCopies(index), Is.Zero);
            Assert.That(session.IsBattleCardUnlocked(card), Is.False);
            CoreCardPackProgress progress = Progress("neutral", 0, 5u);
            progress.cards = new[] { new CoreCardCopySave { id = card.Id, count = 1 } };
            Assert.That(session.RestoreCardPackProgress(progress), Is.True);
            Assert.That(session.IsBattleCardUnlocked(card), Is.True);
            Assert.That(session.GetBattleCardUnlockReason(card), Is.Empty);
            Assert.That(session.GetAvailableBattleCards(NavalBattleStage.Naval), Has.Member(index));
            Assert.That(session.GetBaseBattleCardOwnedCopies(index), Is.EqualTo(1));
            List<int> deck = session.GetBattleDeck(NavalBattleStage.Naval).ToList();
            deck[0] = index;
            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            deck[1] = index;
            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.False);
            Assert.That(session.GetBattleDeck(NavalBattleStage.Naval).Count(i => i == index), Is.EqualTo(1));
            Assert.That(session.GetCardPackPool(school).Any(other => other != index &&
                CoreVoyageSession.CardLibrary[other].SchoolId == school && !session.IsBattleCardUnlocked(CoreVoyageSession.CardLibrary[other])), Is.True);
            session.StartVoyage();
            var resumed = new CoreVoyageSession();
            Assert.That(resumed.TryRestoreVoyageCheckpoint(session.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            Assert.That(resumed.GetBaseBattleCardOwnedCopies(index), Is.EqualTo(1));
            CollectionAssert.AreEqual(session.GetBattleDeck(NavalBattleStage.Naval), resumed.GetBattleDeck(NavalBattleStage.Naval),
                "Acquired copies must be restored before validating a locked-school deck.");
        }

        [Test]
        public void LargeOwnedCountsRemainExactButTheBattleDeckKeepsItsTwoCopyLimit()
        {
            var session = new CoreVoyageSession();
            int index = session.GetBattleDeck(NavalBattleStage.Naval)[0];
            CoreCardPackProgress progress = Progress("neutral", 0, 8u);
            progress.cards = new[] { new CoreCardCopySave { id = CoreVoyageSession.CardLibrary[index].Id, count = 27 } };
            Assert.That(session.RestoreCardPackProgress(progress), Is.True);
            Assert.That(session.GetBaseBattleCardOwnedCopies(index), Is.EqualTo(29));
            List<int> deck = session.GetBattleDeck(NavalBattleStage.Naval).Where(i => i != index).Take(7).ToList();
            while (deck.Count < 7) deck.Add(session.GetAvailableBattleCards(NavalBattleStage.Naval).First(i =>
                i != index && deck.Count(value => value == i) < 2));
            deck.AddRange(new[] { index, index, index });
            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.False);
            Assert.That(session.RestoreBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            Assert.That(session.GetBattleDeck(NavalBattleStage.Naval).Count(i => i == index), Is.EqualTo(2));
            Assert.That(session.GetBaseBattleCardOwnedCopies(index), Is.EqualTo(29));
        }

        [Test]
        public void EmptyUnknownAndAtSeaOpenAttemptsLeaveInventoryRandomnessGoldAndDecksUntouched()
        {
            CoreVoyageSession session = Seeded("neutral", 1, 90u);
            string before = JsonUtility.ToJson(session.CaptureCardPackProgress());
            decimal gold = session.ProfileGold;
            foreach (string pack in new[] { "fire", "missing", null })
            {
                Assert.That(session.TryOpenCardPack(pack, out int[] cards, out string reason), Is.False);
                Assert.That(cards, Is.Empty); Assert.That(reason, Is.Not.Empty);
                Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            }
            session.StartVoyage();
            Assert.That(session.TryOpenCardPack("neutral", out int[] atSea, out _), Is.False);
            Assert.That(atSea, Is.Empty);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void OverflowAndInvalidRestoreAreAtomicAndCapturedDtosNeverAliasLiveInventory()
        {
            CoreVoyageSession session = Seeded("neutral", 1, 2u);
            CoreCardPackProgress saturated = session.CaptureCardPackProgress();
            saturated.cards = session.GetCardPackPool("neutral").Select(index => new CoreCardCopySave {
                id = CoreVoyageSession.CardLibrary[index].Id,
                count = int.MaxValue - (CoreVoyageSession.CardLibrary[index].SchoolId.Length == 0 ? CoreVoyageSession.BaseBattleCardCopies : 0)
            }).ToArray();
            Assert.That(session.RestoreCardPackProgress(saturated), Is.True);
            string before = JsonUtility.ToJson(session.CaptureCardPackProgress());
            Assert.That(session.TryOpenCardPack("neutral", out int[] cards, out _), Is.False);
            Assert.That(cards, Is.Empty);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            CoreCardPackProgress invalid = session.CaptureCardPackProgress();
            invalid.packs[0].count = -1;
            Assert.That(session.RestoreCardPackProgress(invalid), Is.False);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            invalid = session.CaptureCardPackProgress();
            invalid.cards[0].id = CoreSpecialCardCatalog.All[0].Card.Id;
            Assert.That(session.RestoreCardPackProgress(invalid), Is.False);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            Assert.That(session.GrantCardPacks("neutral", int.MaxValue), Is.False);
            Assert.That(session.GetCardPackCount("neutral"), Is.EqualTo(1));
        }

        [Test]
        public void ProfileAndVoyageRoundTripsPreserveUnlockedDrawsInventoryAndTheNextPackResult()
        {
            CoreVoyageSession source = Seeded("fire", 3, 47u);
            Assert.That(source.TryOpenCardPack("fire", out _, out _), Is.True);
            string profile = JsonUtility.ToJson(source.CaptureCardPackProgress());
            var copied = new CoreVoyageSession();
            Assert.That(copied.RestoreCardPackProgress(JsonUtility.FromJson<CoreCardPackProgress>(profile)), Is.True);
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            var resumed = new CoreVoyageSession();
            Assert.That(resumed.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(JsonUtility.ToJson(resumed.CaptureCardPackProgress()), Is.EqualTo(profile));
            source.SafeReturn(); resumed.SafeReturn();
            source.ReturnToPortAfterSettlement(); resumed.ReturnToPortAfterSettlement();
            Assert.That(source.TryOpenCardPack("fire", out int[] first, out _), Is.True);
            Assert.That(resumed.TryOpenCardPack("fire", out int[] second, out _), Is.True);
            Assert.That(copied.TryOpenCardPack("fire", out int[] third, out _), Is.True);
            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreEqual(first, third);
            Assert.That(JsonUtility.ToJson(resumed.CaptureCardPackProgress()), Is.EqualTo(JsonUtility.ToJson(source.CaptureCardPackProgress())));
        }

        [Test]
        public void MissingLegacyProgressKeepsHistoricCollectionsAndInvalidCheckpointDoesNotAlterTheProfile()
        {
            var source = new CoreVoyageSession();
            int[] available = source.GetAvailableBattleCards(NavalBattleStage.Naval).ToArray();
            Assert.That(source.RestoreCardPackProgress(null), Is.True);
            CollectionAssert.AreEqual(available, source.GetAvailableBattleCards(NavalBattleStage.Naval));
            Assert.That(available.All(index => source.GetBaseBattleCardOwnedCopies(index) == 2), Is.True);
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.cardPackProgress = null;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.CaptureCardPackProgress().packs, Is.Empty);
            checkpoint.cardPackProgress = Progress("neutral", -1, 3u);
            var target = Seeded("tide", 4, 91u);
            string before = JsonUtility.ToJson(target.CaptureCardPackProgress());
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(JsonUtility.ToJson(target.CaptureCardPackProgress()), Is.EqualTo(before));
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
        }

        private static IEnumerable<TestCaseData> BossVictories()
        {
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All)
                foreach (bool captured in new[] { false, true })
                    yield return new TestCaseData(boss.Id, boss.SchoolId, captured);
            foreach (CoreLegendaryWarship boss in CoreLegendaryWarships.All)
                foreach (bool captured in new[] { false, true })
                    yield return new TestCaseData(boss.Id, boss.SchoolId, captured);
            yield return new TestCaseData(CoreBattleBalance.TreasureBossId, "curse", false);
            yield return new TestCaseData(CoreBattleBalance.TreasureBossId, "curse", true);
        }

        [TestCaseSource(nameof(BossVictories))]
        public void EveryAuthoredBossDropsExactlyOneMatchingPackForEitherVictoryType(string id, string school, bool captured)
        {
            CoreVoyageSession session = EnterBattle(id);
            Win(session, captured);
            Assert.That(session.GetCardPackCount(school), Is.EqualTo(1));
            Assert.That(session.CaptureCardPackProgress().packs.Sum(pack => pack.count), Is.EqualTo(1));
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.GetCardPackCount(school), Is.EqualTo(1), "A terminal callback cannot claim the same win twice.");
        }

        [TestCase("random_ambush")] [TestCase("random_ambush_warning")]
        [TestCase("event_reef_coral_ambush")] [TestCase("event_reef_open_water_ambush")]
        [TestCase("event_trade_open_water_ambush")] [TestCase("event_moon_open_water_ambush")]
        [TestCase(CoreGuidedTutorialContent.AmbushEventId)]
        public void ExplicitVoyageAmbushesDropOneOrdinaryPack(string id)
        {
            CoreVoyageSession session = EnterBattle(id);
            Win(session, false);
            Assert.That(session.GetCardPackCount("neutral"), Is.EqualTo(1));
            Assert.That(session.CaptureCardPackProgress().packs.Sum(pack => pack.count), Is.EqualTo(1));
        }

        [TestCase("ordinary")] [TestCase("defeat")] [TestCase("retreat")] [TestCase("exhausted")]
        public void OtherBattlesAndUnwonOrExhaustedRewardsDoNotGrantPacks(string outcome)
        {
            CoreVoyageSession session = EnterBattle(outcome == "ordinary" ? "unrelated-battle" : "random_ambush");
            if (outcome == "exhausted")
                typeof(CoreVoyageSession).GetField("pendingSeaChartRewardMultiplier", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 0d);
            if (outcome == "defeat")
            {
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 0);
                Assert.That(session.EndBattleTurn(), Is.True);
            }
            else if (outcome == "retreat") Assert.That(session.RetreatFromBattle(), Is.True);
            else Win(session, false);
            Assert.That(session.CaptureCardPackProgress().packs, Is.Empty);
        }

        [Test]
        public void TrainingVictoryDoesNotGrantAnyCardPack()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(session.CaptureCardPackProgress().packs, Is.Empty);
        }

        [Test]
        public void WonAmbushPackSurvivesVoyageCheckpointAndReturnWithoutBeingGrantedAgain()
        {
            CoreVoyageSession source = EnterBattle("event_reef_coral_ambush");
            Win(source, false);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(source.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            Assert.That(restored.GetCardPackCount("neutral"), Is.EqualTo(1));
            restored.SafeReturn(); restored.ReturnToPortAfterSettlement(); restored.ReturnToPortAfterSettlement();
            Assert.That(restored.GetCardPackCount("neutral"), Is.EqualTo(1));
        }

        private static readonly NavalBattleStage[] Stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
        private static CoreCardPackProgress Progress(string pack, int count, uint seed) => new CoreCardPackProgress {
            packs = new[] { new CoreCardPackCountSave { id = pack, count = count } }, randomState = seed
        };
        private static CoreVoyageSession Seeded(string pack, int count, uint seed)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreCardPackProgress(Progress(pack, count, seed)), Is.True);
            return session;
        }

        private static CoreVoyageSession EnterBattle(string id)
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            CoreEncounter encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ??
                CoreLegendaryWarships.Find(id)?.CreateEncounter() ??
                (id == CoreBattleBalance.TreasureBossId ? CoreLegendaryWarships.CreateTreasureEncounter() :
                new CoreEncounter(id, "卡包奖励测试", "海战", "测试", true, false, 1, 70,
                    new CoreEncounterChoice("迎战", "胜利后结算", 0, new VoyageEffect(), startsBattle: true)));
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(0), Is.True);
            return session;
        }

        private static void Win(CoreVoyageSession session, bool captured)
        {
            if (captured) { session.Battle.Stage = NavalBattleStage.Boarding; session.Battle.EnemyBoardingHealth = 0; }
            else session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }
    }
}
