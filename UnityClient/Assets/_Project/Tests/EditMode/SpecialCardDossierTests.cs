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
            typeof(CoreGameplayDemoController).GetField("selectedComponentShopOfferId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, offer.Id);
            Invoke("BuildComponentShopDetail", panel, offer);
            Object.DestroyImmediate(Find<RectTransform>("Port Facility Header").gameObject);
            Invoke("BuildFacilityHeader", PortFacilityArtCatalog.ComponentShopFacilityIndex);
            Canvas.ForceUpdateCanvases();
            RectTransform detail = Find<RectTransform>("Component Shop Detail");
            RectTransform purchase = Find<Button>("Purchase Component " + offer.Id).GetComponent<RectTransform>();
            RectTransform purchaseAndEquip = Find<Button>("Purchase And Equip Component " + offer.Id).GetComponent<RectTransform>();
            Vector3 purchasePosition = purchase.position;
            Vector3 purchaseAndEquipPosition = purchaseAndEquip.position;
            decimal gold = controller.Session.ProfileGold;
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            string purchaseKey = CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id);
            bool wasSaved = PlayerPrefs.HasKey(purchaseKey);
            CoreVoyageSession sameSession = controller.Session;
            Button back = Find<Button>("Return To Port Hub");
            Button previous = Find<Button>("Previous Component Dossier Page");
            Button next = Find<Button>("Next Component Dossier Page");
            Assert.That(detail.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Read Component Story Chapter"), Is.False,
                "Component lore is reached through the story tab and page navigation.");
            RectTransform header = Find<RectTransform>("Port Facility Header");
            Assert.That(back.transform.parent, Is.SameAs(header));
            AssertContained(header.rect, BoundsIn(header, back.GetComponent<RectTransform>()), "Return button / facility header");
            Assert.That(BoundsIn(detail, back.GetComponent<RectTransform>()).yMin, Is.GreaterThan(detail.rect.yMax));
            Assert.That(back.GetComponentInChildren<Text>().text, Is.EqualTo("返回部件陈列"));
            Assert.That(host.GetComponentsInChildren<Button>(true).Count(button =>
                button.GetComponentInChildren<Text>(true)?.text.Contains("返回部件陈列") == true), Is.EqualTo(1));
            Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button => button.name == "Back To Component Shop" ||
                button.GetComponentInChildren<Text>(true)?.text.Contains("返回部件陈列") == true), Is.False,
                "The component dossier must use only the header return button.");
            Rect footerPaper = Rect.MinMaxRect(-288f, -264f, 600f, -220f);
            string[] footerNames = { "Previous Component Dossier Page", "Component Dossier Page Counter", "Next Component Dossier Page", purchase.name, purchaseAndEquip.name };
            Rect previousFooter = default;
            for (int index = 0; index < footerNames.Length; index++)
            {
                Rect current = BoundsIn(detail, Find<RectTransform>(footerNames[index]));
                AssertContained(footerPaper, current, footerNames[index]);
                if (index > 0) Assert.That(current.xMin - previousFooter.xMax, Is.GreaterThanOrEqualTo(19.9f), footerNames[index]);
                previousFooter = current;
            }

            for (int page = 0; page < ComponentPages.Length; page++)
            {
                Find<Button>("ComponentDossierPage" + ComponentPages[page]).onClick.Invoke();
                AssertComponentPage(page);
                Assert.That(back.gameObject.activeInHierarchy, Is.True);
                Assert.That(back.IsInteractable(), Is.True);
                Assert.That(back.GetComponentInChildren<Text>().text, Is.EqualTo("返回部件陈列"));
                Assert.That(purchase.gameObject.activeInHierarchy, Is.True);
                Assert.That(purchaseAndEquip.gameObject.activeInHierarchy, Is.True);
                Assert.That(purchase.position, Is.EqualTo(purchasePosition));
                Assert.That(purchaseAndEquip.position, Is.EqualTo(purchaseAndEquipPosition));
                Assert.That(BoundsIn(detail, purchase).Overlaps(BoundsIn(detail, purchaseAndEquip)), Is.False,
                    "Buying into inventory and buying with immediate equipment remain separate controls.");
                if (PlayerPrefs.GetInt(purchaseKey, 0) == 0 && gold >= offer.Price)
                {
                    Assert.That(purchase.GetComponentInChildren<Text>().text, Is.EqualTo("购买 · 230 金币"));
                    Assert.That(purchaseAndEquip.GetComponentInChildren<Text>().text, Is.EqualTo("购买后装配 · 230 金币"));
                }
                Assert.That(detail.GetComponentsInChildren<Text>(true).Any(label => label.name == "Component Detail Price"), Is.False,
                    "The component dossier preview omits the separate price label.");
                foreach (Button button in detail.GetComponentsInChildren<Button>())
                    AssertInsidePaper(Find<RawImage>("Component Detail Parchment").rectTransform, button.GetComponent<RectTransform>());
                foreach (Text label in detail.GetComponentsInChildren<Text>()) AssertTextFits(label);
                RectTransform pageRoot = Find<RectTransform>("Component Dossier " + ComponentPages[page] + " Page");
                foreach (Transform child in pageRoot)
                {
                    RectTransform content = child as RectTransform;
                    if (content != null)
                        Assert.That(BoundsIn(detail, content).yMin - Mathf.Max(BoundsIn(detail, purchase).yMax, BoundsIn(detail, purchaseAndEquip).yMax), Is.GreaterThanOrEqualTo(8.9f),
                            content.name + " must stay clear of the shared footer.");
                }
            }

            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(entry.Id);
            if (rarity < ComponentRarity.Rare)
            {
                Assert.That(definition, Is.Null);
                Assert.That(Find<RectTransform>("Component Dossier SpecialCards Page")
                    .GetComponentsInChildren<Text>(true).Select(text => text.text),
                    Is.EqualTo(new[] { "没有专属卡牌" }));
            }
            else
            {
                Assert.That(definition, Is.Not.Null);
                Assert.That(Find<Text>("Component Special Card Name").text, Does.Contain(definition.Card.Name));
                Assert.That(WithoutWhitespace(Find<Text>("Component Special Card Lore").text),
                    Is.EqualTo(WithoutWhitespace(definition.Lore)));
                AssertCompleteCardFace("Component", definition.Card, Find<RectTransform>("Component Dossier SpecialCards Page"));
            }
            next.onClick.Invoke();
            AssertComponentPage(3);
            previous.onClick.Invoke();
            AssertComponentPage(2);
            Assert.That(Find<Text>("Component Detail Story Body").gameObject.activeInHierarchy, Is.True);
            previous.onClick.Invoke();
            AssertComponentPage(1);
            previous.onClick.Invoke();
            AssertComponentPage(0);
            previous.onClick.Invoke();
            AssertComponentPage(0);
            Assert.That(Find<Text>("Component Detail Description").gameObject.activeInHierarchy, Is.True);
            for (int page = 1; page < ComponentPages.Length; page++)
            {
                next.onClick.Invoke();
                AssertComponentPage(page);
            }
            Assert.That(Find<RectTransform>("Component Shop Detail"), Is.SameAs(detail), "Page navigation must not rebuild the dossier.");
            Assert.That(purchase.position, Is.EqualTo(purchasePosition));
            Assert.That(purchaseAndEquip.position, Is.EqualTo(purchaseAndEquipPosition));
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
            decimal gold = controller.Session.ProfileGold;
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            KeyValuePair<string, int>[] progress = controller.Session.SpecialCardExperience.ToArray();
            int checkedCards = 0;
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                foreach (Transform child in page.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                Invoke("BuildDossierSpecialCard", page, "Audit", definition, 860f, string.Empty, Color.black, Color.black);
                Canvas.ForceUpdateCanvases();
                int index = CoreVoyageSession.CardLibrary.ToList().FindIndex(card => card.Id == definition.Card.Id);
                CoreBattleCard card = controller.Session.GetBattleCard(index);
                string effectText = Find<Text>("Audit Special Card Effect").text;
                Assert.That(VisibleEffectWithoutLineBreaks(effectText),
                    Is.EqualTo(VisibleEffectWithoutLineBreaks(ExpectedDossierEffectText(card))),
                    "The card face quotes the current stage's actual effects, including the player damage multiplier.");
                RectTransform face = AssertCompleteCardFace("Audit", card, page);
                Text calculation = null;
                if (card.IsAllowedInStage(NavalBattleStage.Boarding))
                {
                    calculation = Find<Text>("Audit Special Card Calculation");
                    Assert.That(calculation.text, Is.EqualTo(controller.Session.BattleCardPortDescription(card)),
                        "Current supply-dependent effects remain readable beside the card face.");
                    Assert.That(calculation.transform.IsChildOf(face), Is.False);
                }
                if (card.IsAllowedInStage(NavalBattleStage.Boarding) && (card.Damage > 0 || card.Block > 0 || card.Repair > 0))
                {
                    CoreBattleCardEffects actual = controller.Session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                    Assert.That(calculation.text, Does.Contain("卡牌效果：").And.Contain("当前接舷效果：")
                        .And.Not.Contain("普通水手").And.Not.Contain("人接舷实际"));
                    string actualText = calculation.text.Substring(calculation.text.IndexOf('\n') + 1);
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
                RectTransform viewport = Find<RectTransform>("Audit Special Card Details Viewport");
                RectTransform content = Find<RectTransform>("Audit Special Card Details Content");
                AssertInsidePaper(page, viewport);
                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Text lore = Find<Text>("Audit Special Card Lore");
                Assert.That(lore.transform.IsChildOf(content), Is.True);
                Assert.That(BoundsIn(page, viewport).xMin, Is.GreaterThan(BoundsIn(page, face).xMax),
                    "The calculation and story column stays beside the complete card.");
                if (calculation != null)
                    Assert.That(lore.rectTransform.anchoredPosition.y,
                        Is.LessThanOrEqualTo(calculation.rectTransform.anchoredPosition.y - calculation.rectTransform.rect.height - 8f),
                        "The complete calculation and story occupy separate paragraphs in scroll content.");
                foreach (Text label in page.GetComponentsInChildren<Text>())
                {
                    if (!label.transform.IsChildOf(face))
                        Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(20), definition.SourceId + " / " + label.name);
                    Assert.That(label.resizeTextForBestFit, Is.False);
                    AssertTextFits(label, "card=" + card.Id + "; source=" + definition.SourceId + "; rank=" + card.UpgradeLevel +
                        "; canvasScale=" + label.GetComponentInParent<Canvas>().scaleFactor + "; faceScale=" + face.localScale +
                        "; font=" + label.fontSize + "; pixelsPerUnit=" + label.pixelsPerUnit + "; rect=" + label.rectTransform.rect.size +
                        "; text=" + label.text);
                    AssertInsidePaper(label.rectTransform.IsChildOf(content) ? content : page, label.rectTransform);
                }
                AssertScrollableContent(viewport, content);
                Assert.That(page.GetComponentsInChildren<Button>().Length, Is.Zero, "Dossier card pages are read-only.");
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                CollectionAssert.AreEqual(acquired, controller.Session.AcquiredSpecialCardIds);
                CollectionAssert.AreEquivalent(progress, controller.Session.SpecialCardExperience.ToArray());
                checkedCards++;
            }
            Assert.That(checkedCards, Is.EqualTo(36 + 90), "Audit every authored crew and rare-or-better component card; retired placeholders are not content.");
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
                    controller.Session.BattleCardPortDescription(card), TextAnchor.UpperCenter, null, null, 20, null, null);
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
            Assert.That(effect.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.EffectPosition));
            Assert.That(lore.rectTransform.anchoredPosition.y, Is.EqualTo(-12f),
                "The first details line needs real inset below the mask, not a relaxed mesh bound.");
            AssertTextFits(effect);
            AssertTextFits(lore);
            AssertCompleteCardFace("Small Canvas", definition.Card, page);
            RectTransform viewport = Find<RectTransform>("Small Canvas Special Card Details Viewport");
            AssertInsidePaper(page, viewport);
            AssertScrollableContent(viewport, Find<RectTransform>("Small Canvas Special Card Details Content"));
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
            Assert.That(Find<Text>("Tavern Special Card Effect").text, Does.Contain("恢复"));
            Assert.That(Find<Text>("Tavern Special Card Name").text, Does.Not.Contain("没有专属"));
            foreach (Text label in Find<RectTransform>("Tavern Dossier SpecialCards Page").GetComponentsInChildren<Text>())
                AssertTextFits(label);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(acquired, controller.Session.AcquiredSpecialCardIds);
        }

        [Test]
        public void CookSpecialCardTab_ShowsNewPersonalCardWithoutRecruitingOrSpendingGold()
        {
            CreatePreview(CoreGameplayPreviewScreen.TavernCrewDetail);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.WhiteShell, 0);
            controller.ConfigureEditorCrewDossier(4, 3);
            CoreSpecialCardDefinition cook = CoreSpecialCardCatalog.ForCrew("crew_white_shell_cook_flora");
            decimal gold = controller.Session.ProfileGold;
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            Find<Button>("DossierPageSpecialCards").onClick.Invoke();
            Assert.That(Find<Text>("Tavern Special Card Name").text, Is.EqualTo(cook.Card.Name));
            Assert.That(VisibleEffectWithoutLineBreaks(Find<Text>("Tavern Special Card Effect").text),
                Is.EqualTo(VisibleEffectWithoutLineBreaks(ExpectedDossierEffectText(cook.Card))));
            Assert.That(VisibleEffectWithoutLineBreaks(Find<Text>("Tavern Special Card Lore").text),
                Is.EqualTo(VisibleEffectWithoutLineBreaks(cook.Lore)));
            Assert.That(Find<Text>("Tavern Special Card Growth").text, Does.Contain("尚未获得"));
            Assert.That(Find<Text>("Tavern Dossier Page Counter").text, Is.EqualTo("第 4 / 4 页"));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(acquired, controller.Session.AcquiredSpecialCardIds);
        }

        [Test]
        public void PepeDossier_ShowsHisSupportCardAndGrowthWithoutRecruitingOrSpending()
        {
            CreatePreview(CoreGameplayPreviewScreen.TavernCrewDetail);
            CoreSpecialCardDefinition pepe = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_cook_pepe");
            controller.ConfigureEditorCrewDossier(4, 3);
            decimal gold = controller.Session.ProfileGold;
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            Find<Button>("DossierPageSpecialCards").onClick.Invoke();
            Assert.That(Find<Text>("Tavern Special Card Name").text, Does.Contain("同锅热汤"));
            Assert.That(Find<Text>("Tavern Special Card Effect").text, Does.Contain("格挡").And.Contain("抽"));
            Assert.That(Find<Text>("Tavern Special Card Stage").text, Is.EqualTo("策略 / 远程攻击 / 近战"));
            Assert.That(VisibleEffectWithoutLineBreaks(Find<Text>("Tavern Special Card Lore").text),
                Is.EqualTo(VisibleEffectWithoutLineBreaks(pepe.Lore)));
            Assert.That(Find<Text>("Tavern Special Card Growth").text, Does.Contain("尚未获得"));
            foreach (Text label in Find<RectTransform>("Tavern Dossier SpecialCards Page").GetComponentsInChildren<Text>())
                AssertTextFits(label);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(acquired, controller.Session.AcquiredSpecialCardIds);
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

        private void AssertComponentPage(int page)
        {
            for (int other = 0; other < ComponentPages.Length; other++)
                Assert.That(Find<RectTransform>("Component Dossier " + ComponentPages[other] + " Page").gameObject.activeSelf,
                    Is.EqualTo(page == other));
            Assert.That(Find<Text>("Component Dossier Page Counter").text, Is.EqualTo("第 " + (page + 1) + " / 4 页"));
            Assert.That(Find<Button>("Previous Component Dossier Page").interactable, Is.EqualTo(page > 0));
            Assert.That(Find<Button>("Next Component Dossier Page").interactable, Is.EqualTo(page < ComponentPages.Length - 1));
        }

        private static void AssertContained(Rect region, Rect item, string name)
        {
            Assert.That(item.xMin, Is.GreaterThanOrEqualTo(region.xMin - .1f), name);
            Assert.That(item.xMax, Is.LessThanOrEqualTo(region.xMax + .1f), name);
            Assert.That(item.yMin, Is.GreaterThanOrEqualTo(region.yMin - .1f), name);
            Assert.That(item.yMax, Is.LessThanOrEqualTo(region.yMax + .1f), name);
        }

        private static string VisibleEffectWithoutLineBreaks(string text) =>
            text.Replace("<b>", string.Empty).Replace("</b>", string.Empty)
                .Replace("\r", string.Empty).Replace("\n", string.Empty);

        private static string WithoutWhitespace(string text) =>
            new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());

        private string ExpectedDossierEffectText(CoreBattleCard card)
        {
            NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Boarding)
                ? NavalBattleStage.Boarding : card.IsAllowedInStage(NavalBattleStage.Naval)
                    ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
            return CardFaceLayout.FormatDescription(controller.Session.BattleCardFaceDescription(card, stage));
        }

        private RectTransform AssertCompleteCardFace(string prefix, CoreBattleCard card, RectTransform page)
        {
            RectTransform face = Find<RectTransform>(prefix + " Special Card Face");
            RawImage art = Find<RawImage>(prefix + " Special Card Art");
            Text name = Find<Text>(prefix + " Special Card Name");
            Text effect = Find<Text>(prefix + " Special Card Effect");
            Assert.That(face.sizeDelta, Is.EqualTo(CardFaceLayout.CardSize));
            bool reviewedSheet = prefix == "Component" || prefix == "Tavern";
            float frameScale = Mathf.Min(.86f, (page.rect.height - (reviewedSheet ? 36f : 12f)) / CardFaceLayout.FrameSize.y);
            Assert.That(face.localScale.x, Is.EqualTo(frameScale).Within(.001f));
            Assert.That(face.localScale.y, Is.EqualTo(face.localScale.x));
            Assert.That(art.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).size));
            Assert.That(art.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).center));
            Assert.That(art.texture, Is.Not.Null);
            Assert.That(name.text, Is.EqualTo(CardFaceLayout.DisplayName(card)));
            Assert.That(name.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.NameSize));
            Assert.That(VisibleEffectWithoutLineBreaks(effect.text),
                Is.EqualTo(VisibleEffectWithoutLineBreaks(ExpectedDossierEffectText(card))));
            Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize));
            Assert.That(effect.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.EffectPosition));
            Assert.That(effect.supportRichText, Is.True);
            Assert.That(name.transform.IsChildOf(face) && art.transform.IsChildOf(face) && effect.transform.IsChildOf(face), Is.True);
            Assert.That(face.GetComponentInChildren<BattleCardSurfaceGraphic>().CardType, Is.EqualTo(card.Type));
            Assert.That(face.GetComponentsInChildren<ScrollRect>(true), Is.Empty, "The face shares the same fixed layout as battle cards.");
            Assert.That(Find<Text>(prefix + " Special Card Stage").text, Does.Not.Contain("能量"),
                "Energy appears once in the blue cost crystal.");
            CardOrnamentGraphic[] ornaments = face.GetComponentsInChildren<CardOrnamentGraphic>(true);
            CardOrnamentGraphic energy = ornaments.Single(item => item.Kind == CardOrnamentKind.Energy);
            Assert.That(energy.GetComponentInChildren<Text>().text, Is.EqualTo(card.Energy.ToString()));
            Assert.That(energy.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EnergyIconSize));
            Assert.That(energy.rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.CostPosition(card, CardOrnamentKind.Energy)));
            CardOrnamentGraphic[] powder = ornaments.Where(item => item.Kind == CardOrnamentKind.Gunpowder).ToArray();
            Assert.That(powder.Length, Is.EqualTo(card.PowderCost > 0 ? 1 : 0));
            if (powder.Length > 0)
            {
                Assert.That(powder[0].GetComponentInChildren<Text>().text, Is.EqualTo(card.PowderCost.ToString()));
                Assert.That(powder[0].rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.PowderIconSize));
                Assert.That(powder[0].rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.CostPosition(card, CardOrnamentKind.Gunpowder)));
            }
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(card).size));
            Assert.That(frame.rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(card).center));
            Assert.That(frame.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(card)));
            AssertInsidePaper(page, frame.rectTransform);
            Rect frameBounds = BoundsIn(page, frame.rectTransform);
            Assert.That(frameBounds.yMax, Is.LessThanOrEqualTo(page.rect.yMax - 5.9f));
            Assert.That(frameBounds.yMin, Is.GreaterThanOrEqualTo(page.rect.yMin + 5.9f));
            Assert.That(frameBounds.center.y, Is.EqualTo(page.rect.center.y + (reviewedSheet ? 12f : 0f)).Within(.1f));
            foreach (Graphic graphic in face.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name + " leaves dossier navigation unobstructed");
            return face;
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                local.Max(point => point.x), local.Max(point => point.y));
        }

        private static void AssertTextFits(Text label, string context = "")
        {
            Assert.That(label.preferredHeight,
                Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f),
                label.name + " must retain its complete text at the authored font size. " + context);
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
            if (!label.name.Contains("Effect") && !label.name.Contains("Lore") && !label.name.Contains("Calculation")) return;
            if (label.name.EndsWith(" Special Card Effect", StringComparison.Ordinal) &&
                label.transform.parent.name.EndsWith(" Special Card Face", StringComparison.Ordinal))
            {
                RectTransform paper = label.transform.parent.Find(label.name + " Paper") as RectTransform;
                Assert.That(paper, Is.Not.Null, "The fixed card effect retains the authored PNG writing-area geometry.");
                Assert.That(paper.rect.size, Is.EqualTo(CardFaceLayout.EffectPaperSize));
                Assert.That(paper.anchoredPosition, Is.EqualTo(CardFaceLayout.EffectPaperPosition));
                Assert.That(paper.GetComponent<Image>().enabled, Is.False,
                    "The complete frame PNG supplies the visible writing paper.");
                // The fixed text field measures line layout; its surrounding
                // paper also accommodates the font's raised or lowered bearings.
                // At small canvas scales validate the complete rendered glyphs
                // against that actual visible surface in both axes.
                float rasterTolerance = 1f / scale;
                foreach (UIVertex vertex in generator.verts)
                {
                    Vector3 glyph = paper.InverseTransformPoint(label.rectTransform.TransformPoint(vertex.position / scale));
                    Assert.That(glyph.x, Is.InRange(paper.rect.xMin - rasterTolerance, paper.rect.xMax + rasterTolerance),
                        label.name + " generated a glyph more than one device pixel outside its writing surface horizontally at scale " + scale +
                        "; text=" + label.text);
                    Assert.That(glyph.y, Is.InRange(paper.rect.yMin - rasterTolerance, paper.rect.yMax + rasterTolerance),
                        label.name + " generated a glyph more than one device pixel outside its writing surface vertically at scale " + scale +
                        "; text=" + label.text);
                }
                return;
            }
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
