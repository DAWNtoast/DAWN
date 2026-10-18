using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    // Unity lifecycle entry points share one place so startup, frame updates,
    // suspension and teardown retain an explicit cross-system order.
    public sealed partial class CoreGameplayDemoController
    {
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
            tutorial = new TutorialVoyageSession();
            session.RestoreProfile(
                ProfileGoldPersistence.Load(),
                PlayerPrefs.GetString(CurrentPortKey, "断锚港"));
            string previousCoverage = PlayerPrefs.GetString(LastVoyageExplorationCoverageKey, string.Empty);
            if (float.TryParse(previousCoverage, NumberStyles.Float, CultureInfo.InvariantCulture, out float coverage) &&
                !float.IsNaN(coverage) && !float.IsInfinity(coverage))
                session.RestoreLastVoyageExplorationCoverage(coverage);
            tutorialActive = PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 0;
            art = new LegacyDemoArtCatalog();
            portArt = new PortArtCatalog();
            portMapIcons = new PortMapIconCatalog();
            battleEnvironmentArt = new BattleEnvironmentArtCatalog();
            componentArt = new ComponentArtCatalog();
            componentShopArt = new ComponentShopArtCatalog();
            cardWorkshopArt = new CardWorkshopArtCatalog();
            maintenanceArt = new MaintenanceArtCatalog();
            RestoreComponentLoadout();
            if (componentInventoryReadFailed)
            {
                ReturnToMenuAfterSuspensionFailure(ComponentInventoryReadFailureNotice);
                return;
            }
            if (!RestorePortProgression())
            {
                ReturnToMenuAfterSuspensionFailure(ShipPortReadFailureNotice);
                return;
            }
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
            if (!TryInitializeWarehouseIncome(DateTime.UtcNow, out string incomeNotice))
            {
                ProtectVoyageAfterIncomeFailure(suspendedVoyage, incomeNotice);
                return;
            }
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
            if (OpeningComicCatalog.ConsumePlaybackRequest() && suspendedVoyage == null)
                BuildOpeningComicOverlay();
            ShowWarehouseIncomeReceipt();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && profileSaveBlocked) return;
            if (Application.isPlaying)
            {
                ResumeAudioPresentation();
                ResumeBattlePresentation();
                ResumeNavigationPresentation();
                QueueUnusedArtCollection();
            }
            // Saved previews own their authored controls. Explicit editor sync
            // upgrades old seed fields without overlaying an obsolete dropdown.
            if (!Application.isPlaying && editorPreviewPersistGeneratedUi)
            {
                Canvas previewCanvas = transform.Find(EditorPreviewCanvasName)?.GetComponent<Canvas>();
                if (previewCanvas != null) FixedGameViewport.Create(previewCanvas);
            }
            if (!Application.isPlaying && !editorPreviewSuppressed && !editorPreviewPersistGeneratedUi)
            {
                RebuildEditorPreview();
            }
        }

        private void Update()
        {
            UpdatePortHeaderReadouts();
            if (!Application.isPlaying)
            {
                UpdateEditorPreviewIfNeeded();
                return;
            }

            if (UiEscapeInput.IsPressed) HandleEscapeNavigation();
            RefreshMarketMonthIfNeeded();
            EnsureAudioListener();
            UpdateBattleEntryCue();
            UpdateWorldPhaseCue();
            string desiredKey = ResolveDesiredMusicKey();
            if (desiredKey != activeMusicKey && musicCrossfade <= 0f) SetMusic(desiredKey, false);
            UpdateMusicCrossfade();
            UpdateAmbience();
            UpdateEffectVoices();
        }

        private void OnApplicationPause(bool paused)
        {
            // Sailing has already committed its costs and destination before the
            // presentation starts. Resume at that result, never replay the action.
            if (paused) SuspendNavigationPresentation();
            else if (isActiveAndEnabled) ResumeNavigationPresentation();

            if (paused && (ramComicOverlay != null || boardingTransitionOverlay != null ||
                activeBattleSchoolAttack != null || HasBattleResolutionPresentation))
            {
                ramComicResumeAfterPause = true;
                SuspendBattlePresentation();
            }
            else if (!paused && ramComicResumeAfterPause && isActiveAndEnabled)
            {
                ramComicResumeAfterPause = false;
                ResumeBattlePresentation();
            }
        }

        private void OnDisable()
        {
            DismissSchoolUnlockNotice();
            RestoreDeparturePaintedHeader();
            CloseOpeningComic();
            CloseVoyageCargo(false);
            SuspendBattlePresentation();
            SuspendNavigationPresentation();
            SuspendScreenTransition();
            SuspendUnusedArtCollection();
            CloseGameMaster();
            if (!Application.isPlaying && !editorPreviewPersistGeneratedUi)
            {
                RemoveEditorPreview();
                ReleaseGeneratedVisualResources();
            }
        }

        private void OnDestroy()
        {
            CancelBattleSchoolAttackPresentation();
            CancelBattleResolutionPresentation();
            CloseBattleDeckOverview(false);
            CloseBoardingCardChoice();
            CloseNavalCardChoice();
            RestoreDeparturePaintedHeader();
            CloseOpeningComic();
            ClearAchievementNotifications();
            ReleaseGeneratedVisualResources();
        }
    }
}
