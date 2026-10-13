// Temporary scoped migration. Import only into the isolated validation project's Editor assembly.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        [Serializable] private sealed class BoardingSyncReport
        {
            public string capturedAtUtc, project;
            public string scope = "Exactly three authored boarding scenes. Keeps the existing scene GUID, controller serialization, scene roots, Battle Card root and graphics outside its payload/readouts. Replaces only Battle Card children, including the deck painting, plus Battle Readouts Overlay. Uses a disposable production preview session; no PlayerPrefs writes. Newly required transient sprites/materials may be persisted by the existing production preview asset helper.";
            public List<BoardingSyncScene> scenes = new List<BoardingSyncScene>();
            public List<BoardingSyncAsset> changedEditableAssets = new List<BoardingSyncAsset>();
            public List<string> referencedEditableAssetsAndMeta = new List<string>();
        }
        [Serializable] private sealed class BoardingSyncAsset
        {
            public string path, change, beforeSha256, afterSha256;
        }
        [Serializable] private sealed class BoardingSyncScene
        {
            public string path, beforeSha256, afterSha256, metaSha256, controllerIdentity, error;
            public bool retainedIdentityVerified;
        }

        public static void SyncBoardingCinematicFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            string project = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName).TrimEnd('/', '\\');
            string permittedProject = Path.GetFullPath(BoardingSyncArgument("-boardingSyncProject")).TrimEnd('/', '\\');
            if (!string.Equals(project, permittedProject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(project, "D:\\Game\\UnityClient", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run this scoped migration only in the explicitly named isolated project.");
            string reportPath = Path.GetFullPath(BoardingSyncArgument("-boardingSyncReport"));
            string backupRoot = Path.GetFullPath(BoardingSyncArgument("-boardingSyncBackup"));
            if (File.Exists(reportPath)) throw new IOException("Report already exists; keep prior evidence.");
            if (Directory.Exists(backupRoot) && Directory.EnumerateFileSystemEntries(backupRoot).Any())
                throw new IOException("Use a new/empty backup directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            Directory.CreateDirectory(backupRoot);
            var report = new BoardingSyncReport { capturedAtUtc = DateTime.UtcNow.ToString("O"), project = project };
            Dictionary<string, string> editableBefore = BoardingEditableAssetHashes();
            try
            {
                foreach (string reference in new[] { "UIBattle/UIBattle_BoardingHook.unity", "UIBattle/UIBattle_BoardingLadder.unity", "UIBattle/UIBattle_BoardingRam.unity" })
                {
                    var item = new BoardingSyncScene { path = EditableSceneFolder + "/" + reference };
                    report.scenes.Add(item);
                    try { SyncBoardingCinematicScene(item, backupRoot); }
                    catch (Exception error) { item.error = error.ToString(); throw; }
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                Dictionary<string, string> editableAfter = BoardingEditableAssetHashes();
                foreach (string path in editableBefore.Keys.Union(editableAfter.Keys).OrderBy(path => path, StringComparer.Ordinal))
                {
                    editableBefore.TryGetValue(path, out string before);
                    editableAfter.TryGetValue(path, out string after);
                    if (before == after) continue;
                    report.changedEditableAssets.Add(new BoardingSyncAsset { path = path,
                        change = before == null ? "added" : after == null ? "deleted" : "modified",
                        beforeSha256 = before, afterSha256 = after });
                }
                foreach (string asset in AssetDatabase.GetDependencies(report.scenes.Select(item => item.path).ToArray(), true)
                    .Where(path => path.StartsWith(EditableVisualAssetFolder + "/", StringComparison.Ordinal)).OrderBy(path => path))
                {
                    report.referencedEditableAssetsAndMeta.Add(asset);
                    if (File.Exists(AbsolutePath(asset) + ".meta")) report.referencedEditableAssetsAndMeta.Add(asset + ".meta");
                }
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            if (report.scenes.Count != 3 || report.scenes.Any(item => !item.retainedIdentityVerified || !string.IsNullOrEmpty(item.error)))
                throw new InvalidOperationException("Boarding scene synchronization failed.");
            Debug.Log("BOARDING_CINEMATIC_SYNC_OK: three scoped scenes; report " + reportPath);

#endif
        }

        private static void SyncBoardingCinematicScene(BoardingSyncScene item, string backupRoot)
        {
            string absolute = AbsolutePath(item.path);
            if (!File.Exists(absolute) || !File.Exists(absolute + ".meta")) throw new FileNotFoundException(item.path);
            item.beforeSha256 = BoardingHash(absolute);
            item.metaSha256 = BoardingHash(absolute + ".meta");
            string backup = Path.Combine(backupRoot, item.path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(absolute, backup, false);
            File.Copy(absolute + ".meta", backup + ".meta", false);

            Scene scene = EditorSceneManager.OpenScene(item.path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            string controllerBefore = BoardingStableJson(destination);
            item.controllerIdentity = GlobalObjectId.GetGlobalObjectIdSlow(destination).ToString();
            string[] rootIds = scene.GetRootGameObjects().Select(root => GlobalObjectId.GetGlobalObjectIdSlow(root).ToString()).OrderBy(id => id).ToArray();
            Transform content = destination.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Phase Content");
            Transform target = content.Find("Battle Overlay/Battle Card");
            Transform oldReadouts = content.Find("Battle Readouts Overlay");
            if (target == null) throw new InvalidOperationException("Saved Battle Card missing in " + item.path);
            RectTransform targetRect = (RectTransform)target;
            Vector2 targetSize = targetRect.sizeDelta, targetPosition = targetRect.anchoredPosition;
            Vector3 targetScale = targetRect.localScale;
            string targetSurface = BoardingStableJson(target.GetComponent<Image>());
            Dictionary<string, string> retainedGraphics = BoardingRetainedGraphics(destination, target, oldReadouts);

            // Reuse the repaired permanent entry point, so future editor syncs
            // and this audit produce the same payload instead of two migrations.
            string reference = item.path.Substring(EditableSceneFolder.Length + 1);
            SyncBoardingCrewScene(reference);
            scene = SceneManager.GetSceneByPath(item.path);
            destination = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            content = destination.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Phase Content");
            target = content.Find("Battle Overlay/Battle Card");
            targetRect = (RectTransform)target;
            Dictionary<string, string> afterGraphics = BoardingRetainedGraphics(destination, target, content.Find("Battle Readouts Overlay"));
            if (BoardingStableJson(destination) != controllerBefore ||
                GlobalObjectId.GetGlobalObjectIdSlow(destination).ToString() != item.controllerIdentity ||
                !rootIds.SequenceEqual(scene.GetRootGameObjects().Select(root => GlobalObjectId.GetGlobalObjectIdSlow(root).ToString()).OrderBy(id => id)) ||
                targetRect.sizeDelta != targetSize || targetRect.anchoredPosition != targetPosition || targetRect.localScale != targetScale ||
                BoardingStableJson(target.GetComponent<Image>()) != targetSurface || afterGraphics.Count != retainedGraphics.Count ||
                retainedGraphics.Any(pair => !afterGraphics.TryGetValue(pair.Key, out string value) || value != pair.Value))
                throw new InvalidOperationException("Scene identity, controller, outer battle layout or an unrelated graphic changed.");
            item.retainedIdentityVerified = true;
            if (BoardingHash(absolute + ".meta") != item.metaSha256) throw new InvalidOperationException("Scene meta changed.");
            item.afterSha256 = BoardingHash(absolute);
        }

        private static string BoardingStableJson(Object value) =>
            Regex.Replace(EditorJsonUtility.ToJson(value), "\"instanceID\"\\s*:\\s*-?\\d+", "\"instanceID\":0");

        private static Dictionary<string, string> BoardingRetainedGraphics(CoreGameplayDemoController controller, Transform target, Transform readouts)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Graphic graphic in controller.GetComponentsInChildren<Graphic>(true))
            {
                Transform item = graphic.transform;
                if (item == target || item.IsChildOf(target) || (readouts != null && (item == readouts || item.IsChildOf(readouts)))) continue;
                var names = new List<string>();
                for (Transform cursor = item; cursor != null && cursor != controller.transform; cursor = cursor.parent)
                    names.Insert(0, cursor.name + "#" + cursor.GetSiblingIndex());
                string key = string.Join("/", names) + "::" + graphic.GetType().Name;
                result.Add(key, BoardingStableJson(graphic) + "|texture=" + AssetDatabase.GetAssetPath(graphic.mainTexture) +
                    "|material=" + AssetDatabase.GetAssetPath(graphic.material) + "|rect=" + BoardingStableJson(graphic.rectTransform));
            }
            return result;
        }

        private static string BoardingSyncArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass " + name + " ABSOLUTE_PATH");
            return args[index + 1];
        }

        private static string BoardingHash(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static Dictionary<string, string> BoardingEditableAssetHashes()
        {
            string directory = AbsolutePath(EditableVisualAssetFolder);
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(directory)) return hashes;
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                string relative = EditableVisualAssetFolder + "/" + path.Substring(directory.Length).TrimStart('/', '\\').Replace('\\', '/');
                hashes.Add(relative, BoardingHash(path));
            }
            return hashes;
        }
    }
}
