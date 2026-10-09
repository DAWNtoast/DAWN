using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class SpecialCardDossierTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private static readonly string[] ComponentPages = { "Overview", "Effects", "Story", "SpecialCards" };

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase(ComponentRarity.Common)]
        [TestCase(ComponentRarity.Uncommon)]
        [TestCase(ComponentRarity.Rare)]
        [TestCase(ComponentRarity.Epic)]
        [TestCase(ComponentRarity.Legendary)]
        public void ComponentPages_KeepPurchaseFixedAndOnlyBlueOrBetterHaveSpecialCards(ComponentRarity rarity)
        {
            CreatePreview(CoreGameplayPreviewScreen.ComponentShop);
            ComponentArtEntry entry = new ComponentArtCatalog().Entries.First(item => item.Rarity == rarity);
            PortComponentShopStockItem offer = new PortComponentShopStockItem("dossier-test|" + entry.Id,
                controller.Session.CurrentPort, "2026-09", entry, 0.94f, 230);
            RectTransform panel = Find<RectTransform>("Component Shop Panel");
            foreach (Transform child in panel.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            Invoke("BuildComponentShopDetail", panel, offer);
            Canvas.ForceUpdateCanvases();
            RectTransform detail = Find<RectTransform>("Component Shop Detail");
            RectTransform purchase = Find<Button>("Purchase Component " + offer.Id).GetComponent<RectTransform>();
            Vector3 purchasePosition = purchase.position;
            decimal gold = controller.Session.ProfileGold;
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            string purchaseKey = CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id);
            bool wasSaved = PlayerPrefs.HasKey(purchaseKey);
            CoreVoyageSession sameSession = controller.Session;

            for (int page = 0; page < ComponentPages.Length; page++)
            {
                Find<Button>("ComponentDossierPage" + ComponentPages[page]).onClick.Invoke();
                Assert.That(purchase.gameObject.activeInHierarchy, Is.True);
                Assert.That(purchase.position, Is.EqualTo(purchasePosition));
                Assert.That(detail.GetComponentsInChildren<Text>(true).Any(label => label.name == "Component Detail Price"), Is.False,
                    "The component dossier preview omits the separate price label.");
                for (int other = 0; other < ComponentPages.Length; other++)
                    Assert.That(Find<RectTransform>("Component Dossier " + ComponentPages[other] + " Page").gameObject.activeSelf,
                        Is.EqualTo(page == other));
                foreach (Button button in detail.GetComponentsInChildren<Button>())
                    AssertInsidePaper(Find<RawImage>("Component Detail Parchment").rectTransform, button.GetComponent<RectTransform>());
                foreach (Text label in detail.GetComponentsInChildren<Text>()) AssertTextFits(label);
            }

            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(entry.Id);
            if (rarity < ComponentRarity.Rare)
            {
                Assert.That(definition, Is.Null);
                Assert.That(Find<Text>("Component Special Card Empty").text, Does.Contain("蓝色"));
            }
            else
            {
                Assert.That(definition, Is.Not.Null);
                Assert.That(Find<Text>("Component Special Card Name").text, Does.Contain(definition.Card.Name));
                Assert.That(Find<Text>("Component Special Card Lore").text, Is.EqualTo(definition.Lore));
            }
            Find<Button>("Read Component Story Chapter").onClick.Invoke();
            Assert.That(Find<Text>("Component Detail Story Body").gameObject.activeInHierarchy, Is.True);
            Find<Button>("Read Component Story Chapter").onClick.Invoke();
            Assert.That(Find<Text>("Component Detail Description").gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.Session, Is.SameAs(sameSession));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(acquired, controller.Session.AcquiredSpecialCardIds);
            Assert.That(PlayerPrefs.HasKey(purchaseKey), Is.EqualTo(wasSaved));
        }

        [TestCase(0)]
        [TestCase(840)]
        public void EverySpecialCard_HasCompleteReadableEffectLoreAndLiveExperience(int experience)
        {
            CreatePreview(CoreGameplayPreviewScreen.ComponentShop);
            if (experience > 0)
                controller.Session.RestoreSpecialCardExperience(CoreSpecialCardCatalog.All.ToDictionary(item => item.Card.Id, item => experience));
            RectTransform panel = Find<RectTransform>("Component Shop Panel");
            foreach (Transform child in panel.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            GameObject sheet = new GameObject("Special Card Dossier Content Audit", typeof(RectTransform));
            RectTransform page = sheet.GetComponent<RectTransform>();
            page.SetParent(panel, false);
            page.sizeDelta = new Vector2(860f, 300f);
            int checkedCards = 0;
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                foreach (Transform child in page.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                Invoke("BuildDossierSpecialCard", page, "Audit", definition, 860f, string.Empty, Color.black, Color.black);
                Canvas.ForceUpdateCanvases();
                int index = CoreVoyageSession.CardLibrary.ToList().FindIndex(card => card.Id == definition.Card.Id);
                CoreBattleCard card = controller.Session.GetBattleCard(index);
                string effectText = Find<Text>("Audit Special Card Effect").text;
                Assert.That(effectText, Is.EqualTo(controller.Session.BattleCardPortDescription(card)));
                Assert.That(effectText, Does.Contain(card.Description), "The upgraded base effect remains available for comparison.");
                if (card.IsAllowedInStage(NavalBattleStage.Boarding) && (card.Damage > 0 || card.Block > 0 || card.Repair > 0))
                {
                    CoreBattleCardEffects actual = controller.Session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                    Assert.That(effectText, Does.Contain("基础（" + CoreBattleCardEffects.ReferenceOrdinaryCrew + " 名普通水手）")
                        .And.Contain("当前 " + controller.Session.SelectedOrdinaryCrew + " 人接舷实际"));
                    string actualText = effectText.Substring(effectText.IndexOf('\n') + 1);
                    if (card.Damage > 0) Assert.That(actualText, Does.Contain("伤害 " + actual.Damage));
                    if (card.Block > 0) Assert.That(actualText, Does.Contain("格挡 " + actual.Block));
                    if (card.Repair > 0) Assert.That(actualText, Does.Contain("恢复 " + actual.Repair));
                }
                Assert.That(Find<Text>("Audit Special Card Lore").text, Is.EqualTo(definition.Lore));
                Assert.That(Find<RawImage>("Audit Special Card Art").texture, Is.Not.Null);
                Assert.That(Find<Text>("Audit Special Card Growth").text, Does.Contain("不可金币强化"));
                if (experience > 0)
                {
                    Assert.That(Find<Text>("Audit Special Card Name").text, Does.EndWith("+3"));
                    Assert.That(Find<Text>("Audit Special Card Growth").text, Does.Contain(experience.ToString()));
                    Assert.That(Find<Text>("Audit Special Card Growth").text, Does.Contain("已达三阶"));
                }
                RectTransform viewport = Find<RectTransform>("Audit Special Card Effect Viewport");
                RectTransform content = Find<RectTransform>("Audit Special Card Effect Content");
                AssertInsidePaper(page, viewport);
                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Text effect = Find<Text>("Audit Special Card Effect");
                Text lore = Find<Text>("Audit Special Card Lore");
                Assert.That(lore.rectTransform.anchoredPosition.y,
                    Is.LessThanOrEqualTo(effect.rectTransform.anchoredPosition.y - effect.rectTransform.rect.height - 8f),
                    "The complete effect and story must occupy separate paragraphs in scroll content.");
                foreach (Text label in page.GetComponentsInChildren<Text>())
                {
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(20), definition.SourceId + " / " + label.name);
                    Assert.That(label.resizeTextForBestFit, Is.False);
                    AssertTextFits(label);
                    AssertInsidePaper(label.rectTransform.IsChildOf(content) ? content : page, label.rectTransform);
                }
                AssertScrollableContent(viewport, content);
                Assert.That(page.GetComponentsInChildren<Button>().Length, Is.Zero, "Dossier card pages are read-only.");
                checkedCards++;
            }
            Assert.That(checkedCards, Is.EqualTo(30 + 75), "Audit every authored crew and rare-or-better component card; retired placeholders are not content.");
        }

        [TestCase(0)]
        [TestCase(840)]
        public void CompactCardEffectArea_KeepsLongEffectsReadableWithoutCoveringTheAddHint(int experience)
        {
            CreatePreview(CoreGameplayPreviewScreen.ComponentShop);
            if (experience > 0)
                controller.Session.RestoreSpecialCardExperience(CoreSpecialCardCatalog.All.ToDictionary(item => item.Card.Id, item => experience));
            RectTransform panel = Find<RectTransform>("Component Shop Panel");
            GameObject cardObject = new GameObject("Compact Effect Card Audit", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform cardFace = cardObject.GetComponent<RectTransform>();
            cardFace.SetParent(panel, false);
            cardFace.sizeDelta = new Vector2(218f, 200f);
            int scrollableCards = 0;
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                foreach (Transform child in cardFace.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                int index = CoreVoyageSession.CardLibrary.ToList().FindIndex(card => card.Id == definition.Card.Id);
                CoreBattleCard card = controller.Session.GetBattleCard(index);
                Invoke("CreateCardEffectArea", "Compact Effect Audit", cardFace,
                    new Vector2(196f, 76f), new Vector2(0f, -44f), 12, Color.black,
                    controller.Session.BattleCardPortDescription(card), TextAnchor.UpperCenter, null, null, 20, null);
                Canvas.ForceUpdateCanvases();
                Text effect = Find<Text>("Compact Effect Audit");
                RectTransform viewport = Find<RectTransform>("Compact Effect Audit Viewport");
                RectTransform content = Find<RectTransform>("Compact Effect Audit Content");
                Assert.That(effect.text, Is.EqualTo(controller.Session.BattleCardPortDescription(card)));
                Assert.That(effect.fontSize, Is.EqualTo(12));
                Assert.That(effect.resizeTextForBestFit, Is.False);
                AssertTextFits(effect);
                AssertInsidePaper(content, effect.rectTransform);
                AssertInsidePaper(cardFace, viewport);
                Assert.That(viewport.anchoredPosition.y - viewport.rect.height * 0.5f, Is.GreaterThan(-84f),
                    "The scrolling effect must stay above the add hint's upper edge.");
                AssertScrollableContent(viewport, content);
                if (viewport.GetComponent<ScrollRect>() != null) scrollableCards++;
            }
            Assert.That(scrollableCards, Is.GreaterThan(0), "Long tactics, including flame rounds, must remain available through scrolling.");
        }

        [Test]
        public void SmallCanvasScale_KeepsRaisedCjkGlyphsInsideTheEffectContent()
        {
            CreatePreview(CoreGameplayPreviewScreen.ComponentShop);
            Canvas canvas = host.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.scaleFactor = 0.3849002f;
            RectTransform panel = Find<RectTransform>("Component Shop Panel");
            GameObject sheet = new GameObject("Small Canvas Dossier Audit", typeof(RectTransform));
            RectTransform page = sheet.GetComponent<RectTransform>();
            page.SetParent(panel, false);
            page.sizeDelta = new Vector2(860f, 300f);
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_navigator_evelyn");
            Invoke("BuildDossierSpecialCard", page, "Small Canvas", definition, 860f, string.Empty, Color.black, Color.black);
            Canvas.ForceUpdateCanvases();
            Text effect = Find<Text>("Small Canvas Special Card Effect");
            Text lore = Find<Text>("Small Canvas Special Card Lore");
            Assert.That(effect.pixelsPerUnit, Is.EqualTo(0.3849002f).Within(0.00001f));
            Assert.That(effect.rectTransform.anchoredPosition.y, Is.EqualTo(-12f),
                "The first line needs real inset below the mask, not a relaxed mesh bound.");
            AssertTextFits(effect);
            AssertTextFits(lore);
            RectTransform viewport = Find<RectTransform>("Small Canvas Special Card Effect Viewport");
            AssertInsidePaper(page, viewport);
            AssertScrollableContent(viewport, Find<RectTransform>("Small Canvas Special Card Effect Content"));
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DoctorDossierShowsPersonalBoardingCardWithoutRecruiting(CoreGameplayPreviewPort port)
        {
            CreatePreview(CoreGameplayPreviewScreen.TavernCrewDetail);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, port, 0);
            controller.ConfigureEditorCrewDossier(2, 3);
            decimal gold = controller.Session.ProfileGold;
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            Find<Button>("DossierPageSpecialCards").onClick.Invoke();
            Assert.That(Find<Text>("Tavern Special Card Stage").text, Does.StartWith("近战"));
            Assert.That(Find<Text>("Tavern Special Card Effect").text, Does.Contain("船员生命"));
            Assert.That(Find<Text>("Tavern Special Card Name").text, Does.Not.Contain("没有专属"));
            foreach (Text label in Find<RectTransform>("Tavern Dossier SpecialCards Page").GetComponentsInChildren<Text>())
                AssertTextFits(label);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(acquired, controller.Session.AcquiredSpecialCardIds);
        }

        [Test]
        public void CrewSpecialCardTab_IsPresentForCrewWithoutCardsAndDoesNotSpendGold()
        {
            CreatePreview(CoreGameplayPreviewScreen.TavernCrewDetail);
            controller.ConfigureEditorCrewDossier(4, 3); // Cook retains non-card support skills.
            decimal gold = controller.Session.ProfileGold;
            Find<Button>("DossierPageSpecialCards").onClick.Invoke();
            Assert.That(Find<Text>("Tavern Special Card Empty").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<Text>("Tavern Special Card Empty").text, Does.Contain("职业专长"));
            Assert.That(Find<Text>("Tavern Dossier Page Counter").text, Is.EqualTo("第 4 / 4 页"));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }

        private void CreatePreview(CoreGameplayPreviewScreen screen)
        {
            host = new GameObject("Special Card Dossier Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
        }

        private void Invoke(string method, params object[] arguments) => typeof(CoreGameplayDemoController)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, arguments);

        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static void AssertTextFits(Text label)
        {
            Assert.That(label.preferredHeight,
                Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f), label.name + " must retain its complete text at the authored font size.");
            TextGenerationSettings actualSettings = label.GetGenerationSettings(label.rectTransform.rect.size);
            label.cachedTextGenerator.Populate(label.text, actualSettings);
            TextGenerationSettings completeSettings = actualSettings;
            completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
            completeSettings.generateOutOfBounds = true;
            using (TextGenerator complete = new TextGenerator())
            {
                complete.Populate(label.text, completeSettings);
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(complete.characterCountVisible),
                    label.name + " must generate every character, including the last effect line; preferredHeight alone can undermeasure the embedded font. " +
                    "Height=" + label.rectTransform.rect.height + ", preferred=" + label.preferredHeight + ", text=" + label.text);
                Assert.That(label.cachedTextGenerator.vertexCount, Is.EqualTo(complete.vertexCount),
                    label.name + " must retain the complete glyph mesh.");
                AssertGlyphsInsideTextRect(label, complete, Mathf.Max(0.01f, completeSettings.scaleFactor));
                completeSettings.scaleFactor = 1f;
                complete.Populate(label.text, completeSettings);
                AssertGlyphsInsideTextRect(label, complete, 1f);
            }
        }

        private static void AssertGlyphsInsideTextRect(Text label, TextGenerator generator, float scale)
        {
            if (!label.name.Contains("Effect") && !label.name.Contains("Lore")) return;
            // Overflow belongs only to the scroll viewport. Every generated
            // glyph still needs room in its own paragraph before the next one.
            foreach (UIVertex vertex in generator.verts)
                Assert.That(vertex.position.y / scale, Is.GreaterThanOrEqualTo(label.rectTransform.rect.yMin - 1f),
                    label.name + " generated a glyph below its paragraph at scale " + scale +
                    "; enlarge the content height instead of overlapping the following lore or controls.");
            RectTransform content = label.rectTransform.parent as RectTransform;
            if (content == null || !content.name.EndsWith(" Content")) return;
            Text previous = content.GetComponentsInChildren<Text>(true)
                .Where(other => other != label && other.rectTransform.anchoredPosition.y > label.rectTransform.anchoredPosition.y)
                .OrderBy(other => other.rectTransform.anchoredPosition.y).FirstOrDefault();
            float safeTop = previous == null ? content.rect.yMax :
                previous.rectTransform.anchoredPosition.y - previous.rectTransform.rect.height;
            foreach (UIVertex vertex in generator.verts)
            {
                float glyphY = content.InverseTransformPoint(label.rectTransform.TransformPoint(vertex.position / scale)).y;
                Assert.That(glyphY, Is.LessThanOrEqualTo(safeTop + 0.5f),
                    label.name + " must leave raised CJK glyphs inside the content and below the preceding paragraph at scale " + scale + ".");
            }
        }

        private static void AssertScrollableContent(RectTransform viewport, RectTransform content)
        {
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            if (content.rect.height > viewport.rect.height + 0.5f)
            {
                Assert.That(scroll, Is.Not.Null, "Text taller than its viewport must remain reachable.");
                Assert.That(scroll.content, Is.SameAs(content));
                Assert.That(scroll.viewport, Is.SameAs(viewport));
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.verticalScrollbar, Is.Not.Null);
                Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(viewport.GetComponent<Image>().raycastTarget, Is.True,
                    "ScrollRect must receive the drag so EventSystem cancels the parent card's pending click.");
            }
            else Assert.That(scroll, Is.Null, "Short effects should not introduce unnecessary scrolling.");
        }

        private static void AssertInsidePaper(RectTransform paper, RectTransform item)
        {
            Vector3[] corners = new Vector3[4];
            item.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = paper.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(paper.rect.xMin - 0.1f, paper.rect.xMax + 0.1f), item.name);
                Assert.That(point.y, Is.InRange(paper.rect.yMin - 0.1f, paper.rect.yMax + 0.1f), item.name);
            }
        }
    }
}
