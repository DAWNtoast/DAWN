using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureProvisionPanel;
        private Text departureProvisionSummary;
        private Button departureProvisionBuy;
        private DateTime departureProvisionDisplayedDate;

        private sealed class DepartureProvisionQuote
        {
            public DateTime Date;
            public int VoyageDays, TargetDays, Water, Food;
            public decimal Cost;
            public double AvailableDays;
            public string Reason = string.Empty;
            public bool HasItems => Water > 0 || Food > 0;
            public bool CanPurchase => HasItems && Reason.Length == 0;
        }

        private DepartureProvisionQuote QuoteDepartureProvisions(DateTime now)
        {
            EnsureDepartureCrewSelection();
            int voyageDays = session.SelectedTreasureMap != null
                ? session.SelectedTreasureMap.Rank.RecommendedVoyageDays
                : CoreVoyageSession.Routes[session.SelectedRouteIndex].RecommendedVoyageDays;
            double daily = session.EstimateDailyRationConsumption(
                selectedDepartureOrdinaryCrew + selectedDepartureSpecialCrew.Count, HasSelectedDepartureProfession(4));
            var quote = new DepartureProvisionQuote {
                Date = MarketMonth(now), VoyageDays = voyageDays, TargetDays = voyageDays + 2,
                AvailableDays = daily > 0 ? Math.Min(session.Cargo.Water, session.Cargo.Food) / daily : 0
            };
            int target = (int)Math.Min(int.MaxValue, Math.Ceiling(daily * quote.TargetDays - 1e-9d));
            quote.Water = ProvisionShortfall(target, session.Cargo.Water);
            quote.Food = ProvisionShortfall(target, session.Cargo.Food);
            decimal cost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Water, now, quote.Water) +
                PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Food, now, quote.Food);
            quote.Cost = cost;
            int level = PortInvestmentLevel(session.CurrentPort);
            if (session.Phase != CoreDemoPhase.Port) quote.Reason = "返回港口后才能采购。";
            else if (daily <= 0) quote.Reason = "请先配置出航船员。";
            else if (!CoreEconomyBalance.HasNonNegativeCargo(session.Cargo)) quote.Reason = "货物数量异常，请先检查货舱。";
            else if (!quote.HasItems) quote.Reason = string.Empty;
            else if (quote.Water > MarketRemaining(session.CurrentPort, PortMarketItem.Water, now, level) ||
                quote.Food > MarketRemaining(session.CurrentPort, PortMarketItem.Food, now, level))
                quote.Reason = "本月水粮库存或额度不足，请到市场查看。";
            else if (cost > session.ProfileGold) quote.Reason = "金币不足，还缺 " + PortMarketCatalog.FormatPrice(cost - session.ProfileGold) + " 金币。";
            else if ((double)session.Cargo.UsedVolume + quote.Water * 0.07d + quote.Food * 0.09d > session.CargoCapacityValue + .001d)
                quote.Reason = "货舱空间不足；请先卸货或前往市场调整数量。";
            return quote;
        }

        private static int ProvisionShortfall(int target, int existing)
        {
            long missing = Math.Max(0L, (long)target - existing);
            return (int)Math.Min(int.MaxValue, missing);
        }

        private void BuildDepartureProvisionPurchase(RectTransform panel)
        {
            DateTime now = MarketNow;
            // A callback may already have rebuilt the quote at the new month.
            // Record that batch so Update does not destroy the new controls again.
            marketLastObservedMonth = MarketMonth(now);
            DepartureProvisionQuote quote = QuoteDepartureProvisions(now);
            Text summary = CreateText("Departure Provision Quote", panel, new Vector2(520f, 62f),
                new Vector2(0f, -182f), 16, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            // Parent the icons to the quote so a route/seed requote replaces them
            // together with the existing text, without leaving duplicate images.
            CreateItemIcon("Departure Provision Water Icon", summary.transform, ItemIconCatalog.ForCargo("water"),
                new Vector2(-279f, 19f), 32f);
            CreateItemIcon("Departure Provision Food Icon", summary.transform, ItemIconCatalog.ForCargo("food"),
                new Vector2(-279f, -19f), 32f);
            summary.text = "探索备航 " + quote.TargetDays + " 天（建议 " + quote.VoyageDays + "＋余量 2）· 现有约 " + quote.AvailableDays.ToString("0.0") + " 天\n" +
                (quote.HasItems ? "需购淡水 +" + quote.Water + "、食物 +" + quote.Food + " · 合计 " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金币（市场价）" : "水粮已备足。") +
                "\n" + (quote.Reason.Length > 0 ? quote.Reason : "水粮直接装船，不受市场收货方式影响。");
            Button buy = CreateButton("Departure Buy Provisions", panel, new Vector2(320f, 40f),
                new Vector2(-100f, -237f), Gold, DeepSea, 16);
            buy.GetComponentInChildren<Text>().text = quote.HasItems ? "一键补齐水粮 · " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金" : "水粮已备足";
            buy.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive && quote.CanPurchase;
            departureProvisionPanel = panel;
            departureProvisionSummary = summary;
            departureProvisionBuy = buy;
            departureProvisionDisplayedDate = quote.Date;
            CoreVoyageSession quotedSession = session;
            string quotedPort = session.CurrentPort;
            int quotedShip = session.SelectedShipIndex;
            buy.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || tutorialActive || panel == null ||
                    !panel.gameObject.activeInHierarchy || panel != departureProvisionPanel || buy != departureProvisionBuy ||
                    !ReferenceEquals(session, quotedSession) || session.Phase != CoreDemoPhase.Port || activePortFacility != 8 ||
                    departureCrewConfigurationOpen || session.CurrentPort != quotedPort || session.SelectedShipIndex != quotedShip) return;
                CommitDepartureSeed(panel);
                BuyDepartureProvisions(departureProvisionDisplayedDate);
            });
            if (tutorialActive)
            {
                summary.text = "教学补给已准备。\n教学后可按航期补齐水粮。";
                buy.GetComponentInChildren<Text>().text = "教学补给已准备";
            }
            DepartureLayout.ApplySetup(panel);
        }

        private void RefreshDepartureProvisionPurchase(string notice)
        {
            if (departureProvisionPanel == null || !departureProvisionPanel.gameObject.activeInHierarchy ||
                departureProvisionSummary == null || departureProvisionBuy == null || session == null ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 8 || departureCrewConfigurationOpen) return;
            DateTime now = MarketNow;
            DepartureProvisionQuote quote = QuoteDepartureProvisions(now);
            departureProvisionDisplayedDate = quote.Date;
            marketLastObservedMonth = quote.Date;
            departureProvisionSummary.text = "探索备航 " + quote.TargetDays + " 天（建议 " + quote.VoyageDays + "＋余量 2）· 现有约 " + quote.AvailableDays.ToString("0.0") + " 天\n" +
                (quote.HasItems ? "需购淡水 +" + quote.Water + "、食物 +" + quote.Food + " · 合计 " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金币（市场价）" : "水粮已备足。") +
                "\n" + (!string.IsNullOrEmpty(notice) ? notice : quote.Reason.Length > 0 ? quote.Reason : "水粮直接装船，不受市场收货方式影响。");
            departureProvisionBuy.GetComponentInChildren<Text>().text = quote.HasItems
                ? "一键补齐水粮 · " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金" : "水粮已备足";
            departureProvisionBuy.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive && quote.CanPurchase;
            DepartureLayout.ApplySetup(departureProvisionPanel);
        }

        private void BuyDepartureProvisions(DateTime displayedDate)
        {
            if (!Application.isPlaying || editorPreviewBuilding || tutorialActive || session == null ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 8) return;
            DateTime now = MarketNow;
            if (MarketMonth(now) != displayedDate)
            {
                portNotice = "新月报价已刷新，请核价后重购。";
                RefreshDepartureProvisionPurchase(portNotice);
                return;
            }
            DepartureProvisionQuote quote = QuoteDepartureProvisions(now);
            if (!quote.CanPurchase)
            {
                portNotice = quote.HasItems ? quote.Reason : "水粮已备足。";
                RefreshDepartureProvisionPurchase(portNotice);
                return;
            }
            if (!session.TryPurchaseMarketProvisions(quote.Water, quote.Food, quote.Cost))
            {
                portNotice = "采购未完成，请检查金币和货舱；未扣款。";
                RefreshDepartureProvisionPurchase(portNotice);
                return;
            }
            if (quote.Water > 0) PlayerPrefs.SetInt(MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Water),
                MarketPurchased(session.CurrentPort, PortMarketItem.Water, now) + quote.Water);
            if (quote.Food > 0) PlayerPrefs.SetInt(MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Food),
                MarketPurchased(session.CurrentPort, PortMarketItem.Food, now) + quote.Food);
            SaveProgress();
            portNotice = "水粮已直接装入船舱：淡水 +" + quote.Water + "、食物 +" + quote.Food + "，花费 " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金币。";
            Render();
        }
    }
}
