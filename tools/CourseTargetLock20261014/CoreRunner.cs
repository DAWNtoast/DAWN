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

internal static class CourseTargetLockCoreRunner
{
    private static string Output;
    private static readonly TextWriter RealConsole = Console.Out;
    private static int Main(string[] args)
    {
        Output = args[0];
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "test-starts.log"), string.Empty);
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object> { { "NumberOfTestWorkers", 0 }, { "WorkDirectory", Output } });
        Console.WriteLine("Discovered " + runner.CountTestCases(TestFilter.Empty) + " NUnit cases.");
        var result = runner.Run(new Listener(), TestFilter.Empty);
        File.WriteAllText(Path.Combine(Output, "results.xml"), result.ToXml(true).OuterXml, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(Output, "failures.txt"), Descendants(result)
            .Where(r => !r.Test.IsSuite && r.ResultState.Status == TestStatus.Failed)
            .Select(r => r.FullName + "\n" + r.Message + "\n" + r.StackTrace));
        Console.WriteLine("NUnit: passed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount);
        return result.FailCount == 0 && result.PassCount > 0 ? 0 : 1;
    }
    private static IEnumerable<ITestResult> Descendants(ITestResult result)
    {
        yield return result;
        foreach (var child in result.Children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private sealed class Listener : ITestListener
    {
        public void TestStarted(ITest test)
        {
            if (!test.IsSuite) File.AppendAllText(Path.Combine(Output, "test-starts.log"), test.FullName + Environment.NewLine);
        }
        public void TestFinished(ITestResult result)
        {
            if (!result.Test.IsSuite && result.ResultState.Status == TestStatus.Failed)
                RealConsole.WriteLine("FAIL " + result.FullName + "\n" + result.Message);
        }
        public void TestOutput(TestOutput output) { RealConsole.Write(output.Text); }
    }
}
