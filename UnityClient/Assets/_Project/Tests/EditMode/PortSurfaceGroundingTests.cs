using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class PortSurfaceGroundingTests
    {
        private GameObject host;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        private Transform Preview(CoreGameplayPreviewScreen screen)
        {
            host = new GameObject("Port Surface Grounding Test");
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
            return host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static void AssertNoHoverGlow(Button button, Image glow)
        {
            Vector3 position = button.transform.localPosition;
            Vector3 scale = button.transform.localScale;
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            Assert.That(trigger, Is.Not.Null);
            trigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
            Assert.That(button.interactable, Is.True, "Removing the glow must not disable the grounded object's click target.");
            Assert.That(glow.color.a, Is.EqualTo(0f), "Grounded objects must not paint a pointer glow.");
            Assert.That(button.transform.localPosition, Is.EqualTo(position));
            Assert.That(button.transform.localScale, Is.EqualTo(scale), "Hover scaling would lift the object's feet off the floor.");
            trigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerExit).callback.Invoke(new BaseEventData(null));
            Assert.That(glow.color.a, Is.EqualTo(0f));
        }

        [Test]
        public void CardChestsStayOnRugDuringHover()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.CardWorkshop);
            for (int index = 0; index < 4; index++)
            {
                Button button = Find<Button>(preview, "Open Deck Chest " + index);
                Assert.That(button.GetComponent<RectTransform>().anchoredPosition.y, Is.LessThan(-100f));
                RawImage art = Find<RawImage>(preview, "Card Workshop Chest " + index);
                Image shadow = Find<Image>(preview, "Card Workshop Chest Contact " + index + " 0");
                Vector3 shadowPosition = shadow.transform.position;
                Assert.That(shadow.transform.parent.name, Is.EqualTo("Card Workshop Chest Stage"));
                Assert.That(shadow.raycastTarget, Is.False);
                Assert.That(art.rectTransform.rect.width / art.rectTransform.rect.height,
                    Is.EqualTo((float)art.texture.width / art.texture.height).Within(0.001f));
                for (int foot = 0; foot < 2; foot++)
                {
                    Vector2 uv = CardWorkshopChestPlacement.FootUv(index, foot == 1);
                    Rect rect = art.rectTransform.rect;
                    Vector3 paintedFoot = art.transform.TransformPoint(new Vector3(
                        Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y), 0f));
                    Image footShadow = Find<Image>(preview, "Card Workshop Chest Foot " + index + " " + foot);
                    Assert.That(Vector3.Distance(paintedFoot, footShadow.transform.position), Is.LessThan(0.01f),
                        "Painted feet, not transparent sprite bounds, must touch the floor shadow.");
                }
                Text label = Find<Text>(preview, "Card Workshop Chest Name " + index);
                Assert.That(label.transform.position.y, Is.LessThan(shadowPosition.y), "Names belong below the chest, not on its front.");
                AssertNoHoverGlow(button, Find<Image>(preview, "Card Workshop Chest Glow " + index));
                Assert.That(shadow.transform.position, Is.EqualTo(shadowPosition));
            }
        }

        [Test]
        public void PortAndTavernHotspotsKeepTheirClickTargetsWithoutPaintingGlow()
        {
            Transform hub = Preview(CoreGameplayPreviewScreen.PortHub);
            Button tavern = Find<Button>(hub, "Port Hotspot Tavern");
            Image tavernGlow = tavern.targetGraphic as Image;
            Assert.That(tavernGlow, Is.Not.Null);
            Assert.That(tavernGlow.color.a, Is.EqualTo(0f));
            Assert.That(tavern.interactable, Is.True);
            EventTrigger hubTrigger = tavern.GetComponent<EventTrigger>();
            hubTrigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
            Assert.That(tavernGlow.color.a, Is.EqualTo(0f));

            Object.DestroyImmediate(host);
            host = null;
            Transform tavernPage = Preview(CoreGameplayPreviewScreen.Tavern);
            for (int index = 0; index < 6; index++)
            {
                Button crew = Find<Button>(tavernPage, "Tavern Crew " + index);
                Image crewGlow = crew.targetGraphic as Image;
                Assert.That(crewGlow, Is.Not.Null);
                Assert.That(crewGlow.color.a, Is.EqualTo(0f));
                bool interactable = crew.interactable;
                EventTrigger trigger = crew.GetComponent<EventTrigger>();
                trigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
                Assert.That(crewGlow.color.a, Is.EqualTo(0f));
                Assert.That(crew.interactable, Is.EqualTo(interactable));
            }
        }

        [Test]
        public void ComponentShopOffersDoNotPaintPersistentRarityAura()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.ComponentShop);
            RawImage[] auras = preview.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("Component Shop Rarity Aura"))
                .ToArray();
            Assert.That(auras.Length, Is.GreaterThan(0));
            Assert.That(auras.All(image => image.color.a == 0f), Is.True,
                "Rarity should be shown by the plaque/text without a persistent coloured halo.");
        }

        [Test]
        public void CardChestsUseTheClearFloorAndForegroundDepth()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.CardWorkshop);
            for (int index = 0; index < 4; index++)
            {
                RectTransform contact = Find<Image>(preview, "Card Workshop Chest Contact " + index + " 0").rectTransform;
                Assert.That(Mathf.Abs(contact.anchoredPosition.x) + contact.rect.width * 0.5f, Is.LessThan(660f),
                    "Keep chests inside the rug, away from painted side shelves.");
                Assert.That(contact.anchoredPosition.y, Is.InRange(-300f, -190f));
            }
            RectTransform front = Find<RawImage>(preview, "Card Workshop Chest 0").rectTransform;
            RectTransform rear = Find<RawImage>(preview, "Card Workshop Chest 1").rectTransform;
            Assert.That(front.parent.GetSiblingIndex(), Is.GreaterThan(rear.parent.GetSiblingIndex()));
        }

        [Test]
        public void CardChestsHaveEqualPaintedWidthsAndSeparateLabelsAndHitTargets()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.CardWorkshop);
            float previousRight = float.NegativeInfinity;
            for (int index = 0; index < 4; index++)
            {
                RawImage art = Find<RawImage>(preview, "Card Workshop Chest " + index);
                Rect uv = CardWorkshopChestPlacement.PaintedBoundsUv(index);
                Rect rect = art.rectTransform.rect;
                Assert.That(uv.width * rect.width, Is.EqualTo(280f).Within(0.01f),
                    "All four entrances must use the same painted width, not the PNG canvas width.");
                Assert.That(rect.width / rect.height,
                    Is.EqualTo((float)art.texture.width / art.texture.height).Within(0.001f),
                    "Normalize size without stretching the chest illustration.");
                RectTransform button = Find<Button>(preview, "Open Deck Chest " + index).GetComponent<RectTransform>();
                Assert.That(button.anchoredPosition.x - button.rect.width * 0.5f, Is.GreaterThan(previousRight),
                    "Neighbouring invisible click rectangles must not overlap.");
                previousRight = button.anchoredPosition.x + button.rect.width * 0.5f;
                Vector3 bottomLeft = art.transform.TransformPoint(new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, uv.xMin), Mathf.Lerp(rect.yMin, rect.yMax, uv.yMin)));
                Vector3 topRight = art.transform.TransformPoint(new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, uv.xMax), Mathf.Lerp(rect.yMin, rect.yMax, uv.yMax)));
                Assert.That(button.rect.Contains(button.InverseTransformPoint(bottomLeft)), Is.True);
                Assert.That(button.rect.Contains(button.InverseTransformPoint(topRight)), Is.True,
                    "Enlarged painted art must remain clickable all the way to the lid.");
                RectTransform label = Find<Text>(preview, "Card Workshop Chest Name " + index).rectTransform;
                float labelTop = label.TransformPoint(new Vector2(0f, label.rect.yMax)).y;
                Assert.That(labelTop, Is.LessThan(bottomLeft.y), "Labels must remain below the painted feet.");
                Assert.That(Find<Image>(preview, "Card Workshop Chest Glow " + index).rectTransform.rect.width,
                    Is.EqualTo(280f).Within(0.01f), "Hover feedback should follow the painted size, not transparent padding.");
            }
        }

        [TestCase(0, "deck_chest_standard_v1")]
        [TestCase(1, "deck_chest_artillery_v1")]
        [TestCase(2, "deck_chest_navigation_v1")]
        [TestCase(3, "deck_chest_captain_v1")]
        public void CardChestPaintedBoundsMatchSourcePixels(int index, string assetName)
        {
            // Read the original PNG into a temporary texture; keep production imports
            // non-readable. Catch future art replacements that invalidate normalization.
            Texture2D source = new Texture2D(2, 2);
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath,
                    "_Project/Resources/CardWorkshopV2/" + assetName + ".png");
                Assert.That(source.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                Color32[] pixels = source.GetPixels32();
                int left = source.width, right = -1, bottom = source.height, top = -1;
                for (int y = 0; y < source.height; y++)
                    for (int x = 0; x < source.width; x++)
                    {
                        if (pixels[y * source.width + x].a < 128) continue;
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
                Assert.That(right, Is.GreaterThan(left));
                Rect uv = CardWorkshopChestPlacement.PaintedBoundsUv(index);
                Assert.That(uv.xMin * source.width, Is.EqualTo(left).Within(0.01f));
                Assert.That(uv.yMin * source.height, Is.EqualTo(bottom).Within(0.01f));
                Assert.That(uv.width * source.width, Is.EqualTo(right - left + 1).Within(0.01f));
                Assert.That(uv.height * source.height, Is.EqualTo(top - bottom + 1).Within(0.01f));
                Vector2 size = CardWorkshopChestPlacement.ArtSize(index, source.width / (float)source.height);
                Assert.That((right - left + 1f) / source.width * size.x, Is.EqualTo(280f).Within(0.01f));
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void DispatchUsesOneIntegratedRoomLayerAndStationaryPaper()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.Dispatch);
            RawImage room = Find<RawImage>(preview, "Dispatch Background");
            Assert.That(room.texture.name, Is.EqualTo("dispatch_room_integrated_v3"));
            Assert.That(room.color, Is.EqualTo(Color.white), "Preserve the unified room/table illumination.");
            Assert.That(preview.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name == "Dispatch Command Table" || item.name.StartsWith("Dispatch Table Foot Contact")), Is.False,
                "The table and shadows are painted into the room; a second furniture layer must not return.");
            for (int index = 0; index < 3; index++)
            {
                Button button = Find<Button>(preview, "Dispatch Mission Chart " + index);
                RawImage chart = Find<RawImage>(preview, "Dispatch Mission Chart Art " + index);
                Assert.That(chart.rectTransform.sizeDelta.y / chart.rectTransform.sizeDelta.x, Is.LessThan(0.6f));
                Assert.That(chart.GetComponent<TabletopQuadProjection>(), Is.Not.Null);
                RawImage contact = Find<RawImage>(preview, "Dispatch Chart Paper Contact " + index);
                Assert.That(contact.texture, Is.EqualTo(chart.texture), "The contact shadow must follow the paper silhouette.");
                Assert.That(contact.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0f, -2f)));
                Vector3 chartPosition = chart.transform.position;
                Quaternion chartRotation = chart.transform.rotation;
                AssertNoHoverGlow(button, Find<Image>(preview, "Dispatch Mission Chart Glow " + index));
                Assert.That(chart.transform.position, Is.EqualTo(chartPosition));
                Assert.That(chart.transform.rotation, Is.EqualTo(chartRotation));
            }
        }

        [Test]
        public void TreasureMapsStayOnSeparateFrontFacingBoardsDuringHover()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.TreasureMap);
            Assert.That(Find<RawImage>(preview, "Treasure Background").texture.name, Is.EqualTo("treasure_room_boards_v2"));
            Assert.That(Find<RawImage>(preview, "Treasure Background").color, Is.EqualTo(Color.white));
            Assert.That(preview.GetComponentsInChildren<SceneTabletopProjection>(true), Is.Empty,
                "Front-facing charts must not use the old steep tabletop projection.");
            RectTransform reading = Find<Image>(preview, "Treasure Route Reading Desk").rectTransform;
            Assert.That(reading.GetComponent<Image>().sprite, Is.EqualTo(WoodButtonSkin.Face));
            Assert.That(reading.anchoredPosition.y + PhaseSurfaceLayout.StagePositionY - reading.rect.height * 0.5f,
                Is.GreaterThanOrEqualTo(-524f), "Keep at least 16px below the route dossier at the screen edge.");
            float previousRight = float.NegativeInfinity;
            for (int index = 0; index < 3; index++)
            {
                TreasureChartDisplayLayout layout = TreasureChartDisplayLayout.For(index);
                RawImage chart = Find<RawImage>(preview, "Treasure Route Art " + index);
                Button button = Find<Button>(preview, "Treasure Route " + index);
                TabletopQuadProjection projection = chart.GetComponent<TabletopQuadProjection>();
                Assert.That(projection, Is.Not.Null);
                Assert.That(chart.rectTransform.sizeDelta.y, Is.GreaterThan(175f));
                Assert.That(chart.rectTransform.sizeDelta.x, Is.GreaterThan(310f));
                Assert.That(chart.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(button.interactable, Is.True);
                Assert.That(button.GetComponent<Image>().raycastTarget, Is.False, "Do not let an invisible rectangular hit target bridge the tilted edges.");
                Assert.That(button.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(layout.Position));
                Assert.That(layout.Position.x - layout.Size.x * 0.5f, Is.GreaterThan(previousRight + 80f));
                previousRight = layout.Position.x + layout.Size.x * 0.5f;
                Assert.That(projection.Project(Vector2.up).y, Is.EqualTo(projection.Project(Vector2.one).y).Within(0.001f));
                Assert.That(projection.Project(Vector2.up).x, Is.LessThan(0.07f));
                Assert.That(projection.Project(Vector2.one).x, Is.GreaterThan(0.93f), "Only the shallow painted face perspective is retained.");
                Assert.That(projection.Contains(new Vector2(0.5f, 0.5f)), Is.True);
                Image edge = Find<Image>(preview, "Treasure Chart Paper Edge " + index);
                foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                {
                    Vector2 point = projection.Project(corner);
                    Rect bounds = chart.rectTransform.rect;
                    Vector3 world = chart.transform.TransformPoint(new Vector3(Mathf.Lerp(bounds.xMin, bounds.xMax, point.x), Mathf.Lerp(bounds.yMin, bounds.yMax, point.y)));
                    Vector2 edgeLocal = edge.transform.InverseTransformPoint(world);
                    Vector2 edgeUv = (edgeLocal - edge.rectTransform.rect.min) / edge.rectTransform.rect.size;
                    Assert.That(edge.GetComponent<TabletopQuadProjection>().Contains(edgeUv), Is.True,
                        "The entire sea chart belongs inside the board's paper border.");
                }
                Text label = Find<Text>(preview, "Treasure Route Name " + index);
                Assert.That(label.resizeTextForBestFit, Is.True);
                Assert.That(label.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
                Assert.That(layout.LabelPosition.y + layout.LabelSize.y * 0.5f,
                    Is.LessThan(layout.Position.y - layout.Size.y * 0.5f), "The name rail must never cover the map.");
                Assert.That(reading.anchoredPosition.y + reading.rect.height * 0.5f,
                    Is.LessThan(TreasureChartDisplayLayout.PixelToPanel(new Vector2(832f, 714f)).y - 40f),
                    "Keep 40px below the stand feet as well as the 16px screen-bottom margin checked above.");
                Vector3 paperPosition = chart.transform.position;
                AssertNoHoverGlow(button, Find<Image>(preview, "Treasure Route Hover " + index));
                Assert.That(chart.transform.position, Is.EqualTo(paperPosition));
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void TreasureRoomThreeChartsAreAvailableInEveryPort(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Treasure Room Regional Test");
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TreasureMap, port, 2);
            Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Assert.That(Find<RawImage>(preview, "Treasure Background").texture.name, Is.EqualTo("treasure_room_boards_v2"));
            RawImage[] maps = preview.GetComponentsInChildren<RawImage>(true).Where(item => item.name.StartsWith("Treasure Route Art ")).ToArray();
            Assert.That(maps.Length, Is.EqualTo(3));
            Assert.That(maps.Select(item => item.texture).Distinct().Count(), Is.EqualTo(3));
            Assert.That(Find<Text>(preview, "Treasure Route Name 2").text, Does.Contain("已选"));
            Assert.That(Find<Text>(preview, "Treasure Route Name 0").text, Does.Not.Contain("已选"));
            Assert.That(Find<Text>(preview, "Treasure Route Name 1").text, Does.Not.Contain("已选"));
        }

        [Test]
        public void ShipyardHasOneInteractiveAfloatVesselAndSeparateBrowserControls()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.Shipyard);
            Find<Button>(preview, "Shipyard Ship 0");
            Assert.That(preview.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Shipyard Ship ")), Is.EqualTo(1));
            Find<Button>(preview, "Shipyard Previous Vessel");
            Find<Button>(preview, "Shipyard Next Vessel");
            Find<RawImage>(preview, "Shipyard Modular Ship Base");
            Find<BattleShipWaterGraphic>(preview, "Shipyard Water Contact");
            Assert.That(Find<RawImage>(preview, "Shipyard Background").texture.name, Is.EqualTo("shipyard_water_gallery_v3"));
            AssertNoHoverGlow(Find<Button>(preview, "Shipyard Ship 0"), Find<Image>(preview, "Shipyard Berth Glow 0"));
        }

        [Test]
        public void WarehouseSeparatesExhibitsBasicCargoAndRareCargoTabs()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.Warehouse);
            Find<Button>(preview, "Warehouse Tab Valuables");
            Assert.That(preview.GetComponentsInChildren<Button>(true).Any(button => button.name == "Warehouse Tab Basics"), Is.False);
            Find<Button>(preview, "Warehouse Tab Storage").onClick.Invoke();
            preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Find<Button>(preview, "Warehouse Storage Row " + PortMarketCatalog.Key(item));
            Assert.That(preview.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Warehouse Tab ")), Is.EqualTo(3));
            Find<Button>(preview, "Warehouse Tab Rare").onClick.Invoke();
            preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.RareItems)
                Find<Button>(preview, "Warehouse Rare Row " + item.Key);
            Assert.That(preview.GetComponentsInChildren<Button>(true).Any(button => button.name.StartsWith("Warehouse Storage Row ")), Is.False);
            Find<Button>(preview, "Warehouse Tab Valuables").onClick.Invoke();
            preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Find<Button>(preview, "Warehouse Display Selected");
            Assert.That(preview.GetComponentsInChildren<Button>(true).Any(button => button.name.StartsWith("Warehouse Storage Row ")), Is.False);
        }
    }
}
