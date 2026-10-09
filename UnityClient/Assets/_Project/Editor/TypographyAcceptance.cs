using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Disposable edit-mode typography review. No user scenes, saves, settings,
    /// purchases, or recruitment are modified. Font checks are diagnostics;
    /// screenshots must still be reviewed for legibility and visual character.
    /// </summary>
    public static class TypographyAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int Layer = 31;
        private static readonly Regex RichText = new Regex("</?(?:color|size|b|i|material)(?:=[^>]*)?>", RegexOptions.IgnoreCase);

        private sealed class CaptureCase
        {
            public string label;
            public bool mainMenu;
            public bool specimen;
            public bool settings;
            public CoreGameplayPreviewScreen screen;
            public CoreGameplayPreviewPort port;
            public int route;
        }

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string safety = "Disposable editor preview only. Never enters Play Mode, applies settings, changes PlayerPrefs, or saves user scenes.";
            public int expectedCaptureCount;
            public int successfulCaptureCount;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public string page;
            public string file;
            public bool complete;
            public int visibleTextCount;
            public List<string> fonts = new List<string>();
            public List<string> missingGlyphs = new List<string>();
            public List<string> layoutWarnings = new List<string>();
            public string error;
        }

        [MenuItem("Shattered Sail/Art Review/Capture Game Typography")]
        public static void CaptureFromMenu()
        {
            string directory = Capture(ResolveOutputDirectory());
            Debug.Log("Typography gallery: " + directory);
            EditorUtility.RevealInFinder(Path.Combine(directory, "index.html"));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.TypographyAcceptance.CaptureFromCommandLine
        /// -typographyCapturePath D:/Game/UnityClient/Artifacts/Typography/review-01
        /// Requires graphics: do not pass -nographics.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("TYPOGRAPHY_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before capturing typography.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Typography capture requires graphics. Remove -nographics.");
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty; use a fresh -typographyCapturePath: " + directory);
            Directory.CreateDirectory(directory);
            List<CaptureCase> cases = Cases().ToList();
            Review review = new Review { capturedAtUtc = DateTime.UtcNow.ToString("O"), expectedCaptureCount = cases.Count };
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousActive = RenderTexture.active;
            EventSystem previousEvents = EventSystem.current;
            GameObject previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            GameObject cameraHost = null;
            GameObject host = null;
            RenderTexture target = null;
            try
            {
                cameraHost = new GameObject("Typography Review Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer;
                camera.scene = previewScene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Typography Review Target" };
                target.Create();
                camera.targetTexture = target;

                for (int index = 0; index < cases.Count; index += 1)
                {
                    CaptureCase item = cases[index];
                    Record record = new Record { page = item.label, file = (index + 1).ToString("D2") + "_" + (item.specimen ? "TypeSpecimen" : item.mainMenu ? "MainMenu" : item.screen.ToString()) + (item.settings ? "_Settings" : "") + ".png" };
                    review.captures.Add(record);
                    try
                    {
                        host = new GameObject("Typography Review " + item.label) { hideFlags = HideFlags.HideAndDontSave };
                        host.SetActive(false);
                        SceneManager.MoveGameObjectToScene(host, previewScene);
                        if (item.specimen)
                        {
                            BuildSpecimen(host.transform);
                            host.SetActive(true);
                        }
                        else if (item.mainMenu)
                        {
                            host.AddComponent<MainMenuController>();
                            host.SetActive(true);
                        }
                        else
                        {
                            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                            controller.ConfigureEditorPreview(item.screen, item.port, item.route);
                            host.SetActive(true);
                            if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Required preview did not build.");
                        }

                        Canvas canvas = host.GetComponentInChildren<Canvas>();
                        if (canvas == null) throw new InvalidOperationException("Preview Canvas is missing.");
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.worldCamera = camera;
                        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                        canvasRect.sizeDelta = new Vector2(Width, Height);
                        canvasRect.position = Vector3.zero;
                        canvasRect.localScale = Vector3.one;
                        if (item.settings)
                        {
                            Button settings = host.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Port Facility Settings");
                            if (settings == null) throw new InvalidOperationException("Port settings button is missing.");
                            settings.onClick.Invoke();
                        }
                        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                        Canvas.ForceUpdateCanvases();
                        InspectTypography(canvas, record);
                        // The glyph requests in the inspector can rebuild a shared font atlas.
                        Canvas.ForceUpdateCanvases();
                        if (GraphicsSettings.currentRenderPipeline != null)
                            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        else camera.Render();
                        SaveFrame(target, Path.Combine(directory, record.file));
                        record.complete = true;
                        review.successfulCaptureCount += 1;
                    }
                    catch (Exception exception)
                    {
                        record.error = exception.Message;
                        review.errors.Add(item.label + ": " + exception.Message);
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        if (host != null)
                        {
                            Button close = host.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Close Port Settings");
                            if (close != null) close.onClick.Invoke();
                            UnityEngine.Object.DestroyImmediate(host);
                            host = null;
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(previewScene);
                if (previousEvents != null)
                    previousEvents.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(review, true), new UTF8Encoding(false));
                WriteGallery(directory, review);
            }
            if (review.errors.Count > 0 || review.successfulCaptureCount != review.expectedCaptureCount)
                throw new InvalidOperationException("Typography review incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static IEnumerable<CaptureCase> Cases()
        {
            yield return new CaptureCase { label = "主菜单 · 标题与按钮", mainMenu = true };
            yield return Page("港口总览 · 建筑标签", CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull);
            yield return Page("酒馆 · 人物姓名", CoreGameplayPreviewScreen.Tavern, CoreGameplayPreviewPort.GrayGull);
            yield return Page("船员档案 · 长篇故事", CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.TwilightTide);
            yield return Page("港口投资建设 · 检查与采购数值", CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.SaltCrown);
            yield return Page("部件商店 · 商品名称", CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.BronzeBell);
            yield return Page("部件档案 · 效果与背景", CoreGameplayPreviewScreen.ComponentShopDetail, CoreGameplayPreviewPort.BronzeBell);
            yield return Page("卡组工坊 · 箱子铭牌", CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.GrayGull);
            yield return Page("卡组构筑 · 卡牌说明", CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull);
            yield return Page("远航部署 · 船员与时间", CoreGameplayPreviewScreen.DispatchDeployment, CoreGameplayPreviewPort.WhiteShell);
            yield return Page("出航码头 · 输入框与航线", CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.GrayGull);
            yield return Page("航海探索 · 地图标记", CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull);
            yield return Page("事件 · 说明与选择", CoreGameplayPreviewScreen.Encounter, CoreGameplayPreviewPort.TwilightTide);
            yield return Page("海战 · 卡牌与战斗数值", CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull);
            yield return Page("结算 · 奖励与统计", CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull);
            CaptureCase settings = Page("港口设置 · 羊皮纸弹窗", CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.GrayGull);
            settings.settings = true;
            yield return settings;
            yield return new CaptureCase { label = "羊皮纸字样 · 立即使用 / 小字号 / 数字符号", specimen = true };
        }

        private static void BuildSpecimen(Transform parent)
        {
            GameObject canvasHost = new GameObject("Typography Specimen Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasHost.transform.SetParent(parent, false);
            Canvas canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasHost.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            RectTransform canvasRect = canvasHost.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(Width, Height);
            Texture2D parchment = Resources.Load<Texture2D>("CardWorkshopV2/deck_builder_parchment_v2");
            if (parchment == null) throw new InvalidOperationException("The existing collection parchment is missing.");
            RectTransform paper = SpecimenRect("Existing Parchment", canvasRect, new Vector2(1840f, 1000f), Vector2.zero);
            RawImage background = paper.gameObject.AddComponent<RawImage>();
            background.texture = parchment;
            background.uvRect = new Rect(0.06f, 0.12f, 0.88f, 0.76f);
            background.raycastTarget = false;
            Color ink = new Color(0.16f, 0.085f, 0.045f, 1f);
            SpecimenText("Specimen Heading", paper, new Vector2(1500f, 70f), new Vector2(0f, 385f), 36,
                "香萃刻宋 · 海盗航海手札", ink);
            SpecimenText("Reference Phrase", paper, new Vector2(1580f, 295f), new Vector2(0f, 180f), 200,
                "立即使用", ink);
            SpecimenText("Specimen Buttons", paper, new Vector2(1500f, 95f), new Vector2(0f, -55f), 46,
                "返回港口　　开始航行　　招募船员", ink);
            SpecimenText("Specimen Ports", paper, new Vector2(1500f, 58f), new Vector2(0f, -138f), 28,
                "断锚港 · 白贝港 · 铜钟港 · 盐冠港 · 暮潮港 · 灰鸥港", ink);
            SpecimenText("Specimen Body 20", paper, new Vector2(1500f, 68f), new Vector2(0f, -226f), 20,
                "正文 20 号　暮潮港的巫师在礁湖迎战海妖，将咒文装入空心炮弹，击碎海面上的回音。\n"
                + "船长循着旧海图航向第七码头。潮声之间，远方灯塔终于传来了船员约定的信号。", ink);
            SpecimenText("Specimen Body 16", paper, new Vector2(1500f, 60f), new Vector2(0f, -308f), 16,
                "正文 16 号　船体 70/70 · 装甲 4 · 火力 8 · 航速 15 节 · 货舱 68.2/90 · 本机时间 2026-09-07 05:20\n"
                + "已完成检查 ✓　总金币 1,234,567　← 返回　下一页 →　‹ › ◆ ◇ × + − ％　SHATTERED SAIL", ink);
            SpecimenText("Specimen Caption", paper, new Vector2(1500f, 36f), new Vector2(0f, -404f), 16,
                "同一款内嵌字体 · 原生字重 · 真实 Unity 文本渲染（不是烘焙文字图片）", ink);
        }

        private static RectTransform SpecimenRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void SpecimenText(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, string value, Color ink)
        {
            Text text = SpecimenRect(name, parent, size, position).gameObject.AddComponent<Text>();
            GameTypography.Apply(text, fontSize, FontStyle.Normal);
            text.text = value;
            text.color = ink;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.supportRichText = false;
        }

        private static CaptureCase Page(string label, CoreGameplayPreviewScreen screen, CoreGameplayPreviewPort port)
        {
            return new CaptureCase { label = label, screen = screen, port = port, route = 0 };
        }

        private static void InspectTypography(Canvas canvas, Record record)
        {
            HashSet<string> fonts = new HashSet<string>();
            HashSet<string> missing = new HashSet<string>();
            foreach (Text label in canvas.GetComponentsInChildren<Text>())
            {
                if (!label.enabled || !label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text)) continue;
                record.visibleTextCount += 1;
                if (label.font == null)
                {
                    missing.Add(label.name + ": missing Font reference");
                    continue;
                }
                string fontPath = AssetDatabase.GetAssetPath(label.font);
                fonts.Add(label.font.name + " | " + (string.IsNullOrEmpty(fontPath) ? "runtime/built-in font" : fontPath));
                string plain = label.supportRichText ? RichText.Replace(label.text, string.Empty) : label.text;
                if (label.font.dynamic) label.font.RequestCharactersInTexture(plain, label.fontSize, label.fontStyle);
                foreach (char glyph in plain.Distinct())
                {
                    if (!char.IsWhiteSpace(glyph) && !char.IsControl(glyph) && !label.font.HasCharacter(glyph))
                        missing.Add(label.font.name + " | U+" + ((int)glyph).ToString("X4") + " " + glyph);
                }
                if (!label.resizeTextForBestFit && label.verticalOverflow == VerticalWrapMode.Truncate &&
                    label.preferredHeight > label.rectTransform.rect.height + 2f)
                    record.layoutWarnings.Add(label.name + " | preferred height " + label.preferredHeight.ToString("0.0") + " > rect height " + label.rectTransform.rect.height.ToString("0.0"));
            }
            record.fonts = fonts.OrderBy(value => value).ToList();
            record.missingGlyphs = missing.OrderBy(value => value).ToList();
            if (record.visibleTextCount == 0) throw new InvalidOperationException("No visible UI text was found.");
        }

        private static void SaveFrame(RenderTexture target, string file)
        {
            RenderTexture.active = target;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                Color32[] pixels = image.GetPixels32();
                int dark = 255, bright = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int level = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    dark = Math.Min(dark, level); bright = Math.Max(bright, level);
                }
                if (bright - dark < 20) throw new InvalidOperationException("Frame is nearly uniform; preview did not render correctly.");
                File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review review)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>全游戏字体验收</title><style>body{margin:28px;background:#201b17;color:#efe0bd;font:16px system-ui}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(620px,1fr));gap:24px}figure{margin:0;padding:14px;background:#302921;border:1px solid #826d49}img{width:100%;display:block;margin:12px 0}small{display:block;color:#c5bca8;line-height:1.6}a{color:#e8ba66}summary{cursor:pointer}li{line-height:1.7}p{max-width:1100px;line-height:1.7}</style>");
            html.Append("<h1>全游戏字体 · 视觉验收</h1><p>").Append(review.successfulCaptureCount).Append(" / ").Append(review.expectedCaptureCount)
                .Append(" 页 · 1920 × 1080。截图来自独立编辑预览，未更改用户场景、存档或设置。点击图片打开原图检查字形、故事阅读性、数值与箭头。</p><p><a href=\"report.json\">字体资产、缺字和布局诊断报告</a>。自动报告仅作提示，不代替人工检查；竖向溢出提示可能包含已有固定高度布局。</p><section>");
            foreach (Record record in review.captures)
            {
                html.Append("<figure><figcaption>").Append(Escape(record.page)).Append("<small>").Append(record.visibleTextCount).Append(" 个可见文字节点</small></figcaption>");
                if (record.complete) html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><img src=\"").Append(Escape(record.file)).Append("\" loading=\"lazy\"></a>");
                else html.Append("<p>捕获失败：").Append(Escape(record.error)).Append("</p>");
                foreach (string font in record.fonts) html.Append("<small>").Append(Escape(font)).Append("</small>");
                if (record.missingGlyphs.Count > 0 || record.layoutWarnings.Count > 0)
                {
                    html.Append("<details><summary>诊断提示 · 缺字 ").Append(record.missingGlyphs.Count).Append(" · 潜在竖向裁切 ").Append(record.layoutWarnings.Count).Append("</summary><ul>");
                    foreach (string warning in record.missingGlyphs.Concat(record.layoutWarnings)) html.Append("<li>").Append(Escape(warning)).Append("</li>");
                    html.Append("</ul></details>");
                }
                html.Append("</figure>");
            }
            html.Append("</section></html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index += 1)
                if (arguments[index] == "-typographyCapturePath") return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            return Path.Combine(project, "Artifacts", "Typography", "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
    }
}
