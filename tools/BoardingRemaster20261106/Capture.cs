using System;
using System.Collections.Generic;
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
    // Installed only into the leased verification project's Editor folder.
    public static class BoardingCapture20261106
    {
        private const int Width = 400, Height = 272, Frames = 12, Columns = 4, CaptureLayer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool deathsOnly;
        private static bool fourActions;
        private static readonly string[] Identities = { "imperial", "ghost", "route_reef", "route_gold", "route_moon",
            "treasure", "iron_oath", "thunder_crown", "red_widow", "ember_ark", "tide_requiem", "last_horizon", "moon_weave" };
        private static readonly string[] Encounters = { "gold_customs", "moon_ghost", "boss_route_shattered_sail_reef",
            "boss_route_golden_tide_trade", "boss_route_moon_tide_ruins", CoreBattleBalance.TreasureBossId,
            "legendary_pirate_iron_oath", "legendary_pirate_thunder_crown", "legendary_pirate_red_widow",
            "legendary_pirate_ember_ark", "legendary_pirate_tide_requiem", "legendary_pirate_last_horizon",
            "boss_route_moon_tide_ruins_arcane" };

        [Serializable] public sealed class Clip
        {
            public string identity, encounter, displayName, texture, action, sheet, actionAtlas;
            public float duration;
        }
        [Serializable] public sealed class CaptureReport
        {
            public string scope = "Named enemies come from the production encounter and boarding UI. Each rig is isolated on a plain background and its production action is sampled at twelve normalized times.";
            public int width = Width, height = Height, frames = Frames, columns = Columns;
            public List<Clip> clips = new List<Clip>();
        }

        public static void CaptureFromCommandLine()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\','/');
            if (project != "D:/Game/tmp/shipyard-assembly-20261017" || Application.isPlaying)
                throw new InvalidOperationException("A stopped, leased editor project is required.");
            string[] args = Environment.GetCommandLineArgs();
            deathsOnly = args.Contains("-boardingNamedDeathsOnly");
            fourActions = args.Contains("-boardingBossFourActions");
            string output = Path.GetFullPath(args[Array.IndexOf(args, "-navalVfxOutput") + 1]);
            string task = fourActions ? "boarding-boss-actions-20261106" : "boarding-remaster-20261106";
            if (!output.Replace('\\','/').StartsWith("D:/Game/outputs/"+task+"/validation/", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Output must be inside this task's validation directory.");
            var report = new CaptureReport();
            for (int index = 0; index < Identities.Length; index++)
            {
                CaptureEnemy(output, Identities[index], Encounters[index], report);
                File.WriteAllText(Path.Combine(output, "capture-report.json"), JsonUtility.ToJson(report, true));
            }
            if (report.clips.Count != (deathsOnly ? 13 : fourActions ? 52 : 65)) throw new InvalidOperationException("Incomplete enemy action capture.");
            Debug.Log("BOARDING_NAMED_ACTIONS_OK: " + report.clips.Count);
        }

        private static CoreEncounter Encounter(CoreVoyageSession session, string id)
        {
            if (id == CoreBattleBalance.TreasureBossId) return CoreLegendaryWarships.CreateTreasureEncounter();
            if (id.StartsWith("legendary_pirate_", StringComparison.Ordinal)) return CoreLegendaryWarships.Find(id).CreateEncounter();
            if (id.StartsWith("boss_route_", StringComparison.Ordinal)) return CoreRouteBossCatalog.Find(id).CreateEncounter();
            int route = id == "gold_customs" ? 1 : 2;
            var pool = (IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", Private)
                .Invoke(session, new object[] { CoreVoyageSession.Routes[route] });
            return pool.Single(item => item.Id == id);
        }

        private static void CaptureEnemy(string output, string key, string encounterId, CaptureReport report)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture previous = RenderTexture.active, target = null;
            Texture2D frame = null, sheet = null;
            try
            {
                var host = new GameObject("Disposable boarding session");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, (CoreGameplayPreviewPort)0, 0);
                host.SetActive(true);
                CoreVoyageSession session = controller.Session;
                CoreEncounter encounter = Encounter(session, encounterId);
                typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", Private).Invoke(session, new object[] { NavalBattleStage.Boarding });
                typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(controller, null);
                string identity = "enemy_" + key;
                BoardingActorGraphic actor = host.GetComponentsInChildren<BoardingActorGraphic>(true).Single(a => a.Identity == identity);
                RawImage image = actor.GetComponent<RawImage>();
                if (image.texture.name != "boarding_enemy_" + key + "_v1" || actor.UsesActionAtlas || !actor.Formation)
                    throw new InvalidOperationException("Unexpected encounter art or production rig for " + identity);
                Canvas.ForceUpdateCanvases();
                Vector2 nativeSize = image.rectTransform.rect.size;
                var canvasHost = new GameObject("Isolated production actor", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasHost, scene);
                var canvas = canvasHost.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = nativeSize;
                image.rectTransform.SetParent(canvasRect, false);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = Vector2.one * .5f;
                image.rectTransform.sizeDelta = nativeSize;
                image.rectTransform.anchoredPosition = Vector2.zero;
                foreach (Transform item in canvasHost.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = CaptureLayer;
                Camera camera = new GameObject("Actor preview camera", typeof(Camera)).GetComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .115f, .14f, 1f);
                camera.orthographic = true;
                Bounds motionBounds = new Bounds(Vector3.zero, nativeSize);
                foreach (BoardingActorAction action in Actions())
                {
                    actor.PlayAction(BoardingActorAction.Idle);
                    actor.PlayAction(action, 1f);
                    for (int i = 0; i < Frames; i++)
                    {
                        actor.SetPresentationTime(i / (float)(Frames - 1));
                        Canvas.ForceUpdateCanvases();
                        Mesh mesh = image.canvasRenderer.GetMesh();
                        if (mesh != null) motionBounds.Encapsulate(mesh.bounds);
                    }
                }
                camera.orthographicSize = Mathf.Max(motionBounds.size.y, motionBounds.size.x * Height / Width) * .56f;
                camera.transform.position = new Vector3(motionBounds.center.x, motionBounds.center.y, -500);
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 1000f;
                camera.cullingMask = 1 << CaptureLayer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.aspect = Width / (float)Height;
                canvas.worldCamera = camera;
                frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                sheet = new Texture2D(Width * Columns, Height * (Frames / Columns), TextureFormat.RGB24, false);
                foreach (BoardingActorAction action in Actions())
                {
                    float duration = action == BoardingActorAction.Death ? .9f : BoardingCardActorPresentation.Duration(action);
                    actor.PlayAction(BoardingActorAction.Idle);
                    actor.PlayAction(action, duration);
                    if (actor.Action != action) throw new InvalidOperationException(identity + " did not enter " + action);
                    if (fourActions && (action == BoardingActorAction.Death || image.texture == null ||
                        image.texture != Resources.Load<Texture2D>("BoardingBattle/BossActions/" + identity)))
                        throw new InvalidOperationException("Missing new boss action sheet for " + identity + " " + action);
                    if (deathsOnly && (action != BoardingActorAction.Death || !actor.UsesDefeatAtlas ||
                        image.texture != Resources.Load<Texture2D>("BoardingBattle/Defeats/" + identity)))
                        throw new InvalidOperationException("Missing painted defeat for " + identity);
                    for (int i = 0; i < Frames; i++)
                    {
                        actor.SetPresentationTime(i * duration / (Frames - 1));
                        Canvas.ForceUpdateCanvases();
                        if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                        else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target;
                        frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                        frame.Apply(false, false);
                        sheet.SetPixels32((i % Columns) * Width, (Frames / Columns - 1 - i / Columns) * Height,
                            Width, Height, frame.GetPixels32());
                    }
                    sheet.Apply(false, false);
                    string name = identity + "_" + action + ".png";
                    using (var file = new FileStream(Path.Combine(output, name), FileMode.CreateNew))
                    {
                        byte[] bytes = sheet.EncodeToPNG();
                        file.Write(bytes, 0, bytes.Length);
                    }
                    report.clips.Add(new Clip { identity = identity, encounter = encounterId, displayName = encounter.Name,
                        texture = image.texture.name, action = action.ToString(), sheet = name, duration = duration,
                        actionAtlas = fourActions ? "BoardingBattle/BossActions/" + identity : null });
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (frame != null) Object.DestroyImmediate(frame);
                if (sheet != null) Object.DestroyImmediate(sheet);
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static BoardingActorAction[] Actions() => deathsOnly ? new[] { BoardingActorAction.Death } :
            fourActions ? new[] { BoardingActorAction.Attack, BoardingActorAction.Hit, BoardingActorAction.Cast, BoardingActorAction.Guard } :
            new[] { BoardingActorAction.Attack, BoardingActorAction.Cast, BoardingActorAction.Hit, BoardingActorAction.Guard, BoardingActorAction.Death };
    }
}
