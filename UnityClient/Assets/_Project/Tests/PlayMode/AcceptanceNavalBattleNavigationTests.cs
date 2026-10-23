using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private static bool AcceptanceNavalMovementNeeded(CoreVoyageSession session) =>
            session.Phase == CoreDemoPhase.Battle && session.Battle.Stage == NavalBattleStage.Naval &&
            !session.NavalCannonTargetInRange && session.Battle.NavalActionPoints > 0 &&
            !session.Battle.NavalAwaitingHeadingSelection && session.GetNavalLegalNextSteps().Count > 0;

        private static double AcceptanceNavalActionPointScore(CoreVoyageSession session, CoreBattleCard card) =>
            card.NavalActionPointGain > 0 && session.Battle.Stage == NavalBattleStage.Naval &&
            !session.NavalCannonTargetInRange && session.Battle.NavalActionPoints == 0
                ? 32d + card.NavalActionPointGain * 4d : 0d;

        // Every mutation below is a real pointer drag or the driver's normal UI click.
        // Query the next legal cells after each drag segment, rather than assuming
        // that an AP card moves the ship or that a turn can reverse its heading.
        private static IEnumerator AdvanceAcceptanceNavalRange(CoreGameplayDemoController controller,
            Func<string, IEnumerator> click)
        {
            CoreVoyageSession session = controller.Session;
            Assert.That(AcceptanceNavalMovementNeeded(session), Is.True);
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            CoreBattleState battle = session.Battle;
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            VoyagePoint start = battle.PlayerPositionMeters;
            int energy = battle.Energy, points = battle.NavalActionPoints;
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            Assert.That(input, Is.Not.Null);
            var drag = new TacticalDragPointer(input);
            for (int step = 0; step < points; step++)
            {
                CoreNavalMovementNode last = session.NavalMovementPath[session.NavalMovementPath.Count - 1];
                if (step > 0 && CoreVoyageSession.NavalCannonInRange(last.PositionMeters, battle.EnemyPositionMeters)) break;
                var legal = session.GetNavalLegalNextSteps();
                if (legal.Count == 0) break;
                VoyagePoint next = legal.OrderBy(point => CoreVoyageSession.NavalGridDistance(point, battle.EnemyPositionMeters))
                    .ThenBy(point => AcceptanceCourseLookahead(last.PositionMeters, point, battle.EnemyPositionMeters))
                    .First();
                drag.DragTo(next);
                yield return null;
                Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(step + 1));
                AssertTacticalPoint(battle.PlayerPositionMeters, start);
                Assert.That(battle.NavalActionPoints, Is.EqualTo(points), "Planning spends no AP.");
            }
            drag.Release();
            yield return null;
            Assert.That(session.NavalMovementPlannedSteps, Is.GreaterThan(0));
            Assert.That(battle.Energy, Is.EqualTo(energy));
            yield return click("Commit Naval Movement");
            if (session.Phase != CoreDemoPhase.Battle || battle.Stage != NavalBattleStage.Naval) yield break;
            if (battle.NavalAwaitingHeadingSelection)
            {
                var headings = session.GetNavalArrivalHeadings();
                Assert.That(headings.Count, Is.GreaterThan(0));
                int choice = Enumerable.Range(0, headings.Count)
                    .OrderBy(index => CoreVoyageSession.NavalGridDistance(
                        AcceptanceCourseForward(battle.PlayerPositionMeters, headings[index]), battle.EnemyPositionMeters))
                    .First();
                string target = "Naval Arrival Heading " + choice;
                yield return WaitTacticalHeadingPointerReady(target);
                yield return click(target);
                Assert.That(battle.NavalAwaitingHeadingSelection, Is.False);
            }
            Assert.That(battle.Energy, Is.EqualTo(energy), "Sailing spends AP, not card energy.");
        }

        private static int AcceptanceCourseLookahead(VoyagePoint previous, VoyagePoint next, VoyagePoint enemy)
        {
            float heading = (float)(Math.Atan2(next.Y - previous.Y, next.X - previous.X) * 180d / Math.PI);
            return new[] { heading, heading + 90f, heading - 90f }
                .Min(angle => CoreVoyageSession.NavalGridDistance(AcceptanceCourseForward(next, angle), enemy));
        }

        private static VoyagePoint AcceptanceCourseForward(VoyagePoint origin, float heading)
        {
            double radians = heading * Math.PI / 180d;
            return new VoyagePoint(origin.X + (float)Math.Round(Math.Cos(radians)) * CoreVoyageSession.NavalGridCellMeters,
                origin.Y + (float)Math.Round(Math.Sin(radians)) * CoreVoyageSession.NavalGridCellMeters);
        }

        [UnityTest]
        public IEnumerator AcceptanceNavalRangeDriver_UsesRealRoutesAndTurnsBackFromAnAwayHeading()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (float heading in new[] { 0f, 180f })
            {
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
                SetNavalFieldTestPose(session, true, 0, 0);
                SetNavalFieldTestPose(session, false, 6, 0);
                typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, heading);
                SetResponsiveHand(session, "card_approach", "card_chain_shot");
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                Assert.That(AcceptanceNavalActionPointScore(session, session.GetBattleCard(session.Battle.Hand[0])), Is.Zero,
                    "Use existing AP before buying more movement.");
                ClickAcceptanceButton("Battle Card 0");
                yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
                int energy = session.Battle.Energy, played = session.Battle.CardsPlayedThisTurn;
                yield return AdvanceAcceptanceNavalRange(controller, name => AcceptanceNavigationTestClick(controller, name));
                Assert.That(session.Battle.NavalActionPoints, Is.Zero);
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(played));
                Assert.That(session.Battle.Energy, Is.EqualTo(energy));
                Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
                Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f));
                if (heading == 0f)
                {
                    AssertTacticalPoint(session.Battle.PlayerPositionMeters, new VoyagePoint(200f, 0f));
                    Assert.That(session.NavalCannonTargetInRange, Is.True);
                }
                else
                {
                    Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(-100f));
                    Assert.That(Math.Abs(session.Battle.PlayerPositionMeters.Y), Is.EqualTo(100f));
                    Assert.That(AcceptanceNavalActionPointScore(session,
                        session.GetBattleCard(ResponsiveCardIndex("card_approach"))), Is.GreaterThan(0d));
                }
                Assert.That(AcceptanceNavalMovementNeeded(session), Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator AcceptanceNavigationTestClick(CoreGameplayDemoController controller, string name)
        {
            ClickAcceptanceButton(name);
            yield return WaitResponsiveBattle(controller);
        }
    }
}
