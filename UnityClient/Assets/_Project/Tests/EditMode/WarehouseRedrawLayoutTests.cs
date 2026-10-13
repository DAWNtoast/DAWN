using System;
using System.Linq;
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
                    Assert.That(ledger.GetComponent<Image>().color.a, Is.Zero, "The painted cabinet supplies its own frame.");
                    Assert.That(ledger.GetComponent<Image>().sprite, Is.Null, "No parchment is attached to the ledger.");
                    Assert.That(host.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Warehouse Display Slot ", StringComparison.Ordinal)), Is.EqualTo(6));
                    string prefix = page == 1 ? "Warehouse Storage Row " : "Warehouse Rare Row ";
                    if (page > 0)
                    {
                        Button[] rows = host.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                        Assert.That(rows.Length, Is.EqualTo(page == 1 ? 9 : 6));
                        Rect[] rowBounds = rows.Select(b => RelativeBounds(b.GetComponent<RectTransform>(), ledger)).OrderByDescending(r => r.y).ToArray();
                        foreach (Button row in rows) AssertContained(ledger, row.GetComponent<RectTransform>());
                        foreach (Rect row in rowBounds) Assert.That(row.Overlaps(bounds), Is.False);
                        for (int row = 1; row < rowBounds.Length; row++) Assert.That(rowBounds[row - 1].Overlaps(rowBounds[row]), Is.False, "Inventory rows overlap.");
                        var capacity = Find<Text>(host, "Warehouse Cargo Capacity");
                        Assert.That(capacity.text, Does.Contain(" / "));
                        AssertContained(summary, capacity.rectTransform);
                    }
                    else AssertContained(summary, Find<Text>(host, "Warehouse Collection Count").rectTransform);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }
        private static T Find<T>(GameObject host, string name) where T : Component =>
            host.GetComponentsInChildren<T>(true).Single(item => item.name == name && item.gameObject.activeInHierarchy);
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
