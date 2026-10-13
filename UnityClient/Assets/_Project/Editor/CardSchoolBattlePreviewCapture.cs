using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class CardSchoolBattlePreviewCapture
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public string scope = "Arranged real production battle UIs, not a simulated playthrough. School state and life-payment tooltip at 1920x1080 and 1280x720; isolated preview scenes and no player-save writes.";
            public bool partialArtPreview;
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string file, state, status, error, paymentTooltip, paymentUnavailableReason;
            public int width, height;
            public bool captured, statusFits, schoolPanelVisible, paymentTooltipVisible, paymentTooltipFits;
            public float scaleFactor;
            public List<string> handCards = new List<string>();
            public List<string> missingArtwork = new List<string>();
        }

        // -executeMethod ShatteredSail.Editor.CardSchoolBattlePreviewCapture.CaptureFromCommandLine
        // -cardSchoolCapturePath D:/Game/Artifacts/<fresh directory>
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped editor with graphics required.");
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            if (!project.StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Isolated validation project required.");
            string[] args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-cardSchoolCapturePath");
            if (option < 0 || option + 1 >= args.Length || !Path.IsPathRooted(args[option + 1])) throw new ArgumentException("Absolute -cardSchoolCapturePath required.");
            string output = Path.GetFullPath(args[option + 1]);
            if (!output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Artifacts output required.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use a fresh output directory.");
            Directory.CreateDirectory(output); CardSchoolArtCatalog.ReleaseLoadedTextures();
            var report = new Report { partialArtPreview = Array.IndexOf(args, "-cardSchoolAllowMissingArt") >= 0 };
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                foreach (string state in new[] { "Naval", "Boarding", "BloodPaymentBlocked" })
                {
                    var frame = new Frame { width = size.x, height = size.y, state = state, file = state + "-" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { Capture(output, frame); }
                    catch (Exception error) { frame.error = error.ToString(); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                }
            if (report.frames.Any(frame => !frame.captured || !frame.statusFits || !frame.schoolPanelVisible || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("School battle preview failed; inspect report.json.");
            if (!report.partialArtPreview && report.frames.Any(frame => frame.missingArtwork.Count > 0))
                throw new InvalidOperationException("School battle preview has missing card illustrations; inspect report.json.");
            Debug.Log((report.partialArtPreview ? "CARD_SCHOOL_BATTLE_PARTIAL_PREVIEW_OK: " : "CARD_SCHOOL_BATTLE_PREVIEW_OK: ") + report.frames.Count);
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default; RenderTexture target = null; Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("School Battle Capture Host") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                CoreVoyageSession session = controller.Session;
                CoreDemoPhase phase = session.Phase;
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Port);
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
                NavalBattleStage stage = frame.state == "Naval" ? NavalBattleStage.Naval : NavalBattleStage.Boarding;
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", PrivateInstance).Invoke(session, new object[] { stage });
                CoreBattleState battle = session.Battle;
                battle.DistanceMeters = 100f; battle.Alignment = NavalBattleAlignment.Parallel; battle.Energy = battle.MaxEnergy = 10;
                session.Cargo.Powder = session.Cargo.Cannonballs = 20; session.Cargo.Medicine = 6;
                battle.Hand.Clear(); battle.DrawPile.Clear(); battle.DiscardPile.Clear();
                foreach (string school in CardSchoolArtCatalog.SchoolIds)
                {
                    int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i =>
                    {
                        CoreBattleCard card = CoreVoyageSession.CardLibrary[i];
                        return card.SchoolId == school && card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 && card.IsAllowedInStage(stage) &&
                            (school != "blood" || card.SchoolEffect.Mechanic == CoreSchoolMechanic.BloodRisk);
                    });
                    CoreBattleCard selected = CoreVoyageSession.CardLibrary[index];
                    battle.Hand.Add(index); frame.handCards.Add(selected.Id);
                    if (CardSchoolArtCatalog.Illustration(selected, null) == null) frame.missingArtwork.Add(selected.ArtId);
                }
                SetState(battle, "Mana", 12); SetState(battle, "CurseMarks", 3); SetState(battle, "CurseTurns", 2);
                SetState(battle, "CurseWeaknessPercent", 5); SetState(battle, "ThunderChain", 4);
                SetState(battle, "TideCounter", 18); SetState(battle, "TideCounterMinimum", 6); SetState(battle, "TideCounterTurns", 2);
                SetState(battle, "TideStance", 2); SetState(battle, "TideStancePower", 6); SetState(battle, "TideStanceTurns", 1);
                SetState(battle, "NaturalFireGuard", 8); SetState(battle, "NaturalFireGuardTurns", 1);
                SetState(battle, "ScorchDamage", 10); SetState(battle, "ScorchCharges", 2);
                battle.EnemyBurnDamage = 8; battle.EnemyBurnTurns = 3;
                if (frame.state == "BloodPaymentBlocked")
                    battle.PlayerBoardingHealth = session.GetBattleCard(battle.Hand[5]).SchoolEffect.Threshold;
                typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance).Invoke(controller, null);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                if (frame.state == "BloodPaymentBlocked")
                {
                    Transform bubble = canvas.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Card Keyword Bubble 5");
                    // Complete the production hover targets for a stopped-editor
                    // still; no update ticks run in this preview scene.
                    Component feedback = bubble.parent.GetComponent("BattleCardHoverFeedback");
                    ExecuteEvents.Execute<IPointerEnterHandler>(bubble.parent.gameObject,
                        new PointerEventData(null), ExecuteEvents.pointerEnterHandler);
                    if (feedback != null)
                    {
                        var cardRect = (RectTransform)bubble.parent;
                        cardRect.anchoredPosition = (Vector2)feedback.GetType().GetField("targetPosition", PrivateInstance).GetValue(feedback);
                        cardRect.localScale = Vector3.one;
                        cardRect.localRotation = Quaternion.Euler(0f, 0f, -.7f);
                    }
                    bubble.gameObject.SetActive(true);
                    // Editor capture has no hover animation ticks. Show its completed
                    // production hover state, including the fade group's final alpha.
                    bubble.GetComponent<CanvasGroup>().alpha = 1f;
                    Canvas hovered = bubble.parent.GetComponent<Canvas>();
                    hovered.overrideSorting = true; hovered.sortingOrder = 30;
                    frame.paymentUnavailableReason = session.CardUnavailableReason(5);
                }
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                var cameraHost = new GameObject("School Battle Capture Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene); Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5f; camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f); camera.cullingMask = 1 << 30;
                camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
                camera.targetTexture = target; camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty(); Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target }); else camera.Render();
                }
                Text status = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Battle School Status");
                frame.status = status.text; frame.schoolPanelVisible = status.gameObject.activeInHierarchy;
                frame.statusFits = status.preferredHeight <= status.rectTransform.rect.height + .1f;
                if (frame.state == "BloodPaymentBlocked")
                {
                    Transform bubble = canvas.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Card Keyword Bubble 5");
                    Text body = bubble.Find("Keyword Bubble Text").GetComponent<Text>();
                    frame.paymentTooltip = body.text;
                    frame.paymentTooltipVisible = bubble.gameObject.activeInHierarchy && bubble.GetComponent<CanvasGroup>().alpha == 1f;
                    frame.paymentTooltipFits = body.preferredHeight <= body.rectTransform.rect.height + .1f;
                    if (!frame.paymentTooltipVisible || !frame.paymentTooltipFits || !frame.paymentTooltip.Contains("生命不足") || string.IsNullOrEmpty(frame.paymentUnavailableReason))
                        throw new InvalidOperationException("The life-payment refusal tooltip must be visible, complete and explain the blocked card.");
                }
                frame.scaleFactor = canvas.scaleFactor;
                if (Mathf.Abs(frame.scaleFactor - frame.width / 1920f) > .01f) throw new InvalidOperationException("Unexpected production canvas scale.");
                RenderTexture.active = target; pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG()); frame.captured = true;
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void SetState(CoreBattleState battle, string property, int value) =>
            typeof(CoreSchoolBattleState).GetProperty(property).SetValue(battle.Schools, value);
    }
}
