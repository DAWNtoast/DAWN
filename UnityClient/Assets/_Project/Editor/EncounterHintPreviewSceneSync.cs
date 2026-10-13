using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Retire only the two redundant labels on saved embedded encounter cards.</summary>
    public static class EncounterHintPreviewSceneSync
    {
        private static readonly string[] HintNames = { "Encounter Journal Heading", "Encounter Terrain" };
        private static readonly Regex BlockPattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.EncounterHintPreviewSceneSync.SyncFromCommandLine
        /// Optional: -encounterHintBackupPath and -encounterHintReportPath.
        /// Existing sample states and retained YAML blocks are never rebuilt or resaved.
        /// </summary>
        public static void SyncFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Encounter hint synchronization requires stopped edit mode.");

            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/EncounterHintClean20261011"));
            string backupRoot = CommandLinePath("-encounterHintBackupPath", Path.Combine(artifactRoot, "scene-backup"));
            string reportPath = CommandLinePath("-encounterHintReportPath", Path.Combine(artifactRoot, "scene-sync.json"));
            string[] paths = new[] { GameplayPreviewSceneBuilder.SceneFolder }
                .SelectMany(folder => Directory.GetFiles(AbsolutePath(folder), "*.unity", SearchOption.AllDirectories)
                    .Select(path => folder + "/" + path.Substring(AbsolutePath(folder).Length + 1).Replace('\\', '/')))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Patch[] patches = paths.Select(PreparePatch).Where(patch => patch != null).ToArray();
            if (patches.Sum(patch => patch.Cards) == 0)
                throw new InvalidOperationException("No authored embedded encounter cards were found; no scenes were written.");

            // Prepare and validate every candidate before writing any saved preview.
            foreach (Patch patch in patches)
                if (!File.ReadAllBytes(patch.Absolute).SequenceEqual(patch.BeforeBytes))
                    throw new IOException("Authored encounter preview changed during preparation: " + patch.Path);
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
                scannedScenes = paths.Length,
                matchedScenes = patches.Length,
                embeddedCards = patches.Sum(patch => patch.Cards),
                backupRoot = backupRoot,
                preserved = "Every retained serialized block, asset GUID, artwork, map, choice, callback and saved gameplay value is unchanged. Standalone tutorial encounter labels are outside the embedded dashboard path.",
            };
            foreach (Patch patch in patches)
            {
                bool changed = patch.Before != patch.After;
                if (changed)
                {
                    File.WriteAllBytes(patch.Absolute, Encoding.UTF8.GetBytes(patch.After));
                    if (File.ReadAllText(patch.Absolute) != patch.After.TrimStart('\uFEFF'))
                        throw new IOException("Encounter hint preview write verification failed: " + patch.Path);
                    report.updatedScenes++;
                    report.removedHints += patch.Removed.Count;
                }
                report.scenes.Add(new SceneResult
                {
                    path = patch.Path, updated = changed, embeddedCards = patch.Cards,
                    removedHints = patch.Removed,
                });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("ENCOUNTER_HINT_PREVIEW_SYNC_OK: updated " + report.updatedScenes +
                " authored scenes; removed " + report.removedHints + " redundant hints; report=" + reportPath);
        }

        private static Patch PreparePatch(string path)
        {
            string absolute = AbsolutePath(path);
            byte[] beforeBytes = File.ReadAllBytes(absolute);
            string before = new UTF8Encoding(false, true).GetString(beforeBytes);
            if (!before.Contains("m_Name: Encounter Paper Content") || !before.Contains("m_Name: Forward Viewport")) return null;
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("Refusing to replace unsaved authored edits in " + path);

            Dictionary<string, string> blocks = BlockPattern.Matches(before).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            var removedIds = new HashSet<string>();
            var patchedParents = new Dictionary<string, string>();
            var removed = new List<RemovedHint>();
            int cards = 0;
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(path);
                Transform[] contents = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(IsEmbeddedPaperContent).ToArray();
                cards = contents.Length;
                foreach (Transform content in contents)
                {
                    string parentId = ObjectId(content, blocks, path);
                    string parentBlock = blocks[parentId];
                    foreach (string name in HintNames)
                    {
                        Transform[] matches = content.Cast<Transform>().Where(child => child.name == name).ToArray();
                        if (matches.Length > 1)
                            throw new InvalidDataException("Ambiguous embedded encounter hint " + name + " in " + path);
                        if (matches.Length == 0) continue; // Already-clean previews are idempotent.
                        Transform hint = matches[0];
                        Text label = hint.GetComponent<Text>();
                        if (label == null || hint.childCount != 0 || hint.GetComponents<Component>()
                            .Any(component => !(component is RectTransform) && !(component is CanvasRenderer) && !(component is Text)))
                            throw new InvalidDataException("Unexpected components on retired encounter hint " + name + " in " + path);
                        string transformId = ObjectId(hint, blocks, path);
                        var childReference = new Regex(@"(?m)^  - \{fileID: " + Regex.Escape(transformId) + @"\}\r?\n");
                        if (childReference.Matches(parentBlock).Count != 1)
                            throw new InvalidDataException("Expected one embedded content child reference for " + name + " in " + path);
                        parentBlock = childReference.Replace(parentBlock, "");
                        removedIds.Add(ObjectId(hint.gameObject, blocks, path));
                        foreach (Component component in hint.GetComponents<Component>())
                            removedIds.Add(ObjectId(component, blocks, path));
                        removed.Add(new RemovedHint { name = name, text = label.text, transformFileId = transformId });
                    }
                    patchedParents.Add(parentId, parentBlock);
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
            if (cards == 0) return null;

            string after = BlockPattern.Replace(before, match =>
            {
                string id = match.Groups[2].Value;
                return removedIds.Contains(id) ? "" : patchedParents.TryGetValue(id, out string parent) ? parent : match.Value;
            });
            foreach (string id in removedIds)
                if (Regex.IsMatch(after, @"\{fileID: " + Regex.Escape(id) + @"(?:[,}])"))
                    throw new InvalidDataException("A retained object references removed encounter hint " + id + " in " + path);
            // Every other serialized block must remain byte-for-byte identical.
            Dictionary<string, string> retained = BlockPattern.Matches(after).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            if (retained.Count != blocks.Count - removedIds.Count || blocks.Any(pair =>
                !removedIds.Contains(pair.Key) && (!retained.TryGetValue(pair.Key, out string block) ||
                block != (patchedParents.TryGetValue(pair.Key, out string parent) ? parent : pair.Value))))
                throw new InvalidDataException("Retained authored encounter content changed in " + path);
            return new Patch
            {
                Path = path, Absolute = absolute, BeforeBytes = beforeBytes, Before = before,
                After = after, Cards = cards, Removed = removed,
            };
        }

        private static bool IsEmbeddedPaperContent(Transform item)
        {
            string[] hierarchy = { "Encounter Paper Content", "Encounter Card", "Forward Viewport", "Forward View Panel", "Phase Content" };
            foreach (string name in hierarchy)
            {
                if (item == null || item.name != name) return false;
                item = item.parent;
            }
            return true;
        }

        private static string ObjectId(UnityEngine.Object item, Dictionary<string, string> blocks, string path)
        {
            if (item == null) throw new InvalidDataException("Missing authored encounter component in " + path);
            string id = GlobalObjectId.GetGlobalObjectIdSlow(item).targetObjectId.ToString(CultureInfo.InvariantCulture);
            if (!blocks.ContainsKey(id))
                throw new InvalidDataException("Missing serialized encounter object " + item.name + " (" + id + ") in " + path);
            return id;
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
            public int Cards;
            public List<RemovedHint> Removed;
        }

        [Serializable]
        private sealed class SyncReport
        {
            public int scannedScenes, matchedScenes, embeddedCards, updatedScenes, removedHints;
            public string backupRoot, preserved;
            public List<SceneResult> scenes = new List<SceneResult>();
        }

        [Serializable]
        private sealed class SceneResult
        {
            public string path;
            public bool updated;
            public int embeddedCards;
            public List<RemovedHint> removedHints;
        }

        [Serializable]
        private sealed class RemovedHint
        {
            public string name, text, transformFileId;
        }
    }
}
