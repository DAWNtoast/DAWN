using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavalAttackRange_ActionPointChangesRecomputeMovementAndPostMoveAttacksWithoutReselecting()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_brace");
            SetRangePreviewActionPoints(session, 0);
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            var preview = Object.FindFirstObjectByType<NavalAttackRangePreview>();
            var graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            ClickTacticalShipForRoute(input);
            foreach (int points in new[] { 0, 1, 3, 0 })
            {
                SetRangePreviewActionPoints(session, points);
                string unchanged = GuidedBossFingerprint(session);
                yield return null;
                yield return null;
                Assert.That(input.IsShipSelected, Is.True, "Changing AP must not silently deselect the ship.");
                Assert.That(preview.ShipRangePinned, Is.True);
                CollectionAssert.AreEquivalent(session.GetNavalReachableCells().Select(TacticalPointKey),
                    input.ReachableCells.Select(TacticalPointKey));
                AssertAttackCell(graphic, session, 4 + points, 0, true);
                AssertAttackCell(graphic, session, 5 + points, 0, false);
                AssertAttackCell(graphic, session, -5, 0, false);
                AssertAttackCell(graphic, session, 0, 0, false);
                AssertAttackOverlayAtCell(graphic, new VoyagePoint(start.X + 100f, start.Y), points == 0);
                Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged), "Range refresh is read-only.");
                if (points == 3)
                {
                    AssertAttackCell(graphic, session, 3, 3, true);
                    Button cannon = RequiredResponsive<Button>("Battle Card 0");
                    HoverAttackCard(cannon, true);
                    AssertAttackCell(graphic, session, 5, 0, false);
                    AssertAttackOverlayAtCell(graphic, new VoyagePoint(start.X + 100f, start.Y), false);
                    HoverAttackCard(cannon, false);
                    AssertAttackCell(graphic, session, 7, 0, true);
                }
            }
            Assert.That(input.ReachableCells, Is.Empty, "Reducing AP back to zero also retracts blue movement cells.");
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalAttackRange_ApproachCardsKeepSelectionAndRestoreBlueCellsFromPinnedRange()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_approach", "card_approach", "card_chain_shot");
            SetRangePreviewActionPoints(session, 0);
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            var preview = Object.FindFirstObjectByType<NavalAttackRangePreview>();
            var graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            ClickTacticalShipForRoute(input);
            AssertAttackOverlayAtCell(graphic, new VoyagePoint(start.X + 100f, start.Y), true);
            for (int expectedPoints = 1; expectedPoints <= 2; expectedPoints++)
            {
                if (expectedPoints == 2)
                {
                    // Match the retained red pin after movement has ended and movement selection is closed.
                    typeof(NavalTacticalMovementInput).GetMethod("SetShipSelected", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(input, new object[] { false, false });
                    Assert.That(input.IsShipSelected, Is.False);
                    Assert.That(preview.ShipRangePinned, Is.True);
                }
                Assert.That(session.GetBattleCard(session.Battle.Hand[0]).Id, Is.EqualTo("card_approach"));
                yield return WaitTacticalHeadingPointerReady("Battle Card 0");
                ClickAcceptanceButton("Battle Card 0");
                yield return WaitResponsiveBattle(controller);
                yield return null;
                Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(expectedPoints));
                Assert.That(input.IsShipSelected, Is.True, "Playing an AP card refreshes blue cells without a second ship click.");
                Assert.That(preview.ShipRangePinned, Is.True);
                VoyagePoint newlyReachable = new VoyagePoint(start.X + expectedPoints * 100f, start.Y);
                Assert.That(input.ReachableCells.Any(point => TacticalPointsMatch(point, newlyReachable)), Is.True);
                AssertAttackOverlayAtCell(graphic, newlyReachable, false);
                AssertAttackCell(graphic, session, 4 + expectedPoints, 0, true);
            }
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, start);
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalAttackRange_RamDestinationDoesNotBecomeAnExtraCannonOrigin()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            ConfigureCloseNavalTestPose(session.Battle, true);
            SetResponsiveHand(session, "card_chain_shot", "card_brace");
            SetRangePreviewActionPoints(session, 1);
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            var graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            ClickTacticalShipForRoute(input);
            yield return null;
            CollectionAssert.AreEquivalent(new[] { TacticalPointKey(session.Battle.EnemyPositionMeters) },
                input.ReachableCells.Select(TacticalPointKey));
            AssertAttackCell(graphic, session, 4, 0, true);
            AssertAttackCell(graphic, session, 5, 0, false);
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetRangePreviewActionPoints(CoreVoyageSession session, int points) =>
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, points);

        private static void AssertAttackOverlayAtCell(NavalTacticalAttackRangeGraphic graphic, VoyagePoint cell, bool expected)
        {
            var board = Object.FindFirstObjectByType<NavalTacticalBoardView>();
            Vector2 sample = board.WorldToBoard(new VoyagePoint(cell.X + 17f, cell.Y + 11f));
            var mesh = new Mesh();
            try
            {
                using (var helper = new VertexHelper())
                {
                    typeof(NavalTacticalAttackRangeGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(graphic, new object[] { helper });
                    helper.FillMesh(mesh);
                }
                bool covered = false;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector2 a = vertices[triangles[index]], b = vertices[triangles[index + 1]], c = vertices[triangles[index + 2]];
                    float first = AttackRangeCross(b - a, sample - a), second = AttackRangeCross(c - b, sample - b),
                        third = AttackRangeCross(a - c, sample - c);
                    covered |= first > .0001f && second > .0001f && third > .0001f ||
                        first < -.0001f && second < -.0001f && third < -.0001f;
                }
                Assert.That(covered, Is.EqualTo(expected), "Actual red mesh coverage at " + TacticalPointKey(cell));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        private static float AttackRangeCross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
