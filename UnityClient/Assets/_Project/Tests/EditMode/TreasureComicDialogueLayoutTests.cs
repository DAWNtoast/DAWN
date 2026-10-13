using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class TreasureComicDialogueLayoutTests
    {
        [TestCase("weathered", 0, 5, 1920, 1080)]
        [TestCase("weathered", 1, 6, 1920, 1080)]
        [TestCase("gilded", 0, 6, 1920, 1080)]
        [TestCase("gilded", 1, 6, 1920, 1080)]
        [TestCase("moonlit", 0, 5, 1920, 1080)]
        [TestCase("moonlit", 1, 5, 1920, 1080)]
        [TestCase("weathered", 0, 5, 1280, 960)]
        [TestCase("weathered", 1, 6, 1280, 960)]
        [TestCase("gilded", 0, 6, 1280, 960)]
        [TestCase("gilded", 1, 6, 1280, 960)]
        [TestCase("moonlit", 0, 5, 1280, 960)]
        [TestCase("moonlit", 1, 5, 1280, 960)]
        public void EveryPage_KeepsCompleteArtisticDialogueInsideItsAuthoredBoxes(
            string rankId, int pageIndex, int expectedBalloons, int width, int height)
        {
            GameObject host = new GameObject("Treasure Dialogue Layout Audit", typeof(RectTransform), typeof(Canvas));
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)host.transform).sizeDelta = new Vector2(width, height);
                GameObject pageHost = new GameObject("Treasure Story Comic Page", typeof(RectTransform), typeof(ComicPageGraphic));
                pageHost.transform.SetParent(host.transform, false);
                ComicPageGraphic page = pageHost.GetComponent<ComicPageGraphic>();
                page.rectTransform.anchorMin = Vector2.zero;
                page.rectTransform.anchorMax = Vector2.one;
                page.rectTransform.sizeDelta = Vector2.zero;
                page.texture = Resources.Load<Texture2D>(CoreTreasureStoryCatalog.FindChapter(rankId).ImageResources[pageIndex]);
                Assert.That(page.texture, Is.Not.Null, "The test must use the shipped page, not a substitute canvas.");
                page.SetPanel(new Rect(0f, 0f, 1f, 1f), false);
                var authored = TreasureComicDialogueCatalog.GetPage(rankId, pageIndex);
                Assert.That(authored.Count, Is.EqualTo(expectedBalloons), "Each authored speech balloon and caption needs dialogue.");
                TreasureComicDialogueLayout layout = pageHost.AddComponent<TreasureComicDialogueLayout>();
                layout.Configure(page, authored);
                Canvas.ForceUpdateCanvases();
                layout.RefreshLayout();
                Canvas.ForceUpdateCanvases();

                Assert.That(page.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                    "Complete treasure pages keep edge dialogue visible, including in a 4:3 window.");
                Text[] labels = host.GetComponentsInChildren<Text>(true);
                Assert.That(labels.Length, Is.EqualTo(expectedBalloons), "No separate title, footer, or extra labels belong above this comic.");
                for (int index = 0; index < authored.Count; index++)
                {
                    Text label = labels.Single(item => item.name == "Treasure Comic Dialogue " + (index + 1));
                    string context = rankId + " page " + (pageIndex + 1) + " / " + width + "x" + height + " / " + label.name;
                    Assert.That(label.gameObject.activeInHierarchy, Is.True, context);
                    Assert.That(label.text, Is.EqualTo(authored[index].Text), context);
                    Assert.That(string.IsNullOrWhiteSpace(label.text), Is.False, context);
                    Assert.That(label.font, Is.SameAs(GameTypography.InterfaceFont), context);
                    Assert.That(label.raycastTarget, Is.False, context + " leaves click-to-advance unobstructed.");
                    Assert.That(label.color.a, Is.GreaterThan(0f), context);

                    label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
                    string missing = new string(label.text.Distinct()
                        .Where(character => !char.IsWhiteSpace(character) && !label.font.HasCharacter(character)).ToArray());
                    Assert.That(missing, Is.Empty, context + " must render the Chinese dialogue without missing-glyph boxes.");
                    AssertWithinAuthoredBox(page, label, authored[index].Bounds, context);
                    AssertCompleteGlyphMesh(page, label, authored[index].Bounds, context);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void AssertWithinAuthoredBox(ComicPageGraphic page, Text label, Rect sourceBounds, string context)
        {
            var corners = new Vector3[4];
            label.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = page.rectTransform.InverseTransformPoint(corner);
                Rect pageRect = page.rectTransform.rect;
                Vector2 uv = new Vector2((local.x - pageRect.xMin) / pageRect.width,
                    (local.y - pageRect.yMin) / pageRect.height);
                Assert.That(uv.x, Is.InRange(sourceBounds.xMin - .0001f, sourceBounds.xMax + .0001f), context + " horizontal lettering bounds");
                Assert.That(uv.y, Is.InRange(sourceBounds.yMin - .0001f, sourceBounds.yMax + .0001f), context + " vertical lettering bounds");
                Assert.That(uv.x, Is.InRange(0f, 1f), context + " remains visible horizontally");
                Assert.That(uv.y, Is.InRange(0f, 1f), context + " remains visible vertically");
            }
        }

        private static void AssertCompleteGlyphMesh(ComicPageGraphic page, Text label, Rect sourceBounds, string context)
        {
            Rect rect = label.rectTransform.rect;
            Assert.That(rect.width, Is.GreaterThan(0f), context);
            Assert.That(rect.height, Is.GreaterThan(0f), context);
            TextGenerationSettings settings = label.GetGenerationSettings(rect.size);
            label.cachedTextGenerator.Populate(label.text, settings);
            TextGenerationSettings completeSettings = settings;
            // Overflow must retain the size that the actual constrained rendering selected.
            // Leaving best fit enabled here would recompute a larger, unconstrained font.
            completeSettings.resizeTextForBestFit = false;
            completeSettings.fontSize = settings.resizeTextForBestFit
                ? label.cachedTextGenerator.fontSizeUsedForBestFit
                : settings.fontSize;
            completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
            completeSettings.generateOutOfBounds = true;
            using (var complete = new TextGenerator())
            {
                complete.Populate(label.text, completeSettings);
                Assert.That(complete.vertexCount, Is.GreaterThan(4), context + " must produce visible glyphs.");
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(complete.characterCountVisible),
                    context + " must retain every character, including the last line.");
                Assert.That(label.cachedTextGenerator.vertexCount, Is.EqualTo(complete.vertexCount), context + " must not truncate its glyph mesh.");
                float pixelsPerUnit = label.pixelsPerUnit;
                var rendered = label.cachedTextGenerator.verts;
                Vector2 firstVertex = rendered[0].position / pixelsPerUnit;
                Vector2 rounding = label.PixelAdjustPoint(firstVertex) - firstVertex;
                Rect pageRect = page.rectTransform.rect;
                float horizontalTolerance = 1f / pageRect.width;
                float verticalTolerance = 1f / pageRect.height;
                // Check the actual rendered glyphs against the source artwork's safe writing
                // area. Decorative glyphs may overhang the padded Text rect without leaving
                // their balloon; the authored artwork bounds remain the visibility contract.
                for (int vertex = 0; vertex < rendered.Count; vertex++)
                {
                    Vector3 position = rendered[vertex].position / pixelsPerUnit + new Vector3(rounding.x, rounding.y, 0f);
                    Vector3 local = page.rectTransform.InverseTransformPoint(label.rectTransform.TransformPoint(position));
                    Vector2 uv = new Vector2((local.x - pageRect.xMin) / pageRect.width,
                        (local.y - pageRect.yMin) / pageRect.height);
                    Assert.That(uv.x, Is.InRange(sourceBounds.xMin - horizontalTolerance, sourceBounds.xMax + horizontalTolerance),
                        context + " glyph exceeds the artwork's horizontal writing area.");
                    Assert.That(uv.y, Is.InRange(sourceBounds.yMin - verticalTolerance, sourceBounds.yMax + verticalTolerance),
                        context + " glyph exceeds the artwork's vertical writing area.");
                }
            }
        }
    }
}
