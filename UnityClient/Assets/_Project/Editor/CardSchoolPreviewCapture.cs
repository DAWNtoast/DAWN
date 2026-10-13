using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    /// <summary>Disposable production card faces. Does not save scenes or player progress.</summary>
    public static class CardSchoolPreviewCapture
    {
        private const int Layer = 30, Width = 1920, Height = 1080;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public string capturedAtUtc;
            public string scope = "Actual production CreateCollectionCardFace with fixed face templates and the preview session's numeric quotes, artwork, school frames and resource badges. Disposable editor rendering; no gameplay simulation or authored-scene edits.";
            public int expectedSchoolCards = 132, schoolCards, renderedCards;
            public int previewRank;
            public string schoolFilter;
            public string cardFilter;
            public bool completeCatalogRender;
            public bool frameVariantPreview;
            public List<CardRecord> cards = new List<CardRecord>();
            public List<string> pages = new List<string>(), errors = new List<string>();
        }
        [Serializable] private sealed class CardRecord
        {
            public string id, artId, artResourcePath, frameResourcePath, specialResourceType, specialResourceCost, schoolId, name, effect, file, page;
            public bool artworkFound, individualArtwork, frameFound, textFits, costLabelsAtCorners, fullSourceUv, sourceAspectPreserved;
            public bool titleFitsAuthoredRibbon;
            public string titleFont;
            public Rect titleMeshBounds;
            public CardRibbonTitleLayout titleCurve;
            public int nameFontSize, effectFontSize, energy, powderCost, cannonballCost;
            public Rect artUv;
            public Vector2 illustrationSize, illustrationPosition, nameSize, effectSize;
            public Rect frameBounds;
        }

        // -executeMethod ShatteredSail.Editor.CardSchoolPreviewCapture.CaptureFromCommandLine
        // -cardSchoolCapturePath D:/Game/Artifacts/CardSchools20261011/Preview01
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped editor with graphics required.");
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if (!project.StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run only in the isolated D:/Game/tmp validation project.");
            string[] args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-cardSchoolCapturePath");
            int filterOption = Array.IndexOf(args, "-cardSchoolPreviewSchool");
            string schoolFilter = filterOption < 0 ? null : filterOption + 1 < args.Length ? args[filterOption + 1] : "";
            int cardFilterOption = Array.IndexOf(args, "-cardSchoolPreviewCard");
            string cardFilter = cardFilterOption < 0 ? null : cardFilterOption + 1 < args.Length ? args[cardFilterOption + 1] : "";
            int rankOption = Array.IndexOf(args, "-cardSchoolPreviewRank");
            int previewRank = rankOption < 0 ? 0 : int.Parse(args[rankOption + 1]);
            if (previewRank < 0 || previewRank > 3) throw new ArgumentException("Preview rank must be 0 through 3.");
            if (cardFilter != null && !CoreVoyageSession.CardLibrary.Any(card => card.Id == cardFilter && card.AllowedStages != NavalBattleStages.None))
                throw new ArgumentException("-cardSchoolPreviewCard requires an active card ID.");
            if (schoolFilter != null && !CardSchoolArtCatalog.SchoolIds.Contains(schoolFilter))
                throw new ArgumentException("-cardSchoolPreviewSchool requires one of the six schools; omit for a complete catalog capture.");
            if (option < 0 || option + 1 >= args.Length || !Path.IsPathRooted(args[option + 1])) throw new ArgumentException("Pass an absolute -cardSchoolCapturePath.");
            string output = Path.GetFullPath(args[option + 1]);
            if (!output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Capture output must stay in Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Capture directory must be new or empty.");
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "cards"));
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O"), schoolFilter = schoolFilter, cardFilter = cardFilter,
                completeCatalogRender = schoolFilter == null && cardFilter == null, previewRank = previewRank };
            if (args.Contains("-cardTitleRibbonPreview"))
            {
                report.completeCatalogRender = false;
                report.scope = "Four real production card definitions: wood, fire, arcane and the actual +3 navigator card; authored title font and curved glyph mesh.";
                string[] ids = { "card_cannon_barrage", "card_school_fire_b04", "card_special_cannon_worlds_end_volley", "card_navigator_course" };
                var session = new CoreVoyageSession();
                CoreBattleCard[] examples = ids.Select((id, ordinal) => {
                    int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == id);
                    return session.GetBattleCardAtLevel(index, ordinal == 3 ? 3 : 0);
                }).ToArray();
                CapturePage(output, report, "ribbon-title-examples", examples);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(output, report);
                if (report.errors.Count > 0) throw new InvalidOperationException("Title ribbon preview found issues; see report.json.");
                Debug.Log("CARD_TITLE_RIBBON_PREVIEW_OK: four actual cards, including +3.");
                return;
            }
            if (args.Contains("-cardFrameVariantPreview"))
            {
                report.frameVariantPreview = true;
                report.completeCatalogRender = false;
                report.scope = "Installed integrated-frame material/geometry fixtures rendered through the production card factory. Real illustration sources; sample costs and descriptions, not live card definitions. Missing variants intentionally omitted at this prototype stage.";
                foreach (string school in CardSchoolArtCatalog.SchoolIds.Concat(new[] { "neutral" }))
                {
                    CoreBattleCard source = CoreVoyageSession.CardLibrary.First(card => card.AllowedStages != NavalBattleStages.None &&
                        CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) == school);
                    var fixtures = new List<CoreBattleCard>();
                    for (int powder = 0; powder <= 1; powder++)
                        for (int cannonball = 0; cannonball <= 1; cannonball++)
                            if (CardSchoolArtCatalog.Frame(school, powder, cannonball) != null)
                                fixtures.Add(FrameReviewCard(source, powder, cannonball, null));
                    CoreSchoolMechanic special = school == "arcane" ? CoreSchoolMechanic.ArcaneWard :
                        school == "blood" ? CoreSchoolMechanic.BloodWard : school == "curse" ? CoreSchoolMechanic.CurseDrain :
                        school == "thunder" ? CoreSchoolMechanic.ThunderFinish : CoreSchoolMechanic.None;
                    if (special != CoreSchoolMechanic.None)
                        for (int resources = 0; resources <= 1; resources++)
                        {
                            CoreBattleCard card = FrameReviewCard(source, resources, resources, new CoreSchoolCardEffect(special, threshold: 2));
                            if (CardSchoolArtCatalog.Frame(card) != null) fixtures.Add(card);
                        }
                    if (fixtures.Count > 0) CapturePage(output, report, "frames-" + school, fixtures.ToArray());
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(output, report);
                if (report.errors.Count > 0) throw new InvalidOperationException("Frame review found issues; see report.json.");
                Debug.Log("CARD_FRAME_MATERIAL_PREVIEW_OK: " + report.renderedCards + " installed variants; fixture costs, not live cards.");
                return;
            }
            var rankSession = new CoreVoyageSession();
            CoreBattleCard[] catalog = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Select(index => rankSession.GetBattleCardAtLevel(index, previewRank)).Where(card => card != null).ToArray();
            CoreBattleCard[] schoolCards = catalog.Where(card => CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) != "neutral").ToArray();
            report.schoolCards = schoolCards.Length;
            if (schoolCards.Length != report.expectedSchoolCards) report.errors.Add("Expected 132 school cards, found " + schoolCards.Length);
            foreach (string school in CardSchoolArtCatalog.SchoolIds)
            {
                CoreBattleCard[] cards = schoolCards.Where(card => card.SchoolId == school).ToArray();
                if (cards.Length != 22) report.errors.Add(school + " has " + cards.Length + " cards, expected 22.");
                if (schoolFilter != null && schoolFilter != school) continue;
                if (cardFilter != null) cards = cards.Where(card => card.Id == cardFilter).ToArray();
                for (int offset = 0; offset < cards.Length; offset += 10)
                    CapturePage(output, report, school + "-" + (offset / 10 + 1), cards.Skip(offset).Take(10).ToArray());
            }
            // Include all neutral source cards as well, so blue components and the
            // remaining named crew can be checked against the ordinary wooden frame.
            CoreBattleCard[] neutral = catalog.Where(card => CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) == "neutral").ToArray();
            if (cardFilter != null) neutral = neutral.Where(card => card.Id == cardFilter).ToArray();
            for (int offset = 0; schoolFilter == null && offset < neutral.Length; offset += 10)
                CapturePage(output, report, "neutral-" + (offset / 10 + 1), neutral.Skip(offset).Take(10).ToArray());
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            WriteGallery(output, report);
            if (report.errors.Count > 0) throw new InvalidOperationException("Card capture found " + report.errors.Count + " issues; see report.json.");
            Debug.Log((report.completeCatalogRender ? "CARD_SCHOOL_PREVIEW_OK: " : "CARD_SCHOOL_PREVIEW_PARTIAL_OK: ") +
                report.schoolCards + " catalog school cards; " + report.renderedCards + " rendered faces; filter=" + (schoolFilter ?? "all"));
        }

        private static CoreBattleCard FrameReviewCard(CoreBattleCard source, int powder, int cannonball, CoreSchoolCardEffect special)
        {
            string school = CardSchoolArtCatalog.NormalizeSchool(source.SchoolId);
            string name = CoreSchoolUnlockCatalog.Find(school)?.Name ?? "普通木制";
            return new CoreBattleCard(source.Id, name + " · 边框样例", source.Type, 1, 0, 0, 0, 0, powder,
                "仅展示边框材质与费用位置。\n示例行动点 1；火药 " + powder + "；炮弹 " + cannonball + "。",
                source.AllowedStages, source.Action, source.RequiredCrew, source.ArtId, cannonball, source.RequiredComponent,
                0, source.SchoolId, special);
        }

        private static void CapturePage(string output, Report report, string pageId, CoreBattleCard[] cards)
        {
            Scene scene = default; RenderTexture target = null; Texture2D pixels = null;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Card School Preview Host") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(value => value.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                foreach (Transform child in canvas.transform) child.gameObject.SetActive(false);
                var gallery = new GameObject("School Card Gallery", typeof(RectTransform));
                gallery.transform.SetParent(canvas.transform, false);
                RectTransform panel = (RectTransform)gallery.transform;
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
                panel.sizeDelta = new Vector2(Width, Height);
                MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", PrivateInstance);
                if (factory == null) throw new MissingMethodException("Production card face factory missing.");
                var faces = new List<RectTransform>();
                for (int index = 0; index < cards.Length; index++)
                {
                    CoreBattleCard card = cards[index];
                    RectTransform face = (RectTransform)factory.Invoke(controller, new object[] { panel, card,
                        "Review " + index, "Review Art " + index, "Review Name " + index, 1f, null, "Review Effect " + index });
                    float rowPitch = CardFaceLayout.FrameSize.y + 12f;
                    int rows = Mathf.CeilToInt(cards.Length / 5f);
                    face.anchoredPosition = new Vector2(-560f + (index % 5) * 280f,
                        (rows - 1) * rowPitch * .5f - (index / 5) * rowPitch - CardSchoolArtCatalog.FrameBounds(card).center.y);
                    faces.Add(face);
                }
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                var cameraHost = new GameObject("Card School Preview Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene); Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.045f, .055f, .07f, 1f);
                camera.orthographic = true; camera.orthographicSize = 5f; camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f); camera.cullingMask = 1 << Layer;
                camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
                camera.targetTexture = target; camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in gallery.GetComponentsInChildren<Text>()) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                if (Mathf.Abs(canvas.scaleFactor - 1f) > .01f) throw new InvalidOperationException("Expected production 1920x1080 card scale 1; got " + canvas.scaleFactor);
                string pageFile = pageId + ".png"; report.pages.Add(pageFile);
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, pageFile), pixels.EncodeToPNG());
                for (int index = 0; index < cards.Length; index++)
                {
                    CoreBattleCard card = cards[index]; RectTransform face = faces[index];
                    RawImage art = face.GetComponentsInChildren<RawImage>().Single();
                    CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
                    Text name = face.GetComponentsInChildren<Text>().Single(text => text.name == "Review Name " + index);
                    Text effect = face.GetComponentsInChildren<Text>().Single(text => text.name == "Review Effect " + index);
                    CardRibbonTitleEffect titleCurve = name.GetComponent<CardRibbonTitleEffect>();
                    var record = new CardRecord { id = card.Id, artId = card.ArtId, schoolId = CardSchoolArtCatalog.NormalizeSchool(card.SchoolId),
                        artResourcePath = CardSchoolArtCatalog.DedicatedIllustrationPath(card), individualArtwork = CardSchoolArtCatalog.HasDedicatedArt(card),
                        frameResourcePath = CardSchoolArtCatalog.FrameResourcePath(card),
                        frameBounds = CardSchoolArtCatalog.FrameBounds(card),
                        specialResourceType = CardSpecialCostPresentation.ResourceType(card), specialResourceCost = CardSpecialCostPresentation.CostText(card),
                        name = name.text, effect = effect.text, page = pageFile,
                        file = "cards/" + (report.frameVariantPreview ? Path.GetFileName(CardSchoolArtCatalog.FrameResourcePath(card)) : card.Id) + ".png", artworkFound = art.texture != null,
                        frameFound = frame != null && frame.HasAuthoredTexture, nameFontSize = name.fontSize, effectFontSize = effect.fontSize,
                        titleFont = name.font != null ? name.font.name : string.Empty,
                        titleFitsAuthoredRibbon = titleCurve != null && titleCurve.FitsAuthoredRibbon && titleCurve.RenderedGlyphCount > 0,
                        titleMeshBounds = titleCurve != null ? titleCurve.RenderedBounds : Rect.zero,
                        titleCurve = titleCurve != null ? titleCurve.Layout : null,
                        costLabelsAtCorners = CostLabelsAtCorners(face, card),
                        textFits = name.preferredWidth <= name.rectTransform.rect.width + .1f && effect.preferredHeight <= effect.rectTransform.rect.height + .1f,
                        energy = card.Energy, powderCost = card.PowderCost, cannonballCost = card.CannonballCost, artUv = art.uvRect,
                        illustrationSize = art.rectTransform.sizeDelta, illustrationPosition = art.rectTransform.anchoredPosition,
                        nameSize = name.rectTransform.sizeDelta, effectSize = effect.rectTransform.sizeDelta,
                        fullSourceUv = art.uvRect == new Rect(0f, 0f, 1f, 1f),
                        sourceAspectPreserved = art.texture != null && Mathf.Abs(art.rectTransform.rect.width / art.rectTransform.rect.height -
                            (float)art.texture.width / art.texture.height) < .0001f };
                    report.cards.Add(record);
                    if (!record.artworkFound || !record.individualArtwork || !record.frameFound || !record.textFits || !record.costLabelsAtCorners ||
                        !record.fullSourceUv || !record.sourceAspectPreserved || !record.titleFitsAuthoredRibbon)
                        report.errors.Add(card.Id + ": missing individual art/frame, cropped/stretched illustration, text overflow or displaced cost label (" + record.artResourcePath + ").");
                    Vector2 centre = RectTransformUtility.WorldToScreenPoint(camera, face.position);
                    // Read the rendered card including its complete corner emblems. This is
                    // a screenshot of the production UI, not a substitute card illustration.
                    Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
                    const int margin = 4;
                    int cropWidth = Mathf.CeilToInt(bounds.width) + margin * 2;
                    int cropHeight = Mathf.CeilToInt(bounds.height) + margin * 2;
                    var crop = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false, false);
                    try { crop.ReadPixels(new Rect(Mathf.Round(centre.x + bounds.xMin - margin), Mathf.Round(centre.y + bounds.yMin - margin), cropWidth, cropHeight), 0, 0); crop.Apply(false, false);
                        File.WriteAllBytes(Path.Combine(output, record.file), crop.EncodeToPNG()); }
                    finally { Object.DestroyImmediate(crop); }
                    report.renderedCards++;
                }
            }
            catch (Exception error) { report.errors.Add(pageId + ": " + error); }
            finally
            {
                RenderTexture.active = oldActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static bool CostLabelsAtCorners(RectTransform face, CoreBattleCard card)
        {
            CardOrnamentGraphic[] labels = face.GetComponentsInChildren<CardOrnamentGraphic>().Where(item => item.IsMarkerOnly).ToArray();
            int expected = 1 + (card.PowderCost > 0 ? 1 : 0) + (card.CannonballCost > 0 ? 1 : 0) +
                (CardSpecialCostPresentation.ResourceType(card).Length > 0 ? 1 : 0);
            if (labels.Length != expected) return false;
            foreach (CardOrnamentGraphic label in labels)
            {
                Vector2 expectedCorner;
                switch (label.Kind)
                {
                    case CardOrnamentKind.Energy: expectedCorner = CardFaceLayout.EnergyIconPosition; break;
                    case CardOrnamentKind.Gunpowder: expectedCorner = CardFaceLayout.PowderIconPosition; break;
                    case CardOrnamentKind.Cannonball: expectedCorner = CardFaceLayout.CannonballIconPosition; break;
                    default: expectedCorner = CardFaceLayout.SpecialCostIconPosition; break;
                }
                // A little optical adjustment is valid; the middle of the card is not.
                if (Vector2.Distance(label.rectTransform.anchoredPosition, expectedCorner) > 12f) return false;
                Text value = label.GetComponentInChildren<Text>();
                if (value == null || value.rectTransform.anchoredPosition != Vector2.zero) return false;
            }
            return true;
        }

        private static void WriteGallery(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>游戏内体系卡牌</title><style>body{background:#10141c;color:#eee;font:16px system-ui;margin:32px}section{display:flex;flex-wrap:wrap;gap:20px}figure{margin:0;width:280px}img{width:280px;height:auto}figcaption{font-size:12px;overflow-wrap:anywhere;margin:8px 0 24px}h2{clear:both}</style><h1>游戏内体系卡牌</h1><p>生产卡面渲染 · 完整效果 / 独立卡图 / 体系边框 / 费用</p>");
            if (report.frameVariantPreview) html.Append("<p>一体边框材质样例：使用示例费用，只展示已安装变体，不代表真实卡牌数值。</p>");
            else if (!report.completeCatalogRender) html.Append("<p>局部预览：").Append(report.cardFilter ?? report.schoolFilter).Append("。仅报告所列卡牌。</p>");
            foreach (var group in report.cards.GroupBy(card => card.schoolId))
            {
                html.Append("<h2>").Append(group.Key).Append("</h2><section>");
                foreach (CardRecord card in group)
                    html.Append("<figure><a href='").Append(card.file).Append("'><img loading='lazy' src='").Append(card.file).Append("'></a><figcaption>")
                        .Append(System.Net.WebUtility.HtmlEncode(card.name)).Append("<br>").Append(card.id).Append("</figcaption></figure>");
                html.Append("</section>");
            }
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
