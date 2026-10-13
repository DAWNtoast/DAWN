using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreMarketBasicSupplyPurchaseTests
    {
        [Test]
        public void NineBasicItemsLoadTogetherAndDebitTheExactCombinedQuote()
        {
            var session = new CoreVoyageSession();
            Dictionary<string, int> order = SmallNineItemOrder();
            decimal gold = session.ProfileGold;
            int[] before = BasicAmounts(session);
            int rare = session.Cargo.Gem;

            Assert.That(session.TryPurchaseMarketBasicSupplies(order, 12.3456m), Is.True);

            Assert.That(session.ProfileGold, Is.EqualTo(gold - 12.3456m));
            for (int index = 0; index < CoreWarehouseItemCatalog.BasicItems.Count; index++)
            {
                string key = CoreWarehouseItemCatalog.BasicItems[index].Key;
                Assert.That(session.GetMarketCargoAmount(key), Is.EqualTo(before[index] + order[key]), key);
            }
            Assert.That(order["water"], Is.EqualTo(3), "Committing must preserve the caller's order.");
            Assert.That(order["food"], Is.EqualTo(4));
            Assert.That(order.Count, Is.EqualTo(9));
            Assert.That(session.Cargo.Gem, Is.EqualTo(rare));
        }

        [TestCase("water")]
        [TestCase("food")]
        [TestCase("beer")]
        [TestCase("medicine")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase("cloth")]
        [TestCase("powder")]
        [TestCase("cannonballs")]
        public void NegativePurchaseLineCannotSellCargoOrPartiallyBuyOtherLines(string invalid)
        {
            Dictionary<string, int> order = SmallNineItemOrder();
            order[invalid] = -1;
            AssertRejectedWithoutMutation(new CoreVoyageSession(), order, 20m);
        }

        [TestCase("water")]
        [TestCase("food")]
        [TestCase("beer")]
        [TestCase("medicine")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase("cloth")]
        [TestCase("powder")]
        [TestCase("cannonballs")]
        public void QuantityAdditionOverflowRejectsEveryLineBeforePayment(string invalid)
        {
            var session = new CoreVoyageSession();
            SetCargoAmount(session.Cargo, invalid, 1);
            Dictionary<string, int> order = SmallNineItemOrder();
            order[invalid] = int.MaxValue;
            AssertRejectedWithoutMutation(session, order, 20m);
        }

        [Test]
        public void CombinedHoldLimitRejectsIndividuallyFittingWaterAndFood()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = session.Cargo.Food = 0;
            float otherVolume = session.Cargo.UsedVolume - session.Cargo.Wood;
            session.Cargo.Wood = (int)(session.CargoCapacityValue - otherVolume - 10f);
            Assert.That(session.Cargo.UsedVolume + 100 * .07d, Is.LessThanOrEqualTo(session.CargoCapacityValue));
            Assert.That(session.Cargo.UsedVolume + 100 * .09d, Is.LessThanOrEqualTo(session.CargoCapacityValue));
            AssertRejectedWithoutMutation(session, new Dictionary<string, int> { ["water"] = 100, ["food"] = 100 }, 20m);
        }

        [Test]
        public void CombinedCostAboveFundsRejectsTheAffordableFirstLine()
        {
            var session = new CoreVoyageSession();
            decimal lineCost = session.ProfileGold / 2m + .0001m;
            Assert.That(lineCost, Is.LessThan(session.ProfileGold));
            AssertRejectedWithoutMutation(session, new Dictionary<string, int> { ["water"] = 1, ["food"] = 1 }, lineCost * 2m);
        }

        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1.00001")]
        [TestCase("2147483648")]
        public void InvalidPaymentDoesNotSpendOrLoad(string cost)
        {
            AssertRejectedWithoutMutation(new CoreVoyageSession(), SmallNineItemOrder(),
                decimal.Parse(cost, CultureInfo.InvariantCulture));
        }

        [Test]
        public void EmptyOrMissingOrderDoesNotSpend()
        {
            var session = new CoreVoyageSession();
            AssertRejectedWithoutMutation(session, null, 1m);
            AssertRejectedWithoutMutation(session, new Dictionary<string, int>(), 1m);
            AssertRejectedWithoutMutation(session, new Dictionary<string, int> { ["water"] = 0 }, 1m);
        }

        [Test]
        public void PurchaseAtSeaDoesNotSpendOrLoad()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            AssertRejectedWithoutMutation(session, SmallNineItemOrder(), 20m);
        }

        [Test]
        public void InvalidExistingCargoRejectsTheWholeOrder()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Cloth = -1;
            AssertRejectedWithoutMutation(session, SmallNineItemOrder(), 20m);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void InvalidPhysicalItemVolumeRejectsBeforeAnyMutation(double invalidVolume)
        {
            CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.Find("cannonballs");
            FieldInfo field = typeof(CoreWarehouseItemDefinition).GetField("<UnitVolume>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            double original = item.UnitVolume;
            try
            {
                field.SetValue(item, invalidVolume);
                AssertRejectedWithoutMutation(new CoreVoyageSession(), SmallNineItemOrder(), 20m);
            }
            finally
            {
                field.SetValue(item, original);
            }
        }

        [Test]
        public void RareOrUnknownCargoRejectsAllOtherwiseValidLines()
        {
            Dictionary<string, int> order = SmallNineItemOrder();
            order["gem"] = 1;
            AssertRejectedWithoutMutation(new CoreVoyageSession(), order, 20m);
            order.Remove("gem");
            order["unknown"] = 1;
            AssertRejectedWithoutMutation(new CoreVoyageSession(), order, 20m);
        }

        [Test]
        public void DuplicateNormalizedKeysRejectInsteadOfChargingOrLoadingTwice()
        {
            Dictionary<string, int> order = SmallNineItemOrder();
            order[" WATER "] = 3;
            AssertRejectedWithoutMutation(new CoreVoyageSession(), order, 20m);
        }

        [Test]
        public void APartialOrderAcceptsNormalizedKeysAndLeavesMissingLinesUnchanged()
        {
            var session = new CoreVoyageSession();
            int iron = session.Cargo.Iron;
            int water = session.Cargo.Water;
            int food = session.Cargo.Food;
            Assert.That(session.TryPurchaseMarketBasicSupplies(
                new Dictionary<string, int> { [" IRON "] = 1 }, .1234m), Is.True);
            Assert.That(session.Cargo.Iron, Is.EqualTo(iron + 1));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
        }

        private static Dictionary<string, int> SmallNineItemOrder() => new Dictionary<string, int>
        {
            ["water"] = 3, ["food"] = 4, ["beer"] = 1, ["medicine"] = 1, ["wood"] = 1,
            ["iron"] = 1, ["cloth"] = 1, ["powder"] = 1, ["cannonballs"] = 1,
        };

        private static void AssertRejectedWithoutMutation(CoreVoyageSession session, IReadOnlyDictionary<string, int> order, decimal cost)
        {
            decimal[] before = MoneyAndCargo(session);
            Assert.That(session.TryPurchaseMarketBasicSupplies(order, cost), Is.False);
            Assert.That(MoneyAndCargo(session), Is.EqualTo(before),
                "A rejected order must preserve every cargo amount and profile gold.");
        }

        private static int[] BasicAmounts(CoreVoyageSession session)
        {
            var amounts = new int[CoreWarehouseItemCatalog.BasicItems.Count];
            for (int index = 0; index < amounts.Length; index++)
                amounts[index] = session.GetMarketCargoAmount(CoreWarehouseItemCatalog.BasicItems[index].Key);
            return amounts;
        }

        private static void SetCargoAmount(VoyageCargo cargo, string key, int amount)
        {
            string property = char.ToUpperInvariant(key[0]) + key.Substring(1);
            typeof(VoyageCargo).GetProperty(property).SetValue(cargo, amount);
        }

        private static decimal[] MoneyAndCargo(CoreVoyageSession session)
        {
            VoyageCargo cargo = session.Cargo;
            return new[] { session.ProfileGold, cargo.Water, cargo.Food, cargo.Beer, cargo.Medicine,
                cargo.Wood, cargo.Iron, cargo.Cloth, cargo.Powder, cargo.Cannonballs, cargo.Gold, cargo.Fame,
                cargo.Gem, cargo.AncientMaterial, cargo.Relic, cargo.BlackPearl, cargo.AncientCompass, cargo.Lantern, cargo.Curse };
        }
    }
}
