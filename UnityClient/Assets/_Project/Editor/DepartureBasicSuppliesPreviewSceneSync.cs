using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
    public static partial class GameplayPreviewSceneBuilder
    {
        /// <summary>Updates shared geometry and the two supply captions in existing authored previews.</summary>
        public static void SyncDepartureBasicSuppliesFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Departure basic supply synchronization requires stopped edit mode.");
            DepartureLayoutPatch[] patches = ChartSeedReferencePaths.Select(PrepareDepartureBasicSuppliesPatch).ToArray();
            foreach (DepartureLayoutPatch patch in patches)
                if (File.ReadAllText(patch.Path) != patch.Before)
                    throw new IOException("Departure scene changed during supply patch preparation: " + patch.Path);
            int changed = 0;
            foreach (DepartureLayoutPatch patch in patches)
            {
                if (patch.Before == patch.After) continue;
                string backup = AbsolutePath("Artifacts/DepartureBasicSupplies20261011/preview-backup/" + patch.Reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(patch.Path, backup);
                File.WriteAllText(patch.Path, patch.After, new UTF8Encoding(false));
                changed++;
            }
            AssetDatabase.Refresh();
            Debug.Log("DEPARTURE_BASIC_SUPPLIES_PREVIEW_SYNC: verified " + patches.Length + "; updated " + changed +
                " authored scenes; patched " + patches.Sum(patch => patch.ChangedFields) +
                " geometry/caption fields. Existing artwork, callbacks and tutorial gates retained.");
        }

        public static void SyncAndCaptureDepartureBasicSuppliesFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            SyncDepartureBasicSuppliesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureDepartureLayoutFromCommandLine();
        }

        private static DepartureLayoutPatch PrepareDepartureBasicSuppliesPatch(string reference)
        {
            // The existing layout patch checks every component and permits only
            // known RectTransform/Text geometry fields before restoring the fixture.
            DepartureLayoutPatch patch = PrepareDepartureLayoutPatch(reference);
            string scenePath = EditableSceneFolder + "/" + reference;
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(scenePath);
                CoreGameplayDemoController controller = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                Transform setup = controller.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Phase Content").Find("Voyage Setup");
                if (setup == null) throw new InvalidOperationException("Missing authored departure setup in " + scenePath);
                if (setup.Find("Departure Crew Configuration Title") != null) return patch;
                foreach (var caption in new[] {
                    new[] { "Departure Buy Provisions/Label", "购买基础物资" },
                    new[] { "Adjust Voyage Supplies/Label", "前往市场" },
                })
                {
                    // Older tutorial fixtures intentionally have no quick-purchase
                    // entry. Retain that authored gate and update their market
                    // caption without creating a new button or callback.
                    if (caption[0] == "Departure Buy Provisions/Label" &&
                        reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal) &&
                        setup.Find("Departure Buy Provisions") == null)
                        continue;
                    Text label = setup.Find(caption[0])?.GetComponent<Text>();
                    if (label == null) throw new InvalidOperationException("Missing supply caption " + caption[0] + " in " + scenePath);
                    string id = GlobalObjectId.GetGlobalObjectIdSlow(label).targetObjectId.ToString(CultureInfo.InvariantCulture);
                    var blockPattern = new Regex(@"(?ms)^--- !u!114 &" + Regex.Escape(id) + @"[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
                    if (blockPattern.Matches(patch.After).Count != 1)
                        throw new InvalidDataException("Expected one supply label YAML block " + id + " in " + scenePath);
                    patch.After = blockPattern.Replace(patch.After, block =>
                    {
                        var textPattern = new Regex(@"(?m)^(  m_Text: )[^\r\n]*");
                        if (textPattern.Matches(block.Value).Count != 1)
                            throw new InvalidDataException("Expected one inline supply m_Text in " + scenePath);
                        string desired = "\"" + string.Concat(caption[1].Select(character => "\\u" + ((int)character).ToString("X4"))) + "\"";
                        string after = textPattern.Replace(block.Value, text => text.Groups[1].Value + desired);
                        if (after != block.Value) patch.ChangedFields++;
                        return after;
                    });
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
            return patch;
        }
    }
}
