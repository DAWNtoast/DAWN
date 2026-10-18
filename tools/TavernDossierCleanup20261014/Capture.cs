using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Isolated, unsaved editor fixtures only. No recruitment or save writes.
    public static class TavernDossierCleanupCapture
    {
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the isolated validation project.");
            string output = Environment.GetEnvironmentVariable("TAVERN_CLEANUP_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Missing output path.");
            Directory.CreateDirectory(output);
            Type fixtureType = typeof(CrewDossierAcceptance).GetNestedType("Preview", BindingFlags.NonPublic);
            MethodInfo save = typeof(CrewDossierAcceptance).GetMethod("SaveFrame", BindingFlags.Static | BindingFlags.NonPublic);
            string[] removed = { "Previous Tavern Dossier Page", "Next Tavern Dossier Page", "Tavern Dossier Page Counter", "Tavern Special Card Growth" };
            int captures = 0;
            foreach (int profession in new[] { 0, 2 })
            {
                object fixture = Activator.CreateInstance(fixtureType, new object[] { CoreGameplayPreviewPort.BrokenAnchor });
                try
                {
                    fixtureType.GetMethod("SelectCrew").Invoke(fixture, new object[] { profession });
                    Transform root = (Transform)fixtureType.GetProperty("Root").GetValue(fixture);
                    foreach (string page in new[] { "Background", "Story", "Skills", "SpecialCards" })
                    {
                        fixtureType.GetMethod("SelectPage").Invoke(fixture, new object[] { page });
                        foreach (string name in removed)
                            if (root.GetComponentsInChildren<Transform>(true).Any(item => item.name == name))
                                throw new InvalidOperationException("Removed content remains: " + name);
                        if (!root.GetComponentsInChildren<Button>(false).Any(button => button.name == "Recruit Tavern Crew " + profession))
                            throw new InvalidOperationException("Recruitment action was removed.");
                        fixtureType.GetMethod("RenderStableFrame").Invoke(fixture, null);
                        RenderTexture target = (RenderTexture)fixtureType.GetProperty("Target").GetValue(fixture);
                        save.Invoke(null, new object[] { target, Path.Combine(output, (profession == 0 ? "navigator_" : "doctor_") + page + ".png") });
                        captures++;
                    }
                }
                finally { ((IDisposable)fixture).Dispose(); }
            }
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: " + captures + " production UI captures; removed controls absent; all four tabs and crew action remain.");
            Debug.Log("TAVERN_DOSSIER_CLEANUP_OK: " + captures);
        }
    }
}
