using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace ShatteredSail.Core.Tests
{
    public sealed class GameplayFlowPreviewTests
    {
        private GameObject host;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase(GameplayFlowPreviewState.RecruitmentOffer)]
        [TestCase(GameplayFlowPreviewState.ContractSigned)]
        [TestCase(GameplayFlowPreviewState.UpgradePreview)]
        [TestCase(GameplayFlowPreviewState.UpgradeComplete)]
        [TestCase(GameplayFlowPreviewState.TreasureDeparture)]
        [TestCase(GameplayFlowPreviewState.TreasureNavigation)]
        [TestCase(GameplayFlowPreviewState.TreasureClue)]
        [TestCase(GameplayFlowPreviewState.TreasureCoreLocked)]
        [TestCase(GameplayFlowPreviewState.TreasureCoreReady)]
        [TestCase(GameplayFlowPreviewState.TreasureSettlement)]
        public void FlowState_UsesIndependentDataAndBuildsItsRealScreen(GameplayFlowPreviewState state)
        {
            string before = ProfileSignature();
            host = new GameObject("Flow State Preview Test");
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.WhiteShell, 0);
            controller.ConfigureEditorFlowState(state);
            Assert.That(controller.HasCompleteEditorPreview, Is.True, state.ToString());
            Assert.That(ProfileSignature(), Is.EqualTo(before), "Preview samples must not write a player's progression or ownership.");
            if (state == GameplayFlowPreviewState.RecruitmentOffer || state == GameplayFlowPreviewState.ContractSigned)
            {
                Button contract = host.GetComponentsInChildren<Button>(true).Single(item => item.name == "Recruit Tavern Crew 1");
                Assert.That(contract.GetComponentInChildren<Text>().text, Does.Contain(state == GameplayFlowPreviewState.ContractSigned ? "当前职业代表" : "签署契约"));
                contract.onClick.Invoke();
                Assert.That(ProfileSignature(), Is.EqualTo(before), "Even a preview button callback cannot sign a real contract.");
            }
            if (state == GameplayFlowPreviewState.TreasureCoreLocked)
            {
                Assert.That(controller.Session.IsChoiceAvailable(0), Is.False);
                Assert.That(controller.Session.ChoiceUnavailableReason(0), Does.Contain("绞盘"));
                Assert.That(controller.Session.HasTreasureMark("weathered_winch"), Is.False);
                Assert.That(controller.Session.TreasureClues, Is.Zero);
            }
            if (state == GameplayFlowPreviewState.TreasureClue)
            {
                Assert.That(controller.Session.IsTreasureExpedition, Is.True);
                Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("weathered_winch"));
                Assert.That(controller.Session.HasTreasureMark("weathered_winch"), Is.False);
                Assert.That(controller.Session.IsChoiceAvailable(2), Is.True);
            }
            if (state == GameplayFlowPreviewState.TreasureCoreReady)
            {
                Assert.That(controller.Session.IsChoiceAvailable(0), Is.True, controller.Session.ChoiceUnavailableReason(0));
                Assert.That(controller.Session.TotalHours, Is.EqualTo(12d), "Both real slow repairs must consume their authored time.");
                Assert.That(controller.Session.Cargo.Water, Is.LessThan(180), "Tool preparation still consumes the sample crew's rations.");
                Assert.That(controller.Session.Cargo.UsedVolume + 19f, Is.LessThanOrEqualTo(controller.Session.CargoCapacityValue),
                    "The legal sample loadout must leave room for all complete-salvage cargo before the final ration deduction.");
            }
            if (state == GameplayFlowPreviewState.TreasureCoreReady || state == GameplayFlowPreviewState.TreasureSettlement)
            {
                Assert.That(controller.Session.IsTreasureExpedition, Is.True);
                Assert.That(controller.Session.TreasureClues, Is.Zero, "New previews must not unlock the core by injecting legacy clues.");
                foreach (string id in new[] { "weathered_winch", "weathered_bell" })
                {
                    Assert.That(controller.Session.HasTreasureMark(id), Is.True, id);
                    Assert.That(controller.Session.Map.Nodes.Single(node => node.Encounter.Id == id).Visited, Is.True,
                        "Tools must be earned by resolving their real source encounters: " + id);
                }
            }
            if (state == GameplayFlowPreviewState.TreasureSettlement)
            {
                Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
                Assert.That(controller.Session.Settlement.TreasureCompleted, Is.True);
                Assert.That(controller.Session.Settlement.BankedAncientMaterial, Is.GreaterThan(0));
            }
        }

        private static string ProfileSignature()
        {
            string[] keys = {
                ProfileGoldPersistence.DecimalKey,
                CoreGameplayDemoController.ProfileGoldKey, CoreGameplayDemoController.TreasureProgressKey,
                CoreGameplayDemoController.BattleCardUpgradeKey, CoreGameplayDemoController.CrewRecruitmentKey(1),
                CoreGameplayDemoController.CrewVariantRecruitmentKey(CrewNarrativeCatalog.ForPortAndProfession("白贝港", 1).Id),
                CoreGameplayDemoController.ActiveCrewVariantKey(1),
            };
            return string.Join("|", keys.Select(key => key + ":" + PlayerPrefs.HasKey(key) + ":" +
                PlayerPrefs.GetString(key, "<not-string>") + ":" + PlayerPrefs.GetInt(key, -991)));
        }
    }
}
