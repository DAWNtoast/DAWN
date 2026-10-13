using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ShatteredSail.Core.Gameplay;
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
    // Install only in the task's disposable validation project. Production UI,
    // in-memory preview fixtures, no craft/purchase callback and no scene save.
    public static class TreasureTableSceneCapture
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Private Editor preview scenes using production UI and Expand scaling. Includes actual GraphicRaycaster probes, projected paper corners, text fit and 720p/1080p/1440p/4K/16:10 captures. No gameplay callbacks; PlayMode tests verify identification, crafting, folio and departure.";
            public List<Page> pages = new List<Page>();
        }
        [Serializable] private sealed class Page
        {
            public string state, port, file, background;
            public int width, height;
            public float scale;
            public bool passed;
            public List<Label> labels = new List<Label>();
            public List<Probe> probes = new List<Probe>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Label
        {
            public string name, text;
            public Rect bounds;
            public float minimumPixelFontSize, renderedPixelFontSize, preferredHeight, availableHeight;
        }
        [Serializable] private sealed class Probe
        {
            public string target, sample, firstHit, hitButton;
            public Vector2 point;
            public bool passed;
        }

        [Serializable] private sealed class SavedReport
        {
            public bool passed, allDiskScenesUnchanged;
            public int diskSceneCount;
            public string scope = "Reopens six persisted treasure preview scenes without configuring or refreshing their controllers. Checks saved asset references, layout and state-specific folio nodes. SHA256 comparison covers every .unity file under Assets. Does not invoke callbacks, save scenes, or rebuild previews.";
            public List<SavedPage> pages = new List<SavedPage>();
            public List<string> changedDiskScenes = new List<string>();
        }
        [Serializable] private sealed class SavedPage
        {
            public string path, state, background, sha256Before, sha256After, folioTitle;
            public bool passed, persistedUi, completeUi, sceneDirtyOnLoad, sourceSceneUnchanged;
            public Vector2 referenceResolution;
            public int validTextureReferences, validFontReferences;
            public List<SavedBoard> boards = new List<SavedBoard>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class SavedBoard
        {
            public int index;
            public string name, progress, texture;
            public Vector2 namePosition, progressPosition;
            public List<Vector2> paperCorners = new List<Vector2>();
        }

        public static void CheckSaved()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!string.Equals(project, "D:/Game/tmp/treasure-table-scene-20261013", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requires the dedicated treasure-table validation project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saved-scene inspection requires stopped Edit mode.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save modified open scenes before inspection.");
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-treasureTableSavedPath");
            string output = argument >= 0 && argument + 1 < args.Length ? args[argument + 1] : null;
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output) || Directory.Exists(output))
                throw new InvalidOperationException("Supply a new absolute -treasureTableSavedPath directory.");
            Directory.CreateDirectory(output);
            string root = "Assets/_Project/Scenes/";
            string[] paths = { root + "UIPreviews/UI_TreasureMap.unity" };
            TreasureMapPreviewState[] states = { TreasureMapPreviewState.MapUnopened };
            Dictionary<string,string> before = SceneHashes();
            var report = new SavedReport { diskSceneCount = before.Count };
            try
            {
                // The project is dedicated to this task and contains no dirty scenes.
                // Start empty so each requested file is genuinely reopened from disk.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                for (int index = 0; index < paths.Length; index++)
                {
                    var page = new SavedPage { path = paths[index], state = states[index].ToString(), sha256Before = FileHash(Path.Combine(project,paths[index])) };
                    report.pages.Add(page);
                    Scene scene = default;
                    try
                    {
                        scene = EditorSceneManager.OpenScene(paths[index], OpenSceneMode.Additive);
                        page.sceneDirtyOnLoad = scene.isDirty;
                        CoreGameplayDemoController controller = scene.GetRootGameObjects()
                            .SelectMany(item => item.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                        CheckSavedPage(page, controller, states[index]);
                    }
                    catch (Exception error) { page.errors.Add(error.ToString()); }
                    finally
                    {
                        if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                        page.sha256After = FileHash(Path.Combine(project,paths[index]));
                        page.sourceSceneUnchanged = page.sha256Before == page.sha256After;
                        if (!page.sourceSceneUnchanged) page.errors.Add("Inspection modified the saved source scene.");
                        page.passed = page.errors.Count == 0;
                    }
                }
            }
            finally
            {
                Dictionary<string,string> after = SceneHashes();
                foreach (string path in before.Keys.Union(after.Keys))
                    if (!before.TryGetValue(path,out string first) || !after.TryGetValue(path,out string second) || first != second)
                        report.changedDiskScenes.Add(path);
                report.allDiskScenesUnchanged = report.changedDiskScenes.Count == 0;
                report.passed = report.pages.Count == 6 && report.pages.All(page => page.passed) && report.allDiskScenesUnchanged;
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            }
            if (!report.passed) throw new InvalidOperationException("Saved treasure previews failed inspection; see report.json.");
            Debug.Log("TREASURE_TABLE_SAVED_OK: 6 reopened previews; " + report.diskSceneCount + " disk scenes unchanged; " + output);
        }

        private static void CheckSavedPage(SavedPage page, CoreGameplayDemoController controller, TreasureMapPreviewState state)
        {
            GameObject host = controller.gameObject;
            page.persistedUi = controller.EditorPreviewPersistsGeneratedUi;
            page.completeUi = controller.HasCompleteEditorPreview;
            if (!page.persistedUi || !page.completeUi || controller.EditorPreviewScreen != CoreGameplayPreviewScreen.TreasureMap)
                page.errors.Add("Saved controller does not own a complete persisted treasure UI.");
            var serialized = new SerializedObject(controller);
            if (serialized.FindProperty("editorPreviewTreasureState").enumValueIndex != (int)state)
                page.errors.Add("Serialized treasure state differs from the saved scene's intended state.");
            Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            page.referenceResolution = scaler.referenceResolution;
            if (!canvas.enabled || canvas.GetComponent<GraphicRaycaster>() == null ||
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand ||
                Vector2.Distance(page.referenceResolution,new Vector2(1920,1080)) > .01f)
                page.errors.Add("Saved Canvas/scaler/raycaster configuration is incomplete.");
            RawImage background = Find<RawImage>(host,"Treasure Background");
            page.background = AssetDatabase.GetAssetPath(background.texture);
            if (background.texture == null || background.texture.name != "treasure_worktable_background_v3" || string.IsNullOrEmpty(page.background))
                page.errors.Add("Saved background does not resolve to the approved v3 asset.");
            RectTransform room = Find<RectTransform>(host,"Treasure Panel");
            RectTransform workbench = Find<RectTransform>(host,"Treasure Workbench");
            if (workbench.GetComponent<Image>() != null ||
                host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Treasure Workbench Heading"))
                page.errors.Add("The saved scene retains the old floating workbench panel.");
            foreach (string name in new[] { "Treasure Workbench Status", "Treasure Workbench Notice", "Treasure Identify Status", "Treasure Folio Status", "Treasure Purify Status" })
                if (string.IsNullOrWhiteSpace(Find<Text>(host,name).text)) page.errors.Add("Saved status is empty: " + name);
            foreach (string name in new[] { "Treasure Identify Fragment", "Treasure Open Folio", "Treasure Purify Curse" })
            {
                Button button = Find<Button>(host,name);
                Image hit = button.GetComponent<Image>();
                Text label = button.GetComponentInChildren<Text>(true);
                if (!button.enabled || hit == null || !hit.raycastTarget || hit.color.a != 0 || hit.sprite != null ||
                    button.targetGraphic != label || string.IsNullOrWhiteSpace(label.text))
                    page.errors.Add("Saved tabletop object hotspot is incomplete: " + name);
            }
            for (int index = 0; index < 3; index++)
            {
                TreasureChartDisplayLayout layout = TreasureChartDisplayLayout.For(index);
                Button button = Find<Button>(host,"Treasure Route " + index);
                RawImage chart = Find<RawImage>(host,"Treasure Route Art " + index);
                TabletopQuadProjection projection = chart.GetComponent<TabletopQuadProjection>();
                Text name = Find<Text>(host,"Treasure Route Name " + index);
                Text progress = Find<Text>(host,"Treasure Route Progress " + index);
                int fragments = state == TreasureMapPreviewState.FragmentsReady && index == 0 ? 4 : 0;
                var board = new SavedBoard { index = index, name = name.text, progress = progress.text,
                    texture = AssetDatabase.GetAssetPath(chart.texture), namePosition = room.InverseTransformPoint(name.transform.position),
                    progressPosition = room.InverseTransformPoint(progress.transform.position) };
                page.boards.Add(board);
                if (board.name != CoreTreasureHuntCatalog.Ranks[index].Name + "藏宝图" || !board.progress.Contains(fragments + "/4"))
                    page.errors.Add("Saved map name/progress mismatch: " + index);
                if (fragments == 4 && !board.progress.Contains("点击合成")) page.errors.Add("Ready map lost its craft prompt.");
                if (Vector2.Distance(board.namePosition,layout.NamePosition) > .05f || Vector2.Distance(board.progressPosition,layout.LabelPosition) > .05f)
                    page.errors.Add("Saved map plaques moved off their production positions: " + index);
                if (!button.enabled || !button.interactable || button.GetComponent<Image>().raycastTarget || !chart.raycastTarget || projection == null)
                    page.errors.Add("Saved map no longer uses its projected paper as the hit target: " + index);
                if (chart.texture == null || chart.texture.name != "treasure_chart_" + CoreTreasureHuntCatalog.Ranks[index].Id || string.IsNullOrEmpty(board.texture))
                    page.errors.Add("Saved chart asset reference is missing: " + index);
                foreach (Vector2 uv in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                {
                    Vector2 projected = projection.Project(uv);
                    Vector2 actual = room.InverseTransformPoint(chart.transform.TransformPoint(chart.rectTransform.rect.min + projected * chart.rectTransform.rect.size));
                    Vector2 expected = layout.Position + (ExpectedProjection(layout,uv) - Vector2.one * .5f) * (layout.Size - Vector2.one * 5f);
                    board.paperCorners.Add(actual);
                    if (Vector2.Distance(actual,expected) > .05f) page.errors.Add("Saved paper corner differs from production layout: " + index + " / " + uv);
                }
                for (int piece = 0; piece < 4; piece++)
                    if (Find<RawImage>(host,"Treasure Fragment " + index + " " + piece).color.a != (piece < fragments ? 1f : 0f))
                        page.errors.Add("Saved fragment visibility mismatch: " + index + " / " + piece);
            }
            RectTransform folio = host.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(item => item.name == "Treasure Folio");
            bool needsFolio = state == TreasureMapPreviewState.MapDecoded || state == TreasureMapPreviewState.RewardClaim;
            if ((folio != null) != needsFolio) page.errors.Add("Saved folio visibility does not match state.");
            if (needsFolio && folio != null)
            {
                page.folioTitle = Find<Text>(host,"Treasure Folio Title").text;
                if (page.folioTitle != (state == TreasureMapPreviewState.MapDecoded ? "未使用的藏宝图" : "已使用的藏宝图"))
                    page.errors.Add("Saved folio opened the wrong archive view.");
                foreach (string name in new[] { "Treasure Close Folio", "Treasure Folio Back", "Treasure Browse Unused", "Treasure Browse Used" })
                    if (!Find<Button>(host,name).enabled) page.errors.Add("Disabled saved folio control: " + name);
                Text details = Find<Text>(host,"Treasure Map Details 0");
                if (!details.text.Contains("TREASURE-PREVIEW-WEATHERED-01") || !details.text.Contains("探索度"))
                    page.errors.Add("Saved folio lost its seed/exploration details.");
                if (state == TreasureMapPreviewState.MapDecoded && Find<RawImage>(host,"Treasure Map Preview 0").texture == null)
                    page.errors.Add("Saved unused folio has no chart artwork.");
                if (state == TreasureMapPreviewState.RewardClaim && !details.text.Contains("未记录"))
                    page.errors.Add("Legacy completed preview must retain its unrecorded rank/exploration semantics.");
            }
            foreach (RawImage image in host.GetComponentsInChildren<RawImage>(true))
            {
                if (!image.enabled || !image.gameObject.activeInHierarchy) continue;
                if (image.texture == null || !AssetDatabase.Contains(image.texture)) page.errors.Add("Unresolved saved texture: " + image.name);
                else page.validTextureReferences++;
            }
            foreach (Text label in host.GetComponentsInChildren<Text>(true))
            {
                if (!label.enabled || string.IsNullOrWhiteSpace(label.text)) continue;
                if (label.font == null || !AssetDatabase.Contains(label.font)) page.errors.Add("Unresolved saved font: " + label.name);
                else page.validFontReferences++;
            }
        }

        private static Vector2 ExpectedProjection(TreasureChartDisplayLayout layout, Vector2 uv)
        {
            Vector2 Corner(string name) => (Vector2)typeof(TreasureChartDisplayLayout).GetField(name,BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layout);
            return Vector2.Lerp(Vector2.Lerp(Corner("bottomLeft"),Corner("bottomRight"),uv.x),
                Vector2.Lerp(Corner("topLeft"),Corner("topRight"),uv.x),uv.y);
        }
        private static Dictionary<string,string> SceneHashes() => Directory.GetFiles(Application.dataPath,"*.unity",SearchOption.AllDirectories)
            .ToDictionary(path => path,FileHash,StringComparer.OrdinalIgnoreCase);
        private static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",string.Empty);
        }

        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!string.Equals(project, "D:/Game/tmp/treasure-table-scene-20261013", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requires the dedicated treasure-table validation project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Run stopped Editor with graphics enabled.");
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-treasureTableCapturePath");
            string output = argument >= 0 && argument + 1 < args.Length ? args[argument + 1] : null;
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output) || Directory.Exists(output))
                throw new InvalidOperationException("Supply a new absolute -treasureTableCapturePath directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1280,720), new Vector2Int(1920,1080),
                    new Vector2Int(2560,1440), new Vector2Int(3840,2160), new Vector2Int(1280,800) })
                    foreach (string state in new[] { "Unopened", "MixedProgress", "DecodedFolio", "UsedFolio" })
                        Capture(report, output, state, CoreGameplayPreviewPort.TwilightTide, size.x, size.y);
                foreach (CoreGameplayPreviewPort port in new[] { CoreGameplayPreviewPort.BrokenAnchor, CoreGameplayPreviewPort.WhiteShell,
                    CoreGameplayPreviewPort.BronzeBell, CoreGameplayPreviewPort.SaltCrown, CoreGameplayPreviewPort.GrayGull })
                    Capture(report, output, "MixedProgress", port, 1920,1080);
                report.passed = report.pages.Count == 25 && report.pages.All(page => page.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Treasure table validation failed; inspect report.json.");
            Debug.Log("TREASURE_TABLE_CAPTURE_OK: " + report.pages.Count + " verified frames; " + output);
        }

        private static void Capture(Report report, string output, string state, CoreGameplayPreviewPort port, int width, int height)
        {
            var page = new Page { state = state, port = port.ToString(), width = width, height = height,
                file = "treasure_" + port.ToString().ToLowerInvariant() + "_" + state.ToLowerInvariant() + "_" + width + "x" + height + ".png" };
            report.pages.Add(page);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null, eventHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                host = new GameObject("Treasure Table Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TreasureMap, port, 0);
                controller.ConfigureEditorTreasureState(state == "DecodedFolio" ? TreasureMapPreviewState.MapDecoded :
                    state == "UsedFolio" ? TreasureMapPreviewState.RewardClaim : TreasureMapPreviewState.MapUnopened);
                host.SetActive(true);
                if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Production preview incomplete.");
                if (state == "MixedProgress")
                {
                    controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 900,
                        fragments = 3, identifiedWeatheredFragments = 4, identifiedGildedFragments = 2, identifiedMoonlitFragments = 1 });
                    typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                }
                string treasureBefore = JsonUtility.ToJson(controller.Session.CaptureTreasureProgress());
                cameraHost = new GameObject("Treasure Table Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << 31;
                camera.scene = scene;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                    throw new InvalidOperationException("Unexpected production CanvasScaler.");
                page.scale = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
                Vector2 logicalSize = new Vector2(width / page.scale, height / page.scale);
                scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.scaleFactor = 1;
                RectTransform root = canvas.GetComponent<RectTransform>();
                root.sizeDelta = logicalSize;
                root.position = Vector3.zero;
                root.localScale = Vector3.one;
                camera.orthographicSize = logicalSize.y * .5f;
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                eventHost = new GameObject("Treasure Capture EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventHost, scene);
                EventSystem events = eventHost.GetComponent<EventSystem>();
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                Validate(page, host, camera, events, raycaster, state);
                if (JsonUtility.ToJson(controller.Session.CaptureTreasureProgress()) != treasureBefore)
                    page.errors.Add("Read-only capture changed treasure progress.");
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0,0,width,height),0,0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output,page.file),texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                page.passed = page.errors.Count == 0;
            }
            catch (Exception e) { page.errors.Add(e.ToString()); page.passed = false; }
            finally
            {
                RenderTexture.active = previous;
                if (eventHost != null) UnityEngine.Object.DestroyImmediate(eventHost);
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Validate(Page page, GameObject host, Camera camera, EventSystem events, GraphicRaycaster raycaster, string state)
        {
            RawImage background = Find<RawImage>(host, "Treasure Background");
            page.background = AssetDatabase.GetAssetPath(background.texture);
            if (background.texture == null || background.texture.name != "treasure_worktable_background_v3")
                page.errors.Add("Approved table background was not loaded.");
            bool folio = state == "DecodedFolio" || state == "UsedFolio";
            if (!folio)
            {
                foreach (string name in new[] { "Treasure Workbench Status", "Treasure Workbench Notice",
                    "Treasure Identify Status", "Treasure Folio Status", "Treasure Purify Status" })
                    CheckLabel(page, Find<Text>(host, name), camera);
                if (host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Treasure Workbench Heading"))
                    page.errors.Add("Obsolete floating workbench heading remains.");
                foreach (string name in new[] { "Treasure Identify Fragment", "Treasure Open Folio", "Treasure Purify Curse" })
                {
                    Button button = Find<Button>(host, name);
                    CheckLabel(page, button.GetComponentInChildren<Text>(), camera);
                    RectTransform rect = button.GetComponent<RectTransform>();
                    ProbeHit(page, events, raycaster, button, "center", camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)));
                    Rect bounds = Bounds(rect, camera);
                    if (!InsideScreen(page, bounds)) page.errors.Add("Object hotspot leaves the screen: " + name);
                }
                for (int index = 0; index < 3; index++)
                {
                    Button button = Find<Button>(host, "Treasure Route " + index);
                    RawImage chart = Find<RawImage>(host, "Treasure Route Art " + index);
                    TabletopQuadProjection projection = chart.GetComponent<TabletopQuadProjection>();
                    Text name = Find<Text>(host, "Treasure Route Name " + index);
                    Text progress = Find<Text>(host, "Treasure Route Progress " + index);
                    if (name.text != CoreTreasureHuntCatalog.Ranks[index].Name + "藏宝图") page.errors.Add("Incorrect independent map name: " + index);
                    if (!progress.text.Contains("/4")) page.errors.Add("Missing independent progress: " + index);
                    if (state == "MixedProgress" && !progress.text.Contains(new[] {4,2,1}[index] + "/4")) page.errors.Add("Mixed progress fixture mismatch: " + index);
                    CheckLabel(page, name, camera);
                    CheckLabel(page, progress, camera);
                    if (Bounds(name.rectTransform, camera).Overlaps(Bounds(progress.rectTransform, camera))) page.errors.Add("Name/progress labels overlap: " + index);
                    if (button.GetComponent<Image>().raycastTarget) page.errors.Add("Rectangular map hit target bridges the projected face: " + index);
                    Rect local = chart.rectTransform.rect;
                    foreach (Vector2 uv in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one, Vector2.one * .5f })
                    {
                        Vector2 projected = projection.Project(uv);
                        Vector2 corner = camera.WorldToScreenPoint(chart.transform.TransformPoint(local.min + projected * local.size));
                        if (corner.x < 0 || corner.y < 0 || corner.x > page.width || corner.y > page.height) page.errors.Add("Projected chart corner leaves screen: " + index);
                        // Inset a few pixels so the actual raycaster, rather than boundary tolerance, proves reachability.
                        Vector2 inside = projection.Project(Vector2.Lerp(uv, Vector2.one * .5f, .04f));
                        Vector2 point = camera.WorldToScreenPoint(chart.transform.TransformPoint(local.min + inside * local.size));
                        ProbeHit(page, events, raycaster, button, "chart " + uv, point);
                    }
                }
            }
            else
            {
                foreach (Text label in Find<RectTransform>(host, "Treasure Folio").GetComponentsInChildren<Text>(false))
                    if (!string.IsNullOrEmpty(label.text)) CheckLabel(page, label, camera);
                Button close = Find<Button>(host, "Treasure Close Folio");
                RectTransform rect = close.GetComponent<RectTransform>();
                ProbeHit(page, events, raycaster, close, "center", camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)));
            }
        }

        private static T Find<T>(GameObject host, string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name);
        private static void CheckLabel(Page page, Text text, Camera camera)
        {
            if (text == null) { page.errors.Add("Missing label component."); return; }
            float size = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
            var item = new Label { name = text.name, text = text.text, bounds = Bounds(text.rectTransform,camera),
                minimumPixelFontSize = size * page.scale, preferredHeight = text.preferredHeight, availableHeight = text.rectTransform.rect.height };
            page.labels.Add(item);
            if (!InsideScreen(page, item.bounds)) page.errors.Add("Label outside screen: " + text.name);
            if (string.IsNullOrWhiteSpace(text.text)) return;
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            using (var generator = new TextGenerator())
            {
                generator.Populate(text.text, settings);
                int actualSize = text.resizeTextForBestFit ? generator.fontSizeUsedForBestFit : text.fontSize;
                item.renderedPixelFontSize = actualSize * page.scale;
            }
            settings.resizeTextForBestFit = false;
            settings.fontSize = (int)size;
            using (var generator = new TextGenerator())
            {
                float requiredHeight = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                if (requiredHeight > item.availableHeight + 1f) page.errors.Add("Text clips at minimum font size: " + text.name);
            }
            if ((text.name.StartsWith("Treasure Route ",StringComparison.Ordinal) || text.transform.parent.GetComponent<Button>() != null) && item.renderedPixelFontSize < 12f)
                page.errors.Add("Map/action label below 12 physical pixels: " + text.name);
        }
        private static void ProbeHit(Page page, EventSystem events, GraphicRaycaster raycaster, Button target, string sample, Vector2 point)
        {
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            hits.Sort((a,b) => b.depth.CompareTo(a.depth));
            GameObject first = hits.Count == 0 ? null : hits[0].gameObject;
            Button hit = first == null ? null : first.GetComponentInParent<Button>();
            var probe = new Probe { target = target.name, sample = sample, point = point, firstHit = first == null ? "NONE" : first.name,
                hitButton = hit == null ? "NONE" : hit.name, passed = hit == target };
            page.probes.Add(probe);
            if (!probe.passed) page.errors.Add("Pointer blocked: " + target.name + " / " + sample + " hit " + probe.firstHit);
        }
        private static bool InsideScreen(Page page, Rect bounds) => bounds.xMin >= -1 && bounds.yMin >= -1 && bounds.xMax <= page.width + 1 && bounds.yMax <= page.height + 1;
        private static Rect Bounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue,float.MaxValue), max = new Vector2(float.MinValue,float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point = camera.WorldToScreenPoint(corner); min = Vector2.Min(min,point); max = Vector2.Max(max,point); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
