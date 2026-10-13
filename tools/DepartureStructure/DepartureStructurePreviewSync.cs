using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    // This migration is deliberately restricted to the disposable project.
    // Unity serializes candidates there; only whitelisted visual fields are
    // copied into the original YAML blocks, preserving every authored ID.
    public static class DepartureStructurePreviewSync
    {
        private const string IsolatedProject = "D:/Game/tmp/departure-structure-20261012";
        private const string ProductionProject = "D:/Game/UnityClient";
        private const string SpriteFolder = "Assets/_Project/Presentation/EditablePreviewAssets/DepartureStructure20261012";
        private static readonly Regex Blocks = new Regex(@"(?ms)^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
        private static readonly string[] RectFields = { "m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_AnchorMin", "m_AnchorMax", "m_AnchoredPosition", "m_SizeDelta", "m_Pivot" };
        private static readonly string[] GraphicFields = { "m_Enabled", "m_Color" };
        private static readonly string[] ImageFields = { "m_Sprite", "m_Type", "m_PreserveAspect", "m_FillCenter", "m_FillMethod", "m_FillAmount", "m_FillClockwise", "m_FillOrigin", "m_UseSpriteMesh", "m_PixelsPerUnitMultiplier" };
        private static readonly string[] ShadowFields = { "m_Enabled", "m_EffectColor", "m_EffectDistance", "m_UseGraphicAlpha" };

        [Serializable] private sealed class Report
        {
            public string capturedAtUtc, isolatedProject = IsolatedProject, productionProject = ProductionProject;
            public string scope = "Exactly the 14 ChartSeedReferencePaths. Production DepartureLayout.Apply and DepartureSkin.Apply, retaining every existing object/component ID, callbacks, labels, scene state and nonvisual serialized data. Only whitelisted visual YAML fields are patched. Missing WoodButtonRoleState outline components may be appended only to Route 0..2 and Start Voyage; no GameObjects are added. No automatic production writes.";
            public bool complete;
            public string failure;
            public List<SceneResult> scenes = new List<SceneResult>();
            public List<CopyFile> copyBackFiles = new List<CopyFile>();
            public List<string> spriteAssets = new List<string>();
        }
        [Serializable] private sealed class SceneResult
        {
            public string path, beforeSha256, afterSha256, nonVisualBeforeSha256, nonVisualAfterSha256, yamlNonVisualBeforeSha256, yamlNonVisualAfterSha256;
            public int objects, components, yamlBlocks, changedBlocks, changedFields, addedVisualComponents;
            public bool objectIdsUnchanged, nonVisualStateUnchanged, reloaded;
            public List<string> newVisualComponentIds = new List<string>();
            public List<FieldChange> changes = new List<FieldChange>();
        }
        [Serializable] private sealed class FieldChange { public string objectId, objectName, type, field, before, after; }
        [Serializable] private sealed class CopyFile { public string assetPath, sourcePath, originalSha256, sha256; public bool originallyExisted; }
        private sealed class Patch { public string path; public byte[] before, after; public SceneResult result; }
        private sealed class Snapshot { public Object target; public string id, baseline; public string[] allowed; }

        // -executeMethod ShatteredSail.Editor.DepartureStructurePreviewSync.SyncFromCommandLine
        // -departureStructureSyncPath <new or empty absolute directory under D:/Game/Artifacts>
        public static void SyncFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(project, IsolatedProject, StringComparison.OrdinalIgnoreCase) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run only in the stopped disposable project: " + IsolatedProject);
            string output = OutputPath();
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            var patches = new List<Patch>();
            var originals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] paths = GameplayPreviewSceneBuilder.ChartSeedReferencePaths.Select(reference => GameplayPreviewSceneBuilder.EditableSceneFolder + "/" + reference).ToArray();
                if (paths.Length != 14 || paths.Distinct().Count() != 14) throw new InvalidDataException("Expected exactly 14 distinct departure preview paths.");
                foreach (string path in paths)
                {
                    Remember(path, originals, true);
                    Remember(path + ".meta", originals, true);
                    Backup(path, output);
                    Backup(path + ".meta", output);
                }
                if (Directory.Exists(Absolute(SpriteFolder)))
                    foreach (string file in Directory.EnumerateFiles(Absolute(SpriteFolder), "*", SearchOption.AllDirectories))
                        RememberGenerated(Relative(file), originals);
                if (File.Exists(Absolute(SpriteFolder + ".meta"))) RememberGenerated(SpriteFolder + ".meta", originals);
                EnsureAssetFolder(SpriteFolder);
                foreach (string path in paths) patches.Add(Prepare(path, File.ReadAllBytes(Absolute(path)), report.spriteAssets));
                foreach (Patch patch in patches)
                    if (!File.ReadAllBytes(Absolute(patch.path)).SequenceEqual(patch.before)) throw new IOException("Isolated scene changed during preparation: " + patch.path);
                AssetDatabase.SaveAssets();
                foreach (Patch patch in patches)
                {
                    File.WriteAllBytes(Absolute(patch.path), patch.after);
                    report.scenes.Add(patch.result);
                    string after = Path.Combine(output, "After", patch.path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(after));
                    File.WriteAllBytes(after, patch.after);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (Patch patch in patches) VerifyReload(patch);
                foreach (string path in paths)
                    if (HashFile(Absolute(path + ".meta")) != originals[path + ".meta"]) throw new InvalidDataException("Scene meta changed: " + path);
                var copyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Patch patch in patches) if (!patch.before.SequenceEqual(patch.after)) copyPaths.Add(patch.path);
                foreach (string asset in report.spriteAssets) { copyPaths.Add(asset); copyPaths.Add(asset + ".meta"); }
                copyPaths.Add(SpriteFolder + ".meta");
                foreach (string path in copyPaths.OrderBy(value => value, StringComparer.Ordinal))
                {
                    string hash = HashFile(Absolute(path));
                    bool existed = originals.TryGetValue(path, out string original);
                    // Existing assets are reused; unchanged files need not be copied.
                    if (existed && original == hash) continue;
                    string production = Path.Combine(ProductionProject, path.Replace('/', Path.DirectorySeparatorChar));
                    if (!existed && File.Exists(production)) throw new IOException("New isolated asset collides with production: " + path);
                    report.copyBackFiles.Add(new CopyFile { assetPath = path, sourcePath = Absolute(path), originallyExisted = existed, originalSha256 = original ?? string.Empty, sha256 = hash });
                    string reviewed = Path.Combine(output, "After", path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(reviewed));
                    if (!File.Exists(reviewed)) File.Copy(Absolute(path), reviewed, false);
                }
                WriteDiff(output, patches);
                report.complete = true;
            }
            catch (Exception error)
            {
                // Never leave a partially synchronized set of isolated scenes.
                foreach (Patch patch in patches) File.WriteAllBytes(Absolute(patch.path), patch.before);
                report.failure = error.ToString();
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(output, "preview-sync-report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                AssetDatabase.Refresh();
            }
            Debug.Log("DEPARTURE_STRUCTURE_PREVIEW_SYNC_OK: 14 scenes verified; IDs, callbacks, labels and nonvisual state retained. Review and copy-back manifest: " + output);

#endif
        }

        private static Patch Prepare(string path, byte[] before, List<string> spriteAssets)
        {
            string originalText = Encoding.UTF8.GetString(before);
            Dictionary<string, string> original = Parse(originalText);
            Scene scene = default;
            try
            {
                Scene loaded = SceneManager.GetSceneByPath(path);
                if (loaded.IsValid() && loaded.isLoaded) throw new InvalidOperationException("Close the saved preview before synchronization: " + path);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                CoreGameplayDemoController controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                if (!controller.HasCompleteEditorPreview || !controller.EditorPreviewPersistsGeneratedUi || controller.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture)
                    throw new InvalidDataException("Expected a complete persistent departure preview: " + path);
                RectTransform routes = controller.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "Route Selection");
                RectTransform setup = controller.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "Voyage Setup");
                Object[] inventory = Inventory(scene);
                Button[] missingRoles = new[] { routes.Find("Route 0"), routes.Find("Route 1"), routes.Find("Route 2"), setup.Find("Start Voyage") }
                    .Where(item => item != null).Select(item => item.GetComponent<Button>()).Where(button => button != null && button.GetComponent<WoodButtonRoleState>() == null).ToArray();
                var componentOwners = new HashSet<Object>(missingRoles.Select(button => (Object)button.gameObject));
                Snapshot[] snapshots = inventory.Select(target => new Snapshot { target = target, id = Id(target),
                    allowed = componentOwners.Contains(target) ? new[] { "m_Component" } : InScope(target, routes, setup) ? Allowed(target) : Array.Empty<string>() }).ToArray();
                foreach (Snapshot snapshot in snapshots) snapshot.baseline = NonVisualJson(snapshot.target, snapshot.allowed);
                var result = new SceneResult { path = path, beforeSha256 = Hash(before), objects = inventory.Count(item => item is GameObject),
                    components = inventory.Count(item => item is Component), yamlBlocks = original.Count,
                    nonVisualBeforeSha256 = NonVisualHash(snapshots) };

                var newRoles = new List<WoodButtonRoleState>();
                foreach (Button button in missingRoles)
                {
                    bool selected = false;
                    if (button.name.StartsWith("Route ", StringComparison.Ordinal))
                    {
                        Text label = button.transform.Find("Label")?.GetComponent<Text>();
                        if (label == null || string.IsNullOrEmpty(label.text) || !(label.text.StartsWith("◆", StringComparison.Ordinal) || label.text.StartsWith("◇", StringComparison.Ordinal)))
                            throw new InvalidDataException("A saved route needs its authored ◆/◇ label to initialize the visual selection state: " + path + " / " + button.name);
                        selected = label.text.StartsWith("◆", StringComparison.Ordinal);
                    }
                    WoodButtonRoleState role = button.gameObject.AddComponent<WoodButtonRoleState>();
                    var serializedRole = new SerializedObject(role);
                    serializedRole.FindProperty("selected").boolValue = selected;
                    serializedRole.ApplyModifiedPropertiesWithoutUndo();
                    newRoles.Add(role);
                }
                DepartureLayout.Apply(routes, setup);
                DepartureSkin.Apply(routes, setup);
                if (!inventory.SequenceEqual(Inventory(scene).Where(item => !newRoles.Contains(item as WoodButtonRoleState))))
                    throw new InvalidDataException("Layout/skin added, removed or reordered authored objects/components beyond the explicit visual outlines: " + path);
                foreach (Image image in snapshots.Select(snapshot => snapshot.target).OfType<Image>().Where(image => InScope(image, routes, setup)))
                {
                    if (image.sprite == null || AssetDatabase.Contains(image.sprite)) continue;
                    image.sprite = PersistSprite(image.sprite, spriteAssets);
                    image.overrideSprite = null;
                }
                result.nonVisualAfterSha256 = NonVisualHash(snapshots);
                result.nonVisualStateUnchanged = result.nonVisualBeforeSha256 == result.nonVisualAfterSha256;
                if (!result.nonVisualStateUnchanged)
                {
                    string mutated = string.Join(", ", snapshots.Where(snapshot => HasNonVisualChange(snapshot)).Select(snapshot => snapshot.target.name + " (" + snapshot.target.GetType().Name + ")"));
                    throw new InvalidDataException("Layout/skin changed nonvisual serialized state: " + path + " / " + mutated);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to serialize isolated candidate: " + path);
                Dictionary<string, string> candidate = Parse(File.ReadAllText(Absolute(path)));
                var newIds = new HashSet<string>(newRoles.Select(role => Id(role)));
                if (newIds.Contains("0") || newIds.Any(id => original.ContainsKey(id)) || !original.Keys.Concat(newIds).OrderBy(id => id).SequenceEqual(candidate.Keys.OrderBy(id => id)))
                    throw new InvalidDataException("Candidate changed authored YAML object IDs: " + path);
                foreach (WoodButtonRoleState role in newRoles)
                {
                    string owner = Id(role.gameObject);
                    string[] oldComponents = ComponentReferences(original[owner]);
                    string[] newComponents = ComponentReferences(candidate[owner]);
                    if (!oldComponents.Concat(new[] { Id(role) }).SequenceEqual(newComponents))
                        throw new InvalidDataException("The visual outline was not appended after all retained components: " + path + " / " + role.name);
                }
                result.objectIdsUnchanged = true;
                var changes = new Dictionary<string, string>();
                foreach (Snapshot snapshot in snapshots)
                {
                    if (!original.TryGetValue(snapshot.id, out string old) || !candidate.TryGetValue(snapshot.id, out string current))
                        throw new InvalidDataException("Object ID missing from candidate: " + snapshot.id + " / " + snapshot.target.name);
                    string changed = old;
                    foreach (string field in snapshot.allowed)
                    {
                        string next = CopyField(changed, current, field, path);
                        if (next == changed) continue;
                        result.changes.Add(new FieldChange { objectId = snapshot.id, objectName = snapshot.target.name, type = snapshot.target.GetType().Name,
                            field = field, before = ReadField(changed, field), after = ReadField(current, field) });
                        changed = next;
                    }
                    if (changed != old) changes[snapshot.id] = changed;
                }
                string after = Blocks.Replace(originalText, match => changes.TryGetValue(match.Groups[1].Value, out string change) ? change : match.Value);
                foreach (WoodButtonRoleState role in newRoles)
                {
                    string id = Id(role);
                    after += candidate[id];
                    result.newVisualComponentIds.Add(id);
                    result.changes.Add(new FieldChange { objectId = id, objectName = role.name, type = role.GetType().Name,
                        field = "new visual outline component", before = string.Empty, after = candidate[id] });
                }
                result.addedVisualComponents = newIds.Count;
                Dictionary<string, string> final = Parse(after);
                if (final.Count != original.Count + newIds.Count || original.Any(pair => final[pair.Key] != (changes.TryGetValue(pair.Key, out string change) ? change : pair.Value)))
                    throw new InvalidDataException("Unexpected YAML block mutation: " + path);
                result.yamlNonVisualBeforeSha256 = YamlNonVisualHash(original, snapshots);
                result.yamlNonVisualAfterSha256 = YamlNonVisualHash(final.Where(pair => original.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value), snapshots);
                if (result.yamlNonVisualBeforeSha256 != result.yamlNonVisualAfterSha256) throw new InvalidDataException("Final YAML nonvisual data changed: " + path);
                result.changedBlocks = changes.Count + newIds.Count; result.changedFields = result.changes.Count;
                byte[] afterBytes = new UTF8Encoding(false).GetBytes(after);
                result.afterSha256 = Hash(afterBytes);
                return new Patch { path = path, before = before, after = afterBytes, result = result };
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                File.WriteAllBytes(Absolute(path), before);
            }
        }

        private static void VerifyReload(Patch patch)
        {
            Scene scene = EditorSceneManager.OpenScene(patch.path, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                if (!controller.HasCompleteEditorPreview || !controller.EditorPreviewPersistsGeneratedUi) throw new InvalidDataException("Patched preview did not reload: " + patch.path);
                var originalIds = Parse(Encoding.UTF8.GetString(patch.before));
                if (Inventory(scene).Any(item => !originalIds.ContainsKey(Id(item)) && !patch.result.newVisualComponentIds.Contains(Id(item))))
                    throw new InvalidDataException("Reloaded object did not retain its ID or match an approved visual outline: " + patch.path);
                foreach (Image image in controller.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null && !AssetDatabase.Contains(image.sprite)) throw new InvalidDataException("Reloaded preview references a transient sprite: " + image.name);
                patch.result.reloaded = true;
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            if (HashFile(Absolute(patch.path)) != patch.result.afterSha256) throw new IOException("Reload unexpectedly rewrote patched scene: " + patch.path);
        }

        private static string[] Allowed(Object target)
        {
            if (target is RectTransform) return RectFields;
            if (target is Text) return GraphicFields.Concat(new[] { "m_FontData" }).ToArray();
            if (target is Image) return GraphicFields.Concat(ImageFields).ToArray();
            if (target is RawImage) return GraphicFields.Concat(new[] { "m_UVRect" }).ToArray();
            if (target is Selectable) return new[] { "m_Colors", "m_SpriteState", "m_Transition" };
            if (target is WoodButtonRoleState) return ShadowFields.Concat(new[] { "role", "explicitRole" }).ToArray();
            if (target is Shadow) return ShadowFields;
            if (target is GameObject && (target.name == "Material Timber Rim" || target.name.EndsWith(" Backing", StringComparison.Ordinal))) return new[] { "m_IsActive" };
            return Array.Empty<string>();
        }

        private static bool InScope(Object target, Transform routes, Transform setup)
        {
            Transform transform = target is GameObject go ? go.transform : target is Component component ? component.transform : null;
            return transform != null && (transform == routes || transform == setup || transform.IsChildOf(routes) || transform.IsChildOf(setup));
        }

        private static Object[] Inventory(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .SelectMany(transform => new Object[] { transform.gameObject }.Concat(transform.GetComponents<Component>().Cast<Object>())).ToArray();

        private static string NonVisualHash(Snapshot[] snapshots) => Hash(Encoding.UTF8.GetBytes(string.Join("\n", snapshots.Select(snapshot => snapshot.id + ":" + NonVisualJson(snapshot.target, snapshot.allowed)))));

        private static bool HasNonVisualChange(Snapshot snapshot) => snapshot.baseline != NonVisualJson(snapshot.target, snapshot.allowed);

        private static string NonVisualJson(Object target, string[] allowed)
        {
            var serialized = new SerializedObject(target);
            var entries = new List<string>();
            SerializedProperty cursor = serialized.GetIterator();
            while (cursor.Next(true))
            {
                string root = cursor.propertyPath.Split('.')[0];
                if (allowed.Contains(root) || cursor.propertyType == SerializedPropertyType.Generic) continue;
                entries.Add(cursor.propertyPath + "=" + PropertyValue(cursor));
            }
            return string.Join("\n", entries);
        }

        private static string PropertyValue(SerializedProperty value)
        {
            switch (value.propertyType)
            {
                case SerializedPropertyType.Integer: return value.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character: case SerializedPropertyType.LayerMask: return value.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return value.boolValue.ToString();
                case SerializedPropertyType.Float: return value.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return value.stringValue;
                case SerializedPropertyType.ObjectReference: return value.objectReferenceValue == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(value.objectReferenceValue).ToString();
                case SerializedPropertyType.Color: return value.colorValue.ToString("R");
                case SerializedPropertyType.Vector2: return value.vector2Value.ToString("R");
                case SerializedPropertyType.Vector3: return value.vector3Value.ToString("R");
                case SerializedPropertyType.Vector4: return value.vector4Value.ToString("R");
                case SerializedPropertyType.Rect: return value.rectValue.ToString("R");
                case SerializedPropertyType.Bounds: return value.boundsValue.ToString("R");
                case SerializedPropertyType.Quaternion: return value.quaternionValue.ToString("R");
                case SerializedPropertyType.Vector2Int: return value.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int: return value.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt: return value.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt: return value.boundsIntValue.ToString();
                case SerializedPropertyType.Hash128: return value.hash128Value.ToString();
                case SerializedPropertyType.FixedBufferSize: return value.fixedBufferSize.ToString(CultureInfo.InvariantCulture);
                default: throw new InvalidDataException("Unsupported snapshot property: " + value.propertyPath + " / " + value.propertyType);
            }
        }

        private static string YamlNonVisualHash(Dictionary<string, string> blocks, Snapshot[] snapshots)
        {
            var result = new Dictionary<string, string>(blocks);
            foreach (Snapshot snapshot in snapshots)
            {
                string block = result[snapshot.id];
                foreach (string field in snapshot.allowed) block = FieldPattern(field).Replace(block, string.Empty);
                result[snapshot.id] = block;
            }
            return Hash(Encoding.UTF8.GetBytes(string.Concat(result.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value))));
        }

        private static Sprite PersistSprite(Sprite source, List<string> assets)
        {
            Texture2D texture = source.texture;
            string texturePath = texture == null ? string.Empty : AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath)) throw new InvalidDataException("Departure skin sprite must use an imported texture: " + source.name);
            Vector2 pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            string key = AssetDatabase.AssetPathToGUID(texturePath) + "|" + source.rect.ToString("R") + "|" + source.border.ToString("R") + "|" + pivot.ToString("R") + "|" + source.pixelsPerUnit.ToString("R", CultureInfo.InvariantCulture);
            string path = SpriteFolder + "/" + Regex.Replace(source.name, @"[^A-Za-z0-9_-]", "_") + "_" + Hash(Encoding.UTF8.GetBytes(key)).Substring(0, 16) + ".sprite.asset";
            Sprite copy = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (copy == null)
            {
                copy = Sprite.Create(texture, source.rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                copy.name = source.name; copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, path);
            }
            if (copy.texture != texture || copy.rect != source.rect || copy.border != source.border || (copy.pivot - source.pivot).sqrMagnitude > .01f)
                throw new InvalidDataException("Persistent sprite does not match generated source: " + path);
            if (!assets.Contains(path)) assets.Add(path);
            return copy;
        }

        private static string CopyField(string original, string candidate, string field, string path)
        {
            Regex pattern = FieldPattern(field);
            Match source = pattern.Match(original), replacement = pattern.Match(candidate);
            if (!source.Success && !replacement.Success) return original;
            if (!source.Success || !replacement.Success || pattern.Matches(original).Count != 1 || pattern.Matches(candidate).Count != 1)
                throw new InvalidDataException("Ambiguous visual YAML field: " + path + " / " + field);
            return original.Remove(source.Index, source.Length).Insert(source.Index, replacement.Value);
        }
        private static Regex FieldPattern(string field) => new Regex(@"(?ms)^  " + Regex.Escape(field) + @":.*?(?=^  [A-Za-z_][A-Za-z_0-9]*:|\z)");
        private static string ReadField(string block, string field) => FieldPattern(field).Match(block).Value;
        private static string[] ComponentReferences(string block) => Regex.Matches(ReadField(block, "m_Component"), @"component: \{fileID: (-?\d+)\}")
            .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
        private static Dictionary<string, string> Parse(string text) => Blocks.Matches(text).Cast<Match>().ToDictionary(match => match.Groups[1].Value, match => match.Value);
        private static string Id(Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).targetObjectId.ToString(CultureInfo.InvariantCulture);
        private static string Absolute(string relative) => Path.GetFullPath(Path.Combine(IsolatedProject, relative.Replace('/', Path.DirectorySeparatorChar)));
        private static string Relative(string absolute) => Path.GetFullPath(absolute).Substring(Path.GetFullPath(IsolatedProject).Length + 1).Replace('\\', '/');
        private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
        private static string Hash(byte[] value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }

        private static void Remember(string path, Dictionary<string, string> originals, bool requireMatch)
        {
            string source = Absolute(path);
            if (!File.Exists(source)) throw new FileNotFoundException("Missing isolated source", source);
            string hash = HashFile(source);
            string production = Path.Combine(ProductionProject, path.Replace('/', Path.DirectorySeparatorChar));
            if (requireMatch && (!File.Exists(production) || HashFile(production) != hash)) throw new IOException("Isolated baseline differs from current production: " + path);
            originals[path] = hash;
        }
        private static void RememberGenerated(string path, Dictionary<string, string> originals)
        {
            // Reuse deterministic assets left by a failed isolated preparation.
            // They are still new files for copy-back unless production owns them.
            string production = Path.Combine(ProductionProject, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(production)) Remember(path, originals, true);
        }
        private static void Backup(string path, string output)
        {
            string destination = Path.Combine(output, "Before", path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(Absolute(path), destination, false);
        }
        private static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/'); string current = parts[0];
            for (int index = 1; index < parts.Length; index++) { string child = current + "/" + parts[index]; if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, parts[index]); current = child; }
        }
        private static string OutputPath()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-departureStructureSyncPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("An absolute -departureStructureSyncPath is required.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.StartsWith(Path.GetFullPath("D:/Game/Artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Sync output must be under D:/Game/Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Sync output must be new or empty.");
            Directory.CreateDirectory(output); return output;
        }
        private static void WriteDiff(string output, IEnumerable<Patch> patches)
        {
            var diff = new StringBuilder();
            foreach (Patch patch in patches)
            {
                diff.AppendLine("--- Before/" + patch.path).AppendLine("+++ After/" + patch.path);
                foreach (FieldChange change in patch.result.changes)
                {
                    diff.AppendLine("@@ object " + change.objectId + " " + change.objectName + " (" + change.type + ") / " + change.field + " @@");
                    foreach (string line in change.before.TrimEnd('\r', '\n').Split('\n')) diff.Append('-').AppendLine(line.TrimEnd('\r'));
                    foreach (string line in change.after.TrimEnd('\r', '\n').Split('\n')) diff.Append('+').AppendLine(line.TrimEnd('\r'));
                }
            }
            File.WriteAllText(Path.Combine(output, "visual-fields.diff"), diff.ToString(), new UTF8Encoding(false));
        }
    }
}
