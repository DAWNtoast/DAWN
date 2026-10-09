// Temporary Editor-only migration. Import into the isolated validation project's
// Assets/_Project/Editor, not the main project. No complete preview regeneration.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static partial class GameplayPreviewSceneBuilder
    {
        public static void SyncVoyageHeaderFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required for voyage header migration.");
            string backupRoot = VoyageHeaderAuditArgument("-voyageHeaderBackupPath");
            string reportPath = VoyageHeaderAuditArgument("-voyageHeaderReportPath");
            if (File.Exists(reportPath)) throw new IOException("Choose a new report filename; previous evidence is preserved.");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var report = new VoyageHeaderMigrationReport { startedUtc = DateTime.UtcNow.ToString("O"), backupRoot = backupRoot };
            try
            {
                foreach (string reference in NavigationEnduranceReferencePaths)
                    report.scenes.Add(MigrateVoyageHeader(reference, backupRoot));
                if (report.scenes.Count != NavigationEnduranceReferencePaths.Length)
                    throw new InvalidOperationException("Not all explicit navigation preview references were migrated.");
                report.status = "passed";
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                report.status = "failed";
                report.error = exception.ToString();
                throw;
            }
            finally
            {
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            Debug.Log("VOYAGE_HEADER_PREVIEW_SYNC_OK: " + report.scenes.Count + " scoped previews; report=" + reportPath);
        }

        private static VoyageHeaderSceneRecord MigrateVoyageHeader(string reference, string backupRoot)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            byte[] originalBytes = File.ReadAllBytes(absolute);
            Scene previous = SceneManager.GetActiveScene();
            Scene destinationScene = SceneManager.GetSceneByPath(path);
            bool openedHere = !destinationScene.IsValid() || !destinationScene.isLoaded;
            if (!openedHere && destinationScene.isDirty)
                throw new InvalidOperationException("Refusing unsaved scene edits: " + path);
            if (openedHere) destinationScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Scene stagingScene = default;
            GameObject staging = null;
            try
            {
                SceneManager.SetActiveScene(destinationScene);
                CoreGameplayDemoController destination = destinationScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                RectTransform content = destination.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "Phase Content");
                RectTransform header = content.Find("Voyage Header") as RectTransform;
                if (header == null || content.Find("Navigation Chart/Procedural Sea Chart Frame/Map Layer") == null ||
                    content.Find("Navigation Controls") == null || content.Find("Navigation Journal") == null)
                    throw new InvalidOperationException("Missing authored navigation hierarchy: " + path);
                var record = new VoyageHeaderSceneRecord { path = path };
                RectTransform[] oldRibbons = content.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect.name == "Lookout Time Ribbon").ToArray();
                if (oldRibbons.Length > 1) throw new InvalidOperationException("Ambiguous authored clock: " + path);
                VoyageTimeRibbonGraphic oldTrack = oldRibbons.SingleOrDefault()?.GetComponentInChildren<VoyageTimeRibbonGraphic>(true);
                Text oldReadout = oldRibbons.SingleOrDefault()?.GetComponentsInChildren<Text>(true)
                    .SingleOrDefault(label => label.name == "Lookout Time Readout");
                double? oldHours = oldTrack == null ? (double?)null : oldTrack.ElapsedHours;
                string oldDateText = oldReadout == null ? null : oldReadout.text;
                if (oldHours.HasValue != (oldDateText != null))
                    throw new InvalidOperationException("Incomplete authored clock; date and timeline must be preserved together: " + path);
                Text lookoutHeading = content.GetComponentsInChildren<Text>(true).SingleOrDefault(label => label.name == "Lookout Heading");
                HeaderRetainedGraphic[] retained = destination.GetComponentsInChildren<Graphic>(true)
                    .Where(graphic => graphic != lookoutHeading && !oldRibbons.Any(ribbon => graphic.transform.IsChildOf(ribbon)))
                    .Select(graphic => new HeaderRetainedGraphic(graphic)).ToArray();

                string backup = Path.Combine(backupRoot, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.WriteAllBytes(backup, originalBytes);
                if (File.Exists(absolute + ".meta") && !File.Exists(backup + ".meta")) File.Copy(absolute + ".meta", backup + ".meta");

                stagingScene = EditorSceneManager.NewPreviewScene();
                staging = new GameObject("Disposable Voyage Header Template");
                staging.SetActive(false);
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                var source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                staging.SetActive(true);
                source.RefreshEditorPreview();
                Transform freshHeader = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Voyage Header");
                Transform freshRibbon = freshHeader.Find("Lookout Time Ribbon");
                if (freshRibbon == null) throw new InvalidOperationException("Fresh preview has no header clock: " + path);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == source.EditorPreviewScreen);
                if (spec == null) throw new InvalidOperationException("No matching preview art specification: " + path);

                // Clone just the compact clock. Keep existing authored date/hours
                // where available; event pages formerly had no displayed clock.
                foreach (RectTransform old in oldRibbons) Object.DestroyImmediate(old.gameObject);
                CopyTreasureLayer(freshRibbon, header, "Lookout Time Ribbon");
                Transform ribbonCopy = header.Find("Lookout Time Ribbon");
                VoyageTimeRibbonGraphic track = ribbonCopy.GetComponentInChildren<VoyageTimeRibbonGraphic>(true);
                Text readout = ribbonCopy.GetComponentsInChildren<Text>(true).Single(label => label.name == "Lookout Time Readout");
                if (oldHours.HasValue) track.SetVoyageHours(oldHours.Value);
                if (oldDateText != null) readout.text = oldDateText;
                record.clockSource = oldHours.HasValue && oldDateText != null ? "retained authored clock" : "matching fresh editor preview";
                record.elapsedHours = track.ElapsedHours;
                record.readout = readout.text;
                record.previewScreen = source.EditorPreviewScreen.ToString();

                Transform settings = header.Find("Voyage Settings");
                if (settings == null)
                {
                    Transform freshSettings = freshHeader.Find("Voyage Settings");
                    if (freshSettings == null) throw new InvalidOperationException("Missing settings template: " + path);
                    PersistGeneratedSceneVisuals(freshSettings, spec);
                    CopyTreasureLayer(freshSettings, header, "Voyage Settings");
                    settings = header.Find("Voyage Settings");
                    record.settingsAdded = true;
                }
                ((RectTransform)settings).sizeDelta = new Vector2(128f, 58f);
                if (header.Find("Voyage Cargo") == null)
                {
                    // Reuse that exact authored button's sprites/materials; do
                    // not replace the scene's existing wood art with a new skin.
                    GameObject cargo = Object.Instantiate(settings.gameObject, header, false);
                    cargo.name = "Voyage Cargo";
                    // Saved previews rebind runtime actions when rebuilt; never
                    // inherit a persistent Settings action on the Cargo button.
                    cargo.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
                    cargo.GetComponentsInChildren<Text>(true).Single().text = "船舱";
                    record.cargoAdded = true;
                }
                if (lookoutHeading != null)
                {
                    int separator = lookoutHeading.text.LastIndexOf('·');
                    if (separator >= 0 && Enum.GetValues(typeof(NavigationTimeOfDay)).Cast<NavigationTimeOfDay>()
                        .Any(time => lookoutHeading.text.Substring(separator + 1).Trim() == NavigationTimeOfDayUtility.Label(time)))
                        lookoutHeading.text = lookoutHeading.text.Substring(0, separator).TrimEnd();
                }
                foreach (Transform child in ribbonCopy.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.None;
                SceneManager.SetActiveScene(destinationScene);
                NavigationDashboardLayout.Attach(content);
                foreach (HeaderRetainedGraphic graphic in retained) graphic.AssertUnchanged(path);
                if (content.GetComponentsInChildren<VoyageTimeRibbonGraphic>(true).Length != 1 || track.transform.parent.parent != header)
                    throw new InvalidOperationException("Clock not uniquely owned by the header: " + path);
                record.retainedGraphicCount = retained.Length;
                EditorSceneManager.MarkSceneDirty(destinationScene);
                if (!EditorSceneManager.SaveScene(destinationScene, path)) throw new IOException("Cannot save " + path);
                RepairSavedNavigationCanvasScale(absolute);
                AssertSavedNavigationCanvasScale(absolute);
                return record;
            }
            finally
            {
                if (staging != null) Object.DestroyImmediate(staging);
                if (stagingScene.IsValid()) EditorSceneManager.ClosePreviewScene(stagingScene);
                if (openedHere && destinationScene.IsValid() && destinationScene.isLoaded) EditorSceneManager.CloseScene(destinationScene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static string VoyageHeaderAuditArgument(string option)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, option);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException(option + " requires an absolute path.");
            string path = Path.GetFullPath(args[index + 1]);
            if (!path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(option + " must be under Artifacts.");
            return path;
        }

        private sealed class HeaderRetainedGraphic
        {
            private readonly Graphic graphic;
            private readonly string name, text;
            private readonly Object art;
            public HeaderRetainedGraphic(Graphic graphic)
            {
                this.graphic = graphic; name = graphic.name; text = (graphic as Text)?.text;
                art = graphic is Image image ? image.sprite : graphic is RawImage raw ? raw.texture : null;
            }
            public void AssertUnchanged(string scene)
            {
                if (graphic == null || graphic.name != name || graphic is Text label && label.text != text ||
                    graphic is Image image && image.sprite != art || graphic is RawImage raw && raw.texture != art)
                    throw new InvalidOperationException("Unrelated authored text/art changed: " + name + " in " + scene);
            }
        }

        [Serializable] private sealed class VoyageHeaderMigrationReport
        {
            public string status = "running", startedUtc, backupRoot, error;
            public string scope = "Only header clock/actions and removal of duplicated lookout day/night; existing map, event, cargo, journal and art retained. No whole-scene regeneration.";
            public List<VoyageHeaderSceneRecord> scenes = new List<VoyageHeaderSceneRecord>();
        }
        [Serializable] private sealed class VoyageHeaderSceneRecord
        {
            public string path, previewScreen, clockSource, readout;
            public double elapsedHours;
            public bool settingsAdded, cargoAdded;
            public int retainedGraphicCount;
        }
    }
}
