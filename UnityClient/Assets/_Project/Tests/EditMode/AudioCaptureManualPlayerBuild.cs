#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;

[assembly: TestPlayerBuildModifier(typeof(ShatteredSail.Core.Tests.AudioCaptureManualPlayerBuild))]
[assembly: PostBuildCleanup(typeof(ShatteredSail.Core.Tests.AudioCaptureManualPlayerBuild))]

namespace ShatteredSail.Core.Tests
{
    /// <summary>
    /// Opt-in test-player launch split. Normal tests and release builds are unchanged.
    /// The opt-in Player callback writes results without an Editor connection.
    /// </summary>
    public sealed class AudioCaptureManualPlayerBuild : ITestPlayerBuildModifier,
        IPostprocessBuildWithReport, IPostBuildCleanup
    {
        private static string expectedPlayerPath;
        private static string readyPath;
        private static BuildReport completedBuild;

        public int callbackOrder => int.MaxValue;

        [Serializable]
        private sealed class BuildReady
        {
            public int schema = 1;
            public int editorPid;
            public long editorStartUtcTicks;
            public string playerPath, platform, builtUtc;
        }

        public BuildPlayerOptions ModifyOptions(BuildPlayerOptions options)
        {
            if (!Requested()) return options;
            ValidateAndRemember(options.target, options.locationPathName);
            options.options &= ~(BuildOptions.AutoRunPlayer | BuildOptions.ConnectToHost | BuildOptions.WaitForPlayerConnection);
            return options;
        }

#if UNITY_6000_1_OR_NEWER
        public BuildPlayerWithProfileOptions ModifyOptions(BuildPlayerWithProfileOptions options)
        {
            if (!Requested()) return options;
            // UTF also invokes this overload for its null-profile companion options.
            // Do not create a profile or switch the build route.
            options.options &= ~(BuildOptions.AutoRunPlayer | BuildOptions.ConnectToHost | BuildOptions.WaitForPlayerConnection);
            return options;
        }
#endif

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!Requested() || string.IsNullOrEmpty(expectedPlayerPath)) return;
            if (report.summary.platform == BuildTarget.StandaloneWindows64 &&
                string.Equals(Path.GetFullPath(report.summary.outputPath), expectedPlayerPath,
                    StringComparison.OrdinalIgnoreCase)) completedBuild = report;
        }

        public void Cleanup()
        {
            if (!Requested() || string.IsNullOrEmpty(expectedPlayerPath)) return;
            // UTF invokes cleanup after BuildPipeline.BuildPlayer returns, even on a
            // failed build. Never signal readiness from an old or incomplete exe.
            if (completedBuild == null || completedBuild.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Audio capture Player build did not succeed; no ready marker was written.");
            if (!File.Exists(expectedPlayerPath))
                throw new BuildFailedException("Audio capture Player executable is missing: " + expectedPlayerPath);
            using (Process editor = Process.GetCurrentProcess())
            {
                var ready = new BuildReady
                {
                    editorPid = editor.Id,
                    editorStartUtcTicks = editor.StartTime.ToUniversalTime().Ticks,
                    playerPath = expectedPlayerPath,
                    platform = BuildTarget.StandaloneWindows64.ToString(),
                    builtUtc = DateTime.UtcNow.ToString("O"),
                };
                string temporary = readyPath + ".pending";
                // Both paths must be fresh. Atomic publication prevents partial JSON reads.
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(ready, true));
                File.Move(temporary, readyPath);
            }
            UnityEngine.Debug.Log("AUDIO_CAPTURE_PLAYER_BUILD_READY " + readyPath);
            // UTF may exit with code 3 because no Editor-side test callbacks ran.
            // This marker means only a successful build; the Player writes its own XML.
        }

        private static void ValidateAndRemember(BuildTarget target, string playerPath)
        {
            if (target != BuildTarget.StandaloneWindows64 || !HasArgument("-runTests"))
                throw new BuildFailedException("-audioCaptureManualPlayer requires a StandaloneWindows64 test Player build.");
            string marker = ValueAfter("-audioCaptureBuildReady");
            if (string.IsNullOrWhiteSpace(marker) || !Path.IsPathRooted(marker))
                throw new BuildFailedException("Provide an absolute, fresh -audioCaptureBuildReady path.");
            marker = Path.GetFullPath(marker);
            if (!Directory.Exists(Path.GetDirectoryName(marker)) || File.Exists(marker) || File.Exists(marker + ".pending"))
                throw new BuildFailedException("Audio capture marker directory must exist and its marker must be new: " + marker);
            expectedPlayerPath = Path.GetFullPath(playerPath);
            readyPath = marker;
            completedBuild = null;
        }

        private static bool Requested() => HasArgument("-audioCaptureManualPlayer");

        private static bool HasArgument(string expected)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ValueAfter(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < args.Length; index++)
                if (string.Equals(args[index], expected, StringComparison.Ordinal)) return args[index + 1];
            return null;
        }
    }
}
#endif
