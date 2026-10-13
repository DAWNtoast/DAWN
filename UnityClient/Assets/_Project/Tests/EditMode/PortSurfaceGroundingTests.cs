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
        public void CardWorkshopOpensThePreviewWithoutChestOrRoomArt()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.CardWorkshop);
            Find<RectTransform>(preview, "Card Workshop Preview Panel");
            Find<RectTransform>(preview, "Workshop Preview Collection");
            RawImage background = Find<RawImage>(preview, "CardWorkshop Background");
            Assert.That(background.texture, Is.Null, "The workshop room illustration is no longer displayed.");
            Assert.That(preview.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name.StartsWith("Card Workshop Chest") || item.name.StartsWith("Open Deck Chest")), Is.False);
            Assert.That(Find<Button>(preview, "Open Battle Card Upgrades").interactable, Is.True);
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                Assert.That(Find<Button>(preview, "Edit Stage Deck " + stage).interactable, Is.True);
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
        public void WorkshopCollectionHasEightCardsWithCompleteUniformlyScaledFrames()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.CardWorkshop);
            Button[] choices = Find<RectTransform>(preview, "Workshop Preview Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
            Assert.That(choices, Has.Length.EqualTo(8));
            foreach (Button choice in choices)
            {
                CardSchoolFrameGraphic frame = choice.GetComponentInChildren<CardSchoolFrameGraphic>();
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.HasAuthoredTexture, Is.True);
                RectTransform face = frame.transform.parent.GetComponent<RectTransform>();
                Assert.That(face.localScale.x, Is.GreaterThan(0f));
                Assert.That(face.localScale.y, Is.EqualTo(face.localScale.x));
                Vector3[] corners = new Vector3[4];
                frame.rectTransform.GetWorldCorners(corners);
                RectTransform click = choice.GetComponent<RectTransform>();
                Rect hitBounds = Rect.MinMaxRect(click.rect.xMin - .5f, click.rect.yMin - .5f,
                    click.rect.xMax + .5f, click.rect.yMax + .5f);
                foreach (Vector3 corner in corners)
                    Assert.That(hitBounds.Contains(click.InverseTransformPoint(corner)), Is.True,
                        "The entire authored card, including its outer frame, remains clickable.");
            }
        }

        [Test]
        public void WorkshopUpgradeMergesBothRightSectionsAndCanReturnToEntrances()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.CardWorkshop);
            Find<Button>(preview, "Open Battle Card Upgrades").onClick.Invoke();
            preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Find<RectTransform>(preview, "Battle Card Upgrade Panel");
            Assert.That(Find<Text>(preview, "Card Upgrade Heading").text, Is.EqualTo("卡牌强化"));
            Assert.That(preview.GetComponentsInChildren<Button>(true).Any(button => button.name.StartsWith("Edit Stage Deck ")), Is.False);
            Find<Button>(preview, "Back From Card Upgrades").onClick.Invoke();
            preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Find<Button>(preview, "Open Battle Card Upgrades");
            Find<Button>(preview, "Edit Stage Deck Preparation");
            Assert.That(preview.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Card Upgrade Panel"), Is.False);
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
        public void TreasureMapsStayOnSeparateFrontFacingBoardsDuringHover()
        {
            Transform preview = Preview(CoreGameplayPreviewScreen.TreasureMap);
            Assert.That(Find<RawImage>(preview, "Treasure Background").texture.name, Is.EqualTo("treasure_worktable_background_v3"));
            Assert.That(Find<RawImage>(preview, "Treasure Background").color, Is.EqualTo(Color.white));
            Assert.That(preview.GetComponentsInChildren<SceneTabletopProjection>(true), Is.Empty,
                "Front-facing charts must not use the old steep tabletop projection.");
            foreach (string name in new[] { "Treasure Route Reading Desk", "Treasure Selected Route", "Treasure Route Detail" })
                Assert.That(preview.GetComponentsInChildren<Transform>(true).Any(item => item.name == name), Is.False,
                    "The lower reading desk and its information have been removed.");
            Text status = Find<Text>(preview, "Treasure Workbench Status");
            Assert.That(status.text, Does.Not.Contain("月蚀"));
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 1f));
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
                Assert.That(layout.Position.x - layout.Size.x * 0.5f, Is.GreaterThan(previousRight + 50f),
                    "The separate tabletop frames must retain a visible, non-interactive gap.");
                previousRight = layout.Position.x + layout.Size.x * 0.5f;
                Assert.That(projection.Project(Vector2.up).y, Is.EqualTo(projection.Project(Vector2.one).y).Within(0.001f));
                Assert.That(projection.Project(Vector2.up).x, Is.InRange(0f, 0.2f));
                Assert.That(projection.Project(Vector2.one).x, Is.InRange(0.8f, 1f), "Only the shallow painted face perspective is retained.");
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
                Text progress = Find<Text>(preview, "Treasure Route Progress " + index);
                Assert.That(label.resizeTextForBestFit, Is.True);
                Assert.That(label.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
                Assert.That(label.text, Is.EqualTo(CoreTreasureHuntCatalog.Ranks[index].Name + "藏宝图"));
                Assert.That(progress.text, Does.Contain("/4"));
                Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(layout.NamePosition - layout.Position));
                Assert.That(progress.rectTransform.anchoredPosition, Is.EqualTo(layout.LabelPosition - layout.Position));
                Assert.That(layout.NamePosition.y + layout.NameSize.y * 0.5f,
                    Is.LessThan(layout.Position.y - layout.Size.y * 0.5f), "The painted name plate must stay below the map.");
                Assert.That(layout.LabelPosition.y + layout.LabelSize.y * 0.5f,
                    Is.LessThan(layout.NamePosition.y - layout.NameSize.y * 0.5f), "Progress uses its own painted plate below the name.");
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
            Assert.That(Find<RawImage>(preview, "Treasure Background").texture.name, Is.EqualTo("treasure_worktable_background_v3"));
            RawImage[] maps = preview.GetComponentsInChildren<RawImage>(true).Where(item => item.name.StartsWith("Treasure Route Art ")).ToArray();
            Assert.That(maps.Length, Is.EqualTo(3));
            Assert.That(maps.Select(item => item.texture).Distinct().Count(), Is.EqualTo(3));
            Assert.That(Find<Text>(preview, "Treasure Route Name 2").text, Does.Contain("月蚀藏宝图"));
            Assert.That(Find<Text>(preview, "Treasure Route Name 0").text, Does.Contain("残旧藏宝图"));
            Assert.That(Find<Text>(preview, "Treasure Route Name 1").text, Does.Contain("鎏金藏宝图"));
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
