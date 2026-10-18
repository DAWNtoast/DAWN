using UnityEditor;

namespace ShatteredSail.Editor
{
    /// <summary>Builds the same release scenes for macOS when the Unity editor has Mac support installed.</summary>
    public static class MacReleaseBuilder
    {
        public static void BuildFromCommandLine()
        {
            DesktopReleaseBuilder.BuildFromCommandLine(BuildTarget.StandaloneOSX);
        }
    }
}
