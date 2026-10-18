// Install only in an isolated project's Editor folder. Production builders run
// in temporary PreviewScenes; no scene, prefab or profile is saved by this tool.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Core.Gameplay;
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
    public static class FleetRedrawCapture
    {
        private const int Layer = 31, Width = 1920, Height = 1080;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc, projectPath, shipShaderSha256;
            public string scope = "48 actual Unity UI frames: 12 player shipyard galleries and dossiers; six route bosses and six legendary ships in naval battle and encounter introduction. Production builders, fresh in-memory PreviewScenes, enabled CanvasScaler, no profile or scene writes.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string state, name, encounterId, file, error, expectedAssetPath, screenMatchMode;
            public int index, routeIndex, preludePage;
            public bool scalerVerified, expectedArtVerified, preludeFlowVerified, preludeBackgroundVerified;
            public string preludeTitle, preludeText, preludeBackgroundPath;
            public float scaleFactor;
            public Vector2 displaySize, canvasSize;
            public List<Art> shipArt = new List<Art>();
        }
        [Serializable] public sealed class Art
        {
            public string name, texture, assetPath, sourceSha256, filter, shader, graphicsFormat;
            public int width, height;
            public bool formatHasAlpha;
            public Rect uv, screenRect;
            public float waterContactY, useColorKey, smoothCutout;
        }

        // Repairs only stale import artifacts in this disposable validation project.
        public static void RefreshValidationImports()
        {
            foreach (string resource in new[] { "BoardingBattle/Backgrounds/boarding_fog_dusk_v1",
                "BoardingBattle/Characters/boarding_enemy_tide_requiem_v1", "PortArt/special_crew_sheet", "ComponentArt/bow_sawtooth_drift" })
            {
                string asset = "Assets/_Project/Resources/" + resource + ".png";
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Texture2D texture = Resources.Load<Texture2D>(resource);
                if (texture == null) throw new InvalidOperationException("Validation resource remains unavailable: " + asset);
                Debug.Log("FLEET_REIMPORT_OK: " + asset + " " + texture.width + "x" + texture.height);
            }
        }

        public static void CaptureFromCommandLine() => RunCapture(false);
        public static void CapturePreludeFromCommandLine() => RunCapture(true);

        private static void RunCapture(bool preludeOnly)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-fleetCapturePath");
            if (key < 0 || key + 1 >= args.Length || !Path.IsPathRooted(args[key + 1]))
                throw new ArgumentException("Pass -fleetCapturePath NEW_ABSOLUTE_DIRECTORY");
            string output = Path.GetFullPath(args[key + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Capture directory must be empty.");
            Directory.CreateDirectory(output);
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O"),
                projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                shipShaderSha256 = SourceHash("Assets/_Project/Resources/Shaders/BattleShipCutout.shader") };
            if (preludeOnly)
            {
                report.scope = "12 actual Unity legendary ship prelude frames: six ships, two pages each. A real in-memory treasure voyage enters its battle, Render builds the modal prelude, and the actual advance button opens page two. No profile or scene writes.";
                for (int index = 0; index < CoreLegendaryWarships.All.Count; index++)
                {
                    CoreLegendaryWarship ship = CoreLegendaryWarships.All[index];
                    for (int page = 0; page < CoreTreasureStoryCatalog.FindBattle(ship.Id).Pages.Count; page++)
                        report.frames.Add(new Frame { state = "LegendaryPrelude", index = index, preludePage = page,
                            encounterId = ship.Id, name = ship.Name, file = "Legendary_" + index.ToString("D2") + "_Prelude_" + (page + 1) + ".png",
                            expectedAssetPath = "Assets/_Project/Resources/" + SpecialWarshipArtCatalog.ResourcePath(ship.Id) + ".png" });
                }
            }
            else
            {
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index++)
                foreach (string state in new[] { "Shipyard", "ShipyardDetail" })
                    report.frames.Add(new Frame { state = state, index = index, name = CoreVoyageSession.Ships[index].Name,
                        file = state + "_" + index.ToString("D2") + ".png" });
            for (int index = 0; index < CoreRouteBossCatalog.All.Count; index++)
            {
                CoreRouteBoss boss = CoreRouteBossCatalog.All[index];
                int route = Enumerable.Range(0, CoreVoyageSession.Routes.Count).Single(i => CoreVoyageSession.Routes[i].Id == boss.RouteId);
                AddSpecialFrames(report, "Boss", index, route, boss.Id, boss.Name);
            }
            for (int index = 0; index < CoreLegendaryWarships.All.Count; index++)
            {
                CoreLegendaryWarship ship = CoreLegendaryWarships.All[index];
                AddSpecialFrames(report, "Legendary", index, 2, ship.Id, ship.Name);
            }
            }
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); }
                catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("FLEET_FRAME: " + frame.file + " " + (string.IsNullOrEmpty(frame.error) ? "OK" : "FAILED"));
            }
            WriteGallery(output, report);
            int expectedFrames = preludeOnly ? 12 : 48;
            if (report.frames.Count != expectedFrames || report.frames.Any(f => !string.IsNullOrEmpty(f.error) || !f.scalerVerified || !f.expectedArtVerified || (preludeOnly && (!f.preludeFlowVerified || !f.preludeBackgroundVerified))))
                throw new InvalidOperationException("Fleet capture incomplete; inspect report.json.");
            Debug.Log("FLEET_REDRAW_CAPTURE_OK: " + expectedFrames + " frames in " + output);
        }

        private static void AddSpecialFrames(Report report, string group, int index, int route, string id, string name)
        {
            foreach (string state in new[] { "Battle", "Introduction" })
                report.frames.Add(new Frame { state = group + state, index = index, routeIndex = route,
                    encounterId = id, name = name, file = group + "_" + index.ToString("D2") + "_" + state + ".png",
                    expectedAssetPath = "Assets/_Project/Resources/" + SpecialWarshipArtCatalog.ResourcePath(id) + ".png" });
        }

        private static string SourceHash(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path))))).Replace("-", "");
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                bool shipyard = frame.state.StartsWith("Shipyard", StringComparison.Ordinal);
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Fleet Review");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                CoreGameplayPreviewPort port = shipyard ? (CoreGameplayPreviewPort)CoreShipMarketCatalog.HomePortIndex(frame.index) : CoreGameplayPreviewPort.GrayGull;
                controller.ConfigureEditorPreview(shipyard ? CoreGameplayPreviewScreen.Shipyard : CoreGameplayPreviewScreen.Battle, port, frame.routeIndex);
                host.SetActive(true);
                if (shipyard)
                {
                    controller.ConfigureEditorShipyard(frame.index, frame.state == "ShipyardDetail");
                    Texture2D expected = frame.index == 0 ? Resources.Load<Texture2D>("ModularShip/light_sloop_base") : new PortArtCatalog().ShipTexture(frame.index);
                    frame.expectedAssetPath = AssetDatabase.GetAssetPath(expected);
                }
                else ConfigureSpecial(controller, frame);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler disabled.");
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                var cameraHost = new GameObject("Disposable Fleet Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.displaySize = canvas.renderingDisplaySize;
                frame.canvasSize = canvas.GetComponent<RectTransform>().rect.size; frame.scaleFactor = canvas.scaleFactor;
                float expectedScale = Mathf.Min(Width / scaler.referenceResolution.x, Height / scaler.referenceResolution.y);
                frame.screenMatchMode = scaler.screenMatchMode.ToString();
                frame.scalerVerified = scaler.enabled && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand &&
                    Mathf.Abs(frame.displaySize.x - Width) < .5f && Mathf.Abs(frame.displaySize.y - Height) < .5f &&
                    Mathf.Abs(frame.scaleFactor - expectedScale) < .001f && Mathf.Abs(frame.canvasSize.x - Width / expectedScale) < 1f &&
                    Mathf.Abs(frame.canvasSize.y - Height / expectedScale) < 1f;
                if (!frame.scalerVerified) throw new InvalidOperationException("Actual render target/scaler geometry mismatch.");
                foreach (RawImage image in canvas.GetComponentsInChildren<RawImage>(false))
                {
                    if (image.texture == null) continue;
                    string assetPath = AssetDatabase.GetAssetPath(image.texture);
                    bool expectedArt = assetPath == frame.expectedAssetPath;
                    if (!expectedArt && !image.name.Contains("Ship")) continue;
                    if (!expectedArt && !assetPath.Contains("ship_") && !assetPath.Contains("light_sloop")) continue;
                    var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                    Vector2[] screen = corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)).ToArray();
                    frame.shipArt.Add(new Art { name = image.name, texture = image.texture.name, assetPath = assetPath,
                        width = image.texture.width, height = image.texture.height, sourceSha256 = SourceHash(assetPath),
                        filter = image.texture.filterMode.ToString(), shader = image.material.shader.name, uv = image.uvRect,
                        graphicsFormat = image.texture.graphicsFormat.ToString(), formatHasAlpha = GraphicsFormatUtility.HasAlphaChannel(image.texture.graphicsFormat),
                        useColorKey = image.material.HasProperty("_UseColorKey") ? image.material.GetFloat("_UseColorKey") : -1f,
                        smoothCutout = image.material.HasProperty("_SmoothCutout") ? image.material.GetFloat("_SmoothCutout") : -1f,
                        screenRect = Rect.MinMaxRect(screen.Min(v => v.x), screen.Min(v => v.y), screen.Max(v => v.x), screen.Max(v => v.y)),
                        waterContactY = camera.WorldToScreenPoint(image.rectTransform.TransformPoint(SpecialWarshipArtCatalog.HullContactLocal(image))).y });
                }
                string expectedName = shipyard ? null : frame.state == "LegendaryPrelude" ? "Treasure Legendary Ship Art" : frame.state.EndsWith("Battle", StringComparison.Ordinal) ? "Enemy Ship Art" : "Demo Event Art";
                frame.expectedArtVerified = !string.IsNullOrEmpty(frame.expectedAssetPath) &&
                    frame.shipArt.Any(a => a.assetPath == frame.expectedAssetPath && (expectedName == null || a.name == expectedName));
                if (!frame.expectedArtVerified) throw new InvalidOperationException("The production UI did not display the intended ship: " + frame.expectedAssetPath);
                RenderTexture.active = target; pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
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

        private static void ConfigureSpecial(CoreGameplayDemoController controller, Frame frame)
        {
            var session = new CoreVoyageSession();
            typeof(CoreGameplayDemoController).GetField("session", Private).SetValue(controller, session);
            bool prelude = frame.state == "LegendaryPrelude";
            if (prelude)
            {
                string rank = CoreTreasureStoryCatalog.Chapters.Single(c => c.LegendaryIds.Contains(frame.encounterId)).RankId;
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10),
                    selectedMapId = "fleet-prelude", maps = new[] { new CoreTreasureMapSave { id = "fleet-prelude", seed = "FLEET-" + frame.encounterId, rankId = rank } } });
                if (!session.TryStartTreasureVoyage(out string reason)) throw new InvalidOperationException(reason);
            }
            else
            {
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                session.SelectRoute(frame.routeIndex); session.StartVoyage();
            }
            CoreEncounter encounter = CoreRouteBossCatalog.Find(frame.encounterId)?.CreateEncounter() ?? CoreLegendaryWarships.Find(frame.encounterId)?.CreateEncounter();
            if (encounter == null) throw new InvalidOperationException("Unknown special ship: " + frame.encounterId);
            CoreMapNode node = session.Map.Nodes.FirstOrDefault(n => n.Encounter.Id == encounter.Id);
            if (node == null) node = new CoreMapNode { Id = 990, Position = session.Position, Encounter = encounter, TerrainConstraint = EventTerrainConstraint.OpenSea };
            node.Encounter = encounter; node.Discovered = true; node.IntelRevealed = true;
            typeof(CoreVoyageSession).GetField("activeNode", Private).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            if (prelude || frame.state.EndsWith("Battle", StringComparison.Ordinal))
            {
                Invoke(session, "StartBattle", encounter, true);
                Invoke(session, "EnterBattleStage", NavalBattleStage.Naval);
                session.Battle.DistanceMeters = 120f; session.Battle.Alignment = NavalBattleAlignment.Parallel;
            }
            else typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            bool previous = (bool)typeof(CoreGameplayDemoController).GetField("editorPreviewBuilding", Private).GetValue(controller);
            typeof(CoreGameplayDemoController).GetField("editorPreviewBuilding", Private).SetValue(controller, true);
            try
            {
                Invoke(controller, "Render");
                if (prelude)
                {
                    for (int page = 0; page < frame.preludePage; page++)
                        controller.GetComponentsInChildren<Button>(false).Single(b => b.name == "Treasure Story Advance").onClick.Invoke();
                    frame.preludeTitle = controller.GetComponentsInChildren<Text>(false).Single(t => t.name == "Treasure Legendary Story Title").text;
                    frame.preludeText = controller.GetComponentsInChildren<Text>(false).Single(t => t.name == "Treasure Legendary Story Text").text;
                    ComicPageGraphic background = controller.GetComponentsInChildren<ComicPageGraphic>(false).Single(p => p.name == "Treasure Story Comic Page");
                    frame.preludeBackgroundPath = AssetDatabase.GetAssetPath(background.texture);
                    frame.preludeBackgroundVerified = frame.preludeBackgroundPath.StartsWith("Assets/_Project/Resources/BattleEnvironments/", StringComparison.Ordinal);
                    if (!frame.preludeBackgroundVerified) throw new InvalidOperationException("Legendary prelude must use a battle sea background: " + frame.preludeBackgroundPath);
                    TreasureBattleStory story = CoreTreasureStoryCatalog.FindBattle(frame.encounterId);
                    frame.preludeFlowVerified = session.IsTreasureVoyage && session.Phase == CoreDemoPhase.Battle &&
                        frame.preludeText == story.Pages[frame.preludePage] && frame.preludeTitle == story.ShipName + " · " + (frame.preludePage + 1) + " / " + story.Pages.Count;
                    if (!frame.preludeFlowVerified) throw new InvalidOperationException("Actual legendary prelude page did not match the requested story.");
                }
            }
            finally { typeof(CoreGameplayDemoController).GetField("editorPreviewBuilding", Private).SetValue(controller, previous); }
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, Private);
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            return info.Invoke(target, args);
        }

        private static void WriteGallery(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>Fleet redraw Unity verification</title><style>body{background:#10232c;color:#f1dec1;font:18px sans-serif;margin:28px}section{margin-bottom:32px}img{width:100%;max-width:1440px;height:auto;border:1px solid #776646}p{color:#c1c7c9}</style><h1>船只重绘 · 实际 Unity UI 验证</h1><p>1920×1080，临时内存场景，不写入存档。</p><p>").Append(System.Net.WebUtility.HtmlEncode(report.scope)).Append("</p>");
            foreach (Frame frame in report.frames)
                html.Append("<section><h2>").Append(frame.state).Append(" · ").Append(frame.index).Append(" · ").Append(System.Net.WebUtility.HtmlEncode(frame.name))
                    .Append("</h2><img loading='lazy' src='").Append(frame.file).Append("'><p>").Append(System.Net.WebUtility.HtmlEncode(frame.expectedAssetPath))
                    .Append("</p><p>").Append(System.Net.WebUtility.HtmlEncode(frame.error)).Append("</p></section>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.Append("</html>").ToString(), new UTF8Encoding(false));
        }
    }
}
