// Install this read-only audit only in the isolated validation project's Editor folder.
using System;
using System.Collections;
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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class NavigationSavedPreviewAudit
    {
        private const int Width = 1920, Height = 1080, Layer = 29;
        private static readonly string[] Directions = { "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest" };
        private static readonly string[] Captions = { "北", "东北", "东", "东南", "南", "西南", "西", "西北" };

        // -executeMethod ShatteredSail.Editor.NavigationSavedPreviewAudit.RunFromCommandLine
        // -navigationSavedReport <migration scene-sync.json> -navigationSavedOutput <empty output directory>
        public static void RunFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required; omit -nographics.");
            string input = Argument("-navigationSavedReport"), output = Argument("-navigationSavedOutput");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use an empty audit output directory.");
            Directory.CreateDirectory(output);
            SyncReport source = JsonUtility.FromJson<SyncReport>(File.ReadAllText(input));
            if (source == null || !source.complete || source.scenes == null || source.scenes.Length != source.matchedScenes || source.scenes.Length == 0)
                throw new InvalidDataException("Migration report is incomplete.");
            var report = new AuditReport { sourceReport = input };
            foreach (string path in source.spriteAssets)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(sprite != null && sprite.texture != null && AssetDatabase.Contains(sprite), "Persistent sprite did not reload: " + path);
            }
            foreach (string resource in new[] { NavigationHeaderSkin.HeaderResourcePath, NavigationHeaderSkin.CompassResourcePath,
                NavigationInterfaceSkin.PanelFrameResourcePath })
                Require(Resources.Load<Texture2D>(resource) != null, "Resource did not reload: " + resource);
            foreach (SavedScene saved in source.scenes)
            {
                var result = new SceneResult { path = saved.path };
                report.scenes.Add(result);
                byte[] before = File.ReadAllBytes(Absolute(saved.path));
                Scene scene = default;
                try
                {
                    Require(Hash(before) == saved.afterSha256, "Scene differs from migration report: " + saved.path);
                    string yaml = Encoding.UTF8.GetString(before);
                    foreach (string asset in source.spriteAssets)
                        Require(yaml.Contains("guid: " + AssetDatabase.AssetPathToGUID(asset)), "Saved scene lost sprite reference: " + asset);
                    scene = EditorSceneManager.OpenPreviewScene(saved.path);
                    Transform[] all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                    Require(all.All(item => item.GetComponents<Component>().All(component => component != null)), "Missing script after reopening.");
                    CoreGameplayDemoController[] controllers = all.Select(item => item.GetComponent<CoreGameplayDemoController>()).Where(item => item != null).ToArray();
                    Require(controllers.Length == 1 && controllers[0].EditorPreviewPersistsGeneratedUi, "Scene must use its saved authored UI.");
                    RectTransform content = all.OfType<RectTransform>().Single(item => item.name == "Phase Content");
                    RectTransform header = Child(content, "Voyage Header"), controls = Child(content, "Navigation Controls");
                    Require(header != null && controls != null, "Saved dashboard roots are missing.");
                    ValidateImage(Child(header, "Voyage Header Frame"), NavigationHeaderSkin.HeaderResourcePath);
                    ValidateImage(Child(header, "Voyage Header Compass"), NavigationHeaderSkin.CompassResourcePath);
                    RawImage compass = Child(controls, "Navigation Compass Artwork").GetComponent<RawImage>();
                    Require(compass.texture == Resources.Load<Texture2D>(NavigationCompassControl.ResourcePath), "Saved compass resource is missing.");
                    Require(controls.GetComponentsInChildren<NavigationCompassSectorGraphic>(true).Length == 8, "Expected eight saved compass sectors.");
                    for (int index = 0; index < Directions.Length; index++)
                    {
                        RectTransform buttonRoot = Child(controls, "Heading " + Directions[index]);
                        Button button = buttonRoot.GetComponent<Button>();
                        NavigationCompassSectorGraphic sector = Child(buttonRoot, "Navigation Compass Sector").GetComponent<NavigationCompassSectorGraphic>();
                        Text label = Child(buttonRoot, "Label").GetComponent<Text>();
                        float expectedAngle = Mathf.Repeat(90f - index * 45f, 360f);
                        Require(button.targetGraphic == sector && sector.raycastTarget, "Heading lost its sector input target: " + Directions[index]);
                        Require(label.text == Captions[index] && !label.raycastTarget, "Direction caption changed: " + Directions[index]);
                        Require(Mathf.Abs(Mathf.DeltaAngle(sector.DirectionDegrees, expectedAngle)) < .01f, "Sector direction is incorrect.");
                        Vector2 position = label.rectTransform.anchoredPosition;
                        float actualAngle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
                        Require(Mathf.Abs(Mathf.DeltaAngle(actualAngle, expectedAngle)) < .01f, "Direction labels overlap or occupy the wrong wedge.");
                        Require(Mathf.Abs(position.magnitude / buttonRoot.rect.width - .335f) < .002f, "Caption radius is incorrect.");
                    }
                    RectTransform map = Child(Child(Child(content, "Navigation Chart"), "Procedural Sea Chart Frame"), "Map Layer");
                    Require(map != null, "Saved map is missing.");
                    // The remaining one-character event glyph is an icon/hit target,
                    // not the removed name caption. Keep it visible and interactive.
                    result.visibleMapLabels = map.GetComponentsInChildren<Text>(true).Count(label => label.enabled && label.gameObject.activeInHierarchy &&
                        label.color.a > .01f && !string.IsNullOrWhiteSpace(label.text) && !IsEventGlyph(label));
                    Require(result.visibleMapLabels == 0, "Removed map/event/boss labels became visible again.");
                    VoyageMapTileLayerGraphic fog = map.GetComponentsInChildren<VoyageMapTileLayerGraphic>(true).Single(item => item.name == "Chart Fog Tiles");
                    Require(fog.TileCount == 96 * 64, "Saved fine fog data is incomplete.");
                    IEnumerable tiles = (IEnumerable)typeof(VoyageMapTileLayerGraphic).GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fog);
                    foreach (object tile in tiles)
                    {
                        Color32 color = (Color32)tile.GetType().GetField("Color").GetValue(tile);
                        if (color.a < 128) continue;
                        Require(color.a == 255, "Unexplored fog is translucent after reload.");
                        result.opaqueFogCells++;
                    }
                    string captureName = saved.path.EndsWith("/UI_Navigation.unity", StringComparison.Ordinal) ? "SavedNavigation_1920x1080.png" :
                        saved.path.EndsWith("/UI_Encounter.unity", StringComparison.Ordinal) ? "SavedEncounter_1920x1080.png" :
                        saved.path.EndsWith("/TutorialVoyage/02_FirstSail.unity", StringComparison.Ordinal) ? "SavedTutorial_1920x1080.png" : null;
                    if (captureName != null)
                    {
                        Capture(scene, content.GetComponentInParent<Canvas>(), Path.Combine(output, captureName));
                        result.screenshot = captureName;
                        report.captures++;
                    }
                    result.passed = true;
                }
                catch (Exception failure) { result.error = failure.ToString(); }
                finally
                {
                    if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                    if (!File.ReadAllBytes(Absolute(saved.path)).SequenceEqual(before))
                    {
                        result.passed = false;
                        result.error += "\nRead-only audit unexpectedly modified the authored scene.";
                    }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                }
            }
            report.complete = report.scenes.All(item => item.passed) && report.captures == 3;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (!report.complete) throw new InvalidOperationException("Saved navigation audit failed; inspect report.json.");
            Debug.Log("NAVIGATION_SAVED_PREVIEW_AUDIT_OK: " + report.scenes.Count + " reopened scenes, three saved-scene captures; " + output);

#endif
        }

        private static void ValidateImage(RectTransform rect, string resource)
        {
            Image image = rect == null ? null : rect.GetComponent<Image>();
            Require(image != null && image.sprite != null && image.sprite.texture == Resources.Load<Texture2D>(resource),
                "Saved image is missing or uses stale art: " + resource);
        }

        private static void Capture(Scene scene, Canvas canvas, string file)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Require(scaler != null && scaler.enabled, "Saved CanvasScaler must remain enabled.");
            GameObject cameraHost = new GameObject("Disposable Saved Navigation Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000f;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.cullingMask = 1 << Layer;
            camera.allowHDR = camera.allowMSAA = false;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                Require(Mathf.Abs(canvas.renderingDisplaySize.x - Width) < .5f && Mathf.Abs(canvas.renderingDisplaySize.y - Height) < .5f &&
                    Mathf.Abs(canvas.pixelRect.width - Width) < .5f && Mathf.Abs(canvas.pixelRect.height - Height) < .5f,
                    "Render target did not drive saved canvas geometry.");
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int low = 255, high = 0;
                for (int index = 0; index < samples.Length; index += 997)
                { int value = (samples[index].r + samples[index].g + samples[index].b) / 3; low = Math.Min(low, value); high = Math.Max(high, value); }
                Require(high - low >= 20, "Saved-scene rendering is nearly blank.");
                File.WriteAllBytes(file, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (pixels != null) Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraHost);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private static bool IsEventGlyph(Text label) => label.name == "Label" && label.transform.parent != null &&
            label.transform.parent.name.StartsWith("Event Node ", StringComparison.Ordinal) && label.text.Length == 1;
        private static RectTransform Child(Transform parent, string name) => parent == null ? null : parent.Find(name) as RectTransform;
        private static string Absolute(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        private static string Argument(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException(flag + " requires an absolute path.");
            return Path.GetFullPath(args[index + 1]);
        }
        private static string Hash(byte[] bytes)
        { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        [Serializable] private sealed class SyncReport { public bool complete; public int matchedScenes; public string[] spriteAssets; public SavedScene[] scenes; }
        [Serializable] private sealed class SavedScene { public string path, afterSha256; }
        [Serializable] private sealed class AuditReport
        {
            public string sourceReport;
            public string scope = "Reopens migrated authored scenes without constructing a controller or invoking gameplay callbacks. Verifies persistent visuals, eight compass sectors and captions, absent visible map labels, opaque unexplored fog and byte-identical scene files. Three screenshots render the saved hierarchy through its original CanvasScaler.";
            public bool complete;
            public int captures;
            public List<SceneResult> scenes = new List<SceneResult>();
        }
        [Serializable] private sealed class SceneResult
        { public string path, screenshot, error; public bool passed; public int visibleMapLabels, opaqueFogCells; }
    }
}
