using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    // Public fields deliberately keep these DTOs compatible with Unity JsonUtility.
    [Serializable] public sealed class CoreVoyageCargoCheckpoint
    {
        public int water, food, beer, medicine, wood, iron, cloth, powder, cannonballs, gold, fame;
        public int gem, ancientMaterial, relic, blackPearl, ancientCompass, lantern, curse;
    }

    [Serializable] public sealed class CoreVoyageNodeCheckpoint
    {
        public int id;
        public string encounterId;
        public float x, y;
        public bool discovered, intelRevealed, visited, skipped;
    }

    [Serializable] public sealed class CoreVoyageValueCheckpoint { public string id; public int value; }
    [Serializable] public sealed class CoreVoyageDeckCheckpoint { public int[] cards; }

    [Serializable] public sealed class CoreVoyageCheckpoint
    {
        public int version = 1;
        // Absent in legacy version-1 JSON; never reinterpret an in-flight sea.
        public int mapGeometryVersion;
        public int phase, routeIndex, shipIndex, heading, plannedDistance;
        public string selectedSeed, selectedDestinationPort, mapSeed, departurePort, destinationPort, profileGold;
        public uint mapHash, voyageRandomState, bossTreasureRandomState, encounterRandomState;
        public bool hasEncounterRandom;
        public string selectedChartSeed, activeChartSeed;
        public float x, y, targetX, targetY, totalDistance;
        public bool hasDirectTarget;
        public double totalHours, elapsedGameHours, rationProgress;
        public int hull, morale, ordinaryCrew, specialCrew;
        public float lastVoyageExplorationCoverage;
        public bool hasLastVoyageExplorationCoverage;
        public bool navigator, carpenter, doctor, gunner, cook, diver;
        public int componentHull, componentArmor, componentFirepower, contextHull, contextArmor, contextFirepower;
        public float componentSpeed, componentVision, contextSpeed, contextVision, contextRations;
        public float battleBowQuality, battleHullQuality;
        public CoreVoyageCargoCheckpoint cargo, departureCargo;
        public bool[] exploredCells;
        // Optional in version 1. Missing legacy evidence starts empty rather
        // than treating previously imported chart knowledge as this voyage's work.
        public bool[] voyageExploredCells;
        // Optional in version 1: older checkpoints only stored terrain-cell knowledge.
        public bool[] chartExploredCells;
        public CoreVoyageNodeCheckpoint[] nodes;
        public int activeNodeId = -1;
        public string activeEncounterId;
        public bool bossDefeated, randomAmbushOccurred, lastBattleWasCaptured, lastBattleRetreated;
        public bool pendingBoardingComponentDrop;
        public int pendingBoardingComponentSlot = -1;
        public int battleTerrain;
        public string[] chartRandomOrder, chartRandomEncounterIds;
        public int chartRandomIndex, chartCompletedRemovedNodes;
        public float nextChartRandomDistance;
        public CoreSeaChartProgress seaChartProgress;
        public CoreTreasureProgress treasureProgress;
        // Unity's inline serializer may materialize null nested classes as empty objects.
        public bool isTreasureVoyage;
        public CoreTreasureMapSave activeTreasureMap;
        public int treasureClues, voyageTreasureFragments;
        public bool treasureAdventureCompleted;
        public int guidedTutorialLeg;
        // Missing in legacy JSON: zero preserves the old one-event navigator route.
        public int guidedTutorialLayoutVersion;
        public string[] guidedTutorialCompletedIds;
        public bool guidedTutorialRandomEventTriggered, guidedTutorialAmbushTriggered, guidedTutorialBossDefeated;
        public string[] voyageSpecialCards, acquiredSpecialCards, activeSpecialCrewIds, activeSpecialComponentIds, componentBattleTypes;
        public bool explicitSpecialCardSources;
        public double specialCardDistanceRemainder;
        public CoreVoyageValueCheckpoint[] specialCardExperience, cardUpgradeLevels;
        public CoreVoyageDeckCheckpoint[] battleDecks;
        public string[] logs;
    }

    public sealed partial class CoreVoyageSession
    {
        public bool CanCaptureVoyageCheckpoint => Map != null && departureCargo != null && voyageRandom != null &&
            (Phase == CoreDemoPhase.Sailing || Phase == CoreDemoPhase.Encounter) && Battle == null && Settlement == null;

        /// <summary>Captures a stable sailing/encounter boundary without settling or spending anything.</summary>
        public CoreVoyageCheckpoint CaptureVoyageCheckpoint()
        {
            if (!CanCaptureVoyageCheckpoint) throw new InvalidOperationException("只能在航行或事件选择时保存当前航程。");
            return new CoreVoyageCheckpoint
            {
                phase = (int)Phase, routeIndex = SelectedRouteIndex, shipIndex = SelectedShipIndex,
                mapGeometryVersion = Map.GeometryVersion,
                selectedSeed = SelectedSeed, selectedDestinationPort = SelectedDestinationPort, mapSeed = Map.Seed, mapHash = Map.Hash,
                departurePort = Map.DepartureName, destinationPort = Map.DestinationName,
                profileGold = ProfileGold.ToString(CultureInfo.InvariantCulture),
                selectedChartSeed = selectedSeaChart?.Seed ?? string.Empty, activeChartSeed = activeSeaChart?.Seed ?? string.Empty,
                voyageRandomState = voyageRandom.State, bossTreasureRandomState = bossTreasureRandom.State,
                hasEncounterRandom = encounterRandom != null, encounterRandomState = encounterRandom?.State ?? 0,
                x = Position.X, y = Position.Y, heading = (int)Heading, plannedDistance = PlannedDistanceNm,
                hasDirectTarget = HasDirectCourseTarget,
                targetX = HasDirectCourseTarget ? DirectCourseTarget.X : 0f,
                targetY = HasDirectCourseTarget ? DirectCourseTarget.Y : 0f,
                totalDistance = TotalDistanceNm, totalHours = TotalHours, elapsedGameHours = ElapsedGameHours, rationProgress = rationProgress,
                hull = Hull, morale = Morale, lastVoyageExplorationCoverage = lastVoyageExplorationCoverage,
                hasLastVoyageExplorationCoverage = hasLastVoyageExplorationCoverage,
                ordinaryCrew = selectedOrdinaryCrew, specialCrew = selectedSpecialCrewCount,
                navigator = hasNavigator, carpenter = hasCarpenter, doctor = hasDoctor, gunner = hasGunner, cook = hasCook, diver = hasDiver,
                componentHull = componentHullBonus, componentArmor = componentArmorBonus, componentFirepower = componentFirepowerBonus,
                componentSpeed = componentSpeedBonus, componentVision = componentVisionBonus,
                contextHull = componentContextHullBonus, contextArmor = componentContextArmorBonus, contextFirepower = componentContextFirepowerBonus,
                contextSpeed = componentContextSpeedBonus, contextVision = componentContextVisionBonus, contextRations = componentContextRationMultiplier,
                battleBowQuality = battleBowQuality, battleHullQuality = battleHullQuality,
                cargo = CaptureCheckpointCargo(Cargo), departureCargo = CaptureCheckpointCargo(departureCargo),
                exploredCells = (bool[])Map.ExploredCells.Clone(),
                voyageExploredCells = (bool[])Map.VoyageExploredCells.Clone(),
                chartExploredCells = Map.CaptureChartExploration(),
                nodes = Map.Nodes.Select(n => new CoreVoyageNodeCheckpoint { id = n.Id, encounterId = n.Encounter.Id,
                    x = n.Position.X, y = n.Position.Y, discovered = n.Discovered, intelRevealed = n.IntelRevealed,
                    visited = n.Visited, skipped = n.Skipped }).ToArray(),
                activeNodeId = activeNode?.Id ?? -1, activeEncounterId = ActiveEncounter?.Id ?? string.Empty,
                bossDefeated = bossDefeated, randomAmbushOccurred = randomAmbushOccurred, lastBattleWasCaptured = LastBattleWasCaptured,
                lastBattleRetreated = LastBattleRetreated, battleTerrain = (int)BattleTerrain,
                pendingBoardingComponentDrop = pendingBoardingComponentDrop, pendingBoardingComponentSlot = pendingBoardingComponentSlot,
                chartRandomOrder = (string[])chartRandomOrder.Clone(), chartRandomEncounterIds = chartRandomEncounters.Select(e => e.Id).ToArray(),
                chartRandomIndex = chartRandomIndex, chartCompletedRemovedNodes = seaChartCompletedRemovedNodes,
                nextChartRandomDistance = nextChartRandomDistance, seaChartProgress = CaptureSeaChartProgress(), treasureProgress = CaptureTreasureProgress(),
                isTreasureVoyage = IsTreasureVoyage,
                activeTreasureMap = ActiveTreasureMap == null ? null : new CoreTreasureMapSave { id = ActiveTreasureMap.Id,
                    seed = ActiveTreasureMap.Seed, rankId = ActiveTreasureMap.RankId,
                    mapGeometryVersion = ActiveTreasureMap.MapGeometryVersion },
                treasureClues = TreasureClues, voyageTreasureFragments = voyageTreasureFragments, treasureAdventureCompleted = TreasureAdventureCompleted,
                guidedTutorialLeg = GuidedTutorialLeg, guidedTutorialCompletedIds = guidedTutorialCompletedIds.ToArray(),
                guidedTutorialLayoutVersion = guidedTutorialLayoutVersion,
                guidedTutorialRandomEventTriggered = GuidedTutorialRandomEventTriggered, guidedTutorialAmbushTriggered = GuidedTutorialAmbushTriggered,
                guidedTutorialBossDefeated = GuidedTutorialBossDefeated, voyageSpecialCards = voyageSpecialCards.OrderBy(x => x).ToArray(),
                acquiredSpecialCards = acquiredSpecialCards.OrderBy(x => x).ToArray(),
                activeSpecialCrewIds = activeSpecialCrewIds.OrderBy(x => x).ToArray(), activeSpecialComponentIds = activeSpecialComponentIds.OrderBy(x => x).ToArray(),
                componentBattleTypes = componentBattleTypes.OrderBy(x => x).ToArray(), explicitSpecialCardSources = explicitSpecialCardSources,
                specialCardDistanceRemainder = specialCardDistanceRemainder,
                specialCardExperience = CaptureCheckpointValues(specialCardExperience), cardUpgradeLevels = CaptureCheckpointValues(cardUpgradeLevels),
                battleDecks = battleDecks.Select(deck => new CoreVoyageDeckCheckpoint { cards = deck.ToArray() }).ToArray(), logs = logEntries.ToArray(),
            };
        }

        /// <summary>Apply only after loading the matching profile. Validation occurs in a detached session,
        /// so corrupt/incompatible checkpoints never partially alter the existing profile or consume a map.</summary>
        public bool TryRestoreVoyageCheckpoint(CoreVoyageCheckpoint checkpoint, out string reason)
        {
            reason = "航程存档损坏或版本不兼容，原存档已保留。";
            if (Phase != CoreDemoPhase.Port) { reason = "只能在载入游戏时恢复航程。"; return false; }
            if (!TryBuildCheckpointSession(checkpoint, out CoreVoyageSession restored)) return false;
            ApplyCheckpointSession(restored);
            reason = string.Empty;
            return true;
        }

        private static bool TryBuildCheckpointSession(CoreVoyageCheckpoint c, out CoreVoyageSession s)
        {
            s = null;
            if (c == null || !ValidMapGeometryVersion(c.mapGeometryVersion)) return false;
            var geometry = new CoreVoyageMap(c.mapGeometryVersion);
            if (c == null || c.version != 1 || (c.phase != (int)CoreDemoPhase.Sailing && c.phase != (int)CoreDemoPhase.Encounter) ||
                c.routeIndex < 0 || c.routeIndex >= routes.Count || c.shipIndex < 0 || c.shipIndex >= ships.Count ||
                !portNames.Contains(c.departurePort) || !portNames.Contains(c.selectedDestinationPort) ||
                c.selectedDestinationPort == c.departurePort || string.IsNullOrWhiteSpace(c.destinationPort) ||
                string.IsNullOrWhiteSpace(c.selectedSeed) || c.selectedSeed.Length > 512 || string.IsNullOrWhiteSpace(c.mapSeed) || c.mapSeed.Length > 512 ||
                !decimal.TryParse(c.profileGold, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal gold) || gold < 0 || gold > int.MaxValue ||
                !CheckpointPoint(c.x, c.y, geometry) || !CheckpointPoint(c.targetX, c.targetY, geometry) || c.heading < 0 || c.heading > 7 ||
                c.plannedDistance < MinimumDistanceNm || c.plannedDistance > MaximumDistanceNm ||
                !CheckpointFinite(c.totalDistance) || c.totalDistance < 0 || !IsValidGameDuration(c.totalHours) ||
                !IsValidGameDuration(c.elapsedGameHours) || c.elapsedGameHours >= GameCalendarCycleHours ||
                !IsValidGameDuration(c.rationProgress) ||
                c.morale < 0 || c.morale > MaxMorale || !CheckpointFinite(c.lastVoyageExplorationCoverage) ||
                c.lastVoyageExplorationCoverage < 0f || c.lastVoyageExplorationCoverage > 100f ||
                c.hull <= 0 || c.ordinaryCrew < 0 || c.ordinaryCrew > ships[c.shipIndex].CrewCount ||
                c.specialCrew < 0 || c.specialCrew > ships[c.shipIndex].CrewCount || c.guidedTutorialLeg < 0 || c.guidedTutorialLeg > CoreGuidedTutorialContent.BossVoyageLeg ||
                c.guidedTutorialLayoutVersion < 0 || c.guidedTutorialLayoutVersion > 1 ||
                c.nodes == null || c.nodes.Length > 256 || c.exploredCells == null || c.exploredCells.Length != geometry.Columns * geometry.Rows ||
                c.voyageExploredCells != null && c.voyageExploredCells.Length != 0 && c.voyageExploredCells.Length != geometry.Columns * geometry.Rows ||
                c.chartExploredCells != null && c.chartExploredCells.Length != 0 && c.chartExploredCells.Length != geometry.FineColumns * geometry.FineRows ||
                c.logs == null || c.logs.Length > 256 || c.logs.Any(log => log == null || log.Length > 8192) ||
                c.guidedTutorialCompletedIds == null || c.guidedTutorialCompletedIds.Length > 16 ||
                c.chartRandomOrder == null || c.chartRandomEncounterIds == null || c.chartRandomOrder.Length != c.chartRandomEncounterIds.Length ||
                c.chartRandomOrder.Length > SeaChartRandomTemplates.Length || c.chartRandomIndex < 0 || c.chartRandomIndex > c.chartRandomOrder.Length ||
                !CheckpointFinite(c.nextChartRandomDistance) || c.nextChartRandomDistance < 0 || c.chartCompletedRemovedNodes < 0 || c.chartCompletedRemovedNodes > 256 ||
                c.treasureClues < 0 || c.voyageTreasureFragments < 0 || c.treasureAdventureCompleted ||
                c.pendingBoardingComponentDrop && (c.pendingBoardingComponentSlot < 0 || c.pendingBoardingComponentSlot >= ComponentSlotCountForLoot()) ||
                !c.pendingBoardingComponentDrop && c.pendingBoardingComponentSlot != -1 ||
                !Enum.IsDefined(typeof(SeaTerrainType), c.battleTerrain) || !CheckpointBonuses(c) ||
                !IsValidGameDuration(c.specialCardDistanceRemainder) || c.specialCardDistanceRemainder >= 1d ||
                !TryCheckpointCargo(c.cargo, out VoyageCargo cargo) || !TryCheckpointCargo(c.departureCargo, out VoyageCargo departure)) return false;

            var candidate = new CoreVoyageSession();
            candidate.SelectedRouteIndex = c.routeIndex; candidate.SelectedShipIndex = c.shipIndex;
            candidate.SelectedSeed = c.selectedSeed; candidate.CurrentPort = c.departurePort;
            candidate.SelectedDestinationPort = c.selectedDestinationPort;
            candidate.ProfileGold = gold; candidate.GuidedTutorialLeg = c.guidedTutorialLeg;
            candidate.guidedTutorialLayoutVersion = c.guidedTutorialLayoutVersion;
            if (!candidate.RestoreSeaChartProgress(c.seaChartProgress) || !ValidateCheckpointTreasure(c.treasureProgress)) return false;
            candidate.RestoreTreasureProgress(c.treasureProgress);
            if (!string.IsNullOrEmpty(c.selectedChartSeed) && !TryParseChartSeed(c.selectedChartSeed, out candidate.selectedSeaChart)) return false;
            if (!string.IsNullOrEmpty(c.activeChartSeed) && !TryParseChartSeed(c.activeChartSeed, out candidate.activeSeaChart)) return false;
            if (candidate.activeSeaChart != null && (!candidate.activeSeaChart.ContainsPort(c.departurePort) ||
                candidate.activeSeaChart.OtherPort(c.departurePort) != c.destinationPort || candidate.activeSeaChart.GeographyHash != c.mapHash ||
                candidate.activeSeaChart.MapGeometryVersion != c.mapGeometryVersion ||
                candidate.ActiveSeaChartState == null)) return false;
            if (candidate.selectedSeaChart != null && (!candidate.selectedSeaChart.ContainsPort(c.departurePort) ||
                candidate.selectedSeaChart.RouteIndex != c.routeIndex ||
                !c.isTreasureVoyage && candidate.selectedSeaChart.MapGeometryVersion != c.mapGeometryVersion)) return false;

            if (c.isTreasureVoyage)
            {
                CoreTreasureMapSave map = c.activeTreasureMap;
                if (!ValidCheckpointMap(map) || map.mapGeometryVersion != c.mapGeometryVersion || c.guidedTutorialLeg != 0 || candidate.activeSeaChart != null ||
                    candidate.treasureMaps.Any(m => m.Id == map.id || m.Seed == map.seed) || candidate.completedTreasureSeeds.Contains(map.seed) ||
                    !candidate.issuedTreasureSeeds.Contains(map.seed)) return false;
                CoreUsedTreasureMapRecord archived = candidate.usedTreasureMaps.Find(m => m.Seed == map.seed);
                if (archived?.Rank != null && (archived.Id != map.id || archived.RankId != map.rankId ||
                    archived.MapGeometryVersion != map.mapGeometryVersion)) return false;
                candidate.ActiveTreasureMap = new CoreTreasureMapRecord(map.id, map.seed, map.rankId, map.mapGeometryVersion);
                candidate.Map = candidate.GenerateTreasureMap(candidate.ActiveTreasureMap, c.departurePort);
            }
            else if (c.guidedTutorialLeg > 0)
                candidate.Map = candidate.GenerateGuidedTutorialMap(c.departurePort, c.destinationPort);
            else if (candidate.selectedSeaChart != null)
                candidate.Map = candidate.GenerateSelectedSeaChartMap();
            else
                candidate.Map = candidate.GenerateMap(routes[c.routeIndex], c.mapSeed, c.departurePort, c.destinationPort,
                    geometryVersion: c.mapGeometryVersion);
            if (candidate.Map.GeometryVersion != c.mapGeometryVersion || candidate.Map.Hash != c.mapHash ||
                candidate.Map.Seed != c.mapSeed || candidate.Map.DestinationName != c.destinationPort) return false;
            if (candidate.ActiveSeaChartState != null) candidate.RemoveDefeatedSeaChartBosses(candidate.ActiveSeaChartState);
            if (candidate.Map.Nodes.Count != c.nodes.Length) return false;
            var ids = new HashSet<int>();
            foreach (CoreVoyageNodeCheckpoint n in c.nodes)
            {
                if (n == null || !ids.Add(n.id)) return false;
                CoreMapNode target = candidate.Map.Nodes.Find(node => node.Id == n.id);
                if (target == null || target.Encounter.Id != n.encounterId || !CheckpointPoint(n.x, n.y, geometry) ||
                    Math.Abs(target.Position.X - n.x) > .001f || Math.Abs(target.Position.Y - n.y) > .001f ||
                    n.skipped && !n.visited && !CanRevisitEncounter(target.Encounter)) return false;
                target.Discovered = n.discovered; target.IntelRevealed = n.intelRevealed; target.Visited = n.visited; target.Skipped = n.skipped;
            }
            Array.Copy(c.exploredCells, candidate.Map.ExploredCells, c.exploredCells.Length);
            if (c.voyageExploredCells != null && c.voyageExploredCells.Length != 0)
                Array.Copy(c.voyageExploredCells, candidate.Map.VoyageExploredCells, c.voyageExploredCells.Length);
            if (!candidate.Map.RestoreChartExploration(c.chartExploredCells)) return false;
            // Legacy sailing checkpoints retained the active rank and actual explored cells,
            // so these records can be recovered without inventing historical information.
            candidate.UpdateActiveUsedTreasureMapCoverage();
            candidate.Cargo = cargo; candidate.departureCargo = departure;
            candidate.Position = new VoyagePoint(c.x, c.y); candidate.Heading = (SailingDirection)c.heading;
            candidate.PlannedDistanceNm = c.plannedDistance; candidate.HasDirectCourseTarget = c.hasDirectTarget;
            candidate.DirectCourseTarget = new VoyagePoint(c.targetX, c.targetY);
            candidate.TotalDistanceNm = c.totalDistance; candidate.TotalHours = c.totalHours; candidate.ElapsedGameHours = c.elapsedGameHours;
            candidate.rationProgress = c.rationProgress; candidate.Hull = c.hull; candidate.Morale = c.morale;
            candidate.lastVoyageExplorationCoverage = c.lastVoyageExplorationCoverage;
            candidate.hasLastVoyageExplorationCoverage = c.hasLastVoyageExplorationCoverage;
            candidate.selectedOrdinaryCrew = c.ordinaryCrew; candidate.selectedSpecialCrewCount = c.specialCrew;
            candidate.hasNavigator = c.navigator; candidate.hasCarpenter = c.carpenter; candidate.hasDoctor = c.doctor;
            candidate.hasGunner = c.gunner; candidate.hasCook = c.cook; candidate.hasDiver = c.diver;
            candidate.componentHullBonus = c.componentHull; candidate.componentArmorBonus = c.componentArmor; candidate.componentFirepowerBonus = c.componentFirepower;
            candidate.componentSpeedBonus = c.componentSpeed; candidate.componentVisionBonus = c.componentVision;
            candidate.componentContextHullBonus = c.contextHull; candidate.componentContextArmorBonus = c.contextArmor; candidate.componentContextFirepowerBonus = c.contextFirepower;
            candidate.componentContextSpeedBonus = c.contextSpeed; candidate.componentContextVisionBonus = c.contextVision; candidate.componentContextRationMultiplier = c.contextRations;
            candidate.battleBowQuality = c.battleBowQuality; candidate.battleHullQuality = c.battleHullQuality;
            // A weather bonus can expire after a repair without clamping the current hull.
            if (candidate.Hull > Math.Max(candidate.MaxHull, candidate.SelectedShip.MaxHull + c.componentHull + 20)) return false;
            candidate.voyageRandom = new Mulberry32Random(c.voyageRandomState); candidate.bossTreasureRandom = new Mulberry32Random(c.bossTreasureRandomState);
            candidate.encounterRandom = c.hasEncounterRandom ? new Mulberry32Random(c.encounterRandomState) : null;
            candidate.chartRandomOrder = (string[])c.chartRandomOrder.Clone();
            candidate.chartRandomEncounters = new CoreEncounter[c.chartRandomOrder.Length];
            if (c.chartRandomOrder.Distinct().Count() != c.chartRandomOrder.Length ||
                (candidate.activeSeaChart != null && c.guidedTutorialLeg == 0) != c.hasEncounterRandom ||
                c.hasEncounterRandom && c.chartRandomOrder.Length != SeaChartRandomTemplates.Length ||
                !c.hasEncounterRandom && c.chartRandomOrder.Length != 0) return false;
            for (int i = 0; i < c.chartRandomOrder.Length; i++)
            {
                string key = c.chartRandomOrder[i], id = c.chartRandomEncounterIds[i];
                if (!IsKnownChartRandomEvent(key, false) || (key == "random_ambush" ? id != key && id != "random_ambush_warning" : id != key)) return false;
                candidate.chartRandomEncounters[i] = key == "random_ambush" ? candidate.CreateRandomAmbush(id == "random_ambush_warning") : candidate.CreateSeaChartRandomEncounter(key);
                candidate.chartRandomEncounters[i].RepeatPolicy = CoreEncounterRepeatPolicy.RandomVoyage;
            }
            candidate.chartRandomIndex = c.chartRandomIndex; candidate.nextChartRandomDistance = c.nextChartRandomDistance;
            candidate.seaChartCompletedRemovedNodes = c.chartCompletedRemovedNodes;
            candidate.activeNode = c.activeNodeId < 0 ? null : candidate.Map.Nodes.Find(n => n.Id == c.activeNodeId);
            if (c.phase == (int)CoreDemoPhase.Sailing)
            { if (c.activeNodeId != -1 || !string.IsNullOrEmpty(c.activeEncounterId)) return false; }
            else
            {
                if (c.activeNodeId >= 0)
                {
                    if (candidate.activeNode == null || candidate.activeNode.Visited || candidate.activeNode.Encounter.Id != c.activeEncounterId) return false;
                    candidate.ActiveEncounter = candidate.activeNode.Encounter;
                }
                else if (c.activeNodeId != -1) return false;
                else if (candidate.UsesCombinedGuidedTutorialVoyage && c.activeEncounterId == CoreGuidedTutorialContent.AmbushEventId &&
                    c.guidedTutorialAmbushTriggered && c.guidedTutorialCompletedIds.Contains(CoreGuidedTutorialContent.NavigatorEventId) &&
                    !c.guidedTutorialCompletedIds.Contains(CoreGuidedTutorialContent.AmbushEventId))
                    candidate.ActiveEncounter = CoreGuidedTutorialContent.AmbushEvent();
                else if (candidate.activeSeaChart != null && c.guidedTutorialLeg == 0)
                {
                    if (c.chartRandomIndex < 1 || candidate.chartRandomEncounters[c.chartRandomIndex - 1].Id != c.activeEncounterId) return false;
                    candidate.ActiveEncounter = candidate.chartRandomEncounters[c.chartRandomIndex - 1];
                }
                else if (c.activeEncounterId == "random_ambush" || c.activeEncounterId == "random_ambush_warning")
                    candidate.ActiveEncounter = candidate.CreateRandomAmbush(c.activeEncounterId == "random_ambush_warning");
                else return false;
            }
            candidate.bossDefeated = c.bossDefeated; candidate.randomAmbushOccurred = c.randomAmbushOccurred;
            candidate.LastBattleWasCaptured = c.lastBattleWasCaptured; candidate.LastBattleRetreated = c.lastBattleRetreated;
            candidate.pendingBoardingComponentDrop = c.pendingBoardingComponentDrop; candidate.pendingBoardingComponentSlot = c.pendingBoardingComponentSlot;
            candidate.BattleTerrain = (SeaTerrainType)c.battleTerrain;
            candidate.TreasureClues = c.treasureClues; candidate.voyageTreasureFragments = c.voyageTreasureFragments;
            candidate.TreasureAdventureCompleted = c.treasureAdventureCompleted;
            string[] tutorialIds = { CoreGuidedTutorialContent.FirstEventId, CoreGuidedTutorialContent.OptionalEventId,
                CoreGuidedTutorialContent.CrewRequirementEventId, CoreGuidedTutorialContent.NavigatorEventId,
                CoreGuidedTutorialContent.RandomEventId, CoreGuidedTutorialContent.AmbushEventId, CoreGuidedTutorialContent.BossEventId };
            if (c.guidedTutorialCompletedIds.Any(id => !tutorialIds.Contains(id)) || c.guidedTutorialCompletedIds.Distinct().Count() != c.guidedTutorialCompletedIds.Length) return false;
            candidate.guidedTutorialCompletedIds.AddRange(c.guidedTutorialCompletedIds);
            candidate.GuidedTutorialRandomEventTriggered = c.guidedTutorialRandomEventTriggered;
            candidate.GuidedTutorialAmbushTriggered = c.guidedTutorialAmbushTriggered; candidate.GuidedTutorialBossDefeated = c.guidedTutorialBossDefeated;
            if (!RestoreCheckpointCards(candidate, c)) return false;
            candidate.logEntries.Clear(); candidate.logEntries.AddRange(c.logs);
            candidate.Phase = (CoreDemoPhase)c.phase;
            s = candidate;
            return true;
        }

        private void ApplyCheckpointSession(CoreVoyageSession s)
        {
            // All parsing, reconstruction and allocation has succeeded. Profile-owned warehouses,
            // collection records and presentation inventories stay with the loaded profile.
            SelectedRouteIndex = s.SelectedRouteIndex; SelectedShipIndex = s.SelectedShipIndex; SelectedSeed = s.SelectedSeed;
            CurrentPort = s.CurrentPort; SelectedDestinationPort = s.SelectedDestinationPort; ProfileGold = s.ProfileGold;
            Map = s.Map; Cargo = s.Cargo; departureCargo = s.departureCargo;
            Position = s.Position; Heading = s.Heading; PlannedDistanceNm = s.PlannedDistanceNm;
            HasDirectCourseTarget = s.HasDirectCourseTarget; DirectCourseTarget = s.DirectCourseTarget;
            TotalDistanceNm = s.TotalDistanceNm; TotalHours = s.TotalHours; ElapsedGameHours = s.ElapsedGameHours;
            rationProgress = s.rationProgress; Hull = s.Hull; Morale = s.Morale;
            lastVoyageExplorationCoverage = s.lastVoyageExplorationCoverage;
            hasLastVoyageExplorationCoverage = s.hasLastVoyageExplorationCoverage;
            selectedOrdinaryCrew = s.selectedOrdinaryCrew; selectedSpecialCrewCount = s.selectedSpecialCrewCount;
            hasNavigator = s.hasNavigator; hasCarpenter = s.hasCarpenter; hasDoctor = s.hasDoctor; hasGunner = s.hasGunner; hasCook = s.hasCook; hasDiver = s.hasDiver;
            componentHullBonus = s.componentHullBonus; componentArmorBonus = s.componentArmorBonus; componentFirepowerBonus = s.componentFirepowerBonus;
            componentSpeedBonus = s.componentSpeedBonus; componentVisionBonus = s.componentVisionBonus;
            componentContextHullBonus = s.componentContextHullBonus; componentContextArmorBonus = s.componentContextArmorBonus; componentContextFirepowerBonus = s.componentContextFirepowerBonus;
            componentContextSpeedBonus = s.componentContextSpeedBonus; componentContextVisionBonus = s.componentContextVisionBonus; componentContextRationMultiplier = s.componentContextRationMultiplier;
            battleBowQuality = s.battleBowQuality; battleHullQuality = s.battleHullQuality;
            voyageRandom = s.voyageRandom; bossTreasureRandom = s.bossTreasureRandom; encounterRandom = s.encounterRandom;
            activeNode = s.activeNode; ActiveEncounter = s.ActiveEncounter; Battle = null; Settlement = null;
            pendingBattleReward = null; pendingSeaChartClaim = false; pendingSeaChartRewardMultiplier = 1d;
            bossDefeated = s.bossDefeated; randomAmbushOccurred = s.randomAmbushOccurred;
            LastBattleWasCaptured = s.LastBattleWasCaptured; LastBattleRetreated = s.LastBattleRetreated; BattleTerrain = s.BattleTerrain;
            pendingBoardingComponentDrop = s.pendingBoardingComponentDrop; pendingBoardingComponentSlot = s.pendingBoardingComponentSlot;
            selectedSeaChart = s.selectedSeaChart; activeSeaChart = s.activeSeaChart;
            seaChartStates.Clear(); foreach (var pair in s.seaChartStates) seaChartStates.Add(pair.Key, pair.Value);
            seaChartAliases.Clear(); seaChartAliases.AddRange(s.seaChartAliases); TouchSeaChartProgress();
            chartRandomOrder = s.chartRandomOrder; chartRandomEncounters = s.chartRandomEncounters;
            chartRandomIndex = s.chartRandomIndex; nextChartRandomDistance = s.nextChartRandomDistance; seaChartCompletedRemovedNodes = s.seaChartCompletedRemovedNodes;
            PirateExperience = s.PirateExperience; TreasureFragments = s.TreasureFragments; MoonlitPityCount = s.MoonlitPityCount;
            identifiedTreasureFragments.Clear(); foreach (var pair in s.identifiedTreasureFragments) identifiedTreasureFragments.Add(pair.Key, pair.Value);
            treasureIdentificationSeed = s.treasureIdentificationSeed; treasureIdentificationSequence = s.treasureIdentificationSequence;
            treasureMaps.Clear(); treasureMaps.AddRange(s.treasureMaps); completedTreasureSeeds.Clear(); completedTreasureSeeds.AddRange(s.completedTreasureSeeds);
            usedTreasureMaps.Clear(); usedTreasureMaps.AddRange(s.usedTreasureMaps);
            issuedTreasureSeeds.Clear(); issuedTreasureSeeds.UnionWith(s.issuedTreasureSeeds); selectedTreasureMapId = s.selectedTreasureMapId;
            ActiveTreasureMap = s.ActiveTreasureMap; TreasureClues = s.TreasureClues; voyageTreasureFragments = s.voyageTreasureFragments; TreasureAdventureCompleted = s.TreasureAdventureCompleted;
            GuidedTutorialLeg = s.GuidedTutorialLeg; guidedTutorialCompletedIds.Clear(); guidedTutorialCompletedIds.AddRange(s.guidedTutorialCompletedIds);
            guidedTutorialLayoutVersion = s.guidedTutorialLayoutVersion;
            GuidedTutorialRandomEventTriggered = s.GuidedTutorialRandomEventTriggered; GuidedTutorialAmbushTriggered = s.GuidedTutorialAmbushTriggered; GuidedTutorialBossDefeated = s.GuidedTutorialBossDefeated;
            CopyCheckpointSet(voyageSpecialCards, s.voyageSpecialCards); CopyCheckpointSet(acquiredSpecialCards, s.acquiredSpecialCards);
            CopyCheckpointSet(activeSpecialCrewIds, s.activeSpecialCrewIds); CopyCheckpointSet(activeSpecialComponentIds, s.activeSpecialComponentIds);
            CopyCheckpointSet(componentBattleTypes, s.componentBattleTypes); explicitSpecialCardSources = s.explicitSpecialCardSources;
            specialCardDistanceRemainder = s.specialCardDistanceRemainder;
            specialCardExperience.Clear(); foreach (var pair in s.specialCardExperience) specialCardExperience.Add(pair.Key, pair.Value);
            cardUpgradeLevels.Clear(); foreach (var pair in s.cardUpgradeLevels) cardUpgradeLevels.Add(pair.Key, pair.Value);
            for (int i = 0; i < battleDecks.Length; i++) { battleDecks[i].Clear(); battleDecks[i].AddRange(s.battleDecks[i]); }
            logEntries.Clear(); logEntries.AddRange(s.logEntries);
            Phase = s.Phase;
        }

        private static bool RestoreCheckpointCards(CoreVoyageSession s, CoreVoyageCheckpoint c)
        {
            bool ValidSet(string[] values, Func<string, bool> valid) => values != null && values.Length <= cardLibrary.Count &&
                values.Distinct(StringComparer.Ordinal).Count() == values.Length && values.All(value => value != null && valid(value));
            if (!ValidSet(c.voyageSpecialCards, IsSpecialBattleCard) || !ValidSet(c.acquiredSpecialCards, IsSpecialBattleCard) ||
                !ValidSet(c.activeSpecialCrewIds, id => CoreSpecialCardCatalog.ForCrew(id) != null) ||
                !ValidSet(c.activeSpecialComponentIds, id => CoreSpecialCardCatalog.ForComponent(id) != null) ||
                !ValidSet(c.componentBattleTypes, id => new[] { "bow", "sail", "cannon", "plating", "instrument" }.Contains(id)) ||
                c.battleDecks == null || c.battleDecks.Length != 3 || c.specialCardExperience == null || c.cardUpgradeLevels == null ||
                c.specialCardExperience.Length > cardLibrary.Count || c.cardUpgradeLevels.Length > cardLibrary.Count) return false;
            for (int i = 0; i < 3; i++)
            {
                if (c.battleDecks[i] == null) return false;
                NavalBattleStage stage = (NavalBattleStage)i;
                int[] saved = c.battleDecks[i].cards;
                // Version 1 saves used 8–10 base cards and up to three copies.
                // Accept that old shape without accepting corrupt card membership.
                if (!IsValidBaseDeck(saved, stage) && !IsValidBaseDeck(saved, stage, 8, 10, 3)) return false;
                List<int> restored = s.BuildRestoredBattleDeck(stage, saved);
                if (!IsValidBaseDeck(restored, stage)) return false;
                s.battleDecks[i].Clear(); s.battleDecks[i].AddRange(restored);
            }
            foreach (CoreVoyageValueCheckpoint item in c.specialCardExperience)
            {
                if (item == null || !IsSpecialBattleCard(item.id) || item.value < 0 || s.specialCardExperience.ContainsKey(item.id)) return false;
                s.specialCardExperience.Add(item.id, item.value);
            }
            foreach (CoreVoyageValueCheckpoint item in c.cardUpgradeLevels)
            {
                if (item == null || item.id == null || !cardLibrary.Any(card => card.Id == item.id && card.AllowedStages != NavalBattleStages.None) ||
                    IsSpecialBattleCard(item.id) || item.value < 0 || item.value > CoreBattleUpgradeBalance.MaximumLevel || s.cardUpgradeLevels.ContainsKey(item.id)) return false;
                s.cardUpgradeLevels.Add(item.id, item.value);
            }
            s.voyageSpecialCards.UnionWith(c.voyageSpecialCards); s.acquiredSpecialCards.UnionWith(c.acquiredSpecialCards);
            s.activeSpecialCrewIds.UnionWith(c.activeSpecialCrewIds); s.activeSpecialComponentIds.UnionWith(c.activeSpecialComponentIds);
            s.componentBattleTypes.UnionWith(c.componentBattleTypes); s.explicitSpecialCardSources = c.explicitSpecialCardSources;
            s.specialCardDistanceRemainder = c.specialCardDistanceRemainder;
            return true;
        }

        private static bool CheckpointBonuses(CoreVoyageCheckpoint c) =>
            new[] { c.componentHull, c.componentArmor, c.componentFirepower, c.contextHull, c.contextArmor, c.contextFirepower }.All(value => value >= -100000 && value <= 100000) &&
            new[] { c.componentSpeed, c.componentVision, c.contextSpeed, c.contextVision, c.contextRations, c.battleBowQuality, c.battleHullQuality }.All(CheckpointFinite) &&
            c.contextRations > 0 && c.contextRations <= 100 && c.battleBowQuality > 0 && c.battleBowQuality <= 100 && c.battleHullQuality > 0 && c.battleHullQuality <= 100;

        private static bool ValidateCheckpointTreasure(CoreTreasureProgress p) => p != null && p.pirateExperience >= 0 && p.fragments >= 0 &&
            p.identifiedWeatheredFragments >= 0 && p.identifiedGildedFragments >= 0 && p.identifiedMoonlitFragments >= 0 && p.identificationSequence >= 0 &&
            (string.IsNullOrWhiteSpace(p.identificationSeed) || ValidCheckpointSeed(p.identificationSeed)) &&
            p.moonlitPityCount >= 0 && p.moonlitPityCount <= CoreTreasureHuntCatalog.MoonlitPityCap && p.maps != null && p.maps.Length <= CoreTreasureHuntCatalog.MaxHeldMaps &&
            p.maps.All(ValidCheckpointMap) && p.maps.Select(m => m.id).Distinct().Count() == p.maps.Length && p.maps.Select(m => m.seed).Distinct().Count() == p.maps.Length &&
            (p.usedMaps == null || p.usedMaps.All(ValidCheckpointUsedMap) &&
                p.usedMaps.Select(m => m.id).Distinct().Count() == p.usedMaps.Length && p.usedMaps.Select(m => m.seed).Distinct().Count() == p.usedMaps.Length &&
                !p.maps.Any(m => p.usedMaps.Any(used => used.seed == m.seed || used.id == m.id))) &&
            p.completedSeeds != null && p.issuedSeeds != null && p.completedSeeds.All(ValidCheckpointSeed) && p.issuedSeeds.All(ValidCheckpointSeed) &&
            !p.maps.Any(m => p.completedSeeds.Contains(m.seed));
        private static bool ValidCheckpointUsedMap(CoreUsedTreasureMapSave m) => m != null && ValidCheckpointSeed(m.id) && ValidCheckpointSeed(m.seed) &&
            ValidMapGeometryVersion(m.mapGeometryVersion) &&
            (string.IsNullOrEmpty(m.rankId) || CoreTreasureHuntCatalog.FindRank(m.rankId) != null) &&
            (string.IsNullOrEmpty(m.departurePort) || portNames.Contains(m.departurePort)) &&
            CheckpointFinite(m.explorationCoverage) && m.explorationCoverage >= 0f && m.explorationCoverage <= 100f;
        private static bool ValidCheckpointMap(CoreTreasureMapSave m) => m != null && ValidCheckpointSeed(m.id) && ValidCheckpointSeed(m.seed) &&
            ValidMapGeometryVersion(m.mapGeometryVersion) && CoreTreasureHuntCatalog.FindRank(m.rankId) != null;
        private static bool ValidMapGeometryVersion(int version) => version == 0 || version == 1;
        private static bool ValidCheckpointSeed(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 512;
        private static bool CheckpointFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool CheckpointPoint(float x, float y, CoreVoyageMap map) => CheckpointFinite(x) && CheckpointFinite(y) &&
            x >= 0 && x <= map.WorldWidth && y >= 0 && y <= map.WorldHeight;
        private static void CopyCheckpointSet(HashSet<string> target, HashSet<string> source) { target.Clear(); target.UnionWith(source); }
        private static CoreVoyageValueCheckpoint[] CaptureCheckpointValues(Dictionary<string, int> values) => values.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new CoreVoyageValueCheckpoint { id = p.Key, value = p.Value }).ToArray();

        private static CoreVoyageCargoCheckpoint CaptureCheckpointCargo(VoyageCargo c) => new CoreVoyageCargoCheckpoint
        {
            water = c.Water, food = c.Food, beer = c.Beer, medicine = c.Medicine, wood = c.Wood, iron = c.Iron, cloth = c.Cloth,
            powder = c.Powder, cannonballs = c.Cannonballs, gold = c.Gold, fame = c.Fame, gem = c.Gem, ancientMaterial = c.AncientMaterial,
            relic = c.Relic, blackPearl = c.BlackPearl, ancientCompass = c.AncientCompass, lantern = c.Lantern, curse = c.Curse,
        };

        private static bool TryCheckpointCargo(CoreVoyageCargoCheckpoint c, out VoyageCargo cargo)
        {
            cargo = null;
            if (c == null || new[] { c.water, c.food, c.beer, c.medicine, c.wood, c.iron, c.cloth, c.powder, c.cannonballs,
                c.gold, c.fame, c.gem, c.ancientMaterial, c.relic, c.blackPearl, c.ancientCompass, c.lantern, c.curse }.Any(value => value < 0)) return false;
            cargo = new VoyageCargo { Water = c.water, Food = c.food, Beer = c.beer, Medicine = c.medicine, Wood = c.wood,
                Iron = c.iron, Cloth = c.cloth, Powder = c.powder, Cannonballs = c.cannonballs, Gold = c.gold, Fame = c.fame,
                Gem = c.gem, AncientMaterial = c.ancientMaterial, Relic = c.relic, BlackPearl = c.blackPearl,
                AncientCompass = c.ancientCompass, Lantern = c.lantern, Curse = c.curse };
            return true;
        }
    }
}
