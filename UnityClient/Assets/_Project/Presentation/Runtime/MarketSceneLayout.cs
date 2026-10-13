using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public enum MarketShopKind { Supplies, Medicine, Shipwright, Ordnance }

    /// <summary>Scene-space anchors and the exhaustive market-to-shop mapping.</summary>
    public static class MarketSceneLayout
    {
        public static readonly Vector2 DrawerSize = new Vector2(560f, 850f);
        private static readonly IReadOnlyList<PortMarketItem>[] ShopItems =
        {
            Array.AsReadOnly(new[] { PortMarketItem.Water, PortMarketItem.Food, PortMarketItem.Beer }),
            Array.AsReadOnly(new[] { PortMarketItem.Medicine }),
            Array.AsReadOnly(new[] { PortMarketItem.Wood, PortMarketItem.Iron, PortMarketItem.Cloth }),
            Array.AsReadOnly(new[] { PortMarketItem.Powder, PortMarketItem.Cannonballs }),
        };
        public static IReadOnlyList<PortMarketItem> ItemsForShop(MarketShopKind shop) => ShopItems[(int)shop];
        public static bool IsLeftShop(MarketShopKind shop) => shop == MarketShopKind.Supplies || shop == MarketShopKind.Medicine;
        public static Vector2 DrawerPosition(MarketShopKind shop) => new Vector2(IsLeftShop(shop) ? 650f : -650f, -20f);
        public static string ShopName(MarketShopKind shop)
        {
            switch (shop)
            {
                case MarketShopKind.Supplies: return "补给铺";
                case MarketShopKind.Medicine: return "药品铺";
                case MarketShopKind.Shipwright: return "船材铺";
                default: return "军需铺";
            }
        }
        public static MarketShopKind ShopForItem(PortMarketItem item)
        {
            switch (item)
            {
                case PortMarketItem.Water:
                case PortMarketItem.Food:
                case PortMarketItem.Beer: return MarketShopKind.Supplies;
                case PortMarketItem.Medicine: return MarketShopKind.Medicine;
                case PortMarketItem.Wood:
                case PortMarketItem.Iron:
                case PortMarketItem.Cloth: return MarketShopKind.Shipwright;
                default: return MarketShopKind.Ordnance;
            }
        }
        public static Vector2 HotspotPosition(MarketShopKind shop)
        {
            switch (shop)
            {
                case MarketShopKind.Supplies: return new Vector2(-735f, -95f);
                case MarketShopKind.Medicine: return new Vector2(-328f, -30f);
                case MarketShopKind.Shipwright: return new Vector2(328f, -30f);
                default: return new Vector2(735f, -95f);
            }
        }
        public static Vector2 HotspotSize(MarketShopKind shop) =>
            shop == MarketShopKind.Supplies || shop == MarketShopKind.Ordnance
                ? new Vector2(390f, 660f) : new Vector2(260f, 460f);
        public static Vector2 SignPosition(MarketShopKind shop) => SignPosition(shop, new Vector2(1920f, 1080f));
        public static Vector2 SignPosition(MarketShopKind shop, Vector2 sourceSize)
        {
            Vector2 uv;
            switch (shop)
            {
                case MarketShopKind.Supplies: uv = new Vector2(.13f, .31f); break;
                case MarketShopKind.Medicine: uv = new Vector2(.33f, .38f); break;
                case MarketShopKind.Shipwright: uv = new Vector2(.67f, .38f); break;
                default: uv = new Vector2(.87f, .31f); break;
            }
            return PhaseSurfaceLayout.SourceUvToContent(uv, sourceSize);
        }
        public static Vector2 HotspotPosition(MarketShopKind shop, Vector2 sourceSize)
        {
            bool front = shop == MarketShopKind.Supplies || shop == MarketShopKind.Ordnance;
            return SignPosition(shop, sourceSize) + new Vector2(0f, front ? -271f : -130f);
        }
    }
}
