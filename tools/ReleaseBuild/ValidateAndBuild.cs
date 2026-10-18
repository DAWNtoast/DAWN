using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    // Copied into the isolated release project's Editor directory only.
    public static class ReleaseAssetValidation
    {
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-releaseEvidence");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("An absolute release evidence directory is required.");
            string evidence = args[index + 1];
            const string root = "Assets/_Project/Resources/";
            var images = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .OrderBy(path => path).ToArray();
            var detail = new StringBuilder();
            int repaired = 0, failed = 0;
            foreach (string path in images)
            {
                string resourcePath = path.Substring(root.Length);
                resourcePath = resourcePath.Substring(0, resourcePath.Length - Path.GetExtension(path).Length);
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var resource = Resources.Load<Texture2D>(resourcePath);
                if (asset == null || resource == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    resource = Resources.Load<Texture2D>(resourcePath);
                    repaired++;
                }
                bool valid = asset != null && resource != null && asset == resource;
                detail.AppendLine((valid ? "OK: " : "FAIL: ") + path);
                if (!valid) failed++;
                if (resource != null) Resources.UnloadAsset(resource);
            }
            Directory.CreateDirectory(evidence);
            string summary = (failed == 0 && images.Length > 0 ? "PASS" : "FAIL") +
                "\nImages: " + images.Length + "\nRepaired: " + repaired + "\nFailed: " + failed + "\n";
            File.WriteAllText(Path.Combine(evidence, "image-import-report.txt"), summary + detail);
            if (failed != 0 || images.Length == 0) throw new InvalidOperationException(summary);
            WindowsReleaseBuilder.BuildFromCommandLine();
        }
    }
}
