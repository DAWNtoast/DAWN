using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;

// Independent report. The existing base-card JSON schema and renderer are unchanged.
internal static class ExportUpgradeList
{
    public sealed class RankRow
    {
        public int Level;
        public string Label;
        public string Name;
        public int Energy;
        public string OtherCost;
        public string Effect;
        public string EffectRichText;
        public ExportCardList.KeywordEntry[] Keywords;
        public string UpgradeCost;
        public int? UpgradeGold;
        public int? CumulativeGold;
        public int? SourceExperience;
        public bool SameCardFaceAsPrevious;
    }

    public sealed class StageEntry
    {
        public string Stage;
        public int MaximumLevel;
        public string LimitExplanation;
        public List<RankRow> Levels = new List<RankRow>();
    }

    public sealed class CardEntry
    {
        public string Id;
        public string Number;
        public string Name;
        public string SchoolId;
        public string School;
        public string Source;
        public bool UsesSourceExperience;
        public int SharedMaximumLevel;
        public List<StageEntry> Stages = new List<StageEntry>();
    }

    public sealed class BaseReport
    {
        public List<ExportCardList.CardEntry> Cards { get; set; }
        public List<ExportCardList.GlossaryEntry> Glossary { get; set; }
        public int StageCount { get; set; }
    }

    public sealed class Report
    {
        public string Date;
        public string Basis;
        public string[] Rules;
        public int CardCount;
        public int StageCount;
        public int LevelCount;
        public List<CardEntry> Cards;
    }

    private static readonly string[] Schools = { "", "fire", "arcane", "curse", "tide", "thunder", "blood" };
    private static readonly NavalBattleStage[] Stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly MethodInfo OtherAtStage = typeof(CatalogResourceCosts).GetMethod("OtherAtStage", PrivateStatic);
    private static readonly MethodInfo ExperienceForLevel = typeof(CoreVoyageSession).GetMethod("SpecialCardExperienceForLevel", PrivateStatic);
    private static readonly string[] Rules = {
        "每张牌使用独立强化曲线，并非所有数值统一乘以同一倍率。强化可分别影响伤害、格挡、恢复、抽牌、回能、行动点或体系资源，以下以实际卡面定义为准。",
        "常规牌从基础到 +1、+1 到 +2、+2 到 +3 的单次价格分别为 120、240、480 金币；累计价格分别为 120、360、840 金币。各牌仅能强化到自身上限。",
        "同名同身份卡牌的所有副本共用强化等级；显示名称相同但编号不同的卡牌仍为不同身份。不同阶段共用已保存等级，但各阶段效果应用自己的强化上限。",
        "船员牌与部件牌随对应来源出海航行获得经验，不能花金币强化。达到 +1／+2／+3 的累计经验为 120／360／840；仍受每牌上限限制。",
        "表中的使用消耗是能量、耗材及特殊资源费用，和强化价格分开列出。X、条件收益及增效消耗保留卡面含义；不代入实战资源、不加入装备或临时加成。",
        "上限为 +0 的阶段仅列基础效果，并明确标记固定效果。不会把超过阶段上限的重复卡面伪装成更高强化等级。"
    };

    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: ExportUpgradeList.exe <output-directory> <base-card-json>");
        string output = Path.GetFullPath(args[0]);
        string baselinePath = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        BaseReport baseline = serializer.Deserialize<BaseReport>(File.ReadAllText(baselinePath, Encoding.UTF8));
        Require(baseline?.Cards != null && baseline.Cards.Count > 0, "Missing base catalogue");
        Require(baseline.Cards.Select(card => card.Id).Distinct().Count() == baseline.Cards.Count, "Duplicate base identities");
        var cards = new List<CardEntry>();
        var session = new CoreVoyageSession();
        int stageCount = 0, levelCount = 0;
        Require(ExperienceForLevel != null && OtherAtStage != null, "Required production report interface missing");
        Require(CoreBattleUpgradeBalance.CostForNextLevel(0) == 120 && CoreBattleUpgradeBalance.CostForNextLevel(1) == 240 &&
            CoreBattleUpgradeBalance.CostForNextLevel(2) == 480, "Update report explanation for changed gold prices");
        Require(Xp(1) == 120 && Xp(2) == 360 && Xp(3) == 840, "Update report explanation for changed source experience");
        for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
        {
            CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
            if (!(bool)BaseCall("Published", original)) continue;
            CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
            Require(basis != null && basis.Id == original.Id && basis.UpgradeLevel == 0, "Invalid identity: " + original.Id);
            var card = new CardEntry {
                Id = basis.Id, Number = CoreBattleCardNumbering.Label(basis), Name = basis.Name,
                SchoolId = basis.SchoolId ?? "", School = (string)BaseCall("School", basis.SchoolId),
                Source = (string)BaseCall("Source", basis), UsesSourceExperience = CoreVoyageSession.IsSpecialBattleCard(basis.Id),
                SharedMaximumLevel = session.GetCardMaximumUpgradeLevel(index)
            };
            Require(Schools.Contains(card.SchoolId), "Unknown school: " + card.Id);
            Require(Regex.IsMatch(card.Number ?? "", @"^\d+$"), "Missing number: " + card.Id);
            Require(card.SharedMaximumLevel >= 0 && card.SharedMaximumLevel <= CoreBattleUpgradeBalance.MaximumLevel, "Invalid saved cap: " + card.Id);
            ExportCardList.CardEntry prior = baseline.Cards.SingleOrDefault(row => row.Id == card.Id);
            Require(prior != null && prior.Number == card.Number && prior.Name == card.Name && prior.SchoolId == card.SchoolId &&
                prior.Source == card.Source, "Base catalogue identity differs: " + card.Id);
            foreach (NavalBattleStage stage in Stages.Where(basis.IsAllowedInStage))
            {
                CoreBattleCard stageBasis = Resolve(basis, stage);
                int maximum = CoreBattleUpgradeBalance.MaximumLevelFor(stageBasis);
                Require(maximum >= 0 && maximum <= card.SharedMaximumLevel, "Stage cap exceeds shared cap: " + card.Id);
                var stageEntry = new StageEntry { Stage = (string)BaseCall("Stage", stage), MaximumLevel = maximum };
                stageEntry.LimitExplanation = maximum == 0 ? "固定效果：本阶段无可强化等级。" : "本阶段强化上限 +" + maximum + "。";
                if (maximum < card.SharedMaximumLevel)
                    stageEntry.LimitExplanation += "同身份可在其他阶段成长至 +" + card.SharedMaximumLevel + "，本阶段效果止于 +" + maximum + "。";
                for (int level = 0; level <= maximum; level++)
                {
                    CoreBattleCard resolved = Resolve(session.GetBattleCardAtLevel(index, level), stage);
                    Require(resolved != null && resolved.Id == card.Id && resolved.UpgradeLevel == level && resolved.IsAllowedInStage(stage),
                        "Invalid stage/rank identity: " + card.Id + "/" + stage + "/" + level);
                    string effect = CoreBattleCardText.RenderTemplate(resolved.DescriptionTemplate, resolved, stage);
                    string otherCost = (string)OtherAtStage.Invoke(null, new object[] { resolved, stage });
                    Require(!string.IsNullOrWhiteSpace(effect) && !Regex.IsMatch(effect, @"[{}]|TODO|TBD"), "Incomplete effect: " + card.Id + "/" + stage + "/" + level);
                    Require(resolved.Energy >= 0 && !string.IsNullOrWhiteSpace(otherCost) && !Regex.IsMatch(otherCost, @"[{}]|TODO|TBD"), "Incomplete cost: " + card.Id);
                    var row = new RankRow { Level = level, Label = level == 0 ? "基础 +0" : "+" + level, Name = resolved.Name,
                        Energy = resolved.Energy, OtherCost = otherCost, Effect = effect, UpgradeCost = "基础",
                        EffectRichText = CoreBattleCardKeywordText.EmphasizeKeywords(effect),
                        Keywords = CoreBattleCardKeywordText.Build(resolved, effect).Select(keyword => new ExportCardList.KeywordEntry {
                            Id = baseline.Glossary.Single(term => term.Meaning == keyword.Meaning && term.Names.Contains(keyword.Keyword)).Id,
                            Name = keyword.Keyword, Meaning = keyword.Meaning
                        }).ToArray() };
                    if (level > 0)
                    {
                        if (card.UsesSourceExperience)
                        {
                            row.SourceExperience = Xp(level);
                            row.UpgradeCost = "来源累计经验 " + row.SourceExperience;
                        }
                        else
                        {
                            row.UpgradeGold = CoreBattleUpgradeBalance.CostForNextLevel(level - 1);
                            row.CumulativeGold = Enumerable.Range(0, level).Sum(CoreBattleUpgradeBalance.CostForNextLevel);
                            row.UpgradeCost = "本阶 " + row.UpgradeGold + " 金币（累计 " + row.CumulativeGold + "）";
                        }
                        RankRow previous = stageEntry.Levels[level - 1];
                        row.SameCardFaceAsPrevious = previous.Effect == row.Effect && previous.Name == row.Name &&
                            previous.Energy == row.Energy && previous.OtherCost == row.OtherCost;
                    }
                    else
                    {
                        ExportCardList.Variant old = prior.StageVariants.SingleOrDefault(value => value.Stages.Contains(stageEntry.Stage));
                        Require(old != null && old.Name == row.Name && old.Energy == row.Energy.ToString(CultureInfo.InvariantCulture) &&
                            old.OtherCost == row.OtherCost && old.Effect == row.Effect,
                            "Base card face differs from supplied catalogue: " + card.Id + "/" + stageEntry.Stage);
                    }
                    stageEntry.Levels.Add(row);
                    levelCount++;
                }
                Require(stageEntry.Levels.Count == maximum + 1 && stageEntry.Levels.Select(row => row.Level).SequenceEqual(Enumerable.Range(0, maximum + 1)),
                    "Rank range missing or duplicated: " + card.Id);
                card.Stages.Add(stageEntry);
                stageCount++;
            }
            Require(card.Stages.Count > 0, "No legal stage: " + card.Id);
            Require(prior.StageVariants.SelectMany(row => row.Stages).OrderBy(value => value).SequenceEqual(card.Stages.Select(row => row.Stage).OrderBy(value => value)),
                "Playable stages differ: " + card.Id);
            cards.Add(card);
        }
        cards = cards.OrderBy(card => Array.IndexOf(Schools, card.SchoolId)).ThenBy(card => int.Parse(card.Number, CultureInfo.InvariantCulture)).ToList();
        Require(cards.Count == baseline.Cards.Count && stageCount == baseline.StageCount, "Published card/stage count differs");
        Require(cards.Select(card => card.Id).Distinct().Count() == cards.Count && cards.Select(card => card.Number).Distinct().Count() == cards.Count,
            "Duplicate identities/numbers");
        Require(CoreSpecialCardCatalog.All.All(source => cards.Any(card => card.Id == source.Card.Id)), "Missing special-source identities");
        var report = new Report { Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Basis = "按实际卡牌独立曲线，覆盖所有现役身份及可用阶段，从基础至该阶段合法强化上限。", Rules = Rules,
            CardCount = cards.Count, StageCount = stageCount, LevelCount = levelCount, Cards = cards };
        File.WriteAllText(Path.Combine(output, "card-upgrade-data.json"), serializer.Serialize(report), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "全部卡牌强化清单.md"), Markdown(report), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "全部卡牌强化清单.html"), Html(report, File.Exists(Path.Combine(output, "card-list-data.json"))), new UTF8Encoding(false));
        var validation = new StringBuilder();
        validation.AppendLine("Base catalogue: " + baselinePath);
        validation.AppendLine("Published card identities: " + cards.Count);
        validation.AppendLine("Playable stage definitions: " + stageCount);
        validation.AppendLine("Legal rank/stage rows: " + levelCount);
        validation.AppendLine("Fixed stage definitions: " + cards.Sum(card => card.Stages.Count(stage => stage.MaximumLevel == 0)));
        validation.AppendLine("Consecutive equal card-face rows: " + cards.Sum(card => card.Stages.Sum(stage => stage.Levels.Count(row => row.SameCardFaceAsPrevious))));
        foreach (string school in Schools) validation.AppendLine((string)BaseCall("School", school) + ": " + cards.Count(card => card.SchoolId == school));
        validation.AppendLine("PASS: published identities/numbers/stages match base catalogue; every +0 name, effect and cost matches exactly; rank ranges legal and complete; no placeholders; all special sources included.");
        File.WriteAllText(Path.Combine(output, "upgrade-validation.txt"), validation.ToString(), new UTF8Encoding(false));
        Console.Write(validation);
        return 0;
    }

    private static object BaseCall(string method, object value) => typeof(ExportCardList).GetMethod(method, PrivateStatic).Invoke(null, new[] { value });
    private static int Xp(int level) => (int)ExperienceForLevel.Invoke(null, new object[] { level });
    private static CoreBattleCard Resolve(CoreBattleCard card, NavalBattleStage stage) => stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(card) : card;
    private static string PlayCost(RankRow row) => "能量 " + row.Energy + (row.OtherCost == "—" ? "；无其他消耗" : "；" + row.OtherCost);
    private static string Md(string value) => (value ?? "").Replace("\r", "").Replace("\n", "<br>").Replace("|", "\\|");
    private static string H(string value) => (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    private static string EffectHtml(string richText) => H(richText)
        .Replace("&lt;b&gt;", "<strong class=\"effect-keyword\">").Replace("&lt;/b&gt;", "</strong>")
        .Replace("\r", "").Replace("\n", "<br>");
    private static string Anchor(string school) => string.IsNullOrEmpty(school) ? "ordinary" : school;

    private static string Markdown(Report report)
    {
        var text = new StringBuilder("# 全部卡牌强化清单\n\n" + report.Date + "｜独立强化曲线｜单次价格 120／240／480 金币\n\n");
        text.AppendLine(report.CardCount + " 张卡牌；" + report.StageCount + " 个可用阶段定义；" + report.LevelCount + " 行合法等级效果。\n");
        foreach (string rule in Rules) text.AppendLine("- " + rule);
        text.AppendLine();
        foreach (string school in Schools)
        {
            CardEntry[] schoolCards = report.Cards.Where(card => card.SchoolId == school).ToArray();
            text.AppendLine("\n## " + (string)BaseCall("School", school) + "系（" + schoolCards.Length + " 张）\n");
            foreach (CardEntry card in schoolCards)
            {
                text.AppendLine("### " + card.Number + " " + Md(card.Name) + "｜" + Md(card.Source) + "\n");
                foreach (StageEntry stage in card.Stages)
                {
                    text.AppendLine("**" + stage.Stage + (stage.Levels[0].Name == card.Name ? "" : " · " + Md(stage.Levels[0].Name)) + "** · " + stage.LimitExplanation + "\n");
                    text.AppendLine("| 等级 | 强化价格／来源经验 | 使用消耗 | 效果 |");
                    text.AppendLine("| --- | --- | --- | --- |");
                    foreach (RankRow row in stage.Levels)
                        text.AppendLine("| " + row.Label + " | " + row.UpgradeCost + " | " + Md(PlayCost(row)) + " | " + Md(row.Effect) +
                            (row.SameCardFaceAsPrevious ? "<br>（本阶卡面与上阶相同。）" : "") + " |");
                    text.AppendLine();
                }
            }
        }
        return text.ToString();
    }

    private static string Html(Report report, bool hasBaseHtml)
    {
        var text = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>全部卡牌强化清单</title><style>");
        text.Append("*{box-sizing:border-box}body{margin:0;background:#f4f3ef;color:#202b31;font:16px/1.7 'Microsoft YaHei',sans-serif}header,main,nav{max-width:1260px;margin:auto;padding:24px}header{padding-bottom:12px}h1{font-size:32px;margin:0 0 8px}h2{margin:36px 0 16px;border-bottom:2px solid #247178;padding-bottom:8px}h3{margin:0 0 12px;font-size:21px}nav{position:sticky;top:0;background:#203b42;color:white;padding-top:12px;padding-bottom:12px;display:flex;gap:18px;flex-wrap:wrap;z-index:1}nav a{color:white;text-decoration:none}.meta,.note{color:#52636b}.rules{padding-left:22px}.rules li{margin:5px 0}article{background:#fff;border:1px solid #d5ddda;border-radius:8px;padding:22px;margin:20px 0;scroll-margin-top:80px}section{scroll-margin-top:80px}.stage{margin:16px 0 8px}.table-wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;min-width:680px;margin:10px 0 20px}th,td{text-align:left;vertical-align:top;border:1px solid #d7dfdd;padding:10px 12px}th{background:#e8efec;font-weight:600}td:first-child{width:85px;white-space:nowrap}td:nth-child(2){width:180px}td:nth-child(3){width:200px}tr:nth-child(even){background:#fafbf9}.fixed{color:#705a23}footer{max-width:1260px;margin:0 auto;padding:20px 24px 40px;color:#52636b}@media(max-width:700px){header,main{padding:18px}h1{font-size:26px}article{padding:14px}nav{font-size:14px;gap:12px}}@media print{nav{position:static}body{background:white}article{break-inside:avoid}th,td{font-size:11px;padding:5px}header,main{max-width:none;padding:10px}}</style></head><body><header>");
        text.Append("<h1>全部卡牌强化清单</h1><p class=\"meta\">" + H(report.Date) + " · 独立强化曲线 · 单次价格 120／240／480 金币</p><p>" + report.CardCount + " 张卡牌 · " + report.StageCount + " 个可用阶段定义 · " + report.LevelCount + " 行合法等级效果</p><ul class=\"rules\">");
        foreach (string rule in Rules) text.Append("<li>" + H(rule) + "</li>");
        text.Append("</ul></header><nav aria-label=\"卡牌体系\">");
        foreach (string school in Schools) text.Append("<a href=\"#school-" + Anchor(school) + "\">" + H((string)BaseCall("School", school)) + "</a>");
        if (hasBaseHtml) text.Append("<a href=\"全部卡牌清单.html\">基础清单</a>");
        text.Append("<a href=\"全部卡牌强化清单.md\">Markdown</a></nav><main>");
        foreach (string school in Schools)
        {
            CardEntry[] schoolCards = report.Cards.Where(card => card.SchoolId == school).ToArray();
            text.Append("<section id=\"school-" + Anchor(school) + "\"><h2>" + H((string)BaseCall("School", school)) + "系（" + schoolCards.Length + " 张）</h2>");
            foreach (CardEntry card in schoolCards)
            {
                text.Append("<article id=\"card-" + H(card.Number) + "\"><h3>" + H(card.Number + " " + card.Name) + "</h3><div class=\"meta\">" + H(card.Source) + "</div>");
                foreach (StageEntry stage in card.Stages)
                {
                    text.Append("<p class=\"stage\"><strong>" + H(stage.Stage + (stage.Levels[0].Name == card.Name ? "" : " · " + stage.Levels[0].Name)) + "</strong> · <span class=\"" + (stage.MaximumLevel == 0 ? "fixed" : "note") + "\">" + H(stage.LimitExplanation) + "</span></p><div class=\"table-wrap\"><table><thead><tr><th>等级</th><th>强化价格／来源经验</th><th>使用消耗</th><th>效果</th></tr></thead><tbody>");
                    foreach (RankRow row in stage.Levels)
                        text.Append("<tr><td>" + H(row.Label) + "</td><td>" + H(row.UpgradeCost) + "</td><td>" + H(PlayCost(row)) + "</td><td>" + EffectHtml(row.EffectRichText) +
                            (row.SameCardFaceAsPrevious ? "<br><span class=\"note\">本阶卡面与上阶相同。</span>" : "") + "</td></tr>");
                    text.Append("</tbody></table></div>");
                }
                text.Append("</article>");
            }
            text.Append("</section>");
        }
        text.Append("</main><footer>由实际卡牌定义生成。仅展示合法等级，不包含装备及实战临时加成。</footer></body></html>");
        return text.ToString();
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
