using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortInvestmentTests
    {
        private static readonly PropertyInfo[] CargoProperties = typeof(VoyageCargo).GetProperties()
            .Where(property => property.CanWrite && property.PropertyType == typeof(int)).ToArray();

        private static IEnumerable<string> CargoPropertyNames => CargoProperties.Select(property => property.Name);

        [Test]
        public void MultipleDeliveriesAccumulateEnoughMaterialsForAnInvestmentLargerThanShipCapacity()
        {
            CoreVoyageSession session = Ready();
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 0);
            for (int delivery = 0; delivery < 3; delivery++)
            {
                // Each delivered load fits the starter ship; the combined
                // investment materials do not have to fit aboard together.
                session.Cargo.Wood = 30; session.Cargo.Iron = 10; session.Cargo.Beer = 10;
                Assert.That(session.Cargo.UsedVolume, Is.LessThan(session.CargoCapacityValue));
                Assert.That(session.TryDepositPortCargo("wood", 30), Is.True);
                Assert.That(session.TryDepositPortCargo("iron", 10), Is.True);
                Assert.That(session.TryDepositPortCargo("beer", 10), Is.True);
            }
            Assert.That(80 + 25 * 2 + 25 * .08f, Is.GreaterThan(session.CargoCapacityValue));
            Assert.That(session.TrySpendPortInvestment(120, 80, 25, "beer", 25), Is.True);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(10));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "iron"), Is.EqualTo(5));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "beer"), Is.EqualTo(5));
            Assert.That(session.Cargo.UsedVolume, Is.Zero);
        }

        [Test]
        public void InvestmentUsesLocalStorageFirstThenOnlyTheMissingCargoAndNeverOtherPorts()
        {
            CoreVoyageSession session = Ready();
            session.PortWarehouse.Restore("v1;断锚港,wood,4;断锚港,iron,2;断锚港,beer,3;白贝港,wood,100;白贝港,iron,100;白贝港,beer,100");
            Assert.That(session.GetPortInvestmentMaterialAmount(" WOOD "), Is.EqualTo(24L));
            Assert.That(session.TrySpendPortInvestment(120, 6, 4, "beer", 6), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(18));
            Assert.That(session.Cargo.Iron, Is.EqualTo(18));
            Assert.That(session.Cargo.Beer, Is.EqualTo(17));
            foreach (string item in new[] { "wood", "iron", "beer" })
            {
                Assert.That(session.PortWarehouse.GetAmount("断锚港", item), Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount("白贝港", item), Is.EqualTo(100));
            }
        }

        [Test]
        public void OtherPortsStockCannotPayAnInvestmentOrHideAShortfall()
        {
            CoreVoyageSession session = Ready();
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Beer = 0;
            session.PortWarehouse.Restore("v1;白贝港,wood,100;白贝港,iron,100;白贝港,beer,100;断锚港,wood,6;断锚港,iron,4;断锚港,beer,5");
            Assert.That(session.GetPortInvestmentMaterialAmount("beer"), Is.EqualTo(5L));
            AssertRejectedWithoutChanges(session, 120, 6, 4, "beer", 6);
            session.RestoreProfile(session.ProfileGold, "白贝港");
            Assert.That(session.TrySpendPortInvestment(120, 6, 4, "beer", 6), Is.True);
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "beer"), Is.EqualTo(5));
        }

        [TestCase("gold")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase("medicine")]
        public void MixedBalancesAreNotPartiallyConsumedWhenAnyRequirementIsShort(string missing)
        {
            CoreVoyageSession session = Ready();
            session.PortWarehouse.Restore("v1;断锚港,wood,1;断锚港,iron,1;断锚港,medicine,1");
            if (missing == "gold") session.RestoreProfile(119, session.CurrentPort);
            else if (missing == "wood") session.Cargo.Wood = 4;
            else if (missing == "iron") session.Cargo.Iron = 2;
            else session.Cargo.Medicine = 7;
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [Test]
        public void AnUnknownPortCannotSpendCargoOrAnyKnownPortsStock()
        {
            CoreVoyageSession session = Ready();
            session.PortWarehouse.Restore("v1;断锚港,wood,100;断锚港,iron,100;断锚港,medicine,100");
            session.RestoreProfile(session.ProfileGold, "未知港");
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [Test]
        public void LargeCombinedBalancesDoNotOverflowAndPreferStoredMaterials()
        {
            CoreVoyageSession session = Ready();
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Beer = int.MaxValue;
            session.PortWarehouse.Restore("v1;断锚港,wood,2147483647;断锚港,iron,2147483647;断锚港,beer,2147483647");
            Assert.That(session.GetPortInvestmentMaterialAmount("wood"), Is.EqualTo(2L * int.MaxValue));
            Assert.That(session.TrySpendPortInvestment(120, int.MaxValue, int.MaxValue, "beer", int.MaxValue), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(int.MaxValue));
            Assert.That(session.Cargo.Iron, Is.EqualTo(int.MaxValue));
            Assert.That(session.Cargo.Beer, Is.EqualTo(int.MaxValue));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo("v2"));
        }

        [TestCase("beer", "啤酒")]
        [TestCase("medicine", "药品")]
        [TestCase("cloth", "帆布")]
        [TestCase("powder", "火药")]
        [TestCase("cannonballs", "炮弹")]
        [TestCase("water", "淡水")]
        [TestCase("food", "食物")]
        public void ValidInvestment_NormalizesItemAndSpendsExactlyTheFourRequiredBalances(string item, string name)
        {
            CoreVoyageSession session = Ready();
            Dictionary<string, int> cargoBefore = CaptureCargo(session);
            decimal goldBefore = session.ProfileGold;

            Assert.That(session.TrySpendPortInvestment(120, 6, 4, " \t" + item.ToUpperInvariant() + " ", 9), Is.True);

            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore - 120));
            foreach (PropertyInfo property in CargoProperties)
            {
                int cost = property.Name == "Wood" ? 6 : property.Name == "Iron" ? 4 :
                    property.Name.Equals(item, StringComparison.OrdinalIgnoreCase) ? 9 : 0;
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(cargoBefore[property.Name] - cost), property.Name);
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.LogEntries.Last(), Does.Contain("港口投资").And.Contain("金币 -120")
                .And.Contain("木材 -6").And.Contain("铁矿 -4").And.Contain(name + " -9"));
        }

        [Test]
        public void ExactBalancesAreAcceptedAndARepeatCannotSpendAgain()
        {
            CoreVoyageSession session = Ready();
            session.RestoreProfile(120, session.CurrentPort);
            session.Cargo.Wood = 6;
            session.Cargo.Iron = 4;
            session.Cargo.Medicine = 9;
            Assert.That(session.TrySpendPortInvestment(120, 6, 4, "medicine", 9), Is.True);
            Assert.That(session.ProfileGold, Is.Zero);
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.Cargo.Iron, Is.Zero);
            Assert.That(session.Cargo.Medicine, Is.Zero);
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [TestCase(0, 6, 4, 9)]
        [TestCase(-1, 6, 4, 9)]
        [TestCase(int.MinValue, 6, 4, 9)]
        [TestCase(120, 0, 4, 9)]
        [TestCase(120, -1, 4, 9)]
        [TestCase(120, int.MinValue, 4, 9)]
        [TestCase(120, 6, 0, 9)]
        [TestCase(120, 6, -1, 9)]
        [TestCase(120, 6, int.MinValue, 9)]
        [TestCase(120, 6, 4, 0)]
        [TestCase(120, 6, 4, -1)]
        [TestCase(120, 6, 4, int.MinValue)]
        [TestCase(int.MaxValue, 6, 4, 9)]
        [TestCase(120, int.MaxValue, 4, 9)]
        [TestCase(120, 6, int.MaxValue, 9)]
        [TestCase(120, 6, 4, int.MaxValue)]
        public void NonPositiveOrUnaffordableExtremeCostsDoNotSpendAnyBalance(int gold, int wood, int iron, int quantity)
        {
            AssertRejectedWithoutChanges(Ready(), gold, wood, iron, "medicine", quantity);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        [TestCase("unknown")]
        [TestCase("gold")]
        [TestCase("gem")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase(" WoOd ")]
        [TestCase(" IRON ")]
        public void UnknownOrDuplicateMaterialKeysCannotCreateAPartialInvestment(string item)
        {
            AssertRejectedWithoutChanges(Ready(), 120, 6, 4, item, 9);
        }

        [TestCase("gold")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase("medicine")]
        public void BeingOneUnitShortOfAnyRequirementPreservesAllOtherAssets(string missing)
        {
            CoreVoyageSession session = Ready();
            if (missing == "gold") session.RestoreProfile(119, session.CurrentPort);
            else if (missing == "wood") session.Cargo.Wood = 5;
            else if (missing == "iron") session.Cargo.Iron = 3;
            else session.Cargo.Medicine = 8;
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void InvestmentIsRejectedInEveryNonPortPhase(CoreDemoPhase phase)
        {
            CoreVoyageSession session = Ready();
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [TestCaseSource(nameof(CargoPropertyNames))]
        public void AnyNegativeCargoBalancePreventsTheWholeInvestment(string resource)
        {
            CoreVoyageSession session = Ready();
            CargoProperties.Single(property => property.Name == resource).SetValue(session.Cargo, -1);
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [Test]
        public void AboardCashCannotSubstituteForAnInsufficientPortAccount()
        {
            CoreVoyageSession session = Ready();
            session.RestoreProfile(119, session.CurrentPort);
            session.Cargo.Gold = 10000;
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9);
        }

        [Test]
        public void ValidIntegerLimitBalancesCanBeSpentWithoutWrappingOrDiscardingOtherCargo()
        {
            CoreVoyageSession session = Ready();
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Medicine = int.MaxValue;
            Dictionary<string, int> cargoBefore = CaptureCargo(session);

            Assert.That(session.TrySpendPortInvestment(int.MaxValue, int.MaxValue, int.MaxValue,
                "medicine", int.MaxValue), Is.True);

            Assert.That(session.ProfileGold, Is.Zero);
            foreach (PropertyInfo property in CargoProperties)
            {
                bool spent = property.Name == "Wood" || property.Name == "Iron" || property.Name == "Medicine";
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(spent ? 0 : cargoBefore[property.Name]), property.Name);
            }
        }

        [TestCase("gem")]
        [TestCase("ancientmaterial")]
        [TestCase("relic")]
        [TestCase("blackpearl")]
        [TestCase("ancientcompass")]
        [TestCase("lantern")]
        public void RareDonationUsesOnlyLocalWarehouseThenCarriedRemainder(string key)
        {
            CoreVoyageSession session = Ready();
            Assert.That(session.PortWarehouse.Restore("v2;断锚港," + key + ",2;白贝港," + key + ",100"), Is.True);
            Assert.That(session.GetPortInvestmentMaterialAmount(key), Is.EqualTo(22L));
            Dictionary<string, int> before = CaptureCargo(session);
            Assert.That(session.TrySpendPortInvestment(120, 6, 4, "medicine", 9,
                new[] { new CorePortInvestmentRequirement(" " + key.ToUpperInvariant() + " ", 3) }), Is.True);
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(19));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", key), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("白贝港", key), Is.EqualTo(100));
            foreach (PropertyInfo property in CargoProperties)
            {
                int spent = property.Name == "Wood" ? 6 : property.Name == "Iron" ? 4 : property.Name == "Medicine" ? 9 :
                    property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(before[property.Name] - spent), property.Name);
            }
        }

        [Test]
        public void MissingSecondRareDonationCannotSpendTheFirstOrCommonCostsOrForeignStock()
        {
            CoreVoyageSession session = Ready();
            session.Cargo.Gem = 0; session.Cargo.Relic = 0;
            session.PortWarehouse.Restore("v2;断锚港,gem,2;白贝港,relic,1");
            AssertRejectedWithoutChanges(session, 120, 6, 4, "medicine", 9, new[]
            {
                new CorePortInvestmentRequirement("gem", 2), new CorePortInvestmentRequirement("relic", 1),
            });
        }

        [TestCase("wood", 1)]
        [TestCase("medicine", 1)]
        [TestCase("curse", 1)]
        [TestCase("gold", 1)]
        [TestCase("unknown", 1)]
        [TestCase(null, 1)]
        [TestCase("relic", 0)]
        [TestCase("relic", -1)]
        [TestCase("relic", int.MinValue)]
        public void InvalidRareRequirementRejectsEveryBalance(string key, int quantity)
        {
            AssertRejectedWithoutChanges(Ready(), 120, 6, 4, "medicine", 9, new[]
            {
                new CorePortInvestmentRequirement("gem", 1), new CorePortInvestmentRequirement(key, quantity),
            });
        }

        [Test]
        public void DuplicateRareKeysAndOverflowSizedRequirementsCannotCausePartialDebits()
        {
            AssertRejectedWithoutChanges(Ready(), 120, 6, 4, "medicine", 9, new[]
            {
                new CorePortInvestmentRequirement("gem", 15), new CorePortInvestmentRequirement(" GEM ", 15),
            });
            CoreVoyageSession session = Ready();
            session.Cargo.Gem = int.MaxValue;
            session.PortWarehouse.Restore("v2;断锚港,gem,2147483647");
            Assert.That(session.GetPortInvestmentMaterialAmount("gem"), Is.EqualTo(2L * int.MaxValue));
            Assert.That(session.TrySpendPortInvestment(120, 6, 4, "medicine", 9,
                new[] { new CorePortInvestmentRequirement("gem", int.MaxValue) }), Is.True);
            Assert.That(session.Cargo.Gem, Is.EqualTo(int.MaxValue));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "gem"), Is.Zero);
        }

        [Test]
        public void LegacyBasicWarehouseCanFundEarlyInvestmentWithoutAnyRareDonation()
        {
            CoreVoyageSession session = Ready();
            session.Cargo.Gem = session.Cargo.AncientMaterial = session.Cargo.Relic = session.Cargo.BlackPearl =
                session.Cargo.AncientCompass = session.Cargo.Lantern = 0;
            session.PortWarehouse.Restore("v1;断锚港,wood,6;断锚港,iron,4;断锚港,medicine,9");
            Assert.That(session.TrySpendPortInvestment(120, 6, 4, "medicine", 9), Is.True);
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo("v2"));
        }

        private static CoreVoyageSession Ready()
        {
            var session = new CoreVoyageSession();
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 20);
            return session;
        }

        private static Dictionary<string, int> CaptureCargo(CoreVoyageSession session) =>
            CargoProperties.ToDictionary(property => property.Name, property => (int)property.GetValue(session.Cargo));

        private static void AssertRejectedWithoutChanges(CoreVoyageSession session, int gold, int wood, int iron,
            string requiredItem, int requiredQuantity, IReadOnlyList<CorePortInvestmentRequirement> rareMaterials = null)
        {
            decimal accountBefore = session.ProfileGold;
            CoreDemoPhase phaseBefore = session.Phase;
            string[] logBefore = session.LogEntries.ToArray();
            string warehouseBefore = session.PortWarehouse.Serialize();
            Dictionary<string, int> cargoBefore = CaptureCargo(session);
            Assert.That(session.TrySpendPortInvestment(gold, wood, iron, requiredItem, requiredQuantity, rareMaterials), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(accountBefore));
            Assert.That(session.Phase, Is.EqualTo(phaseBefore));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore));
            foreach (PropertyInfo property in CargoProperties)
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(cargoBefore[property.Name]), property.Name);
            CollectionAssert.AreEqual(logBefore, session.LogEntries);
        }
    }
}
