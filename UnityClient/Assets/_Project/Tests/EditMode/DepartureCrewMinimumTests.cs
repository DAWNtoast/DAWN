using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class DepartureCrewMinimumTests
    {
        [TestCase(0, 4)] [TestCase(1, 7)] [TestCase(2, 9)] [TestCase(3, 6)]
        [TestCase(4, 7)] [TestCase(5, 4)] [TestCase(6, 7)] [TestCase(7, 12)]
        [TestCase(8, 9)] [TestCase(9, 10)] [TestCase(10, 6)] [TestCase(11, 8)]
        public void EveryShipRoundsFortyPercentUpAndAcceptsTheExactRequiredCount(int ship, int required)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.SelectShip(ship), Is.True);
            Assert.That(session.MinimumDepartureOrdinaryCrew, Is.EqualTo(required));
            session.ConfigureDepartureCrew(required - 1, session.CrewCountValue - required + 1);
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));
            Assert.That(session.HasEnoughOrdinaryCrewForDeparture, Is.False, "Specialists cannot satisfy the ordinary-sailor minimum.");
            Assert.That(session.CanStartOrdinaryVoyage(out string reason), Is.False);
            Assert.That(reason, Does.Contain("40%").And.Contain(required.ToString()));
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage());
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.Map, Is.Null);

            session.ConfigureDepartureCrew(required, session.CrewCountValue - required);
            Assert.That(session.CanStartWithDepartureCrew(out reason), Is.True, reason);
            Assert.That(session.CanStartOrdinaryVoyage(out reason), Is.True, reason);
            session.StartVoyage();
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(required));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        [TestCase("ordinary")] [TestCase("chart")] [TestCase("treasure")]
        [TestCase("guided1")] [TestCase("guided2")] [TestCase("guided3")]
        public void EveryDeparturePathRejectsBeforeMoneyCargoOrPersistentProgressChanges(string mode)
        {
            CoreVoyageSession session = Ready(mode);
            session.ConfigureDepartureCrew(3, 7);
            decimal gold = session.ProfileGold;
            int[] cargo = CargoAmounts(session);
            string seed = session.SelectedSeed, destination = session.SelectedDestinationPort;
            int route = session.SelectedRouteIndex;
            CoreTreasureProgress savedTreasure = session.CaptureTreasureProgress();
            Assert.That(session.CanStartWithDepartureCrew(out string reason), Is.False);
            Assert.That(session.CanStartOrdinaryVoyage(out string ordinaryReason), Is.False);
            Assert.That(session.CanStartSelectedChart(out string chartReason), Is.False);
            Assert.That(ordinaryReason, Is.EqualTo(reason));
            Assert.That(chartReason, Is.EqualTo(reason));
            if (mode == "treasure")
            {
                Assert.That(session.CanStartTreasureVoyage(out string treasureReason), Is.False);
                Assert.That(treasureReason, Is.EqualTo(reason));
                Assert.That(session.TryStartTreasureVoyage(out treasureReason), Is.False);
            }
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage());
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage(destination));
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage(destination, new[] { 1, 2 }));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.Map, Is.Null);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(CargoAmounts(session), Is.EqualTo(cargo));
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(destination));
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(route));
            Assert.That(session.TotalHours, Is.Zero);
            Assert.That(session.TotalDistanceNm, Is.Zero);
            Assert.That(session.CanCaptureVoyageCheckpoint, Is.False);
            Assert.That(session.CaptureSeaChartProgress().charts, Is.Empty);
            CoreTreasureProgress after = session.CaptureTreasureProgress();
            Assert.That(after.maps.Select(item => item.id), Is.EqualTo(savedTreasure.maps.Select(item => item.id)));
            Assert.That(after.usedMaps, Is.Empty);
            Assert.That(after.completedSeeds, Is.EqualTo(savedTreasure.completedSeeds));
            Assert.That(after.issuedSeeds, Is.EqualTo(savedTreasure.issuedSeeds));
            Assert.That(after.selectedMapId, Is.EqualTo(savedTreasure.selectedMapId));
            Assert.That(after.fragments, Is.EqualTo(savedTreasure.fragments));
            Assert.That(after.pirateExperience, Is.EqualTo(savedTreasure.pirateExperience));

            session.ConfigureDepartureCrew(4, 6);
            Assert.That(session.CanStartWithDepartureCrew(out reason), Is.True, reason);
            if (mode == "treasure") Assert.That(session.TryStartTreasureVoyage(out reason), Is.True, reason);
            else session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(4));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(10));
        }

        [Test]
        public void FortyPercentDepartureBoundaryDoesNotAlterHalfStaffedSpeedOrEightyPercentBonus()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.MinimumDepartureOrdinaryCrew, Is.EqualTo(4));
            Assert.That(session.MinimumOrdinaryCrew, Is.EqualTo(5));
            session.ConfigureDepartureCrew(4, 6);
            Assert.That(session.HasEnoughOrdinaryCrewForDeparture, Is.True);
            Assert.That(session.IsShortStaffed, Is.True);
            Assert.That(session.CrewSpeedPenalty, Is.EqualTo(.8f));
            Assert.That(session.CrewCombatPenalty, Is.EqualTo(2));
            session.ConfigureDepartureCrew(8, 2);
            Assert.That(session.CrewSpeedBonus, Is.Zero);
            session.ConfigureDepartureCrew(9, 1);
            Assert.That(session.CrewSpeedBonus, Is.EqualTo(.65f).Within(.0001f));
        }

        [Test]
        public void LegacyVoyageBelowMinimumCanResumeButMustRestaffBeforeItsNextDeparture()
        {
            var source = Ready("ordinary");
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.ordinaryCrew = 3;
            checkpoint.specialCrew = 6;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(restored.SelectedOrdinaryCrew, Is.EqualTo(3));
            Assert.That(restored.CaptureVoyageCheckpoint().ordinaryCrew, Is.EqualTo(3));
            Assert.That(restored.SafeReturn(), Is.True);
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.CanStartOrdinaryVoyage(out reason), Is.False);
            Assert.Throws<InvalidOperationException>(() => restored.StartVoyage());
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Port));
            restored.ConfigureDepartureCrew(4, 6);
            restored.StartVoyage();
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        private static CoreVoyageSession Ready(string mode)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Beer = session.Cargo.Medicine = 0;
            session.SetSeed("DEPARTURE-MINIMUM");
            if (mode == "chart")
                Assert.That(session.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港"), out _), Is.True);
            else if (mode == "treasure")
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400,
                    selectedMapId = "weathered:CREW-MINIMUM-TREASURE", maps = new[] { new CoreTreasureMapSave {
                        id = "weathered:CREW-MINIMUM-TREASURE", seed = "CREW-MINIMUM-TREASURE", rankId = "weathered" } } });
            else if (mode.StartsWith("guided", StringComparison.Ordinal))
                session.ConfigureGuidedTutorialVoyage(int.Parse(mode.Substring("guided".Length)));
            return session;
        }

        private static int[] CargoAmounts(CoreVoyageSession session) => new[] { session.Cargo.Gold, session.Cargo.Water,
            session.Cargo.Food, session.Cargo.Wood, session.Cargo.Iron, session.Cargo.Cloth, session.Cargo.Powder,
            session.Cargo.Cannonballs, session.Cargo.Beer, session.Cargo.Medicine, session.Cargo.Gem, session.Cargo.Relic,
            session.Cargo.BlackPearl, session.Cargo.AncientMaterial, session.Cargo.AncientCompass, session.Cargo.Lantern };
    }
}
