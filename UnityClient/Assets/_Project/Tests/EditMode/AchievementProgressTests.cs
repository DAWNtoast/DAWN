using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class AchievementProgressTests
    {
        private static readonly string[] Ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private static readonly string[] Resources = { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Fame" };
        private readonly Dictionary<string, SavedPreference> original = new Dictionary<string, SavedPreference>();

        [SetUp]
        public void IsolateSave()
        {
            original.Clear();
            foreach (string key in AchievementCatalog.PersistenceKeys) Preserve(key, false);
            foreach (string key in new[]
            {
                CoreGameplayDemoController.TutorialCompletedKey, CoreGameplayDemoController.TutorialRewardGrantedKey,
                CoreGameplayDemoController.GuidedTutorialCheckpointKey, CoreGameplayDemoController.GuidedTutorialDeckEditedKey,
                CoreGameplayDemoController.GuidedTutorialComponentRewardKey, CoreGameplayDemoController.GuidedTutorialRecoveryKey,
                CoreGameplayDemoController.GuidedTutorialNavigatorEventKey, CoreGameplayDemoController.ProfileGoldKey,
                CoreGameplayDemoController.SelectedShipKey, CoreGameplayDemoController.ComponentPurchaseAchievementKey,
                CoreGameplayDemoController.WarehouseDisplayRecordKey,
                CoreGameplayDemoController.BattleDeckEditedKey,
            }) Preserve(key, false);
            foreach (string key in new[]
            {
                CoreGameplayDemoController.CurrentPortKey, ProfileGoldPersistence.DecimalKey,
                CoreGameplayDemoController.ComponentInventoryKey, CoreGameplayDemoController.CollectionProgressKey,
                CoreGameplayDemoController.WarehouseOwnedKey, CoreGameplayDemoController.WarehouseDisplayKey,
                CoreGameplayDemoController.DrawnSeaChartKey, CoreGameplayDemoController.BattleDeckKey,
                CoreGameplayDemoController.BattleCardUpgradeKey, CoreGameplayDemoController.AcquiredSpecialCardsKey,
            }) Preserve(key, true);
            foreach (string port in Ports)
            {
                Preserve(CoreGameplayDemoController.PortInvestmentKey(port), false);
                Preserve(CoreGameplayDemoController.WarehouseOwnedPortKey(port), true);
                Preserve(CoreGameplayDemoController.WarehouseDisplayPortKey(port), true);
            }
            foreach (string resource in Resources) Preserve(CoreGameplayDemoController.CargoKey(resource), false);
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++) Preserve(CoreGameplayDemoController.ShipOwnedKey(ship), false);
            for (int profession = 0; profession < CrewNarrativeCatalog.ProfessionCount; profession++)
                Preserve(CoreGameplayDemoController.CrewRecruitmentKey(profession), false);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                Preserve(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), false);
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot))) Preserve(CoreGameplayDemoController.ComponentLoadoutKey(slot), true);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage))) Preserve(CoreGameplayDemoController.StageBattleDeckKey(stage), true);
            var art = new ComponentArtCatalog();
            DateTime savedDate = CoreGameplayDemoController.SavedGameDateTime();
            foreach (string port in Ports)
                foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(art, port, savedDate))
                    Preserve(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id), false);
            foreach (string key in original.Keys) PlayerPrefs.DeleteKey(key);
        }

        [TearDown]
        public void RestoreSave()
        {
            foreach (KeyValuePair<string, SavedPreference> entry in original)
            {
                PlayerPrefs.DeleteKey(entry.Key);
                if (!entry.Value.Exists) continue;
                if (entry.Value.IsString) PlayerPrefs.SetString(entry.Key, entry.Value.Text);
                else PlayerPrefs.SetInt(entry.Key, entry.Value.Number);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void NewSaveHasFiftyUniqueAchievementsAndNoUnlockedProgress()
        {
            Assert.That(AchievementCatalog.All.Count, Is.EqualTo(50));
            Assert.That(AchievementCatalog.All.Select(item => item.Id).Distinct().Count(), Is.EqualTo(50));
            Assert.That(AchievementCatalog.CountUnlocked(), Is.Zero);
        }

        [Test]
        public void ReadOnlyQueriesPreviewOldSaveWithoutWritingAchievementRecords()
        {
            ProfileGoldPersistence.Save(10000m);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            Assert.That(Unlocked("gold_2000"), Is.True);
            Assert.That(AchievementCatalog.CountUnlocked(), Is.GreaterThanOrEqualTo(5));
            Assert.That(AchievementCatalog.PersistenceKeys.Any(PlayerPrefs.HasKey), Is.False);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(10000m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey), Is.EqualTo("灰鸥港"));
        }

        [Test]
        public void CapturedMoneySuppliesAndFameRemainUnlockedAfterSpending()
        {
            ProfileGoldPersistence.Save(10000m);
            foreach (string resource in Resources) PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey(resource), 500);
            AchievementCatalog.CaptureProgress();
            ProfileGoldPersistence.Save(0m);
            foreach (string resource in Resources) PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey(resource), 0);
            AchievementCatalog.CaptureProgress();
            AssertUnlocked("gold_600", "gold_900", "gold_1200", "gold_2000", "cargo_ready", "fame");
            Assert.That(Unlocked("cargo_all"), Is.False, "Possessing supplies is not evidence of a completed long voyage.");
        }

        [Test]
        public void SequentialPortVisitsAccumulateWithoutAnyInvestment()
        {
            foreach (string port in Ports)
            {
                PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, port);
                AchievementCatalog.CaptureProgress();
            }
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, Ports[0]);
            AssertUnlocked("port_gray_gull", "port_white_shell", "port_bronze_bell", "port_salt_crown", "port_twilight", "all_ports");
            Assert.That(Unlocked("invest_port"), Is.False);
        }

        [Test]
        public void ExistingCollectionPortHistoryMigratesAndSurvivesItsRemoval()
        {
            var session = new CoreVoyageSession();
            foreach (string port in Ports) session.RecordCollectionPortVisit(port);
            PlayerPrefs.SetString(CoreGameplayDemoController.CollectionProgressKey, JsonUtility.ToJson(session.CaptureCollectionProgress()));
            Assert.That(Unlocked("all_ports"), Is.True);
            AchievementCatalog.CaptureProgress();
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CollectionProgressKey);
            AssertUnlocked("all_ports", "port_gray_gull", "port_white_shell", "port_bronze_bell", "port_salt_crown", "port_twilight");
        }

        [Test]
        public void ZeroOrNegativeInvestmentKeysDoNotProveVisitsOrInvestment()
        {
            foreach (string port in Ports) PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey("灰鸥港"), -1);
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked("all_ports"), Is.False);
            Assert.That(Unlocked("port_gray_gull"), Is.False);
            Assert.That(Unlocked("invest_port"), Is.False);
            Assert.That(Unlocked("invest_all_ports"), Is.False);
        }

        [Test]
        public void SharedRoutesRequiresAnInvestmentInEveryPort()
        {
            foreach (string port in Ports.Take(5)) PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), 2);
            AssertUnlocked("invest_port", "invest_ten");
            Assert.That(Unlocked("invest_all_ports"), Is.False, "Ten levels in five ports do not prove investment in the sixth.");
            PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(Ports[5]), 1);
            AchievementCatalog.CaptureProgress();
            foreach (string port in Ports) PlayerPrefs.DeleteKey(CoreGameplayDemoController.PortInvestmentKey(port));
            AssertUnlocked("invest_all_ports", "invest_ten", "all_ports");
        }

        [Test]
        public void DifferentPeopleOfOneProfessionCountAsThreeCrew()
        {
            foreach (string port in Ports.Take(3)) Recruit(CrewNarrativeCatalog.ForPortAndProfession(port, 0));
            AssertUnlocked("crew_one", "crew_three");
            Assert.That(Unlocked("crew_six"), Is.False);
        }

        [Test]
        public void SixCrewFromOnePortDoNotCountAsSixPortFriends()
        {
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.ForPort(Ports[0]).Take(6)) Recruit(crew);
            AssertUnlocked("crew_one", "crew_three", "crew_six");
            Assert.That(Unlocked("crew_variants"), Is.False);
        }

        [Test]
        public void OneCrewFromEachPortCompletesBothSixCrewAchievements()
        {
            foreach (string port in Ports) Recruit(CrewNarrativeCatalog.ForPortAndProfession(port, 0));
            AchievementCatalog.CaptureProgress();
            AssertUnlocked("crew_six", "crew_variants");
        }

        [Test]
        public void LegacyProfessionAndItsExplicitIdentityAreNotDoubleCounted()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            Recruit(CrewNarrativeCatalog.LegacyForProfession(0));
            Recruit(CrewNarrativeCatalog.ForPortAndProfession(Ports[0], 0));
            Assert.That(Unlocked("crew_three"), Is.False);
            Recruit(CrewNarrativeCatalog.ForPortAndProfession(Ports[1], 0));
            Assert.That(Unlocked("crew_three"), Is.True);
        }

        [Test]
        public void SixLegacyProfessionFlagsMigrateAsSixPeopleFromOnePort()
        {
            for (int profession = 0; profession < 6; profession++) PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(profession), 1);
            AssertUnlocked("crew_one", "crew_three", "crew_six");
            Assert.That(Unlocked("crew_variants"), Is.False);
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"version\":2,\"initializedShips\":[],\"items\":[]}")]
        public void EmptyInventoryIsNotAnOwnedComponent(string json)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, json);
            Assert.That(Unlocked("component_inventory"), Is.False);
            Assert.That(Unlocked("component_loadout"), Is.False);
        }

        [Test]
        public void FourSlotsSpreadAcrossTwoShipsDoNotCountAsOneFullLoadout()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            var inventory = new OwnedShipComponentInventory(new ComponentArtCatalog());
            for (int ship = 0; ship < 2; ship++)
            {
                inventory.EnsureLoadout(ship);
                foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                    if (slot != ComponentSlot.Bow && slot != ComponentSlot.Sail) inventory.TryUnequip(ship, slot);
            }
            SaveInventory(inventory);
            AssertUnlocked("component_inventory", "component_loadout");
            Assert.That(Unlocked("component_full"), Is.False);
            Assert.That(Unlocked("component_cannon"), Is.False);
        }

        [Test]
        public void PhysicalInventoryUnlocksEquipmentAndKeepsItAfterUnloading()
        {
            var inventory = new OwnedShipComponentInventory(new ComponentArtCatalog());
            inventory.EnsureLoadout(0);
            inventory.TryUnequip(0, ComponentSlot.Plating);
            SaveInventory(inventory);
            AchievementCatalog.CaptureProgress();
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot))) inventory.TryUnequip(0, slot);
            foreach (OwnedShipComponent item in inventory.Items.ToArray()) inventory.TryRemoveUninstalled(item.InstanceId);
            SaveInventory(inventory);
            AchievementCatalog.CaptureProgress();
            AssertUnlocked("component_inventory", "component_loadout", "component_cannon");
            Assert.That(Unlocked("component_full"), Is.False);
        }

        [Test]
        public void LegacyFittingsAreRecognizedWithoutCreatingStarterParts()
        {
            var art = new ComponentArtCatalog();
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(slot), art.Entries.First(item => item.Slot == slot).Id);
            AssertUnlocked("component_loadout", "component_cannon");
            Assert.That(Unlocked("component_full"), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey), Is.False);
            AchievementCatalog.CaptureProgress();
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot))) PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentLoadoutKey(slot));
            AssertUnlocked("component_loadout", "component_cannon");
            Assert.That(Unlocked("component_full"), Is.False);
        }

        [Test]
        public void GraduationAndUnrelatedOwnedRewardsCannotImplyTutorialCompassOrPlayerInstallation()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            var collection = new CoreWarehouseCollection();
            Assert.That(collection.Grant(CoreWarehouseCollection.BoatswainBellTreasureId), Is.True);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, collection.ExportOwned());
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            Assert.That(inventory.Grant(art.Entries[0]), Is.Not.Null);
            SaveInventory(inventory);
            AchievementCatalog.CaptureProgress();
            AssertUnlocked("tutorial_checkpoint");
            Assert.That(Unlocked("tutorial_reward"), Is.False);
            Assert.That(Unlocked("tutorial_component"), Is.False);
            Assert.That(Unlocked("warehouse_one"), Is.False, "Existing collection stock cannot invent voyage reward evidence.");
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialRewardGrantedKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialComponentRewardKey), Is.False);
        }

        [Test]
        public void TutorialActionsCanBeCompletedAfterGraduationAndRemainPermanent()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            AchievementCatalog.RecordDeckEdited();
            AchievementCatalog.RecordDamagedReturn();
            AchievementCatalog.RecordNavigatorVoyage();
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialDeckEditedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialRecoveryKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey);
            AssertUnlocked("tutorial_deck", "tutorial_recovery", "tutorial_navigator");
        }

        [Test]
        public void CaptureDoesNotPromoteTheOldLooseRecoveryFlagToAChallenge()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialDeckEditedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialRecoveryKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey, 1);
            AchievementCatalog.CaptureProgress();
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialDeckEditedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialRecoveryKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey);
            AssertUnlocked("tutorial_deck", "tutorial_navigator");
            Assert.That(Unlocked("tutorial_recovery"), Is.False);
        }

        [Test]
        public void ClearingAchievementProgressRemovesAllMilestonesAndVisitHistoryOnly()
        {
            foreach (string port in Ports)
            {
                PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, port);
                AchievementCatalog.CaptureProgress();
            }
            AchievementCatalog.RecordDeckEdited(NavalBattleStage.Naval);
            AchievementCatalog.RecordAuthoredDeckVictories(1 << (int)NavalBattleStage.Naval);
            AchievementCatalog.RecordDamagedReturn();
            AchievementCatalog.RecordNavigatorVoyage();
            ProfileGoldPersistence.Save(17m);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, Ports[0]);
            AchievementCatalog.ClearProgress();
            Assert.That(AchievementCatalog.PersistenceKeys.Any(PlayerPrefs.HasKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.BattleDeckEditedKey), Is.False);
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) Assert.That(PlayerPrefs.HasKey(key), Is.False);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(17m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey), Is.EqualTo(Ports[0]));
            AchievementCatalog.CaptureProgress();
            Assert.That(AchievementCatalog.CountUnlocked(), Is.Zero);
        }

        [Test]
        public void OneSaveCanAccumulateAllFiftyAcrossMutuallyExclusiveStates()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialRewardGrantedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 1);
            AchievementCatalog.RecordDeckEdited();
            AchievementCatalog.RecordDamagedReturn();
            AchievementCatalog.RecordNavigatorVoyage();
            var collection = new CoreWarehouseCollection();
            foreach (CoreWarehouseTreasureDefinition item in CoreWarehouseCollection.Catalog.Take(10)) Assert.That(collection.Grant(item.Id), Is.True);
            collection.Grant(CoreWarehouseCollection.TutorialTreasureId);
            collection.Grant("seventh_harbor_beacon_lantern");
            for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++) Assert.That(collection.TryDisplay(collection.OwnedIds[slot], slot), Is.True);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, collection.ExportOwned());
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, collection.ExportDisplay());
            ProfileGoldPersistence.Save(10000m);
            foreach (string resource in Resources) PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey(resource), 500);
            AchievementCatalog.CaptureProgress();
            ProfileGoldPersistence.Save(0m);
            foreach (string resource in Resources) PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey(resource), 0);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseDisplayKey);

            foreach (string port in Ports)
            {
                PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, port);
                PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), 2);
                Recruit(CrewNarrativeCatalog.ForPortAndProfession(port, 0));
                AchievementCatalog.CaptureProgress();
            }
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++) PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            inventory.InitializeEmptyLoadout(0);
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                var item = inventory.Grant(art.ExpandedEntries.First(entry => entry.Slot == slot && entry.Rarity == ComponentRarity.Rare));
                Assert.That(inventory.TryEquip(0, item.InstanceId), Is.True);
            }
            SaveInventory(inventory);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentPurchaseAchievementKey, 1);
            AchievementCatalog.CaptureProgress();
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot))) inventory.TryUnequip(0, slot);
            SaveInventory(inventory);

            var session = new CoreVoyageSession();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Join(",", session.GetBattleDeck(stage)));
                AchievementCatalog.RecordDeckEdited(stage);
            }
            AchievementCatalog.RecordAuthoredDeckVictories(7);
            AchievementCatalog.RecordVoyageTreasure();
            AchievementCatalog.RecordSuppliedLongVoyage();
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleDeckKey, string.Join(",", session.BattleDeck));
            int card = session.GetBattleDeck(NavalBattleStage.Preparation)[0];
            Assert.That(session.TryUpgradeBattleCard(card, out string reason), Is.True, reason);
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Join(";", session.CardUpgradeLevels.Select(pair => pair.Key + "=" + pair.Value)));
            CoreSpecialCardDefinition special = CoreSpecialCardCatalog.ForCrew(CrewNarrativeCatalog.ForPortAndProfession(Ports[0], 0).Id);
            Assert.That(special, Is.Not.Null);
            PlayerPrefs.SetString(CoreGameplayDemoController.AcquiredSpecialCardsKey, special.Card.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.DrawnSeaChartKey, CoreVoyageSession.CreateChartSeed(0, Ports[0], Ports[1]));
            AchievementCatalog.CaptureProgress();

            AchievementCatalog.RecordVoyageThreeEvents();
            AchievementCatalog.RecordVoyageHalfChart();
            AchievementCatalog.RecordVoyageEventArrival();
            Assert.That(AchievementCatalog.All.Where(item => !AchievementCatalog.IsUnlocked(item)).Select(item => item.Id), Is.Empty);
            Assert.That(AchievementCatalog.CountUnlocked(), Is.EqualTo(50));
            // Every gameplay condition can subsequently disappear; only this save's earned records remain.
            var progressKeys = new HashSet<string>(AchievementCatalog.PersistenceKeys);
            foreach (string key in original.Keys) if (!progressKeys.Contains(key)) PlayerPrefs.DeleteKey(key);
            AchievementCatalog.CaptureProgress();
            Assert.That(AchievementCatalog.CountUnlocked(), Is.EqualTo(50));
            AchievementCatalog.ClearProgress();
            Assert.That(AchievementCatalog.CountUnlocked(), Is.Zero);
        }

        private static bool Unlocked(string id) => AchievementCatalog.IsUnlocked(AchievementCatalog.All.Single(item => item.Id == id));
        private static void AssertUnlocked(params string[] ids)
        {
            foreach (string id in ids) Assert.That(Unlocked(id), Is.True, id);
        }
        private static void Recruit(CrewVariantDefinition crew) => PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
        private static void SaveInventory(OwnedShipComponentInventory inventory) => PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, inventory.Export());
        private void Preserve(string key, bool isString)
        {
            if (original.ContainsKey(key)) return;
            original.Add(key, new SavedPreference
            {
                Exists = PlayerPrefs.HasKey(key), IsString = isString,
                Text = isString ? PlayerPrefs.GetString(key) : null,
                Number = isString ? 0 : PlayerPrefs.GetInt(key),
            });
        }
        private sealed class SavedPreference
        {
            public bool Exists;
            public bool IsString;
            public string Text;
            public int Number;
        }
    }
}
