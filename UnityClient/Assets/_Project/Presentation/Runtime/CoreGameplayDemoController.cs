using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Runtime-built UI for the playable core loop. The scene deliberately owns
    /// only this component so the prototype remains easy to refresh and test.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed partial class CoreGameplayDemoController : MonoBehaviour
    {
        public const string TutorialCompletedKey = "ShatteredSail.TutorialCompleted.v1";
        public const string TutorialRewardGrantedKey = "ShatteredSail.TutorialRewardGranted.v1";
        public const string ProfileGoldKey = "ShatteredSail.ProfileGold.v1";
        public const string CurrentPortKey = "ShatteredSail.CurrentPort.v1";
        public const string SelectedShipKey = "ShatteredSail.SelectedShip.v1";
        public const string BattleDeckKey = "ShatteredSail.BattleDeck.v1";
        public const string DispatchMissionKey = "ShatteredSail.Dispatch.Mission.v1";
        public const string DispatchEndsAtKey = "ShatteredSail.Dispatch.EndsAt.v1";
        public const string DispatchDurationKey = DispatchDurationHoursKey;
        public const string CargoKeyPrefix = "ShatteredSail.Cargo.v1.";

        private static readonly Color Ink = UiTheme.Gameplay.Ink;
        private static readonly Color MutedInk = UiTheme.Gameplay.MutedInk;
        private static readonly Color DeepSea = UiTheme.Gameplay.DeepSea;
        private static readonly Color Panel = UiTheme.Gameplay.Panel;
        private static readonly Color PanelRaised = UiTheme.Gameplay.PanelRaised;
        private static readonly Color Teal = UiTheme.Gameplay.Teal;
        private static readonly Color Gold = UiTheme.Gameplay.Gold;
        private static readonly Color Coral = UiTheme.Gameplay.Coral;
        private static readonly Color Green = UiTheme.Gameplay.Green;
        private static readonly Color Fog = UiTheme.Gameplay.Fog;
        private CoreVoyageSession session;
        private TutorialVoyageSession tutorial;
        private LegacyDemoArtCatalog art;
        private PortArtCatalog portArt;
        private PortMapIconCatalog portMapIcons;
        private BattleEnvironmentArtCatalog battleEnvironmentArt;
        private ComponentArtCatalog componentArt;
        private bool tutorialActive;
        private Font interfaceFont;
        private Texture2D seaTexture;
        private Texture2D circleTexture;
        private Sprite seaSprite;
        private Sprite circleSprite;
        private RectTransform contentRoot;
        private RectTransform legacyCanvasRoot;
        private RectTransform legacyOceanBackdrop;
        private Text headerTitle;
        private Text headerStatus;
        private string portNotice = "选择港口设施，完成本次出航整备。";
        private int selectedTavernCandidate = -1;
        private int selectedShipPreview;
        private int selectedUpgradePreview;
        private string courseTargetNotice = "点击海图设定航点。";

        public CoreVoyageSession Session => session;
        public TutorialVoyageSession TutorialSession => tutorial;
        public bool IsTutorialActive => tutorialActive;
        public string ActivePortSection => activePortFacility < 0 ? "港口总览" : PortFacilityNames[activePortFacility];
        private bool profileSaveBlocked;
        private string profileSaveFailure;
        private RectTransform profileSaveFailurePanel;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!ProfileSaveJournal.TryRecover(out string profileNotice))
            {
                profileSaveBlocked = true;
                ReturnToMenuAfterSuspensionFailure(profileNotice);
                return;
            }
            RemoveEditorPreview();
            if (!VoyageSuspensionPersistence.TryRead(out VoyageSuspensionRecord suspendedVoyage, out string suspensionNotice))
            {
                ReturnToMenuAfterSuspensionFailure(suspensionNotice);
                return;
            }
            Application.targetFrameRate = 60;
            session = new CoreVoyageSession();
            RestoreGameCalendar();
            MigrateDispatchGameClock();
            tutorial = new TutorialVoyageSession();
            session.RestoreProfile(
                ProfileGoldPersistence.Load(),
                PlayerPrefs.GetString(CurrentPortKey, "断锚港"));
            tutorialActive = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 0;
            art = new LegacyDemoArtCatalog();
            portArt = new PortArtCatalog();
            portMapIcons = new PortMapIconCatalog();
            battleEnvironmentArt = new BattleEnvironmentArtCatalog();
            componentArt = new ComponentArtCatalog();
            componentShopArt = new ComponentShopArtCatalog();
            cardWorkshopArt = new CardWorkshopArtCatalog();
            dispatchArt = new DispatchArtCatalog();
            maintenanceArt = new MaintenanceArtCatalog();
            RestoreComponentLoadout();
            if (componentInventoryReadFailed)
            {
                ReturnToMenuAfterSuspensionFailure(ComponentInventoryReadFailureNotice);
                return;
            }
            RestorePortProgression();
            RestoreWarehouseCollection();
            RestoreTreasureProgress();
            RestoreSeaChartProgress();
            RestoreCollectionRewards();
            if (suspendedVoyage != null && !TryRestoreSuspendedVoyage(suspendedVoyage, out suspensionNotice))
            {
                ReturnToMenuAfterSuspensionFailure(suspensionNotice);
                return;
            }
            if (suspendedVoyage != null) PersistCrewAtSea();
            facilityArt = new PortFacilityArtCatalog();
            interfaceFont = CreateInterfaceFont();
            seaSprite = CreateSeaSprite();
            circleSprite = CreateCircleSprite();
            EnsureEventSystem();
            BuildCanvas("Core Gameplay Canvas");
            if (suspendedVoyage == null) InitializeGuidedTutorial();
            InitializeAudio();
            Render();
            InitializeAchievementNotifications();
        }

        private void OnDestroy()
        {
            ClearAchievementNotifications();
            ReleaseGeneratedVisualResources();
        }

        private void BuildCanvas(string canvasName)
        {
            GameObject canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // The dashboard is authored on a fixed 1920x1080 surface. Fitting
            // its entire surface keeps edge chart markers clickable in 4:3,
            // 16:10 and resized windows as well as the reference aspect ratio.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            // CanvasScaler can sample a zero editor viewport while the preview is being serialized.
            // Keep the authored preview root at unit scale so child RectTransforms remain visible and editable.
            canvasRect.localScale = Vector3.one;
            contentRoot = CreateRect("Phase Content", canvasRect, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            legacyCanvasRoot = canvasRect;
        }

        private void Render()
        {
            if (profileSaveBlocked) return;
            CloseVoyageCargo(false);
            SaveSeaChartProgress();
            RefreshCollectionRewards();
            RestoreGuidedTutorialControls();
            UpdateGuidedTutorialProgress();
            ResolveBoardingComponentDrop();
            bool battle = session != null && session.Phase == CoreDemoPhase.Battle;
            if (battle)
                RemoveLegacyBattleScaffolding();
            else
                EnsureLegacyCanvasScaffolding();
            InvalidateCurrentUiView();
            ClearChildren(contentRoot);
            bool navigation = session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter;
            contentRoot.anchorMin = navigation ? Vector2.zero : Vector2.one * 0.5f;
            contentRoot.anchorMax = navigation ? Vector2.one : Vector2.one * 0.5f;
            contentRoot.sizeDelta = navigation ? Vector2.zero : PhaseSurfaceLayout.ContentSize;
            contentRoot.anchoredPosition = Vector2.zero;
            if (!battle && headerTitle != null)
            {
                headerTitle.transform.parent.gameObject.SetActive(true);
                Image legacyHeaderSurface = headerTitle.transform.parent.GetComponent<Image>();
                if (legacyHeaderSurface != null) legacyHeaderSurface.enabled = true;
                headerTitle.enabled = true;
                headerTitle.text = GameClientInfo.DisplayName + "";
            }
            if (!battle && headerStatus != null)
            {
                headerStatus.enabled = true;
                headerStatus.text = BuildHeaderStatus();
            }
            BuildCurrentUiView();
            BuildGuidedTutorialActions();
            BuildGuidedTutorialOverlay();
            if (navigation) NavigationDashboardLayout.Attach(contentRoot);

            // Tutorial banners and focus decorations are added after the phase
            // content. Keep the regional voyage header above those decorations
            // so it remains a stable navigation/status bar during the lesson.
            if (session.Phase == CoreDemoPhase.Battle)
            {
                BringVoyageHeaderToFront();
                // The authored battle readouts are a separate overlay so they
                // can sit above the regional header without lifting the entire
                // battle card and its ocean artwork over the header.
                BringBattleReadoutsToFront();
            }
            else if (session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter)
            {
                BringVoyageHeaderToFront();
            }

            // Treasure chapter pages are appended last so their modal surface
            // remains above the voyage header, battle readouts and card hand.
            BuildTreasureStoryOverlayIfNeeded();
            PlayScreenTransitionIfNeeded();
        }

        private string BuildHeaderStatus()
        {
            if (tutorialActive)
            {
                return "航海实习";
            }

            if (session.Phase == CoreDemoPhase.Port)
            {
                return session.CurrentPort + "　|　船长资金 " + PortMarketCatalog.FormatPrice(session.ProfileGold) + " 金币";
            }

            if (session.Phase == CoreDemoPhase.Settlement)
            {
                return "航程结算　|　" + PortMarketCatalog.FormatPrice(session.ProfileGold) + " 金币";
            }

            return "船体 " + session.Hull + "/" + session.MaxHull +
                "　士气 " + session.Morale + "/5　航程 " + session.TotalDistanceNm.ToString("0") + " 海里";
        }

        /// <summary>
        /// Removes any legacy generic surfaces left by an older preview or runtime build.
        /// Regional port headers and voyage headers own their screens now.
        /// </summary>
        private void EnsureLegacyCanvasScaffolding()
        {
            RemoveLegacyBattleScaffolding();
        }

        private void RemoveLegacyBattleScaffolding()
        {
            if (legacyCanvasRoot == null && contentRoot != null)
                legacyCanvasRoot = contentRoot.parent as RectTransform;
            if (legacyCanvasRoot == null) return;

            Transform backdrop = legacyCanvasRoot.Find("Ocean Backdrop");
            if (backdrop != null)
            {
                legacyOceanBackdrop = backdrop as RectTransform;
                RemoveLegacyBattleNode(backdrop);
                legacyOceanBackdrop = null;
            }

            Transform header = legacyCanvasRoot.Find("Header");
            if (header != null)
            {
                RemoveLegacyBattleNode(header);
                headerTitle = null;
                headerStatus = null;
            }
        }

        private static void RemoveLegacyBattleNode(Transform node)
        {
            if (node == null) return;
            if (Application.isPlaying)
                Destroy(node.gameObject);
            else
                DestroyImmediate(node.gameObject);
        }
        private void BuildTutorial()
        {
            RectTransform banner = CreateFixedPanel("Tutorial Banner", contentRoot, new Vector2(1820f, 105f), new Vector2(0f, 420f), Panel);
            Text eyebrow = CreateText("Tutorial Eyebrow", banner, new Vector2(1320f, 28f), new Vector2(-180f, 25f), 15, FontStyle.Bold, TextAnchor.MiddleLeft, Teal);
            eyebrow.text = "断锚港航海学院";
            Text title = CreateText("Tutorial Title", banner, new Vector2(1320f, 48f), new Vector2(-180f, -12f), 29, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            title.text = tutorial.Leg < 2 ? "轻帆船首航训练" : "航海士进阶训练";
            Button skip = CreateButton("Skip Tutorial", banner, new Vector2(240f, 52f), new Vector2(750f, 0f), PanelRaised, MutedInk, 16);
            skip.GetComponentInChildren<Text>().text = "跳过教学";
            skip.onClick.AddListener(() => CompleteTutorial(false));

            RectTransform mapPanel = CreateFixedPanel("Tutorial Route", contentRoot, new Vector2(1250f, 815f), new Vector2(-300f, -45f), Panel);
            Text mapTitle = CreateText("Tutorial Map Title", mapPanel, new Vector2(1140f, 42f), new Vector2(0f, 360f), 21, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            mapTitle.text = "教学海图 · 碎帆礁海　" + (tutorial.HasNavigator ? "种子 " + tutorial.Seed : "海图种子待绘制");
            RectTransform mapFrame = CreateFixedPanel("Tutorial Map Frame", mapPanel, new Vector2(1160f, 690f), new Vector2(0f, -20f), DeepSea);
            RawImage mapArt = CreateRawImage("Tutorial Demo Route Map", mapFrame, new Vector2(1148f, 646f), Vector2.zero, art.RouteMap(0), new Rect(0f, 0f, 1f, 1f));
            mapArt.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            mapArt.raycastTarget = false;
            RectTransform routeRoot = CreateRect("Tutorial Route Markers", mapFrame, new Vector2(1100f, 610f), Vector2.zero);
            BuildTutorialRoute(routeRoot);

            RectTransform guide = CreateFixedPanel("Tutorial Guide", contentRoot, new Vector2(550f, 815f), new Vector2(650f, -45f), Panel);
            Text stage = CreateText("Tutorial Stage", guide, new Vector2(470f, 45f), new Vector2(0f, 350f), 23, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            stage.text = TutorialStageTitle();
            Text objective = CreateText("Tutorial Objective", guide, new Vector2(470f, 132f), new Vector2(0f, 255f), 18, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            objective.horizontalOverflow = HorizontalWrapMode.Wrap;
            objective.text = tutorial.GetObjective();
            Text stats = CreateText("Tutorial Stats", guide, new Vector2(470f, 88f), new Vector2(0f, 145f), 15, FontStyle.Bold, TextAnchor.UpperLeft, MutedInk);
            stats.text = "船体 " + tutorial.Hull + "/" + TutorialVoyageSession.MaxHull + "　淡水 " + tutorial.Water + "　食物 " + tutorial.Food +
                "\n木材 " + tutorial.Wood + "　金币 " + tutorial.Gold + "　声望 " + tutorial.Fame +
                "\n航程 " + tutorial.TotalNauticalMiles.ToString("0") + " 海里　用时 " + tutorial.TotalHours.ToString("0.0") + " 小时";
            Text logHeading = CreateText("Tutorial Log Heading", guide, new Vector2(470f, 30f), new Vector2(0f, 76f), 15, FontStyle.Bold, TextAnchor.MiddleLeft, Teal);
            logHeading.text = "教学航海日志";
            Text log = CreateText("Tutorial Log", guide, new Vector2(470f, 260f), new Vector2(0f, -70f), 14, FontStyle.Normal, TextAnchor.UpperLeft, MutedInk);
            log.horizontalOverflow = HorizontalWrapMode.Wrap;
            log.verticalOverflow = VerticalWrapMode.Truncate;
            log.text = BuildTutorialLog(6);

            Button primary = CreateButton("Tutorial Primary", guide, new Vector2(470f, 66f), new Vector2(0f, -305f), Gold, DeepSea, 19);
            ConfigureTutorialPrimary(primary);
            Button reset = CreateButton("Reset Tutorial", guide, new Vector2(180f, 42f), new Vector2(145f, -365f), PanelRaised, MutedInk, 14);
            reset.GetComponentInChildren<Text>().text = "重置本次教学";
            reset.onClick.AddListener(() =>
            {
                tutorial.Reset();
                Render();
            });

            if (tutorial.Phase == TutorialVoyagePhase.ResolvingEncounter)
            {
                BuildTutorialEncounterOverlay();
            }
            else if (tutorial.Phase == TutorialVoyagePhase.Battle)
            {
                BuildTutorialBattleOverlay();
            }
        }

        private void BuildTutorialRoute(RectTransform routeRoot)
        {
            Vector2[] positions =
            {
                new Vector2(-500f, -145f),
                new Vector2(-260f, 105f),
                new Vector2(-20f, -55f),
                new Vector2(245f, 110f),
                new Vector2(500f, -35f),
            };
            string[] firstLabels = { "断锚港", "漂流补给箱", "搁浅的酒桶", "海图暗语", "黑帆伏击" };
            string[] advancedLabels = { "断锚港", "偏航的星盘", "雾礁暗灯", "裂桅号演习舰", "灰鸥港" };
            string[] labels = tutorial.Leg == 2 ? advancedLabels : firstLabels;
            for (int index = 0; index < positions.Length - 1; index += 1)
            {
                CreateLine(routeRoot, positions[index], positions[index + 1], new Color(Gold.r, Gold.g, Gold.b, 0.72f), 5f);
            }

            int current = tutorial.Phase == TutorialVoyagePhase.Complete
                ? 4
                : Mathf.Clamp(tutorial.CurrentNodeIndex + 1, 0, 4);
            for (int index = 0; index < positions.Length; index += 1)
            {
                if (!TutorialChartVisibility.IsNodeVisible(tutorial, index)) continue;
                bool visited = index <= current;
                Color color = index == current ? Gold : visited ? Teal : Html("#263D43");
                Image marker = CreateImage("Tutorial Node " + index, routeRoot, new Vector2(index == 0 || index == 4 ? 44f : 34f, index == 0 || index == 4 ? 44f : 34f), positions[index], color, circleSprite);
                marker.raycastTarget = false;
                Text label = CreateText("Tutorial Node Label " + index, routeRoot, new Vector2(190f, 44f), positions[index] + new Vector2(0f, -42f), 14, FontStyle.Bold, TextAnchor.UpperCenter, visited ? Ink : MutedInk);
                label.text = tutorial.Leg == 2 && index == 2 && !tutorial.HiddenLocationRevealed ? "雾中信号" : labels[index];
            }

            RawImage ship = CreateRawImage("Tutorial Player Ship", routeRoot, new Vector2(112f, 62f), positions[current] + new Vector2(0f, 36f), art.ShipSheet, art.ShipUv(0, 0));
            ship.raycastTarget = false;
        }

        private void ConfigureTutorialPrimary(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            button.interactable = true;
            switch (tutorial.Phase)
            {
                case TutorialVoyagePhase.AwaitingDeparture:
                    label.text = "开始第一次出航";
                    button.onClick.AddListener(() =>
                    {
                        tutorial.BeginFirstLeg();
                        Render();
                    });
                    break;
                case TutorialVoyagePhase.ReadyToSail:
                    label.text = tutorial.Leg == 2 && tutorial.CurrentNodeIndex >= 2 ? "驶入灰鸥港" : "驶向下一个教学信号";
                    button.onClick.AddListener(() =>
                    {
                        tutorial.SailToNext();
                        Render();
                    });
                    break;
                case TutorialVoyagePhase.FirstLegComplete:
                    label.text = "招募航海士，开始进阶训练";
                    button.onClick.AddListener(() =>
                    {
                        tutorial.BeginAdvancedLeg();
                        Render();
                    });
                    break;
                case TutorialVoyagePhase.Complete:
                    label.text = "领取奖励，进入港口";
                    button.onClick.AddListener(() => CompleteTutorial(true));
                    break;
                default:
                    label.text = "请先处理当前教学事件";
                    button.interactable = false;
                    break;
            }
        }

        private void BuildTutorialEncounterOverlay()
        {
            TutorialEncounter encounter = tutorial.CurrentEncounter;
            if (encounter == null) return;
            var choices = new List<EncounterDossierChoice>();
            for (int index = 0; index < encounter.Choices.Count; index++)
            {
                int captured = index;
                TutorialChoice choice = encounter.Choices[index];
                bool available = tutorial.IsChoiceAvailable(index);
                string condition = string.IsNullOrEmpty(choice.RequiredCrew) ? string.Empty : "需要" + CrewLabel(choice.RequiredCrew);
                choices.Add(new EncounterDossierChoice
                {
                    ObjectName = "Tutorial Choice " + index, Label = choice.Label,
                    Detail = choice.Detail + (string.IsNullOrEmpty(condition) ? string.Empty : " · " + condition),
                    Hours = choice.DurationHours, Available = available,
                    LockedReason = "缺少所需船员", Resolve = () => { tutorial.ResolveChoice(captured); PlaySfx("encounter_choice", 0.5f); Render(); }
                });
            }
            if (encounter.Optional)
                choices.Add(new EncounterDossierChoice
                {
                    ObjectName = "Tutorial Skip Encounter", Label = "略过事件，继续航行",
                    Detail = "不消耗事件时间", Available = true, IsSkip = true,
                    Resolve = () => { tutorial.SkipCurrentEncounter(); PlaySfx("encounter_choice", 0.42f); Render(); }
                });
            BuildEncounterDossier("Tutorial Encounter", encounter.Name, encounter.Category, encounter.Description,
                !encounter.Optional, 0, 0, "新手航海日志 · 首航训练", SeaTerrainType.OpenWater,
                art.EventSheet(0), art.EncounterUv(encounter.Id), choices);
        }

        private void BuildTutorialBattleOverlay()
        {
            RectTransform overlay = CreateStretchPanel("Tutorial Battle Overlay", contentRoot, new Color(0.015f, 0.035f, 0.045f, 0.93f));
            RectTransform card = CreateFixedPanel("Tutorial Battle Card", overlay, new Vector2(1400f, 720f), Vector2.zero, PanelRaised);
            RawImage playerShip = CreateRawImage("Tutorial Player Battle Ship", card, new Vector2(300f, 165f), new Vector2(-470f, 225f), art.ShipSheet, art.ShipUv(0, 0));
            playerShip.raycastTarget = false;
            RawImage enemyShip = CreateRawImage("Tutorial Enemy Battle Ship", card, new Vector2(300f, 165f), new Vector2(470f, 225f), art.ShipSheet, art.ShipUv(3, 0));
            enemyShip.raycastTarget = false;
            ConfigureBattleShipIdle(playerShip.rectTransform, 0f, 4.5f, 1.2f);
            ConfigureBattleShipIdle(enemyShip.rectTransform, 1.35f, 5.5f, 1.5f);
            CreateBattleHullBar("Tutorial Player Hull Bar", card, new Vector2(300f, 18f), new Vector2(-470f, 120f), tutorial.Hull, TutorialVoyageSession.MaxHull, Green);
            CreateBattleHullBar("Tutorial Enemy Hull Bar", card, new Vector2(300f, 18f), new Vector2(470f, 120f), tutorial.BattleEnemyHull, tutorial.BattleEnemyMaxHull, Coral);
            Text title = CreateText("Tutorial Battle Title", card, new Vector2(1220f, 50f), new Vector2(0f, 305f), 29, FontStyle.Bold, TextAnchor.MiddleCenter, Coral);
            title.text = "教学海战 · 裂桅号演习舰";
            Text status = CreateText("Tutorial Battle Status", card, new Vector2(760f, 125f), new Vector2(0f, 205f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            status.text = "敌舰船体 " + tutorial.BattleEnemyHull + "/" + tutorial.BattleEnemyMaxHull + "　下一次反击 " + tutorial.NextEnemyDamage +
                "\n我方船体 " + tutorial.Hull + "　当前格挡 " + tutorial.BattleBlock + "　木材 " + tutorial.Wood +
                "\n" + tutorial.LastBattleMessage;

            RectTransform actions = CreateRect("Tutorial Battle Actions", card, new Vector2(1120f, 300f), new Vector2(0f, -55f));
            HorizontalLayoutGroup layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            AddTutorialBattleAction(actions, "Tutorial Fire Cannons", "火炮齐射", "造成 9 点伤害", TutorialBattleAction.FireCannons, "card_cannon_barrage", Coral);
            AddTutorialBattleAction(actions, "Tutorial Brace", "全员防御", "获得 8 点格挡", TutorialBattleAction.Brace, "card_brace", Teal);
            AddTutorialBattleAction(actions, "Tutorial Repair", "紧急修理", "木材 -2，船体 +6", TutorialBattleAction.EmergencyRepair, "card_emergency_repair", Gold);
            Text hint = CreateText("Tutorial Battle Hint", card, new Vector2(1100f, 40f), new Vector2(0f, -310f), 15, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            hint.text = "每次行动后，敌舰都会反击。";
        }

        private void AddTutorialBattleAction(RectTransform parent, string name, string label, string detail, TutorialBattleAction action, string cardId, Color color)
        {
            bool available = tutorial.IsBattleActionAvailable(action);
            Button button = CreateButton(name, parent, new Vector2(350f, 285f), Vector2.zero, available ? color : Html("#34484A"), Ink, 17);
            ConfigureBattleCardPresentation(button, color);
            RawImage cardArt = CreateRawImage("Action Art", button.transform, new Vector2(190f, 190f), new Vector2(0f, 36f), art.CardSheet, art.CardUv(cardId));
            cardArt.raycastTarget = false;
            Text text = button.GetComponentInChildren<Text>();
            text.rectTransform.sizeDelta = new Vector2(300f, 70f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -96f);
            text.text = label + "\n" + detail;
            button.interactable = available;
            button.onClick.AddListener(() =>
            {
                BeginTutorialBattleAnimation(button, action);
            });
        }

        private string TutorialStageTitle()
        {
            if (tutorial.Phase == TutorialVoyagePhase.AwaitingDeparture) return "01 · 第一次出航";
            if (tutorial.Phase == TutorialVoyagePhase.FirstLegComplete) return "05 · 招募航海士";
            if (tutorial.Phase == TutorialVoyagePhase.Complete) return "10 · 航海学院毕业";
            if (tutorial.Phase == TutorialVoyagePhase.Battle) return "09 · 教学卡牌海战";
            if (tutorial.Phase == TutorialVoyagePhase.ReadyToSail) return tutorial.Leg == 1 ? "首航 · 航向规划" : "进阶 · 航向规划";
            if (tutorial.CurrentEncounter == null) return "教学航线";
            switch (tutorial.CurrentEncounter.Id)
            {
                case "tutorial_first_event": return "02 · 不可跳过事件";
                case "tutorial_optional_event": return "03 · 可跳过事件";
                case "tutorial_crew_requirement_event": return "04 · 船员需求";
                case "tutorial_ambush_event": return "05 · 伏击与返航";
                case "tutorial_navigator_event": return "07 · 航海士能力";
                case "tutorial_hidden_event": return "08 · 隐藏事件";
                case "tutorial_boss_event": return "09 · 首领战";
                default: return "教学事件";
            }
        }

        private string BuildTutorialLog(int maximumLines)
        {
            StringBuilder builder = new StringBuilder();
            IReadOnlyList<string> entries = tutorial.LogEntries;
            int start = Math.Max(0, entries.Count - maximumLines);
            for (int index = entries.Count - 1; index >= start; index -= 1)
            {
                builder.Append("• ").Append(entries[index]);
                if (index > start) builder.AppendLine();
            }
            return builder.ToString();
        }

        private void CompleteTutorial(bool graduated)
        {
            if (graduated && tutorial.Phase != TutorialVoyagePhase.Complete)
            {
                return;
            }

            if (graduated)
            {
                session.RestoreProfile(session.ProfileGold, "灰鸥港");
                if (PlayerPrefs.GetInt(TutorialRewardGrantedKey, 0) == 0)
                {
                    int reward = Math.Max(40, tutorial.Gold - 100);
                    session.GrantProfileGold(reward, "航海学院毕业奖励");
                    PlayerPrefs.SetInt(TutorialRewardGrantedKey, 1);
                }
            }

            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            tutorialActive = false;
            activePortFacility = -1;
            SaveProgress();
            Render();
        }

        private void SaveProgress()
        {
            // Inspector/UI preview sample data must never replace the player's profile.
            if (!Application.isPlaying || editorPreviewBuilding) return;
            if (profileSaveBlocked) throw new ProfileSaveException(profileSaveFailure ?? "角色保存等待恢复。");
            try
            {
                if (session == null || !CoreVoyageSession.DestinationPorts.Contains(session.CurrentPort) ||
                    warehouseCollections.Keys.Any(port => !CoreVoyageSession.DestinationPorts.Contains(port)))
                    throw new ProfileSaveException("角色港口或仓库归属无法识别，已停止保存并保留原档案。");
                if (!ProfileSaveJournal.TryBegin(out string notice)) throw new ProfileSaveException(notice);
                RefreshCollectionRewards();
                // The durable journal protects these writes even on platforms
                // where Set reaches native storage before PlayerPrefs.Save.
                SaveComponentInventory();
                SaveWarehouseCollection();
                SaveTreasureProgress();
                QueueSeaChartProgress();
                SaveGameCalendar();
                ProfileGoldPersistence.Save(session.ProfileGold);
                PlayerPrefs.SetString(CurrentPortKey, session.CurrentPort);
                PlayerPrefs.SetInt(SelectedShipKey, session.SelectedShipIndex);
                SaveStageBattleDecks();
                SaveSpecialCardProgress(false);
                SaveBattleCardUpgrades();
                PlayerPrefs.SetString(BattleDeckKey, string.Join(",", configuredBattleDeck));
                SaveCargo();
                AchievementCatalog.CaptureProgress();
                PlayerPrefs.Save();
                if (!ProfileSaveJournal.TryComplete(out notice)) throw new ProfileSaveException(notice);
            }
            catch (Exception error)
            {
                BlockAfterProfileSaveFailure(error.Message);
                // Do not let an existing void caller continue to leave the
                // scene, grant follow-up rewards or render another save.
                throw error is ProfileSaveException ? error : new ProfileSaveException(profileSaveFailure, error);
            }
            // Presentation must never turn an already committed save into a
            // storage failure. This helper catches its own display failures.
            NotifyCommittedAchievementsSafely();
        }

        private void BlockAfterProfileSaveFailure(string reason)
        {
            profileSaveBlocked = true;
            profileSaveFailure = reason;
            ClearAchievementNotifications();
            StopAllCoroutines();
            enabled = false;
            if (profileSaveFailurePanel != null || interfaceFont == null) return;
            var overlayObject = new GameObject("Profile Save Failure Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayObject.transform.SetParent(transform, false);
            Canvas overlay = overlayObject.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 32760;
            CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            profileSaveFailurePanel = CreateRect("Profile Save Failure", overlay.transform, Vector2.zero, Vector2.zero);
            profileSaveFailurePanel.anchorMin = Vector2.zero;
            profileSaveFailurePanel.anchorMax = Vector2.one;
            profileSaveFailurePanel.offsetMin = profileSaveFailurePanel.offsetMax = Vector2.zero;
            Image shade = profileSaveFailurePanel.gameObject.AddComponent<Image>();
            shade.color = new Color(0.01f, 0.03f, 0.04f, 0.97f);
            shade.raycastTarget = true;
            Text title = CreateText("Profile Save Failure Title", profileSaveFailurePanel, new Vector2(1000f, 70f), new Vector2(0f, 190f), 34, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = "角色保存未完成";
            Text body = CreateText("Profile Save Failure Detail", profileSaveFailurePanel, new Vector2(1120f, 240f), new Vector2(0f, 5f), 23, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            body.text = reason + "\n\n已暂停游戏和档案写入。请修复存储问题后重试恢复；\n恢复会丢弃本次未完成的保存，并返回主菜单。";
            Button recover = CreateButton("Recover Profile Save", profileSaveFailurePanel, new Vector2(660f, 68f), new Vector2(0f, -210f), Gold, DeepSea, 24);
            recover.GetComponentInChildren<Text>().text = "恢复上次存档并返回主菜单";
            recover.navigation = new Navigation { mode = Navigation.Mode.None };
            recover.onClick.AddListener(() =>
            {
                if (!Application.CanStreamedLevelBeLoaded("MainMenu")) { body.text = "主菜单场景不可用，原日志已保留，请检查安装文件后重试。"; return; }
                if (!ProfileSaveJournal.TryRecover(out string notice)) { body.text = notice + "\n\n尚未恢复。修复存储问题后可再次点击重试。"; return; }
                VoyageSuspensionPersistence.PendingNotice = notice;
                SceneManager.LoadScene("MainMenu");
            });
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(recover.gameObject);
        }

        public static string CargoKey(string resource)
        {
            return CargoKeyPrefix + resource;
        }

        private void SaveCargo()
        {
            PlayerPrefs.SetInt(CargoKey("Water"), session.Cargo.Water);
            PlayerPrefs.SetInt(CargoKey("Food"), session.Cargo.Food);
            PlayerPrefs.SetInt(CargoKey("Beer"), session.Cargo.Beer);
            PlayerPrefs.SetInt(CargoKey("Medicine"), session.Cargo.Medicine);
            PlayerPrefs.SetInt(CargoKey("Wood"), session.Cargo.Wood);
            PlayerPrefs.SetInt(CargoKey("Iron"), session.Cargo.Iron);
            PlayerPrefs.SetInt(CargoKey("Cloth"), session.Cargo.Cloth);
            PlayerPrefs.SetInt(CargoKey("Powder"), session.Cargo.Powder);
            PlayerPrefs.SetInt(CargoKey("Cannonballs"), session.Cargo.Cannonballs);
            PlayerPrefs.SetInt(CargoKey("Gold"), session.Cargo.Gold);
            PlayerPrefs.SetInt(CargoKey("Fame"), session.Cargo.Fame);
        }

        private void BuildVoyageFacilityBody()
        {
            // Preview detection still uses this marker; it does not need a visible background.
            CreateRect("Port Briefing", contentRoot, new Vector2(1820f, 86f), new Vector2(0f, 340f));

            // Use the space below either header; the destination row and actions
            // stay fixed while enlarged ship details can scroll independently.
            BeginDepartureChartSelectionView();
            RectTransform routePanel = CreateFixedPanel("Route Selection", contentRoot, DepartureLayout.RouteSize, DepartureLayout.RoutePosition, FacilityPanelColor());
            BuildDepartureDestinationSelection(routePanel);
            BuildTreasureDepartureRoute(routePanel);
            for (int index = 0; session.SelectedTreasureMap == null && index < CoreVoyageSession.Routes.Count; index += 1)
            {
                int capturedIndex = index;
                CoreRouteDefinition route = CoreVoyageSession.Routes[index];
                bool selected = index == session.SelectedRouteIndex;
                float rowY = 200f - index * 200f;
                Button routeButton = CreateButton(
                    "Route " + index,
                    routePanel,
                    new Vector2(1060f, 132f),
                    new Vector2(0f, rowY),
                    selected ? new Color(0.08f, 0.28f, 0.24f, 0.88f) : ScenePlaqueColor(0.78f),
                    Ink,
                    20);
                RawImage preview = CreateRawImage("Route Preview " + index, routeButton.transform, new Vector2(260f, 120f), new Vector2(-380f, 0f), art.RouteMap(index), new Rect(0f, 0f, 1f, 1f));
                UiImageAspect.Fit(preview, new Vector2(260f, 120f));
                preview.raycastTarget = false;
                WoodButtonSkin.SetSelected(routeButton, selected);
                Text routeLabel = routeButton.GetComponentInChildren<Text>();
                routeLabel.rectTransform.sizeDelta = new Vector2(700f, 130f);
                routeLabel.rectTransform.anchoredPosition = new Vector2(165f, 0f);
                routeLabel.alignment = TextAnchor.MiddleLeft;
                routeLabel.text =
                    (selected ? "◆  " : "◇  ") + route.Name + "　推荐战力 " + route.RecommendedPower + "\n" +
                    route.Theme + "　·　每海图格 " + route.NauticalMilesPerMapUnit.ToString("0") + " 海里\n" +
                    "区域首领：" + route.BossName + "（" + route.BossPower + "）";

                routeButton.onClick.AddListener(() =>
                {
                    if (routeButton == null || !routeButton.gameObject.activeInHierarchy ||
                        !IsDepartureShipView() || DepartureChartModeActive) return;
                    if (tutorialActive && capturedIndex != 0) return;
                    session.SelectRoute(capturedIndex);
                    OnGuidedTutorialRouteSelected(capturedIndex);
                    Render();
                });
            }

            RectTransform setupPanel = CreateFixedPanel("Voyage Setup", contentRoot, DepartureLayout.SetupSize, DepartureLayout.SetupPosition, ScenePlaqueColor(0.86f));
            // Information must stay readable over every port's busy artwork.
            Image setupBacking = setupPanel.GetComponent<Image>();
            setupBacking.sprite = null;
            setupBacking.color = ScenePlaqueColor(0.97f);
            Text setupTitle = CreateText("Setup Title", setupPanel, new Vector2(340f, 40f), new Vector2(-90f, 274f), 23, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            setupTitle.text = "旗舰 · 出航配置";
            Button replayTutorial = CreateButton("Replay Tutorial", setupPanel, new Vector2(140f, 36f), new Vector2(190f, 274f), PanelRaised, MutedInk, 13);
            replayTutorial.GetComponentInChildren<Text>().text = "教学说明";
            replayTutorial.onClick.AddListener(() =>
            {
                portNotice = "新存档通过三次真实航行学习基础事件、付费招募与航海士职业事件、默认卡组首领战，再返航绘图、陈列藏品并装配船坞部件；进行中的教学从最近港口节点继续。重新体验请在主菜单创建新存档。";
                Render();
            });
            EnsureDepartureCrewSelection();
            BuildDepartureChartControls(routePanel, setupPanel);
            if (departureCrewConfigurationOpen)
            {
                BuildDepartureCrewConfiguration(setupPanel);
                DepartureLayout.Apply(routePanel, setupPanel);
                return;
            }
            BuildDepartureVesselSelector(setupPanel);
            Text ship = CreateText("Ship Stats", setupPanel, new Vector2(240f, 108f), new Vector2(125f, 180f), 18, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            ship.text = "船体 " + session.MaxHull + "　装甲 " + session.EffectiveArmor + "\n火力 " + session.EffectiveFirepower + "　航速 " + session.EffectiveSpeedKnots.ToString("0") + " 节\n" +
                "船员 " + session.CrewCountValue + "　货舱 " + session.CargoCapacityValue.ToString("0") + "\n" + BuildCrewCapabilitySummary();
            Text destinationSummary = CreateText("Destination Summary", setupPanel, new Vector2(390f, 28f), new Vector2(115f, 100f), 16, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            destinationSummary.text = session.SelectedTreasureMap != null ? "隐藏海域 · 返航港：" + session.CurrentPort :
                "到达港口：" + session.SelectedDestinationPort + "　（当前港：" + session.CurrentPort + "）";

            CoreRouteDefinition selectedRoute = CoreVoyageSession.Routes[session.SelectedRouteIndex];
            ComponentSlot[] previewSlots = { ComponentSlot.Bow, ComponentSlot.Plating, ComponentSlot.Cannon, ComponentSlot.Instrument, ComponentSlot.Sail };
            for (int upgradeIndex = 0; upgradeIndex < previewSlots.Length; upgradeIndex += 1)
            {
                float x = -208f + upgradeIndex * 104f;
                ComponentArtEntry upgradeEntry = EquippedComponent(previewSlots[upgradeIndex]);
                if (upgradeEntry != null)
                {
                    RawImage upgrade = CreateRawImage("Upgrade Part " + upgradeIndex, setupPanel, new Vector2(54f, 54f), new Vector2(x, -46f), upgradeEntry.Texture, new Rect(0f, 0f, 1f, 1f));
                    upgrade.raycastTarget = false;
                }
                Text upgradeName = CreateText("Upgrade Name " + upgradeIndex, setupPanel, new Vector2(100f, 20f), new Vector2(x, -83f), 12, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
                upgradeName.text = upgradeEntry != null ? upgradeEntry.Name : "空槽";
            }

            Text cargo = CreateText("Cargo", setupPanel, new Vector2(520f, 52f), new Vector2(0f, -122f), 16, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            cargo.text = "实际装载　淡水 " + session.Cargo.Water + "　食物 " + session.Cargo.Food + "　药品 " + session.Cargo.Medicine +
                "\n木材 " + session.Cargo.Wood + "　火药 " + session.Cargo.Powder + "　炮弹 " + session.Cargo.Cannonballs +
                "\n货舱 " + session.Cargo.UsedVolume.ToString("0.0") + "/" + session.CargoCapacityValue.ToString("0") + "　航线：" +
                (session.SelectedTreasureMap != null ? session.SelectedTreasureMap.Name : selectedRoute.Name);
            BuildDepartureProvisionPurchase(setupPanel);
            Button adjustSupplies = CreateButton("Adjust Voyage Supplies", setupPanel, new Vector2(180f, 40f), new Vector2(170f, -237f), PanelRaised, Gold, 16);
            adjustSupplies.GetComponentInChildren<Text>().text = "采购补给";
            adjustSupplies.onClick.AddListener(() =>
            {
                activePortFacility = PortFacilityArtCatalog.MarketFacilityIndex;
                portNotice = "按预计航程备足补给。";
                Render();
            });

            Button configureDecks = CreateButton("Configure Voyage Battle Decks", setupPanel,
                new Vector2(210f, 36f), new Vector2(-115f, -277f), PanelRaised, Gold, 14);
            configureDecks.GetComponentInChildren<Text>().text = "配置三阶段牌组";
            configureDecks.onClick.AddListener(() =>
            {
                ResetCardWorkshopView();
                activePortFacility = 4;
                selectedCardWorkshopChest = 3;
                cardWorkshopDeckBuilderOpen = true;
                battleDeckOverviewOpen = true;
                portNotice = "配置备战、炮击、接舷牌组。随船专业船员自动加入专属牌。";
                Render();
            });
            Button configureCrew = CreateButton("Configure Voyage Crew", setupPanel,
                new Vector2(210f, 36f), new Vector2(115f, -277f), PanelRaised, Gold, 14);
            configureCrew.GetComponentInChildren<Text>().text = "船员配置";
            configureCrew.onClick.AddListener(() =>
            {
                departureCrewConfigurationOpen = true;
                EnsureDepartureCrewSelection();
                Render();
            });

            Button start = CreateButton("Start Voyage", setupPanel, new Vector2(520f, 44f), new Vector2(0f, -322f), Gold, DeepSea, 20);
            start.GetComponentInChildren<Text>().text = session.SelectedTreasureMap != null ? "消耗藏宝图 · 扬帆出航" : "扬帆出航";
            start.onClick.AddListener(() =>
            {
                if (start == null || !start.gameObject.activeInHierarchy || !IsDepartureShipView() ||
                    !DepartureChartViewCurrent(setupPanel, departureChartOwner, departureChartRevision)) return;
                if (TryGetSeaChartSaveProtectionReason(out string chartReason))
                {
                    portNotice = chartReason;
                    Render();
                    return;
                }
                RefreshCrewCapabilities();
                ApplyDepartureCrewConfiguration();
                if (!PrepareGuidedTutorialDeparture()) return;
                if (session.SelectedTreasureMap != null && !session.CanStartTreasureVoyage(out string reason))
                {
                    portNotice = reason + "。藏宝图未消耗。";
                    Render();
                    return;
                }
                if (!PrepareDepartureChartForVoyage(setupPanel)) return;
                session.StartVoyage();
                if (session.Phase != CoreDemoPhase.Sailing) { Render(); return; }
                ApplyRecordedSeaChartKnowledge();
                departureUseChartSeed = false;
                PersistCrewAtSea();
                // Tutorial reloads replay the lesson from its pre-departure port
                // snapshot. Keep that wallet/cargo snapshot; treasure departures
                // still persist consumption of their one-shot map immediately.
                if (!tutorialActive) SaveProgress();
                pendingSupplyOrder.Clear();
                courseTargetNotice = "点击海图设定航点。";
                PlaySfx("sail_depart", 0.72f);
                Render();
            });
            RefreshDepartureChartControls();
            DepartureLayout.Apply(routePanel, setupPanel);
        }

        private void BuildSailing()
        {
            BuildVoyageHeader("航海探索");
            BuildNavigationDashboard();
            BringVoyageHeaderToFront();
        }

        private void BuildMap(RectTransform mapRoot)
        {
            Image inputSurface = CreateImage("Map Course Input", mapRoot, new Vector2(1200f, 675f), Vector2.zero, Color.clear);
            inputSurface.raycastTarget = session.Phase == CoreDemoPhase.Sailing;
            MapCourseInputSurface courseInput = inputSurface.gameObject.AddComponent<MapCourseInputSurface>();
            courseInput.Initialize(mapRoot, SelectCourseAtMapPoint);

            BuildVoyageTerrainLayers(mapRoot);

            BuildSeaConditionOverlay(mapRoot);

            RectTransform course = CreateRect("Course Preview Layer", mapRoot, new Vector2(1200f, 675f), Vector2.zero);
            BuildMapCoursePreview(course);

            if (!session.IsTreasureVoyage)
            {
                CreatePortMarker(mapRoot, session.Map.Departure, session.Map.DepartureName, session.Map.DepartureName, false);
                CreatePortMarker(mapRoot, session.Map.Destination, session.Map.DestinationName, session.BossDefeated ? session.Map.DestinationName : session.Map.DestinationName + " · 航道封锁", true);
            }

            for (int index = 0; index < session.Map.Nodes.Count; index += 1)
            {
                CoreMapNode node = session.Map.Nodes[index];
                bool treasureCore = session.IsTreasureVoyage && node.Encounter.Id == "treasure_core";
                if (!node.IsChartVisible && !treasureCore)
                {
                    continue;
                }

                int capturedIndex = index;
                CoreBossKind nodeBossKind = node.Encounter.BossKind;
                bool elite = node.IsBoss || nodeBossKind != CoreBossKind.None;
                Color color = node.Visited ? Html("#536A68") : nodeBossKind == CoreBossKind.Legendary ? Html("#C189F0") : node.IsBoss ? Coral : node.Encounter.Hidden ? Html("#9A76B8") : Gold;
                Button marker = CreateButton("Event Node " + node.Id, mapRoot, new Vector2(elite ? 46f : 36f, elite ? 46f : 36f), MapPosition(node.Position), color, Ink, 18);
                // Use an embedded-font glyph instead of the OS-only crossed swords.
                Text markerGlyph = marker.GetComponentInChildren<Text>();
                markerGlyph.text = node.Visited ? "✓" : treasureCore ? "X" : nodeBossKind == CoreBossKind.Legendary ? "传" : nodeBossKind == CoreBossKind.Treasure ? "藏" : node.IsBoss ? "首" : "!";
                markerGlyph.rectTransform.sizeDelta = marker.GetComponent<RectTransform>().sizeDelta - new Vector2(4f, 4f);
                if (!node.Visited && (nodeBossKind == CoreBossKind.Legendary || nodeBossKind == CoreBossKind.Treasure || treasureCore))
                {
                    VoyageEventMarkerPulse pulse = marker.gameObject.AddComponent<VoyageEventMarkerPulse>();
                    pulse.Configure(node.Id * 0.47f, nodeBossKind == CoreBossKind.Treasure || treasureCore);
                }
                if (!treasureCore) DecorateVoyageEventMarker(marker, node);
                marker.interactable = session.Phase == CoreDemoPhase.Sailing && !node.Visited;
                marker.onClick.AddListener(() =>
                {
                    SelectCourseToEvent(capturedIndex);
                });
                // Keep the caption inside the chart's writable field.  Nodes at
                // the outer grid edge used to place their 190x46 label outside
                // the 1200x675 map root; after the dashboard scale that spilled
                // into the course hint below the chart or into the forward-view
                // panel at the left/right edges.  The marker itself stays at its
                // authored position, while the caption is nudged inward.
                Vector2 labelSize = new Vector2(190f, 46f);
                Vector2 labelPosition = ClampMapCaptionPosition(MapPosition(node.Position) + new Vector2(0f, -39f), labelSize);
                Text label = CreateText("Event Label " + node.Id, mapRoot, labelSize, labelPosition, 14, FontStyle.Bold, TextAnchor.UpperCenter, Ink);
                label.text = !treasureCore && node.IntelRevealed && !node.Discovered ? "情报：未知信号" : node.Encounter.Name;
            }

            RectTransform playerMarker = BuildActiveShipPreview("Player Ship", mapRoot, new Vector2(98f, 64f), MapPosition(session.Position));
            foreach (RawImage layer in playerMarker.GetComponentsInChildren<RawImage>())
                if (layer.texture == portArt.ShipSheet) BattleShipCutout.Attach(layer);
            Image heading = CreateImage("Ship Heading", mapRoot, new Vector2(13f, 26f), MapPosition(session.Position) + new Vector2(0f, 30f), Gold);
            heading.rectTransform.localRotation = Quaternion.Euler(0f, 0f, DirectionRotation(session.Heading));
            heading.raycastTarget = false;
            Vector2 shipLabelSize = new Vector2(140f, 28f);
            Text shipLabel = CreateText("Ship Label", mapRoot, shipLabelSize,
                ClampMapCaptionPosition(MapPosition(session.Position) + new Vector2(0f, 36f), shipLabelSize),
                14, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            shipLabel.text = session.SelectedShip.Name;
        }

        private void BuildMapCoursePreview(RectTransform mapRoot)
        {
            SailingForecast forecast = session.GetForecast();
            if (forecast != null)
            {
                for (int pathIndex = 1; pathIndex < forecast.PathPoints.Count; pathIndex += 1)
                {
                    CreateLine(mapRoot, MapPosition(forecast.PathPoints[pathIndex - 1]), MapPosition(forecast.PathPoints[pathIndex]), new Color(Gold.r, Gold.g, Gold.b, 0.82f), 4f);
                }
                if (session.HasDirectCourseTarget)
                {
                    Image halo = CreateImage("Direct Course Target Halo", mapRoot, new Vector2(54f, 54f), MapPosition(forecast.Target), new Color(Gold.r, Gold.g, Gold.b, 0.28f), circleSprite);
                    halo.raycastTarget = false;
                    Text targetLabel = CreateText("Course Target Label", mapRoot, new Vector2(260f, 34f), MapPosition(forecast.Target) + new Vector2(0f, 38f), 14, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
                    targetLabel.text = courseTargetNotice;
                }
                Image target = CreateImage("Course Target", mapRoot, new Vector2(24f, 24f), MapPosition(forecast.Target), Gold, circleSprite);
                target.raycastTarget = false;
            }
        }

        private void BuildSeaConditionOverlay(RectTransform mapRoot)
        {
            Color currentBlue = Html("#58B8E8");
            Color windBlue = Html("#78C8EF");
            int arrowIndex = 0;
            for (int row = 3; row < CoreVoyageMap.GridRows; row += 6)
            {
                for (int column = 3; column < CoreVoyageMap.GridColumns; column += 8)
                {
                    int cellIndex = row * CoreVoyageMap.GridColumns + column;
                    if (!session.Map.ExploredCells[cellIndex] || !session.Map.IsPassable(column, row))
                    {
                        continue;
                    }

                    VoyagePoint samplePoint = session.Map.CellCenter(column, row);
                    SeaCondition condition = session.GetSeaConditionAt(samplePoint);
                    float jitterX = ((column * 37 + row * 17) % 17) - 8f;
                    float jitterY = ((column * 13 + row * 41) % 13) - 6f;
                    Vector2 position = MapPosition(samplePoint) + new Vector2(jitterX, jitterY);
                    CreateFlowArrow("Current Field Arrow " + arrowIndex, mapRoot, position, condition.CurrentVector, currentBlue, 18f + (arrowIndex % 3) * 2f, 0.28f + (arrowIndex % 2) * 0.06f);
                    arrowIndex += 1;
                }
            }

            SeaCondition global = session.GetSeaConditionAt(session.Position);
            RectTransform legend = CreateFixedPanel("Sea Conditions Legend", mapRoot, new Vector2(310f, 48f), new Vector2(-430f, 292f), new Color(Panel.r, Panel.g, Panel.b, 0.72f));
            legend.GetComponent<Image>().raycastTarget = false;
            CreateFlowArrow("Global Wind Direction Arrow", legend, new Vector2(-112f, 0f), global.WindVector, windBlue, 30f, 0.62f);
            Text legendText = CreateText("Sea Conditions Legend Text", legend, new Vector2(215f, 30f), new Vector2(34f, 0f), 14, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            legendText.text = "全域海风 · " + session.DirectionLabel(global.WindDirection) + " " + global.WindStrength.ToString("0.0") + " 节";
        }

        private void CreateFlowArrow(string name, Transform parent, Vector2 position, VoyagePoint direction, Color color, float length, float alpha)
        {
            RectTransform root = CreateRect(name, parent, new Vector2(length + 18f, 22f), position);
            root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.Y, direction.X) * Mathf.Rad2Deg);
            Color arrowColor = new Color(color.r, color.g, color.b, alpha);
            Image shaft = CreateImage("Shaft", root, new Vector2(length, 2f), new Vector2(-3f, 0f), arrowColor);
            shaft.raycastTarget = false;
            Image upperHead = CreateImage("Upper Head", root, new Vector2(8f, 2f), new Vector2(length * 0.5f - 4f, 3f), arrowColor);
            upperHead.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 40f);
            upperHead.raycastTarget = false;
            Image lowerHead = CreateImage("Lower Head", root, new Vector2(8f, 2f), new Vector2(length * 0.5f - 4f, -3f), arrowColor);
            lowerHead.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            lowerHead.raycastTarget = false;
        }

        private static Vector2 TerrainVisualSize(SeaTerrainType terrain, float cellWidth, float cellHeight)
        {
            switch (terrain)
            {
                case SeaTerrainType.FogBank: return new Vector2(cellWidth * 1.8f, cellHeight * 1.45f);
                case SeaTerrainType.StormFront: return new Vector2(cellWidth * 1.55f, cellHeight * 1.35f);
                case SeaTerrainType.Shoal: return new Vector2(cellWidth * 1.2f, cellHeight * 0.75f);
                case SeaTerrainType.KelpField: return new Vector2(cellWidth * 0.72f, cellHeight * 1.25f);
                case SeaTerrainType.Island: return new Vector2(cellWidth * 1.65f, cellHeight * 1.55f);
                case SeaTerrainType.Beach: return new Vector2(cellWidth * 1.35f, cellHeight * 0.92f);
                case SeaTerrainType.Strait: return new Vector2(cellWidth * 0.8f, cellHeight * 1.35f);
                default: return new Vector2(cellWidth * 0.82f, cellHeight * 0.82f);
            }
        }

        private static Color TerrainVisualColor(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.Shoal: return new Color(0.72f, 0.61f, 0.32f, 0.34f);
                case SeaTerrainType.Reef: return new Color(0.76f, 0.34f, 0.25f, 0.46f);
                case SeaTerrainType.KelpField: return new Color(0.16f, 0.5f, 0.33f, 0.4f);
                case SeaTerrainType.FogBank: return new Color(0.66f, 0.76f, 0.78f, 0.32f);
                case SeaTerrainType.StormFront: return new Color(0.45f, 0.29f, 0.58f, 0.46f);
                case SeaTerrainType.Island: return new Color(0.2f, 0.31f, 0.19f, 0.96f);
                case SeaTerrainType.Beach: return new Color(0.82f, 0.68f, 0.36f, 0.72f);
                case SeaTerrainType.Strait: return new Color(0.25f, 0.72f, 0.78f, 0.32f);
                default: return Color.clear;
            }
        }

        private void SelectCourseAtMapPoint(Vector2 localPoint)
        {
            if (tutorialActive || navigationMotionPlaying || voyageCargoOverlay != null || portSettingsOverlay != null ||
                session.Phase != CoreDemoPhase.Sailing || session.Map == null)
            {
                return;
            }

            Vector2 clampedLocal = new Vector2(
                Mathf.Clamp(localPoint.x, -580f, 580f),
                Mathf.Clamp(localPoint.y, -315f, 315f));
            int snapIndex = -1;
            float closestPixels = 58f;
            for (int index = 0; index < session.Map.Nodes.Count; index += 1)
            {
                CoreMapNode node = session.Map.Nodes[index];
                if (node.Visited || node.Skipped || !node.IsChartVisible)
                {
                    continue;
                }

                float pixels = Vector2.Distance(clampedLocal, MapPosition(node.Position));
                if (pixels <= closestPixels)
                {
                    closestPixels = pixels;
                    snapIndex = index;
                }
            }

            float portPixels = Vector2.Distance(clampedLocal, MapPosition(session.Map.Destination));
            if (!session.IsTreasureVoyage && portPixels <= 58f && (snapIndex < 0 || portPixels < closestPixels))
            {
                SelectCourseToDestination();
                return;
            }

            if (snapIndex >= 0)
            {
                SelectCourseToEvent(snapIndex);
                return;
            }

            VoyagePoint target = new VoyagePoint(
                Mathf.InverseLerp(-580f, 580f, clampedLocal.x) * 100f,
                Mathf.InverseLerp(-315f, 315f, clampedLocal.y) * 64f);
            session.SetCourseTo(target);
            SailingForecast forecast = session.GetForecast();
            courseTargetNotice = forecast == null
                ? "无法设置航点"
                : "自由航点 · " + forecast.ActualDistanceNm.ToString("0") + " 海里";
            PlaySfx("course_plot", 0.44f);
            Render();
        }

        private void SelectCourseToEvent(int nodeIndex)
        {
            if (navigationMotionPlaying || session.Phase != CoreDemoPhase.Sailing || session.Map == null || nodeIndex < 0 || nodeIndex >= session.Map.Nodes.Count)
            {
                return;
            }

            CoreMapNode node = session.Map.Nodes[nodeIndex];
            if (node.Visited || !node.IsChartVisible) return;
            session.SetCourseTo(node.Position);
            courseTargetNotice = "已吸附 · " + (node.IntelRevealed && !node.Discovered ? "未知信号" : node.Encounter.Name);
            PlaySfx("course_plot", 0.48f);
            Render();
        }

        private void SelectCourseToDestination()
        {
            if (tutorialActive || navigationMotionPlaying || session.Phase != CoreDemoPhase.Sailing || session.Map == null) return;
            session.SetCourseTo(session.Map.Destination);
            courseTargetNotice = "已吸附 · " + session.Map.DestinationName + (session.BossDefeated ? "" : " · 航道封锁");
            PlaySfx("course_plot", 0.44f);
            Render();
        }

        private void CreatePortMarker(RectTransform mapRoot, VoyagePoint point, string portName, string labelText, bool destination)
        {
            Vector2 position = MapPosition(point);
            Image halo = CreateImage(destination ? "Destination Port Halo" : "Departure Port Halo", mapRoot, new Vector2(76f, 76f), position, new Color((destination ? Green : Teal).r, (destination ? Green : Teal).g, (destination ? Green : Teal).b, 0.3f), circleSprite);
            halo.raycastTarget = false;
            Texture2D icon = portMapIcons == null ? null : portMapIcons.Get(portName);
            Graphic marker;
            if (icon != null)
            {
                marker = CreateRawImage(destination ? "Destination Port" : "Departure Port", mapRoot, new Vector2(66f, 66f), position, icon, new Rect(0f, 0f, 1f, 1f));
            }
            else
            {
                marker = CreateImage(destination ? "Destination Port" : "Departure Port", mapRoot, new Vector2(48f, 48f), position, destination ? Green : Teal, circleSprite);
            }
            marker.raycastTarget = destination && !tutorialActive && session.Phase == CoreDemoPhase.Sailing;
            if (destination)
            {
                // Use the icon's own bounds, so its edge selects the port exactly.
                // Event markers drawn above it retain their normal click priority.
                Button selectPort = marker.gameObject.AddComponent<Button>();
                selectPort.targetGraphic = marker;
                selectPort.transition = Selectable.Transition.None;
                selectPort.navigation = new Navigation { mode = Navigation.Mode.None };
                selectPort.interactable = marker.raycastTarget;
                selectPort.onClick.AddListener(SelectCourseToDestination);
            }
            Vector2 labelSize = new Vector2(240f, 40f);
            Text label = CreateText(destination ? "Destination Label" : "Departure Label", mapRoot, labelSize,
                ClampMapCaptionPosition(position + new Vector2(0f, -51f), labelSize), 15, FontStyle.Bold, TextAnchor.UpperCenter, Ink);
            label.text = labelText;
        }

        private static Color ProceduralWaterColor(int routeIndex)
        {
            switch (routeIndex)
            {
                case 1: return Html("#0B5261");
                case 2: return Html("#172F58");
                default: return Html("#0B4052");
            }
        }

        private void BuildNavigationControls(RectTransform panel)
        {
            BuildDashboardControls(panel);
        }

        private static string SignedSpeed(float value)
        {
            return (value >= 0f ? "+" : string.Empty) + value.ToString("0.0") + " 节";
        }

        private void BuildEncounterOverlay()
        {
            CoreEncounter encounter = session.ActiveEncounter;
            if (encounter == null) return;
            var choices = new List<EncounterDossierChoice>();
            for (int index = 0; index < encounter.Choices.Count; index++)
            {
                int captured = index;
                CoreEncounterChoice choice = encounter.Choices[index];
                bool available = session.IsChoiceAvailable(index);
                string condition = session.EncounterChoiceConditionSummary(choice);
                choices.Add(new EncounterDossierChoice
                {
                    ObjectName = "Encounter Choice " + index, Label = choice.Label,
                    Detail = ChartEncounterChoiceDetail(index) + (string.IsNullOrEmpty(condition) ? string.Empty : " · " + condition),
                    Hours = choice.Hours, Available = available, StartsBattle = choice.StartsBattle,
                    LockedReason = available ? string.Empty : session.ChoiceUnavailableReason(index),
                    Resolve = () => { session.ResolveChoice(captured); PlaySfx("encounter_choice", 0.5f); Render(); }
                });
            }
            if (!encounter.Mandatory)
                choices.Add(new EncounterDossierChoice
                {
                    ObjectName = "Skip Encounter", Label = "略过事件，继续航行",
                    Detail = "不推进事件时间，不领取事件收益", Available = true, IsSkip = true,
                    Resolve = () => { session.SkipEncounter(); PlaySfx("encounter_choice", 0.42f); Render(); }
                });
            if (session.CanRequestEmergencyTow())
                choices.Add(new EncounterDossierChoice
                {
                    ObjectName = "Emergency Encounter Return", Label = "请求拖带救援",
                    Detail = "放弃事件收益 · 仅保护 35% 本航次剩余战利品", Available = true, IsSkip = true,
                    Resolve = () => { session.RequestEmergencyTow(); PlaySfx("encounter_choice", 0.36f); Render(); }
                });
            SeaTerrainType terrain = session.Map == null ? SeaTerrainType.OpenWater : session.CurrentEncounterSeaTerrain;
            string location = session.Map == null ? "航海日志" : session.Map.Route.Name;
            BuildEncounterDossier("Encounter", encounter.Name, encounter.Category, encounter.Description,
                encounter.Mandatory, encounter.Risk, session.SelectedRouteIndex, location, terrain,
                session.IsTreasureVoyage ? Resources.Load<Texture2D>("TreasureHunt/treasure_hunt_event_sheet") : art.EventSheet(session.SelectedRouteIndex),
                session.IsTreasureVoyage ? TreasureEncounterUv(encounter.Id) : art.EncounterUv(encounter), choices);
            UpdateEncounterChartRewardPresentation();
        }

        private void BuildBattleOverlay()
        {
            BuildVoyageHeader("海战");
            float battleHeaderHeight = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            Vector2 battleSurfaceSize = new Vector2(1920f, 1080f - battleHeaderHeight);
            Vector2 battleSurfacePosition = new Vector2(0f, -battleHeaderHeight * .5f);
            RectTransform overlay = CreateFixedPanel("Battle Overlay", contentRoot,
                battleSurfaceSize, battleSurfacePosition,
                new Color(0.015f, 0.035f, 0.045f, 0.96f));
            RectTransform card = CreateFixedPanel("Battle Card", overlay, new Vector2(1740f, 900f), Vector2.zero, PanelRaised);
            Image cardSurface = card.GetComponent<Image>();
            // The authored battle preview keeps this root as a flat tiled fill;
            // its canvas sprite was removed so the local scene art owns the frame.
            cardSurface.sprite = null;
            cardSurface.type = Image.Type.Tiled;
            cardSurface.pixelsPerUnitMultiplier = 3.0117648f;
            // Fit the authored battle card below the actual ordinary/tutorial header
            // below the header without spilling into the header band.
            card.localScale = Vector3.one * Mathf.Min(
                battleSurfaceSize.x / 1740f,
                battleSurfaceSize.y / 900f);
            CoreBattleState battle = session.Battle;
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            Texture2D battleTexture = boarding
                ? BoardingBattleArtCatalog.ForEnvironment(session.BattleTerrain,
                    NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours))
                : null;
            bool hasBoardingPainting = battleTexture != null;
            if (battleTexture == null)
                battleTexture = battleEnvironmentArt == null ? null : battleEnvironmentArt.ForTerrain(session.BattleTerrain);
            if (battleTexture != null)
            {
                // Frame the lower portion of the location painting: both keels
                // belong below its horizon, with clear water behind the hand.
                // The authored scene painting is the battle surface itself.  It must reach the edge of the battle card so the underlying Ocean Backdrop never peeks through as a second frame.
                RawImage battleBackdrop = CreateRawImage("Battle Scene Art", card, new Vector2(1860f, 900f), Vector2.zero,
                    battleTexture, boarding ? BoardingBattleLayout.CoverUv(battleTexture, new Vector2(1860f, 900f))
                        : BattleViewLayout.BackdropUv(battle.DistanceMeters, new Vector2(1860f, 900f)));
                battleBackdrop.color = Color.white;
                battleBackdrop.raycastTarget = false;
                Color sceneTint = hasBoardingPainting ? new Color(0.015f, 0.025f, 0.035f, 0.08f)
                    : boarding ? NavigationTimeOfDayUtility.OverlayColor(NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours))
                    : new Color(0.015f, 0.035f, 0.045f, 0.22f);
                Image battleTint = CreateStretchPanel("Battle Scene Tint", card, sceneTint).GetComponent<Image>();
                battleTint.raycastTarget = false;
            }

            Texture2D combatShipSheet = portArt.ShipSheet != null ? portArt.ShipSheet : art.ShipSheet;
            int enemyShipIndex = battle.IsBoss ? Mathf.Clamp(session.SelectedRouteIndex + 1, 1, 3) : session.SelectedRouteIndex == 2 ? 3 : 1;
            Rect enemyUv = portArt.ShipSheet != null ? portArt.ShipUv(enemyShipIndex) : art.ShipUv(battle.IsBoss ? 3 : 1, battle.IsBoss ? 1 : 0);
            // Exclude the atlas separator, then face the opponent toward the
            // flagship without mirroring its HUD or combat animation offsets.
            if (combatShipSheet != null)
            {
                // Two compression blocks also exclude dark bleed from the
                // atlas separators on platforms using block compression.
                float insetX = 10f / combatShipSheet.width;
                float insetY = 10f / combatShipSheet.height;
                enemyUv = new Rect(enemyUv.x + insetX, enemyUv.y + insetY,
                    enemyUv.width - insetX * 2f, enemyUv.height - insetY * 2f);
            }
            enemyUv = new Rect(enemyUv.x, enemyUv.y + enemyUv.height * 0.085f,
                enemyUv.width, enemyUv.height * 0.90f);
            enemyUv = new Rect(enemyUv.xMax, enemyUv.y, -enemyUv.width, enemyUv.height);
            BuildStagedBattleScene(card, battle, combatShipSheet, enemyUv);

            Text badge = UiKit.CreateBadge("Battle Badge", card, new Vector2(520f, 34f), new Vector2(0f, 410f), string.Empty, 18, Ink);
            UiLabelBacking.Apply(badge);
            CoreBossKind bossKind = battle.Profile == null ? (battle.IsBoss ? CoreBossKind.Route : CoreBossKind.None) : battle.Profile.BossKind;
            badge.text = (bossKind == CoreBossKind.Legendary ? "传奇战舰挑战" : bossKind == CoreBossKind.Treasure ? "藏宝图首领战" : battle.IsBoss ? "区域首领战" : "航路遭遇战") + " · " + BattleEnvironmentArtCatalog.LabelFor(session.BattleTerrain) + " · 回合 " + (session.IsGuidedBossBattle ? battle.PhaseTurn : battle.Turn);
            // The battle surface scales past the original corner offsets. Inset
            // each callout and reserve extra height for the large crew summary.
            float battleNameHeight = 108f + 40f * (GameReadability.Scale - 1f);
            float battleNameY = 450f - battleNameHeight * .5f;
            RectTransform playerNameBoard = CreateFixedPanel("Player Battle Name Board", card,
                new Vector2(460f, battleNameHeight), new Vector2(-620f, battleNameY), PanelRaised);
            ApplyRetreatBattleWoodBoard(playerNameBoard.GetComponent<Image>());
            playerNameBoard.GetComponent<Image>().raycastTarget = false;
            Text playerName = CreateText("Player Battle Name", card, new Vector2(440f, battleNameHeight - 12f), new Vector2(-620f, battleNameY), 23, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            playerName.text = BattleCrewName(battle, true);
            playerName.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform enemyNameBoard = CreateFixedPanel("Enemy Battle Name Board", card,
                new Vector2(460f, battleNameHeight), new Vector2(620f, battleNameY), PanelRaised);
            ApplyRetreatBattleWoodBoard(enemyNameBoard.GetComponent<Image>());
            enemyNameBoard.GetComponent<Image>().raycastTarget = false;
            Text enemyName = CreateText("Enemy Battle Name", card, new Vector2(440f, battleNameHeight - 12f), new Vector2(620f, battleNameY), 23, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            enemyName.text = BattleCrewName(battle, false);
            enemyName.horizontalOverflow = HorizontalWrapMode.Wrap;
            ConfigureBattleDefenses(card, battle);
            CreateEnemyIntentHud(card, battle);

            Text status = CreateText("Battle Status", card, new Vector2(1030f, 34f), new Vector2(0f, -112f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            if (boarding)
            {
                status.rectTransform.anchoredPosition = new Vector2(0f, 380f);
                status.rectTransform.sizeDelta = new Vector2(1030f, 24f);
                UiLabelBacking.Apply(status, true);
            }
            status.text = battle.LastAction;
            UiLabelBacking.Apply(status);
            RectTransform battleReadouts = CreateRect("Battle Readouts Overlay", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero);
            battleReadouts.localScale = card.localScale;
            BuildNavalBattleTactics(card, battle, battleReadouts);

            RectTransform hand = CreateRect("Battle Hand", card, new Vector2(1120f, 306f), new Vector2(0f, -286f));
            HorizontalLayoutGroup layout = hand.gameObject.AddComponent<HorizontalLayoutGroup>();
            // Extra draws fan within the hand area rather than spilling over
            // the action/resource HUDs. Hover renders a complete card on top.
            layout.spacing = battle.Hand.Count <= 5 ? 16f : (1120f - battle.Hand.Count * 206f) / (battle.Hand.Count - 1);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            retainedBattleHandViews.Clear();
            for (int index = 0; index < battle.Hand.Count; index += 1)
            {
                retainedBattleHandViews.Add(CreateRetainedBattleHandCard(hand, battle, index));
            }

            CreateBattleResourceHud(card, battle, true);
            CreateBattleResourceHud(card, battle, false);

            Button endTurn = CreateButton("End Battle Turn", card, new Vector2(230f, 52f), new Vector2(735f, -265f), Coral, Ink, 18);
            endTurn.GetComponentInChildren<Text>().text = battle.Stage == NavalBattleStage.Preparation ? "备战完成 · 进入炮战" : "结束回合";
            endTurn.onClick.AddListener(() =>
            {
                BeginEnemyTurnAnimation();
            });
            bool emergencyReturn = session.CanRequestEmergencyTow();
            Button retreat = CreateButton(emergencyReturn ? "Emergency Battle Return" : "Retreat Battle", card,
                new Vector2(230f, 44f), new Vector2(-735f, -277.6f), PanelRaised, Ink, 16);
            retreat.GetComponentInChildren<Text>().text = emergencyReturn ? "应急撤离 · 保留 35%" :
                session.CanRetreatBattle ? "脱离战斗 · 撤回港口" : session.IsGuidedBossBattle ? "完成首领战斗教学" : "接舷中无法撤退";
            retreat.interactable = session.CanRetreatBattle || emergencyReturn;
            retreat.onClick.AddListener(() => RetreatFromRetainedBattle(card, battle));
            CacheRetainedBattleView(card, hand, battle, retreat, battleReadouts);
        }

        private void CreateEnemyIntentHud(RectTransform parent, CoreBattleState battle)
        {
            string intent = session.GetEnemyIntent();
            bool defending = !session.EnemyWillRam && (intent.Contains("格挡设为") || battle.Stage == NavalBattleStage.Preparation);
            RectTransform hud = CreateFixedPanel("Enemy Intent HUD", parent, new Vector2(340f, 72f), new Vector2(735f, 334f), new Color(0.025f, 0.065f, 0.08f, 0.94f));
            // Intent is a callout board in the same right-hand column as the enemy name. Reuse the Retreat Battle wood face so the board reads as one physical surface rather than a blue overlay.
            ApplyRetreatBattleWoodBoard(hud.GetComponent<Image>());
            Outline outline = hud.gameObject.AddComponent<Outline>();
            outline.effectColor = defending ? Gold : Coral;
            outline.effectDistance = new Vector2(2f, -2f);
            Image iconPlate = CreateImage("Enemy Intent Icon", hud, new Vector2(50f, 50f), new Vector2(-132f, 0f), defending ? Gold : Coral, circleSprite);
            iconPlate.raycastTarget = false;
            Text icon = CreateText("Enemy Intent Glyph", iconPlate.transform, new Vector2(42f, 42f), Vector2.zero, 22, FontStyle.Bold, TextAnchor.MiddleCenter, DeepSea);
            icon.text = session.EnemyWillRam ? "撞" : defending ? "策" : battle.Stage == NavalBattleStage.Boarding ? "刃" : "攻";
            Text label = CreateText("Enemy Intent Text", hud, new Vector2(245f, 58f), new Vector2(38f, 0f), GameReadability.FontSize(22), FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            label.text = "敌方意图\n" + intent;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            GameReadability.Body(label, 22);
            ConfigureEnemyIntentLayout(parent, hud, label, iconPlate, session.EnemyWillRam);
        }

        private void CreateBattleResourceHud(RectTransform parent, CoreBattleState battle, bool player)
        {
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            Vector2 position = player ? new Vector2(-735f, -390f) : new Vector2(735f, -390f);
            Color accent = player ? Teal : Coral;
            RectTransform hud = CreateFixedPanel(player ? "Player Resource HUD" : "Enemy Resource HUD", parent, new Vector2(230f, 92f), position, new Color(0.02f, 0.07f, 0.085f, 0.94f));
            ApplyRetreatBattleWoodBoard(hud.GetComponent<Image>());
            Outline outline = hud.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            Text title = CreateText(player ? "Player Resource Title" : "Enemy Resource Title", hud, new Vector2(190f, 36f), new Vector2(0f, 30f), GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleCenter, accent);
            title.text = player ? "旗舰资源 · 格挡 " + battle.PlayerBlock : "敌舰状态";
            CreateBattleResourceRow(hud, player ? "能" : "回", player ? battle.Energy + "/" + battle.MaxEnergy : (session.IsGuidedBossBattle ? battle.PhaseTurn : battle.Turn).ToString(), new Vector2(-54f, -4f), player ? Gold : Ink);
            CreateBattleResourceRow(hud, player ? "药" : "盾", player ? (boarding ? session.Cargo.Medicine : session.Cargo.Powder).ToString() : battle.EnemyBlock.ToString(), new Vector2(54f, -4f), player ? Coral : Gold);
            Text reserve = CreateText(player ? "Player Ammunition" : "Enemy Burning", hud, new Vector2(206f, 36f), new Vector2(0f, -31f), GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            reserve.text = player ? (boarding ? "药品仅在有效增疗时消耗" : "炮弹 " + session.Cargo.Cannonballs + " · 装填 +" + battle.PlayerLoadedShot) : "燃烧 " + battle.EnemyBurnTurns + " 回合";
        }

        private static void ApplyRetreatBattleWoodBoard(Image image)
        {
            if (image == null || WoodButtonSkin.Face == null) return;
            image.sprite = WoodButtonSkin.Face;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            image.color = Color.white;
            float corner = Mathf.Clamp(Mathf.Min(image.rectTransform.sizeDelta.x, image.rectTransform.sizeDelta.y) * 0.12f, 3f, 9f);
            image.pixelsPerUnitMultiplier = 112f / corner;
            image.raycastTarget = false;
        }

        private void CreateBattleResourceRow(RectTransform parent, string glyph, string value, Vector2 position, Color color)
        {
            Image icon = CreateImage("Resource " + glyph + " Icon", parent, new Vector2(38f, 38f), position + new Vector2(-22f, 0f), color, circleSprite);
            icon.raycastTarget = false;
            Text glyphText = CreateText("Resource " + glyph + " Glyph", icon.transform, new Vector2(32f, 32f), Vector2.zero, GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleCenter, DeepSea);
            glyphText.text = glyph;
            Text amount = CreateText("Resource " + glyph + " Value", parent, new Vector2(68f, 38f * GameReadability.Scale), position + new Vector2(26f, 0f), GameReadability.FontSize(22), FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            amount.text = value;
        }

        private RectTransform CreateCardKeywordBubble(Transform card, int index, CoreBattleCard battleCard, bool rightSide)
        {
            float extraHeight = session.Battle.Stage == NavalBattleStage.Boarding && battleCard.Repair > 0 ? 110f : 0f;
            RectTransform bubble = CreateFixedPanel("Card Keyword Bubble " + index, card, new Vector2(420f, 552f + extraHeight), new Vector2(rightSide ? 322f : -322f, 118f + extraHeight * .5f), new Color(0.025f, 0.06f, 0.072f, 0.98f));
            Image background = bubble.GetComponent<Image>();
            background.raycastTarget = false;
            Outline outline = bubble.gameObject.AddComponent<Outline>();
            Color accent = CardEffectColor(battleCard);
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(2f, -2f);
            Image pointer = CreateImage("Keyword Bubble Pointer", bubble, new Vector2(18f, 18f), new Vector2(rightSide ? -200f : 200f, -78f - extraHeight * .5f), new Color(0.025f, 0.06f, 0.072f, 0.98f));
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            pointer.raycastTarget = false;
            // Keep the tooltip's three semantic fields on their own rows. The
            // hand card already uses this structure, but the hover description
            // used to omit the card name and put the category in the same line
            // as a generic "特殊词条" label, which made longer categories
            // collide with the effect text on narrow resolutions.
            // The shared surface skin renders this bubble on parchment. Keep
            // category color on its border, and use dark ink for the small text.
            Color tooltipInk = Html("#2B2116");
            Text title = CreateText("Keyword Bubble Title", bubble, new Vector2(384f, 64f), new Vector2(0f, 232f + extraHeight * .5f), 24, FontStyle.Bold, TextAnchor.UpperLeft, tooltipInk);
            title.text = battleCard.Name + "\n<size=18>分类 · " + battleCard.Type + "</size>";
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 22;
            title.resizeTextMaxSize = 24;
            title.raycastTarget = false;
            Text explanation = CreateText("Keyword Bubble Text", bubble, new Vector2(384f, 456f + extraHeight), new Vector2(0f, -36f), 20, FontStyle.Normal, TextAnchor.UpperLeft, tooltipInk);
            explanation.text = BattleCardTooltipText(battleCard, index);
            explanation.horizontalOverflow = HorizontalWrapMode.Wrap;
            explanation.verticalOverflow = VerticalWrapMode.Truncate;
            explanation.resizeTextForBestFit = false;
            explanation.resizeTextMinSize = 20;
            explanation.resizeTextMaxSize = 20;
            explanation.raycastTarget = false;
            ConfigureCardKeywordBubbleGeometry(bubble, rightSide, extraHeight);
            bubble.gameObject.SetActive(false);
            return bubble;
        }

        private static void ConfigureCardKeywordBubbleGeometry(RectTransform bubble, bool rightSide, float extraHeight)
        {
            if (bubble == null) return;
            Text body = bubble.Find("Keyword Bubble Text")?.GetComponent<Text>();
            if (body != null)
            {
                body.fontSize = 20;
                body.resizeTextForBestFit = false;
                body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 384f);
                extraHeight = Mathf.Max(extraHeight, MeasureCardTextHeight(body) - 456f);
            }
            bubble.sizeDelta = new Vector2(420f,552f + extraHeight);
            bubble.anchoredPosition = new Vector2(rightSide ? 322f : -322f,118f + extraHeight * .5f);
            RectTransform pointer = bubble.Find("Keyword Bubble Pointer") as RectTransform;
            if (pointer != null) pointer.anchoredPosition = new Vector2(rightSide ? -200f : 200f,-78f - extraHeight * .5f);
            RectTransform title = bubble.Find("Keyword Bubble Title") as RectTransform;
            if (title != null) { title.sizeDelta = new Vector2(384f,64f); title.anchoredPosition = new Vector2(0f,232f + extraHeight * .5f); }
            RectTransform explanation = bubble.Find("Keyword Bubble Text") as RectTransform;
            if (explanation != null) { explanation.sizeDelta = new Vector2(384f,456f + extraHeight); explanation.anchoredPosition = new Vector2(0f,-36f); }
        }

        private string CardKeywordHelp(CoreBattleCard card)
        {
            StringBuilder help = new StringBuilder();
            help.Append("<color=#5C3A0C>能量</color>　本回合打出卡牌的行动点。\n");
            if (card.Damage > 0) help.Append(session.Battle.Stage == NavalBattleStage.Boarding ? "<color=#6B251C>白刃</color>　削减船员生命，归零即可夺船。\n" : "<color=#6B251C>伤害</color>　扣除敌舰格挡后削减船体。\n");
            if (card.Block > 0) help.Append("<color=#154C46>格挡</color>　本回合优先抵消敌方攻击。\n");
            if (card.Repair > 0) help.Append(session.Battle.Stage == NavalBattleStage.Boarding ? "<color=#234921>治疗</color>　恢复船员生命，不超过上限。\n" : "<color=#234921>修复</color>　恢复旗舰船体，不超过上限。\n");
            if (card.Draw > 0) help.Append("<color=#5C3A0C>抽牌</color>　立即从牌堆补充手牌。\n");
            if (card.PowderCost > 0) help.Append("<color=#6B251C>火药</color>　出航携带的有限弹药资源。\n");
            return help.ToString().TrimEnd();
        }

        private void BuildSettlement()
        {
            BuildSettlementPresentation(session.Settlement, false);
        }

        private string BuildLog(int maximumLines)
        {
            StringBuilder builder = new StringBuilder();
            IReadOnlyList<string> entries = session.LogEntries;
            int start = Math.Max(0, entries.Count - maximumLines);
            for (int index = entries.Count - 1; index >= start; index -= 1)
            {
                builder.Append("• ").Append(entries[index]);
                if (index > start) builder.AppendLine();
            }
            return builder.ToString();
        }

        private static string CrewLabel(string crew)
        {
            if (crew == "navigator") return "航海士";
            if (crew == "carpenter") return "木匠";
            if (crew == "cook") return "厨师";
            if (crew == "doctor") return "船医";
            if (crew == "gunner") return "炮手";
            if (crew == "diver") return "潜水员";
            return crew;
        }

        private static float DirectionRotation(SailingDirection direction)
        {
            switch (direction)
            {
                case SailingDirection.North: return 0f;
                case SailingDirection.NorthEast: return -45f;
                case SailingDirection.East: return -90f;
                case SailingDirection.SouthEast: return -135f;
                case SailingDirection.South: return 180f;
                case SailingDirection.SouthWest: return 135f;
                case SailingDirection.West: return 90f;
                case SailingDirection.NorthWest: return 45f;
                default: return -90f;
            }
        }

    private static Vector2 MapPosition(VoyagePoint point)
    {
        return new Vector2(Mathf.Lerp(-580f, 580f, point.X / 100f), Mathf.Lerp(-315f, 315f, point.Y / 64f));
    }

    private static Vector2 ClampMapCaptionPosition(Vector2 position, Vector2 size)
    {
        // Leave a safe lower margin for the map course hint, which sits just
        // below the chart frame after the dashboard scale is applied.
        float halfWidth = Mathf.Max(0f, size.x * 0.5f);
        float halfHeight = Mathf.Max(0f, size.y * 0.5f);
        return new Vector2(
            Mathf.Clamp(position.x, -600f + halfWidth, 600f - halfWidth),
            Mathf.Clamp(position.y, -292f, 337.5f - halfHeight));
    }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                InputSystemUIInputModule input = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                    .GetComponent<InputSystemUIInputModule>();
                input.actionsAsset = UnityEngine.InputSystem.InputSystem.actions;
            }
        }

        private Font CreateInterfaceFont()
        {
            return GameTypography.InterfaceFont;
        }

        private Sprite CreateSeaSprite()
        {
            seaTexture = new Texture2D(128, 64, TextureFormat.RGBA32, false)
            {
                name = "Core Gameplay Sea Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color shallow = Html("#245A69");
            Color deep = Html("#0C3343");
            for (int y = 0; y < seaTexture.height; y += 1)
            {
                for (int x = 0; x < seaTexture.width; x += 1)
                {
                    float wave = Mathf.Sin(x * 0.23f + y * 0.34f) * 0.05f + Mathf.Sin(x * 0.08f - y * 0.19f) * 0.03f;
                    float gradient = Mathf.Clamp01(y / (float)(seaTexture.height - 1) + wave);
                    seaTexture.SetPixel(x, y, Color.Lerp(deep, shallow, gradient));
                }
            }
            seaTexture.Apply();
            return Sprite.Create(seaTexture, new Rect(0f, 0f, seaTexture.width, seaTexture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateCircleSprite()
        {
            circleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                name = "Core Gameplay Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y += 1)
            {
                for (int x = 0; x < 64; x += 1)
                {
                    float alpha = Mathf.Clamp01(32f - Vector2.Distance(center, new Vector2(x, y)));
                    circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            circleTexture.Apply();
            return Sprite.Create(circleTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
        }

        private static Color Html(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
        }

        private RectTransform CreateStretchPanel(string name, Transform parent, Color color)
        {
            return UiKit.CreateStretchPanel(name, parent, color);
        }

        private RectTransform CreateFixedPanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            return UiKit.CreatePanel(name, parent, size, position, color);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            return UiKit.CreateRect(name, parent, size, position);
        }

        private Image CreateImage(string name, Transform parent, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
        {
            return UiKit.CreateImage(name, parent, size, position, color, sprite);
        }

        private RawImage CreateRawImage(string name, Transform parent, Vector2 size, Vector2 position, Texture texture, Rect uvRect)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uvRect;
            image.color = Color.white;
            return image;
        }

        private Text CreateText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            return UiKit.CreateText(name, parent, size, position, fontSize, style, alignment, color);
        }

        private Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position, Color normalColor, Color textColor, int fontSize)
        {
            return UiKit.CreateButton(name, parent, size, position, normalColor, textColor, fontSize, animate: true, specialSkinFallback: true);
        }

        private InputField CreateInputField(string name, Transform parent, Vector2 size, Vector2 position, string value)
        {
            return UiKit.CreateInputField(name, parent, size, position, value,
                "输入任意种子", Html("#071C24"), Ink, MutedInk);
        }

        private Dropdown CreateSeedDropdown(string name, Transform parent, Vector2 size, Vector2 position,
            string selectedSeed, out InputField legacyInput)
        {
            return CreateDepartureChartSeedInput(name, parent, size, position, selectedSeed, out legacyInput);
        }
        private Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position, float minimum, float maximum, float value)
        {
            return UiKit.CreateSlider(name, parent, size, position, minimum, maximum, value,
                Html("#071C24"), Gold, handleSprite: circleSprite);
        }

        private Image CreateLine(Transform parent, Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 difference = to - from;
            Image line = CreateImage("Course Line", parent, new Vector2(difference.magnitude, thickness), (from + to) * 0.5f, color);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
            line.raycastTarget = false;
            return line;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
