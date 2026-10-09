using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator CollectionRewards_LegacyBattleTreasureAndRecruitmentEvidenceBackfillsOnce()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, JsonUtility.ToJson(new CoreSeaChartProgress
            {
                charts = new[] { new CoreSeaChartState { seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港"),
                    visits = 1, defeatedBosses = new[] { "boss_" + CoreVoyageSession.Routes[0].Id } } },
            }));
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, JsonUtility.ToJson(new CoreTreasureProgress
            {
                completedSeeds = new[] { "LEGACY-COMPLETE-1", "LEGACY-COMPLETE-2", "LEGACY-COMPLETE-3" },
            }));
            string[] expected = { "first_victory_compass", "first_stormglass", "evelyn_six_port_ruler",
                "empty_berth_registry", "nameless_sailor_nameplate" };
            for (int load = 0; load < 2; load++)
            {
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CollectionAssert.AreEquivalent(expected, controller.WarehouseCollection.OwnedIds);
                Assert.That(controller.Session.CaptureCollectionProgress().completedTreasureRanks, Is.Empty,
                    "Old completed seeds alone cannot prove their map quality.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CollectionRewards_NewGameClearsPreviousCollectionMilestonesAndDisplayRecord()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey, 6);
            PlayerPrefs.SetString(CoreGameplayDemoController.CollectionProgressKey, JsonUtility.ToJson(new CoreCollectionProgress
            {
                defeatedBossIds = new[] { "boss_" + CoreVoyageSession.Routes[0].Id },
            }));
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, "first_victory_compass");
            typeof(MainMenuController).GetMethod("StartNewGame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(controller.Session.CaptureCollectionProgress().defeatedBossIds, Is.Empty);
            Assert.That(controller.Session.CaptureCollectionProgress().completedChartSeeds, Is.Empty);
            Assert.That(controller.Session.CaptureCollectionProgress().completedTreasureSeeds, Is.Empty);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey), Is.Zero);
            Assert.That(controller.IsTutorialActive, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CollectionRewards_BossVictoryPersistsAndClaimsOnceAcrossPortsAndReload()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            session.StartVoyage();
            CoreMapNode boss = session.Map.Nodes.Single(node => node.IsBoss);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, boss);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, boss.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(0), Is.True);
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            RenderResponsiveBattle(controller);
            yield return null;
            var saved = JsonUtility.FromJson<CoreCollectionProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.CollectionProgressKey));
            Assert.That(saved.defeatedBossIds, Does.Contain(boss.Encounter.Id));
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty, "The reward is collected in port, not in battle.");
            Assert.That(session.SafeReturn(), Is.True);
            RenderResponsiveBattle(controller); yield return null;
            Button skip = ActiveButton("Skip Settlement Reveal");
            if (skip != null && skip.IsInteractable()) { ClickAcceptanceButton("Skip Settlement Reveal"); yield return null; }
            ClickAcceptanceButton("Return To Port"); yield return null;
            CollectionAssert.AreEquivalent(new[] { "first_victory_compass", "first_stormglass" }, controller.WarehouseCollection.OwnedIds);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.OwnedIds.Count, Is.EqualTo(2));
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "白贝港");
            RenderResponsiveBattle(controller); yield return null;
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty, "A different port must not reissue the same trophies.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey).Split(',').Length, Is.EqualTo(2));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CollectionRewards_OneProfileClaimsAllFortyAndKeepsDisplayAchievementAfterPortChange()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            string[] ports = CoreVoyageSession.DestinationPorts.ToArray();
            var progress = new CoreCollectionProgress
            {
                defeatedBossIds = CoreVoyageSession.Routes.Select(route => "boss_" + route.Id)
                    .Concat(CoreLegendaryWarships.All.Select(ship => ship.Id)).ToArray(),
                completedChartSeeds = Enumerable.Range(0, 5).Select(i => CoreVoyageSession.CreateChartSeed(i % 3, ports[i], ports[i + 1])).ToArray(),
                completedRouteIds = CoreVoyageSession.Routes.Select(route => route.Id).ToArray(),
                visitedPorts = ports,
                completedTreasureSeeds = Enumerable.Range(0, 8).Select(i => "COLLECTION-FINISHED-" + i).ToArray(),
                completedTreasureRanks = new[] { "weathered", "gilded", "moonlit" },
            };
            Assert.That(new CoreVoyageSession().RestoreCollectionProgress(progress), Is.True, "The saved milestones must be valid.");
            PlayerPrefs.SetString(CoreGameplayDemoController.CollectionProgressKey, JsonUtility.ToJson(progress));
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.OwnedIds.Count, Is.EqualTo(40));
            Assert.That(controller.WarehouseCollection.Owns("seventh_harbor_beacon_lantern"), Is.True);
            ClickWarehouseButton("Port Hotspot Warehouse"); yield return null;
            for (int slot = 0; slot < 6; slot++)
                Assert.That(controller.WarehouseCollection.TryDisplay(CoreWarehouseCollection.Catalog[slot].Id, slot), Is.True);
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey), Is.EqualTo(6));
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "白贝港");
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            RenderResponsiveBattle(controller); yield return null;
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey), Is.EqualTo(6));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey).Split(',').Distinct().Count(), Is.EqualTo(40));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            ClickWarehouseButton("Port Hotspot Warehouse"); yield return null;
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            Assert.That(GameObject.Find("Warehouse Collection Count").GetComponent<Text>().text, Does.Contain("40 / 40"));
            ClickWarehouseButton("Warehouse Treasure first_victory_compass"); yield return null;
            Assert.That(RequireWarehouseButton("Warehouse Display Selected").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Warehouse Inventory State first_victory_compass").GetComponent<Text>().text, Does.Contain("断锚港"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CollectionRewards_CatalogListsAllFortyUnownedRequirementsWithoutGrantingThem()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickWarehouseButton("Port Hotspot Warehouse"); yield return null;
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            var seen = new HashSet<string>();
            for (int page = 0; page < 14; page++)
            {
                Button[] tiles = controller.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Warehouse Treasure ") &&
                    !button.name.StartsWith("Warehouse Treasure Page")).ToArray();
                foreach (Button tile in tiles) seen.Add(tile.name.Substring("Warehouse Treasure ".Length));
                string selectedTile = tiles[0].name;
                ClickWarehouseButton(selectedTile); yield return null;
                Assert.That(RequireWarehouseButton("Warehouse Display Selected").IsInteractable(), Is.False);
                Text source = GameObject.Find("Warehouse Treasure Source").GetComponent<Text>();
                Assert.That(source.text, Is.EqualTo(CoreWarehouseCollection.Catalog.First(item => item.Id == selectedTile.Substring("Warehouse Treasure ".Length)).Source));
                if (page < 13) { ClickWarehouseButton("Warehouse Treasure Page Next"); yield return null; }
            }
            CollectionAssert.AreEquivalent(CoreWarehouseCollection.Catalog.Select(item => item.Id), seen);
            Assert.That(RequireWarehouseButton("Warehouse Treasure Page Next").IsInteractable(), Is.False);
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            NavalBattleVisualCapture.Capture(controller, "Collection_Catalog_Final_Requirement.png", "未获得藏品可查真实获取途径，最终引航灯需其余39件", "Warehouse Panel");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
