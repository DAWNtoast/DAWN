using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class MaintenanceGroundingLayoutTests
    {
        private static IEnumerable<TestCaseData> Ports()
        {
            yield return new TestCaseData(CoreGameplayPreviewPort.BrokenAnchor);
            yield return new TestCaseData(CoreGameplayPreviewPort.WhiteShell);
            yield return new TestCaseData(CoreGameplayPreviewPort.BronzeBell);
            yield return new TestCaseData(CoreGameplayPreviewPort.SaltCrown);
            yield return new TestCaseData(CoreGameplayPreviewPort.TwilightTide);
            yield return new TestCaseData(CoreGameplayPreviewPort.GrayGull);
        }

        [TestCaseSource(nameof(Ports))]
        public void InvestmentPreview_UsesDedicatedBackdropForEveryPort(CoreGameplayPreviewPort port)
        {
            GameObject host = new GameObject("Port Investment Grounding Preview Test");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, port, 0);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                RawImage background = Find<RawImage>(preview, "Maintenance Background");
                Assert.That(background.texture, Is.Not.Null);
                Assert.That(background.texture.name, Does.Contain("port_investment_background_v2"));
                Assert.That(background.rectTransform.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.BackgroundSize));
                Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(PhaseSurfaceLayout.BackgroundPosition));
                Assert.That(background.uvRect, Is.EqualTo(PhaseSurfaceLayout.BackgroundUv(background.texture)));
                Assert.That(background.raycastTarget, Is.False);
                Assert.That(FindTransform(preview, "Maintenance Supply Panel"), Is.Null);
                Assert.That(FindTransform(preview, "Maintenance Modular Ship"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InvestmentLedger_ContainsFiveTiersAndAccessibleControls()
        {
            GameObject host = new GameObject("Port Investment Ledger Preview Test");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance,
                    CoreGameplayPreviewPort.SaltCrown, 0);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                RectTransform panel = Find<RectTransform>(preview, "Port Investment Panel");
                RectTransform summary = Find<RectTransform>(preview, "Port Investment Summary");
                RectTransform ledger = Find<RectTransform>(preview, "Port Investment Ledger");
                Assert.That(summary.rect.width, Is.GreaterThan(500f));
                Assert.That(ledger.rect.width, Is.GreaterThan(700f));
                Assert.That(panel.rect.width, Is.GreaterThan(summary.rect.width + ledger.rect.width));
                ScrollRect plans = Find<ScrollRect>(ledger, "Investment Plan Reading Scroll");
                for (int tier = 1; tier <= 5; tier += 1)
                {
                    RectTransform row = Find<RectTransform>(preview, "Port Investment Tier " + tier);
                    Assert.That(row.parent, Is.SameAs(plans.content));
                    Assert.That(row.rect.height, Is.GreaterThanOrEqualTo(46f));
                    AssertContained(plans.content, row);
                    Assert.That(Find<Text>(preview, "Port Investment Tier Label " + tier).text,
                        Does.Contain("Lv." + tier));
                }
                Assert.That(Find<Button>(preview, "Confirm Port Investment").GetComponentInChildren<Text>().text,
                    Does.Contain("Lv.1"));
                Assert.That(Find<Button>(preview, "Open Port Market").GetComponentInChildren<Text>().text,
                    Does.Contain("港口市场"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static IEnumerable<TestCaseData> ReadingCases()
        {
            foreach (CoreGameplayPreviewPort port in System.Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                for (int level = 0; level < 3; level++)
                    foreach (float scale in new[] { 1f, 2f / 3f }) yield return new TestCaseData(port, level, scale);
        }

        [TestCaseSource(nameof(ReadingCases))]
        public void InvestmentReadingSizes_ShowFullCostsAndKeepActionsOutsideScroll(CoreGameplayPreviewPort port, int level, float scale)
        {
            int? previous = GameReadability.OverrideLevel;
            var host = new GameObject("Investment Reading Layout Test");
            try
            {
                GameReadability.OverrideLevel = level;
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, port, 0);
                // Match final 1080p/720p glyph rasterization after construction:
                // the large-text Lv.5 cost previously lost its last material at 1x.
                Canvas canvas = host.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.scaleFactor = scale;
                RectTransform panel = Find<RectTransform>(host.transform, "Port Investment Panel");
                Canvas.ForceUpdateCanvases();
                foreach (Text text in panel.GetComponentsInChildren<Text>(true))
                {
                    bool heading = text.name.EndsWith("Title", System.StringComparison.Ordinal);
                    bool auxiliary = text.name == "Port Investment Port" || text.name == "Port Investment Ledger Hint" || text.name == "Port Investment Notice";
                    Assert.That(text.fontSize, Is.EqualTo(GameReadability.FontSize(heading ? 32 : auxiliary ? 18 : 22)), text.name);
                    Assert.That(text.resizeTextForBestFit, Is.False, text.name);
                    if (!heading) Assert.That(text.font, Is.SameAs(GameTypography.BodyFont), text.name);
                    Assert.That(text.pixelsPerUnit, Is.EqualTo(scale).Within(.0001f), text.name);
                    AssertReadingTextComplete(text);
                }
                RectTransform ledger = Find<RectTransform>(panel, "Port Investment Ledger");
                Button[] actions = new[] { "Confirm Port Investment", "Open Port Market", "Open Investment Warehouse" }
                    .Select(name => Find<Button>(panel, name)).ToArray();
                Vector3[] positions = actions.Select(button => button.transform.position).ToArray();
                foreach (Button button in actions) AssertContained(ledger, (RectTransform)button.transform);
                foreach (ScrollRect scroll in panel.GetComponentsInChildren<ScrollRect>(true))
                {
                    Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                    Assert.That(scroll.verticalScrollbar, Is.Not.Null);
                    RectTransform last = scroll.content.GetChild(scroll.content.childCount - 1) as RectTransform;
                    foreach (Button button in actions)
                        Assert.That(button.transform.IsChildOf(scroll.content), Is.False, button.name + " must remain reachable while reading");
                    Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                    AssertContained((RectTransform)scroll.verticalScrollbar.transform, scroll.verticalScrollbar.handleRect);
                    // Individual long paragraphs can exceed a viewport. Their last
                    // edge must still be reachable without shrinking the text.
                    var corners = new Vector3[4]; last.GetWorldCorners(corners);
                    float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
                    Assert.That(bottom, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1f), scroll.name);
                    Assert.That(bottom, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 1f), scroll.name);
                }
                for (int i = 0; i < actions.Length; i++) Assert.That(actions[i].transform.position, Is.EqualTo(positions[i]));
                Assert.That(Find<Text>(panel, "Port Investment Notice").text, Does.Contain("本次投入："));
                for (int tier = 1; tier <= 5; tier++) Assert.That(Find<Text>(panel, "Port Investment Tier Label " + tier).text, Does.Contain("Lv." + tier));
            }
            finally { Object.DestroyImmediate(host); GameReadability.OverrideLevel = previous; }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LegacySupplyBoard_UsesReadingScrollAndFixedCheckout(int level)
        {
            int? previous = GameReadability.OverrideLevel;
            var host = new GameObject("Supply Reading Layout Test");
            try
            {
                GameReadability.OverrideLevel = level;
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.GrayGull, 0);
                typeof(CoreGameplayDemoController).GetMethod("BuildMaintenanceFacility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
                RectTransform supply = Find<RectTransform>(host.transform, "Maintenance Supply Panel");
                Canvas.ForceUpdateCanvases();
                ScrollRect scroll = Find<ScrollRect>(supply, "Maintenance Reading Scroll");
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                foreach (Text text in supply.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.resizeTextForBestFit, Is.False, text.name);
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)), text.name);
                    AssertReadingTextComplete(text);
                }
                foreach (string name in new[] { "Clear Supply Order", "Fill Standard Supply Order", "Confirm Supply Purchase" })
                    Assert.That(Find<Button>(supply, name).transform.IsChildOf(scroll.content), Is.False);
            }
            finally { Object.DestroyImmediate(host); GameReadability.OverrideLevel = previous; }
        }

        private static void AssertReadingTextComplete(Text text)
        {
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name);
            TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            using (var visible = new TextGenerator())
            using (var full = new TextGenerator())
            {
                visible.Populate(text.text, settings); settings.verticalOverflow = VerticalWrapMode.Overflow; full.Populate(text.text, settings);
                Assert.That(visible.characterCount, Is.EqualTo(full.characterCount), text.name);
                Assert.That(visible.vertexCount, Is.EqualTo(full.vertexCount), text.name);
                Assert.That(visible.vertexCount, Is.GreaterThanOrEqualTo(4), text.name);
            }
        }

        private static Transform FindTransform(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform item = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
            Assert.That(item, Is.Not.Null, name);
            return item.GetComponent<T>();
        }

        private static void AssertContained(RectTransform parent, RectTransform item)
        {
            Vector3[] corners = new Vector3[4];
            item.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - 1f, parent.rect.xMax + 1f));
                Assert.That(local.y, Is.InRange(parent.rect.yMin - 1f, parent.rect.yMax + 1f));
            }
        }
    }
}
