using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    // Review-only capture: adjust the layout in an unsaved preview scene.
    // The runtime layout and generated background asset remain independently editable.
    public static class LorneDossierLayoutCapture
    {
        public static void CaptureFromCommandLine()
        {
            string output = Argument("-lorneReviewOutput");
            string background = Argument("-lorneReviewBackground");
            Directory.CreateDirectory(output);
            Type fixtureType = typeof(CrewDossierAcceptance).GetNestedType("Preview", BindingFlags.NonPublic);
            object fixture = Activator.CreateInstance(fixtureType, new object[] { CoreGameplayPreviewPort.BrokenAnchor });
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(background))) throw new IOException("Cannot read review background.");
                texture.name = "Lorne Integrated Parchment Review";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                fixtureType.GetMethod("SelectCrew").Invoke(fixture, new object[] { 0 });
                Transform root = (Transform)fixtureType.GetProperty("Root").GetValue(fixture);
                Find<RawImage>(root, "Tavern Dossier Parchment").texture = texture;
                Find<RawImage>(root, "Tavern Crew Detail Full Body").gameObject.SetActive(false);
                ApplyReviewedLayout(root);
                string[] keys = { "Background", "Story", "Skills", "SpecialCards" };
                string[] labels = { "人物背景", "航海异闻", "职业技能", "专属卡牌" };
                StringBuilder report = new StringBuilder("Review v2. Name position unchanged. Other profile content shifted 88 UI pixels left. Footer shifted 88 left and 18 up. Paragraph wording retained; widths, paragraph breaks and line spacing revised. Temporary preview only.\n");
                for (int page = 0; page < keys.Length; page++)
                {
                    fixtureType.GetMethod("SelectPage").Invoke(fixture, new object[] { keys[page] });
                    fixtureType.GetMethod("RenderStableFrame").Invoke(fixture, null);
                    RenderTexture target = (RenderTexture)fixtureType.GetProperty("Target").GetValue(fixture);
                    string file = (page + 1).ToString("00") + "_" + keys[page] + ".png";
                    typeof(CrewDossierAcceptance).GetMethod("SaveFrame", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { target, Path.Combine(output, file) });
                    foreach (Text text in Find<RectTransform>(root, "Tavern Crew Detail").GetComponentsInChildren<Text>(false))
                    {
                        if (string.IsNullOrEmpty(text.text)) continue;
                        if (text.preferredHeight > text.rectTransform.rect.height + 0.5f)
                            report.AppendLine(labels[page] + " / height overflow observation: " + text.name);
                    }
                    report.AppendLine(labels[page] + ": " + file);
                }
                File.WriteAllText(Path.Combine(output, "capture-notes.txt"), report.ToString(), new UTF8Encoding(false));
                Debug.Log("LORNE_LAYOUT_REVIEW_COMPLETE: " + output);
            }
            finally
            {
                ((IDisposable)fixture).Dispose();
                Object.DestroyImmediate(texture);
            }
        }

        private static void ApplyReviewedLayout(Transform root)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform name = Find<Text>(root, "Tavern Crew Detail Name").rectTransform;
            Vector3 originalNamePosition = name.position;
            Find<RectTransform>(root, "Tavern Crew Detail Profile").anchoredPosition += new Vector2(-88f, 0f);
            name.anchoredPosition += new Vector2(88f, 0f);
            foreach (string footer in new[] { "Previous Tavern Dossier Page", "Tavern Dossier Page Counter", "Next Tavern Dossier Page", "Recruit Tavern Crew 0" })
                Find<RectTransform>(root, footer).anchoredPosition += new Vector2(-88f, 18f);

            foreach (string page in new[] { "Background", "Story", "Skills" })
            {
                RectTransform bodyPage = Find<RectTransform>(root, "Tavern Dossier " + page + " Page");
                bodyPage.sizeDelta = new Vector2(930f, 286f);
                bodyPage.anchoredPosition = new Vector2(0f, -62f);
            }

            Text biography = Find<Text>(root, "Tavern Crew Biography");
            Reflow(biography, Paragraphs(biography.text), 20f, 8f, 820f, 264f, 27, 1.25f);
            Reflow(Find<Text>(root, "Tavern Crew Story Heading"), null, 0f, 8f, 860f, 38f, 25, 1.1f);
            Text story = Find<Text>(root, "Tavern Crew Story");
            Reflow(story, Paragraphs(story.text), 20f, 56f, 850f, 230f, 26, 1.22f);
            Reflow(Find<Text>(root, "Tavern Crew Skill Heading"), null, 0f, 8f, 850f, 40f, 28, 1.1f);
            Text skill = Find<Text>(root, "Tavern Crew Skill");
            string skillLines = skill.text.Replace("\r", "").Replace("\n", "").Replace("；", "；\n\n").Replace("，", "，\n");
            Reflow(skill, skillLines, 20f, 64f, 850f, 192f, 26, 1.3f);

            // Preserve the card artwork while opening a gap above the raised footer.
            Find<RectTransform>(root, "Tavern Special Card Face").anchoredPosition += new Vector2(0f, 12f);
            Text stage = Find<Text>(root, "Tavern Special Card Stage");
            NarrowAtLeft(stage.rectTransform, 580f);
            RectTransform viewport = Find<RectTransform>(root, "Tavern Special Card Details Viewport");
            NarrowAtLeft(viewport, 580f);
            RectTransform content = Find<RectTransform>(root, "Tavern Special Card Details Content");
            content.sizeDelta = new Vector2(580f, content.sizeDelta.y);
            Text lore = Find<Text>(root, "Tavern Special Card Lore");
            Reflow(lore, Paragraphs(lore.text), 8f, 12f, 564f, 190f, 24, 1.22f);
            lore.color = new Color32(53, 35, 21, 255);
            Text growth = Find<Text>(root, "Tavern Special Card Growth");
            growth.rectTransform.sizeDelta = new Vector2(580f, 60f);
            growth.rectTransform.anchoredPosition = new Vector2(75f, -102f);
            growth.fontSize = 22;
            growth.lineSpacing = 1.15f;
            growth.color = lore.color;
            Canvas.ForceUpdateCanvases();
            if (Vector3.Distance(originalNamePosition, name.position) > 0.01f)
                throw new InvalidOperationException("Character name moved during the layout review.");
        }

        private static string Paragraphs(string text)
        {
            return text.Replace("\r", "").Replace("\n", "").Replace("。", "。\n\n").TrimEnd();
        }

        private static void NarrowAtLeft(RectTransform rect, float width)
        {
            rect.anchoredPosition += new Vector2((width - rect.sizeDelta.x) * 0.5f, 0f);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static void Reflow(Text text, string value, float left, float top, float width, float height, int fontSize, float spacing)
        {
            if (value != null)
            {
                if (Regex.Replace(text.text, @"\s+", "") != Regex.Replace(value, @"\s+", ""))
                    throw new InvalidOperationException("Reflow changed the wording: " + text.name);
                text.text = value;
            }
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
            text.fontSize = fontSize;
            text.lineSpacing = spacing;
            text.alignment = TextAnchor.UpperLeft;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            return root.GetComponentsInChildren<T>(true).Single(value => value.name == name);
        }

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + key);
            return Path.GetFullPath(args[index + 1]);
        }
    }
}
