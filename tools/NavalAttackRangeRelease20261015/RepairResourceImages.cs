using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class NavalReleaseImageImport
    {
        public static void Run()
        {
            const string root = "Assets/_Project/Resources/";
            const string reportPath = "D:/Game/Artifacts/NavalAttackRangeRelease20261015/ImageRepair/import-report.txt";
            var report = new StringBuilder();
            var paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .OrderBy(path => path).ToArray();
            int repaired = 0, failed = 0;
            foreach (string path in paths)
            {
                string resourcePath = path.Substring(root.Length);
                resourcePath = resourcePath.Substring(0, resourcePath.Length - Path.GetExtension(path).Length);
                var originalType = AssetDatabase.GetMainAssetTypeAtPath(path);
                Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Texture2D resource = Resources.Load<Texture2D>(resourcePath);
                if (asset == null || resource == null)
                {
                    report.AppendLine("Repair: " + path + " / " + originalType);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    resource = Resources.Load<Texture2D>(resourcePath);
                    repaired++;
                }
                bool valid = asset != null && resource != null && asset == resource;
                report.AppendLine((valid ? "OK: " : "FAIL: ") + path + " / " + (asset == null ? "missing" : asset.width + "x" + asset.height));
                if (!valid) failed++;
                if (resource != null) Resources.UnloadAsset(resource);
            }
            string summary = (failed == 0 ? "PASS" : "FAIL") + "\nImages: " + paths.Length + "\nRepaired: " + repaired + "\nFailed: " + failed + "\n";
            File.WriteAllText(reportPath, summary + report);
            if (paths.Length != 875 || failed != 0) throw new InvalidOperationException(summary);
            Debug.Log("NAVAL_RELEASE_IMAGE_IMPORT " + summary);
        }
    }
}
