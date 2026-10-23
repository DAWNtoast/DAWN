using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace ShatteredSail.Editor
{
    public static class RoyalShipyardCapture
    {
        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-royalShipyardOutput");
            string output = key >= 0 ? args[key + 1] : "D:/Game/Artifacts/RoyalShipyard20261022/review02";
            if (Directory.Exists(output) && Directory.GetFiles(output, "*.png").Length != 0)
                throw new IOException("Use a fresh screenshot output directory.");
            Directory.CreateDirectory(output);
            var capture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (int ship in new[] { 0, 4, 7, 11 })
            foreach (int width in ship == 11 ? new[] { 1920, 1280 } : new[] { 1920 })
            {
                string stem = "Shipyard_" + ship + "_" + width;
                var frame = new UiAuditReadOnlyCapture.MenuFrame { state = "Shipyard", variant = "RoyalShipyard_" + ship,
                    width = width, height = width * 9 / 16, file = stem + ".png" };
                capture.Invoke(null, new object[] { output, frame });
                File.WriteAllText(Path.Combine(output, stem + ".json"), JsonUtility.ToJson(frame, true));
                Debug.Log("ROYAL_SHIPYARD_FRAME_OK " + frame.file);
            }
        }
    }
}
