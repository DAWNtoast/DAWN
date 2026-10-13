using System;
using System.Collections.Generic;
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
        private static readonly string[] RemovedDepartureSetupNames =
        {
            "Ship Stats", "Departure Ship Schematic Caption", "Departure Vessel Hint", "Destination Summary",
            "Cargo", "Departure Provision Quote", "Departure Provision Water Icon", "Departure Provision Food Icon",
            "Upgrade Part 0", "Upgrade Part 1", "Upgrade Part 2", "Upgrade Part 3", "Upgrade Part 4",
            "Upgrade Name 0", "Upgrade Name 1", "Upgrade Name 2", "Upgrade Name 3", "Upgrade Name 4",
        };

        /// <summary>
        /// Removes only the retired setup information and updates the shared departure
        /// geometry. Patches original YAML blocks so artwork, callbacks, tutorial gates,
        /// and all unrelated authored values keep their original serialized identities.
        /// </summary>
        public static void SyncDepartureCleanPanelFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Departure panel synchronization requires stopped edit mode.");
            CleanDeparturePatch[] patches = ChartSeedReferencePaths.Select(PrepareCleanDeparturePatch).ToArray();
            foreach (CleanDeparturePatch patch in patches)
                if (File.ReadAllText(patch.Path) != patch.Before)
                    throw new IOException("Departure scene changed during cleanup preparation: " + patch.Path);
            int changed = 0;
            foreach (CleanDeparturePatch patch in patches)
            {
                if (patch.Before == patch.After) continue;
                string backup = AbsolutePath("Artifacts/DepartureCleanPanel20261011/preview-backup/" + patch.Reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(patch.Path, backup);
                File.WriteAllText(patch.Path, patch.After, new UTF8Encoding(false));
                changed++;
            }
            AssetDatabase.Refresh();
            Debug.Log("DEPARTURE_CLEAN_PANEL_PREVIEW_SYNC: verified " + patches.Length + "; updated " + changed +
                " authored scenes; removed " + patches.Sum(patch => patch.RemovedObjects) +
                " retired information objects; patched " + patches.Sum(patch => patch.ChangedFields) +
                " layout fields. Existing artwork, callbacks and tutorial gates retained.");
        }

        public static void SyncAndCaptureDepartureCleanPanelFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            SyncDepartureCleanPanelFromCommandLine();
            UIPreviewSceneAcceptance.CaptureDepartureLayoutFromCommandLine();
        }

        private sealed class CleanDeparturePatch
        {
            public string Reference;
            public string Path;
            public string Before;
            public string After;
            public int RemovedObjects;
            public int ChangedFields;
        }

        private static CleanDeparturePatch PrepareCleanDeparturePatch(string reference)
        {
            string scenePath = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(scenePath);
            string before = File.ReadAllText(absolute);
            var blockPattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
            Dictionary<string, string> blocks = blockPattern.Matches(before).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            var removedIds = new HashSet<string>();
            var removedRootTransforms = new HashSet<string>();
            string setupId = null;
            Scene preview = default;
            int fields = 0;
            int removedObjects = 0;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(scenePath);
                CoreGameplayDemoController controller = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                if (controller.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture || !controller.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete authored departure preview in " + scenePath);
                Transform content = controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                RectTransform routes = content.Find("Route Selection") as RectTransform;
                RectTransform setup = content.Find("Voyage Setup") as RectTransform;
                if (routes == null || setup == null)
                    throw new InvalidOperationException("Missing authored departure panels in " + scenePath);
                setupId = CleanDepartureObjectId(setup, blocks, scenePath);
                foreach (Transform item in setup.Cast<Transform>().Where(item => RemovedDepartureSetupNames.Contains(item.name)))
                {
                    removedRootTransforms.Add(CleanDepartureObjectId(item, blocks, scenePath));
                    foreach (Transform descendant in item.GetComponentsInChildren<Transform>(true))
                    {
                        removedObjects++;
                        removedIds.Add(CleanDepartureObjectId(descendant.gameObject, blocks, scenePath));
                        foreach (Component component in descendant.GetComponents<Component>())
                            removedIds.Add(CleanDepartureObjectId(component, blocks, scenePath));
                    }
                }

                // The removed subtree is explicitly allowed. Every retained component
                // may change only the known geometry/text fields, never its content.
                DepartureLayoutSnapshot[] snapshots = controller.GetComponentsInChildren<Component>(true)
                    .Where(component => !removedIds.Contains(CleanDepartureObjectId(component, blocks, scenePath)))
                    .Select(component =>
                    {
                        string[] allowed = component is RectTransform ? DepartureRectLayoutProperties :
                            component is Text ? DepartureTextLayoutProperties : Array.Empty<string>();
                        var serialized = new SerializedObject(component);
                        return new DepartureLayoutSnapshot
                        {
                            Component = component,
                            Json = EditorJsonUtility.ToJson(component),
                            Id = CleanDepartureObjectId(component, blocks, scenePath),
                            Values = allowed.Select(path => DepartureLayoutValue.Read(serialized.FindProperty(path))).ToArray(),
                        };
                    }).ToArray();

                if (setup.Find("Departure Crew Configuration Title") == null)
                    FitAuthoredDepartureShip(setup, scenePath);
                DepartureLayout.Apply(routes, setup);
                foreach (DepartureLayoutSnapshot snapshot in snapshots)
                {
                    var serialized = new SerializedObject(snapshot.Component);
                    DepartureLayoutValue[] current = snapshot.Values
                        .Select(value => DepartureLayoutValue.Read(serialized.FindProperty(value.Path))).ToArray();
                    for (int index = 0; index < current.Length; index++)
                    {
                        if (current[index].Yaml() == snapshot.Values[index].Yaml()) continue;
                        string block = blocks[snapshot.Id];
                        string key = current[index].Path.Split('.').Last();
                        var propertyPattern = new Regex(@"(?m)^([ \t]*" + Regex.Escape(key) + @": )[^\r\n]*");
                        if (propertyPattern.Matches(block).Count != 1)
                            throw new InvalidDataException("Expected one " + key + " in authored object " + snapshot.Id);
                        blocks[snapshot.Id] = propertyPattern.Replace(block, match => match.Groups[1].Value + current[index].Yaml());
                        fields++;
                    }
                    foreach (DepartureLayoutValue value in snapshot.Values) value.Restore(serialized);
                    if (snapshot.Values.Length > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (EditorJsonUtility.ToJson(snapshot.Component) != snapshot.Json)
                        throw new InvalidOperationException("Departure cleanup changed retained content on " +
                            snapshot.Component.name + " (" + snapshot.Component.GetType().Name + ") in " + scenePath);
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }

            string setupBlock = blocks[setupId];
            foreach (string id in removedRootTransforms)
            {
                var childPattern = new Regex(@"(?m)^  - \{fileID: " + Regex.Escape(id) + @"\}\r?\n");
                if (childPattern.Matches(setupBlock).Count != 1)
                    throw new InvalidDataException("Expected one setup child reference for removed node " + id + " in " + scenePath);
                setupBlock = childPattern.Replace(setupBlock, "");
            }
            blocks[setupId] = setupBlock;
            string after = blockPattern.Replace(before, match =>
                removedIds.Contains(match.Groups[2].Value) ? "" : blocks[match.Groups[2].Value]);
            foreach (string id in removedIds)
                if (Regex.IsMatch(after, @"\{fileID: " + Regex.Escape(id) + @"(?:[,}])"))
                    throw new InvalidDataException("A retained object references removed departure information " + id + " in " + scenePath);
            return new CleanDeparturePatch
            {
                Reference = reference, Path = absolute, Before = before, After = after,
                RemovedObjects = removedObjects, ChangedFields = fields,
            };
        }

        private static string CleanDepartureObjectId(UnityEngine.Object item, Dictionary<string, string> blocks, string scenePath)
        {
            if (item == null) throw new InvalidOperationException("Missing authored departure component in " + scenePath);
            string id = GlobalObjectId.GetGlobalObjectIdSlow(item).targetObjectId.ToString(CultureInfo.InvariantCulture);
            if (!blocks.ContainsKey(id))
                throw new InvalidDataException("Missing serialized authored object " + item.name + " (" + id + ") in " + scenePath);
            return id;
        }

        private static void FitAuthoredDepartureShip(RectTransform setup, string scenePath)
        {
            RectTransform hit = setup.Find("Inspect Departure Vessel") as RectTransform;
            RectTransform ship = hit?.Find("Starter Modular Ship") as RectTransform;
            if (ship == null)
                throw new InvalidOperationException("Missing complete authored ship hierarchy in " + scenePath);
            RawImage hull = ship.Cast<Transform>().Select(item => item.GetComponent<RawImage>())
                .SingleOrDefault(item => item != null && (item.name == "Starter Modular Ship Base" || item.name == "Starter Modular Ship Hull"));
            if (hull == null || hull.texture == null || hull.rectTransform.sizeDelta.x <= 0 || hull.rectTransform.sizeDelta.y <= 0)
                throw new InvalidOperationException("Missing fitted authored ship hull in " + scenePath);
            Vector2 fittedHull = UiImageAspect.FitSize(hull.texture, hull.uvRect, DepartureLayout.ShipPreviewSize);
            Vector2 previousHull = hull.rectTransform.sizeDelta;
            float scale = fittedHull.x / previousHull.x;
            if (Mathf.Abs(fittedHull.y / previousHull.y - scale) > .001f)
                throw new InvalidOperationException("Authored ship hull cannot be uniformly enlarged in " + scenePath);
            foreach (RectTransform layer in ship.GetComponentsInChildren<RectTransform>(true))
            {
                if (layer == ship) continue;
                if (layer.anchorMin != layer.anchorMax)
                    throw new InvalidOperationException("Stretched authored ship layer requires explicit migration in " + scenePath);
                layer.sizeDelta *= scale;
                layer.anchoredPosition *= scale;
            }
            ship.sizeDelta = DepartureLayout.ShipPreviewSize;
            ship.anchoredPosition = Vector2.zero;
        }
    }
}
