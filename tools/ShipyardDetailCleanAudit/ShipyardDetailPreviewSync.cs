using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    // Install only in the isolated audit project's Editor directory.
    public static partial class GameplayPreviewSceneBuilder
    {
        public static void SyncShipyardDetailCleanFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Shipyard detail synchronization requires stopped edit mode.");

            string[] args = Environment.GetCommandLineArgs();
            int outputArgument = Array.IndexOf(args, "-shipyardCleanOutput");
            if (outputArgument < 0 || outputArgument + 1 >= args.Length)
                throw new ArgumentException("Missing -shipyardCleanOutput directory.");
            string output = Path.GetFullPath(args[outputArgument + 1]);
            Directory.CreateDirectory(output);

            // Include tutorial equipment scenes by their configured screen, rather
            // than relying on the scene name. Dynamic UIPreviews regenerate at load.
            var detailSetting = new Regex(@"(?m)^  editorPreviewScreen: " +
                (int)CoreGameplayPreviewScreen.ShipyardDetail + @"\r?$",
                RegexOptions.CultureInvariant);
            string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { EditableSceneFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => detailSetting.IsMatch(File.ReadAllText(AbsolutePath(path))))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("No editable shipyard detail scenes found.");

            foreach (string path in paths)
            {
                string reference = path.Substring(EditableSceneFolder.Length + 1);
                // A negative port/ship preserves all existing preview settings and
                // authored harbour/header geometry; only the Shipyard Panel rebuilds.
                SyncRegionalShipyard(reference, -1, -1);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string[] dependencies = AssetDatabase.GetDependencies(paths, true)
                .Where(path => path.StartsWith(EditableVisualAssetFolder + "/", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            File.WriteAllLines(Path.Combine(output, "synced-scenes.txt"), paths);
            File.WriteAllLines(Path.Combine(output, "preview-dependencies.txt"), dependencies);
            Debug.Log("SHIPYARD_DETAIL_CLEAN_PREVIEWS_SYNCED: " + paths.Length);

#endif
        }
    }
}
