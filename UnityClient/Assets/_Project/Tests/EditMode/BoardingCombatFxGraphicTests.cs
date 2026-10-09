using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingCombatFxGraphicTests
    {
        [TestCase(BoardingCombatFxKind.Dash)]
        [TestCase(BoardingCombatFxKind.Slash)]
        [TestCase(BoardingCombatFxKind.Guard)]
        [TestCase(BoardingCombatFxKind.GuardBreak)]
        [TestCase(BoardingCombatFxKind.Heal)]
        public void BoardingFx_HasFiniteBoundedDeterministicGeometryAndNoGrowingChildHierarchy(BoardingCombatFxKind kind)
        {
            var host = new GameObject("Boarding FX Test", typeof(RectTransform), typeof(BoardingCombatFxGraphic));
            var mesh = new Mesh();
            try
            {
                BoardingCombatFxGraphic graphic = host.GetComponent<BoardingCombatFxGraphic>();
                graphic.Configure(kind, Color.white);
                Assert.That(graphic.Kind, Is.EqualTo(kind));
                Assert.That(graphic.raycastTarget, Is.False);
                int childCount = host.GetComponentsInChildren<Transform>(true).Length;
                foreach (Vector2 size in new[] { new Vector2(640f, 180f), new Vector2(40f, 100f) })
                {
                    graphic.rectTransform.sizeDelta = size;
                    graphic.Progress = .5f;
                    Populate(graphic, mesh);
                    Assert.That(mesh.vertexCount, Is.InRange(3, 4096));
                    Assert.That(mesh.triangles.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(6144));
                    string expected = GeometrySignature(mesh);
                    Populate(graphic, mesh);
                    Assert.That(GeometrySignature(mesh), Is.EqualTo(expected));
                    for (int frame = 1; frame < 20; frame++)
                    {
                        graphic.Progress = frame / 20f;
                        Populate(graphic, mesh);
                        Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(4096));
                        Rect rect = graphic.rectTransform.rect;
                        foreach (Vector3 vertex in mesh.vertices)
                        {
                            Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False);
                            Assert.That(vertex.x, Is.InRange(rect.xMin - .001f, rect.xMax + .001f));
                            Assert.That(vertex.y, Is.InRange(rect.yMin - .001f, rect.yMax + .001f));
                        }
                    }
                }
                Assert.That(host.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(childCount));
                graphic.Progress = 1f;
                Populate(graphic, mesh);
                Assert.That(mesh.vertexCount, Is.Zero, "Finished effects must leave no opaque geometry.");
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(host); }
        }

        [Test]
        public void BoardingFx_InvalidProgressIsNormalizedBeforeDrawing()
        {
            var host = new GameObject("Boarding FX Progress", typeof(RectTransform), typeof(BoardingCombatFxGraphic));
            try
            {
                BoardingCombatFxGraphic graphic = host.GetComponent<BoardingCombatFxGraphic>();
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
                {
                    graphic.Progress = value;
                    Assert.That(graphic.Progress, Is.Zero);
                }
                graphic.Progress = 2f;
                Assert.That(graphic.Progress, Is.EqualTo(1f));
                graphic.Progress = .37f;
                Assert.That(graphic.Progress, Is.EqualTo(.37f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void BoardingFx_DifferentCombatEventsHaveDistinctVisibleGeometry()
        {
            var host = new GameObject("Boarding FX Kinds", typeof(RectTransform), typeof(BoardingCombatFxGraphic));
            var mesh = new Mesh();
            try
            {
                BoardingCombatFxGraphic graphic = host.GetComponent<BoardingCombatFxGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(640f, 180f);
                var signatures = new HashSet<string>();
                foreach (BoardingCombatFxKind kind in new[] { BoardingCombatFxKind.Dash, BoardingCombatFxKind.Slash,
                    BoardingCombatFxKind.Guard, BoardingCombatFxKind.GuardBreak, BoardingCombatFxKind.Heal })
                {
                    graphic.Configure(kind, Color.white);
                    graphic.Progress = .5f;
                    Populate(graphic, mesh);
                    Assert.That(signatures.Add(GeometrySignature(mesh)), Is.True, kind + " must not display another action's shape.");
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(host); }
        }

        private static string GeometrySignature(Mesh mesh) => string.Join(";", mesh.vertices.Select(v =>
            v.x.ToString("R", CultureInfo.InvariantCulture) + "," + v.y.ToString("R", CultureInfo.InvariantCulture))) +
            "|" + string.Join(",", mesh.triangles) + "|" + string.Join(";", mesh.colors32.Select(c => c.r + "," + c.g + "," + c.b + "," + c.a));

        private static void Populate(BoardingCombatFxGraphic graphic, Mesh mesh)
        {
            using (var helper = new VertexHelper())
            {
                typeof(BoardingCombatFxGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
        }
    }
}
