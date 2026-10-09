using System;
using System.Globalization;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ProfileGoldPersistenceTests
    {
        private bool hadLegacy, hadDecimal;
        private int legacy;
        private string exact;

        [SetUp]
        public void Preserve()
        {
            hadLegacy = PlayerPrefs.HasKey(CoreGameplayDemoController.ProfileGoldKey);
            hadDecimal = PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey);
            legacy = PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey);
            exact = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey);
            ProfileGoldPersistence.Reset();
        }

        [TearDown]
        public void Restore()
        {
            ProfileGoldPersistence.Reset();
            if (hadLegacy) PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, legacy);
            if (hadDecimal) PlayerPrefs.SetString(ProfileGoldPersistence.DecimalKey, exact);
        }

        [Test]
        public void LegacyIntegerSaveMigratesWithoutChangingBalance()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 675);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(675m));
            ProfileGoldPersistence.Save(ProfileGoldPersistence.Load());
            Assert.That(PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey), Is.EqualTo("675"));
        }

        [Test]
        public void DecimalBalanceRoundTripsUnderCommaDecimalCulture()
        {
            CultureInfo prior = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                ProfileGoldPersistence.Save(499.7312m);
                Assert.That(PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey), Is.EqualTo("499.7312"));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(499.7312m));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(499));
                Assert.That(PortMarketCatalog.FormatPrice(499.7300m), Is.EqualTo("499.73"));
            }
            finally { CultureInfo.CurrentCulture = prior; }
        }

        [Test]
        public void OldAboveCapAssetsRemainSpendableWithoutDataLoss()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, int.MaxValue);
            decimal balance = ProfileGoldPersistence.Load();
            Assert.That(balance, Is.EqualTo((decimal)int.MaxValue));
            ProfileGoldPersistence.Save(balance - .0123m);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(int.MaxValue - .0123m));
        }

        [TestCase("broken")]
        [TestCase("-1")]
        [TestCase("10.00001")]
        [TestCase("2147483648")]
        public void InvalidDecimalRecordFallsBackToLegacyWithoutMutatingIt(string invalid)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 723);
            PlayerPrefs.SetString(ProfileGoldPersistence.DecimalKey, invalid);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(723m));
            Assert.That(PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey), Is.EqualTo(invalid));
        }

        [Test]
        public void InvalidSaveCannotOverwriteAnExistingBalance()
        {
            ProfileGoldPersistence.Save(18.25m);
            Assert.Throws<ArgumentOutOfRangeException>(() => ProfileGoldPersistence.Save(-1m));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProfileGoldPersistence.Save(.00001m));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(18.25m));
        }

        [Test]
        public void NewProfileResetRemovesBothRepresentations()
        {
            ProfileGoldPersistence.Save(.0423m);
            ProfileGoldPersistence.Reset();
            Assert.That(PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ProfileGoldKey), Is.False);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(500m));
        }
    }
}
