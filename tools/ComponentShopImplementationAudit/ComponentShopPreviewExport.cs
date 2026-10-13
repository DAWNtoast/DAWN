// Scratch-only editor helper. Copy into scratch Assets/_Project/Editor after UI acceptance.
// Does not belong in production Assets. It exports only four component-shop previews.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        private static readonly string[] ShopV3ExportScenes =
        {
            "Assets/_Project/Scenes/UIPreviewsForMe/UI_ComponentShop.unity",
            "Assets/_Project/Scenes/UIPreviewsForMe/UIComponentShop/UIComponentShop_OfferBoard.unity",
            "Assets/_Project/Scenes/UIPreviewsForMe/UIComponentShop/UIComponentShop_OfferSoldOut.unity",
            "Assets/_Project/Scenes/UIPreviewsForMe/UIComponentShop/UIComponentShop_RefreshNotice.unity"
        };
        [Serializable] private sealed class ShopV3ExportReport
        {
            public string scratchProject;
            public string createdUtc;
            public string[] scenes;
            public string[] changedScenes;
            public string[] assets;
            public string[] files;
            public ShopV3SceneBaseline[] originalScenes;
        }
        [Serializable] private sealed class ShopV3SceneBaseline { public string path, sha256; }
        [Serializable] private sealed class ShopV3TextReport
        {
            public string createdUtc;
            public int equipment, texts;
            public ShopV3TextMeasurement[] measurements;
            public ShopV3TextMeasurement[] overflow;
            public string[] imageIssues;
        }
        [Serializable] private sealed class ShopV3TextMeasurement
        {
            public string component, page, name, content;
            public float width, height, minimumFontHeight;
            public int fontSize, minimumFontSize;
        }

        private static string ShopV3ScratchProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run the component-shop helper only in stopped edit mode.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!root.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This export helper is restricted to a disposable D:/Game/tmp project: " + root);
            return root;
        }
        private static string ShopV3Output(string option, string suffix)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, option);
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1] :
                "D:/Game/Artifacts/ComponentShopImplementation20261012/" + suffix;
            output = Path.GetFullPath(output).Replace('\\', '/');
            if (!output.StartsWith("D:/Game/Artifacts/ComponentShopImplementation20261012/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Export must stay inside this task's artifact directory: " + output);
            Directory.CreateDirectory(output);
            return output;
        }
        private static string ShopV3Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static Dictionary<string, string> ShopV3SceneHashes(string project)
        {
            return Directory.GetFiles(Path.Combine(project, "Assets/_Project/Scenes"), "*.unity", SearchOption.AllDirectories)
                .ToDictionary(path => path.Substring(project.Length + 1).Replace('\\', '/'), ShopV3Hash);
        }
        private static void ShopV3Invoke(CoreGameplayDemoController controller, string method, params object[] args)
        {
            MethodInfo target = typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (target == null) throw new MissingMethodException(method);
            target.Invoke(controller, args);
        }

        // -executeMethod ShatteredSail.Editor.GameplayPreviewSceneBuilder.ExportComponentShopV3FromCommandLine
        // -componentShopExportRoot D:/Game/Artifacts/ComponentShopImplementation20261012/preview-export
        public static void ExportComponentShopV3FromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            string project = ShopV3ScratchProject();
            string output = ShopV3Output("-componentShopExportRoot", "preview-export");
            var before = ShopV3SceneHashes(project);
            var exported = new List<string>();
            var writtenAssets = new HashSet<string>();
            foreach (string path in ShopV3ExportScenes)
            {
                if (!File.Exists(Path.Combine(project, path))) throw new FileNotFoundException(path);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                if (controller.EditorPreviewScreen != CoreGameplayPreviewScreen.ComponentShop)
                    throw new InvalidOperationException("Whitelisted scene contains another screen: " + path);
                if (!controller.EditorPreviewPersistsGeneratedUi)
                    throw new InvalidOperationException("Expected a hand-editable component-shop preview: " + path);
                // Deliberately refresh only this shop's generated Canvas. Keep its
                // controller, scene GUID, port choice, camera and external roots.
                controller.RefreshEditorPreview();
                if (path.EndsWith("_RefreshNotice.unity", StringComparison.Ordinal))
                {
                    DateTime current = controller.Session.GameDateTime;
                    DateTime next = new DateTime(current.Year, current.Month, 1, 8, 0, 0).AddMonths(1);
                    controller.Session.RestoreGameClock((next - ShatteredSail.Core.Gameplay.CoreVoyageSession.GameClockOrigin).TotalHours);
                    ShopV3Invoke(controller, "Render");
                }
                if (!controller.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete shop preview: " + path);
                Transform panel = controller.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Component Shop Panel");
                if (path.EndsWith("_OfferSoldOut.unity", StringComparison.Ordinal))
                {
                    // This is a visual fixture only; never write a player's receipt.
                    var first = panel.GetComponentsInChildren<Button>(true).First(item => item.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal));
                    RawImage icon = first.GetComponentsInChildren<RawImage>(true).Single();
                    icon.color = new Color(.68f, .68f, .68f, 1f);
                    Text state = first.GetComponentsInChildren<Text>(true).Single(item => item.name.StartsWith("Component Shop Thumbnail State ", StringComparison.Ordinal));
                    state.text = "本月已售出";
                }
                string prefix = "ComponentShopV3_" + Path.GetFileNameWithoutExtension(path);
                var spec = new PreviewSceneSpec(prefix, "部件商店", CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.BronzeBell);
                PersistGeneratedSceneVisuals(controller.transform, spec);
                Canvas.ForceUpdateCanvases();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Cannot save scratch scene: " + path);
                RepairSavedNavigationCanvasScale(Path.Combine(project, path));
                AssertSavedNavigationCanvasScale(Path.Combine(project, path));
                foreach (string asset in Directory.GetFiles(Path.Combine(project, EditableVisualAssetFolder), prefix + "*", SearchOption.TopDirectoryOnly))
                    writtenAssets.Add(asset.Substring(project.Length + 1).Replace('\\', '/'));
                exported.Add(path);
                exported.Add(path + ".meta");
            }
            var after = ShopV3SceneHashes(project);
            var changed = after.Where(item => !before.ContainsKey(item.Key) || before[item.Key] != item.Value).Select(item => item.Key).ToArray();
            if (changed.Except(ShopV3ExportScenes).Any())
                throw new InvalidOperationException("Unexpected non-shop scene changed: " + string.Join(", ", changed.Except(ShopV3ExportScenes)));
            foreach (string path in writtenAssets) exported.Add(path);
            foreach (string path in exported)
            {
                string destination = Path.Combine(output, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(Path.Combine(project, path), destination, true);
            }
            File.WriteAllText(Path.Combine(output, "export-report.json"), JsonUtility.ToJson(new ShopV3ExportReport
            {
                scratchProject = project, createdUtc = DateTime.UtcNow.ToString("O"), scenes = ShopV3ExportScenes,
                changedScenes = changed, assets = writtenAssets.OrderBy(path => path).ToArray(), files = exported.OrderBy(path => path).ToArray(),
                originalScenes = ShopV3ExportScenes.Select(path => new ShopV3SceneBaseline { path = path, sha256 = before[path] }).ToArray()
            }, true));
            Debug.Log("COMPONENT_SHOP_V3_EXPORT: " + output + " (4 shop scenes; no other scene modified)");

#endif
        }

        // -executeMethod ShatteredSail.Editor.GameplayPreviewSceneBuilder.AuditComponentShopV3TextFromCommandLine
        // -componentShopTextReportRoot D:/Game/Artifacts/ComponentShopImplementation20261012/text-audit
        public static void AuditComponentShopV3TextFromCommandLine()
        {
            ShopV3ScratchProject();
            string output = ShopV3Output("-componentShopTextReportRoot", "text-audit");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject host = new GameObject("Component Shop V3 Text Audit");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.BronzeBell, 0);
            RectTransform panel = controller.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "Component Shop Panel");
            var measurements = new List<ShopV3TextMeasurement>();
            var imageIssues = new List<string>();
            var catalog = new ComponentArtCatalog();
            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                for (int i = panel.childCount - 1; i >= 0; i--) Object.DestroyImmediate(panel.GetChild(i).gameObject);
                var offer = new PortComponentShopStockItem("ui-text-audit|" + entry.Id, controller.Session.CurrentPort,
                    PortComponentShopCatalog.MonthKey(controller.Session.GameDateTime), entry, 1.2f, entry.Price);
                ShopV3Invoke(controller, "BuildComponentShopDetail", panel, offer);
                var measured = new HashSet<Text>();
                foreach (string page in new[] { "Overview", "Effects", "Story", "SpecialCards" })
                {
                    panel.GetComponentsInChildren<Button>(true).Single(item => item.name == "ComponentDossierPage" + page).onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    foreach (Text text in panel.GetComponentsInChildren<Text>(false))
                    {
                        if (!measured.Add(text)) continue;
                        var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                        settings.resizeTextForBestFit = false;
                        settings.fontSize = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
                        settings.verticalOverflow = VerticalWrapMode.Overflow;
                        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
                        float needed = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                        measurements.Add(new ShopV3TextMeasurement
                        {
                            component = entry.Id, page = page, name = text.name, content = text.text,
                            width = text.rectTransform.rect.width, height = text.rectTransform.rect.height,
                            minimumFontHeight = needed, fontSize = text.fontSize, minimumFontSize = settings.fontSize
                        });
                    }
                }
                RawImage model = panel.GetComponentsInChildren<RawImage>().Single(item => item.name == "Component Detail Model");
                float expected = model.texture.width * model.uvRect.width / (model.texture.height * model.uvRect.height);
                float actual = model.rectTransform.rect.width / model.rectTransform.rect.height;
                if (Mathf.Abs(expected - actual) > .002f) imageIssues.Add(entry.Id + ": aspect ratio");
                if (model.texture != entry.ShipLayer) imageIssues.Add(entry.Id + ": mismatched main art");
            }
            var overflow = measurements.Where(item => item.minimumFontHeight > item.height + 1f).ToArray();
            File.WriteAllText(Path.Combine(output, "text-audit.json"), JsonUtility.ToJson(new ShopV3TextReport
            {
                createdUtc = DateTime.UtcNow.ToString("O"), equipment = catalog.ExpandedEntries.Count,
                texts = measurements.Count, measurements = measurements.ToArray(), overflow = overflow, imageIssues = imageIssues.ToArray()
            }, true));
            Object.DestroyImmediate(host);
            if (overflow.Length != 0 || imageIssues.Count != 0)
                throw new InvalidOperationException("Component shop audit found " + overflow.Length + " overflowing texts, " + imageIssues.Count + " art issues. See " + output);
            Debug.Log("COMPONENT_SHOP_V3_TEXT_AUDIT: " + catalog.ExpandedEntries.Count + " equipment; " + measurements.Count + " texts; no clipping at minimum font size.");
        }
    }
}
