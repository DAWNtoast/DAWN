using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiImageAspectGeometryTests
    {
        [TestCase(1536f, 1024f, 1f, 1f, 126f, 126f, 126f, 84f)]
        [TestCase(1536f, 1024f, 1f, 1f, 176f, 176f, 176f, 117.333333f)]
        [TestCase(1536f, 1024f, 1f, 1f, 500f, 300f, 450f, 300f)]
        [TestCase(1672f, 941f, 1f, 1f, 260f, 120f, 213.21998f, 120f)]
        [TestCase(1254f, 1254f, 0.5f, 0.45f, 320f, 240f, 266.666667f, 240f)]
        [TestCase(1254f, 1254f, -0.5f, 0.45f, 320f, 240f, 266.666667f, 240f)]
        [TestCase(1254f, 1254f, -0.5f, -0.45f, 320f, 240f, 266.666667f, 240f)]
        public void SampledPixelsFitInsideAuthoredFrameWithoutStretching(float sourceWidth, float sourceHeight,
            float uvWidth, float uvHeight, float boxWidth, float boxHeight, float expectedWidth, float expectedHeight)
        {
            Vector2 actual = UiImageAspect.FitSize(new Vector2(sourceWidth, sourceHeight),
                new Rect(0.3f, 0.4f, uvWidth, uvHeight), new Vector2(boxWidth, boxHeight));
            Assert.That(actual.x, Is.EqualTo(expectedWidth).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expectedHeight).Within(0.001f));
            Assert.That(actual.x, Is.LessThanOrEqualTo(boxWidth + 0.001f));
            Assert.That(actual.y, Is.LessThanOrEqualTo(boxHeight + 0.001f));
        }

        [Test]
        public void FitPreservesMirroredUvAndAuthoredPositionAndWorksWithStretchAnchors()
        {
            GameObject parent = new GameObject("Aspect parent", typeof(RectTransform));
            GameObject child = new GameObject("Aspect child", typeof(RectTransform), typeof(RawImage));
            Texture2D texture = new Texture2D(300, 200);
            try
            {
                parent.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);
                child.transform.SetParent(parent.transform, false);
                RawImage image = child.GetComponent<RawImage>();
                Rect uv = new Rect(0.9f, 0.1f, -0.5f, 0.5f);
                image.texture = texture;
                image.uvRect = uv;
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.anchoredPosition = new Vector2(17f, -23f);
                UiImageAspect.Fit(image, new Vector2(126f, 126f));
                UiImageAspect.Fit(image, new Vector2(126f, 126f));
                Assert.That(image.rectTransform.rect.width, Is.EqualTo(126f).Within(0.001f));
                Assert.That(image.rectTransform.rect.height, Is.EqualTo(84f).Within(0.001f));
                Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(17f, -23f)));
                Assert.That(image.uvRect, Is.EqualTo(uv));
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingOrDegenerateTextureKeepsFinitePlaceholderBounds()
        {
            Vector2 bounds = new Vector2(126f, 84f);
            Assert.That(UiImageAspect.FitSize((Texture)null, new Rect(0, 0, 1, 1), bounds), Is.EqualTo(bounds));
            Assert.That(UiImageAspect.FitSize(new Vector2(300f, 200f), new Rect(0, 0, 0, 1), bounds), Is.EqualTo(bounds));
            Assert.That(UiImageAspect.FitSize(new Vector2(300f, 200f), new Rect(0, 0, 1, 1),
                new Vector2(float.NaN, -1f)), Is.EqualTo(Vector2.zero));
        }

        [TestCase(1)]
        [TestCase(4)]
        public void ActiveShipFactoryFitsAtlasGuttersOnceAndPreservesDisplayFrame(int shipIndex)
        {
            GameObject host = new GameObject("Ship aspect factory", typeof(RectTransform));
            host.SetActive(false);
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                CoreVoyageSession session = new CoreVoyageSession();
                Assert.That(session.SelectShip(shipIndex), Is.True);
                PortArtCatalog catalog = new PortArtCatalog();
                SetField(controller, "session", session);
                SetField(controller, "portArt", catalog);
                RectTransform preview = (RectTransform)typeof(CoreGameplayDemoController)
                    .GetMethod("BuildActiveShipPreview", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { "Test Ship", host.transform, new Vector2(180f, 110f), Vector2.zero });
                RawImage hull = preview.Find("Test Ship Hull").GetComponent<RawImage>();
                Rect source = catalog.ShipUv(shipIndex);
                float pixelWidth = catalog.ShipTexture(shipIndex).width * source.width - 20f;
                float pixelHeight = catalog.ShipTexture(shipIndex).height * source.height - 20f;
                Assert.That(hull.rectTransform.rect.width / hull.rectTransform.rect.height,
                    Is.EqualTo(pixelWidth / pixelHeight).Within(0.0001f));
                Assert.That(hull.uvRect.width * hull.texture.width, Is.EqualTo(pixelWidth).Within(0.001f));
                Assert.That(hull.uvRect.height * hull.texture.height, Is.EqualTo(pixelHeight).Within(0.001f));
                Assert.That(preview.sizeDelta, Is.EqualTo(new Vector2(180f, 110f)));
                Assert.That(hull.rectTransform.rect.height, Is.EqualTo(110f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase("跳板接舷")]
        [TestCase("角对角锁定")]
        [TestCase("角对侧舷卡锁")]
        public void BoardingMarkerHasSeparateIconAndReadableTwoLineLabel(string connectionLabel)
        {
            GameObject host = new GameObject("Marker geometry", typeof(RectTransform));
            host.SetActive(false);
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                GameObject zone = new GameObject("Boarding Clash Zone", typeof(RectTransform), typeof(Image));
                zone.transform.SetParent(host.transform, false);
                RectTransform frame = zone.GetComponent<RectTransform>();
                typeof(CoreGameplayDemoController).GetMethod("ConfigureBoardingMeleeMarker", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { frame, connectionLabel, Color.yellow });
                Text label = frame.Find("Boarding Clash Label").GetComponent<Text>();
                RectTransform icon = (RectTransform)frame.Find("Boarding Clash Mark");
                Assert.That(label.text.Split('\n').Length, Is.EqualTo(2));
                Assert.That(label.text, Does.Not.Contain("\u2694"));
                Assert.That(frame.Find("Boarding Clash Glyph"), Is.Null);
                Assert.That(icon.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(6));
                float iconRight = icon.anchoredPosition.x + icon.rect.xMax;
                float labelLeft = label.rectTransform.anchoredPosition.x + label.rectTransform.rect.xMin;
                Assert.That(labelLeft - iconRight, Is.GreaterThanOrEqualTo(16f));
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * label.lineSpacing * 2f));
                Assert.That(zone.GetComponent<Image>().color.a, Is.GreaterThanOrEqualTo(0.95f));
                Assert.That(zone.GetComponent<Image>().sprite, Is.Null);
                Assert.That(label.color.grayscale, Is.GreaterThan(0.8f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(523.914f, 523.914f, NavalBoardingApproach.Alongside)]
        [TestCase(523.914f, 523.914f, NavalBoardingApproach.Quarter)]
        [TestCase(483.566f, 523.914f, NavalBoardingApproach.Alongside)]
        [TestCase(483.566f, 523.914f, NavalBoardingApproach.Quarter)]
        public void BoardingTimberFitsHullAndConnectionsMeetActualCleats(float playerWidth, float enemyWidth, NavalBoardingApproach approach)
        {
            GameObject host = new GameObject("Boarding geometry", typeof(RectTransform));
            try
            {
                RectTransform root = host.GetComponent<RectTransform>();
                root.localScale = Vector3.one * 1.37f;
                RectTransform player = MakeDeck(root, "Player Boarding Deck", BattleViewLayout.BoardingPosition(false, approach));
                RectTransform enemy = MakeDeck(root, "Enemy Boarding Deck", BattleViewLayout.BoardingPosition(true, approach));
                RectTransform bridge = MakeRect("Boarding Gangplank", root, new Vector2(250f, 30f), Vector2.zero);
                for (int rope = 0; rope < 2; rope++) MakeRect("Boarding Grapple Rope " + rope, root, new Vector2(272f, 5f), Vector2.zero);
                MethodInfo configure = typeof(CoreGameplayDemoController).GetMethod("ConfigureBoardingDeckGeometry", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo connect = typeof(CoreGameplayDemoController).GetMethod("UpdateBoardingConnections", BindingFlags.Static | BindingFlags.NonPublic);
                configure.Invoke(null, new object[] { player, playerWidth, false });
                configure.Invoke(null, new object[] { enemy, enemyWidth, true });
                connect.Invoke(null, new object[] { root });
                Vector2 savedBridgeSize = bridge.rect.size;
                Vector3 savedBridgePosition = bridge.localPosition;
                // A repeated authored-scene sync must not shrink the deck again.
                configure.Invoke(null, new object[] { player, playerWidth, false });
                configure.Invoke(null, new object[] { enemy, enemyWidth, true });
                connect.Invoke(null, new object[] { root });
                Assert.That(bridge.rect.size, Is.EqualTo(savedBridgeSize));
                Assert.That(bridge.localPosition, Is.EqualTo(savedBridgePosition));

                foreach (RectTransform deck in new[] { player, enemy })
                {
                    float hullWidth = deck == player ? playerWidth : enemyWidth;
                    foreach (RectTransform timber in deck)
                    {
                        Assert.That(timber.anchoredPosition.x + timber.rect.xMin, Is.GreaterThanOrEqualTo(-hullWidth * 0.5f), timber.name);
                        Assert.That(timber.anchoredPosition.x + timber.rect.xMax, Is.LessThanOrEqualTo(hullWidth * 0.5f), timber.name);
                    }
                    Assert.That(((RectTransform)deck.Find(deck.name + " Deck Surface")).rect.width,
                        Is.LessThan(hullWidth * 0.8f), "Timber must shrink with the fitted hull, not retain its old platform width.");
                }

                Vector3 playerMiddle = Vector3.zero;
                Vector3 enemyMiddle = Vector3.zero;
                for (int rope = 0; rope < 2; rope++)
                {
                    RectTransform playerCleat = (RectTransform)player.Find(player.name + " Cleat Cap " + rope);
                    RectTransform enemyCleat = (RectTransform)enemy.Find(enemy.name + " Cleat Cap " + rope);
                    Vector3 from = playerCleat.TransformPoint(playerCleat.rect.center);
                    Vector3 to = enemyCleat.TransformPoint(enemyCleat.rect.center);
                    playerMiddle += from * 0.5f;
                    enemyMiddle += to * 0.5f;
                    AssertConnectionEndpoints((RectTransform)root.Find("Boarding Grapple Rope " + rope), from, to);
                }
                AssertConnectionEndpoints(bridge, playerMiddle, enemyMiddle);
                float heightDifference = root.InverseTransformPoint(enemyMiddle).y - root.InverseTransformPoint(playerMiddle).y;
                Assert.That(heightDifference, Is.EqualTo(approach == NavalBoardingApproach.Quarter ? 46f : 0f).Within(0.001f));
                if (approach == NavalBoardingApproach.Quarter)
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, bridge.localEulerAngles.z)), Is.GreaterThan(1f));
                else
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, bridge.localEulerAngles.z)), Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static RectTransform MakeDeck(RectTransform root, string name, Vector2 position)
        {
            RectTransform deck = MakeRect(name, root, new Vector2(650f, 160f), position);
            MakeRect(name + " Deck Surface", deck, new Vector2(500f, 52f), new Vector2(0f, 41f));
            for (int post = 0; post < 6; post++) MakeRect(name + " Rail " + post, deck, new Vector2(6f, 26f), new Vector2(-204f + post * 81.6f, 74f));
            MakeRect(name + " Gunwale", deck, new Vector2(419f, 5f), new Vector2(0f, 87f));
            for (int cleat = 0; cleat < 2; cleat++)
            {
                MakeRect(name + " Cleat Post " + cleat, deck, new Vector2(7f, 15f), new Vector2(225f, 26f + cleat * 27f));
                MakeRect(name + " Cleat Cap " + cleat, deck, new Vector2(22f, 5f), new Vector2(225f, 33f + cleat * 27f));
            }
            return deck;
        }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void AssertConnectionEndpoints(RectTransform connection, Vector3 from, Vector3 to)
        {
            Vector3 left = connection.TransformPoint(new Vector3(connection.rect.xMin, 0f, 0f));
            Vector3 right = connection.TransformPoint(new Vector3(connection.rect.xMax, 0f, 0f));
            Assert.That(Vector3.Distance(left, from), Is.LessThan(0.001f), connection.name + " left endpoint");
            Assert.That(Vector3.Distance(right, to), Is.LessThan(0.001f), connection.name + " right endpoint");
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
