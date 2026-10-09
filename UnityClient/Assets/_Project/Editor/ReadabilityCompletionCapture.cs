// Disposable, read-only Editor previews. Never enters PlayMode, saves a scene,
// invokes purchase/recruitment/departure callbacks, or writes PlayerPrefs/profiles.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public static class ReadabilityCompletionCapture
    {
        private const int Layer = 31;
        [Serializable] public sealed class Report
        {
            public string scope = "Disposable live production previews at three reading sizes. Actual enabled CanvasScaler is verified against the render target. Geometry includes masked/offscreen scroll contents and labels behind overlays; it is evidence for visual review, not an automatic accessibility pass. No PlayerPrefs, saves or scene assets are written.";
            public string capturedAtUtc;
            public List<MenuFrame> frames = new List<MenuFrame>();
        }
        [Serializable] public sealed class MenuFrame
        {
            public string state, file, error, scrollPosition;
            public int width, height, level;
            public bool verifiedScaler;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor;
            public string screenMatchMode;
            public List<TextGeometry> text = new List<TextGeometry>();
            public List<ScrollGeometry> scrolls = new List<ScrollGeometry>();
        }
        [Serializable] public sealed class ScrollGeometry
        {
            public string path;
            public float normalizedPosition, viewportHeight, contentHeight;
        }
        [Serializable] public sealed class TextGeometry
        {
            public string path, content, fontName;
            public bool meshAvailable, meshOutsideTarget, rectOutsideTarget, bestFit, withinScrollView;
            public Rect rectPixels, meshPixels;
            public int meshVertexCount, fontSize, generatedCharacters, completeCharacters;
            public float preferredHeight, rectHeight;
        }

        // -executeMethod ShatteredSail.Editor.ReadabilityCompletionCapture.CaptureFromCommandLine
        // -readabilityOutput D:/Game/Artifacts/ReadabilityCompletion20261009/MarketCapture
        // -readabilityScreens Market (or comma-separated groups/states; omit for all)
        public static void CaptureFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Output must be new/empty: " + output);
            Directory.CreateDirectory(output);
            string[] states = CaptureStates();
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            int? previous = GameReadability.OverrideLevel;
            try
            {
                for (int level = 0; level < 3; level++)
                {
                    GameReadability.OverrideLevel = level;
                    foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720) })
                    foreach (string state in states)
                    {
                        var frame = new MenuFrame { state = state, level = level, width = size.x, height = size.y,
                            scrollPosition = state.EndsWith("Bottom", StringComparison.Ordinal) ? "bottom" : "top",
                            file = state + "_L" + level + "_" + size.x + "x" + size.y + ".png" };
                        report.frames.Add(frame);
                        try { CaptureFrame(output, frame); }
                        catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                        File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                        WriteGallery(output, report);
                    }
                }
            }
            finally { GameReadability.OverrideLevel = previous; }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error)))
                throw new InvalidOperationException("Readability capture incomplete; inspect report.json and index.html.");
            Debug.Log("READABILITY_CAPTURE_OK: " + report.frames.Count + " frames at " + output);
        }

        private static string[] CaptureStates()
        {
            var groups = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase) {
                { "Menu", new[] { "Home", "HomeBottom", "Settings" } },
                { "Settings", new[] { "Settings" } },
                { "Achievements", new[] { "Achievements", "AchievementsBottom" } },
                { "NewGameConfirmation", new[] { "NewGameConfirmation", "NewGameConfirmationBottom" } },
                { "PortSettings", new[] { "PortSettings" } },
                { "Investment", new[] { "Maintenance", "MaintenanceBottom" } },
                { "ComponentShop", new[] { "ComponentShop", "ComponentShopBottom", "ComponentShopLastPage", "ComponentShopDetail", "ComponentShopDetailBottom", "ComponentShopEffects", "ComponentShopStory", "ComponentShopSpecialCards" } },
                { "Market", new[] { "Market", "MarketLastPage", "MarketChoice" } },
                { "Warehouse", new[] { "Warehouse", "WarehouseBottom" } },
                { "DeckBuilder", new[] { "DeckBuilder", "DeckBuilderBottom", "Battle" } },
                { "VoyageDeparture", new[] { "VoyageDeparture", "VoyageDepartureBottom", "VoyageDepartureCrew", "VoyageDepartureCrewBottom" } },
                { "Navigation", new[] { "Navigation", "ForecastOpen" } },
                { "NavigationTutorial", new[] { "NavigationTutorial", "NavigationTutorialBottom" } },
                { "Encounter", new[] { "Encounter", "EncounterBottom" } },
                { "Tavern", new[] { "TavernCrewDetail", "TavernCrewDetailBottom", "TavernCrewDetail1", "TavernCrewDetail1Bottom", "TavernCrewDetail2", "TavernCrewDetail3" } },
                { "Shipyard", new[] { "ShipyardDetail", "ShipyardDetailBottom", "ShipyardLore", "ShipyardLoreBottom", "ShipyardEquipment", "ShipyardEquipmentBottom" } }
            };
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args,"-readabilityScreens");
            if (index < 0) return groups.Values.SelectMany(v => v).Distinct().ToArray();
            if (index + 1 >= args.Length) throw new ArgumentException("-readabilityScreens needs comma-separated group/state names.");
            string[] selected = args[index+1].Split(new[]{',',';'},StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            var valid = new HashSet<string>(groups.Values.SelectMany(v=>v),StringComparer.Ordinal);
            return selected.SelectMany(s => groups.TryGetValue(s,out string[] group) ? group :
                valid.Contains(s) ? new[]{s} : throw new ArgumentException("Unknown readability group/state: " + s)).Distinct().ToArray();
        }
        private static FieldInfo Field(string name) => typeof(CoreGameplayDemoController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)
            ?? throw new MissingFieldException(name);
        private static void Invoke(CoreGameplayDemoController controller,string name,params object[] arguments)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)
                ?? throw new MissingMethodException(name);
            try { method.Invoke(controller,arguments); }
            catch(TargetInvocationException e) { throw e.InnerException ?? e; }
        }
        private static void WriteGallery(string output,Report report)
        {
            string Escape(string value) => (value ?? "").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>字体可读性验收</title><style>body{font:16px system-ui;background:#181a20;color:#eee;margin:24px}img{width:100%;height:auto}article{margin:28px 0;border:1px solid #555;padding:12px}small{color:#bbb}select{font:inherit;padding:6px}pre{white-space:pre-wrap;color:#ffa090}</style><h1>字体可读性验收</h1><p>三档字体、1080p 与 720p；滚动底部和首屏单独展示。几何报告包含滚动视口外文本，需结合截图判断。</p><label>字号 <select id='size'><option value=''>全部</option><option value='0'>标准</option><option value='1'>大字</option><option value='2'>特大</option></select></label>");
            foreach(MenuFrame frame in report.frames)
            {
                html.Append("<article data-level='").Append(frame.level).Append("'><h2>").Append(Escape(frame.state)).Append(" · ").Append(new[]{"标准","大字","特大"}[frame.level]).Append(" · ").Append(frame.width).Append('×').Append(frame.height).Append("</h2>");
                if(string.IsNullOrEmpty(frame.error)) html.Append("<a href='").Append(Escape(frame.file)).Append("'><img loading='lazy' src='").Append(Escape(frame.file)).Append("'></a>");
                else html.Append("<pre>").Append(Escape(frame.error)).Append("</pre>");
                html.Append("<small>CanvasScaler verified: ").Append(frame.verifiedScaler).Append("; labels: ").Append(frame.text.Count).Append("</small></article>");
            }
            html.Append("<script>document.querySelector('#size').onchange=e=>document.querySelectorAll('article').forEach(a=>a.hidden=e.target.value!==''&&a.dataset.level!==e.target.value)</script>");
            File.WriteAllText(Path.Combine(output,"index.html"),html.ToString(),new UTF8Encoding(false));
        }
        private static void CaptureFrame(string output, MenuFrame frame)
        {
            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable UI Audit Main Menu");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                string state = frame.state.Replace("Bottom", "");
                bool menu = state == "Home" || state == "Settings" || state == "Achievements" || state == "NewGameConfirmation";
                string mapped = state == "MarketLastPage" || state == "MarketChoice" ? "Market" :
                    state == "ComponentShopLastPage" ? "ComponentShop" :
                    state == "ComponentShopEffects" || state == "ComponentShopStory" || state == "ComponentShopSpecialCards" ? "ComponentShopDetail" :
                    state == "PortSettings" ? "PortHub" :
                    state == "NavigationTutorial" ? "Navigation" :
                    state == "ForecastOpen" ? "Navigation" : state == "VoyageDepartureCrew" ? "VoyageDeparture" :
                    state.StartsWith("TavernCrewDetail", StringComparison.Ordinal) ? "TavernCrewDetail" :
                    state == "ShipyardLore" || state == "ShipyardEquipment" ? "ShipyardDetail" : state;
                if (!Enum.TryParse(mapped, out CoreGameplayPreviewScreen screen) && !menu)
                    throw new ArgumentException("Unknown capture state: " + frame.state);
                CoreGameplayDemoController controller = null;
                if (menu) host.AddComponent<MainMenuController>();
                else
                {
                    controller = host.AddComponent<CoreGameplayDemoController>();
                    controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                }
                host.SetActive(true);
                if (state == "Achievements")
                {
                    MethodInfo open = typeof(MainMenuController).GetMethod("OpenAchievements", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException("MainMenuController.OpenAchievements");
                    open.Invoke(host.GetComponent<MainMenuController>(), null);
                }
                if (state == "PortSettings") Invoke(controller, "OpenPortSettings");
                if (state == "NavigationTutorial")
                {
                    // Read-only fixture: match the navigation layout tests without
                    // starting a real tutorial, voyage, or persistence callback.
                    Field("tutorialActive").SetValue(controller, true);
                    Invoke(controller, "Render");
                    foreach (string name in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
                        if (!host.GetComponentsInChildren<Text>(true).Any(text => text.name == name))
                            throw new InvalidOperationException("Tutorial fixture did not produce " + name);
                }
                if (state == "Encounter")
                {
                    // The same optional three-choice fixture used by the layout
                    // tests. Choices and Skip are rendered but never invoked.
                    var encounter = new CoreEncounter("event_reef_broken_mast", "断桅求援", "航海事件",
                        "远海首领舰盘踞在十日航程之外，控制着通往新港的关键海域。", false, false, 3, 0,
                        new CoreEncounterChoice("记录星位", "声望 +8、显示 2 个隐藏点", .5, new VoyageEffect { Gold = 10 }),
                        new CoreEncounterChoice("改变航向", "安全通过，主要成本来自时间", 0, new VoyageEffect { Gold = 20 }),
                        new CoreEncounterChoice("木匠加固后打捞", "啤酒 -1、金币 +80、士气 -1", 1, new VoyageEffect { Gold = 30 }));
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    typeof(CoreVoyageSession).GetProperty("ActiveEncounter", flags).SetValue(controller.Session, encounter);
                    typeof(CoreVoyageSession).GetProperty("Phase", flags).SetValue(controller.Session, CoreDemoPhase.Encounter);
                    Invoke(controller, "Render");
                    if (host.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Encounter Choice ", StringComparison.Ordinal)) != 3 ||
                        !host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Skip Encounter"))
                        throw new InvalidOperationException("Optional encounter fixture must render three choices and Skip.");
                }
                if (state == "ComponentShopEffects" || state == "ComponentShopStory" || state == "ComponentShopSpecialCards")
                    host.GetComponentsInChildren<Button>(true).Single(button => button.name == "ComponentDossierPage" + state.Substring("ComponentShop".Length)).onClick.Invoke();
                if (state == "ComponentShopLastPage")
                    for (int attempt = 0; attempt < 100; attempt++)
                    {
                        Button next = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Component Shop Next Display");
                        if (!next.interactable) break;
                        next.onClick.Invoke();
                    }
                if (state.StartsWith("TavernCrewDetail", StringComparison.Ordinal))
                {
                    string page = state.Substring("TavernCrewDetail".Length);
                    controller.ConfigureEditorCrewDossier(0, string.IsNullOrEmpty(page) ? 0 : int.Parse(page));
                }
                if (state == "ShipyardLore") controller.ConfigureEditorShipyard(11, true, 1);
                if (state == "ShipyardEquipment") controller.ConfigureEditorShipyard(0, true, 2);
                if (state == "MarketLastPage")
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        Button next = host.GetComponentsInChildren<Button>(true).Single(b => b.name == "Market Next Page");
                        if (!next.interactable) break;
                        next.onClick.Invoke(); // Known pagination callback changes the disposable view only.
                    }
                if (state == "MarketChoice")
                {
                    var quotes = (System.Collections.IDictionary)Field("marketPurchaseQuotes").GetValue(controller);
                    Invoke(controller, "BuildMarketPurchaseChoice", quotes[PortMarketItem.Water]);
                }
                if (state == "VoyageDepartureCrew")
                {
                    Field("departureCrewConfigurationOpen").SetValue(controller, true);
                    Invoke(controller, "Render");
                }
                if (state == "ForecastOpen")
                    host.GetComponentsInChildren<Button>(true).Single(b => b.name == "Forecast Details").onClick.Invoke();
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name ==
                    (menu ? MainMenuController.EditorPreviewCanvasName : CoreGameplayDemoController.EditorPreviewCanvasName));
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (menu && child.name == "Settings Panel") child.gameObject.SetActive(state == "Settings");
                    if (menu && child.name == "Achievements Panel") child.gameObject.SetActive(state == "Achievements");
                    if (menu && child.name == "New Game Confirmation") child.gameObject.SetActive(state == "NewGameConfirmation");



                    child.gameObject.layer = Layer;
                }
                GameObject cameraHost = new GameObject("Disposable UI Audit Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                rt = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                camera.targetTexture = rt;
                camera.rect = new Rect(0,0,1,1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                // No assignment to canvas.scaleFactor, RectTransform size or localScale.
                // Calling the original scaler Handle preserves production calculations.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    foreach (ScrollRect scroll in canvas.GetComponentsInChildren<ScrollRect>(false))
                    {
                        scroll.StopMovement();
                        scroll.verticalNormalizedPosition = frame.scrollPosition == "bottom" ? 0f : 1f;
                    }
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.scaleFactor = canvas.scaleFactor;
                frame.screenMatchMode = scaler.screenMatchMode.ToString();
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr,2), Mathf.Log(hr,2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled &&
                    Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<0.5f && Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<0.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.canvasPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.cameraPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<0.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1f &&
                    Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1f;
                if (!frame.verifiedScaler)
                    throw new InvalidOperationException("Target texture did not drive actual scaler/display geometry. Refuse false multi-resolution capture. Read recorded display/scale fields and use fixed Game View instead.");
                foreach (ScrollRect scroll in canvas.GetComponentsInChildren<ScrollRect>(false))
                    frame.scrolls.Add(new ScrollGeometry { path = PathOf(scroll.transform, canvas.transform),
                        normalizedPosition = scroll.verticalNormalizedPosition,
                        viewportHeight = scroll.viewport == null ? ((RectTransform)scroll.transform).rect.height : scroll.viewport.rect.height,
                        contentHeight = scroll.content == null ? 0 : scroll.content.rect.height });
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                    var geometry = new TextGeometry { path = PathOf(label.transform, canvas.transform), content = label.text,
                        fontSize = label.fontSize, fontName = label.font == null ? "<missing>" : label.font.name,
                        bestFit = label.resizeTextForBestFit, preferredHeight = label.preferredHeight,
                        rectHeight = label.rectTransform.rect.height,
                        withinScrollView = label.GetComponentInParent<ScrollRect>() != null };
                    TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                    using (var shown = new TextGenerator())
                    using (var full = new TextGenerator())
                    {
                        shown.Populate(label.text, settings);
                        settings.verticalOverflow = VerticalWrapMode.Overflow;
                        full.Populate(label.text, settings);
                        geometry.generatedCharacters = shown.characterCount;
                        geometry.completeCharacters = full.characterCount;
                    }
                    var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                    geometry.rectPixels = Bounds(corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)));
                    geometry.rectOutsideTarget = Outside(geometry.rectPixels, frame.width, frame.height);
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    try
                    {
                        geometry.meshVertexCount = mesh == null ? 0 : mesh.vertexCount;
                        geometry.meshAvailable = mesh != null && mesh.vertexCount > 0;
                        if (geometry.meshAvailable)
                        {
                            geometry.meshPixels = Bounds(mesh.vertices.Select(v => (Vector2)camera.WorldToScreenPoint(label.transform.TransformPoint(v))));
                            geometry.meshOutsideTarget = Outside(geometry.meshPixels, frame.width, frame.height);
                        }
                    }
                    finally { /* CanvasRenderer owns the returned mesh. */ }
                    frame.text.Add(geometry);
                }
                RenderTexture.active = rt;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height), 0,0);
                pixels.Apply(false,false);
                Color32[] samples = pixels.GetPixels32(); int low=255,high=0;
                for (int i=0;i<samples.Length;i+=997) { int b=(samples[i].r+samples[i].g+samples[i].b)/3; low=Math.Min(low,b); high=Math.Max(high,b); }
                if (high-low<20) throw new InvalidOperationException("Nearly blank frame; render not accepted.");
                File.WriteAllBytes(Path.Combine(output,frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>(); for (Transform t=item;t!=null && t!=root;t=t.parent) names.Insert(0,t.name); return string.Join("/",names);
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] p=points.ToArray(); return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
        }
        private static bool Outside(Rect r,int width,int height) { return r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > width+0.5f || r.yMax > height+0.5f; }
        private static void RequireEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required; omit -nographics.");
        }
        private static string Output()
        {
            string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-readabilityOutput");
            if(index<0 || index+1>=args.Length) throw new ArgumentException("Pass -readabilityOutput NEW_ABSOLUTE_OUTPUT_DIR");
            if(!Path.IsPathRooted(args[index+1]))throw new ArgumentException("Absolute output required.");
            return Path.GetFullPath(args[index+1]);
        }
    }
}
