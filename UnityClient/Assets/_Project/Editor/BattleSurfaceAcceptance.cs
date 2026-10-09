using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Renders the actual battle presenter in disposable edit-mode preview scenes.
    /// Only temporary session data is arranged; user scenes, saves and settings are untouched.
    /// </summary>
    public static class BattleSurfaceAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int Layer = 31;

        [Serializable]
        private sealed class Report
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string safety = "Disposable edit-mode preview scenes. No Play Mode, scene saves, PlayerPrefs writes or settings application. Only temporary encounters and hands are arranged. All images are actual Unity camera renders, without image post-processing.";
            public int expectedBattleCount = 9;
            public int successfulBattleCount;
            public int successfulImageCount;
            public double totalElapsedMilliseconds;
            public List<BattleRecord> battles = new List<BattleRecord>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class BattleRecord
        {
            public string id;
            public int routeIndex;
            public string route;
            public string port;
            public string seed;
            public bool boss;
            public string enemy;
            public string terrain;
            public string background;
            public string enemyTexture;
            public Rect enemyTextureUv;
            public List<string> handTypes = new List<string>();
            public bool endTurnAboveEnemyStatus;
            public double buildMilliseconds;
            public int graphicCount;
            public int missingTextureCount;
            public int materialErrorCount;
            public bool complete;
            public string error;
            public List<UiBoundsRecord> bounds = new List<UiBoundsRecord>();
            public List<string> materialShaders = new List<string>();
            public List<string> diagnostics = new List<string>();
            public List<FrameRecord> frames = new List<FrameRecord>();
        }

        [Serializable]
        private sealed class UiBoundsRecord
        {
            public string name;
            public Vector2 anchoredPosition;
            public Vector2 size;
            public Rect worldRect;
        }

        [Serializable]
        private sealed class FrameRecord
        {
            public string file;
            public string label;
            public string framing;
            public float cameraWorldWidth;
            public float cameraWorldHeight;
            public double renderMilliseconds;
            public bool complete;
        }

        [MenuItem("Shattered Sail/Art Review/Capture Battle Surfaces")]
        public static void CaptureFromMenu()
        {
            string directory = Capture(ResolveOutputDirectory());
            Debug.Log("Battle surface gallery: " + directory);
            EditorUtility.RevealInFinder(Path.Combine(directory, "index.html"));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.BattleSurfaceAcceptance.CaptureFromCommandLine
        /// -battleSurfaceCapturePath D:/Game/UnityClient/Artifacts/BattleSurfaces/review-01
        /// Requires graphics: do not pass -nographics. The output directory must be new or empty.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("BATTLE_SURFACE_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before capturing battle surfaces.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Battle capture requires graphics. Remove -nographics.");
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Output directory is not empty. Use a fresh -battleSurfaceCapturePath: " + directory);
            Directory.CreateDirectory(directory);
            Report report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            Stopwatch totalWatch = Stopwatch.StartNew();
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousTarget = RenderTexture.active;
            EventSystem previousEvents = EventSystem.current;
            GameObject previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            GameObject cameraHost = null;
            GameObject host = null;
            RenderTexture target = null;
            BattleRecord activeRecord = null;
            Application.LogCallback collectErrors = (message, stackTrace, type) =>
            {
                if (activeRecord != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    activeRecord.diagnostics.Add(type + ": " + message);
            };
            Application.logMessageReceived += collectErrors;
            try
            {
                cameraHost = new GameObject("Battle Surface Review Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.cullingMask = 1 << Layer;
                camera.scene = scene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Battle Surface Review Target" };
                target.Create();
                camera.targetTexture = target;

                EventTerrainConstraint[] environments =
                {
                    EventTerrainConstraint.OpenSea, EventTerrainConstraint.Beach, EventTerrainConstraint.Strait,
                    EventTerrainConstraint.Reef, EventTerrainConstraint.Fog, EventTerrainConstraint.Storm
                };
                for (int caseIndex = 0; caseIndex < report.expectedBattleCount; caseIndex += 1)
                {
                    bool boss = caseIndex >= environments.Length;
                    int routeIndex = boss ? caseIndex - environments.Length : caseIndex % 3;
                    EventTerrainConstraint environment = boss
                        ? new[] { EventTerrainConstraint.Strait, EventTerrainConstraint.Beach, EventTerrainConstraint.Storm }[routeIndex]
                        : environments[caseIndex];
                    string prefix = (caseIndex + 1).ToString("D2") + "_" + (boss ? "Boss" : "Normal") + "_Route" + routeIndex + "_" + environment;
                    BattleRecord record = new BattleRecord { id = prefix, routeIndex = routeIndex, boss = boss };
                    report.battles.Add(record);
                    activeRecord = record;
                    try
                    {
                        Stopwatch buildWatch = Stopwatch.StartNew();
                        host = new GameObject("Battle Surface Review " + prefix) { hideFlags = HideFlags.HideAndDontSave };
                        host.SetActive(false);
                        SceneManager.MoveGameObjectToScene(host, scene);
                        CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                        // Navigation prepares a deterministic voyage without relying on a particular
                        // reachable encounter in that seed. Everything below remains temporary.
                        controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.TwilightTide, routeIndex);
                        host.SetActive(true);
                        if (!controller.HasCompleteEditorPreview || controller.Session.Map == null)
                            throw new InvalidOperationException("Voyage preview did not build.");
                        EnterBattle(controller.Session, environment, boss);
                        ShowAllCardTypes(controller.Session.Battle);
                        InvokePrivate(controller, "Render");
                        Canvas canvas = host.GetComponentInChildren<Canvas>();
                        if (canvas == null) throw new InvalidOperationException("Preview Canvas is missing.");
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.worldCamera = camera;
                        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                        canvasRect.sizeDelta = new Vector2(Width, Height);
                        canvasRect.position = Vector3.zero;
                        canvasRect.localScale = Vector3.one;
                        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                        Canvas.ForceUpdateCanvases();
                        buildWatch.Stop();
                        record.buildMilliseconds = buildWatch.Elapsed.TotalMilliseconds;
                        record.route = controller.Session.Map.Route.Name;
                        record.port = controller.Session.CurrentPort;
                        record.seed = controller.Session.Map.Seed;
                        record.terrain = controller.Session.BattleTerrain.ToString();
                        record.enemy = controller.Session.Battle.EnemyName;
                        record.handTypes = controller.Session.Battle.Hand.Select(index => CoreVoyageSession.CardLibrary[index].Type).ToList();
                        InspectUi(canvas, record);
                        RectTransform enemyShip = RequireRect(canvas, "Enemy Ship Art");
                        RawImage enemyGraphic = enemyShip.GetComponent<RawImage>();
                        if (enemyGraphic != null)
                        {
                            record.enemyTexture = enemyGraphic.texture == null ? null : enemyGraphic.texture.name;
                            record.enemyTextureUv = enemyGraphic.uvRect;
                        }
                        RawImage backdrop = RequireRect(canvas, "Battle Scene Art").GetComponent<RawImage>();
                        record.background = backdrop.texture == null ? null : backdrop.texture.name;
                        Rect enemyHudBounds = WorldRect(RequireRect(canvas, "Enemy Resource HUD"));
                        Rect endTurnBounds = WorldRect(RequireRect(canvas, "End Battle Turn"));
                        record.endTurnAboveEnemyStatus = endTurnBounds.yMin >= enemyHudBounds.yMax;

                        CaptureFrame(camera, target, new Rect(-Width * 0.5f, -Height * 0.5f, Width, Height), directory,
                            record, prefix + "_Battle.png", (boss ? "首领战" : "普通战") + " · " + BattleEnvironmentArtCatalog.LabelFor(controller.Session.BattleTerrain), "Full 1920 × 1080 UI");
                        if (boss)
                        {
                            Rect bounds = WorldRect(enemyShip);
                            bounds = Rect.MinMaxRect(bounds.xMin - 90f, bounds.yMin - 80f, bounds.xMax + 90f, bounds.yMax + 45f);
                            CaptureFrame(camera, target, bounds, directory, record, prefix + "_BossSurface.png", "首领船体透明边缘与海面近景", "New camera framing of the enemy ship plus surrounding sea; no image enlargement or retouching");
                        }
                        if (caseIndex == 0)
                            CaptureFrame(camera, target, WorldRect(RequireRect(canvas, "Battle Hand")), directory, record,
                                prefix + "_CardTypes.png", "五种卡牌底板对比", "New camera framing of the actual hand; one card per gameplay type arranged in the temporary session");
                        record.complete = true;
                        report.successfulBattleCount += 1;
                        report.successfulImageCount += record.frames.Count(frame => frame.complete);
                    }
                    catch (Exception exception)
                    {
                        record.error = exception.Message;
                        report.errors.Add(prefix + ": " + exception.Message);
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        if (host != null) { UnityEngine.Object.DestroyImmediate(host); host = null; }
                        activeRecord = null;
                    }
                }
            }
            finally
            {
                activeRecord = null;
                Application.logMessageReceived -= collectErrors;
                RenderTexture.active = previousTarget;
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
                if (previousEvents != null)
                    previousEvents.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                totalWatch.Stop();
                report.totalElapsedMilliseconds = totalWatch.Elapsed.TotalMilliseconds;
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulBattleCount != report.expectedBattleCount ||
                report.battles.Any(record => record.missingTextureCount > 0 || record.materialErrorCount > 0 || record.diagnostics.Count > 0))
                throw new InvalidOperationException("Battle surface review incomplete or produced rendering errors. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static void EnterBattle(CoreVoyageSession session, EventTerrainConstraint terrain, bool boss)
        {
            CoreEncounter encounter = boss ? session.Map.Nodes.First(node => node.IsBoss).Encounter :
                new CoreEncounter("battle_surface_review", "航路伏击", "海战", "临时美术验收遭遇。", true, false, 2, 90);
            CoreMapNode node = new CoreMapNode
                { Id = 999, Position = session.Position, Encounter = encounter, TerrainConstraint = terrain, IsBoss = boss };
            FieldInfo activeNode = typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo startBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (activeNode == null || startBattle == null) throw new MissingMemberException("Temporary battle setup requires activeNode and StartBattle.");
            activeNode.SetValue(session, node);
            startBattle.Invoke(session, new object[] { encounter, boss });
        }

        private static void ShowAllCardTypes(CoreBattleState battle)
        {
            battle.Hand.Clear();
            HashSet<string> types = new HashSet<string>();
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index += 1)
                if (types.Add(CoreVoyageSession.CardLibrary[index].Type)) battle.Hand.Add(index);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
            method.Invoke(target, null);
        }

        private static void InspectUi(Canvas canvas, BattleRecord record)
        {
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>();
            record.graphicCount = graphics.Length;
            HashSet<string> shaders = new HashSet<string>();
            foreach (Graphic graphic in graphics)
            {
                if (graphic is RawImage raw && raw.texture == null)
                {
                    record.missingTextureCount += 1;
                    record.diagnostics.Add("Missing RawImage texture: " + graphic.name);
                }
                Material material = graphic.materialForRendering;
                if (material == null || material.shader == null || !material.shader.isSupported ||
                    material.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    record.materialErrorCount += 1;
                    record.diagnostics.Add("Missing/unsupported UI material: " + graphic.name);
                }
                else shaders.Add(material.shader.name);
                if (graphic is Text label && label.font != null && label.font.dynamic && !string.IsNullOrEmpty(label.text))
                    label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            }
            record.materialShaders = shaders.OrderBy(item => item).ToList();
            Canvas.ForceUpdateCanvases();
            string[] names = { "Battle Card", "Battle Scene Art", "Player Ship Art", "Enemy Ship Art", "Battle Hand",
                "Player Battle Hull Bar", "Enemy Battle Hull Bar", "Enemy Intent HUD", "Player Resource HUD", "Enemy Resource HUD", "End Battle Turn" };
            foreach (string name in names)
            {
                RectTransform rect = RequireRect(canvas, name);
                record.bounds.Add(new UiBoundsRecord { name = name, anchoredPosition = rect.anchoredPosition,
                    size = rect.rect.size, worldRect = WorldRect(rect) });
            }
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>().Where(item => item.name.StartsWith("Battle Card ", StringComparison.Ordinal)))
                record.bounds.Add(new UiBoundsRecord { name = rect.name, anchoredPosition = rect.anchoredPosition,
                    size = rect.rect.size, worldRect = WorldRect(rect) });
        }

        private static RectTransform RequireRect(Canvas canvas, string name)
        {
            RectTransform rect = canvas.GetComponentsInChildren<RectTransform>().FirstOrDefault(item => item.name == name);
            if (rect == null) throw new InvalidOperationException("Required battle UI node is missing: " + name);
            return rect;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
                corners.Max(point => point.x), corners.Max(point => point.y));
        }

        private static void CaptureFrame(Camera camera, RenderTexture target, Rect worldView, string directory,
            BattleRecord record, string file, string label, string framing)
        {
            float worldHeight = Mathf.Max(worldView.height, worldView.width / camera.aspect);
            camera.orthographicSize = worldHeight * 0.5f;
            camera.transform.position = new Vector3(worldView.center.x, worldView.center.y, -1000f);
            FrameRecord frame = new FrameRecord { file = file, label = label, framing = framing,
                cameraWorldWidth = worldHeight * camera.aspect, cameraWorldHeight = worldHeight };
            record.frames.Add(frame);
            Stopwatch watch = Stopwatch.StartNew();
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
            SaveFrame(target, Path.Combine(directory, file));
            watch.Stop();
            frame.renderMilliseconds = watch.Elapsed.TotalMilliseconds;
            frame.complete = true;
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
                int minimum = 255, maximum = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int brightness = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    minimum = Math.Min(minimum, brightness);
                    maximum = Math.Max(maximum, brightness);
                }
                if (maximum - minimum < 20) throw new InvalidOperationException("Nearly uniform frame; battle preview did not render.");
                File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Report report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>海战界面 · 海面与卡牌验收</title><style>body{margin:28px;background:#071f2a;color:#e7e1ce;font:16px system-ui}article{padding:22px;margin:24px 0;background:#103440;border:1px solid #5c7b80}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(550px,1fr));gap:18px}figure{margin:0}img{width:100%;display:block;margin:12px 0}small{display:block;color:#bdd0cd;line-height:1.6}a{color:#efc37a}p{line-height:1.7}summary{cursor:pointer}</style>");
            html.Append("<h1>海战界面 · 海面与卡牌验收</h1><p>").Append(report.successfulBattleCount).Append(" / ").Append(report.expectedBattleCount)
                .Append(" 组战斗，").Append(report.successfulImageCount).Append(" 张真实 Unity 截图。覆盖六种海战地形和三条航线首领。临时预览手牌包含五种类型，以便对比卡牌底板；不改变正式牌组。</p><p>船体近景与手牌近景是独立相机渲染，无图片后期处理。悬停与出牌动画由 Play Mode 测试验证，未在静态截图中伪造。<a href=\"report.json\">完整材质、布局边界与诊断数据</a></p>");
            foreach (BattleRecord battle in report.battles)
            {
                html.Append("<article><h2>").Append(Escape(battle.route)).Append(" · ").Append(Escape(battle.enemy)).Append(" · ").Append(Escape(battle.terrain))
                    .Append("</h2><p>构建 ").Append(battle.buildMilliseconds.ToString("0")).Append(" ms · 缺失纹理 ").Append(battle.missingTextureCount)
                    .Append(" · 材质错误 ").Append(battle.materialErrorCount).Append(" · 结束回合按钮在敌舰状态框上方：").Append(battle.endTurnAboveEnemyStatus ? "是" : "否").Append("</p>");
                if (!battle.complete) html.Append("<p>捕获失败：").Append(Escape(battle.error)).Append("</p>");
                if (battle.diagnostics.Count > 0)
                {
                    html.Append("<details><summary>诊断信息</summary><ul>");
                    foreach (string diagnostic in battle.diagnostics) html.Append("<li>").Append(Escape(diagnostic)).Append("</li>");
                    html.Append("</ul></details>");
                }
                html.Append("<section>");
                foreach (FrameRecord frame in battle.frames.Where(item => item.complete))
                    html.Append("<figure><figcaption>").Append(Escape(frame.label)).Append("<small>").Append(Escape(frame.framing)).Append("</small></figcaption><a href=\"")
                        .Append(Escape(frame.file)).Append("\"><img src=\"").Append(Escape(frame.file)).Append("\" loading=\"lazy\"></a></figure>");
                html.Append("</section></article>");
            }
            html.Append("</html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
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
                if (arguments[index] == "-battleSurfaceCapturePath") return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            return Path.Combine(project, "Artifacts", "BattleSurfaces", "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
    }
}
