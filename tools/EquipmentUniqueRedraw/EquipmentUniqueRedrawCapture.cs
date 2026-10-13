// Temporary review utility. Copy only to the disposable validation project's Editor folder.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class EquipmentUniqueRedrawCapture
    {
        private const int Layer = 31, Width = 1920, Height = 1080;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc;
            public string scope = "Actual production builders in disposable in-memory scenes: 150 equipment dossiers, 50 showroom pages and 38 inventory pages. No player saves, scene saves or package build.";
            public List<Frame> frames = new List<Frame>();
            public List<Asset> assets = new List<Asset>();
        }
        [Serializable] public sealed class Frame
        {
            public string state, id, file, error;
            public int route, cell, page;
            public bool scalerVerified;
            public List<Art> visibleArt = new List<Art>();
        }
        [Serializable] public sealed class Art
        {
            public string name, texture, assetPath, filter;
            public int width, height;
            public Rect uv, screenRect;
            public float sourceAspect, displayAspect;
        }
        [Serializable] public sealed class Asset
        {
            public string path, guid, sha256, graphicsFormat, filter;
            public int width, height, mipmapCount;
            public bool alpha;
        }
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("GPU required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-artPolishCapturePath");
            if (key < 0 || key + 1 >= args.Length || !Path.IsPathRooted(args[key + 1])) throw new ArgumentException("Pass absolute -artPolishCapturePath.");
            string output = Path.GetFullPath(args[key + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use an empty capture directory.");
            Directory.CreateDirectory(output);
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            var catalog = new ComponentArtCatalog();
            ComponentArtEntry[] entries = catalog.ExpandedEntries.OrderBy(e => e.Id).ToArray();
            if (entries.Length != 150 || entries.Select(e => e.ArtSourceId).Distinct().Count() != 150) throw new InvalidOperationException("Expected 150 independent equipment identities.");
            foreach (var entry in entries) report.frames.Add(new Frame { state = "Detail", id = entry.ArtSourceId, file = "detail_" + entry.ArtSourceId + ".png" });
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot))) for (int page = 0; page < 10; page++)
                report.frames.Add(new Frame { state = "Shop", id = slot.ToString(), page = page, file = "shop_" + slot + "_" + page + ".png" });
            for (int page = 0; page < 38; page++) report.frames.Add(new Frame { state = "Inventory", id = "All", page = page, file = "inventory_" + page.ToString("D2") + ".png" });
            bool smoke = Array.IndexOf(args, "-artPolishSmoke") >= 0;
            if (smoke)
            {
                report.scope = "Development smoke captures using currently installed assets; not final art acceptance.";
                report.frames = report.frames.Where(f => f.state == "Detail" ? f.id == "plating_corkwave_liner" || f.id == "bow_oak_ram" : f.state == "Shop" ? f.id == "Cannon" && f.page == 0 : f.page == 0).ToList();
            }
            foreach (var frame in report.frames)
            {
                try { Capture(output, frame, entries); }
                catch (Exception ex) { frame.error = ex.ToString(); Debug.LogWarning(frame.file + ": " + ex.Message); }
                Save(output, report);
            }
            if (!smoke)
            {
                foreach (var entry in entries) VerifyAsset(report, "Assets/_Project/Resources/ModularShip/Layers/" + entry.ArtSourceId + ".png", 1024, 1024, true);
                foreach (var entry in entries) VerifyAsset(report, "Assets/_Project/Resources/ComponentArt/" + entry.ArtSourceId + ".png", 512, 512, true);
            }
            Save(output, report);
            var html = new StringBuilder("<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>150件独立装备实际界面验收</title><style>body{background:#12252e;color:#eee3c9;font:16px sans-serif;margin:28px}img{width:100%;max-width:1440px}section{margin-bottom:30px}</style><h1>150件独立装备 · 实际界面验收</h1><p>150件部件详情、50页陈列、38页配件库存。1920×1080，生产界面构建器，临时场景，未出包。</p>");
            foreach (var f in report.frames) html.Append("<section><h2>").Append(f.file).Append("</h2><img loading='lazy' src='").Append(f.file).Append("'><p>").Append(System.Net.WebUtility.HtmlEncode(f.error)).Append("</p></section>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.Append("</html>").ToString(), new UTF8Encoding(false));
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error) || !f.scalerVerified)) throw new InvalidOperationException("Capture incomplete; inspect report.");
            Debug.Log("EQUIPMENT_UNIQUE_REDRAW_CAPTURE_OK: " + report.frames.Count + " frames; " + report.assets.Count + " imported assets.");
        }
        private static void Save(string output, Report report) => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        private static void VerifyAsset(Report report, string path, int width, int height, bool needsAlpha)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != width || texture.height != height) throw new InvalidOperationException("Imported resolution mismatch: " + path);
            bool alpha = GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
            if (needsAlpha && !alpha) throw new InvalidOperationException("Imported alpha missing: " + path);
            if (texture.filterMode != FilterMode.Bilinear) throw new InvalidOperationException("Painted asset must use bilinear sampling: " + path);
            if (texture.mipmapCount <= 1) throw new InvalidOperationException("Equipment minification mipmaps missing: " + path);
            using (var sha = SHA256.Create()) report.assets.Add(new Asset { path = path, guid = AssetDatabase.AssetPathToGUID(path), width = texture.width, height = texture.height, mipmapCount = texture.mipmapCount, alpha = alpha, graphicsFormat = texture.graphicsFormat.ToString(), filter = texture.filterMode.ToString(), sha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path))))).Replace("-", "").ToLowerInvariant() });
        }
        private static void Capture(string output, Frame frame, ComponentArtEntry[] entries)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Art Polish Review"); host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                bool equipment = frame.state == "Shop" || frame.state == "Detail";
                controller.ConfigureEditorPreview(equipment ? CoreGameplayPreviewScreen.ComponentShop : CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewPort.GrayGull, Math.Min(frame.route, 2));
                host.SetActive(true);
                if (equipment) ConfigureEquipment(controller, frame, entries);
                else ConfigureInventory(controller, frame, entries);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                var cameraHost = new GameObject("Disposable Art Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f; camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer; camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture = target; camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); typeof(CanvasScaler).GetMethod("Handle", Private).Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty(); Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target }); else camera.Render();
                }
                float expected = Mathf.Min(Width / scaler.referenceResolution.x, Height / scaler.referenceResolution.y);
                frame.scalerVerified = scaler.enabled && Mathf.Abs(canvas.renderingDisplaySize.x - Width) < .5f && Mathf.Abs(canvas.renderingDisplaySize.y - Height) < .5f && Mathf.Abs(canvas.scaleFactor - expected) < .001f;
                if (!frame.scalerVerified) throw new InvalidOperationException("Production scaler mismatch.");
                foreach (RawImage image in canvas.GetComponentsInChildren<RawImage>(false))
                {
                    string asset = image.texture == null ? "" : AssetDatabase.GetAssetPath(image.texture);
                    if (!(asset.Contains("/ModularShip/Layers/") || asset.Contains("/ComponentArt/")) || image.color.a < .01f) continue;
                    Vector3[] corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                    Vector2[] screen = corners.Select(c => (Vector2)camera.WorldToScreenPoint(c)).ToArray();
                    float sourceAspect = image.texture.width * image.uvRect.width / (image.texture.height * image.uvRect.height);
                    float displayAspect = image.rectTransform.rect.width / image.rectTransform.rect.height;
                    if (Mathf.Abs(sourceAspect - displayAspect) > .002f) throw new InvalidOperationException("Painted subject stretched: " + image.name);
                    frame.visibleArt.Add(new Art { name = image.name, texture = image.texture.name, assetPath = asset, width = image.texture.width, height = image.texture.height, filter = image.texture.filterMode.ToString(), uv = image.uvRect, sourceAspect = sourceAspect, displayAspect = displayAspect, screenRect = Rect.MinMaxRect(screen.Min(v => v.x), screen.Min(v => v.y), screen.Max(v => v.x), screen.Max(v => v.y)) });
                }
                if (frame.visibleArt.Count == 0) throw new InvalidOperationException("Expected artwork missing.");
                RenderTexture.active = target; pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false); pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; if (pixels != null) Object.DestroyImmediate(pixels); if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); if (target != null) { target.Release(); Object.DestroyImmediate(target); } }
        }
        private static void ConfigureEquipment(CoreGameplayDemoController controller, Frame frame, ComponentArtEntry[] entries)
        {
            RectTransform panel = Find<RectTransform>(controller, "Component Shop Panel"); Clear(panel);
            Func<ComponentArtEntry, PortComponentShopStockItem> offer = e => new PortComponentShopStockItem("art-review|" + e.ArtSourceId, controller.Session.CurrentPort, PortComponentShopCatalog.MonthKey(controller.Session.GameDateTime), e, 1f, 200);
            if (frame.state == "Detail") Invoke(controller, "BuildComponentDossier", panel, offer(entries.Single(e => e.ArtSourceId == frame.id)));
            else
            {
                ComponentSlot slot = (ComponentSlot)Enum.Parse(typeof(ComponentSlot), frame.id);
                var stock = entries.Where(e => e.Slot == slot).Select(offer).ToList();
                typeof(CoreGameplayDemoController).GetField("componentShopDisplayPage", Private).SetValue(controller, frame.page);
                Invoke(controller, "BuildComponentShopShowroom", panel, stock, controller.Session.GameDateTime, 0);
            }
        }
        private static void ConfigureInventory(CoreGameplayDemoController controller, Frame frame, ComponentArtEntry[] entries)
        {
            var catalog = (ComponentArtCatalog)typeof(CoreGameplayDemoController).GetField("componentArt", Private).GetValue(controller);
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "art-review-" + serial++);
            inventory.EnsureLoadout(0);
            foreach (var entry in entries) inventory.Grant(catalog.GetById(entry.Id), 1f);
            typeof(CoreGameplayDemoController).GetField("componentInventory", Private).SetValue(controller, inventory);
            typeof(CoreGameplayDemoController).GetField("shipyardInventoryPage", Private).SetValue(controller, frame.page);
            typeof(CoreGameplayDemoController).GetField("shipyardInventoryShowAll", Private).SetValue(controller, true);
            typeof(CoreGameplayDemoController).GetField("shipyardDossierTab", Private).SetValue(controller, ShipyardDossierTab.Equipment);
            typeof(CoreGameplayDemoController).GetField("shipyardDetailOpen", Private).SetValue(controller, true);
            typeof(CoreGameplayDemoController).GetField("selectedShipPreview", Private).SetValue(controller, 0);
            RectTransform panel = Find<RectTransform>(controller, "Shipyard Panel"); Clear(panel);
            Invoke(controller, "BuildShipyardDossier", panel, 0);
        }
        private static T Find<T>(Component host, string name) where T : Component => host.GetComponentsInChildren<T>(true).First(x => x.name == name);
        private static void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject); }
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    }
}
