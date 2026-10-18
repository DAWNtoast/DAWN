using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void HandleEscapeNavigation()
        {
            if (!UiEscapeInput.TryConsume() || profileSaveBlocked || returnMenuSaving || contentRoot == null) return;
            if (schoolUnlockNotice != null && schoolUnlockNotice.activeInHierarchy)
            {
                AdvanceSchoolUnlockNotice();
                return;
            }
            if (DismissEscapeOverlay(warehouseIncomeOverlay, "Close Warehouse Income")) return;

            // A gesture is the innermost interaction. Cancel it without editing
            // the deck or returning from the page, regardless of Update order.
            foreach (DeckBuilderCardDrag card in contentRoot.GetComponentsInChildren<DeckBuilderCardDrag>())
                if (card.TryCancelGesture()) return;
            foreach (WarehouseTreasureDrag treasure in contentRoot.GetComponentsInChildren<WarehouseTreasureDrag>())
                if (treasure.TryCancelGesture()) return;

            // F10 can place the tool window above a settings confirmation.
            if (DismissEscapeOverlay(gameMasterOverlay, "Close Game Master")) return;
            if (DismissEscapeOverlay(returnMenuConfirmation, "Cancel Return To Main Menu")) return;
            if (DismissEscapeOverlay(portSettingsOverlay, "Close Port Settings")) return;
            if (DismissEscapeOverlay(battleDeckOverlay, "Close Battle Deck")) return;
            if (DismissEscapeOverlay(voyageCargoOverlay, "Close Voyage Cargo")) return;
            if (DismissEscapeOverlay(boardingCardChoiceOverlay, "Cancel Boarding Card Choice")) return;
            if (IsNavalCardChoiceOpen) { CloseNavalCardChoice(); return; }

            // Story choices, battles, settlement and mandatory encounters require
            // their own explicit actions. Escape must never confirm or skip them.
            if (session == null || session.Phase != CoreDemoPhase.Port || navigationMotionPlaying ||
                battleAnimationPlaying || IsEscapeLayerActive(openingComicOverlay) ||
                IsEscapeLayerActive(treasureStoryOverlay) || IsEscapeLayerActive(ramComicOverlay) ||
                IsEscapeLayerActive(boardingTransitionOverlay)) return;

            if (DismissEscapeOverlay(departureChartWarning, "Close Chart Seed Warning")) return;
            if (DismissEscapeOverlay(departureTreasureSeedPicker, "Close Departure Treasure Seed Selection")) return;
            if (DismissEscapeOverlay(departureBasicSupplyOverlay, "Departure Basic Supplies Close")) return;
            if (DismissEscapeOverlay(warehouseCargoModal, "Cancel Warehouse Cargo Adjustment",
                    "Cancel Warehouse Cargo Sale", "Cancel Warehouse Item Actions")) return;
            if (UiEscapeInput.TryCloseDropdown(contentRoot)) return;

            if (BlockIncompleteDeckBuilderNavigation()) return;

            // These are cancellation/navigation controls only. Invoke the live
            // button so tutorial locks, quote identities and return contexts all
            // use exactly the same validation as a pointer click.
            if (TryInvokeEscapeButton(contentRoot, "Close Shipyard Equipment", "Cancel Component Sale", "Cancel Market Purchase",
                    "Close Market Shop", "Treasure Folio Back", "Treasure Close Folio",
                    "Back To Card Workshop Chests", "Back From Card Upgrades",
                    "Back To Component Shop Sales", "Back To Voyage Setup", "Return To Port Investment")) return;
            if (warehouseCatalogOpen && TryInvokeEscapeButton(contentRoot, "Warehouse Collection Catalog")) return;
            TryInvokeEscapeButton(contentRoot, "Return To Port Hub");
        }

        private static bool IsEscapeLayerActive(Transform layer) => layer != null && layer.gameObject.activeInHierarchy;

        private static bool DismissEscapeOverlay(Transform overlay, params string[] buttonNames)
        {
            if (!IsEscapeLayerActive(overlay)) return false;
            TryInvokeEscapeButton(overlay, buttonNames);
            // A modal with a disabled close control still owns the key. Never
            // fall through to the page behind it when closing is prohibited.
            return true;
        }

        private static bool TryInvokeEscapeButton(Transform root, params string[] buttonNames)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>();
            foreach (string name in buttonNames)
                foreach (Button button in buttons)
                {
                    if (button.name != name || !button.gameObject.activeInHierarchy) continue;
                    if (button.IsActive() && button.IsInteractable()) button.onClick.Invoke();
                    return true;
                }
            return false;
        }
    }
}
