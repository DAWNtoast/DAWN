using System;
using System.Collections;
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
        public IEnumerator AdditionalPortVfx_MarketUsesCommittedDeltasAndNeverReplaysStaleOrFailedTransactions()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ship);
                yield return EnterMarketQuantity("water", "7");
                var session = controller.Session;
                decimal beforeGold = session.ProfileGold;
                Text goldLabel = GameObject.Find("Port Facility Gold").GetComponent<Text>();
                Vector2 goldSize = goldLabel.rectTransform.sizeDelta;
                Vector3 goldPosition = goldLabel.rectTransform.position;
                var queued = MarketConfirm("water").onClick;
                queued.Invoke();
                PortTransactionMotion purchase = ActivePortTransaction();
                Assert.That(purchase, Is.Not.Null);
                Assert.That(purchase.ReceiptText, Does.Contain("随船物资").And.Contain("+7"));
                Assert.That(purchase.SecondaryText, Is.EqualTo("金币 −" + PortMarketCatalog.FormatPrice(beforeGold - session.ProfileGold)));
                Assert.That(session.Cargo.Water, Is.EqualTo(7));
                Assert.That(goldLabel.rectTransform.sizeDelta, Is.EqualTo(goldSize));
                Assert.That(goldLabel.rectTransform.position, Is.EqualTo(goldPosition));
                AssertPortTransactionDoesNotInterceptInput(purchase);
                purchase.SetElapsed(.56f);
                AssertPortTransactionGeometry(purchase, "Market Inventory Toggle", "Market Quantity Slider water", "Market Trade Total", "Market Quantity Input water");
                NavalBattleVisualCapture.Capture(controller, "AdditionalVfx_06_MarketPurchase.png", "市场购买：实际物资与金币差额、图标落位", "Phase Content");
                queued.Invoke();
                InvokePortVfxRender(controller);
                Assert.That(ActivePortTransaction(), Is.SameAs(purchase));
                Assert.That(purchase.Elapsed, Is.EqualTo(.56f), "A retained Render must not restart the receipt.");
                Assert.That(session.Cargo.Water, Is.EqualTo(7));
                purchase.SetElapsed(PortTransactionMotion.Duration); yield return null;
                InvokePortVfxRender(controller);
                Assert.That(ActivePortTransaction(), Is.Null);

                yield return EnterMarketQuantity("water", "7", purchase: false);
                beforeGold = session.ProfileGold;
                var saleCallback = MarketConfirm("water", false).onClick;
                saleCallback.Invoke();
                PortTransactionMotion sale = ActivePortTransaction();
                Assert.That(sale.ReceiptText, Does.Contain("−7"));
                Assert.That(sale.SecondaryText, Is.EqualTo("金币 +" + PortMarketCatalog.FormatPrice(session.ProfileGold - beforeGold)));
                saleCallback.Invoke();
                Assert.That(ActivePortTransaction(), Is.SameAs(sale));
                Assert.That(session.Cargo.Water, Is.Zero);
                sale.SetElapsed(PortTransactionMotion.Duration); yield return null;

                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                yield return EnterMarketQuantity("water", "3");
                MarketConfirm("water").onClick.Invoke(); yield return null;
                Assert.That(ActivePortTransaction(), Is.Null, "Opening the destination choice is not a completed trade.");
                ActiveButton("Market Buy To Warehouse").onClick.Invoke();
                PortTransactionMotion warehouse = ActivePortTransaction();
                Assert.That(warehouse.ReceiptText, Does.Contain("本港仓储").And.Contain("+3"));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(3));
                Assert.That(session.Cargo.Water, Is.Zero);
                warehouse.SetElapsed(PortTransactionMotion.Duration); yield return null;

                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ship);
                yield return EnterMarketQuantity("water", "1");
                var unaffordable = MarketConfirm("water").onClick;
                session.RestoreProfile(0m, session.CurrentPort);
                unaffordable.Invoke(); yield return null;
                Assert.That(ActivePortTransaction(), Is.Null, "Failed commits do not create a success receipt.");
                Assert.That(session.Cargo.Water, Is.Zero);
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AdditionalPortVfx_EquipmentMovesOnceAndStopsWithTheView()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            PopulateBareStarterForFixture(controller);
            var catalog = new ComponentArtCatalog();
            ComponentArtEntry entry = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent owned = controller.GrantOwnedComponent(entry);
            decimal gold = controller.Session.ProfileGold;
            int count = controller.OwnedComponents.Count;
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Ship 0");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + owned.InstanceId);
            var staleEquip = ActiveButton("Shipyard Equip Component").onClick;
            staleEquip.Invoke();
            PortTransactionMotion install = ActivePortTransaction();
            Assert.That(install, Is.Not.Null);
            Assert.That(install.ReceiptText, Does.Contain("已装配").And.Contain(entry.Name));
            Assert.That(Vector2.Distance(install.Source, install.Destination), Is.GreaterThan(20f));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(owned.InstanceId));
            AssertPortTransactionDoesNotInterceptInput(install);
            staleEquip.Invoke();
            Assert.That(ActivePortTransaction(), Is.SameAs(install));
            install.SetElapsed(.56f);
            AssertPortTransactionGeometry(install, "Shipyard Installed Art Bow", "Shipyard Installed Name Bow", "Shipyard Installed Name Plating",
                "Shipyard Equipment Current Comparison Stats", "Shipyard Equip Component");
            AssertPortTransactionTracksRepositionedTarget(install);
            NavalBattleVisualCapture.Capture(controller, "AdditionalVfx_03_ShipyardEquip.png", "配件装配：落位火花、实际属性变化与短亮", "Shipyard Equipment Page");
            install.SetElapsed(PortTransactionMotion.Duration); yield return null;
            InvokePortVfxRender(controller);
            Assert.That(ActivePortTransaction(), Is.Null, "Revisiting a dossier cannot replay an old fitting.");

            var staleUnload = ActiveButton("Shipyard Unequip Component Bow").onClick;
            staleUnload.Invoke();
            PortTransactionMotion unload = ActivePortTransaction();
            Assert.That(unload, Is.Not.Null);
            Assert.That(unload.ReceiptText, Does.Contain("已卸下").And.Contain(entry.Name));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            staleUnload.Invoke();
            Assert.That(ActivePortTransaction(), Is.SameAs(unload));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
            unload.SetElapsed(.56f);
            AssertPortTransactionGeometry(unload, "Shipyard Inventory Art " + owned.InstanceId, "Shipyard Inventory Name " + owned.InstanceId,
                "Shipyard Equipment Current Comparison Stats", "Shipyard Unequip Component Bow");
            NavalBattleVisualCapture.Capture(controller, "AdditionalVfx_03_ShipyardUnequip.png", "配件卸下：原物件移回库存，实际属性减少", "Shipyard Equipment Page");
            controller.enabled = false;
            yield return null;
            Assert.That(ActivePortTransaction(), Is.Null, "Owner deactivation must retire transient receipts and borrowed art.");
            controller.enabled = true;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AdditionalPortVfx_AmbienceRebindsSixPaintingsAndParksOutsideTheHub()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            PortAmbientGraphic retained = Object.FindFirstObjectByType<PortAmbientGraphic>();
            Assert.That(retained, Is.Not.Null);
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            string[] slugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
            for (int i = 0; i < ports.Length; i++)
            {
                controller.Session.RestoreProfile(controller.Session.ProfileGold, ports[i]);
                InvokePortVfxRender(controller); yield return null;
                PortAmbientGraphic live = Object.FindFirstObjectByType<PortAmbientGraphic>();
                Assert.That(live, Is.SameAs(retained));
                Assert.That(live.BoundArtwork, Is.EqualTo("port_hub_" + slugs[i] + "_v3"));
                Assert.That(live.WaterRegionCount, Is.EqualTo(2));
                Assert.That(live.HasSmoke, Is.EqualTo(i == 2));
                Assert.That(live.LampCount, Is.EqualTo(i == 4 ? 2 : 0));
                Assert.That(live.raycastTarget, Is.False);
                live.SetMotionTime(2.1f);
                Canvas.ForceUpdateCanvases();
                Mesh mesh = live.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                NavalBattleVisualCapture.Capture(controller, "AdditionalVfx_12_Port_" + slugs[i] + ".png", ports[i] + "：按原图局部水面与现有光源添加低强度动态", "Phase Content");
            }
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            Assert.That(retained.gameObject.activeInHierarchy, Is.False);
            Assert.That(retained.MotionTime, Is.Zero);
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(Object.FindFirstObjectByType<PortAmbientGraphic>(), Is.SameAs(retained));
            Assert.That(retained.BoundArtwork, Is.EqualTo("port_hub_gray_gull_v3"));
            LogAssert.NoUnexpectedReceived();
        }

        private static PortTransactionMotion ActivePortTransaction() => Object.FindObjectsByType<PortTransactionMotion>(FindObjectsSortMode.None)
            .FirstOrDefault(item => item.IsPlaying && item.gameObject.activeInHierarchy);

        private static void AssertPortTransactionDoesNotInterceptInput(PortTransactionMotion motion)
        {
            Assert.That(motion.GetComponentsInChildren<Button>(true), Is.Empty, "The effect has no skip or other action button.");
            foreach (Graphic graphic in motion.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
            foreach (CanvasGroup group in motion.GetComponentsInChildren<CanvasGroup>(true)) Assert.That(group.blocksRaycasts, Is.False);
        }

        private static void InvokePortVfxRender(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        private static void AssertPortTransactionGeometry(PortTransactionMotion motion, string targetName, params string[] readableControls)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform space = (RectTransform)motion.transform.parent;
            RectTransform target = space.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(item => item.name == targetName);
            Assert.That(target, Is.Not.Null, "The real landing target must be present: " + targetName);
            Assert.That(motion.DestinationAnchor, Is.SameAs(target), "Bind the real installed/inventory icon, not a sampled pre-layout position.");
            Vector2 expected = PortTransactionMotion.Anchor(space, target);
            Assert.That(Vector2.Distance(motion.Destination, expected), Is.LessThan(.5f));
            var drawn = motion.GetComponent<PortTransactionGraphic>();
            Assert.That(Vector2.Distance(drawn.Destination, expected), Is.LessThan(.5f), "The landing mesh uses the settled target position.");
            Rect receipt = PortVfxBoundsIn(space, motion.ReceiptRect);
            Assert.That(receipt.yMax, Is.LessThanOrEqualTo(space.rect.yMax - PhaseSurfaceLayout.HeaderHeight - 10f), "Receipts remain below the header.");
            foreach (string name in readableControls)
            {
                RectTransform control = space.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(item => item.name == name);
                Assert.That(control, Is.Not.Null, "The readability check requires the real visible control: " + name);
                Assert.That(receipt.Overlaps(PortVfxBoundsIn(space, control)), Is.False, "The receipt must not cover " + name);
            }
        }

        private static void AssertPortTransactionTracksRepositionedTarget(PortTransactionMotion motion)
        {
            RectTransform target = motion.DestinationAnchor;
            Vector2 original = target.anchoredPosition;
            try
            {
                target.anchoredPosition += new Vector2(17f, -11f);
                motion.SetElapsed(.56f);
                Canvas.ForceUpdateCanvases();
                Vector2 expected = PortTransactionMotion.Anchor((RectTransform)motion.transform.parent, target);
                Assert.That(Vector2.Distance(motion.GetComponent<PortTransactionGraphic>().Destination, expected), Is.LessThan(.5f),
                    "Late layout movement must carry the landing ring with its real target.");
            }
            finally { target.anchoredPosition = original; motion.SetElapsed(.56f); Canvas.ForceUpdateCanvases(); }
        }

        private static Rect PortVfxBoundsIn(RectTransform space, RectTransform target)
        {
            Vector3[] corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min = space.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return new Rect(min, max - min);
        }
    }
}
