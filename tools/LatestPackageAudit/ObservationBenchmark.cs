extern alias baseline;
extern alias optimized;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BeforeSession = baseline::ShatteredSail.Core.Gameplay.CoreVoyageSession;
using AfterSession = optimized::ShatteredSail.Core.Gameplay.CoreVoyageSession;
using BeforePoint = baseline::ShatteredSail.Core.Gameplay.VoyagePoint;
using AfterPoint = optimized::ShatteredSail.Core.Gameplay.VoyagePoint;

internal static class ObservationBenchmark
{
    private const int Iterations = 1000;
    private static int Main()
    {
        var before = new BeforeSession();
        var after = new AfterSession();
        before.SetSeed("CHART-DETAIL-BENCHMARK"); after.SetSeed("CHART-DETAIL-BENCHMARK");
        before.ConfigureCrewCapabilities(false, false, false, false, false, false);
        after.ConfigureCrewCapabilities(false, false, false, false, false, false);
        before.StartVoyage(); after.StartVoyage();
        float[,] coordinates = { { 50f, 32f }, { 51.3f, 33.7f }, { 0f, 0f }, { 100f, 64f }, { 8f, 33f } };
        var beforePoints = Enumerable.Range(0, 5).Select(i => new BeforePoint(coordinates[i, 0], coordinates[i, 1])).ToArray();
        var afterPoints = Enumerable.Range(0, 5).Select(i => new AfterPoint(coordinates[i, 0], coordinates[i, 1])).ToArray();
        var beforeReveal = (Action<BeforePoint>)Delegate.CreateDelegate(typeof(Action<BeforePoint>), before,
            typeof(BeforeSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic));
        var afterReveal = (Action<AfterPoint>)Delegate.CreateDelegate(typeof(Action<AfterPoint>), after,
            typeof(AfterSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic));
        Action<int> beforeAction = i => {
            Array.Clear(before.Map.ExploredCells, 0, before.Map.ExploredCells.Length);
            Array.Clear(before.Map.ChartExploredCells, 0, before.Map.ChartExploredCells.Length);
            beforeReveal(beforePoints[i % 5]);
        };
        Action<int> afterAction = i => {
            Array.Clear(after.Map.ExploredCells, 0, after.Map.ExploredCells.Length);
            Array.Clear(after.Map.ChartExploredCells, 0, after.Map.ChartExploredCells.Length);
            afterReveal(afterPoints[i % 5]);
        };
        for (int i = 0; i < 50; i++) { beforeAction(i); afterAction(i); }
        for (int i = 0; i < 5; i++) {
            beforeAction(i); afterAction(i);
            if (!before.Map.ExploredCells.SequenceEqual(after.Map.ExploredCells) ||
                !before.Map.ChartExploredCells.SequenceEqual(after.Map.ChartExploredCells))
                throw new Exception("Baseline and optimized output mismatch at point " + i);
        }
        double[] oldSamples = new double[5], newSamples = new double[5];
        for (int pass = 0; pass < 5; pass++) {
            // Alternate order so warm CPU and background scheduling do not
            // systematically favor either implementation.
            if ((pass & 1) == 0) {
                oldSamples[pass] = Time(beforeAction); newSamples[pass] = Time(afterAction);
            } else {
                newSamples[pass] = Time(afterAction); oldSamples[pass] = Time(beforeAction);
            }
        }
        Array.Sort(oldSamples); Array.Sort(newSamples);
        Console.WriteLine("Pure Core/Mono benchmark; no Unity Editor or Player, no FPS inference.");
        Console.WriteLine("Baseline reconstructs the old full-grid RevealAt; its method matches the archived source snapshot ignoring whitespace.");
        Console.WriteLine("Seed: CHART-DETAIL-BENCHMARK; navigator=false; original route; mixed generated terrain.");
        Console.WriteLine("5 alternating passes of " + Iterations + " observations; 50 warmup observations; arrays reset each iteration.");
        Console.WriteLine("Baseline exact original full-grid RevealAt median: " + oldSamples[2].ToString("F3") + " ms / " + Iterations);
        Console.WriteLine("Optimized bounded RevealAt median: " + newSamples[2].ToString("F3") + " ms / " + Iterations);
        Console.WriteLine("Observed speedup: " + (oldSamples[2] / newSamples[2]).ToString("F2") + "x");
        float radius = 120f / after.Map.Route.NauticalMilesPerMapUnit;
        Console.WriteLine("Map-unit vision radius: " + radius.ToString("F3"));
        for (int i = 0; i < 5; i++) {
            int coarse = Candidates(coordinates[i, 0], coordinates[i, 1], radius, 48, 32, 1f);
            int fine = Candidates(coordinates[i, 0], coordinates[i, 1], radius, 96, 64, .5f);
            Console.WriteLine("Point " + coordinates[i, 0] + "," + coordinates[i, 1] + ": original 7680 candidates; bounded " +
                (coarse + fine) + " (coarse " + coarse + ", fine " + fine + ")");
        }
        Console.WriteLine("Full old/new per-cell parity passed for all 5 benchmark points.");
        return 0;
    }
    private static double Time(Action<int> action) {
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) action(i);
        timer.Stop(); return timer.Elapsed.TotalMilliseconds;
    }
    private static int Candidates(float x, float y, float radius, int columns, int rows, float edgeScale) {
        float search = Math.Max(1.2f, radius + .85f * edgeScale);
        int firstX = Math.Max(0, (int)(Math.Max(0f, Math.Min(100f, x - search)) / 100f * columns) - 1);
        int lastX = Math.Min(columns - 1, (int)(Math.Max(0f, Math.Min(100f, x + search)) / 100f * columns) + 1);
        int firstY = Math.Max(0, (int)(Math.Max(0f, Math.Min(64f, y - search)) / 64f * rows) - 1);
        int lastY = Math.Min(rows - 1, (int)(Math.Max(0f, Math.Min(64f, y + search)) / 64f * rows) + 1);
        return (lastX - firstX + 1) * (lastY - firstY + 1);
    }
}
