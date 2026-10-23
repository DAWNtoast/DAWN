using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalTacticalBoardViewTests
    {
        private GameObject fixture;
        private RectTransform root, player, enemy;
        private NavalTacticalBoardView board;

        [SetUp]
        public void SetUp()
        {
            fixture = new GameObject("Tactical board fixture", typeof(RectTransform), typeof(Canvas));
            root = fixture.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(1920f, 1080f);
            RectTransform boardRect = NewRect("Board", root);
            board = boardRect.gameObject.AddComponent<NavalTacticalBoardView>();
            player = NewRect("Player", root);
            enemy = NewRect("Enemy", root);
            board.Configure(new CoreBattleState { DistanceMeters = 600f }, player, enemy,
                NewRect("Player hull", root), NewRect("Enemy hull", root),
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        }

        [TearDown]
        public void TearDown()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
        }

        [Test]
        public void Configure_ImmediatelyCentresTheSixCellSeparationAcrossTheScreen()
        {
            Assert.That(board.PlayerScreenPosition.x, Is.EqualTo(-225f).Within(.001f));
            Assert.That(board.EnemyScreenPosition.x, Is.EqualTo(225f).Within(.001f));
            Assert.That(board.CellSizePixels, Is.EqualTo(75f).Within(.001f));
            Assert.That(player.localPosition.x, Is.EqualTo(-225f).Within(.001f));
            Assert.That(enemy.localPosition.x, Is.EqualTo(225f).Within(.001f));
            Assert.That(board.CameraRotationDegrees, Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void InitialShips_OccupyGridCellCentresInsteadOfLineIntersections()
        {
            NavalTacticalGridGraphic grid = board.GetComponentInChildren<NavalTacticalGridGraphic>();
            Assert.That(grid.GridOriginPixels.x, Is.EqualTo(board.CellSizePixels * .5f));
            Assert.That(grid.GridOriginPixels.y, Is.EqualTo(board.CellSizePixels * .5f));
            AssertInCellCentre(grid, player);
            AssertInCellCentre(grid, enemy);
        }

        [TestCase(30f, 180f)]
        [TestCase(60f, 90f)]
        [TestCase(100f, 0f)]
        [TestCase(200f, -90f)]
        [TestCase(320f, 35f)]
        [TestCase(600f, 135f)]
        [TestCase(2400f, -135f)]
        public void EveryRangeAndWorldBearing_KeepsShipsOnOppositeSidesWithExactScale(float distance, float bearing)
        {
            float radians = bearing * Mathf.Deg2Rad;
            var foe = new VoyagePoint(200f, -100f);
            var ally = new VoyagePoint(foe.X + Mathf.Cos(radians) * distance, foe.Y + Mathf.Sin(radians) * distance);
            board.ApplyFrame(new CoreNavalTacticalFrame(ally, foe, 0f, 180f));
            Assert.That(board.PlayerScreenPosition.x, Is.LessThan(0f));
            Assert.That(board.EnemyScreenPosition.x, Is.GreaterThan(0f));
            Assert.That(board.PlayerScreenPosition.x + board.EnemyScreenPosition.x, Is.EqualTo(0f).Within(.002f));
            Assert.That(board.PlayerScreenPosition.y, Is.EqualTo(board.EnemyScreenPosition.y).Within(.002f));
            float expected = distance * board.CellSizePixels / CoreVoyageSession.NavalGridCellMeters;
            Assert.That(Vector2.Distance(board.PlayerScreenPosition, board.EnemyScreenPosition), Is.EqualTo(expected).Within(.005f),
                "Screen framing must not invent a minimum separation or falsify the displayed grid scale.");
            AssertEnemyRemainsWorldPivot();
        }

        [Test]
        public void QuarterOrbit_KeepsEnemyAsWorldPivotAndTurnsCompassWithoutRotatingText()
        {
            for (int step = 0; step <= 12; step++)
            {
                float bearing = 180f - step * 7.5f;
                float radians = bearing * Mathf.Deg2Rad;
                board.ApplyFrame(new CoreNavalTacticalFrame(
                    new VoyagePoint(Mathf.Cos(radians) * 600f, Mathf.Sin(radians) * 600f),
                    new VoyagePoint(0f, 0f), 0f, 180f));
                Assert.That(board.PlayerScreenPosition.x, Is.EqualTo(-225f).Within(.002f));
                Assert.That(board.EnemyScreenPosition.x, Is.EqualTo(225f).Within(.002f));
                AssertEnemyRemainsWorldPivot();
            }
            Assert.That(board.CameraRotationDegrees, Is.EqualTo(90f).Within(.001f));
            var east = (RectTransform)board.transform.Find("Tactical compass/Compass 东");
            var north = (RectTransform)board.transform.Find("Tactical compass/Compass 北");
            Assert.That(east.anchoredPosition.y, Is.GreaterThan(50f));
            Assert.That(north.anchoredPosition.x, Is.LessThan(-50f));
            foreach (Transform child in board.transform.Find("Tactical compass"))
                Assert.That(Quaternion.Angle(child.localRotation, Quaternion.identity), Is.LessThan(.001f));
        }

        [Test]
        public void MovingEnemyByWholeCells_PreservesCellCentresAfterCameraRotation()
        {
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(200f, 500f),
                new VoyagePoint(200f, -100f), 0f, 180f));
            NavalTacticalGridGraphic grid = board.GetComponentInChildren<NavalTacticalGridGraphic>();
            AssertInCellCentre(grid, player);
            AssertInCellCentre(grid, enemy);
        }

        [Test]
        public void BoardingRange_SeparatesHealthReadoutsWithoutChangingShipDistance()
        {
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(-50f, 0f), new VoyagePoint(0f, 0f), 0f, 180f));
            RectTransform allyReadout = root.Find("Player hull") as RectTransform;
            RectTransform enemyReadout = root.Find("Enemy hull") as RectTransform;
            Assert.That(enemyReadout.anchoredPosition.x - allyReadout.anchoredPosition.x, Is.GreaterThanOrEqualTo(300f));
            Assert.That(board.EnemyScreenPosition.x - board.PlayerScreenPosition.x,
                Is.EqualTo(.5f * board.CellSizePixels).Within(.001f));
        }

        [TestCase(0f)]
        [TestCase(35f)]
        [TestCase(90f)]
        [TestCase(145f)]
        public void ClickProjection_AfterOrbitSelectsTheSameWorldCell(float bearing)
        {
            float radians = bearing * Mathf.Deg2Rad;
            var foe = new VoyagePoint(37f, -48f);
            board.ApplyFrame(new CoreNavalTacticalFrame(
                new VoyagePoint(foe.X + Mathf.Cos(radians) * 600f, foe.Y + Mathf.Sin(radians) * 600f), foe, 0f, 180f));
            var expected = new VoyagePoint(100f, 200f);
            // A click anywhere near a cell centre must resolve in world coordinates,
            // even when the camera rotates and the enemy is between grid centres.
            Vector2 local = board.WorldToBoard(new VoyagePoint(expected.X + 12f, expected.Y - 18f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, board.transform.TransformPoint(local));
            Assert.That(board.TryScreenToWorldCell(screen, null, out VoyagePoint actual), Is.True);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(.01f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(.01f));
        }

        [Test]
        public void SeaLandmarks_StayAtTheSameWorldCoordinatesThroughOrbitMovementResizeAndZoom()
        {
            var landmark = new VoyagePoint(140f, -90f);
            Vector2 before = SeaUvAt(landmark);
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(200f, 500f),
                new VoyagePoint(200f, -100f), 90f, 180f));
            Assert.That(Vector2.Distance(SeaUvAt(landmark), before), Is.LessThan(.00001f));
            ((RectTransform)board.transform).sizeDelta = new Vector2(1280f, 720f);
            board.SetVerticalFraming(210f);
            Assert.That(Vector2.Distance(SeaUvAt(landmark), before), Is.LessThan(.00001f));
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(-2200f, -100f),
                new VoyagePoint(200f, -100f), 180f, 180f));
            Assert.That(board.CellSizePixels, Is.LessThan(75f), "The last frame must exercise distance zoom.");
            Assert.That(Vector2.Distance(SeaUvAt(landmark), before), Is.LessThan(.00001f));
        }

        private Vector2 SeaUvAt(VoyagePoint point)
        {
            RawImage sea = board.transform.Find("Tactical world/Shared tactical sea").GetComponent<RawImage>();
            Vector2 local = sea.rectTransform.InverseTransformPoint(board.transform.TransformPoint(board.WorldToBoard(point)));
            Rect rect = sea.rectTransform.rect;
            return new Vector2(sea.uvRect.x + (local.x - rect.xMin) / rect.width * sea.uvRect.width,
                sea.uvRect.y + (local.y - rect.yMin) / rect.height * sea.uvRect.height);
        }

        private void AssertEnemyRemainsWorldPivot()
        {
            Vector2 actualPivot = root.InverseTransformPoint(board.transform.Find("Tactical world").position);
            Assert.That(actualPivot.x, Is.EqualTo(board.EnemyScreenPosition.x).Within(.002f));
            Assert.That(actualPivot.y, Is.EqualTo(board.EnemyScreenPosition.y).Within(.002f));
        }

        private void AssertInCellCentre(NavalTacticalGridGraphic grid, RectTransform ship)
        {
            Vector2 position = grid.rectTransform.InverseTransformPoint(ship.position);
            Vector2 relative = position - grid.GridOriginPixels;
            Assert.That(Mathf.Repeat(relative.x, board.CellSizePixels), Is.EqualTo(board.CellSizePixels * .5f).Within(.005f));
            Assert.That(Mathf.Repeat(relative.y, board.CellSizePixels), Is.EqualTo(board.CellSizePixels * .5f).Within(.005f));
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }
    }
}
