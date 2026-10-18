using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageExplorationRevisitTests
    {
        [TestCase(0, "event_reef_hidden_cache")]
        [TestCase(1, "gold_wreck")]
        [TestCase(2, "moon_obelisk")]
        public void LockedClueDestination_SkipCheckpointRestoreAndSelectPreservesReward(int route, string targetId)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            session.SelectRoute(route);
            session.SetSeed("EXPLORATION-CHAIN-20261015-" + route);
            session.StartVoyage();
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == targetId);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, target.Position);
            session.SetCourseTo(target.Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(targetId));
            int gated = Enumerable.Range(0, target.Encounter.Choices.Count).First(index => !string.IsNullOrEmpty(target.Encounter.Choices[index].RequiredDiscovery));
            Assert.That(session.IsChoiceAvailable(gated), Is.False);
            Assert.That(session.SkipEncounter(), Is.True);
            Assert.That(target.Visited, Is.False);
            Assert.That(target.Skipped, Is.True);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(session.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            CoreMapNode restoredTarget = restored.Map.Nodes.Single(node => node.Encounter.Id == targetId);
            Assert.That(restoredTarget.Visited, Is.False);
            Assert.That(restoredTarget.Skipped, Is.True);
            restored.SetCourseTo(restoredTarget.Position);
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(targetId));
            Assert.That(restoredTarget.Skipped, Is.False);
            Assert.That(restored.IsChoiceAvailable(gated), Is.False);
        }
    }
}
