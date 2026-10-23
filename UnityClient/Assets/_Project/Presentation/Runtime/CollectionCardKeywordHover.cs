using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Glossary hover for collection buttons without taking over their click, drag or scroll handling.</summary>
    [DisallowMultipleComponent]
    public sealed class CollectionCardKeywordHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerMoveHandler, IPointerDownHandler, IPointerUpHandler
    {
        private static CollectionCardKeywordHover visibleOwner;
        private readonly List<RectTransform> companionCards = new List<RectTransform>();
        private RectTransform cardFace, tooltip;
        private IReadOnlyList<CoreBattleCardKeyword> entries;
        private ScrollRect[] scrollParents;
        private PointerEventData pointer;
        private bool pressed;

        public static CollectionCardKeywordHover Bind(GameObject pointerTarget, RectTransform cardFace,
            CoreBattleCard card, string visibleDescription)
        {
            if (pointerTarget == null || cardFace == null) return null;
            CollectionCardKeywordHover hover = pointerTarget.GetComponent<CollectionCardKeywordHover>();
            if (hover == null) hover = pointerTarget.AddComponent<CollectionCardKeywordHover>();
            hover.Hide();
            hover.UnsubscribeScroll();
            hover.ReleaseTooltip();
            hover.cardFace = cardFace;
            hover.entries = CoreBattleCardKeywordText.Build(card, visibleDescription);
            hover.pointer = null;
            hover.pressed = false;
            hover.scrollParents = pointerTarget.GetComponentsInParent<ScrollRect>(true);
            foreach (ScrollRect scroll in hover.scrollParents) scroll.onValueChanged.AddListener(hover.OnScrollChanged);
            return hover;
        }

        public void Show()
        {
            if (visibleOwner != null && visibleOwner != this) visibleOwner.Hide();
            if (!isActiveAndEnabled || cardFace == null || !cardFace.gameObject.activeInHierarchy ||
                pressed || pointer != null && pointer.dragging || entries == null || entries.Count == 0)
            {
                Hide();
                return;
            }
            if (tooltip == null)
            {
                tooltip = CardKeywordTooltip.Create(cardFace, "Collection Card Keyword Tooltip", entries,
                    GameTypography.BodyFont, allowRightFallback: true);
                tooltip.gameObject.AddComponent<Canvas>();
            }
            visibleOwner = this;
            tooltip.gameObject.SetActive(true);
            // Apply after activation: Unity can reset a disabled nested canvas's sorting on enable.
            // Draw above neighboring cards and outside list masks, without adding an input surface.
            Canvas canvas = tooltip.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;
            companionCards.Clear();
            // Standalone faces in one panel form a comparison, unlike collection
            // faces nested inside independent selection buttons or preview hosts.
            if (cardFace == transform && cardFace.parent != null)
                foreach (Transform sibling in cardFace.parent)
                {
                    if (sibling == transform || !sibling.gameObject.activeInHierarchy) continue;
                    CollectionCardKeywordHover other = sibling.GetComponent<CollectionCardKeywordHover>();
                    if (other != null && other.isActiveAndEnabled && other.cardFace == sibling)
                        companionCards.Add(other.cardFace);
                }
            CardKeywordTooltip layout = tooltip.GetComponent<CardKeywordTooltip>();
            layout.SetCompanionCards(companionCards);
            layout.RefreshPosition();
        }

        public void Hide()
        {
            if (visibleOwner == this) visibleOwner = null;
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointer = eventData;
            Show();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            pointer = eventData;
            Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointer = null;
            Hide();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointer = eventData;
            pressed = true;
            Hide();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointer = eventData;
            pressed = false;
        }

        private void LateUpdate()
        {
            if (visibleOwner == this && (cardFace == null || !cardFace.gameObject.activeInHierarchy ||
                pointer != null && pointer.dragging)) Hide();
        }

        private void OnScrollChanged(Vector2 _) => Hide();

        private void OnDisable()
        {
            pointer = null;
            pressed = false;
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
            UnsubscribeScroll();
            ReleaseTooltip();
        }

        private void UnsubscribeScroll()
        {
            if (scrollParents == null) return;
            foreach (ScrollRect scroll in scrollParents)
                if (scroll != null) scroll.onValueChanged.RemoveListener(OnScrollChanged);
            scrollParents = null;
        }

        private void ReleaseTooltip()
        {
            if (tooltip == null) return;
            GameObject previous = tooltip.gameObject;
            tooltip = null;
            previous.SetActive(false);
            if (Application.isPlaying) Destroy(previous);
            else DestroyImmediate(previous);
        }
    }
}
