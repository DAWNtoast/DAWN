using System;
using System.Globalization;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private PortMarketItem? marketSelectedItem;
        private bool marketTradeSelecting, marketTradeBuying;
        private bool marketInventoryExpanded;
        private RectTransform marketItemDetailRoot, marketTradeSelectionRoot, marketTradeFooter;
        private RawImage marketShopEmblem;
        private Text marketDetailBuyPrice, marketDetailSellPrice;
        private Text marketDetailStock, marketDetailCargo;
        private Text marketQuantityLabel, marketQuantityValue, marketTradeTotal;
        private Button marketDetailBuy, marketDetailSell, marketDetailBack, marketTradeConfirm, marketTradeCancel;
        private Button marketInventoryToggle, marketQuantityMinus, marketQuantityPlus, marketQuantityMax;
        private Button marketDetailStorageMode;
        private InputField marketQuantityInput;
        private Slider marketQuantitySlider;

        private void ResetMarketItemDetailView()
        {
            marketSelectedItem = null;
            marketTradeSelecting = marketTradeBuying = false;
            marketInventoryExpanded = false;
            marketItemDetailRoot = marketTradeSelectionRoot = marketTradeFooter = null;
            marketShopEmblem = null;
            marketDetailBuyPrice = marketDetailSellPrice = null;
            marketDetailStock = marketDetailCargo = null;
            marketQuantityLabel = marketQuantityValue = marketTradeTotal = null;
            marketDetailBuy = marketDetailSell = marketDetailBack = marketTradeConfirm = marketTradeCancel = null;
            marketInventoryToggle = marketQuantityMinus = marketQuantityPlus = marketQuantityMax = null;
            marketDetailStorageMode = null;
            marketQuantitySlider = null;
            marketQuantityInput = null;
        }

        private void BuildMarketItemDetail(RectTransform board, Color ink)
        {
            marketItemDetailRoot = CreateFixedPanel("Market Item Detail", board,
                MarketSceneLayout.DrawerSize, MarketSceneLayout.DrawerPosition(MarketShopKind.Supplies), Html("#E1CAA2"));
            Image paper = marketItemDetailRoot.GetComponent<Image>();
            MarketShopSkin.ApplyPanel(paper);
            paper.raycastTarget = true; // The whole drawer consumes hits, including its blank parchment.
            // Keep the header, goods and quantity controls still when the ledger expands.
            RectTransform ledger = CreateRect("Market Shop Ledger", marketItemDetailRoot, MarketSceneLayout.DrawerSize,
                new Vector2(0f, -MarketSceneLayout.DrawerSize.y * .5f));
            ledger.anchorMin = ledger.anchorMax = new Vector2(.5f, 1f);
            CreateImage("Market Shop Emblem Rim", ledger, new Vector2(64f, 64f),
                new Vector2(-146f, 342f), Html("#9A7139"), circleSprite).raycastTarget = false;
            CreateImage("Market Shop Emblem Backing", ledger, new Vector2(58f, 58f),
                new Vector2(-146f, 342f), Html("#493018"), circleSprite).raycastTarget = false;
            marketShopEmblem = CreateItemIcon("Market Shop Emblem", ledger, ItemIconCatalog.ForCargo("water"),
                new Vector2(-146f, 342f), 52f);
            marketShopEmblem.raycastTarget = false;
            marketShopTitle = CreateText("Market Shop Title", ledger, new Vector2(220f, 48f),
                new Vector2(12f, 342f), 30, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketCloseShop = CreateButton("Close Market Shop", ledger, new Vector2(40f, 44f),
                new Vector2(160f, 342f), PanelRaised, Ink, 30);
            marketCloseShop.GetComponentInChildren<Text>().text = "×";
            MarketShopSkin.ApplyTextButton(marketCloseShop);
            marketDetailBack = marketCloseShop;
            marketCatalogGrid = CreateRect("Market Catalog Grid", ledger,
                new Vector2(360f, 112f), new Vector2(0f, 246f));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                RectTransform row = CreateRect("Market Row " + key, marketCatalogGrid, new Vector2(112f, 112f), Vector2.zero);
                Image tabFace = row.gameObject.AddComponent<Image>();
                tabFace.color = new Color(.45f, .29f, .13f, .06f);
                tabFace.raycastTarget = true;
                Button tabButton = row.gameObject.AddComponent<Button>();
                tabButton.targetGraphic = tabFace;
                MarketShopSkin.ApplyLightButton(tabButton);
                RawImage icon = CreateItemIcon("Market Item Icon " + key, row, ItemIconCatalog.ForCargo(key), new Vector2(0f, 16f), 66f);
                icon.raycastTarget = true;
                Button select = icon.gameObject.AddComponent<Button>();
                select.targetGraphic = icon;
                Text name = CreateText("Market Item Name " + key, row, new Vector2(104f, 27f),
                    new Vector2(0f, -34f), 21, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
                name.raycastTarget = false;
                // The retained row contract remains exhaustive; prices now live in
                // the selected item's ledger rather than cluttering the scene.
                Text buy = CreateText("Market Buy Price " + key, row, Vector2.zero, Vector2.zero, 18, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
                Text unit = CreateText("Market Buy Unit " + key, row, Vector2.zero, Vector2.zero, 18, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
                buy.gameObject.SetActive(false);
                unit.gameObject.SetActive(false);
                marketRowViews[item] = new MarketRowView { Root = row, Name = name, BuyPrice = buy, BuyUnit = unit, Icon = select };
            }
            Image rule = CreateImage("Market Ledger Divider", ledger, new Vector2(348f, 1f), new Vector2(0f, 180f), Html("#AD956E"));
            rule.raycastTarget = false;
            marketDetailBuyPrice = CreateText("Market Detail Buy Price", ledger, new Vector2(344f, 30f),
                new Vector2(0f, 145f), 23, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketDetailSellPrice = CreateText("Market Detail Sell Price", ledger, new Vector2(344f, 30f),
                new Vector2(0f, 108f), 23, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            CreateImage("Market Trade Divider", ledger, new Vector2(348f, 1f), new Vector2(0f, 77f), Html("#AD956E")).raycastTarget = false;
            marketDetailBuy = CreateButton("Market Detail Buy", ledger, new Vector2(174f, 46f),
                new Vector2(-93f, 34f), Gold, DeepSea, 23);
            marketDetailSell = CreateButton("Market Detail Sell", ledger, new Vector2(174f, 46f),
                new Vector2(93f, 34f), PanelRaised, Ink, 23);

            marketTradeSelectionRoot = CreateRect("Market Trade Selection", ledger,
                new Vector2(360f, 820f), Vector2.zero);
            marketQuantityLabel = CreateText("Market Quantity Label", marketTradeSelectionRoot, new Vector2(344f, 28f),
                new Vector2(0f, -21f), 19, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            marketQuantityMinus = CreateButton("Market Quantity Minus", marketTradeSelectionRoot, new Vector2(46f, 46f),
                new Vector2(-157f, -63f), PanelRaised, Ink, 26);
            marketQuantityMinus.GetComponentInChildren<Text>().text = "−";
            marketQuantityInput = CreateInputField("Market Quantity Input", marketTradeSelectionRoot,
                new Vector2(142f, 46f), new Vector2(-55f, -63f), "0");
            marketQuantityInput.contentType = InputField.ContentType.IntegerNumber;
            marketQuantityInput.characterLimit = 10;
            marketQuantityInput.textComponent.alignment = TextAnchor.MiddleCenter;
            marketQuantityInput.textComponent.fontSize = 24;
            marketQuantityInput.textComponent.resizeTextForBestFit = true;
            marketQuantityInput.textComponent.resizeTextMinSize = 18;
            marketQuantityInput.textComponent.resizeTextMaxSize = 24;
            ((Text)marketQuantityInput.placeholder).text = "数量";
            marketQuantityValue = marketQuantityInput.textComponent;
            marketQuantityPlus = CreateButton("Market Quantity Plus", marketTradeSelectionRoot, new Vector2(46f, 46f),
                new Vector2(47f, -63f), PanelRaised, Ink, 26);
            marketQuantityPlus.GetComponentInChildren<Text>().text = "+";
            marketQuantityMax = CreateButton("Market Quantity Max", marketTradeSelectionRoot, new Vector2(102f, 46f),
                new Vector2(129f, -63f), PanelRaised, Ink, 20);
            marketQuantityMax.GetComponentInChildren<Text>().text = "最大";
            marketQuantitySlider = CreateSlider("Market Quantity Slider", marketTradeSelectionRoot,
                new Vector2(358f, 32f), new Vector2(0f, -110f), 0f, 1f, 0f);
            marketQuantitySlider.wholeNumbers = true;
            RectTransform handleTrack = (RectTransform)marketQuantitySlider.handleRect.parent;
            handleTrack.sizeDelta = new Vector2(handleTrack.sizeDelta.x, 0f);
            marketQuantitySlider.handleRect.sizeDelta = new Vector2(28f, 32f);
            Image sliderTarget = marketQuantitySlider.gameObject.AddComponent<Image>();
            sliderTarget.color = Color.clear;
            sliderTarget.raycastTarget = true;
            MarketShopSkin.ApplyInput(marketQuantityInput);
            foreach (Button quantityButton in new[] { marketQuantityMinus, marketQuantityPlus, marketQuantityMax })
                MarketShopSkin.ApplyLightButton(quantityButton);
            MarketShopSkin.ApplySlider(marketQuantitySlider);
            marketTradeTotal = CreateText("Market Trade Total", marketTradeSelectionRoot, new Vector2(344f, 38f),
                new Vector2(0f, -155f), 26, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketTradeTotal.resizeTextForBestFit = true;
            marketTradeTotal.resizeTextMinSize = 19;
            marketTradeTotal.resizeTextMaxSize = 26;
            marketTradeFooter = CreateRect("Market Trade Footer", marketTradeSelectionRoot, new Vector2(360f, 160f), new Vector2(0f, -310f));
            marketDetailStorageMode = CreateButton("Market Detail Storage Mode", marketTradeFooter, new Vector2(360f, 42f),
                new Vector2(0f, 59f), PanelRaised, ink, 20);
            MarketShopSkin.ApplyLightButton(marketDetailStorageMode);
            marketTradeConfirm = CreateButton("Confirm Market Trade", marketTradeFooter, new Vector2(360f, 52f),
                new Vector2(0f, -5f), Gold, DeepSea, 25);
            marketTradeCancel = CreateButton("Cancel Market Trade", marketTradeFooter, new Vector2(360f, 30f),
                new Vector2(0f, -54f), PanelRaised, Ink, 20);
            marketTradeCancel.GetComponentInChildren<Text>().text = "取消";
            MarketShopSkin.ApplyTextButton(marketTradeCancel);

            marketInventoryToggle = CreateButton("Market Inventory Toggle", ledger,
                new Vector2(360f, 38f), new Vector2(0f, -199f), PanelRaised, Ink, 19);
            MarketShopSkin.ApplyLightButton(marketInventoryToggle);
            marketDetailStock = CreateText("Market Detail Stock", ledger, new Vector2(344f, 48f),
                new Vector2(0f, -244f), 17, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            marketDetailCargo = CreateText("Market Detail Cargo", ledger, new Vector2(344f, 26f),
                new Vector2(0f, -286f), 17, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            marketDetailStock.lineSpacing = marketDetailCargo.lineSpacing = 1.15f;
            foreach (Text text in new[] { marketDetailStock, marketDetailCargo })
            {
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.raycastTarget = false;
            }
            marketItemDetailRoot.gameObject.SetActive(false);
        }

        private void OpenMarketItemDetail(PortMarketItem item)
        {
            if (session == null || session.Phase != CoreDemoPhase.Port || marketItemDetailRoot == null ||
                marketPurchaseView == null || !marketPurchaseView.gameObject.activeInHierarchy ||
                activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex ||
                !PortMarketCatalog.AllItems.Contains(item) || pendingMarketPurchase != null) return;
            if (marketSelectedItem == item && marketSelectedShop == MarketSceneLayout.ShopForItem(item)) return;
            marketSelectedShop = MarketSceneLayout.ShopForItem(item);
            marketSelectedItem = item;
            marketTradeSelecting = marketTradeBuying = true;
            marketInventoryExpanded = false;
            marketQuantities[item] = PortMarketCatalog.PurchaseStep(item);
            RefreshMarketItemDetailPage();
        }

        private void StartMarketTrade(bool buying)
        {
            if (session == null || session.Phase != CoreDemoPhase.Port ||
                activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex ||
                !marketSelectedItem.HasValue || marketItemDetailRoot == null ||
                !marketItemDetailRoot.gameObject.activeInHierarchy || pendingMarketPurchase != null) return;
            PortMarketItem item = marketSelectedItem.Value;
            int maximum = MarketDetailTradeMaximum(item, buying, MarketNow, PortInvestmentLevel(session.CurrentPort));
            marketQuantities[item] = Math.Min(Math.Max(0, MarketQuantity(item)), maximum);
            marketTradeBuying = buying;
            marketTradeSelecting = true;
            RefreshMarketItemDetailPage();
        }

        private void RefreshMarketItemDetailPage()
        {
            if (Application.isPlaying && !editorPreviewBuilding)
            {
                Render();
                return;
            }
            marketPurchaseRevision++;
            foreach (MarketPurchaseQuote quote in marketPurchaseQuotes.Values) quote.Consumed = true;
            marketPurchaseQuotes.Clear();
            pendingMarketPurchase = null;
            RefreshMarketFacilityContents();
        }

        private void RefreshMarketItemDetail(DateTime now, int level)
        {
            bool detail = marketSelectedItem.HasValue && marketSelectedShop.HasValue && marketItemDetailRoot != null;
            if (marketItemDetailRoot == null) return;
            marketItemDetailRoot.gameObject.SetActive(detail);
            if (!detail) return;
            float expansion = marketInventoryExpanded ? MarketSceneLayout.InventoryExpansionHeight : 0f;
            marketItemDetailRoot.sizeDelta = MarketSceneLayout.DrawerSize + new Vector2(0f, expansion);
            marketItemDetailRoot.anchoredPosition = MarketSceneLayout.DrawerPosition(marketSelectedShop.Value) - new Vector2(0f, expansion * .5f);
            marketTradeFooter.anchoredPosition = new Vector2(0f, -310f - expansion);
            marketShopTitle.text = MarketSceneLayout.ShopName(marketSelectedShop.Value);
            ItemIconArt shopArt = ItemIconCatalog.ForCargo(PortMarketCatalog.Key(MarketSceneLayout.ItemsForShop(marketSelectedShop.Value)[0]));
            marketShopEmblem.texture = shopArt.Texture;
            marketShopEmblem.uvRect = shopArt.Uv;
            marketShopEmblem.enabled = shopArt.IsAvailable;
            UiImageAspect.Fit(marketShopEmblem, new Vector2(52f, 52f));
            marketCloseShop.onClick = new Button.ButtonClickedEvent();
            AddMarketViewAction(marketCloseShop, CloseMarketShop);
            var shopItems = MarketSceneLayout.ItemsForShop(marketSelectedShop.Value);
            foreach (PortMarketItem catalogItem in PortMarketCatalog.AllItems)
            {
                MarketRowView row = marketRowViews[catalogItem];
                int tab = -1;
                for (int i = 0; i < shopItems.Count; i++) if (shopItems[i] == catalogItem) tab = i;
                row.Root.gameObject.SetActive(tab >= 0);
                if (tab < 0) continue;
                row.Root.anchoredPosition = new Vector2((tab - (shopItems.Count - 1) * .5f) * 120f, 0f);
                bool selected = catalogItem == marketSelectedItem;
                MarketShopSkin.ApplyLightButton(row.Root.GetComponent<Button>(), selected);
                row.Name.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                row.Name.color = selected ? Html("#382410") : Html("#70583D");
            }
            PortMarketItem item = marketSelectedItem.Value;
            string key = PortMarketCatalog.Key(item);
            string port = session.CurrentPort;
            int purchased = MarketPurchased(port, item, now);
            int remaining = MarketRemaining(port, item, now, level);
            int carried = session.GetMarketCargoAmount(key);
            int stored = session.PortWarehouse.GetAmount(port, key);
            int buyMaximum = MarketDetailTradeMaximum(item, true, now, level);
            int sellMaximum = MarketDetailTradeMaximum(item, false, now, level);
            int maximum = marketTradeBuying ? buyMaximum : sellMaximum;
            int quantity = Math.Min(Math.Max(0, MarketQuantity(item)), maximum);
            marketQuantities[item] = quantity;
            int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
            int sellLot = PortMarketCatalog.SaleStep(item);
            marketDetailBuyPrice.text = "买入　　" + PortMarketCatalog.FormatPrice(
                PortMarketCatalog.CalculateBuyCost(port, item, now, buyLot, session.Cargo.Fame)) + " 金 <size=19>/ " + buyLot + " 单位</size>";
            marketDetailSellPrice.text = "卖出　　" + PortMarketCatalog.FormatPrice(
                PortMarketCatalog.CalculateSellRevenue(port, item, now, sellLot)) + " 金 <size=19>/ " + sellLot + " 单位</size>";
            marketDetailStock.name = "Market Quota " + key;
            marketDetailCargo.name = "Market Holdings " + key;
            marketDetailStock.gameObject.SetActive(marketInventoryExpanded);
            marketDetailCargo.gameObject.SetActive(marketInventoryExpanded);
            marketDetailStock.text = "本月配货 " + PortMarketCatalog.DayState(port, item, now, level).Stock + " · 可购 " + remaining +
                "\n已购 " + purchased + " / 月额度 " + PortMarketCatalog.MaxPurchaseQuota(item, level);
            marketDetailCargo.text = "随船 " + carried + " · 本港仓储 " + stored;
            marketInventoryToggle.GetComponentInChildren<Text>().text = marketInventoryExpanded ? "库存与额度  ▲" : "库存与额度  ▼";
            decimal cost = PortMarketCatalog.CalculateBuyCost(port, item, now, quantity, session.Cargo.Fame);
            decimal revenue = PortMarketCatalog.CalculateSellRevenue(port, item, now, quantity);
            MarketPurchaseQuote quote = CreateMarketPurchaseQuote(item, quantity, cost, now, level, purchased, remaining);
            bool live = Application.isPlaying && !editorPreviewBuilding;
            bool buying = marketTradeBuying;
            marketInventoryToggle.interactable = true;
            BindMarketDetailAction(marketInventoryToggle, quote, marketTradeSelecting, buying, () =>
            {
                marketInventoryExpanded = !marketInventoryExpanded;
                RefreshMarketItemDetailPage();
            });
            marketDetailBuy.name = "Market Buy " + key;
            marketDetailSell.name = "Market Sell " + key;
            marketDetailBuy.GetComponentInChildren<Text>().text = "买入";
            marketDetailSell.GetComponentInChildren<Text>().text = "卖出";
            MarketShopSkin.SetMode(marketDetailBuy, buying);
            MarketShopSkin.SetMode(marketDetailSell, !buying);
            marketDetailStorageMode.GetComponentInChildren<Text>().text = buying
                ? "收货：" + (EffectiveMarketStorageMode == MarketPurchaseStorageMode.Ship ? "船只仓库" :
                    EffectiveMarketStorageMode == MarketPurchaseStorageMode.Warehouse ? "本港仓库" : "每次询问") + "　›"
                : "出售：随船货物";
            marketDetailStorageMode.interactable = buying && live && !tutorialActive;
            marketDetailStorageMode.onClick = new Button.ButtonClickedEvent();
            BindMarketStorageModeAction(marketDetailStorageMode);
            marketDetailBuy.interactable = marketDetailSell.interactable = live;
            BindMarketDetailAction(marketDetailBuy, quote, marketTradeSelecting, buying, () => StartMarketTrade(true));
            BindMarketDetailAction(marketDetailSell, quote, marketTradeSelecting, buying, () => StartMarketTrade(false));
            marketTradeSelectionRoot.gameObject.SetActive(marketTradeSelecting);
            marketQuantitySlider.name = "Market Quantity Slider " + key;
            marketQuantityInput.name = "Market Quantity Input " + key;
            marketQuantityValue.name = "Market Quantity " + key;
            marketQuantityLabel.text = (buying ? "买入" : "卖出") + "数量 · 最多 " + maximum;
            marketQuantityInput.onEndEdit = new InputField.EndEditEvent();
            marketQuantityInput.SetTextWithoutNotify(quantity.ToString(CultureInfo.InvariantCulture));
            marketQuantityInput.interactable = live && maximum > 0;
            marketTradeTotal.text = (buying ? "合计  −" : "收入  +") + PortMarketCatalog.FormatPrice(buying ? cost : revenue) + " 金";
            marketQuantitySlider.onValueChanged = new Slider.SliderEvent();
            marketQuantitySlider.minValue = 0f;
            marketQuantitySlider.maxValue = maximum;
            marketQuantitySlider.SetValueWithoutNotify(quantity);
            marketQuantitySlider.interactable = live && maximum > 0;
            Slider slider = marketQuantitySlider;
            slider.onValueChanged.AddListener(value =>
            {
                if (!live || !slider.IsInteractable() || !slider.gameObject.activeInHierarchy ||
                    !MarketDetailQuoteIsCurrent(quote, true, buying) || float.IsNaN(value) || float.IsInfinity(value)) return;
                marketQuantities[item] = MarketDetailSliderQuantity(slider, value, maximum);
                RefreshMarketItemDetailPage();
            });
            marketQuantityInput.onEndEdit.AddListener(value =>
            {
                if (!live || !marketQuantityInput.IsInteractable() || !MarketDetailQuoteIsCurrent(quote, true, buying)) return;
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long entered))
                    marketQuantities[item] = (int)Math.Max(0L, Math.Min((long)maximum, entered));
                else marketQuantities[item] = 0;
                RefreshMarketItemDetailPage();
            });
            int step = Math.Max(1, buying ? PortMarketCatalog.PurchaseStep(item) : PortMarketCatalog.SaleStep(item));
            marketQuantityMinus.interactable = live && quantity > 0;
            marketQuantityPlus.interactable = live && quantity < maximum;
            marketQuantityMax.interactable = live && maximum > 0 && quantity != maximum;
            BindMarketQuantityButton(marketQuantityMinus, quote, buying, Math.Max(0, quantity - step));
            BindMarketQuantityButton(marketQuantityPlus, quote, buying, (int)Math.Min((long)maximum, (long)quantity + step));
            BindMarketQuantityButton(marketQuantityMax, quote, buying, maximum);
            marketTradeConfirm.name = "Confirm Market " + (buying ? "Buy " : "Sell ") + key;
            marketTradeConfirm.GetComponentInChildren<Text>().text = buying ? "确认买入" : "确认出售";
            marketTradeConfirm.interactable = live && quantity > 0 && quantity <= maximum &&
                (buying ? cost > 0m && session.ProfileGold >= cost && MarketPurchaseFitsMode(quote) :
                    revenue > 0m && carried >= quantity && CoreEconomyBalance.IsValidGoldAmount(revenue) &&
                    revenue <= CoreEconomyBalance.MaximumProfileGold - session.ProfileGold);
            marketTradeCancel.interactable = true;
            BindMarketDetailAction(marketTradeConfirm, quote, true, buying, () =>
            {
                if (buying) BuyQuotedMarketItem(quote);
                else SellQuotedMarketItem(quote, quantity, revenue, carried);
            });
            marketTradeCancel.onClick = new Button.ButtonClickedEvent();
            AddMarketViewAction(marketTradeCancel, CloseMarketShop);
        }

        private void BindMarketQuantityButton(Button button, MarketPurchaseQuote quote, bool buying, int quantity)
        {
            BindMarketDetailAction(button, quote, true, buying, () =>
            {
                marketQuantities[quote.Item] = quantity;
                RefreshMarketItemDetailPage();
            });
        }

        private void BindMarketDetailAction(Button button, MarketPurchaseQuote quote, bool selecting, bool buying, Action action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            AddMarketViewAction(button, () =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || !button.gameObject.activeInHierarchy ||
                    !button.IsInteractable() || !MarketDetailQuoteIsCurrent(quote, selecting, buying)) return;
                action();
            });
        }
        private bool MarketDetailQuoteIsCurrent(MarketPurchaseQuote quote, bool selecting, bool buying) =>
            MarketQuoteHasCurrentIdentity(quote) && pendingMarketPurchase == null &&
            marketItemDetailRoot != null && marketItemDetailRoot.gameObject.activeInHierarchy &&
            marketSelectedItem.HasValue && marketSelectedItem.Value == quote.Item &&
            marketTradeSelecting == selecting && (!selecting || marketTradeBuying == buying) &&
            MarketMonth(MarketNow) == quote.Date && marketDisplayedDate == quote.Date &&
            EffectiveMarketStorageMode == quote.Mode && PortInvestmentLevel(quote.Port) == quote.InvestmentLevel &&
            MarketQuantity(quote.Item) == quote.Quantity;

        private int MarketDetailTradeMaximum(PortMarketItem item, bool buying, DateTime now, int level)
        {
            int purchased = MarketPurchased(session.CurrentPort, item, now);
            int remaining = MarketRemaining(session.CurrentPort, item, now, level);
            int low = 0;
            int high = Math.Max(0, buying ? remaining : session.GetMarketCargoAmount(PortMarketCatalog.Key(item)));
            while (low < high)
            {
                int candidate = low + (int)(((long)high - low + 1L) / 2L);
                bool fits;
                if (buying)
                {
                    decimal cost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, candidate, session.Cargo.Fame);
                    var quote = new MarketPurchaseQuote(session, marketPurchaseView, marketPurchaseRevision, now,
                        item, candidate, cost, level, purchased, remaining, EffectiveMarketStorageMode);
                    fits = cost > 0m && CoreEconomyBalance.IsValidGoldPayment(cost) &&
                        session.ProfileGold >= cost && MarketPurchaseFitsMode(quote);
                }
                else
                {
                    decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, item, now, candidate);
                    fits = revenue > 0m && CoreEconomyBalance.IsValidGoldAmount(revenue) &&
                        revenue <= CoreEconomyBalance.MaximumProfileGold - session.ProfileGold;
                }
                if (fits) low = candidate;
                else high = candidate - 1;
            }
            return low;
        }

        private static int MarketDetailSliderQuantity(Slider slider, float value, int maximum)
        {
            // Unity stores float values; map an int.MaxValue endpoint before
            // rounding so a legal whole-stock sale cannot overflow its quantity.
            if (value <= slider.minValue) return 0;
            if (value >= slider.maxValue) return maximum;
            double rounded = Math.Round((double)value, MidpointRounding.AwayFromZero);
            return (int)Math.Max(0d, Math.Min((double)maximum, rounded));
        }
    }
}





