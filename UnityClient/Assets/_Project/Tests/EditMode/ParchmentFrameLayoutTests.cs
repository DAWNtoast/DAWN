using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ParchmentFrameLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private readonly Dictionary<string, int?> savedInts = new Dictionary<string, int?>();
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private const float ReadingGap = 6f;
        private const float PixelTolerance = .5f;

        [SetUp]
        public void PreservePlayerProfile()
        {
            Assert.That(Application.isPlaying, Is.False);
            foreach (string key in IntKeys().Distinct())
                savedInts[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            foreach (string key in StringKeys().Distinct())
                savedStrings[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            // Preserve real editor profiles even if a callback's save guard regresses.
            foreach (var entry in savedInts)
            {
                if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
            }
            foreach (var entry in savedStrings)
            {
                if (entry.Value != null) PlayerPrefs.SetString(entry.Key, entry.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
            }
            savedInts.Clear();
            savedStrings.Clear();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Upgrade_TitleGlyphsPagingAndActionsStayInsidePaintedRule(int stage)
        {
            CreatePreview(CoreGameplayPreviewScreen.CardUpgrade, stage);
            AssertWorkshopPaperControls("Battle Card Upgrade Panel",
                "Card Upgrade Heading", "Upgrade Selected Name", "Back From Card Upgrades", "Confirm Battle Card Upgrade");
            AssertWorkshopPaperControls("Workshop Preview Parchment", "Previous Upgrade Cards", "Next Upgrade Cards",
                "Workshop Preview Stage Preparation", "Workshop Preview Stage Naval", "Workshop Preview Stage Boarding",
                "Open Special Card Collection");
            AssertProfileUnchanged();
        }

        [Test]
        public void SpecialCollection_TitleAndFooterGlyphsAndControlsStayInsidePaintedRule()
        {
            CreatePreview(CoreGameplayPreviewScreen.SpecialCardCollection);
            AssertWorkshopPaperControls("Workshop Preview Parchment", "Previous Upgrade Cards", "Next Upgrade Cards",
                "Workshop Preview Stage Preparation", "Workshop Preview Stage Naval", "Workshop Preview Stage Boarding",
                "Open Special Card Collection", "Upgrade Collection Page");
            AssertWorkshopPaperControls("Workshop Forge Entry", "Card Upgrade Heading", "Workshop Upgrade Hammer Icon",
                "Workshop Upgrade Disabled Mark", "Open Battle Card Upgrades");
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                AssertWorkshopPaperControls("Workshop Stage " + stage, "Edit Stage Deck " + stage);
            foreach (RectTransform status in host.GetComponentsInChildren<RectTransform>(true).Where(item =>
                item.name.StartsWith("Special Card Deck Status ") || item.name.StartsWith("Configure Special Card Source ")))
                AssertWorkshopPaperControls("Workshop Preview Parchment", status.name);
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(item =>
                item.name.StartsWith("Special Card Source ") || item.name.StartsWith("Special Card Progress ")), Is.False);
            foreach (string removed in new[] { "Special Card Collection Panel", "Special Card Collection Heading", "Special Card Detail Active", "Special Card Read Only Notice" })
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == removed), Is.False, removed);
            Assert.That(Find<Button>("Open Battle Card Upgrades").interactable, Is.False);
            AssertProfileUnchanged();
        }

        [Test]
        public void DeckBuilder_PagingAndReturnControlsStayInsideTheMarketPaper()
        {
            CreatePreview(CoreGameplayPreviewScreen.DeckBuilder);
            AssertWorkshopPaperControls("Deck Builder Parchment", "Previous Battle Card Collection Page",
                "Next Battle Card Collection Page", "Battle Card Collection Page");
            AssertWorkshopPaperControls("Deck Builder Deck Rail", "Back To Card Workshop Chests");
            Assert.That(Find<Text>("Deck Builder Status").text, Is.Empty,
                "A valid deck needs no explanatory status caption.");

            // Model an unfinished edit without changing the saved, valid deck.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var controllerType = typeof(CoreGameplayDemoController);
            var stage = (NavalBattleStage)controllerType.GetField("selectedBattleDeckStage", flags).GetValue(controller);
            int[] validDeck = controller.Session.GetBattleDeck(stage).ToArray();
            int draftCount = CoreVoyageSession.MinimumBaseBattleDeckSize - 1;
            Assert.That(validDeck.Length, Is.GreaterThanOrEqualTo(CoreVoyageSession.MinimumBaseBattleDeckSize));
            controllerType.GetField("deckBuilderDraft", flags).SetValue(controller, validDeck.Take(draftCount).ToList());
            controllerType.GetField("deckBuilderDraftSession", flags).SetValue(controller, controller.Session);
            controllerType.GetField("deckBuilderDraftStage", flags).SetValue(controller, stage);
            controllerType.GetField("deckBuilderDraftPort", flags).SetValue(controller, controller.Session.CurrentPort);
            Click("Back To Card Workshop Chests");
            Assert.That(Find<Text>("Deck Builder Status").text,
                Is.EqualTo("基础牌组至少需要 " + CoreVoyageSession.MinimumBaseBattleDeckSize +
                    " 张卡牌（当前 " + draftCount + " 张，请补足后离开）"));
            Assert.That(Find<RectTransform>("Deck Builder Panel").gameObject.activeInHierarchy, Is.True,
                "The real return action must keep an incomplete deck open.");
            CollectionAssert.AreEqual(validDeck, controller.Session.GetBattleDeck(stage),
                "An incomplete draft must not replace the last valid gameplay deck.");
            AssertWorkshopPaperControls("Deck Builder Deck Rail", "Deck Builder Status", "Back To Card Workshop Chests");
            AssertProfileUnchanged();
        }

        [Test]
        public void Departure_MainAndCrewConfigurationKeepTheirRealActionsWithinTheRule()
        {
            CreatePreview(CoreGameplayPreviewScreen.VoyageDeparture);
            Image routePaper = DepartureSurface("Route Selection");
            AssertGlyphs(routePaper, host.transform, "Departure Route Summary", "Seed Label");
            AssertRect(routePaper, Find<Toggle>("Use Chart Seed").GetComponent<RectTransform>());
            AssertButtons(routePaper, "Departure Treasure Seed Picker", "Route 0", "Route 1", "Route 2");
            foreach (Button destination in routePaper.GetComponentsInChildren<Button>(false)
                .Where(button => button.name.StartsWith("Departure Destination ", StringComparison.Ordinal)))
                AssertRect(routePaper, destination.GetComponent<RectTransform>());
            Image paper = DepartureSurface("Voyage Setup");
            if (paper.sprite == null)
                AssertGlyphs(paper, host.transform, "Setup Title", "Departure Dock Ordinary Readout", "Departure Dock Special Readout", "Departure Dock Speed Readout");
            else AssertGlyphs(paper, host.transform, "Setup Title", "DepartureOrdinaryCrewCount", "DepartureCrewSpeedPreview");
            AssertButtons(paper, "Start Voyage", "Departure Buy Provisions", "Adjust Voyage Supplies",
                "Configure Voyage Battle Decks", "Configure Voyage Crew",
                "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton");
            Click("Configure Voyage Crew");
            Image retainedSetup = DepartureSurface("Voyage Setup");
            AssertGlyphs(retainedSetup, host.transform, "Setup Title");
            paper = Paper("Departure Crew Configuration", MarketShopSkin.Paper);
            AssertGlyphs(paper, host.transform, "Departure Crew Configuration Title", "Departure Crew Total");
            AssertButtons(paper, "Back To Voyage Setup", "Confirm Departure Crew");
            AssertRect(paper, Find<ScrollRect>("Departure Special Crew Scroll").GetComponent<RectTransform>());
            Click("Confirm Departure Crew");
            AssertProfileUnchanged();
        }

        [TestCase("Navigation Chart")]
        [TestCase("Navigation Controls")]
        public void Navigation_ReadingPanelsKeepContentInsideCurrentTimberFrames(string panel)
        {
            CreatePreview(CoreGameplayPreviewScreen.Navigation);
            Image paper = Find<Image>(panel);
            Image frame = Find<Image>(panel + " Timber Frame");
            Assert.That(frame.sprite.texture.name, Is.EqualTo("navigation_panel_frame_v3"));
            Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(frame.fillCenter, Is.False);
            if (panel == "Navigation Chart")
            {
                Assert.That(paper.sprite, Is.Null, "The map retains its solid sea backing.");
                Assert.That(Find<Image>(NavigationInterfaceSkin.TitleStripName).sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
                AssertGlyphs(paper, host.transform, "Map Title", "Water Food Endurance");
            }
            else if (panel == "Navigation Controls")
            {
                Assert.That(paper.sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
                Assert.That(paper.type, Is.EqualTo(Image.Type.Tiled));
                AssertGlyphs(paper, host.transform, "Navigation Title", "Distance Label", "Forecast");
                AssertButtons(paper, "Execute Sail", "Safe Return", "Forecast Details");
                AssertRect(paper, Find<Slider>("Distance Slider").GetComponent<RectTransform>());
                foreach (Button heading in paper.GetComponentsInChildren<Button>(false)
                    .Where(button => button.name.StartsWith("Heading ", StringComparison.Ordinal)))
                    AssertRect(paper, heading.GetComponent<RectTransform>());
            }
            AssertProfileUnchanged();
        }

        [TestCase(TreasureMapPreviewState.MapDecoded)]
        [TestCase(TreasureMapPreviewState.RewardClaim)]
        public void TreasureFolio_HeadingCardsAndBrowserControlsRespectTheRule(TreasureMapPreviewState state)
        {
            CreatePreview(CoreGameplayPreviewScreen.TreasureMap);
            controller.ConfigureEditorTreasureState(state);
            Image paper = Paper("Treasure Folio", MarketShopSkin.Paper);
            AssertGlyphs(paper, host.transform, "Treasure Folio Title", "Treasure Map Details 0", "Treasure Folio Page");
            AssertButtons(paper, "Treasure Close Folio", "Treasure Folio Back",
                "Treasure Folio Previous Page", "Treasure Folio Next Page", "Treasure Browse Unused", "Treasure Browse Used");
            AssertRect(paper, Find<RectTransform>("Treasure Folio Card 0"));
            if (state == TreasureMapPreviewState.MapDecoded)
                AssertRect(paper, Find<RectTransform>("Treasure Map Preview 0"));
            AssertProfileUnchanged();
        }

        [Test]
        public void Investment_OverlaysStayWithinTheApprovedScenesPaintedReadingAreas()
        {
            CreatePreview(CoreGameplayPreviewScreen.Maintenance);
            Canvas.ForceUpdateCanvases();
            Image summary = Find<Image>("Port Investment Summary");
            Image ledger = Find<Image>("Port Investment Ledger");
            foreach (Image overlay in new[] { summary, ledger })
            {
                Assert.That(overlay.sprite, Is.Null, overlay.name);
                Assert.That(overlay.color.a, Is.Zero, overlay.name + " preserves the scene's own material");
                Assert.That(overlay.raycastTarget, Is.False, overlay.name);
            }
            // Independently measured on the 1672 x 941 approved source image.
            // These bounds catch both cropped artwork and displaced UI overlays.
            Rect benefitArea = Rect.MinMaxRect(180f, 680f, 980f, 825f);
            Rect ledgerArea = Rect.MinMaxRect(1150f, 180f, 1560f, 750f);
            Rect projectArea = Rect.MinMaxRect(135f, 123f, 420f, 190f);
            foreach (Text label in summary.GetComponentsInChildren<Text>(true))
            {
                AssertInvestmentPaintedBounds(label.rectTransform, benefitArea);
                AssertCompleteMarketText(label);
            }
            foreach (Text label in ledger.GetComponentsInChildren<Text>(true))
            {
                AssertInvestmentPaintedBounds(label.rectTransform, ledgerArea);
                AssertCompleteMarketText(label);
            }
            foreach (RawImage icon in ledger.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("Port Investment Requirement Icon ")))
            {
                Assert.That(icon.texture, Is.Not.Null, icon.name);
                AssertInvestmentPaintedBounds(icon.rectTransform, ledgerArea);
            }
            foreach (string name in new[] { "Confirm Port Investment", "Open Port Market", "Open Investment Warehouse" })
                AssertInvestmentPaintedBounds(Find<Button>(name).GetComponent<RectTransform>(), ledgerArea);
            foreach (string name in new[] { "Project Name", "Project Caption", "Current Level", "Next Level" })
            {
                Text label = Find<Text>("Port Investment " + name);
                AssertInvestmentPaintedBounds(label.rectTransform, projectArea);
                AssertCompleteMarketText(label);
            }
            AssertProfileUnchanged();
        }

        private void AssertInvestmentPaintedBounds(RectTransform item, Rect sourceArea)
        {
            RawImage backdrop = Find<RawImage>("Maintenance Background");
            Assert.That(backdrop.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            RectTransform art = backdrop.rectTransform;
            var corners = new Vector3[4]; item.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = art.InverseTransformPoint(corner);
                float x = (local.x - art.rect.xMin) / art.rect.width * 1672f;
                float y = (art.rect.yMax - local.y) / art.rect.height * 941f;
                Assert.That(x, Is.InRange(sourceArea.xMin - .5f, sourceArea.xMax + .5f), item.name + " painted horizontal margin");
                Assert.That(y, Is.InRange(sourceArea.yMin - .5f, sourceArea.yMax + .5f), item.name + " painted vertical margin");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Warehouse_ValuablesStorageAndRareRespectTheCabinetOpening(int page)
        {
            CreatePreview(CoreGameplayPreviewScreen.Warehouse);
            if (page == 1) controller.ConfigureEditorPreviewWarehouseStorage(0);
            if (page == 2) controller.ConfigureEditorPreviewWarehouseRare(0);
            Image paper = Find<Image>("Warehouse Ledger");
            Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper));
            Assert.That(paper.color.a, Is.EqualTo(1f));
            AssertButtons(paper, "Warehouse Tab Valuables", "Warehouse Tab Storage", "Warehouse Tab Rare");
            if (page == 0)
            {
                AssertButtons(paper, "Warehouse Treasure Page Previous", "Warehouse Treasure Page Next",
                    "Warehouse Collection Catalog", "Warehouse Display Selected", "Warehouse Remove Selected");
                AssertGlyphs(paper, host.transform, "Warehouse Treasure Page");
            }
            else
            {
                string hintName = page == 1 ? "Warehouse Storage Hint" : "Warehouse Rare Hint";
                // The default preview notice is empty. Exercise the actual footer Text
                // with a representative result, without mutating inventories or saving.
                Find<Text>(hintName).text = "本港库存已更新，随船货物已核对。";
                AssertGlyphs(paper, host.transform,
                    page == 1 ? "Warehouse Storage Summary" : "Warehouse Rare Summary", hintName);
                if (page == 2) AssertButtons(paper, "Warehouse Store All Rare");
                foreach (Button row in paper.GetComponentsInChildren<Button>(false).Where(button =>
                    button.name.StartsWith(page == 1 ? "Warehouse Storage Row " : "Warehouse Rare Row ", StringComparison.Ordinal)))
                    AssertRect(paper, row.GetComponent<RectTransform>());
            }
            AssertProfileUnchanged();
        }

        [Test]
        public void Market_FourShopsLeaveTheSceneVisibleAndGoodsRemainInsideTheirDrawer()
        {
            CreatePreview(CoreGameplayPreviewScreen.Market);
            Assert.That(Find<RectTransform>("Market Item Detail").gameObject.activeInHierarchy, Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Market Footer"), Is.False);
            Assert.That(Find<Button>("Market Detail Storage Mode").gameObject.activeInHierarchy, Is.False);
            foreach (MarketShopKind shop in Enum.GetValues(typeof(MarketShopKind)))
            {
                Assert.That(Find<Button>("Market Shop " + shop).gameObject.activeInHierarchy, Is.True);
                InvokeMarketPreview("OpenMarketShop",shop);
                Image paper = Paper("Market Item Detail",MarketShopSkin.Paper);
                AssertGlyphs(paper,host.transform,"Market Shop Title");
                AssertButtons(paper,"Close Market Shop","Cancel Market Trade","Market Detail Storage Mode");
                foreach (PortMarketItem item in MarketSceneLayout.ItemsForShop(shop))
                {
                    string key = PortMarketCatalog.Key(item);
                    AssertRect(paper,Find<RectTransform>("Market Row " + key));
                    AssertButtons(paper,"Market Item Icon " + key);
                    AssertGlyphs(paper,host.transform,"Market Item Name " + key);
                }
            }
            AssertProfileUnchanged();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Market_ItemDetailAndQuantitySelectionKeepTheirControlsWithinThePaintedRule(bool buying)
        {
            CreatePreview(CoreGameplayPreviewScreen.Market);
            Button storageMode = Find<Button>("Market Detail Storage Mode");
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                OpenMarketPreview(item);
                StartMarketPreviewTrade(buying);
                Image paper = Paper("Market Item Detail",MarketShopSkin.Paper);
                Assert.That(Find<Button>("Market Detail Storage Mode"),Is.SameAs(storageMode));
                Assert.That(storageMode.gameObject.activeInHierarchy,Is.True);
                AssertButtons(paper,"Market Detail Storage Mode");
                Text itemName = Find<Text>("Market Item Name " + key);
                Assert.That(itemName.gameObject.activeInHierarchy,Is.True);
                Assert.That(itemName.text,Is.EqualTo(PortMarketCatalog.Name(item)));
                int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item),PortMarketCatalog.SaleStep(item));
                int sellLot = PortMarketCatalog.SaleStep(item);
                decimal buyPrice = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort,item,controller.Session.GameDateTime,buyLot,controller.Session.Cargo.Fame);
                decimal sellPrice = PortMarketCatalog.CalculateSellRevenue(controller.Session.CurrentPort,item,controller.Session.GameDateTime,sellLot);
                Assert.That(Find<Text>("Market Detail Buy Price").text,Does.Contain(PortMarketCatalog.FormatPrice(buyPrice)));
                Assert.That(Find<Text>("Market Detail Sell Price").text,Does.Contain(PortMarketCatalog.FormatPrice(sellPrice)));
                AssertButtons(paper,"Market Buy " + key,"Market Sell " + key,"Market Inventory Toggle",
                    "Confirm Market " + (buying ? "Buy " : "Sell ") + key,"Market Quantity Minus","Market Quantity Plus","Market Quantity Max");
                InputField quantityInput = Find<InputField>("Market Quantity Input " + key);
                Assert.That(quantityInput.gameObject.activeInHierarchy,Is.True);
                AssertRect(paper,quantityInput.GetComponent<RectTransform>());
                AssertRect(paper,Find<Slider>("Market Quantity Slider " + key).GetComponent<RectTransform>());
                AssertGlyphs(paper,host.transform,"Market Item Name " + key,"Market Detail Buy Price","Market Detail Sell Price","Market Quantity Label","Market Trade Total");
                SetMarketPreviewInventoryExpanded(true);
                Text inventory = Find<Text>((buying ? "Market Quota " : "Market Holdings ") + key);
                Assert.That(inventory.gameObject.activeInHierarchy,Is.True);
                AssertCompleteMarketText(inventory);
                AssertGlyph(paper,inventory);
            }
            AssertProfileUnchanged();
        }
        [Test]
        public void MainMenu_AchievementHeadingAndFooterStayWithinTheRuleAcrossPages()
        {
            host = new GameObject("Parchment Menu Frame Preview");
            host.AddComponent<MainMenuController>();
            Click("Achievements");
            Image paper = Paper("Achievements Card", MarketShopSkin.Paper);
            AssertGlyphs(paper, paper.transform, "Modal Eyebrow", "Modal Title", "Achievements Page Indicator");
            AssertButtons(paper, "Achievements Previous Page", "Achievements Next Page", "Back From Achievements");
            Click("Achievements Next Page");
            AssertGlyphs(paper, paper.transform, "Achievements Page Indicator");
            Click("Back From Achievements");
            AssertProfileUnchanged();
        }

        [Test]
        public void EditorTransactionCallbacks_DoNotCommitThePlayerProfile()
        {
            CreatePreview(CoreGameplayPreviewScreen.CardUpgrade);
            Click("Confirm Battle Card Upgrade");
            AssertProfileUnchanged();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.GrayGull, 0);
            decimal gold = controller.Session.ProfileGold;
            Find<Button>("Confirm Port Investment").onClick.Invoke();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            AssertProfileUnchanged();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Warehouse, CoreGameplayPreviewPort.GrayGull, 0);
            Click("Warehouse Display Selected");
            Click("Warehouse Remove Selected");
            controller.ConfigureEditorPreviewWarehouseStorage(0);
            int wood = controller.Session.Cargo.Wood;
            Find<Button>("Warehouse Storage Row wood").onClick.Invoke();
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(wood));
            controller.ConfigureEditorPreviewWarehouseRare(0);
            int gems = controller.Session.Cargo.Gem;
            Find<Button>("Warehouse Store All Rare").onClick.Invoke();
            Assert.That(controller.Session.Cargo.Gem, Is.EqualTo(gems));
            AssertProfileUnchanged();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.GrayGull, 0);
            int water = controller.Session.Cargo.Water;
            gold = controller.Session.ProfileGold;
            foreach (bool buying in new[] { true, false })
            {
                OpenMarketPreview(PortMarketItem.Water);
                Assert.That(Find<Button>(buying ? "Market Buy water" : "Market Sell water").interactable, Is.False);
                StartMarketPreviewTrade(buying);
                Slider quantity = Find<Slider>("Market Quantity Slider water");
                Assert.That(quantity.interactable, Is.False);
                quantity.onValueChanged.Invoke(7f);
                Button confirm = Find<Button>(buying ? "Confirm Market Buy water" : "Confirm Market Sell water");
                Assert.That(confirm.interactable, Is.False);
                confirm.onClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                AssertProfileUnchanged();
            }
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
            AssertProfileUnchanged();
        }

        [Test]
        public void InnerRuleProjectionRejectsAnOuterContainedEdgeControlButAcceptsCenteredGlyphs()
        {
            CreatePreview(CoreGameplayPreviewScreen.Market);
            OpenMarketPreview(PortMarketItem.Water);
            Image paper = Paper("Market Item Detail", MarketShopSkin.Paper);
            Rect outer = paper.rectTransform.rect;
            Rect edgeControl = new Rect(outer.center.x - 4f, outer.yMin + 2f, 8f, 6f);
            Assert.That(Contains(outer, edgeControl), Is.True, "The old outer-paper check would accept this control.");
            Assert.That(Contains(InnerRule(paper), edgeControl), Is.False, "The painted rule excludes it.");
            GameObject labelHost = new GameObject("Whitespace Frame Probe", typeof(RectTransform), typeof(Text));
            labelHost.transform.SetParent(paper.transform, false);
            Text label = labelHost.GetComponent<Text>();
            label.font = Find<Text>("Market Shop Title").font;
            label.fontSize = 24;
            label.text = "内框";
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.sizeDelta = outer.size;
            Assert.That(Contains(InnerRule(paper), label.rectTransform.rect), Is.False,
                "Large empty text rectangles are intentionally not the glyph test.");
            AssertGlyph(paper, label);
            AssertProfileUnchanged();
        }

        private void CreatePreview(CoreGameplayPreviewScreen screen, int stage = 0)
        {
            host = new GameObject("Parchment Frame Preview");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, stage);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True, screen.ToString());
            Canvas.ForceUpdateCanvases();
        }

        private T Find<T>(string name) where T : Component => FindIn<T>(host.transform, name);

        private static T FindIn<T>(Transform scope, string name) where T : Component
        {
            T[] matches = scope.GetComponentsInChildren<T>(true).Where(item => item.name == name).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), name);
            return matches[0];
        }

        private void AssertWorkshopPaperControls(string name, params string[] controls)
        {
            Canvas.ForceUpdateCanvases();
            Image texture = Find<Image>(name + " Texture");
            Assert.That(texture.sprite, Is.Not.Null, name);
            Assert.That(texture.sprite, Is.SameAs(MarketShopSkin.Paper), name + " uses the shared market reading surface.");
            foreach (string control in controls)
            {
                RectTransform rect = Find<RectTransform>(control);
                Text label = rect.GetComponent<Text>();
                if (label != null) AssertGlyph(texture, label);
                else AssertRect(texture, rect);
            }
        }

        private Image DepartureSurface(string name)
        {
            if (!PortFacilityArtCatalog.UsesPaintedDepartureScene(controller.Session.CurrentPort))
                return Paper(name, DepartureSkin.Paper);
            Image surface = Find<Image>(name);
            Assert.That(surface.sprite, Is.Null, name + " preserves the regional painted dock");
            Assert.That(surface.color.a, Is.Zero, name);
            Assert.That(surface.raycastTarget, Is.False, name + " leaves interaction to its authored controls");
            Assert.That(surface.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)), name);
            return surface;
        }

        private Image Paper(string name, Sprite expected)
        {
            Canvas.ForceUpdateCanvases();
            Image image = Find<Image>(name);
            Assert.That(image.sprite, Is.SameAs(expected), name);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), name + " requires the shared market paper sprite");
            Assert.That(image.sprite.border.x, Is.GreaterThan(0f), name);
            return image;
        }

        private void Click(string name) => Find<Button>(name).onClick.Invoke();

        private void OpenMarketPreview(PortMarketItem item) => InvokeMarketPreview("OpenMarketItemDetail", item);

        private void StartMarketPreviewTrade(bool buying) => InvokeMarketPreview("StartMarketTrade", buying);

        private void SetMarketPreviewInventoryExpanded(bool expanded)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CoreGameplayDemoController).GetField("marketInventoryExpanded", flags).SetValue(controller, expanded);
            typeof(CoreGameplayDemoController).GetMethod("RefreshMarketItemDetailPage", flags).Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
        }

        private void InvokeMarketPreview(string name, object argument)
        {
            var method = typeof(CoreGameplayDemoController).GetMethod(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(controller, new[] { argument });
            Canvas.ForceUpdateCanvases();
        }

        private void AssertButtons(Image paper, params string[] names)
        {
            foreach (string name in names) AssertRect(paper, Find<Button>(name).GetComponent<RectTransform>());
        }

        private static void AssertRect(Image paper, RectTransform component)
        {
            AssertInside(paper, BoundsInPaper(paper, component), component.name + " complete rectangle");
        }

        private static Rect BoundsInPaper(Image paper, RectTransform component)
        {
            var corners = new Vector3[4];
            component.GetWorldCorners(corners);
            Vector3[] local = corners.Select(paper.transform.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(p => p.x), local.Min(p => p.y),
                local.Max(p => p.x), local.Max(p => p.y));
        }

        private static void AssertGlyphs(Image paper, Transform scope, params string[] names)
        {
            foreach (string name in names) AssertGlyph(paper, FindIn<Text>(scope, name));
        }

        private static void AssertCompleteMarketText(Text label)
        {
            Assert.That(label.raycastTarget, Is.False, label.name);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + PixelTolerance),
                label.name + " / complete lines fit their height");
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + PixelTolerance),
                label.name + " / complete lines fit their width");
            TextGenerationSettings visibleSettings = label.GetGenerationSettings(label.rectTransform.rect.size);
            TextGenerationSettings completeSettings = visibleSettings;
            completeSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
            completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var visible = new TextGenerator())
            using (var complete = new TextGenerator())
            {
                Assert.That(visible.Populate(label.text, visibleSettings), Is.True, label.name);
                Assert.That(complete.Populate(label.text, completeSettings), Is.True, label.name);
                Assert.That(visible.vertexCount, Is.GreaterThanOrEqualTo(4), label.name + " / visible glyphs");
                Assert.That(visible.characterCount, Is.GreaterThan(0), label.name + " / visible characters");
                Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), label.name + " / no clipped characters");
                Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " / no clipped glyphs");
            }
        }

        private static void AssertGlyph(Image paper, Text label)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(label.text, Is.Not.Empty, label.name);
            using (var generator = new TextGenerator())
            {
                Assert.That(generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size)), Is.True, label.name);
                IList<UIVertex> vertices = generator.verts;
                float unitsPerPixel = 1f / label.pixelsPerUnit;
                // Match the installed UGUI Text.OnPopulateMesh pixel rounding.
                Vector2 first = vertices.Count == 0 ? Vector2.zero : (Vector2)vertices[0].position * unitsPerPixel;
                Vector2 rounding = label.PixelAdjustPoint(first) - first;
                var visible = new List<Vector3>();
                for (int quad = 0; quad + 3 < vertices.Count; quad += 4)
                {
                    Vector3[] points = Enumerable.Range(0, 4).Select(i => vertices[quad + i].position).ToArray();
                    // Spaces and any terminal empty quad must not add the local origin.
                    if (points.Max(p => p.x) - points.Min(p => p.x) <= .001f ||
                        points.Max(p => p.y) - points.Min(p => p.y) <= .001f) continue;
                    foreach (Vector3 point in points)
                    {
                        Vector3 local = point * unitsPerPixel + (Vector3)rounding;
                        visible.Add(paper.transform.InverseTransformPoint(label.transform.TransformPoint(local)));
                    }
                }
                Assert.That(visible, Is.Not.Empty, label.name + " must render visible glyphs");
                AssertInside(paper, Rect.MinMaxRect(visible.Min(p => p.x), visible.Min(p => p.y),
                    visible.Max(p => p.x), visible.Max(p => p.y)), label.name + " rendered glyphs");
            }
        }

        private static void AssertInside(Image paper, Rect bounds, string context)
        {
            Rect safe = InnerRule(paper);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - PixelTolerance), context + " / left painted rule");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + PixelTolerance), context + " / right painted rule");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - PixelTolerance), context + " / bottom painted rule");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + PixelTolerance), context + " / top painted rule");
        }

        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin && inner.xMax <= outer.xMax &&
            inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;

        private static Rect InnerRule(Image image)
        {
            if (image.sprite != MarketShopSkin.Paper &&
                (image.name == "Navigation Chart" || image.name == "Navigation Controls"))
            {
                // The navigation redesign uses a thin timber rim over a sea
                // surface or tiled paper, not the old inset aged-paper rule.
                Rect panel = image.rectTransform.rect;
                return Rect.MinMaxRect(panel.xMin + 4f, panel.yMin + 4f, panel.xMax - 4f, panel.yMax - 4f);
            }
            if ((image.name == "Route Selection" || image.name == "Voyage Setup") && image.sprite == null)
                return image.rectTransform.rect; // Scene hotspots use the full painted 16:9 surface.
            if (image.name == "Warehouse Ledger" && image.sprite == null)
            {
                Rect opening = image.rectTransform.rect;
                return Rect.MinMaxRect(opening.xMin + 20f, opening.yMin + 20f, opening.xMax - 20f, opening.yMax - 20f);
            }
            // Every reading surface shares the market sprite. Follow its actual
            // sliced border and each image's density, including compact boards.
            Sprite sprite = image.sprite;
            Assert.That(sprite, Is.Not.Null, image.name);
            Assert.That(sprite, Is.SameAs(MarketShopSkin.Paper), image.name);
            Rect rect = image.GetPixelAdjustedRect();
            float ppu = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
            Vector4 border = sprite.border / ppu;
            for (int axis = 0; axis < 2; axis++)
            {
                float originalSize = image.rectTransform.rect.size[axis];
                if (originalSize > 0f)
                { border[axis] *= rect.size[axis] / originalSize; border[axis + 2] *= rect.size[axis] / originalSize; }
                float combined = border[axis] + border[axis + 2];
                if (combined > rect.size[axis])
                { border[axis] *= rect.size[axis] / combined; border[axis + 2] *= rect.size[axis] / combined; }
            }
            // Nine-slice padding is wider than the visible painted frame.
            for (int edge = 0; edge < 4; edge++)
                border[edge] *= MarketShopSkin.PaperReadingBorder / sprite.border[edge];
            return Rect.MinMaxRect(rect.xMin + border.x + ReadingGap, rect.yMin + border.y + ReadingGap,
                rect.xMax - border.z - ReadingGap, rect.yMax - border.w - ReadingGap);
        }

        private void AssertProfileUnchanged()
        {
            foreach (var entry in savedInts)
            {
                Assert.That(PlayerPrefs.HasKey(entry.Key), Is.EqualTo(entry.Value.HasValue), entry.Key);
                if (entry.Value.HasValue) Assert.That(PlayerPrefs.GetInt(entry.Key), Is.EqualTo(entry.Value.Value), entry.Key);
            }
            foreach (var entry in savedStrings)
            {
                Assert.That(PlayerPrefs.HasKey(entry.Key), Is.EqualTo(entry.Value != null), entry.Key);
                if (entry.Value != null) Assert.That(PlayerPrefs.GetString(entry.Key), Is.EqualTo(entry.Value), entry.Key);
            }
        }

        private static readonly string[] Ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private static readonly string[] CargoResources = { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" };

        private static IEnumerable<string> IntKeys()
        {
            yield return CoreGameplayDemoController.ProfileGoldKey;
            yield return CoreGameplayDemoController.SelectedShipKey;
            yield return CoreGameplayDemoController.TutorialCompletedKey;
            yield return CoreGameplayDemoController.TutorialRewardGrantedKey;
            yield return CoreGameplayDemoController.GuidedTutorialCheckpointKey;
            yield return CoreGameplayDemoController.GuidedTutorialWarehousePendingKey;
            foreach (string resource in CargoResources) yield return CoreGameplayDemoController.CargoKey(resource);
            foreach (string port in Ports)
            {
                yield return CoreGameplayDemoController.PortInvestmentKey(port);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    yield return CoreGameplayDemoController.MarketPurchaseKey(port, CoreVoyageSession.NewCampaignStartDate(DateTime.Now), item);
            }
        }

        private static IEnumerable<string> StringKeys()
        {
            yield return ProfileGoldPersistence.DecimalKey;
            yield return CoreGameplayDemoController.CurrentPortKey;
            yield return CoreGameplayDemoController.WarehouseOwnedKey;
            yield return CoreGameplayDemoController.WarehouseDisplayKey;
            yield return CoreGameplayDemoController.PortWarehouseCargoKey;
            yield return CoreGameplayDemoController.BattleDeckKey;
            yield return CoreGameplayDemoController.BattleCardUpgradeKey;
            yield return CoreGameplayDemoController.SpecialCardExperienceKey;
            yield return CoreGameplayDemoController.AcquiredSpecialCardsKey;
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                yield return CoreGameplayDemoController.StageBattleDeckKey(stage);
        }
    }
}
