using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Deterministic sightline fixtures, not player saves or replacement preview scenes.</summary>
    public static class NavigationLookoutAcceptance
    {
        public static void CaptureReviewFromCommandLine()
        {
            CaptureFromCommandLine();
            UIPreviewSceneAcceptance.CaptureFromCommandLine();
            EncounterDossierAcceptance.CaptureFromCommandLine();
        }

        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-lookoutCapturePath");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("-lookoutCapturePath is required");
            string directory = Path.GetFullPath(args[index + 1]);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new IOException("Use a new capture directory.");
            Directory.CreateDirectory(directory);
            string[] names = { "01_Far_East", "02_Close_East", "03_Turn_West", "04_Storm_Occlusion" };
            for (int i = 0; i < names.Length; i++) Capture(directory, names[i], i);
            File.WriteAllText(Path.Combine(directory, "README.md"),
                "# 航向与距离验收\n\n隔离场景内的确定性视线测试数据；不是新增游戏事件，不写玩家存档。\n" +
                "相同世界位置：海盗船、岛屿、礁群和暴风。依次查看远处东向、向东接近、向西转身、雨幕遮挡。\n" +
                "验收船使用额外 180 海里视距（与船员视距加成走相同参数）。所有画面由真实 UI 与投影算法渲染，非拼贴效果图。\n");
        }

        private static void Capture(string directory, string name, int variant)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                host = new GameObject("Lookout Acceptance") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                CoreVoyageSession session = controller.Session;
                var map = new CoreVoyageMap { Route = CoreVoyageSession.Routes[0], Seed = "LOOKOUT-ACCEPTANCE", Hash = 314159,
                    Departure = new VoyagePoint(45, 32), Destination = new VoyagePoint(80, 32), DepartureName = "灰鸥港", DestinationName = "盐冠港" };
                for (int i = 0; i < map.CellCount; i++) map.ExploredCells[i] = true;
                for (int x = 28; x <= 30; x++) for (int y = 18; y <= 19; y++) map.TerrainCells[y * 48 + x] = SeaTerrainType.Island;
                for (int x = 28; x <= 29; x++) map.TerrainCells[14 * 48 + x] = SeaTerrainType.Reef;
                for (int x = 31; x <= 33; x++) map.TerrainCells[12 * 48 + x] = SeaTerrainType.StormFront;
                map.Nodes.Add(new CoreMapNode { Id = 777, Position = new VoyagePoint(57, 32), Discovered = true,
                    Encounter = new CoreEncounter("pirate_lookout_fixture", "黑帆海盗船", "验收目标", "仅用于视线验收", true, false, 2, 70) });
                if (variant == 3)
                    for (int x = 25; x <= 27; x++) for (int y = 15; y <= 17; y++) map.TerrainCells[y * 48 + x] = SeaTerrainType.StormFront;
                Set(session, "Map", map); Set(session, "Position", new VoyagePoint(variant == 1 ? 54 : 50, 32));
                Set(session, "Phase", CoreDemoPhase.Sailing);
                typeof(CoreVoyageSession).GetField("componentVisionBonus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 180f);
                session.SetHeading(variant == 2 ? SailingDirection.West : SailingDirection.East);
                typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
                host.GetComponentsInChildren<Text>().Single(x => x.name == "Title").text = "航海视野验收 · " + name;
                var observations = VoyageLookoutProjector.Project(map, session.Position, session.Heading, 120 + (session.HasNavigator ? 60 : 0) + session.VisionBonusNm);
                if (variant == 2 && observations.Count != 0) throw new InvalidOperationException("Behind-ship scenery leaked into westward view.");
                if (variant < 2 && !observations.Any(x => x.Kind == LookoutSceneryKind.PirateShip)) throw new InvalidOperationException("Visible pirate ship missing.");
                if (variant == 3 && observations.Any(x => x.Kind == LookoutSceneryKind.PirateShip)) throw new InvalidOperationException("Storm did not occlude pirate ship.");
                File.WriteAllText(Path.Combine(directory, name + ".txt"), string.Join("\n", observations.Select(x => x.Key + " / " + x.Kind + " / " + x.DistanceNm + "nm / x=" + x.Horizontal + " / scale=" + x.Scale + " / alpha=" + x.Visibility)));
                Canvas canvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName).GetComponent<Canvas>();
                cameraHost = new GameObject("Lookout Acceptance Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
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
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target;
                var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (host != null) Object.DestroyImmediate(host);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    }
}
