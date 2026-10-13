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
    /// <summary>Copies presentation changes into authored voyages without rebuilding their gameplay samples.</summary>
    public static class NavigationInterfacePreviewSceneSync
    {
        private const string SpriteFolder = "Assets/_Project/Presentation/EditablePreviewAssets/NavigationUIV3";
        private static readonly string[] Roots = { "Voyage Header", "Navigation Chart", "Navigation Controls", "Forward View Panel" };
        private static readonly Regex Blocks = new Regex(@"(?ms)^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
        private static readonly string[] RectFields = { "m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_Children",
            "m_AnchorMin", "m_AnchorMax", "m_AnchoredPosition", "m_SizeDelta", "m_Pivot" };
        private static readonly string[] ImageFields = { "m_Enabled", "m_Color", "m_RaycastTarget", "m_Sprite", "m_Type",
            "m_PreserveAspect", "m_FillCenter", "m_PixelsPerUnitMultiplier" };
        private static readonly string[] TextFields = { "m_Enabled", "m_Color", "m_RaycastTarget", "m_FontData" };

        // -executeMethod ShatteredSail.Editor.NavigationInterfacePreviewSceneSync.SyncFromCommandLine
        // Required: -navigationInterfaceOutput <fresh absolute directory>.
        public static void SyncFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navigationInterfaceOutput");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("-navigationInterfaceOutput requires an absolute output directory.");
            Sync(Path.GetFullPath(args[index + 1]));
        }

        public static void Sync(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Navigation interface synchronization requires stopped edit mode.");
            output = Path.GetFullPath(output);
            string backupRoot = Path.Combine(output, "scene-backup");
            if (Directory.Exists(backupRoot)) throw new IOException("Use a fresh backup directory: " + backupRoot);
            foreach (string resource in new[] { NavigationHeaderSkin.HeaderResourcePath, NavigationHeaderSkin.CompassResourcePath })
                if (Resources.Load<Texture2D>(resource) == null) throw new InvalidDataException("Missing navigation artwork: " + resource);
            string[] folders = { GameplayPreviewSceneBuilder.SceneFolder };
            string[] paths = folders.Where(AssetDatabase.IsValidFolder)
                .SelectMany(folder => AssetDatabase.FindAssets("t:Scene", new[] { folder }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var report = new Report { backupRoot = backupRoot, scannedScenes = paths.Length };
            var patches = new List<Patch>();
            Directory.CreateDirectory(backupRoot);
            EnsureAssetFolder(SpriteFolder);
            try
            {
                foreach (string path in paths)
                {
                    byte[] before = File.ReadAllBytes(Absolute(path));
                    string text = Encoding.UTF8.GetString(before);
                    if (Roots.Any(name => !text.Contains("m_Name: " + name))) continue;
                    Scene loaded = SceneManager.GetSceneByPath(path);
                    if (loaded.IsValid() && loaded.isLoaded)
                        throw new InvalidOperationException("Close the authored scene before synchronizing: " + path);
                    string backup = Path.Combine(backupRoot, path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.WriteAllBytes(backup, before);
                    if (File.Exists(Absolute(path) + ".meta")) File.Copy(Absolute(path) + ".meta", backup + ".meta");
                    patches.Add(Prepare(path, before, report.spriteAssets));
                }
                if (patches.Count == 0) throw new InvalidDataException("No authored navigation dashboards found.");
                // Preparation temporarily serializes each scene only to assign new
                // object IDs, then restores its exact original bytes in finally.
                // Validate the complete set before publishing any migration.
                foreach (Patch patch in patches)
                    if (!File.ReadAllBytes(Absolute(patch.path)).SequenceEqual(patch.before))
                        throw new IOException("Authored scene changed during preparation: " + patch.path);
                AssetDatabase.SaveAssets();
                foreach (Patch patch in patches)
                {
                    File.WriteAllBytes(Absolute(patch.path), patch.after);
                    if (!File.ReadAllBytes(Absolute(patch.path)).SequenceEqual(patch.after))
                        throw new IOException("Navigation preview write verification failed: " + patch.path);
                    report.scenes.Add(patch.result);
                    if (patch.result.updated) report.updatedScenes++;
                }
                report.matchedScenes = patches.Count;
                report.complete = true;
            }
            catch (Exception failure)
            {
                // An incomplete migration never leaves a mixture of old/new scenes.
                foreach (Patch patch in patches) File.WriteAllBytes(Absolute(patch.path), patch.before);
                report.failure = failure.ToString();
                throw;
            }
            finally
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "scene-sync.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                AssetDatabase.Refresh();
            }
            Debug.Log("NAVIGATION_INTERFACE_PREVIEW_SYNC_OK: " + report.updatedScenes + " authored scenes updated; report=" +
                Path.Combine(output, "scene-sync.json"));
        }

        private static Patch Prepare(string path, byte[] before, List<string> spriteAssets)
        {
            string originalText = Encoding.UTF8.GetString(before);
            Dictionary<string, string> original = Parse(originalText);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                RectTransform[] contents = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name == "Phase Content" && Roots.All(name => Direct(rect, name) != null)).ToArray();
                if (contents.Length == 0) throw new InvalidDataException("Dashboard hierarchy does not match its saved names: " + path);
                var textEdits = new HashSet<Text>();
                foreach (RectTransform content in contents)
                {
                    // Only the disposable in-memory Canvas uses reference sizing;
                    // its serialized Canvas/Scaler/root blocks remain byte-identical.
                    Canvas canvas = content.GetComponentInParent<Canvas>();
                    CanvasScaler scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
                    if (scaler == null || scaler.referenceResolution.x <= 0f || scaler.referenceResolution.y <= 0f)
                        throw new InvalidDataException("Missing reference Canvas: " + path);
                    scaler.enabled = false;
                    canvas.renderMode = RenderMode.WorldSpace;
                    RectTransform canvasRect = (RectTransform)canvas.transform;
                    canvasRect.localScale = Vector3.one;
                    canvasRect.sizeDelta = scaler.referenceResolution;
                    canvasRect.ForceUpdateRectTransforms();
                    NavigationHeaderSkin.Apply(Direct(content, "Voyage Header"));
                    NavigationDashboardLayout.Attach(content);
                    content.ForceUpdateRectTransforms();
                    Direct(content, "Navigation Dashboard Layout").GetComponent<NavigationDashboardLayout>().Apply();
                    FormatAuthoredReadouts(content, textEdits);
                }
                // Some ExecuteAlways layout callbacks reset runtime sprites. Complete
                // layout before converting them, then suppress callbacks during save.
                Canvas.ForceUpdateCanvases();
                var allowed = new Dictionary<Object, string[]>();
                foreach (RectTransform content in contents)
                {
                    allowed[content] = RectFields;
                    foreach (string rootName in Roots.Concat(new[] { "Navigation Dashboard Layout" }))
                    {
                        RectTransform root = Direct(content, rootName);
                        if (root == null) continue;
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        {
                            allowed[child.gameObject] = child.name == "Material Timber Rim"
                                ? new[] { "m_Component", "m_IsActive" } : new[] { "m_Component" };
                            foreach (Component component in child.GetComponents<Component>())
                            {
                                if (component == null) throw new InvalidDataException("Missing script under dashboard: " + path);
                                allowed[component] = AllowedFields(component, textEdits);
                            }
                        }
                    }
                }
                var temporaryDisabled = new Dictionary<Behaviour, bool>();
                foreach (Behaviour behaviour in allowed.Keys.OfType<Behaviour>().Where(value =>
                    value is NavigationDashboardLayout || value is NavigationCompassControl))
                {
                    temporaryDisabled[behaviour] = behaviour.enabled;
                    behaviour.enabled = false;
                }
                foreach (Image image in allowed.Keys.OfType<Image>().ToArray())
                {
                    if (image.sprite == null || AssetDatabase.Contains(image.sprite)) continue;
                    image.sprite = PersistSprite(image.sprite, spriteAssets);
                    image.overrideSprite = null;
                }
                // Save invokes no controller/session rebuild. Existing gameplay
                // component blocks are never copied into the final YAML below.
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not serialize navigation preview: " + path);
                Dictionary<string, string> candidate = Parse(File.ReadAllText(Absolute(path)));
                foreach (KeyValuePair<Behaviour, bool> pair in temporaryDisabled)
                {
                    string id = Id(pair.Key);
                    // New compass behaviours are serialized enabled; suppression
                    // applies only to the temporary editor scene used for migration.
                    candidate[id] = Regex.Replace(candidate[id], @"(?m)^  m_Enabled: [01]", "  m_Enabled: " + (pair.Value ? "1" : "0"));
                }
                foreach (Image image in allowed.Keys.OfType<Image>())
                    if (image.sprite != null && !AssetDatabase.Contains(image.sprite))
                        throw new InvalidDataException("Runtime sprite was not persisted before serialization: " + image.name + " in " + path);
                if (original.Keys.Any(id => !candidate.ContainsKey(id)))
                    throw new InvalidDataException("A saved authored object was removed during presentation migration: " + path);
                var changes = new Dictionary<string, string>();
                var additions = new HashSet<string>();
                foreach (KeyValuePair<Object, string[]> entry in allowed)
                {
                    string id = Id(entry.Key);
                    if (id == "0" || !candidate.ContainsKey(id))
                        throw new InvalidDataException("Unserialized dashboard object: " + entry.Key.name + " in " + path);
                    if (!original.TryGetValue(id, out string old))
                    {
                        AssertNewObject(entry.Key, path);
                        additions.Add(id);
                        continue;
                    }
                    string changed = old;
                    foreach (string field in entry.Value) changed = CopyField(changed, candidate[id], field, path);
                    if (changed != old) changes[id] = changed;
                }
                string afterText = Blocks.Replace(originalText, match => changes.TryGetValue(match.Groups[1].Value, out string change) ? change : match.Value);
                afterText += string.Concat(additions.OrderBy(id => long.Parse(id, CultureInfo.InvariantCulture)).Select(id => candidate[id]));
                Dictionary<string, string> final = Parse(afterText);
                if (final.Count != original.Count + additions.Count || original.Any(pair =>
                    final[pair.Key] != (changes.TryGetValue(pair.Key, out string change) ? change : pair.Value)))
                    throw new InvalidDataException("Unexpected mutation of retained authored objects: " + path);
                foreach (string block in changes.Values.Concat(additions.Select(id => final[id])))
                    foreach (Match reference in Regex.Matches(block, @"\{fileID: (-?\d+)\}"))
                        if (reference.Groups[1].Value != "0" && !final.ContainsKey(reference.Groups[1].Value))
                            throw new InvalidDataException("Dangling local reference " + reference.Value + " in " + path);
                byte[] after = new UTF8Encoding(false).GetBytes(afterText);
                return new Patch { path = path, before = before, after = after, result = new SceneResult
                {
                    path = path, updated = !before.SequenceEqual(after), dashboards = contents.Length,
                    changedUiBlocks = changes.Count, addedUiBlocks = additions.Count, formattedReadouts = textEdits.Count,
                    beforeSha256 = Hash(before), afterSha256 = Hash(after),
                    metaSha256 = File.Exists(Absolute(path) + ".meta") ? Hash(File.ReadAllBytes(Absolute(path) + ".meta")) : "",
                } };
            }
            finally
            {
                try
                {
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
                finally { File.WriteAllBytes(Absolute(path), before); }
            }
        }

        private static string[] AllowedFields(Component component, HashSet<Text> textEdits)
        {
            if (component is RectTransform) return RectFields;
            if (component is Text text) return textEdits.Contains(text) ? TextFields.Concat(new[] { "m_Text" }).ToArray() : TextFields;
            if (component is Image) return ImageFields;
            if (component is RawImage) return new[] { "m_Enabled", "m_Color", "m_RaycastTarget", "m_UVRect" };
            if (component is Button) return new[] { "m_Navigation", "m_Transition", "m_TargetGraphic", "m_Colors" };
            if (component is ScrollRect) return new[] { "m_Vertical" };
            if (component is WoodButtonRoleState) return new[] { "m_Enabled", "m_EffectColor", "m_EffectDistance", "m_UseGraphicAlpha", "role", "explicitRole" };
            if (component is Outline) return new[] { "m_Enabled", "m_EffectColor", "m_EffectDistance", "m_UseGraphicAlpha" };
            if (component is NauticalButtonMotion) return new[] { "m_Enabled" };
            if (component is NavigationCompassControl) return new[] { "compassCenter", "compassDiameter" };
            return Array.Empty<string>();
        }

        private static void AssertNewObject(Object value, string path)
        {
            if (value is GameObject || value is RectTransform || value is CanvasRenderer || value is Image ||
                value is RawImage || value is Outline || value is NavigationCompassControl || value is NavigationDashboardLayout) return;
            throw new InvalidDataException("Unexpected new component " + value.GetType().Name + " in " + path);
        }

        private static void FormatAuthoredReadouts(RectTransform content, HashSet<Text> changed)
        {
            Text title = Direct(Direct(content, "Navigation Chart"), "Map Title")?.GetComponent<Text>();
            if (title != null)
            {
                Match route = Regex.Match(title.text, @"\A[^\r\n·]+ · (?<route>[^\r\n→]+ → [^\r\n→]+)\z");
                if (route.Success) { title.text = route.Groups["route"].Value; changed.Add(title); }
                title.fontSize = 23;
            }
            RectTransform controls = Direct(content, "Navigation Controls");
            Text forecast = Direct(controls, "Forecast")?.GetComponent<Text>();
            if (forecast != null)
            {
                Match parsed = Regex.Match(forecast.text,
                    @"\A预计 (?<hours>\d+(?:\.\d+)?) 小时[ \u3000]*·[ \u3000]*航速 (?<speed>\d+(?:\.\d+)?) 节\n" +
                    @"(?<supply>淡水 [−-]\d+[ \u3000]+食物 [−-]\d+)(?<reroute>[ \u3000]*·[ \u3000]*已绕岛)?(?<tail>(?:\n[^\r\n]*)*)\z");
                if (parsed.Success)
                {
                    forecast.text = "预计 " + parsed.Groups["hours"].Value + " 小时\n航速 " + parsed.Groups["speed"].Value +
                        " 节" + (parsed.Groups["reroute"].Success ? " · 已绕岛" : "") + "\n" +
                        parsed.Groups["supply"].Value + parsed.Groups["tail"].Value;
                    changed.Add(forecast);
                }
                forecast.fontSize = 20;
            }
            Text heading = content.GetComponentsInChildren<Text>(true).SingleOrDefault(text => text.name == "Lookout Heading");
            Text report = content.GetComponentsInChildren<Text>(true).SingleOrDefault(text => text.name == "Lookout Report");
            if (heading != null) heading.fontSize = 27;
            if (report != null) report.fontSize = 22;
        }

        private static Sprite PersistSprite(Sprite source, List<string> assets)
        {
            Texture2D texture = source.texture;
            string texturePath = texture == null ? "" : AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath)) throw new InvalidDataException("A new navigation sprite must reference an imported texture: " + source.name);
            Vector2 pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            string key = AssetDatabase.AssetPathToGUID(texturePath) + "|" + source.rect.ToString("R") + "|" +
                source.border.ToString("R") + "|" + pivot.ToString("R") + "|" + source.pixelsPerUnit.ToString("R", CultureInfo.InvariantCulture);
            string assetPath = SpriteFolder + "/" + Regex.Replace(source.name, @"[^A-Za-z0-9_-]", "_") + "_" +
                Hash(Encoding.UTF8.GetBytes(key)).Substring(0, 16) + ".sprite.asset";
            Sprite copy = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (copy == null)
            {
                copy = Sprite.Create(texture, source.rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
                copy.name = source.name;
                copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, assetPath);
            }
            if (copy.texture != texture || copy.rect != source.rect || copy.border != source.border)
                throw new InvalidDataException("Persistent navigation sprite does not match its source: " + assetPath);
            if (!assets.Contains(assetPath)) assets.Add(assetPath);
            return copy;
        }

        private static string CopyField(string original, string candidate, string field, string path)
        {
            var expression = new Regex(@"(?ms)^  " + Regex.Escape(field) + @":.*?(?=^  [A-Za-z_][A-Za-z_0-9]*:|\z)");
            Match source = expression.Match(original), replacement = expression.Match(candidate);
            if (!source.Success && !replacement.Success) return original;
            if (!source.Success || !replacement.Success || expression.Matches(original).Count != 1 || expression.Matches(candidate).Count != 1)
                throw new InvalidDataException("Ambiguous UI field " + field + " in " + path);
            return original.Remove(source.Index, source.Length).Insert(source.Index, replacement.Value);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string child = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, parts[index]);
                current = child;
            }
        }
        private static RectTransform Direct(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform[] matches = parent.Cast<Transform>().Where(child => child.name == name).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("Ambiguous dashboard child: " + name);
            return matches.Length == 0 ? null : matches[0] as RectTransform;
        }
        private static Dictionary<string, string> Parse(string source) => Blocks.Matches(source).Cast<Match>()
            .ToDictionary(match => match.Groups[1].Value, match => match.Value);
        private static string Id(Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).targetObjectId.ToString(CultureInfo.InvariantCulture);
        private static string Absolute(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        private static string Hash(byte[] bytes)
        { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private sealed class Patch { internal string path; internal byte[] before, after; internal SceneResult result; }
        [Serializable] private sealed class Report
        {
            public string backupRoot, failure;
            public bool complete;
            public int scannedScenes, matchedScenes, updatedScenes;
            public string preserved = "All existing object IDs, non-UI serialized blocks, gameplay state, event text, choices and callbacks are retained. Only listed visual fields and new dashboard decoration/compass components are merged; route prefixes and recognized forecast formatting are updated without changing values.";
            public List<string> spriteAssets = new List<string>();
            public List<SceneResult> scenes = new List<SceneResult>();
        }
        [Serializable] private sealed class SceneResult
        {
            public string path, beforeSha256, afterSha256, metaSha256;
            public bool updated;
            public int dashboards, changedUiBlocks, addedUiBlocks, formattedReadouts;
        }
    }
}
