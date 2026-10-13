using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        /// <summary>
        /// Patches the authored header's seven existing YAML objects only. Opening and
        /// re-saving scenes is deliberately unnecessary, so authored art, tutorial gates,
        /// destination controls and all unrelated serialized values stay byte-for-byte intact.
        /// </summary>
        public static void SyncPortFacilityNavigationFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Facility navigation previews require stopped edit mode.");
            FieldInfo namesField = typeof(CoreGameplayDemoController).GetField("PortFacilityNames",
                BindingFlags.Static | BindingFlags.NonPublic);
            string[] names = (string[])namesField.GetValue(null);
            string directory = AbsolutePath("Assets/_Project/Scenes");
            var changes = Directory.GetFiles(directory, "*.unity", SearchOption.AllDirectories)
                .Where(path => !GameplayPreviewSceneBuilder.IsRetiredEditablePreviewPath(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => PrepareFacilityNavigationPatch(path, names)).Where(item => item != null).ToArray();

            // Resolve and validate every target before touching any scene file.
            foreach (FacilityNavigationPatch change in changes)
            {
                if (File.ReadAllText(change.Path) != change.Before)
                    throw new IOException("Scene changed during facility navigation sync: " + change.Path);
            }
            int updated = 0;
            foreach (FacilityNavigationPatch change in changes)
            {
                if (change.Before == change.After) continue;
                string relative = change.Path.Substring(directory.Length + 1);
                string backup = AbsolutePath("Artifacts/FacilityNavigation20261007/scene-backup/" + relative);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(change.Path, backup);
                File.WriteAllText(change.Path, change.After, new UTF8Encoding(false));
                updated++;
            }
            AssetDatabase.Refresh();
            Debug.Log("PORT_FACILITY_NAVIGATION_SYNC: verified " + changes.Length + "; updated " + updated +
                " authored scenes. Only adjacent-facility captions and header layout changed.");
        }

        public static void SyncAndCapturePortFacilityNavigationFromCommandLine()
        {
            SyncPortFacilityNavigationFromCommandLine();
            UIPreviewSceneAcceptance.CapturePortFacilityNavigationFromCommandLine();
        }

        private sealed class FacilityNavigationPatch
        {
            public string Path;
            public string Before;
            public string After;
        }

        private sealed class FacilityNavigationYamlObject
        {
            public string Id;
            public int ClassId;
            public string Value;

            public string Read(string key)
            {
                Match match = Regex.Match(Value, @"(?m)^\s*" + Regex.Escape(key) + @": ([^\r\n]*)");
                if (!match.Success) throw new InvalidDataException("Missing " + key + " on YAML object " + Id);
                return match.Groups[1].Value;
            }

            public void Set(string key, string value)
            {
                var pattern = new Regex(@"(?m)^([ \t]*" + Regex.Escape(key) + @": )[^\r\n]*");
                if (pattern.Matches(Value).Count != 1)
                    throw new InvalidDataException("Expected exactly one " + key + " on YAML object " + Id);
                Value = pattern.Replace(Value, match => match.Groups[1].Value + value);
            }

            public string Owner => Read("m_GameObject");
            public string FileReference => "{fileID: " + Id + "}";
        }

        private static FacilityNavigationPatch PrepareFacilityNavigationPatch(string path, string[] names)
        {
            string before = File.ReadAllText(path);
            if (!before.Contains("  m_Name: Previous Port Facility")) return null;
            var blockPattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
            var blocks = blockPattern.Matches(before).Cast<Match>().Select(match => new FacilityNavigationYamlObject
            {
                ClassId = int.Parse(match.Groups[1].Value), Id = match.Groups[2].Value, Value = match.Value,
            }).ToArray();
            try
            {
                FacilityNavigationYamlObject header = FacilityNavigationNamedObject(blocks, "Port Facility Header");
                FacilityNavigationYamlObject headerRect = FacilityNavigationComponent(blocks, header.Id, 224);
                FacilityNavigationYamlObject title = FacilityNavigationHeaderChild(blocks, headerRect, "Port Facility Title");
                FacilityNavigationYamlObject port = FacilityNavigationHeaderChild(blocks, headerRect, "Port Facility Port");
                string currentTitle = ReadFacilityNavigationText(FacilityNavigationComponent(blocks, title.Id, 114, "  m_FontData:"));
                string currentPort = ReadFacilityNavigationText(FacilityNavigationComponent(blocks, port.Id, 114, "  m_FontData:"));
                int index = Array.FindIndex(names, name => name == currentTitle);
                if (index < 0)
                    index = Array.FindIndex(names, name => PortFacilityArtCatalog.RegionalFacilityTitle(
                        Array.IndexOf(names, name), currentPort, name) == currentTitle);
                if (index < 0) throw new InvalidDataException("Unknown authored facility title: " + currentTitle);

                int previousIndex = (index + names.Length - 1) % names.Length;
                int nextIndex = (index + 1) % names.Length;
                string previous = PortFacilityArtCatalog.RegionalFacilityTitle(previousIndex, currentPort, names[previousIndex]);
                string next = PortFacilityArtCatalog.RegionalFacilityTitle(nextIndex, currentPort, names[nextIndex]);
                PatchFacilityNavigationButton(blocks, headerRect, "Previous Port Facility", -594, "← " + previous);
                PatchFacilityNavigationButton(blocks, headerRect, "Next Port Facility", -176, next + " →");
                FacilityNavigationYamlObject titleRect = FacilityNavigationComponent(blocks, title.Id, 224);
                titleRect.Set("m_AnchoredPosition", "{x: -385, y: 0}");
                titleRect.Set("m_SizeDelta", "{x: 236, y: 56}");
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Unable to sync facility header in " + path, exception);
            }

            var byId = blocks.ToDictionary(block => block.Id);
            return new FacilityNavigationPatch
            {
                Path = path, Before = before,
                After = blockPattern.Replace(before, match => byId[match.Groups[2].Value].Value),
            };
        }

        private static FacilityNavigationYamlObject FacilityNavigationNamedObject(FacilityNavigationYamlObject[] blocks, string name)
        {
            return blocks.Single(block => block.ClassId == 1 && block.Read("m_Name") == name);
        }

        private static FacilityNavigationYamlObject FacilityNavigationComponent(FacilityNavigationYamlObject[] blocks,
            string gameObjectId, int classId, string marker = null)
        {
            return blocks.Single(block => block.ClassId == classId &&
                block.Owner == "{fileID: " + gameObjectId + "}" && (marker == null || block.Value.Contains(marker)));
        }

        private static FacilityNavigationYamlObject FacilityNavigationHeaderChild(FacilityNavigationYamlObject[] blocks,
            FacilityNavigationYamlObject parentRect, string name)
        {
            FacilityNavigationYamlObject child = FacilityNavigationNamedObject(blocks, name);
            if (FacilityNavigationComponent(blocks, child.Id, 224).Read("m_Father") != parentRect.FileReference)
                throw new InvalidDataException(name + " is not a child of the authored facility header.");
            return child;
        }

        private static string ReadFacilityNavigationText(FacilityNavigationYamlObject text)
        {
            string value = text.Read("m_Text");
            return value.StartsWith("\"", StringComparison.Ordinal)
                ? Regex.Unescape(value.Substring(1, value.Length - 2)).Trim() : value.Trim();
        }

        private static void PatchFacilityNavigationButton(FacilityNavigationYamlObject[] blocks,
            FacilityNavigationYamlObject headerRect, string name, int x, string caption)
        {
            FacilityNavigationYamlObject button = FacilityNavigationHeaderChild(blocks, headerRect, name);
            FacilityNavigationYamlObject rect = FacilityNavigationComponent(blocks, button.Id, 224);
            rect.Set("m_AnchoredPosition", "{x: " + x + ", y: 0}");
            rect.Set("m_SizeDelta", "{x: 166, y: 58}");
            FacilityNavigationYamlObject labelRect = blocks.Single(block => block.ClassId == 224 &&
                block.Read("m_Father") == rect.FileReference && blocks.Any(child => child.ClassId == 1 &&
                    child.FileReference == block.Owner && child.Read("m_Name") == "Label"));
            string labelId = Regex.Match(labelRect.Owner, @"\d+").Value;
            FacilityNavigationYamlObject label = FacilityNavigationComponent(blocks, labelId, 114, "  m_FontData:");
            // Same insets and best-fit bounds applied by WoodButtonSkin to a 166 x 58 control.
            labelRect.Set("m_AnchoredPosition", "{x: 0, y: 0}");
            labelRect.Set("m_SizeDelta", "{x: 146.08, y: 49.88}");
            label.Set("m_FontSize", "20");
            label.Set("m_BestFit", "1");
            label.Set("m_MinSize", "17");
            label.Set("m_MaxSize", "20");
            label.Set("m_HorizontalOverflow", "0");
            label.Set("m_VerticalOverflow", "0");
            label.Set("m_Text", "\"" + string.Concat(caption.Select(character => character > 127
                ? "\\u" + ((int)character).ToString("X4") : character.ToString())) + "\"");
        }
    }
}
