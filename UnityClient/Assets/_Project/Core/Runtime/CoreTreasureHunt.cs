using System;
using System.Collections.Generic;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    [Serializable]
    public sealed class CoreTreasureMapSave
    {
        public string id;
        public string seed;
        public string rankId;
        // Missing from old saves means the original 48 x 32 sea.
        public int mapGeometryVersion;
        // Absent in old saves: retain the original shared event pool in flight and in archives.
        public int contentVersion;
    }

    [Serializable]
    public sealed class CoreUsedTreasureMapSave
    {
        public string id;
        public string seed;
        public string rankId;
        public int mapGeometryVersion;
        public int contentVersion;
        public string departurePort;
        public bool hasExplorationCoverage;
        public float explorationCoverage;
    }

    [Serializable]
    public sealed class CoreTreasureProgress
    {
        public int pirateExperience;
        // Legacy fragments remain unidentified; migration never assigns or spends them.
        public int fragments;
        public int identifiedWeatheredFragments;
        public int identifiedGildedFragments;
        public int identifiedMoonlitFragments;
        public string identificationSeed;
        public long identificationSequence;
        // Consecutive non-moonlit identifications after the moonlit rank is unlocked.
        // Missing in older saves, where JsonUtility restores the default 0.
        public int moonlitPityCount;
        public string selectedMapId;
        public CoreTreasureMapSave[] maps = Array.Empty<CoreTreasureMapSave>();
        public CoreUsedTreasureMapSave[] usedMaps = Array.Empty<CoreUsedTreasureMapSave>();
        public string[] completedSeeds = Array.Empty<string>();
        public string[] issuedSeeds = Array.Empty<string>();
        public int gems;
        public int ancientMaterials;
        public int relics;
        public int blackPearls;
        public int ancientCompasses;
        public int lanterns;
        public int curses;
    }

    public sealed partial class CoreVoyageSession
    {
        private static readonly int[] pirateThresholds = { 0, 100, 260, 520, 900, 1400, 2100, 3000, 4100, 5400 };
        private readonly List<CoreTreasureMapRecord> treasureMaps = new List<CoreTreasureMapRecord>();
        private readonly List<CoreUsedTreasureMapRecord> usedTreasureMaps = new List<CoreUsedTreasureMapRecord>();
        private readonly List<string> completedTreasureSeeds = new List<string>();
        private readonly HashSet<string> issuedTreasureSeeds = new HashSet<string>(StringComparer.Ordinal);
        private string selectedTreasureMapId = string.Empty;
        private readonly Dictionary<string, int> identifiedTreasureFragments = new Dictionary<string, int>(StringComparer.Ordinal);
        private string treasureIdentificationSeed = Guid.NewGuid().ToString("N");
        private long treasureIdentificationSequence;
        private int voyageTreasureFragments;
        public int PirateExperience { get; private set; }
        public int PirateLevel
        {
            get { int level = 1; for (int i = 1; i < pirateThresholds.Length; i++) if (PirateExperience >= pirateThresholds[i]) level = i + 1; return level; }
        }
        public int MaximumPirateLevel => pirateThresholds.Length;
        public int NextPirateLevelExperience => PirateLevel >= MaximumPirateLevel ? 0 : pirateThresholds[PirateLevel];
        public int PirateExperienceToNextLevel => Math.Max(0, NextPirateLevelExperience - PirateExperience);
        public static int PirateExperienceForLevel(int level) => pirateThresholds[Math.Max(1, Math.Min(pirateThresholds.Length, level)) - 1];
        public int TreasureFragments { get; private set; }
        public int MoonlitPityCount { get; private set; }
        public double MoonlitIdentificationChance => PirateLevel < CoreTreasureHuntCatalog.MoonlitUnlockLevel ? 0d :
            Math.Min(CoreTreasureHuntCatalog.MoonlitMaxChance,
                CoreTreasureHuntCatalog.MoonlitBaseChance + MoonlitPityCount * CoreTreasureHuntCatalog.MoonlitPityStep);
        public double MoonlitCraftChance => MoonlitIdentificationChance;
        public int VoyageTreasureFragments => voyageTreasureFragments;
        public IReadOnlyList<CoreTreasureMapRecord> TreasureMaps => treasureMaps.AsReadOnly();
        public IReadOnlyList<CoreUsedTreasureMapRecord> UsedTreasureMaps => usedTreasureMaps.AsReadOnly();
        public IReadOnlyList<string> CompletedTreasureSeeds => completedTreasureSeeds.AsReadOnly();
        public CoreTreasureMapRecord SelectedTreasureMap => treasureMaps.Find(map => map.Id == selectedTreasureMapId);
        public CoreTreasureMapRecord ActiveTreasureMap { get; private set; }
        public bool IsTreasureVoyage => Map != null && Map.IsTreasureMap;
        public int TreasureClues { get; private set; }
        public int TreasureRequiredClues => ActiveTreasureMap?.Rank.RequiredClues ?? 0;
        public bool TreasureAdventureCompleted { get; private set; }
        public double AmbushDetectionChance => Map == null ? 0d : Math.Min(.85d,
            Map.Route.AmbushDetectionChance + (HasNavigator ? .38d : 0d) +
            (IsTreasureVoyage ? Math.Min(.12d, Cargo.Lantern * .03d) : 0d));
        private int TreasureBattleTier => ActiveTreasureMap?.RankId == "moonlit" ? 2 : ActiveTreasureMap?.RankId == "gilded" ? 1 : 0;

        public CoreTreasureProgress CaptureTreasureProgress()
        {
            UpdateActiveUsedTreasureMapCoverage();
            var held = new CoreTreasureMapSave[treasureMaps.Count];
            for (int i = 0; i < held.Length; i++) held[i] = new CoreTreasureMapSave {
                id = treasureMaps[i].Id, seed = treasureMaps[i].Seed, rankId = treasureMaps[i].RankId,
                mapGeometryVersion = treasureMaps[i].MapGeometryVersion, contentVersion = treasureMaps[i].ContentVersion };
            var used = new CoreUsedTreasureMapSave[usedTreasureMaps.Count];
            for (int i = 0; i < used.Length; i++)
            {
                CoreUsedTreasureMapRecord record = usedTreasureMaps[i];
                used[i] = new CoreUsedTreasureMapSave { id = record.Id, seed = record.Seed, rankId = record.RankId,
                    mapGeometryVersion = record.MapGeometryVersion, contentVersion = record.ContentVersion,
                    departurePort = record.DeparturePort, hasExplorationCoverage = record.HasExplorationCoverage,
                    explorationCoverage = record.ExplorationCoverage };
            }
            var issued = new List<string>(issuedTreasureSeeds); issued.Sort(StringComparer.Ordinal);
            return new CoreTreasureProgress {
                pirateExperience = PirateExperience, fragments = TreasureFragments, moonlitPityCount = MoonlitPityCount,
                identifiedWeatheredFragments = GetIdentifiedTreasureFragments("weathered"),
                identifiedGildedFragments = GetIdentifiedTreasureFragments("gilded"),
                identifiedMoonlitFragments = GetIdentifiedTreasureFragments("moonlit"),
                identificationSeed = treasureIdentificationSeed, identificationSequence = treasureIdentificationSequence,
                selectedMapId = selectedTreasureMapId,
                maps = held, usedMaps = used, completedSeeds = completedTreasureSeeds.ToArray(), issuedSeeds = issued.ToArray(),
                gems = Cargo.Gem, ancientMaterials = Cargo.AncientMaterial, relics = Cargo.Relic, blackPearls = Cargo.BlackPearl,
                ancientCompasses = Cargo.AncientCompass, lanterns = Cargo.Lantern, curses = Cargo.Curse,
            };
        }

        public void RestoreTreasureProgress(CoreTreasureProgress progress)
        {
            if (Phase != CoreDemoPhase.Port) return;
            progress = progress ?? new CoreTreasureProgress();
            // An unknown geometry must not be skipped and then overwritten by
            // a save of the remaining inventory. The profile loader preserves
            // the original JSON when it receives this compatibility failure.
            foreach (CoreTreasureMapSave saved in progress.maps ?? Array.Empty<CoreTreasureMapSave>())
                if (saved != null && (!ValidMapGeometryVersion(saved.mapGeometryVersion) || !ValidTreasureContentVersion(saved.contentVersion)))
                    throw new ArgumentException("藏宝图海域版本不兼容，原始档案已保留。", nameof(progress));
            foreach (CoreUsedTreasureMapSave saved in progress.usedMaps ?? Array.Empty<CoreUsedTreasureMapSave>())
                if (saved != null && (!ValidMapGeometryVersion(saved.mapGeometryVersion) || !ValidTreasureContentVersion(saved.contentVersion)))
                    throw new ArgumentException("藏宝图海域版本不兼容，原始档案已保留。", nameof(progress));
            PirateExperience = Math.Max(0, progress.pirateExperience);
            TreasureFragments = Math.Max(0, progress.fragments);
            identifiedTreasureFragments.Clear();
            identifiedTreasureFragments["weathered"] = Math.Max(0, progress.identifiedWeatheredFragments);
            identifiedTreasureFragments["gilded"] = Math.Max(0, progress.identifiedGildedFragments);
            identifiedTreasureFragments["moonlit"] = Math.Max(0, progress.identifiedMoonlitFragments);
            // A fixed migration seed makes reloading an old save before its first
            // identification stable, even before the upgraded save is written.
            treasureIdentificationSeed = string.IsNullOrWhiteSpace(progress.identificationSeed)
                ? "LEGACY-TREASURE-FRAGMENTS-V1" : progress.identificationSeed.Trim();
            treasureIdentificationSequence = Math.Max(0L, progress.identificationSequence);
            MoonlitPityCount = Math.Max(0, Math.Min(CoreTreasureHuntCatalog.MoonlitPityCap, progress.moonlitPityCount));
            treasureMaps.Clear(); usedTreasureMaps.Clear(); completedTreasureSeeds.Clear(); issuedTreasureSeeds.Clear();
            foreach (string seed in progress.issuedSeeds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(seed)) issuedTreasureSeeds.Add(seed.Trim());
            foreach (string seed in progress.completedSeeds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(seed) && !completedTreasureSeeds.Contains(seed.Trim()))
                { completedTreasureSeeds.Add(seed.Trim()); issuedTreasureSeeds.Add(seed.Trim()); }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreUsedTreasureMapSave save in progress.usedMaps ?? Array.Empty<CoreUsedTreasureMapSave>())
            {
                if (save == null || string.IsNullOrWhiteSpace(save.id) || string.IsNullOrWhiteSpace(save.seed) ||
                    !string.IsNullOrEmpty(save.rankId) && CoreTreasureHuntCatalog.FindRank(save.rankId) == null ||
                    !ValidMapGeometryVersion(save.mapGeometryVersion) || !ValidTreasureContentVersion(save.contentVersion) ||
                    !ids.Add(save.id) || !seeds.Add(save.seed.Trim())) continue;
                bool hasCoverage = save.hasExplorationCoverage && !float.IsNaN(save.explorationCoverage) && !float.IsInfinity(save.explorationCoverage);
                usedTreasureMaps.Add(new CoreUsedTreasureMapRecord(save.id, save.seed.Trim(), save.rankId,
                    save.departurePort, hasCoverage, hasCoverage ? ClampExplorationCoverage(save.explorationCoverage) : 0f,
                    save.mapGeometryVersion, save.contentVersion));
                issuedTreasureSeeds.Add(save.seed.Trim());
            }
            foreach (CoreTreasureMapSave save in progress.maps ?? Array.Empty<CoreTreasureMapSave>())
            {
                if (save == null || string.IsNullOrWhiteSpace(save.seed) || string.IsNullOrWhiteSpace(save.id) ||
                    CoreTreasureHuntCatalog.FindRank(save.rankId) == null || !ValidMapGeometryVersion(save.mapGeometryVersion) ||
                    !ValidTreasureContentVersion(save.contentVersion)) continue;
                if (completedTreasureSeeds.Contains(save.seed.Trim()))
                {
                    // A legacy record can sometimes retain its actual rank beside a completed seed.
                    AddUsedTreasureMap(new CoreTreasureMapRecord(save.id, save.seed.Trim(), save.rankId, save.mapGeometryVersion, save.contentVersion), string.Empty);
                    continue;
                }
                if (treasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps || !ids.Add(save.id) || !seeds.Add(save.seed.Trim())) continue;
                treasureMaps.Add(new CoreTreasureMapRecord(save.id, save.seed.Trim(), save.rankId, save.mapGeometryVersion, save.contentVersion));
                issuedTreasureSeeds.Add(save.seed.Trim());
            }
            // Issued seeds absent from the held inventory have been consumed, but old saves
            // cannot tell us their category or exploration. Preserve that uncertainty.
            var legacySeeds = new List<string>(issuedTreasureSeeds); legacySeeds.Sort(StringComparer.Ordinal);
            foreach (string seed in legacySeeds)
                if (!treasureMaps.Exists(map => map.Seed == seed) && !usedTreasureMaps.Exists(map => map.Seed == seed))
                    usedTreasureMaps.Add(new CoreUsedTreasureMapRecord("used:" + seed, seed, string.Empty, string.Empty, false, 0f));
            selectedTreasureMapId = treasureMaps.Exists(map => map.Id == progress.selectedMapId) ? progress.selectedMapId : string.Empty;
            Cargo.Gem = Math.Max(0, progress.gems); Cargo.AncientMaterial = Math.Max(0, progress.ancientMaterials);
            Cargo.Relic = Math.Max(0, progress.relics); Cargo.BlackPearl = Math.Max(0, progress.blackPearls);
            Cargo.AncientCompass = Math.Max(0, progress.ancientCompasses); Cargo.Lantern = Math.Max(0, progress.lanterns);
            Cargo.Curse = Math.Max(0, progress.curses);
        }

        public int GetIdentifiedTreasureFragments(string rankId) =>
            rankId != null && identifiedTreasureFragments.TryGetValue(rankId, out int count) ? count : 0;

        private string NextIdentifiedTreasureRank()
        {
            string rollSeed = treasureIdentificationSeed + "::" + treasureIdentificationSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            double roll = new Mulberry32Random(SeedHash.Compute("TREASURE-FRAGMENT::" + rollSeed)).NextDouble();
            bool moonlitEligible = PirateLevel >= CoreTreasureHuntCatalog.MoonlitUnlockLevel;
            return moonlitEligible && roll >= 1d - MoonlitIdentificationChance ? "moonlit" :
                PirateLevel >= 3 && roll >= (moonlitEligible ? .40 : .55) ? "gilded" : "weathered";
        }

        public bool CanIdentifyTreasureFragment(out string reason)
        {
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能鉴定藏宝图碎片。" :
                PirateLevel < 2 ? "海盗等级达到 2 后解锁碎片鉴定。" :
                TreasureFragments <= 0 ? "没有未鉴定的藏宝图碎片。" :
                treasureIdentificationSequence == long.MaxValue ? "碎片鉴定记录已满。" :
                GetIdentifiedTreasureFragments(NextIdentifiedTreasureRank()) == int.MaxValue ? "该图碎片已满，请先拼合藏宝图。" : string.Empty;
            return reason.Length == 0;
        }

        public bool TryIdentifyTreasureFragment(out string reason)
        {
            if (!CanIdentifyTreasureFragment(out reason)) return false;
            string rankId = NextIdentifiedTreasureRank();
            TreasureFragments--;
            identifiedTreasureFragments[rankId] = GetIdentifiedTreasureFragments(rankId) + 1;
            treasureIdentificationSequence++;
            if (PirateLevel >= CoreTreasureHuntCatalog.MoonlitUnlockLevel)
                MoonlitPityCount = rankId == "moonlit" ? 0 : Math.Min(CoreTreasureHuntCatalog.MoonlitPityCap, MoonlitPityCount + 1);
            reason = "已鉴定为「" + CoreTreasureHuntCatalog.FindRank(rankId).Name + "藏宝图」碎片，已归入对应拼图。";
            AddLog(reason);
            return true;
        }

        private string FirstReadyTreasureRank()
        {
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                if (PirateLevel >= rank.UnlockLevel && GetIdentifiedTreasureFragments(rank.Id) >= CoreTreasureHuntCatalog.FragmentCost)
                    return rank.Id;
            return null;
        }

        public bool CanCraftTreasureMap(out string reason)
        {
            string rankId = FirstReadyTreasureRank();
            if (rankId != null) return CanCraftTreasureMap(rankId, out reason);
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能拼合藏宝图。" :
                PirateLevel < 2 ? "海盗等级达到 2 后解锁拼合。" :
                treasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps ? "最多持有 6 张未使用藏宝图。" :
                "请先鉴定碎片，集齐同一张藏宝图的 4 个碎片后再拼合。";
            return false;
        }

        public bool CanCraftTreasureMap(string rankId, out string reason)
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.FindRank(rankId);
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能拼合藏宝图。" :
                rank == null ? "未知的藏宝图。" :
                PirateLevel < rank.UnlockLevel ? "海盗等级达到 " + rank.UnlockLevel + " 后解锁此图。" :
                treasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps ? "最多持有 6 张未使用藏宝图。" :
                GetIdentifiedTreasureFragments(rankId) < CoreTreasureHuntCatalog.FragmentCost ?
                    "还缺 " + (CoreTreasureHuntCatalog.FragmentCost - GetIdentifiedTreasureFragments(rankId)) + " 个已鉴定的「" + rank.Name + "藏宝图」碎片。" : string.Empty;
            return reason.Length == 0;
        }

        public bool TryCraftTreasureMap(string seed, out string reason)
        {
            if (!CanCraftTreasureMap(out reason)) return false;
            return TryCraftTreasureMapForRank(FirstReadyTreasureRank(), seed, out reason);
        }

        public bool TryCraftTreasureMapForRank(string rankId, string seed, out string reason)
        {
            if (!CanCraftTreasureMap(rankId, out reason)) return false;
            seed = string.IsNullOrWhiteSpace(seed) ? "CACHE-" + Guid.NewGuid().ToString("N") : seed.Trim();
            if (seed.Length > 64) { reason = "藏宝图种子最多 64 个字符。"; return false; }
            if (issuedTreasureSeeds.Contains(seed)) { reason = "该种子的藏宝图已经拼合或使用过。"; return false; }
            var record = new CoreTreasureMapRecord(rankId + ":" + seed, seed, rankId, contentVersion: 1);
            identifiedTreasureFragments[rankId] -= CoreTreasureHuntCatalog.FragmentCost;
            treasureMaps.Add(record); issuedTreasureSeeds.Add(seed);
            AddLog("拼合「" + record.Name + "」，消耗同图碎片 4。种子已收入藏图册，可在任意港口装备出航。");
            return true;
        }

        public bool TrySelectTreasureSeed(string seed, out string reason)
        {
            string normalizedSeed = string.IsNullOrWhiteSpace(seed) ? string.Empty : seed.Trim();
            CoreTreasureMapRecord record = treasureMaps.Find(map => map.Seed == normalizedSeed);
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能装备藏宝图。" :
                record == null ? "该种子不属于持有的未使用藏宝图。" : string.Empty;
            if (reason.Length > 0) return false;
            return TrySelectTreasureMap(record.Id, out reason);
        }

        public bool TrySelectTreasureMap(string id, out string reason)
        {
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能装备藏宝图。" :
                !treasureMaps.Exists(map => map.Id == id) ? "这张藏宝图不在未使用库存中。" : string.Empty;
            if (reason.Length > 0) return false;
            selectedTreasureMapId = id;
            return true;
        }

        public void ClearSelectedTreasureMap() { if (Phase == CoreDemoPhase.Port) selectedTreasureMapId = string.Empty; }

        public bool CanStartTreasureVoyage(out string reason)
        {
            if (!CanStartWithDepartureCrew(out reason)) return false;
            CoreTreasureMapRecord record = SelectedTreasureMap;
            int dayRations = Math.Max(2, (int)Math.Ceiling(EffectiveRationCrewCount()));
            reason = Phase != CoreDemoPhase.Port ? "只能在港口开启藏宝航行。" :
                record == null ? "请先装备一张未使用藏宝图。" :
                GuidedTutorialLeg > 0 ? "请先完成当前新手教学。" :
                PirateLevel < record.Rank.UnlockLevel ? "海盗等级不足，需要等级 " + record.Rank.UnlockLevel + "。" :
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo) ? "物资数量无效，请先整备。" :
                Cargo.UsedVolume > CargoCapacityValue + .001f ? "货舱超载，无法出航；藏宝图尚未消耗。" :
                Cargo.Water < dayRations || Cargo.Food < dayRations ? "至少准备一天口粮：淡水和食物各 " + dayRations + "；藏宝图尚未消耗。" :
                ProfileGold < 0 || (long)Cargo.Gold + Math.Min(ProfileGold, Math.Max(0, 60 - Cargo.Gold)) > int.MaxValue ? "金币数量无效；藏宝图尚未消耗。" : string.Empty;
            return reason.Length == 0;
        }

        public bool TryStartTreasureVoyage(out string reason)
        {
            if (!CanStartTreasureVoyage(out reason)) return false;
            StartVoyage();
            return true;
        }

        // Upgrade only a held, unused chart at the new-voyage boundary. Never
        // reinterpret the events, node IDs or geography of an active/used chart.
        internal CoreTreasureMapRecord PromoteTreasureMapForNewVoyage(CoreTreasureMapRecord record)
        {
            if (record == null || record.ContentVersion != 0) return record;
            int index = treasureMaps.IndexOf(record);
            if (index < 0) return record;
            var promoted = new CoreTreasureMapRecord(record.Id, record.Seed, record.RankId, record.MapGeometryVersion, 1);
            treasureMaps[index] = promoted;
            return promoted;
        }

        private void BeginTreasureVoyageState(CoreTreasureMapRecord treasure)
        {
            ActiveTreasureMap = treasure;
            TreasureAdventureCompleted = false; TreasureClues = 0; voyageTreasureFragments = 0;
            if (treasure == null) return;
            treasureMaps.Remove(treasure); selectedTreasureMapId = string.Empty;
            AddUsedTreasureMap(treasure, Map.DepartureName);
            UpdateActiveUsedTreasureMapCoverage();
            AddLog(treasure.ContentVersion == 0
                ? "已消耗「" + treasure.Name + "」。集齐 " + treasure.Rank.RequiredClues + " 条线索后开启叉号秘藏；撤离或失败不返还。"
                : "已消耗「" + treasure.Name + "」。" + CoreTreasureExpeditionCatalog.Overview(treasure.RankId) + " 撤离或失败不返还海图。");
        }

        private void AddUsedTreasureMap(CoreTreasureMapRecord treasure, string departurePort)
        {
            int existingIndex = usedTreasureMaps.FindIndex(map => map.Seed == treasure.Seed);
            if (existingIndex >= 0 && usedTreasureMaps[existingIndex].Rank != null) return;
            var record = new CoreUsedTreasureMapRecord(treasure.Id, treasure.Seed, treasure.RankId, departurePort, false, 0f,
                treasure.MapGeometryVersion, treasure.ContentVersion);
            if (existingIndex < 0) usedTreasureMaps.Add(record);
            else usedTreasureMaps[existingIndex] = record;
            issuedTreasureSeeds.Add(treasure.Seed);
        }

        private void UpdateActiveUsedTreasureMapCoverage()
        {
            if (!IsTreasureVoyage || ActiveTreasureMap == null) return;
            AddUsedTreasureMap(ActiveTreasureMap, Map.DepartureName);
            CoreUsedTreasureMapRecord record = usedTreasureMaps.Find(map => map.Seed == ActiveTreasureMap.Seed);
            record.RecordExploration(ClampExplorationCoverage(ExplorationCoverage()));
        }

        /// <summary>Regenerates the entire consumed chart without changing the live voyage or its fog.</summary>
        public CoreVoyageMap PreviewUsedTreasureMap(CoreUsedTreasureMapRecord record)
        {
            if (record == null || record.Rank == null) return null;
            string port = string.IsNullOrWhiteSpace(record.DeparturePort) ? CurrentPort : record.DeparturePort;
            return GenerateTreasureMap(new CoreTreasureMapRecord(record.Id, record.Seed, record.RankId,
                record.MapGeometryVersion, record.ContentVersion), port);
        }

        private static CoreRouteDefinition TreasureRoute(CoreTreasureMapRank rank, int contentVersion = 0) =>
            new CoreRouteDefinition("route_treasure_hunt", rank.Name + "藏宝海域", contentVersion == 0
                ? "收集方位线索，寻找秘藏核心" : CoreTreasureExpeditionCatalog.ThemeName(rank.Id) + "：" + CoreTreasureExpeditionCatalog.Overview(rank.Id), rank.UnlockLevel,
                rank.RecommendedPower, rank.RecommendedVoyageDays, rank.NauticalMilesPerMapUnit, rank.MinimumEventSpacing,
                rank.AmbushChance, rank.AmbushDetection, "秘藏核心", rank.RecommendedPower);

        private CoreVoyageMap GenerateTreasureMap(CoreTreasureMapRecord record, string port)
        {
            CoreTreasureMapRank rank = record.Rank;
            uint hash = SeedHash.Compute("TREASURE::" + record.RankId + "::" + record.Seed);
            var random = new Mulberry32Random(hash ^ 0x7F4A7C15u);
            var map = new CoreVoyageMap(record.MapGeometryVersion) {
                Seed = record.Seed, Hash = hash, IsTreasureMap = true, Route = TreasureRoute(rank, record.ContentVersion),
                Departure = new VoyagePoint(8f, 53f), Destination = new VoyagePoint(88f, 20f),
                DepartureName = port, DestinationName = "秘藏核心",
                CurrentPhaseA = random.NextDouble() * Math.PI * 2, CurrentPhaseB = random.NextDouble() * Math.PI * 2,
            };
            double wind = random.NextDouble() * Math.PI * 2;
            map.GlobalWindVector = new VoyagePoint((float)Math.Cos(wind), (float)Math.Sin(wind));
            map.GlobalWindDirection = ClosestDirection(wind); map.GlobalWindStrength = 5f + (float)random.NextDouble() * 8f;
            void Mix(List<CoreEncounter> values) {
                for (int i = values.Count - 1; i > 0; i--) { int j = (int)(random.NextDouble() * (i + 1)); var temp = values[i]; values[i] = values[j]; values[j] = temp; }
            }
            var pool = new List<CoreEncounter>(record.ContentVersion == 0
                ? CoreTreasureHuntCatalog.CreateEncounters(rank) : CoreTreasureExpeditionCatalog.CreateEncounters(rank));
            CoreEncounter core = pool.Find(encounter => encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            var objectives = new HashSet<string>(StringComparer.Ordinal);
            List<CoreEncounter> selected;
            if (record.ContentVersion == 0)
            {
                // Keep the precise legacy shuffle order and random consumption.
                var clues = pool.FindAll(encounter => encounter.Category == "线索");
                Mix(clues);
                selected = clues.GetRange(0, rank.RequiredClues + 1);
                pool.Remove(core);
                foreach (CoreEncounter clue in selected) pool.Remove(clue);
                Mix(pool);
                selected.AddRange(pool.GetRange(0, rank.SidePointCount - selected.Count));
            }
            else
            {
                selected = new List<CoreEncounter>();
                pool.Remove(core);
                foreach (string id in CoreTreasureExpeditionCatalog.GuaranteedEventIds(record.RankId))
                {
                    CoreEncounter encounter = pool.Find(item => item.Id == id);
                    if (encounter == null || !objectives.Add(id))
                        throw new InvalidOperationException("藏宝图必选事件缺失或重复：" + id);
                    selected.Add(encounter); pool.Remove(encounter);
                }
                if (selected.Count > rank.SidePointCount || pool.Count < rank.SidePointCount - selected.Count)
                    throw new InvalidOperationException("藏宝图专属事件数量不足。");
                Mix(pool);
                selected.AddRange(pool.GetRange(0, rank.SidePointCount - selected.Count));
            }
            Mix(selected); selected.Add(core);
            // Each treasure rank carries a small chapter of the legendary fleet.
            // Place those encounters before the core so the vault remains the
            // final destination while all six warships can be met on the three
            // treasure maps.
            selected.InsertRange(selected.Count - 1, CoreLegendaryWarships.CreateTreasureEncounters(record.RankId, shipRosterVersion > 0));
            foreach (CoreEncounter encounter in selected) map.Nodes.Add(new CoreMapNode {
                Id = map.Nodes.Count, Encounter = encounter, TerrainConstraint = TerrainConstraintFor(encounter, false),
                Discovered = encounter == core || objectives.Contains(encounter.Id),
                IntelRevealed = encounter == core || objectives.Contains(encounter.Id),
            });
            VoyageMapGenerator.Generate(map, rank.MinimumEventSpacing, 8f, 0f, 7f, 18f);
            map.Destination = map.Nodes[map.Nodes.Count - 1].Position;
            GenerateTerrainCurrents(map, new Mulberry32Random(hash ^ 0x6A09E667u));
            return map;
        }

        public string ChoiceUnavailableReason(int index)
        {
            if (Phase != CoreDemoPhase.Encounter || ActiveEncounter == null || index < 0 || index >= ActiveEncounter.Choices.Count) return "当前没有可选事件。";
            if (IsChoiceAvailable(index)) return string.Empty;
            CoreEncounterChoice choice = ActiveEncounter.Choices[index];
            string conditionReason = EncounterChoiceConditionReason(choice);
            if (conditionReason.Length > 0) return conditionReason;
            if (TreasureClues < choice.RequiredTreasureClues) return "线索不足，还缺 " + (choice.RequiredTreasureClues - TreasureClues) + " 条（" + TreasureClues + "/" + choice.RequiredTreasureClues + "）。";
            if (choice.Effect.TreasureComplete && Hull + choice.Effect.Hull <= 0) return "船体不足以承受开启损伤。";
            int rations = RationCostFor(choice.Hours);
            if (Cargo.Water < rations || Cargo.Food < rations) return "耗时需要淡水、食物各 " + rations + "；当前口粮不足。";
            VoyageEffect applied = choice.StartsBattle ? SplitBattleEffect(choice.Effect, false) : EffectiveChoiceEffect(index);
            string rareShortage = RareResourceShortage(applied);
            if (rareShortage.Length > 0) return rareShortage;
            if (!CanAfford(applied, rations)) return "物资不足，无法支付选项成本（包括耗时口粮）。";
            if (Cargo.UsedVolume - rations * .16f + CargoVolumeDelta(applied) > CargoCapacityValue + .001f) return "货舱空间不足，不能装下选项奖励。";
            return "当前条件不允许执行。";
        }

        private string RareResourceShortage(VoyageEffect effect)
        {
            // Event costs are paid by the vessel. Stock left in any port cannot
            // satisfy a choice encountered at sea.
            if ((long)Cargo.Gem + effect.Gem < 0) return "随船宝石不足";
            if ((long)Cargo.AncientMaterial + effect.AncientMaterial < 0) return "随船古代材料不足";
            if ((long)Cargo.Relic + effect.Relic < 0) return "随船稀有遗物不足";
            if ((long)Cargo.BlackPearl + effect.BlackPearl < 0) return "随船黑珍珠不足";
            if ((long)Cargo.AncientCompass + effect.AncientCompass < 0) return "随船古代罗盘不足";
            if ((long)Cargo.Lantern + effect.Lantern < 0) return "随船航海灯不足";
            return string.Empty;
        }

        private static int SumResource(int current, int delta) => (int)Math.Max(0L, Math.Min(int.MaxValue, (long)current + delta));
        private void ApplyTreasureEffect(VoyageEffect effect)
        {
            voyageTreasureFragments = SumResource(voyageTreasureFragments, effect.MapFragments);
            TreasureClues = SumResource(TreasureClues, effect.TreasureClues);
            Cargo.Gem = SumResource(Cargo.Gem, effect.Gem); Cargo.AncientMaterial = SumResource(Cargo.AncientMaterial, effect.AncientMaterial);
            Cargo.Relic = SumResource(Cargo.Relic, effect.Relic); Cargo.BlackPearl = SumResource(Cargo.BlackPearl, effect.BlackPearl);
            Cargo.AncientCompass = SumResource(Cargo.AncientCompass, effect.AncientCompass); Cargo.Lantern = SumResource(Cargo.Lantern, effect.Lantern);
            Cargo.Curse = SumResource(Cargo.Curse, effect.Curse);
        }
        private bool CanAffordTreasureEffect(VoyageEffect effect)
        {
            bool Fits(int current, int delta) => (long)current + delta >= 0 && (long)current + delta <= int.MaxValue;
            return Fits(voyageTreasureFragments, effect.MapFragments) && Fits(TreasureClues, effect.TreasureClues) &&
                Fits(Cargo.Gem, effect.Gem) && Fits(Cargo.AncientMaterial, effect.AncientMaterial) && Fits(Cargo.Relic, effect.Relic) &&
                Fits(Cargo.BlackPearl, effect.BlackPearl) && Fits(Cargo.AncientCompass, effect.AncientCompass) &&
                Fits(Cargo.Lantern, effect.Lantern) && Fits(Cargo.Curse, effect.Curse);
        }
        private void ApplyTreasureBattleReward(VoyageEffect effect, bool captured)
        {
            int Receive(int current, int amount, string name, bool tangibleLoot = true)
            {
                // Map fragments and clues do not occupy cargo space. Their authored
                // quantities keep the existing rule; only cash/fame use the new x2.
                long gain = captured && tangibleLoot && amount > 0 ? (long)Math.Ceiling(amount * 1.5d) : amount;
                if (tangibleLoot) gain = ScaleSeaChartBattleReward(gain);
                long total = (long)current + gain;
                if (total > int.MaxValue) AddLog("战利品数量上限：" + name + " " + (total - int.MaxValue) + " 未能入账；已有余额保留。");
                return (int)Math.Max(0L, Math.Min(int.MaxValue, total));
            }
            voyageTreasureFragments = Receive(voyageTreasureFragments, effect.MapFragments, "藏宝图碎片");
            TreasureClues = Receive(TreasureClues, effect.TreasureClues, "藏宝线索", false);
            // Gem/material/relic/pearl/compass/lantern rewards have already been
            // converted to cash by CoreBattleRewardBalance, with ordinary supplies.
            Cargo.Curse = Receive(Cargo.Curse, effect.Curse, "诅咒", false);
        }
        private static float TreasureCargoVolumeDelta(VoyageEffect effect) =>
            effect.Gem * 5f + effect.AncientMaterial * 1.5f + effect.Relic * 5f + effect.BlackPearl * 4f + effect.AncientCompass * 3f + effect.Lantern;
        private void RetainTreasureCargo(VoyageCargo starting, float protection)
        {
            // Rare objects are indivisible: round losses down, so a 5% cargo loss
            // cannot erase a single gem/relic/pearl/compass/lantern. Cash and common materials
            // retain their existing floor(gain * protection) settlement rule.
            int RareGain(int current, int initial) => (int)Math.Ceiling(Math.Max(0, current - initial) * (double)protection);
            int RareRetained(int current, int initial) => Math.Min(current, initial) + RareGain(current, initial);
            Settlement.BankedGem = RareGain(Cargo.Gem, starting.Gem);
            Settlement.BankedAncientMaterial = ProtectedGain(Cargo.AncientMaterial, starting.AncientMaterial, protection);
            Settlement.BankedRelic = RareGain(Cargo.Relic, starting.Relic);
            Settlement.BankedBlackPearl = RareGain(Cargo.BlackPearl, starting.BlackPearl);
            Cargo.Gem = RareRetained(Cargo.Gem, starting.Gem);
            Cargo.AncientMaterial = RetainedResource(Cargo.AncientMaterial, starting.AncientMaterial, protection);
            Cargo.Relic = RareRetained(Cargo.Relic, starting.Relic);
            Cargo.BlackPearl = RareRetained(Cargo.BlackPearl, starting.BlackPearl);
            Cargo.AncientCompass = RareRetained(Cargo.AncientCompass, starting.AncientCompass);
            Cargo.Lantern = RareRetained(Cargo.Lantern, starting.Lantern);
        }
        private int VoyagePirateExperience(VoyageOutcome outcome, int completed)
        {
            bool progressed = outcome == VoyageOutcome.ArrivedAtPort || completed > 0 || TotalDistanceNm >= 20f;
            if (!progressed) return 0;
            if (GuidedTutorialLeg > 0) return GuidedTutorialLeg == CoreGuidedTutorialContent.BossVoyageLeg ||
                UsesCombinedGuidedTutorialVoyage && GuidedTutorialBossDefeated ? 80 : 20;
            // Ordinary empty returns must not turn the minimum sailing step
            // into repeatable pirate experience. Treasure voyages keep their rules.
            if (!IsTreasureVoyage && outcome == VoyageOutcome.SafeReturn && completed == 0) return 0;
            // The 20-nautical-mile eligibility threshold stays physical. Experience
            // measures chart exploration instead: the old per-mile coefficient
            // could promote a captain straight to level 6 after one ordinary trip.
            double chartDistance = TotalDistanceNm / (double)Map.Route.NauticalMilesPerMapUnit;
            return (int)Math.Min(int.MaxValue, Math.Max(20d, Math.Round(15d + completed * 10d + chartDistance * 1.5d +
                (outcome == VoyageOutcome.ArrivedAtPort || outcome == VoyageOutcome.TreasureRecovered ? 60d : 0d))));
        }
        private void CommitTreasureVoyageSettlement()
        {
            UpdateActiveUsedTreasureMapCoverage();
            TreasureFragments = SumResource(TreasureFragments, Settlement.BankedFragments);
            PirateExperience = SumResource(PirateExperience, Settlement.BankedPirateExperience);
            if (Settlement.TreasureCompleted && ActiveTreasureMap != null && !completedTreasureSeeds.Contains(ActiveTreasureMap.Seed))
                completedTreasureSeeds.Add(ActiveTreasureMap.Seed);
        }
    }
}
