using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
public static class Runner {
 public static int Main(string[] args){var runner=new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());runner.Load(Assembly.GetExecutingAssembly(),new Dictionary<string,object>{{"NumberOfTestWorkers",0}});var result=runner.Run(TestListener.NULL,TestFilter.Empty);File.WriteAllText(Path.Combine(args[0],"results.xml"),result.ToXml(true).OuterXml);Console.WriteLine("Passed="+result.PassCount+" Failed="+result.FailCount+" Skipped="+result.SkipCount+" Inconclusive="+result.InconclusiveCount);Failures(result);return result.FailCount==0?0:1;}
 static void Failures(ITestResult r){if(r.HasChildren){foreach(var child in r.Children)Failures(child);}else if(r.ResultState.Status==TestStatus.Failed)Console.WriteLine(r.FullName+"\n"+r.Message+"\n"+r.StackTrace);}
}
