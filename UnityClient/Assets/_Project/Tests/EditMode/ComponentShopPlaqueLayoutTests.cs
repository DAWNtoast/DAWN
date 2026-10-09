using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentShopPlaqueLayoutTests
    {
        private GameObject host;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void LabelsStayOnInnerTimberAndLeaveTheModelAndPlaqueClickable(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Component Shop Plaque Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, port, 0);
            Texture2D plaqueTexture = RegionalShopFurniture.For(controller.Session.CurrentPort).Atlas;
            RawImage[] plaques = host.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("Component Shop Plaque ") && image.texture == plaqueTexture).ToArray();
            Assert.That(plaques, Is.Not.Empty);
            foreach (RawImage plaque in plaques)
            {
                string id = plaque.name.Substring("Component Shop Plaque ".Length);
                ComponentArtEntry entry = new ComponentArtCatalog().Entries.First(item => item.Id == id);
                Rect safe = ComponentShopDisplayGeometry.For(entry, controller.Session.CurrentPort).WritingArea;
                RectMask2D mask = plaque.GetComponentInChildren<RectMask2D>();
                Assert.That(mask, Is.Not.Null);
                Assert.That(plaque.raycastTarget, Is.True);
                Text[] texts = plaque.GetComponentsInChildren<Text>();
                Assert.That(texts.Length, Is.EqualTo(2));
                ScrollRect scroll = plaque.GetComponentInChildren<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                var viewportCorners = new Vector3[4]; scroll.viewport.GetWorldCorners(viewportCorners);
                foreach (Vector3 corner in viewportCorners)
                {
                    Vector3 local = plaque.transform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(safe.xMin - .1f, safe.xMax + .1f));
                    Assert.That(local.y, Is.InRange(safe.yMin - .1f, safe.yMax + .1f));
                }
                foreach (Text text in texts)
                {
                    Assert.That(text.raycastTarget, Is.False, "Let the physical sign handle hover/clicks.");
                    Assert.That(text.resizeTextForBestFit, Is.False);
                    Assert.That(text.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
                    Assert.That(text.font, Is.SameAs(GameTypography.BodyFont));
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f));
                    Vector3[] corners = new Vector3[4];
                    text.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = scroll.content.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(scroll.content.rect.xMin - .1f, scroll.content.rect.xMax + .1f), text.name);
                        Assert.That(local.y, Is.InRange(scroll.content.rect.yMin - .1f, scroll.content.rect.yMax + .1f), text.name);
                    }
                }
            }
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name.StartsWith("Component Shop Mount") || item.name.StartsWith("Component Shop Merchant")), Is.False);
            foreach (EventTrigger trigger in host.GetComponentsInChildren<EventTrigger>(true).Where(item => item.name.StartsWith("Component Shop Offer ")))
            {
                // Offer IDs differ from component IDs; exclude the similarly named contact shadows.
                var plaque = trigger.GetComponentsInChildren<RawImage>().Single(item =>
                    item.name.StartsWith("Component Shop Plaque ") && item.texture == plaqueTexture);
                var beforePosition = plaque.rectTransform.position;
                var beforeScale = plaque.rectTransform.lossyScale;
                var beforeColor = plaque.color;
                trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
                Assert.That(plaque.color, Is.EqualTo(beforeColor), "Offer hover must not paint a coloured plaque highlight.");
                Assert.That(plaque.rectTransform.position, Is.EqualTo(beforePosition));
                Assert.That(plaque.rectTransform.lossyScale, Is.EqualTo(beforeScale), "Hover must never lift physical furniture.");
                trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerExit).callback.Invoke(new BaseEventData(null));
                Assert.That(plaque.color, Is.EqualTo(beforeColor));
            }
        }

        [TestCase(0, 1f)]
        [TestCase(0, 2f / 3f)]
        [TestCase(1, 1f)]
        [TestCase(1, 2f / 3f)]
        [TestCase(2, 1f)]
        [TestCase(2, 2f / 3f)]
        public void ReadingSizes_KeepAllOffersReachableWithoutPerPageFontChanges(int level, float scale)
        {
            int? previous = GameReadability.OverrideLevel;
            try
            {
                GameReadability.OverrideLevel = level;
                host = new GameObject("Component Shop Reading Test");
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.GrayGull, 0);
                Canvas canvas = host.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.scaleFactor = scale;
                decimal gold = controller.Session.ProfileGold;
                int seen = 0;
                for (int page = 0; page < 10; page++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (Text text in host.GetComponentsInChildren<Text>(false).Where(text => !string.IsNullOrEmpty(text.text) &&
                        (text.name.StartsWith("Component Shop Plaque ") || text.name.StartsWith("Component Shop Offer Suitability "))))
                    {
                        Assert.That(text.font, Is.SameAs(GameTypography.BodyFont), text.name);
                        Assert.That(text.fontSize, Is.EqualTo(GameReadability.FontSize(text.name.StartsWith("Component Shop Plaque Name ") ? 22 : 18)), text.name);
                        Assert.That(text.resizeTextForBestFit, Is.False, text.name);
                        Assert.That(text.pixelsPerUnit, Is.EqualTo(scale).Within(.0001f), text.name);
                        AssertComplete(text);
                    }
                    Button[] offers = host.GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("Component Shop Offer ")).ToArray();
                    seen += offers.Length;
                    foreach (ScrollRect scroll in host.GetComponentsInChildren<ScrollRect>(false))
                    {
                        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                        RectTransform last = (RectTransform)scroll.content.GetChild(scroll.content.childCount - 1);
                        var corners = new Vector3[4]; last.GetWorldCorners(corners);
                        Assert.That(scroll.viewport.InverseTransformPoint(corners[0]).y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1));
                    }
                    Button next = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Component Shop Next Display");
                    if (!next.interactable) break;
                    next.onClick.Invoke();
                }
                Assert.That(seen, Is.EqualTo(PortComponentShopCatalog.BuildStock(new ComponentArtCatalog(), controller.Session.CurrentPort, controller.Session.GameDateTime).Count));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Button offer = host.GetComponentsInChildren<Button>(false).First(button => button.name.StartsWith("Component Shop Offer "));
                offer.onClick.Invoke();
                foreach (string page in new[] { "Overview", "Effects", "Story", "SpecialCards" })
                {
                    host.GetComponentsInChildren<Button>(true).Single(button => button.name == "ComponentDossierPage" + page).onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    Transform detail = host.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Component Shop Detail");
                    foreach (Text text in detail.GetComponentsInChildren<Text>(false).Where(text => !string.IsNullOrEmpty(text.text)))
                    {
                        Assert.That(text.resizeTextForBestFit, Is.False, text.name);
                        Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)), text.name);
                        Assert.That(text.pixelsPerUnit, Is.EqualTo(scale).Within(.0001f), text.name);
                        AssertComplete(text);
                    }
                }
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            }
            finally { GameReadability.OverrideLevel = previous; }
        }

        private static void AssertComplete(Text text)
        {
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name);
            TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            using (var visible = new TextGenerator())
            using (var full = new TextGenerator())
            {
                visible.Populate(text.text, settings); settings.verticalOverflow = VerticalWrapMode.Overflow; full.Populate(text.text, settings);
                Assert.That(visible.vertexCount, Is.EqualTo(full.vertexCount), text.name);
                Assert.That(visible.characterCount, Is.EqualTo(full.characterCount), text.name);
                Assert.That(visible.vertexCount, Is.GreaterThanOrEqualTo(4), text.name);
            }
        }
    }
}
