using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool ShowingReusableChartEncounter => session != null && session.ActiveEncounter != null &&
            !session.IsTreasureVoyage && session.GuidedTutorialLeg == 0 && !string.IsNullOrEmpty(session.CurrentChartSeed);

        private string ChartEncounterChoiceDetail(int index)
        {
            CoreEncounterChoice choice = session.ActiveEncounter.Choices[index];
            if (choice.StartsBattle)
            {
                bool guidedBoss = session.ActiveEncounter.Id == CoreGuidedTutorialContent.BossEventId;
                bool boss = guidedBoss || session.ActiveEncounter.BossKind != CoreBossKind.None;
                double multiplier = choice.Renewable ? 1d : session.ActiveEncounterRewardMultiplier;
                string details;
                if (guidedBoss && session.GuidedTutorialLeg > 0)
                    details = "金币 +" + CoreBattleRewardBalance.GoldReward(true, choice.Effect, false, multiplier, session.HasDoctor, session.HasDiver) +
                        "／夺船 +" + CoreBattleRewardBalance.GoldReward(true, choice.Effect, true, multiplier, session.HasDoctor, session.HasDiver) +
                        "；碎片×1\n返港领白色部件与「破浪者的罗盘」";
                else
                    details = CoreBattleRewardDescriptions.Victory(boss, choice.Effect, multiplier, session.HasDoctor, session.HasDiver)
                        .Replace("；", "\n");
                details += CoreBattleRewardDescriptions.AdditionalEffects(choice.Effect, multiplier, session.HasDoctor);
                var exploration = new List<string>();
                AppendExplorationChoiceDetails(exploration, session.EffectiveChoiceEffect(index));
                if (exploration.Count > 0) details += "\n胜利：" + string.Join(" · ", exploration);
                if (session.ActiveEncounter.BossKind == CoreBossKind.Route) details += "\n胜利后开放新港航道";
                if (choice.RequiredTreasureClues > 0) details += "\n需要藏宝线索 ≥" + choice.RequiredTreasureClues;
                return details;
            }
            if (!ShowingReusableChartEncounter || choice.Renewable || session.ActiveEncounterRewardMultiplier >= 1d)
            {
                if (!session.IsTreasureVoyage || session.ActiveTreasureMap.ContentVersion == 0) return choice.Detail;
                string expedition = CoreVoyageSession.TreasureExpeditionEffectSummary(session.EffectiveChoiceEffect(index));
                return string.IsNullOrEmpty(expedition) || choice.Detail.Contains(expedition)
                    ? choice.Detail : choice.Detail + "\n" + expedition;
            }
            VoyageEffect effect = session.EffectiveChoiceEffect(index);
            var values = new List<string>();
            void Add(string label, int value)
            {
                if (value != 0) values.Add(label + (value > 0 ? "+" : "") + value);
            }
            Add("金币", effect.Gold); Add("淡水", effect.Water); Add("食物", effect.Food);
            Add("啤酒", effect.Beer); Add("药品", effect.Medicine); Add("木材", effect.Wood);
            Add("铁矿", effect.Iron); Add("布料", effect.Cloth); Add("火药", effect.Powder);
            Add("炮弹", effect.Cannonballs); Add("船体", effect.Hull); Add("声望", effect.Fame);
            Add("士气", effect.Morale); Add("线索", effect.Reveal); Add("藏宝图碎片", effect.MapFragments);
            Add("宝石", effect.Gem); Add("古代材料", effect.AncientMaterial); Add("遗物", effect.Relic);
            Add("黑珍珠", effect.BlackPearl); Add("古代罗盘", effect.AncientCompass); Add("灯笼", effect.Lantern);
            Add("诅咒", effect.Curse);
            AppendExplorationChoiceDetails(values, effect);
            if (effect.ForceReturn) values.Add("返航");
            return values.Count == 0 ? "本地点收益已耗尽，耗时照常" : string.Join(" · ", values);
        }

        private void UpdateEncounterChartRewardPresentation()
        {
            if (!ShowingReusableChartEncounter || contentRoot == null) return;
            string caption;
            switch (session.ActiveEncounter.RepeatPolicy)
            {
                case CoreEncounterRepeatPolicy.RandomVoyage: caption = "本航次随机事件"; break;
                case CoreEncounterRepeatPolicy.Renewable: caption = "可重复使用的服务"; break;
                default:
                    caption = (session.ActiveEncounterClaimCount == 0 ? "首次收益" : "第 " + ((long)session.ActiveEncounterClaimCount + 1) + " 次收益") +
                        " · " + session.ActiveEncounterRewardMultiplier.ToString("0%");
                    break;
            }
            foreach (Text label in contentRoot.GetComponentsInChildren<Text>())
                if (label.name == "Encounter Journal Heading" && label.gameObject.activeInHierarchy)
                {
                    label.text = caption;
                    break;
                }
        }
    }
}
