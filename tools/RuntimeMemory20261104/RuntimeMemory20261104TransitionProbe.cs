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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Diagnostics
{
    // V2 diagnostic source: compile this file ALONE to the existing probe assembly; never install beside the V1 source.
    public sealed class RuntimeMemory20261104Probe : MonoBehaviour
    {
        public const string Company = "ShatteredSailAudit";
        public const string Product = "RuntimeMemory20261104";
        private const string OutputRoot = "D:/Game/Artifacts/RuntimeMemory20261104/";
        private readonly Report report = new Report();
        private readonly HashSet<string> targetNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "field_fire_v2", "field_arcane_v2", "field_curse_v2", "field_tide_v2", "field_thunder_v2", "field_blood_v2",
            "naval_fx_atlas_v1", "fire_eruption", "tide_breaker", "battle_icons_v1",
            "wood_common", "wood_uncommon", "wood_rare", "wood_epic", "wood_legendary"
        };
        private readonly HashSet<string> ramNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ram_player_head_on_page01", "ram_player_head_on_page02", "ram_player_head_on_page03", "ram_player_head_on_page04"
        };
        private string output;
        private bool finished, reserved;
        private int frames;
        private double started;
        private Camera auditCamera;
        private RenderTexture target;
        private RenderPipeline.StandardRequest request;
        private GameObject fixture;
        private Texture2D[] ramFixturePages;

        [Serializable] private sealed class Report
        {
            public string probeVersion = "transition-v2";
            public string method = "Isolated non-Development Windows x64 Mono Player, actual production resource getters and graphic Configure methods, one common 1920x1080 SDR offscreen target. Fixture is synthetic, not full gameplay. Cache collection is ONLY the production RuntimeArtMemory.CollectAfterReturningToMenu method; probe never calls GC.Collect, UnloadUnusedAssets, or UnloadAsset. External 250ms OS sampling supplies PrivateBytes/WorkingSet; Unity texture bytes are partial in release and are not total GPU memory. No player preferences are written.";
            public string startedUtc, completedUtc, unity, company, product, persistentDataPath, graphicsApi, gpu, error;
            public bool identityVerified, developmentBuild, success;
            public int renderedFrames, expectedTargetTextures = 15, expectedRamTextures = 4;
            public List<Point> points = new List<Point>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Point
        {
            public string name, windowStartUtc, windowEndUtc;
            public int framesInWindow, targetTextureCount, targetSpriteCount, ramTextureCount;
            public long totalAllocatedBytes, totalReservedBytes, unusedReservedBytes, managedHeapBytes, targetTextureBytes;
            public bool runtimeTextureByteApiAvailable;
            public List<TextureInfo> textures = new List<TextureInfo>();
            public List<TextureInfo> ramTextures = new List<TextureInfo>();
        }
        [Serializable] private sealed class TextureInfo
        {
            public string name, format;
            public int id, width, height, mipCount;
            public long bytes;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-runtimeMemory20261104Output");
            if (at < 0) return;
            var host = new GameObject("Runtime memory 20261104 diagnostic");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<RuntimeMemory20261104Probe>();
            try
            {
                if (at + 1 >= args.Length || Array.LastIndexOf(args, "-runtimeMemory20261104Output") != at)
                    throw new ArgumentException("Supply one output argument and a fresh absolute .json path.");
                probe.Initialize(args[at + 1]);
            }
            catch (Exception error) { probe.Fail(error); }
        }

        private void Initialize(string path)
        {
            started = Time.realtimeSinceStartupAsDouble;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            report.unity = Application.unityVersion; report.company = Application.companyName; report.product = Application.productName;
            report.persistentDataPath = Application.persistentDataPath; report.developmentBuild = Debug.isDebugBuild;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); report.gpu = SystemInfo.graphicsDeviceName;
            string identity = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", Company, Product));
            if (Application.isEditor || Debug.isDebugBuild || Application.companyName != Company || Application.productName != Product ||
                !string.Equals(Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar), identity, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the isolated non-Development diagnostic Player is allowed.");
            report.identityVerified = true;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("A graphical Player is required.");
            output = Path.GetFullPath(path);
            if (!Path.IsPathRooted(path) || Path.GetExtension(output) != ".json" || !output.StartsWith(Path.GetFullPath(OutputRoot), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use a new .json under this task's Artifacts directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            reserved = true;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Application.logMessageReceived += OnLog;
            CreateRenderer();
            StartCoroutine(Guarded(Run()));
        }

        private IEnumerator Run()
        {
            yield return null; yield return null;
            if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return new WaitForSecondsRealtime(2f);
            RefreshCanvases();
            yield return ProductionMenuCollection();
            yield return Measure("menu_initial");
            CreateFixture();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Measure("art_open_first");
            RequireAllTargets("art_open_first");
            CaptureFrame("art_open_first");
            yield return ProductionMenuCollection();
            yield return Measure("art_live_after_collection");
            RequireAllTargets("art_live_after_collection");
            CloseFixture();
            yield return null; yield return null;
            yield return ProductionMenuCollection();
            yield return Measure("art_closed_first");
            CreateFixture();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Measure("art_reopened");
            RequireAllTargets("art_reopened");
            CaptureFrame("art_reopened");
            CloseFixture();
            yield return null; yield return null;
            yield return ProductionMenuCollection();
            yield return Measure("art_closed_second");
            // Fixed follow-on workload: one real production ram sequence, not an arbitrary allocation sized to force a result.
            CreateRamFixture();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Measure("ram_open");
            if (report.points.Last().ramTextureCount != report.expectedRamTextures)
                throw new InvalidOperationException("The complete four-page production ram sequence did not load.");
            CaptureFrame("ram_open");
            CloseFixture();
            yield return null; yield return null;
            yield return ProductionMenuCollection();
            yield return Measure("ram_closed");
            if (report.points.Last().ramTextureCount != 0)
                throw new InvalidOperationException("Closed follow-on ram pages remained referenced by the diagnostic fixture.");
            if (frames < 100) throw new InvalidOperationException("Insufficient actual offscreen frames.");
            if (report.errors.Count != 0) throw new InvalidOperationException("Player emitted errors; inspect report.");
            Complete(true, null);
        }

        private static IEnumerator ProductionMenuCollection()
        {
            Type type = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.RuntimeArtMemory", true);
            MethodInfo method = type.GetMethod("CollectAfterReturningToMenu", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(type.FullName, "CollectAfterReturningToMenu");
            return (IEnumerator)method.Invoke(null, null);
        }

        private void CreateFixture()
        {
            fixture = new GameObject("Diagnostic resource display", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = fixture.GetComponent<Canvas>();
            canvas.sortingOrder = 3000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            for (int i = 0; i < 6; i++)
                AddRaw("Field " + i, NavalFieldArtCatalog.ForField((CoreNavalFieldKind)i), new Vector2(-700 + i * 260, 310));
            AddRaw("Combat symbols", BattleIconArtCatalog.Texture, new Vector2(700, -330));
            for (int i = 0; i < 5; i++)
            {
                GameObject go = NewGraphic("Rarity " + i, new Vector2(-650 + i * 290, -330), new Vector2(240, 120));
                var image = go.AddComponent<Image>(); image.sprite = ComponentRarityWoodSkin.FaceFor((ComponentRarity)i);
                if (image.sprite == null) throw new InvalidOperationException("Missing rarity art " + i);
            }
            var naval = NewGraphic("Naval atlas effect", Vector2.zero, new Vector2(1920, 1080)).AddComponent<NavalCombatFxGraphic>();
            naval.Configure(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, true, BattleSchoolAttackOutcome.Hit,
                new Vector2(-500, 0), new Vector2(500, 0)); naval.SetProgress(.48f);
            for (int i = 0; i < 2; i++)
            {
                var school = NewGraphic("Boarding school " + i, Vector2.zero, new Vector2(1920, 1080)).AddComponent<BattleSchoolAttackGraphic>();
                school.Configure(i == 0 ? BattleSchoolAttackKind.Fire : BattleSchoolAttackKind.Tide,
                    BattleSchoolAttackOutcome.Hit, new Vector2(-500, i * 140 - 70), new Vector2(500, i * 140 - 70), true);
                school.SetProgress(.48f);
            }
            RefreshCanvases();
        }
        private void CreateRamFixture()
        {
            fixture = new GameObject("Diagnostic production ram sequence", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            fixture.GetComponent<Canvas>().sortingOrder = 3000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            // Production RamComicTexture loads the complete group before presentation. Keep the same four exact resources live.
            ramFixturePages = new Texture2D[RamComicCatalog.PagesPerSequence];
            for (int page = 0; page < ramFixturePages.Length; page++)
            {
                ramFixturePages[page] = Resources.Load<Texture2D>(RamComicCatalog.ImageResource(0, page));
                if (ramFixturePages[page] == null) throw new InvalidOperationException("Missing production ram page " + page);
            }
            RawImage image = NewGraphic("Production ram first page", Vector2.zero, new Vector2(1920, 1080)).AddComponent<RawImage>();
            image.texture = ramFixturePages[0]; image.raycastTarget = false;
            image.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            image.GetComponent<AspectRatioFitter>().aspectRatio = (float)ramFixturePages[0].width / ramFixturePages[0].height;
            RefreshCanvases();
        }
        private GameObject NewGraphic(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rect = (RectTransform)go.transform; rect.SetParent(fixture.transform, false);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return go;
        }
        private void AddRaw(string name, Texture texture, Vector2 position)
        {
            if (texture == null) throw new InvalidOperationException("Missing " + name);
            var image = NewGraphic(name, position, new Vector2(220, 180)).AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
        }
        private void CloseFixture()
        {
            ramFixturePages = null;
            if (fixture != null) { fixture.SetActive(false); Destroy(fixture); fixture = null; }
        }

        private IEnumerator Measure(string name)
        {
            RefreshCanvases();
            yield return new WaitForSecondsRealtime(.5f);
            var point = new Point { name = name, windowStartUtc = DateTime.UtcNow.ToString("O") };
            int startFrames = frames;
            yield return new WaitForSecondsRealtime(2f);
            point.windowEndUtc = DateTime.UtcNow.ToString("O"); point.framesInWindow = frames - startFrames;
            if (point.framesInWindow < 20) throw new InvalidOperationException("Not enough actual rendered frames at " + name);
            point.totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
            point.totalReservedBytes = Profiler.GetTotalReservedMemoryLong();
            point.unusedReservedBytes = Profiler.GetTotalUnusedReservedMemoryLong();
            point.managedHeapBytes = Profiler.GetMonoHeapSizeLong();
            // Inventories are collected after the OS measurement window; no Texture reference is retained in the report.
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => targetNames.Contains(t.name)).OrderBy(t => t.name))
            {
                long bytes = Profiler.GetRuntimeMemorySizeLong(texture);
                point.runtimeTextureByteApiAvailable |= bytes > 0;
                point.targetTextureBytes += Math.Max(0, bytes);
                point.textures.Add(new TextureInfo { name = texture.name, id = texture.GetInstanceID(), width = texture.width,
                    height = texture.height, mipCount = texture.mipmapCount, format = texture.format.ToString(), bytes = bytes });
            }
            point.targetTextureCount = point.textures.Count;
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => ramNames.Contains(t.name)).OrderBy(t => t.name))
                point.ramTextures.Add(new TextureInfo { name = texture.name, id = texture.GetInstanceID(), width = texture.width,
                    height = texture.height, mipCount = texture.mipmapCount, format = texture.format.ToString(), bytes = Profiler.GetRuntimeMemorySizeLong(texture) });
            point.ramTextureCount = point.ramTextures.Count;
            point.targetSpriteCount = Resources.FindObjectsOfTypeAll<Sprite>().Count(s => s.name.StartsWith("Component Rarity Wood ", StringComparison.Ordinal));
            report.points.Add(point);
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        private void RequireAllTargets(string name)
        {
            Point point = report.points.Last();
            if (point.name != name || point.targetTextureCount != report.expectedTargetTextures || point.targetSpriteCount != 5)
                throw new InvalidOperationException("Display resource count differs at " + name + ": " + point.targetTextureCount + "/15 textures, " + point.targetSpriteCount + "/5 sprites.");
        }

        private void CaptureFrame(string name)
        {
            // Readback occurs after this stage's measurement window; both A/B builds use the identical capture sequence.
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "-" + name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; if (pixels != null) Destroy(pixels); }
        }

        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0 && !finished)
            {
                object next;
                try
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; }
                    next = current.Current;
                    if (next is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception error) { Fail(error); yield break; }
                yield return next;
            }
        }
        private void CreateRenderer()
        {
            var go = new GameObject("Memory probe controlled renderer", typeof(Camera)); go.transform.SetParent(transform, false);
            auditCamera = go.GetComponent<Camera>(); auditCamera.enabled = false; auditCamera.orthographic = true;
            auditCamera.orthographicSize = 540; auditCamera.aspect = 1920f / 1080f;
            auditCamera.nearClipPlane = .1f; auditCamera.farClipPlane = 2000f; auditCamera.transform.position = new Vector3(0, 0, -1000);
            auditCamera.clearFlags = CameraClearFlags.SolidColor; auditCamera.backgroundColor = Color.black;
            auditCamera.allowHDR = false; auditCamera.allowMSAA = false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.name = "Common runtime memory 20261104 SDR output";
            if (!target.Create()) throw new InvalidOperationException("Cannot create controlled render target.");
            auditCamera.targetTexture = target; request = new RenderPipeline.StandardRequest { destination = target };
            RenderPipelineManager.endCameraRendering += OnRendered; Camera.onPostRender += OnBuiltinRendered;
        }
        private void RefreshCanvases()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera != auditCamera) camera.enabled = false;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = auditCamera; canvas.planeDistance = 100;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.isActiveAndEnabled) { scaler.enabled = false; scaler.enabled = true; }
            }
            Canvas.ForceUpdateCanvases();
        }
        private void LateUpdate()
        {
            if (finished || auditCamera == null) return;
            if (Time.realtimeSinceStartupAsDouble - started > 65) { Fail(new TimeoutException("Probe exceeded 65 seconds.")); return; }
            try
            {
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(auditCamera, request);
                else auditCamera.Render();
            }
            catch (Exception error) { Fail(error); }
        }
        private void OnRendered(ScriptableRenderContext context, Camera camera) { if (camera == auditCamera) frames++; }
        private void OnBuiltinRendered(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null && camera == auditCamera) frames++; }
        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace);
        }
        private void Fail(Exception error) => Complete(false, error.ToString());
        private void Complete(bool success, string error)
        {
            if (finished) return;
            finished = true; report.success = success; report.error = error;
            report.completedUtc = DateTime.UtcNow.ToString("O"); report.renderedFrames = frames;
            if (reserved) File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("RUNTIME_MEMORY_20261104 " + (success ? "PASS" : "FAIL " + error));
            Application.Quit(success ? 0 : 2);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            RenderPipelineManager.endCameraRendering -= OnRendered; Camera.onPostRender -= OnBuiltinRendered;
            CloseFixture();
            if (auditCamera != null) auditCamera.targetTexture = null;
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
