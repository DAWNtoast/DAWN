using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShatteredSail.Editor
{
    /// <summary>Shared release policy for desktop players, including their existing save identity.</summary>
    internal static class DesktopReleaseBuilder
    {
        private const string DefaultVersion = "0.3.20261015.3";
        private static readonly string[] Scenes = {
            "Assets/_Project/Scenes/MainMenu.unity",
            "Assets/_Project/Scenes/CoreGameplayDemo.unity"
        };

        internal static void BuildFromCommandLine(BuildTarget target)
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = ValidateOutput(Argument(args, "-releaseOutput"), target);
            string version = ResolveVersion(args);
            bool mac = target == BuildTarget.StandaloneOSX;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Install " + (mac ? "macOS" : "Windows") + " Build Support (Mono) for this Unity editor first.");

            // Unity can silently skip scripts whose metadata has an invalid GUID.
            foreach (string meta in Directory.GetFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories))
                if (!Regex.IsMatch(File.ReadAllText(meta), @"(?m)^guid: [a-fA-F0-9]{32}\r?$"))
                    throw new BuildFailedException("Invalid asset metadata: " + meta);
            foreach (string scene in Scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new FileNotFoundException("Required release scene is missing.", scene);

            string previousCompany = PlayerSettings.companyName;
            string previousProduct = PlayerSettings.productName;
            string previousVersion = PlayerSettings.bundleVersion;
            bool previousResizable = PlayerSettings.resizableWindow;
            FullScreenMode previousFullscreen = PlayerSettings.fullScreenMode;
            ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            string platform = mac ? BuildPipeline.GetBuildTargetName(target) : null;
            string previousArchitecture = mac ? EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture") : null;
            try
            {
                // Keep the shipped storage identity so existing saves remain visible.
                // The game title and executable/app filename are 第七港口.
                PlayerSettings.companyName = "ShatteredSail";
                PlayerSettings.productName = "Shattered Sail";
                PlayerSettings.bundleVersion = version;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                if (mac)
                {
                    string universal = OSArchitecture.x64ARM64.ToString().ToLowerInvariant();
                    EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", universal);
                    if (!string.Equals(EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture"), universal, StringComparison.OrdinalIgnoreCase))
                        throw new BuildFailedException("Could not select the macOS Intel + Apple silicon architecture.");
                }

                string outputDirectory = Path.GetDirectoryName(output);
                Directory.CreateDirectory(outputDirectory);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = Scenes,
                    locationPathName = output,
                    target = target,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
                });
                BuildSummary summary = report.summary;
                string result = "Result: " + summary.result + "\nUnity: " + Application.unityVersion +
                    "\nVersion: " + PlayerSettings.bundleVersion + "\nPlatform: " +
                    (mac ? "macOS Universal (x86_64 + arm64) / Mono" : "Windows x86_64 / Mono") +
                    "\nGame title: 第七港口" +
                    "\nProduct: " + PlayerSettings.productName + "\nOutput: " + summary.outputPath +
                    "\nBytes: " + summary.totalSize + "\nSeconds: " + summary.totalTime.TotalSeconds.ToString(CultureInfo.InvariantCulture) +
                    "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings +
                    "\nScenes:\n" + string.Join("\n", Scenes) + "\n";
                File.WriteAllText(Path.Combine(outputDirectory, "build-report.txt"), result);
                Debug.Log((mac ? "MAC_RELEASE_BUILD_REPORT\n" : "RELEASE_BUILD_REPORT\n") + result);
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new BuildFailedException((mac ? "macOS" : "Windows") + " player build did not pass: " + summary.result);
            }
            finally
            {
                PlayerSettings.companyName = previousCompany;
                PlayerSettings.productName = previousProduct;
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.resizableWindow = previousResizable;
                PlayerSettings.fullScreenMode = previousFullscreen;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                if (mac) EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", previousArchitecture);
                AssetDatabase.SaveAssets();
            }
        }

        private static string ResolveVersion(string[] args)
        {
            string version = Argument(args, "-releaseVersion") ?? DefaultVersion;
            if (!Regex.IsMatch(version, @"\A[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\z"))
                throw new ArgumentException("Supply -releaseVersion as three or four dot-separated numeric components.");
            return version;
        }

        private static string ValidateOutput(string output, BuildTarget target)
        {
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneOSX)
                throw new ArgumentException("Only Windows x86_64 and macOS desktop releases are supported.", nameof(target));
            string extension = target == BuildTarget.StandaloneOSX ? ".app" : ".exe";
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output) ||
                !string.Equals(Path.GetExtension(output), extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Supply -releaseOutput with an absolute, new " + extension + " path.");
            string fullPath = Path.GetFullPath(output);
            // Drive-relative paths (C:player.exe) and current-drive roots (\player.exe)
            // are rooted on Windows, but still depend on the working directory.
            if (!string.Equals(Path.GetPathRoot(output).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), Path.GetPathRoot(fullPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Supply -releaseOutput with a fully qualified absolute path.");
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
                throw new IOException("Refusing to overwrite an existing player: " + fullPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (File.Exists(directory) || (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length > 0))
                throw new IOException("Use a new or empty build directory to avoid mixing player versions: " + directory);
            return fullPath;
        }

        private static string Argument(string[] args, string key)
        {
            string value = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], key, StringComparison.Ordinal)) continue;
                if (value != null) throw new ArgumentException("Duplicate release argument: " + key);
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("Missing value for release argument: " + key);
                value = args[++i];
            }
            return value;
        }
    }
}
