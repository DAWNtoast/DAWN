using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        // Arrange cargo before the real return action, then follow the first live
        // reveal and its pointer controls. Never seek, replay or complete the chest.
        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementReturnE2E_SafeReturnFirstReveal_OnlyCurrentRewardAccelerates_ConfirmBanksOnceAndReloads()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            Assert.That(session.PortWarehouse.Restore(string.Empty), Is.True);
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn, showOverview: false);
            VoyageSettlement result = session.Settlement;
            Assert.That(result.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            Assert.That(result.ProtectionRatio, Is.EqualTo(.85f).Within(.001f));
            var retained = SnapshotInvestmentResources(session);
            int[] rare = RareWarehouseCargo(session);
            string warehouseBefore = session.PortWarehouse.Serialize();
            decimal expectedGold = session.ProfileGold + session.Cargo.Gold + result.BankedGold;
            var sequence = LiveReturnChest(controller);
            var plan = SettlementChestRewardPlan.Create(result);
            Assert.That(plan.Items.Count, Is.GreaterThan(2));
            Button finish = ActiveButton("Return To Port");
            Button.ButtonClickedEvent oldFinish = finish.onClick;
            Button.ButtonClickedEvent oldStore = ActiveButton("Settlement Store All Cargo").onClick;
            Assert.That(finish.IsInteractable(), Is.False);
            oldFinish.Invoke(); oldFinish.Invoke(); oldStore.Invoke();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            AssertInvestmentResourcesEqual(session, retained);
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore));

            float deadline = Time.realtimeSinceStartup + 4f;
            while (sequence.Phase == SettlementChestSequencePhase.Opening && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(sequence.Phase, Is.EqualTo(SettlementChestSequencePhase.Rewards));
            RectTransform first = controller.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == "Settlement Chest Reward " + plan.Items[0].Id);
            Text firstAmount = ReturnChestAmount(controller, plan.Items[0].Id);
            Text nextAmount = ReturnChestAmount(controller, plan.Items[1].Id);
            float beforeClick = sequence.Elapsed, clickWallTime = Time.realtimeSinceStartup;
            float ordinaryFrameTime = 0f, bestAccelerationMargin = float.NegativeInfinity;
            bool observedAcceleration = false;
            SettlementChestTestActions.ClickCurrentReward();
            SettlementChestTestActions.ClickCurrentReward();
            SettlementChestTestActions.ClickCurrentReward();
            Assert.That(sequence.Elapsed, Is.EqualTo(beforeClick), "Repeated clicks must accelerate playback, never jump to another reward.");
            deadline = Time.realtimeSinceStartup + 2f;
            while ((Mathf.Abs(first.localScale.x - SettlementChestSequenceController.LandedScale) > .001f ||
                firstAmount.text != "+" + plan.Items[0].Amount.ToString("N0")) &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                ordinaryFrameTime += Time.unscaledDeltaTime;
                // Include both real wall time and the entire rendered frame so a
                // slow pre-click layout cannot masquerade as faster playback.
                float ordinaryUpperBound = Mathf.Max(Time.realtimeSinceStartup - clickWallTime, ordinaryFrameTime);
                float acceleratedAdvance = sequence.Elapsed - beforeClick;
                float margin = acceleratedAdvance - ordinaryUpperBound * 1.5f;
                bestAccelerationMargin = Mathf.Max(bestAccelerationMargin, margin);
                if (ordinaryUpperBound >= .03f && margin > .04f) observedAcceleration = true;
            }
            Assert.That(observedAcceleration, Is.True,
                "Real pointer clicks must visibly accelerate the first reward; an unchanged/empty handler cannot pass. " +
                "elapsedAdvance=" + (sequence.Elapsed - beforeClick) + "; wall=" + (Time.realtimeSinceStartup - clickWallTime) +
                "; frameTime=" + ordinaryFrameTime + "; bestMarginOver1.5x=" + bestAccelerationMargin);
            Assert.That(first.localScale.x, Is.EqualTo(SettlementChestSequenceController.LandedScale).Within(.001f));
            Assert.That(firstAmount.text, Is.EqualTo("+" + plan.Items[0].Amount.ToString("N0")));
            Assert.That(nextAmount.text, Is.EqualTo("+0"), "The next reward must still begin at zero after repeated clicks on the previous reward.");
            Assert.That(sequence.IsComplete, Is.False);
            float nextElapsed = sequence.Elapsed, nextWallTime = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(.08f);
            float wallAdvance = Time.realtimeSinceStartup - nextWallTime;
            Assert.That(sequence.Elapsed - nextElapsed, Is.LessThanOrEqualTo(wallAdvance * 1.5f + .04f),
                "The following reward must return to ordinary speed without another click.");
            AssertInvestmentResourcesEqual(session, retained);
            CollectionAssert.AreEqual(rare, RareWarehouseCargo(session));

            yield return SettlementChestTestActions.ShowOverview();
            Assert.That(session.Settlement, Is.SameAs(result));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            AssertInvestmentResourcesEqual(session, retained);
            AssertSettlementStorageSelected(false);
            foreach (var item in plan.Items)
                Assert.That(ReturnChestAmount(controller, item.Id).text, Is.EqualTo("+" + item.Amount.ToString("N0")));
            ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
            AssertSettlementStorageSelected(true);
            AssertInvestmentResourcesEqual(session, retained);
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore));
            yield return WaitForReturnLedgerGraphics(controller);
            NavalBattleVisualCapture.Capture(controller, "SettlementReturnE2E_SafeReturn_Overview.png",
                "首次真实返航开箱；连点只加速当前奖励，账本确认前资金与仓储保持不变", "Settlement Card");
            ClickAcceptanceButton("Return To Port");
            oldFinish.Invoke(); oldFinish.Invoke(); oldStore.Invoke(); yield return null;
            yield return ClickThroughAcceptanceArrivalComic(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.CurrentPort, Is.EqualTo(result.Destination));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                Assert.That(session.GetMarketCargoAmount(key), Is.Zero, key);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, key), Is.EqualTo(retained.cargo[item]), key);
            }
            AssertSettlementRareDestination(session, session.CurrentPort, "白贝港", rare, true);
            string warehouseAfter = session.PortWarehouse.Serialize();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseAfter));
            Assert.That(session.Cargo.Gold, Is.Zero);
            AssertSettlementRareDestination(session, session.CurrentPort, "白贝港", rare, true);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementReturnE2E_FailedReturn_FirstRevealUsesRescuedAmounts_ConfirmationDoesNotLoseCargoAgain()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.Failure, showOverview: false);
            VoyageSettlement result = session.Settlement;
            Assert.That(result.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(result.ProtectionRatio, Is.EqualTo(.35f).Within(.001f));
            Assert.That(result.BankedGold, Is.EqualTo(7), "The actual failed return must retain 35% of the fixture's 20 earned gold.");
            Assert.That(result.BankedWood, Is.EqualTo(3));
            var retained = SnapshotInvestmentResources(session);
            int[] rare = RareWarehouseCargo(session);
            CollectionAssert.AreEqual(new[] { 3, 2, 3, 3, 3, 3 }, rare);
            string warehouse = session.PortWarehouse.Serialize();
            decimal expectedGold = session.ProfileGold + session.Cargo.Gold + result.BankedGold;
            Assert.That(SettlementChestRewardPlan.Create(result).IsJackpot, Is.False);
            Assert.That(LiveReturnChest(controller).IsComplete, Is.False);
            Button.ButtonClickedEvent finish = ActiveButton("Return To Port").onClick;
            finish.Invoke(); finish.Invoke();
            AssertInvestmentResourcesEqual(session, retained);
            yield return SettlementChestTestActions.ShowOverview();
            AssertInvestmentResourcesEqual(session, retained);
            Assert.That(ReturnChestAmount(controller, "Gold").text, Is.EqualTo("+7"));
            Assert.That(ReturnChestAmount(controller, "Wood").text, Is.EqualTo("+3"));
            AssertSettlementStorageSelected(false);
            yield return WaitForReturnLedgerGraphics(controller);
            NavalBattleVisualCapture.Capture(controller, "SettlementReturnE2E_Failure_Overview.png",
                "真实缺水返航按35%保护率结算；开箱只显示抢救所得，确认不重复扣损", "Settlement Card");
            ClickAcceptanceButton("Return To Port"); finish.Invoke(); finish.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(retained.cargo[item]), item.ToString());
            CollectionAssert.AreEqual(rare, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(retained.cargo[item]), item.ToString());
            CollectionAssert.AreEqual(rare, RareWarehouseCargo(session));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementReturnE2E_NoVoyageRewards_ShowsLedgerDirectly_AndConfirmationPersistsWithoutInventedRewards()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickThroughAcceptanceArrivalComic(controller);
            var session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 40;
            decimal goldBefore = session.ProfileGold;
            int fameBefore = session.Cargo.Fame, experienceBefore = session.PirateExperience, fragmentsBefore = session.TreasureFragments;
            string warehouseBefore = session.PortWarehouse.Serialize();
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            decimal goldAtSea = session.ProfileGold;
            int travelCash = session.Cargo.Gold;
            Assert.That(goldAtSea + travelCash, Is.EqualTo(goldBefore), "Departure moves travel cash out of the profile without spending it.");
            ClickAcceptanceButton("Safe Return"); yield return null; yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            Assert.That(SettlementChestRewardPlan.Create(session.Settlement).HasRewards, Is.False);
            Assert.That(controller.GetComponentsInChildren<SettlementChestSequenceController>(true), Is.Empty,
                "A rewardless real voyage should expose the ledger without an empty chest or an input-blocking shade.");
            Assert.That(GameObject.Find("Settlement Chest Shade"), Is.Null);
            Assert.That(ActiveButton("Return To Port").IsInteractable(), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(goldAtSea));
            Assert.That(session.Cargo.Gold, Is.EqualTo(travelCash));
            Button.ButtonClickedEvent oldFinish = ActiveButton("Return To Port").onClick;
            yield return WaitForReturnLedgerGraphics(controller);
            NavalBattleVisualCapture.Capture(controller, "SettlementReturnE2E_NoRewards_Overview.png",
                "真实出航立即安全返航；无新增奖励直接显示可点击账本", "Settlement Card");
            ClickAcceptanceButton("Return To Port"); oldFinish.Invoke(); oldFinish.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
            Assert.That(session.PirateExperience, Is.EqualTo(experienceBefore));
            Assert.That(session.TreasureFragments, Is.EqualTo(fragmentsBefore));
            Assert.That(session.Cargo.Fame, Is.EqualTo(fameBefore));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
            Assert.That(session.PirateExperience, Is.EqualTo(experienceBefore));
            Assert.That(session.TreasureFragments, Is.EqualTo(fragmentsBefore));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementReturnE2E_NextVoyageArrival_StaleReturnCallbacksCannotSkipFirstRevealOrConfirmNewLedger()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn, showOverview: false);
            yield return SettlementChestTestActions.ShowOverview();
            Button.ButtonClickedEvent oldFinish = ActiveButton("Return To Port").onClick;
            Button.ButtonClickedEvent oldStore = ActiveButton("Settlement Store All Cargo").onClick;
            ClickAcceptanceButton("Return To Port"); yield return null;
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.ArrivedAtPort, showOverview: false);
            VoyageSettlement second = session.Settlement;
            Assert.That(second.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            string origin = session.CurrentPort;
            Assert.That(second.Destination, Is.Not.EqualTo(origin));
            var retained = SnapshotInvestmentResources(session);
            decimal expectedGold = session.ProfileGold + session.Cargo.Gold + second.BankedGold;
            var sequence = LiveReturnChest(controller);
            oldFinish.Invoke(); oldStore.Invoke(); oldFinish.Invoke();
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(session.Settlement, Is.SameAs(second));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            AssertInvestmentResourcesEqual(session, retained);
            yield return SettlementChestTestActions.ShowOverview();
            oldFinish.Invoke(); oldStore.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.CurrentPort, Is.EqualTo(origin));
            AssertSettlementStorageSelected(false);
            AssertInvestmentResourcesEqual(session, retained);
            ClickAcceptanceButton("Return To Port"); yield return null;
            yield return ClickThroughAcceptanceArrivalComic(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.CurrentPort, Is.EqualTo(second.Destination));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            oldFinish.Invoke(); oldStore.Invoke(); yield return null;
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            Assert.That(session.CurrentPort, Is.EqualTo(second.Destination));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitForReturnLedgerGraphics(CoreGameplayDemoController controller)
        {
            // UI screen transitions, font meshes and mask culling settle on real
            // rendered frames. Do not seek either reveal or force a hidden graphic visible.
            string[] required = {
                "Settlement Badge", "Settlement Featured Name", "Settlement Featured Icon Gold",
                "Settlement Resource Wood", "Settlement Resource Iron", "Settlement Resource Fame",
                "Settlement Resource Icon Wood", "Settlement Resource Icon Iron",
                "Settlement Distance", "Settlement Exploration", "Settlement Events"
            };
            float started = Time.realtimeSinceStartup, deadline = started + 5f;
            int stableFrames = 0;
            string diagnostics = string.Empty;
            while (Time.realtimeSinceStartup < deadline && stableFrames < 3)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                var ledger = controller.GetComponentsInChildren<RectTransform>(true)
                    .Single(item => item.name == "Settlement Card" && item.gameObject.activeInHierarchy);
                var reveal = ledger.GetComponent<SettlementRevealController>();
                var chest = ledger.GetComponent<SettlementChestSequenceController>();
                bool ready = reveal != null && reveal.IsComplete && (chest == null || chest.IsComplete);
                var graphics = ledger.GetComponentsInChildren<Graphic>(true);
                var samples = new System.Collections.Generic.List<string>();
                foreach (string name in required)
                {
                    Graphic graphic = graphics.SingleOrDefault(item => item.name == name);
                    if (graphic == null)
                    {
                        ready = false; samples.Add(name + ":MISSING"); continue;
                    }
                    // Unity owns this renderer mesh: inspect it without copying or releasing it.
                    Mesh mesh = graphic.canvasRenderer.GetMesh();
                    int vertexCount = mesh == null ? 0 : mesh.vertexCount;
                    float inheritedAlpha = graphic.canvasRenderer.GetInheritedAlpha();
                    bool visible = graphic.isActiveAndEnabled && graphic.color.a >= .99f && inheritedAlpha >= .99f &&
                        !graphic.canvasRenderer.cull && graphic.depth >= 0 && vertexCount > 0 &&
                        graphic.canvasRenderer.GetColor().a >= .99f;
                    if (graphic is Text label) visible &= !string.IsNullOrEmpty(label.text);
                    if (graphic is RawImage icon) visible &= icon.texture != null;
                    ready &= visible;
                    samples.Add(name + ":active=" + graphic.isActiveAndEnabled + ",alpha=" + graphic.color.a +
                        ",inherited=" + inheritedAlpha + ",culled=" + graphic.canvasRenderer.cull +
                        ",depth=" + graphic.depth + ",vertices=" + vertexCount + ",size=" + graphic.rectTransform.rect.size +
                        ",groups=" + string.Join("/", graphic.GetComponentsInParent<CanvasGroup>(true)
                            .Select(group => group.name + "=" + group.alpha)));
                }
                diagnostics = "reveal=" + (reveal == null ? "missing" : reveal.Elapsed.ToString("0.000")) +
                    "; chest=" + (chest == null ? "none" : chest.Elapsed.ToString("0.000") + "/" + chest.Duration.ToString("0.000")) +
                    "; " + string.Join(" | ", samples);
                stableFrames = ready && Time.realtimeSinceStartup - started >= .35f ? stableFrames + 1 : 0;
            }
            Assert.That(stableFrames, Is.EqualTo(3), "The actual ledger must render every resource label/icon and voyage metric before capture. " + diagnostics);
            TestContext.WriteLine("Settlement ledger graphics verified for " + controller.Session.Settlement.Outcome + ": " + diagnostics);
        }

        private static SettlementChestSequenceController LiveReturnChest(CoreGameplayDemoController controller)
        {
            var sequence = controller.GetComponentsInChildren<SettlementChestSequenceController>(true)
                .Single(item => item.gameObject.activeInHierarchy);
            Assert.That(sequence.IsComplete, Is.False, "The fixture must stop at the original first reveal, not replay an already completed ledger.");
            return sequence;
        }

        private static Text ReturnChestAmount(CoreGameplayDemoController controller, string id) =>
            controller.GetComponentsInChildren<Text>(true).Single(item => item.name == "Settlement Chest Item Amount " + id);
    }
}
