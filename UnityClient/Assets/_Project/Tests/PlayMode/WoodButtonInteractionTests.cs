using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WoodButtonInteractionTests
    {
        [UnityTest]
        public IEnumerator HoverPressDisableAndClickKeepTheWoodAndGeometry()
        {
            var root = new GameObject("Wood Button Interaction Fixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                var host = new GameObject("Confirm Fixture", typeof(RectTransform), typeof(Image), typeof(Button));
                host.transform.SetParent(root.transform, false);
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(230, 52);
                var button = host.GetComponent<Button>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                WoodButtonSkin.Apply(button, Color.blue);
                var colors = button.colors;
                colors.fadeDuration = 0;
                button.colors = colors;
                int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                yield return null;
                Vector3 restingPosition = rect.localPosition, restingScale = rect.localScale;
                Color normal = button.targetGraphic.canvasRenderer.GetColor();
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                button.OnPointerEnter(pointer);
                Color highlighted = button.targetGraphic.canvasRenderer.GetColor();
                Assert.That(highlighted, Is.Not.EqualTo(normal));
                button.OnPointerDown(pointer);
                Assert.That(button.targetGraphic.canvasRenderer.GetColor(), Is.Not.EqualTo(highlighted));
                button.OnPointerUp(pointer);
                button.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(1));
                button.OnPointerExit(pointer);
                Assert.That(button.targetGraphic.canvasRenderer.GetColor(), Is.EqualTo(normal));
                button.interactable = false;
                button.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(1), "Disabled purchase/action buttons cannot fire.");
                Assert.That(button.targetGraphic.canvasRenderer.GetColor(), Is.Not.EqualTo(normal));
                Assert.That(((Image)button.targetGraphic).sprite, Is.SameAs(WoodButtonSkin.Face));
                Assert.That(rect.localPosition, Is.EqualTo(restingPosition));
                Assert.That(rect.localScale, Is.EqualTo(restingScale));
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(230, 52)));
            }
            finally { Object.Destroy(root); }
        }
    }
}
