using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    // Shares the main fixture's complete profile snapshot and restoration.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator Warehouse_ValuablesDefaultDoesNotInventATreasureForLegacyCompletedSaves()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickWarehouseButton("Port Hotspot Warehouse");
            yield return null;

            Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Background"), Is.Not.Null);
            Image collectionSurface = AssertLiveWarehouseCabinetSurface();
            Vector2 ledgerSize = collectionSurface.rectTransform.sizeDelta;
            Vector2 ledgerPosition = collectionSurface.rectTransform.anchoredPosition;
            Assert.That(GameObject.Find("Warehouse Collection Count").GetComponent<Text>().color.grayscale, Is.GreaterThan(.6f));
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(RequireWarehouseButton("Warehouse Display Selected").IsInteractable(), Is.False);
            Assert.That(RequireWarehouseButton("Warehouse Remove Selected").IsInteractable(), Is.False);
            Assert.That(ActiveButton("Warehouse Tab Basics"), Is.Null);
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Null,
                "The warehouse opens on the keepsake collection, with cargo on the port-storage tab.");
            NavalBattleVisualCapture.Capture(controller, "60_Warehouse_Valuables_Empty.png",
                "藏品陈列页默认打开；尚未获得藏品时保留空展位，不替旧存档虚构战利品", "Warehouse Panel");

            yield return ClickShipyardControl("Warehouse Tab Storage");
            Assert.That(AssertLiveWarehouseCabinetSurface(), Is.SameAs(collectionSurface));
            Assert.That(collectionSurface.rectTransform.sizeDelta, Is.EqualTo(ledgerSize));
            Assert.That(collectionSurface.rectTransform.anchoredPosition, Is.EqualTo(ledgerPosition));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(RequireWarehouseButton("Warehouse Storage Row " + PortMarketCatalog.Key(item)).IsInteractable(), Is.True);
            Assert.That(ActiveButton("Warehouse Tab Basics"), Is.Null);
            Assert.That(ActiveButton("Warehouse Deposit Travel Cash"), Is.Null);
            Assert.That(ActiveButton("Warehouse Restock"), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "63_Warehouse_Port_Storage.png",
                "藏品陈列、基础货物与珍稀物品分栏；九行基础物资继续使用调整或出售流程", "Warehouse Panel");
            ClickWarehouseButton("Warehouse Tab Valuables");
            yield return null;
            Assert.That(AssertLiveWarehouseCabinetSurface(), Is.SameAs(collectionSurface));
            Assert.That(GameObject.Find("Warehouse Collection Count").GetComponent<Text>().color.grayscale, Is.GreaterThan(.6f));
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(RequireWarehouseButton("Warehouse Display Selected").IsInteractable(), Is.False);
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Null);
            Assert.That(ActiveButton("Warehouse Tab Basics"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private static Image AssertLiveWarehouseCabinetSurface()
        {
            Image surface = GameObject.Find("Warehouse Ledger").GetComponent<Image>();
            Assert.That(surface.sprite, Is.Null, "Regional cabinet supplies the outer framing.");
            Assert.That(surface.color.a, Is.Zero);
            Assert.That(GameObject.Find("Warehouse Summary Bar"), Is.Not.Null);
            return surface;
        }

        [UnityTest]
        public IEnumerator Warehouse_SelectDisplayMoveRemoveAndReloadPreserveTheSingleOwnedKeepsake()
        {
            // An isolated saved-collection fixture; granting the real tutorial
            // reward is exercised by the three-voyage guided-flow test.
            string id = CoreWarehouseCollection.TutorialTreasureId;
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, id);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, ",,,,,");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal gold = controller.Session.ProfileGold;
            int hull = controller.Session.Hull;
            int[] cargo = WarehouseCargoSnapshot(controller);
            ClickWarehouseButton("Port Hotspot Warehouse");
            yield return null;
            ClickWarehouseButton("Warehouse Treasure " + id);
            yield return null;

            CoreWarehouseTreasureDefinition treasure = CoreWarehouseCollection.Catalog[0];
            string detail = string.Join("\n", GameObject.Find("Warehouse Panel")
                .GetComponentsInChildren<Text>().Select(label => label.text));
            Assert.That(detail, Does.Contain(treasure.Name));
            Assert.That(detail, Does.Contain(treasure.Description));
            Assert.That(detail, Does.Contain(treasure.Story));
            Assert.That(detail, Does.Contain(treasure.Source));
            Assert.That(controller.WarehouseCollection.IsDisplayed(id), Is.False);
            Assert.That(ActiveButton("Warehouse Sell Cargo"), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "61_Warehouse_Treasure_Detail.png",
                "破浪者的罗盘：可阅读来历与首胜故事，可选择展位陈列", "Warehouse Panel");
            ClickWarehouseButton("Warehouse Display Selected");
            yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey), Is.EqualTo(id + ",,,,,"));

            ClickWarehouseButton("Warehouse Display Slot 5");
            yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id),
                "Choosing a target alone must not silently move the keepsake.");
            ClickWarehouseButton("Warehouse Display Selected");
            yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty);
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(id));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey), Is.EqualTo(",,,,," + id));
            CollectionAssert.AreEqual(new[] { id }, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.Hull, Is.EqualTo(hull));
            CollectionAssert.AreEqual(cargo, WarehouseCargoSnapshot(controller));
            NavalBattleVisualCapture.Capture(controller, "62_Warehouse_Treasure_On_Display.png",
                "罗盘移动至第六展位，原展位恢复为空；陈列不改变资产与战斗数值", "Warehouse Panel");

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(id));
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty);
            CollectionAssert.AreEqual(new[] { id }, controller.WarehouseCollection.OwnedIds);
            ClickWarehouseButton("Port Hotspot Warehouse");
            yield return null;
            ClickWarehouseButton("Warehouse Treasure " + id);
            yield return null;
            ClickWarehouseButton("Warehouse Remove Selected");
            yield return null;
            Assert.That(controller.WarehouseCollection.IsDisplayed(id), Is.False);
            Assert.That(controller.WarehouseCollection.Owns(id), Is.True);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey), Is.EqualTo(",,,,,"));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey), Is.EqualTo(id));
            Assert.That(RequireWarehouseButton("Warehouse Remove Selected").IsInteractable(), Is.False);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(new[] { id }, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.IsDisplayed(id), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(cargo, WarehouseCargoSnapshot(controller));
            LogAssert.NoUnexpectedReceived();
        }

        private static int[] WarehouseCargoSnapshot(CoreGameplayDemoController controller)
        {
            var cargo = controller.Session.Cargo;
            return new[] { cargo.Water, cargo.Food, cargo.Beer, cargo.Medicine, cargo.Wood,
                cargo.Iron, cargo.Cloth, cargo.Powder, cargo.Cannonballs, cargo.Gold, cargo.Fame };
        }

        private static Button RequireWarehouseButton(string name)
        {
            Button button = ActiveButton(name);
            Assert.That(button, Is.Not.Null, "Missing warehouse UI action: " + name);
            return button;
        }

        private static void ClickWarehouseButton(string name)
        {
            Button button = RequireWarehouseButton(name);
            Assert.That(button.IsInteractable(), Is.True, "Warehouse action unexpectedly locked: " + name);
            button.onClick.Invoke();
        }
    }
}
