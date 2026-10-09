using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Update only scene-label decoration, preserving all authored layout and controls.</summary>
    public static class UiLabelBackingSceneSync
    {
        private const string OutlineGuid = "e19747de3f5aca642ab2be37e372fb86";
        private static readonly Regex ObjectPattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");

        public static void SyncFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Scene-label cleanup requires stopped edit mode.");
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(project, "Assets/_Project/Scenes");
            var styleRoot = new GameObject("Scene Label Style Root", typeof(RectTransform));
            var styleHost = new GameObject("Scene Label Style", typeof(RectTransform), typeof(Text));
            styleHost.transform.SetParent(styleRoot.transform, false);
            ScenePatch[] changes;
            try
            {
                Text style = styleHost.GetComponent<Text>();
                UiLabelBacking.Apply(style);
                Outline outline = style.GetComponent<Outline>();
                if (outline == null || !outline.enabled)
                    throw new InvalidOperationException("Runtime scene-label styling did not supply an outline.");
                changes = Directory.GetFiles(directory, "*.unity", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => PreparePatch(path, style.color, outline)).Where(patch => patch != null).ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(styleRoot); }

            // Validate the complete batch before writing; no scene is regenerated or reserialized.
            foreach (ScenePatch change in changes)
                if (File.ReadAllText(change.Path) != change.Before)
                    throw new IOException("Scene changed during label cleanup: " + change.Path);
            int updated = 0;
            foreach (ScenePatch change in changes)
            {
                if (change.Before == change.After) continue;
                string backup = Path.Combine(project, "Artifacts/SceneLabelCleanup20261007/scene-backup",
                    change.Path.Substring(directory.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(change.Path, backup);
                File.WriteAllText(change.Path, change.After, new UTF8Encoding(false));
                updated++;
            }
            AssetDatabase.Refresh();
            Debug.Log("UI_LABEL_BACKING_SYNC_OK: verified " + changes.Length + "; updated " + updated +
                " authored scenes; " + changes.Sum(change => change.LabelCount) + " labels styled; " +
                changes.Sum(change => change.BackingCount) + " label rectangles disabled.");
        }

        public static void SyncAndCaptureFromCommandLine()
        {
            SyncFromCommandLine();
            UIPreviewSceneAcceptance.CaptureCleanSceneLabelsFromCommandLine();
        }

        private sealed class ScenePatch
        {
            public string Path, Before, After;
            public int LabelCount, BackingCount;
        }

        private sealed class YamlObject
        {
            public string Id, Value;
            public int ClassId;
            public string Reference => "{fileID: " + Id + "}";
            public string Read(string key)
            {
                Match match = Regex.Match(Value, @"(?m)^[ \t]*" + Regex.Escape(key) + @": ([^\r\n]*)");
                if (!match.Success) throw new InvalidDataException("Missing " + key + " on object " + Id);
                return match.Groups[1].Value;
            }
            public void Set(string key, string value)
            {
                var pattern = new Regex(@"(?m)^([ \t]*" + Regex.Escape(key) + @": )[^\r\n]*");
                if (pattern.Matches(Value).Count != 1)
                    throw new InvalidDataException("Expected exactly one " + key + " on object " + Id);
                Value = pattern.Replace(Value, match => match.Groups[1].Value + value);
            }
        }

        private static ScenePatch PreparePatch(string path, Color labelColor, Outline style)
        {
            string before = File.ReadAllText(path);
            if (!Regex.IsMatch(before, @"(?m)^  m_Name: (Port Label |Port Hub Hint|Tavern Crew (Name|Status) |Card Workshop Chest Name |Component Shop (Offer Suitability |Plaque (Name|Status|Writing Area) )|Battle Card Cost Rule|Battle Curse (Panel|Status))"))
                return null;
            var blocks = ObjectPattern.Matches(before).Cast<Match>().Select(match => new YamlObject
            {
                ClassId = int.Parse(match.Groups[1].Value), Id = match.Groups[2].Value, Value = match.Value,
            }).ToList();
            YamlObject[] originalBlocks = blocks.ToArray();
            YamlObject[] objects = blocks.Where(block => block.ClassId == 1).ToArray();
            var patch = new ScenePatch { Path = path, Before = before };
            string newline = before.Contains("\r\n") ? "\r\n" : "\n";
            long nextId = 9000000000000000L;
            try
            {
                foreach (YamlObject host in objects)
                {
                    string name = host.Read("m_Name");
                    if (name.StartsWith("Component Shop Plaque Writing Area ", StringComparison.Ordinal) ||
                        name == "Battle Card Cost Rule Panel" || name == "Battle Curse Panel")
                    {
                        DisableImage(blocks, host);
                        patch.BackingCount++;
                        continue;
                    }
                    if (!IsSceneLabel(name)) continue;
                    YamlObject text = Component(blocks, host, 114, "UnityEngine.UI.Text");
                    YamlObject rect = Component(blocks, host, 224);
                    YamlObject backing = objects.SingleOrDefault(candidate =>
                        candidate.Read("m_Name") == name + " Backing" &&
                        Component(blocks, candidate, 224).Read("m_Father") == rect.Read("m_Father"));
                    if (backing != null) { DisableImage(blocks, backing); patch.BackingCount++; }
                    bool keepAccent = name.StartsWith("Tavern Crew Status ", StringComparison.Ordinal) ||
                        name.StartsWith("Card Workshop Chest Name ", StringComparison.Ordinal) ||
                        name.StartsWith("Component Shop Plaque ", StringComparison.Ordinal) ||
                        name == "Battle Card Cost Rule" || name == "Battle Curse Status";
                    if (!keepAccent) text.Set("m_Color", ColorValue(labelColor));
                    List<YamlObject> effects = blocks.Where(block => block.ClassId == 114 &&
                        block.Read("m_GameObject") == host.Reference &&
                        (block.Value.Contains("UnityEngine.UI.Outline") || block.Value.Contains("UnityEngine.UI.Shadow"))).ToList();
                    YamlObject outline = effects.FirstOrDefault(block => block.Value.Contains("UnityEngine.UI.Outline"));
                    if (outline == null)
                    {
                        while (blocks.Any(block => block.Id == nextId.ToString(CultureInfo.InvariantCulture))) nextId++;
                        outline = NewOutline((nextId++).ToString(CultureInfo.InvariantCulture), host, newline);
                        blocks.Add(outline);
                        var componentPattern = new Regex(@"(?m)^  - component: \{fileID: -?\d+\}[^\r\n]*\r?\n");
                        Match last = componentPattern.Matches(host.Value).Cast<Match>().Last();
                        host.Value = host.Value.Insert(last.Index + last.Length, "  - component: " + outline.Reference + newline);
                    }
                    foreach (YamlObject effect in effects)
                        effect.Set("m_Enabled", effect == outline ? "1" : "0");
                    outline.Set("m_Enabled", "1");
                    outline.Set("m_EffectColor", ColorValue(style.effectColor));
                    outline.Set("m_EffectDistance", "{x: " + Number(style.effectDistance.x) + ", y: " + Number(style.effectDistance.y) + "}");
                    outline.Set("m_UseGraphicAlpha", style.useGraphicAlpha ? "1" : "0");
                    patch.LabelCount++;
                }
            }
            catch (Exception exception) { throw new InvalidDataException("Unable to clean scene labels in " + path, exception); }
            if (patch.LabelCount == 0 && patch.BackingCount == 0) return null;
            var byId = blocks.ToDictionary(block => block.Id);
            patch.After = ObjectPattern.Replace(before, match => byId[match.Groups[2].Value].Value);
            // New outline blocks have no scene-root or hierarchy effects.
            foreach (YamlObject added in blocks.Skip(originalBlocks.Length)) patch.After += added.Value;
            return patch;
        }

        private static bool IsSceneLabel(string name)
        {
            if (name.EndsWith(" Backing", StringComparison.Ordinal)) return false;
            return name == "Battle Card Cost Rule" || name == "Battle Curse Status" ||
                name == "Port Hub Hint" || name.StartsWith("Port Label ", StringComparison.Ordinal) ||
                name.StartsWith("Tavern Crew Name ", StringComparison.Ordinal) ||
                name.StartsWith("Tavern Crew Status ", StringComparison.Ordinal) ||
                name.StartsWith("Card Workshop Chest Name ", StringComparison.Ordinal) ||
                name.StartsWith("Component Shop Offer Suitability ", StringComparison.Ordinal) ||
                name.StartsWith("Component Shop Plaque Name ", StringComparison.Ordinal) ||
                name.StartsWith("Component Shop Plaque Status ", StringComparison.Ordinal);
        }

        private static YamlObject Component(List<YamlObject> blocks, YamlObject host, int classId, string marker = null)
        {
            return blocks.Single(block => block.ClassId == classId && block.Read("m_GameObject") == host.Reference &&
                (marker == null || block.Value.Contains(marker)));
        }

        private static void DisableImage(List<YamlObject> blocks, YamlObject host)
        {
            YamlObject image = blocks.SingleOrDefault(block => block.ClassId == 114 &&
                block.Read("m_GameObject") == host.Reference && block.Value.Contains("UnityEngine.UI.Image"));
            if (image == null) return;
            image.Set("m_Enabled", "0");
            image.Set("m_RaycastTarget", "0");
        }

        private static string Number(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
        private static string ColorValue(Color color) => "{r: " + Number(color.r) + ", g: " + Number(color.g) +
            ", b: " + Number(color.b) + ", a: " + Number(color.a) + "}";

        private static YamlObject NewOutline(string id, YamlObject host, string newline)
        {
            return new YamlObject
            {
                ClassId = 114, Id = id,
                Value = string.Join(newline, new[]
                {
                    "--- !u!114 &" + id, "MonoBehaviour:", "  m_ObjectHideFlags: 0",
                    "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
                    "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: " + host.Reference, "  m_Enabled: 1",
                    "  m_EditorHideFlags: 0", "  m_Script: {fileID: 11500000, guid: " + OutlineGuid + ", type: 3}",
                    "  m_Name: ", "  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Outline",
                    "  m_EffectColor: {r: 0, g: 0, b: 0, a: 1}", "  m_EffectDistance: {x: 1, y: -1}",
                    "  m_UseGraphicAlpha: 1", "",
                }),
            };
        }
    }
}
