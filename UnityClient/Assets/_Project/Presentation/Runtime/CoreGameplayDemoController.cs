using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Core-loop UI with prefab-backed port headers and retained inventory views.
    /// Other screens share procedural layout builders with the editor previews.
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
        public const string LastVoyageExplorationCoverageKey = "ShatteredSail.LastVoyageExplorationCoverage.v1";
        public const string BattleDeckKey = "ShatteredSail.BattleDeck.v1";
        /// <summary>Set only after the player changes a battle deck in the workshop.</summary>
        public const string BattleDeckEditedKey = "ShatteredSail.BattleDeck.Edited.v1";
        public const string CargoKeyPrefix = "ShatteredSail.Cargo.v1.";

        private static readonly Color Ink = Html("#E8E1C7");
        private static readonly Color MutedInk = Html("#9DB0AE");
        private static readonly Color DeepSea = Html("#071820");
        private static readonly Color Panel = Html("#0F2932");
        private static readonly Color PanelRaised = Html("#173A43");
        private static readonly Color Teal = Html("#287E79");
        private static readonly Color Gold = Html("#D8A541");
        private static readonly Color Coral = Html("#CE6750");
        private static readonly Color Green = Html("#5EAE78");
        private static readonly Color Fog = Html("#061218");
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

        public CoreVoyageSession Session => session;
        public TutorialVoyageSession TutorialSession => tutorial;
        public bool IsTutorialActive => tutorialActive;
        public string ActivePortSection => activePortFacility < 0 ? "港口总览" : PortFacilityNames[activePortFacility];
        private bool profileSaveBlocked;
        private string profileSaveFailure;
        private RectTransform profileSaveFailurePanel;
        private bool pendingVoyageThreeEventsAchievement;
        private bool pendingVoyageHalfChartAchievement;
        private bool pendingVoyageEventArrivalAchievement;

        private void BuildCanvas(string canvasName)
        {
            GameObject canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Fit the fixed game surface; excess window space becomes letterbox.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            // CanvasScaler can sample a zero editor viewport while the preview is being serialized.
            // Keep the authored preview root at unit scale so child RectTransforms remain visible and editable.
            canvasRect.localScale = Vector3.one;
            canvasRect = FixedGameViewport.Create(canvas);
            contentRoot = CreateRect("Phase Content", canvasRect, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            legacyCanvasRoot = canvasRect;
        }

        private void Render()
        {
            if (profileSaveBlocked) return;
            RestoreShipyardEquipmentViewport();
            CloseBattleDeckOverview(false);
            CloseBoardingCardChoice();
            CloseNavalCardChoice();
            RestoreDeckBuilderNavigationControls();
            RestoreDeparturePaintedHeader();
            CloseTreasureStoryOverlay();
            CloseVoyageCargo(false);
            SaveSeaChartProgress();
            RefreshCollectionRewards();
            RestoreGuidedTutorialControls();
            UpdateGuidedTutorialProgress();
            ResolveBoardingComponentDrop();
            if (TryRefreshPortBody()) return;
            bool battle = session != null && session.Phase == CoreDemoPhase.Battle;
            if (battle)
                RemoveLegacyBattleScaffolding();
            else
                EnsureLegacyCanvasScaffolding();
            // An input field can emit EndEdit while its route panel is disabled.
            // Invalidate the outgoing chart callbacks before either panel is torn down.
            departureChartRevision++;
            ClearPhaseContentForRender();
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
            switch (session.Phase)
            {
                case CoreDemoPhase.Port:
                    BuildPort();
                    break;
                case CoreDemoPhase.Sailing:
                    BuildSailing();
                    break;
                case CoreDemoPhase.Encounter:
                    BuildSailing();
                    BuildEncounterOverlay();
                    break;
                case CoreDemoPhase.Battle:
                    BuildBattleOverlay();
                    break;
                case CoreDemoPhase.Settlement:
                    BuildSettlement();
                    break;
            }
            BuildGuidedTutorialActions();
            BuildGuidedTutorialOverlay();
            if (navigation)
            {
                NavigationDashboardLayout.Attach(contentRoot);
                InitializeNavigationControlColors();
            }

            // Keep the regional header above the phase art and focus markers.
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

            BringGuidedTutorialBannerToFront();

            // Treasure chapter pages are appended last so their modal surface
            // remains above the voyage header, battle readouts and card hand.
            BuildTreasureStoryOverlayIfNeeded();
            PlayScreenTransitionIfNeeded();
            ApplyDeckBuilderNavigationLock();
            RecordRenderedPortView();
            QueueUnusedArtCollection();
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
                "　士气 " + session.Morale + "/" + CoreVoyageSession.MaxMorale +
                (session.Phase == CoreDemoPhase.Battle && session.Battle != null && session.Battle.Stage == NavalBattleStage.Boarding
                    ? "　接舷暴击 " + session.BoardingCriticalChancePercent + "%" : string.Empty) +
                "　航程 " + session.TotalDistanceNm.ToString("0") + " 海里";
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

            RectTransform actions = CreateRect("Tutorial Battle Actions", card, new Vector2(1120f, 355f), new Vector2(0f, -85f));
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
            RectTransform slot = CreateRect(name + " Slot", parent, new Vector2(350f, 335f), Vector2.zero);
            RectTransform face = CreateRect(name + " Face", slot, CardFaceLayout.CardSize, Vector2.zero);
            face.localScale = Vector3.one * Mathf.Min(.88f, (slot.rect.height - 8f) / CardFaceLayout.FrameSize.y);
            Button button = CreateButton(name, face, CardFaceLayout.CardSize, Vector2.zero,
                available ? color : Html("#34484A"), Ink, 17);
            string cardType = action == TutorialBattleAction.FireCannons ? "炮击" :
                action == TutorialBattleAction.EmergencyRepair ? "维修" : "防御";
            BattleCardPalette palette = CardSchoolArtCatalog.Palette(string.Empty);
            Image backing = button.GetComponent<Image>();
            backing.sprite = NauticalSurfaceSkin.Paper;
            backing.type = Image.Type.Tiled;
            backing.color = Color.Lerp(Color.white, palette.Parchment, 0.35f);
            ColorBlock states = button.colors;
            states.normalColor = states.highlightedColor = states.selectedColor = Color.white;
            states.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            states.disabledColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            button.colors = states;
            ConfigureBattleCardPresentation(button, color);
            CoreBattleCard illustrationCard = CoreVoyageSession.CardLibrary.First(card => card.Id == cardId);
            CreateCardIllustration("Action Art", button.transform, illustrationCard);
            Image effectPaper = CreateImage("Tutorial Action Effect Paper", button.transform,
                CardFaceLayout.EffectPaperSize, CardFaceLayout.EffectPaperPosition,
                Color.Lerp(Color.white, palette.Parchment, 0.35f), NauticalSurfaceSkin.Paper);
            effectPaper.type = Image.Type.Tiled;
            effectPaper.pixelsPerUnitMultiplier = NauticalSurfaceSkin.Paper == null ? 1f : NauticalSurfaceSkin.Paper.rect.width / 280f;
            effectPaper.raycastTarget = false;
            RectTransform ribbon = CreateCardOrnament("Tutorial Action Name Ribbon", button.transform,
                CardFaceLayout.NamePlateSize, CardFaceLayout.NamePlatePosition, CardOrnamentKind.NameRibbon, cardType, string.Empty);
            Text title = CreateText("Tutorial Action Name", ribbon, CardFaceLayout.NameSize, CardFaceLayout.NamePosition,
                20, FontStyle.Bold, TextAnchor.MiddleCenter, palette.TitleInk);
            title.text = label;
            title.color = palette.TitleInk;
            CardFaceLayout.FitName(title);
            Text text = button.GetComponentInChildren<Text>();
            text.rectTransform.sizeDelta = CardFaceLayout.EffectSize;
            text.rectTransform.anchoredPosition = CardFaceLayout.EffectPosition;
            text.text = FormatBattleCardDescription(detail);
            text.color = palette.Ink;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            CardFaceLayout.FitEffect(text);
            text.transform.SetAsLastSibling();
            AddCardSchoolFrame(button.transform, string.Empty, available);
            // This legacy tutorial has no action-point economy. Its repair
            // material payment remains described by the actual tutorial rules.
            CreateCardCostIcon("Tutorial Action Energy Icon", "Tutorial Action Energy Cost", button.transform,
                CardFaceLayout.EnergyIconSize, CardSchoolArtCatalog.CostPosition(illustrationCard, CardOrnamentKind.Energy),
                CardOrnamentKind.Energy, cardType, 0);
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
                SaveShipPorts();
                PlayerPrefs.SetString(LastVoyageExplorationCoverageKey,
                    session.HasLastVoyageExplorationCoverage
                        ? session.LastVoyageExplorationCoverage.ToString("R", CultureInfo.InvariantCulture)
                        : string.Empty);
                SaveStageBattleDecks();
                SaveSpecialCardProgress(false);
                SaveBattleCardUpgrades();
                PlayerPrefs.SetString(BattleDeckKey, string.Join(",", configuredBattleDeck));
                SaveCargo();
                if (pendingVoyageThreeEventsAchievement) AchievementCatalog.RecordVoyageThreeEvents();
                if (pendingVoyageHalfChartAchievement) AchievementCatalog.RecordVoyageHalfChart();
                if (pendingVoyageEventArrivalAchievement) AchievementCatalog.RecordVoyageEventArrival();
                AchievementCatalog.CaptureProgress();
                PlayerPrefs.Save();
                if (!ProfileSaveJournal.TryComplete(out notice)) throw new ProfileSaveException(notice);
                ClearPendingVoyageAchievements();
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

        private void ClearPendingVoyageAchievements()
        {
            pendingVoyageThreeEventsAchievement = false;
            pendingVoyageHalfChartAchievement = false;
            pendingVoyageEventArrivalAchievement = false;
        }

        private void BlockAfterProfileSaveFailure(string reason)
        {
            ClearPendingVoyageAchievements();
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
            profileSaveFailurePanel = CreateRect("Profile Save Failure", FixedGameViewport.Create(overlay), Vector2.zero, Vector2.zero);
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
                if (warehouseIncomeFailedVoyage != null &&
                    !VoyageSuspensionPersistence.TrySave(warehouseIncomeFailedVoyage, out string voyageNotice))
                {
                    body.text = "尚未保存原航程，请修复存储问题后再次重试：" + voyageNotice;
                    return;
                }
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

            // Keep the two departure columns aligned with the hand-editable
            // UIPreviewsForMe scene. The route list owns the full left frame;
            // the destination buttons sit above the three route rows.
            BeginDepartureChartSelectionView();
            RectTransform routePanel = CreateFixedPanel("Route Selection", contentRoot, DepartureLayout.RoutePanelSize, DepartureLayout.RoutePanelPosition, FacilityPanelColor());
            DepartureSkin.ApplyPanel(routePanel.GetComponent<Image>());
            BuildDepartureDestinationSelection(routePanel);
            BuildTreasureDepartureRoute(routePanel);
            for (int index = 0; (UsesDepartureDockScene || session.SelectedTreasureMap == null) && index < CoreVoyageSession.Routes.Count; index += 1)
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
                DepartureSkin.SetRouteSelected(routeButton, selected);
                Text routeLabel = routeButton.GetComponentInChildren<Text>();
                routeLabel.rectTransform.sizeDelta = new Vector2(700f, 130f);
                routeLabel.rectTransform.anchoredPosition = new Vector2(165f, 0f);
                routeLabel.alignment = TextAnchor.MiddleLeft;
                routeLabel.fontStyle = FontStyle.Normal;
                routeLabel.text =
                    (selected ? "◆  " : "◇  ") + "<size=26>" + route.Name + "</size>\n" +
                    route.Theme + "\n" +
                    "每海图格 " + route.NauticalMilesPerMapUnit.ToString("0") + " 海里\n区域首领：" + SpecialWarshipArtCatalog.RouteCandidatesLabel(route) +
                    (session.PirateLevel < route.UnlockLevel ? " · 等级 " + route.UnlockLevel + " 开放" : string.Empty);

                routeButton.onClick.AddListener(() =>
                {
                    if (routeButton == null || !routeButton.gameObject.activeInHierarchy ||
                        !IsDepartureShipView() || departureCrewConfigurationOpen || DepartureChartModeActive || DepartureDockTreasureModeActive) return;
                    if (tutorialActive && capturedIndex != 0) return;
                    if (!session.CanSelectRoute(capturedIndex, out _)) return;
                    session.SelectRoute(capturedIndex);
                    OnGuidedTutorialRouteSelected(capturedIndex);
                    Render();
                });
            }

            RectTransform setupPanel = CreateFixedPanel("Voyage Setup", contentRoot, DepartureLayout.SetupPanelSize, DepartureLayout.SetupPanelPosition, ScenePlaqueColor(0.86f));
            // Information must stay readable over every port's busy artwork.
            Image setupBacking = setupPanel.GetComponent<Image>();
            DepartureSkin.ApplyPanel(setupBacking);
            Text setupTitle = CreateText("Setup Title", setupPanel, new Vector2(340f, 40f), new Vector2(-90f, 274f), 23, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            setupTitle.text = "旗舰 · 出航配置";
            EnsureDepartureCrewSelection();
            BuildDepartureChartControls(routePanel, setupPanel);
            BuildDepartureVesselSelector(setupPanel);
            BuildDepartureOrdinaryCrewControls(setupPanel);
            BuildDepartureProvisionPurchase(setupPanel);
            Button adjustSupplies = CreateButton("Adjust Voyage Supplies", setupPanel, new Vector2(180f, 40f), new Vector2(170f, -237f), PanelRaised, Gold, 16);
            adjustSupplies.GetComponentInChildren<Text>().text = "前往市场";
            adjustSupplies.onClick.AddListener(() =>
            {
                if (adjustSupplies == null || !adjustSupplies.gameObject.activeInHierarchy ||
                    !adjustSupplies.IsInteractable() || !IsDepartureShipView() || departureCrewConfigurationOpen) return;
                OpenFacilityFromDeparture(PortFacilityArtCatalog.MarketFacilityIndex, setupPanel);
                portNotice = "按预计航程备足补给。";
                Render();
            });

            Button configureDecks = CreateButton("Configure Voyage Battle Decks", setupPanel,
                new Vector2(210f, 36f), new Vector2(-115f, -277f), PanelRaised, Gold, 14);
            configureDecks.GetComponentInChildren<Text>().text = "配置三阶段牌组";
            configureDecks.onClick.AddListener(() =>
            {
                if (configureDecks == null || !configureDecks.gameObject.activeInHierarchy ||
                    !configureDecks.IsInteractable() || !IsDepartureShipView() || departureCrewConfigurationOpen) return;
                ResetCardWorkshopView();
                OpenFacilityFromDeparture(4, setupPanel);
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
                if (configureCrew == null || !configureCrew.gameObject.activeInHierarchy ||
                    !IsDepartureShipView() || departureCrewConfigurationOpen) return;
                CommitDepartureSeed(setupPanel);
                departureCrewConfigurationOpen = true;
                BuildDepartureCrewConfiguration(setupPanel);
            });

            Button start = CreateButton("Start Voyage", setupPanel, new Vector2(520f, 44f), new Vector2(0f, -322f), Gold, DeepSea, 20);
            start.GetComponentInChildren<Text>().text = session.SelectedTreasureMap != null ? "消耗藏宝图 · 扬帆出航" : "扬帆出航";
            start.onClick.AddListener(() =>
            {
                if (start == null || !start.gameObject.activeInHierarchy || !IsDepartureShipView() ||
                    !DepartureChartViewCurrent(setupPanel, departureChartOwner, departureChartRevision)) return;
                if (DepartureDockTreasureModeActive && session.SelectedTreasureMap == null) return;
                if (TryGetSeaChartSaveProtectionReason(out string chartReason))
                {
                    portNotice = chartReason;
                    Render();
                    return;
                }
                RefreshCrewCapabilities();
                ApplyDepartureCrewConfiguration();
                if (!session.CanStartWithDepartureCrew(out string crewReason))
                {
                    portNotice = crewReason;
                    Render();
                    return;
                }
                if (!PrepareGuidedTutorialDeparture()) return;
                if (session.SelectedTreasureMap != null && !session.CanStartTreasureVoyage(out string reason))
                {
                    portNotice = reason + "。藏宝图未消耗。";
                    Render();
                    return;
                }
                if (!PrepareDepartureChartForVoyage(setupPanel)) return;
                if (!session.CanStartOrdinaryVoyage(out string routeReason))
                {
                    portNotice = routeReason;
                    Render();
                    return;
                }
                session.StartVoyage(session.SelectedDestinationPort, RecordedDepartureChartLocationIds());
                if (session.Phase != CoreDemoPhase.Sailing) { Render(); return; }
                ApplyRecordedSeaChartKnowledge();
                departureUseChartSeed = false;
                departureDockTreasureMode = false;
                PersistCrewAtSea();
                // Tutorial reloads replay the lesson from its pre-departure port
                // snapshot. Keep that wallet/cargo snapshot; treasure departures
                // still persist consumption of their one-shot map immediately.
                if (!tutorialActive) SaveProgress();
                pendingSupplyOrder.Clear();
                PlaySfx("sail_depart", 0.72f);
                Render();
            });
            RefreshDepartureChartControls();
            ApplyDeparturePresentation(routePanel, setupPanel);
            if (departureCrewConfigurationOpen) BuildDepartureCrewConfiguration(setupPanel);
        }

        private void BuildSailing()
        {
            BuildVoyageHeader("航海探索");
            BuildNavigationDashboard();
            BringVoyageHeaderToFront();
        }

        private void BuildMap(RectTransform mapRoot)
        {
            Image inputSurface = CreateImage("Map Course Input", mapRoot, mapRoot.sizeDelta, Vector2.zero, Color.clear);
            inputSurface.raycastTarget = session.Phase == CoreDemoPhase.Sailing;
            MapCourseInputSurface courseInput = inputSurface.gameObject.AddComponent<MapCourseInputSurface>();
            courseInput.Initialize(mapRoot, SelectCourseAtMapPoint);

            BuildVoyageTerrainLayers(mapRoot);

            BuildSeaConditionOverlay(mapRoot);

            RectTransform course = CreateRect("Course Preview Layer", mapRoot, mapRoot.sizeDelta, Vector2.zero);
            BuildMapCoursePreview(course);

            if (!session.IsTreasureVoyage)
            {
                CreatePortMarker(mapRoot, session.Map.Departure, session.Map.DepartureName, false);
                CreatePortMarker(mapRoot, session.Map.Destination, session.Map.DestinationName, true);
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
                if (!treasureCore) DecorateVoyageEventMarker(marker, node);
                DecorateVoyageEventRiskHalo(marker, node);
                DecorateDestinationBossGuidance(marker, node);
                if (!node.Visited && (nodeBossKind == CoreBossKind.Legendary || nodeBossKind == CoreBossKind.Treasure || treasureCore))
                {
                    VoyageEventMarkerPulse pulse = marker.gameObject.AddComponent<VoyageEventMarkerPulse>();
                    pulse.Configure(node.Id * 0.47f, nodeBossKind == CoreBossKind.Treasure || treasureCore);
                }
                marker.interactable = session.Phase == CoreDemoPhase.Sailing && !node.Visited;
                marker.onClick.AddListener(() =>
                {
                    SelectCourseToEvent(capturedIndex);
                });
            }

            RectTransform playerMarker = BuildActiveShipPreview("Player Ship", mapRoot, new Vector2(98f, 64f), MapPosition(session.Position));
            foreach (RawImage layer in playerMarker.GetComponentsInChildren<RawImage>())
                if (layer.texture == portArt.ShipSheet) BattleShipCutout.Attach(layer);
            ConfigureNavigationMapViewport(mapRoot);
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
                }
                Image target = CreateImage("Course Target", mapRoot, new Vector2(24f, 24f), MapPosition(forecast.Target), Gold, circleSprite);
                target.raycastTarget = false;
            }
        }

        private void BuildSeaConditionOverlay(RectTransform mapRoot)
        {
            Color currentBlue = Html("#58B8E8");
            int arrowIndex = 0;
            for (int row = 3; row < session.Map.Rows; row += 6)
            {
                for (int column = 3; column < session.Map.Columns; column += 8)
                {
                    int cellIndex = row * session.Map.Columns + column;
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
                Mathf.Clamp(localPoint.x, -ChartGridWidth * .5f, ChartGridWidth * .5f),
                Mathf.Clamp(localPoint.y, -ChartGridHeight * .5f, ChartGridHeight * .5f));
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
                (clampedLocal.x / ChartGridWidth + .5f) * session.Map.WorldWidth,
                (clampedLocal.y / ChartGridHeight + .5f) * session.Map.WorldHeight);
            session.SetCourseTo(target);
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
            PlaySfx("course_plot", 0.48f);
            Render();
        }

        private void SelectCourseToDestination()
        {
            if (tutorialActive || navigationMotionPlaying || session.Phase != CoreDemoPhase.Sailing || session.Map == null) return;
            session.SetCourseTo(session.Map.Destination);
            PlaySfx("course_plot", 0.44f);
            Render();
        }

        private void CreatePortMarker(RectTransform mapRoot, VoyagePoint point, string portName, bool destination)
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
            ApplyDestinationPortGuidance(marker, halo, portName, destination);
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
                    Detail = ChartEncounterChoiceDetail(index), Requirement = condition,
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
            bool specialShip = SpecialWarshipArtCatalog.TryLoad(encounter.Id, out Texture2D shipArt, out Rect shipUv);
            BuildEncounterDossier("Encounter", encounter.Name, encounter.Category, encounter.Description,
                encounter.Mandatory, encounter.Risk, session.SelectedRouteIndex, location, terrain,
                specialShip ? shipArt : session.IsTreasureVoyage ? Resources.Load<Texture2D>("TreasureHunt/treasure_hunt_event_sheet") : art.EventSheet(session.SelectedRouteIndex),
                specialShip ? shipUv : session.IsTreasureVoyage ? TreasureEncounterUv(encounter.Id) : art.EncounterUv(encounter), choices);
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
            if (boarding && battleTexture == null)
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
                if (boarding) BoardingDeckBackdrop.Attach(battleBackdrop, session.BattleTerrain,
                    NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours));
                Color sceneTint = hasBoardingPainting ? new Color(0.015f, 0.025f, 0.035f, 0.08f)
                    : boarding ? NavigationTimeOfDayUtility.OverlayColor(NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours))
                    : new Color(0.015f, 0.035f, 0.045f, 0.22f);
                Image battleTint = CreateStretchPanel("Battle Scene Tint", card, sceneTint).GetComponent<Image>();
                battleTint.raycastTarget = false;
            }

            Texture2D combatShipSheet = portArt.ShipSheet != null ? portArt.ShipSheet : art.ShipSheet;
            int enemyShipIndex = battle.IsBoss ? Mathf.Clamp(session.SelectedRouteIndex + 1, 1, 3) : session.SelectedRouteIndex == 2 ? 3 : 1;
            Rect enemyUv = portArt.ShipSheet != null ? portArt.ShipUv(enemyShipIndex) : art.ShipUv(battle.IsBoss ? 3 : 1, battle.IsBoss ? 1 : 0);
            bool specialEnemy = SpecialWarshipArtCatalog.TryLoad(session.ActiveEncounter?.Id, out Texture2D specialSheet, out Rect specialUv);
            if (specialEnemy) { combatShipSheet = specialSheet; enemyUv = specialUv; }
            // Exclude the atlas separator, then face the opponent toward the
            // flagship without mirroring its HUD or combat animation offsets.
            if (combatShipSheet != null && !specialEnemy)
            {
                // Two compression blocks also exclude dark bleed from the
                // atlas separators on platforms using block compression.
                float insetX = 10f / combatShipSheet.width;
                float insetY = 10f / combatShipSheet.height;
                enemyUv = new Rect(enemyUv.x + insetX, enemyUv.y + insetY,
                    enemyUv.width - insetX * 2f, enemyUv.height - insetY * 2f);
            }
            enemyUv = new Rect(enemyUv.xMax, enemyUv.y, -enemyUv.width, enemyUv.height);
            BuildStagedBattleScene(card, battle, combatShipSheet, enemyUv);

            Text badge = CreateText("Battle Badge", card, new Vector2(520f, 34f), new Vector2(0f, 410f), 18, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            UiLabelBacking.Apply(badge);
            CoreBossKind bossKind = battle.Profile == null ? (battle.IsBoss ? CoreBossKind.Route : CoreBossKind.None) : battle.Profile.BossKind;
            badge.text = (bossKind == CoreBossKind.Legendary ? "传奇战舰挑战" : bossKind == CoreBossKind.Treasure ? "藏宝图首领战" : battle.IsBoss ? "区域首领战" : "航路遭遇战") + " · " + BattleEnvironmentArtCatalog.LabelFor(session.BattleTerrain) + " · 回合 " + (session.IsGuidedBossBattle ? battle.PhaseTurn : battle.Turn);
            // Keep the combatant labels in the upper corner callouts from the authored battle layout. Their wide separation leaves the central range/alignment readout unobstructed.
            RectTransform playerNameBoard = CreateFixedPanel("Player Battle Name Board", card,
                new Vector2(400f, 80f), new Vector2(-727f, 410f), PanelRaised);
            ApplyRetreatBattleWoodBoard(playerNameBoard.GetComponent<Image>());
            playerNameBoard.GetComponent<Image>().raycastTarget = false;
            Text playerName = CreateText("Player Battle Name", card, new Vector2(400f, 80f), new Vector2(-727f, 410f), 23, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            playerName.text = BattleCrewName(battle, true);
            RectTransform enemyNameBoard = CreateFixedPanel("Enemy Battle Name Board", card,
                new Vector2(400f, 80f), new Vector2(727f, 410f), PanelRaised);
            ApplyRetreatBattleWoodBoard(enemyNameBoard.GetComponent<Image>());
            enemyNameBoard.GetComponent<Image>().raycastTarget = false;
            Text enemyName = CreateText("Enemy Battle Name", card, new Vector2(400f, 80f), new Vector2(727f, 410f), 23, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            enemyName.text = BattleCrewName(battle, false);
            ConfigureBattleDefenses(card, battle);
            CreateEnemyIntentHud(card, battle);

            RectTransform battleReadouts = CreateRect("Battle Readouts Overlay", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero);
            battleReadouts.localScale = card.localScale;
            BuildNavalBattleTactics(card, battle, battleReadouts);

            RectTransform hand = CreateRect("Battle Hand", card, CardFaceLayout.HandSize, CardFaceLayout.HandPosition);
            retainedBattleHandViews.Clear();
            for (int index = 0; index < battle.Hand.Count; index += 1)
            {
                retainedBattleHandViews.Add(CreateRetainedBattleHandCard(hand, battle, index));
            }
            ArrangeBattleHand(hand);

            CreateBattleResourceHud(card, battle, true);
            CreateBattleResourceHud(card, battle, false);

            Button endTurn = CreateButton("End Battle Turn", card, new Vector2(230f, 52f), new Vector2(735f, -265f), Coral, Ink, 18);
            endTurn.interactable = !battle.NavalAwaitingHeadingSelection;
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
            bool defending = !session.EnemyWillRam && (battle.Stage == NavalBattleStage.Boarding ? session.EnemyBoardingAttackDamage == 0 :
                intent.Contains("格挡设为") || battle.Stage == NavalBattleStage.Preparation);
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
            Text label = CreateText("Enemy Intent Text", hud, new Vector2(245f, 58f), new Vector2(38f, 0f), 15, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            label.text = "敌方意图\n" + intent;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 15;
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
            Text title = CreateText(player ? "Player Resource Title" : "Enemy Resource Title", hud, new Vector2(190f, 24f), new Vector2(0f, 30f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, accent);
            title.text = player ? "旗舰资源 · 格挡 " + battle.PlayerBlock : "敌舰状态";
            CreateBattleResourceRow(hud, player ? "能" : "回",
                player ? battle.Energy + "/" + battle.MaxEnergy : (session.IsGuidedBossBattle ? battle.PhaseTurn : battle.Turn).ToString(),
                new Vector2(-54f, -4f), player ? Gold : Ink,
                player ? CardOrnamentKind.Energy : (CardOrnamentKind?)null);
            // Keep the stable "药" row key used by retained value updates. Its
            // visible symbol is gunpowder at sea and medicine during boarding.
            CreateBattleResourceRow(hud, player ? "药" : "盾",
                player ? (boarding ? session.Cargo.Medicine : session.Cargo.Powder).ToString() : battle.EnemyBlock.ToString(),
                new Vector2(54f, -4f), player ? Coral : Gold,
                player && !boarding ? CardOrnamentKind.Gunpowder : (CardOrnamentKind?)null);
            Text reserve = CreateText(player ? "Player Ammunition" : "Enemy Burning", hud, new Vector2(206f, 20f), new Vector2(0f, -31f), 12, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            reserve.text = player ? BattleSupplyReserveText(boarding) : "燃烧 " + battle.EnemyBurnTurns + " 回合";
        }

        private string BattleSupplyReserveText(bool boarding) =>
            (boarding ? "火药 " + session.Cargo.Powder : "炮弹 " + session.Cargo.Cannonballs) + " · 木板 " + session.Cargo.Wood;

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

        private void CreateBattleResourceRow(RectTransform parent, string glyph, string value, Vector2 position, Color color,
            CardOrnamentKind? ornament = null)
        {
            if (ornament.HasValue)
            {
                CreateCardOrnament("Resource " + glyph + " Icon", parent, new Vector2(38f, 38f),
                    position + new Vector2(-22f, 0f), ornament.Value, string.Empty);
            }
            else
            {
                Image icon = CreateImage("Resource " + glyph + " Icon", parent, new Vector2(38f, 38f), position + new Vector2(-22f, 0f), color, circleSprite);
                icon.raycastTarget = false;
                Text glyphText = CreateText("Resource " + glyph + " Glyph", icon.transform, new Vector2(32f, 32f), Vector2.zero, 16, FontStyle.Bold, TextAnchor.MiddleCenter, DeepSea);
                glyphText.text = glyph;
            }
            Text amount = CreateText("Resource " + glyph + " Value", parent, new Vector2(48f, 38f), position + new Vector2(26f, 0f), 18, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            amount.text = value;
        }

        private RectTransform CreateCardKeywordBubble(Transform card, int index, CoreBattleCard battleCard, bool rightSide)
        {
            string tooltipText = BattleCardTooltipText(battleCard, index);
            if (string.IsNullOrWhiteSpace(tooltipText)) return null;
            float extraHeight = 0f;
            float bubbleOffset = CardFaceLayout.FrameSize.x * .5f + 222f;
            RectTransform bubble = CreateFixedPanel("Card Keyword Bubble " + index, card, new Vector2(420f, 552f + extraHeight), new Vector2(rightSide ? bubbleOffset : -bubbleOffset, 118f + extraHeight * .5f), new Color(0.025f, 0.06f, 0.072f, 0.98f));
            Image background = bubble.GetComponent<Image>();
            background.raycastTarget = false;
            Outline outline = bubble.gameObject.AddComponent<Outline>();
            Color accent = CardSchoolArtCatalog.Palette(battleCard.SchoolId).Accent;
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
            title.text = battleCard.Name + "\n<size=18>" + CardKeywordCategory(battleCard) + "</size>";
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 22;
            title.resizeTextMaxSize = 24;
            title.raycastTarget = false;
            Text explanation = CreateText("Keyword Bubble Text", bubble, new Vector2(384f, 456f + extraHeight), new Vector2(0f, -36f), 20, FontStyle.Normal, TextAnchor.UpperLeft, tooltipInk);
            explanation.text = tooltipText;
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
                // The calculation-only bubble can shrink as well as grow. Keep
                // the original lower edge/pointer and reserve the title above it.
                extraHeight = Mathf.Max(120f, MeasureCardTextHeight(body)) - 456f;
            }
            bubble.sizeDelta = new Vector2(420f,552f + extraHeight);
            float bubbleOffset = CardFaceLayout.FrameSize.x * .5f + 222f;
            bubble.anchoredPosition = new Vector2(rightSide ? bubbleOffset : -bubbleOffset,118f + extraHeight * .5f);
            RectTransform pointer = bubble.Find("Keyword Bubble Pointer") as RectTransform;
            if (pointer != null) pointer.anchoredPosition = new Vector2(rightSide ? -200f : 200f,-78f - extraHeight * .5f);
            RectTransform title = bubble.Find("Keyword Bubble Title") as RectTransform;
            if (title != null) { title.sizeDelta = new Vector2(384f,64f); title.anchoredPosition = new Vector2(0f,232f + extraHeight * .5f); }
            RectTransform explanation = bubble.Find("Keyword Bubble Text") as RectTransform;
            if (explanation != null) { explanation.sizeDelta = new Vector2(384f,456f + extraHeight); explanation.anchoredPosition = new Vector2(0f,-36f); }
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

    private Vector2 MapPosition(VoyagePoint point)
    {
        return new Vector2((point.X / session.Map.WorldWidth - .5f) * ChartGridWidth,
            (point.Y / session.Map.WorldHeight - .5f) * ChartGridHeight);
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
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            if (name == "Fill") NauticalSurfaceSkin.ApplyControlPart(panel.GetComponent<Image>());
            else NauticalSurfaceSkin.ApplyPanel(panel.GetComponent<Image>(), color);
            return rect;
        }

        private RectTransform CreateFixedPanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            NauticalSurfaceSkin.ApplyPanel(image, color);
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private Image CreateImage(string name, Transform parent, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            NauticalSurfaceSkin.ApplyControlPart(image);
            return image;
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
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            GameTypography.Apply(text, fontSize, style);
            text.alignment = alignment;
            text.color = NauticalSurfaceSkin.ReadableTextColor(parent, color);
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        private Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position, Color normalColor, Color textColor, int fontSize)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = normalColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (rect.GetComponent<NauticalButtonMotion>() == null)
                rect.gameObject.AddComponent<NauticalButtonMotion>();
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.26f, 0.27f, 0.75f);
            button.colors = colors;
            Text label = CreateText("Label", rect, size - new Vector2(24f, 10f), Vector2.zero, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            label.text = name;
            if (!WoodButtonSkin.Apply(button, normalColor))
                NauticalSurfaceSkin.ApplySpecialButton(button, normalColor);
            return button;
        }

        private InputField CreateInputField(string name, Transform parent, Vector2 size, Vector2 position, string value)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = Html("#071C24");
            NauticalSurfaceSkin.ApplyPanel(background, background.color);
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            Text valueText = CreateText("Text", rect, size - new Vector2(32f, 8f), Vector2.zero, 19, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            valueText.raycastTarget = true;
            Text placeholder = CreateText("Placeholder", rect, size - new Vector2(32f, 8f), Vector2.zero, 18, FontStyle.Italic, TextAnchor.MiddleLeft, MutedInk);
            placeholder.text = "输入任意种子";
            input.textComponent = valueText;
            input.placeholder = placeholder;
            input.text = value;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private Dropdown CreateSeedDropdown(string name, Transform parent, Vector2 size, Vector2 position,
            string selectedSeed, out InputField legacyInput)
        {
            return CreateDepartureChartSeedInput(name, parent, size, position, selectedSeed, out legacyInput);
        }
        private Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position, float minimum, float maximum, float value)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            RectTransform background = CreateRect("Background", root, new Vector2(size.x, 8f), Vector2.zero);
            background.gameObject.AddComponent<Image>().color = Html("#071C24");
            NauticalSurfaceSkin.ApplyControlPart(background.GetComponent<Image>());
            RectTransform fillArea = CreateRect("Fill Area", root, new Vector2(size.x - 18f, 8f), new Vector2(-9f, 0f));
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Gold;
            NauticalSurfaceSkin.ApplyControlPart(fill.GetComponent<Image>());
            RectTransform handleArea = CreateRect("Handle Slide Area", root, new Vector2(size.x - 24f, size.y), Vector2.zero);
            Image handle = CreateImage("Handle", handleArea, new Vector2(28f, 0f), Vector2.zero, Gold, circleSprite);
            slider.fillRect = fill;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.value = value;
            return slider;
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
