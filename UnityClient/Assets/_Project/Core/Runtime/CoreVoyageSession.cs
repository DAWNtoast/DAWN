using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreDemoPhase
    {
        Port,
        Sailing,
        Encounter,
        Battle,
        Settlement,
    }

    public enum SailingDirection
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest,
    }

    public enum VoyageOutcome
    {
        SafeReturn,
        ArrivedAtPort,
        Failure,
        TreasureRecovered,
    }

    public struct VoyagePoint
    {
        public VoyagePoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    public sealed class CoreRouteDefinition
    {
        public CoreRouteDefinition(
            string id,
            string name,
            string theme,
            int unlockLevel,
            int recommendedPower,
            int recommendedVoyageDays,
            float nauticalMilesPerMapUnit,
            float minimumEventSpacing,
            double ambushChancePer100Nm,
            double ambushDetectionChance,
            string bossName,
            int bossPower)
        {
            Id = id;
            Name = name;
            Theme = theme;
            UnlockLevel = unlockLevel;
            RecommendedPower = recommendedPower;
            RecommendedVoyageDays = recommendedVoyageDays;
            NauticalMilesPerMapUnit = nauticalMilesPerMapUnit;
            MinimumEventSpacing = minimumEventSpacing;
            AmbushChancePer100Nm = ambushChancePer100Nm;
            AmbushDetectionChance = ambushDetectionChance;
            BossName = bossName;
            BossPower = bossPower;
        }

        public string Id { get; }
        public string Name { get; }
        public string Theme { get; }
        public int UnlockLevel { get; }
        public int RecommendedPower { get; }
        public int RecommendedVoyageDays { get; }
        public float NauticalMilesPerMapUnit { get; }
        public float MinimumEventSpacing { get; }
        public double AmbushChancePer100Nm { get; }
        public double AmbushDetectionChance { get; }
        public string BossName { get; }
        public int BossPower { get; }
    }

    public sealed class VoyageEffect
    {
        internal bool CrewBonusesApplied { get; set; }
        public int Hull { get; set; }
        public int Water { get; set; }
        public int Food { get; set; }
        public int Beer { get; set; }
        public int Medicine { get; set; }
        public int Wood { get; set; }
        public int Iron { get; set; }
        public int Cloth { get; set; }
        public int Powder { get; set; }
        public int Cannonballs { get; set; }
        public int Gold { get; set; }
        public int Fame { get; set; }
        public int Morale { get; set; }
        public int Reveal { get; set; }
        public int MapFragments { get; set; }
        public int Gem { get; set; }
        public int AncientMaterial { get; set; }
        public int Relic { get; set; }
        public int BlackPearl { get; set; }
        public int AncientCompass { get; set; }
        public int Lantern { get; set; }
        public int Curse { get; set; }
        public int TreasureClues { get; set; }
        public bool TreasureComplete { get; set; }
        public bool ForceReturn { get; set; }
        public bool PostponeTreasureCore { get; set; }
    }

    public sealed class CoreEncounterChoice
    {
        public CoreEncounterChoice(
            string label,
            string detail,
            double hours,
            VoyageEffect effect,
            string requiredCrew = "",
            bool startsBattle = false,
            int requiredTreasureClues = 0,
            bool renewable = false,
            string requiredShip = "",
            CoreVoyageTimeBand requiredTime = CoreVoyageTimeBand.Any,
            CoreVoyageWeather requiredWeather = CoreVoyageWeather.Any,
            EventTerrainConstraint requiredTerrain = EventTerrainConstraint.Any,
            int minimumCurse = 0,
            int maximumCurse = int.MaxValue)
        {
            Label = label;
            Detail = detail;
            Hours = hours;
            Effect = effect ?? new VoyageEffect();
            RequiredCrew = requiredCrew ?? string.Empty;
            StartsBattle = startsBattle;
            RequiredTreasureClues = Math.Max(0, requiredTreasureClues);
            Renewable = renewable;
            RequiredShip = requiredShip ?? string.Empty;
            RequiredTime = requiredTime;
            RequiredWeather = requiredWeather;
            RequiredTerrain = requiredTerrain;
            MinimumCurse = Math.Max(0, minimumCurse);
            MaximumCurse = Math.Max(MinimumCurse, maximumCurse);
        }

        public string Label { get; }
        public string Detail { get; }
        public double Hours { get; }
        public VoyageEffect Effect { get; }
        public string RequiredCrew { get; }
        public bool StartsBattle { get; }
        public int RequiredTreasureClues { get; }
        public bool Renewable { get; }
        public string RequiredShip { get; }
        public CoreVoyageTimeBand RequiredTime { get; }
        public CoreVoyageWeather RequiredWeather { get; }
        public EventTerrainConstraint RequiredTerrain { get; }
        public int MinimumCurse { get; }
        public int MaximumCurse { get; }
    }

    public sealed class CoreEncounter
    {
        public CoreEncounter(
            string id,
            string name,
            string category,
            string description,
            bool mandatory,
            bool hidden,
            int risk,
            int power,
            params CoreEncounterChoice[] choices)
        {
            Id = id;
            Name = name;
            Category = category;
            Description = description;
            Mandatory = mandatory;
            Hidden = hidden;
            Risk = risk;
            Power = power;
            Choices = Array.AsReadOnly(choices ?? Array.Empty<CoreEncounterChoice>());
        }

        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }
        public bool Mandatory { get; }
        public bool Hidden { get; }
        public EncounterDiscoveryKind DiscoveryKind => CoreEncounterDiscoveryRules.Classify(Id, Hidden);
        public bool CanDiscoverBySight => DiscoveryKind == EncounterDiscoveryKind.SightedLocation;
        public bool CanRevealByIntel => DiscoveryKind == EncounterDiscoveryKind.HiddenLocation;
        public bool HasChartLocation => CanDiscoverBySight || CanRevealByIntel;
        public int Risk { get; }
        public int Power { get; }
        public CoreBossKind BossKind => CoreBattleBalance.Classify(Id);
        public IReadOnlyList<CoreEncounterChoice> Choices { get; }
        public CoreEncounterRepeatPolicy RepeatPolicy { get; set; } = CoreEncounterRepeatPolicy.Diminishing;
    }

    public sealed class CoreMapNode
    {
        public int Id { get; set; }
        public VoyagePoint Position { get; set; }
        public CoreEncounter Encounter { get; set; }
        public bool IsBoss { get; set; }
        public bool Discovered { get; set; }
        public bool IntelRevealed { get; set; }
        public bool Visited { get; set; }
        public bool Skipped { get; set; }
        public bool IsChartVisible => Encounter != null && Encounter.HasChartLocation && (Discovered || IntelRevealed);
        public EventTerrainConstraint TerrainConstraint { get; set; }
        public CoreEventFootprint Footprint { get; set; }
    }

    public enum EventTerrainConstraint
    {
        OpenSea,
        Beach,
        Strait,
        Reef,
        Fog,
        Storm,
        // Kept at the end so existing enum values and seeded map layouts stay stable.
        Any,
    }

    /// <summary>Four time bands used by voyage choices and component context.</summary>
    public enum CoreVoyageTimeBand
    {
        Any,
        Dawn,
        Day,
        Dusk,
        Night,
    }

    /// <summary>Weather is derived from the active event terrain plus the map's wind/current.</summary>
    public enum CoreVoyageWeather
    {
        Any,
        Calm,
        Wind,
        Rain,
        Fog,
        Storm,
    }

    public enum SeaTerrainType
    {
        OpenWater,
        Shoal,
        Reef,
        KelpField,
        FogBank,
        StormFront,
        Island,
        Beach,
        Strait,
    }

    public sealed class CoreVoyageMap
    {
        public const int GridColumns = 48;
        public const int GridRows = 32;
        public const float Width = 100f;
        public const float Height = 64f;

        private readonly List<CoreEventFootprint> eventFootprints = new List<CoreEventFootprint>();

        public CoreVoyageMap()
        {
            for (int index = 0; index < TerrainFeatureIds.Length; index += 1)
            {
                TerrainFeatureIds[index] = -1;
                AnchorNodeIds[index] = -1;
            }
        }

        public string Seed { get; set; }
        public uint Hash { get; set; }
        public CoreRouteDefinition Route { get; set; }
        public VoyagePoint Departure { get; set; }
        public VoyagePoint Destination { get; set; }
        public string DepartureName { get; set; }
        public string DestinationName { get; set; }
        public bool IsTreasureMap { get; set; }
        public List<CoreMapNode> Nodes { get; } = new List<CoreMapNode>();
        public List<VoyagePoint> Islands { get; } = new List<VoyagePoint>();
        public bool[] ExploredCells { get; } = new bool[GridColumns * GridRows];
        public SeaTerrainType[] TerrainCells { get; } = new SeaTerrainType[GridColumns * GridRows];
        public int[] TerrainFeatureIds { get; } = new int[GridColumns * GridRows];
        public int[] AnchorNodeIds { get; } = new int[GridColumns * GridRows];
        public bool[] EventAnchorCells { get; } = new bool[GridColumns * GridRows];
        public bool[] EventReservedCells { get; } = new bool[GridColumns * GridRows];
        public byte[] TerrainVariants { get; } = new byte[GridColumns * GridRows];
        public VoyagePoint[] CurrentVectors { get; } = new VoyagePoint[GridColumns * GridRows];
        public float[] CurrentStrengths { get; } = new float[GridColumns * GridRows];
        public VoyagePoint GlobalWindVector { get; set; }
        public SailingDirection GlobalWindDirection { get; set; }
        public float GlobalWindStrength { get; set; }
        public double CurrentPhaseA { get; set; }
        public double CurrentPhaseB { get; set; }
        public float BossExclusionRadius { get; set; }
        public int CellCount => GridColumns * GridRows;
        public float CellWidth => Width / GridColumns;
        public float CellHeight => Height / GridRows;
        public IReadOnlyList<CoreEventFootprint> EventFootprints => eventFootprints;

        internal List<CoreEventFootprint> MutableEventFootprints => eventFootprints;

        public bool IsInBounds(int column, int row)
        {
            return column >= 0 && column < GridColumns && row >= 0 && row < GridRows;
        }

        public int CellIndex(int column, int row)
        {
            if (!IsInBounds(column, row))
            {
                throw new ArgumentOutOfRangeException(nameof(column), "Cell coordinates are outside the voyage map.");
            }

            return row * GridColumns + column;
        }

        public VoyagePoint CellCenter(int column, int row)
        {
            return new VoyagePoint(
                (column + 0.5f) * Width / GridColumns,
                (row + 0.5f) * Height / GridRows);
        }

        public SeaTerrainType TerrainAt(int column, int row)
        {
            int safeColumn = Math.Max(0, Math.Min(GridColumns - 1, column));
            int safeRow = Math.Max(0, Math.Min(GridRows - 1, row));
            return TerrainCells[safeRow * GridColumns + safeColumn];
        }

        public bool IsPassable(int column, int row)
        {
            return IsInBounds(column, row) && TerrainAt(column, row) != SeaTerrainType.Island;
        }

        public int CellIndex(VoyagePoint point)
        {
            VoyageGridCoordinate cell = PointToCell(point);
            return cell.Row * GridColumns + cell.Column;
        }

        public VoyageGridCoordinate PointToCell(VoyagePoint point)
        {
            int column = Math.Max(0, Math.Min(GridColumns - 1, (int)(point.X / Width * GridColumns)));
            int row = Math.Max(0, Math.Min(GridRows - 1, (int)(point.Y / Height * GridRows)));
            return new VoyageGridCoordinate(column, row);
        }

        public bool TryPointToCell(VoyagePoint point, out VoyageGridCoordinate cell)
        {
            bool inside = point.X >= 0f && point.X <= Width && point.Y >= 0f && point.Y <= Height;
            cell = PointToCell(point);
            return inside;
        }

        public SeaTerrainType TerrainAt(VoyagePoint point)
        {
            VoyageGridCoordinate cell = PointToCell(point);
            return TerrainAt(cell.Column, cell.Row);
        }

        public bool IsPassable(VoyagePoint point)
        {
            return TryPointToCell(point, out VoyageGridCoordinate cell) && IsPassable(cell.Column, cell.Row);
        }

        public int TerrainFeatureIdAt(int column, int row)
        {
            return IsInBounds(column, row) ? TerrainFeatureIds[row * GridColumns + column] : -1;
        }

        public int AnchorNodeIdAt(int column, int row)
        {
            return IsInBounds(column, row) ? AnchorNodeIds[row * GridColumns + column] : -1;
        }

        public bool TryGetCell(int column, int row, out CoreVoyageMapCell cell)
        {
            if (!IsInBounds(column, row))
            {
                cell = default(CoreVoyageMapCell);
                return false;
            }

            int index = row * GridColumns + column;
            cell = new CoreVoyageMapCell(
                column,
                row,
                CellCenter(column, row),
                TerrainCells[index],
                ExploredCells[index],
                IsPassable(column, row),
                CurrentVectors[index],
                CurrentStrengths[index],
                TerrainFeatureIds[index],
                AnchorNodeIds[index],
                EventAnchorCells[index],
                EventReservedCells[index],
                TerrainVariants[index]);
            return true;
        }

        public CoreVoyageMapCell GetCell(int column, int row)
        {
            if (!TryGetCell(column, row, out CoreVoyageMapCell cell))
            {
                throw new ArgumentOutOfRangeException(nameof(column), "Cell coordinates are outside the voyage map.");
            }

            return cell;
        }

        public CoreEventFootprint FootprintForNode(int nodeId)
        {
            for (int index = 0; index < eventFootprints.Count; index += 1)
            {
                if (eventFootprints[index].NodeId == nodeId)
                {
                    return eventFootprints[index];
                }
            }

            return null;
        }
    }

    public sealed class VoyageCargo
    {
        public int Water { get; set; }
        public int Food { get; set; }
        public int Beer { get; set; }
        public int Medicine { get; set; }
        public int Wood { get; set; }
        public int Iron { get; set; }
        public int Cloth { get; set; }
        public int Powder { get; set; }
        public int Cannonballs { get; set; }
        public int Gold { get; set; }
        public int Fame { get; set; }
        public int Gem { get; set; }
        public int AncientMaterial { get; set; }
        public int Relic { get; set; }
        public int BlackPearl { get; set; }
        public int AncientCompass { get; set; }
        public int Lantern { get; set; }
        public int Curse { get; set; }

        public float UsedVolume =>
            Water * 0.07f + Food * 0.09f + Beer * 0.08f + Medicine * 0.04f +
            Wood + Iron * 2f + Cloth + Powder * 0.5f + Cannonballs +
            Gem * 5f + AncientMaterial * 1.5f + Relic * 5f + BlackPearl * 4f + AncientCompass * 3f + Lantern;
    }

    public enum VoyageSupplyType
    {
        Water,
        Food,
        Medicine,
        Wood,
        Powder,
        Cannonballs,
    }

    public sealed class VoyageSupplyOrder
    {
        public int Water { get; set; }
        public int Food { get; set; }
        public int Medicine { get; set; }
        public int Wood { get; set; }
        public int Powder { get; set; }
        public int Cannonballs { get; set; }

        public int this[VoyageSupplyType type]
        {
            get
            {
                switch (type)
                {
                    case VoyageSupplyType.Water: return Water;
                    case VoyageSupplyType.Food: return Food;
                    case VoyageSupplyType.Medicine: return Medicine;
                    case VoyageSupplyType.Wood: return Wood;
                    case VoyageSupplyType.Powder: return Powder;
                    default: return Cannonballs;
                }
            }
            set
            {
                int amount = Math.Max(0, value);
                switch (type)
                {
                    case VoyageSupplyType.Water: Water = amount; break;
                    case VoyageSupplyType.Food: Food = amount; break;
                    case VoyageSupplyType.Medicine: Medicine = amount; break;
                    case VoyageSupplyType.Wood: Wood = amount; break;
                    case VoyageSupplyType.Powder: Powder = amount; break;
                    default: Cannonballs = amount; break;
                }
            }
        }

        public int TotalUnits => (int)Math.Min(int.MaxValue, (long)Math.Max(0, Water) + Math.Max(0, Food) +
            Math.Max(0, Medicine) + Math.Max(0, Wood) + Math.Max(0, Powder) + Math.Max(0, Cannonballs));

        public void Clear()
        {
            Water = 0;
            Food = 0;
            Medicine = 0;
            Wood = 0;
            Powder = 0;
            Cannonballs = 0;
        }
    }

    public sealed class VoyageSupplyQuote
    {
        public decimal Cost { get; set; }
        public float AddedVolume { get; set; }
        public float ProjectedVolume { get; set; }
        public bool HasItems { get; set; }
        public bool FitsCargo { get; set; }
        public bool CanAfford { get; set; }
        public bool CanPurchase => HasItems && FitsCargo && CanAfford;
    }

    public sealed class SeaCondition
    {
        public VoyagePoint CurrentVector { get; set; }
        public SailingDirection CurrentDirection { get; set; }
        public float CurrentStrength { get; set; }
        public VoyagePoint WindVector { get; set; }
        public SailingDirection WindDirection { get; set; }
        public float WindStrength { get; set; }
    }

    public sealed class SailingForecast
    {
        public VoyagePoint Start { get; set; }
        public VoyagePoint Target { get; set; }
        public float PlannedDistanceNm { get; set; }
        public float ActualDistanceNm { get; set; }
        public float EffectiveSpeedKnots { get; set; }
        public double Hours { get; set; }
        public int WaterCost { get; set; }
        public int FoodCost { get; set; }
        public float CurrentStrength { get; set; }
        public string CurrentRelation { get; set; }
        public SailingDirection CurrentDirection { get; set; }
        public float CurrentSpeedModifier { get; set; }
        public float WindStrength { get; set; }
        public string WindRelation { get; set; }
        public SailingDirection WindDirection { get; set; }
        public float WindSpeedModifier { get; set; }
        public bool Clipped { get; set; }
        public bool Rerouted { get; set; }
        public bool Blocked { get; set; }
        public List<VoyagePoint> PathPoints { get; set; } = new List<VoyagePoint>();
    }

    public sealed class VoyageSettlement
    {
        public VoyageOutcome Outcome { get; set; }
        public string Title { get; set; }
        public string Destination { get; set; }
        public float ProtectionRatio { get; set; }
        public float DistanceNm { get; set; }
        public double Hours { get; set; }
        public float Coverage { get; set; }
        public int CompletedEvents { get; set; }
        public int TotalEvents { get; set; }
        public int BankedGold { get; set; }
        public int BankedWood { get; set; }
        public int BankedIron { get; set; }
        public int BankedFame { get; set; }
        public bool ChartSaved { get; set; }
        public int BankedFragments { get; set; }
        public int BankedPirateExperience { get; set; }
        public bool TreasureCompleted { get; set; }
        public int BankedGem { get; set; }
        public int BankedAncientMaterial { get; set; }
        public int BankedRelic { get; set; }
        public int BankedBlackPearl { get; set; }
    }

    public sealed class CoreShipDefinition
    {
        public CoreShipDefinition(string id, string name, int maxHull, int armor, int firepower, float speedKnots, float cargoCapacity, int crewCount)
        {
            Id = id;
            Name = name;
            MaxHull = maxHull;
            Armor = armor;
            Firepower = firepower;
            SpeedKnots = speedKnots;
            CargoCapacity = cargoCapacity;
            CrewCount = crewCount;
            Lore = global::ShatteredSail.Content.SeventhPortLoreCatalog.DescribeShip(id, string.Empty);
        }

        public string Id { get; }
        public string Name { get; }
        public string LoreRelationLabel => global::ShatteredSail.Content.SeventhPortLoreCatalog.RelationLabelForEntity(Id);
        public int MaxHull { get; }
        public int Armor { get; }
        public int Firepower { get; }
        public float SpeedKnots { get; }
        public float CargoCapacity { get; }
        public int CrewCount { get; }
        public string Lore { get; }
    }

    /// <summary>
    /// A deterministic vertical slice of the browser demo's main loop. Values
    /// are anchored to route_config.csv, ship_config.csv, and card_config.csv.
    /// </summary>
    public sealed partial class CoreVoyageSession
    {
        public const int ShipMaxHull = 55;
        public const int ShipArmor = 2;
        public const int ShipFirepower = 6;
        public const float ShipSpeedKnots = 13f;
        public const float CargoCapacity = 80f;
        public const int CrewCount = 10;
        public const int MinimumDistanceNm = 20;
        public const int MaximumDistanceNm = 180;
        private const float PortArrivalRadiusMapUnits = 1.7f;
        public const float BossMinimumSailingDays = 10f;
        public const float BossBorderMarginMapUnits = 8f;
        public const float BossPortClearanceMapUnits = 7f;
        public const float BossPortApproachRadiusMapUnits = 18f;

        private static readonly IReadOnlyList<CoreRouteDefinition> routes = Array.AsReadOnly(new[]
        {
            new CoreRouteDefinition("route_shattered_sail_reef", "碎帆礁海", "礁石打捞与基础海战", 1, 70, 24, 27f, 9f, 0.012, 0.30, "裂桅号", 105),
            new CoreRouteDefinition("route_golden_tide_trade", "黄金潮汐商路", "贸易护航与帝国封锁", 3, 120, 22, 15f, 10f, 0.008, 0.30, "税金女王号", 145),
            new CoreRouteDefinition("route_moon_tide_ruins", "月潮遗迹海", "遗迹诅咒与深海异象", 5, 165, 25, 24f, 9.5f, 0.018, 0.20, "月蚀圣骸", 190),
        });

        private static readonly IReadOnlyList<CoreShipDefinition> ships = Array.AsReadOnly(new[]
        {
            new CoreShipDefinition("sloop", "轻帆船 · 灰鸥", 55, 2, 6, 13f, 80f, 10),
            new CoreShipDefinition("merchant", "双桅商船 · 金潮", 95, 4, 7, 12f, 150f, 16),
            new CoreShipDefinition("patrol", "巡防舰 · 铜钟", 155, 10, 22, 13f, 150f, 22),
            new CoreShipDefinition("relic", "遗迹猎船 · 月蚀", 110, 5, 14, 18f, 145f, 14),
            new CoreShipDefinition("salvage", "打捞船 · 拾潮", 125, 6, 8, 11f, 185f, 16),
            new CoreShipDefinition("courier", "快帆邮船 · 白羽", 65, 2, 6, 21f, 75f, 10),
            new CoreShipDefinition("provision", "补给船 · 丰穗", 115, 4, 6, 11f, 230f, 16),
            new CoreShipDefinition("gunship", "重炮舰 · 雷铸", 240, 16, 38, 11f, 185f, 28),
            new CoreShipDefinition("barge", "远洋货驳 · 盐山", 170, 6, 8, 10f, 330f, 22),
            new CoreShipDefinition("privateer", "武装商船 · 誓金", 190, 10, 24, 14f, 230f, 24),
            new CoreShipDefinition("occult", "雾行船 · 夜灯", 135, 6, 22, 21f, 170f, 14),
            new CoreShipDefinition("clipper", "飞剪商船 · 逐风", 145, 6, 14, 21f, 280f, 20),
        });

        private static readonly string[] portNames = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private static readonly IReadOnlyList<string> destinationPorts = Array.AsReadOnly(portNames);
        private static readonly VoyageSupplyType[] purchasableSupplies =
        {
            VoyageSupplyType.Water,
            VoyageSupplyType.Food,
            VoyageSupplyType.Medicine,
            VoyageSupplyType.Wood,
            VoyageSupplyType.Powder,
            VoyageSupplyType.Cannonballs,
        };
        private readonly List<string> logEntries = new List<string>();
        private Mulberry32Random voyageRandom;
        private Mulberry32Random bossTreasureRandom = new Mulberry32Random(unchecked((uint)Guid.NewGuid().GetHashCode()));
        private double rationProgress;
        private CoreMapNode activeNode;
        private VoyageCargo departureCargo;
        private VoyageEffect pendingBattleReward;
        private bool pendingBoardingComponentDrop;
        private int pendingBoardingComponentSlot = -1;
        private bool bossDefeated;
        private bool randomAmbushOccurred;
        private bool hasNavigator = true;
        private bool hasCarpenter = true;
        private bool hasDoctor = true;
        private bool hasGunner = true;
        private bool hasCook = true;
        private bool hasDiver = true;
        private int componentHullBonus;
        private int componentArmorBonus;
        private int componentFirepowerBonus;
        private float componentSpeedBonus;
        private float componentVisionBonus;
        private int componentContextHullBonus;
        private int componentContextArmorBonus;
        private int componentContextFirepowerBonus;
        private float componentContextSpeedBonus;
        private float componentContextVisionBonus;
        private float componentContextRationMultiplier = 1f;
        private int selectedOrdinaryCrew;
        private int selectedSpecialCrewCount;

        public CoreVoyageSession()
        {
            CurrentPort = "断锚港";
            SelectedDestinationPort = DefaultDestinationFor(CurrentPort);
            ProfileGold = 500;
            SelectedShipIndex = 0;
            SelectedRouteIndex = 0;
            SelectedSeed = "SHATTERED-SAIL";
            Heading = SailingDirection.East;
            PlannedDistanceNm = 120;
            Phase = CoreDemoPhase.Port;
            Hull = MaxHull;
            Morale = 5;
            Cargo = CreateStartingCargo();
            selectedOrdinaryCrew = SelectedShip.CrewCount;
            selectedSpecialCrewCount = 0;
            AddLog("断锚港已完成整备，轻帆船等待出航。");
        }

        public static IReadOnlyList<CoreRouteDefinition> Routes => routes;
        public static IReadOnlyList<CoreBattleCard> CardLibrary => cardLibrary;
        public static IReadOnlyList<CoreShipDefinition> Ships => ships;
        public static IReadOnlyList<string> DestinationPorts => destinationPorts;
        public static IReadOnlyList<VoyageSupplyType> PurchasableSupplies => purchasableSupplies;
        public CoreDemoPhase Phase { get; private set; }
        public int SelectedRouteIndex { get; private set; }
        public string SelectedSeed { get; private set; }
        public string CurrentPort { get; private set; }
        /// <summary>港口 phase 中玩家为下一张海图选择的到达港。</summary>
        public string SelectedDestinationPort { get; private set; }
        public decimal ProfileGold { get; private set; }
        public int SelectedShipIndex { get; private set; }
        public CoreShipDefinition SelectedShip => ships[SelectedShipIndex];
        public int MaxHull => Math.Max(1, SelectedShip.MaxHull + componentHullBonus + componentContextHullBonus);
        public int Armor => Math.Max(0, SelectedShip.Armor + componentArmorBonus + componentContextArmorBonus);
        public int Firepower => Math.Max(0, SelectedShip.Firepower + componentFirepowerBonus + componentContextFirepowerBonus);
        public float SpeedKnots => Math.Max(2.5f, SelectedShip.SpeedKnots + componentSpeedBonus + componentContextSpeedBonus);
        public float VisionBonusNm => componentVisionBonus + componentContextVisionBonus;
        public float CargoCapacityValue => SelectedShip.CargoCapacity;
        public int CrewCountValue => SelectedShip.CrewCount;
        /// <summary>当前出航编组中的普通船员数量。</summary>
        public int SelectedOrdinaryCrew => Math.Max(0, Math.Min(CrewCountValue, selectedOrdinaryCrew));
        /// <summary>当前出航编组中的特殊船员数量。</summary>
        public int SelectedSpecialCrewCount => Math.Max(0, selectedSpecialCrewCount);
        public int MinimumOrdinaryCrew => (CrewCountValue + 1) / 2;
        public bool IsShortStaffed => SelectedOrdinaryCrew < MinimumOrdinaryCrew;
        public float CrewSpeedPenalty => IsShortStaffed ? (MinimumOrdinaryCrew - SelectedOrdinaryCrew) * 0.8f : 0f;
        public int CrewCombatPenalty => IsShortStaffed ? (MinimumOrdinaryCrew - SelectedOrdinaryCrew) * 2 : 0;
        public float EffectiveSpeedKnots => Math.Max(2.5f, SpeedKnots - CrewSpeedPenalty);
        public int EffectiveFirepower => Math.Max(0, Firepower - CrewCombatPenalty);
        public int EffectiveArmor => Math.Max(0, Armor - (CrewCombatPenalty / 2));
        public int EffectiveCrewCount => Math.Min(CrewCountValue, SelectedOrdinaryCrew + SelectedSpecialCrewCount);
        public int Hull { get; private set; }
        public int Morale { get; private set; }
        public VoyageCargo Cargo { get; private set; }
        public CoreVoyageMap Map { get; private set; }
        public VoyagePoint Position { get; private set; }
        public SailingDirection Heading { get; private set; }
        public int PlannedDistanceNm { get; private set; }
        public bool HasDirectCourseTarget { get; private set; }
        public VoyagePoint DirectCourseTarget { get; private set; }
        public float TotalDistanceNm { get; private set; }
        public double TotalHours { get; private set; }
        public CoreEncounter ActiveEncounter { get; private set; }
        public CoreBattleState Battle { get; private set; }
        public SeaTerrainType BattleTerrain { get; private set; } = SeaTerrainType.OpenWater;
        public VoyageSettlement Settlement { get; private set; }
        public IReadOnlyList<string> LogEntries => logEntries;
        public IReadOnlyList<int> BattleDeck => GetBattleDeck(NavalBattleStage.Naval);
        public bool HasNavigator => hasNavigator;
        public bool CanDrawSeaChart => HasNavigator && !IsTreasureVoyage;
        public bool HasCarpenter => hasCarpenter;
        public bool HasDoctor => hasDoctor;
        public bool HasGunner => hasGunner;
        public bool HasCook => hasCook;
        public bool HasDiver => hasDiver;
        public bool BossDefeated => bossDefeated;
        /// <summary>Boarding victories pay the naval victory loot multiplied by 1.5.</summary>
        public const int BoardingLootMultiplierPercent = 150;
        /// <summary>Probability that a successful capture also yields a component.</summary>
        public const double BoardingComponentDropChance = 0.35d;
        public bool LastBattleWasCaptured { get; private set; }

        /// <summary>
        /// Consumes the one-shot component drop selected by a boarding victory.
        /// The core session exposes only the slot; the presentation layer owns
        /// the component inventory and art catalog.
        /// </summary>
        public bool TryConsumeBoardingComponentDrop(out int slot)
        {
            slot = pendingBoardingComponentSlot;
            if (!pendingBoardingComponentDrop || slot < 0)
            {
                slot = -1;
                return false;
            }

            pendingBoardingComponentDrop = false;
            pendingBoardingComponentSlot = -1;
            return true;
        }

        public void RecordBoardingComponentDrop(string componentName)
        {
            if (!string.IsNullOrWhiteSpace(componentName))
                AddLog("夺船额外战利品：获得「" + componentName.Trim() + "」。");
        }

        public void RestoreProfile(decimal profileGold, string currentPort)
        {
            if (Phase != CoreDemoPhase.Port)
            {
                return;
            }

            // Legacy integer saves could exceed the newer account limit. Preserve those
            // assets, while every new credit still obeys MaximumProfileGold.
            decimal restoredGold = Math.Max(0m, Math.Min(int.MaxValue, profileGold));
            ProfileGold = decimal.Truncate(restoredGold * 10000m) / 10000m;
            if (!string.IsNullOrWhiteSpace(currentPort))
            {
                CurrentPort = currentPort.Trim();
            }
            EnsureSelectedDestination();
        }

        public bool SelectDestinationPort(int portIndex)
        {
            if (portIndex < 0 || portIndex >= portNames.Length)
            {
                return false;
            }

            return SelectDestinationPort(portNames[portIndex]);
        }

        public bool SelectDestinationPort(string portName)
        {
            if (Phase != CoreDemoPhase.Port || string.IsNullOrWhiteSpace(portName))
            {
                return false;
            }

            string candidate = portName.Trim();
            if (string.Equals(candidate, CurrentPort, StringComparison.Ordinal) ||
                Array.IndexOf(portNames, candidate) < 0)
            {
                return false;
            }

            SelectedDestinationPort = candidate;
            return true;
        }

        public void GrantProfileGold(int amount, string reason)
        {
            if (Phase != CoreDemoPhase.Port || amount <= 0)
            {
                return;
            }

            int banked = (int)Math.Min(amount, decimal.Floor(Math.Max(0m,
                CoreEconomyBalance.MaximumProfileGold - ProfileGold)));
            int aboard = amount - banked;
            if ((long)Cargo.Gold + aboard > int.MaxValue)
            {
                AddLog("账户与随船金币已达数量上限，暂时无法接收本次奖励。");
                return;
            }
            ProfileGold += banked;
            Cargo.Gold += aboard;
            AddLog((string.IsNullOrWhiteSpace(reason) ? "港口奖励" : reason.Trim()) + "：金币 +" + amount + "。");
        }

        public bool SelectShip(int shipIndex)
        {
            if (!CanSelectShip(shipIndex))
            {
                return false;
            }

            SelectedShipIndex = shipIndex;
            selectedOrdinaryCrew = SelectedShip.CrewCount;
            selectedSpecialCrewCount = 0;
            Hull = MaxHull;
            AddLog("已将旗舰切换为「" + SelectedShip.Name + "」。");
            return true;
        }

        public void ConfigureCrewCapabilities(bool navigator, bool carpenter, bool doctor, bool gunner, bool cook, bool diver)
        {
            if (Phase != CoreDemoPhase.Port)
            {
                return;
            }

            hasNavigator = navigator;
            hasCarpenter = carpenter;
            hasDoctor = doctor;
            hasGunner = gunner;
            hasCook = cook;
            hasDiver = diver;
        }

        /// <summary>Stores numeric crew counts selected for the next departure.</summary>
        public void ConfigureDepartureCrew(int ordinaryCrew, int specialCrew)
        {
            if (Phase != CoreDemoPhase.Port) return;
            // Keep the persisted/API-facing formation within the ship's actual
            // berths. The departure panel already applies this clamp while the
            // sliders move, but callers can also configure a voyage directly
            // (save migration, scripted tutorial and tests). Without the same
            // invariant here, a malformed special-crew count could make the
            // roster report more people than the vessel can carry and silently
            // crowd out ordinary sailors at voyage start.
            selectedSpecialCrewCount = Math.Max(0, Math.Min(CrewCountValue, specialCrew));
            int ordinaryCapacity = Math.Max(0, CrewCountValue - selectedSpecialCrewCount);
            selectedOrdinaryCrew = Math.Max(0, Math.Min(ordinaryCapacity, ordinaryCrew));
        }

        public void ConfigureComponentBonuses(int hull, int armor, int firepower, float speed, float vision, bool preserveCurrentHull = false)
        {
            if (Phase != CoreDemoPhase.Port)
            {
                return;
            }

            // Returning to port ends the temporary weather/route window. Clear
            // it before recomputing the permanent loadout totals so a storm
            // bonus cannot be carried into the next refit or shop purchase.
            componentContextHullBonus = 0;
            componentContextArmorBonus = 0;
            componentContextFirepowerBonus = 0;
            componentContextSpeedBonus = 0f;
            componentContextVisionBonus = 0f;
            componentContextRationMultiplier = 1f;
            int previousMaxHull = MaxHull;
            // The starter components are already included in ship base stats.
            // Preserve negative deltas when a lighter variant trades durability
            // or firepower for speed; clamp the final ship stat, not its delta.
            componentHullBonus = hull;
            componentArmorBonus = armor;
            componentFirepowerBonus = firepower;
            componentSpeedBonus = speed;
            componentVisionBonus = vision;
            // Environmental bonuses only belong to an active voyage. A refit
            // in port must clear the previous leg's temporary context effect.
            componentContextHullBonus = 0;
            componentContextArmorBonus = 0;
            componentContextFirepowerBonus = 0;
            componentContextSpeedBonus = 0f;
            componentContextVisionBonus = 0f;
            componentContextRationMultiplier = 1f;
            // Refitting physical inventory must not turn unequip/equip into a free repair loop.
            Hull = Math.Min(MaxHull, Hull + (preserveCurrentHull ? 0 : Math.Max(0, MaxHull - previousMaxHull)));
        }

        /// <summary>
        /// Applies the temporary bonus from the currently matching time,
        /// route and weather profile.  Presentation refreshes this before a
        /// forecast and after each sailing step; the core keeps the resulting
        /// stats in the same seams as the permanent loadout bonuses.
        /// </summary>
        public void ConfigureComponentContextEffect(int hull, int armor, int firepower, float speed, float vision, float rationMultiplier)
        {
            componentContextHullBonus = Math.Max(-20, Math.Min(20, hull));
            componentContextArmorBonus = Math.Max(-10, Math.Min(10, armor));
            componentContextFirepowerBonus = Math.Max(-20, Math.Min(20, firepower));
            componentContextSpeedBonus = Math.Max(-3f, Math.Min(3f, speed));
            componentContextVisionBonus = Math.Max(-50f, Math.Min(50f, vision));
            componentContextRationMultiplier = Math.Max(0.8f, Math.Min(1.2f, rationMultiplier <= 0f ? 1f : rationMultiplier));
        }

        public bool TrySpendProfileGold(decimal amount, string reason)
        {
            if (Phase != CoreDemoPhase.Port || amount <= 0 || !CoreEconomyBalance.IsValidGoldPayment(amount) || ProfileGold < amount)
            {
                return false;
            }

            ProfileGold -= amount;
            AddLog((string.IsNullOrWhiteSpace(reason) ? "港口消费" : reason.Trim()) + "：金币 -" + amount + "。");
            return true;
        }

        public bool TryDepositTravelCash()
        {
            if (Phase != CoreDemoPhase.Port || Cargo.Gold <= 0 || ProfileGold >= CoreEconomyBalance.MaximumProfileGold)
                return false;
            // Ship cash is integral. Never round a fractional account headroom up.
            int amount = (int)Math.Min(Cargo.Gold, decimal.Floor(CoreEconomyBalance.MaximumProfileGold - ProfileGold));
            if (amount <= 0) return false;
            ProfileGold += amount;
            Cargo.Gold -= amount;
            AddLog("随船备用金存入船长账户：金币 +" + amount + "。");
            return true;
        }

        public int GetMarketCargoAmount(string material)
        {
            switch ((material ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "water": return Cargo.Water;
                case "food": return Cargo.Food;
                case "beer": return Cargo.Beer;
                case "medicine": return Cargo.Medicine;
                case "wood": return Cargo.Wood;
                case "iron": return Cargo.Iron;
                case "cloth": return Cargo.Cloth;
                case "powder": return Cargo.Powder;
                case "cannonballs": return Cargo.Cannonballs;
                default: return 0;
            }
        }

        public bool TryPurchaseMarketCargo(string material, int quantity, decimal cost, float volumePerUnit)
        {
            string key = (material ?? string.Empty).Trim().ToLowerInvariant();
            if (Phase != CoreDemoPhase.Port || quantity <= 0 || cost <= 0 || !CoreEconomyBalance.IsValidGoldPayment(cost) ||
                volumePerUnit < 0f || float.IsNaN(volumePerUnit) || float.IsInfinity(volumePerUnit) ||
                ProfileGold < cost || !CoreEconomyBalance.HasNonNegativeCargo(Cargo)) return false;
            if (!CanAddMarketCargo(key, quantity)) return false;
            // Keep compatibility with conservative caller quotes, but never let a
            // smaller quote bypass the physical volume used by the cargo hold.
            double unitVolume = Math.Max(volumePerUnit, CoreWarehouseItemCatalog.Find(key).UnitVolume);
            if ((double)Cargo.UsedVolume + quantity * unitVolume > CargoCapacityValue + 0.001d) return false;
            ProfileGold -= cost;
            AddMarketCargo(key, quantity);
            AddLog("港口市场采购" + MarketMaterialName(key) + " ×" + quantity + "：金币 -" + cost + "。");
            return true;
        }

        public bool TrySellMarketCargo(string material, int quantity, decimal revenue)
        {
            string key = (material ?? string.Empty).Trim().ToLowerInvariant();
            if (Phase != CoreDemoPhase.Port || quantity <= 0 || revenue <= 0 || !CoreEconomyBalance.IsValidGoldAmount(revenue) ||
                GetMarketCargoAmount(key) < quantity || revenue > CoreEconomyBalance.MaximumProfileGold - ProfileGold) return false;
            RemoveMarketCargo(key, quantity);
            ProfileGold += revenue;
            AddLog("港口市场出售" + MarketMaterialName(key) + " ×" + quantity + "：金币 +" + revenue + "。");
            return true;
        }

        public bool TryPurchaseMarketProvisions(int water, int food, decimal totalCost)
        {
            // A combined quote is one transaction: never buy only the water when
            // the food would exceed the remaining money or hold capacity.
            if (Phase != CoreDemoPhase.Port || water < 0 || food < 0 || (water == 0 && food == 0) ||
                totalCost <= 0 || !CoreEconomyBalance.IsValidGoldPayment(totalCost) || ProfileGold < totalCost || !CoreEconomyBalance.HasNonNegativeCargo(Cargo) ||
                water > int.MaxValue - Cargo.Water || food > int.MaxValue - Cargo.Food ||
                (double)Cargo.UsedVolume + water * 0.07d + food * 0.09d > CargoCapacityValue + 0.001d)
                return false;
            ProfileGold -= totalCost;
            Cargo.Water += water;
            Cargo.Food += food;
            AddLog("出航水粮采购：淡水 +" + water + "、食物 +" + food + "，金币 -" + totalCost + "。");
            return true;
        }

        private bool CanAddMarketCargo(string material, int quantity)
        {
            if (!IsMarketMaterial(material)) return false;
            int current = GetMarketCargoAmount(material);
            return current >= 0 && quantity <= int.MaxValue - current;
        }

        private static bool IsMarketMaterial(string material)
        {
            switch (material)
            {
                case "water":
                case "food":
                case "beer":
                case "medicine":
                case "wood":
                case "iron":
                case "cloth":
                case "powder":
                case "cannonballs":
                    return true;
                default:
                    return false;
            }
        }

        private static string MarketMaterialName(string material)
        {
            switch (material)
            {
                case "water": return "淡水";
                case "food": return "食物";
                case "beer": return "啤酒";
                case "medicine": return "药品";
                case "wood": return "木材";
                case "iron": return "铁矿";
                case "cloth": return "帆布";
                case "powder": return "火药";
                default: return "炮弹";
            }
        }

        private void AddMarketCargo(string material, int quantity)
        {
            switch (material)
            {
                case "water": Cargo.Water += quantity; break;
                case "food": Cargo.Food += quantity; break;
                case "beer": Cargo.Beer += quantity; break;
                case "medicine": Cargo.Medicine += quantity; break;
                case "wood": Cargo.Wood += quantity; break;
                case "iron": Cargo.Iron += quantity; break;
                case "cloth": Cargo.Cloth += quantity; break;
                case "powder": Cargo.Powder += quantity; break;
                case "cannonballs": Cargo.Cannonballs += quantity; break;
            }
        }

        private void RemoveMarketCargo(string material, int quantity)
        {
            switch (material)
            {
                case "water": Cargo.Water -= quantity; break;
                case "food": Cargo.Food -= quantity; break;
                case "beer": Cargo.Beer -= quantity; break;
                case "medicine": Cargo.Medicine -= quantity; break;
                case "wood": Cargo.Wood -= quantity; break;
                case "iron": Cargo.Iron -= quantity; break;
                case "cloth": Cargo.Cloth -= quantity; break;
                case "powder": Cargo.Powder -= quantity; break;
                case "cannonballs": Cargo.Cannonballs -= quantity; break;
            }
        }

        public bool TrySellCargoMaterial(string material, int quantity)
        {
            if (Phase != CoreDemoPhase.Port || quantity <= 0) return false;
            int available;
            int unitPrice = GameplayBalance.MaterialSalePrice(material);
            string name;
            switch (material)
            {
                case "wood": available = Cargo.Wood; name = "木材"; break;
                case "iron": available = Cargo.Iron; name = "铁矿"; break;
                case "cloth": available = Cargo.Cloth; name = "帆布"; break;
                default: return false;
            }
            long proceeds = (long)quantity * unitPrice;
            if (quantity > available || proceeds + ProfileGold > CoreEconomyBalance.MaximumProfileGold) return false;
            if (material == "wood") Cargo.Wood -= quantity;
            else if (material == "iron") Cargo.Iron -= quantity;
            else Cargo.Cloth -= quantity;
            ProfileGold += (int)proceeds;
            AddLog("仓库出售" + name + " ×" + quantity + "：金币 +" + proceeds + "，已腾出货舱空间。");
            return true;
        }

        public static string SupplyName(VoyageSupplyType type)
        {
            switch (type)
            {
                case VoyageSupplyType.Water: return "淡水";
                case VoyageSupplyType.Food: return "食物";
                case VoyageSupplyType.Medicine: return "药品";
                case VoyageSupplyType.Wood: return "木材";
                case VoyageSupplyType.Powder: return "火药";
                default: return "炮弹";
            }
        }

        public static float SupplyVolume(VoyageSupplyType type)
        {
            switch (type)
            {
                case VoyageSupplyType.Water: return 0.07f;
                case VoyageSupplyType.Food: return 0.09f;
                case VoyageSupplyType.Medicine: return 0.04f;
                case VoyageSupplyType.Wood: return 1f;
                case VoyageSupplyType.Powder: return 0.5f;
                default: return 1f;
            }
        }

        public int GetSupplyAmount(VoyageSupplyType type)
        {
            switch (type)
            {
                case VoyageSupplyType.Water: return Cargo.Water;
                case VoyageSupplyType.Food: return Cargo.Food;
                case VoyageSupplyType.Medicine: return Cargo.Medicine;
                case VoyageSupplyType.Wood: return Cargo.Wood;
                case VoyageSupplyType.Powder: return Cargo.Powder;
                default: return Cargo.Cannonballs;
            }
        }

        public decimal GetSupplyUnitPrice(VoyageSupplyType type)
        {
            return CorePortEconomy.BaseSupplyUnitPrice(CurrentPort, type);
        }

        public decimal CalculateSupplyPurchaseCost(VoyageSupplyType type, int amount)
        {
            int quantity = Math.Max(0, amount);
            decimal unitPrice = CorePortEconomy.BaseSupplyUnitPrice(CurrentPort, type);
            return quantity * unitPrice;
        }

        public VoyageSupplyQuote QuoteSupplyPurchase(VoyageSupplyOrder order)
        {
            VoyageSupplyQuote quote = new VoyageSupplyQuote
            {
                ProjectedVolume = Cargo.UsedVolume,
                FitsCargo = true,
                CanAfford = true,
            };

            if (order == null)
            {
                return quote;
            }

            decimal totalCost = 0m;
            foreach (VoyageSupplyType type in purchasableSupplies)
            {
                int amount = Math.Max(0, order[type]);
                totalCost += CalculateSupplyPurchaseCost(type, amount);
                quote.AddedVolume += SupplyVolume(type) * amount;
                quote.HasItems |= amount > 0;
            }

            quote.ProjectedVolume += quote.AddedVolume;
            quote.Cost = totalCost;
            quote.FitsCargo = CoreEconomyBalance.HasNonNegativeCargo(Cargo) && quote.ProjectedVolume <= CargoCapacityValue + 0.001f;
            quote.CanAfford = CoreEconomyBalance.IsValidGoldPayment(totalCost) && ProfileGold >= totalCost;
            return quote;
        }

        public bool TryPurchaseSupplies(VoyageSupplyOrder order)
        {
            if (Phase != CoreDemoPhase.Port)
            {
                return false;
            }

            VoyageSupplyQuote quote = QuoteSupplyPurchase(order);
            if (!quote.CanPurchase)
            {
                return false;
            }

            int water = Math.Max(0, order.Water);
            int food = Math.Max(0, order.Food);
            int medicine = Math.Max(0, order.Medicine);
            int wood = Math.Max(0, order.Wood);
            int powder = Math.Max(0, order.Powder);
            int cannonballs = Math.Max(0, order.Cannonballs);
            ProfileGold -= quote.Cost;
            Cargo.Water += water;
            Cargo.Food += food;
            Cargo.Medicine += medicine;
            Cargo.Wood += wood;
            Cargo.Powder += powder;
            Cargo.Cannonballs += cannonballs;
            AddLog("出航补给采购完成：" + (water + food + medicine + wood + powder + cannonballs) + " 单位物资，金币 -" + quote.Cost + "。");
            return true;
        }

        public bool RestockAtPort()
        {
            if (Phase != CoreDemoPhase.Port)
            {
                return false;
            }

            return TryPurchaseSupplies(CreateRecommendedSupplyOrder());
        }

        public VoyageSupplyOrder CreateRecommendedSupplyOrder()
        {
            // Aim for route endurance while normally reserving half the hold for equipment and loot.
            // Existing goods are never discarded: food/water take priority within the actual free hold.
            int target = Math.Min((int)Math.Ceiling(CrewCountValue * GameplayBalance.SupplyDays(SelectedRouteIndex) / 10d) * 10,
                (int)(CargoCapacityValue * 0.5f / 0.16f / 10f) * 10);
            var order = new VoyageSupplyOrder();
            float space = Math.Max(0f, CargoCapacityValue - Cargo.UsedVolume);
            while (true)
            {
                bool waterFits = Cargo.Water + order.Water < target && space + 0.0001f >= SupplyVolume(VoyageSupplyType.Water);
                bool foodFits = Cargo.Food + order.Food < target && space + 0.0001f >= SupplyVolume(VoyageSupplyType.Food);
                if (!waterFits && !foodFits) break;
                bool addWater = waterFits && (!foodFits || Cargo.Water + order.Water <= Cargo.Food + order.Food);
                if (addWater) { order.Water++; space -= SupplyVolume(VoyageSupplyType.Water); }
                else { order.Food++; space -= SupplyVolume(VoyageSupplyType.Food); }
            }
            foreach (VoyageSupplyType type in new[] { VoyageSupplyType.Medicine, VoyageSupplyType.Wood, VoyageSupplyType.Powder, VoyageSupplyType.Cannonballs })
            {
                int reserve = type == VoyageSupplyType.Medicine ? 2 : type == VoyageSupplyType.Cannonballs ? 10 : 8;
                int amount = Math.Min(Math.Max(0, reserve - GetSupplyAmount(type)),
                    Math.Max(0, (int)Math.Floor((space + 0.0001f) / SupplyVolume(type))));
                order[type] = amount;
                space -= amount * SupplyVolume(type);
            }
            return order;
        }

        public void SelectRoute(int routeIndex)
        {
            if (Phase != CoreDemoPhase.Port)
            {
                return;
            }

            SelectedRouteIndex = Math.Max(0, Math.Min(routes.Count - 1, routeIndex));
            selectedSeaChart = null;
            selectedTreasureMapId = string.Empty;
        }

        public void SetSeed(string seed)
        {
            if (Phase == CoreDemoPhase.Port)
            {
                selectedSeaChart = null;
                SelectedSeed = string.IsNullOrWhiteSpace(seed) ? SeedHash.DefaultSeed : seed.Trim();
            }
        }

        public void StartVoyage()
        {
            StartVoyage(SelectedDestinationPort);
        }

        public void StartVoyage(string destinationPort)
        {
            if (Phase != CoreDemoPhase.Port)
            {
                throw new InvalidOperationException("A voyage can only start from port.");
            }

            CoreTreasureMapRecord treasure = SelectedTreasureMap;
            if (treasure != null && !CanStartTreasureVoyage(out string treasureFailure))
                throw new InvalidOperationException(treasureFailure);

            // Structured seeds cannot fall back to the legacy raw-seed path
            // after a route change or an API caller clearing selection state.
            if (treasure == null && GuidedTutorialLeg == 0 && selectedSeaChart == null &&
                SelectedSeed.StartsWith("SS2.", StringComparison.OrdinalIgnoreCase) &&
                !TrySelectChartSeed(SelectedSeed, out string selectionFailure))
                throw new InvalidOperationException(selectionFailure);
            if (!CanStartSelectedChart(out string chartFailure)) throw new InvalidOperationException(chartFailure);
            if (selectedSeaChart != null && treasure == null && GuidedTutorialLeg == 0 && !string.IsNullOrWhiteSpace(destinationPort) &&
                destinationPort.Trim() != selectedSeaChart.OtherPort(CurrentPort))
                throw new InvalidOperationException("载入海图只能驶往其另一端港口。");

            EnsureSelectedDestination();
            if (!string.IsNullOrWhiteSpace(destinationPort))
            {
                SelectDestinationPort(destinationPort);
            }
            CoreRouteDefinition route = treasure == null ? routes[SelectedRouteIndex] : TreasureRoute(treasure.Rank);
            // Generate before consuming the one-shot record or mutating the port purse.
            CoreVoyageMap nextMap = treasure == null
                ? (GuidedTutorialLeg > 0 ? GenerateGuidedTutorialMap(CurrentPort) : selectedSeaChart != null ? GenerateSelectedSeaChartMap() :
                    GenerateMap(route, SelectedSeed, CurrentPort, SelectedDestinationPort))
                : GenerateTreasureMap(treasure, CurrentPort);
            LockSpecialCardsForVoyage();
            // Voyage rewards and cash remain integer resources; leave the account's
            // fractional balance in port instead of truncating it on departure.
            int travelCash = (int)Math.Min(decimal.Floor(ProfileGold), Math.Max(0, 60 - Cargo.Gold));
            ProfileGold -= travelCash;
            Cargo.Gold += travelCash;
            Map = nextMap;
            RecordCollectionPortVisit(Map.DepartureName);
            BeginTreasureVoyageState(treasure);
            ResetGuidedTutorialVoyageProgress();
            voyageRandom = new Mulberry32Random(Map.Hash ^ 0xA511E9B3u);
            // Repeating a chart still gets a fresh loot chance, without changing
            // the chart's deterministic combat draws or sea-event sequence.
            bossTreasureRandom = new Mulberry32Random(unchecked((uint)Guid.NewGuid().GetHashCode()));
            Position = Map.Departure;
            Heading = SailingDirection.East;
            PlannedDistanceNm = 120;
            Hull = MaxHull;
            Morale = 5;
            TotalDistanceNm = 0f;
            TotalHours = 0d;
            rationProgress = 0d;
            departureCargo = SnapshotCargo(Cargo);
            pendingBattleReward = null;
            pendingBoardingComponentDrop = false;
            pendingBoardingComponentSlot = -1;
            LastBattleWasCaptured = false;
            bossDefeated = false;
            pendingSeaChartRewardMultiplier = 1d;
            pendingSeaChartClaim = false;
            randomAmbushOccurred = false;
            ActiveEncounter = null;
            activeNode = null;
            HasDirectCourseTarget = false;
            Battle = null;
            Settlement = null;
            BeginSeaChartVoyage(treasure);
            if (activeSeaChart != null)
                Heading = ClosestDirection(Math.Atan2(Map.Destination.Y - Map.Departure.Y, Map.Destination.X - Map.Departure.X));
            RevealAt(Position);
            Phase = CoreDemoPhase.Sailing;
            AddLog("驶入" + route.Name + "。");
        }

        public void SetHeading(SailingDirection direction)
        {
            if (Phase == CoreDemoPhase.Sailing)
            {
                Heading = direction;
                HasDirectCourseTarget = false;
            }
        }

        public void SetDistance(int nauticalMiles)
        {
            if (Phase != CoreDemoPhase.Sailing)
            {
                return;
            }

            int clamped = Math.Max(MinimumDistanceNm, Math.Min(MaximumDistanceNm, nauticalMiles));
            PlannedDistanceNm = (int)Math.Round(clamped / 10d) * 10;
            HasDirectCourseTarget = false;
        }

        public void SetCourseTo(VoyagePoint target)
        {
            if (Phase != CoreDemoPhase.Sailing || Map == null)
            {
                return;
            }

            // Selecting a deferred optional boss is an explicit request to revisit it.
            // A nearby marker opens its challenge without requiring a zero-length sail.
            foreach (CoreMapNode node in Map.Nodes)
            {
                if (node.Visited || !CanRevisitEncounter(node.Encounter) || Distance(target, node.Position) > 0.25f) continue;
                node.Skipped = false;
                if (Distance(Position, node.Position) <= 1.5f)
                {
                    node.Discovered = true;
                    activeNode = node;
                    ActiveEncounter = node.Encounter;
                    HasDirectCourseTarget = false;
                    Phase = CoreDemoPhase.Encounter;
                    AddLog("重新查看可选挑战：「" + node.Encounter.Name + "」。");
                    return;
                }
                break;
            }

            float dx = target.X - Position.X;
            float dy = target.Y - Position.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.2)
            {
                // A last, short approach to an unlocked port is still a valid course.
                // Keep arbitrary near-zero sea clicks from replacing the current course.
                if (bossDefeated && Distance(target, Map.Destination) < 0.0001f)
                {
                    DirectCourseTarget = Map.Destination;
                    HasDirectCourseTarget = true;
                }
                return;
            }

            double angle = Math.Atan2(dy, dx);
            Heading = ClosestDirection(angle);
            float requestedMapDistance = (float)Math.Sqrt(dx * dx + dy * dy);
            int distance = (int)Math.Round(requestedMapDistance * Map.Route.NauticalMilesPerMapUnit / 10d) * 10;
            SetDistance(distance);
            float maximumMapDistance = MaximumDistanceNm / Map.Route.NauticalMilesPerMapUnit;
            float directMapDistance = Math.Min(requestedMapDistance, maximumMapDistance);
            float scale = directMapDistance / requestedMapDistance;
            DirectCourseTarget = new VoyagePoint(Position.X + dx * scale, Position.Y + dy * scale);
            HasDirectCourseTarget = true;
        }

        public SailingForecast GetForecast()
        {
            if (Map == null)
            {
                return null;
            }

            if (HasDirectCourseTarget)
            {
                VoyagePoint directTarget = new VoyagePoint(
                    Clamp(DirectCourseTarget.X, 2f, 98f),
                    Clamp(DirectCourseTarget.Y, 2f, 62f));
                return CreatePathForecast(Position, directTarget, false);
            }

            VoyagePoint vector = DirectionVector(Heading);
            float mapDistance = PlannedDistanceNm / Map.Route.NauticalMilesPerMapUnit;
            VoyagePoint rawTarget = new VoyagePoint(Position.X + vector.X * mapDistance, Position.Y + vector.Y * mapDistance);
            VoyagePoint target = new VoyagePoint(Clamp(rawTarget.X, 2f, 98f), Clamp(rawTarget.Y, 2f, 62f));
            float actualMapDistance = Distance(Position, target);
            return CreatePathForecast(Position, target, actualMapDistance + 0.01f < mapDistance);
        }

        public IReadOnlyList<VoyagePoint> PlanNavigablePath(VoyagePoint start, VoyagePoint target)
        {
            return FindNavigablePath(start, target);
        }

        private SailingForecast CreatePathForecast(VoyagePoint start, VoyagePoint requestedTarget, bool clipped)
        {
            VoyagePoint target = SnapToNearestPassable(requestedTarget);
            List<VoyagePoint> path = FindNavigablePath(start, target);
            if (path.Count < 2)
            {
                SailingForecast blocked = CreateForecast(start, start, 0f, clipped);
                blocked.Blocked = true;
                blocked.PathPoints = path;
                return blocked;
            }

            List<VoyagePoint> dockingPath = SnapPathToDestination(path);
            bool dockingApproach = !ReferenceEquals(dockingPath, path);
            // Docking stops this leg before its original boundary destination.
            // Do not apply edge-reef damage to the shortened, safe approach.
            if (dockingApproach) clipped = false;
            path = dockingPath;
            float pathMapDistance = PathDistance(path);
            SailingForecast forecast = CreateForecast(start, path[path.Count - 1], pathMapDistance * Map.Route.NauticalMilesPerMapUnit, clipped);
            forecast.PathPoints = path;
            forecast.Rerouted = !HasNavigableLine(start, path[path.Count - 1]) ||
                (!dockingApproach && path.Count > 2 && pathMapDistance > Distance(start, path[path.Count - 1]) * 1.025f);
            return forecast;
        }

        private List<VoyagePoint> SnapPathToDestination(List<VoyagePoint> path)
        {
            if (!bossDefeated || path.Count < 2 ||
                DistanceToPath(Map.Destination, path, out float progress) > PortArrivalRadiusMapUnits)
                return path;

            // End the forecast at the pier as well as the actual sail. Include the
            // final approach in travel time, provisions and XP; never jump over land.
            List<VoyagePoint> approachPath = new List<VoyagePoint> { path[0] };
            for (int index = 1; index < path.Count; index += 1)
            {
                VoyagePoint start = path[index - 1];
                VoyagePoint end = path[index];
                float segmentLength = Distance(start, end);
                if (progress > segmentLength && index < path.Count - 1)
                {
                    approachPath.Add(end);
                    progress -= segmentLength;
                    continue;
                }

                float t = segmentLength > 0f ? Clamp(progress / segmentLength, 0f, 1f) : 0f;
                VoyagePoint approach = new VoyagePoint(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                if (!HasNavigableLine(approach, Map.Destination)) return path;
                if (Distance(approachPath[approachPath.Count - 1], approach) > 0f)
                    approachPath.Add(approach);
                approachPath.Add(Map.Destination);
                return approachPath;
            }
            return path;
        }

        public SeaCondition GetSeaConditionAt(VoyagePoint position)
        {
            if (Map == null)
            {
                return null;
            }

            int cellIndex = Map.CellIndex(position);
            VoyagePoint current = Map.CurrentVectors[cellIndex];
            if (Math.Abs(current.X) + Math.Abs(current.Y) < 0.001f)
            {
                current = new VoyagePoint(1f, 0f);
            }
            double currentAngle = Math.Atan2(current.Y, current.X);

            return new SeaCondition
            {
                CurrentVector = current,
                CurrentDirection = ClosestDirection(currentAngle),
                CurrentStrength = Map.CurrentStrengths[cellIndex],
                WindVector = Map.GlobalWindVector,
                WindDirection = Map.GlobalWindDirection,
                WindStrength = Map.GlobalWindStrength,
            };
        }

        public bool Sail()
        {
            if (Phase != CoreDemoPhase.Sailing || Map == null)
            {
                return false;
            }

            SailingForecast planned = GetForecast();
            if (planned != null && planned.Blocked)
            {
                AddLog("目标海域被连续岛链封锁，请选择另一处航点。");
                return false;
            }
            bool reachesDestination = bossDefeated && planned != null &&
                Distance(planned.Target, Map.Destination) < 0.0001f;
            if (planned == null || (planned.ActualDistanceNm < 1f && !reachesDestination))
            {
                AddLog("已接近海域边界，请调整航向。");
                return false;
            }

            CoreMapNode collisionNode = null;
            VoyagePoint end = planned.Target;
            float closestProgress = float.MaxValue;
            for (int index = 0; index < Map.Nodes.Count; index += 1)
            {
                CoreMapNode node = Map.Nodes[index];
                if (UsesCombinedGuidedTutorialVoyage && node.Encounter.Id == CoreGuidedTutorialContent.BossEventId &&
                    !guidedTutorialCompletedIds.Contains(CoreGuidedTutorialContent.AmbushEventId)) continue;
                if (node.Visited || (node.Skipped && CanRevisitEncounter(node.Encounter)))
                {
                    continue;
                }

                float progress;
                float distance = DistanceToPath(node.Position, planned.PathPoints, out progress);
                if (progress > 0.2f && distance <= 1.25f && progress < closestProgress)
                {
                    closestProgress = progress;
                    collisionNode = node;
                }
            }

            CoreEncounter guidedTransientEncounter = FindGuidedTutorialTransientEncounter(planned.PathPoints,
                out VoyagePoint guidedTransientPosition, out float guidedTransientProgress);
            if (guidedTransientEncounter != null && guidedTransientProgress < closestProgress)
            {
                collisionNode = null;
                end = guidedTransientPosition;
                closestProgress = guidedTransientProgress;
            }
            else guidedTransientEncounter = null;

            CoreEncounter chartTransientEncounter = FindSeaChartTransientEncounter(planned.PathPoints,
                out VoyagePoint chartTransientPosition, out float chartTransientProgress);
            if (chartTransientEncounter != null && chartTransientProgress < closestProgress)
            {
                collisionNode = null;
                guidedTransientEncounter = null;
                end = chartTransientPosition;
                closestProgress = chartTransientProgress;
            }
            else chartTransientEncounter = null;

            float destinationProgress = reachesDestination ? PathDistance(planned.PathPoints) : float.MaxValue;
            if (reachesDestination && destinationProgress < closestProgress)
            {
                collisionNode = null;
                end = Map.Destination;
                closestProgress = destinationProgress;
            }
            else if (collisionNode != null)
            {
                end = collisionNode.Position;
            }

            float actualMapDistance = closestProgress < float.MaxValue ? closestProgress : PathDistance(planned.PathPoints);
            float actualDistance = actualMapDistance * Map.Route.NauticalMilesPerMapUnit;
            SailingForecast forecast = CreateForecast(Position, end, actualDistance, planned.Clipped);
            if (Cargo.Water < forecast.WaterCost || Cargo.Food < forecast.FoodCost)
            {
                AddLog("本航段补给不足，需要淡水和食物各 " + forecast.WaterCost + "。");
                return false;
            }

            VoyagePoint start = Position;
            ConsumeHours(forecast.Hours);
            TotalDistanceNm += actualDistance;
            AwardSpecialCardVoyageDistance(actualDistance);
            Position = end;
            HasDirectCourseTarget = false;
            RevealPath(planned.PathPoints, actualMapDistance);
            AddLog("向" + DirectionLabel(Heading) + "航行 " + actualDistance.ToString("0") + " 海里，用时 " + forecast.Hours.ToString("0.0") + " 小时。");

            if (planned.Clipped && actualMapDistance + 0.001f >= PathDistance(planned.PathPoints))
            {
                Hull = Math.Max(0, Hull - 2);
                AddLog("船体擦过海域边缘暗礁，船体 -2。");
            }

            if (Hull <= 0)
            {
                FinishVoyage(VoyageOutcome.Failure);
                return true;
            }

            if (reachesDestination && destinationProgress <= closestProgress)
            {
                FinishVoyage(VoyageOutcome.ArrivedAtPort);
                return true;
            }

            if (collisionNode != null)
            {
                // A trigger position keeps the voyage deterministic, but does not
                // make an onboard incident a place that can be charted.
                collisionNode.Discovered = collisionNode.Encounter.HasChartLocation;
                collisionNode.IntelRevealed = collisionNode.Encounter.CanRevealByIntel;
                activeNode = collisionNode;
                ActiveEncounter = collisionNode.Encounter;
                Phase = CoreDemoPhase.Encounter;
                AddLog("遭遇" + (ActiveEncounter.Mandatory ? "强制" : "可选") + "事件：「" + ActiveEncounter.Name + "」。");
                return true;
            }

            if (guidedTransientEncounter != null)
            {
                BeginGuidedTutorialTransientEncounter(guidedTransientEncounter);
                return true;
            }

            if (chartTransientEncounter != null)
            {
                BeginSeaChartTransientEncounter(chartTransientEncounter);
                return true;
            }

            double ambushChance = 1d - Math.Pow(1d - Map.Route.AmbushChancePer100Nm, actualDistance / 100d);
            if (activeSeaChart == null && GuidedTutorialLeg == 0 && !randomAmbushOccurred && voyageRandom.NextDouble() < ambushChance)
            {
                randomAmbushOccurred = true;
                bool detected = voyageRandom.NextDouble() < AmbushDetectionChance;
                ActiveEncounter = CreateRandomAmbush(detected);
                activeNode = null;
                Phase = CoreDemoPhase.Encounter;
                AddLog(detected ? "航海士提前发现航路伏击。" : "敌船从礁影后突然发动伏击。");
            }

            return true;
        }

        public bool IsChoiceAvailable(int choiceIndex)
        {
            if (Phase != CoreDemoPhase.Encounter || ActiveEncounter == null || choiceIndex < 0 || choiceIndex >= ActiveEncounter.Choices.Count)
            {
                return false;
            }

            CoreEncounterChoice choice = ActiveEncounter.Choices[choiceIndex];
            if (choice == null || !EncounterChoiceConditionsMet(choice) ||
                double.IsNaN(choice.Hours) || double.IsInfinity(choice.Hours) || choice.Hours < 0d) return false;
            if (TreasureClues < choice.RequiredTreasureClues ||
                (choice.Effect.TreasureComplete && (!IsTreasureVoyage || TreasureAdventureCompleted || Hull + choice.Effect.Hull <= 0))) return false;
            VoyageEffect applied = choice.StartsBattle ? SplitBattleEffect(choice.Effect, false) : EffectiveChoiceEffect(choiceIndex);
            int rations = RationCostFor(choice.Hours);
            if (!CanAfford(applied, rations)) return false;
            return Cargo.UsedVolume - rations * 0.16f + CargoVolumeDelta(applied) <= CargoCapacityValue + 0.001f;
        }

        /// <summary>
        /// Returns the shared condition failure used by both the core resolver and
        /// the encounter dossier. Keeping this in the session prevents a UI-only
        /// condition from disagreeing with the actual choice resolver.
        /// </summary>
        public string EncounterChoiceConditionReason(CoreEncounterChoice choice)
        {
            if (choice == null) return "当前选项不存在。";
            if (!HasEncounterCrewRequirement(choice.RequiredCrew))
                return "缺少所需船员：" + CrewRequirementLabel(choice.RequiredCrew) + "。";
            if (!string.IsNullOrWhiteSpace(choice.RequiredShip) && !ShipMatchesRequirement(choice.RequiredShip))
            {
                return "需要船型：" + ShipRequirementLabel(choice.RequiredShip) + "。";
            }

            if (choice.RequiredTime != CoreVoyageTimeBand.Any && CurrentVoyageTimeBand != choice.RequiredTime)
                return "仅限" + TimeBandLabel(choice.RequiredTime) + "执行（当前为" + TimeBandLabel(CurrentVoyageTimeBand) + "）。";
            if (choice.RequiredWeather != CoreVoyageWeather.Any && CurrentVoyageWeather != choice.RequiredWeather)
                return "需要" + WeatherLabel(choice.RequiredWeather) + "（当前为" + WeatherLabel(CurrentVoyageWeather) + "）。";
            if (choice.RequiredTerrain != EventTerrainConstraint.Any && CurrentEncounterTerrain != choice.RequiredTerrain)
                return "需要" + TerrainLabel(choice.RequiredTerrain) + "地形（当前为" + TerrainLabel(CurrentEncounterTerrain) + "）。";
            if (Cargo.Curse < choice.MinimumCurse)
                return "至少需要 " + choice.MinimumCurse + " 层诅咒（当前 " + Cargo.Curse + " 层）。";
            if (Cargo.Curse > choice.MaximumCurse)
                return "诅咒层数不能超过 " + choice.MaximumCurse + "（当前 " + Cargo.Curse + " 层）。";
            return string.Empty;
        }

        public string EncounterChoiceConditionSummary(CoreEncounterChoice choice)
        {
            if (choice == null) return string.Empty;
            List<string> requirements = new List<string>();
            if (!string.IsNullOrWhiteSpace(choice.RequiredCrew)) requirements.Add("需要" + CrewRequirementLabel(choice.RequiredCrew));
            if (!string.IsNullOrWhiteSpace(choice.RequiredShip))
                requirements.Add("船型：" + ShipRequirementLabel(choice.RequiredShip));
            if (choice.RequiredTime != CoreVoyageTimeBand.Any) requirements.Add("限" + TimeBandLabel(choice.RequiredTime));
            if (choice.RequiredWeather != CoreVoyageWeather.Any) requirements.Add("限" + WeatherLabel(choice.RequiredWeather));
            if (choice.RequiredTerrain != EventTerrainConstraint.Any) requirements.Add("限" + TerrainLabel(choice.RequiredTerrain));
            if (choice.MinimumCurse > 0) requirements.Add("诅咒≥" + choice.MinimumCurse);
            if (choice.MaximumCurse < int.MaxValue) requirements.Add("诅咒≤" + choice.MaximumCurse);
            return string.Join(" · ", requirements);
        }

        private bool EncounterChoiceConditionsMet(CoreEncounterChoice choice) =>
            EncounterChoiceConditionReason(choice).Length == 0;

        private bool ShipMatchesRequirement(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement)) return true;
            string[] ids = requirement.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < ids.Length; index += 1)
                if (string.Equals(SelectedShip.Id, ids[index].Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ShipRequirementLabel(string requirement)
        {
            string[] ids = requirement.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> labels = new List<string>();
            for (int index = 0; index < ids.Length; index += 1)
            {
                string id = ids[index].Trim();
                CoreShipDefinition ship = ships.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
                labels.Add(ship == null ? id : ship.Name);
            }
            return string.Join("或", labels);
        }

        private static string CrewRequirementLabel(string requiredCrew)
        {
            switch (requiredCrew)
            {
                case "navigator": return "航海士";
                case "carpenter": return "木匠";
                case "doctor": return "船医";
                case "gunner": return "炮手";
                case "cook": return "厨师";
                case "diver": return "潜水员";
                default: return "指定职业船员";
            }
        }

        public CoreVoyageTimeBand CurrentVoyageTimeBand
        {
            get
            {
                double hour = GameDateTime.TimeOfDay.TotalHours;
                return hour < 6d ? CoreVoyageTimeBand.Dawn :
                    hour < 12d ? CoreVoyageTimeBand.Day :
                    hour < 18d ? CoreVoyageTimeBand.Dusk : CoreVoyageTimeBand.Night;
            }
        }

        public CoreVoyageWeather CurrentVoyageWeather
        {
            get
            {
                EventTerrainConstraint terrain = CurrentEncounterTerrain;
                if (terrain == EventTerrainConstraint.Storm) return CoreVoyageWeather.Storm;
                if (terrain == EventTerrainConstraint.Fog) return CoreVoyageWeather.Fog;
                SeaCondition condition = GetSeaConditionAt(Position);
                if (condition == null) return CoreVoyageWeather.Calm;
                // The map stores wind in knots (5–13) and current strength in
                // map units (roughly 0.65–2.2), so presentation's old 0.75
                // threshold incorrectly classified every open-water cell as wind.
                if (condition.WindStrength >= 9f) return CoreVoyageWeather.Wind;
                if (condition.CurrentStrength >= 1.4f) return CoreVoyageWeather.Rain;
                return CoreVoyageWeather.Calm;
            }
        }

        public EventTerrainConstraint CurrentEncounterTerrain
        {
            get
            {
                if (activeNode != null && activeNode.TerrainConstraint != EventTerrainConstraint.Any)
                    return activeNode.TerrainConstraint;
                if (Map == null) return EventTerrainConstraint.OpenSea;
                switch (Map.TerrainAt(Position))
                {
                    case SeaTerrainType.Beach:
                    case SeaTerrainType.Island: return EventTerrainConstraint.Beach;
                    case SeaTerrainType.Strait: return EventTerrainConstraint.Strait;
                    case SeaTerrainType.Reef:
                    case SeaTerrainType.Shoal:
                    case SeaTerrainType.KelpField: return EventTerrainConstraint.Reef;
                    case SeaTerrainType.FogBank: return EventTerrainConstraint.Fog;
                    case SeaTerrainType.StormFront: return EventTerrainConstraint.Storm;
                    default: return EventTerrainConstraint.OpenSea;
                }
            }
        }

        public SeaTerrainType CurrentEncounterSeaTerrain
        {
            get
            {
                switch (CurrentEncounterTerrain)
                {
                    case EventTerrainConstraint.Beach: return SeaTerrainType.Beach;
                    case EventTerrainConstraint.Strait: return SeaTerrainType.Strait;
                    case EventTerrainConstraint.Reef: return SeaTerrainType.Reef;
                    case EventTerrainConstraint.Fog: return SeaTerrainType.FogBank;
                    case EventTerrainConstraint.Storm: return SeaTerrainType.StormFront;
                    default: return SeaTerrainType.OpenWater;
                }
            }
        }

        private static string TimeBandLabel(CoreVoyageTimeBand band)
        {
            switch (band)
            {
                case CoreVoyageTimeBand.Dawn: return "黎明";
                case CoreVoyageTimeBand.Day: return "白昼";
                case CoreVoyageTimeBand.Dusk: return "黄昏";
                case CoreVoyageTimeBand.Night: return "夜航";
                default: return "任意时段";
            }
        }

        private static string WeatherLabel(CoreVoyageWeather weather)
        {
            switch (weather)
            {
                case CoreVoyageWeather.Wind: return "顺风或强风";
                case CoreVoyageWeather.Rain: return "急流天气";
                case CoreVoyageWeather.Fog: return "雾海";
                case CoreVoyageWeather.Storm: return "风暴";
                case CoreVoyageWeather.Calm: return "平静海况";
                default: return "任意天气";
            }
        }

        private static string TerrainLabel(EventTerrainConstraint terrain)
        {
            switch (terrain)
            {
                case EventTerrainConstraint.Beach: return "浅滩";
                case EventTerrainConstraint.Strait: return "海峡";
                case EventTerrainConstraint.Reef: return "礁区";
                case EventTerrainConstraint.Fog: return "雾区";
                case EventTerrainConstraint.Storm: return "风暴区";
                default: return "开放海域";
            }
        }

        /// <summary>Shared by encounter eligibility and UI explanations; unknown roles fail closed.</summary>
        public bool HasEncounterCrewRequirement(string requiredCrew)
        {
            switch (requiredCrew)
            {
                case null:
                case "": return true;
                case "navigator": return HasNavigator;
                case "carpenter": return HasCarpenter;
                case "doctor": return HasDoctor;
                case "gunner": return HasGunner;
                case "cook": return HasCook;
                case "diver": return HasDiver;
                default: return false;
            }
        }

        public bool ResolveChoice(int choiceIndex)
        {
            if (!IsChoiceAvailable(choiceIndex))
            {
                return false;
            }

            CoreEncounterChoice choice = ActiveEncounter.Choices[choiceIndex];
            ConsumeHours(choice.Hours);
            ApplyEffect(choice.StartsBattle ? SplitBattleEffect(choice.Effect, false) : EffectiveChoiceEffect(choiceIndex));
            if (!choice.StartsBattle && ShouldRecordSeaChartClaim(choice)) RecordSeaChartEncounterClaim();
            AddLog("选择「" + choice.Label + "」：" + choice.Detail + "。");
            if (Hull <= 0)
            {
                FinishVoyage(VoyageOutcome.Failure);
                return true;
            }

            if (choice.StartsBattle)
            {
                pendingBattleReward = SplitBattleEffect(choice.Effect, true);
                PrepareSeaChartBattleReward(choice);
                StartBattle(ActiveEncounter, activeNode != null && activeNode.IsBoss);
                return true;
            }

            if (IsTreasureVoyage && choice.Effect.TreasureComplete)
            {
                CompleteEncounter(false);
                TreasureAdventureCompleted = true;
                FinishVoyage(VoyageOutcome.TreasureRecovered);
                return true;
            }
            if (IsTreasureVoyage && choice.Effect.PostponeTreasureCore)
            {
                CompleteEncounter(true);
                return true;
            }
            if (IsTreasureVoyage && choice.Effect.ForceReturn)
            {
                ActiveEncounter = null;
                activeNode = null;
                Phase = CoreDemoPhase.Sailing;
                SafeReturn();
                return true;
            }

            bool completedNavigatorLesson = GuidedTutorialLeg == CoreGuidedTutorialContent.NavigatorVoyageLeg &&
                ActiveEncounter.Id == CoreGuidedTutorialContent.NavigatorEventId;
            if (completedNavigatorLesson && choice.RequiredCrew != "navigator")
            {
                // The zero-cost fallback is a way out of an unaffordable lesson,
                // not a successful interpretation. Keep its node unvisited and
                // use ordinary return/tow rules without advancing the checkpoint.
                AddLog("潮汐暗标尚未解读，保留笔记返港整备；本次不完成航海士教学。");
                ActiveEncounter = null;
                activeNode = null;
                Phase = CoreDemoPhase.Sailing;
                SafeReturn();
                return true;
            }
            CompleteEncounter(IsOptionalBoss(ActiveEncounter));
            // A saved legacy voyage keeps its original one-event itinerary.
            if (completedNavigatorLesson && !UsesCombinedGuidedTutorialVoyage) FinishVoyage(VoyageOutcome.ArrivedAtPort);
            return true;
        }

        public bool SkipEncounter()
        {
            if (Phase != CoreDemoPhase.Encounter || ActiveEncounter == null || ActiveEncounter.Mandatory)
            {
                return false;
            }

            AddLog("略过「" + ActiveEncounter.Name + "」，未推进事件时间。");
            CompleteEncounter(true);
            return true;
        }

        public bool SafeReturn()
        {
            if (Phase != CoreDemoPhase.Sailing)
            {
                return false;
            }

            if (Cargo.Water < 2 || Cargo.Food < 1)
            {
                AddLog("口粮耗尽，请求拖带救援，按失败保护率结算。" );
                FinishVoyage(VoyageOutcome.Failure);
                return true;
            }
            Cargo.Water -= 2;
            Cargo.Food -= 1;
            FinishVoyage(VoyageOutcome.SafeReturn);
            return true;
        }

        public bool CanRequestEmergencyTow()
        {
            if (Phase == CoreDemoPhase.Battle)
                // Tutorial action gating is not a shortage. Only the physical
                // escape ration cost can make this emergency exit available.
                return Battle != null && Battle.Stage != NavalBattleStage.Boarding && Hull > 0 &&
                    (Cargo.Water < RationCostFor(1d) + 2 || Cargo.Food < RationCostFor(1d) + 1);
            if (Phase != CoreDemoPhase.Encounter || ActiveEncounter == null || !ActiveEncounter.Mandatory) return false;
            for (int index = 0; index < ActiveEncounter.Choices.Count; index++)
                if (IsChoiceAvailable(index)) return false;
            return true;
        }

        public bool RequestEmergencyTow()
        {
            if (!CanRequestEmergencyTow()) return false;
            AddLog(Phase == CoreDemoPhase.Battle
                ? "无法承担脱离海战的补给成本，放弃战斗收益并请求拖带救援，按失败保护率结算。"
                : "无法承担强制事件的物资成本，放弃事件收益并请求拖带救援。");
            FinishVoyage(VoyageOutcome.Failure);
            return true;
        }

        public void ReturnToPortAfterSettlement()
        {
            if (Phase != CoreDemoPhase.Settlement || Settlement == null)
            {
                return;
            }

            CommitCollectionVoyageSettlement();
            if (Settlement.Outcome == VoyageOutcome.ArrivedAtPort)
            {
                CurrentPort = Settlement.Destination;
                EnsureSelectedDestination();
            }
            // The protected gain was removed from the aboard purse at settlement.
            // Put it back before depositing, retaining any account-limit excess aboard.
            Cargo.Gold += Settlement.BankedGold;
            CommitTreasureVoyageSettlement();
            if (IsGuidedBossVoyage && Settlement.BankedFragments > 0 &&
                ActiveSeaChartState?.pendingTutorialFragments == 1)
            {
                ActiveSeaChartState.pendingTutorialFragments = 0;
                TouchSeaChartProgress();
            }
            Phase = CoreDemoPhase.Port;
            // Remaining voyage principal is not profit, but must be spendable again in port.
            TryDepositTravelCash();
            Map = null;
            ActiveEncounter = null;
            activeNode = null;
            Battle = null;
            AddLog("已停靠 " + CurrentPort + "，结算物资完成入库。");
        }

        public float ExplorationCoverage()
        {
            if (Map == null)
            {
                return 0f;
            }

            int count = 0;
            for (int index = 0; index < Map.ExploredCells.Length; index += 1)
            {
                if (Map.ExploredCells[index]) count += 1;
            }
            return count / (float)Map.ExploredCells.Length * 100f;
        }

        public int CompletedEventCount()
        {
            if (Map == null) return 0;
            int count = seaChartCompletedRemovedNodes;
            for (int index = 0; index < Map.Nodes.Count; index += 1)
            {
                if (Map.Nodes[index].Visited && !Map.Nodes[index].Skipped) count += 1;
            }
            return count;
        }

        public int TotalEventCount() => Map == null ? 0 : Map.Nodes.Count + seaChartCompletedRemovedNodes;

        public string DirectionLabel(SailingDirection direction)
        {
            switch (direction)
            {
                case SailingDirection.North: return "北";
                case SailingDirection.NorthEast: return "东北";
                case SailingDirection.East: return "东";
                case SailingDirection.SouthEast: return "东南";
                case SailingDirection.South: return "南";
                case SailingDirection.SouthWest: return "西南";
                case SailingDirection.West: return "西";
                case SailingDirection.NorthWest: return "西北";
                default: return "东";
            }
        }

        private string DefaultDestinationFor(string departureName)
        {
            int departureIndex = Array.IndexOf(portNames, departureName);
            if (departureIndex < 0) return portNames[1];
            return portNames[(departureIndex + 1) % portNames.Length];
        }

        private string NormalizeDestination(string departureName, string destinationName)
        {
            if (string.IsNullOrWhiteSpace(destinationName) ||
                Array.IndexOf(portNames, destinationName) < 0 ||
                string.Equals(departureName, destinationName, StringComparison.Ordinal))
            {
                return DefaultDestinationFor(departureName);
            }
            return destinationName;
        }

        private void EnsureSelectedDestination()
        {
            if (selectedSeaChart != null && selectedSeaChart.ContainsPort(CurrentPort))
            {
                SelectedDestinationPort = selectedSeaChart.OtherPort(CurrentPort);
                SelectedRouteIndex = selectedSeaChart.RouteIndex;
                return;
            }
            SelectedDestinationPort = NormalizeDestination(CurrentPort, SelectedDestinationPort);
        }

        private CoreVoyageMap GenerateMap(CoreRouteDefinition route, string seed, string departureName)
        {
            return GenerateMap(route, seed, departureName, DefaultDestinationFor(departureName));
        }

        private CoreVoyageMap GenerateMap(CoreRouteDefinition route, string seed, string departureName, string destinationName, uint? geographyHash = null)
        {
            destinationName = NormalizeDestination(departureName, destinationName);
            uint hash = geographyHash ?? SeedHash.Compute(route.Id + "::" + seed + "::" + departureName + "::" + destinationName);
            Mulberry32Random random = new Mulberry32Random(hash ^ 0x9E3779B9u);

            CoreVoyageMap map = new CoreVoyageMap
            {
                Seed = seed,
                Hash = hash,
                Route = route,
                Departure = new VoyagePoint(9f, 51f),
                Destination = new VoyagePoint(90f, 12f + (float)Math.Round(random.NextDouble() * 34d)),
                DepartureName = departureName,
                DestinationName = destinationName,
                CurrentPhaseA = random.NextDouble() * Math.PI * 2d,
                CurrentPhaseB = random.NextDouble() * Math.PI * 2d,
            };
            double windAngle = random.NextDouble() * Math.PI * 2d;
            map.GlobalWindVector = new VoyagePoint((float)Math.Cos(windAngle), (float)Math.Sin(windAngle));
            map.GlobalWindDirection = ClosestDirection(windAngle);
            map.GlobalWindStrength = 5f + (float)random.NextDouble() * 8f;
            map.BossExclusionRadius = BossTenDayRadiusMapUnits(route);

            IReadOnlyList<CoreEncounter> encounterPool = CreateEncounterPool(route);
            List<CoreEncounter> encounters = new List<CoreEncounter>(encounterPool.Count);
            HashSet<string> encounterIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < encounterPool.Count; index += 1)
            {
                CoreEncounter candidate = encounterPool[index];
                if (candidate != null && encounterIds.Add(candidate.Id))
                {
                    encounters.Add(candidate);
                }
            }
            for (int index = encounters.Count - 1; index > 0; index -= 1)
            {
                int swap = (int)(random.NextDouble() * (index + 1));
                CoreEncounter held = encounters[index];
                encounters[index] = encounters[swap];
                encounters[swap] = held;
            }

            for (int pointIndex = 0; pointIndex < encounters.Count; pointIndex += 1)
            {
                CoreEncounter encounter = encounters[pointIndex];
                map.Nodes.Add(new CoreMapNode
                {
                    Id = pointIndex,
                    Encounter = encounter,
                    TerrainConstraint = TerrainConstraintFor(encounter, false),
                });
            }

            map.Nodes.Add(new CoreMapNode
            {
                Id = encounters.Count,
                Encounter = CreateBossEncounter(route),
                IsBoss = true,
                TerrainConstraint = EventTerrainConstraint.Storm,
            });
            CoreLegendaryWarships.AddThirdChartEncounters(map);
            VoyageMapGenerator.Generate(
                map,
                route.MinimumEventSpacing,
                BossBorderMarginMapUnits,
                map.BossExclusionRadius,
                BossPortClearanceMapUnits,
                BossPortApproachRadiusMapUnits);
            GenerateTerrainCurrents(map, new Mulberry32Random(hash ^ 0x6A09E667u));
            return map;
        }

        /// <summary>
        /// Converts ten days of intended route progress into chart units. The
        /// recommended voyage duration includes navigation and encounter pacing,
        /// unlike a physically impossible 24-hours-at-top-speed conversion.
        /// </summary>
        public static float BossTenDayRadiusMapUnits(CoreRouteDefinition route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            float voyageDays = Math.Max(BossMinimumSailingDays, route.RecommendedVoyageDays);
            return CoreVoyageMap.Width * BossMinimumSailingDays / voyageDays;
        }

        private static EventTerrainConstraint TerrainConstraintFor(CoreEncounter encounter, bool isBoss)
        {
            if (isBoss) return EventTerrainConstraint.Storm;
            string id = encounter == null ? string.Empty : encounter.Id;
            // Event terrain is authored from the scene description where the
            // generated node is placed. Hidden is a discovery flag, not a
            // weather type, so explicit cases must win over the old fallback.
            if (id.IndexOf("storm_debris", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Storm;
            if (id.IndexOf("strait_beacon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("smuggler_jetty", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Strait;
            if (id.IndexOf("reef_snare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("coral_maze", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("brine_geyser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("false_cache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("reef_divers", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("hidden_cache", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Reef;
            if (id.IndexOf("fogbell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("siren_conch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("whisper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("moon_ghost", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Fog;
            if (id.IndexOf("gold_customs", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.OpenSea;
            if (id.IndexOf("obelisk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("drowned_shrine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("pearl_crab", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Beach;
            if (id.IndexOf("broken_mast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("saltwater_leak", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Reef;
            if (id.IndexOf("whale_song", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.OpenSea;
            if (id.IndexOf("random_ambush", StringComparison.OrdinalIgnoreCase) >= 0) return EventTerrainConstraint.Reef;
            if (id.IndexOf("shortcut", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("strait", StringComparison.OrdinalIgnoreCase) >= 0)
                return EventTerrainConstraint.Strait;
            if (id.IndexOf("wreck", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("sunken", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("fisher", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("castaway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("beach", StringComparison.OrdinalIgnoreCase) >= 0)
                return EventTerrainConstraint.Beach;
            if (encounter != null && encounter.Hidden) return EventTerrainConstraint.Fog;
            if (IsBattleTerrain(encounter)) return EventTerrainConstraint.Reef;
            return EventTerrainConstraint.OpenSea;
        }

        private static void GenerateTerrainCurrents(CoreVoyageMap map, Mulberry32Random random)
        {
            float routeX = map.Destination.X - map.Departure.X;
            float routeY = map.Destination.Y - map.Departure.Y;
            Normalize(ref routeX, ref routeY);
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    int index = row * CoreVoyageMap.GridColumns + column;
                    if (!map.IsPassable(column, row))
                    {
                        map.CurrentVectors[index] = new VoyagePoint(0f, 0f);
                        map.CurrentStrengths[index] = 0f;
                        continue;
                    }

                    float repelX = (map.IsPassable(column - 1, row) ? 0f : 1f) - (map.IsPassable(column + 1, row) ? 0f : 1f);
                    float repelY = (map.IsPassable(column, row - 1) ? 0f : 1f) - (map.IsPassable(column, row + 1) ? 0f : 1f);
                    VoyagePoint center = map.CellCenter(column, row);
                    float curve = (float)Math.Sin(center.X * 0.13d + center.Y * 0.19d + map.CurrentPhaseA) * 0.48f;
                    float x = routeX * 0.66f - routeY * curve + repelX * 0.72f;
                    float y = routeY * 0.66f + routeX * curve + repelY * 0.72f;
                    Normalize(ref x, ref y);
                    map.CurrentVectors[index] = new VoyagePoint(x, y);
                    float terrainBonus = map.TerrainCells[index] == SeaTerrainType.Strait ? 0.8f : 0f;
                    map.CurrentStrengths[index] = 0.65f + (float)random.NextDouble() * 1.55f + terrainBonus;
                }
            }
        }

        private static void Normalize(ref float x, ref float y)
        {
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length < 0.0001f) { x = 1f; y = 0f; return; }
            x /= length;
            y /= length;
        }

        private static void PointToCell(VoyagePoint point, out int column, out int row)
        {
            column = Math.Max(0, Math.Min(CoreVoyageMap.GridColumns - 1, (int)(point.X / CoreVoyageMap.Width * CoreVoyageMap.GridColumns)));
            row = Math.Max(0, Math.Min(CoreVoyageMap.GridRows - 1, (int)(point.Y / CoreVoyageMap.Height * CoreVoyageMap.GridRows)));
        }

        private static bool IsBattleTerrain(CoreEncounter encounter)
        {
            return encounter != null &&
                (encounter.Power > 0 ||
                 encounter.Category.IndexOf("战", StringComparison.Ordinal) >= 0 ||
                 encounter.Id.IndexOf("ambush", StringComparison.Ordinal) >= 0);
        }

        private IReadOnlyList<CoreEncounter> CreateEncounterPool(CoreRouteDefinition route)
        {
            if (route.Id == routes[1].Id)
            {
                return AddCommonEncounters(new[]
                {
                    new CoreEncounter("gold_convoy", "无旗商队", "贸易", "一支没有悬挂旗号的商队等待交换补给。", false, false, 1, 0,
                        new CoreEncounterChoice("出售木材", "木材 -4、金币 +36", 1, new VoyageEffect { Wood = -4, Gold = 36 }, renewable: true),
                        new CoreEncounterChoice("购买食物", "海上应急补给：金币 -12、食物 +45", 1, new VoyageEffect { Gold = -12, Food = 45 }, renewable: true,
                            requiredShip: "merchant|provision"))
                        { RepeatPolicy = CoreEncounterRepeatPolicy.Renewable },
                    new CoreEncounter("gold_customs", "帝国临检船", "强制事件", "帝国巡逻舰要求检查货舱与航行许可。", true, false, 2, 115,
                        new CoreEncounterChoice("缴纳通行费", "金币 -30、声望 +4", 2, new VoyageEffect { Gold = -30, Fame = 4 }),
                        // Mandatory encounters keep an unconditional combat
                        // fallback so a low-cash sloop can never be soft-locked.
                        new CoreEncounterChoice("拒绝检查", "胜利后金币 +70，另计海战奖励", 1, new VoyageEffect { Gold = 70 }, startsBattle: true)),
                    new CoreEncounter("gold_wreck", "香料沉船", "打捞", "破裂的商船货舱仍漂着可出售的货箱。", false, true, 2, 0,
                        new CoreEncounterChoice("快速打捞", "金币 +75、船体 -3", 3, new VoyageEffect { Gold = 75, Hull = -3 },
                            requiredShip: "salvage"),
                        new CoreEncounterChoice("木匠加固后打捞", "木材 +8、金币 +35", 4, new VoyageEffect { Wood = 8, Gold = 35 }, "carpenter")),
                });
            }

            if (route.Id == routes[2].Id)
            {
                return AddCommonEncounters(new[]
                {
                    new CoreEncounter("moon_obelisk", "月痕方碑", "遗迹", "潮水退去，刻满旧王国星位的方碑露出海面。", false, false, 2, 0,
                        new CoreEncounterChoice("记录星位", "声望 +8、显示 2 个隐藏点", 3, new VoyageEffect { Fame = 8, Reveal = 2 }),
                        new CoreEncounterChoice("撬取金属", "铁矿 +10、船体 -4", 4, new VoyageEffect { Iron = 10, Hull = -4 },
                            requiredShip: "salvage")),
                    new CoreEncounter("moon_whisper", "深海低语", "强制事件", "雾中传来的低语令水手失去方向感。", true, true, 3, 0,
                        new CoreEncounterChoice("保持航向", "士气 -1", 4, new VoyageEffect { Morale = -1 }),
                        new CoreEncounterChoice("航海士校准", "显示 3 个隐藏点", 2, new VoyageEffect { Reveal = 3 }, "navigator",
                            requiredTime: CoreVoyageTimeBand.Night)),
                    new CoreEncounter("moon_ghost", "无灯幽舰", "战斗", "一艘没有灯火的古船正横切航迹。", true, false, 3, 155,
                        new CoreEncounterChoice("进入战斗", "击退幽舰后金币 +90，另计海战奖励", 1, new VoyageEffect { Gold = 90 }, startsBattle: true)),
                });
            }

            return AddCommonEncounters(new[]
            {
                new CoreEncounter("event_reef_sharp_current", "暗流推船", "路线", "暗流从船底托起船身，水面漂着古航图的盐封残片。逆流搜索可找回一片藏宝线索。", false, false, 2, 0,
                    new CoreEncounterChoice("逆流搜索", "藏宝图碎片 +1", 4, new VoyageEffect { MapFragments = 1 }),
                    new CoreEncounterChoice("沿流测绘", "显示 1 个隐藏点", 1, new VoyageEffect { Reveal = 1 }, "navigator")),
                new CoreEncounter("event_reef_hidden_cache", "潮痕秘藏", "宝藏", "退潮刻痕指向碎礁石缝；获得情报或贴近航过后，可记录失落宝库的坐标。", false, true, 2, 0,
                    new CoreEncounterChoice("记录藏宝坐标", "藏宝图碎片 +1、声望 +8", 2, new VoyageEffect { MapFragments = 1, Fame = 8 }),
                    new CoreEncounterChoice("撬开石匣", "木材 -6、金币 +120", 5, new VoyageEffect { Wood = -6, Gold = 120 },
                        requiredShip: "salvage")),
                new CoreEncounter("event_reef_broken_mast", "断桅求援", "事件", "一艘断桅小船卡在礁石边缘，水手们用破布挥舞求援。", false, false, 1, 0,
                    new CoreEncounterChoice("救援", "木材 -6、声望 +15", 4, new VoyageEffect { Wood = -6, Fame = 15 }),
                    new CoreEncounterChoice("掠夺", "啤酒 -1、金币 +80、士气 -1", 3, new VoyageEffect { Beer = -1, Gold = 80, Morale = -1 }),
                    new CoreEncounterChoice("木匠处理", "布料 +8、船体 +4", 3, new VoyageEffect { Cloth = 8, Hull = 4 }, "carpenter")),
                new CoreEncounter("event_reef_shortcut", "礁间近路", "强制事件", "两排黑色礁牙之间露出一条窄道，潮水正在迅速回落。", true, true, 2, 75,
                    new CoreEncounterChoice("绕路", "安全通过，主要成本来自时间", 2, new VoyageEffect()),
                    new CoreEncounterChoice("强穿", "船体 -10", 1, new VoyageEffect { Hull = -10 },
                        requiredWeather: CoreVoyageWeather.Rain),
                    new CoreEncounterChoice("航海士领航", "显示 2 个隐藏点", 0.5, new VoyageEffect { Reveal = 2 }, "navigator")),
                new CoreEncounter("event_reef_sunken_boat", "半沉小船", "打捞", "一艘半沉小船卡在浅滩，货舱仍有完整木料。", false, false, 1, 0,
                    new CoreEncounterChoice("浅层打捞", "木材 +12", 3, new VoyageEffect { Wood = 12 }),
                    new CoreEncounterChoice("深入搜索", "铁矿 +6、金币 +25、船体 -4", 5, new VoyageEffect { Iron = 6, Gold = 25, Hull = -4 },
                        requiredShip: "salvage")),
                new CoreEncounter("event_reef_saltwater_leak", "舱底进水", "强制事件", "船舱底部传来湿木裂响，水线正在慢慢升高。", true, true, 2, 0,
                    new CoreEncounterChoice("立即修补", "木材 -4、船体 +8", 3, new VoyageEffect { Wood = -4, Hull = 8 }, renewable: true),
                    new CoreEncounterChoice("木匠修补", "木材 -2、船体 +10", 2, new VoyageEffect { Wood = -2, Hull = 10 }, "carpenter", renewable: true),
                    new CoreEncounterChoice("弃舱封堵", "船体 -6，不消耗物资", 0, new VoyageEffect { Hull = -6 }))
                    { RepeatPolicy = CoreEncounterRepeatPolicy.Renewable },
                new CoreEncounter("event_reef_fisher_cache", "海民储藏箱", "补给", "礁石背面绑着一只海民留下的防水交易箱。", false, false, 1, 0,
                    new CoreEncounterChoice("购买补给", "海上应急补给：金币 -2、食物 +6", 1, new VoyageEffect { Gold = -2, Food = 6 }, renewable: true),
                    new CoreEncounterChoice("偷拿", "食物 +4、声望 -10", 2, new VoyageEffect { Food = 4, Fame = -10 })),
                new CoreEncounter("event_reef_coral_ambush", "珊瑚伏击", "战斗", "珊瑚后升起几面破帆，掠夺者早已等在浅水里。", true, false, 3, 95,
                    new CoreEncounterChoice("躲入礁缝", "船体 -4", 2, new VoyageEffect { Hull = -4 }),
                    new CoreEncounterChoice("迎战", "战斗胜利金币 +110", 2, new VoyageEffect { Gold = 110 }, startsBattle: true,
                        requiredShip: "patrol|gunship|privateer")),
            });
        }

        private static IReadOnlyList<CoreEncounter> AddCommonEncounters(CoreEncounter[] routeEncounters)
        {
            List<CoreEncounter> encounters = new List<CoreEncounter>(routeEncounters ?? Array.Empty<CoreEncounter>());
            encounters.Add(new CoreEncounter("common_beach_castaway", "潮滩漂流者", "海滩事件", "退潮后的沙洲上有人点燃求救烟火。", false, false, 1, 0,
                new CoreEncounterChoice("接回船上", "声望 +6、食物 -3", 2, new VoyageEffect { Fame = 6, Food = -3 }),
                new CoreEncounterChoice("留下补给", "食物 -5、声望 +10", 1, new VoyageEffect { Food = -5, Fame = 10 })));
            encounters.Add(new CoreEncounter("common_strait_beacon", "峡湾引航灯", "峡谷事件", "两座岛礁间的旧引航灯仍在随潮闪烁。", false, false, 1, 0,
                new CoreEncounterChoice("校准灯塔", "声望 +5、显示 1 个隐藏点", 2, new VoyageEffect { Fame = 5, Reveal = 1 }),
                new CoreEncounterChoice("拆取灯具", "铁矿 +5", 2, new VoyageEffect { Iron = 5 },
                    requiredTime: CoreVoyageTimeBand.Dusk)));
            encounters.Add(new CoreEncounter("common_floating_market", "漂泊市集", "贸易", "几艘平底船连成临时市集，商人们正在收帆。", false, false, 1, 0,
                new CoreEncounterChoice("购买补给", "海上应急补给：金币 -7、食物 +24", 1, new VoyageEffect { Gold = -7, Food = 24 }, renewable: true),
                new CoreEncounterChoice("出售布料", "布料 -5、金币 +32", 1, new VoyageEffect { Cloth = -5, Gold = 32 }, renewable: true))
                { RepeatPolicy = CoreEncounterRepeatPolicy.Renewable });
            encounters.Add(new CoreEncounter("common_whale_song", "鲸歌航道", "奇遇", "深蓝水面下传来成群鲸兽的低沉歌声。", false, true, 1, 0,
                new CoreEncounterChoice("随鲸群航行", "士气 +1、声望 +4", 3, new VoyageEffect { Morale = 1, Fame = 4 }),
                new CoreEncounterChoice("记录迁徙", "显示 2 个隐藏点", 2, new VoyageEffect { Reveal = 2 })));
            encounters.Add(new CoreEncounter("common_storm_debris", "风暴残骸", "风暴事件", "破碎桅杆在雷云下随浪翻滚。", true, false, 2, 0,
                new CoreEncounterChoice("减帆穿越", "船体 -3", 3, new VoyageEffect { Hull = -3 }),
                new CoreEncounterChoice("木匠加固", "木材 -2、船体 +4", 2, new VoyageEffect { Wood = -2, Hull = 4 }, "carpenter",
                    renewable: true, requiredWeather: CoreVoyageWeather.Storm))
                { RepeatPolicy = CoreEncounterRepeatPolicy.Renewable });
            encounters.Add(new CoreEncounter("common_reef_divers", "珊瑚采珠人", "礁区事件", "采珠人的小艇停在礁盘背风面。", false, false, 1, 0,
                new CoreEncounterChoice("交换药材", "药品 -1、金币 +28", 2, new VoyageEffect { Medicine = -1, Gold = 28 }, renewable: true,
                    requiredWeather: CoreVoyageWeather.Calm),
                new CoreEncounterChoice("协助打捞", "金币 +18、声望 +5", 3, new VoyageEffect { Gold = 18, Fame = 5 })));
            return encounters.AsReadOnly();
        }

        private CoreEncounter CreateBossEncounter(CoreRouteDefinition route)
        {
            return new CoreEncounter("boss_" + route.Id, route.BossName, "区域首领", "远海首领舰盘踞在十日航程之外，控制着通往新港的关键海域。", true, false, 4, route.BossPower,
                new CoreEncounterChoice("挑战首领", "胜利后开放新港航道", 1,
                    new VoyageEffect { Gold = 180, Fame = 20 }, startsBattle: true));
        }

        private CoreEncounter CreateRandomAmbush(bool detected)
        {
            if (detected)
            {
                return new CoreEncounter("random_ambush_warning", "航路伏击预警", "随机事件", "航海士提前发现敌船，可以绕开或抢先接战。", false, true, 2, Map.Route.RecommendedPower,
                    new CoreEncounterChoice("改变航向", "避开本次伏击", 0, new VoyageEffect()),
                    new CoreEncounterChoice("抢先开火", "胜利后金币 +65，另计海战奖励", 1,
                        new VoyageEffect { Gold = 65 }, startsBattle: true));
            }

            return new CoreEncounter("random_ambush", "航路伏击", "随机战斗", "敌船从礁影后突然出现，已经无法安全脱离。", true, true, 3, Map.Route.RecommendedPower,
                new CoreEncounterChoice("正面迎战", "战斗胜利金币 +80，另计海战奖励", 1,
                    new VoyageEffect { Gold = 80 }, startsBattle: true));
        }

        private SailingForecast CreateForecast(VoyagePoint start, VoyagePoint target, float actualDistanceNm, bool clipped)
        {
            float courseX = target.X - start.X;
            float courseY = target.Y - start.Y;
            double headingAngle = courseX * courseX + courseY * courseY > 0.0001f
                ? Math.Atan2(courseY, courseX)
                : DirectionAngle(Heading);
            SeaCondition condition = GetSeaConditionAt(start);
            double currentProjection = condition.CurrentVector.X * Math.Cos(headingAngle) + condition.CurrentVector.Y * Math.Sin(headingAngle);
            double windProjection = condition.WindVector.X * Math.Cos(headingAngle) + condition.WindVector.Y * Math.Sin(headingAngle);
            float currentModifier = condition.CurrentStrength * 0.72f * (float)currentProjection;
            float windModifier = condition.WindStrength * 0.28f * (float)windProjection;
            float hullModifier = -(1f - Hull / (float)MaxHull) * 3.5f;
            float speed = Math.Max(2.5f, EffectiveSpeedKnots + (HasNavigator ? 1f : 0f) + currentModifier + windModifier + hullModifier);
            double hours = actualDistanceNm / speed;
            int rationCost = RationCostFor(hours);
            string currentRelation = currentProjection > 0.25d ? "顺流" : currentProjection < -0.25d ? "逆流" : "横流";
            string windRelation = windProjection > 0.25d ? "顺风" : windProjection < -0.25d ? "逆风" : "侧风";
            return new SailingForecast
            {
                Start = start,
                Target = target,
                PlannedDistanceNm = PlannedDistanceNm,
                ActualDistanceNm = actualDistanceNm,
                EffectiveSpeedKnots = speed,
                Hours = hours,
                WaterCost = rationCost,
                FoodCost = rationCost,
                CurrentStrength = condition.CurrentStrength,
                CurrentRelation = currentRelation,
                CurrentDirection = condition.CurrentDirection,
                CurrentSpeedModifier = currentModifier,
                WindStrength = condition.WindStrength,
                WindRelation = windRelation,
                WindDirection = condition.WindDirection,
                WindSpeedModifier = windModifier,
                Clipped = clipped,
            };
        }

        private void ConsumeHours(double hours)
        {
            AdvanceGameClock(hours);
            int cost = RationCostFor(hours);
            rationProgress += EffectiveRationCrewCount() * Math.Max(0d, hours) / 24d;
            Cargo.Water = Math.Max(0, Cargo.Water - cost);
            Cargo.Food = Math.Max(0, Cargo.Food - cost);
            TotalHours += Math.Max(0d, hours);
        }

        private int RationCostFor(double hours)
        {
            // Prepay the first fractional ration of a voyage. Later actions use
            // the cumulative difference, not a separate round-up per click.
            // Repeated zero-supply departures can no longer sail for free before
            // requesting rescue; splitting an equal duration remains equal cost.
            // The epsilon removes floating-point noise at exact ration boundaries.
            const double roundingEpsilon = 1e-9d;
            double before = Math.Ceiling(rationProgress - roundingEpsilon);
            double after = Math.Ceiling(rationProgress + EffectiveRationCrewCount() * Math.Max(0d, hours) / 24d - roundingEpsilon);
            return (int)Math.Min(int.MaxValue, Math.Max(0d, after - before));
        }

        private double EffectiveRationCrewCount()
        {
            return EstimateDailyRationConsumption(EffectiveCrewCount, HasCook);
        }

        public double WaterSupplyHours => RemainingSupplyHours(Cargo.Water);
        public double FoodSupplyHours => RemainingSupplyHours(Cargo.Food);

        private double RemainingSupplyHours(int stock)
        {
            double dailyConsumption = EffectiveRationCrewCount();
            if (dailyConsumption <= 0d) return double.PositiveInfinity;
            // A fractional ration is charged up front by RationCostFor. Its
            // unconsumed portion still supports the crew, even at zero stock.
            double prepaidRations = Math.Ceiling(rationProgress - 1e-9d) - rationProgress;
            return Math.Max(0d, Math.Max(0, stock) + prepaidRations) * 24d / dailyConsumption;
        }

        public double EstimateDailyRationConsumption(int crewCount, bool cook)
        {
            return Math.Max(0, Math.Min(CrewCountValue, crewCount)) *
                (cook ? GameplayBalance.CookRationMultiplier : 1d) * componentContextRationMultiplier;
        }

        private void ApplyEffect(VoyageEffect effect)
        {
            int medicineEffect = effect.CrewBonusesApplied ? effect.Medicine : AdjustedMedicineEffect(effect.Medicine);
            long woodEffect = effect.CrewBonusesApplied ? effect.Wood : AdjustedSalvageEffect(effect.Wood);
            long ironEffect = effect.CrewBonusesApplied ? effect.Iron : AdjustedSalvageEffect(effect.Iron);
            Hull = (int)Math.Max(0L, Math.Min(MaxHull, (long)Hull + effect.Hull + (!effect.CrewBonusesApplied && effect.Hull > 0 && HasDoctor ? 2 : 0)));
            Morale = (int)Math.Max(0L, Math.Min(5L, (long)Morale + effect.Morale));
            Cargo.Water = Math.Max(0, Cargo.Water + effect.Water);
            Cargo.Food = Math.Max(0, Cargo.Food + effect.Food);
            Cargo.Beer = Math.Max(0, Cargo.Beer + effect.Beer);
            Cargo.Medicine = Math.Max(0, Cargo.Medicine + medicineEffect);
            Cargo.Wood = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)Cargo.Wood + woodEffect));
            Cargo.Iron = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)Cargo.Iron + ironEffect));
            Cargo.Cloth = Math.Max(0, Cargo.Cloth + effect.Cloth);
            Cargo.Powder = Math.Max(0, Cargo.Powder + effect.Powder);
            Cargo.Cannonballs = Math.Max(0, Cargo.Cannonballs + effect.Cannonballs);
            Cargo.Gold = Math.Max(0, Cargo.Gold + effect.Gold);
            Cargo.Fame = Math.Max(0, Cargo.Fame + effect.Fame);
            ApplyTreasureEffect(effect);
            if (effect.Reveal > 0) RevealNearestHidden(effect.Reveal);
        }

        private bool CanAfford(VoyageEffect effect, int elapsedRations = 0)
        {
            bool Fits(long current, long delta) => current >= 0 && current + delta >= 0 && current + delta <= int.MaxValue;
            // Time is paid before the effect, exactly as ResolveChoice applies it.
            // A promised food reward cannot fund its own waiting time.
            return Fits((long)Cargo.Water - elapsedRations, effect.Water) && Fits((long)Cargo.Food - elapsedRations, effect.Food) && Fits(Cargo.Beer, effect.Beer) &&
                Fits(Cargo.Medicine, effect.CrewBonusesApplied ? effect.Medicine : AdjustedMedicineEffect(effect.Medicine)) &&
                Fits(Cargo.Wood, effect.CrewBonusesApplied ? effect.Wood : AdjustedSalvageEffect(effect.Wood)) &&
                Fits(Cargo.Iron, effect.CrewBonusesApplied ? effect.Iron : AdjustedSalvageEffect(effect.Iron)) && Fits(Cargo.Cloth, effect.Cloth) &&
                Fits(Cargo.Powder, effect.Powder) && Fits(Cargo.Cannonballs, effect.Cannonballs) &&
                Fits(Cargo.Gold, effect.Gold) && Fits(Cargo.Fame, effect.Fame) && CanAffordTreasureEffect(effect);
        }

        private float CargoVolumeDelta(VoyageEffect effect)
        {
            long wood = effect.CrewBonusesApplied ? effect.Wood : AdjustedSalvageEffect(effect.Wood);
            long iron = effect.CrewBonusesApplied ? effect.Iron : AdjustedSalvageEffect(effect.Iron);
            return effect.Water * 0.07f + effect.Food * 0.09f + effect.Beer * 0.08f +
                (effect.CrewBonusesApplied ? effect.Medicine : AdjustedMedicineEffect(effect.Medicine)) * 0.04f + wood + iron * 2f +
                effect.Cloth + effect.Powder * 0.5f + effect.Cannonballs + TreasureCargoVolumeDelta(effect);
        }

        private int AdjustedMedicineEffect(int amount)
        {
            return amount < 0 && HasDoctor ? -(int)Math.Ceiling(-(double)amount * 0.75d) : amount;
        }

        private long AdjustedSalvageEffect(int amount)
        {
            return amount > 0 && HasDiver ? (long)amount + Math.Max(1, amount / 4) : amount;
        }

        private void CompleteEncounter(bool skipped)
        {
            RecordGuidedTutorialEncounterCompleted(ActiveEncounter);
            if (activeNode != null)
            {
                activeNode.Visited = !(skipped && CanRevisitEncounter(activeNode.Encounter));
                activeNode.Skipped = skipped;
            }
            ActiveEncounter = null;
            activeNode = null;
            Phase = CoreDemoPhase.Sailing;
        }

        private static bool IsOptionalBoss(CoreEncounter encounter) => encounter != null &&
            (encounter.BossKind == CoreBossKind.Treasure || encounter.BossKind == CoreBossKind.Legendary);

        private static bool CanRevisitEncounter(CoreEncounter encounter) => IsOptionalBoss(encounter) ||
            encounter != null && encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId;

        private void Shuffle(List<int> values)
        {
            for (int index = values.Count - 1; index > 0; index -= 1)
            {
                int swap = (int)(voyageRandom.NextDouble() * (index + 1));
                int value = values[index];
                values[index] = values[swap];
                values[swap] = value;
            }
        }

        private void WinBattle()
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null) return;
            RestoreGuidedBossSupplies();
            bool guidedBoss = IsGuidedBossVoyage &&
                ActiveEncounter?.Id == CoreGuidedTutorialContent.BossEventId;
            if (guidedBoss)
                GuidedTutorialBossDefeated = true;
            bool wasBoss = Battle.IsBoss;
            CoreBossKind defeatedKind = Battle.Profile != null ? Battle.Profile.BossKind :
                (ActiveEncounter != null ? ActiveEncounter.BossKind : (wasBoss ? CoreBossKind.Route : CoreBossKind.None));
            bool captured = Battle.Stage == NavalBattleStage.Boarding;
            LastBattleWasCaptured = captured;
            Battle.LastAction = captured ? "敌方船员投降，成功夺船。" : "敌舰沉没。";
            if (HasCarpenter)
            {
                Hull = Math.Min(MaxHull, Hull + 2);
            }
            // Combat must finish even when an old save has filled an integer purse.
            // Preserve all existing assets, credit what can be represented, and name
            // any uncredited excess explicitly instead of wrapping or losing the fight.
            int Receive(int current, long amount, string resource)
            {
                long total = (long)current + amount;
                if (total > int.MaxValue)
                    AddLog("战利品数量上限：" + resource + " " + (total - int.MaxValue) + " 未能入账；已有余额保留。");
                return (int)Math.Max(0L, Math.Min(int.MaxValue, total));
            }
            long Salvage(int amount) => amount > 0 && HasDiver ? (long)amount + Math.Max(1, amount / 4) : amount;
            long BoardingLoot(long amount) => ScaleSeaChartBattleReward(captured && amount > 0 ?
                (long)Math.Ceiling(amount * (BoardingLootMultiplierPercent / 100d)) : amount);
            long gold = BoardingLoot((wasBoss ? CoreBattleBalance.BossVictoryGold : 45) + (long)(pendingBattleReward?.Gold ?? 0));
            long fame = BoardingLoot((wasBoss ? CoreBattleBalance.BossVictoryFame : 6) + (long)(pendingBattleReward?.Fame ?? 0));
            if (pendingBattleReward != null)
            {
                VoyageEffect reward = pendingBattleReward;
                Hull = (int)Math.Max(0L, Math.Min(MaxHull, (long)Hull + ScaleSeaChartBattleReward(reward.Hull + (reward.Hull > 0 && HasDoctor ? 2 : 0))));
                Morale = (int)Math.Max(0L, Math.Min(5L, (long)Morale + ScaleSeaChartBattleReward(reward.Morale)));
                Cargo.Water = Receive(Cargo.Water, BoardingLoot(reward.Water), "淡水");
                Cargo.Food = Receive(Cargo.Food, BoardingLoot(reward.Food), "口粮");
                Cargo.Beer = Receive(Cargo.Beer, BoardingLoot(reward.Beer), "啤酒");
                Cargo.Medicine = Receive(Cargo.Medicine, BoardingLoot(AdjustedMedicineEffect(reward.Medicine)), "药品");
                Cargo.Wood = Receive(Cargo.Wood, BoardingLoot(Salvage(reward.Wood)), "木材");
                Cargo.Iron = Receive(Cargo.Iron, BoardingLoot(Salvage(reward.Iron)), "铁矿");
                Cargo.Cloth = Receive(Cargo.Cloth, BoardingLoot(reward.Cloth), "帆布");
                Cargo.Powder = Receive(Cargo.Powder, BoardingLoot(reward.Powder), "火药");
                Cargo.Cannonballs = Receive(Cargo.Cannonballs, BoardingLoot(reward.Cannonballs), "炮弹");
                if (reward.Reveal > 0) RevealNearestHidden((int)ScaleSeaChartBattleReward(reward.Reveal));
                // Treasure resources obey the same delayed victory-only reward boundary.
                ApplyTreasureBattleReward(reward, captured);
            }
            int previousGold = Cargo.Gold, previousFame = Cargo.Fame;
            Cargo.Gold = Receive(Cargo.Gold, gold, "金币");
            Cargo.Fame = Receive(Cargo.Fame, fame, "声望");
            gold = (long)Cargo.Gold - previousGold;
            fame = (long)Cargo.Fame - previousFame;
            if (captured)
            {
                pendingBoardingComponentSlot = -1;
                pendingBoardingComponentDrop = pendingSeaChartRewardMultiplier > 0d &&
                    (voyageRandom == null || voyageRandom.NextDouble() < BoardingComponentDropChance * pendingSeaChartRewardMultiplier);
                if (pendingBoardingComponentDrop)
                {
                    int slotCount = ComponentSlotCountForLoot();
                    int anchorSlot = voyageRandom == null ? 0 : (int)(voyageRandom.NextDouble() * slotCount);
                    int offset = voyageRandom == null ? 1 : 1 + (int)(voyageRandom.NextDouble() * (slotCount - 1));
                    pendingBoardingComponentSlot = (anchorSlot + offset) % slotCount;
                }
            }
            else
            {
                pendingBoardingComponentDrop = false;
                pendingBoardingComponentSlot = -1;
            }
            pendingBattleReward = null;
            // Optional treasure and legendary victories never unlock the route's port.
            bossDefeated |= defeatedKind == CoreBossKind.Route;
            AddLog((captured ? "夺船胜利" : "击沉敌舰") + "：金币 +" + gold + "、声望 +" + fame +
                (captured ? "（战利品×1.5）" : string.Empty) + "。");
            if (wasBoss && (guidedBoss || bossTreasureRandom.NextDouble() < CoreBattleBalance.BossTreasureFragmentDropChance * pendingSeaChartRewardMultiplier))
            {
                // This additional drop is exactly one fragment for either
                // victory type; boarding multipliers apply to other loot only.
                int fragmentsBefore = voyageTreasureFragments;
                voyageTreasureFragments = Receive(voyageTreasureFragments, 1, "藏宝图碎片");
                if (voyageTreasureFragments > fragmentsBefore)
                {
                    AddLog((guidedBoss ? "新手首领必定掉落" : "首领掉落") + "：藏宝图碎片 +1（返航后入账）。");
                    if (guidedBoss && activeSeaChart?.TutorialWaters == true && ActiveSeaChartState != null)
                        ActiveSeaChartState.pendingTutorialFragments = 1;
                }
            }
            RecordCollectionBattleVictory();
            RecordSeaChartBattleVictory(wasBoss || defeatedKind != CoreBossKind.None);
            Battle = null;
            CompleteEncounter(false);
            if (ActiveSeaChartState != null) RemoveDefeatedSeaChartBosses(ActiveSeaChartState, true);
        }

        private static int ComponentSlotCountForLoot() => 5;

        private void FinishVoyage(VoyageOutcome outcome)
        {
            RestoreGuidedBossSupplies();
            float protection = outcome == VoyageOutcome.ArrivedAtPort || outcome == VoyageOutcome.TreasureRecovered ? 0.95f : outcome == VoyageOutcome.SafeReturn ? 0.85f : 0.35f;
            string title = outcome == VoyageOutcome.TreasureRecovered ? "秘藏探索完成" : outcome == VoyageOutcome.ArrivedAtPort ? "抵达新港" : outcome == VoyageOutcome.SafeReturn ? "安全返航" : "航行失败";
            int completed = CompletedEventCount();
            VoyageCargo starting = departureCargo ?? SnapshotCargo(Cargo);
            int earnedGold = Math.Max(0, Cargo.Gold - starting.Gold);
            int wood = ProtectedGain(Cargo.Wood, starting.Wood, protection);
            int iron = ProtectedGain(Cargo.Iron, starting.Iron, protection);
            int fame = ProtectedGain(Cargo.Fame, starting.Fame, protection);
            Settlement = new VoyageSettlement
            {
                Outcome = outcome,
                Title = title,
                Destination = outcome == VoyageOutcome.ArrivedAtPort ? Map.DestinationName : Map.DepartureName,
                ProtectionRatio = protection,
                DistanceNm = TotalDistanceNm,
                Hours = TotalHours,
                Coverage = ExplorationCoverage(),
                CompletedEvents = completed,
                TotalEvents = TotalEventCount(),
                BankedGold = (int)Math.Floor(earnedGold * protection),
                BankedWood = wood,
                BankedIron = iron,
                BankedFame = fame,
                ChartSaved = CanDrawSeaChart && outcome != VoyageOutcome.Failure,
                BankedFragments = voyageTreasureFragments,
                BankedPirateExperience = VoyagePirateExperience(outcome, completed),
                TreasureCompleted = outcome == VoyageOutcome.TreasureRecovered,
            };
            // Transfer net cash to the settlement once; it must not remain spendable aboard.
            Cargo.Gold -= earnedGold;
            Cargo.Wood = RetainedResource(Cargo.Wood, starting.Wood, protection);
            Cargo.Iron = RetainedResource(Cargo.Iron, starting.Iron, protection);
            Cargo.Fame = RetainedResource(Cargo.Fame, starting.Fame, protection);
            Cargo.Cloth = RetainedResource(Cargo.Cloth, starting.Cloth, protection);
            Cargo.Water = RetainedResource(Cargo.Water, starting.Water, protection);
            Cargo.Food = RetainedResource(Cargo.Food, starting.Food, protection);
            Cargo.Beer = RetainedResource(Cargo.Beer, starting.Beer, protection);
            Cargo.Medicine = RetainedResource(Cargo.Medicine, starting.Medicine, protection);
            Cargo.Powder = RetainedResource(Cargo.Powder, starting.Powder, protection);
            Cargo.Cannonballs = RetainedResource(Cargo.Cannonballs, starting.Cannonballs, protection);
            RetainTreasureCargo(starting, protection);
            pendingBattleReward = null;
            Phase = CoreDemoPhase.Settlement;
            ActiveEncounter = null;
            activeNode = null;
            Battle = null;
            AddLog(title + "：货物保护率 " + (protection * 100f).ToString("0") + "% 。");
        }

        private static int ProtectedGain(int current, int departure, float protection) =>
            (int)Math.Floor(Math.Max(0, current - departure) * protection);

        private static int RetainedResource(int current, int departure, float protection) =>
            Math.Min(current, departure) + ProtectedGain(current, departure, protection);

        private static VoyageCargo SnapshotCargo(VoyageCargo source) => new VoyageCargo
        {
            Water = source.Water, Food = source.Food, Beer = source.Beer, Medicine = source.Medicine,
            Wood = source.Wood, Iron = source.Iron, Cloth = source.Cloth, Powder = source.Powder,
            Cannonballs = source.Cannonballs, Gold = source.Gold, Fame = source.Fame,
            Gem = source.Gem, AncientMaterial = source.AncientMaterial, Relic = source.Relic,
            BlackPearl = source.BlackPearl, AncientCompass = source.AncientCompass, Lantern = source.Lantern, Curse = source.Curse,
        };

        private static VoyageEffect SplitBattleEffect(VoyageEffect source, bool rewards)
        {
            int Part(int value) => rewards ? Math.Max(0, value) : Math.Min(0, value);
            return new VoyageEffect
            {
                Hull = Part(source.Hull), Morale = Part(source.Morale), Water = Part(source.Water),
                Food = Part(source.Food), Beer = Part(source.Beer), Medicine = Part(source.Medicine),
                Wood = Part(source.Wood), Iron = Part(source.Iron), Cloth = Part(source.Cloth),
                Powder = Part(source.Powder), Cannonballs = Part(source.Cannonballs),
                Gold = Part(source.Gold), Fame = Part(source.Fame), Reveal = Part(source.Reveal),
                MapFragments = Part(source.MapFragments), Gem = Part(source.Gem), AncientMaterial = Part(source.AncientMaterial),
                Relic = Part(source.Relic), BlackPearl = Part(source.BlackPearl), AncientCompass = Part(source.AncientCompass),
                Lantern = Part(source.Lantern), Curse = Part(source.Curse), TreasureClues = Part(source.TreasureClues),
            };
        }

        private void RevealAt(VoyagePoint position)
        {
            float visionNm = 120f + (HasNavigator ? 60f : 0f) + VisionBonusNm;
            float radius = visionNm / Map.Route.NauticalMilesPerMapUnit;
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    if (CanSeeCell(position, column, row, radius))
                    {
                        Map.ExploredCells[row * CoreVoyageMap.GridColumns + column] = true;
                    }
                }
            }

            for (int index = 0; index < Map.Nodes.Count; index += 1)
            {
                CoreMapNode node = Map.Nodes[index];
                int nodeColumn = Math.Max(0, Math.Min(CoreVoyageMap.GridColumns - 1, (int)(node.Position.X / CoreVoyageMap.Width * CoreVoyageMap.GridColumns)));
                int nodeRow = Math.Max(0, Math.Min(CoreVoyageMap.GridRows - 1, (int)(node.Position.Y / CoreVoyageMap.Height * CoreVoyageMap.GridRows)));
                if (node.Encounter != null && node.Encounter.CanDiscoverBySight && Distance(node.Position, position) <= radius && CanSeeCell(position, nodeColumn, nodeRow, radius))
                {
                    node.Discovered = true;
                }
            }
        }

        private bool CanSeeCell(VoyagePoint observer, int column, int row, float baseRadius)
        {
            VoyagePoint center = Map.CellCenter(column, row);
            float distance = Distance(observer, center);
            if (distance <= 1.2f)
            {
                return true;
            }

            float edgeVariation = VisibilityEdgeVariation(Map.Hash, column, row);
            if (distance > baseRadius + Math.Max(0f, edgeVariation))
            {
                return false;
            }

            int samples = Math.Max(1, (int)Math.Ceiling(distance / 1.15f));
            float obstruction = 0f;
            for (int sample = 1; sample <= samples; sample += 1)
            {
                float t = sample / (float)samples;
                VoyagePoint point = new VoyagePoint(
                    observer.X + (center.X - observer.X) * t,
                    observer.Y + (center.Y - observer.Y) * t);
                int sampleColumn = Math.Max(0, Math.Min(CoreVoyageMap.GridColumns - 1, (int)(point.X / CoreVoyageMap.Width * CoreVoyageMap.GridColumns)));
                int sampleRow = Math.Max(0, Math.Min(CoreVoyageMap.GridRows - 1, (int)(point.Y / CoreVoyageMap.Height * CoreVoyageMap.GridRows)));
                float multiplier = sample == samples ? 0.42f : 0.18f;
                obstruction += TerrainVisionPenalty(Map.TerrainAt(sampleColumn, sampleRow)) * multiplier;
            }

            return distance + obstruction <= baseRadius + edgeVariation;
        }

        private static float TerrainVisionPenalty(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.Shoal: return 0.18f;
                case SeaTerrainType.Reef: return 0.72f;
                case SeaTerrainType.KelpField: return 0.32f;
                case SeaTerrainType.FogBank: return 1.45f;
                case SeaTerrainType.StormFront: return 1.85f;
                case SeaTerrainType.Island: return 2.6f;
                case SeaTerrainType.Beach: return 0.12f;
                case SeaTerrainType.Strait: return 0.05f;
                default: return 0f;
            }
        }

        private static float VisibilityEdgeVariation(uint mapHash, int column, int row)
        {
            unchecked
            {
                uint value = mapHash ^ ((uint)column * 374761393u) ^ ((uint)row * 668265263u);
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return ((value & 1023u) / 1023f - 0.5f) * 1.7f;
            }
        }

        private VoyagePoint SnapToNearestPassable(VoyagePoint point)
        {
            PointToCell(point, out int originColumn, out int originRow);
            if (Map.IsPassable(originColumn, originRow)) return point;
            for (int radius = 1; radius < Math.Max(CoreVoyageMap.GridColumns, CoreVoyageMap.GridRows); radius += 1)
            {
                VoyagePoint best = point;
                float bestDistance = float.MaxValue;
                for (int row = originRow - radius; row <= originRow + radius; row += 1)
                {
                    for (int column = originColumn - radius; column <= originColumn + radius; column += 1)
                    {
                        if (column < 0 || row < 0 || column >= CoreVoyageMap.GridColumns || row >= CoreVoyageMap.GridRows || !Map.IsPassable(column, row)) continue;
                        VoyagePoint candidate = Map.CellCenter(column, row);
                        float distance = Distance(point, candidate);
                        if (distance < bestDistance) { best = candidate; bestDistance = distance; }
                    }
                }
                if (bestDistance < float.MaxValue) return best;
            }
            return point;
        }

        private List<VoyagePoint> FindNavigablePath(VoyagePoint start, VoyagePoint requestedTarget)
        {
            VoyagePoint target = SnapToNearestPassable(requestedTarget);
            PointToCell(start, out int startColumn, out int startRow);
            PointToCell(target, out int targetColumn, out int targetRow);
            int startIndex = startRow * CoreVoyageMap.GridColumns + startColumn;
            int targetIndex = targetRow * CoreVoyageMap.GridColumns + targetColumn;
            int count = CoreVoyageMap.GridColumns * CoreVoyageMap.GridRows;
            float[] cost = new float[count];
            int[] previous = new int[count];
            bool[] closed = new bool[count];
            for (int index = 0; index < count; index += 1) { cost[index] = float.MaxValue; previous[index] = -1; }
            cost[startIndex] = 0f;
            List<int> open = new List<int> { startIndex };
            int[] offsets = { -1, 0, 1 };
            while (open.Count > 0)
            {
                int bestOpen = 0;
                float bestScore = float.MaxValue;
                for (int index = 0; index < open.Count; index += 1)
                {
                    int cell = open[index];
                    int column = cell % CoreVoyageMap.GridColumns;
                    int row = cell / CoreVoyageMap.GridColumns;
                    float dx = targetColumn - column;
                    float dy = targetRow - row;
                    float score = cost[cell] + (float)Math.Sqrt(dx * dx + dy * dy);
                    if (score < bestScore) { bestScore = score; bestOpen = index; }
                }

                int current = open[bestOpen];
                open.RemoveAt(bestOpen);
                if (current == targetIndex) break;
                if (closed[current]) continue;
                closed[current] = true;
                int currentColumn = current % CoreVoyageMap.GridColumns;
                int currentRow = current / CoreVoyageMap.GridColumns;
                foreach (int y in offsets)
                {
                    foreach (int x in offsets)
                    {
                        if (x == 0 && y == 0) continue;
                        int column = currentColumn + x;
                        int row = currentRow + y;
                        if (column < 0 || row < 0 || column >= CoreVoyageMap.GridColumns || row >= CoreVoyageMap.GridRows || !Map.IsPassable(column, row)) continue;
                        if (x != 0 && y != 0 && (!Map.IsPassable(currentColumn + x, currentRow) || !Map.IsPassable(currentColumn, currentRow + y))) continue;
                        int neighbor = row * CoreVoyageMap.GridColumns + column;
                        if (closed[neighbor]) continue;
                        float step = x == 0 || y == 0 ? 1f : 1.41421356f;
                        step += TerrainNavigationCost(Map.TerrainCells[neighbor]);
                        float candidate = cost[current] + step;
                        if (candidate + 0.0001f >= cost[neighbor]) continue;
                        cost[neighbor] = candidate;
                        previous[neighbor] = current;
                        if (!open.Contains(neighbor)) open.Add(neighbor);
                    }
                }
            }

            if (targetIndex != startIndex && previous[targetIndex] < 0) return new List<VoyagePoint> { start };
            List<VoyagePoint> raw = new List<VoyagePoint>();
            int cursor = targetIndex;
            raw.Add(target);
            while (cursor != startIndex && cursor >= 0)
            {
                cursor = previous[cursor];
                if (cursor >= 0 && cursor != startIndex)
                    raw.Add(Map.CellCenter(cursor % CoreVoyageMap.GridColumns, cursor / CoreVoyageMap.GridColumns));
            }
            raw.Add(start);
            raw.Reverse();

            List<VoyagePoint> simplified = new List<VoyagePoint> { raw[0] };
            int anchor = 0;
            while (anchor < raw.Count - 1)
            {
                int farthest = anchor + 1;
                for (int candidate = raw.Count - 1; candidate > anchor + 1; candidate -= 1)
                {
                    if (HasNavigableLine(raw[anchor], raw[candidate])) { farthest = candidate; break; }
                }
                simplified.Add(raw[farthest]);
                anchor = farthest;
            }
            return simplified;
        }

        private bool HasNavigableLine(VoyagePoint start, VoyagePoint end)
        {
            float distance = Distance(start, end);
            int samples = Math.Max(1, (int)Math.Ceiling(distance / 0.45f));
            for (int sample = 0; sample <= samples; sample += 1)
            {
                float t = sample / (float)samples;
                VoyagePoint point = new VoyagePoint(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                PointToCell(point, out int column, out int row);
                if (!Map.IsPassable(column, row)) return false;
            }
            return true;
        }

        private static float TerrainNavigationCost(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.StormFront: return 0.55f;
                case SeaTerrainType.FogBank: return 0.35f;
                case SeaTerrainType.Reef: return 0.28f;
                case SeaTerrainType.KelpField: return 0.18f;
                case SeaTerrainType.Shoal:
                case SeaTerrainType.Beach: return 0.12f;
                case SeaTerrainType.Strait: return -0.05f;
                default: return 0f;
            }
        }

        private static float PathDistance(IReadOnlyList<VoyagePoint> path)
        {
            float distance = 0f;
            if (path == null) return distance;
            for (int index = 1; index < path.Count; index += 1) distance += Distance(path[index - 1], path[index]);
            return distance;
        }

        private static float DistanceToPath(VoyagePoint point, IReadOnlyList<VoyagePoint> path, out float progress)
        {
            progress = 0f;
            if (path == null || path.Count < 2) return float.MaxValue;
            float bestDistance = float.MaxValue;
            float travelled = 0f;
            for (int index = 1; index < path.Count; index += 1)
            {
                float t;
                float distance = DistanceToSegment(point, path[index - 1], path[index], out t);
                float segmentLength = Distance(path[index - 1], path[index]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    progress = travelled + segmentLength * t;
                }
                travelled += segmentLength;
            }
            return bestDistance;
        }

        private void RevealPath(VoyagePoint start, VoyagePoint end, float nauticalMiles)
        {
            int samples = Math.Max(1, (int)Math.Ceiling(nauticalMiles / 10f));
            for (int sample = 0; sample <= samples; sample += 1)
            {
                float t = sample / (float)samples;
                RevealAt(new VoyagePoint(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t));
            }
        }

        private void RevealPath(IReadOnlyList<VoyagePoint> path, float maximumMapDistance)
        {
            if (path == null || path.Count < 2) return;
            float remaining = Math.Max(0f, maximumMapDistance);
            for (int index = 1; index < path.Count && remaining > 0f; index += 1)
            {
                VoyagePoint start = path[index - 1];
                VoyagePoint end = path[index];
                float segment = Distance(start, end);
                float portion = Math.Min(segment, remaining);
                VoyagePoint clippedEnd = segment <= portion + 0.001f
                    ? end
                    : new VoyagePoint(start.X + (end.X - start.X) * portion / segment, start.Y + (end.Y - start.Y) * portion / segment);
                RevealPath(start, clippedEnd, portion * Map.Route.NauticalMilesPerMapUnit);
                remaining -= portion;
            }
        }

        private void RevealNearestHidden(int count)
        {
            for (int reveal = 0; reveal < count; reveal += 1)
            {
                CoreMapNode nearest = null;
                float nearestDistance = float.MaxValue;
                for (int index = 0; index < Map.Nodes.Count; index += 1)
                {
                    CoreMapNode candidate = Map.Nodes[index];
                    if (candidate.Encounter == null || !candidate.Encounter.CanRevealByIntel || candidate.Visited || candidate.IntelRevealed) continue;
                    float distance = Distance(Position, candidate.Position);
                    if (distance < nearestDistance)
                    {
                        nearest = candidate;
                        nearestDistance = distance;
                    }
                }

                if (nearest == null) return;
                nearest.IntelRevealed = true;
                nearest.Discovered = true;
            }
        }

        private static VoyageCargo CreateStartingCargo()
        {
            return new VoyageCargo
            {
                Water = 286,
                Food = 286,
                Beer = 4,
                Medicine = 2,
                Wood = 8,
                Powder = 8,
                Cannonballs = 10,
                Gold = 60,
            };
        }

        private static VoyagePoint DirectionVector(SailingDirection direction)
        {
            const float diagonal = 0.70710678f;
            switch (direction)
            {
                case SailingDirection.North: return new VoyagePoint(0f, 1f);
                case SailingDirection.NorthEast: return new VoyagePoint(diagonal, diagonal);
                case SailingDirection.East: return new VoyagePoint(1f, 0f);
                case SailingDirection.SouthEast: return new VoyagePoint(diagonal, -diagonal);
                case SailingDirection.South: return new VoyagePoint(0f, -1f);
                case SailingDirection.SouthWest: return new VoyagePoint(-diagonal, -diagonal);
                case SailingDirection.West: return new VoyagePoint(-1f, 0f);
                case SailingDirection.NorthWest: return new VoyagePoint(-diagonal, diagonal);
                default: return new VoyagePoint(1f, 0f);
            }
        }

        private static double DirectionAngle(SailingDirection direction)
        {
            VoyagePoint vector = DirectionVector(direction);
            return Math.Atan2(vector.Y, vector.X);
        }

        private static SailingDirection ClosestDirection(double angle)
        {
            SailingDirection best = SailingDirection.East;
            double difference = double.MaxValue;
            foreach (SailingDirection direction in Enum.GetValues(typeof(SailingDirection)))
            {
                double candidate = DirectionAngle(direction);
                double delta = Math.Abs(Math.Atan2(Math.Sin(angle - candidate), Math.Cos(angle - candidate)));
                if (delta < difference)
                {
                    best = direction;
                    difference = delta;
                }
            }
            return best;
        }

        private static float Distance(VoyagePoint first, VoyagePoint second)
        {
            float dx = first.X - second.X;
            float dy = first.Y - second.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float DistanceToSegment(VoyagePoint point, VoyagePoint start, VoyagePoint end, out float t)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0.0001f)
            {
                t = 0f;
                return Distance(point, start);
            }

            t = Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0f, 1f);
            return Distance(point, new VoyagePoint(start.X + dx * t, start.Y + dy * t));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void AddLog(string message)
        {
            logEntries.Add(message);
            if (logEntries.Count > 30) logEntries.RemoveAt(0);
        }
    }
}
