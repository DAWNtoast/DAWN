using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static RectTransform ReadingRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var host = new GameObject(name, typeof(RectTransform));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static ScrollRect MaintenanceReadingScroll(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            RectTransform root = ReadingRect(name, parent, size, position);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = ReadingRect(name + " Viewport", root, size - new Vector2(24, 0), new Vector2(-12, 0));
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = ReadingRect(name + " Content", viewport, viewport.sizeDelta, Vector2.zero);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = false; scroll.scrollSensitivity = 42f;
            RectTransform rail = ReadingRect(name + " Scrollbar", root, new Vector2(14, size.y), new Vector2(size.x * .5f - 7, 0));
            rail.gameObject.AddComponent<Image>().color = new Color(.18f, .10f, .04f, .18f);
            RectTransform handle = ReadingRect("Handle", rail, new Vector2(14, 32), Vector2.zero);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.sizeDelta = Vector2.zero;
            Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.30f, .18f, .06f, .8f);
            var bar = rail.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = bar;
            return scroll;
        }

        private static Text MaintenanceText(Transform root, string name) =>
            root.GetComponentsInChildren<Text>(true).Single(text => text.name == name);

        private static void SetMaintenanceReadingText(Text text, int size, bool heading = false)
        {
            if (heading) GameReadability.Heading(text, size); else GameReadability.Body(text, size);
            text.lineSpacing = 1f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static float FlowMaintenanceText(Text text, RectTransform content, float top, int size, bool heading = false)
        {
            SetMaintenanceReadingText(text, size, heading);
            RectTransform rect = text.rectTransform;
            rect.SetParent(content, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(content.rect.width - 16f, 1000f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, MeasureMaintenanceTextHeight(text));
            // Legacy UGUI rounds the embedded CJK ascenders in physical pixels.
            // At a 1/3 canvas scale their mesh can rise six design points above
            // an UpperLeft Text rect, even though preferredHeight fits. Reserve
            // real space before every paragraph, including the first one, just
            // as the shared card-effect layout does; do not crop or shrink it.
            const float glyphTopPadding = 12f;
            rect.anchoredPosition = new Vector2(0, -top - glyphTopPadding);
            return top + glyphTopPadding + rect.rect.height + 12f;
        }

        private static float MeasureMaintenanceTextHeight(Text text)
        {
            // Preview construction can precede the final CanvasScaler update.
            // Dynamic-font pixel rounding at 1080p can wrap one more line than
            // the initial scale or 720p. Reserve that line at the actual width,
            // with a small horizontal allowance, before positioning later rows.
            float height = text.preferredHeight;
            TextGenerationSettings settings = text.GetGenerationSettings(
                new Vector2(Mathf.Max(1f, text.rectTransform.rect.width - 16f), 0f));
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var generator = new TextGenerator())
                foreach (float scale in new[] { text.pixelsPerUnit, 1f, 2f / 3f })
                {
                    settings.scaleFactor = scale;
                    height = Mathf.Max(height, generator.GetPreferredHeight(text.text, settings) / scale);
                }
            return Mathf.Ceil(height) + 8f;
        }

        private static void FinishMaintenanceScroll(ScrollRect scroll, float height)
        {
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.rect.height, height));
            scroll.content.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }

        private static void FitMaintenanceBoardText(RectTransform supply)
        {
            // The writing face keeps its authored bounds. Orders scroll inside it;
            // checkout controls and the timber notice remain independently reachable.
            var labels = supply.GetComponentsInChildren<Text>(true);
            ScrollRect scroll = MaintenanceReadingScroll("Maintenance Reading Scroll", supply, new Vector2(516, 240), new Vector2(0, 48));
            float top = FlowMaintenanceText(labels.Single(t => t.name == "Maintenance Supply Title"), scroll.content, 0, 32, true);
            top = FlowMaintenanceText(labels.Single(t => t.name == "Maintenance Supply Instruction"), scroll.content, top, 18);
            RectTransform[] rows = supply.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name.StartsWith("Supply Row ", System.StringComparison.Ordinal)).ToArray();
            foreach (RectTransform row in rows)
            {
                row.SetParent(scroll.content, false); row.anchorMin = row.anchorMax = row.pivot = new Vector2(.5f, 1f);
                row.sizeDelta = new Vector2(scroll.content.rect.width - 8, 1000);
                Text label = row.GetComponentsInChildren<Text>(true).Single(t => t.name.StartsWith("Supply Label ", System.StringComparison.Ordinal));
                float labelHeight = FlowMaintenanceText(label, row, 8, 22);
                float rowHeight = labelHeight + 78;
                row.sizeDelta = new Vector2(row.sizeDelta.x, rowHeight); row.anchoredPosition = new Vector2(0, -top);
                foreach (Button button in row.GetComponentsInChildren<Button>(true))
                {
                    RectTransform rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, .5f);
                    rect.sizeDelta = new Vector2(48, 64);
                    rect.anchoredPosition = new Vector2(button.name.Contains("Decrease") ? -208 : -40, -labelHeight - 34);
                    Text glyph = button.GetComponentInChildren<Text>(); SetMaintenanceReadingText(glyph, 22);
                    glyph.rectTransform.sizeDelta = new Vector2(44, 60);
                }
                foreach (Text text in row.GetComponentsInChildren<Text>(true).Where(t => t.name.StartsWith("Supply Purchase Amount ") || t.name.StartsWith("Supply Line Cost ")))
                {
                    SetMaintenanceReadingText(text, 22);
                    RectTransform rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
                    rect.pivot = new Vector2(.5f, .5f);
                    bool amount = text.name.StartsWith("Supply Purchase Amount ");
                    rect.sizeDelta = new Vector2(amount ? 112 : 240, 70);
                    rect.anchoredPosition = new Vector2(amount ? -124 : 112, -labelHeight - 34);
                }
                top += rowHeight + 12;
            }
            top = FlowMaintenanceText(labels.Single(t => t.name == "Supply Purchase Summary"), scroll.content, top, 18);
            FinishMaintenanceScroll(scroll, top);
            int index = 0;
            foreach (string name in new[] { "Clear Supply Order", "Fill Standard Supply Order", "Confirm Supply Purchase" })
            {
                Button button = supply.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
                var rect = (RectTransform)button.transform; rect.sizeDelta = new Vector2(164, 76);
                rect.anchoredPosition = new Vector2(-174 + index++ * 174, -126);
                Text text = button.GetComponentInChildren<Text>(); SetMaintenanceReadingText(text, 22);
                text.rectTransform.sizeDelta = new Vector2(156, 72);
            }
            Text notice = labels.Single(t => t.name == "Maintenance Notice");
            ScrollRect noticeScroll = MaintenanceReadingScroll("Maintenance Notice Reading Scroll", (RectTransform)notice.transform.parent,
                new Vector2(464, 40), Vector2.zero);
            FinishMaintenanceScroll(noticeScroll, FlowMaintenanceText(notice, noticeScroll.content, 0, 18));
        }
    }
}
