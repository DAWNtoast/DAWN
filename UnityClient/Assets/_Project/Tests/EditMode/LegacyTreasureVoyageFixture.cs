using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    internal static class LegacyTreasureVoyageFixture
    {
        // Fresh departures promote held charts to the current expedition. Old
        // event regressions must instead resume an already active legacy chart.
        internal static CoreVoyageSession Resume(CoreVoyageSession source)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            CoreTreasureMapRecord active = source.ActiveTreasureMap;
            Assert.That(active, Is.Not.Null);
            var legacy = (CoreTreasureMapRecord)typeof(CoreTreasureMapRecord).GetConstructors(hidden).Single()
                .Invoke(new object[] { active.Id, active.Seed, active.RankId, active.MapGeometryVersion, 0 });
            var map = (CoreVoyageMap)typeof(CoreVoyageSession).GetMethod("GenerateTreasureMap", hidden)
                .Invoke(source, new object[] { legacy, source.CurrentPort });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(source, map);
            typeof(CoreVoyageSession).GetProperty("ActiveTreasureMap").SetValue(source, legacy);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps.Single(item => item.seed == active.Seed).contentVersion = 0;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(
                JsonUtility.ToJson(checkpoint)), out string reason), Is.True, reason);
            Assert.That(restored.ActiveTreasureMap.ContentVersion, Is.Zero);
            return restored;
        }
    }
}
