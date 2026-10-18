using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TreasureExpeditionClockTests
    {
        [TestCase(5, 59, CoreVoyageTimeBand.Night, "夜航")]
        [TestCase(6, 0, CoreVoyageTimeBand.Day, "白昼")]
        [TestCase(16, 59, CoreVoyageTimeBand.Day, "白昼")]
        [TestCase(17, 0, CoreVoyageTimeBand.Dusk, "黄昏")]
        [TestCase(19, 59, CoreVoyageTimeBand.Dusk, "黄昏")]
        [TestCase(20, 0, CoreVoyageTimeBand.Night, "夜航")]
        [TestCase(23, 59, CoreVoyageTimeBand.Night, "夜航")]
        [TestCase(0, 0, CoreVoyageTimeBand.Night, "夜航")]
        public void MoonlitRitualAvailabilityAndStatusAgreeWithVisibleClock(int hour, int minute, CoreVoyageTimeBand expected, string label)
        {
            CoreVoyageSession session = Expedition(hour, minute);
            NavigationTimeOfDay visible = NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours);
            CoreVoyageTimeBand visibleBand = visible == NavigationTimeOfDay.Day ? CoreVoyageTimeBand.Day :
                visible == NavigationTimeOfDay.Dusk ? CoreVoyageTimeBand.Dusk : CoreVoyageTimeBand.Night;
            Assert.That(session.CurrentTreasureExpeditionTimeBand, Is.EqualTo(expected));
            Assert.That(session.CurrentTreasureExpeditionTimeBand, Is.EqualTo(visibleBand));
            Assert.That(session.TreasureExpeditionStatusSummary, Does.Contain("当前" + label));
            foreach (string id in new[] { "moonlit_dawn", "moonlit_dusk", "moonlit_midnight" })
            {
                CoreEncounterChoice ritual = session.Map.Nodes.Single(n => n.Encounter.Id == id).Encounter.Choices[0];
                string reason = session.EncounterChoiceConditionReason(ritual);
                Assert.That(reason.Length == 0, Is.EqualTo(ritual.RequiredTime == expected), id + ": " + reason);
                if (ritual.RequiredTime != expected)
                    Assert.That(reason, Does.Contain("当前为" + label));
            }
        }

        [TestCase(12, CoreVoyageTimeBand.Dusk)]
        [TestCase(18, CoreVoyageTimeBand.Night)]
        [TestCase(0, CoreVoyageTimeBand.Dawn)]
        public void ExistingTimeBandsAndOrdinaryChoiceWindowsRemainCompatible(int hour, CoreVoyageTimeBand expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreGameClock((hour + 16d) % 24d), Is.True);
            session.StartVoyage();
            Assert.That(session.IsTreasureExpedition, Is.False);
            Assert.That(session.CurrentVoyageTimeBand, Is.EqualTo(expected));
            var legacyChoice = new CoreEncounterChoice("旧时段", "", 0, new VoyageEffect(), requiredTime: expected);
            Assert.That(session.EncounterChoiceConditionReason(legacyChoice), Is.Empty);
        }

        [Test]
        public void RestoredLegacyTreasureVoyageKeepsItsOldTimeGate()
        {
            CoreVoyageSession source = Expedition(18, 0);
            CoreTreasureMapRecord active = source.ActiveTreasureMap;
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var record = (CoreTreasureMapRecord)typeof(CoreTreasureMapRecord).GetConstructors(hidden).Single()
                .Invoke(new object[] { active.Id, active.Seed, active.RankId, active.MapGeometryVersion, 0 });
            var map = (CoreVoyageMap)typeof(CoreVoyageSession).GetMethod("GenerateTreasureMap", hidden)
                .Invoke(source, new object[] { record, source.CurrentPort });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(source, map);
            typeof(CoreVoyageSession).GetProperty("ActiveTreasureMap").SetValue(source, record);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps[0].contentVersion = 0;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.IsTreasureExpedition, Is.False);
            Assert.That(restored.CurrentVoyageTimeBand, Is.EqualTo(CoreVoyageTimeBand.Night));
            Assert.That(NavigationTimeOfDayUtility.FromVoyageHours(restored.ElapsedGameHours), Is.EqualTo(NavigationTimeOfDay.Dusk));
            CoreEncounterChoice oldStarReading = CoreTreasureHuntCatalog.CreateEncounters(record.Rank)
                .Single(e => e.Id == "treasure_clue_star").Choices[0];
            Assert.That(restored.EncounterChoiceConditionReason(oldStarReading), Is.Empty);
        }

        private static CoreVoyageSession Expedition(int hour, int minute)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreGameClock((hour + minute / 60d + 16d) % 24d), Is.True);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = "clock-map",
                maps = new[] { new CoreTreasureMapSave { id = "clock-map", seed = "MOONLIT-CLOCK", rankId = "moonlit", contentVersion = 1 } } });
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            return session;
        }
    }
}
