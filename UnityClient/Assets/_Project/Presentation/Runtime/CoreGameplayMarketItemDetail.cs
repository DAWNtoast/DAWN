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
        private RectTransform marketItemDetailRoot, marketTradeSelectionRoot;
        private RawImage marketDetailIcon;
        private Text marketDetailName, marketDetailBuyPrice, marketDetailSellPrice, marketDetailDescription;
        private Text marketDetailStock, marketDetailCargo;
        private Text marketQuantityLabel, marketQuantityValue, marketTradeTotal;
        private Button marketDetailBuy, marketDetailSell, marketDetailBack, marketTradeConfirm, marketTradeCancel;
        private Button marketInventoryToggle, marketQuantityMinus, marketQuantityPlus, marketQuantityMax;
        private InputField marketQuantityInput;
        private Slider marketQuantitySlider;

        private void ResetMarketItemDetailView()
        {
            marketSelectedItem = null;
            marketTradeSelecting = marketTradeBuying = false;
            marketInventoryExpanded = false;
            marketItemDetailRoot = marketTradeSelectionRoot = null;
            marketDetailIcon = null;
            marketDetailName = marketDetailBuyPrice = marketDetailSellPrice = marketDetailDescription = null;
            marketDetailStock = marketDetailCargo = null;
            marketQuantityLabel = marketQuantityValue = marketTradeTotal = null;
            marketDetailBuy = marketDetailSell = marketDetailBack = marketTradeConfirm = marketTradeCancel = null;
            marketInventoryToggle = marketQuantityMinus = marketQuantityPlus = marketQuantityMax = null;
            marketQuantitySlider = null;
            marketQuantityInput = null;
        }

        private void BuildMarketItemDetail(RectTransform board, Color ink)
        {
            marketItemDetailRoot = CreateFixedPanel("Market Item Detail", board,
                MarketSceneLayout.DrawerSize, new Vector2(650f, -20f), Html("#E1CAA2"));
            Image paper = marketItemDetailRoot.GetComponent<Image>();
            NauticalSurfaceSkin.ApplyReadingPanel(paper);
            paper.raycastTarget = true; // The whole drawer consumes hits, including its blank parchment.
            Outline edge = marketItemDetailRoot.gameObject.AddComponent<Outline>();
            edge.effectColor = Html("#684324");
            edge.effectDistance = new Vector2(2f, -2f);
            marketShopTitle = CreateText("Market Shop Title", marketItemDetailRoot, new Vector2(420f, 48f),
                new Vector2(-20f, 353f), 32, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketCloseShop = CreateButton("Close Market Shop", marketItemDetailRoot, new Vector2(46f, 46f),
                new Vector2(216f, 353f), PanelRaised, Ink, 27);
            marketCloseShop.GetComponentInChildren<Text>().text = "×";
            marketDetailBack = marketCloseShop;
            marketCatalogGrid = CreateRect("Market Catalog Grid", marketItemDetailRoot,
                new Vector2(490f, 108f), new Vector2(0f, 265f));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                RectTransform row = CreateRect("Market Row " + key, marketCatalogGrid, new Vector2(146f, 106f), Vector2.zero);
                Image tabFace = row.gameObject.AddComponent<Image>();
                tabFace.color = new Color(.45f, .29f, .13f, .06f);
                tabFace.raycastTarget = true;
                Button tabButton = row.gameObject.AddComponent<Button>();
                tabButton.targetGraphic = tabFace;
                RawImage icon = CreateItemIcon("Market Item Icon " + key, row, ItemIconCatalog.ForCargo(key), new Vector2(0f, 16f), 66f);
                icon.raycastTarget = true;
                Button select = icon.gameObject.AddComponent<Button>();
                select.targetGraphic = icon;
                Text name = CreateText("Market Item Name " + key, row, new Vector2(140f, 27f),
                    new Vector2(0f, -32f), 21, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
                name.raycastTarget = false;
                // The retained row contract remains exhaustive; prices now live in
                // the selected item's ledger rather than cluttering the scene.
                Text buy = CreateText("Market Buy Price " + key, row, Vector2.zero, Vector2.zero, 18, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
                Text unit = CreateText("Market Buy Unit " + key, row, Vector2.zero, Vector2.zero, 18, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
                buy.gameObject.SetActive(false);
                unit.gameObject.SetActive(false);
                marketRowViews[item] = new MarketRowView { Root = row, Name = name, BuyPrice = buy, BuyUnit = unit, Icon = select };
            }
            Image rule = CreateImage("Market Ledger Divider", marketItemDetailRoot, new Vector2(482f, 2f), new Vector2(0f, 215f), Html("#99774F"));
            rule.raycastTarget = false;
            marketDetailIcon = CreateItemIcon("Market Detail Icon", marketItemDetailRoot,
                ItemIconCatalog.ForCargo("water"), new Vector2(-172f, 148f), 112f);
            marketDetailIcon.raycastTarget = false;
            marketDetailName = CreateText("Market Detail Name", marketItemDetailRoot, new Vector2(300f, 40f),
                new Vector2(67f, 181f), 28, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketDetailDescription = CreateText("Market Item Description", marketItemDetailRoot, new Vector2(300f, 64f),
                new Vector2(67f, 135f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, Html("#70583D"));
            marketDetailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            marketDetailDescription.verticalOverflow = VerticalWrapMode.Truncate;
            marketDetailBuyPrice = CreateText("Market Detail Buy Price", marketItemDetailRoot, new Vector2(466f, 28f),
                new Vector2(0f, 70f), 22, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketDetailSellPrice = CreateText("Market Detail Sell Price", marketItemDetailRoot, new Vector2(466f, 28f),
                new Vector2(0f, 40f), 22, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            marketDetailBuy = CreateButton("Market Detail Buy", marketItemDetailRoot, new Vector2(233f, 44f),
                new Vector2(-123f, -8f), Gold, DeepSea, 21);
            marketDetailSell = CreateButton("Market Detail Sell", marketItemDetailRoot, new Vector2(233f, 44f),
                new Vector2(123f, -8f), PanelRaised, Ink, 21);

            marketTradeSelectionRoot = CreateRect("Market Trade Selection", marketItemDetailRoot,
                new Vector2(500f, 390f), new Vector2(0f, -210f));
            marketQuantityLabel = CreateText("Market Quantity Label", marketTradeSelectionRoot, new Vector2(466f, 28f),
                new Vector2(0f, 157f), 19, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            marketQuantityMinus = CreateButton("Market Quantity Minus", marketTradeSelectionRoot, new Vector2(56f, 44f),
                new Vector2(-212f, 112f), PanelRaised, Ink, 26);
            marketQuantityMinus.GetComponentInChildren<Text>().text = "−";
            marketQuantityInput = CreateInputField("Market Quantity Input", marketTradeSelectionRoot,
                new Vector2(218f, 44f), new Vector2(-67f, 112f), "0");
            marketQuantityInput.contentType = InputField.ContentType.IntegerNumber;
            marketQuantityInput.characterLimit = 10;
            marketQuantityInput.textComponent.alignment = TextAnchor.MiddleCenter;
            marketQuantityInput.textComponent.fontSize = 24;
            ((Text)marketQuantityInput.placeholder).text = "数量";
            marketQuantityValue = marketQuantityInput.textComponent;
            marketQuantityPlus = CreateButton("Market Quantity Plus", marketTradeSelectionRoot, new Vector2(56f, 44f),
                new Vector2(78f, 112f), PanelRaised, Ink, 26);
            marketQuantityPlus.GetComponentInChildren<Text>().text = "+";
            marketQuantityMax = CreateButton("Market Quantity Max", marketTradeSelectionRoot, new Vector2(126f, 44f),
                new Vector2(179f, 112f), PanelRaised, Ink, 20);
            marketQuantityMax.GetComponentInChildren<Text>().text = "最大";
            marketQuantitySlider = CreateSlider("Market Quantity Slider", marketTradeSelectionRoot,
                new Vector2(478f, 32f), new Vector2(0f, 65f), 0f, 1f, 0f);
            marketQuantitySlider.wholeNumbers = true;
            RectTransform handleTrack = (RectTransform)marketQuantitySlider.handleRect.parent;
            handleTrack.sizeDelta = new Vector2(handleTrack.sizeDelta.x, 0f);
            marketQuantitySlider.handleRect.sizeDelta = new Vector2(28f, 32f);
            Image sliderTarget = marketQuantitySlider.gameObject.AddComponent<Image>();
            sliderTarget.color = Color.clear;
            sliderTarget.raycastTarget = true;
            marketTradeTotal = CreateText("Market Trade Total", marketTradeSelectionRoot, new Vector2(466f, 36f),
                new Vector2(0f, 18f), 26, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            marketTradeConfirm = CreateButton("Confirm Market Trade", marketTradeSelectionRoot, new Vector2(480f, 46f),
                new Vector2(0f, -109f), Gold, DeepSea, 23);
            marketTradeCancel = CreateButton("Cancel Market Trade", marketTradeSelectionRoot, new Vector2(480f, 26f),
                new Vector2(0f, -149f), PanelRaised, Ink, 18);
            marketTradeCancel.GetComponentInChildren<Text>().text = "收起店铺";

            marketInventoryToggle = CreateButton("Market Inventory Toggle", marketItemDetailRoot,
                new Vector2(480f, 30f), new Vector2(0f, -231f), PanelRaised, Ink, 18);
            marketDetailStock = CreateText("Market Detail Stock", marketItemDetailRoot, new Vector2(286f, 48f),
                new Vector2(-82f, -272f), 17, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            marketDetailCargo = CreateText("Market Detail Cargo", marketItemDetailRoot, new Vector2(170f, 48f),
                new Vector2(161f, -272f), 17, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
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
            marketItemDetailRoot.anchoredPosition = MarketSceneLayout.DrawerPosition(marketSelectedShop.Value);
            marketShopTitle.text = MarketSceneLayout.ShopName(marketSelectedShop.Value);
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
                row.Root.anchoredPosition = new Vector2((tab - (shopItems.Count - 1) * .5f) * 162f, 0f);
                row.Root.GetComponent<Image>().color = catalogItem == marketSelectedItem
                    ? new Color(.54f, .32f, .10f, .22f) : new Color(.45f, .29f, .13f, .04f);
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
            ItemIconArt art = ItemIconCatalog.ForCargo(key);
            marketDetailIcon.name = "Market Detail Icon " + key;
            marketDetailIcon.texture = art.Texture;
            marketDetailIcon.uvRect = art.Uv;
            marketDetailIcon.enabled = art.IsAvailable;
            UiImageAspect.Fit(marketDetailIcon, new Vector2(112f, 112f));
            marketDetailName.text = PortMarketCatalog.Name(item);
            marketDetailDescription.text = MarketItemDescription(item);
            int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
            int sellLot = PortMarketCatalog.SaleStep(item);
            marketDetailBuyPrice.text = "买入　" + PortMarketCatalog.FormatPrice(
                PortMarketCatalog.CalculateBuyCost(port, item, now, buyLot, session.Cargo.Fame)) + " 金 / " + buyLot + " 单位";
            marketDetailSellPrice.text = "卖出　" + PortMarketCatalog.FormatPrice(
                PortMarketCatalog.CalculateSellRevenue(port, item, now, sellLot)) + " 金 / " + sellLot + " 单位";
            marketDetailStock.name = "Market Quota " + key;
            marketDetailCargo.name = "Market Holdings " + key;
            marketDetailStock.gameObject.SetActive(marketInventoryExpanded);
            marketDetailCargo.gameObject.SetActive(marketInventoryExpanded);
            marketDetailStock.text = "本月配货 " + PortMarketCatalog.DayState(port, item, now, level).Stock + " · 可购 " + remaining +
                "\n已购 " + purchased + " / 月额度 " + PortMarketCatalog.MaxPurchaseQuota(item, level);
            marketDetailCargo.text = "随船 " + carried + "\n本港仓储 " + stored;
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
            marketDetailBuy.GetComponentInChildren<Text>().text = buying ? "●  买入" : "买入";
            marketDetailSell.GetComponentInChildren<Text>().text = buying ? "卖出" : "●  卖出";
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

        private static string MarketItemDescription(PortMarketItem item)
        {
            switch (item)
            {
                case PortMarketItem.Water: return "清洁的淡水，是远航的基础。";
                case PortMarketItem.Food: return "装船前备足口粮，让船员安心远航。";
                case PortMarketItem.Beer: return "为漫长的海上生活添一份慰藉。";
                case PortMarketItem.Medicine: return "妥善储备药品，应对航途中的伤病。";
                case PortMarketItem.Wood: return "结实的船用木材，用于修补与建设。";
                case PortMarketItem.Iron: return "坚固的铁料，是船舶维护的重要物资。";
                case PortMarketItem.Cloth: return "耐用的帆布，为船帆与维修备料。";
                case PortMarketItem.Powder: return "保持干燥的火药，为舰炮提供火力。";
                default: return "铸造结实的炮弹，出航前检查储备。";
            }
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





