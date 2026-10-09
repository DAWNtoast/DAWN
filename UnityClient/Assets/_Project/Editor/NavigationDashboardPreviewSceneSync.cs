using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        private static readonly string[] DashboardPanelNames =
        {
            "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal",
        };

        /// <summary>Patch the existing authored navigation hierarchy; never rebuild sample gameplay state.</summary>
        public static void SyncNavigationDashboardFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Navigation dashboard synchronization requires edit mode.");

            string backupRoot = DashboardCommandLinePath("-navigationLayoutBackupPath",
                AbsolutePath("Artifacts/NavigationLayout20261008/preview-before"));
            string reportPath = DashboardCommandLinePath("-navigationLayoutReportPath",
                AbsolutePath("Artifacts/NavigationLayout20261008/preview-sync.json"));
            string[] scenePaths = Directory.GetFiles(AbsolutePath(EditableSceneFolder), "*.unity", SearchOption.AllDirectories)
                .Select(path => EditableSceneFolder + "/" + path.Substring(AbsolutePath(EditableSceneFolder).Length + 1).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var report = new DashboardSyncReport { scannedScenes = scenePaths.Length, backupRoot = backupRoot };

            foreach (string path in scenePaths)
            {
                string absolute = AbsolutePath(path);
                // The cheap text prefilter avoids opening unrelated authored pages.
                // Actual direct-parent membership is checked on the scene below.
                byte[] originalBytes = File.ReadAllBytes(absolute);
                string text = System.Text.Encoding.UTF8.GetString(originalBytes);
                if (DashboardPanelNames.Any(name => !text.Contains(name))) continue;

                Scene previous = SceneManager.GetActiveScene();
                Scene scene = SceneManager.GetSceneByPath(path);
                bool openedHere = !scene.IsValid() || !scene.isLoaded;
                if (!openedHere && scene.isDirty)
                    throw new InvalidOperationException("Refusing to replace unsaved authored edits in " + path);
                if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    RectTransform[] contents = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                        .Where(rect => rect.name == "Phase Content" && HasDashboardChildren(rect)).ToArray();
                    if (contents.Length == 0) continue;

                    string backup = Path.Combine(backupRoot, path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.WriteAllBytes(backup, originalBytes);
                    if (File.Exists(absolute + ".meta") && !File.Exists(backup + ".meta"))
                        File.Copy(absolute + ".meta", backup + ".meta");

                    // Attach is responsible only for responsive geometry. Keep
                    // all authored text and image identities, including map nodes,
                    // tutorial/event state, cargo, logs and custom backdrops.
                    DashboardGraphicSnapshot[] graphics = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Graphic>(true))
                        .Select(graphic => new DashboardGraphicSnapshot(graphic)).ToArray();
                    foreach (RectTransform content in contents)
                    {
                        NavigationDashboardLayout.Attach(content);
                        NavigationDashboardLayout[] layouts = content.GetComponentsInChildren<NavigationDashboardLayout>(true)
                            .Where(layout => layout.transform.parent == content).ToArray();
                        if (layouts.Length != 1)
                            throw new InvalidOperationException("Expected one direct dashboard layout host in " + path);
                        layouts[0].Apply();
                        if (!HasDashboardChildren(content) ||
                            content.Find("Navigation Chart/Procedural Sea Chart Frame/Map Layer") == null)
                            throw new InvalidOperationException("Authored navigation paths changed in " + path);
                    }
                    foreach (DashboardGraphicSnapshot graphic in graphics) graphic.AssertRetained(path);

                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, path))
                        throw new InvalidOperationException("Failed to save dashboard preview " + path);
                    // SaveScene may serialize a zero overlay scale in batch mode.
                    // Reuse the existing narrow repair of the identified root line.
                    RepairSavedNavigationCanvasScale(absolute);
                    AssertSavedNavigationCanvasScale(absolute);
                    report.updated.Add(new DashboardSceneResult { path = path, phaseContents = contents.Length });
                    Debug.Log("NAVIGATION_DASHBOARD_PREVIEW_PATCHED: " + path);
                }
                finally
                {
                    if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }

            if (report.updated.Count == 0)
                throw new InvalidOperationException("No authored navigation dashboards were found; no scenes were saved.");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("NAVIGATION_DASHBOARD_PREVIEW_SYNC_OK: " + report.updated.Count + " scenes; report=" + reportPath);
        }

        private static bool HasDashboardChildren(RectTransform content)
        {
            foreach (string name in DashboardPanelNames)
            {
                Transform[] matches = content.Cast<Transform>().Where(child => child.name == name).ToArray();
                if (matches.Length != 1 || !(matches[0] is RectTransform)) return false;
            }
            return true;
        }

        private static string DashboardCommandLinePath(string option, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, option);
            if (index < 0) return Path.GetFullPath(fallback);
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("-"))
                throw new ArgumentException("Missing path after " + option);
            return Path.GetFullPath(args[index + 1]);
        }

        private sealed class DashboardGraphicSnapshot
        {
            private readonly Graphic graphic;
            private readonly string name;
            private readonly string text;
            private readonly UnityEngine.Object art;

            internal DashboardGraphicSnapshot(Graphic graphic)
            {
                this.graphic = graphic;
                name = graphic.name;
                text = (graphic as Text)?.text;
                art = graphic is Image image ? image.sprite : graphic is RawImage raw ? raw.texture : null;
            }

            internal void AssertRetained(string scene)
            {
                if (graphic == null || graphic.name != name ||
                    graphic is Text label && label.text != text ||
                    graphic is Image image && image.sprite != art ||
                    graphic is RawImage raw && raw.texture != art)
                    throw new InvalidOperationException("Authored text/art changed while laying out " + name + " in " + scene);
            }
        }

        [Serializable]
        private sealed class DashboardSyncReport
        {
            public int scannedScenes;
            public string backupRoot;
            public List<DashboardSceneResult> updated = new List<DashboardSceneResult>();
        }

        [Serializable]
        private sealed class DashboardSceneResult
        {
            public string path;
            public int phaseContents;
        }
    }
}
