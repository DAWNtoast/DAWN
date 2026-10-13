// Copy into an isolated Unity project's Editor directory. Synchronous, graphics required.
// No production asset changes, scene saves, PlayerPrefs or persistence calls.
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
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class CardClarityCapture
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Layer = 30, Columns = 4;
        private static readonly float[] Scales = { 1f, .76f };

        [Serializable] private sealed class Report
        {
            public string variant, startedUtc, completedUtc, unityVersion, projectPath, status;
            public string scope = "Production CreateCollectionCardFace, actual ScreenSpaceCamera/CanvasScaler at 1920x1080 and 3840x2160. Upper row scale 1, lower row workshop scale .76. Crops are unchanged pixels read directly from the same render target. Catalog checks cover active cards at ranks 0 and 3; no progress or preferences are loaded or saved.";
            public bool catalogRequested;
            public int activeCardCount, catalogChecks, failedChecks;
            public List<string> representativeIds = new List<string>(), pages = new List<string>(), errors = new List<string>();
            public List<Record> representatives = new List<Record>(), catalog = new List<Record>();
        }

        [Serializable] private sealed class TextureRecord
        {
            public string name, assetPath, format, filterMode;
            public int width, height, mipmapCount;
            public long runtimeBytes;
        }

        [Serializable] private sealed class Record
        {
            public string id, school, title, effect, titleFont, frameFile, cardFile, titleFile, artFile;
            public int rank, width, height, hostScreenWidth, hostScreenHeight, titleFontSize, effectFontSize;
            public int expectedTitleGlyphs, renderedTitleGlyphs, effectGlyphs, fullEffectGlyphs;
            public float canvasScale, cardScale, textPixelsPerUnit, renderedTitlePixelsPerUnit;
            public Vector2 canvasDisplaySize, canvasReferenceResolution;
            public Rect canvasPixelRect, titleLocalBounds, titlePixelBounds, cardPixelBounds, artPixelBounds;
            public bool titleFits, titleComplete, effectFits, effectComplete, frameFitsTarget, passed;
            public TextureRecord artTexture, frameTexture, titleAtlas;
        }

        // -executeMethod ShatteredSail.Editor.CardClarityCapture.CaptureFromCommandLine
        // -cardClarityOutput D:/Game/Artifacts/DisplayClarity20261013/oldtitle512
        // -cardClarityVariant oldtitle512
        // Optional: -cardClaritySkipCatalog, -cardClarityCards id1,id2,...
        public static void CaptureFromCommandLine()
        {
            Require(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode, "Stopped Editor required.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            Require(project.StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase), "Use an isolated D:/Game/tmp project.");
            string requested = Argument("-cardClarityOutput");
            Require(!string.IsNullOrEmpty(requested) && Path.IsPathRooted(requested), "Absolute -cardClarityOutput required.");
            string output = Path.GetFullPath(requested);
            Require(output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase), "Output must stay inside D:/Game/Artifacts.");
            Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Use a new or empty output directory.");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Path.Combine(output, "crops"));
            var report = new Report { variant = Argument("-cardClarityVariant") ?? "unspecified", startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, projectPath = project,
                catalogRequested = !Environment.GetCommandLineArgs().Contains("-cardClaritySkipCatalog"), status = "running" };
            try
            {
                EnsurePipeline();
                CardSchoolArtCatalog.ReleaseLoadedTextures();
                var session = new CoreVoyageSession();
                int[] active = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .Where(i => CoreVoyageSession.CardLibrary[i].AllowedStages != NavalBattleStages.None).ToArray();
                report.activeCardCount = active.Length;
                int[] examples = RepresentativeIndices(active);
                report.representativeIds.AddRange(examples.Select(i => CoreVoyageSession.CardLibrary[i].Id));
                foreach (int width in new[] { 1920, 3840 })
                {
                    using (var preview = new Preview(width, width * 9 / 16))
                    {
                        for (int offset = 0; offset < examples.Length; offset += Columns)
                        {
                            CoreBattleCard[] cards = examples.Skip(offset).Take(Columns).Select(i => session.GetBattleCardAtLevel(i, 3)).ToArray();
                            string page = "representatives-" + width + "-page" + (offset / Columns + 1);
                            CaptureBatch(preview, cards, 3, page, output, report, true);
                        }
                        if (report.catalogRequested)
                            foreach (int rank in new[] { 0, 3 })
                                for (int offset = 0; offset < active.Length; offset += Columns)
                                {
                                    CoreBattleCard[] cards = active.Skip(offset).Take(Columns).Select(i => session.GetBattleCardAtLevel(i, rank)).ToArray();
                                    CaptureBatch(preview, cards, rank, "catalog-" + width + "-rank" + rank + "-" + offset, output, report, false);
                                }
                    }
                }
                // Baseline layout failures are evidence, not a reason to abort the remaining
                // captures. Operational failures still return a failing executeMethod below.
                report.status = report.failedChecks == 0 ? "passed" : "completed-with-layout-failures";
            }
            catch (Exception error) { report.status = "failed"; report.errors.Add(error.ToString()); }
            finally
            {
                report.completedUtc = DateTime.UtcNow.ToString("O");
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteIndex(output, report);
            }
            Require(report.status != "failed", "Card clarity capture failed; see " + Path.Combine(output, "report.json"));
            Debug.Log("CARD_CLARITY_CAPTURE_COMPLETE: " + report.variant + "; representative faces " + report.representatives.Count +
                "; catalog checks " + report.catalogChecks + "; layout failures " + report.failedChecks + "; " + output);
        }

        private static int[] RepresentativeIndices(int[] active)
        {
            string chosen = Argument("-cardClarityCards");
            if (!string.IsNullOrEmpty(chosen))
                return chosen.Split(',').Select(id => active.Single(i => CoreVoyageSession.CardLibrary[i].Id == id.Trim())).ToArray();
            // Long names exercise the ribbon and the actual rank +3 suffix in each school.
            return CardSchoolArtCatalog.SchoolIds.Concat(new[] { "neutral" }).Select(school => active
                .Where(i => CardSchoolArtCatalog.NormalizeSchool(CoreVoyageSession.CardLibrary[i].SchoolId) == school)
                .OrderByDescending(i => CardFaceLayout.DisplayName(CoreVoyageSession.CardLibrary[i]).Length)
                .ThenBy(i => CoreVoyageSession.CardLibrary[i].Id, StringComparer.Ordinal).First()).ToArray();
        }

        private static void CaptureBatch(Preview preview, CoreBattleCard[] cards, int rank, string page,
            string output, Report report, bool saveImages)
        {
            preview.Clear();
            var faces = new List<RectTransform>();
            for (int row = 0; row < Scales.Length; row++)
                for (int column = 0; column < cards.Length; column++)
                    faces.Add(preview.Add(cards[column], row, column));
            preview.Render();
            string frameFile = page + ".png";
            if (saveImages)
            {
                preview.SavePixels(output, frameFile, new Rect(0, 0, preview.Width, preview.Height));
                report.pages.Add(frameFile);
            }
            for (int index = 0; index < faces.Count; index++)
            {
                int row = index / cards.Length, column = index % cards.Length;
                CoreBattleCard card = cards[column];
                Record record = Measure(preview, faces[index], card, rank, Scales[row]);
                if (saveImages)
                {
                    string stem = "crops/" + preview.Width + "-" + card.Id + "-rank" + rank + (row == 0 ? "-inspect" : "-workshop");
                    record.frameFile = frameFile;
                    record.cardFile = stem + "-card.png";
                    record.titleFile = stem + "-title.png";
                    record.artFile = stem + "-art.png";
                    preview.SavePixels(output, record.cardFile, record.cardPixelBounds);
                    preview.SavePixels(output, record.titleFile, record.titlePixelBounds);
                    preview.SavePixels(output, record.artFile, record.artPixelBounds);
                    report.representatives.Add(record);
                }
                else { report.catalog.Add(record); report.catalogChecks++; }
                if (!record.passed)
                {
                    report.failedChecks++;
                    report.errors.Add(page + "/" + card.Id + "/scale=" + Scales[row] + ": titleFits=" + record.titleFits +
                        ", titleComplete=" + record.titleComplete + ", effectFits=" + record.effectFits +
                        ", effectComplete=" + record.effectComplete + ", frameFits=" + record.frameFitsTarget);
                }
            }
        }

        private static Record Measure(Preview preview, RectTransform face, CoreBattleCard card, int rank, float scale)
        {
            Text title = face.GetComponentsInChildren<Text>().Single(t => t.name == "Clarity Title");
            Text effect = face.GetComponentsInChildren<Text>().Single(t => t.name == "Clarity Effect");
            CardRibbonTitleEffect curve = title.GetComponent<CardRibbonTitleEffect>();
            RawImage art = face.GetComponentsInChildren<RawImage>().Single();
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            int expected = title.text.Count(c => !char.IsWhiteSpace(c));
            int actualEffect = CountQuads(effect.cachedTextGenerator.verts);
            int fullEffect;
            using (var full = new TextGenerator())
            {
                TextGenerationSettings settings = effect.GetGenerationSettings(effect.rectTransform.rect.size);
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                settings.resizeTextForBestFit = false;
                full.Populate(effect.text, settings);
                fullEffect = CountQuads(full.verts);
            }
            Rect titleBounds = curve != null ? curve.RenderedBounds : Rect.zero;
            Rect cardPixels = PixelBounds(preview.Camera, face, CardSchoolArtCatalog.FrameBounds(card), 4);
            // The title crop includes the complete authored paper band, plus any overflowing glyph.
            Rect ribbonBounds = ((RectTransform)title.transform.parent).rect;
            Rect ribbonPixels = PixelBounds(preview.Camera, (RectTransform)title.transform.parent, ribbonBounds, 2);
            Rect titlePixels = PixelBounds(preview.Camera, title.rectTransform, titleBounds, 2);
            titlePixels = Rect.MinMaxRect(Mathf.Min(ribbonPixels.xMin, titlePixels.xMin), Mathf.Min(ribbonPixels.yMin, titlePixels.yMin),
                Mathf.Max(ribbonPixels.xMax, titlePixels.xMax), Mathf.Max(ribbonPixels.yMax, titlePixels.yMax));
            var record = new Record { id = card.Id, school = CardSchoolArtCatalog.NormalizeSchool(card.SchoolId), rank = rank,
                title = title.text, effect = effect.text, width = preview.Width, height = preview.Height,
                hostScreenWidth = Screen.width, hostScreenHeight = Screen.height, canvasScale = preview.Canvas.scaleFactor,
                canvasDisplaySize = preview.Canvas.renderingDisplaySize, canvasPixelRect = preview.Canvas.pixelRect,
                canvasReferenceResolution = preview.Scaler.referenceResolution, cardScale = scale, textPixelsPerUnit = title.pixelsPerUnit,
                // The old baseline lacks this diagnostic property; its canonical title stream is 1x.
                renderedTitlePixelsPerUnit = ReadRenderedDensity(curve),
                titleFont = title.font != null ? title.font.name : "", titleFontSize = title.fontSize, effectFontSize = effect.fontSize,
                expectedTitleGlyphs = expected, renderedTitleGlyphs = curve != null ? curve.RenderedGlyphCount : 0,
                effectGlyphs = actualEffect, fullEffectGlyphs = fullEffect, titleLocalBounds = titleBounds,
                titlePixelBounds = titlePixels, cardPixelBounds = cardPixels,
                artPixelBounds = PixelBounds(preview.Camera, art.rectTransform, art.rectTransform.rect, 0),
                titleFits = curve != null && curve.FitsAuthoredRibbon,
                titleComplete = curve != null && curve.RenderedGlyphCount == expected && expected > 0,
                effectFits = effect.preferredHeight <= effect.rectTransform.rect.height + .5f,
                effectComplete = actualEffect == fullEffect && fullEffect > 0,
                frameFitsTarget = cardPixels.xMin >= 0 && cardPixels.yMin >= 0 && cardPixels.xMax <= preview.Width && cardPixels.yMax <= preview.Height,
                artTexture = DescribeTexture(art.texture), frameTexture = DescribeTexture(frame != null ? frame.mainTexture : null),
                titleAtlas = DescribeTexture(title.font != null && title.font.material != null ? title.font.material.mainTexture : null) };
            record.passed = record.titleFits && record.titleComplete && record.effectFits && record.effectComplete && record.frameFitsTarget && art.texture != null;
            return record;
        }

        private static int CountQuads(IList<UIVertex> vertices)
        {
            int result = 0;
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                Vector2 min = vertices[i].position, max = min;
                for (int j = 1; j < 4; j++) { Vector2 p = vertices[i + j].position; min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                if (max.x - min.x > .001f && max.y - min.y > .001f) result++;
            }
            return result;
        }

        private static float ReadRenderedDensity(CardRibbonTitleEffect curve)
        {
            if (curve == null) return 0f;
            PropertyInfo property = curve.GetType().GetProperty("RenderedPixelsPerUnit", BindingFlags.Instance | BindingFlags.Public);
            return property != null ? Convert.ToSingle(property.GetValue(curve)) : 1f;
        }

        private static TextureRecord DescribeTexture(Texture texture)
        {
            if (texture == null) return null;
            return new TextureRecord { name = texture.name, assetPath = AssetDatabase.GetAssetPath(texture), width = texture.width,
                height = texture.height, format = texture is Texture2D two ? two.format.ToString() : texture.GetType().Name,
                filterMode = texture.filterMode.ToString(), mipmapCount = texture.mipmapCount, runtimeBytes = Profiler.GetRuntimeMemorySizeLong(texture) };
        }

        private static Rect PixelBounds(Camera camera, RectTransform transform, Rect local, int margin)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (Vector2 point in new[] { local.min, new Vector2(local.xMin, local.yMax), local.max, new Vector2(local.xMax, local.yMin) })
            {
                Vector2 pixel = RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(point));
                min = Vector2.Min(min, pixel); max = Vector2.Max(max, pixel);
            }
            return Rect.MinMaxRect(Mathf.Floor(min.x) - margin, Mathf.Floor(min.y) - margin, Mathf.Ceil(max.x) + margin, Mathf.Ceil(max.y) + margin);
        }

        private sealed class Preview : IDisposable
        {
            public readonly int Width, Height;
            public Canvas Canvas { get; private set; }
            public CanvasScaler Scaler { get; private set; }
            public Camera Camera { get; private set; }
            private Scene scene;
            private GameObject host;
            private RectTransform gallery;
            private CoreGameplayDemoController controller;
            private RenderTexture target;
            private MethodInfo factory, handle;

            public Preview(int width, int height)
            {
                Width = width; Height = height;
                try
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    host = new GameObject("Disposable Card Clarity Host") { hideFlags = HideFlags.HideAndDontSave };
                    host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                    controller = host.AddComponent<CoreGameplayDemoController>();
                    controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
                    host.SetActive(true);
                    Canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                    foreach (Transform child in Canvas.transform) child.gameObject.SetActive(false);
                    gallery = (RectTransform)new GameObject("Card Clarity Gallery", typeof(RectTransform)).transform;
                    gallery.SetParent(Canvas.transform, false);
                    gallery.anchorMin = gallery.anchorMax = gallery.pivot = new Vector2(.5f, .5f);
                    gallery.sizeDelta = new Vector2(1920, 1080);
                    factory = typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", PrivateInstance);
                    handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                    Require(factory != null && handle != null, "Production card factory or CanvasScaler.Handle unavailable.");
                    var cameraHost = new GameObject("Card Clarity Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(cameraHost, scene);
                    Camera = cameraHost.GetComponent<Camera>();
                    Camera.enabled = false; Camera.scene = scene; Camera.clearFlags = CameraClearFlags.SolidColor;
                    Camera.backgroundColor = new Color(.045f, .055f, .07f, 1f);
                    Camera.orthographic = true; Camera.orthographicSize = 5f; Camera.nearClipPlane = .1f; Camera.farClipPlane = 2000f;
                    Camera.transform.position = new Vector3(0, 0, -1000); Camera.cullingMask = 1 << Layer;
                    Camera.allowHDR = false; Camera.allowMSAA = false;
                    target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Require(target.Create(), "RenderTexture creation failed.");
                    Camera.targetTexture = target; Camera.aspect = width / (float)height;
                    Canvas.renderMode = RenderMode.ScreenSpaceCamera; Canvas.worldCamera = Camera; Canvas.planeDistance = 100f;
                    Scaler = Canvas.GetComponent<CanvasScaler>();
                    Require(Scaler != null && Scaler.enabled, "Production CanvasScaler must remain enabled.");
                }
                catch { Dispose(); throw; }
            }

            public void Clear()
            {
                while (gallery.childCount > 0) Object.DestroyImmediate(gallery.GetChild(0).gameObject);
            }

            public RectTransform Add(CoreBattleCard card, int row, int column)
            {
                Require(card != null, "Missing ranked production card.");
                RectTransform face = (RectTransform)factory.Invoke(controller, new object[] { gallery, card, "Clarity " + row + " " + column,
                    "Clarity Art", "Clarity Title", Scales[row], null, "Clarity Effect" });
                Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
                face.anchoredPosition = new Vector2(-615f + column * 410f, row == 0 ? 240f : -220f) - bounds.center * Scales[row];
                return face;
            }

            public void Render()
            {
                foreach (Transform child in Canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                for (int pass = 0; pass < 3; pass++)
                {
                    UnityEngine.Canvas.ForceUpdateCanvases(); handle.Invoke(Scaler, null);
                    foreach (Text text in gallery.GetComponentsInChildren<Text>()) text.SetAllDirty();
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(Camera, new RenderPipeline.StandardRequest { destination = target });
                    else Camera.Render();
                }
                Require(Mathf.Abs(Canvas.scaleFactor - Width / 1920f) < .001f &&
                    Mathf.Abs(Canvas.renderingDisplaySize.x - Width) < .5f && Mathf.Abs(Canvas.renderingDisplaySize.y - Height) < .5f &&
                    Mathf.Abs(Canvas.pixelRect.width - Width) < .5f && Mathf.Abs(Canvas.pixelRect.height - Height) < .5f,
                    "Render target did not drive actual production CanvasScaler: " + Canvas.scaleFactor + ", " + Canvas.renderingDisplaySize);
            }

            public void SavePixels(string output, string file, Rect area)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(area.xMin), 0, Width - 1), y = Mathf.Clamp(Mathf.FloorToInt(area.yMin), 0, Height - 1);
                int width = Mathf.Clamp(Mathf.CeilToInt(area.xMax), x + 1, Width) - x;
                int height = Mathf.Clamp(Mathf.CeilToInt(area.yMax), y + 1, Height) - y;
                RenderTexture previous = RenderTexture.active;
                Texture2D pixels = null;
                try
                {
                    RenderTexture.active = target;
                    pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                    pixels.ReadPixels(new Rect(x, y, width, height), 0, 0, false); pixels.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(output, file), pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; if (pixels != null) Object.DestroyImmediate(pixels); }
            }

            public void Dispose()
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void EnsurePipeline()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null || RenderPipelineManager.currentPipeline != null) return;
            // executeMethod can run before the Editor's first graphics frame. SubmitRenderRequest
            // requires the actual pipeline instance; invoke the engine's normal creation path.
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            MethodInfo prepare = typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", flags,
                null, new[] { typeof(RenderPipelineAsset) }, null) ??
                typeof(RenderPipelineManager).GetMethod("PrepareRenderPipeline", flags,
                    null, new[] { typeof(RenderPipelineAsset) }, null);
            Require(prepare != null, "SRP is not initialized and the engine's pipeline preparation entry is unavailable.");
            object prepared = prepare.Invoke(null, new object[] { asset });
            Require(!(prepared is bool ready) || ready, "The engine declined to prepare its current SRP asset.");
            Require(RenderPipelineManager.currentPipeline != null, "SRP instance was not created; refusing an empty capture.");
        }

        private static void WriteIndex(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Card clarity</title><style>body{background:#151922;color:#eee;font:16px system-ui;margin:24px}img{display:block;max-width:100%;height:auto}a{color:#9bd}figure{margin:16px 0}small{color:#bbc}</style>");
            html.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(report.variant)).Append("</h1><p>真实 Unity 画布渲染；上排 1.0，下排工坊 0.76。页面全景适应浏览器窗口；裁图链接保留原始像素。</p>");
            foreach (string page in report.pages) html.Append("<figure><a href='").Append(page).Append("'>").Append(page).Append("</a><img src='").Append(page).Append("'></figure>");
            foreach (Record record in report.representatives)
                html.Append("<p>").Append(System.Net.WebUtility.HtmlEncode(record.title)).Append(" · ").Append(record.width).Append(" · scale ").Append(record.cardScale)
                    .Append(" <a href='").Append(record.cardFile).Append("'>原像素卡面</a> <a href='").Append(record.titleFile).Append("'>卡名</a> <a href='").Append(record.artFile).Append("'>插画</a></p>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    }
}
