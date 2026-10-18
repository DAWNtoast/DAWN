using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreMarketCargoMappingTests
    {
        [TestCase("water", 0, "淡水")]
        [TestCase("food", 1, "食物")]
        [TestCase("beer", 2, "啤酒")]
        [TestCase("medicine", 3, "药品")]
        [TestCase("wood", 4, "木材")]
        [TestCase("iron", 5, "铁矿")]
        [TestCase("cloth", 6, "帆布")]
        [TestCase("powder", 7, "火药")]
        [TestCase("cannonballs", 8, "炮弹")]
        public void BasicMarketTransactionsChangeOnlyTheirMatchingCargoAndKeepTheirLabels(string key, int index, string name)
        {
            var session = new CoreVoyageSession();
            string input = " " + key.ToUpperInvariant() + " ";
            int[] expected = CargoAmounts(session.Cargo);
            decimal gold = session.ProfileGold;
            Assert.That(session.GetMarketCargoAmount(input), Is.EqualTo(expected[index]));

            Assert.That(session.TryPurchaseMarketCargo(input, 2, 1.2345m, 0f), Is.True);
            expected[index] += 2;
            Assert.That(CargoAmounts(session.Cargo), Is.EqualTo(expected), "Purchases must change only the selected material.");
            Assert.That(session.GetMarketCargoAmount(input), Is.EqualTo(expected[index]));
            Assert.That(session.ProfileGold, Is.EqualTo(gold - 1.2345m));
            StringAssert.StartsWith("港口市场采购" + name + " ×2", session.LogEntries[session.LogEntries.Count - 1]);

            Assert.That(session.TrySellMarketCargo(input, 1, .2345m), Is.True);
            expected[index] -= 1;
            Assert.That(CargoAmounts(session.Cargo), Is.EqualTo(expected), "Sales must change only the selected material.");
            Assert.That(session.GetMarketCargoAmount(input), Is.EqualTo(expected[index]));
            Assert.That(session.ProfileGold, Is.EqualTo(gold - 1m));
            StringAssert.StartsWith("港口市场出售" + name + " ×1", session.LogEntries[session.LogEntries.Count - 1]);
        }

        [TestCase("gem")]
        [TestCase("ancientmaterial")]
        [TestCase("relic")]
        [TestCase("blackpearl")]
        [TestCase("ancientcompass")]
        [TestCase("lantern")]
        [TestCase("gold")]
        [TestCase("fame")]
        [TestCase("curse")]
        [TestCase("unknown")]
        [TestCase("")]
        [TestCase(null)]
        public void NonMarketItemsCannotUseBasicMarketPricesOrChangeAnyAssets(string key)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Gem = session.Cargo.AncientMaterial = session.Cargo.Relic = 1;
            session.Cargo.BlackPearl = session.Cargo.AncientCompass = session.Cargo.Lantern = 1;
            session.Cargo.Gold = session.Cargo.Fame = session.Cargo.Curse = 1;
            string input = key == null ? null : " " + key.ToUpperInvariant() + " ";
            int[] before = CargoAmounts(session.Cargo);
            decimal gold = session.ProfileGold;
            string warehouse = session.PortWarehouse.Serialize();
            int logCount = session.LogEntries.Count;

            Assert.That(session.GetMarketCargoAmount(input), Is.Zero);
            Assert.That(session.TryPurchaseMarketCargo(input, 1, 1m, 0f), Is.False);
            Assert.That(session.TrySellMarketCargo(input, 1, 1m), Is.False);
            Assert.That(session.TryPurchasePortWarehouseCargo(input, 1, 1m), Is.False);
            Assert.That(CargoAmounts(session.Cargo), Is.EqualTo(before));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            Assert.That(session.LogEntries.Count, Is.EqualTo(logCount));
        }

        private static int[] CargoAmounts(VoyageCargo cargo) => new[]
        {
            cargo.Water, cargo.Food, cargo.Beer, cargo.Medicine, cargo.Wood, cargo.Iron, cargo.Cloth,
            cargo.Powder, cargo.Cannonballs, cargo.Gold, cargo.Fame, cargo.Gem, cargo.AncientMaterial,
            cargo.Relic, cargo.BlackPearl, cargo.AncientCompass, cargo.Lantern, cargo.Curse,
        };
    }
}
