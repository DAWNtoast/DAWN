using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Regional prices for physical cargo. Producers buy and sell cheaply;
    /// importers buy and sell dearly. The spread pays for moving goods, never
    /// for immediately reselling them at the same port.
    /// </summary>
    public static class CorePortEconomy
    {
        public const decimal SaleRatio = 0.80m;
        public const decimal DailyMinimum = 0.95m;
        public const decimal DailyRange = 0.10m;

        public static decimal BuyBasePrice(string item)
        {
            switch (item)
            {
                case "water": return 0.04m;
                case "food": return 0.12m;
                case "beer": return 3m;
                case "medicine": return 8m;
                case "wood": return 5m;
                case "iron": return 10m;
                case "cloth": return 7m;
                case "powder": return 5m;
                case "cannonballs": return 8m;
                default: return 0m;
            }
        }

        public static decimal LocalPriceMultiplier(string portName, string item)
        {
            switch ((portName ?? string.Empty).Trim())
            {
                case "断锚港":
                    if (item == "wood") return 0.65m;
                    if (item == "beer") return 1.60m;
                    if (item == "medicine") return 1.55m;
                    break;
                case "白贝港":
                    if (item == "water" || item == "food" || item == "beer") return 0.65m;
                    if (item == "wood") return 1.60m;
                    if (item == "medicine") return 1.45m;
                    break;
                case "铜钟港":
                    if (item == "water" || item == "food") return 1.25m;
                    if (item == "wood" || item == "cannonballs") return 0.75m;
                    if (item == "iron") return 0.65m;
                    if (item == "cloth") return 1.50m;
                    break;
                case "盐冠港":
                    if (item == "water" || item == "cloth") return 1.60m;
                    if (item == "food") return 1.55m;
                    if (item == "beer") return 0.75m;
                    if (item == "powder" || item == "cannonballs") return 0.65m;
                    break;
                case "暮潮港":
                    if (item == "water" || item == "food") return 1.45m;
                    if (item == "beer" || item == "wood") return 1.60m;
                    if (item == "medicine") return 0.65m;
                    if (item == "iron") return 1.50m;
                    break;
                case "灰鸥港":
                    if (item == "medicine") return 0.85m;
                    if (item == "iron") return 1.60m;
                    if (item == "cloth") return 0.65m;
                    if (item == "powder" || item == "cannonballs") return 1.55m;
                    break;
            }
            return 1m;
        }

        public static string TradeSummary(string portName)
        {
            switch ((portName ?? string.Empty).Trim())
            {
                case "断锚港": return "特产：木材　·　需求：啤酒、药品";
                case "白贝港": return "特产：水粮、啤酒　·　需求：木材、药品";
                case "铜钟港": return "特产：铁矿、木材、炮弹　·　需求：帆布、水粮";
                case "盐冠港": return "特产：火药、炮弹、啤酒　·　需求：帆布、水粮";
                case "暮潮港": return "特产：药品　·　需求：木材、啤酒、铁矿";
                case "灰鸥港": return "特产：帆布、药品　·　需求：铁矿、火药、炮弹";
                default: return "跨港运货赚取差价；缩短航程可节省水粮。";
            }
        }

        public static decimal BaseSupplyUnitPrice(string portName, VoyageSupplyType type)
        {
            string key = type.ToString().ToLowerInvariant();
            return BuyBasePrice(key) * LocalPriceMultiplier(portName, key);
        }
    }
}
