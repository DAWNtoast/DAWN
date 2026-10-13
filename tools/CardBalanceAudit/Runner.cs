using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using ShatteredSail.Core.Gameplay;

internal static class CardBalanceCoreTestRunner
{
    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(output);
        ExportCards(Path.Combine(output, "cards.tsv"));
        if (args.Contains("--export-only")) return 0;
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object>
        {
            { "NumberOfTestWorkers", 0 }, { "WorkDirectory", output }
        });
        Console.WriteLine("Discovered " + runner.CountTestCases(TestFilter.Empty) + " NUnit cases.");
        ITestResult result = runner.Run(new FailureListener(), TestFilter.Empty);
        File.WriteAllText(Path.Combine(output, "results.xml"), result.ToXml(true).OuterXml, new UTF8Encoding(false));
        using (var failures = new StreamWriter(Path.Combine(output, "failures.tsv"), false, new UTF8Encoding(false)))
        {
            failures.WriteLine("Test\tMessage\tStackTrace");
            foreach (ITestResult failure in Descendants(result).Where(value => !value.Test.IsSuite && value.ResultState.Status == TestStatus.Failed))
            {
                failures.WriteLine(Field(failure.FullName) + "\t" + Field(failure.Message) + "\t" + Field(failure.StackTrace));
                Console.WriteLine("FAIL " + failure.FullName + Environment.NewLine + failure.Message);
            }
        }
        Console.WriteLine("NUnit: passed=" + result.PassCount + " failed=" + result.FailCount +
            " skipped=" + result.SkipCount + " inconclusive=" + result.InconclusiveCount);
        return result.FailCount == 0 && result.PassCount > 0 ? 0 : 1;
    }

    private static IEnumerable<ITestResult> Descendants(ITestResult result)
    {
        yield return result;
        foreach (ITestResult child in result.Children)
            foreach (ITestResult descendant in Descendants(child)) yield return descendant;
    }

    private static void ExportCards(string path)
    {
        string[] names = { "Id", "ArtId", "Name", "Type", "SchoolId", "AllowedStages", "Action", "Energy", "Damage", "Block", "Repair", "Draw", "PowderCost", "CannonballCost", "MovementMeters", "StrategyAmount", "StrategyLimit", "BurnDamage", "BurnDuration", "UpgradeLevel", "RequiredCrew", "RequiredComponent", "Description", "DescriptionTemplate" };
        string[] effectNames = { "Mechanic", "Power", "Secondary", "Threshold", "Duration", "Limit", "Movement", "Upgrades", "FallbackDamage", "FallbackBlock", "WoodCost" };
        var session = new CoreVoyageSession();
        int rows = 0;
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            writer.WriteLine(string.Join("\t", new[] { "LibraryIndex" }.Concat(names).Concat(effectNames.Select(name => "SchoolEffect." + name))));
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                for (int rank = 0; rank <= 3; rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    var fields = new List<string> { index.ToString(CultureInfo.InvariantCulture) };
                    fields.AddRange(names.Select(name => Field(typeof(CoreBattleCard).GetProperty(name).GetValue(card, null))));
                    fields.AddRange(effectNames.Select(name => card.SchoolEffect == null ? "" : Field(typeof(CoreSchoolCardEffect).GetProperty(name).GetValue(card.SchoolEffect, null))));
                    writer.WriteLine(string.Join("\t", fields));
                    rows++;
                }
            }
        }
        Console.WriteLine("Exported " + rows + " published card/rank rows to " + path);
    }

    private static string Field(object value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private sealed class FailureListener : ITestListener
    {
        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result) { }
        public void TestOutput(TestOutput output) { }
    }
}
