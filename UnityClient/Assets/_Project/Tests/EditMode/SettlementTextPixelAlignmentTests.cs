using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class SettlementTextPixelAlignmentTests
    {
        private GameObject host, cameraHost;
        private RenderTexture target;
        private Camera camera;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (cameraHost != null) Object.DestroyImmediate(cameraHost);
            if (target != null) Object.DestroyImmediate(target);
        }

        [TestCase(1920, 0f)]
        [TestCase(1920, .25f)]
        [TestCase(3840, 0f)]
        [TestCase(3840, .25f)]
        public void OddPixelHeightCentersWithinHalfPixelWithoutBlurringGlyphs(int width, float initialPhase)
        {
            Text label = CreateLabel(width, initialPhase);
            using (var mesh = OddHeightGlyph(label.canvas.scaleFactor))
            {
                List<UIVertex> before = Snapshot(mesh);
                label.GetComponent<SettlementTextCentering>().ModifyMesh(mesh);
                AssertUnchangedGlyphs(before, mesh);
                AssertCenteredOnScreenGrid(label, mesh);
            }
        }

        [TestCase(1920)]
        [TestCase(3840)]
        public void TransformOnlyAnimationRebuildsAndItsFinalFrameReturnsToPixelGrid(int width)
        {
            Text label = CreateLabel(width, .25f);
            SettlementTextCentering effect = label.GetComponent<SettlementTextCentering>();
            using (var mesh = OddHeightGlyph(label.canvas.scaleFactor)) effect.ModifyMesh(mesh);
            int dirties = 0;
            label.RegisterDirtyVerticesCallback(() => dirties++);
            label.transform.parent.localScale = Vector3.one * 1.06f;
            label.rectTransform.anchoredPosition += new Vector2(0f, .37f);
            InvokeLateUpdate(effect);
            Assert.That(dirties, Is.GreaterThan(0), "A transform-only reveal frame must rebuild its pixel correction.");
            using (var mesh = OddHeightGlyph(label.canvas.scaleFactor)) effect.ModifyMesh(mesh);
            dirties = 0;
            label.transform.parent.localScale = Vector3.one;
            InvokeLateUpdate(effect);
            Assert.That(dirties, Is.GreaterThan(0), "The final pulse frame must rebuild even when Text.text stays the same.");
            using (var mesh = OddHeightGlyph(label.canvas.scaleFactor))
            {
                effect.ModifyMesh(mesh);
                AssertCenteredOnScreenGrid(label, mesh);
            }
            dirties = 0;
            InvokeLateUpdate(effect);
            Assert.That(dirties, Is.Zero, "An unchanged settled label must not rebuild every frame.");
            label.text = string.Empty;
            using (var empty = new VertexHelper()) effect.ModifyMesh(empty);
            dirties = 0;
            label.rectTransform.anchoredPosition += Vector2.up;
            InvokeLateUpdate(effect);
            Assert.That(dirties, Is.Zero, "An empty animated label must not repeatedly request a mesh.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void WorldSpaceRotatedTiltedAndMirroredLabelsKeepExactLogicalCentering(int scenario)
        {
            Text label = CreateLabel(1920, .25f);
            if (scenario == 0) label.canvas.renderMode = RenderMode.WorldSpace;
            if (scenario == 1) label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            if (scenario == 2) label.rectTransform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            if (scenario == 3) label.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            using (var mesh = OddHeightGlyph(1f))
            {
                List<UIVertex> before = Snapshot(mesh);
                label.GetComponent<SettlementTextCentering>().ModifyMesh(mesh);
                AssertUnchangedGlyphs(before, mesh);
                Assert.That(VisibleBounds(mesh).center.y, Is.EqualTo(label.rectTransform.rect.center.y).Within(.0001f));
            }
        }

        [TestCase(1920)]
        [TestCase(3840)]
        public void ActualSettlementLabelsKeepTypographyAndFinishWithinHalfPixelOfCenter(int width)
        {
            host = new GameObject("Settlement Pixel Audit");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Canvas canvas = host.GetComponentsInChildren<Canvas>(true).First(value => value.isRootCanvas);
            ConfigureScreen(canvas, width);
            SettlementRevealController reveal = host.GetComponentInChildren<SettlementRevealController>();
            reveal.CompleteImmediately();
            Canvas.ForceUpdateCanvases();
            SettlementTextCentering[] effects = host.GetComponentsInChildren<SettlementTextCentering>()
                .Where(value => value.isActiveAndEnabled && !string.IsNullOrEmpty(value.GetComponent<Text>().text)).ToArray();
            Assert.That(effects.Length, Is.GreaterThan(10), "Exercise the actual settlement labels and controls.");
            foreach (SettlementTextCentering effect in effects)
            {
                Text label = effect.GetComponent<Text>();
                using (var mesh = new VertexHelper())
                {
                    typeof(Text).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null).Invoke(label, new object[] { mesh });
                    List<UIVertex> before = Snapshot(mesh);
                    effect.ModifyMesh(mesh);
                    AssertUnchangedGlyphs(before, mesh);
                    AssertCenteredOnScreenGrid(label, mesh);
                }
            }
        }

        private Text CreateLabel(int width, float initialPhase)
        {
            host = new GameObject("Settlement Pixel Canvas", typeof(Canvas));
            Canvas canvas = host.GetComponent<Canvas>();
            ConfigureScreen(canvas, width);
            RectTransform track = new GameObject("Reveal Track", typeof(RectTransform)).GetComponent<RectTransform>();
            track.SetParent(host.transform, false);
            Text label = new GameObject("Settlement Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(track, false);
            label.rectTransform.sizeDelta = new Vector2(200f, 40f);
            label.rectTransform.anchoredPosition = new Vector2(0f, initialPhase / canvas.scaleFactor);
            label.font = GameTypography.BodyFont;
            label.text = "+1,268";
            label.gameObject.AddComponent<SettlementTextCentering>();
            return label;
        }

        private void ConfigureScreen(Canvas canvas, int width)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            cameraHost = new GameObject("Settlement Pixel Camera", typeof(Camera));
            camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 540f;
            target = new RenderTexture(width, width * 9 / 16, 0);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.pixelPerfect = false;
            canvas.scaleFactor = width / 1920f;
            Canvas.ForceUpdateCanvases();
            Assert.That(canvas.scaleFactor, Is.EqualTo(width / 1920f));
            Assert.That(canvas.pixelRect.width, Is.EqualTo(width).Within(.01f));
        }

        private static VertexHelper OddHeightGlyph(float density)
        {
            var mesh = new VertexHelper();
            var quad = new UIVertex[4];
            Vector2[] points = { new Vector2(-5f, 15f), new Vector2(5f, 15f), new Vector2(5f, -4f), new Vector2(-5f, -4f) };
            for (int i = 0; i < quad.Length; i++)
            {
                quad[i] = UIVertex.simpleVert;
                quad[i].position = points[i] / density;
                quad[i].uv0 = new Vector4(i % 2, i / 2, 0f, 0f);
            }
            mesh.AddUIVertexQuad(quad);
            return mesh;
        }

        private void AssertCenteredOnScreenGrid(Text label, VertexHelper mesh)
        {
            Rect bounds = VisibleBounds(mesh);
            Vector2 screenCenter = ScreenPoint(label, bounds.center);
            Vector2 desiredCenter = ScreenPoint(label, new Vector2(bounds.center.x, label.rectTransform.rect.center.y));
            Assert.That(Mathf.Abs(screenCenter.y - desiredCenter.y), Is.LessThanOrEqualTo(.503f), label.name + " vertical centering");
            UIVertex vertex = default;
            for (int start = 0; start + 3 < mesh.currentVertCount; start += 4)
            {
                mesh.PopulateUIVertex(ref vertex, start);
                Vector3 first = vertex.position;
                mesh.PopulateUIVertex(ref vertex, start + 2);
                if (Mathf.Abs(first.x - vertex.position.x) < .001f || Mathf.Abs(first.y - vertex.position.y) < .001f) continue;
                for (int i = start; i < start + 4; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    float y = ScreenPoint(label, vertex.position).y;
                    Assert.That(Mathf.Abs(y - Mathf.Round(y)), Is.LessThan(.003f), label.name + " glyph edge " + i);
                }
            }
        }

        private Vector2 ScreenPoint(Text label, Vector3 local)
            => RectTransformUtility.WorldToScreenPoint(camera, label.rectTransform.TransformPoint(local));

        private static void AssertUnchangedGlyphs(List<UIVertex> before, VertexHelper mesh)
        {
            Assert.That(mesh.currentVertCount, Is.EqualTo(before.Count));
            UIVertex vertex = default;
            mesh.PopulateUIVertex(ref vertex, 0);
            float translation = vertex.position.y - before[0].position.y;
            for (int i = 0; i < before.Count; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Assert.That(vertex.position.x, Is.EqualTo(before[i].position.x));
                Assert.That(vertex.position.z, Is.EqualTo(before[i].position.z));
                Assert.That(vertex.position.y - before[i].position.y, Is.EqualTo(translation).Within(.0001f));
                Assert.That(vertex.uv0, Is.EqualTo(before[i].uv0));
                Assert.That(vertex.color, Is.EqualTo(before[i].color));
            }
        }

        private static List<UIVertex> Snapshot(VertexHelper mesh)
        {
            var result = new List<UIVertex>();
            UIVertex vertex = default;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                result.Add(vertex);
            }
            return result;
        }

        private static Rect VisibleBounds(VertexHelper mesh)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            List<UIVertex> vertices = Snapshot(mesh);
            for (int start = 0; start + 3 < vertices.Count; start += 4)
            {
                Vector2 first = vertices[start].position, opposite = vertices[start + 2].position;
                if (Mathf.Abs(first.x - opposite.x) < .001f || Mathf.Abs(first.y - opposite.y) < .001f) continue;
                for (int i = start; i < start + 4; i++)
                {
                    min = Vector2.Min(min, vertices[i].position);
                    max = Vector2.Max(max, vertices[i].position);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void InvokeLateUpdate(SettlementTextCentering effect)
            => typeof(SettlementTextCentering).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, null);
    }
}
