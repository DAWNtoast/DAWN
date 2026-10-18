using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        /// <summary>Opt-in real UI frames. Run alone in an isolated project with a graphics device.</summary>
        [UnityTest, Timeout(360000)]
        public IEnumerator BattleHandFlight_CaptureOpeningDiscardAndNextDraw()
        {
            string destination = Environment.GetEnvironmentVariable("SEVENTH_PORT_CARD_FLIGHT_CAPTURE");
            if (string.IsNullOrWhiteSpace(destination))
                Assert.Ignore("Set SEVENTH_PORT_CARD_FLIGHT_CAPTURE to a new output directory to capture the live battle UI.");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Capture needs a graphics device. Do not pass -nographics.");
            string directory = Path.GetFullPath(destination);
            Assert.That(!Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory).Any(),
                Is.True, "Capture output must be a new or empty directory.");
            Directory.CreateDirectory(directory);

            float oldCaptureDelta = Time.captureDeltaTime;
            float oldTimeScale = Time.timeScale;
            int oldTargetRate = Application.targetFrameRate;
            int oldVSync = QualitySettings.vSyncCount;
            try
            {
                Time.timeScale = 1f;
                Time.captureDeltaTime = 1f / 30f;
                Application.targetFrameRate = 30;
                QualitySettings.vSyncCount = 0;
                using (var storage = new VoyageCargoStorageScope())
                {
                    yield return LoadResponsiveBattleScene();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
                    SetResponsiveHand(session, "card_brace", "card_chain_shot", "card_emergency_repair",
                        "card_brace", "card_full_sail_turn");
                    SetResponsiveDrawPile(session, "card_full_sail_turn", "card_brace", "card_chain_shot",
                        "card_emergency_repair", "card_brace");
                    session.Cargo.Powder = session.Cargo.Cannonballs = 20;
                    RenderResponsiveBattle(controller);
                    using (var recorder = new CardFlightFrameRecorder(directory))
                    {
                        // Warm the real materials before replaying the presentation-only opening.
                        // Shader/upload stalls must not consume the 400 ms animation being reviewed.
                        BattleHandFlightMotion opening = Object.FindFirstObjectByType<BattleHandFlightMotion>();
                        opening.Complete();
                        yield return null;
                        recorder.Capture(controller, "warmup", false);
                        yield return null;
                        RectTransform[] openingCards = Enumerable.Range(0, session.Battle.Hand.Count)
                            .Select(index => RequiredResponsive<RectTransform>("Battle Card " + index)).ToArray();
                        opening.Deal(openingCards, RequiredResponsive<RectTransform>("Battle Draw Pile"));
                        recorder.Capture(controller, "opening");
                        int openingRestingFrames = 0;
                        for (int frame = 0; frame < 180; frame++)
                        {
                            yield return null;
                            recorder.Capture(controller, "opening");
                            openingRestingFrames = controller.IsBattleAnimationPlaying ? 0 : openingRestingFrames + 1;
                            if (openingRestingFrames >= 15) break;
                        }
                        Assert.That(controller.IsBattleAnimationPlaying, Is.False,
                            "Opening animation must finish before the real End Turn callback.");
                        AssertResponsiveHandMatchesCore(session);
                        int turn = session.Battle.Turn;
                        RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
                        int restingFrames = 0;
                        bool sawNextDeal = false;
                        for (int frame = 0; frame < 360; frame++)
                        {
                            yield return null;
                            BattleHandFlightMotion motion = Object.FindFirstObjectByType<BattleHandFlightMotion>();
                            if (session.Battle.Turn > turn && motion != null && motion.IsPlaying) sawNextDeal = true;
                            string phase = session.Battle.Turn == turn ? "discard" :
                                motion != null && motion.IsPlaying ? "next-draw" : "enemy-turn-or-settled";
                            recorder.Capture(controller, phase);
                            restingFrames = controller.IsBattleAnimationPlaying ? 0 : restingFrames + 1;
                            if (restingFrames >= 15) break;
                        }
                        Assert.That(sawNextDeal, Is.True, "Capture must include the next turn's incoming cards.");
                        Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                        Assert.That(session.Battle.Turn, Is.EqualTo(turn + 1));
                        AssertResponsiveHandMatchesCore(session);
                    }
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.captureDeltaTime = oldCaptureDelta;
                Time.timeScale = oldTimeScale;
                Application.targetFrameRate = oldTargetRate;
                QualitySettings.vSyncCount = oldVSync;
            }
        }

        /// <summary>Explicit camera rendering also works in batch mode, where EndOfFrame may not resume.</summary>
        private sealed class CardFlightFrameRecorder : IDisposable
        {
            private const int Width = 1920, Height = 1080, Layer = 31;
            private readonly string directory;
            private readonly GameObject cameraHost;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly Texture2D pixels;
            private readonly CardFlightCaptureManifest manifest = new CardFlightCaptureManifest();
            private readonly List<byte[]> rawFrames = new List<byte[]>();
            private float timeline;

            public CardFlightFrameRecorder(string destination)
            {
                directory = destination;
                cameraHost = new GameObject("Card Flight Capture Camera", typeof(Camera));
                camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height * .5f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            }

            public void Capture(CoreGameplayDemoController controller, string phase, bool keep = true)
            {
                Canvas canvas = RequiredResponsive<RectTransform>("Battle Card").GetComponentInParent<Canvas>().rootCanvas;
                RectTransform rect = canvas.GetComponent<RectTransform>();
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                bool scalerEnabled = scaler != null && scaler.enabled;
                RenderMode originalMode = canvas.renderMode;
                Camera originalCamera = canvas.worldCamera;
                Vector2 originalSize = rect.sizeDelta;
                Vector3 originalPosition = rect.localPosition, originalScale = rect.localScale;
                Quaternion originalRotation = rect.localRotation;
                float originalPlane = canvas.planeDistance;
                RenderTexture originalActive = RenderTexture.active;
                Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
                int[] layers = descendants.Select(child => child.gameObject.layer).ToArray();
                try
                {
                    if (scaler != null) scaler.enabled = false;
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                    rect.sizeDelta = new Vector2(Width, Height);
                    rect.position = Vector3.zero;
                    rect.rotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                    foreach (Transform child in descendants) child.gameObject.layer = Layer;
                    if (!keep)
                        foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    if (!keep) return;
                    string file = manifest.frames.Count.ToString("0000") + ".png";
                    // Encoding PNGs in the animation loop would advance unscaled time by
                    // its CPU cost. Copy raw pixels now and encode once the live turn ends.
                    rawFrames.Add(pixels.GetRawTextureData());
                    if (manifest.frames.Count > 0) timeline += Time.unscaledDeltaTime;
                    manifest.frames.Add(new CardFlightCaptureFrame
                    {
                        file = file, phase = phase, seconds = timeline,
                        unscaledDeltaTime = Time.unscaledDeltaTime,
                        animationPlaying = controller.IsBattleAnimationPlaying,
                        turn = controller.Session.Battle.Turn,
                        handCount = controller.Session.Battle.Hand.Count,
                    });
                }
                finally
                {
                    RenderTexture.active = originalActive;
                    for (int index = 0; index < descendants.Length; index++)
                        if (descendants[index] != null) descendants[index].gameObject.layer = layers[index];
                    canvas.renderMode = originalMode;
                    canvas.worldCamera = originalCamera;
                    canvas.planeDistance = originalPlane;
                    rect.sizeDelta = originalSize;
                    rect.localPosition = originalPosition;
                    rect.localRotation = originalRotation;
                    rect.localScale = originalScale;
                    if (scaler != null) scaler.enabled = scalerEnabled;
                }
            }

            public void Dispose()
            {
                File.WriteAllText(Path.Combine(directory, "capture.json"), JsonUtility.ToJson(manifest, true));
                for (int index = 0; index < rawFrames.Count; index++)
                {
                    pixels.LoadRawTextureData(rawFrames[index]);
                    File.WriteAllBytes(Path.Combine(directory, manifest.frames[index].file), pixels.EncodeToPNG());
                    rawFrames[index] = null;
                }
                rawFrames.Clear();
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cameraHost);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [Serializable]
        private sealed class CardFlightCaptureManifest
        {
            public int width = 1920, height = 1080, requestedFramesPerSecond = 30;
            public string source = "Actual Unity battle canvas; isolated test saves; opening deal, real End Turn, next draw.";
            public List<CardFlightCaptureFrame> frames = new List<CardFlightCaptureFrame>();
        }

        [Serializable]
        private sealed class CardFlightCaptureFrame
        {
            public string file, phase;
            public float seconds, unscaledDeltaTime;
            public bool animationPlaying;
            public int turn, handCount;
        }
    }
}
