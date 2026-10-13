using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ShatteredSail.Editor
{
    /// <summary>Read-only asset/import and actual full-screen UV resolution audit.</summary>
    public static class ComicResolutionAudit
    {
        [Serializable] private sealed class Report
        {
            public string capturedUtc, buildTarget, quality;
            public int viewportWidth = 1920, viewportHeight = 1080, mipmapLimit;
            public bool verifiedFullHd;
            public List<Record> assets = new List<Record>();
        }

        [Serializable] private sealed class Record
        {
            public string family, resource, path, sourceSha256, format, graphicsFormat, filter, compression;
            public int sourceWidth, sourceHeight, importedWidth, importedHeight, maxSize, mipmapCount, panelCount;
            public bool mipmaps, streaming, standaloneOverride, resized, uncompressed, verifiedFullHd;
            public Rect displayedUv;
            public float effectiveWidth, effectiveHeight, upscaleX, upscaleY, sourcePixelCoveragePercent;
        }

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-comicResolutionReport");
            if (outputIndex < 0 || outputIndex + 1 >= args.Length || !Path.IsPathRooted(args[outputIndex + 1]))
                throw new ArgumentException("Pass -comicResolutionReport NEW_ABSOLUTE_JSON_PATH.");
            string output = args[outputIndex + 1];
            if (File.Exists(output)) throw new IOException("Report already exists: " + output);
            var report = new Report {
                capturedUtc = DateTime.UtcNow.ToString("O"), buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], mipmapLimit = QualitySettings.globalTextureMipmapLimit
            };
            // Inspect the pages actually used by runtime catalogs. Retired four-
            // panel atlases must never masquerade as playable full-HD artwork.
            for (int sequence = 0; sequence < RamComicCatalog.SequenceCount; sequence++)
                for (int page = 0; page < RamComicCatalog.PagesPerSequence; page++)
                    report.assets.Add(Inspect(RamComicCatalog.ImageResource(sequence, page), "ram"));
            foreach (TreasureStoryChapter chapter in CoreTreasureStoryCatalog.Chapters)
                foreach (string resource in chapter.ImageResources)
                    report.assets.Add(Inspect(resource, "treasure"));
            foreach (OpeningComicPage page in OpeningComicCatalog.Pages)
                report.assets.Add(Inspect(page.ImageResource, "opening"));
            report.verifiedFullHd = report.assets.Count == 26 && report.assets.All(record => record.verifiedFullHd);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (!report.verifiedFullHd)
                throw new InvalidOperationException("Comic resolution/import audit failed: " +
                    string.Join(", ", report.assets.Where(record => !record.verifiedFullHd).Select(record => record.resource)) +
                    ". See " + output);
            Debug.Log("COMIC_RESOLUTION_AUDIT_OK assets=" + report.assets.Count + " displayedPages=" + report.assets.Sum(asset => asset.panelCount));
        }

        private static Record Inspect(string resource, string family)
        {
            string path = "Assets/_Project/Resources/" + resource + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || texture == null) throw new InvalidOperationException("Missing imported comic: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var settings = importer.GetPlatformTextureSettings("Standalone");
            var active = settings.overridden ? settings : importer.GetDefaultPlatformTextureSettings();
            var record = new Record {
                family = family, resource = resource, path = path, sourceWidth = width, sourceHeight = height,
                importedWidth = texture.width, importedHeight = texture.height, format = texture.format.ToString(),
                graphicsFormat = texture.graphicsFormat.ToString(), filter = texture.filterMode.ToString(),
                compression = active.textureCompression.ToString(), maxSize = active.maxTextureSize,
                mipmaps = importer.mipmapEnabled, streaming = importer.streamingMipmaps, mipmapCount = texture.mipmapCount,
                standaloneOverride = settings.overridden, resized = width != texture.width || height != texture.height,
                uncompressed = active.textureCompression == TextureImporterCompression.Uncompressed &&
                    !GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat),
                panelCount = 1
            };
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) record.sourceSha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            var host = new GameObject("Disposable Comic Resolution Sample", typeof(RectTransform));
            try
            {
                ComicPageGraphic page = host.AddComponent<ComicPageGraphic>();
                page.rectTransform.sizeDelta = new Vector2(1920f, 1080f);
                page.texture = texture;
                page.SetPanel(new Rect(0f, 0f, 1f, 1f), family == "ram");
                record.displayedUv = page.uvRect;
                record.effectiveWidth = texture.width * page.uvRect.width;
                record.effectiveHeight = texture.height * page.uvRect.height;
                record.upscaleX = 1920f / record.effectiveWidth;
                record.upscaleY = 1080f / record.effectiveHeight;
                record.sourcePixelCoveragePercent = 100f / (record.upscaleX * record.upscaleY);
                record.verifiedFullHd = !record.resized && record.uncompressed &&
                    !record.mipmaps && record.mipmapCount == 1 &&
                    record.effectiveWidth >= 1919.99f && record.effectiveHeight >= 1079.99f;
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            return record;
        }
    }
}
