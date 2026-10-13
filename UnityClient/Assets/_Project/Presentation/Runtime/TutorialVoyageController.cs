using System;
using System.Collections.Generic;
using System.Text;
using ShatteredSail.Core.Random;
using ShatteredSail.Core.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    public sealed class TutorialVoyageController : MonoBehaviour
    {
        private static readonly Color Ink = Html("#E8E1C7");
        private static readonly Color MutedInk = Html("#A9B3B0");
        private static readonly Color DeepSea = Html("#081A24");
        private static readonly Color Panel = Html("#102B34");
        private static readonly Color PanelRaised = Html("#173943");
        private static readonly Color Teal = Html("#2A7C78");
        private static readonly Color Gold = Html("#D9A441");
        private static readonly Color Coral = Html("#CF694F");
        private static readonly Color Green = Html("#5FAE78");

        private readonly List<Image> nodeMarkers = new List<Image>();
        private readonly List<Text> nodeLabels = new List<Text>();
        private readonly Vector2[] nodePositions =
        {
            new Vector2(-520f, -70f),
            new Vector2(-270f, 65f),
            new Vector2(-15f, -35f),
            new Vector2(250f, 80f),
            new Vector2(520f, -40f),
        };

        private TutorialVoyageSession session;
        private Font interfaceFont;
        private Sprite seaSprite;
        private Sprite reefSprite;
        private Sprite circleSprite;
        private Texture2D seaTexture;
        private Texture2D circleTexture;
        private Text routeTitle;
        private Text routeSubtitle;
        private Text statsText;
        private Text stageText;
        private Text objectiveText;
        private Text logText;
        private Button primaryButton;
        private Text primaryButtonLabel;
        private GameObject modalOverlay;
        private Text modalTitle;
        private Text modalBadge;
        private Text modalBody;
        private RectTransform modalChoiceRoot;
        private RectTransform routeRoot;
        private RectTransform shipMarker;
        private int renderedLeg = -1;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            session = new TutorialVoyageSession();
            interfaceFont = CreateInterfaceFont();
            circleSprite = CreateCircleSprite();
            seaSprite = CreateSeaSprite();
            reefSprite = CreateReefSprite();
            EnsureEventSystem();
            BuildInterface();
            Render();
        }

        private void Update()
        {
            if (shipMarker == null || session == null)
            {
                return;
            }

            int positionIndex = Mathf.Clamp(session.CurrentNodeIndex + 1, 0, nodePositions.Length - 1);
            shipMarker.anchoredPosition = Vector2.Lerp(
                shipMarker.anchoredPosition,
                nodePositions[positionIndex] + new Vector2(0f, 28f),
                1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        }

        private void OnDestroy()
        {
            if (reefSprite != null) Destroy(reefSprite);
            if (seaSprite != null)
            {
                Destroy(seaSprite);
            }

            if (circleSprite != null)
            {
                Destroy(circleSprite);
            }

            if (seaTexture != null)
            {
                Destroy(seaTexture);
            }

            if (circleTexture != null)
            {
                Destroy(circleTexture);
            }
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new GameObject("Tutorial Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform canvasRect = FixedGameViewport.Create(canvas);
            RectTransform routeInfo = CreateFixedPanel("Voyage Route Info", canvasRect, new Vector2(1872f, 48f), new Vector2(0f, 360f), Panel);
            routeTitle = CreateText("Route Title", routeInfo, new Vector2(430f, 40f), new Vector2(-690f, 0f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            routeSubtitle = CreateText("Subtitle", routeInfo, new Vector2(940f, 40f), new Vector2(190f, 0f), 17, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            RectTransform stats = CreateFixedPanel("Voyage Status", canvasRect, new Vector2(1872f, 66f), new Vector2(0f, 410f), Panel);
            statsText = CreateText("Stats", stats, new Vector2(1816f, 54f), Vector2.zero, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);

            RectTransform mapPanel = CreateFixedPanel("Chart Panel", canvasRect, new Vector2(1260f, 790f), new Vector2(-300f, -35f), Panel);
            Image chartImage = CreateImage("Procedural Sea", mapPanel, new Vector2(1220f, 720f), new Vector2(0f, -20f), Color.white, seaSprite);
            chartImage.type = Image.Type.Simple;
            routeRoot = CreateRect("Route Overlay", chartImage.rectTransform, new Vector2(1180f, 650f), new Vector2(0f, -5f));
            Text chartCaption = CreateText("Chart Caption", mapPanel, new Vector2(1180f, 44f), new Vector2(0f, 355f), 22, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            chartCaption.text = "海图 · 航迹视野会逐段揭开信号";

            RectTransform sidePanel = CreateFixedPanel("Guide Panel", canvasRect, new Vector2(580f, 790f), new Vector2(650f, -35f), Panel);
            stageText = CreateText("Stage", sidePanel, new Vector2(500f, 50f), new Vector2(0f, 330f), 25, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            objectiveText = CreateText("Objective", sidePanel, new Vector2(500f, 180f), new Vector2(0f, 205f), 21, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Keep long step instructions inside the guide bay. Overflowing
            // text used to run through the divider and cover the log heading.
            objectiveText.verticalOverflow = VerticalWrapMode.Truncate;
            objectiveText.resizeTextForBestFit = true;
            objectiveText.resizeTextMinSize = 15;
            objectiveText.resizeTextMaxSize = 21;

            Image divider = CreateImage("Divider", sidePanel, new Vector2(500f, 2f), new Vector2(0f, 95f), Teal);
            divider.raycastTarget = false;
            Text logHeading = CreateText("Log Heading", sidePanel, new Vector2(500f, 38f), new Vector2(0f, 65f), 18, FontStyle.Bold, TextAnchor.MiddleLeft, MutedInk);
            logHeading.text = "航海日志";
            logText = CreateText("Log", sidePanel, new Vector2(500f, 410f), new Vector2(0f, -170f), 17, FontStyle.Normal, TextAnchor.UpperLeft, MutedInk);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            Text help = CreateText("Help", sidePanel, new Vector2(500f, 40f), new Vector2(0f, -350f), 15, FontStyle.Normal, TextAnchor.MiddleLeft, Html("#71898B"));
            help.text = "测试版操作：使用底部按钮航行；事件会自动暂停航线。";

            RectTransform footer = CreateFixedPanel("Footer", canvasRect, new Vector2(1920f, 88f), new Vector2(0f, -496f), Html("#0A2029"));
            primaryButton = CreateButton("Primary Action", footer, new Vector2(520f, 58f), Vector2.zero, Gold, Ink, 21);
            primaryButtonLabel = primaryButton.GetComponentInChildren<Text>();
            Button resetButton = CreateButton("Reset", footer, new Vector2(180f, 48f), new Vector2(810f, 0f), PanelRaised, MutedInk, 17);
            resetButton.GetComponentInChildren<Text>().text = "重新开始";
            resetButton.onClick.AddListener(ResetTutorial);

            BuildModal(canvasRect);
        }

        private void BuildModal(RectTransform canvasRect)
        {
            modalOverlay = CreateStretchPanel("Encounter Overlay", canvasRect, new Color(0.01f, 0.04f, 0.06f, 0.82f)).gameObject;
            RectTransform card = CreateFixedPanel("Encounter Card", modalOverlay.transform, new Vector2(980f, 650f), Vector2.zero, Html("#173943"));
            Image accent = CreateImage("Accent", card, new Vector2(10f, 650f), new Vector2(-485f, 0f), Gold);
            accent.raycastTarget = false;
            modalBadge = CreateText("Badge", card, new Vector2(840f, 34f), new Vector2(0f, 270f), 16, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            modalTitle = CreateText("Event Title", card, new Vector2(840f, 58f), new Vector2(0f, 225f), 31, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            modalBody = CreateText("Event Body", card, new Vector2(840f, 150f), new Vector2(0f, 120f), 20, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            modalBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            // The choice stack begins immediately below this writing bay;
            // clipping/best-fit keeps a long encounter description from
            // obscuring the first choice button.
            modalBody.verticalOverflow = VerticalWrapMode.Truncate;
            modalBody.resizeTextForBestFit = true;
            modalBody.resizeTextMinSize = 14;
            modalBody.resizeTextMaxSize = 20;
            modalChoiceRoot = CreateRect("Choices", card, new Vector2(840f, 315f), new Vector2(0f, -120f));
            VerticalLayoutGroup layout = modalChoiceRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            modalOverlay.SetActive(false);
        }

        private void Render()
        {
            int displayLeg = session.Leg == 2 ? 2 : 1;
            if (displayLeg != renderedLeg)
            {
                BuildRoute(displayLeg);
                renderedLeg = displayLeg;
            }

            if (routeTitle != null) routeTitle.text = displayLeg == 1 ? "断锚港首航训练" : "领航员进阶训练";
            if (routeSubtitle != null) routeSubtitle.text = session.HasNavigator
                ? "碎帆礁海 · 种子 " + session.Seed + " · " + SeedHash.Compute("route_shattered_sail_reef::" + session.Seed).ToString("X8")
                : "碎帆礁海 · 海图尚未绘制，随船航海士可揭示种子";
            statsText.text =
                "船体  " + session.Hull + "/" + TutorialVoyageSession.MaxHull +
                "     淡水  " + session.Water +
                "     食物  " + session.Food +
                "     木材  " + session.Wood +
                "     啤酒  " + session.Beer +
                "     金币  " + session.Gold +
                "     声望  " + session.Fame +
                "     航速  " + session.CurrentSpeedKnots.ToString("0.#") + " 节";
            stageText.text = GetStageTitle();
            objectiveText.text = session.GetObjective();
            logText.text = BuildLogText();
            RefreshMapState();
            RefreshPrimaryButton();
            RefreshModal();
        }

        private void BuildRoute(int leg)
        {
            foreach (Transform child in routeRoot)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            nodeMarkers.Clear();
            nodeLabels.Clear();
            Mulberry32Random random = new Mulberry32Random(SeedHash.Compute(leg == 1 ? TutorialVoyageSession.FirstLegSeed : TutorialVoyageSession.AdvancedLegSeed));
            for (int index = 0; index < 15; index += 1)
            {
                float x = Mathf.Lerp(-560f, 560f, (float)random.NextDouble());
                float y = Mathf.Lerp(-280f, 260f, (float)random.NextDouble());
                float size = Mathf.Lerp(28f, 82f, (float)random.NextDouble());
                Image island = CreateImage("Reef " + index, routeRoot, new Vector2(size * 1.6f, size), new Vector2(x, y), reefSprite == null ? new Color(0.18f, 0.31f, 0.25f, 0.78f) : Color.white, reefSprite == null ? circleSprite : reefSprite);
                island.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 25f, (float)random.NextDouble()));
                island.raycastTarget = false;
            }

            for (int index = 0; index < nodePositions.Length - 1; index += 1)
            {
                CreateLine(routeRoot, nodePositions[index], nodePositions[index + 1], new Color(Gold.r, Gold.g, Gold.b, 0.45f), 5f);
            }

            string[] firstLabels = { "断锚港", "漂流补给箱", "搁浅的酒桶", "陌生海图暗语", "黑帆伏击" };
            string[] advancedLabels = { "断锚港", "偏航的星盘", "雾礁暗灯", "裂桅号演习舰", "灰鸥港" };
            string[] labels = leg == 1 ? firstLabels : advancedLabels;
            for (int index = 0; index < nodePositions.Length; index += 1)
            {
                Image marker = CreateImage("Node " + index, routeRoot, new Vector2(index == 0 || index == 4 ? 46f : 36f, index == 0 || index == 4 ? 46f : 36f), nodePositions[index], Gold, circleSprite);
                marker.raycastTarget = false;
                nodeMarkers.Add(marker);
                Text label = CreateText("Node Label " + index, routeRoot, new Vector2(205f, 56f), nodePositions[index] + new Vector2(0f, -52f), 16, FontStyle.Bold, TextAnchor.UpperCenter, Ink);
                label.text = labels[index];
                nodeLabels.Add(label);
            }

            Image ship = CreateImage("Player Ship", routeRoot, new Vector2(34f, 46f), nodePositions[0] + new Vector2(0f, 28f), Gold);
            ship.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            ship.raycastTarget = false;
            shipMarker = ship.rectTransform;
        }

        private void RefreshMapState()
        {
            if (nodeMarkers.Count != nodePositions.Length)
            {
                return;
            }

            int currentMarker = Mathf.Clamp(session.CurrentNodeIndex + 1, 0, nodeMarkers.Count - 1);
            for (int markerIndex = 0; markerIndex < nodeMarkers.Count; markerIndex += 1)
            {
                bool visible = IsNodeVisible(markerIndex);
                nodeMarkers[markerIndex].gameObject.SetActive(visible);
                nodeLabels[markerIndex].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                bool current = markerIndex == currentMarker;
                bool visited = markerIndex < currentMarker;
                nodeMarkers[markerIndex].color = current ? Coral : visited ? Green : Gold;
                nodeLabels[markerIndex].color = current ? Html("#FFD8C9") : Ink;
            }
        }

        private bool IsNodeVisible(int markerIndex)
        {
            return TutorialChartVisibility.IsNodeVisible(session, markerIndex);
        }

        private void RefreshPrimaryButton()
        {
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.interactable = true;

            switch (session.Phase)
            {
                case TutorialVoyagePhase.AwaitingDeparture:
                    primaryButtonLabel.text = "开始第一次出航";
                    primaryButton.onClick.AddListener(BeginFirstLeg);
                    break;
                case TutorialVoyagePhase.ReadyToSail:
                    bool destination = session.Leg == 2 && session.CurrentNodeIndex + 1 >= session.CurrentLegEncounters.Count;
                    primaryButtonLabel.text = destination ? "驶入灰鸥港并完成测绘" : "航行至下一处信号";
                    primaryButton.onClick.AddListener(SailNext);
                    break;
                case TutorialVoyagePhase.FirstLegComplete:
                    primaryButtonLabel.text = "招募航海士并开始进阶航行";
                    primaryButton.onClick.AddListener(BeginAdvancedLeg);
                    break;
                case TutorialVoyagePhase.Complete:
                    primaryButtonLabel.text = "重新体验教学航线";
                    primaryButton.onClick.AddListener(ResetTutorial);
                    break;
                default:
                    primaryButtonLabel.text = "请先处理当前事件";
                    primaryButton.interactable = false;
                    break;
            }
        }

        private void RefreshModal()
        {
            bool show = session.Phase == TutorialVoyagePhase.ResolvingEncounter || session.Phase == TutorialVoyagePhase.Battle;
            modalOverlay.SetActive(show);
            ClearChildren(modalChoiceRoot);
            if (!show)
            {
                return;
            }

            if (session.Phase == TutorialVoyagePhase.Battle)
            {
                RenderBattleModal();
                return;
            }

            TutorialEncounter encounter = session.CurrentEncounter;
            modalBadge.text = encounter.Category + (encounter.Optional ? " · 可略过" : " · 必须处理");
            modalTitle.text = encounter.Name;
            modalBody.text = encounter.Description;

            for (int index = 0; index < encounter.Choices.Count; index += 1)
            {
                int capturedIndex = index;
                TutorialChoice choice = encounter.Choices[index];
                bool available = session.IsChoiceAvailable(index);
                string crewNote = string.IsNullOrEmpty(choice.RequiredCrew) ? string.Empty : "  [需要" + CrewDisplayName(choice.RequiredCrew) + "]";
                string label = choice.Label + crewNote + "\n" + choice.Detail + " · " + choice.DurationHours.ToString("0.#") + " 小时";
                Button button = CreateModalButton("Choice " + index, label, available ? Teal : Html("#34484A"));
                button.interactable = available;
                button.onClick.AddListener(() => ResolveChoice(capturedIndex));
            }

            if (encounter.Optional)
            {
                Button skip = CreateModalButton("Skip", "略过事件\n不推进事件时间，不获得奖励", Panel);
                skip.onClick.AddListener(SkipEncounter);
            }
        }

        private void RenderBattleModal()
        {
            modalBadge.text = "教学卡牌战斗 · 敌方意图公开";
            modalTitle.text = "裂桅号演习舰  " + session.BattleEnemyHull + "/" + session.BattleEnemyMaxHull;
            modalBody.text = session.LastBattleMessage + "\n\n下一次敌方攻击：" + session.NextEnemyDamage + "  ·  我方船体：" + session.Hull + "  ·  当前格挡：" + session.BattleBlock;

            Button fire = CreateModalButton("Fire Cannons", "火炮齐射\n造成 9 点伤害", Coral);
            fire.onClick.AddListener(() => PerformBattleAction(TutorialBattleAction.FireCannons));
            Button brace = CreateModalButton("Brace", "全员防御\n获得 8 点格挡", Teal);
            brace.onClick.AddListener(() => PerformBattleAction(TutorialBattleAction.Brace));
            Button repair = CreateModalButton("Repair", "紧急修理\n木材 -2、船体 +6", Gold);
            repair.interactable = session.IsBattleActionAvailable(TutorialBattleAction.EmergencyRepair);
            repair.onClick.AddListener(() => PerformBattleAction(TutorialBattleAction.EmergencyRepair));
        }

        private Button CreateModalButton(string name, string label, Color color)
        {
            Button button = CreateButton(name, modalChoiceRoot, new Vector2(840f, 68f), Vector2.zero, color, Ink, 18);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 68f;
            layout.minHeight = 68f;
            button.GetComponentInChildren<Text>().text = label;
            return button;
        }

        private void BeginFirstLeg()
        {
            session.BeginFirstLeg();
            Render();
        }

        private void BeginAdvancedLeg()
        {
            session.BeginAdvancedLeg();
            Render();
        }

        private void SailNext()
        {
            session.SailToNext();
            Render();
        }

        private void ResolveChoice(int choiceIndex)
        {
            session.ResolveChoice(choiceIndex);
            Render();
        }

        private void SkipEncounter()
        {
            session.SkipCurrentEncounter();
            Render();
        }

        private void PerformBattleAction(TutorialBattleAction action)
        {
            session.PerformBattleAction(action);
            Render();
        }

        private void ResetTutorial()
        {
            session.Reset();
            renderedLeg = -1;
            Render();
        }

        private string GetStageTitle()
        {
            switch (session.Phase)
            {
                case TutorialVoyagePhase.AwaitingDeparture:
                    return "01 · 第一次出航";
                case TutorialVoyagePhase.FirstLegComplete:
                    return "05 · 酒馆招募航海士";
                case TutorialVoyagePhase.Complete:
                    return "教学完成";
                case TutorialVoyagePhase.Battle:
                    return "09 · Boss 卡牌对战";
                case TutorialVoyagePhase.ResolvingEncounter:
                    return EncounterStageTitle(session.CurrentEncounter.Id);
                case TutorialVoyagePhase.ReadyToSail:
                    return session.Leg == 1 ? "首航 · 航向规划" : "进阶 · 航向规划";
                default:
                    return "教学航线";
            }
        }

        private static string EncounterStageTitle(string encounterId)
        {
            switch (encounterId)
            {
                case "tutorial_first_event": return "02 · 不可跳过事件";
                case "tutorial_optional_event": return "03 · 可跳过事件";
                case "tutorial_crew_requirement_event": return "04 · 特殊船员需求";
                case "tutorial_ambush_event": return "05 · 发现海盗埋伏";
                case "tutorial_navigator_event": return "07 · 特殊船员事件";
                case "tutorial_hidden_event": return "08 · 探索隐藏事件";
                case "tutorial_boss_event": return "09 · Boss 卡牌对战";
                default: return "教学事件";
            }
        }

        private string BuildLogText()
        {
            StringBuilder builder = new StringBuilder();
            int first = Mathf.Max(0, session.LogEntries.Count - 9);
            for (int index = first; index < session.LogEntries.Count; index += 1)
            {
                builder.Append("• ").Append(session.LogEntries[index]);
                if (index + 1 < session.LogEntries.Count)
                {
                    builder.AppendLine().AppendLine();
                }
            }
            return builder.ToString();
        }

        private static string CrewDisplayName(string crewId)
        {
            return crewId == "navigator" ? "航海士" : crewId == "carpenter" ? "木匠" : crewId;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject input = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.GetComponent<InputSystemUIInputModule>().actionsAsset = UnityEngine.InputSystem.InputSystem.actions;
        }

        private Font CreateInterfaceFont()
        {
            return GameTypography.InterfaceFont;
        }

        private Sprite CreateSeaSprite()
        {
            Texture2D paintedSea = Resources.Load<Texture2D>("VoyageMapV2/water_chart_base_v2");
            if (paintedSea != null)
                return Sprite.Create(paintedSea, new Rect(0f, 0f, paintedSea.width, paintedSea.height), Vector2.one * .5f, 100f);
            // Development fallback only; never destroy a shared Resources texture.
            seaTexture = new Texture2D(128, 64, TextureFormat.RGBA32, false)
            {
                name = "Tutorial Sea Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color shallow = Html("#245868");
            Color deep = Html("#0D3444");
            for (int y = 0; y < seaTexture.height; y += 1)
            {
                for (int x = 0; x < seaTexture.width; x += 1)
                {
                    float wave = Mathf.Sin(x * 0.23f + y * 0.34f) * 0.04f + Mathf.Sin(x * 0.08f - y * 0.19f) * 0.025f;
                    float gradient = Mathf.Clamp01(y / (float)(seaTexture.height - 1) + wave);
                    seaTexture.SetPixel(x, y, Color.Lerp(deep, shallow, gradient));
                }
            }
            seaTexture.Apply();
            return Sprite.Create(seaTexture, new Rect(0f, 0f, seaTexture.width, seaTexture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateReefSprite()
        {
            Texture2D reef = Resources.Load<Texture2D>("VoyageMapV2/terrain_island_v2");
            return reef == null ? null : Sprite.Create(reef, new Rect(0, 0, reef.width, reef.height), Vector2.one * .5f, 100f);
        }

        private Sprite CreateCircleSprite()
        {
            circleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                name = "Tutorial Circle",
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
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.26f, 0.27f, 0.75f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            Text label = CreateText("Label", rect, size - new Vector2(24f, 10f), Vector2.zero, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            label.text = name;
            WoodButtonSkin.Apply(button, normalColor);
            return button;
        }

        private Image CreateLine(Transform parent, Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 difference = to - from;
            Image line = CreateImage("Route Segment", parent, new Vector2(difference.magnitude, thickness), (from + to) * 0.5f, color);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
            line.raycastTarget = false;
            return line;
        }

        private static void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }
}


