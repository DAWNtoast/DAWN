using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreMarketCargoVolumeSafetyTests
    {
        [TestCase(0f)]
        [TestCase(0.01f)]
        [TestCase(1f)]
        public void UnderstatedCargoVolumeCannotOverloadTheShip(float quotedVolume)
        {
            var session = new CoreVoyageSession();
            decimal gold = session.ProfileGold;
            int iron = session.Cargo.Iron;
            float volume = session.Cargo.UsedVolume;
            double ironVolume = CoreWarehouseItemCatalog.Find("iron").UnitVolume;
            int quantity = (int)((session.CargoCapacityValue - volume) / ironVolume) + 1;
            // The caller quotes less than iron's actual two units of hold space.
            Assert.That(volume + quantity * quotedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue),
                "The understated caller quote must appear to fit the remaining hold.");
            Assert.That(volume + quantity * ironVolume, Is.GreaterThan(session.CargoCapacityValue + 0.001d),
                "The actual cargo must exceed the hold, including its comparison tolerance.");
            Assert.That(session.TryPurchaseMarketCargo("iron", quantity, 1m, quotedVolume), Is.False);
            Assert.That(session.Cargo.Iron, Is.EqualTo(iron));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteCargoQuoteIsRejectedBeforeAnyDebit(float quotedVolume)
        {
            var session = new CoreVoyageSession();
            decimal gold = session.ProfileGold;
            int water = session.Cargo.Water;
            Assert.That(session.TryPurchaseMarketCargo("water", 1, 1m, quotedVolume), Is.False);
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void ValidCargoPurchaseStillCommitsTheQuotedPaymentExactlyOnce()
        {
            var session = new CoreVoyageSession();
            decimal gold = session.ProfileGold;
            int iron = session.Cargo.Iron;
            float volume = session.Cargo.UsedVolume;
            Assert.That(session.TryPurchaseMarketCargo(" IRON ", 1, 7.1234m, 2f), Is.True);
            Assert.That(session.Cargo.Iron, Is.EqualTo(iron + 1));
            Assert.That(session.ProfileGold, Is.EqualTo(gold - 7.1234m));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume + 2f).Within(0.001f));
        }
    }
}
