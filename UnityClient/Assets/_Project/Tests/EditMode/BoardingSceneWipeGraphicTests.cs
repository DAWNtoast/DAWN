using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingSceneWipeGraphicTests
    {
        [Test]
        public void BoardingWipe_CreatesItsRendererAndSubmitsOpaqueGeometryThroughTheCanvasRebuildPath()
        {
            var canvasHost = new GameObject("Wipe Canvas Contract", typeof(RectTransform), typeof(Canvas));
            var host = new GameObject("Wipe Renderer Contract", typeof(RectTransform), typeof(BoardingSceneWipeGraphic));
            try
            {
                BoardingSceneWipeGraphic graphic = host.GetComponent<BoardingSceneWipeGraphic>();
                // Do not access graphic.canvasRenderer first: its lazy getter
                // can conceal a missing RequireComponent on the authored type.
                CanvasRenderer renderer = host.GetComponent<CanvasRenderer>();
                Assert.That(renderer, Is.Not.Null, "Adding the wipe must install its renderer before the first canvas rebuild.");
                PropertyInfo legacy = typeof(Graphic).GetProperty("useLegacyMeshGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.GetValue(graphic), Is.False, "The wipe explicitly uses the VertexHelper mesh generation contract.");
                Canvas canvas = canvasHost.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasHost.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                host.transform.SetParent(canvasHost.transform, false);
                graphic.rectTransform.anchorMin = Vector2.zero;
                graphic.rectTransform.anchorMax = Vector2.one;
                graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
                graphic.color = new Color(.88f, .84f, .70f, 1f);
                graphic.SetProgress(1f);
                graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                graphic.Rebuild(CanvasUpdate.PreRender);
                Assert.That(graphic.canvas, Is.SameAs(canvas));
                Assert.That(renderer.cull, Is.False);
                Assert.That(renderer.materialCount, Is.EqualTo(1));
                Assert.That(renderer.GetMaterial(0), Is.Not.Null);
                // This is the actual renderer-owned mesh, not a direct call to
                // the protected geometry method as in the corner math tests.
                Mesh rendered = renderer.GetMesh();
                Assert.That(rendered, Is.Not.Null);
                Assert.That(rendered.vertexCount, Is.EqualTo(4));
                Assert.That(rendered.triangles.Length, Is.EqualTo(6));
                foreach (Color32 tint in rendered.colors32)
                {
                    Assert.That(tint.a, Is.EqualTo(255));
                    Assert.That(tint.r, Is.GreaterThan(200));
                    Assert.That(tint.g, Is.GreaterThan(200));
                    Assert.That(tint.b, Is.GreaterThan(150));
                }
                graphic.SetProgress(0f);
                graphic.Rebuild(CanvasUpdate.PreRender);
                rendered = renderer.GetMesh();
                Assert.That(rendered == null ? 0 : rendered.vertexCount, Is.Zero, "Revealing the scene must also clear the submitted renderer mesh.");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(canvasHost);
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1024f, 768f)]
        [TestCase(3440f, 1440f)]
        [TestCase(1920f, 3413f)]
        public void BoardingWipe_FullProgressCoversAllViewportCornersIncludingPortrait(float width, float height)
        {
            var host = new GameObject("Wipe Geometry Test", typeof(RectTransform), typeof(BoardingSceneWipeGraphic));
            var mesh = new Mesh();
            try
            {
                BoardingSceneWipeGraphic graphic = host.GetComponent<BoardingSceneWipeGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(width, height);
                graphic.SetProgress(1f);
                Populate(graphic, mesh);
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(mesh.triangles.Length, Is.EqualTo(6));
                Rect bounds = graphic.rectTransform.rect;
                foreach (Vector2 corner in new[] { new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax),
                    new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax) })
                {
                    bool covered = false;
                    for (int i = 0; i < mesh.triangles.Length; i += 3)
                        covered |= InTriangle(corner, mesh.vertices[mesh.triangles[i]], mesh.vertices[mesh.triangles[i + 1]], mesh.vertices[mesh.triangles[i + 2]]);
                    Assert.That(covered, Is.True, "The view must be fully hidden before swapping scenes; uncovered corner " + corner);
                }
                graphic.SetProgress(0f);
                Populate(graphic, mesh);
                Assert.That(mesh.vertexCount, Is.Zero);
                graphic.SetProgress(float.NaN);
                Assert.That(graphic.Progress, Is.Zero);
                graphic.SetProgress(-1f);
                Assert.That(graphic.Progress, Is.Zero);
                graphic.SetProgress(2f);
                Assert.That(graphic.Progress, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(host); }
        }

        private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(point - b, a - b), d2 = Cross(point - c, b - c), d3 = Cross(point - a, c - a);
            return !((d1 < -.01f || d2 < -.01f || d3 < -.01f) && (d1 > .01f || d2 > .01f || d3 > .01f));
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static void Populate(BoardingSceneWipeGraphic graphic, Mesh mesh)
        {
            using (var helper = new VertexHelper())
            {
                typeof(BoardingSceneWipeGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
        }
    }
}
