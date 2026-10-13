using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class MainMenuLayoutTests
    {
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
                    fixture.AssertFixedGameSurface(width, height, state);
                    foreach (Text label in fixture.Host.GetComponentsInChildren<Text>(false))
                        AssertInsideGameSurface(fixture.GameRoot, label.rectTransform, state + " / " + label.name);
                    foreach (Selectable control in fixture.Host.GetComponentsInChildren<Selectable>(false))
                        AssertInsideGameSurface(fixture.GameRoot, control.GetComponent<RectTransform>(), state + " / " + control.name);
                }
                fixture.Show("Home");
                fixture.Render();
                RectTransform menu = fixture.Find<RectTransform>("Main Menu Content");
                foreach (string removedLabel in new[] { "Eyebrow", "Subtitle", "Version", "Save State" })
                    Assert.That(menu.Find(removedLabel), Is.Null, removedLabel + " must be removed from the main menu.");

                RawImage title = fixture.Find<RawImage>("Game Title Artwork");
                Texture2D titleTexture = Resources.Load<Texture2D>("MainMenu/main_menu_title_bilingual");
                Assert.That(titleTexture, Is.Not.Null, "The bilingual title artwork must be available to the menu.");
                Assert.That(title.texture, Is.SameAs(titleTexture));
                Assert.That(title.isActiveAndEnabled && title.color.a > 0f, Is.True,
                    "The title artwork must remain visible on the home screen.");
                Assert.That(titleTexture.width / (float)titleTexture.height, Is.EqualTo(2f).Within(0.01f));
                Assert.That(title.rectTransform.rect.width / title.rectTransform.rect.height,
                    Is.EqualTo(titleTexture.width / (float)titleTexture.height).Within(0.01f),
                    "The nautical lettering must keep the source artwork's proportions.");
                AssertInsideGameSurface(fixture.GameRoot, title.rectTransform, "Home / Game Title Artwork");

                string[] actionNames = { "New Game", "Continue Game", "Settings", "Achievements", "Exit Game" };
                foreach (string name in actionNames)
                    Assert.That(menu.Find(name + "/Detail"), Is.Null,
                        name + " must keep a single title without auxiliary menu text.");
                Rect[] actions = actionNames.Select(name =>
                    RectIn(menu, fixture.Find<Button>(name).GetComponent<RectTransform>())).ToArray();
                Rect heading = RectIn(menu, title.rectTransform);
                Assert.That(heading.yMin - actions[0].yMax, Is.GreaterThanOrEqualTo(12f),
                    "The large title needs clear separation from the first action.");
                for (int index = 1; index < actions.Length; index++)
                    Assert.That(actions[index - 1].yMin - actions[index].yMax, Is.GreaterThan(0f),
                        actionNames[index - 1] + " must appear above " + actionNames[index] + " with separate hit areas.");

                fixture.Click("Settings");
                Assert.That(fixture.Find<RectTransform>("Settings Panel").gameObject.activeInHierarchy, Is.True,
                    "The centered game surface must preserve real pointer interaction at " + width + "x" + height);
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

        private static void AssertInsideGameSurface(RectTransform surface, RectTransform rect, string context)
        {
            Assert.That(rect.IsChildOf(surface), Is.True, context + " belongs to the fixed game surface");
            Rect bounds = RectIn(surface, rect);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(surface.rect.xMin - .75f), context + " left");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(surface.rect.xMax + .75f), context + " right");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(surface.rect.yMin - .75f), context + " bottom");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(surface.rect.yMax + .75f), context + " top");
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
            private static readonly MethodInfo AddRaycaster = typeof(RaycasterManager)
                .GetMethod("AddRaycaster", BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo RemoveRaycaster = typeof(RaycasterManager)
                .GetMethod("RemoveRaycasters", BindingFlags.Static | BindingFlags.NonPublic);
            private readonly System.Collections.Generic.HashSet<BaseRaycaster> registeredRaycasters =
                new System.Collections.Generic.HashSet<BaseRaycaster>();
            private readonly Scene scene;
            private readonly RenderTexture target;
            public readonly GameObject Host;
            public readonly Canvas Canvas;
            public readonly Camera Camera;
            private readonly EventSystem events;
            public RectTransform GameRoot => Find<RectTransform>(FixedGameViewport.Name);
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
                var eventHost = new GameObject("Menu Layout Events");
                eventHost.SetActive(false);
                SceneManager.MoveGameObjectToScene(eventHost, scene);
                events = eventHost.AddComponent<EventSystem>();
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
                RegisterRaycasters();
            }
            public void AssertFixedGameSurface(int width, int height, string state)
            {
                string context = state + " " + width + "x" + height;
                RectTransform root = GameRoot;
                Assert.That(root.rect.width, Is.EqualTo(1920f).Within(.01f), context + " logical game width");
                Assert.That(root.rect.height, Is.EqualTo(1080f).Within(.01f), context + " logical game height");
                Assert.That(root.GetComponent<RectMask2D>(), Is.Not.Null, context + " game content stays out of the bars");
                float scale = Mathf.Min(width / 1920f, height / 1080f);
                var expected = new Rect((width - 1920f * scale) * .5f, (height - 1080f * scale) * .5f,
                    1920f * scale, 1080f * scale);
                var corners = new Vector3[4];
                root.GetWorldCorners(corners);
                Vector3 lower = Camera.WorldToScreenPoint(corners[0]);
                Vector3 upper = Camera.WorldToScreenPoint(corners[2]);
                Assert.That(lower.x, Is.EqualTo(expected.xMin).Within(.1f), context + " centered game left");
                Assert.That(lower.y, Is.EqualTo(expected.yMin).Within(.1f), context + " centered game bottom");
                Assert.That(upper.x, Is.EqualTo(expected.xMax).Within(.1f), context + " centered game right");
                Assert.That(upper.y, Is.EqualTo(expected.yMax).Within(.1f), context + " centered game top");
                Assert.That((upper.x - lower.x) / (upper.y - lower.y), Is.EqualTo(16f / 9f).Within(.0001f), context);
                Graphic bars = Find<Graphic>("Game Letterbox");
                Assert.That(bars.isActiveAndEnabled, Is.True, context);
                Assert.That(bars.color, Is.EqualTo(Color.black), context + " opaque black bars");
                Assert.That(bars.raycastTarget, Is.False, context + " bars cannot intercept input");
                RectTransform canvasRect = Canvas.GetComponent<RectTransform>();
                Rect barBounds = RectIn(canvasRect, bars.rectTransform);
                Assert.That(barBounds.xMin, Is.EqualTo(canvasRect.rect.xMin).Within(.01f), context);
                Assert.That(barBounds.xMax, Is.EqualTo(canvasRect.rect.xMax).Within(.01f), context);
                Assert.That(barBounds.yMin, Is.EqualTo(canvasRect.rect.yMin).Within(.01f), context);
                Assert.That(barBounds.yMax, Is.EqualTo(canvasRect.rect.yMax).Within(.01f), context);
                Transform overlay = Canvas.transform.Find("Game Letterbox Overlay");
                Assert.That(overlay, Is.Not.Null, context + " black bars must cover independently sorted popups");
                var probes = new System.Collections.Generic.List<Vector2>();
                if (expected.xMin > 1f)
                    foreach (float y in new[] { .1f, .5f, .9f })
                    {
                        probes.Add(new Vector2(expected.xMin * .5f, height * y));
                        probes.Add(new Vector2((expected.xMax + width) * .5f, height * y));
                    }
                if (expected.yMin > 1f)
                    foreach (float x in new[] { .1f, .5f, .9f })
                    {
                        probes.Add(new Vector2(width * x, expected.yMin * .5f));
                        probes.Add(new Vector2(width * x, (expected.yMax + height) * .5f));
                    }
                foreach (Vector2 pixel in probes)
                {
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    var pointer = new PointerEventData(events) { position = pixel };
                    events.RaycastAll(pointer, hits);
                    Assert.That(hits, Is.Not.Empty, context + " black bar must block input at " + pixel);
                    Assert.That(hits[0].gameObject.transform.IsChildOf(overlay), Is.True,
                        context + " topmost hit must be the black bar at " + pixel);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.Null,
                        context + " black bar click cannot reach an underlying action");
                    Assert.That(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.Null,
                        context + " blocked click must not invoke a control");
                }
                foreach (float x in new[] { .01f, .5f, .99f })
                    foreach (float y in new[] { .01f, .5f, .99f })
                    {
                        Vector2 pixel = new Vector2(expected.xMin + expected.width * x, expected.yMin + expected.height * y);
                        var hits = new System.Collections.Generic.List<RaycastResult>();
                        events.RaycastAll(new PointerEventData(events) { position = pixel }, hits);
                        Assert.That(hits.Any(hit => hit.gameObject.transform.IsChildOf(overlay)), Is.False,
                            context + " the black bars must leave game input clear at " + pixel);
                    }
                if (probes.Count == 0)
                {
                    foreach (Graphic edge in overlay.GetComponentsInChildren<Graphic>().Where(edge => edge.isActiveAndEnabled))
                        Assert.That(Mathf.Min(Mathf.Abs(edge.rectTransform.rect.width), Mathf.Abs(edge.rectTransform.rect.height)),
                            Is.LessThanOrEqualTo(.001f), context + " 16:9 must have no visible or interactive black bar");
                }
            }
            public void Click(string name)
            {
                Button button = Find<Button>(name);
                var rect = button.GetComponent<RectTransform>();
                var pointer = new PointerEventData(events)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(rect.rect.center))
                };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty, name + " must receive a real screen-space pointer hit");
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject), name);
                pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = hits[0];
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }
            private void RegisterRaycasters()
            {
                // Preview scenes do not run BaseRaycaster's normal OnEnable.
                // Register actual modules through the same API, preserving the
                // normal EventSystem raycasts and cross-canvas hit sorting.
                foreach (BaseRaycaster raycaster in Canvas.GetComponentsInChildren<BaseRaycaster>())
                {
                    if (!raycaster.isActiveAndEnabled || RaycasterManager.GetRaycasters().Contains(raycaster)) continue;
                    AddRaycaster.Invoke(null, new object[] { raycaster });
                    registeredRaycasters.Add(raycaster);
                }
            }
            public void Dispose()
            {
                foreach (BaseRaycaster raycaster in registeredRaycasters)
                    RemoveRaycaster.Invoke(null, new object[] { raycaster });
                registeredRaycasters.Clear();
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
