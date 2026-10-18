using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    // Installed only in the isolated validation project. Temporary crew keys are restored after every frame.
    public static class DepartureCrewModalCapture20261014
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Three 1920x1080 crew modal states using the production CanvasScaler and transient preview scenes. Temporary crew fixtures use the independently verified validation PlayerPrefs identity and restore every touched key; no player archive or scene saves.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string state, file, background;
            public int width = 1920, height = 1080, rosterCount, selectedCount, navigatorCount;
            public bool passed, verifiedScaler, backgroundUnchanged, vesselUnchanged;
            public float scaleFactor, expectedScaleFactor, scrollPosition;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect, modalBounds, viewportBounds, vesselBounds;
            public string[] crewIds;
            public List<string> interactions = new List<string>();
            public List<string> errors = new List<string>();
            public List<Control> controls = new List<Control>();
        }
        [Serializable] private sealed class Control
        {
            public string name, label;
            public bool interactable;
            public Rect bounds;
        }
        private sealed class RectState
        {
            public Vector2 size, position, anchorMin, anchorMax, pivot;
            public Vector3 scale;
            public Quaternion rotation;
            public RectState(RectTransform rect)
            {
                size = rect.sizeDelta; position = rect.anchoredPosition;
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot;
                scale = rect.localScale; rotation = rect.localRotation;
            }
            public bool Matches(RectTransform rect) => size == rect.sizeDelta && position == rect.anchoredPosition &&
                anchorMin == rect.anchorMin && anchorMax == rect.anchorMax && pivot == rect.pivot &&
                scale == rect.localScale && rotation == rect.localRotation;
        }
        private sealed class PrefValue
        {
            public string key, text;
            public int number;
            public bool existed, isString;
        }

        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!project.Equals("D:/Game/tmp/departure-chart-inline-20261014", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This capture requires the task-owned isolated project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped Editor with graphics is required.");
            if (PlayerSettings.companyName != "DepartureCrewModalValidation20261014" ||
                PlayerSettings.productName != "DepartureCrewModalValidation20261014" ||
                Application.companyName != "DepartureCrewModalValidation20261014" ||
                Application.productName != "DepartureCrewModalValidation20261014")
                throw new InvalidOperationException("The isolated PlayerPrefs identity is not configured; run Prepare first.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-departureCrewModalOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -departureCrewModalOutput NEW_ABSOLUTE_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/DepartureCrewModal20261014/validation/", StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                throw new InvalidOperationException("Use a fresh task artifact directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (string state in new[] { "crew-default", "crew-many", "crew-scroll" }) Capture(report, output, state);
                report.passed = report.frames.Count == 3 && report.frames.All(frame => frame.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Crew modal capture failed; inspect report.json.");
            Debug.Log("DEPARTURE_CREW_MODAL_CAPTURE_OK: three verified 1920x1080 frames at " + output);
        }

        private static void Capture(Report report, string output, string state)
        {
            var frame = new Frame { state = state, file = state + "_1920x1080.png" };
            report.frames.Add(frame);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            List<PrefValue> preferences = null;
            try
            {
                preferences = InstallIsolatedRoster(state == "crew-default" ? 6 : 12);
                var host = new GameObject("Departure Crew Modal Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var gameplay = host.AddComponent<CoreGameplayDemoController>();
                gameplay.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                gameplay.Session.Cargo.Water = gameplay.Session.Cargo.Food = 80;
                Call(gameplay, "Render");
                Canvas.ForceUpdateCanvases();

                RawImage background = Find<RawImage>(host, "Voyage Background");
                Texture backgroundTexture = background.texture;
                Rect backgroundUv = background.uvRect;
                var backgroundState = new RectState(background.rectTransform);
                frame.background = backgroundTexture == null ? "missing" : backgroundTexture.name;
                var vesselState = new RectState(Find<RectTransform>(host, "Inspect Departure Vessel"));
                var shipState = new RectState(Find<RectTransform>(host, "Starter Modular Ship"));
                RawImage hullBefore = Find<RectTransform>(host, "Starter Modular Ship").GetComponentsInChildren<RawImage>(true).First();
                string hullName = hullBefore.name;
                Texture hullTexture = hullBefore.texture;
                var hullState = new RectState(hullBefore.rectTransform);
                Find<Button>(host, "Configure Voyage Crew").onClick.Invoke();
                frame.interactions.Add("Opened the production Configure Voyage Crew button above the unchanged dock scene.");
                Canvas.ForceUpdateCanvases();

                Button[] crewButtons = CrewButtons(host);
                frame.rosterCount = crewButtons.Length;
                frame.crewIds = crewButtons.Select(button => button.GetComponentsInChildren<Text>(true)
                    .Single(label => label.name == "Departure Special Crew Identity").text).ToArray();
                frame.selectedCount = gameplay.Session.SelectedSpecialCrewCount;
                frame.navigatorCount = gameplay.Session.NavigatorCount;
                if (state == "crew-default" && frame.rosterCount != 6) frame.errors.Add("Default preview must expose the six current-port sample crew.");
                if (state != "crew-default" && frame.rosterCount != 12) frame.errors.Add("Isolated fixture must expose all 12 recruited crew, including repeated professions.");
                if (frame.crewIds.Distinct().Count() != frame.rosterCount) frame.errors.Add("Duplicate identities appeared in the roster.");
                if (state != "crew-default" && frame.crewIds.Select(CrewNarrativeCatalog.FindById).Count(crew => crew.ProfessionIndex == 0) < 2)
                    frame.errors.Add("The expanded fixture did not contain multiple navigators.");
                if (state != "crew-default" && (frame.selectedCount <= 6 || frame.navigatorCount < 2))
                    frame.errors.Add("The fixture did not board more than six specialists and multiple navigators.");
                ScrollRect scroll = Find<ScrollRect>(host, "Departure Special Crew Scroll");
                if (state != "crew-default" && scroll.content.rect.height <= scroll.viewport.rect.height)
                    frame.errors.Add("The long roster has no scrollable content.");
                if (state == "crew-scroll")
                {
                    scroll.verticalNormalizedPosition = 0f;
                    frame.interactions.Add("Scrolled the production roster viewport to its last row.");
                }
                else if (scroll.content.rect.height > scroll.viewport.rect.height + .5f && scroll.verticalNormalizedPosition < .999f) frame.errors.Add("New modal did not open at the first row.");

                background = Find<RawImage>(host, "Voyage Background");
                frame.backgroundUnchanged = background.texture == backgroundTexture && background.uvRect == backgroundUv && backgroundState.Matches(background.rectTransform);
                if (!frame.backgroundUnchanged) frame.errors.Add("Opening the modal changed the dock background or its geometry.");
                RawImage hullAfter = Find<RawImage>(host, hullName);
                frame.vesselUnchanged = vesselState.Matches(Find<RectTransform>(host, "Inspect Departure Vessel")) &&
                    shipState.Matches(Find<RectTransform>(host, "Starter Modular Ship")) && hullState.Matches(hullAfter.rectTransform) && hullTexture == hullAfter.texture;
                if (!frame.vesselUnchanged) frame.errors.Add("Opening the modal moved or replaced the vessel.");
                foreach (string name in new[] { "Route Selection", "Voyage Setup" })
                {
                    RectTransform root = Find<RectTransform>(host, name);
                    if (root.sizeDelta != PhaseSurfaceLayout.ContentSize || root.anchoredPosition != Vector2.zero || root.GetComponent<Image>().color.a != 0f)
                        frame.errors.Add("Modal replaced the transparent dock scene layout: " + name);
                }
                RectTransform modal = Find<RectTransform>(host, "Departure Crew Configuration");
                RectTransform overlay = Find<RectTransform>(host, "Departure Crew Overlay");
                if (modal.parent != overlay || !overlay.GetComponent<Image>().raycastTarget)
                    frame.errors.Add("Modal is not above an input-blocking overlay.");

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production scaler must remain enabled.");
                var cameraHost = new GameObject("Departure Crew Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << 31;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    scroll.verticalNormalizedPosition = state == "crew-scroll" ? 0f : 1f;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.scaleFactor = canvas.scaleFactor;
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled && Mathf.Abs(frame.renderingDisplaySize.x - frame.width) < .5f && Mathf.Abs(frame.renderingDisplaySize.y - frame.height) < .5f &&
                    Mathf.Abs(frame.canvasPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.canvasPixelRect.height - frame.height) < .5f &&
                    Mathf.Abs(frame.cameraPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.cameraPixelRect.height - frame.height) < .5f &&
                    Mathf.Abs(frame.scaleFactor - frame.expectedScaleFactor) < .001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x - frame.width / frame.scaleFactor) < 1 && Mathf.Abs(frame.logicalCanvasSize.y - frame.height / frame.scaleFactor) < 1;
                if (!frame.verifiedScaler) frame.errors.Add("Render target did not drive the production scaler.");
                frame.modalBounds = Bounds(modal, camera);
                frame.viewportBounds = Bounds(scroll.viewport, camera);
                frame.vesselBounds = Bounds(Find<RectTransform>(host, "Inspect Departure Vessel"), camera);
                frame.scrollPosition = scroll.verticalNormalizedPosition;
                if (frame.modalBounds.xMin < 0 || frame.modalBounds.yMin < 0 || frame.modalBounds.xMax > frame.width || frame.modalBounds.yMax > frame.height)
                    frame.errors.Add("Crew modal lies outside the output frame.");
                if (state == "crew-scroll" && (frame.scrollPosition > .001f || !Bounds(crewButtons.Last().GetComponent<RectTransform>(), camera).Overlaps(frame.viewportBounds)))
                    frame.errors.Add("The final crew row is not visible at the scroll endpoint.");
                foreach (Selectable control in canvas.GetComponentsInChildren<Selectable>(false))
                    frame.controls.Add(new Control { name = control.name, label = control.GetComponentInChildren<Text>()?.text,
                        bounds = Bounds(control.GetComponent<RectTransform>(), camera), interactable = control.IsInteractable() });
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
                frame.passed = frame.errors.Count == 0;
            }
            catch (Exception exception) { frame.errors.Add(exception.ToString()); frame.passed = false; }
            finally
            {
                try
                {
                    RenderTexture.active = previous;
                    if (pixels != null) Object.DestroyImmediate(pixels);
                    EditorSceneManager.ClosePreviewScene(scene);
                    if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                }
                finally { RestoreIsolatedRoster(preferences); }
            }
        }

        private static List<PrefValue> InstallIsolatedRoster(int count)
        {
            if (Application.companyName != "DepartureCrewModalValidation20261014" ||
                Application.productName != "DepartureCrewModalValidation20261014")
                throw new InvalidOperationException("Refusing crew fixture outside the validation PlayerPrefs identity.");
            var saved = new List<PrefValue>();
            void Remember(string key, bool text)
            {
                saved.Add(new PrefValue { key = key, existed = PlayerPrefs.HasKey(key), isString = text,
                    number = text ? 0 : PlayerPrefs.GetInt(key), text = text ? PlayerPrefs.GetString(key) : null });
            }
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                Remember(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), false);
                Remember(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id), true);
            }
            for (int profession = 0; profession < CrewNarrativeCatalog.ProfessionCount; profession++)
                Remember(CoreGameplayDemoController.CrewRecruitmentKey(profession), false);
            try
            {
                foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                    PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id));
                for (int profession = 0; profession < CrewNarrativeCatalog.ProfessionCount; profession++)
                    PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewRecruitmentKey(profession));
                foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew.Take(count))
                {
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
                    PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id), "断锚港");
                }
                return saved;
            }
            catch { RestoreIsolatedRoster(saved); throw; }
        }
        private static void RestoreIsolatedRoster(List<PrefValue> saved)
        {
            if (saved == null) return;
            foreach (PrefValue value in saved)
            {
                if (!value.existed) PlayerPrefs.DeleteKey(value.key);
                else if (value.isString) PlayerPrefs.SetString(value.key, value.text);
                else PlayerPrefs.SetInt(value.key, value.number);
            }
        }
        private static Button[] CrewButtons(GameObject host) => host.GetComponentsInChildren<Button>(false)
            .Where(button => button.name.StartsWith("Departure Special Crew Toggle "))
            .OrderBy(button => int.Parse(button.name.Substring("Departure Special Crew Toggle ".Length))).ToArray();
        private static void Call(object instance, string method) => instance.GetType().GetMethod(method, PrivateInstance).Invoke(instance, null);
        private static T Find<T>(GameObject host, string name) where T : Component => host.GetComponentsInChildren<T>(true)
            .Single(item => item.name == name && item.gameObject.activeInHierarchy);
        private static Rect Bounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
