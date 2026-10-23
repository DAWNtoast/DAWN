using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalArrivalContactTests
    {
        [TestCase(true, 90f)] [TestCase(true, 270f)]
        [TestCase(false, 0f)] [TestCase(false, 90f)] [TestCase(false, 180f)] [TestCase(false, 270f)]
        public void ArrivalContactUsesOneRemainingPointAndItsLegalFinalHeading(bool boarding, float enemyHeading)
        {
            CoreVoyageSession session = Arrival(2, enemyHeading);
            CoreBattleState battle = session.Battle;
            Assert.That(battle.NavalActionPoints, Is.Zero);
            Assert.That(battle.NavalArrivalActionPoints, Is.EqualTo(1));
            Assert.That(battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(session.CanBoard(out _), Is.False);
            Assert.That(session.CanRam(out _), Is.False);
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.CanPlayCard(0), Is.False);
            string before = ArrivalState(session);
            Assert.That(session.CanNavalArrivalContact(boarding, out float heading, out string reason), Is.True, reason);
            Assert.That(heading, Is.EqualTo(boarding ? enemyHeading : 0f));
            Assert.That(ArrivalState(session), Is.EqualTo(before), "The availability query must not mutate state.");
            int revision = battle.TacticalRevision;
            Assert.That(session.TryNavalArrivalContact(boarding, out reason), Is.True, reason);
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(battle.BoardingConnection, Is.EqualTo(boarding ? NavalBoardingConnection.Planned :
                enemyHeading == 0f ? NavalBoardingConnection.RamBowToStern :
                enemyHeading == 180f ? NavalBoardingConnection.RamBowToBow : NavalBoardingConnection.RamBowToBroadside));
            Assert.That(battle.NavalActionPoints, Is.Zero);
            Assert.That(battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(battle.PlayerHeadingDegrees, Is.EqualTo(heading).Within(.01f));
            Assert.That(battle.TacticalRevision, Is.GreaterThan(revision));
            Assert.That(battle.LastTacticalMovement.Frames[0].PlayerPositionMeters.X, Is.EqualTo(-100f).Within(.01f));
            Assert.That(battle.LastTacticalMovement.Frames[0].PlayerHeadingDegrees, Is.Zero);
            Assert.That(battle.LastTacticalMovement.Frames.Last().PlayerHeadingDegrees, Is.EqualTo(heading).Within(.01f));
            before = ArrivalState(session);
            Assert.That(session.TryNavalArrivalContact(boarding, out _), Is.False);
            Assert.That(ArrivalState(session), Is.EqualTo(before), "A repeated callback must not settle contact twice.");
        }

        [TestCase(true)] [TestCase(false)]
        public void ArrivalContactCannotSpendAnExhaustedMovementBudget(bool boarding)
        {
            CoreVoyageSession session = Arrival(1, 90f);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            AssertArrivalRejectedWithoutMutation(session, boarding, "剩余 1 行动点");
        }

        [Test]
        public void ArrivalBoardingChoosesTheOppositeParallelHeadingInsteadOfReversing(
            [Values(0f, 90f, 180f, 270f)] float movementHeading,
            [Values(-1f, 1f)] float side)
        {
            CoreVoyageSession session = NewBattle();
            VoyagePoint arrival = Direction(movementHeading, 100f);
            VoyagePoint lateral = Direction(movementHeading + 90f, side * 100f);
            Pose(session, 0f, 0f, movementHeading, arrival.X + lateral.X, arrival.Y + lateral.Y,
                (movementHeading + 180f) % 360f);
            SetPoints(session, 2);
            Assert.That(session.TryAppendNavalMovementStep(arrival, out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            string before = ArrivalState(session);
            Assert.That(session.CanNavalArrivalContact(true, out float heading, out reason), Is.True, reason);
            Assert.That(heading, Is.EqualTo(movementHeading));
            Assert.That(ArrivalState(session), Is.EqualTo(before));
            Assert.That(session.TryNavalArrivalContact(true, out reason), Is.True, reason);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(movementHeading));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
        }

        [Test]
        public void ArrivalRamRechecksEnemyPoseAndCannotReverseTowardAnEnemyNowBehind()
        {
            CoreVoyageSession session = Arrival(2, 90f);
            Pose(session, -100f, 0f, 0f, -200f, 0f, 90f);
            AssertArrivalRejectedWithoutMutation(session, false, "不能反向");
        }

        [TestCase(true, 0f, 100f, 0f, "相邻")]
        [TestCase(false, 0f, 100f, 90f, "相邻")]
        [TestCase(true, -50f, 0f, 0f, "相邻")]
        [TestCase(false, -50f, 0f, 90f, "相邻")]
        [TestCase(true, 100f, 0f, 90f, "相邻")]
        [TestCase(false, 100f, 0f, 90f, "相邻")]
        [TestCase(true, 0f, 0f, 0f, "舷侧相邻")]
        [TestCase(true, 0f, 0f, 180f, "舷侧相邻")]
        public void ArrivalContactRechecksActualGeometryInsteadOfAlignment(
            bool boarding, float enemyX, float enemyY, float enemyHeading, string expectedReason)
        {
            CoreVoyageSession session = Arrival(2, enemyHeading);
            Pose(session, -100f, 0f, 0f, enemyX, enemyY, enemyHeading);
            session.Battle.Alignment = boarding ? NavalBattleAlignment.Parallel : NavalBattleAlignment.EnemyBroadside;
            AssertArrivalRejectedWithoutMutation(session, boarding, expectedReason);
        }

        [Test]
        public void OrdinaryStopDiscardsAllArrivalBudgetAndNextTurnOnlyGrantsOnePoint()
        {
            CoreVoyageSession session = Arrival(4, 90f);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(3));
            string before = ArrivalState(session);
            Assert.That(session.ConfirmNavalArrivalHeading(180f, out _), Is.False);
            Assert.That(ArrivalState(session), Is.EqualTo(before));
            Assert.That(session.ConfirmNavalArrivalHeading(0f, out string reason), Is.True, reason);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.CanRam(out _), Is.False);
            AssertArrivalRejectedWithoutMutation(session, false, "没有等待");
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void SuccessfulArrivalContactDiscardsAnyBudgetBeyondItsOnePointCost(bool boarding)
        {
            CoreVoyageSession session = Arrival(4, 90f);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(3));
            Assert.That(session.TryNavalArrivalContact(boarding, out string reason), Is.True, reason);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
        }

        [TestCase(true, 90f)] [TestCase(true, 270f)]
        [TestCase(false, 0f)] [TestCase(false, 90f)] [TestCase(false, 180f)] [TestCase(false, 270f)]
        public void ArrivalContactMatchesTheExistingDirectContactSettlement(bool boarding, float enemyHeading)
        {
            CoreVoyageSession arrival = Arrival(2, enemyHeading);
            CoreVoyageSession direct = NewBattle();
            Pose(direct, -100f, 0f, boarding ? enemyHeading : 0f, 0f, 0f, enemyHeading);
            direct.Battle.Energy = 0;
            Assert.That(arrival.TryNavalArrivalContact(boarding, out string reason), Is.True, reason);
            Assert.That(boarding ? direct.TryBoard() : direct.TryRam(), Is.True);
            Assert.That(arrival.Hull, Is.EqualTo(direct.Hull));
            Assert.That(arrival.Morale, Is.EqualTo(direct.Morale));
            Assert.That(arrival.Battle.EnemyHull, Is.EqualTo(direct.Battle.EnemyHull));
            Assert.That(arrival.Battle.PlayerBoardingHealth, Is.EqualTo(direct.Battle.PlayerBoardingHealth));
            Assert.That(arrival.Battle.EnemyBoardingHealth, Is.EqualTo(direct.Battle.EnemyBoardingHealth));
            Assert.That(arrival.Battle.Energy, Is.EqualTo(direct.Battle.Energy));
            Assert.That(arrival.Battle.BoardingConnection, Is.EqualTo(direct.Battle.BoardingConnection));
            Assert.That(arrival.Battle.BoardingApproach, Is.EqualTo(direct.Battle.BoardingApproach));
            Assert.That(arrival.Battle.Hand, Is.EqualTo(direct.Battle.Hand));
        }

        [Test]
        public void LethalArrivalRamSettlesOnceWithoutEnergyAndClearsPendingBudget()
        {
            CoreVoyageSession session = Arrival(4, 90f);
            CoreBattleState battle = session.Battle;
            battle.EnemyHull = 1;
            Assert.That(session.TryNavalArrivalContact(false, out string reason), Is.True, reason);
            Assert.That(session.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(battle.EnemyHull, Is.Zero);
            Assert.That(battle.Energy, Is.Zero);
            Assert.That(battle.NavalActionPoints, Is.Zero);
            Assert.That(battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(battle.NavalAwaitingHeadingSelection, Is.False);
            string before = ArrivalState(session);
            Assert.That(session.TryNavalArrivalContact(false, out _), Is.False);
            Assert.That(ArrivalState(session), Is.EqualTo(before));
        }

        [Test]
        public void ArrivalContactDoesNotResolveAnAlreadyDefeatedBattleDuringItsFailurePath()
        {
            CoreVoyageSession session = Arrival(2, 0f);
            session.Battle.EnemyHull = 0;
            AssertArrivalRejectedWithoutMutation(session, true, "战斗已结束");
        }

        [Test]
        public void GuidedMovementCannotSkipItsNormalHeadingAndCardLessonsUsingArrivalContact()
        {
            CoreVoyageSession session = CoreGuidedBossScriptTests.StartBoss();
            CoreGuidedBossScriptTests.Reach(session, GuidedBossBattleStep.CourseRamMove);
            SetPoints(session, 2);
            Assert.That(session.TryAppendNavalMovementStep(session.GetNavalLegalNextSteps()[0], out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            AssertArrivalRejectedWithoutMutation(session, false, "当前教学动作");
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamMove));
            Assert.That(session.ConfirmNavalArrivalHeading(0f, out reason), Is.True, reason);
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamAction));
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
        }

        private static VoyagePoint Direction(float heading, float distance)
        {
            double radians = heading * Math.PI / 180d;
            return new VoyagePoint((float)Math.Round(Math.Cos(radians)) * distance,
                (float)Math.Round(Math.Sin(radians)) * distance);
        }
        private static CoreVoyageSession Arrival(int points, float enemyHeading)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, enemyHeading);
            SetPoints(session, points);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-100f, 0f), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            session.Battle.Energy = 0;
            return session;
        }

        private static void AssertArrivalRejectedWithoutMutation(CoreVoyageSession session, bool boarding, string expectedReason)
        {
            string before = ArrivalState(session);
            Assert.That(session.CanNavalArrivalContact(boarding, out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain(expectedReason));
            Assert.That(ArrivalState(session), Is.EqualTo(before));
            Assert.That(session.TryNavalArrivalContact(boarding, out reason), Is.False);
            Assert.That(reason, Does.Contain(expectedReason));
            Assert.That(ArrivalState(session), Is.EqualTo(before));
        }

        private static string ArrivalState(CoreVoyageSession session)
        {
            CoreBattleState b = session.Battle;
            return string.Join("|", new object[] { session.Phase, session.TrainingOutcome, session.GuidedBossStep,
                session.Hull, session.Morale, session.Cargo.Powder, session.Cargo.Cannonballs, session.Cargo.Wood,
                session.Cargo.Medicine, b.Stage, b.Turn, b.Energy, b.NavalActionPoints, b.NavalArrivalActionPoints,
                b.NavalAwaitingHeadingSelection, b.PlayerPositionMeters.X, b.PlayerPositionMeters.Y,
                b.EnemyPositionMeters.X, b.EnemyPositionMeters.Y, b.PlayerHeadingDegrees, b.EnemyHeadingDegrees,
                b.EnemyHull, b.PlayerBoardingHealth, b.EnemyBoardingHealth, b.PlayerBlock, b.EnemyBlock,
                b.Alignment, b.TacticalRevision, b.LastAction, string.Join(",", b.Hand), string.Join(",", b.DrawPile),
                string.Join(",", b.DiscardPile) });
        }

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            return session;
        }

        private static void Pose(CoreVoyageSession session, float px, float py, float ph, float ex, float ey, float eh)
        {
            MethodInfo method = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(session.Battle, new object[] { true, new VoyagePoint(px, py), ph });
            method.Invoke(session.Battle, new object[] { false, new VoyagePoint(ex, ey), eh });
        }
        private static void SetPoints(CoreVoyageSession session, int points) =>
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, points);
    }
}
