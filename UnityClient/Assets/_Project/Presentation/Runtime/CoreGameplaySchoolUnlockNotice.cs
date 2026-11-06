using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Keep the shared return/lifecycle/navigation hooks compatible while the
        // former level-based card reward notice is retired. Level rewards other
        // than cards are handled by their existing progression surfaces.
        private GameObject schoolUnlockNotice;

        private void ShowSchoolUnlockNotice(int previousLevel)
        {
            // Ordinary school cards now come from packs, never from level gains.
            DismissSchoolUnlockNotice();
        }

        private void AdvanceSchoolUnlockNotice() => DismissSchoolUnlockNotice();

        private void DismissSchoolUnlockNotice()
        {
            if (schoolUnlockNotice == null) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(schoolUnlockNotice.transform))
                EventSystem.current.SetSelectedGameObject(null);
            schoolUnlockNotice.SetActive(false);
            if (Application.isPlaying) Destroy(schoolUnlockNotice);
            else DestroyImmediate(schoolUnlockNotice);
            schoolUnlockNotice = null;
        }
    }
}
