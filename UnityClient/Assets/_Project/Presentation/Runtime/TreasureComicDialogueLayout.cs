using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Places editable artistic lettering inside the balloons already painted on each page.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TreasureComicDialogueLayout : MonoBehaviour
    {
        private ComicPageGraphic page;
        private IReadOnlyList<TreasureComicDialogue> dialogue;
        private Text[] labels;
        private Rect lastUv;
        private Vector2 lastSize;
        private bool layoutDirty;

        public void Configure(ComicPageGraphic page, IReadOnlyList<TreasureComicDialogue> dialogue)
        {
            if (labels != null)
                foreach (Text label in labels)
                    if (label != null)
                    {
                        label.gameObject.SetActive(false);
                        if (Application.isPlaying) Destroy(label.gameObject);
                        else DestroyImmediate(label.gameObject);
                    }

            this.page = page;
            this.dialogue = dialogue;
            labels = new Text[dialogue.Count];
            for (int index = 0; index < dialogue.Count; index++)
            {
                var lettering = new GameObject("Treasure Comic Dialogue " + (index + 1), typeof(RectTransform));
                lettering.layer = page.gameObject.layer;
                lettering.transform.SetParent(page.transform, false);
                Text label = lettering.AddComponent<Text>();
                label.font = GameTypography.InterfaceFont;
                label.fontStyle = FontStyle.Normal;
                label.text = dialogue[index].Text;
                label.color = new Color32(49, 30, 19, 255);
                label.alignment = TextAnchor.MiddleCenter;
                label.lineSpacing = 1f;
                label.supportRichText = false;
                label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                labels[index] = label;
            }
            layoutDirty = true;
            RefreshLayout();
        }

        private void OnEnable() => Canvas.willRenderCanvases += RefreshLayout;
        private void OnDisable() => Canvas.willRenderCanvases -= RefreshLayout;

        public void RefreshLayout()
        {
            if (page == null || labels == null) return;
            Rect uv = page.uvRect;
            Vector2 size = page.rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f || uv.width <= 0f || uv.height <= 0f) return;
            if (!layoutDirty && lastUv == uv && lastSize == size) return;
            layoutDirty = false;
            lastUv = uv;
            lastSize = size;

            // Size the font uniformly even when the complete page adapts to a narrower window.
            float scale = Mathf.Min(size.x / (1672f * uv.width), size.y / (941f * uv.height));
            int maximum = Mathf.Max(1, Mathf.RoundToInt(26f * scale));
            int minimum = Mathf.Clamp(Mathf.RoundToInt(9f * scale), 1, maximum);
            for (int index = 0; index < labels.Length; index++)
            {
                Text label = labels[index];
                Rect bounds = dialogue[index].Bounds;
                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2((bounds.xMin - uv.xMin) / uv.width, (bounds.yMin - uv.yMin) / uv.height);
                rect.anchorMax = new Vector2((bounds.xMax - uv.xMin) / uv.width, (bounds.yMax - uv.yMin) / uv.height);
                // Leave room for the display face's pointed strokes beyond its line metrics.
                Vector2 padding = Vector2.one * (3f * scale);
                rect.offsetMin = padding;
                rect.offsetMax = -padding;
                label.fontSize = maximum;
                label.resizeTextMinSize = minimum;
                label.resizeTextMaxSize = maximum;
            }
        }
    }
}
