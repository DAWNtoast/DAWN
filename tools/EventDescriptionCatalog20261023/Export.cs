using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;

internal static class ExportEventDescriptions
{
    private sealed class Entry
    {
        public string Section, Context, Id, Name, Category, Description, Source;
    }

    private static readonly List<Entry> current = new List<Entry>();
    private static readonly List<Entry> legacy = new List<Entry>();
    private static readonly List<Entry> originals = new List<Entry>();
    private static readonly string[] oldChartIds =
        { "random_tailwind", "random_floating_supplies", "random_food_spoilage" };

    private static object Invoke(CoreVoyageSession session, string name, params object[] args)
    {
        var method = typeof(CoreVoyageSession).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) throw new InvalidOperationException("Missing factory: " + name);
        return method.Invoke(session, args);
    }

    private static void Add(List<Entry> target, string section, string context, CoreEncounter encounter, string source)
    {
        if (encounter == null || string.IsNullOrWhiteSpace(encounter.Id) || string.IsNullOrWhiteSpace(encounter.Description))
            throw new InvalidOperationException("An event has no ID or description: " + context);
        target.Add(new Entry { Section = section, Context = context, Id = encounter.Id, Name = encounter.Name,
            Category = encounter.Category, Description = encounter.Description, Source = source });
    }

    private static void AddExploration(string section, string context, CoreEncounter raw, string routeId, string source)
    {
        var enriched = CoreVoyageEventCatalog.EnrichExploration(raw, routeId);
        Add(current, section, context, enriched, source + "；CoreVoyageExplorationCatalog.cs");
        if (raw.Description != enriched.Description)
            Add(originals, "探索处理前的原始定义", context, raw, source);
    }

    private static string Key(Entry e) => string.Join("\u001f", e.Id, e.Name, e.Category, e.Description);
    private static string Cell(string value) => (value ?? "").Replace("|", "\\|").Replace("\r\n", "<br>").Replace("\n", "<br>");
    private static void Quote(StringBuilder doc, string value)
    {
        foreach (string line in value.Replace("\r\n", "\n").Split('\n')) doc.AppendLine("> " + line);
        doc.AppendLine();
    }

    private static void WriteEntries(StringBuilder doc, IEnumerable<Entry> entries)
    {
        foreach (var section in entries.GroupBy(e => e.Section))
        {
            doc.AppendLine("### " + section.Key).AppendLine();
            int number = 0;
            foreach (var group in section.GroupBy(Key))
            {
                Entry e = group.First();
                doc.AppendLine("#### " + (++number) + ". " + e.Name).AppendLine();
                doc.AppendLine("- 事件 ID：`" + e.Id + "`");
                doc.AppendLine("- 类型：" + e.Category);
                doc.AppendLine("- 适用范围：" + string.Join("；", group.Select(x => x.Context).Distinct()));
                doc.AppendLine("- 定义来源：" + string.Join("；", group.Select(x => x.Source).Distinct())).AppendLine();
                Quote(doc, e.Description);
            }
        }
    }

    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        var session = new CoreVoyageSession();
        session.StartVoyage();
        foreach (var route in CoreVoyageSession.Routes)
        {
            string context = route.Name + "（" + route.Id + "）";
            var basePool = (IReadOnlyList<CoreEncounter>)Invoke(session, "CreateEncounterPool", route);
            foreach (var e in basePool)
                AddExploration(route.Name + " · 基础事件", context, e, route.Id, "CoreVoyageSession.cs");
            foreach (var e in CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route.Id))
                AddExploration(route.Name + " · 扩展固定事件", context, e, route.Id, "CoreVoyageEventCatalog.cs / CoreVoyageEventExpansion.cs");
            foreach (string id in CoreVoyageEventCatalog.RandomEventIds(route.Id))
                Add(current, route.Name + " · 随机事件", context,
                    CoreVoyageEventCatalog.CreateRandomEncounter(id, route.RecommendedPower),
                    "CoreVoyageEventCatalog.cs / CoreVoyageEventExpansion.cs");
            Add(legacy, "旧版区域首领占位定义", context,
                (CoreEncounter)Invoke(session, "CreateBossEncounter", route), "CoreVoyageSession.cs");
        }

        foreach (var boss in CoreRouteBossCatalog.All)
        {
            var route = CoreVoyageSession.Routes.First(r => r.Id == boss.RouteId);
            Add(current, "区域 Boss（六系）", route.Name + "；" + CoreWarshipDesignCatalog.SchoolName(boss.SchoolId),
                boss.CreateEncounter(), "CoreRouteBossCatalog.cs");
        }
        foreach (var ship in CoreLegendaryWarships.All)
            Add(current, "传奇战舰（六系）", CoreWarshipDesignCatalog.SchoolName(ship.SchoolId),
                ship.CreateEncounter(), "CoreLegendaryWarships.cs");
        Add(current, "沉月王庭守藏舰", "月潮航域中的藏宝图首领", CoreLegendaryWarships.CreateTreasureEncounter(), "CoreLegendaryWarships.cs");

        foreach (var rank in CoreTreasureHuntCatalog.Ranks)
        {
            string context = rank.Name + "藏宝图（" + rank.Id + "）";
            foreach (var e in CoreTreasureExpeditionCatalog.CreateEncounters(rank))
                Add(current, context + " · 探险事件", context, e, "CoreTreasureExpeditionCatalog.cs");
            foreach (var e in CoreTreasureHuntCatalog.CreateEncounters(rank))
                Add(legacy, "旧版宝藏事件（ContentVersion = 0）", context, e, "CoreTreasureHuntCatalog.cs");
        }

        const string tutorial = "现役引导教程";
        Add(current, tutorial, "首次事件", CoreGuidedTutorialContent.FirstEvent(), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "可选事件", CoreGuidedTutorialContent.OptionalEvent(), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "船员条件事件", CoreGuidedTutorialContent.CrewRequirementEvent(), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "航海士事件", CoreGuidedTutorialContent.NavigatorEvent(), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "随机事件：顺风分支（luckyWind = true）", CoreGuidedTutorialContent.RandomEvent(true), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "随机事件：另一分支（luckyWind = false）", CoreGuidedTutorialContent.RandomEvent(false), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "伏击事件", CoreGuidedTutorialContent.AmbushEvent(), "CoreGuidedTutorialContent.cs");
        Add(current, tutorial, "教程首领", CoreGuidedTutorialContent.BossEvent(), "CoreGuidedTutorialContent.cs");

        foreach (string fieldName in new[] { "firstLegEncounters", "advancedLegEncounters" })
        {
            var field = typeof(TutorialVoyageSession).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            foreach (TutorialEncounter e in (IReadOnlyList<TutorialEncounter>)field.GetValue(null))
                legacy.Add(new Entry { Section = "旧版教程", Context = fieldName == "firstLegEncounters" ? "首航阶段" : "进阶阶段",
                    Id = e.Id, Name = e.Name, Category = e.Category, Description = e.Description, Source = "TutorialVoyageSession.cs" });
        }
        foreach (string id in oldChartIds)
        {
            bool isCurrent = id != "random_food_spoilage";
            Add(isCurrent ? current : legacy,
                isCurrent ? "通用航行随机事件" : "旧版海图随机事件（chartRandomVersion = 0）",
                isCurrent ? "全部三条航域的现役随机池；另兼容旧海图" : "旧海图随机事件池",
                (CoreEncounter)Invoke(session, "CreateSeaChartRandomEncounter", id), "CoreSeaChartVoyage.cs / CoreSeaChartEventPacing.cs");
        }
        foreach (bool detected in new[] { false, true })
            Add(current, "通用航路伏击", "藏宝航行／无活动海图航行；另兼容旧海图随机池；" + (detected ? "提前发现伏击" : "未能提前发现伏击"),
                (CoreEncounter)Invoke(session, "CreateRandomAmbush", detected), "CoreVoyageSession.cs / CoreSeaChartVoyage.cs");

        var doc = new StringBuilder();
        doc.AppendLine("# 事件目录与文本描述").AppendLine();
        doc.AppendLine("整理日期：2026-10-23。来源：当前项目中的实际事件工厂及剧情目录。").AppendLine();
        doc.AppendLine("本文收录事件名称、ID、类型、适用范围和描述原文。探索事件使用运行时补充后的最终描述；同一 ID 的航域、宝藏等级或教程分支文本分别保留。选项、收益、数值条件和战斗结算文案不在本清单范围内。").AppendLine();
        doc.AppendLine("正文为现役事件；旧版教程、旧存档兼容事件及探索处理前的原始定义另列附录。训练木桩是战斗测试对象，不计入航海事件。以下为全事件池目录，一张具体海图只会从中抽取部分事件。").AppendLine();
        doc.AppendLine("## 数量与阅读说明").AppendLine();
        doc.AppendLine("- 现役事件：**" + current.Select(e => e.Id).Distinct().Count() + " 个事件 ID**，**" + current.Select(Key).Distinct().Count() + " 种名称／类型／描述组合**。");
        doc.AppendLine("- 按航域、档位与触发分支列示：**" + current.Count + " 条目录记录**；相同公共事件可能出现在多个航域，另有两条通用随机事件集中列示。");
        doc.AppendLine("- 旧版兼容定义：**" + legacy.Select(e => e.Id).Distinct().Count() + " 个事件 ID**，**" + legacy.Select(Key).Distinct().Count() + " 种文本组合**；相同文本跨档位合并列示。");
        doc.AppendLine("- 另附 **3 个宝藏章节的叙述定义（6 段）**、**6 艘传奇战舰的实际战前文本（12 页）**。不计为新事件 ID；宝藏漫画实际字幕另由 TreasureComicDialogueCatalog 按内容版本提供，章节叙述定义不等同于屏幕字幕。").AppendLine();
        doc.AppendLine("| 分类 | 适用记录数 | 本分类事件 ID 数 |");
        doc.AppendLine("| --- | ---: | ---: |");
        foreach (var section in current.GroupBy(e => e.Section))
            doc.AppendLine("| " + Cell(section.Key) + " | " + section.Count() + " | " + section.Select(e => e.Id).Distinct().Count() + " |");
        doc.AppendLine().AppendLine("分类 ID 数不可直接相加作为全局去重数量。每条航域现役随机池包含本域 22 条及通用 2 条，共 24 条。").AppendLine();

        doc.AppendLine("## 一、现役事件描述").AppendLine();
        WriteEntries(doc, current);
        doc.AppendLine("## 二、宝藏航域概述与章节叙述定义").AppendLine();
        doc.AppendLine("以下章节文字来自 CoreTreasureStoryCatalog.Chapters.Pages，供叙事设定核对。实际漫画叠加字幕使用 TreasureComicDialogueCatalog.GetPage(rankId, page, contentVersion)，存在新旧版本差异，不应把本节文字当作当前屏幕上的逐字字幕。").AppendLine();
        foreach (var rank in CoreTreasureHuntCatalog.Ranks)
        {
            doc.AppendLine("### " + rank.Name + "：" + CoreTreasureExpeditionCatalog.ThemeName(rank.Id)).AppendLine();
            doc.AppendLine("档位 ID：`" + rank.Id + "`。航域概述：").AppendLine();
            Quote(doc, CoreTreasureExpeditionCatalog.Overview(rank.Id));
            var chapter = CoreTreasureStoryCatalog.FindChapter(rank.Id);
            if (chapter == null) throw new InvalidOperationException("Missing chapter: " + rank.Id);
            doc.AppendLine("章节标题：**" + chapter.Title + "**。").AppendLine();
            doc.AppendLine("关联事件：" + string.Join("、", chapter.LegendaryIds.Select(id => "`" + id + "`")) + "。").AppendLine();
            for (int i = 0; i < chapter.Pages.Count; i++)
            {
                doc.AppendLine("**第 " + (i + 1) + " 页**").AppendLine();
                Quote(doc, chapter.Pages[i]);
            }
        }
        doc.AppendLine("## 三、传奇战舰战前文本").AppendLine();
        foreach (var story in CoreTreasureStoryCatalog.Battles)
        {
            doc.AppendLine("### " + story.ShipName).AppendLine();
            doc.AppendLine("对应事件：`" + story.EncounterId + "`。").AppendLine();
            for (int i = 0; i < story.Pages.Count; i++)
            {
                doc.AppendLine("**第 " + (i + 1) + " 页**").AppendLine();
                Quote(doc, story.Pages[i]);
            }
        }
        doc.AppendLine("## 附录 A：旧版及兼容事件描述").AppendLine();
        doc.AppendLine("以下为保留在代码中的旧版定义。同 ID 的现役描述以正文为准；它们不代表新航行一定会显示的内容。").AppendLine();
        WriteEntries(doc, legacy);
        doc.AppendLine("## 附录 B：探索处理前的原始定义").AppendLine();
        doc.AppendLine("以下事件的原始 Description 会被 EnrichExploration 补充或替换。这里只作来源核对，游戏地图中的最终显示文本见正文。其余未改变的原始描述不重复列出。").AppendLine();
        WriteEntries(doc, originals);
        doc.AppendLine("## 源文件与更新方式").AppendLine();
        foreach (string name in new[] { "CoreVoyageSession.cs", "CoreVoyageEventCatalog.cs", "CoreVoyageEventExpansion.cs",
            "CoreVoyageExplorationCatalog.cs", "CoreSeaChartVoyage.cs", "CoreSeaChartEventPacing.cs", "CoreRouteBossCatalog.cs", "CoreLegendaryWarships.cs",
            "CoreTreasureExpeditionCatalog.cs", "CoreTreasureHuntCatalog.cs", "CoreTreasureHunt.cs", "CoreGuidedTutorialContent.cs", "TutorialVoyageSession.cs" })
            doc.AppendLine("- [" + name + "](" + Path.Combine(root, "UnityClient/Assets/_Project/Core/Runtime", name).Replace('\\', '/') + ")");
        doc.AppendLine("- [CoreTreasureStoryCatalog.cs](" + Path.Combine(root, "UnityClient/Assets/_Project/Presentation/Runtime/CoreTreasureStoryCatalog.cs").Replace('\\', '/') + ")");
        doc.AppendLine("- [重新导出脚本](" + Path.Combine(root, "tools/EventDescriptionCatalog20261023/Export.ps1").Replace('\\', '/') + ")").AppendLine();
        doc.AppendLine("导出脚本重新编译当前 Core 源码并调用实际事件工厂，不依赖 Unity 旧 DLL，也不以单张随机海图作为全量清单。源码散列和编译日志保存在 Artifacts/EventDescriptionCatalog20261023。");
        string output = Path.Combine(root, "docs/事件目录与文本描述_2026-10-23.md");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, doc.ToString(), new UTF8Encoding(false));
        Console.WriteLine("Document: " + output);
        Console.WriteLine("Current: " + current.Count + " occurrences; " + current.Select(e => e.Id).Distinct().Count() + " IDs; " + current.Select(Key).Distinct().Count() + " text variants.");
        Console.WriteLine("Legacy: " + legacy.Count + " occurrences; " + legacy.Select(e => e.Id).Distinct().Count() + " IDs; " + legacy.Select(Key).Distinct().Count() + " text variants.");
        Console.WriteLine("Original descriptions replaced by exploration: " + originals.Count + ".");
        return 0;
    }
}
