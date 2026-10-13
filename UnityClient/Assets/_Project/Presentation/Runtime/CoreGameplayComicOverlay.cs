using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform CreateComicOverlay(string name)
        {
            // Phase Content is a fixed design surface in battle. Its parent is
            // the actual canvas, which also covers wide and tall windows.
            RectTransform overlay = CreateStretchPanel(name, contentRoot.parent, Color.black);
            Canvas canvas = overlay.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1600; // Above transient achievement notices as well as the battle hand.
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            overlay.GetComponent<Image>().raycastTarget = true;
            if (Application.isPlaying && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            return overlay;
        }

        private static ComicPageGraphic CreateComicPage(string name, RectTransform overlay, Texture texture)
        {
            RectTransform rect = CreateRect(name, overlay, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            ComicPageGraphic page = rect.gameObject.AddComponent<ComicPageGraphic>();
            page.texture = texture;
            page.raycastTarget = false;
            page.SetPanel(new Rect(0f, 0f, 1f, 1f));
            return page;
        }

        private static Button CreateComicAdvance(string name, RectTransform overlay)
        {
            RectTransform rect = CreateRect(name, overlay, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            Image surface = rect.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            Button advance = rect.gameObject.AddComponent<Button>();
            advance.targetGraphic = surface;
            advance.transition = Selectable.Transition.None;
            advance.navigation = new Navigation { mode = Navigation.Mode.None };
            return advance;
        }

        private void CloseComicOverlay(RectTransform overlay)
        {
            if (overlay == null) return;
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(overlay)) EventSystem.current.SetSelectedGameObject(null);
            overlay.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
        }
    }
}
