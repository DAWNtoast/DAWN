using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public IEnumerator VoyageCargo_ItemClicksRetainVisibleSheetGridAndDetailsWithoutRebuilding()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadVoyageCargoFixture();
                CoreVoyageSession session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                Dictionary<string, GameObject> retained = CaptureVoyageCargoObjects(new[]
                {
                    "Voyage Cargo Overlay", "Voyage Cargo Sheet", "Voyage Cargo Title", "Voyage Cargo Capacity",
                    "Close Voyage Cargo", "Voyage Cargo Basic Tab", "Voyage Cargo Special Tab", "Voyage Cargo List",
                    "Voyage Cargo Details", "Voyage Cargo Details Divider", "Voyage Cargo Notice"
                });
                foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.BasicItems)
                    foreach (string prefix in new[] { "Voyage Cargo Item ", "Voyage Cargo Item Icon ",
                        "Voyage Cargo Item Name ", "Voyage Cargo Item Amount ", "Voyage Cargo Item Volume " })
                        retained.Add(prefix + item.Key, GameObject.Find(prefix + item.Key));

                ClickAcceptanceButton("Voyage Cargo Item water");
                // Check before deferred Destroy runs: replacing the paper already causes a visible flash.
                AssertVoyageCargoObjectsRetained(retained);
                foreach (var item in CaptureVoyageCargoObjects(VoyageCargoDetailObjectNames)) retained.Add(item.Key, item.Value);
                AssertVoyageCargoSelectedContents(session, "water");
                yield return null;
                AssertVoyageCargoObjectsRetained(retained);

                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (string key in new[] { "food", "beer", "wood", "water" })
                    {
                        ClickAcceptanceButton("Voyage Cargo Item " + key);
                        AssertVoyageCargoObjectsRetained(retained);
                        AssertVoyageCargoSelectedContents(session, key);
                        Assert.That(GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text, Is.EqualTo("1"));
                    }
                    yield return null;
                    AssertVoyageCargoObjectsRetained(retained);
                    AssertVoyageCargoGrid(session, false);
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageCargo_RetainedSelectionPreservesQuantityAndRetiresPreviousItemActions()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadVoyageCargoFixture();
                CoreVoyageSession session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                ClickAcceptanceButton("Voyage Cargo Item wood"); yield return null;
                Dictionary<string, GameObject> shell = CaptureVoyageCargoObjects(new[]
                {
                    "Voyage Cargo Overlay", "Voyage Cargo Sheet", "Close Voyage Cargo", "Voyage Cargo List",
                    "Voyage Cargo Details", "Voyage Cargo Details Divider", "Voyage Cargo Capacity"
                });
                Dictionary<string, GameObject> details = CaptureVoyageCargoObjects(VoyageCargoDetailObjectNames);
                SetVoyageCargoQuantity("2");
                foreach (int ignored in Enumerable.Range(0, 3))
                {
                    ClickAcceptanceButton("Voyage Cargo Item wood");
                    Assert.That(GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text, Is.EqualTo("2"),
                        "Clicking the selected item must preserve the player's typed quantity.");
                    AssertVoyageCargoObjectsRetained(shell);
                    AssertVoyageCargoObjectsRetained(details);
                }
                Button.ButtonClickedEvent[] retired = new[]
                {
                    ActiveButton("Confirm Voyage Cargo Discard").onClick,
                    ActiveButton("Voyage Cargo Quantity Increase").onClick,
                    ActiveButton("Voyage Cargo Quantity Decrease").onClick,
                    ActiveButton("Voyage Cargo Discard All").onClick
                };
                int wood = session.Cargo.Wood, water = session.Cargo.Water;
                ClickAcceptanceButton("Voyage Cargo Item water");
                SetVoyageCargoQuantity("3");
                foreach (Button.ButtonClickedEvent oldAction in retired)
                {
                    oldAction.Invoke();
                    Assert.That(GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text, Is.EqualTo("3"),
                        "A cached action for the old item must not act on the new selection.");
                    Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
                    Assert.That(session.Cargo.Water, Is.EqualTo(water));
                }
                AssertVoyageCargoObjectsRetained(shell);
                AssertVoyageCargoObjectsRetained(details);
                AssertVoyageCargoSelectedContents(session, "water");
                ClickAcceptanceButton("Voyage Cargo Quantity Increase");
                Assert.That(GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text, Is.EqualTo("4"));
                ClickAcceptanceButton("Voyage Cargo Quantity Decrease");
                Assert.That(GameObject.Find("Voyage Cargo Quantity").GetComponent<InputField>().text, Is.EqualTo("3"));

                ClickAcceptanceButton("Voyage Cargo Special Tab");
                AssertVoyageCargoObjectsRetained(shell);
                yield return null;
                AssertVoyageCargoObjectsRetained(shell);
                AssertVoyageCargoGrid(session, true);
                ClickAcceptanceButton("Voyage Cargo Item gem");
                AssertVoyageCargoObjectsRetained(details);
                AssertVoyageCargoSelectedContents(session, "gem");
                ClickAcceptanceButton("Voyage Cargo Basic Tab"); yield return null;
                AssertVoyageCargoObjectsRetained(shell);
                AssertVoyageCargoGrid(session, false);
                ClickAcceptanceButton("Voyage Cargo Item food");
                AssertVoyageCargoObjectsRetained(details);
                AssertVoyageCargoSelectedContents(session, "food");
                string capacity = GameObject.Find("Voyage Cargo Capacity").GetComponent<Text>().text;
                int food = session.Cargo.Food;
                Button.ButtonClickedEvent staleStock = ActiveButton("Confirm Voyage Cargo Discard").onClick;
                session.Cargo.Food += 1;
                staleStock.Invoke();
                Assert.That(session.Cargo.Food, Is.EqualTo(food + 1), "A changed inventory invalidates the displayed quote.");
                AssertVoyageCargoObjectsRetained(shell);
                Assert.That(GameObject.Find("Voyage Cargo Capacity").GetComponent<Text>().text, Is.Not.EqualTo(capacity));
                Assert.That(GameObject.Find("Voyage Cargo Selection Hint"), Is.Not.Null);
                yield return null;
                ClickAcceptanceButton("Voyage Cargo Item food");
                AssertVoyageCargoObjectsRetained(details);
                AssertVoyageCargoSelectedContents(session, "food");
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static readonly string[] VoyageCargoDetailObjectNames =
        {
            "Voyage Cargo Selected Icon", "Voyage Cargo Selected Item", "Voyage Cargo Selected Amount",
            "Voyage Cargo Unit Volume", "Voyage Cargo Item Description", "Voyage Cargo Quantity Label",
            "Voyage Cargo Quantity", "Voyage Cargo Quantity Text", "Voyage Cargo Quantity Decrease",
            "Voyage Cargo Quantity Increase", "Voyage Cargo Discard All", "Voyage Cargo Discard Warning",
            "Confirm Voyage Cargo Discard"
        };

        private static Dictionary<string, GameObject> CaptureVoyageCargoObjects(IEnumerable<string> names) =>
            names.ToDictionary(name => name, name =>
            {
                GameObject view = GameObject.Find(name);
                Assert.That(view, Is.Not.Null, name);
                return view;
            });

        private static void AssertVoyageCargoObjectsRetained(Dictionary<string, GameObject> expected)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in expected)
            {
                Assert.That(item.Value != null && item.Value.activeInHierarchy, Is.True,
                    item.Key + " must stay visible during selection, without replacement or an inactive frame.");
                Assert.That(GameObject.Find(item.Key), Is.SameAs(item.Value), item.Key + " must be updated in place.");
                Assert.That(all.Count(view => view.name == item.Key), Is.EqualTo(1),
                    item.Key + " must not leave a duplicate pending destruction after the click.");
            }
        }

        private static void AssertVoyageCargoSelectedContents(CoreVoyageSession session, string key)
        {
            CoreWarehouseItemDefinition selected = CoreWarehouseItemCatalog.Find(key);
            Assert.That(GameObject.Find("Voyage Cargo Selected Item").GetComponent<Text>().text, Is.EqualTo(selected.Name));
            Assert.That(GameObject.Find("Voyage Cargo Selected Amount").GetComponent<Text>().text,
                Is.EqualTo("随船数量：" + session.GetWarehouseCargoAmount(key)));
            Assert.That(GameObject.Find("Voyage Cargo Unit Volume").GetComponent<Text>().text,
                Is.EqualTo("每件占用：" + selected.UnitVolume.ToString("0.##", CultureInfo.InvariantCulture)));
            Assert.That(GameObject.Find("Voyage Cargo Item Description").GetComponent<Text>().text, Is.EqualTo(selected.Description));
            AssertItemIcon("Voyage Cargo Selected Icon", ItemIconCatalog.ForCargo(key), 90f);
            foreach (Button row in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.name.StartsWith("Voyage Cargo Item ", StringComparison.Ordinal)))
            {
                bool isSelected = row.name == "Voyage Cargo Item " + key;
                Transform[] borders = row.GetComponentsInChildren<Transform>(true)
                    .Where(child => child.name.StartsWith("Voyage Cargo Slot Border ", StringComparison.Ordinal)).ToArray();
                Assert.That(borders.Length, Is.EqualTo(4), row.name + " must update its existing outline.");
                Color expectedFace = isSelected ? new Color(.95f, .86f, .67f, 1f) : new Color(.94f, .91f, .84f, 1f);
                Color actualFace = row.colors.normalColor;
                Assert.That(actualFace.r, Is.EqualTo(expectedFace.r).Within(.001f), row.name);
                Assert.That(actualFace.g, Is.EqualTo(expectedFace.g).Within(.001f), row.name);
                Assert.That(actualFace.b, Is.EqualTo(expectedFace.b).Within(.001f), row.name);
                Assert.That(actualFace.a, Is.EqualTo(1f).Within(.001f), row.name);
                Sprite face = row.GetComponent<Image>().sprite;
                Assert.That(face, Is.Not.Null, row.name);
                Assert.That(face.name, Is.EqualTo("Market Light Control"), row.name);
                foreach (Transform border in borders)
                    Assert.That(border.GetComponent<Image>().color.a, Is.EqualTo(isSelected ? .85f : .48f).Within(.001f), row.name);
            }
        }
    }
}
