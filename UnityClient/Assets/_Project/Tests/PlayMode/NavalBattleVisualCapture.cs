using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Opt-in screenshots of live test coroutines, not mockups or player saves.
    /// Pass -navalVisualCapturePath followed by a NEW Artifacts subdirectory to
    /// the isolated Unity test process. Ordinary test runs do not write files.
    /// </summary>
    internal static class NavalBattleVisualCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int CaptureLayer = 31;
        private static bool resolved;
        private static string directory;
        private static readonly CaptureReport Report = new CaptureReport();

        [Serializable]
        private sealed class CaptureReport
        {
            public string scope = "Actual Unity PlayMode battle UI captured during the visual-feedback regression tests. Only isolated in-memory fixtures are arranged; each action uses the real UI callback and combat rules.";
            public int width = Width;
            public int height = Height;
            public List<Frame> frames = new List<Frame>();
        }

        [Serializable]
        private sealed class Frame
        {
            public string file;
            public string caption;
            public string capturedAtUtc;
            public bool animationPlaying;
            public string stage;
            public float distanceMeters;
            public string alignment;
            public int playerHull;
            public int enemyHull;
            public int playerBlock;
            public int enemyBlock;
            public float displayedEnemyHealth;
            public Rect backdropUv;
            public List<string> visibleEffects = new List<string>();
        }

        public static void Capture(CoreGameplayDemoController controller, string fileName, string caption, string rootName = "Battle Card")
        {
            ResolveDirectory();
            if (directory == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Battle visual captures need a graphics device; do not use -nographics.");
            if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected a simple PNG filename.", nameof(fileName));
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) throw new IOException("A capture already exists: " + path);
            GameObject battleRoot = GameObject.Find(rootName);
            if (battleRoot == null) throw new InvalidOperationException("The live UI is missing: " + rootName);
            Canvas canvas = battleRoot.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerEnabled = scaler != null && scaler.enabled;
            RenderMode originalMode = canvas.renderMode;
            Camera originalCamera = canvas.worldCamera;
            Vector2 originalSize = rect.sizeDelta;
            Vector3 originalPosition = rect.localPosition;
            Quaternion originalRotation = rect.localRotation;
            Vector3 originalScale = rect.localScale;
            float originalPlaneDistance = canvas.planeDistance;
            RenderTexture originalActive = RenderTexture.active;
            Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
            int[] originalLayers = descendants.Select(child => child.gameObject.layer).ToArray();
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                if (scaler != null) scaler.enabled = false;
                cameraHost = new GameObject("Battle Visual Test Capture Camera", typeof(Camera));
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
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                rect.sizeDelta = new Vector2(Width, Height);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                foreach (Transform child in descendants) child.gameObject.layer = CaptureLayer;
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int index = 0; index < samples.Length; index += 997)
                {
                    int brightness = (samples[index].r + samples[index].g + samples[index].b) / 3;
                    minimum = Math.Min(minimum, brightness);
                    maximum = Math.Max(maximum, brightness);
                }
                if (maximum - minimum < 20) throw new InvalidOperationException("Captured UI appears blank: " + fileName);
                using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] data = pixels.EncodeToPNG();
                    stream.Write(data, 0, data.Length);
                }
                CoreBattleState battle = controller == null ? null : controller.Session.Battle;
                RawImage background = GameObject.Find("Battle Scene Art")?.GetComponent<RawImage>();
                Image enemyHealth = GameObject.Find("Enemy Battle Hull Bar")?.GetComponent<Image>();
                Frame frame = new Frame
                {
                    file = fileName,
                    caption = caption,
                    capturedAtUtc = DateTime.UtcNow.ToString("O"),
                    animationPlaying = controller != null && controller.IsBattleAnimationPlaying,
                    stage = battle == null ? (controller == null ? "MainMenu" : controller.Session.Phase.ToString()) : battle.Stage.ToString(),
                    distanceMeters = battle == null ? 0f : battle.DistanceMeters,
                    alignment = battle == null ? string.Empty : battle.Alignment.ToString(),
                    playerHull = controller == null ? 0 : controller.Session.Hull,
                    enemyHull = battle == null ? 0 : battle.EnemyHull,
                    playerBlock = battle == null ? 0 : battle.PlayerBlock,
                    enemyBlock = battle == null ? 0 : battle.EnemyBlock,
                    displayedEnemyHealth = enemyHealth == null ? 0f : enemyHealth.fillAmount,
                    backdropUv = background == null ? default : background.uvRect,
                };
                frame.visibleEffects.AddRange(descendants.Where(child => child.gameObject.activeInHierarchy &&
                        (child.name.Contains("FX") || child.name.Contains("Flash") || child.name.Contains("Smoke") || child.name.Contains("Impact") || child.name.Contains("Projectile")))
                    .Select(child => child.name).Distinct());
                Report.frames.Add(frame);
                WriteReport();
            }
            finally
            {
                RenderTexture.active = originalActive;
                for (int index = 0; index < descendants.Length; index++)
                    if (descendants[index] != null) descendants[index].gameObject.layer = originalLayers[index];
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                rect.sizeDelta = originalSize;
                rect.localPosition = originalPosition;
                rect.localRotation = originalRotation;
                rect.localScale = originalScale;
                if (scaler != null) scaler.enabled = scalerEnabled;
                Canvas.ForceUpdateCanvases();
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void ResolveDirectory()
        {
            if (resolved) return;
            resolved = true;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navalVisualCapturePath");
            if (index < 0) return;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("-navalVisualCapturePath needs a new Artifacts subdirectory.");
            string destination = Path.GetFullPath(args[index + 1]);
            if (!destination.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(segment => string.Equals(segment, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Battle test captures must be placed underneath Artifacts.");
            if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
                throw new IOException("Choose a new or empty capture directory: " + destination);
            Directory.CreateDirectory(destination);
            directory = destination;
        }

        private static void WriteReport()
        {
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(Report, true), new UTF8Encoding(false));
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>海战动态反馈验收</title><style>body{margin:28px;background:#10212b;color:#f3e4c5;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,560px),1fr));gap:22px}figure{margin:0;padding:12px;background:#263941;border:1px solid #705c3e}img{width:100%;display:block}figcaption,p{line-height:1.7}small{display:block;color:#c9c0ad}a{color:#f1c471}</style><h1>海战动态反馈验收</h1><p>实际 Unity PlayMode 截帧 · 1920 × 1080<br>运行中的出牌、炮击、护盾与镜头动画；所有数据均来自隔离测试，不修改玩家存档。<br><a href=\"report.json\">结构化报告</a></p><main>");
            foreach (Frame frame in Report.frames.OrderBy(item => item.file, StringComparer.Ordinal))
            {
                html.Append("<figure><a href=\"").Append(Escape(frame.file)).Append("\"><img loading=\"lazy\" src=\"")
                    .Append(Escape(frame.file)).Append("\" alt=\"").Append(Escape(frame.caption)).Append("\"></a><figcaption>")
                    .Append(Escape(frame.caption)).Append("<small>").Append(Escape(frame.stage)).Append(" · ")
                    .Append(frame.distanceMeters.ToString("0")).Append(" 米 · ").Append(frame.animationPlaying ? "动画中" : "动作完成")
                    .Append(" · 敌舰船体 ").Append(frame.enemyHull).Append(" · 格挡 ").Append(frame.enemyBlock)
                    .Append("</small></figcaption></figure>");
            }
            html.Append("</main></html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value) =>
            (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
