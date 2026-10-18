using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Content;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Renders the production dossier for all crew in unsaved preview scenes.</summary>
    public static class IntegratedCrewDossierAcceptance
    {
        [Serializable] private sealed class Review
        {
            public string generatedUtc;
            public int characters, pages, expectedPages;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Record
        {
            public string id, name, port, page, image, texture, frameTexture;
            public int textureWidth, textureHeight;
            public bool nameFixed, roleBesideName, textPreserved, noLegacyCutout, ownBackgroundPortrait;
            public bool portraitPreservesColors, portraitCoversWindow, portraitTopAligned, portraitAspectPreserved;
        }

        public static void CaptureFromCommandLine()
        {
            string output = Argument("-dossierReviewOutput", true);
            string onlyCrew = Argument("-dossierReviewCrew", false);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new empty dossier review output directory.");
            Directory.CreateDirectory(output);
            string[] pages = { "Background", "Story", "Skills", "SpecialCards" };
            Type previewType = typeof(CrewDossierAcceptance).GetNestedType("Preview", BindingFlags.NonPublic);
            Review report = new Review { generatedUtc = DateTime.UtcNow.ToString("O") };
            foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
            {
                object preview = Activator.CreateInstance(previewType, new object[] { port });
                try
                {
                    CoreGameplayDemoController controller = (CoreGameplayDemoController)previewType.GetProperty("Controller").GetValue(preview);
                    foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.ForPort(controller.Session.CurrentPort))
                    {
                        if (!string.IsNullOrEmpty(onlyCrew) && crew.Id != onlyCrew) continue;
                        report.characters++;
                        report.expectedPages += 4;
                        previewType.GetMethod("SelectCrew").Invoke(preview, new object[] { crew.ProfessionIndex });
                        Transform root = (Transform)previewType.GetProperty("Root").GetValue(preview);
                        foreach (string page in pages)
                        {
                            Record record = new Record { id = crew.Id, name = crew.DisplayName, port = crew.PortName, page = page,
                                image = crew.Id + "_" + page + ".png" };
                            report.captures.Add(record);
                            try
                            {
                                previewType.GetMethod("SelectPage").Invoke(preview, new object[] { page });
                                previewType.GetMethod("RenderStableFrame").Invoke(preview, null);
                                Inspect(root, crew, record, report);
                                RenderTexture target = (RenderTexture)previewType.GetProperty("Target").GetValue(preview);
                                typeof(CrewDossierAcceptance).GetMethod("SaveFrame", BindingFlags.Static | BindingFlags.NonPublic)
                                    .Invoke(null, new object[] { target, Path.Combine(output, record.image) });
                                report.pages++;
                            }
                            catch (Exception error) { report.errors.Add(crew.Id + "/" + page + ": " + (error.InnerException ?? error).Message); }
                        }
                    }
                }
                finally { ((IDisposable)preview).Dispose(); }
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            WriteGallery(output, report);
            if (report.characters != (string.IsNullOrEmpty(onlyCrew) ? 36 : 1) || report.pages != report.expectedPages || report.errors.Count > 0)
                throw new InvalidOperationException("Dossier acceptance failed. " + report.pages + "/" + report.expectedPages + " pages; errors " + report.errors.Count + ". See " + output);
            Debug.Log("INTEGRATED_DOSSIER_ACCEPTANCE_PASS: " + report.characters + " characters / " + report.pages + " pages / " + output);
        }

        private static void Inspect(Transform root, CrewVariantDefinition crew, Record record, Review report)
        {
            Transform detail = Find<RectTransform>(root, "Tavern Crew Detail");
            RawImage paper = Find<RawImage>(detail, "Tavern Dossier Parchment");
            RawImage portrait = Find<RawImage>(detail, "Tavern Dossier Background Portrait");
            RectTransform viewport = Find<RectTransform>(detail, "Tavern Dossier Portrait Viewport");
            record.texture = AssetDatabase.GetAssetPath(portrait.texture);
            record.frameTexture = AssetDatabase.GetAssetPath(paper.texture);
            record.textureWidth = portrait.texture == null ? 0 : portrait.texture.width;
            record.textureHeight = portrait.texture == null ? 0 : portrait.texture.height;
            string expected = "Assets/_Project/Resources/PortArt/CrewDossiers/" + crew.Id + "_portrait_v2.png";
            record.ownBackgroundPortrait = record.texture == expected;
            Check(record.ownBackgroundPortrait, crew.Id + ": wrong or missing background portrait " + record.texture, report);
            Check(record.textureWidth >= 512 && record.textureHeight >= 640, crew.Id + ": insufficient portrait resolution", report);
            Check(record.frameTexture == "Assets/_Project/Resources/PortArt/UI/tavern_crew_dossier_frame_4k_layers_v1.jpg",
                crew.Id + ": wrong dossier frame", report);
            Rect portraitBounds = Bounds(viewport, portrait.rectTransform);
            record.portraitCoversWindow = portraitBounds.xMin <= viewport.rect.xMin + .1f && portraitBounds.xMax >= viewport.rect.xMax - .1f &&
                portraitBounds.yMin <= viewport.rect.yMin + .1f && portraitBounds.yMax >= viewport.rect.yMax - .1f;
            record.portraitTopAligned = Mathf.Abs(portraitBounds.yMax - viewport.rect.yMax) < .1f;
            record.portraitAspectPreserved = Mathf.Abs(portrait.rectTransform.rect.width / portrait.rectTransform.rect.height -
                (float)record.textureWidth / record.textureHeight) < .001f;
            record.portraitPreservesColors = portrait.GetComponent<CrewArtChromaKey>() == null &&
                portrait.GetComponent<TavernPortraitPaperBlend>() == null;
            Check(record.portraitCoversWindow && record.portraitTopAligned && record.portraitAspectPreserved,
                crew.Id + ": portrait crop stretches the face, exposes an empty edge, or loses top alignment", report);
            Check(record.portraitPreservesColors, crew.Id + ": portrait colors are keyed or blended away", report);
            record.noLegacyCutout = !detail.GetComponentsInChildren<RawImage>(false).Any(image => image.name == "Tavern Crew Detail Full Body") &&
                !detail.GetComponentsInChildren<TavernPortraitPaperBlend>(false).Any();
            Check(record.noLegacyCutout, crew.Id + ": legacy portrait cutout/blend remains", report);
            Text name = Find<Text>(detail, "Tavern Crew Detail Name");
            Text role = Find<Text>(detail, "Tavern Crew Detail Role");
            Rect nameBounds = Bounds(detail, name.rectTransform);
            Rect roleBounds = Bounds(detail, role.rectTransform);
            record.nameFixed = Mathf.Abs(nameBounds.xMin - (-165f)) < 0.5f && Mathf.Abs(nameBounds.center.y - 210f) < 0.5f;
            record.roleBesideName = Mathf.Abs(roleBounds.center.y - nameBounds.center.y) < 1f && roleBounds.xMin > 200f;
            Check(record.nameFixed, crew.Id + ": name position changed", report);
            Check(record.roleBesideName, crew.Id + ": role is not beside the name", report);
            record.textPreserved = name.text == crew.DisplayName &&
                SameText(Find<Text>(detail, "Tavern Crew Biography").text, crew.Biography) &&
                SameText(Find<Text>(detail, "Tavern Crew Story").text, CrewNarrativeCatalog.StoryFor(crew).Body) &&
                SameText(Find<Text>(detail, "Tavern Crew Skill").text, crew.SkillDescription);
            Check(record.textPreserved, crew.Id + ": narrative wording changed", report);
            string[] proseNames = { "Tavern Crew Biography", "Tavern Crew Story", "Tavern Crew Skill", "Tavern Crew Story Heading", "Tavern Crew Skill Heading", "Tavern Crew Detail Role" };
            foreach (Text text in detail.GetComponentsInChildren<Text>(false).Where(text => proseNames.Contains(text.name)))
                Check(text.verticalOverflow != VerticalWrapMode.Truncate || text.preferredHeight <= text.rectTransform.rect.height + 0.5f,
                    crew.Id + "/" + record.page + ": clipped text " + text.name, report);
            Button recruit = Find<Button>(detail, "Recruit Tavern Crew " + crew.ProfessionIndex);
            Rect recruitBounds = Bounds(detail, recruit.GetComponent<RectTransform>());
            Check(recruitBounds.xMax <= 600f && recruitBounds.yMin >= -265f, crew.Id + ": contract button outside inner paper lane", report);
        }

        private static bool SameText(string left, string right) => Regex.Replace(left ?? "", @"\s+", "") == Regex.Replace(right ?? "", @"\s+", "");
        private static void Check(bool pass, string message, Review report) { if (!pass && !report.errors.Contains(message)) report.errors.Add(message); }
        private static Rect Bounds(Transform parent, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
            foreach (Vector3 corner in corners) { Vector2 point = parent.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static T Find<T>(Transform root, string name) where T : Component => root.GetComponentsInChildren<T>(true).Single(item => item.name == name);
        private static string Argument(string key, bool required)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            if (index >= 0 && index + 1 < args.Length) return args[index + 1];
            if (required) throw new ArgumentException("Missing " + key);
            return null;
        }
        private static void WriteGallery(string output, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang='zh-CN'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>角色详情页验收</title><style>body{margin:24px;background:#171411;color:#ecdfc3;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:18px}figure{margin:0}img{width:100%;height:auto}a{color:#e6bc72}figcaption{padding:8px 0}</style><h1>36 位角色 · 四页详情</h1><p>");
            html.Append(report.characters).Append(" 位角色 / ").Append(report.pages).Append(" 页。姓名与简介同行；页签和正文上移，底部操作避开卷边。</p><main>");
            foreach (Record item in report.captures)
                html.Append("<figure><a href='").Append(item.image).Append("'><img loading='lazy' src='").Append(item.image).Append("'></a><figcaption>")
                    .Append(item.port).Append(" · ").Append(item.name).Append(" · ").Append(item.page).Append("</figcaption></figure>");
            html.Append("</main><p><a href='report.json'>验收数据</a></p></html>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
