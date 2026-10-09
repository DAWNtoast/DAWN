using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Tests;

internal static class Program
{
    private static int Main()
    {
        var fixture = new CoreGuidedTutorialVoyageTests();
        int failures = 0, passed = 0;
        foreach (MethodInfo method in fixture.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            TestCaseAttribute[] cases = method.GetCustomAttributes(typeof(TestCaseAttribute), false).Cast<TestCaseAttribute>().ToArray();
            if (!Attribute.IsDefined(method, typeof(TestAttribute)) && cases.Length == 0) continue;
            IEnumerable<object[]> inputs = cases.Length > 0 ? cases.Select(item => item.Arguments) : new[] { new object[0] };
            foreach (object[] values in inputs)
            {
                string name = method.Name + "(" + string.Join(",", values) + ")";
                try { method.Invoke(fixture, values); passed++; Console.WriteLine("PASS " + name); }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine("FAIL " + name + ": " + (exception.InnerException ?? exception));
                }
            }
        }
        Console.WriteLine("Guided tutorial Core audit: " + passed + " passed, " + failures + " failed.");
        return failures == 0 ? 0 : 1;
    }
}
