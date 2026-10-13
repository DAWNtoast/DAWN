using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DepartureBasicSuppliesLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void BasicSuppliesModalCentersNineItemsAndKeepsPurchaseOnlyControlsReadable(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Departure Basic Supplies Layout Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
            host.SetActive(true);
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            MethodInfo open = typeof(CoreGameplayDemoController).GetMethod("OpenDepartureBasicSupplies",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(open, Is.Not.Null);
            open.Invoke(controller, new object[] { setup });
            Canvas.ForceUpdateCanvases();
            RectTransform modal = Find<RectTransform>("Departure Basic Supplies");
            Assert.That(modal.rect.width, Is.EqualTo(1080f).Within(.1f));
            Assert.That(modal.rect.height, Is.EqualTo(660f).Within(.1f));
            Assert.That(modal.anchoredPosition, Is.EqualTo(Vector2.zero));
            Image overlay = Find<Image>("Departure Basic Supplies Overlay");
            Assert.That(overlay.raycastTarget, Is.True, "The modal background blocks the departure controls below it.");
            RawImage[] icons = modal.GetComponentsInChildren<RawImage>()
                .Where(item => item.name.StartsWith("Departure Basic Supply Icon ")).ToArray();
            Assert.That(icons.Length, Is.EqualTo(9));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                RawImage icon = Find<RawImage>("Departure Basic Supply Icon " + key);
                ItemIconArt expected = ItemIconCatalog.ForCargo(key);
                Assert.That(icon.texture, Is.SameAs(expected.Texture), key);
                Assert.That(icon.uvRect, Is.EqualTo(expected.Uv), key);
                Assert.That(icon.raycastTarget, Is.False, key);
                Rect art = Bounds(modal, icon.rectTransform);
                Text held = Find<Text>("Departure Basic Supply Held " + key);
                Rect heldBounds = Bounds(modal, held.rectTransform);
                Assert.That(heldBounds.xMin, Is.GreaterThanOrEqualTo(art.xMax), key + " held quantity belongs beside its icon");
                Rect minus = Bounds(modal, Find<Button>("Departure Basic Supply Decrease " + key).GetComponent<RectTransform>());
                Rect quantity = Bounds(modal, Find<Text>("Departure Basic Supply Quantity " + key).rectTransform);
                Rect plus = Bounds(modal, Find<Button>("Departure Basic Supply Increase " + key).GetComponent<RectTransform>());
                Assert.That(minus.xMax, Is.LessThanOrEqualTo(quantity.xMin), key + " minus / quantity");
                Assert.That(quantity.xMax, Is.LessThanOrEqualTo(plus.xMin), key + " quantity / plus");
                Assert.That(minus.center.y, Is.EqualTo(plus.center.y).Within(.1f), key);
                foreach (Rect bounds in new[] { art, heldBounds, minus, quantity, plus }) AssertInside(modal, bounds, key);
                Text endurance = controller.GetComponentsInChildren<Text>(true)
                    .SingleOrDefault(text => text.name == "Departure Basic Supply Endurance " + key);
                Assert.That(endurance != null, Is.EqualTo(item == PortMarketItem.Water || item == PortMarketItem.Food), key);
                if (endurance != null)
                {
                    Assert.That(endurance.text, Does.Contain("天"), key);
                    Rect enduranceBounds = Bounds(modal, endurance.rectTransform);
                    AssertInside(modal, enduranceBounds, key);
                    Assert.That(enduranceBounds.Overlaps(art), Is.False, key + " endurance / icon");
                }
                Assert.That(held.preferredHeight, Is.LessThanOrEqualTo(held.rectTransform.rect.height + 1f), key);
            }
            Button recommend = Find<Button>("Departure Basic Supplies Recommend");
            Assert.That(recommend.GetComponentInChildren<Text>().text, Is.EqualTo("设定为推荐数量"));
            Rect recommendation = Bounds(modal, recommend.GetComponent<RectTransform>());
            Assert.That(recommendation.center.x, Is.LessThan(0f), "Recommendation remains in the lower left.");
            Assert.That(recommendation.center.y, Is.LessThan(0f));
            string[] footerNames = { "Departure Basic Supplies Recommend", "Departure Basic Supplies Purchase", "Departure Basic Supplies Close" };
            Rect[] footer = footerNames.Select(name => Bounds(modal, Find<Button>(name).GetComponent<RectTransform>())).ToArray();
            for (int index = 0; index < footer.Length; index++)
            {
                AssertInside(modal, footer[index], footerNames[index]);
                Text caption = Find<Button>(footerNames[index]).GetComponentInChildren<Text>();
                Assert.That(caption.preferredHeight, Is.LessThanOrEqualTo(caption.rectTransform.rect.height + 1f), footerNames[index]);
                for (int other = index + 1; other < footer.Length; other++)
                    Assert.That(footer[index].Overlaps(footer[other]), Is.False, footerNames[index] + " / " + footerNames[other]);
            }
            Assert.That(Find<Button>("Departure Basic Supplies Purchase").interactable, Is.False,
                "Opening a safe art preview never authorizes a persisted purchase.");
            Assert.That(modal.GetComponentsInChildren<Button>().Any(button =>
                button.name.Contains("Sell") || button.GetComponentInChildren<Text>()?.text.Contains("卖出") == true), Is.False);
        }

        private T Find<T>(string name) where T : Component =>
            controller.GetComponentsInChildren<T>(true).Single(item => item.name == name);

        private static Rect Bounds(Transform parent, RectTransform item)
        {
            Vector3[] corners = new Vector3[4]; item.GetWorldCorners(corners);
            Vector2 minimum = parent.InverseTransformPoint(corners[0]);
            Vector2 maximum = minimum;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = parent.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static void AssertInside(RectTransform modal, Rect bounds, string name)
        {
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(modal.rect.xMin - .1f), name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(modal.rect.xMax + .1f), name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(modal.rect.yMin - .1f), name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(modal.rect.yMax + .1f), name);
        }
    }
}
