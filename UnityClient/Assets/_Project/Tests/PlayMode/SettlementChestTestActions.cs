using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    internal static class SettlementChestTestActions
    {
        internal static IEnumerator ShowOverview()
        {
            var sequence = Object.FindObjectsByType<SettlementChestSequenceController>(FindObjectsSortMode.None)
                .SingleOrDefault(item => item.gameObject.activeInHierarchy && !item.IsComplete);
            if (sequence == null) yield break;
            yield return null;
            float deadline = Time.realtimeSinceStartup + sequence.Duration + 2f;
            while (!sequence.IsComplete && Time.realtimeSinceStartup < deadline)
            {
                ClickCurrentReward();
                yield return new WaitForSecondsRealtime(.12f);
            }
            Assert.That(sequence.IsComplete, Is.True, "Accelerating each reward must eventually reveal the normal settlement overview.");
        }

        internal static void ClickCurrentReward()
        {
            Image surface = Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
                .Single(item => item.name == "Settlement Chest Shade" && item.gameObject.activeInHierarchy);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = surface.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform rect = surface.rectTransform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.SameAs(surface.gameObject),
                "The chest input surface must receive the click instead of the hidden settlement actions.");
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True,
                "The full-screen chest surface must handle left clicks.");
        }
    }
}
