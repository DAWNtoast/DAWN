// Temporary acceptance tool: copy only into an isolated D:/Game/tmp/.../Assets/_Project/Editor project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    public static class EnhancedBackgroundCapture
    {
        private const int Width = 3840, Height = 2160, Layer = 30;
        private const string OutputVariable = "BACKGROUND_CLARITY_CAPTURE_DIR";
        private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] Ports = { "BrokenAnchor", "WhiteShell", "BronzeBell", "SaltCrown", "TwilightTide", "GrayGull" };
        private static readonly string[] Slugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };

        [Serializable] public sealed class Report
        {
            public string status = "running", startedUtc, completedUtc, projectPath, outputPath, unityVersion;
            public string scope = "19 production UI frames: Home once, PortHub/Warehouse/Tavern at all six ports; native 3840x2160 GPU render/readback. Imported texture, desktop importer, full-image Sprite and visible background measured. No source, scene or player-save writes; visual composition still requires review.";
            public int expectedFrames = 19, capturedFrames, passedFrames;
            public List<Frame> frames = new List<Frame>();
            public List<string> errors = new List<string>();
        }

        [Serializable] public sealed class Frame
        {
            public string state, port, resource, file, status = "running", screenshotSha256;
            public int width = Width, height = Height;
            public float canvasScale;
            public Rect canvasPixelRect;
            public Vector2 referenceResolution;
            public bool scalerVerified, imageHasContent, captured;
            public TextureCheck texture;
            public List<BackgroundGraphic> backgrounds = new List<BackgroundGraphic>();
            public List<string> errors = new List<string>();
        }

        [Serializable] public sealed class TextureCheck
        {
            public string assetPath, guid, sourceSha256, metaSha256, format, graphicsFormat, filter, textureType, spriteMode;
            public int sourceWidth, sourceHeight, width, height, mipmapCount, defaultMaxSize, standaloneMaxSize, effectiveMaxSize;
            public bool readable, standaloneOverride, isBC7, sourceIs4K, importedIs4K, sourceUnchangedDuringCapture;
            public int spriteCount;
            public bool expectsFullImageSprite, fullImageSpritesVerified;
            public List<SpriteCheck> sprites = new List<SpriteCheck>();
        }

        [Serializable] public sealed class SpriteCheck
        {
            public string name, guid;
            public long localId;
            public Rect rect;
            public Vector2 pivot;
            public Vector4 border;
            public bool fullImage, sameTexture;
        }

        [Serializable] public sealed class BackgroundGraphic
        {
            public string path, type;
            public Rect screenRect, uv;
            public float availableWidth, availableHeight, upscale;
            public bool culled;
        }

        // -executeMethod ShatteredSail.Editor.EnhancedBackgroundCapture.Run
        // Required environment: BACKGROUND_CLARITY_CAPTURE_DIR=<new absolute output directory>
        // Graphics must remain enabled: no -nographics. This method never starts Play mode.
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only an isolated project under D:/Game/tmp/ is supported.");
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use a stopped, non-Play editor.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("GPU capture is required; omit -nographics.");
            string requested = Environment.GetEnvironmentVariable(OutputVariable);
            if (string.IsNullOrWhiteSpace(requested) || !Path.IsPathRooted(requested))
                throw new ArgumentException(OutputVariable + " must specify a new absolute output directory.");
            string output = Path.GetFullPath(requested).Replace('\\', '/').TrimEnd('/');
            if (output.Equals(project, StringComparison.OrdinalIgnoreCase) || output.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase) ||
                output.Equals("D:/Game/UnityClient", StringComparison.OrdinalIgnoreCase) || output.StartsWith("D:/Game/UnityClient/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Capture output must be outside the Unity projects.");
            if (output.IndexOf("/ResolutionUpgradeAudit20261013", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Do not write into the previous resolution-audit evidence directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Output must be new or empty: " + output);
            Directory.CreateDirectory(output);

            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), projectPath = project,
                outputPath = output, unityVersion = Application.unityVersion };
            AddFrame(report, "Home", "GrayGull", "MainMenu/main_menu_background");
            foreach (string state in new[] { "PortHub", "Warehouse", "Tavern" })
                for (int i = 0; i < Ports.Length; i++)
                    AddFrame(report, state, Ports[i], state == "PortHub" ? "PortArt/port_hub_" + Slugs[i] + "_v3" :
                        "PortFacilities/Regional/" + state + "/" + state.ToLowerInvariant() + "_" + Slugs[i] + "_v1");
            SaveReport(output, report);
            try
            {
                EnsurePipeline();
                foreach (Frame frame in report.frames)
                {
                    try { Capture(project, output, frame); }
                    catch (Exception error) { frame.errors.Add(error.ToString()); }
                    frame.status = frame.errors.Count == 0 && frame.captured ? "passed" : "failed";
                    if (frame.captured) report.capturedFrames++;
                    if (frame.status == "passed") report.passedFrames++;
                    else report.errors.Add(frame.state + "/" + frame.port + ": " + string.Join(" | ", frame.errors));
                    SaveReport(output, report);
                }
                if (report.capturedFrames != 19) report.errors.Add("Expected all 19 full-resolution screenshots.");
                report.status = report.errors.Count == 0 && report.passedFrames == 19 ? "passed" : "failed";
            }
            catch (Exception error) { report.errors.Add(error.ToString()); report.status = "failed"; }
            finally
            {
                report.completedUtc = DateTime.UtcNow.ToString("O");
                SaveReport(output, report);
            }
            Debug.Log("ENHANCED_BACKGROUND_CAPTURE " + report.status + " captured=" + report.capturedFrames + " passed=" + report.passedFrames + " output=" + output);
            if (report.status != "passed") throw new InvalidOperationException("Enhanced background acceptance failed; inspect " + output + "/report.json");
        }

        private static void AddFrame(Report report, string state, string port, string resource)
        {
            report.frames.Add(new Frame { state = state, port = port, resource = resource,
                file = (report.frames.Count + 1).ToString("D2") + "-" + state + "-" + port + "-3840x2160.png" });
        }

        private static void Capture(string project, string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previousActive = RenderTexture.active;
            Texture2D background = null;
            try
            {
                frame.texture = InspectTexture(project, frame, out background);
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Enhanced Background " + frame.state) { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                bool menu = frame.state == "Home";
                if (menu) host.AddComponent<MainMenuController>();
                else host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(
                    (CoreGameplayPreviewScreen)Enum.Parse(typeof(CoreGameplayPreviewScreen), frame.state),
                    (CoreGameplayPreviewPort)Enum.Parse(typeof(CoreGameplayPreviewPort), frame.port), 0);
                host.SetActive(true);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name ==
                    (menu ? MainMenuController.EditorPreviewCanvasName : CoreGameplayDemoController.EditorPreviewCanvasName));
                if (menu)
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                        if (child.name == "Settings Panel" || child.name == "Achievements Panel" || child.name == "New Game Confirmation")
                            child.gameObject.SetActive(false);
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;

                var cameraHost = new GameObject("Background Acceptance Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0, 0, -1000f); camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                if (!target.Create()) throw new InvalidOperationException("Render target allocation failed.");
                camera.targetTexture = target; camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || !scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unavailable.");
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(false)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.canvasScale = canvas.scaleFactor; frame.canvasPixelRect = canvas.pixelRect;
                frame.referenceResolution = scaler.referenceResolution;
                frame.scalerVerified = Mathf.Abs(canvas.scaleFactor - 2f) < .001f &&
                    Mathf.Abs(canvas.pixelRect.width - Width) < .5f && Mathf.Abs(canvas.pixelRect.height - Height) < .5f &&
                    Mathf.Abs(canvas.renderingDisplaySize.x - Width) < .5f && Mathf.Abs(canvas.renderingDisplaySize.y - Height) < .5f;
                Check(frame, frame.scalerVerified, "The production canvas did not render at 3840x2160, scale 2.");
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(false))
                {
                    if (!graphic.enabled || graphic.mainTexture != background || graphic.color.a <= .001f || graphic.canvasRenderer.GetInheritedAlpha() <= .001f) continue;
                    Mesh mesh = graphic.canvasRenderer.GetMesh();
                    if (mesh == null || mesh.vertexCount < 3) continue;
                    Rect pixels = Bounds(mesh.vertices.Select(v => (Vector2)camera.WorldToScreenPoint(graphic.transform.TransformPoint(v))));
                    if (!pixels.Overlaps(new Rect(0, 0, Width, Height))) continue;
                    Rect uv = Bounds(mesh.uv);
                    float availableWidth = background.width * Mathf.Abs(uv.width), availableHeight = background.height * Mathf.Abs(uv.height);
                    frame.backgrounds.Add(new BackgroundGraphic { path = PathOf(graphic.transform, canvas.transform), type = graphic.GetType().Name,
                        screenRect = pixels, uv = uv, availableWidth = availableWidth, availableHeight = availableHeight,
                        upscale = Mathf.Max(pixels.width / Mathf.Max(1f, availableWidth), pixels.height / Mathf.Max(1f, availableHeight)), culled = graphic.canvasRenderer.cull });
                }
                Check(frame, frame.backgrounds.Any(b => !b.culled), "Expected background is not rendered in the production UI.");
                RenderTexture.active = target;
                SaveScreenshot(output, frame);
                frame.texture.sourceUnchangedDuringCapture = Hash(Path.Combine(project, frame.texture.assetPath)) == frame.texture.sourceSha256;
                Check(frame, frame.texture.sourceUnchangedDuringCapture, "Source PNG changed while capturing this frame.");
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static TextureCheck InspectTexture(string project, Frame frame, out Texture2D texture)
        {
            string asset = "Assets/_Project/Resources/" + frame.resource + ".png";
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
            if (texture == null) throw new FileNotFoundException("Background texture not imported: " + asset);
            var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("TextureImporter missing: " + asset);
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            var row = new TextureCheck { assetPath = asset, guid = AssetDatabase.AssetPathToGUID(asset),
                sourceSha256 = Hash(Path.Combine(project, asset)), metaSha256 = Hash(Path.Combine(project, asset + ".meta")),
                sourceWidth = sourceWidth, sourceHeight = sourceHeight, width = texture.width, height = texture.height,
                sourceIs4K = sourceWidth == Width && sourceHeight == Height, importedIs4K = texture.width == Width && texture.height == Height,
                format = texture.format.ToString(), graphicsFormat = texture.graphicsFormat.ToString(), isBC7 = texture.format == TextureFormat.BC7,
                readable = texture.isReadable, mipmapCount = texture.mipmapCount, filter = texture.filterMode.ToString(),
                textureType = importer.textureType.ToString(), spriteMode = importer.spriteImportMode.ToString(),
                defaultMaxSize = importer.maxTextureSize, standaloneMaxSize = standalone.maxTextureSize, standaloneOverride = standalone.overridden,
                effectiveMaxSize = standalone.overridden ? standalone.maxTextureSize : importer.GetDefaultPlatformTextureSettings().maxTextureSize,
                expectsFullImageSprite = frame.state == "PortHub" || frame.state == "Tavern" };
            Check(frame, row.sourceIs4K, "Source PNG is not exactly 3840x2160.");
            Check(frame, row.importedIs4K, "Imported texture is not exactly 3840x2160; check the desktop maximum.");
            Check(frame, row.effectiveMaxSize >= Width, "Effective desktop maximum still reduces 4K art.");
            Check(frame, row.isBC7, "Actual imported texture is not BC7.");
            Check(frame, !row.readable && row.mipmapCount == 1, "Background readability/mipmap policy changed.");
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(asset).OfType<Sprite>())
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId);
                Rect rect = sprite.rect;
                bool full = Mathf.Abs(rect.x) < .01f && Mathf.Abs(rect.y) < .01f && Mathf.Abs(rect.width - Width) < .01f && Mathf.Abs(rect.height - Height) < .01f;
                row.sprites.Add(new SpriteCheck { name = sprite.name, guid = guid, localId = localId, rect = rect,
                    pivot = sprite.pivot, border = sprite.border, fullImage = full, sameTexture = sprite.texture == texture });
            }
            row.spriteCount = row.sprites.Count;
            row.fullImageSpritesVerified = !row.expectsFullImageSprite || (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Multiple && row.spriteCount == 1 && row.sprites.All(s => s.fullImage && s.sameTexture));
            Check(frame, row.fullImageSpritesVerified, "The original full-image Sprite must retain its identity and span the complete 3840x2160 texture.");
            return row;
        }

        private static void SaveScreenshot(string output, Frame frame)
        {
            Texture2D pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                pixels.Apply(false, false);
                Color32[] sample = pixels.GetPixels32();
                int min = 255, max = 0, nonBlack = 0, count = 0;
                for (int i = 0; i < sample.Length; i += 257)
                {
                    int value = (sample[i].r + sample[i].g + sample[i].b) / 3;
                    min = Math.Min(min, value); max = Math.Max(max, value);
                    if (value > 8) nonBlack++;
                    count++;
                }
                frame.imageHasContent = max - min > 20 && nonBlack > count / 20;
                byte[] png = pixels.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(output, frame.file), png);
                using (SHA256 sha = SHA256.Create()) frame.screenshotSha256 = Hex(sha.ComputeHash(png));
                frame.captured = true;
                Check(frame, frame.imageHasContent, "Screenshot is blank or nearly uniform.");
            }
            finally { Object.DestroyImmediate(pixels); }
        }

        private static void EnsurePipeline()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null || RenderPipelineManager.currentPipeline != null) return;
            MethodInfo prepare = typeof(RenderPipelineManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "TryPrepareRenderPipeline" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(RenderPipelineAsset));
            if (prepare == null) throw new MissingMethodException("RenderPipelineManager.TryPrepareRenderPipeline(RenderPipelineAsset)");
            prepare.Invoke(null, new object[] { asset });
            if (RenderPipelineManager.currentPipeline == null) throw new InvalidOperationException("Render pipeline initialization did not complete.");
        }

        private static void Check(Frame frame, bool okay, string error) { if (!okay) frame.errors.Add(error); }
        private static void SaveReport(string output, Report report) => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        private static string Hash(string path) { using (var input = File.OpenRead(path)) using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(input)); }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        private static string PathOf(Transform item, Transform root) { string path = item.name; while (item.parent != null && item.parent != root) { item = item.parent; path = item.name + "/" + path; } return path; }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return new Rect(min, max - min);
        }
    }
}
