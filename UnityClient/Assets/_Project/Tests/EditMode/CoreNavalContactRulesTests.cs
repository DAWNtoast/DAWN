using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalContactRulesTests
    {
        [TestCase(100f, 0f, true)] [TestCase(-100f, 0f, true)]
        [TestCase(0f, 100f, true)] [TestCase(0f, -100f, true)]
        [TestCase(100f, 100f, false)] [TestCase(0f, 0f, false)]
        [TestCase(50f, 0f, false)] [TestCase(60f, 0f, false)]
        [TestCase(99f, 0f, false)] [TestCase(101f, 0f, false)]
        [TestCase(150f, 0f, false)] [TestCase(200f, 0f, false)]
        public void BoardingRequiresExactlyOneOrthogonalGridCell(float x, float y, bool legal)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, 0f, 0f, 90f, x, y, 90f);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.Energy = 0;
            Assert.That(session.NavalShipsAreOrthogonallyAdjacent, Is.EqualTo(legal));
            Assert.That(session.CanBoard(out _), Is.EqualTo(legal));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
        public void SameHeadingBoardsWithoutEnergyOrAnAlignmentFlag(float heading)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, heading, 0f, 0f, heading);
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            session.Battle.Energy = 0;
            Assert.That(session.CanBoard(out string reason), Is.True, reason);
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
        }

        [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
        public void AlignmentCannotAllowBoardingWhenTheShipsDoNotShareHeading(float enemyHeading)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, 0f, 0f, 0f, enemyHeading);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.CanBoard(out string reason), Is.False);
            Assert.That(reason, Does.Contain("同向"));
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [TestCase(0f, 90f)] [TestCase(0f, 270f)]
        [TestCase(90f, 0f)] [TestCase(90f, 180f)]
        [TestCase(180f, 90f)] [TestCase(180f, 270f)]
        [TestCase(270f, 0f)] [TestCase(270f, 180f)]
        public void AimedPerpendicularRamUsesOneActionPointAndNoEnergy(float heading, float enemyHeading)
        {
            CoreVoyageSession session = NewBattle();
            float radians = heading * (float)Math.PI / 180f;
            Pose(session, 0f, 0f, heading, (float)Math.Round(Math.Cos(radians)) * 100f,
                (float)Math.Round(Math.Sin(radians)) * 100f, enemyHeading);
            session.Battle.PlayerMovingTowardEnemy = false;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.Energy = 0;
            SetPoints(session, 3);
            session.Battle.EnemyHull = 1; // Lethal ram exposes the action payment before a phase refill.
            Assert.That(session.CanRam(out string reason), Is.True, reason);
            Assert.That(session.TryRam(), Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.EnemyHull, Is.Zero);
        }

        [TestCase(0f, 0f)] [TestCase(0f, 180f)]
        [TestCase(180f, 90f)] [TestCase(90f, 0f)]
        public void InvalidRamHeadingCannotBeBypassedByAlignmentOrPriorMovement(float playerHeading, float enemyHeading)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100f, 0f, playerHeading, 0f, 0f, enemyHeading);
            session.Battle.PlayerMovingTowardEnemy = true;
            session.Battle.Alignment = NavalBattleAlignment.EnemyBroadside;
            Assert.That(session.CanRam(out _), Is.False);
            Assert.That(session.TryRam(), Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void MovingToContactClearsPointsAndRequiresANewPointBeforeConnecting(bool ram)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200f, 0f, 0f, 0f, 0f, ram ? 90f : 0f);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-100f, 0f), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.CanBoard(out _), Is.False);
            Assert.That(session.CanRam(out _), Is.False);
            Assert.That(session.ConfirmNavalArrivalHeading(0f, out reason), Is.True, reason);
            Assert.That(ram ? session.CanRam(out reason) : session.CanBoard(out reason), Is.False);
            Assert.That(reason, Does.Contain("1 行动点"));
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_approach"));
            Assert.That(session.PlayCard(0), Is.True);
            session.Battle.Energy = 0;
            Assert.That(ram ? session.TryRam() : session.TryBoard(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
        }

        [TestCase(0f, 180f, false)] [TestCase(180f, 180f, false)]
        [TestCase(90f, 0f, false)] [TestCase(90f, 180f, true)]
        public void EnemyRamUsesTheSamePerpendicularAimedContactRule(float playerHeading, float enemyHeading, bool legal)
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider,
                500, 1, 7, 0, 0, 30, 1f, 1f, 1d, 120f);
            Pose(session, -100f, 0f, playerHeading, 0f, 0f, enemyHeading);
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            Assert.That(session.EnemyWillRam, Is.EqualTo(legal));
            if (!legal) return;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.True);
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.RamBowToBroadside));
        }

        [Test]
        public void EnemyNavigationEndsOnGridAndTurnsAsideWhenTheNextCellIsOccupied()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                500, 0, 0, 0, 0, 30, 1f, 1f, 0d);
            Pose(session, -600f, 0f, 0f, 0f, 0f, 180f);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.EnemyPositionMeters.X, Is.EqualTo(-200f).Within(.01f));
            for (int turn = 0; turn < 6; turn++)
            {
                session.Battle.PlayerBlock = 999;
                Assert.That(session.EndBattleTurn(), Is.True);
                Assert.That(session.Battle.EnemyPositionMeters.X % 100f, Is.EqualTo(0f).Within(.01f));
                Assert.That(session.Battle.EnemyPositionMeters.Y % 100f, Is.EqualTo(0f).Within(.01f));
                Assert.That(session.Battle.EnemyHeadingDegrees % 90f, Is.EqualTo(0f).Within(.01f));
                Assert.That(session.Battle.DistanceMeters, Is.GreaterThanOrEqualTo(99.99f));
            }
            Assert.That(session.CanRam(out string reason), Is.True, reason);
        }

        [Test]
        public void EnemyRamForecastUsesActualSideArmorDespiteHeadOnMetadata()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider,
                500, 1, 7, 0, 0, 30, 1f, 1f, 1d, 120f);
            Pose(session, -100f, 0f, 90f, 0f, 0f, 180f);
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            session.Battle.PlayerBowQuality = 5f;
            session.Battle.PlayerHullQuality = 1f;
            int before = session.Hull;
            string intent = session.GetEnemyIntent();
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(intent, Does.Contain("己舰 -" + (before - session.Hull) + " /"));
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
