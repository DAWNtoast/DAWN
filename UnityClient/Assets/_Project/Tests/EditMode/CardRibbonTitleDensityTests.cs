using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class CardRibbonTitleDensityTests
    {
        [TestCase(.5f, .76f, 1f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(2f, .76f, 2f)]
        [TestCase(2f, 1f, 2f)]
        [TestCase(2f, 1.5f, 3f)]
        public void TitleUsesScreenDensityWithoutMovingGlyphsOrDroppingUpgradeSuffix(
            float canvasScale, float cardScale, float expectedDensity)
        {
            GameObject host = new GameObject("Ribbon Density", typeof(Canvas));
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = 1f;
                Text title = CreateTitle(host.transform);
                CardRibbonTitleEffect ribbon = title.GetComponent<CardRibbonTitleEffect>();
                int fontSize = title.fontSize;
                List<Vector3> centers;
                Rect canonicalBounds;
                using (var mesh = new VertexHelper())
                {
                    ribbon.ModifyMesh(mesh);
                    centers = GlyphCenters(mesh);
                    canonicalBounds = ribbon.RenderedBounds;
                }

                canvas.scaleFactor = canvasScale;
                title.transform.parent.localScale = Vector3.one * cardScale;
                CardFaceLayout.FitName(title);
                using (var mesh = new VertexHelper())
                {
                    ribbon.ModifyMesh(mesh);
                    Assert.That(title.fontSize, Is.EqualTo(fontSize), "Screen density must not change the chosen logical font size.");
                    Assert.That(ribbon.RenderedPixelsPerUnit, Is.EqualTo(expectedDensity));
                    Assert.That(ribbon.RenderedGlyphCount, Is.EqualTo(title.text.Count(c => !char.IsWhiteSpace(c))));
                    Assert.That(ribbon.FitsAuthoredRibbon, Is.True);
                    List<Vector3> actualCenters = GlyphCenters(mesh);
                    Assert.That(actualCenters.Count, Is.EqualTo(centers.Count));
                    for (int i = 0; i < centers.Count; i++)
                        Assert.That(Vector3.Distance(actualCenters[i], centers[i]), Is.LessThan(.005f), "Glyph " + i + " moved on the ribbon.");
                    Rect bounds = ribbon.RenderedBounds;
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(canonicalBounds.xMin - .01f));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(canonicalBounds.xMax + .01f));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(canonicalBounds.yMin - .01f));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(canonicalBounds.yMax + .01f));
                    AssertAtlasDensity(mesh, title.font, expectedDensity);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void HoverScaleChangeRequestsDenseMeshAndSurvivesFontAtlasGrowth()
        {
            GameObject host = new GameObject("Ribbon Hover Density", typeof(Canvas));
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = 2f;
                Text title = CreateTitle(host.transform);
                CardRibbonTitleEffect ribbon = title.GetComponent<CardRibbonTitleEffect>();
                title.transform.parent.localScale = Vector3.one * .45f;
                using (var mesh = new VertexHelper())
                {
                    ribbon.ModifyMesh(mesh);
                    Assert.That(ribbon.RenderedPixelsPerUnit, Is.EqualTo(1f));
                    title.transform.parent.localScale = Vector3.one;
                    int dirty = 0;
                    title.RegisterDirtyVerticesCallback(() => dirty++);
                    typeof(CardRibbonTitleEffect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ribbon, null);
                    Assert.That(dirty, Is.GreaterThan(0), "A localScale-only hover must rebuild glyph density.");
                    ribbon.ModifyMesh(mesh);
                    AssertAtlasDensity(mesh, title.font, 2f);
                    Rect bounds = ribbon.RenderedBounds;
                    string otherGlyphs = new string(Enumerable.Range(0x4e00, 192).Select(i => (char)i).ToArray());
                    title.font.RequestCharactersInTexture(otherGlyphs, 48, FontStyle.Normal);
                    ribbon.ModifyMesh(mesh);
                    Assert.That(ribbon.RenderedBounds, Is.EqualTo(bounds));
                    Assert.That(ribbon.FitsAuthoredRibbon, Is.True);
                    AssertAtlasDensity(mesh, title.font, 2f);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static Text CreateTitle(Transform root)
        {
            RectTransform card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(root, false);
            Text title = new GameObject("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            title.transform.SetParent(card, false);
            title.rectTransform.sizeDelta = CardFaceLayout.NameSize;
            title.alignment = TextAnchor.MiddleCenter;
            title.text = CardFaceLayout.ReferenceName;
            CardFaceLayout.FitName(title);
            return title;
        }

        private static List<Vector3> GlyphCenters(VertexHelper mesh)
        {
            var result = new List<Vector3>();
            UIVertex first = default, opposite = default;
            for (int i = 0; i + 3 < mesh.currentVertCount; i += 4)
            {
                mesh.PopulateUIVertex(ref first, i);
                mesh.PopulateUIVertex(ref opposite, i + 2);
                if (Vector3.Distance(first.position, opposite.position) > .001f)
                    result.Add((first.position + opposite.position) * .5f);
            }
            return result;
        }

        private static void AssertAtlasDensity(VertexHelper mesh, Font font, float minimumDensity)
        {
            Texture atlas = font.material.mainTexture;
            Vector2 pixels = new Vector2(atlas.width, atlas.height);
            UIVertex first = default, second = default;
            int checkedEdges = 0;
            for (int i = 0; i + 3 < mesh.currentVertCount; i += 4)
            {
                for (int edge = 0; edge < 2; edge++)
                {
                    mesh.PopulateUIVertex(ref first, i + edge);
                    mesh.PopulateUIVertex(ref second, i + edge + 1);
                    float localLength = Vector3.Distance(first.position, second.position);
                    if (localLength < .001f) continue;
                    float texels = Vector2.Scale((Vector2)(first.uv0 - second.uv0), pixels).magnitude;
                    Assert.That(texels / localLength, Is.GreaterThanOrEqualTo(minimumDensity - .02f),
                        "The rendered UVs must reference dense font pixels, not merely a larger quad.");
                    checkedEdges++;
                }
            }
            Assert.That(checkedEdges, Is.GreaterThan(0));
        }
    }
}
