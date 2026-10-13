using System;
using System.IO;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class RuntimeCardArtSnapshot
{
    private static int Main(string[] args)
    {
        var output = new StringBuilder("[");
        for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
        {
            CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
            CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(card.Id);
            if (index > 0) output.Append(',');
            output.Append("{\"index\":").Append(index)
                .Append(",\"id\":").Append(Json(card.Id))
                .Append(",\"artId\":").Append(Json(card.ArtId))
                .Append(",\"schoolId\":").Append(Json(card.SchoolId))
                .Append(",\"allowedStages\":").Append((int)card.AllowedStages)
                .Append(",\"requiredCrew\":").Append(Json(card.RequiredCrew))
                .Append(",\"requiredComponent\":").Append(Json(card.RequiredComponent))
                .Append(",\"sourceId\":").Append(Json(source == null ? "" : source.SourceId))
                .Append(",\"sourceKind\":").Append(Json(source == null ? "" : source.SourceKind.ToString()))
                .Append(",\"sourceName\":").Append(Json(source == null ? "" : source.SourceName)).Append('}');
        }
        output.Append(']');
        File.WriteAllText(args[0], output.ToString(), new UTF8Encoding(false));
        Console.WriteLine("Runtime card art identities: " + CoreVoyageSession.CardLibrary.Count);
        return 0;
    }

    private static string Json(string text)
    {
        if (text == null) return "null";
        return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    }
}
