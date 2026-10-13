using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private DateTime maintenanceDisplayedDate = DateTime.MinValue;

        private sealed class MaintenanceSupplyQuote
        {
            public decimal Cost;
            public float ProjectedVolume;
            public bool HasItems, FitsCargo = true, CanAfford = true, WithinQuota = true;
            public string Reason = string.Empty;
            public bool CanPurchase => HasItems && FitsCargo && CanAfford && WithinQuota && Reason.Length == 0;
        }

        private static PortMarketItem MaintenanceMarketItem(VoyageSupplyType type)
        {
            switch (type)
            {
                case VoyageSupplyType.Water: return PortMarketItem.Water;
                case VoyageSupplyType.Food: return PortMarketItem.Food;
                case VoyageSupplyType.Medicine: return PortMarketItem.Medicine;
                case VoyageSupplyType.Wood: return PortMarketItem.Wood;
                case VoyageSupplyType.Powder: return PortMarketItem.Powder;
                case VoyageSupplyType.Cannonballs: return PortMarketItem.Cannonballs;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private MaintenanceSupplyQuote QuoteMaintenanceSupplies(VoyageSupplyOrder order, DateTime now)
        {
            var quote = new MaintenanceSupplyQuote { ProjectedVolume = session.Cargo.UsedVolume };
            if (order == null) return quote;
            int level = PortInvestmentLevel(session.CurrentPort);
            decimal totalCost = 0m;
            double projectedVolume = session.Cargo.UsedVolume;
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
            {
                int amount = order[type];
                if (amount < 0 || amount > int.MaxValue - session.GetSupplyAmount(type))
                {
                    quote.Reason = "物资数量异常，请重新配置清单。";
                    quote.FitsCargo = false;
                    return quote;
                }
                PortMarketItem item = MaintenanceMarketItem(type);
                quote.HasItems |= amount > 0;
                totalCost += PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, amount, session.Cargo.Fame);
                projectedVolume += amount * (double)PortMarketCatalog.CargoVolume(item);
                if (amount > MarketRemaining(session.CurrentPort, item, now, level))
                {
                    quote.WithinQuota = false;
                    if (quote.Reason.Length == 0)
                        quote.Reason = CoreVoyageSession.SupplyName(type) + "本月库存或额度不足，下个游戏月首日补货。";
                }
            }
            quote.Cost = totalCost;
            quote.ProjectedVolume = (float)projectedVolume;
            quote.FitsCargo = CoreEconomyBalance.HasNonNegativeCargo(session.Cargo) &&
                projectedVolume <= session.CargoCapacityValue + .001d;
            quote.CanAfford = totalCost <= session.ProfileGold;
            if (session.Phase != CoreDemoPhase.Port) quote.Reason = "返回港口后才能采购。";
            else if (!quote.FitsCargo) quote.Reason = "舱容不足或货物异常，请先检查货舱。";
            else if (!quote.CanAfford) quote.Reason = "船长资金不足。";
            return quote;
        }

        private void ConfirmSupplyPurchase()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port) return;
            DateTime now = MarketNow;
            if (maintenanceDisplayedDate != MarketMonth(now))
            {
                portNotice = "新月行情已刷新，请确认整备采购的新报价后再次购买。";
                Render();
                return;
            }

            VoyageSupplyOrder order = CopySupplyOrder(pendingSupplyOrder);
            MaintenanceSupplyQuote quote = QuoteMaintenanceSupplies(order, now);
            if (!quote.CanPurchase)
            {
                portNotice = quote.HasItems ? quote.Reason : "请选择采购数量。";
                Render();
                return;
            }
            // Validate the complete quote before committing any cargo, money or
            // market ledger entry. All three purchase screens share these keys.
            if (!session.TryPurchaseMarketSupplyOrder(order, quote.Cost))
            {
                portNotice = "采购未完成，请检查金币和货舱；未扣款。";
                Render();
                return;
            }
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
            {
                int amount = order[type];
                if (amount <= 0) continue;
                PortMarketItem item = MaintenanceMarketItem(type);
                PlayerPrefs.SetInt(MarketPurchaseKey(session.CurrentPort, now, item),
                    MarketPurchased(session.CurrentPort, item, now) + amount);
            }
            pendingSupplyOrder.Clear();
            PlaySfx("trade_confirm", .5f);
            SaveProgress();
            portNotice = "采购完成：" + order.TotalUnits + " 单位物资已装船，支付 " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金币；已计入本月市场额度。";
            Render();
        }
    }
}
