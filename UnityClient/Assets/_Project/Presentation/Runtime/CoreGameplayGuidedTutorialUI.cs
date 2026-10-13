using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum GuidedTutorialStep
    {
        // Retain step identifiers/ordinals for compatibility with saved tutorial progress.
        // The full navigator-event voyage uses the existing Second* steps.
        FirstDeparture, FirstSail, FirstMandatory, FirstOptional, FirstCrewEvent,
        FirstReturn, FirstSettlement, Tavern, RecruitNavigator, SecondPier,
        SecondRoute, SecondDeparture, SecondSail, SecondCrewEvent,
        // Retain serialized legacy ordinals. The combined second voyage also
        // uses the ambush step before the fixed boss lesson.
        SecondRandom, SecondAmbush, SecondRetreat, SecondSettlement, Workshop, BuildDeck,
        ThirdPier, ThirdRoute, ThirdDeparture, ThirdSail, TutorialBoss,
        ThirdReturn, ThirdSettlement, DrawChart, Warehouse, SelectTreasure, DisplayTreasure, Complete,
        Shipyard, ShipEquipment, UnequipRewardComponent, SelectRewardComponent, EquipRewardComponent
    }

    public sealed partial class CoreGameplayDemoController
    {
        private sealed class GuidedControlSnapshot
        {
            public Selectable Control;
            public bool Interactable;
            public Selectable.Transition Transition;
            public Button.ButtonClickedEvent Click;
            public Slider.SliderEvent Slide;
            public InputField.EndEditEvent Submit;
            public GuidedBossBattleStep BossStep;
        }

        private sealed class GuidedMapSnapshot
        {
            public MapCourseInputSurface Input;
            public bool Enabled;
            public Graphic Graphic;
            public bool Raycast;
        }

        private readonly List<GuidedControlSnapshot> guidedControlSnapshots = new List<GuidedControlSnapshot>();
        private readonly List<GuidedMapSnapshot> guidedMapSnapshots = new List<GuidedMapSnapshot>();
        private readonly List<GameObject> guidedDecorations = new List<GameObject>();
        private readonly List<Graphic> guidedHiddenLabels = new List<Graphic>();
        private readonly List<Behaviour> guidedDisabledFeedback = new List<Behaviour>();

        /// <summary>
        /// Decorates the ordinary gameplay UI after it has been built. It does not
        /// create alternate voyages, resolve choices, award items or advance steps.
        /// </summary>
        private void BuildGuidedTutorialOverlay()
        {
            RestoreGuidedTutorialControls(false);
            if (!tutorialActive || guidedTutorialStep == GuidedTutorialStep.Complete || contentRoot == null) return;

            GuidedTutorialStep renderedStep = guidedTutorialStep;
            foreach (Selectable control in contentRoot.GetComponentsInChildren<Selectable>(true))
            {
                var snapshot = new GuidedControlSnapshot
                {
                    Control = control, Interactable = control.interactable, Transition = control.transition,
                    BossStep = GuidedBossPresentationStep
                };
                guidedControlSnapshots.Add(snapshot);
                bool permitted = GuidedTutorialAllowsControl(control.name);
                // Only the two explicit failed-attempt lessons accept a click
                // on an unplayable card; Core observes the real failure without
                // playing it or consuming resources. All other rules stay intact.
                if (permitted && IsGuidedBossBlockedCardControl(control.name)) control.interactable = true;
                if (!permitted)
                {
                    // Teaching locks leave the original scene and button brightness intact.
                    // Real unavailable actions (costs, energy, crew) keep their disabled style.
                    if (snapshot.Interactable)
                    {
                        control.transition = Selectable.Transition.None;
                        if (control.targetGraphic != null)
                            control.targetGraphic.CrossFadeColor(control.colors.normalColor * control.colors.colorMultiplier, 0f, true, true);
                    }
                    control.interactable = false;
                    AddGuidedInputGuard(control);
                    foreach (EventTrigger trigger in control.GetComponentsInChildren<EventTrigger>(true))
                    {
                        if (!trigger.enabled) continue;
                        guidedDisabledFeedback.Add(trigger);
                        trigger.enabled = false;
                    }
                    BattleCardHoverFeedback hover = control.GetComponent<BattleCardHoverFeedback>();
                    if (hover != null && hover.enabled)
                    {
                        guidedDisabledFeedback.Add(hover);
                        hover.enabled = false;
                    }
                }
                Button button = control as Button;
                if (button != null)
                {
                    snapshot.Click = button.onClick;
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(() =>
                    {
                        if (GuidedControlCanInvoke(snapshot, renderedStep)) snapshot.Click.Invoke();
                    });
                }
                Slider slider = control as Slider;
                if (slider != null)
                {
                    snapshot.Slide = slider.onValueChanged;
                    slider.onValueChanged = new Slider.SliderEvent();
                    slider.onValueChanged.AddListener(value =>
                    {
                        if (GuidedControlCanInvoke(snapshot, renderedStep)) snapshot.Slide.Invoke(value);
                    });
                }
                InputField input = control as InputField;
                if (input != null)
                {
                    snapshot.Submit = input.onEndEdit;
                    input.onEndEdit = new InputField.EndEditEvent();
                    input.onEndEdit.AddListener(value =>
                    {
                        if (GuidedControlCanInvoke(snapshot, renderedStep)) snapshot.Submit.Invoke(value);
                    });
                }
            }

            foreach (MapCourseInputSurface input in contentRoot.GetComponentsInChildren<MapCourseInputSurface>(true))
            {
                Graphic graphic = input.GetComponent<Graphic>();
                guidedMapSnapshots.Add(new GuidedMapSnapshot
                {
                    Input = input, Enabled = input.enabled, Graphic = graphic,
                    Raycast = graphic != null && graphic.raycastTarget
                });
                input.enabled = false;
                if (graphic != null) graphic.raycastTarget = false;
            }

            Transform activeChart = FindActiveGuidedContent("Navigation Chart");
            Transform hint = activeChart == null ? null : activeChart.Find("Map Course Hint");
            if (hint != null && hint.TryGetComponent(out Text mapHint))
                mapHint.text = "教学航线 · 固定向东航行 · 完成引导后开放自由航点";

            // A previously selected button must not keep keyboard submit focus
            // when its role is locked by the next tutorial step.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                Selectable selected = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
                if (selected != null && !selected.IsInteractable()) EventSystem.current.SetSelectedGameObject(null);
            }
            BuildGuidedTutorialBanner();
            BuildGuidedTutorialFocus();
            BringGuidedTutorialBannerToFront();
        }

        private bool GuidedControlCanInvoke(GuidedControlSnapshot snapshot, GuidedTutorialStep renderedStep)
        {
            if (snapshot.Control == null || !snapshot.Control.gameObject.activeInHierarchy ||
                !snapshot.Control.IsInteractable()) return false;
            if (snapshot.BossStep != GuidedBossPresentationStep) return false;
            // Reject callbacks cached from a previous teaching screen, including
            // direct UnityEvent.Invoke calls which bypass Button.interactable.
            return !tutorialActive || guidedTutorialStep == GuidedTutorialStep.Complete ||
                (renderedStep == guidedTutorialStep && GuidedTutorialAllowsControl(snapshot.Control.name));
        }

        private void AddGuidedInputGuard(Selectable control)
        {
            RectTransform shade = CreateRect("Guide Input Guard", control.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            Image image = shade.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            // Button and EventTrigger live on the parent. Catch pointer events
            // here so locked transparent hotspots cannot still light up on hover.
            shade.gameObject.AddComponent<GuidedTutorialPointerBlocker>();
            guidedDecorations.Add(shade.gameObject);
        }

        private void RestoreGuidedTutorialControls(bool retiringScreen = true)
        {
            foreach (GuidedControlSnapshot snapshot in guidedControlSnapshots)
            {
                if (snapshot.Control == null) continue;
                snapshot.Control.transition = snapshot.Transition;
                snapshot.Control.interactable = snapshot.Interactable;
                // Render retires these objects immediately after this cleanup.
                // Keep their guard events in place: code holding the old Button
                // itself must not obtain an unguarded callback while Destroy is
                // deferred to the end of the frame. A same-screen re-decoration
                // restores events first so wrappers do not accumulate.
                if (!retiringScreen)
                {
                    if (snapshot.Click != null && snapshot.Control is Button button) button.onClick = snapshot.Click;
                    if (snapshot.Slide != null && snapshot.Control is Slider slider) slider.onValueChanged = snapshot.Slide;
                    if (snapshot.Submit != null && snapshot.Control is InputField input) input.onEndEdit = snapshot.Submit;
                }
            }
            guidedControlSnapshots.Clear();
            foreach (GuidedMapSnapshot snapshot in guidedMapSnapshots)
            {
                if (snapshot.Input != null) snapshot.Input.enabled = snapshot.Enabled;
                if (snapshot.Graphic != null) snapshot.Graphic.raycastTarget = snapshot.Raycast;
            }
            guidedMapSnapshots.Clear();
            foreach (Behaviour feedback in guidedDisabledFeedback) if (feedback != null) feedback.enabled = true;
            guidedDisabledFeedback.Clear();
            foreach (Graphic graphic in guidedHiddenLabels) if (graphic != null) graphic.enabled = true;
            guidedHiddenLabels.Clear();
            foreach (GameObject decoration in guidedDecorations)
            {
                if (decoration == null) continue;
                decoration.SetActive(false);
                if (Application.isPlaying) Destroy(decoration); else DestroyImmediate(decoration);
            }
            guidedDecorations.Clear();
        }

        private bool GuidedTutorialAllowsControl(string name)
        {
            // Settings and safe suspension remain available even when a lesson
            // restricts ordinary navigation or requires an emergency return.
            if (name == "Voyage Settings" || name == "Voyage Cargo" || name == "Port Hub Settings" || name == "Port Facility Settings") return true;
            GuidedTutorialStep step = guidedTutorialStep;
            if (step == GuidedTutorialStep.Complete) return true;
            if (session != null && session.CanRequestEmergencyTow())
                return name == "Emergency Encounter Return" || name == "Emergency Battle Return";
            if (GuidedTutorialNeedsEncounterFallback())
                return name == "Encounter Choice 1" || name == "Skip Encounter";
            if (GuidedTutorialNeedsSupplyReturn) return name == "Safe Return";
            if (GuidedTutorialAllowsRecoveryControl(name)) return true;
            if (session != null && session.Phase == CoreDemoPhase.Sailing && GuidedIsSailingTarget(step))
                return name == GuidedTutorialNavigationTargetControl;
            switch (step)
            {
                case GuidedTutorialStep.FirstDeparture:
                    if (activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex)
                        return name == "Port Hotspot Voyage" || name == "Return To Port Hub";
                    goto case GuidedTutorialStep.SecondRoute;
                case GuidedTutorialStep.SecondRoute:
                case GuidedTutorialStep.ThirdRoute:
                    return name == "Start Voyage" || name == "Route 0" ||
                        name.StartsWith("Departure Destination ", StringComparison.Ordinal);
                case GuidedTutorialStep.SecondDeparture:
                case GuidedTutorialStep.ThirdDeparture:
                    return name == "Start Voyage" || name.StartsWith("Departure Destination ", StringComparison.Ordinal) || name == "Route 0";
                case GuidedTutorialStep.FirstMandatory:
                case GuidedTutorialStep.SecondRandom:
                    return name.StartsWith("Encounter Choice ", StringComparison.Ordinal);
                case GuidedTutorialStep.SecondAmbush:
                    return session != null && session.UsesCombinedGuidedTutorialVoyage
                        ? GuidedEncounterChoiceMatches(name, choice => !choice.StartsBattle)
                        : name.StartsWith("Encounter Choice ", StringComparison.Ordinal);
                case GuidedTutorialStep.FirstOptional:
                    return name == "Skip Encounter" || name.StartsWith("Encounter Choice ", StringComparison.Ordinal);
                case GuidedTutorialStep.FirstCrewEvent:
                    return name == "Encounter Choice 1";
                case GuidedTutorialStep.SecondCrewEvent:
                    return GuidedEncounterChoiceMatches(name, choice => choice.RequiredCrew == "navigator");
                case GuidedTutorialStep.FirstReturn:
                case GuidedTutorialStep.ThirdReturn:
                    return name == "Safe Return";
                case GuidedTutorialStep.SecondRetreat:
                    return name == "Retreat Battle";
                case GuidedTutorialStep.FirstSettlement:
                case GuidedTutorialStep.SecondSettlement:
                case GuidedTutorialStep.ThirdSettlement:
                    return name == "Return To Port" || name == "Skip Settlement Reveal";
                case GuidedTutorialStep.DrawChart:
                    return name == "Draw Sea Chart" || name == "Skip Settlement Reveal";
                case GuidedTutorialStep.Tavern:
                    return name == "Port Hotspot Tavern" || name == "Return To Port Hub";
                case GuidedTutorialStep.RecruitNavigator:
                    return name == "Tavern Crew 0" || name == "Recruit Tavern Crew 0" ||
                        (name == "Return To Port Hub" && IsTavernDossierOpen) || name.StartsWith("DossierPage", StringComparison.Ordinal) ||
                        name == "Previous Tavern Dossier Page" || name == "Next Tavern Dossier Page";
                case GuidedTutorialStep.SecondPier:
                case GuidedTutorialStep.ThirdPier:
                    return name == "Port Hotspot Voyage" || name == "Return To Port Hub";
                case GuidedTutorialStep.Warehouse:
                    return name == "Port Hotspot Warehouse" || name == "Return To Port Hub";
                case GuidedTutorialStep.SelectTreasure:
                    return name == "Warehouse Tab Valuables" ||
                        name == "Warehouse Treasure " + CoreWarehouseCollection.TutorialTreasureId;
                case GuidedTutorialStep.DisplayTreasure:
                    return name == "Warehouse Tab Valuables" || name == "Warehouse Display Selected" ||
                        name == "Warehouse Treasure " + CoreWarehouseCollection.TutorialTreasureId ||
                        GuidedWarehouseDisplaySlotMatches(name);
                case GuidedTutorialStep.Shipyard:
                    return name == "Port Hotspot Shipyard" || name == "Return To Port Hub" ||
                        name == "Shipyard Ship " + session.SelectedShipIndex;
                case GuidedTutorialStep.ShipEquipment:
                    return name == "Shipyard Dossier Tab Equipment";
                case GuidedTutorialStep.UnequipRewardComponent:
                    return name == "Shipyard Unequip Component Bow";
                case GuidedTutorialStep.SelectRewardComponent:
                    OwnedShipComponent reward = GuidedTutorialOwnedReward();
                    return reward != null && name == "Shipyard Equipment Owned " + reward.InstanceId;
                case GuidedTutorialStep.EquipRewardComponent:
                    return name == "Shipyard Equip Component";
                case GuidedTutorialStep.TutorialBoss:
                    if (HasGuidedBossPresentation) return name == GuidedBossTargetControl;
                    return name.StartsWith("Battle Card ", StringComparison.Ordinal) || name == "End Battle Turn" ||
                        name == "Battle Ram" || name == "Battle Board" ||
                        GuidedEncounterChoiceMatches(name, choice => choice.StartsBattle);
                default:
                    return false;
            }
        }

        private bool GuidedTutorialAllowsRecoveryControl(string name)
        {
            if (!GuidedTutorialRecoveryActive || session == null || session.Phase != CoreDemoPhase.Port) return false;
            if (name == "Port Hotspot Maintenance" || name == "Port Hotspot Warehouse" ||
                name == "Port Hotspot Voyage" || name == "Return To Port Hub") return true;
            if (activePortFacility == PortFacilityArtCatalog.MaintenanceFacilityIndex)
                return name == "Open Port Market";
            if (activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex)
                return name.StartsWith("Market Shop ", StringComparison.Ordinal) ||
                    name.StartsWith("Market Row ", StringComparison.Ordinal) ||
                    name.StartsWith("Market Item Icon ", StringComparison.Ordinal) ||
                    name.StartsWith("Market Buy ", StringComparison.Ordinal) ||
                    name.StartsWith("Market Sell ", StringComparison.Ordinal) ||
                    name.StartsWith("Market Quantity Slider ", StringComparison.Ordinal) ||
                    name.StartsWith("Market Quantity Input ", StringComparison.Ordinal) ||
                    name == "Market Quantity Minus" || name == "Market Quantity Plus" || name == "Market Quantity Max" ||
                    name == "Close Market Shop" || name == "Market Scene Empty Space" || name == "Market Inventory Toggle" ||
                    name.StartsWith("Confirm Market Buy ", StringComparison.Ordinal) ||
                    name.StartsWith("Confirm Market Sell ", StringComparison.Ordinal) ||
                    name == "Market Detail Back" || name == "Cancel Market Trade" ||
                    name == "Return To Port Hub";
            if (activePortFacility == 0)
                return name == "Warehouse Tab Storage" || name == "Warehouse Tab Valuables" ||
                    name.StartsWith("Warehouse Storage Row ", StringComparison.Ordinal) ||
                    name == "Warehouse Adjust Cargo" || name == "Warehouse Sell Cargo" ||
                    name == "Warehouse Cargo Adjustment Slider" || name == "Warehouse Stored Sale Slider" ||
                    name == "Warehouse Ship Sale Slider" || name.StartsWith("Confirm Warehouse Cargo ", StringComparison.Ordinal) ||
                    name.StartsWith("Cancel Warehouse ", StringComparison.Ordinal);
            return false;
        }

        private static bool GuidedWarehouseDisplaySlotMatches(string name)
        {
            const string prefix = "Warehouse Display Slot ";
            return name.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(name.Substring(prefix.Length), out int slot) &&
                slot >= 0 && slot < CoreWarehouseCollection.DisplaySlotCount;
        }

        private bool GuidedTutorialNeedsEncounterFallback()
        {
            if (session == null || session.Phase != CoreDemoPhase.Encounter || session.ActiveEncounter == null) return false;
            // Preserve the ordinary zero-cost return / optional skip routes when
            // a low-resource checkpoint cannot afford the teaching choice.
            if (guidedTutorialStep == GuidedTutorialStep.SecondCrewEvent)
                return !session.IsChoiceAvailable(0);
            if (guidedTutorialStep != GuidedTutorialStep.SecondRandom || session.ActiveEncounter.Mandatory) return false;
            for (int i = 0; i < session.ActiveEncounter.Choices.Count; i++)
                if (session.IsChoiceAvailable(i)) return false;
            return true;
        }

        private bool GuidedEncounterChoiceMatches(string name, Predicate<CoreEncounterChoice> match)
        {
            const string prefix = "Encounter Choice ";
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || session == null || session.ActiveEncounter == null ||
                !int.TryParse(name.Substring(prefix.Length), out int index)) return false;
            return index >= 0 && index < session.ActiveEncounter.Choices.Count && match(session.ActiveEncounter.Choices[index]);
        }

        private static bool GuidedIsSailingTarget(GuidedTutorialStep step)
        {
            return step == GuidedTutorialStep.FirstSail || step == GuidedTutorialStep.FirstMandatory ||
                step == GuidedTutorialStep.FirstOptional || step == GuidedTutorialStep.FirstCrewEvent ||
                step == GuidedTutorialStep.SecondSail || step == GuidedTutorialStep.SecondCrewEvent ||
                step == GuidedTutorialStep.SecondRandom || step == GuidedTutorialStep.SecondAmbush ||
                step == GuidedTutorialStep.ThirdSail || step == GuidedTutorialStep.TutorialBoss;
        }

        private void BuildGuidedTutorialBanner()
        {
            bool inPort = session != null && session.Phase == CoreDemoPhase.Port;
            // A separate reading surface keeps every lesson legible over both
            // the carved headers and the illustrated scenes. Grow for long
            // instructions instead of shrinking the teaching text.
            const float width = 480f, padding = 40f, top = 24f, gap = 8f;
            RectTransform banner = CreateRect("Guided Tutorial Banner", contentRoot,
                new Vector2(width, 140f), Vector2.zero);
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(0f, 1f);
            banner.anchoredPosition = new Vector2(20f, -8f);
            guidedDecorations.Add(banner.gameObject);
            Image backing = banner.gameObject.AddComponent<Image>();
            backing.color = Html("#EBD5A7");
            NauticalSurfaceSkin.ApplyReadingPanel(backing);
            // Scale the painted border to this small note, keeping ornament
            // outside the inset writing area instead of crossing the letters.
            if (backing.sprite != null) backing.pixelsPerUnitMultiplier = backing.sprite.rect.width / width;
            backing.raycastTarget = false;

            if (inPort)
            {
                Transform originalDescription = FindActiveGuidedContent("Port Facility Description");
                if (originalDescription != null) HideGuidedOriginalLabel(originalDescription.GetComponent<Graphic>());
            }
            else HideGuidedOriginalLabel(headerTitle);

            Text title = CreateText("Guided Tutorial Objective", banner,
                new Vector2(width - padding * 2f, 32f), Vector2.zero,
                24, FontStyle.Normal, TextAnchor.UpperLeft, Html("#684022"));
            title.text = GuidedTutorialRecoveryActive && inPort && (activePortFacility == 0 || activePortFacility == PortFacilityArtCatalog.MaintenanceFacilityIndex)
                ? "挑战后整备 · 为下一次航行做准备"
                : HasGuidedBossPresentation ? GuidedBossObjective
                : session.Phase == CoreDemoPhase.Sailing && GuidedIsSailingTarget(guidedTutorialStep) &&
                    !GuidedTutorialNeedsSupplyReturn && !session.CanRequestEmergencyTow()
                    ? CurrentGuidedTutorialNavigationInputStep == GuidedTutorialNavigationInputStep.Heading
                        ? "航行指引 1 / 2 · 选择航向" : "航行指引 2 / 2 · 执行航行"
                    : GuidedTutorialObjective(guidedTutorialStep);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = false;
            float titleHeight = Mathf.Max(32f, Mathf.Ceil(title.preferredHeight));
            PositionGuidedReadingText(title, padding, top, titleHeight);
            Text instruction = CreateText("Guided Tutorial Instruction", banner,
                new Vector2(width - padding * 2f, 68f), Vector2.zero,
                26, FontStyle.Normal, TextAnchor.UpperLeft, Html("#302014"));
            instruction.text = GuidedTutorialInstruction(guidedTutorialStep);
            instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
            instruction.verticalOverflow = VerticalWrapMode.Truncate;
            instruction.lineSpacing = 1.1f;
            instruction.resizeTextForBestFit = false;
            float instructionHeight = Mathf.Max(68f, Mathf.Ceil(instruction.preferredHeight));
            PositionGuidedReadingText(instruction, padding, top + titleHeight + gap, instructionHeight);
            banner.sizeDelta = new Vector2(width, top + titleHeight + gap + instructionHeight + top);
        }

        private static void PositionGuidedReadingText(Text text, float left, float top, float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private void BringGuidedTutorialBannerToFront()
        {
            // Previous steps can leave an inactive namesake until end-of-frame.
            Transform banner = FindActiveGuidedContent("Guided Tutorial Banner");
            if (banner == null) return;
            banner.SetAsLastSibling();
            // Render may have resized the voyage surface after creating the
            // annotation; resolve against that final geometry before display.
            GuidedTutorialBannerLayout layout = banner.GetComponent<GuidedTutorialBannerLayout>();
            if (layout != null) layout.Apply();
        }

        private void HideGuidedOriginalLabel(Graphic graphic)
        {
            if (graphic == null || !graphic.enabled) return;
            guidedHiddenLabels.Add(graphic);
            graphic.enabled = false;
        }

        private static string GuidedTutorialObjective(GuidedTutorialStep step)
        {
            if (step >= GuidedTutorialStep.Shipyard) return "初航收尾 · 为旗舰装上首胜部件";
            if (step <= GuidedTutorialStep.FirstSettlement) return "初航 1 / 2 · 认识航行与事件";
            if (step <= GuidedTutorialStep.RecruitNavigator) return "初航 2 / 2 · 招募你的航海士";
            if (step <= GuidedTutorialStep.SecondSettlement) return "初航 2 / 2 · 船员与伏击";
            if (step < GuidedTutorialStep.ThirdReturn) return "首领教学 · 炮战与接舷";
            if (step >= GuidedTutorialStep.Warehouse && step <= GuidedTutorialStep.DisplayTreasure)
                return "初航收尾 · 陈列你的首胜纪念品";
            return "初航收尾 · 返航结算与战利品";
        }

        private string GuidedTutorialInstruction(GuidedTutorialStep step)
        {
            if (HasGuidedBossPresentation) return GuidedBossInstruction;
            if (session != null && session.CanRequestEmergencyTow())
                return "补给不足，选择「应急返港」，按失败保护率保留 35%。回港补给后重试。";
            if (GuidedTutorialNeedsEncounterFallback())
                return guidedTutorialStep == GuidedTutorialStep.SecondCrewEvent
                    ? "补给不足，选择「保留笔记，返港整备」。按正常返航或拖带规则结算，补给后重试。"
                    : "补给不足，先略过事件，再返港补给重试。";
            if (GuidedTutorialNeedsSupplyReturn)
                return "补给不足，主动返航或拖带救援后，回港采购重试。本次不完成教学。";
            if (GuidedTutorialRecoveryActive && session != null && session.Phase == CoreDemoPhase.Port)
            {
                if (activePortFacility == 0 || activePortFacility == PortFacilityArtCatalog.MaintenanceFacilityIndex)
                    return guidedTutorialCheckpoint == 0
                        ? "按市场行情买卖物资，再到出航码头重试航行与事件教学。不会自动发放免费物资。"
                        : "按市场行情买卖物资，再到出航码头用默认卡组重试。不会自动发放免费物资。";
                if (activePortFacility < 0)
                    return "到「港口市场」采购，或到「仓库」出售材料；随后到「出航码头」重试，无需编组。";
            }
            if (session != null && session.Phase == CoreDemoPhase.Sailing && GuidedIsSailingTarget(step))
            {
                switch (CurrentGuidedTutorialNavigationInputStep)
                {
                    case GuidedTutorialNavigationInputStep.Heading:
                        return "点击右侧罗盘中的「东」，确定教学航向。";
                    default:
                        return "教学航程已设定。核对用时与补给，点击「执行航行」。";
                }
            }
            switch (step)
            {
                case GuidedTutorialStep.FirstDeparture:
                    if (activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex)
                        return activePortFacility < 0
                            ? "进入红框中的「出航码头」，准备第一次航行。"
                            : "先「返回港口」，再进入「出航码头」。";
                    if (!guidedDepartureDestinationSelected) return "先选择到达港口；置暗的是当前出发港。";
                    if (!guidedDepartureRouteSelected) return "选择第一张海图，确认本次航线难度。";
                    return "选择「扬帆出航」，开始第一段航程。";
                case GuidedTutorialStep.FirstSail: return "选择「东」并「执行航行」；补给与事件按正常规则结算。";
                case GuidedTutorialStep.FirstMandatory: return "强制事件不能略过。核对代价，选择一项可用方案。";
                case GuidedTutorialStep.FirstOptional: return "核对代价与收益，选择红框内的可用方案，或「略过事件」。";
                case GuidedTutorialStep.FirstCrewEvent: return "专业选项需要航海士。选择「收好笔记」，返港招募。";
                case GuidedTutorialStep.FirstReturn: return "选择「主动返航」；随船战利品按返航保护率结算。";
                case GuidedTutorialStep.FirstSettlement: return "核对入库明细，选择「确认结算 · 返回港口」。";
                case GuidedTutorialStep.Tavern: return "进入「酒馆」；若在功能页，先「返回港口」。";
                case GuidedTutorialStep.RecruitNavigator: return "查看航海士的档案与技能，支付契约金招募。";
                case GuidedTutorialStep.SecondPier: return "返回港口，进入「出航码头」，带航海士处理事件并迎击首领。";
                case GuidedTutorialStep.SecondRoute:
                    if (!guidedDepartureDestinationSelected) return "先选择到达港口，确定贸易终点。";
                    if (!guidedDepartureRouteSelected) return "选择第一张海图，进入航海士事件教学航线。";
                    return "选择「扬帆出航」，开始航海士事件教学。";
                case GuidedTutorialStep.SecondDeparture: return "选择「扬帆出航」，带航海士处理潮汐暗标。";
                case GuidedTutorialStep.SecondSail:
                    return session != null && session.UsesCombinedGuidedTutorialVoyage
                        ? "向东航行，依次处理船员事件、侦测伏击和首领信号。"
                        : "向东航行，让航海士处理特殊船员事件后再继续。";
                case GuidedTutorialStep.SecondCrewEvent: return "选择航海士的职业选项，获取隐藏航道情报。";
                case GuidedTutorialStep.SecondRandom: return "处理隐藏讯息事件，再向东航行。";
                case GuidedTutorialStep.SecondAmbush:
                    return session != null && session.UsesCombinedGuidedTutorialVoyage
                        ? "航海士提前发现伏击。选择「避开炮烟，继续航行」，保留物资前往首领。"
                        : "遭遇伏击后，选择脱离战斗并返航。";
                case GuidedTutorialStep.SecondRetreat: return "选择「脱离战斗 · 撤回港口」，进入结算。";
                case GuidedTutorialStep.SecondSettlement:
                    return session == null || !session.GuidedTutorialCompletedEncounterIds.Contains(CoreGuidedTutorialContent.NavigatorEventId)
                        ? "暗标尚未解读。结算返港补给后重试航海士事件；本次不推进首领教学。"
                        : session.UsesCombinedGuidedTutorialVoyage
                        ? "首领教学尚未完成。确认结算，返港补给后重新出航。"
                        : "确认结算返港；特殊船员事件已完成，下次出航进入首领教学。";
                case GuidedTutorialStep.ThirdPier: return "进入「出航码头」，准备用默认卡组挑战首领。";
                case GuidedTutorialStep.ThirdRoute:
                    if (!guidedDepartureDestinationSelected) return "先选择首领教学的到达港口。";
                    if (!guidedDepartureRouteSelected) return "选择第一张海图，进入首领教学航线。";
                    return "选择「扬帆出航」挑战首领。";
                case GuidedTutorialStep.ThirdDeparture: return "选择「扬帆出航」，用默认卡组挑战首领。";
                case GuidedTutorialStep.ThirdSail: return "向东航行，前往首领信号。默认卡组已备好，无需前往工坊。";
                case GuidedTutorialStep.TutorialBoss:
                    return session != null && session.Phase == CoreDemoPhase.Encounter
                        ? "选择红框交战选项，学习炮战与接舷。本次胜利必得 1 个藏宝图碎片。"
                        : "先打策略牌，再炮击或接舷，行动后「结束回合」。同名牌每次打出后费用 +1，副本共享；下回合或切换阶段重置。";
                case GuidedTutorialStep.ThirdReturn: return "已获 1 个藏宝图碎片、白色部件与「破浪者的罗盘」。选择「主动返航」保全战利品。";
                case GuidedTutorialStep.DrawChart: return "先「绘制海图」，再确认结算，将战利品带回仓库。";
                case GuidedTutorialStep.ThirdSettlement:
                    return session != null && session.Settlement != null && session.Settlement.Outcome == VoyageOutcome.Failure
                        ? "挑战未完成，结算返港补给后用默认卡组重试。本次不完成初航教学。"
                        : "海图已绘制，确认结算返港，将碎片、部件与贵重品入库。随后学习陈列藏品。";
                case GuidedTutorialStep.Warehouse: return "进入「仓库」陈列首胜贵重品，再到皇家船坞装备部件。";
                case GuidedTutorialStep.SelectTreasure: return "在右侧「贵重品」中选择「破浪者的罗盘」，准备陈列。";
                case GuidedTutorialStep.DisplayTreasure: return "选择左侧展示格，再「陈列到展位」；藏品不会消耗。随后去船坞装备首胜部件。";
                case GuidedTutorialStep.Shipyard:
                    return activePortFacility == 1
                        ? "选择当前旗舰，再进入「配件」页。"
                        : "返回港口，进入「皇家船坞」，为旗舰装上首胜部件。";
                case GuidedTutorialStep.ShipEquipment: return "选择「配件」页签，查看装备与库存。";
                case GuidedTutorialStep.UnequipRewardComponent: return "首胜部件已装上，先「卸下」再重新装备。不会损失或重复领取部件。";
                case GuidedTutorialStep.SelectRewardComponent: return "选择库存中的「漂木锯齿首」，查看效果。";
                case GuidedTutorialStep.EquipRewardComponent: return "选择装配／替换，将首胜部件装到旗舰；旧件保留在库存。完成后可自由探索。";
                default: return "跟随箭头操作红框目标；其余操作稍后开放。";
            }
        }
    }

    /// <summary>Consumes pointer events on locked controls, including EventTrigger hover callbacks.</summary>
    public sealed class GuidedTutorialPointerBlocker : MonoBehaviour, IPointerClickHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public void OnPointerClick(PointerEventData eventData) { }
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerUp(PointerEventData eventData) { }
        public void OnPointerEnter(PointerEventData eventData) { }
        public void OnPointerExit(PointerEventData eventData) { }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) { }
    }
}

