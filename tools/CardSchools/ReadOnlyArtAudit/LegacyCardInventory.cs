using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Content;

internal static class LegacyCardInventory
{
    private static int Main(string[] args)
    {
        var cards = new List<object>();
        var session = new CoreVoyageSession();
        int retired = 0, school = 0, newNeutral = 0;
        for (int i = 0; i < CoreVoyageSession.CardLibrary.Count; i++)
        {
            var c = CoreVoyageSession.CardLibrary[i];
            if (c.AllowedStages == NavalBattleStages.None) { retired++; continue; }
            if (!string.IsNullOrEmpty(c.SchoolId)) { school++; continue; }
            if (c.ArtId.StartsWith("card_school_neutral_", StringComparison.Ordinal)) { newNeutral++; continue; }
            var source = CoreSpecialCardCatalog.FindByCardId(c.Id);
            var crew = source != null && source.SourceKind == CoreSpecialCardSourceKind.Crew
                ? CrewNarrativeCatalog.FindById(source.SourceId) : null;
            var stages = new List<string>();
            var display = new Dictionary<string, string>();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                if (c.IsAllowedInStage(stage))
                {
                    stages.Add(stage.ToString());
                    display[stage.ToString()] = session.BattleCardEffectDescription(c, stage);
                }
            cards.Add(new {
                index = i, cardId = c.Id, name = c.Name, cardName = c.Name, type = c.Type,
                oldArtId = c.ArtId, artId = c.ArtId, schoolId = c.SchoolId,
                description = c.Description, effect = c.Description, descriptionTemplate = c.DescriptionTemplate,
                displayDescriptionByStage = display, portDisplayDescription = session.BattleCardPortDescription(c),
                allowedStages = (int)c.AllowedStages, phase = stages, energy = c.Energy,
                powderCost = c.PowderCost, cannonballCost = c.CannonballCost,
                damage = c.Damage, block = c.Block, repair = c.Repair, draw = c.Draw,
                action = c.Action.ToString(), requiredCrew = c.RequiredCrew, requiredComponent = c.RequiredComponent,
                sourceId = source != null ? source.SourceId : c.RequiredCrew.Length > 0 ? c.RequiredCrew : c.RequiredComponent,
                sourceKind = source != null ? source.SourceKind.ToString().ToLowerInvariant() : "basic",
                sourceCategory = source != null ? "identitySource" : c.RequiredCrew.Length > 0 ? "genericProfessionCompatibility" : c.RequiredComponent.Length > 0 ? "genericComponentCompatibility" : "sourceFreeBase",
                sourceName = source != null ? source.SourceName : "",
                sourceLore = source != null ? source.Lore : "", storyId = source != null ? source.StoryId : "",
                crewIdentity = crew == null ? null : new {
                    id = crew.Id, displayName = crew.DisplayName, port = crew.PortName,
                    professionIndex = crew.ProfessionIndex, professionId = crew.ProfessionId,
                    roleSummary = crew.RoleSummary, specialty = crew.Specialty, appearanceTags = crew.AppearanceTags,
                    biography = crew.Biography, portraitAssetName = crew.PortraitAssetName
                }
            });
        }
        var data = new {
            schema = "shattered-sail-legacy-card-art-inventory-v1", generatedUtc = DateTime.UtcNow.ToString("o"),
            dataBasis = "Fresh compilation of current production Core + CrewNarrativeCatalog; no Unity or PlayerPrefs.",
            counts = new { librarySlots = CoreVoyageSession.CardLibrary.Count, retiredSlots = retired,
                activeCards = CoreVoyageSession.CardLibrary.Count - retired, excludedSchoolCards = school,
                excludedNewNeutralCards = newNeutral, legacyCards = cards.Count },
            displayPreviewContext = new { ordinaryCrew = session.SelectedOrdinaryCrew, medicine = session.Cargo.Medicine,
                damageBasis = "authored card values", upgradeLevel = 0, battleActive = false },
            cards = cards
        };
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(args[0], serializer.Serialize(data), new UTF8Encoding(false));
        Console.WriteLine("Legacy inventory: " + cards.Count + "; slots: " + CoreVoyageSession.CardLibrary.Count);
        return cards.Count == 80 && school == 132 && newNeutral == 4 && retired == 3 ? 0 : 1;
    }
}
