using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        // Cargo is arranged around real Core settlement boundaries. This suite
        // verifies the settlement UI transaction, not a natural-economy voyage.
        [UnityTest, Timeout(180000)]
        public IEnumerator SettlementWarehouse_CheckedAndUncheckedSuccessArrivalAndFailureUseOnlySettledCargoInActualPort()
        {
            foreach (VoyageOutcome outcome in new[] { VoyageOutcome.SafeReturn, VoyageOutcome.Failure, VoyageOutcome.ArrivedAtPort })
            foreach (bool store in new[] { false, true })
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                string origin = session.CurrentPort;
                string destination = origin == "白贝港" ? "断锚港" : "白贝港";
                session.RestoreProfile(2000, origin);
                Assert.That(session.PortWarehouse.Restore(string.Empty), Is.True);
                yield return CreateWarehouseSettlementFixture(controller, outcome);
                string[] components = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray();
                Assert.That(controller.IsTutorialActive, Is.False, "The arrival fixture must not reactivate guided UI locks.");
                Assert.That(session.Settlement.Outcome, Is.EqualTo(outcome));
                AssertSettlementStorageSelected(false);
                InvestmentResourceSnapshot retained = SnapshotInvestmentResources(session);
                int retainedFame = session.Cargo.Fame, retainedCurse = session.Cargo.Curse;
                int[] retainedRare = RareWarehouseCargo(session);
                // Each rare category started at one and gained three. Ancient
                // material uses ordinary gain protection; indivisible
                // valuables round losses down. Test the retained quantities,
                // not the raw four units present before settlement.
                CollectionAssert.AreEqual(outcome == VoyageOutcome.Failure
                    ? new[] { 3, 2, 3, 3, 3, 3 } : new[] { 4, 3, 4, 4, 4, 4 }, retainedRare);
                decimal expectedGold = session.ProfileGold + session.Cargo.Gold + session.Settlement.BankedGold;
                string port = outcome == VoyageOutcome.ArrivedAtPort ? destination : origin;
                string otherPort = port == "白贝港" ? "断锚港" : "白贝港";
                Assert.That(session.Settlement.Destination, Is.EqualTo(port));
                Assert.That(retained.cargo[PortMarketItem.Wood], Is.LessThan(12),
                    "The fixture must actually lose some gained wood before storage, so raw gain copying cannot pass.");
                if (store)
                {
                    string warehouseBefore = session.PortWarehouse.Serialize();
                    SettlementRevealController reveal = Object.FindFirstObjectByType<SettlementRevealController>();
                    ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
                    AssertSettlementStorageSelected(true);
                    AssertInvestmentResourcesEqual(session, retained);
                    CollectionAssert.AreEqual(retainedRare, RareWarehouseCargo(session));
                    Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore), "Checking the box only chooses a destination; it does not move cargo yet.");
                    Assert.That(Object.FindFirstObjectByType<SettlementRevealController>(), Is.SameAs(reveal), "Checking must not rebuild/restart the settlement reveal.");
                }
                CaptureSettlementWarehouse(controller, "01_" + outcome + "_" + (store ? "Store" : "Keep"),
                    "结算边界夹具 · " + outcome + " · " + (store ? "勾选全货入仓" : "默认保留船货"));
                Button.ButtonClickedEvent oldFinish = ActiveButton("Return To Port").onClick;
                ClickAcceptanceButton("Return To Port"); yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(session.CurrentPort, Is.EqualTo(port));
                Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    Assert.That(session.GetMarketCargoAmount(key), Is.EqualTo(store ? 0 : retained.cargo[item]), key);
                    Assert.That(session.PortWarehouse.GetAmount(port, key), Is.EqualTo(store ? retained.cargo[item] : 0), key);
                    Assert.That(session.PortWarehouse.GetAmount(otherPort, key), Is.Zero, "No cargo may follow the selected route to the wrong port: " + key);
                }
                Assert.That(session.Cargo.Fame, Is.EqualTo(retainedFame));
                AssertSettlementRareDestination(session, port, otherPort, retainedRare, store);
                Assert.That(session.Cargo.Curse, Is.EqualTo(retainedCurse));
                Assert.That(session.Cargo.Gold, Is.Zero, "Travel cash still goes to the profile, not the warehouse.");
                CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
                string warehouseAfter = session.PortWarehouse.Serialize();
                oldFinish.Invoke(); oldFinish.Invoke(); yield return null;
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseAfter));
                Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
                AssertSettlementRareDestination(session, port, otherPort, retainedRare, store);
                AssertRareWarehouseSavedSplit(session);
                if (store)
                {
                    yield return OpenPortStorage();
                    yield return ShowPortStorageItem("wood");
                    Assert.That(GameObject.Find("Warehouse Storage Page"), Is.Null, "Settlement cargo is listed across all nine rows without paging.");
                    NavalBattleVisualCapture.Capture(controller, "SettlementWarehouse_Stored_" + outcome + ".png",
                        "结算存仓夹具 · " + outcome + " 实际确认后，本港仓储显示保全货物", "Warehouse Panel");
                    ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
                    Assert.That(ActiveRareWarehouseRows("Warehouse Rare Row "), Is.EqualTo(6));
                    NavalBattleVisualCapture.Capture(controller, "SettlementWarehouse_Stored_Rare_" + outcome + ".png",
                        "结算存仓夹具 · " + outcome + " 六类珍稀按结算保全后的数量存入实际抵达港", "Warehouse Panel");
                }

                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                Assert.That(session.CurrentPort, Is.EqualTo(port));
                Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseAfter));
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(store ? 0 : retained.cargo[item]));
                AssertSettlementRareDestination(session, port, otherPort, retainedRare, store);
                Assert.That(session.Cargo.Fame, Is.EqualTo(retainedFame));
                Assert.That(session.Cargo.Curse, Is.EqualTo(retainedCurse));
                Assert.That(session.Cargo.Gold, Is.Zero);
                AssertRareWarehouseSavedSplit(session);
                CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SettlementWarehouse_OverflowKeepsEverySettledItemOnShipWithoutPartialStorage()
        {
            foreach (string overflowing in new[] { "wood", "ancientcompass" })
            {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.RestoreProfile(2000, "断锚港");
            Assert.That(session.PortWarehouse.Restore("v2;断锚港," + overflowing + ",2147483647;白贝港,wood,5;白贝港,gem,7"), Is.True);
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn);
            InvestmentResourceSnapshot retained = SnapshotInvestmentResources(session);
            int[] retainedRare = RareWarehouseCargo(session);
            int retainedFame = session.Cargo.Fame, retainedCurse = session.Cargo.Curse;
            string[] components = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray();
            string warehouse = session.PortWarehouse.Serialize();
            decimal expectedGold = session.ProfileGold + session.Cargo.Gold + session.Settlement.BankedGold;
            ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
            AssertSettlementStorageSelected(true);
            Button.ButtonClickedEvent oldFinish = ActiveButton("Return To Port").onClick;
            ClickAcceptanceButton("Return To Port"); yield return null;
            oldFinish.Invoke(); oldFinish.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(retained.cargo[item]),
                    "Overflowing " + overflowing + " must not discard or partly transfer another item: " + item);
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            CollectionAssert.AreEqual(retainedRare, RareWarehouseCargo(session));
            Assert.That(session.Cargo.Fame, Is.EqualTo(retainedFame));
            Assert.That(session.Cargo.Curse, Is.EqualTo(retainedCurse));
            Assert.That(session.Cargo.Gold, Is.Zero);
            CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
            AssertRareWarehouseSavedSplit(session);
            NavalBattleVisualCapture.Capture(controller, "SettlementWarehouse_02_Overflow_" + overflowing + ".png",
                "结算存仓夹具 · " + overflowing + " 溢出后，九类基础与六类珍稀全单留船，金币正常结算", "Phase Content");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(retained.cargo[item]));
            CollectionAssert.AreEqual(retainedRare, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            Assert.That(session.Cargo.Fame, Is.EqualTo(retainedFame));
            Assert.That(session.Cargo.Curse, Is.EqualTo(retainedCurse));
            CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
            AssertRareWarehouseSavedSplit(session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SettlementWarehouse_NewVoyageDefaultsUncheckedAndRejectsPriorSettlementCallbacks()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn);
            ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
            Button.ButtonClickedEvent oldOption = ActiveButton("Settlement Store All Cargo").onClick;
            Button.ButtonClickedEvent oldFinish = ActiveButton("Return To Port").onClick;
            ClickAcceptanceButton("Return To Port"); yield return null;
            string warehouse = session.PortWarehouse.Serialize();

            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn);
            VoyageSettlement second = session.Settlement;
            InvestmentResourceSnapshot retained = SnapshotInvestmentResources(session);
            int[] retainedRare = RareWarehouseCargo(session);
            int retainedFame = session.Cargo.Fame, retainedCurse = session.Cargo.Curse;
            decimal expectedGold = session.ProfileGold + session.Cargo.Gold + session.Settlement.BankedGold;
            string[] components = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray();
            AssertSettlementStorageSelected(false);
            oldOption.Invoke(); oldFinish.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement), "An old return button must not silently confirm a later voyage.");
            Assert.That(session.Settlement, Is.SameAs(second));
            AssertSettlementStorageSelected(false);
            AssertInvestmentResourcesEqual(session, retained);
            CollectionAssert.AreEqual(retainedRare, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            CaptureSettlementWarehouse(controller, "03_Next_Voyage_Unchecked", "新结算默认不勾选，上一趟选项与确认回调均无效");
            ClickAcceptanceButton("Return To Port"); yield return null;
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(retained.cargo[item]));
            CollectionAssert.AreEqual(retainedRare, RareWarehouseCargo(session));
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
            Assert.That(session.Cargo.Fame, Is.EqualTo(retainedFame));
            Assert.That(session.Cargo.Curse, Is.EqualTo(retainedCurse));
            Assert.That(session.Cargo.Gold, Is.Zero);
            CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
            AssertRareWarehouseSavedSplit(session);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator CreateWarehouseSettlementFixture(CoreGameplayDemoController controller, VoyageOutcome outcome)
        {
            CoreVoyageSession session = controller.Session;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            // The all-category boundary fixture needs room for real mixed cargo.
            // Arrange an owned merchant; this is not a ship-acquisition test.
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            controller.EnsureShipComponentLoadout(1);
            Assert.That(session.SelectShip(1), Is.True);
            Assert.That(session.ConfigureGuidedTutorialVoyage(outcome == VoyageOutcome.ArrivedAtPort ? 2 : 0), Is.True);
            session.SelectDestinationPort(session.CurrentPort == "白贝港" ? "断锚港" : "白贝港");
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) SetInvestmentCargo(session.Cargo, item, 2);
            session.Cargo.Water = session.Cargo.Food = 40;
            session.Cargo.Fame = 3;
            SetRareWarehouseFixture(session, 1, 1, 1, 1, 1, 1);
            session.Cargo.Curse = 2;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            if (outcome == VoyageOutcome.ArrivedAtPort)
            {
                // Ordinary departure UI intentionally clears the Core lesson
                // fixture when controller tutorial is complete. This fixture
                // verifies the supported legacy save's one-event arrival
                // boundary; the current combined lesson continues to a boss.
                // Match the legacy checkpoint test's layout before departure,
                // then use real sailing/choice APIs for the arrival itself.
                // Never assign phase, settlement or current port directly.
                Assert.That(session.ConfigureGuidedTutorialVoyage(2), Is.True);
                typeof(CoreVoyageSession).GetField("guidedTutorialLayoutVersion", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(session, 0);
                Assert.That(session.UsesCombinedGuidedTutorialVoyage, Is.False);
                session.ConfigureCrewCapabilities(true, false, false, false, false, false);
                session.StartVoyage();
                typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                yield return null;
            }
            else { ClickAcceptanceButton("Start Voyage"); yield return null; }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                // Mixed cargo and all six rare gains fit the merchant fixture;
                // the wood gain still proves loss happens before warehouse transfer.
                SetInvestmentCargo(session.Cargo, item, session.GetMarketCargoAmount(PortMarketCatalog.Key(item)) +
                    (item == PortMarketItem.Wood ? 10 : 5));
            session.Cargo.Gold += 20; session.Cargo.Fame += 10;
            SetRareWarehouseFixture(session, 4, 4, 4, 4, 4, 4);
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue),
                "Settlement cargo fixtures must fit the real ship before sailing or resolving an event.");
            if (outcome == VoyageOutcome.ArrivedAtPort)
            {
                // The supported legacy itinerary reaches its destination through
                // the real navigator choice. Controller tutorial remains completed;
                // no private phase, settlement, outcome or port setters are used.
                Assert.That(session.HasNavigator, Is.True);
                CoreMapNode navigator = session.Map.Nodes.Single(node => node.Encounter.Id == CoreGuidedTutorialContent.NavigatorEventId);
                for (int leg = 0; session.Phase == CoreDemoPhase.Sailing && leg < 4; leg++)
                {
                    session.SetCourseTo(navigator.Position);
                    ClickAcceptanceButton("Execute Sail");
                    yield return WaitForWarehouseNavigationUi(session);
                }
                Assert.That(session.ActiveEncounter?.Id, Is.EqualTo(CoreGuidedTutorialContent.NavigatorEventId));
                Assert.That(session.IsChoiceAvailable(0), Is.True, session.ChoiceUnavailableReason(0));
                ClickAcceptanceButton("Encounter Choice 0"); yield return null;
            }
            else
            {
                if (outcome == VoyageOutcome.Failure) session.Cargo.Water = 0;
                ClickAcceptanceButton("Safe Return"); yield return null;
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
            { ClickAcceptanceButton("Skip Settlement Reveal"); yield return null; }
        }

        private static IEnumerator WaitForWarehouseNavigationUi(CoreVoyageSession session)
        {
            // Sail commits the encounter before its brief automatic presentation
            // finishes. Wait for the real visible control rather than resolving
            // the Core encounter or clicking the obscured prior sailing UI.
            float deadline = Time.realtimeSinceStartup + 5f;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string diagnostic = "No next control has appeared.";
            do
            {
                yield return null;
                Button nextControl = session.Phase == CoreDemoPhase.Encounter
                    ? ActiveButton("Encounter Choice 0")
                    : session.Phase == CoreDemoPhase.Sailing ? ActiveButton("Execute Sail") : null;
                if (!NavigationMotionIsPlaying(controller) && nextControl != null && nextControl.IsInteractable() &&
                    WarehouseControlReceivesPointer(nextControl, out diagnostic))
                    yield break;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.Fail("Sailing presentation did not expose a pointer-reachable event choice or next sailing control within 5 seconds; phase=" + session.Phase + "; " + diagnostic);
        }

        private static bool WarehouseControlReceivesPointer(Button button, out string diagnostic)
        {
            // A freshly rebuilt Graphic can be interactable before its renderer
            // has a raycastable depth. Use the same real hit test as the click,
            // without sending input or changing the animation/CanvasGroup.
            Canvas.ForceUpdateCanvases();
            Canvas parentCanvas = button.GetComponentInParent<Canvas>();
            Canvas canvas = parentCanvas == null ? null : parentCanvas.rootCanvas;
            if (canvas == null || EventSystem.current == null)
            {
                diagnostic = button.name + ": missing Canvas or EventSystem.";
                return false;
            }
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform rect = (RectTransform)button.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button)
            {
                diagnostic = string.Empty;
                return true;
            }
            Graphic graphic = button.targetGraphic;
            diagnostic = button.name + "; pointer=" + position + "; screen=" + Screen.width + "x" + Screen.height +
                "; depth=" + (graphic == null ? "none" : graphic.depth.ToString()) +
                "; culled=" + (graphic != null && graphic.canvasRenderer.cull) +
                "; raycast=" + (graphic != null && graphic.raycastTarget) +
                "; firstHit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name) +
                "; groups=" + string.Join(" | ", button.GetComponentsInParent<CanvasGroup>(true).Select(group =>
                    group.name + ":alpha=" + group.alpha + ",blocks=" + group.blocksRaycasts + ",interactable=" + group.interactable + ",ignoreParents=" + group.ignoreParentGroups)) +
                "; raycasters=" + string.Join(" | ", Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(raycaster =>
                    raycaster.name + ":enabled=" + raycaster.enabled + ",active=" + raycaster.gameObject.activeInHierarchy));
            return false;
        }

        private static void AssertSettlementStorageSelected(bool selected)
        {
            Button option = ActiveButton("Settlement Store All Cargo");
            Assert.That(option, Is.Not.Null);
            Assert.That(option.IsInteractable(), Is.True);
            Assert.That(option.GetComponentInChildren<Text>().text,
                Does.Contain("是否将所有货物置入仓库"));
            Transform indicator = option.transform.Find("Settlement Cargo Checkbox/Settlement Cargo Checkbox Interior/Settlement Cargo Selected");
            Assert.That(indicator, Is.Not.Null, "Selection uses a graphic, independent of system font fallback.");
            Assert.That(indicator.gameObject.activeSelf, Is.EqualTo(selected));
        }

        private static void AssertSettlementRareDestination(CoreVoyageSession session, string port, string otherPort,
            int[] retained, bool store)
        {
            int[] carried = RareWarehouseCargo(session);
            for (int index = 0; index < RareWarehouseTestKeys.Length; index++)
            {
                string key = RareWarehouseTestKeys[index];
                int stored = session.PortWarehouse.GetAmount(port, key);
                Assert.That(carried[index], Is.EqualTo(store ? 0 : retained[index]), key + " carried after settlement");
                Assert.That(stored, Is.EqualTo(store ? retained[index] : 0), key + " stored after settlement");
                Assert.That((long)carried[index] + stored, Is.EqualTo(retained[index]), key + " must not be copied from raw gained cargo");
                Assert.That(session.PortWarehouse.GetAmount(otherPort, key), Is.Zero, key + " cannot be delivered to another port");
            }
        }

        private static void CaptureSettlementWarehouse(CoreGameplayDemoController controller, string stage, string detail) =>
            NavalBattleVisualCapture.Capture(controller, "SettlementWarehouse_" + stage + ".png", detail, "Settlement Card");
    }
}
