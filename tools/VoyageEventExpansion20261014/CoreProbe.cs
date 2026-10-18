using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;
using ShatteredSail.Core.Gameplay;

internal static class VoyageEventCoreProbe
{
    public static string Output;
    static readonly TextWriter RealConsole=Console.Out;
    public static string SourceDirectory;
    private static int Main(string[] args)
    {
        Output=args[0]; SourceDirectory=args[1]; Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"test-starts.log"),string.Empty);
        File.WriteAllText(Path.Combine(Output,"scope.txt"),"Pure current Core with NUnit. Existing fixture sources are compiled whole, unchanged. Expanded checks use actual Sail()/GetForecast(), map terrain, encounter dispatch, resolution, capture/restore and sea-chart progress. Long-voyage scheduler probes refill cargo/hull to isolate scheduling; they do not measure economic balance or claim realistic player routing. Some fixed combat is completed through the normal terminal action with enemy health set to zero. Low-resource new-event safety is verified separately without refilling. Checkpoint checks are DTO round trips, not Unity JsonUtility serialization. Legacy map position baselines were captured from pre-expansion code on 2026-10-14. No Unity process or retired preview directory is used.\n");
        File.WriteAllText(Path.Combine(Output,"map-density.csv"),"route,sample,seed,total_nodes,new_fixed,sighted_new,within_1_25,within_5,straight_nm\n");
        File.WriteAllText(Path.Combine(Output,"sailing-events.csv"),"scenario,route,sequence,id,distance_nm,gap_nm\n");
        var runner=new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(),new Dictionary<string,object>{{"NumberOfTestWorkers",0},{"WorkDirectory",Output}});
        Console.WriteLine("Discovered "+runner.CountTestCases(TestFilter.Empty)+" NUnit cases.");
        var result=runner.Run(new Listener(),TestFilter.Empty);
        File.WriteAllText(Path.Combine(Output,"results.xml"),result.ToXml(true).OuterXml,new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(Output,"failures.txt"),Descendants(result).Where(r=>!r.Test.IsSuite&&r.ResultState.Status==TestStatus.Failed).Select(r=>r.FullName+"\n"+r.Message+"\n"+r.StackTrace));
        Console.WriteLine("NUnit: passed="+result.PassCount+" failed="+result.FailCount+" skipped="+result.SkipCount+" inconclusive="+result.InconclusiveCount);
        return result.FailCount==0&&result.PassCount>0?0:1;
    }
    static IEnumerable<ITestResult> Descendants(ITestResult result) { yield return result; foreach(var c in result.Children) foreach(var d in Descendants(c)) yield return d; }
    sealed class Listener:ITestListener
    {
        public void TestStarted(ITest test) { if(!test.IsSuite) File.AppendAllText(Path.Combine(Output,"test-starts.log"),test.FullName+Environment.NewLine); }
        public void TestFinished(ITestResult result) { if(!result.Test.IsSuite&&result.ResultState.Status==TestStatus.Failed) RealConsole.WriteLine("FAIL "+result.FullName+"\n"+result.Message); }
        public void TestOutput(TestOutput output) { RealConsole.Write(output.Text); }
    }
}

[TestFixture]
public sealed class VoyageEventExpansionProbe
{
    const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[][] BaselineIds={
        "common_beach_castaway,common_floating_market,common_reef_divers,common_storm_debris,common_strait_beacon,common_whale_song,event_reef_broken_mast,event_reef_coral_ambush,event_reef_fisher_cache,event_reef_hidden_cache,event_reef_saltwater_leak,event_reef_sharp_current,event_reef_shortcut,event_reef_sunken_boat".Split(','),
        "common_beach_castaway,common_floating_market,common_reef_divers,common_storm_debris,common_strait_beacon,common_whale_song,gold_convoy,gold_customs,gold_wreck".Split(','),
        "common_beach_castaway,common_floating_market,common_reef_divers,common_storm_debris,common_strait_beacon,common_whale_song,moon_ghost,moon_obelisk,moon_whisper".Split(',')};
    static string F(double value)=>value.ToString("F3",CultureInfo.InvariantCulture);
    static double Distance(VoyagePoint a,VoyagePoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    static double SegmentDistance(VoyagePoint p,VoyagePoint a,VoyagePoint b)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,t=Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));
        double x=p.X-a.X-t*dx,y=p.Y-a.Y-t*dy;return Math.Sqrt(x*x+y*y);
    }
    static CoreVoyageSession Ready(int route,string seed=null)
    {
        var session=new CoreVoyageSession();session.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=1500});
        session.RestoreProfile(10000,"断锚港");Refill(session);
        Assert.That(session.TrySelectChartSeed(seed??CoreVoyageSession.CreateChartSeed(route,"断锚港","白贝港"),out string reason),Is.True,reason);
        return session;
    }
    static void Refill(CoreVoyageSession session)
    {
        foreach(var p in typeof(VoyageCargo).GetProperties().Where(p=>p.PropertyType==typeof(int)))p.SetValue(session.Cargo,0);
        session.Cargo.Water=session.Cargo.Food=200;session.Cargo.Wood=8;session.Cargo.Medicine=4;
        typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session,session.MaxHull);
    }
    static void Enter(CoreVoyageSession session,CoreEncounter encounter)
    {
        typeof(CoreVoyageSession).GetField("activeNode",PrivateInstance).SetValue(session,null);
        typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,encounter);
        typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session,CoreDemoPhase.Encounter);
    }
    static bool IsDynamic(CoreEncounter encounter)=>encounter!=null&&encounter.RepeatPolicy==CoreEncounterRepeatPolicy.RandomVoyage;
    static string DynamicId(CoreEncounter encounter)=>encounter.Id=="random_ambush_warning"?"random_ambush":encounter.Id;
    static void Resolve(CoreVoyageSession session)
    {
        if(session.Phase!=CoreDemoPhase.Encounter)return;
        int choice=Enumerable.Range(0,session.ActiveEncounter.Choices.Count)
            .Where(i=>session.IsChoiceAvailable(i)&&!session.ActiveEncounter.Choices[i].StartsBattle&&!session.ActiveEncounter.Choices[i].Effect.ForceReturn)
            .OrderBy(i=>session.ActiveEncounter.Choices[i].Hours).DefaultIfEmpty(-1).First();
        if(choice>=0)Assert.That(session.ResolveChoice(choice),Is.True);
        else if(!session.SkipEncounter())
        {
            int battle=Enumerable.Range(0,session.ActiveEncounter.Choices.Count).First(i=>session.IsChoiceAvailable(i));
            Assert.That(session.ResolveChoice(battle),Is.True);
            if(session.Phase==CoreDemoPhase.Battle){session.Battle.EnemyHull=0;Assert.That(session.EndBattleTurn(),Is.True);}
        }
        Assert.That(session.Phase,Is.EqualTo(CoreDemoPhase.Sailing),"An event must release the voyage.");
    }
    static void SailStep(CoreVoyageSession session)
    {
        Refill(session);SailingDirection chosen=SailingDirection.East;double best=double.NegativeInfinity;
        foreach(SailingDirection heading in Enum.GetValues(typeof(SailingDirection)))
        {
            session.SetHeading(heading);session.SetDistance(180);var forecast=session.GetForecast();
            if(forecast==null||forecast.Blocked||forecast.Clipped||forecast.ActualDistanceNm<20)continue;
            double clearance=Math.Min(Math.Min(forecast.Target.X,session.Map.WorldWidth-forecast.Target.X),Math.Min(forecast.Target.Y,session.Map.WorldHeight-forecast.Target.Y));
            double score=forecast.ActualDistanceNm+clearance*3;
            if(score>best){best=score;chosen=heading;}
        }
        Assert.That(best,Is.GreaterThan(double.NegativeInfinity),"A reachable unclipped heading must remain.");
        session.SetHeading(chosen);session.SetDistance(180);float before=session.TotalDistanceNm;
        Assert.That(session.Sail(),Is.True,"Actual Sail must progress.");
        Assert.That(session.TotalDistanceNm,Is.GreaterThan(before));
        Assert.That(session.Phase,Is.Not.EqualTo(CoreDemoPhase.Settlement),"Routing probe must stay at sea.");
    }
    static Tuple<string,float> NextDynamic(CoreVoyageSession session)
    {
        for(int step=0;step<120;step++)
        {
            Resolve(session);SailStep(session);
            if(session.Phase==CoreDemoPhase.Encounter&&IsDynamic(session.ActiveEncounter))return Tuple.Create(DynamicId(session.ActiveEncounter),session.TotalDistanceNm);
        }
        Assert.Fail("No dynamic event in 120 real Sail calls.");return null;
    }
    static void Record(string scenario,int route,int sequence,Tuple<string,float> found,float previous)
    {
        File.AppendAllText(Path.Combine(VoyageEventCoreProbe.Output,"sailing-events.csv"),String.Join(",",scenario,route,sequence,found.Item1,F(found.Item2),F(found.Item2-previous))+"\n");
    }

    [TestCase(0)][TestCase(1)][TestCase(2)]
    public void NewMapsPreserveOldEventsAndVaryFourOfEightNewFixedEvents(int route)
    {
        var catalog=CoreVoyageEventCatalog.CreateFixedEncounters(CoreVoyageSession.Routes[route].Id).ToArray();
        Assert.That(catalog.Length,Is.EqualTo(8));var newIds=new HashSet<string>(catalog.Select(e=>e.Id));var sets=new HashSet<string>();var union=new HashSet<string>();
        for(int sample=0;sample<20;sample++)
        {
            var session=Ready(route);Assert.That(session.SelectedSeed,Does.StartWith("SS4."));var map=session.PreviewSelectedSeaChart();
            Assert.That(map.GeometryVersion,Is.EqualTo(2));
            foreach(string id in BaselineIds[route])Assert.That(map.Nodes.Any(n=>n.Encounter.Id==id),Is.True,id);
            var added=map.Nodes.Where(n=>newIds.Contains(n.Encounter.Id)).ToArray();Assert.That(added.Length,Is.EqualTo(4));
            Assert.That(map.Nodes.Select(n=>n.Encounter.Id).Distinct().Count(),Is.EqualTo(map.Nodes.Count));
            foreach(var node in added){Assert.That(node.Encounter.HasChartLocation,Is.True,node.Encounter.Id);union.Add(node.Encounter.Id);}
            sets.Add(String.Join("|",added.Select(n=>n.Encounter.Id).OrderBy(id=>id)));
            var ordinary=map.Nodes.Where(n=>BaselineIds[route].Contains(n.Encounter.Id)||newIds.Contains(n.Encounter.Id)).ToArray();
            File.AppendAllText(Path.Combine(VoyageEventCoreProbe.Output,"map-density.csv"),String.Join(",",route,sample,session.SelectedSeed,map.Nodes.Count,added.Length,added.Count(n=>n.Encounter.CanDiscoverBySight),ordinary.Count(n=>SegmentDistance(n.Position,map.Departure,map.Destination)<=1.25),ordinary.Count(n=>SegmentDistance(n.Position,map.Departure,map.Destination)<=5),F(Distance(map.Departure,map.Destination)*map.Route.NauticalMilesPerMapUnit))+"\n");
        }
        Assert.That(sets.Count,Is.GreaterThan(1),"New charts must not always use one fixed-event set.");Assert.That(union.Count,Is.GreaterThan(4));
        Console.WriteLine("NEW MAP route="+route+" distinct additions sets="+sets.Count+" new events covered="+union.Count);
    }

    [Test]
    public void LegacySS3ChartsRetainTheirMeasuredNodePositions()
    {
        var rows=File.ReadAllLines(Path.Combine(VoyageEventCoreProbe.SourceDirectory,"LegacyMapPositions.csv")).Skip(1).Select(s=>s.Split(',').Select(v=>v.Trim('"')).ToArray()).ToArray();
        foreach(var group in rows.GroupBy(r=>r[1]))
        {
            var map=Ready(int.Parse(group.First()[0]),group.Key).PreviewSelectedSeaChart();Assert.That(map.GeometryVersion,Is.EqualTo(1));Assert.That(map.Nodes.Count,Is.EqualTo(group.Count()));
            foreach(var row in group)
            {
                var node=map.Nodes.Single(n=>n.Encounter.Id==row[2]);
                Assert.That(node.Position.X,Is.EqualTo(float.Parse(row[3],CultureInfo.InvariantCulture)).Within(.0011),node.Encounter.Id+" X");
                Assert.That(node.Position.Y,Is.EqualTo(float.Parse(row[4],CultureInfo.InvariantCulture)).Within(.0011),node.Encounter.Id+" Y");
            }
        }
    }

    [TestCase(0)][TestCase(1)][TestCase(2)]
    public void EveryNewDynamicAndFixedEventResolvesOrCanBeSkippedWithEmptyStores(int route)
    {
        var routeDef=CoreVoyageSession.Routes[route];var dynamicIds=CoreVoyageEventCatalog.RandomEventIds(routeDef.Id).ToArray();
        Assert.That(dynamicIds.Length,Is.EqualTo(10));Assert.That(dynamicIds.Distinct().Count(),Is.EqualTo(10));
        var encounters=dynamicIds.Select(id=>CoreVoyageEventCatalog.CreateRandomEncounter(id,routeDef.RecommendedPower)).Concat(CoreVoyageEventCatalog.CreateFixedEncounters(routeDef.Id));
        foreach(var encounter in encounters)
        {
            Assert.That(encounter,Is.Not.Null);var session=Ready(route);session.StartVoyage();
            foreach(var p in typeof(VoyageCargo).GetProperties().Where(p=>p.PropertyType==typeof(int)))p.SetValue(session.Cargo,0);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session,1);session.ConfigureCrewCapabilities(false,false,false,false,false,false);Enter(session,encounter);
            if(!encounter.Mandatory){Assert.That(session.SkipEncounter(),Is.True,encounter.Id);Assert.That(session.Phase,Is.EqualTo(CoreDemoPhase.Sailing));continue;}
            var exits=Enumerable.Range(0,encounter.Choices.Count).Where(i=>!encounter.Choices[i].StartsBattle&&session.IsChoiceAvailable(i)).ToArray();
            Assert.That(exits,Is.Not.Empty,encounter.Id+" has no zero-store noncombat fallback.");
            Assert.That(session.ResolveChoice(exits[0]),Is.True,encounter.Id);Assert.That(session.Phase,Is.EqualTo(CoreDemoPhase.Sailing).Or.EqualTo(CoreDemoPhase.Settlement),encounter.Id);
        }
    }

    [TestCase(0)][TestCase(1)][TestCase(2)]
    public void RealSailingContinuesBeyondFourEventsAndRefillsTwelveItemBags(int route)
    {
        var session=Ready(route);session.StartVoyage();Assert.That(session.CurrentVoyageRandomEventIds.Count,Is.EqualTo(12));
        var ids=new List<string>();float previous=0;float minGap=route==1?180:220;float lastFixed=-1000;
        for(int eventIndex=0;eventIndex<28;eventIndex++)
        {
            Tuple<string,float> found=null;
            for(int step=0;step<120;step++)
            {
                if(session.Phase==CoreDemoPhase.Encounter&&!IsDynamic(session.ActiveEncounter))lastFixed=session.TotalDistanceNm;
                Resolve(session);SailStep(session);
                if(session.Phase==CoreDemoPhase.Encounter&&IsDynamic(session.ActiveEncounter)){found=Tuple.Create(DynamicId(session.ActiveEncounter),session.TotalDistanceNm);break;}
            }
            Assert.That(found,Is.Not.Null,"Dynamic scheduler exhausted its bag or became stuck.");
            if(eventIndex>0)Assert.That(found.Item2-previous,Is.GreaterThanOrEqualTo(minGap-.1f),"Random events become too dense.");
            else Assert.That(found.Item2,Is.GreaterThanOrEqualTo(159.9f));
            Assert.That(found.Item2-lastFixed,Is.GreaterThanOrEqualTo(99.8f),"Fixed and random encounters stack without breathing room.");
            Assert.That(ids.Skip(Math.Max(0,ids.Count-8)).Contains(found.Item1),Is.False,"Route recent-8 repeats too soon.");
            ids.Add(found.Item1);Record("long-sail",route,eventIndex,found,previous);previous=found.Item2;
        }
        Assert.That(ids.Take(12).Distinct().Count(),Is.EqualTo(12));Assert.That(ids.Skip(12).Take(12).Distinct().Count(),Is.EqualTo(12));
        Assert.That(previous,Is.GreaterThan(5000f));Assert.That(ids.Distinct().Count(),Is.EqualTo(12));
        Console.WriteLine("LONG SAIL route="+route+" real-distance="+F(previous)+" dynamic-events="+ids.Count+" distinct="+ids.Distinct().Count());
    }

    [TestCase(0)][TestCase(1)][TestCase(2)]
    public void CheckpointKeepsNextEventsAndProgressCarriesRecentHistoryToNewSeeds(int route)
    {
        var original=Ready(route);original.StartVoyage();var recent=new List<string>();
        for(int i=0;i<8;i++){var found=NextDynamic(original);recent.Add(found.Item1);}
        var checkpoint=original.CaptureVoyageCheckpoint();Assert.That(checkpoint.chartRandomVersion,Is.EqualTo(1));
        var restored=new CoreVoyageSession();Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint,out string reason),Is.True,reason);
        Assert.That(restored.ActiveEncounter.Id,Is.EqualTo(original.ActiveEncounter.Id));
        float previousContinuation=checkpoint.totalDistance;
        for(int i=0;i<10;i++)
        {
            var a=NextDynamic(original);var b=NextDynamic(restored);Assert.That(b.Item1,Is.EqualTo(a.Item1));Assert.That(b.Item2,Is.EqualTo(a.Item2).Within(.01f));
            recent.Add(a.Item1);Record("checkpoint-continuation",route,i,a,previousContinuation);previousContinuation=a.Item2;
        }
        Resolve(original);Assert.That(original.SafeReturn(),Is.True);original.ReturnToPortAfterSettlement();var progress=original.CaptureSeaChartProgress();
        var next=Ready(route);Assert.That(next.RestoreSeaChartProgress(progress),Is.True);next.StartVoyage();
        Assert.That(next.SelectedSeed,Is.Not.EqualTo(original.SelectedSeed));
        Assert.That(recent.Skip(recent.Count-8).Contains(next.CurrentVoyageRandomEventIds[0]),Is.False,"New seed forgets recent route events after save/restore.");
        var first=NextDynamic(next);Assert.That(recent.Skip(recent.Count-8).Contains(first.Item1),Is.False);
    }

    [Test]
    public void LegacyFourEventCheckpointStillRestoresAndNewDepartureEnablesExpandedPool()
    {
        string legacySeed=File.ReadAllLines(Path.Combine(VoyageEventCoreProbe.SourceDirectory,"LegacyMapPositions.csv"))[1].Split(',')[1].Trim('"');
        var source=Ready(0,legacySeed);source.StartVoyage();var checkpoint=source.CaptureVoyageCheckpoint();
        checkpoint.chartRandomVersion=0;checkpoint.chartRandomOrder=new[]{"random_tailwind","random_floating_supplies","random_food_spoilage","random_ambush"};
        checkpoint.chartRandomEncounterIds=(string[])checkpoint.chartRandomOrder.Clone();checkpoint.chartRandomIndex=0;checkpoint.nextChartRandomDistance=180;
        var restored=new CoreVoyageSession();Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint,out string reason),Is.True,reason);Assert.That(restored.CurrentVoyageRandomEventIds.Count,Is.EqualTo(4));
        for(int i=0;i<4;i++){var found=NextDynamic(restored);Assert.That(found.Item1,Is.EqualTo(checkpoint.chartRandomOrder[i]));}
        Resolve(restored);float start=restored.TotalDistanceNm;
        for(int i=0;i<12;i++){SailStep(restored);Assert.That(IsDynamic(restored.ActiveEncounter),Is.False,"Suspended legacy script must not silently upgrade mid-voyage.");Resolve(restored);}
        Assert.That(restored.TotalDistanceNm-start,Is.GreaterThan(1000));Assert.That(restored.SafeReturn(),Is.True);restored.ReturnToPortAfterSettlement();Refill(restored);restored.StartVoyage();
        Assert.That(restored.CurrentVoyageRandomEventIds.Count,Is.EqualTo(12));Assert.That(restored.CaptureVoyageCheckpoint().chartRandomVersion,Is.EqualTo(1));
    }
}
