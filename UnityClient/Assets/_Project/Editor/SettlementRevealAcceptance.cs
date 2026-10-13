using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class SettlementRevealAcceptance
    {
        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-settlementCapturePath");
            if (index < 0 || index + 1 == args.Length) throw new ArgumentException("-settlementCapturePath is required");
            string directory = Path.GetFullPath(args[index + 1]);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new IOException("Use a new capture directory.");
            Directory.CreateDirectory(directory);
            string[] names = { "01_Arrival", "02_SafeReturn", "03_Failure", "04_Empty", "05_Materials", "06_LargeValue", "07_Reveal_060", "08_Reveal_120" };
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>远航结算 · 验收</title><style>body{background:#161611;color:#eadcb9;font:18px/1.7 system-ui;max-width:1500px;margin:32px auto;padding:24px}img{width:100%;display:block}figure{margin:32px 0}a{color:#efc878}</style><h1>远航结算 · 战利品揭晓</h1><p>实际 Unity 渲染；全部为明确标记的演示数据，不写入玩家存档。前六张为完成态，最后两张展示同一段有限演出的不同时间。</p>");
            for (int i = 0; i < names.Length; i++)
            {
                Capture(directory, names[i], i < 6 ? i : 0, i == 6 ? 0.6f : i == 7 ? 1.2f : SettlementRevealController.Duration);
                html.Append("<figure><a href='").Append(names[i]).Append(".png'><img loading='lazy' src='").Append(names[i]).Append(".png'></a><figcaption>").Append(names[i]).Append("</figcaption></figure>");
            }
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString());
            File.WriteAllText(Path.Combine(directory, "report.txt"), "8 / 8 captures completed. Temporary preview data, no scene or save writes.\n" + string.Join("\n", names));
            if (Array.IndexOf(args, "-uiPreviewCapturePath") >= 0) UIPreviewSceneAcceptance.CaptureFromCommandLine();
        }

        private static void Capture(string directory, string name, int scenario, float time)
        {
            Scene scene = EditorSceneManager.NewPreviewScene(); GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                host = new GameObject("Settlement Acceptance") { hideFlags = HideFlags.HideAndDontSave }; host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0); host.SetActive(true);
                Canvas canvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName).GetComponent<Canvas>();
                RectTransform content = (RectTransform)FixedGameViewport.Bounds(canvas).Find("Phase Content");
                foreach (Transform child in content.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                typeof(CoreGameplayDemoController).GetMethod("BuildSettlementPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { SettlementPreviewSamples.Create(scenario), true });
                host.GetComponentInChildren<SettlementRevealController>().SetElapsed(time);
                cameraHost = new GameObject("Settlement Acceptance Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 540; camera.aspect = 1920f / 1080f;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 2000; camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << 31; camera.scene = scene; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture = target;
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                RectTransform rect = canvas.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1920, 1080); rect.position = Vector3.zero; rect.localScale = Vector3.one;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty(); Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target }); else camera.Render();
                }
                RenderTexture.active = target; var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost); if (host != null) Object.DestroyImmediate(host);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); } EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
