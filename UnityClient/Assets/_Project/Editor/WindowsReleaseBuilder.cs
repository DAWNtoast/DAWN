using UnityEditor;

namespace ShatteredSail.Editor
{
    /// <summary>Explicit player scenes; UI preview and sample scenes never enter this build.</summary>
    public static class WindowsReleaseBuilder
    {
        public static void BuildFromCommandLine()
        {
            DesktopReleaseBuilder.BuildFromCommandLine(BuildTarget.StandaloneWindows64);
        }
    }
}
