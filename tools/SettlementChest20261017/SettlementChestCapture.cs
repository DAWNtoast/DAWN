#if UNITY_EDITOR
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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class SettlementChestCapture
    {
        private const int Layer = 31;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report { public string scope; public List<Frame> frames = new List<Frame>(); public float animationDuration; public int animationFrames; public int animationFps = 15; }
        [Serializable] private sealed class Frame { public string state, file, phase; public int width, height; public float elapsed, duration; public bool complete, overviewInteractive, fireworks; public int visibleTexts; }
        public static void CaptureFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("A graphics device is required.");
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Missing -uiAuditPath.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.StartsWith(@"D:\Game\Artifacts\SettlementChest20261017\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Evidence must stay in this task's artifact directory.");
            Directory.CreateDirectory(output);
            var report = new Report { scope = "Disposable production UI only; no gameplay callbacks, scene saves or player-save changes. Keyframes use the enabled production CanvasScaler. GIF frames use silent deterministic SetElapsed; separate PlayMode tests verify automatic playback and transition." };
            foreach (string state in new[] { "Ordinary", "Jackpot", "Failure", "Empty", "Treasure", "Tutorial" })
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) })
            using (var rig = new Rig(Fixture(state), state == "Tutorial", size.x, size.y))
            {
                float time = rig.Sequence == null ? 0f : rig.Sequence.FireworksStart > 0 ? rig.Sequence.FireworksStart + .55f : 1.65f;
                string file = state + "_" + size.x + "x" + size.y + ".png";
                report.frames.Add(rig.Capture(Path.Combine(output, file), state, time));
                if (state == "Jackpot" && size.x == 1920)
                {
                    report.frames.Add(rig.Capture(Path.Combine(output, "Jackpot_Charge_1920x1080.png"), "JackpotCharge", rig.Sequence.JackpotStart + .8f));
                    report.frames.Add(rig.Capture(Path.Combine(output, "Overview_1920x1080.png"), "Overview", rig.Sequence.Duration));
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            string animation = Path.Combine(output, "animation"); Directory.CreateDirectory(animation);
            using (var rig = new Rig(Fixture("Jackpot"), false, 1280, 720))
            {
                report.animationDuration = rig.Sequence.Duration + 1.3f;
                report.animationFrames = Mathf.CeilToInt(report.animationDuration * report.animationFps);
                for (int frame = 0; frame < report.animationFrames; frame++)
                    rig.Capture(Path.Combine(animation, "frame_" + frame.ToString("D4") + ".png"), "JackpotAnimation", frame / (float)report.animationFps, false);
                if (!rig.Sequence.IsComplete) throw new InvalidOperationException("Animation did not end on the overview.");
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SETTLEMENT_CHEST_CAPTURE_OK: " + report.frames.Count + " keyframes and " + report.animationFrames + " animation frames at " + output);
        }

        private static VoyageSettlement Fixture(string state)
        {
            if (state == "Empty") return new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, Destination = "断锚港", Title = "安全返航" };
            var result = SettlementPreviewSamples.Create(state == "Failure" ? 2 : state == "Ordinary" ? 0 : 1);
            if (state == "Jackpot") { result.BankedGold = 12868; result.BankedWood = 64; result.BankedIron = 32; result.BankedFame = 87; result.BankedGem = 8; }
            if (state == "Treasure") { result.TreasureCompleted = true; result.Outcome = VoyageOutcome.TreasureRecovered; result.BankedGold = 1600; result.BankedFragments = 3; result.BankedPirateExperience = 125; result.BankedGem = 12; result.BankedAncientMaterial = 8; result.BankedRelic = 3; result.BankedBlackPearl = 2; }
            if (state == "Tutorial") { result.BankedGold = 10; result.BankedWood = 0; result.BankedIron = 0; result.BankedFame = 0; result.BankedFragments = 1; result.BankedPirateExperience = 15; }
            return result;
        }

        private sealed class Rig : IDisposable
        {
            private readonly Scene scene;
            private readonly GameObject host;
            private readonly CoreGameplayDemoController controller;
            private readonly Canvas canvas;
            private readonly CanvasScaler scaler;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly int width, height;
            private readonly decimal goldBefore;
            private readonly CoreDemoPhase phaseBefore;
            private readonly VoyageSettlement result;
            private readonly string rewardsBefore;
            public SettlementChestSequenceController Sequence { get; }
            public Rig(VoyageSettlement fixture, bool tutorial, int targetWidth, int targetHeight)
            {
                result = fixture; rewardsBefore = JsonUtility.ToJson(result); width = targetWidth; height = targetHeight;
                scene = EditorSceneManager.NewPreviewScene(); host = new GameObject("Disposable Settlement Chest Capture"); host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0); host.SetActive(true);
                canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                scaler = canvas.GetComponent<CanvasScaler>(); if (!scaler.enabled) throw new InvalidOperationException("Production scaler must remain enabled.");
                Transform content = FixedGameViewport.Bounds(canvas).Find("Phase Content");
                foreach (Transform child in content.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                goldBefore = controller.Session.ProfileGold; phaseBefore = controller.Session.Phase;
                if (tutorial)
                {
                    typeof(CoreGameplayDemoController).GetField("tutorialActive", Private).SetValue(controller, true);
                    typeof(CoreVoyageSession).GetProperty("GuidedTutorialLeg").GetSetMethod(true).Invoke(controller.Session, new object[] { CoreGuidedTutorialContent.BossVoyageLeg });
                    typeof(CoreVoyageSession).GetProperty("GuidedTutorialBossDefeated").GetSetMethod(true).Invoke(controller.Session, new object[] { true });
                }
                typeof(CoreGameplayDemoController).GetMethod("BuildSettlementPresentation", Private).Invoke(controller, new object[] { result, true });
                Sequence = host.GetComponentsInChildren<SettlementChestSequenceController>(true).SingleOrDefault();
                host.GetComponentsInChildren<SettlementRevealController>(true).Single().CompleteImmediately();
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                var cameraHost = new GameObject("Disposable Settlement Chest Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
                camera = cameraHost.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.orthographic = true;
                camera.orthographicSize = 5f; camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
                camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = width / (float)height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
            }
            public Frame Capture(string file, string state, float elapsed, bool verify = true)
            {
                Sequence?.SetElapsed(elapsed);
                var handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 3; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                if (controller.Session.ProfileGold != goldBefore || controller.Session.Phase != phaseBefore || JsonUtility.ToJson(result) != rewardsBefore)
                    throw new InvalidOperationException("Presentation mutated gameplay data.");
                var frame = new Frame { state = state, file = Path.GetFileName(file), width = width, height = height,
                    elapsed = Sequence == null ? 0f : Sequence.Elapsed, duration = Sequence == null ? 0f : Sequence.Duration,
                    phase = Sequence == null ? "Overview" : Sequence.Phase.ToString(), complete = Sequence == null || Sequence.IsComplete,
                    overviewInteractive = Sequence == null || Sequence.GetComponent<CanvasGroup>().interactable,
                    fireworks = host.GetComponentsInChildren<SettlementFireworksGraphic>(true).Any(effect => effect.IsEmitting) };
                if (verify)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(false).Where(text => text.enabled && !string.IsNullOrEmpty(text.text)))
                    {
                        float alpha = 1f;
                        foreach (CanvasGroup group in text.GetComponentsInParent<CanvasGroup>()) alpha *= group.alpha;
                        if (alpha < .02f) continue;
                        Mesh mesh = text.canvasRenderer.GetMesh(); if (mesh == null || mesh.vertexCount == 0) continue;
                        frame.visibleTexts++;
                        foreach (Vector3 vertex in mesh.vertices)
                        {
                            Vector3 screen = camera.WorldToScreenPoint(text.transform.TransformPoint(vertex));
                            if (screen.x < -1f || screen.x > width + 1f || screen.y < -1f || screen.y > height + 1f)
                                throw new InvalidOperationException("Visible text outside capture: " + text.name);
                        }
                    }
                    if (frame.visibleTexts == 0) throw new InvalidOperationException("Capture has no visible text.");
                    if (frame.complete && !frame.overviewInteractive) throw new InvalidOperationException("Completed sequence did not expose report input.");
                    if (!frame.complete && frame.overviewInteractive) throw new InvalidOperationException("Report input enabled during chest sequence.");
                }
                RenderTexture previous = RenderTexture.active;
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(file, pixels.EncodeToPNG()); }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
                return frame;
            }
            public void Dispose()
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
