using System;
using System.Collections.Generic;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Presentation
{
    public enum PortMarketItem
    {
        Water,
        Food,
        Beer,
        Medicine,
        Wood,
        Iron,
        Cloth,
        Powder,
        Cannonballs,
    }

    public readonly struct PortMarketDayState
    {
        public PortMarketDayState(decimal buyMultiplier, decimal sellMultiplier, int stock)
        {
            BuyMultiplier = buyMultiplier;
            SellMultiplier = sellMultiplier;
            Stock = Math.Max(0, stock);
        }

        public decimal BuyMultiplier { get; }
        public decimal SellMultiplier { get; }
        public int Stock { get; }
    }

    /// <summary>
    /// Deterministic port-to-port market rules. The same port, game month and item
    /// always produce the same monthly demand, while each port has its own
    /// purchase and sale specialties to make trading routes meaningful.
    /// </summary>
    public static class PortMarketCatalog
    {
        private static readonly IReadOnlyList<PortMarketItem> Items = Array.AsReadOnly(new[]
        {
            PortMarketItem.Water,
            PortMarketItem.Food,
            PortMarketItem.Beer,
            PortMarketItem.Medicine,
            PortMarketItem.Wood,
            PortMarketItem.Iron,
            PortMarketItem.Cloth,
            PortMarketItem.Powder,
            PortMarketItem.Cannonballs,
        });

        public static IReadOnlyList<PortMarketItem> AllItems => Items;

        public static string DayKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string MonthKey(DateTime date)
        {
            return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        public static string NextRefreshLabel(DateTime date)
        {
            if (date.Year == 9999 && date.Month == 12) return "已达历法上限";
            return new DateTime(date.Year, date.Month, 1).AddMonths(1)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string CanonicalPortName(string portName)
        {
            return NormalizePort(portName);
        }

        public static string Key(PortMarketItem item)
        {
            return item.ToString().ToLowerInvariant();
        }

        public static string Name(PortMarketItem item)
        {
            switch (item)
            {
                case PortMarketItem.Water: return "淡水";
                case PortMarketItem.Food: return "食物";
                case PortMarketItem.Beer: return "啤酒";
                case PortMarketItem.Medicine: return "药品";
                case PortMarketItem.Wood: return "木材";
                case PortMarketItem.Iron: return "铁矿";
                case PortMarketItem.Cloth: return "帆布";
                case PortMarketItem.Powder: return "火药";
                default: return "炮弹";
            }
        }

        public static decimal BaseBuyPrice(PortMarketItem item)
        {
            return CorePortEconomy.BuyBasePrice(Key(item));
        }

        public static decimal BaseSellPrice(PortMarketItem item)
        {
            return BaseBuyPrice(item) * CorePortEconomy.SaleRatio;
        }

        public static float CargoVolume(PortMarketItem item)
        {
            switch (item)
            {
                case PortMarketItem.Water: return 0.07f;
                case PortMarketItem.Food: return 0.09f;
                case PortMarketItem.Beer: return 0.08f;
                case PortMarketItem.Medicine: return 0.04f;
                case PortMarketItem.Wood: return 1f;
                case PortMarketItem.Iron: return 2f;
                case PortMarketItem.Cloth: return 1f;
                case PortMarketItem.Powder: return 0.5f;
                default: return 1f;
            }
        }

        public static int PurchaseStep(PortMarketItem item)
        {
            return item == PortMarketItem.Water || item == PortMarketItem.Food ? 100 : 1;
        }

        public static int SaleStep(PortMarketItem item)
        {
            return item == PortMarketItem.Water || item == PortMarketItem.Food ? 100 : 10;
        }

        public static int MaxPurchaseQuota(PortMarketItem item, int investmentLevel)
        {
            int level = Math.Max(0, Math.Min(PortInvestmentCatalog.MaxLevel, investmentLevel));
            int baseQuota;
            switch (item)
            {
                case PortMarketItem.Water:
                    baseQuota = 6000;
                    break;
                case PortMarketItem.Food:
                    baseQuota = 4000;
                    break;
                case PortMarketItem.Beer:
                    baseQuota = 240;
                    break;
                case PortMarketItem.Medicine:
                    baseQuota = 120;
                    break;
                default:
                    baseQuota = 200;
                    break;
            }

            // A monthly batch supports several normal trade circuits. Construction
            // expands the entire supply contract, with greater capacity at high tiers.
            int[] quarterMultipliers = { 4, 6, 9, 13, 18, 24 };
            return baseQuota * quarterMultipliers[level] / 4;
        }

        public static PortMarketDayState DayState(string portName, PortMarketItem item, DateTime date,
            int investmentLevel = 0)
        {
            string port = NormalizePort(portName);
            string month = MonthKey(date);
            Mulberry32Random random = new Mulberry32Random(
                SeedHash.Compute("PORT-MARKET-MONTHLY::" + month + "::" + port + "::" + Key(item)));
            decimal buy = CorePortEconomy.DailyMinimum + (decimal)random.NextDouble() * CorePortEconomy.DailyRange;
            decimal sell = CorePortEconomy.DailyMinimum + (decimal)random.NextDouble() * CorePortEconomy.DailyRange;
            int baseQuota = MaxPurchaseQuota(item, 0);
            int min = baseQuota * 9 / 10;
            int baseStock = min + (int)Math.Floor(random.NextDouble() * (baseQuota - min + 1));
            // Keep this month's original stock and purchased quantities intact.
            // Every upgrade adds its full quota increment to physical stock too.
            int stock = baseStock + MaxPurchaseQuota(item, investmentLevel) - baseQuota;
            return new PortMarketDayState(buy, sell, stock);
        }

        public static decimal BuyUnitPrice(string portName, PortMarketItem item, DateTime date)
        {
            PortMarketDayState day = DayState(portName, item, date);
            decimal raw = BaseBuyPrice(item) * CorePortEconomy.LocalPriceMultiplier(portName, Key(item)) * day.BuyMultiplier;
            return decimal.Round(raw, 4);
        }

        public static decimal SellUnitPrice(string portName, PortMarketItem item, DateTime date)
        {
            PortMarketDayState day = DayState(portName, item, date);
            decimal raw = BaseSellPrice(item) * CorePortEconomy.LocalPriceMultiplier(portName, Key(item)) * day.SellMultiplier;
            return decimal.Round(raw, 4);
        }

        public static decimal CalculateBuyCost(string portName, PortMarketItem item, DateTime date, int quantity)
        {
            if (quantity <= 0) return 0;
            return BuyUnitPrice(portName, item, date) * quantity;
        }

        public static decimal CalculateSellRevenue(string portName, PortMarketItem item, DateTime date, int quantity)
        {
            if (quantity <= 0) return 0;
            return SellUnitPrice(portName, item, date) * quantity;
        }

        public static string FormatPrice(decimal price)
        {
            return price.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public static float PortPurchaseMultiplier(string portName, PortMarketItem item)
        {
            return (float)CorePortEconomy.LocalPriceMultiplier(portName, Key(item));
        }

        public static float PortSaleMultiplier(string portName, PortMarketItem item)
        {
            return (float)CorePortEconomy.LocalPriceMultiplier(portName, Key(item));
        }

        private static string NormalizePort(string portName)
        {
            return (portName ?? string.Empty).Trim();
        }

    }
}
