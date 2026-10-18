using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    // Independent validation-only fixture. Uses real Unity serialization, unlike
    // the pure Core runner, and does not modify the player's project or profile.
    public sealed class PortBossGuidanceJsonTests
    {
        [Test]
        public void JsonRoundTripKeepsObservedGuidanceAfterLeavingPortSight()
        {
            CoreVoyageSession source = Start();
            typeof(CoreVoyageSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(source, new object[] { source.Map.Destination });
            SetPosition(source, source.Map.Departure);
            string json = JsonUtility.ToJson(source.CaptureVoyageCheckpoint());
            Assert.That(json, Does.Contain("\"destinationGuidanceVersion\":1"));
            Assert.That(json, Does.Contain("\"destinationPortDiscovered\":true"));
            CoreVoyageSession restored = Restore(JsonUtility.FromJson<CoreVoyageCheckpoint>(json));
            Assert.That(restored.DestinationPortDiscovered, Is.True);
            Assert.That(restored.IsDestinationBossGuidanceActive, Is.True);
            Assert.That(restored.DestinationBossGuidanceNode.Discovered, Is.True);
            Assert.That(GuideLogs(restored), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldJsonWithoutBothFieldsMigratesOnlyCurrentSight(bool currentlyVisible)
        {
            CoreVoyageSession source = Start();
            if (currentlyVisible) SetPosition(source, source.Map.Destination);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            for (int i = 0; i < checkpoint.exploredCells.Length; i++) checkpoint.exploredCells[i] = true;
            for (int i = 0; i < checkpoint.chartExploredCells.Length; i++) checkpoint.chartExploredCells[i] = true;
            string json = JsonUtility.ToJson(checkpoint);
            json = Regex.Replace(json, @",?""destinationGuidanceVersion"":\d+", string.Empty);
            json = Regex.Replace(json, @",?""destinationPortDiscovered"":(?:true|false)", string.Empty);
            Assert.That(json, Does.Not.Contain("destinationGuidanceVersion"));
            Assert.That(json, Does.Not.Contain("destinationPortDiscovered"));
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(json);
            Assert.That(decoded.destinationGuidanceVersion, Is.Zero);
            Assert.That(decoded.destinationPortDiscovered, Is.False);
            CoreVoyageSession restored = Restore(decoded);
            Assert.That(restored.IsDestinationBossGuidanceActive, Is.EqualTo(currentlyVisible));
            Assert.That(GuideLogs(restored), Is.EqualTo(currentlyVisible ? 1 : 0));
            CoreVoyageSession resumed = Restore(JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint())));
            Assert.That(GuideLogs(resumed), Is.EqualTo(GuideLogs(restored)));
        }

        [Test]
        public void CurrentVersionJsonPreservesExplicitUnobservedFlag()
        {
            CoreVoyageSession source = Start();
            SetPosition(source, source.Map.Destination);
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            Assert.That(decoded.destinationGuidanceVersion, Is.EqualTo(1));
            Assert.That(decoded.destinationPortDiscovered, Is.False);
            Assert.That(Restore(decoded).IsDestinationBossGuidanceActive, Is.False);
        }

        private static CoreVoyageSession Start()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            Assert.That(session.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason), Is.True, reason);
            session.StartVoyage();
            return session;
        }

        private static void SetPosition(CoreVoyageSession session, VoyagePoint position) =>
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, position);
        private static int GuideLogs(CoreVoyageSession session) =>
            session.LogEntries.Count(log => log.StartsWith("发现目的港「", StringComparison.Ordinal));
        private static CoreVoyageSession Restore(CoreVoyageCheckpoint checkpoint)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            return session;
        }
    }
}
