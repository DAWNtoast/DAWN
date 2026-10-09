using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.PerformanceAudit
{
    // Copied only into the isolated diagnostic build, never into the shipped Assets.
    [DefaultExecutionOrder(32000)]
    public sealed class PlayerPerformanceProbe : MonoBehaviour
    {
        private const int WarmupFrames = 60, SampleFrames = 180;
        private const double TimeoutSeconds = 180;
        private const int DefaultRenderWidth = 1920, DefaultRenderHeight = 1080, CaptureLayer = 31;
        private const string Seed = "SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E";
        private readonly Report report = new Report();
        private readonly List<Counter> counters = new List<Counter>();
        private string outputPath, captureDirectory;
        private bool finished, reservedOutput;
        private double startedAt;
        private Camera offscreenCamera;
        private RenderTexture renderTarget;
        private RenderPipeline.StandardRequest renderRequest;
        private readonly List<CanvasConfiguration> canvasConfigurations = new List<CanvasConfiguration>();
        private bool offscreenEnabled;
        private int renderedFrames;
        private double lastRenderCallbackMs = -1;
        private int renderWidth = DefaultRenderWidth, renderHeight = DefaultRenderHeight;
        private bool captureUiVariants;

        [Serializable] private sealed class Report
        {
            public string status = "running", error, startedUtc, completedUtc;
            public string unity, company, product, persistentDataPath, cpu, gpu, graphicsApi, operatingSystem;
            public string method = "Windows standalone Development Player, hidden window, real production UI rendered offscreen to the verified renderTargetWidth/renderTargetHeight (default 1920x1080) with a 60 FPS cap; " +
                "not screen-present latency or release-player FPS. Explicit isolated performance fixture, not earned progression. " +
                "Each scenario warms 60 rendered frames then records 180 frames. Frame intervals use Time.unscaledDeltaTime; " +
                "LateUpdate forces the current production root Canvases through ScreenSpaceCamera and Camera.Render or SRP SubmitRenderRequest. " +
                "Render Callback is CPU wall time around Canvas.ForceUpdateCanvases plus render submission, not isolated GPU completion time. " +
                "Profiler counters describe the last completed frame. Main/render thread nanoseconds are converted to milliseconds; memory remains bytes. " +
                "Unsupported or unavailable samples are -1 and excluded from percentiles. Scene setup, screenshots, object inventories and JSON serialization occur outside measured frames. " +
                "Achievement page callbacks run every 30 frames. Other scenarios measure idle presentation including its normal animation, not gameplay action latency. " +
                "Timing includes the normal 60 FPS cap; this is not an uncapped throughput benchmark. Button callbacks are used, not OS input automation. " +
                "Texture2D snapshots enumerate only already loaded objects after each timed interval and before screenshot readback. " +
                "Detailed snapshots are spooled to JSON and their managed references released between scenarios; the final report reloads them only after all measurements. " +
                "Runtime texture bytes are Profiler.GetRuntimeMemorySizeLong estimates of Unity native object memory, not measured GPU residency. " +
                "Optional uiVariants are untimed captures and do not add scenarios to the seven timing scenarios.";
            public string fixture = "Clean audit identity; completed tutorial, 2000 gold, navigator, three collection entries, seeded supplies and port storage; " +
                "fixed chart seed. Core StartBattle is invoked by reflection at the map boss, then the ordinary retreat button creates settlement.";
            public string mapSeed = Seed;
            public bool developmentBuild, identityVerified, uiVariantsEnabled;
            public int width, height, targetFrameRate, vSyncCount, warmupFrames = WarmupFrames, sampleFrames = SampleFrames;
            public int renderTargetWidth = DefaultRenderWidth, renderTargetHeight = DefaultRenderHeight;
            public string renderPipeline;
            public double firstMenuInteractiveSeconds = -1, elapsedSeconds;
            public MenuReadinessDiagnostic menuReadiness;
            public List<Scenario> scenarios = new List<Scenario>();
            public List<UiVariant> uiVariants = new List<UiVariant>();
            public List<ObjectInventory> warehouseCycleInventories = new List<ObjectInventory>();
        }

        [Serializable] private sealed class MenuReadinessDiagnostic
        {
            public bool ready, buttonActive, buttonInteractable, eventSystemActive;
            public int attempts, screenWidth, screenHeight;
            public double waitedSeconds;
            public Vector2 pointerPosition;
            public string buttonPath, canvasPath, canvasRenderMode, canvasCamera, canvasRect, eventSystem, inputModule;
            public float canvasScaleFactor;
            public string[] raycastHits;
            public string screenshot, screenshotError;
        }

        [Serializable] private sealed class Scenario
        {
            public string name, phase, screenshot;
            public int width, height, targetFrameRate, vSyncCount;
            public int renderTargetWidth, renderTargetHeight, renderedFrames, screenshotMinimumBrightness, screenshotMaximumBrightness;
            public long screenshotBytes;
            public string[] renderedRootCanvases;
            public CanvasDimensions[] rootCanvasDimensions;
            public TextureSnapshot textures;
            public ObjectInventory objects;
            public Metric frameInterval, renderCallbackMs;
            public Metric[] counters;
        }

        [Serializable] private sealed class CanvasDimensions
        {
            public string path, renderMode, scalerMode, screenMatchMode;
            public Vector2 renderingDisplaySize, logicalSize;
            public Rect pixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor;
            public bool verified;
        }

        [Serializable] private sealed class TextureSnapshot
        {
            public string checkpoint, detailsFile;
            public string scope = "Already loaded Texture2D objects, unique instance IDs; excludes RenderTexture; captured outside timing before screenshot readback. Names are not unique resource paths.";
            public long runtimeMemoryBytes;
            public int unavailableCount, entryCount;
            public TextureDetails[] entries;
        }

        [Serializable] private sealed class TextureDetails
        {
            public string name, format, graphicsFormat, hideFlags, error;
            public int instanceId, width, height, mipmapCount;
            public bool isReadable;
            public long runtimeMemoryBytes = -1;
        }

        [Serializable] private sealed class UiVariant
        {
            public string name, phase, screenshot;
            public string method = "Untimed production UI button callback followed by offscreen render/capture; excluded from seven scenarios.";
            public int width, height, renderTargetWidth, renderTargetHeight, screenshotMinimumBrightness, screenshotMaximumBrightness;
            public long screenshotBytes;
            public CanvasDimensions[] rootCanvasDimensions;
        }

        private sealed class CanvasConfiguration
        {
            public Canvas Canvas;
            public Camera Camera;
            public RenderMode Mode;
            public float PlaneDistance, ScaleFactor;
            public Vector2 Size;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public Transform[] Descendants;
            public int[] Layers;
        }

        [Serializable] private sealed class Metric
        {
            public string name, unit, nativeUnit, availabilityError;
            public bool recorderValid, available;
            public int validSamples;
            public double p50 = -1, p95 = -1, p99 = -1, max = -1;
            public double[] samples = new double[SampleFrames];
            public void Summarize()
            {
                double[] values = samples.Where(value => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value)).ToArray();
                Array.Sort(values);
                validSamples = values.Length;
                if (values.Length == 0) return;
                p50 = Percentile(values, .50); p95 = Percentile(values, .95); p99 = Percentile(values, .99); max = values[values.Length - 1];
            }
            private static double Percentile(double[] values, double percentile) => values[Math.Max(0, (int)Math.Ceiling(values.Length * percentile) - 1)];
        }

        [Serializable] private sealed class ObjectInventory
        {
            public string checkpoint;
            public int gameObjects, activeGameObjects, graphics, textures, sprites, materials, audioClips;
            public long managedHeapBytes;
        }

        private sealed class Counter : IDisposable
        {
            public string Name, Unit, NativeUnit, Error;
            public double Scale;
            public ProfilerRecorder Recorder;
            public bool Available;
            public double Read() => Available && Recorder.Valid && Recorder.Count > 0 ? Recorder.LastValue * Scale : -1;
            public void Dispose() { if (Recorder.Valid) Recorder.Dispose(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-playerPerformanceOutput");
            if (at < 0) return;
            var host = new GameObject("Isolated Player Performance Probe");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<PlayerPerformanceProbe>();
            try
            {
                if (at + 1 >= args.Length) throw new ArgumentException("Missing -playerPerformanceOutput JSON path.");
                probe.Initialize(args[at + 1], args);
            }
            catch (Exception error) { probe.Fail(error); }
        }

        private void Initialize(string path, string[] args)
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            report.unity = Application.unityVersion;
            report.company = Application.companyName; report.product = Application.productName;
            report.persistentDataPath = Application.persistentDataPath;
            report.developmentBuild = Debug.isDebugBuild;
            report.cpu = SystemInfo.processorType; report.gpu = SystemInfo.graphicsDeviceName;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); report.operatingSystem = SystemInfo.operatingSystem;
            if (!Path.IsPathRooted(path) || !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("-playerPerformanceOutput must be an absolute new .json file path.");
            outputPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            using (new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            reservedOutput = true;
            bool hasWidth = Array.IndexOf(args, "-playerAuditWidth") >= 0;
            bool hasHeight = Array.IndexOf(args, "-playerAuditHeight") >= 0;
            if (hasWidth != hasHeight) throw new ArgumentException("Specify both -playerAuditWidth and -playerAuditHeight.");
            renderWidth = ReadDimension(args, "-playerAuditWidth", DefaultRenderWidth, 640, 7680);
            renderHeight = ReadDimension(args, "-playerAuditHeight", DefaultRenderHeight, 480, 4320);
            captureUiVariants = Array.IndexOf(args, "-playerAuditUiVariants") >= 0;
            report.renderTargetWidth = renderWidth; report.renderTargetHeight = renderHeight;
            report.uiVariantsEnabled = captureUiVariants;
            if (Application.isEditor || Application.companyName != "ShatteredSailAudit" || Application.productName != "Optimization20261008")
                throw new InvalidOperationException("Refusing to touch preferences outside the dedicated ShatteredSailAudit/Optimization20261008 Player identity.");
            report.identityVerified = true;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("This benchmark requires the graphical Player; -nographics is unsupported.");
            if (renderWidth > SystemInfo.maxTextureSize || renderHeight > SystemInfo.maxTextureSize)
                throw new InvalidOperationException("Requested dimensions exceed the graphics device texture limit.");
            captureDirectory = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "-captures");
            if (Directory.Exists(captureDirectory)) throw new IOException("Capture directory already exists; use a fresh report filename.");
            Directory.CreateDirectory(captureDirectory);
            // Isolate checkpoints as well as the identity-specific PlayerPrefs.
            Type persistence = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
            FieldInfo directory = persistence.GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
            if (directory == null) throw new MissingFieldException(persistence.FullName, "directoryOverride");
            directory.SetValue(null, Path.Combine(captureDirectory, "audit-checkpoints"));
            Type journal = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.ProfileSaveJournal", false);
            if (journal != null)
            {
                FieldInfo journalDirectory = journal.GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
                if (journalDirectory == null) throw new MissingFieldException(journal.FullName, "directoryOverride");
                journalDirectory.SetValue(null, Path.Combine(captureDirectory, "audit-profile-journal"));
            }
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
            report.targetFrameRate = Application.targetFrameRate; report.vSyncCount = QualitySettings.vSyncCount;
            report.width = Screen.width; report.height = Screen.height;
            AddCounter(ProfilerCategory.Internal, "Main Thread", "milliseconds", "TimeNanoseconds", 1e-6);
            AddCounter(ProfilerCategory.Internal, "Render Thread", "milliseconds", "TimeNanoseconds", 1e-6);
            AddCounter(ProfilerCategory.Memory, "GC Allocated In Frame", "bytes", "Bytes", 1);
            AddCounter(ProfilerCategory.Memory, "System Used Memory", "bytes", "Bytes", 1);
            AddCounter(ProfilerCategory.Memory, "Total Used Memory", "bytes", "Bytes", 1);
            AddCounter(ProfilerCategory.Memory, "GC Used Memory", "bytes", "Bytes", 1);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guarded(Run()));
        }

        private static int ReadDimension(string[] args, string option, int fallback, int minimum, int maximum)
        {
            int at = Array.IndexOf(args, option);
            if (at < 0) return fallback;
            if (Array.LastIndexOf(args, option) != at || at + 1 >= args.Length ||
                !int.TryParse(args[at + 1], out int value) || value < minimum || value > maximum)
                throw new ArgumentException(option + " must occur once with an integer from " + minimum + " through " + maximum + ".");
            return value;
        }

        private void AddCounter(ProfilerCategory category, string name, string unit, string expectedNativeUnit, double scale)
        {
            var counter = new Counter { Name = name, Unit = unit, Scale = scale };
            try
            {
                counter.Recorder = ProfilerRecorder.StartNew(category, name, 1);
                counter.Available = counter.Recorder.Valid;
                counter.NativeUnit = counter.Available ? counter.Recorder.UnitType.ToString() : "unavailable";
                if (counter.Available && counter.NativeUnit != expectedNativeUnit)
                {
                    counter.Error = "Unexpected native unit " + counter.NativeUnit + "; expected " + expectedNativeUnit;
                    counter.Available = false;
                }
            }
            catch (Exception error) { counter.Error = error.Message; counter.Available = false; }
            counters.Add(counter);
        }

        private IEnumerator Run()
        {
            yield return null;
            if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadScene("MainMenu");
            yield return WaitForMenuReadiness();
            report.firstMenuInteractiveSeconds = Time.realtimeSinceStartupAsDouble;
            CreateOffscreenRenderer();
            yield return Measure("01_main_menu_idle", null);
            Click("Achievements");
            Button next = FindButton("Achievements Next Page");
            Button previous = GameObject.Find("Achievements Previous Page")?.GetComponent<Button>();
            if (previous == null) throw new InvalidOperationException("Missing previous achievement page control.");
            bool forward = true;
            yield return Measure("02_achievement_page_turning", frame =>
            {
                if (frame % 30 != 0) return;
                if (!next.interactable) forward = false;
                if (!previous.interactable) forward = true;
                (forward ? next : previous).onClick.Invoke();
            });
            Click("Back From Achievements");

            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 2000);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey,
                string.Join(",", CoreWarehouseCollection.Catalog.Take(3).Select(item => item.Id)));
            PlayerPrefs.Save();
            RestoreCanvasConfigurations();
            SceneManager.LoadScene("CoreGameplayDemo");
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            if (controller == null || controller.Session == null || controller.IsTutorialActive)
                throw new InvalidOperationException("The port fixture did not initialize.");
            CoreVoyageSession session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Wood = 10; session.Cargo.Powder = 25; session.Cargo.Cannonballs = 40;
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.PortWarehouse.Restore("v1;断锚港,water,600;断锚港,food,500;断锚港,wood,120;断锚港,iron,35;断锚港,cloth,42");
            MethodInfo renderMethod = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            if (renderMethod == null) throw new MissingMethodException("CoreGameplayDemoController.Render");
            var render = (Action)Delegate.CreateDelegate(typeof(Action), controller, renderMethod);
            render();
            yield return Measure("03_port_hub", null);
            Click("Port Hotspot Warehouse");
            yield return null;
            Click("Warehouse Tab Storage");
            yield return Measure("04_warehouse_storage", null);
            if (captureUiVariants) yield return CaptureWarehouseUiVariants();
            Click("Return To Port Hub");
            yield return null;
            if (!session.TrySelectChartSeed(Seed, out string failure)) throw new InvalidOperationException("Fixed chart refused: " + failure);
            session.StartVoyage();
            render();
            if (session.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Navigation fixture did not enter sailing.");
            yield return Measure("05_navigation_map", null);
            CoreMapNode boss = session.Map.Nodes.First(node => node.IsBoss);
            MethodInfo startBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (startBattle == null) throw new MissingMethodException("CoreVoyageSession.StartBattle");
            startBattle.Invoke(session, new object[] { boss.Encounter, true });
            render();
            if (session.Phase != CoreDemoPhase.Battle) throw new InvalidOperationException("Battle fixture did not enter battle.");
            yield return Measure("06_battle_preparation", null);
            Click("Retreat Battle");
            if (session.Phase != CoreDemoPhase.Settlement) throw new InvalidOperationException("Ordinary battle retreat did not enter settlement.");
            yield return Measure("07_settlement", null);
            Click("Return To Port");
            yield return Frames(30);
            // Warm both directions before comparing the same port-hub state.
            Click("Port Hotspot Warehouse"); yield return Frames(15);
            Click("Return To Port Hub"); yield return Frames(30);
            report.warehouseCycleInventories.Add(Inventory("before_30_cycles_port_hub"));
            for (int cycle = 0; cycle < 30; cycle++)
            {
                Click("Port Hotspot Warehouse"); yield return Frames(15);
                Click("Return To Port Hub"); yield return Frames(15);
                if ((cycle + 1) % 10 == 0) report.warehouseCycleInventories.Add(Inventory("after_" + (cycle + 1) + "_cycles_port_hub"));
            }
            Complete();
        }

        private IEnumerator WaitForMenuReadiness()
        {
            double waitStarted = Time.realtimeSinceStartupAsDouble;
            var diagnostic = new MenuReadinessDiagnostic();
            report.menuReadiness = diagnostic;
            var hits = new List<RaycastResult>(16);
            Button button = null;
            Canvas canvas = null;
            EventSystem input = null;
            Vector2 position = Vector2.zero;
            while (true)
            {
                Canvas.ForceUpdateCanvases();
                diagnostic.attempts++;
                button = GameObject.Find("Achievements")?.GetComponent<Button>();
                canvas = button == null ? null : button.GetComponentInParent<Canvas>()?.rootCanvas;
                input = EventSystem.current;
                hits.Clear();
                if (button != null && canvas != null && input != null && input.isActiveAndEnabled)
                {
                    Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var rect = (RectTransform)button.transform;
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                    var pointer = new PointerEventData(input) { button = PointerEventData.InputButton.Left, position = position };
                    input.RaycastAll(pointer, hits);
                    diagnostic.ready = button.isActiveAndEnabled && button.IsInteractable() && hits.Count > 0 &&
                        hits[0].gameObject.GetComponentInParent<Button>() == button;
                }
                diagnostic.waitedSeconds = Time.realtimeSinceStartupAsDouble - waitStarted;
                if (diagnostic.ready || diagnostic.waitedSeconds >= 5d) break;
                yield return null;
            }
            diagnostic.screenWidth = Screen.width; diagnostic.screenHeight = Screen.height;
            diagnostic.pointerPosition = position;
            diagnostic.buttonPath = ObjectPath(button == null ? null : button.transform);
            diagnostic.buttonActive = button != null && button.isActiveAndEnabled;
            diagnostic.buttonInteractable = button != null && button.IsInteractable();
            diagnostic.canvasPath = ObjectPath(canvas == null ? null : canvas.transform);
            diagnostic.canvasRenderMode = canvas == null ? "missing" : canvas.renderMode.ToString();
            diagnostic.canvasCamera = canvas == null || canvas.worldCamera == null ? "none" : ObjectPath(canvas.worldCamera.transform);
            diagnostic.canvasScaleFactor = canvas == null ? -1 : canvas.scaleFactor;
            diagnostic.canvasRect = canvas == null ? "missing" : ((RectTransform)canvas.transform).rect.ToString();
            diagnostic.eventSystem = ObjectPath(input == null ? null : input.transform);
            diagnostic.eventSystemActive = input != null && input.isActiveAndEnabled;
            diagnostic.inputModule = input == null || input.currentInputModule == null ? "none" : input.currentInputModule.GetType().FullName;
            diagnostic.raycastHits = hits.Select(hit => ObjectPath(hit.gameObject.transform) +
                " | module=" + (hit.module == null ? "none" : hit.module.GetType().Name) +
                " | sortingLayer=" + hit.sortingLayer + " order=" + hit.sortingOrder +
                " depth=" + hit.depth + " distance=" + hit.distance).ToArray();
            if (diagnostic.ready) yield break;
            diagnostic.screenshotError = "Offscreen capture is intentionally not configured until the menu passes real raycast readiness; the hidden window has no readable backbuffer.";
            throw new InvalidOperationException("Main menu was not reachable by a real UI raycast within 5 seconds. See menuReadiness.");
        }

        private static string ObjectPath(Transform item)
        {
            if (item == null) return "missing";
            string path = item.name;
            while (item.parent != null) { item = item.parent; path = item.name + "/" + path; }
            return path;
        }

        private IEnumerator Measure(string name, Action<int> duringFrame)
        {
            RefreshOffscreenCanvases();
            var scenario = new Scenario { name = name, phase = SceneManager.GetActiveScene().name,
                width = Screen.width, height = Screen.height, targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
                renderTargetWidth = renderTarget.width, renderTargetHeight = renderTarget.height,
                renderedRootCanvases = canvasConfigurations.Select(item => ObjectPath(item.Canvas.transform)).ToArray(),
                frameInterval = new Metric { name = "Frame Interval", unit = "milliseconds", nativeUnit = "seconds", available = true },
                renderCallbackMs = new Metric { name = "Render Callback", unit = "milliseconds", nativeUnit = "Stopwatch ticks", available = true },
                counters = new Metric[counters.Count] };
            for (int index = 0; index < counters.Count; index++)
            {
                Counter counter = counters[index];
                scenario.counters[index] = new Metric { name = counter.Name, unit = counter.Unit,
                    nativeUnit = counter.NativeUnit, recorderValid = counter.Recorder.Valid,
                    available = counter.Available, availabilityError = counter.Error };
                for (int frame = 0; frame < SampleFrames; frame++) scenario.counters[index].samples[frame] = -1;
            }
            report.scenarios.Add(scenario);
            for (int frame = 0; frame < SampleFrames; frame++)
                scenario.frameInterval.samples[frame] = scenario.renderCallbackMs.samples[frame] = -1;
            for (int frame = 0; frame < WarmupFrames; frame++) { duringFrame?.Invoke(frame); yield return null; }
            for (int frame = 0; frame < SampleFrames; frame++)
            {
                int beforeRender = renderedFrames;
                duringFrame?.Invoke(frame + WarmupFrames);
                yield return null;
                if (renderedFrames != beforeRender + 1)
                    throw new InvalidOperationException("Every sampled frame must render the actual UI exactly once.");
                scenario.renderedFrames++;
                scenario.frameInterval.samples[frame] = Time.unscaledDeltaTime * 1000d;
                scenario.renderCallbackMs.samples[frame] = lastRenderCallbackMs;
                for (int index = 0; index < counters.Count; index++) scenario.counters[index].samples[frame] = counters[index].Read();
            }
            scenario.frameInterval.Summarize();
            scenario.renderCallbackMs.Summarize();
            foreach (Metric metric in scenario.counters)
            {
                metric.Summarize();
                metric.available &= metric.validSamples > 0;
            }
            scenario.rootCanvasDimensions = CaptureCanvasDimensions();
            RequireVerifiedCanvasDimensions(scenario.rootCanvasDimensions);
            scenario.objects = Inventory(name);
            scenario.textures = CaptureTextureSnapshot(name);
            PersistTextureSnapshot(scenario.textures);
            string path = Path.Combine(captureDirectory, name + ".png");
            SaveOffscreenCapture(path, scenario);
            scenario.screenshot = path;
            yield return null;
        }

        private IEnumerator CaptureWarehouseUiVariants()
        {
            ClickVariantControl("Warehouse Storage Header Stored");
            yield return CaptureUiVariant("04a_warehouse_storage_sorted_stored");
            ClickVariantControl("Warehouse Tab Rare");
            ClickVariantControl("Warehouse Rare Header Name");
            yield return CaptureUiVariant("04b_warehouse_rare_default_name");
            ClickVariantControl("Warehouse Rare Header Volume");
            yield return CaptureUiVariant("04c_warehouse_rare_sorted_volume");
            ClickVariantControl("Warehouse Tab Storage");
            ClickVariantControl("Warehouse Storage Header Name");
            yield return Frames(2);
        }

        private void ClickVariantControl(string name)
        {
            if (FindButton(name, false) == null)
                throw new InvalidOperationException("-playerAuditUiVariants requires an active stage-4 sorting Button named '" + name + "'. No fallback screenshot was accepted.");
            Click(name);
        }

        private IEnumerator CaptureUiVariant(string name)
        {
            // Let CanvasScaler and production UI complete the real layout/render.
            // These extra frames and screenshot allocations are deliberately untimed.
            yield return Frames(3);
            var variant = new UiVariant { name = name, phase = SceneManager.GetActiveScene().name,
                width = Screen.width, height = Screen.height,
                renderTargetWidth = renderWidth, renderTargetHeight = renderHeight,
                rootCanvasDimensions = CaptureCanvasDimensions() };
            report.uiVariants.Add(variant);
            RequireVerifiedCanvasDimensions(variant.rootCanvasDimensions);
            var capture = new Scenario();
            SaveOffscreenCapture(Path.Combine(captureDirectory, name + ".png"), capture);
            variant.screenshot = capture.screenshot; variant.screenshotBytes = capture.screenshotBytes;
            variant.screenshotMinimumBrightness = capture.screenshotMinimumBrightness;
            variant.screenshotMaximumBrightness = capture.screenshotMaximumBrightness;
            yield return null;
        }

        private static TextureSnapshot CaptureTextureSnapshot(string checkpoint)
        {
            var snapshot = new TextureSnapshot { checkpoint = checkpoint };
            var entries = new List<TextureDetails>();
            var seen = new HashSet<int>();
            // Enumeration must never load an asset just to measure it.
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (texture == null || !seen.Add(texture.GetInstanceID())) continue;
                var entry = new TextureDetails { name = texture.name, instanceId = texture.GetInstanceID(),
                    width = texture.width, height = texture.height, format = texture.format.ToString(),
                    graphicsFormat = texture.graphicsFormat.ToString(), mipmapCount = texture.mipmapCount,
                    isReadable = texture.isReadable, hideFlags = texture.hideFlags.ToString() };
                try
                {
                    long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                    if (bytes > 0) { entry.runtimeMemoryBytes = bytes; snapshot.runtimeMemoryBytes += bytes; }
                    else entry.error = "Runtime memory size unavailable/nonpositive: " + bytes;
                }
                catch (Exception error) { entry.error = error.Message; }
                if (entry.runtimeMemoryBytes < 0) snapshot.unavailableCount++;
                entries.Add(entry);
            }
            snapshot.entries = entries.OrderBy(entry => entry.name, StringComparer.Ordinal).ThenBy(entry => entry.instanceId).ToArray();
            snapshot.entryCount = snapshot.entries.Length;
            return snapshot;
        }

        private void PersistTextureSnapshot(TextureSnapshot snapshot)
        {
            snapshot.detailsFile = Path.Combine(captureDirectory, snapshot.checkpoint + "-textures.json");
            using (var stream = new FileStream(snapshot.detailsFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(snapshot, true));
            // Do not keep hundreds of texture names/format strings alive through
            // subsequent memory samples. No forced GC is introduced into the run.
            snapshot.entries = null;
        }

        private void RestoreTextureDetailsForFinalReport()
        {
            foreach (Scenario scenario in report.scenarios)
            {
                TextureSnapshot snapshot = scenario.textures;
                if (snapshot == null || snapshot.entries != null) continue;
                TextureSnapshot saved = JsonUtility.FromJson<TextureSnapshot>(File.ReadAllText(snapshot.detailsFile));
                if (saved == null || saved.checkpoint != snapshot.checkpoint || saved.entries == null ||
                    saved.entries.Length != snapshot.entryCount)
                    throw new InvalidDataException("Texture detail evidence is incomplete: " + snapshot.detailsFile);
                snapshot.entries = saved.entries;
            }
        }

        private CanvasDimensions[] CaptureCanvasDimensions()
        {
            var result = new List<CanvasDimensions>();
            foreach (CanvasConfiguration configuration in canvasConfigurations)
            {
                Canvas canvas = configuration.Canvas;
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                var rect = (RectTransform)canvas.transform;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                var item = new CanvasDimensions { path = ObjectPath(canvas.transform), renderMode = canvas.renderMode.ToString(),
                    renderingDisplaySize = canvas.renderingDisplaySize, pixelRect = canvas.pixelRect,
                    cameraPixelRect = offscreenCamera.pixelRect, logicalSize = rect.rect.size, scaleFactor = canvas.scaleFactor,
                    expectedScaleFactor = canvas.scaleFactor, scalerMode = scaler == null ? "none" : scaler.uiScaleMode.ToString(),
                    screenMatchMode = scaler == null ? "none" : scaler.screenMatchMode.ToString() };
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    float wr = renderWidth / scaler.referenceResolution.x, hr = renderHeight / scaler.referenceResolution.y;
                    item.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) :
                        scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) :
                        Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                }
                item.verified = canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == offscreenCamera &&
                    (scaler == null || scaler.isActiveAndEnabled) &&
                    Mathf.Abs(item.renderingDisplaySize.x - renderWidth) < .5f && Mathf.Abs(item.renderingDisplaySize.y - renderHeight) < .5f &&
                    Mathf.Abs(item.pixelRect.width - renderWidth) < .5f && Mathf.Abs(item.pixelRect.height - renderHeight) < .5f &&
                    Mathf.Abs(item.cameraPixelRect.width - renderWidth) < .5f && Mathf.Abs(item.cameraPixelRect.height - renderHeight) < .5f &&
                    item.scaleFactor > 0 && Mathf.Abs(item.scaleFactor - item.expectedScaleFactor) < .001f &&
                    Mathf.Abs(item.logicalSize.x - renderWidth / item.scaleFactor) < 1f &&
                    Mathf.Abs(item.logicalSize.y - renderHeight / item.scaleFactor) < 1f;
                result.Add(item);
            }
            return result.ToArray();
        }

        private static void RequireVerifiedCanvasDimensions(CanvasDimensions[] dimensions)
        {
            if (dimensions.Length == 0 || dimensions.Any(item => !item.verified))
                throw new InvalidOperationException("Target dimensions did not drive all actual root Canvas/CanvasScaler geometry; refusing false multi-resolution evidence. See rootCanvasDimensions.");
        }

        private static IEnumerator Frames(int count) { for (int frame = 0; frame < count; frame++) yield return null; }
        private static Button FindButton(string name, bool required = true)
        {
            GameObject found = GameObject.Find(name);
            Button button = found == null ? null : found.GetComponent<Button>();
            if (button != null && button.isActiveAndEnabled && button.IsInteractable()) return button;
            if (required) throw new InvalidOperationException("Missing or disabled action: " + name);
            return null;
        }
        private void Click(string name)
        {
            FindButton(name).onClick.Invoke();
            if (offscreenCamera != null) RefreshOffscreenCanvases();
        }

        private void CreateOffscreenRenderer()
        {
            var cameraHost = new GameObject("Player Audit Offscreen Camera", typeof(Camera));
            cameraHost.transform.SetParent(transform, false);
            offscreenCamera = cameraHost.GetComponent<Camera>();
            offscreenCamera.enabled = false;
            offscreenCamera.clearFlags = CameraClearFlags.SolidColor;
            offscreenCamera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
            offscreenCamera.orthographic = true;
            offscreenCamera.orthographicSize = renderHeight * .5f;
            offscreenCamera.aspect = renderWidth / (float)renderHeight;
            offscreenCamera.nearClipPlane = .1f;
            offscreenCamera.farClipPlane = 2000f;
            offscreenCamera.transform.position = new Vector3(0, 0, -1000);
            offscreenCamera.cullingMask = 1 << CaptureLayer;
            offscreenCamera.allowHDR = false;
            offscreenCamera.allowMSAA = false;
            renderTarget = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            renderTarget.name = "Player Audit " + renderWidth + "x" + renderHeight + " Target";
            if (!renderTarget.Create()) throw new InvalidOperationException("Cannot create the offscreen render target.");
            offscreenCamera.targetTexture = renderTarget;
            renderRequest = new RenderPipeline.StandardRequest { destination = renderTarget };
            report.renderPipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().FullName;
            VerifyRenderTarget();
        }

        // Called only when setting up a scenario or changing UI outside sampling.
        // It also finds fresh root Canvases and descendants made by production Render().
        private void RefreshOffscreenCanvases()
        {
            RestoreCanvasConfigurations();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled) continue;
                var rect = (RectTransform)canvas.transform;
                Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
                var configuration = new CanvasConfiguration { Canvas = canvas, Mode = canvas.renderMode, Camera = canvas.worldCamera,
                    PlaneDistance = canvas.planeDistance, ScaleFactor = canvas.scaleFactor, Size = rect.sizeDelta,
                    Position = rect.localPosition, Rotation = rect.localRotation, Scale = rect.localScale,
                    Descendants = descendants, Layers = new int[descendants.Length] };
                for (int index = 0; index < descendants.Length; index++)
                {
                    configuration.Layers[index] = descendants[index].gameObject.layer;
                    descendants[index].gameObject.layer = CaptureLayer;
                }
                canvasConfigurations.Add(configuration);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = offscreenCamera;
                canvas.planeDistance = 100f;
                RefreshCanvasScaler(canvas);
            }
            if (canvasConfigurations.Count == 0) throw new InvalidOperationException("No active production root Canvas found for offscreen rendering.");
            Canvas.ForceUpdateCanvases();
            VerifyRenderTarget();
            offscreenEnabled = true;
        }

        private void RestoreCanvasConfigurations()
        {
            offscreenEnabled = false;
            foreach (CanvasConfiguration configuration in canvasConfigurations)
            {
                for (int index = 0; index < configuration.Descendants.Length; index++)
                    if (configuration.Descendants[index] != null)
                        configuration.Descendants[index].gameObject.layer = configuration.Layers[index];
                if (configuration.Canvas == null) continue;
                Canvas canvas = configuration.Canvas;
                canvas.renderMode = configuration.Mode; canvas.worldCamera = configuration.Camera;
                canvas.planeDistance = configuration.PlaneDistance; canvas.scaleFactor = configuration.ScaleFactor;
                var rect = (RectTransform)canvas.transform;
                rect.sizeDelta = configuration.Size; rect.localPosition = configuration.Position;
                rect.localRotation = configuration.Rotation; rect.localScale = configuration.Scale;
                RefreshCanvasScaler(canvas);
            }
            canvasConfigurations.Clear();
        }

        private static void RefreshCanvasScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || !scaler.isActiveAndEnabled) return;
            // Rebind its cached previous scale after switching rendering displays.
            // Otherwise restoring Canvas.scaleFactor can leave CanvasScaler's cache
            // at the target scale and prevent it from applying that scale again.
            // This is setup/restore only, never part of the sampled frame loop.
            scaler.enabled = false;
            scaler.enabled = true;
        }

        private void VerifyRenderTarget()
        {
            if (renderTarget == null || !renderTarget.IsCreated() || renderTarget.width != renderWidth || renderTarget.height != renderHeight ||
                offscreenCamera == null || offscreenCamera.targetTexture != renderTarget || offscreenCamera.pixelWidth != renderWidth || offscreenCamera.pixelHeight != renderHeight)
                throw new InvalidOperationException("Offscreen render target/camera must remain exactly " + renderWidth + "x" + renderHeight + ".");
        }

        private void LateUpdate()
        {
            if (finished || !offscreenEnabled) return;
            try
            {
                VerifyRenderTarget();
                long before = System.Diagnostics.Stopwatch.GetTimestamp();
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(offscreenCamera, renderRequest);
                else offscreenCamera.Render();
                lastRenderCallbackMs = (System.Diagnostics.Stopwatch.GetTimestamp() - before) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                renderedFrames++;
            }
            catch (Exception error) { Fail(error); }
        }

        private void SaveOffscreenCapture(string path, Scenario scenario)
        {
            VerifyRenderTarget();
            if (File.Exists(path)) throw new IOException("Refusing to replace capture: " + path);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = renderTarget;
                pixels = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int index = 0; index < samples.Length; index += 997)
                {
                    int brightness = (samples[index].r + samples[index].g + samples[index].b) / 3;
                    minimum = Math.Min(minimum, brightness); maximum = Math.Max(maximum, brightness);
                }
                scenario.screenshotMinimumBrightness = minimum; scenario.screenshotMaximumBrightness = maximum;
                byte[] png = pixels.EncodeToPNG();
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(png, 0, png.Length);
                scenario.screenshot = path; scenario.screenshotBytes = png.Length;
                if (maximum - minimum < 20 || png.Length == 0)
                    throw new InvalidOperationException("Actual offscreen UI image is blank: " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Destroy(pixels);
            }
        }

        private void ReleaseOffscreenRenderer()
        {
            RestoreCanvasConfigurations();
            if (offscreenCamera != null) { offscreenCamera.targetTexture = null; Destroy(offscreenCamera.gameObject); offscreenCamera = null; }
            if (renderTarget != null) { renderTarget.Release(); Destroy(renderTarget); renderTarget = null; }
            renderRequest = null;
        }

        private static ObjectInventory Inventory(string name)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return new ObjectInventory { checkpoint = name, gameObjects = objects.Length, activeGameObjects = objects.Count(item => item.activeInHierarchy),
                graphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                textures = Resources.FindObjectsOfTypeAll<Texture>().Length, sprites = Resources.FindObjectsOfTypeAll<Sprite>().Length,
                materials = Resources.FindObjectsOfTypeAll<Material>().Length, audioClips = Resources.FindObjectsOfTypeAll<AudioClip>().Length,
                managedHeapBytes = GC.GetTotalMemory(false) };
        }

        // Flatten nested routines so setup/capture exceptions cannot silently abandon the report.
        private IEnumerator Guarded(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(root);
            while (!finished && stack.Count > 0)
            {
                object next = null;
                try
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
                    next = current.Current;
                }
                catch (Exception error) { Fail(error); yield break; }
                if (next is IEnumerator child) stack.Push(child);
                else yield return next;
            }
        }

        private void Update()
        {
            if (!finished && reservedOutput && Time.realtimeSinceStartupAsDouble - startedAt > TimeoutSeconds)
                Fail(new TimeoutException("Player performance audit exceeded 180 seconds."));
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                Fail(new InvalidOperationException(message + "\n" + stack));
        }
        private void Complete() { report.status = "passed"; Finish(0); }
        private void Fail(Exception error)
        {
            if (finished) return;
            report.status = "failed"; report.error = error.ToString(); Finish(1);
        }
        private void Finish(int exitCode)
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
            foreach (Counter counter in counters) counter.Dispose();
            ReleaseOffscreenRenderer();
            try
            {
                RestoreTextureDetailsForFinalReport();
                if (reservedOutput) File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception error)
            {
                report.status = "failed"; report.error = (report.error ?? string.Empty) + "\nFinal report evidence: " + error;
                exitCode = 1;
                try { if (reservedOutput) File.WriteAllText(outputPath, JsonUtility.ToJson(report, true)); }
                catch (Exception writeError) { Debug.LogError("Cannot write audit report: " + writeError); }
            }
            if (exitCode != 0) Debug.LogError("Player performance audit failed: " + report.error);
            Application.Quit(exitCode);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            foreach (Counter counter in counters) counter.Dispose();
            ReleaseOffscreenRenderer();
        }
    }
}
