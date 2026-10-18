using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
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
    /// <summary>
    /// Six event-page captures using temporary, non-Play preview scenes.
    /// Does not invoke button callbacks, write PlayerPrefs, or save scenes/assets.
    /// </summary>
    public static class EncounterDossierAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int CaptureLayer = 31;

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string scope = "Three real route events, four actions, tutorial long text with a locked navigator choice, and a mandatory tutorial event. Temporary non-Play previews; no save writes or button callbacks.";
            public int expectedCaptureCount = 6;
            public int successfulCaptureCount;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public string label;
            public string file;
            public int routeIndex;
            public string route;
            public string eventId;
            public string eventName;
            public string frameTexture;
            public string illustrationTexture;
            public bool tutorial;
            public bool syntheticFixture;
            public string fixtureNote;
            public bool mandatory;
            public int descriptionLength;
            public int choiceCount;
            public int lockedChoiceCount;
            public bool hasSkip;
            public int uiObjectCount;
            public bool captured;
            public List<string> unityErrors = new List<string>();
            public string error;
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.EncounterDossierAcceptance.CaptureFromCommandLine
        /// -encounterCapturePath D:/Game/UnityClient/Artifacts/EncounterDossier/review-01
        /// The output directory must be new or empty. Do not pass -nographics.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("ENCOUNTER_DOSSIER_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Encounter acceptance requires stopped, non-Play edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Encounter captures require graphics. Remove -nographics.");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty. Use a new -encounterCapturePath: " + directory);
            Directory.CreateDirectory(directory);

            Review report = new Review { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            report.captures.AddRange(new[]
            {
                new Record { label = "碎帆礁海 · 礁间近路", routeIndex = 0, eventId = "event_reef_shortcut", file = "01_Reef_Shortcut.png" },
                new Record { label = "黄金潮汐商路 · 帝国临检船", routeIndex = 1, eventId = "gold_customs", file = "02_Golden_Customs.png" },
                new Record { label = "月潮遗迹海 · 深海低语", routeIndex = 2, eventId = "moon_whisper", file = "03_Moon_Whisper.png" },
                new Record { label = "三项选择与略过 · 断桅求援", routeIndex = 0, eventId = "event_reef_broken_mast", file = "04_Four_Actions.png",
                    fixtureNote = "Real encounter data; temporary stocked cargo and a carpenter make all three choices available." },
                new Record { label = "教学 · 最长正文与缺少航海士", routeIndex = 0, tutorial = true, syntheticFixture = true,
                    eventId = "tutorial_crew_requirement_event", file = "05_Tutorial_Long_Text_Locked.png",
                    fixtureNote = "Layout stress fixture: combines the longest first-leg tutorial description with the real crew-requirement choices and enables skip. It is not a new playable event." },
                new Record { label = "教学 · 必须处理补给箱", routeIndex = 0, tutorial = true,
                    eventId = "tutorial_first_event", file = "06_Tutorial_Mandatory.png" },
            });
            try
            {
                foreach (Record record in report.captures)
                {
                    Application.LogCallback collectErrors = (condition, stackTrace, type) =>
                    {
                        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                            record.unityErrors.Add(condition + (string.IsNullOrEmpty(stackTrace) ? string.Empty : "\n" + stackTrace));
                    };
                    Application.logMessageReceived += collectErrors;
                    try
                    {
                        CaptureOne(directory, record);
                        if (record.unityErrors.Count > 0)
                            throw new InvalidOperationException("Unity reported " + record.unityErrors.Count + " error(s) while building or rendering this capture.");
                        record.captured = true;
                        report.successfulCaptureCount++;
                    }
                    catch (Exception exception)
                    {
                        Exception cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException : exception;
                        record.error = cause.Message;
                        report.errors.Add(record.label + ": " + cause.Message);
                        Debug.LogWarning("Encounter capture failed: " + record.label + " — " + cause.Message);
                    }
                    finally { Application.logMessageReceived -= collectErrors; }
                }
                if (report.captures.Take(3).All(record => record.captured) &&
                    report.captures.Take(3).Select(record => record.frameTexture).Distinct().Count() != 3)
                    report.errors.Add("The three route captures did not use three distinct frame textures.");
            }
            finally
            {
                WriteNewFile(Path.Combine(directory, "report.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulCaptureCount != report.expectedCaptureCount)
                throw new InvalidOperationException("Encounter acceptance is incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static void CaptureOne(string directory, Record record)
        {
            Scene previewScene = default;
            RenderTexture previousActive = RenderTexture.active;
            GameObject host = null;
            GameObject cameraHost = null;
            RenderTexture target = null;
            try
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                host = new GameObject("Encounter Acceptance Host") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, previewScene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Encounter, CoreGameplayPreviewPort.GrayGull, record.routeIndex);
                host.SetActive(true);
                if (controller.Session == null || controller.Session.Map == null)
                    throw new InvalidOperationException("The isolated encounter preview did not initialize its voyage data.");
                if (record.tutorial) ArrangeTutorial(controller, record);
                else ArrangeCore(controller, record);
                Invoke(controller, "Render");

                Transform root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                Canvas canvas = root != null ? root.GetComponent<Canvas>() : null;
                if (canvas == null) throw new InvalidOperationException("The preview Canvas was not created.");
                string prefix = record.tutorial ? "Tutorial Encounter" : "Encounter";
                Find<RectTransform>(root, prefix + " Card");
                Image paper = Find<Image>(root, prefix + " Parchment");
                RawImage picture = Find<RawImage>(root, record.tutorial ? "Tutorial Event Art" : "Demo Event Art");
                if (paper.sprite != MarketShopSkin.Paper || picture.texture == null)
                    throw new InvalidOperationException("The light paper frame or event illustration is missing.");
                record.frameTexture = paper.sprite.texture.name;
                record.illustrationTexture = picture.texture.name;
                string choicesName = record.tutorial ? "Tutorial Choices" : "Encounter Choices";
                string skipName = record.tutorial ? "Tutorial Skip Encounter" : "Skip Encounter";
                Button[] actions = Find<RectTransform>(root, choicesName).GetComponentsInChildren<Button>(true);
                record.hasSkip = actions.Any(button => button.name == skipName);
                record.lockedChoiceCount = actions.Count(button => !button.interactable);
                if (actions.Length != record.choiceCount + (record.mandatory ? 0 : 1) || record.hasSkip == record.mandatory)
                    throw new InvalidOperationException("The displayed actions do not match the encounter's choice/skip rules.");
                if (record.syntheticFixture && record.lockedChoiceCount == 0)
                    throw new InvalidOperationException("The tutorial fixture must show a locked navigator choice.");

                cameraHost = new GameObject("Encounter Acceptance Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
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
                camera.cullingMask = 1 << CaptureLayer;
                camera.scene = previewScene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Encounter Acceptance Target" };
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(Width, Height);
                canvasRect.position = Vector3.zero;
                canvasRect.rotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one;
                Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children) child.gameObject.layer = CaptureLayer;
                record.uiObjectCount = children.Length;
                // Dynamic-font atlas repacks can invalidate earlier text meshes.
                // Rebuild and render three passes before reading the stable frame.
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                SaveFrame(target, Path.Combine(directory, record.file));
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (host != null) Object.DestroyImmediate(host);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void ArrangeCore(CoreGameplayDemoController controller, Record record)
        {
            CoreVoyageSession session = controller.Session;
            CoreMapNode node = session.Map.Nodes.FirstOrDefault(item => item.Encounter.Id == record.eventId);
            if (node == null) throw new InvalidOperationException("The real route encounter was not found: " + record.eventId);
            if (record.eventId == "event_reef_broken_mast")
            {
                SetProperty(session, "Phase", CoreDemoPhase.Port);
                session.ConfigureCrewCapabilities(false, true, false, false, false, false);
                SetProperty(session, "Cargo", new VoyageCargo { Wood = 25, Beer = 2, Gold = 100, Water = 12, Food = 12 });
            }
            SetProperty(session, "Position", node.Position);
            SetProperty(session, "ActiveEncounter", node.Encounter);
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            SetField(session, "activeNode", node);
            node.Discovered = true;
            record.route = session.Map.Route.Name;
            record.eventName = node.Encounter.Name;
            record.mandatory = node.Encounter.Mandatory;
            record.descriptionLength = node.Encounter.Description.Length;
            record.choiceCount = node.Encounter.Choices.Count;
        }

        private static void ArrangeTutorial(CoreGameplayDemoController controller, Record record)
        {
            TutorialVoyageSession session = controller.TutorialSession;
            session.BeginFirstLeg();
            TutorialEncounter encounter = session.CurrentLegEncounters.First(item => item.Id == record.eventId);
            if (record.syntheticFixture)
            {
                TutorialEncounter longest = session.CurrentLegEncounters.OrderByDescending(item => item.Description.Length).First();
                encounter = new TutorialEncounter(encounter.Id, "首航抉择 · 长文验收样例", encounter.Category,
                    longest.Description, true, false, encounter.Choices.ToArray());
            }
            SetProperty(session, "CurrentEncounter", encounter);
            SetProperty(session, "Phase", TutorialVoyagePhase.ResolvingEncounter);
            SetField(controller, "tutorialActive", true);
            record.route = "首航教学";
            record.eventName = encounter.Name;
            record.mandatory = !encounter.Optional;
            record.descriptionLength = encounter.Description.Length;
            record.choiceCount = encounter.Choices.Count;
        }

        private static void SaveFrame(RenderTexture target, string path)
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
                    dark = Math.Min(dark, level);
                    bright = Math.Max(bright, level);
                }
                if (bright - dark < 20) throw new InvalidOperationException("Captured frame is nearly uniform; the UI may not have rendered.");
                WriteNewFile(path, image.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>航海事件图文验收</title>");
            html.Append("<style>body{margin:32px;background:#171c1b;color:#f0dfbb;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,540px),1fr));gap:24px}figure{margin:0;padding:12px;border:1px solid #756247;background:#29302a}img{display:block;width:100%;height:auto}figcaption,p{line-height:1.7}small{display:block;color:#c4bba9;overflow-wrap:anywhere}a{color:#ebbe6c}.error{color:#ffb99d}</style></head><body>");
            html.Append("<h1>航海事件图文验收</h1><p>").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append(" 张 · 1920 × 1080 · 编辑模式独立预览<br>检查插画比例、三航线边框、纸张内文字、木牌按钮、锁定提示和底部略过。点击截图查看原尺寸。</p><p><a href=\"report.json\">结构化报告</a><br>截图导出成功后仍需逐图视觉检查。生成时间（UTC）：")
                .Append(Escape(report.capturedAtUtc)).Append("</p><main>");
            foreach (Record record in report.captures)
            {
                html.Append("<figure>");
                if (record.captured)
                    html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><img loading=\"lazy\" width=\"1920\" height=\"1080\" alt=\"")
                        .Append(Escape(record.label)).Append("\" src=\"").Append(Escape(record.file)).Append("\"></a>");
                html.Append("<figcaption>").Append(Escape(record.label)).Append("<small>").Append(Escape(record.eventName)).Append(" · ")
                    .Append(record.choiceCount).Append(" 项选择 · ").Append(record.hasSkip ? "可以略过" : "必须处理").Append(" · 锁定 ")
                    .Append(record.lockedChoiceCount).Append(" 项 · 正文 ").Append(record.descriptionLength).Append(" 字符</small><small>")
                    .Append(Escape(record.frameTexture)).Append("</small>");
                if (!string.IsNullOrEmpty(record.fixtureNote)) html.Append("<small>").Append(Escape(record.fixtureNote)).Append("</small>");
                if (!record.captured) html.Append("<small class=\"error\">捕获失败：").Append(Escape(record.error)).Append("</small>");
                html.Append("</figcaption></figure>");
            }
            html.Append("</main>");
            foreach (string error in report.errors) html.Append("<p class=\"error\">").Append(Escape(error)).Append("</p>");
            html.Append("</body></html>");
            WriteNewFile(Path.Combine(directory, "index.html"), Encoding.UTF8.GetBytes(html.ToString()));
        }

        private static void WriteNewFile(string path, byte[] bytes)
        {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T value = root.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            if (value == null) throw new InvalidOperationException("Missing preview object: " + name);
            return value;
        }

        private static void SetProperty(object target, string name, object value)
        {
            MethodInfo setter = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);
            if (setter == null) throw new InvalidOperationException("Missing fixture property: " + name);
            setter.Invoke(target, new[] { value });
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Missing fixture field: " + name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Missing preview method: " + name);
            method.Invoke(target, null);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] != "-encounterCapturePath") continue;
                if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("-encounterCapturePath requires a fresh output directory.");
                return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            }
            throw new ArgumentException("Pass -encounterCapturePath followed by a new or empty output directory.");
        }
    }
}
