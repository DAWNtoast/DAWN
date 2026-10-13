using System;
using System.Collections.Generic;
using System.Globalization;
using ShatteredSail.Content;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public enum ComponentSlot
    {
        Bow,
        Sail,
        Cannon,
        Plating,
        Instrument,
    }

    public enum ComponentRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// Environmental tags used by component tradeoffs.  A component can be
    /// excellent in one situation while remaining a sidegrade elsewhere.
    /// </summary>
    public enum ComponentTimeBand
    {
        Any,
        Dawn,
        Day,
        Dusk,
        Night,
    }

    public enum ComponentRouteType
    {
        Any,
        Coastal,
        Strait,
        Reef,
        Trade,
        OpenSea,
        DeepSea,
    }

    public enum ComponentWeatherType
    {
        Any,
        Calm,
        Wind,
        Rain,
        Fog,
        Storm,
    }

    /// <summary>
    /// Context supplied by the voyage layer when it wants to evaluate an
    /// installed component.  Keeping this small makes it usable by previews,
    /// the shop and a live voyage without coupling the catalog to a scene.
    /// </summary>
    public struct ComponentVoyageContext
    {
        public ComponentVoyageContext(ComponentTimeBand time, ComponentRouteType route, ComponentWeatherType weather)
        {
            Time = time;
            Route = route;
            Weather = weather;
        }

        public ComponentTimeBand Time { get; }
        public ComponentRouteType Route { get; }
        public ComponentWeatherType Weather { get; }
    }

    public struct ComponentContextEffect
    {
        public int HullBonus { get; set; }
        public int ArmorBonus { get; set; }
        public int FirepowerBonus { get; set; }
        public float SpeedBonus { get; set; }
        public float VisionBonus { get; set; }
        public float RationMultiplier { get; set; }
        public bool IsActive { get; set; }

        public static ComponentContextEffect None => new ComponentContextEffect { RationMultiplier = 1f };
    }

    /// <summary>
    /// Presentation data mirrored from component_config.csv. Gameplay IDs remain
    /// stable while each equipment ID has its own independent visual identity.
    /// </summary>
    public sealed class ComponentArtEntry
    {
        public ComponentArtEntry(
            string id,
            string name,
            ComponentSlot slot,
            ComponentRarity rarity,
            int installWood,
            int price,
            string effect,
            string specialization,
            string description,
            string artSourceId,
            string storyId,
            int hullBonus,
            int armorBonus,
            int firepowerBonus,
            float speedBonus,
            float visionBonus,
            ComponentTimeBand bestTime = ComponentTimeBand.Any,
            ComponentRouteType bestRoute = ComponentRouteType.Any,
            ComponentWeatherType bestWeather = ComponentWeatherType.Any,
            string environmentalEffect = "")
        {
            Id = id;
            Name = name;
            Slot = slot;
            Rarity = rarity;
            InstallWood = installWood;
            Price = price;
            Effect = effect;
            Specialization = specialization;
            Description = description;
            ArtSourceId = artSourceId;
            StoryId = storyId;
            HullBonus = hullBonus;
            ArmorBonus = armorBonus;
            FirepowerBonus = firepowerBonus;
            SpeedBonus = speedBonus;
            VisionBonus = visionBonus;
            BestTime = bestTime;
            BestRoute = bestRoute;
            BestWeather = bestWeather;
            EnvironmentalEffect = environmentalEffect ?? string.Empty;
            foreach (CrewStoryDefinition story in CrewNarrativeCatalog.Stories)
            {
                if (story.Id != storyId) continue;
                StoryChapter = story.Chapter;
                StoryTitle = story.ChapterLabel;
                StoryBody = story.Body;
                break;
            }
        }

        public string Id { get; }
        public string Name { get; }
        public ComponentSlot Slot { get; }
        public ComponentRarity Rarity { get; }
        public int InstallWood { get; }
        public int Price { get; }
        public string Effect { get; }
        public string Specialization { get; }
        public string Description { get; }
        public string ArtSourceId { get; }
        public string StoryId { get; }
        public int StoryChapter { get; }
        public string StoryTitle { get; } = string.Empty;
        public string StoryBody { get; } = string.Empty;
        /// <summary>Whether the linked chapter is signed testimony or a lead.</summary>
        public string LoreRelationLabel => SeventhPortLoreCatalog.RelationLabelForStory(StoryId);
        public int HullBonus { get; }
        public int ArmorBonus { get; }
        public int FirepowerBonus { get; }
        public float SpeedBonus { get; }
        public float VisionBonus { get; }
        public ComponentTimeBand BestTime { get; internal set; }
        public ComponentRouteType BestRoute { get; internal set; }
        public ComponentWeatherType BestWeather { get; internal set; }
        public string EnvironmentalEffect { get; internal set; }
        private Texture2D texture;
        private Texture2D shipLayer;
        private bool textureResolved, shipLayerResolved;
        private bool textureOverridden, shipLayerOverridden;
        private Func<string, Texture2D> artLoader;

        public Texture2D Texture
        {
            get
            {
                if (!textureResolved)
                {
                    texture = artLoader?.Invoke("ComponentArt/" + ArtSourceId);
                    textureResolved = true;
                }
                return texture;
            }
            internal set { texture = value; textureResolved = textureOverridden = true; }
        }

        public Texture2D ShipLayer
        {
            get
            {
                if (!shipLayerResolved)
                {
                    shipLayer = artLoader?.Invoke("ModularShip/Layers/" + ArtSourceId);
                    shipLayerResolved = true;
                }
                return shipLayer;
            }
            internal set { shipLayer = value; shipLayerResolved = shipLayerOverridden = true; }
        }

        internal void ConfigureArtLoader(Func<string, Texture2D> loader)
        {
            artLoader = loader;
        }

        internal void ReleaseLoadedTextures()
        {
            if (!textureOverridden)
            {
                texture = null;
                textureResolved = false;
            }
            if (!shipLayerOverridden)
            {
                shipLayer = null;
                shipLayerResolved = false;
            }
        }

        public bool Matches(ComponentVoyageContext context)
        {
            return (BestTime == ComponentTimeBand.Any || BestTime == context.Time) &&
                (BestRoute == ComponentRouteType.Any || BestRoute == context.Route) &&
                (BestWeather == ComponentWeatherType.Any || BestWeather == context.Weather);
        }

        /// <summary>
        /// Returns a modest bonus for each matching condition.  The base stats
        /// remain useful everywhere; the conditional effect rewards a deliberate
        /// fit without making an off-season component unusable.
        /// </summary>
        public ComponentContextEffect Evaluate(ComponentVoyageContext context)
        {
            int matches = 0;
            if (BestTime != ComponentTimeBand.Any && BestTime == context.Time) matches++;
            if (BestRoute != ComponentRouteType.Any && BestRoute == context.Route) matches++;
            if (BestWeather != ComponentWeatherType.Any && BestWeather == context.Weather) matches++;
            if (matches == 0) return ComponentContextEffect.None;
            return new ComponentContextEffect
            {
                HullBonus = matches >= 3 ? 1 : 0,
                ArmorBonus = BestWeather == ComponentWeatherType.Storm && context.Weather == BestWeather ? matches : 0,
                FirepowerBonus = BestRoute == ComponentRouteType.Reef && context.Route == BestRoute ? matches : 0,
                SpeedBonus = 0.15f * matches,
                VisionBonus = (BestTime == ComponentTimeBand.Night && context.Time == BestTime ? 5f : 0f) +
                    (BestWeather == ComponentWeatherType.Fog && context.Weather == BestWeather ? 5f : 0f),
                RationMultiplier = BestRoute == ComponentRouteType.OpenSea && context.Route == BestRoute ? 0.96f : 1f,
                IsActive = true,
            };
        }
    }

    /// <summary>
    /// Loads the complete five-slot, five-rarity, five-variant catalog from a
    /// Resources CSV and resolves reusable icon and modular-ship art on demand.
    /// </summary>
    public sealed class ComponentArtCatalog
    {
        public const int EntriesPerRarityPerSlot = 5;
        public const int RarityCount = 5;
        public const int EntriesPerSlot = EntriesPerRarityPerSlot * RarityCount;
        public const int SlotCount = 5;
        public const int ExpectedEntryCount = EntriesPerSlot * SlotCount;
        public const int AdditionalEntriesPerSlot = RarityCount;
        public const int ExpandedEntryCount = ExpectedEntryCount + AdditionalEntriesPerSlot * SlotCount;
        public const string CatalogResourcePath = "Data/component_catalog_v2";

        private readonly List<ComponentArtEntry> entries;
        private readonly List<ComponentArtEntry> expandedEntries;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Func<string, Texture2D> resourceLoader;

        public ComponentArtCatalog() : this(LoadTexture)
        {
        }

        private ComponentArtCatalog(Func<string, Texture2D> loader)
        {
            resourceLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            entries = BuildEntries();
            expandedEntries = BuildExpandedEntries(entries);
            Func<string, Texture2D> lazyLoader = LoadCachedTexture;
            for (int index = 0; index < expandedEntries.Count; index += 1)
            {
                ComponentArtEntry entry = expandedEntries[index];
                entry.ConfigureArtLoader(lazyLoader);
            }
        }

        private Texture2D LoadCachedTexture(string path)
        {
            if (!textures.TryGetValue(path, out Texture2D texture))
            {
                texture = resourceLoader(path);
                textures.Add(path, texture);
            }
            return texture;
        }

        public IReadOnlyList<ComponentArtEntry> Entries => entries;
        /// <summary>
        /// Full market catalogue.  Entries retains the original 125-item order
        /// so saved loadouts and existing previews remain stable; new stock uses
        /// this expanded view.
        /// </summary>
        public IReadOnlyList<ComponentArtEntry> ExpandedEntries => expandedEntries;
        public Texture2D BaseShipTexture => LoadCachedTexture("ModularShip/light_sloop_base");

        /// <summary>Drop borrowed image references without changing metadata or entry overrides.</summary>
        public void ReleaseLoadedTextures()
        {
            foreach (ComponentArtEntry entry in expandedEntries) entry.ReleaseLoadedTextures();
            textures.Clear();
        }

        public bool IsAvailable
        {
            get
            {
                if (entries.Count != ExpectedEntryCount)
                {
                    return false;
                }

                int[,] distribution = new int[SlotCount, RarityCount];
                for (int index = 0; index < entries.Count; index += 1)
                {
                    ComponentArtEntry entry = entries[index];
                    if (entry.Texture == null || entry.ShipLayer == null ||
                        string.IsNullOrWhiteSpace(entry.Id) ||
                        string.IsNullOrWhiteSpace(entry.Name) ||
                        string.IsNullOrWhiteSpace(entry.Effect) ||
                        string.IsNullOrWhiteSpace(entry.Specialization) ||
                        string.IsNullOrWhiteSpace(entry.Description) ||
                        string.IsNullOrWhiteSpace(entry.StoryBody) ||
                        string.IsNullOrWhiteSpace(entry.ArtSourceId))
                    {
                        return false;
                    }

                    distribution[(int)entry.Slot, (int)entry.Rarity] += 1;
                }

                for (int slot = 0; slot < SlotCount; slot += 1)
                {
                    for (int rarity = 0; rarity < RarityCount; rarity += 1)
                    {
                        if (distribution[slot, rarity] != EntriesPerRarityPerSlot)
                        {
                            return false;
                        }
                    }
                }

                return BaseShipTexture != null;
            }
        }

        public bool ExpandedIsAvailable
        {
            get
            {
                if (expandedEntries == null || expandedEntries.Count != ExpandedEntryCount || BaseShipTexture == null)
                    return false;
                for (int index = 0; index < expandedEntries.Count; index += 1)
                {
                    ComponentArtEntry entry = expandedEntries[index];
                    if (entry.Texture == null || entry.ShipLayer == null ||
                        string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) ||
                        string.IsNullOrWhiteSpace(entry.Description) || string.IsNullOrWhiteSpace(entry.EnvironmentalEffect))
                        return false;
                }
                return true;
            }
        }

        public ComponentArtEntry Get(int index)
        {
            return entries[Mathf.Clamp(index, 0, entries.Count - 1)];
        }

        public int IndexOf(ComponentArtEntry entry)
        {
            return entries.IndexOf(entry);
        }

        public ComponentArtEntry GetById(string id)
        {
            for (int index = 0; index < entries.Count; index += 1)
            {
                if (string.Equals(entries[index].Id, id, StringComparison.Ordinal))
                {
                    return entries[index];
                }
            }

            for (int index = 0; index < expandedEntries.Count; index += 1)
            {
                if (string.Equals(expandedEntries[index].Id, id, StringComparison.Ordinal))
                    return expandedEntries[index];
            }

            return null;
        }

        public static string SlotName(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Bow: return "船首";
                case ComponentSlot.Sail: return "船帆";
                case ComponentSlot.Cannon: return "火炮";
                case ComponentSlot.Plating: return "船壳";
                default: return "航海仪";
            }
        }

        public static string RarityName(ComponentRarity rarity)
        {
            switch (rarity)
            {
                case ComponentRarity.Common: return "普通";
                case ComponentRarity.Uncommon: return "精良";
                case ComponentRarity.Rare: return "稀有";
                case ComponentRarity.Epic: return "史诗";
                default: return "传说";
            }
        }

        public static string EnvironmentSummary(ComponentArtEntry entry)
        {
            if (entry == null) return string.Empty;
            return "适航 " + TimeName(entry.BestTime) + " · " + RouteName(entry.BestRoute) + " · " + WeatherName(entry.BestWeather) +
                "\n" + (string.IsNullOrWhiteSpace(entry.EnvironmentalEffect) ? "条件效果：无" : entry.EnvironmentalEffect.Trim());
        }

        public static Color RarityColor(ComponentRarity rarity)
        {
            switch (rarity)
            {
                case ComponentRarity.Common: return Html("#B8C0C8");
                case ComponentRarity.Uncommon: return Html("#48D66B");
                case ComponentRarity.Rare: return Html("#3B8BFF");
                case ComponentRarity.Epic: return Html("#B05CFF");
                default: return Html("#FFB52E");
            }
        }

        private static Color Html(string value)
        {
            Color color;
            ColorUtility.TryParseHtmlString(value, out color);
            return color;
        }

        private static Texture2D LoadTexture(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                // Component illustrations are painted, high-resolution assets rather
                // than pixel art.  The old Point/Trilinear split made legacy parts
                // shimmer beside the newer market sidegrades when cards were scaled.
                // Use one sampling policy for both the icon and ship-layer copy so
                // every part keeps the same soft painted edge at showroom and voyage
                // sizes.  Clamp prevents transparent atlas borders from bleeding in.
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 2;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            return texture;
        }

        private static List<ComponentArtEntry> BuildEntries()
        {
            TextAsset catalog = Resources.Load<TextAsset>(CatalogResourcePath);
            List<ComponentArtEntry> result = new List<ComponentArtEntry>(ExpectedEntryCount);
            if (catalog == null)
            {
                Debug.LogError("Missing component catalog Resources/" + CatalogResourcePath + ".csv.");
                return result;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = catalog.text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex += 1)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] cells = line.Split(',');
                if (cells.Length != 20)
                {
                    Debug.LogError("Invalid component catalog row " + (lineIndex + 1) + ": expected 20 columns but found " + cells.Length + ".");
                    continue;
                }

                string id = cells[0].Trim();
                if (string.IsNullOrWhiteSpace(id) ||
                    !ids.Add(id) ||
                    string.IsNullOrWhiteSpace(cells[1]) ||
                    string.IsNullOrWhiteSpace(cells[15]) ||
                    string.IsNullOrWhiteSpace(cells[16]) ||
                    string.IsNullOrWhiteSpace(cells[17]) ||
                    string.IsNullOrWhiteSpace(cells[18]) ||
                    string.IsNullOrWhiteSpace(cells[19]) ||
                    !Enum.TryParse(cells[2].Trim(), true, out ComponentSlot slot) ||
                    !Enum.TryParse(cells[3].Trim(), true, out ComponentRarity rarity) ||
                    !Enum.IsDefined(typeof(ComponentSlot), slot) ||
                    !Enum.IsDefined(typeof(ComponentRarity), rarity) ||
                    !TryPositiveInt(cells[4], out int installWood) ||
                    !TryPositiveInt(cells[13], out int price) ||
                    !int.TryParse(cells[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hullBonus) ||
                    !int.TryParse(cells[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int armorBonus) ||
                    !int.TryParse(cells[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int firepowerBonus) ||
                    !TryFiniteFloat(cells[8], out float speedBonus) ||
                    !TryFiniteFloat(cells[9], out float visionBonus))
                {
                    Debug.LogError("Invalid component catalog values at row " + (lineIndex + 1) + ".");
                    continue;
                }

                result.Add(new ComponentArtEntry(
                    id,
                    cells[1].Trim(),
                    slot,
                    rarity,
                    installWood,
                    price,
                    cells[16].Trim(),
                    cells[17].Trim(),
                    SeventhPortLoreCatalog.DescribeComponent(id, cells[15].Trim()),
                    cells[18].Trim(),
                    cells[19].Trim(),
                    hullBonus,
                    armorBonus,
                    firepowerBonus,
                    speedBonus,
                    visionBonus));
            }

            ApplyDefaultEnvironmentalProfiles(result);
            if (result.Count != ExpectedEntryCount)
            {
                Debug.LogError("Component catalog contains " + result.Count + " entries; expected " + ExpectedEntryCount + ".");
            }

            return result;
        }

        private static void ApplyDefaultEnvironmentalProfiles(List<ComponentArtEntry> result)
        {
            // Existing rows receive a deterministic profile without changing
            // their authored names, prices, IDs or starter ordering.
            for (int index = 0; index < result.Count; index += 1)
            {
                ComponentArtEntry entry = result[index];
                entry.BestTime = (ComponentTimeBand)((index + (int)entry.Slot) % 5);
                entry.BestRoute = (ComponentRouteType)((index * 2 + (int)entry.Slot) % 7);
                entry.BestWeather = (ComponentWeatherType)((index + (int)entry.Rarity) % 6);
                entry.EnvironmentalEffect = "匹配适航条件时，每项额外获得航速 +0.15；夜航或浓雾再获得视野 +5。";
            }
        }

        private static List<ComponentArtEntry> BuildExpandedEntries(IReadOnlyList<ComponentArtEntry> legacy)
        {
            List<ComponentArtEntry> result = new List<ComponentArtEntry>(ExpandedEntryCount);
            result.AddRange(legacy);

            // One additional authored sidegrade for every slot and rarity. The
            // entries keep independent art and stable save IDs.
            result.Add(Extra("bow_mistglass_ram", "雾玻璃破浪角", ComponentSlot.Bow, ComponentRarity.Common, 6, 5, 0, 0, 0.1f, 8f, 300, "船体 +5　航速 +0.1　视野 +8", "黎明礁区破雾", "bow_mistglass_ram", "seventh_berth_01", ComponentTimeBand.Dawn, ComponentRouteType.Reef, ComponentWeatherType.Fog, "角脊嵌着一线雾玻璃，初晴时才看得出旧裂纹。"));
            result.Add(Extra("bow_tidehook_spur", "潮钩导流首", ComponentSlot.Bow, ComponentRarity.Uncommon, 8, 7, 0, 1, 0.2f, 0f, 680, "船体 +7　火力 +1　航速 +0.2", "狭潮接舷", "bow_tidehook_spur", "seventh_berth_09", ComponentTimeBand.Dusk, ComponentRouteType.Strait, ComponentWeatherType.Rain, "弯钩下磨得最亮的一道槽，是接舷索长年留下的。"));
            result.Add(Extra("bow_sunwake_crest", "日醒狮首", ComponentSlot.Bow, ComponentRarity.Rare, 10, 12, 1, 1, 0.3f, 10f, 1380, "船体 +12　装甲 +1　火力 +1　航速 +0.3　视野 +10", "晴昼正面压制", "bow_sunwake_crest", "seventh_berth_05", ComponentTimeBand.Day, ComponentRouteType.OpenSea, ComponentWeatherType.Calm, "狮鬃镀金已经褪去，迎光的额面仍被水手擦得发亮。"));
            result.Add(Extra("bow_deepcurrent_fang", "深流鲸牙", ComponentSlot.Bow, ComponentRarity.Epic, 14, 17, 2, 0, -0.2f, 26f, 2780, "船体 +17　装甲 +2　航速 -0.2　视野 +26", "深海逆流", "bow_deepcurrent_fang", "seventh_berth_07", ComponentTimeBand.Night, ComponentRouteType.DeepSea, ComponentWeatherType.Fog, "牙根留着深海的灰色，一道道水痕沿骨缝爬向尖端。"));
            result.Add(Extra("bow_freewake_lance", "自由尾流长枪", ComponentSlot.Bow, ComponentRarity.Legendary, 18, 25, 2, 4, 0.2f, 34f, 5900, "船体 +25　装甲 +2　火力 +4　航速 +0.2　视野 +34", "风暴破阵", "bow_freewake_lance", "seventh_berth_14", ComponentTimeBand.Dusk, ComponentRouteType.OpenSea, ComponentWeatherType.Storm, "长枪的焊缝粗糙，掌舵人却舍不得换掉这根旧船首。"));

            result.Add(Extra("sail_dawnkite", "曙光风筝帆", ComponentSlot.Sail, ComponentRarity.Common, 3, 0, 0, 0, 0.8f, 8f, 300, "航速 +0.8　视野 +8", "晨雾轻航", "sail_dawnkite", "seventh_berth_02", ComponentTimeBand.Dawn, ComponentRouteType.Coastal, ComponentWeatherType.Fog, "轻帆展开像一只旧纸鸢，补线沿着受风的弧度走。"));
            result.Add(Extra("sail_strait_lugger", "海峡斜桁帆", ComponentSlot.Sail, ComponentRarity.Uncommon, 4, 0, 0, 1, 1.3f, 16f, 680, "火力 +1　航速 +1.3　视野 +16", "窄道顺风", "sail_strait_lugger", "seventh_berth_06", ComponentTimeBand.Day, ComponentRouteType.Strait, ComponentWeatherType.Wind, "斜桁两端磨着相同的索痕，记下无数次狭水转舵。"));
            result.Add(Extra("sail_evening_silk", "暮色商丝帆", ComponentSlot.Sail, ComponentRarity.Rare, 6, 1, 0, 0, 1.7f, 35f, 1380, "船体 +1　航速 +1.7　视野 +35", "黄昏商路", "sail_evening_silk", "seventh_berth_14", ComponentTimeBand.Dusk, ComponentRouteType.Trade, ComponentWeatherType.Calm, "暮光落上商丝，帆脚藏着的细密补针便显了出来。"));
            result.Add(Extra("sail_fogshroud_wing", "雾幕隐翼帆", ComponentSlot.Sail, ComponentRarity.Epic, 8, 0, 0, 0, 2.1f, 72f, 2780, "航速 +2.1　视野 +72", "雾海潜行", "sail_fogshroud_wing", "seventh_berth_12", ComponentTimeBand.Night, ComponentRouteType.DeepSea, ComponentWeatherType.Fog, "薄帆悬在桁下，湿雾聚成小滴，沿暗色缝线落下。"));
            result.Add(Extra("sail_thunderreef_wing", "雷礁九翼帆", ComponentSlot.Sail, ComponentRarity.Legendary, 11, 1, 0, 2, 3.0f, 64f, 5900, "船体 +1　火力 +2　航速 +3　视野 +64", "雷暴越礁", "sail_thunderreef_wing", "seventh_berth_05", ComponentTimeBand.Day, ComponentRouteType.Reef, ComponentWeatherType.Storm, "九翼收起时叠如礁石，撑开后，旧绳结一齐绷紧。"));

            result.Add(Extra("cannon_fogbell_saker", "雾钟短炮", ComponentSlot.Cannon, ComponentRarity.Common, 4, 0, 0, 2, 0f, 6f, 300, "火力 +2　视野 +6", "浓雾近防", "cannon_fogbell_saker", "seventh_berth_03", ComponentTimeBand.Night, ComponentRouteType.Strait, ComponentWeatherType.Fog, "短炮的耳轴留着钟匠的锉痕，炮手将它擦得最勤。"));
            result.Add(Extra("cannon_crosswind_pivot", "横风转轴炮", ComponentSlot.Cannon, ComponentRarity.Uncommon, 6, 0, 0, 4, 0.2f, 0f, 680, "火力 +4　航速 +0.2", "侧风齐射", "cannon_crosswind_pivot", "seventh_berth_14", ComponentTimeBand.Day, ComponentRouteType.OpenSea, ComponentWeatherType.Wind, "转轴上的油槽已被磨深，仍留着上一任炮长的刻线。"));
            result.Add(Extra("cannon_tradeguard_battery", "商路护航炮组", ComponentSlot.Cannon, ComponentRarity.Rare, 10, 0, 1, 6, 0f, 12f, 1380, "装甲 +1　火力 +6　视野 +12", "商路拦截", "cannon_tradeguard_battery", "seventh_berth_04", ComponentTimeBand.Dusk, ComponentRouteType.Trade, ComponentWeatherType.Calm, "炮座背面尽是货箱磨痕，这组炮一直与商船同路。"));
            result.Add(Extra("cannon_deepstar_lance", "深星穿潮炮", ComponentSlot.Cannon, ComponentRarity.Epic, 14, 0, 0, 10, -0.2f, 28f, 2780, "火力 +10　航速 -0.2　视野 +28", "深海重击", "cannon_deepstar_lance", "seventh_berth_12", ComponentTimeBand.Night, ComponentRouteType.DeepSea, ComponentWeatherType.Rain, "细长炮管涂得黝黑，只有膛口留一圈星子般的亮铜。"));
            result.Add(Extra("cannon_stormcrown_broadsides", "风暴冠列炮", ComponentSlot.Cannon, ComponentRarity.Legendary, 16, 0, 1, 15, -0.4f, 24f, 5900, "装甲 +1　火力 +15　航速 -0.4　视野 +24", "风暴决战", "cannon_stormcrown_broadsides", "seventh_berth_15", ComponentTimeBand.Dusk, ComponentRouteType.OpenSea, ComponentWeatherType.Storm, "列炮的铜箍颜色各异，是历次修补没有磨去的旧痕。"));

            result.Add(Extra("plating_saltfoam_skin", "盐沫浮壳", ComponentSlot.Plating, ComponentRarity.Common, 6, 7, 1, 0, 0.1f, 4f, 300, "船体 +7　装甲 +1　航速 +0.1　视野 +4", "沿岸抗浪", "plating_saltfoam_skin", "seventh_berth_08", ComponentTimeBand.Day, ComponentRouteType.Coastal, ComponentWeatherType.Rain, "浮壳接缝泛着白盐，船匠每次检查都先用拇指抹过。"));
            result.Add(Extra("plating_strait_laminate", "海峡叠板", ComponentSlot.Plating, ComponentRarity.Uncommon, 9, 11, 2, 0, -0.2f, 0f, 680, "船体 +11　装甲 +2　航速 -0.2", "窄道抗撞", "plating_strait_laminate", "seventh_berth_07", ComponentTimeBand.Dawn, ComponentRouteType.Strait, ComponentWeatherType.Wind, "层板交错压合，外缘的碰痕一层叠着一层。"));
            result.Add(Extra("plating_trade_oathwall", "商盟誓墙", ComponentSlot.Plating, ComponentRarity.Rare, 12, 16, 3, 0, -0.3f, 9f, 1380, "船体 +16　装甲 +3　航速 -0.3　视野 +9", "商船护航", "plating_trade_oathwall", "seventh_berth_14", ComponentTimeBand.Dusk, ComponentRouteType.Trade, ComponentWeatherType.Calm, "舷墙背后留着商队的粉笔号，修补时从不刨掉。"));
            result.Add(Extra("plating_fogiron_carapace", "雾铁甲壳", ComponentSlot.Plating, ComponentRarity.Epic, 16, 23, 5, 0, -0.8f, 14f, 2780, "船体 +23　装甲 +5　航速 -0.8　视野 +14", "雾海深潜", "plating_fogiron_carapace", "seventh_berth_13", ComponentTimeBand.Night, ComponentRouteType.DeepSea, ComponentWeatherType.Fog, "雾铁表面不见光泽，补钉却能在手心摸出清楚的排列。"));
            result.Add(Extra("plating_stormark_bulwark", "风暴方舟壁", ComponentSlot.Plating, ComponentRarity.Legendary, 22, 35, 7, 0, -1.1f, 18f, 5900, "船体 +35　装甲 +7　航速 -1.1　视野 +18", "雷暴生存", "plating_stormark_bulwark", "seventh_berth_09", ComponentTimeBand.Day, ComponentRouteType.OpenSea, ComponentWeatherType.Storm, "厚壁上嵌着旧船木，刮去表漆还能辨认原先的木纹。"));

            result.Add(Extra("instrument_dawnseeker", "曙光寻针", ComponentSlot.Instrument, ComponentRarity.Common, 1, 0, 0, 0, 0.1f, 26f, 300, "航速 +0.1　视野 +26", "晨曦定位", "instrument_dawnseeker", "seventh_berth_03", ComponentTimeBand.Dawn, ComponentRouteType.Coastal, ComponentWeatherType.Calm, "细针悬在浅铜盘中，盘盖内侧磨着常被拇指按住的圆痕。"));
            result.Add(Extra("instrument_strait_echo", "海峡回声盘", ComponentSlot.Instrument, ComponentRarity.Uncommon, 3, 0, 0, 0, 0.2f, 48f, 680, "航速 +0.2　视野 +48", "窄道测深", "instrument_strait_echo", "seventh_berth_03", ComponentTimeBand.Day, ComponentRouteType.Strait, ComponentWeatherType.Rain, "盘沿布满细小刻度，记录员的指甲常停在最浅的那道。"));
            result.Add(Extra("instrument_tradeledger_oracle", "商路账盘", ComponentSlot.Instrument, ComponentRarity.Rare, 4, 0, 0, 1, 0.4f, 66f, 1380, "火力 +1　航速 +0.4　视野 +66", "商路预判", "instrument_tradeledger_oracle", "seventh_berth_04", ComponentTimeBand.Dusk, ComponentRouteType.Trade, ComponentWeatherType.Wind, "账盘旁留着墨渍和算筹凹痕，海盐已吃进木座。"));
            result.Add(Extra("instrument_reefstar_astrolabe", "礁星潮盘", ComponentSlot.Instrument, ComponentRarity.Epic, 5, 1, 0, 0, 0.6f, 96f, 2780, "船体 +1　航速 +0.6　视野 +96", "礁海观星", "instrument_reefstar_astrolabe", "seventh_berth_06", ComponentTimeBand.Night, ComponentRouteType.Reef, ComponentWeatherType.Fog, "星环内侧刻着窄小的潮纹，需凑近舱灯才能看清。"));
            result.Add(Extra("instrument_stormoracle", "风暴谕盘", ComponentSlot.Instrument, ComponentRarity.Legendary, 8, 2, 0, 1, 0.8f, 128f, 5900, "船体 +2　火力 +1　航速 +0.8　视野 +128", "雷暴预警", "instrument_stormoracle", "seventh_berth_15", ComponentTimeBand.Dawn, ComponentRouteType.OpenSea, ComponentWeatherType.Storm, "盘罩留着被沙雨打过的细点，谕盘的刻线仍然清楚。"));

            return result;
        }

        private static ComponentArtEntry Extra(
            string id, string name, ComponentSlot slot, ComponentRarity rarity,
            int installWood, int hull, int armor,
            int firepower, float speed, float vision, int price, string effect,
            string specialization, string artSourceId, string storyId,
            ComponentTimeBand bestTime, ComponentRouteType bestRoute, ComponentWeatherType bestWeather, string narrative)
        {
            string description = narrative + "它在" + TimeName(bestTime) + "、" +
                RouteName(bestRoute) + "的" + WeatherName(bestWeather) + "中最能发挥作用；错过条件时仍保留基础装配属性。";
            description = SeventhPortLoreCatalog.DescribeComponent(id, description);
            return new ComponentArtEntry(id, name, slot, rarity, installWood, price,
                effect, specialization, description, artSourceId, storyId, hull, armor, firepower, speed, vision,
                bestTime, bestRoute, bestWeather,
                "同时满足适航时间、航道和天气中的每一项，额外获得一档条件效果。 ");
        }

        private static string TimeName(ComponentTimeBand value)
        {
            switch (value)
            {
                case ComponentTimeBand.Dawn: return "黎明";
                case ComponentTimeBand.Day: return "白昼";
                case ComponentTimeBand.Dusk: return "黄昏";
                case ComponentTimeBand.Night: return "夜间";
                default: return "任意时段";
            }
        }

        private static string RouteName(ComponentRouteType value)
        {
            switch (value)
            {
                case ComponentRouteType.Coastal: return "沿岸航道";
                case ComponentRouteType.Strait: return "海峡航道";
                case ComponentRouteType.Reef: return "礁区航道";
                case ComponentRouteType.Trade: return "商贸航道";
                case ComponentRouteType.OpenSea: return "开放海域";
                case ComponentRouteType.DeepSea: return "深海航道";
                default: return "任意航道";
            }
        }

        private static string WeatherName(ComponentWeatherType value)
        {
            switch (value)
            {
                case ComponentWeatherType.Calm: return "晴朗";
                case ComponentWeatherType.Wind: return "强风";
                case ComponentWeatherType.Rain: return "降雨";
                case ComponentWeatherType.Fog: return "浓雾";
                case ComponentWeatherType.Storm: return "风暴";
                default: return "平稳天气";
            }
        }

        private static bool TryPositiveInt(string value, out int result)
        {
            return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result > 0;
        }

        private static bool TryFiniteFloat(string value, out float result)
        {
            return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                && !float.IsNaN(result) && !float.IsInfinity(result);
        }
    }
}
