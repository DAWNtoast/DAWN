using System.Collections;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator LanternRecovery_LegacyGeneratedRewardSurvivesReturnStoresAndFundsHighPortInvestment()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            const string port = "暮潮港";
            PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, 4);
            Assert.That(cost.RareMaterials.Count, Is.EqualTo(1));
            Assert.That(cost.RareMaterials[0].Key, Is.EqualTo("lantern"));
            Assert.That(cost.RareMaterials[0].Quantity, Is.EqualTo(1));

            // Isolated regression fixture: prepare the map, navigator, prior port
            // level and ordinary investment costs. Never inject a lantern. Its
            // sole source below is the generated fogbell encounter's real button.
            PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), 3);
            session.RestoreProfile(cost.Gold + 1000.4321m, port);
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession(port, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(navigator.Id), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(0), navigator.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(navigator.Id), port);
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 1400, selectedMapId = "lantern-recovery",
                maps = new[] { new CoreTreasureMapSave {
                    id = "lantern-recovery", seed = "LAMP-AUDIT-0", rankId = "weathered"
                } }
            });
            session.Cargo.Water = session.Cargo.Food = 150;
            Assert.That(session.PortWarehouse.Restore("v2;" + port + ",wood," + cost.Wood +
                ";" + port + ",iron," + cost.Iron + ";" + port + "," +
                PortMarketCatalog.Key(cost.RequiredItem) + "," + cost.RequiredQuantity), Is.True);
            Assert.That(session.Cargo.Lantern, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "lantern"), Is.Zero);
            Assert.That(session.Cargo.UsedVolume, Is.LessThan(session.CargoCapacityValue));

            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.IsTreasureVoyage, Is.True);
            Assert.That(session.HasNavigator, Is.True, "The actual departure must include the recruited navigator located in this port.");
            RestoreLegacyTreasureFlow(session);
            Assert.That(session.HasNavigator, Is.True, "Restoring the old voyage must preserve its recruited navigator.");
            CoreMapNode fogbell = session.Map.Nodes.Single(node => node.Encounter.Id == "treasure_clue_fogbell");
            // Skip navigation only; use the map's generated event, not an injected
            // reward/template, and operate the actual encounter and return UI.
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, fogbell);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, fogbell.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            RenderRareEventFixture(controller); yield return null;
            yield return DismissTreasureStoryIfPresent(controller);
            yield return WaitForEncounterDossierPresentationCompletion();
            Assert.That(ActiveButton("Encounter Choice 1").IsInteractable(), Is.True, session.ChoiceUnavailableReason(1));
            Assert.That(ActiveButton("Encounter Choice 1").GetComponentInChildren<Text>().text, Does.Contain("航海士辨认灯语"));
            NavalBattleVisualCapture.Capture(controller, "LanternRecovery_01_RealReward.png",
                "隔离流程回归：生成的雾钟灯塔通过真实选项给予一盏航海灯，未配置灯库存", "Encounter Card");
            ClickAcceptanceButton("Encounter Choice 1"); yield return null;
            Assert.That(session.Cargo.Lantern, Is.EqualTo(1));
            Assert.That(fogbell.Visited, Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));

            ClickAcceptanceButton("Safe Return"); yield return null;
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            Assert.That(session.Cargo.Lantern, Is.EqualTo(1), "A fractional cargo loss must not floor the single indivisible lantern to zero.");
            Button.ButtonClickedEvent settledAgain = ActiveButton("Return To Port").onClick;
            yield return SettlementChestTestActions.ShowOverview();
            ClickAcceptanceButton("Return To Port"); yield return null;
            settledAgain.Invoke(); yield return null;
            Assert.That(session.CurrentPort, Is.EqualTo(port));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.Cargo.Lantern, Is.EqualTo(1));
            AssertRareWarehouseSavedSplit(session);

            yield return OpenRareWarehouse();
            ClickAcceptanceButton("Warehouse Store All Rare"); yield return null;
            Assert.That(session.Cargo.Lantern, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "lantern"), Is.EqualTo(1));
            AssertRareWarehouseSavedSplit(session);
            NavalBattleVisualCapture.Capture(controller, "LanternRecovery_02_Stored.png",
                "真实事件所得的一盏灯返港后存入暮潮港仓库", "Warehouse Panel");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            yield return OpenInvestmentThroughMarket();
            AssertInvestmentRequirement("lantern", "航海灯", "1/1");
            Assert.That(session.GetPortInvestmentMaterialAmount("lantern"), Is.EqualTo(1L));
            AssertInvestmentComparison(3);
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
            NavalBattleVisualCapture.Capture(controller, "LanternRecovery_03_Investment.png",
                "航海灯从实际事件到港口四级投资的完整消耗链；金币和基础建材为测试夹具", "Port Investment Panel");
            decimal gold = session.ProfileGold;
            Button.ButtonClickedEvent investedAgain = ActiveButton("Confirm Port Investment").onClick;
            ClickAcceptanceButton("Confirm Port Investment"); yield return null;
            investedAgain.Invoke(); yield return null;
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(4));
            Assert.That(session.ProfileGold, Is.EqualTo(gold - cost.Gold));
            Assert.That(session.Cargo.Lantern, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "lantern"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "iron"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, PortMarketCatalog.Key(cost.RequiredItem)), Is.Zero);
            AssertInvestmentComparison(4);
            string after = SnapshotRareInvestment(session);
            AssertRareWarehouseSavedSplit(session);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            session = controller.Session;
            Assert.That(session.CurrentPort, Is.EqualTo(port));
            Assert.That(SnapshotRareInvestment(session), Is.EqualTo(after), "Reload must neither erase another stock nor resurrect the donated lantern.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(4));
            AssertRareWarehouseSavedSplit(session);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
