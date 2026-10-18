using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CurseRisk_PurificationChargesOneLayerRejectsStaleClicksAndPersists()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.Cargo.Curse = 2;
            session.RestoreProfile(60.4321m, session.CurrentPort);
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            AssertWarehouseHasNoCurseService(controller, 2, 60.4321m);
            foreach (string tab in new[] { "Warehouse Tab Storage", "Warehouse Tab Rare", "Warehouse Tab Valuables" })
            {
                ClickAcceptanceButton(tab); yield return null;
                AssertWarehouseHasNoCurseService(controller, 2, 60.4321m);
            }
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            ClickAcceptanceButton("Port Hotspot Treasure"); yield return null;
            AssertTreasurePurificationStatus(2);
            Button purification = ActiveButton("Treasure Purify Curse");
            Assert.That(purification.IsInteractable(), Is.True);
            Button.ButtonClickedEvent oldPurification = purification.onClick;
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_01_Purification.png",
                "藏宝图桌净化每层支付 30 金币；船长收藏室没有净化入口", "Treasure Workbench");

            ClickAcceptanceButton("Treasure Purify Curse"); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(30.4321m));
            AssertTreasurePurificationStatus(1);
            AssertSavedCurse(session);
            oldPurification.Invoke(); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1), "A retained callback may not purify another layer after the quote changed.");
            Assert.That(session.ProfileGold, Is.EqualTo(30.4321m));

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(30.4321m));
            oldPurification.Invoke(); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1), "A callback from the destroyed controller must not mutate the reloaded save.");
            Assert.That(session.ProfileGold, Is.EqualTo(30.4321m));
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            AssertWarehouseHasNoCurseService(controller, 1, 30.4321m);
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            ClickAcceptanceButton("Port Hotspot Treasure"); yield return null;
            ClickAcceptanceButton("Treasure Purify Curse"); yield return null;
            Assert.That(session.CurseCount, Is.Zero);
            Assert.That(session.ProfileGold, Is.EqualTo(.4321m));
            Button clearedPurification = controller.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Treasure Purify Curse");
            Assert.That(clearedPurification.gameObject.activeInHierarchy, Is.True);
            Assert.That(clearedPurification.interactable, Is.False);
            AssertTreasurePurificationStatus(0);
            clearedPurification.onClick.Invoke(); yield return null;
            Assert.That(session.CurseCount, Is.Zero);
            Assert.That(session.ProfileGold, Is.EqualTo(.4321m));
            AssertSavedCurse(session);

            // A separate unaffordable fixture exercises the rendered disabled
            // button and its callback. It must not save a partial payment.
            session.Cargo.Curse = 1;
            session.RestoreProfile(29.9999m, session.CurrentPort);
            SaveCurseFixture(controller);
            RenderRareEventFixture(controller); yield return null;
            AssertTreasurePurificationStatus(1);
            Assert.That(ActiveButton("Treasure Purify Curse").IsInteractable(), Is.False);
            ActiveButton("Treasure Purify Curse").onClick.Invoke(); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(29.9999m));
            AssertSavedCurse(session);
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_02_InsufficientGold.png",
                "金币不足不能净化，禁用按钮回调也不能扣款或清除诅咒", "Treasure Workbench");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CurseRisk_LegacyGeneratedChoicesPersistThroughReturnCargoStorageAndReload()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            string seed = FindGeneratedCurseSeed();
            session.RestoreTreasureProgress(CurseMapFixture(seed));
            session.Cargo.Water = session.Cargo.Food = 150;
            Assert.That(session.CurseCount, Is.Zero);
            Assert.That(session.Cargo.Cloth, Is.Zero);
            // The fixture supplies only a map and provisions. Both curse stacks
            // and the stored cloth must come from actual generated event buttons.
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.IsTreasureVoyage, Is.True);
            RestoreLegacyTreasureFlow(session);
            string[] eventIds = { "treasure_false_cache", "treasure_smuggler_jetty" };
            int[] choiceIndices = { 0, 1 };
            int cashBefore = session.Cargo.Gold;
            for (int index = 0; index < eventIds.Length; index++)
            {
                CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == eventIds[index]);
                EnterGeneratedCurseEncounter(session, node);
                RenderRareEventFixture(controller); yield return null;
                if (index == 0) yield return DismissTreasureStoryIfPresent(controller);
                yield return WaitForEncounterDossierPresentationCompletion();
                string choiceName = "Encounter Choice " + choiceIndices[index];
                Assert.That(ActiveButton(choiceName).IsInteractable(), Is.True, session.ChoiceUnavailableReason(choiceIndices[index]));
                Assert.That(string.Join(" ", ActiveButton(choiceName).GetComponentsInChildren<Text>().Select(label => label.text)), Does.Contain("诅咒"));
                if (index == 0)
                    NavalBattleVisualCapture.Capture(controller, "CurseRisk_03_RealEvent.png",
                        "真实生成藏宝事件：获得金币同时承担可净化的战斗承伤风险", "Encounter Card");
                ClickAcceptanceButton(choiceName); yield return null;
                Assert.That(node.Visited, Is.True);
                Assert.That(session.CurseCount, Is.EqualTo(index + 1));
                Assert.That(session.CurseDamageBonusPercent, Is.EqualTo((index + 1) * 10));
            }
            Assert.That(session.Cargo.Gold, Is.GreaterThan(cashBefore));
            Assert.That(session.Cargo.Cloth, Is.GreaterThan(0));
            ClickAcceptanceButton("Safe Return"); yield return null;
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            Assert.That(session.CurseCount, Is.EqualTo(2), "Cargo protection must not discount the curse paid for the reward.");
            int storedCloth = session.Cargo.Cloth;
            Assert.That(storedCloth, Is.GreaterThan(0));
            yield return SettlementChestTestActions.ShowOverview();
            ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
            yield return SettlementChestTestActions.ShowOverview();
            ClickAcceptanceButton("Return To Port"); yield return null;
            Assert.That(session.Cargo.Cloth, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "cloth"), Is.EqualTo(storedCloth));
            Assert.That(session.CurseCount, Is.EqualTo(2), "The curse belongs to the expedition, not warehouse cargo.");
            AssertSavedCurse(session);
            decimal goldAfterReturn = session.ProfileGold;
            string warehouseAfterReturn = session.PortWarehouse.Serialize();

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
            Assert.That(session.CurseCount, Is.EqualTo(2));
            Assert.That(session.CurseDamageBonusPercent, Is.EqualTo(20));
            Assert.That(session.ProfileGold, Is.EqualTo(goldAfterReturn));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseAfterReturn));
            Assert.That(session.Cargo.Cloth, Is.Zero);
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            AssertWarehouseHasNoCurseService(controller, 2, goldAfterReturn);
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            ClickAcceptanceButton("Port Hotspot Treasure"); yield return null;
            AssertTreasurePurificationStatus(2);
            ClickAcceptanceButton("Treasure Purify Curse"); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(goldAfterReturn - CoreVoyageSession.CursePurificationCost));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseAfterReturn));
            AssertSavedCurse(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CurseRisk_BattleWarningRemainsVisibleAcrossPreparationNavalAndBoarding()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.Cargo.Curse = 3;
            session.Cargo.Water = session.Cargo.Food = 150;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Start Voyage"); yield return null;
            var encounter = new CoreEncounter("curse-ui-battle", "诅咒战斗回归敌舰", "战斗", "隔离战斗界面夹具", true, false, 2, 90);
            var node = new CoreMapNode { Id = 998, Position = session.Position, Encounter = encounter,
                TerrainConstraint = EventTerrainConstraint.OpenSea };
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            RenderRareEventFixture(controller); yield return null;
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            yield return WaitForCurseBattleFeedback(controller);
            AssertCurseStatus(30);

            ClickAcceptanceButton("End Battle Turn");
            yield return WaitForCurseBattleFeedback(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            AssertCurseStatus(30);
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_04_NavalWarning.png",
                "海战持续提示三层诅咒及承伤增加 30%", "Battle Card");
            ConfigureCloseNavalTestPose(session.Battle, false);
            RenderRareEventFixture(controller); yield return null;
            ClickAcceptanceButton("Battle Board");
            yield return WaitForCurseBattleFeedback(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            AssertCurseStatus(30);
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_05_BoardingWarning.png",
                "接舷阶段仍提示真实的诅咒承伤风险", "Battle Card");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertWarehouseHasNoCurseService(CoreGameplayDemoController controller, int count, decimal gold)
        {
            Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
            Assert.That(controller.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name == "Treasure Curse Panel" || item.name == "Treasure Purify Curse"), Is.False,
                "The captain's collection room must not retain a visible or hidden purification service.");
            Assert.That(controller.Session.CurseCount, Is.EqualTo(count));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }

        private static void AssertTreasurePurificationStatus(int count)
        {
            GameObject status = GameObject.Find("Treasure Purify Status");
            Assert.That(status, Is.Not.Null);
            Text label = status.GetComponent<Text>();
            Assert.That(label, Is.Not.Null);
            if (count == 0) Assert.That(label.text, Is.EqualTo("当前无诅咒"));
            else Assert.That(label.text, Does.Contain(count + " 层").And.Contain("30 金"));
        }

        private static void AssertCurseStatus(int damageBonusPercent)
        {
            GameObject status = GameObject.Find("Status voyage_curse");
            Assert.That(status, Is.Not.Null, "The player status row must retain the voyage curse badge.");
            Assert.That(status.activeInHierarchy, Is.True);
            Text label = status.transform.Find("Amount").GetComponent<Text>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo(damageBonusPercent + "%"));
            Assert.That(NavalHudTooltipText(status.name), Does.Contain("诅咒").And.Contain(damageBonusPercent + "%"));
            Canvas.ForceUpdateCanvases();
            Assert.That(label.rectTransform.rect.width, Is.GreaterThan(0));
            Assert.That(label.rectTransform.rect.height, Is.GreaterThan(0));
        }

        private static void AssertSavedCurse(CoreVoyageSession session)
        {
            var progress = JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey));
            Assert.That(progress, Is.Not.Null);
            Assert.That(progress.curses, Is.EqualTo(session.CurseCount));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(session.ProfileGold));
        }

        private static void SaveCurseFixture(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        private static CoreTreasureProgress CurseMapFixture(string seed) => new CoreTreasureProgress {
            pirateExperience = 1400, selectedMapId = "curse-risk-flow",
            maps = new[] { new CoreTreasureMapSave { id = "curse-risk-flow", seed = seed, rankId = "weathered" } }
        };

        private static string FindGeneratedCurseSeed()
        {
            for (int index = 0; index < 100; index++)
            {
                string seed = "CURSE-RISK-FLOW-" + index;
                var probe = new CoreVoyageSession();
                probe.RestoreTreasureProgress(CurseMapFixture(seed));
                probe.Cargo.Water = probe.Cargo.Food = 150;
                probe.Cargo.Wood = probe.Cargo.Iron = probe.Cargo.Cloth = probe.Cargo.Beer = 0;
                probe.Cargo.Medicine = probe.Cargo.Powder = probe.Cargo.Cannonballs = 0;
                if (!probe.TryStartTreasureVoyage(out _)) continue;
                RestoreLegacyTreasureFlow(probe);
                if (probe.Map.Nodes.Any(node => node.Encounter.Id == "treasure_false_cache") &&
                    probe.Map.Nodes.Any(node => node.Encounter.Id == "treasure_smuggler_jetty")) return seed;
            }
            Assert.Fail("No generated map containing both authored curse rewards was found in the deterministic fixture seed range.");
            return null;
        }

        // Fresh departures promote held maps to the new expedition. These old
        // authored reward chains remain supported only in resumed legacy voyages.
        // Regenerate that version, round-trip its full checkpoint, then exercise
        // the same controller/session and real event, return and storage buttons.
        private static void RestoreLegacyTreasureFlow(CoreVoyageSession session)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            CoreTreasureMapRecord active = session.ActiveTreasureMap;
            Assert.That(active, Is.Not.Null);
            var legacy = (CoreTreasureMapRecord)typeof(CoreTreasureMapRecord).GetConstructors(hidden).Single()
                .Invoke(new object[] { active.Id, active.Seed, active.RankId, active.MapGeometryVersion, 0 });
            var map = (CoreVoyageMap)typeof(CoreVoyageSession).GetMethod("GenerateTreasureMap", hidden)
                .Invoke(session, new object[] { legacy, session.CurrentPort });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(session, map);
            typeof(CoreVoyageSession).GetProperty("ActiveTreasureMap").SetValue(session, legacy);
            CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps.Single(item => item.seed == active.Seed).contentVersion = 0;
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Port);
            Assert.That(session.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(
                JsonUtility.ToJson(checkpoint)), out string reason), Is.True, reason);
            Assert.That(session.ActiveTreasureMap.ContentVersion, Is.Zero);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        private static void EnterGeneratedCurseEncounter(CoreVoyageSession session, CoreMapNode node)
        {
            // Navigation is skipped; generation, costs, rewards and UI choices
            // still execute production code, without inserting a curse directly.
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }

        private static IEnumerator WaitForCurseBattleFeedback(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            yield return null;
        }
    }
}
