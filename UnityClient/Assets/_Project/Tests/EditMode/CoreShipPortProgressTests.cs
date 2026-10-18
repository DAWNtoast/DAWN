using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreShipPortProgressTests
    {
        [Test]
        public void SailingTheSalvageShipLeavesTheStarterAtItsOriginalPort()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RegisterShipAtCurrentPort(4), Is.True);
            Assert.That(session.SelectShip(4), Is.True);
            Arrive(session, "白贝港");

            Assert.That(session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(session.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(session.IsShipAtCurrentPort(0), Is.False);
            Assert.That(session.IsShipAtCurrentPort(4), Is.True);
            Assert.That(session.CanSelectShip(0), Is.False);
            Assert.That(session.SelectShip(0), Is.False);
            Assert.That(session.RegisterShipAtCurrentPort(0), Is.False);
            Assert.That(session.SelectedShipIndex, Is.EqualTo(4));

            Arrive(session, "断锚港");
            Assert.That(session.IsShipAtCurrentPort(0), Is.True);
            Assert.That(session.SelectShip(0), Is.True);
        }

        [Test]
        public void BuyingAndSwitchingShipsMovesOnlyTheFlagshipOnTheNextVoyage()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.SelectShip(4), Is.True);
            Arrive(session, "白贝港");
            Assert.That(session.RegisterShipAtCurrentPort(5), Is.True);
            Assert.That(session.SelectShip(5), Is.True);
            Arrive(session, "铜钟港");

            Assert.That(session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(session.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(session.ShipPort(5), Is.EqualTo("铜钟港"));
            Assert.That(session.CaptureShipPortProgress().ships.Select(entry => entry.shipIndex), Is.EqualTo(new[] { 0, 4, 5 }));
        }

        [Test]
        public void ProfileRoundTripPreservesInactiveBerthsInsteadOfResettingThemToHome()
        {
            var source = new CoreVoyageSession();
            source.SelectShip(4);
            Arrive(source, "白贝港");
            source.SelectShip(5);
            Arrive(source, "铜钟港");
            CoreShipPortProgress saved = source.CaptureShipPortProgress();

            var restored = new CoreVoyageSession();
            restored.RestoreProfile(500, source.CurrentPort);
            Assert.That(restored.RestoreShipPortProgress(saved, new[] { 0, 4, 5 }, 5), Is.True);
            Assert.That(restored.SelectShip(5), Is.True);
            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(restored.ShipPort(5), Is.EqualTo("铜钟港"));
            saved.ships[0].port = "暮潮港";
            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"), "Restored records must not retain caller-owned mutable DTOs.");
        }

        [Test]
        public void LegacyProfileKeepsFlagshipHereAndOtherOwnedShipsAtTheirHomePorts()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(500, "白贝港");
            Assert.That(session.RestoreShipPortProgress(null, new[] { 0, 1, 4, 7 }, 4), Is.True);
            Assert.That(session.SelectShip(4), Is.True);
            Assert.That(session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(session.ShipPort(1), Is.EqualTo("灰鸥港"));
            Assert.That(session.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(session.ShipPort(7), Is.EqualTo("铜钟港"));
            Assert.That(session.ShipPort(5), Is.Empty, "Unowned local stock must not acquire a saved berth.");
        }

        [TestCase(VoyageOutcome.SafeReturn)]
        [TestCase(VoyageOutcome.Failure)]
        public void ReturningOrFailingKeepsTheFlagshipAtItsDeparturePort(VoyageOutcome outcome)
        {
            var session = new CoreVoyageSession();
            session.SelectShip(4);
            session.StartVoyage("白贝港");
            Finish(session, outcome);
            Assert.That(session.CurrentPort, Is.EqualTo("断锚港"));
            Assert.That(session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(session.ShipPort(4), Is.EqualTo("断锚港"));
        }

        [Test]
        public void CheckpointPreservesAllBerthsAndArrivalMovesOnlyItsFlagship()
        {
            var source = new CoreVoyageSession();
            source.SelectShip(4);
            Arrive(source, "白贝港");
            source.SelectShip(5);
            source.StartVoyage("铜钟港");
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Finish(restored, VoyageOutcome.ArrivedAtPort);

            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(restored.ShipPort(5), Is.EqualTo("铜钟港"));
        }

        [Test]
        public void LegacyCheckpointPreservesTheLoadedProfilesInactiveBerths()
        {
            var source = new CoreVoyageSession();
            source.SelectShip(5);
            source.StartVoyage("铜钟港");
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.shipPortProgress = null;
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreShipPortProgress(new CoreShipPortProgress
            {
                ships = new[] { new CoreShipPortLocation { shipIndex = 4, port = "暮潮港" } },
            }, new[] { 0, 4, 5 }, 5), Is.True);
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Finish(restored, VoyageOutcome.ArrivedAtPort);

            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(4), Is.EqualTo("暮潮港"));
            Assert.That(restored.ShipPort(5), Is.EqualTo("铜钟港"));
        }

        [TestCase("port")]
        [TestCase("ship")]
        [TestCase("duplicate")]
        [TestCase("version")]
        [TestCase("null_entry")]
        public void InvalidProfileLocationDataDoesNotPartiallyMutateSession(string corruption)
        {
            var session = new CoreVoyageSession();
            session.SelectShip(4);
            CoreShipPortProgress invalid = session.CaptureShipPortProgress();
            if (corruption == "port") invalid.ships[0].port = "missing";
            if (corruption == "ship") invalid.ships[0].shipIndex = 999;
            if (corruption == "duplicate") invalid.ships[1].shipIndex = invalid.ships[0].shipIndex;
            if (corruption == "version") invalid.version = 99;
            if (corruption == "null_entry") invalid.ships[0] = null;
            Assert.That(session.RestoreShipPortProgress(invalid, new[] { 0, 4 }, 4), Is.False);
            Assert.That(session.SelectedShipIndex, Is.EqualTo(4));
            Assert.That(session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(session.ShipPort(4), Is.EqualTo("断锚港"));
        }

        [Test]
        public void CheckpointRejectsFlagshipAtAnotherPortWithoutMutatingTheLoadedProfile()
        {
            var source = new CoreVoyageSession();
            source.SelectShip(4);
            source.StartVoyage("白贝港");
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.shipPortProgress.ships.Single(entry => entry.shipIndex == 4).port = "暮潮港";
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(restored.SelectedShipIndex, Is.Zero);
            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(4), Is.Empty);
        }

        [Test]
        public void ExplicitProfileRelocationMovesOnlyTheSelectedShip()
        {
            var session = new CoreVoyageSession();
            session.SelectShip(4);
            session.RestoreProfile(500, "暮潮港");
            Assert.That(session.ShipPort(4), Is.EqualTo("暮潮港"));
            Assert.That(session.ShipPort(0), Is.EqualTo("断锚港"));
        }

        private static void Arrive(CoreVoyageSession session, string destination)
        {
            session.StartVoyage(destination);
            Finish(session, VoyageOutcome.ArrivedAtPort);
        }

        private static void Finish(CoreVoyageSession session, VoyageOutcome outcome)
        {
            typeof(CoreVoyageSession).GetMethod("FinishVoyage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { outcome });
            session.ReturnToPortAfterSettlement();
        }
    }
}
