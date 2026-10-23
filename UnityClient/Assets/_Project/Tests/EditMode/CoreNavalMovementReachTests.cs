using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalMovementReachTests
    {
        [Test]
        public void OnePointHighlightsOnlyTheCellAheadWithoutSelectingIt()
        {
            var s = NewBattle(1);
            CollectionAssert.AreEqual(new[] { new VoyagePoint(100, 0) }, s.GetNavalReachableCells());
            Assert.That(s.NavalMovementPlannedSteps, Is.Zero);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [TestCase(2, 4)] [TestCase(3, 11)]
        public void ReachIncludesAllTurnsWithinBudgetAndMergesArrivalDirections(int points, int count)
        {
            var s = NewBattle(points);
            var result = s.GetNavalReachableCells();
            Assert.That(result.Count, Is.EqualTo(count));
            Assert.That(result.Distinct().Count(), Is.EqualTo(count));
            CollectionAssert.Contains(result, new VoyagePoint(100, 100));
            CollectionAssert.Contains(result, new VoyagePoint(100, -100));
            CollectionAssert.DoesNotContain(result, new VoyagePoint(-100, 0));
            CollectionAssert.DoesNotContain(result, new VoyagePoint(0, 0));
            if (points == 3)
                CollectionAssert.AreEquivalent(new[] {
                    new VoyagePoint(100, 0), new VoyagePoint(200, 0), new VoyagePoint(100, 100), new VoyagePoint(100, -100),
                    new VoyagePoint(300, 0), new VoyagePoint(200, 100), new VoyagePoint(200, -100),
                    new VoyagePoint(100, 200), new VoyagePoint(0, 100), new VoyagePoint(100, -200), new VoyagePoint(0, -100),
                }, result);
        }

        [Test]
        public void ReachUsesTranslatedPoseAndActualHeadingRatherThanBoardCoordinates()
        {
            var s = NewBattle(2);
            Pose(s, true, new VoyagePoint(525, -175), 90f);
            CollectionAssert.AreEquivalent(new[] { new VoyagePoint(525, -75), new VoyagePoint(525, 25),
                new VoyagePoint(425, -75), new VoyagePoint(625, -75) }, s.GetNavalReachableCells());
        }

        [Test]
        public void OriginIsReachableOnlyAfterPayingForACompleteLegalLoop()
        {
            var s = NewBattle(3);
            CollectionAssert.DoesNotContain(s.GetNavalReachableCells(), new VoyagePoint(0, 0));
            Points(s, 4);
            CollectionAssert.Contains(s.GetNavalReachableCells(), new VoyagePoint(0, 0));
            CollectionAssert.DoesNotContain(s.GetNavalReachableCells(), new VoyagePoint(-100, 0), "A reverse-adjacent cell still needs a longer detour.");
        }

        [TestCase(100f, 0)] [TestCase(150f, 9)] [TestCase(200f, 9)]
        public void EnemyBlocksEveryCrossingEdgeIncludingBetweenGridCenters(float enemyX, int reachableCount)
        {
            var s = NewBattle(3);
            Pose(s, false, new VoyagePoint(enemyX, 0), 180f);
            var result = s.GetNavalReachableCells();
            Assert.That(result.Count, Is.EqualTo(reachableCount));
            CollectionAssert.DoesNotContain(result, new VoyagePoint(200, 0));
            CollectionAssert.DoesNotContain(result, new VoyagePoint(300, 0));
            if (enemyX > 100) CollectionAssert.Contains(result, new VoyagePoint(200, 100), "Legal routes around the obstacle remain highlighted.");
        }

        [Test]
        public void ReachIgnoresButNeverMutatesAnExistingPreviewOrResources()
        {
            var s = NewBattle(3);
            Append(s, 100, 0); Append(s, 100, 100);
            CoreNavalMovementNode[] before = s.NavalMovementPath.ToArray();
            int revision = s.Battle.TacticalRevision, energy = s.Battle.Energy;
            int powder = s.Cargo.Powder, balls = s.Cargo.Cannonballs;
            Assert.That(s.GetNavalReachableCells().Count, Is.EqualTo(11), "The overlay represents the full move, not only the unspent preview tail.");
            CollectionAssert.AreEqual(before, s.NavalMovementPath);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.Battle.PlayerHeadingDegrees, Is.Zero);
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(s.Cargo.Powder, Is.EqualTo(powder)); Assert.That(s.Cargo.Cannonballs, Is.EqualTo(balls));
        }

        [Test]
        public void AttackOriginsUseTideArrivalWithoutChangingBlueCellsOrLiveState()
        {
            var s = NewFieldBattle(1, 20);
            Field(s, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            Append(s, 100, 0);
            CoreNavalMovementNode[] path = s.NavalMovementPath.ToArray();
            string[] ledger = s.Battle.NavalFields.TriggerLedger.ToArray();
            int revision = s.Battle.TacticalRevision, block = s.Battle.PlayerBlock, energy = s.Battle.Energy;
            long sequence = s.Battle.NavalFields.PresentationSequence;

            CollectionAssert.AreEqual(new[] { new VoyagePoint(200, 0) }, s.GetNavalReachableAttackOrigins());
            CollectionAssert.AreEqual(new[] { new VoyagePoint(200, 0) }, s.GetNavalReachableAttackOrigins());
            CollectionAssert.AreEqual(new[] { new VoyagePoint(100, 0) }, s.GetNavalReachableCells());
            CollectionAssert.AreEqual(path, s.NavalMovementPath);
            CollectionAssert.AreEqual(ledger, s.Battle.NavalFields.TriggerLedger);
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(s.Hull, Is.EqualTo(20));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(block));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.Battle.PlayerHeadingDegrees, Is.Zero);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(1));
            Assert.That(s.Battle.NavalFields.Cells[0].Kind, Is.EqualTo(CoreNavalFieldKind.Tide));
            Assert.That(s.Battle.NavalFields.PresentationSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void AttackOriginsExcludeEnemyCollisionEndpoint()
        {
            var s = NewFieldBattle(2, 20);
            Pose(s, false, new VoyagePoint(100, 0), 180f);
            CollectionAssert.Contains(s.GetNavalReachableCells(), new VoyagePoint(100, 0));
            Assert.That(s.GetNavalReachableAttackOrigins(), Is.Empty);
            Field(s, CoreNavalFieldKind.Blood, 0, 1);
            Assert.That(s.GetNavalReachableAttackOrigins(), Is.Empty, "The terrain-aware search also excludes ram endpoints.");
        }

        [Test]
        public void AttackOriginsExcludeSinkingAndStopAtUntriggeredThunder()
        {
            var s = NewFieldBattle(3, 2);
            Field(s, CoreNavalFieldKind.Fire, 1, 0);
            Assert.That(s.GetNavalReachableAttackOrigins(), Is.Empty);
            Assert.That(s.Hull, Is.EqualTo(2));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);

            s.Battle.NavalFields.Cells.Clear();
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 20);
            Field(s, CoreNavalFieldKind.Thunder, 1, 0);
            CollectionAssert.AreEqual(new[] { new VoyagePoint(100, 0) }, s.GetNavalReachableAttackOrigins());
            Assert.That(s.Hull, Is.EqualTo(20));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
        }

        [Test]
        public void AttackOriginsCanSailThroughTideWhoseStoppingPushWouldSinkTheShip()
        {
            var s = NewFieldBattle(1, 2);
            Field(s, CoreNavalFieldKind.Tide, 1, 0, 0, 1);
            Field(s, CoreNavalFieldKind.Fire, 1, 1);
            Assert.That(s.GetNavalReachableAttackOrigins(), Is.Empty);
            Points(s, 2);
            var origins = s.GetNavalReachableAttackOrigins();
            CollectionAssert.Contains(origins, new VoyagePoint(200, 0));
            CollectionAssert.DoesNotContain(origins, new VoyagePoint(100, 100));
            Assert.That(s.Hull, Is.EqualTo(2));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
        }

        [Test]
        public void AttackOriginsRetainLongerSurvivingDetoursAroundFire()
        {
            var s = NewFieldBattle(5, 3);
            Field(s, CoreNavalFieldKind.Fire, 2, 0);
            Field(s, CoreNavalFieldKind.Fire, 5, 0);
            CollectionAssert.DoesNotContain(s.GetNavalReachableAttackOrigins(), new VoyagePoint(500, 0));
            // Seven steps can bypass the first fire along the next row, leaving
            // enough hull to stop in the second fire. The five-step route sinks.
            Points(s, 7);
            CollectionAssert.Contains(s.GetNavalReachableAttackOrigins(), new VoyagePoint(500, 0));
            Assert.That(s.Hull, Is.EqualTo(3));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
        }

        [Test]
        public void RangeAndDragAreLockedByTutorialAndArrivalSelection()
        {
            var tutorial = CoreGuidedBossScriptTests.StartBoss();
            Assert.That(tutorial.GetNavalReachableCells(), Is.Empty);
            Assert.That(tutorial.TryAppendNavalMovementSegment(new VoyagePoint(-500, 0), out string reason), Is.False);
            Assert.That(reason, Does.Contain("教学"));
            Assert.That(tutorial.TryTruncateNavalMovementPath(0, out reason), Is.False);
            CoreGuidedBossScriptTests.Reach(tutorial, GuidedBossBattleStep.CourseFreeMove);
            Assert.That(tutorial.GetNavalReachableCells().Count, Is.EqualTo(1));
            Assert.That(tutorial.TryAppendNavalMovementSegment(new VoyagePoint(-500, 0), out reason), Is.True, reason);
            Assert.That(tutorial.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(tutorial.GetNavalReachableCells(), Is.Empty);
            Assert.That(tutorial.TryAppendNavalMovementSegment(new VoyagePoint(-400, 0), out reason), Is.False);
            Assert.That(reason, Does.Contain("到达朝向"));
            Assert.That(tutorial.TryTruncateNavalMovementPath(0, out reason), Is.False);
        }

        [Test]
        public void NoPointsPortAndBoardingHaveNoReachableMovementCells()
        {
            Assert.That(new CoreVoyageSession().GetNavalReachableCells(), Is.Empty);
            var s = NewBattle(0);
            Assert.That(s.GetNavalReachableCells(), Is.Empty);
            Points(s, 3); s.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(s.GetNavalReachableCells(), Is.Empty);
            Assert.That(s.TryTruncateNavalMovementPath(0, out _), Is.False);
        }

        [Test]
        public void StraightDragSamplesAddEveryCrossedCellWithoutSpendingOrChoosingTurns()
        {
            var s = NewBattle(4);
            int revision = s.Battle.TacticalRevision;
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out string reason), Is.True, reason);
            CollectionAssert.AreEqual(new[] { new VoyagePoint(0, 0), new VoyagePoint(100, 0), new VoyagePoint(200, 0), new VoyagePoint(300, 0) },
                s.NavalMovementPath.Select(node => node.PositionMeters).ToArray());
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(300, 100), out reason), Is.True, reason);
            Assert.That(s.NavalMovementPlannedSteps, Is.EqualTo(4));
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(4));
            Assert.That(s.Battle.TacticalRevision, Is.EqualTo(revision));
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(300, 100)));
            Assert.That(s.Battle.NavalActionPoints, Is.Zero);
            Assert.That(s.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(s.ConfirmNavalArrivalHeading(270f, out reason), Is.False);
            Assert.That(s.ConfirmNavalArrivalHeading(180f, out reason), Is.True, reason);
        }

        [TestCase(200f, 100f)] [TestCase(150f, 0f)] [TestCase(-100f, 0f)] [TestCase(float.NaN, 0f)]
        public void InvalidDragSegmentNeverFindsAnAlternateRouteOrChangesThePreview(float x, float y)
        {
            var s = NewBattle(3);
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(x, y), out _), Is.False);
            Assert.That(s.NavalMovementPlannedSteps, Is.Zero);
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void OverBudgetAndObstructedDragSegmentsAreAtomic()
        {
            var s = NewBattle(2);
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out string reason), Is.False);
            Assert.That(reason, Does.Contain("行动点不足"));
            Assert.That(s.NavalMovementPlannedSteps, Is.Zero);
            Points(s, 3); Pose(s, false, new VoyagePoint(250, 0), 180f);
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out reason), Is.False);
            Assert.That(reason, Does.Contain("敌舰"));
            Assert.That(s.NavalMovementPlannedSteps, Is.Zero, "Even the two valid prefix cells are not committed by an atomic segment call.");
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(3));
        }

        [Test]
        public void RetracingKeepsTheChosenPrefixAndPermitsANewTurnWithoutMoving()
        {
            var s = NewBattle(3);
            Append(s, 100, 0); Append(s, 100, 100); Append(s, 200, 100);
            Assert.That(s.TryTruncateNavalMovementPath(1, out string reason), Is.True, reason);
            Assert.That(s.NavalMovementPlannedSteps, Is.EqualTo(1));
            Assert.That(s.NavalMovementPath[1].HeadingDegrees, Is.Zero);
            Append(s, 100, -100);
            Assert.That(s.TryTruncateNavalMovementPath(-1, out _), Is.False);
            Assert.That(s.TryTruncateNavalMovementPath(3, out _), Is.False);
            Assert.That(s.NavalMovementPlannedSteps, Is.EqualTo(2));
            Assert.That(s.TryTruncateNavalMovementPath(0, out reason), Is.True, reason);
            Assert.That(s.NavalMovementPlannedSteps, Is.Zero);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(3));
        }

        private static CoreVoyageSession NewBattle(int points)
        {
            var s = new CoreVoyageSession();
            s.StartVoyage();
            var encounter = new CoreEncounter("drag_route_test", "测试敌舰", "海战", "拖动选路", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { encounter, false });
            Pose(s, true, new VoyagePoint(0, 0), 0f);
            Pose(s, false, new VoyagePoint(1000, 0), 180f);
            Points(s, points);
            return s;
        }

        private static void Points(CoreVoyageSession s, int value) =>
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(s.Battle, value);
        private static CoreVoyageSession NewFieldBattle(int points, int hull)
        {
            var s = NewBattle(points);
            s.Battle.Stage = NavalBattleStage.Naval;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, hull);
            typeof(CoreBattleState).GetProperty("PlayerBlock").SetValue(s.Battle, 0);
            return s;
        }
        private static void Field(CoreVoyageSession s, CoreNavalFieldKind kind, int x, int y, int dx = 0, int dy = 0) =>
            s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell { X = x, Y = y, Kind = kind, PlayerOwned = true,
                InstanceId = s.Battle.NavalFields.Cells.Count + 1, ExpiresAtOwnerTurn = 100, DirectionX = dx, DirectionY = dy });
        private static void Pose(CoreVoyageSession s, bool player, VoyagePoint point, float heading) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { player, point, heading });
        private static void Append(CoreVoyageSession s, float x, float y) =>
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(x, y), out string reason), Is.True, reason);
    }
}
