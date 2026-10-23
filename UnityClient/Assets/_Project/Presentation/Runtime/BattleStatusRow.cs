using System;
using System.Collections.Generic;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Retained, wrapping status icons directly below a combatant's health.</summary>
    public sealed class BattleStatusRow : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform Rect;
            public BattleStatusIconGraphic Icon;
            public Text Value;
            public BattleStatusHover Hover;
            public BattleHudChangeMotion Motion;
            public CoreBattleStatus State;
            public int Order;
            public bool PendingRetirement;
        }

        private readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
        private readonly List<string> removed = new List<string>();
        private readonly List<Slot> ordered = new List<Slot>();
        private readonly List<Slot> retiring = new List<Slot>();
        private readonly List<Slot> layoutSlots = new List<Slot>();
        private readonly Dictionary<string, CoreBattleStatus> presented = new Dictionary<string, CoreBattleStatus>();
        private Font font;
        private BattleStatusTooltip tooltip;
        private Func<bool> suppress;
        private CanvasGroup visibility;
        private RectTransform healthBar;
        private float maximumHeight = 140f;
        private float cellSize;
        private bool initialized;
        private bool pendingChanges;
        private Behaviour owner;
        private bool hadOwner;
        public int VisibleStatusCount => IsSuppressed ? 0 : slots.Count;
        public int RetiringStatusCount => retiring.Count;
        public bool IsSuppressed { get; private set; }
        public void SetOwner(Behaviour value) { owner = value; hadOwner = value != null; }

        public void Configure(Font labelFont, RectTransform overlayRoot, Func<bool> shouldSuppress, float heightLimit = 140f)
        {
            font = labelFont;
            maximumHeight = Mathf.Max(50f, heightLimit);
            suppress = shouldSuppress;
            visibility = GetComponent<CanvasGroup>();
            if (visibility == null) visibility = gameObject.AddComponent<CanvasGroup>();
            tooltip = BattleStatusTooltip.GetOrCreate(overlayRoot, labelFont);
            RefreshVisibility();
        }

        public void SetStatuses(IReadOnlyList<CoreBattleStatus> statuses, bool animateChanges = false)
        {
            UpdateVisibility(false);
            bool animate = initialized && animateChanges && !IsSuppressed;
            bool defer = initialized && animateChanges && IsSuppressed;
            ordered.Clear();
            removed.Clear();
            foreach (string id in slots.Keys) removed.Add(id);
            for (int index = 0; index < statuses.Count; index++)
            {
                CoreBattleStatus status = statuses[index];
                for (int retired = retiring.Count - 1; retired >= 0; retired--)
                    if (retiring[retired].PendingRetirement && retiring[retired].State.Id == status.Id)
                    { DestroySlot(retiring[retired]); retiring.RemoveAt(retired); }
                bool added = !slots.TryGetValue(status.Id, out Slot slot);
                if (added)
                {
                    slot = CreateSlot(status.Id);
                    slots.Add(status.Id, slot);
                }
                removed.Remove(status.Id);
                slot.Icon.Configure(status.Kind, status.Id, status.IsDebuff);
                slot.Value.text = status.Value;
                slot.Hover.Configure(this, tooltip, status.Name, status.Tooltip);
                if (animate)
                {
                    if (!presented.TryGetValue(status.Id, out CoreBattleStatus before))
                        slot.Motion.Play("新增", StatusTint(status, true), BattleHudChangeKind.Added, font);
                    else if (before.Value != status.Value || before.Tooltip != status.Tooltip)
                        PlayStatusChange(slot, before, status);
                }
                else slot.Motion.Cancel();
                slot.State = status;
                slot.Order = index;
                slot.Rect.SetSiblingIndex(index);
                ordered.Add(slot);
            }
            foreach (string id in removed)
            {
                Slot slot = slots[id];
                slots.Remove(id);
                if ((animate || defer) && presented.ContainsKey(id))
                {
                    // Keep a noninteractive visual placeholder briefly, so live icons
                    // cannot move on top of a fading effect. It is no longer a status.
                    slot.Rect.name = "Retiring Status " + id;
                    slot.Hover.enabled = false;
                    foreach (Graphic graphic in slot.Rect.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                    slot.Motion.Cancel();
                    slot.PendingRetirement = defer;
                    if (!defer) slot.Motion.Play("消退", StatusTint(slot.State, false), BattleHudChangeKind.Removed, font, true);
                    retiring.Add(slot);
                }
                else DestroySlot(slot);
            }
            if (defer) pendingChanges = true;
            else
            {
                if (animate) StartPendingRetirements();
                else ClearPendingRetirements();
                RememberPresentedStatuses();
            }
            initialized = true;
            Arrange();
            RefreshVisibility();
        }

        public void SetCellSize(float size)
        {
            float next = size > 0f ? Mathf.Clamp(size, 50f, 75f) * (2f / 3f) : 0f;
            if (Mathf.Approximately(cellSize, next)) return;
            cellSize = next;
            foreach (Slot slot in slots.Values) SizeSlot(slot);
            foreach (Slot slot in retiring) SizeSlot(slot);
            Arrange();
        }

        public void FollowHealthBar(RectTransform anchor)
        {
            healthBar = anchor;
            FollowAnchor();
        }

        public void RefreshVisibility() => UpdateVisibility(true);

        private void UpdateVisibility(bool revealPending)
        {
            bool ownerInactive = hadOwner && (owner == null || !owner.isActiveAndEnabled);
            if (ownerInactive) ResetPresentation();
            IsSuppressed = ownerInactive || suppress != null && suppress();
            if (visibility != null)
            {
                visibility.alpha = IsSuppressed ? 0f : 1f;
                visibility.blocksRaycasts = !IsSuppressed;
                visibility.interactable = !IsSuppressed;
            }
            if (IsSuppressed && tooltip != null) tooltip.HideRow(this);
            if (IsSuppressed)
            {
                foreach (Slot slot in slots.Values) slot.Motion.Cancel();
                foreach (Slot slot in retiring) slot.Motion.Cancel();
            }
            else if (revealPending) RevealPendingChanges();
        }

        private void LateUpdate() { FollowAnchor(); RefreshVisibility(); RemoveFinishedRetirements(); }
        private void FollowAnchor()
        {
            if (healthBar == null) return;
            transform.position = healthBar.TransformPoint(new Vector3(0f, -healthBar.rect.height * .5f - 12f, 0f));
            transform.rotation = healthBar.rotation;
        }
        private void OnDisable() => ResetPresentation();

        private void ResetPresentation()
        {
            initialized = false;
            pendingChanges = false;
            presented.Clear();
            if (tooltip != null) tooltip.HideRow(this);
            foreach (Slot slot in slots.Values) slot.Motion.Cancel();
            foreach (Slot slot in retiring) DestroySlot(slot);
            retiring.Clear();
        }
        private void OnRectTransformDimensionsChange() => Arrange();

        private void Arrange()
        {
            if (ordered == null) return;
            layoutSlots.Clear();
            layoutSlots.AddRange(ordered);
            retiring.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (Slot slot in retiring) layoutSlots.Insert(Mathf.Clamp(slot.Order, 0, layoutSlots.Count), slot);
            if (layoutSlots.Count == 0) return;
            RectTransform rect = (RectTransform)transform;
            float scale = 1f;
            float columnWidth = cellSize > 0f ? cellSize : 52f;
            float rowHeight = cellSize > 0f ? cellSize : 50f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(rect.rect.width / columnWidth));
            int rows = Mathf.CeilToInt(layoutSlots.Count / (float)columns);
            // Keep all effects above the hand, even during dense boss encounters.
            while (rows * rowHeight * scale > maximumHeight && scale > .4f)
            {
                scale -= .025f;
                columns = Mathf.Max(1, Mathf.FloorToInt(rect.rect.width / (columnWidth * scale)));
                rows = Mathf.CeilToInt(layoutSlots.Count / (float)columns);
            }
            float step = Mathf.Min((cellSize > 0f ? cellSize : 58f) * scale, rect.rect.width / columns);
            float height = rows * rowHeight * scale;
            if (!Mathf.Approximately(rect.rect.height, height))
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            for (int index = 0; index < layoutSlots.Count; index++)
            {
                int row = index / columns;
                int rowCount = Mathf.Min(columns, layoutSlots.Count - row * columns);
                layoutSlots[index].Rect.anchoredPosition = new Vector2(
                    (index % columns - (rowCount - 1) * .5f) * step, (-rowHeight * .5f - row * rowHeight) * scale);
                layoutSlots[index].Rect.localScale = Vector3.one * scale;
            }
        }

        private void RemoveFinishedRetirements()
        {
            bool changed = false;
            for (int index = retiring.Count - 1; index >= 0; index--)
            {
                Slot slot = retiring[index];
                if (slot.Rect != null && (slot.PendingRetirement || slot.Motion.IsPlaying)) continue;
                DestroySlot(slot); retiring.RemoveAt(index); changed = true;
            }
            if (changed) Arrange();
        }

        private static void DestroySlot(Slot slot)
        {
            if (slot.Rect == null) return;
            slot.Rect.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(slot.Rect.gameObject); else DestroyImmediate(slot.Rect.gameObject);
        }

        private void RevealPendingChanges()
        {
            if (!initialized || !pendingChanges) return;
            foreach (Slot slot in slots.Values)
            {
                CoreBattleStatus status = slot.State;
                if (!presented.TryGetValue(status.Id, out CoreBattleStatus before))
                    slot.Motion.Play("新增", StatusTint(status, true), BattleHudChangeKind.Added, font);
                else if (before.Value != status.Value || before.Tooltip != status.Tooltip)
                    PlayStatusChange(slot, before, status);
            }
            StartPendingRetirements();
            RememberPresentedStatuses();
        }

        private void StartPendingRetirements()
        {
            foreach (Slot slot in retiring)
                if (slot.PendingRetirement)
                {
                    slot.PendingRetirement = false;
                    slot.Motion.Play("消退", StatusTint(slot.State, false), BattleHudChangeKind.Removed, font, true);
                }
        }

        private void ClearPendingRetirements()
        {
            for (int index = retiring.Count - 1; index >= 0; index--)
                if (retiring[index].PendingRetirement) { DestroySlot(retiring[index]); retiring.RemoveAt(index); }
        }

        private void RememberPresentedStatuses()
        {
            presented.Clear();
            foreach (var pair in slots) presented.Add(pair.Key, pair.Value.State);
            pendingChanges = false;
        }

        private void PlayStatusChange(Slot slot, CoreBattleStatus beforeState, CoreBattleStatus status)
        {
            string suffix = status.Value.EndsWith("%", StringComparison.Ordinal) ? "%" : string.Empty;
            string oldSuffix = beforeState.Value.EndsWith("%", StringComparison.Ordinal) ? "%" : string.Empty;
            if (suffix == oldSuffix && long.TryParse(status.Value.TrimEnd('%'), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long value) &&
                long.TryParse(beforeState.Value.TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out long before) && value != before)
            {
                long difference = value - before;
                bool increase = difference > 0;
                slot.Motion.Play((increase ? "+" : "−") + Math.Abs(difference).ToString(CultureInfo.InvariantCulture) + suffix,
                    StatusTint(status, increase), increase ? BattleHudChangeKind.Increased : BattleHudChangeKind.Decreased, font);
            }
            else slot.Motion.Play("更新", new Color(.73f, .86f, 1f), BattleHudChangeKind.Changed, font);
        }

        private static Color StatusTint(CoreBattleStatus status, bool increase) =>
            status.IsDebuff == increase ? new Color(1f, .64f, .49f) : new Color(.51f, 1f, .83f);

        private void SizeSlot(Slot slot)
        {
            bool naval = cellSize > 0f;
            float scale = naval ? cellSize / 75f : 1f;
            slot.Rect.sizeDelta = naval ? Vector2.one * cellSize : new Vector2(50f, 46f);
            slot.Icon.rectTransform.sizeDelta = Vector2.one * (naval ? cellSize : 34f);
            slot.Icon.rectTransform.anchoredPosition = new Vector2(naval ? -4f : -5f, 4f) * scale;
            slot.Value.rectTransform.sizeDelta = naval ? new Vector2(cellSize - 4f, 28f * scale) : new Vector2(48f, 24f);
            slot.Value.rectTransform.anchoredPosition = naval ? new Vector2(0f, -23f) * scale : new Vector2(1f, -11f);
            slot.Value.fontSize = slot.Value.resizeTextMaxSize = naval ? Mathf.RoundToInt(26f * scale) : 18;
            slot.Value.resizeTextMinSize = naval ? Mathf.RoundToInt(18f * scale) : 13;
        }

        private Slot CreateSlot(string id)
        {
            var root = new GameObject("Status " + id, typeof(RectTransform), typeof(Image), typeof(BattleStatusHover));
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(50f, 46f);
            root.GetComponent<Image>().color = new Color(.025f, .045f, .065f, .28f);
            var symbol = new GameObject("Icon", typeof(RectTransform), typeof(BattleStatusIconGraphic));
            var symbolRect = (RectTransform)symbol.transform;
            symbolRect.SetParent(rect, false);
            symbolRect.sizeDelta = new Vector2(34f, 34f);
            symbolRect.anchoredPosition = new Vector2(-5f, 4f);
            var label = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(Outline));
            Text value = label.GetComponent<Text>();
            value.rectTransform.SetParent(rect, false);
            value.rectTransform.sizeDelta = new Vector2(48f, 24f);
            value.rectTransform.anchoredPosition = new Vector2(1f, -11f);
            value.font = font;
            value.fontSize = 18;
            value.fontStyle = FontStyle.Normal;
            value.alignment = TextAnchor.MiddleRight;
            value.color = new Color(1f, .97f, .86f);
            value.raycastTarget = false;
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 13;
            value.resizeTextMaxSize = 18;
            Outline outline = label.GetComponent<Outline>();
            outline.effectColor = new Color(.025f, .035f, .055f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            var slot = new Slot { Rect = rect, Icon = symbol.GetComponent<BattleStatusIconGraphic>(), Value = value,
                Hover = root.GetComponent<BattleStatusHover>(), Motion = root.AddComponent<BattleHudChangeMotion>() };
            SizeSlot(slot);
            return slot;
        }
    }

    public sealed class BattleStatusHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private BattleStatusRow row;
        private BattleStatusTooltip tooltip;
        private string title, description;
        public void Configure(BattleStatusRow owner, BattleStatusTooltip popup, string name, string explanation)
        {
            row = owner; tooltip = popup; title = name; description = explanation;
            if (tooltip != null && tooltip.IsOwner(this)) tooltip.Show(this, row, title, description);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if ((row == null || !row.IsSuppressed) && tooltip != null)
                tooltip.Show(this, row, title, description);
        }
        public void OnPointerExit(PointerEventData eventData) { if (tooltip != null) tooltip.Hide(this); }
        private void OnDisable() { if (tooltip != null) tooltip.Hide(this); }
    }

    public sealed class BattleStatusTooltip : MonoBehaviour
    {
        private BattleStatusHover owner;
        private BattleStatusRow row;
        private Text label;
        private string heading, body;
        public bool IsOwner(BattleStatusHover candidate) => owner == candidate;

        public static BattleStatusTooltip GetOrCreate(RectTransform parent, Font font)
        {
            BattleStatusTooltip existing = parent.Find("Battle Status Tooltip")?.GetComponent<BattleStatusTooltip>();
            if (existing != null) return existing;
            var root = new GameObject("Battle Status Tooltip", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(BattleStatusTooltip));
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(342f, 120f);
            root.GetComponent<Image>().color = new Color(.035f, .055f, .085f, .98f);
            root.GetComponent<Image>().raycastTarget = false;
            Outline border = root.GetComponent<Outline>();
            border.effectColor = new Color(.74f, .59f, .31f, .95f);
            border.effectDistance = new Vector2(1f, -1f);
            var textObject = new GameObject("Description", typeof(RectTransform), typeof(Text));
            Text text = textObject.GetComponent<Text>();
            text.rectTransform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16f, 12f);
            text.rectTransform.offsetMax = new Vector2(-16f, -12f);
            text.font = font;
            text.fontSize = 17;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(.97f, .94f, .84f);
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            BattleStatusTooltip result = root.GetComponent<BattleStatusTooltip>();
            result.label = text;
            root.SetActive(false);
            return result;
        }

        public void Show(BattleStatusHover source, BattleStatusRow sourceRow, string title, string description)
        {
            if (sourceRow != null && sourceRow.IsSuppressed) return;
            owner = source; row = sourceRow;
            heading = title;
            body = description.StartsWith(title + "\n", StringComparison.Ordinal)
                ? description.Substring(title.Length + 1) : description;
            label.text = "<b><color=#EFC982>" + heading + "</color></b>\n" + body;
            PositionTooltip();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (owner == null || !owner.isActiveAndEnabled ||
                row != null && (row.IsSuppressed || !row.isActiveAndEnabled)) { Close(); return; }
            PositionTooltip();
        }

        private void PositionTooltip()
        {
            RectTransform rect = (RectTransform)transform;
            RectTransform parent = (RectTransform)rect.parent;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(342f, parent.rect.width - 24f));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, label.preferredHeight + 28f);
            Vector3 local = parent.InverseTransformPoint(owner.transform.position);
            float x = Mathf.Clamp(local.x, parent.rect.xMin + rect.rect.width * .5f + 8f,
                parent.rect.xMax - rect.rect.width * .5f - 8f);
            float y = local.y + 30f + rect.rect.height * .5f;
            if (y + rect.rect.height * .5f > parent.rect.yMax - 8f)
                y = local.y - 30f - rect.rect.height * .5f;
            y = Mathf.Clamp(y, parent.rect.yMin + rect.rect.height * .5f + 8f,
                parent.rect.yMax - rect.rect.height * .5f - 8f);
            rect.localPosition = new Vector3(x, y, 0f);
        }

        public void Hide(BattleStatusHover source) { if (owner == source) Close(); }
        public void HideRow(BattleStatusRow source) { if (row == source) Close(); }
        private void Close() { owner = null; row = null; gameObject.SetActive(false); }
    }
}
