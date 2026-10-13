using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Read-only text measurements in a disposable Editor Canvas; never loads a player save.</summary>
    public static class CardDescriptionTypographyAudit
    {
        private static readonly Vector2 TextSize = new Vector2(176f, 94f);
        private static readonly Regex RichTags = new Regex(@"</?(?:b|i|size|color|material|quad)(?:\s[^>]*|=[^>]*)?>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex NumericTokens = new Regex(@"[+\-−]?\d+(?:\.\d+)?(?:%|‰)?",
            RegexOptions.CultureInvariant);

        [Serializable] private sealed class Report
        {
            public string generatedAtUtc, project, font, fontResourcePath, quoteContext, lineIndexMode, notes, auditMode;
            public Vector2 textSize;
            public float canvasScale = 1f, lineSpacing = 1f;
            public int activeCards, retiredSlots, definitionSamples, dynamicSamples;
            public int expectedActiveCards = 216, expectedDefinitionSamples = 864, expectedDynamicSamples = 1088;
            public bool formattedWrapApiAvailable;
            public int atlasReadbacks, atlasRebuildEvents;
            public List<string> errors = new List<string>();
            public List<Coverage> coverage = new List<Coverage>();
            public List<Histogram> actualFitFontSizes = new List<Histogram>();
            public List<Recommendation> recommendations = new List<Recommendation>();
            public List<Sample> samples = new List<Sample>();
        }

        [Serializable] private sealed class Coverage
        {
            public string kind, variant;
            public int rank = -1, fontSize, samples, fits, layoutFits, visibleInkFits, inkMeasurementsSucceeded;
            public float fitRate;
        }

        [Serializable] private sealed class Histogram
        {
            public string kind, variant;
            public int rank = -1, fontSize, samples;
        }

        [Serializable] private sealed class Recommendation
        {
            public string kind, variant;
            public int rank = -1, highestFontSizeAtLeast90Percent;
            public float targetRate = .9f;
        }

        [Serializable] private sealed class Sample
        {
            public string id, name, kind, stage, allowedStages, raw, formatted, rendered;
            public int rank, fontSize, lineCount;
            public bool fits, layoutFits, visibleInkFits, numericTokensPreserved, numericTokensSameMultiset, formattedIdempotent;
            public string[] rawNumericTokens, formattedNumericTokens;
            public Measurement actualFitRaw, actualFitFormatted;
            public List<Measurement> measurements = new List<Measurement>();
        }

        [Serializable] private sealed class Measurement
        {
            public string variant, lineSource, rendered, renderedInput, inkError;
            public int fontSize, lineCount, reportedLineCount, fullGlyphQuads, actualGlyphQuads;
            public bool fits, layoutFits, visibleInkFits, inkMeasurementSucceeded, fullTextGenerated, linesReconstructVisibleText;
            public float preferredHeight;
            public Rect fullMeshBounds;
            public Rect visibleInkBounds;
            public int atlasVersion, atlasWidth, atlasHeight;
            public int paintedGlyphQuads, confirmedWhitespaceQuads, inklessNonWhitespaceQuads;
            public int[] lineStartIndices;
            public string[] lines;
        }

        // -executeMethod ShatteredSail.Editor.CardDescriptionTypographyAudit.RunFromCommandLine
        // -descriptionAuditPath D:/Game/Artifacts/CardDescriptions20261012/Typography
        public static void RunFromCommandLine()
        {
            Run(false);
        }

        public static void RunInkProbeFromCommandLine()
        {
            Run(true);
        }

        private static void Run(bool probeOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Description audit requires a stopped isolated Editor.");
            string directory = OutputDirectory();
            Directory.CreateDirectory(directory);
            GameObject host = null;
            Report report = null;
            FontAtlasSampler atlas = null;
            try
            {
                Font font = GameTypography.BodyFont;
                if (font == null || font != Resources.Load<Font>(GameTypography.BodyFontResourcePath))
                    throw new InvalidOperationException("The embedded BodyFont is missing; fallback measurements are invalid.");
                atlas = new FontAtlasSampler(font, probeOnly);
                host = new GameObject("__CardDescriptionTypographyAudit", typeof(Canvas));
                host.hideFlags = HideFlags.HideAndDontSave;
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = 1f;
                Text text = new GameObject("Description Measure", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                text.transform.SetParent(host.transform, false);
                text.rectTransform.sizeDelta = TextSize;
                text.font = font;
                text.fontStyle = FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter;
                text.supportRichText = true;
                text.resizeTextForBestFit = false;
                text.lineSpacing = 1f;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.raycastTarget = false;

                report = new Report
                {
                    generatedAtUtc = DateTime.UtcNow.ToString("O"),
                    auditMode = probeOnly ? "visible_ink_probe" : "complete_catalog",
                    formattedWrapApiAvailable = WrapMethod() != null,
                    project = Directory.GetParent(Application.dataPath).FullName,
                    font = font.name, fontResourcePath = GameTypography.BodyFontResourcePath, textSize = TextSize,
                    quoteContext = "Definitions: immutable upgraded card.Description. Dynamic: public BattleCardFaceDescription " +
                        "at each allowed stage, port preview with 28 ordinary crew and 1 medicine, no live Battle. " +
                        "The fixed face template folds quoted immediate bonuses into numeric slots; live battle state changes are tested separately.",
                    notes = "Fixed font measurements use Text.GetGenerationSettings at scaleFactor=1, lineSpacing=1, " +
                        "full overflow mesh and line list, compared with the matching truncated mesh. layoutFits requires complete " +
                        "quads and preferredHeight<=94. visibleInkFits samples actual font-atlas alpha>1/255 through every " +
                        "quad UV affine mapping and tests visible pixel footprints against 176x94 (0.5 logical pixel raster tolerance). " +
                        "fullMeshBounds includes transparent texture padding and remains diagnostic only. fits requires both " +
                        "layoutFits and a successful visible-ink fit. No graphics/readback means failure, never a synthetic pass. " +
                        "Formatted fixed-size candidates use CardDescriptionText.Wrap when available; ActualFit measures " +
                        "the exact text.text produced by production FitEffect. " +
                        "Rich line indexing is probed at runtime and checked by reconstructing visible text. " +
                        "Unverified rich lines are explicitly replaced by an independent non-rich plain-text measurement. " +
                        "HTML is a text gallery, not an additional Unity raster capture. No voyage is played or saved."
                };
                report.lineIndexMode = ProbeLineIndices(text);
                CoreVoyageSession session = PreviewSession();
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
                    if (original.AllowedStages == NavalBattleStages.None) { report.retiredSlots++; continue; }
                    if (probeOnly && original.Id != "card_full_sail_turn" && original.Id != "card_load_shot") continue;
                    report.activeCards++;
                    for (int rank = 0; rank <= CoreBattleUpgradeBalance.MaximumLevel; rank++)
                    {
                        CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                        if (probeOnly && rank != 0) continue;
                        AddSample(report, text, atlas, card, rank, "definition", "Definition", card.Description, probeOnly);
                        report.definitionSamples++;
                        if (probeOnly) continue;
                        foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                        {
                            if (!card.IsAllowedInStage(stage)) continue;
                            AddSample(report, text, atlas, card, rank, "dynamic", stage.ToString(),
                                session.BattleCardFaceDescription(card, stage), false);
                            report.dynamicSamples++;
                        }
                    }
                }
                if (probeOnly)
                {
                    report.expectedActiveCards = 2;
                    report.expectedDefinitionSamples = 2;
                    report.expectedDynamicSamples = 0;
                }
                if (report.activeCards != report.expectedActiveCards)
                    report.errors.Add("Expected 216 active cards, measured " + report.activeCards + ".");
                if (report.definitionSamples != report.expectedDefinitionSamples)
                    report.errors.Add("Expected 864 ranked definitions, measured " + report.definitionSamples + ".");
                if (report.dynamicSamples != report.expectedDynamicSamples)
                    report.errors.Add("Expected 1088 legal-stage quotes, measured " + report.dynamicSamples + ".");
                foreach (Sample sample in report.samples)
                    foreach (Measurement measurement in sample.measurements.Concat(new[] { sample.actualFitRaw, sample.actualFitFormatted }))
                        if (!measurement.inkMeasurementSucceeded && report.errors.Count < 20)
                            report.errors.Add(sample.id + "/" + sample.rank + "/" + sample.stage + "/" + measurement.variant +
                                "/" + measurement.fontSize + ": " + measurement.inkError);
                report.atlasReadbacks = atlas.Readbacks;
                report.atlasRebuildEvents = atlas.Version;
                if (!probeOnly) Summarize(report);
                Save(directory, report);
                if (report.errors.Count > 0)
                    throw new InvalidOperationException(string.Join("\n", report.errors));
                Debug.Log("CARD_DESCRIPTION_TYPOGRAPHY_AUDIT_OK: " + report.definitionSamples + " definitions, " +
                    report.dynamicSamples + " legal-stage quotes. " + Path.Combine(directory, "report.json"));
            }
            catch (Exception exception)
            {
                if (report != null)
                {
                    if (!report.errors.Contains(exception.Message)) report.errors.Add(exception.ToString());
                    Save(directory, report);
                }
                throw;
            }
            finally
            {
                if (atlas != null) atlas.Dispose();
                if (host != null) Object.DestroyImmediate(host);
            }
        }

        private static CoreVoyageSession PreviewSession()
        {
            var session = new CoreVoyageSession();
            if (!session.TryDepositAllPortCargo() || !session.PortWarehouse.Restore("v2") || !session.SelectShip(7))
                throw new InvalidOperationException("Could not configure the declared 28-crew port preview fixture.");
            session.ConfigureDepartureCrew(28, 0);
            if (session.SelectedOrdinaryCrew != 28)
                throw new InvalidOperationException("The description fixture does not have exactly 28 ordinary crew.");
            session.Cargo.Medicine = 1;
            return session;
        }

        private static void AddSample(Report report, Text text, FontAtlasSampler atlas, CoreBattleCard card, int rank,
            string kind, string stage, string source, bool probeOnly)
        {
            string raw = CoreBattleCardText.FormatEffects(source);
            string formatted = CardFaceLayout.FormatDescription(raw);
            string[] before = Numbers(raw), after = Numbers(formatted);
            var sample = new Sample
            {
                id = card.Id, name = card.Name, rank = rank, kind = kind, stage = stage,
                allowedStages = card.AllowedStages.ToString(), raw = raw, formatted = formatted,
                rawNumericTokens = before, formattedNumericTokens = after,
                numericTokensPreserved = before.SequenceEqual(after),
                numericTokensSameMultiset = before.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(after.OrderBy(value => value, StringComparer.Ordinal)),
                formattedIdempotent = formatted == CardFaceLayout.FormatDescription(formatted)
            };
            for (int size = probeOnly ? 17 : 14; size <= (probeOnly ? 17 : 19); size++)
            {
                sample.measurements.Add(Measure(text, atlas, raw, size, "raw", report.lineIndexMode));
                sample.measurements.Add(Measure(text, atlas, formatted, size, "formatted", report.lineIndexMode));
            }
            sample.actualFitRaw = ActualFit(text, atlas, raw, "raw", report.lineIndexMode);
            sample.actualFitFormatted = ActualFit(text, atlas, formatted, "formatted", report.lineIndexMode);
            sample.fontSize = sample.actualFitFormatted.fontSize;
            sample.lineCount = sample.actualFitFormatted.lineCount;
            sample.fits = sample.actualFitFormatted.fits;
            sample.layoutFits = sample.actualFitFormatted.layoutFits;
            sample.visibleInkFits = sample.actualFitFormatted.visibleInkFits;
            sample.rendered = sample.actualFitFormatted.rendered;
            report.samples.Add(sample);
        }

        private static Measurement ActualFit(Text text, FontAtlasSampler atlas, string content, string variant, string indexMode)
        {
            text.text = content;
            CardFaceLayout.FitEffect(text);
            return Measure(text, atlas, text.text, text.fontSize, variant, indexMode, false);
        }

        private static MethodInfo WrapMethod() => typeof(CardDescriptionText).GetMethod("Wrap", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Text), typeof(string), typeof(int) }, null);

        private static TextGenerationSettings Settings(Text text, int size, VerticalWrapMode overflow, bool rich = true)
        {
            text.fontSize = size;
            TextGenerationSettings settings = text.GetGenerationSettings(TextSize);
            settings.scaleFactor = 1f;
            settings.fontSize = size;
            settings.fontStyle = FontStyle.Normal;
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = overflow;
            settings.lineSpacing = 1f;
            settings.richText = rich;
            return settings;
        }

        private static Measurement Measure(Text text, FontAtlasSampler atlas, string content, int size, string variant,
            string indexMode, bool wrapCandidate = true)
        {
            text.fontSize = size;
            MethodInfo wrap = wrapCandidate && variant == "formatted" ? WrapMethod() : null;
            if (wrap != null)
            {
                if (wrap.ReturnType != typeof(string)) throw new InvalidOperationException("CardDescriptionText.Wrap must return the rendered string.");
                content = (string)wrap.Invoke(null, new object[] { text, content, size });
            }
            atlas.BeginMeasurement(content, size);
            TextGenerationSettings settings = Settings(text, size, VerticalWrapMode.Overflow);
            using (var complete = new TextGenerator())
            using (var actual = new TextGenerator())
            using (var metrics = new TextGenerator())
            {
                bool populated = false, actualPopulated = false, inkMeasured = false;
                Rect ink = Rect.zero;
                int version = 0, atlasWidth = 0, atlasHeight = 0;
                string inkError = string.Empty;
                float height = 0f;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    // Warm all glyphs before recording UVs. A later atlas rebuild
                    // must never make those UVs point at a different character.
                    metrics.Invalidate();
                    height = metrics.GetPreferredHeight(content, settings);
                    complete.Invalidate();
                    populated = complete.Populate(content, settings);
                    version = atlas.Version;
                    TextGenerationSettings actualSettings = settings;
                    actualSettings.verticalOverflow = VerticalWrapMode.Truncate;
                    actual.Invalidate();
                    actualPopulated = actual.Populate(content, actualSettings);
                    inkMeasured = atlas.TryVisibleBounds(complete.verts, version, out ink,
                        out atlasWidth, out atlasHeight, out inkError);
                    if (inkMeasured || !inkError.StartsWith("Atlas changed", StringComparison.Ordinal)) break;
                }
                int fullQuads = GlyphQuadCount(complete.verts, out Rect bounds);
                int[] starts = complete.lines.Select(line => line.startCharIdx).ToArray();
                string plain = Plain(content);
                string[] lines;
                bool reconstructed = TryLines(content, plain, starts, indexMode, out lines);
                string lineSource = indexMode + "; visible-text reconstruction checked";
                if (!reconstructed)
                {
                    using (var plainGenerator = new TextGenerator())
                    {
                        TextGenerationSettings plainSettings = settings;
                        plainSettings.richText = false;
                        plainGenerator.Populate(plain, plainSettings);
                        int[] plainStarts = plainGenerator.lines.Select(line => line.startCharIdx).ToArray();
                        TryLines(plain, plain, plainStarts, "plain-visible-indices", out lines);
                    }
                    lineSource = "independent plainText measurement; rich wrapping may differ";
                }
                int actualQuads = GlyphQuadCount(actual.verts, out _);
                Rect box = text.rectTransform.rect;
                bool allGenerated = populated && actualPopulated && actualQuads == fullQuads;
                bool layoutFits = allGenerated && fullQuads > 0 && height <= TextSize.y + .01f;
                bool visibleInkFits = inkMeasured && ink.xMin >= box.xMin - .5f && ink.xMax <= box.xMax + .5f &&
                    ink.yMin >= box.yMin - .5f && ink.yMax <= box.yMax + .5f;
                return new Measurement
                {
                    renderedInput = content,
                    variant = variant, fontSize = size, lineCount = starts.Length, lineStartIndices = starts,
                    lines = lines, reportedLineCount = lines.Length, rendered = string.Join("\n", lines),
                    lineSource = lineSource, linesReconstructVisibleText = reconstructed,
                    preferredHeight = height, fullMeshBounds = bounds,
                    fullGlyphQuads = fullQuads, actualGlyphQuads = actualQuads, fullTextGenerated = allGenerated,
                    layoutFits = layoutFits, visibleInkFits = visibleInkFits, inkMeasurementSucceeded = inkMeasured,
                    visibleInkBounds = ink, inkError = inkError, atlasVersion = version,
                    paintedGlyphQuads = atlas.PaintedGlyphQuads, confirmedWhitespaceQuads = atlas.WhitespaceQuads,
                    inklessNonWhitespaceQuads = atlas.MissingGlyphQuads,
                    atlasWidth = atlasWidth, atlasHeight = atlasHeight, fits = layoutFits && visibleInkFits
                };
            }
        }

        private static string ProbeLineIndices(Text text)
        {
            // An explicit newline after tags distinguishes raw-source and visible-character indices.
            const string source = "<b>AB</b>\nCD";
            using (var generator = new TextGenerator())
            {
                TextGenerationSettings settings = Settings(text, 16, VerticalWrapMode.Overflow);
                settings.generationExtents = new Vector2(1000f, 94f);
                if (!generator.Populate(source, settings) || generator.lines.Count != 2)
                    return "unverified-rich-indices";
                int second = generator.lines[1].startCharIdx;
                if (second == source.IndexOf('C')) return "rich-source-indices";
                if (second == Plain(source).IndexOf('C')) return "plain-visible-indices";
                return "unverified-rich-indices";
            }
        }

        private static bool TryLines(string source, string plain, int[] starts, string mode, out string[] lines)
        {
            lines = Array.Empty<string>();
            if (starts.Length == 0 || starts[0] != 0 || mode == "unverified-rich-indices") return false;
            bool sourceIndices = mode == "rich-source-indices";
            string input = sourceIndices ? source : plain;
            var result = new List<string>(starts.Length);
            for (int index = 0; index < starts.Length; index++)
            {
                int start = starts[index], end = index + 1 < starts.Length ? starts[index + 1] : input.Length;
                if (start < 0 || end < start || end > input.Length) return false;
                string line = input.Substring(start, end - start);
                result.Add((sourceIndices ? Plain(line) : line).Replace("\r", string.Empty).Replace("\n", string.Empty));
            }
            lines = result.ToArray();
            return string.Join(string.Empty, lines) == plain.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private static int GlyphQuadCount(IList<UIVertex> vertices, out Rect bounds)
        {
            int count = 0;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int index = 0; index + 3 < vertices.Count; index += 4)
            {
                Vector2 min = vertices[index].position, max = min;
                for (int corner = 1; corner < 4; corner++)
                {
                    Vector2 point = vertices[index + corner].position;
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                if (max.x - min.x < .001f || max.y - min.y < .001f) continue;
                count++;
                minimum = Vector2.Min(minimum, min); maximum = Vector2.Max(maximum, max);
            }
            bounds = count == 0 ? Rect.zero : Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return count;
        }

        private sealed class FontAtlasSampler : IDisposable
        {
            private readonly Font font;
            private readonly bool freshEveryMeasurement;
            private byte[] alpha;
            private int cachedVersion = -1, textureId, width, height;
            private readonly Dictionary<Vector4, RectInt> inkCells = new Dictionary<Vector4, RectInt>();
            private readonly HashSet<(char character, int size, FontStyle style)> sampledGlyphs =
                new HashSet<(char character, int size, FontStyle style)>();
            private List<(char character, int size, FontStyle style)> currentGlyphs;
            private bool forceReadback;
            public int Version { get; private set; }
            public int Readbacks { get; private set; }
            public int PaintedGlyphQuads { get; private set; }
            public int WhitespaceQuads { get; private set; }
            public int MissingGlyphQuads { get; private set; }

            public FontAtlasSampler(Font font, bool freshEveryMeasurement)
            {
                this.font = font;
                this.freshEveryMeasurement = freshEveryMeasurement;
                Font.textureRebuilt += OnTextureRebuilt;
            }

            public void BeginMeasurement(string content, int fontSize)
            {
                // Appending glyphs to an existing atlas does not necessarily
                // raise textureRebuilt. Cache only glyphs captured after their
                // exact size/style was requested by this measurement.
                currentGlyphs = RequestedGlyphs(content, fontSize);
                forceReadback = freshEveryMeasurement || currentGlyphs.Any(glyph => !sampledGlyphs.Contains(glyph));
            }

            private void OnTextureRebuilt(Font changed)
            {
                if (changed == font) { Version++; sampledGlyphs.Clear(); }
            }

            public void Dispose()
            {
                Font.textureRebuilt -= OnTextureRebuilt;
                alpha = null;
                inkCells.Clear();
                sampledGlyphs.Clear();
                currentGlyphs = null;
            }

            public bool TryVisibleBounds(IList<UIVertex> vertices, int generationVersion, out Rect bounds,
                out int atlasWidth, out int atlasHeight, out string error)
            {
                bounds = Rect.zero;
                atlasWidth = atlasHeight = 0;
                error = string.Empty;
                PaintedGlyphQuads = WhitespaceQuads = MissingGlyphQuads = 0;
                if (generationVersion != Version) { error = "Atlas changed after glyph generation."; return false; }
                if (!ReadAtlas(generationVersion, out error)) return false;
                atlasWidth = width; atlasHeight = height;
                Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                int visible = 0;
                for (int index = 0; index + 3 < vertices.Count; index += 4)
                {
                    Vector2 originUv = Uv(vertices[index]), edgeU = Uv(vertices[index + 1]) - originUv,
                        edgeV = Uv(vertices[index + 3]) - originUv;
                    Vector2 origin = vertices[index].position, positionU = (Vector2)vertices[index + 1].position - origin,
                        positionV = (Vector2)vertices[index + 3].position - origin;
                    float determinant = edgeU.x * edgeV.y - edgeU.y * edgeV.x;
                    if (Mathf.Abs(positionU.x * positionV.y - positionU.y * positionV.x) < .000001f) continue;
                    if (Mathf.Abs(determinant) < .000000001f) { MissingGlyphQuads++; continue; }
                    Vector2 uvMin = originUv, uvMax = originUv;
                    for (int corner = 1; corner < 4; corner++)
                    {
                        Vector2 uv = Uv(vertices[index + corner]);
                        uvMin = Vector2.Min(uvMin, uv); uvMax = Vector2.Max(uvMax, uv);
                    }
                    RectInt pixels = InkPixels(uvMin, uvMax);
                    if (pixels.width == 0 || pixels.height == 0)
                    {
                        if (IsWhitespaceCell(uvMin, uvMax)) WhitespaceQuads++;
                        else MissingGlyphQuads++;
                        continue;
                    }
                    visible++;
                    PaintedGlyphQuads++;
                    // Include the footprint of each alpha-positive texel, not
                    // its transparent padded quad. Clamp footprints to the UV cell.
                    Vector2 inkMin = Vector2.Max(uvMin, new Vector2((float)pixels.xMin / width, (float)pixels.yMin / height));
                    Vector2 inkMax = Vector2.Min(uvMax, new Vector2((float)pixels.xMax / width, (float)pixels.yMax / height));
                    foreach (Vector2 uv in new[] { inkMin, new Vector2(inkMin.x, inkMax.y), inkMax, new Vector2(inkMax.x, inkMin.y) })
                    {
                        Vector2 delta = uv - originUv;
                        float u = (delta.x * edgeV.y - delta.y * edgeV.x) / determinant;
                        float v = (edgeU.x * delta.y - edgeU.y * delta.x) / determinant;
                        Vector2 point = origin + u * positionU + v * positionV;
                        minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
                    }
                }
                if (generationVersion != Version) { error = "Atlas changed during ink sampling."; return false; }
                if (MissingGlyphQuads > 0)
                {
                    error = MissingGlyphQuads + " nonempty glyph quads have no verified alpha-positive ink (not confirmed whitespace).";
                    return false;
                }
                if (visible == 0) { error = "No alpha-positive glyph pixels were sampled from the actual font atlas."; return false; }
                bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
                return true;
            }

            private bool ReadAtlas(int generationVersion, out string error)
            {
                error = string.Empty;
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                { error = "Graphics device is Null; visible ink cannot be verified."; return false; }
                Texture source = font.material != null ? font.material.mainTexture : null;
                if (source == null || source.width <= 0 || source.height <= 0)
                { error = "The dynamic font has no usable atlas texture."; return false; }
                if (!forceReadback && alpha != null && cachedVersion == generationVersion && textureId == source.GetInstanceID() &&
                    width == source.width && height == source.height) return true;
                RenderTexture temporary = null, previous = RenderTexture.active;
                Texture2D readable = null;
                try
                {
                    int w = source.width, h = source.height;
                    temporary = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readable = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                    readable.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
                    readable.Apply(false, false);
                    Color32[] pixels = readable.GetPixels32();
                    if (Version != generationVersion || font.material.mainTexture != source || source.width != w || source.height != h)
                    { error = "Atlas changed during readable texture capture."; return false; }
                    byte[] snapshot = new byte[pixels.Length];
                    for (int index = 0; index < pixels.Length; index++) snapshot[index] = pixels[index].a;
                    alpha = snapshot; width = w; height = h; textureId = source.GetInstanceID();
                    cachedVersion = generationVersion; inkCells.Clear(); Readbacks++;
                    sampledGlyphs.UnionWith(currentGlyphs);
                    forceReadback = false;
                    return true;
                }
                catch (Exception exception) { error = "Font atlas readback failed: " + exception.Message; return false; }
                finally
                {
                    RenderTexture.active = previous;
                    if (readable != null) Object.DestroyImmediate(readable);
                    if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                }
            }

            private RectInt InkPixels(Vector2 uvMin, Vector2 uvMax)
            {
                int left = Mathf.Clamp(Mathf.CeilToInt(uvMin.x * width - .5f), 0, width - 1);
                int right = Mathf.Clamp(Mathf.FloorToInt(uvMax.x * width - .5f), 0, width - 1);
                int bottom = Mathf.Clamp(Mathf.CeilToInt(uvMin.y * height - .5f), 0, height - 1);
                int top = Mathf.Clamp(Mathf.FloorToInt(uvMax.y * height - .5f), 0, height - 1);
                Vector4 key = new Vector4(left, bottom, right, top);
                if (inkCells.TryGetValue(key, out RectInt cached)) return cached;
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = bottom; y <= top; y++)
                    for (int x = left; x <= right; x++)
                        if (alpha[y * width + x] > 1)
                        {
                            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                        }
                RectInt result = maxX < 0 ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                inkCells[key] = result;
                return result;
            }

            private bool IsWhitespaceCell(Vector2 uvMin, Vector2 uvMax)
            {
                foreach (var glyph in currentGlyphs)
                {
                    if (!char.IsWhiteSpace(glyph.character) || !font.GetCharacterInfo(glyph.character,
                        out CharacterInfo information, glyph.size, glyph.style)) continue;
                    Vector2 min = Vector2.Min(Vector2.Min(information.uvBottomLeft, information.uvBottomRight),
                        Vector2.Min(information.uvTopLeft, information.uvTopRight));
                    Vector2 max = Vector2.Max(Vector2.Max(information.uvBottomLeft, information.uvBottomRight),
                        Vector2.Max(information.uvTopLeft, information.uvTopRight));
                    if ((min - uvMin).sqrMagnitude < .0000000001f && (max - uvMax).sqrMagnitude < .0000000001f) return true;
                }
                return false;
            }

            private static List<(char character, int size, FontStyle style)> RequestedGlyphs(string content, int fontSize)
            {
                var requests = new HashSet<(char character, int size, FontStyle style)>();
                bool bold = false, italic = false;
                int size = fontSize;
                var sizes = new Stack<int>();
                for (int index = 0; index < content.Length; index++)
                {
                    if (content[index] == '<')
                    {
                        int end = content.IndexOf('>', index + 1);
                        if (end >= 0)
                        {
                            string tag = content.Substring(index + 1, end - index - 1).ToLowerInvariant();
                            if (tag == "b") bold = true;
                            else if (tag == "/b") bold = false;
                            else if (tag == "i") italic = true;
                            else if (tag == "/i") italic = false;
                            else if (tag.StartsWith("size=", StringComparison.Ordinal) && int.TryParse(tag.Substring(5), out int explicitSize))
                            { sizes.Push(size); size = explicitSize; }
                            else if (tag == "/size" && sizes.Count > 0) size = sizes.Pop();
                            index = end;
                            continue;
                        }
                    }
                    if (content[index] == '\n' || content[index] == '\r') continue;
                    FontStyle style = bold ? (italic ? FontStyle.BoldAndItalic : FontStyle.Bold) :
                        italic ? FontStyle.Italic : FontStyle.Normal;
                    requests.Add((content[index], size, style));
                }
                return requests.ToList();
            }

            private static Vector2 Uv(UIVertex vertex) => new Vector2(vertex.uv0.x, vertex.uv0.y);
        }

        private static void Summarize(Report report)
        {
            foreach (string kind in new[] { "definition", "dynamic", "all" })
            {
                foreach (int rank in kind == "definition" ? new[] { -1, 0, 1, 2, 3 } : new[] { -1 })
                {
                List<Sample> samples = report.samples.Where(sample => (kind == "all" || sample.kind == kind) &&
                    (rank < 0 || sample.rank == rank)).ToList();
                foreach (string variant in new[] { "raw", "formatted" })
                {
                    int highest = 0;
                    for (int size = 14; size <= 19; size++)
                    {
                        int fits = samples.Count(sample => sample.measurements.Single(value =>
                            value.variant == variant && value.fontSize == size).fits);
                        float rate = samples.Count == 0 ? 0f : (float)fits / samples.Count;
                        report.coverage.Add(new Coverage { kind = kind, rank = rank, variant = variant, fontSize = size,
                            samples = samples.Count, fits = fits, fitRate = rate,
                            layoutFits = samples.Count(sample => sample.measurements.Single(value => value.variant == variant && value.fontSize == size).layoutFits),
                            visibleInkFits = samples.Count(sample => sample.measurements.Single(value => value.variant == variant && value.fontSize == size).visibleInkFits),
                            inkMeasurementsSucceeded = samples.Count(sample => sample.measurements.Single(value => value.variant == variant && value.fontSize == size).inkMeasurementSucceeded) });
                        if (rate >= .9f) highest = size;
                    }
                    report.recommendations.Add(new Recommendation { kind = kind, rank = rank, variant = variant,
                        highestFontSizeAtLeast90Percent = highest });
                    foreach (var group in samples.GroupBy(sample => (variant == "raw" ?
                        sample.actualFitRaw : sample.actualFitFormatted).fontSize).OrderBy(group => group.Key))
                        report.actualFitFontSizes.Add(new Histogram { kind = kind, rank = rank, variant = variant,
                            fontSize = group.Key, samples = group.Count() });
                }
                }
            }
            foreach (string variant in new[] { "raw", "formatted" })
            {
                int highest = 0;
                for (int size = 14; size <= 19; size++)
                    if (report.coverage.Where(row => row.kind == "definition" && row.rank >= 0 &&
                        row.variant == variant && row.fontSize == size).All(row => row.fitRate >= .9f)) highest = size;
                report.recommendations.Add(new Recommendation { kind = "definition_each_rank", variant = variant,
                    highestFontSizeAtLeast90Percent = highest });
            }
        }

        private static string[] Numbers(string value) => NumericTokens.Matches(Plain(value)).Cast<Match>()
            .Select(match => match.Value).ToArray();
        private static string Plain(string value) => RichTags.Replace(value ?? string.Empty, string.Empty);

        private static string OutputDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(arguments, "-descriptionAuditPath");
            if (option < 0 || option + 1 >= arguments.Length || !Path.IsPathRooted(arguments[option + 1]))
                throw new ArgumentException("Provide -descriptionAuditPath with an absolute Artifacts directory.");
            string path = Path.GetFullPath(arguments[option + 1]);
            if (!path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("The description audit output must be inside an Artifacts directory.");
            return path;
        }

        private static void Save(string directory, Report report)
        {
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            var html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>Card description typography audit</title>" +
                "<style>body{max-width:1300px;margin:24px auto;background:#f0e3c4;color:#302819;font:16px system-ui}table{border-collapse:collapse;width:100%}" +
                "td,th{border:1px solid #aa9975;padding:6px;vertical-align:top}pre{white-space:pre-wrap;margin:0;line-height:1.4}details{margin:12px 0;padding:10px;background:#fff7e7}" +
                ".fail{color:#9c2618}.ok{color:#286036}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}</style><h1>卡牌描述字号与换行审计</h1>");
            html.Append("<p>定义 ").Append(report.definitionSamples).Append("；合法阶段报价 ").Append(report.dynamicSamples)
                .Append("；文字框 176×94，Canvas 1×。浏览器仅展示文本，Unity 测量结果为准。</p><p>")
                .Append(Escape(report.quoteContext)).Append("</p><p>行索引：").Append(Escape(report.lineIndexMode))
                .Append("；无法验证的富文本切片会明确标为独立纯文本换行。</p><table><tr><th>范围/文本</th><th>字号</th><th>拟合数</th><th>拟合率</th></tr>");
            foreach (Coverage row in report.coverage)
                html.Append("<tr><td>").Append(Escape(row.kind + " / " + row.variant +
                    (row.rank < 0 ? " / all ranks" : " / rank " + row.rank))).Append("</td><td>").Append(row.fontSize)
                    .Append("</td><td>").Append(row.fits).Append('/').Append(row.samples).Append("</td><td>")
                    .Append((row.fitRate * 100f).ToString("0.0", CultureInfo.InvariantCulture)).Append("%</td></tr>");
            html.Append("</table><p>≥90% 最高字号（0 表示 14–19 均未达到）：");
            foreach (Recommendation row in report.recommendations)
                html.Append(Escape(row.kind + "/" + row.variant + (row.rank < 0 ? "" : "/rank" + row.rank)))
                    .Append('=').Append(row.highestFontSizeAtLeast90Percent).Append("； ");
            html.Append("</p>");
            foreach (Sample sample in report.samples)
            {
                html.Append("<details><summary>").Append(Escape(sample.id + " · " + sample.name + " +" + sample.rank + " · " + sample.stage))
                    .Append(" — FitEffect ").Append(sample.fontSize).Append("px · ").Append(sample.lineCount).Append("行 · ")
                    .Append(sample.fits ? "完整拟合" : "需要检查").Append(" · 数字保留 ").Append(sample.numericTokensPreserved).Append("</summary><div class=pair><section><h3>Core 原文</h3><pre>")
                    .Append(Escape(sample.raw)).Append("</pre></section><section><h3>展示格式</h3><pre>")
                    .Append(Escape(Plain(sample.formatted))).Append("</pre></section></div><h3>实际 FitEffect 自动换行</h3><pre>")
                    .Append(Escape(sample.rendered)).Append("</pre><p>").Append(Escape(sample.actualFitFormatted.lineSource))
                    .Append("</p><table><tr><th>字号</th><th>原文换行</th><th>展示换行</th></tr>");
                foreach (int size in sample.measurements.Select(value => value.fontSize).Distinct().OrderBy(value => value))
                {
                    html.Append("<tr><td>").Append(size).Append("</td>");
                    foreach (string variant in new[] { "raw", "formatted" })
                    {
                        Measurement row = sample.measurements.Single(value => value.fontSize == size && value.variant == variant);
                        html.Append("<td class=\"").Append(row.fits ? "ok" : "fail").Append("\">")
                            .Append(row.fits ? "✓ " : "✗ ").Append(row.lineCount).Append("行；完整字形 ")
                            .Append(row.actualGlyphQuads).Append('/').Append(row.fullGlyphQuads)
                            .Append("；layout ").Append(row.layoutFits).Append("；ink ").Append(row.visibleInkFits).Append("<pre>")
                            .Append(Escape(row.rendered)).Append("</pre><small>").Append(Escape(row.lineSource)).Append("</small></td>");
                    }
                    html.Append("</tr>");
                }
                html.Append("</table></details>");
            }
            html.Append("</html>");
            File.WriteAllText(Path.Combine(directory, "gallery.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value) => (value ?? string.Empty).Replace("&", "&amp;")
            .Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
