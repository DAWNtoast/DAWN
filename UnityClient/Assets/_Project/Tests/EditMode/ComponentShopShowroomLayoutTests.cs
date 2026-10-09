using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentShopShowroomLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        private static IEnumerable<TestCaseData> StockCases()
        {
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                foreach (int count in new[] { 1, 2, 3, 4, 5, 6, 16 })
                    yield return new TestCaseData(count, slot).SetName("ShopFloor_" + slot + "_Stock" + count);
        }

        [TestCaseSource(nameof(StockCases))]
        public void EveryStockCount_KeepsCompleteDisplaysOnCentralFloorAndAllOffersReachable(int count, ComponentSlot slot)
        {
            CreatePreview();
            List<PortComponentShopStockItem> stock = CreateStock(count, slot);
            HashSet<string> seen = new HashSet<string>();
            int pageCount = (count + 2) / 3;
            for (int page = 0; page < pageCount; page++)
            {
                RectTransform panel = BuildStock(stock, page);
                Button[] offers = panel.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal)).ToArray();
                Assert.That(offers.Length, Is.EqualTo(Math.Min(3, count - page * 3)), "Show at most three physical displays per page.");

                AssertNoMerchantOrSupports(panel);
                List<Rect> displayBounds = new List<Rect>();
                foreach (Button offer in offers)
                {
                    PortComponentShopStockItem item = stock.Single(candidate => offer.name == "Component Shop Offer " + candidate.Id);
                    Assert.That(seen.Add(item.Id), Is.True, "An offer must occur exactly once across the showroom pages.");
                    RawImage model = Require<RawImage>(offer.transform, "Component Shop Model " + item.Entry.Id);
                    RawImage plaque = Require<RawImage>(offer.transform, "Component Shop Plaque " + item.Entry.Id);
                    Rect modelBounds = BoundsIn(panel, model.rectTransform);
                    Rect plaqueBounds = BoundsIn(panel, plaque.rectTransform);
                    Assert.That(modelBounds.Overlaps(plaqueBounds), Is.False, "The sign must never cover the component.");
                    Rect bounds = Union(modelBounds,
                        BoundsIn(panel, plaque.rectTransform));
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-530f), item.Entry.Id + ": the left wall and foreground cargo are outside the display floor.");
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(530f), item.Entry.Id + ": preserve the right wall and keep all displays central.");
                    foreach (Rect previous in displayBounds)
                        Assert.That(bounds.Overlaps(previous), Is.False, item.Entry.Id + ": complete components and plaques must not interpenetrate another display.");
                    displayBounds.Add(bounds);

                    RectTransform assembly = Require<RectTransform>(offer.transform, "Component Shop Display Assembly " + item.Entry.Id);
                    ComponentShopDisplayGeometry geometry = ComponentShopDisplayGeometry.For(item.Entry, controller.Session.CurrentPort);
                    AssertFloorPoint(panel.InverseTransformPoint(assembly.TransformPoint(
                        new Vector2(geometry.PlaquePosition.x, ComponentShopDisplayGeometry.PlaqueFloorY))), item.Entry.Id + " plaque foot");
                    Assert.That(assembly.localScale.x, Is.EqualTo(assembly.localScale.y).Within(0.0001f),
                        "The component and its sign must retain a shared uniform scale.");

                    foreach (RawImage target in new[] { model, plaque })
                    {
                        Assert.That(target.raycastTarget, Is.True, target.name);
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target.gameObject), Is.SameAs(offer.gameObject));
                    }
                }
            }
            CollectionAssert.AreEquivalent(stock.Select(item => item.Id), seen, "Pagination must retain every monthly offer.");
        }

        [TestCase(4)]
        [TestCase(5)]
        public void PageControls_AdvanceAndReturnWithoutBuyingAnOffer(int count)
        {
            CreatePreview();
            List<PortComponentShopStockItem> stock = CreateStock(count, ComponentSlot.Bow);
            BuildStock(stock, 0);
            decimal gold = controller.Session.ProfileGold;
            Button previous = Require<Button>(host.transform, "Component Shop Previous Display");
            Button next = Require<Button>(host.transform, "Component Shop Next Display");
            Assert.That(previous.interactable, Is.False);
            Assert.That(next.interactable, Is.True);
            next.onClick.Invoke();
            Assert.That((int)PageField().GetValue(controller), Is.EqualTo(1));
            CollectionAssert.AreEqual(stock.Skip(3).Select(item => "Component Shop Offer " + item.Id),
                VisibleOfferNames(), "The callback must preserve this showroom's stock and show its remaining offers.");
            Assert.That(Require<Button>(host.transform, "Component Shop Previous Display").interactable, Is.True);
            Assert.That(Require<Button>(host.transform, "Component Shop Next Display").interactable, Is.False);
            Require<Button>(host.transform, "Component Shop Previous Display").onClick.Invoke();
            Assert.That((int)PageField().GetValue(controller), Is.Zero);
            CollectionAssert.AreEqual(stock.Take(3).Select(item => "Component Shop Offer " + item.Id), VisibleOfferNames());
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (PortComponentShopStockItem item in stock)
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(item.Id), 0), Is.Zero);
        }

        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        public void ShopAndDetail_ContainNoMerchantOrAddedSupports(CoreGameplayPreviewPort port)
        {
            CreatePreview(port);
            AssertNoMerchantOrSupports(host.transform);
            Button offer = host.GetComponentsInChildren<Button>(true)
                .First(item => item.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal));
            decimal gold = controller.Session.ProfileGold;
            offer.onClick.Invoke();
            Assert.That(host.GetComponentsInChildren<RawImage>(true).Any(item => item.name == "Component Detail Model"), Is.True,
                "Clicking a part/sign must open its support-free detail sheet.");
            AssertNoMerchantOrSupports(host.transform);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Viewing a component must not buy it.");
        }

        private static void AssertNoMerchantOrSupports(Transform root)
        {
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name.StartsWith("Component Shop Merchant", StringComparison.Ordinal) ||
                item.name.StartsWith("Component Shop Mount", StringComparison.Ordinal) ||
                item.name.StartsWith("Component Detail Mount", StringComparison.Ordinal) ||
                item.name.Contains("Furniture Foot") || item.name.Contains("Load Contact") ||
                item.name.Contains("Unified Art Frame")), Is.False,
                "Remove merchant, racks, foreground lips, backing panels and orphaned support shadows.");

            foreach (RawImage aura in root.GetComponentsInChildren<RawImage>(true)
                .Where(item => item.name.IndexOf("Rarity Aura", StringComparison.Ordinal) >= 0))
                Assert.That(aura.color.a, Is.LessThanOrEqualTo(0.001f), aura.name);
        }

        [Test]
        public void ChangingPreviewPort_ResetsDisplayPageAndShowsTheNewPortsFirstOffers()
        {
            CreatePreview(CoreGameplayPreviewPort.GrayGull);
            PageField().SetValue(controller, 1);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.BrokenAnchor, 0);

            Assert.That(controller.Session.CurrentPort, Is.EqualTo("断锚港"));
            Assert.That((int)PageField().GetValue(controller), Is.Zero,
                "A new port preview must not inherit the previous port's display page.");
            IReadOnlyList<PortComponentShopStockItem> stock = PortComponentShopCatalog.BuildStock(
                new ComponentArtCatalog(), controller.Session.CurrentPort, controller.Session.GameDateTime);
            CollectionAssert.AreEqual(stock.Take(3).Select(item => "Component Shop Offer " + item.Id), VisibleOfferNames(),
                "The new port must open on its first group of at most three monthly offers.");
        }

        private void CreatePreview(CoreGameplayPreviewPort port = CoreGameplayPreviewPort.GrayGull)
        {
            host = new GameObject("Component Shop Showroom Layout Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, port, 0);
        }

        private List<PortComponentShopStockItem> CreateStock(int count, ComponentSlot slot)
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry[] entries = catalog.Entries.Where(item => item.Slot == slot).ToArray();
            return Enumerable.Range(0, count).Select(index =>
            {
                ComponentArtEntry entry = entries[index];
                return new PortComponentShopStockItem("layout-test|" + count + "|" + slot + "|" + index,
                    controller.Session.CurrentPort, PortComponentShopCatalog.MonthKey(controller.Session.GameDateTime), entry, 1f, 200);
            }).ToList();
        }

        private RectTransform BuildStock(IReadOnlyList<PortComponentShopStockItem> stock, int page)
        {
            PageField().SetValue(controller, page);
            RectTransform panel = Require<RectTransform>(host.transform, "Component Shop Panel");
            for (int index = panel.childCount - 1; index >= 0; index--) Object.DestroyImmediate(panel.GetChild(index).gameObject);
            MethodInfo build = typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopShowroom", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);
            build.Invoke(controller, new object[] { panel, stock, controller.Session.GameDateTime, 0 });
            Canvas.ForceUpdateCanvases();
            return panel;
        }

        private static FieldInfo PageField()
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField("componentShopDisplayPage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field;
        }

        private static void AssertFloorPoint(Vector3 point, string context)
        {
            Assert.That(point.x, Is.InRange(-500f, 560f), context + ": a load-bearing foot must avoid the side shelving.");
            Assert.That(point.y, Is.InRange(-365f, -110f), context + ": a load-bearing foot must be on the central floor band, including the signs' foreground feet.");
        }

        private string[] VisibleOfferNames()
        {
            return host.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal))
                .Select(button => button.name).ToArray();
        }

        private static T Require<T>(Transform root, string name) where T : Component
        {
            T item = root.GetComponentsInChildren<T>(true).FirstOrDefault(candidate => candidate.name == name);
            Assert.That(item, Is.Not.Null, name);
            return item;
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }
    }
}
