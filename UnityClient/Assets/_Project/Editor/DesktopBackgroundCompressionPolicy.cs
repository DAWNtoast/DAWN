using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Desktop-only, explicit full-background policy. No startup callback scans
    /// or reimports assets; existing art is processed on its next import or by
    /// the explicit ApplyAll command. Source PNGs are never rewritten.
    /// </summary>
    public static class DesktopBackgroundCompressionPolicy
    {
        private const string Root = "Assets/_Project/Resources/";
        private static readonly string[] paths = CreatePaths();
        private static readonly HashSet<string> pathSet = new HashSet<string>(paths, StringComparer.Ordinal);
        public static IReadOnlyList<string> AssetPaths { get; } = Array.AsReadOnly(paths);

        private static string[] CreatePaths()
        {
            var result = new List<string> { Root + "MainMenu/main_menu_background.png" };
            string[] ports = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
            foreach (string port in ports)
            {
                result.Add(Root + "PortArt/port_hub_" + port + "_v3.png");
                result.Add(Root + "PortFacilities/Regional/Tavern/tavern_" + port + "_v1.png");
                result.Add(Root + "PortFacilities/Regional/ComponentShop/component_shop_" + port + "_v1.png");
                result.Add(Root + "PortFacilities/Regional/Maintenance/maintenance_" + port + "_side_v2.png");
            }
            // The current generic backgrounds in PortFacilityArtCatalog, not
            // every historical image or prop under the PortFacilities folder.
            string[] facilities = { "warehouse_background", "shipyard_water_gallery_v3", "tavern_background_seated_v2",
                "component_shop_background_v2", "card_workshop_background", "dispatch_room_integrated_v3",
                "treasure_room_boards_v2", "port_investment_background_v2", "voyage_background", "Market/market_background_v1" };
            foreach (string facility in facilities) result.Add(Root + "PortFacilities/" + facility + ".png");
            string[] naval = { "beach_cove", "coral_reef", "misty_sea", "open_ocean", "rocky_strait", "storm_sea" };
            foreach (string environment in naval) result.Add(Root + "BattleEnvironments/battle_" + environment + "_v1.png");
            string[] terrains = { "open", "coast", "strait", "reef", "fog", "storm" };
            string[] times = { "day", "dusk", "night" };
            foreach (string terrain in terrains)
                foreach (string time in times)
                    result.Add(Root + "BoardingBattle/Backgrounds/boarding_" + terrain + "_" + time + "_v1.png");
            // Full ocean panoramas only; transparent navigation scenery remains untouched.
            result.Add(Root + "NavigationLookout/lookout_sea_v1.png");
            foreach (string weather in new[] { "clear", "fog", "storm" })
                foreach (string time in times)
                    if (weather != "clear" || time != "day")
                        result.Add(Root + "NavigationLookout/Environments/lookout_" + weather + "_" + time + "_v2.png");
            return result.ToArray();
        }

        public static bool IsListed(string assetPath) => assetPath != null && pathSet.Contains(assetPath);

        public static bool SupportsTarget(BuildTarget target) => target == BuildTarget.StandaloneWindows ||
            target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneOSX || target == BuildTarget.StandaloneLinux64;

        public static bool IsEligible(string assetPath, BuildTarget target, bool readable,
            TextureImporterType type, TextureImporterShape shape) => IsListed(assetPath) && SupportsTarget(target) &&
            !readable && type == TextureImporterType.Default && shape == TextureImporterShape.Texture2D;

        public static int AlignedDimension(int dimension)
        {
            if (dimension < 1 || dimension > int.MaxValue - 2) throw new ArgumentOutOfRangeException(nameof(dimension));
            // Halfway values round up. At least one 4x4 compression block.
            return Math.Max(4, ((dimension + 2) / 4) * 4);
        }

        public static void ConfigureStandaloneSettings(TextureImporterPlatformSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.name = "Standalone";
            settings.overridden = true;
            settings.format = TextureImporterFormat.BC7;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            // Leave maxTextureSize, resize algorithm, alpha handling and every
            // texture-level sampling/readability/mipmap property untouched.
        }

        /// <summary>Pixel-center, clamped bilinear resampling over the whole normalized UV domain.</summary>
        public static Color32[] Resample(Color32[] source, int width, int height, int targetWidth, int targetHeight)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (width < 1 || height < 1 || targetWidth < 1 || targetHeight < 1 ||
                (long)width * height != source.Length || (long)targetWidth * targetHeight > int.MaxValue)
                throw new ArgumentException("Invalid background resampling dimensions.");
            var destination = new Color32[targetWidth * targetHeight];
            if (width == targetWidth && height == targetHeight)
            {
                Array.Copy(source, destination, source.Length);
                return destination;
            }
            for (int y = 0; y < targetHeight; y++)
            {
                float sy = Mathf.Clamp((y + .5f) * height / targetHeight - .5f, 0f, height - 1f);
                int y0 = (int)sy, y1 = Math.Min(y0 + 1, height - 1);
                float fy = sy - y0;
                for (int x = 0; x < targetWidth; x++)
                {
                    float sx = Mathf.Clamp((x + .5f) * width / targetWidth - .5f, 0f, width - 1f);
                    int x0 = (int)sx, x1 = Math.Min(x0 + 1, width - 1);
                    float fx = sx - x0;
                    Color a = Color.LerpUnclamped(source[y0 * width + x0], source[y0 * width + x1], fx);
                    Color b = Color.LerpUnclamped(source[y1 * width + x0], source[y1 * width + x1], fx);
                    destination[y * targetWidth + x] = Color.LerpUnclamped(a, b, fy);
                }
            }
            return destination;
        }

        /// <summary>
        /// Runs on the importer's temporary readable texture, never a runtime
        /// resource. Unity can select RGBA32 from the original NPOT dimensions
        /// before this callback, so explicitly compress the aligned result.
        /// </summary>
        public static void CompressImportedTexture(Texture2D texture, bool mipmapsEnabled)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            int width = AlignedDimension(texture.width), height = AlignedDimension(texture.height);
            if (width != texture.width || height != texture.height)
            {
                Color32[] pixels = Resample(texture.GetPixels32(), texture.width, texture.height, width, height);
                if (!texture.Reinitialize(width, height)) throw new InvalidOperationException("Cannot align background texture.");
                texture.SetPixels32(pixels);
                texture.Apply(mipmapsEnabled, false);
            }
            if (texture.format != TextureFormat.BC7)
                EditorUtility.CompressTexture(texture, TextureFormat.BC7, TextureCompressionQuality.Best);
            // Do not call Apply after compression: importer finalization owns the
            // upload and restores the original runtime Read/Write setting.
            if (texture.format != TextureFormat.BC7)
                throw new InvalidOperationException("Aligned background could not be encoded as BC7.");
        }

        [Serializable] private sealed class Audit
        {
            public string generatedUtc, target;
            public string scope = "Explicit whitelist reimport. Source PNG bytes unchanged; desktop BC7; nearest-four whole-image resampling. Build/package size and visual quality require separate validation.";
            public int listed, imported, missing, excluded, failed;
            public List<Record> assets = new List<Record>();
        }

        [Serializable] private sealed class Record
        {
            public string path, status, error, sourceSha256, format, graphicsFormat;
            public int sourceWidth, sourceHeight, width, height, mipmapCount, maxTextureSize;
            public bool readable, sourceUnchanged;
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.DesktopBackgroundCompressionPolicy.ApplyAllFromCommandLine
        /// -desktopBackgroundCompressionReport &lt;new absolute JSON path&gt;
        /// </summary>
        public static void ApplyAllFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-desktopBackgroundCompressionReport");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("A new absolute -desktopBackgroundCompressionReport path is required.");
            ApplyAll(args[index + 1]);
        }

        public static void ApplyAll(string reportPath)
        {
            if (string.IsNullOrEmpty(reportPath) || !Path.IsPathRooted(reportPath)) throw new ArgumentException("Absolute report path required.");
            if (File.Exists(reportPath)) throw new IOException("Refusing to overwrite report: " + reportPath);
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!SupportsTarget(target)) throw new InvalidOperationException("Desktop background compression is not enabled for " + target);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            var audit = new Audit { generatedUtc = DateTime.UtcNow.ToString("O"), target = target.ToString(), listed = paths.Length };
            // Reserve the report first; no resource imports occur on a path error.
            using (var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                try
                {
                    foreach (string path in paths)
                    {
                        var record = new Record { path = path };
                        audit.assets.Add(record);
                        if (!File.Exists(path)) { record.status = "missing-skipped"; audit.missing++; continue; }
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null || !IsEligible(path, target, importer.isReadable, importer.textureType, importer.textureShape))
                        { record.status = "excluded-skipped"; audit.excluded++; continue; }
                        try
                        {
                            record.sourceSha256 = SourceHash(path);
                            importer.GetSourceTextureWidthAndHeight(out record.sourceWidth, out record.sourceHeight);
                            var previous = importer.GetPlatformTextureSettings("Standalone");
                            record.maxTextureSize = previous.overridden ? previous.maxTextureSize : importer.GetDefaultPlatformTextureSettings().maxTextureSize;
                            bool oldReadable = importer.isReadable, oldMips = importer.mipmapEnabled;
                            FilterMode oldFilter = importer.filterMode;
                            TextureWrapMode oldU = importer.wrapModeU, oldV = importer.wrapModeV, oldW = importer.wrapModeW;
                            // Persist the effective override before importing;
                            // callback-only changes may be too late for format
                            // selection in an already prepared import request.
                            var settings = previous.overridden ? previous : importer.GetDefaultPlatformTextureSettings();
                            ConfigureStandaloneSettings(settings);
                            importer.SetPlatformTextureSettings(settings);
                            importer.SaveAndReimport();
                            importer = (TextureImporter)AssetImporter.GetAtPath(path);
                            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                            if (texture == null) throw new InvalidOperationException("Imported texture is missing.");
                            record.width = texture.width; record.height = texture.height;
                            record.format = texture.format.ToString(); record.graphicsFormat = texture.graphicsFormat.ToString();
                            record.mipmapCount = texture.mipmapCount; record.readable = texture.isReadable;
                            record.sourceUnchanged = record.sourceSha256 == SourceHash(path);
                            if (!record.sourceUnchanged || texture.format != TextureFormat.BC7 || texture.width % 4 != 0 || texture.height % 4 != 0)
                                throw new InvalidOperationException("Source bytes, BC7 format, or aligned dimensions failed verification.");
                            if (importer.isReadable != oldReadable || texture.isReadable != oldReadable || importer.mipmapEnabled != oldMips ||
                                (texture.mipmapCount > 1) != oldMips ||
                                importer.filterMode != oldFilter || importer.wrapModeU != oldU || importer.wrapModeV != oldV || importer.wrapModeW != oldW ||
                                importer.GetPlatformTextureSettings("Standalone").maxTextureSize != record.maxTextureSize)
                                throw new InvalidOperationException("Protected import settings changed.");
                            record.status = "passed"; audit.imported++;
                        }
                        catch (Exception error) { record.status = "failed"; record.error = error.ToString(); audit.failed++; }
                    }
                }
                finally
                {
                    byte[] json = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(audit, true));
                    output.Write(json, 0, json.Length); output.Flush(true);
                }
            }
            if (audit.failed > 0) throw new InvalidOperationException("Background compression verification failed for " + audit.failed + " asset(s). See " + reportPath);
            Debug.Log("Desktop background compression: " + audit.imported + " passed, " + audit.missing + " missing, " + audit.excluded + " excluded. " + reportPath);
        }

        private static string SourceHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
