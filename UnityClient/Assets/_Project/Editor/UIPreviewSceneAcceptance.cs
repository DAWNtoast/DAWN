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

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Captures the actual serialized UI preview scenes, not replacement fixtures.
    /// Each asset is opened in an isolated preview scene and closed without saving.
    /// No Play Mode, button callbacks, ConfigureEditorPreview or PlayerPrefs writes.
    /// </summary>
    public static class UIPreviewSceneAcceptance
    {
        private const string SceneDirectory = "Assets/_Project/Scenes/UIPreviews";
        private const int Width = 1920;
        private const int Height = 1080;
        private const int CaptureLayer = 31;

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public string sourceDirectory = SceneDirectory;
            public string scope = "Actual saved UI_*.unity scenes; serialized defaults retained; isolated non-Play editor rendering; no scene or player-save writes.";
            public int width = Width;
            public int height = Height;
            public int expectedCaptureCount;
            public int successfulCaptureCount;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public string scene;
            public string screen;
            public string port;
            public int routeIndex;
            public string file;
            public bool completePreview;
            public bool rebuiltMissingPreview;
            public string temporaryPreviewVariant;
            public bool sourceSceneUnchanged;
            public int uiObjectCount;
            public bool captured;
            public List<string> unityErrors = new List<string>();
            public string error;
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.UIPreviewSceneAcceptance.CaptureFromCommandLine
        /// -uiPreviewCapturePath D:/Game/UnityClient/Artifacts/UIPreviews/review-01
        /// The directory must be new or empty. Graphics must be enabled (no -nographics).
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("UI_PREVIEW_SCENE_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            return Capture(outputDirectory, SceneDirectory, null);
        }

        /// <summary>Read the authored references used by the new-game flow without regenerating them.</summary>
        public static void CaptureEditableFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            string[] references = {
                "UI_PortHub.unity", "UI_VoyageDeparture.unity", "UI_Navigation.unity",
                "UI_Encounter.unity", "UI_Settlement.unity", "UI_Tavern.unity",
                "UI_TavernCrewDetail.unity", "UI_Battle.unity", "UI_Warehouse.unity",
                "UI_Shipyard.unity", "UI_ShipyardDetail.unity", "UI_TreasureMap.unity",
                "TutorialVoyage/01_FirstDeparture.unity", "TutorialVoyage/03_FirstMandatory.unity",
                "TutorialVoyage/04_FirstOptional.unity", "TutorialVoyage/05_FirstCrewEvent.unity",
                "TutorialVoyage/14_SecondCrewEvent.unity", "TutorialVoyage/25_TutorialBoss.unity",
                "TutorialVoyage/28_DrawChart.unity", "TutorialVoyage/31_DisplayTreasure.unity",
                "TutorialVoyage/37_EquipRewardComponent.unity"
            };
            Debug.Log("EDITABLE_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", references));
        }

        public static void CaptureTreasureAdventureFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("TREASURE_ADVENTURE_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.TreasureAdventureReferencePaths));
        }

        public static void CaptureDepartureProvisionsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("DEPARTURE_PROVISION_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.DepartureProvisionReferencePaths));
        }

        public static void CaptureTradeEconomyFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("TRADE_ECONOMY_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.DepartureProvisionReferencePaths));
        }

        public static void CaptureMarketQuantityFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("MARKET_QUANTITY_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/MarketQuantity20261002/preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.MarketQuantityReferencePaths));
        }

        public static void CapturePortWarehouseFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("PORT_WAREHOUSE_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.PortWarehouseReferencePaths));
        }

        public static void CaptureStorageRoutingFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("STORAGE_ROUTING_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/StorageRouting20261002/preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.StorageRoutingReferencePaths));
        }

        public static void CaptureWarehouseCargoFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("WAREHOUSE_CARGO_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/StorageRouting20261002/warehouse-preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.WarehouseCargoReferencePaths));
        }

        public static void CaptureRareWarehouseFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("RARE_WAREHOUSE_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/RareWarehouse20261003/preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.RareWarehouseReferencePaths));
        }

        public static void CaptureRareInvestmentFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("RARE_INVESTMENT_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/RareConsumption20261003/investment-preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.RareInvestmentReferencePaths));
        }

        public static void CaptureWarehouseTabsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("WAREHOUSE_TABS_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/WarehouseTabs20261002/preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.WarehouseTabsReferencePaths));
        }

        public static void CaptureComponentResaleFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("COMPONENT_RESALE_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.ComponentResaleReferencePaths));
        }

        public static void CaptureChartSeedsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("CHART_SEED_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/SeaChartSeeds20261002/preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.ChartSeedReferencePaths));
        }

        public static void CaptureDepartureDestinationsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("DEPARTURE_DESTINATION_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", new[]
                {
                    "UI_VoyageDeparture.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_RecordedChart.unity",
                }));
        }

        public static void CaptureDepartureChartSelectionFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("DEPARTURE_CHART_SELECTION_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", new[]
                {
                    "UI_VoyageDeparture.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_RecordedChart.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_InvalidChart.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity",
                    "TutorialVoyage/11_SecondRoute.unity",
                }));
        }

        public static void CaptureDepartureLayoutFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            string output = ResolveOutputDirectory();
            Debug.Log("DEPARTURE_LAYOUT_UI_ACCEPTANCE: " + Capture(output,
                "Assets/_Project/Scenes/UIPreviewsForMe", new[]
                {
                    "UI_VoyageDeparture.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_RecordedChart.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_InvalidChart.unity",
                    "UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity",
                    "TutorialVoyage/11_SecondRoute.unity",
                }));
            Debug.Log("DEPARTURE_CREW_LAYOUT_UI_ACCEPTANCE: " + Capture(Path.Combine(output, "crew"),
                "Assets/_Project/Scenes/UIPreviewsForMe", new[] { "UI_VoyageDeparture.unity" }, "departure-crew"));
        }

        public static void CapturePortFacilityNavigationFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("PORT_FACILITY_NAVIGATION_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", new[]
                {
                    "UI_VoyageDeparture.unity",
                    "UI_Warehouse.unity",
                    "UIShipyard/UIShipyard_Port3.unity",
                }));
        }

        public static void CaptureRegionalShipyardsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("REGIONAL_SHIPYARD_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/RegionalShips20261002/preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.RegionalShipyardReferencePaths));
        }

        public static void CaptureCleanSceneLabelsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("CLEAN_SCENE_LABELS_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", new[]
                {
                    "UI_PortHub.unity", "UI_Tavern.unity", "UI_CardWorkshop.unity", "UI_ComponentShop.unity",
                    "UI_Battle.unity",
                }));
        }

        public static void CaptureBoardingCrewFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("BOARDING_CREW_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory("-navalVisualCapturePath",
                "Artifacts/ShipProgression20261003/boarding-preview-captures"),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.BoardingCrewReferencePaths));
        }

        public static void CaptureNavigationEnduranceFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("NAVIGATION_ENDURANCE_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.NavigationEnduranceReferencePaths));
        }

        public static void CaptureBossTreasureDropsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Debug.Log("BOSS_TREASURE_DROP_UI_ACCEPTANCE: " + Capture(ResolveOutputDirectory(),
                "Assets/_Project/Scenes/UIPreviewsForMe", GameplayPreviewSceneBuilder.BossTreasureDropReferencePaths));
        }

        private static string Capture(string outputDirectory, string sourceDirectory, string[] references, string variant = null)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("UI preview acceptance requires stopped, non-Play edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("UI preview captures require graphics. Remove -nographics.");

            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty. Use a new -uiPreviewCapturePath: " + directory);
            string project = Directory.GetParent(Application.dataPath).FullName;
            string sceneDirectory = Path.Combine(project, sourceDirectory);
            if (!Directory.Exists(sceneDirectory))
                throw new DirectoryNotFoundException("UI preview scene directory is missing: " + sceneDirectory);
            string[] scenes = references == null
                ? Directory.GetFiles(sceneDirectory, "UI_*.unity", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : references.Select(path => Path.Combine(sceneDirectory, path)).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No saved UI_*.unity scenes were found in " + SceneDirectory);
            Directory.CreateDirectory(directory);

            Review report = new Review
            {
                sourceDirectory = sourceDirectory,
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                expectedCaptureCount = scenes.Length,
            };
            if (variant != null)
                report.scope = "Temporary departure crew fixture generated from an isolated authored preview; source scene and player saves are not written.";
            try
            {
                foreach (string absoluteScenePath in scenes)
                {
                    Record record = new Record
                    {
                        scene = sourceDirectory + "/" + absoluteScenePath.Substring(sceneDirectory.Length + 1).Replace('\\', '/'),
                        file = Path.GetFileNameWithoutExtension(absoluteScenePath) + (variant == null ? string.Empty : "_Crew") + ".png",
                        temporaryPreviewVariant = variant,
                    };
                    report.captures.Add(record);
                    Application.LogCallback captureErrors = (condition, stackTrace, type) =>
                    {
                        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                            record.unityErrors.Add(condition + (string.IsNullOrEmpty(stackTrace) ? string.Empty : "\n" + stackTrace));
                    };
                    Application.logMessageReceived += captureErrors;
                    try
                    {
                        CaptureScene(absoluteScenePath, directory, record);
                        if (record.unityErrors.Count > 0)
                            throw new InvalidOperationException("Unity reported " + record.unityErrors.Count + " error(s) while opening/rendering/closing this scene.");
                        record.captured = true;
                        report.successfulCaptureCount += 1;
                    }
                    catch (Exception exception)
                    {
                        Exception cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException : exception;
                        record.error = cause.Message;
                        report.errors.Add(record.scene + ": " + cause.Message);
                        Debug.LogWarning("UI preview capture failed: " + record.scene + " — " + cause.Message);
                    }
                    finally { Application.logMessageReceived -= captureErrors; }
                }

                // Discovering files alone would otherwise silently accept an
                // accidentally deleted page. Every runtime preview screen needs
                // at least one saved scene; extra route/frame variants are valid.
                foreach (CoreGameplayPreviewScreen screen in references == null
                    ? (CoreGameplayPreviewScreen[])Enum.GetValues(typeof(CoreGameplayPreviewScreen))
                    : Array.Empty<CoreGameplayPreviewScreen>())
                {
                    if (!report.captures.Any(record => record.screen == screen.ToString()))
                        report.errors.Add("No saved preview scene covers screen: " + screen);
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulCaptureCount != report.expectedCaptureCount)
                throw new InvalidOperationException("UI preview scene acceptance is incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static void CaptureScene(string absoluteScenePath, string directory, Record record)
        {
            string originalHash = HashFile(absoluteScenePath);
            Scene previewScene = default;
            RenderTexture previousActive = RenderTexture.active;
            GameObject cameraHost = null;
            RenderTexture target = null;
            try
            {
                // Unlike OpenScene, this does not replace or add to the user's
                // working scene setup. All generated children remain temporary.
                previewScene = EditorSceneManager.OpenPreviewScene(record.scene);
                if (!previewScene.IsValid() || !previewScene.isLoaded)
                    throw new InvalidOperationException("The saved scene could not be opened as a preview scene.");
                CoreGameplayDemoController[] controllers = previewScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).ToArray();
                if (controllers.Length != 1)
                    throw new InvalidOperationException("Expected one CoreGameplayDemoController, found " + controllers.Length + ".");
                CoreGameplayDemoController controller = controllers[0];
                SerializedObject serialized = new SerializedObject(controller);
                record.screen = ReadEnum<CoreGameplayPreviewScreen>(serialized, "editorPreviewScreen").ToString();
                record.port = ReadEnum<CoreGameplayPreviewPort>(serialized, "editorPreviewPort").ToString();
                SerializedProperty route = serialized.FindProperty("editorPreviewRouteIndex");
                if (route == null) throw new InvalidOperationException("Missing serialized editorPreviewRouteIndex.");
                record.routeIndex = route.intValue;
                if (!controller.isActiveAndEnabled)
                    throw new InvalidOperationException("The saved preview controller is inactive or disabled.");

                if (record.temporaryPreviewVariant == "departure-crew")
                {
                    // The authored departure references do not include the expanded
                    // crew form. Generate this one declared fixture only in memory,
                    // through the regular editor-preview path without gameplay callbacks.
                    FieldInfo crewOpen = typeof(CoreGameplayDemoController).GetField("departureCrewConfigurationOpen",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (crewOpen == null) throw new MissingFieldException("departureCrewConfigurationOpen");
                    crewOpen.SetValue(controller, true);
                    controller.RefreshEditorPreview();
                    if (!controller.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Departure Crew Configuration Title"))
                        throw new InvalidOperationException("Temporary departure crew preview did not render its form.");
                    // A newly created Toggle starts false before its graphic is
                    // assigned. Setting false again does not invoke PlayEffect;
                    // runtime Start normally supplies this initial visual update,
                    // but this isolated Edit Mode capture never runs Start.
                    foreach (UnityEngine.UI.Toggle toggle in controller.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
                        if (toggle.name == "Use Chart Seed" && toggle.graphic != null)
                            toggle.graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
                }

                if (!controller.HasCompleteEditorPreview)
                {
                    if (record.scene.Contains("/UIPreviewsForMe/"))
                        throw new InvalidOperationException("Authored reference is incomplete; it must not be regenerated during comparison.");
                    MethodInfo rebuild = typeof(CoreGameplayDemoController).GetMethod("RebuildEditorPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (rebuild == null) throw new InvalidOperationException("Missing non-persistent RebuildEditorPreview method.");
                    record.rebuiltMissingPreview = true;
                    rebuild.Invoke(controller, null);
                }
                record.completePreview = controller.HasCompleteEditorPreview;
                if (!record.completePreview)
                    throw new InvalidOperationException("The scene did not generate its required preview screen root.");
                Transform previewRoot = controller.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                Canvas canvas = previewRoot != null ? previewRoot.GetComponent<Canvas>() : null;
                if (canvas == null) throw new InvalidOperationException("The expected preview Canvas was not found.");
                WriteLayout(canvas, Path.Combine(directory, Path.GetFileNameWithoutExtension(record.file) + ".layout.json"));

                cameraHost = new GameObject("Saved UI Preview Capture Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
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
                    { name = "Saved UI Preview Capture Target" };
                target.Create();
                camera.targetTexture = target;

                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                canvas.worldCamera = camera;
                RectTransform rect = canvas.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(Width, Height);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children) child.gameObject.layer = CaptureLayer;
                record.uiObjectCount = children.Length;
                // Allow dynamic-font atlas rebuilds from best-fit labels to
                // settle before the actual screenshot is read back.
                for (int frame = 0; frame < 3; frame++)
                {
                    foreach (UnityEngine.UI.Text text in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    camera.transform.position = rect.TransformPoint(rect.rect.center) + Vector3.back * 1000f;
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                SaveFrame(target, Path.Combine(directory, record.file));
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
                record.sourceSceneUnchanged = originalHash == HashFile(absoluteScenePath);
                if (!record.sourceSceneUnchanged)
                    throw new InvalidOperationException("The source scene changed during acceptance: " + record.scene);
            }
        }

        private static T ReadEnum<T>(SerializedObject serialized, string name) where T : struct
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null || !Enum.IsDefined(typeof(T), property.intValue))
                throw new InvalidOperationException("Missing or invalid serialized preview configuration: " + name);
            return (T)Enum.ToObject(typeof(T), property.intValue);
        }

        [Serializable]
        private sealed class LayoutSnapshot { public List<LayoutObject> objects = new List<LayoutObject>(); }

        [Serializable]
        private sealed class LayoutObject
        {
            public string path, name, text, imageName, textureName;
            public bool active;
            public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
            public Vector3 localScale;
            public int fontSize;
            public Color color;
            public Rect uvRect;
        }

        private static void WriteLayout(Canvas canvas, string path)
        {
            var snapshot = new LayoutSnapshot();
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == canvas.transform) continue;
                var names = new List<string>();
                for (Transform item = rect; item != canvas.transform && item != null; item = item.parent)
                    names.Insert(0, item.name);
                var text = rect.GetComponent<UnityEngine.UI.Text>();
                var image = rect.GetComponent<UnityEngine.UI.Image>();
                var raw = rect.GetComponent<UnityEngine.UI.RawImage>();
                var graphic = rect.GetComponent<UnityEngine.UI.Graphic>();
                snapshot.objects.Add(new LayoutObject {
                    path = string.Join("/", names), name = rect.name, active = rect.gameObject.activeInHierarchy,
                    anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
                    anchoredPosition = rect.anchoredPosition, sizeDelta = rect.sizeDelta, localScale = rect.localScale,
                    fontSize = text != null ? text.fontSize : 0, text = text != null ? text.text : null,
                    imageName = image != null && image.sprite != null ? image.sprite.name : null,
                    textureName = raw != null && raw.texture != null ? raw.texture.name : null,
                    color = graphic != null ? graphic.color : Color.clear,
                    uvRect = raw != null ? raw.uvRect : default
                });
            }
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
        }

        private static string HashFile(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Convert.ToBase64String(hash.ComputeHash(stream));
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
                if (bright - dark < 20)
                    throw new InvalidOperationException("Captured frame is nearly uniform; preview UI may not have rendered.");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>UI 场景预览检查目录</title>");
            html.Append("<style>body{background:#13202a;color:#f4e5c5;font:16px system-ui;margin:32px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr));gap:20px}figure{margin:0;padding:10px;background:#26343c;border:1px solid #67543c}img{display:block;width:100%;height:auto}figcaption{padding:10px 4px;line-height:1.7}a{color:#edba63}small{display:block;color:#becad0;overflow-wrap:anywhere}.error{color:#ffad99}p{line-height:1.7;max-width:1100px}</style></head><body>");
            html.Append("<h1>UI 场景预览检查目录</h1><p>").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append(" 张 · 1920 × 1080 · 读取 ").Append(Escape(report.sourceDirectory))
                .Append(" 中实际保存的场景及其默认配置。<br>编辑模式隔离渲染，没有进入游戏，也没有保存场景或修改玩家存档。点击缩略图查看原图。</p><p><a href=\"report.json\">结构化检查报告</a><br>生成时间（UTC）：")
                .Append(Escape(report.capturedAtUtc)).Append("<br>结构与渲染通过不等于视觉设计通过，请逐图检查摆放、遮挡、文字与交互区域。</p>");
            if (report.errors.Count > 0)
            {
                html.Append("<h2>需要处理</h2><ul class=\"error\">");
                foreach (string error in report.errors) html.Append("<li>").Append(Escape(error)).Append("</li>");
                html.Append("</ul>");
            }
            html.Append("<main>");
            foreach (Record record in report.captures)
            {
                html.Append("<figure>");
                if (record.captured)
                    html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><img loading=\"lazy\" width=\"1920\" height=\"1080\" alt=\"")
                        .Append(Escape(record.screen)).Append("\" src=\"").Append(Escape(record.file)).Append("\"></a>");
                html.Append("<figcaption>").Append(Escape(Path.GetFileNameWithoutExtension(record.scene)))
                    .Append("<small>").Append(Escape(record.screen)).Append(" · ").Append(Escape(record.port)).Append(" · 配置索引 ").Append(record.routeIndex)
                    .Append(" · ").Append(record.uiObjectCount).Append(" 个 UI 对象</small><small>").Append(Escape(record.scene)).Append("</small>");
                if (!record.captured) html.Append("<small class=\"error\">捕获失败：").Append(Escape(record.error)).Append("</small>");
                html.Append("</figcaption></figure>");
            }
            html.Append("</main></body></html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ResolveOutputDirectory(string argumentName = "-uiPreviewCapturePath", string defaultDirectory = null)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length; index++)
            {
                if (args[index] != argumentName) continue;
                if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException(argumentName + " requires a fresh output directory.");
                return Path.GetFullPath(Path.Combine(project, args[index + 1]));
            }
            return Path.Combine(project, defaultDirectory ?? Path.Combine("Artifacts", "UIPreviews"),
                "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
    }
}
