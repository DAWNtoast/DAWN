using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class DepartureProvisionPurchaseTests
    {
        [TestCase(100, 200)]
        [TestCase(0, 200)]
        [TestCase(100, 0)]
        public void MarketProvisionOrder_BuysBothGoodsAsOneTransaction(int water, int food)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = 0; session.Cargo.Food = 0;
            decimal gold = session.ProfileGold;
            Assert.That(session.TryPurchaseMarketProvisions(water, food, 37), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold - 37));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
        }

        [TestCase(-1, 100, 20)]
        [TestCase(100, -1, 20)]
        [TestCase(0, 0, 20)]
        [TestCase(100, 100, 0)]
        [TestCase(100, 100, 501)]
        [TestCase(int.MaxValue, 100, 20)]
        public void InvalidOrUnaffordableOrders_DoNotPartiallySpend(int water, int food, int cost)
        {
            var session = new CoreVoyageSession();
            int beforeWater = session.Cargo.Water;
            int beforeFood = session.Cargo.Food;
            decimal gold = session.ProfileGold;
            Assert.That(session.TryPurchaseMarketProvisions(water, food, cost), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Water, Is.EqualTo(beforeWater));
            Assert.That(session.Cargo.Food, Is.EqualTo(beforeFood));
        }

        [Test]
        public void CombinedCapacityFailure_DoesNotPurchaseOnlyOneResource()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = 0; session.Cargo.Food = 0;
            float other = session.Cargo.UsedVolume - session.Cargo.Wood;
            session.Cargo.Wood = (int)(session.CargoCapacityValue - other - 10f);
            Assert.That(session.Cargo.UsedVolume + 100 * .09f, Is.LessThanOrEqualTo(session.CargoCapacityValue));
            decimal gold = session.ProfileGold;
            Assert.That(session.TryPurchaseMarketProvisions(100, 100, 40), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Water, Is.Zero);
            Assert.That(session.Cargo.Food, Is.Zero);
        }

        [Test]
        public void PurchaseAtSea_IsRejectedWithoutChangingCargoOrMoney()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            decimal gold = session.ProfileGold;
            int water = session.Cargo.Water;
            int food = session.Cargo.Food;
            Assert.That(session.TryPurchaseMarketProvisions(100, 100, 40), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
        }

        [Test]
        public void ProvisionEstimate_UsesTheSelectedCrewAndCook()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.EstimateDailyRationConsumption(8, false), Is.EqualTo(8));
            Assert.That(session.EstimateDailyRationConsumption(8, true), Is.LessThan(8));
            Assert.That(session.EstimateDailyRationConsumption(4, true),
                Is.EqualTo(session.EstimateDailyRationConsumption(8, true) / 2));
            Assert.That(session.EstimateDailyRationConsumption(999, false), Is.EqualTo(session.CrewCountValue));
        }
    }
}
