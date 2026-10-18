using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreDestinationBossGuidanceTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SailingIntoPortSightRevealsBlockingBossOutsideSightWithoutExploringItsCell()
        {
            CoreVoyageSession session = StartOrdinary();
            CoreMapNode boss = session.Map.Nodes.Single(node => node.IsBoss);
            foreach (CoreMapNode node in session.Map.Nodes) { node.Discovered = false; if (!node.IsBoss) node.Visited = true; }
            Array.Clear(session.Map.TerrainCells, 0, session.Map.TerrainCells.Length);
            Array.Clear(session.Map.ExploredCells, 0, session.Map.ExploredCells.Length);
            Array.Clear(session.Map.ChartExploredCells, 0, session.Map.ChartExploredCells.Length);
            VoyagePoint approach = AwayFromBoss(session, 6f);
            SetProperty(session, "Position", approach);
            Observe(session, approach);
            Assert.That(session.DestinationPortDiscovered, Is.False);
            Assert.That(boss.Discovered, Is.False);
            session.SetCourseTo(AwayFromBoss(session, 2f));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.DestinationPortDiscovered, Is.True);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.True);
            Assert.That(session.DestinationBossGuidanceNode, Is.SameAs(boss));
            Assert.That(boss.Discovered, Is.True);
            Assert.That(boss.IsChartVisible, Is.True);
            Assert.That(session.Map.ExploredCells[session.Map.CellIndex(boss.Position)], Is.False, "Revealing an objective must not explore the sea around it.");
            Assert.That(session.BossDefeated, Is.False, "Guidance must not unlock the port.");
            Assert.That(GuidanceLogs(session), Is.EqualTo(1));
            Observe(session, session.Position);
            Assert.That(GuidanceLogs(session), Is.EqualTo(1));
        }

        [Test]
        public void PassingThroughSightOnASailingLegPersistsGuidanceAfterLeavingSightAgain()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.Cargo.Water = session.Cargo.Food = 200;
            session.SetSeed("PORT-GUIDE-GRAZING");
            session.StartVoyage();
            foreach (CoreMapNode node in session.Map.Nodes) if (!node.IsBoss) node.Visited = true;
            Array.Clear(session.Map.TerrainCells, 0, session.Map.TerrainCells.Length);
            VoyagePoint port = session.Map.Destination;
            VoyagePoint center = AwayFromBoss(session, 3.5f);
            float dx = (center.X - port.X) / 3.5f, dy = (center.Y - port.Y) / 3.5f;
            var start = new VoyagePoint(center.X - dy * 3f, center.Y + dx * 3f);
            var end = new VoyagePoint(center.X + dy * 3f, center.Y - dx * 3f);
            SetProperty(session, "Position", start);
            Observe(session, start);
            Assert.That(session.DestinationPortDiscovered, Is.False);
            session.SetCourseTo(end);
            session.SetDistance(session.MaximumDistanceNm);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Position.X, Is.EqualTo(end.X).Within(.001f));
            Assert.That(session.Position.Y, Is.EqualTo(end.Y).Within(.001f));
            Assert.That((bool)typeof(CoreVoyageSession).GetMethod("CanSeePoint", Private).Invoke(session,
                new object[] { end, port, 120f / session.Map.Route.NauticalMilesPerMapUnit, 0f }), Is.False);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.True, "An observation made during the sailed path must survive leaving its radius.");
            Assert.That(GuidanceLogs(session), Is.EqualTo(1));
        }

        [Test]
        public void HistoricalExplorationAndKnownMarkersDoNotCountAsSeeingTheDestination()
        {
            CoreVoyageSession session = StartOrdinary();
            for (int i = 0; i < session.Map.ExploredCells.Length; i++) session.Map.ExploredCells[i] = true;
            for (int i = 0; i < session.Map.ChartExploredCells.Length; i++) session.Map.ChartExploredCells[i] = true;
            foreach (CoreMapNode node in session.Map.Nodes) node.Discovered = true;
            Observe(session, session.Map.Departure);
            Assert.That(session.DestinationPortDiscovered, Is.False);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.False);
            Assert.That(session.DestinationBossGuidanceNode, Is.Null);
            Assert.That(GuidanceLogs(session), Is.Zero);
        }

        [Test]
        public void PortWithinNominalRadiusStillRequiresActualTerrainVisibility()
        {
            CoreVoyageSession session = StartOrdinary();
            VoyagePoint observer = AwayFromBoss(session, 3.9f);
            for (int i = 0; i < session.Map.TerrainCells.Length; i++) session.Map.TerrainCells[i] = SeaTerrainType.StormFront;
            Observe(session, observer);
            Assert.That(session.DestinationPortDiscovered, Is.False, "A nominal distance check must not see through the storm.");
            Array.Clear(session.Map.TerrainCells, 0, session.Map.TerrainCells.Length);
            Observe(session, observer);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.True);
        }

        [Test]
        public void GuidanceSurvivesCheckpointAwayFromPortAndResetsForANewVoyage()
        {
            CoreVoyageSession source = StartOrdinary();
            Observe(source, AwayFromBoss(source, 2f));
            SetProperty(source, "Position", source.Map.Departure);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            Assert.That(checkpoint.destinationPortDiscovered, Is.True);
            CoreVoyageSession restored = Restore(checkpoint);
            Assert.That(restored.IsDestinationBossGuidanceActive, Is.True);
            Assert.That(restored.DestinationBossGuidanceNode.Discovered, Is.True);
            Assert.That(GuidanceLogs(restored), Is.EqualTo(1));
            Observe(restored, restored.Map.Departure);
            Assert.That(GuidanceLogs(restored), Is.EqualTo(1));
            Assert.That(restored.SafeReturn(), Is.True);
            restored.ReturnToPortAfterSettlement();
            restored.StartVoyage();
            Assert.That(restored.DestinationPortDiscovered, Is.False);
            Assert.That(restored.IsDestinationBossGuidanceActive, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyCheckpointInfersDiscoveryOnlyFromCurrentSight(bool currentlyVisible)
        {
            CoreVoyageSession source = StartOrdinary();
            if (currentlyVisible) SetProperty(source, "Position", AwayFromBoss(source, 2f));
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.destinationGuidanceVersion = 0;
            checkpoint.destinationPortDiscovered = false;
            for (int i = 0; i < checkpoint.exploredCells.Length; i++) checkpoint.exploredCells[i] = true;
            for (int i = 0; i < checkpoint.chartExploredCells.Length; i++) checkpoint.chartExploredCells[i] = true;
            CoreVoyageSession restored = Restore(checkpoint);
            Assert.That(restored.DestinationPortDiscovered, Is.EqualTo(currentlyVisible));
            Assert.That(restored.IsDestinationBossGuidanceActive, Is.EqualTo(currentlyVisible));
            Assert.That(GuidanceLogs(restored), Is.EqualTo(currentlyVisible ? 1 : 0));
            CoreVoyageSession again = Restore(restored.CaptureVoyageCheckpoint());
            Assert.That(GuidanceLogs(again), Is.EqualTo(GuidanceLogs(restored)), "Resume must not replay the first-sighting log.");
        }

        [Test]
        public void RegionalVictoryEndsGuidanceAndReplayOfUnlockedChartDoesNotBlockThePortAgain()
        {
            CoreVoyageSession session = StartOrdinary();
            Observe(session, AwayFromBoss(session, 2f));
            CoreMapNode boss = session.DestinationBossGuidanceNode;
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            SetProperty(session, "ActiveEncounter", boss.Encounter);
            typeof(CoreVoyageSession).GetField("activeNode", Private).SetValue(session, boss);
            int choice = Enumerable.Range(0, boss.Encounter.Choices.Count).First(i => boss.Encounter.Choices[i].StartsBattle);
            Assert.That(session.ResolveChoice(choice), Is.True, session.ChoiceUnavailableReason(choice));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True, "The terminal battle action must resolve the regional victory.");
            Assert.That(session.BossDefeated, Is.True);
            Assert.That(session.DestinationPortDiscovered, Is.True);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.False);
            Assert.That(session.DestinationBossGuidanceNode, Is.Null);
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.StartVoyage();
            Assert.That(session.BossDefeated, Is.True, "Existing route access remains open despite its new guardian.");
            Assert.That(session.DestinationPortDiscovered, Is.False);
            Observe(session, session.Map.Destination);
            Assert.That(session.DestinationPortDiscovered, Is.True);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.False);
        }

        [Test]
        public void ReverseChartObservesItsActualDestinationRatherThanTheForwardEndpoint()
        {
            CoreVoyageSession session = StartOrdinary(true);
            Assert.That(session.Map.DepartureName, Is.EqualTo("白贝港"));
            Assert.That(session.Map.DestinationName, Is.EqualTo("断锚港"));
            Observe(session, session.Map.Departure);
            Assert.That(session.DestinationPortDiscovered, Is.False);
            Observe(session, session.Map.Destination);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.True);
            Assert.That(session.DestinationBossGuidanceNode.IsBoss, Is.True);
        }

        [TestCase("guided")]
        [TestCase("tutorial-chart")]
        [TestCase("treasure")]
        public void TutorialAndTreasureVoyagesDoNotAcquireOrdinaryPortGuidance(string kind)
        {
            var session = new CoreVoyageSession();
            if (kind == "guided") { session.ConfigureGuidedTutorialVoyage(1); session.StartVoyage(); }
            else if (kind == "tutorial-chart")
            {
                Assert.That(session.TrySelectChartSeed("SS2.0.0.1.0.2.72533DBC.00000000.4858B60F", out string reason), Is.True, reason);
                session.StartVoyage();
            }
            else
            {
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = "guide-map",
                    maps = new[] { new CoreTreasureMapSave { id = "guide-map", seed = "PORT-GUIDE-TREASURE", rankId = "weathered" } } });
                Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            }
            Observe(session, session.Map.Destination);
            Assert.That(session.DestinationPortDiscovered, Is.False);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.False);
            Assert.That(session.DestinationBossGuidanceNode, Is.Null);
            Assert.That(GuidanceLogs(session), Is.Zero);
        }

        private static CoreVoyageSession StartOrdinary(bool reverse = false)
        {
            var session = new CoreVoyageSession();
            if (reverse) session.RestoreProfile(500, "白贝港");
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = 10;
            Assert.That(session.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason), Is.True, reason);
            session.StartVoyage();
            return session;
        }

        private static VoyagePoint AwayFromBoss(CoreVoyageSession session, float distance)
        {
            CoreMapNode boss = session.Map.Nodes.Single(node => node.IsBoss);
            VoyagePoint port = session.Map.Destination;
            float dx = port.X - boss.Position.X, dy = port.Y - boss.Position.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            return new VoyagePoint(port.X + dx / length * distance, port.Y + dy / length * distance);
        }

        private static void Observe(CoreVoyageSession session, VoyagePoint observer) =>
            typeof(CoreVoyageSession).GetMethod("RevealAt", Private).Invoke(session, new object[] { observer });
        private static void SetProperty(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);
        private static int GuidanceLogs(CoreVoyageSession session) => session.LogEntries.Count(log => log.StartsWith("发现目的港「", StringComparison.Ordinal));
        private static CoreVoyageSession Restore(CoreVoyageCheckpoint checkpoint)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            return session;
        }
    }
}
