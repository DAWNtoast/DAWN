using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    // Retain the retired Distance ordinal so suspended older lessons remain readable.
    public enum GuidedTutorialNavigationInputStep { Heading, Distance, Sail }

    public sealed partial class CoreGameplayDemoController
    {
        // Port checkpoints track committed tutorial milestones independently of
        // explicit voyage snapshots. A suspended voyage resumes its lesson in place;
        // without a snapshot, reload retries from the last port without duplicating rewards.
        public const string GuidedTutorialCheckpointKey = "ShatteredSail.GuidedTutorial.Checkpoint.v2";
        // Legacy key retained only so new-game/reset code can clear old saves.
        public const string GuidedTutorialDeckEditedKey = "ShatteredSail.GuidedTutorial.DeckEdited.v2";
        public const string GuidedTutorialComponentRewardKey = "ShatteredSail.GuidedTutorial.ComponentReward.v2";
        public const string GuidedTutorialRecoveryKey = "ShatteredSail.GuidedTutorial.Recovery.v2";
        // Marks the completed navigator-event voyage; value -1 preserves an old
        // direct-boss checkpoint while value 1 unlocks the new boss departure.
        public const string GuidedTutorialNavigatorEventKey = "ShatteredSail.GuidedTutorial.NavigatorEvent.v1";
        // Checkpoint 5 meant complete before the warehouse lesson was added.
        // A separate pending marker extends new saves without re-locking alumni.
        public const string GuidedTutorialWarehousePendingKey = "ShatteredSail.GuidedTutorial.WarehousePending.v1";
        public const string GuidedTutorialEquipmentPendingKey = "ShatteredSail.GuidedTutorial.EquipmentPending.v1";
        public const string DrawnSeaChartKey = "ShatteredSail.DrawnSeaChart.v1";
        public const string GuidedTutorialRewardComponentId = "bow_sawtooth_drift";

        private GuidedTutorialStep guidedTutorialStep;
        private int guidedTutorialCheckpoint;
        private bool guidedTutorialSupplyReturnRequired;
        private bool guidedDepartureDestinationSelected;
        private bool guidedDepartureRouteSelected;
        private DrawnSeaChartRecord pendingDrawnChart;
        private GuidedTutorialNavigationInputStep guidedTutorialNavigationInputStep;
        private CoreVoyageMap guidedNavigationMap;
        private CoreDemoPhase guidedNavigationPreviousPhase;
        private float guidedNavigationDistanceNm = -1f;
        private int guidedNavigationCompletedEncounters = -1;
        private bool guidedNavigationInitialized;
        public GuidedTutorialStep CurrentGuidedTutorialStep => guidedTutorialStep;
        public GuidedTutorialNavigationInputStep CurrentGuidedTutorialNavigationInputStep => guidedTutorialNavigationInputStep;
        private string GuidedTutorialNavigationTargetControl =>
            guidedTutorialNavigationInputStep == GuidedTutorialNavigationInputStep.Heading ? "Heading East" : "Execute Sail";
        public bool GuidedTutorialRecoveryActive => tutorialActive && PlayerPrefs.GetInt(GuidedTutorialRecoveryKey, 0) == 1;
        public bool GuidedTutorialWarehousePending => tutorialActive && PlayerPrefs.GetInt(GuidedTutorialWarehousePendingKey, 0) == 1;
        public bool GuidedTutorialEquipmentPending => tutorialActive && PlayerPrefs.GetInt(GuidedTutorialEquipmentPendingKey, 0) == 1;
        private bool GuidedTutorialNeedsSupplyReturn => guidedTutorialSupplyReturnRequired;

        [Serializable]
        private sealed class DrawnSeaChartRecord
        {
            public int version = 1;
            public int mapGeometryVersion;
            public int routeIndex;
            public string routeName;
            public string seed;
            public string hash;
            public string port;
            public string departurePort;
            public string destinationPort;
            public float coverage;
            public bool tutorialWaters;
            public bool[] exploredCells;
            public bool[] chartExploredCells;
            public int[] revealedLocationIds;
        }

        private void InitializeGuidedTutorial()
        {
            guidedTutorialStep = GuidedTutorialStep.Complete;
            if (!tutorialActive) return;
            guidedTutorialCheckpoint = Mathf.Clamp(PlayerPrefs.GetInt(GuidedTutorialCheckpointKey, 0), 0, 5);
            // Old saves paused at mandatory deck editing (3) or the third pier (4).
            // Only the new flow writes the navigator marker, so legacy saves keep
            // their direct-boss recovery behavior without replaying the event voyage.
            if ((guidedTutorialCheckpoint == 3 || guidedTutorialCheckpoint == 4) &&
                PlayerPrefs.GetInt(GuidedTutorialNavigatorEventKey, 0) != 1)
            {
                SetGuidedTutorialCheckpoint(2);
                PlayerPrefs.SetInt(GuidedTutorialNavigatorEventKey, -1);
                PlayerPrefs.Save();
            }
            if (guidedTutorialCheckpoint == 5 && !GuidedTutorialWarehousePending && !GuidedTutorialEquipmentPending)
            {
                tutorialActive = false;
                PlayerPrefs.SetInt(TutorialCompletedKey, 1);
                PlayerPrefs.Save();
                return;
            }
            ResetGuidedTutorialPortStep();
        }

        private void ResetGuidedTutorialPortStep()
        {
            selectedTavernCandidate = -1;
            guidedDepartureDestinationSelected = false;
            guidedDepartureRouteSelected = false;
            ResetCardWorkshopView();
            if (GuidedTutorialWarehousePending)
            {
                // The treasure was committed with the final port settlement;
                // reloading resumes the display lesson, not the boss reward.
                selectedWarehouseTreasureId = string.Empty;
                warehouseStorageOpen = false;
                selectedWarehouseDisplaySlot = 0;
                activePortFacility = -1;
                guidedTutorialStep = GuidedTutorialStep.Warehouse;
                return;
            }
            if (GuidedTutorialEquipmentPending)
            {
                activePortFacility = -1;
                selectedShipPreview = session.SelectedShipIndex;
                shipyardDetailOpen = false;
                selectedShipyardComponentInstanceId = null;
                guidedTutorialStep = GuidedTutorialStep.Shipyard;
                return;
            }
            // Every port checkpoint resumes at the harbor overview. The first
            // departure lesson guides the player to the pier through its real hotspot.
            activePortFacility = -1;
            switch (guidedTutorialCheckpoint)
            {
                case 0: session.SelectRoute(0); guidedTutorialStep = GuidedTutorialStep.FirstDeparture; break;
                case 1: guidedTutorialStep = GuidedTutorialStep.Tavern; break;
                case 2: guidedTutorialStep = GuidedTutorialStep.SecondPier; break;
                case 3: guidedTutorialStep = GuidedTutorialStep.ThirdPier; break;
                default: guidedTutorialStep = GuidedTutorialStep.SecondPier; break;
            }
        }

        private void SetGuidedTutorialCheckpoint(int value)
        {
            guidedTutorialCheckpoint = value;
            PlayerPrefs.SetInt(GuidedTutorialCheckpointKey, value);
            PlayerPrefs.Save();
        }

        private void UpdateGuidedTutorialProgress()
        {
            guidedTutorialSupplyReturnRequired = false;
            if (!tutorialActive || editorPreviewBuilding || session == null) return;
            SynchronizeGuidedNavigationSegment();
            if (session.Phase == CoreDemoPhase.Port)
            {
                if (GuidedTutorialWarehousePending)
                {
                    guidedTutorialStep = activePortFacility != 0 ? GuidedTutorialStep.Warehouse :
                        selectedWarehouseTreasureId != CoreWarehouseCollection.TutorialTreasureId
                            ? GuidedTutorialStep.SelectTreasure : GuidedTutorialStep.DisplayTreasure;
                    return;
                }
                if (GuidedTutorialEquipmentPending)
                {
                    if (activePortFacility != 1 || !shipyardDetailOpen || selectedShipPreview != session.SelectedShipIndex)
                        guidedTutorialStep = GuidedTutorialStep.Shipyard;
                    else if (shipyardDossierTab != ShipyardDossierTab.Equipment)
                        guidedTutorialStep = GuidedTutorialStep.ShipEquipment;
                    else
                    {
                        OwnedShipComponent reward = GuidedTutorialOwnedReward();
                        // Older, unfinished display lessons already fitted the reward.
                        // Teach a real remove-and-refit action without issuing a duplicate.
                        guidedTutorialStep = reward != null && reward.ShipIndex == session.SelectedShipIndex
                            ? GuidedTutorialStep.UnequipRewardComponent
                            : reward != null && selectedShipyardComponentInstanceId == reward.InstanceId
                                ? GuidedTutorialStep.EquipRewardComponent : GuidedTutorialStep.SelectRewardComponent;
                    }
                    return;
                }
                if (guidedTutorialCheckpoint == 1)
                {
                    if (session.HasNavigator)
                    {
                        SetGuidedTutorialCheckpoint(2);
                        guidedTutorialStep = GuidedTutorialStep.SecondPier;
                    }
                    else guidedTutorialStep = activePortFacility == 2 ? GuidedTutorialStep.RecruitNavigator : GuidedTutorialStep.Tavern;
                }
                if (guidedTutorialCheckpoint == 2)
                    guidedTutorialStep = activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex ? GuidedTutorialStep.SecondPier :
                        guidedTutorialStep == GuidedTutorialStep.SecondDeparture
                            ? GuidedTutorialStep.SecondDeparture : GuidedTutorialStep.SecondRoute;
                if (guidedTutorialCheckpoint == 3)
                    guidedTutorialStep = activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex ? GuidedTutorialStep.ThirdPier :
                        guidedTutorialStep == GuidedTutorialStep.ThirdDeparture
                            ? GuidedTutorialStep.ThirdDeparture : GuidedTutorialStep.ThirdRoute;
                return;
            }

            int leg = session.GuidedTutorialLeg;
            if (session.Phase == CoreDemoPhase.Settlement)
            {
                guidedTutorialStep = session.GuidedTutorialBossDefeated && session.Settlement.Outcome != VoyageOutcome.Failure
                    ? pendingDrawnChart == null ? GuidedTutorialStep.DrawChart : GuidedTutorialStep.ThirdSettlement
                    : leg == 1 ? GuidedTutorialStep.FirstSettlement
                    : leg == CoreGuidedTutorialContent.NavigatorVoyageLeg ? GuidedTutorialStep.SecondSettlement
                    : GuidedTutorialStep.ThirdSettlement;
                return;
            }
            if (session.Phase == CoreDemoPhase.Battle)
            {
                guidedTutorialStep = session.ActiveEncounter != null && session.ActiveEncounter.Id == CoreGuidedTutorialContent.AmbushEventId
                    ? GuidedTutorialStep.SecondRetreat : GuidedTutorialStep.TutorialBoss;
                return;
            }
            if (session.Phase == CoreDemoPhase.Encounter)
            {
                switch (session.ActiveEncounter.Id)
                {
                    case "tutorial_first_event": guidedTutorialStep = GuidedTutorialStep.FirstMandatory; break;
                    case "tutorial_optional_event": guidedTutorialStep = GuidedTutorialStep.FirstOptional; break;
                    case "tutorial_crew_requirement_event": guidedTutorialStep = GuidedTutorialStep.FirstCrewEvent; break;
                    case CoreGuidedTutorialContent.NavigatorEventId: guidedTutorialStep = GuidedTutorialStep.SecondCrewEvent; break;
                    case CoreGuidedTutorialContent.RandomEventId: guidedTutorialStep = GuidedTutorialStep.SecondRandom; break;
                    case CoreGuidedTutorialContent.AmbushEventId: guidedTutorialStep = GuidedTutorialStep.SecondAmbush; break;
                    case CoreGuidedTutorialContent.BossEventId: guidedTutorialStep = GuidedTutorialStep.TutorialBoss; break;
                }
                return;
            }
            if (session.Phase != CoreDemoPhase.Sailing) return;
            SailingForecast forecast = session.GetForecast();
            guidedTutorialSupplyReturnRequired = forecast == null || forecast.Blocked ||
                session.Cargo.Water < forecast.WaterCost || session.Cargo.Food < forecast.FoodCost;
            int completed = session.GuidedTutorialCompletedEncounters;
            if (leg == 1)
                guidedTutorialStep = completed >= 3 ? GuidedTutorialStep.FirstReturn : completed == 2 ? GuidedTutorialStep.FirstCrewEvent :
                    completed == 1 ? GuidedTutorialStep.FirstOptional : GuidedTutorialStep.FirstSail;
            else if (leg == CoreGuidedTutorialContent.NavigatorVoyageLeg)
                guidedTutorialStep = session.GuidedTutorialBossDefeated ? GuidedTutorialStep.ThirdReturn : GuidedTutorialStep.SecondSail;
            else if (leg == CoreGuidedTutorialContent.BossVoyageLeg)
                guidedTutorialStep = session.GuidedTutorialBossDefeated ? GuidedTutorialStep.ThirdReturn : GuidedTutorialStep.ThirdSail;
        }

        private void SynchronizeGuidedNavigationSegment()
        {
            if (session.Phase == CoreDemoPhase.Sailing && (!guidedNavigationInitialized ||
                guidedNavigationMap != session.Map || guidedNavigationPreviousPhase != CoreDemoPhase.Sailing ||
                !Mathf.Approximately(guidedNavigationDistanceNm, session.TotalDistanceNm) ||
                guidedNavigationCompletedEncounters != session.GuidedTutorialCompletedEncounters))
            {
                guidedTutorialNavigationInputStep = GuidedTutorialNavigationInputStep.Heading;
            }
            if (session.Phase == CoreDemoPhase.Sailing)
            {
                // The lesson owns its fixed route distance. Old suspended
                // sliders may contain an intermediate value; normalize it before
                // forecast/supply checks and continue directly to execution.
                session.SetDistance(CoreGuidedTutorialContent.PlannedDistanceNm);
                if (guidedTutorialNavigationInputStep == GuidedTutorialNavigationInputStep.Distance)
                    guidedTutorialNavigationInputStep = GuidedTutorialNavigationInputStep.Sail;
            }
            guidedNavigationInitialized = true;
            guidedNavigationMap = session.Map;
            guidedNavigationPreviousPhase = session.Phase;
            guidedNavigationDistanceNm = session.TotalDistanceNm;
            guidedNavigationCompletedEncounters = session.GuidedTutorialCompletedEncounters;
        }

        private bool CanApplyGuidedNavigationInput(Selectable control, GuidedTutorialNavigationInputStep input)
        {
            if (control == null || !control.isActiveAndEnabled || !control.IsInteractable() ||
                navigationMotionPlaying || session == null || session.Phase != CoreDemoPhase.Sailing) return false;
            return !tutorialActive || (input != GuidedTutorialNavigationInputStep.Distance && GuidedIsSailingTarget(guidedTutorialStep) &&
                !GuidedTutorialNeedsSupplyReturn && guidedTutorialNavigationInputStep == input);
        }

        private void OnGuidedTutorialRouteSelected(int index)
        {
            if (!tutorialActive || index != 0 || session == null || session.Phase != CoreDemoPhase.Port || activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex) return;
            guidedDepartureRouteSelected = true;
            if (guidedTutorialCheckpoint == 2)
                guidedTutorialStep = GuidedTutorialStep.SecondDeparture;
            if (guidedTutorialCheckpoint == 3)
                guidedTutorialStep = GuidedTutorialStep.ThirdDeparture;
        }

        private void OnGuidedTutorialDestinationSelected()
        {
            if (!tutorialActive || session == null || session.Phase != CoreDemoPhase.Port || activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex) return;
            if (guidedTutorialStep != GuidedTutorialStep.FirstDeparture &&
                guidedTutorialStep != GuidedTutorialStep.SecondRoute &&
                guidedTutorialStep != GuidedTutorialStep.SecondDeparture &&
                guidedTutorialStep != GuidedTutorialStep.ThirdRoute &&
                guidedTutorialStep != GuidedTutorialStep.ThirdDeparture) return;
            guidedDepartureDestinationSelected = true;
            if (guidedTutorialCheckpoint == 2 && guidedDepartureRouteSelected)
                guidedTutorialStep = GuidedTutorialStep.SecondDeparture;
            if (guidedTutorialCheckpoint == 3 && guidedDepartureRouteSelected)
                guidedTutorialStep = GuidedTutorialStep.ThirdDeparture;
        }

        private OwnedShipComponent GuidedTutorialOwnedReward()
        {
            foreach (OwnedShipComponent item in OwnedComponents)
                if (item.ComponentId == GuidedTutorialRewardComponentId &&
                    (item.ShipIndex < 0 || item.ShipIndex == session.SelectedShipIndex)) return item;
            return null;
        }

        // Called only after the ordinary equipment action has succeeded. Merely
        // opening a tab, choosing inventory, or restoring a save cannot graduate.
        private void OnGuidedTutorialComponentEquipped()
        {
            if (!tutorialActive || !GuidedTutorialEquipmentPending || GuidedTutorialWarehousePending ||
                session == null || session.Phase != CoreDemoPhase.Port || activePortFacility != 1 ||
                !shipyardDetailOpen || selectedShipPreview != session.SelectedShipIndex ||
                shipyardDossierTab != ShipyardDossierTab.Equipment ||
                guidedTutorialStep != GuidedTutorialStep.EquipRewardComponent) return;
            OwnedShipComponent reward = GuidedTutorialOwnedReward();
            if (reward == null || reward.ShipIndex != session.SelectedShipIndex ||
                selectedShipyardComponentInstanceId != reward.InstanceId) return;

            guidedTutorialCheckpoint = 5;
            guidedTutorialStep = GuidedTutorialStep.Complete;
            tutorialActive = false;
            PlayerPrefs.DeleteKey(GuidedTutorialEquipmentPendingKey);
            PlayerPrefs.SetInt(GuidedTutorialCheckpointKey, guidedTutorialCheckpoint);
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(TutorialRewardGrantedKey, 1);
            session.ConfigureGuidedTutorialVoyage(0);
            portNotice = "引导完成！罗盘已陈列，白色部件「漂木锯齿首」已亲自装上旗舰，海图已收录。现在可以自由探索。";
            SaveProgress();
        }

        private bool PrepareGuidedTutorialDeparture()
        {
            pendingDrawnChart = null;
            if (!tutorialActive) return session.ConfigureGuidedTutorialVoyage(0);
            if (session.Phase != CoreDemoPhase.Port ||
                activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex ||
                (guidedTutorialStep != GuidedTutorialStep.FirstDeparture &&
                 guidedTutorialStep != GuidedTutorialStep.SecondDeparture &&
                 guidedTutorialStep != GuidedTutorialStep.ThirdDeparture)) return false;
            // Keep the lesson forgiving for existing saves and scripted flows: the
            // highlighted controls teach the new order, while a direct departure
            // still receives the default valid destination and first chart.
            if (!guidedDepartureDestinationSelected)
            {
                session.SelectDestinationPort(session.SelectedDestinationPort);
                guidedDepartureDestinationSelected = true;
            }
            if (!guidedDepartureRouteSelected)
            {
                session.SelectRoute(0);
                guidedDepartureRouteSelected = true;
            }
            int leg = guidedTutorialCheckpoint == 0 ? 1 :
                guidedTutorialCheckpoint == 2 ? (PlayerPrefs.GetInt(GuidedTutorialNavigatorEventKey, 0) == -1
                    ? CoreGuidedTutorialContent.BossVoyageLeg : CoreGuidedTutorialContent.NavigatorVoyageLeg) :
                guidedTutorialCheckpoint == 3 ? CoreGuidedTutorialContent.BossVoyageLeg : 0;
            if (leg <= 0 || (leg > 1 && !session.HasNavigator)) return false;
            if (!session.ConfigureGuidedTutorialVoyage(leg)) return false;
            guidedNavigationInitialized = false;
            session.SelectRoute(0);
            PlayerPrefs.DeleteKey(GuidedTutorialRecoveryKey);
            SaveProgress(); // Snapshot before the ordinary departure cash transfer and consumption.
            return true;
        }

        private void BuildGuidedTutorialActions()
        {
            if (!tutorialActive || editorPreviewBuilding) return;
            if (session.GuidedTutorialLeg >= CoreGuidedTutorialContent.NavigatorVoyageLeg && session.GuidedTutorialBossDefeated &&
                (session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Settlement))
            {
                Transform lookout = session.Phase == CoreDemoPhase.Sailing ? FindActiveGuidedContent("Forward View Panel") : null;
                Transform settlement = session.Phase == CoreDemoPhase.Settlement ? FindActiveGuidedContent("Settlement Card") : null;
                Transform parent = lookout != null ? lookout : settlement == null ? null : settlement.Find("Settlement Hero");
                if (parent == null) return;
                // Sailing leaves the journal and ration-duration footer fully
                // visible. Settlement stacks this card above the fragment/XP
                // manifest instead of putting two rewards in the same slot.
                BuildGuidedTutorialRewardPlaque(parent, lookout != null ? new Vector2(0, -164) : new Vector2(0, -55));
            }
        }

        private void BuildGuidedTutorialRewardPlaque(Transform parent, Vector2 position)
        {
            ComponentArtEntry reward = componentArt.GetById(GuidedTutorialRewardComponentId);
            RectTransform plaque = CreateFixedPanel("Guided Tutorial Component Reward", parent, new Vector2(630, 76), position, PanelRaised);
            plaque.GetComponent<Image>().raycastTarget = false;
            if (reward != null) CreateRawImage("Guided Tutorial Reward Art", plaque, new Vector2(84, 66), new Vector2(-257, 0), reward.Texture, new Rect(0, 0, 1, 1)).raycastTarget = false;
            Text label = CreateText("Guided Tutorial Reward Text", plaque, new Vector2(514, 70), new Vector2(50, 0), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            label.text = "老船长遗物 · 白色品质部件 ＋ 贵重藏品\n漂木锯齿首 ＋ 破浪者的罗盘 · 归港后入库";
            label.raycastTarget = false;
        }

        private Transform FindActiveGuidedContent(string name)
        {
            // ClearChildren deactivates old screens before Unity's deferred Destroy.
            // Transform.Find alone can still return the retired view in this frame.
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child.name == name && child.gameObject.activeSelf) return child;
            }
            return null;
        }

        private void RetreatCurrentBattle()
        {
            if (battleAnimationPlaying || !session.CanRetreatBattle) return;
            if (tutorialActive && guidedTutorialStep != GuidedTutorialStep.SecondRetreat) return;
            if (!session.RetreatFromBattle()) return;
            Render();
        }

        // A normal settlement feature, shared by the tutorial and free voyages.
        private void DrawCurrentSeaChart()
        {
            if (session.Phase != CoreDemoPhase.Settlement || session.Map == null || !session.CanDrawSeaChart ||
                session.Settlement.Outcome == VoyageOutcome.Failure || pendingDrawnChart != null || session.IsTreasureVoyage) return;
            var locations = new List<int>();
            foreach (CoreMapNode node in session.Map.Nodes)
                if (node.IsChartVisible) locations.Add(node.Id);
            string chartSeed = session.CurrentChartSeed;
            if (!CoreVoyageSession.TryParseChartSeed(chartSeed, out _) &&
                !session.TryImportLegacyChart(session.Map.Seed, session.SelectedRouteIndex, session.Map.DepartureName,
                    session.Map.DestinationName, session.Map.Hash, session.GuidedTutorialLeg != 0, out chartSeed,
                    session.Map.GeometryVersion)) return;
            pendingDrawnChart = new DrawnSeaChartRecord
            {
                routeIndex = session.SelectedRouteIndex, routeName = session.Map.Route.Name,
                mapGeometryVersion = session.Map.GeometryVersion,
                seed = chartSeed, hash = session.Map.Hash.ToString("X8"), port = session.CurrentPort,
                departurePort = session.Map.DepartureName, destinationPort = session.Map.DestinationName,
                coverage = session.Settlement.Coverage, tutorialWaters = CoreVoyageSession.TryParseChartSeed(chartSeed, out CoreSeaChartDefinition chartDefinition) && chartDefinition.TutorialWaters,
                exploredCells = (bool[])session.Map.ExploredCells.Clone(), revealedLocationIds = locations.ToArray(),
                chartExploredCells = session.Map.CaptureChartExploration(),
            };
            PlaySfx("chart_draw", 0.46f);
            RefreshSettlementChartState();
        }

        private void ConfirmVoyageSettlement()
        {
            if (profileSaveBlocked || session.Phase != CoreDemoPhase.Settlement) return;
            bool success = session.Settlement.Outcome != VoyageOutcome.Failure;
            int leg = session.GuidedTutorialLeg;
            bool bossLessonComplete = tutorialActive && success && leg >= CoreGuidedTutorialContent.NavigatorVoyageLeg &&
                session.GuidedTutorialBossDefeated;
            bool legacyNavigatorComplete = tutorialActive && success && leg == CoreGuidedTutorialContent.NavigatorVoyageLeg &&
                !session.UsesCombinedGuidedTutorialVoyage &&
                session.GuidedTutorialCompletedEncounterIds.Contains(CoreGuidedTutorialContent.NavigatorEventId);
            bool lessonComplete = bossLessonComplete || legacyNavigatorComplete ||
                tutorialActive && success && leg == 1 && session.GuidedTutorialCompletedEncounters >= 3;
            if (bossLessonComplete && pendingDrawnChart == null) return;
            VoyageSettlement settledResult = session.Settlement;
            bool voyageThreeEventsComplete = CoreAchievementCatalog.MeetsVoyageThreeEventsRequirement(
                settledResult, tutorialActive || leg > 0, session.IsTreasureVoyage);
            bool voyageHalfChartComplete = CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(
                settledResult, tutorialActive || leg > 0, session.IsTreasureVoyage);
            bool voyageEventArrivalComplete = CoreAchievementCatalog.MeetsVoyageEventArrivalRequirement(
                settledResult, tutorialActive || leg > 0, session.IsTreasureVoyage);
            bool returnedWithDamage = session.Hull < session.MaxHull;
            bool navigatorVoyageComplete = success && session.HasNavigator;
            session.ReturnToPortAfterSettlement();
            if (session.Phase != CoreDemoPhase.Port) return;
            if (Application.isPlaying && !editorPreviewBuilding)
            {
                if (returnedWithDamage) AchievementCatalog.RecordDamagedReturn();
                if (navigatorVoyageComplete) AchievementCatalog.RecordNavigatorVoyage();
            }
            if (lessonComplete) PlaySfx("milestone_unlock", 0.58f);
            string storageNotice = StoreSettledCargoIfRequested(settledResult);
            // Crew locations, cargo, chart ownership and rewards belong to one
            // confirmed return and are flushed together by SaveProgress below.
            CommitCrewLocationsAfterReturnInternal(false);
            if (pendingDrawnChart != null)
            {
                PlayerPrefs.SetString(DrawnSeaChartKey, JsonUtility.ToJson(pendingDrawnChart));
                RecordConfirmedSeaChart(pendingDrawnChart);
                pendingDrawnChart = null;
            }
            if (lessonComplete)
            {
                if (bossLessonComplete)
                {
                    if (PlayerPrefs.GetInt(GuidedTutorialComponentRewardKey, 0) == 0)
                    {
                        ComponentArtEntry reward = componentArt.GetById(GuidedTutorialRewardComponentId);
                        // Inventory and the one-off grant marker are flushed together.
                        // The player fits this part in the final shipyard lesson.
                        if (reward != null && GrantOwnedComponent(reward, 1f) != null)
                        {
                            PlayerPrefs.SetInt(GuidedTutorialComponentRewardKey, 1);
                        }
                    }
                    // The unique valuable follows the same confirmed return as
                    // equipment. Idempotent ownership prevents replay/reload
                    // farming, and the following display lesson awards nothing.
                    GrantWarehouseTreasureOnce(CoreWarehouseCollection.TutorialTreasureId);
                    guidedTutorialCheckpoint = 5;
                    PlayerPrefs.SetInt(GuidedTutorialWarehousePendingKey, 1);
                    PlayerPrefs.SetInt(GuidedTutorialEquipmentPendingKey, 1);
                    PlayerPrefs.SetInt(TutorialCompletedKey, 0);
                    PlayerPrefs.SetInt(GuidedTutorialNavigatorEventKey, 1);
                    PlayerPrefs.DeleteKey(GuidedTutorialRecoveryKey);
                    guidedTutorialStep = GuidedTutorialStep.Warehouse;
                    portNotice = "老船长遗物已入库：白色部件「漂木锯齿首」待装配，贵重品「破浪者的罗盘」已收藏。先到仓库陈列罗盘，再到皇家船坞亲自装上部件。";
                    session.ConfigureGuidedTutorialVoyage(0);
                }
                else if (legacyNavigatorComplete)
                {
                    // Already-suspended legacy navigator voyages keep their
                    // authored port checkpoint and subsequent boss departure.
                    guidedTutorialCheckpoint = 3;
                    PlayerPrefs.SetInt(GuidedTutorialNavigatorEventKey, 1);
                    guidedTutorialStep = GuidedTutorialStep.ThirdPier;
                    portNotice = "航海士已完成潮汐暗标事件。下一次出航进入首领教学。";
                }
                else guidedTutorialCheckpoint = 1;
                PlayerPrefs.SetInt(GuidedTutorialCheckpointKey, guidedTutorialCheckpoint);
            }
            else if (tutorialActive && leg > 0)
            {
                // Failure remains the ordinary paid-resource outcome. Open only
                // real provisioning/sales before another default-deck attempt.
                if (leg == CoreGuidedTutorialContent.BossVoyageLeg)
                    guidedTutorialCheckpoint = PlayerPrefs.GetInt(GuidedTutorialNavigatorEventKey, 0) == 1 ? 3 : 2;
                else if (leg == CoreGuidedTutorialContent.NavigatorVoyageLeg)
                    guidedTutorialCheckpoint = 2;
                PlayerPrefs.SetInt(GuidedTutorialCheckpointKey, guidedTutorialCheckpoint);
                PlayerPrefs.SetInt(GuidedTutorialRecoveryKey, 1);
                portNotice = leg == CoreGuidedTutorialContent.BossVoyageLeg
                    ? "挑战尚未完成。可以到港口市场采购或出售物资，再从出航码头使用默认卡组重试首领战。"
                    : leg == CoreGuidedTutorialContent.NavigatorVoyageLeg
                        ? "航海士事件航程尚未完成。补给后再次出航，先完成特殊船员事件再继续首领教学。"
                        : "初航尚未完成。可以到港口市场采购或出售物资，再从出航码头重新完成航行与事件教学。";
            }
            if (!string.IsNullOrEmpty(storageNotice)) portNotice = storageNotice;
            activePortFacility = -1;
            // Keep this evidence in memory until the return transaction begins.
            // In particular, viewing or abandoning the settlement writes no unlock.
            pendingVoyageThreeEventsAchievement = Application.isPlaying && !editorPreviewBuilding && voyageThreeEventsComplete;
            pendingVoyageHalfChartAchievement = Application.isPlaying && !editorPreviewBuilding && voyageHalfChartComplete;
            pendingVoyageEventArrivalAchievement = Application.isPlaying && !editorPreviewBuilding && voyageEventArrivalComplete;
            SaveProgress();
            if (tutorialActive) ResetGuidedTutorialPortStep();
            Render();
        }

        private string LastDrawnChartSummary(int routeIndex = -1)
        {
            try
            {
                string json = PlayerPrefs.GetString(DrawnSeaChartKey, string.Empty);
                if (string.IsNullOrEmpty(json)) return string.Empty;
                DrawnSeaChartRecord chart = JsonUtility.FromJson<DrawnSeaChartRecord>(json);
                if (chart == null || chart.version != 1 || string.IsNullOrEmpty(chart.seed)) return string.Empty;
                if (routeIndex >= 0 && chart.routeIndex != routeIndex) return string.Empty;
                string endpoints = !string.IsNullOrEmpty(chart.departurePort) && !string.IsNullOrEmpty(chart.destinationPort)
                    ? chart.departurePort + " → " + chart.destinationPort : chart.port;
                return "已绘海图：" + chart.routeName + "　" + endpoints + (chart.tutorialWaters ? " · 近港教学水域" : "") + "　探索 " + chart.coverage.ToString("0") + "%\n种子 " + chart.seed + " / " + chart.hash;
            }
            catch (ArgumentException) { return string.Empty; }
        }
    }
}
