using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    // Shares the existing fixture's full save-key isolation and restoration.
    public sealed partial class AchievementProgressTests
    {
        [TestCase("fresh")]
        [TestCase("mixed")]
        [TestCase("legacy")]
        [TestCase("captured")]
        public void BatchQuery_MatchesIndividualQueriesAndCountWithoutWritingPreferences(string state)
        {
            if (state == "mixed" || state == "captured") PrepareBatchMixedProgress();
            if (state == "legacy")
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 1200);
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(1), 1);
                var art = new ComponentArtCatalog();
                foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                    PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(slot),
                        art.ExpandedEntries.First(entry => entry.Slot == slot).Id);
                PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentPurchaseAchievementKey, 1);
            }
            if (state == "captured")
            {
                AchievementCatalog.CaptureProgress();
                ProfileGoldPersistence.Save(0m);
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseOwnedKey);
            }

            var before = original.ToDictionary(pair => pair.Key, pair => ReadBatchPreference(pair.Key, pair.Value.IsString));
            var destination = Enumerable.Repeat(true, AchievementCatalog.All.Count + 2).ToArray();
            int actualCount = AchievementCatalog.FillUnlockStates(destination);
            int expectedCount = 0;
            for (int index = 0; index < AchievementCatalog.All.Count; index++)
            {
                bool expected = AchievementCatalog.IsUnlocked(AchievementCatalog.All[index]);
                Assert.That(destination[index], Is.EqualTo(expected), state + " / " + AchievementCatalog.All[index].Id);
                if (expected) expectedCount++;
            }
            Assert.That(actualCount, Is.EqualTo(expectedCount));
            Assert.That(AchievementCatalog.CountUnlocked(), Is.EqualTo(actualCount));
            Assert.That(destination[AchievementCatalog.All.Count], Is.True, "The caller's trailing buffer must remain untouched.");
            Assert.That(destination[AchievementCatalog.All.Count + 1], Is.True);
            if (state == "fresh") Assert.That(actualCount, Is.Zero);
            else Assert.That(actualCount, Is.GreaterThan(0));
            foreach (var pair in original)
                Assert.That(ReadBatchPreference(pair.Key, pair.Value.IsString), Is.EqualTo(before[pair.Key]),
                    "A status query changed a preference: " + pair.Key);
        }

        [Test]
        public void BatchQuery_RefreshesReusedBufferAfterChangesCaptureAndClear()
        {
            var states = new bool[AchievementCatalog.All.Count];
            int goldIndex = AchievementCatalog.All.ToList().FindIndex(item => item.Id == "gold_900");
            int collectionIndex = AchievementCatalog.All.ToList().FindIndex(item => item.Id == "warehouse_three");
            ProfileGoldPersistence.Save(899.9999m);
            AchievementCatalog.FillUnlockStates(states);
            Assert.That(states[goldIndex], Is.False);
            Assert.That(states[collectionIndex], Is.False);

            ProfileGoldPersistence.Save(900m);
            var collection = new CoreWarehouseCollection();
            foreach (CoreWarehouseTreasureDefinition item in CoreWarehouseCollection.Catalog.Take(3))
                Assert.That(collection.Grant(item.Id), Is.True);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, collection.ExportOwned());
            AchievementCatalog.FillUnlockStates(states);
            Assert.That(states[goldIndex], Is.True);
            Assert.That(states[collectionIndex], Is.True);

            ProfileGoldPersistence.Save(0m);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseOwnedKey);
            AchievementCatalog.FillUnlockStates(states);
            Assert.That(states[goldIndex], Is.False, "An uncaptured condition must refresh after spending.");
            Assert.That(states[collectionIndex], Is.False, "Snapshot-derived counts must not outlive one query.");

            ProfileGoldPersistence.Save(900m);
            AchievementCatalog.CaptureProgress();
            ProfileGoldPersistence.Save(0m);
            AchievementCatalog.FillUnlockStates(states);
            Assert.That(states[goldIndex], Is.True, "Earned records still survive spending.");
            AchievementCatalog.ClearProgress();
            AchievementCatalog.FillUnlockStates(states);
            Assert.That(states[goldIndex], Is.False, "Clearing records must affect the next refresh.");
        }

        [Test]
        public void BatchQuery_RejectsInvalidBuffersBeforeChangingTheirContents()
        {
            Assert.Throws<ArgumentNullException>(() => AchievementCatalog.FillUnlockStates(null));
            var tooSmall = Enumerable.Repeat(true, AchievementCatalog.All.Count - 1).ToArray();
            Assert.Throws<ArgumentException>(() => AchievementCatalog.FillUnlockStates(tooSmall));
            Assert.That(tooSmall.All(value => value), Is.True);
            Assert.That(AchievementCatalog.PersistenceKeys.Any(PlayerPrefs.HasKey), Is.False);
        }

        [Test]
        public void BatchQuery_ReportsWarmAlternatingTimingAndSupportedAllocationMeasurements()
        {
            PrepareBatchMixedProgress();
            const int warmups = 3;
            const int rounds = 7;
            const int iterations = 3;
            var individualStates = new bool[AchievementCatalog.All.Count];
            var batchStates = new bool[AchievementCatalog.All.Count];
            for (int warmup = 0; warmup < warmups; warmup++)
            {
                EvaluateIndividually(individualStates);
                AchievementCatalog.FillUnlockStates(batchStates);
            }
            CollectionAssert.AreEqual(individualStates, batchStates);
            bool allocationCounterAvailable = BatchAllocationCounterWorks();
            var individual = new BatchQueryMeasurement[rounds];
            var batch = new BatchQueryMeasurement[rounds];
            for (int round = 0; round < rounds; round++)
            {
                if ((round & 1) == 0)
                {
                    individual[round] = MeasureBatchQuery(false, individualStates, iterations, allocationCounterAvailable);
                    batch[round] = MeasureBatchQuery(true, batchStates, iterations, allocationCounterAvailable);
                }
                else
                {
                    batch[round] = MeasureBatchQuery(true, batchStates, iterations, allocationCounterAvailable);
                    individual[round] = MeasureBatchQuery(false, individualStates, iterations, allocationCounterAvailable);
                }
                Assert.That(batch[round].CountSum, Is.EqualTo(individual[round].CountSum));
                CollectionAssert.AreEqual(individualStates, batchStates);
            }
            double individualMilliseconds = BatchMedian(individual.Select(value => value.Milliseconds).ToArray());
            double batchMilliseconds = BatchMedian(batch.Select(value => value.Milliseconds).ToArray());
            TestContext.WriteLine("Achievement query benchmark: 50 statuses, mixed uncaptured save, " + warmups +
                " warmups, " + rounds + " alternating rounds, " + iterations + " queries per sample; preallocated result buffers.");
            TestContext.WriteLine("Median milliseconds/query: individual=" + individualMilliseconds.ToString("F4") +
                "; batch=" + batchMilliseconds.ToString("F4") + ". Timing is diagnostic, not a pass/fail threshold.");
            if (allocationCounterAvailable)
            {
                double individualBytes = BatchMedian(individual.Select(value => value.Bytes).ToArray());
                double batchBytes = BatchMedian(batch.Select(value => value.Bytes).ToArray());
                TestContext.WriteLine("Median managed bytes/query: individual=" + individualBytes.ToString("F0") +
                    "; batch=" + batchBytes.ToString("F0") + ". Allocation counter passed a 65536-byte probe.");
                Assert.That(batchBytes, Is.LessThan(individualBytes), "Sharing component and crew snapshots should reduce measured allocation.");
            }
            else
                TestContext.WriteLine("Managed allocation: Unavailable (-1). GC.GetAllocatedBytesForCurrentThread failed the known-allocation probe; no zero-allocation or reduction claim is made.");
        }

        private static void PrepareBatchMixedProgress()
        {
            ProfileGoldPersistence.Save(900m);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            foreach (string port in Ports.Take(3)) Recruit(CrewNarrativeCatalog.ForPortAndProfession(port, 0));
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            inventory.EnsureLoadout(0);
            inventory.Grant(art.ExpandedEntries[0], 1.05f);
            SaveInventory(inventory);
            // Keep legacy monthly-shop scanning outside both benchmark paths;
            // calendar and unrelated receipt keys are not modified by this fixture.
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentPurchaseAchievementKey, 1);
            var collection = new CoreWarehouseCollection();
            foreach (CoreWarehouseTreasureDefinition item in CoreWarehouseCollection.Catalog.Take(3)) collection.Grant(item.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, collection.ExportOwned());
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 100);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 100);
        }

        private static string ReadBatchPreference(string key, bool isString) => !PlayerPrefs.HasKey(key) ? "absent" :
            isString ? "string:" + PlayerPrefs.GetString(key) : "int:" + PlayerPrefs.GetInt(key);

        private static int EvaluateIndividually(bool[] states)
        {
            int count = 0;
            for (int index = 0; index < AchievementCatalog.All.Count; index++)
            {
                bool value = AchievementCatalog.IsUnlocked(AchievementCatalog.All[index]);
                states[index] = value;
                if (value) count++;
            }
            return count;
        }

        private static bool BatchAllocationCounterWorks()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[65536];
            long delta = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(probe);
            return delta >= probe.Length;
        }

        private static BatchQueryMeasurement MeasureBatchQuery(bool batched, bool[] states, int iterations, bool allocationCounterAvailable)
        {
            long allocatedBefore = allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
            long started = Stopwatch.GetTimestamp();
            int countSum = 0;
            for (int iteration = 0; iteration < iterations; iteration++)
                countSum += batched ? AchievementCatalog.FillUnlockStates(states) : EvaluateIndividually(states);
            long elapsed = Stopwatch.GetTimestamp() - started;
            long allocated = allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : -1;
            return new BatchQueryMeasurement
            {
                Milliseconds = elapsed * 1000d / Stopwatch.Frequency / iterations,
                Bytes = allocationCounterAvailable ? allocated / (double)iterations : -1,
                CountSum = countSum,
            };
        }

        private static double BatchMedian(double[] values)
        {
            Array.Sort(values);
            return values[values.Length / 2];
        }

        private struct BatchQueryMeasurement
        {
            public double Milliseconds;
            public double Bytes;
            public int CountSum;
        }
    }
}
