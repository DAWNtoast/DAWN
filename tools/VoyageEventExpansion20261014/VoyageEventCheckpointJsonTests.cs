using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class VoyageEventCheckpointJsonTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly string OldChart="SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6";
        static CoreVoyageSession Ready(int route)
        {
            var session=new CoreVoyageSession();session.RestoreTreasureProgress(new CoreTreasureProgress{pirateExperience=1500});
            Assert.That(session.TrySelectChartSeed(route==0?OldChart:CoreVoyageSession.CreateChartSeed(route,"断锚港","白贝港"),out string reason),Is.True,reason);session.StartVoyage();return session;
        }
        static void Draw(CoreVoyageSession session)
        {
            typeof(CoreVoyageSession).GetMethod("CompleteEncounter",Hidden).Invoke(session,new object[]{false});
            var encounters=(CoreEncounter[])typeof(CoreVoyageSession).GetField("chartRandomEncounters",Hidden).GetValue(session);
            int index=(int)typeof(CoreVoyageSession).GetField("chartRandomIndex",Hidden).GetValue(session);
            typeof(CoreVoyageSession).GetMethod("BeginSeaChartTransientEncounter",Hidden).Invoke(session,new object[]{encounters[index]});
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void NewActiveIncidentAndRouteHistorySurviveUnityJsonRoundTrip(int route)
        {
            var session=Ready(route);for(int i=0;i<5;i++)Draw(session);
            string json=JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
            var parsed=JsonUtility.FromJson<CoreVoyageCheckpoint>(json);Assert.That(parsed.chartRandomVersion,Is.EqualTo(1));
            Assert.That(parsed.seaChartProgress.randomHistory.Single(h=>h.routeIndex==route).recentEvents.Length,Is.EqualTo(5));
            var loaded=new CoreVoyageSession();Assert.That(loaded.TryRestoreVoyageCheckpoint(parsed,out string reason),Is.True,reason);
            Assert.That(loaded.ActiveEncounter.Id,Is.EqualTo(session.ActiveEncounter.Id));
            Assert.That(JsonUtility.ToJson(loaded.CaptureVoyageCheckpoint()),Is.EqualTo(json));
            var progress=JsonUtility.FromJson<CoreSeaChartProgress>(JsonUtility.ToJson(loaded.CaptureSeaChartProgress()));
            var fresh=new CoreVoyageSession();Assert.That(fresh.RestoreSeaChartProgress(progress),Is.True);
            CollectionAssert.AreEqual(progress.randomHistory.Single(h=>h.routeIndex==route).recentEvents,fresh.CaptureSeaChartProgress().randomHistory.Single(h=>h.routeIndex==route).recentEvents);
        }
        [Test]
        public void OmittedVersionAndHistoryFieldsRetainOldFourEventCheckpointSemantics()
        {
            var session=Ready(0);var old=session.CaptureVoyageCheckpoint();old.chartRandomVersion=0;
            old.chartRandomOrder=new[]{"random_tailwind","random_floating_supplies","random_food_spoilage","random_ambush"};
            old.chartRandomEncounterIds=(string[])old.chartRandomOrder.Clone();old.chartRandomIndex=4;old.nextChartRandomDistance=180;old.seaChartProgress.randomHistory=Array.Empty<CoreSeaChartRandomHistory>();
            string json=JsonUtility.ToJson(old);Assert.That(json,Does.Contain("\"chartRandomVersion\":0,"));Assert.That(json,Does.Contain(",\"randomHistory\":[]"));
            json=json.Replace("\"chartRandomVersion\":0,",string.Empty).Replace(",\"randomHistory\":[]",string.Empty);
            var parsed=JsonUtility.FromJson<CoreVoyageCheckpoint>(json);Assert.That(parsed.chartRandomVersion,Is.Zero);
            var loaded=new CoreVoyageSession();Assert.That(loaded.TryRestoreVoyageCheckpoint(parsed,out string reason),Is.True,reason);
            Assert.That(loaded.CaptureVoyageCheckpoint().chartRandomVersion,Is.Zero);Assert.That(loaded.CurrentVoyageRandomEventIds.Count,Is.EqualTo(4));
            Assert.That(loaded.CaptureSeaChartProgress().randomHistory,Is.Empty);
            Assert.That(loaded.SafeReturn(),Is.True);loaded.ReturnToPortAfterSettlement();loaded.StartVoyage();
            Assert.That(loaded.CurrentVoyageRandomEventIds.Count,Is.EqualTo(12));Assert.That(loaded.CaptureVoyageCheckpoint().chartRandomVersion,Is.EqualTo(1));
        }
    }
}
