using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Read-only, unsaved component dossier rendering and full-catalog layout checks.</summary>
    public static class ComponentDossierAcceptance
    {
        private const int Width = 1920, Height = 1080, CaptureLayer = 31;
        private static readonly string[] Pages = { "Overview", "Effects", "Story", "SpecialCards" };
        private static readonly string[] Titles = { "部件来历", "装配效果", "航海异闻", "专属卡牌" };
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [Serializable] private sealed class Review
        {
            public string generatedUtc, mode;
            public int width = Width, height = Height, components, expectedPages, pages, expectedCaptures, captures, passedPages;
            public bool sourceScenesUnchanged, checkedPlayerPrefsUnchanged;
            public string scope = "All ExpandedEntries, four pages each. Default screenshots: first entry of each rarity/slot pair, except Common/Cannon uses cannon_ropeguard_swivel to match the user's reference. Temporary non-Play scenes; only page-tab callbacks. No purchase, equipment or save callbacks.";
            public List<Record> records = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable] private sealed class Record
        {
            public string id, name, rarity, slot, page, image, modelTexture, paperTexture;
            public bool textPreserved, headerAligned, footerInsidePaper, footerSeparated, pageClearOfFooter, layoutPassed;
            public Rect nameBounds, metaBounds, pageBounds;
            public List<TextRecord> texts = new List<TextRecord>();
            public List<FooterRecord> footer = new List<FooterRecord>();
            public List<string> issues = new List<string>();
        }

        [Serializable] private sealed class TextRecord
        {
            public string name, scroll;
            public int characters, fontSize;
            public float preferredHeight, rectHeight;
            public bool fits, scrollContentCoversText;
        }

        [Serializable] private sealed class FooterRecord { public string name; public Rect bounds; }

        // -executeMethod ShatteredSail.Editor.ComponentDossierAcceptance.CaptureFromCommandLine
        // -componentDossierOutput D:/Game/Artifacts/ComponentDossier/After
        // [-componentDossierBefore] [-componentDossierSample] [-componentDossierCaptureAll]
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Component acceptance requires non-Play edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Component capture requires graphics; remove -nographics.");
            string output = Path.GetFullPath(Argument("-componentDossierOutput"));
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new empty -componentDossierOutput directory: " + output);
            bool before = Flag("-componentDossierBefore") || Flag("-before");
            bool captureAll = Flag("-componentDossierCaptureAll");
            Directory.CreateDirectory(output);
            Review review = new Review { generatedUtc = DateTime.UtcNow.ToString("O"), mode = before ? "Before (layout observations only)" : "After (layout assertions)" };
            Dictionary<string, string> scenes = Snapshot("SnapshotSourceScenes");
            Dictionary<string, string> preferences = Snapshot("SnapshotRelevantPlayerPrefs");
            Application.LogCallback errors = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    review.errors.Add("Unity: " + message);
            };
            Application.logMessageReceived += errors;
            try
            {
                ComponentArtEntry[] entries = new ComponentArtCatalog().ExpandedEntries.ToArray();
                HashSet<string> samples = new HashSet<string>(entries.GroupBy(entry => entry.Rarity + "/" + entry.Slot)
                    .Select(group => (group.First().Rarity == ComponentRarity.Common && group.First().Slot == ComponentSlot.Cannon
                        ? group.Single(entry => entry.Id == "cannon_ropeguard_swivel") : group.First()).Id));
                review.components = entries.Length;
                review.expectedPages = entries.Length * Pages.Length;
                review.expectedCaptures = (captureAll ? entries.Length : samples.Count) * Pages.Length;
                if (entries.Length != 150 || samples.Count != 25)
                    review.errors.Add("Expected 150 component entries and 25 rarity/slot sample groups; received " + entries.Length + "/" + samples.Count + ".");
                using (Preview preview = new Preview())
                {
                    foreach (ComponentArtEntry entry in entries)
                    {
                        preview.SelectEntry(entry);
                        for (int page = 0; page < Pages.Length; page++)
                        {
                            Record record = new Record { id = entry.Id, name = entry.Name, rarity = entry.Rarity.ToString(), slot = entry.Slot.ToString(), page = Pages[page] };
                            review.records.Add(record);
                            try
                            {
                                preview.SelectPage(page);
                                bool capture = captureAll || samples.Contains(entry.Id);
                                if (capture) preview.RenderStableFrame();
                                else Canvas.ForceUpdateCanvases();
                                Inspect(preview, entry, page, record);
                                record.layoutPassed = record.issues.Count == 0;
                                if (record.layoutPassed) review.passedPages++;
                                if (capture)
                                {
                                    record.image = entry.Id + "_" + Pages[page] + ".png";
                                    typeof(CrewDossierAcceptance).GetMethod("SaveFrame", PrivateStatic)
                                        .Invoke(null, new object[] { preview.Target, Path.Combine(output, record.image) });
                                    if (new FileInfo(Path.Combine(output, record.image)).Length < 20000)
                                        throw new InvalidOperationException("Screenshot is unexpectedly empty.");
                                    review.captures++;
                                }
                                review.pages++;
                            }
                            catch (Exception exception)
                            {
                                review.errors.Add(entry.Id + "/" + Pages[page] + ": " + (exception.InnerException ?? exception).Message);
                            }
                        }
                        Debug.Log("COMPONENT_DOSSIER_PROGRESS: " + review.pages + "/" + review.expectedPages + " pages, " + entry.Id);
                    }
                }
            }
            finally
            {
                Application.logMessageReceived -= errors;
                review.sourceScenesUnchanged = Equal(scenes, Snapshot("SnapshotSourceScenes"));
                review.checkedPlayerPrefsUnchanged = Equal(preferences, Snapshot("SnapshotRelevantPlayerPrefs"));
                if (!review.sourceScenesUnchanged) review.errors.Add("Source scene bytes changed during read-only capture.");
                if (!review.checkedPlayerPrefsUnchanged) review.errors.Add("A checked PlayerPrefs value changed during read-only capture.");
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(review, true), new UTF8Encoding(false));
                WriteGallery(output, review);
            }
            if (review.errors.Count != 0 || review.pages != review.expectedPages || review.captures != review.expectedCaptures ||
                (!before && review.passedPages != review.expectedPages))
                throw new InvalidOperationException("Component dossier acceptance failed: " + review.passedPages + "/" + review.expectedPages +
                    " layout pages, " + review.captures + "/" + review.expectedCaptures + " captures, " + review.errors.Count + " execution errors. See " + output);
            Debug.Log("COMPONENT_DOSSIER_ACCEPTANCE_PASS: " + review.pages + " pages, " + review.captures + " captures; " + review.mode + "; " + output);
        }

        private static void Inspect(Preview preview, ComponentArtEntry entry, int page, Record record)
        {
            RectTransform detail = Find<RectTransform>(preview.Root, "Component Shop Detail");
            Text name = Find<Text>(detail, "Component Detail Name");
            Text meta = Find<Text>(detail, "Component Detail Meta");
            RawImage model = Find<RawImage>(detail, "Component Detail Model");
            RawImage paper = Find<RawImage>(detail, "Component Detail Parchment");
            record.modelTexture = AssetDatabase.GetAssetPath(model.texture);
            record.paperTexture = AssetDatabase.GetAssetPath(paper.texture);
            Require(model.texture != null && model.texture.width > 0, "Missing component illustration", record);
            Require(paper.texture != null && paper.texture.width > 0, "Missing parchment", record);
            record.nameBounds = Bounds(detail, name.rectTransform);
            record.metaBounds = Bounds(detail, meta.rectTransform);
            record.headerAligned = Mathf.Abs(record.nameBounds.xMin + 165f) < 0.5f && Mathf.Abs(record.nameBounds.center.y - 210f) < 0.5f &&
                Mathf.Abs(record.metaBounds.center.y - record.nameBounds.center.y) < 0.5f && record.metaBounds.xMin >= record.nameBounds.xMax + 10f;
            Require(record.headerAligned, "Name/meta must share the header row with original name position", record);
            record.textPreserved = Same(name.text, entry.Name) && Same(meta.text, ComponentArtCatalog.RarityName(entry.Rarity) + " · " + ComponentArtCatalog.SlotName(entry.Slot)) &&
                Same(Find<Text>(detail, "Component Detail Description").text, entry.Description) &&
                Contains(Find<Text>(detail, "Component Detail Specialization").text, entry.Specialization) &&
                Same(Find<Text>(detail, "Component Detail Story Chapter").text, entry.StoryTitle) &&
                Same(Find<Text>(detail, "Component Detail Story Body").text, entry.StoryBody) &&
                Contains(AllText(Find<RectTransform>(detail, "Component Dossier Effects Page")), entry.Effect) &&
                Contains(AllText(Find<RectTransform>(detail, "Component Dossier Effects Page")), ComponentArtCatalog.EnvironmentSummary(entry));
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(entry.Id);
            if (definition != null)
            {
                record.textPreserved &= Same(Find<Text>(detail, "Component Special Card Lore").text, definition.Lore);
                CoreBattleCard card = definition.Card;
                NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding :
                    card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
                record.textPreserved &= Same(Find<Text>(detail, "Component Special Card Effect").text,
                    CardFaceLayout.FormatDescription(preview.Controller.Session.BattleCardFaceDescription(card, stage)));
            }
            else
                record.textPreserved &= Find<RectTransform>(detail, "Component Dossier SpecialCards Page")
                    .GetComponentsInChildren<Text>(true).Select(text => text.text)
                    .SequenceEqual(new[] { "没有专属卡牌" });
            Require(record.textPreserved, "Source description/effect/story/card text changed or lost", record);
            Rect footerLane = Rect.MinMaxRect(-288f, -264f, 600f, -220f);
            string[] footerNames = { "Previous Component Dossier Page", "Component Dossier Page Counter", "Next Component Dossier Page",
                "Purchase Component " + preview.Offer.Id, "Purchase And Equip Component " + preview.Offer.Id };
            record.footerInsidePaper = true;
            record.footerSeparated = true;
            float right = float.NegativeInfinity;
            foreach (string footerName in footerNames)
            {
                Rect bounds = Bounds(detail, Find<RectTransform>(detail, footerName));
                record.footer.Add(new FooterRecord { name = footerName, bounds = bounds });
                record.footerInsidePaper &= Inside(footerLane, bounds);
                record.footerSeparated &= bounds.xMin - right >= 19f;
                right = bounds.xMax;
            }
            Require(record.footerInsidePaper, "Footer extends into parchment ornament/edge", record);
            Require(record.footerSeparated, "Footer controls overlap or have less than 19px separation", record);
            RectTransform pageRoot = Find<RectTransform>(detail, "Component Dossier " + Pages[page] + " Page");
            record.pageBounds = Bounds(detail, pageRoot);
            float footerTop = record.footer.Max(item => item.bounds.yMax);
            Rect[] contentBounds = pageRoot.Cast<Transform>().Where(child => child.gameObject.activeSelf && child is RectTransform)
                .Select(child => Bounds(detail, (RectTransform)child)).ToArray();
            record.pageClearOfFooter = contentBounds.All(bounds => bounds.yMin - footerTop >= 8f);
            Require(record.pageClearOfFooter, "Visible reading content is too close to the footer", record);
            Rect tab = Bounds(detail, Find<Button>(detail, "ComponentDossierPage" + Pages[page]).GetComponent<RectTransform>());
            Require(contentBounds.All(bounds => bounds.yMax <= tab.yMin - 4f), "Visible reading content overlaps its tab row", record);
            for (int other = 0; other < Pages.Length; other++)
                Require(Find<RectTransform>(detail, "Component Dossier " + Pages[other] + " Page").gameObject.activeSelf == (page == other), "Incorrect active page", record);
            foreach (Text text in detail.GetComponentsInChildren<Text>(false))
            {
                ScrollRect scroll = text.GetComponentInParent<ScrollRect>();
                TextRecord check = new TextRecord { name = text.name, characters = text.text.Length, fontSize = text.fontSize,
                    preferredHeight = text.preferredHeight, rectHeight = text.rectTransform.rect.height, scroll = scroll == null ? null : scroll.name };
                check.fits = check.preferredHeight <= check.rectHeight + 0.6f;
                check.scrollContentCoversText = scroll == null || (scroll.content != null && scroll.viewport != null &&
                    Inside(scroll.content.rect, Bounds(scroll.content, text.rectTransform)));
                record.texts.Add(check);
                Require(check.fits, "Text exceeds its rectangle: " + text.name + " (" + check.preferredHeight + "/" + check.rectHeight + ")", record);
                Require(check.scrollContentCoversText, "Scroll content does not contain complete text: " + text.name, record);
                if (scroll != null && scroll.transform.IsChildOf(pageRoot))
                    Require(Inside(pageRoot.rect, Bounds(pageRoot, scroll.viewport)), "Reading viewport extends beyond page: " + scroll.name, record);
            }
        }

        private sealed class Preview : IDisposable
        {
            private Scene scene;
            private GameObject host, cameraHost;
            private Camera camera;
            private Canvas canvas;
            private readonly RenderTexture previousActive;
            public CoreGameplayDemoController Controller { get; private set; }
            public Transform Root => canvas.transform;
            public RenderTexture Target { get; private set; }
            public PortComponentShopStockItem Offer { get; private set; }
            public Preview()
            {
                previousActive = RenderTexture.active;
                try
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    host = new GameObject("Component Dossier Read-Only Preview") { hideFlags = HideFlags.HideAndDontSave };
                    host.SetActive(false);
                    SceneManager.MoveGameObjectToScene(host, scene);
                    Controller = host.AddComponent<CoreGameplayDemoController>();
                    Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShopDetail, CoreGameplayPreviewPort.BrokenAnchor, 0);
                    host.SetActive(true);
                    canvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName).GetComponent<Canvas>();
                    cameraHost = new GameObject("Component Dossier Preview Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(cameraHost, scene);
                    camera = cameraHost.GetComponent<Camera>();
                    camera.enabled = false;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                    camera.orthographic = true;
                    camera.orthographicSize = Height * 0.5f;
                    camera.aspect = Width / (float)Height;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 2000f;
                    camera.transform.position = new Vector3(0f, 0f, -1000f);
                    camera.cullingMask = 1 << CaptureLayer;
                    camera.scene = scene;
                    camera.allowHDR = false;
                    camera.allowMSAA = false;
                    Target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Component Dossier Acceptance" };
                    Target.Create();
                    camera.targetTexture = Target;
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                    RectTransform rect = canvas.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(Width, Height);
                    rect.position = Vector3.zero;
                    rect.rotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                }
                catch { Dispose(); throw; }
            }

            public void SelectEntry(ComponentArtEntry entry)
            {
                Offer = new PortComponentShopStockItem("component-layout-review|" + entry.Id, Controller.Session.CurrentPort, "2026-10", entry, 0.94f, entry.Price);
                RectTransform panel = Find<RectTransform>(Root, "Component Shop Panel");
                foreach (Transform child in panel.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                typeof(CoreGameplayDemoController).GetField("selectedComponentShopOfferId", PrivateInstance).SetValue(Controller, Offer.Id);
                typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopDetail", PrivateInstance).Invoke(Controller, new object[] { panel, Offer });
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CaptureLayer;
            }

            public void SelectPage(int page)
            {
                decimal gold = Controller.Session.ProfileGold;
                string[] cards = Controller.Session.AcquiredSpecialCardIds.ToArray();
                string key = CoreGameplayDemoController.ComponentShopPurchaseKey(Offer.Id);
                bool saved = PlayerPrefs.HasKey(key);
                int purchase = PlayerPrefs.GetInt(key, 0);
                // The only invoked UI callbacks are the four local visibility tabs.
                Find<Button>(Root, "ComponentDossierPage" + Pages[page]).onClick.Invoke();
                if (gold != Controller.Session.ProfileGold || !cards.SequenceEqual(Controller.Session.AcquiredSpecialCardIds) ||
                    PlayerPrefs.HasKey(key) != saved || PlayerPrefs.GetInt(key, 0) != purchase)
                    throw new InvalidOperationException("A page tab changed purchase/session state.");
            }

            public void RenderStableFrame()
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = Target });
                    else camera.Render();
                }
            }

            public void Dispose()
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (host != null) Object.DestroyImmediate(host);
                if (Target != null) { Target.Release(); Object.DestroyImmediate(Target); }
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string AllText(Transform root) => string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(text => text.text));
        private static string Plain(string text) => Regex.Replace(Regex.Replace(text ?? "", "<[^>]+>", ""), @"\s+", "");
        private static bool Same(string left, string right) => Plain(left) == Plain(right);
        private static bool Contains(string value, string expected) => Plain(value).Contains(Plain(expected));
        private static void Require(bool pass, string message, Record record) { if (!pass && !record.issues.Contains(message)) record.issues.Add(message); }
        private static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.6f && inner.xMax <= outer.xMax + 0.6f && inner.yMin >= outer.yMin - 0.6f && inner.yMax <= outer.yMax + 0.6f;
        private static T Find<T>(Transform root, string name) where T : Component => root.GetComponentsInChildren<T>(true).Single(item => item.name == name);
        private static Rect Bounds(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
            foreach (Vector3 corner in corners) { Vector2 point = parent.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Dictionary<string, string> Snapshot(string name) => (Dictionary<string, string>)typeof(CrewDossierAcceptance).GetMethod(name, PrivateStatic).Invoke(null, null);
        private static bool Equal(Dictionary<string, string> left, Dictionary<string, string> right) => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string value) && pair.Value == value);
        private static bool Flag(string key) => Environment.GetCommandLineArgs().Contains(key);
        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-")) return args[index + 1];
            throw new ArgumentException("Missing " + key);
        }
        private static string Html(string value) => (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        private static void WriteGallery(string output, Review review)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang='zh-CN'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>部件详情排版审核</title><style>body{margin:24px;background:#171411;color:#ecdfc3;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(460px,1fr));gap:20px}figure{margin:0}img{width:100%;height:auto}a{color:#e6bc72}figcaption{padding:8px 0}input{padding:10px;width:min(560px,90%);margin-bottom:20px}</style><h1>部件详情排版审核</h1><p>");
            html.Append(review.components).Append(" 个部件，").Append(review.pages).Append(" 页检查，").Append(review.captures).Append(" 张截图；").Append(Html(review.mode)).Append("。</p><input id='filter' placeholder='筛选名称、品质、槽位或页面'><main>");
            foreach (Record record in review.records.Where(item => !string.IsNullOrEmpty(item.image)))
            {
                int page = Array.IndexOf(Pages, record.page);
                string caption = record.name + " · " + record.rarity + " · " + record.slot + " · " + Titles[page];
                html.Append("<figure data-caption='").Append(Html(caption)).Append("'><a href='").Append(Html(record.image)).Append("'><img loading='lazy' src='").Append(Html(record.image)).Append("'></a><figcaption>").Append(Html(caption)).Append("</figcaption></figure>");
            }
            html.Append("</main><p><a href='report.json'>全部部件布局与文字验收数据</a></p><script>document.querySelector('#filter').addEventListener('input',e=>document.querySelectorAll('figure').forEach(f=>f.hidden=!f.dataset.caption.toLowerCase().includes(e.target.value.toLowerCase())))</script></html>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
