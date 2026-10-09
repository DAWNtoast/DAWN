using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreMarketSupplyOrderTests
    {
        [Test]
        public void SixSupplyOrder_ChargesTheQuotedTotalOnceAndAddsEveryLine()
        {
            var session = new CoreVoyageSession();
            var order = new VoyageSupplyOrder { Water = 10, Food = 10, Medicine = 1, Wood = 1, Powder = 1, Cannonballs = 1 };
            decimal gold = session.ProfileGold;
            int[] before = SupplyAmounts(session);

            Assert.That(session.TryPurchaseMarketSupplyOrder(order, 85), Is.True);

            Assert.That(session.ProfileGold, Is.EqualTo(gold - 85));
            int index = 0;
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                Assert.That(session.GetSupplyAmount(type), Is.EqualTo(before[index++] + order[type]), type.ToString());
            Assert.That(order.Water, Is.EqualTo(10), "Committing must not alter the caller's order.");
        }

        [Test]
        public void IndividuallyFittingLinesThatExceedTheCombinedHold_DoNotPartiallyPurchase()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = 0;
            session.Cargo.Food = 0;
            float other = session.Cargo.UsedVolume - session.Cargo.Wood;
            session.Cargo.Wood = (int)(session.CargoCapacityValue - other - 10f);
            Assert.That(session.Cargo.UsedVolume + 100 * .07f, Is.LessThanOrEqualTo(session.CargoCapacityValue));
            Assert.That(session.Cargo.UsedVolume + 100 * .09f, Is.LessThanOrEqualTo(session.CargoCapacityValue));
            Assert.That(session.Cargo.UsedVolume + 100 * .16f, Is.GreaterThan(session.CargoCapacityValue));

            AssertRejectedWithoutMutation(session, new VoyageSupplyOrder { Water = 100, Food = 100 }, 40);
        }

        [Test]
        public void CombinedPriceAboveFunds_DoesNotBuyTheAffordableFirstLine()
        {
            var session = new CoreVoyageSession();
            decimal linePrice = session.ProfileGold / 2 + 1;
            Assert.That(linePrice, Is.LessThanOrEqualTo(session.ProfileGold));
            AssertRejectedWithoutMutation(session, new VoyageSupplyOrder { Water = 10, Food = 10 }, linePrice * 2);
        }

        [TestCase(VoyageSupplyType.Water)]
        [TestCase(VoyageSupplyType.Food)]
        [TestCase(VoyageSupplyType.Medicine)]
        [TestCase(VoyageSupplyType.Wood)]
        [TestCase(VoyageSupplyType.Powder)]
        [TestCase(VoyageSupplyType.Cannonballs)]
        public void NegativeLineAfterValidLines_RejectsTheWholeOrder(VoyageSupplyType invalid)
        {
            var order = new VoyageSupplyOrder { Water = 10, Food = 10, Medicine = 1, Wood = 1, Powder = 1, Cannonballs = 1 };
            // Set the public property directly: the indexer intentionally clamps UI edits.
            switch (invalid)
            {
                case VoyageSupplyType.Water: order.Water = -1; break;
                case VoyageSupplyType.Food: order.Food = -1; break;
                case VoyageSupplyType.Medicine: order.Medicine = -1; break;
                case VoyageSupplyType.Wood: order.Wood = -1; break;
                case VoyageSupplyType.Powder: order.Powder = -1; break;
                case VoyageSupplyType.Cannonballs: order.Cannonballs = -1; break;
            }
            AssertRejectedWithoutMutation(new CoreVoyageSession(), order, 40);
        }

        [TestCase(VoyageSupplyType.Water)]
        [TestCase(VoyageSupplyType.Food)]
        [TestCase(VoyageSupplyType.Medicine)]
        [TestCase(VoyageSupplyType.Wood)]
        [TestCase(VoyageSupplyType.Powder)]
        [TestCase(VoyageSupplyType.Cannonballs)]
        public void QuantityAdditionOverflow_DoesNotWrapOrMutateOtherLines(VoyageSupplyType invalid)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.GetSupplyAmount(invalid), Is.GreaterThan(0));
            var order = new VoyageSupplyOrder { Water = 10, Food = 10, Medicine = 1, Wood = 1, Powder = 1, Cannonballs = 1 };
            order[invalid] = int.MaxValue;
            AssertRejectedWithoutMutation(session, order, 40);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MaxValue)]
        public void InvalidTotalCost_DoesNotChangeTheAccountOrCargo(int cost)
        {
            AssertRejectedWithoutMutation(new CoreVoyageSession(), new VoyageSupplyOrder { Water = 10 }, cost);
        }

        [Test]
        public void EmptyOrMissingOrder_DoesNotSpend()
        {
            var session = new CoreVoyageSession();
            AssertRejectedWithoutMutation(session, null, 40);
            AssertRejectedWithoutMutation(session, new VoyageSupplyOrder(), 40);
        }

        [Test]
        public void PurchaseAtSea_DoesNotChangeTheAccountOrCargo()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            AssertRejectedWithoutMutation(session, new VoyageSupplyOrder { Water = 10, Food = 10 }, 40);
        }

        private static void AssertRejectedWithoutMutation(CoreVoyageSession session, VoyageSupplyOrder order, decimal cost)
        {
            decimal[] before = MoneyAndCargo(session);
            Assert.That(session.TryPurchaseMarketSupplyOrder(order, cost), Is.False);
            Assert.That(MoneyAndCargo(session), Is.EqualTo(before), "A rejected order must preserve every resource and profile gold.");
        }

        private static int[] SupplyAmounts(CoreVoyageSession session) => new[]
        {
            session.Cargo.Water, session.Cargo.Food, session.Cargo.Medicine,
            session.Cargo.Wood, session.Cargo.Powder, session.Cargo.Cannonballs,
        };

        private static decimal[] MoneyAndCargo(CoreVoyageSession session)
        {
            VoyageCargo cargo = session.Cargo;
            return new[] { session.ProfileGold, cargo.Water, cargo.Food, cargo.Beer, cargo.Medicine,
                cargo.Wood, cargo.Iron, cargo.Cloth, cargo.Powder, cargo.Cannonballs, cargo.Gold, cargo.Fame,
                cargo.Gem, cargo.AncientMaterial, cargo.Relic, cargo.BlackPearl, cargo.AncientCompass, cargo.Lantern, cargo.Curse };
        }
    }
}
