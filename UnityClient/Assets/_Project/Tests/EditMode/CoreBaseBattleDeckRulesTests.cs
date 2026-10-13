using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBaseBattleDeckRulesTests
    {
        [Test]
        public void BaseInventory_HoldsTwoOfEveryBaseCardAndNoSourceOrRetiredCards()
        {
            var session = new CoreVoyageSession();
            Assert.That(CoreVoyageSession.BaseBattleCardCopies, Is.EqualTo(2));
            Assert.That(CoreVoyageSession.MinimumBaseBattleDeckSize, Is.EqualTo(10));
            Assert.That(CoreVoyageSession.MaximumBaseBattleDeckSize, Is.EqualTo(20));
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                bool basic = card.AllowedStages != NavalBattleStages.None &&
                    card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0;
                Assert.That(session.GetBaseBattleCardOwnedCopies(index), Is.EqualTo(basic ? 2 : 0), card.Id);
            }
            Assert.That(session.GetBaseBattleCardOwnedCopies(-1), Is.Zero);
            Assert.That(session.GetBaseBattleCardOwnedCopies(CoreVoyageSession.CardLibrary.Count), Is.Zero);
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void EachStage_AllowsTenThroughTwentyAndEnforcesIndependentTwoCopyInventory(NavalBattleStage stage)
        {
            CoreVoyageSession session = UnlockedSession();
            int[] pool = session.GetAvailableBattleCards(stage).Take(11).ToArray();
            Assert.That(pool.Length, Is.EqualTo(11), "Fixture needs enough cards to isolate the size limit.");
            int[] legal = pool.Take(10).SelectMany(index => new[] { index, index }).ToArray();
            foreach (int count in new[] { 10, 11, 19, 20 })
            {
                Assert.That(session.SetBattleDeck(stage, legal.Take(count).ToArray()), Is.True);
                CollectionAssert.AreEqual(legal.Take(count), session.GetBattleDeck(stage));
            }
            Assert.That(session.SetBattleDeck(stage, legal.Take(9).ToArray()), Is.False);
            Assert.That(session.SetBattleDeck(stage, legal.Concat(new[] { pool[10] }).ToArray()), Is.False);
            int[] thirdCopy = legal.ToArray(); thirdCopy[thirdCopy.Length - 1] = legal[0];
            Assert.That(session.SetBattleDeck(stage, thirdCopy), Is.False);
            CollectionAssert.AreEqual(legal, session.GetBattleDeck(stage), "Rejected edits must be atomic.");
            Assert.That(session.SetBattleDeck(stage, session.GetBattleDeck(stage)), Is.True, "Self-assignment must preserve the list.");
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void PortRestore_TrimsThirdCopyPreservesOrderFillsSameStageAndIsIdempotent(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            int[][] otherDecks = Stages().Select(value => session.GetBattleDeck(value).ToArray()).ToArray();
            int[] legacy = LegacyDeck(session, stage);
            int[] preserved = legacy.Where((index, position) => legacy.Take(position).Count(value => value == index) < 2).ToArray();
            Assert.That(session.RestoreBattleDeck(stage, legacy), Is.True);
            int[] restored = session.GetBattleDeck(stage).ToArray();
            Assert.That(restored.Length, Is.EqualTo(10));
            CollectionAssert.AreEqual(preserved, restored.Take(preserved.Length));
            Assert.That(restored.GroupBy(index => index).All(group => group.Count() <= 2), Is.True);
            Assert.That(restored.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(stage)), Is.True);
            Assert.That(session.RestoreBattleDeck(stage, restored), Is.True);
            CollectionAssert.AreEqual(restored, session.GetBattleDeck(stage));
            foreach (NavalBattleStage other in Stages().Where(value => value != stage))
                CollectionAssert.AreEqual(otherDecks[(int)other], session.GetBattleDeck(other));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void PortRestore_DiscardsUnusableEntriesWithoutUnlockingCards(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            int[] unavailable = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Where(index => !session.GetAvailableBattleCards(stage).Contains(index)).ToArray();
            Assert.That(session.RestoreBattleDeck(stage, new[] { -1, int.MaxValue }.Concat(unavailable).ToArray()), Is.True);
            CollectionAssert.AreEqual(new CoreVoyageSession().GetBattleDeck(stage).Take(CoreVoyageSession.MinimumBaseBattleDeckSize),
                session.GetBattleDeck(stage), "Migration fills the ten-card minimum; the twelve-card starter preset may contain more.");
            session.StartVoyage();
            int[] before = session.GetBattleDeck(stage).ToArray();
            Assert.That(session.RestoreBattleDeck(stage, Array.Empty<int>()), Is.False);
            CollectionAssert.AreEqual(before, session.GetBattleDeck(stage));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void CheckpointRestore_MigratesOldEightCardThreeCopyDeckAndKeepsOtherStages(NavalBattleStage stage)
        {
            var source = new CoreVoyageSession();
            int[] legacy = LegacyDeck(source, stage);
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.battleDecks[(int)stage].cards = legacy;
            var expected = new CoreVoyageSession();
            Assert.That(expected.RestoreBattleDeck(stage, legacy), Is.True);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            foreach (NavalBattleStage value in Stages())
                CollectionAssert.AreEqual(expected.GetBattleDeck(value), restored.GetBattleDeck(value));
            CollectionAssert.AreEqual(legacy, checkpoint.battleDecks[(int)stage].cards, "Migration must not mutate its input DTO.");
            var again = new CoreVoyageSession();
            Assert.That(again.TryRestoreVoyageCheckpoint(restored.CaptureVoyageCheckpoint(), out reason), Is.True, reason);
            CollectionAssert.AreEqual(restored.GetBattleDeck(stage), again.GetBattleDeck(stage));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void CheckpointRestore_PreservesNewTwentyCardDeck(NavalBattleStage stage)
        {
            CoreVoyageSession source = UnlockedSession();
            int[] cards = source.GetAvailableBattleCards(stage).Take(10).SelectMany(index => new[] { index, index }).ToArray();
            Assert.That(source.SetBattleDeck(stage, cards), Is.True);
            source.StartVoyage();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(source.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            CollectionAssert.AreEqual(cards, restored.GetBattleDeck(stage));
        }

        [TestCase("invalid-index")]
        [TestCase("wrong-stage")]
        [TestCase("source-card")]
        [TestCase("four-copies")]
        [TestCase("seven-cards")]
        public void CheckpointRestore_RejectsCorruptionInsteadOfTreatingItAsLegacyMigration(string corruption)
        {
            var source = new CoreVoyageSession(); source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            int[] cards = checkpoint.battleDecks[(int)NavalBattleStage.Preparation].cards;
            switch (corruption)
            {
                case "invalid-index": cards[0] = int.MaxValue; break;
                case "wrong-stage": cards[0] = Index("card_cutlass_raid"); break;
                case "source-card": cards[0] = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .First(index => CoreVoyageSession.CardLibrary[index].RequiredCrew.Length > 0); break;
                case "four-copies": for (int index = 1; index < 4; index++) cards[index] = cards[0]; break;
                case "seven-cards": checkpoint.battleDecks[0].cards = cards.Take(7).ToArray(); break;
            }
            var target = new CoreVoyageSession(); target.RestoreProfile(987m, "铜钟港");
            int[] before = target.GetBattleDeck(NavalBattleStage.Preparation).ToArray();
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(target.ProfileGold, Is.EqualTo(987m));
            Assert.That(target.CurrentPort, Is.EqualTo("铜钟港"));
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            CollectionAssert.AreEqual(before, target.GetBattleDeck(NavalBattleStage.Preparation));
        }

        [Test]
        public void SourceCards_AreAddedOutsideTwentyCardLimitAndCannotConsumeBaseInventory()
        {
            CoreVoyageSession session = UnlockedSession();
            session.ConfigureSpecialCardSources(new[] { "crew_twilight_tide_gunner_yilan" }, null);
            foreach (NavalBattleStage stage in Stages())
            {
                int[] cards = session.GetAvailableBattleCards(stage).Take(10).SelectMany(index => new[] { index, index }).ToArray();
                Assert.That(session.SetBattleDeck(stage, cards), Is.True);
                foreach (int bonus in session.GetBonusBattleCards(stage))
                {
                    Assert.That(session.GetBaseBattleCardOwnedCopies(bonus), Is.Zero);
                    int[] withSource = cards.ToArray(); withSource[0] = bonus;
                    Assert.That(session.SetBattleDeck(stage, withSource), Is.False);
                }
                CollectionAssert.AreEqual(cards, session.GetBattleDeck(stage));
            }
            Assert.That(session.GetBonusBattleCards(NavalBattleStage.Naval), Is.Not.Empty);
            Assert.That(session.GetBattleDeck(NavalBattleStage.Naval).Count + session.GetBonusBattleCards(NavalBattleStage.Naval).Count, Is.GreaterThan(20));
        }

        private static int[] LegacyDeck(CoreVoyageSession session, NavalBattleStage stage)
        {
            int[] available = session.GetAvailableBattleCards(stage).Take(6).ToArray();
            return Enumerable.Repeat(available[0], 3).Concat(available.Skip(1)).ToArray();
        }
        private static NavalBattleStage[] Stages() => new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static CoreVoyageSession UnlockedSession()
        {
            var session = new CoreVoyageSession();
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.pirateExperience = 5400;
            session.RestoreTreasureProgress(progress);
            return session;
        }
    }
}
