using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class RamBoardingTransitionTests
    {
        [TestCase(0f, NavalBoardingConnection.RamBowToStern, NavalBoardingApproach.Alongside)]
        [TestCase(180f, NavalBoardingConnection.RamBowToBow, NavalBoardingApproach.Alongside)]
        [TestCase(90f, NavalBoardingConnection.RamBowToBroadside, NavalBoardingApproach.Quarter)]
        [TestCase(270f, NavalBoardingConnection.RamBowToBroadside, NavalBoardingApproach.Quarter)]
        public void NonLethalRam_ConnectsShipsAndStartsMelee(
            float enemyHeading, NavalBoardingConnection connection, NavalBoardingApproach approach)
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, enemyHeading);
            session.Battle.Alignment = NavalBattleAlignment.HeadOn; // Stale metadata cannot determine impact.
            session.Battle.PlayerMovingTowardEnemy = false;
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;

            Assert.That(session.TryRam(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(connection));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(approach));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.GreaterThan(0));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.GreaterThan(0));
            Assert.That(session.Hull, Is.LessThan(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.LessThan(enemyHull));
            Assert.That(session.Battle.Hand.All(card => CoreVoyageSession.CardLibrary[card].IsAllowedInStage(NavalBattleStage.Boarding)), Is.True);
            Assert.That(session.Battle.LastAction, Does.Contain("进入接舷近战"));
            Assert.That(session.CanRam(out _), Is.False);
        }

        private static CoreVoyageSession StartNavalBattle()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.SetSeed("RAM-BOARDING-TRANSITION");
            session.StartVoyage();
            var encounter = new CoreEncounter("ram_boarding", "测试敌舰", "海战", "冲撞接舷测试", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, new object[] { encounter, false });
            Assert.That(session.EndBattleTurn(), Is.True);
            return session;
        }
    }
}

