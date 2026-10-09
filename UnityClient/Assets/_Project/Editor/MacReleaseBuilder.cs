using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShatteredSail.Editor
{
    /// <summary>Builds the same release scenes for macOS when the Unity editor has Mac support installed.</summary>
    public static class MacReleaseBuilder
    {
        public static void BuildFromCommandLine()
        {
            string output = Argument("-releaseOutput");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output) ||
                !string.Equals(Path.GetExtension(output), ".app", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Supply -releaseOutput with an absolute, new .app path.");
            if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0)
                throw new IOException("Refusing to overwrite a non-empty macOS app bundle: " + output);
            string outputDirectory = Path.GetDirectoryName(output);
            if (Directory.Exists(outputDirectory) && Directory.GetFileSystemEntries(outputDirectory).Length > 0)
                throw new IOException("Use a new or empty build directory to avoid mixing player versions: " + outputDirectory);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new BuildFailedException("Install macOS Build Support (Mono) for this Unity editor first.");

            foreach (string meta in Directory.GetFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories))
                if (!Regex.IsMatch(File.ReadAllText(meta), @"(?m)^guid: [a-fA-F0-9]{32}\r?$"))
                    throw new BuildFailedException("Invalid asset metadata: " + meta);

            string[] scenes = {
                "Assets/_Project/Scenes/MainMenu.unity",
                "Assets/_Project/Scenes/CoreGameplayDemo.unity"
            };
            foreach (string scene in scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new FileNotFoundException("Required release scene is missing.", scene);

            string previousCompany = PlayerSettings.companyName;
            string previousProduct = PlayerSettings.productName;
            string previousVersion = PlayerSettings.bundleVersion;
            bool previousResizable = PlayerSettings.resizableWindow;
            FullScreenMode previousFullscreen = PlayerSettings.fullScreenMode;
            ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            string platform = BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);
            string previousArchitecture = EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture");
            try
            {
                PlayerSettings.companyName = "ShatteredSail";
                // Preserve the storage identity used by existing player saves.
                // The game title and release app filename are 第七港口.
                PlayerSettings.productName = "Shattered Sail";
                PlayerSettings.bundleVersion = "0.3.20261009";
                PlayerSettings.resizableWindow = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                string universal = OSArchitecture.x64ARM64.ToString().ToLowerInvariant();
                EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", universal);
                if (!string.Equals(EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture"), universal, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Could not select the macOS Intel + Apple silicon architecture.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneOSX,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
                });
                BuildSummary summary = report.summary;
                string result = "Result: " + summary.result + "\nUnity: " + Application.unityVersion +
                    "\nVersion: " + PlayerSettings.bundleVersion + "\nPlatform: macOS Universal (x86_64 + arm64) / Mono" +
                    "\nGame title: 第七港口" +
                    "\nProduct: " + PlayerSettings.productName + "\nOutput: " + summary.outputPath +
                    "\nBytes: " + summary.totalSize + "\nSeconds: " + summary.totalTime.TotalSeconds +
                    "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings +
                    "\nScenes:\n" + string.Join("\n", scenes) + "\n";
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-report.txt"), result);
                Debug.Log("MAC_RELEASE_BUILD_REPORT\n" + result);
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new BuildFailedException("macOS player build did not pass: " + summary.result);
            }
            finally
            {
                PlayerSettings.companyName = previousCompany;
                PlayerSettings.productName = previousProduct;
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.resizableWindow = previousResizable;
                PlayerSettings.fullScreenMode = previousFullscreen;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", previousArchitecture);
                AssetDatabase.SaveAssets();
            }
        }

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
