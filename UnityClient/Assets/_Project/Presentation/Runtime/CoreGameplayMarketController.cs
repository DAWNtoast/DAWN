using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const string PortMarketPurchaseKeyPrefix = "ShatteredSail.PortMarketPurchase.v3.";
        private Func<DateTime> marketTimeProvider;
        private DateTime MarketNow => marketTimeProvider?.Invoke() ?? session?.GameDateTime ?? CoreVoyageSession.GameClockOrigin;
        private DateTime marketDisplayedDate = DateTime.MinValue;
        // The first game month starts at DateTime.MinValue; use a sentinel that
        // cannot equal any normalized month to detect its first rollover too.
        private DateTime marketLastObservedMonth = DateTime.MaxValue;
        private readonly Dictionary<PortMarketItem, int> marketQuantities = new Dictionary<PortMarketItem, int>();

        public static string MarketPurchaseKey(string portName, DateTime date, PortMarketItem item)
        {
            return PortMarketPurchaseKeyPrefix + WorldEconomyScope + "." + PortMarketCatalog.CanonicalPortName(portName) + "." +
                PortMarketCatalog.MonthKey(date) + "." + PortMarketCatalog.Key(item);
        }

        // Every port derives its batch from the same persistent game month,
        // including ports the player has not visited since the month changed.
        private void RefreshMarketMonthIfNeeded()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null ||
                session.Phase != CoreDemoPhase.Port) return;
            DateTime month = MarketMonth(MarketNow);
            if (marketLastObservedMonth == DateTime.MaxValue)
            {
                marketLastObservedMonth = month;
                return;
            }
            if (marketLastObservedMonth == month) return;
            marketLastObservedMonth = month;
            if (activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex ||
                activePortFacility == PortFacilityArtCatalog.MaintenanceFacilityIndex || activePortFacility == 8 ||
                activePortFacility == PortFacilityArtCatalog.ComponentShopFacilityIndex)
            {
                if (activePortFacility == 8 && contentRoot != null &&
                    contentRoot.Find("Voyage Setup") is RectTransform departure)
                    CommitDepartureSeed(departure);
                ResetComponentShopView();
                portNotice = "新游戏月已开始，所有港口的市场和部件商店库存已刷新。";
                PlaySfx("shop_refresh", 0.42f);
                Render();
            }
        }

        private bool RefreshExpiredMarketQuote(DateTime now)
        {
            if (marketDisplayedDate == MarketMonth(now)) return false;
            portNotice = "新月行情已刷新，请确认价格后再次交易。";
            Render();
            return true;
        }

        private int MarketPurchased(string portName, PortMarketItem item, DateTime date)
        {
            // Only generated preview fixtures ignore the player's purchase ledger.
            // Standalone queries (including edit-mode validation) use real records.
            if (editorPreviewBuilding) return 0;
            return Mathf.Max(0, PlayerPrefs.GetInt(MarketPurchaseKey(portName, date, item), 0));
        }

        private int MarketRemaining(string portName, PortMarketItem item, DateTime date, int investmentLevel)
        {
            PortMarketDayState state = PortMarketCatalog.DayState(portName, item, date, investmentLevel);
            int quota = PortMarketCatalog.MaxPurchaseQuota(item, investmentLevel);
            return Mathf.Max(0, Math.Min(state.Stock, quota) - MarketPurchased(portName, item, date));
        }

        private int MarketQuantity(PortMarketItem item)
        {
            int requested = marketQuantities.TryGetValue(item, out int value) && value > 0
                ? value : PortMarketCatalog.PurchaseStep(item);
            return Math.Min(requested, MarketQuantityLimit(item));
        }

        private int MarketQuantityLimit(PortMarketItem item) => Math.Max(
            session.GetMarketCargoAmount(PortMarketCatalog.Key(item)),
            MarketRemaining(session.CurrentPort, item, MarketNow, PortInvestmentLevel(session.CurrentPort)));

        private int MarketSaleQuantity(PortMarketItem item) => Math.Min(
            session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), MarketQuantity(item));

        private void AdjustMarketQuantity(PortMarketItem item, int direction)
        {
            bool dismissChoice = pendingMarketPurchase != null;
            if (pendingMarketPurchase != null) pendingMarketPurchase.Consumed = true;
            pendingMarketPurchase = null;
            int step = PortMarketCatalog.PurchaseStep(item);
            int limit = MarketQuantityLimit(item);
            if (limit <= 0) return;
            int current = MarketQuantity(item);
            if (marketQuantityInputs.TryGetValue(item, out InputField input) && input != null &&
                long.TryParse(input.text, out long typed)) current = (int)Math.Max(0L, Math.Min(limit, typed));
            long next = Math.Max(1L, (long)current + (long)direction * step);
            int quantity = (int)Math.Min(limit, next);
            marketQuantities[item] = quantity;
            marketQuantityDrafts[item] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // A queued edit can dismiss a destination choice. Ordinary quantity
            // changes only refresh this row, keeping focus and the other rows alive.
            if (dismissChoice || input == null) Render();
            else input.text = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void BuildMarketFacility()
        {
            PrepareMarketPage();
            BeginMarketPurchaseView();
            // Keep the market readable over the illustrated port by using a pale paper
            // ledger and high-contrast timber rows. The previous translucent row fills
            // let the background bleed through, so both labels and controls disappeared
            // into the scene art.
            Color marketPaper = new Color(0.90f, 0.82f, 0.68f, 0.97f);
            Color marketInk = Html("#2C1B12");
            Color marketRowPrimary = new Color(0.14f, 0.072f, 0.028f, 0.94f);
            Color marketRowAlternate = new Color(0.20f, 0.105f, 0.040f, 0.94f);
            Color marketLightInk = Html("#F8E8C6");
            Color marketLightGold = Html("#FFE0A3");

            RectTransform panel = CreateFixedPanel("Market Panel", contentRoot,
                new Vector2(1820f, 730f), new Vector2(0f, -45f), Color.clear);
            marketPurchaseView = panel;
            RectTransform board = CreateFixedPanel("Market Board", panel,
                new Vector2(1760f, 700f), new Vector2(0f, -5f), marketPaper);
            board.GetComponent<Image>().raycastTarget = false;

            DateTime now = MarketNow;
            marketDisplayedDate = MarketMonth(now);
            marketLastObservedMonth = MarketMonth(now);
            int level = PortInvestmentLevel(session.CurrentPort);
            Text title = CreateText("Market Title", panel, new Vector2(600f, 64f), new Vector2(-535f, 312f),
                GameReadability.FontSize(32), FontStyle.Bold, TextAnchor.MiddleLeft, marketInk);
            title.resizeTextForBestFit = false;
            title.text = session.CurrentPort + " · 港口市场";
            AddSceneLabelOutline(title);
            Text tradeSummary = CreateText("Market Trade Summary", panel, new Vector2(980f, 64f), new Vector2(350f, 312f),
                18, FontStyle.Normal, TextAnchor.MiddleRight, marketInk);
            tradeSummary.text = CorePortEconomy.TradeSummary(session.CurrentPort);
            SetMarketBodyFont(tradeSummary, 18);
            AddSceneLabelOutline(tradeSummary);
            Text summary = CreateText("Market Summary", panel, new Vector2(1070f, 74f), new Vector2(-285f, 241f),
                18, FontStyle.Normal, TextAnchor.MiddleLeft, marketInk);
            summary.text = "游戏 " + PortMarketCatalog.MonthKey(now) + " 月行情　·　投资 Lv." + level +
                "　·　下次补货 " + PortMarketCatalog.NextRefreshLabel(now) + " 00:00　·　货舱 " + session.Cargo.UsedVolume.ToString("0.0") + "/" + session.CargoCapacityValue.ToString("0.0");
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            summary.verticalOverflow = VerticalWrapMode.Truncate;
            SetMarketBodyFont(summary, 18);
            AddSceneLabelOutline(summary);
            BuildMarketStorageModeControl(panel, marketInk);

            string[] columnIds = { "Item", "Buy", "Sell", "Quota", "Quantity", "Trade" };
            string[] columnNames = { "商品", "买入价", "卖出价", "本月余量 / 额度", "数量", "交易" };
            float[] columnPositions = { -680f, -455f, -233f, 35f, 306f, 645f };
            float[] columnWidths = { 235f, 210f, 210f, 306f, 196f, 340f };
            for (int columnIndex = 0; columnIndex < columnNames.Length; columnIndex++)
            {
                Text column = CreateText("Market Column Header " + columnIds[columnIndex], board,
                    new Vector2(columnWidths[columnIndex], 34f), new Vector2(columnPositions[columnIndex], 180f),
                    18, FontStyle.Bold, columnIndex == 0 ? TextAnchor.MiddleLeft :
                        columnIndex == 1 || columnIndex == 2 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter, marketInk);
                column.text = columnNames[columnIndex];
                SetMarketBodyFont(column, 18);
                AddSceneLabelOutline(column);
            }

            int start = marketPage * MarketRowsPerPage;
            int end = Math.Min(start + MarketRowsPerPage, PortMarketCatalog.AllItems.Count);
            float rowHeight = MarketRowsPerPage == 4 ? 98f : MarketRowsPerPage == 3 ? 132f : 200f;
            for (int index = start; index < end; index += 1)
            {
                PortMarketItem item = PortMarketCatalog.AllItems[index];
                int quantity = MarketQuantity(item);
                int remaining = MarketRemaining(session.CurrentPort, item, now, level);
                int purchased = MarketPurchased(session.CurrentPort, item, now);
                int cargo = session.GetMarketCargoAmount(PortMarketCatalog.Key(item));
                int stored = session.PortWarehouse.GetAmount(session.CurrentPort, PortMarketCatalog.Key(item));
                float y = 160f - rowHeight * .5f - (index - start) * (rowHeight + 10f);
                Color rowColor = index % 2 == 0 ? marketRowPrimary : marketRowAlternate;
                RectTransform row = CreateFixedPanel("Market Row " + PortMarketCatalog.Key(item), board,
                    new Vector2(1680f, rowHeight), new Vector2(0f, y), rowColor);
                CreateItemIcon("Market Item Icon " + PortMarketCatalog.Key(item), row,
                    ItemIconCatalog.ForCargo(PortMarketCatalog.Key(item)), new Vector2(-816f, 0f), 36f);
                Text name = CreateText("Market Item Name " + PortMarketCatalog.Key(item), row,
                    new Vector2(235f, rowHeight - 12f), new Vector2(-680f, 0f), 22, FontStyle.Normal,
                    TextAnchor.MiddleLeft, marketLightInk);
                name.text = PortMarketCatalog.Name(item) + "\n<size=" + GameReadability.FontSize(18) + ">随船 " + cargo + "\n本港仓储 " + stored + "</size>";
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                SetMarketBodyFont(name, 22);
                AddSceneLabelOutline(name);
                Text buy = CreateText("Market Buy Price " + PortMarketCatalog.Key(item), row,
                    new Vector2(210f, rowHeight - 12f), new Vector2(-455f, 0f), 22, FontStyle.Normal,
                    TextAnchor.MiddleRight, Gold);
                int purchaseStep = PortMarketCatalog.PurchaseStep(item);
                int saleStep = PortMarketCatalog.SaleStep(item);
                // Compare equal-sized lots. Rounding each single item before
                // multiplying would overstate the cost of a bulk trade load.
                int quoteQuantity = Math.Max(purchaseStep, saleStep);
                decimal buyStepCost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, quoteQuantity);
                decimal sellStepRevenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, item, now, saleStep);
                buy.text = quoteQuantity > 1
                    ? "每" + quoteQuantity + "单位 " + PortMarketCatalog.FormatPrice(buyStepCost) + " 金"
                    : PortMarketCatalog.FormatPrice(buyStepCost) + " 金 / 个";
                buy.horizontalOverflow = HorizontalWrapMode.Wrap;
                buy.verticalOverflow = VerticalWrapMode.Truncate;
                SetMarketBodyFont(buy, 22);
                AddSceneLabelOutline(buy);
                Text sell = CreateText("Market Sell Price " + PortMarketCatalog.Key(item), row,
                    new Vector2(210f, rowHeight - 12f), new Vector2(-233f, 0f), 22, FontStyle.Normal,
                    TextAnchor.MiddleRight, marketLightGold);
                sell.text = saleStep > 1
                    ? "每" + saleStep + "单位 " + PortMarketCatalog.FormatPrice(sellStepRevenue) + " 金"
                    : PortMarketCatalog.FormatPrice(sellStepRevenue) + " 金 / 个";
                sell.horizontalOverflow = HorizontalWrapMode.Wrap;
                sell.verticalOverflow = VerticalWrapMode.Truncate;
                SetMarketBodyFont(sell, 22);
                AddSceneLabelOutline(sell);
                Text quota = CreateText("Market Quota " + PortMarketCatalog.Key(item), row,
                    new Vector2(306f, rowHeight - 12f), new Vector2(35f, 0f), 18, FontStyle.Normal,
                    TextAnchor.MiddleCenter, marketLightInk);
                quota.text = "库存 " + PortMarketCatalog.DayState(session.CurrentPort, item, now, level).Stock +
                    " · 可购 " + remaining + "\n已购 " + purchased + " / 额度 " + PortMarketCatalog.MaxPurchaseQuota(item, level);
                SetMarketBodyFont(quota, 18);
                AddSceneLabelOutline(quota);

                int captured = index;
                float stepperSize = 42f + 8f * GameReadability.Level;
                float stepperY = stepperSize * .5f + 2f;
                Button decrease = CreateButton("Market Decrease " + PortMarketCatalog.Key(item), row,
                    new Vector2(stepperSize, stepperSize), new Vector2(439f, -stepperY), PanelRaised, Ink, 22);
                decrease.GetComponentInChildren<Text>().text = "−";
                SetMarketBodyFont(decrease.GetComponentInChildren<Text>(), 22);
                decrease.GetComponentInChildren<Text>().rectTransform.sizeDelta = Vector2.one * (stepperSize - 4f);
                AddMarketViewAction(decrease, () => AdjustMarketQuantity(PortMarketCatalog.AllItems[captured], -1), true);
                InputField amount = CreateMarketQuantityInput(row, item, quantity, Math.Max(remaining, cargo), marketLightInk);
                Button increase = CreateButton("Market Increase " + PortMarketCatalog.Key(item), row,
                    new Vector2(stepperSize, stepperSize), new Vector2(439f, stepperY), PanelRaised, Ink, 22);
                increase.GetComponentInChildren<Text>().text = "+";
                SetMarketBodyFont(increase.GetComponentInChildren<Text>(), 22);
                increase.GetComponentInChildren<Text>().rectTransform.sizeDelta = Vector2.one * (stepperSize - 4f);
                AddMarketViewAction(increase, () => AdjustMarketQuantity(PortMarketCatalog.AllItems[captured], 1), true);

                int capturedQuantity = quantity;
                decimal buyCost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, capturedQuantity);
                Button buyButton = CreateButton("Market Buy " + PortMarketCatalog.Key(item), row,
                    new Vector2(160f, rowHeight - 12f), new Vector2(555f, 0f), Gold, DeepSea, 16);
                WoodButtonSkin.SetRole(buyButton, WoodButtonRole.Primary);
                Text buyLabel = buyButton.GetComponentInChildren<Text>();
                buyLabel.rectTransform.sizeDelta = new Vector2(148f, rowHeight - 16f);
                buyLabel.text = MarketTradeButtonText(true, capturedQuantity, buyCost);
                buyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                buyLabel.verticalOverflow = VerticalWrapMode.Truncate;
                SetMarketBodyFont(buyLabel, 22);
                MarketPurchaseQuote purchase = CreateMarketPurchaseQuote(item, capturedQuantity, buyCost, now, level,
                    purchased, remaining);
                buyButton.interactable = Application.isPlaying && !editorPreviewBuilding && capturedQuantity <= remaining &&
                    session.ProfileGold >= buyCost && MarketPurchaseFitsMode(purchase);
                buyButton.onClick.AddListener(() => BuyQuotedMarketItem(purchase));
                int sellQuantity = MarketSaleQuantity(item);
                decimal sellRevenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, item, now, sellQuantity);
                Button sellButton = CreateButton("Market Sell " + PortMarketCatalog.Key(item), row,
                    new Vector2(160f, rowHeight - 12f), new Vector2(735f, 0f), PanelRaised, Ink, 16);
                WoodButtonSkin.SetRole(sellButton, WoodButtonRole.Secondary);
                Text sellLabel = sellButton.GetComponentInChildren<Text>();
                sellLabel.rectTransform.sizeDelta = new Vector2(148f, rowHeight - 16f);
                sellLabel.text = MarketTradeButtonText(false, sellQuantity, sellRevenue);
                sellLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                sellLabel.verticalOverflow = VerticalWrapMode.Truncate;
                SetMarketBodyFont(sellLabel, 22);
                sellButton.interactable = Application.isPlaying && !editorPreviewBuilding && sellQuantity > 0 &&
                    sellRevenue > 0 && cargo >= sellQuantity;
                AddMarketViewAction(sellButton, () =>
                {
                    SellQuotedMarketItem(purchase, sellQuantity, sellRevenue, cargo);
                });
                BindMarketQuantityInput(amount, item, buyButton, sellButton, now, level, purchased, remaining);
            }

            Text investmentNotice = CreateText("Market Investment Notice", board,
                new Vector2(810f, 72f), new Vector2(-425f, -309f), 18, FontStyle.Normal,
                TextAnchor.MiddleLeft, marketInk);
            investmentNotice.text = "各港每月首日补货；投资增加库存与额度，已购数量保留。";
            SetMarketBodyFont(investmentNotice, 18);
            AddSceneLabelOutline(investmentNotice);
            BuildMarketPagination(board, marketInk);
            Button openInvestment = CreateButton("Market Open Investment", board,
                new Vector2(280f, 76f), new Vector2(690f, -309f), PanelRaised, Ink, 18);
            openInvestment.GetComponentInChildren<Text>().text = "港口投资\n提升额度";
            SetMarketBodyFont(openInvestment.GetComponentInChildren<Text>(), 22);
            openInvestment.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(268f, 74f);
            AddMarketViewAction(openInvestment, () =>
            {
                activePortFacility = 7;
                pendingSupplyOrder.Clear();
                portNotice = "港口投资增加每月库存和额度；升级立即提供新增货量，不重置本月已购记录。";
                Render();
            });
        }

        private void BuyMarketItem(PortMarketItem item)
        {
            if (marketPurchaseQuotes.TryGetValue(item, out MarketPurchaseQuote quote)) BuyQuotedMarketItem(quote);
        }

        private void SellMarketItem(PortMarketItem item)
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port) return;
            DateTime now = MarketNow;
            if (RefreshExpiredMarketQuote(now)) return;
            int quantity = MarketSaleQuantity(item);
            int available = session.GetMarketCargoAmount(PortMarketCatalog.Key(item));
            if (quantity <= 0 || available < quantity)
            {
                portNotice = "市场出售失败：库存不足。";
                Render();
                return;
            }
            decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, item, now, quantity);
            if (revenue <= 0)
            {
                portNotice = "本批货物未产生可售金额，请合并更多数量后出售。";
                Render();
                return;
            }
            if (!session.TrySellMarketCargo(PortMarketCatalog.Key(item), quantity, revenue))
            {
                portNotice = "市场出售失败：账户金币已达上限。";
                Render();
                return;
            }
            SaveProgress();
            portNotice = "已在" + session.CurrentPort + "出售" + PortMarketCatalog.Name(item) + " ×" + quantity + "。";
            Render();
        }

        private void SellQuotedMarketItem(MarketPurchaseQuote quote, int quantity, decimal revenue, int cargo)
        {
            if (!Application.isPlaying || editorPreviewBuilding || pendingMarketPurchase != null ||
                !MarketQuoteHasCurrentIdentity(quote)) return;
            if (RefreshExpiredMarketQuote(MarketNow)) return;
            if (quantity <= 0 || revenue <= 0m || MarketSaleQuantity(quote.Item) != quantity ||
                session.GetMarketCargoAmount(PortMarketCatalog.Key(quote.Item)) != cargo ||
                PortMarketCatalog.CalculateSellRevenue(quote.Port, quote.Item, MarketNow, quantity) != revenue)
            {
                quote.Consumed = true;
                portNotice = "出售报价或船货已变化，请核对数量后再次交易。";
                Render();
                return;
            }
            // Invalidate before committing, including callbacks already queued by
            // input focus changes. One displayed batch can only be sold once.
            quote.Consumed = true;
            SellMarketItem(quote.Item);
        }
    }
}
