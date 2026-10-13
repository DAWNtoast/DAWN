using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>The complete game stays on a centered 16:9 surface at every window size.</summary>
    public static class FixedGameViewport
    {
        public const string Name = "Game Viewport";
        public static readonly Vector2 ReferenceSize = new Vector2(1920f, 1080f);

        public static RectTransform Bounds(Canvas canvas)
        {
            if (canvas == null) return null;
            Transform root = canvas.rootCanvas.transform;
            return root.Find(Name) as RectTransform ?? root as RectTransform;
        }

        public static RectTransform Create(Canvas canvas)
        {
            RectTransform existing = canvas.transform.Find(Name) as RectTransform;
            if (existing != null) return existing;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }

            // Remember pre-existing preview children before adding the frame.
            Transform[] children = new Transform[canvas.transform.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = canvas.transform.GetChild(i);

            var bars = new GameObject("Game Letterbox", typeof(RectTransform), typeof(Image));
            var barsRect = (RectTransform)bars.transform;
            barsRect.SetParent(canvas.transform, false);
            barsRect.anchorMin = Vector2.zero;
            barsRect.anchorMax = Vector2.one;
            barsRect.offsetMin = barsRect.offsetMax = Vector2.zero;
            bars.GetComponent<Image>().color = Color.black;
            bars.GetComponent<Image>().raycastTarget = false;
            barsRect.SetAsFirstSibling();

            var host = new GameObject(Name, typeof(RectTransform), typeof(RectMask2D));
            var viewport = (RectTransform)host.transform;
            viewport.SetParent(canvas.transform, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = Vector2.one * .5f;
            viewport.sizeDelta = ReferenceSize;
            viewport.anchoredPosition = Vector2.zero;
            foreach (Transform child in children)
            {
                // Older serialized custom graphics can predate their renderer
                // requirement. Supply it before enrolling them in the mask.
                foreach (MaskableGraphic graphic in child.GetComponentsInChildren<MaskableGraphic>(true))
                    if (graphic.GetComponent<CanvasRenderer>() == null)
                        graphic.gameObject.AddComponent<CanvasRenderer>();
                child.SetParent(viewport, false);
            }

            // Sorted child canvases (hovered cards, tutorial highlights) bypass
            // parent masks. Cover and intercept only the space outside the game.
            var overlayHost = new GameObject("Game Letterbox Overlay", typeof(RectTransform),
                typeof(Canvas), typeof(GraphicRaycaster));
            var overlayRect = (RectTransform)overlayHost.transform;
            overlayRect.SetParent(canvas.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            Canvas overlay = overlayHost.GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 32767;
            AddBar(overlayRect, "Top", new Vector2(0, .5f), Vector2.one,
                new Vector2(0, 540), Vector2.zero);
            AddBar(overlayRect, "Bottom", Vector2.zero, new Vector2(1, .5f),
                Vector2.zero, new Vector2(0, -540));
            AddBar(overlayRect, "Left", Vector2.zero, new Vector2(.5f, 1),
                Vector2.zero, new Vector2(-960, 0));
            AddBar(overlayRect, "Right", new Vector2(.5f, 0), Vector2.one,
                new Vector2(960, 0), Vector2.zero);
            return viewport;
        }

        private static void AddBar(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var host = new GameObject("Letterbox " + name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            host.GetComponent<Image>().color = Color.black;
            host.GetComponent<Image>().raycastTarget = true;
        }
    }
}
