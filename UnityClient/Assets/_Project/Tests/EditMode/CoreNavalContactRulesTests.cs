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
        public void ContactRequiresExactlyOneOrthogonalGridCell(float x, float y, bool legal)
        {
            CoreVoyageSession session = NewBattle();
            float boardingHeading = x == 0f ? 0f : 90f;
            Pose(session, 0f, 0f, boardingHeading, x, y, boardingHeading);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.Energy = 0;
            Assert.That(session.NavalShipsAreOrthogonallyAdjacent, Is.EqualTo(legal));
            Assert.That(session.CanBoard(out _), Is.EqualTo(legal));
            Pose(session, 0f, 0f, (float)(Math.Atan2(y, x) * 180d / Math.PI), x, y, boardingHeading);
            Assert.That(session.CanRam(out _), Is.EqualTo(legal));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [Test]
        public void ParallelShipsBoardFromEitherSideWithSameOrOppositeHeadings(
            [Values(0f, 90f, 180f, 270f)] float heading,
            [Values(false, true)] bool opposite,
            [Values(-1f, 1f)] float side)
        {
            CoreVoyageSession session = NewBattle();
            VoyagePoint offset = Direction(heading + 90f, side * 100f);
            Pose(session, offset.X, offset.Y, heading, 0f, 0f, (heading + (opposite ? 180f : 0f)) % 360f);
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            session.Battle.Energy = 0;
            Assert.That(session.CanBoard(out string reason), Is.True, reason);
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
        }

        [TestCase(90f)] [TestCase(270f)]
        public void AlignmentCannotAllowBoardingWhenTheShipsAreNotParallel(float enemyHeading)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, 0f, -100f, 0f, 0f, 0f, enemyHeading);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.CanBoard(out string reason), Is.False);
            Assert.That(reason, Does.Contain("平行"));
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [Test]
        public void ParallelShipsCannotBoardFromAnAdjacentBowOrSternCell(
            [Values(0f, 90f, 180f, 270f)] float heading,
            [Values(false, true)] bool opposite,
            [Values(-1f, 1f)] float end)
        {
            CoreVoyageSession session = NewBattle();
            VoyagePoint offset = Direction(heading, end * 100f);
            Pose(session, offset.X, offset.Y, heading, 0f, 0f, (heading + (opposite ? 180f : 0f)) % 360f);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.NavalShipsAreOrthogonallyAdjacent, Is.True);
            Assert.That(session.CanBoard(out string reason), Is.False);
            Assert.That(reason, Does.Contain("舷侧相邻"));
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [Test]
        public void RamSettlementUsesTheActualStruckEndAndItsArmorForBothAttackers(
            [Values(false, true)] bool enemyAttacks,
            [Values(0f, 90f, 180f, 270f)] float attackHeading,
            [Values(0f, 90f, 180f, 270f)] float targetHeading)
        {
            CoreVoyageSession session = NewBattle();
            VoyagePoint target = Direction(attackHeading, 100f);
            NavalBoardingConnection expected = attackHeading == targetHeading
                ? NavalBoardingConnection.RamBowToStern
                : (attackHeading + 180f) % 360f == targetHeading
                ? NavalBoardingConnection.RamBowToBow : NavalBoardingConnection.RamBowToBroadside;
            bool hitsBow = expected == NavalBoardingConnection.RamBowToBow;
            if (enemyAttacks)
            {
                session.Battle.Profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider,
                    500, 1, 7, 0, 0, 30, 1f, 1f, 1d, 120f);
                Pose(session, target.X, target.Y, targetHeading, 0f, 0f, attackHeading);
                session.Battle.EnemyBowQuality = 2f;
                session.Battle.PlayerBowQuality = 5f;
                session.Battle.PlayerHullQuality = 1f;
            }
            else
            {
                Pose(session, 0f, 0f, attackHeading, target.X, target.Y, targetHeading);
                session.Battle.PlayerBowQuality = 2f;
                session.Battle.EnemyBowQuality = 5f;
                session.Battle.EnemyHullQuality = 1f;
            }
            session.Battle.Alignment = hitsBow ? NavalBattleAlignment.EnemyBroadside : NavalBattleAlignment.HeadOn;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 999;
            int playerHull = session.Hull, enemyHull = session.Battle.EnemyHull;
            string forecast = session.GetEnemyIntent();
            Assert.That(enemyAttacks ? session.EnemyWillRam : session.CanRam(out _), Is.True);
            Assert.That(enemyAttacks ? session.EnemyRamConnection : session.PlayerRamConnection, Is.EqualTo(expected));
            Assert.That(enemyAttacks ? session.EndBattleTurn() : session.TryRam(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(expected));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(expected == NavalBoardingConnection.RamBowToBroadside
                ? NavalBoardingApproach.Quarter : NavalBoardingApproach.Alongside));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.EqualTo(enemyAttacks));
            int damage = hitsBow ? 7 : 27, recoil = hitsBow ? 28 : 6;
            Assert.That(playerHull - session.Hull, Is.EqualTo(enemyAttacks ? damage : recoil));
            Assert.That(enemyHull - session.Battle.EnemyHull,
                Is.EqualTo(enemyAttacks ? recoil : damage * CoreBattleBalance.PlayerRamDamageMultiplier));
            if (enemyAttacks)
                Assert.That(forecast, Does.Contain("己舰 -" + damage + " / 敌舰 -" + recoil));
        }

        [TestCase(100f, 100f)] [TestCase(0f, 0f)] [TestCase(50f, 0f)]
        [TestCase(99f, 0f)] [TestCase(101f, 0f)] [TestCase(200f, 0f)]
        public void EnemyRamRejectsDiagonalCoincidentAndNonAdjacentPositions(float x, float y)
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider,
                500, 1, 7, 0, 0, 30, 1f, 1f, 1d, 120f);
            Pose(session, x, y, 90f, 0f, 0f, (float)(Math.Atan2(y, x) * 180d / Math.PI));
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            Assert.That(session.EnemyWillRam, Is.False);
            Assert.That(session.GetEnemyIntent(), Does.Not.Contain("冲撞→接舷"));
        }

        [Test]
        public void AimedRamWorksFromAllFourDirectionsAgainstEveryEnemyHeading(
            [Values(0f, 90f, 180f, 270f)] float heading,
            [Values(0f, 90f, 180f, 270f)] float enemyHeading)
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

        [Test]
        public void InvalidRamHeadingCannotBeBypassedByAlignmentOrPriorMovement(
            [Values(90f, 180f, 270f)] float playerHeading,
            [Values(0f, 90f, 180f, 270f)] float enemyHeading)
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
            Pose(session, -200f, 0f, 0f, 0f, 0f, 90f);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-100f, 0f), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.CanBoard(out _), Is.False);
            Assert.That(session.CanRam(out _), Is.False);
            Assert.That(session.ConfirmNavalArrivalHeading(ram ? 0f : 90f, out reason), Is.True, reason);
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

        [TestCase(0f, 180f, true)] [TestCase(180f, 180f, true)]
        [TestCase(90f, 0f, false)] [TestCase(90f, 180f, true)]
        [TestCase(90f, 90f, false)] [TestCase(90f, 270f, false)]
        public void EnemyRamRequiresItsBowToPointAtThePlayer(float playerHeading, float enemyHeading, bool legal)
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
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(playerHeading == 0f
                ? NavalBoardingConnection.RamBowToBow : playerHeading == 180f
                ? NavalBoardingConnection.RamBowToStern : NavalBoardingConnection.RamBowToBroadside));
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

        private static VoyagePoint Direction(float heading, float distance)
        {
            double radians = heading * Math.PI / 180d;
            return new VoyagePoint((float)Math.Round(Math.Cos(radians)) * distance,
                (float)Math.Round(Math.Sin(radians)) * distance);
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
