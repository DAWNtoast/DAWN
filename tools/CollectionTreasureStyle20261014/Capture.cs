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

namespace ShatteredSail.Editor
{
    // Installed only in the isolated validation project. All samples are editor
    // fixtures; no transaction is confirmed and no player preferences are read.
    public static class CollectionTreasureStyleCapture
    {
        private static readonly string[] AllStates = {
            "Collection", "CollectionSingle", "Catalog", "Storage", "RareStorage", "ItemActions",
            "Adjustment", "Sale", "RareAdjustment", "RareSale", "TreasureDesk",
            "TreasureReady", "FolioOverview", "FolioEmpty", "FolioUnused",
            "FolioHistory", "FolioHistoryEmpty", "FolioLegacyHistory", "FolioUnusedSingle",
            "FolioHistorySingle", "FolioUnusedLongSeed"
        };
        private static readonly string[] FocusStates = {
            "Collection", "Catalog", "Storage", "RareStorage", "Adjustment",
            "Sale", "TreasureDesk", "FolioUnused", "FolioHistory"
        };
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Production UI in private Editor PreviewScenes. All states at 1080p, focused pages at 720p, 1440p and 1280x1024. Production Expand CanvasScaler. Fixtures never commit transactions or player saves; live interactions require separate PlayMode verification.";
            public List<Page> pages = new List<Page>();
        }
        [Serializable] private sealed class Page
        {
            public string state, file, port;
            public int width, height;
            public bool passed;
            public Vector2 referenceResolution;
            public List<Control> controls = new List<Control>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Control
        {
            public string name, label;
            public Rect bounds;
            public bool interactable;
        }

        public static void Run()
        {
            RequireIsolatedProject();
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use stopped Editor with graphics enabled.");
            string output = Environment.GetEnvironmentVariable("COLLECTION_TREASURE_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Missing capture directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (string state in AllStates) Capture(report, output, state, 1920, 1080);
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(2560, 1440), new Vector2Int(1280, 1024) })
                    foreach (string state in FocusStates) Capture(report, output, state, size.x, size.y);
                report.passed = report.pages.Count == AllStates.Length + FocusStates.Length * 3 && report.pages.All(page => page.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Collection/treasure capture failed; inspect report.json.");
            Debug.Log("COLLECTION_TREASURE_CAPTURE_OK: " + report.pages.Count + " production frames; " + output);
        }

        public static void SyncPreviews()
        {
            RequireIsolatedProject();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Preview synchronization requires edit mode.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save modified scenes before synchronization.");
            const string warehousePath = "Assets/_Project/Scenes/UIPreviews/UI_Warehouse.unity";
            string backupRoot = Environment.GetEnvironmentVariable("COLLECTION_TREASURE_PREVIEW_BACKUP");
            if (string.IsNullOrWhiteSpace(backupRoot)) throw new InvalidOperationException("Missing preview backup directory.");
            Directory.CreateDirectory(backupRoot);
            File.Copy(warehousePath, Path.Combine(backupRoot, "UI_Warehouse.unity"), false);
            File.Copy("Assets/_Project/Scenes/UIPreviews/UI_TreasureMap.unity", Path.Combine(backupRoot, "UI_TreasureMap.unity"), false);
            Scene warehouse = EditorSceneManager.OpenScene(warehousePath, OpenSceneMode.Single);
            CoreGameplayDemoController controller = warehouse.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (controller.EditorPreviewScreen != CoreGameplayPreviewScreen.Warehouse)
                throw new InvalidOperationException("Unexpected collection preview scene.");
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("editorPreviewPersistGeneratedUi").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.RefreshEditorPreview();
            if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete collection preview.");
            // Reuse only the existing asset-persistence helper, never a retired
            // scene generator. The scene camera and controller retain identity.
            Type builder = typeof(GameplayPreviewSceneBuilder);
            var specs = (Array)builder.GetField("Specs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            object spec = specs.Cast<object>().Single(item => (CoreGameplayPreviewScreen)item.GetType()
                .GetProperty("Screen").GetValue(item) == CoreGameplayPreviewScreen.Warehouse);
            builder.GetMethod("PersistGeneratedSceneVisuals", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { (object)controller.transform, spec });
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(warehouse);
            if (!EditorSceneManager.SaveScene(warehouse, warehousePath)) throw new IOException("Failed to save collection preview.");
            AssetDatabase.SaveAssets();
            GameplayPreviewSceneBuilder.SyncTreasureWorktableFromCommandLine();
            Debug.Log("COLLECTION_TREASURE_PREVIEWS_OK: UI_Warehouse and UI_TreasureMap only.");
        }

        private static void RequireIsolatedProject()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requires an isolated project under D:/Game/tmp.");
        }

        private static void Configure(CoreGameplayDemoController controller, string state)
        {
            if (state == "CollectionSingle")
            {
                controller.WarehouseCollection.Import(CoreWarehouseCollection.TutorialTreasureId, CoreWarehouseCollection.TutorialTreasureId);
                SetField(controller, "selectedWarehouseTreasureId", CoreWarehouseCollection.TutorialTreasureId);
                SetField(controller, "warehouseTreasurePage", 0);
                Invoke(controller, "Render");
            }
            else if (state == "Catalog")
            {
                SetField(controller, "warehouseCatalogOpen", true);
                Invoke(controller, "Render");
            }
            else if (state == "Storage" || state == "Adjustment" || state == "Sale" || state == "ItemActions")
            {
                controller.ConfigureEditorPreviewWarehouseStorage(state == "Adjustment" ? 1 : state == "Sale" ? 2 : 0);
                if (state == "ItemActions")
                {
                    object quote = Invoke(controller, "CreateWarehouseCargoQuote", PortMarketItem.Wood, true);
                    SetField(controller, "activeWarehouseCargoQuote", quote);
                    Invoke(controller, "BuildWarehouseCargoActions", quote);
                }
            }
            else if (state == "RareStorage" || state == "RareAdjustment" || state == "RareSale")
                controller.ConfigureEditorPreviewWarehouseRare(state == "RareAdjustment" ? 1 : state == "RareSale" ? 2 : 0);
            else if (state == "TreasureReady")
                controller.ConfigureEditorTreasureState(TreasureMapPreviewState.FragmentsReady);
            else if (state.StartsWith("Folio", StringComparison.Ordinal))
            {
                if (state == "FolioLegacyHistory")
                    controller.ConfigureEditorTreasureState(TreasureMapPreviewState.RewardClaim);
                else if (state == "FolioOverview" || state.StartsWith("FolioUnused", StringComparison.Ordinal) ||
                    state == "FolioHistory" || state == "FolioHistorySingle")
                {
                    var fixture = new CoreTreasureProgress
                    {
                        pirateExperience = 1000,
                        maps = new[] {
                            new CoreTreasureMapSave { id = "style-weathered", seed = "COLLECTION-STYLE-WEATHERED", rankId = "weathered" },
                            new CoreTreasureMapSave { id = "style-gilded", seed = "COLLECTION-STYLE-GILDED", rankId = "gilded" }
                        },
                        usedMaps = new[] {
                            new CoreUsedTreasureMapSave { id = "style-used-weathered", seed = "COLLECTION-STYLE-USED-WEATHERED", rankId = "weathered", departurePort = controller.Session.CurrentPort, hasExplorationCoverage = true, explorationCoverage = 67.5f },
                            new CoreUsedTreasureMapSave { id = "style-used-moonlit", seed = "COLLECTION-STYLE-USED-MOONLIT", rankId = "moonlit", departurePort = controller.Session.CurrentPort, hasExplorationCoverage = true, explorationCoverage = 100f }
                        }
                    };
                    if (state == "FolioUnusedSingle") fixture.maps = fixture.maps.Take(1).ToArray();
                    if (state == "FolioHistorySingle") fixture.usedMaps = fixture.usedMaps.Take(1).ToArray();
                    if (state == "FolioUnusedLongSeed")
                        for (int index = 0; index < fixture.maps.Length; index++)
                            fixture.maps[index].seed = new string('W', 63) + index;
                    controller.Session.RestoreTreasureProgress(fixture);
                }
                SetField(controller, "treasureFolioOpen", true);
                SetField(controller, "treasureFolioView", state.StartsWith("FolioUnused", StringComparison.Ordinal) ? 1 : state.Contains("History") ? 2 : 0);
                SetField(controller, "treasureFolioPage", 0);
                Invoke(controller, "Render");
            }
        }

        private static void Capture(Report report, string output, string state, int width, int height)
        {
            var page = new Page { state = state, width = width, height = height, file = state.ToLowerInvariant() + "_" + width + "x" + height + ".png" };
            report.pages.Add(page);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                bool treasure = state.StartsWith("Treasure", StringComparison.Ordinal) || state.StartsWith("Folio", StringComparison.Ordinal);
                host = new GameObject("Collection Treasure Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(treasure ? CoreGameplayPreviewScreen.TreasureMap : CoreGameplayPreviewScreen.Warehouse,
                    treasure ? CoreGameplayPreviewPort.TwilightTide : CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Production preview incomplete.");
                Configure(controller, state);
                page.port = controller.Session.CurrentPort;
                cameraHost = new GameObject("Collection Treasure Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
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
                page.referenceResolution = scaler.referenceResolution;
                float scale = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
                Vector2 logicalSize = new Vector2(width / scale, height / scale);
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
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();

                foreach (Button button in host.GetComponentsInChildren<Button>(false))
                {
                    Rect bounds = Bounds(button.GetComponent<RectTransform>(), camera);
                    page.controls.Add(new Control { name = button.name, label = button.GetComponentInChildren<Text>()?.text,
                        bounds = bounds, interactable = button.interactable });
                    if (bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > width + 1 || bounds.yMax > height + 1)
                        page.errors.Add("Control outside screen: " + button.name);
                }
                string expected = state == "Catalog" ? "Warehouse Collection Catalog Panel" :
                    state == "ItemActions" ? "Warehouse Item Actions" : state.Contains("Adjustment") ? "Warehouse Cargo Adjustment" :
                    state.Contains("Sale") ? "Warehouse Cargo Sale" : state.StartsWith("Folio", StringComparison.Ordinal) ? "Treasure Folio" :
                    treasure ? "Treasure Workbench" : "Warehouse Ledger";
                if (!host.GetComponentsInChildren<Transform>(false).Any(item => item.name == expected))
                    page.errors.Add("Missing expected live content: " + expected);
                if (!page.controls.Any(item => item.name == "Return To Port Hub")) page.errors.Add("Missing return to port control.");
                foreach (Slider slider in host.GetComponentsInChildren<Slider>(false))
                {
                    Rect bounds = Bounds(slider.GetComponent<RectTransform>(), camera);
                    if (bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > width || bounds.yMax > height)
                        page.errors.Add("Slider outside screen: " + slider.name);
                    if (slider.handleRect == null || slider.handleRect.GetComponent<Image>()?.sprite == null)
                        page.errors.Add("Slider has no visible authored handle: " + slider.name);
                }
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, page.file), texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                page.passed = page.errors.Count == 0;
            }
            catch (Exception e) { page.errors.Add(e.ToString()); page.passed = false; }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Rect Bounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point = camera.WorldToScreenPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void SetField(object instance, string name, object value) => typeof(CoreGameplayDemoController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static object Invoke(object instance, string name, params object[] args) => typeof(CoreGameplayDemoController)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(method => method.Name == name &&
                method.GetParameters().Length == args.Length && method.GetParameters().Select((parameter, index) =>
                    args[index] == null || parameter.ParameterType.IsInstanceOfType(args[index])).All(match => match))
            .Invoke(instance, args);
    }
}
