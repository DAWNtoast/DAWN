#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class RuntimeMemory20261104Builder
    {
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-runtimeMemory20261104BuildOutput");
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1])) throw new ArgumentException("Supply one fresh absolute .exe output.");
            string output = Path.GetFullPath(args[at + 1]);
            string directory = Path.GetDirectoryName(output);
            if (Path.GetExtension(output) != ".exe" || !output.StartsWith(Path.GetFullPath("D:/Game/Artifacts/RuntimeMemory20261104/"), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use this task's Artifacts directory.");
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0) throw new IOException("Preserve prior builds; use a fresh empty directory.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!project.StartsWith(Path.GetFullPath("D:/Game/tmp/"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Build from an isolated tmp project only.");
            const string scene = "Assets/_Project/Scenes/MainMenu.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new FileNotFoundException(scene);
            string company = PlayerSettings.companyName, product = PlayerSettings.productName, version = PlayerSettings.bundleVersion;
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var fullscreen = PlayerSettings.fullScreenMode; bool resizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.companyName = "ShatteredSailAudit"; PlayerSettings.productName = "RuntimeMemory20261104";
                PlayerSettings.bundleVersion = "0.3.20261104-memory-probe";
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(directory);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC });
                var summary = report.summary;
                File.WriteAllText(Path.Combine(directory, "build-report.txt"), "Result: " + summary.result + "\nUnity: " + Application.unityVersion +
                    "\nWindows x64 / Mono / non-Development\nIdentity: ShatteredSailAudit / RuntimeMemory20261104\nDiagnostic only; not a playable delivery package\nOutput: " + summary.outputPath +
                    "\nBytes: " + summary.totalSize + "\nSeconds: " + summary.totalTime.TotalSeconds + "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings);
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
