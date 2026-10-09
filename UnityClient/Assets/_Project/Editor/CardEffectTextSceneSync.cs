using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    /// <summary>
    /// Migrate saved card text surfaces without refreshing their controller or replacing the room.
    /// Run in the validation copy, then copy back only the scenes listed as changed in the report.
    /// </summary>
    public static class CardEffectTextSceneSync
    {
        private const string SceneRoot = "Assets/_Project/Scenes";
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Serializable] private sealed class Report
        {
            public string project;
            public string generatedAtUtc;
            public List<SceneResult> scenes = new List<SceneResult>();
        }

        [Serializable] private sealed class SceneResult
        {
            public string path;
            public string beforeSha256;
            public string afterSha256;
            public int changedSurfaces;
        }

        private sealed class AreaSpec
        {
            public Vector2 Size, Position;
            public int FontSize;
            public TextAnchor Alignment = TextAnchor.UpperLeft;
            public string LoreName;
            public int LoreSize = 20;
        }

        public static void SyncFromCommandLine()
        {
            RequireStoppedEditor();
            var report = new Report { project = Directory.GetParent(Application.dataPath).FullName,
                generatedAtUtc = DateTime.UtcNow.ToString("O") };
            foreach (string path in CandidatePaths())
            {
                var record = new SceneResult { path = path, beforeSha256 = Hash(path) };
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                try
                {
                    record.changedSurfaces = RepairScene(scene);
                    AssertScene(scene);
                    if (record.changedSurfaces > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
                        foreach (string method in new[] { "RepairSavedNavigationCanvasScale", "AssertSavedNavigationCanvasScale" })
                            Invoke(typeof(GameplayPreviewSceneBuilder), method, null, Path.GetFullPath(path));
                    }
                    record.afterSha256 = Hash(path);
                    report.scenes.Add(record);
                    SaveReport(report);
                }
                finally
                {
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.Refresh();
            SaveReport(report);
            Debug.Log("CARD_EFFECT_TEXT_SCENE_SYNC_OK: " + report.scenes.Count(item => item.changedSurfaces > 0) +
                " scenes, " + report.scenes.Sum(item => item.changedSurfaces) + " card surfaces. Report: " + ReportPath());
        }

        public static void ValidateFromCommandLine()
        {
            RequireStoppedEditor();
            int count = 0;
            foreach (string path in CandidatePaths())
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                try { AssertScene(scene); count++; }
                finally { if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            Debug.Log("CARD_EFFECT_TEXT_SCENE_VALIDATION_OK: " + count + " saved scenes.");
        }

        public static int RepairScene(Scene scene)
        {
            int changed = 0;
            foreach (CoreGameplayDemoController host in Hosts(scene))
            {
                if (!host.EditorPreviewPersistsGeneratedUi) continue;
                // All existing artwork keeps its pixels, UVs, colors and authored placement.
                // Only the illustrations within card faces may borrow space for longer text.
                var artwork = host.GetComponentsInChildren<RawImage>(true).ToDictionary(image => image,
                    image => EditorJsonUtility.ToJson(image) + (IsCardArt(image.name) ? string.Empty : EditorJsonUtility.ToJson(image.rectTransform)));
                foreach (Text label in host.GetComponentsInChildren<Text>(true).ToArray())
                {
                    if (label == null) continue;
                    if (IsBattleFace(label))
                    {
                        string before = BattleFaceSnapshot(label);
                        string original = label.text;
                        int categoryEnd = original.IndexOf("</size>\n", StringComparison.Ordinal);
                        if (categoryEnd >= 0)
                        {
                            categoryEnd += "</size>\n".Length;
                            label.text = original.Substring(0, categoryEnd) + FormatBattle(ReflowLegacy(original.Substring(categoryEnd)));
                        }
                        else label.text = FormatBattle(ReflowLegacy(original));
                        AssertSameWords(original, label.text, label.name);
                        label.horizontalOverflow = HorizontalWrapMode.Wrap;
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        Invoke(typeof(CoreGameplayDemoController), "RefreshBattleCardTextLayout", null, label);
                        if (before != BattleFaceSnapshot(label)) changed++;
                    }
                    else if (label.name == "Keyword Bubble Text" && label.transform.parent.name.StartsWith("Card Keyword Bubble ", StringComparison.Ordinal))
                    {
                        RectTransform bubble = label.transform.parent as RectTransform;
                        string before = BubbleSnapshot(bubble);
                        string original = label.text;
                        label.text = FormatBattle(ReflowLegacy(original));
                        AssertSameWords(original, label.text, label.name);
                        label.horizontalOverflow = HorizontalWrapMode.Wrap;
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        Invoke(typeof(CoreGameplayDemoController), "ConfigureCardKeywordBubbleGeometry", null,
                            bubble, bubble.anchoredPosition.x >= 0f, label.text.Contains("药品：") ? 110f : 0f);
                        if (before != BubbleSnapshot(bubble)) changed++;
                    }
                    else if (TryAreaSpec(label, out AreaSpec spec))
                    {
                        if (RepairArea(host, label, spec)) changed++;
                    }
                }
                foreach (var item in artwork)
                {
                    RawImage image = item.Key;
                    if (image == null || item.Value != EditorJsonUtility.ToJson(image) +
                        (IsCardArt(image.name) ? string.Empty : EditorJsonUtility.ToJson(image.rectTransform)))
                        throw new InvalidOperationException("Card text synchronization changed unrelated artwork: " + scene.path);
                }
            }
            return changed;
        }

        private static bool RepairArea(CoreGameplayDemoController host, Text old, AreaSpec spec)
        {
            string name = old.name;
            RectTransform existing = EffectViewport(old);
            Transform parent = existing == null ? old.transform.parent : existing.parent;
            string original = old.text;
            string formatted = CoreBattleCardText.FormatEffects(existing == null ? ReflowLegacy(original) : original);
            Text lore = string.IsNullOrEmpty(spec.LoreName) ? null : parent.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(item => item.name == spec.LoreName);
            string story = lore == null ? null : lore.text;
            Color storyColor = lore == null ? old.color : lore.color;
            Color color = old.color;
            bool geometryChanged = RepairAreaNeighbours(parent, name);
            if (existing != null && existing.sizeDelta == spec.Size && existing.anchoredPosition == spec.Position &&
                old.text == formatted && old.fontSize == spec.FontSize && !old.resizeTextForBestFit &&
                old.alignment == spec.Alignment && AreaLayoutFits(existing, old)) return geometryChanged;

            int sibling = existing == null ? old.transform.GetSiblingIndex() : existing.GetSiblingIndex();
            AssertSameWords(original, formatted, name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            else
            {
                Object.DestroyImmediate(old.gameObject);
                if (lore != null) Object.DestroyImmediate(lore.gameObject);
            }
            Text replacement = (Text)Invoke(typeof(CoreGameplayDemoController), "CreateCardEffectArea", host,
                name, parent, spec.Size, spec.Position, spec.FontSize, color, formatted, spec.Alignment,
                spec.LoreName, story, spec.LoreSize, (Color?)storyColor);
            RectTransform viewport = EffectViewport(replacement);
            viewport.SetSiblingIndex(sibling);
            AssertSameWords(original, replacement.text, name);
            return true;
        }

        private static bool RepairAreaNeighbours(Transform parent, string name)
        {
            bool changed = false;
            if (name.StartsWith("Workshop Card Description ", StringComparison.Ordinal))
            {
                string suffix = name.Substring("Workshop Card Description ".Length);
                changed |= SetRect(parent.Find("Workshop Card Art " + suffix) as RectTransform, new Vector2(66f, 66f), new Vector2(0f, 60f));
                RectTransform title = parent.Find("Workshop Card Name " + suffix) as RectTransform;
                if (title != null) changed |= SetRect(title, title.sizeDelta, new Vector2(0f, 10f));
            }
            else if (name.StartsWith("Upgrade Comparison Body ", StringComparison.Ordinal))
            {
                string suffix = name.Substring("Upgrade Comparison Body ".Length);
                RectTransform title = parent.Find("Upgrade Comparison Heading " + suffix) as RectTransform;
                if (title != null) changed |= SetRect(title, new Vector2(340f, 34f), new Vector2(title.anchoredPosition.x, 114f));
            }
            return changed;
        }

        private static bool TryAreaSpec(Text label, out AreaSpec spec)
        {
            spec = new AreaSpec();
            string name = label.name;
            if (name.StartsWith("Workshop Card Description ", StringComparison.Ordinal))
            {
                spec.Size = new Vector2(196f, 76f); spec.Position = new Vector2(0f, -44f);
                spec.FontSize = 12; spec.Alignment = TextAnchor.UpperCenter;
            }
            else if (name.StartsWith("Upgrade Comparison Body ", StringComparison.Ordinal))
            {
                spec.Size = new Vector2(340f, 182f); spec.Position = new Vector2(name.EndsWith("Current", StringComparison.Ordinal) ? 215f : 600f, 1f);
                spec.FontSize = 20;
            }
            else if (name == "Special Card Detail Effect")
            {
                spec.Size = new Vector2(710f, 244f); spec.Position = new Vector2(407f, -35f);
                spec.FontSize = 23; spec.LoreName = "Special Card Detail Lore"; spec.LoreSize = 21;
            }
            else if (name.EndsWith(" Special Card Effect", StringComparison.Ordinal))
            {
                RectTransform existing = EffectViewport(label);
                RectTransform page = (existing == null ? label.transform.parent : existing.parent) as RectTransform;
                spec.Size = new Vector2(page.rect.width - 204f, 160f); spec.Position = new Vector2(102f, -7f);
                spec.FontSize = label.fontSize == 20 ? 20 : 22;
                spec.LoreName = name.Substring(0, name.Length - "Effect".Length) + "Lore";
            }
            else return false;
            return true;
        }

        private static void AssertScene(Scene scene)
        {
            Canvas.ForceUpdateCanvases();
            foreach (CoreGameplayDemoController host in Hosts(scene).Where(item => item.EditorPreviewPersistsGeneratedUi))
                foreach (Text label in host.GetComponentsInChildren<Text>(true))
                {
                    bool face = IsBattleFace(label);
                    bool bubble = label.name == "Keyword Bubble Text" && label.transform.parent.name.StartsWith("Card Keyword Bubble ", StringComparison.Ordinal);
                    bool area = TryAreaSpec(label, out AreaSpec spec);
                    if (!face && !bubble && !area) continue;
                    if (label.resizeTextForBestFit || label.horizontalOverflow != HorizontalWrapMode.Wrap || !TextFits(label))
                        throw new InvalidOperationException("Incomplete card text at a fixed readable size: " + scene.path + " / " + label.name);
                    if (area)
                    {
                        RectTransform viewport = EffectViewport(label);
                        if (viewport == null || viewport.sizeDelta != spec.Size || viewport.anchoredPosition != spec.Position)
                            throw new InvalidOperationException("Saved card text lacks its current effect viewport: " + scene.path + " / " + label.name);
                        if (!AreaLayoutFits(viewport, label))
                            throw new InvalidOperationException("Saved card text lacks safe paragraph/content height or scrolling: " + scene.path + " / " + label.name);
                        foreach (Text paragraph in viewport.GetComponentsInChildren<Text>(true))
                            AssertParagraphMeshFits(paragraph, scene.path);
                    }
                }
        }

        private static string ReflowLegacy(string value)
        {
            // Remove the old fixed-character hard wraps, retaining paragraphs, headings and
            // rich keyword rows. The shared formatter restores the semantic effect breaks.
            string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string joined = Regex.Replace(normalized, @"(?<![。；！？：\n])\n(?!\n|<color=|同名牌|当前|基础（|药品：|治疗预览：|伤害 |格挡 |恢复 )", string.Empty);
            return Regex.Replace(joined, @"(当前 \d+ 人实际：)(?!\n)", "$1\n");
        }

        private static string FormatBattle(string value) => (string)Invoke(typeof(CoreGameplayDemoController), "FormatBattleCardDescription", null, value);
        private static RectTransform EffectViewport(Text label) => label.transform.parent != null &&
            label.transform.parent.name == label.name + " Content" ? label.transform.parent.parent as RectTransform : null;
        private static bool IsBattleFace(Text label) => label.name == "Label" && label.transform.parent != null &&
            Regex.IsMatch(label.transform.parent.name, @"^Battle Card \d+$");
        private static bool IsCardArt(string name) => Regex.IsMatch(name, @"^(Workshop )?Card Art \d+$");
        private static bool TextFits(Text label)
        {
            bool effectParagraph = label.transform.parent != null && label.transform.parent.name.EndsWith(" Content", StringComparison.Ordinal) &&
                label.transform.parent.parent != null && label.transform.parent.parent.name.EndsWith(" Viewport", StringComparison.Ordinal);
            float required = effectParagraph ? (float)Invoke(typeof(CoreGameplayDemoController), "MeasureCardTextHeight", null, label) : label.preferredHeight;
            return (!effectParagraph || label.verticalOverflow == VerticalWrapMode.Overflow) && required <= label.rectTransform.rect.height + .5f;
        }

        private static bool AreaLayoutFits(RectTransform viewport, Text effect)
        {
            RectTransform content = viewport.Find(effect.name + " Content") as RectTransform;
            if (content == null || viewport.GetComponent<RectMask2D>() == null) return false;
            Text[] paragraphs = content.GetComponentsInChildren<Text>(true).OrderByDescending(item => item.rectTransform.anchoredPosition.y).ToArray();
            if (paragraphs.Length == 0 || paragraphs[0] != effect) return false;
            // Positive top bearings of the embedded CJK font need real space above
            // the first paragraph, inside the shared clipping viewport.
            float bottom = 12f;
            foreach (Text paragraph in paragraphs)
            {
                RectTransform rect = paragraph.rectTransform;
                if (paragraph.resizeTextForBestFit || paragraph.horizontalOverflow != HorizontalWrapMode.Wrap || !TextFits(paragraph) ||
                    rect.anchorMin != new Vector2(.5f, 1f) || rect.anchorMax != new Vector2(.5f, 1f) || rect.pivot != new Vector2(.5f, 1f) ||
                    Mathf.Abs(rect.rect.width - (viewport.rect.width - 16f)) > .5f ||
                    Mathf.Abs(rect.anchoredPosition.x + 4f) > .5f || Mathf.Abs(rect.anchoredPosition.y + bottom) > .5f) return false;
                bottom += rect.rect.height + 12f;
            }
            bottom -= 12f;
            if (content.rect.height + .5f < Mathf.Max(viewport.rect.height, bottom)) return false;
            if (bottom > viewport.rect.height + .5f)
            {
                ScrollRect scroll = viewport.GetComponent<ScrollRect>();
                if (scroll == null || !scroll.vertical || scroll.content != content || scroll.viewport != viewport ||
                    scroll.verticalScrollbar == null || scroll.verticalScrollbar.handleRect == null) return false;
            }
            return true;
        }

        private static void AssertParagraphMeshFits(Text paragraph, string scenePath)
        {
            // Inactive dossier tabs have no CanvasRenderer mesh. Generate the same text mesh
            // directly at both scales instead, including its final glyph row and rich text.
            TextGenerationSettings settings = paragraph.GetGenerationSettings(paragraph.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = paragraph.fontSize;
            float originalScale = Mathf.Max(.01f, settings.scaleFactor);
            foreach (float scale in new[] { originalScale, 1f })
            {
                settings.scaleFactor = scale;
                settings.verticalOverflow = paragraph.verticalOverflow;
                settings.generateOutOfBounds = false;
                using (var actual = new TextGenerator())
                using (var complete = new TextGenerator())
                {
                    actual.Populate(paragraph.text, settings);
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    settings.generateOutOfBounds = true;
                    complete.Populate(paragraph.text, settings);
                    if ((!string.IsNullOrWhiteSpace(paragraph.text) && complete.verts.Count <= 4) ||
                        actual.characterCountVisible != complete.characterCountVisible || actual.vertexCount != complete.vertexCount)
                        throw new InvalidOperationException("Card text mesh omits characters: " + scenePath + " / " + paragraph.name);
                    Rect bounds = paragraph.rectTransform.rect;
                    // This project's uGUI Text.OnPopulateMesh processes every returned vertex.
                    // Convert exactly as it does: generated pixels / pixelsPerUnit.
                    float glyphMin = float.PositiveInfinity, glyphMax = float.NegativeInfinity;
                    for (int index = 0; index < complete.verts.Count; index++)
                    {
                        float y = complete.verts[index].position.y / scale;
                        glyphMin = Mathf.Min(glyphMin, y);
                        glyphMax = Mathf.Max(glyphMax, y);
                    }
                    RectTransform rect = paragraph.rectTransform;
                    RectTransform content = rect.parent as RectTransform;
                    Text previous = content.GetComponentsInChildren<Text>(true)
                        .Where(item => item != paragraph && item.rectTransform.anchoredPosition.y > rect.anchoredPosition.y)
                        .OrderBy(item => item.rectTransform.anchoredPosition.y).FirstOrDefault();
                    // A glyph's top bearing can legitimately be above its own line box.
                    // Check the actual safe region: the content's padded top, or the
                    // preceding paragraph's bottom. Keep the strict own-bottom check.
                    float topLimit = previous == null ? content.rect.yMax :
                        previous.rectTransform.anchoredPosition.y + previous.rectTransform.rect.yMin;
                    float glyphContentMin = rect.anchoredPosition.y + glyphMin;
                    float glyphContentMax = rect.anchoredPosition.y + glyphMax;
                    if (glyphMin < bounds.yMin - 2f || glyphContentMax > topLimit + 2f || glyphContentMin < content.rect.yMin - 2f)
                        throw new InvalidOperationException("Card text glyph extends past its paragraph: " + scenePath + " / " + paragraph.name +
                            "; scale=" + scale + ", originalScale=" + originalScale + ", font=" + paragraph.fontSize +
                            ", rectY=[" + bounds.yMin + "," + bounds.yMax + "], glyphY=[" + glyphMin + "," + glyphMax +
                            "], topProtrusion=" + (glyphMax - bounds.yMax) + ", bottomProtrusion=" + (bounds.yMin - glyphMin) +
                            ", contentGlyphY=[" + glyphContentMin + "," + glyphContentMax + "], contentY=[" + content.rect.yMin + "," + content.rect.yMax +
                            "], safeTop=" + topLimit + ", previousParagraph=" + (previous == null ? "none" : previous.name) +
                            ", preferredHeight=" + paragraph.preferredHeight + ", safeHeight=" +
                            Invoke(typeof(CoreGameplayDemoController), "MeasureCardTextHeight", null, paragraph) +
                            ", pivot=" + paragraph.rectTransform.pivot + ", text=" + paragraph.text.Replace("\n", "\\n"));
                }
            }
        }

        private static string BattleFaceSnapshot(Text label)
        {
            var text = new StringBuilder(EditorJsonUtility.ToJson(label));
            text.Append(EditorJsonUtility.ToJson(label.rectTransform));
            foreach (RectTransform child in label.transform.parent.Cast<Transform>().OfType<RectTransform>())
                if (child.name.StartsWith("Card Description Parchment ", StringComparison.Ordinal) ||
                    child.name.StartsWith("Card Art ", StringComparison.Ordinal) || child.name.StartsWith("Card Repeat Cost Plate ", StringComparison.Ordinal))
                    text.Append(EditorJsonUtility.ToJson(child));
            return text.ToString();
        }

        private static string BubbleSnapshot(RectTransform bubble) => EditorJsonUtility.ToJson(bubble) +
            string.Join("", bubble.GetComponentsInChildren<RectTransform>(true).Select(item => EditorJsonUtility.ToJson(item))) +
            string.Join("", bubble.GetComponentsInChildren<Text>(true).Select(item => EditorJsonUtility.ToJson(item)));
        private static bool SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null || rect.sizeDelta == size && rect.anchoredPosition == position) return false;
            rect.sizeDelta = size; rect.anchoredPosition = position; return true;
        }
        private static void AssertSameWords(string before, string after, string name)
        {
            Func<string, string> compact = text => text.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace('\u00a0', ' ');
            if (compact(before) != compact(after)) throw new InvalidOperationException("Card synchronization changed saved effect content: " + name);
        }
        private static IEnumerable<CoreGameplayDemoController> Hosts(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true));
        private static IEnumerable<string> CandidatePaths() => Directory.GetFiles(SceneRoot, "*.unity", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).Where(path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("m_Name: Keyword Bubble Text") || text.Contains("m_Name: Workshop Card Description ") ||
                    text.Contains("m_Name: Upgrade Comparison Body ") || text.Contains(" Special Card Effect") || text.Contains("m_Name: Special Card Detail Effect");
            });
        private static object Invoke(Type type, string method, object instance, params object[] args)
        {
            MethodInfo callback = type.GetMethod(method, instance == null ? PrivateStatic : PrivateInstance);
            if (callback == null) throw new MissingMethodException(type.FullName, method);
            try { return callback.Invoke(instance, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static void RequireStoppedEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Card effect scene synchronization requires stopped edit mode.");
        }
        private static string ReportPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-cardEffectSyncReport");
            return Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : "Artifacts/CardEffectText/scene-sync.json");
        }
        private static void SaveReport(Report report)
        {
            string path = ReportPath(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }
        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
        }
    }
}
