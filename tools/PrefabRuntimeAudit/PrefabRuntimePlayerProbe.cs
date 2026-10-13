using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.PrefabAudit
{
    // Diagnostic project only. This source must be absent from the delivered Player.
    [DefaultExecutionOrder(32000)]
    public sealed class PrefabRuntimePlayerProbe : MonoBehaviour
    {
        private const string Company = "ShatteredSailAudit", Product = "PrefabRuntimeOptimization20261014";
        private const string Seed = "SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int ExpectedCheckpoints = 68;
        private static readonly string[] Ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private readonly Report report = new Report();
        private string output, evidence;
        private bool finished, reserved;
        private double started;
        private Camera auditCamera;
        private RenderTexture auditTarget;
        private RenderPipeline.StandardRequest auditRequest;
        private int renderedFrames;
        private readonly List<CanvasBinding> canvasBindings = new List<CanvasBinding>();
        private sealed class CanvasBinding { public Canvas canvas; public RenderMode mode; public Camera camera; public float plane; }
        private readonly Dictionary<string, int> actionOccurrences = new Dictionary<string, int>();
        private ActionTiming pendingAction;
        private long pendingStarted;
        private bool visualCapture;
        private string stage = "Startup";
        private int fixtureLoads;

        [Serializable] private sealed class ActionTiming
        {
            public string id, stage, action, utc, status = "pending", scene;
            public int occurrence, startFrame, firstRenderedFrame, readyRenderedFrame;
            public double callbackMs = -1, bindAndLayoutMs = -1, firstRenderedMs = -1, readyRenderedMs = -1;
            public string targetScene;
            public int targetSceneHandle, targetSceneLoadedFrame = -1;
            public double targetSceneLoadedMs = -1;
            public bool targetSceneRenderVerified;
        }

        [Serializable] private sealed class Report
        {
            public string status = "running", error, startedUtc, completedUtc, unity, company, product, persistentDataPath, graphicsApi, gpu;
            public string method = "Non-Development Windows x64 Mono diagnostic Player; isolated identity and fixture. Hidden window with one identical 1920x1080 SDR offscreen target and explicit production Canvas rendering at target 60 FPS, VSync off. Controlled-rendering process RAM totals, not normal visible-window absolute RAM. Actual camera render callbacks counted. Production UI callbacks plus seeded fixture/reflection, not OS input or earned progression. No forced GC or probe-driven UnloadUnusedAssets. Stable checkpoints use 1.5s UTC windows after production cleanup/fade; transient comics labelled separately. Inventories and JSON occur after windows. External 250ms PrivateBytes/WorkingSet are authoritative; release native texture bytes are partial/unavailable, never total graphics memory. Includes common renderer/probe/driver overhead; no dedicated VRAM measurement.";            public bool developmentBuild, identityVerified, controlledOffscreenRendering; public int renderedCameraFrames;
            public int width, height, expectedCheckpoints = ExpectedCheckpoints, facilityCycles, sceneCycles, crewDetails, navigationVariants, ramVariants, menuModalCycles, portModalCycles, achievementPageChanges;
            public double elapsedSeconds;
            public List<Checkpoint> checkpoints = new List<Checkpoint>();
            public bool visualCaptureRun;
            public string probeRevision = "v2";
            public string timingMethod = "v2: Stopwatch around actual production Button.onClick/Render/LoadScene callbacks; lookup excluded. LoadScene CallbackMs is request submission only; TargetSceneLoadedMs is the matching sceneLoaded event. Scene first/ready render requires matching active scene handle and an active production Canvas from that scene, so old-scene frames cannot close a request. Startup callback is bootstrap-before-scene to initial sceneLoaded, not pure UI creation. BindAndLayout is diagnostic canvas rebinding plus forced layout. FirstRendered is actual controlled-camera CPU end-render, not GPU fence/display present. ReadyRendered additionally requires gameplay controller fade/collection complete. No fixed settle waits enter timestamps; timings close in render callback. Superseded same-frame actions retained; only complete paired metrics compared. First-in-process is not cold disk cache. Separate screenshot runs are excluded from performance trials.";
            public List<ActionTiming> actions = new List<ActionTiming>();
        }
        [Serializable] private sealed class Checkpoint
        {
            public string name, phase, port, detailsFile, processMemoryStatus, windowStartUtc, sampleUtc, inventoryCompletedUtc; public bool transient; public int renderedFramesInWindow; public string[] activeRootCanvases;
            public double elapsedSeconds;
            public long textureBytes, audioBytes, allocatedBytes, reservedBytes, managedUsedBytes, managedReservedBytes, playerPrivateBytes = -1, playerWorkingSetBytes = -1;
            public int textures, audioClips, textureSizeUnavailable, audioSizeUnavailable, gameObjects, graphics, materials, sprites, loadedFonts, productionCollections;
        }
        [Serializable] private sealed class Details
        {
            public string checkpoint;
            public List<TextureDetail> textures = new List<TextureDetail>();
            public List<AudioDetail> audio = new List<AudioDetail>(); public List<RenderTextureDetail> renderTextures = new List<RenderTextureDetail>();
            public List<string> fonts = new List<string>();
        }
        [Serializable] private sealed class TextureDetail
        {
            public string name, format, hideFlags;
            public int instanceId, width, height, mipmaps;
            public bool readable;
            public long bytes;
        }
        [Serializable] private sealed class RenderTextureDetail { public string name, color, depth; public int width, height, samples; public bool created; }
        [Serializable] private sealed class AudioDetail
        {
            public string name, loadType, loadState;
            public int instanceId, channels, frequency;
            public float seconds;
            public long bytes;
        }
        [Serializable] private sealed class UiSnapshot { public string checkpoint; public List<UiGraphic> graphics = new List<UiGraphic>(); }
        [Serializable] private sealed class UiGraphic
        {
            public string path, type, text, font, fontStyle, alignment, texture, sprite;
            public int fontSize; public bool raycast, interactable;
            public Color color; public Vector2[] corners; public Rect uv;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-prefabRuntimePlayerOutput");
            if (at < 0) return;
            var host = new GameObject("Isolated Runtime Memory Player Probe");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<PrefabRuntimePlayerProbe>();
            try
            {
                if (at + 1 >= args.Length || Array.LastIndexOf(args, "-prefabRuntimePlayerOutput") != at)
                    throw new ArgumentException("Supply -prefabRuntimePlayerOutput once with a fresh absolute .json path.");
                probe.Initialize(args[at + 1]);
            }
            catch (Exception error) { probe.Fail(error); }
        }

        private void Initialize(string path)
        {
            started = Time.realtimeSinceStartupAsDouble;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            report.unity = Application.unityVersion; report.company = Application.companyName; report.product = Application.productName;
            report.persistentDataPath = Application.persistentDataPath; report.developmentBuild = UnityEngine.Debug.isDebugBuild;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); report.gpu = SystemInfo.graphicsDeviceName;
            // This guard must precede all PlayerPrefs or persistence mutations.
            string identityDirectory = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", Company, Product));
            if (Application.isEditor || Application.companyName != Company || Application.productName != Product || UnityEngine.Debug.isDebugBuild ||
                !string.Equals(Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar), identityDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing preference writes: non-Development isolated audit identity/path required.");
            report.identityVerified = true;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphical Player required; do not use -nographics.");
            if (!Path.IsPathRooted(path) || !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Absolute fresh .json required.");
            output = Path.GetFullPath(path);
            string outputRoot = Path.GetFullPath("D:/Game/Artifacts/PrefabRuntimeOptimization20261014").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use PrefabRuntimeOptimization20261014 artifact output.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            reserved = true;
            evidence = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "-details");
            if (Directory.Exists(evidence)) throw new IOException("Preserve earlier evidence; choose a new report.");
            Directory.CreateDirectory(evidence);
            OverrideDirectory("VoyageSuspensionPersistence", Path.Combine(evidence, "audit-checkpoints"));
            OverrideDirectory("ProfileSaveJournal", Path.Combine(evidence, "audit-journal"));
            PlayerPrefs.DeleteAll(); PlayerPrefs.Save();
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Application.logMessageReceived += OnLog;
            visualCapture = Array.IndexOf(Environment.GetCommandLineArgs(), "-prefabVisualCapture") >= 0;
            report.visualCaptureRun = visualCapture;
            CreateRenderer();
            SceneManager.sceneLoaded += OnSceneLoaded;
            BeginAction("Startup:BeforeFirstScene");
            StartCoroutine(Guarded(Run()));
        }

        private static void OverrideDirectory(string typeName, string path)
        {
            Type type = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation." + typeName, true);
            FieldInfo field = type.GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null) throw new MissingFieldException(type.FullName, "directoryOverride");
            field.SetValue(null, path);
        }

        private IEnumerator Run()
        {
            yield return null; yield return null;
            if (SceneManager.GetActiveScene().name != "MainMenu") LoadSceneMeasured("MainMenu");
            yield return Settle();
            RequireButton("Achievements");
            report.width = Screen.width; report.height = Screen.height;
            if (report.width != 1920 || report.height != 1080) throw new InvalidOperationException("Start Player with 1920x1080 screen arguments.");
            yield return Measure("01_MainMenu_Fresh");
            Click("Achievements"); yield return Settle(); yield return Measure("02_Achievements");
            Click("Back From Achievements"); yield return ActionRendered();
            yield return ExerciseMenuModals();
            yield return LoadFixture(); yield return Measure("03_Port_Fresh");
            yield return ExercisePortModals();
            string[] facilities = { "Market", "Warehouse", "Tavern", "ComponentShop", "CardWorkshop", "Maintenance", "Shipyard", "Treasure", "Voyage" };
            foreach (string facility in facilities)
            {
                stage = "FirstFacility:" + facility;
                Click("Port Hotspot " + facility); yield return Settle(); RequireVisibleArt(); yield return Measure("04_Facility_" + facility);
                if (facility == "Warehouse") { Click("Warehouse Tab Storage"); yield return Settle(); yield return Measure("04_Facility_WarehouseStorage"); }
                Click("Return To Port Hub"); yield return Settle();
            }
            yield return Measure("05_FacilityCycles_Before");
            for (int cycle = 1; cycle <= 20; cycle++)
            {
                stage = "FacilityCycle:" + cycle;
                Click("Port Hotspot " + (cycle % 2 == 0 ? "Market" : "Warehouse"));
                yield return null; yield return null;
                Click("Return To Port Hub"); yield return null; yield return null;
                report.facilityCycles++;
                if (cycle == 1 || cycle == 10 || cycle == 20) { yield return Settle(); yield return Measure("06_FacilityCycle_" + cycle); }
            }
            foreach (string port in Ports)
            {
                stage = "RegionalVisit:" + port;
                Controller().Session.RestoreProfile(2000m, port); Render(); yield return Settle();
                Click("Port Hotspot Tavern"); yield return Settle();
                for (int crew = 0; crew < 6; crew++)
                {
                    Click("Tavern Crew " + crew); yield return Settle();
                    RequireImage("Tavern Dossier Parchment");
                    Click("DossierPageStory"); Click("DossierPageSpecialCards");
                    // All six identities are actually displayed; one aggregate checkpoint per port.
                    report.crewDetails++;
                    if (crew == 5) yield return Measure("07_TavernDetails_" + port);
                    Click("Return To Port Hub"); yield return Settle();
                }
                Click("Return To Port Hub"); yield return Settle();
                Click("Port Hotspot ComponentShop"); yield return Settle(); RequireVisibleArt(); yield return Measure("08_ComponentShop_" + port);
                Click("Return To Port Hub"); yield return Settle();
            }
            Controller().Session.RestoreProfile(2000m, Ports[0]); Render(); yield return Settle();
            CoreVoyageSession voyage = Controller().Session;
            voyage.Cargo.Water = voyage.Cargo.Food = 100; voyage.Cargo.Wood = 10; voyage.Cargo.Powder = 25; voyage.Cargo.Cannonballs = 40;
            voyage.ConfigureCrewCapabilities(true, false, false, false, false, false);
            if (!voyage.TrySelectChartSeed(Seed, out string reason)) throw new InvalidOperationException(reason);
            voyage.StartVoyage(); Render();
            SeaTerrainType[] weathers = { SeaTerrainType.OpenWater, SeaTerrainType.FogBank, SeaTerrainType.StormFront };
            double[] hours = { 0d, 10d, 14d };
            for (int weather = 0; weather < weathers.Length; weather++)
                for (int time = 0; time < hours.Length; time++)
                {
                    stage = "Navigation:" + weathers[weather] + ":" + time;
                    // Resource-stage fixture only. RestoreGameClock is a port-only save API;
                    // set its private property directly here without advancing a voyage,
                    // consuming supplies, earning rewards or claiming real progression.
                    voyage.Map.TerrainCells[voyage.Map.CellIndex(voyage.Position)] = weathers[weather];
                    SetNavigationFixtureClock(voyage, hours[time]);
                    Render(); yield return Settle(); RequireVisibleArt();
                    yield return Measure("09_Navigation_" + weathers[weather] + "_" + time); report.navigationVariants++;
                }
            voyage = null;
            for (int variant = 0; variant < 4; variant++)
            {
                stage = "RamVariant:" + variant;
                bool enemy = variant >= 2;
                CoreVoyageSession battle = ArrangeRam(enemy, variant % 2 == 0 ? NavalBattleAlignment.HeadOn : NavalBattleAlignment.EnemyBroadside);
                Render(); yield return Settle(); yield return Measure("10_Naval_BeforeRam_" + variant);
                Click(enemy ? "End Battle Turn" : "Battle Ram");
                yield return WaitForComic(); yield return null; yield return null;
                RequireImage("Battle Ram Comic Page"); yield return Measure("11_RamComic_" + variant, true);
                yield return WaitForBattleAnimation(); yield return Settle();
                if (battle.Phase != CoreDemoPhase.Battle || battle.Battle.Stage != NavalBattleStage.Boarding) throw new InvalidOperationException("Ram fixture did not reach boarding.");
                RequireImage("Battle Scene Art"); yield return Measure("12_Boarding_" + variant); report.ramVariants++;
                battle = null;
            }
            stage = "MainMenuReturned";
            LoadSceneMeasured("MainMenu"); yield return Settle(); yield return Measure("13_MainMenu_Returned");
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                stage = "SceneCycle:" + cycle;
                yield return LoadFixture(); yield return Measure("14_SceneCycle_Port_" + cycle);
                Click("Port Hotspot Market"); yield return Settle();
                Click("Return To Port Hub"); yield return Settle();
                LoadSceneMeasured("MainMenu"); yield return Settle(); yield return Measure("15_SceneCycle_Menu_" + cycle);
                report.sceneCycles++;
            }
            if (report.checkpoints.Count != ExpectedCheckpoints || report.facilityCycles != 20 || report.sceneCycles != 3 || report.crewDetails != 36 || report.navigationVariants != 9 || report.ramVariants != 4 || report.menuModalCycles != 6 || report.portModalCycles != 6 || report.achievementPageChanges != 12)
                throw new InvalidOperationException("Incomplete diagnostic sequence; refusing a partial PASS.");
            if (renderedFrames < 100) throw new InvalidOperationException("Fewer than 100 real rendered frames.");
            CaptureFinalFrame();
            report.status = "passed"; Finish(0);
        }

        private IEnumerator ExerciseMenuModals()
        {
            for (int cycle = 1; cycle <= 6; cycle++)
            {
                stage = "MenuModalCycle:" + cycle;
                Click("Settings"); yield return ActionRendered();
                if (cycle == 1 || cycle == 6) { yield return Settle(); yield return Measure("02a_MenuSettings_" + cycle); }
                Click("Back From Settings"); yield return ActionRendered();
                Click("Achievements"); yield return ActionRendered();
                Click("Achievements Next Page"); yield return ActionRendered(); report.achievementPageChanges++;
                Click("Achievements Previous Page"); yield return ActionRendered(); report.achievementPageChanges++;
                if (cycle == 6) { yield return Settle(); yield return Measure("02b_Achievements_Warm"); }
                Click("Back From Achievements"); yield return ActionRendered();
                // Explicit modal fixture: no new-game reset, corrupt save, or user profile mutation.
                InvokeMenu("OpenNewGameConfirmation"); yield return ActionRendered();
                if (cycle == 1 || cycle == 6) { yield return Settle(); yield return Measure("02c_NewGameConfirmation_" + cycle); }
                Click("Cancel New Game"); yield return ActionRendered();
                InvokeMenu("ShowSaveNotice", "隔离诊断提示：检查固定界面结构与关闭操作。"); yield return ActionRendered();
                if (cycle == 1 || cycle == 6) { yield return Settle(); yield return Measure("02d_SaveNotice_" + cycle); }
                Click("Close Voyage Save Notice"); yield return ActionRendered();
                report.menuModalCycles++;
            }
        }
        private IEnumerator ExercisePortModals()
        {
            for (int cycle = 1; cycle <= 6; cycle++)
            {
                stage = "PortModalCycle:" + cycle;
                Click("Port Hub Settings"); yield return ActionRendered();
                if (cycle == 1 || cycle == 6) { yield return Settle(); yield return Measure("03a_PortSettings_" + cycle); }
                Click("Return To Main Menu"); yield return ActionRendered();
                if (cycle == 1 || cycle == 6) { yield return Settle(); yield return Measure("03b_ReturnConfirmation_" + cycle); }
                Click("Cancel Return To Main Menu"); yield return ActionRendered();
                Click("Close Port Settings"); yield return ActionRendered();
                report.portModalCycles++;
            }
        }
        private void InvokeMenu(string method, params object[] arguments)
        {
            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
            MethodInfo callback = typeof(MainMenuController).GetMethod(method, PrivateInstance) ?? throw new MissingMethodException(method);
            BeginAction("InvokeMenu:" + method);
            callback.Invoke(controller, arguments);
            CompleteCallbackAndBind();
        }
        private IEnumerator ActionRendered()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (pendingAction != null)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Action failed to render ready: " + pendingAction.action);
                yield return null;
            }
            // Let destruction queued by the callback finish before the next action.
            yield return null;
        }

        private IEnumerator LoadFixture()
        {
            if (++fixtureLoads == 1) stage = "FirstPort";
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "PrefabRuntimeFixture20261014");
            ProfileGoldPersistence.Save(2000m);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, Ports[0]);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Join(",", CoreWarehouseCollection.Catalog.Take(3).Select(item => item.Id)));
            PlayerPrefs.Save();
            LoadSceneMeasured("CoreGameplayDemo"); yield return ActionRendered();
            if (Controller().IsTutorialActive || Controller().Session.Phase != CoreDemoPhase.Port) throw new InvalidOperationException("Port fixture initialization failed.");
            Controller().Session.PortWarehouse.Restore("v1;断锚港,water,600;断锚港,food,500;断锚港,wood,120;断锚港,iron,35;断锚港,cloth,42");
            Render(); yield return Settle();
        }

        private static CoreVoyageSession ArrangeRam(bool enemy, NavalBattleAlignment alignment)
        {
            var session = new CoreVoyageSession();
            if (session.Phase != CoreDemoPhase.Port || !session.RestoreGameClock(0d)) throw new InvalidOperationException("Ram fixture must start at port.");
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.SetSeed("BATTLE-RESPONSIVENESS-REGRESSION"); session.StartVoyage();
            if (session.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Ram fixture must enter sailing before creating an encounter.");
            Field("session").SetValue(Controller(), session);
            var encounter = new CoreEncounter("memory_battle", "内存诊断敌舰", "海战", "隔离资源阶段fixture", true, false, 2, 180);
            var node = new CoreMapNode { Id = 998, Position = session.Position, Encounter = encounter, TerrainConstraint = EventTerrainConstraint.OpenSea };
            typeof(CoreVoyageSession).GetField("activeNode", PrivateInstance).SetValue(session, node);
            typeof(CoreVoyageSession).GetMethod("StartBattle", PrivateInstance).Invoke(session, new object[] { encounter, false });
            if (session.Phase != CoreDemoPhase.Battle || session.Battle == null || session.Battle.Stage != NavalBattleStage.Preparation)
                throw new InvalidOperationException("Ram fixture encounter must enter preparation.");
            if (!session.EndBattleTurn()) throw new InvalidOperationException("Cannot enter naval phase.");
            CoreBattleState battle = session.Battle;
            if (session.Phase != CoreDemoPhase.Battle || battle.Stage != NavalBattleStage.Naval) throw new InvalidOperationException("Preparation must finish in naval phase.");
            battle.PlayerBlock = 0; battle.PlayerLoadedShot = 0; battle.PlayerFireRounds = 0; battle.PlayerDeckBuff = 0;
            var profile = new CoreBattleProfile(enemy ? CoreBossKind.Legendary : CoreBossKind.None, CoreBattleArchetype.Raider,
                battle.EnemyMaxHull, 1, 7, 0, 0, battle.EnemyOrdinaryCrew, 1f, 1f, enemy ? 1d : 0d, 120f);
            typeof(CoreBattleState).GetProperty("Profile").SetValue(battle, profile);
            battle.Alignment = alignment; battle.DistanceMeters = 80f; battle.PlayerMovingTowardEnemy = !enemy;
            battle.EnemyBurnDamage = 0; battle.EnemyBurnTurns = 0;
            battle.Hand.Clear(); battle.Hand.AddRange(new[] { "card_brace", "card_emergency_repair" }.Select(id => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == id)));
            battle.Energy = battle.MaxEnergy; battle.CardsPlayedThisTurn = 0;
            if (enemy ? !session.EnemyWillRam : !session.CanRam(out _)) throw new InvalidOperationException("Invalid ram fixture.");
            return session;
        }

        private static void SetNavigationFixtureClock(CoreVoyageSession voyage, double hours)
        {
            if (voyage.Phase != CoreDemoPhase.Sailing || double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0d)
                throw new InvalidOperationException("Invalid navigation resource-stage clock fixture.");
            PropertyInfo clock = typeof(CoreVoyageSession).GetProperty("ElapsedGameHours", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setter = clock?.GetSetMethod(true);
            if (setter == null) throw new MissingMethodException("CoreVoyageSession.ElapsedGameHours private setter");
            setter.Invoke(voyage, new object[] { hours });
            if (voyage.ElapsedGameHours != hours || voyage.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Navigation clock fixture was not applied.");
        }

        private static CoreGameplayDemoController Controller() => Object.FindFirstObjectByType<CoreGameplayDemoController>() ?? throw new InvalidOperationException("No gameplay controller.");
        private static FieldInfo Field(string name) => typeof(CoreGameplayDemoController).GetField(name, PrivateInstance) ?? throw new MissingFieldException(name);
        private void Render()
        {
            CoreGameplayDemoController controller = Controller();
            MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance);
            BeginAction("Render:" + Controller().Session.Phase);
            render.Invoke(controller, null);
            CompleteCallbackAndBind();
        }
        private static Button RequireButton(string name)
        {
            Button button = GameObject.Find(name)?.GetComponent<Button>();
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) throw new InvalidOperationException("Missing/disabled UI action: " + name);
            return button;
        }
        private void Click(string name)
        {
            Button button = RequireButton(name);
            BeginAction("Click:" + name);
            button.onClick.Invoke();
            CompleteCallbackAndBind();
        }
        private void LoadSceneMeasured(string name)
        {
            BeginAction("LoadScene:" + name);
            SceneManager.LoadScene(name, LoadSceneMode.Single);
            CompleteCallbackAndBind();
        }
        private void BeginAction(string action)
        {
            if (pendingAction != null) pendingAction.status = pendingAction.firstRenderedMs >= 0 ? "superseded-before-ready" : "superseded-before-render";
            actionOccurrences.TryGetValue(action, out int occurrence);
            actionOccurrences[action] = ++occurrence;
            pendingAction = new ActionTiming { id = (report.actions.Count + 1).ToString("D4"), stage = stage, action = action,
                occurrence = occurrence, utc = DateTime.UtcNow.ToString("O"), startFrame = Time.frameCount, scene = SceneManager.GetActiveScene().name };
            if (action.StartsWith("LoadScene:", StringComparison.Ordinal)) pendingAction.targetScene = action.Substring("LoadScene:".Length);
            else if (action == "Startup:BeforeFirstScene") pendingAction.targetScene = "MainMenu";
            report.actions.Add(pendingAction);
            pendingStarted = Stopwatch.GetTimestamp();
        }
        private double ActionMilliseconds() => (Stopwatch.GetTimestamp() - pendingStarted) * 1000d / Stopwatch.Frequency;
        private void CompleteCallbackAndBind()
        {
            if (pendingAction != null) pendingAction.callbackMs = ActionMilliseconds();
            double before = ActionMilliseconds();
            RefreshCanvases();
            if (pendingAction != null) pendingAction.bindAndLayoutMs = ActionMilliseconds() - before;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Covers initial Awake/OnEnable without waiting for the sampling coroutine.
            if (pendingAction != null && pendingAction.targetScene == scene.name)
            {
                pendingAction.targetSceneHandle = scene.handle;
                pendingAction.targetSceneLoadedMs = ActionMilliseconds();
                pendingAction.targetSceneLoadedFrame = Time.frameCount;
                if (pendingAction.action == "Startup:BeforeFirstScene") pendingAction.callbackMs = pendingAction.targetSceneLoadedMs;
            }
            RefreshCanvases();
        }
        private void RecordRenderedAction()
        {
            if (pendingAction == null) return;
            if (!string.IsNullOrEmpty(pendingAction.targetScene))
            {
                Scene active = SceneManager.GetActiveScene();
                if (pendingAction.targetSceneLoadedMs < 0 || !active.isLoaded || active.name != pendingAction.targetScene || active.handle != pendingAction.targetSceneHandle ||
                    !canvasBindings.Any(binding => binding.canvas != null && binding.canvas.isActiveAndEnabled && binding.canvas.gameObject.scene.handle == active.handle)) return;
                pendingAction.targetSceneRenderVerified = true;
            }
            double elapsed = ActionMilliseconds();
            if (pendingAction.firstRenderedMs < 0) { pendingAction.firstRenderedMs = elapsed; pendingAction.firstRenderedFrame = Time.frameCount; }
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            bool collecting = controller != null && Field("runtimeArtCollection").GetValue(controller) != null;
            CanvasGroup fade = controller == null ? null : (CanvasGroup)Field("transitionCanvasGroup").GetValue(controller);
            if (collecting || (fade != null && fade.alpha < .999f)) return;
            pendingAction.readyRenderedMs = elapsed; pendingAction.readyRenderedFrame = Time.frameCount; pendingAction.status = "rendered-ready";
            pendingAction = null;
        }
        private static void RequireImage(string name)
        {
            RawImage image = GameObject.Find(name)?.GetComponent<RawImage>();
            if (image == null || image.texture == null || !image.isActiveAndEnabled) throw new InvalidOperationException("Expected visible resource: " + name);
        }
        private static void RequireVisibleArt()
        {
            if (!Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Any(image => image.isActiveAndEnabled && image.texture != null)) throw new InvalidOperationException("Visible art missing.");
        }
        private IEnumerator Settle()
        {
            RefreshCanvases();
            yield return new WaitForSecondsRealtime(.8f);
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (true)
            {
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                bool collecting = controller != null && Field("runtimeArtCollection").GetValue(controller) != null;
                CanvasGroup fade = controller == null ? null : (CanvasGroup)Field("transitionCanvasGroup").GetValue(controller);
                if (!collecting && (fade == null || fade.alpha >= .999f)) break;
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Production collection/fade did not settle.");
                yield return null;
            }
            Canvas.ForceUpdateCanvases(); yield return null; yield return null;
        }
        private static IEnumerator WaitForComic()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 3d;
            while (GameObject.Find("Battle Ram Comic Page") == null)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Real ram comic missing.");
                yield return null;
            }
        }
        private IEnumerator WaitForBattleAnimation()
        {
            // The current sequence has four 4-second pages. The old 12-second
            // assumption was stale. Exercise the ordinary manual advance button;
            // hidden-window focus policy must not decide whether a fixture finishes.
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            int lastAdvancedPanel = -1;
            while (Controller().IsBattleAnimationPlaying)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Battle animation did not finish.");
                Button advance = GameObject.Find("Battle Ram Comic Advance")?.GetComponent<Button>();
                int panel = (int)Field("ramComicPanelIndex").GetValue(Controller());
                if (advance != null && advance.isActiveAndEnabled && advance.IsInteractable() && panel > lastAdvancedPanel)
                {
                    // Let each real page be rendered before requesting the next.
                    yield return new WaitForSecondsRealtime(.25f);
                    Click("Battle Ram Comic Advance");
                    lastAdvancedPanel = panel;
                }
                yield return null;
            }
        }

        private IEnumerator Measure(string name, bool transient = false)
        {
            RefreshCanvases();
            string begin = DateTime.UtcNow.ToString("O");
            int framesBefore = renderedFrames;
            if (transient) { yield return null; yield return null; }
            else yield return new WaitForSecondsRealtime(1.5f);
            int actualFrames = renderedFrames - framesBefore;
            if (actualFrames < (transient ? 1 : 30)) throw new InvalidOperationException("Insufficient real camera frames for " + name + ": " + actualFrames);
            Sample(name, begin, actualFrames, transient);
            if (visualCapture) CaptureFrame(name);
        }

        private void Sample(string name, string windowStart, int framesInWindow, bool transient)
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var point = new Checkpoint { name = name, elapsedSeconds = Time.realtimeSinceStartupAsDouble - started,
                windowStartUtc = windowStart, sampleUtc = DateTime.UtcNow.ToString("O"), transient = transient, renderedFramesInWindow = framesInWindow,
                activeRootCanvases = canvasBindings.Where(b => b.canvas != null && b.canvas.isActiveAndEnabled).Select(b => b.canvas.name).ToArray(),
                phase = controller?.Session?.Phase.ToString() ?? "MainMenu", port = controller?.Session?.CurrentPort,
                allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(), reservedBytes = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),
                managedUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(), managedReservedBytes = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong(),
                gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length, graphics = Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None).Length,
                materials = Resources.FindObjectsOfTypeAll<Material>().Length, sprites = Resources.FindObjectsOfTypeAll<Sprite>().Length,
                productionCollections = controller == null ? -1 : (int)Field("runtimeArtCollectionCount").GetValue(controller) };
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    process.Refresh();
                    point.playerPrivateBytes = process.PrivateMemorySize64 > 0 ? process.PrivateMemorySize64 : -1;
                    point.playerWorkingSetBytes = process.WorkingSet64 > 0 ? process.WorkingSet64 : -1;
                    point.processMemoryStatus = point.playerPrivateBytes > 0 && point.playerWorkingSetBytes > 0 ? "available; verify external sampler" : "unavailable; use external sampler";
                }
            }
            catch (Exception error) { point.processMemoryStatus = "unavailable: " + error.Message; }
            var details = new Details { checkpoint = name };
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (texture == null) continue;
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                point.textureBytes += Math.Max(0, bytes);
                if (bytes <= 0) point.textureSizeUnavailable++;
                details.textures.Add(new TextureDetail { name = texture.name, instanceId = texture.GetInstanceID(), width = texture.width, height = texture.height,
                    format = texture.format.ToString(), mipmaps = texture.mipmapCount, readable = texture.isReadable, hideFlags = texture.hideFlags.ToString(), bytes = bytes });
            }
            foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (clip == null) continue;
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip); point.audioBytes += Math.Max(0, bytes);
                if (bytes <= 0) point.audioSizeUnavailable++;
                details.audio.Add(new AudioDetail { name = clip.name, instanceId = clip.GetInstanceID(), channels = clip.channels, frequency = clip.frequency,
                    seconds = clip.length, loadType = clip.loadType.ToString(), loadState = clip.loadState.ToString(), bytes = bytes });
            }
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>()) if (font != null) details.fonts.Add(font.name);
            point.textures = details.textures.Count; point.audioClips = details.audio.Count; point.loadedFonts = details.fonts.Count;
            // Release native-byte APIs can omit real GPU textures; OS samples are authoritative.
            foreach (RenderTexture rt in Resources.FindObjectsOfTypeAll<RenderTexture>())
                details.renderTextures.Add(new RenderTextureDetail { name = rt.name, color = rt.graphicsFormat.ToString(), depth = rt.depthStencilFormat.ToString(), width = rt.width, height = rt.height, samples = rt.antiAliasing, created = rt.IsCreated() });
            details.textures.Sort((a, b) => b.bytes.CompareTo(a.bytes));
            point.detailsFile = Path.Combine(evidence, name + ".json");
            using (var stream = new FileStream(point.detailsFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(details, true));
            report.checkpoints.Add(point);
            // Flush partial scalar evidence so a killed/timed-out process cannot appear as a clean run.
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            point.inventoryCompletedUtc = DateTime.UtcNow.ToString("O");
            UnityEngine.Debug.Log("MEMORY_CHECKPOINT " + name + " elapsed=" + point.elapsedSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " textures=" + point.textureBytes);
        }

        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (!finished && stack.Count > 0)
            {
                object next;
                try
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
                    next = current.Current;
                }
                catch (Exception error) { Fail(error); yield break; }
                if (next is IEnumerator child) stack.Push(child); else yield return next;
            }
        }
        private void Update()
        {
            if (!finished && reserved && Time.realtimeSinceStartupAsDouble - started > 600d) Fail(new TimeoutException("Player memory sequence exceeded 600 seconds."));
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Fail(new InvalidOperationException(message + "\n" + stack));
        }
        private void Fail(Exception error)
        {
            if (finished) return;
            report.status = "failed"; report.error = error.ToString(); Finish(1);
        }
        private void Finish(int exitCode)
        {
            if (finished) return;
            finished = true; Application.logMessageReceived -= OnLog; SceneManager.sceneLoaded -= OnSceneLoaded;
            report.renderedCameraFrames = renderedFrames;
            report.completedUtc = DateTime.UtcNow.ToString("O"); report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - started;
            try { if (reserved) File.WriteAllText(output, JsonUtility.ToJson(report, true)); }
            catch (Exception error) { exitCode = 1; UnityEngine.Debug.LogError("Cannot write memory evidence: " + error); }
            if (exitCode != 0) UnityEngine.Debug.LogError("Runtime memory diagnostic failed: " + report.error);
            ReleaseRenderer();
            Application.Quit(exitCode);
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; SceneManager.sceneLoaded -= OnSceneLoaded; ReleaseRenderer(); }

        private void CreateRenderer()
        {
            var host = new GameObject("Memory Audit Controlled Renderer", typeof(Camera));
            host.transform.SetParent(transform, false);
            auditCamera = host.GetComponent<Camera>();
            auditCamera.enabled = false; auditCamera.orthographic = true;
            auditCamera.orthographicSize = 540f; auditCamera.aspect = 1920f / 1080f;
            auditCamera.nearClipPlane = .1f; auditCamera.farClipPlane = 2000f;
            auditCamera.transform.position = new Vector3(0f, 0f, -1000f);
            auditCamera.cullingMask = ~0; auditCamera.clearFlags = CameraClearFlags.SolidColor;
            auditCamera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
            auditCamera.allowHDR = false; auditCamera.allowMSAA = false;
            auditTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            auditTarget.name = "Common Memory Audit SDR Output";
            if (!auditTarget.Create()) throw new InvalidOperationException("Cannot create controlled render target.");
            auditCamera.targetTexture = auditTarget;
            auditRequest = new RenderPipeline.StandardRequest { destination = auditTarget };
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            Camera.onPostRender += OnBuiltinCameraRendered;
            report.controlledOffscreenRendering = true;
        }
        private void OnCameraRendered(ScriptableRenderContext context, Camera camera) { if (camera == auditCamera) { renderedFrames++; RecordRenderedAction(); } }
        private void OnBuiltinCameraRendered(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null && camera == auditCamera) { renderedFrames++; RecordRenderedAction(); } }
        private void RefreshCanvases()
        {
            if (auditCamera == null) return;
            canvasBindings.RemoveAll(binding => binding.canvas == null);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera != auditCamera) camera.enabled = false;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled) continue;
                if (!canvasBindings.Any(binding => binding.canvas == canvas))
                    canvasBindings.Add(new CanvasBinding { canvas = canvas, mode = canvas.renderMode, camera = canvas.worldCamera, plane = canvas.planeDistance });
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = auditCamera; canvas.planeDistance = 100f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.isActiveAndEnabled) { scaler.enabled = false; scaler.enabled = true; }
            }
            Canvas.ForceUpdateCanvases();
        }
        private void LateUpdate()
        {
            if (finished || auditCamera == null || canvasBindings.Count == 0) return;
            try
            {
                if (!auditTarget.IsCreated() || auditCamera.pixelWidth != 1920 || auditCamera.pixelHeight != 1080)
                    throw new InvalidOperationException("Controlled output size changed.");
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(auditCamera, auditRequest);
                else auditCamera.Render();
            }
            catch (Exception error) { Fail(error); }
        }
        private void ReleaseRenderer()
        {
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            Camera.onPostRender -= OnBuiltinCameraRendered;
            foreach (CanvasBinding binding in canvasBindings)
                if (binding.canvas != null) { binding.canvas.renderMode = binding.mode; binding.canvas.worldCamera = binding.camera; binding.canvas.planeDistance = binding.plane; }
            canvasBindings.Clear();
            if (auditCamera != null) { auditCamera.targetTexture = null; Destroy(auditCamera.gameObject); auditCamera = null; }
            if (auditTarget != null) { auditTarget.Release(); Destroy(auditTarget); auditTarget = null; }
        }
        private void CaptureFinalFrame() => CaptureFrame("final-menu");
        private void CaptureFrame(string name)
        {
            // All measured UTC windows have closed. This readback cannot enter their peaks/medians.
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = auditTarget;
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply(false, false);
                Color32[] values = pixels.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int i = 0; i < values.Length; i += 997)
                {
                    int brightness = (values[i].r + values[i].g + values[i].b) / 3;
                    minimum = Math.Min(minimum, brightness); maximum = Math.Max(maximum, brightness);
                }
                if (maximum - minimum < 20) throw new InvalidOperationException("Controlled final production UI rendered blank.");
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), name + ".png"), pixels.EncodeToPNG());
                if (visualCapture)
                {
                    var snapshot = new UiSnapshot { checkpoint = name };
                    foreach (Graphic graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                    {
                        if (!graphic.isActiveAndEnabled) continue;
                        var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
                        var item = new UiGraphic { path = HierarchyPath(graphic.transform), type = graphic.GetType().Name, color = graphic.color,
                            raycast = graphic.raycastTarget, corners = corners.Select(p => RectTransformUtility.WorldToScreenPoint(auditCamera, p)).ToArray() };
                        if (graphic is Text text) { item.text = text.text; item.font = text.font == null ? null : text.font.name; item.fontStyle = text.fontStyle.ToString(); item.fontSize = text.fontSize; item.alignment = text.alignment.ToString(); }
                        if (graphic is RawImage raw) { item.texture = raw.texture == null ? null : raw.texture.name; item.uv = raw.uvRect; }
                        if (graphic is Image image) { item.sprite = image.sprite == null ? null : image.sprite.name; item.texture = image.mainTexture == null ? null : image.mainTexture.name; }
                        Selectable selectable = graphic.GetComponent<Selectable>(); item.interactable = selectable != null && selectable.IsInteractable();
                        snapshot.graphics.Add(item);
                    }
                    snapshot.graphics.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), name + ".ui.json"), JsonUtility.ToJson(snapshot, true));
                }
            }
            finally { RenderTexture.active = previous; if (pixels != null) Destroy(pixels); }
        }
        private static string HierarchyPath(Transform node)
        {
            string path = node.name;
            while (node.parent != null) { node = node.parent; path = node.name + "/" + path; }
            return path;
        }
    }
}
