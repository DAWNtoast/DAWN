using System;
using System.Linq;
using System.Reflection;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum CoreGameplayPreviewScreen
    {
        PortHub,
        Warehouse,
        Shipyard,
        Tavern,
        TavernCrewDetail,
        ComponentShop,
        CardWorkshop,
        Dispatch,
        TreasureMap,
        Maintenance,
        VoyageDeparture,
        Navigation,
        Encounter,
        Battle,
        Settlement,
        DeckBuilder,
        ComponentShopDetail,
        DispatchDeployment,
        ShipyardDetail,
        CardUpgrade,
        SpecialCardCollection,
        Market,
    }

    public enum CoreGameplayPreviewPort
    {
        BrokenAnchor,
        WhiteShell,
        BronzeBell,
        SaltCrown,
        TwilightTide,
        GrayGull,
    }

    public enum TreasureMapPreviewState
    {
        MapUnopened,
        FragmentsReady,
        MapDecoded,
        RewardClaim,
    }

    public sealed partial class CoreGameplayDemoController
    {
        public const string EditorPreviewCanvasName = "Core Gameplay Canvas [Editor Preview]";

        [Header("非运行状态场景预览")]
        [SerializeField] private CoreGameplayPreviewScreen editorPreviewScreen = CoreGameplayPreviewScreen.PortHub;
        [SerializeField] private CoreGameplayPreviewPort editorPreviewPort = CoreGameplayPreviewPort.BrokenAnchor;
        [SerializeField, Range(0, 2), Tooltip("航线或派遣序号；卡牌强化预览中为 0 策略、1 远程攻击、2 近战。")]
        private int editorPreviewRouteIndex;
        [SerializeField, Range(0, 5), Tooltip("船员档案中预览的职业序号。无需进入 Play 模式。")]
        private int editorPreviewCrewIndex;
        [SerializeField, Range(0, 3), Tooltip("船员档案介绍页：0 人物背景，1 航海异闻，2 职业技能，3 专属卡牌。")]
        private int editorPreviewCrewPage;
        [SerializeField, Range(0, 11), Tooltip("船坞中预览的船型；左右浏览与船只档案共用此序号。")]
        private int editorPreviewShipIndex;
        [SerializeField, Range(0, 2), Tooltip("船只档案页：0 性能，1 背景，2 配件装配。")]
        private int editorPreviewShipPage;
        [SerializeField, Tooltip("藏宝图桌的独立样本状态，不读取或写入玩家存档。")]
        private TreasureMapPreviewState editorPreviewTreasureState;
        [SerializeField, Tooltip("招募、强化与藏宝航程的独立编辑样本。不会写入玩家档案。")]
        private GameplayFlowPreviewState editorPreviewFlowState;
        [SerializeField, Tooltip("可保存编辑场景使用。启用后预览 Canvas 会作为场景内容保留，不会在编辑器生命周期中自动重建或销毁。")]
        private bool editorPreviewPersistGeneratedUi;

        private bool editorPreviewBuilding;
        private bool editorPreviewSuppressed;
        private CoreGameplayPreviewScreen renderedPreviewScreen = (CoreGameplayPreviewScreen)(-1);
        private CoreGameplayPreviewPort renderedPreviewPort = (CoreGameplayPreviewPort)(-1);
        private int renderedPreviewRouteIndex = -1;
        private int renderedPreviewCrewIndex = -1;
        private int renderedPreviewCrewPage = -1;
        private int renderedPreviewShipIndex = -1;
        private int renderedPreviewShipPage = -1;
        private TreasureMapPreviewState renderedPreviewTreasureState = (TreasureMapPreviewState)(-1);
        private GameplayFlowPreviewState renderedPreviewFlowState = (GameplayFlowPreviewState)(-1);

        public CoreGameplayPreviewScreen EditorPreviewScreen => editorPreviewScreen;
        public bool HasEditorPreview => transform.Find(EditorPreviewCanvasName) != null;
        public bool HasCompleteEditorPreview
        {
            get
            {
                Transform preview = transform.Find(EditorPreviewCanvasName);
                string expected = ExpectedPreviewObjectName(editorPreviewScreen);
                return preview != null && preview.GetComponentsInChildren<Transform>(true).Any(item => item.name == expected);
            }
        }


        public void ConfigureEditorPreview(CoreGameplayPreviewScreen screen, CoreGameplayPreviewPort port, int routeIndex)
        {
            ConfigureEditorPreview(screen, port, routeIndex, false);
        }

        public void ConfigureEditorPreview(CoreGameplayPreviewScreen screen, CoreGameplayPreviewPort port,
            int routeIndex, bool persistGeneratedUi)
        {
            shipyardReturnToDeparture = false;
            editorPreviewScreen = screen;
            editorPreviewPort = port;
            editorPreviewRouteIndex = Mathf.Clamp(routeIndex, 0, CoreVoyageSession.Routes.Count - 1);
            editorPreviewCrewIndex = 0;
            editorPreviewCrewPage = 0;
            editorPreviewShipIndex = 0;
            editorPreviewShipPage = 0;
            editorPreviewTreasureState = TreasureMapPreviewState.MapUnopened;
            editorPreviewFlowState = GameplayFlowPreviewState.None;
            editorPreviewPersistGeneratedUi = persistGeneratedUi;
            if (!Application.isPlaying && isActiveAndEnabled)
            {
                RebuildEditorPreview();
            }
        }

        public bool EditorPreviewPersistsGeneratedUi => editorPreviewPersistGeneratedUi;

        public void ConfigureEditorTreasureState(TreasureMapPreviewState state)
        {
            if (Application.isPlaying) return;
            editorPreviewScreen = CoreGameplayPreviewScreen.TreasureMap;
            editorPreviewFlowState = GameplayFlowPreviewState.None;
            editorPreviewTreasureState = state;
            if (isActiveAndEnabled) RebuildEditorPreview();
        }

        public void ConfigureEditorCrewDossier(int professionIndex, int page)
        {
            if (Application.isPlaying) return;
            editorPreviewScreen = CoreGameplayPreviewScreen.TavernCrewDetail;
            editorPreviewCrewIndex = Mathf.Clamp(professionIndex, 0, 5);
            editorPreviewCrewPage = Mathf.Clamp(page, 0, 3);
            if (isActiveAndEnabled) RebuildEditorPreview();
        }

        public void ConfigureEditorShipyard(int shipIndex, bool detail, int page = 0)
        {
            if (Application.isPlaying) return;
            shipyardReturnToDeparture = false;
            editorPreviewScreen = detail ? CoreGameplayPreviewScreen.ShipyardDetail : CoreGameplayPreviewScreen.Shipyard;
            editorPreviewShipIndex = Mathf.Clamp(shipIndex, 0, CoreVoyageSession.Ships.Count - 1);
            editorPreviewShipPage = Mathf.Clamp(page, 0, 2);
            ResetShipyardEquipmentSelection();
            if (isActiveAndEnabled) RebuildEditorPreview();
        }

        public void PrepareEditorPreviewForSceneSave()
        {
            if (Application.isPlaying || editorPreviewPersistGeneratedUi) return;
            editorPreviewSuppressed = true;
            RemoveEditorPreview();
            ReleaseGeneratedVisualResources();
        }

        [ContextMenu("刷新当前 UI 预览")]
        public void RefreshEditorPreview()
        {
            if (Application.isPlaying || !isActiveAndEnabled) return;
            editorPreviewSuppressed = false;
            RebuildEditorPreview();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && profileSaveBlocked) return;
            if (Application.isPlaying)
            {
                ResumeAudioPresentation();
                ResumeBattlePresentation();
                ResumeNavigationPresentation();
            }
            // Saved previews own their authored controls. Explicit editor sync
            // upgrades old seed fields without overlaying an obsolete dropdown.
            if (!Application.isPlaying && !editorPreviewSuppressed && !editorPreviewPersistGeneratedUi)
            {
                RebuildEditorPreview();
            }
        }

        private void OnDisable()
        {
            CloseVoyageCargo(false);
            SuspendBattlePresentation();
            SuspendNavigationPresentation();
            SuspendScreenTransition();
            CloseGameMaster();
            if (!Application.isPlaying && !editorPreviewPersistGeneratedUi)
            {
                RemoveEditorPreview();
                ReleaseGeneratedVisualResources();
            }
        }

        private void UpdateEditorPreviewIfNeeded()
        {
            if (Application.isPlaying || editorPreviewSuppressed || editorPreviewPersistGeneratedUi || !isActiveAndEnabled)
            {
                return;
            }

            if (!HasEditorPreview || renderedPreviewScreen != editorPreviewScreen || renderedPreviewPort != editorPreviewPort ||
                renderedPreviewRouteIndex != editorPreviewRouteIndex ||
                renderedPreviewFlowState != editorPreviewFlowState ||
                (editorPreviewScreen == CoreGameplayPreviewScreen.TreasureMap && renderedPreviewTreasureState != editorPreviewTreasureState) ||
                ((editorPreviewScreen == CoreGameplayPreviewScreen.Shipyard || editorPreviewScreen == CoreGameplayPreviewScreen.ShipyardDetail) &&
                 (renderedPreviewShipIndex != editorPreviewShipIndex || renderedPreviewShipPage != editorPreviewShipPage)) ||
                (editorPreviewScreen == CoreGameplayPreviewScreen.TavernCrewDetail &&
                 (renderedPreviewCrewIndex != editorPreviewCrewIndex || renderedPreviewCrewPage != editorPreviewCrewPage)))
            {
                RebuildEditorPreview();
            }
        }

        private void RebuildEditorPreview()
        {
            if (editorPreviewBuilding || editorPreviewSuppressed)
            {
                return;
            }

            editorPreviewBuilding = true;
            try
            {
                RemoveEditorPreview();
                ReleaseGeneratedVisualResources();
                InitializeEditorPreviewData();
                BuildCanvas(EditorPreviewCanvasName);
                BuildSelectedEditorPreview();
                if (!editorPreviewPersistGeneratedUi)
                    SetEditorPreviewFlags(transform.Find(EditorPreviewCanvasName));
                renderedPreviewScreen = editorPreviewScreen;
                renderedPreviewTreasureState = editorPreviewTreasureState;
                renderedPreviewFlowState = editorPreviewFlowState;
                renderedPreviewPort = editorPreviewPort;
                renderedPreviewRouteIndex = editorPreviewRouteIndex;
                renderedPreviewCrewIndex = editorPreviewCrewIndex;
                renderedPreviewCrewPage = editorPreviewCrewPage;
                renderedPreviewShipIndex = editorPreviewShipIndex;
                renderedPreviewShipPage = editorPreviewShipPage;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                RemoveEditorPreview();
                ReleaseGeneratedVisualResources();
            }
            finally
            {
                editorPreviewBuilding = false;
            }
        }

        private void InitializeEditorPreviewData()
        {
            session = new CoreVoyageSession();
            session.RestoreGameClock((CoreVoyageSession.NewCampaignStartDate(DateTime.Now) - CoreVoyageSession.GameClockOrigin).TotalHours);
            session.RestoreProfile(572, PreviewPortName(editorPreviewPort));
            tutorial = new TutorialVoyageSession();
            tutorialActive = false;
            art = new LegacyDemoArtCatalog();
            portArt = new PortArtCatalog();
            portMapIcons = new PortMapIconCatalog();
            battleEnvironmentArt = new BattleEnvironmentArtCatalog();
            componentArt = new ComponentArtCatalog();
            componentShopArt = new ComponentShopArtCatalog();
            facilityArt = new PortFacilityArtCatalog();
            cardWorkshopArt = new CardWorkshopArtCatalog();
            dispatchArt = new DispatchArtCatalog();
            maintenanceArt = new MaintenanceArtCatalog();
            interfaceFont = CreateInterfaceFont();
            seaSprite = CreateSeaSprite();
            circleSprite = CreateCircleSprite();
            InitializeEditorPreviewCardSources();
            activePortFacility = -1;
            ResetTavernDossierView();
            ResetComponentShopView();
            ResetCardWorkshopView();
            warehouseCollection.Import(string.Empty, string.Empty);
            warehouseStorageOpen = false;
            selectedWarehouseTreasureId = string.Empty;
            selectedWarehouseDisplaySlot = 0;
            if (editorPreviewScreen == CoreGameplayPreviewScreen.Warehouse)
            {
                // A deterministic art/layout fixture, never restored from or
                // committed to the player's ownership and display records.
                for (int index = 0; index < CoreWarehouseCollection.Catalog.Count; index++)
                    warehouseCollection.Grant(CoreWarehouseCollection.Catalog[index].Id);
                // Keep the original three showcase pieces in the cabinet while the
                // inventory demonstrates that the catalog can contain many more.
                for (int slot = 0; slot < Math.Min(3, CoreWarehouseCollection.DisplaySlotCount); slot++)
                {
                    warehouseCollection.TryDisplay(CoreWarehouseCollection.Catalog[slot].Id, slot);
                }
                selectedWarehouseTreasureId = CoreWarehouseCollection.TutorialTreasureId;
            }
            selectedDispatchCrew.Clear();
            pendingSupplyOrder.Clear();
            portNotice = "编辑预览样本 · 本港航海士 / 船匠与蓝色火炮，不读取或写入玩家卡牌存档。";
            courseTargetNotice = "点击海图任意位置设置自由航点。";
            InitializeTreasureEditorPreview();
        }

        private void InitializeTreasureEditorPreview()
        {
            treasureFolioOpen = false;
            treasureFolioShowCompleted = false;
            treasureDetailMapId = null;
            if (editorPreviewScreen != CoreGameplayPreviewScreen.TreasureMap) return;
            session.SelectRoute(editorPreviewRouteIndex);
            const string seed = "TREASURE-PREVIEW-WEATHERED-01";
            const string id = "preview-treasure-map-01";
            bool ownsMap = editorPreviewTreasureState == TreasureMapPreviewState.MapDecoded;
            bool completed = editorPreviewTreasureState == TreasureMapPreviewState.RewardClaim;
            session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 100,
                fragments = editorPreviewTreasureState == TreasureMapPreviewState.FragmentsReady ? 4 : 0,
                selectedMapId = ownsMap ? id : string.Empty,
                maps = ownsMap ? new[] { new CoreTreasureMapSave { id = id, seed = seed, rankId = "weathered" } } : new CoreTreasureMapSave[0],
                issuedSeeds = ownsMap || completed ? new[] { seed } : new string[0],
                completedSeeds = completed ? new[] { seed } : new string[0],
                gems = completed ? 1 : 0,
                ancientMaterials = completed ? 6 : 0,
            });
            treasureFolioOpen = ownsMap || completed;
            treasureFolioShowCompleted = completed;
            treasureDetailMapId = ownsMap ? id : null;
        }

        private void InitializeEditorPreviewCardSources()
        {
            // Preview equipment is deterministic fixture data, not the player's
            // current loadout. Never restore, migrate, equip or save a real item.
            int previewComponentSerial = 0;
            componentInventory = new OwnedShipComponentInventory(componentArt,
                () => "preview-component-" + previewComponentSerial++);
            componentInventory.EnsureLoadout(0);
            ComponentArtEntry sample = componentArt.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent installedSample = componentInventory.Grant(sample, 1f);
            componentInventory.TryEquip(0, installedSample.InstanceId);
            // The equipment page also needs visible spare pieces, without creating
            // ownership or replacing the player's inventory when opening a preview scene.
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                componentInventory.Grant(componentArt.Entries.First(item => item.Slot == slot && item.Rarity == ComponentRarity.Uncommon), 1.02f);
            RefreshComponentBonuses();
            session.ConfigureCrewCapabilities(true, true, false, false, false, false);
            string[] crewIds =
            {
                CrewNarrativeCatalog.ForPortAndProfession(session.CurrentPort, 0).Id,
                CrewNarrativeCatalog.ForPortAndProfession(session.CurrentPort, 1).Id,
            };
            session.ConfigureSpecialCardSources(crewIds, new[] { sample.Id });
        }

        private void BuildSelectedEditorPreview()
        {
            ClearChildren(contentRoot);
            if (headerTitle != null) headerTitle.text = GameClientInfo.DisplayName + " · UI 场景预览";
            string previewPortName = PreviewPortName(editorPreviewPort);
            if (headerStatus != null) headerStatus.text = previewPortName + "　|　" + PortFacilityArtCatalog.RegionalFacilityTitle(
                editorPreviewScreen == CoreGameplayPreviewScreen.Tavern ? PortFacilityArtCatalog.TavernFacilityIndex : -1,
                previewPortName,
                PreviewScreenLabel(editorPreviewScreen));

            if (BuildFlowStateEditorPreview()) return;
            int facilityIndex = FacilityIndex(editorPreviewScreen);
            if (facilityIndex >= 0)
            {
                activePortFacility = facilityIndex;
                selectedShipPreview = editorPreviewShipIndex;
                shipyardDetailOpen = editorPreviewScreen == CoreGameplayPreviewScreen.ShipyardDetail;
                shipyardDossierTab = (ShipyardDossierTab)Mathf.Clamp(editorPreviewShipPage, 0, 2);
                selectedComponentShopOfferId = null;
                cardWorkshopDeckBuilderOpen = editorPreviewScreen == CoreGameplayPreviewScreen.DeckBuilder ||
                    editorPreviewScreen == CoreGameplayPreviewScreen.CardUpgrade;
                battleCardUpgradeOpen = editorPreviewScreen == CoreGameplayPreviewScreen.CardUpgrade;
                specialCardCollectionOpen = editorPreviewScreen == CoreGameplayPreviewScreen.SpecialCardCollection;
                selectedBattleDeckStage = (NavalBattleStage)Mathf.Clamp(editorPreviewRouteIndex, 0, 2);
                selectedCardWorkshopChest = specialCardCollectionOpen ? 3 : cardWorkshopDeckBuilderOpen ? (int)selectedBattleDeckStage : -1;
                selectedDispatchMission = editorPreviewScreen == CoreGameplayPreviewScreen.DispatchDeployment
                    ? Mathf.Clamp(editorPreviewRouteIndex, 0, DispatchNames.Length - 1)
                    : -1;
                selectedDispatchDurationHours = selectedDispatchMission >= 0
                    ? DispatchDurations[selectedDispatchMission]
                    : DispatchDurations[0];
                if (editorPreviewScreen == CoreGameplayPreviewScreen.ComponentShopDetail)
                {
                    PortComponentShopStockItem previewOffer = PortComponentShopCatalog
                        .BuildStock(componentArt, session.CurrentPort, session.GameDateTime)
                        .FirstOrDefault();
                    selectedComponentShopOfferId = previewOffer == null ? null : previewOffer.Id;
                }
                if (editorPreviewScreen == CoreGameplayPreviewScreen.TavernCrewDetail)
                {
                    selectedTavernCandidate = Mathf.Clamp(editorPreviewCrewIndex, 0, TavernCrewSlotCount - 1);
                    tavernDossierCrewId = CurrentTavernCrew(selectedTavernCandidate).Id;
                    tavernDossierPage = Mathf.Clamp(editorPreviewCrewPage, 0, 3);
                }
                BuildPort();
                return;
            }

            switch (editorPreviewScreen)
            {
                case CoreGameplayPreviewScreen.PortHub:
                    BuildPortHub();
                    break;
                case CoreGameplayPreviewScreen.Navigation:
                    PreparePreviewVoyage(true);
                    BuildSailing();
                    break;
                case CoreGameplayPreviewScreen.Encounter:
                    StagePreviewEncounter(false);
                    BuildSailing();
                    if (session.Phase == CoreDemoPhase.Encounter) BuildEncounterOverlay();
                    break;
                case CoreGameplayPreviewScreen.Battle:
                    PreparePreviewBattle();
                    BuildBattleOverlay();
                    break;
                case CoreGameplayPreviewScreen.Settlement:
                    PreparePreviewVoyage();
                    session.SafeReturn();
                    // An explicitly labelled visual fixture, never applied to the session or a save.
                    BuildSettlementPresentation(SettlementPreviewSamples.Create(0, previewPortName), true);
                    break;
                default:
                    BuildPortHub();
                    break;
            }

            // Battle uses the authored regional header and scene art. Remove
            // the generic editor scaffold after the battle hierarchy is built
            // so the saved preview has the same composition as the game.
            if (editorPreviewScreen == CoreGameplayPreviewScreen.Battle)
                RemoveLegacyBattleScaffolding();
        }

        private void PreparePreviewVoyage(bool revealTerrainForArtReview = false)
        {
            session.SelectRoute(editorPreviewRouteIndex);
            session.SetSeed("EDITOR-PREVIEW-" + editorPreviewRouteIndex);
            session.StartVoyage();
            if (!revealTerrainForArtReview || session.Map == null)
            {
                return;
            }

            // The dedicated navigation preview is an art/layout workbench. Reveal
            // terrain there so artists can inspect the complete seeded result
            // without changing fog-of-war or event discovery in the real game.
            for (int index = 0; index < session.Map.ExploredCells.Length; index += 1)
            {
                session.Map.ExploredCells[index] = true;
            }
        }

        private CoreMapNode StagePreviewEncounter(bool requireBattle)
        {
            if (Application.isPlaying || !editorPreviewBuilding)
                throw new InvalidOperationException("Encounter fixtures may only stage an isolated editor preview.");

            PreparePreviewVoyage();
            CoreMapNode sample = session.Map.Nodes.FirstOrDefault(node =>
                !node.IsBoss && node.Encounter != null &&
                (!requireBattle || node.Encounter.Choices.Any(choice => choice.StartsBattle)))
                ?? session.Map.Nodes.FirstOrDefault(node => node.IsBoss && node.Encounter != null);
            if (sample == null)
                throw new InvalidOperationException("The preview chart contains no suitable encounter.");

            // A layout workbench stages an existing encounter rather than sailing
            // until random events, prerequisites or provisions happen to allow it.
            // Like the treasure editor fixture, this only mutates the fresh preview
            // session; live ResolveChoice eligibility and rewards are unchanged.
            SetFlowPreviewValue("Position", sample.Position);
            SetFlowPreviewValue("ActiveEncounter", sample.Encounter);
            SetFlowPreviewValue("Phase", CoreDemoPhase.Encounter);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, sample);
            sample.Discovered = true;
            typeof(CoreVoyageSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { sample.Position });
            return sample;
        }

        private void PreparePreviewBattle()
        {
            CoreMapNode sample = StagePreviewEncounter(true);
            // Use the real battle constructor so profile, decks and opening hand
            // match the selected route and preview loadout, without fake HUD data.
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { sample.Encounter, sample.IsBoss });
            if (session.Phase != CoreDemoPhase.Battle || session.Battle == null || session.Battle.Hand.Count == 0)
                throw new InvalidOperationException("The editor battle fixture did not construct a playable battle state.");
        }

        private void RemoveEditorPreview()
        {
            Transform preview = transform.Find(EditorPreviewCanvasName);
            if (preview == null)
            {
                return;
            }

            preview.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(preview.gameObject);
            }
            else
            {
                DestroyImmediate(preview.gameObject);
            }
        }

        private void ReleaseGeneratedVisualResources()
        {
            if (portHeaderArt != null) portHeaderArt.Dispose();
            portHeaderArt = null;
            DestroyGeneratedVisual(seaSprite);
            DestroyGeneratedVisual(circleSprite);
            DestroyGeneratedVisual(seaTexture);
            DestroyGeneratedVisual(circleTexture);
            seaSprite = null;
            circleSprite = null;
            seaTexture = null;
            circleTexture = null;
        }

        private static void DestroyGeneratedVisual(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private static void SetEditorPreviewFlags(Transform root)
        {
            if (root == null) return;
            root.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            for (int index = 0; index < root.childCount; index += 1)
            {
                SetEditorPreviewFlags(root.GetChild(index));
            }
        }

        private static int FacilityIndex(CoreGameplayPreviewScreen screen)
        {
            switch (screen)
            {
                case CoreGameplayPreviewScreen.Warehouse: return 0;
                case CoreGameplayPreviewScreen.Shipyard:
                case CoreGameplayPreviewScreen.ShipyardDetail: return 1;
                case CoreGameplayPreviewScreen.Tavern:
                case CoreGameplayPreviewScreen.TavernCrewDetail: return 2;
                case CoreGameplayPreviewScreen.ComponentShop:
                case CoreGameplayPreviewScreen.ComponentShopDetail: return 3;
                case CoreGameplayPreviewScreen.CardWorkshop: return 4;
                case CoreGameplayPreviewScreen.DeckBuilder: return 4;
                case CoreGameplayPreviewScreen.CardUpgrade: return 4;
                case CoreGameplayPreviewScreen.SpecialCardCollection: return 4;
                case CoreGameplayPreviewScreen.Dispatch:
                case CoreGameplayPreviewScreen.DispatchDeployment: return 5;
                case CoreGameplayPreviewScreen.TreasureMap: return 6;
                case CoreGameplayPreviewScreen.Maintenance: return 7;
                case CoreGameplayPreviewScreen.VoyageDeparture: return 8;
                case CoreGameplayPreviewScreen.Market: return 9;
                default: return -1;
            }
        }

        private static string PreviewPortName(CoreGameplayPreviewPort port)
        {
            switch (port)
            {
                case CoreGameplayPreviewPort.WhiteShell: return "白贝港";
                case CoreGameplayPreviewPort.BronzeBell: return "铜钟港";
                case CoreGameplayPreviewPort.SaltCrown: return "盐冠港";
                case CoreGameplayPreviewPort.TwilightTide: return "暮潮港";
                case CoreGameplayPreviewPort.GrayGull: return "灰鸥港";
                default: return "断锚港";
            }
        }

        private static string PreviewScreenLabel(CoreGameplayPreviewScreen screen)
        {
            switch (screen)
            {
                case CoreGameplayPreviewScreen.PortHub: return "港口总览";
                case CoreGameplayPreviewScreen.Warehouse: return "船长收藏室";
                case CoreGameplayPreviewScreen.Shipyard: return "皇家船坞";
                case CoreGameplayPreviewScreen.ShipyardDetail: return "船只档案";
                case CoreGameplayPreviewScreen.Tavern: return "灰鸥酒馆";
                case CoreGameplayPreviewScreen.TavernCrewDetail: return "特殊船员详情";
                case CoreGameplayPreviewScreen.ComponentShop: return "部件商店";
                case CoreGameplayPreviewScreen.ComponentShopDetail: return "部件详情";
                case CoreGameplayPreviewScreen.CardWorkshop: return "卡组工坊";
                case CoreGameplayPreviewScreen.DeckBuilder: return "卡组构筑";
                case CoreGameplayPreviewScreen.CardUpgrade: return "基础卡牌强化";
                case CoreGameplayPreviewScreen.SpecialCardCollection: return "特殊卡牌收藏";
                case CoreGameplayPreviewScreen.Dispatch: return "远航派遣台";
                case CoreGameplayPreviewScreen.DispatchDeployment: return "派遣部署";
                case CoreGameplayPreviewScreen.TreasureMap: return "藏宝图桌";
                case CoreGameplayPreviewScreen.Maintenance: return "港口投资建设";
                case CoreGameplayPreviewScreen.VoyageDeparture: return "出航码头";
                case CoreGameplayPreviewScreen.Market: return "港口市场";
                case CoreGameplayPreviewScreen.Navigation: return "航海探索";
                case CoreGameplayPreviewScreen.Encounter: return "航海事件";
                case CoreGameplayPreviewScreen.Battle: return "海战界面";
                case CoreGameplayPreviewScreen.Settlement: return "航行结算";
                default: return screen.ToString();
            }
        }

        private static string ExpectedPreviewObjectName(CoreGameplayPreviewScreen screen)
        {
            switch (screen)
            {
                case CoreGameplayPreviewScreen.PortHub: return "Port Hub";
                case CoreGameplayPreviewScreen.Warehouse: return "Warehouse Panel";
                case CoreGameplayPreviewScreen.Shipyard: return "Shipyard Panel";
                case CoreGameplayPreviewScreen.ShipyardDetail: return "Shipyard Detail";
                case CoreGameplayPreviewScreen.Tavern: return "Tavern Crew Stage";
                case CoreGameplayPreviewScreen.TavernCrewDetail: return "Tavern Crew Detail";
                case CoreGameplayPreviewScreen.ComponentShop: return "Component Shop Panel";
                case CoreGameplayPreviewScreen.ComponentShopDetail: return "Component Shop Detail";
                case CoreGameplayPreviewScreen.CardWorkshop: return "Card Workshop Chest Stage";
                case CoreGameplayPreviewScreen.DeckBuilder: return "Deck Builder Panel";
                case CoreGameplayPreviewScreen.CardUpgrade: return "Battle Card Upgrade Panel";
                case CoreGameplayPreviewScreen.SpecialCardCollection: return "Special Card Collection Panel";
                case CoreGameplayPreviewScreen.Dispatch: return "Dispatch Panel";
                case CoreGameplayPreviewScreen.DispatchDeployment: return "Dispatch Deployment Panel";
                case CoreGameplayPreviewScreen.TreasureMap: return "Treasure Panel";
                case CoreGameplayPreviewScreen.Maintenance: return "Port Investment Panel";
                case CoreGameplayPreviewScreen.VoyageDeparture: return "Port Briefing";
                case CoreGameplayPreviewScreen.Market: return "Market Panel";
                case CoreGameplayPreviewScreen.Navigation: return "Navigation Chart";
                case CoreGameplayPreviewScreen.Encounter: return "Encounter Card";
                case CoreGameplayPreviewScreen.Battle: return "Battle Card";
                case CoreGameplayPreviewScreen.Settlement: return "Settlement Card";
                default: return string.Empty;
            }
        }
    }
}

