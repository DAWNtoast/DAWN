using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentDossierLayoutTests
    {
        private static readonly string[] Pages = { "Overview", "Effects", "Story", "SpecialCards" };
        private static readonly Rect WritingPaper = Rect.MinMaxRect(-253f, -216f, 765f, 237f);
        private static readonly Rect FooterPaper = Rect.MinMaxRect(-288f, -264f, 600f, -220f);
        private GameObject host;
        private CoreGameplayDemoController controller;
        private RectTransform panel;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(ComponentSlot.Bow)]
        [TestCase(ComponentSlot.Sail)]
        [TestCase(ComponentSlot.Cannon)]
        [TestCase(ComponentSlot.Plating)]
        [TestCase(ComponentSlot.Instrument)]
        public void EveryMarketComponentKeepsFourCompleteReadablePagesAndSeparatePurchaseControls(ComponentSlot slot)
        {
            CreatePreview();
            ComponentArtEntry[] entries = new ComponentArtCatalog().ExpandedEntries.Where(entry => entry.Slot == slot).ToArray();
            Assert.That(entries.Length, Is.EqualTo(30), "Audit all six variants at each of the five rarities, including expanded market stock.");
            int pagesChecked = 0;
            foreach (ComponentArtEntry entry in entries)
            {
                var offer = new PortComponentShopStockItem("component-layout|" + entry.Id,
                    controller.Session.CurrentPort, "2026-10", entry, .94f, 230);
                string purchaseKey = CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id);
                bool hadPurchaseKey = PlayerPrefs.HasKey(purchaseKey);
                int purchaseValue = PlayerPrefs.GetInt(purchaseKey, -1);
                var session = controller.Session;
                decimal gold = session.ProfileGold;
                string[] acquired = session.AcquiredSpecialCardIds.ToArray();
                var experience = session.SpecialCardExperience.ToArray();
                string equipped = controller.EquippedComponent(slot)?.Id;

                Build(offer);
                RectTransform detail = Find<RectTransform>("Component Shop Detail");
                Button purchase = Find<Button>("Purchase Component " + offer.Id);
                Button equip = Find<Button>("Purchase And Equip Component " + offer.Id);
                Vector3 purchasePosition = purchase.transform.position;
                Vector3 equipPosition = equip.transform.position;
                AssertHeadingAndFooter(detail, offer);
                RawImage model = Find<RawImage>("Component Detail Model");
                Assert.That(model.texture, Is.Not.Null, entry.Id);
                Assert.That(model.rectTransform.sizeDelta,
                    Is.EqualTo(ComponentShopDisplayGeometry.FitDetailModelSize(model.uvRect)), entry.Id + " preserves its native aspect ratio");
                Assert.That(model.raycastTarget, Is.False);
                Assert.That(model.rectTransform.rect.width, Is.LessThanOrEqualTo(360.1f));
                Assert.That(model.rectTransform.rect.height, Is.LessThanOrEqualTo(300.1f));

                AssertText("Component Detail Name", entry.Name, 36, entry.Id);
                AssertText("Component Detail Meta", ComponentArtCatalog.RarityName(entry.Rarity) + " · " + ComponentArtCatalog.SlotName(entry.Slot), 22, entry.Id);
                Assert.That(Find<Text>("Component Detail Quality").text, Does.Contain("94%"), entry.Id);
                Assert.That(WithoutWhitespace(Find<Text>("Component Detail Specialization").text),
                    Does.Contain(WithoutWhitespace(entry.Specialization)), entry.Id);
                AssertText("Component Detail Description", entry.Description, 25, entry.Id);
                AssertText("Component Detail Story Chapter", entry.StoryTitle, 25, entry.Id);
                AssertText("Component Detail Story Body", entry.StoryBody, 25, entry.Id);
                string effects = string.Concat(Find<RectTransform>("Component Dossier Effects Page")
                    .GetComponentsInChildren<Text>(true).Select(label => label.text));
                Assert.That(WithoutWhitespace(effects), Does.Contain(WithoutWhitespace(entry.Effect)), entry.Id);
                Assert.That(WithoutWhitespace(effects), Does.Contain(WithoutWhitespace(ComponentArtCatalog.EnvironmentSummary(entry))), entry.Id);

                for (int page = 0; page < Pages.Length; page++)
                {
                    Find<Button>("ComponentDossierPage" + Pages[page]).onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    AssertPage(page);
                    RectTransform pageRoot = Find<RectTransform>("Component Dossier " + Pages[page] + " Page");
                    Assert.That(pageRoot.rect.height, Is.EqualTo(page == 3 ? 368f : 326f));
                    Assert.That(purchase.gameObject.activeInHierarchy && equip.gameObject.activeInHierarchy, Is.True, entry.Id);
                    Assert.That(purchase.transform.position, Is.EqualTo(purchasePosition));
                    Assert.That(equip.transform.position, Is.EqualTo(equipPosition));
                    if (page < 3)
                    {
                        foreach (Text label in pageRoot.GetComponentsInChildren<Text>())
                        {
                            Rect visible = VisibleBounds(detail, label);
                            AssertContained(WritingPaper, visible, entry.Id + " / " + label.name);
                            Assert.That(visible.yMin - FooterPaper.yMax, Is.GreaterThanOrEqualTo(3.9f), label.name);
                            Assert.That(visible.xMin - BoundsIn(detail, model.rectTransform).xMax,
                                Is.GreaterThanOrEqualTo(130f), entry.Id + " keeps prose clear of the illustration");
                            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(24), label.name);
                        }
                    }
                    else AssertSpecialCard(detail, pageRoot, entry);
                    foreach (Text label in detail.GetComponentsInChildren<Text>()) AssertCompleteText(label, entry.Id);
                    AssertScrollInputClearOfButtons(detail, entry.Id);
                    pagesChecked++;
                }

                // Both arrow paths and direct tabs must only change the visible page.
                Find<Button>("Next Component Dossier Page").onClick.Invoke();
                AssertPage(3);
                for (int page = 2; page >= 0; page--)
                {
                    Find<Button>("Previous Component Dossier Page").onClick.Invoke();
                    AssertPage(page);
                }
                Find<Button>("Previous Component Dossier Page").onClick.Invoke();
                AssertPage(0);
                Assert.That(Find<RectTransform>("Component Shop Detail"), Is.SameAs(detail));
                Assert.That(controller.Session, Is.SameAs(session));
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.EquippedComponent(slot)?.Id, Is.EqualTo(equipped));
                CollectionAssert.AreEqual(acquired, session.AcquiredSpecialCardIds);
                CollectionAssert.AreEquivalent(experience, session.SpecialCardExperience.ToArray());
                Assert.That(PlayerPrefs.HasKey(purchaseKey), Is.EqualTo(hadPurchaseKey));
                Assert.That(PlayerPrefs.GetInt(purchaseKey, -1), Is.EqualTo(purchaseValue));
            }
            Assert.That(pagesChecked, Is.EqualTo(120));
        }

        [TestCase(false, true)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void PurchaseAndEquipKeepTheirDistinctCaptionsAndAvailabilityOnEveryPage(bool sold, bool affordable)
        {
            CreatePreview();
            ComponentArtEntry entry = new ComponentArtCatalog().ExpandedEntries.Last();
            int price = affordable ? 1 : decimal.ToInt32(controller.Session.ProfileGold) + 1000;
            var offer = new PortComponentShopStockItem("component-layout-state|" + sold + "|" + affordable,
                controller.Session.CurrentPort, "2026-10", entry, 1f, price);
            string key = CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id);
            bool hadKey = PlayerPrefs.HasKey(key);
            int previous = PlayerPrefs.GetInt(key, 0);
            try
            {
                PlayerPrefs.SetInt(key, sold ? 1 : 0);
                Build(offer);
                string status = sold ? "已售出" : affordable ? price + " 金币" : "金币不足";
                foreach (string page in Pages)
                {
                    Find<Button>("ComponentDossierPage" + page).onClick.Invoke();
                    Button purchase = Find<Button>("Purchase Component " + offer.Id);
                    Button equip = Find<Button>("Purchase And Equip Component " + offer.Id);
                    Assert.That(purchase.GetComponentInChildren<Text>().text, Is.EqualTo("购买 · " + status));
                    Assert.That(equip.GetComponentInChildren<Text>().text, Is.EqualTo("购买后装配 · " + status));
                    Assert.That(purchase.interactable, Is.EqualTo(!sold && affordable));
                    Assert.That(equip.interactable, Is.EqualTo(!sold && affordable));
                    Assert.That(purchase, Is.Not.SameAs(equip));
                    AssertCompleteText(purchase.GetComponentInChildren<Text>(), status);
                    AssertCompleteText(equip.GetComponentInChildren<Text>(), status);
                }
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(sold ? 1 : 0));
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
            }
        }

        private void CreatePreview()
        {
            host = new GameObject("Component Dossier Layout Audit");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.GrayGull, 0);
            panel = Find<RectTransform>("Component Shop Panel");
        }

        private void Build(PortComponentShopStockItem offer)
        {
            foreach (Transform child in panel.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            typeof(CoreGameplayDemoController).GetField("selectedComponentShopOfferId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, offer.Id);
            typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopDetail", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { panel, offer });
            Canvas.ForceUpdateCanvases();
        }

        private void AssertHeadingAndFooter(RectTransform detail, PortComponentShopStockItem offer)
        {
            Rect name = BoundsIn(detail, Find<Text>("Component Detail Name").rectTransform);
            Rect meta = BoundsIn(detail, Find<Text>("Component Detail Meta").rectTransform);
            Assert.That(name.xMin, Is.EqualTo(-165f).Within(.1f));
            Assert.That(name.center.y, Is.EqualTo(210f).Within(.1f));
            Assert.That(meta.center.y, Is.EqualTo(name.center.y).Within(.1f));
            Assert.That(meta.xMin - name.xMax, Is.GreaterThanOrEqualTo(16.9f));
            foreach (string field in new[] { "Component Detail Name", "Component Detail Meta" })
            {
                Text text = Find<Text>(field);
                AssertContained(WritingPaper, BoundsIn(detail, text.rectTransform), field);
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width), offer.Entry.Id + " / " + field);
            }
            foreach (string page in Pages)
            {
                RectTransform tab = Find<Button>("ComponentDossierPage" + page).GetComponent<RectTransform>();
                Assert.That(tab.anchoredPosition.y, Is.EqualTo(160f));
                AssertContained(WritingPaper, BoundsIn(detail, tab), tab.name);
            }
            string[] footer = { "Previous Component Dossier Page", "Component Dossier Page Counter", "Next Component Dossier Page",
                "Purchase Component " + offer.Id, "Purchase And Equip Component " + offer.Id };
            Rect previous = default;
            for (int index = 0; index < footer.Length; index++)
            {
                Rect current = BoundsIn(detail, Find<RectTransform>(footer[index]));
                AssertContained(FooterPaper, current, footer[index]);
                Assert.That(current.height, Is.GreaterThanOrEqualTo(43.9f));
                if (index > 0) Assert.That(current.xMin - previous.xMax, Is.GreaterThanOrEqualTo(19.9f), footer[index]);
                previous = current;
            }
        }

        private void AssertSpecialCard(RectTransform detail, RectTransform page, ComponentArtEntry entry)
        {
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(entry.Id);
            Assert.That(definition != null, Is.EqualTo(entry.Rarity >= ComponentRarity.Rare), entry.Id);
            if (definition == null)
            {
                Assert.That(page.GetComponentsInChildren<Text>(true).Select(text => text.text),
                    Is.EqualTo(new[] { "没有专属卡牌" }));
                Assert.That(page.GetComponentsInChildren<CardSchoolFrameGraphic>(), Is.Empty);
                foreach (Text text in page.GetComponentsInChildren<Text>())
                    AssertContained(WritingPaper, VisibleBounds(detail, text), text.name);
                return;
            }
            AssertText("Component Special Card Lore", definition.Lore, 24, entry.Id);
            RectTransform face = Find<RectTransform>("Component Special Card Face");
            Rect frame = BoundsIn(detail, face.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            AssertContained(WritingPaper, frame, entry.Id + " / full ornate card frame");
            Assert.That(frame.yMin - FooterPaper.yMax, Is.GreaterThanOrEqualTo(3.9f));
            Assert.That(face.GetComponentsInChildren<Button>(), Is.Empty, "Reading a card cannot acquire its source.");
            Rect stage = BoundsIn(detail, Find<Text>("Component Special Card Stage").rectTransform);
            Rect details = BoundsIn(detail, Find<RectTransform>("Component Special Card Details Viewport"));
            Rect growth = BoundsIn(detail, Find<Text>("Component Special Card Growth").rectTransform);
            foreach (Rect region in new[] { stage, details, growth })
            {
                AssertContained(WritingPaper, region, entry.Id + " / card explanation column");
                Assert.That(region.xMin, Is.GreaterThan(frame.xMax));
            }
            Assert.That(stage.yMin - details.yMax, Is.GreaterThanOrEqualTo(.9f));
            Assert.That(details.yMin - growth.yMax, Is.GreaterThanOrEqualTo(.9f));
            Assert.That(growth.yMin - FooterPaper.yMax, Is.GreaterThanOrEqualTo(5.9f));
            Assert.That(Find<Text>("Component Special Card Growth").fontSize, Is.GreaterThanOrEqualTo(22));
        }

        private void AssertPage(int selected)
        {
            for (int page = 0; page < Pages.Length; page++)
                Assert.That(Find<RectTransform>("Component Dossier " + Pages[page] + " Page").gameObject.activeSelf, Is.EqualTo(page == selected));
            Assert.That(Find<Text>("Component Dossier Page Counter").text, Is.EqualTo("第 " + (selected + 1) + " / 4 页"));
            Assert.That(Find<Button>("Previous Component Dossier Page").interactable, Is.EqualTo(selected > 0));
            Assert.That(Find<Button>("Next Component Dossier Page").interactable, Is.EqualTo(selected < 3));
        }

        private void AssertText(string name, string expected, int minimumSize, string item)
        {
            Text label = Find<Text>(name);
            Assert.That(WithoutWhitespace(label.text), Is.EqualTo(WithoutWhitespace(expected)), item + " / " + name);
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(minimumSize), name);
            Assert.That(label.resizeTextForBestFit, Is.False, name);
        }

        private static void AssertCompleteText(Text label, string item)
        {
            bool readingText = label.name.StartsWith("Component Detail ", StringComparison.Ordinal) ||
                label.name.StartsWith("Component Special Card ", StringComparison.Ordinal);
            if (readingText)
                Assert.That(label.resizeTextForBestFit, Is.False, item + " / " + label.name);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), item + " / " + label.name);
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            label.cachedTextGenerator.Populate(label.text, settings);
            Button button = label.GetComponentInParent<Button>();
            if (button != null)
            {
                // The shared button skin may fit its caption. Verify the size
                // it actually renders instead of forbidding that factory policy.
                float pixelScale = Mathf.Max(.01f, settings.scaleFactor);
                // Unity reports best-fit size in raster pixels, including the
                // CanvasScaler factor (24 logical units becomes 8 at 1/3).
                int renderedPixels = label.resizeTextForBestFit ? label.cachedTextGenerator.fontSizeUsedForBestFit :
                    Mathf.FloorToInt(label.fontSize * pixelScale);
                Assert.That(renderedPixels, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(20f * pixelScale)),
                    item + " / " + button.name + " caption stays readable");
                settings.fontSize = Mathf.CeilToInt(renderedPixels / pixelScale);
                settings.resizeTextForBestFit = false;
            }
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            using (var complete = new TextGenerator())
            {
                complete.Populate(label.text, settings);
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(complete.characterCountVisible),
                    item + " / " + label.name + " must retain its last line");
                Assert.That(label.cachedTextGenerator.vertexCount, Is.EqualTo(complete.vertexCount),
                    item + " / " + label.name + " must retain its complete glyph mesh");
                if (button != null)
                {
                    RectTransform surface = button.GetComponent<RectTransform>();
                    float scale = Mathf.Max(.01f, settings.scaleFactor);
                    float tolerance = 1f / scale;
                    foreach (UIVertex vertex in complete.verts)
                    {
                        Vector3 glyph = surface.InverseTransformPoint(label.rectTransform.TransformPoint(vertex.position / scale));
                        Assert.That(glyph.x, Is.InRange(surface.rect.xMin - tolerance, surface.rect.xMax + tolerance), button.name);
                        Assert.That(glyph.y, Is.InRange(surface.rect.yMin - tolerance, surface.rect.yMax + tolerance), button.name);
                    }
                }
                bool prose = label.name == "Component Detail Description" || label.name == "Component Detail Story Body" ||
                    label.name == "Component Detail Effect" || label.name == "Component Special Card Lore" ||
                    label.name == "Component Special Card Calculation";
                if (prose)
                {
                    foreach (float scale in new[] { Mathf.Max(.01f, settings.scaleFactor), 1f })
                    {
                        settings.scaleFactor = scale;
                        complete.Populate(label.text, settings);
                        foreach (UIVertex vertex in complete.verts)
                            Assert.That(vertex.position.y / scale, Is.GreaterThanOrEqualTo(label.rectTransform.rect.yMin - 1f),
                                item + " / " + label.name + " has a glyph below the measured paragraph at scale " + scale);
                    }
                }
            }
            ScrollRect scroll = label.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            AssertContained(scroll.content.rect, BoundsIn(scroll.content, label.rectTransform), label.name + " / complete scroll content");
        }

        private static void AssertScrollInputClearOfButtons(RectTransform detail, string item)
        {
            foreach (ScrollRect scroll in detail.GetComponentsInChildren<ScrollRect>())
            {
                Rect visible = BoundsIn(detail, scroll.viewport);
                AssertContained(WritingPaper, visible, item + " / " + scroll.name);
                foreach (Button button in detail.GetComponentsInChildren<Button>())
                    Assert.That(visible.Overlaps(BoundsIn(detail, button.GetComponent<RectTransform>())), Is.False,
                        scroll.name + " must not intercept " + button.name);
            }
        }

        private static Rect VisibleBounds(RectTransform detail, Text label)
        {
            ScrollRect scroll = label.GetComponentInParent<ScrollRect>();
            return BoundsIn(detail, scroll != null ? scroll.viewport : label.rectTransform);
        }

        private static string WithoutWhitespace(string text) => new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());

        private static void AssertContained(Rect paper, Rect content, string context)
        {
            Assert.That(content.xMin, Is.GreaterThanOrEqualTo(paper.xMin - .1f), context);
            Assert.That(content.xMax, Is.LessThanOrEqualTo(paper.xMax + .1f), context);
            Assert.That(content.yMin, Is.GreaterThanOrEqualTo(paper.yMin - .1f), context);
            Assert.That(content.yMax, Is.LessThanOrEqualTo(paper.yMax + .1f), context);
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }

        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
    }
}
