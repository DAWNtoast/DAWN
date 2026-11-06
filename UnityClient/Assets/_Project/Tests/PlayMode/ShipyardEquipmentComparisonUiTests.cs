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
            AssertShipyardComparisonMetadata("Current", oldBow);
            AssertShipyardComparisonMetadata("Selected", null);
            AssertShipyardComparisonLayout();

            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            Assert.That(oldBow, Is.Null, "The new starter hull must expose an empty bow slot.");
            AssertShipyardComparisonMetadata("Current", null);
            AssertShipyardComparisonMetadata("Selected", bowEntry);
            Assert.That(ShipyardComparisonLabel("Selected", "Stats"), Does.Not.Match("[（(].*[）)]"), "The empty starter slot has no current fields to compare.");
            Assert.That(ShipyardComparisonLabel("Current", "Stats"), Does.Not.Match("[（(].*[）)]"), "Differences belong in the right comparison panel.");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null, "Inspecting a candidate must not install it.");

            yield return ClickShipyardControl("Shipyard Equipment Owned " + sail.InstanceId);
            Assert.That(oldSail, Is.Null);
            AssertShipyardComparisonMetadata("Current", null);
            AssertShipyardComparisonMetadata("Selected", sailEntry);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(bow.InstanceId));
            AssertShipyardComparisonMetadata("Current", bowEntry);
            AssertShipyardComparisonMetadata("Selected", null);
            AssertShipyardRarityBoard(ActiveButton("Shipyard Equipment Slot Bow").GetComponent<Image>(), bowEntry.Rarity);
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False, "Successful installation clears the candidate selection.");

            Button.ButtonClickedEvent previousUnload = ActiveButton("Shipyard Unequip Component Bow").onClick;
            yield return ClickShipyardControl("Shipyard Equipment Slot Sail");
            Assert.That(GameObject.Find("Shipyard Unequip Component Bow"), Is.Null);
            Assert.That(ActiveButton("Shipyard Unequip Component Sail").interactable, Is.False, "The inspected empty sail has nothing to unload.");
            previousUnload.Invoke();
            yield return null;
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(bow.InstanceId),
                "A retained unload action must not remove the previously inspected fitting after the slot changes.");
            yield return ClickShipyardControl("Shipyard Equipment Slot Bow");
            yield return ClickShipyardControl("Shipyard Unequip Component Bow");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            AssertShipyardComparisonMetadata("Current", null);
            AssertShipyardComparisonMetadata("Selected", null);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            AssertShipyardComparisonMetadata("Current", null);
            Assert.That(ShipyardComparisonLabel("Selected", "Stats"), Does.Not.Match("[（(].*[）)]"), "An empty slot has no existing fields to compare.");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.True);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            AssertShipyardComparisonLayout();

            // The largest authored effect set contains the five actual bonuses.
            // Preserve its text size and reach the last row through a scroll
            // when the font's measured bounds exceed the comparison pane.
            yield return ClickShipyardControl("Shipyard Equipment All Components");
            string[] inventoryBeforeComparison = controller.OwnedComponents
                .Select(item => item.InstanceId + ":" + item.ShipIndex).OrderBy(value => value).ToArray();
            yield return ClickShipyardControl("Shipyard Equipment Owned " + fullEffects.InstanceId);
            AssertShipyardComparisonMetadata("Selected", fullEffectsEntry);
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
                bool overflows = comparisonScroll.content.rect.height > comparisonScroll.viewport.rect.height + .5f;
                Assert.That(comparisonScroll.vertical, Is.EqualTo(overflows),
                    "The resized comparison pane only scrolls while its measured content exceeds the viewport.");
                if (overflows)
                {
                    comparisonScroll.verticalNormalizedPosition = 0f;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(comparisonScroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.01f));
                }
                Rect finalRows = ShipyardRectIn(comparisonScroll.viewport, fullStats.name);
                Assert.That(finalRows.yMin, Is.GreaterThanOrEqualTo(comparisonScroll.viewport.rect.yMin - 1f),
                    "The final vision row must be visible, either directly or at the bottom of the scroll.");
                if (!overflows)
                    Assert.That(finalRows.yMax, Is.LessThanOrEqualTo(comparisonScroll.viewport.rect.yMax + 1f),
                        "When all rows fit, the first row also stays inside the expanded viewport without scrolling.");
            }
            Assert.That(new[] { ShipyardComparisonLabel("Current", "Stats"), fullStats.text }
                .Any(value => value.Contains("品质") || value.Contains("%")), Is.False,
                "Equipment stats remain flat values; an attached card may legitimately have percentage effects.");
            NavalBattleVisualCapture.Capture(controller, "86_Shipyard_FiveEffects_Bottom.png", "视野末行完整显示，底部装配按钮保持可点击", "Shipyard Detail");
            CardKeywordTooltip selectedTooltip = ShowShipyardAttachedCardHover("Selected", fullEffectsEntry);
            yield return null;
            NavalBattleVisualCapture.Capture(controller, "87_Shipyard_AttachedCard_Hover.png", "配件附带卡牌复用工坊悬停详情", "Shipyard Detail");
            GameObject selectedPreview = GameObject.Find("Shipyard Equipment Selected Comparison Attached Card Preview");
            ExecuteEvents.Execute(selectedPreview, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            Assert.That(VisibleCollectionKeywords(), Is.Empty, "Moving off the attached card dismisses its details.");
            ShowShipyardAttachedCardHover("Selected", fullEffectsEntry);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + longestName.InstanceId);
            Assert.That(selectedTooltip == null || !selectedTooltip.gameObject.activeInHierarchy, Is.True,
                "Changing the inspected component closes the previous card's details.");
            Assert.That(VisibleCollectionKeywords(), Is.Empty);
            AssertShipyardComparisonMetadata("Selected", longestNameEntry);
            foreach (string labelName in new[] { "Shipyard Inventory Name " + longestName.InstanceId })
            {
                Text label = GameObject.Find(labelName).GetComponent<Text>();
                Assert.That(label.text, Is.EqualTo(longestNameEntry.Name));
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width), "The longest component name must not truncate: " + longestNameEntry.Name);
                Assert.That(label.resizeTextForBestFit, Is.False);
            }
            Assert.That(controller.InstalledShipComponent(0, fullEffectsEntry.Slot), Is.Null, "Reading the full comparison does not install the candidate.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(inventoryBeforeComparison, controller.OwnedComponents
                .Select(item => item.InstanceId + ":" + item.ShipIndex).OrderBy(value => value).ToArray(),
                "Reading rarity and attached cards must not acquire, equip or consume any physical component.");

            // The special-card preview follows the item across installation and removal.
            yield return ClickShipyardControl("Shipyard Equipment Owned " + fullEffects.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            AssertShipyardComparisonMetadata("Current", fullEffectsEntry);
            AssertShipyardComparisonMetadata("Selected", null);
            CardKeywordTooltip currentTooltip = ShowShipyardAttachedCardHover("Current", fullEffectsEntry);
            yield return ClickShipyardControl("Shipyard Unequip Component Bow");
            Assert.That(currentTooltip == null || !currentTooltip.gameObject.activeInHierarchy, Is.True,
                "Unloading a component also closes its attached-card details.");
            Assert.That(VisibleCollectionKeywords(), Is.Empty);
            AssertShipyardComparisonMetadata("Current", null);
            AssertShipyardComparisonMetadata("Selected", null);

            ComponentArtEntry noCardEntry = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Bow &&
                entry.Rarity == ComponentRarity.Common && CoreSpecialCardCatalog.ForComponent(entry.Id) == null);
            OwnedShipComponent noCard = controller.GrantOwnedComponent(noCardEntry);
            yield return ClickShipyardControl("Shipyard Equipment Slot Bow");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + noCard.InstanceId);
            AssertShipyardComparisonMetadata("Selected", noCardEntry);
            Assert.That(ShipyardComparisonLabel("Selected", "Attached Card Name"), Is.EqualTo("无附带卡牌"),
                "A real component without an attached card is distinct from an empty selection.");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            yield return ClickShipyardControl("Return To Port Hub");
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

        private static void AssertShipyardRarityBoard(Image board, ComponentRarity rarity)
        {
            // A live Image can retain its generated sprite after the shared lookup cache is released.
            Sprite expected = ComponentRarityWoodSkin.FaceFor(rarity);
            Assert.That(expected, Is.Not.Null);
            Assert.That(board.sprite, Is.Not.Null);
            Assert.That(board.sprite.texture, Is.SameAs(expected.texture), "The board uses the authored texture for this rarity.");
            Assert.That(board.sprite.rect, Is.EqualTo(expected.rect));
            Assert.That(board.sprite.border, Is.EqualTo(expected.border));
            Assert.That(board.type, Is.EqualTo(Image.Type.Sliced));
        }

        private static void AssertShipyardComparisonMetadata(string side, ComponentArtEntry entry)
        {
            string prefix = "Shipyard Equipment " + side + " Comparison";
            Image board = GameObject.Find(prefix + " Component Board").GetComponent<Image>();
            AssertShipyardRarityBoard(board, entry?.Rarity ?? ComponentRarity.Common);
            foreach (string removed in new[] { " Name", " Slot", " Rarity" })
                Assert.That(GameObject.Find(prefix + removed), Is.Null, "The compact component area only shows its icon.");
            if (entry == null)
                Assert.That(ShipyardComparisonLabel(side, "Empty Art"), Is.EqualTo(side == "Current" ? "未装配" : "未选择配件"));
            else
            {
                RawImage icon = GameObject.Find(prefix + " Art").GetComponent<RawImage>();
                ItemIconArt expected = ItemIconCatalog.ForComponent(entry);
                Assert.That(icon.texture, Is.SameAs(expected.Texture), "The icon follows the actual inspected component.");
                Assert.That(icon.uvRect, Is.EqualTo(expected.Uv));
                Assert.That(icon.transform.IsChildOf(board.transform), Is.True);
                Assert.That(board.GetComponentsInChildren<Text>(), Is.Empty);
            }
            CoreSpecialCardDefinition attached = entry == null ? null : CoreSpecialCardCatalog.ForComponent(entry.Id);
            GameObject preview = GameObject.Find(prefix + " Attached Card Preview");
            Assert.That(preview != null, Is.EqualTo(attached != null),
                "The card face must appear only for this component's actual attached card.");
            if (preview != null)
            {
                Assert.That(ShipyardComparisonLabel(side, "Attached Card Face Name"), Does.Contain(attached.Card.Name));
                Assert.That(GameObject.Find(prefix + " Attached Card Name"), Is.Null,
                    "The enlarged card carries its own title without a duplicate side label.");
                Assert.That(preview.GetComponentsInChildren<RawImage>().Any(image => image.texture != null), Is.True,
                    "The attached-card preview includes its authored illustration.");
                Assert.That(preview.GetComponent<CollectionCardKeywordHover>(), Is.Not.Null);
                Assert.That(preview.GetComponent<Image>().raycastTarget, Is.True);
                RectTransform comparison = board.transform.parent.GetComponent<RectTransform>();
                Assert.That(ShipyardRectIn(comparison, preview.name).height,
                    Is.GreaterThan(ShipyardRectIn(comparison, board.name).height * 1.5f),
                    "The compact component icon reserves the larger display area for its attached card.");
            }
            else
                Assert.That(ShipyardComparisonLabel(side, "Attached Card Name"), Is.EqualTo(entry == null ? "—" : "无附带卡牌"));
        }

        private static CardKeywordTooltip ShowShipyardAttachedCardHover(string side, ComponentArtEntry entry)
        {
            string prefix = "Shipyard Equipment " + side + " Comparison";
            GameObject preview = GameObject.Find(prefix + " Attached Card Preview");
            Assert.That(preview, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = preview.GetComponent<RectTransform>();
            Canvas canvas = preview.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.SameAs(preview), "The full visible card receives pointer hover.");
            Assert.That(ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerEnterHandler), Is.True);
            CardKeywordTooltip[] details = VisibleCollectionKeywords();
            Assert.That(details, Has.Length.EqualTo(1), "Attached cards reuse the workshop's hover details.");
            Text effect = GameObject.Find(prefix + " Attached Card Effect").GetComponent<Text>();
            string description = (string)typeof(CardDescriptionText).GetMethod("SourceFor",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { effect });
            var expected = CoreBattleCardKeywordText.Build(CoreSpecialCardCatalog.ForComponent(entry.Id).Card, description);
            CollectionAssert.AreEqual(expected.SelectMany(item => new[] { item.Keyword, item.Meaning }).ToArray(),
                details[0].GetComponentsInChildren<Text>().Select(label => label.text).ToArray());
            Assert.That(details[0].GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True,
                "The details do not intercept the pointer or the component controls.");
            return details[0];
        }

        private static void AssertShipyardComparisonLayout()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform detail = GameObject.Find("Shipyard Detail").GetComponent<RectTransform>();
            Rect equip = ShipyardRectIn(detail, "Shipyard Equip Component");
            Rect current = ShipyardRectIn(detail, "Shipyard Equipment Current Comparison");
            Rect selected = ShipyardRectIn(detail, "Shipyard Equipment Selected Comparison");
            Rect vessel = ShipyardRectIn(detail, "Shipyard Dossier Ship Window");
            Rect inventory = ShipyardRectIn(detail, "Shipyard Equipment Inventory");
            Button returnButton = ActiveButton("Return To Port Hub");
            Assert.That(returnButton.GetComponentInChildren<Text>().text, Does.Contain("返回船只档案"));
            Assert.That(returnButton.transform.IsChildOf(GameObject.Find("Port Facility Header").transform), Is.True);
            foreach (string removed in new[] { "Close Shipyard Equipment", "Shipyard Equipment Window Title", "Shipyard Equipment Header Rule",
                "Shipyard Dossier Name", "Shipyard Dossier Role" })
                Assert.That(GameObject.Find(removed), Is.Null, "The equipment page must use the shared header instead of a second title row.");
            RectTransform vesselArea = GameObject.Find("Shipyard Equipment Vessel Surface").GetComponent<RectTransform>();
            Assert.That(vesselArea.GetComponentsInChildren<Graphic>(true), Is.Empty,
                "The ship and its fittings share the outer paper without an inner frame.");
            Assert.That(current.xMax, Is.LessThan(selected.xMin));
            Assert.That(current.yMin, Is.EqualTo(selected.yMin).Within(0.5f));
            Assert.That(equip.yMax, Is.LessThan(current.yMin));
            Assert.That(vessel.xMax, Is.LessThan(inventory.xMin), "The ship preview stays left of the inventory.");
            Assert.That(inventory.xMax, Is.LessThan(current.xMin), "Inventory and comparison occupy separate columns.");
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                Rect installed = ShipyardRectIn(detail, "Shipyard Equipment Slot " + slot);
                Assert.That(installed.height, Is.GreaterThanOrEqualTo(52f), "Installed fittings retain enough room for the icon and component name.");
                Assert.That(installed.Overlaps(vessel), Is.False, "Fittings surround the ship without covering it.");
                Assert.That(installed.xMax, Is.LessThan(inventory.xMin), "Fittings stay in the ship preview area.");
            }
            Button[] componentRows = detail.GetComponentsInChildren<Button>().Where(button =>
                button.name.StartsWith("Shipyard Equipment Slot ") || button.name.StartsWith("Shipyard Equipment Owned ")).ToArray();
            foreach (Button row in componentRows)
            {
                Text[] labels = row.GetComponentsInChildren<Text>().Where(label => !string.IsNullOrWhiteSpace(label.text)).ToArray();
                Assert.That(labels.Length, Is.EqualTo(1), row.name + " displays only the component name.");
                Assert.That(row.GetComponent<ComponentRarityWoodSkin>(), Is.Not.Null,
                    row.name + " uses the same rarity board as the component shop.");
            }
            Button unload = detail.GetComponentsInChildren<Button>().Single(button => button.name.StartsWith("Shipyard Unequip Component "));
            Rect unloadBounds = ShipyardRectIn(detail, unload.name);
            Assert.That(unloadBounds.yMax, Is.LessThan(current.yMin));
            Assert.That(unloadBounds.xMin, Is.EqualTo(current.xMin).Within(.5f));
            Assert.That(unloadBounds.xMax, Is.EqualTo(current.xMax).Within(.5f));
            Assert.That(equip.xMin, Is.EqualTo(selected.xMin).Within(.5f));
            Assert.That(equip.xMax, Is.EqualTo(selected.xMax).Within(.5f));
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
                        Assert.That(headerReturn.GetComponentInChildren<Text>(true).text, Is.EqualTo("返回船只档案"));
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
                        else yield return ClickShipyardControl("Return To Port Hub");
                        Assert.That(GameObject.Find("Shipyard Equipment Overlay"), Is.Null);
                        Assert.That(GameObject.Find("Shipyard Detail"), Is.Not.Null);
                        Assert.That(GameObject.Find("Shipyard Dossier Story") != null, Is.EqualTo(openingPage == "Background"));
                        Assert.That(GameObject.Find("Shipyard Dossier Stat 0") != null, Is.EqualTo(openingPage == "Performance"));
                        Assert.That(ActiveButton("Return To Port Hub").GetComponentInChildren<Text>(true).text, Is.EqualTo("返回船坞"));
                        yield return ClickShipyardControl("Port Facility Settings");
                        AssertShipyardSettingsAboveCurrentPage(retainedSettings);
                        yield return ClickShipyardControl("Close Port Settings");
                    }
                }
                yield return ClickShipyardControl("Return To Port Hub");
                Assert.That(GameObject.Find("Shipyard Detail"), Is.Null, "A second return leaves the dossier for the ship gallery.");
                Assert.That(ActiveButton("Shipyard Ship 0"), Is.Not.Null);
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
