using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalTacticalAttackRangeGraphicTests
    {
        private GameObject fixture;
        private NavalTacticalBoardView board;
        private NavalTacticalAttackRangeGraphic graphic;

        [SetUp]
        public void SetUp()
        {
            fixture = new GameObject("Attack range fixture", typeof(RectTransform), typeof(Canvas));
            var root = (RectTransform)fixture.transform;
            root.sizeDelta = new Vector2(1920f, 1080f);
            RectTransform boardRect = NewRect("Board", root);
            board = boardRect.gameObject.AddComponent<NavalTacticalBoardView>();
            board.Configure(new CoreBattleState { DistanceMeters = 600f }, NewRect("Player", root), NewRect("Enemy", root),
                NewRect("Player hull", root), NewRect("Enemy hull", root), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            graphic = NewRect("Attack range", boardRect).gameObject.AddComponent<NavalTacticalAttackRangeGraphic>();
        }

        [TearDown]
        public void TearDown()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
        }

        [TestCase(600f, 180f)]
        [TestCase(600f, 35f)]
        [TestCase(2400f, 135f)]
        public void RangeCells_FollowTheWorldGridWhenTheCameraRotatesAndZooms(float distance, float bearing)
        {
            float radians = bearing * Mathf.Deg2Rad;
            var enemy = new VoyagePoint(200f, -100f);
            board.ApplyFrame(new CoreNavalTacticalFrame(
                new VoyagePoint(enemy.X + Mathf.Cos(radians) * distance, enemy.Y + Mathf.Sin(radians) * distance), enemy, 0f, 180f));
            var cell = new VoyagePoint(100f, 200f);
            graphic.Configure(board, new[] { cell });
            Mesh mesh = PopulateMesh();
            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(4), "A cell needs both a fill and a visible outline.");
                Vector3[] vertices = mesh.vertices;
                Vector2 centre = Vector2.zero;
                for (int index = 0; index < 4; index++) centre += (Vector2)vertices[index] * .25f;
                Assert.That(Vector2.Distance(centre, board.WorldToBoard(cell)), Is.LessThan(.005f));
                var corner = new VoyagePoint(54f, 154f);
                Assert.That(Vector2.Distance(vertices[0], board.WorldToBoard(corner)), Is.LessThan(.005f));
                Assert.That(Vector2.Distance(vertices[0], vertices[1]), Is.EqualTo(board.CellSizePixels * .92f).Within(.005f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void RangeCells_UseTransparentRedFillWithStrongerRedBordersAndNeverBlockInput()
        {
            Assert.That(graphic.raycastTarget, Is.False);
            graphic.Configure(board, new[] { new VoyagePoint(0f, 0f) });
            Mesh mesh = PopulateMesh();
            try
            {
                Color fill = mesh.colors[0], border = mesh.colors[4];
                Assert.That(fill.r, Is.GreaterThan(fill.g * 2f));
                Assert.That(fill.r, Is.GreaterThan(fill.b * 2f));
                Assert.That(fill.a, Is.InRange(.15f, .4f));
                Assert.That(border.r, Is.GreaterThan(border.g * 2f));
                Assert.That(border.a, Is.GreaterThan(.75f));
                Assert.That(border.a, Is.GreaterThan(fill.a));
                Assert.That(graphic.raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Reconfigure_CopiesTheSourceAndClearsPreviousCardCells()
        {
            var source = new List<VoyagePoint> { new VoyagePoint(100f, 200f), new VoyagePoint(200f, 200f) };
            graphic.Configure(board, source);
            source.Clear();
            Assert.That(graphic.Cells.Count, Is.EqualTo(2));
            graphic.Configure(board, new[] { new VoyagePoint(300f, 0f) });
            Assert.That(graphic.Cells.Count, Is.EqualTo(1));
            Assert.That(graphic.Cells[0].X, Is.EqualTo(300f));
            graphic.Configure(board, null);
            Assert.That(graphic.Cells, Is.Empty);
            Mesh mesh = PopulateMesh();
            try { Assert.That(mesh.vertexCount, Is.Zero, "Leaving the card must remove every attack cell."); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(10000f, 0f)]
        [TestCase(10000f, 35f)]
        [TestCase(10000f, 135f)]
        [TestCase(1000000f, 35f)]
        public void UnlimitedRange_AtDistantZoomStaysBelowCanvasVertexLimitsAndLeavesThePlayerCellClear(float distance, float bearing)
        {
            float radians = bearing * Mathf.Deg2Rad;
            var player = new VoyagePoint(0f, 0f);
            board.ApplyFrame(new CoreNavalTacticalFrame(player,
                new VoyagePoint(Mathf.Cos(radians) * distance, Mathf.Sin(radians) * distance), 0f, 180f));
            graphic.ConfigureUnlimited(board, player);
            Assert.That(graphic.IsUnlimitedPreview, Is.True);
            Assert.That(graphic.Cells, Is.Empty, "Unlimited preview must avoid storing every visible cell.");
            Assert.That(graphic.raycastTarget, Is.False);
            Mesh mesh = PopulateMesh();
            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(16));
                Assert.That(mesh.vertexCount, Is.LessThan(65000), "A far-away target must never overflow the UGUI mesh.");
                Vector2 playerScreen = board.WorldToBoard(player);
                Vector3[] vertices = mesh.vertices;
                Color[] colors = mesh.colors;
                int[] triangles = mesh.triangles;
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    if (colors[triangles[index]].a > .5f) continue;
                    Vector2 first = vertices[triangles[index]], second = vertices[triangles[index + 1]], third = vertices[triangles[index + 2]];
                    Assert.That(PointStrictlyInsideTriangle(playerScreen, first, second, third), Is.False,
                        "No red filled triangle may cover the player's own grid cell.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            graphic.Configure(board, null);
            Assert.That(graphic.IsUnlimitedPreview, Is.False);
        }

        private static bool PointStrictlyInsideTriangle(Vector2 point, Vector2 first, Vector2 second, Vector2 third)
        {
            float a = Cross(second - first, point - first);
            float b = Cross(third - second, point - second);
            float c = Cross(first - third, point - third);
            return (a > .0001f && b > .0001f && c > .0001f) || (a < -.0001f && b < -.0001f && c < -.0001f);
        }

        private static float Cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;

        [Test]
        public void UnboundGraphic_ProducesNoMesh()
        {
            graphic.Configure(null, new[] { new VoyagePoint(0f, 0f) });
            Mesh mesh = PopulateMesh();
            try { Assert.That(mesh.vertexCount, Is.Zero); }
            finally { Object.DestroyImmediate(mesh); }
        }

        private Mesh PopulateMesh()
        {
            var mesh = new Mesh();
            using (var helper = new VertexHelper())
            {
                typeof(NavalTacticalAttackRangeGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }
    }
}
