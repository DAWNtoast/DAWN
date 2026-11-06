using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;

// Read-only export of every current card identity, independent of player unlocks,
// equipment, inventory, battle state and saved upgrade levels.
internal static class ExportCardList
{
    public sealed class GlossaryEntry
    {
        public string Id;
        public string[] Names;
        public string Meaning;
    }
    public sealed class KeywordEntry
    {
        public string Id;
        public string Name;
        public string Meaning;
    }
    public sealed class Variant
    {
        public List<string> Stages = new List<string>();
        public string Name;
        public string Energy;
        public string OtherCost;
        public string Effect;
        public string EffectRichText;
        public KeywordEntry[] Keywords;
    }
    public sealed class CardEntry
    {
        public string Id;
        public string Number;
        public string Name;
        public string SchoolId;
        public string School;
        public string Source;
        public bool IsSourceCard;
        public int? RequiredPirateLevel;
        public string UnlockRequirement;
        public List<Variant> StageVariants = new List<Variant>();
    }
    public sealed class SchoolUnlockEntry
    {
        public string SchoolId;
        public string School;
        public int RequiredLevel;
    }

    private static readonly string[] Schools = { "", "fire", "arcane", "curse", "tide", "thunder", "blood" };
    private static readonly NavalBattleStage[] Stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
    private const string SourceCardUnlockRule = "船员牌及部件牌按对应来源获得，不受上述流派等级门槛限制。";
    private static readonly MethodInfo OtherAtStage = typeof(CatalogResourceCosts).GetMethod("OtherAtStage", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly string[] Rules = {
        "冲撞进入接舷战时，继承被撞方所在格子的场地：玩家冲撞敌方时取敌方所在格，敌方冲撞玩家时取玩家所在格。",
        "主动接舷时，继承双方所在格子的场地。同类型分别取当前剩余持续时间的最大值，并保留该来源的归属；最大值相同时，随机选择一个来源。",
        "继承后的场地作用于接舷战中的敌我双方；场地归属仅决定持续时间的衰减时机。",
        "火场、折射阵、禁域和血池保留完整效果。",
        "火场主动攻击增伤 25%，折射阵承受主动攻击增伤 25%，禁域承受主动攻击减伤 25%；这些比例不改变燃烧、场地伤害、咒印消灭和主动生命支付。",
        "雷栅保留连锁不会清空或消耗的效果；每次连锁实际增加至至少 50 时，受到 1000 点伤害，先扣除格挡。接舷战中不保留移动获得连锁的效果。",
        "定向水流不继承到接舷战。",
        "场地在所属方的对方回合结束时减少 1 回合：己方场地在敌方回合结束时衰减，敌方场地在己方回合结束时衰减。回合结束时，先结算已有燃烧伤害，再结算场地施加的燃烧、咒印和血池生命流失，最后减少场地持续时间。"
    };

    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Usage: ExportCardList.exe <output-directory>");
        string output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        GlossaryEntry[] glossary = ReadGlossary();
        SchoolUnlockEntry[] schoolUnlocks = CoreSchoolUnlockCatalog.All.Select(school => new SchoolUnlockEntry {
            SchoolId = school.Id, School = school.Name, RequiredLevel = school.RequiredLevel
        }).ToArray();
        var cards = new List<CardEntry>();
        var session = new CoreVoyageSession();
        int stageCount = 0, crewCount = 0, componentCount = 0;
        for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
        {
            CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
            if (!Published(original)) continue;
            CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
            Require(basis.Id == original.Id && basis.UpgradeLevel == 0, "Invalid base identity: " + original.Id);
            Require(Schools.Contains(basis.SchoolId ?? ""), "Unknown school: " + basis.Id);
            if (basis.RequiredCrew.Length > 0) crewCount++;
            if (basis.RequiredComponent.Length > 0) componentCount++;
            bool isSourceCard = basis.RequiredCrew.Length > 0 || basis.RequiredComponent.Length > 0;
            CoreSchoolUnlockDefinition schoolUnlock = CoreSchoolUnlockCatalog.Find(basis.SchoolId);
            var row = new CardEntry {
                Id = basis.Id, Number = CoreBattleCardNumbering.Label(basis), Name = basis.Name,
                SchoolId = basis.SchoolId ?? "", School = School(basis.SchoolId), Source = Source(basis),
                IsSourceCard = isSourceCard, RequiredPirateLevel = isSourceCard ? null : (int?)schoolUnlock?.RequiredLevel,
                UnlockRequirement = isSourceCard ? SourceCardUnlockRule : schoolUnlock == null ? "无流派等级门槛" :
                    "海盗等级 " + schoolUnlock.RequiredLevel + " 解锁" + schoolUnlock.Name + "基础牌"
            };
            Require(row.Number.Length > 0, "Missing card number: " + row.Id);
            foreach (NavalBattleStage stage in Stages.Where(basis.IsAllowedInStage))
            {
                stageCount++;
                CoreBattleCard resolved = stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(basis) : basis;
                // Render the same authored wording as card faces, with base values only.
                // The live session quote would add equipped firepower to cannon damage.
                string effect = CoreBattleCardText.RenderTemplate(resolved.DescriptionTemplate, resolved, stage);
                Require(!string.IsNullOrWhiteSpace(effect) && !Regex.IsMatch(effect, @"[{}]|TODO|TBD"), "Incomplete effect: " + row.Id);
                var variant = new Variant {
                    Name = resolved.Name, Energy = resolved.Energy.ToString(),
                    OtherCost = (string)OtherAtStage.Invoke(null, new object[] { resolved, stage }),
                    Effect = effect,
                    EffectRichText = CoreBattleCardKeywordText.EmphasizeKeywords(effect),
                    Keywords = CoreBattleCardKeywordText.Build(resolved, effect).Select(keyword => {
                        GlossaryEntry definition = glossary.Single(entry => entry.Meaning == keyword.Meaning && entry.Names.Contains(keyword.Keyword));
                        return new KeywordEntry { Id = definition.Id, Name = keyword.Keyword, Meaning = keyword.Meaning };
                    }).ToArray()
                };
                Require(!string.IsNullOrWhiteSpace(variant.OtherCost), "Missing cost: " + row.Id);
                Variant same = row.StageVariants.FirstOrDefault(other => other.Name == variant.Name && other.Energy == variant.Energy &&
                    other.OtherCost == variant.OtherCost && other.Effect == variant.Effect);
                if (same == null) { same = variant; row.StageVariants.Add(same); }
                same.Stages.Add(Stage(stage));
            }
            Require(row.StageVariants.Count > 0, "Card has no usable stage: " + row.Id);
            cards.Add(row);
        }
        cards = cards.OrderBy(card => Array.IndexOf(Schools, card.SchoolId)).ThenBy(card => int.Parse(card.Number)).ToList();
        Require(cards.Select(card => card.Id).Distinct().Count() == cards.Count, "Duplicate identities");
        Require(cards.Select(card => card.Number).Distinct().Count() == cards.Count, "Duplicate numbers");
        Require(CoreSpecialCardCatalog.All.All(source => cards.Any(card => card.Id == source.Card.Id)), "Missing special source cards");
        CheckRepresentativeCards(cards);
        string exportDate = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var report = new { Date = exportDate, Cards = cards, Glossary = glossary, Rules = Rules, StageCount = stageCount,
            CrewCount = crewCount, ComponentCount = componentCount,
            SchoolUnlocks = schoolUnlocks, SchoolUnlockSource = "CoreSchoolUnlockCatalog", SourceCardUnlockRule,
            Basis = "当前现役卡牌，未强化 +0，按可用阶段列出基础卡面；包含所有解锁等级和来源卡。" };
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(Path.Combine(output, "card-list-data.json"), serializer.Serialize(report), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "全部卡牌清单.md"), Markdown(cards, glossary, schoolUnlocks, exportDate), new UTF8Encoding(false));
        var validation = new StringBuilder();
        validation.AppendLine("Current card identities: " + cards.Count);
        validation.AppendLine("Playable stage definitions: " + stageCount);
        validation.AppendLine("Grouped display variants: " + cards.Sum(card => card.StageVariants.Count));
        validation.AppendLine("Glossary definitions: " + glossary.Length);
        validation.AppendLine("Boarding field inheritance rules: " + Rules.Length);
        validation.AppendLine("Crew cards: " + crewCount + "; component cards: " + componentCount);
        foreach (string school in Schools) validation.AppendLine(School(school) + ": " + cards.Count(card => card.SchoolId == school));
        validation.AppendLine("School unlocks from CoreSchoolUnlockCatalog: " + string.Join("; ", schoolUnlocks.Select(school => school.SchoolId + "=" + school.RequiredLevel)));
        Require(cards.Where(card => card.IsSourceCard).All(card => card.RequiredPirateLevel == null), "Source card incorrectly level-gated");
        validation.AppendLine("PASS: unique identities/numbers, all special cards, complete effects, glossary resolution, stage-specific costs, representative values.");
        File.WriteAllText(Path.Combine(output, "validation.txt"), validation.ToString(), new UTF8Encoding(false));
        Console.Write(validation);
        return 0;
    }

    private static GlossaryEntry[] ReadGlossary()
    {
        // Reflect the private glossary only in this export tool so the report cannot
        // drift from hover definitions, without expanding the game's public API.
        var result = new List<GlossaryEntry>();
        var definitions = (IEnumerable)typeof(CoreBattleCardKeywordText).GetField("Definitions", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (object definition in definitions)
        {
            Type type = definition.GetType();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            string pattern = ((Regex)type.GetField("Pattern", flags).GetValue(definition)).ToString();
            string names = pattern.Split(new[] { "(?=", "(?!" }, StringSplitOptions.None)[0];
            var entry = new GlossaryEntry {
                Id = (string)type.GetField("Id", flags).GetValue(definition),
                Meaning = (string)type.GetField("Meaning", flags).GetValue(definition), Names = names.Split('|')
            };
            foreach (string name in entry.Names)
            {
                var matches = CoreBattleCardKeywordText.Build(null, name + "：");
                Require(matches.Count == 1 && matches[0].Keyword == name && matches[0].Meaning == entry.Meaning,
                    "Glossary pattern needs explicit export handling: " + pattern);
            }
            result.Add(entry);
        }
        return result.ToArray();
    }

    private static void CheckRepresentativeCards(List<CardEntry> cards)
    {
        var tactical = cards.Single(card => card.Id == "card_tactical_draw");
        var boardingTactical = cards.Single(card => card.Id == "card_boarding_basic_08");
        Require(tactical.StageVariants.Any(row => row.Stages.Contains("海战") && row.Energy == "1" && row.Effect.Contains("抽 2 张牌")) &&
            tactical.StageVariants.All(row => !row.Stages.Contains("接舷")) &&
            boardingTactical.StageVariants.All(row => row.Stages.SequenceEqual(new[] { "接舷" }) && row.Energy == "2" && row.Effect.Contains("抽 3 张牌")),
            "Independent ship/boarding tactical cards mismatch");
        Require(!cards.Single(card => card.Id == "card_boarding_fire_14").StageVariants.Single().OtherCost.Contains("生命值"), "Retired fire_14 life cost returned");
        Require(cards.Single(card => card.Id == "card_boarding_blood_01").StageVariants.Single().OtherCost.Contains("生命值 6"), "Missing authored blood cost");
        Require(cards.Single(card => card.Id == "card_load_shot").StageVariants.All(row => row.Keywords.Length == 1 && row.Keywords[0].Name == "额外装药"), "Loading keyword mismatch");
        Require(cards.Single(card => card.Id == "card_chain_shot").StageVariants.All(row => row.Effect.Contains("10 点船体伤害")), "Cannon export included live equipment");
    }

    private static string Markdown(List<CardEntry> cards, GlossaryEntry[] glossary, SchoolUnlockEntry[] schoolUnlocks, string exportDate)
    {
        var text = new StringBuilder("# 全部卡牌清单\n\n" + exportDate + "｜当前现役卡牌｜未强化 +0\n\n## 字段名称与说明\n\n");
        foreach (GlossaryEntry term in glossary) text.AppendLine("- **" + string.Join("／", term.Names) + "**：" + term.Meaning + "。");
        text.AppendLine("\n## 接舷场地继承规则\n");
        foreach (string rule in Rules) text.AppendLine("- " + rule);
        text.AppendLine("\n## 清单说明\n");
        text.AppendLine("共 " + cards.Count + " 张，按普通、火焰、秘术、诅咒、潮汐、雷霆、血契分组，包含全部船员牌与部件牌。退役身份和合并别名不重复列出。同名但编号不同的卡牌分别保留。\n");
        text.AppendLine("基础流派牌按海盗等级解锁：" + string.Join("、", schoolUnlocks.Select(school => school.School + " " + school.RequiredLevel + " 级")) + "。普通常规牌无流派等级门槛。" + SourceCardUnlockRule + "\n");
        text.AppendLine("消耗与效果均为未强化（+0）的基础卡面，分阶段列出，不含装备和实战临时加成。资源费用栏 X 表示消耗全部对应资源；焚烧效果中的 X 表示选择焚烧的手牌数（1～3）。增效消耗用于额外效果，缺少耗材时按卡面使用基础效果；治疗实际支付还受缺失生命影响。\n");
        text.AppendLine("字段表示该段效果中会触发悬浮释义的关键词；— 表示无此类关键词。字段说明只解释含义，不加入计算流程。\n");
        foreach (string school in Schools)
        {
            CardEntry[] schoolCards = cards.Where(card => card.SchoolId == school).ToArray();
            text.AppendLine("\n## " + School(school) + "系（" + schoolCards.Length + " 张）\n");
            foreach (CardEntry card in schoolCards)
            {
                text.AppendLine("### " + card.Number + " " + card.Name + "｜" + card.Source + "\n");
                text.AppendLine("**解锁**：" + card.UnlockRequirement + "\n");
                foreach (Variant variant in card.StageVariants)
                {
                    text.AppendLine("**阶段：" + string.Join("／", variant.Stages) + (variant.Name == card.Name ? "" : "；阶段名称：" + variant.Name) + "**\n");
                    text.AppendLine("- **消耗**：能量 " + variant.Energy + (variant.OtherCost == "—" ? "；无其他消耗" : "；" + variant.OtherCost));
                    text.AppendLine("- **效果**：" + variant.Effect.Replace("\r", "").Replace("\n", " "));
                    text.AppendLine("- **字段**：" + (variant.Keywords.Length == 0 ? "—" : string.Join("、", variant.Keywords.Select(term => term.Name))) + "\n");
                }
                text.AppendLine();
            }
        }
        return text.ToString();
    }

    private static bool Published(CoreBattleCard card) => card.AllowedStages != NavalBattleStages.None && !CoreBattleCardAliases.IsAlias(card.Id) &&
        !(card.AllowedStages == NavalBattleStages.Boarding && card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(card));
    private static string School(string id) => string.IsNullOrEmpty(id) ? "普通" : CoreSchoolUnlockCatalog.Find(id)?.Name ?? id;
    private static string Stage(NavalBattleStage stage) => stage == NavalBattleStage.Preparation ? "准备" : stage == NavalBattleStage.Naval ? "海战" : "接舷";
    private static string Source(CoreBattleCard card)
    {
        CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(card.Id);
        if (source != null) return (source.SourceKind == CoreSpecialCardSourceKind.Crew ? "船员牌：" : "部件牌：") + source.SourceName;
        if (card.RequiredCrew.Length > 0) return "通用船员牌：" + SourceName(card.RequiredCrew);
        if (card.RequiredComponent.Length > 0) return "通用部件牌：" + SourceName(card.RequiredComponent);
        return "常规卡牌";
    }
    private static string SourceName(string source)
    {
        switch (source)
        {
            case "navigator": return "领航员"; case "carpenter": return "木匠"; case "doctor": return "医生";
            case "gunner": return "炮手"; case "cook": return "厨师"; case "diver": return "潜水员";
            case "bow": return "舰艏"; case "sail": return "风帆"; case "cannon": return "火炮";
            case "plating": case "armor": return "装甲"; case "instrument": return "仪器"; default: return source;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
