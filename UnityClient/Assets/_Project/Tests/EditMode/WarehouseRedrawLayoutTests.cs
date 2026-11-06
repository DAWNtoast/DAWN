using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WarehouseRedrawLayoutTests
    {
        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void RegionalRoom_AllThreeTabsShareOneAlignedSummaryAndRetainInventory(CoreGameplayPreviewPort port)
        {
            var host = new GameObject("Warehouse Regional Layout Test");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Warehouse, port, 0);
                host.SetActive(true);
                Rect? sharedBounds = null;
                Vector2? sharedCellSize = null;
                Texture room = null;
                for (int page = 0; page < 3; page++)
                {
                    if (page == 1) controller.ConfigureEditorPreviewWarehouseStorage(0);
                    if (page == 2) controller.ConfigureEditorPreviewWarehouseRare(0);
                    Canvas.ForceUpdateCanvases();
                    var background = Find<RawImage>(host, "Warehouse Background");
                    Assert.That(background.texture, Is.Not.Null);
                    Assert.That(background.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)), "The composed room must not be PhaseSurface-cropped.");
                    Assert.That(background.rectTransform.rect.size, Is.EqualTo(new Vector2(1920, 1080)));
                    Assert.That(background.color, Is.EqualTo(Color.white));
                    if (room == null) room = background.texture;
                    Assert.That(background.texture, Is.SameAs(room), "Tab changes retain the port room.");
                    Assert.That(Find<RawImage>(host, "Warehouse Cabinet Art").texture, Is.Null, "Do not draw a second room over regional art.");
                    var ledger = Find<RectTransform>(host, "Warehouse Ledger");
                    var summary = Find<RectTransform>(host, "Warehouse Summary Bar");
                    Rect bounds = RelativeBounds(summary, ledger);
                    if (!sharedBounds.HasValue) sharedBounds = bounds;
                    Assert.That(bounds, Is.EqualTo(sharedBounds.Value), "All tabs must have identical information bar position, width and height.");
                    AssertContained(ledger, summary);
                    foreach (string name in new[] { "Warehouse Tab Valuables", "Warehouse Tab Storage", "Warehouse Tab Rare" })
                    {
                        var tab = Find<RectTransform>(host, name);
                        AssertContained(ledger, tab);
                        Assert.That(RelativeBounds(tab, ledger).Overlaps(bounds), Is.False, "Tabs must not cover the information bar.");
                    }
                    Assert.That(ledger.GetComponent<Image>().color.a, Is.EqualTo(1f), "The inventory ledger must remain readable over every port background.");
                    Assert.That(ledger.GetComponent<Image>().sprite, Is.SameAs(MarketShopSkin.Paper), "The collection ledger shares the market's paper and timber frame.");
                    Assert.That(host.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Warehouse Display Slot ", StringComparison.Ordinal)), Is.EqualTo(6));
                    string prefix = page == 1 ? "Warehouse Storage Row " : "Warehouse Rare Row ";
                    if (page > 0)
                    {
                        Assert.That(host.GetComponentsInChildren<Button>().Any(button =>
                            button.name.StartsWith("Warehouse Storage Header ", StringComparison.Ordinal) ||
                            button.name.StartsWith("Warehouse Rare Header ", StringComparison.Ordinal) ||
                            button.name == "Warehouse Store All Rare"), Is.False,
                            "Inventory space must not contain the removed sorting or deposit-all controls.");
                        Button[] cells = host.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                        int expectedRows = page == 1 ? 3 : 2;
                        Assert.That(cells.Length, Is.EqualTo(expectedRows * 3));
                        Rect[] cellBounds = cells.Select(b => RelativeBounds(b.GetComponent<RectTransform>(), ledger))
                            .OrderByDescending(r => Mathf.Round(r.y * 10f)).ThenBy(r => r.x).ToArray();
                        if (!sharedCellSize.HasValue) sharedCellSize = cellBounds[0].size;
                        Assert.That(cellBounds[0].width, Is.EqualTo(sharedCellSize.Value.x).Within(.1f),
                            "Basic and rare inventory cells must have the same width.");
                        Assert.That(cellBounds[0].height, Is.EqualTo(sharedCellSize.Value.y).Within(.1f),
                            "Basic and rare inventory cells must have the same height.");
                        for (int cell = 0; cell < cellBounds.Length; cell++)
                        {
                            Assert.That(cellBounds[cell].Overlaps(bounds), Is.False, "Inventory cells must stay below the summary.");
                            Assert.That(cellBounds[cell].x, Is.EqualTo(cellBounds[cell % 3].x).Within(.1f), "Every row must share three aligned columns.");
                            Assert.That(cellBounds[cell].y, Is.EqualTo(cellBounds[cell / 3 * 3].y).Within(.1f), "Every column must share the same row heights.");
                            Assert.That(cellBounds[cell].width, Is.EqualTo(cellBounds[0].width).Within(.1f), "Inventory cells must have a consistent width.");
                            Assert.That(cellBounds[cell].height, Is.EqualTo(cellBounds[0].height).Within(.1f), "Inventory cells must have a consistent height.");
                            for (int other = cell + 1; other < cellBounds.Length; other++)
                                Assert.That(cellBounds[cell].Overlaps(cellBounds[other]), Is.False, "Inventory cells overlap.");
                        }
                        Assert.That(cellBounds[0].x, Is.LessThan(cellBounds[1].x));
                        Assert.That(cellBounds[1].x, Is.LessThan(cellBounds[2].x));
                        for (int row = 1; row < expectedRows; row++)
                            Assert.That(cellBounds[(row - 1) * 3].y, Is.GreaterThan(cellBounds[row * 3].y));
                        foreach (Button cell in cells)
                        {
                            var cellRect = cell.GetComponent<RectTransform>();
                            AssertContained(ledger, cellRect);
                            string key = cell.name.Substring(prefix.Length);
                            string labelPrefix = page == 1 ? "Warehouse Storage " : "Warehouse Rare ";
                            Text quantity = Find<Text>(host, labelPrefix + "Quantity " + key);
                            Rect quantityBounds = RelativeBounds(quantity.rectTransform, cellRect);
                            Assert.That(quantityBounds.center.x, Is.GreaterThan(cellRect.rect.center.x), "The selected inventory quantity belongs in the lower-right corner.");
                            Assert.That(quantityBounds.yMax, Is.LessThanOrEqualTo(cellRect.rect.center.y), "The quantity must stay below the item art.");
                            Assert.That(quantity.alignment, Is.EqualTo(TextAnchor.MiddleRight));
                            Text[] visibleLabels = cell.GetComponentsInChildren<Text>().Where(label => !string.IsNullOrEmpty(label.text)).ToArray();
                            CollectionAssert.AreEquivalent(new[] { labelPrefix + "Name " + key, labelPrefix + "Quantity " + key },
                                visibleLabels.Select(label => label.name), "A cell contains only its item name and one quantity.");
                            foreach (Text label in cell.GetComponentsInChildren<Text>().Where(label => !string.IsNullOrEmpty(label.text)))
                                AssertContained(cellRect, label.rectTransform);
                            AssertMaximumInventoryFits(quantity);
                        }
                        Button storedLocation = Find<Button>(host, "Warehouse Inventory Location Stored");
                        Button carriedLocation = Find<Button>(host, "Warehouse Inventory Location Carried");
                        Assert.That(storedLocation.GetComponentInChildren<Text>().text, Is.EqualTo("本港仓库"));
                        Assert.That(carriedLocation.GetComponentInChildren<Text>().text, Is.EqualTo("船舱"));
                        AssertContained(summary, storedLocation.GetComponent<RectTransform>());
                        AssertContained(summary, carriedLocation.GetComponent<RectTransform>());
                        Rect storedLocationBounds = RelativeBounds(storedLocation.GetComponent<RectTransform>(), summary);
                        Rect carriedLocationBounds = RelativeBounds(carriedLocation.GetComponent<RectTransform>(), summary);
                        Assert.That(storedLocationBounds.center.x, Is.LessThan(summary.rect.center.x));
                        Assert.That(carriedLocationBounds.center.x, Is.GreaterThan(summary.rect.center.x));
                        Assert.That(storedLocationBounds.Overlaps(carriedLocationBounds), Is.False);
                        Assert.That(host.GetComponentsInChildren<Transform>().Any(item =>
                            item.name.StartsWith("Warehouse Cargo Capacity", StringComparison.Ordinal) ||
                            item.name == "Warehouse Storage Summary" || item.name == "Warehouse Rare Summary"), Is.False,
                            "Location buttons replace the former stock heading and capacity display.");
                        AssertLocationQuantities(controller, host, prefix, false);
                        AssertLocationQuantities(controller, host, prefix, true);
                        AssertLocationQuantities(controller, host, prefix, false);
                    }
                    else AssertContained(summary, Find<Text>(host, "Warehouse Collection Count").rectTransform);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }
        private static T Find<T>(GameObject host, string name) where T : Component =>
            host.GetComponentsInChildren<T>(true).Single(item => item.name == name && item.gameObject.activeInHierarchy);
        private static void AssertLocationQuantities(CoreGameplayDemoController controller, GameObject host, string prefix, bool carried)
        {
            typeof(CoreGameplayDemoController).GetField("warehouseViewingCarriedCargo", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, carried);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
            string labelPrefix = prefix == "Warehouse Storage Row " ? "Warehouse Storage " : "Warehouse Rare ";
            foreach (Button cell in host.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(prefix, StringComparison.Ordinal)))
            {
                string key = cell.name.Substring(prefix.Length);
                int expected = carried ? controller.Session.GetWarehouseCargoAmount(key) :
                    controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, key);
                Text quantity = Find<Text>(host, labelPrefix + "Quantity " + key);
                Assert.That(quantity.text, Is.EqualTo(expected.ToString(CultureInfo.InvariantCulture)),
                    "Every cell must display only the selected inventory's quantity.");
                AssertMaximumInventoryFits(quantity);
                CollectionAssert.AreEquivalent(new[] { labelPrefix + "Name " + key, labelPrefix + "Quantity " + key },
                    cell.GetComponentsInChildren<Text>().Where(label => !string.IsNullOrEmpty(label.text)).Select(label => label.name),
                    "Switching locations must not leave duplicate quantities or old labels visible.");
            }
        }
        private static void AssertMaximumInventoryFits(Text label)
        {
            string original = label.text;
            try
            {
                label.text = int.MaxValue.ToString(CultureInfo.InvariantCulture);
                Rect bounds = label.rectTransform.rect;
                TextGenerationSettings settings = label.GetGenerationSettings(bounds.size);
                using (var generator = new TextGenerator())
                {
                    Assert.That(generator.Populate(label.text, settings), Is.True, label.name);
                    Assert.That(generator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length),
                        label.name + " must show every digit at the supported inventory limit.");
                    Assert.That(generator.lineCount, Is.EqualTo(1), label.name + " must keep its quantity on one line.");
                    Assert.That(generator.vertexCount, Is.GreaterThanOrEqualTo(label.text.Length * 4),
                        label.name + " must retain the complete digit mesh.");
                    RectTransform cell = (RectTransform)label.transform.parent;
                    float scale = Mathf.Max(.01f, label.pixelsPerUnit);
                    float rasterTolerance = 1f / scale;
                    // TextGenerator can add an empty trailing quad. Inspect the ten
                    // actual digit quads, including their glyph bearings, in cell
                    // coordinates: the item border is the visible clipping limit.
                    for (int vertexIndex = 0; vertexIndex < label.text.Length * 4; vertexIndex++)
                    {
                        Vector3 local = generator.verts[vertexIndex].position / scale;
                        Vector3 point = cell.InverseTransformPoint(label.rectTransform.TransformPoint(local));
                        Assert.That(point.x, Is.InRange(cell.rect.xMin - rasterTolerance, cell.rect.xMax + rasterTolerance),
                            label.name + " must keep every painted digit inside the item cell at the inventory limit.");
                        Assert.That(point.y, Is.InRange(cell.rect.yMin - rasterTolerance, cell.rect.yMax + rasterTolerance),
                            label.name + " must keep every painted digit above the item cell's bottom edge.");
                    }
                }
            }
            finally { label.text = original; }
        }
        private static Rect RelativeBounds(RectTransform child, RectTransform parent)
        {
            var points = new Vector3[4]; child.GetWorldCorners(points);
            Vector3 a = parent.InverseTransformPoint(points[0]), b = parent.InverseTransformPoint(points[2]);
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
        }
        private static void AssertContained(RectTransform parent, RectTransform child)
        {
            Rect bounds = RelativeBounds(child, parent);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - .1f), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + .1f), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - .1f), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + .1f), child.name);
        }
    }
}
