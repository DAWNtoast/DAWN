using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildGuidedTutorialFocus()
        {
            if (HasGuidedBossPresentation) { BuildGuidedBattleCourseFocus(); return; }
            var targets = new List<Selectable>();
            CollectGuidedTutorialFocusTargets(targets);
            if (targets.Count == 0) return;
            RectTransform layer = CreateRect("Guided Tutorial Focus Layer", contentRoot, Vector2.zero, Vector2.zero);
            StretchGuidedFocus(layer);
            guidedDecorations.Add(layer.gameObject);
            var markers = new List<GuidedTutorialFocusGraphic>();
            foreach (Selectable target in targets)
            {
                RectTransform marker = CreateRect("Guide Focus " + target.name, layer, Vector2.zero, Vector2.zero);
                StretchGuidedFocus(marker);
                GuidedTutorialFocusGraphic graphic = marker.gameObject.AddComponent<GuidedTutorialFocusGraphic>();
                graphic.Configure(target);
                markers.Add(graphic);
            }
            Transform banner = FindActiveGuidedContent("Guided Tutorial Banner");
            if (banner != null)
                banner.gameObject.AddComponent<GuidedTutorialBannerLayout>().Configure(contentRoot, markers.ToArray(),
                    GuidedTutorialReadingContext());
            if (EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                Selectable current = selected == null ? null : selected.GetComponent<Selectable>();
                if (current == null || !current.gameObject.activeInHierarchy || !current.IsInteractable())
                    EventSystem.current.SetSelectedGameObject(targets[0].gameObject);
            }
        }

        private static void StretchGuidedFocus(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private RectTransform[] GuidedTutorialReadingContext()
        {
            string[] names = session.Phase == CoreDemoPhase.Battle
                ? new[] { "Voyage Header", "Player Battle Name", "Enemy Battle Name", "Player Ship Art", "Enemy Ship Art",
                    "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track", "Player Resource HUD", "Enemy Intent HUD",
                    "Battle Buff Panel", "Enemy Resource HUD" }
                : session.Phase == CoreDemoPhase.Encounter
                    ? new[] { "Voyage Header", "Navigation Map Title Strip", "Encounter Title", "Encounter Description Viewport", "Encounter Illustration Mount", "Encounter Secondary Actions" }
                : session.Phase == CoreDemoPhase.Sailing
                    ? new[] { "Voyage Header", "Navigation Map Title Strip", "Distance Label", "Forecast", "Lookout Report" }
                : session.Phase == CoreDemoPhase.Port
                    ? new[] { "Port Hub", "Port Facility Header" }
                    : Array.Empty<string>();
            var context = new List<RectTransform>();
            foreach (RectTransform rect in contentRoot.GetComponentsInChildren<RectTransform>())
                if (rect.gameObject.activeInHierarchy && Array.IndexOf(names, rect.name) >= 0) context.Add(rect);
            return context.ToArray();
        }

        private bool FocusGuidedControl(List<Selectable> targets, string name)
        {
            foreach (GuidedControlSnapshot snapshot in guidedControlSnapshots)
            {
                Selectable control = snapshot.Control;
                if (control == null || !control.gameObject.activeInHierarchy || control.name != name ||
                    !control.interactable || !GuidedTutorialAllowsControl(name)) continue;
                // Dealing temporarily locks a legal hand card through its CanvasGroup.
                // Register its annotation now: the graphic stays hidden while locked and
                // follows the same card automatically when the flight restores input.
                bool awaitingHandDeal = IsBattleHandFlying && control.transform.parent == retainedBattleHand;
                if (!control.IsInteractable() && !awaitingHandDeal) continue;
                if (!targets.Contains(control)) targets.Add(control);
                return true;
            }
            return false;
        }

        private bool FocusFirstGuidedPrefix(List<Selectable> targets, string prefix)
        {
            foreach (GuidedControlSnapshot snapshot in guidedControlSnapshots)
                if (snapshot.Control != null && snapshot.Control.name.StartsWith(prefix, StringComparison.Ordinal) &&
                    FocusGuidedControl(targets, snapshot.Control.name)) return true;
            return false;
        }

        private void CollectGuidedTutorialFocusTargets(List<Selectable> targets)
        {
            if (session.CanRequestEmergencyTow())
            {
                FocusGuidedControl(targets, "Emergency Encounter Return");
                FocusGuidedControl(targets, "Emergency Battle Return");
                return;
            }
            if (GuidedTutorialNeedsSupplyReturn) { FocusGuidedControl(targets, "Safe Return"); return; }
            if (session.Phase == CoreDemoPhase.Sailing && GuidedIsSailingTarget(guidedTutorialStep))
            {
                FocusGuidedControl(targets, GuidedTutorialNavigationTargetControl);
                return;
            }
            // Highlight every legal event alternative, including the optional skip.
            // Never point at a professional/cost requirement that cannot be met.
            if (session.Phase == CoreDemoPhase.Encounter)
            {
                foreach (GuidedControlSnapshot snapshot in guidedControlSnapshots)
                    if (snapshot.Control != null && (snapshot.Control.name.StartsWith("Encounter Choice ", StringComparison.Ordinal) ||
                        snapshot.Control.name == "Skip Encounter")) FocusGuidedControl(targets, snapshot.Control.name);
                return;
            }
            if (GuidedTutorialRecoveryActive && session.Phase == CoreDemoPhase.Port && activePortFacility == PortFacilityArtCatalog.MaintenanceFacilityIndex)
            {
                FocusGuidedControl(targets, "Open Port Market");
                FocusGuidedControl(targets, "Return To Port Hub");
                return;
            }
            if (GuidedTutorialRecoveryActive && session.Phase == CoreDemoPhase.Port &&
                activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex)
            {
                if (!FocusFirstGuidedPrefix(targets, "Confirm Market Buy ") &&
                    !FocusFirstGuidedPrefix(targets, "Confirm Market Sell ") &&
                    !FocusFirstGuidedPrefix(targets, "Market Buy ") &&
                    !FocusFirstGuidedPrefix(targets, "Market Sell ") &&
                    !FocusFirstGuidedPrefix(targets, "Market Item Icon "))
                    FocusFirstGuidedPrefix(targets, "Market Shop ");
                FocusFirstGuidedPrefix(targets, "Market Quantity Slider ");
                FocusGuidedControl(targets, "Cancel Market Trade");
                FocusGuidedControl(targets, "Close Market Shop");
                FocusGuidedControl(targets, "Return To Port Hub");
                return;
            }
            if (GuidedTutorialRecoveryActive && session.Phase == CoreDemoPhase.Port && activePortFacility == 0)
            {
                bool materialFocused = false;
                foreach (string material in new[] { "wood", "iron", "cloth" })
                    if (session.GetMarketCargoAmount(material) > 0 || session.PortWarehouse.GetAmount(CurrentWarehousePort, material) > 0)
                    {
                        materialFocused = FocusGuidedControl(targets, "Warehouse Storage Row " + material);
                        if (materialFocused) break;
                    }
                if (!materialFocused) FocusGuidedControl(targets, "Warehouse Tab Storage");
                FocusGuidedControl(targets, "Return To Port Hub");
                return;
            }
            switch (guidedTutorialStep)
            {
                case GuidedTutorialStep.FirstDeparture:
                    if (activePortFacility != PortFacilityArtCatalog.VoyageFacilityIndex)
                    {
                        FocusGuidedPortDestination(targets, "Voyage");
                        break;
                    }
                    goto case GuidedTutorialStep.SecondRoute;
                case GuidedTutorialStep.SecondRoute:
                case GuidedTutorialStep.ThirdRoute:
                    if (!guidedDepartureDestinationSelected) FocusFirstGuidedPrefix(targets, "Departure Destination ");
                    if (!guidedDepartureRouteSelected) FocusGuidedControl(targets, "Route 0");
                    FocusGuidedControl(targets, "Start Voyage");
                    break;
                case GuidedTutorialStep.SecondDeparture:
                case GuidedTutorialStep.ThirdDeparture: FocusGuidedControl(targets, "Start Voyage"); break;

                case GuidedTutorialStep.FirstReturn:
                case GuidedTutorialStep.ThirdReturn: FocusGuidedControl(targets, "Safe Return"); break;
                case GuidedTutorialStep.SecondRetreat: FocusGuidedControl(targets, "Retreat Battle"); break;

                case GuidedTutorialStep.FirstSettlement:
                case GuidedTutorialStep.SecondSettlement:
                case GuidedTutorialStep.ThirdSettlement: FocusGuidedControl(targets, "Return To Port"); break;
                case GuidedTutorialStep.DrawChart: FocusGuidedControl(targets, "Draw Sea Chart"); break;
                case GuidedTutorialStep.Tavern: FocusGuidedPortDestination(targets, "Tavern"); break;
                case GuidedTutorialStep.SecondPier:
                case GuidedTutorialStep.ThirdPier: FocusGuidedPortDestination(targets, "Voyage"); break;
                case GuidedTutorialStep.Warehouse: FocusGuidedPortDestination(targets, "Warehouse"); break;
                case GuidedTutorialStep.RecruitNavigator:
                    if (!FocusGuidedControl(targets, "Recruit Tavern Crew 0")) FocusGuidedControl(targets, "Tavern Crew 0");
                    break;
                case GuidedTutorialStep.SelectTreasure:
                    if (!FocusGuidedControl(targets, "Warehouse Treasure " + CoreWarehouseCollection.TutorialTreasureId))
                        FocusGuidedControl(targets, "Warehouse Tab Valuables");
                    break;
                case GuidedTutorialStep.DisplayTreasure:
                    FocusGuidedControl(targets, "Warehouse Display Slot " + selectedWarehouseDisplaySlot);
                    FocusGuidedControl(targets, "Warehouse Display Selected");
                    break;
                case GuidedTutorialStep.Shipyard:
                    if (!FocusGuidedControl(targets, "Shipyard Ship " + session.SelectedShipIndex))
                        FocusGuidedPortDestination(targets, "Shipyard");
                    break;
                case GuidedTutorialStep.ShipEquipment:
                    FocusGuidedControl(targets, "Shipyard Dossier Tab Equipment");
                    break;
                case GuidedTutorialStep.UnequipRewardComponent:
                    FocusGuidedControl(targets, "Shipyard Unequip Component Bow");
                    break;
                case GuidedTutorialStep.SelectRewardComponent:
                    OwnedShipComponent reward = GuidedTutorialOwnedReward();
                    if (reward != null) FocusGuidedControl(targets, "Shipyard Equipment Owned " + reward.InstanceId);
                    break;
                case GuidedTutorialStep.EquipRewardComponent:
                    FocusGuidedControl(targets, "Shipyard Equip Component");
                    break;
                case GuidedTutorialStep.TutorialBoss:
                    if (HasGuidedBossPresentation)
                    {
                        FocusGuidedControl(targets, GuidedBossTargetControl);
                        break;
                    }
                    // Suggest a usable card without choosing a strategy for the player.
                    // Other legal cards and maneuvers remain playable.
                    if (!FocusFirstGuidedPrefix(targets, "Battle Card ")) FocusGuidedControl(targets, "End Battle Turn");
                    break;
            }
        }

        private void FocusGuidedPortDestination(List<Selectable> targets, string facility)
        {
            if (!FocusGuidedControl(targets, "Port Hotspot " + facility)) FocusGuidedControl(targets, "Return To Port Hub");
            if (GuidedTutorialRecoveryActive && activePortFacility < 0)
            {
                FocusGuidedControl(targets, "Port Hotspot Maintenance");
                FocusGuidedControl(targets, "Port Hotspot Warehouse");
            }
        }

    }
}

