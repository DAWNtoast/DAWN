using System;
using System.Reflection;

internal static class CaptureLegacySnapshot
{
    static int Main(string[] args)
    {
        var assembly = Assembly.LoadFrom(args[0]);
        var type = assembly.GetType("ShatteredSail.Core.Tests.VoyageEventDistributionTests", true);
        var generate = type.GetMethod("Generate", BindingFlags.NonPublic | BindingFlags.Static);
        var signature = type.GetMethod("Signature", BindingFlags.NonPublic | BindingFlags.Static);
        Console.WriteLine("version,route,sample,sha256");
        for (int route = 0; route < 3; route++) for (int sample = 0; sample < 3; sample++)
        {
            var map = generate.Invoke(null, new object[] { 2, route, sample, null });
            Console.WriteLine("2," + route + "," + sample + "," + signature.Invoke(null, new[] { map }));
        }
        return 0;
    }
}
