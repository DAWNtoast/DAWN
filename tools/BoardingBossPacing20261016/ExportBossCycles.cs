using System;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class ExportBossCycles
{
    private static void Main(string[] args)
    {
        using (var output = new StreamWriter(args[0], false, new UTF8Encoding(false)))
        {
            output.WriteLine("enemy,school,cycleTurn,cycleLength,energy,cards");
            foreach (var id in CoreRouteBossCatalog.All.Select(b => b.Id).Concat(CoreLegendaryWarships.All.Select(b => b.Id)))
            {
                var deck = CoreBoardingEnemyCatalog.ForEncounter(id);
                for (var index = 0; index < deck.Turns.Count; index++)
                {
                    var turn = deck.Turns[index];
                    output.WriteLine(string.Join(",", new[] { id, deck.SchoolId, (index + 1).ToString(),
                        deck.Turns.Count.ToString(), turn.TotalEnergy.ToString(), string.Join("+", turn.Cards.Select(c => c.Name)) }
                        .Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")));
                }
            }
        }
    }
}
