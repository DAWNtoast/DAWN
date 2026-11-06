using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;

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
                    details = CoreBattleRewardDescriptions.Victory(boss, choice.Effect, multiplier, session.HasDoctor, session.HasDiver,
                        sparring: CoreLegendaryWarships.IsSparringEncounter(session.ActiveEncounter.Id))
                        .Replace("；", "\n");
                details += CoreBattleRewardDescriptions.AdditionalEffects(choice.Effect, multiplier, session.HasDoctor);
                var exploration = new List<string>();
                AppendExplorationChoiceDetails(exploration, session.EffectiveChoiceEffect(index));
                if (exploration.Count > 0) details += "\n胜利：" + string.Join(" · ", exploration);
                if (session.ActiveEncounter.BossKind == CoreBossKind.Route) details += "\n胜利后开放新港航道";
                return details;
            }
            if (!ShowingReusableChartEncounter || choice.Renewable || session.ActiveEncounterRewardMultiplier >= 1d)
            {
                if (!session.IsTreasureVoyage || session.ActiveTreasureMap.ContentVersion == 0) return choice.Detail;
                return AppendMissingTreasureExpeditionEffects(choice.Detail, session.EffectiveChoiceEffect(index));
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
            return string.Join(" · ", values);
        }

        private static string AppendMissingTreasureExpeditionEffects(string detail, VoyageEffect effect)
        {
            string authored = detail ?? string.Empty;
            if (effect == null) return authored;
            string compact = Regex.Replace(authored, @"\s+", string.Empty);
            var missing = new List<string>();
            if (!string.IsNullOrEmpty(effect.TreasureMark))
            {
                string label = CoreVoyageSession.TreasureMarkLabel(effect.TreasureMark);
                bool namedGrant = Regex.IsMatch(compact,
                    @"(?:取得|获得|建立)(?:了)?[「“]?" + Regex.Escape(label));
                bool phase = effect.TreasureMark == "moonlit_dawn" || effect.TreasureMark == "moonlit_dusk" ||
                    effect.TreasureMark == "moonlit_midnight";
                bool phaseGrant = phase && (compact.Contains("点亮此相") || Regex.IsMatch(compact,
                    @"点亮[「“]?" + Regex.Escape(label)));
                if (!namedGrant && !phaseGrant) missing.Add("取得" + label);
            }
            void AddNumber(string label, int value)
            {
                if (value == 0) return;
                string signed = (value > 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);
                bool present = Regex.IsMatch(compact, Regex.Escape(label + signed) + @"(?![\d.])");
                if (!present && label == "通缉" && value < 0)
                {
                    string amount = (-(long)value).ToString(CultureInfo.InvariantCulture);
                    present = Regex.IsMatch(compact, @"清除(?:至多|最多)?" + amount + @"点通缉");
                }
                if (!present) missing.Add(label + " " + signed);
            }
            // Compare individual effects, so prose aliases and a different effect order
            // cannot cause an otherwise complete authored description to be repeated.
            AddNumber("信用", effect.TreasureCredit);
            AddNumber("通缉", effect.TreasureHeat);
            AddNumber("共鸣", effect.TreasureResonance);
            if (missing.Count == 0) return authored;
            return (string.IsNullOrWhiteSpace(authored) ? string.Empty : authored + "\n") + string.Join("、", missing);
        }

    }
}
