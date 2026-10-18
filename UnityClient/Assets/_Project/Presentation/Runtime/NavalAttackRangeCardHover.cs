using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    public sealed class NavalAttackRangeCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private NavalAttackRangePreview preview;
        private Func<CoreBattleCard> card;
        public void Configure(NavalAttackRangePreview view, Func<CoreBattleCard> getCard)
        { preview = view; card = getCard; }
        public void OnPointerEnter(PointerEventData eventData)
        { if (preview != null) preview.BeginCardHover(this, card); }
        public void OnPointerExit(PointerEventData eventData)
        { if (preview != null) preview.EndCardHover(this); }
        private void OnDisable()
        { if (preview != null) preview.EndCardHover(this); }
    }
}
