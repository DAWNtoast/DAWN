using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreWarehouseItemDefinition
    {
        private readonly Func<VoyageCargo, int> read;
        private readonly Action<VoyageCargo, int> write;

        internal CoreWarehouseItemDefinition(string key, string name, string description, double volume, bool rare,
            Func<VoyageCargo, int> read, Action<VoyageCargo, int> write)
        {
            Key = key; Name = name; Description = description; UnitVolume = volume; IsRare = rare;
            this.read = read; this.write = write;
        }

        public string Key { get; }
        public string Name { get; }
        public string Description { get; }
        public double UnitVolume { get; }
        public bool IsRare { get; }
        internal int Read(VoyageCargo cargo) => cargo == null ? 0 : read(cargo);
        internal void Write(VoyageCargo cargo, int amount) => write(cargo, amount);
    }

    /// <summary>Physical warehouse cargo only. This catalog does not define market prices.</summary>
    public static class CoreWarehouseItemCatalog
    {
        public static IReadOnlyList<CoreWarehouseItemDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new CoreWarehouseItemDefinition("water", "淡水", "桶壁还带着岸上井水的凉意。离港越远，水手倒水的手便越稳。", .07d, false, c => c.Water, (c, n) => c.Water = n),
            new CoreWarehouseItemDefinition("food", "食物", "盐肉、硬饼与晒干的豆子。钟声一响，几只锡碗便把整片海的寂寞敲散。", .09d, false, c => c.Food, (c, n) => c.Food = n),
            new CoreWarehouseItemDefinition("beer", "啤酒", "木塞一松，麦香便漫过舱板。水手举杯时，故乡总在同一个方向。", .08d, false, c => c.Beer, (c, n) => c.Beer = n),
            new CoreWarehouseItemDefinition("medicine", "药品", "药草用蜡纸裹紧，针线收在干燥处。有人熬过长夜，靠的是这一点苦味。", .04d, false, c => c.Medicine, (c, n) => c.Medicine = n),
            new CoreWarehouseItemDefinition("wood", "木材", "锯口留着浅淡的松脂香。船匠敲一敲木纹，听它还能替船挡住多少浪。", 1d, false, c => c.Wood, (c, n) => c.Wood = n),
            new CoreWarehouseItemDefinition("iron", "铁矿", "矿石沉默而粗粝，红锈沾满搬运工的掌心。进了炉火，才有另一副骨头。", 2d, false, c => c.Iron, (c, n) => c.Iron = n),
            new CoreWarehouseItemDefinition("cloth", "布料", "紧卷的布匹藏着织机的余温。裁成帆，便替岸上的人尝一尝远方的风。", 1d, false, c => c.Cloth, (c, n) => c.Cloth = n),
            new CoreWarehouseItemDefinition("powder", "火药", "细黑的颗粒睡在密封桶里。炮手搬它时，总比搬金币更轻一些。", .5d, false, c => c.Powder, (c, n) => c.Powder = n),
            new CoreWarehouseItemDefinition("cannonballs", "炮弹", "铁球一颗颗排进舱底，随船身低声碰响。开火以前，雷声已藏在脚下。", 1d, false, c => c.Cannonballs, (c, n) => c.Cannonballs = n),
            new CoreWarehouseItemDefinition("gem", "宝石", "盐垢洗去，棱面便露出一簇冷光。商人掂它的分量，水手记得捞它的深度。", 5d, true, c => c.Gem, (c, n) => c.Gem = n),
            new CoreWarehouseItemDefinition("ancientmaterial", "古代材料", "旧工匠留下的材片，断口纹路细得像潮线。如今的炉匠也说不准它出自哪一炉。", 1.5d, true, c => c.AncientMaterial, (c, n) => c.AncientMaterial = n),
            new CoreWarehouseItemDefinition("relic", "稀有遗物", "边角被岁月磨圆，铭文只剩半行。它的主人早已无名，托住它的手却仍会迟疑。", 5d, true, c => c.Relic, (c, n) => c.Relic = n),
            new CoreWarehouseItemDefinition("blackpearl", "黑珍珠", "墨色珠面裹着一圈幽光。采珠人不肯说它来自哪片水域，只把绳结又系紧了一重。", 4d, true, c => c.BlackPearl, (c, n) => c.BlackPearl = n),
            new CoreWarehouseItemDefinition("ancientcompass", "古代罗盘", "铜盖磨亮，盘沿刻着失传的方位。船长把它揣入怀中，等旧海作证。", 3d, true, c => c.AncientCompass, (c, n) => c.AncientCompass = n),
            new CoreWarehouseItemDefinition("lantern", "航海灯", "灯罩上结着薄盐，提环磨出温润的铜色。夜班水手接过它，也接过尚未走完的黑暗。", 1d, true, c => c.Lantern, (c, n) => c.Lantern = n),
        });

        public static IReadOnlyList<CoreWarehouseItemDefinition> BasicItems { get; } = Select(false);
        public static IReadOnlyList<CoreWarehouseItemDefinition> RareItems { get; } = Select(true);

        public static CoreWarehouseItemDefinition Find(string key)
        {
            string normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
            foreach (CoreWarehouseItemDefinition item in All)
                if (item.Key == normalized) return item;
            return null;
        }

        private static IReadOnlyList<CoreWarehouseItemDefinition> Select(bool rare)
        {
            var items = new List<CoreWarehouseItemDefinition>();
            foreach (CoreWarehouseItemDefinition item in All)
                if (item.IsRare == rare) items.Add(item);
            return items.AsReadOnly();
        }
    }
}
