// Isolated editor validation only. No saved scenes, assets or PlayerPrefs writes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public static class ComponentRarityHaloCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Layer = 31;
        [Serializable] private sealed class Report
        {
            public string generatedUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Production rarity halo: five rarities by five slots, original icon and ShipLayer art; production shop, dossier, assembly and resale. Fixtures are memory-only. Temporary PreviewScenes; no profile, asset or scene saves.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string file, state, error;
            public int width = 1920, height = 1080, halos;
            public bool nonInteractiveHalos;
            public List<string> samples = new List<string>();
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!project.Equals("D:/Game/tmp/component-rarity-halo-20261019", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run only in the named disposable rarity validation project.");
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-componentRarityHaloOutput");
            if (key < 0 || key + 1 >= args.Length) throw new ArgumentException("Pass -componentRarityHaloOutput ABSOLUTE_NEW_ARTIFACTS_DIRECTORY.");
            string output = Path.GetFullPath(args[key + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output must be below D:/Game/Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new empty output directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            report.frames.Add(new Frame { state = "IconSheet", file = "01_Icon_5x5.png", width = 2000, height = 1500 });
            report.frames.Add(new Frame { state = "LayerSheet", file = "02_ShipLayer_5x5.png", width = 2000, height = 1500 });
            for (int rarity = 0; rarity < 5; rarity++)
                report.frames.Add(new Frame { state = "Shop" + rarity, file = "03_Shop_" + (ComponentRarity)rarity + ".png" });
            report.frames.Add(new Frame { state = "Dossier", file = "04_Dossier_Legendary.png" });
            report.frames.Add(new Frame { state = "Equipment", file = "05_Equipment_AllRarities.png" });
            report.frames.Add(new Frame { state = "Resale", file = "06_Resale_AllRarities.png" });
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); }
                catch (Exception exception) { frame.error = exception.ToString(); Debug.LogWarning(frame.file + ": " + exception.Message); }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("RARITY_HALO_FRAME: " + frame.file + " " + (frame.error == null ? "OK" : "FAILED"));
            }
            WriteGallery(output, report);
            if (report.frames.Any(frame => frame.error != null || frame.halos == 0 || !frame.nonInteractiveHalos))
                throw new InvalidOperationException("Rarity validation incomplete; inspect report.json.");
            Debug.Log("RARITY_HALO_CAPTURE_OK: " + report.frames.Count + " frames in " + output);
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Rarity Halo Review") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(frame.state == "Equipment" ? CoreGameplayPreviewScreen.Shipyard : CoreGameplayPreviewScreen.ComponentShop,
                    CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                if (frame.state == "Equipment") controller.ConfigureEditorShipyard(0, true, 2);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                var catalog = (ComponentArtCatalog)Get(controller, "componentArt");
                if (frame.state.EndsWith("Sheet", StringComparison.Ordinal)) BuildSheet(controller, canvas, catalog, frame);
                else BuildScreen(controller, canvas, catalog, frame);
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                var cameraHost = new GameObject("Disposable Rarity Halo Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.03f, .06f, .075f, 1f); camera.orthographic = true;
                camera.orthographicSize = frame.height * .5f; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.aspect = frame.width / (float)frame.height;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(frame.width, frame.height); canvasRect.position = Vector3.zero;
                canvasRect.rotation = Quaternion.identity; canvasRect.localScale = Vector3.one;
                for (int pass = 0; pass < 4; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                Graphic[] halos = canvas.GetComponentsInChildren<Graphic>(false)
                    .Where(item => item.GetType().Name == "ComponentRarityHaloGraphic").ToArray();
                frame.halos = halos.Length; frame.nonInteractiveHalos = halos.All(item => !item.raycastTarget);
                if (frame.halos == 0) throw new InvalidOperationException("No active production halo graphics found.");
                if (!frame.nonInteractiveHalos) throw new InvalidOperationException("Halo graphics must not intercept pointer input.");
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void BuildSheet(CoreGameplayDemoController controller, Canvas canvas, ComponentArtCatalog catalog, Frame frame)
        {
            Clear(canvas.transform);
            Font font = (Font)Get(controller, "interfaceFont");
            bool layers = frame.state == "LayerSheet";
            TextLabel(canvas.transform, "Title", layers ? "部件稀有度 · 展示图光圈" : "部件稀有度 · 图标光圈", font,
                new Vector2(0, 696), new Vector2(1900, 58), 34, new Color(.97f, .91f, .79f));
            for (int rarity = 0; rarity < 5; rarity++)
            {
                TextLabel(canvas.transform, "Rarity " + rarity, ComponentArtCatalog.RarityName((ComponentRarity)rarity), font,
                    new Vector2(-760 + rarity * 380, 634), new Vector2(350, 42), 28, ComponentArtCatalog.RarityColor((ComponentRarity)rarity));
                for (int slot = 0; slot < 5; slot++)
                {
                    ComponentArtEntry entry = catalog.ExpandedEntries.First(item => (int)item.Rarity == rarity && (int)item.Slot == slot);
                    frame.samples.Add(entry.Id);
                    Vector2 cellPosition = new Vector2(-760 + rarity * 380, 475 - slot * 250);
                    RectTransform cell = Rect(canvas.transform, "Sample " + entry.Id, new Vector2(354, 230), cellPosition);
                    var background = cell.gameObject.AddComponent<Image>();
                    background.color = slot % 2 == 0 ? new Color(.055f, .09f, .10f, 1) : new Color(.13f, .12f, .10f, 1);
                    background.raycastTarget = false;
                    if (layers)
                    {
                        RectTransform display = Rect(cell, "Sample Display " + entry.Id, new Vector2(390, 422), new Vector2(0, 16));
                        display.localScale = Vector3.one * .47f;
                        Invoke(controller, "BuildComponentShopDisplay", display, entry, "Sample " + entry.Id + " ", string.Empty,
                            false, false, false, false, false, null, null, null);
                    }
                    else
                        Invoke(controller, "CreateComponentIcon", "Sample Art " + entry.Id, cell, entry, new Vector2(0, 16), 144f, false);
                    TextLabel(cell, "Sample Name " + entry.Id, entry.Name, font, new Vector2(0, -88), new Vector2(336, 35), 23,
                        new Color(.95f, .90f, .79f));
                }
            }
            TextLabel(canvas.transform, "Footer", "船首 / 船帆 / 火炮 / 装甲 / 仪器 · 原始资源与生产界面光圈", font,
                new Vector2(0, -691), new Vector2(1900, 42), 22, new Color(.71f, .73f, .70f));
        }

        private static void BuildScreen(CoreGameplayDemoController controller, Canvas canvas, ComponentArtCatalog catalog, Frame frame)
        {
            var samples = Enumerable.Range(0, 5).Select(rarity => catalog.ExpandedEntries.First(entry =>
                entry.Slot == ComponentSlot.Bow && (int)entry.Rarity == rarity)).ToArray();
            frame.samples.AddRange(samples.Select(entry => entry.Id));
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "rarity-review-" + serial++);
            inventory.EnsureLoadout(0);
            OwnedShipComponent selected = null;
            foreach (ComponentArtEntry entry in samples) selected = inventory.Grant(entry);
            Set(controller, "componentInventory", inventory);
            if (frame.state == "Equipment")
            {
                Set(controller, "selectedShipyardComponentInstanceId", selected.InstanceId);
                Set(controller, "selectedShipyardComponentSlot", ComponentSlot.Bow);
                Set(controller, "shipyardInventoryShowAll", true); Set(controller, "shipyardInventoryPage", 0);
                Invoke(controller, "BuildSelectedEditorPreview");
                return;
            }
            RectTransform panel = canvas.GetComponentsInChildren<RectTransform>(true).First(item => item.name == "Component Shop Panel");
            Clear(panel);
            if (frame.state == "Resale") { Invoke(controller, "BuildComponentResale", panel); return; }
            List<PortComponentShopStockItem> stock = samples.Select(entry => new PortComponentShopStockItem(
                "rarity-halo-review|" + entry.Id, controller.Session.CurrentPort, "2026-10", entry, entry.Price)).ToList();
            if (frame.state == "Dossier")
            {
                Set(controller, "selectedComponentShopOfferId", stock[4].Id);
                Invoke(controller, "BuildComponentShopDetail", panel, stock[4]);
                Invoke(controller, "RefreshFacilityReturnCaption");
                return;
            }
            int rarityIndex = int.Parse(frame.state.Substring(4));
            Set(controller, "componentShopCategory", (int)ComponentSlot.Bow); Set(controller, "componentShopDisplayPage", 0);
            Set(controller, "componentShopShowroomOfferId", stock[rarityIndex].Id);
            Invoke(controller, "BuildComponentShopShowroom", panel, stock, controller.Session.GameDateTime, 0);
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false); result.anchorMin = result.anchorMax = result.pivot = new Vector2(.5f, .5f);
            result.sizeDelta = size; result.anchoredPosition = position; return result;
        }
        private static void TextLabel(Transform parent, string name, string value, Font font, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            Text label = Rect(parent, name, size, position).gameObject.AddComponent<Text>();
            label.font = font; label.text = value; label.fontSize = fontSize; label.color = color;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        }
        private static void Clear(Transform parent)
        { for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject); }
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void WriteGallery(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>部件稀有度光圈</title><style>body{background:#101b1f;color:#eee4ce;font:16px system-ui;margin:28px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}img{width:100%;height:auto}a{color:#bce}figcaption{padding:10px 0}</style><h1>部件稀有度光圈</h1><p>真实美术资源与生产界面。五档稀有度 × 五类部件，商店、详情、装配、回售。</p><main>");
            foreach (Frame frame in report.frames)
                html.Append("<figure><a href='").Append(frame.file).Append("'><img loading='lazy' src='").Append(frame.file)
                    .Append("'></a><figcaption>").Append(frame.state).Append(" · ").Append(frame.halos).Append(" halos")
                    .Append(frame.error == null ? "" : " · FAILED").Append("</figcaption></figure>");
            html.Append("</main></html>");
            File.WriteAllText(Path.Combine(output, "review.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}