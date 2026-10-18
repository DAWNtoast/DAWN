// Install only in the isolated validation project. No production scene is saved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public static class BattleHudCapture
    {
        private const int Layer = 31;
        [Serializable] private sealed class Report
        {
            public string scope = "Disposable production battle UI, with representative in-memory statuses. Original CanvasScaler and target geometry verified. No saves or scene regeneration.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string state, file, error;
            public int width, height, playerStatuses, enemyStatuses;
            public float scaleFactor, expectedScaleFactor;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect;
            public bool verifiedScaler;
            public int intentCount;
            public bool movementSelected;
            public string[] intents;
            public float playerStatusAlpha, enemyStatusAlpha, playerInheritedAlphaMax, enemyInheritedAlphaMax;
            public bool playerStatusRaycasts, enemyStatusRaycasts, verifiedStatusVisibility;
            public string cannonValue, cannonTooltip;
        }

        public static void CaptureFromCommandLine()
        {
            CaptureSet(false);
        }

        public static void CaptureQuickFromCommandLine()
        {
            CaptureSet(true);
        }

        public static void CaptureCompactFromCommandLine() => CaptureSet(true, true);
        public static void CaptureCompactFullFromCommandLine() => CaptureSet(false, true);

        private static void CaptureSet(bool quick, bool compact = false)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains("-quit"))
                throw new ArgumentException("Capture needs real Editor frames for CanvasGroup rendering; omit -quit. It exits when finished.");
            int index = Array.IndexOf(args, "-battleHudCapturePath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass an absolute -battleHudCapturePath.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part.Equals("Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Screenshots belong below Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new capture directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            string[] states = compact ? new[] { "Naval", "NavalCannon", "NavalMovement", "Boarding", "BoardingMultiIntent", "DiscardPile" } :
                quick ? new[] { "Naval", "Boarding" } : new[] { "Naval", "Boarding", "NavalTooltip", "BoardingTooltip" };
            foreach (string state in states)
                foreach (Vector2Int size in quick ? new[] { new Vector2Int(1920, 1080) } : new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) })
                {
                    var frame = new Frame { state = state, width = size.x, height = size.y, file = state + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                }
            // Advance real Editor/player frames between scene mutation and rendering,
            // including native CanvasGroup inheritance as well as managed UI rebuilds.
            int nextFrame = 0;
            Frame activeFrame = null;
            IEnumerator capture = null;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (capture == null)
                {
                    if (nextFrame >= report.frames.Count)
                    {
                        EditorApplication.update -= tick;
                        bool failed = report.frames.Any(frame => !frame.verifiedScaler || !frame.verifiedStatusVisibility || !string.IsNullOrEmpty(frame.error));
                        if (failed) Debug.LogError("Battle HUD capture failed; inspect " + Path.Combine(output, "report.json"));
                        else Debug.Log("BATTLE_HUD_CAPTURE_OK: " + report.frames.Count + " verified screenshots at " + output);
                        EditorApplication.Exit(failed ? 1 : 0);
                        return;
                    }
                    activeFrame = report.frames[nextFrame++];
                    capture = Capture(output, activeFrame);
                }
                bool complete = false;
                try { complete = !capture.MoveNext(); }
                catch (Exception exception)
                {
                    activeFrame.error = (exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception).ToString();
                    complete = true;
                }
                if (complete)
                {
                    (capture as IDisposable)?.Dispose();
                    capture = null;
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                }
                EditorApplication.QueuePlayerLoopUpdate();
            };
            EditorApplication.update += tick;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static IEnumerator Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable Battle HUD Audit");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                CoreBattleState battle = controller.Session.Battle;
                bool boarding = frame.state.StartsWith("Boarding", StringComparison.Ordinal);
                Set(battle, "Stage", boarding ? NavalBattleStage.Boarding : NavalBattleStage.Naval);
                if (frame.state == "NavalCannon")
                {
                    // The live distance setter moves the tactical poses to three cells.
                    // Turn one selects the authored cannon plan rather than the turn-two
                    // defensive plan; all damage still comes from the real battle rules.
                    battle.DistanceMeters = 300f;
                    battle.PhaseTurn = 1;
                }
                Set(battle, "Energy", 3);
                Set(battle, "NavalActionPoints", 3);
                Set(battle.Schools, "Mana", 2);
                Set(battle.Schools, "ThunderChain", 3);
                Set(battle.Schools, "TidePower", 4);
                Set(battle.Schools, "CurseMarks", 2);
                controller.Session.Cargo.Powder = 12;
                controller.Session.Cargo.Cannonballs = 18;
                controller.Session.Cargo.Wood = 8;
                controller.Session.Cargo.Medicine = 5;
                Set(battle, "PlayerBlock", 12);
                Set(battle, "EnemyBlock", 9);
                Set(battle, "PlayerFireRounds", 2);
                Set(battle, "PlayerLoadedShot", 3);
                Set(battle, "PlayerDeckBuff", 2);
                Set(battle, "EnemyLoadedShot", 8);
                Set(battle, "EnemyBurnDamage", 3);
                Set(battle, "EnemyBurnTurns", 2);
                if (boarding)
                {
                    if (frame.state == "BoardingMultiIntent")
                    {
                        Set(battle.BoardingEnemy, "Deck", CoreBoardingEnemyCatalog.ForSchool("thunder"));
                        Set(battle, "PhaseTurn", 1);
                    }
                    Set(battle.Boarding, "FirePactTurns", 2);
                    Set(battle.Boarding, "FireRetaliations", 2);
                    Set(battle.Boarding, "FreePowderUses", 1);
                    Set(battle.Boarding, "ThunderReady", 3);
                    Set(battle.Boarding, "CurseContractTurns", 2);
                    Set(battle.Boarding, "TideAmbushTurns", 2);
                    Set(battle.Boarding, "BloodDebtTurns", 2);
                    Set(battle.Boarding, "ThunderWeakness", 2);
                    Set(battle.Boarding, "ThunderWeaknessTurns", 2);
                    Set(battle.Boarding, "CurseArmorTurns", 2);
                    Set(battle.BoardingEnemy, "PlayerBurnDamage", 2);
                    Set(battle.BoardingEnemy, "PlayerBurnTurns", 2);
                }
                else
                {
                    Set(battle.Schools, "PrepareFireGuard", 4);
                    Set(battle.Schools, "ScorchCharges", 2);
                    Set(battle.Schools, "ScorchDamage", 3);
                    Set(battle.NavalFields.Player, "FireImmunityCharges", 1);
                    Set(battle.NavalFields.Player, "AnchorCharges", 1);
                    Set(battle.NavalFields.Enemy, "MovementSlow", 1);
                    Set(battle.NavalExpansion, "RuneClamp", true);
                    Set(battle.NavalExpansion, "OilPlayerEndsRemaining", 2);
                }
                if (frame.state == "DiscardPile")
                {
                    int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(item => CoreVoyageSession.CardLibrary[item].Id == "card_brace");
                    battle.DrawPile.Clear();
                    for (int item = 0; item < 11; item++) battle.DrawPile.Add(card);
                    battle.DiscardPile.Clear();
                    for (int item = 0; item < 3; item++) battle.DiscardPile.Add(card);
                }
                Invoke(controller, "Render");
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || !scaler.enabled) throw new InvalidOperationException("Production scaler must remain enabled.");
                GameObject cameraHost = new GameObject("Battle HUD Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (NavigationDashboardLayout layout in host.GetComponentsInChildren<NavigationDashboardLayout>(false))
                        Invoke(layout, "LateUpdate");
                    foreach (BattleStatusRow row in host.GetComponentsInChildren<BattleStatusRow>(false))
                        Invoke(row, "LateUpdate");
                    Transform intentRow = host.GetComponentsInChildren<Transform>(false).FirstOrDefault(item => item.name == "Enemy Intent Icons");
                    if (intentRow != null)
                        foreach (MonoBehaviour component in intentRow.GetComponents<MonoBehaviour>())
                            component.GetType().GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(component, null);
                    if (pass == 2 && frame.state == "NavalMovement")
                    {
                        GameObject eventHost = new GameObject("Battle HUD Movement Capture Events", typeof(EventSystem));
                        SceneManager.MoveGameObjectToScene(eventHost, scene);
                        Transform ship = host.GetComponentsInChildren<Transform>(false).Single(item => item.name == "Player Ship Art");
                        var pointer = new PointerEventData(eventHost.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left,
                            position = RectTransformUtility.WorldToScreenPoint(camera, ship.position) };
                        if (!ExecuteEvents.Execute(ship.gameObject, pointer, ExecuteEvents.pointerClickHandler))
                            throw new InvalidOperationException("Player ship does not accept the real movement selection click.");
                        NavalTacticalMovementInput input = host.GetComponentInChildren<NavalTacticalMovementInput>();
                        input.RefreshNow();
                        frame.movementSelected = input.IsShipSelected;
                        if (!frame.movementSelected) throw new InvalidOperationException("Movement capture failed to select the player ship.");
                        foreach (BattleStatusRow row in host.GetComponentsInChildren<BattleStatusRow>(false)) row.RefreshVisibility();
                    }
                    if (pass == 2 && frame.state == "DiscardPile")
                    {
                        Button pile = host.GetComponentsInChildren<Button>(false).Single(item => item.name == "Battle Discard Pile");
                        pile.onClick.Invoke();
                    }
                    if (pass == 2 && frame.state.EndsWith("Tooltip", StringComparison.Ordinal))
                    {
                        GameObject eventHost = new GameObject("Battle HUD Capture Events", typeof(EventSystem));
                        SceneManager.MoveGameObjectToScene(eventHost, scene);
                        BattleStatusRow row = host.GetComponentsInChildren<BattleStatusRow>(true).First(item => item.name.StartsWith(boarding ? "Enemy" : "Player", StringComparison.Ordinal));
                        Transform badge = row.GetComponentsInChildren<Transform>(false).First(item => item.name.StartsWith("Status ", StringComparison.Ordinal));
                        var pointer = new PointerEventData(eventHost.GetComponent<EventSystem>()) { position = RectTransformUtility.WorldToScreenPoint(camera, badge.position) };
                        if (!ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerEnterHandler))
                            throw new InvalidOperationException("Status badge does not accept hover.");
                    }
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    // Native CanvasRenderer inherited alpha is updated by the player loop,
                    // independently of the managed layout and mesh rebuild callbacks.
                    EditorApplication.QueuePlayerLoopUpdate();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                    yield return null;
                }
                frame.scaleFactor = canvas.scaleFactor;
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                float widthRatio = frame.width / scaler.referenceResolution.x, heightRatio = frame.height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(widthRatio, heightRatio) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(widthRatio, heightRatio) :
                    Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(widthRatio, 2f), Mathf.Log(heightRatio, 2f), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled && Mathf.Abs(frame.scaleFactor - frame.expectedScaleFactor) < .001f &&
                    Mathf.Abs(frame.renderingDisplaySize.x - frame.width) < .5f && Mathf.Abs(frame.renderingDisplaySize.y - frame.height) < .5f &&
                    Mathf.Abs(frame.canvasPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.canvasPixelRect.height - frame.height) < .5f &&
                    Mathf.Abs(frame.logicalCanvasSize.x - frame.width / frame.scaleFactor) < 1f && Mathf.Abs(frame.logicalCanvasSize.y - frame.height / frame.scaleFactor) < 1f;
                if (!frame.verifiedScaler) throw new InvalidOperationException("Target does not drive the actual CanvasScaler geometry.");
                BattleStatusRow[] rows = host.GetComponentsInChildren<BattleStatusRow>(true);
                BattleStatusRow playerRow = rows.Single(row => row.name == "Player Battle Status Row");
                BattleStatusRow enemyRow = rows.Single(row => row.name == "Enemy Battle Status Row");
                frame.playerStatuses = playerRow.VisibleStatusCount;
                frame.enemyStatuses = enemyRow.VisibleStatusCount;
                frame.playerStatusAlpha = playerRow.GetComponent<CanvasGroup>().alpha;
                frame.enemyStatusAlpha = enemyRow.GetComponent<CanvasGroup>().alpha;
                frame.playerStatusRaycasts = playerRow.GetComponent<CanvasGroup>().blocksRaycasts;
                frame.enemyStatusRaycasts = enemyRow.GetComponent<CanvasGroup>().blocksRaycasts;
                frame.playerInheritedAlphaMax = playerRow.GetComponentsInChildren<CanvasRenderer>(false).Max(renderer => renderer.GetInheritedAlpha());
                frame.enemyInheritedAlphaMax = enemyRow.GetComponentsInChildren<CanvasRenderer>(false).Max(renderer => renderer.GetInheritedAlpha());
                if (frame.state == "NavalMovement")
                {
                    frame.verifiedStatusVisibility = frame.playerStatuses == 0 && frame.enemyStatuses == 0 &&
                        frame.playerStatusAlpha < .001f && frame.enemyStatusAlpha < .001f &&
                        frame.playerInheritedAlphaMax < .001f && frame.enemyInheritedAlphaMax < .001f &&
                        !frame.playerStatusRaycasts && !frame.enemyStatusRaycasts;
                }
                else frame.verifiedStatusVisibility = frame.playerStatuses > 0 && frame.enemyStatuses > 0 &&
                    frame.playerStatusAlpha > .99f && frame.enemyStatusAlpha > .99f &&
                    frame.playerInheritedAlphaMax > .99f && frame.enemyInheritedAlphaMax > .99f;
                if (!frame.verifiedStatusVisibility)
                    throw new InvalidOperationException("Status CanvasGroup and rendered inherited alpha must agree with the capture state.");
                var liveIntents = CoreBattleIntentPresentation.Build(controller.Session);
                frame.intentCount = liveIntents.Count;
                frame.intents = liveIntents.Select(intent => intent.Id + "=" + intent.Value).ToArray();
                if (frame.state == "NavalCannon")
                {
                    CoreBattleIntent cannon = liveIntents.SingleOrDefault(intent => intent.Kind == CoreBattleIntentKind.Cannon);
                    if (cannon == null || !cannon.Amount.HasValue || cannon.Amount.Value <= 0)
                        throw new InvalidOperationException("The real three-cell, turn-one enemy plan must yield positive cannon damage.");
                    Transform badge = host.GetComponentsInChildren<Transform>(false).Single(item => item.name == "Intent " + cannon.Id);
                    Text amount = badge.Find("Amount").GetComponent<Text>();
                    BattleCommandIconGraphic icon = badge.Find("Icon").GetComponent<BattleCommandIconGraphic>();
                    if (amount.text != cannon.Value || icon.Kind != BattleCommandIconKind.Cannonball)
                        throw new InvalidOperationException("The cannonball intent and its lower-right value must match live cannon damage.");
                    frame.cannonValue = cannon.Value;
                    frame.cannonTooltip = cannon.Tooltip;
                }
                if (frame.state == "BoardingMultiIntent" && liveIntents.Count < 2) throw new InvalidOperationException("The authored thunder enemy turn must show multiple intents.");
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0f, 0f, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < samples.Length; i += 997) { int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3; low = Math.Min(low, brightness); high = Math.Max(high, brightness); }
                if (high - low < 20) throw new InvalidOperationException("Nearly blank frame.");
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

        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static void SetOptional(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = target.GetType().GetField(name, flags);
            if (field != null) { field.SetValue(target, value); return; }
            PropertyInfo property = target.GetType().GetProperty(name, flags);
            if (property?.GetSetMethod(true) != null) property.GetSetMethod(true).Invoke(target, new[] { value });
        }
        private static void Set(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (target.GetType().GetField(name, flags) == null && target.GetType().GetProperty(name, flags)?.GetSetMethod(true) == null)
                throw new MissingMemberException(target.GetType().Name, name);
            SetOptional(target, name, value);
        }
    }
}
