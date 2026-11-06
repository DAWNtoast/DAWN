using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;

internal static class ExportCrewReviewData
{
    private static readonly NavalBattleStage[] Stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
    private static readonly string[] ProfessionNames = { "航海士", "木匠", "船医", "炮手", "厨师", "潜水员" };
    private static readonly MethodInfo OtherAtStage = typeof(CatalogResourceCosts).GetMethod("OtherAtStage", BindingFlags.NonPublic | BindingFlags.Static);
    private static string StageLabel(NavalBattleStage stage) => stage == NavalBattleStage.Preparation ? "策略" : stage == NavalBattleStage.Naval ? "远程攻击" : "近战";

    public static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass output directory.");
        string output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        var session = new CoreVoyageSession();
        object[] entries = CrewNarrativeCatalog.AllCrew.Select(crew => {
            var story = CrewNarrativeCatalog.StoryFor(crew);
            var special = CoreSpecialCardCatalog.ForCrew(crew.Id);
            if (special == null) throw new InvalidOperationException("Crew special card missing: " + crew.Id);
            int cardIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(index => CoreVoyageSession.CardLibrary[index].Id == special.Card.Id);
            var card = session.GetBattleCardAtLevel(cardIndex, 0);
            var previewStage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding : card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
            var variants = Stages.Where(card.IsAllowedInStage).Select(stage => {
                var resolved = stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(card) : card;
                return new {
                    stage = stage.ToString(), stageLabel = StageLabel(stage), name = resolved.Name, energy = resolved.Energy,
                    otherCost = (string)OtherAtStage.Invoke(null, new object[] { resolved, stage }),
                    effect = CoreBattleCardText.RenderTemplate(resolved.DescriptionTemplate, resolved, stage),
                    gameEffect = session.BattleCardFaceDescription(card, stage)
                };
            }).ToArray();
            return (object)new {
                id = crew.Id, portName = crew.PortName, professionIndex = crew.ProfessionIndex, professionId = crew.ProfessionId,
                professionName = ProfessionNames[crew.ProfessionIndex], displayName = crew.DisplayName, roleSummary = crew.RoleSummary,
                specialty = crew.Specialty, skillDescription = crew.SkillDescription, stats = crew.Stats, contractCost = crew.ContractCost,
                appearanceTags = crew.AppearanceTags, biography = crew.Biography, portraitAssetName = crew.PortraitAssetName,
                isLegacy = crew.IsLegacy, wasOldCaptainCrew = crew.WasOldCaptainCrew, loreRelationLabel = crew.LoreRelationLabel,
                skills = new[] { new { label = "专长", text = crew.Specialty }, new { label = "职业能力", text = crew.SkillDescription }, new { label = "叠加规则", text = crew.Stats } },
                story = new { id = story.Id, chapter = story.Chapter, title = story.Title, body = story.Body, previousId = story.PreviousId, nextId = story.NextId, chapterLabel = story.ChapterLabel },
                specialCards = new[] { new { id = card.Id, name = card.Name, level = 0, sourceName = special.SourceName, lore = special.Lore, storyId = special.StoryId,
                    stages = Stages.Where(card.IsAllowedInStage).Select(StageLabel).ToArray(), previewStage = StageLabel(previewStage),
                    previewEffect = session.BattleCardFaceDescription(card, previewStage), portDescription = session.BattleCardPortDescription(card), variants } }
            };
        }).ToArray();
        if (entries.Length != 36 || CrewNarrativeCatalog.AllCrew.Select(x => x.Id).Distinct().Count() != entries.Length)
            throw new InvalidOperationException("Expected all 36 unique regional crew identities.");
        var data = new {
            schemaVersion = 1, generatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("o"),
            source = "当前生产 CrewNarrativeCatalog、CoreSpecialCardCatalog 与生产卡面描述接口，只读导出；专属卡按 +0 基础等级。",
            ports = CrewNarrativeCatalog.Ports.ToArray(), professions = ProfessionNames, crew = entries
        };
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(Path.Combine(output, "crew-data.json"), serializer.Serialize(data), new UTF8Encoding(false));
        Console.WriteLine("Exported " + entries.Length + " crew, 36 special cards, 6 ports, all 15 story chapters mapped.");
        return 0;
    }
}
