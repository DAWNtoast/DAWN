using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Reproducible, non-playing visual acceptance for the regional facilities.
    /// Uses a private preview scene: no open scene, save game, or project asset
    /// is rewritten. Run with graphics enabled (do not pass -nographics).
    /// </summary>
    public static class RegionalPortSceneAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int CaptureLayer = 31;

        private static readonly CoreGameplayPreviewScreen[] Screens =
        {
            CoreGameplayPreviewScreen.Tavern,
            CoreGameplayPreviewScreen.ComponentShop,
            CoreGameplayPreviewScreen.Maintenance,
            CoreGameplayPreviewScreen.TavernCrewDetail,
            CoreGameplayPreviewScreen.ComponentShopDetail,
        };

        [Serializable]
        private sealed class AcceptanceReport
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public int expectedRegionalBackgrounds = PortFacilityArtCatalog.ExpectedRegionalBackgroundCount;
            public int availableRegionalBackgrounds;
            public bool allRegionalBackgroundsAvailable;
            public List<string> missingBackgrounds = new List<string>();
            public List<string> duplicateBackgrounds = new List<string>();
            public List<BackgroundRecord> backgrounds = new List<BackgroundRecord>();
            public List<CaptureRecord> captures = new List<CaptureRecord>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class BackgroundRecord
        {
            public string port;
            public int facility;
            public string resourcePath;
            public string assetPath;
            public string sha256;
            public int width;
            public int height;
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string port;
            public string screen;
            public string file;
            public bool completePreview;
        }

        [MenuItem("Shattered Sail/Art Review/Capture All Regional Facilities and Dossiers")]
        public static void CaptureFromMenu()
        {
            string path = Capture(ResolveOutputDirectory());
            Debug.Log("Regional port visual review is ready: " + path);
            EditorUtility.RevealInFinder(Path.Combine(path, "index.html"));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.RegionalPortSceneAcceptance.CaptureFromCommandLine
        /// Optional: -regionalCapturePath Artifacts/RegionalPorts/review-01
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            string path = Capture(ResolveOutputDirectory());
            Debug.Log("REGIONAL_PORT_ACCEPTANCE: " + path);
        }

        public static void ValidateAssetsFromCommandLine()
        {
            AcceptanceReport report = InspectBackgroundAssets();
            string directory = ResolveOutputDirectory();
            Directory.CreateDirectory(directory);
            WriteReport(directory, report);
            RequireCompleteArt(report);
            Debug.Log("REGIONAL_PORT_ART_VALIDATION: 18/18 distinct resource files are available. " + directory);
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before capturing editor previews.");
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException("Screenshot capture requires a graphics device. Remove -nographics.");
            }

            string directory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(directory);
            AcceptanceReport report = InspectBackgroundAssets();
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;
            GameObject host = null;
            GameObject cameraHost = null;
            try
            {
                host = new GameObject("Regional Facility Acceptance Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, previewScene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                host.SetActive(true);

                cameraHost = new GameObject("Regional Facility Acceptance Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << CaptureLayer;
                camera.scene = previewScene;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.name = "Regional Facility Acceptance Target";
                target.Create();
                camera.targetTexture = target;

                foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                {
                    foreach (CoreGameplayPreviewScreen screen in Screens)
                    {
                        CaptureRecord record = new CaptureRecord
                        {
                            port = PortName(port),
                            screen = screen.ToString(),
                            file = port + "_" + screen + ".png",
                        };
                        report.captures.Add(record);
                        try
                        {
                            controller.ConfigureEditorPreview(screen, port, 0);
                            record.completePreview = controller.HasCompleteEditorPreview;
                            if (!record.completePreview)
                            {
                                throw new InvalidOperationException("The editor preview did not build all required objects.");
                            }
                            Canvas canvas = host.GetComponentInChildren<Canvas>();
                            if (canvas == null) throw new InvalidOperationException("No preview Canvas was created.");

                            // A world-space canvas with an orthographic capture
                            // camera is independent of the current Game View size.
                            canvas.renderMode = RenderMode.WorldSpace;
                            canvas.worldCamera = camera;
                            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                            canvasRect.sizeDelta = new Vector2(Width, Height);
                            canvasRect.position = Vector3.zero;
                            canvasRect.localScale = Vector3.one;
                            foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true))
                            {
                                item.gameObject.layer = CaptureLayer;
                            }
                            Canvas.ForceUpdateCanvases();
                            Render(camera, target);
                            SaveFrame(target, Path.Combine(directory, record.file));
                        }
                        catch (Exception exception)
                        {
                            record.completePreview = false;
                            report.errors.Add(record.port + " / " + record.screen + ": " + exception.Message);
                            Debug.LogException(exception);
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                EditorSceneManager.ClosePreviewScene(previewScene);
                WriteReport(directory, report);
                WriteGallery(directory, report);
            }

            if (report.errors.Count > 0)
            {
                throw new InvalidOperationException("Regional review captured with " + report.errors.Count + " errors. See " + Path.Combine(directory, "report.json"));
            }
            RequireCompleteArt(report);
            return directory;
        }

        private static void Render(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                // URP in this project supports the engine StandardRequest API.
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            }
            else
            {
                camera.Render();
            }
        }

        private static void SaveFrame(RenderTexture target, string filePath)
        {
            RenderTexture.active = target;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                // A successful render call must not be mistaken for a useful
                // screenshot when a null/unsupported pipeline yields a blank.
                Color32[] pixels = image.GetPixels32();
                int darkest = 255;
                int brightest = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    Color32 pixel = pixels[index];
                    int brightness = (pixel.r + pixel.g + pixel.b) / 3;
                    darkest = Math.Min(darkest, brightness);
                    brightest = Math.Max(brightest, brightness);
                }
                if (brightest - darkest < 20)
                {
                    throw new InvalidOperationException("Captured frame is nearly uniform; the UI may not have rendered.");
                }
                File.WriteAllBytes(filePath, image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static AcceptanceReport InspectBackgroundAssets()
        {
            PortFacilityArtCatalog catalog = new PortFacilityArtCatalog();
            AcceptanceReport report = new AcceptanceReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                availableRegionalBackgrounds = catalog.LoadedRegionalBackgroundCount,
                allRegionalBackgroundsAvailable = catalog.AreAllRegionalBackgroundsAvailable,
                missingBackgrounds = catalog.MissingRegionalBackgroundPaths().ToList(),
            };
            Dictionary<string, string> hashOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (int facility in PortFacilityArtCatalog.SupportedRegionalFacilities)
            {
                foreach (string port in PortFacilityArtCatalog.SupportedRegionalPorts)
                {
                    Texture2D texture = catalog.RegionalBackground(facility, port);
                    BackgroundRecord item = new BackgroundRecord
                    {
                        port = port,
                        facility = facility,
                        resourcePath = PortFacilityArtCatalog.RegionalBackgroundResourcePath(facility, port),
                        assetPath = texture == null ? string.Empty : AssetDatabase.GetAssetPath(texture),
                        width = texture == null ? 0 : texture.width,
                        height = texture == null ? 0 : texture.height,
                    };
                    report.backgrounds.Add(item);
                    if (string.IsNullOrEmpty(item.assetPath)) continue;
                    string absolutePath = Path.Combine(ProjectRoot(), item.assetPath);
                    using (SHA256 sha = SHA256.Create())
                    using (FileStream source = File.OpenRead(absolutePath))
                    {
                        item.sha256 = BitConverter.ToString(sha.ComputeHash(source)).Replace("-", string.Empty).ToLowerInvariant();
                    }
                    if (hashOwners.TryGetValue(item.sha256, out string firstOwner))
                    {
                        report.duplicateBackgrounds.Add(firstOwner + " == " + item.resourcePath);
                    }
                    else
                    {
                        hashOwners.Add(item.sha256, item.resourcePath);
                    }
                }
            }
            return report;
        }

        private static void RequireCompleteArt(AcceptanceReport report)
        {
            if (!report.allRegionalBackgroundsAvailable || report.duplicateBackgrounds.Count > 0)
            {
                throw new InvalidOperationException("Regional art is incomplete: " + report.availableRegionalBackgrounds + "/18 backgrounds; " +
                    report.duplicateBackgrounds.Count + " byte-identical duplicates. Fallback images are not accepted as new regional art.");
            }
        }

        private static void WriteReport(string directory, AcceptanceReport report)
        {
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        private static void WriteGallery(string directory, AcceptanceReport report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>港口区域美术验收</title>");
            html.Append("<style>body{background:#152129;color:#f4e8cd;font:16px system-ui;margin:32px}section{display:grid;grid-template-columns:repeat(3,minmax(260px,1fr));gap:16px}figure{margin:0;background:#253640;padding:10px}img{width:100%;height:auto}figcaption{padding:8px 0}a{color:#edba63}h2{margin-top:36px}</style>");
            html.Append("<h1>六港场景与档案验收</h1><p>1920 × 1080 · 非运行状态直接构建 · ").Append(report.availableRegionalBackgrounds).Append("/18 独立背景。</p>");
            html.Append("<p><a href=\"report.json\">查看缺图、同文件复用与捕获结果</a>。文件哈希只检查重复文件，是否非换色重绘仍需逐图人工审核。</p>");
            foreach (string port in PortFacilityArtCatalog.SupportedRegionalPorts)
            {
                html.Append("<h2>").Append(port).Append("</h2><section>");
                foreach (CaptureRecord record in report.captures.Where(item => item.port == port))
                {
                    html.Append("<figure><a href=\"").Append(record.file).Append("\"><img loading=\"lazy\" src=\"").Append(record.file).Append("\"></a><figcaption>")
                        .Append(record.screen).Append(record.completePreview ? "" : " · 捕获失败").Append("</figcaption></figure>");
                }
                html.Append("</section>");
            }
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string ResolveOutputDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index += 1)
            {
                if (arguments[index] == "-regionalCapturePath")
                {
                    return Path.GetFullPath(Path.Combine(ProjectRoot(), arguments[index + 1]));
                }
            }
            return Path.Combine(ProjectRoot(), "Artifacts", "RegionalPorts", "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        private static string PortName(CoreGameplayPreviewPort port)
        {
            return PortFacilityArtCatalog.SupportedRegionalPorts[(int)port];
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }
    }
}
