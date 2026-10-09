using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static void ApplyComponentDossierReadingLayout(RectTransform detail, RectTransform[] pages, Button[] tabs)
        {
            detail.sizeDelta = new Vector2(1640, 760);
            detail.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Component Detail Parchment").rectTransform.sizeDelta = new Vector2(1580, 760);
            Text name = MaintenanceText(detail, "Component Detail Name"); SetMaintenanceReadingText(name, 32, true);
            name.rectTransform.sizeDelta = new Vector2(860, 70); name.rectTransform.anchoredPosition = new Vector2(300, 315);
            Text meta = MaintenanceText(detail, "Component Detail Meta"); SetMaintenanceReadingText(meta, 18);
            meta.rectTransform.sizeDelta = new Vector2(860, 40); meta.rectTransform.anchoredPosition = new Vector2(300, 257);
            for (int i = 0; i < pages.Length; i++)
            {
                RectTransform tab = (RectTransform)tabs[i].transform;
                tab.sizeDelta = new Vector2(200, 64); tab.anchoredPosition = new Vector2(-30 + i * 220, 198);
                Text tabLabel = tabs[i].GetComponentInChildren<Text>(); SetMaintenanceReadingText(tabLabel, 22);
                tabLabel.rectTransform.sizeDelta = new Vector2(188, 56);
                RectTransform page = pages[i]; page.sizeDelta = new Vector2(860, 350); page.anchoredPosition = new Vector2(300, -25);
                Text[] text = page.GetComponentsInChildren<Text>(true).OrderByDescending(label => label.transform.position.y).ToArray();
                ScrollRect[] previousScrolls = page.GetComponentsInChildren<ScrollRect>(true);
                RawImage[] pictures = page.GetComponentsInChildren<RawImage>(true);
                ScrollRect scroll = MaintenanceReadingScroll(page.name + " Reading Scroll", page, page.rect.size, Vector2.zero);
                float top = 0;
                foreach (RawImage picture in pictures)
                {
                    RectTransform rect = picture.rectTransform; rect.SetParent(scroll.content, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f, 1f);
                    rect.anchoredPosition = new Vector2(0, -top); top += rect.rect.height + 12;
                }
                foreach (Text label in text)
                    top = FlowMaintenanceText(label, scroll.content, top,
                        label.name.EndsWith("Growth", StringComparison.Ordinal) || label.name.EndsWith("Stage", StringComparison.Ordinal) ? 18 : 22);
                foreach (ScrollRect old in previousScrolls) old.gameObject.SetActive(false);
                FinishMaintenanceScroll(scroll, top);
            }
            foreach (Button button in detail.GetComponentsInChildren<Button>(true).Where(button => button.name == "Read Component Story Chapter" ||
                button.name == "Back To Component Shop" || button.name.StartsWith("Purchase Component ", StringComparison.Ordinal)))
            {
                RectTransform rect = (RectTransform)button.transform; rect.sizeDelta = new Vector2(rect.rect.width, 96);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -280);
                Text text = button.GetComponentInChildren<Text>(); SetMaintenanceReadingText(text, 22);
                text.rectTransform.sizeDelta = rect.sizeDelta - new Vector2(20, 8);
            }
        }

        private static void ApplyComponentShopReadingLayout(RectTransform panel)
        {
            Text rule = CurrentShopChildren<Text>(panel).Single(text => text.name == "Component Shop Regional Rule");
            SetMaintenanceReadingText(rule, 18);
            foreach (Button button in CurrentShopChildren<Button>(panel))
            {
                bool previous = button.name == "Component Shop Previous Display";
                bool next = button.name == "Component Shop Next Display";
                bool resale = button.name == "Open Component Resale";
                if (!previous && !next && !resale) continue;
                RectTransform rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(resale ? 340 : 200, 64);
                rect.anchoredPosition = new Vector2(resale ? 650 : previous ? -280 : 280, 265);
                Text label = button.GetComponentInChildren<Text>(); SetMaintenanceReadingText(label, 22);
                label.rectTransform.sizeDelta = rect.sizeDelta - new Vector2(16, 8);
            }
            Text page = CurrentShopChildren<Text>(panel).SingleOrDefault(text => text.name == "Component Shop Display Page");
            if (page != null)
            {
                SetMaintenanceReadingText(page, 18);
                page.rectTransform.sizeDelta = new Vector2(280, 64); page.rectTransform.anchoredPosition = new Vector2(0, 265);
            }
            foreach (Text suitability in CurrentShopChildren<Text>(panel).Where(text => text.name.StartsWith("Component Shop Offer Suitability ", StringComparison.Ordinal)))
            {
                SetMaintenanceReadingText(suitability, 18);
                suitability.rectTransform.sizeDelta = new Vector2(310, 72);
                suitability.rectTransform.anchoredPosition = new Vector2(0, 174);
            }
            foreach (RectTransform writing in CurrentShopChildren<RectTransform>(panel).Where(rect => rect.name.StartsWith("Component Shop Plaque Writing Area ", StringComparison.Ordinal)).ToArray())
            {
                // The timber, component model and offer hit area keep their authored
                // positions. Text uses its full chosen size within that writing bay.
                Text[] text = writing.GetComponentsInChildren<Text>(true);
                ScrollRect scroll = MaintenanceReadingScroll("Component Plaque Reading Scroll", writing, writing.rect.size, Vector2.zero);
                float top = 0;
                foreach (Text label in text.OrderByDescending(label => label.rectTransform.anchoredPosition.y))
                    top = FlowMaintenanceText(label, scroll.content, top,
                        label.name.StartsWith("Component Shop Plaque Name ", StringComparison.Ordinal) ? 22 : 18);
                FinishMaintenanceScroll(scroll, top);
            }
        }

        private static IEnumerable<T> CurrentShopChildren<T>(RectTransform panel) where T : Component
        {
            // ClearChildren retires old direct children immediately, then Destroy
            // removes them at frame end. Include only the newly built generation.
            // Check activeSelf at the panel boundary: a disposable preview host
            // itself may be inactive while its new UI is being constructed.
            return panel.GetComponentsInChildren<T>(true).Where(component =>
            {
                Transform child = component.transform;
                while (child != null && child.parent != panel) child = child.parent;
                return child != null && child.gameObject.activeSelf;
            });
        }
    }
}
