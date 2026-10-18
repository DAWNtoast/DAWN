using System;
using System.Globalization;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreWarehouseIncomeTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FirstObservation_StartsAtNowWithoutRetroactiveIncome()
        {
            var income = new CoreWarehouseIncome();
            Assert.That(income.Restore(string.Empty), Is.True);
            income.Observe(Start, 6);
            Assert.That(income.LastUtcTicks, Is.EqualTo(Start.Ticks));
            Assert.That(income.PendingGold, Is.Zero);
            Assert.That(income.Claim(Start), Is.Zero);
            Assert.That(income.Claim(Start.AddHours(1)), Is.EqualTo(60));
        }

        [Test]
        public void AllPortsCombined_EarnTenGoldPerDisplayedItemPerRealHour()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 2 + 3);
            Assert.That(income.Claim(Start.AddHours(2)), Is.EqualTo(100));
            Assert.That(income.PendingGold, Is.Zero);
        }

        [Test]
        public void PartialHours_AreProratedAndFractionalGoldSurvivesClaims()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            Assert.That(income.Claim(Start.AddMinutes(3)), Is.Zero);
            Assert.That(income.PendingGold, Is.EqualTo(.5m));
            Assert.That(income.Claim(Start.AddMinutes(9)), Is.EqualTo(1));
            Assert.That(income.PendingGold, Is.EqualTo(.5m));
            Assert.That(income.Claim(Start.AddMinutes(12)), Is.EqualTo(1));
            Assert.That(income.PendingGold, Is.Zero);
        }

        [Test]
        public void CountChanges_AccrueOldCountBeforeApplyingNewCount()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            income.Observe(Start.AddMinutes(30), 3);
            income.Observe(Start.AddMinutes(60), 2);
            Assert.That(income.Claim(Start.AddMinutes(90)), Is.EqualTo(30));
        }

        [Test]
        public void EmptyDisplayTime_DoesNotPayItemsPlacedLater()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 0);
            income.Observe(Start.AddDays(30), 1);
            Assert.That(income.Claim(Start.AddDays(30)), Is.Zero);
            Assert.That(income.Claim(Start.AddDays(30).AddMinutes(30)), Is.EqualTo(5));
        }

        [Test]
        public void RemovingLastItem_PreservesEarnedIncomeAndStopsFurtherAccrual()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            income.Observe(Start.AddMinutes(30), 0);
            Assert.That(income.Claim(Start.AddDays(1)), Is.EqualTo(5));
            Assert.That(income.Claim(Start.AddDays(2)), Is.Zero);
        }

        [Test]
        public void Cap_DiscardsOverflowAndImmediateLoginCannotClaimIt()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 10);
            Assert.That(income.Claim(Start.AddHours(4)), Is.EqualTo(300));
            Assert.That(income.Claim(Start.AddHours(4)), Is.Zero);
            Assert.That(income.Claim(Start.AddHours(4).AddMinutes(6)), Is.EqualTo(10));
        }

        [Test]
        public void Rollback_DoesNotMoveBaselineOrReplayAlreadyAccruedTime()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            Assert.That(income.Claim(Start.AddHours(1)), Is.EqualTo(10));
            Assert.That(income.Claim(Start.AddHours(-5)), Is.Zero);
            Assert.That(income.LastUtcTicks, Is.EqualTo(Start.AddHours(1).Ticks));
            Assert.That(income.Claim(Start.AddHours(1)), Is.Zero);
            Assert.That(income.Claim(Start.AddHours(2)), Is.EqualTo(10));
        }

        [Test]
        public void FrequentObservation_DoesNotLoseGoldToRounding()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            for (int second = 1; second <= 3600; second++)
                income.Observe(Start.AddSeconds(second), 1);
            Assert.That(income.Claim(Start.AddHours(1)), Is.EqualTo(10));
            Assert.That(income.PendingGold, Is.Zero);
        }

        [Test]
        public void SaveReload_PreservesFractionalUnitsCountAndClaimIdempotence()
        {
            var original = new CoreWarehouseIncome();
            original.Observe(Start, 1);
            original.Observe(Start.AddMinutes(1), 1);
            string snapshot = original.Serialize();
            var restored = new CoreWarehouseIncome();
            Assert.That(restored.Restore(snapshot), Is.True);
            Assert.That(restored.Serialize(), Is.EqualTo(snapshot));
            Assert.That(restored.CurrentDisplayedCount, Is.EqualTo(1));
            Assert.That(restored.Claim(Start.AddHours(1)), Is.EqualTo(10));
            var afterClaim = new CoreWarehouseIncome();
            Assert.That(afterClaim.Restore(restored.Serialize()), Is.True);
            Assert.That(afterClaim.Claim(Start.AddHours(1)), Is.Zero);
        }

        [Test]
        public void FrequentSaveReload_PreservesEveryElapsedTick()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            for (int second = 1; second <= 3600; second++)
            {
                income.Observe(Start.AddSeconds(second), 1);
                var restored = new CoreWarehouseIncome();
                Assert.That(restored.Restore(income.Serialize()), Is.True);
                income = restored;
            }
            Assert.That(income.Claim(Start.AddHours(1)), Is.EqualTo(10));
        }

        [Test]
        public void ExtremeAbsenceAndDisplayCount_SaturateWithoutOverflow()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(new DateTime(0, DateTimeKind.Utc), int.MaxValue);
            Assert.That(income.IsInitialized, Is.True);
            Assert.That(income.Claim(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)), Is.EqualTo(300));
        }

        [Test]
        public void Snapshot_IsIndependentOfCurrentCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var income = new CoreWarehouseIncome();
                income.Observe(Start, 1);
                income.Observe(Start.AddMinutes(3), 1);
                string serialized = income.Serialize();
                Assert.That(serialized, Does.Contain("|18000000000|"));
                var restored = new CoreWarehouseIncome();
                Assert.That(restored.Restore(serialized), Is.True);
                Assert.That(restored.Claim(Start.AddMinutes(6)), Is.EqualTo(1));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("garbage")]
        [TestCase("2|100|0|1")]
        [TestCase("1|-1|0|1")]
        [TestCase("1|3155378976000000000|0|1")]
        [TestCase("1|100|-1|1")]
        [TestCase("1|100|10800000000001|1")]
        [TestCase("1|100|NaN|1")]
        [TestCase("1|100|0|-1")]
        [TestCase("1|100|0|2147483648")]
        [TestCase("1|100|0|1|extra")]
        public void InvalidSnapshot_IsRejectedWithoutChangingExistingState(string snapshot)
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 2);
            income.Observe(Start.AddMinutes(3), 2);
            string before = income.Serialize();
            Assert.That(income.Restore(snapshot), Is.False);
            Assert.That(income.Serialize(), Is.EqualTo(before));
        }

        [Test]
        public void NegativeCount_IsRejectedBeforeAccrualOrMutation()
        {
            var income = new CoreWarehouseIncome();
            income.Observe(Start, 1);
            string before = income.Serialize();
            Assert.Throws<ArgumentOutOfRangeException>(() => income.Observe(Start.AddHours(1), -1));
            Assert.That(income.Serialize(), Is.EqualTo(before));
        }
    }
}
