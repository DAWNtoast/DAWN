using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private const double ExplorationBoostLimitHours = 24d;
        private double sailingBoostHoursRemaining;
        private double surveyBoostHoursRemaining;
        private readonly HashSet<string> explorationDiscoveries = new HashSet<string>(StringComparer.Ordinal);

        public double SailingBoostHoursRemaining => sailingBoostHoursRemaining;
        public double SurveyBoostHoursRemaining => surveyBoostHoursRemaining;
        public bool HasDiscovery(string key) => key != null && explorationDiscoveries.Contains(key);
        public static string DiscoveryLabel(string key) => CoreVoyageEventCatalog.DiscoveryName(key);
        public string ExplorationStatusSummary
        {
            get
            {
                var parts = new List<string>();
                if (sailingBoostHoursRemaining > 0d) parts.Add("航速 +25% · 余 " + sailingBoostHoursRemaining.ToString("0.#") + " 小时");
                if (surveyBoostHoursRemaining > 0d) parts.Add("瞭望 +35% · 余 " + surveyBoostHoursRemaining.ToString("0.#") + " 小时");
                foreach (string key in explorationDiscoveries.OrderBy(value => value, StringComparer.Ordinal))
                {
                    string targetId = ExplorationDiscoveryTarget(key);
                    CoreMapNode[] targets = Map?.Nodes.Where(node => node.Encounter != null &&
                        node.Encounter.Id == targetId && node.Encounter.HasChartLocation).ToArray();
                    string progress = targets == null || targets.Length == 0 ? "（本海图无对应地点）" :
                        targets.All(node => node.Visited) ? "（已探索）" : string.Empty;
                    parts.Add(DiscoveryLabel(key) + " → " + CoreVoyageEventCatalog.DiscoveryTargetName(targetId) + progress);
                }
                return string.Join("；", parts);
            }
        }

        private static bool IsKnownExplorationDiscovery(string key) => key == "reef_dock_chart" ||
            key == "trade_rescue_seal" || key == "moon_star_chart";
        private static bool IsValidExplorationDuration(double hours) => IsValidGameDuration(hours) && hours <= ExplorationBoostLimitHours;
        private static string ExplorationDiscoveryTarget(string key) => key == "reef_dock_chart" ? "event_reef_hidden_cache" :
            key == "trade_rescue_seal" ? "gold_wreck" : key == "moon_star_chart" ? "moon_obelisk" : string.Empty;

        private void ResetExplorationVoyageState()
        {
            sailingBoostHoursRemaining = surveyBoostHoursRemaining = 0d;
            explorationDiscoveries.Clear();
        }

        private void ConsumeExplorationHours(double hours)
        {
            bool sailingExpired = sailingBoostHoursRemaining > 0d && hours >= sailingBoostHoursRemaining;
            bool surveyExpired = surveyBoostHoursRemaining > 0d && hours >= surveyBoostHoursRemaining;
            sailingBoostHoursRemaining = Math.Max(0d, sailingBoostHoursRemaining - hours);
            surveyBoostHoursRemaining = Math.Max(0d, surveyBoostHoursRemaining - hours);
            if (sailingExpired) AddLog("顺航增益已结束，航速恢复正常。");
            if (surveyExpired) AddLog("瞭望增益已结束，视野恢复正常。");
        }

        private static double ExplorationSailingHours(double distanceNm, double baseSpeed, double boostHours)
        {
            double boostedDistance = baseSpeed * 1.25d * boostHours;
            return distanceNm <= boostedDistance ? distanceNm / (baseSpeed * 1.25d) :
                boostHours + (distanceNm - boostedDistance) / baseSpeed;
        }

        private double ExplorationSailingHours(double distanceNm, double baseSpeed) =>
            ExplorationSailingHours(distanceNm, baseSpeed, sailingBoostHoursRemaining);

        private void ApplyExplorationEffect(VoyageEffect effect)
        {
            if (IsValidGameDuration(effect.SailingBoostHours) && effect.SailingBoostHours > 0d)
            {
                sailingBoostHoursRemaining = Math.Max(sailingBoostHoursRemaining, Math.Min(ExplorationBoostLimitHours, effect.SailingBoostHours));
                AddLog("获得顺航：实际航速 +25%，剩余 " + sailingBoostHoursRemaining.ToString("0.#") + " 小时（再次获得时保留较长的剩余时长，效果不叠加）。");
            }
            if (IsValidGameDuration(effect.SurveyBoostHours) && effect.SurveyBoostHours > 0d)
            {
                surveyBoostHoursRemaining = Math.Max(surveyBoostHoursRemaining, Math.Min(ExplorationBoostLimitHours, effect.SurveyBoostHours));
                AddLog("获得瞭望：视野 +35%，剩余 " + surveyBoostHoursRemaining.ToString("0.#") + " 小时（仍需获取情报才能发现隐藏地点）。");
                if (Map != null) RevealAt(Position);
            }
            if (IsKnownExplorationDiscovery(effect.GrantDiscovery) && explorationDiscoveries.Add(effect.GrantDiscovery))
                AddLog("获得本航次线索：「" + DiscoveryLabel(effect.GrantDiscovery) + "」，可解锁对应地点的特殊选项。");
            if (!string.IsNullOrWhiteSpace(effect.RevealEncounterId)) RevealExplorationTarget(effect.RevealEncounterId);
        }

        private void RevealExplorationTarget(string encounterId)
        {
            CoreMapNode target = Map?.Nodes.Where(node => node.Encounter != null && node.Encounter.Id == encounterId &&
                node.Encounter.HasChartLocation && !node.Visited).OrderBy(node => Distance(Position, node.Position)).ThenBy(node => node.Id).FirstOrDefault();
            string label = CoreVoyageEventCatalog.DiscoveryTargetName(encounterId);
            if (target == null) { AddLog("线索指向「" + label + "」，本海图没有尚未访问的对应地点。"); return; }
            if (target.IsChartVisible) { AddLog("已确认线索：「" + target.Encounter.Name + "」的位置已在海图标出。"); return; }
            target.IntelRevealed = true;
            AddLog("已根据线索在海图上标出「" + target.Encounter.Name + "」，可直接规划航路。");
        }

        private string ExplorationChoiceConditionReason(CoreEncounterChoice choice)
        {
            if (!string.IsNullOrWhiteSpace(choice.RequiredDiscovery) && !HasDiscovery(choice.RequiredDiscovery))
                return "需要本航次线索：" + DiscoveryLabel(choice.RequiredDiscovery) + "。";
            VoyageEffect effect = choice.Effect;
            bool hasExploration = !string.IsNullOrWhiteSpace(effect.GrantDiscovery) || !string.IsNullOrWhiteSpace(effect.RevealEncounterId) ||
                effect.SailingBoostHours > 0d || effect.SurveyBoostHours > 0d;
            if (!hasExploration || choice.StartsBattle) return string.Empty;
            // Availability must see the same diminished reward that resolution
            // applies, otherwise an exhausted clue service can still take payment.
            if (ActiveEncounter != null)
                for (int index = 0; index < ActiveEncounter.Choices.Count; index++)
                    if (ReferenceEquals(ActiveEncounter.Choices[index], choice)) { effect = EffectiveChoiceEffect(index); break; }
            if (HasOtherExplorationChoiceBenefit(effect)) return string.Empty;
            bool newDiscovery = IsKnownExplorationDiscovery(effect.GrantDiscovery) && !HasDiscovery(effect.GrantDiscovery);
            bool newTarget = Map != null && Map.Nodes.Any(node => node.Encounter != null &&
                node.Encounter.Id == effect.RevealEncounterId && node.Encounter.HasChartLocation && !node.Visited && !node.IsChartVisible);
            bool longerSailing = effect.SailingBoostHours > 0d && Math.Min(ExplorationBoostLimitHours, effect.SailingBoostHours) >
                Math.Max(0d, sailingBoostHoursRemaining - choice.Hours);
            bool longerSurvey = effect.SurveyBoostHours > 0d && Math.Min(ExplorationBoostLimitHours, effect.SurveyBoostHours) >
                Math.Max(0d, surveyBoostHoursRemaining - choice.Hours);
            if (newDiscovery || newTarget || longerSailing || longerSurvey) return string.Empty;
            return SeaChartRewardMultiplier(choice) <= 0d ? "此处已无探索收益，不必再次支付物资。" :
                "相关线索和地点情报均已掌握，已有增益的持续时间也不会延长。";
        }

        private static bool HasOtherExplorationChoiceBenefit(VoyageEffect e) => e.Hull > 0 || e.Water > 0 || e.Food > 0 ||
            e.Beer > 0 || e.Medicine > 0 || e.Wood > 0 || e.Iron > 0 || e.Cloth > 0 || e.Powder > 0 || e.Cannonballs > 0 ||
            e.Gold > 0 || e.Fame > 0 || e.Morale > 0 || e.Reveal > 0 || e.MapFragments > 0 || e.Gem > 0 ||
            e.AncientMaterial > 0 || e.Relic > 0 || e.BlackPearl > 0 || e.AncientCompass > 0 || e.Lantern > 0 ||
            e.TreasureClues > 0 || e.Curse < 0 ||
            e.TreasureComplete || e.ForceReturn || e.PostponeTreasureCore || !string.IsNullOrEmpty(e.TreasureMark) ||
            e.TreasureCredit > 0 || e.TreasureHeat < 0 || e.TreasureResonance > 0;
    }
}
