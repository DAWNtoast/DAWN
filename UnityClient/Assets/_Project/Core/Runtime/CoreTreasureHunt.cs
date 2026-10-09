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
    }

    [Serializable]
    public sealed class CoreTreasureProgress
    {
        public int pirateExperience;
        public int fragments;
        // Consecutive non-moonlit crafts after the moonlit rank is unlocked.
        // Missing in older saves, where JsonUtility restores the default 0.
        public int moonlitPityCount;
        public string selectedMapId;
        public CoreTreasureMapSave[] maps = Array.Empty<CoreTreasureMapSave>();
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
        private static readonly int[] pirateThresholds = { 0, 100, 260, 520, 900, 1400 };
        private readonly List<CoreTreasureMapRecord> treasureMaps = new List<CoreTreasureMapRecord>();
        private readonly List<string> completedTreasureSeeds = new List<string>();
        private readonly HashSet<string> issuedTreasureSeeds = new HashSet<string>(StringComparer.Ordinal);
        private string selectedTreasureMapId = string.Empty;
        private int voyageTreasureFragments;
        public int PirateExperience { get; private set; }
        public int PirateLevel
        {
            get { int level = 1; for (int i = 1; i < pirateThresholds.Length; i++) if (PirateExperience >= pirateThresholds[i]) level = i + 1; return level; }
        }
        public int TreasureFragments { get; private set; }
        public int MoonlitPityCount { get; private set; }
        public double MoonlitCraftChance => PirateLevel < CoreTreasureHuntCatalog.MoonlitUnlockLevel ? 0d :
            Math.Min(CoreTreasureHuntCatalog.MoonlitMaxChance,
                CoreTreasureHuntCatalog.MoonlitBaseChance + MoonlitPityCount * CoreTreasureHuntCatalog.MoonlitPityStep);
        public int VoyageTreasureFragments => voyageTreasureFragments;
        public IReadOnlyList<CoreTreasureMapRecord> TreasureMaps => treasureMaps.AsReadOnly();
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
            var held = new CoreTreasureMapSave[treasureMaps.Count];
            for (int i = 0; i < held.Length; i++) held[i] = new CoreTreasureMapSave {
                id = treasureMaps[i].Id, seed = treasureMaps[i].Seed, rankId = treasureMaps[i].RankId };
            var issued = new List<string>(issuedTreasureSeeds); issued.Sort(StringComparer.Ordinal);
            return new CoreTreasureProgress {
                pirateExperience = PirateExperience, fragments = TreasureFragments, moonlitPityCount = MoonlitPityCount,
                selectedMapId = selectedTreasureMapId,
                maps = held, completedSeeds = completedTreasureSeeds.ToArray(), issuedSeeds = issued.ToArray(),
                gems = Cargo.Gem, ancientMaterials = Cargo.AncientMaterial, relics = Cargo.Relic, blackPearls = Cargo.BlackPearl,
                ancientCompasses = Cargo.AncientCompass, lanterns = Cargo.Lantern, curses = Cargo.Curse,
            };
        }

        public void RestoreTreasureProgress(CoreTreasureProgress progress)
        {
            if (Phase != CoreDemoPhase.Port) return;
            progress = progress ?? new CoreTreasureProgress();
            PirateExperience = Math.Max(0, progress.pirateExperience);
            TreasureFragments = Math.Max(0, progress.fragments);
            MoonlitPityCount = Math.Max(0, Math.Min(CoreTreasureHuntCatalog.MoonlitPityCap, progress.moonlitPityCount));
            treasureMaps.Clear(); completedTreasureSeeds.Clear(); issuedTreasureSeeds.Clear();
            foreach (string seed in progress.issuedSeeds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(seed)) issuedTreasureSeeds.Add(seed.Trim());
            foreach (string seed in progress.completedSeeds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(seed) && !completedTreasureSeeds.Contains(seed.Trim()))
                { completedTreasureSeeds.Add(seed.Trim()); issuedTreasureSeeds.Add(seed.Trim()); }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreTreasureMapSave save in progress.maps ?? Array.Empty<CoreTreasureMapSave>())
            {
                if (save == null || string.IsNullOrWhiteSpace(save.seed) || string.IsNullOrWhiteSpace(save.id) ||
                    CoreTreasureHuntCatalog.FindRank(save.rankId) == null || treasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps ||
                    completedTreasureSeeds.Contains(save.seed.Trim()) || !ids.Add(save.id) || !seeds.Add(save.seed.Trim())) continue;
                treasureMaps.Add(new CoreTreasureMapRecord(save.id, save.seed.Trim(), save.rankId));
                issuedTreasureSeeds.Add(save.seed.Trim());
            }
            selectedTreasureMapId = treasureMaps.Exists(map => map.Id == progress.selectedMapId) ? progress.selectedMapId : string.Empty;
            Cargo.Gem = Math.Max(0, progress.gems); Cargo.AncientMaterial = Math.Max(0, progress.ancientMaterials);
            Cargo.Relic = Math.Max(0, progress.relics); Cargo.BlackPearl = Math.Max(0, progress.blackPearls);
            Cargo.AncientCompass = Math.Max(0, progress.ancientCompasses); Cargo.Lantern = Math.Max(0, progress.lanterns);
            Cargo.Curse = Math.Max(0, progress.curses);
        }

        public bool CanCraftTreasureMap(out string reason)
        {
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能拼合藏宝图。" :
                PirateLevel < 2 ? "海盗等级达到 2 后解锁拼合。" :
                treasureMaps.Count >= CoreTreasureHuntCatalog.MaxHeldMaps ? "最多持有 6 张未使用藏宝图。" :
                TreasureFragments < CoreTreasureHuntCatalog.FragmentCost ? "还缺 " + (CoreTreasureHuntCatalog.FragmentCost - TreasureFragments) + " 个藏宝图碎片。" : string.Empty;
            return reason.Length == 0;
        }

        public bool TryCraftTreasureMap(string seed, out string reason)
        {
            if (!CanCraftTreasureMap(out reason)) return false;
            seed = string.IsNullOrWhiteSpace(seed) ? "CACHE-" + Guid.NewGuid().ToString("N") : seed.Trim();
            if (seed.Length > 64) { reason = "藏宝图种子最多 64 个字符。"; return false; }
            if (issuedTreasureSeeds.Contains(seed)) { reason = "该种子的藏宝图已经拼合或使用过。"; return false; }
            double roll = new Mulberry32Random(SeedHash.Compute("TREASURE-RANK::" + seed)).NextDouble();
            bool moonlitEligible = PirateLevel >= CoreTreasureHuntCatalog.MoonlitUnlockLevel;
            string rankId = moonlitEligible && roll >= 1d - MoonlitCraftChance ? "moonlit" :
                PirateLevel >= 3 && roll >= (moonlitEligible ? .40 : .55) ? "gilded" : "weathered";
            if (moonlitEligible)
                MoonlitPityCount = rankId == "moonlit" ? 0 : Math.Min(CoreTreasureHuntCatalog.MoonlitPityCap, MoonlitPityCount + 1);
            var record = new CoreTreasureMapRecord(rankId + ":" + seed, seed, rankId);
            TreasureFragments -= CoreTreasureHuntCatalog.FragmentCost;
            treasureMaps.Add(record); issuedTreasureSeeds.Add(seed); selectedTreasureMapId = record.Id;
            AddLog("拼合「" + record.Name + "」，消耗碎片 4。藏宝图将在成功出航时消耗。");
            return true;
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

        private void BeginTreasureVoyageState(CoreTreasureMapRecord treasure)
        {
            ActiveTreasureMap = treasure;
            TreasureAdventureCompleted = false; TreasureClues = 0; voyageTreasureFragments = 0;
            if (treasure == null) return;
            treasureMaps.Remove(treasure); selectedTreasureMapId = string.Empty;
            AddLog("已消耗「" + treasure.Name + "」。集齐 " + treasure.Rank.RequiredClues + " 条线索后开启叉号秘藏；撤离或失败不返还。");
        }

        private static CoreRouteDefinition TreasureRoute(CoreTreasureMapRank rank) =>
            new CoreRouteDefinition("route_treasure_hunt", rank.Name + "藏宝海域", "收集方位线索，寻找秘藏核心", rank.UnlockLevel,
                rank.RecommendedPower, rank.RecommendedVoyageDays, rank.NauticalMilesPerMapUnit, rank.MinimumEventSpacing,
                rank.AmbushChance, rank.AmbushDetection, "秘藏核心", rank.RecommendedPower);

        private CoreVoyageMap GenerateTreasureMap(CoreTreasureMapRecord record, string port)
        {
            CoreTreasureMapRank rank = record.Rank;
            uint hash = SeedHash.Compute("TREASURE::" + record.RankId + "::" + record.Seed);
            var random = new Mulberry32Random(hash ^ 0x7F4A7C15u);
            var map = new CoreVoyageMap {
                Seed = record.Seed, Hash = hash, IsTreasureMap = true, Route = TreasureRoute(rank),
                Departure = new VoyagePoint(8f, 53f), Destination = new VoyagePoint(88f, 20f),
                DepartureName = port, DestinationName = "秘藏核心",
                CurrentPhaseA = random.NextDouble() * Math.PI * 2, CurrentPhaseB = random.NextDouble() * Math.PI * 2,
            };
            double wind = random.NextDouble() * Math.PI * 2;
            map.GlobalWindVector = new VoyagePoint((float)Math.Cos(wind), (float)Math.Sin(wind));
            map.GlobalWindDirection = ClosestDirection(wind); map.GlobalWindStrength = 5f + (float)random.NextDouble() * 8f;
            var pool = new List<CoreEncounter>(CoreTreasureHuntCatalog.CreateEncounters(rank));
            var clues = pool.FindAll(encounter => encounter.Category == "线索");
            void Mix(List<CoreEncounter> values) {
                for (int i = values.Count - 1; i > 0; i--) { int j = (int)(random.NextDouble() * (i + 1)); var temp = values[i]; values[i] = values[j]; values[j] = temp; }
            }
            Mix(clues);
            var selected = clues.GetRange(0, rank.RequiredClues + 1);
            CoreEncounter core = pool.Find(encounter => encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            pool.Remove(core);
            foreach (CoreEncounter clue in selected) pool.Remove(clue);
            Mix(pool);
            selected.AddRange(pool.GetRange(0, rank.SidePointCount - selected.Count)); Mix(selected); selected.Add(core);
            // Each treasure rank carries a small chapter of the legendary fleet.
            // Place those encounters before the core so the vault remains the
            // final destination while all five warships can be met on the three
            // treasure maps.
            selected.InsertRange(selected.Count - 1, CoreLegendaryWarships.CreateTreasureEncounters(record.RankId));
            foreach (CoreEncounter encounter in selected) map.Nodes.Add(new CoreMapNode {
                Id = map.Nodes.Count, Encounter = encounter, TerrainConstraint = TerrainConstraintFor(encounter, false),
                Discovered = encounter == core, IntelRevealed = encounter == core,
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
                long gain = captured && tangibleLoot && amount > 0 ? (long)Math.Ceiling(amount * 1.5d) : amount;
                if (tangibleLoot) gain = ScaleSeaChartBattleReward(gain);
                long total = (long)current + gain;
                if (total > int.MaxValue) AddLog("战利品数量上限：" + name + " " + (total - int.MaxValue) + " 未能入账；已有余额保留。");
                return (int)Math.Max(0L, Math.Min(int.MaxValue, total));
            }
            voyageTreasureFragments = Receive(voyageTreasureFragments, effect.MapFragments, "藏宝图碎片");
            TreasureClues = Receive(TreasureClues, effect.TreasureClues, "藏宝线索", false);
            Cargo.Gem = Receive(Cargo.Gem, effect.Gem, "宝石");
            Cargo.AncientMaterial = Receive(Cargo.AncientMaterial, effect.AncientMaterial, "古代材料");
            Cargo.Relic = Receive(Cargo.Relic, effect.Relic, "稀有遗物");
            Cargo.BlackPearl = Receive(Cargo.BlackPearl, effect.BlackPearl, "黑珍珠");
            Cargo.AncientCompass = Receive(Cargo.AncientCompass, effect.AncientCompass, "古代罗盘");
            Cargo.Lantern = Receive(Cargo.Lantern, effect.Lantern, "航海灯");
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
            // The 20-nautical-mile eligibility threshold stays physical. Experience
            // measures chart exploration instead: the old per-mile coefficient
            // could promote a captain straight to level 6 after one ordinary trip.
            double chartDistance = TotalDistanceNm / (double)Map.Route.NauticalMilesPerMapUnit;
            return (int)Math.Min(int.MaxValue, Math.Max(20d, Math.Round(15d + completed * 10d + chartDistance * 1.5d +
                (outcome == VoyageOutcome.ArrivedAtPort || outcome == VoyageOutcome.TreasureRecovered ? 60d : 0d))));
        }
        private void CommitTreasureVoyageSettlement()
        {
            TreasureFragments = SumResource(TreasureFragments, Settlement.BankedFragments);
            PirateExperience = SumResource(PirateExperience, Settlement.BankedPirateExperience);
            if (Settlement.TreasureCompleted && ActiveTreasureMap != null && !completedTreasureSeeds.Contains(ActiveTreasureMap.Seed))
                completedTreasureSeeds.Add(ActiveTreasureMap.Seed);
        }
    }
}
