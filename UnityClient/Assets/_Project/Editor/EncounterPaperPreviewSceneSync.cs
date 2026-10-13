using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    /// <summary>Updates only embedded event presentation in saved, authored voyage previews.</summary>
    public static class EncounterPaperPreviewSceneSync
    {
        private static readonly Regex Blocks = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");

        // -executeMethod ShatteredSail.Editor.EncounterPaperPreviewSceneSync.SyncFromCommandLine
        // Optional: -encounterPaperBackupPath and -encounterPaperReportPath.
        public static void SyncFromCommandLine()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/EncounterPaper20261011"));
            Sync(CommandLinePath("-encounterPaperBackupPath", Path.Combine(root, "scene-backup")),
                CommandLinePath("-encounterPaperReportPath", Path.Combine(root, "scene-sync.json")));
        }

        public static void Sync(string backupRoot, string reportPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Encounter paper synchronization requires stopped edit mode.");
            if (string.IsNullOrWhiteSpace(backupRoot) || string.IsNullOrWhiteSpace(reportPath))
                throw new ArgumentException("Provide a backup directory and report path for preview synchronization.");
            backupRoot = Path.GetFullPath(backupRoot);
            reportPath = Path.GetFullPath(reportPath);
            string[] paths = new[] { GameplayPreviewSceneBuilder.SceneFolder }
                .SelectMany(folder => Directory.GetFiles(AbsolutePath(folder), "*.unity", SearchOption.AllDirectories)
                    .Select(path => folder + "/" + path.Substring(AbsolutePath(folder).Length + 1).Replace('\\', '/')))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Patch[] patches = paths.Select(PreparePatch).Where(patch => patch != null).ToArray();
            if (patches.Length == 0)
                throw new InvalidOperationException("No authored embedded encounters found; no scenes were written.");

            // Prepare and validate all candidates before writing. Never rebuild a
            // saved sample or serialize a whole scene over unrelated authored work.
            foreach (Patch patch in patches)
                if (!File.ReadAllBytes(patch.Absolute).SequenceEqual(patch.BeforeBytes))
                    throw new IOException("Authored encounter changed during preparation: " + patch.Path);
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
                scannedScenes = paths.Length, matchedScenes = patches.Length, backupRoot = backupRoot,
                preserved = "Authored event descriptions, illustration textures, choices, callbacks, gameplay values, non-event blocks and asset GUIDs remain unchanged. Only the embedded themed frame is removed; event geometry, text formatting, illustration UV and scrolling presentation follow the runtime layout. The decision footer follows whether the option list scrolls. Known 5x4 event atlas cells are recovered only after exact old/new UV matching; custom UVs are cropped within their existing source region.",
            };
            foreach (Patch patch in patches)
            {
                byte[] afterBytes = Encoding.UTF8.GetBytes(patch.After);
                bool changed = patch.Before != patch.After;
                if (changed)
                {
                    File.WriteAllBytes(patch.Absolute, afterBytes);
                    if (!File.ReadAllBytes(patch.Absolute).SequenceEqual(afterBytes))
                        throw new IOException("Encounter paper write verification failed: " + patch.Path);
                    report.updatedScenes++;
                }
                report.embeddedCards += patch.Cards;
                report.removedFrames += patch.RemovedFrames;
                report.scenes.Add(new SceneResult
                {
                    path = patch.Path, updated = changed, embeddedCards = patch.Cards,
                    removedFrames = patch.RemovedFrames, removedSerializedObjects = patch.RemovedObjects,
                    changedPresentationBlocks = patch.ChangedBlocks, beforeSha256 = Hash(patch.BeforeBytes),
                    recoveredAtlasIllustrations = patch.RecoveredAtlasIllustrations,
                    preservedCustomIllustrationCrops = patch.PreservedCustomIllustrationCrops,
                    afterSha256 = Hash(afterBytes), metaSha256 = File.Exists(patch.Absolute + ".meta")
                        ? Hash(File.ReadAllBytes(patch.Absolute + ".meta")) : "",
                });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("ENCOUNTER_PAPER_PREVIEW_SYNC_OK: updated " + report.updatedScenes +
                " authored scenes; removed " + report.removedFrames + " themed frames; report=" + reportPath);
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
            Dictionary<string, string> blocks = Blocks.Matches(before).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            var edits = new Dictionary<string, string>();
            var removedIds = new HashSet<string>();
            int cards = 0, removedFrames = 0, recoveredAtlasIllustrations = 0, preservedCustomIllustrationCrops = 0;
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(path);
                RectTransform[] encounters = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(IsEmbeddedCard).ToArray();
                cards = encounters.Length;
                foreach (RectTransform card in encounters)
                {
                    RectTransform content = card.parent.parent.parent as RectTransform;
                    Canvas canvas = content.GetComponentInParent<Canvas>();
                    CanvasScaler scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
                    if (canvas == null || scaler == null || scaler.referenceResolution.x <= 0 || scaler.referenceResolution.y <= 0)
                        throw new InvalidDataException("Missing reference Canvas for encounter in " + path);
                    // Batch-mode overlay dimensions can be zero. Only this
                    // disposable preview uses the authored reference resolution.
                    scaler.enabled = false;
                    canvas.renderMode = RenderMode.WorldSpace;
                    RectTransform canvasRect = (RectTransform)canvas.transform;
                    canvasRect.localScale = Vector3.one;
                    canvasRect.sizeDelta = scaler.referenceResolution;
                    canvasRect.ForceUpdateRectTransforms();

                    RectTransform frame = DirectChild(card, "Encounter Themed Frame");
                    if (frame != null)
                    {
                        if (frame.childCount != 0 || frame.GetComponent<RawImage>() == null ||
                            frame.GetComponents<Component>().Any(component => !(component is RectTransform) &&
                                !(component is CanvasRenderer) && !(component is RawImage)))
                            throw new InvalidDataException("Unexpected authored components on retired frame in " + path);
                        string parentId = ObjectId(card, blocks, path), frameId = ObjectId(frame, blocks, path);
                        var childReference = new Regex(@"(?m)^  - \{fileID: " + Regex.Escape(frameId) + @"\}\r?\n");
                        string parentBlock = blocks[parentId];
                        if (childReference.Matches(parentBlock).Count != 1)
                            throw new InvalidDataException("Expected one themed frame child reference in " + path);
                        edits[parentId] = childReference.Replace(parentBlock, "");
                        removedIds.Add(ObjectId(frame.gameObject, blocks, path));
                        foreach (Component component in frame.GetComponents<Component>())
                            removedIds.Add(ObjectId(component, blocks, path));
                        UnityEngine.Object.DestroyImmediate(frame.gameObject);
                        removedFrames++;
                    }
                    NavigationDashboardLayout[] layouts = content.GetComponentsInChildren<NavigationDashboardLayout>(true)
                        .Where(layout => layout.transform.parent == content).ToArray();
                    if (layouts.Length != 1)
                        throw new InvalidDataException("Expected one authored dashboard layout in " + path);
                    content.ForceUpdateRectTransforms();
                    layouts[0].Apply();
                    RawImage[] illustrations = card.GetComponentsInChildren<RawImage>(true)
                        .Where(image => image.name == "Demo Event Art").ToArray();
                    if (illustrations.Length != 1)
                        throw new InvalidDataException("Expected one authored event illustration in " + path);
                    if (ReframeIllustration(illustrations[0])) recoveredAtlasIllustrations++;
                    else preservedCustomIllustrationCrops++;
                    if (DirectChild(card, "Encounter Parchment") == null || DirectChild(card, "Encounter Paper Content") == null ||
                        DirectChild(card, "Encounter Themed Frame") != null)
                        throw new InvalidDataException("Embedded event must retain paper and content without themed frame in " + path);

                    foreach (RectTransform rect in card.GetComponentsInChildren<RectTransform>(true))
                    {
                        string id = ObjectId(rect, blocks, path);
                        string block = edits.TryGetValue(id, out string existing) ? existing : blocks[id];
                        block = VectorField(block, "m_LocalPosition", rect.localPosition, path);
                        block = VectorField(block, "m_LocalScale", rect.localScale, path);
                        block = VectorField(block, "m_AnchorMin", rect.anchorMin, path);
                        block = VectorField(block, "m_AnchorMax", rect.anchorMax, path);
                        block = VectorField(block, "m_AnchoredPosition", rect.anchoredPosition, path);
                        block = VectorField(block, "m_SizeDelta", rect.sizeDelta, path);
                        block = VectorField(block, "m_Pivot", rect.pivot, path);
                        if (block != blocks[id]) edits[id] = block;
                    }
                    foreach (Text label in card.GetComponentsInChildren<Text>(true))
                    {
                        string id = ObjectId(label, blocks, path);
                        string block = blocks[id];
                        block = ScalarField(block, "m_FontSize", label.fontSize, 4, path);
                        block = ScalarField(block, "m_FontStyle", (int)label.fontStyle, 4, path);
                        block = ScalarField(block, "m_BestFit", label.resizeTextForBestFit ? 1 : 0, 4, path);
                        block = ScalarField(block, "m_MinSize", label.resizeTextMinSize, 4, path);
                        block = ScalarField(block, "m_MaxSize", label.resizeTextMaxSize, 4, path);
                        block = ScalarField(block, "m_Alignment", (int)label.alignment, 4, path);
                        block = ScalarField(block, "m_HorizontalOverflow", (int)label.horizontalOverflow, 4, path);
                        block = ScalarField(block, "m_VerticalOverflow", (int)label.verticalOverflow, 4, path);
                        block = ScalarField(block, "m_LineSpacing", label.lineSpacing, 4, path);
                        if (label.name == "Encounter Decision Footer") block = FooterField(block, label.text, path);
                        if (block != blocks[id]) edits[id] = block;
                    }
                    foreach (RawImage image in card.GetComponentsInChildren<RawImage>(true))
                    {
                        string id = ObjectId(image, blocks, path);
                        string block = RectField(blocks[id], "m_UVRect", image.uvRect, path);
                        if (block != blocks[id]) edits[id] = block;
                    }
                    foreach (ScrollRect scroll in card.GetComponentsInChildren<ScrollRect>(true))
                    {
                        string id = ObjectId(scroll, blocks, path);
                        string block = ScalarField(blocks[id], "m_Horizontal", scroll.horizontal ? 1 : 0, 2, path);
                        block = ScalarField(block, "m_Vertical", scroll.vertical ? 1 : 0, 2, path);
                        block = ScalarField(block, "m_ScrollSensitivity", scroll.scrollSensitivity, 2, path);
                        if (block != blocks[id]) edits[id] = block;
                    }
                }
            }
            finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
            if (cards == 0) return null;
            string after = Blocks.Replace(before, match =>
            {
                string id = match.Groups[2].Value;
                return removedIds.Contains(id) ? "" : edits.TryGetValue(id, out string edit) ? edit : match.Value;
            });
            foreach (string id in removedIds)
                if (Regex.IsMatch(after, @"\{fileID: " + Regex.Escape(id) + @"(?:[,}])"))
                    throw new InvalidDataException("Retained object references removed frame object " + id + " in " + path);
            Dictionary<string, string> retained = Blocks.Matches(after).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            if (retained.Count != blocks.Count - removedIds.Count || blocks.Any(pair => !removedIds.Contains(pair.Key) &&
                (!retained.TryGetValue(pair.Key, out string block) || block != (edits.TryGetValue(pair.Key, out string edit) ? edit : pair.Value))))
                throw new InvalidDataException("Retained authored encounter blocks changed unexpectedly in " + path);
            return new Patch
            {
                Path = path, Absolute = absolute, BeforeBytes = beforeBytes, Before = before, After = after,
                Cards = cards, RemovedFrames = removedFrames, RemovedObjects = removedIds.Count, ChangedBlocks = edits.Count,
                RecoveredAtlasIllustrations = recoveredAtlasIllustrations,
                PreservedCustomIllustrationCrops = preservedCustomIllustrationCrops,
            };
        }

        private static bool IsEmbeddedCard(RectTransform item)
        {
            string[] hierarchy = { "Encounter Card", "Forward Viewport", "Forward View Panel", "Phase Content" };
            Transform cursor = item;
            foreach (string name in hierarchy)
            {
                if (cursor == null || cursor.name != name) return false;
                cursor = cursor.parent;
            }
            return true;
        }

        private static RectTransform DirectChild(Transform parent, string name)
        {
            Transform[] children = parent.Cast<Transform>().Where(child => child.name == name).ToArray();
            if (children.Length > 1) throw new InvalidDataException("Ambiguous encounter child " + name);
            return children.Length == 0 ? null : children[0] as RectTransform;
        }

        private static bool ReframeIllustration(RawImage image)
        {
            string texturePath = AssetDatabase.GetAssetPath(image.texture);
            string[] knownAtlases =
            {
                "Assets/_Project/Resources/LegacyDemo/shattered_sail_event_sheet_v2.png",
                "Assets/_Project/Resources/LegacyDemo/golden_tide_event_sheet_v2.png",
                "Assets/_Project/Resources/LegacyDemo/moon_tide_event_sheet_v2.png",
                "Assets/_Project/Resources/TreasureHunt/treasure_hunt_event_sheet.png",
            };
            if (knownAtlases.Contains(texturePath, StringComparer.Ordinal))
            {
                for (int row = 0; row < 4; row++)
                for (int column = 0; column < 5; column++)
                {
                    Rect cell = LegacyDemoArtCatalog.CellUv(column, row, 5, 4);
                    Rect inset = new Rect(cell.x + cell.width * .05f, cell.y + cell.height * .065f,
                        cell.width * .9f, cell.height * .87f);
                    Rect previous = CoverUv(image.texture, inset, 238f / 280f);
                    Rect current = CoverUv(image.texture, inset, 4f / 3f);
                    if (!SameUv(image.uvRect, previous) && !SameUv(image.uvRect, current)) continue;
                    image.uvRect = current;
                    return true;
                }
            }
            image.uvRect = CoverUv(image.texture, image.uvRect, 4f / 3f);
            return false;
        }

        private static bool SameUv(Rect left, Rect right) => Mathf.Abs(left.x - right.x) < .00001f &&
            Mathf.Abs(left.y - right.y) < .00001f && Mathf.Abs(left.width - right.width) < .00001f &&
            Mathf.Abs(left.height - right.height) < .00001f;

        private static Rect CoverUv(Texture texture, Rect source, float targetAspect)
        {
            if (texture == null) return source;
            float aspect = texture.width * source.width / (texture.height * source.height);
            if (aspect > targetAspect)
            {
                float width = source.width * targetAspect / aspect;
                return new Rect(source.center.x - width * .5f, source.y, width, source.height);
            }
            float height = source.height * aspect / targetAspect;
            return new Rect(source.x, source.center.y - height * .5f, source.width, height);
        }

        private static string ScalarField(string block, string name, float value, int indent, string path)
        {
            var field = new Regex(@"(?m)^(?<prefix>" + new string(' ', indent) + Regex.Escape(name) + @": )(?<value>[^\r\n]+)");
            Match match = field.Match(block);
            if (field.Matches(block).Count != 1 || !float.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float previous) || float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("Invalid presentation scalar " + name + " in " + path);
            if (Mathf.Abs(previous - value) <= .00001f) return block;
            return block.Remove(match.Index, match.Length).Insert(match.Index,
                match.Groups["prefix"].Value + value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string FooterField(string block, string value, string path)
        {
            var field = new Regex(@"(?m)^(?<prefix>  m_Text: )[^\r\n]*");
            Match match = field.Match(block);
            if (field.Matches(block).Count != 1 || (value != "上下滚动查看更多" && value != "选择可行方案继续" && value != "略过不耗时"))
                throw new InvalidDataException("Unexpected embedded decision footer in " + path);
            var quoted = new StringBuilder("\"");
            foreach (char letter in value) quoted.Append("\\u").Append(((int)letter).ToString("X4", CultureInfo.InvariantCulture));
            quoted.Append('"');
            return block.Remove(match.Index, match.Length).Insert(match.Index, match.Groups["prefix"].Value + quoted);
        }

        private static string VectorField(string block, string name, Vector2 value, string path) =>
            ValuesField(block, name, new[] { "x", "y" }, new[] { value.x, value.y }, path);
        private static string VectorField(string block, string name, Vector3 value, string path) =>
            ValuesField(block, name, new[] { "x", "y", "z" }, new[] { value.x, value.y, value.z }, path);

        private static string RectField(string block, string name, Rect value, string path)
        {
            var field = new Regex(@"(?m)^  " + Regex.Escape(name) + @":\r?\n    serializedVersion: 2\r?\n    x: [^\r\n]+\r?\n    y: [^\r\n]+\r?\n    width: [^\r\n]+\r?\n    height: [^\r\n]+");
            Match match = field.Match(block);
            if (field.Matches(block).Count != 1)
                throw new InvalidDataException("Invalid UV rectangle " + name + " in " + path);
            string changed = match.Value;
            changed = ScalarField(changed, "x", value.x, 4, path);
            changed = ScalarField(changed, "y", value.y, 4, path);
            changed = ScalarField(changed, "width", value.width, 4, path);
            changed = ScalarField(changed, "height", value.height, 4, path);
            return block.Remove(match.Index, match.Length).Insert(match.Index, changed);
        }

        private static string ValuesField(string block, string name, string[] axes, float[] values, string path)
        {
            var field = new Regex(@"(?m)^(?<prefix>  " + Regex.Escape(name) + @": \{)(?<value>[^}\r\n]*)(?<suffix>\})");
            Match match = field.Match(block);
            if (field.Matches(block).Count != 1 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("Invalid layout field " + name + " in " + path);
            string[] parts = match.Groups["value"].Value.Split(',');
            bool equal = parts.Length == axes.Length;
            for (int i = 0; equal && i < axes.Length; i++)
            {
                string[] pair = parts[i].Trim().Split(':');
                equal = pair.Length == 2 && pair[0] == axes[i] && float.TryParse(pair[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float previous) && Mathf.Abs(previous - values[i]) <= .00001f;
            }
            if (equal) return block;
            string replacement = match.Groups["prefix"].Value + string.Join(", ", axes.Select((axis, i) =>
                axis + ": " + values[i].ToString("R", CultureInfo.InvariantCulture))) + match.Groups["suffix"].Value;
            return block.Remove(match.Index, match.Length).Insert(match.Index, replacement);
        }

        private static string ObjectId(UnityEngine.Object item, Dictionary<string, string> blocks, string path)
        {
            if (item == null) throw new InvalidDataException("Missing authored encounter component in " + path);
            string id = GlobalObjectId.GetGlobalObjectIdSlow(item).targetObjectId.ToString(CultureInfo.InvariantCulture);
            if (!blocks.ContainsKey(id))
                throw new InvalidDataException("Missing serialized encounter object " + item.name + " (" + id + ") in " + path);
            return id;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
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
            public int Cards, RemovedFrames, RemovedObjects, ChangedBlocks, RecoveredAtlasIllustrations, PreservedCustomIllustrationCrops;
        }
        [Serializable]
        private sealed class SyncReport
        {
            public int scannedScenes, matchedScenes, updatedScenes, embeddedCards, removedFrames;
            public string backupRoot, preserved;
            public List<SceneResult> scenes = new List<SceneResult>();
        }
        [Serializable]
        private sealed class SceneResult
        {
            public string path, beforeSha256, afterSha256, metaSha256;
            public bool updated;
            public int embeddedCards, removedFrames, removedSerializedObjects, changedPresentationBlocks;
            public int recoveredAtlasIllustrations, preservedCustomIllustrationCrops;
        }
    }
}
