using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class MainMenuLayoutTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ReadingSizes_SettingsAndHomeKeepTextSizeAndControlsReachable(int level)
        {
            int? previous = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = level;
            try
            {
                using (var fixture = new MenuFixture(1280, 720))
                {
                    fixture.Show("Settings Panel"); fixture.Render();
                    RectTransform card = fixture.Find<RectTransform>("Settings Card");
                    foreach (Text label in card.GetComponentsInChildren<Text>(false))
                    {
                        if (string.IsNullOrEmpty(label.text)) continue;
                        Assert.That(label.resizeTextForBestFit, Is.False, label.name);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), label.name);
                        AssertInsideScreen(fixture.Camera, label.rectTransform, 1280, 720, label.name);
                    }
                    foreach (Button button in card.GetComponentsInChildren<Button>())
                        Assert.That(button.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(22)), button.name);
                    ReadabilitySelector selector = card.GetComponentInChildren<ReadabilitySelector>();
                    selector.Buttons[(level + 1) % 3].onClick.Invoke();
                    Assert.That(selector.Value, Is.EqualTo((level + 1) % 3));
                    Assert.That(GameReadability.Level, Is.EqualTo(level), "Choosing a draft must not apply it before confirmation.");
                    fixture.Show("Home"); fixture.Render();
                    foreach (string name in new[] { "Eyebrow", "English Title", "Save State", "Version" })
                    {
                        Text label = fixture.Find<Text>(name);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), name);
                        Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length), name);
                    }
                    Text intro = fixture.Find<Text>("Subtitle");
                    Assert.That(intro.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
                    Assert.That(intro.text, Is.EqualTo(SeventhPortLoreCatalog.CurrentIntroduction));
                    if (level > 0)
                    {
                        ScrollRect scroll = intro.GetComponentInParent<ScrollRect>();
                        Assert.That(scroll, Is.Not.Null);
                        scroll.verticalNormalizedPosition = 0f; fixture.Render();
                        Rect thumb = RectIn(scroll.verticalScrollbar.GetComponent<RectTransform>(), scroll.verticalScrollbar.handleRect);
                        Assert.That(thumb.width, Is.LessThanOrEqualTo(8.1f), "The scrollbar thumb must stay on its narrow rail.");
                        Assert.That(RectIn(scroll.viewport, intro.rectTransform).yMin,
                            Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1f), "The last story line remains reachable.");
                    }
                    foreach (string name in new[] { "New Game", "Continue Game", "Settings", "Achievements", "Exit Game" })
                    {
                        Button button = fixture.Find<Button>(name);
                        AssertInsideScreen(fixture.Camera, button.GetComponent<RectTransform>(), 1280, 720, name);
                        Assert.That(button.transform.Find("Title").GetComponent<Text>().fontSize, Is.EqualTo(GameReadability.FontSize(26)));
                    }
                }
            }
            finally { GameReadability.OverrideLevel = previous; }
        }

        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1280, 1024)]
        [TestCase(3440, 1440)]
        [TestCase(3840, 1080)]
        public void RealCanvasScaler_KeepsMenuAndDialogsInsideEverySupportedViewport(int width, int height)
        {
            using (var fixture = new MenuFixture(width, height))
            {
                foreach (string state in new[] { "Home", "Settings Panel", "Achievements Panel", "New Game Confirmation" })
                {
                    fixture.Show(state);
                    fixture.Render();
                    Assert.That(fixture.Canvas.renderingDisplaySize.x, Is.EqualTo(width).Within(0.5f));
                    Assert.That(fixture.Canvas.renderingDisplaySize.y, Is.EqualTo(height).Within(0.5f));
                    Assert.That(fixture.Canvas.scaleFactor,
                        Is.EqualTo(Mathf.Min(width / 1920f, height / 1080f)).Within(0.001f));
                    foreach (Text label in fixture.Host.GetComponentsInChildren<Text>(false))
                        AssertInsideScreen(fixture.Camera, label.rectTransform, width, height, state + " / " + label.name);
                    foreach (Selectable control in fixture.Host.GetComponentsInChildren<Selectable>(false))
                        AssertInsideScreen(fixture.Camera, control.GetComponent<RectTransform>(), width, height, state + " / " + control.name);
                }
                fixture.Show("Home");
                fixture.Render();
                Text introduction = fixture.Find<Text>("Subtitle");
                Assert.That(introduction.text, Is.EqualTo(SeventhPortLoreCatalog.CurrentIntroduction), "Do not shorten away the story to make it fit.");
                Assert.That(introduction.resizeTextForBestFit, Is.False, "The paragraph must not shrink back to the unreadable old size.");
                Assert.That(introduction.fontSize * fixture.Canvas.scaleFactor, Is.GreaterThanOrEqualTo(14f));
                Assert.That(introduction.preferredHeight, Is.LessThanOrEqualTo(introduction.rectTransform.rect.height + 0.5f),
                    "The full introduction must fit without vertical truncation.");
                Assert.That(introduction.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(introduction.text.Length),
                    "All story characters must survive actual text generation.");
                Rect intro = RectIn(fixture.Find<RectTransform>("Main Menu Content"), introduction.rectTransform);
                Rect firstButton = RectIn(fixture.Find<RectTransform>("Main Menu Content"), fixture.Find<Button>("New Game").GetComponent<RectTransform>());
                Assert.That(intro.yMin - firstButton.yMax, Is.GreaterThanOrEqualTo(12f), "The narrative needs separation from the first action.");
            }
        }

        [Test]
        public void PaperDialogs_UseDarkReadableInkWhileWoodButtonsKeepLightLabels()
        {
            using (var fixture = new MenuFixture(1280, 720))
            {
                ColorUtility.TryParseHtmlString("#BEA16A", out Color conservativePaper);
                foreach (string state in new[] { "Settings Panel", "Achievements Panel", "New Game Confirmation" })
                {
                    fixture.Show(state);
                    fixture.Render();
                    Transform panel = fixture.Find<RectTransform>(state);
                    foreach (Text label in panel.GetComponentsInChildren<Text>(false))
                    {
                        if (label.GetComponentInParent<Button>() != null || label.name == "Symbol") continue;
                        Assert.That(Contrast(label.color, conservativePaper), Is.GreaterThanOrEqualTo(4.5f), label.name + " on parchment");
                        Assert.That(label.color.a, Is.EqualTo(1f), label.name + " must not become faded body text");
                        Assert.That(label.fontSize * fixture.Canvas.scaleFactor, Is.GreaterThanOrEqualTo(12f), label.name + " at 720p");
                    }
                    foreach (Button button in panel.GetComponentsInChildren<Button>(false))
                        Assert.That(Luminance(button.GetComponentInChildren<Text>().color), Is.GreaterThan(0.5f), button.name + " remains light on wood");
                }
            }
        }

        private static void AssertInsideScreen(Camera camera, RectTransform rect, int width, int height, string context)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 p = camera.WorldToScreenPoint(corner);
                Assert.That(p.x, Is.InRange(-0.75f, width + 0.75f), context + " / x");
                Assert.That(p.y, Is.InRange(-0.75f, height + 0.75f), context + " / y");
            }
        }

        private static Rect RectIn(Transform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(p => p.x), local.Min(p => p.y), local.Max(p => p.x), local.Max(p => p.y));
        }

        private static float Luminance(Color color)
        {
            Func<float, float> linear = c => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * linear(color.r) + 0.7152f * linear(color.g) + 0.0722f * linear(color.b);
        }
        private static float Contrast(Color a, Color b)
        {
            float x = Luminance(a), y = Luminance(b);
            return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f);
        }

        private sealed class MenuFixture : IDisposable
        {
            private readonly Scene scene;
            private readonly RenderTexture target;
            public readonly GameObject Host;
            public readonly Canvas Canvas;
            public readonly Camera Camera;
            public MenuFixture(int width, int height)
            {
                scene = EditorSceneManager.NewPreviewScene();
                Host = new GameObject("Menu Layout Regression");
                Host.SetActive(false);
                SceneManager.MoveGameObjectToScene(Host, scene);
                Host.AddComponent<MainMenuController>();
                Host.SetActive(true);
                Canvas = Host.GetComponentInChildren<Canvas>();
                var cameraHost = new GameObject("Menu Layout Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera = cameraHost.GetComponent<Camera>();
                Camera.enabled = false;
                Camera.scene = scene;
                Camera.orthographic = true;
                Camera.orthographicSize = 5f;
                Camera.nearClipPlane = 0.1f;
                Camera.farClipPlane = 2000f;
                Camera.transform.position = new Vector3(0, 0, -1000);
                Camera.cullingMask = 1 << 31;
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = Color.black;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                Camera.targetTexture = target;
                Camera.aspect = width / (float)height;
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = Camera;
                Canvas.planeDistance = 100f;
                foreach (Transform child in Canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            }
            public T Find<T>(string name) where T : Component => Host.GetComponentsInChildren<T>(true).Single(c => c.name == name);
            public void Show(string state)
            {
                if (state == "Achievements Panel") Find<Button>("Achievements").onClick.Invoke();
                foreach (string name in new[] { "Settings Panel", "Achievements Panel", "New Game Confirmation" })
                {
                    RectTransform panel = Host.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(item => item.name == name);
                    if (panel != null) panel.gameObject.SetActive(name == state);
                }
                foreach (Transform child in Canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            }
            public void Render()
            {
                CanvasScaler scaler = Canvas.GetComponent<CanvasScaler>();
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < 3; i++)
                {
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in Host.GetComponentsInChildren<Text>(false)) label.SetAllDirty();
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(Camera, new RenderPipeline.StandardRequest { destination = target });
                    else Camera.Render();
                }
            }
            public void Dispose()
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
