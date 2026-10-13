using System.Collections.Generic;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class DeckBuilderNavigationSnapshot
        {
            public Button Button;
            public bool Interactable;
            public Button.ButtonClickedEvent Click;
        }

        private readonly List<DeckBuilderNavigationSnapshot> deckBuilderNavigationSnapshots =
            new List<DeckBuilderNavigationSnapshot>();

        private void RestoreDeckBuilderNavigationControls()
        {
            // Restore before the tutorial restores its own controls. Retained port
            // headers must recover their original state once the draft is valid.
            foreach (DeckBuilderNavigationSnapshot snapshot in deckBuilderNavigationSnapshots)
            {
                if (snapshot.Button == null) continue;
                snapshot.Button.interactable = snapshot.Interactable;
                snapshot.Button.onClick = snapshot.Click;
            }
            deckBuilderNavigationSnapshots.Clear();
        }

        private void ApplyDeckBuilderNavigationLock()
        {
            if (!DeckBuilderNeedsMoreCards || contentRoot == null) return;
            // Leave card editing, collection paging, and all scroll controls usable.
            // Apply after tutorial overlays so every other visible button is covered.
            foreach (Button button in contentRoot.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy || IsDeckBuilderEditingButton(button.name)) continue;
                var snapshot = new DeckBuilderNavigationSnapshot
                {
                    Button = button,
                    Interactable = button.interactable,
                    Click = button.onClick,
                };
                deckBuilderNavigationSnapshots.Add(snapshot);
                button.interactable = false;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() =>
                {
                    if (BlockIncompleteDeckBuilderNavigation()) return;
                    if (snapshot.Button != null && snapshot.Button.isActiveAndEnabled && snapshot.Button.IsInteractable())
                        snapshot.Click.Invoke();
                });
            }
        }

        private static bool IsDeckBuilderEditingButton(string name) =>
            name.StartsWith("Add Deck Card ", System.StringComparison.Ordinal) ||
            name.StartsWith("Remove Deck Card ", System.StringComparison.Ordinal) ||
            name == "Previous Battle Card Collection Page" || name == "Next Battle Card Collection Page";
    }
}
