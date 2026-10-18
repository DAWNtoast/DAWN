using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One human-scale deck in front of the location's distant sea and sky.</summary>
    public sealed class BoardingDeckBackdrop : MonoBehaviour
    {
        private RawImage scenery;
        private RawImage deck;
        public RawImage Deck => deck;

        public static void Attach(RawImage background, SeaTerrainType terrain, NavigationTimeOfDay time)
        {
            Texture2D texture = BoardingBattleArtCatalog.JoinedDeck();
            if (background == null || texture == null) return;
            var view = background.gameObject.AddComponent<BoardingDeckBackdrop>();
            view.scenery = background;
            var obj = new GameObject("Boarding Joined Deck", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            obj.transform.SetParent(background.transform.parent, false);
            obj.transform.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
            view.deck = obj.GetComponent<RawImage>();
            view.deck.texture = texture;
            view.deck.color = BoardingBattleArtCatalog.DeckLight(terrain, time);
            view.deck.raycastTarget = false;
            view.RefreshPose();
        }

        public void RefreshPose()
        {
            if (scenery == null || deck == null) return;
            RectTransform skyRect = scenery.rectTransform;
            skyRect.anchorMin = skyRect.anchorMax = skyRect.pivot = new Vector2(.5f, .5f);
            skyRect.sizeDelta = new Vector2(1860f, 400f);
            skyRect.anchoredPosition = new Vector2(0f, 250f);
            scenery.uvRect = BoardingBattleLayout.SceneryUv(scenery.texture, skyRect.sizeDelta);
            RectTransform deckRect = deck.rectTransform;
            deckRect.anchorMin = deckRect.anchorMax = deckRect.pivot = new Vector2(.5f, .5f);
            deckRect.sizeDelta = new Vector2(1860f, 900f);
            deckRect.anchoredPosition = Vector2.zero;
            deck.uvRect = BoardingBattleLayout.JoinedDeckUv(deck.texture, deckRect.sizeDelta);
        }
    }
}
