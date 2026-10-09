using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "Artifacts/ReleaseAudit/Economy";
        Directory.CreateDirectory(output);
        int passed = 0, failed = 0;
        var lines = new List<string> { "# 非战斗经济离线回归", "", "执行 UTC：" + DateTime.UtcNow.ToString("u"), "",
            "仅编译纯 Core 与 NUnit 测试代码，用 Mono 执行；不启动 Unity，不读取或写入 PlayerPrefs。此报告不替代 Unity EditMode/PlayMode 及画面验证。", "" };
        foreach (Type fixture in new[] { typeof(CoreEconomySafetyTests), typeof(CoreVoyageSessionTests),
            typeof(CoreVoyageSafetyAuditTests), typeof(EncounterDiscoveryRulesTests) })
        foreach (MethodInfo method in fixture.GetMethods().OrderBy(item => item.Name))
        {
            TestCaseAttribute[] cases = method.GetCustomAttributes(typeof(TestCaseAttribute), false).Cast<TestCaseAttribute>().ToArray();
            bool isTest = method.IsDefined(typeof(TestAttribute), false);
            if (!isTest && cases.Length == 0) continue;
            IEnumerable<object[]> arguments = cases.Length > 0 ? cases.Select(item => item.Arguments) : new[] { new object[0] };
            foreach (object[] values in arguments)
            {
                string name = fixture.Name + "." + method.Name + "(" + string.Join(", ", values) + ")";
                try
                {
                    method.Invoke(Activator.CreateInstance(fixture), values);
                    passed++;
                    lines.Add("- PASS " + name);
                }
                catch (Exception error)
                {
                    failed++;
                    Exception actual = error is TargetInvocationException ? error.InnerException : error;
                    lines.Add("- FAIL " + name + ": " + actual.Message.Replace("\r", " ").Replace("\n", " "));
                    Console.WriteLine("FAIL " + name + ": " + actual);
                }
            }
        }
        lines.Insert(6, "通过 " + passed + "，失败 " + failed + "。\n");
        File.WriteAllLines(Path.Combine(output, "Regression.md"), lines, new UTF8Encoding(false));
        Console.WriteLine("Economy regression: " + passed + " passed, " + failed + " failed. Report: " + Path.GetFullPath(output));
        return failed == 0 ? 0 : 1;
    }
}
