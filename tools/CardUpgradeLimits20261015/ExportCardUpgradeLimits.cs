using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class ExportCardUpgradeLimits
{
    // These explanations audit the execution switches independently of the cap API.
    // An unexpected fixed canonical card fails export instead of silently disappearing.
    private static readonly Dictionary<string, string> FixedBoardingReasons = new Dictionary<string, string>
    {
        { "fire_10", "固定免除接下来2张火焰牌各1火药；本牌燃尽，次数与费用不成长。" },
        { "fire_12", "固定持续3个己方回合；每回合首次新增自燃时复制灼烧，单次上限3层。" },
        { "fire_15", "固定转移最多4层自燃，保留原剩余次数；没有新增伤害、治疗或抽牌。" },
        { "arcane_03", "固定获得3魔力，仍受魔力上限约束；魔力是系别资源，不是行动能量。" },
        { "arcane_12", "固定保留1张手牌，下一回合该实体牌基础费用减少1；不抵消同名加价。" },
        { "arcane_14", "固定从弃牌堆取回1张奥术攻击；取回不属于抽牌，不增加回收数量。" },
        { "arcane_15", "固定提前结算最早的一项延迟攻击；伤害取决于原攻击，不由本牌强化。" },
        { "curse_02", "固定附加3层咒痕，层数上限6，每批3次敌方回合结束；咒痕不自动伤害。" },
        { "curse_04", "固定持续3个敌方回合，每回合首次实际受攻击伤害增加2层咒痕。" },
        { "curse_05", "固定使接下来2张主动攻击各附加1层咒痕；次数、层数均固定。" },
        { "curse_10", "固定使每层尚未延长的咒痕延长1次；不新增层数或伤害。" },
        { "tide_05", "固定将最多6点尚未延长的潮势延长1次；不产生新潮势。" },
    };

    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: ExportCardUpgradeLimits.exe <markdown> <csv>");
        var session = new CoreVoyageSession();
        var canonical = CoreBoardingCardCatalog.All.ToArray();
        if (canonical.Length != 103 || canonical.Select(card => card.Id).Distinct().Count() != 103)
            throw new InvalidOperationException("The canonical boarding roster must contain 103 distinct cards.");
        var fixedKeys = canonical.Where(card => CoreBattleUpgradeBalance.MaximumLevelFor(card) == 0)
            .Select(card => card.BoardingSpec.Key).OrderBy(key => key).ToArray();
        if (!fixedKeys.SequenceEqual(FixedBoardingReasons.Keys.OrderBy(key => key)))
            throw new InvalidOperationException("Fixed boarding classification changed; review execution and update the explicit fixed-effect audit.");

        var rows = new List<string[]>();
        int cards = 0, stages = 0;
        for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
        {
            CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
            if (original.AllowedStages == NavalBattleStages.None) continue;
            cards++;
            int maximum = session.GetCardMaximumUpgradeLevel(index);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                if (!original.IsAllowedInStage(stage)) continue;
                stages++;
                CoreBattleCard baseline = ForStage(session.GetBattleCardAtLevel(index, 0), stage);
                string fixedReason = CoreBattleUpgradeBalance.MaximumLevelFor(baseline) == 0 ? FixedReason(baseline) : string.Empty;
                if (CoreBattleUpgradeBalance.MaximumLevelFor(baseline) == 0 && fixedReason.Length == 0)
                    throw new InvalidOperationException("Unreviewed fixed stage definition: " + baseline.Id + "/" + stage);
                for (int level = 0; level <= maximum; level++)
                {
                    CoreBattleCard card = ForStage(session.GetBattleCardAtLevel(index, level), stage);
                    CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(card.Id);
                    int schoolDraw = SchoolDraw(card);
                    rows.Add(new[]
                    {
                        N(index), card.Id, Identity(card), card.BoardingSpec?.Key ?? string.Empty,
                        card.Name, card.SchoolId, School(card), source?.SourceKind.ToString() ??
                            (card.RequiredCrew.Length > 0 ? "Crew" : card.RequiredComponent.Length > 0 ? "Component" : "Base"),
                        source?.SourceId ?? (card.RequiredCrew.Length > 0 ? card.RequiredCrew : card.RequiredComponent),
                        source?.SourceName ?? string.Empty, stage.ToString(), StageName(stage), N(card.Energy),
                        N(maximum), N(CoreBattleUpgradeBalance.MaximumLevelFor(baseline)),
                        N(CoreBattleUpgradeBalance.HasUtilityEffect(baseline) ? CoreBattleUpgradeBalance.UtilityMaximumLevel(baseline) : 0),
                        N(level), N(card.UpgradeLevel), N(card.Damage),
                        N(stage == NavalBattleStage.Boarding ? card.BoardingBlock ?? card.Block : card.Block),
                        N(card.Repair), N(card.Draw + schoolDraw), N(card.Draw), N(schoolDraw),
                        N(card.BoardingSpec?.Key == "thunder_08" ? 1 : 0),
                        N(CoreBattleUpgradeBalance.EnergyRecovery(card)), N(card.PowderCost), N(card.CannonballCost),
                        N(card.MovementMeters), N(card.StrategyAmount), N(card.StrategyLimit),
                        card.SchoolEffect?.Mechanic.ToString() ?? string.Empty, N(card.SchoolEffect?.Power ?? 0),
                        N(card.SchoolEffect?.Secondary ?? 0), N(card.SchoolEffect?.Threshold ?? 0),
                        N(card.SchoolEffect?.Duration ?? 0), N(card.SchoolEffect?.Limit ?? 0),
                        Condition(card), fixedReason, card.Description.Replace("\r", string.Empty).Replace("\n", " / "),
                    });
                    if (card.Description.Contains("{draw}") || card.Description.Contains("{energyRecovery}"))
                        throw new InvalidOperationException("Unrendered utility placeholder: " + card.Id);
                }
            }
        }

        string[] headers = { "LibraryIndex", "CardId", "IdentityKind", "BoardingKey", "Name", "SchoolId", "SchoolName",
            "SourceKind", "SourceId", "SourceName", "Stage", "StageName", "BaseEnergyCost", "CardMaximumLevel",
            "StageDefinitionMaximumLevel", "UtilityEffectMaximumLevel", "RequestedLevel", "EffectiveCardLevel",
            "AuthoredDamage", "AuthoredBlock", "AuthoredRepair", "Draw", "PrintedDraw", "SchoolEffectDraw",
            "ConditionalAdditionalDraw", "EnergyRecovery", "PowderCost", "CannonballCost", "MovementMeters",
            "StrategyAmount", "StrategyLimit", "SchoolMechanic", "SchoolPower", "SchoolSecondary", "SchoolThreshold",
            "SchoolDuration", "SchoolLimit", "Condition", "FixedEffectAudit", "Description" };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
        using (var writer = new StreamWriter(args[1], false, new UTF8Encoding(true)))
        {
            writer.WriteLine(string.Join(",", headers.Select(Csv)));
            foreach (string[] row in rows) writer.WriteLine(string.Join(",", row.Select(Csv)));
        }
        WriteMarkdown(args[0], session, canonical, cards, stages, rows.Count);
        Console.WriteLine("Exported " + cards + " valid stable card IDs, " + stages + " stage definitions, " + rows.Count + " rank rows.");
        Console.WriteLine("Canonical boarding caps: " + string.Join(", ", new[] { 3, 2, 1, 0 }
            .Select(cap => "+" + cap + "=" + canonical.Count(card => CoreBattleUpgradeBalance.MaximumLevelFor(card) == cap))));
        Console.WriteLine("All " + fixedKeys.Length + " fixed canonical cards matched the explicit execution audit.");
        return 0;
    }

    private static CoreBattleCard ForStage(CoreBattleCard card, NavalBattleStage stage) =>
        stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(card) : card;
    private static int SchoolDraw(CoreBattleCard card) => card.SchoolEffect != null &&
        (card.SchoolEffect.Mechanic == CoreSchoolMechanic.ArcaneEcho || card.SchoolEffect.Mechanic == CoreSchoolMechanic.ThunderEcho)
        ? card.SchoolEffect.Power : 0;
    private static string Identity(CoreBattleCard card) => card.Id.StartsWith("card_boarding_", StringComparison.Ordinal)
        ? "CanonicalBoarding" : card.RequiredCrew.Length > 0 || card.RequiredComponent.Length > 0
        ? "Source" : CoreBoardingCardCatalog.IsLegacyAlias(card) ? "LegacyAlias" : "Base";
    private static string School(CoreBattleCard card) => string.IsNullOrEmpty(card.SchoolId) ? "中立" :
        CoreSchoolUnlockCatalog.Find(card.SchoolId)?.Name ?? card.SchoolId;
    private static string StageName(NavalBattleStage stage) => stage == NavalBattleStage.Preparation ? "准备" :
        stage == NavalBattleStage.Naval ? "炮战" : "接舷";
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    private static string Condition(CoreBattleCard card)
    {
        switch (card.BoardingSpec?.Key)
        {
            case "arcane_09": return "查看顶5张，选择Draw张；受剩余牌与手牌上限约束";
            case "thunder_08": return "出牌前至少3连锁时，额外抽1张；条件与额外1张不强化";
            case "thunder_07": return "至少2连锁；同名每回合一次";
            case "blood_10": return "只有本次攻击实际扣除敌方生命才抽Draw张";
            case "blood_14": return "生命不高于40%；同名每战一次并移出本场";
        }
        if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.ThunderEcho)
            return "满足既有连锁阈值才抽牌，每回合一次；阈值不强化";
        return string.Empty;
    }

    private static string FixedReason(CoreBattleCard card)
    {
        if (card.BoardingSpec != null && FixedBoardingReasons.TryGetValue(card.BoardingSpec.Key, out string reason))
            return reason;
        switch (card.Id)
        {
            case "card_approach": return "海战印刷费用为0，固定获得1行动点，不自动移动；行动点不随强化增长，整卡上限为+0。同名牌当回合每成功使用一次，后续费用仍+1。";
            case "card_special_sail_moonweave": return "固定获得3临时魔力、远离10米并取消冲撞；SchoolEffect.Upgrades=None。";
            default: return string.Empty;
        }
    }

    private static void WriteMarkdown(string path, CoreVoyageSession session, CoreBattleCard[] canonical,
        int cardCount, int stageCount, int rowCount)
    {
        var text = new StringBuilder();
        text.AppendLine("# 玩家卡牌强化上限 · 2026-10-15\n");
        text.AppendLine("此文件由真实 Core 定义与强化 API 导出，不含敌方专用卡。完整明细见 [CSV](../outputs/balance_configs/card_upgrade_limits_20261015.csv)。");
        text.AppendLine("\n覆盖 " + cardCount + " 个有效稳定卡牌 ID、" + stageCount + " 个可用阶段定义、" + rowCount + " 个逐级记录。已删除的重复通用牌不再计入；在用的历史别名、船员和部件来源保留原 ID。\n");
        text.AppendLine("## 分项规则\n");
        text.AppendLine("- 0–1 费牌的抽牌、行动能量恢复效果最多提升 1 阶；2 费最多提升 2 阶。每阶在原效果上加 1。以该阶段印刷基础费用分类，不使用临时减费或同名加价。\n- 伤害、格挡、治疗以及已设计为可成长的灼烧、减伤、反击等数值仍可到 +3，倍率依次为 1 / 1.2 / 1.5 / 2，四舍五入。复合牌只封顶抽牌/回能部分，其他效果继续成长。\n- 纯抽牌/回能牌整卡上限跟随其效果；纯固定功能牌为 +0。多阶段同一 ID 的整卡上限取各阶段所需最大值，各阶段效果独立封顶。\n- 魔力、咒痕、潮势、连锁等系别资源，资源费用、触发条件、次数、时长、选择范围和条件额外奖励不会因为抽牌/回能强化而增加。\n- 原有高阶购买记录与来源经验保留，读取时按新上限封顶有效值；不把已保存记录删除或重写为较低阶。船员/部件来源仍随航行经验成长。\n");
        text.AppendLine("- 数值取整后，整卡上限停在所有效果最后一次实际增长的等级，裁掉末尾无变化的阶级。例如基础 1 的成长为 1 / 1 / 2 / 2，只需 +2；基础 2 的 2 / 2 / 3 / 4 仍需 +3。中间的取整平台不跳级，旧记录仍完整保留。\n");
        text.AppendLine("CSV 每行对应 ID × 阶段 × 等级 0..整卡上限。AuthoredDamage/Block/Repair 是卡定义中的基础数值；系别条件、延迟、灼烧、反击和资源公式见 Description/School 字段，不将静态 0 误读为没有效果。Draw 包含 SchoolEffect.Power 型抽牌；PrintedDraw 单列原字段。雷云搜寻的条件额外 1 张独立列示。抽牌仍受手牌上限与可用牌数限制。\n");
        text.AppendLine("## 103 张标准接舷牌分类\n");
        text.AppendLine("| 整卡上限 | 数量 |\n|---|---:|");
        foreach (int cap in new[] { 3, 2, 1, 0 })
            text.AppendLine("| +" + cap + " | " + canonical.Count(card => CoreBattleUpgradeBalance.MaximumLevelFor(card) == cap) + " |");
        foreach (int cap in new[] { 3, 2, 1, 0 })
        {
            text.AppendLine("\n### +" + cap + "\n");
            foreach (var group in canonical.Where(card => CoreBattleUpgradeBalance.MaximumLevelFor(card) == cap).GroupBy(School))
                text.AppendLine("- " + group.Key + "：" + string.Join("、", group.Select(card => card.Name + "（`" + card.BoardingSpec.Key + "`）")) + "。");
        }
        text.AppendLine("\n## +0 固定效果逐张核对\n");
        text.AppendLine("核对对象是实际执行分支中的固定资源、次数与选择规则，不仅比较 Damage/Block/Repair 字段。以下每张均没有遗漏的按等级缩放数值；导出器在 +0 名单变化时会失败，要求重新人工核查。\n");
        text.AppendLine("| 牌名 / 键 | 固定原因 |\n|---|---|");
        foreach (CoreBattleCard card in canonical.Where(card => CoreBattleUpgradeBalance.MaximumLevelFor(card) == 0))
            text.AppendLine("| " + card.Name + " / `" + card.BoardingSpec.Key + "` | " + FixedBoardingReasons[card.BoardingSpec.Key] + " |");
        int fixedExtraCount = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Count(index =>
            CoreVoyageSession.CardLibrary[index].AllowedStages != NavalBattleStages.None &&
            !CoreVoyageSession.CardLibrary[index].Id.StartsWith("card_boarding_", StringComparison.Ordinal) &&
            session.GetCardMaximumUpgradeLevel(index) == 0);
        text.AppendLine("\n标准接舷目录之外还有以下 " + fixedExtraCount + " 个在用的整卡 +0 稳定 ID，均已逐项核对；其中船员来源的恶兆契约复用同一固定接舷规则：\n");
        text.AppendLine("| 稳定 ID / 牌名 | 固定原因 |\n|---|---|");
        for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
        {
            CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
            if (card.AllowedStages == NavalBattleStages.None || card.Id.StartsWith("card_boarding_", StringComparison.Ordinal) ||
                session.GetCardMaximumUpgradeLevel(index) != 0) continue;
            text.AppendLine("| `" + card.Id + "` / " + card.Name + " | " + FixedReason(card) + " |");
        }
        text.AppendLine("\n重复牌清理：并排行船、左转舵、右转舵、调转船头、直航推进的旧 ID 已退役，统一解析为迎敌推进（0费获得1行动点）；绕至侧舷统一解析为拉开距离。旧索引保留用于存档兼容，不再作为独立牌加入收藏或默认牌组；相同保留牌共用两张上限及当回合同名加价。\n");
        text.AppendLine("## 关键例子\n");
        text.AppendLine("| 牌与阶段 | 整卡上限 | 各合法阶效果 |\n|---|---:|---|");
        Example(text, session, "card_boarding_basic_09", NavalBattleStage.Boarding);
        Example(text, session, "card_boarding_basic_10", NavalBattleStage.Boarding);
        Example(text, session, "card_boarding_basic_08", NavalBattleStage.Boarding);
        Example(text, session, "card_boarding_thunder_06", NavalBattleStage.Boarding);
        Example(text, session, "card_boarding_blood_10", NavalBattleStage.Boarding);
        Example(text, session, "card_boarding_thunder_08", NavalBattleStage.Boarding);
        Example(text, session, "card_boarding_arcane_09", NavalBattleStage.Boarding);
        Example(text, session, "card_tactical_draw", NavalBattleStage.Naval);
        Example(text, session, "card_tactical_draw", NavalBattleStage.Boarding);
        Example(text, session, "card_special_sail_last_free_wind", NavalBattleStage.Naval);
        Example(text, session, "card_school_curse_b06", NavalBattleStage.Naval);
        Example(text, session, "card_navigator_course", NavalBattleStage.Naval);
        Example(text, session, "card_component_sail", NavalBattleStage.Naval);
        text.AppendLine("\n电弧导流的下一击增伤仍按 +3 成长（8 / 10 / 12 / 16），抽牌在 +1 达到 2 张后不再增加。逐命追击仍要求实际扣血；奥术研读仍只查看顶部 5 张；雷云搜寻仍要求至少 3 连锁才额外抽 1 张。\n");
        text.AppendLine("## 复跑\n");
        text.AppendLine("从仓库根目录执行；使用已安装 Unity 的 Roslyn / Mono 编译真实 Core，且不启动编辑器：\n");
        text.AppendLine("```powershell\n& ./tools/CardUpgradeLimits20261015/RunExport.ps1\n```\n");
        text.AppendLine("默认覆盖本 Markdown 与 CSV；编译日志、导出日志和源文件 SHA-256 存于 `Artifacts/CardUpgradeLimits20261015`。脚本会核对编译时源文件未变化。可用 `-UnityData` 指定其他 Unity Editor/Data 目录，`-Output` 指定审计产物目录。\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void Example(StringBuilder text, CoreVoyageSession session, string id, NavalBattleStage stage)
    {
        int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        int maximum = session.GetCardMaximumUpgradeLevel(index);
        CoreBattleCard baseline = ForStage(session.GetBattleCardAtLevel(index, 0), stage);
        var values = new List<string>();
        for (int rank = 0; rank <= maximum; rank++)
        {
            CoreBattleCard card = ForStage(session.GetBattleCardAtLevel(index, rank), stage);
            var parts = new List<string>();
            if (card.Damage > 0) parts.Add("伤害 " + card.Damage);
            if (card.Block > 0) parts.Add("格挡 " + card.Block);
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseWard) parts.Add("每层咒印格挡 " + card.SchoolEffect.Power);
            if (card.Draw + SchoolDraw(card) > 0) parts.Add("抽 " + (card.Draw + SchoolDraw(card)));
            if (CoreBattleUpgradeBalance.EnergyRecovery(card) > 0) parts.Add("回能 " + CoreBattleUpgradeBalance.EnergyRecovery(card));
            values.Add("+" + rank + "：" + string.Join("，", parts));
        }
        text.AppendLine("| " + baseline.Name + " / " + StageName(stage) + "（" + baseline.Energy + "费） | +" + maximum + " | " + string.Join("；", values) + " |");
    }
}
