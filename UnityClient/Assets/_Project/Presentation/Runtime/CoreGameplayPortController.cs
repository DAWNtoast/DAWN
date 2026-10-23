using System;
using System.Collections.Generic;
using System.Linq;
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
        // Retired prototype identity: read only to remove its obsolete save flag.
        public const string LegacyWitchRecruitedKey = "ShatteredSail.Crew.WitchRecruited";

        private static readonly string[] PortFacilityNames =
        {
            "船长收藏室",
            "皇家船坞",
            "灰鸥酒馆",
            "部件商店",
            "卡组工坊",
            "藏宝图桌",
            "港口投资建设",
            "出航码头",
            "港口市场",
        };

        private const int TavernCrewSlotCount = CrewNarrativeCatalog.ProfessionCount;
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
        private MaintenanceArtCatalog maintenanceArt;
        private int activePortFacility = -1;
        private string selectedComponentShopOfferId;
        private int componentShopDisplayPage;
        private bool cardWorkshopDeckBuilderOpen;
        private int selectedCardWorkshopChest = -1;
        private readonly VoyageSupplyOrder pendingSupplyOrder = new VoyageSupplyOrder();
        private readonly List<int> configuredBattleDeck = new List<int> { 0, 0, 1, 1, 2, 3, 4, 5, 6, 7 };

        private void BuildPort()
        {
            if (!HasInvestmentReturn) ClearInvestmentReturn();
            if (!HasDepartureReturn) ClearDepartureReturn();
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
                case PortFacilityArtCatalog.TreasureFacilityIndex: BuildTreasureFacility(); break;
                case PortFacilityArtCatalog.MaintenanceFacilityIndex: BuildPortInvestmentFacility(); break;
                case PortFacilityArtCatalog.VoyageFacilityIndex: BuildVoyageFacilityBody(); break;
                case PortFacilityArtCatalog.MarketFacilityIndex: BuildMarketFacility(); break;
                default: BuildVoyageFacilityBody(); break;
            }
        }

        private void BuildPortHub()
        {
            Texture2D regionalBackground = portArt.PortBackgroundFor(session.CurrentPort);
            Texture2D background = regionalBackground != null
                ? regionalBackground
                : facilityArt.Background(PortFacilityArtCatalog.VoyageFacilityIndex);
            BuildReusablePortBackdrop("Port Hub Background", background, Color.white,
                "Port Hub Tint", new Color(0.018f, 0.045f, 0.055f, 0.08f));

            BuildPortHubHeader();

            // Each regional composition supplies its own landmark and name positions.
            // All coordinates pass through the same crop as the authored backdrop.
            PortHubLayout layout = PortHubLayoutCatalog.ForPort(session.CurrentPort);
            IReadOnlyList<Rect> landmarkBounds = layout.LandmarkBounds;
            IReadOnlyList<Vector2> signAnchors = layout.SignAnchors;
            Vector2 sourceSize = background == null ? new Vector2(1920f, 1080f) : new Vector2(background.width, background.height);

            for (int index = 0; index < PortFacilityNames.Length; index += 1)
            {
                int captured = index;
                Rect landmark = landmarkBounds[index];
                Vector2 position = PhaseSurfaceLayout.SourceUvToContent(landmark.center, sourceSize);
                Vector2 size = PhaseSurfaceLayout.SourcePixelSizeToContent(Vector2.Scale(landmark.size, sourceSize), sourceSize);
                Button hotspot = CreateButton(
                    "Port Hotspot " + FacilityKey(index),
                    contentRoot,
                    size,
                    position,
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
                Vector2 signPosition = PhaseSurfaceLayout.SourceUvToContent(signAnchors[index], sourceSize) - position;
                string title = PortFacilityArtCatalog.RegionalFacilityTitle(index, session.CurrentPort, PortFacilityNames[index]);
                Vector2 signSize = new Vector2(Mathf.Max(168f, title.Length * 22f + 62f), 48f);
                RectTransform plate = CreateRect("Port Sign " + FacilityKey(index), hotspot.transform, signSize, signPosition);
                Image sign = plate.gameObject.AddComponent<PortNameplateGraphic>();
                sign.color = Color.white;
                sign.raycastTarget = false;
                Text label = CreateText(
                    "Port Label " + FacilityKey(index),
                    hotspot.transform,
                    new Vector2(signSize.x - 54f, 34f),
                    signPosition + new Vector2(16f, 1.5f),
                    22,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ink);
                label.text = title;
                UiLabelBacking.Apply(label);
                ConfigurePortNameplate(hotspot, hotspotGlow, label, sign);
                hotspot.onClick.AddListener(() =>
                {
                    if (BlockIncompleteDeckBuilderNavigation()) return;
                    EnterPortFacility(captured);
                    portNotice = "已进入" + PortFacilityArtCatalog.RegionalFacilityTitle(captured, session.CurrentPort, PortFacilityNames[captured]) + "。";
                    Render();
                });
            }

        }

        private static void ConfigurePortNameplate(Button hotspot, Image glow, Text label, Image sign)
        {
            // Compact brass names remain visible and rest on each landmark's
            // frontage. The building owns pointer input, including over its name.
            hotspot.transition = Selectable.Transition.None;
            glow.color = Color.clear;
            label.raycastTarget = false;
            bool hovered = false;
            bool focused = false;
            Color restingLabel = label.color;
            EventTrigger trigger = hotspot.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = hotspot.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();

            void RefreshNameplate()
            {
                bool emphasized = hovered || focused;
                label.color = emphasized ? new Color(1f, .91f, .62f, 1f) : restingLabel;
                sign.color = emphasized ? new Color(1.12f, 1.08f, .97f, 1f) : Color.white;
                glow.color = Color.clear;
            }

            void Bind(EventTriggerType type, Action callback)
            {
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => callback());
                trigger.triggers.Add(entry);
            }

            Bind(EventTriggerType.PointerEnter, () => { hovered = true; RefreshNameplate(); });
            Bind(EventTriggerType.PointerExit, () => { hovered = false; RefreshNameplate(); });
            Bind(EventTriggerType.Select, () => { focused = true; RefreshNameplate(); });
            Bind(EventTriggerType.Deselect, () => { focused = false; RefreshNameplate(); });
        }
        private static void ConfigurePortHotspotHover(Button hotspot, Image glow, Text label, Image sign = null)
        {
            if (hotspot == null || glow == null || label == null)
            {
                return;
            }

            hotspot.transition = Selectable.Transition.None;
            // The scene label sits on the painted landmark, inside its hitbox.
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
            if (index == 4)
            {
                // Explicitly clear the retained room texture when entering the card workspace.
                BuildReusablePortBackdrop("CardWorkshop Background", null, Html("#1D1711"),
                    "Facility Backdrop Tint", Color.clear);
                return;
            }
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

            if (index == PortFacilityArtCatalog.ComponentShopFacilityIndex)
            {
                Texture2D hall = Resources.Load<Texture2D>("ComponentShopV3/shipyard_hall");
                if (hall != null) texture = hall;
            }

            bool preserveFacilityColor =
                index == PortFacilityArtCatalog.WarehouseFacilityIndex ||
                index == 1 ||
                index == PortFacilityArtCatalog.TavernFacilityIndex ||
                index == PortFacilityArtCatalog.ComponentShopFacilityIndex ||
                index == PortFacilityArtCatalog.TreasureFacilityIndex ||
                index == PortFacilityArtCatalog.MaintenanceFacilityIndex ||
                index == PortFacilityArtCatalog.VoyageFacilityIndex ||
                index == PortFacilityArtCatalog.MarketFacilityIndex;
            float tintAlpha = preserveFacilityColor ? 0f : 0.13f;
            BuildReusablePortBackdrop(FacilityKey(index) + " Background", texture,
                preserveFacilityColor ? Color.white : new Color(0.9f, 0.92f, 0.94f, 1f),
                "Facility Backdrop Tint", new Color(0.005f, 0.025f, 0.035f, tintAlpha),
                fullSurface: index == PortFacilityArtCatalog.WarehouseFacilityIndex ||
                    index == PortFacilityArtCatalog.MaintenanceFacilityIndex ||
                    index == PortFacilityArtCatalog.VoyageFacilityIndex && PortFacilityArtCatalog.UsesPaintedDepartureScene(portName));
        }

        private void BuildFacilityHeader(int index)
        {
            if (TryBuildPrefabPortHeader(false, index)) return;
            RectTransform header = CreateRegionalPortHeader("Port Facility Header", "Port Facility Frame", out PortHeaderTheme theme);

            Button back = CreatePortHeaderButton("Return To Port Hub", header, new Vector2(200f, 58f), new Vector2(-793f, 0f), FacilityReturnCaption, 22, theme.Accent);
            back.onClick.AddListener(ReturnFromCachedFacility);

            int previousIndex = (index + PortFacilityNames.Length - 1) % PortFacilityNames.Length;
            string previousTitle = PortFacilityArtCatalog.RegionalFacilityTitle(previousIndex, session.CurrentPort, PortFacilityNames[previousIndex]);
            Button previous = CreatePortHeaderButton("Previous Port Facility", header, new Vector2(166f, 58f),
                new Vector2(-594f, 0f), "← " + previousTitle, 20, theme.Accent);
            previous.onClick.AddListener(PreviousCachedFacility);
            Text title = CreateText("Port Facility Title", header, new Vector2(236f, 56f), new Vector2(-385f, 0f), 29, FontStyle.Bold, TextAnchor.MiddleCenter, theme.Accent);
            title.text = PortFacilityArtCatalog.RegionalFacilityTitle(index, session.CurrentPort, PortFacilityNames[index]);
            title.raycastTarget = false;
            int nextIndex = (index + 1) % PortFacilityNames.Length;
            string nextTitle = PortFacilityArtCatalog.RegionalFacilityTitle(nextIndex, session.CurrentPort, PortFacilityNames[nextIndex]);
            Button next = CreatePortHeaderButton("Next Port Facility", header, new Vector2(166f, 58f),
                new Vector2(-176f, 0f), nextTitle + " →", 20, theme.Accent);
            next.onClick.AddListener(NextCachedFacility);
            BuildPortHeaderReadouts(header, theme, false);
            ApplyComponentShopHeaderLayout(header, index == PortFacilityArtCatalog.ComponentShopFacilityIndex);

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

            // Each port has its own furniture, activities and character positions.
            // Keep profession identity independent of the painting's spatial order.
            bool hasRegionalTavern = facilityArt.RegionalBackground(PortFacilityArtCatalog.TavernFacilityIndex, session.CurrentPort) != null;
            if (hasRegionalTavern)
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

                Vector2 namePosition = hasRegionalTavern
                    ? TavernCrewLayoutCatalog.NameOffset(session.CurrentPort, index)
                    : new Vector2(0f, hotspotSizes[index].y * 0.5f + 12f);
                Text name = CreateText(
                    "Tavern Crew Name " + index,
                    character.transform,
                    new Vector2(220f, 34f),
                    namePosition,
                    17,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ink);
                name.text = crew.DisplayName;
                // Labels may sit beside a counter or below a high platform.
                // They remain clickable children of their own crew button.
                name.raycastTarget = true;
                AddSceneLabelOutline(name);
                UiLabelBacking.Apply(name);
                Text status = CreateText(
                    "Tavern Crew Status " + index,
                    character.transform,
                    new Vector2(205f, 23f),
                    namePosition + new Vector2(0f, 28f),
                    12,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    !recruited ? Gold : availableHere ? Green : MutedInk);
                status.text = !recruited && !awayHere ? "查看档案" : availableHere ? "✓ 已入队" : "人在其他港口";
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
            RectTransform panel = CreateFixedPanel("Component Shop Panel", contentRoot, new Vector2(1820f, 930f), new Vector2(0f, -55f), Color.clear);
            if (componentResaleOpen)
            {
                OwnedShipComponent selectedItem = OwnedComponents.FirstOrDefault(item =>
                    item.InstanceId == selectedComponentResaleInstanceId && item.ShipIndex == -1);
                ComponentArtEntry selectedEntry = selectedItem == null ? null : componentArt.GetById(selectedItem.ComponentId);
                if (selectedEntry != null)
                {
                    BuildComponentReadingDossier(panel, selectedEntry, selectedItem.InstanceId);
                    return;
                }
                selectedComponentResaleInstanceId = null;
                RefreshFacilityReturnCaption();
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
            RefreshFacilityReturnCaption();
            BuildComponentShopShowroom(panel, stock, now, soldCount);
        }

        private void BuildComponentShopDetail(RectTransform panel, PortComponentShopStockItem offer)
        {
            BuildComponentDossier(panel, offer);
        }

        private void PurchaseComponentOffer(PortComponentShopStockItem offer, bool equipAfterPurchase)
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null ||
                session.Phase != CoreDemoPhase.Port || componentInventoryReadFailed || componentInventory == null ||
                (equipAfterPurchase && !CanRefitShip(session.SelectedShipIndex))) return;
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
            if (equipAfterPurchase)
            {
                PlaySfx("shipyard_equip", 0.56f);
                EquipPurchasedComponent(offer.Entry);
            }
            else
            {
                PlaySfx("trade_confirm", 0.56f);
                GrantOwnedComponent(offer.Entry);
            }
            SaveProgress();
            portNotice = "已在" + session.CurrentPort + "购入「" + offer.Entry.Name + "」，金币 -" +
                offer.Price + (equipAfterPurchase
                    ? "；已装配至" + ComponentArtCatalog.SlotName(offer.Entry.Slot) + "，原部件已移回库存。"
                    : "；已放入部件库存，可在皇家船坞装配。");
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

            BuildCardWorkshopPreview();
        }

        private void ResetCardWorkshopView()
        {
            ClearWorkshopSpecialCardReturn();
            ResetDeckBuilderPresentation();
            specialCardCollectionOpen = false;
            specialCardCollectionPage = 0;
            selectedSpecialCardIndex = -1;
            battleCardUpgradeOpen = false;
            cardWorkshopDeckBuilderOpen = false;
            selectedCardWorkshopChest = -1;
            battleCardUpgradePage = 0;
            selectedBattleCardUpgrade = -1;
            battleDeckOverviewOpen = false;
            selectedBattleDeckStage = NavalBattleStage.Preparation;
            battleCardCollectionPage = 0;
        }

        private void ResetComponentShopView()
        {
            selectedComponentShopOfferId = null;
            componentShopDisplayPage = 0;
            componentShopCategory = -1;
            componentShopShowroomOfferId = null;
            componentResaleOpen = false;
            componentResalePage = 0;
            selectedComponentResaleInstanceId = null;
            pendingComponentSaleId = null;
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

        private void BuildTreasureFacility()
        {
            RectTransform panel = CreateFixedPanel("Treasure Panel", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f), Color.clear);
            panel.GetComponent<Image>().raycastTarget = false;
            BuildTreasureFragmentBoards(panel);
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
            // the roster atlas and are keyed transparent by BuildActiveShipPreview.
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
            // Keep the ship root and its sibling order intact while clipping
            // the base hull at the shallow-water line.
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
                // Both parents use unit scale/rotation; preserve the hull's
                // position when moving it under the translated clipping mask.
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
            Image supplyPaper = CreateImage("Maintenance Supply Board Backing", supply,
                new Vector2(584f, 388f), new Vector2(0f, -6f), Color.white);
            NauticalSurfaceSkin.ApplyReadingPanel(supplyPaper);
            // The standing board retains its timber, feet and props. Its smaller
            // writing face uses a finer rim around the existing supply controls.
            supplyPaper.pixelsPerUnitMultiplier = 4f;
            supplyPaper.raycastTarget = false;

            // The enlarged paper face holds all six supplies at once. This is a
            // fixed ledger: no scrolling state, hidden ammunition rows or scroll rail.
            Text title = CreateText("Maintenance Supply Title", supply, new Vector2(510f, 30f), new Vector2(0f, 156f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, DeepSea);
            title.text = "出航补给采购";
            Text instruction = CreateText("Maintenance Supply Instruction", supply, new Vector2(510f, 20f), new Vector2(0f, 130f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, DeepSea);
            instruction.text = "水粮每次 10 单位 · 与市场共享本月价格和采购额度 · 声望采购折扣 " +
                PortMarketCatalog.FamePurchaseDiscountPercent(session.Cargo.Fame) + "%";
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
                    PortMarketCatalog.FormatPrice(PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, priceQuantity, session.Cargo.Fame)) + " 金币";

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
                lineCost.text = PortMarketCatalog.FormatPrice(PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, pendingSupplyOrder[type], session.Cargo.Fame)) + " 金";
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
            portNotice = HasRecommendedSupplyShortfall(pendingSupplyOrder, session.EffectiveCrewCount, session.HasCook)
                ? "舱容或本月库存不足，未达到完整推荐储备；请检查续航与弹药。"
                : quote.HasItems ? "推荐清单已按航程与战斗储备调整，确认后扣费。" : "当前物资已达到推荐储备。";
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
            if (session == null) return;
            if (HasCurrentDepartureCrewSelection) EnsureDepartureCrewSelection();
            // Source restoration can initialize the departure roster. Resolve
            // capabilities afterwards so standby identities and the actual
            // navigator headcount agree with the cards aboard this ship.
            RefreshSpecialCardSources();
            session.ConfigureCrewCapabilities(
                AvailableVoyageProfessionCount(0),
                IsCrewAvailableForVoyage(1),
                IsCrewAvailableForVoyage(2),
                IsCrewAvailableForVoyage(3),
                IsCrewAvailableForVoyage(4),
                IsCrewAvailableForVoyage(5));
        }

        private bool HasCurrentDepartureCrewSelection => session != null && selectedDepartureOrdinaryCrew >= 0 &&
            departureCrewShipIndex == session.SelectedShipIndex &&
            string.Equals(departureCrewPort, session.CurrentPort, StringComparison.Ordinal);

        private int AvailableVoyageProfessionCount(int profession)
        {
            if (session == null) return 0;
            if (HasCurrentDepartureCrewSelection) return SelectedDepartureProfessionCount(profession);
            int count = 0;
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                if (crew.ProfessionIndex == profession && IsCrewAvailableAtCurrentPort(crew)) count++;
            return count;
        }

        private bool IsCrewAvailableForVoyage(int profession) => AvailableVoyageProfessionCount(profession) > 0;

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

        private bool RestorePortProgression()
        {
            PlayerPrefs.DeleteKey(LegacyWitchRecruitedKey);
            MigrateLegacyCrewIdentities();
            MigrateMissingCrewLocations();
            int selectedShip = Mathf.Clamp(PlayerPrefs.GetInt(SelectedShipKey, 0), 0, CoreVoyageSession.Ships.Count - 1);
            if (selectedShip > 0 && PlayerPrefs.GetInt(ShipOwnedKey(selectedShip), 0) == 0)
            {
                selectedShip = 0;
            }
            if (!RestoreShipPorts(selectedShip)) return false;
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
            return true;
        }

        private static string FacilityKey(int index)
        {
            string[] keys = { "Warehouse", "Shipyard", "Tavern", "ComponentShop", "CardWorkshop", "Treasure", "Maintenance", "Voyage", "Market" };
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
