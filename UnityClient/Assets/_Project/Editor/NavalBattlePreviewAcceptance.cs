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
    /// <summary>
    /// Actual battle UI rendered from disposable edit-mode sessions. Fixture-only
    /// reflection stays here: no saved scenes, player preferences or runtime rules change.
    /// </summary>
    public static class NavalBattlePreviewAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int CaptureLayer = 31;

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string scope = "Four actual Unity UI renders: preparation, distant naval combat, close parallel ships, and boarding. Temporary editor preview sessions with arranged hands and health, using the existing Yilan and Noya crew identities and their authored special cards. No Play Mode, button callbacks, PlayerPrefs writes, or scene/asset saves.";
            public int expectedCaptureCount = 4;
            public int successfulCaptureCount;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public string label;
            public string file;
            public NavalBattleStage stage;
            public string stageName;
            public float distanceMeters;
            public string alignment;
            public int playerLife;
            public int playerMaxLife;
            public int enemyLife;
            public int enemyMaxLife;
            public float playerHealthBarFill;
            public float enemyHealthBarFill;
            public Vector2 playerShipPosition;
            public Vector2 enemyShipPosition;
            public Vector2 playerShipSize;
            public Vector2 enemyShipSize;
            public float enemyShipYaw;
            public Rect backdropUv;
            public string boardingApproach;
            public int uiObjectCount;
            public bool canRam;
            public bool canBoard;
            public string ramReason;
            public string boardingReason;
            public List<string> hand = new List<string>();
            public List<string> unavailableCards = new List<string>();
            public List<string> unityErrors = new List<string>();
            public bool captured;
            public string error;
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.NavalBattlePreviewAcceptance.CaptureFromCommandLine
        /// -navalBattleCapturePath D:/Game/UnityClient/Artifacts/NavalBattle/review-01
        /// The output directory must be new or empty, underneath an Artifacts directory.
        /// Requires a graphics device; do not pass -nographics.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("NAVAL_BATTLE_PREVIEW_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Naval battle acceptance requires stopped edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Naval battle captures require graphics. Remove -nographics.");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("A designated Artifacts output directory is required.", nameof(outputDirectory));
            string directory = Path.GetFullPath(outputDirectory);
            bool inArtifacts = directory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase));
            if (!inArtifacts)
                throw new ArgumentException("Choose a capture directory underneath Artifacts: " + directory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty. Choose a fresh -navalBattleCapturePath: " + directory);
            Directory.CreateDirectory(directory);

            Review report = new Review { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            report.captures.AddRange(new[]
            {
                new Record { label = "备战策略 · 600 米", file = "01_Preparation_600m.png", stage = NavalBattleStage.Preparation, distanceMeters = 600f },
                new Record { label = "炮击机动 · 远距 450 米", file = "02_Naval_Far_450m.png", stage = NavalBattleStage.Naval, distanceMeters = 450f },
                new Record { label = "炮击机动 · 平行接近 50 米", file = "03_Naval_Close_50m.png", stage = NavalBattleStage.Naval, distanceMeters = 50f },
                new Record { label = "接舷近战 · 船员生命", file = "04_Boarding.png", stage = NavalBattleStage.Boarding, distanceMeters = 0f },
            });
            try
            {
                foreach (Record record in report.captures)
                {
                    Application.LogCallback collectErrors = (condition, stackTrace, type) =>
                    {
                        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                            record.unityErrors.Add(condition + (string.IsNullOrEmpty(stackTrace) ? string.Empty : "\n" + stackTrace));
                    };
                    Application.logMessageReceived += collectErrors;
                    try
                    {
                        CaptureOne(directory, record);
                        if (record.unityErrors.Count > 0)
                            throw new InvalidOperationException("Unity reported " + record.unityErrors.Count + " error(s) during this capture.");
                        record.captured = true;
                        report.successfulCaptureCount += 1;
                    }
                    catch (Exception exception)
                    {
                        Exception cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException : exception;
                        record.error = cause.Message;
                        report.errors.Add(record.label + ": " + cause.Message);
                    }
                    finally { Application.logMessageReceived -= collectErrors; }
                }
            }
            finally
            {
                WriteNewFile(Path.Combine(directory, "report.json"), new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(report, true)));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulCaptureCount != report.expectedCaptureCount)
                throw new InvalidOperationException("Naval battle acceptance is incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static void CaptureOne(string directory, Record record)
        {
            Scene previewScene = default;
            RenderTexture previousActive = RenderTexture.active;
            EventSystem previousEvents = EventSystem.current;
            GameObject previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            GameObject host = null;
            GameObject cameraHost = null;
            RenderTexture target = null;
            try
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                host = new GameObject("Naval Battle Acceptance Host") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, previewScene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                if (controller.Session == null || controller.Session.Phase != CoreDemoPhase.Port)
                    throw new InvalidOperationException("The disposable port preview did not initialize a session.");
                controller.Session.ConfigureSpecialCardSources(new[]
                {
                    "crew_twilight_tide_gunner_yilan", "crew_gray_gull_diver_noya"
                }, Array.Empty<string>());
                Invoke(controller, "PreparePreviewEncounter", true);
                if (controller.Session == null || controller.Session.Battle == null)
                    throw new InvalidOperationException("The existing Battle preview did not initialize a battle.");

                ArrangeFixture(controller.Session, record);
                Invoke(controller, "Render");
                Transform root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                if (root == null) throw new InvalidOperationException("The preview Canvas was not created.");
                Canvas canvas = root.GetComponent<Canvas>();
                if (canvas == null) throw new InvalidOperationException("The preview root has no Canvas.");
                Find<RectTransform>(root, "Battle Card");
                RectTransform player = Find<RectTransform>(root, "Player Ship Art");
                RectTransform enemy = Find<RectTransform>(root, "Enemy Ship Art");
                Image playerBar = Find<Image>(root, "Player Battle Hull Bar");
                Image enemyBar = Find<Image>(root, "Enemy Battle Hull Bar");
                RecordBattleUi(controller.Session, record, player, enemy, playerBar, enemyBar);
                record.backdropUv = Find<RawImage>(root, "Battle Scene Art").uvRect;

                cameraHost = new GameObject("Naval Battle Acceptance Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << CaptureLayer;
                camera.scene = previewScene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Naval Battle Acceptance Target" };
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(Width, Height);
                canvasRect.position = Vector3.zero;
                canvasRect.rotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one;
                Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children) child.gameObject.layer = CaptureLayer;
                record.uiObjectCount = children.Length;
                for (int pass = 0; pass < 3; pass += 1)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                SaveFrame(target, Path.Combine(directory, record.file));
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (host != null) Object.DestroyImmediate(host);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
                if (previousEvents != null)
                    previousEvents.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            }
        }

        private static void ArrangeFixture(CoreVoyageSession session, Record record)
        {
            // Existing authored crew were configured before departure; only battle
            // phase and presentation values are arranged for this visual fixture.
            Invoke(session, "EnterBattleStage", record.stage);
            CoreBattleState battle = session.Battle;
            battle.DistanceMeters = record.distanceMeters;
            battle.Alignment = record.distanceMeters <= 60f ? NavalBattleAlignment.Parallel : NavalBattleAlignment.HeadOn;
            battle.PlayerMovingTowardEnemy = false;
            battle.EnemyMovingTowardPlayer = record.stage != NavalBattleStage.Boarding;
            battle.PlayerBlock = 6;
            battle.EnemyBlock = 4;
            battle.Energy = battle.MaxEnergy = 3;
            battle.PlayerLoadedShot = record.stage == NavalBattleStage.Naval ? 4 : 0;
            battle.PlayerFireRounds = record.stage == NavalBattleStage.Naval ? 2 : 0;
            battle.PlayerDeckBuff = record.stage == NavalBattleStage.Preparation ? 0 : 2;
            battle.EnemyHull = record.stage == NavalBattleStage.Preparation ? battle.EnemyMaxHull : Mathf.Max(1, Mathf.RoundToInt(battle.EnemyMaxHull * 0.72f));
            battle.PlayerBoardingHealth = Mathf.Max(1, battle.PlayerMaxBoardingHealth - 13);
            battle.EnemyBoardingHealth = Mathf.Max(1, battle.EnemyMaxBoardingHealth - 17);
            battle.LastAction = "验收预览：临时配置阶段、距离、手牌与生命，用于检查实际战斗界面。";
            string fireCard = CoreSpecialCardCatalog.ForCrew("crew_twilight_tide_gunner_yilan").Card.Id;
            string boardingCard = CoreSpecialCardCatalog.ForCrew("crew_gray_gull_diver_noya").Card.Id;
            string[] hand = record.stage == NavalBattleStage.Preparation
                ? new[] { "card_deck_polish", "card_load_shot", "card_approach", "card_parallel_course", fireCard }
                : record.stage == NavalBattleStage.Naval
                    ? new[] { "card_chain_shot", "card_cannon_barrage", "card_approach", "card_parallel_course", fireCard }
                    : new[] { "card_cutlass_raid", "card_crew_guard", "card_crew_bandage", "card_cutlass_raid", boardingCard };
            battle.Hand.Clear();
            battle.DrawPile.Clear();
            battle.DiscardPile.Clear();
            battle.DrawPile.AddRange(session.GetBattleDeck(record.stage));
            battle.DrawPile.AddRange(session.GetCrewBattleCards(record.stage));
            foreach (string id in hand)
            {
                int cardIndex = -1;
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index += 1)
                    if (CoreVoyageSession.CardLibrary[index].Id == id) { cardIndex = index; break; }
                if (cardIndex < 0 || !CoreVoyageSession.CardLibrary[cardIndex].IsAllowedInStage(record.stage))
                    throw new InvalidOperationException("Missing or wrong-stage fixture card: " + id);
                if (!battle.DrawPile.Remove(cardIndex))
                    throw new InvalidOperationException("Fixture card is not in the effective stage deck: " + id);
                battle.Hand.Add(cardIndex);
            }
            if (record.stage == NavalBattleStage.Preparation && battle.Hand.Any(index => CoreVoyageSession.CardLibrary[index].Action == NavalBattleCardAction.Cannon))
                throw new InvalidOperationException("The preparation fixture must not contain cannon cards.");
        }

        private static void RecordBattleUi(CoreVoyageSession session, Record record, RectTransform player, RectTransform enemy, Image playerBar, Image enemyBar)
        {
            CoreBattleState battle = session.Battle;
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            record.stageName = battle.Stage.ToString();
            record.alignment = battle.Alignment.ToString();
            record.playerLife = boarding ? battle.PlayerBoardingHealth : session.Hull;
            record.playerMaxLife = boarding ? battle.PlayerMaxBoardingHealth : session.MaxHull;
            record.enemyLife = boarding ? battle.EnemyBoardingHealth : battle.EnemyHull;
            record.enemyMaxLife = boarding ? battle.EnemyMaxBoardingHealth : battle.EnemyMaxHull;
            record.playerHealthBarFill = playerBar.fillAmount;
            record.enemyHealthBarFill = enemyBar.fillAmount;
            record.playerShipPosition = player.anchoredPosition;
            record.enemyShipPosition = enemy.anchoredPosition;
            // Ship roots retain a fixed design rectangle during a continuous zoom;
            // actual visual size includes their current presentation scale.
            record.playerShipSize = Vector2.Scale(player.rect.size, new Vector2(Mathf.Abs(player.localScale.x), Mathf.Abs(player.localScale.y)));
            record.enemyShipSize = Vector2.Scale(enemy.rect.size, new Vector2(Mathf.Abs(enemy.localScale.x), Mathf.Abs(enemy.localScale.y)));
            record.enemyShipYaw = enemy.localEulerAngles.y;
            record.boardingApproach = battle.BoardingApproach.ToString();
            record.canRam = session.CanRam(out record.ramReason);
            record.canBoard = session.CanBoard(out record.boardingReason);
            for (int slot = 0; slot < battle.Hand.Count; slot += 1)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[battle.Hand[slot]];
                record.hand.Add(card.Id);
                if (!session.CanPlayCard(slot)) record.unavailableCards.Add(card.Id + ": " + session.CardUnavailableReason(slot));
            }
            float playerExpected = record.playerLife / (float)Mathf.Max(1, record.playerMaxLife);
            float enemyExpected = record.enemyLife / (float)Mathf.Max(1, record.enemyMaxLife);
            if (Mathf.Abs(playerBar.fillAmount - playerExpected) > 0.001f || Mathf.Abs(enemyBar.fillAmount - enemyExpected) > 0.001f)
                throw new InvalidOperationException("Health bars do not match the current stage's " + (boarding ? "crew health." : "hull health."));
        }

        private static void SaveFrame(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                Color32[] pixels = image.GetPixels32();
                int dark = 255, bright = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int level = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    dark = Math.Min(dark, level);
                    bright = Math.Max(bright, level);
                }
                if (bright - dark < 20) throw new InvalidOperationException("The capture is nearly uniform; battle UI may not have rendered.");
                WriteNewFile(path, image.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>三阶段海战预览</title><style>body{margin:28px;background:#15232b;color:#f3e4c5;font:16px system-ui}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,560px),1fr));gap:22px}figure{margin:0;padding:12px;background:#263941;border:1px solid #705c3e}img{width:100%;display:block}figcaption,p{line-height:1.7}small{display:block;color:#c9c0ad}a{color:#f1c471}.error{color:#ffb2a2}</style><h1>三阶段海战预览</h1><p>");
            html.Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append(" 张 · 1920 × 1080 · 实际 Unity 战斗界面<br>测试状态仅存在于隔离预览中，未写入玩家存档或场景。请检查远近舰位、阶段手牌、生命条、文字与按钮遮挡。<br><a href=\"report.json\">结构化报告</a> · UTC ")
                .Append(Escape(report.capturedAtUtc)).Append("</p><main>");
            foreach (Record record in report.captures)
            {
                html.Append("<figure>");
                if (record.captured) html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><img loading=\"lazy\" width=\"1920\" height=\"1080\" src=\"")
                    .Append(Escape(record.file)).Append("\" alt=\"").Append(Escape(record.label)).Append("\"></a>");
                html.Append("<figcaption>").Append(Escape(record.label)).Append("<small>")
                    .Append(Escape(record.stageName)).Append(" · ").Append(Escape(record.alignment)).Append(" · ")
                    .Append(record.uiObjectCount).Append(" 个 UI 对象</small><small>我方生命 ").Append(record.playerLife).Append("/").Append(record.playerMaxLife)
                    .Append(" · 敌方生命 ").Append(record.enemyLife).Append("/").Append(record.enemyMaxLife).Append("</small>");
                if (!record.captured) html.Append("<small class=\"error\">").Append(Escape(record.error)).Append("</small>");
                html.Append("</figcaption></figure>");
            }
            html.Append("</main></html>");
            WriteNewFile(Path.Combine(directory, "index.html"), new UTF8Encoding(false).GetBytes(html.ToString()));
        }

        private static void WriteNewFile(string path, byte[] bytes)
        {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T component = root.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            if (component == null) throw new InvalidOperationException("Missing battle preview object: " + name);
            return component;
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
            method.Invoke(target, arguments);
        }

        private static string Escape(string value) =>
            (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index += 1)
            {
                if (arguments[index] != "-navalBattleCapturePath") continue;
                if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("-navalBattleCapturePath requires a fresh Artifacts output directory.");
                return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            }
            throw new ArgumentException("Pass -navalBattleCapturePath followed by a new or empty Artifacts directory.");
        }
    }
}
