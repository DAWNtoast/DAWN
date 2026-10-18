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
        catalog.AppendLine("| 编号 | 卡牌 | 体系 | 阶段 | 能量 | 效果 | 内部 ID | 历史索引 |\n| --- | --- | --- | --- | ---: | --- | --- | ---: |");
        foreach (var row in all)
            catalog.AppendLine("| " + CoreBattleCardNumbering.Label(row.Card) + " | " + Cell(row.Card.Name) + " | " + School(row.Card.SchoolId) +
                " | " + Stage(row.Card.AllowedStages) + " | " + row.Card.Energy + " | " + Cell(row.Card.Description) + " | `" + row.Card.Id + "` | " + row.Index + " |");
        File.WriteAllText(Path.Combine(root, "docs/CardCatalog20261017.md"), catalog.ToString(), new UTF8Encoding(false));

        var special = new StringBuilder("# 特殊卡牌重设计（2026-10-17）\n\n");
        special.AppendLine("共 126 张：36 张船员牌、90 张部件牌。船员使用 8xxx，部件使用 9xxx。90 张部件牌全部在海战阶段使用，装配来源、故事、卡图、内部 ID 和历史索引保持对应。完整编号见 [卡牌编号与顺序](CardCatalog20261017.md)。\n");
        special.AppendLine("## 体系特权\n");
        special.AppendLine("六张传奇仪器牌打出后激活持续能力，消耗 2 能量并抽 1 张。能力持续到本场海战结束或进入接舷阶段，同类不叠加；重复激活不重置本回合额度。激活牌本身不占首张额度，激活前本回合已成功打出的同系牌计入首张。未成功出牌、取消选区、资源不足均不消耗额度。每个新的己方回合刷新额度。\n");
        special.AppendLine("| 体系 | 持续效果 |\n| --- | --- |\n| 火焰 | 每回合首张其他火焰牌结算后点燃敌舰：3 伤害，持续 2 回合；沿用燃烧取高及持续时间规则。 |\n| 秘术 | 每回合首张其他秘术牌结算后恢复 1 法力，不超过上限；不能预支该法力支付本张牌。 |\n| 诅咒 | 每回合首张其他诅咒牌结算后追加 1 咒印，最多增至 6 层，持续至少 2 回合；每层伤害至少 2，不降低已有更强咒印。 |\n| 潮汐 | 每回合首张其他潮汐牌结算后直接获得 3 潮势，不超过上限；沿用回合结束潮势清空规则。 |\n| 雷霆 | 每回合首张其他雷霆攻击结算后额外获得 1 连锁，不超过上限；非攻击牌不占该额度。 |\n| 血契 | 每回合首张其他血契牌免生命成本，覆盖献血攻击、献血护盾、血誓阵与其他海战血契牌；能量与耗材照常支付。若首张本来不耗生命，仍会使用首张额度。 |\n");
        special.AppendLine("这些是出牌激活的能力；仅装备对应部件、尚未打出该特殊牌时，不会自动激活。状态栏显示待触发／已用；血契免费时，卡面费用和实际支付同步显示为 0。\n");
        special.AppendLine("下表均为 +0。所有牌的独立成长曲线、强化上限、分阶段逐级完整效果见 [逐卡强化设计](CardUpgradeDesign20261017.md)。六项持续特权的数值与触发次数固定，不随强化增长；这六张牌的抽牌维持 +0 抽 1 张、+1 抽 2 张、+2 抽 3 张，上限为 +2。\n");
        special.AppendLine("## 全部特殊卡 +0 效果\n");
        special.AppendLine("| 编号 | 卡牌 | 来源 | 体系 | 阶段 | 能量 | 效果 |\n| --- | --- | --- | --- | --- | ---: | --- |");
        foreach (var definition in CoreSpecialCardCatalog.All.OrderBy(item => CoreBattleCardNumbering.Number(item.Card)))
        {
            CoreBattleCard card = definition.Card;
            special.AppendLine("| " + CoreBattleCardNumbering.Label(card) + " | " + Cell(card.Name) + " | " + Cell(definition.SourceName) +
                " | " + School(card.SchoolId) + " | " + Stage(card.AllowedStages) + " | " + card.Energy + " | " + Cell(card.Description) + " |");
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
