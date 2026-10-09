#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShatteredSail.Editor
{
    // Copied into the isolated validation project only. Never shipped in the game.
    public static class PlayerPerformanceAuditBuilder
    {
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-playerAuditBuildOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -playerAuditBuildOutput.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!Path.IsPathRooted(args[index + 1]) || Path.GetExtension(output) != ".exe")
                throw new ArgumentException("Use an absolute .exe path.");
            string directory = Path.GetDirectoryName(output);
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0)
                throw new IOException("Refusing to mix build versions: " + directory);
            string previousCompany = PlayerSettings.companyName;
            string previousProduct = PlayerSettings.productName;
            string previousVersion = PlayerSettings.bundleVersion;
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var previousFullscreen = PlayerSettings.fullScreenMode;
            bool previousResizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.companyName = "ShatteredSailAudit";
                PlayerSettings.productName = "Optimization20261008";
                PlayerSettings.bundleVersion = "0.3.20261008-audit";
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(directory);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/MainMenu.unity", "Assets/_Project/Scenes/CoreGameplayDemo.unity" },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
                });
                var summary = report.summary;
                File.WriteAllText(Path.Combine(directory, "build-report.txt"),
                    "Result: " + summary.result + "\nUnity: " + Application.unityVersion +
                    "\nPlatform: Windows x86_64 / Mono / Development\nIdentity: ShatteredSailAudit / Optimization20261008" +
                    "\nPurpose: isolated performance fixture, not a release distribution" +
                    "\nOutput: " + summary.outputPath + "\nBytes: " + summary.totalSize +
                    "\nSeconds: " + summary.totalTime.TotalSeconds + "\nErrors: " + summary.totalErrors +
                    "\nWarnings: " + summary.totalWarnings + "\n");
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new BuildFailedException("Performance Player build failed: " + summary.result);
            }
            finally
            {
                PlayerSettings.companyName = previousCompany;
                PlayerSettings.productName = previousProduct;
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.fullScreenMode = previousFullscreen;
                PlayerSettings.resizableWindow = previousResizable;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
