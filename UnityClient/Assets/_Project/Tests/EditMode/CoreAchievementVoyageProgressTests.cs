using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreAchievementVoyageProgressTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RealCardPlaysCountOnlyEditedStagesAfterTheBattleIsWon()
        {
            CoreVoyageSession session = Voyage(5);
            StartOrdinaryBattle(session);
            PlayGuard(session, NavalBattleStage.Preparation);
            PlayGuard(session, NavalBattleStage.Naval);
            PlayGuard(session, NavalBattleStage.Boarding);
            Assert.That(session.AchievementVoyageBattleWins, Is.Zero);
            Assert.That(session.AchievementWonEditedDeckStages, Is.Zero);
            WinCurrentBattle(session);
            Assert.That(session.AchievementVoyageBattleWins, Is.EqualTo(1));
            Assert.That(session.AchievementWonEditedDeckStages, Is.EqualTo(5));
        }

        [Test]
        public void MerelyEnteringStagesAndRejectedCardPlaysDoNotProveDeckUse()
        {
            CoreVoyageSession session = Voyage(7);
            StartOrdinaryBattle(session);
            Assert.That(session.PlayCard(-1), Is.False);
            Invoke(session, "EnterBattleStage", NavalBattleStage.Naval);
            Invoke(session, "EnterBattleStage", NavalBattleStage.Boarding);
            WinCurrentBattle(session);
            Assert.That(session.AchievementVoyageBattleWins, Is.EqualTo(1));
            Assert.That(session.AchievementWonEditedDeckStages, Is.Zero);
        }

        [Test]
        public void FailedBattleDoesNotKeepItsPlayedStageEvidence()
        {
            CoreVoyageSession session = Voyage(7);
            StartOrdinaryBattle(session);
            PlayGuard(session, NavalBattleStage.Preparation);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 0);
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.AchievementVoyageBattleWins, Is.Zero);
            Assert.That(session.AchievementWonEditedDeckStages, Is.Zero);
        }

        [Test]
        public void TrainingAndTutorialVictoriesDoNotCount()
        {
            CoreVoyageSession training = Voyage(7);
            Assert.That(training.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            PlayGuard(training, NavalBattleStage.Preparation);
            WinCurrentBattle(training);
            Assert.That(training.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(training.AchievementVoyageBattleWins, Is.Zero);
            Assert.That(training.AchievementWonEditedDeckStages, Is.Zero);

            var tutorial = new CoreVoyageSession();
            tutorial.ConfigureAchievementEditedDeckStages(7);
            Assert.That(tutorial.ConfigureGuidedTutorialVoyage(1), Is.True);
            tutorial.StartVoyage();
            StartOrdinaryBattle(tutorial);
            PlayGuard(tutorial, NavalBattleStage.Preparation);
            WinCurrentBattle(tutorial);
            Assert.That(tutorial.AchievementVoyageBattleWins, Is.Zero);
            Assert.That(tutorial.AchievementWonEditedDeckStages, Is.Zero);
        }

        [Test]
        public void ConsecutiveBattlesDoNotInheritThePreviousBattlesCardUses()
        {
            CoreVoyageSession session = Voyage(7);
            StartOrdinaryBattle(session);
            PlayGuard(session, NavalBattleStage.Preparation);
            WinCurrentBattle(session);
            StartOrdinaryBattle(session);
            PlayGuard(session, NavalBattleStage.Naval);
            WinCurrentBattle(session);
            Assert.That(session.AchievementVoyageBattleWins, Is.EqualTo(2));
            Assert.That(session.AchievementWonEditedDeckStages, Is.EqualTo(3));
        }

        [Test]
        public void RationsConsumedToZeroThenAwardedByTheSameEventRemainRecorded()
        {
            CoreVoyageSession session = Voyage();
            session.Cargo.Water = session.Cargo.Food = 1;
            Invoke(session, "ConsumeHours", .5d);
            Assert.That(session.Cargo.Water, Is.Zero);
            Invoke(session, "ApplyEffect", new VoyageEffect { Water = 10, Food = 10 });
            Assert.That(session.Cargo.Water, Is.EqualTo(10));
            Assert.That(session.AchievementVoyageRanOutOfSupplies, Is.True);
        }

        [Test]
        public void PortAndPostSettlementCargoChangesDoNotSpoilVoyageEvidence()
        {
            CoreVoyageSession session = Voyage();
            Assert.That(session.SafeReturn(), Is.True);
            session.Cargo.Water = 0;
            Assert.That(session.AchievementVoyageRanOutOfSupplies, Is.False);
            session.ReturnToPortAfterSettlement();
            session.Cargo.Food = 0;
            Assert.That(session.AchievementVoyageRanOutOfSupplies, Is.False);
        }

        [Test]
        public void TrainingBeforeDepartureDoesNotDisableLaterSupplyObservation()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            WinCurrentBattle(session);
            session.StartVoyage();
            session.Cargo.Water = 0;
            session.Cargo.Water = 20;
            Assert.That(session.AchievementVoyageRanOutOfSupplies, Is.True);
        }

        [Test]
        public void FinishedTrainingAtSeaDoesNotLeaveSupplyObservationDisabled()
        {
            CoreVoyageSession session = Voyage();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            WinCurrentBattle(session);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            session.Cargo.Food = 0;
            session.Cargo.Food = 20;
            Assert.That(session.AchievementVoyageRanOutOfSupplies, Is.True);
        }

        [Test]
        public void CheckpointKeepsVictoriesAndSupplyHistoryAndRebindsTheCargoObserver()
        {
            CoreVoyageSession source = Voyage(7);
            StartOrdinaryBattle(source);
            PlayGuard(source, NavalBattleStage.Preparation);
            WinCurrentBattle(source);
            var restored = Restore(source.CaptureVoyageCheckpoint());
            Assert.That(restored.AchievementVoyageBattleWins, Is.EqualTo(1));
            Assert.That(restored.AchievementWonEditedDeckStages, Is.EqualTo(1));
            Assert.That(restored.AchievementVoyageSupplyEvidenceAvailable, Is.True);
            restored.Cargo.Food = 0;
            restored.Cargo.Food = 30;
            Assert.That(restored.AchievementVoyageRanOutOfSupplies, Is.True);
            Assert.That(source.AchievementVoyageRanOutOfSupplies, Is.False);
            var again = Restore(restored.CaptureVoyageCheckpoint());
            Assert.That(again.AchievementVoyageRanOutOfSupplies, Is.True);
        }

        [Test]
        public void LegacyCheckpointDoesNotInventUninterruptedSupplyEvidence()
        {
            CoreVoyageCheckpoint checkpoint = Voyage().CaptureVoyageCheckpoint();
            checkpoint.achievementProgressVersion = 0;
            checkpoint.achievementVoyageSupplyEvidenceAvailable = false;
            var restored = Restore(checkpoint);
            Assert.That(restored.AchievementVoyageSupplyEvidenceAvailable, Is.False);
            Assert.That(Restore(restored.CaptureVoyageCheckpoint()).AchievementVoyageSupplyEvidenceAvailable, Is.False);
        }

        [Test]
        public void NewVoyageResetsThePreviousVoyagesEvidence()
        {
            CoreVoyageSession session = Voyage(7);
            StartOrdinaryBattle(session);
            PlayGuard(session, NavalBattleStage.Preparation);
            WinCurrentBattle(session);
            session.Cargo.Water = 0;
            session.Cargo.Water = 20;
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.StartVoyage();
            Assert.That(session.AchievementVoyageBattleWins, Is.Zero);
            Assert.That(session.AchievementWonEditedDeckStages, Is.Zero);
            Assert.That(session.AchievementVoyageRanOutOfSupplies, Is.False);
            Assert.That(session.AchievementVoyageSupplyEvidenceAvailable, Is.True);
        }

        [TestCase(2, 0, 0, 0)]
        [TestCase(1, 8, 0, 0)]
        [TestCase(1, 7, -1, 0)]
        [TestCase(1, 1, 1, 2)]
        [TestCase(1, 7, 0, 1)]
        public void CorruptEvidenceCannotPartiallyReplaceTheProfile(int version, int edited, int wins, int stages)
        {
            CoreVoyageCheckpoint checkpoint = Voyage().CaptureVoyageCheckpoint();
            checkpoint.achievementProgressVersion = version;
            checkpoint.achievementEditedDeckStages = edited;
            checkpoint.achievementVoyageBattleWins = wins;
            checkpoint.achievementWonEditedDeckStages = stages;
            var target = new CoreVoyageSession();
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.Map, Is.Null);
            Assert.That(target.AchievementVoyageBattleWins, Is.Zero);
        }

        private static CoreVoyageSession Voyage(int edited = 0)
        {
            var session = new CoreVoyageSession();
            session.ConfigureAchievementEditedDeckStages(edited);
            session.StartVoyage();
            return session;
        }

        private static void StartOrdinaryBattle(CoreVoyageSession session) => Invoke(session, "StartBattle",
            new CoreEncounter("achievement_battle_fixture", "成就验证敌舰", "海战", "隔离核心夹具", true, false, 1, 70), false);

        private static void PlayGuard(CoreVoyageSession session, NavalBattleStage stage)
        {
            if (session.Battle.Stage != stage) Invoke(session, "EnterBattleStage", stage);
            string id = stage == NavalBattleStage.Boarding ? "card_crew_guard" : "card_brace";
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == id);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            session.Battle.Energy = 20;
            Assert.That(session.PlayCard(0), Is.True, id);
        }

        private static void WinCurrentBattle(CoreVoyageSession session)
        {
            session.Battle.EnemyHull = 0;
            Invoke(session, "ResolveBattleOutcome");
        }

        private static CoreVoyageSession Restore(CoreVoyageCheckpoint checkpoint)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(
                JsonUtility.ToJson(checkpoint)), out string reason), Is.True, reason);
            return session;
        }

        private static void Invoke(CoreVoyageSession session, string name, params object[] arguments) =>
            typeof(CoreVoyageSession).GetMethod(name, Private).Invoke(session, arguments);
    }
}
