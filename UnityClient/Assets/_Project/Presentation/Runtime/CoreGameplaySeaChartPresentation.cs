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
            if (!ShowingReusableChartEncounter || choice.Renewable || session.ActiveEncounterRewardMultiplier >= 1d)
                return choice.Detail;
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
            if (choice.StartsBattle) values.Add("胜利战利品×" + session.ActiveEncounterRewardMultiplier.ToString("0%"));
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
