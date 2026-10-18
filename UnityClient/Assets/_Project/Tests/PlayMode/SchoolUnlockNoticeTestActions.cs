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
    // A confirmed voyage may unlock schools before the next port action.
    // Follow the real modal buttons; never bypass their input interception.
    internal static class SchoolUnlockNoticeTestActions
    {
        internal static IEnumerator ConfirmAll()
        {
            for (int page = 0; page < 6 && GameObject.Find("School Unlock Notice") != null; page++)
            {
                yield return null;
                GameObject notice = GameObject.Find("School Unlock Notice");
                Assert.That(notice, Is.Not.Null);
                Button button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .SingleOrDefault(candidate => candidate.transform.IsChildOf(notice.transform) &&
                        candidate.gameObject.activeInHierarchy &&
                        (candidate.name == "Next School Unlock" || candidate.name == "Confirm School Unlock"));
                Assert.That(button, Is.Not.Null, "Every unlock page must have an actionable continuation.");
                Assert.That(button.IsInteractable(), Is.True);
                Canvas.ForceUpdateCanvases();
                RectTransform rect = button.GetComponent<RectTransform>();
                var pointer = new PointerEventData(EventSystem.current) {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                    "The actual school unlock action must receive the pointer.");
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
            }
            Assert.That(GameObject.Find("School Unlock Notice"), Is.Null,
                "All announced schools must be acknowledged before the next port action.");
        }
    }
}
