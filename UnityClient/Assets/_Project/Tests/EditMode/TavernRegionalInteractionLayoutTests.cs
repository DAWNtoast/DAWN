using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    [TestFixture(CoreGameplayPreviewPort.BrokenAnchor, "断锚港")]
    [TestFixture(CoreGameplayPreviewPort.WhiteShell, "白贝港")]
    [TestFixture(CoreGameplayPreviewPort.BronzeBell, "铜钟港")]
    [TestFixture(CoreGameplayPreviewPort.SaltCrown, "盐冠港")]
    [TestFixture(CoreGameplayPreviewPort.TwilightTide, "暮潮港")]
    [TestFixture(CoreGameplayPreviewPort.GrayGull, "灰鸥港")]
    public sealed class TavernRegionalInteractionLayoutTests
    {
        private readonly CoreGameplayPreviewPort port;
        private readonly string portName;
        private Scene scene;
        private GameObject host;
        private CoreGameplayDemoController controller;
        private Camera camera;
        private EventSystem events;
        private RenderTexture target;
        private Dictionary<string, string> savedPreferences;

        public TavernRegionalInteractionLayoutTests(CoreGameplayPreviewPort port, string portName)
        {
            this.port = port;
            this.portName = portName;
        }

        // One host, isolated scene, camera and render target per port. The six
        // cases exercise real dossier/return navigation instead of rebuilding
        // the entire preview for every profession or pointer target.
        [OneTimeSetUp]
        public void CreateRegionalPreview()
        {
            Assert.That(Application.isPlaying, Is.False);
            savedPreferences = ReadRelevantPreferences();
            scene = EditorSceneManager.NewPreviewScene();
            host = new GameObject("Regional Tavern Interaction " + port) { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, scene);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, port, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);

            var cameraHost = new GameObject("Tavern Interaction Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000f;
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080f;
            camera.rect = new Rect(0f, 0f, 1f, 1f);

            // A disabled local EventSystem supplies pointer context without
            // replacing the editor's current EventSystem or querying other UIs.
            var eventHost = new GameObject("Tavern Interaction Events");
            eventHost.SetActive(false);
            SceneManager.MoveGameObjectToScene(eventHost, scene);
            events = eventHost.AddComponent<EventSystem>();
        }

        [OneTimeTearDown]
        public void DisposeRegionalPreview()
        {
            if (camera != null) camera.targetTexture = null;
            if (host != null) Object.DestroyImmediate(host);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            AssertPreferencesUnchanged();
        }

        [TearDown]
        public void ReturnToLineupAfterCase()
        {
            // Recover the shared fixture after an assertion fails in a dossier.
            if (host != null && Active<RectTransform>("Tavern Crew Detail") != null)
                Find<Button>("Return To Port Hub").onClick.Invoke();
            AssertPreferencesUnchanged();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void PaintedCrewAndName_RaycastToTheirOwnRegionalDossier(int profession)
        {
            CrewVariantDefinition expected = CrewNarrativeCatalog.ForPortAndProfession(portName, profession);
            RenderCurrentCanvas();
            AssertLineupGeometry(profession, expected);

            ClickAndAssertDossier(Find<Button>("Tavern Crew " + profession).GetComponent<RectTransform>(), profession, expected);
            ReturnThroughDossierHeader();

            // Reacquire the actual controls: navigation may replace the body.
            RenderCurrentCanvas();
            ClickAndAssertDossier(Find<Text>("Tavern Crew Name " + profession).rectTransform, profession, expected);
            ReturnThroughDossierHeader();
        }

        private void AssertLineupGeometry(int profession, CrewVariantDefinition expected)
        {
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(portName));
            RectTransform canvas = CurrentCanvas().GetComponent<RectTransform>();
            RawImage background = Find<RawImage>("Tavern Background");
            Assert.That(background.texture, Is.Not.Null, portName + " regional painting");
            Rect picture = BoundsIn(canvas, background.rectTransform);
            Rect header = BoundsIn(canvas, Find<RectTransform>("Port Facility Header"));
            Button[] crew = Enumerable.Range(0, 6).Select(index => Find<Button>("Tavern Crew " + index)).ToArray();
            Assert.That(host.GetComponentsInChildren<Button>(true).Count(button => button.gameObject.activeInHierarchy &&
                button.name.StartsWith("Tavern Crew ") && button.name.Length == "Tavern Crew 0".Length), Is.EqualTo(6));

            Rect own = BoundsIn(canvas, crew[profession].GetComponent<RectTransform>());
            AssertContained(picture, own, expected.Id + " painted hotspot");
            Assert.That(own.width, Is.GreaterThan(0f));
            Assert.That(own.height, Is.GreaterThan(0f));
            Assert.That(crew[profession].IsInteractable(), Is.True, expected.Id);
            foreach (int other in Enumerable.Range(0, 6).Where(index => index != profession))
                Assert.That(OverlapsBeyondTransformTolerance(own, BoundsIn(canvas, crew[other].GetComponent<RectTransform>())), Is.False,
                    expected.Id + " hotspot overlaps profession " + other);

            Text name = Find<Text>("Tavern Crew Name " + profession);
            Text status = Find<Text>("Tavern Crew Status " + profession);
            Assert.That(name.text, Is.EqualTo(expected.DisplayName));
            foreach (Text label in new[] { name, status })
            {
                Rect bounds = BoundsIn(canvas, label.rectTransform);
                Assert.That(label.text, Is.Not.Empty, expected.Id + " / " + label.name);
                AssertContained(picture, bounds, expected.Id + " / complete " + label.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(header.yMin + .05f),
                    expected.Id + " / " + label.name + " must stay below the facility header");
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f),
                    expected.Id + " / " + label.name + " must not truncate horizontally");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                    expected.Id + " / " + label.name + " must not truncate vertically");
                foreach (int other in Enumerable.Range(0, 6).Where(index => index != profession))
                    Assert.That(OverlapsBeyondTransformTolerance(bounds, BoundsIn(canvas, crew[other].GetComponent<RectTransform>())), Is.False,
                        expected.Id + " / " + label.name + " covers another person's head/torso (profession " + other + ")");
            }
        }

        private void ClickAndAssertDossier(RectTransform clickTarget, int profession, CrewVariantDefinition expected)
        {
            Button button = Find<Button>("Tavern Crew " + profession);
            var raycaster = CurrentCanvas().GetComponent<GraphicRaycaster>();
            Assert.That(raycaster.eventCamera, Is.SameAs(camera));
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, clickTarget.TransformPoint(clickTarget.rect.center));
            var pointer = new PointerEventData(events)
            {
                position = point,
                pressPosition = point,
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            string context = expected.Id + " / " + clickTarget.name;
            Assert.That(hits, Is.Not.Empty, context + " has no graphic raycast hit at " + point);
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), context + " is intercepted by " + hits[0].gameObject.name);
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pointerPressRaycast = hits[0];
            Assert.That(ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler), Is.True, context);

            // Assert what the click really opened, not only the hotspot's name
            // or the source catalogue entry. The artwork ID also detects a
            // different port's same-profession dossier being selected.
            Assert.That(Find<RectTransform>("Tavern Crew Detail").gameObject.activeInHierarchy, Is.True, context);
            Assert.That(Find<Text>("Tavern Crew Detail Name").text, Is.EqualTo(expected.DisplayName), context);
            Assert.That(Find<Text>("Tavern Crew Detail Role").text,
                Is.EqualTo(portName + "　·　" + expected.RoleSummary), context);
            RawImage dossier = Find<RawImage>("Tavern Dossier Parchment");
            Assert.That(dossier.texture, Is.Not.Null, context);
            Assert.That(dossier.texture.name, Is.EqualTo(expected.Id + "_dossier_v1"), context);
            Assert.That(Find<Button>("Recruit Tavern Crew " + profession).gameObject.activeInHierarchy, Is.True, context);
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(portName), context);
        }

        private void ReturnThroughDossierHeader()
        {
            Button back = Find<Button>("Return To Port Hub");
            Assert.That(back.GetComponentInChildren<Text>(true).text, Is.EqualTo("返回酒馆"));
            back.onClick.Invoke();
            Assert.That(Active<RectTransform>("Tavern Crew Detail"), Is.Null);
            Assert.That(Find<RectTransform>("Tavern Crew Stage").gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(portName));
        }

        private Canvas CurrentCanvas() => host.GetComponentsInChildren<Canvas>(true)
            .Single(item => item.isRootCanvas && item.gameObject.activeInHierarchy);

        private void RenderCurrentCanvas()
        {
            Canvas canvas = CurrentCanvas();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int pass = 0; pass < 2; pass++)
            {
                Canvas.ForceUpdateCanvases();
                handle.Invoke(scaler, null);
                foreach (Text label in canvas.GetComponentsInChildren<Text>()) label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
            }
            Assert.That(canvas.pixelRect.width, Is.EqualTo(1920f).Within(.5f));
            Assert.That(canvas.pixelRect.height, Is.EqualTo(1080f).Within(.5f));
        }

        private T Active<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true)
            .FirstOrDefault(component => component.name == name && component.gameObject.activeInHierarchy);

        private T Find<T>(string name) where T : Component
        {
            T result = Active<T>(name);
            Assert.That(result, Is.Not.Null, portName + " / " + name);
            return result;
        }

        private static Rect BoundsIn(RectTransform root, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = root.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = root.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool OverlapsBeyondTransformTolerance(Rect first, Rect second)
        {
            // Shared authored edges can differ by roughly 0.00006 UI units
            // after world/canvas float transforms. Use the same subpixel
            // tolerance as containment, while rejecting any visible overlap.
            return Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin) > .05f &&
                Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin) > .05f;
        }

        private static void AssertContained(Rect outer, Rect inner, string context)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .05f), context + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .05f), context + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .05f), context + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .05f), context + " top");
        }

        private static Dictionary<string, string> ReadRelevantPreferences()
        {
            // Read-only snapshots: these tests never seed or restore PlayerPrefs.
            var snapshot = new Dictionary<string, string>();
            var intKeys = new List<string> { CoreGameplayDemoController.ProfileGoldKey, CoreGameplayDemoController.LegacyWitchRecruitedKey };
            var stringKeys = new List<string> { CoreGameplayDemoController.CurrentPortKey, CoreGameplayDemoController.CrewAtSeaKey };
            for (int profession = 0; profession < 6; profession++)
            {
                intKeys.Add(CoreGameplayDemoController.CrewRecruitmentKey(profession));
                stringKeys.Add(CoreGameplayDemoController.ActiveCrewVariantKey(profession));
            }
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                intKeys.Add(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id));
                stringKeys.Add(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id));
            }
            foreach (string key in intKeys) snapshot[key] = PlayerPrefs.HasKey(key) + ":" + PlayerPrefs.GetInt(key, 0);
            foreach (string key in stringKeys) snapshot[key] = PlayerPrefs.HasKey(key) + ":" + PlayerPrefs.GetString(key, string.Empty);
            return snapshot;
        }

        private void AssertPreferencesUnchanged()
        {
            if (savedPreferences == null) return;
            Dictionary<string, string> after = ReadRelevantPreferences();
            foreach (KeyValuePair<string, string> preference in savedPreferences)
                Assert.That(after[preference.Key], Is.EqualTo(preference.Value),
                    "Preview interaction must not write player progress: " + preference.Key);
        }
    }
}
