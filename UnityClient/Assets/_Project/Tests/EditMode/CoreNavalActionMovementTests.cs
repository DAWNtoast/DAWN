using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalActionMovementTests
    {
        [Test]
        public void ShipTurns_StartWithOneActionPointAndThreeEnergyWithoutCarryingUnusedPoints()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.NavalActionMovementActive, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(3));
            Play(session, "card_turn_port");
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(3));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(600f));
        }

        [TestCase("card_turn_port")]
        [TestCase("card_turn_starboard")]
        [TestCase("card_turn_about")]
        [TestCase("card_sail_forward")]
        [TestCase("card_full_sail_turn")]
        [TestCase("card_approach")]
        [TestCase("card_retreat")]
        [TestCase("card_circle_enemy")]
        [TestCase("card_parallel_course")]
        public void EveryBasicMovementCard_AtBaseRankAddsOneActionPointAndKeepsPose(string id)
        {
            CoreVoyageSession session = NewBattle();
            CoreNavalTacticalFrame frame = session.Battle.TacticalFrame;
            int revision = session.Battle.TacticalRevision;
            Play(session, id);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(frame.PlayerHeadingDegrees));
            AssertPoint(session.Battle.PlayerPositionMeters, frame.PlayerPositionMeters.X, frame.PlayerPositionMeters.Y);
            Assert.That(session.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(session.GetBattleCard(CardIndex(id)).Description, Does.Contain("获得 1 点行动点"));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void Approach_FirstUseIsFreeAtZeroEnergyAndRepeatCostResetsNextTurn(NavalBattleStage stage)
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Stage = stage;
            session.Battle.Energy = 0;
            int cardIndex = CardIndex("card_approach");
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(new[] { cardIndex, cardIndex });
            int revision = session.Battle.TacticalRevision;

            Assert.That(session.GetBattleCard(cardIndex).Energy, Is.Zero);
            Assert.That(session.BattleCardFaceDescription(session.GetBattleCard(cardIndex), stage),
                Is.EqualTo("获得 1 点行动点。"));
            Assert.That(session.CanPlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(session.GetBattleCardEnergyCost(cardIndex), Is.EqualTo(1));
            Assert.That(session.PlayCard(0), Is.False, "The second copy still pays the ordinary repeat cost.");
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));

            session.Battle.Energy = 1;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(cardIndex), Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [TestCase("card_school_fire_b06")]
        [TestCase("card_school_tide_b05")]
        [TestCase("card_school_thunder_b05")]
        public void SchoolMovementCards_PreserveOtherEffectsButCannotMoveOrTurn(string id)
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Cargo.Powder = session.Cargo.Wood = 30;
            Play(session, id);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            AssertPoint(session.Battle.PlayerPositionMeters, -600f, 0f);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f));
            if (id != "card_school_thunder_b05") Assert.That(session.Battle.PlayerBlock, Is.GreaterThan(0));
            else Assert.That(session.Battle.Schools.ThunderReadyDamage, Is.GreaterThan(0));
        }

        [Test]
        public void AllAuthoredMovementCards_AtBaseRankRenderActionPointsWithoutLegacyTravelInstructions()
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.NavalActionPointGain > 0))
            {
                Assert.That(card.Description, Does.Contain("获得 1 点行动点"), card.Id);
                Assert.That(card.Description, Does.Not.Contain("{movement}").And.Not.Contain("{navalActionPoints}"), card.Id);
                Assert.That(card.Description, Does.Not.Contain("沿船头前进").And.Not.Contain("船头向左转").And.Not.Contain("再将船头转向敌舰"), card.Id);
                Assert.That(session.BattleCardFaceDescription(card, NavalBattleStage.Naval), Does.Contain("获得 1 点行动点"), card.Id);
            }
        }

        [Test]
        public void DefaultOnePoint_OnlyPermitsTheCellDirectlyAhead()
        {
            CoreVoyageSession session = NewBattle();
            var choices = session.GetNavalLegalNextSteps();
            Assert.That(choices.Count, Is.EqualTo(1));
            AssertPoint(choices[0], -500f, 0f);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-600f, 100f), out string reason), Is.False);
            Assert.That(reason, Does.Contain("第一格"));
            Append(session, -500f, 0f);
            Assert.That(session.GetNavalLegalNextSteps(), Is.Empty);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-400f, 0f), out reason), Is.False);
            Assert.That(reason, Does.Contain("行动点不足"));
        }

        [Test]
        public void PlanningUndoAndClear_DoNotMoveShipsOrSpendPoints()
        {
            CoreVoyageSession session = NewBattle();
            Play(session, "card_turn_port");
            int revision = session.Battle.TacticalRevision;
            Append(session, -500f, 0f);
            Append(session, -500f, 100f);
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(2));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            AssertPoint(session.Battle.PlayerPositionMeters, -600f, 0f);
            Assert.That(session.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(session.UndoNavalMovementStep(), Is.True);
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(1));
            session.ClearNavalMovementPath();
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(0));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void UserExample_EastThenNorthCostsTwoAndAllowsEastNorthWest(float heading)
        {
            CoreVoyageSession session = NewBattle();
            Play(session, "card_turn_port");
            Append(session, -500f, 0f);
            Append(session, -500f, 100f);
            CollectionAssert.AreEquivalent(new[] { 0f, 90f, 180f }, session.GetNavalArrivalHeadings());
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            AssertPoint(session.Battle.PlayerPositionMeters, -500f, 100f);
            Assert.That(session.ConfirmNavalArrivalHeading(heading, out reason), Is.True, reason);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(heading).Within(.001f));
        }

        [Test]
        public void UserExample_SouthAtTheSameTargetRequiresFourStepDetour()
        {
            CoreVoyageSession shortRoute = NewBattle();
            Play(shortRoute, "card_turn_port");
            Append(shortRoute, -500f, 0f);
            Append(shortRoute, -500f, 100f);
            Assert.That(shortRoute.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(shortRoute.ConfirmNavalArrivalHeading(270f, out reason), Is.False);
            Assert.That(reason, Does.Contain("不能反向"));

            CoreVoyageSession detour = NewBattle();
            Play(detour, "card_turn_port");
            Play(detour, "card_turn_starboard");
            Append(detour, -500f, 0f);
            Append(detour, -400f, 0f);
            Append(detour, -400f, 100f);
            Assert.That(detour.TryAppendNavalMovementStep(new VoyagePoint(-500f, 100f), out reason), Is.False,
                "Three points cannot pay for the fourth leg required by the southern arrival heading.");
            Play(detour, "card_turn_about");
            Append(detour, -500f, 100f);
            Assert.That(detour.NavalMovementPlannedSteps, Is.EqualTo(4));
            Assert.That(detour.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(detour.ConfirmNavalArrivalHeading(270f, out reason), Is.True, reason);
            AssertPoint(detour.Battle.PlayerPositionMeters, -500f, 100f);
        }

        [Test]
        public void IntermediateRoute_CannotReverseOrSkipAdjacentCells()
        {
            CoreVoyageSession session = NewBattle();
            Play(session, "card_turn_port");
            Append(session, -500f, 0f);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-600f, 0f), out string reason), Is.False);
            Assert.That(reason, Does.Contain("不能直接反向"));
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-300f, 0f), out reason), Is.False);
            Assert.That(reason, Does.Contain("逐格"));
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(1));
        }

        [Test]
        public void CommittingMovement_ClearsAllPointsAndLocksCardsAndTurnUntilHeadingIsConfirmed()
        {
            CoreVoyageSession session = NewBattle();
            Play(session, "card_turn_port");
            Play(session, "card_turn_starboard");
            Append(session, -500f, 0f);
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero, "Unused points are cleared too.");
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            int revision = session.Battle.TacticalRevision;
            int turn = session.Battle.Turn;
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(CardIndex("card_brace"));
            Assert.That(session.CanPlayCard(0), Is.False);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.CommitNavalMovement(out reason), Is.False, "The same route cannot execute twice.");
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            Assert.That(session.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(session.ConfirmNavalArrivalHeading(90f, out reason), Is.True, reason);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.TacticalRevision, Is.GreaterThan(revision), "Heading confirmation publishes its own animation.");
            Assert.That(session.CanPlayCard(0), Is.True);
        }

        [Test]
        public void CardsAndMovement_CanAlternateButEveryNewMoveNeedsFreshPoints()
        {
            CoreVoyageSession session = NewBattle();
            Append(session, -500f, 0f);
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(session.ConfirmNavalArrivalHeading(90f, out reason), Is.True, reason);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-500f, 100f), out reason), Is.False);
            Play(session, "card_sail_forward");
            AssertPoint(session.Battle.PlayerPositionMeters, -500f, 0f);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Append(session, -500f, 100f);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.ConfirmNavalArrivalHeading(0f, out reason), Is.True, reason);
            AssertPoint(session.Battle.PlayerPositionMeters, -500f, 100f);
        }

        [Test]
        public void RouteCannotCrossAnEnemyOrSpendPointsOnAnInvalidAttempt()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.DistanceMeters = 80f;
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(20f, 0f), out string reason), Is.False);
            Assert.That(reason, Does.Contain("敌舰"));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            AssertPoint(session.Battle.PlayerPositionMeters, -80f, 0f);
        }

        [Test]
        public void NavalActionHints_RequirePlannedSailingAndArrivalHeadingInsteadOfCardSteering()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.Battle.LastAction, Does.Contain("准备阶段可部署卡牌并规划航行"));
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DistanceMeters = 100f;
            Assert.That(session.CanBoard(out string boardReason), Is.False);
            Assert.That(boardReason, Does.Contain("舷侧相邻").And.Not.Contain("打出并排行船"));
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { true, session.Battle.PlayerPositionMeters, 90f });
            Assert.That(session.CanRam(out string ramReason), Is.False);
            Assert.That(ramReason, Does.Contain("船头指向").And.Not.Contain("打出"));
        }

        [Test]
        public void Commit_RevalidatesEveryEdgeAgainstChangedEnemyPositionBeforeAnyMovement()
        {
            CoreVoyageSession session = NewBattle();
            Play(session, "card_turn_port");
            Append(session, -500f, 0f);
            Append(session, -400f, 0f);
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { false, new VoyagePoint(-450f, 0f), 180f });
            int revision = session.Battle.TacticalRevision;
            Assert.That(session.CommitNavalMovement(out string reason), Is.False);
            Assert.That(reason, Does.Contain("敌舰"));
            AssertPoint(session.Battle.PlayerPositionMeters, -600f, 0f);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(2));
        }

        [Test]
        public void Commit_RevalidatesAvailablePointsWithoutPartiallyExecutingTheRoute()
        {
            CoreVoyageSession session = NewBattle();
            Play(session, "card_turn_port");
            Append(session, -500f, 0f);
            Append(session, -400f, 0f);
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, 1);
            Assert.That(session.CommitNavalMovement(out string reason), Is.False);
            Assert.That(reason, Does.Contain("行动点不足"));
            AssertPoint(session.Battle.PlayerPositionMeters, -600f, 0f);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
        }

        [Test]
        public void MovementPointsAndEnergy_AreIndependentAndMovingDoesNotSpendEnergy()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Energy = 0;
            Append(session, -500f, 0f);
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(session.ConfirmNavalArrivalHeading(0f, out reason), Is.True, reason);
            Assert.That(session.Battle.Energy, Is.Zero);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(CardIndex("card_full_sail_turn"));
            Assert.That(session.CardUnavailableReason(0), Is.EqualTo("能量不足"));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
        }

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("NAVAL-ACTION-POINT-TEST");
            session.StartVoyage();
            var encounter = new CoreEncounter("action_point_test", "测试敌舰", "海战", "行动点测试", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            return session;
        }

        private static int CardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static void Play(CoreVoyageSession session, string id)
        {
            session.Battle.Energy = 100;
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(CardIndex(id));
            Assert.That(session.PlayCard(0), Is.True, id + ": " + session.CardUnavailableReason(0));
        }

        private static void Append(CoreVoyageSession session, float x, float y) =>
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(x, y), out string reason), Is.True, reason);

        private static void AssertPoint(VoyagePoint point, float x, float y)
        {
            Assert.That(point.X, Is.EqualTo(x).Within(.01f));
            Assert.That(point.Y, Is.EqualTo(y).Within(.01f));
        }
    }
}
