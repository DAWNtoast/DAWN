using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentShopPlaqueLayoutTests
    {
        private GameObject host;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }
        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void RightCatalogueAlignsWithCategoriesAndOpensWithoutSummaryOrDisplayFixtures(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Component Shop Vertical Catalogue Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, port, 0);
            Canvas.ForceUpdateCanvases();
            Assert.That(host.GetComponentsInChildren<Transform>().Any(item =>
                item.name.StartsWith("Component Shop Plaque ") || item.name.StartsWith("Component Shop Merchant") ||
                item.name.Contains("Component Shop Summary") || item.name.Contains("Parchment") ||
                item.name.Contains("Scale Support") || item.name.Contains("Contact Shadow")), Is.False);
            Assert.That(host.GetComponentsInChildren<Button>().Any(item => item.name.StartsWith("Purchase")), Is.False);
            Button[] categories = host.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Component Shop Category ")).ToArray();
            Assert.That(categories.Length, Is.EqualTo(5));
            Button[] offers = host.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Component Shop Offer ")).ToArray();
            Assert.That(offers.Length, Is.InRange(1, 5));
            for (int index = 0; index < offers.Length; index++)
            {
                Button offer = offers[index];
                RectTransform rect = offer.GetComponent<RectTransform>();
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(218f, 106f)));
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(800f, 305f - index * 120f)));
                Assert.That(categories[index].GetComponent<RectTransform>().anchoredPosition.y, Is.EqualTo(rect.anchoredPosition.y));
                Assert.That(offer.targetGraphic, Is.Not.Null); Assert.That(offer.targetGraphic.raycastTarget, Is.True);
                Assert.That(offer.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(1));
                Assert.That(offer.GetComponentsInChildren<Text>().Any(item => item.name.StartsWith("Component Shop Thumbnail Name ")), Is.True);
                foreach (Text text in offer.GetComponentsInChildren<Text>())
                {
                    Assert.That(text.raycastTarget, Is.False);
                    Assert.That(text.text, Does.Not.Contain("占用空间")); Assert.That(text.text, Does.Not.Contain("安装时间"));
                    var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 point = rect.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(-109.1f, 109.1f), text.name);
                        Assert.That(point.y, Is.InRange(-53.1f, 53.1f), text.name);
                    }
                }
            }
        }
    }
}
