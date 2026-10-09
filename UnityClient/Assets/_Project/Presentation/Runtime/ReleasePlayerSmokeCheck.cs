using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Read-only, opt-in packaged-player acceptance check. Never starts a new game,
    /// applies settings, edits PlayerPrefs or enables GM. Normal launches do not create it.
    /// </summary>
    public sealed class ReleasePlayerSmokeCheck : MonoBehaviour
    {
        private const string SmokeArgument = "-shatteredSailSmokeTest";
        private static string[] commandLineArguments = Environment.GetCommandLineArgs();

        // Available before scene Awake, unlike the AfterSceneLoad probe itself.
        // Even a malformed opt-in must protect saves before Bootstrap rejects it.
        internal static bool IsRequested => Array.IndexOf(commandLineArguments, SmokeArgument) >= 0;

        private string destination;
        private readonly List<string> failures = new List<string>();
        private float started;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor || !IsRequested) return;
            string[] args = commandLineArguments;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != SmokeArgument) continue;
                if (i + 1 >= args.Length || !Path.IsPathRooted(args[i + 1]))
                {
                    Debug.LogError("Smoke report path must be absolute.");
                    Application.Quit(2);
                    return;
                }
                ReleasePlayerSmokeCheck check = new GameObject("Opt-in Player Acceptance").AddComponent<ReleasePlayerSmokeCheck>();
                check.destination = args[i + 1];
                check.started = Time.realtimeSinceStartup;
                // The launcher deliberately hides the acceptance window. Normal
                // play keeps its ordinary focus behaviour; this opt-in run must
                // continue pumping/rendering frames without keyboard focus.
                Application.runInBackground = true;
                Application.logMessageReceived += check.RecordLog;
                check.StartCoroutine(check.CheckPlayer());
                return;
            }
        }

        private IEnumerator CheckPlayer()
        {
            yield return null;
            yield return null;
            try
            {
                Require(SceneManager.GetActiveScene().name == "MainMenu", "Player did not start in MainMenu.");
                Text gameTitle = FindObjectsByType<Text>(FindObjectsSortMode.None).FirstOrDefault(label => label.name == "Game Title");
                Require(GameClientInfo.DisplayName == "第七港口" && gameTitle != null && gameTitle.text == GameClientInfo.DisplayName,
                    "Packaged menu title must be 第七港口.");
                Require(FindFirstObjectByType<MainMenuController>() != null, "Main menu controller missing.");
                Require(EventSystem.current != null, "Input EventSystem missing.");
                Require(Resources.Load<Texture2D>("MainMenu/main_menu_background") != null, "Packaged background missing.");
                Require(SceneManager.sceneCountInBuildSettings == 2, "Player must contain only MainMenu and CoreGameplayDemo.");
                Require(Application.CanStreamedLevelBeLoaded("MainMenu"), "Main menu scene missing from player.");
                Require(Application.CanStreamedLevelBeLoaded("CoreGameplayDemo"), "Core scene missing from player.");
                Require(!Application.CanStreamedLevelBeLoaded("TutorialVoyage"), "Legacy developer tutorial scene must not be included.");
                Require(!Application.CanStreamedLevelBeLoaded("SampleScene"), "Sample scene must not be included.");
                Require(FindObjectsByType<Text>(FindObjectsSortMode.None).All(label => label.font != null), "UI font missing.");
                Require(FindButton("New Game") != null && FindButton("Exit Game") != null, "Main menu actions missing.");
                Button settings = FindButton("Settings");
                Require(settings != null && settings.interactable, "Settings action missing.");
                settings.onClick.Invoke();
                Button back = FindButton("Back From Settings");
                Require(back != null && back.gameObject.activeInHierarchy, "Settings did not open.");
                back.onClick.Invoke();
                Require(!back.gameObject.activeInHierarchy, "Settings did not close.");
                Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Graphics not initialized.");
                Canvas.ForceUpdateCanvases();
            }
            catch (Exception error) { failures.Add(error.ToString()); }
            // A hidden Windows swap chain can have no readable backbuffer. Render
            // the actual packaged UI through its active pipeline into a target.
            yield return null;
            try
            {
                Directory.CreateDirectory(destination);
                CapturePackagedMenu();
                CapturePackagedBattleShips();
                CheckPackagedAudio();
            }
            catch (Exception error) { failures.Add(error.ToString()); }
            Finish();
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 45f)
            {
                failures.Add("Packaged player acceptance timed out.");
                Finish();
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= RecordLog;
            string report = (failures.Count == 0 ? "PASS" : "FAIL") + "\nProduct: " + Application.productName +
                "\nCompany: " + Application.companyName + "\nVersion: " + Application.version +
                "\nUnity: " + Application.unityVersion + "\nGraphics: " + SystemInfo.graphicsDeviceName +
                "\nResolution: " + Screen.width + "x" + Screen.height +
                "\nChecks: startup, packaged scenes, background, fonts, input, settings open/close, packaged UI offscreen frame, " +
                ShatteredSail.Core.Gameplay.CoreVoyageSession.Ships.Count + " turned/mirrored ship cutouts and visible foreground, 63 packaged 48kHz audio clips." +
                "\nSave changes: none. Explicit smoke mode bypasses main-menu profile recovery, voyage-file reads and achievement capture; new-game, continue and settings-apply callbacks are blocked. No GM action invoked.\n" + string.Join("\n", failures);
            try
            {
                Directory.CreateDirectory(destination);
                File.WriteAllText(Path.Combine(destination, "player-smoke-report.txt"), report);
            }
            catch (Exception error) { Debug.LogError(error); failures.Add(error.Message); }
            Debug.Log("PLAYER_SMOKE_RESULT\n" + report);
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }

        private void RecordLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failures.Add(message + "\n" + stack);
        }

        private static Button FindButton(string name) =>
            FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(button => button.name == name);

        private void CheckPackagedAudio()
        {
            string[] groups = { "Music", "BattleSfx", "WorldSfx", "InterfaceSfx", "Ambience" };
            int[] counts = { 21, 14, 11, 11, 6 };
            var lines = new List<string> { "group,name,frequency,channels,samples,seconds" };
            for (int index = 0; index < groups.Length; index++)
            {
                AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + groups[index]);
                Require(clips.Length == counts[index], "Packaged audio count mismatch: " + groups[index]);
                foreach (AudioClip clip in clips)
                {
                    Require(clip.frequency == 48000 && clip.samples > 0, "Invalid packaged audio: " + clip.name);
                    if (groups[index] == "Music" || groups[index] == "Ambience")
                        Require(clip.channels == 2, "Packaged stereo master collapsed: " + clip.name);
                    lines.Add(string.Join(",", groups[index], clip.name, clip.frequency, clip.channels,
                        clip.samples, clip.length.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            File.WriteAllLines(Path.Combine(destination, "packaged-audio.csv"), lines);
        }

        private void CapturePackagedMenu()
        {
            Canvas canvas = GameObject.Find("Main Menu Canvas").GetComponent<Canvas>();
            RectTransform rect = canvas.GetComponent<RectTransform>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            bool scaling = scaler.enabled;
            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            Vector2 size = rect.sizeDelta;
            Vector3 position = rect.localPosition, scale = rect.localScale;
            Quaternion rotation = rect.localRotation;
            RenderTexture active = RenderTexture.active;
            Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
            int[] layers = children.Select(child => child.gameObject.layer).ToArray();
            var cameraHost = new GameObject("Packaged UI Capture", typeof(Camera));
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = null;
            try
            {
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 540f;
                camera.aspect = 1920f / 1080f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << 30;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target.Create();
                camera.targetTexture = target;
                scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                foreach (Transform child in children) child.gameObject.layer = 30;
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target;
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int darkest = 255, brightest = 0;
                for (int index = 0; index < samples.Length; index += 997)
                {
                    int light = (samples[index].r + samples[index].g + samples[index].b) / 3;
                    darkest = Math.Min(darkest, light);
                    brightest = Math.Max(brightest, light);
                }
                Require(brightest - darkest >= 20, "Packaged UI render is blank.");
                File.WriteAllBytes(Path.Combine(destination, "Player-MainMenu.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                for (int index = 0; index < children.Length; index++) children[index].gameObject.layer = layers[index];
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                rect.sizeDelta = size;
                rect.localPosition = position;
                rect.localRotation = rotation;
                rect.localScale = scale;
                scaler.enabled = scaling;
                Canvas.ForceUpdateCanvases();
                cameraHost.GetComponent<Camera>().targetTexture = null;
                target.Release();
                Destroy(target);
                Destroy(cameraHost);
                if (pixels != null) Destroy(pixels);
            }
        }

        private void CapturePackagedBattleShips()
        {
            // Exercise the shader compiled into the actual player, not an Editor
            // substitute. This synthetic canvas never creates a voyage or profile.
            Shader shader = Resources.Load<Shader>(BattleShipCutout.ShaderResourcePath);
            Require(shader != null && shader.isSupported, "Packaged ship cutout shader missing or unsupported.");
            var catalog = new PortArtCatalog();
            Require(catalog.ShipSheet != null, "Packaged ship roster missing.");
            const int width = 800, height = 720, layer = 30;
            var cameraHost = new GameObject("Packaged Battle Capture", typeof(Camera));
            var canvasHost = new GameObject("Packaged Ship Shader Check", typeof(RectTransform), typeof(Canvas));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.17f, 0.47f, 0.29f, 1f);
                camera.cullingMask = 1 << layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target.Create();
                camera.targetTexture = target;
                canvasHost.layer = layer;
                Canvas canvas = canvasHost.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(width, height);
                canvasRect.position = Vector3.zero;
                canvasRect.localScale = Vector3.one;
                var ships = new RectTransform[ShatteredSail.Core.Gameplay.CoreVoyageSession.Ships.Count];
                float[] yaws = { 0f, 55f, 125f, 180f };
                for (int ship = 0; ship < ships.Length; ship++)
                {
                    var host = new GameObject("Packaged Ship " + ship, typeof(RectTransform), typeof(RawImage));
                    host.layer = layer;
                    RectTransform rect = host.GetComponent<RectTransform>();
                    rect.SetParent(canvasRect, false);
                    rect.sizeDelta = new Vector2(180f, 180f);
                    rect.anchoredPosition = new Vector2(-300f + ship % 4 * 200f, 240f - ship / 4 * 240f);
                    rect.localRotation = Quaternion.Euler(0f, yaws[ship % 4], 0f);
                    RawImage image = host.GetComponent<RawImage>();
                    image.texture = catalog.ShipTexture(ship);
                    Require(image.texture != null, "Packaged regional ship illustration missing: " + ship);
                    Rect uv = catalog.ShipUv(ship);
                    image.uvRect = ship % 2 == 0 ? uv : new Rect(uv.xMax, uv.y, -uv.width, uv.height);
                    image.raycastTarget = false;
                    BattleShipCutout.Attach(image);
                    ships[ship] = rect;
                }
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(destination, "Player-BattleShipCutouts.png"), pixels.EncodeToPNG());
                Color background = pixels.GetPixel(8, 8);
                foreach (RectTransform ship in ships)
                {
                    foreach (float along in new[] { 0.25f, 0.5f, 0.75f })
                        foreach (Vector2 edge in new[] { new Vector2(0.002f, along), new Vector2(0.998f, along), new Vector2(along, 0.002f), new Vector2(along, 0.998f) })
                            Require(SmokeColorDistance(SampleShip(pixels, camera, ship, edge), background) < 0.035f,
                                ship.name + ": opaque atlas border after turn at " + edge);
                    int visible = 0;
                    foreach (float x in new[] { 0.35f, 0.5f, 0.65f })
                        foreach (float y in new[] { 0.35f, 0.5f, 0.65f })
                            if (SmokeColorDistance(SampleShip(pixels, camera, ship, new Vector2(x, y)), background) > 0.1f) visible++;
                    Require(visible >= 3, ship.name + ": ship body missing from the packaged render.");
                }
            }
            finally
            {
                RenderTexture.active = previous;
                cameraHost.GetComponent<Camera>().targetTexture = null;
                target.Release();
                Destroy(target);
                Destroy(canvasHost);
                Destroy(cameraHost);
                if (pixels != null) Destroy(pixels);
            }
        }

        private static Color SampleShip(Texture2D pixels, Camera camera, RectTransform ship, Vector2 uv)
        {
            Vector3 local = new Vector3((uv.x - 0.5f) * ship.rect.width, (uv.y - 0.5f) * ship.rect.height, 0f);
            Vector3 screen = camera.WorldToScreenPoint(ship.TransformPoint(local));
            return pixels.GetPixel(Mathf.FloorToInt(screen.x), Mathf.FloorToInt(screen.y));
        }

        private static float SmokeColorDistance(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
