using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    // Disposable-project scene authoring only. The existing builder cannot be
    // reused directly: its private save method writes a shared asset directory
    // and passes the source sprite's pixel pivot as a normalized Sprite.Create
    // pivot. This helper shares the same public preview construction API while
    // making only new, task-owned assets and one new scene.
    public static class DepartureDockSceneSave
    {
        private const string IsolatedProject = "D:/Game/tmp/departure-dock-scene-20261012";
        private const string ScenePath = "Assets/_Project/Scenes/UIPreviewsForMe/UIVoyageDeparture/UIVoyageDeparture_BrokenAnchorPaintedV3.unity";
        private const string VisualFolder = "Assets/_Project/Presentation/EditablePreviewAssets/DepartureDockScenePaintedV320261012";
        private const string BackgroundResource = "PortFacilities/Regional/Voyage/voyage_broken_anchor_scene_v3";

        [Serializable] private sealed class Manifest
        {
            public string utc, project, scenePath = ScenePath, visualFolder = VisualFolder;
            public string scope = "One new editable production-controller preview. Existing authored scenes are hash-checked and never saved. Persistent sprites preserve normalized pivot, source rect, border and pixels-per-unit. PlayerPrefs are not read or written by this helper.";
            public string playBehavior = "Matches existing editable previews: entering Play removes the saved editor Canvas and the real controller restores the player's current profile, vessel and port. The editor's sample session and runtime-added UnityEvent callbacks are not serialized or substituted for player progress.";
            public bool passed, completeAfterReload, persistedAfterReload, oldScenesUnchanged;
            public int gameObjects, components, graphics, buttons, spriteCopies, textureCopies, materialCopies;
            public List<FileEntry> files = new List<FileEntry>();
            public List<FileEntry> existingDependencies = new List<FileEntry>();
            public List<PivotEntry> sprites = new List<PivotEntry>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class FileEntry
        {
            public string assetPath, sourcePath, packagePath, sha256, guid;
            public long bytes;
        }
        [Serializable] private sealed class PivotEntry
        {
            public string path, name;
            public Vector2 sourcePixelPivot, normalizedPivot, savedPixelPivot;
            public Rect sourceRect;
            public Vector4 border;
            public float pixelsPerUnit;
        }

        public static void SaveFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            string project = RequireIsolatedProject();
            string output = RequiredEmptyOutput("-departureDockSceneSavePath");
            var report = new Manifest { utc = DateTime.UtcNow.ToString("O"), project = project };
            var existingScenes = SnapshotExistingScenes();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Scene authoring requires the stopped editor.");
                if (File.Exists(Absolute(ScenePath)) || File.Exists(Absolute(ScenePath) + ".meta"))
                    throw new IOException("Refusing to overwrite the new scene or its metadata: " + ScenePath);
                if (Directory.Exists(Absolute(VisualFolder)) || File.Exists(Absolute(VisualFolder) + ".meta"))
                    throw new IOException("Task-owned visual directory must not already exist: " + VisualFolder);
                if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/')))
                    throw new DirectoryNotFoundException("Expected existing departure scene folder is missing.");
                Texture2D expectedBackground = Resources.Load<Texture2D>(BackgroundResource);
                if (expectedBackground == null) throw new InvalidOperationException("Missing v3 painted-control background: " + BackgroundResource);

                string parent = Path.GetDirectoryName(VisualFolder).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent)) throw new DirectoryNotFoundException(parent);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(VisualFolder));

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = Path.GetFileNameWithoutExtension(ScenePath);
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.03f, .10f, .14f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 540f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.farClipPlane = 2000f;
                cameraObject.tag = "MainCamera";

                var host = new GameObject("UI Preview · 断锚港场景码头 · 可编辑");
                host.SetActive(false);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.BrokenAnchor, 0, true);
                host.SetActive(true);
                controller.RefreshEditorPreview();
                ValidatePreview(controller, expectedBackground);
                Canvas.ForceUpdateCanvases();

                var persistence = new Persistence(report);
                persistence.PersistReferences(host);
                Canvas.ForceUpdateCanvases();
                AssertNoTransientVisualReferences(host);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);

                // Destroy all source runtime objects before reopening. A preview
                // that only works while unsaved caches are alive is not complete.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                ValidatePreview(controller, expectedBackground);
                AssertNoTransientVisualReferences(controller.gameObject);
                report.completeAfterReload = controller.HasCompleteEditorPreview;
                report.persistedAfterReload = controller.EditorPreviewPersistsGeneratedUi;
                report.gameObjects = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
                report.components = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Component>(true).Length);
                report.graphics = controller.GetComponentsInChildren<Graphic>(true).Length;
                report.buttons = controller.GetComponentsInChildren<Button>(true).Length;

                VerifyExistingScenes(existingScenes);
                report.oldScenesUnchanged = true;
                foreach (string path in NewFilePaths()) report.files.Add(Package(path, output));
                var newFiles = new HashSet<string>(report.files.Select(file => file.assetPath), StringComparer.OrdinalIgnoreCase);
                foreach (string dependency in AssetDatabase.GetDependencies(ScenePath, true).OrderBy(path => path, StringComparer.Ordinal))
                    if (!newFiles.Contains(dependency) && File.Exists(Absolute(dependency)))
                        report.existingDependencies.Add(Describe(dependency));
                report.passed = report.completeAfterReload && report.persistedAfterReload && report.oldScenesUnchanged && report.files.Count > 2;
            }
            catch (Exception error)
            {
                report.errors.Add(error.ToString());
                Debug.LogException(error);
            }
            finally
            {
                File.WriteAllText(Path.Combine(output, "copy-manifest.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            if (!report.passed) throw new InvalidOperationException("New dock preview scene generation failed; inspect copy-manifest.json.");
            Debug.Log("DEPARTURE_DOCK_SCENE_SAVE_OK: new scene plus " + (report.files.Count - 2) + " asset/metadata files; " + output);

#endif
        }

        private sealed class Persistence
        {
            private readonly Manifest report;
            private readonly Dictionary<int, Object> copies = new Dictionary<int, Object>();
            private int serial;
            public Persistence(Manifest report) { this.report = report; }

            public void PersistReferences(GameObject host)
            {
                foreach (Component component in host.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidDataException("Missing script in new dock preview.");
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object source = property.objectReferenceValue;
                        if (source == null || IsPersistent(source)) continue;
                        if (source is Sprite || source is Texture2D || source is Material)
                        {
                            property.objectReferenceValue = Copy(source);
                            changed = true;
                        }
                    }
                    if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(component); }
                }
            }

            private Object Copy(Object source)
            {
                if (source == null || IsPersistent(source)) return source;
                if (copies.TryGetValue(source.GetInstanceID(), out Object cached)) return cached;
                if (source is Sprite sprite) return CopySprite(sprite);
                if (source is Texture2D texture) return CopyTexture(texture);
                if (source is Material material) return CopyMaterial(material);
                throw new NotSupportedException("Unsupported transient visual: " + source.GetType().Name + " " + source.name);
            }

            private Sprite CopySprite(Sprite source)
            {
                if (source.texture == null || source.rect.width <= 0f || source.rect.height <= 0f)
                    throw new InvalidDataException("Invalid generated sprite: " + source.name);
                Texture2D texture = (Texture2D)Copy(source.texture);
                Vector2 normalizedPivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
                Sprite copy = Sprite.Create(texture, source.rect, normalizedPivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                copy.name = source.name;
                copy.hideFlags = HideFlags.None;
                if (Vector2.Distance(copy.pivot, source.pivot) > .02f)
                    throw new InvalidDataException("Sprite normalized pivot did not preserve its pixel contact: " + source.name);
                string path = Save(source, copy, ".sprite.asset");
                report.spriteCopies++;
                report.sprites.Add(new PivotEntry { path = path, name = source.name, sourcePixelPivot = source.pivot,
                    normalizedPivot = normalizedPivot, savedPixelPivot = copy.pivot, sourceRect = source.rect,
                    border = source.border, pixelsPerUnit = source.pixelsPerUnit });
                return copy;
            }

            private Texture2D CopyTexture(Texture2D source)
            {
                Texture2D copy = Object.Instantiate(source);
                copy.name = source.name;
                copy.hideFlags = HideFlags.None;
                Save(source, copy, ".texture.asset");
                report.textureCopies++;
                return copy;
            }

            private Material CopyMaterial(Material source)
            {
                Material copy = new Material(source) { name = source.name, hideFlags = HideFlags.None };
                // Cache before traversing dependencies so shared generated
                // textures are persisted once and every sampler is retained.
                Save(source, copy, ".mat");
                foreach (string property in copy.GetTexturePropertyNames())
                {
                    Texture texture = copy.GetTexture(property);
                    if (texture == null || IsPersistent(texture)) continue;
                    if (!(texture is Texture2D)) throw new NotSupportedException("Generated material contains non-Texture2D sampler: " + property);
                    copy.SetTexture(property, (Texture)Copy(texture));
                }
                EditorUtility.SetDirty(copy);
                report.materialCopies++;
                return copy;
            }

            private string Save(Object source, Object copy, string extension)
            {
                string path = VisualFolder + "/" + (++serial).ToString("D3") + "_" + SafeName(source.name) + extension;
                if (File.Exists(Absolute(path))) throw new IOException("Refusing to replace generated asset: " + path);
                AssetDatabase.CreateAsset(copy, path);
                copies.Add(source.GetInstanceID(), copy);
                return path;
            }
        }

        private static void ValidatePreview(CoreGameplayDemoController controller, Texture2D expectedBackground)
        {
            if (!controller.EditorPreviewPersistsGeneratedUi || !controller.HasCompleteEditorPreview)
                throw new InvalidDataException("Incomplete persistent departure preview.");
            if (controller.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture)
                throw new InvalidDataException("Saved controller is not configured for departure.");
            Transform[] nodes = controller.GetComponentsInChildren<Transform>(true);
            foreach (string name in new[] { "Route Selection", "Voyage Setup", "Starter Modular Ship", "Inspect Departure Vessel",
                "Departure Dock Water Contact", "Departure Treasure Seed Picker", "Use Chart Seed", "Departure Previous Vessel",
                "Departure Next Vessel", "Departure Buy Provisions", "Adjust Voyage Supplies", "Configure Voyage Battle Decks",
                "Configure Voyage Crew", "Start Voyage", "Departure Dock Ordinary Voyage", "Departure Dock Chart Shortcut",
                "Departure Dock Ordinary Readout", "Departure Dock Special Readout", "Departure Dock Speed Readout", "Departure Dock Foreground" })
                if (nodes.Count(node => node.name == name) != 1) throw new InvalidDataException("Missing/duplicate production node: " + name);
            RawImage background = controller.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Voyage Background");
            if (background.texture != expectedBackground) throw new InvalidDataException("Saved dock background is not the v3 painted-control image.");
            RectTransform route = nodes.Single(node => node.name == "Route Selection") as RectTransform;
            RectTransform hit = nodes.Single(node => node.name == "Inspect Departure Vessel") as RectTransform;
            RectTransform water = nodes.Single(node => node.name == "Departure Dock Water Contact") as RectTransform;
            if (route == null || Vector2.Distance(route.sizeDelta, PhaseSurfaceLayout.ContentSize) > .2f || route.anchoredPosition.sqrMagnitude > .04f)
                throw new InvalidDataException("Saved painted route controls do not use the full scene surface.");
            if (hit == null || Vector2.Distance(hit.sizeDelta, DepartureDockSceneLayout.ShipHitSize) > .2f ||
                Vector2.Distance(hit.anchoredPosition, DepartureDockSceneLayout.ShipHitPosition) > .2f)
                throw new InvalidDataException("Saved vessel inspection area does not match the v3 scene.");
            if (water == null || Vector2.Distance(water.anchoredPosition,
                new Vector2(DepartureDockSceneLayout.ShipCenterX, DepartureDockSceneLayout.ShipWaterlineY)) > .2f)
                throw new InvalidDataException("Saved vessel water contact left the authored berth anchor.");
            DepartureDockForegroundGraphic foreground = controller.GetComponentsInChildren<DepartureDockForegroundGraphic>(true).Single();
            CanvasRenderer renderer = foreground.GetComponent<CanvasRenderer>();
            if (renderer == null || !foreground.isActiveAndEnabled || foreground.raycastTarget || foreground.mainTexture != expectedBackground)
                throw new InvalidDataException("Saved dock foreground is missing its renderer, image, or nonblocking active state.");
            Canvas.ForceUpdateCanvases();
            foreground.Rebuild(CanvasUpdate.PreRender);
            Mesh mesh = renderer.GetMesh();
            if (mesh == null || mesh.vertexCount <= 0 || mesh.triangles.Length <= 0)
                throw new InvalidDataException("Saved dock foreground does not generate a populated rendering mesh.");
            foreach (DeparturePaintedHotspot feedback in controller.GetComponentsInChildren<DeparturePaintedHotspot>(true))
                if (feedback.raycastTarget) throw new InvalidDataException("Saved decorative feedback blocks input: " + feedback.name);
            foreach (Transform node in nodes)
                if ((node.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    throw new InvalidDataException("Unsavable GameObject remains in the authored hierarchy: " + node.name);
        }

        private static void AssertNoTransientVisualReferences(GameObject host)
        {
            foreach (Component component in host.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidDataException("Saved preview contains a missing script.");
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object source = property.objectReferenceValue;
                    if ((source is Sprite || source is Texture || source is Material) && !IsPersistent(source))
                        throw new InvalidDataException("Transient visual reference: " + component.name + "." + property.propertyPath + " -> " + source.name);
                }
            }
        }

        private static bool IsPersistent(Object value) => value != null && (EditorUtility.IsPersistent(value) || AssetDatabase.GetAssetPath(value).Length != 0);

        private static Dictionary<string, string> SnapshotExistingScenes()
        {
            string root = Absolute("Assets/_Project/Scenes");
            return Directory.GetFiles(root, "*.unity", SearchOption.AllDirectories)
                .SelectMany(path => File.Exists(path + ".meta") ? new[] { path, path + ".meta" } : new[] { path })
                .ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)), StringComparer.OrdinalIgnoreCase);
        }

        private static void VerifyExistingScenes(Dictionary<string, string> snapshot)
        {
            foreach (KeyValuePair<string, string> existing in snapshot)
                if (!File.Exists(existing.Key) || Hash(File.ReadAllBytes(existing.Key)) != existing.Value)
                    throw new InvalidDataException("An existing authored scene/meta changed unexpectedly: " + existing.Key);
        }

        private static IEnumerable<string> NewFilePaths()
        {
            yield return ScenePath;
            yield return ScenePath + ".meta";
            yield return VisualFolder + ".meta";
            string project = RequireIsolatedProject();
            foreach (string file in Directory.GetFiles(Absolute(VisualFolder), "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
                yield return file.Replace('\\', '/').Substring(project.Length + 1);
        }

        private static FileEntry Package(string assetPath, string output)
        {
            FileEntry entry = Describe(assetPath);
            entry.packagePath = "files/" + assetPath;
            string destination = Path.Combine(output, entry.packagePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(entry.sourcePath, destination, false);
            if (Hash(File.ReadAllBytes(destination)) != entry.sha256) throw new IOException("Package hash mismatch: " + assetPath);
            return entry;
        }

        private static FileEntry Describe(string assetPath)
        {
            string absolute = Absolute(assetPath);
            byte[] bytes = File.ReadAllBytes(absolute);
            return new FileEntry { assetPath = assetPath, sourcePath = absolute.Replace('\\', '/'), sha256 = Hash(bytes), bytes = bytes.LongLength,
                guid = assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath) };
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string SafeName(string name) => new string(name.Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').Take(100).ToArray());
        private static string Absolute(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath.Replace('/', Path.DirectorySeparatorChar)));

        private static string RequireIsolatedProject()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(project, IsolatedProject, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This helper may run only in " + IsolatedProject);
            return project;
        }

        private static string RequiredEmptyOutput(string option)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, option);
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1])) throw new ArgumentException("An absolute " + option + " is required.");
            string output = Path.GetFullPath(args[at + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Scene output must be new or empty.");
            Directory.CreateDirectory(output);
            return output;
        }
    }
}
