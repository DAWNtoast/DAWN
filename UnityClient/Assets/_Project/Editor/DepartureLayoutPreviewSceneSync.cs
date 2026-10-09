using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        /// <summary>
        /// Runs the shared layout against isolated authored previews, then writes only
        /// changed RectTransform/Text layout fields to their original YAML objects.
        /// No scene is re-saved and no artwork, labels, gates or callbacks are replaced.
        /// </summary>
        public static void SyncDepartureLayoutFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Departure layout synchronization requires stopped edit mode.");
            DepartureLayoutPatch[] patches = ChartSeedReferencePaths.Select(PrepareDepartureLayoutPatch).ToArray();
            foreach (DepartureLayoutPatch patch in patches)
                if (File.ReadAllText(patch.Path) != patch.Before)
                    throw new IOException("Departure scene changed during layout preparation: " + patch.Path);
            int changed = 0;
            foreach (DepartureLayoutPatch patch in patches)
            {
                if (patch.Before == patch.After) continue;
                string backup = AbsolutePath("Artifacts/DepartureLayout20261007/preview-backup/" + patch.Reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(patch.Path, backup);
                File.WriteAllText(patch.Path, patch.After, new UTF8Encoding(false));
                changed++;
            }
            AssetDatabase.Refresh();
            Debug.Log("DEPARTURE_LAYOUT_PREVIEW_SYNC: verified " + patches.Length + "; updated " + changed +
                " authored scenes; patched " + patches.Sum(patch => patch.ChangedFields) +
                " layout fields. Artwork, labels, gates and unrelated serialized values retained.");
        }

        public static void SyncAndCaptureDepartureLayoutFromCommandLine()
        {
            SyncDepartureLayoutFromCommandLine();
            UIPreviewSceneAcceptance.CaptureDepartureLayoutFromCommandLine();
        }

        private sealed class DepartureLayoutPatch
        {
            public string Reference;
            public string Path;
            public string Before;
            public string After;
            public int ChangedFields;
        }

        private sealed class DepartureLayoutValue
        {
            public string Path;
            public SerializedPropertyType Type;
            public int Integer;
            public float Float;
            public bool Boolean;
            public Vector2 Vector2;
            public Vector3 Vector3;
            public Quaternion Quaternion;

            public static DepartureLayoutValue Read(SerializedProperty property)
            {
                if (property == null) throw new InvalidOperationException("Missing departure layout property.");
                var result = new DepartureLayoutValue { Path = property.propertyPath, Type = property.propertyType };
                switch (result.Type)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum: result.Integer = property.intValue; break;
                    case SerializedPropertyType.Float: result.Float = property.floatValue; break;
                    case SerializedPropertyType.Boolean: result.Boolean = property.boolValue; break;
                    case SerializedPropertyType.Vector2: result.Vector2 = property.vector2Value; break;
                    case SerializedPropertyType.Vector3: result.Vector3 = property.vector3Value; break;
                    case SerializedPropertyType.Quaternion: result.Quaternion = property.quaternionValue; break;
                    default: throw new InvalidOperationException("Unsupported departure layout property " + property.propertyPath);
                }
                return result;
            }

            public void Restore(SerializedObject target)
            {
                SerializedProperty property = target.FindProperty(Path);
                switch (Type)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum: property.intValue = Integer; break;
                    case SerializedPropertyType.Float: property.floatValue = Float; break;
                    case SerializedPropertyType.Boolean: property.boolValue = Boolean; break;
                    case SerializedPropertyType.Vector2: property.vector2Value = Vector2; break;
                    case SerializedPropertyType.Vector3: property.vector3Value = Vector3; break;
                    case SerializedPropertyType.Quaternion: property.quaternionValue = Quaternion; break;
                }
            }

            public string Yaml()
            {
                switch (Type)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum: return Integer.ToString(CultureInfo.InvariantCulture);
                    case SerializedPropertyType.Float: return Number(Float);
                    case SerializedPropertyType.Boolean: return Boolean ? "1" : "0";
                    case SerializedPropertyType.Vector2: return "{x: " + Number(Vector2.x) + ", y: " + Number(Vector2.y) + "}";
                    case SerializedPropertyType.Vector3: return "{x: " + Number(Vector3.x) + ", y: " + Number(Vector3.y) + ", z: " + Number(Vector3.z) + "}";
                    case SerializedPropertyType.Quaternion: return "{x: " + Number(Quaternion.x) + ", y: " + Number(Quaternion.y) +
                        ", z: " + Number(Quaternion.z) + ", w: " + Number(Quaternion.w) + "}";
                    default: throw new InvalidOperationException("Unsupported departure layout value.");
                }
            }

            private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        }

        private sealed class DepartureLayoutSnapshot
        {
            public Component Component;
            public string Json;
            public string Id;
            public DepartureLayoutValue[] Values;
        }

        private static readonly string[] DepartureRectLayoutProperties =
        {
            "m_LocalRotation", "m_LocalPosition", "m_LocalScale", "m_AnchorMin", "m_AnchorMax",
            "m_AnchoredPosition", "m_SizeDelta", "m_Pivot",
        };

        private static readonly string[] DepartureTextLayoutProperties =
        {
            "m_FontData.m_FontSize", "m_FontData.m_FontStyle", "m_FontData.m_BestFit",
            "m_FontData.m_MinSize", "m_FontData.m_MaxSize", "m_FontData.m_Alignment",
            "m_FontData.m_HorizontalOverflow", "m_FontData.m_VerticalOverflow", "m_FontData.m_LineSpacing",
        };

        private static DepartureLayoutPatch PrepareDepartureLayoutPatch(string reference)
        {
            string scenePath = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(scenePath);
            string before = File.ReadAllText(absolute);
            var blockPattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
            Dictionary<string, string> blocks = blockPattern.Matches(before).Cast<Match>()
                .ToDictionary(match => match.Groups[2].Value, match => match.Value);
            Scene preview = default;
            int fields = 0;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(scenePath);
                CoreGameplayDemoController controller = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                if (controller.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture || !controller.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete authored departure preview in " + scenePath);
                Transform content = controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                RectTransform routes = content.Find("Route Selection") as RectTransform;
                RectTransform setup = content.Find("Voyage Setup") as RectTransform;
                if (routes == null || setup == null)
                    throw new InvalidOperationException("Missing authored departure panels in " + scenePath);
                Component[] components = controller.GetComponentsInChildren<Component>(true);
                DepartureLayoutSnapshot[] snapshots = components.Select(component =>
                {
                    string[] allowed = component is RectTransform ? DepartureRectLayoutProperties :
                        component is Text ? DepartureTextLayoutProperties : Array.Empty<string>();
                    var serialized = new SerializedObject(component);
                    return new DepartureLayoutSnapshot
                    {
                        Component = component,
                        Json = EditorJsonUtility.ToJson(component),
                        Id = GlobalObjectId.GetGlobalObjectIdSlow(component).targetObjectId.ToString(CultureInfo.InvariantCulture),
                        Values = allowed.Select(path => DepartureLayoutValue.Read(serialized.FindProperty(path))).ToArray(),
                    };
                }).ToArray();

                DepartureLayout.Apply(routes, setup);

                if (!components.SequenceEqual(controller.GetComponentsInChildren<Component>(true)))
                    throw new InvalidOperationException("Shared departure layout added, removed or reordered authored objects in " + scenePath);
                var currentValues = snapshots.ToDictionary(snapshot => snapshot, snapshot =>
                {
                    var serialized = new SerializedObject(snapshot.Component);
                    return snapshot.Values.Select(value => DepartureLayoutValue.Read(serialized.FindProperty(value.Path))).ToArray();
                });
                foreach (DepartureLayoutSnapshot snapshot in snapshots)
                {
                    var serialized = new SerializedObject(snapshot.Component);
                    DepartureLayoutValue[] current = currentValues[snapshot];
                    for (int index = 0; index < current.Length; index++)
                    {
                        if (current[index].Yaml() == snapshot.Values[index].Yaml()) continue;
                        if (!blocks.TryGetValue(snapshot.Id, out string block))
                            throw new InvalidOperationException("Missing authored YAML object " + snapshot.Id + " in " + scenePath);
                        string key = current[index].Path.Split('.').Last();
                        var propertyPattern = new Regex(@"(?m)^([ \t]*" + Regex.Escape(key) + @": )[^\r\n]*");
                        if (propertyPattern.Matches(block).Count != 1)
                            throw new InvalidDataException("Expected one " + key + " in authored object " + snapshot.Id);
                        blocks[snapshot.Id] = propertyPattern.Replace(block, match => match.Groups[1].Value + current[index].Yaml());
                        fields++;
                    }
                    // Restore the permitted values in memory. Any remaining JSON
                    // change is outside the layout scope and must abort the patch.
                    foreach (DepartureLayoutValue value in snapshot.Values) value.Restore(serialized);
                    if (snapshot.Values.Length > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (EditorJsonUtility.ToJson(snapshot.Component) != snapshot.Json)
                        throw new InvalidOperationException("Shared departure layout changed non-layout data on " +
                            snapshot.Component.name + " (" + snapshot.Component.GetType().Name + ") in " + scenePath);
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
            return new DepartureLayoutPatch
            {
                Reference = reference, Path = absolute, Before = before, ChangedFields = fields,
                After = blockPattern.Replace(before, match => blocks[match.Groups[2].Value]),
            };
        }
    }
}
