using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform fullWindowFacilityViewportParent;
        private GameObject fullWindowFacilityLetterbox;
        private bool fullWindowFacilityLetterboxWasActive;

        // These paper-based workspaces use all available window space. Painted
        // facilities return to their authored viewport when the page changes.
        private void ExpandFacilityToWindow()
        {
            if (fullWindowFacilityViewportParent != null || contentRoot == null) return;
            Canvas canvas = contentRoot.GetComponentInParent<Canvas>().rootCanvas;
            fullWindowFacilityViewportParent = contentRoot.parent as RectTransform;
            contentRoot.SetParent(canvas.transform, false);
            Transform letterbox = canvas.transform.Find("Game Letterbox Overlay");
            if (letterbox != null)
            {
                fullWindowFacilityLetterbox = letterbox.gameObject;
                fullWindowFacilityLetterboxWasActive = letterbox.gameObject.activeSelf;
                letterbox.gameObject.SetActive(false);
            }
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = contentRoot.offsetMax = Vector2.zero;
        }

        private void RestoreFullWindowFacilityViewport()
        {
            if (fullWindowFacilityViewportParent != null && contentRoot != null)
            {
                contentRoot.SetParent(fullWindowFacilityViewportParent, false);
                contentRoot.anchorMin = contentRoot.anchorMax = Vector2.one * .5f;
                contentRoot.sizeDelta = PhaseSurfaceLayout.ContentSize;
                contentRoot.anchoredPosition = Vector2.zero;
            }
            fullWindowFacilityViewportParent = null;
            if (fullWindowFacilityLetterbox != null)
                fullWindowFacilityLetterbox.SetActive(fullWindowFacilityLetterboxWasActive);
            fullWindowFacilityLetterbox = null;
        }
    }
}
