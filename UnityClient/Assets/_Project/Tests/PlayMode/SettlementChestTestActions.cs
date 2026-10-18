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
            if (sequence.IsComplete) yield break;
            Button skip = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(item => item.name == "Skip Settlement Reveal" && item.gameObject.activeInHierarchy);
            Assert.That(skip.IsInteractable(), Is.True, "The chest must offer its normal skip action.");
            Canvas.ForceUpdateCanvases();
            Canvas canvas = skip.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform rect = (RectTransform)skip.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(skip),
                "The visible chest skip action must receive the pointer.");
            ExecuteEvents.Execute(skip.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(skip.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(skip.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(sequence.IsComplete, Is.True, "Skipping must reveal the normal settlement overview.");
        }
    }
}
