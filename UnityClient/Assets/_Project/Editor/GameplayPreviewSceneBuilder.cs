using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public const string SceneFolder = "Assets/_Project/Scenes/UIPreviews";
        // Retired on 2026-10-13. Keep the path readable for reports and exclusion checks.
        public const string EditableSceneFolder = "Assets/_Project/Scenes/UIPreviewsForMe";

        public static void RejectRetiredEditablePreviews()
        {
            throw new NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; its scenes must not be created, refreshed or saved. Use the maintained UIPreviews tools.");
        }

        public static bool IsRetiredEditablePreviewPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            return normalized.Equals(EditableSceneFolder, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(EditableSceneFolder + "/", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/" + EditableSceneFolder, StringComparison.OrdinalIgnoreCase) ||
                normalized.IndexOf("/" + EditableSceneFolder + "/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class PreviewSceneSpec
        {
            public PreviewSceneSpec(string fileName, string displayName, CoreGameplayPreviewScreen screen, CoreGameplayPreviewPort port, int routeIndex = 0)
            {
                FileName = fileName;
                DisplayName = displayName;
                Screen = screen;
                Port = port;
                RouteIndex = routeIndex;
            }

            public string FileName { get; }
            public string DisplayName { get; }
            public CoreGameplayPreviewScreen Screen { get; }
            public CoreGameplayPreviewPort Port { get; }
            public int RouteIndex { get; }
            public string Path => SceneFolder + "/" + FileName + ".unity";
        }

        private static readonly PreviewSceneSpec[] Specs =
        {
            new PreviewSceneSpec("UI_PortHub", "港口总览", CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.BrokenAnchor),
            new PreviewSceneSpec("UI_Warehouse", "船长收藏室", CoreGameplayPreviewScreen.Warehouse, CoreGameplayPreviewPort.BrokenAnchor),
            new PreviewSceneSpec("UI_Shipyard", "皇家船坞", CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewPort.BronzeBell),
            new PreviewSceneSpec("UI_ShipyardDetail", "船只档案", CoreGameplayPreviewScreen.ShipyardDetail, CoreGameplayPreviewPort.BronzeBell),
            new PreviewSceneSpec("UI_Tavern", "灰鸥酒馆", CoreGameplayPreviewScreen.Tavern, CoreGameplayPreviewPort.GrayGull),
            new PreviewSceneSpec("UI_TavernCrewDetail", "特殊船员详情", CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.GrayGull),
            new PreviewSceneSpec("UI_ComponentShop", "部件商店", CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.BronzeBell),
            new PreviewSceneSpec("UI_ComponentDetail", "部件详情", CoreGameplayPreviewScreen.ComponentShopDetail, CoreGameplayPreviewPort.BronzeBell),
            new PreviewSceneSpec("UI_CardWorkshop", "卡组工坊", CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.TwilightTide),
            new PreviewSceneSpec("UI_DeckBuilder", "卡组构筑", CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.TwilightTide),
            new PreviewSceneSpec("UI_TreasureMap", "藏宝图桌", CoreGameplayPreviewScreen.TreasureMap, CoreGameplayPreviewPort.TwilightTide),
            new PreviewSceneSpec("UI_Maintenance", "港口投资建设", CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.SaltCrown),
            new PreviewSceneSpec("UI_VoyageDeparture", "出航码头", CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.BrokenAnchor),
            new PreviewSceneSpec("UI_Market", "港口市场", CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell),
            new PreviewSceneSpec("UI_Navigation", "航海探索", CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.WhiteShell, 0),
            new PreviewSceneSpec("UI_Encounter", "航海事件", CoreGameplayPreviewScreen.Encounter, CoreGameplayPreviewPort.BronzeBell, 1),
            new PreviewSceneSpec("UI_Battle", "海战界面", CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.TwilightTide, 2),
            new PreviewSceneSpec("UI_Settlement", "航行结算", CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 1),
        };

        public static int PreviewSceneCount => Specs.Length;

        [MenuItem("Shattered Sail/Scenes/Refresh Open UI Previews")]
        public static void RefreshOpenPreviews()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int refreshed = 0;
            for (int index = 0; index < SceneManager.sceneCount; index += 1)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded || !scene.path.StartsWith(SceneFolder + "/", StringComparison.Ordinal)) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (CoreGameplayDemoController controller in root.GetComponentsInChildren<CoreGameplayDemoController>())
                    {
                        if (!controller.isActiveAndEnabled) continue;
                        controller.RefreshEditorPreview();
                        refreshed += 1;
                    }
                }
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log("UI previews refreshed: " + refreshed + ". No scenes were saved or replaced.");
        }

        [MenuItem("Shattered Sail/Scenes/Refresh Open UI Previews", true)]
        private static bool CanRefreshOpenPreviews() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Shattered Sail/Scenes/Create or Refresh All UI Preview Scenes")]
        public static void CreateOrRefreshFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateOrRefreshAll(true);
        }

        public static void CreateOrRefreshEditableFromMenu()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateOrRefreshEditableAll(true);
        }

        public static void SelectEditablePreviewFolder()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(EditableSceneFolder);
            if (folder == null)
            {
                CreateOrRefreshEditableAll(false);
                folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(EditableSceneFolder);
            }
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        [MenuItem("Shattered Sail/Scenes/Open UI Preview Folder")]
        public static void SelectPreviewFolder()
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SceneFolder);
            if (folder == null)
            {
                CreateOrRefreshFromMenu();
                folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SceneFolder);
            }
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        public static void CreateFromCommandLine()
        {
            CreateOrRefreshAll(false);
        }

        // Batch-mode entry point for the hand-editable scene set.
        public static void CreateEditableFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            CreateOrRefreshEditableAll(false);
        }

        public static void AddSeedDropdownToEditableVoyageDeparture()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            // Retain the command name for integrations, but synchronize the
            // complete editable input, mode toggle and narrow history selector.
            SyncChartSeedStatesFromCommandLine();
        }
        public static void CreateWarehouseFromCommandLine()
        {
            string absoluteFolder = AbsolutePath(SceneFolder);
            if (!Directory.Exists(absoluteFolder)) Directory.CreateDirectory(absoluteFolder);
            PreviewSceneSpec warehouse = Array.Find(Specs, spec => spec.Screen == CoreGameplayPreviewScreen.Warehouse);
            CreatePreviewScene(warehouse);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Warehouse UI preview scene refreshed: " + warehouse.Path + ".");
        }

        // The worktable background and its complete dynamic panel share one layout contract.
        // The maintained treasure preview is updated; its Canvas and header remain authored.
        public static void SyncTreasureWorktableFromCommandLine() => SyncTreasureStatesFromCommandLine();

        public static void SyncTreasureStatesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Treasure preview synchronization requires edit mode.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save modified open scenes before synchronizing treasure previews.");
            PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.TreasureMap);
            string[] paths =
            {
                SceneFolder + "/UI_TreasureMap.unity",
            };
            TreasureMapPreviewState[] states =
            {
                TreasureMapPreviewState.MapUnopened,
            };
            string backupRoot = AbsolutePath("Artifacts/TreasureWorktable20261013/preview-backup/" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
            foreach (string path in paths)
                if (!File.Exists(AbsolutePath(path)))
                    throw new FileNotFoundException("Missing authored treasure preview", AbsolutePath(path));
            foreach (string path in paths)
            {
                string backup = Path.Combine(backupRoot, path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(AbsolutePath(path), backup);
            }
            for (int index = 0; index < paths.Length; index++)
            {
                string absolute = AbsolutePath(paths[index]);
                string before = File.ReadAllText(absolute);
                Scene scene = EditorSceneManager.OpenScene(paths[index], OpenSceneMode.Single);
                CoreGameplayDemoController destination = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.TreasureMap)
                    throw new InvalidOperationException("Expected a treasure preview in " + paths[index]);
                Transform destinationRoom = destination.GetComponentsInChildren<Transform>(true).SingleOrDefault(item => item.name == "Treasure Panel");
                if (destinationRoom == null)
                {
                    // Older lightweight previews can contain only the controller.
                    destination.ConfigureEditorPreview(CoreGameplayPreviewScreen.TreasureMap, spec.Port, spec.RouteIndex, true);
                    destination.ConfigureEditorTreasureState(states[index]);
                    PersistGeneratedSceneVisuals(destination.transform, spec);
                    destinationRoom = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Treasure Panel");
                }
                Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                GameObject staging = new GameObject("__TreasureStateStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                try
                {
                    CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                    source.ConfigureEditorPreview(CoreGameplayPreviewScreen.TreasureMap, spec.Port, spec.RouteIndex, true);
                    source.ConfigureEditorTreasureState(states[index]);
                    if (!source.HasCompleteEditorPreview)
                        throw new InvalidOperationException("Incomplete treasure staging preview in " + paths[index]);
                    PersistGeneratedSceneVisuals(staging.transform, spec);
                    Transform sourceRoom = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Treasure Panel");
                    Transform destinationContent = destinationRoom.parent;
                    Transform sourceBackground = sourceRoom.parent.Find("Treasure Background");
                    RawImage backgroundArt = sourceBackground == null ? null : sourceBackground.GetComponent<RawImage>();
                    if (backgroundArt == null || backgroundArt.texture == null)
                        throw new InvalidOperationException("Missing generated treasure worktable background in " + paths[index]);
                    if (sourceRoom.parent.Find("Port Facility Description") == null)
                        CopyTreasureLayer(null, destinationContent, "Port Facility Description");
                    SyncPreviewCanvasScaling(source, destination);
                    ReplaceTreasureWorktableLayer(sourceBackground, destinationContent, "Treasure Background");
                    ReplaceTreasureWorktableLayer(sourceRoom, destinationContent, "Treasure Panel");
                    CopyTreasureLayer(sourceRoom.parent.Find("Treasure Folio Overlay"), destinationContent, "Treasure Folio Overlay");
                    var serialized = new SerializedObject(destination);
                    serialized.FindProperty("editorPreviewTreasureState").enumValueIndex = (int)states[index];
                    serialized.FindProperty("editorPreviewPersistGeneratedUi").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                finally
                {
                    if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                    if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                    if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                }
                if (File.ReadAllText(absolute) != before)
                    throw new IOException("Treasure scene changed during preview synchronization: " + paths[index]);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, paths[index]))
                    throw new InvalidOperationException("Failed to save treasure preview " + paths[index]);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TREASURE_WORKTABLE_PREVIEW_SYNC: updated " + paths.Length +
                " treasure backgrounds and dynamic panels; Canvas, header and unrelated pages preserved. Backup: " + backupRoot);
        }

        private static void ReplaceTreasureWorktableLayer(Transform source, Transform parent, string name)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Transform previous = parent.Find(name);
            int siblingIndex = previous == null ? source.GetSiblingIndex() : previous.GetSiblingIndex();
            CopyTreasureLayer(source, parent, name);
            parent.Find(name).SetSiblingIndex(siblingIndex);
        }

        private static void CopyTreasureLayer(Transform source, Transform parent, string name)
        {
            Transform previous = parent.Find(name);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            if (source == null) return;
            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
            copy.name = name;
        }

        private static void CreateOrRefreshAll(bool openFirstScene)
        {
            string absoluteFolder = AbsolutePath(SceneFolder);
            if (!Directory.Exists(absoluteFolder))
            {
                Directory.CreateDirectory(absoluteFolder);
            }

            for (int index = 0; index < Specs.Length; index += 1)
            {
                CreatePreviewScene(Specs[index]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (openFirstScene)
            {
                Scene scene = EditorSceneManager.OpenScene(Specs[0].Path, OpenSceneMode.Single);
                Selection.activeGameObject = scene.GetRootGameObjects()[1];
            }
            Debug.Log("UI preview scenes created: " + Specs.Length + " scenes in " + SceneFolder + ".");
        }

        private static void CreateOrRefreshEditableAll(bool openFirstScene)
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            string absoluteFolder = AbsolutePath(EditableSceneFolder);
            if (!Directory.Exists(absoluteFolder)) Directory.CreateDirectory(absoluteFolder);
            string readmePath = Path.Combine(absoluteFolder, "README.md");
            if (!File.Exists(readmePath))
                File.WriteAllText(readmePath, EditablePreviewReadme, new System.Text.UTF8Encoding(false));

            for (int index = 0; index < Specs.Length; index += 1)
                CreateEditablePreviewScene(Specs[index]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (openFirstScene)
            {
                Scene scene = EditorSceneManager.OpenScene(EditablePath(Specs[0]), OpenSceneMode.Single);
                Selection.activeGameObject = scene.GetRootGameObjects()[1];
            }
            Debug.Log("Editable UI preview scenes created: " + Specs.Length + " scenes in " + EditableSceneFolder + ". Generated Canvas objects are serialized and can be edited directly.");
        }

        private static void CreatePreviewScene(PreviewSceneSpec spec)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = spec.FileName;

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.10f, 0.14f, 1f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject controllerObject = new GameObject("UI Preview · " + spec.DisplayName);
            CoreGameplayDemoController controller = controllerObject.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(spec.Screen, spec.Port, spec.RouteIndex);
            if (spec.Screen == CoreGameplayPreviewScreen.ShipyardDetail) controller.ConfigureEditorShipyard(1, true);
            EditorUtility.SetDirty(controller);

            if (!controller.HasCompleteEditorPreview)
            {
                throw new InvalidOperationException("Failed to build complete editor preview content for " + spec.Path + ".");
            }

            controller.PrepareEditorPreviewForSceneSave();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, spec.Path))
            {
                throw new InvalidOperationException("Failed to save UI preview scene at " + spec.Path + ".");
            }
        }

        private static void CreateEditablePreviewScene(PreviewSceneSpec spec)
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = spec.FileName;

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.10f, 0.14f, 1f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject controllerObject = new GameObject("UI Preview · " + spec.DisplayName + " · 可编辑");
            CoreGameplayDemoController controller = controllerObject.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(spec.Screen, spec.Port, spec.RouteIndex, true);
            if (spec.Screen == CoreGameplayPreviewScreen.ShipyardDetail) controller.ConfigureEditorShipyard(1, true);
            EditorUtility.SetDirty(controller);

            if (!controller.HasCompleteEditorPreview)
                throw new InvalidOperationException("Failed to build complete editable UI preview content for " + EditablePath(spec) + ".");

            PersistGeneratedSceneVisuals(controllerObject.transform, spec);
            // Persistent preview mode intentionally keeps the generated Canvas in
            // the scene. Do not call PrepareEditorPreviewForSceneSave here.
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, EditablePath(spec)))
                throw new InvalidOperationException("Failed to save editable UI preview scene at " + EditablePath(spec) + ".");
        }

        private const string EditableVisualAssetFolder = "Assets/_Project/Presentation/EditablePreviewAssets";

        private static void PersistGeneratedSceneVisuals(Transform root, PreviewSceneSpec spec)
        {
            string absoluteFolder = AbsolutePath(EditableVisualAssetFolder);
            if (!Directory.Exists(absoluteFolder)) Directory.CreateDirectory(absoluteFolder);
            AssetDatabase.Refresh();
            var sprites = new System.Collections.Generic.Dictionary<int, Sprite>();
            var textures = new System.Collections.Generic.Dictionary<int, Texture2D>();
            var materials = new System.Collections.Generic.Dictionary<int, Material>();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).Length == 0)
                {
                    image.sprite = PersistentSprite(image.sprite, spec, sprites, textures);
                    EditorUtility.SetDirty(image);
                }
                PersistGraphicMaterial(image, spec, materials);
            }
            foreach (RawImage image in root.GetComponentsInChildren<RawImage>(true))
            {
                Texture2D texture = image.texture as Texture2D;
                if (texture != null && AssetDatabase.GetAssetPath(texture).Length == 0)
                {
                    image.texture = PersistentTexture(texture, spec, textures);
                    EditorUtility.SetDirty(image);
                }
                PersistGraphicMaterial(image, spec, materials);
            }
            AssetDatabase.SaveAssets();
        }

        private static void PersistGraphicMaterial(Graphic graphic, PreviewSceneSpec spec,
            System.Collections.Generic.Dictionary<int, Material> materials)
        {
            if (graphic == null || graphic.material == null || AssetDatabase.GetAssetPath(graphic.material).Length != 0) return;
            int key = graphic.material.GetInstanceID();
            if (!materials.TryGetValue(key, out Material copy))
            {
                string path = EditableVisualAssetFolder + "/" + SafeAssetName(spec.FileName + "_" + graphic.material.name + "_material") + ".mat";
                copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy == null)
                {
                    copy = new Material(graphic.material) { name = graphic.material.name };
                    AssetDatabase.CreateAsset(copy, path);
                }
                materials[key] = copy;
            }
            graphic.material = copy;
            EditorUtility.SetDirty(graphic);
        }

        private static Sprite PersistentSprite(Sprite source, PreviewSceneSpec spec,
            System.Collections.Generic.Dictionary<int, Sprite> sprites,
            System.Collections.Generic.Dictionary<int, Texture2D> textures)
        {
            int key = source.GetInstanceID();
            if (sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D texture = source.texture;
            if (texture == null) return source;
            Texture2D persistentTexture = AssetDatabase.GetAssetPath(texture).Length == 0
                ? PersistentTexture(texture, spec, textures)
                : texture;
            string assetName = SafeAssetName(spec.FileName + "_" + source.name + "_" +
                Mathf.RoundToInt(source.rect.x) + "_" + Mathf.RoundToInt(source.rect.y) + "_" +
                Mathf.RoundToInt(source.rect.width) + "_" + Mathf.RoundToInt(source.rect.height));
            string path = EditableVisualAssetFolder + "/" + assetName + ".sprite.asset";
            Sprite copy = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (copy == null)
            {
                copy = Sprite.Create(persistentTexture, source.rect, source.pivot, source.pixelsPerUnit,
                    0, SpriteMeshType.FullRect, source.border);
                copy.name = source.name;
                AssetDatabase.CreateAsset(copy, path);
            }
            sprites[key] = copy;
            return copy;
        }

        private static Texture2D PersistentTexture(Texture2D source, PreviewSceneSpec spec,
            System.Collections.Generic.Dictionary<int, Texture2D> textures)
        {
            int key = source.GetInstanceID();
            if (textures.TryGetValue(key, out Texture2D cached)) return cached;
            string assetName = SafeAssetName(spec.FileName + "_" + source.name + "_texture");
            string path = EditableVisualAssetFolder + "/" + assetName + ".texture.asset";
            Texture2D copy = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (copy == null)
            {
                copy = UnityEngine.Object.Instantiate(source);
                copy.name = source.name;
                copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, path);
            }
            textures[key] = copy;
            return copy;
        }

        private static string SafeAssetName(string value)
        {
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index += 1)
            {
                char c = chars[index];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') chars[index] = '_';
            }
            return new string(chars);
        }

        private static string EditablePath(PreviewSceneSpec spec)
        {
            return EditableSceneFolder + "/" + spec.FileName + ".unity";
        }

        private const string EditablePreviewReadme = @"# 可编辑 UI 预览

此文件夹中的场景保存了完整的 Canvas 层级，可以直接在 Unity 的 Hierarchy 和 Inspector 中调整布局、尺寸、层级、文字与颜色，然后使用 Ctrl+S 保存。

这些场景与 `Assets/_Project/Scenes/UIPreviews` 中的运行时预览分开。这里的 Canvas 不使用 `DontSaveInEditor`，打开、编译或关闭场景时也不会自动删除。需要恢复代码默认布局时，请使用菜单 `Shattered Sail > Scenes > Create or Refresh Editable UI Preview Scenes`，该操作会重新生成并覆盖当前编辑场景。

场景由同一个 `CoreGameplayDemoController` 提供数据与美术资源，保存后的 Canvas 是独立的编辑副本。修改完成后，请把需要长期保留的布局调整同步回对应的 `CoreGameplay*` 构建代码；后续优化时可直接以此文件夹中你保存的场景为准。
";

        private static string AbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
