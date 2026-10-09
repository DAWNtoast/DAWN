using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const string ComponentShopPurchaseKeyPrefix = "ShatteredSail.ComponentShopPurchase.v1.";
        private const string CrewRecruitmentKeyPrefix = "ShatteredSail.CrewRecruited.v1.";
        private const string CrewVariantRecruitmentKeyPrefix = "ShatteredSail.CrewVariantRecruited.v2.";
        private const string ActiveCrewVariantKeyPrefix = "ShatteredSail.ActiveCrewVariant.v2.";
        private const string ShipOwnedKeyPrefix = "ShatteredSail.ShipOwned.v1.";
        public const string DispatchDurationHoursKey = "ShatteredSail.Dispatch.DurationHours.v1";
        public const string DispatchCrewCsvKey = "ShatteredSail.Dispatch.CrewCsv.v1";
        public const string DispatchCrewKey = DispatchCrewCsvKey;
        public const string DispatchCrewIdentityKey = "ShatteredSail.Dispatch.CrewIdentities.v2";
        public const string DispatchBalanceVersionKey = "ShatteredSail.Dispatch.BalanceVersion";
        // Retired prototype identity: read only to remove its obsolete save flag.
        public const string LegacyWitchRecruitedKey = "ShatteredSail.Crew.WitchRecruited";

        private static readonly string[] PortFacilityNames =
        {
            "船长收藏室",
            "皇家船坞",
            "灰鸥酒馆",
            "部件商店",
            "卡组工坊",
            "远航派遣台",
            "藏宝图桌",
            "港口投资建设",
            "出航码头",
            "港口市场",
        };

        private const int TavernCrewSlotCount = CrewNarrativeCatalog.ProfessionCount;
        private static readonly string[] DispatchNames = { "近岸沉船打捞", "商路护航委托", "雾礁灯塔测绘" };
        private static readonly string[] DispatchCrewHints = { "推荐潜水员：额外收益 +15%", "推荐炮术官：额外收益 +15%", "推荐航海士：额外收益 +15%" };
        private static readonly int[] DispatchDurations = { GameplayBalance.DispatchHours(0), GameplayBalance.DispatchHours(1), GameplayBalance.DispatchHours(2) };
        private static readonly int[] DispatchRecommendedCrew = { GameplayBalance.DispatchSpecialist(0), GameplayBalance.DispatchSpecialist(1), GameplayBalance.DispatchSpecialist(2) };
        private static readonly string[] CardWorkshopChestNames =
        {
            "策略卡牌强化",
            "远程攻击强化",
            "近战卡牌强化",
            "特殊卡牌收藏",
        };

        private static readonly string[] CardWorkshopChestDescriptions =
        {
            "金币强化基础策略牌 · 战前布阵",
            "金币强化基础远程牌 · 炮击机动",
            "金币强化基础近战牌 · 接舷配合",
            "已获得的专属战术 · 随船航行成长",
        };

        private PortFacilityArtCatalog facilityArt;
        private CardWorkshopArtCatalog cardWorkshopArt;
        private ComponentShopArtCatalog componentShopArt;
        private DispatchArtCatalog dispatchArt;
        private MaintenanceArtCatalog maintenanceArt;
        private int activePortFacility = -1;
        private string selectedComponentShopOfferId;
        private int componentShopDisplayPage;
        private bool cardWorkshopDeckBuilderOpen;
        private int selectedCardWorkshopChest = -1;
        private int selectedDispatchMission = -1;
        private int selectedDispatchDurationHours;
        private readonly List<int> selectedDispatchCrew = new List<int>();
        private readonly VoyageSupplyOrder pendingSupplyOrder = new VoyageSupplyOrder();
        private readonly List<int> configuredBattleDeck = new List<int> { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7 };

        private void BuildPort()
        {
            if (!editorPreviewBuilding && Application.isPlaying)
            {
                // Every port card preview must use the same selected roster as departure.
                // The existing selection preserves standby and manually adjusted sailors.
                ApplyDepartureCrewConfiguration();
            }
            if (activePortFacility != 1)
            {
                shipyardDetailOpen = false;
                shipyardReturnToDeparture = false;
                selectedShipPreview = session.SelectedShipIndex;
            }
            if (activePortFacility != PortFacilityArtCatalog.TavernFacilityIndex)
                ResetTavernDossierView();
            if (activePortFacility < 0)
            {
                BuildPortHub();
                return;
            }

            BuildFacilityBackdrop(activePortFacility);
            BuildFacilityHeader(activePortFacility);
            switch (activePortFacility)
            {
                case 0: BuildWarehouseFacility(); break;
                case 1: BuildShipyardFacility(); break;
                case 2: BuildTavernFacility(); break;
                case 3: BuildComponentShopFacility(); break;
                case 4: BuildCardWorkshopFacility(); break;
                case 5: BuildDispatchFacility(); break;
                case 6: BuildTreasureFacility(); break;
                case 7: BuildPortInvestmentFacility(); break;
                case 8: BuildVoyageFacilityBody(); break;
                case 9: BuildMarketFacility(); break;
                default: BuildVoyageFacilityBody(); break;
            }
        }

        private void BuildPortHub()
        {
            Texture2D regionalBackground = portArt.PortBackgroundFor(session.CurrentPort);
            Texture2D background = regionalBackground != null
                ? regionalBackground
                : facilityArt.Background(8);
            RawImage backdrop = CreateRawImage("Port Hub Background", contentRoot,
                PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition, background,
                PhaseSurfaceLayout.BackgroundUv(background));
            backdrop.color = Color.white;
            backdrop.raycastTarget = false;
            Image tint = CreateStretchPanel("Port Hub Tint", contentRoot, new Color(0.018f, 0.045f, 0.055f, 0.08f)).GetComponent<Image>();
            tint.raycastTarget = false;

            BuildPortHubHeader();

            Vector2[] positions =
            {
                new Vector2(-805f, 215f),
                new Vector2(660f, 115f),
                new Vector2(-815f, -90f),
                new Vector2(-355f, -200f),
                new Vector2(-365f, 135f),
                new Vector2(-30f, -30f),
                new Vector2(220f, 55f),
                new Vector2(635f, -275f),
                new Vector2(75f, -250f),
                new Vector2(-560f, -340f),
            };
            Vector2[] hotspotSizes =
            {
                new Vector2(220f, 168f),
                new Vector2(260f, 188f),
                new Vector2(220f, 158f),
                new Vector2(230f, 170f),
                new Vector2(240f, 178f),
                new Vector2(230f, 174f),
                new Vector2(250f, 182f),
                new Vector2(250f, 176f),
                new Vector2(260f, 190f),
                new Vector2(260f, 176f),
            };

            for (int index = 0; index < PortFacilityNames.Length; index += 1)
            {
                int captured = index;
                Button hotspot = CreateButton(
                    "Port Hotspot " + FacilityKey(index),
                    contentRoot,
                    hotspotSizes[index],
                    positions[index],
                    new Color(1f, 1f, 1f, 0f),
                    Ink,
                    16);
                hotspot.GetComponentInChildren<Text>().text = string.Empty;
                ColorBlock colors = hotspot.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                // The facility is represented by the painted background. Keep
                // pointer interaction transparent instead of adding a yellow
                // circle over the tavern (or any other port landmark).
                colors.highlightedColor = colors.normalColor;
                colors.selectedColor = colors.normalColor;
                colors.pressedColor = new Color(Teal.r, Teal.g, Teal.b, 0.34f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.12f;
                hotspot.colors = colors;
                Image hotspotGlow = hotspot.targetGraphic as Image;
                if (hotspotGlow != null)
                {
                    hotspotGlow.sprite = circleSprite;
                }
                Vector2 signPosition = new Vector2(0f, hotspotSizes[index].y * 0.5f + 17f);
                Image sign = CreateImage("Port Sign " + FacilityKey(index), hotspot.transform,
                    new Vector2(200f, 44f), signPosition,
                    index == 8 ? new Color(1f, .85f, .60f, 1f) : new Color(.77f, .75f, .70f, .96f),
                    WoodButtonSkin.Face);
                sign.type = Image.Type.Sliced;
                sign.pixelsPerUnitMultiplier = 24f;
                // Names and their plates route clicks to the same landmark button.
                sign.raycastTarget = true;
                Text label = CreateText(
                    "Port Label " + FacilityKey(index),
                    hotspot.transform,
                    new Vector2(188f, 36f),
                    signPosition,
                    19,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ink);
                label.text = PortFacilityArtCatalog.RegionalFacilityTitle(index, session.CurrentPort, PortFacilityNames[index]);
                UiLabelBacking.Apply(label);
                ConfigurePortHotspotHover(hotspot, hotspotGlow, label, sign);
                hotspot.onClick.AddListener(() =>
                {
                    ResetCardWorkshopView();
                    ResetComponentShopView();
                    ResetDispatchView();
                    investmentSupplyPanelOpen = false;
                    activePortFacility = captured;
                    if (captured == 2) ResetTavernDossierView();
                    portNotice = "已进入" + PortFacilityArtCatalog.RegionalFacilityTitle(captured, session.CurrentPort, PortFacilityNames[captured]) + "。";
                    Render();
                });
            }

            Text hint = CreateText("Port Hub Hint", contentRoot, new Vector2(1400f, 54f), new Vector2(0f, -455f), 16, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            hint.text = "" +
                (string.IsNullOrWhiteSpace(portNotice) ? string.Empty : "\n" + portNotice);
            hint.raycastTarget = false;
            AddSceneLabelOutline(hint);
            UiLabelBacking.Apply(hint);
        }

        private static void ConfigurePortHotspotHover(Button hotspot, Image glow, Text label, Image sign = null)
        {
            if (hotspot == null || glow == null || label == null)
            {
                return;
            }

            hotspot.transition = Selectable.Transition.None;
            // The scene label sits just above the transparent landmark hitbox.
            // Keep it in the same event hierarchy so clicking the visible name
            // invokes the parent Button instead of falling through to the art.
            label.raycastTarget = true;
            glow.color = Color.clear;

            Color restingLabelColor = label.color;
            Color restingSignColor = sign == null ? Color.white : sign.color;
            int restingFontSize = label.fontSize;
            Color hoverLabel = new Color(1f, 0.88f, 0.48f, 1f);

            EventTrigger trigger = hotspot.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = hotspot.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers = new List<EventTrigger.Entry>();
            EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ =>
            {
                glow.color = Color.clear;
                label.color = hoverLabel;
                label.fontSize = restingFontSize + 2;
                if (sign != null) sign.color = Color.Lerp(restingSignColor, new Color(1f, .90f, .70f, 1f), .65f);
            });
            trigger.triggers.Add(pointerEnter);

            EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ =>
            {
                glow.color = Color.clear;
                label.color = restingLabelColor;
                label.fontSize = restingFontSize;
                if (sign != null) sign.color = restingSignColor;
            });
            trigger.triggers.Add(pointerExit);
        }

        private void BuildFacilityBackdrop(int index)
        {
            string portName = session == null ? string.Empty : session.CurrentPort;
            Texture2D texture = facilityArt == null || index == PortFacilityArtCatalog.MaintenanceFacilityIndex
                ? null
                : facilityArt.RegionalBackground(index, portName);
            if (texture == null)
            {
                if (index == PortFacilityArtCatalog.MaintenanceFacilityIndex && facilityArt != null)
                {
                    texture = facilityArt.Background(index);
                }
                else if (index == PortFacilityArtCatalog.MaintenanceFacilityIndex &&
                         maintenanceArt != null && maintenanceArt.Background != null)
                {
                    texture = maintenanceArt.Background;
                }
                else if (facilityArt != null)
                {
                    texture = facilityArt.Background(index);
                }
            }

            RawImage backdrop = CreateRawImage(FacilityKey(index) + " Background", contentRoot,
                PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition, texture,
                PhaseSurfaceLayout.BackgroundUv(texture));
            bool preserveFacilityColor =
                index == 1 ||
                index == PortFacilityArtCatalog.TavernFacilityIndex ||
                index == PortFacilityArtCatalog.ComponentShopFacilityIndex ||
                index == PortFacilityArtCatalog.DispatchFacilityIndex ||
                index == PortFacilityArtCatalog.TreasureFacilityIndex ||
                index == PortFacilityArtCatalog.MaintenanceFacilityIndex ||
                index == PortFacilityArtCatalog.MarketFacilityIndex;
            backdrop.color = preserveFacilityColor ? Color.white : new Color(0.9f, 0.92f, 0.94f, 1f);
            backdrop.raycastTarget = false;
            float tintAlpha = preserveFacilityColor ? 0f : 0.13f;
            Image tint = CreateStretchPanel("Facility Backdrop Tint", contentRoot, new Color(0.005f, 0.025f, 0.035f, tintAlpha)).GetComponent<Image>();
            tint.raycastTarget = false;
        }

        private void BuildFacilityHeader(int index)
        {
            RectTransform header = CreateRegionalPortHeader("Port Facility Header", "Port Facility Frame", out PortHeaderTheme theme);

            Button back = CreatePortHeaderButton("Return To Port Hub", header, new Vector2(200f, 58f), new Vector2(-793f, 0f), "返回港口", 22, theme.Accent);
            back.onClick.AddListener(() =>
            {
                ResetCardWorkshopView();
                ResetComponentShopView();
                ResetDispatchView();
                investmentSupplyPanelOpen = false;
                activePortFacility = -1;
                portNotice = "选择港口设施。";
                Render();
            });

            int previousIndex = (index + PortFacilityNames.Length - 1) % PortFacilityNames.Length;
            string previousTitle = PortFacilityArtCatalog.RegionalFacilityTitle(previousIndex, session.CurrentPort, PortFacilityNames[previousIndex]);
            Button previous = CreatePortHeaderButton("Previous Port Facility", header, new Vector2(166f, 58f),
                new Vector2(-594f, 0f), "← " + previousTitle, 20, theme.Accent);
            previous.onClick.AddListener(() =>
            {
                ResetCardWorkshopView();
                ResetComponentShopView();
                ResetDispatchView();
                investmentSupplyPanelOpen = false;
                activePortFacility = (activePortFacility + PortFacilityNames.Length - 1) % PortFacilityNames.Length;
                Render();
            });
            Text title = CreateText("Port Facility Title", header, new Vector2(236f, 56f), new Vector2(-385f, 0f), 29, FontStyle.Bold, TextAnchor.MiddleCenter, theme.Accent);
            title.text = PortFacilityArtCatalog.RegionalFacilityTitle(index, session.CurrentPort, PortFacilityNames[index]);
            title.raycastTarget = false;
            int nextIndex = (index + 1) % PortFacilityNames.Length;
            string nextTitle = PortFacilityArtCatalog.RegionalFacilityTitle(nextIndex, session.CurrentPort, PortFacilityNames[nextIndex]);
            Button next = CreatePortHeaderButton("Next Port Facility", header, new Vector2(166f, 58f),
                new Vector2(-176f, 0f), nextTitle + " →", 20, theme.Accent);
            next.onClick.AddListener(() =>
            {
                ResetCardWorkshopView();
                ResetComponentShopView();
                ResetDispatchView();
                investmentSupplyPanelOpen = false;
                activePortFacility = (activePortFacility + 1) % PortFacilityNames.Length;
                Render();
            });
            BuildPortHeaderReadouts(header, theme, false);

        }


        private void BuildTavernFacility()
        {
            if (selectedTavernCandidate >= 0)
            {
                BuildTavernCrewDetail(selectedTavernCandidate);
                return;
            }

            ResetTavernDossierView();
            RectTransform stage = CreateFixedPanel("Tavern Crew Stage", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f), Color.clear);
            stage.GetComponent<Image>().raycastTarget = false;

            // The six crew members are painted into the room so furniture, shadows,
            // scale and foreground occlusion feel like one scene. These transparent
            // hotspots retain per-character hover and click interaction.
            Vector2[] hotspotPositions =
            {
                new Vector2(-790f, 110f), // Evelyn: left bar stool
                new Vector2(-585f, 105f), // Barlow: adjacent bar stool
                new Vector2(-365f, -15f), // Moer: central table, left
                new Vector2(-120f, 20f),  // Leisa: central table, rear-left
                new Vector2(135f, 20f),   // Pepe: central table, rear-right
                new Vector2(405f, -5f),   // Noya: central table, right
            };
            Vector2[] hotspotSizes =
            {
                new Vector2(175f, 330f),
                new Vector2(190f, 330f),
                new Vector2(215f, 420f),
                new Vector2(205f, 390f),
                new Vector2(205f, 390f),
                new Vector2(240f, 420f),
            };

            // Regional paintings retain the same two-stool/four-chair layout,
            // but people are not at identical pixels. Use measured art bounds.
            if (facilityArt.RegionalBackground(PortFacilityArtCatalog.TavernFacilityIndex, session.CurrentPort) != null)
            {
                for (int index = 0; index < TavernCrewSlotCount; index += 1)
                {
                    hotspotPositions[index] = TavernCrewLayoutCatalog.StageCenter(session.CurrentPort, index);
                    hotspotSizes[index] = TavernCrewLayoutCatalog.StageSize(session.CurrentPort, index);
                }
            }

            for (int index = 0; index < TavernCrewSlotCount; index += 1)
            {
                int captured = index;
                CrewVariantDefinition crew = CurrentTavernCrew(index);
                CrewVariantDefinition capturedCrew = crew;
                bool recruited = IsCrewRecruitedForView(crew);
                bool awayHere = HasRecruitedCrewAwayFromCurrentPort(index);
                bool availableHere = !awayHere && (!recruited || IsCrewAvailableAtCurrentPort(crew));
                Button character = CreateButton(
                    "Tavern Crew " + index,
                    stage,
                    hotspotSizes[index],
                    hotspotPositions[index],
                    new Color(1f, 1f, 1f, 0f),
                    Ink,
                    16);
                character.GetComponentInChildren<Text>().text = string.Empty;
                Image hotspotGlow = character.targetGraphic as Image;
                if (hotspotGlow != null)
                {
                    hotspotGlow.sprite = circleSprite;
                    hotspotGlow.type = Image.Type.Simple;
                }

                Text name = CreateText(
                    "Tavern Crew Name " + index,
                    character.transform,
                    new Vector2(220f, 54f * GameReadability.Scale),
                    new Vector2(0f, hotspotSizes[index].y * 0.5f + 12f),
                    GameReadability.FontSize(32),
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ink);
                name.text = crew.DisplayName;
                GameReadability.Body(name, 32);
                // Crew names are positioned above the painted character bounds;
                // the visible label must remain part of the character's click
                // target rather than leaving a dead strip above the hotspot.
                name.raycastTarget = true;
                AddSceneLabelOutline(name);
                UiLabelBacking.Apply(name);
                Text status = CreateText(
                    "Tavern Crew Status " + index,
                    character.transform,
                    new Vector2(220f, 32f * GameReadability.Scale),
                    new Vector2(0f, hotspotSizes[index].y * 0.5f + 18f + 43f * GameReadability.Scale),
                    GameReadability.FontSize(18),
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    !recruited ? Gold : availableHere ? Green : MutedInk);
                status.text = !recruited && !awayHere ? "查看档案" : availableHere ? "✓ 已入队" : "人在其他港口";
                GameReadability.Body(status, 18);
                if (GameReadability.Level > 0)
                {
                    float labelX = -725f + index * 290f;
                    name.rectTransform.sizeDelta = new Vector2(274f, 80f);
                    name.rectTransform.anchoredPosition = new Vector2(labelX, -228f) - hotspotPositions[index];
                    status.rectTransform.sizeDelta = new Vector2(274f, 48f);
                    status.rectTransform.anchoredPosition = new Vector2(labelX, -301f) - hotspotPositions[index];
                }
                status.raycastTarget = true;
                AddSceneLabelOutline(status);
                UiLabelBacking.Apply(status, true);
                ConfigurePortHotspotHover(character, hotspotGlow, name);
                character.interactable = availableHere;
                if ((!recruited && awayHere) || (recruited && !availableHere))
                {
                    Image unavailableShade = CreateImage("Tavern Crew Away Shade " + index, character.transform, hotspotSizes[index], Vector2.zero, new Color(0.02f, 0.03f, 0.04f, 0.62f));
                    unavailableShade.raycastTarget = false;
                }
                character.onClick.AddListener(() =>
                {
                    if (!availableHere) return;
                    ResetTavernDossierView();
                    selectedTavernCandidate = captured;
                    portNotice = "正在查看「" + capturedCrew.DisplayName + "」的招募档案。";
                    Render();
                });
            }
        }

        private void RecruitTavernCrew(int index, CrewVariantDefinition crew)
        {
            if (!Application.isPlaying || editorPreviewBuilding) return;
            int safe = Mathf.Clamp(index, 0, TavernCrewSlotCount - 1);
            if (crew == null || IsCrewRecruited(crew))
            {
                return;
            }

            int cost = crew.ContractCost;
            if (!session.TrySpendProfileGold(cost, "酒馆船员契约"))
            {
                portNotice = "招募失败：金币不足。";
                Render();
                return;
            }

            MigrateLegacyCrewIdentities();
            PlayerPrefs.SetInt(CrewVariantRecruitmentKey(crew.Id), 1);
            PlayerPrefs.SetInt(CrewRecruitmentKey(safe), 1);
            PlayerPrefs.SetString(ActiveCrewVariantKey(safe), crew.Id);
            MarkCrewAtCurrentPortOnRecruitment(crew);
            PlaySfx("tavern_recruit", 0.62f);
            RefreshCrewCapabilities();
            SaveProgress();
            portNotice = "已从" + crew.PortName + "招募「" + crew.DisplayName + "」，职业能力已启用。";
            Render();
        }

        private void BuildComponentShopFacility()
        {
            RectTransform panel = CreateFixedPanel("Component Shop Panel", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f), Color.clear);
            if (componentResaleOpen)
            {
                BuildComponentResale(panel);
                return;
            }
            DateTime now = MarketNow;
            int refreshRevision = Application.isPlaying && !editorPreviewBuilding ? ComponentShopRefreshRevision(session.CurrentPort) : 0;
            string refreshScope = Application.isPlaying && !editorPreviewBuilding ? ComponentShopRefreshScope() : string.Empty;
            // Editor previews can be interacted with after the initial build,
            // when editorPreviewBuilding is already false. Keep those previews
            // on the legacy deterministic batch so clicking an offer can reopen
            // its detail sheet; only a live player session uses the expanded
            // 150-item market and its separate purchase namespace.
            bool liveShop = Application.isPlaying && !editorPreviewBuilding;
            IReadOnlyList<PortComponentShopStockItem> stock = liveShop
                ? PortComponentShopCatalog.BuildExpandedStock(componentArt, session.CurrentPort, now, refreshRevision, refreshScope,
                    PortInvestmentRarityShift(session.CurrentPort), PortInvestmentLevel(session.CurrentPort))
                : PortComponentShopCatalog.BuildStock(componentArt, session.CurrentPort, now, refreshRevision, refreshScope,
                    PortInvestmentRarityShift(session.CurrentPort), PortInvestmentLevel(session.CurrentPort));
            PortComponentShopStockItem selectedOffer = null;
            int soldCount = 0;
            for (int index = 0; index < stock.Count; index += 1)
            {
                if (PlayerPrefs.GetInt(ComponentShopPurchaseKey(stock[index].Id), 0) != 0)
                {
                    soldCount += 1;
                }

                if (string.Equals(stock[index].Id, selectedComponentShopOfferId, StringComparison.Ordinal))
                {
                    selectedOffer = stock[index];
                }
            }

            if (selectedOffer != null)
            {
                BuildComponentShopDetail(panel, selectedOffer);
                return;
            }

            selectedComponentShopOfferId = null;
            BuildComponentShopShowroom(panel, stock, now, soldCount);
        }

        private void BuildComponentShopShowroom(
            RectTransform panel,
            IReadOnlyList<PortComponentShopStockItem> stock,
            DateTime now,
            int soldCount)
        {
            BuildComponentResaleEntrance(panel);
            // The authored UIPreviewsForMe composition leaves the showroom clear
            // above the two offers. Keep only the rule text, moved below the
            // display row where it sits in the preview scene.
            Text marketRule = CreateText("Component Shop Regional Rule", panel,
                new Vector2(1725.2849f, 78.0589f), new Vector2(-28f, -394f), 30,
                FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            marketRule.text = "本港主售 " + PortComponentShopCatalog.PreferredSlotLabel(session.CurrentPort) +
                "　·　新增高阶货稀有及以上 " + (PortInvestmentCatalog.HighQualityChance(PortInvestmentLevel(session.CurrentPort)) * 100f).ToString("0.0") +
                "%　·　每游戏月刷新（下次 " + PortComponentShopCatalog.NextRefreshLabel(now) + "）";
            AddSceneLabelOutline(marketRule);

            int pageCount = ComponentShopShowroomLayout.PageCount(stock.Count);
            componentShopDisplayPage = Mathf.Clamp(componentShopDisplayPage, 0, pageCount - 1);
            int visibleCount = ComponentShopShowroomLayout.VisibleCount(stock.Count, componentShopDisplayPage);
            int firstOffer = componentShopDisplayPage * ComponentShopShowroomLayout.PageSize;
            if (pageCount > 1)
            {
                Button previous = CreateButton("Component Shop Previous Display", panel, new Vector2(138f, 32f), new Vector2(-225f, 207f), ScenePlaqueColor(0.86f), Ink, 15);
                previous.GetComponentInChildren<Text>().text = "← 上一组";
                previous.interactable = componentShopDisplayPage > 0;
                previous.onClick.AddListener(() =>
                {
                    componentShopDisplayPage--;
                    ClearChildren(panel);
                    BuildComponentShopShowroom(panel, stock, now, soldCount);
                });
                Text page = CreateText("Component Shop Display Page", panel, new Vector2(280f, 32f), new Vector2(0f, 207f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
                page.text = "陈列 " + (firstOffer + 1) + "–" + (firstOffer + visibleCount) + " / " + stock.Count + " 件";
                AddSceneLabelOutline(page);
                Button next = CreateButton("Component Shop Next Display", panel, new Vector2(138f, 32f), new Vector2(225f, 207f), ScenePlaqueColor(0.86f), Ink, 15);
                next.GetComponentInChildren<Text>().text = "下一组 →";
                next.interactable = componentShopDisplayPage + 1 < pageCount;
                next.onClick.AddListener(() =>
                {
                    componentShopDisplayPage++;
                    ClearChildren(panel);
                    BuildComponentShopShowroom(panel, stock, now, soldCount);
                });
            }
            for (int visibleIndex = 0; visibleIndex < visibleCount; visibleIndex += 1)
            {
                int index = firstOffer + visibleIndex;
                PortComponentShopStockItem offer = stock[index];
                PortComponentShopStockItem captured = offer;
                ComponentArtEntry entry = offer.Entry;
                bool mysteryOffer = offer.Id.StartsWith("mystery|", StringComparison.Ordinal);
                bool selected = EquippedComponent(entry.Slot)?.Id == entry.Id;
                bool sold = PlayerPrefs.GetInt(ComponentShopPurchaseKey(offer.Id), 0) != 0;
                bool affordable = session.ProfileGold >= offer.Price;
                Color rarityColor = ComponentArtCatalog.RarityColor(entry.Rarity);
                ComponentShopShowroomLayout.Spot display = ComponentShopShowroomLayout.GetDisplay(visibleCount, visibleIndex);
                Button card = CreateButton("Component Shop Offer " + offer.Id, panel, new Vector2(350f, 400f), display.Position, Color.clear, Ink, 16);
                Image cardHitArea = card.targetGraphic as Image;
                if (cardHitArea != null)
                {
                    // Mystery offers are display-only surfaces. Keep the button
                    // as the click owner, but remove any source image/texture
                    // from the offer root so it cannot add a hidden card plate.
                    if (mysteryOffer)
                    {
                        cardHitArea.sprite = null;
                        cardHitArea.type = Image.Type.Simple;
                    }
                    cardHitArea.color = Color.clear;
                    // The card is the complete interaction surface. The model
                    // and plaque still bubble pointer events to this Button,
                    // while the transparent root also makes the empty spacing
                    // between them clickable instead of leaving dead zones.
                    cardHitArea.raycastTarget = true;
                }
                card.GetComponentInChildren<Text>().text = string.Empty;

                RectTransform assembly = CreateRect("Component Shop Display Assembly " + entry.Id, card.transform, new Vector2(380f, 440f), Vector2.zero);
                // Keep each unobstructed component and its separate sign together.
                // Larger inventories use another group instead of covering the walls.
                assembly.localScale = Vector3.one * display.Scale;
                BuildComponentShopDisplay(assembly, entry, "Component Shop ", " " + entry.Id,
                    sold, selected, true, true, mysteryOffer, out RawImage aura, out RawImage model, out RawImage plaque);
                Rect safeText = ComponentShopDisplayGeometry.For(entry, session.CurrentPort).WritingArea;
                // UIPreviewsForMe gives every offer the same enlarged writing bay,
                // title field and status field as the left reference component.
                Vector2 writingAreaSize = new Vector2(300f, 100f);
                RectTransform plaqueText = CreateRect("Component Shop Plaque Writing Area " + entry.Id, plaque.transform, writingAreaSize, safeText.center);
                plaqueText.gameObject.AddComponent<RectMask2D>();
                Vector2 nameSize = new Vector2(300f, 50f);
                Vector2 statusSize = new Vector2(200f, 40f);
                Vector2 statusPosition = new Vector2(0f, -35f);
                int nameFontSize = 30;
                int statusFontSize = 20;
                Text name = CreateText("Component Shop Plaque Name " + entry.Id, plaqueText, nameSize, new Vector2(0f, 10f), nameFontSize, FontStyle.Normal, TextAnchor.MiddleCenter, sold ? MutedInk : Ink);
                name.text = entry.Name;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = nameFontSize;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.raycastTarget = false;
                UiLabelBacking.Apply(name, true);
                Text action = CreateText("Component Shop Plaque Status " + entry.Id, plaqueText, statusSize, statusPosition, statusFontSize, FontStyle.Normal, TextAnchor.MiddleCenter, sold ? MutedInk : Ink);
                action.text = sold
                    ? "本月已售出"
                    : selected
                        ? offer.Price + " 金币 · 已装同款 · 可再购"
                        : offer.Price + " 金币 · " + (affordable ? "查看详情" : "资金不足");
                action.resizeTextForBestFit = true;
                action.resizeTextMinSize = 14;
                action.resizeTextMaxSize = statusFontSize;
                action.horizontalOverflow = HorizontalWrapMode.Wrap;
                action.verticalOverflow = VerticalWrapMode.Truncate;
                action.raycastTarget = false;
                UiLabelBacking.Apply(action, true);
                Text suitability = CreateText("Component Shop Offer Suitability " + entry.Id, card.transform,
                    new Vector2(310f, 34f), new Vector2(0f, 174f), 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                    sold ? MutedInk : rarityColor);
                string[] environmentLines = ComponentArtCatalog.EnvironmentSummary(entry).Split('\n');
                suitability.text = environmentLines.Length == 0 ? string.Empty : environmentLines[0];
                suitability.resizeTextForBestFit = true;
                suitability.resizeTextMinSize = 14;
                suitability.resizeTextMaxSize = 16;
                suitability.horizontalOverflow = HorizontalWrapMode.Wrap;
                suitability.verticalOverflow = VerticalWrapMode.Truncate;
                suitability.raycastTarget = false;
                UiLabelBacking.Apply(suitability);
                ConfigureComponentShopHover(card, aura, model, plaque);
                card.interactable = !sold;
                card.onClick.AddListener(() =>
                {
                    selectedComponentShopOfferId = captured.Id;
                    portNotice = "正在查看「" + captured.Entry.Name + "」的部件档案。";
                    Render();
                });
            }
            ApplyComponentShopReadingLayout(panel);
        }

        private void BuildComponentShopDetail(RectTransform panel, PortComponentShopStockItem offer)
        {
            BuildComponentDossier(panel, offer);
        }

        private void PurchaseComponentOffer(PortComponentShopStockItem offer)
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null ||
                session.Phase != CoreDemoPhase.Port || !CanRefitShip(session.SelectedShipIndex)) return;
            // The player may leave a detail sheet open across a month boundary
            // or move to another port before an old callback is invoked.
            // Check ownership before any currency or purchase-state mutation.
            if (!PortComponentShopCatalog.IsCurrentOffer(offer, session.CurrentPort, MarketNow,
                ComponentShopRefreshRevision(session.CurrentPort), ComponentShopRefreshScope(), PortInvestmentLevel(session.CurrentPort)))
            {
                selectedComponentShopOfferId = null;
                portNotice = "港口、日期或批次已变，报价已刷新；未扣款。";
                Render();
                return;
            }

            if (offer == null || PlayerPrefs.GetInt(ComponentShopPurchaseKey(offer.Id), 0) != 0)
            {
                Render();
                return;
            }

            if (!session.TrySpendProfileGold(offer.Price, session.CurrentPort + "部件采购"))
            {
                portNotice = "购买失败：金币不足，还需 " + PortMarketCatalog.FormatPrice(Math.Max(0, offer.Price - session.ProfileGold)) + " 金币。";
                Render();
                return;
            }

            PlayerPrefs.SetInt(ComponentShopPurchaseKey(offer.Id), 1);
            PlayerPrefs.SetInt(ComponentPurchaseAchievementKey, 1);
            PlaySfx("shipyard_equip", 0.56f);
            EquipComponentWithQuality(offer.Entry, offer.Quality);
            SaveProgress();
            portNotice = "已在" + session.CurrentPort + "购入「" + offer.Entry.Name + "」，金币 -" +
                offer.Price + "；已装配至" + ComponentArtCatalog.SlotName(offer.Entry.Slot) + "，原部件已移回库存。";
            Render();
        }

        private static void ConfigureComponentShopHover(Button offer, Graphic aura, RawImage model, RawImage plaque)
        {
            if (offer == null || aura == null || model == null || plaque == null)
            {
                return;
            }

            offer.transition = Selectable.Transition.None;
            Color restingAura = aura.color;
            Color restingModel = model.color;
            Color restingPlaque = plaque.color;
            EventTrigger trigger = offer.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = offer.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();

            EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ =>
            {
                // Keep the showroom art at its authored brightness. A rarity
                // aura and a second brightened model made the offer look like a
                // yellow spotlight when the pointer crossed it.
                aura.color = restingAura;
                model.color = restingModel;
                plaque.color = restingPlaque;
            });
            trigger.triggers.Add(pointerEnter);

            EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ =>
            {
                aura.color = restingAura;
                model.color = restingModel;
                plaque.color = restingPlaque;
            });
            trigger.triggers.Add(pointerExit);
        }

        public static string ComponentShopPurchaseKey(string offerId)
        {
            return ComponentShopPurchaseKeyPrefix + WorldEconomyScope + "." + offerId;
        }

        private void BuildCardWorkshopFacility()
        {
            if (specialCardCollectionOpen)
            {
                BuildSpecialCardCollection();
                return;
            }
            if (cardWorkshopDeckBuilderOpen)
            {
                BuildDeckBuilderFacility();
                return;
            }

            BuildCardWorkshopChestStage();
        }

        private void BuildCardWorkshopChestStage()
        {
            RectTransform stage = CreateFixedPanel("Card Workshop Chest Stage", contentRoot,
                new Vector2(1820f, 650f), new Vector2(-8f, -181f), Color.clear);

            Color[] accents = { Gold, Coral, Teal, Html("#9A72D8") };
            int chestCount = Mathf.Min(CardWorkshopChestNames.Length, cardWorkshopArt == null ? 0 : cardWorkshopArt.ChestCount);
            // The inner pair sits farther back; draw the foreground pair last. Every contact
            // is anchored to the rug independently of the button and its hover feedback.
            int[] depthOrder = { 1, 2, 0, 3 };
            foreach (int index in depthOrder)
            {
                if (index >= chestCount) continue;
                int captured = index;
                Texture2D texture = cardWorkshopArt.Chest(index);
                float aspect = texture == null || texture.height <= 0 ? 1.25f : texture.width / (float)texture.height;
                Vector2 artSize = CardWorkshopChestPlacement.ArtSize(index, aspect);
                Vector2 paintedSize = CardWorkshopChestPlacement.PaintedSize(index, artSize);
                Vector2 floor = CardWorkshopChestPlacement.FloorPosition(index);
                float contactY = CardWorkshopChestPlacement.ContactOffset(index, artSize);
                float footprint = CardWorkshopChestPlacement.FootprintWidth(index, artSize);
                CreateSurfaceContact("Card Workshop Chest Contact " + index, stage, floor, new Vector2(footprint + 36f, 26f));
                // Small dense shadows beneath both painted feet close the light gap that a
                // single ellipse centred on the transparent image rectangle leaves behind.
                for (int foot = 0; foot < 2; foot++)
                {
                    Vector2 uv = CardWorkshopChestPlacement.FootUv(index, foot == 1);
                    Image contact = CreateImage("Card Workshop Chest Foot " + index + " " + foot, stage,
                        new Vector2(36f, 8f), floor + new Vector2((uv.x - 0.5f) * artSize.x, 0f),
                        new Color(0.015f, 0.009f, 0.006f, 0.65f), circleSprite);
                    contact.raycastTarget = false;
                }
                Button open = CreateButton("Open Deck Chest " + index, stage, new Vector2(315f, artSize.y + 90f),
                    CardWorkshopChestPlacement.ButtonPosition(index, artSize), Color.clear, Ink, 16);
                open.GetComponentInChildren<Text>().text = string.Empty;
                Image glow = CreateImage("Card Workshop Chest Glow " + index, open.transform,
                    new Vector2(paintedSize.x, paintedSize.y * 0.78f), CardWorkshopChestPlacement.ArtPosition, Color.clear, circleSprite);
                glow.raycastTarget = false;
                RawImage chest = CreateRawImage("Card Workshop Chest " + index, open.transform, artSize,
                    CardWorkshopChestPlacement.ArtPosition, texture, new Rect(0f, 0f, 1f, 1f));
                chest.raycastTarget = false;
                ConfigureSceneItemButton(open, chest, false, accents[index]);
                Text name = CreateText("Card Workshop Chest Name " + index, open.transform, new Vector2(330f, 48f * GameReadability.Scale), new Vector2(0f, contactY - 36f), GameReadability.FontSize(32), FontStyle.Bold, TextAnchor.MiddleCenter, accents[index]);
                name.text = CardWorkshopChestNames[index];
                GameReadability.Body(name, 32);
                AddSceneLabelOutline(name);
                UiLabelBacking.Apply(name, true);
                ConfigureGroundedObjectHover(open, glow, chest, name, accents[index]);
                open.onClick.AddListener(() =>
                {
                    selectedCardWorkshopChest = captured;
                    cardWorkshopDeckBuilderOpen = captured < 3;
                    battleCardUpgradeOpen = captured < 3;
                    specialCardCollectionOpen = captured == 3;
                    specialCardCollectionPage = 0;
                    selectedSpecialCardIndex = -1;
                    battleDeckOverviewOpen = false;
                    selectedBattleDeckStage = captured == 1 ? NavalBattleStage.Naval : captured == 2 ? NavalBattleStage.Boarding : NavalBattleStage.Preparation;
                    battleCardUpgradePage = 0;
                    selectedBattleCardUpgrade = -1;
                    battleCardCollectionPage = 0;
                    portNotice = captured == 3 ? "特殊牌只从对应来源的实际出海航程获得经验，不接受金币强化。" : "已打开「" + CardWorkshopChestNames[captured] + "」，仅强化基础战术。";
                    Render();
                });
            }

            Button decks = CreateButton("Workshop Configure Three Decks", stage, new Vector2(360f, 48f * GameReadability.Scale), new Vector2(0f, -258f), PanelRaised, Ink, GameReadability.FontSize(22));
            decks.GetComponentInChildren<Text>().text = "编组出航三套牌组";
            GameReadability.Body(decks.GetComponentInChildren<Text>(), 22);
            decks.onClick.AddListener(() => { cardWorkshopDeckBuilderOpen = true; battleCardUpgradeOpen = false; battleDeckOverviewOpen = true; specialCardCollectionOpen = false; Render(); });
        }

        private void ResetCardWorkshopView()
        {
            specialCardCollectionOpen = false;
            specialCardCollectionPage = 0;
            specialCardSourceFilter = 0;
            selectedSpecialCardIndex = -1;
            battleCardUpgradeOpen = false;
            cardWorkshopDeckBuilderOpen = false;
            selectedCardWorkshopChest = -1;
            battleDeckOverviewOpen = false;
            selectedBattleDeckStage = NavalBattleStage.Preparation;
            battleCardCollectionPage = 0;
        }

        private void ResetComponentShopView()
        {
            selectedComponentShopOfferId = null;
            componentShopDisplayPage = 0;
            componentResaleOpen = false;
            componentResalePage = 0;
            pendingComponentSaleId = null;
        }

        private void ResetDispatchView()
        {
            selectedDispatchMission = -1;
            selectedDispatchDurationHours = 0;
            selectedDispatchCrew.Clear();
        }

        private void BuildDispatchFacility()
        {
            RectTransform panel = CreateFixedPanel("Dispatch Panel", contentRoot, new Vector2(1820f, 650f), new Vector2(-13f, -133f), Color.clear);
            int activeMission = Application.isPlaying ? PlayerPrefs.GetInt(DispatchMissionKey, -1) : -1;
            if (activeMission >= 0)
            {
                BuildActiveDispatch(panel, activeMission);
                return;
            }

            if (selectedDispatchMission >= 0 && selectedDispatchMission < DispatchNames.Length)
            {
                BuildDispatchDeployment(panel, selectedDispatchMission);
                return;
            }

            BuildDispatchTableStage(panel);
        }

        private void BuildDispatchTableStage(RectTransform panel)
        {
            RectTransform stage = CreateFixedPanel("Dispatch Table Stage", panel, new Vector2(1820f, 650f), Vector2.zero, Color.clear);
            // Furniture, floor shadow and miniature ships now share a single
            // painted room layer. Only the three interactive papers sit above it.

            Text heading = CreateText("Dispatch Table Heading", stage, new Vector2(1320f, 42f), new Vector2(-8f, 408f), 30, FontStyle.Normal, TextAnchor.MiddleCenter, Gold);
            heading.text = "寻找第七港口 · 选择委托海图";
            AddSceneLabelOutline(heading);

            Color[] accents = { Html("#50B7AE"), Gold, Html("#7D8FD1") };
            for (int index = 0; index < DispatchNames.Length; index += 1)
            {
                int captured = index;
                Rect chartBounds = DispatchTabletopLayout.ChartBounds(index);
                Button chartButton = CreateButton("Dispatch Mission Chart " + index, stage, chartBounds.size, chartBounds.center, Color.clear, Ink, 16);
                chartButton.GetComponentInChildren<Text>().text = string.Empty;
                chartButton.GetComponent<Image>().raycastTarget = false;
                Texture2D chartTexture = dispatchArt == null ? art.RouteMap(index) : dispatchArt.MissionChart(index);
                // The shadow follows the paper silhouette and the same wooden plane, not a floating oval.
                RawImage contact = CreateRawImage("Dispatch Chart Paper Contact " + index, chartButton.transform,
                    chartBounds.size, new Vector2(0f, -2f), chartTexture, new Rect(0f, 0f, 1f, 1f));
                contact.color = new Color(0.035f, 0.020f, 0.009f, 0.65f);
                contact.raycastTarget = false;
                DispatchTabletopLayout.ConfigureProjection(contact.gameObject.AddComponent<TabletopQuadProjection>(), index);
                Image glow = CreateImage("Dispatch Mission Chart Glow " + index, chartButton.transform, chartBounds.size, Vector2.zero, Color.clear, circleSprite);
                glow.raycastTarget = false;
                DispatchTabletopLayout.ConfigureProjection(glow.gameObject.AddComponent<TabletopQuadProjection>(), index);
                RawImage chart = CreateRawImage("Dispatch Mission Chart Art " + index, chartButton.transform, chartBounds.size, Vector2.zero, chartTexture, new Rect(0f, 0f, 1f, 1f));
                DispatchTabletopLayout.ConfigureProjection(chart.gameObject.AddComponent<TabletopQuadProjection>(), index);
                chart.raycastTarget = true;
                // Readable labels below the table apron stay separate from the foreshortened paper.
                Vector2[] authoredLabelPositions =
                {
                    new Vector2(13.5f, 55.435f),
                    new Vector2(27f, 59.061f),
                    new Vector2(25.755f, 58.688f),
                };
                Text title = CreateText("Dispatch Mission Name " + index, chartButton.transform, new Vector2(290f, 32f), authoredLabelPositions[index], 17, FontStyle.Normal, TextAnchor.MiddleCenter, accents[index]);
                title.text = DispatchNames[index];
                // The readable caption is authored below the foreshortened
                // paper, outside the chart's projected rectangle. Let its
                // pointer event bubble to the same chart Button so the visible
                // label does not become a dead click strip.
                title.raycastTarget = true;
                AddSceneLabelOutline(title);
                ConfigureDispatchChartHover(chartButton, glow, chart, accents[index]);
                chartButton.onClick.AddListener(() =>
                {
                    selectedDispatchMission = captured;
                    selectedDispatchDurationHours = DispatchDurations[captured];
                    selectedDispatchCrew.Clear();
                    portNotice = "正在部署「" + DispatchNames[captured] + "」；确认前不会扣除金币。";
                    Render();
                });
            }

        }

        private void BuildDispatchDeployment(RectTransform panel, int missionIndex)
        {
            int safe = Mathf.Clamp(missionIndex, 0, DispatchNames.Length - 1);
            int minimumHours = DispatchDurations[safe];
            selectedDispatchDurationHours = Mathf.Clamp(
                selectedDispatchDurationHours <= 0 ? minimumHours : selectedDispatchDurationHours,
                minimumHours,
                minimumHours * 2);

            RectTransform deployment = CreateFixedPanel("Dispatch Deployment Panel", panel, new Vector2(1820f, 650f), Vector2.zero, Color.clear);
            RawImage parchment = CreateRawImage(
                "Dispatch Deployment Parchment",
                deployment,
                new Vector2(1700f, 630f),
                Vector2.zero,
                cardWorkshopArt == null ? null : cardWorkshopArt.CollectionParchment,
                new Rect(0f, 0f, 1f, 1f));
            parchment.raycastTarget = false;
            RawImage frame = CreateRawImage(
                "Dispatch Deployment Frame " + safe,
                deployment,
                new Vector2(1760f, 650f),
                Vector2.zero,
                dispatchArt == null ? null : dispatchArt.MissionFrame(safe),
                new Rect(0f, 0f, 1f, 1f));
            frame.raycastTarget = false;

            Color parchmentInk = Html("#302116");
            Color parchmentMuted = Html("#71583A");
            Color missionAccent = safe == 0 ? Html("#276E68") : safe == 1 ? Html("#8A531F") : Html("#4B568D");
            Text title = CreateText("Dispatch Deployment Title", deployment, new Vector2(620f, 48f), new Vector2(-450f, 206f), 27, FontStyle.Normal, TextAnchor.MiddleCenter, parchmentInk);
            title.text = DispatchNames[safe] + " · 派遣部署";
            Text missionRule = CreateText("Dispatch Deployment Mission Rule", deployment, new Vector2(570f, 62f), new Vector2(-450f, 153f), 20, FontStyle.Normal, TextAnchor.UpperCenter, missionAccent);
            missionRule.text = DispatchDurations[safe] + " 现实小时 · " + DispatchRewardSummary(safe, DispatchDurations[safe], null) +
                "\n" + DispatchCrewHints[safe] + " · 离线继续计时\n不推进游戏日历 · 派遣记录补齐第七港口航图";

            Texture2D chartTexture = dispatchArt == null ? art.RouteMap(safe) : dispatchArt.MissionChart(safe);
            RawImage chart = CreateRawImage("Dispatch Deployment Chart", deployment, new Vector2(500f, 300f), new Vector2(-450f, -28f), chartTexture, new Rect(0f, 0f, 1f, 1f));
            UiImageAspect.Fit(chart, new Vector2(500f, 300f));
            chart.raycastTarget = false;
            Outline chartOutline = chart.gameObject.AddComponent<Outline>();
            chartOutline.effectColor = missionAccent;
            chartOutline.effectDistance = new Vector2(3f, -3f);
            // Keep the heading and the selected summary in separate columns.  The old
            // 760px heading extended into the summary label (and into the third crew
            // card), making the two strings render on top of each other at runtime.
            Text crewHeading = CreateText("Dispatch Crew Heading", deployment, new Vector2(430f, 34f), new Vector2(90f, 205f), 19, FontStyle.Normal, TextAnchor.MiddleLeft, parchmentInk);
            crewHeading.text = "特殊船员领队　·　最多选择 2 名";
            Vector2[] crewPositions =
            {
                new Vector2(90f, 126f), new Vector2(350f, 126f), new Vector2(610f, 126f),
                new Vector2(90f, 20f), new Vector2(350f, 20f), new Vector2(610f, 20f),
            };
            for (int index = 0; index < TavernCrewSlotCount; index += 1)
            {
                int captured = index;
                CrewVariantDefinition rosterCrew = ActiveCrewForProfession(index);
                bool recruited = !Application.isPlaying || (IsCrewRecruited(index) && IsCrewAvailableAtCurrentPort(rosterCrew));
                bool selected = selectedDispatchCrew.Contains(index);
                Color buttonColor = selected
                    ? new Color(missionAccent.r, missionAccent.g, missionAccent.b, 0.9f)
                    : recruited ? new Color(0.38f, 0.27f, 0.15f, 0.42f) : new Color(0.28f, 0.25f, 0.21f, 0.28f);
                Button crew = CreateButton("Dispatch Crew Toggle " + index, deployment, new Vector2(242f, 96f), crewPositions[index], buttonColor, selected ? Color.white : parchmentInk, 14);
                crew.GetComponentInChildren<Text>().text = string.Empty;
                crew.interactable = recruited;
                RawImage portrait = CreateRawImage("Dispatch Crew Portrait " + index, crew.transform, new Vector2(74f, 68.71429f), new Vector2(-73f, 0f), portArt.CrewPortrait(rosterCrew), portArt.CrewPortraitUv(rosterCrew, true));
                portrait.raycastTarget = false;
                FitCrewPortrait(portrait, new Vector2(74f, 68.71429f));
                Text crewName = CreateText("Dispatch Crew Name " + index, crew.transform, new Vector2(145f, 42f), new Vector2(40f, 17f), 13, FontStyle.Normal, TextAnchor.MiddleLeft, selected ? Color.white : parchmentInk);
                crewName.text = rosterCrew.DisplayName;
                Text crewState = CreateText("Dispatch Crew State " + index, crew.transform, new Vector2(145f, 30f), new Vector2(40f, -23f), 11, FontStyle.Normal, TextAnchor.MiddleLeft, selected ? Color.white : parchmentMuted);
                crewState.text = !recruited ? "尚未招募" : selected ? "已加入派遣" : index == DispatchRecommendedCrew[safe] ? "任务推荐" : "可选择";
                crew.onClick.AddListener(() => ToggleDispatchCrew(captured));
                if (RecruitedCrewForProfession(index).Count > 1)
                {
                    Button cycle = CreateButton("Cycle Dispatch Crew Identity " + index, crew.transform,
                        new Vector2(31f, 28f), new Vector2(104f, -34f), missionAccent, Color.white, 16);
                    cycle.GetComponentInChildren<Text>().text = "›";
                    cycle.onClick.AddListener(() => CycleActiveCrewIdentity(captured));
                }
            }

            // Keep the selected summary in the authored right-hand bay while
            // leaving a clear gap after the 430 px crew heading column.
            Text selectedCrew = CreateText("Dispatch Selected Crew", deployment, new Vector2(300f, 34f), new Vector2(500f, 205f), 19, FontStyle.Normal, TextAnchor.MiddleLeft, missionAccent);
            selectedCrew.text = "已选 " + selectedDispatchCrew.Count + "/2　" + DispatchCrewNames(selectedDispatchCrew);
            selectedCrew.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedCrew.verticalOverflow = VerticalWrapMode.Truncate;
            selectedCrew.resizeTextForBestFit = true;
            selectedCrew.resizeTextMinSize = 14;
            selectedCrew.resizeTextMaxSize = 19;
            Text durationHeading = CreateText("Dispatch Duration Heading", deployment, new Vector2(83.8962f, 30f), new Vector2(11.948f, -62.3838f), 19, FontStyle.Normal, TextAnchor.MiddleLeft, parchmentInk);
            durationHeading.text = "派遣时长";
            Slider duration = CreateSlider("Dispatch Duration Slider", deployment, new Vector2(590f, 30f), new Vector2(285f, -98f), minimumHours, minimumHours * 2, selectedDispatchDurationHours);
            duration.wholeNumbers = true;
            DispatchDurationHelm.Apply(duration);
            Text durationValue = CreateText("Dispatch Duration Value", deployment, new Vector2(150f, 30f), new Vector2(650f, -98f), 16, FontStyle.Normal, TextAnchor.MiddleCenter, missionAccent);
            durationValue.text = selectedDispatchDurationHours + " 现实小时";
            Text reward = CreateText("Dispatch Reward Preview", deployment, new Vector2(760f, 28f), new Vector2(350f, -132f), 14, FontStyle.Normal, TextAnchor.UpperLeft, parchmentInk);
            reward.text = "预计收益　" + DispatchRewardSummary(safe, selectedDispatchDurationHours, selectedDispatchCrew);
            duration.onValueChanged.AddListener(value =>
            {
                selectedDispatchDurationHours = Mathf.RoundToInt(value);
                durationValue.text = selectedDispatchDurationHours + " 现实小时";
                reward.text = "预计收益　" + DispatchRewardSummary(safe, selectedDispatchDurationHours, selectedDispatchCrew);
            });

            // Keep actions on the shared inner paper area, clear of the bottom crest and
            // the larger salvage/escort ornaments at the lower-right corner.
            RectTransform actions = CreateRect("Dispatch Deployment Actions", deployment, new Vector2(500f, 44f), new Vector2(330f, -176f));
            Button back = CreateButton("Back To Dispatch Table", actions, new Vector2(220f, 44f), new Vector2(-140f, 0f), Html("#6D4B2C"), Ink, 15);
            back.GetComponentInChildren<Text>().text = "返回委托海图";
            back.onClick.AddListener(() =>
            {
                ResetDispatchView();
                portNotice = "已返回远航派遣桌。";
                Render();
            });
            Button confirm = CreateButton("Confirm Dispatch Deployment", actions, new Vector2(260f, 44f), new Vector2(120f, 0f), missionAccent, Color.white, 16);
            confirm.GetComponentInChildren<Text>().text = "确认派遣 · 无需投入";
            confirm.interactable = true;
            confirm.onClick.AddListener(() => StartDispatch(safe));
        }

        private void ToggleDispatchCrew(int crewIndex)
        {
            int safe = Mathf.Clamp(crewIndex, 0, TavernCrewSlotCount - 1);
            if (!IsCrewRecruited(safe) || !IsCrewAvailableAtCurrentPort(ActiveCrewForProfession(safe)))
            {
                portNotice = "该特殊船员尚未招募或人在其他港口，无法加入派遣。";
                return;
            }

            if (selectedDispatchCrew.Contains(safe))
            {
                selectedDispatchCrew.Remove(safe);
                portNotice = "已将「" + ActiveCrewForProfession(safe).DisplayName + "」移出派遣编组。";
                Render();
                return;
            }

            if (selectedDispatchCrew.Count >= 2)
            {
                portNotice = "每次派遣最多 2 名特殊船员。";
                Render();
                return;
            }

            selectedDispatchCrew.Add(safe);
            selectedDispatchCrew.Sort();
            portNotice = "已将「" + ActiveCrewForProfession(safe).DisplayName + "」加入派遣编组。";
            Render();
        }

        private void StartDispatch(int missionIndex)
        {
            if (session.Phase != CoreDemoPhase.Port || missionIndex < 0 || missionIndex >= DispatchNames.Length) return;
            int safe = Mathf.Clamp(missionIndex, 0, DispatchNames.Length - 1);
            if (PlayerPrefs.GetInt(DispatchMissionKey, -1) >= 0)
            {
                portNotice = "已有一支派遣队正在执行任务。";
                Render();
                return;
            }

            int durationHours = Mathf.Clamp(selectedDispatchDurationHours, DispatchDurations[safe], DispatchDurations[safe] * 2);
            List<int> crew = new List<int>();
            for (int index = 0; index < selectedDispatchCrew.Count && crew.Count < 2; index += 1)
            {
                int crewIndex = selectedDispatchCrew[index];
                if (crewIndex >= 0 && crewIndex < TavernCrewSlotCount && IsCrewRecruited(crewIndex) && IsCrewAvailableAtCurrentPort(ActiveCrewForProfession(crewIndex)) && !crew.Contains(crewIndex))
                {
                    crew.Add(crewIndex);
                }
            }


            PlayerPrefs.SetInt(DispatchDurationHoursKey, durationHours);
            PlayerPrefs.SetInt(DispatchBalanceVersionKey, GameplayBalance.Version);
            PlayerPrefs.SetString(DispatchCrewKey, BuildDispatchCrewCsv(crew));
            PlayerPrefs.SetString(DispatchCrewIdentityKey, BuildDispatchCrewIdentityCsv(crew));
            PlayerPrefs.SetString(DispatchEndsAtKey, DispatchDeadline(durationHours).ToString());
            PlayerPrefs.SetInt(DispatchRealClockKey, 1);
            PlayerPrefs.DeleteKey(DispatchGameClockKey);
            PlayerPrefs.SetInt(DispatchMissionKey, safe);
            if (Application.isPlaying && !editorPreviewBuilding) AchievementCatalog.RecordDispatchStarted(durationHours);
            RefreshCrewCapabilities();
            SaveProgress();
            string crewSummary = DispatchCrewNames(crew);
            ResetDispatchView();
            PlaySfx("dispatch_start", 0.56f);
            portNotice = "已派出「" + (string.IsNullOrEmpty(crewSummary) ? "普通水手编组" : crewSummary) + "」执行「" + DispatchNames[safe] + "」，按现实时间计时，离线继续；不推进游戏日历。";
            Render();
        }

        private void BuildActiveDispatch(RectTransform panel, int missionIndex)
        {
            int safe = Mathf.Clamp(missionIndex, 0, DispatchNames.Length - 1);
            bool validTimestamp = CoreEconomyBalance.IsValidDispatchRecord(missionIndex, PlayerPrefs.GetString(DispatchEndsAtKey, "0"), out long endsAt);
            long remainingTicks = validTimestamp ? Math.Max(0L, endsAt - DateTime.UtcNow.Ticks) : long.MaxValue;
            bool ready = validTimestamp && remainingTicks == 0L;
            int durationHours = Mathf.Clamp(PlayerPrefs.GetInt(DispatchDurationHoursKey, DispatchDurations[safe]), DispatchDurations[safe], DispatchDurations[safe] * 2);
            List<int> crew = ParseDispatchCrewCsv(PlayerPrefs.GetString(DispatchCrewKey, string.Empty));
            bool legacyRewards = PlayerPrefs.GetInt(DispatchBalanceVersionKey, 1) < GameplayBalance.Version;
            int rewardPercent = DispatchRewardPercent(safe, durationHours, crew, legacyRewards);

            RectTransform active = CreateFixedPanel("Active Dispatch Job", panel, new Vector2(1320f, 470f), Vector2.zero, ScenePlaqueColor(0.88f));
            Text badge = CreateText("Active Dispatch Badge", active, new Vector2(1160f, 34f), new Vector2(0f, 175f), 16, FontStyle.Bold, TextAnchor.MiddleCenter, ready ? Gold : Teal);
            badge.text = !validTimestamp ? "派遣记录异常 · 请撤回任务" : ready ? "任务完成 · 等待结算" : "船员派遣中 · 现实时间，离线继续";
            Text title = CreateText("Active Dispatch Title", active, new Vector2(1160f, 62f), new Vector2(0f, 118f), 31, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = DispatchNames[safe];
            string crewSummary = PersistedDispatchCrewNames(crew);
            Text status = CreateText("Active Dispatch Status", active, new Vector2(1100f, 175f), new Vector2(0f, 20f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            status.text = "计划时长 " + durationHours + " 现实小时　·　领队 " + (string.IsNullOrEmpty(crewSummary) ? "普通水手编组" : crewSummary) +
                          "\n预计收益 " + DispatchRewardSummary(safe, durationHours, crew, legacyRewards) + "　·　收益倍率 " + rewardPercent + "%\n离线继续计时；不推进游戏日历。\n" +
                          (!validTimestamp ? "派遣时间记录无法读取；撤回不会获得收益。" : ready ? "远航队已回港，可领取全部收益。" : "剩余约 " + FormatDispatchRemaining(remainingTicks));
            Button resolve = CreateButton(ready ? "Claim Dispatch Reward" : "Recall Dispatch Job", active, new Vector2(470f, 62f), new Vector2(0f, -145f), ready ? Gold : Coral, ready ? DeepSea : Ink, 18);
            resolve.GetComponentInChildren<Text>().text = ready ? "领取派遣收益" : validTimestamp ? "提前撤回 · 放弃收益" : "清除异常派遣记录";
            int capturedMission = missionIndex;
            resolve.onClick.AddListener(() => ResolveDispatch(capturedMission, ready));
            Text notice = CreateText("Active Dispatch Notice", active, new Vector2(1240f, 42f), new Vector2(0f, -205f),
                14, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            notice.text = portNotice;
            if (validTimestamp && !ready)
                StartCoroutine(RefreshDispatchDeadline(active, endsAt));
        }

        private void ResolveDispatch(int missionIndex, bool completed)
        {
            int activeMission = PlayerPrefs.GetInt(DispatchMissionKey, -1);
            if (session.Phase != CoreDemoPhase.Port || activeMission < 0 || activeMission != missionIndex)
            {
                return;
            }

            bool validRecord = CoreEconomyBalance.IsValidDispatchRecord(activeMission,
                PlayerPrefs.GetString(DispatchEndsAtKey, "0"), out long endsAt);
            if (!validRecord)
            {
                ClearDispatchPersistence();
                RefreshCrewCapabilities();
                ResetDispatchView();
                portNotice = "已清除异常派遣记录并释放船员，本次未发放收益；其他存档保持不变。";
                SaveProgress();
                Render();
                return;
            }

            int safe = activeMission;
            bool readyNow = DateTime.UtcNow.Ticks >= endsAt;
            // An open page can cross its deadline. Never discard an already
            // completed job because its old button was originally a recall.
            completed |= readyNow;

            if (completed)
            {
                if (!readyNow)
                {
                    portNotice = "派遣队尚未返港，暂时不能领取收益。";
                    Render();
                    return;
                }

                int durationHours = Mathf.Clamp(PlayerPrefs.GetInt(DispatchDurationHoursKey, DispatchDurations[safe]), DispatchDurations[safe], DispatchDurations[safe] * 2);
                List<int> crew = ParseDispatchCrewCsv(PlayerPrefs.GetString(DispatchCrewKey, string.Empty));
                bool legacyRewards = PlayerPrefs.GetInt(DispatchBalanceVersionKey, 1) < GameplayBalance.Version;
                int rewardPercent = DispatchRewardPercent(safe, durationHours, crew, legacyRewards);
                int[] rewards = DispatchRewards(safe, rewardPercent, legacyRewards);
                int gold = rewards[0], wood = rewards[1], iron = rewards[2], fame = rewards[3];
                if (!session.TryReceivePortRewards(gold, wood, iron, fame, DispatchNames[safe] + "收益", out string failure))
                {
                    portNotice = failure;
                    Render();
                    return;
                }
                if (Application.isPlaying && !editorPreviewBuilding) AchievementCatalog.RecordDispatchCompleted();
                portNotice = "派遣任务已完成，" + DispatchRewardSummary(safe, durationHours, crew, legacyRewards) + " 已存入仓库与船长账户。";
            }
            else
            {
                portNotice = "已提前撤回，未获任务收益。";
            }

            PlaySfx(completed ? "dispatch_complete" : "dispatch_recall", completed ? 0.58f : 0.42f);

            ClearDispatchPersistence();
            RefreshCrewCapabilities();
            ResetDispatchView();
            SaveProgress();
            Render();
        }

        private static void ClearDispatchPersistence()
        {
            PlayerPrefs.DeleteKey(DispatchRealClockKey);
            PlayerPrefs.DeleteKey(DispatchGameClockKey);
            PlayerPrefs.DeleteKey(DispatchMissionKey);
            PlayerPrefs.DeleteKey(DispatchEndsAtKey);
            PlayerPrefs.DeleteKey(DispatchDurationHoursKey);
            PlayerPrefs.DeleteKey(DispatchBalanceVersionKey);
            PlayerPrefs.DeleteKey(DispatchCrewKey);
            PlayerPrefs.DeleteKey(DispatchCrewIdentityKey);
        }

        private static int DispatchRewardPercent(int missionIndex, int durationHours, IReadOnlyList<int> crew, bool legacy = false)
        {
            if (!legacy) return GameplayBalance.DispatchPercent(missionIndex, durationHours, crew);
            int safe = Mathf.Clamp(missionIndex, 0, DispatchDurations.Length - 1);
            int baseline = DispatchDurations[safe];
            float extension = Mathf.Clamp01((durationHours - baseline) / (float)Mathf.Max(1, baseline));
            int durationBonus = Mathf.RoundToInt(extension * 50f);
            int crewBonus = Mathf.Clamp(crew == null ? 0 : crew.Count, 0, 2) * 10;
            return 100 + durationBonus + crewBonus;
        }

        private static int ScaleDispatchReward(int baseline, int rewardPercent)
        {
            return baseline <= 0 ? 0 : Mathf.Max(0, Mathf.RoundToInt(baseline * Mathf.Clamp(rewardPercent, 50, 300) / 100f));
        }

        private static int[] DispatchRewards(int mission, int percent, bool legacy)
        {
            // Existing jobs retain the quote they were launched with before this balance pass.
            if (legacy) return new[]
            {
                ScaleDispatchReward(mission == 0 ? 35 : mission == 1 ? 110 : 70, percent),
                ScaleDispatchReward(mission == 0 ? 4 : 0, percent),
                ScaleDispatchReward(mission == 2 ? 5 : 0, percent),
                ScaleDispatchReward(mission == 1 ? 3 : mission == 2 ? 2 : 0, percent),
            };
            return new[]
            {
                GameplayBalance.ScaleReward(GameplayBalance.DispatchGold(mission), percent),
                GameplayBalance.ScaleReward(GameplayBalance.DispatchWood(mission), percent),
                GameplayBalance.ScaleReward(GameplayBalance.DispatchIron(mission), percent),
                GameplayBalance.ScaleReward(GameplayBalance.DispatchFame(mission), percent),
            };
        }

        private static string DispatchRewardSummary(int missionIndex, int durationHours, IReadOnlyList<int> crew, bool legacy = false)
        {
            int safe = Mathf.Clamp(missionIndex, 0, DispatchNames.Length - 1);
            int rewardPercent = DispatchRewardPercent(safe, durationHours, crew, legacy);
            List<string> rewards = new List<string>();
            int[] amounts = DispatchRewards(safe, rewardPercent, legacy);
            int gold = amounts[0], wood = amounts[1], iron = amounts[2], fame = amounts[3];
            if (gold > 0) rewards.Add("金币 +" + gold);
            if (wood > 0) rewards.Add("木材 +" + wood);
            if (iron > 0) rewards.Add("铁矿 +" + iron);
            if (fame > 0) rewards.Add("声望 +" + fame);
            return rewards.Count > 0 ? string.Join(" / ", rewards) : "无固定物资";
        }

        private static string BuildDispatchCrewCsv(IReadOnlyList<int> crew)
        {
            if (crew == null || crew.Count == 0) return string.Empty;
            List<string> values = new List<string>();
            for (int index = 0; index < crew.Count && values.Count < 2; index += 1)
            {
                values.Add(crew[index].ToString());
            }
            return string.Join(",", values);
        }

        private static List<int> ParseDispatchCrewCsv(string value)
        {
            List<int> result = new List<int>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            string[] parts = value.Split(',');
            for (int index = 0; index < parts.Length && result.Count < 2; index += 1)
            {
                if (int.TryParse(parts[index], out int crewIndex) && crewIndex >= 0 && crewIndex < TavernCrewSlotCount && !result.Contains(crewIndex))
                {
                    result.Add(crewIndex);
                }
            }
            return result;
        }

        private static string DispatchCrewNames(IReadOnlyList<int> crew)
        {
            if (crew == null || crew.Count == 0) return string.Empty;
            List<string> names = new List<string>();
            for (int index = 0; index < crew.Count; index += 1)
            {
                int crewIndex = crew[index];
                if (crewIndex >= 0 && crewIndex < TavernCrewSlotCount)
                {
                    names.Add(ActiveCrewForProfession(crewIndex).DisplayName);
                }
            }
            return string.Join(" / ", names);
        }

        private static string BuildDispatchCrewIdentityCsv(IReadOnlyList<int> professions)
        {
            List<string> ids = new List<string>();
            if (professions != null)
                for (int index = 0; index < professions.Count && ids.Count < 2; index += 1)
                    ids.Add(ActiveCrewForProfession(professions[index]).Id);
            return string.Join(",", ids);
        }

        private static void FitCrewPortrait(RawImage portrait, Vector2 maximumSize)
        {
            if (portrait == null || portrait.texture == null) return;
            float width = portrait.texture.width * portrait.uvRect.width;
            float height = portrait.texture.height * portrait.uvRect.height;
            if (width <= 0f || height <= 0f) return;
            float scale = Mathf.Min(maximumSize.x / width, maximumSize.y / height);
            portrait.rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
        }

        private static string PersistedDispatchCrewNames(IReadOnlyList<int> legacyProfessions)
        {
            // Snapshot the personal identities at departure. Recruiting a new
            // navigator at another port must not rename a crew already at sea.
            string identities = PlayerPrefs.GetString(DispatchCrewIdentityKey, string.Empty);
            List<string> names = new List<string>();
            foreach (string id in identities.Split(','))
            {
                CrewVariantDefinition crew = CrewNarrativeCatalog.FindById(id);
                if (crew != null && names.Count < 2) names.Add(crew.DisplayName);
            }
            if (names.Count == 0 && legacyProfessions != null)
                for (int index = 0; index < legacyProfessions.Count && names.Count < 2; index += 1)
                    names.Add(CrewNarrativeCatalog.LegacyForProfession(legacyProfessions[index]).DisplayName);
            return string.Join(" / ", names);
        }

        private static List<CrewVariantDefinition> RecruitedCrewForProfession(int profession)
        {
            List<CrewVariantDefinition> crew = new List<CrewVariantDefinition>();
            foreach (CrewVariantDefinition candidate in CrewNarrativeCatalog.AllCrew)
                if (candidate.ProfessionIndex == profession && IsCrewRecruited(candidate)) crew.Add(candidate);
            return crew;
        }

        private void CycleActiveCrewIdentity(int profession)
        {
            List<CrewVariantDefinition> roster = RecruitedCrewForProfession(profession);
            if (roster.Count < 2) return;
            string activeId = ActiveCrewForProfession(profession).Id;
            int next = 0;
            for (int index = 0; index < roster.Count; index += 1)
                if (roster[index].Id == activeId) next = (index + 1) % roster.Count;
            PlayerPrefs.SetString(ActiveCrewVariantKey(profession), roster[next].Id);
            RefreshCrewCapabilities();
            SaveProgress();
            portNotice = "派遣候选已切换为「" + roster[next].DisplayName + "」。";
            Render();
        }

        private static void ConfigureDispatchChartHover(Button button, Image glow, RawImage chart, Color accent)
        {
            if (button == null || glow == null || chart == null) return;
            button.transition = Selectable.Transition.None;
            button.targetGraphic = chart;
            Color restingChart = chart.color;
            Color restingGlow = Color.clear;
            glow.color = restingGlow;

            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();
            EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ =>
            {
                // The chart remains readable at its authored colour. Selection
                // is communicated by the detail sheet after click, not a glow.
                glow.color = restingGlow;
                chart.color = restingChart;
            });
            trigger.triggers.Add(pointerEnter);
            EventTrigger.Entry pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ =>
            {
                glow.color = restingGlow;
                chart.color = restingChart;
            });
            trigger.triggers.Add(pointerExit);
        }

        private static string FormatDispatchRemaining(long ticks)
        {
            TimeSpan remaining = TimeSpan.FromTicks(Math.Max(0L, ticks));
            if (remaining.TotalHours >= 1d)
            {
                return Math.Ceiling(remaining.TotalHours) + " 小时";
            }
            return Math.Max(1d, Math.Ceiling(remaining.TotalMinutes)) + " 分钟";
        }

        private void BuildTreasureFacility()
        {
            RectTransform panel = CreateFixedPanel("Treasure Panel", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f), Color.clear);
            panel.GetComponent<Image>().raycastTarget = false;
            for (int index = 0; index < CoreVoyageSession.Routes.Count; index += 1)
            {
                int captured = index;
                CoreRouteDefinition route = CoreVoyageSession.Routes[index];
                bool selected = session.SelectedRouteIndex == index;
                TreasureChartDisplayLayout display = TreasureChartDisplayLayout.For(index);
                // Furniture, feet and their contact shadows are painted into the
                // enlarged room. Only the paper is layered onto its measured face.
                Button card = CreateButton("Treasure Route " + index, panel, display.Size, display.Position, Color.clear, Ink, 18);
                card.GetComponentInChildren<Text>().text = string.Empty;
                card.GetComponent<Image>().raycastTarget = false;
                Image paperContact = CreateImage("Treasure Chart Paper Contact " + index, card.transform, display.Size,
                    new Vector2(0f, -2f), new Color(0.025f, 0.015f, 0.005f, 0.55f));
                paperContact.raycastTarget = false;
                display.Project(paperContact);
                Image paperEdge = CreateImage("Treasure Chart Paper Edge " + index, card.transform, display.Size,
                    Vector2.zero, selected ? Html("#D9B969") : Html("#A88C59"));
                paperEdge.raycastTarget = false;
                display.Project(paperEdge);
                RawImage map = CreateRawImage("Treasure Route Art " + index, card.transform, display.Size - Vector2.one * 5f,
                    Vector2.zero, art.RouteMap(index), new Rect(0f, 0f, 1f, 1f));
                display.Project(map);
                map.raycastTarget = true;
                map.color = new Color(0.91f, 0.88f, 0.80f, 1f);
                Image glow = CreateImage("Treasure Route Hover " + index, card.transform, display.Size,
                    Vector2.zero, Color.clear);
                glow.raycastTarget = false;
                display.Project(glow);
                Text title = CreateText("Treasure Route Name " + index, card.transform, display.LabelSize,
                    display.LabelPosition - display.Position, 20, FontStyle.Bold, TextAnchor.MiddleCenter, selected ? Gold : Ink);
                title.text = route.Name + (selected ? " · 已选" : string.Empty);
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 17;
                title.resizeTextMaxSize = 20;
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                title.verticalOverflow = VerticalWrapMode.Truncate;
                title.raycastTarget = true;
                AddSceneLabelOutline(title);
                ConfigureGroundedObjectHover(card, glow, map, title, Gold);
                card.onClick.AddListener(() =>
                {
                    session.SelectRoute(captured);
                    session.ClearSelectedTreasureMap();
                    SaveProgress();
                    portNotice = "已在海图展板标记「" + CoreVoyageSession.Routes[captured].Name + "」。";
                    Render();
                });
            }
            // Keep the reading HUD below the grounded feet, never over the maps.
            CoreRouteDefinition selectedRoute = CoreVoyageSession.Routes[session.SelectedRouteIndex];
            // The background now uses a centered cover crop. Keep the reading desk
            // below the painted board feet while leaving its bottom inside the
            // 1920x932 surface (content y = -540).
            RectTransform reading = CreateFixedPanel("Treasure Route Reading Desk", panel, new Vector2(1240f, 126f), new Vector2(0f, -404f), Color.white);
            Image readingFace = reading.GetComponent<Image>();
            readingFace.sprite = WoodButtonSkin.Face;
            readingFace.type = Image.Type.Sliced;
            readingFace.pixelsPerUnitMultiplier = 14f;
            readingFace.raycastTarget = false;
            Text readingTitle = CreateText("Treasure Selected Route", reading, new Vector2(1160f, 28f), new Vector2(0f, 46f), 24, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            readingTitle.text = selectedRoute.Name + " · 航线档案";
            Text readingDetail = CreateText("Treasure Route Detail", reading, new Vector2(1160f, 88f), new Vector2(0f, -16f), 18, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            readingDetail.resizeTextForBestFit = true;
            readingDetail.resizeTextMinSize = 18;
            readingDetail.resizeTextMaxSize = 18;
            readingDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            readingDetail.verticalOverflow = VerticalWrapMode.Truncate;
            // An archived drawing belongs only to its own route. Keep the route
            // information and switching hint visible after the first chart is drawn.
            string drawnChart = LastDrawnChartSummary(session.SelectedRouteIndex);
            string chartDetail = !string.IsNullOrEmpty(drawnChart) ? drawnChart :
                session.CanDrawSeaChart ? "海图种子：" + session.SelectedSeed : "海图尚未绘制 · 随船航海士可揭示种子";
            readingDetail.text = selectedRoute.Theme + "　·　推荐战力 " + selectedRoute.RecommendedPower + "　·　区域首领 " + selectedRoute.BossName +
                "\n" + chartDetail + "\n选择展板切换航线";
            BuildTreasureWorkbench(panel);
        }

        private void BuildMaintenanceFacility()
        {
            RectTransform maintenance = CreateFixedPanel(
                "Maintenance Panel",
                contentRoot,
                new Vector2(1820f, 650f),
                new Vector2(0f, -55f),
                Color.clear);
            Image maintenancePanelImage = maintenance.GetComponent<Image>();
            if (maintenancePanelImage != null)
            {
                maintenancePanelImage.raycastTarget = false;
            }

            BuildMaintenanceShipInWater(maintenance);
            // Only the enlarged purchasing board remains on the right quay.
            // Draw it after the ship/water; their complete bounds stay separate.
            BuildMaintenanceSupplyBoard(maintenance);
            if (investmentSupplyPanelOpen)
            {
                Button returnToInvestment = CreateButton("Return To Port Investment", contentRoot,
                    new Vector2(300f, 42f), new Vector2(-470f, 345f), PanelRaised, Ink, 16);
                returnToInvestment.GetComponentInChildren<Text>().text = "返回港口投资建设";
                returnToInvestment.onClick.AddListener(() =>
                {
                    investmentSupplyPanelOpen = false;
                    Render();
                });
            }
        }

        private void BuildMaintenanceShipInWater(RectTransform parent)
        {
            MaintenanceSceneLayout layout = MaintenanceSceneLayoutCatalog.ForPort(session.CurrentPort);
            RectTransform waterBasin = CreateFixedPanel(
                "Maintenance Water Basin",
                parent,
                new Vector2(850f, 500f),
                layout.WaterlinePosition + new Vector2(0f, 115f),
                Color.clear);
            Image basinImage = waterBasin.GetComponent<Image>();
            if (basinImage != null)
            {
                // The shallow basin is already painted into the facility
                // background. This transparent anchor keeps the composition
                // addressable without laying a synthetic UI oval over it.
                basinImage.color = Color.clear;
                basinImage.raycastTarget = false;
            }

            RectTransform namePlate = CreateFixedPanel(
                "Maintenance Ship Name Plate",
                parent,
                new Vector2(520f, 54f),
                layout.ShipPosition + new Vector2(0f, 244f * layout.ShipScale),
                ScenePlaqueColor(0.72f));
            Text shipName = CreateText(
                "Maintenance Ship Name",
                namePlate,
                new Vector2(490f, 42f),
                Vector2.zero,
                19,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Gold);
            shipName.text = session.SelectedShip.Name + " · 下水整备中";

            // Use the selected hull for every vessel. Alternate hulls come from
            // the roster atlas and are keyed transparent by BuildActiveShipPreview;
            // their installed component layers are kept in the same local stack.
            RectTransform ship = session.SelectedShipIndex == 0
                ? BuildModularShipPreview(
                    "Maintenance Modular Ship",
                    parent,
                    MaintenanceSceneLayoutCatalog.ShipAvailableSize * layout.ShipScale,
                    layout.ShipPosition)
                : BuildActiveShipPreview(
                    "Maintenance Modular Ship",
                    parent,
                    MaintenanceSceneLayoutCatalog.ShipAvailableSize * layout.ShipScale,
                    layout.ShipPosition);
            AlignShipHullToWaterline(ship, ship.GetComponentInChildren<RawImage>(),
                layout.WaterlinePosition.y - 4f * layout.ShipScale);
            ClipMaintenanceShipBelowWaterline(ship, layout);

            if (maintenanceArt != null && maintenanceArt.WaterForeground != null)
            {
                RawImage water = CreateRawImage(
                    "Maintenance Water Foreground",
                    parent,
                    new Vector2(730f * layout.ShipScale, 92f),
                    layout.WaterlinePosition,
                    maintenanceArt.WaterForeground,
                    new Rect(0f, 0f, 1f, 1f));
                water.raycastTarget = false;
                // Regional boathouses already own their authored shallows. Keep
                // only a light waterline over the modular hull, not an opaque
                // rectangle of the generic harbour's water over a new scene.
                bool regionalWater = facilityArt != null &&
                    facilityArt.HasRegionalBackground(PortFacilityArtCatalog.MaintenanceFacilityIndex, session.CurrentPort);
                water.color = new Color(1f, 1f, 1f,
                    regionalWater ? MaintenanceSceneLayoutCatalog.RegionalWaterlineOpacity : 1f);
            }

            RectTransform statusPlate = CreateFixedPanel(
                "Maintenance Ship Status Plate",
                parent,
                new Vector2(690f, 64f),
                layout.WaterlinePosition + new Vector2(0f, -140f),
                ScenePlaqueColor(0.76f));
            Text status = CreateText(
                "Maintenance Ship Status",
                statusPlate,
                new Vector2(660f, 54f),
                Vector2.zero,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Ink);
            status.text = "船体 " + session.Hull + "/" + session.MaxHull +
                "　装甲 " + session.Armor +
                "　火力 " + session.Firepower +
                "　航速 " + session.SpeedKnots.ToString("0") + " 节" +
                "　货舱 " + session.Cargo.UsedVolume.ToString("0.0") + "/" + session.CargoCapacityValue.ToString("0");
        }

        private void ClipMaintenanceShipBelowWaterline(RectTransform ship, MaintenanceSceneLayout layout)
        {
            // Keep the existing ship root (and its sibling order) intact. Every
            // full-sized modular layer shares this one shallow-water clipping
            // plane; cropping only the hull would leave plating/bow tips afloat.
            List<RectTransform> layers = new List<RectTransform>();
            for (int index = 0; index < ship.childCount; index += 1)
            {
                RectTransform layer = ship.GetChild(index) as RectTransform;
                if (layer != null) layers.Add(layer);
            }

            float top = ship.rect.yMax + 32f * layout.ShipScale;
            float bottom = layout.WaterlinePosition.y - ship.anchoredPosition.y - 6f * layout.ShipScale;
            RectTransform clipping = CreateRect(
                "Maintenance Ship Waterline Clip",
                ship,
                new Vector2(ship.rect.width + 96f * layout.ShipScale, top - bottom),
                new Vector2(0f, (top + bottom) * 0.5f));
            RectMask2D mask = clipping.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, 8);

            foreach (RectTransform layer in layers)
            {
                // Reparenting with a translated mask must not resize or shift
                // any equipment relative to the base hull. Both parents use
                // unit scale/rotation; only their local origins differ.
                Vector3 oldPosition = layer.localPosition;
                layer.SetParent(clipping, false);
                layer.localPosition = oldPosition - clipping.localPosition;
            }
        }

        private void BuildMaintenanceSupplyBoard(RectTransform parent)
        {
            DateTime now = MarketNow;
            maintenanceDisplayedDate = MarketMonth(now);
            marketLastObservedMonth = MarketMonth(now);
            int investmentLevel = PortInvestmentLevel(session.CurrentPort);
            MaintenanceSceneLayout layout = MaintenanceSceneLayoutCatalog.ForPort(session.CurrentPort);
            BuildMaintenanceBoardContactShadows(parent, "Supply", layout.SupplyPosition,
                MaintenanceSceneLayoutCatalog.SupplyBoardSize, layout.SupplyScale,
                MaintenanceSceneLayoutCatalog.SupplyLeftFootUv,
                MaintenanceSceneLayoutCatalog.SupplyRightFootUv);
            RectTransform supply = CreateFixedPanel(
                "Maintenance Supply Panel",
                parent,
                MaintenanceSceneLayoutCatalog.SupplyBoardSize,
                layout.SupplyPosition,
                Color.clear);
            supply.localScale = Vector3.one * layout.SupplyScale;
            supply.GetComponent<Image>().raycastTarget = false;
            if (maintenanceArt != null && maintenanceArt.SupplyBoard != null)
            {
                RawImage boardArt = CreateRawImage(
                    "Maintenance Supply Board Art",
                    supply,
                    MaintenanceSceneLayoutCatalog.SupplyBoardSize,
                    Vector2.zero,
                    maintenanceArt.SupplyBoard,
                    new Rect(0f, 0f, 1f, 1f));
                boardArt.raycastTarget = false;
            }
            else
            {
                CreateFixedPanel(
                    "Maintenance Supply Board Backing",
                    supply,
                    MaintenanceSceneLayoutCatalog.SupplyPaperSafeRect.size,
                    MaintenanceSceneLayoutCatalog.SupplyPaperSafeRect.center,
                    new Color(0.12f, 0.07f, 0.025f, 0.92f));
            }

            // The enlarged paper face holds all six supplies at once. This is a
            // fixed ledger: no scrolling state, hidden ammunition rows or scroll rail.
            Text title = CreateText("Maintenance Supply Title", supply, new Vector2(510f, 30f), new Vector2(0f, 156f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, DeepSea);
            title.text = "出航补给采购";
            Text instruction = CreateText("Maintenance Supply Instruction", supply, new Vector2(510f, 20f), new Vector2(0f, 130f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, DeepSea);
            instruction.text = "水粮每次 10 单位 · 与市场共享本月价格和采购额度";
            RectTransform ledger = CreateRect("Maintenance Supply Ledger", supply,
                new Vector2(520f, 216f), new Vector2(0f, 6f));
            RectTransform entries = CreateRect("Maintenance Supply Ledger Entries", ledger,
                ledger.sizeDelta, Vector2.zero);

            int rowIndex = 0;
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
            {
                VoyageSupplyType capturedType = type;
                int step = SupplyPurchaseStep(type);
                float y = -18f - rowIndex * 36f;
                RectTransform row = CreateFixedPanel("Supply Row " + type, entries, new Vector2(516f, 34f), new Vector2(0f, y), new Color(0.21f, 0.10f, 0.025f, rowIndex % 2 == 0 ? 0.09f : 0.025f));
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                Text label = CreateText("Supply Label " + type, row, new Vector2(230f, 32f), new Vector2(-139f, 0f), 15, FontStyle.Bold, TextAnchor.MiddleLeft, DeepSea);
                int priceQuantity = type == VoyageSupplyType.Water || type == VoyageSupplyType.Food ? 100 : 1;
                PortMarketItem item = MaintenanceMarketItem(type);
                int remaining = MarketRemaining(session.CurrentPort, item, now, investmentLevel);
                label.text = CoreVoyageSession.SupplyName(type) + "　现有 " + session.GetSupplyAmount(type) +
                    " · 可购 " + remaining + "\n每 " + priceQuantity + " 单位 " +
                    PortMarketCatalog.FormatPrice(PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, priceQuantity)) + " 金币";

                Button decrease = CreateButton("Supply Decrease " + type, row, new Vector2(34f, 32f), new Vector2(10f, 0f), Panel, Ink, 17);
                ConfigureSupplyAmountButton(decrease, "−");
                decrease.interactable = pendingSupplyOrder[type] > 0;
                decrease.onClick.AddListener(() => AdjustSupplyPurchase(capturedType, -step));

                Text amount = CreateText("Supply Purchase Amount " + type, row, new Vector2(60f, 32f), new Vector2(61f, 0f), 15, FontStyle.Bold, TextAnchor.MiddleCenter, Html("#783826"));
                amount.text = "+" + pendingSupplyOrder[type];

                VoyageSupplyOrder increased = CopySupplyOrder(pendingSupplyOrder);
                increased[type] += step;
                Button increase = CreateButton("Supply Increase " + type, row, new Vector2(34f, 32f), new Vector2(112f, 0f), Teal, Ink, 17);
                ConfigureSupplyAmountButton(increase, "+");
                MaintenanceSupplyQuote increasedQuote = QuoteMaintenanceSupplies(increased, now);
                increase.interactable = increased[type] <= 2000 && increasedQuote.FitsCargo && increasedQuote.WithinQuota;
                increase.onClick.AddListener(() => AdjustSupplyPurchase(capturedType, step));

                Text lineCost = CreateText("Supply Line Cost " + type, row, new Vector2(110f, 32f), new Vector2(197f, 0f), 14, FontStyle.Bold, TextAnchor.MiddleRight, DeepSea);
                lineCost.text = PortMarketCatalog.FormatPrice(PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, pendingSupplyOrder[type])) + " 金";
                rowIndex += 1;
            }

            MaintenanceSupplyQuote quote = QuoteMaintenanceSupplies(pendingSupplyOrder, now);
            // Keep the total on one line, clear of the parchment's torn edge.
            // Validation remains visible on the existing lower notice rail.
            Text summary = CreateText("Supply Purchase Summary", supply, new Vector2(520f, 22f), new Vector2(0f, -121f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, quote.CanPurchase || !quote.HasItems ? DeepSea : Html("#8B2F24"));
            string validation = !quote.HasItems ? "请选择采购数量" : quote.CanPurchase ? "订单可以结算" : quote.Reason;
            summary.text = "采购 " + pendingSupplyOrder.TotalUnits + " 单位 · " + PortMarketCatalog.FormatPrice(quote.Cost) + " 金币 · 货舱 " +
                session.Cargo.UsedVolume.ToString("0.0") + "→" + quote.ProjectedVolume.ToString("0.0") + "/" + session.CargoCapacityValue.ToString("0");

            Button clear = CreateButton("Clear Supply Order", supply, new Vector2(156f, 30f), new Vector2(-176f, -151f), PanelRaised, MutedInk, 14);
            clear.GetComponentInChildren<Text>().text = "清空采购";
            clear.interactable = quote.HasItems;
            clear.onClick.AddListener(() =>
            {
                pendingSupplyOrder.Clear();
                portNotice = "采购清单已清空。";
                Render();
            });

            Button standard = CreateButton("Fill Standard Supply Order", supply, new Vector2(156f, 30f), new Vector2(0f, -151f), PanelRaised, Ink, 14);
            standard.GetComponentInChildren<Text>().text = "补至标准量";
            standard.onClick.AddListener(FillStandardSupplyOrder);

            Button purchase = CreateButton("Confirm Supply Purchase", supply, new Vector2(164f, 30f), new Vector2(176f, -151f), quote.CanPurchase ? Gold : PanelRaised, quote.CanPurchase ? DeepSea : MutedInk, 14);
            purchase.GetComponentInChildren<Text>().text = "确认采购";
            purchase.interactable = Application.isPlaying && !editorPreviewBuilding && quote.CanPurchase;
            purchase.onClick.AddListener(ConfirmSupplyPurchase);

            RectTransform noticePlate = CreateFixedPanel(
                "Maintenance Notice Plate",
                supply,
                MaintenanceSceneLayoutCatalog.SupplyNoticeSafeRect.size,
                MaintenanceSceneLayoutCatalog.SupplyNoticeSafeRect.center,
                ScenePlaqueColor(0.84f));
            noticePlate.gameObject.AddComponent<RectMask2D>();
            Text note = CreateText("Maintenance Notice", noticePlate, new Vector2(452f, 38f), Vector2.zero, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            note.text = validation + "\n" + portNotice;
            // Text bounds belong to the writing face or timber notice rail.
            // Long save-state values and notices must not grow beyond those
            // surfaces; steppers retain their explicitly padded single glyphs.
            FitMaintenanceBoardText(supply);
        }

        private void BuildMaintenanceBoardContactShadows(RectTransform parent, string boardName,
            Vector2 boardPosition, Vector2 boardSize, float scale, Vector2 leftFootUv, Vector2 rightFootUv)
        {
            Vector2[] feet = { leftFootUv, rightFootUv };
            for (int index = 0; index < feet.Length; index += 1)
            {
                Vector2 foot = boardPosition + MaintenanceSceneLayoutCatalog.FootOffset(feet[index], boardSize) * scale;
                // Local, flattened penumbrae under each real foot. No opaque
                // rectangle or fabricated floor is allowed to bridge the canal.
                for (int layer = 0; layer < 3; layer += 1)
                {
                    float falloff = 1f - layer * 0.22f;
                    Image shadow = CreateImage("Maintenance " + boardName + " Foot Contact " + index + " " + layer,
                        parent, new Vector2(66f, 13f) * scale * falloff, foot,
                        new Color(0.025f, 0.018f, 0.012f, 0.085f + layer * 0.055f), circleSprite);
                    shadow.raycastTarget = false;
                }
            }
        }

        private static void ConfigureSupplyAmountButton(Button button, string symbol)
        {
            Text label = button.GetComponentInChildren<Text>();
            label.text = symbol;
            // Small physical steppers must not inherit full-size button padding.
            label.rectTransform.sizeDelta = new Vector2(26f, 24f);
            // Stepper glyphs are intentionally single-line, but still clip to
            // their 26×24 button bay so a future localized symbol cannot bleed
            // into the quantity or cost columns.
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.fontSize = 18;
        }

        private void AdjustSupplyPurchase(VoyageSupplyType type, int delta)
        {
            pendingSupplyOrder[type] = Mathf.Clamp(pendingSupplyOrder[type] + delta, 0, 2000);
            portNotice = "确认清单后扣费并装船。";
            Render();
        }

        private void FillStandardSupplyOrder()
        {
            pendingSupplyOrder.Clear();
            DateTime now = MarketNow;
            int level = PortInvestmentLevel(session.CurrentPort);
            VoyageSupplyOrder recommended = session.CreateRecommendedSupplyOrder();
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                pendingSupplyOrder[type] = Math.Min(recommended[type], MarketRemaining(session.CurrentPort, MaintenanceMarketItem(type), now, level));
            MaintenanceSupplyQuote quote = QuoteMaintenanceSupplies(pendingSupplyOrder, now);
            portNotice = quote.HasItems
                ? "推荐清单已按航线、船员、剩余舱容和本月额度调整，确认后扣费。"
                : "无需补充，或本月额度、货舱余量不足；可前往市场检查。";
            Render();
        }

        private static int SupplyPurchaseStep(VoyageSupplyType type)
        {
            return type == VoyageSupplyType.Water || type == VoyageSupplyType.Food ? 10 : 1;
        }

        private static VoyageSupplyOrder CopySupplyOrder(VoyageSupplyOrder source)
        {
            return new VoyageSupplyOrder
            {
                Water = source.Water,
                Food = source.Food,
                Medicine = source.Medicine,
                Wood = source.Wood,
                Powder = source.Powder,
                Cannonballs = source.Cannonballs,
            };
        }

        public static string CrewRecruitmentKey(int crewIndex)
        {
            return CrewRecruitmentKeyPrefix + Mathf.Clamp(crewIndex, 0, TavernCrewSlotCount - 1);
        }

        public static string CrewVariantRecruitmentKey(string crewId)
        {
            return CrewVariantRecruitmentKeyPrefix + (string.IsNullOrWhiteSpace(crewId) ? "unknown" : crewId.Trim());
        }

        public static string ActiveCrewVariantKey(int professionIndex)
        {
            return ActiveCrewVariantKeyPrefix + Mathf.Clamp(professionIndex, 0, TavernCrewSlotCount - 1);
        }

        public static string ShipOwnedKey(int shipIndex)
        {
            return ShipOwnedKeyPrefix + Mathf.Clamp(shipIndex, 0, CoreVoyageSession.Ships.Count - 1);
        }

        private bool IsCrewRecruited(int crewIndex)
        {
            int safe = Mathf.Clamp(crewIndex, 0, TavernCrewSlotCount - 1);
            if (PlayerPrefs.GetInt(CrewRecruitmentKey(safe), 0) == 1) return true;
            IReadOnlyList<CrewVariantDefinition> allCrew = CrewNarrativeCatalog.AllCrew;
            for (int index = 0; index < allCrew.Count; index += 1)
            {
                if (allCrew[index].ProfessionIndex == safe && PlayerPrefs.GetInt(CrewVariantRecruitmentKey(allCrew[index].Id), 0) == 1) return true;
            }
            return false;
        }

        private CrewVariantDefinition CurrentTavernCrew(int professionIndex)
        {
            string port = session == null ? "灰鸥港" : session.CurrentPort;
            return CrewNarrativeCatalog.ForPortAndProfession(port, professionIndex);
        }

        private static bool IsCrewRecruited(CrewVariantDefinition crew)
        {
            if (crew == null) return false;
            if (PlayerPrefs.GetInt(CrewVariantRecruitmentKey(crew.Id), 0) == 1) return true;
            // The v1 flag is an aggregate profession capability after migration,
            // not proof that the Gray Gull person was individually recruited.
            return crew.IsLegacy && !HasStoredCrewIdentity(crew.ProfessionIndex) &&
                PlayerPrefs.GetInt(CrewRecruitmentKey(crew.ProfessionIndex), 0) == 1;
        }

        private static bool HasStoredCrewIdentity(int profession)
        {
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                if (crew.ProfessionIndex == profession && PlayerPrefs.GetInt(CrewVariantRecruitmentKey(crew.Id), 0) == 1) return true;
            return false;
        }

        private static void MigrateLegacyCrewIdentities()
        {
            for (int profession = 0; profession < TavernCrewSlotCount; profession += 1)
            {
                if (PlayerPrefs.GetInt(CrewRecruitmentKey(profession), 0) != 1 || HasStoredCrewIdentity(profession)) continue;
                CrewVariantDefinition legacy = CrewNarrativeCatalog.LegacyForProfession(profession);
                PlayerPrefs.SetInt(CrewVariantRecruitmentKey(legacy.Id), 1);
                if (!PlayerPrefs.HasKey(ActiveCrewVariantKey(profession)))
                    PlayerPrefs.SetString(ActiveCrewVariantKey(profession), legacy.Id);
            }
        }

        private void MigrateMissingCrewLocations()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null) return;
            RecoverCrewLocationsAfterReload();

            // Early v2 saves persisted the recruited identity but did not yet
            // persist its port. The identity is known to be in the current
            // session, so anchor it here before capability and card-source
            // restoration runs. This keeps old saves playable without
            // changing the normal recruit/return location flow.
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                if (!IsCrewRecruited(crew)) continue;
                string key = CrewLocationKeyForReset(crew.Id);
                if (!PlayerPrefs.HasKey(key))
                    PlayerPrefs.SetString(key, session.CurrentPort);
            }
        }

        private static CrewVariantDefinition ActiveCrewForProfession(int professionIndex)
        {
            int safe = Mathf.Clamp(professionIndex, 0, TavernCrewSlotCount - 1);
            string activeId = PlayerPrefs.GetString(ActiveCrewVariantKey(safe), string.Empty);
            CrewVariantDefinition active = CrewNarrativeCatalog.FindById(activeId);
            if (active != null && active.ProfessionIndex == safe && IsCrewRecruited(active)) return active;

            IReadOnlyList<CrewVariantDefinition> allCrew = CrewNarrativeCatalog.AllCrew;
            for (int index = 0; index < allCrew.Count; index += 1)
            {
                CrewVariantDefinition candidate = allCrew[index];
                if (candidate.ProfessionIndex == safe && PlayerPrefs.GetInt(CrewVariantRecruitmentKey(candidate.Id), 0) == 1)
                {
                    return candidate;
                }
            }

            // A v1 save only knows that the profession was recruited.  Resolve
            // that aggregate flag to the original Gray Gull identity.
            return CrewNarrativeCatalog.LegacyForProfession(safe);
        }

        private void RefreshCrewCapabilities()
        {
            session.ConfigureCrewCapabilities(
                IsCrewAvailableForVoyage(0),
                IsCrewAvailableForVoyage(1),
                IsCrewAvailableForVoyage(2),
                IsCrewAvailableForVoyage(3),
                IsCrewAvailableForVoyage(4),
                IsCrewAvailableForVoyage(5));
            RefreshSpecialCardSources();
        }

        private bool IsCrewAvailableForVoyage(int profession)
        {
            if (!IsCrewRecruited(profession)) return false;
            CrewVariantDefinition located = ActiveCrewForProfession(profession);
            if (located != null && !IsCrewAvailableAtCurrentPort(located)) return false;
            if (PlayerPrefs.GetInt(DispatchMissionKey, -1) < 0) return true;
            List<int> dispatched = ParseDispatchCrewCsv(PlayerPrefs.GetString(DispatchCrewKey, string.Empty));
            if (!dispatched.Contains(profession)) return true;
            CrewVariantDefinition active = ActiveCrewForProfession(profession);
            foreach (string id in PlayerPrefs.GetString(DispatchCrewIdentityKey, string.Empty).Split(','))
            {
                CrewVariantDefinition away = CrewNarrativeCatalog.FindById(id);
                if (away != null && away.ProfessionIndex == profession)
                    return active != null && !string.Equals(active.Id, away.Id, StringComparison.Ordinal);
            }
            // A v1 dispatch has only a profession snapshot; reserve that role
            // until its crew is recalled or the completed mission is claimed.
            return false;
        }

        private string BuildCrewCapabilitySummary()
        {
            List<string> roles = new List<string>();
            if (session.HasNavigator) roles.Add("航海士");
            if (session.HasCarpenter) roles.Add("木匠");
            if (session.HasDoctor) roles.Add("船医");
            if (session.HasGunner) roles.Add("炮术官");
            if (session.HasCook) roles.Add("厨师");
            if (session.HasDiver) roles.Add("潜水员");
            return roles.Count > 0 ? string.Join(" / ", roles) + "已登船" : "仅普通水手 · 职业选项未启用";
        }

        private void RestorePortProgression()
        {
            PlayerPrefs.DeleteKey(LegacyWitchRecruitedKey);
            MigrateLegacyCrewIdentities();
            MigrateMissingCrewLocations();
            int selectedShip = Mathf.Clamp(PlayerPrefs.GetInt(SelectedShipKey, 0), 0, CoreVoyageSession.Ships.Count - 1);
            if (selectedShip > 0 && PlayerPrefs.GetInt(ShipOwnedKey(selectedShip), 0) == 0)
            {
                selectedShip = 0;
            }
            session.SelectShip(selectedShip);
            RefreshComponentBonuses();
            selectedShipPreview = selectedShip;

            if (PlayerPrefs.HasKey(CargoKey("Water")))
            {
                session.Cargo.Water = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Water"), session.Cargo.Water));
                session.Cargo.Food = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Food"), session.Cargo.Food));
                session.Cargo.Beer = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Beer"), session.Cargo.Beer));
                session.Cargo.Medicine = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Medicine"), session.Cargo.Medicine));
                session.Cargo.Wood = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Wood"), session.Cargo.Wood));
                session.Cargo.Iron = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Iron"), session.Cargo.Iron));
                session.Cargo.Cloth = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Cloth"), session.Cargo.Cloth));
                session.Cargo.Powder = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Powder"), session.Cargo.Powder));
                session.Cargo.Cannonballs = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Cannonballs"), session.Cargo.Cannonballs));
                session.Cargo.Gold = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Gold"), session.Cargo.Gold));
                session.Cargo.Fame = Mathf.Max(0, PlayerPrefs.GetInt(CargoKey("Fame"), session.Cargo.Fame));
            }

            RefreshCrewCapabilities();
            RestoreStageBattleDecks();
            RestoreBattleCardUpgrades();
            RestoreSpecialCardProgress();
        }

        private static string FacilityKey(int index)
        {
            string[] keys = { "Warehouse", "Shipyard", "Tavern", "ComponentShop", "CardWorkshop", "Dispatch", "Treasure", "Maintenance", "Voyage", "Market" };
            return keys[Mathf.Clamp(index, 0, keys.Length - 1)];
        }

        private void BuildComponentShopDisplay(Transform parent, ComponentArtEntry entry, string prefix, string suffix,
            bool sold, bool selected, bool interactive, bool includePlaque, bool transparentPlaque,
            out RawImage aura, out RawImage model, out RawImage plaque)
        {
            ComponentShopDisplayGeometry geometry = ComponentShopDisplayGeometry.For(entry, session.CurrentPort);
            // A detail sheet has no foreground sign: centre the part in the
            // illustration area rather than retaining empty space for a rack.
            // UIPreviewsForMe uses the left-hand showroom component as the
            // authored reference. Treat its 300x194.4 rectangle as a fit box,
            // not as a forced RawImage size: RawImage does not preserve the
            // cropped UV aspect ratio by itself. Detail sheets continue to use
            // their measured native-fit sizing.
            Vector2 modelSize = includePlaque
                ? ComponentShopDisplayGeometry.FitShowroomModelSize(geometry.ModelUv)
                : ComponentShopDisplayGeometry.FitDetailModelSize(geometry.ModelUv);
            Vector2 modelPosition = includePlaque ? geometry.ShowroomModelPositionFor(modelSize) : Vector2.zero;
            // The component artwork sits directly on the scene surface. Do not
            // add a panel, card, or rectangular shadow behind it: the source
            // atlas already carries the transparent silhouette edge. The two
            // callers intentionally use different fit boxes (the compact shop
            // card and the larger detail sheet) while both preserve native UV
            // aspect ratio.
            // The aura follows the same fitted bounds. Using one fixed aura
            // rectangle would reintroduce non-uniform scaling whenever a tall
            // or narrow component is displayed.
            aura = CreateRawImage(prefix + "Rarity Aura" + suffix, parent,
                modelSize * 1.035f,
                modelPosition, entry.ShipLayer, geometry.ModelUv);
            // Rarity is communicated by the plaque/text, not a persistent
            // coloured halo behind every offer.
            aura.color = Color.clear;
            aura.raycastTarget = false;
            model = CreateRawImage(prefix + "Model" + suffix, parent, modelSize,
                modelPosition, entry.ShipLayer, geometry.ModelUv);
            model.raycastTarget = interactive;
            model.color = sold ? new Color(0.5f, 0.5f, 0.5f, 0.72f) : Color.white;
            plaque = null;
            if (!includePlaque) return;
            plaque = CreateRawImage(prefix + "Plaque" + suffix, parent, geometry.PlaqueSize,
                geometry.PlaquePosition, geometry.RegionalFurniture != null ? geometry.RegionalFurniture.Atlas : componentShopArt == null ? null : componentShopArt.Plaque, geometry.PlaqueUv);
            if (geometry.RegionalFurniture != null) plaque.material = geometry.RegionalFurniture.Material;
            plaque.raycastTarget = interactive;
            plaque.color = sold ? new Color(0.62f, 0.62f, 0.62f, 0.9f) : Color.white;
            // Every generated mystery offer intentionally leaves its painted sign
            // surface transparent while retaining the writing area and click target.
            if (transparentPlaque)
                plaque.color = new Color(1f, 1f, 1f, 0f);
        }

        private static Color ScenePlaqueColor(float alpha)
        {
            return new Color(0.075f, 0.045f, 0.025f, alpha);
        }

        private static void ConfigureSceneItemButton(Button button, Graphic target, bool selected, Color accent)
        {
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? Color.Lerp(Color.white, accent, 0.22f) : Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.58f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.8f);
            colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.78f);
            colors.colorMultiplier = 1.25f;
            colors.fadeDuration = 0.11f;
            button.colors = colors;
        }

        private static void AddSceneLabelOutline(Text text)
        {
            if (text == null) return;
            // Keep one, contrast-aware outline per label. Older preview objects can
            // carry a Shadow/Outline component from their authored prefab; stacking
            // those effects draws the same glyph twice with a visible offset.
            foreach (Shadow shadow in text.GetComponents<Shadow>())
            {
                if (shadow.GetType() == typeof(Shadow)) shadow.enabled = false;
            }
            Outline outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            Outline[] outlines = text.GetComponents<Outline>();
            for (int index = 0; index < outlines.Length; index += 1)
            {
                if (outlines[index] != outline) outlines[index].enabled = false;
            }
            // The embedded woodcut face is already crisp. A near-black 2 px
            // outline on dark-ink labels creates a second, offset glyph image
            // on parchment (most visible in Market and Port Investment). Keep
            // outlines only for light text over dark timber rows and make them
            // small enough to support contrast without reading as a duplicate.
            float luminance = text.color.r * 0.2126f + text.color.g * 0.7152f + text.color.b * 0.0722f;
            if (luminance < 0.42f)
            {
                outline.enabled = false;
                return;
            }
            outline.enabled = true;
            outline.useGraphicAlpha = true;
            outline.effectColor = new Color(0.01f, 0.015f, 0.02f, 0.30f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);
        }

        private static Color FacilityPanelColor()
        {
            return new Color(Panel.r, Panel.g, Panel.b, 0.08f);
        }
    }
}
