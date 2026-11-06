using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>Opt-in candidate import only; production PNGs and metadata are never changed.</summary>
    public sealed class RamComicCompressionCandidateTests
    {
        private const double MinimumPsnr = 38d;
        private const double MaximumMeanError = 2d;
        private static Type Policy => Type.GetType("ShatteredSail.Editor.DesktopBackgroundCompressionPolicy, ShatteredSail.Editor", true);

        [Serializable] private sealed class Difference
        {
            public double psnrDb, meanAbsoluteChannelError, edgeMeanAbsoluteChannelError;
            public int peakChannelError, p95ChannelError, p99ChannelError, edgePeakChannelError, edgeP95ChannelError, edgeP99ChannelError;
        }
        [Serializable] private sealed class Page
        {
            public string resource, sourceSha256, sourceMetaSha256, baselineFormat, candidateFormat, error;
            public int sourceWidth, sourceHeight, importedWidth, importedHeight;
            public long baselineRuntimeBytes, candidateRuntimeBytes, originalDecodedRuntimeBytes;
            public double candidateImportSeconds, runtimeReduction;
            public bool baselineReadable, candidateReadable, sourcePngUnchanged, sourceMetaUnchanged, qualityPassed;
            public Difference originalToCandidate, importedBaselineToCandidate, originalToImportedBaseline;
        }
        [Serializable] private sealed class Report
        {
            public string scope, alignment, generatedUtc, graphicsDevice, colorSpace, note, policySourceSha256, postprocessorSourceSha256,
                catalogSourceSha256, presentationSourceSha256;
            public double minimumPsnrDb = MinimumPsnr, maximumMeanAbsoluteChannelError = MaximumMeanError;
            public bool temporaryAssetsRemoved;
            public List<Page> pages = new List<Page>();
        }

        [Test, Timeout(1800000)]
        public void ArtOptimization10_CompareCandidateImportsWithoutChangingProductionSources()
        {
            string scope = Environment.GetEnvironmentVariable("ART_OPT_RAM_COMPRESSION");
            if (scope != "sample" && scope != "all") Assert.Ignore("Set ART_OPT_RAM_COMPRESSION=sample or all for the isolated import experiment.");
            string alignment = Environment.GetEnvironmentVariable("ART_OPT_RAM_ALIGNMENT");
            bool pad = alignment == "pad";
            Assert.That(string.IsNullOrEmpty(alignment) || pad || alignment == "resample", Is.True);
            string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            Assert.That(data.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase), Is.True,
                "Candidate imports are permitted only in a borrowed temporary project.");
            AssertIsolatedValidationIdentity();
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            Assert.That(SystemInfo.SupportsTextureFormat(TextureFormat.BC7), Is.True);

            string destination = Environment.GetEnvironmentVariable("ART_OPT_RAM_REPORT_DIR");
            if (string.IsNullOrWhiteSpace(destination))
            {
                string capture = Environment.GetEnvironmentVariable("COMIC_PAGINATION_CAPTURE_DIR");
                Assert.That(capture, Is.Not.Null.And.Not.Empty, "Supply ART_OPT_RAM_REPORT_DIR or the normal comic capture directory.");
                destination = Path.Combine(Path.GetDirectoryName(capture), "ram-compression");
            }
            destination = Path.GetFullPath(destination);
            Assert.That(destination.Replace('\\', '/').StartsWith("D:/Game/outputs/", StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any(), Is.False,
                "Use a fresh report directory.");
            Directory.CreateDirectory(destination);
            string reportPath = Path.Combine(destination, "report.json");
            string folder = "Assets/__ArtOptimizationRamCandidate_" + Guid.NewGuid().ToString("N");
            var listed = (HashSet<string>)Policy.GetField("pathSet", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var padded = (HashSet<string>)Policy.GetField("paddedPathSet", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var report = new Report
            {
                scope = scope, alignment = pad ? "pad" : "resample", generatedUtc = DateTime.UtcNow.ToString("O"), graphicsDevice = SystemInfo.graphicsDeviceName,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                policySourceSha256 = Hash("Assets/_Project/Editor/DesktopBackgroundCompressionPolicy.cs"),
                postprocessorSourceSha256 = Hash("Assets/_Project/Editor/DesktopBackgroundCompressionPostprocessor.cs"),
                catalogSourceSha256 = Hash("Assets/_Project/Presentation/Runtime/RamComicCatalog.cs"),
                presentationSourceSha256 = Hash("Assets/_Project/Presentation/Runtime/CoreGameplayRamComicPresentation.cs"),
                note = (pad ? "Candidate imports preserve source pixels with repeated edge padding; GPU sampling excludes padding. " : "Candidate imports use whole-image resampling to block alignment. ") + "PNG bytes are unchanged. Both imported textures are non-readable; Profiler runtime bytes are measured native texture allocations, not package size or total application GPU memory. Import timing is not first-play loading time. A/B PNGs are GPU readbacks at original source dimensions, not live gameplay screenshots."
            };
            var failures = new List<string>();
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                int count = scope == "sample" ? 1 : RamComicCatalog.ResourceCount;
                for (int index = 0; index < count; index++)
                {
                    string resource = RamComicCatalog.ImageResource(index / RamComicCatalog.PagesPerSequence, index % RamComicCatalog.PagesPerSequence);
                    string originalPath = "Assets/_Project/Resources/" + resource + ".png";
                    string baselinePath = folder + "/baseline.png";
                    string candidatePath = folder + "/candidate.png";
                    var record = new Page { resource = resource, sourceSha256 = Hash(originalPath), sourceMetaSha256 = Hash(originalPath + ".meta") };
                    report.pages.Add(record);
                    Texture2D decoded = null, originalPixels = null, baselinePixels = null, candidatePixels = null;
                    try
                    {
                        byte[] png = File.ReadAllBytes(originalPath);
                        decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                        Assert.That(decoded.LoadImage(png, true), Is.True);
                        decoded.filterMode = FilterMode.Bilinear;
                        decoded.wrapMode = TextureWrapMode.Clamp;
                        record.sourceWidth = decoded.width; record.sourceHeight = decoded.height;
                        record.originalDecodedRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(decoded);
                        CopySource(originalPath, baselinePath);
                        AssetDatabase.ImportAsset(baselinePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        var baselineImporter = (TextureImporter)AssetImporter.GetAtPath(baselinePath);
                        var baselineSettings = baselineImporter.GetPlatformTextureSettings("Standalone");
                        baselineSettings.name = "Standalone"; baselineSettings.overridden = true;
                        baselineSettings.format = TextureImporterFormat.Automatic;
                        baselineSettings.textureCompression = TextureImporterCompression.Uncompressed;
                        baselineSettings.maxTextureSize = 4096;
                        baselineImporter.SetPlatformTextureSettings(baselineSettings);
                        baselineImporter.isReadable = false;
                        baselineImporter.SaveAndReimport();
                        Texture2D baseline = AssetDatabase.LoadAssetAtPath<Texture2D>(baselinePath);
                        record.baselineFormat = baseline.format.ToString();
                        record.baselineReadable = baseline.isReadable;
                        record.baselineRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(baseline);

                        CopySource(originalPath, candidatePath);
                        Assert.That(listed.Add(candidatePath), Is.True);
                        if (pad) Assert.That(padded.Add(candidatePath), Is.True);
                        var watch = Stopwatch.StartNew();
                        AssetDatabase.ImportAsset(candidatePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        watch.Stop();
                        record.candidateImportSeconds = watch.Elapsed.TotalSeconds;
                        Texture2D candidate = AssetDatabase.LoadAssetAtPath<Texture2D>(candidatePath);
                        record.candidateFormat = candidate.format.ToString();
                        record.candidateReadable = candidate.isReadable;
                        record.candidateRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(candidate);
                        record.importedWidth = candidate.width; record.importedHeight = candidate.height;
                        record.runtimeReduction = 1d - record.candidateRuntimeBytes / (double)record.baselineRuntimeBytes;
                        Assert.That(candidate.format, Is.EqualTo(TextureFormat.BC7), "Verify actual GPU encoding, not importer settings.");
                        Assert.That(candidate.isReadable, Is.False);
                        Assert.That(baseline.isReadable, Is.False);
                        Assert.That(candidate.width % 4, Is.Zero);
                        Assert.That(candidate.height % 4, Is.Zero);
                        Assert.That(Math.Abs(candidate.width - decoded.width), Is.LessThanOrEqualTo(2));
                        Assert.That(Math.Abs(candidate.height - decoded.height), Is.LessThanOrEqualTo(2));

                        originalPixels = Readback(decoded, decoded.width, decoded.height);
                        baselinePixels = Readback(baseline, decoded.width, decoded.height);
                        Rect displayed = pad ? RamComicCatalog.ImageUvRect(candidate) : new Rect(0f, 0f, 1f, 1f);
                        Assert.That(candidate.width * displayed.width, Is.EqualTo(decoded.width).Within(.001f));
                        if (pad) Assert.That(candidate.height * displayed.height, Is.EqualTo(decoded.height).Within(.001f));
                        candidatePixels = Readback(candidate, decoded.width, decoded.height, displayed.size);
                        Color32[] originalSamples = originalPixels.GetPixels32();
                        Color32[] baselineSamples = baselinePixels.GetPixels32();
                        Color32[] candidateSamples = candidatePixels.GetPixels32();
                        record.originalToCandidate = Compare(originalSamples, candidateSamples, decoded.width);
                        record.importedBaselineToCandidate = Compare(baselineSamples, candidateSamples, decoded.width);
                        record.originalToImportedBaseline = Compare(originalSamples, baselineSamples, decoded.width);
                        record.qualityPassed = record.originalToCandidate.psnrDb >= MinimumPsnr &&
                            record.originalToCandidate.meanAbsoluteChannelError <= MaximumMeanError &&
                            record.importedBaselineToCandidate.psnrDb >= MinimumPsnr &&
                            record.importedBaselineToCandidate.meanAbsoluteChannelError <= MaximumMeanError &&
                            record.candidateRuntimeBytes <= record.baselineRuntimeBytes * .5d;
                        if (!record.qualityPassed) failures.Add(resource + ": PSNR=" + record.originalToCandidate.psnrDb.ToString("F3") +
                            " dB; mean=" + record.originalToCandidate.meanAbsoluteChannelError.ToString("F3") +
                            "; memory reduction=" + record.runtimeReduction.ToString("P2"));
                        if (index == 0 || index == 12)
                        {
                            string name = Path.GetFileName(resource);
                            File.WriteAllBytes(Path.Combine(destination, name + "_original-gpu.png"), originalPixels.EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(destination, name + "_candidate-gpu.png"), candidatePixels.EncodeToPNG());
                        }
                    }
                    catch (Exception exception)
                    {
                        record.error = exception.ToString();
                        throw;
                    }
                    finally
                    {
                        listed.Remove(candidatePath);
                        padded.Remove(candidatePath);
                        if (AssetDatabase.LoadMainAssetAtPath(candidatePath) != null || File.Exists(candidatePath)) AssetDatabase.DeleteAsset(candidatePath);
                        if (AssetDatabase.LoadMainAssetAtPath(baselinePath) != null || File.Exists(baselinePath)) AssetDatabase.DeleteAsset(baselinePath);
                        foreach (Texture2D texture in new[] { decoded, originalPixels, baselinePixels, candidatePixels })
                            if (texture != null) Object.DestroyImmediate(texture);
                        record.sourcePngUnchanged = Hash(originalPath) == record.sourceSha256;
                        record.sourceMetaUnchanged = Hash(originalPath + ".meta") == record.sourceMetaSha256;
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        Assert.That(record.sourcePngUnchanged && record.sourceMetaUnchanged, Is.True, "Candidate testing must leave borrowed production inputs unchanged.");
                    }
                }
            }
            finally
            {
                listed.Remove(folder + "/candidate.png");
                padded.Remove(folder + "/candidate.png");
                AssetDatabase.DeleteAsset(folder);
                report.temporaryAssetsRemoved = !Directory.Exists(folder) && !File.Exists(folder + ".meta");
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            Assert.That(report.temporaryAssetsRemoved, Is.True);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static void CopySource(string source, string destination)
        {
            File.Copy(source, destination, false);
            string metadata = File.ReadAllText(source + ".meta");
            metadata = Regex.Replace(metadata, @"(?m)^guid: [a-fA-F0-9]+", "guid: " + Guid.NewGuid().ToString("N"));
            File.WriteAllText(destination + ".meta", metadata);
        }

        [Test, Timeout(1800000)]
        public void ArtOptimization10_InstalledResourcesKeepQualifiedPixelsAndNativeMemory()
        {
            if (Environment.GetEnvironmentVariable("ART_OPT_RAM_COMPRESSION") != "installed")
                Assert.Ignore("Set ART_OPT_RAM_COMPRESSION=installed only after qualified production metadata has been installed.");
            Assert.That(Path.GetFullPath(Application.dataPath).Replace('\\', '/').StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase), Is.True);
            AssertIsolatedValidationIdentity();
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string qualifiedPath = Environment.GetEnvironmentVariable("ART_OPT_RAM_QUALIFIED_REPORT");
            Assert.That(qualifiedPath, Is.Not.Null.And.Not.Empty);
            var qualified = JsonUtility.FromJson<Report>(File.ReadAllText(qualifiedPath));
            Assert.That(qualified.scope, Is.EqualTo("all"));
            Assert.That(qualified.alignment, Is.EqualTo("pad"));
            Assert.That(qualified.pages.Count, Is.EqualTo(RamComicCatalog.ResourceCount));
            string destination = Environment.GetEnvironmentVariable("ART_OPT_RAM_REPORT_DIR");
            if (string.IsNullOrWhiteSpace(destination))
                destination = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("COMIC_PAGINATION_CAPTURE_DIR")), "ram-compression-installed");
            destination = Path.GetFullPath(destination);
            Assert.That(destination.Replace('\\', '/').StartsWith("D:/Game/outputs/", StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any(), Is.False);
            Directory.CreateDirectory(destination);
            var report = new Report
            {
                scope = "installed", alignment = "pad", generatedUtc = DateTime.UtcNow.ToString("O"),
                graphicsDevice = SystemInfo.graphicsDeviceName, colorSpace = QualitySettings.activeColorSpace.ToString(),
                policySourceSha256 = Hash("Assets/_Project/Editor/DesktopBackgroundCompressionPolicy.cs"),
                postprocessorSourceSha256 = Hash("Assets/_Project/Editor/DesktopBackgroundCompressionPostprocessor.cs"),
                catalogSourceSha256 = Hash("Assets/_Project/Presentation/Runtime/RamComicCatalog.cs"),
                presentationSourceSha256 = Hash("Assets/_Project/Presentation/Runtime/CoreGameplayRamComicPresentation.cs"),
                temporaryAssetsRemoved = true,
                note = "Production Resources.Load path compared with the PNG at that same source path using displayed source UV. No candidate assets or imports are created. Baseline native memory comes from the preserved qualified candidate report; installed native memory and original-size GPU quality are measured in this run. This is texture allocation, not total GPU memory; no first-play timing claim."
            };
            var failures = new List<string>();
            try
            {
                for (int index = 0; index < RamComicCatalog.ResourceCount; index++)
                {
                    string resource = RamComicCatalog.ImageResource(index / RamComicCatalog.PagesPerSequence, index % RamComicCatalog.PagesPerSequence);
                    Page previous = qualified.pages.Single(page => page.resource == resource);
                    Assert.That(previous.qualityPassed && previous.sourcePngUnchanged && previous.sourceMetaUnchanged, Is.True);
                    string sourcePath = "Assets/_Project/Resources/" + resource + ".png";
                    var record = new Page { resource = resource, sourceSha256 = Hash(sourcePath), sourceMetaSha256 = Hash(sourcePath + ".meta"),
                        baselineRuntimeBytes = previous.baselineRuntimeBytes, baselineFormat = previous.baselineFormat };
                    report.pages.Add(record);
                    Assert.That(record.sourceSha256, Is.EqualTo(previous.sourceSha256));
                    Texture2D decoded = null, raw = null, actual = null;
                    try
                    {
                        var texture = Resources.Load<Texture2D>(resource);
                        Assert.That(texture, Is.Not.Null);
                        Assert.That(texture.format, Is.EqualTo(TextureFormat.BC7));
                        Assert.That(texture.isReadable, Is.False);
                        Assert.That(texture.width, Is.EqualTo(3344));
                        Assert.That(texture.height, Is.EqualTo(1884));
                        record.candidateFormat = texture.format.ToString();
                        record.candidateReadable = texture.isReadable;
                        record.importedWidth = texture.width; record.importedHeight = texture.height;
                        record.candidateRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(texture);
                        record.runtimeReduction = 1d - record.candidateRuntimeBytes / (double)record.baselineRuntimeBytes;
                        decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                        Assert.That(decoded.LoadImage(File.ReadAllBytes(sourcePath), true), Is.True);
                        decoded.filterMode = FilterMode.Bilinear; decoded.wrapMode = TextureWrapMode.Clamp;
                        record.sourceWidth = decoded.width; record.sourceHeight = decoded.height;
                        raw = Readback(decoded, decoded.width, decoded.height);
                        actual = Readback(texture, decoded.width, decoded.height, RamComicCatalog.ImageUvRect(texture).size);
                        record.originalToCandidate = Compare(raw.GetPixels32(), actual.GetPixels32(), decoded.width);
                        record.qualityPassed = record.originalToCandidate.psnrDb >= MinimumPsnr &&
                            record.originalToCandidate.meanAbsoluteChannelError <= MaximumMeanError &&
                            record.candidateRuntimeBytes <= record.baselineRuntimeBytes * .5d;
                        if (!record.qualityPassed) failures.Add(resource);
                        if (index == 0 || index == 12)
                            File.WriteAllBytes(Path.Combine(destination, Path.GetFileName(resource) + "_installed-gpu.png"), actual.EncodeToPNG());
                    }
                    catch (Exception exception) { record.error = exception.ToString(); throw; }
                    finally
                    {
                        foreach (Texture2D texture in new[] { decoded, raw, actual }) if (texture != null) Object.DestroyImmediate(texture);
                        record.sourcePngUnchanged = Hash(sourcePath) == record.sourceSha256;
                        record.sourceMetaUnchanged = Hash(sourcePath + ".meta") == record.sourceMetaSha256;
                        Assert.That(record.sourcePngUnchanged && record.sourceMetaUnchanged, Is.True);
                    }
                }
            }
            finally { File.WriteAllText(Path.Combine(destination, "report.json"), JsonUtility.ToJson(report, true)); }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
        }

        private static Texture2D Readback(Texture source, int width, int height, Vector2? scale = null)
        {
            RenderTexture previous = RenderTexture.active;
            bool oldSrgb = GL.sRGBWrite;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, target, scale ?? Vector2.one, Vector2.zero);
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(false, false);
                return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
            finally { RenderTexture.active = previous; GL.sRGBWrite = oldSrgb; RenderTexture.ReleaseTemporary(target); }
        }

        private static void AssertIsolatedValidationIdentity()
        {
            bool comicValidation = Application.companyName == "CodexCardSupplyValidation" &&
                Application.productName == "CardSupplyFrames20261021";
            bool releaseValidation = Application.companyName == "ShatteredSailValidation" &&
                Application.productName == "CodeStructureReleaseValidation";
            Assert.That(comicValidation || releaseValidation, Is.True,
                "Ram compression validation requires one of the two explicit isolated storage identities.");
        }

        private static Difference Compare(Color32[] baseline, Color32[] candidate, int width)
        {
            var result = new Difference();
            var histogram = new long[256];
            var edgeHistogram = new long[256];
            long absolute = 0, squared = 0, edgeAbsolute = 0, edgeChannels = 0;
            for (int i = 0; i < baseline.Length; i++)
            {
                Color32 a = baseline[i], b = candidate[i];
                bool edge = false;
                if (i % width > 0)
                {
                    Color32 left = baseline[i - 1];
                    edge = Math.Abs(a.r - left.r) + Math.Abs(a.g - left.g) + Math.Abs(a.b - left.b) > 75;
                }
                if (i >= width)
                {
                    Color32 down = baseline[i - width];
                    edge |= Math.Abs(a.r - down.r) + Math.Abs(a.g - down.g) + Math.Abs(a.b - down.b) > 75;
                }
                for (int channel = 0; channel < 3; channel++)
                {
                    int delta = Math.Abs(channel == 0 ? a.r - b.r : channel == 1 ? a.g - b.g : a.b - b.b);
                    absolute += delta; squared += delta * delta; histogram[delta]++;
                    result.peakChannelError = Math.Max(result.peakChannelError, delta);
                    if (edge) { edgeAbsolute += delta; edgeChannels++; edgeHistogram[delta]++; result.edgePeakChannelError = Math.Max(result.edgePeakChannelError, delta); }
                }
            }
            long total = baseline.LongLength * 3;
            result.meanAbsoluteChannelError = absolute / (double)total;
            result.psnrDb = squared == 0 ? 99d : 10d * Math.Log10(255d * 255d / (squared / (double)total));
            result.edgeMeanAbsoluteChannelError = edgeChannels == 0 ? 0 : edgeAbsolute / (double)edgeChannels;
            long accumulated = 0;
            bool got95 = false, got99 = false;
            for (int value = 0; value < histogram.Length; value++)
            {
                accumulated += histogram[value];
                if (!got95 && accumulated >= total * .95d) { result.p95ChannelError = value; got95 = true; }
                if (!got99 && accumulated >= total * .99d) { result.p99ChannelError = value; got99 = true; }
            }
            accumulated = 0; got95 = false; got99 = false;
            for (int value = 0; value < edgeHistogram.Length && edgeChannels > 0; value++)
            {
                accumulated += edgeHistogram[value];
                if (!got95 && accumulated >= edgeChannels * .95d) { result.edgeP95ChannelError = value; got95 = true; }
                if (!got99 && accumulated >= edgeChannels * .99d) { result.edgeP99ChannelError = value; got99 = true; }
            }
            return result;
        }
    }
}
