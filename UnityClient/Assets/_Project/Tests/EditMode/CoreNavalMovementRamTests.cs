using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalMovementRamTests
    {
        [TestCase(0f, 0f, NavalBoardingConnection.RamBowToStern)]
        [TestCase(90f, 270f, NavalBoardingConnection.RamBowToBow)]
        [TestCase(180f, 90f, NavalBoardingConnection.RamBowToBroadside)]
        [TestCase(270f, 180f, NavalBoardingConnection.RamBowToBroadside)]
        public void EnteringEnemyCellUsesOneMovementPointAndImmediatelyRams(
            float heading, float enemyHeading, NavalBoardingConnection expected)
        {
            CoreVoyageSession session = NewBattle();
            double radians = heading * Math.PI / 180d;
            Pose(session, (float)Math.Round(Math.Cos(radians)) * -100f,
                (float)Math.Round(Math.Sin(radians)) * -100f, heading, 0f, 0f, enemyHeading);
            Points(session, 1);
            session.Battle.Energy = 0;
            int hull = session.Hull, enemyHull = session.Battle.EnemyHull;
            Append(session, 0f, 0f);
            Assert.That(session.NavalMovementEndsInRam, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(expected));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(expected));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.False);
            Assert.That(session.Hull, Is.LessThan(hull));
            Assert.That(session.Battle.EnemyHull, Is.LessThan(enemyHull));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            AssertArrivalCleared(session);
            string settled = State(session);
            Assert.That(session.CommitNavalMovement(out _, out _), Is.False);
            Assert.That(State(session), Is.EqualTo(settled));
        }

        [TestCase(3)] [TestCase(5)]
        public void MultiStepRouteIncludesTheCollisionInItsBudget(int points)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -300f, 0f, 0f, 0f, 0f, 90f);
            Points(session, points);
            Append(session, -200f, 0f); Append(session, -100f, 0f); Append(session, 0f, 0f);
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(3));
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(NavalBoardingConnection.RamBowToBroadside));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            AssertArrivalCleared(session);
        }

        [Test]
        public void EnemyCellEndsTheRouteAndTruncatingRestoresPlanning()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 3);
            Append(session, -100f, 0f);
            Assert.That(session.GetNavalLegalNextSteps().Any(p => At(p, 0f, 0f)), Is.True);
            Append(session, 0f, 0f);
            Assert.That(session.GetNavalLegalNextSteps(), Is.Empty);
            string planned = State(session);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100f, 0f), out _), Is.False);
            Assert.That(State(session), Is.EqualTo(planned));
            Assert.That(session.TryTruncateNavalMovementPath(1, out string reason), Is.True, reason);
            Assert.That(session.NavalMovementEndsInRam, Is.False);
            Append(session, -100f, 100f);
        }

        [Test]
        public void UndoAndClearRemoveTheRamPreviewWithoutSpendingPoints()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 3);
            Append(session, -100f, 0f); Append(session, 0f, 0f);
            Assert.That(session.UndoNavalMovementStep(), Is.True);
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(1));
            Assert.That(session.NavalMovementEndsInRam, Is.False);
            Append(session, 0f, 0f);
            Assert.That(session.NavalMovementEndsInRam, Is.True);
            session.ClearNavalMovementPath();
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            Assert.That(session.NavalMovementEndsInRam, Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void ReachIncludesEnemyButDoesNotContinueThroughIt()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 3);
            string before = State(session);
            var reachable = session.GetNavalReachableCells();
            Assert.That(reachable.Any(p => At(p, 0f, 0f)), Is.True);
            Assert.That(reachable.Any(p => At(p, 100f, 0f)), Is.False);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [Test]
        public void LongDragOntoEnemyIsLegalButPastEnemyIsAtomicallyRejected()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 3);
            string before = State(session);
            Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(100f, 0f), out _), Is.False);
            Assert.That(State(session), Is.EqualTo(before));
            Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(0f, 0f), out string reason), Is.True, reason);
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(2));
            Assert.That(session.NavalMovementEndsInRam, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void CommitRechecksWhetherTheEnemyActuallyOccupiesTheEndpoint(bool enemyArrives)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, enemyArrives ? 100f : 0f, 0f, 90f);
            Points(session, 2);
            Append(session, -100f, 0f); Append(session, 0f, 0f);
            Assert.That(session.NavalMovementEndsInRam, Is.EqualTo(!enemyArrives));
            Pose(session, -200f, 0f, 0f, enemyArrives ? 0f : 100f, 0f, 90f);
            Assert.That(session.NavalMovementEndsInRam, Is.EqualTo(enemyArrives));
            int hull = session.Hull, enemyHull = session.Battle.EnemyHull;
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(enemyArrives ? NavalBoardingConnection.RamBowToBroadside : NavalBoardingConnection.Planned));
            Assert.That(session.Battle.Stage, Is.EqualTo(enemyArrives ? NavalBattleStage.Boarding : NavalBattleStage.Naval));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.EqualTo(!enemyArrives));
            if (enemyArrives) AssertArrivalCleared(session);
            else
            {
                Assert.That(session.Hull, Is.EqualTo(hull));
                Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void CommitRejectsChangedEnemyObstructionOrInsufficientBudgetBeforeMutating(bool reduceBudget)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -300f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 3);
            Append(session, -200f, 0f); Append(session, -100f, 0f); Append(session, 0f, 0f);
            if (reduceBudget) Points(session, 2);
            else Pose(session, -300f, 0f, 0f, -100f, 0f, 90f);
            string before = State(session);
            Assert.That(session.CommitNavalMovement(out _, out _), Is.False);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(false, 0f, NavalBoardingConnection.RamBowToStern)]
        [TestCase(false, 180f, NavalBoardingConnection.RamBowToBow)]
        [TestCase(true, 90f, NavalBoardingConnection.RamBowToBroadside)]
        public void LethalMovementRamReturnsItsActualConnectionWithoutStartingMelee(
            bool playerDies, float enemyHeading, NavalBoardingConnection expected)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, 0f, 0f, 0f, enemyHeading);
            Points(session, 1);
            if (playerDies) typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            else session.Battle.EnemyHull = 1;
            Append(session, 0f, 0f);
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(expected));
            Assert.That(session.TrainingOutcome, Is.EqualTo(playerDies ? CoreTrainingBattleOutcome.Defeat : CoreTrainingBattleOutcome.Victory));
            Assert.That(session.Battle.Stage, Is.Not.EqualTo(NavalBattleStage.Boarding));
            Assert.That(playerDies ? session.Hull : session.Battle.EnemyHull, Is.Zero);
            AssertArrivalCleared(session);
            string settled = State(session);
            Assert.That(session.CommitNavalMovement(out _, out _), Is.False);
            Assert.That(State(session), Is.EqualTo(settled));
        }

        [Test]
        public void OriginalCommitSignatureAlsoTriggersTheMovementRam()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 1);
            Append(session, 0f, 0f);
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            AssertArrivalCleared(session);
        }

        [Test]
        public void PreparationCannotUseTheEnemyCellToSkipIntoRamCombat()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            Pose(session, -100f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 1);
            string before = State(session);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(0f, 0f), out _), Is.False);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [Test]
        public void TutorialMovementCannotBypassHeadingAndCardLessonsViaRam()
        {
            CoreVoyageSession session = CoreGuidedBossScriptTests.StartBoss();
            CoreGuidedBossScriptTests.Reach(session, GuidedBossBattleStep.CourseRamMove);
            Pose(session, -100f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 1);
            string before = State(session);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(0f, 0f), out _), Is.False);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [Test]
        public void ThunderInterruptsMovementBeforeThePlannedRam()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 2);
            Place(session, CoreNavalFieldKind.Thunder, -1, 0);
            Append(session, -100f, 0f);
            Append(session, 0f, 0f);
            Assert.That(session.NavalMovementFieldPreview.StopsAtThunder, Is.True);
            int enemyHull = session.Battle.EnemyHull;
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(At(session.Battle.PlayerPositionMeters, -100f, 0f), Is.True);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(1));
        }

        [Test]
        public void TurningRamCanceledByTideKeepsTheLastSailedHeading()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, 0f, 0f, 100f, 0f);
            Points(session, 2);
            Place(session, CoreNavalFieldKind.Tide, 0, 0, 1, 0);
            Append(session, 0f, 0f);
            Append(session, 0f, 100f);
            Assert.That(At(session.NavalMovementFieldPreview.FinalPositionMeters, 100f, 0f), Is.True);
            int enemyHull = session.Battle.EnemyHull;
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(At(session.Battle.PlayerPositionMeters, 100f, 0f), Is.True);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f).Within(.01f));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(session.ConfirmNavalArrivalHeading(180f, out _), Is.False);
            Assert.That(session.ConfirmNavalArrivalHeading(0f, out _), Is.True);
        }

        [Test]
        public void ImmediateRamDoesNotApplyStartingTideAsMovement()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 1);
            Place(session, CoreNavalFieldKind.Tide, -1, 0, 0, 1);
            Append(session, 0f, 0f);
            Assert.That(session.NavalMovementFieldPreview.ActiveSteps, Is.Zero);
            Assert.That(session.NavalMovementFieldPreview.Events, Is.Empty);
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(NavalBoardingConnection.RamBowToBroadside));
            Assert.That(session.Battle.PlayerPositionMeters.Y, Is.EqualTo(0f).Within(.01f));
        }

        [Test]
        public void LethalFieldStopsMovementBeforeThePlannedRam()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Points(session, 2);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            Place(session, CoreNavalFieldKind.Fire, -1, 0);
            Append(session, -100f, 0f);
            Append(session, 0f, 0f);
            Assert.That(session.NavalMovementFieldPreview.Sinks, Is.True);
            CoreBattleState battle = session.Battle;
            int enemyHull = battle.EnemyHull;
            Assert.That(session.CommitNavalMovement(out string reason, out NavalBoardingConnection connection), Is.True, reason);
            Assert.That(connection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
            Assert.That(battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(At(battle.PlayerPositionMeters, -100f, 0f), Is.True);
            Assert.That(battle.NavalAwaitingHeadingSelection, Is.False);
        }

        [Test]
        public void EnemyCellFieldDoesNotAddOrdinaryMovementDamage()
        {
            CoreVoyageSession route = NewBattle();
            CoreVoyageSession direct = NewBattle();
            foreach (CoreVoyageSession session in new[] { route, direct })
            {
                Pose(session, -100f, 0f, 0f, 0f, 0f, 90f);
                Points(session, 1);
                Place(session, CoreNavalFieldKind.Fire, 0, 0);
            }
            Append(route, 0f, 0f);
            Assert.That(route.NavalMovementFieldPreview.Damage, Is.Zero);
            Assert.That(route.NavalMovementFieldPreview.Events, Is.Empty);
            Assert.That(route.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(direct.TryRam(), Is.True);
            Assert.That(route.Hull, Is.EqualTo(direct.Hull));
            Assert.That(route.Battle.EnemyHull, Is.EqualTo(direct.Battle.EnemyHull));
        }

        private static void Place(CoreVoyageSession session, CoreNavalFieldKind kind, int x, int y, int dx = 0, int dy = 0) =>
            Assert.That(session.PlaceNavalField(kind, true, x, y, false, dx, dy), Is.True);
        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            return session;
        }

        private static void Append(CoreVoyageSession session, float x, float y) =>
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(x, y), out string reason), Is.True, reason);

        private static bool At(VoyagePoint point, float x, float y) =>
            Math.Abs(point.X - x) < .01f && Math.Abs(point.Y - y) < .01f;

        private static void AssertArrivalCleared(CoreVoyageSession session)
        {
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            Assert.That(session.NavalMovementEndsInRam, Is.False);
        }

        private static string State(CoreVoyageSession session) => string.Join("|", new object[]
        {
            session.Phase, session.TrainingOutcome, session.GuidedBossStep, session.Hull, session.Morale,
            session.Battle.Stage, session.Battle.Energy, session.Battle.NavalActionPoints,
            session.Battle.NavalArrivalActionPoints, session.Battle.NavalAwaitingHeadingSelection,
            session.Battle.PlayerPositionMeters.X, session.Battle.PlayerPositionMeters.Y,
            session.Battle.PlayerHeadingDegrees, session.Battle.EnemyPositionMeters.X,
            session.Battle.EnemyPositionMeters.Y, session.Battle.EnemyHeadingDegrees,
            session.Battle.EnemyHull, session.Battle.TacticalRevision, session.Battle.BoardingConnection,
            session.NavalMovementEndsInRam, session.NavalMovementPlannedSteps,
            string.Join(",", session.NavalMovementPath.Select(p => p.PositionMeters.X + ":" + p.PositionMeters.Y))
        });

        private static void Pose(CoreVoyageSession session, float px, float py, float ph, float ex, float ey, float eh)
        {
            MethodInfo method = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(session.Battle, new object[] { true, new VoyagePoint(px, py), ph });
            method.Invoke(session.Battle, new object[] { false, new VoyagePoint(ex, ey), eh });
        }

        private static void Points(CoreVoyageSession session, int points) =>
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, points);
    }
}
