using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementChestFlow_HiddenActionsAreBlocked_ThenAutoOverviewDoesNotBankOrConfirm()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn);
            // Existing transaction fixture ends after clicking skip. Replay only its presentation
            // to audit the intro against the exact real Core settlement/cargo it created.
            RestartChestPresentation(controller);
            yield return null; yield return null;
            var sequence = controller.GetComponentsInChildren<SettlementChestSequenceController>(true).Single();
            var session = controller.Session;
            Assert.That(sequence.IsComplete, Is.False);
            decimal gold = session.ProfileGold;
            string warehouse = session.PortWarehouse.Serialize();
            int cargoGold = session.Cargo.Gold, wood = session.Cargo.Wood;
            bool chart = session.Settlement.ChartSaved;
            string chartsSave = PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty);
            var overview = sequence.GetComponent<CanvasGroup>();
            Assert.That(overview.alpha, Is.Zero);
            Assert.That(overview.interactable, Is.False);
            Assert.That(overview.blocksRaycasts, Is.False);
            foreach (string name in new[] { "Return To Port", "Settlement Store All Cargo", "Draw Sea Chart" })
            {
                Button button = controller.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
                Assert.That(button.IsInteractable(), Is.False, name + " must be disabled through the overview group.");
                button.onClick.Invoke();
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.ChartSaved, Is.EqualTo(chart));
            Assert.That((bool)typeof(CoreGameplayDemoController).GetField("storeAllSettlementCargo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.False);
            float originalScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                float deadline = Time.realtimeSinceStartup + sequence.Duration + 2f;
                while (!sequence.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            }
            finally { Time.timeScale = originalScale; }
            Assert.That(sequence.IsComplete, Is.True, "The live sequence must automatically finish while game time is paused.");
            Assert.That(overview.alpha, Is.EqualTo(1f));
            Assert.That(overview.interactable && overview.blocksRaycasts, Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement), "The animation must never confirm Return To Port.");
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty), Is.EqualTo(chartsSave));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>().IsInteractable(), Is.True);
            NavalBattleVisualCapture.Capture(controller, "SettlementChest_AutoOverview.png",
                "真实Core结算夹具；TimeScale=0自然播放结束自动进入一览，未点击返港、未入账、未入库", "Settlement Card");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementChestFlow_TutorialWaitsUntilChestCompletes_AndSkipPreservesSettlement()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn);
            typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, true);
            typeof(CoreGameplayDemoController).GetField("guidedTutorialStep", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, GuidedTutorialStep.FirstSettlement);
            RestartChestPresentation(controller);
            yield return null; yield return null;
            var sequence = controller.GetComponentsInChildren<SettlementChestSequenceController>(true).Single();
            Assert.That(sequence.IsComplete, Is.False);
            var snapshots = (ICollection)typeof(CoreGameplayDemoController).GetField("guidedControlSnapshots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(snapshots.Count, Is.Zero, "Tutorial blockers must not cover or lock the chest skip control.");
            Button skip = controller.GetComponentsInChildren<Button>(true).Single(button => button.name == "Skip Settlement Reveal");
            Assert.That(skip.IsInteractable(), Is.True);
            decimal gold = controller.Session.ProfileGold;
            skip.onClick.Invoke(); skip.onClick.Invoke(); yield return null;
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(snapshots.Count, Is.GreaterThan(0), "Tutorial input guidance must resume on the final overview.");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>().IsInteractable(), Is.True);
            NavalBattleVisualCapture.Capture(controller, "SettlementChest_TutorialOverview.png",
                "教学结算夹具；宝箱期间无教学遮挡，跳过后教学指引恢复且未确认结算", "Settlement Card");
            LogAssert.NoUnexpectedReceived();
        }

        private static void RestartChestPresentation(CoreGameplayDemoController controller)
        {
            typeof(CoreGameplayDemoController).GetField("settlementChestShown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, null);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        }
    }
}
