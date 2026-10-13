using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageCargo_RealBasicAndSpecialDiscardsValidateQuantityAndLeavePortProfileUntouched()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadVoyageCargoFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                string voyageBeforeOpen = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
                string[] portBefore = VoyageCargoPortSnapshot();
                decimal gold = session.ProfileGold;
                int voyageGold = session.Cargo.Gold;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                GameObject overlay = GameObject.Find("Voyage Cargo Overlay");
                Assert.That(overlay, Is.Not.Null);
                // The map may receive a queued pointer-up after the modal opens.
                typeof(CoreGameplayDemoController).GetMethod("SelectCourseAtMapPoint", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { new Vector2(340f, 180f) });
                Assert.That(controller.Session, Is.SameAs(session));
                Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.SameAs(overlay));
                Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(voyageBeforeOpen));
                Assert.That(ActiveButton("Execute Sail").IsInteractable(), Is.False, "The modal must disable underlying navigation.");
                Assert.That(ActiveButton("Heading South").IsInteractable(), Is.False);
                AssertVoyageCargoGrid(session, false);
                Assert.That(GameObject.Find("Voyage Cargo Item gold"), Is.Null, "Gold is read-only, not discardable cargo.");
                Assert.That(GameObject.Find("Voyage Cargo Capacity").GetComponent<Text>().text, Does.Contain("随船金币"));
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                AssertVoyageCargoDetailsBlendWithSheet();
                int wood = session.Cargo.Wood;
                ClickAcceptanceButton("Voyage Cargo Discard All"); yield return null;
                Assert.That(GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text, Is.EqualTo(wood.ToString()));
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood), "Selecting all is not a discard confirmation.");
                foreach (string invalid in new[] { "0", "999", "-1", "invalid" })
                {
                    SetVoyageCargoQuantity(invalid);
                    Button confirm = ActiveButton("Confirm Voyage Cargo Discard");
                    Assert.That(confirm.IsInteractable(), Is.False, invalid);
                    confirm.onClick.Invoke();
                    Assert.That(session.Cargo.Wood, Is.EqualTo(wood), "Invalid queued callbacks cannot discard: " + invalid);
                }
                SetVoyageCargoQuantity("2");
                float volume = session.Cargo.UsedVolume;
                string capacity = GameObject.Find("Voyage Cargo Capacity").GetComponent<Text>().text;
                ClickAcceptanceButton("Confirm Voyage Cargo Discard"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood - 2));
                Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume - 2f).Within(.001f));
                Assert.That(GameObject.Find("Voyage Cargo Capacity").GetComponent<Text>().text, Is.Not.EqualTo(capacity));
                Assert.That(GameObject.Find("Voyage Cargo Item Amount wood").GetComponent<Text>().text, Does.Contain((wood - 2).ToString()));
                Assert.That(GameObject.Find("Voyage Cargo Notice").GetComponent<Text>().text, Is.Not.Empty);
                AssertVoyageCargoGrid(session, false);

                ClickAcceptanceButton("Voyage Cargo Special Tab"); yield return null;
                AssertVoyageCargoGrid(session, true);
                ClickAcceptanceButton("Voyage Cargo Item lantern"); yield return null;
                Assert.That(ActiveButton("Confirm Voyage Cargo Discard").IsInteractable(), Is.False);
                Assert.That(session.Cargo.Lantern, Is.Zero, "Zero-stock rows remain inspectable.");
                ClickAcceptanceButton("Voyage Cargo Item gem"); yield return null;
                AssertVoyageCargoDetailsBlendWithSheet();
                int gems = session.Cargo.Gem;
                volume = session.Cargo.UsedVolume;
                SetVoyageCargoQuantity("1");
                ClickAcceptanceButton("Confirm Voyage Cargo Discard"); yield return null;
                Assert.That(session.Cargo.Gem, Is.EqualTo(gems - 1));
                Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume - 5f).Within(.001f));
                AssertVoyageCargoGrid(session, true);
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Gold, Is.EqualTo(voyageGold));
                Assert.That(VoyageCargoPortSnapshot(), Is.EqualTo(portBefore));
                ClickAcceptanceButton("Close Voyage Cargo"); yield return null;
                Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.Null);
                Assert.That(ActiveButton("Execute Sail").IsInteractable(), Is.True);
                ClickAcceptanceButton("Heading South"); yield return null;
                Assert.That(session.Heading, Is.EqualTo(SailingDirection.South), "Closing restores actual navigation input.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageCargo_RetiredRowsConfirmationsAndInventoryQuotesCannotDiscardAgain()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadVoyageCargoFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                SetVoyageCargoQuantity("2");
                Button.ButtonClickedEvent retiredConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                Button.ButtonClickedEvent retiredRow = ActiveButton("Voyage Cargo Item wood").onClick;
                ClickAcceptanceButton("Confirm Voyage Cargo Discard"); yield return null;
                int wood = session.Cargo.Wood;
                retiredConfirm.Invoke(); retiredRow.Invoke(); retiredConfirm.Invoke();
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood), "A consumed view cannot repeat the discard.");

                retiredRow = ActiveButton("Voyage Cargo Item wood").onClick;
                retiredConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                ClickAcceptanceButton("Voyage Cargo Special Tab"); yield return null;
                retiredRow.Invoke(); retiredConfirm.Invoke();
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
                Assert.That(GameObject.Find("Voyage Cargo Item wood"), Is.Null);
                ClickAcceptanceButton("Voyage Cargo Item gem"); yield return null;
                SetVoyageCargoQuantity("1");
                retiredRow = ActiveButton("Voyage Cargo Item gem").onClick;
                retiredConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                int gems = session.Cargo.Gem;
                ClickAcceptanceButton("Close Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                retiredRow.Invoke(); retiredConfirm.Invoke();
                Assert.That(session.Cargo.Gem, Is.EqualTo(gems), "Closing and reopening cannot reactivate a retired quote.");

                ClickAcceptanceButton("Voyage Cargo Special Tab"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item gem"); yield return null;
                SetVoyageCargoQuantity("1");
                retiredConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                // Model another inventory mutation after the displayed quote.
                // Even an increase must invalidate that exact quote.
                session.Cargo.Gem += 1;
                retiredConfirm.Invoke(); yield return null;
                Assert.That(session.Cargo.Gem, Is.EqualTo(gems + 1));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageCargo_NavigationMotionDisableAndRenderRetireAllCargoCallbacks()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadVoyageCargoFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                session.Map.Nodes.Clear();
                Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
                SetDestinationTestPosition(session, new VoyagePoint(30f, 32f));
                SetDestinationTestField(session, "randomAmbushOccurred", true);
                session.SetHeading(SailingDirection.East); session.SetDistance(20);
                RenderVoyageCargoFixture(controller); yield return null;
                Button.ButtonClickedEvent retiredOpen = ActiveButton("Voyage Cargo").onClick;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                SetVoyageCargoQuantity("1");
                Button.ButtonClickedEvent motionConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                int woodBeforeMotion = session.Cargo.Wood;
                ClickAcceptanceButton("Close Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Execute Sail");
                Assert.That((bool)typeof(CoreGameplayDemoController).GetField("navigationMotionPlaying",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.True);
                retiredOpen.Invoke(); motionConfirm.Invoke();
                Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.Null);
                Assert.That(session.Cargo.Wood, Is.EqualTo(woodBeforeMotion));
                yield return WaitForNavigationMotionCompletion(controller);

                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                SetVoyageCargoQuantity("1");
                Button.ButtonClickedEvent retiredConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                int wood = session.Cargo.Wood;
                controller.enabled = false;
                retiredConfirm.Invoke();
                Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.Null);
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
                controller.enabled = true;
                RenderVoyageCargoFixture(controller); yield return null;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                retiredConfirm = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                RenderVoyageCargoFixture(controller); yield return null;
                retiredConfirm.Invoke();
                Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.Null);
                Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
                Assert.That(ActiveButton("Heading South").IsInteractable(), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageCargo_SaveReturnAndResumePreservesDiscardsWithoutBankingCargo()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadVoyageCargoFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                string[] portBefore = VoyageCargoPortSnapshot();
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                SetVoyageCargoQuantity("2");
                ClickAcceptanceButton("Confirm Voyage Cargo Discard"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Special Tab"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item gem"); yield return null;
                SetVoyageCargoQuantity("1");
                ClickAcceptanceButton("Confirm Voyage Cargo Discard"); yield return null;
                ClickAcceptanceButton("Close Voyage Cargo"); yield return null;
                yield return WaitForItemIconView(controller);
                CoreVoyageCheckpoint expected = controller.Session.CaptureVoyageCheckpoint();
                int[] cargo = CoreWarehouseItemCatalog.All.Select(item => controller.Session.GetWarehouseCargoAmount(item.Key)).ToArray();
                ClickAcceptanceButton("Voyage Settings"); yield return null;
                ClickAcceptanceButton("Return To Main Menu"); yield return null;
                ClickAcceptanceButton("Confirm Return To Main Menu"); yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(VoyageCargoPortSnapshot(), Is.EqualTo(portBefore));
                Assert.That(new ProtectedCheckpointStore(storage.CheckpointDirectory).TryLoad(out _, out string notice), Is.True, notice);
                ClickAcceptanceButton("Continue Game"); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(CoreWarehouseItemCatalog.All.Select(item => controller.Session.GetWarehouseCargoAmount(item.Key)).ToArray(), Is.EqualTo(cargo));
                Assert.That(controller.Session.Map.Hash, Is.EqualTo(expected.mapHash));
                Assert.That(controller.Session.TotalHours, Is.EqualTo(expected.totalHours));
                Assert.That(VoyageCargoPortSnapshot(), Is.EqualTo(portBefore));
                Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private IEnumerator LoadVoyageCargoFixture()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("VOYAGE-CARGO-UI-REGRESSION");
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) SetInvestmentCargo(session.Cargo, item, 3);
            session.Cargo.Water = session.Cargo.Food = 100; session.Cargo.Wood = 8;
            SetRareWarehouseFixture(session, 3, 2, 1, 1, 1, 1);
            session.Cargo.Lantern = 0;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            RenderVoyageCargoFixture(controller);
            yield return WaitForItemIconView(controller);
        }

        private static void SetVoyageCargoQuantity(string quantity)
        {
            GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text = quantity;
            Canvas.ForceUpdateCanvases();
        }

        private static void RenderVoyageCargoFixture(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        private static void AssertVoyageCargoGrid(CoreVoyageSession session, bool special)
        {
            Canvas.ForceUpdateCanvases();
            var expected = special ? CoreWarehouseItemCatalog.RareItems : CoreWarehouseItemCatalog.BasicItems;
            Button[] items = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Voyage Cargo Item ", StringComparison.Ordinal)).ToArray();
            Assert.That(items.Length, Is.EqualTo(expected.Count));
            var grid = GameObject.Find("Voyage Cargo List").GetComponent<RectTransform>();
            RectTransform[] cells = grid.Cast<Transform>().Where(child =>
                    child.name.StartsWith("Voyage Cargo Item ", StringComparison.Ordinal) ||
                    child.name.StartsWith("Voyage Cargo Empty Slot ", StringComparison.Ordinal))
                .Cast<RectTransform>().OrderByDescending(cell => cell.anchoredPosition.y)
                .ThenBy(cell => cell.anchoredPosition.x).ToArray();
            Assert.That(cells.Length, Is.EqualTo(9), "Both tabs retain all nine evenly arranged slots.");
            Rect[] bounds = cells.Select(cell => VoyageCargoBounds(grid, cell)).ToArray();
            for (int i = 0; i < cells.Length; i++)
            {
                AssertVoyageCargoContains(grid.rect, bounds[i], cells[i].name);
                Assert.That(bounds[i].width, Is.EqualTo(bounds[0].width).Within(.01f));
                Assert.That(bounds[i].height, Is.EqualTo(bounds[0].height).Within(.01f));
                Assert.That(bounds[i].center.x, Is.EqualTo(bounds[i % 3].center.x).Within(.01f));
                Assert.That(bounds[i].center.y, Is.EqualTo(bounds[i / 3 * 3].center.y).Within(.01f));
                for (int j = 0; j < i; j++) Assert.That(bounds[i].Overlaps(bounds[j]), Is.False, cells[i].name + " / " + cells[j].name);
                if (cells[i].name.StartsWith("Voyage Cargo Empty Slot ", StringComparison.Ordinal))
                {
                    foreach (Selectable control in cells[i].GetComponentsInChildren<Selectable>())
                        Assert.That(control.IsInteractable(), Is.False, "Empty slots cannot select or discard cargo.");
                    foreach (Graphic graphic in cells[i].GetComponentsInChildren<Graphic>())
                        Assert.That(graphic.raycastTarget, Is.False, "Empty slots must not intercept input.");
                }
            }
            Assert.That(bounds[1].center.x - bounds[0].center.x,
                Is.EqualTo(bounds[2].center.x - bounds[1].center.x).Within(.01f), "Columns have equal spacing.");
            Assert.That(bounds[0].center.y - bounds[3].center.y,
                Is.EqualTo(bounds[3].center.y - bounds[6].center.y).Within(.01f), "Rows have equal spacing.");
            foreach (CoreWarehouseItemDefinition item in expected)
            {
                var cell = (RectTransform)ActiveButton("Voyage Cargo Item " + item.Key).transform;
                var icon = cell.Find("Voyage Cargo Item Icon " + item.Key).GetComponent<RectTransform>();
                Text name = cell.Find("Voyage Cargo Item Name " + item.Key).GetComponent<Text>();
                Text amount = cell.Find("Voyage Cargo Item Amount " + item.Key).GetComponent<Text>();
                Text volume = cell.Find("Voyage Cargo Item Volume " + item.Key).GetComponent<Text>();
                int carried = session.GetWarehouseCargoAmount(item.Key);
                Assert.That(name.text, Is.EqualTo(item.Name));
                Assert.That(name.fontSize, Is.LessThanOrEqualTo(18), "Names use the requested small caption size.");
                Assert.That(amount.text, Is.EqualTo(carried.ToString(CultureInfo.InvariantCulture)));
                Assert.That(volume.text, Is.EqualTo((carried * item.UnitVolume).ToString("0.##", CultureInfo.InvariantCulture)));
                Rect iconBounds = VoyageCargoBounds(cell, icon);
                Rect nameBounds = VoyageCargoBounds(cell, name.rectTransform);
                Rect amountBounds = VoyageCargoBounds(cell, amount.rectTransform);
                Rect volumeBounds = VoyageCargoBounds(cell, volume.rectTransform);
                foreach (Rect content in new[] { iconBounds, nameBounds, amountBounds, volumeBounds })
                    AssertVoyageCargoContains(cell.rect, content, item.Key);
                Assert.That(nameBounds.yMax, Is.LessThanOrEqualTo(iconBounds.yMin + .01f), "Caption sits below the icon: " + item.Key);
                Assert.That(amountBounds.yMax, Is.LessThanOrEqualTo(nameBounds.yMin + .01f));
                Assert.That(volumeBounds.yMax, Is.LessThanOrEqualTo(nameBounds.yMin + .01f));
                Assert.That(amountBounds.xMax, Is.LessThan(0f), "Quantity occupies the lower left corner.");
                Assert.That(volumeBounds.xMin, Is.GreaterThan(0f), "Occupied space occupies the lower right corner.");
                Assert.That(amountBounds.center.y, Is.EqualTo(volumeBounds.center.y).Within(.01f));
            }
        }

        private static void AssertVoyageCargoDetailsBlendWithSheet()
        {
            var details = GameObject.Find("Voyage Cargo Details").GetComponent<RectTransform>();
            Image backing = details.GetComponent<Image>();
            Assert.That(backing == null || backing.color.a <= .1f, Is.True, "Details share the main parchment without an opaque colored rectangle.");
            var sheet = (RectTransform)details.parent;
            var grid = GameObject.Find("Voyage Cargo List").GetComponent<RectTransform>();
            Assert.That(VoyageCargoBounds(sheet, details).Overlaps(VoyageCargoBounds(sheet, grid)), Is.False);
            Image quantityBacking = GameObject.Find("Voyage Cargo Quantity").GetComponent<Image>();
            Assert.That(quantityBacking.color.a, Is.LessThan(.35f), "The quantity field lets the parchment texture show through.");
        }

        private static Rect VoyageCargoBounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private static void AssertVoyageCargoContains(Rect container, Rect content, string context)
        {
            Assert.That(content.xMin, Is.GreaterThanOrEqualTo(container.xMin - .01f), context);
            Assert.That(content.xMax, Is.LessThanOrEqualTo(container.xMax + .01f), context);
            Assert.That(content.yMin, Is.GreaterThanOrEqualTo(container.yMin - .01f), context);
            Assert.That(content.yMax, Is.LessThanOrEqualTo(container.yMax + .01f), context);
        }

        private static string[] VoyageCargoPortSnapshot() => new[] {
            "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame"
        }.Select(CoreGameplayDemoController.CargoKey).Append(CoreGameplayDemoController.ProfileGoldKey)
            .Select(key => key + "=" + PlayerPrefs.HasKey(key) + ":" + PlayerPrefs.GetInt(key, -98765))
            .Concat(new[] { CoreGameplayDemoController.PortWarehouseCargoKey, CoreGameplayDemoController.TreasureProgressKey,
                CoreGameplayDemoController.CurrentPortKey, ProfileGoldPersistence.DecimalKey }
                .Select(key => key + "=" + PlayerPrefs.HasKey(key) + ":" + PlayerPrefs.GetString(key, "<absent>"))).ToArray();

        private sealed class VoyageCargoStorageScope : IDisposable
        {
            private readonly Dictionary<FieldInfo, object> previous = new Dictionary<FieldInfo, object>();
            private readonly string directory = Path.Combine(Path.GetTempPath(), "ShatteredSailVoyageCargo-" + Guid.NewGuid().ToString("N"));
            public string CheckpointDirectory => Path.Combine(directory, "voyage");

            public VoyageCargoStorageScope()
            {
                Type suspension = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
                SaveAndSet(suspension.GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic), CheckpointDirectory);
                SaveAndSet(suspension.GetField("PendingNotice", BindingFlags.Static | BindingFlags.NonPublic), null);
                foreach (FieldInfo field in typeof(ProfileSaveJournal).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                    if (!field.IsLiteral && !field.IsInitOnly) SaveAndSet(field, null);
                typeof(ProfileSaveJournal).GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, Path.Combine(directory, "profile"));
                SaveAndSet(typeof(MainMenuController).GetField("profileRecoveryBlocked", BindingFlags.Static | BindingFlags.NonPublic), false);
            }

            private void SaveAndSet(FieldInfo field, object value)
            {
                Assert.That(field, Is.Not.Null, "Isolated storage support is required.");
                previous.Add(field, field.GetValue(null)); field.SetValue(null, value);
            }

            public void Dispose()
            {
                // Retire live controllers before restoring the caller's paths.
                foreach (CoreGameplayDemoController controller in Object.FindObjectsByType<CoreGameplayDemoController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(controller.gameObject);
                foreach (MainMenuController controller in Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(controller.gameObject);
                foreach (var item in previous) item.Key.SetValue(null, item.Value);
                string resolved = Path.GetFullPath(directory);
                Assert.That(resolved.StartsWith(Path.Combine(Path.GetTempPath(), "ShatteredSailVoyageCargo-"), StringComparison.OrdinalIgnoreCase), Is.True);
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
