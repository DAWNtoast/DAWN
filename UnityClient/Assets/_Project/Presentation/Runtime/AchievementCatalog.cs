using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>A read-only display snapshot; permanent milestones never lose their completed state.</summary>
    public readonly struct AchievementProgressView
    {
        public bool Unlocked { get; }
        public decimal Current { get; }
        public decimal Target { get; }
        public bool IsQuantified { get; }
        public string Detail { get; }
        public float Fraction => Unlocked ? 1f : Target > 0m ? (float)Math.Min(1m, Current / Target) : 0f;

        public AchievementProgressView(bool unlocked, decimal current, decimal target, bool quantified, string detail = null)
        {
            Unlocked = unlocked;
            Current = Math.Max(0m, current);
            Target = Math.Max(1m, target);
            IsQuantified = quantified;
            Detail = detail;
        }
    }

    /// <summary>
    /// PlayerPrefs adapter for the canonical Core achievement catalog. Queries
    /// are read-only; gameplay save boundaries capture earned milestones.
    /// </summary>
    public static class AchievementCatalog
    {
        public const int TotalCount = CoreAchievementCatalog.CatalogCount;
        private const string UnlockPrefix = "ShatteredSail.Achievements.Unlocked.v1.";
        private const string VisitPrefix = "ShatteredSail.Achievements.Visited.v1.";

        private static readonly string[] Ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };

        public static IReadOnlyList<CoreAchievementDefinition> All => CoreAchievementCatalog.All;

        public static int CountUnlocked() => EvaluateUnlockStates(null);

        /// <summary>
        /// Fills the caller's reusable buffer in catalogue order and returns the
        /// unlocked count. A fresh shared snapshot keeps each refresh read-only.
        /// </summary>
        public static int FillUnlockStates(bool[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < All.Count)
                throw new ArgumentException("The destination must hold every achievement.", nameof(destination));
            return EvaluateUnlockStates(destination);
        }

        public static int FillProgress(AchievementProgressView[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < All.Count) throw new ArgumentException("The destination must hold every achievement.", nameof(destination));
            var snapshot = new ProgressSnapshot();
            int count = 0;
            for (int index = 0; index < All.Count; index++)
            {
                CoreAchievementDefinition definition = All[index];
                bool unlocked = Int(UnlockPrefix + definition.Id) == 1 || MeetsRequirement(definition, snapshot);
                destination[index] = DisplayProgress(definition, snapshot, unlocked);
                if (unlocked) count++;
            }
            return count;
        }

        /// <summary>Reads only permanent records, for notification diffs after a successful gameplay commit.</summary>
        public static void FillCapturedStates(bool[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < All.Count) throw new ArgumentException("The destination must hold every achievement.", nameof(destination));
            for (int index = 0; index < All.Count; index++) destination[index] = Int(UnlockPrefix + All[index].Id) == 1;
        }

        private static AchievementProgressView DisplayProgress(CoreAchievementDefinition definition, ProgressSnapshot snapshot, bool unlocked)
        {
            decimal current;
            decimal target = definition.Target;
            string detail = null;
            switch (definition.RequirementKey)
            {
                case "profile_gold": current = snapshot.Gold; break;
                case "ports_visited": current = snapshot.VisitedPortCount; break;
                case "port_investment_levels": current = snapshot.InvestmentCount; break;
                case "ports_invested": current = InvestedPortCount(); break;
                case "ships_owned": current = snapshot.OwnedShipCount; break;
                case "components_equipped": current = snapshot.MaxLoadoutCount; break;
                case "crew_recruited": current = snapshot.CrewCount; break;
                case "crew_ports_recruited": current = snapshot.CrewPortCount; break;
                case "warehouse_treasures_owned": current = snapshot.WarehouseCount; break;
                case "warehouse_treasures_displayed": current = DisplayCount(); break;
                case "configured_cards": current = Int(CoreGameplayDemoController.BattleDeckEditedKey) == 1
                    ? CsvCount(CoreGameplayDemoController.BattleDeckKey) : 0; break;
                case "configured_deck_stages": current = Int(CoreGameplayDemoController.BattleDeckEditedKey) == 1
                    ? StageDeckCount() : 0; break;
                case "special_cards_owned": current = CsvCount(CoreGameplayDemoController.AcquiredSpecialCardsKey, ';'); break;
                case "profile_fame": current = Cargo("Fame"); break;
                case "prepared_water_and_food":
                    int water = Math.Max(0, Cargo("Water")), food = Math.Max(0, Cargo("Food"));
                    current = Math.Min(water, food);
                    detail = "水 " + water + "/" + definition.Target + " · 粮 " + food + "/" + definition.Target;
                    break;
                case "all_base_cargo_present":
                    current = 0;
                    foreach (string resource in BaseCargo) if (Cargo(resource) >= definition.Target) current++;
                    target = BaseCargo.Length;
                    detail = current + " / " + target + " 种补给";
                    break;
                case "seventh_harbor_prerequisites":
                    current = (Int(CoreGameplayDemoController.TutorialCompletedKey) == 1 ? 1 : 0) +
                        (snapshot.AllPortsVisited ? 1 : 0) + (snapshot.OwnedShipCount >= definition.Target ? 1 : 0);
                    target = 3;
                    detail = current + " / 3 项条件";
                    break;
                default: return new AchievementProgressView(unlocked, unlocked ? 1m : 0m, 1m, false);
            }
            return new AchievementProgressView(unlocked, current, target, true, detail);
        }

        private static readonly string[] BaseCargo = { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs" };

        private static int EvaluateUnlockStates(bool[] destination)
        {
            ProgressSnapshot snapshot = null;
            int unlocked = 0;
            for (int index = 0; index < All.Count; index += 1)
            {
                CoreAchievementDefinition definition = All[index];
                bool value = Int(UnlockPrefix + definition.Id) == 1 ||
                    MeetsRequirement(definition, snapshot ?? (snapshot = new ProgressSnapshot()));
                if (destination != null) destination[index] = value;
                if (value) unlocked += 1;
            }
            return unlocked;
        }

        public static bool IsUnlocked(CoreAchievementDefinition definition)
        {
            if (definition == null) return false;
            // Metadata supplied by a caller cannot override a shipped target or
            // create a second persistence key for a retired prototype alias.
            CoreAchievementDefinition canonical = CoreAchievementCatalog.Find(definition.Id);
            return canonical != null && (Int(UnlockPrefix + canonical.Id) == 1 || MeetsRequirement(canonical, new ProgressSnapshot()));
        }

        // Called by real gameplay save boundaries, never by an editor preview or
        // a UI query. Keep these writes in the same flush as their source state.
        public static void CaptureProgress()
        {
            var snapshot = new ProgressSnapshot();
            foreach (string port in Ports)
                if (snapshot.Visited(port)) PlayerPrefs.SetInt(VisitPrefix + port, 1);
            foreach (CoreAchievementDefinition definition in All)
                if (Int(UnlockPrefix + definition.Id) != 1 && MeetsRequirement(definition, snapshot)) Unlock(definition.Id);
        }

        public static IEnumerable<string> PersistenceKeys
        {
            get
            {
                foreach (CoreAchievementDefinition definition in All) yield return UnlockPrefix + definition.Id;
                foreach (string port in Ports) yield return VisitPrefix + port;
            }
        }

        public static void ClearProgress()
        {
            foreach (string key in PersistenceKeys) PlayerPrefs.DeleteKey(key);
            // This is gameplay evidence rather than an achievement record, but
            // it must reset with the achievement history when a profile starts
            // over; otherwise restored default decks could look player-authored.
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleDeckEditedKey);
        }

        private static void Unlock(string id) => PlayerPrefs.SetInt(UnlockPrefix + id, 1);
        public static void RecordDeckEdited()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.BattleDeckEditedKey, 1);
            Unlock("tutorial_deck");
        }
        public static void RecordDamagedReturn() => Unlock("tutorial_recovery");
        public static void RecordNavigatorVoyage() => Unlock("tutorial_navigator");
        public static void RecordVoyageThreeEvents() => Unlock(CoreAchievementCatalog.VoyageThreeEventsId);
        public static void RecordVoyageHalfChart() => Unlock(CoreAchievementCatalog.VoyageHalfChartId);
        public static void RecordVoyageEventArrival() => Unlock(CoreAchievementCatalog.VoyageEventArrivalId);
        private static bool MeetsRequirement(CoreAchievementDefinition definition, ProgressSnapshot snapshot)
        {
            switch (definition.Id)
            {
                case "tutorial_departure": return Int(CoreGameplayDemoController.TutorialCompletedKey) == 1;
                case "tutorial_reward": return Int(CoreGameplayDemoController.TutorialRewardGrantedKey) == 1 || HasTutorialTreasure() ||
                    (Int(CoreGameplayDemoController.TutorialCompletedKey) == 1 && snapshot.WarehouseCount >= definition.Target);
                case "tutorial_checkpoint": return Int(CoreGameplayDemoController.GuidedTutorialCheckpointKey) >= definition.Target || Int(CoreGameplayDemoController.TutorialCompletedKey) == 1;
                case "tutorial_deck": return Int(CoreGameplayDemoController.GuidedTutorialDeckEditedKey) == 1;
                case "tutorial_component": return Int(CoreGameplayDemoController.GuidedTutorialComponentRewardKey) == 1 || snapshot.HasSpareComponent;
                case "tutorial_recovery": return Int(CoreGameplayDemoController.GuidedTutorialRecoveryKey) == 1;
                case "tutorial_navigator": return Int(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey) == 1;
                // Only the confirmed settlement transaction supplies this evidence.
                // Opening a menu or reading unrelated profile counters cannot earn it.
                case CoreAchievementCatalog.VoyageThreeEventsId:
                case CoreAchievementCatalog.VoyageHalfChartId:
                case CoreAchievementCatalog.VoyageEventArrivalId: return false;
                case "tutorial_warehouse": return snapshot.WarehouseCount >= definition.Target;
                case "component_loadout": return snapshot.MaxLoadoutCount >= definition.Target;
                case "gold_600": return snapshot.Gold >= definition.Target;
                case "gold_900": return snapshot.Gold >= definition.Target;
                case "gold_1200": return snapshot.Gold >= definition.Target;
                case "gold_2000": return snapshot.Gold >= definition.Target;
                case "port_gray_gull": return snapshot.Visited("灰鸥港");
                case "port_white_shell": return snapshot.Visited("白贝港");
                case "port_bronze_bell": return snapshot.Visited("铜钟港");
                case "port_salt_crown": return snapshot.Visited("盐冠港");
                case "port_twilight": return snapshot.Visited("暮潮港");
                case "all_ports": return snapshot.VisitedPortCount >= definition.Target;
                case "invest_port": return snapshot.InvestmentCount >= definition.Target;
                case "invest_all_ports": return InvestedPortCount() >= definition.Target;
                case "invest_ten": return snapshot.InvestmentCount >= definition.Target;
                case "ship_second": return snapshot.OwnedShipCount >= definition.Target;
                case "ship_third": return snapshot.OwnedShipCount >= definition.Target;
                case "ship_fourth": return snapshot.OwnedShipCount >= definition.Target;
                case "all_ships": return snapshot.OwnedShipCount >= definition.Target;
                case "component_inventory": return snapshot.HasComponents;
                case "component_cannon": return snapshot.HasCannon;
                case "component_full": return snapshot.MaxLoadoutCount >= definition.Target;
                case "component_shop": return HasPurchasedComponent(snapshot);
                case "crew_one": return snapshot.CrewCount >= definition.Target;
                case "crew_three": return snapshot.CrewCount >= definition.Target;
                case "crew_six": return snapshot.CrewCount >= definition.Target;
                case "crew_variants": return snapshot.CrewPortCount >= definition.Target;
                case "warehouse_one": return snapshot.WarehouseCount >= definition.Target;
                case "warehouse_three": return snapshot.WarehouseCount >= definition.Target;
                case "warehouse_ten": return snapshot.WarehouseCount >= definition.Target;
                case "warehouse_display_six": return DisplayCount() >= definition.Target;
                case "chart_drawn": return !string.IsNullOrEmpty(PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey, string.Empty));
                // Loading a new profile writes curated defaults for all three
                // stages. Require an explicit workshop edit so those defaults
                // do not masquerade as player-authored milestones.
                case "battle_deck": return Int(CoreGameplayDemoController.BattleDeckEditedKey) == 1 &&
                    CsvCount(CoreGameplayDemoController.BattleDeckKey) >= definition.Target;
                case "battle_stages": return Int(CoreGameplayDemoController.BattleDeckEditedKey) == 1 &&
                    StageDeckCount() >= definition.Target;
                case "card_upgrade": return !string.IsNullOrEmpty(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty));
                case "special_card": return CsvCount(CoreGameplayDemoController.AcquiredSpecialCardsKey, ';') >= definition.Target;
                case "cargo_ready": return Cargo("Water") >= definition.Target && Cargo("Food") >= definition.Target;
                case "cargo_all": return Cargo("Water") >= definition.Target && Cargo("Food") >= definition.Target && Cargo("Beer") >= definition.Target && Cargo("Medicine") >= definition.Target && Cargo("Wood") >= definition.Target && Cargo("Iron") >= definition.Target && Cargo("Cloth") >= definition.Target && Cargo("Powder") >= definition.Target && Cargo("Cannonballs") >= definition.Target;
                case "fame": return Cargo("Fame") >= definition.Target;
                case "seventh_harbor": return Int(CoreGameplayDemoController.TutorialCompletedKey) == 1 && snapshot.AllPortsVisited && snapshot.OwnedShipCount >= definition.Target;
                default: return false;
            }
        }

        private static int Int(string key) => PlayerPrefs.GetInt(key, 0);
        private static decimal Gold() => ProfileGoldPersistence.Load();

        private static bool HasTutorialTreasure()
        {
            foreach (string id in PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Empty).Split(','))
                if (id == CoreWarehouseCollection.TutorialTreasureId) return true;
            return false;
        }

        private static int InvestmentCount()
        {
            int total = 0;
            for (int index = 0; index < Ports.Length; index += 1)
                total += Mathf.Max(0, PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(Ports[index]), 0));
            return total;
        }

        private static int InvestedPortCount()
        {
            int count = 0;
            foreach (string port in Ports)
                if (Int(CoreGameplayDemoController.PortInvestmentKey(port)) > 0) count += 1;
            return count;
        }

        private static int OwnedShips()
        {
            int count = 1;
            for (int index = 1; index < CoreVoyageSession.Ships.Count; index += 1)
                if (Int(CoreGameplayDemoController.ShipOwnedKey(index)) == 1) count += 1;
            return count;
        }

        private static bool HasPurchasedComponent(ProgressSnapshot snapshot)
        {
            if (Int(CoreGameplayDemoController.ComponentPurchaseAchievementKey) == 1) return true;
            for (int portIndex = 0; portIndex < Ports.Length; portIndex += 1)
            {
                foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(snapshot.Art, Ports[portIndex], CoreGameplayDemoController.SavedGameDateTime()))
                    if (Int(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id)) == 1) return true;
            }
            return false;
        }

        private sealed class ProgressSnapshot
        {
            private HashSet<string> visited;
            private List<CrewVariantDefinition> crew;
            private OwnedShipComponentInventory components;
            private bool componentsLoaded;
            private int maxLoadoutCount;
            private bool hasCannon;
            private ComponentArtCatalog art;
            private decimal? gold;
            private int? warehouseCount;
            private int? investmentCount;
            private int? ownedShipCount;
            public ComponentArtCatalog Art => art ?? (art = new ComponentArtCatalog());
            public decimal Gold => gold ?? (gold = AchievementCatalog.Gold()).Value;
            public int WarehouseCount => warehouseCount ?? (warehouseCount = AchievementCatalog.WarehouseCount()).Value;
            public int InvestmentCount => investmentCount ?? (investmentCount = AchievementCatalog.InvestmentCount()).Value;
            public int OwnedShipCount => ownedShipCount ?? (ownedShipCount = OwnedShips()).Value;

            public bool Visited(string port)
            {
                if (visited == null)
                {
                    visited = new HashSet<string>(StringComparer.Ordinal);
                    string current = PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey, string.Empty);
                    foreach (string known in Ports)
                        if (known == current || Int(VisitPrefix + known) == 1 ||
                            Int(CoreGameplayDemoController.PortInvestmentKey(known)) > 0) visited.Add(known);
                    // Collection history already records real arrivals in existing saves.
                    try
                    {
                        string json = PlayerPrefs.GetString(CoreGameplayDemoController.CollectionProgressKey, string.Empty);
                        CoreCollectionProgress history = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<CoreCollectionProgress>(json);
                        if (history != null && history.version == 1 && history.visitedPorts != null)
                            foreach (string known in Ports)
                                if (Array.IndexOf(history.visitedPorts, known) >= 0) visited.Add(known);
                    }
                    catch (ArgumentException) { }
                }
                return visited.Contains(port);
            }

            public int VisitedPortCount
            {
                get
                {
                    int count = 0;
                    foreach (string port in Ports) if (Visited(port)) count += 1;
                    return count;
                }
            }

            public bool AllPortsVisited => VisitedPortCount == Ports.Length;

            private List<CrewVariantDefinition> RecruitedCrew
            {
                get
                {
                    if (crew != null) return crew;
                    crew = new List<CrewVariantDefinition>();
                    var professions = new HashSet<int>();
                    foreach (CrewVariantDefinition candidate in CrewNarrativeCatalog.AllCrew)
                    {
                        if (Int(CoreGameplayDemoController.CrewVariantRecruitmentKey(candidate.Id)) != 1) continue;
                        crew.Add(candidate);
                        professions.Add(candidate.ProfessionIndex);
                    }
                    // Older saves retained only a profession. Match the game's
                    // own migration, without counting that identity twice.
                    for (int profession = 0; profession < 6; profession++)
                    {
                        if (professions.Contains(profession) || Int(CoreGameplayDemoController.CrewRecruitmentKey(profession)) != 1) continue;
                        CrewVariantDefinition legacy = CrewNarrativeCatalog.LegacyForProfession(profession);
                        if (legacy != null) crew.Add(legacy);
                    }
                    return crew;
                }
            }

            public int CrewCount => RecruitedCrew.Count;
            public int CrewPortCount
            {
                get
                {
                    var ports = new HashSet<string>(StringComparer.Ordinal);
                    foreach (CrewVariantDefinition candidate in RecruitedCrew)
                        if (Array.IndexOf(Ports, candidate.PortName) >= 0) ports.Add(candidate.PortName);
                    return ports.Count;
                }
            }

            private void LoadComponents()
            {
                if (componentsLoaded) return;
                componentsLoaded = true;
                components = new OwnedShipComponentInventory(Art);
                bool Owns(int ship) => ship >= 0 && ship < CoreVoyageSession.Ships.Count &&
                    (ship == 0 || Int(CoreGameplayDemoController.ShipOwnedKey(ship)) == 1);
                bool restored = components.Import(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey, string.Empty), Owns);
                if (!restored)
                {
                    // Read legacy fittings without generating the free starter loadout.
                    int count = 0;
                    foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                    {
                        ComponentArtEntry entry = Art.GetById(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentLoadoutKey(slot), string.Empty));
                        if (entry == null || entry.Slot != slot) continue;
                        components.Grant(entry, 1f);
                        count++;
                        hasCannon |= slot == ComponentSlot.Cannon;
                    }
                    maxLoadoutCount = count;
                    return;
                }
                for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
                {
                    if (!Owns(ship)) continue;
                    int count = 0;
                    foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                    {
                        if (components.Installed(ship, slot) == null) continue;
                        count++;
                        hasCannon |= slot == ComponentSlot.Cannon;
                    }
                    maxLoadoutCount = Math.Max(maxLoadoutCount, count);
                }
            }

            public bool HasComponents { get { LoadComponents(); return components.Items.Count > 0; } }
            public int MaxLoadoutCount { get { LoadComponents(); return maxLoadoutCount; } }
            public bool HasCannon { get { LoadComponents(); return hasCannon; } }
            public bool HasSpareComponent
            {
                get
                {
                    LoadComponents();
                    foreach (OwnedShipComponent item in components.Items) if (item.ShipIndex < 0) return true;
                    return false;
                }
            }
        }

        private static int WarehouseCount() => CsvCount(CoreGameplayDemoController.WarehouseOwnedKey);

        private static int DisplayCount()
        {
            int count = 0;
            foreach (string token in PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey, string.Empty).Split(','))
                if (!string.IsNullOrEmpty(token)) count += 1;
            return Math.Max(count, PlayerPrefs.GetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey, 0));
        }

        private static int StageDeckCount()
        {
            int count = 0;
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                if (CsvCount(CoreGameplayDemoController.StageBattleDeckKey(stage)) > 0) count += 1;
            return count;
        }

        private static int CsvCount(string key, char separator = ',')
        {
            string raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            int count = 0;
            foreach (string token in raw.Split(separator)) if (!string.IsNullOrWhiteSpace(token)) count += 1;
            return count;
        }

        private static int Cargo(string resource) => PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey(resource), 0);
    }
}
