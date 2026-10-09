using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Material coverage audit and supplemental previews. Never invokes player actions or saves.</summary>
    public static class NauticalSurfaceAcceptance
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-surfaceCapturePath");
            if (index < 0 || index + 1 == args.Length) throw new ArgumentException("-surfaceCapturePath required");
            string directory = Path.GetFullPath(args[index + 1]);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new IOException("Use a new directory.");
            Directory.CreateDirectory(directory);
            var audit = new StringBuilder("Significant untextured Images (including intentional dimmers and artwork-covered backing plates):\n");
            string[] names = { "MainMenu", "MainMenu_Settings", "MainMenu_Achievements", "MainMenu_Confirmation", "PortSettings", "Tutorial" };
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>界面材质 · 补充检查</title><style>body{background:#181914;color:#eaddbd;font:18px/1.7 system-ui;max-width:1500px;margin:32px auto;padding:24px}img{width:100%;display:block}figure{margin:32px 0}a{color:#e6ba70}</style><h1>界面材质 · 补充检查</h1><p>实际 Unity 渲染；临时非运行预览，仅打开只读展示面板，不更改存档。</p>");
            for (int i = 0; i < names.Length; i++)
            {
                Capture(directory, names[i], i, audit);
                html.Append("<figure><img src='").Append(names[i]).Append(".png'><figcaption>").Append(names[i]).Append("</figcaption></figure>");
            }
            AuditCorePreviews(audit);
            File.WriteAllText(Path.Combine(directory, "surface-audit.txt"), audit.ToString());
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString());
            if (Array.IndexOf(args, "-uiPreviewCapturePath") >= 0) UIPreviewSceneAcceptance.CaptureFromCommandLine();
        }

        private static void AuditCorePreviews(StringBuilder report)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var host = new GameObject("Material Audit"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                foreach (CoreGameplayPreviewScreen screen in Enum.GetValues(typeof(CoreGameplayPreviewScreen)))
                {
                    controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0); host.SetActive(true);
                    Canvas.ForceUpdateCanvases(); Audit(host, screen.ToString(), report);
                }
            }
            finally { Object.DestroyImmediate(host); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Audit(GameObject host, string name, StringBuilder report)
        {
            report.Append("\n[").Append(name).Append("]\n");
            foreach (Image image in host.GetComponentsInChildren<Image>(true).Where(i => i.sprite == null && i.color.a > .01f && i.rectTransform.rect.width >= 100 && i.rectTransform.rect.height >= 35))
                report.Append(image.name).Append(" | ").Append(image.rectTransform.rect.size).Append(" | ").Append(image.color).Append(" | children: ")
                    .Append(string.Join(", ", image.transform.Cast<Transform>().Select(t => t.name))).Append('\n');
        }

        private static void Capture(string directory, string name, int scenario, StringBuilder audit)
        {
            Scene scene = EditorSceneManager.NewPreviewScene(); GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            TutorialVoyageController tutorial = null;
            try
            {
                host = new GameObject("Material Acceptance") { hideFlags = HideFlags.HideAndDontSave }; host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                if (scenario < 4)
                {
                    host.AddComponent<MainMenuController>(); host.SetActive(true);
                    if (scenario > 0)
                    {
                        string modal = new[] { "Settings Panel", "Achievements Panel", "New Game Confirmation" }[scenario - 1];
                        if (modal == "Achievements Panel")
                            host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements").onClick.Invoke();
                        host.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == modal).gameObject.SetActive(true);
                    }
                }
                else if (scenario == 4)
                {
                    var controller = host.AddComponent<CoreGameplayDemoController>();
                    controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull, 0); host.SetActive(true);
                    typeof(CoreGameplayDemoController).GetMethod("OpenPortSettings", Private).Invoke(controller, null);
                }
                else
                {
                    tutorial = host.AddComponent<TutorialVoyageController>();
                    Type type = typeof(TutorialVoyageController);
                    type.GetField("session", Private).SetValue(tutorial, new TutorialVoyageSession());
                    foreach (string key in new[] { "interfaceFont", "circleSprite", "seaSprite", "reefSprite" })
                    {
                        string method = key == "interfaceFont" ? "CreateInterfaceFont" : key == "circleSprite" ? "CreateCircleSprite" : key == "reefSprite" ? "CreateReefSprite" : "CreateSeaSprite";
                        type.GetField(key, Private).SetValue(tutorial, type.GetMethod(method, Private).Invoke(tutorial, null));
                    }
                    type.GetMethod("BuildInterface", Private).Invoke(tutorial, null);
                    type.GetMethod("Render", Private).Invoke(tutorial, null); host.SetActive(true);
                }
                Canvas canvas = host.GetComponentInChildren<Canvas>(true);
                cameraHost = new GameObject("Material Acceptance Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 540; camera.aspect = 1920f / 1080f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000; camera.transform.position = new Vector3(0, 0, -1000);
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
                Audit(host, name, audit);
                RenderTexture.active = target; var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                RenderTexture.active = previous;
                if (tutorial != null)
                    foreach (string key in new[] { "seaSprite", "circleSprite", "reefSprite", "seaTexture", "circleTexture" })
                    {
                        FieldInfo field = typeof(TutorialVoyageController).GetField(key, Private);
                        Object value = field.GetValue(tutorial) as Object; field.SetValue(tutorial, null);
                        if (value != null) Object.DestroyImmediate(value);
                    }
                if (cameraHost != null) Object.DestroyImmediate(cameraHost); if (host != null) Object.DestroyImmediate(host);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); } EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
