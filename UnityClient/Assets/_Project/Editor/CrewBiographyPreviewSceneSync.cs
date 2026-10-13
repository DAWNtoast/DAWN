using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Content;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class CrewBiographyPreviewSceneSync
    {
        // -executeMethod ShatteredSail.Editor.CrewBiographyPreviewSceneSync.SyncFromCommandLine
        public static void SyncFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            GameplayPreviewSceneBuilder.SyncCrewBiographyFromCommandLine();
        }

        // Also requires -crewDossierCapturePath pointing to a new or empty directory.
        // Graphics must remain enabled for the 36-crew acceptance captures.
        public static void SyncAndCaptureFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            SyncFromCommandLine();
            CrewDossierAcceptance.CaptureFromCommandLine();
        }
    }

    public static partial class GameplayPreviewSceneBuilder
    {
        private static readonly string[] CrewBiographySceneReferences =
        {
            "UI_TavernCrewDetail.unity",
            "UITavernCrewDetail/UITavernCrewDetail_Biography.unity",
            "UITavernCrewDetail/UITavernCrewDetail_Lore.unity",
            "UITavernCrewDetail/UITavernCrewDetail_Skills.unity",
            "UITavern/UITavern_RecruitmentOffer.unity",
            "UITavern/UITavern_ContractSigned.unity",
            "TutorialVoyage/09_RecruitNavigator.unity",
        };

        private sealed class CrewBiographyScenePatch
        {
            public string Reference;
            public string Path;
            public string Before;
            public string After;
            public bool Utf8Bom;
            public int RemovedObjects;
        }

        /// <summary>
        /// Changes only the existing background text, its rectangle and the two removed
        /// background children. No scene is opened or re-saved, preserving authored art,
        /// page selection, callbacks and all unrelated YAML blocks byte for byte.
        /// </summary>
        public static void SyncCrewBiographyFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Crew biography synchronization requires stopped edit mode.");
            MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod("FormatCrewBiography",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (formatter == null) throw new MissingMethodException("CoreGameplayDemoController.FormatCrewBiography");

            CrewBiographyScenePatch[] patches = CrewBiographySceneReferences
                .Select(reference => PrepareCrewBiographyScenePatch(reference, formatter)).ToArray();
            foreach (CrewBiographyScenePatch patch in patches)
                if (File.ReadAllText(patch.Path) != patch.Before)
                    throw new IOException("Crew preview changed during synchronization preparation: " + patch.Path);

            // All seven patches are validated before backups or scene edits begin.
            string backupRoot = Path.GetFullPath(AbsolutePath("../Artifacts/CrewBiography20261007/scene-backup"));
            foreach (CrewBiographyScenePatch patch in patches.Where(patch => patch.Before != patch.After))
            {
                string backup = Path.Combine(backupRoot, patch.Reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(patch.Path, backup);
            }
            int updated = 0;
            foreach (CrewBiographyScenePatch patch in patches)
            {
                if (patch.Before == patch.After) continue;
                File.WriteAllText(patch.Path, patch.After, new UTF8Encoding(patch.Utf8Bom));
                updated++;
            }
            AssetDatabase.Refresh();
            Debug.Log("CREW_BIOGRAPHY_PREVIEW_SYNC: verified " + patches.Length + "; updated " + updated +
                "; removed " + patches.Sum(patch => patch.RemovedObjects) + " YAML objects. Backup: " + backupRoot);
        }

        private static CrewBiographyScenePatch PrepareCrewBiographyScenePatch(string reference, MethodInfo formatter)
        {
            string path = AbsolutePath("Assets/_Project/Scenes/UIPreviewsForMe/" + reference);
            byte[] bytes = File.ReadAllBytes(path);
            string before = File.ReadAllText(path);
            var blockPattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
            FacilityNavigationYamlObject[] blocks = blockPattern.Matches(before).Cast<Match>()
                .Select(match => new FacilityNavigationYamlObject
                {
                    ClassId = int.Parse(match.Groups[1].Value), Id = match.Groups[2].Value, Value = match.Value,
                }).ToArray();
            try
            {
                var byId = blocks.ToDictionary(block => block.Id, StringComparer.Ordinal);
                FacilityNavigationYamlObject name = FacilityNavigationNamedObject(blocks, "Tavern Crew Detail Name");
                string displayName = ReadFacilityNavigationText(FacilityNavigationComponent(blocks, name.Id, 114, "  m_FontData:"));
                CrewVariantDefinition crew = CrewNarrativeCatalog.AllCrew.Single(candidate => candidate.DisplayName == displayName);
                string biography = (string)formatter.Invoke(null, new object[] { crew.Biography });
                if (biography.Replace("\r", string.Empty).Replace("\n", string.Empty) !=
                    crew.Biography.Replace("\r", string.Empty).Replace("\n", string.Empty))
                    throw new InvalidDataException("Biography formatting changed the catalogue's text: " + crew.Id);

                FacilityNavigationYamlObject page = FacilityNavigationNamedObject(blocks, "Tavern Dossier Background Page");
                FacilityNavigationYamlObject pageRect = FacilityNavigationComponent(blocks, page.Id, 224);
                FacilityNavigationYamlObject body = FacilityNavigationHeaderChild(blocks, pageRect, "Tavern Crew Biography");
                FacilityNavigationYamlObject bodyRect = FacilityNavigationComponent(blocks, body.Id, 224);
                FacilityNavigationYamlObject bodyText = FacilityNavigationComponent(blocks, body.Id, 114, "  m_FontData:");
                var removedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string obsoleteName in new[] { "Tavern Crew Appearance", "Tavern Crew Profile Heading" })
                {
                    FacilityNavigationYamlObject obsolete = blocks.SingleOrDefault(block =>
                        block.ClassId == 1 && block.Read("m_Name") == obsoleteName);
                    if (obsolete == null) continue; // An already synchronized scene is safe to verify again.
                    FacilityNavigationYamlObject obsoleteRect = FacilityNavigationComponent(blocks, obsolete.Id, 224);
                    if (obsoleteRect.Read("m_Father") != pageRect.FileReference || obsoleteRect.Read("m_Children") != "[]")
                        throw new InvalidDataException("Obsolete text is not a leaf of the background page: " + obsoleteName);
                    string[] componentIds = Regex.Matches(obsolete.Value, @"(?m)^  - component: \{fileID: (-?\d+)\}")
                        .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
                    if (!componentIds.Contains(obsoleteRect.Id) || componentIds.Length < 3)
                        throw new InvalidDataException("Incomplete obsolete text component list: " + obsoleteName);
                    foreach (string componentId in componentIds)
                    {
                        if (!byId.TryGetValue(componentId, out FacilityNavigationYamlObject component) ||
                            component.Owner != obsolete.FileReference)
                            throw new InvalidDataException("Unexpected owner of obsolete component " + componentId);
                        removedIds.Add(componentId);
                    }
                    removedIds.Add(obsolete.Id);
                    var childReference = new Regex(@"(?m)^  - \{fileID: " + Regex.Escape(obsoleteRect.Id) + @"\}\r?\n");
                    if (childReference.Matches(pageRect.Value).Count != 1)
                        throw new InvalidDataException("Expected one background child reference for " + obsoleteName);
                    pageRect.Value = childReference.Replace(pageRect.Value, string.Empty);
                }

                bodyRect.Set("m_AnchoredPosition", "{x: 0, y: 0}");
                bodyRect.Set("m_SizeDelta", "{x: 900, y: 220}");
                bodyText.Set("m_Text", CrewBiographyYamlString(biography));
                bodyText.Set("m_FontSize", "28");
                bodyText.Set("m_LineSpacing", "1.25");
                bodyText.Set("m_Color", "{r: 0.20784314, g: 0.13725491, b: 0.08235294, a: 1}");
                bodyText.Set("m_Alignment", "0");
                bodyText.Set("m_BestFit", "0");
                bodyText.Set("m_HorizontalOverflow", "0");
                bodyText.Set("m_VerticalOverflow", "0");

                string after = blockPattern.Replace(before, match => removedIds.Contains(match.Groups[2].Value)
                    ? string.Empty : byId[match.Groups[2].Value].Value);
                foreach (string removedId in removedIds)
                    if (Regex.IsMatch(after, @"\{fileID: " + Regex.Escape(removedId) + @"(?:[,}])"))
                        throw new InvalidDataException("A remaining YAML object references removed object " + removedId);
                return new CrewBiographyScenePatch
                {
                    Reference = reference, Path = path, Before = before, After = after,
                    Utf8Bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf,
                    RemovedObjects = removedIds.Count,
                };
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Unable to synchronize crew biography in " + path, exception);
            }
        }

        private static string CrewBiographyYamlString(string value)
        {
            var result = new StringBuilder("\"");
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '"': result.Append("\\\""); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (character > 127 || char.IsControl(character))
                            result.Append("\\u").Append(((int)character).ToString("X4"));
                        else result.Append(character);
                        break;
                }
            }
            return result.Append('"').ToString();
        }
    }
}
