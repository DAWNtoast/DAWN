#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class StartupMemoryAuditBuilder
    {
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-startupMemoryBuildOutput");
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1])) throw new ArgumentException("Supply -startupMemoryBuildOutput with a fresh absolute .exe.");
            string output = Path.GetFullPath(args[at + 1]);
            string directory = Path.GetDirectoryName(output);
            if (!string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase) || !output.StartsWith("D:\\Game\\Artifacts\\StartupMemory20261011\\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must be an .exe in this audit directory.");
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0) throw new IOException("Use an empty diagnostic build directory.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!project.StartsWith("D:\\Game\\tmp\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Build only from an isolated tmp project.");
            string[] scenes = { "Assets/_Project/Scenes/MainMenu.unity", "Assets/_Project/Scenes/CoreGameplayDemo.unity" };
            foreach (string scene in scenes) if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new FileNotFoundException(scene);
            string company = PlayerSettings.companyName, product = PlayerSettings.productName, version = PlayerSettings.bundleVersion;
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var fullscreen = PlayerSettings.fullScreenMode; bool resizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.companyName = "ShatteredSailStartupAudit";
                PlayerSettings.productName = "StartupMemory20261011";
                PlayerSettings.bundleVersion = "0.3.20261011-memory-audit";
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(directory);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC });
                var summary = report.summary;
                File.WriteAllText(Path.Combine(directory, "build-report.txt"), "Result: " + summary.result + "\nUnity: " + Application.unityVersion +
                    "\nWindows x64 / Mono / non-Development\nIdentity: ShatteredSailStartupAudit / StartupMemory20261011\nDiagnostic only, not delivery package\nOutput: " + summary.outputPath +
                    "\nBytes: " + summary.totalSize + "\nSeconds: " + summary.totalTime.TotalSeconds + "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings + "\nScenes:\n" + string.Join("\n", scenes));
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0) throw new BuildFailedException("Memory diagnostic build failed.");
            }
            finally
            {
                PlayerSettings.companyName = company; PlayerSettings.productName = product; PlayerSettings.bundleVersion = version;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend); PlayerSettings.fullScreenMode = fullscreen; PlayerSettings.resizableWindow = resizable;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
