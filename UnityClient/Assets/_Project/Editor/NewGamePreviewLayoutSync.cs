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

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Transfers audited navigation/treasure geometry and settlement backdrop placement into saved references.
    /// Edits only named serialized properties: no scene opening, Canvas rebuilding,
    /// object replacement, event changes, text changes, or player-save access.
    /// </summary>
    public static class NewGamePreviewLayoutSync
    {
        private const string Folder = "Assets/_Project/Scenes/UIPreviewsForMe";
        private const string PaperAsset = "Assets/_Project/Presentation/EditablePreviewAssets/UI_Navigation_UISurfaces_journal_parchment_v1_0_0_1024_1024.sprite.asset";
        private static readonly Regex Documents = new Regex(
            @"(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+)\r?\n.*?(?=^--- !u!|\z)");
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Serializable] private sealed class Report
        {
            public bool applied;
            public List<Entry> scenes = new List<Entry>();
        }

        [Serializable] private sealed class Entry
        {
            public string path;
            public int changedProperties;
            public List<string> properties = new List<string>();
        }

        public static void InspectFromCommandLine() => Run(false);
        public static void ApplyFromCommandLine() => Run(true);
        public static void ApplyAndCaptureFromCommandLine()
        {
            Run(true);
            UIPreviewSceneAcceptance.CaptureEditableFromCommandLine();
        }

        private static void Run(bool apply)
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Preview layout synchronization requires stopped edit mode.");
            string project = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(project, Folder);
            string paperGuid = AssetDatabase.AssetPathToGUID(PaperAsset);
            Sprite paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperAsset);
            if (string.IsNullOrEmpty(paperGuid) || paper == null)
                throw new InvalidDataException("Missing persistent parchment sprite: " + PaperAsset);
            var report = new Report { applied = apply };
            var changes = new List<KeyValuePair<string, string>>();
            foreach (string path in Directory.GetFiles(folder, "*.unity", SearchOption.AllDirectories).OrderBy(p => p))
            {
                string source = File.ReadAllText(path);
                Dictionary<string, string> names = ObjectNames(source);
                bool navigation = names.Values.Contains("Navigation Chart");
                bool treasure = names.Values.Contains("Treasure Panel");
                bool settlement = names.Values.Contains("Settlement Hold");
                bool departure = names.Values.Contains("Seed Label");
                if (!navigation && !treasure && !settlement && !departure) continue;
                var entry = new Entry { path = path.Substring(project.Length + 1).Replace('\\', '/') };
                string updated = PatchScene(source, names, navigation, treasure, settlement, paperGuid, paper.rect.width / 340f, entry);
                entry.changedProperties = entry.properties.Count;
                report.scenes.Add(entry);
                if (updated != source) changes.Add(new KeyValuePair<string, string>(path, updated));
            }
            if (report.scenes.Count == 0) throw new InvalidOperationException("No saved navigation, treasure or settlement references were found.");
            // Validate every candidate before writing the first scene.
            if (apply)
            {
                string backupRoot = Path.Combine(project, "Artifacts/NewGamePreviewAudit20260930/sync-backup");
                foreach (var change in changes)
                {
                    string backup = Path.Combine(backupRoot, change.Key.Substring(project.Length + 1));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.Copy(change.Key, backup);
                }
                foreach (var change in changes) File.WriteAllText(change.Key, change.Value, new UTF8Encoding(false));
            }
            string output = Path.Combine(project, "Artifacts/NewGamePreviewAudit20260930/preview-layout-sync.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (apply) AssetDatabase.Refresh();
            Debug.Log("PREVIEW_LAYOUT_SYNC: " + (apply ? "applied " : "inspected ") + report.scenes.Count +
                " scenes, " + changes.Count + " changed; report " + output);
        }

        private static Dictionary<string, string> ObjectNames(string source)
        {
            var result = new Dictionary<string, string>();
            foreach (Match document in Documents.Matches(source))
            {
                if (document.Groups["type"].Value != "1") continue;
                Match name = Regex.Match(document.Value, @"(?m)^  m_Name: ([^\r\n]*)");
                if (name.Success) result.Add(document.Groups["id"].Value, name.Groups[1].Value.Trim('"'));
            }
            return result;
        }

        private static string PatchScene(string source, Dictionary<string, string> names,
            bool navigation, bool treasure, bool settlement, string paperGuid, float paperMultiplier, Entry entry)
        {
            return Documents.Replace(source, document =>
            {
                string block = document.Value;
                Match owner = Regex.Match(block, @"m_GameObject: \{fileID: (-?\d+)\}");
                if (!owner.Success || !names.TryGetValue(owner.Groups[1].Value, out string name)) return block;
                string type = document.Groups["type"].Value;
                if (type == "224")
                {
                    if (name == "Seed Label")
                    {
                        block = Set(block, "m_AnchoredPosition", V2(new Vector2(0f, 66f)), name, entry);
                        block = Set(block, "m_SizeDelta", V2(new Vector2(390f, 35f)), name, entry);
                    }
                    if (navigation && NavigationPanel(name, out Vector2 size, out Vector2 position))
                    {
                        Vector2 scale = new Vector2(PhaseSurfaceLayout.BackgroundSize.x / 1870f,
                            PhaseSurfaceLayout.BackgroundSize.y / 940f);
                        position = Vector2.Scale(position - new Vector2(5f, 5f), scale) + PhaseSurfaceLayout.BackgroundPosition;
                        block = Set(block, "m_LocalScale", V3(new Vector3(scale.x, scale.y, 1f)), name, entry);
                        block = Set(block, "m_AnchoredPosition", V2(position), name, entry);
                        block = Set(block, "m_SizeDelta", V2(size), name, entry);
                    }
                    if (treasure && TreasureRect(name, out size, out position))
                    {
                        block = Set(block, "m_LocalScale", V3(Vector3.one), name, entry);
                        block = Set(block, "m_AnchoredPosition", V2(position), name, entry);
                        block = Set(block, "m_SizeDelta", V2(size), name, entry);
                    }
                    if (settlement && name == "Settlement Hold")
                        block = Set(block, "m_AnchoredPosition", V2(new Vector2(0f, -45f)), name, entry);
                }
                if (type == "114" && navigation && block.Contains("m_FontData:") && NavigationInk(name, out Color ink))
                    block = Set(block, "m_Color", "{r: " + F(ink.r) + ", g: " + F(ink.g) + ", b: " + F(ink.b) + ", a: 1}", name, entry);
                if (type == "114" && navigation && block.Contains("m_PixelsPerUnitMultiplier:") &&
                    (name == "Navigation Chart" || name == "Navigation Controls" || name == "Navigation Journal" || name == "Voyage Journal Scroll"))
                {
                    block = Set(block, "m_Sprite", "{fileID: 21300000, guid: " + paperGuid + ", type: 2}", name, entry);
                    block = Set(block, "m_Type", "2", name, entry);
                    block = Set(block, "m_PixelsPerUnitMultiplier", F(paperMultiplier), name, entry);
                    block = Set(block, "m_Color", "{r: 0.72, g: 0.61, b: 0.44, a: " + (name == "Voyage Journal Scroll" ? "0.12}" : "1}"), name, entry);
                }
                if (type == "114" && treasure && name == "Treasure Route Detail" && block.Contains("m_FontData:"))
                {
                    block = Set(block, "m_FontSize", "18", name, entry, 4);
                    block = Set(block, "m_BestFit", "1", name, entry, 4);
                    block = Set(block, "m_MinSize", "18", name, entry, 4);
                    block = Set(block, "m_MaxSize", "18", name, entry, 4);
                }
                if (type == "114" && treasure && name == "Treasure Background" && block.Contains("m_UVRect:"))
                {
                    Rect uv = PhaseSurfaceLayout.BackgroundUv(new Vector2(1672f, 941f));
                    var pattern = new Regex(@"(?m)^  m_UVRect:\r?\n    serializedVersion: 2\r?\n    x: [^\r\n]+\r?\n    y: [^\r\n]+\r?\n    width: [^\r\n]+\r?\n    height: [^\r\n]+");
                    Match current = pattern.Match(block);
                    if (!current.Success) throw new InvalidDataException(entry.path + ": unsupported treasure background UV layout.");
                    string nl = block.Contains("\r\n") ? "\r\n" : "\n";
                    string replacement = "  m_UVRect:" + nl + "    serializedVersion: 2" + nl +
                        "    x: " + F(uv.x) + nl + "    y: " + F(uv.y) + nl +
                        "    width: " + F(uv.width) + nl + "    height: " + F(uv.height);
                    if (current.Value != replacement)
                    {
                        entry.properties.Add(name + ".m_UVRect");
                        block = pattern.Replace(block, replacement, 1);
                    }
                }
                return block;
            });
        }

        private static bool NavigationPanel(string name, out Vector2 size, out Vector2 position)
        {
            switch (name)
            {
                case "Navigation Chart": size = new Vector2(860, 560); position = new Vector2(-500, 195); return true;
                case "Navigation Controls": size = new Vector2(860, 360); position = new Vector2(-500, -285); return true;
                case "Forward View Panel": size = new Vector2(980, 650); position = new Vector2(450, 150); return true;
                case "Navigation Journal": size = new Vector2(980, 270); position = new Vector2(450, -330); return true;
                default: size = position = Vector2.zero; return false;
            }
        }

        private static bool NavigationInk(string name, out Color ink)
        {
            string hex;
            switch (name)
            {
                case "Map Title": case "Map Course Hint": case "Navigation Title": case "Compass": case "Log Title": hex = "#694014"; break;
                case "Map Seed": case "Forecast": case "Voyage Cargo Summary": hex = "#4F4A3E"; break;
                case "Distance Label": case "Cargo Status": case "Voyage Log": hex = "#2E281E"; break;
                default: ink = Color.clear; return false;
            }
            ColorUtility.TryParseHtmlString(hex, out ink);
            return true;
        }

        private static bool TreasureRect(string name, out Vector2 size, out Vector2 position)
        {
            size = position = Vector2.zero;
            if (name == "Treasure Background") { size = PhaseSurfaceLayout.BackgroundSize; position = PhaseSurfaceLayout.BackgroundPosition; return true; }
            if (name == "Treasure Panel") { size = new Vector2(1820, 650); position = new Vector2(0, PhaseSurfaceLayout.StagePositionY); return true; }
            if (name == "Treasure Route Reading Desk") { size = new Vector2(1240, 126); position = new Vector2(0, -404); return true; }
            if (name == "Treasure Selected Route") { size = new Vector2(1160, 28); position = new Vector2(0, 46); return true; }
            if (name == "Treasure Route Detail") { size = new Vector2(1160, 88); position = new Vector2(0, -16); return true; }
            for (int index = 0; index < 3; index++)
            {
                TreasureChartDisplayLayout display = TreasureChartDisplayLayout.For(index);
                if (name == "Treasure Route " + index) { size = display.Size; position = display.Position; return true; }
                if (name == "Treasure Route Name " + index) { size = display.LabelSize; position = display.LabelPosition - display.Position; return true; }
                if (name == "Treasure Route Art " + index) { size = display.Size - Vector2.one * 5f; return true; }
                if (name == "Treasure Chart Paper Contact " + index) { size = display.Size; position = new Vector2(0, -2); return true; }
                if (name == "Treasure Chart Paper Edge " + index || name == "Treasure Route Hover " + index) { size = display.Size; return true; }
            }
            return false;
        }

        private static string Set(string block, string property, string value, string name, Entry entry, int spaces = 2)
        {
            string indent = new string(' ', spaces);
            var pattern = new Regex(@"(?m)^" + indent + Regex.Escape(property) + @": [^\r\n]+");
            Match current = pattern.Match(block);
            if (!current.Success) throw new InvalidDataException(entry.path + ": missing " + name + "." + property);
            string replacement = indent + property + ": " + value;
            if (current.Value == replacement) return block;
            entry.properties.Add(name + "." + property);
            return pattern.Replace(block, replacement, 1);
        }

        private static string F(float value) => value.ToString("0.########", Invariant);
        private static string V2(Vector2 value) => "{x: " + F(value.x) + ", y: " + F(value.y) + "}";
        private static string V3(Vector3 value) => "{x: " + F(value.x) + ", y: " + F(value.y) + ", z: " + F(value.z) + "}";
    }
}
