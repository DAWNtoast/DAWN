using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Display quotes use the same cash and fame rules as victory settlement.</summary>
    public static class CoreBattleRewardDescriptions
    {
        public static string Victory(bool boss, VoyageEffect reward, double rewardMultiplier = 1d,
            bool doctor = false, bool diver = false) =>
            "击沉：金币 +" + CoreBattleRewardBalance.GoldReward(boss, reward, false, rewardMultiplier, doctor, diver) +
            "、声望 +" + CoreBattleRewardBalance.FameReward(boss, reward, false, rewardMultiplier) +
            "；夺船×2：金币 +" + CoreBattleRewardBalance.GoldReward(boss, reward, true, rewardMultiplier, doctor, diver) +
            "、声望 +" + CoreBattleRewardBalance.FameReward(boss, reward, true, rewardMultiplier);

        public static string AdditionalEffects(VoyageEffect effect, double rewardMultiplier = 1d, bool doctor = false)
        {
            if (effect == null) return string.Empty;
            var costs = new List<string>();
            void Cost(string label, int value) { if (value < 0) costs.Add(label + " " + value); }
            Cost("金币", effect.Gold); Cost("声望", effect.Fame); Cost("船体", effect.Hull); Cost("士气", effect.Morale);
            Cost("淡水", effect.Water); Cost("食物", effect.Food); Cost("啤酒", effect.Beer);
            Cost("药品", doctor && effect.Medicine < 0 ? -(int)Math.Ceiling(-(double)effect.Medicine * .75d) : effect.Medicine);
            Cost("木材", effect.Wood); Cost("铁矿", effect.Iron); Cost("布料", effect.Cloth);
            Cost("火药", effect.Powder); Cost("炮弹", effect.Cannonballs); Cost("宝石", effect.Gem);
            Cost("古代材料", effect.AncientMaterial); Cost("遗物", effect.Relic); Cost("黑珍珠", effect.BlackPearl);
            Cost("古代罗盘", effect.AncientCompass); Cost("航海灯", effect.Lantern);
            Cost("藏宝图碎片", effect.MapFragments); Cost("藏宝线索", effect.TreasureClues); Cost("诅咒", effect.Curse);
            var progress = new List<string>();
            double multiplier = double.IsNaN(rewardMultiplier) ? 0d : Math.Max(0d, Math.Min(1d, rewardMultiplier));
            long Scale(long value) => (long)Math.Floor(value * multiplier);
            void Gain(string label, long value) { if (value > 0) progress.Add(label + " +" + value); }
            if (effect.Hull > 0) Gain("船体", Scale((long)effect.Hull + (doctor ? 2 : 0)));
            if (effect.Morale > 0) Gain("士气", Scale(effect.Morale));
            if (effect.Reveal > 0) Gain("情报", Scale(effect.Reveal));
            if (effect.TreasureClues > 0) Gain("藏宝线索", effect.TreasureClues);
            if (effect.Curse > 0) Gain("诅咒", effect.Curse);
            if (effect.MapFragments > 0)
            {
                // Authored fragment effects retain their previous integer scaling;
                // the separate boss fragment roll is not part of the cash quote.
                long naval = Scale(effect.MapFragments), boarding = Scale((long)Math.Ceiling(effect.MapFragments * 1.5d));
                if (naval > 0 || boarding > 0)
                    progress.Add("藏宝图碎片 +" + naval + (boarding == naval ? string.Empty : "／夺船 +" + boarding));
            }
            return (costs.Count > 0 ? "\n入战：" + string.Join("、", costs) : string.Empty) +
                (progress.Count > 0 ? "\n胜利：" + string.Join("、", progress) : string.Empty);
        }
    }
}
