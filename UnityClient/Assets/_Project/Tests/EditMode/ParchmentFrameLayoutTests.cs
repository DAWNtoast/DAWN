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
        public void Departure_MainAndCrewConfigurationKeepTheirRealActionsWithinTheRule()
        {
            CreatePreview(CoreGameplayPreviewScreen.VoyageDeparture);
            Image routePaper = Paper("Route Selection", DepartureSkin.Paper);
            AssertGlyphs(routePaper, host.transform, "Departure Route Summary", "Seed Label");
            AssertRect(routePaper, Find<Toggle>("Use Chart Seed").GetComponent<RectTransform>());
            AssertButtons(routePaper, "Departure Treasure Seed Picker", "Route 0", "Route 1", "Route 2");
            foreach (Button destination in routePaper.GetComponentsInChildren<Button>(false)
                .Where(button => button.name.StartsWith("Departure Destination ", StringComparison.Ordinal)))
                AssertRect(routePaper, destination.GetComponent<RectTransform>());
            Image paper = Paper("Voyage Setup", DepartureSkin.Paper);
            AssertGlyphs(paper, host.transform, "Setup Title", "DepartureOrdinaryCrewCount", "DepartureCrewSpeedPreview");
            AssertButtons(paper, "Start Voyage", "Departure Buy Provisions", "Adjust Voyage Supplies",
                "Configure Voyage Battle Decks", "Configure Voyage Crew",
                "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton");
            Click("Configure Voyage Crew");
            paper = Paper("Voyage Setup", DepartureSkin.Paper);
            AssertGlyphs(paper, host.transform, "Setup Title", "Departure Crew Configuration Title", "Departure Crew Total");
            AssertButtons(paper, "Back To Voyage Setup", "Confirm Departure Crew");
            AssertRect(paper, Find<Slider>("Departure Ordinary Crew Slider").GetComponent<RectTransform>());
            Click("Confirm Departure Crew");
            AssertProfileUnchanged();
        }

        [TestCase("Navigation Chart")]
        [TestCase("Navigation Controls")]
        public void Navigation_ReadingPanelsReserveTheDrawnRule(string panel)
        {
            CreatePreview(CoreGameplayPreviewScreen.Navigation);
            Image paper = Paper(panel, NauticalSurfaceSkin.ReadingPaper);
            if (panel == "Navigation Chart")
                AssertGlyphs(paper, host.transform, "Map Title", "Water Food Endurance");
            else if (panel == "Navigation Controls")
            {
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
            Image paper = Paper("Treasure Folio", NauticalSurfaceSkin.ReadingPaper);
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
            Assert.That(paper.sprite, Is.Null, "The regional cabinet replaces the collection paper.");
            Assert.That(paper.color.a, Is.Zero);
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
            Assert.That(Find<Button>("Market Storage Mode").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<Button>("Market Open Investment").gameObject.activeInHierarchy, Is.True);
            foreach (MarketShopKind shop in Enum.GetValues(typeof(MarketShopKind)))
            {
                Assert.That(Find<Button>("Market Shop " + shop).gameObject.activeInHierarchy, Is.True);
                InvokeMarketPreview("OpenMarketShop",shop);
                Image paper = Paper("Market Item Detail",NauticalSurfaceSkin.ReadingPaper);
                AssertGlyphs(paper,host.transform,"Market Shop Title");
                AssertButtons(paper,"Close Market Shop","Cancel Market Trade");
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
            Button storageMode = Find<Button>("Market Storage Mode");
            Button investment = Find<Button>("Market Open Investment");
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                OpenMarketPreview(item);
                StartMarketPreviewTrade(buying);
                Image paper = Paper("Market Item Detail",NauticalSurfaceSkin.ReadingPaper);
                Assert.That(Find<Button>("Market Storage Mode"),Is.SameAs(storageMode));
                Assert.That(Find<Button>("Market Open Investment"),Is.SameAs(investment));
                Assert.That(storageMode.gameObject.activeInHierarchy && investment.gameObject.activeInHierarchy,Is.True);
                Assert.That(Find<Text>("Market Detail Name").text,Is.EqualTo(PortMarketCatalog.Name(item)));
                int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item),PortMarketCatalog.SaleStep(item));
                int sellLot = PortMarketCatalog.SaleStep(item);
                decimal buyPrice = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort,item,controller.Session.GameDateTime,buyLot,controller.Session.Cargo.Fame);
                decimal sellPrice = PortMarketCatalog.CalculateSellRevenue(controller.Session.CurrentPort,item,controller.Session.GameDateTime,sellLot);
                Assert.That(Find<Text>("Market Detail Buy Price").text,Does.Contain(PortMarketCatalog.FormatPrice(buyPrice)));
                Assert.That(Find<Text>("Market Detail Sell Price").text,Does.Contain(PortMarketCatalog.FormatPrice(sellPrice)));
                AssertButtons(paper,"Market Buy " + key,"Market Sell " + key,"Market Inventory Toggle",
                    "Confirm Market " + (buying ? "Buy " : "Sell ") + key,"Market Quantity Minus","Market Quantity Plus","Market Quantity Max");
                AssertRect(paper,Find<InputField>("Market Quantity Input " + key).GetComponent<RectTransform>());
                AssertRect(paper,Find<Slider>("Market Quantity Slider " + key).GetComponent<RectTransform>());
                AssertGlyphs(paper,host.transform,"Market Detail Name","Market Detail Buy Price","Market Detail Sell Price","Market Quantity Label","Market Trade Total");
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
            Image paper = Paper("Achievements Card", NauticalSurfaceSkin.ReadingPaper);
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
            Image paper = Paper("Market Board", NauticalSurfaceSkin.ReadingPaper);
            Rect outer = paper.rectTransform.rect;
            Rect edgeControl = new Rect(outer.center.x - 4f, outer.yMin + 2f, 8f, 6f);
            Assert.That(Contains(outer, edgeControl), Is.True, "The old outer-paper check would accept this control.");
            Assert.That(Contains(InnerRule(paper), edgeControl), Is.False, "The painted rule excludes it.");
            GameObject labelHost = new GameObject("Whitespace Frame Probe", typeof(RectTransform), typeof(Text));
            labelHost.transform.SetParent(paper.transform, false);
            Text label = labelHost.GetComponent<Text>();
            label.font = Find<Text>("Market Title").font;
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
            Image paper = Find<Image>(name);
            RawImage texture = Find<RawImage>(name + " Texture");
            Assert.That(texture.texture, Is.Not.Null, name + " uses the authored aged parchment.");
            Rect safe = paper.rectTransform.rect;
            float inset = name == "Workshop Preview Parchment" ? 6f : 12f;
            safe = Rect.MinMaxRect(safe.xMin + inset, safe.yMin + inset, safe.xMax - inset, safe.yMax - inset);
            foreach (string control in controls)
            {
                Rect bounds = BoundsInPaper(paper, Find<RectTransform>(control));
                Assert.That(Contains(safe, bounds), Is.True, control + " remains inside the parchment edge.");
            }
        }

        private Image Paper(string name, Sprite expected)
        {
            Canvas.ForceUpdateCanvases();
            Image image = Find<Image>(name);
            Assert.That(image.sprite, Is.SameAs(expected), name);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), name + " requires the real aged-paper sprite");
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
            if ((image.name == "Route Selection" || image.name == "Voyage Setup") && image.sprite == DepartureSkin.Paper)
            {
                // The departure folio uses paper without the old printed black rule.
                // Independently reserve readable margins around its torn outer edges.
                Rect folio = image.rectTransform.rect;
                return Rect.MinMaxRect(folio.xMin + 24f, folio.yMin + 36f, folio.xMax - 24f, folio.yMax - 24f);
            }
            if (image.name == "Warehouse Ledger" && image.sprite == null)
            {
                Rect opening = image.rectTransform.rect;
                return Rect.MinMaxRect(opening.xMin + 20f, opening.yMin + 20f, opening.xMax - 20f, opening.yMax - 20f);
            }
            // Measured ink lines in the source PNGs, independent of production UI coordinates.
            // Normalize source measurements to survive importer texture downscaling.
            Sprite sprite = image.sprite;
            Vector2 sourceSize;
            Vector4 line; // left, bottom, right, top source insets
            if (sprite == NauticalSurfaceSkin.ReadingPaper)
            { sourceSize = new Vector2(1536f, 1024f); line = new Vector4(60f, 67f, 60f, 62f); }
            else if (sprite == NauticalSurfaceSkin.InvestmentPaper)
            { sourceSize = new Vector2(1254f, 1254f); line = new Vector4(42f, 52f, 42f, 48f); }
            else
            {
                Assert.That(sprite, Is.SameAs(NauticalSurfaceSkin.CollectionPaper));
                sourceSize = new Vector2(1024f, 1536f); line = new Vector4(44f, 64f, 44f, 48f);
            }
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
            float left = ProjectInset(line.x / sourceSize.x * sprite.rect.width, sprite.border.x, sprite.border.z,
                sprite.rect.width, border.x, border.z, rect.width);
            float right = ProjectInset(line.z / sourceSize.x * sprite.rect.width, sprite.border.z, sprite.border.x,
                sprite.rect.width, border.z, border.x, rect.width);
            float bottom = ProjectInset(line.y / sourceSize.y * sprite.rect.height, sprite.border.y, sprite.border.w,
                sprite.rect.height, border.y, border.w, rect.height);
            float top = ProjectInset(line.w / sourceSize.y * sprite.rect.height, sprite.border.w, sprite.border.y,
                sprite.rect.height, border.w, border.y, rect.height);
            return Rect.MinMaxRect(rect.xMin + left + ReadingGap, rect.yMin + bottom + ReadingGap,
                rect.xMax - right - ReadingGap, rect.yMax - top - ReadingGap);
        }

        private static float ProjectInset(float sourceInset, float nearSourceBorder, float farSourceBorder,
            float sourceLength, float nearUiBorder, float farUiBorder, float uiLength)
        {
            if (sourceInset <= nearSourceBorder)
                return nearSourceBorder <= 0f ? 0f : sourceInset / nearSourceBorder * nearUiBorder;
            float centreSource = sourceLength - nearSourceBorder - farSourceBorder;
            return nearUiBorder + (sourceInset - nearSourceBorder) / centreSource *
                Mathf.Max(0f, uiLength - nearUiBorder - farUiBorder);
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
