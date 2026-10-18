using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    // Run only in the isolated release project to repair its cached asset imports.
    public static class NavalReleaseFrameImport
    {
        public static void Run()
        {
            const string root = "Assets/_Project/Resources/CardSchools/Frames/Integrated/";
            const string reportPath = "D:/Game/Artifacts/NavalAttackRangeRelease20261015/FrameRepair/import-report.txt";
            var report = new StringBuilder();
            foreach (string name in new[] { "frame-arcane-powder", "frame-neutral-cannonball" })
            {
                string path = root + name + ".png";
                report.AppendLine("Before: " + path + " / " + AssetDatabase.GetMainAssetTypeAtPath(path));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            int count = 0;
            foreach (string file in Directory.GetFiles(root, "*.png"))
            {
                string path = file.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Texture2D resource = Resources.Load<Texture2D>("CardSchools/Frames/Integrated/" + name);
                report.AppendLine(name + ": " + (asset == null ? "MISSING" : asset.width + "x" + asset.height) + ", Resource=" + (resource != null));
                if (asset == null || resource == null || asset != resource)
                {
                    File.WriteAllText(reportPath, "FAIL\n" + report);
                    throw new InvalidOperationException("Card frame import incomplete: " + path);
                }
                count++;
            }
            if (count != 36) throw new InvalidOperationException("Expected 36 integrated frames, got " + count);
            File.WriteAllText(reportPath, "PASS\n" + report);
            Debug.Log("NAVAL_RELEASE_FRAME_IMPORT PASS: " + count);
        }
    }
}
