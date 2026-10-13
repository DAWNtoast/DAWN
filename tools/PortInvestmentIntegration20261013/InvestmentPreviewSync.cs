using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    // Installed only in the registered, isolated validation project by Run.ps1.
    public static class InvestmentPreviewSync
    {
        private const string Project = "D:/Game/tmp/port-investment-layout-20261011";
        private const string Source = "D:/Game/UnityClient";
        private const string PreviewRoot = "Assets/_Project/Scenes/UIPreviewsForMe/";
        private const string MarkerName = ".port-investment-scene-validation.json";
        private static readonly string[] Paths = {
            PreviewRoot + "UI_Maintenance.unity",
            PreviewRoot + "UIMaintenance/UIMaintenance_RepairAvailable.unity",
            PreviewRoot + "UIMaintenance/UIMaintenance_InsufficientMaterials.unity",
            PreviewRoot + "UIMaintenance/UIMaintenance_RepairComplete.unity"
        };
        private static readonly HashSet<string> Layers = new HashSet<string>(StringComparer.Ordinal) {
            "Maintenance Background", "Facility Backdrop Tint", "Port Facility Header", "Port Investment Panel", "Port Facility Description"
        };
        [Serializable] private sealed class Marker { public string task, project, source, snapshot; }
        [Serializable] private sealed class Baseline { public string project, source; public FileRecord[] files; }
        [Serializable] private sealed class FileRecord { public string path, sha256, packagePath; }
        [Serializable] private sealed class SceneRecord {
            public string path, beforeSha256, afterSha256, metaSha256, settingsBefore, settingsAfter;
            public int untouchedObjects, preservedCallbacks;
            public bool completeAfterReload;
            public List<string> replacedLayers = new List<string>();
        }
        [Serializable] private sealed class Manifest {
            public string project = Project, source = Source, utc, visualFolder;
            public string scope = "Four existing investment previews only. Preserves every serialized editor fixture option, scene GUID, named persistent button actions and unrelated hierarchy. Replaces the investment background, current header, panel, tint and obsolete description from the current runtime. No PlayMode, callbacks, player saves or global preview generators are invoked.";
            public bool passed;
            public List<SceneRecord> scenes = new List<SceneRecord>();
            public List<FileRecord> files = new List<FileRecord>();
            public List<FileRecord> existingDependencies = new List<FileRecord>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class SavedCaptureReport {
            public bool passed;
            public string scope = "Actual four saved investment scenes opened in private PreviewScenes without rebuilding or saving. Each captured at 1920x1080 and 3840x2160 using the production Expand ratio.";
            public List<SavedCapture> pages = new List<SavedCapture>();
        }
        [Serializable] private sealed class SavedCapture {
            public string scene, file, sha256, error;
            public int width, height;
            public bool passed;
        }

        public static void CaptureSaved()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            RequireProject();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Saved scene captures require graphics.");
            string output = Output("-investmentCapturePath");
            var report = new SavedCaptureReport();
            try
            {
                foreach (string path in Paths)
                    foreach (int width in new[] { 1920, 3840 })
                    {
                        int height = width * 9 / 16;
                        var page = new SavedCapture { scene = path, width = width, height = height, sha256 = Hash(Absolute(path)),
                            file = Path.GetFileNameWithoutExtension(path) + "_" + width + "x" + height + ".png" };
                        report.pages.Add(page);
                        Scene scene = default; GameObject cameraHost = null; RenderTexture target = null;
                        RenderTexture previous = RenderTexture.active;
                        try
                        {
                            scene = EditorSceneManager.OpenPreviewScene(path);
                            CoreGameplayDemoController controller = Controller(scene);
                            Validate(controller);
                            foreach (string name in Layers)
                                if (Content(controller).Find(name) is Transform layer) AssertPersistent(layer.gameObject);
                            Canvas canvas = controller.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                                throw new InvalidDataException("Unexpected saved CanvasScaler.");
                            float scale = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
                            Vector2 logical = new Vector2(width / scale, height / scale);
                            cameraHost = new GameObject("Investment Saved Preview Camera", typeof(Camera));
                            SceneManager.MoveGameObjectToScene(cameraHost, scene);
                            Camera camera = cameraHost.GetComponent<Camera>();
                            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                            camera.orthographic = true; camera.orthographicSize = logical.y * .5f; camera.aspect = width / (float)height;
                            camera.nearClipPlane = .1f; camera.farClipPlane = 2000f; camera.transform.position = new Vector3(0f, 0f, -1000f);
                            camera.cullingMask = 1 << 31; camera.scene = scene; camera.allowHDR = camera.allowMSAA = false;
                            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                            target.Create(); camera.targetTexture = target;
                            scaler.enabled = false; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; canvas.scaleFactor = 1f;
                            RectTransform root = canvas.GetComponent<RectTransform>(); root.sizeDelta = logical;
                            root.position = Vector3.zero; root.rotation = Quaternion.identity; root.localScale = Vector3.one;
                            foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                            for (int pass = 0; pass < 2; pass++)
                            {
                                foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                                Canvas.ForceUpdateCanvases();
                                if (GraphicsSettings.currentRenderPipeline != null)
                                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                                else camera.Render();
                            }
                            RenderTexture.active = target;
                            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                            try { pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output, page.file), pixels.EncodeToPNG()); }
                            finally { Object.DestroyImmediate(pixels); }
                            if (Hash(Absolute(path)) != page.sha256) throw new IOException("Saved scene changed during read-only capture.");
                            page.passed = true;
                        }
                        catch (Exception error) { page.error = error.ToString(); Debug.LogException(error); }
                        finally
                        {
                            RenderTexture.active = previous;
                            if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
                        }
                    }
                report.passed = report.pages.Count == 8 && report.pages.All(page => page.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false)); }
            if (!report.passed) throw new InvalidOperationException("Saved investment capture failed; inspect report.json.");
            Debug.Log("INVESTMENT_SAVED_CAPTURE_OK: eight actual saved-scene frames at " + output);

#endif
        }

        public static void Run()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            RequireProject();
            string output = Output("-investmentSyncPath");
            var manifest = new Manifest { utc = DateTime.UtcNow.ToString("O"),
                visualFolder = "Assets/_Project/Presentation/EditablePreviewAssets/PortInvestmentScene20261013_" + Guid.NewGuid().ToString("N") };
            try
            {
                Baseline baseline = ReadBaseline();
                // Package script paths returned by AssetDatabase are virtual:
                // Unity may resolve them through Library/PackageCache. The
                // source project need not have a physical Packages/<id> tree.
                // Lock the package graph instead, before any scene is saved.
                foreach (string packageFile in new[] { "Packages/manifest.json", "Packages/packages-lock.json" })
                {
                    string scratchFile = Absolute(packageFile);
                    string sourceFile = Path.Combine(Source, packageFile);
                    if (!File.Exists(scratchFile) || !File.Exists(sourceFile))
                        throw new FileNotFoundException("Missing package version manifest: " + packageFile);
                    string digest = Hash(scratchFile);
                    if (Hash(sourceFile) != digest)
                        throw new IOException("Package versions differ from production; resync before preview work: " + packageFile);
                    manifest.existingDependencies.Add(new FileRecord { path = packageFile, sha256 = digest });
                }
                foreach (string path in Paths)
                    foreach (string file in new[] { path, path + ".meta" })
                    {
                        FileRecord original = baseline.files.Single(item => item.path == file);
                        if (Hash(Absolute(file)) != original.sha256 || Hash(Path.Combine(Source, file)) != original.sha256)
                            throw new IOException("Scene changed since source snapshot; Sync again before any preview work: " + file);
                    }
                var unrelatedScenes = Directory.GetFiles(Absolute("Assets/_Project/Scenes"), "*.unity", SearchOption.AllDirectories)
                    .Where(file => !Paths.Contains(Relative(file))).ToDictionary(file => file, Hash, StringComparer.OrdinalIgnoreCase);
                AssetDatabase.CreateFolder("Assets/_Project/Presentation/EditablePreviewAssets", Path.GetFileName(manifest.visualFolder));
                var persistence = new Persistence(manifest.visualFolder);
                foreach (string path in Paths) Sync(path, manifest, persistence, output);
                AssetDatabase.SaveAssets();
                foreach (var previous in unrelatedScenes)
                    if (Hash(previous.Key) != previous.Value) throw new IOException("Unrelated scene was modified: " + previous.Key);
                foreach (SceneRecord scene in manifest.scenes) Package(scene.path, output, manifest);
                Package(manifest.visualFolder + ".meta", output, manifest);
                foreach (string file in Directory.GetFiles(Absolute(manifest.visualFolder), "*", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.Ordinal))
                    Package(Relative(file), output, manifest);
                var packaged = new HashSet<string>(manifest.files.Select(file => file.path), StringComparer.Ordinal);
                foreach (string dependency in AssetDatabase.GetDependencies(Paths, true).OrderBy(path => path, StringComparer.Ordinal))
                {
                    // CopyBack rechecks the two package manifests above. Never
                    // copy or compare a virtual package script as a source file.
                    if (dependency.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                    if (packaged.Contains(dependency) || !File.Exists(Absolute(dependency))) continue;
                    foreach (string file in new[] { dependency, dependency + ".meta" })
                    {
                        if (!File.Exists(Absolute(file))) continue;
                        string digest = Hash(Absolute(file));
                        if (!File.Exists(Path.Combine(Source, file)) || Hash(Path.Combine(Source, file)) != digest)
                            throw new IOException("Existing dependency differs from production; resync before copyback: " + file);
                        manifest.existingDependencies.Add(new FileRecord { path = file, sha256 = digest });
                    }
                }
                manifest.passed = manifest.scenes.Count == 4 && manifest.scenes.All(scene => scene.completeAfterReload);
            }
            catch (Exception error) { manifest.errors.Add(error.ToString()); Debug.LogException(error); }
            finally { File.WriteAllText(Path.Combine(output, "copy-manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false)); }
            if (!manifest.passed) throw new InvalidOperationException("Investment preview sync failed; inspect copy-manifest.json.");
            Debug.Log("INVESTMENT_PREVIEW_SYNC_OK: four investment scenes and task-owned visual dependencies packaged at " + output);

#endif
        }

        private static void Sync(string path, Manifest manifest, Persistence persistence, string output)
        {
            string absolute = Absolute(path);
            var record = new SceneRecord { path = path, beforeSha256 = Hash(absolute), metaSha256 = Hash(absolute + ".meta"), settingsBefore = Settings(File.ReadAllText(absolute)) };
            manifest.scenes.Add(record);
            string backup = Path.Combine(output, "originals", path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(absolute, backup); File.Copy(absolute + ".meta", backup + ".meta");
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = Controller(scene);
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.Maintenance || !destination.EditorPreviewPersistsGeneratedUi)
                throw new InvalidDataException("Expected a persistent investment preview: " + path);
            Transform target = Content(destination);
            Transform[] unaffected = destination.GetComponentsInChildren<Transform>(true).Where(item => !IsInsideLayer(item, target)).ToArray();
            var identities = unaffected.ToDictionary(item => item, item => Tuple.Create(item.name, item.parent, item.gameObject.activeSelf));
            record.untouchedObjects = unaffected.Length;
            Scene stagingScene = EditorSceneManager.NewPreviewScene();
            GameObject host = null;
            try
            {
                host = new GameObject("__InvestmentScopedPreviewStaging");
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, stagingScene);
                var source = host.AddComponent<CoreGameplayDemoController>();
                CopyFixture(destination, source);
                host.SetActive(true); source.RefreshEditorPreview();
                Validate(source);
                Transform generated = Content(source);
                var scoped = new HashSet<string>(Layers, StringComparer.Ordinal);
                foreach (Transform child in generated)
                    if (child.name.StartsWith("Port Investment ", StringComparison.Ordinal)) scoped.Add(child.name);
                foreach (Transform child in target)
                    if (child.name.StartsWith("Port Investment ", StringComparison.Ordinal)) scoped.Add(child.name);
                foreach (string name in scoped)
                {
                    Transform replacement = generated.Find(name);
                    Transform old = target.Find(name);
                    if (replacement == null && (name == "Maintenance Background" || name == "Port Facility Header" || name == "Port Investment Panel"))
                        throw new InvalidDataException("Missing required current runtime layer: " + name);
                    if (replacement == null && old == null) continue;
                    if (replacement != null)
                    {
                        persistence.PersistReferences(replacement.gameObject);
                        GameObject clone = Object.Instantiate(replacement.gameObject, target, false);
                        clone.name = name;
                        if (old != null) PreserveCallbacks(old, clone.transform, record);
                        CleanFlags(clone);
                        if (old != null) Object.DestroyImmediate(old.gameObject);
                        clone.transform.SetSiblingIndex(Math.Min(replacement.GetSiblingIndex(), target.childCount - 1));
                    }
                    else Object.DestroyImmediate(old.gameObject);
                    record.replacedLayers.Add(name);
                }
                foreach (var item in identities)
                    if (item.Key == null || item.Key.name != item.Value.Item1 || item.Key.parent != item.Value.Item2 || item.Key.gameObject.activeSelf != item.Value.Item3)
                        throw new InvalidDataException("Unrelated authored object changed during scoped sync: " + item.Value.Item1);
                Validate(destination);
                foreach (string name in scoped)
                    if (target.Find(name) is Transform layer) AssertPersistent(layer.gameObject);
                if (Hash(absolute) != record.beforeSha256 || Hash(absolute + ".meta") != record.metaSha256)
                    throw new IOException("Scratch scene changed while synchronization was preparing: " + path);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to save " + path);
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(stagingScene);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            record.settingsAfter = Settings(File.ReadAllText(absolute));
            if (record.settingsAfter != record.settingsBefore || Hash(absolute + ".meta") != record.metaSha256)
                throw new InvalidDataException("Serialized fixture options or scene GUID changed: " + path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene reopened = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController saved = Controller(reopened);
            Validate(saved);
            foreach (string name in record.replacedLayers)
                if (Content(saved).Find(name) is Transform layer) AssertPersistent(layer.gameObject);
            record.completeAfterReload = saved.HasCompleteEditorPreview && saved.EditorPreviewPersistsGeneratedUi;
            record.afterSha256 = Hash(absolute);
        }

        private static void CopyFixture(CoreGameplayDemoController from, CoreGameplayDemoController to)
        {
            var original = new SerializedObject(from); var generated = new SerializedObject(to);
            SerializedProperty property = original.GetIterator();
            while (property.NextVisible(true))
                if (property.name.StartsWith("editorPreview", StringComparison.Ordinal)) generated.CopyFromSerializedProperty(property);
            generated.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void PreserveCallbacks(Transform old, Transform replacement, SceneRecord record)
        {
            foreach (Button button in old.GetComponentsInChildren<Button>(true))
            {
                int count = button.onClick.GetPersistentEventCount();
                if (count == 0) continue;
                Button[] matches = replacement.GetComponentsInChildren<Button>(true).Where(candidate => candidate.name == button.name).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Authored persistent action has no unique replacement: " + button.name);
                var source = new SerializedObject(button); var target = new SerializedObject(matches[0]);
                target.CopyFromSerializedProperty(source.FindProperty("m_OnClick"));
                target.ApplyModifiedPropertiesWithoutUndo(); record.preservedCallbacks += count;
            }
        }
        private static bool IsInsideLayer(Transform item, Transform content)
        {
            for (Transform at = item; at != null && at != content; at = at.parent)
                if (at.parent == content && (Layers.Contains(at.name) || at.name.StartsWith("Port Investment ", StringComparison.Ordinal))) return true;
            return false;
        }
        private static void Validate(CoreGameplayDemoController controller)
        {
            if (!controller.HasCompleteEditorPreview || !controller.EditorPreviewPersistsGeneratedUi || controller.EditorPreviewScreen != CoreGameplayPreviewScreen.Maintenance)
                throw new InvalidDataException("Incomplete persistent investment preview.");
            Transform content = Content(controller);
            RawImage background = content.Find("Maintenance Background")?.GetComponent<RawImage>();
            if (background == null || background.texture == null || !background.texture.name.Contains("port_investment_scene_internal_v1") || background.uvRect != new Rect(0f, 0f, 1f, 1f))
                throw new InvalidDataException("Investment scene does not display the approved full-frame internal background.");
            Transform panel = content.Find("Port Investment Panel");
            if (panel == null || panel.GetComponent<Image>().color.a > .001f) throw new InvalidDataException("Investment panel must leave the approved painting visible.");
            foreach (string name in new[] { "Port Investment Project Name", "Port Investment Ledger", "Port Investment Summary", "Confirm Port Investment", "Open Port Market", "Open Investment Warehouse" })
                if (panel.GetComponentsInChildren<Transform>(true).Count(item => item.name == name) != 1) throw new InvalidDataException("Missing/duplicate investment node: " + name);
        }
        private static void CleanFlags(GameObject root)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                item.gameObject.hideFlags = HideFlags.None;
                foreach (Component component in item.GetComponents<Component>())
                    if (component != null) component.hideFlags = HideFlags.None;
            }
        }
        private sealed class Persistence
        {
            private readonly string folder;
            private readonly Dictionary<int, Object> copies = new Dictionary<int, Object>();
            private int serial;
            public Persistence(string path) { folder = path; }
            public void PersistReferences(GameObject host)
            {
                foreach (Component component in host.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidDataException("Missing script in runtime investment layer.");
                    var serialized = new SerializedObject(component); SerializedProperty property = serialized.GetIterator(); bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object source = property.objectReferenceValue;
                        if (source == null || IsPersistent(source)) continue;
                        if (source is Sprite || source is Texture2D || source is Material) { property.objectReferenceValue = Copy(source); changed = true; }
                    }
                    if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(component); }
                }
            }
            private Object Copy(Object source)
            {
                if (source == null || IsPersistent(source)) return source;
                if (copies.TryGetValue(source.GetInstanceID(), out Object previous)) return previous;
                if (source is Sprite sprite)
                {
                    Vector2 pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                    Sprite copy = Sprite.Create((Texture2D)Copy(sprite.texture), sprite.rect, pivot, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
                    if (Vector2.Distance(copy.pivot, sprite.pivot) > .02f) throw new InvalidDataException("Sprite pivot changed: " + sprite.name);
                    return Save(source, copy, ".sprite.asset");
                }
                if (source is Texture2D texture) return Save(source, Object.Instantiate(texture), ".texture.asset");
                if (source is Material material)
                {
                    Material copy = (Material)Save(source, new Material(material), ".mat");
                    foreach (string name in copy.GetTexturePropertyNames())
                    {
                        Texture sampler = copy.GetTexture(name);
                        if (sampler == null || IsPersistent(sampler)) continue;
                        if (!(sampler is Texture2D)) throw new InvalidDataException("Unsupported transient material texture: " + name);
                        copy.SetTexture(name, (Texture)Copy(sampler));
                    }
                    EditorUtility.SetDirty(copy); return copy;
                }
                throw new NotSupportedException("Unsupported generated visual: " + source.name);
            }
            private Object Save(Object source, Object copy, string extension)
            {
                string name = new string(source.name.Select(letter => char.IsLetterOrDigit(letter) || letter == '_' ? letter : '_').Take(90).ToArray());
                string path = folder + "/" + (++serial).ToString("D4") + "_" + name + extension;
                copy.name = source.name; copy.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(copy, path); copies.Add(source.GetInstanceID(), copy); return copy;
            }
        }
        private static void AssertPersistent(GameObject host)
        {
            foreach (Component component in host.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidDataException("Missing script in saved investment layer.");
                var serialized = new SerializedObject(component); SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Object value &&
                        (value is Sprite || value is Texture || value is Material) && !IsPersistent(value))
                        throw new InvalidDataException("Transient saved visual: " + component.name + "." + property.propertyPath);
            }
        }
        private static void Package(string path, string output, Manifest manifest)
        {
            string relative = "files/" + path; string destination = Path.Combine(output, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(Absolute(path), destination);
            manifest.files.Add(new FileRecord { path = path, packagePath = relative, sha256 = Hash(destination) });
        }
        private static Baseline ReadBaseline()
        {
            Marker marker = JsonUtility.FromJson<Marker>(File.ReadAllText(Path.Combine(Project, MarkerName)));
            if (marker.task != "PortInvestmentScene20261013" || Normalize(marker.project) != Project || Normalize(marker.source) != Source)
                throw new InvalidOperationException("Invalid dedicated scratch marker.");
            Baseline baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(marker.snapshot));
            if (Normalize(baseline.project) != Project || Normalize(baseline.source) != Source || baseline.files == null || baseline.files.Length != 8)
                throw new InvalidDataException("Expected the eight scene/meta source snapshot entries.");
            return baseline;
        }
        private static void RequireProject()
        {
            if (Normalize(Path.Combine(Application.dataPath, "..")) != Project || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Investment sync requires stopped isolated project " + Project);
            if (PlayerSettings.companyName != "PortInvestmentSceneValidation" || PlayerSettings.productName != "PortInvestmentSceneValidation20261013")
                throw new InvalidOperationException("Isolated PlayerPrefs identity was not configured.");
        }
        private static string Output(string flag)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("Absolute " + flag + " required.");
            string output = Normalize(args[index + 1]);
            if (!output.StartsWith("D:/Game/Artifacts/PortInvestmentRedesign20261013/validation/", StringComparison.OrdinalIgnoreCase)) throw new IOException("Output must be in task validation artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use a new sync output directory.");
            Directory.CreateDirectory(output); return output;
        }
        private static CoreGameplayDemoController Controller(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
        private static Transform Content(CoreGameplayDemoController controller) => controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
        private static bool IsPersistent(Object value) => value != null && (EditorUtility.IsPersistent(value) || AssetDatabase.GetAssetPath(value).Length != 0);
        private static string Settings(string text) => string.Join("\n", Regex.Matches(text, @"(?m)^  editorPreview[^\r\n]*").Cast<Match>().Select(match => match.Value));
        private static string Normalize(string path) => Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        private static string Absolute(string path) => Path.GetFullPath(Path.Combine(Project, path));
        private static string Relative(string path) => Normalize(path).Substring(Project.Length + 1);
        private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    }
}
