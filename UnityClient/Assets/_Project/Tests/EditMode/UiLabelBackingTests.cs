using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiLabelBackingTests
    {
        private GameObject root;

        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void RepeatedApplyUsesOneOutlineWithoutAddingABoxOrChangingAccent()
        {
            Text label = CreateLabel();
            new GameObject("Following Label", typeof(RectTransform)).transform.SetParent(root.transform, false);
            label.color = new Color(.12f, .65f, .4f, 1f);
            Color originalAccent = label.color;
            Shadow oldShadow = label.gameObject.AddComponent<Shadow>();
            Outline existingOutline = label.gameObject.AddComponent<Outline>();
            UiLabelBacking.Apply(label, true);
            UiLabelBacking.Apply(label, true);
            UiLabelBacking.Apply(label, true);

            Assert.That(root.transform.childCount, Is.EqualTo(2), "Styling must not create rectangular label plates.");
            Assert.That(root.GetComponentsInChildren<Image>(true), Is.Empty);
            Assert.That(label.color, Is.EqualTo(originalAccent));
            Assert.That(label.GetComponents<Outline>().Length, Is.EqualTo(1));
            Assert.That(existingOutline.enabled, Is.True);
            Assert.That(existingOutline.useGraphicAlpha, Is.True);
            Assert.That(oldShadow.enabled, Is.False, "Stacked effects make glyphs look doubled.");
        }

        [TestCase(0f, 1f)]
        [TestCase(.5f, .5f)]
        [TestCase(1f, 0f)]
        public void ExistingBoxIsDisabledWithoutMovingOrResizingTheLabel(float pivotX, float pivotY)
        {
            Text label = CreateLabel();
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = new Vector2(.1f, .2f);
            textRect.anchorMax = new Vector2(.8f, .9f);
            textRect.pivot = new Vector2(pivotX, pivotY);
            textRect.anchoredPosition3D = new Vector3(17f, -9f, 2f);
            textRect.localScale = new Vector3(1.4f, .8f, 1f);
            textRect.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Vector2 originalSize = textRect.sizeDelta;
            var before = new Vector3[4];
            textRect.GetWorldCorners(before);
            var legacy = new GameObject("Scene Label Backing", typeof(RectTransform), typeof(Image));
            legacy.transform.SetParent(root.transform, false);
            Image backing = legacy.GetComponent<Image>();
            backing.color = new Color(.035f, .065f, .073f, .92f);
            backing.raycastTarget = true;
            label.raycastTarget = true;
            UiLabelBacking.Apply(label);

            var after = new Vector3[4];
            textRect.GetWorldCorners(after);
            CollectionAssert.AreEqual(before, after);
            Assert.That(textRect.sizeDelta, Is.EqualTo(originalSize));
            Assert.That(label.raycastTarget, Is.True, "Removing a plate must retain label click routing.");
            Assert.That(backing.enabled, Is.False);
            Assert.That(backing.raycastTarget, Is.False);
            Assert.That(label.GetComponent<Outline>().enabled, Is.True);
        }

        private Text CreateLabel()
        {
            root = new GameObject("Backing Test", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(600f, 400f);
            var host = new GameObject("Scene Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            host.transform.SetParent(root.transform, false);
            Text label = host.GetComponent<Text>();
            label.rectTransform.sizeDelta = new Vector2(120f, 30f);
            return label;
        }
    }
}
