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
        public IEnumerator CollectionRewards_LegacyMilestonesBackfillWithoutTreatingRecruitmentAsCardTraining()
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
            string[] expected = { "first_victory_compass", "first_stormglass",
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
        public IEnumerator CollectionRewards_RecruitedCrewRequiresOwnFullyUpgradedCardAndClaimsOnceAcrossReload()
        {
            yield return VerifyRecruitmentCollectionCardLevels("crew_broken_anchor_navigator_lorne", "lorne_first_berth_token");
        }

        [UnityTest]
        public IEnumerator CollectionRewards_PepeRequiresHisNewCharacterCardAtItsAuthoredMaximum()
        {
            yield return VerifyRecruitmentCollectionCardLevels("crew_gray_gull_cook_pepe", "pepe_shared_pot_lid");
        }

        [UnityTest]
        public IEnumerator CollectionRewards_HelenRequiresAllThreeAuthoredUpgradesAndClaimsOnceAcrossReload()
        {
            yield return VerifyRecruitmentCollectionCardLevels("crew_bronze_bell_gunner_helen", "helen_thirteenth_cannon_coin");
        }

        [UnityTest]
        public IEnumerator CollectionRewards_PreviouslyOwnedCrewKeepsakeSurvivesUntrainedCardAndReload()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey("crew_broken_anchor_navigator_lorne"), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, "lorne_first_berth_token");
            for (int load = 0; load < 2; load++)
            {
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.GetCardUpgradeLevel(CoreSpecialCardCatalog.ForCrew("crew_broken_anchor_navigator_lorne").Card.Id), Is.Zero);
                CollectionAssert.AreEquivalent(new[] { "lorne_first_berth_token" }, controller.WarehouseCollection.OwnedIds);
                typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator VerifyRecruitmentCollectionCardLevels(string crewId, string treasureId)
        {
            CrewVariantDefinition crew = CrewNarrativeCatalog.FindById(crewId);
            CoreSpecialCardDefinition card = CoreSpecialCardCatalog.ForCrew(crewId);
            Assert.That(card, Is.Not.Null, "Every recruitment keepsake must have an obtainable character card.");
            int maximumLevel = new CoreVoyageSession().GetCardMaximumUpgradeLevel(card.Card.Id);
            int requiredExperience = new[] { 0, 120, 360, 840 }[maximumLevel];
            CoreSpecialCardDefinition other = CoreSpecialCardCatalog.ForCrew("crew_white_shell_navigator_saina");
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, crew.PortName);
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, other.Card.Id + "=840");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty, "A trained card cannot replace recruitment.");
            typeof(CoreGameplayDemoController).GetMethod("RecruitTavernCrew", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { crew.ProfessionIndex, crew });
            yield return null;
            Assert.That(controller.Session.AcquiredSpecialCardIds, Does.Contain(card.Card.Id));
            Assert.That(controller.Session.GetCardUpgradeLevel(card.Card.Id), Is.Zero);
            Assert.That(controller.WarehouseCollection.Owns(treasureId), Is.False, "Recruitment alone and another character's level three card are insufficient.");
            foreach (int experience in new[] { 119, 120, 359, 360, 839 }.Where(value => value < requiredExperience))
            {
                controller.Session.RestoreSpecialCardExperience(new Dictionary<string, int> { [card.Card.Id] = experience });
                RenderResponsiveBattle(controller); yield return null;
                Assert.That(controller.Session.GetCardUpgradeLevel(card.Card.Id), Is.EqualTo(experience < 120 ? 0 : experience < 360 ? 1 : 2));
                Assert.That(controller.WarehouseCollection.Owns(treasureId), Is.False, "Card experience " + experience + " cannot unlock the keepsake.");
            }
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.GetCardUpgradeLevel(card.Card.Id), Is.EqualTo(maximumLevel - 1));
            Assert.That(controller.WarehouseCollection.Owns(treasureId), Is.False, "Reload must preserve the incomplete card requirement.");
            controller.Session.RestoreSpecialCardExperience(new Dictionary<string, int> { [card.Card.Id] = requiredExperience });
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            CollectionAssert.AreEquivalent(new[] { treasureId }, controller.WarehouseCollection.OwnedIds);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey), Does.Contain(card.Card.Id + "=" + requiredExperience));
            for (int load = 0; load < 2; load++)
            {
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.GetCardUpgradeLevel(card.Card.Id), Is.EqualTo(maximumLevel));
                CollectionAssert.AreEquivalent(new[] { treasureId }, controller.WarehouseCollection.OwnedIds);
            }
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "白贝港");
            RenderResponsiveBattle(controller); yield return null;
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty, "A second port must not reissue the same character keepsake.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(crew.PortName)), Is.EqualTo(treasureId));
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
                defeatedBossIds = CoreRouteBossCatalog.All.Select(boss => boss.Id)
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
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, string.Join(";", CoreSpecialCardCatalog.All
                .Where(card => card.SourceKind == CoreSpecialCardSourceKind.Crew).Select(card => card.Card.Id + "=840")));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.OwnedIds.Count, Is.EqualTo(40));
            Assert.That(controller.WarehouseCollection.Owns("seventh_harbor_beacon_lantern"), Is.True);
            string receivingPort = controller.Session.CurrentPort;
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
            Dictionary<string, string> collectionBefore = CaptureCatalogWarehouseSave();
            decimal goldBefore = controller.Session.ProfileGold;
            int[] cargoBefore = WarehouseCargoSnapshot(controller);
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            var seen = new HashSet<string>();
            for (int page = 0; page < 5; page++)
            {
                AssertCatalogListPage(controller, page, "40 / 40", receivingPort, seen);
                if (page == 0)
                    NavalBattleVisualCapture.Capture(controller, "Collection_Catalog_All_Owned_Other_Port.png",
                        "当前港没有本港藏品；全存档40件均保留获得港归属，图鉴直接列出解锁方式", "Warehouse Collection Catalog Panel");
                if (page < 4) { ClickWarehouseButton("Warehouse Catalog Page Next"); yield return null; }
            }
            CollectionAssert.AreEquivalent(CoreWarehouseCollection.Catalog.Select(item => item.Id), seen);
            AssertCatalogWarehouseSaveUnchanged(collectionBefore);
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBefore));
            CollectionAssert.AreEqual(cargoBefore, WarehouseCargoSnapshot(controller));
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Null);
            Assert.That(GameObject.Find("Warehouse Catalog Row first_victory_compass"), Is.Null);
            Assert.That(GameObject.Find("Warehouse Collection Count").GetComponent<Text>().text, Does.Contain("白贝港珍藏 0 件"));
            Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo("1 / 1"));
            Assert.That(RequireWarehouseButton("Warehouse Display Selected").IsInteractable(), Is.False);
            Assert.That(RequireWarehouseButton("Warehouse Remove Selected").IsInteractable(), Is.False);
            Assert.That(ActiveButton("Warehouse Treasure first_victory_compass"), Is.Null);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(receivingPort)).Split(',').Length, Is.EqualTo(40));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey), Is.EqualTo(6));
            AssertCatalogWarehouseSaveUnchanged(collectionBefore);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CollectionRewards_CatalogListsAllFortyUnownedRequirementsWithoutGrantingThem()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickWarehouseButton("Port Hotspot Warehouse"); yield return null;
            Dictionary<string, string> collectionBefore = CaptureCatalogWarehouseSave();
            decimal goldBefore = controller.Session.ProfileGold;
            int hullBefore = controller.Session.Hull;
            int[] cargoBefore = WarehouseCargoSnapshot(controller);
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            Button.ButtonClickedEvent oldNextPage = RequireWarehouseButton("Warehouse Catalog Page Next").onClick;
            Button.ButtonClickedEvent oldReturn = RequireWarehouseButton("Warehouse Collection Catalog").onClick;
            var seen = new HashSet<string>();
            for (int page = 0; page < 5; page++)
            {
                AssertCatalogListPage(controller, page, "0 / 40", null, seen);
                if (page == 0)
                    NavalBattleVisualCapture.Capture(controller, "Collection_Catalog_First_Page.png",
                        "藏品图鉴首页：左侧图标与名称，右侧直接显示真实解锁方式，不展示简介或故事", "Warehouse Collection Catalog Panel");
                if (page == 2 || page == 3)
                    NavalBattleVisualCapture.Capture(controller, "Collection_Catalog_Crew_Page_" + (page + 1) + ".png",
                        "招募类藏品要求对应角色卡牌升至三级；解锁方式完整位于折痕右侧", "Warehouse Collection Catalog Panel");
                if (page == 1)
                {
                    oldNextPage.Invoke(); oldReturn.Invoke(); yield return null;
                    Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Not.Null,
                        "An event retained from the first page must not close the refreshed catalog.");
                    Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo("2 / 5"),
                        "An event retained from the first page must not advance the refreshed catalog.");
                }
                if (page < 4) { ClickWarehouseButton("Warehouse Catalog Page Next"); yield return null; }
            }
            CollectionAssert.AreEquivalent(CoreWarehouseCollection.Catalog.Select(item => item.Id), seen);
            Assert.That(RequireWarehouseButton("Warehouse Catalog Page Next").IsInteractable(), Is.False);
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBefore));
            Assert.That(controller.Session.Hull, Is.EqualTo(hullBefore));
            CollectionAssert.AreEqual(cargoBefore, WarehouseCargoSnapshot(controller));
            AssertCatalogWarehouseSaveUnchanged(collectionBefore);
            NavalBattleVisualCapture.Capture(controller, "Collection_Catalog_Final_Requirement.png",
                "藏品图鉴末页：最终引航灯需其余39件，八行解锁方式完整呈现；浏览不会授予藏品", "Warehouse Collection Catalog Panel");
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Null);
            Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo("1 / 1"));
            Assert.That(RequireWarehouseButton("Warehouse Display Selected").IsInteractable(), Is.False);
            Assert.That(RequireWarehouseButton("Warehouse Remove Selected").IsInteractable(), Is.False);
            ClickWarehouseButton("Warehouse Collection Catalog"); yield return null;
            Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo("1 / 5"));
            oldNextPage.Invoke(); oldReturn.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo("1 / 5"));
            AssertCatalogWarehouseSaveUnchanged(collectionBefore);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertCatalogListPage(CoreGameplayDemoController controller, int page,
            string count, string ownerPort, HashSet<string> seen)
        {
            GameObject catalog = GameObject.Find("Warehouse Collection Catalog Panel");
            Assert.That(catalog, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Assert.That(GameObject.Find("Warehouse Catalog Count").GetComponent<Text>().text, Does.Contain(count));
            Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo((page + 1) + " / 5"));
            Assert.That(RequireWarehouseButton("Warehouse Catalog Page Previous").IsInteractable(), Is.EqualTo(page > 0));
            Assert.That(RequireWarehouseButton("Warehouse Catalog Page Next").IsInteractable(), Is.EqualTo(page < 4));
            RectTransform paper = catalog.GetComponent<RectTransform>();
            Rect sourceColumn = CatalogBounds(paper, GameObject.Find("Warehouse Catalog Source Column").GetComponent<RectTransform>());
            Assert.That(sourceColumn.xMin, Is.GreaterThan(0f), "The unlock heading must sit to the right of the parchment fold.");
            Rect previous = CatalogBounds(paper, RequireWarehouseButton("Warehouse Catalog Page Previous").GetComponent<RectTransform>());
            Rect pageNumber = CatalogBounds(paper, GameObject.Find("Warehouse Catalog Page").GetComponent<RectTransform>());
            Rect next = CatalogBounds(paper, RequireWarehouseButton("Warehouse Catalog Page Next").GetComponent<RectTransform>());
            Rect back = CatalogBounds(paper, RequireWarehouseButton("Warehouse Collection Catalog").GetComponent<RectTransform>());
            Assert.That(previous.xMax, Is.LessThan(pageNumber.xMin));
            Assert.That(pageNumber.xMax, Is.LessThan(next.xMin));
            Assert.That(next.xMax, Is.LessThan(0f), "Both page buttons and the page number must stay left of the fold.");
            Assert.That(back.xMin, Is.GreaterThan(0f));
            Assert.That(back.center.x, Is.EqualTo(sourceColumn.center.x).Within(.5f), "Return must be centred under the right-hand reading column.");
            Assert.That(back.center.y, Is.EqualTo(previous.center.y).Within(.5f));
            foreach (string oldDetail in new[] { "Warehouse Collection Hint", "Warehouse Treasure Title",
                "Warehouse Treasure Source", "Warehouse Treasure Story", "Warehouse Display Selected", "Warehouse Remove Selected" })
                Assert.That(GameObject.Find(oldDetail), Is.Null, "The catalog must hide the old keepsake detail: " + oldDetail);

            const string rowPrefix = "Warehouse Catalog Row ";
            RectTransform[] rows = catalog.GetComponentsInChildren<RectTransform>()
                .Where(row => row.parent == catalog.transform && row.name.StartsWith(rowPrefix)).ToArray();
            CoreWarehouseTreasureDefinition[] expected = CoreWarehouseCollection.Catalog.Skip(page * 8).Take(8).ToArray();
            CollectionAssert.AreEquivalent(expected.Select(item => item.Id), rows.Select(row => row.name.Substring(rowPrefix.Length)));
            Assert.That(rows.Length, Is.EqualTo(8));
            foreach (CoreWarehouseTreasureDefinition treasure in expected)
            {
                Assert.That(seen.Add(treasure.Id), Is.True, "Every catalog entry must occur on exactly one page: " + treasure.Id);
                Transform row = rows.Single(item => item.name == rowPrefix + treasure.Id);
                Text name = row.GetComponentsInChildren<Text>().Single(label => label.name == "Warehouse Catalog Name " + treasure.Id);
                Text source = row.GetComponentsInChildren<Text>().Single(label => label.name == "Warehouse Catalog Source " + treasure.Id);
                Text state = row.GetComponentsInChildren<Text>().Single(label => label.name == "Warehouse Catalog State " + treasure.Id);
                Assert.That(name.text, Is.EqualTo(treasure.Name));
                string normalized = source.text.Replace("\n", string.Empty);
                Assert.That(normalized, Is.EqualTo(treasure.Source));
                Assert.That(normalized, Is.EqualTo(CoreCollectionRewardCatalog.Find(treasure.Id).Source));
                Assert.That(source.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(normalized.Length),
                    "The entire unlock requirement must be visible: " + treasure.Id);
                Rect sourceBounds = CatalogBounds(paper, source.rectTransform);
                Assert.That(sourceBounds.xMin, Is.GreaterThan(0f), "Unlock text must not cross the parchment fold: " + treasure.Id);
                Assert.That(sourceBounds.xMax, Is.LessThan(paper.rect.xMax));
                Assert.That(sourceBounds.yMin, Is.GreaterThan(back.yMax), "The final requirement must clear the paging controls.");
                if (ownerPort == null) Assert.That(state.text, Does.Contain("未获得"));
                else Assert.That(state.text, Does.Contain(ownerPort), "An owned item must keep its receiving port: " + treasure.Id);
                RawImage icon = row.GetComponentsInChildren<RawImage>().Single(image => image.name == "Warehouse Catalog Icon " + treasure.Id);
                ItemIconArt art = ItemIconCatalog.ForTreasure(treasure);
                Assert.That(art.IsAvailable, Is.True, "Every keepsake must have an icon: " + treasure.Id);
                Assert.That(icon.enabled, Is.True);
                Assert.That(icon.texture, Is.SameAs(art.Texture));
                Assert.That(icon.uvRect, Is.EqualTo(art.Uv));
                Assert.That(icon.raycastTarget, Is.False);
                foreach (Text label in new[] { name, source, state }) Assert.That(label.raycastTarget, Is.False);
                foreach (Text label in row.GetComponentsInChildren<Text>())
                {
                    Assert.That(label.text, Is.Not.EqualTo(treasure.Description), "Catalog rows must omit the description.");
                    Assert.That(label.text, Is.Not.EqualTo(treasure.Story), "Catalog rows must omit the story.");
                }
            }
            Assert.That(controller.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Warehouse Treasure ") &&
                !button.name.StartsWith("Warehouse Treasure Page")), Is.False, "The old three-column cards must be inactive in catalog mode.");
        }

        private static Rect CatalogBounds(RectTransform paper, RectTransform item)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector3 minimum = paper.InverseTransformPoint(corners[0]);
            Vector3 maximum = paper.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static Dictionary<string, string> CaptureCatalogWarehouseSave()
        {
            var result = new Dictionary<string, string>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
                foreach (string key in new[] { CoreGameplayDemoController.WarehouseOwnedPortKey(port),
                    CoreGameplayDemoController.WarehouseDisplayPortKey(port) })
                    result[key] = PlayerPrefs.GetString(key, string.Empty);
            foreach (string key in new[] { CoreGameplayDemoController.WarehouseOwnedKey, CoreGameplayDemoController.WarehouseDisplayKey,
                CoreGameplayDemoController.CollectionProgressKey, CoreGameplayDemoController.PortWarehouseCargoKey })
                result[key] = PlayerPrefs.GetString(key, string.Empty);
            return result;
        }

        private static void AssertCatalogWarehouseSaveUnchanged(Dictionary<string, string> before)
        {
            foreach (KeyValuePair<string, string> pair in before)
                Assert.That(PlayerPrefs.GetString(pair.Key, string.Empty), Is.EqualTo(pair.Value), "Reading the catalog must preserve " + pair.Key);
        }
    }
}
