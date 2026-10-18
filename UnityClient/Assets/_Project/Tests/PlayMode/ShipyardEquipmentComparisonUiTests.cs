using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [UnityTest]
        public IEnumerator ShipyardEquipment_ComparisonFollowsRealSelectionReplacementAndEmptySlots()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry bowEntry = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Bow && entry.Rarity == ComponentRarity.Rare);
            ComponentArtEntry sailEntry = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Sail && entry.Rarity == ComponentRarity.Rare);
            OwnedShipComponent bow = controller.GrantOwnedComponent(bowEntry);
            OwnedShipComponent sail = controller.GrantOwnedComponent(sailEntry);
            ComponentArtEntry fullEffectsEntry = catalog.GetById("bow_freewake_lance");
            OwnedShipComponent fullEffects = controller.GrantOwnedComponent(fullEffectsEntry);
            ComponentArtEntry longestNameEntry = catalog.Entries.OrderByDescending(entry => entry.Name.Length).First();
            OwnedShipComponent longestName = controller.GrantOwnedComponent(longestNameEntry);
            ComponentArtEntry oldBow = controller.ShipComponent(0, ComponentSlot.Bow);
            ComponentArtEntry oldSail = controller.ShipComponent(0, ComponentSlot.Sail);
            decimal gold = controller.Session.ProfileGold;

            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Ship 0");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            foreach (string removed in new[] { "Shipyard Dossier Price", "Confirm Shipyard Vessel", "Shipyard Equipment Feedback",
                "Shipyard Equipment Selection Summary", "Shipyard Equipment Inventory Pagination" })
                Assert.That(GameObject.Find(removed), Is.Null, removed + " must not occupy the equipment page.");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False);
            AssertShipyardComparisonLayout();

            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            Assert.That(oldBow, Is.Null, "The new starter hull must expose an empty bow slot.");
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain("未装配"));
            Assert.That(ShipyardComparisonLabel("Selected", "Name"), Does.Contain(bowEntry.Name));
            Assert.That(ShipyardComparisonLabel("Selected", "Stats"), Does.Not.Match("[（(].*[）)]"), "The empty starter slot has no current fields to compare.");
            Assert.That(ShipyardComparisonLabel("Current", "Stats"), Does.Not.Match("[（(].*[）)]"), "Differences belong in the right comparison panel.");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null, "Inspecting a candidate must not install it.");

            yield return ClickShipyardControl("Shipyard Equipment Owned " + sail.InstanceId);
            Assert.That(oldSail, Is.Null);
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain("未装配"), "An empty sail must compare against an empty slot, not the last inspected bow.");
            Assert.That(ShipyardComparisonLabel("Selected", "Name"), Does.Contain(sailEntry.Name));
            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(bow.InstanceId));
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain(bowEntry.Name));
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False, "Successful installation clears the candidate selection.");

            yield return ClickShipyardControl("Shipyard Unequip Component Bow");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain("未装配"));
            Assert.That(ShipyardComparisonLabel("Selected", "Stats"), Does.Not.Match("[（(].*[）)]"), "An empty slot has no existing fields to compare.");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.True);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            AssertShipyardComparisonLayout();

            // The largest authored effect set contains the five actual bonuses.
            // Preserve its text size and reach the last row through a scroll
            // when the font's measured bounds exceed the comparison pane.
            yield return ClickShipyardControl("Shipyard Equipment All Components");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + fullEffects.InstanceId);
            Text fullStats = GameObject.Find("Shipyard Equipment Selected Comparison Stats").GetComponent<Text>();
            Assert.That(fullStats.text.Split('\n').Length, Is.EqualTo(5));
            Assert.That(fullStats.text.Split('\n').Last(), Does.StartWith("视野"));
            Assert.That(fullStats.text, Does.Not.Contain("品质"));
            Assert.That(fullStats.fontSize, Is.GreaterThanOrEqualTo(20));
            Assert.That(fullStats.preferredHeight, Is.LessThanOrEqualTo(fullStats.rectTransform.rect.height), "All five rows remain in the measured text mesh.");
            ScrollRect comparisonScroll = fullStats.GetComponentInParent<ScrollRect>();
            NavalBattleVisualCapture.Capture(controller, "85_Shipyard_FiveEffects_Top.png", "传说部件五项实际属性：比较框保留字号", "Shipyard Detail");
            if (comparisonScroll != null)
            {
                Assert.That(comparisonScroll.content.rect.height, Is.GreaterThan(comparisonScroll.viewport.rect.height));
                comparisonScroll.verticalNormalizedPosition = 0f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(comparisonScroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.01f));
                Rect finalRows = ShipyardRectIn(comparisonScroll.viewport, fullStats.name);
                Assert.That(finalRows.yMin, Is.GreaterThanOrEqualTo(comparisonScroll.viewport.rect.yMin - 1f), "The final vision row must be visible at the bottom of the scroll.");
            }
            Assert.That(GameObject.Find("Shipyard Equipment Page").GetComponentsInChildren<Text>()
                .Any(label => label.text.Contains("品质") || label.text.Contains("%")), Is.False);
            NavalBattleVisualCapture.Capture(controller, "86_Shipyard_FiveEffects_Bottom.png", "视野末行完整显示，底部装配按钮保持可点击", "Shipyard Detail");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + longestName.InstanceId);
            foreach (string labelName in new[] { "Shipyard Inventory Name " + longestName.InstanceId, "Shipyard Equipment Selected Comparison Name" })
            {
                Text label = GameObject.Find(labelName).GetComponent<Text>();
                Assert.That(label.text, Is.EqualTo(longestNameEntry.Name));
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width), "The longest component name must not truncate: " + longestNameEntry.Name);
                Assert.That(label.resizeTextForBestFit, Is.False);
            }
            Assert.That(controller.InstalledShipComponent(0, fullEffectsEntry.Slot), Is.Null, "Reading the full comparison does not install the candidate.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            yield return ClickShipyardControl("Close Shipyard Equipment");
            Assert.That(GameObject.Find("Shipyard Equipment Overlay"), Is.Null);
            yield return ClickShipyardControl("Return To Port Hub");
            Assert.That(GameObject.Find("Shipyard Detail"), Is.Null);
            Assert.That(ActiveButton("Shipyard Ship 0"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private static string ShipyardComparisonLabel(string side, string field)
        {
            GameObject label = GameObject.Find("Shipyard Equipment " + side + " Comparison " + field);
            Assert.That(label, Is.Not.Null, side + " " + field);
            return label.GetComponent<Text>().text;
        }

        private static void AssertShipyardComparisonLayout()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform detail = GameObject.Find("Shipyard Detail").GetComponent<RectTransform>();
            Rect equip = ShipyardRectIn(detail, "Shipyard Equip Component");
            Rect back = ShipyardRectIn(detail, "Close Shipyard Equipment");
            Rect current = ShipyardRectIn(detail, "Shipyard Equipment Current Comparison");
            Rect selected = ShipyardRectIn(detail, "Shipyard Equipment Selected Comparison");
            Rect vessel = ShipyardRectIn(detail, "Shipyard Dossier Ship Window");
            Rect inventory = ShipyardRectIn(detail, "Shipyard Equipment Inventory");
            Button returnButton = ActiveButton("Close Shipyard Equipment");
            Assert.That(returnButton.GetComponentInChildren<Text>().text, Does.Contain("返回船只档案"));
            Assert.That(detail.GetComponentsInChildren<Button>(true).Count(button =>
                button.GetComponentInChildren<Text>(true)?.text.Contains("返回") == true), Is.EqualTo(1));
            Assert.That(current.xMax, Is.LessThan(selected.xMin));
            Assert.That(current.yMin, Is.EqualTo(selected.yMin).Within(0.5f));
            Assert.That(equip.yMax, Is.LessThan(current.yMin));
            Assert.That(vessel.xMax, Is.LessThan(inventory.xMin), "The ship preview stays left of the inventory.");
            Assert.That(inventory.xMax, Is.LessThan(current.xMin), "Inventory and comparison occupy separate columns.");
            Assert.That(back.xMax, Is.LessThan(0));
            Assert.That(back.yMin, Is.GreaterThan(ShipyardRectIn(detail, "Shipyard Dossier Name").yMax));
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                Rect installed = ShipyardRectIn(detail, "Shipyard Equipment Slot " + slot);
                Assert.That(installed.height, Is.GreaterThanOrEqualTo(52f), "Installed fittings retain enough room for their two text lines.");
                Assert.That(installed.Overlaps(vessel), Is.False, "Fittings surround the ship without covering it.");
                Assert.That(installed.xMax, Is.LessThan(inventory.xMin), "Fittings stay in the ship preview area.");
            }
            foreach (Rect content in new[] { inventory, current, selected, equip })
                Assert.That(content.Overlaps(vessel), Is.False, "The equipment comparison and action must not cover the ship illustration.");
        }

        [UnityTest]
        public IEnumerator ShipyardEquipment_FullscreenWindowConsumesEmptySpaceAndReturnsToItsOpeningPage()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return EscapeNavigationLoadPort();
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                decimal gold = controller.Session.ProfileGold;
                int flagship = controller.Session.SelectedShipIndex;
                yield return ClickShipyardControl("Port Hotspot Shipyard");
                yield return ClickShipyardControl("Shipyard Ship 0");
                // Cache settings in the normal fixed viewport before opening
                // equipment, which reparents the page to the full canvas.
                yield return ClickShipyardControl("Port Facility Settings");
                GameObject retainedSettings = GameObject.Find("Port Settings Overlay");
                AssertShipyardSettingsAboveCurrentPage(retainedSettings);
                yield return ClickShipyardControl("Close Port Settings");
                foreach (string openingPage in new[] { "Performance", "Background" })
                {
                    yield return ClickShipyardControl("Shipyard Dossier Tab " + openingPage);
                    GameObject retainedHeader = GameObject.Find("Port Facility Header");
                    foreach (bool useEscape in new[] { false, true })
                    {
                        yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
                        Canvas.ForceUpdateCanvases();
                        Image background = GameObject.Find("Shipyard Equipment Overlay").GetComponent<Image>();
                        Assert.That(background.sprite, Is.Null, "The opaque backing prevents harbour scenery showing around the paper workspace.");
                        Assert.That(background.color.a, Is.EqualTo(1f));
                        Assert.That(background.raycastTarget, Is.True);
                        Image paper = GameObject.Find("Shipyard Equipment Paper").GetComponent<Image>();
                        Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper));
                        Assert.That(paper.color.a, Is.EqualTo(1f));
                        Assert.That(paper.raycastTarget, Is.False);
                        Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(retainedHeader));
                        Canvas canvas = background.GetComponentInParent<Canvas>().rootCanvas;
                        RectTransform canvasRect = (RectTransform)canvas.transform;
                        Rect coverage = ShipyardRectIn(canvasRect, background.name);
                        Assert.That(coverage.xMin, Is.LessThanOrEqualTo(canvasRect.rect.xMin + .1f));
                        Assert.That(coverage.xMax, Is.GreaterThanOrEqualTo(canvasRect.rect.xMax - .1f));
                        Assert.That(coverage.yMin, Is.LessThanOrEqualTo(canvasRect.rect.yMin + .1f));
                        Assert.That(coverage.yMax, Is.GreaterThanOrEqualTo(canvasRect.rect.yMax - .1f));
                        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                        Rect rect = background.rectTransform.rect;
                        PointerEventData pointer = new PointerEventData(EventSystem.current)
                        {
                            button = PointerEventData.InputButton.Left,
                            position = RectTransformUtility.WorldToScreenPoint(camera, background.rectTransform.TransformPoint(
                                new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, .005f), Mathf.Lerp(rect.yMin, rect.yMax, .005f))))
                        };
                        var hits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointer, hits);
                        Assert.That(hits, Is.Not.Empty);
                        Assert.That(hits[0].gameObject, Is.SameAs(background.gameObject), "Empty space must consume the pointer before underlying harbour controls.");
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.Null,
                            "Clicking the solid background must not invoke an underlying button or dismiss the window.");
                        Button headerReturn = ActiveButton("Return To Port Hub");
                        Assert.That(headerReturn.interactable, Is.True);
                        RectTransform returnRect = (RectTransform)headerReturn.transform;
                        pointer.position = RectTransformUtility.WorldToScreenPoint(camera, returnRect.TransformPoint(returnRect.rect.center));
                        hits.Clear();
                        EventSystem.current.RaycastAll(pointer, hits);
                        Assert.That(hits, Is.Not.Empty);
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(headerReturn.gameObject),
                            "The retained facility header remains reachable above the equipment backdrop.");
                        yield return ClickShipyardControl("Port Facility Settings");
                        AssertShipyardSettingsAboveCurrentPage(retainedSettings);
                        yield return ClickShipyardControl("Close Port Settings");
                        Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null);
                        yield return ClickShipyardControl("Shipyard Equipment Slot Bow");
                        Assert.That(GameObject.Find("Shipyard Equipment Overlay"), Is.Not.Null,
                            "Closing settings restores real equipment pointer interaction without leaving the window.");
                        if (useEscape) yield return input.Press();
                        else yield return ClickShipyardControl("Close Shipyard Equipment");
                        Assert.That(GameObject.Find("Shipyard Equipment Overlay"), Is.Null);
                        Assert.That(GameObject.Find("Shipyard Detail"), Is.Not.Null);
                        Assert.That(GameObject.Find("Shipyard Dossier Story") != null, Is.EqualTo(openingPage == "Background"));
                        Assert.That(GameObject.Find("Shipyard Dossier Stat 0") != null, Is.EqualTo(openingPage == "Performance"));
                        yield return ClickShipyardControl("Port Facility Settings");
                        AssertShipyardSettingsAboveCurrentPage(retainedSettings);
                        yield return ClickShipyardControl("Close Port Settings");
                    }
                }
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(flagship));
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void AssertShipyardSettingsAboveCurrentPage(GameObject retainedSettings)
        {
            GameObject settings = GameObject.Find("Port Settings Overlay");
            Assert.That(settings, Is.SameAs(retainedSettings), "Settings must reuse the cached view across normal and full-screen pages.");
            Transform content = GameObject.Find("Phase Content").transform;
            Assert.That(settings.transform.parent, Is.SameAs(content.parent), "Cached settings must follow the current page's modal host.");
            Assert.That(settings.transform.GetSiblingIndex(), Is.GreaterThan(content.GetSiblingIndex()),
                "The settings surface must draw above the active page.");
            Canvas.ForceUpdateCanvases();
            Canvas canvas = settings.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform bounds = settings.GetComponent<RectTransform>();
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, bounds.TransformPoint(
                    new Vector2(Mathf.Lerp(bounds.rect.xMin, bounds.rect.xMax, .01f), Mathf.Lerp(bounds.rect.yMin, bounds.rect.yMax, .01f))))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject == settings || hits[0].gameObject.transform.IsChildOf(settings.transform), Is.True,
                "Settings must receive pointer hits before the equipment or normal page beneath it.");
        }

        private static Rect ShipyardRectIn(RectTransform parent, string name)
        {
            Vector3[] corners = new Vector3[4];
            GameObject target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, name);
            target.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
    }
}
