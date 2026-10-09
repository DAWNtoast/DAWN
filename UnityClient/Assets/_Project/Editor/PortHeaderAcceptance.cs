using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Six-port header art/layout review in isolated, disposable preview scenes.
    /// Captures hub, tavern and maintenance for each port plus two settings drafts.
    /// Never enters Play Mode, applies settings, buys anything, saves a scene, or
    /// writes PlayerPrefs. Only the preview's open/close settings buttons are invoked.
    /// </summary>
    public static class PortHeaderAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int Layer = 31;
        private const int WarmupFrames = 3;
        private static readonly string[] FacilityHeaderOrder =
        {
            "Return To Port Hub", "Previous Port Facility", "Port Facility Title", "Next Port Facility",
            "Port Facility Time", "Port Facility Port", "Port Facility Gold", "Port Facility Settings",
        };
        private static readonly string[] HubHeaderOrder =
        {
            "Port Hub Time", "Port Hub Port", "Port Hub Gold", "Port Hub Settings",
        };

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string safety = "Isolated edit-mode preview. Opens/closes hub and facility settings drafts; never applies settings, alters PlayerPrefs, or saves user scenes.";
            public int expectedCaptureCount = 20;
            public int renderWarmupFrames = WarmupFrames;
            public int successfulCaptureCount;
            public int distinctRegionalFrameCount;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public string port;
            public string screen;
            public string file;
            public bool settingsOpen;
            public bool complete;
            public string title;
            public string localTime;
            public string portReadout;
            public string profileGold;
            public string frameTexture;
            public string frameAssetPath;
            public string frameRendering;
            public string frameMaterial;
            public bool frameInterceptsClicks;
            public bool samePortFrame;
            public List<string> orderedObjects = new List<string>();
            public List<float> controlCenterX = new List<float>();
            public string error;
        }

        private sealed class CaptureCase
        {
            public CoreGameplayPreviewPort port;
            public CoreGameplayPreviewScreen screen;
            public bool settings;
        }

        // Keep value snapshots: rebuilding the preview disposes its generated
        // sprites, so earlier Image/Sprite references cannot be compared later.
        private sealed class FrameReference
        {
            public Texture texture;
            public Material material;
            public Rect spriteRect;
            public Vector4 border;
            public Color color;
            public float pixelsPerUnitMultiplier;
        }

        [MenuItem("Shattered Sail/Art Review/Capture Regional Port Headers")]
        public static void CaptureFromMenu()
        {
            string directory = Capture(ResolveOutputDirectory());
            Debug.Log("Regional port header gallery: " + directory);
            EditorUtility.RevealInFinder(Path.Combine(directory, "index.html"));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.PortHeaderAcceptance.CaptureFromCommandLine
        /// -portHeaderCapturePath D:/Game/UnityClient/Artifacts/PortHeaders/review-01
        /// Graphics are required; do not pass -nographics.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("PORT_HEADER_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.PortHeaderAcceptance.CaptureAllFromCommandLine
        /// Accepts both -portHeaderCapturePath and -uiPreviewCapturePath so one
        /// graphics-enabled editor startup captures both independent galleries.
        /// </summary>
        public static void CaptureAllFromCommandLine()
        {
            CaptureFromCommandLine();
            UIPreviewSceneAcceptance.CaptureFromCommandLine();
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before capturing port headers.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Port header screenshots require graphics. Remove -nographics.");
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty. Use a fresh -portHeaderCapturePath: " + directory);
            Directory.CreateDirectory(directory);

            Review report = new Review { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            HashSet<Texture> regionalFrames = new HashSet<Texture>();
            Dictionary<string, FrameReference> portFrames = new Dictionary<string, FrameReference>(StringComparer.Ordinal);
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousActive = RenderTexture.active;
            EventSystem previousEventSystem = EventSystem.current;
            GameObject previousSelection = previousEventSystem == null ? null : previousEventSystem.currentSelectedGameObject;
            GameObject host = null;
            GameObject cameraHost = null;
            RenderTexture target = null;
            try
            {
                host = new GameObject("Port Header Acceptance Host") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, previewScene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                host.SetActive(true);
                cameraHost = new GameObject("Port Header Acceptance Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer;
                camera.scene = previewScene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Port Header Acceptance Target" };
                target.Create();
                camera.targetTexture = target;

                foreach (CaptureCase item in Cases())
                {
                    Record record = new Record
                    {
                        port = PortFacilityArtCatalog.SupportedRegionalPorts[(int)item.port],
                        screen = item.screen.ToString(), settingsOpen = item.settings,
                        file = item.port + "_" + item.screen + (item.settings ? "_Settings" : string.Empty) + ".png",
                    };
                    report.captures.Add(record);
                    try
                    {
                        controller.ConfigureEditorPreview(item.screen, item.port, 0);
                        if (!controller.HasCompleteEditorPreview)
                            throw new InvalidOperationException("The required port preview did not build.");
                        Canvas canvas = host.GetComponentInChildren<Canvas>();
                        if (canvas == null) throw new InvalidOperationException("The preview Canvas is missing.");
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.worldCamera = camera;
                        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                        canvasRect.sizeDelta = new Vector2(Width, Height);
                        canvasRect.position = Vector3.zero;
                        canvasRect.localScale = Vector3.one;
                        Image frame = InspectHeader(host, item.screen, record);
                        InspectSamePortFrame(frame, portFrames, record);
                        regionalFrames.Add(frame.sprite.texture);
                        if (item.settings)
                        {
                            string settingsButton = item.screen == CoreGameplayPreviewScreen.PortHub ? "Port Hub Settings" : "Port Facility Settings";
                            Require<Button>(host, settingsButton).onClick.Invoke();
                            Require<RectTransform>(host, "Port Settings Overlay");
                            Require<RectTransform>(host, "Port Settings Sheet");
                            RawImage paper = Require<RawImage>(host, "Port Settings Parchment");
                            if (paper.texture == null) throw new InvalidOperationException("Settings draft has no parchment art.");
                        }
                        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                        // Best-fit Chinese labels can rebuild the dynamic font
                        // atlas while rendering. Rebuild all text for three frames
                        // before readback, matching the saved-scene gallery path.
                        for (int warmup = 0; warmup < WarmupFrames; warmup++)
                        {
                            foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                            Canvas.ForceUpdateCanvases();
                            if (GraphicsSettings.currentRenderPipeline != null)
                                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                            else camera.Render();
                        }
                        SaveFrame(target, Path.Combine(directory, record.file));
                        record.complete = true;
                        report.successfulCaptureCount += 1;
                    }
                    catch (Exception exception)
                    {
                        record.error = exception.Message;
                        report.errors.Add(record.port + " / " + record.screen + ": " + exception.Message);
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        // This discards the preview draft and restores any external
                        // EventSystem selection; never invoke Apply Port Settings.
                        Button close = Find(host, "Close Port Settings")?.GetComponent<Button>();
                        if (close != null) close.onClick.Invoke();
                    }
                }
                report.distinctRegionalFrameCount = regionalFrames.Count;
                if (regionalFrames.Count != 6)
                    report.errors.Add("Expected six distinct regional frame textures, found " + regionalFrames.Count + ".");
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(previewScene);
                if (previousEventSystem != null)
                    previousEventSystem.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulCaptureCount != report.expectedCaptureCount)
                throw new InvalidOperationException("Port header review is incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static IEnumerable<CaptureCase> Cases()
        {
            foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
            {
                yield return new CaptureCase { port = port, screen = CoreGameplayPreviewScreen.PortHub };
                yield return new CaptureCase { port = port, screen = CoreGameplayPreviewScreen.Tavern };
                yield return new CaptureCase { port = port, screen = CoreGameplayPreviewScreen.Maintenance };
            }
            yield return new CaptureCase { port = CoreGameplayPreviewPort.GrayGull, screen = CoreGameplayPreviewScreen.PortHub, settings = true };
            yield return new CaptureCase { port = CoreGameplayPreviewPort.GrayGull, screen = CoreGameplayPreviewScreen.Maintenance, settings = true };
        }

        private static Image InspectHeader(GameObject host, CoreGameplayPreviewScreen screen, Record record)
        {
            bool hub = screen == CoreGameplayPreviewScreen.PortHub;
            string prefix = hub ? "Port Hub" : "Port Facility";
            string[] headerOrder = hub ? HubHeaderOrder : FacilityHeaderOrder;
            RectTransform header = Require<RectTransform>(host, hub ? "Port Hub" : "Port Facility Header");
            Image frame = Require<Image>(host, prefix + " Frame");
            if (!header.gameObject.activeInHierarchy || frame.transform.parent != header)
                throw new InvalidOperationException("The regional header/frame is inactive or incorrectly parented.");
            if (frame.sprite == null || frame.sprite.texture == null || frame.type != Image.Type.Sliced || frame.raycastTarget)
                throw new InvalidOperationException("The regional frame must be a textured nine-sliced Image with raycasts disabled.");
            if (Require<RectTransform>(host, "Header").gameObject.activeSelf)
                throw new InvalidOperationException("The generic blue HUD still duplicates the regional port header.");
            if (Find(host, "Facility Profile Status") != null)
                throw new InvalidOperationException("The obsolete combined status readout is still present.");
            if (hub)
            {
                foreach (string name in new[] { "Port Hub Eyebrow", "Port Hub Title", "Port Hub Status" }.Concat(FacilityHeaderOrder))
                    if (Find(host, name) != null)
                        throw new InvalidOperationException("The hub should only retain time, port, gold and settings; found " + name + ".");
            }
            string[] actualControls = header.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf && (child.GetComponent<Text>() != null || child.GetComponent<Button>() != null))
                .Select(child => child.name).ToArray();
            if (!actualControls.SequenceEqual(headerOrder))
                throw new InvalidOperationException("Expected exactly " + headerOrder.Length + " ordered header controls, found: " + string.Join(", ", actualControls));
            float previousRight = float.NegativeInfinity;
            foreach (string name in headerOrder)
            {
                RectTransform control = Require<RectTransform>(host, name);
                if (control.parent != header) throw new InvalidOperationException(name + " does not belong to the regional port header.");
                float x = control.anchoredPosition.x;
                float left = x - control.rect.width * control.pivot.x;
                float right = x + control.rect.width * (1f - control.pivot.x);
                if (left <= previousRight) throw new InvalidOperationException(name + " overlaps or precedes the previous header control.");
                previousRight = right;
                record.orderedObjects.Add(name);
                record.controlCenterX.Add(x);
            }
            record.title = hub ? string.Empty : Require<Text>(host, "Port Facility Title").text;
            record.localTime = Require<Text>(host, prefix + " Time").text;
            record.portReadout = Require<Text>(host, prefix + " Port").text;
            record.profileGold = Require<Text>(host, prefix + " Gold").text;
            record.frameTexture = frame.sprite.texture.name;
            record.frameAssetPath = AssetDatabase.GetAssetPath(frame.sprite.texture);
            record.frameRendering = frame.type.ToString();
            record.frameMaterial = frame.material == null ? string.Empty : frame.material.name;
            record.frameInterceptsClicks = frame.raycastTarget;
            if (record.portReadout != record.port)
                throw new InvalidOperationException("The header port does not match the selected preview port.");
            return frame;
        }

        private static void InspectSamePortFrame(Image frame, Dictionary<string, FrameReference> portFrames, Record record)
        {
            if (portFrames.TryGetValue(record.port, out FrameReference reference))
            {
                if (reference.texture != frame.sprite.texture || reference.material != frame.material ||
                    reference.spriteRect != frame.sprite.rect || reference.border != frame.sprite.border ||
                    reference.color != frame.color || !Mathf.Approximately(reference.pixelsPerUnitMultiplier, frame.pixelsPerUnitMultiplier))
                    throw new InvalidOperationException("Hub and facility headers must share the same regional frame art, material and nine-slice proportions for " + record.port + ".");
            }
            else
            {
                portFrames.Add(record.port, new FrameReference
                {
                    texture = frame.sprite.texture, material = frame.material, spriteRect = frame.sprite.rect,
                    border = frame.sprite.border, color = frame.color, pixelsPerUnitMultiplier = frame.pixelsPerUnitMultiplier,
                });
            }
            record.samePortFrame = true;
        }

        private static Transform Find(GameObject host, string name)
        {
            return host == null ? null : host.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        private static T Require<T>(GameObject host, string name) where T : Component
        {
            T component = Find(host, name)?.GetComponent<T>();
            if (component == null) throw new InvalidOperationException(name + " is missing " + typeof(T).Name + ".");
            return component;
        }

        private static void SaveFrame(RenderTexture target, string file)
        {
            RenderTexture.active = target;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                Color32[] pixels = image.GetPixels32();
                int dark = 255, bright = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int level = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    dark = Math.Min(dark, level); bright = Math.Max(bright, level);
                }
                if (bright - dark < 20) throw new InvalidOperationException("Frame is nearly uniform; the Canvas did not render correctly.");
                File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>六港总览与设施顶栏验收</title>");
            html.Append("<style>body{margin:30px;background:#152127;color:#f5e7c8;font:16px system-ui}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(600px,1fr));gap:22px}figure{margin:0;padding:12px;background:#29343a;border:1px solid #7b6748}img{width:100%;height:auto;display:block}.header{aspect-ratio:1920/165;overflow:hidden;margin:10px 0}.header img{width:100%}figcaption{line-height:1.6}small{display:block;color:#c5c9c6}a{color:#e6b759}p{max-width:1200px;line-height:1.7}</style>");
            html.Append("<h1>六港总览与设施顶栏验收</h1><p>").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append(" 张 · 1920 × 1080 · ").Append(report.distinctRegionalFrameCount).Append(" 种独立边框美术。每港总览、酒馆与后勤各一张，另含灰鸥港总览和后勤设置弹窗。</p>");
            html.Append("<p>港口总览从左到右：本机时间 ｜ 港口名称 ｜ 总金币 ｜ 设置。设施页从左到右：返回港口 ｜ ← ｜ 功能名称 ｜ → ｜ 本机时间 ｜ 港口名称 ｜ 总金币 ｜ 设置。同港口总览与设施使用相同边框材质。以下顶部局部对照来自同一张完整截图；点击可打开原图放大查看。结构检查不代替人工视觉验收。</p><p><a href=\"report.json\">结构化报告</a> · <a href=\"checklist.md\">视觉检查清单</a></p><section>");
            foreach (Record record in report.captures)
            {
                html.Append("<figure><figcaption>").Append(Escape(record.port)).Append(" · ").Append(Escape(record.screen));
                if (record.settingsOpen) html.Append(" · 设置弹窗");
                html.Append("<small>").Append(Escape(record.frameTexture)).Append("</small></figcaption>");
                if (record.complete)
                {
                    html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><div class=\"header\"><img loading=\"lazy\" src=\"")
                        .Append(Escape(record.file)).Append("\"></div><img loading=\"lazy\" src=\"").Append(Escape(record.file)).Append("\"></a>");
                }
                else html.Append("<p>捕获失败：").Append(Escape(record.error)).Append("</p>");
                html.Append("</figure>");
            }
            html.Append("</section></html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
            StringBuilder checklist = new StringBuilder("# 六港总览与设施顶栏 · 人工视觉验收\n\n");
            checklist.Append("捕获成功 ").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append("。仅在独立编辑预览中打开/关闭设置，没有应用设置或写入存档。\n\n")
                .Append("- [ ] 六个港口的边框装饰、材质与港口主题有区别，不是同一图片换色。\n")
                .Append("- [ ] 同一港口的总览、酒馆与后勤沿用同一边框美术、材质和九宫格比例。\n")
                .Append("- [ ] 九宫格角饰没有被拉扁，边框保持清晰；顶部未被裁切。\n")
                .Append("- [ ] 总览只显示时间、港口名称、总金币、设置四项，按此顺序排列，无标题、返回和左右箭头。\n")
                .Append("- [ ] 设施页返回、左箭头、功能名、右箭头、时间、港口、总金币、设置从左到右排列，互不遮挡。\n")
                .Append("- [ ] 总览与设施页只有一行完整顶栏，没有原蓝色原型标题栏重复出现。\n")
                .Append("- [ ] 酒馆与后勤的船员/物品/支撑点未因顶栏变更而位移；背景有效区域保留。\n")
                .Append("- [ ] 明暗两种背景下文字均可读，中文无缺字，悬停与键盘焦点能识别。\n")
                .Append("- [ ] 总览和后勤的设置弹窗羊皮纸均可读，覆盖当前页面，不穿透点击；关闭后原页面仍完整。\n\n");
            foreach (Record record in report.captures)
                checklist.Append("- [").Append(record.port).Append(" · ").Append(record.screen).Append(record.settingsOpen ? " · 设置" : string.Empty)
                    .Append("](").Append(record.file).Append(") — ").Append(record.complete ? "已捕获，待看图" : "失败：" + record.error).Append('\n');
            File.WriteAllText(Path.Combine(directory, "checklist.md"), checklist.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index += 1)
                if (arguments[index] == "-portHeaderCapturePath") return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            return Path.Combine(project, "Artifacts", "PortHeaders", "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
    }
}
