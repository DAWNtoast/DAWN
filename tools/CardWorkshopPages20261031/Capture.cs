// Installed only into the borrowed validation project. No production scene or save writes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public static class CardWorkshopPagesCapture20261031
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Pages = { "Collection", "DeckBuilder", "Packs", "Upgrade" };
        private static readonly string[] Labels = { "查看已有卡牌", "组建牌组", "打开卡包", "卡牌强化" };
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string project;
            public string scope = "Actual production workshop pages in disposable Editor PreviewScenes, production CanvasScaler enabled, no saved scenes or player progress writes. Navigation and incomplete-deck fixtures remain in memory.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string page, file, error, headerTexture;
            public int width, height;
            public bool verifiedScaler, verifiedSettings, verifiedNavigation, verifiedReadOnly, verifiedIncompleteDeckGuard;
            public float scaleFactor;
            public Vector2 displaySize;
            public List<Element> elements = new List<Element>();
        }
        [Serializable] public sealed class Element
        {
            public string name, text;
            public bool interactable;
            public Rect screenBounds;
        }
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.Equals("D:/Game/tmp/shipyard-assembly-20261017", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the assigned isolated validation project.");
            string[] args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-workshopPagesOutput");
            if (option < 0 || option + 1 >= args.Length || !Path.IsPathRooted(args[option + 1]))
                throw new ArgumentException("Pass -workshopPagesOutput ABSOLUTE_DIRECTORY.");
            string output = Path.GetFullPath(args[option + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/outputs/card-workshop-pages-20261031/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output escaped task evidence directory.");
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFiles(output, "*.png").Any() || File.Exists(Path.Combine(output, "report.json")))
                throw new IOException("Preserve previous evidence; choose a new run.");
            var report = new Report { project = project };
            foreach (int width in new[] { 1920, 1280 })
                foreach (string page in Pages)
                    report.frames.Add(new Frame { page = page, width = width, height = width * 9 / 16,
                        file = page + "_" + width + "x" + width * 9 / 16 + ".png" });
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); Debug.Log("WORKSHOP_PAGES_OK: " + frame.file); }
                catch (Exception error)
                {
                    while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                    frame.error = error.ToString(); Debug.LogWarning("WORKSHOP_PAGES_FAILED: " + frame.file + " " + error.Message);
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            int failed = report.frames.Count(frame => !string.IsNullOrEmpty(frame.error));
            Debug.Log("WORKSHOP_PAGES_COMPLETE: " + (report.frames.Count - failed) + " / " + report.frames.Count);
            if (failed > 0) throw new InvalidOperationException("Workshop page verification failed; inspect report.json.");
        }
        private static void Set(CoreGameplayDemoController controller, string field, object value)
        {
            FieldInfo target = typeof(CoreGameplayDemoController).GetField(field, Private);
            if (target == null) throw new MissingFieldException(field);
            target.SetValue(controller, value);
        }
        private static void Invoke(CoreGameplayDemoController controller, string method)
            => typeof(CoreGameplayDemoController).GetMethod(method, Private).Invoke(controller, null);
        private static Button Button(GameObject host, string name)
            => host.GetComponentsInChildren<Button>(false).Single(button => button.name == name);
        private static bool Exists(GameObject host, string name)
            => host.GetComponentsInChildren<Transform>(false).Any(item => item.name == name);
        private static void Click(GameObject host, string name)
        {
            Button button = Button(host, name);
            if (!button.interactable) throw new InvalidOperationException("Expected enabled button: " + name);
            button.onClick.Invoke();
        }
        private static void VerifyNavigation(CoreGameplayDemoController controller, GameObject host, Frame frame)
        {
            decimal gold = controller.Session.ProfileGold;
            int[][] decks = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Select(stage => controller.Session.GetBattleDeck(stage).ToArray()).ToArray();
            string upgrades = string.Join(";", controller.Session.CardUpgradeLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            for (int i = 0; i < Pages.Length; i++)
            {
                Click(host, "Workshop Page " + Pages[i]);
                foreach (string page in Pages) Button(host, "Workshop Page " + page);
                for (int label = 0; label < Pages.Length; label++)
                    if (!Button(host, "Workshop Page " + Pages[label]).GetComponentsInChildren<Text>().Any(text => text.text == Labels[label]))
                        throw new InvalidOperationException("Unexpected workshop navigation label: " + Pages[label]);
                if (Pages[i] == "DeckBuilder" && !Exists(host, "Deck Builder Panel"))
                    throw new InvalidOperationException("Deck page did not open the production deck editor.");
                if (Pages[i] == "Upgrade" && !Exists(host, "Battle Card Upgrade Panel"))
                    throw new InvalidOperationException("Upgrade page did not open the production upgrade comparison.");
                if (Pages[i] == "Packs")
                {
                    if (!host.GetComponentsInChildren<Text>(false).Any(text => text.text.Contains("暂无") && text.text.Contains("卡包")))
                        throw new InvalidOperationException("Pack page needs its explicit empty state.");
                    if (Exists(host, "Deck Builder Panel") || Exists(host, "Battle Card Upgrade Panel"))
                        throw new InvalidOperationException("Pack page leaked another page's content.");
                }
            }
            frame.verifiedNavigation = true;
            int stageIndex = 0;
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                if (!decks[stageIndex++].SequenceEqual(controller.Session.GetBattleDeck(stage)))
                    throw new InvalidOperationException("Changing workshop pages modified a deck.");
            frame.verifiedReadOnly = gold == controller.Session.ProfileGold && upgrades == string.Join(";", controller.Session.CardUpgradeLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            if (!frame.verifiedReadOnly) throw new InvalidOperationException("Changing pages modified gold or upgrades.");

            Click(host, "Workshop Page DeckBuilder");
            NavalBattleStage draftStage = (NavalBattleStage)typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage", Private).GetValue(controller);
            Set(controller, "deckBuilderDraft", controller.Session.GetBattleDeck(draftStage).Take(CoreVoyageSession.MinimumBaseBattleDeckSize - 1).ToList());
            Set(controller, "deckBuilderDraftSession", controller.Session);
            Set(controller, "deckBuilderDraftStage", draftStage);
            Set(controller, "deckBuilderDraftPort", controller.Session.CurrentPort);
            Invoke(controller, "Render");
            // Invoke disabled callbacks too: stale/programmatic clicks must respect the same draft guard.
            foreach (string page in new[] { "Collection", "Packs", "Upgrade" })
            {
                Button(host, "Workshop Page " + page).onClick.Invoke();
                if (!Exists(host, "Deck Builder Panel")) throw new InvalidOperationException("Incomplete deck escaped through " + page);
            }
            frame.verifiedIncompleteDeckGuard = true;
            Invoke(controller, "ClearDeckBuilderDraft");
            Invoke(controller, "Render");
            Click(host, "Workshop Page " + frame.page);
        }
        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new Rect(lower, upper - lower);
        }
        private static void Capture(string output, Frame frame)
        {
            Scene scene = default; RenderTexture target = null, previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Workshop Pages Capture"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.BrokenAnchor, 1);
                host.SetActive(true);
                // Keep all editor-mode mutation guards enabled while exercising production callbacks.
                Set(controller, "editorPreviewBuilding", true);
                VerifyNavigation(controller, host, frame);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler disabled.");
                var cameraHost = new GameObject("Disposable Workshop Pages Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 5; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (WorkshopResponsiveSurface surface in host.GetComponentsInChildren<WorkshopResponsiveSurface>(false)) surface.ApplyLayout();
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.displaySize = canvas.renderingDisplaySize; frame.scaleFactor = canvas.scaleFactor;
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                float expected = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) : scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) : Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = Mathf.Abs(frame.displaySize.x - frame.width) < .5f && Mathf.Abs(frame.displaySize.y - frame.height) < .5f && Mathf.Abs(canvas.scaleFactor - expected) < .001f;
                if (!frame.verifiedScaler) throw new InvalidOperationException("Render target did not drive the production CanvasScaler.");
                Button settings = canvas.GetComponentsInChildren<Button>(false).Single(button => button.name.EndsWith("Settings", StringComparison.Ordinal));
                RectTransform settingsRect = (RectTransform)settings.transform;
                frame.verifiedSettings = Mathf.Abs(settingsRect.rect.width - NauticalHeaderSkin.IconSize) < .01f && Mathf.Abs(settingsRect.rect.height - NauticalHeaderSkin.IconSize) < .01f && Mathf.Abs(settingsRect.anchoredPosition.x + NauticalHeaderSkin.SettingsRightOffset) < .01f && Mathf.Abs(settingsRect.anchoredPosition.y + NauticalHeaderSkin.SettingsTopOffset) < .01f;
                if (!frame.verifiedSettings) throw new InvalidOperationException("Shared 96x96 header settings geometry changed.");
                NauticalHeaderGraphic wood = canvas.GetComponentsInChildren<NauticalHeaderGraphic>(false).Single(graphic => graphic.enabled && graphic.name == "Header Brass Rules");
                frame.headerTexture = AssetDatabase.GetAssetPath(wood.mainTexture);
                if (string.IsNullOrEmpty(frame.headerTexture) || wood.raycastTarget) throw new InvalidOperationException("Regional header texture or input passthrough failed.");
                foreach (Button button in canvas.GetComponentsInChildren<Button>(false))
                {
                    var bounds = ScreenBounds((RectTransform)button.transform, camera);
                    frame.elements.Add(new Element { name = button.name, text = string.Join(" | ", button.GetComponentsInChildren<Text>().Select(text => text.text)), interactable = button.interactable, screenBounds = bounds });
                    if (button.name.StartsWith("Workshop Page ", StringComparison.Ordinal) && (bounds.xMin < 0 || bounds.xMax > frame.width + .5f || bounds.yMin < 0 || bounds.yMax > frame.height + .5f))
                        throw new InvalidOperationException("Workshop navigation escaped viewport: " + button.name);
                }
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < samples.Length; i += 997) { int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3; low = Math.Min(low, brightness); high = Math.Max(high, brightness); }
                if (high - low < 20) throw new InvalidOperationException("Nearly blank frame.");
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }
    }
}
