using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Content;
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
    /// <summary>
    /// Captures the four dossier pages in temporary, non-Play preview scenes.
    /// Only the four page-tab callbacks are invoked. No recruitment, scene-save,
    /// asset-save or PlayerPrefs mutation is performed by this acceptance tool.
    /// </summary>
    public static class CrewDossierAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int CaptureLayer = 31;
        private static readonly string[] PageNames = { "Background", "Story", "Skills", "SpecialCards" };
        private static readonly string[] PageLabels = { "人物背景", "航海异闻", "职业技能", "专属卡牌" };
        private static readonly string[][] PageTextNames =
        {
            new[] { "Tavern Crew Biography" },
            new[] { "Tavern Crew Story Heading", "Tavern Crew Story" },
            new[] { "Tavern Crew Skill Heading", "Tavern Crew Skill" },
            new[] { "Tavern Special Card Name", "Tavern Special Card Growth" },
        };
        private static readonly string[] BodyNames =
        {
            "Tavern Crew Biography", "Tavern Crew Story",
            "Tavern Crew Skill",
            "Tavern Special Card Effect", "Tavern Special Card Lore", "Tavern Special Card Empty",
        };

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string scope = "All 36 crew background pages, plus story, skill and special-card pages for six ports using professionIndex = portIndex % 6 and Gray Gull profession 0. All 36 crew x four pages are checked. Temporary non-Play previews; page-tab callbacks only.";
            public int expectedCaptureCount = 57;
            public int successfulCaptureCount;
            public int expectedLayoutCheckCount = 144;
            public int successfulLayoutCheckCount;
            public int sourceSceneFilesChecked;
            public bool sourceScenesUnchanged;
            public int playerPrefsKeysChecked;
            public bool checkedPlayerPrefsUnchanged;
            public int portraitMasksRecorded;
            public bool originalPortraitSourcesUnchanged;
            public string maskReviewNote = "Coverage statistics and nominal face/corner probes are observations for manual review, not silhouette acceptance thresholds. The mask stores coverage in red; shader bottom feather and original source alpha are not included.";
            public List<PortraitSourceCheck> portraitSources = new List<PortraitSourceCheck>();
            public List<Record> captures = new List<Record>();
            public List<Record> layoutChecks = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public string port;
            public string previewPort;
            public int professionIndex;
            public string crewId;
            public string crewName;
            public string page;
            public string pageLabel;
            public string file;
            public bool referenceCharacter;
            public int fontSize;
            public string portraitTexture;
            public string portraitAssetPath;
            public Rect portraitUv;
            public bool portraitIsOriginalV2Source;
            public string portraitSourceSha256;
            public bool portraitSourceUnchanged;
            public MaskCheck portraitMask;
            public bool fullTextPreserved;
            public bool onlySelectedPageActive;
            public bool captured;
            public bool layoutPassed;
            public List<TextCheck> textChecks = new List<TextCheck>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class PortraitSourceCheck
        {
            public string assetPath;
            public string sha256Before;
            public string sha256After;
            public bool unchanged;
            public string error;
        }

        [Serializable]
        private sealed class MaskCheck
        {
            public string shader;
            public string material;
            public string textureProperty = "_PaperMask";
            public string texture;
            public string coverageChannel = "red";
            public string maskUvSpace = "Single portrait cell, 0..1";
            public Vector4 atlasToMaskUv;
            public bool readable;
            public bool recorded;
            public int width;
            public int height;
            public int pixelCount;
            public float opaqueFraction;
            public float transparentFraction;
            public float intermediateFraction;
            public float meanCoverage;
            public float minimumCoverage;
            public float maximumCoverage;
            public bool opaqueAboveThirtyPercent;
            public bool transparentAboveTenPercent;
            public bool topCornersTransparent;
            public bool nominalFaceCenterOpaque;
            public string thresholds = "Opaque >= 0.95; transparent <= 0.05. Ratio and probe booleans do not affect layoutPassed.";
            public List<MaskProbe> probes = new List<MaskProbe>();
            public string error;
        }

        [Serializable]
        private sealed class MaskProbe
        {
            public string name;
            public Vector2 uv;
            public int sampleCount;
            public float meanCoverage;
            public float minimumCoverage;
            public float maximumCoverage;
        }

        [Serializable]
        private sealed class TextCheck
        {
            public string name;
            public int characterCount;
            public bool body;
            public int fontSize;
            public int configuredFontSize;
            public bool bestFit;
            public float preferredHeight;
            public float rectHeight;
            public bool fits;
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.CrewDossierAcceptance.CaptureFromCommandLine
        /// -crewDossierCapturePath D:/Game/UnityClient/Artifacts/CrewDossier/review-01
        /// The output directory must be new or empty. Do not pass -nographics.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("CREW_DOSSIER_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static void CaptureAllFromCommandLine()
        {
            CaptureFromCommandLine();
            UIPreviewSceneAcceptance.CaptureFromCommandLine();
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Crew dossier acceptance requires stopped, non-Play edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Crew dossier captures require graphics. Remove -nographics.");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty. Use a new -crewDossierCapturePath: " + directory);

            Dictionary<string, string> sourceScenes = SnapshotSourceScenes();
            Dictionary<string, string> preferences = SnapshotRelevantPlayerPrefs();
            Directory.CreateDirectory(directory);
            Review report = new Review
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                sourceSceneFilesChecked = sourceScenes.Count,
                playerPrefsKeysChecked = preferences.Count,
                portraitSources = SnapshotOriginalPortraitSources(),
            };
            try
            {
                CoreGameplayPreviewPort[] ports = (CoreGameplayPreviewPort[])Enum.GetValues(typeof(CoreGameplayPreviewPort));
                for (int portIndex = 0; portIndex < ports.Length; portIndex++)
                    CapturePort(directory, ports[portIndex], portIndex, report);
                report.captures.Sort((left, right) => string.CompareOrdinal(left.file, right.file));
            }
            finally
            {
                try
                {
                    report.sourceScenesUnchanged = SnapshotsEqual(sourceScenes, SnapshotSourceScenes());
                    report.checkedPlayerPrefsUnchanged = SnapshotsEqual(preferences, SnapshotRelevantPlayerPrefs());
                    CompletePortraitSourceChecks(report);
                    if (!report.sourceScenesUnchanged) report.errors.Add("Source scene files changed during acceptance.");
                    if (!report.checkedPlayerPrefsUnchanged) report.errors.Add("A checked PlayerPrefs key changed during acceptance.");
                }
                catch (Exception exception) { report.errors.Add("Final read-only state check failed: " + exception.Message); }
                WriteNewFile(Path.Combine(directory, "report.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulCaptureCount != report.expectedCaptureCount ||
                report.successfulLayoutCheckCount != report.expectedLayoutCheckCount)
                throw new InvalidOperationException("Crew dossier acceptance is incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static void CapturePort(string directory, CoreGameplayPreviewPort port, int portIndex, Review report)
        {
            List<string> unityErrors = new List<string>();
            Application.LogCallback collectErrors = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    unityErrors.Add(condition + (string.IsNullOrEmpty(stackTrace) ? string.Empty : "\n" + stackTrace));
            };
            Application.logMessageReceived += collectErrors;
            try
            {
                using (Preview preview = new Preview(port))
                {
                    for (int profession = 0; profession < CrewNarrativeCatalog.ProfessionCount; profession++)
                    {
                        CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession(preview.Controller.Session.CurrentPort, profession);
                        bool reference = port == CoreGameplayPreviewPort.GrayGull && profession == 0;
                        bool captureAllPages = profession == portIndex % CrewNarrativeCatalog.ProfessionCount || reference;
                        List<Record> records = new List<Record>();
                        for (int page = 0; page < PageNames.Length; page++)
                        {
                            bool shouldCapture = page == 0 || captureAllPages;
                            Record record = new Record
                            {
                                port = crew.PortName, previewPort = port.ToString(), professionIndex = profession,
                                crewId = crew.Id, crewName = crew.DisplayName, page = PageNames[page],
                                pageLabel = PageLabels[page], referenceCharacter = reference,
                                file = shouldCapture ? (reference ? "07_GrayGull_Reference" : (portIndex + 1).ToString("00") + "_" + port) +
                                    "_Crew" + profession + "_" + (page + 1).ToString("00") + "_" + PageNames[page] + ".png" : null,
                            };
                            records.Add(record);
                            report.layoutChecks.Add(record);
                            if (shouldCapture) report.captures.Add(record);
                        }
                        try
                        {
                            preview.SelectCrew(profession);
                            MaskCheck mask = InspectPortraitMask(Find<RawImage>(preview.Root, "Tavern Crew Detail Full Body"));
                            if (mask.recorded) report.portraitMasksRecorded++;
                            foreach (Record record in records) record.portraitMask = mask;
                            foreach (Record record in records)
                            {
                                try
                                {
                                    preview.SelectPage(record.page);
                                    preview.RenderStableFrame();
                                    InspectPage(preview, crew, record, report);
                                    if (!string.IsNullOrEmpty(record.file))
                                    {
                                        SaveFrame(preview.Target, Path.Combine(directory, record.file));
                                        record.captured = true;
                                        report.successfulCaptureCount++;
                                    }
                                }
                                catch (Exception exception) { record.errors.Add(Unwrap(exception).Message); }
                            }
                        }
                        catch (Exception exception)
                        {
                            foreach (Record record in records) record.errors.Add(Unwrap(exception).Message);
                        }
                        foreach (Record record in records)
                        {
                            record.layoutPassed = record.errors.Count == 0;
                            if (record.layoutPassed) report.successfulLayoutCheckCount++;
                            foreach (string error in record.errors)
                                report.errors.Add(record.port + " / " + record.crewId + " / " + record.page + ": " + error);
                        }
                    }
                }
            }
            catch (Exception exception) { report.errors.Add(port + ": " + Unwrap(exception).Message); }
            finally
            {
                Application.logMessageReceived -= collectErrors;
                foreach (string error in unityErrors) report.errors.Add(port + " / Unity: " + error);
            }
        }

        private static void InspectPage(Preview preview, CrewVariantDefinition crew, Record record, Review report)
        {
            Transform detail = Find<RectTransform>(preview.Root, "Tavern Crew Detail");
            RawImage portrait = Find<RawImage>(detail, "Tavern Crew Detail Full Body");
            if (portrait.texture == null) record.errors.Add("Portrait texture is missing.");
            else
            {
                record.portraitTexture = portrait.texture.name;
                record.portraitAssetPath = AssetDatabase.GetAssetPath(portrait.texture);
                record.portraitUv = portrait.uvRect;
                PortraitSourceCheck source = report.portraitSources.FirstOrDefault(item => item.assetPath == record.portraitAssetPath);
                record.portraitIsOriginalV2Source = source != null;
                record.portraitSourceSha256 = source == null ? null : source.sha256Before;
            }
            RawImage paper = Find<RawImage>(detail, "Tavern Dossier Parchment");
            if (paper.texture == null) record.errors.Add("Dossier parchment texture is missing.");
            if (Find<Text>(detail, "Tavern Crew Detail Name").text != crew.DisplayName)
                record.errors.Add("Displayed crew name does not match the selected crew.");

            record.onlySelectedPageActive = PageNames.All(page =>
                Find<RectTransform>(detail, "Tavern Dossier " + page + " Page").gameObject.activeInHierarchy == (page == record.page));
            if (!record.onlySelectedPageActive) record.errors.Add("Exactly the selected dossier page must be active.");
            for (int page = 0; page < PageNames.Length; page++)
            {
                if (Find<Button>(detail, "DossierPage" + PageNames[page]).GetComponentInChildren<Text>().text != PageLabels[page])
                    record.errors.Add(PageNames[page] + " dossier tab label changed.");
                foreach (string textName in PageTextNames[page])
                    if (Find<Text>(detail, textName).isActiveAndEnabled != (PageNames[page] == record.page))
                        record.errors.Add(textName + " visibility does not match its dossier page.");
            }
            record.fullTextPreserved = true;
            foreach (string removedName in new[] { "Tavern Crew Appearance", "Tavern Crew Profile Heading" })
                if (detail.GetComponentsInChildren<Transform>(true).Any(item => item.name == removedName))
                    record.errors.Add("Removed background content is still present: " + removedName + ".");
            CheckExactText(detail, "Tavern Crew Biography", crew.Biography, record, true);
            CheckExactText(detail, "Tavern Crew Story Heading", CrewNarrativeCatalog.StoryFor(crew).ChapterLabel, record);
            CheckExactText(detail, "Tavern Crew Story", CrewNarrativeCatalog.StoryFor(crew).Body, record);
            CheckExactText(detail, "Tavern Crew Skill", crew.SkillDescription, record);

            record.fontSize = int.MaxValue;
            foreach (Text text in detail.GetComponentsInChildren<Text>(false))
            {
                if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                int actualSize = text.resizeTextForBestFit ? text.cachedTextGenerator.fontSizeUsedForBestFit : text.fontSize;
                TextCheck check = new TextCheck
                {
                    name = text.name, characterCount = text.text.Length, body = BodyNames.Contains(text.name),
                    configuredFontSize = text.fontSize, fontSize = actualSize, bestFit = text.resizeTextForBestFit,
                    preferredHeight = text.preferredHeight, rectHeight = text.rectTransform.rect.height,
                };
                check.fits = check.preferredHeight <= check.rectHeight + 0.5f;
                record.textChecks.Add(check);
                if (!check.fits) record.errors.Add(text.name + " text height " + check.preferredHeight.ToString("0.##") +
                    " exceeds rect height " + check.rectHeight.ToString("0.##") + ".");
                if (!check.body) continue;
                record.fontSize = Math.Min(record.fontSize, check.fontSize);
                int minimumSize = text.name == "Tavern Crew Biography" ? 26 :
                    text.name.StartsWith("Tavern Special Card ", StringComparison.Ordinal) ? 20 : 22;
                if (check.fontSize < minimumSize) record.errors.Add(text.name + " active body font size is below " +
                    minimumSize + ": " + check.fontSize + ".");
            }
            if (record.fontSize == int.MaxValue)
            {
                record.fontSize = 0;
                record.errors.Add("No active dossier body text was found.");
            }
        }

        private static void CheckExactText(Transform detail, string objectName, string expected, Record record,
            bool allowParagraphBreaks = false)
        {
            string actual = Find<Text>(detail, objectName).text;
            // Paragraph layout may introduce line breaks, but all original words,
            // punctuation and spaces must remain unchanged.
            if (allowParagraphBreaks)
            {
                actual = actual.Replace("\r", string.Empty).Replace("\n", string.Empty);
                expected = expected.Replace("\r", string.Empty).Replace("\n", string.Empty);
            }
            if (actual == expected) return;
            record.fullTextPreserved = false;
            record.errors.Add(objectName + " does not contain the complete original catalogue text.");
        }

        private static MaskCheck InspectPortraitMask(RawImage portrait)
        {
            MaskCheck result = new MaskCheck();
            try
            {
                Material material = portrait.material;
                result.material = material == null ? null : material.name;
                result.shader = material == null || material.shader == null ? null : material.shader.name;
                if (material == null || !material.HasProperty(result.textureProperty))
                {
                    result.error = "The portrait material has no _PaperMask property.";
                    return result;
                }
                Texture maskTexture = material.GetTexture(result.textureProperty);
                result.texture = maskTexture == null ? null : maskTexture.name;
                Texture2D mask = maskTexture as Texture2D;
                if (mask == null)
                {
                    result.error = "The portrait material has no Texture2D paper mask.";
                    return result;
                }
                result.width = mask.width;
                result.height = mask.height;
                result.readable = mask.isReadable;
                if (material.HasProperty("_MaskUv")) result.atlasToMaskUv = material.GetVector("_MaskUv");
                if (!result.readable)
                {
                    result.error = "Paper mask is not CPU-readable; inspection did not change the importer or perform an extra GPU readback.";
                    return result;
                }
                // Attach owns a readable RGBA32 linear mask whose RGB channels
                // contain coverage. Alpha is 255, so alpha-channel statistics
                // would incorrectly report a fully opaque mask.
                Color32[] pixels = mask.GetPixels32();
                int opaque = 0, transparent = 0, minimum = 255, maximum = 0;
                long sum = 0;
                foreach (Color32 pixel in pixels)
                {
                    int coverage = pixel.r;
                    if (coverage >= 243) opaque++;
                    if (coverage <= 12) transparent++;
                    minimum = Math.Min(minimum, coverage);
                    maximum = Math.Max(maximum, coverage);
                    sum += coverage;
                }
                result.pixelCount = pixels.Length;
                result.opaqueFraction = opaque / (float)pixels.Length;
                result.transparentFraction = transparent / (float)pixels.Length;
                result.intermediateFraction = 1f - result.opaqueFraction - result.transparentFraction;
                result.meanCoverage = (float)(sum / (255.0 * pixels.Length));
                result.minimumCoverage = minimum / 255f;
                result.maximumCoverage = maximum / 255f;
                result.opaqueAboveThirtyPercent = result.opaqueFraction > 0.30f;
                result.transparentAboveTenPercent = result.transparentFraction > 0.10f;
                result.probes.Add(ProbeMask(pixels, mask.width, mask.height, "top-left paper corner", new Vector2(0.025f, 0.975f)));
                result.probes.Add(ProbeMask(pixels, mask.width, mask.height, "top-right paper corner", new Vector2(0.975f, 0.975f)));
                result.probes.Add(ProbeMask(pixels, mask.width, mask.height, "nominal face center", new Vector2(0.50f, 0.65f)));
                result.probes.Add(ProbeMask(pixels, mask.width, mask.height, "nominal upper head or headwear", new Vector2(0.50f, 0.90f)));
                result.probes.Add(ProbeMask(pixels, mask.width, mask.height, "nominal torso center", new Vector2(0.50f, 0.20f)));
                result.topCornersTransparent = result.probes[0].meanCoverage <= 0.05f && result.probes[1].meanCoverage <= 0.05f;
                result.nominalFaceCenterOpaque = result.probes[2].meanCoverage >= 0.95f;
                result.recorded = true;
            }
            catch (Exception exception) { result.error = "Mask observation failed: " + Unwrap(exception).Message; }
            return result;
        }

        private static MaskProbe ProbeMask(Color32[] pixels, int width, int height, string name, Vector2 uv)
        {
            MaskProbe result = new MaskProbe { name = name, uv = uv };
            int centerX = Mathf.RoundToInt(uv.x * (width - 1));
            int centerY = Mathf.RoundToInt(uv.y * (height - 1));
            int radius = Math.Max(1, Mathf.RoundToInt(Math.Min(width, height) * 0.008f));
            int minimum = 255, maximum = 0;
            long sum = 0;
            for (int y = Math.Max(0, centerY - radius); y <= Math.Min(height - 1, centerY + radius); y++)
                for (int x = Math.Max(0, centerX - radius); x <= Math.Min(width - 1, centerX + radius); x++)
                {
                    int coverage = pixels[y * width + x].r;
                    minimum = Math.Min(minimum, coverage);
                    maximum = Math.Max(maximum, coverage);
                    sum += coverage;
                    result.sampleCount++;
                }
            result.minimumCoverage = minimum / 255f;
            result.maximumCoverage = maximum / 255f;
            result.meanCoverage = (float)(sum / (255.0 * result.sampleCount));
            return result;
        }

        private sealed class Preview : IDisposable
        {
            private Scene scene;
            private GameObject host;
            private GameObject cameraHost;
            private Camera camera;
            private Canvas canvas;
            private readonly RenderTexture previousActive;
            public CoreGameplayDemoController Controller { get; private set; }
            public Transform Root => canvas.transform;
            public RenderTexture Target { get; private set; }

            public Preview(CoreGameplayPreviewPort port)
            {
                previousActive = RenderTexture.active;
                try
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    host = new GameObject("Crew Dossier Acceptance Host") { hideFlags = HideFlags.HideAndDontSave };
                    host.SetActive(false);
                    SceneManager.MoveGameObjectToScene(host, scene);
                    Controller = host.AddComponent<CoreGameplayDemoController>();
                    Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, port, 0);
                    host.SetActive(true);
                    if (Controller.Session == null) throw new InvalidOperationException("The isolated crew preview did not initialize.");
                    Transform root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                    canvas = root == null ? null : root.GetComponent<Canvas>();
                    if (canvas == null) throw new InvalidOperationException("The preview Canvas was not created.");
                    cameraHost = new GameObject("Crew Dossier Acceptance Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
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
                    Target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                        { name = "Crew Dossier Acceptance Target" };
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

            public void SelectCrew(int profession)
            {
                SetField(Controller, "selectedTavernCandidate", profession);
                Invoke(Controller, "Render");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CaptureLayer;
                if (!Find<RectTransform>(Root, "Tavern Dossier Background Page").gameObject.activeInHierarchy)
                    throw new InvalidOperationException("A newly opened crew dossier must initially show its background page.");
            }

            public void SelectPage(string page)
            {
                Transform detail = Find<RectTransform>(Root, "Tavern Crew Detail");
                decimal originalGold = Controller.Session.ProfileGold;
                string originalPort = Controller.Session.CurrentPort;
                // This allow-list contains only local page visibility controls.
                // Never invoke any recruit, back, facility or gameplay button.
                if (!PageNames.Contains(page)) throw new ArgumentException("Unknown dossier page: " + page);
                Find<Button>(detail, "DossierPage" + page).onClick.Invoke();
                if (detail == null || Find<RectTransform>(Root, "Tavern Crew Detail") != detail)
                    throw new InvalidOperationException("A dossier page tab rebuilt the screen instead of switching page visibility.");
                if (Controller.Session.ProfileGold != originalGold || Controller.Session.CurrentPort != originalPort)
                    throw new InvalidOperationException("A dossier page tab changed the preview session.");
            }

            public void RenderStableFrame()
            {
                // Dynamic-font atlas repacks can invalidate earlier text meshes.
                // Rebuild and render at least three passes after each page switch.
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

        private static List<PortraitSourceCheck> SnapshotOriginalPortraitSources()
        {
            List<PortraitSourceCheck> result = new List<PortraitSourceCheck>();
            string relativeDirectory = "Assets/_Project/Resources/PortArt/CrewRegional";
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(project, relativeDirectory);
            if (!Directory.Exists(directory)) return result;
            foreach (string path in Directory.EnumerateFiles(directory, "crew_*_portraits_v2.png").OrderBy(path => path))
            {
                PortraitSourceCheck source = new PortraitSourceCheck
                {
                    assetPath = relativeDirectory + "/" + Path.GetFileName(path),
                };
                try { source.sha256Before = HashPortraitSource(path); }
                catch (Exception exception) { source.error = "Initial source hash observation failed: " + exception.Message; }
                result.Add(source);
            }
            return result;
        }

        private static void CompletePortraitSourceChecks(Review report)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            foreach (PortraitSourceCheck source in report.portraitSources)
            {
                try
                {
                    source.sha256After = HashPortraitSource(Path.Combine(project, source.assetPath));
                    source.unchanged = !string.IsNullOrEmpty(source.sha256Before) && source.sha256Before == source.sha256After;
                }
                catch (Exception exception) { source.error = "Final source hash observation failed: " + exception.Message; }
            }
            report.originalPortraitSourcesUnchanged = report.portraitSources.Count == 6 && report.portraitSources.All(source => source.unchanged);
            foreach (Record record in report.layoutChecks)
            {
                PortraitSourceCheck source = report.portraitSources.FirstOrDefault(item => item.assetPath == record.portraitAssetPath);
                record.portraitSourceUnchanged = source != null && source.unchanged;
            }
        }

        private static string HashPortraitSource(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static Dictionary<string, string> SnapshotSourceScenes()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            string scenes = Path.Combine(Application.dataPath, "_Project", "Scenes");
            if (!Directory.Exists(scenes)) return result;
            foreach (string path in Directory.EnumerateFiles(scenes, "*.unity", SearchOption.AllDirectories))
                using (SHA256 hash = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    result.Add(path, Convert.ToBase64String(hash.ComputeHash(stream)));
            return result;
        }

        private static Dictionary<string, string> SnapshotRelevantPlayerPrefs()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] integerKeys =
            {
                CoreGameplayDemoController.ProfileGoldKey, CoreGameplayDemoController.SelectedShipKey,
                CoreGameplayDemoController.TutorialCompletedKey, CoreGameplayDemoController.TutorialRewardGrantedKey,
            };
            foreach (string key in integerKeys) ReadIntPreference(result, key);
            ReadStringPreference(result, ProfileGoldPersistence.DecimalKey);
            ReadStringPreference(result, CoreGameplayDemoController.CurrentPortKey);
            ReadStringPreference(result, CoreGameplayDemoController.BattleDeckKey);
            foreach (string resource in new[] { "Water", "Food", "Beer", "Medicine", "Wood", "Ammo", "Gold", "Relics" })
                ReadIntPreference(result, CoreGameplayDemoController.CargoKey(resource));
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                ReadIntPreference(result, CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id));
            for (int profession = 0; profession < CrewNarrativeCatalog.ProfessionCount; profession++)
            {
                ReadIntPreference(result, CoreGameplayDemoController.CrewRecruitmentKey(profession));
                ReadStringPreference(result, CoreGameplayDemoController.ActiveCrewVariantKey(profession));
            }
            return result;
        }

        private static void ReadIntPreference(Dictionary<string, string> result, string key)
        {
            result[key] = PlayerPrefs.HasKey(key) ? "present:" + PlayerPrefs.GetInt(key).ToString() : "absent";
        }

        private static void ReadStringPreference(Dictionary<string, string> result, string key)
        {
            result[key] = PlayerPrefs.HasKey(key) ? "present:" + PlayerPrefs.GetString(key) : "absent";
        }

        private static bool SnapshotsEqual(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            return before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out string value) && value == pair.Value);
        }

        private static void SaveFrame(RenderTexture target, string path)
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
                    dark = Math.Min(dark, level);
                    bright = Math.Max(bright, level);
                }
                if (bright - dark < 20) throw new InvalidOperationException("Captured frame is nearly uniform; the UI may not have rendered.");
                WriteNewFile(path, image.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>船员档案分页验收</title>");
            html.Append("<style>body{margin:32px;background:#171c1b;color:#f0dfbb;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,540px),1fr));gap:24px}figure{margin:0;padding:12px;border:1px solid #756247;background:#29302a}img{display:block;width:100%;height:auto}figcaption,p{line-height:1.7}small{display:block;color:#c4bba9;overflow-wrap:anywhere}a{color:#ebbe6c}.error{color:#ffb99d}</style></head><body>");
            html.Append("<h1>船员档案分页验收</h1><p>").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append(" 张 · 1920 × 1080 · 六港四页及灰鸥港首位角色参考<br>文字检查 ").Append(report.successfulLayoutCheckCount).Append(" / ")
                .Append(report.expectedLayoutCheckCount).Append(" 页 · 人物背景正文至少 26 px，故事与技能正文至少 22 px，专属卡牌正文至少 20 px · 人物背景仅调整分段，故事与技能原文完整保留。<br>点击截图查看原尺寸；字体与溢出检查通过后，仍需逐图检查纸面位置、头像透明边缘与视觉可读性。</p><p><a href=\"report.json\">完整结构化报告</a><br>生成时间（UTC）：")
                .Append(Escape(report.capturedAtUtc)).Append("<br>源场景文件未改变：").Append(report.sourceScenesUnchanged ? "是" : "否")
                .Append(" · 已检查存档键未改变：").Append(report.checkedPlayerPrefsUnchanged ? "是" : "否")
                .Append("<br>原始 v2 头像源文件哈希未改变：").Append(report.originalPortraitSourcesUnchanged ? "是" : "未确认")
                .Append(" · 已记录蒙版 ").Append(report.portraitMasksRecorded).Append(" / 36 份。蒙版比例、角落和名义面部位置采样仅供人工复核，不计作轮廓验收结论。</p><main>");
            foreach (Record record in report.captures)
            {
                html.Append("<figure>");
                if (record.captured)
                    html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><img loading=\"lazy\" width=\"1920\" height=\"1080\" alt=\"")
                        .Append(Escape(record.port + " · " + record.crewName + " · " + record.pageLabel)).Append("\" src=\"").Append(Escape(record.file)).Append("\"></a>");
                html.Append("<figcaption>").Append(Escape(record.port + " · " + record.crewName + " · " + record.pageLabel))
                    .Append(record.referenceCharacter ? " · 用户参考角色" : string.Empty).Append("<small>").Append(Escape(record.crewId))
                    .Append(" · 正文最小字号 ").Append(record.fontSize).Append(" · ").Append(record.layoutPassed ? "文字检查通过" : "检查失败")
                    .Append("</small><small>").Append(Escape(record.portraitTexture)).Append("</small>");
                if (record.portraitMask != null)
                {
                    MaskCheck mask = record.portraitMask;
                    html.Append("<small>材质：").Append(Escape(mask.shader)).Append(" · 原 v2 源图：")
                        .Append(record.portraitIsOriginalV2Source ? "是" : "未确认").Append("</small>");
                    if (mask.recorded)
                        html.Append("<small>蒙版不透明 ").Append((mask.opaqueFraction * 100f).ToString("0.0")).Append("% · 透明 ")
                            .Append((mask.transparentFraction * 100f).ToString("0.0")).Append("% · 中间覆盖 ")
                            .Append((mask.intermediateFraction * 100f).ToString("0.0")).Append("% · 顶角透明：")
                            .Append(mask.topCornersTransparent ? "是" : "需目检").Append(" · 名义面部中心不透明：")
                            .Append(mask.nominalFaceCenterOpaque ? "是" : "需目检").Append("</small>");
                    if (!string.IsNullOrEmpty(mask.error)) html.Append("<small class=\"error\">蒙版记录：").Append(Escape(mask.error)).Append("</small>");
                }
                foreach (string error in record.errors) html.Append("<small class=\"error\">").Append(Escape(error)).Append("</small>");
                html.Append("</figcaption></figure>");
            }
            html.Append("</main>");
            foreach (string error in report.errors) html.Append("<p class=\"error\">").Append(Escape(error)).Append("</p>");
            html.Append("</body></html>");
            WriteNewFile(Path.Combine(directory, "index.html"), Encoding.UTF8.GetBytes(html.ToString()));
        }

        private static void WriteNewFile(string path, byte[] bytes)
        {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T value = root.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            if (value == null) throw new InvalidOperationException("Missing preview object: " + name);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Missing fixture field: " + name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Missing preview method: " + name);
            method.Invoke(target, null);
        }

        private static Exception Unwrap(Exception exception)
        {
            return exception is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : exception;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] != "-crewDossierCapturePath") continue;
                if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("-crewDossierCapturePath requires a fresh output directory.");
                return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            }
            throw new ArgumentException("Pass -crewDossierCapturePath followed by a new or empty output directory.");
        }
    }
}
