using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        private bool hadTutorialCompleted;
        private bool hadTutorialReward;
        private bool hadProfileGold;
        private bool hadCurrentPort;
        private int savedTutorialCompleted;
        private int savedTutorialReward;
        private int savedProfileGold;
        private string savedCurrentPort;
        private readonly Dictionary<string, int> savedMetaInts = new Dictionary<string, int>();
        private readonly Dictionary<string, string> savedMetaStrings = new Dictionary<string, string>();
        private readonly Dictionary<string, int> savedComponentPurchases = new Dictionary<string, int>();

        [SetUp]
        public void PreserveAndResetProgress()
        {
            IsolateVoyageCheckpointDirectory();
            hadTutorialCompleted = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey);
            hadTutorialReward = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialRewardGrantedKey);
            hadProfileGold = PlayerPrefs.HasKey(CoreGameplayDemoController.ProfileGoldKey);
            hadCurrentPort = PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey);
            savedTutorialCompleted = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            savedTutorialReward = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialRewardGrantedKey, 0);
            savedProfileGold = PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey, 500);
            savedCurrentPort = PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PreserveMetaProgression();
            PreserveComponentPurchases();
            ResetProgress();
        }

        [TearDown]
        public void RestoreProgress()
        {
            ResetProgress();
            if (hadTutorialCompleted) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, savedTutorialCompleted);
            if (hadTutorialReward) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialRewardGrantedKey, savedTutorialReward);
            if (hadProfileGold) PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, savedProfileGold);
            if (hadCurrentPort) PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, savedCurrentPort);
            foreach (KeyValuePair<string, int> entry in savedMetaInts) PlayerPrefs.SetInt(entry.Key, entry.Value);
            foreach (KeyValuePair<string, string> entry in savedMetaStrings) PlayerPrefs.SetString(entry.Key, entry.Value);
            foreach (KeyValuePair<string, int> entry in savedComponentPurchases) PlayerPrefs.SetInt(entry.Key, entry.Value);
            PlayerPrefs.Save();
            RestoreVoyageCheckpointDirectory();
        }

        [UnityTest]
        public IEnumerator MainMenu_LoadsGeneratedArtAndFunctionalPanels()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(controller, Is.Not.Null);
            RawImage background = GameObject.Find("Main Menu Background")?.GetComponent<RawImage>();
            Assert.That(background, Is.Not.Null);
            Assert.That(background.texture, Is.Not.Null, "The generated main-menu artwork should load from Resources.");
            Assert.That(ActiveButton("New Game"), Is.Not.Null);
            Assert.That(ActiveButton("Continue Game"), Is.Not.Null);
            Assert.That(ActiveButton("Settings"), Is.Not.Null);
            Assert.That(ActiveButton("Achievements"), Is.Not.Null);
            Assert.That(ActiveButton("Exit Game"), Is.Not.Null);
            Assert.That(controller.ContinueButton.interactable, Is.False);

            ActiveButton("Settings").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Settings Panel"), Is.Not.Null);
            Assert.That(ActiveButton("Apply Settings"), Is.Not.Null);
            ActiveButton("Back From Settings").onClick.Invoke();
            yield return null;

            ActiveButton("Achievements").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Achievements Panel"), Is.Not.Null);
            Assert.That(ActiveButton("Back From Achievements"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ExistingSave_EnablesContinueAndLoadsUnifiedGame()
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "2400.25");
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(675);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleDeckKey, "0,0,0,1,2,3,4,5");
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Preparation), "8,8,9,9,10,10,11,12");
            PlayerPrefs.SetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Cannon), "cannon_bronze_longgun");
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Sail), "sail_last_free_wind");
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 123);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, CoreWarehouseCollection.TutorialTreasureId);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, ",,," + CoreWarehouseCollection.TutorialTreasureId + ",,");
            PlayerPrefs.Save();

            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.HasSaveData, Is.True);
            Assert.That(controller.ContinueButton.interactable, Is.True);
            controller.ContinueButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoreGameplayDemo"));
            CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.IsTutorialActive, Is.False);
            Assert.That(game.Session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(game.Session.ProfileGold, Is.EqualTo(675));
            Assert.That(game.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(game.Session.HasNavigator, Is.True);
            Assert.That(game.EquippedComponent(ComponentSlot.Cannon).Id, Is.EqualTo("cannon_bronze_longgun"),
                "Continuing an old save must retain stable component IDs rather than treating them as old catalog indexes.");
            Assert.That(game.EquippedComponent(ComponentSlot.Sail).Id, Is.EqualTo("sail_last_free_wind"),
                "An expanded-catalog component must survive save restoration too.");
            Assert.That(game.Session.BattleDeck.Count, Is.EqualTo(8));
            Assert.That(game.Session.GetBattleDeck(NavalBattleStage.Preparation), Is.EqualTo(new[] { 8, 8, 9, 9, 10, 10, 11, 12 }),
                "A separately saved stage takes precedence over the old combined deck.");
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.LegacyWitchRecruitedKey), Is.False,
                "Continuing a historical save must remove the retired placeholder recruitment flag.");
            Assert.That(CoreVoyageSession.CardLibrary.Any(card => card.Id.StartsWith("card_witch_") || card.RequiredCrew == "crew_witch_jinchao"), Is.False);
            Assert.That(game.Session.Cargo.Water, Is.EqualTo(123));
            Assert.That(game.Session.ElapsedGameHours, Is.EqualTo(2400.25d));
            Assert.That(game.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.True);
            Assert.That(game.WarehouseCollection.DisplayedAt(3), Is.EqualTo(CoreWarehouseCollection.TutorialTreasureId));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NewGame_ConfirmsAndResetsExistingProgress()
        {
            foreach (string key in AchievementCatalog.PersistenceKeys) PlayerPrefs.SetInt(key, 1);
            string oldScope = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, oldScope);
            PlayerPrefs.SetString(CoreGameplayDemoController.GameClockKey, "2400.25");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentPurchaseAchievementKey, 1);
            string oldFirstMonth = CoreGameplayDemoController.MarketPurchaseKey("断锚港", CoreVoyageSession.GameClockOrigin, PortMarketItem.Water);
            string oldLaterMonth = CoreGameplayDemoController.MarketPurchaseKey("断锚港", CoreVoyageSession.GameClockOrigin.AddMonths(3), PortMarketItem.Water);
            PlayerPrefs.SetInt(oldFirstMonth, 6000);
            PlayerPrefs.SetInt(oldLaterMonth, 6000);
            PortComponentShopStockItem oldFirstOffer = PortComponentShopCatalog.BuildExpandedStock(new ComponentArtCatalog(), "断锚港", CoreVoyageSession.GameClockOrigin)[0];
            string oldComponentKey = CoreGameplayDemoController.ComponentShopPurchaseKey(oldFirstOffer.Id);
            PlayerPrefs.SetInt(oldComponentKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialRewardGrantedKey, 1);
            ProfileGoldPersistence.Save(900);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 2);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(2), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(CoreVoyageSession.Ships.Count - 1), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            CrewVariantDefinition regionalCrew = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(regionalCrew.Id), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(0), regionalCrew.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewIdentityKey, regionalCrew.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Bow), "bow_last_horizon");
            ComponentArtCatalog oldCatalog = new ComponentArtCatalog();
            OwnedShipComponentInventory oldInventory = new OwnedShipComponentInventory(oldCatalog);
            oldInventory.EnsureLoadout(2);
            oldInventory.Grant(oldCatalog.GetById("bow_last_horizon"), 1.1f);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, oldInventory.Export());
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationKey, 7);
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleDeckKey, "0,0,0,1,2,3,4,5");
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Preparation), "8,8,9,9,10,10,11,12");
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval), "0,0,0,1,2,5,6,7");
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Boarding), "3,3,4,4,14,14,15,15");
            PlayerPrefs.SetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey, 1);
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentConditionKey(slot), 37);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 999);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, CoreWarehouseCollection.TutorialTreasureId);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, CoreWarehouseCollection.TutorialTreasureId + ",,,,,");
            PlayerPrefs.SetInt(CoreGameplayDemoController.WarehousePortMigrationKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseStorageModeKey, (int)MarketPurchaseStorageMode.Ask);
            PlayerPrefs.SetString(CoreGameplayDemoController.PortWarehouseCargoKey, "v1;白贝港,wood,90");
            string oldChart = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, JsonUtility.ToJson(new CoreSeaChartProgress
            {
                charts = new[] { new CoreSeaChartState { seed = oldChart, visits = 3 } }
            }));
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, oldChart);
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedPortKey(port), CoreWarehouseCollection.TutorialTreasureId);
                PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayPortKey(port), CoreWarehouseCollection.TutorialTreasureId + ",,,,,");
            }
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey, 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            ActiveButton("New Game").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("New Game Confirmation"), Is.Not.Null);
            System.DateTime startDateBefore = CoreVoyageSession.NewCampaignStartDate(System.DateTime.Now);
            ActiveButton("Confirm New Game").onClick.Invoke();
            yield return null;
            yield return null;
            System.DateTime startDateAfter = CoreVoyageSession.NewCampaignStartDate(System.DateTime.Now);

            CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.IsTutorialActive, Is.True);
            Assert.That(game.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.FirstDeparture));
            foreach (string key in AchievementCatalog.PersistenceKeys)
                Assert.That(PlayerPrefs.HasKey(key), Is.False, "A new save clears the prior captain's achievement: " + key);
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(ActiveButton("Tutorial Primary"), Is.Null);
            Assert.That(game.Session.CurrentPort, Is.EqualTo("断锚港"));
            Assert.That(game.Session.ProfileGold, Is.EqualTo(500));
            Assert.That(game.Session.GameDateTime == startDateBefore || game.Session.GameDateTime == startDateAfter, Is.True,
                "A new campaign uses the current local date mapped into the game calendar, including a midnight rollover.");
            Assert.That(game.Session.ElapsedGameHours,
                Is.EqualTo((game.Session.GameDateTime - CoreVoyageSession.GameClockOrigin).TotalHours));
            Assert.That(CoreGameplayDemoController.SavedGameDateTime(), Is.EqualTo(game.Session.GameDateTime));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey), Is.Not.EqualTo(oldScope));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentPurchaseAchievementKey), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", CoreVoyageSession.GameClockOrigin, PortMarketItem.Water)), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", CoreVoyageSession.GameClockOrigin.AddMonths(3), PortMarketItem.Water)), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldFirstOffer.Id)), Is.Zero);
            PlayerPrefs.DeleteKey(oldFirstMonth);
            PlayerPrefs.DeleteKey(oldLaterMonth);
            PlayerPrefs.DeleteKey(oldComponentKey);
            Assert.That(game.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Ship));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.MarketPurchaseStorageModeKey), Is.False);
            Assert.That(game.Session.CaptureSeaChartProgress().charts, Is.Empty);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey), Does.Not.Contain(oldChart));
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(port), string.Empty), Is.Empty);
                Assert.That(game.Session.PortWarehouse.GetAmount(port, "wood"), Is.Zero);
            }
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.SelectedShipKey), Is.False);
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ShipOwnedKey(ship)), Is.False, "New game clears owned hull " + ship);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CrewRecruitmentKey(0)), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CrewVariantRecruitmentKey(regionalCrew.Id)), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ActiveCrewVariantKey(0)), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchCrewIdentityKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Bow)), Is.False);
            Assert.That(game.EquippedComponent(ComponentSlot.Bow).Id, Is.EqualTo("bow_oak_ram"));
            Assert.That(game.OwnedComponents.Count, Is.EqualTo(5));
            Assert.That(game.OwnedComponents.All(item => item.ShipIndex == 0 && Mathf.Approximately(item.Quality, 1f)), Is.True);
            Assert.That(game.OwnedComponents.Any(item => item.ComponentId == "bow_last_horizon"), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchDurationKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.BattleDeckKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.LegacyWitchRecruitedKey), Is.False);
            Assert.That(CoreVoyageSession.CardLibrary.Any(card => card.Id.StartsWith("card_witch_") || card.RequiredCrew == "crew_witch_jinchao"), Is.False);
            CoreVoyageSession defaults = new CoreVoyageSession();
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                Assert.That(game.Session.GetBattleDeck(stage), Is.EqualTo(defaults.GetBattleDeck(stage)),
                    "A new voyage profile must not inherit the previous captain's stage deck.");
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage)),
                    Is.EqualTo(string.Join(",", defaults.GetBattleDeck(stage))));
            }
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentConditionKey(slot), 100), Is.EqualTo(100));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CargoKey("Water")), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey), Is.False);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Empty), Is.Empty);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey, string.Empty)
                .Replace(",", string.Empty), Is.Empty);
            Assert.That(game.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(game.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static Button ActiveButton(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(button => button.name == name);
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialRewardGrantedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ProfileGoldKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CurrentPortKey);
            foreach (string key in MetaIntKeys()) PlayerPrefs.DeleteKey(key);
            foreach (string key in MetaStringKeys()) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private void PreserveMetaProgression()
        {
            savedMetaInts.Clear();
            savedMetaStrings.Clear();
            foreach (string key in MetaIntKeys())
            {
                if (PlayerPrefs.HasKey(key)) savedMetaInts[key] = PlayerPrefs.GetInt(key, 0);
            }
            foreach (string key in MetaStringKeys())
            {
                if (PlayerPrefs.HasKey(key)) savedMetaStrings[key] = PlayerPrefs.GetString(key, string.Empty);
            }
        }

        private static IEnumerable<string> MetaIntKeys()
        {
            foreach (string key in AchievementCatalog.PersistenceKeys) yield return key;
            yield return CoreGameplayDemoController.ComponentPurchaseAchievementKey;
            yield return CoreGameplayDemoController.MarketPurchaseStorageModeKey;
            yield return CoreGameplayDemoController.WarehousePortMigrationKey;
            yield return CoreGameplayDemoController.GuidedTutorialCheckpointKey;
            yield return CoreGameplayDemoController.GuidedTutorialDeckEditedKey;
            yield return CoreGameplayDemoController.GuidedTutorialComponentRewardKey;
            yield return CoreGameplayDemoController.GuidedTutorialRecoveryKey;
            yield return CoreGameplayDemoController.GuidedTutorialNavigatorEventKey;
            yield return CoreGameplayDemoController.GuidedTutorialWarehousePendingKey;
            yield return CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey;
            yield return CoreGameplayDemoController.SelectedShipKey;
            yield return CoreGameplayDemoController.DispatchMissionKey;
            yield return CoreGameplayDemoController.DispatchDurationKey;
            yield return CoreGameplayDemoController.DispatchBalanceVersionKey;
            yield return CoreGameplayDemoController.DispatchGameClockKey;
            yield return CoreGameplayDemoController.DispatchRealClockKey;
            yield return CoreGameplayDemoController.LegacyWitchRecruitedKey;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                yield return CoreGameplayDemoController.PortInvestmentKey(port);
                yield return CoreGameplayDemoController.GameMasterShopRefreshKey(port);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    yield return CoreGameplayDemoController.MarketPurchaseKey(port, CoreVoyageSession.GameClockOrigin, item);
            }
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                yield return CoreGameplayDemoController.ComponentConditionKey(slot);
            for (int index = 0; index < 6; index += 1) yield return CoreGameplayDemoController.CrewRecruitmentKey(index);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew) yield return CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id);
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index += 1) yield return CoreGameplayDemoController.ShipOwnedKey(index);
            string[] cargo = { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" };
            for (int index = 0; index < cargo.Length; index += 1) yield return CoreGameplayDemoController.CargoKey(cargo[index]);
        }

        private static IEnumerable<string> MetaStringKeys()
        {
            yield return CoreGameplayDemoController.GameClockKey;
            yield return CoreGameplayDemoController.WorldEconomyScopeKey;
            yield return ProfileGoldPersistence.DecimalKey;
            yield return CoreGameplayDemoController.TreasureProgressKey;
            yield return CoreGameplayDemoController.GameMasterShopScopeKey;
            yield return CoreGameplayDemoController.CrewAtSeaKey;
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                yield return CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id);
            yield return CoreGameplayDemoController.PortWarehouseCargoKey;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                yield return CoreGameplayDemoController.WarehouseOwnedPortKey(port);
                yield return CoreGameplayDemoController.WarehouseDisplayPortKey(port);
            }
            yield return CoreGameplayDemoController.ComponentInventoryKey;
            yield return CoreGameplayDemoController.DrawnSeaChartKey;
            yield return CoreGameplayDemoController.SeaChartProgressKey;
            yield return CoreGameplayDemoController.RecordedSeaChartsKey;
            yield return CoreGameplayDemoController.WarehouseOwnedKey;
            yield return CoreGameplayDemoController.WarehouseDisplayKey;
            yield return CoreGameplayDemoController.BattleDeckKey;
            yield return CoreGameplayDemoController.BattleCardUpgradeKey;
            yield return CoreGameplayDemoController.SpecialCardExperienceKey;
            yield return CoreGameplayDemoController.AcquiredSpecialCardsKey;
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                yield return CoreGameplayDemoController.StageBattleDeckKey(stage);
            yield return CoreGameplayDemoController.DispatchEndsAtKey;
            yield return CoreGameplayDemoController.DispatchCrewKey;
            yield return CoreGameplayDemoController.DispatchCrewIdentityKey;
            for (int index = 0; index < 6; index += 1) yield return CoreGameplayDemoController.ActiveCrewVariantKey(index);
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                yield return CoreGameplayDemoController.ComponentLoadoutKey(slot);
        }

        private void PreserveComponentPurchases()
        {
            savedComponentPurchases.Clear();
            ComponentArtCatalog art = new ComponentArtCatalog();
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            foreach (string port in ports)
            {
                foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(art, port, CoreVoyageSession.GameClockOrigin))
                {
                    string key = CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id);
                    if (PlayerPrefs.HasKey(key)) savedComponentPurchases[key] = PlayerPrefs.GetInt(key, 0);
                }
            }
        }
    }
}
