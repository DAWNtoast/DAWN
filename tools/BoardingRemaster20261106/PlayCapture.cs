// This fixture is installed only in the leased project's PlayMode test assembly.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
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
        [Serializable] private sealed class LiveClip
        {
            public string id, title, sourceCard, input;
            public List<string> frames = new List<string>();
            public List<float> seconds = new List<float>();
            public List<string> frameActions = new List<string>();
            public List<string> observedActions = new List<string>();
        }
        [Serializable] private sealed class LiveReport
        {
            public int width = 960, height = 540;
            public string scope = "Unity PlayMode captures of the production boarding UI following real card or end-turn button input. The test arranges crew, hand, resources and health before each action.";
            public List<LiveClip> clips = new List<LiveClip>();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator BoardingRemaster_RealInputClips()
        {
            string[] args = Environment.GetCommandLineArgs();
            int next = Array.IndexOf(args, "-boardingLiveOutput") + 1;
            if (next == 0) Assert.Ignore("Capture requested only by the boarding verification tool.");
            string output = Path.GetFullPath(args[next]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/outputs/boarding-remaster-20261106/validation/", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Use the task validation directory.");
            if (Directory.Exists(output)) throw new IOException("Use a fresh capture run.");
            Directory.CreateDirectory(output);
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var report = new LiveReport();
                string[][] cases = {
                    new[] { "captain_melee", "船长出刀", "card_crew_thrust" },
                    new[] { "captain_guard", "船长格挡", "card_crew_guard" },
                    new[] { "isha", "伊沙 · 专属卡", "card_special_crew_salt_crown_diver_isha" },
                    new[] { "mirei", "米蕾 · 专属卡", "card_special_crew_white_shell_doctor_mirei" },
                    new[] { "fire", "烈火", "card_boarding_fire_01" }, new[] { "arcane", "奥术", "card_boarding_arcane_01" },
                    new[] { "curse", "咒术", "card_boarding_curse_01" }, new[] { "tide", "潮汐", "card_boarding_tide_04" },
                    new[] { "thunder", "雷霆", "card_boarding_thunder_03" }, new[] { "blood", "血术", "card_boarding_blood_01" },
                    new[] { "enemy", "敌方出手", "" }, new[] { "blocked", "敌方格挡来击", "card_crew_thrust" },
                    new[] { "cast", "船长施法", "card_boarding_curse_02" },
                    new[] { "enemy_death", "敌方倒下", "card_crew_thrust" }, new[] { "player_death", "我方倒下", "" },
                };
                foreach (string[] item in cases)
                {
                    CoreVoyageSession session = PrepareActorCrewBattle(controller, "basic_01", out CrewVariantDefinition[] crew);
                    bool endTurn = item[2].Length == 0;
                    if (!endTurn) SetResponsiveHand(session, item[2], "card_crew_guard");
                    session.Battle.PlayerBoardingHealth = item[0] == "player_death" ? 1 : 150;
                    session.Battle.EnemyBoardingHealth = item[0] == "enemy_death" ? 1 : 200;
                    session.Battle.EnemyBlock = item[0] == "blocked" ? 100 : 0;
                    session.Cargo.Medicine = 60;
                    RenderResponsiveBattle(controller);
                    yield return WaitResponsiveBattle(controller);
                    AssertActorCrewIdentity(session, crew);
                    if (!endTurn) Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
                    var clip = new LiveClip { id = item[0], title = item[1], sourceCard = item[2],
                        input = endTurn ? "End Battle Turn" : "Battle Card 0" };
                    var actors = PlayerActors().Concat(EnemyActors()).ToArray();
                    Canvas canvas = RequiredResponsive<RectTransform>("Battle Card").GetComponentInParent<Canvas>().rootCanvas;
                    using (var recorder = new LiveSceneRecorder(canvas, output, clip))
                    {
                        recorder.Sample();
                        RequiredResponsive<Button>(clip.input).onClick.Invoke();
                        float deadline = Time.realtimeSinceStartup + 20f;
                        float last = Time.realtimeSinceStartup;
                        bool sawMotion = false;
                        do
                        {
                            foreach (var actor in actors.Where(a => a != null && a.Action != BoardingActorAction.Idle))
                            {
                                string state = actor.Identity + "/" + actor.Action;
                                if (!clip.observedActions.Contains(state)) clip.observedActions.Add(state);
                                sawMotion = true;
                            }
                            // Include the actual reveal, contact and recovery in their observed order.
                            if (canvas != null && Time.realtimeSinceStartup - last >= .04f && clip.frames.Count < 120)
                            {
                                recorder.Sample();
                                last = Time.realtimeSinceStartup;
                            }
                            if (!controller.IsBattleAnimationPlaying) break;
                            yield return null;
                        } while (Time.realtimeSinceStartup < deadline);
                        Assert.That(controller.IsBattleAnimationPlaying, Is.False, item[0]);
                        Assert.That(sawMotion, Is.True, item[0] + " did not animate a participant");
                        Assert.That(clip.frames.Count, Is.GreaterThan(3), item[0]);
                        if (canvas != null && GameObject.Find("Battle Card") != null) recorder.Sample();
                    }
                    report.clips.Add(clip);
                    File.WriteAllText(Path.Combine(output, "live-report.json"), JsonUtility.ToJson(report, true));
                    Debug.Log("BOARDING_LIVE_CLIP " + clip.id + " " + clip.frames.Count);
                }
            }
        }

        private sealed class LiveSceneRecorder : IDisposable
        {
            private readonly Canvas canvas;
            private readonly string output;
            private readonly LiveClip clip;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly Texture2D pixels;
            private readonly float started;
            public LiveSceneRecorder(Canvas canvas, string output, LiveClip clip)
            {
                this.canvas = canvas;
                this.output = output;
                this.clip = clip;
                camera = new GameObject("Verification recorder", typeof(Camera)).GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 540;
                camera.aspect = 16f / 9;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << 31;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.allowHDR = camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                started = Time.realtimeSinceStartup;
            }
            public void Sample()
            {
                RectTransform root = (RectTransform)canvas.transform;
                var scaler = canvas.GetComponent<CanvasScaler>();
                bool scalerEnabled = scaler != null && scaler.enabled;
                RenderMode mode = canvas.renderMode;
                Camera original = canvas.worldCamera;
                float plane = canvas.planeDistance;
                Vector2 size = root.sizeDelta;
                Vector3 position = root.localPosition, scale = root.localScale;
                Quaternion rotation = root.localRotation;
                RenderTexture previous = RenderTexture.active;
                Transform[] nodes = canvas.GetComponentsInChildren<Transform>(true);
                int[] layers = nodes.Select(node => node.gameObject.layer).ToArray();
                try
                {
                    if (scaler != null) scaler.enabled = false;
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                    root.sizeDelta = new Vector2(1920, 1080);
                    root.position = Vector3.zero;
                    root.rotation = Quaternion.identity;
                    root.localScale = Vector3.one;
                    foreach (Transform node in nodes) node.gameObject.layer = 31;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                    else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    pixels.Apply(false, false);
                    string file = clip.id + "_" + clip.frames.Count.ToString("D3") + ".jpg";
                    using (var stream = new FileStream(Path.Combine(output, file), FileMode.CreateNew))
                    {
                        byte[] bytes = pixels.EncodeToJPG(90);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                    clip.frames.Add(file);
                    clip.seconds.Add(Time.realtimeSinceStartup - started);
                    clip.frameActions.Add(string.Join(" ", canvas.GetComponentsInChildren<BoardingActorGraphic>()
                        .Where(actor => actor.Action != BoardingActorAction.Idle)
                        .Select(actor => actor.Identity + "/" + actor.Action + "/" + Mathf.RoundToInt(actor.ActionProgress * 100))));
                }
                finally
                {
                    RenderTexture.active = previous;
                    for (int i = 0; i < nodes.Length; i++) if (nodes[i] != null) nodes[i].gameObject.layer = layers[i];
                    canvas.renderMode = mode;
                    canvas.worldCamera = original;
                    canvas.planeDistance = plane;
                    root.sizeDelta = size;
                    root.localPosition = position;
                    root.localRotation = rotation;
                    root.localScale = scale;
                    if (scaler != null) scaler.enabled = scalerEnabled;
                    Canvas.ForceUpdateCanvases();
                }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
