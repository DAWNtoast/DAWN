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
            AssertCurseStatus("Treasure Curse Status", 20);
            Button purification = ActiveButton("Treasure Purify Curse");
            Assert.That(purification.IsInteractable(), Is.True);
            Assert.That(purification.GetComponentInChildren<Text>().text, Does.Contain("30"));
            Button.ButtonClickedEvent oldPurification = purification.onClick;
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_01_Purification.png",
                "诅咒两层使战斗承伤增加 20%；净化每层支付 30 金币", "Treasure Curse Panel");

            ClickAcceptanceButton("Treasure Purify Curse"); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(30.4321m));
            AssertCurseStatus("Treasure Curse Status", 10);
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
            ClickAcceptanceButton("Treasure Purify Curse"); yield return null;
            Assert.That(session.CurseCount, Is.Zero);
            Assert.That(session.ProfileGold, Is.EqualTo(.4321m));
            Button clearedPurification = controller.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Treasure Purify Curse");
            Assert.That(clearedPurification.gameObject.activeInHierarchy, Is.False);
            Assert.That(clearedPurification.interactable, Is.False);
            RectTransform clearedPanel = controller.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Treasure Curse Panel");
            Assert.That(clearedPanel.rect.height, Is.LessThanOrEqualTo(44f), "Normal status should remain a compact marker.");
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
            Assert.That(ActiveButton("Treasure Purify Curse").IsInteractable(), Is.False);
            ActiveButton("Treasure Purify Curse").onClick.Invoke(); yield return null;
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(29.9999m));
            AssertSavedCurse(session);
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_02_InsufficientGold.png",
                "金币不足不能净化，禁用按钮回调也不能扣款或清除诅咒", "Treasure Curse Panel");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CurseRisk_RealGeneratedChoicesPersistThroughReturnCargoStorageAndReload()
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
            ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
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
            AssertCurseStatus("Treasure Curse Status", 20);
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
            AssertCurseStatus("Battle Curse Status", 30);

            ClickAcceptanceButton("End Battle Turn");
            yield return WaitForCurseBattleFeedback(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            AssertCurseStatus("Battle Curse Status", 30);
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_04_NavalWarning.png",
                "海战持续提示三层诅咒及承伤增加 30%", "Battle Card");
            session.Battle.DistanceMeters = 50f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            RenderRareEventFixture(controller); yield return null;
            ClickAcceptanceButton("Battle Board");
            yield return WaitForCurseBattleFeedback(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            AssertCurseStatus("Battle Curse Status", 30);
            NavalBattleVisualCapture.Capture(controller, "CurseRisk_05_BoardingWarning.png",
                "接舷阶段仍提示真实的诅咒承伤风险", "Battle Card");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertCurseStatus(string objectName, int damageBonusPercent)
        {
            GameObject status = GameObject.Find(objectName);
            Assert.That(status, Is.Not.Null, objectName);
            Assert.That(status.activeInHierarchy, Is.True);
            Text label = status.GetComponent<Text>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Does.Contain("诅咒"));
            Assert.That(label.text, Does.Contain(damageBonusPercent + "%"));
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
                if (probe.Map.Nodes.Any(node => node.Encounter.Id == "treasure_false_cache") &&
                    probe.Map.Nodes.Any(node => node.Encounter.Id == "treasure_smuggler_jetty")) return seed;
            }
            Assert.Fail("No generated map containing both authored curse rewards was found in the deterministic fixture seed range.");
            return null;
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
