using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class MarketRowView
        {
            public RectTransform Root;
            public Text Name, BuyPrice, BuyUnit;
            public Button Icon;
            public bool IsAlive => Root != null && Name != null && BuyPrice != null && BuyUnit != null && Icon != null;
        }

        private readonly Dictionary<PortMarketItem, MarketRowView> marketRowViews =
            new Dictionary<PortMarketItem, MarketRowView>();
        private CoreVoyageSession marketViewSession;
        private string marketViewPort;
        private RectTransform marketCatalogGrid;
        private Text marketViewTitle, marketViewSummary;
        private Button marketViewStorageMode, marketViewInvestment;

        private void ReleaseMarketRetainedView()
        {
            marketRowViews.Clear();
            marketViewSession = null;
            marketViewPort = null;
            marketCatalogGrid = null;
            marketViewTitle = null;
            marketViewSummary = null;
            marketViewStorageMode = null;
            marketViewInvestment = null;
            ResetMarketItemDetailView();
            ResetMarketSceneView();
        }

        private bool TryRefreshMarketFacilityView()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null ||
                session.Phase != CoreDemoPhase.Port ||
                activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex ||
                !ReferenceEquals(session, marketViewSession) || session.CurrentPort != marketViewPort ||
                marketPurchaseView == null || !marketPurchaseView.gameObject.activeInHierarchy ||
                marketPurchaseView.parent != contentRoot || marketViewTitle == null || marketViewSummary == null ||
                marketCatalogGrid == null || marketViewStorageMode == null ||
                marketViewInvestment == null || marketRowViews.Count != PortMarketCatalog.AllItems.Count) return false;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                if (!marketRowViews.TryGetValue(item, out MarketRowView row) || !row.IsAlive) return false;

            marketPurchaseRevision++;
            foreach (MarketPurchaseQuote quote in marketPurchaseQuotes.Values) quote.Consumed = true;
            marketPurchaseQuotes.Clear();
            pendingMarketPurchase = null;
            CloseMarketPurchaseChoiceView();
            RefreshMarketFacilityContents();
            return true;
        }

        private void RefreshMarketFacilityContents()
        {
            DateTime now = MarketNow;
            marketDisplayedDate = MarketMonth(now);
            marketLastObservedMonth = marketDisplayedDate;
            int level = PortInvestmentLevel(session.CurrentPort);
            double cargoRoom = Math.Max(0d, session.CargoCapacityValue - session.Cargo.UsedVolume);
            marketViewSummary.text = "货舱余量 " + cargoRoom.ToString("0.0");
            int discount = PortMarketCatalog.FamePurchaseDiscountPercent(session.Cargo.Fame);
            if (discount > 0) marketViewSummary.text += "　·　采购优惠 " + discount + "%";
            marketViewStorageMode.GetComponentInChildren<Text>().text = MarketStorageModeLabel(EffectiveMarketStorageMode);
            marketViewStorageMode.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive;
            marketViewStorageMode.onClick = new Button.ButtonClickedEvent();
            BindMarketStorageModeAction(marketViewStorageMode);
            marketViewInvestment.name = "Market Open Investment";
            marketViewInvestment.GetComponentInChildren<Text>().text = "港口投资 · 提升额度";
            marketViewInvestment.onClick = new Button.ButtonClickedEvent();
            BindMarketInvestmentAction(marketViewInvestment);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) RefreshMarketRow(marketRowViews[item], item, now);
            RefreshMarketSceneShops();
            RefreshMarketItemDetail(now, level);
        }

        private void RefreshMarketRow(MarketRowView row, PortMarketItem item, DateTime now)
        {
            row.Name.text = PortMarketCatalog.Name(item);
            int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
            decimal buy = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, buyLot, session.Cargo.Fame);
            row.BuyPrice.text = PortMarketCatalog.FormatPrice(buy) + " 金";
            row.BuyUnit.text = "每" + buyLot + "单位";
            row.Icon.interactable = Application.isPlaying && !editorPreviewBuilding;
            row.Icon.onClick = new Button.ButtonClickedEvent();
            AddMarketViewAction(row.Icon, () => OpenMarketItemDetail(item));
            Button tab = row.Root.GetComponent<Button>();
            if (tab != null)
            {
                tab.onClick = new Button.ButtonClickedEvent();
                AddMarketViewAction(tab, () => OpenMarketItemDetail(item));
            }
        }

        private void BindMarketInvestmentAction(Button button)
        {
            AddMarketViewAction(button, () =>
            {
                activePortFacility = PortFacilityArtCatalog.MaintenanceFacilityIndex;
                pendingSupplyOrder.Clear();
                portNotice = "港口投资增加每月库存和额度；升级立即提供新增货量，不重置本月已购记录。";
                Render();
            });
        }

        private void CloseMarketPurchaseChoiceView()
        {
            RestoreMarketNavigationAfterChoice();
            if (marketPurchaseView == null) return;
            Transform shade = marketPurchaseView.Find("Market Purchase Choice Shade");
            if (shade != null)
            {
                shade.gameObject.SetActive(false);
                shade.SetParent(null, false);
                Destroy(shade.gameObject);
            }
            Transform board = marketPurchaseView.Find("Market Board");
            CanvasGroup group = board != null ? board.GetComponent<CanvasGroup>() : null;
            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }
    }
}


