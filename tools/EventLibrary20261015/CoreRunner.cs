using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

internal static class EventLibraryCoreRunner
{
    static readonly TextWriter RealConsole = Console.Out;
    static string output;
    static int Main(string[] args)
    {
        output = args[0]; Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "test-starts.log"), string.Empty);
        File.WriteAllText(Path.Combine(output, "scope.txt"), "Current pure Core and complete selected NUnit fixtures, without stripping tests. Includes current/legacy chart geometry, full event definitions, low-resource exits, choice execution, checkpoint round trips, voyage scheduling and preservation of old random-bag versions. Checkpoints are DTO round trips; Unity JSON serialization and UI are verified separately. No Unity process is started.\n");
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object> { { "NumberOfTestWorkers", 0 }, { "WorkDirectory", output } });
        RealConsole.WriteLine("Discovered " + runner.CountTestCases(TestFilter.Empty) + " NUnit cases.");
        var result = runner.Run(new Listener(), TestFilter.Empty);
        File.WriteAllText(Path.Combine(output, "results.xml"), result.ToXml(true).OuterXml, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(output, "failures.txt"), Descendants(result).Where(r => !r.Test.IsSuite && r.ResultState.Status == TestStatus.Failed)
            .Select(r => r.FullName + "\n" + r.Message + "\n" + r.StackTrace));
        RealConsole.WriteLine("NUnit: passed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount + " inconclusive=" + result.InconclusiveCount);
        return result.FailCount == 0 && result.PassCount > 0 ? 0 : 1;
    }
    static IEnumerable<ITestResult> Descendants(ITestResult result)
    {
        yield return result;
        foreach (var child in result.Children) foreach (var nested in Descendants(child)) yield return nested;
    }
    sealed class Listener : ITestListener
    {
        public void TestStarted(ITest test) { if (!test.IsSuite) File.AppendAllText(Path.Combine(output, "test-starts.log"), test.FullName + Environment.NewLine); }
        public void TestFinished(ITestResult result) { if (!result.Test.IsSuite && result.ResultState.Status == TestStatus.Failed) RealConsole.WriteLine("FAIL " + result.FullName + "\n" + result.Message); }
        public void TestOutput(TestOutput value) { RealConsole.Write(value.Text); }
    }
}
