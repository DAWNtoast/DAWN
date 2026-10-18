using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalBoardingApproachTests
    {
        [Test]
        public void FreshBattle_HasAlongsideDefaultsWithoutChangingStrategyPhase()
        {
            CoreVoyageSession session = StartBattle();
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            Assert.That(session.Battle.PlannedBoardingApproach, Is.EqualTo(NavalBoardingApproach.Alongside));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Alongside));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(600f));
        }

        [TestCase(NavalBattleAlignment.HeadOn, NavalBoardingApproach.Alongside)]
        [TestCase(NavalBattleAlignment.EnemyBroadside, NavalBoardingApproach.Alongside)]
        public void ParallelCard_GrantsActionPointWithoutChangingPoseOrApproach(NavalBattleAlignment bearing, NavalBoardingApproach expected)
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.Alignment = bearing;
            session.Battle.DistanceMeters = 100f;
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            int energy = session.Battle.Energy;
            int points = session.Battle.NavalActionPoints;
            Play(session, "card_parallel_course");
            Assert.That(session.Battle.Alignment, Is.EqualTo(bearing));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(100f));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + 1));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy), "The retired parallel identity uses the canonical zero-energy approach card.");
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(session.Battle.PlannedBoardingApproach, Is.EqualTo(expected));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Alongside), "The presentation is latched only at actual boarding.");
        }

        [Test]
        public void RepeatedParallel_PreservesTheEstablishedQuarterApproach()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.Alignment = NavalBattleAlignment.EnemyBroadside;
            session.Battle.PlannedBoardingApproach = NavalBoardingApproach.Quarter;
            Play(session, "card_parallel_course");
            Play(session, "card_parallel_course");
            Assert.That(session.Battle.PlannedBoardingApproach, Is.EqualTo(NavalBoardingApproach.Quarter));
        }

        [TestCase(NavalBattleAlignment.HeadOn)]
        [TestCase(NavalBattleAlignment.Parallel)]
        public void NavigatorCourse_GrantsActionPointWithoutChangingApproach(NavalBattleAlignment initialAlignment)
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.Alignment = initialAlignment;
            session.Battle.PlannedBoardingApproach = NavalBoardingApproach.Alongside;
            Play(session, "card_navigator_course");
            Assert.That(session.Battle.Alignment, Is.EqualTo(initialAlignment));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.PlannedBoardingApproach, Is.EqualTo(NavalBoardingApproach.Alongside));
        }

        [Test]
        public void Boarding_LatchesApproachAndCannotBeChangedByLaterPlanningMetadata()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.Alignment = NavalBattleAlignment.EnemyBroadside;
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.PlannedBoardingApproach = NavalBoardingApproach.Quarter;
            Play(session, "card_parallel_course");
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Quarter));
            session.Battle.PlannedBoardingApproach = NavalBoardingApproach.Alongside;
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Quarter));
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Quarter));
        }

        [TestCase(100f, NavalBattleAlignment.Parallel)]
        [TestCase(40f, NavalBattleAlignment.EnemyBroadside)]
        [TestCase(40f, NavalBattleAlignment.HeadOn)]
        public void ApproachMetadata_DoesNotBypassBoardingGeometry(float distance, NavalBattleAlignment alignment)
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.PlannedBoardingApproach = NavalBoardingApproach.Quarter;
            session.Battle.DistanceMeters = distance;
            session.Battle.Alignment = alignment;
            int energy = session.Battle.Energy;
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Alongside));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
        }

        private static CoreVoyageSession StartBattle()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.SetSeed("BOARDING-APPROACH-TEST");
            session.StartVoyage();
            CoreEncounter encounter = new CoreEncounter("boarding_approach", "测试敌舰", "海战", "接舷构图测试", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, new object[] { encounter, false });
            return session;
        }

        private static CoreVoyageSession StartNavalBattle()
        {
            CoreVoyageSession session = StartBattle();
            Assert.That(session.EndBattleTurn(), Is.True);
            return session;
        }

        private static void Play(CoreVoyageSession session, string cardId)
        {
            int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(index => CoreVoyageSession.CardLibrary[index].Id == cardId);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(card);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
        }
    }
}
