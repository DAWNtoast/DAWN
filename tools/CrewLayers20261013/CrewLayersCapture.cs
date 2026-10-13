// Copy only into the isolated project's Editor folder. No production assets or scenes are written.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
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
    public static class CrewLayersCapture
    {
        private const string AtlasPath = "Assets/_Project/Resources/PortArt/CrewRegional/crew_broken_anchor_portraits_4k_layers_v1.jpg";
        private const string FramePath = "Assets/_Project/Resources/PortArt/UI/tavern_crew_dossier_frame_4k_layers_v1.jpg";
        private const string PortraitName = "Tavern Dossier Atlas Portrait";
        private const string DefaultFrameName = "Tavern Dossier Frame Foreground";
        private const string ExpectedLayerShader = "Shattered Sail/UI/Crew Portrait Chroma Key";
        private const int Layer = 30;
        private static readonly string[] Pages = { "Background", "Story", "Skills", "SpecialCards" };
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo SetMode = typeof(CoreGameplayDemoController).GetMethod("SetTavernDossierAtlasComparison", BindingFlags.Instance | BindingFlags.Public);
        private static readonly PropertyInfo ReadMode = typeof(CoreGameplayDemoController).GetProperty("TavernDossierAtlasComparison", BindingFlags.Instance | BindingFlags.Public);

        [Serializable] public sealed class Report
        {
            public string status = "running", startedUtc, completedUtc, project, unityVersion;
            public string scope = "Six Broken Anchor crew; original A/independent portrait+frame B on the same dossier at native 3840x2160 and 1920x1080. Four real page tabs exercised in both modes. Read-only production builders in unsaved preview scenes; no Play, saves, recruitment or source writes.";
            public string memoryScope = "Profiler.GetRuntimeMemorySizeLong is editor-side Unity resource memory, not measured game VRAM.";
            public string timingScope = "Synchronous SetTavernDossierAtlasComparison setter wall time in the Editor, including any synchronous mask/GPU readback triggered by that setter; excludes the subsequent four capture renders, PNG encoding, and is not game FPS.";
            public string comparisonScope = "Record B screenRect as the complete unmasked portrait mesh, and visibleScreenRect after ancestor RectMask2D/screen intersection. New layout can crop the bottom of a portrait. Compare visible area before attributing sharper appearance to source quality. A portrait is baked into a complete dossier painting, so its separate subject bounds cannot be obtained from a UI mesh.";
            public int expectedFrames = 24, capturedFrames, passedFrames, expectedPagePairs = 48, passedPagePairs;
            public string requestedFrameNode;
            public int requestedFrameWidth, requestedFrameHeight;
            public TextureInfo atlas, frameTexture;
            public List<Frame> frames = new List<Frame>();
            public List<PagePair> pagePairs = new List<PagePair>();
            public List<SelectionCheck> selections = new List<SelectionCheck>();
            public List<SwitchTiming> timings = new List<SwitchTiming>();
            public List<string> errors = new List<string>();
        }
        [Serializable] public sealed class SwitchTiming
        {
            public int width, height, profession, page;
            public string phase;
            public bool fromAtlas, toAtlas, portraitExistedBefore;
            public double setterMilliseconds;
        }
        [Serializable] public sealed class SelectionCheck
        {
            public int width, height, profession;
            public string crewId, name;
            public bool modeBefore, modeAfter, preservedAcrossSelection;
        }
        [Serializable] public sealed class Frame
        {
            public int width, height, profession;
            public string crewId, crewName, variant, page = "Background", file, status, screenshotSha256;
            public bool captured, imageHasContent, scalerVerified;
            public float canvasScale;
            public Rect canvasPixelRect;
            public Snapshot state;
            public LayerCheck layers;
            public List<GraphicInfo> graphics = new List<GraphicInfo>();
            public List<TextInfo> texts = new List<TextInfo>();
            public List<string> errors = new List<string>();
        }
        [Serializable] public sealed class Snapshot
        {
            public int detailId, profileId, nameId, sessionId, selection, page;
            public string gold;
            public string crewId, name, documentTextSha256, profileGeometrySha256, counter;
            public bool atlasMode;
        }
        [Serializable] public sealed class PagePair
        {
            public int width, height, profession;
            public string crewId, page;
            public bool originalTabWorked, atlasTabWorked, originalToAtlasPreserved, atlasToOriginalPreserved;
            public bool atlasAndFrameIndependent, originalHasNoCandidateLayers, restoredHasNoCandidateLayers;
            public Snapshot original, atlas, restored;
            public List<TextInfo> originalLayout = new List<TextInfo>(), atlasLayout = new List<TextInfo>();
            public List<string> errors = new List<string>();
        }
        [Serializable] public sealed class TextureInfo
        {
            public string asset, name, sourceSha256, format, graphicsFormat, filter;
            public int width, height, sourceWidth, sourceHeight, mipmapCount, effectiveMaxSize;
            public long runtimeBytes;
            public bool readable;
        }
        [Serializable] public sealed class GraphicInfo
        {
            public string path, type, material, shader;
            public int graphicId, textureId, materialId, siblingIndex;
            public bool shaderSupported;
            public TextureInfo texture;
            public List<MaterialTextureInfo> materialTextures = new List<MaterialTextureInfo>();
            public Rect rawSourceUv, meshUv, sourcePixels, screenRect;
            public Rect visibleScreenRect, estimatedVisibleSourcePixels;
            public bool clippedByMaskOrScreen;
            public List<MaskInfo> ancestorMasks = new List<MaskInfo>();
            public bool rawImage, culled, raycastTarget;
            public float displayPixelsPerSourcePixel;
        }
        [Serializable] public sealed class MaterialTextureInfo
        {
            public string property, name, asset;
            public int instanceId, width, height;
        }
        [Serializable] public sealed class LayerCheck
        {
            public bool expectedLayered, independentGraphicsAndTextures, candidateLayersAbsent;
            public int portraitCount, frameCount;
            public string portraitPath, framePath, portraitShader, frameShader;
            public int portraitGraphicId, frameGraphicId, portraitTextureId, frameTextureId;
            public Rect portraitRawRect, portraitVisibleRect, frameRawRect, frameVisibleRect;
        }
        [Serializable] public sealed class MaskInfo
        {
            public string path;
            public Rect screenRect;
            public Vector4 padding;
        }
        [Serializable] public sealed class TextInfo
        {
            public string path, content, fontStyle;
            public int fontSize, vertexCount;
            public Rect rectPixels, meshPixels;
            public float pixelsPerUnit, preferredHeight, rectHeight;
            public bool active, culled, inScrollViewport, preferredHeightExceedsRect;
        }

        // -executeMethod ShatteredSail.Editor.CrewLayersCapture.Run
        // CREW_LAYERS_CAPTURE_DIR must point to a new/empty absolute directory outside Unity projects.
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            Require(project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase), "Use an isolated project beneath D:/Game/tmp.");
            Require(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode, "Stopped editor required.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU capture requires graphics enabled.");
            Require(SetMode != null && ReadMode != null, "Production A/B comparison API is not installed.");
            string requested = Environment.GetEnvironmentVariable("CREW_LAYERS_CAPTURE_DIR");
            Require(!string.IsNullOrWhiteSpace(requested) && Path.IsPathRooted(requested), "Set CREW_LAYERS_CAPTURE_DIR to a new absolute directory.");
            string output = Path.GetFullPath(requested).Replace('\\', '/').TrimEnd('/');
            Require(!output.Equals(project, StringComparison.OrdinalIgnoreCase) && !output.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase) &&
                !output.Equals("D:/Game/UnityClient", StringComparison.OrdinalIgnoreCase) && !output.StartsWith("D:/Game/UnityClient/", StringComparison.OrdinalIgnoreCase), "Write evidence outside the Unity projects.");
            Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Output must be new or empty.");
            Directory.CreateDirectory(output);
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), project = project, unityVersion = Application.unityVersion };
            try
            {
                report.requestedFrameNode = Environment.GetEnvironmentVariable("CREW_LAYERS_FRAME_NODE") ?? DefaultFrameName;
                report.requestedFrameWidth = ReadOptionalPositiveInt("CREW_LAYERS_FRAME_WIDTH");
                report.requestedFrameHeight = ReadOptionalPositiveInt("CREW_LAYERS_FRAME_HEIGHT");
                EnsurePipeline();
                Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
                Texture2D frameTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FramePath);
                Require(atlas != null, "Candidate atlas is missing.");
                Require(frameTexture != null, "Candidate frame is missing.");
                report.atlas = MeasureTexture(atlas, project);
                report.frameTexture = MeasureTexture(frameTexture, project);
                Require(atlas.width == 4096 && atlas.height == 4096 && atlas.format == TextureFormat.BC7, "Atlas must import at 4096x4096 BC7.");
                ValidateNativeTexture(report.atlas, "Atlas", 4096, 4096);
                ValidateNativeTexture(report.frameTexture, "Frame", 4096, 8192);
                Require(frameTexture.width == 6336 && frameTexture.height == 2688, "Shared frame must import at its native 6336x2688 size.");
                Require(report.requestedFrameWidth == 0 || frameTexture.width == report.requestedFrameWidth, "Frame width differs from requested final source width.");
                Require(report.requestedFrameHeight == 0 || frameTexture.height == report.requestedFrameHeight, "Frame height differs from requested final source height.");
                foreach (Vector2Int size in new[] { new Vector2Int(3840, 2160), new Vector2Int(1920, 1080) })
                {
                    using (var fixture = new Fixture(size.x, size.y))
                    {
                        Require(!Mode(fixture.Controller), "Comparison must default to original mode.");
                        for (int profession = 0; profession < 6; profession++)
                        {
                            try { CaptureCrew(report, fixture, profession, project, output); }
                            catch (Exception error) { report.errors.Add(size + " crew " + profession + ": " + error); }
                            WriteReport(output, report);
                        }
                    }
                }
                Require(Hash(Path.Combine(project, AtlasPath)) == report.atlas.sourceSha256, "Atlas source bytes changed during capture.");
                Require(Hash(Path.Combine(project, FramePath)) == report.frameTexture.sourceSha256, "Frame source bytes changed during capture.");
                if (report.capturedFrames != 24) report.errors.Add("Expected 24 native-resolution screenshots.");
                if (report.passedPagePairs != 48) report.errors.Add("Expected 48 successful four-page A/B checks.");
                report.status = report.errors.Count == 0 && report.passedFrames == 24 && report.passedPagePairs == 48 ? "passed" : "failed";
            }
            catch (Exception error) { report.errors.Add(error.ToString()); report.status = "failed"; }
            finally { report.completedUtc = DateTime.UtcNow.ToString("O"); WriteReport(output, report); }
            Debug.Log("CREW_LAYERS_CAPTURE " + report.status + " frames=" + report.capturedFrames + "/24 pagePairs=" + report.passedPagePairs + "/48 output=" + output);
            if (report.status != "passed") throw new InvalidOperationException("Crew layered comparison failed. Inspect " + output + "/report.json");
        }

        private static void CaptureCrew(Report report, Fixture fixture, int profession, string project, string output)
        {
            bool before = Mode(fixture.Controller);
            fixture.Controller.ConfigureEditorCrewDossier(profession, 0);
            fixture.BindAndRender();
            Snapshot selected = SnapshotOf(fixture);
            var selection = new SelectionCheck { width = fixture.Width, height = fixture.Height, profession = profession,
                crewId = selected.crewId, name = selected.name, modeBefore = before, modeAfter = Mode(fixture.Controller),
                preservedAcrossSelection = before == Mode(fixture.Controller) };
            report.selections.Add(selection);
            Require(selection.preservedAcrossSelection, "A/B preference reset when selecting another crew.");
            Require(selected.selection == profession && !string.IsNullOrWhiteSpace(selected.crewId) && !string.IsNullOrWhiteSpace(selected.name), "Preview selected the wrong crew.");

            for (int page = 0; page < Pages.Length; page++)
            {
                var pair = new PagePair { width = fixture.Width, height = fixture.Height, profession = profession, crewId = selected.crewId, page = Pages[page] };
                report.pagePairs.Add(pair);
                TimedSwitch(report, fixture, profession, page, "prepare-original", false);
                pair.originalTabWorked = ClickPage(fixture, page);
                pair.original = SnapshotOf(fixture);
                pair.originalLayout = MeasureTexts(fixture);
                pair.originalHasNoCandidateLayers = !ActiveAtlas(fixture) && ActiveFrameGraphics(fixture).Length == 0;
                Check(pair, pair.originalHasNoCandidateLayers, "Original mode shows a candidate portrait/frame layer.");
                if (page == 0) SaveFrame(report, fixture, profession, "original", project, output);

                TimedSwitch(report, fixture, profession, page, "original-to-atlas", true);
                fixture.Render();
                pair.atlas = SnapshotOf(fixture);
                pair.originalToAtlasPreserved = SameDocument(pair.original, pair.atlas);
                Check(pair, pair.originalToAtlasPreserved, "A->B changed dossier/name/profile/session identity, selected crew, page, gold, text or profile geometry.");
                Check(pair, pair.atlas.atlasMode, "A->B mode switch failed.");
                Check(pair, ActiveAtlas(fixture), "B does not show the expected portrait atlas.");
                Graphic[] activeFrames = ActiveFrameGraphics(fixture);
                Graphic[] activePortraits = ActivePortraitGraphics(fixture);
                pair.atlasAndFrameIndependent = activeFrames.Length == 1 && activePortraits.Length == 1 &&
                    activeFrames[0] != activePortraits[0] && activeFrames[0].mainTexture != activePortraits[0].mainTexture;
                Check(pair, pair.atlasAndFrameIndependent, "B must show exactly one independent portrait and frame graphic with separate textures.");
                // Invoke the real selected page's tab again in B; also verifies this control remains usable.
                pair.atlasTabWorked = ClickPage(fixture, page);
                pair.atlasLayout = MeasureTexts(fixture);
                Check(pair, pair.originalTabWorked && pair.atlasTabWorked, "Page tab no longer selects exactly the requested page.");
                if (page == 0) SaveFrame(report, fixture, profession, "atlas", project, output);

                TimedSwitch(report, fixture, profession, page, "atlas-to-original", false);
                fixture.Render();
                pair.restored = SnapshotOf(fixture);
                pair.atlasToOriginalPreserved = SameDocument(pair.original, pair.restored);
                Check(pair, pair.atlasToOriginalPreserved && !pair.restored.atlasMode, "B->A did not restore the same dossier and state.");
                Check(pair, !ActiveAtlas(fixture), "Original mode left the atlas visible.");
                pair.restoredHasNoCandidateLayers = !ActiveAtlas(fixture) && ActiveFrameGraphics(fixture).Length == 0;
                Check(pair, pair.restoredHasNoCandidateLayers, "Restored original mode left a candidate frame/portrait visible.");
                if (pair.errors.Count == 0) report.passedPagePairs++;
                else foreach (string error in pair.errors) report.errors.Add(fixture.Width + " " + selected.crewId + " " + pair.page + ": " + error);
            }
            // Exercise persisted B mode during the next actual editor selection/rebuild.
            TimedSwitch(report, fixture, profession, 3, "retain-atlas-for-next-selection", true);
        }

        private static void SaveFrame(Report report, Fixture fixture, int profession, string variant, string project, string output)
        {
            Snapshot state = SnapshotOf(fixture);
            var frame = new Frame { width = fixture.Width, height = fixture.Height, profession = profession,
                crewId = state.crewId, crewName = state.name, variant = variant, state = state,
                file = "crew-" + profession + "-" + variant + "-" + fixture.Width + "x" + fixture.Height + ".png",
                canvasScale = fixture.Canvas.scaleFactor, canvasPixelRect = fixture.Canvas.pixelRect,
                scalerVerified = fixture.ScalerVerified(), texts = MeasureTexts(fixture) };
            report.frames.Add(frame);
            if (!frame.scalerVerified) frame.errors.Add("Production CanvasScaler did not match the native render target.");
            bool expectAtlas = variant == "atlas";
            if (ActiveAtlas(fixture) != expectAtlas) frame.errors.Add("The requested original/atlas mode is not displayed.");
            foreach (Graphic graphic in fixture.Detail.GetComponentsInChildren<Graphic>(false))
            {
                if (graphic is Text || !graphic.enabled || graphic.color.a <= .001f || graphic.mainTexture == null || graphic.mainTexture.width <= 4 || graphic.canvasRenderer.GetInheritedAlpha() <= .001f) continue;
                Mesh mesh = graphic.canvasRenderer.GetMesh();
                if (mesh == null || mesh.vertexCount < 3) continue;
                Rect screen = MeshBounds(graphic, fixture.Camera);
                if (!screen.Overlaps(new Rect(0, 0, fixture.Width, fixture.Height))) continue;
                Rect uv = Bounds(mesh.uv);
                Texture texture = graphic.mainTexture;
                RawImage raw = graphic as RawImage;
                Material material = graphic.materialForRendering;
                var info = new GraphicInfo { path = PathOf(graphic.transform, fixture.Detail), type = graphic.GetType().Name,
                    graphicId = graphic.GetInstanceID(), textureId = texture.GetInstanceID(), materialId = material != null ? material.GetInstanceID() : 0,
                    siblingIndex = graphic.transform.GetSiblingIndex(), shaderSupported = material != null && material.shader != null && material.shader.isSupported,
                    texture = MeasureTexture(texture, project), material = material != null ? material.name : "", shader = material != null && material.shader != null ? material.shader.name : "",
                    rawImage = raw != null, rawSourceUv = raw != null ? raw.uvRect : uv, meshUv = uv,
                    sourcePixels = new Rect(uv.x * texture.width, uv.y * texture.height, uv.width * texture.width, uv.height * texture.height), screenRect = screen,
                    culled = graphic.canvasRenderer.cull, raycastTarget = graphic.raycastTarget,
                    displayPixelsPerSourcePixel = Mathf.Max(screen.width / Mathf.Max(1f, Mathf.Abs(uv.width) * texture.width), screen.height / Mathf.Max(1f, Mathf.Abs(uv.height) * texture.height)) };
                if (material != null)
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        Texture bound = material.GetTexture(property);
                        if (bound != null) info.materialTextures.Add(new MaterialTextureInfo { property = property, name = bound.name,
                            asset = AssetDatabase.GetAssetPath(bound), instanceId = bound.GetInstanceID(), width = bound.width, height = bound.height });
                    }
                Rect visible = Intersection(screen, new Rect(0, 0, fixture.Width, fixture.Height));
                foreach (RectMask2D mask in graphic.GetComponentsInParent<RectMask2D>())
                {
                    if (!mask.isActiveAndEnabled) continue;
                    Rect local = mask.rectTransform.rect;
                    Vector4 padding = mask.padding;
                    local.xMin += padding.x; local.yMin += padding.y; local.xMax -= padding.z; local.yMax -= padding.w;
                    Rect maskScreen = Bounds(new[] { new Vector2(local.xMin, local.yMin), new Vector2(local.xMin, local.yMax),
                        new Vector2(local.xMax, local.yMax), new Vector2(local.xMax, local.yMin) }
                        .Select(v => (Vector2)fixture.Camera.WorldToScreenPoint(mask.transform.TransformPoint(v))));
                    info.ancestorMasks.Add(new MaskInfo { path = PathOf(mask.transform, fixture.Detail), screenRect = maskScreen, padding = padding });
                    visible = Intersection(visible, maskScreen);
                }
                info.visibleScreenRect = visible;
                info.clippedByMaskOrScreen = Mathf.Abs(visible.width - screen.width) > .1f || Mathf.Abs(visible.height - screen.height) > .1f;
                if (screen.width > 0 && screen.height > 0 && visible.width > 0 && visible.height > 0)
                    info.estimatedVisibleSourcePixels = new Rect((uv.x + (visible.xMin - screen.xMin) / screen.width * uv.width) * texture.width,
                        (uv.y + (visible.yMin - screen.yMin) / screen.height * uv.height) * texture.height,
                        visible.width / screen.width * uv.width * texture.width, visible.height / screen.height * uv.height * texture.height);
                frame.graphics.Add(info);
            }
            GraphicInfo[] portraits = frame.graphics.Where(g => g.texture.asset == AtlasPath).ToArray();
            GraphicInfo[] borders = frame.graphics.Where(g => g.texture.asset == FramePath).ToArray();
            frame.layers = new LayerCheck { expectedLayered = expectAtlas, portraitCount = portraits.Length, frameCount = borders.Length,
                candidateLayersAbsent = portraits.Length == 0 && borders.Length == 0 };
            if (!expectAtlas && !frame.graphics.Any(g => g.path == "Tavern Dossier Parchment")) frame.errors.Add("Original dossier painting is not rendered.");
            if (!expectAtlas && !frame.layers.candidateLayersAbsent) frame.errors.Add("Original screenshot contains candidate artwork.");
            if (expectAtlas)
            {
                GraphicInfo portrait = portraits.Length == 1 ? portraits[0] : null;
                GraphicInfo border = borders.Length == 1 ? borders[0] : null;
                if (portrait == null || portrait.texture.asset != AtlasPath || portrait.texture.width != 4096 || portrait.texture.height != 4096 || portrait.texture.format != "BC7")
                    frame.errors.Add("Candidate portrait does not use the expected 4096x4096 BC7 atlas.");
                if (portrait != null && (portrait.visibleScreenRect.width < 1f || portrait.visibleScreenRect.height < 1f))
                    frame.errors.Add("Candidate portrait is fully outside its mask/screen.");
                if (portrait != null && !NodeMatches(portrait.path, PortraitName)) frame.errors.Add("Candidate portrait node name changed.");
                if (border == null) frame.errors.Add("Candidate must render exactly one independent frame graphic.");
                if (border != null && !string.IsNullOrEmpty(report.requestedFrameNode) && !NodeMatches(border.path, report.requestedFrameNode))
                    frame.errors.Add("Candidate frame node differs from expected node.");
                if (portrait != null && border != null)
                {
                    frame.layers.independentGraphicsAndTextures = portrait.graphicId != border.graphicId && portrait.textureId != border.textureId;
                    frame.layers.portraitPath = portrait.path; frame.layers.framePath = border.path;
                    frame.layers.portraitGraphicId = portrait.graphicId; frame.layers.frameGraphicId = border.graphicId;
                    frame.layers.portraitTextureId = portrait.textureId; frame.layers.frameTextureId = border.textureId;
                    frame.layers.portraitShader = portrait.shader; frame.layers.frameShader = border.shader;
                    frame.layers.portraitRawRect = portrait.screenRect; frame.layers.portraitVisibleRect = portrait.visibleScreenRect;
                    frame.layers.frameRawRect = border.screenRect; frame.layers.frameVisibleRect = border.visibleScreenRect;
                    if (!frame.layers.independentGraphicsAndTextures) frame.errors.Add("Portrait and frame are not independent graphics/textures.");
                    if (!portrait.shaderSupported || !border.shaderSupported) frame.errors.Add("Candidate portrait/frame shader is unsupported.");
                    if (portrait.shader != ExpectedLayerShader || border.shader != ExpectedLayerShader) frame.errors.Add("Both candidate layers must use Crew Portrait Chroma Key, not the previous Paper Blend/default shader.");
                    if (portrait.texture.mipmapCount != 1 || border.texture.mipmapCount != 1 || border.texture.format != "BC7") frame.errors.Add("Candidate texture format/mipmap policy is wrong.");
                    if (border.visibleScreenRect.width < 1f || border.visibleScreenRect.height < 1f) frame.errors.Add("Candidate frame is fully outside its mask/screen.");
                    if (portrait.raycastTarget || border.raycastTarget) frame.errors.Add("Candidate artwork intercepts pointer input.");
                }
            }
            Texture2D pixels = null;
            RenderTexture old = RenderTexture.active;
            try
            {
                RenderTexture.active = fixture.Target;
                pixels = new Texture2D(fixture.Width, fixture.Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, fixture.Width, fixture.Height), 0, 0, false); pixels.Apply(false, false);
                Color32[] values = pixels.GetPixels32(); int min = 255, max = 0;
                for (int i = 0; i < values.Length; i += 257) { int value = (values[i].r + values[i].g + values[i].b) / 3; min = Math.Min(min, value); max = Math.Max(max, value); }
                frame.imageHasContent = max - min > 20;
                if (!frame.imageHasContent) frame.errors.Add("Screenshot is nearly uniform.");
                byte[] png = pixels.EncodeToPNG(); File.WriteAllBytes(Path.Combine(output, frame.file), png);
                frame.screenshotSha256 = Hash(png); frame.captured = true; report.capturedFrames++;
            }
            finally { RenderTexture.active = old; if (pixels != null) Object.DestroyImmediate(pixels); }
            frame.status = frame.errors.Count == 0 ? "passed" : "failed";
            if (frame.status == "passed") report.passedFrames++;
            else foreach (string error in frame.errors) report.errors.Add(frame.file + ": " + error);
            WriteReport(output, report);
        }

        private static Snapshot SnapshotOf(Fixture fixture)
        {
            Transform detail = fixture.Detail;
            RectTransform profile = Find<RectTransform>(detail, "Tavern Crew Detail Profile");
            Text name = Find<Text>(detail, "Tavern Crew Detail Name");
            string text = string.Join("\n", profile.GetComponentsInChildren<Text>(true).Select(t => PathOf(t.transform, profile) + "=" + t.text).OrderBy(s => s, StringComparer.Ordinal));
            string geometry = string.Join("\n", profile.GetComponentsInChildren<RectTransform>(true).Select(r =>
                PathOf(r, profile) + "=" + JsonUtility.ToJson(new RectState { anchorMin = r.anchorMin, anchorMax = r.anchorMax, pivot = r.pivot,
                    size = r.sizeDelta, position = r.anchoredPosition, scale = r.localScale })).OrderBy(s => s, StringComparer.Ordinal));
            return new Snapshot { detailId = detail.GetInstanceID(), profileId = profile.GetInstanceID(), nameId = name.GetInstanceID(),
                sessionId = RuntimeHelpers.GetHashCode(fixture.Controller.Session), gold = fixture.Controller.Session.ProfileGold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                selection = Field<int>(fixture.Controller, "selectedTavernCandidate"), crewId = Field<string>(fixture.Controller, "tavernDossierCrewId"),
                page = Field<int>(fixture.Controller, "tavernDossierPage"), name = name.text, counter = Find<Text>(detail, "Tavern Dossier Page Counter").text,
                documentTextSha256 = Hash(Encoding.UTF8.GetBytes(text)), profileGeometrySha256 = Hash(Encoding.UTF8.GetBytes(geometry)), atlasMode = Mode(fixture.Controller) };
        }
        [Serializable] private sealed class RectState { public Vector2 anchorMin, anchorMax, pivot, size, position; public Vector3 scale; }
        private static bool SameDocument(Snapshot a, Snapshot b) => a.detailId == b.detailId && a.profileId == b.profileId && a.nameId == b.nameId &&
            a.sessionId == b.sessionId && a.selection == b.selection && a.crewId == b.crewId && a.page == b.page && a.gold == b.gold && a.name == b.name &&
            a.counter == b.counter && a.documentTextSha256 == b.documentTextSha256 && a.profileGeometrySha256 == b.profileGeometrySha256;

        private static bool ClickPage(Fixture fixture, int page)
        {
            Button button = Find<Button>(fixture.Detail, "DossierPage" + Pages[page]);
            bool available = button.isActiveAndEnabled && button.IsInteractable();
            if (available) button.onClick.Invoke();
            fixture.Render();
            return available && Field<int>(fixture.Controller, "tavernDossierPage") == page && Pages.Select((p, i) =>
                Find<RectTransform>(fixture.Detail, "Tavern Dossier " + p + " Page").gameObject.activeInHierarchy == (i == page)).All(ok => ok);
        }
        private static List<TextInfo> MeasureTexts(Fixture fixture)
        {
            var result = new List<TextInfo>();
            foreach (Text text in fixture.Detail.GetComponentsInChildren<Text>(false))
            {
                if (!text.enabled || string.IsNullOrWhiteSpace(text.text)) continue;
                Mesh mesh = text.canvasRenderer.GetMesh();
                Vector3[] corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                result.Add(new TextInfo { path = PathOf(text.transform, fixture.Detail), content = text.text,
                    fontSize = text.fontSize, fontStyle = text.fontStyle.ToString(), pixelsPerUnit = text.pixelsPerUnit,
                    rectPixels = Bounds(corners.Select(v => (Vector2)fixture.Camera.WorldToScreenPoint(v))),
                    meshPixels = mesh != null && mesh.vertexCount > 0 ? MeshBounds(text, fixture.Camera) : default,
                    vertexCount = mesh != null ? mesh.vertexCount : 0, active = text.isActiveAndEnabled, culled = text.canvasRenderer.cull,
                    preferredHeight = text.preferredHeight, rectHeight = text.rectTransform.rect.height,
                    preferredHeightExceedsRect = text.preferredHeight > text.rectTransform.rect.height + .5f,
                    inScrollViewport = text.GetComponentInParent<ScrollRect>() != null });
            }
            return result;
        }
        private static TextureInfo MeasureTexture(Texture texture, string project)
        {
            string asset = AssetDatabase.GetAssetPath(texture);
            Texture2D t = texture as Texture2D;
            var result = new TextureInfo { asset = asset, name = texture.name, width = texture.width, height = texture.height,
                format = t != null ? t.format.ToString() : "", graphicsFormat = texture.graphicsFormat.ToString(), filter = texture.filterMode.ToString(),
                runtimeBytes = Profiler.GetRuntimeMemorySizeLong(texture), readable = t != null && t.isReadable, mipmapCount = t != null ? t.mipmapCount : 0 };
            var importer = string.IsNullOrEmpty(asset) ? null : AssetImporter.GetAtPath(asset) as TextureImporter;
            if (importer != null)
            {
                importer.GetSourceTextureWidthAndHeight(out result.sourceWidth, out result.sourceHeight);
                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                result.effectiveMaxSize = standalone.overridden ? standalone.maxTextureSize : importer.GetDefaultPlatformTextureSettings().maxTextureSize;
                result.sourceSha256 = Hash(Path.Combine(project, asset));
            }
            return result;
        }

        private sealed class Fixture : IDisposable
        {
            private Scene scene;
            private GameObject host;
            public readonly int Width, Height;
            public CoreGameplayDemoController Controller;
            public Canvas Canvas;
            public Camera Camera;
            public RenderTexture Target;
            public Transform Detail => Find<RectTransform>(Canvas.transform, "Tavern Crew Detail");
            public Fixture(int width, int height)
            {
                Width = width; Height = height;
                try
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    host = new GameObject("Crew Layers Isolated Preview") { hideFlags = HideFlags.HideAndDontSave };
                    host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                    Controller = host.AddComponent<CoreGameplayDemoController>();
                    Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.BrokenAnchor, 0);
                    host.SetActive(true);
                    var cameraObject = new GameObject("Crew Layers Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(cameraObject, scene); Camera = cameraObject.GetComponent<Camera>();
                    Camera.enabled = false; Camera.scene = scene; Camera.orthographic = true; Camera.orthographicSize = 5f;
                    Camera.transform.position = new Vector3(0, 0, -1000); Camera.nearClipPlane = .1f; Camera.farClipPlane = 2000;
                    Camera.cullingMask = 1 << Layer; Camera.allowHDR = false; Camera.allowMSAA = false;
                    Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
                    Target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Require(Target.Create(), "Cannot create native render target."); Camera.targetTexture = Target; Camera.aspect = width / (float)height;
                    BindAndRender();
                }
                catch { Dispose(); throw; }
            }
            public void BindAndRender()
            {
                Canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                foreach (Transform item in Canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                Canvas.renderMode = RenderMode.ScreenSpaceCamera; Canvas.worldCamera = Camera; Canvas.planeDistance = 100;
                Render();
            }
            public bool ScalerVerified() => Mathf.Abs(Canvas.scaleFactor - Width / 1920f) < .001f &&
                Mathf.Abs(Canvas.pixelRect.width - Width) < .5f && Mathf.Abs(Canvas.pixelRect.height - Height) < .5f &&
                Mathf.Abs(Canvas.renderingDisplaySize.x - Width) < .5f && Mathf.Abs(Canvas.renderingDisplaySize.y - Height) < .5f;
            public void Render()
            {
                // Candidate portrait is created lazily by the actual A/B API after initial canvas binding.
                foreach (Transform item in Canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                CanvasScaler scaler = Canvas.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.enabled, "Production CanvasScaler must stay enabled.");
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                Require(handle != null, "CanvasScaler.Handle missing.");
                for (int pass = 0; pass < 4; pass++)
                {
                    UnityEngine.Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in Canvas.GetComponentsInChildren<Text>(false)) text.SetAllDirty();
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(Camera, new RenderPipeline.StandardRequest { destination = Target });
                    else Camera.Render();
                }
                Require(ScalerVerified(), "Native Canvas scale/resolution mismatch.");
            }
            public void Dispose()
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (Target != null) { Target.Release(); Object.DestroyImmediate(Target); Target = null; }
            }
        }

        private static Graphic[] ActivePortraitGraphics(Fixture fixture) => ActiveAssetGraphics(fixture, AtlasPath);
        private static Graphic[] ActiveFrameGraphics(Fixture fixture) => ActiveAssetGraphics(fixture, FramePath);
        private static Graphic[] ActiveAssetGraphics(Fixture fixture, string asset) => fixture.Detail.GetComponentsInChildren<Graphic>(false)
            .Where(g => g.enabled && g.color.a > .001f && g.canvasRenderer.GetInheritedAlpha() > .001f && g.mainTexture != null && AssetDatabase.GetAssetPath(g.mainTexture) == asset).ToArray();
        private static bool ActiveAtlas(Fixture fixture) => ActivePortraitGraphics(fixture).Any(g => g.name == PortraitName);
        private static bool NodeMatches(string path, string name) => path == name || path.EndsWith("/" + name, StringComparison.Ordinal);
        private static int ReadOptionalPositiveInt(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) return 0;
            Require(int.TryParse(value, out int number) && number > 0, name + " must be a positive integer when set.");
            return number;
        }
        private static void ValidateNativeTexture(TextureInfo texture, string label, int minimumEdge, int maximumEdge)
        {
            Require(texture.format == "BC7" && texture.mipmapCount == 1 && !texture.readable, label + " must be BC7, without mipmaps or CPU-readable pixels.");
            Require(texture.width == texture.sourceWidth && texture.height == texture.sourceHeight, label + " source was downscaled by import.");
            int longest = Mathf.Max(texture.width, texture.height);
            Require(longest >= minimumEdge && longest <= maximumEdge, label + " native longest edge must be in " + minimumEdge + ".." + maximumEdge + ".");
            Require(texture.effectiveMaxSize >= longest, label + " import maximum is smaller than its native source.");
        }
        private static bool Mode(CoreGameplayDemoController controller) => (bool)ReadMode.GetValue(controller);
        private static void Switch(CoreGameplayDemoController controller, bool atlas) => SetMode.Invoke(controller, new object[] { atlas });
        private static void TimedSwitch(Report report, Fixture fixture, int profession, int page, string phase, bool atlas)
        {
            var timing = new SwitchTiming { width = fixture.Width, height = fixture.Height, profession = profession, page = page, phase = phase,
                fromAtlas = Mode(fixture.Controller), toAtlas = atlas,
                portraitExistedBefore = fixture.Detail.GetComponentsInChildren<RawImage>(true).Any(r => r.name == PortraitName) };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Switch(fixture.Controller, atlas);
            watch.Stop(); timing.setterMilliseconds = watch.Elapsed.TotalMilliseconds;
            report.timings.Add(timing);
        }
        private static T Field<T>(object target, string name) { FieldInfo field = target.GetType().GetField(name, PrivateInstance); Require(field != null, "Missing state field " + name); return (T)field.GetValue(target); }
        private static T Find<T>(Transform root, string name) where T : Component => root.GetComponentsInChildren<T>(true).Single(c => c.name == name);
        private static void Check(PagePair pair, bool okay, string message) { if (!okay) pair.errors.Add(message); }
        private static void Require(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); }
        private static string PathOf(Transform item, Transform root) { string path = item.name; while (item.parent != null && item.parent != root) { item = item.parent; path = item.name + "/" + path; } return path; }
        private static Rect MeshBounds(Graphic graphic, Camera camera) => Bounds(graphic.canvasRenderer.GetMesh().vertices.Select(v => (Vector2)camera.WorldToScreenPoint(graphic.transform.TransformPoint(v))));
        private static Rect Bounds(IEnumerable<Vector2> points) { Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity); foreach (Vector2 point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); } return new Rect(min, max - min); }
        private static Rect Intersection(Rect a, Rect b) { float x = Mathf.Max(a.xMin, b.xMin), y = Mathf.Max(a.yMin, b.yMin); return new Rect(x, y, Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - x), Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - y)); }
        private static string Hash(string file) { using (var stream = File.OpenRead(file)) using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        private static string Hash(byte[] bytes) { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void WriteReport(string output, Report report) => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        private static void EnsurePipeline()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null || RenderPipelineManager.currentPipeline != null) return;
            MethodInfo method = typeof(RenderPipelineManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).FirstOrDefault(m => m.Name == "TryPrepareRenderPipeline" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(RenderPipelineAsset));
            Require(method != null, "RenderPipelineManager.TryPrepareRenderPipeline unavailable."); method.Invoke(null, new object[] { asset });
            Require(RenderPipelineManager.currentPipeline != null, "Render pipeline did not initialize.");
        }
    }
}
