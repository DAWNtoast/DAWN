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
            if (RefreshDepartureBasicSupplyMonth()) return;
            if (activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex ||
                activePortFacility == PortFacilityArtCatalog.MaintenanceFacilityIndex || activePortFacility == PortFacilityArtCatalog.VoyageFacilityIndex ||
                activePortFacility == PortFacilityArtCatalog.ComponentShopFacilityIndex)
            {
                if (activePortFacility == PortFacilityArtCatalog.VoyageFacilityIndex && contentRoot != null &&
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
            int requested = marketQuantities.TryGetValue(item, out int value)
                ? Math.Max(0, value) : PortMarketCatalog.PurchaseStep(item);
            return Math.Min(requested, MarketQuantityLimit(item));
        }

        private int MarketQuantityLimit(PortMarketItem item) => Math.Max(
            session.GetMarketCargoAmount(PortMarketCatalog.Key(item)),
            MarketRemaining(session.CurrentPort, item, MarketNow, PortInvestmentLevel(session.CurrentPort)));

        private int MarketSaleQuantity(PortMarketItem item) => Math.Min(
            session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), MarketQuantity(item));

        private void BuildMarketFacility()
        {
            BeginMarketPurchaseView();
            RectTransform panel = CreateRect("Market Panel", contentRoot, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            marketPurchaseView = panel;
            marketViewSession = session;
            marketViewPort = session.CurrentPort;
            RectTransform board = CreateRect("Market Board", panel, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            BuildMarketSceneShops(board);
            BuildMarketItemDetail(board, Html("#2C1B12"));

            RectTransform footer = CreateFixedPanel("Market Footer", board,
                new Vector2(1860f, 70f), new Vector2(0f, -495f), Html("#211910"));
            NauticalSurfaceSkin.ApplyPanel(footer.GetComponent<Image>(), Html("#211910"));
            footer.GetComponent<Image>().raycastTarget = true;
            marketViewTitle = CreateText("Market Title", footer, new Vector2(650f, 52f),
                new Vector2(-565f, 0f), 19, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            marketViewTitle.text = CorePortEconomy.TradeSummary(session.CurrentPort);
            marketViewSummary = CreateText("Market Summary", footer, new Vector2(350f, 52f),
                new Vector2(-42f, 0f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            BuildMarketStorageModeControl(footer);
            marketViewStorageMode.GetComponent<RectTransform>().sizeDelta = new Vector2(310f, 46f);
            marketViewStorageMode.GetComponent<RectTransform>().anchoredPosition = new Vector2(338f, 0f);
            marketViewInvestment = CreateButton("Market Open Investment", footer,
                new Vector2(320f, 46f), new Vector2(720f, 0f), PanelRaised, Ink, 20);
            RefreshMarketFacilityContents();
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

