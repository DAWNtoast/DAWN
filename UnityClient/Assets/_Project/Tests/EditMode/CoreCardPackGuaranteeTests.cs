using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardPackGuaranteeTests
    {
        private static readonly string[] PackIds = { "neutral", "fire", "arcane", "curse", "tide", "thunder", "blood" };

        [TestCase(0)]
        [TestCase(1)]
        public void SecondConsecutiveMissSuppliesTheOnlyUnfilledCardAndKeepsThreeRewards(int copies)
        {
            int missing = SchoolCard("fire");
            uint seed = FindSeed(new[] { "fire", "fire" }, results => results.All(cards => !cards.Contains(missing)));
            int[][] natural = NaturalDraws(seed, "fire", "fire");
            CoreVoyageSession session = Collected(seed, missing, copies);
            CollectionAssert.AreEqual(natural[0], Open(session, "fire"), "The first pack keeps its ordinary draw.");
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.EqualTo(1));
            Assert.That(session.GetBaseBattleCardOwnedCopies(missing), Is.EqualTo(copies));

            int[] guaranteed = Open(session, "fire");
            Assert.That(guaranteed, Has.Length.EqualTo(CoreCardPackCatalog.CardsPerPack));
            Assert.That(guaranteed.Count(index => index == missing), Is.EqualTo(1));
            CollectionAssert.AreEqual(natural[1].Take(2), guaranteed.Take(2));
            Assert.That(session.GetBaseBattleCardOwnedCopies(missing), Is.EqualTo(copies + 1));
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.Zero);
            Assert.That(session.GetCardPackCount("fire"), Is.EqualTo(6));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void NaturalUsefulCopyClearsPendingWithoutReplacingRewardsOrAdvancingExtraRandomness(int copies)
        {
            int missing = SchoolCard("fire");
            uint seed = FindSeed(new[] { "fire" }, results => results[0].Contains(missing));
            CoreVoyageSession ordinary = Collected(seed);
            CoreVoyageSession pending = Collected(seed, missing, copies, 1);
            CollectionAssert.AreEqual(Open(ordinary, "fire"), Open(pending, "fire"));
            Assert.That(pending.CaptureCardPackProgress().randomState,
                Is.EqualTo(ordinary.CaptureCardPackProgress().randomState));
            Assert.That(pending.CaptureCardPackProgress().packsWithoutUsableCopy, Is.Zero);
        }

        [Test]
        public void DuplicateUsefulCardsWithinOnePackRemainCreditedAndStillClearPending()
        {
            uint seed = FindSeed(new[] { "fire" }, results => results[0].GroupBy(index => index)
                .Any(group => School(group.Key) == "fire" && group.Count() > 1));
            int[] expected = NaturalDraws(seed, "fire")[0];
            int missing = expected.GroupBy(index => index).First(group =>
                School(group.Key) == "fire" && group.Count() > 1).Key;
            CoreVoyageSession session = Collected(seed, missing, 1, 1);
            CollectionAssert.AreEqual(expected, Open(session, "fire"));
            Assert.That(session.GetBaseBattleCardOwnedCopies(missing), Is.EqualTo(1 + expected.Count(index => index == missing)));
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.Zero);
        }

        [Test]
        public void PendingGuaranteeIsSharedAcrossTypesAndRemainsPendingThroughFullyCollectedPools()
        {
            int missing = SchoolCard("arcane");
            uint seed = FindSeed(new[] { "fire", "curse", "arcane" }, results => !results[2].Contains(missing));
            int[][] natural = NaturalDraws(seed, "fire", "curse", "arcane");
            CoreVoyageSession session = Collected(seed, missing, 1);
            CollectionAssert.AreEqual(natural[0], Open(session, "fire"));
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.EqualTo(1));
            CollectionAssert.AreEqual(natural[1], Open(session, "curse"));
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.EqualTo(1));
            Assert.That(session.GetBaseBattleCardOwnedCopies(missing), Is.EqualTo(1),
                "A full school pack cannot inject an unowned card from another school.");
            int[] result = Open(session, "arcane");
            Assert.That(result, Has.Member(missing));
            Assert.That(result.All(index => session.GetCardPackPool("arcane").Contains(index)), Is.True);
            Assert.That(session.GetBaseBattleCardOwnedCopies(missing), Is.EqualTo(2));
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.Zero);
        }

        [Test]
        public void EveryTwoSuccessfulPacksProduceAUsefulCopyWhileTheCurrentLegalPoolHasOne()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreCardPackProgress(new CoreCardPackProgress
            {
                randomState = 941523u,
                packs = PackIds.Select(id => new CoreCardPackCountSave { id = id, count = 40 }).ToArray(),
            }), Is.True);
            bool previousMiss = false;
            for (int opened = 0; opened < 280; opened++)
            {
                string pack = PackIds[opened % PackIds.Length];
                var usefulBefore = new HashSet<int>(session.GetCardPackPool(pack)
                    .Where(index => session.GetBaseBattleCardOwnedCopies(index) < 2));
                int[] cards = Open(session, pack);
                bool useful = cards.Any(usefulBefore.Contains);
                if (previousMiss && usefulBefore.Count > 0)
                    Assert.That(useful, Is.True, "Consecutive misses at pack " + opened + "/" + pack);
                previousMiss = !useful;
                Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.EqualTo(useful ? 0 : 1));
            }
        }

        [Test]
        public void ProfileAndVoyageRoundTripsPreservePendingAndTheNextGuaranteedResult()
        {
            int missing = SchoolCard("fire");
            uint seed = FindSeed(new[] { "fire" }, results => !results[0].Contains(missing));
            CoreVoyageSession source = Collected(seed, missing, 1, 1);
            string json = JsonUtility.ToJson(source.CaptureCardPackProgress());
            var profile = new CoreVoyageSession();
            Assert.That(profile.RestoreCardPackProgress(JsonUtility.FromJson<CoreCardPackProgress>(json)), Is.True);
            source.StartVoyage();
            var voyage = new CoreVoyageSession();
            var checkpoint = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            Assert.That(voyage.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(JsonUtility.ToJson(voyage.CaptureCardPackProgress()), Is.EqualTo(json));
            source.SafeReturn(); voyage.SafeReturn();
            source.ReturnToPortAfterSettlement(); voyage.ReturnToPortAfterSettlement();
            int[] expected = Open(source, "fire");
            Assert.That(expected, Has.Member(missing));
            CollectionAssert.AreEqual(expected, Open(profile, "fire"));
            CollectionAssert.AreEqual(expected, Open(voyage, "fire"));
            Assert.That(JsonUtility.ToJson(profile.CaptureCardPackProgress()),
                Is.EqualTo(JsonUtility.ToJson(source.CaptureCardPackProgress())));
            Assert.That(JsonUtility.ToJson(voyage.CaptureCardPackProgress()),
                Is.EqualTo(JsonUtility.ToJson(source.CaptureCardPackProgress())));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void LegacyJsonWithoutTheOptionalCounterStartsWithoutPendingGuarantee(int version)
        {
            CoreVoyageSession session = Collected(31u, pending: 1);
            string json = "{\"version\":" + version + ",\"packs\":[],\"cards\":[],\"randomState\":1234}";
            Assert.That(session.RestoreCardPackProgress(JsonUtility.FromJson<CoreCardPackProgress>(json), 1), Is.True);
            Assert.That(session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.Zero);
            Assert.That(session.CaptureCardPackProgress().randomState, Is.EqualTo(1234u));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidCounterRejectsProfileAndVoyageRestoreWithoutChangingLiveState(int invalid)
        {
            CoreVoyageSession target = Collected(31u, pending: 1);
            string before = JsonUtility.ToJson(target.CaptureCardPackProgress());
            CoreCardPackProgress progress = target.CaptureCardPackProgress();
            progress.packsWithoutUsableCopy = invalid;
            Assert.That(target.RestoreCardPackProgress(progress), Is.False);
            Assert.That(JsonUtility.ToJson(target.CaptureCardPackProgress()), Is.EqualTo(before));
            var voyage = new CoreVoyageSession();
            voyage.StartVoyage();
            CoreVoyageCheckpoint checkpoint = voyage.CaptureVoyageCheckpoint();
            checkpoint.cardPackProgress = progress;
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(JsonUtility.ToJson(target.CaptureCardPackProgress()), Is.EqualTo(before));
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
        }

        [Test]
        public void FailedPackActionsPreservePendingInventoryAndRandomState()
        {
            int missing = SchoolCard("fire");
            uint seed = FindSeed(new[] { "fire" }, results => !results[0].Contains(missing));
            CoreVoyageSession session = Collected(seed, missing, 1, 1);
            CoreCardPackProgress saturated = session.CaptureCardPackProgress();
            saturated.cards = session.GetCardPackPool("neutral").Select(index => new CoreCardCopySave
            {
                id = CoreVoyageSession.CardLibrary[index].Id,
                count = index == missing ? 1 :
                    int.MaxValue - (School(index).Length == 0 ? CoreVoyageSession.BaseBattleCardCopies : 0),
            }).ToArray();
            saturated.packs = new[] { new CoreCardPackCountSave { id = "fire", count = 1 } };
            Assert.That(session.RestoreCardPackProgress(saturated), Is.True);
            string before = JsonUtility.ToJson(session.CaptureCardPackProgress());
            foreach (string pack in new[] { "missing", "arcane", "fire" })
            {
                Assert.That(session.TryOpenCardPack(pack, out int[] cards, out _), Is.False);
                Assert.That(cards, Is.Empty);
                Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
            }
            session.StartVoyage();
            Assert.That(session.TryOpenCardPack("fire", out _, out _), Is.False);
            Assert.That(JsonUtility.ToJson(session.CaptureCardPackProgress()), Is.EqualTo(before));
        }

        private static CoreVoyageSession Collected(uint seed, int missing = -1, int copies = 0, int pending = 0)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreCardPackProgress(new CoreCardPackProgress
            {
                packs = PackIds.Select(id => new CoreCardPackCountSave { id = id, count = 8 }).ToArray(),
                cards = session.GetCardPackPool("neutral").Where(index => School(index).Length > 0)
                    .Select(index => new CoreCardCopySave
                    { id = CoreVoyageSession.CardLibrary[index].Id, count = index == missing ? copies : 2 }).ToArray(),
                randomState = seed,
                packsWithoutUsableCopy = pending,
            }), Is.True);
            return session;
        }

        private static int SchoolCard(string school) => new CoreVoyageSession().GetCardPackPool(school)
            .First(index => School(index) == school);
        private static string School(int index) => CoreVoyageSession.CardLibrary[index].SchoolId;
        private static int[] Open(CoreVoyageSession session, string pack)
        {
            Assert.That(session.TryOpenCardPack(pack, out int[] result, out string reason), Is.True, reason);
            return result;
        }
        private static int[][] NaturalDraws(uint seed, params string[] packs)
        {
            CoreVoyageSession session = Collected(seed);
            return packs.Select(pack => Open(session, pack)).ToArray();
        }
        private static uint FindSeed(string[] packs, Func<int[][], bool> matches)
        {
            for (uint seed = 0; seed < 4096; seed++)
                if (matches(NaturalDraws(seed, packs))) return seed;
            Assert.Fail("No deterministic fixture seed found.");
            return 0;
        }
    }
}
