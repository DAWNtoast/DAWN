using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class ExportUpgradeDesign
{
    private sealed class Entry
    {
        public int Index, Maximum;
        public CoreBattleCard Original;
        public CoreCardUpgradeProfile Profile;
        public readonly Dictionary<NavalBattleStage, CoreBattleCard[]> Stages = new Dictionary<NavalBattleStage, CoreBattleCard[]>();
    }

    private static readonly List<string> failures = new List<string>();
    private static readonly NavalBattleStage[] stageOrder = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: ExportUpgradeDesign.exe <workspace> <artifact-directory>");
        string root = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var session = new CoreVoyageSession();
        Entry[] entries = CoreVoyageSession.CardLibrary.Select((card, index) => new Entry { Original = card, Index = index })
            .Where(row => Published(row.Original)).OrderBy(row => CoreBattleCardNumbering.Number(row.Original)).ToArray();
        var numbers = new HashSet<int>();
        var csv = new List<string[]>();
        int checkedTransitions = 0, checkedStages = 0, actionPointTransitions = 0;
        foreach (Entry entry in entries)
        {
            CoreBattleCard original = entry.Original;
            entry.Profile = CoreCardUpgradeDesign.Find(original);
            entry.Maximum = session.GetCardMaximumUpgradeLevel(entry.Index);
            Check(entry.Profile != null, original.Id, "mapping", "Missing authored upgrade profile.");
            Check(entry.Profile == null || entry.Maximum == entry.Profile.MaximumLevel, original.Id, "mapping", "Runtime maximum differs from authored maximum.");
            Check(!string.IsNullOrWhiteSpace(entry.Profile?.Note), original.Id, "mapping", "Missing card role note.");
            int number = CoreBattleCardNumbering.Number(original);
            Check(number > 0 && numbers.Add(number), original.Id, "number", "Missing or duplicate published card number.");
            foreach (NavalBattleStage stage in stageOrder.Where(original.IsAllowedInStage))
            {
                checkedStages++;
                var ranks = new CoreBattleCard[entry.Maximum + 1];
                entry.Stages.Add(stage, ranks);
                for (int rank = 0; rank <= entry.Maximum; rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(entry.Index, rank);
                    if (stage == NavalBattleStage.Boarding) card = CoreBoardingCardCatalog.ForBoarding(card);
                    ranks[rank] = card;
                    Check(card != null && card.Id == original.Id && card.UpgradeLevel == rank, original.Id, "mapping", Stage(stage) + " +" + rank + " identity/rank mismatch.");
                    if (card == null) continue;
                    Check(!string.IsNullOrWhiteSpace(card.Description) && !card.Description.Contains("{") && !card.Description.Contains("}") &&
                        !card.Description.Contains("TODO") && !card.Description.Contains("TBD"), original.Id, "description", Stage(stage) + " +" + rank + " empty description or unresolved placeholder.");
                    if (rank > 0)
                    {
                        Check(FixedTerms(card) == FixedTerms(ranks[0]), original.Id, "fixed-terms", Stage(stage) + " +" + rank + " changed costs, range, duration, or resource caps.");
                        Check(card.NavalActionPointGain >= ranks[rank - 1].NavalActionPointGain, original.Id, "action-points", Stage(stage) + " +" + rank + " reduced the authored AP gain.");
                    }
                    csv.Add(ExportRow(entry, stage, rank, card));
                }
            }
            for (int rank = 1; rank <= entry.Maximum; rank++)
            {
                checkedTransitions++;
                bool actionPointGrowth = entry.Stages.TryGetValue(NavalBattleStage.Naval, out CoreBattleCard[] navalRanks) &&
                    navalRanks[rank].NavalActionPointGain > navalRanks[rank - 1].NavalActionPointGain;
                if (actionPointGrowth) actionPointTransitions++;
                Check(actionPointGrowth || entry.Stages.Values.Any(ranks => ranks[rank].Description != ranks[rank - 1].Description), original.Id,
                    "growth", "+" + (rank - 1) + " -> +" + rank + " has no visible effect change or naval AP gain in any playable stage.");
                if (actionPointGrowth)
                    Check(navalRanks[rank].Description != navalRanks[rank - 1].Description, original.Id,
                        "action-points-description", "+" + rank + " increased naval AP without updating the naval card description.");
            }
        }

        var document = new StringBuilder("# 逐卡强化设计（2026-10-17）\n\n");
        document.AppendLine("本表由当前 Core 运行时导出：每张现役可见牌使用 `GetBattleCardAtLevel` 获取 +0 至独立上限的实际定义；接舷阶段再经过 `CoreBoardingCardCatalog.ForBoarding`。同一身份的不同阶段分别显示完整 `Description`，相同描述合并阶段标签。这里只展示基础卡面；实战临时增减伤、减费、手牌与资源上限仍按战斗状态结算。\n");
        document.AppendLine("每张牌拥有独立成长曲线和强化上限，定位说明与曲线都按该牌身份配置。纯固定功能牌上限为 +0；费用、系别资源生成与消耗、触发条件、次数、持续时间和范围保持原规则。伤害、格挡、治疗与机制收益按各牌曲线成长；抽牌、回能和海战行动点（AP）的获得量分别按逐卡配置的整数增量成长，并非所有卡牌都增加这些收益。\n");
        document.AppendLine("同类部件按可比较的主要效果贡献，在相同强化等级（较低上限按其上限封顶）及各自满级保留品质成长优势。机制不同的牌，例如施加咒印与引爆咒印、献血攻击与吸血攻击，不能据此宣称逐字段支配；费用、条件和用途仍需一起考虑。船员允许低初始值、较高成长的定位在特定效果上反超，示例见下表。\n");
        document.AppendLine("内部 ID、历史卡库索引、已有强化记录与船员／部件来源经验均保持不变；有效等级读取时按该牌上限封顶，不删除原记录。编号见 [卡牌编号与顺序](CardCatalog20261017.md)，特殊机制见 [特殊卡牌重设计](SpecialCardRedesign20261017.md)。六张传奇仪器的持续特权固定，只有抽牌按 +0／+1／+2 = 1／2／3 成长。\n");
        document.AppendLine("`—` 表示超过该牌上限，不存在该强化档。表内原始换行保留为分行，所有数值直接来自当前运行时，未手工重写效果。\n");
        document.AppendLine("## 上限统计\n");
        document.AppendLine("| 类别 | +0 | +1 | +2 | +3 | 合计 |\n| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var group in entries.GroupBy(row => Category(row.Original)).OrderBy(group => group.Key))
            document.AppendLine(CountRow(CategoryName(group.Key), group.ToArray()));
        document.AppendLine(CountRow("全部现役可见牌", entries));
        document.AppendLine("\n共 " + entries.Length + " 张现役可见身份、" + checkedStages + " 个可用阶段定义、" + csv.Count + " 个逐级阶段记录；不重复列出退役身份或已合并的纯接舷别名。\n");
        document.AppendLine("8xxx／9xxx 统计包含仍可见的通用来源牌。本次按具体身份设计的专属特殊牌为 " +
            CoreSpecialCardCatalog.All.Count(source => source.SourceKind == CoreSpecialCardSourceKind.Crew) + " 张船员牌与 " +
            CoreSpecialCardCatalog.All.Count(source => source.SourceKind == CoreSpecialCardSourceKind.Component) + " 张部件牌，共 " +
            CoreSpecialCardCatalog.All.Count + " 张；其余为原有通用船员／部件来源身份。\n");
        AppendCrewComparisons(document, entries);
        document.AppendLine("## 全部卡牌逐级效果\n");
        document.AppendLine("| 编号 | 卡牌／内部 ID | 体系／来源 | 上限 | 定位说明 | +0 | +1 | +2 | +3 |\n| --- | --- | --- | ---: | --- | --- | --- | --- | --- |");
        foreach (Entry entry in entries)
        {
            document.Append("| " + CoreBattleCardNumbering.Label(entry.Original) + " | " + Cell(entry.Original.Name) + "<br>`" + entry.Original.Id + "` | " +
                Cell(School(entry.Original.SchoolId) + "／" + Source(entry.Original)) + " | +" + entry.Maximum + " | " + Cell(entry.Profile?.Note) + " |");
            for (int rank = 0; rank <= CoreBattleUpgradeBalance.MaximumLevel; rank++)
                document.Append(" " + (rank > entry.Maximum ? "—" : Descriptions(entry, rank)) + " |");
            document.AppendLine();
        }
        document.AppendLine("\n导出校验包含：现役身份到逐卡档案和编号的映射、每个有效强化阶在至少一个可用阶段有实际描述或海战行动点收益、行动点成长与海战卡面同步、全部阶段与等级无未替换占位符、固定费用及移动／持续参数未漂移。详细逐级数据与校验报告位于 `Artifacts/CardUpgradeDesign20261017/Catalog`。这些是运行时定义校验；实际战斗机制另由项目回归测试验证。\n");

        string[] headers = { "Number", "LibraryIndex", "CardId", "Name", "School", "Source", "MaximumLevel", "Role", "Stage", "Level", "Damage", "Block", "BoardingBlock", "Repair", "Draw", "EnergyRecovery", "EnergyCost", "PowderCost", "CannonballCost", "NavalActionPoints", "MovementMeters", "StrategyAmount", "StrategyLimit", "BurnDamage", "BurnDuration", "SchoolMechanic", "SchoolPower", "SchoolSecondary", "SchoolThreshold", "SchoolDuration", "SchoolLimit", "NumericalMultiplier", "BlockMultiplier", "RepairMultiplier", "EffectMultiplier", "Description" };
        using (var writer = new StreamWriter(Path.Combine(output, "card-upgrade-levels.csv"), false, new UTF8Encoding(true)))
        {
            writer.WriteLine(string.Join(",", headers.Select(Csv)));
            foreach (string[] row in csv) writer.WriteLine(string.Join(",", row.Select(Csv)));
        }
        File.WriteAllText(Path.Combine(output, "failures.tsv"), "CardId\tCheck\tDetail\n" + string.Join("\n", failures) + "\n", new UTF8Encoding(false));
        string report = "Published cards: " + entries.Length + "\nAuthored profiles: " + CoreCardUpgradeDesign.All.Count +
            "\nPlayable stage definitions: " + checkedStages + "\nRank/stage rows: " + csv.Count + "\nChecked meaningful rank transitions: " + checkedTransitions +
            "\nNaval AP growth transitions: " + actionPointTransitions +
            "\nCaps: " + string.Join(", ", Enumerable.Range(0, 4).Select(rank => "+" + rank + "=" + entries.Count(row => row.Maximum == rank))) +
            "\nValidation failures: " + failures.Count + "\n";
        File.WriteAllText(Path.Combine(output, "validation.txt"), report, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "card-upgrade-design.draft.md"), document.ToString(), new UTF8Encoding(false));
        Console.Write(report);
        if (failures.Count != 0) { foreach (string failure in failures) Console.WriteLine(failure); return 1; }
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs/CardUpgradeDesign20261017.md"), document.ToString(), new UTF8Encoding(false));
        Console.WriteLine("Published docs/CardUpgradeDesign20261017.md; mapping, stage growth, fixed terms, placeholders, and crew examples passed.");
        return 0;
    }

    private static bool Published(CoreBattleCard card) => card.AllowedStages != NavalBattleStages.None && !CoreBattleCardAliases.IsAlias(card.Id) &&
        !(card.AllowedStages == NavalBattleStages.Boarding && card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(card));
    private static void Check(bool valid, string id, string kind, string detail) { if (!valid) failures.Add(id + "\t" + kind + "\t" + detail); }
    private static string FixedTerms(CoreBattleCard card) => string.Join("/", new[] { card.Energy, card.PowderCost, card.CannonballCost,
        card.MovementMeters, card.StrategyLimit, card.BurnDuration, card.SchoolEffect?.Duration ?? 0,
        card.SchoolEffect?.Limit ?? 0, card.SchoolEffect?.WoodCost ?? 0 }.Select(N));
    private static string[] ExportRow(Entry entry, NavalBattleStage stage, int rank, CoreBattleCard card) => new[] {
        CoreBattleCardNumbering.Label(card), N(entry.Index), card.Id, card.Name, School(card.SchoolId), Source(card), N(entry.Maximum), entry.Profile?.Note,
        Stage(stage), N(rank), N(card.Damage), N(card.Block), N(card.BoardingBlock ?? card.Block), N(card.Repair), N(card.Draw),
        N(CoreBattleUpgradeBalance.EnergyRecovery(card)), N(card.Energy), N(card.PowderCost), N(card.CannonballCost), N(card.NavalActionPointGain),
        N(card.MovementMeters), N(card.StrategyAmount), N(card.StrategyLimit), N(card.BurnDamage), N(card.BurnDuration),
        card.SchoolEffect?.Mechanic.ToString() ?? "", N(card.SchoolEffect?.Power ?? 0), N(card.SchoolEffect?.Secondary ?? 0),
        N(card.SchoolEffect?.Threshold ?? 0), N(card.SchoolEffect?.Duration ?? 0), N(card.SchoolEffect?.Limit ?? 0),
        D(CoreCardUpgradeDesign.Multiplier(card, rank)), D(entry.Profile?.BlockMultiplier(rank) ?? CoreCardUpgradeDesign.Multiplier(card, rank)),
        D(entry.Profile?.RepairMultiplier(rank) ?? CoreCardUpgradeDesign.Multiplier(card, rank)), D(CoreCardUpgradeDesign.EffectMultiplier(card, rank)), card.Description };
    private static string Descriptions(Entry entry, int rank) => string.Join("<br><br>", entry.Stages
        .GroupBy(pair => pair.Value[rank].Description)
        .Select(group => "【" + string.Join("／", group.Select(pair => Stage(pair.Key))) + "】<br>" + Cell(group.Key)));
    private static string CountRow(string title, Entry[] entries) => "| " + title + " | " +
        string.Join(" | ", Enumerable.Range(0, 4).Select(rank => N(entries.Count(row => row.Maximum == rank)))) + " | " + entries.Length + " |";
    private static int Category(CoreBattleCard card) => CoreBattleCardNumbering.Number(card) / 1000;
    private static string CategoryName(int category) => new[] { "未编号", "普通", "火焰", "秘术", "诅咒", "潮汐", "雷霆", "血契", "船员号段（含通用）", "部件号段（含通用）" }[category];
    private static string Source(CoreBattleCard card)
    {
        CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(card.Id);
        if (source != null) return (source.SourceKind == CoreSpecialCardSourceKind.Crew ? "船员：" : "部件：") + source.SourceName;
        if (card.RequiredCrew.Length > 0) return "通用船员：" + card.RequiredCrew;
        if (card.RequiredComponent.Length > 0) return "通用部件：" + card.RequiredComponent;
        return "常规卡牌";
    }
    private static string School(string school) => string.IsNullOrEmpty(school) ? "普通" : CoreSchoolUnlockCatalog.Find(school)?.Name ?? school;
    private static string Stage(NavalBattleStage stage) => stage == NavalBattleStage.Preparation ? "准备" : stage == NavalBattleStage.Naval ? "海战" : "接舷";
    private static string Cell(string value) => (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("|", "&#124;").Replace("\r", "").Replace("\n", "<br>");
    private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static void AppendCrewComparisons(StringBuilder document, Entry[] entries)
    {
        document.AppendLine("## 船员后期反超示例\n");
        document.AppendLine("以下只比较标出的单项基础效果，均取双方可用的同一阶段与各自满级；不表示总强度、单位能量收益或全部机制更强。曲线依次列出 +0 至该牌上限。\n");
        document.AppendLine("| 成长型船员牌 | 对照船员牌 | 阶段／比较项 | 成长型曲线 | 对照曲线 | 满级结果 |\n| --- | --- | --- | --- | --- | --- |");
        CrewComparison(document, entries, "crew_gray_gull_navigator_evelyn", "crew_broken_anchor_navigator_lorne", "格挡", card => card.Block);
        CrewComparison(document, entries, "crew_white_shell_cook_flora", "crew_gray_gull_doctor_moer", "治疗", card => card.Repair);
        CrewComparison(document, entries, "crew_white_shell_diver_kela", "crew_broken_anchor_diver_udo", "伤害", card => card.Damage);
        CrewComparison(document, entries, "crew_white_shell_gunner_gallo", "crew_broken_anchor_gunner_mira", "伤害", card => card.Damage);
        document.AppendLine();
    }
    private static void CrewComparison(StringBuilder document, Entry[] entries, string growthId, string earlyId, string metric, Func<CoreBattleCard, int> value)
    {
        Entry growth = entries.FirstOrDefault(row => row.Original.Id == "card_special_" + growthId);
        Entry early = entries.FirstOrDefault(row => row.Original.Id == "card_special_" + earlyId);
        if (growth == null || early == null) { Check(false, growthId, "crew-example", "Missing compared crew identity."); return; }
        NavalBattleStage[] stages = new[] { NavalBattleStage.Naval, NavalBattleStage.Boarding, NavalBattleStage.Preparation }
            .Where(stage => growth.Stages.ContainsKey(stage) && early.Stages.ContainsKey(stage)).ToArray();
        if (stages.Length == 0) { Check(false, growthId, "crew-example", "No shared playable stage."); return; }
        NavalBattleStage selected = stages[0];
        CoreBattleCard[] a = growth.Stages[selected], b = early.Stages[selected];
        Check(value(a[0]) <= value(b[0]) && value(a.Last()) > value(b.Last()), growthId, "crew-example", "Authored growth no longer overtakes the selected main effect.");
        document.AppendLine("| " + CoreBattleCardNumbering.Label(growth.Original) + " " + Cell(growth.Original.Name) + "（+" + growth.Maximum + "） | " +
            CoreBattleCardNumbering.Label(early.Original) + " " + Cell(early.Original.Name) + "（+" + early.Maximum + "） | " + Stage(selected) + "／" + metric + " | " +
            string.Join(" → ", a.Select(card => N(value(card)))) + " | " + string.Join(" → ", b.Select(card => N(value(card)))) + " | " + value(a.Last()) + " > " + value(b.Last()) + " |");
    }
}
