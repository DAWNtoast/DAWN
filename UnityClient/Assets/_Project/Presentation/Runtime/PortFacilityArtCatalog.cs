using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Resolves each dedicated port-facility background on its first request;
    /// construction does not preload images. The collection room, tavern,
    /// component shop, maintenance boathouse, and departure dock support six true
    /// regional compositions; missing commissioned art falls back to the
    /// established generic scene without borrowing another port's identity.
    /// Regional images must preserve their facility's interaction anchors
    /// (tavern seats, shop display floor/merchant side, maintenance slipway and
    /// board clearances), but architecture and props should be genuinely
    /// redrawn rather than produced by a hue or tint filter.
    /// </summary>
    public sealed class PortFacilityArtCatalog
    {
        public const int WarehouseFacilityIndex = 0;
        public const int TavernFacilityIndex = 2;
        public const int ComponentShopFacilityIndex = 3;
        public const int TreasureFacilityIndex = 5;
        public const int MaintenanceFacilityIndex = 6;
        public const int VoyageFacilityIndex = 7;
        public const int MarketFacilityIndex = 8;
        public const int ExpectedRegionalBackgroundCount = 30;

        private static readonly string[] RegionalPortNames =
        {
            "断锚港",
            "白贝港",
            "铜钟港",
            "盐冠港",
            "暮潮港",
            "灰鸥港",
        };

        private static readonly int[] RegionalFacilityIndices =
        {
            WarehouseFacilityIndex,
            TavernFacilityIndex,
            ComponentShopFacilityIndex,
            MaintenanceFacilityIndex,
            VoyageFacilityIndex,
        };

        private static readonly IReadOnlyList<string> RegionalPortContract = Array.AsReadOnly(RegionalPortNames);
        private static readonly IReadOnlyList<int> RegionalFacilityContract = Array.AsReadOnly(RegionalFacilityIndices);

        private static readonly Dictionary<string, string> PortSlugs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "断锚港", "broken_anchor" },
            { "白贝港", "white_shell" },
            { "铜钟港", "bronze_bell" },
            { "盐冠港", "salt_crown" },
            { "暮潮港", "twilight_tide" },
            { "灰鸥港", "gray_gull" },
        };

        private static readonly string[] BackgroundResourcePaths =
        {
            "PortFacilities/warehouse_background",
            "PortFacilities/shipyard_water_gallery_v3",
            "PortFacilities/tavern_background_seated_v2",
            "PortFacilities/component_shop_background_v2",
            "PortFacilities/card_workshop_background",
            "PortFacilities/treasure_worktable_background_v3",
            "PortFacilities/port_investment_scene_internal_v1",
            "PortFacilities/voyage_background",
            "PortFacilities/Market/market_scene_background_v2",
        };

        private readonly Texture2D[] backgrounds = new Texture2D[BackgroundResourcePaths.Length];
        private readonly bool[] backgroundLoadAttempted = new bool[BackgroundResourcePaths.Length];
        private readonly Dictionary<int, Dictionary<string, Texture2D>> regionalBackgrounds;
        private readonly Func<string, Texture2D> resourceLoader;
        private int loadedRegionalBackgroundCount;

        public PortFacilityArtCatalog() : this(LoadResource)
        {
        }

        // A private loader seam lets tests count actual requests without loading
        // all Resources assets just to prove that construction is lazy.
        private PortFacilityArtCatalog(Func<string, Texture2D> loader)
        {
            resourceLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            regionalBackgrounds = new Dictionary<int, Dictionary<string, Texture2D>>();
            for (int facility = 0; facility < RegionalFacilityIndices.Length; facility += 1)
            {
                int facilityIndex = RegionalFacilityIndices[facility];
                regionalBackgrounds[facilityIndex] = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            }
        }

        /// <summary>The six canonical ports supported by the regional facility-art contract.</summary>
        public static IReadOnlyList<string> SupportedRegionalPorts => RegionalPortContract;

        /// <summary>The facility indices that may provide a distinct composition for every port.</summary>
        public static IReadOnlyList<int> SupportedRegionalFacilities => RegionalFacilityContract;

        /// <summary>Only the six authored regional docks share the painted interaction layout.</summary>
        public static bool UsesPaintedDepartureScene(string portName) =>
            PortSlugs.ContainsKey(portName ?? string.Empty);

        /// <summary>Successfully loaded regional textures; reading this count never loads art.</summary>
        public int LoadedRegionalBackgroundCount => loadedRegionalBackgroundCount;

        /// <summary>Releases cached references and missing-result flags; live UI still owns borrowed art.</summary>
        public void ReleaseLoadedTextures()
        {
            Array.Clear(backgrounds, 0, backgrounds.Length);
            Array.Clear(backgroundLoadAttempted, 0, backgroundLoadAttempted.Length);
            foreach (Dictionary<string, Texture2D> variants in regionalBackgrounds.Values) variants.Clear();
            loadedRegionalBackgroundCount = 0;
        }

        /// <summary>Explicit full resource validation; normal scene lookup remains lazy.</summary>
        public bool AreAllRegionalBackgroundsAvailable
        {
            get
            {
                foreach (int facility in RegionalFacilityIndices)
                    foreach (string port in RegionalPortNames)
                        RegionalBackground(facility, port);
                return loadedRegionalBackgroundCount == ExpectedRegionalBackgroundCount;
            }
        }

        /// <summary>Explicitly probes all commissions without counting fallback art as complete.</summary>
        public IReadOnlyList<string> MissingRegionalBackgroundPaths()
        {
            List<string> paths = new List<string>();
            foreach (int facility in RegionalFacilityIndices)
            {
                foreach (string port in RegionalPortNames)
                {
                    if (!HasRegionalBackground(facility, port))
                    {
                        paths.Add(RegionalBackgroundResourcePath(facility, port));
                    }
                }
            }
            return paths.AsReadOnly();
        }

        /// <summary>Explicitly validates generic facility art by resolving the backgrounds.</summary>
        public bool IsAvailable
        {
            get
            {
                for (int index = 0; index < backgrounds.Length; index += 1)
                {
                    if (Background(index) == null) return false;
                }
                return true;
            }
        }

        public Texture2D Background(int index)
        {
            int resolved = Mathf.Clamp(index, 0, backgrounds.Length - 1);
            if (!backgroundLoadAttempted[resolved])
            {
                backgrounds[resolved] = resourceLoader(BackgroundResourcePaths[resolved]);
                backgroundLoadAttempted[resolved] = true;
            }
            return backgrounds[resolved];
        }

        /// <summary>
        /// Resolves a port-specific facility composition and falls back to the
        /// existing generic scene while a regional image is still in production.
        /// </summary>
        public Texture2D Background(int index, string portName)
        {
            Texture2D regional = RegionalBackground(index, portName);
            return regional != null ? regional : Background(index);
        }

        /// <summary>
        /// Returns only a dedicated regional image. Callers that own a newer
        /// facility-specific fallback (for example Maintenance V2) can apply it
        /// after this lookup without weakening the regional resource contract.
        /// </summary>
        public Texture2D RegionalBackground(int facilityIndex, string portName)
        {
            if (string.IsNullOrWhiteSpace(portName) ||
                !regionalBackgrounds.TryGetValue(facilityIndex, out Dictionary<string, Texture2D> portVariants))
            {
                return null;
            }
            string resourcePath = RegionalBackgroundResourcePath(facilityIndex, portName);
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (portVariants.TryGetValue(portName, out Texture2D texture)) return texture;
            texture = resourceLoader(resourcePath);
            // Null is cached too: a missing commissioned image falls back once
            // without retrying a Resources load on every production Render().
            portVariants.Add(portName, texture);
            if (texture != null) loadedRegionalBackgroundCount += 1;
            return texture;
        }

        public bool HasRegionalBackground(int facilityIndex, string portName)
        {
            return RegionalBackground(facilityIndex, portName) != null;
        }

        /// <summary>Names the same facility using the selected port, not the tutorial harbour.</summary>
        public static string RegionalFacilityTitle(int facilityIndex, string portName, string fallback)
        {
            if (facilityIndex != TavernFacilityIndex || !PortSlugs.ContainsKey(portName ?? string.Empty))
            {
                return fallback;
            }

            switch (portName)
            {
                case "断锚港": return "断锚酒馆";
                case "白贝港": return "白贝酒馆";
                case "铜钟港": return "铜钟酒馆";
                case "盐冠港": return "盐冠酒馆";
                case "暮潮港": return "暮潮酒馆";
                default: return "灰鸥酒馆";
            }
        }

        /// <summary>
        /// Stable Resources path for each commissioned image. Returning an empty
        /// value for unsupported inputs prevents an unknown port from silently
        /// aliasing another port's composition.
        /// </summary>
        public static string RegionalBackgroundResourcePath(int facilityIndex, string portName)
        {
            if (!PortSlugs.TryGetValue(portName ?? string.Empty, out string portSlug))
            {
                return string.Empty;
            }

            switch (facilityIndex)
            {
                case WarehouseFacilityIndex:
                    return "PortFacilities/Regional/Warehouse/warehouse_" + portSlug + "_v1";
                case TavernFacilityIndex:
                    return "PortFacilities/Regional/Tavern/tavern_" + portSlug + "_v1";
                case ComponentShopFacilityIndex:
                    return "PortFacilities/Regional/ComponentShop/component_shop_" + portSlug + "_v1";
                case MaintenanceFacilityIndex:
                    return "PortFacilities/Regional/Maintenance/maintenance_" + portSlug + "_side_v2";
                case VoyageFacilityIndex:
                    return "PortFacilities/Regional/Voyage/voyage_" + portSlug + "_crew_integrated_v1";
                default:
                    return string.Empty;
            }
        }

        private static Texture2D LoadResource(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                // Facility art shares the same painted treatment as regional
                // backdrops.  A common bilinear policy avoids hard pixel steps
                // between legacy 1672x941 art and newer cropped backgrounds.
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 2;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }
    }
}
