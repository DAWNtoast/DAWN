using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortWarehouseTests
    {
        private static readonly string[] Materials =
            { "water", "food", "beer", "medicine", "wood", "iron", "cloth", "powder", "cannonballs" };

        [Test]
        public void SerializationRoundTripsAllSixPortsAndNineGoodsWithoutMergingPorts()
        {
            var records = new List<string> { "v1" };
            int amount = 0;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (string item in Materials) records.Add(port + "," + item + "," + ++amount);
            var warehouse = new CorePortWarehouse();
            Assert.That(warehouse.Restore(string.Join(";", records)), Is.True);
            string saved = warehouse.Serialize();
            var restored = new CorePortWarehouse();
            Assert.That(restored.Restore(saved), Is.True);
            amount = 0;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (string item in Materials) Assert.That(restored.GetAmount(port, item), Is.EqualTo(++amount));
            Assert.That(restored.Serialize(), Is.EqualTo(saved));
            Assert.That(restored.Restore(saved), Is.True);
            Assert.That(restored.Serialize(), Is.EqualTo(saved), "Reloading replaces stock rather than adding it again.");
        }

        [TestCase("garbage")]
        [TestCase("v3;断锚港,wood,10")]
        [TestCase("v1;")]
        [TestCase("v1;未知港,wood,10")]
        [TestCase("v1;断锚港,gem,10")]
        [TestCase("v1;断锚港,wood,-1")]
        [TestCase("v1;断锚港,wood,2147483648")]
        [TestCase("v1;断锚港,wood,999999999999999999999")]
        [TestCase("v1;断锚港,wood,1.5")]
        [TestCase("v1;断锚港,wood,1e3")]
        [TestCase("v1;断锚港,wood,+1")]
        [TestCase("v1;断锚港,wood")]
        [TestCase("v1;断锚港,wood,1,2")]
        [TestCase("v1;断锚港,wood,10;断锚港,wood,10")]
        [TestCase("v1;断锚港,wood,10; 断锚港 , WOOD ,20")]
        [TestCase("v1;断锚港,wood,0;断锚港,wood,1")]
        [TestCase("v1;白贝港,food,20;断锚港,wood,-1")]
        public void MalformedSaveRejectsTheEntireReplacementWithoutLosingExistingStock(string save)
        {
            var warehouse = new CorePortWarehouse();
            Assert.That(warehouse.Restore("v1;断锚港,wood,7;白贝港,food,11"), Is.True);
            string before = warehouse.Serialize();
            Assert.That(warehouse.Restore(save), Is.False);
            Assert.That(warehouse.Serialize(), Is.EqualTo(before));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("v1")]
        public void EmptySaveExplicitlyRestoresAnEmptyWarehouse(string save)
        {
            var warehouse = new CorePortWarehouse();
            warehouse.Restore("v1;断锚港,wood,7");
            Assert.That(warehouse.Restore(save), Is.True);
            Assert.That(warehouse.Serialize(), Is.EqualTo("v2"));
        }

        [Test]
        public void QueriesNormalizeKnownKeysAndCannotAccessInventedPortsOrCargo()
        {
            var warehouse = new CorePortWarehouse();
            Assert.That(warehouse.Restore("v1; 断锚港 , WOOD ,2147483647;白贝港,food,0"), Is.True);
            Assert.That(warehouse.GetAmount(" 断锚港 ", " WOOD "), Is.EqualTo(int.MaxValue));
            Assert.That(warehouse.GetAmount("白贝港", "wood"), Is.Zero);
            Assert.That(warehouse.GetAmount("未知港", "wood"), Is.Zero);
            Assert.That(warehouse.GetAmount("断锚港", "gold"), Is.Zero);
            Assert.That(warehouse.GetAmount(null, null), Is.Zero);
            Assert.That(warehouse.Serialize(), Is.EqualTo("v2;断锚港,wood,2147483647"));
        }

        [Test]
        public void SerializationUsesInvariantNumbersAcrossLocales()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                var warehouse = new CorePortWarehouse();
                Assert.That(warehouse.Restore("v1;断锚港,water,1234567"), Is.True);
                Assert.That(warehouse.Serialize(), Is.EqualTo("v2;断锚港,water,1234567"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
