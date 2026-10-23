using System;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class ExportSpecialCardCatalog
{
    private static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "D:/Game";
        ValidateSpecialSources();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        var all = CoreVoyageSession.CardLibrary.Select((card, index) => new { Card = card, Index = index })
            .Where(row => row.Card.AllowedStages != NavalBattleStages.None && !CoreBattleCardAliases.IsAlias(row.Card.Id) &&
                !(row.Card.AllowedStages == NavalBattleStages.Boarding && row.Card.RequiredCrew.Length == 0 &&
                row.Card.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(row.Card)))
            .OrderBy(row => CoreBattleCardNumbering.Number(row.Card)).ToArray();
        var catalog = new StringBuilder("# 卡牌编号与顺序（2026-10-17）\n\n");
        catalog.AppendLine("编号是玩家可见的卡库顺序，内部 ID 和历史存档索引保持不变。卡库、组卡列表和牌堆浏览按编号排列；实战抽牌仍遵循洗牌规则。强化版沿用基础牌编号，已退役别名共享现役对应牌的编号。\n");
        catalog.AppendLine("| 号段 | 类别 |\n| --- | --- |\n| 1xxx | 普通系别 |\n| 2xxx | 火焰 |\n| 3xxx | 秘术 |\n| 4xxx | 诅咒 |\n| 5xxx | 潮汐 |\n| 6xxx | 雷霆 |\n| 7xxx | 血契 |\n| 8xxx | 特殊船员（跨体系） |\n| 9xxx | 特殊部件（跨体系） |\n");
        catalog.AppendLine("同号段内按体系、阶段、用途、费用排序；特殊牌的体系与来源不因号段变化。以下由当前运行时目录导出，均为 +0。\n");
        catalog.AppendLine("费用按基础卡面列出，不计实战临时加价、减免或资源不足时的变化。`—` 表示无其他消耗；`X` 表示消耗全部对应资源。标注“增效消耗”的资源用于额外效果，有耗材时按规则支付、无耗材时使用基础效果；治疗所需实际支付还会受缺失生命影响。不同阶段费用不同时分别列出。\n");
        catalog.AppendLine("| 编号 | 卡牌 | 体系 | 阶段 | 能量 | 其他资源消耗 | 效果 | 内部 ID | 历史索引 |\n| --- | --- | --- | --- | ---: | --- | --- | --- | ---: |");
        foreach (var row in all)
            catalog.AppendLine("| " + CoreBattleCardNumbering.Label(row.Card) + " | " + Cell(row.Card.Name) + " | " + School(row.Card.SchoolId) +
                " | " + Stage(row.Card.AllowedStages) + " | " + Cell(CatalogResourceCosts.Energy(row.Card)) + " | " + Cell(CatalogResourceCosts.Other(row.Card)) +
                " | " + Cell(row.Card.Description) + " | `" + row.Card.Id + "` | " + row.Index + " |");
        File.WriteAllText(Path.Combine(root, "docs/CardCatalog20261017.md"), catalog.ToString(), new UTF8Encoding(false));

        var special = new StringBuilder("# 特殊卡牌重设计（2026-10-17）\n\n");
        special.AppendLine("共 126 张：36 张船员牌、90 张部件牌。船员使用 8xxx，部件使用 9xxx。90 张部件牌全部在海战阶段使用，装配来源、故事、卡图、内部 ID 和历史索引保持对应。完整编号见 [卡牌编号与顺序](CardCatalog20261017.md)。\n");
        special.AppendLine("## 史诗与传说部件的场地联动\n");
        special.AppendLine("六系各有 5 张史诗和 5 张传说部件牌，共 60 张。火焰、秘术、诅咒、潮汐、雷霆、血契分别联动己方火场、折射阵、禁域、定向水流、雷栅、血誓阵。\n");
        special.AppendLine("| 部位 | 场地联动 |\n| --- | --- |\n| 船首 | 攻击并向相邻空格扩展己方本系场地。 |\n| 船帆 | 获得行动点，并延长己方本系场地的持续时间。 |\n| 火炮 | 攻击位于己方本系场地上的敌舰时提高伤害。 |\n| 船甲 | 位于己方本系场地时额外获得格挡；火焰、诅咒、潮汐、雷霆提供本系场地免疫，秘术恢复法力，血契修复船体。 |\n| 史诗仪器 | 根据己舰或敌舰是否位于己方本系场地，获得本系收益。 |\n| 传说仪器 | 立即扩展并延长己方本系场地，同时抽牌。 |\n");
        special.AppendLine("扩场作用于己舰 4 格内的己方本系场地，优先向敌舰方向扩展到没有场地和船只的相邻格；新增格同样须在 4 格内，并继承原场地的持续时间和水流方向。延时作用于至少有一格在 4 格内的整处己方本系场地。场地不存在时仍可使用卡牌，但不获得对应场地收益。\n");
        special.AppendLine("下表均为 +0。所有牌的独立成长曲线、强化上限、分阶段逐级完整效果见 [逐卡强化设计](CardUpgradeDesign20261017.md)。传说仪器的延时固定为 2 回合，扩场格数与抽牌随强化成长。\n");
        special.AppendLine("## 全部特殊卡 +0 效果\n");
        special.AppendLine("费用列与全卡目录一致：`X` 表示全部对应资源；“增效消耗”用于额外效果，缺少耗材时使用基础效果。\n");
        special.AppendLine("| 编号 | 卡牌 | 来源 | 体系 | 阶段 | 能量 | 其他资源消耗 | 效果 |\n| --- | --- | --- | --- | --- | ---: | --- | --- |");
        foreach (var definition in CoreSpecialCardCatalog.All.OrderBy(item => CoreBattleCardNumbering.Number(item.Card)))
        {
            CoreBattleCard card = definition.Card;
            special.AppendLine("| " + CoreBattleCardNumbering.Label(card) + " | " + Cell(card.Name) + " | " + Cell(definition.SourceName) +
                " | " + School(card.SchoolId) + " | " + Stage(card.AllowedStages) + " | " + Cell(CatalogResourceCosts.Energy(card)) +
                " | " + Cell(CatalogResourceCosts.Other(card)) + " | " + Cell(card.Description) + " |");
        }
        File.WriteAllText(Path.Combine(root, "docs/SpecialCardRedesign20261017.md"), special.ToString(), new UTF8Encoding(false));
        Console.WriteLine("Exported " + all.Length + " numbered cards and " + CoreSpecialCardCatalog.All.Count + " special cards.");
        return 0;
    }
    private static void ValidateSpecialSources()
    {
        var special = CoreSpecialCardCatalog.All;
        if (special.Count != 126 || special.Count(item => item.SourceKind == CoreSpecialCardSourceKind.Crew) != 36 ||
            special.Count(item => item.SourceKind == CoreSpecialCardSourceKind.Component) != 90 ||
            special.Select(item => item.Card.Id).Distinct().Count() != 126)
            throw new InvalidOperationException("The export requires 126 unique source cards: 36 crew and 90 components.");
        foreach (var source in special)
        {
            bool component = source.SourceKind == CoreSpecialCardSourceKind.Component;
            if (CoreBattleCardNumbering.Number(source.Card) / 1000 != (component ? 9 : 8))
                throw new InvalidOperationException("Unexpected source-card number: " + source.Card.Id);
            if (component && source.Card.AllowedStages != NavalBattleStages.Naval)
                throw new InvalidOperationException("Component card is not naval-only: " + source.Card.Id);
            if (source.Card.Description.Contains("{") || source.Card.Description.Contains("}"))
                throw new InvalidOperationException("Unresolved description placeholder: " + source.Card.Id);
        }
        Console.WriteLine("Verified 126 unique special cards, 36 crew in 8xxx, and 90 naval-only components in 9xxx.");
    }
    private static string Cell(string value) => (value ?? "").Replace("|", "／").Replace("\r", "").Replace("\n", " ");
    private static string School(string value)
    {
        switch (value) { case "fire": return "火焰"; case "arcane": return "秘术"; case "curse": return "诅咒";
            case "tide": return "潮汐"; case "thunder": return "雷霆"; case "blood": return "血契"; default: return "普通"; }
    }
    private static string Stage(NavalBattleStages stages) => string.Join("／", new[] {
        (stages & NavalBattleStages.Preparation) != 0 ? "准备" : null,
        (stages & NavalBattleStages.Naval) != 0 ? "海战" : null,
        (stages & NavalBattleStages.Boarding) != 0 ? "接舷" : null }.Where(value => value != null));
}
