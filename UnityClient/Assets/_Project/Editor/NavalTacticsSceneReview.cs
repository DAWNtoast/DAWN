using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Single maintained scene and actual-render review for the tactical naval board.</summary>
    public static class NavalTacticsSceneReview
    {
        public const string ScenePath = "Assets/_Project/Scenes/UIPreviews/UI_Battle.unity";
        private const int Width = 1920, Height = 1080, Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Serializable] private sealed class Report
        {
            public string capturedAtUtc;
            public string scope = "Actual Unity rendering of the maintained naval presenter. Temporary session, no player-profile writes, no retired preview generation.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string file, caption, background;
            public float distanceMeters, cameraRotationDegrees, cellSizePixels;
            public int actionPoints, plannedSteps;
            public bool awaitingHeading;
            public Vector2 playerScreenPosition, enemyScreenPosition;
        }

        [MenuItem("Shattered Sail/Scenes/Refresh Tactical Naval Battle Preview")]
        public static void SaveBattlePreviewFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) SaveBattlePreviewFromCommandLine();
        }

        // This deliberately saves exactly one current UIPreviews scene.
        public static void SaveBattlePreviewFromCommandLine()
        {
            RequireStoppedEditor();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "UI_Battle";
            GameObject cameraHost = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .06f, .075f);
            camera.orthographic = true;
            cameraHost.tag = "MainCamera";
            GameObject host = new GameObject("Naval Tactical Battle Preview");
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.TwilightTide, 2);
            EnterNaval(controller);
            RequireBoard(controller);
            controller.PrepareEditorPreviewForSceneSave();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            Scene reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CoreGameplayDemoController reopenedController = reopened.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (!reopenedController.HasCompleteEditorPreview || reopenedController.Session.Battle == null ||
                reopenedController.Session.Battle.Stage != NavalBattleStage.Naval)
                throw new InvalidOperationException("Saved battle scene did not reopen directly in its complete naval preview.");
            RequireBoard(reopenedController);
            Debug.Log("NAVAL_TACTICS_SCENE_SAVED: " + ScenePath);
            Debug.Log("NAVAL_TACTICS_SCENE_REOPENED: complete tactical naval scene");
        }

        public static void CaptureFromCommandLine()
        {
            RequireStoppedEditor();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Capture requires graphics; omit -nographics.");
            string output = ReadOutput();
            Scene scene = EditorSceneManager.NewPreviewScene();
            EventSystemState selection = new EventSystemState();
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            GameObject host = null;
            try
            {
                host = new GameObject("Naval Tactical Review") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.TwilightTide, 2);
                host.SetActive(true);
                EnterNaval(controller);
                SetReviewHand(controller.Session);
                Render(controller);
                report.frames.Add(Capture(controller, scene, output, "01-initial-grid.png", "开场六格、船头相对；无坐标标识的网格与扇形手牌"));

                // Actual cards grant points; only a selected, committed route moves the ship.
                PlayReviewCard(controller, "card_turn_port");
                PlayReviewCard(controller, "card_sail_forward");
                AppendReviewStep(controller, new VoyagePoint(-500f, 0f));
                AppendReviewStep(controller, new VoyagePoint(-500f, 100f));
                report.frames.Add(Capture(controller, scene, output, "02-selected-route.png", "行动点3：玩家选择向东、向北两格路线，确认前不消耗行动点、不移动"));
                CommitReviewRoute(controller);
                report.frames.Add(Capture(controller, scene, output, "03-arrival-heading.png", "移动清空全部行动点；到达后选择东、北、西朝向，完成定向前禁用卡牌与结束回合"));
                ConfirmReviewHeading(controller, 90f);

                // Fresh real 600 m opening; AP cards fund a legal cell-by-cell northern route.
                // Fixture energy is replenished to isolate navigation from enemy turn decisions.
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.TwilightTide, 2);
                EnterNaval(controller);
                for (int step = 0; step < 11; step++) PlayReviewCard(controller, "card_sail_forward");
                AppendReviewStep(controller, new VoyagePoint(-500f, 0f));
                for (int north = 1; north <= 6; north++) AppendReviewStep(controller, new VoyagePoint(-500f, north * 100f));
                for (int east = -4; east <= 0; east++) AppendReviewStep(controller, new VoyagePoint(east * 100f, 600f));
                CommitReviewRoute(controller);
                ConfirmReviewHeading(controller, 0f);
                report.frames.Add(Capture(controller, scene, output, "04-northern-flank.png", "从600米开场经12个合法网格到达敌方北侧，航行与定向完成；我方始终居左，罗盘东在上"));
                Transform hoverCard = controller.GetComponentsInChildren<Transform>(true).First(item => item.name == "Battle Card 2");
                ExecuteEvents.Execute(hoverCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                report.frames.Add(Capture(controller, scene, output, "05-flank-hover.png", "北侧阵位的实际悬停状态：卡牌上浮、转正、置顶高亮；航行牌提供行动点，海域与罗盘保留世界方向"));
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
                selection.Restore();
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            var html = new StringBuilder("<!doctype html><meta charset=\"utf-8\"><title>海战战棋场景验收</title><style>body{background:#09222c;color:#eee0ba;font:18px sans-serif;margin:24px}img{width:100%;max-width:1600px}figure{margin:24px 0}figcaption{padding:10px}</style><h1>海战战棋 · Unity 实际场景</h1>");
            foreach (Frame frame in report.frames) html.Append("<figure><img src=\"").Append(frame.file).Append("\"><figcaption>").Append(frame.caption).Append("</figcaption></figure>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
            Debug.Log("NAVAL_TACTICS_CAPTURE_COMPLETE: " + output);
        }

        private static void EnterNaval(CoreGameplayDemoController controller)
        {
            if (controller.Session.Battle == null) throw new InvalidOperationException("Battle preview did not create a session.");
            if (controller.Session.Battle.Stage == NavalBattleStage.Preparation && !controller.Session.EndBattleTurn())
                throw new InvalidOperationException("Could not leave preparation.");
            if (controller.Session.Battle.Stage != NavalBattleStage.Naval) throw new InvalidOperationException("Expected naval stage.");
            Render(controller);
        }

        private static void SetReviewHand(CoreVoyageSession session)
        {
            session.Battle.Hand.Clear();
            foreach (string id in new[] { "card_turn_port", "card_sail_forward", "card_turn_starboard", "card_turn_about", "card_chain_shot" })
                session.Battle.Hand.Add(CardIndex(id));
            session.Battle.Energy = session.Battle.MaxEnergy;
            session.Battle.CardsPlayedThisTurn = 0;
        }

        private static void PlayReviewCard(CoreGameplayDemoController controller, string id)
        {
            CoreVoyageSession session = controller.Session;
            session.Battle.Hand.Clear();
            int cardIndex = CardIndex(id);
            session.Battle.Hand.Add(cardIndex);
            session.Battle.Energy = Math.Max(session.Battle.MaxEnergy, session.GetBattleCardEnergyCost(cardIndex));
            session.Battle.CardsPlayedThisTurn = 0;
            if (!session.PlayCard(0)) throw new InvalidOperationException("Review card failed: " + id);
            SetReviewHand(session);
            Render(controller);
        }

        private static int CardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .First(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static void AppendReviewStep(CoreGameplayDemoController controller, VoyagePoint point)
        {
            if (!controller.Session.TryAppendNavalMovementStep(point, out string reason)) throw new InvalidOperationException(reason);
            controller.GetComponentInChildren<NavalTacticalMovementInput>().RefreshNow();
        }

        private static void CommitReviewRoute(CoreGameplayDemoController controller)
        {
            if (!controller.Session.CommitNavalMovement(out string reason)) throw new InvalidOperationException(reason);
            Render(controller);
        }

        private static void ConfirmReviewHeading(CoreGameplayDemoController controller, float heading)
        {
            if (!controller.Session.ConfirmNavalArrivalHeading(heading, out string reason)) throw new InvalidOperationException(reason);
            Render(controller);
        }

        private static void Render(CoreGameplayDemoController controller) => typeof(CoreGameplayDemoController)
            .GetMethod("Render", Private).Invoke(controller, null);

        private static Component RequireBoard(CoreGameplayDemoController controller)
        {
            Transform board = controller.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "Naval Tactical Board");
            Component view = board == null ? null : board.GetComponents<Component>().FirstOrDefault(item => item.GetType().Name == "NavalTacticalBoardView");
            if (view == null) throw new InvalidOperationException("Naval Tactical Board view is missing.");
            return view;
        }

        private static T ReadBoard<T>(Component board, string name) => (T)board.GetType().GetProperty(name).GetValue(board);

        private static Frame Capture(CoreGameplayDemoController controller, Scene scene, string output, string file, string caption)
        {
            Canvas canvas = controller.GetComponentInChildren<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            canvas.renderMode = RenderMode.WorldSpace;
            rect.sizeDelta = new Vector2(Width, Height);
            rect.position = Vector3.zero;
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
            GameObject cameraHost = new GameObject("Naval Tactical Review Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .06f, .075f);
            camera.orthographic = true;
            camera.orthographicSize = Height * .5f;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.cullingMask = 1 << Layer;
            camera.allowHDR = camera.allowMSAA = false;
            canvas.worldCamera = camera;
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                for (int i = 0; i < 2; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (MonoBehaviour behaviour in canvas.GetComponentsInChildren<MonoBehaviour>(true))
                        if (behaviour.isActiveAndEnabled && behaviour.GetType().Name == "BattleCardHoverFeedback")
                            behaviour.GetType().GetMethod("RefreshInspectionLayoutImmediately").Invoke(behaviour, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int i = 0; i < samples.Length; i += 997)
                {
                    int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3;
                    minimum = Math.Min(minimum, brightness);
                    maximum = Math.Max(maximum, brightness);
                }
                if (maximum - minimum < 20) throw new InvalidOperationException("Captured naval UI appears blank: " + file);
                File.WriteAllBytes(Path.Combine(output, file), pixels.EncodeToPNG());
                Component board = RequireBoard(controller);
                RawImage background = canvas.GetComponentsInChildren<RawImage>(true).FirstOrDefault(item => item.name == "Shared tactical sea");
                if (background == null || background.texture == null) throw new InvalidOperationException("Shared tactical background is missing.");
                return new Frame { file = file, caption = caption, distanceMeters = controller.Session.Battle.DistanceMeters,
                    actionPoints = controller.Session.Battle.NavalActionPoints,
                    plannedSteps = controller.Session.NavalMovementPlannedSteps,
                    awaitingHeading = controller.Session.Battle.NavalAwaitingHeadingSelection,
                    background = background == null || background.texture == null ? "" : background.texture.name,
                    cameraRotationDegrees = ReadBoard<float>(board, "CameraRotationDegrees"),
                    cellSizePixels = ReadBoard<float>(board, "CellSizePixels"),
                    playerScreenPosition = ReadBoard<Vector2>(board, "PlayerScreenPosition"),
                    enemyScreenPosition = ReadBoard<Vector2>(board, "EnemyScreenPosition") };
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                if (pixels != null) Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cameraHost);
            }
        }

        private static string ReadOutput()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navalTacticsCapturePath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("Pass -navalTacticsCapturePath NEW_ABSOLUTE_ARTIFACTS_DIRECTORY.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(item => item.Equals("Artifacts", StringComparison.OrdinalIgnoreCase)) || output.Contains("UIPreviewsForMe"))
                throw new ArgumentException("Capture destination must be under Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Choose a new or empty capture directory.");
            Directory.CreateDirectory(output);
            return output;
        }

        private static void RequireStoppedEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
        }

        private sealed class EventSystemState
        {
            private readonly UnityEngine.EventSystems.EventSystem previous = UnityEngine.EventSystems.EventSystem.current;
            private readonly GameObject selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            public void Restore()
            {
                if (previous == null) return;
                UnityEngine.EventSystems.EventSystem.current = previous;
                previous.SetSelectedGameObject(selected);
            }
        }
    }
}
