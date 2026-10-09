using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreGameMasterToolsTests
    {
        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("00042", 42)]
        [TestCase("1000000000", CoreGameMasterTools.MaximumProfileGold)]
        public void ParseGold_AcceptsOnlyExactSupportedAmounts(string input, int expected)
        {
            Assert.That(CoreGameMasterTools.TryParseProfileGold(input, out decimal gold), Is.True);
            Assert.That(gold, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase(" 100")]
        [TestCase("100 ")]
        [TestCase("1\n")]
        [TestCase("1.00001")]
        [TestCase("1,000")]
        [TestCase("1e3")]
        [TestCase("１２３")]
        [TestCase("1000000001")]
        [TestCase("2147483647")]
        [TestCase("9999999999999999999999")]
        public void ParseGold_RejectsMalformedOrOutOfRangeAmounts(string input)
        {
            Assert.That(CoreGameMasterTools.TryParseProfileGold(input, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(CoreGameMasterTools.MaximumProfileGold)]
        public void SetGold_InPortChangesOnlyProfileGold(int gold)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(1234, "灰鸥港");
            var cargo = session.Cargo;
            int cargoGold = cargo.Gold;
            int water = cargo.Water;
            int ship = session.SelectedShipIndex;
            Assert.That(session.TryGameMasterSetProfileGold(gold), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo, Is.SameAs(cargo));
            Assert.That(cargo.Gold, Is.EqualTo(cargoGold));
            Assert.That(cargo.Water, Is.EqualTo(water));
            Assert.That(session.SelectedShipIndex, Is.EqualTo(ship));
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void SetGold_RejectsEveryNonPortPhase(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            decimal gold = session.ProfileGold;
            typeof(CoreVoyageSession).GetProperty("Phase", BindingFlags.Public | BindingFlags.Instance).SetValue(session, phase);
            Assert.That(session.TryGameMasterSetProfileGold(0), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [TestCase(-1)]
        [TestCase(1000000001)]
        [TestCase(int.MaxValue)]
        public void SetGold_RejectsOutOfRangeWithoutClamping(int requested)
        {
            var session = new CoreVoyageSession();
            decimal before = session.ProfileGold;
            Assert.That(session.TryGameMasterSetProfileGold(requested), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(before));
        }

        [Test]
        public void RevisionZero_PreservesLegacyStockAndPurchaseIds()
        {
            var art = new ComponentArtCatalog();
            var date = new DateTime(2026, 9, 13);
            var legacy = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date);
            var explicitZero = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 0, "ignored-until-refresh");
            CollectionAssert.AreEqual(legacy.Select(Describe), explicitZero.Select(Describe));
            Assert.That(explicitZero.All(item => !item.Id.Contains("gm-") && item.RefreshRevision == 0), Is.True);
        }

        [Test]
        public void Refresh_ChangesBatchIdsWithoutChangingClockOrOtherPorts()
        {
            var art = new ComponentArtCatalog();
            var date = new DateTime(2026, 9, 13);
            var original = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date);
            var otherPort = PortComponentShopCatalog.BuildStock(art, "白贝港", date);
            var refreshed = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "save-a");
            var restored = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "save-a");
            CollectionAssert.AreEqual(refreshed.Select(Describe), restored.Select(Describe), "Reopening/restoring must not reroll a batch.");
            Assert.That(refreshed.Count, Is.EqualTo(PortComponentShopCatalog.StockCount(0)));
            Assert.That(original.Select(item => item.Id).Intersect(refreshed.Select(item => item.Id)), Is.Empty);
            Assert.That(refreshed.All(item => item.RefreshKey == PortComponentShopCatalog.MonthKey(date) && item.RefreshRevision == 1), Is.True);
            CollectionAssert.AreEqual(otherPort.Select(Describe), PortComponentShopCatalog.BuildStock(art, "白贝港", date).Select(Describe));
        }

        [Test]
        public void Refresh_InvalidatesOldCallbacksAndNewGamesHaveIndependentReceipts()
        {
            var art = new ComponentArtCatalog();
            var date = new DateTime(2026, 9, 13);
            var original = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 0)[0];
            var first = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "old-save")[0];
            var nextSave = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "new-save")[0];
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(original, "灰鸥港", date, 1, "old-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date, 1, "old-save"), Is.True);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date, 2, "old-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date, 1, "new-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "白贝港", date, 1, "old-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date.AddMonths(1), 1, "old-save"), Is.False);
            Assert.That(first.Id, Is.Not.EqualTo(nextSave.Id));
            Assert.That(CoreGameplayDemoController.ComponentShopPurchaseKey(first.Id),
                Is.Not.EqualTo(CoreGameplayDemoController.ComponentShopPurchaseKey(nextSave.Id)));
        }

        [Test]
        public void NegativeRevision_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PortComponentShopCatalog.BuildStock(new ComponentArtCatalog(), "灰鸥港", new DateTime(2026, 9, 1), -1));
        }

        private static string Describe(PortComponentShopStockItem item)
        {
            return item.Id + ":" + item.Entry.Id + ":" + item.Quality + ":" + item.Price;
        }
    }
}
