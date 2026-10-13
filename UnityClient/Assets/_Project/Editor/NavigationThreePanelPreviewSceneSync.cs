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

namespace ShatteredSail.Editor
{
    /// <summary>Updates saved dashboard geometry without rebuilding authored voyage samples.</summary>
    public static class NavigationThreePanelPreviewSceneSync
    {
        private static readonly string[] PanelNames = { "Navigation Chart", "Forward View Panel", "Navigation Controls" };
        private static readonly Regex Blocks = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");

        // -executeMethod ShatteredSail.Editor.NavigationThreePanelPreviewSceneSync.SyncFromCommandLine
        // Optional: -navigationThreePanelBackupPath and -navigationThreePanelReportPath.
        public static void SyncFromCommandLine()
        {
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/NavigationThreePanel20261011"));
            string backupRoot = CommandLinePath("-navigationThreePanelBackupPath", Path.Combine(artifactRoot, "scene-backup"));
            string reportPath = CommandLinePath("-navigationThreePanelReportPath", Path.Combine(artifactRoot, "scene-sync.json"));
            Sync(backupRoot, reportPath);
        }

        /// <summary>Shared migration used by current and legacy command-line entry points.</summary>
        public static void Sync(string backupRoot, string reportPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Three-panel preview synchronization requires stopped edit mode.");
            if (string.IsNullOrWhiteSpace(backupRoot) || string.IsNullOrWhiteSpace(reportPath))
                throw new ArgumentException("Provide a backup directory and report path for preview synchronization.");
            backupRoot = Path.GetFullPath(backupRoot);
            reportPath = Path.GetFullPath(reportPath);
            string[] paths = new[] { GameplayPreviewSceneBuilder.SceneFolder }
                .SelectMany(folder => Directory.GetFiles(AbsolutePath(folder), "*.unity", SearchOption.AllDirectories)
                    .Select(path => folder + "/" + path.Substring(AbsolutePath(folder).Length + 1).Replace('\\', '/')))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Patch[] patches = paths.Select(PreparePatch).Where(patch => patch != null).ToArray();
            if (patches.Length == 0)
                throw new InvalidOperationException("No authored navigation dashboards found; no scenes were written.");

            // Validate every candidate before writing any scene. All retained
            // object IDs and non-layout properties continue to use the original YAML.
            foreach (Patch patch in patches)
                if (!File.ReadAllBytes(patch.Absolute).SequenceEqual(patch.BeforeBytes))
                    throw new IOException("Authored dashboard changed during preparation: " + patch.Path);
            foreach (Patch patch in patches.Where(patch => patch.Before != patch.After))
            {
                string backup = Path.Combine(backupRoot, patch.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.WriteAllBytes(backup, patch.BeforeBytes);
                if (File.Exists(patch.Absolute + ".meta") && !File.Exists(backup + ".meta"))
                    File.Copy(patch.Absolute + ".meta", backup + ".meta");
            }
            var report = new SyncReport
            {
                scannedScenes = paths.Length, matchedScenes = patches.Length, backupRoot = backupRoot,
                preserved = "Retained authored text, artwork, choices, gameplay values and asset GUIDs are preserved. Only dashboard geometry, panorama UVs and Navigation Journal subtree removal are serialized.",
            };
            foreach (Patch patch in patches)
            {
                byte[] afterBytes = Encoding.UTF8.GetBytes(patch.After);
                bool changed = patch.Before != patch.After;
                if (changed)
                {
                    File.WriteAllBytes(patch.Absolute, afterBytes);
                    if (!File.ReadAllBytes(patch.Absolute).SequenceEqual(afterBytes))
                        throw new IOException("Three-panel preview write verification failed: " + patch.Path);
                    report.updatedScenes++;
                }
                report.scenes.Add(new SceneResult
                {
                    path = patch.Path, updated = changed, dashboards = patch.Dashboards,
                    removedJournals = patch.RemovedJournals, removedSerializedObjects = patch.RemovedObjects,
                    changedLayoutBlocks = patch.ChangedBlocks, beforeSha256 = Hash(patch.BeforeBytes),
                    afterSha256 = Hash(afterBytes), metaSha256 = File.Exists(patch.Absolute + ".meta")
                        ? Hash(File.ReadAllBytes(patch.Absolute + ".meta")) : "",
                });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("NAVIGATION_THREE_PANEL_PREVIEW_SYNC_OK: updated " + report.updatedScenes +
                " authored scenes; journal removed and three panels fitted; report=" + reportPath);
        }

        private static Patch PreparePatch(string path)
        {
            string absolute = AbsolutePath(path);
            byte[] beforeBytes = File.ReadAllBytes(absolute);
            string before = new UTF8Encoding(false, true).GetString(beforeBytes);
            if (PanelNames.Any(name => !before.Contains("m_Name: " + name))) return null;
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("Refusing to replace unsaved authored edits in " + path);
            Dictionary<string, string> blocks = Blocks.Matches(before).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            var edits = new Dictionary<string, string>();
            var removedIds = new HashSet<string>();
            int dashboards = 0, removedJournals = 0;
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(path);
                RectTransform[] contents = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name == "Phase Content" && PanelNames.All(name => DirectChild(rect, name) != null)).ToArray();
                dashboards = contents.Length;
                foreach (RectTransform content in contents)
                {
                    // Overlay Canvas dimensions can be zero in batch mode. Use
                    // its authored reference resolution only inside this disposable
                    // preview; the saved Canvas and CanvasScaler blocks stay intact.
                    Canvas canvas = content.GetComponentInParent<Canvas>();
                    CanvasScaler scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
                    if (canvas == null || scaler == null || scaler.referenceResolution.x <= 0 || scaler.referenceResolution.y <= 0)
                        throw new InvalidDataException("Missing reference Canvas for dashboard in " + path);
                    scaler.enabled = false;
                    canvas.renderMode = RenderMode.WorldSpace;
                    RectTransform canvasRect = (RectTransform)canvas.transform;
                    canvasRect.localScale = Vector3.one;
                    canvasRect.sizeDelta = scaler.referenceResolution;
                    canvasRect.ForceUpdateRectTransforms();

                    RectTransform journal = DirectChild(content, "Navigation Journal");
                    if (journal != null)
                    {
                        string parentId = ObjectId(content, blocks, path);
                        string journalId = ObjectId(journal, blocks, path);
                        string parentBlock = edits.TryGetValue(parentId, out string existing) ? existing : blocks[parentId];
                        var childReference = new Regex(@"(?m)^  - \{fileID: " + Regex.Escape(journalId) + @"\}\r?\n");
                        if (childReference.Matches(parentBlock).Count != 1)
                            throw new InvalidDataException("Expected one Journal reference in dashboard " + path);
                        edits[parentId] = childReference.Replace(parentBlock, "");
                        foreach (Transform child in journal.GetComponentsInChildren<Transform>(true))
                        {
                            removedIds.Add(ObjectId(child.gameObject, blocks, path));
                            foreach (Component component in child.GetComponents<Component>())
                                removedIds.Add(ObjectId(component, blocks, path));
                        }
                        UnityEngine.Object.DestroyImmediate(journal.gameObject);
                        removedJournals++;
                    }
                    NavigationDashboardLayout[] layouts = content.GetComponentsInChildren<NavigationDashboardLayout>(true)
                        .Where(layout => layout.transform.parent == content).ToArray();
                    if (layouts.Length != 1)
                        throw new InvalidDataException("Expected one authored dashboard layout in " + path);
                    NavigationDashboardLayout.Attach(content);
                    content.ForceUpdateRectTransforms();
                    layouts[0].Apply();
                    AssertThreePanels(content, path);
                    foreach (RectTransform rect in content.GetComponentsInChildren<RectTransform>(true))
                    {
                        string id = ObjectId(rect, blocks, path);
                        string block = edits.TryGetValue(id, out string existing) ? existing : blocks[id];
                        block = VectorField(block, "m_LocalPosition", rect.localPosition, path);
                        block = VectorField(block, "m_LocalScale", rect.localScale, path);
                        block = VectorField(block, "m_AnchorMin", rect.anchorMin, path);
                        block = VectorField(block, "m_AnchorMax", rect.anchorMax, path);
                        block = VectorField(block, "m_AnchoredPosition", rect.anchoredPosition, path);
                        block = VectorField(block, "m_SizeDelta", rect.sizeDelta, path);
                        block = VectorField(block, "m_Pivot", rect.pivot, path);
                        if (block != blocks[id]) edits[id] = block;
                    }
                    foreach (string name in new[] { "Lookout Ocean", "Lookout Ocean Transition" })
                    {
                        RawImage panorama = content.GetComponentsInChildren<RawImage>(true).SingleOrDefault(image => image.name == name);
                        if (panorama == null) continue;
                        string id = ObjectId(panorama, blocks, path);
                        string block = RectField(blocks[id], "m_UVRect", panorama.uvRect, path);
                        if (block != blocks[id]) edits[id] = block;
                    }
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
            if (dashboards == 0) return null;
            string after = Blocks.Replace(before, match =>
            {
                string id = match.Groups[2].Value;
                return removedIds.Contains(id) ? "" : edits.TryGetValue(id, out string edit) ? edit : match.Value;
            });
            foreach (string id in removedIds)
                if (Regex.IsMatch(after, @"\{fileID: " + Regex.Escape(id) + @"(?:[,}])"))
                    throw new InvalidDataException("Retained object references removed Journal object " + id + " in " + path);
            Dictionary<string, string> retained = Blocks.Matches(after).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            if (retained.Count != blocks.Count - removedIds.Count || blocks.Any(pair => !removedIds.Contains(pair.Key) &&
                (!retained.TryGetValue(pair.Key, out string block) || block != (edits.TryGetValue(pair.Key, out string edit) ? edit : pair.Value))))
                throw new InvalidDataException("Retained authored dashboard blocks changed unexpectedly in " + path);
            return new Patch
            {
                Path = path, Absolute = absolute, BeforeBytes = beforeBytes, Before = before, After = after,
                Dashboards = dashboards, RemovedJournals = removedJournals, RemovedObjects = removedIds.Count, ChangedBlocks = edits.Count,
            };
        }

        private static void AssertThreePanels(RectTransform content, string path)
        {
            if (DirectChild(content, "Navigation Journal") != null)
                throw new InvalidDataException("Journal remains in migrated dashboard " + path);
            RectTransform chart = DirectChild(content, PanelNames[0]);
            RectTransform lookout = DirectChild(content, PanelNames[1]);
            RectTransform controls = DirectChild(content, PanelNames[2]);
            RectTransform header = DirectChild(content, "Voyage Header");
            Rect body = content.rect;
            body.height -= header == null ? PhaseSurfaceLayout.HeaderHeight : header.rect.height;
            Rect chartBox = PanelRect(content, chart), lookoutBox = PanelRect(content, lookout), controlsBox = PanelRect(content, controls);
            float tolerance = .1f;
            bool close(float a, float b) => Mathf.Abs(a - b) < tolerance;
            if (!close(chartBox.xMin, body.xMin) || !close(chartBox.yMin, body.yMin) || !close(chartBox.yMax, body.yMax) ||
                !close(chartBox.xMax, lookoutBox.xMin) || !close(chartBox.xMax, controlsBox.xMin) ||
                !close(lookoutBox.xMax, body.xMax) || !close(controlsBox.xMax, body.xMax) ||
                !close(lookoutBox.yMax, body.yMax) || !close(controlsBox.yMin, body.yMin) || !close(controlsBox.yMax, lookoutBox.yMin))
                throw new InvalidDataException("Three panels do not tile the body in " + path + "; run with the current runtime layout.");
        }

        private static Rect PanelRect(RectTransform content, RectTransform panel)
        {
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector3 lower = content.InverseTransformPoint(corners[0]);
            Vector3 upper = content.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        }

        private static RectTransform DirectChild(Transform parent, string name)
        {
            Transform[] children = parent.Cast<Transform>().Where(child => child.name == name).ToArray();
            if (children.Length > 1) throw new InvalidDataException("Ambiguous dashboard child " + name);
            return children.Length == 0 ? null : children[0] as RectTransform;
        }

        private static string VectorField(string block, string name, Vector2 value, string path) =>
            ValuesField(block, name, new[] { "x", "y" }, new[] { value.x, value.y }, path);
        private static string VectorField(string block, string name, Vector3 value, string path) =>
            ValuesField(block, name, new[] { "x", "y", "z" }, new[] { value.x, value.y, value.z }, path);
        private static string RectField(string block, string name, Rect value, string path)
        {
            var field = new Regex(@"(?m)^  " + Regex.Escape(name) + @":\r?\n    serializedVersion: 2\r?\n    x: [^\r\n]+\r?\n    y: [^\r\n]+\r?\n    width: [^\r\n]+\r?\n    height: [^\r\n]+");
            Match match = field.Match(block);
            if (!match.Success || field.Matches(block).Count != 1)
                throw new InvalidDataException("Invalid rectangle field " + name + " in " + path);
            string changed = match.Value;
            string[] axes = { "x", "y", "width", "height" };
            float[] values = { value.x, value.y, value.width, value.height };
            for (int i = 0; i < axes.Length; i++)
            {
                var axis = new Regex(@"(?m)^(?<prefix>    " + axes[i] + @": )(?<value>[^\r\n]+)");
                Match current = axis.Match(changed);
                if (!float.TryParse(current.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float previous) ||
                    float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    throw new InvalidDataException("Invalid rectangle coordinate " + name + " in " + path);
                if (Mathf.Abs(previous - values[i]) > .00001f)
                    changed = changed.Remove(current.Index, current.Length).Insert(current.Index,
                        current.Groups["prefix"].Value + values[i].ToString("R", CultureInfo.InvariantCulture));
            }
            return block.Remove(match.Index, match.Length).Insert(match.Index, changed);
        }

        private static string ValuesField(string block, string name, string[] axes, float[] values, string path)
        {
            var field = new Regex(@"(?m)^(?<prefix>  " + Regex.Escape(name) + @": \{)(?<value>[^}\r\n]*)(?<suffix>\})");
            Match match = field.Match(block);
            if (!match.Success || field.Matches(block).Count != 1 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("Invalid layout field " + name + " in " + path);
            string[] parts = match.Groups["value"].Value.Split(',');
            bool equal = parts.Length == axes.Length;
            for (int i = 0; equal && i < axes.Length; i++)
            {
                string[] pair = parts[i].Trim().Split(':');
                equal = pair.Length == 2 && pair[0] == axes[i] && float.TryParse(pair[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float previous) && Mathf.Abs(previous - values[i]) <= .00001f;
            }
            if (equal) return block;
            string replacement = match.Groups["prefix"].Value + string.Join(", ", axes.Select((axis, i) =>
                axis + ": " + values[i].ToString("R", CultureInfo.InvariantCulture))) + match.Groups["suffix"].Value;
            return block.Remove(match.Index, match.Length).Insert(match.Index, replacement);
        }

        private static string ObjectId(UnityEngine.Object item, Dictionary<string, string> blocks, string path)
        {
            if (item == null) throw new InvalidDataException("Missing authored dashboard component in " + path);
            string id = GlobalObjectId.GetGlobalObjectIdSlow(item).targetObjectId.ToString(CultureInfo.InvariantCulture);
            if (!blocks.ContainsKey(id))
                throw new InvalidDataException("Missing serialized dashboard object " + item.name + " (" + id + ") in " + path);
            return id;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string AbsolutePath(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            path.Replace('/', Path.DirectorySeparatorChar));
        private static string CommandLinePath(string option, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, option);
            if (index < 0) return Path.GetFullPath(fallback);
            if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]) || arguments[index + 1].StartsWith("-"))
                throw new ArgumentException("Missing path after " + option);
            return Path.GetFullPath(arguments[index + 1]);
        }

        private sealed class Patch
        {
            public string Path, Absolute, Before, After;
            public byte[] BeforeBytes;
            public int Dashboards, RemovedJournals, RemovedObjects, ChangedBlocks;
        }
        [Serializable]
        private sealed class SyncReport
        {
            public int scannedScenes, matchedScenes, updatedScenes;
            public string backupRoot, preserved;
            public List<SceneResult> scenes = new List<SceneResult>();
        }
        [Serializable]
        private sealed class SceneResult
        {
            public string path, beforeSha256, afterSha256, metaSha256;
            public bool updated;
            public int dashboards, removedJournals, removedSerializedObjects, changedLayoutBlocks;
        }
    }
}
