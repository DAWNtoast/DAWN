// Disposable editor preview only: no scene saves or player profile writes.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class ComponentRarityWoodCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.Equals("D:/Game/tmp/component-rarity-wood-20261022", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the dedicated validation project is allowed.");
            if (Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped editor with graphics required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-rarityWoodOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -rarityWoodOutput.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/ComponentRarityWood20261022/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use the named Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Preserve existing capture evidence.");
            Directory.CreateDirectory(output);
            Capture(output, false);
            Capture(output, true);
            File.WriteAllText(Path.Combine(output, "capture-complete.txt"), "PASS: production showroom, normal port stock and all five rarity boards.");
            Debug.Log("RARITY_WOOD_CAPTURE_OK");
        }

        private static void Capture(string output, bool allRarities)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null, crop = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Component Rarity Wood Preview") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                if (allRarities)
                {
                    var catalog = (ComponentArtCatalog)Get(controller, "componentArt");
                    var stock = Enumerable.Range(0, 5).Select(rarity => catalog.ExpandedEntries.First(entry =>
                        entry.Slot == ComponentSlot.Bow && (int)entry.Rarity == rarity)).Select(entry =>
                        new PortComponentShopStockItem("rarity-wood-capture|" + entry.Id, controller.Session.CurrentPort,
                            PortComponentShopCatalog.MonthKey(controller.Session.GameDateTime), entry, entry.Price)).ToList();
                    RectTransform panel = canvas.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Component Shop Panel");
                    for (int child = panel.childCount - 1; child >= 0; child--) Object.DestroyImmediate(panel.GetChild(child).gameObject);
                    Set(controller, "componentShopCategory", (int)ComponentSlot.Bow);
                    Set(controller, "componentShopDisplayPage", 0);
                    Set(controller, "componentShopShowroomOfferId", stock[1].Id);
                    typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopShowroom", Private)
                        .Invoke(controller, new object[] { panel, stock, controller.Session.GameDateTime, 0 });
                }
                var boards = canvas.GetComponentsInChildren<ComponentRarityWoodSkin>(true);
                if (boards.Length == 0 || (allRarities && boards.Select(item => item.Rarity).Distinct().Count() != 5))
                    throw new InvalidOperationException("Production rarity boards missing.");
                foreach (var board in boards)
                    if (board.GetComponent<Image>().sprite != ComponentRarityWoodSkin.FaceFor(board.Rarity))
                        throw new InvalidOperationException("Fallback or mismatched rarity board: " + board.name);

                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var cameraObject = new GameObject("Rarity Wood Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .035f, .025f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 540f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << 31;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.aspect = 1920f / 1080f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                RectTransform rect = canvas.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                for (int pass = 0; pass < 4; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target;
                string name = allRarities ? "all-rarities" : "broken-anchor";
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                crop = new Texture2D(300, 840, TextureFormat.RGB24, false, false);
                crop.ReadPixels(new Rect(1610f, 100f, 300f, 840f), 0, 0);
                crop.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, name + "-catalogue.png"), crop.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (crop != null) Object.DestroyImmediate(crop);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
