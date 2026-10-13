using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.StartupAudit
{
    // Copied only into an isolated non-Development diagnostic Player.
    public sealed class StartupMemoryProbe : MonoBehaviour
    {
        private const string Company = "ShatteredSailStartupAudit", Product = "StartupMemory20261011";
        private static string output;
        private readonly Report report = new Report();
        private readonly List<string> errors = new List<string>();
        private bool finished;
        private bool offscreen;
        private bool capture;
        private Camera renderCamera;
        private RenderTexture renderTarget;

        [Serializable] private sealed class Report
        {
            public string status, error, company, product, persistentDataPath, unity, gpu, graphicsApi, startedUtc, inventoryUtc, completedUtc, fixture;
            public string method = "Non-Development Windows x64 Mono, normal production startup in an isolated identity; 1920x1080, production targetFrameRate/VSync settings (target 60 is not an actual FPS cap when VSync is active). No extra render targets, forced GC or probe asset unloading. Fixture prepared in a separate process before measurement. OS PrivateBytes/WorkingSet sampled externally every 250ms; stable interval 10-22 seconds after process launch. Native texture sizes may be unavailable in release builds and are not used as process totals. Probe inventories/JSON and optional visual capture occur after the measured interval.";
            public bool identityVerified, developmentBuild, offscreenRendering;
            public int width, height, frames, renderedCameraFrames, gameObjects, sprites, audioClips;
            public double firstMenuReadySeconds = -1;
            public long allocatedBytes, reservedBytes, managedUsedBytes, managedReservedBytes;
            public List<TextureEntry> textures = new List<TextureEntry>();
            public List<RenderTextureEntry> renderTextures = new List<RenderTextureEntry>();
        }
        [Serializable] private sealed class TextureEntry
        {
            public string name, format;
            public int width, height, mipmaps;
            public bool readable;
        }
        [Serializable] private sealed class RenderTextureEntry
        {
            public string name, colorFormat, depthFormat;
            public int width, height, samples;
            public bool created;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-startupMemoryOutput");
            if (index < 0) return;
            try
            {
                if (index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("Absolute output required.");
                output = Path.GetFullPath(args[index + 1]);
                if (!output.StartsWith("D:\\Game\\Artifacts\\StartupMemory20261011\\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use this audit output root.");
                string identity = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", Company, Product);
                if (Application.isEditor || Application.companyName != Company || Application.productName != Product || Debug.isDebugBuild ||
                    !string.Equals(Path.GetFullPath(Application.persistentDataPath), identity, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Isolated non-Development audit identity required before any persistence mutation.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                OverrideDirectory("VoyageSuspensionPersistence", "checkpoints");
                OverrideDirectory("ProfileSaveJournal", "journal");
                Application.runInBackground = true;
                if (Array.IndexOf(args, "-startupMemoryPrepare") >= 0)
                {
                    Prepare(Array.IndexOf(args, "-startupMemoryVoyage") >= 0);
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "fixture-prepared.txt"), "PASS\n" + Application.persistentDataPath);
                    Application.Quit(0);
                    return;
                }
                if (File.Exists(output)) throw new IOException("Preserve earlier evidence.");
                var host = new GameObject("Isolated Startup Memory Probe");
                DontDestroyOnLoad(host);
                var probe = host.AddComponent<StartupMemoryProbe>();
                probe.report.identityVerified = true;
                probe.offscreen = Array.IndexOf(args, "-startupMemoryOffscreen") >= 0;
                probe.capture = Array.IndexOf(args, "-startupMemoryCapture") >= 0;
                if (probe.capture && probe.offscreen) throw new ArgumentException("Visual capture requires the normal window camera/canvas.");
                probe.report.fixture = Array.IndexOf(args, "-startupMemoryVoyage") >= 0 ? "valid suspended voyage" : "empty profile";
                probe.report.startedUtc = DateTime.UtcNow.ToString("O");
                Application.logMessageReceived += probe.OnLog;
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += probe.OnCameraRendered;
            }
            catch (Exception ex) { Debug.LogException(ex); Application.Quit(2); }
        }

        private void Start()
        {
            if (offscreen)
            {
                report.offscreenRendering = true;
                report.method += " Hidden-window fallback: same production camera/HDR setting and menu UI rendered explicitly to one common 1920x1080 SDR output RenderTexture. This additional output and camera-canvas mode are diagnostic overhead, so totals describe this controlled rendering test rather than visible-window RAM.";
                renderCamera = Camera.main;
                renderCamera.enabled = false;
                renderCamera.orthographicSize = 540f;
                renderTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                renderTarget.name = "Common Startup Audit Output";
                renderTarget.Create();
                Canvas canvas = GameObject.Find("Main Menu Canvas").GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = renderCamera;
                canvas.planeDistance = 100f;
                renderCamera.targetTexture = renderTarget;
            }
            StartCoroutine(Run());
        }
        private void OnCameraRendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera) { report.renderedCameraFrames++; }

        private static void OverrideDirectory(string name, string leaf)
        {
            Type type = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation." + name, true);
            type.GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Path.Combine(Path.GetDirectoryName(output), leaf));
        }

        private static void Prepare(bool voyage)
        {
            PlayerPrefs.DeleteAll();
            if (voyage)
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "StartupMemoryFixture20261011");
                PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
                ProfileGoldPersistence.Save(2000m);
                var session = new CoreVoyageSession();
                session.RestoreProfile(2000m, "断锚港");
                session.RestoreGameClock(0d);
                session.ConfigureCrewCapabilities(true, false, false, false, false, false);
                session.Cargo.Water = session.Cargo.Food = 100;
                session.SetSeed("SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E");
                session.StartVoyage();
                var checkpoint = session.CaptureVoyageCheckpoint();
                if (!new CoreVoyageSession().TryRestoreVoyageCheckpoint(checkpoint, out string reason)) throw new InvalidDataException(reason);
                Assembly assembly = typeof(MainMenuController).Assembly;
                Type recordType = assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionRecord", true);
                object record = Activator.CreateInstance(recordType, true);
                Action<string, object> set = (key, value) => recordType.GetField(key).SetValue(record, value);
                set("format", "ShatteredSail.VoyageSuspension"); set("version", 1);
                set("profileScope", "StartupMemoryFixture20261011"); set("voyage", checkpoint);
                set("selectedOrdinaryCrew", -1); set("departureCrewShipIndex", session.SelectedShipIndex); set("departureCrewPort", "断锚港");
                set("selectedSpecialCrew", Array.Empty<string>()); set("standbyCrew", Array.Empty<string>());
                object[] saveArgs = { record, null };
                if (!(bool)assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true)
                    .GetMethod("TrySave", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, saveArgs)) throw new InvalidDataException((string)saveArgs[1]);
            }
            PlayerPrefs.Save();
        }

        private IEnumerator Run()
        {
            while (Time.realtimeSinceStartupAsDouble < 25d)
            {
                report.frames++;
                if (offscreen)
                {
                    Canvas.ForceUpdateCanvases();
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(renderCamera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = renderTarget });
                }
                if (report.firstMenuReadySeconds < 0)
                {
                    var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                    if (menu != null && menu.GetComponentsInChildren<Button>().Any(b => b.name == "Settings" && b.interactable))
                        report.firstMenuReadySeconds = Time.realtimeSinceStartupAsDouble;
                }
                if (offscreen) yield return new WaitForSecondsRealtime(1f / 60f);
                else yield return new WaitForEndOfFrame();
            }
            Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            report.inventoryUtc = DateTime.UtcNow.ToString("O");
            report.allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(); report.reservedBytes = Profiler.GetTotalReservedMemoryLong();
            report.managedUsedBytes = Profiler.GetMonoUsedSizeLong(); report.managedReservedBytes = Profiler.GetMonoHeapSizeLong();
            report.company = Application.companyName; report.product = Application.productName; report.persistentDataPath = Application.persistentDataPath;
            report.unity = Application.unityVersion; report.developmentBuild = Debug.isDebugBuild;
            report.gpu = SystemInfo.graphicsDeviceName; report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.width = Screen.width; report.height = Screen.height;
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                report.textures.Add(new TextureEntry { name = texture.name, format = texture.format.ToString(), width = texture.width, height = texture.height, mipmaps = texture.mipmapCount, readable = texture.isReadable });
            foreach (RenderTexture texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
                report.renderTextures.Add(new RenderTextureEntry { name = texture.name, colorFormat = texture.graphicsFormat.ToString(), depthFormat = texture.depthStencilFormat.ToString(), width = texture.width, height = texture.height, samples = texture.antiAliasing, created = texture.IsCreated() });
            report.gameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Length;
            report.sprites = Resources.FindObjectsOfTypeAll<Sprite>().Length;
            report.audioClips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            if (SceneManager.GetActiveScene().name != "MainMenu" || report.firstMenuReadySeconds < 0 || report.renderedCameraFrames < 100 || report.width != 1920 || report.height != 1080)
                errors.Add("Menu/normal rendering/1920x1080 readiness failed.");
            report.status = errors.Count == 0 ? "PASS" : "FAIL";
            report.error = string.Join("\n", errors);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= OnLog;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            if (renderTarget != null) { renderCamera.targetTexture = null; renderTarget.Release(); Destroy(renderTarget); }
            if (capture)
            {
                // Finish is called after WaitForEndOfFrame. Capture the actual screen only
                // after inventoryUtc and JSON, outside the OS memory comparison interval.
                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), "Player-MainMenu.png"), screenshot.EncodeToPNG()); }
                finally { Destroy(screenshot); }
            }
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }
        private void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack);
        }
    }
}
