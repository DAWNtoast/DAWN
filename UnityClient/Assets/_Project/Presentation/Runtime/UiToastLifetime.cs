using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Non-modal notification lifetime on unscaled time, independent of its content.</summary>
    public sealed class UiToastLifetime : MonoBehaviour
    {
        private GameObject card;
        private Func<bool> isCurrent;
        private float expires;

        public static UiToastLifetime Bind(GameObject host, GameObject card, Func<bool> isCurrent = null)
        {
            UiToastLifetime view = host.GetComponent<UiToastLifetime>();
            if (view == null) view = host.AddComponent<UiToastLifetime>();
            view.Hide();
            view.card = card; view.isCurrent = isCurrent;
            CanvasGroup group = host.GetComponent<CanvasGroup>();
            if (group == null) group = host.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            view.Hide();
            return view;
        }

        public void Show(float seconds = 4f)
        {
            if (!isActiveAndEnabled || card == null || (isCurrent != null && !isCurrent())) return;
            expires = Time.unscaledTime + Mathf.Max(0f, seconds);
            card.SetActive(true);
        }

        public void Hide() { if (card != null) card.SetActive(false); }
        private void OnDisable() => Hide();
        private void Update()
        {
            if (card == null || !card.activeSelf) return;
            if ((isCurrent != null && !isCurrent()) || Time.unscaledTime >= expires) Hide();
        }
    }
}
