using System.Collections.Generic;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private struct VoyageChoiceLogState
        {
            public VoyageCargo Cargo;
            public int Hull, Morale, Fragments, Clues, RevealedLocations;
            public double Hours;
        }

        private VoyageChoiceLogState CaptureVoyageChoiceLogState() => new VoyageChoiceLogState
        {
            Cargo = SnapshotCargo(Cargo), Hull = Hull, Morale = Morale,
            Fragments = voyageTreasureFragments, Clues = TreasureClues, Hours = TotalHours,
            RevealedLocations = CountChoiceRevealedLocations(),
        };

        private int CountChoiceRevealedLocations()
        {
            int count = 0;
            if (Map != null)
                foreach (CoreMapNode node in Map.Nodes)
                    if (node != null && node.IntelRevealed && node.Encounter != null && node.Encounter.CanRevealByIntel) count++;
            return count;
        }

        private void RecordVoyageChoiceResult(CoreEncounterChoice choice, VoyageChoiceLogState before)
        {
            // Compare the committed state so ration costs, repeat discounts,
            // crew benefits and hull/morale caps are reflected exactly once.
            var changes = new List<string>();
            void Add(string label, int previous, int current)
            {
                long delta = (long)current - previous;
                if (delta != 0) changes.Add(label + " " + (delta > 0 ? "+" : "") + delta.ToString(CultureInfo.InvariantCulture));
            }
            Add("金币", before.Cargo.Gold, Cargo.Gold);
            Add("声望", before.Cargo.Fame, Cargo.Fame);
            Add("船体", before.Hull, Hull);
            Add("士气", before.Morale, Morale);
            Add("淡水", before.Cargo.Water, Cargo.Water);
            Add("食物", before.Cargo.Food, Cargo.Food);
            Add("啤酒", before.Cargo.Beer, Cargo.Beer);
            Add("药品", before.Cargo.Medicine, Cargo.Medicine);
            Add("木材", before.Cargo.Wood, Cargo.Wood);
            Add("铁矿", before.Cargo.Iron, Cargo.Iron);
            Add("布料", before.Cargo.Cloth, Cargo.Cloth);
            Add("火药", before.Cargo.Powder, Cargo.Powder);
            Add("炮弹", before.Cargo.Cannonballs, Cargo.Cannonballs);
            Add("宝石", before.Cargo.Gem, Cargo.Gem);
            Add("古代材料", before.Cargo.AncientMaterial, Cargo.AncientMaterial);
            Add("遗物", before.Cargo.Relic, Cargo.Relic);
            Add("黑珍珠", before.Cargo.BlackPearl, Cargo.BlackPearl);
            Add("古代罗盘", before.Cargo.AncientCompass, Cargo.AncientCompass);
            Add("航海灯", before.Cargo.Lantern, Cargo.Lantern);
            Add("诅咒", before.Cargo.Curse, Cargo.Curse);
            Add("藏宝图碎片", before.Fragments, voyageTreasureFragments);
            Add("藏宝线索", before.Clues, TreasureClues);
            int revealedLocations = CountChoiceRevealedLocations();
            if (revealedLocations > before.RevealedLocations)
                Add("揭示隐藏地点", before.RevealedLocations, revealedLocations);

            string result = "选择「" + choice.Label + "」：耗时 " +
                (TotalHours - before.Hours).ToString("0.###", CultureInfo.InvariantCulture) + " 小时；";
            result += changes.Count == 0 ? "物资和船况没有变化" :
                "结算结果（含期间消耗的口粮）：" + string.Join("、", changes);
            if (choice.StartsBattle)
                result += Hull > 0 ? "；进入战斗，获胜后结算奖励" : "；船体已损毁，未进入战斗";
            else if (Hull > 0 && IsTreasureVoyage && choice.Effect.PostponeTreasureCore && !choice.Effect.TreasureComplete)
                result += "；秘藏地点已保留，准备妥当后可再来探索";
            // Authored clues, vision/speed boosts and expedition progress already
            // emit their own outcome logs while ApplyEffect commits them.
            AddLog(result + "。");
        }
    }
}
