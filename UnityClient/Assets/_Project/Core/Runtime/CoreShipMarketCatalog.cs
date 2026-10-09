using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Permanent regional shipyard stock; buying a hull adds no level gate.</summary>
    public static class CoreShipMarketCatalog
    {
        private sealed class Listing
        {
            internal Listing(int cost, int home, int artwork, string role, string specialty)
            { Cost = cost; Home = home; Artwork = artwork; Role = role; Specialty = specialty; }
            internal readonly int Cost, Home, Artwork;
            internal readonly string Role, Specialty;
        }

        // The first four indices are save identifiers and must never be reordered.
        private static readonly Listing[] listings =
        {
            new Listing(0, 0, 0, "近港启航 · 轻装小舱", "人少水粮省\n适合近港起步\n舱小甲薄\n专家与水手\n须有所取舍"),
            new Listing(800, 3, 1, "初阶商贸 · 实用宽舱", "造价较低\n扩充货舱\n适合早期贸易\n炮火有限"),
            new Listing(1800, 2, 2, "常规护航 · 均衡炮战", "炮火装甲中等\n适合护航海战\n亦可贸易\n舱量少于\n远洋商船"),
            new Listing(2200, 4, 3, "遗迹寻踪 · 快航省员", "航速较快\n船员需求较少\n适合远探\n装甲较轻\n不宜持续承伤"),
            new Listing(1300, 0, 1, "重物打捞 · 宽舱坚体", "厚船体宽货舱\n适合装运重物\n航速慢炮火弱\n不宜追击"),
            new Listing(1500, 1, 0, "高速轻运 · 小舱省员", "十人编制\n航速最快\n适合短途轻运\n货舱最小\n不宜硬拼重舰"),
            new Listing(1800, 1, 1, "大舱补给 · 省员运输", "少船员大货舱\n适合补给运输\n炮火有限\n宜避开硬仗"),
            new Listing(4800, 2, 2, "重炮攻坚 · 厚甲远战", "最强炮火厚甲\n货舱可装\n长航补给\n人多航速慢\n须备足水粮"),
            new Listing(2800, 3, 1, "大宗商贸 · 满载远行", "货舱最大\n适合成批运输\n航速最慢\n炮火有限"),
            new Listing(3400, 3, 2, "武装商贸 · 长航护送", "宽舱较强炮火\n适合远洋商贸\n船员需求多\n炮火与装甲\n不及重炮舰"),
            new Listing(4200, 4, 3, "高速远探 · 轻型炮舰", "航速快船员少\n具备护航火力\n装甲较轻\n宜速战\n随后继续探索"),
            new Listing(5200, 5, 1, "逐风商贸 · 高速宽舱", "高速大货舱\n适合长途贸易\n造价较高\n炮火与装甲\n不及专用战舰"),
        };

        private static readonly IReadOnlyList<int>[] stock =
        {
            Array.AsReadOnly(new[] { 0, 1, 4 }),
            Array.AsReadOnly(new[] { 0, 5, 6 }),
            Array.AsReadOnly(new[] { 1, 2, 7 }),
            Array.AsReadOnly(new[] { 1, 8, 9 }),
            Array.AsReadOnly(new[] { 3, 10 }),
            Array.AsReadOnly(new[] { 0, 11 }),
        };
        private static readonly IReadOnlyList<int> emptyStock = Array.AsReadOnly(Array.Empty<int>());

        /// <summary>Unknown hulls cannot become free purchases through a default price.</summary>
        public static int PurchaseCost(int shipIndex) => ValidShip(shipIndex) ? listings[shipIndex].Cost : int.MaxValue;
        public static string Role(int shipIndex) => ValidShip(shipIndex) ? listings[shipIndex].Role : string.Empty;
        public static string Specialty(int shipIndex) => ValidShip(shipIndex) ? listings[shipIndex].Specialty : string.Empty;
        public static int HomePortIndex(int shipIndex) => ValidShip(shipIndex) ? listings[shipIndex].Home : -1;
        public static int ArtworkIndex(int shipIndex) => ValidShip(shipIndex) ? listings[shipIndex].Artwork : 0;
        public static IReadOnlyList<int> ShipsForPort(int portIndex) =>
            portIndex >= 0 && portIndex < stock.Length ? stock[portIndex] : emptyStock;
        public static bool IsSoldAtPort(int portIndex, int shipIndex)
        {
            if (!ValidShip(shipIndex)) return false;
            foreach (int available in ShipsForPort(portIndex))
                if (available == shipIndex) return true;
            return false;
        }
        private static bool ValidShip(int shipIndex) => shipIndex >= 0 && shipIndex < listings.Length;
    }
}
