using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Fits the equipment work areas to the paper while keeping art and text proportional.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ShipyardEquipmentLayout : MonoBehaviour
    {
        private bool applying;
        private Vector2 appliedSize;
        // Bow, sail, cannon, hull and navigation: normalized points on the
        // displayed ship art and on the matching card's outward edge.
        private static readonly Vector2[] ShipAttachmentPoints =
        {
            new Vector2(.78f, .40f), new Vector2(.52f, .76f), new Vector2(.30f, .25f),
            new Vector2(.42f, .14f), new Vector2(.21f, .42f),
        };
        private static readonly Vector2[] SlotLinkStarts =
        {
            new Vector2(.80f, 1f), new Vector2(.35f, 0f), new Vector2(.35f, 1f),
            new Vector2(.60f, 1f), new Vector2(.20f, 0f),
        };

        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();
        private void LateUpdate()
        {
            if (((RectTransform)transform).rect.size != appliedSize) Apply();
        }

        public void Apply()
        {
            if (applying || !isActiveAndEnabled) return;
            RectTransform detail = (RectTransform)transform;
            RectTransform page = Child(detail, "Shipyard Equipment Page");
            if (page == null || detail.rect.width <= 0f || detail.rect.height <= 112f) return;
            applying = true;
            try
            {
                Rect paper = detail.rect;
                const float inset = 18f;
                Rect region = new Rect(paper.xMin + inset, paper.yMin + inset,
                    paper.width - inset * 2f, paper.height - inset * 2f);
                float scale = Mathf.Min(region.width / 1850f, region.height / 922f);
                Box(page, region.size / scale, region.center);
                page.localScale = new Vector3(scale, scale, 1f);
                Rect bounds = page.rect;
                const float gap = 24f;
                float leftWidth = (bounds.width - gap) * .52f;
                Rect vessel = new Rect(bounds.xMin, bounds.yMin, leftWidth, bounds.height);
                Rect right = new Rect(vessel.xMax + gap, bounds.yMin,
                    bounds.width - leftWidth - gap, bounds.height);
                LayoutVessel(page, vessel);
                LayoutEquipment(page, right);
                appliedSize = detail.rect.size;
            }
            finally { applying = false; }
        }

        private static void LayoutVessel(RectTransform page, Rect area)
        {
            Panel(Child(page, "Shipyard Equipment Vessel Surface"), area);
            float sideOffset = Mathf.Min(area.width * .25f, 240f);
            float left = area.xMin + sideOffset, right = area.xMax - sideOffset;
            float upper = area.yMax - 72f, lower = area.yMin + 220f;
            Vector2[] positions = { new Vector2(right, lower), new Vector2(right, upper),
                new Vector2(left, lower), new Vector2(area.center.x, area.yMin + 72f), new Vector2(left, upper) };
            RectTransform[] slots = new RectTransform[positions.Length];
            int rowIndex = 0;
            foreach (RectTransform row in page)
            {
                if (!row.name.StartsWith("Shipyard Equipment Slot ") || row.GetComponent<Button>() == null) continue;
                slots[rowIndex] = row;
                Vector2 position = positions[rowIndex++];
                Box(row, new Vector2(360f, 128f), position);
            }
            RectTransform window = Child(page, "Shipyard Dossier Ship Window");
            float shipBottom = lower + 76f, shipTop = upper - 76f;
            Box(window, new Vector2(area.width - 40f, Mathf.Max(100f, shipTop - shipBottom)),
                new Vector2(area.center.x, (shipBottom + shipTop) * .5f));
            RawImage ship = window == null ? null : window.GetComponentInChildren<RawImage>();
            if (ship != null)
            {
                RectTransform art = (RectTransform)ship.transform.parent;
                Vector2 available = window.rect.size - new Vector2(24f, 24f);
                Vector2 source = ship.rectTransform.rect.size;
                float fit = Mathf.Min(available.x / source.x, available.y / source.y);
                art.anchoredPosition = Vector2.zero;
                art.localScale = new Vector3(fit, fit, 1f);
            }
            for (int i = 0; i < positions.Length; i++)
            {
                RectTransform link = Child(page, "Shipyard Equipment Slot Link " + i);
                if (link == null || slots[i] == null || ship == null) continue;
                Vector2 start = NormalizedPointIn(page, slots[i], SlotLinkStarts[i]);
                Vector2 end = NormalizedPointIn(page, ship.rectTransform, ShipAttachmentPoints[i]);
                Vector2 delta = end - start;
                Box(link, new Vector2(delta.magnitude, 2f), (start + end) * .5f);
                link.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        private static Vector2 NormalizedPointIn(RectTransform parent, RectTransform target, Vector2 point)
        {
            Rect rect = target.rect;
            return parent.InverseTransformPoint(target.TransformPoint(new Vector2(
                rect.xMin + rect.width * point.x, rect.yMin + rect.height * point.y)));
        }

        private static void LayoutEquipment(RectTransform page, Rect area)
        {
            const float gap = 20f, categoryHeight = 68f;
            float categoryWidth = (area.width - 4f * 10f) / 5f;
            int categoryIndex = 0;
            foreach (RectTransform category in page)
            {
                if (!category.name.StartsWith("Shipyard Equipment Category ")) continue;
                Box(category, new Vector2(categoryWidth, categoryHeight),
                    new Vector2(area.xMin + categoryWidth * .5f + categoryIndex++ * (categoryWidth + 10f), area.yMax - categoryHeight * .5f));
                Box(Child(category, "Label"), new Vector2(categoryWidth - 60f, 50f), new Vector2(25f, 0f));
                foreach (RectTransform icon in category)
                    if (icon.name.StartsWith("Shipyard Equipment Category Art "))
                        icon.anchoredPosition = new Vector2(-categoryWidth * .5f + 35f, 0f);
            }
            RectTransform help = Child(page, "Shipyard Equipment Help");
            float helpHeight = help == null ? 0f : 64f;
            if (help != null) Box(help, new Vector2(area.width, 54f), new Vector2(area.center.x, area.yMin + 27f));
            float bottom = area.yMin + helpHeight;
            float height = area.height - categoryHeight - gap - helpHeight;
            float inventoryWidth = (area.width - gap * 2f) * .29f;
            Rect inventoryArea = new Rect(area.xMin, bottom, inventoryWidth, height);
            RectTransform inventory = Child(page, "Shipyard Equipment Inventory");
            Panel(inventory, inventoryArea);
            LayoutInventory(inventory);
            float cardWidth = (area.width - inventoryWidth - gap * 2f) * .5f;
            float cardHeight = height - 80f;
            float comparisonLeft = inventoryArea.xMax + gap;
            LayoutComparison(Child(page, "Shipyard Equipment Current Comparison"),
                new Rect(comparisonLeft, bottom + 80f, cardWidth, cardHeight));
            LayoutComparison(Child(page, "Shipyard Equipment Selected Comparison"),
                new Rect(comparisonLeft + cardWidth + gap, bottom + 80f, cardWidth, cardHeight));
            RectTransform equip = Child(page, "Shipyard Equip Component");
            Box(equip, new Vector2(cardWidth, 60f),
                new Vector2(comparisonLeft + cardWidth * 1.5f + gap, bottom + 30f));
            Box(Child(equip, "Label"), equip.sizeDelta - new Vector2(24f, 10f), Vector2.zero);
            foreach (RectTransform remove in page)
            {
                if (!remove.name.StartsWith("Shipyard Unequip Component ")) continue;
                Box(remove, new Vector2(cardWidth, 60f), new Vector2(comparisonLeft + cardWidth * .5f, bottom + 30f));
                Box(Child(remove, "Label"), remove.sizeDelta - new Vector2(24f, 10f), Vector2.zero);
            }
        }

        private static void LayoutInventory(RectTransform inventory)
        {
            if (inventory == null) return;
            Rect area = inventory.rect;
            Box(Child(inventory, "Shipyard Equipment Inventory Heading"), new Vector2(area.width - 92f, 32f),
                new Vector2(-32f, area.yMax - 30f));
            RectTransform all = Child(inventory, "Shipyard Equipment All Components");
            Box(all, new Vector2(56f, 32f), new Vector2(area.xMax - 42f, area.yMax - 30f));
            Box(Child(all, "Label"), new Vector2(48f, 28f), Vector2.zero);
            Box(Child(inventory, "Shipyard Equipment Previous Page"), new Vector2(48f, 36f),
                new Vector2(area.xMin + 38f, area.yMin + 26f));
            Box(Child(inventory, "Shipyard Equipment Next Page"), new Vector2(48f, 36f),
                new Vector2(area.xMax - 38f, area.yMin + 26f));
            Box(Child(inventory, "Shipyard Equipment Page Number"), new Vector2(74f, 32f), new Vector2(0f, area.yMin + 26f));
            int index = 0;
            float rowHeight = Mathf.Min(172f, (area.height - 146f) / 4f);
            foreach (RectTransform row in inventory)
            {
                if (!row.name.StartsWith("Shipyard Equipment Owned ")) continue;
                Box(row, new Vector2(area.width - 28f, rowHeight),
                    new Vector2(0f, area.yMax - 58f - rowHeight * .5f - index++ * (rowHeight + 10f)));
                foreach (RectTransform item in row)
                {
                    if (item.name.StartsWith("Shipyard Inventory Art "))
                    {
                        Vector2 bounds = new Vector2(row.rect.width - 28f, rowHeight - 62f);
                        item.anchoredPosition = new Vector2(0f, rowHeight * .5f - 12f - bounds.y * .5f);
                        UiImageAspect.Fit(item.GetComponent<RawImage>(), bounds);
                    }
                    else if (item.name.StartsWith("Shipyard Inventory Name "))
                    {
                        Box(item, new Vector2(row.rect.width - 24f, 30f), new Vector2(0f, -rowHeight * .5f + 27f));
                        item.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
                    }
                }
            }
        }

        private static void LayoutComparison(RectTransform card, Rect area)
        {
            if (card == null) return;
            Panel(card, area);
            string prefix = card.name;
            float width = area.width, height = area.height, top = height * .5f;
            const float componentHeight = 208f;
            float readingHeight = height - componentHeight;
            float dividerY = -height * .5f + readingHeight;
            Box(Child(card, prefix + " Heading Surface"), new Vector2(width - 2f, 60f), new Vector2(0f, top - 30f));
            Box(Child(card, prefix + " Heading"), new Vector2(width - 40f, 38f), new Vector2(0f, top - 30f));
            Box(Child(card, prefix + " Reading Surface"), new Vector2(width - 2f, readingHeight), new Vector2(0f, -height * .5f + readingHeight * .5f));
            Box(Child(card, prefix + " Divider"), new Vector2(width - 2f, 2f), new Vector2(0f, dividerY));
            RectTransform board = Child(card, prefix + " Component Board");
            Box(board, new Vector2(width - 32f, 124f), new Vector2(0f, top - 134f));
            RectTransform icon = Child(board, prefix + " Art");
            if (icon != null)
            {
                icon.anchoredPosition = Vector2.zero;
                UiImageAspect.Fit(icon.GetComponent<RawImage>(), new Vector2(width - 60f, 106f));
            }
            Box(Child(board, prefix + " Empty Art"), new Vector2(width - 56f, 80f), Vector2.zero);
            const float statsHeight = 190f;
            float attachedHeight = readingHeight - statsHeight - 30f;
            float attachedTop = -height * .5f + attachedHeight;
            Box(Child(card, prefix + " Attached Card Divider"), new Vector2(width - 44f, 1f), new Vector2(0f, attachedTop));
            Box(Child(card, prefix + " Attached Card Heading"), new Vector2(width - 44f, 30f), new Vector2(0f, attachedTop - 23f));
            RectTransform preview = Child(card, prefix + " Attached Card Preview");
            RectTransform cardName = Child(card, prefix + " Attached Card Name");
            if (preview != null)
            {
                float previewScale = Mathf.Min((width - 52f) / preview.rect.width, (attachedHeight - 60f) / preview.rect.height);
                preview.localScale = Vector3.one * previewScale;
                preview.anchoredPosition = new Vector2(0f, -height * .5f + (attachedHeight - 30f) * .5f);
            }
            else
                Box(cardName, new Vector2(width - 44f, 72f), new Vector2(0f, -height * .5f + attachedHeight * .5f));
            RectTransform stats = Child(card, prefix + " Stats Viewport") ?? Child(card, prefix + " Stats");
            if (stats != null)
            {
                stats.anchorMin = stats.anchorMax = stats.pivot = new Vector2(0f, 1f);
                stats.anchoredPosition = new Vector2(22f, -(height - readingHeight + 18f));
                stats.sizeDelta = new Vector2(width - 44f, statsHeight);
                ScrollRect scroll = stats.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content != null)
                {
                    Text label = scroll.content.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        label.rectTransform.sizeDelta = new Vector2(stats.rect.width - 12f, label.rectTransform.rect.height);
                        float textHeight = Mathf.Ceil(label.preferredHeight) + 4f;
                        label.rectTransform.sizeDelta = new Vector2(stats.rect.width - 12f, textHeight);
                        scroll.content.sizeDelta = new Vector2(stats.rect.width, Mathf.Max(stats.rect.height, textHeight + 16f));
                        scroll.vertical = scroll.content.rect.height > stats.rect.height + .5f;
                        if (!scroll.vertical) scroll.content.anchoredPosition = Vector2.zero;
                    }
                    if (scroll.verticalScrollbar != null)
                    {
                        RectTransform track = (RectTransform)scroll.verticalScrollbar.transform;
                        track.anchoredPosition = new Vector2(stats.rect.width - 3f, -stats.rect.height * .5f);
                        track.sizeDelta = new Vector2(5f, stats.rect.height);
                        if (track.childCount > 0 && track.GetChild(0) is RectTransform handleArea)
                            handleArea.sizeDelta = track.sizeDelta;
                        track.gameObject.SetActive(scroll.vertical);
                    }
                }
            }
        }

        private static void Panel(RectTransform panel, Rect bounds)
        {
            Box(panel, bounds.size, bounds.center);
            if (panel == null) return;
            Box(Child(panel, panel.name + " Top"), new Vector2(bounds.width, 1f), new Vector2(0f, (bounds.height - 1f) * .5f));
            Box(Child(panel, panel.name + " Bottom"), new Vector2(bounds.width, 1f), new Vector2(0f, -(bounds.height - 1f) * .5f));
            Box(Child(panel, panel.name + " Left"), new Vector2(1f, bounds.height), new Vector2(-(bounds.width - 1f) * .5f, 0f));
            Box(Child(panel, panel.name + " Right"), new Vector2(1f, bounds.height), new Vector2((bounds.width - 1f) * .5f, 0f));
        }

        private static void Box(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform child = parent.Find(name);
            return child != null && child.gameObject.activeSelf ? child as RectTransform : null;
        }
    }
}
