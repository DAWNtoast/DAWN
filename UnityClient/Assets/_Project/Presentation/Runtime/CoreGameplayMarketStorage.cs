using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum MarketPurchaseStorageMode
    {
        Warehouse = 0,
        Ship = 1,
        Ask = 2,
    }

    public sealed partial class CoreGameplayDemoController
    {
        public const string MarketPurchaseStorageModeKey = "ShatteredSail.Market.PurchaseStorageMode.v1";

        public MarketPurchaseStorageMode MarketStorageMode
        {
            get
            {
                if (!Application.isPlaying || editorPreviewBuilding) return MarketPurchaseStorageMode.Ship;
                int saved = PlayerPrefs.GetInt(MarketPurchaseStorageModeKey, (int)MarketPurchaseStorageMode.Ship);
                return saved >= 0 && saved <= (int)MarketPurchaseStorageMode.Ask
                    ? (MarketPurchaseStorageMode)saved : MarketPurchaseStorageMode.Ship;
            }
        }

        private MarketPurchaseStorageMode EffectiveMarketStorageMode =>
            tutorialActive ? MarketPurchaseStorageMode.Ship : MarketStorageMode;

        private sealed class MarketPurchaseQuote
        {
            public readonly CoreVoyageSession Session;
            public readonly RectTransform View;
            public readonly int Revision;
            public readonly string Port;
            public readonly DateTime Date;
            public readonly PortMarketItem Item;
            public readonly int Quantity;
            public readonly decimal Cost;
            public readonly int InvestmentLevel;
            public readonly int Purchased;
            public readonly int Remaining;
            public readonly MarketPurchaseStorageMode Mode;
            public bool Consumed;

            public MarketPurchaseQuote(CoreVoyageSession session, RectTransform view, int revision, DateTime date,
                PortMarketItem item, int quantity, decimal cost, int level, int purchased, int remaining,
                MarketPurchaseStorageMode mode)
            {
                Session = session;
                View = view;
                Revision = revision;
                Port = session.CurrentPort;
                Date = MarketMonth(date);
                Item = item;
                Quantity = quantity;
                Cost = cost;
                InvestmentLevel = level;
                Purchased = purchased;
                Remaining = remaining;
                Mode = mode;
            }
        }

        private readonly Dictionary<PortMarketItem, MarketPurchaseQuote> marketPurchaseQuotes =
            new Dictionary<PortMarketItem, MarketPurchaseQuote>();
        private MarketPurchaseQuote pendingMarketPurchase;
        private RectTransform marketPurchaseView;
        private int marketPurchaseRevision;

        private void BeginMarketPurchaseView()
        {
            marketPurchaseRevision++;
            foreach (MarketPurchaseQuote quote in marketPurchaseQuotes.Values) quote.Consumed = true;
            marketPurchaseQuotes.Clear();
            marketRowViews.Clear();
            marketCatalogGrid = null;
            marketViewTitle = null;
            ResetMarketItemDetailView();
            ResetMarketSceneView();
            marketViewSummary = null;
            marketViewStorageMode = null;
            marketViewInvestment = null;
            marketViewSession = null;
            marketViewPort = null;
            pendingMarketPurchase = null;
            marketPurchaseView = null;
        }

        private MarketPurchaseQuote CreateMarketPurchaseQuote(PortMarketItem item, int quantity, decimal cost,
            DateTime now, int level, int purchased, int remaining)
        {
            var quote = new MarketPurchaseQuote(session, marketPurchaseView, marketPurchaseRevision, now,
                item, quantity, cost, level, purchased, remaining, EffectiveMarketStorageMode);
            marketPurchaseQuotes[item] = quote;
            return quote;
        }

        private void BuildMarketStorageModeControl(RectTransform panel)
        {
            Button mode = CreateButton("Market Storage Mode", panel, new Vector2(340f, 40f),
                new Vector2(-388f, -292f), Gold, DeepSea, 18);
            marketViewStorageMode = mode;
            mode.GetComponentInChildren<Text>().text = MarketStorageModeLabel(EffectiveMarketStorageMode);
            mode.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive;
            BindMarketStorageModeAction(mode);
        }

        private void BindMarketStorageModeAction(Button mode)
        {
            AddMarketViewAction(mode, () =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || tutorialActive) return;
                var next = (MarketPurchaseStorageMode)(((int)MarketStorageMode + 1) % 3);
                PlayerPrefs.SetInt(MarketPurchaseStorageModeKey, (int)next);
                PlayerPrefs.Save();
                portNotice = MarketStorageModeLabel(next) + "，已记住此采购偏好。";
                Render();
            });
        }

        private static string MarketStorageModeLabel(MarketPurchaseStorageMode mode)
        {
            switch (mode)
            {
                case MarketPurchaseStorageMode.Warehouse: return "购买后进入仓库";
                case MarketPurchaseStorageMode.Ask: return "每次购买时询问";
                default: return "购买后进入船只仓库";
            }
        }

        private void AddMarketViewAction(Button button, Action action, bool cancelsPendingPurchase = false)
        {
            RectTransform view = marketPurchaseView;
            int revision = marketPurchaseRevision;
            string port = session.CurrentPort;
            CoreVoyageSession owner = session;
            button.onClick.AddListener(() =>
            {
                if (pendingMarketPurchase != null && !cancelsPendingPurchase || view == null || !view.gameObject.activeInHierarchy ||
                    marketPurchaseView != view || marketPurchaseRevision != revision ||
                    !ReferenceEquals(session, owner) || session.Phase != CoreDemoPhase.Port ||
                    session.CurrentPort != port || activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex) return;
                action();
            });
        }

        private bool MarketQuoteHasCurrentIdentity(MarketPurchaseQuote quote)
        {
            return quote != null && !quote.Consumed && ReferenceEquals(quote.Session, session) &&
                quote.View != null && quote.View.gameObject.activeInHierarchy &&
                quote.View == marketPurchaseView && quote.Revision == marketPurchaseRevision &&
                session != null && session.Phase == CoreDemoPhase.Port && session.CurrentPort == quote.Port &&
                activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex &&
                marketPurchaseQuotes.TryGetValue(quote.Item, out MarketPurchaseQuote displayed) && ReferenceEquals(displayed, quote);
        }

        private bool ValidateMarketPurchaseQuote(MarketPurchaseQuote quote)
        {
            if (!Application.isPlaying || editorPreviewBuilding || !MarketQuoteHasCurrentIdentity(quote)) return false;
            DateTime now = MarketNow;
            int level = PortInvestmentLevel(quote.Port);
            bool sameQuote = MarketMonth(now) == quote.Date && marketDisplayedDate == quote.Date &&
                EffectiveMarketStorageMode == quote.Mode && MarketQuantity(quote.Item) == quote.Quantity &&
                level == quote.InvestmentLevel && MarketPurchased(quote.Port, quote.Item, now) == quote.Purchased &&
                MarketRemaining(quote.Port, quote.Item, now, level) == quote.Remaining &&
                PortMarketCatalog.CalculateBuyCost(quote.Port, quote.Item, now, quote.Quantity, session.Cargo.Fame) == quote.Cost;
            if (sameQuote && quote.Quantity > 0 && quote.Quantity <= quote.Remaining && quote.Cost > 0) return true;
            quote.Consumed = true;
            pendingMarketPurchase = null;
            portNotice = "采购报价已变化，请重新核对价格、数量与本月余量后购买。";
            Render();
            return false;
        }

        private bool MarketPurchaseFitsMode(MarketPurchaseQuote quote)
        {
            bool warehouse = MarketWarehouseCanReceive(quote);
            bool ship = MarketShipCanReceive(quote);
            return quote.Mode == MarketPurchaseStorageMode.Warehouse ? warehouse :
                quote.Mode == MarketPurchaseStorageMode.Ship ? ship : warehouse || ship;
        }

        private bool MarketWarehouseCanReceive(MarketPurchaseQuote quote) =>
            quote.Quantity > 0 && (long)session.PortWarehouse.GetAmount(quote.Port, PortMarketCatalog.Key(quote.Item)) +
                quote.Quantity <= int.MaxValue;

        private bool MarketShipCanReceive(MarketPurchaseQuote quote) =>
            quote.Quantity > 0 && CoreEconomyBalance.HasNonNegativeCargo(session.Cargo) &&
            (long)session.GetMarketCargoAmount(PortMarketCatalog.Key(quote.Item)) + quote.Quantity <= int.MaxValue &&
            session.Cargo.UsedVolume + quote.Quantity * (double)PortMarketCatalog.CargoVolume(quote.Item) <=
                session.CargoCapacityValue + .001d;

        private void BuyQuotedMarketItem(MarketPurchaseQuote quote)
        {
            if (pendingMarketPurchase != null || !ValidateMarketPurchaseQuote(quote)) return;
            if (session.ProfileGold < quote.Cost || !MarketPurchaseFitsMode(quote))
            {
                quote.Consumed = true;
                portNotice = "市场采购失败：金币不足或所选收货位置空间不足。";
                Render();
                return;
            }
            if (quote.Mode == MarketPurchaseStorageMode.Ask)
            {
                pendingMarketPurchase = quote;
                BuildMarketPurchaseChoice(quote);
                return;
            }
            CommitMarketPurchase(quote, quote.Mode, false);
        }

        private void CommitMarketPurchase(MarketPurchaseQuote quote, MarketPurchaseStorageMode destination, bool fromChoice)
        {
            if (fromChoice ? !ReferenceEquals(pendingMarketPurchase, quote) : pendingMarketPurchase != null) return;
            if (!ValidateMarketPurchaseQuote(quote)) return;
            bool warehouse = destination == MarketPurchaseStorageMode.Warehouse;
            if (destination != MarketPurchaseStorageMode.Warehouse && destination != MarketPurchaseStorageMode.Ship) return;
            if (fromChoice && quote.Mode != MarketPurchaseStorageMode.Ask || !fromChoice && destination != quote.Mode) return;
            if (session.ProfileGold < quote.Cost || !(warehouse ? MarketWarehouseCanReceive(quote) : MarketShipCanReceive(quote)))
            {
                quote.Consumed = true;
                pendingMarketPurchase = null;
                portNotice = "采购未完成：金币或收货空间已变化，请重新选择。";
                Render();
                return;
            }
            // Invalidate before either destination transaction. The same quote
            // cannot be committed twice or reused for the other destination.
            quote.Consumed = true;
            pendingMarketPurchase = null;
            string key = PortMarketCatalog.Key(quote.Item);
            bool bought = warehouse ? session.TryPurchasePortWarehouseCargo(key, quote.Quantity, quote.Cost) :
                session.TryPurchaseMarketCargo(key, quote.Quantity, quote.Cost, PortMarketCatalog.CargoVolume(quote.Item));
            if (!bought)
            {
                portNotice = "采购未完成：库存、金币或收货空间不足，请重新核对。";
                Render();
                return;
            }
            PlayerPrefs.SetInt(MarketPurchaseKey(quote.Port, quote.Date, quote.Item), quote.Purchased + quote.Quantity);
            SaveProgress();
            portNotice = "已购入" + PortMarketCatalog.Name(quote.Item) + " ×" + quote.Quantity + "，支付 " + PortMarketCatalog.FormatPrice(quote.Cost) +
                " 金币，" + (warehouse ? "存入" + quote.Port + "仓库。" : "装入船只仓库。" );
            Render();
        }

        private void BuildMarketPurchaseChoice(MarketPurchaseQuote quote)
        {
            LockMarketNavigationForChoice();
            // A full market-sized hit surface blocks all row controls while the
            // choice is open. A queued quantity edit cancels this quote before
            // rebuilding; other row callbacks cannot act behind the choice.
            Transform board = quote.View.Find("Market Board");
            if (board != null)
            {
                CanvasGroup group = board.GetComponent<CanvasGroup>();
                if (group == null) group = board.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            RectTransform shade = CreateFixedPanel("Market Purchase Choice Shade", quote.View,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(0f, 0f, 0f, .76f));
            shade.GetComponent<Image>().raycastTarget = true;
            RectTransform dialog = CreateFixedPanel("Market Purchase Choice", shade,
                new Vector2(1060f, 500f), Vector2.zero, new Color(0.90f, 0.82f, 0.68f));
            NauticalSurfaceSkin.ApplyReadingPanel(dialog.GetComponent<Image>());
            Color marketInk = Html("#2C1B12");
            Text title = CreateText("Market Purchase Choice Title", dialog, new Vector2(960f, 50f),
                new Vector2(0f, 193f), 29, FontStyle.Bold, TextAnchor.MiddleCenter, marketInk);
            title.text = "选择收货位置";
            Text terms = CreateText("Market Purchase Choice Quote", dialog, new Vector2(940f, 64f),
                new Vector2(0f, 113f), 23, FontStyle.Normal, TextAnchor.MiddleCenter, marketInk);
            terms.text = PortMarketCatalog.Name(quote.Item) + " ×" + quote.Quantity + "　总价 " +
                PortMarketCatalog.FormatPrice(quote.Cost) + " 金币\n选好位置后付款；取消不扣款。";
            terms.lineSpacing = 1.15f;
            terms.horizontalOverflow = HorizontalWrapMode.Wrap;
            terms.verticalOverflow = VerticalWrapMode.Truncate;
            bool warehouseFits = MarketWarehouseCanReceive(quote), shipFits = MarketShipCanReceive(quote);
            bool canPay = session.ProfileGold >= quote.Cost;
            Button warehouse = CreateButton("Market Buy To Warehouse", dialog, new Vector2(430f, 60f),
                new Vector2(-247f, 16f), Gold, DeepSea, 22);
            warehouse.GetComponentInChildren<Text>().text = "存入本港仓库";
            warehouse.interactable = canPay && warehouseFits;
            warehouse.onClick.AddListener(() => CommitMarketPurchase(quote, MarketPurchaseStorageMode.Warehouse, true));
            Button ship = CreateButton("Market Buy To Ship", dialog, new Vector2(430f, 60f),
                new Vector2(247f, 16f), Gold, DeepSea, 22);
            ship.GetComponentInChildren<Text>().text = "装入船只仓库";
            ship.interactable = canPay && shipFits;
            ship.onClick.AddListener(() => CommitMarketPurchase(quote, MarketPurchaseStorageMode.Ship, true));
            Text warehouseStatus = CreateText("Market Warehouse Purchase Status", dialog, new Vector2(430f, 72f),
                new Vector2(-247f, -64f), 19, FontStyle.Normal, TextAnchor.MiddleCenter, marketInk);
            warehouseStatus.text = !warehouseFits ? "本港仓储数量已达上限" : !canPay ? "船长账户金币不足" :
                quote.Port + "仓储 " + session.PortWarehouse.GetAmount(quote.Port, PortMarketCatalog.Key(quote.Item)) +
                " → " + ((long)session.PortWarehouse.GetAmount(quote.Port, PortMarketCatalog.Key(quote.Item)) + quote.Quantity) +
                "\n物资留在本港，不占船舱";
            Text shipStatus = CreateText("Market Ship Purchase Status", dialog, new Vector2(430f, 72f),
                new Vector2(247f, -64f), 19, FontStyle.Normal, TextAnchor.MiddleCenter, marketInk);
            shipStatus.text = !shipFits ? "货舱空间不足" : !canPay ? "船长账户金币不足" :
                "随船货舱 " + session.Cargo.UsedVolume.ToString("0.0") + " / " + session.CargoCapacityValue.ToString("0.0") +
                "\n本批占用 " + (quote.Quantity * PortMarketCatalog.CargoVolume(quote.Item)).ToString("0.0") + " 舱位";
            foreach (Text status in new[] { warehouseStatus, shipStatus })
            {
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 15;
                status.resizeTextMaxSize = 19;
            }
            Button cancel = CreateButton("Cancel Market Purchase", dialog, new Vector2(430f, 52f),
                new Vector2(0f, -181f), PanelRaised, Ink, 22);
            cancel.GetComponentInChildren<Text>().text = "取消购买";
            cancel.onClick.AddListener(() =>
            {
                if (!ReferenceEquals(pendingMarketPurchase, quote) || !MarketQuoteHasCurrentIdentity(quote)) return;
                CancelPendingMarketPurchase();

            });
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }
    }
}

