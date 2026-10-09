using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static float WarehouseReadableRowHeight => 64f * GameReadability.Scale;
        private static float WarehouseReadableRowStep => WarehouseReadableRowHeight + 8f;

        private RectTransform CreateWarehouseReadableRows(RectTransform ledger, string prefix, int count, bool rare)
        {
            float height = rare ? 332f : 420f;
            RectTransform viewport = CreateRect(prefix + " Rows Viewport", ledger,
                new Vector2(630f, height), new Vector2(0f, 148f - height * .5f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            RectTransform content = CreateRect(prefix + " Rows Content", viewport,
                new Vector2(614f, Mathf.Max(height, count * WarehouseReadableRowStep)), Vector2.zero);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = new Vector2(-6f, 0f);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;
            Image track = CreateImage(prefix + " Scrollbar", viewport,
                new Vector2(8f, height), new Vector2(310f, 0f), new Color(.2f, .13f, .08f, .18f));
            RectTransform handle = CreateRect("Handle", track.transform, Vector2.zero, Vector2.zero);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            Image thumb = handle.gameObject.AddComponent<Image>();
            thumb.color = new Color(.2f, .13f, .08f, .7f);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle; bar.targetGraphic = thumb;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.SetValueWithoutNotify(1f);
            scroll.verticalScrollbar = bar;
            track.gameObject.SetActive(content.rect.height > height);
            return content;
        }

        private static void AnchorWarehouseReadableRow(Button row, int index)
        {
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -WarehouseReadableRowHeight * .5f - index * WarehouseReadableRowStep);
        }
    }
}
