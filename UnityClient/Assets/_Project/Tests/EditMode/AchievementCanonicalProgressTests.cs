using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class AchievementProgressTests
    {
        [TestCase("gold_600", 600)]
        [TestCase("gold_900", 900)]
        [TestCase("gold_1200", 1200)]
        [TestCase("gold_2000", 2000)]
        public void CanonicalGoldThresholdKeepsItsExactDecimalBoundary(string id, int expectedTarget)
        {
            Assert.That(CoreAchievementCatalog.Find(id).Target, Is.EqualTo(expectedTarget));
            ProfileGoldPersistence.Save(expectedTarget - .0001m);
            Assert.That(Unlocked(id), Is.False);
            ProfileGoldPersistence.Save(expectedTarget);
            Assert.That(Unlocked(id), Is.True);
            AchievementCatalog.CaptureProgress();
            ProfileGoldPersistence.Save(0m);
            Assert.That(Unlocked(id), Is.True, "Consolidating definitions must preserve earned milestone records.");
        }

        [TestCase("ship_second", 2)]
        [TestCase("ship_third", 3)]
        [TestCase("ship_fourth", 4)]
        [TestCase("all_ships", 12)]
        public void CanonicalShipThresholdStillCountsTheStarterAndActuallyOwnedShips(string id, int expectedTarget)
        {
            Assert.That(CoreAchievementCatalog.Find(id).Target, Is.EqualTo(expectedTarget));
            for (int ship = 1; ship < expectedTarget - 1; ship++)
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            Assert.That(Unlocked(id), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(expectedTarget - 1), 1);
            Assert.That(Unlocked(id), Is.True);
        }

        [TestCase("warehouse_one", 1)]
        [TestCase("warehouse_three", 3)]
        [TestCase("warehouse_ten", 10)]
        public void CanonicalCollectionThresholdRetainsTheExistingOwnedItemCount(string id, int expectedTarget)
        {
            Assert.That(CoreAchievementCatalog.Find(id).Target, Is.EqualTo(expectedTarget));
            string[] ids = CoreWarehouseCollection.Catalog.Take(expectedTarget).Select(item => item.Id).ToArray();
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Join(",", ids.Take(expectedTarget - 1)));
            Assert.That(Unlocked(id), Is.False);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Join(",", ids));
            Assert.That(Unlocked(id), Is.True);
        }

        [Test]
        public void CanonicalDisplayThresholdRetainsTheSixSlotHistoricalRecord()
        {
            Assert.That(CoreAchievementCatalog.Find("warehouse_display_six").Target, Is.EqualTo(CoreWarehouseCollection.DisplaySlotCount));
            PlayerPrefs.SetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey, 5);
            Assert.That(Unlocked("warehouse_display_six"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey, 6);
            Assert.That(Unlocked("warehouse_display_six"), Is.True);
        }

        [Test]
        public void CanonicalFullLoadoutStillRequiresFourSlotsEvenThoughFiveSlotTypesExist()
        {
            Assert.That(CoreAchievementCatalog.Find("component_full").Target, Is.EqualTo(4));
            var art = new ComponentArtCatalog();
            ComponentSlot[] slots = Enum.GetValues(typeof(ComponentSlot)).Cast<ComponentSlot>().ToArray();
            Assert.That(slots.Length, Is.GreaterThanOrEqualTo(4));
            for (int index = 0; index < 3; index++)
                PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(slots[index]), art.ExpandedEntries.First(item => item.Slot == slots[index]).Id);
            Assert.That(Unlocked("component_full"), Is.False);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(slots[3]), art.ExpandedEntries.First(item => item.Slot == slots[3]).Id);
            Assert.That(Unlocked("component_full"), Is.True);
        }

        [Test]
        public void CanonicalStageThresholdRequiresEachOfTheThreeDeckStages()
        {
            NavalBattleStage[] stages = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().ToArray();
            Assert.That(CoreAchievementCatalog.Find("battle_stages").Target, Is.EqualTo(stages.Length));
            foreach (NavalBattleStage stage in stages.Take(stages.Length - 1))
                PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(stage), "0,1");
            Assert.That(Unlocked("battle_stages"), Is.False);
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(stages.Last()), "0");
            Assert.That(Unlocked("battle_stages"), Is.True);
        }

        [Test]
        public void CanonicalSupplyConditionStillRequiresBothWaterAndFoodAtOneHundred()
        {
            Assert.That(CoreAchievementCatalog.Find("cargo_ready").Target, Is.EqualTo(100));
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 100);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 99);
            Assert.That(Unlocked("cargo_ready"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 100);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 99);
            Assert.That(Unlocked("cargo_ready"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 100);
            Assert.That(Unlocked("cargo_ready"), Is.True);
        }

        [Test]
        public void CanonicalFameThresholdRemainsTwenty()
        {
            Assert.That(CoreAchievementCatalog.Find("fame").Target, Is.EqualTo(20));
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Fame"), 19);
            Assert.That(Unlocked("fame"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Fame"), 20);
            Assert.That(Unlocked("fame"), Is.True);
        }

        [TestCase("", false)]
        [TestCase("0=1", true)]
        [TestCase(" ", true)]
        [TestCase("legacy-unparsed-record", true)]
        public void CanonicalCardUpgradePreservesTheExistingNonEmptyRecordPredicate(string record, bool expected)
        {
            Assert.That(CoreAchievementCatalog.Find("card_upgrade").Target, Is.EqualTo(1));
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleCardUpgradeKey, record);
            Assert.That(Unlocked("card_upgrade"), Is.EqualTo(expected));
        }

        [Test]
        public void CanonicalDispatchDurationCannotUnlockWithoutAnActiveOrRecordedDispatch()
        {
            Assert.That(AchievementCatalog.LongDispatchHours, Is.EqualTo(CoreAchievementCatalog.Find("dispatch_long").Target));
            Assert.That(AchievementCatalog.LongDispatchHours, Is.EqualTo(12));
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationKey, 12);
            Assert.That(Unlocked("dispatch_long"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchMissionKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationKey, 11);
            Assert.That(Unlocked("dispatch_long"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationKey, 12);
            Assert.That(Unlocked("dispatch_long"), Is.True);
            Assert.That(Unlocked("dispatch_resolved"), Is.False);
        }

        [Test]
        public void CallerConstructedMetadataCannotLowerTheCanonicalUnlockTarget()
        {
            var lowered = new CoreAchievementDefinition("gold_900", "Custom", "Custom", CoreAchievementCategory.Economy,
                CoreAchievementRequirementKind.Counter, "profile_gold", 1);
            ProfileGoldPersistence.Save(1m);
            Assert.That(AchievementCatalog.IsUnlocked(lowered), Is.False);
            ProfileGoldPersistence.Save(900m);
            Assert.That(AchievementCatalog.IsUnlocked(lowered), Is.True);
        }

        [Test]
        public void AliasQueriesAndCaptureUseCanonicalKeysAndDoNotMigratePrototypeFlags()
        {
            string[] aliases = { "first_sail", "stranger_port", "sea_trade", "golden_captain" };
            foreach (string alias in aliases)
            {
                string key = "ShatteredSail.Achievements.Unlocked.v1." + alias;
                Preserve(key, false);
                PlayerPrefs.DeleteKey(key);
            }
            ProfileGoldPersistence.Save(900m);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            foreach (string alias in aliases) Assert.That(AchievementCatalog.IsUnlocked(CoreAchievementCatalog.Find(alias)), Is.True, alias);
            AchievementCatalog.CaptureProgress();
            foreach (string alias in aliases)
            {
                string canonical = CoreAchievementCatalog.Find(alias).Id;
                Assert.That(PlayerPrefs.GetInt("ShatteredSail.Achievements.Unlocked.v1." + canonical), Is.EqualTo(1), canonical);
                Assert.That(PlayerPrefs.HasKey("ShatteredSail.Achievements.Unlocked.v1." + alias), Is.False, alias);
            }
            // A prototype-only flag is not evidence for a real player milestone.
            AchievementCatalog.ClearProgress();
            ProfileGoldPersistence.Save(0m);
            PlayerPrefs.SetInt("ShatteredSail.Achievements.Unlocked.v1.sea_trade", 1);
            Assert.That(Unlocked("gold_600"), Is.False);
        }
    }
}
