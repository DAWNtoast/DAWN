using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class AchievementProgressTests
    {
        [TestCase("gold_600", 1000)]
        [TestCase("gold_900", 2500)]
        [TestCase("gold_1200", 5000)]
        [TestCase("gold_2000", 10000)]
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
        public void TutorialCompassDoesNotAlsoGrantTheVoyageTreasureChallenge()
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, CoreWarehouseCollection.TutorialTreasureId);
            Assert.That(Unlocked("tutorial_reward"), Is.False, "The compass still requires graduation.");
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            AchievementCatalog.CaptureProgress();
            AssertUnlocked("tutorial_reward", "tutorial_warehouse");
            Assert.That(Unlocked("warehouse_one"), Is.False);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, CoreWarehouseCollection.BoatswainBellTreasureId);
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked("warehouse_one"), Is.False, "Unrelated pre-existing stock is not a newly completed voyage.");
        }

        [TestCase("battle_deck")]
        [TestCase("battle_stages")]
        [TestCase("tutorial_recovery")]
        [TestCase("tutorial_component")]
        [TestCase("warehouse_one")]
        [TestCase("component_full")]
        [TestCase("cargo_all")]
        [TestCase("seventh_harbor")]
        public void TightenedRequirementsPreservePreviouslyEarnedUnlocks(string id)
        {
            PlayerPrefs.SetInt("ShatteredSail.Achievements.Unlocked.v1." + id, 1);
            Assert.That(Unlocked(id), Is.True);
            AchievementCatalog.CaptureProgress();
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
        public void CanonicalFullLoadoutRequiresFivePlayerInstalledSlotsAndThreeRareParts()
        {
            Assert.That(CoreAchievementCatalog.Find("component_full").Target, Is.EqualTo(5));
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            inventory.EnsureLoadout(0);
            SaveInventory(inventory);
            Assert.That(Unlocked("component_full"), Is.False, "The purchased ship's five default parts are not a player refit.");
            Assert.That(Unlocked("tutorial_component"), Is.False);
            ComponentSlot[] slots = Enum.GetValues(typeof(ComponentSlot)).Cast<ComponentSlot>().ToArray();
            Assert.That(slots.Length, Is.EqualTo(5));
            for (int index = 0; index < slots.Length; index++)
            {
                ComponentRarity rarity = index < 2 ? ComponentRarity.Rare : ComponentRarity.Uncommon;
                var part = inventory.Grant(art.ExpandedEntries.First(item => item.Slot == slots[index] && item.Rarity == rarity));
                Assert.That(inventory.TryEquip(0, part.InstanceId), Is.True);
                SaveInventory(inventory);
                Assert.That(Unlocked("component_full"), Is.False, "All five slots and at least three rare parts must be present together.");
            }
            Assert.That(Unlocked("tutorial_component"), Is.True);
            var thirdRare = inventory.Grant(art.ExpandedEntries.First(item => item.Slot == slots[2] && item.Rarity == ComponentRarity.Rare));
            Assert.That(inventory.TryEquip(0, thirdRare.InstanceId), Is.True);
            SaveInventory(inventory);
            Assert.That(Unlocked("component_full"), Is.True);
            inventory.TryUnequip(0, slots[4]);
            SaveInventory(inventory);
            Assert.That(Unlocked("component_full"), Is.False);
        }

        [Test]
        public void CanonicalStageThresholdRequiresVictoriesUsingEachEditedStage()
        {
            NavalBattleStage[] stages = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().ToArray();
            Assert.That(CoreAchievementCatalog.Find("battle_stages").Target, Is.EqualTo(stages.Length));
            foreach (NavalBattleStage stage in stages.Take(stages.Length - 1))
            {
                AchievementCatalog.RecordDeckEdited(stage);
                AchievementCatalog.RecordAuthoredDeckVictories(1 << (int)stage);
            }
            Assert.That(Unlocked("battle_stages"), Is.False);
            AchievementCatalog.RecordDeckEdited(stages.Last());
            Assert.That(Unlocked("battle_stages"), Is.False, "Editing the third stage alone is insufficient.");
            AchievementCatalog.RecordAuthoredDeckVictories(1 << (int)stages.Last());
            Assert.That(Unlocked("battle_stages"), Is.True);
        }

        [Test]
        public void CanonicalDefaultDecksAndOneWorkshopEditCannotUnlockCombatAchievements()
        {
            var session = new CoreVoyageSession();
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleDeckKey, string.Join(",", session.BattleDeck));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(stage),
                    string.Join(",", session.GetBattleDeck(stage)));

            Assert.That(Unlocked("battle_deck"), Is.False);
            Assert.That(Unlocked("battle_stages"), Is.False);
            AchievementCatalog.RecordAuthoredDeckVictories(7);
            Assert.That(Unlocked("battle_deck"), Is.False, "Default-deck victories do not count.");
            AchievementCatalog.RecordDeckEdited(NavalBattleStage.Preparation);
            Assert.That(Unlocked("tutorial_deck"), Is.True);
            Assert.That(Unlocked("battle_deck"), Is.False);
            Assert.That(Unlocked("battle_stages"), Is.False);
            AchievementCatalog.RecordAuthoredDeckVictories(7);
            Assert.That(Unlocked("battle_deck"), Is.True);
            Assert.That(Unlocked("battle_stages"), Is.False, "Only the edited preparation stage may be credited.");
            Assert.That(PlayerPrefs.GetInt(AchievementCatalog.WonDeckStagesKey), Is.EqualTo(1 << (int)NavalBattleStage.Preparation));
        }

        [Test]
        public void CanonicalSupplyConditionStillRequiresBothWaterAndFoodAtFiveHundred()
        {
            Assert.That(CoreAchievementCatalog.Find("cargo_ready").Target, Is.EqualTo(500));
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 500);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 499);
            Assert.That(Unlocked("cargo_ready"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 500);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 499);
            Assert.That(Unlocked("cargo_ready"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 500);
            Assert.That(Unlocked("cargo_ready"), Is.True);
        }

        [Test]
        public void CanonicalFameThresholdRequiresOneHundred()
        {
            Assert.That(CoreAchievementCatalog.Find("fame").Target, Is.EqualTo(100));
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Fame"), 99);
            Assert.That(Unlocked("fame"), Is.False);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Fame"), 100);
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
        public void CallerConstructedMetadataCannotLowerTheCanonicalUnlockTarget()
        {
            var lowered = new CoreAchievementDefinition("gold_900", "Custom", "Custom", CoreAchievementCategory.Economy,
                CoreAchievementRequirementKind.Counter, "profile_gold", 1);
            ProfileGoldPersistence.Save(1m);
            Assert.That(AchievementCatalog.IsUnlocked(lowered), Is.False);
            ProfileGoldPersistence.Save(2500m);
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
            ProfileGoldPersistence.Save(2500m);
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
