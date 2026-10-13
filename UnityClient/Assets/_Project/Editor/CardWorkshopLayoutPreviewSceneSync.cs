using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        public static readonly string[] CardWorkshopLayoutReferencePaths =
        {
            "UI_CardWorkshop.unity", "UI_DeckBuilder.unity",
            "UICardWorkshop/UICardWorkshop_CardLibrary.unity",
            "UICardWorkshop/UICardWorkshop_UpgradePreview.unity",
            "UICardWorkshop/UICardWorkshop_UpgradeComplete.unity",
            "UICardUpgrade/UICardUpgrade_UpgradePreview.unity",
            "UICardUpgrade/UICardUpgrade_MaxLevel.unity",
            "UICardUpgrade/UICardUpgrade_NoUpgradeAvailable.unity",
            "UIDeckBuilder/UIDeckBuilder_DeckBuilding.unity",
            "UIDeckBuilder/UIDeckBuilder_DeckEmpty.unity",
            "UIDeckBuilder/UIDeckBuilder_DeckReady.unity",
            "UISpecialCardCollection/UISpecialCardCollection_LockedEmpty.unity",
            "UISpecialCardCollection/UISpecialCardCollection_UnlockedCollection.unity",
            "UISpecialCardCollection/UISpecialCardCollection_CardDetail.unity",
        };

        [Serializable] private sealed class WorkshopLayoutSyncReport
        {
            public string scope = "Refresh only the 14 existing saved workshop, upgrade, deck-builder and special-card preview scenes through production editor builders. No player saves and no automatic production copy-back. Legacy scenario seeds are corrected to their named screen; existing upgrade flow states are retained.";
            public string capturedAtUtc, isolatedProject, productionProject = @"D:\Game\UnityClient", captureDirectory;
            public bool savedAndReloaded, capturesPassed;
            public List<WorkshopLayoutSceneResult> scenes = new List<WorkshopLayoutSceneResult>();
            public List<WorkshopLayoutCopyFile> copyBackFiles = new List<WorkshopLayoutCopyFile>();
        }

        [Serializable] private sealed class WorkshopLayoutSceneResult
        {
            public string path, screen, flowState;
            public int stage, renderedCards;
            public bool complete, obsoleteChestsAbsent;
        }

        [Serializable] private sealed class WorkshopLayoutCopyFile
        {
            public string assetPath, sourcePath, originalSha256, sha256;
        }

        /// <summary>
        /// Run in a copied project beneath D:/Game/tmp with graphics enabled.
        /// Pass -cardWorkshopPreviewSyncPath pointing to a fresh Artifacts output.
        /// The copy-back manifest includes original hashes so a caller can reject
        /// concurrent production edits before copying each asset and its meta.
        /// </summary>
        public static void SyncCardWorkshopLayoutPreviewsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            string project = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                !project.StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run workshop preview synchronization only in an isolated stopped project beneath D:/Game/tmp.");
            string[] arguments = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(arguments, "-cardWorkshopPreviewSyncPath");
            if (option < 0 || option + 1 >= arguments.Length)
                throw new ArgumentException("Pass -cardWorkshopPreviewSyncPath with a fresh output directory.");
            string output = Path.GetFullPath(arguments[option + 1]);
            if (!output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Workshop sync output must remain beneath D:/Game/Artifacts.");
            string reportPath = Path.Combine(output, "preview-sync-report.json");
            string captures = Path.Combine(output, "Screenshots");
            if (File.Exists(reportPath) || Directory.Exists(captures) && Directory.EnumerateFileSystemEntries(captures).Any())
                throw new IOException("Use a fresh workshop preview synchronization output directory.");
            Directory.CreateDirectory(output);

            var originals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string reference in CardWorkshopLayoutReferencePaths)
            {
                string path = EditableSceneFolder + "/" + reference;
                if (!File.Exists(AbsolutePath(path))) throw new FileNotFoundException("Missing scoped workshop scene.", path);
                WorkshopRememberOriginal(path, originals);
                WorkshopRememberOriginal(path + ".meta", originals);
                string backup = Path.Combine(output, "Before", path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsolutePath(path), backup, false);
                if (File.Exists(AbsolutePath(path + ".meta"))) File.Copy(AbsolutePath(path + ".meta"), backup + ".meta", false);
            }
            string visualFolder = AbsolutePath(EditableVisualAssetFolder);
            if (Directory.Exists(visualFolder))
                foreach (string file in Directory.EnumerateFiles(visualFolder, "*", SearchOption.AllDirectories))
                    originals[WorkshopAssetPath(project, file)] = WorkshopSyncHash(file);

            var report = new WorkshopLayoutSyncReport
            {
                isolatedProject = project, capturedAtUtc = DateTime.UtcNow.ToString("O"), captureDirectory = captures,
            };
            foreach (string reference in CardWorkshopLayoutReferencePaths)
            {
                string path = EditableSceneFolder + "/" + reference;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                CoreGameplayDemoController controller = WorkshopSceneController(scene);
                var serialized = new SerializedObject(controller);
                CoreGameplayPreviewPort port = (CoreGameplayPreviewPort)serialized.FindProperty("editorPreviewPort").intValue;
                int stage = serialized.FindProperty("editorPreviewRouteIndex").intValue;
                GameplayFlowPreviewState flow = (GameplayFlowPreviewState)serialized.FindProperty("editorPreviewFlowState").intValue;
                CoreGameplayPreviewScreen screen = WorkshopScreenForReference(reference);
                controller.ConfigureEditorPreview(screen, port, stage, true);
                if (screen == CoreGameplayPreviewScreen.CardUpgrade &&
                    (flow == GameplayFlowPreviewState.UpgradePreview || flow == GameplayFlowPreviewState.UpgradeComplete))
                    controller.ConfigureEditorFlowState(flow);
                ValidateWorkshopSavedPreview(controller, path);
                var artSpec = new PreviewSceneSpec(Path.GetFileNameWithoutExtension(reference), "卡组工坊预览", screen, port, stage);
                PersistGeneratedSceneVisuals(controller.transform, artSpec);
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to save " + path);
                Scene saved = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                CoreGameplayDemoController reloaded = WorkshopSceneController(saved);
                ValidateWorkshopSavedPreview(reloaded, path);
                report.scenes.Add(new WorkshopLayoutSceneResult
                {
                    path = path, screen = screen.ToString(), flowState = screen == CoreGameplayPreviewScreen.CardUpgrade ? flow.ToString() : "None",
                    stage = stage, complete = true, obsoleteChestsAbsent = true,
                    renderedCards = reloaded.GetComponentsInChildren<Transform>(true).Count(item =>
                        (item.name.StartsWith("Upgrade Collection ", StringComparison.Ordinal) || item.name.StartsWith("Special Card ", StringComparison.Ordinal) || item.name.StartsWith("Workshop Card ", StringComparison.Ordinal))
                        && item.name.EndsWith(" Face", StringComparison.Ordinal)),
                });
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.savedAndReloaded = true;
            var copyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WorkshopLayoutSceneResult scene in report.scenes)
            {
                copyPaths.Add(scene.path);
                foreach (string dependency in AssetDatabase.GetDependencies(scene.path, true))
                    if (dependency.StartsWith(EditableVisualAssetFolder + "/", StringComparison.Ordinal)) copyPaths.Add(dependency);
            }
            foreach (string asset in copyPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                WorkshopAddCopyFile(asset, project, originals, report);
                if (File.Exists(AbsolutePath(asset + ".meta"))) WorkshopAddCopyFile(asset + ".meta", project, originals, report);
            }
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            MethodInfo capture = typeof(UIPreviewSceneAcceptance).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(string[]), typeof(string) }, null);
            if (capture == null) throw new MissingMethodException("UIPreviewSceneAcceptance.Capture");
            capture.Invoke(null, new object[] { captures, EditableSceneFolder, CardWorkshopLayoutReferencePaths, null });
            report.capturesPassed = true;
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("CARD_WORKSHOP_LAYOUT_PREVIEW_SYNC_OK: 14 scenes saved, reloaded, captured; verified copy-back file list: " + reportPath);
        }

        private static CoreGameplayPreviewScreen WorkshopScreenForReference(string reference)
        {
            if (reference.StartsWith("UISpecialCardCollection/", StringComparison.Ordinal)) return CoreGameplayPreviewScreen.SpecialCardCollection;
            if (reference.StartsWith("UIDeckBuilder/", StringComparison.Ordinal) || reference == "UI_DeckBuilder.unity") return CoreGameplayPreviewScreen.DeckBuilder;
            if (reference.StartsWith("UICardUpgrade/", StringComparison.Ordinal) || reference.Contains("_Upgrade")) return CoreGameplayPreviewScreen.CardUpgrade;
            return CoreGameplayPreviewScreen.CardWorkshop;
        }

        private static CoreGameplayDemoController WorkshopSceneController(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();

        private static void ValidateWorkshopSavedPreview(CoreGameplayDemoController controller, string path)
        {
            if (!controller.HasCompleteEditorPreview || !controller.EditorPreviewPersistsGeneratedUi)
                throw new InvalidOperationException("Incomplete saved workshop preview: " + path);
            Transform[] children = controller.GetComponentsInChildren<Transform>(true);
            if (children.Any(item => item.name == "Card Workshop Chest Stage" || item.name.StartsWith("Open Deck Chest ", StringComparison.Ordinal)))
                throw new InvalidOperationException("Obsolete workshop chests remain in " + path);
            if (controller.EditorPreviewScreen == CoreGameplayPreviewScreen.CardUpgrade &&
                !children.Any(item => item.name == "Card Workshop Preview Panel"))
                throw new InvalidOperationException("Upgrade preview lacks the shared workshop collection: " + path);
            if (controller.EditorPreviewScreen == CoreGameplayPreviewScreen.SpecialCardCollection)
            {
                UnityEngine.UI.Button upgrade = children.Select(item => item.GetComponent<UnityEngine.UI.Button>())
                    .FirstOrDefault(item => item != null && item.name == "Open Battle Card Upgrades");
                if (!children.Any(item => item.name == "Card Workshop Preview Panel") ||
                    children.Any(item => item.name == "Special Card Collection Panel") ||
                    !children.Any(item => item.name == "Workshop Upgrade Disabled Mark") || upgrade == null || upgrade.interactable)
                    throw new InvalidOperationException("Special cards must use the shared workshop with upgrades disabled: " + path);
            }
        }

        private static string WorkshopAssetPath(string project, string absolute) => Path.GetFullPath(absolute)
            .Substring(project.Length + 1).Replace('\\', '/');

        private static void WorkshopRememberOriginal(string asset, Dictionary<string, string> originals)
        {
            if (File.Exists(AbsolutePath(asset))) originals[asset] = WorkshopSyncHash(AbsolutePath(asset));
        }

        private static string WorkshopSyncHash(string file)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream input = File.OpenRead(file))
                return BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void WorkshopAddCopyFile(string asset, string project, Dictionary<string, string> originals, WorkshopLayoutSyncReport report)
        {
            string absolute = AbsolutePath(asset);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing generated preview dependency.", asset);
            report.copyBackFiles.Add(new WorkshopLayoutCopyFile
            {
                assetPath = asset, sourcePath = absolute, originalSha256 = originals.TryGetValue(asset, out string previous) ? previous : string.Empty,
                sha256 = WorkshopSyncHash(absolute),
            });
        }
    }
}
