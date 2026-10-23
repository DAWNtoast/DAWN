using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

// Reuse the card-frame cost definitions rather than maintaining another resource-cost table.
internal static class CatalogResourceCosts
{
    private static readonly NavalBattleStage[] Stages = {
        NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };

    internal static string Energy(CoreBattleCard card) => ByStage(card, (resolved, stage) => resolved.Energy.ToString());
    internal static string Other(CoreBattleCard card) => ByStage(card, OtherAtStage);

    private static string ByStage(CoreBattleCard card, Func<CoreBattleCard, NavalBattleStage, string> describe)
    {
        var groups = Stages.Where(card.IsAllowedInStage).Select(stage => new {
            Stage = stage,
            Cost = describe(stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(card) : card, stage)
        }).GroupBy(row => row.Cost).ToArray();
        if (groups.Length == 1) return groups[0].Key;
        return string.Join("；", groups.Select(group => string.Join("／", group.Select(row => Stage(row.Stage))) + "：" + group.Key));
    }

    private static string OtherAtStage(CoreBattleCard card, NavalBattleStage stage)
    {
        var fixedCosts = new List<string>();
        var boostCosts = new List<string>();
        string key = card.BoardingSpec?.Key;
        bool linkedSupply = key == "fire_05" || key == "arcane_05" || key == "curse_12" ||
            key == "tide_11" || key == "tide_12";
        if (card.PowderCost > 0)
            (key == "fire_05" ? boostCosts : fixedCosts).Add("火药 " + card.PowderCost);
        if (card.CannonballCost > 0) fixedCosts.Add("炮弹 " + card.CannonballCost);

        string supply = CardSupplyCostPresentation.ResourceType(card, stage);
        if (supply.Length > 0)
            boostCosts.Insert(0, Resource(supply, stage) + " " + CardSupplyCostPresentation.BaseCost(card, stage));
        string resource = CardSpecialCostPresentation.ResourceType(card);
        string amount = CardSpecialCostPresentation.CostText(card);
        if (resource.Length > 0)
        {
            if (string.IsNullOrEmpty(amount)) throw new InvalidOperationException("Missing resource cost for " + card.Id);
            (linkedSupply ? boostCosts : fixedCosts).Add(Resource(resource, stage) + " " + amount);
        }

        if (boostCosts.Count > 0) fixedCosts.Add("增效消耗：" + string.Join("＋", boostCosts));
        // Card/terrain fuel is also consumed, although it has no inventory resource badge.
        switch (key)
        {
            case "fire_06": case "fire_09": fixedCosts.Add("焚烧其他手牌 1 张"); break;
            case "fire_07": case "fire_08": fixedCosts.Add("焚烧其他手牌 1～3 张"); break;
            case "fire_10": fixedCosts.Add("焚烧本牌"); break;
            case "blood_14": fixedCosts.Add("本牌移出本场战斗"); break;
        }
        switch (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic)
        {
            case CoreNavalExpansionMechanic.HarborExplosion: fixedCosts.Add("所选 2×2 区域内的己方火场"); break;
            case CoreNavalExpansionMechanic.ThunderCollapse: fixedCosts.Add("所选己方雷栅的剩余格子"); break;
        }
        return fixedCosts.Count == 0 ? "—" : string.Join("；", fixedCosts);
    }

    private static string Resource(string resource, NavalBattleStage stage)
    {
        switch (resource)
        {
            case "mana": return stage == NavalBattleStage.Boarding ? "魔力" : "法力";
            case "health": return stage == NavalBattleStage.Boarding ? "生命值" : "船体";
            case "curse": return "咒印";
            case "tide": return "潮势";
            case "chain": return "连锁";
            case "wood": return "木板";
            case "medicine": return "药物";
            default: throw new InvalidOperationException("Unknown card resource: " + resource);
        }
    }

    private static string Stage(NavalBattleStage stage) => stage == NavalBattleStage.Preparation ? "准备" :
        stage == NavalBattleStage.Naval ? "海战" : "接舷";
}
