// Editor-only audit entry. Copy into an isolated validation project's Editor folder.
// Captures disposable previews only. No saved-scene creation or synchronization.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    public static class SettlementRedesignCapture
    {
        private const int Layer = 31;

        // -executeMethod ShatteredSail.Editor.SettlementRedesignCapture.CaptureFromCommandLine
        // -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR (below an Artifacts directory); omit -nographics.
        public static void CaptureFromCommandLine()
        {
            RequireStoppedEditor();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string output = Output();
            var report = new CaptureReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "27 disposable production settlement renders: arrival sample 0, featured wood sample 4, failure sample 2, " +
                    "large rewards sample 5, safe return, empty voyage, treasure and tutorial fixtures, and isolated +10 gold/zero materials first voyage. " +
                    "The first-voyage fixture is presentation data, not earned loot. BuildSettlementPresentation is called with previewSample=true. " +
                    "No gameplay callbacks, PlayMode, scene writes or player-save writes. Enabled production CanvasScaler and actual target geometry are verified. " +
                    "Text mesh bounds are rendered vertex bounds in target pixels. Existing tutorial plaque text alone permits 3px horizontal glyph bearing; settlement text uses 1px bounds."
            };
            foreach (string state in new[] { "Arrival", "SafeReturn", "Empty", "Materials", "Failure", "LargeValue", "FirstVoyage", "Treasure", "Tutorial" })
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) })
                {
                    var frame = new CaptureFrame { state = state, width = size.x, height = size.y, file = state + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { Capture(output, frame); }
                    catch (Exception exception) { frame.error = exception.ToString(); Debug.LogWarning(frame.file + ": " + exception.Message); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                }
            WriteTextReport(output, report);
            if (report.frames.Count != 27 || report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Settlement alignment capture failed; inspect report.json.");
            Debug.Log("SETTLEMENT_REDESIGN_CAPTURE_OK: 27 verified production captures at " + output);
        }

        private static VoyageSettlement Fixture(string state)
        {
            if (state == "FirstVoyage")
                return new VoyageSettlement
                {
                    Outcome = VoyageOutcome.SafeReturn, Title = "安全返航", Destination = "断锚港",
                    ProtectionRatio = .85f, DistanceNm = 360, Hours = 23.1, Coverage = 19,
                    CompletedEvents = 3, TotalEvents = 3, BankedGold = 10,
                    BankedWood = 0, BankedIron = 0, BankedFame = 0, ChartSaved = false
                };
            int scenario = state == "Materials" ? 4 : state == "Failure" ? 2 : state == "LargeValue" ? 5 : state == "SafeReturn" ? 1 : state == "Empty" ? 3 : 0;
            var result = SettlementPreviewSamples.Create(scenario);
            if (state == "Treasure")
            {
                result.Outcome = VoyageOutcome.TreasureRecovered; result.Title = "秘藏回收"; result.TreasureCompleted = true;
                result.BankedGem = 12; result.BankedAncientMaterial = 8; result.BankedRelic = 3; result.BankedBlackPearl = 2;
                result.BankedFragments = 2; result.BankedPirateExperience = 125;
            }
            if (state == "Tutorial") { result.BankedFragments = 1; result.BankedPirateExperience = 15; }
            return result;
        }

        private static void Capture(string output, CaptureFrame frame)
        {
            Scene scene = default;
            RenderTexture rt = null, previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable Settlement Alignment Audit");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || !scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                Transform content = FixedGameViewport.Bounds(canvas).Find("Phase Content");
                if (content == null) throw new InvalidOperationException("Missing production Phase Content.");
                foreach (Transform child in content.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                decimal goldBefore = controller.Session.ProfileGold;
                CoreDemoPhase phaseBefore = controller.Session.Phase;
                VoyageSettlement result = Fixture(frame.state);
                int[] rewardsBefore = { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame };
                MethodInfo builder = typeof(CoreGameplayDemoController).GetMethod("BuildSettlementPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (builder == null) throw new MissingMethodException("CoreGameplayDemoController.BuildSettlementPresentation");
                if (frame.state == "Tutorial")
                {
                    typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, true);
                    typeof(CoreVoyageSession).GetProperty("GuidedTutorialLeg").GetSetMethod(true).Invoke(controller.Session, new object[] { CoreGuidedTutorialContent.BossVoyageLeg });
                    typeof(CoreVoyageSession).GetProperty("GuidedTutorialBossDefeated").GetSetMethod(true).Invoke(controller.Session, new object[] { true });
                }
                builder.Invoke(controller, new object[] { result, true });
                if (frame.state == "Tutorial")
                {
                    Transform hero = host.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Settlement Hero");
                    typeof(CoreGameplayDemoController).GetMethod("BuildGuidedTutorialRewardPlaque", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(controller, new object[] { hero, new Vector2(0, -55) });
                }
                host.GetComponentsInChildren<SettlementRevealController>(true).Single().CompleteImmediately();
                if (controller.Session.ProfileGold != goldBefore || controller.Session.Phase != phaseBefore ||
                    !rewardsBefore.SequenceEqual(new[] { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame }))
                    throw new InvalidOperationException("The disposable presentation mutated its settlement/profile.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                GameObject cameraHost = new GameObject("Disposable Settlement Alignment Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                rt = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create(); camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                // Preserve production scaling; never assign canvas scaleFactor, size or localScale.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                    else camera.Render();
                }
                VerifyScaler(canvas, scaler, camera, frame);
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                    var geometry = new TextGeometry
                    {
                        path = PathOf(label.transform, canvas.transform), content = label.text, fontSize = label.fontSize,
                        alignment = label.alignment.ToString(), alignByGeometry = label.alignByGeometry,
                        authoredPosition = label.rectTransform.anchoredPosition, authoredSize = label.rectTransform.sizeDelta
                    };
                    geometry.rectPixels = ScreenBounds(label.rectTransform, camera);
                    geometry.parentPixels = ScreenBounds(label.transform.parent as RectTransform, camera);
                    geometry.rectOutsideTarget = Outside(geometry.rectPixels, frame.width, frame.height);
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    geometry.meshVertexCount = mesh == null ? 0 : mesh.vertexCount;
                    geometry.meshAvailable = geometry.meshVertexCount > 0;
                    if (geometry.meshAvailable)
                    {
                        geometry.meshPixels = Bounds(mesh.vertices.Select(vertex => (Vector2)camera.WorldToScreenPoint(label.transform.TransformPoint(vertex))));
                        geometry.meshOutsideTarget = Outside(geometry.meshPixels, frame.width, frame.height);
                        geometry.meshCenterOffsetFromTextRect = geometry.meshPixels.center - geometry.rectPixels.center;
                        geometry.meshCenterOffsetFromParent = geometry.meshPixels.center - geometry.parentPixels.center;
                    }
                    // CanvasRenderer owns the mesh returned by GetMesh().
                    frame.text.Add(geometry);
                }
                foreach (TextGeometry label in frame.text)
                {
                    if (!label.meshAvailable || label.meshOutsideTarget || label.rectOutsideTarget)
                        throw new InvalidOperationException("Missing or off-screen text: " + label.path);
                    float glyphTolerance = label.path.EndsWith("Guided Tutorial Reward Text", StringComparison.Ordinal) ? 3f : 1f;
                    if (label.meshPixels.xMin < label.rectPixels.xMin - glyphTolerance || label.meshPixels.xMax > label.rectPixels.xMax + glyphTolerance ||
                        label.meshPixels.yMin < label.rectPixels.yMin - 1f || label.meshPixels.yMax > label.rectPixels.yMax + 1f)
                        throw new InvalidOperationException("Text exceeds its authored rectangle: " + label.path);
                    if (!label.path.Contains("Guided Tutorial") && (Mathf.Abs(label.meshCenterOffsetFromTextRect.y) > 1f ||
                        (label.alignment == "MiddleCenter" && Mathf.Abs(label.meshCenterOffsetFromTextRect.x) > 1f)))
                        throw new InvalidOperationException("Visible text is not centered: " + label.path);
                    if (label.content.Contains("船体修缮") || label.content.Contains("铸造储备") || label.content.Contains("远航声名"))
                        throw new InvalidOperationException("Removed resource hint is still visible: " + label.path);
                }
                RenderTexture.active = rt;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < samples.Length; i += 997)
                {
                    int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3;
                    low = Math.Min(low, brightness); high = Math.Max(high, brightness);
                }
                if (high - low < 20) throw new InvalidOperationException("Nearly blank frame; capture refused.");
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
                // Inspect finite sparkle clipping on full and shorter reward portraits.
                // The completed state above owns the strict text checks; intermediate
                // reveal elements intentionally have partial opacity and scale.
                if (frame.width == 1920 && (frame.state == "Arrival" || frame.state == "Treasure" || frame.state == "Tutorial"))
                {
                    host.GetComponentsInChildren<SettlementRevealController>(true).Single().SetElapsed(.95f);
                    for (int pass = 0; pass < 4; pass++)
                    {
                        Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                        foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                        Canvas.ForceUpdateCanvases();
                        if (GraphicsSettings.currentRenderPipeline != null)
                            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                        else camera.Render();
                    }
                    RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                    frame.revealFile = frame.state + "_Reveal095_1920x1080.png";
                    File.WriteAllBytes(Path.Combine(output, frame.revealFile), pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }

        private static void VerifyScaler(Canvas canvas, CanvasScaler scaler, Camera camera, CaptureFrame frame)
        {
            frame.renderingDisplaySize = canvas.renderingDisplaySize; frame.canvasPixelRect = canvas.pixelRect;
            frame.cameraPixelRect = camera.pixelRect; frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
            frame.scaleFactor = canvas.scaleFactor; frame.screenMatchMode = scaler.screenMatchMode.ToString();
            float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
            frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) :
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) :
                Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
            frame.verifiedScaler = scaler.enabled &&
                Mathf.Abs(frame.renderingDisplaySize.x - frame.width) < .5f && Mathf.Abs(frame.renderingDisplaySize.y - frame.height) < .5f &&
                Mathf.Abs(frame.canvasPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.canvasPixelRect.height - frame.height) < .5f &&
                Mathf.Abs(frame.cameraPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.cameraPixelRect.height - frame.height) < .5f &&
                Mathf.Abs(frame.scaleFactor - frame.expectedScaleFactor) < .001f &&
                Mathf.Abs(frame.logicalCanvasSize.x - frame.width / frame.scaleFactor) < 1f &&
                Mathf.Abs(frame.logicalCanvasSize.y - frame.height / frame.scaleFactor) < 1f;
            if (!frame.verifiedScaler)
                throw new InvalidOperationException("Target texture did not drive the actual scaler/display geometry; capture refused.");
        }

        private static string Output()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Output must be below an Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use a new or empty output directory.");
            Directory.CreateDirectory(output); return output;
        }
        private static void RequireStoppedEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped Editor required.");
        }
        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>(); for (Transform t = item; t != null && t != root; t = t.parent) names.Insert(0, t.name); return string.Join("/", names);
        }
        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            if (rect == null) return default;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); return Bounds(corners.Select(point => (Vector2)camera.WorldToScreenPoint(point)));
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] array = points.ToArray(); return Rect.MinMaxRect(array.Min(point => point.x), array.Min(point => point.y), array.Max(point => point.x), array.Max(point => point.y));
        }
        private static bool Outside(Rect rect, int width, int height) => rect.xMin < -.5f || rect.yMin < -.5f || rect.xMax > width + .5f || rect.yMax > height + .5f;
        private static void WriteTextReport(string output, CaptureReport report)
        {
            var text = new StringBuilder(report.scope).AppendLine();
            foreach (CaptureFrame frame in report.frames)
            {
                text.AppendLine(frame.file + " scaler=" + frame.verifiedScaler + " error=" + (frame.error ?? ""));
                foreach (TextGeometry label in frame.text)
                    text.AppendLine(label.path + " | text=" + label.content.Replace("\n", "\\n") + " | mesh=" + label.meshPixels +
                        " | center offset/text=" + label.meshCenterOffsetFromTextRect + " | center offset/parent=" + label.meshCenterOffsetFromParent);
            }
            File.WriteAllText(Path.Combine(output, "text-geometry.txt"), text.ToString(), new UTF8Encoding(false));
        }
        [Serializable] private sealed class CaptureReport
        {
            public string capturedAtUtc, scope; public List<CaptureFrame> frames = new List<CaptureFrame>();
        }
        [Serializable] private sealed class CaptureFrame
        {
            public string state, file, revealFile, error, screenMatchMode; public int width, height; public bool verifiedScaler;
            public Vector2 renderingDisplaySize, logicalCanvasSize; public Rect canvasPixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor; public List<TextGeometry> text = new List<TextGeometry>();
        }
        [Serializable] private sealed class TextGeometry
        {
            public string path, content, alignment; public int fontSize, meshVertexCount;
            public bool alignByGeometry, meshAvailable, meshOutsideTarget, rectOutsideTarget;
            public Vector2 authoredPosition, authoredSize, meshCenterOffsetFromTextRect, meshCenterOffsetFromParent;
            public Rect rectPixels, parentPixels, meshPixels;
        }
    }
}
#endif
