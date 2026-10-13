using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavigationCompassControlTests
    {
        private static readonly string[] Directions =
            { "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest" };
        private GameObject host;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void AnnularHitAreas_HaveOneOwnerAtEveryAngleAndExcludeTheHubAndOuterCorners()
        {
            RectTransform panel = CreatePanel();
            NavigationCompassControl.Layout(panel, Vector2.zero, 280f);
            NavigationCompassSectorGraphic[] sectors = host.GetComponentsInChildren<NavigationCompassSectorGraphic>();
            Assert.That(sectors.Length, Is.EqualTo(8));
            for (int sample = 0; sample < 1440; sample++)
            {
                float angle = sample * .25f * Mathf.Deg2Rad;
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 94f;
                Assert.That(sectors.Count(sector => sector.ContainsLocalPoint(point)), Is.EqualTo(1),
                    "Compass seams and overlapping button rectangles must not lose or duplicate a direction at " + sample * .25f);
            }
            foreach (Vector2 point in new[] { Vector2.zero, new Vector2(40, 0), new Vector2(130, 0), new Vector2(130, 130) })
                Assert.That(sectors.Any(sector => sector.ContainsLocalPoint(point)), Is.False, point.ToString());
            for (int index = 0; index < Directions.Length; index++)
            {
                float angle = (90f - index * 45f) * Mathf.Deg2Rad;
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 94f;
                Assert.That(sectors.Single(sector => sector.ContainsLocalPoint(point)).transform.parent.name,
                    Is.EqualTo("Heading " + Directions[index]));
            }
        }

        [Test]
        public void FullSizeTutorialGuards_InheritTheSectorFilterAndCannotCoverTheEnabledEastDirection()
        {
            RectTransform panel = CreatePanel();
            panel.position = new Vector3(480, 320, 0);
            panel.localScale = new Vector3(1.4f, 1.4f, 1f);
            NavigationCompassControl.Layout(panel, new Vector2(-100, -20), 280f);
            foreach (string direction in Directions.Where(direction => direction != "East"))
            {
                Transform heading = panel.Find("Heading " + direction);
                Button button = heading.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.interactable = false;
                var guard = new GameObject("Guide Input Guard", typeof(RectTransform), typeof(Image));
                guard.transform.SetParent(heading, false);
                var rect = guard.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                guard.GetComponent<Image>().color = Color.clear;
            }
            Image[] guards = host.GetComponentsInChildren<Image>().Where(image => image.name == "Guide Input Guard").ToArray();
            Vector2 east = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(new Vector2(-6, -20)));
            Assert.That(guards.Any(guard => guard.Raycast(east, null)), Is.False,
                "A disabled heading's rectangular tutorial guard must not intercept the east wedge.");
            Vector2 north = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(new Vector2(-100, 74)));
            Assert.That(guards.Count(guard => guard.Raycast(north, null)), Is.EqualTo(1));
            Assert.That(guards.Single(guard => guard.Raycast(north, null)).transform.parent.name, Is.EqualTo("Heading North"));
            NavigationCompassControl.Attach(panel);
            foreach (Image guard in guards)
            {
                Button locked = guard.GetComponentInParent<Button>();
                Assert.That(locked.transition, Is.EqualTo(Selectable.Transition.None),
                    "Reapplying the compass must preserve the tutorial's bright but disabled controls.");
                Assert.That(locked.GetComponentInChildren<Text>().color, Is.EqualTo(WoodButtonSkin.Ink));
                Assert.That(locked.targetGraphic.canvasRenderer.GetColor(),
                    Is.EqualTo(locked.colors.normalColor * locked.colors.colorMultiplier));
            }
            Button eastButton = panel.Find("Heading East").GetComponent<Button>();
            Assert.That(eastButton.FindSelectableOnLeft(), Is.Null);
            Assert.That(eastButton.FindSelectableOnRight(), Is.Null);
            Assert.That(eastButton.FindSelectableOnUp(), Is.Null);
            Assert.That(eastButton.FindSelectableOnDown(), Is.Null,
                "Keyboard navigation must not transfer focus into a tutorial lock.");
            Button northButton = panel.Find("Heading North").GetComponent<Button>();
            northButton.interactable = true;
            panel.GetComponent<NavigationCompassControl>().SendMessage("LateUpdate");
            Assert.That(eastButton.FindSelectableOnLeft(), Is.SameAs(northButton),
                "Navigation must refresh when availability changes after initial attachment.");
            Assert.That(eastButton.FindSelectableOnRight(), Is.SameAs(northButton));
        }

        [Test]
        public void TutorialFocus_AnnotatesOnlyTheChosenDirectionWithoutChangingItsControlTarget()
        {
            RectTransform panel = CreatePanel();
            NavigationCompassControl.Layout(panel, Vector2.zero, 280f);
            Button east = panel.Find("Heading East").GetComponent<Button>();
            var focus = new GameObject("East Focus", typeof(RectTransform), typeof(GuidedTutorialFocusGraphic))
                .GetComponent<GuidedTutorialFocusGraphic>();
            focus.transform.SetParent(panel, false);
            focus.rectTransform.sizeDelta = panel.rect.size;
            focus.Configure(east);
            Assert.That(focus.Target, Is.SameAs(east.transform));
            Assert.That(focus.VisualTarget, Is.SameAs(east.transform.Find("Label")));
            Assert.That(focus.TryGetProtectedRect(panel, out Rect bounds), Is.True);
            Assert.That(bounds.width, Is.LessThan(140f), "The red outline must identify East instead of the entire compass.");
            Assert.That(bounds.Contains(Vector2.zero), Is.False, "The decoration at the compass hub is not the taught action.");
            Vector2 labelCenter = panel.InverseTransformPoint(focus.VisualTarget.TransformPoint(focus.VisualTarget.rect.center));
            Assert.That(bounds.Contains(labelCenter), Is.True);
            Assert.That(((NavigationCompassSectorGraphic)east.targetGraphic).ContainsLocalPoint(labelCenter), Is.True);
        }

        [Test]
        public void RepeatedAttachment_PreservesHeadingObjectsActionsAndWoodSelectionState()
        {
            RectTransform panel = CreatePanel();
            Button[] original = Directions.Select(direction => panel.Find("Heading " + direction).GetComponent<Button>()).ToArray();
            int calls = 0;
            original[2].onClick.AddListener(() => calls++);
            WoodButtonSkin.SetSelected(original[2], true);
            original[0].interactable = false;
            NavigationCompassControl.Attach(panel);
            NavigationCompassControl.Layout(panel, new Vector2(-100, -20), 280f);
            NavigationCompassControl.Attach(panel);
            NavigationCompassControl.Layout(panel, new Vector2(-100, -20), 280f);
            Assert.That(panel.Cast<Transform>().Count(child => child.name == "Navigation Compass Artwork"), Is.EqualTo(1));
            Assert.That(host.GetComponentsInChildren<NavigationCompassSectorGraphic>().Length, Is.EqualTo(8));
            for (int index = 0; index < Directions.Length; index++)
            {
                Assert.That(panel.Find("Heading " + Directions[index]).GetComponent<Button>(), Is.SameAs(original[index]));
                Assert.That(original[index].transform.parent, Is.SameAs(panel));
                Assert.That(original[index].targetGraphic, Is.TypeOf<NavigationCompassSectorGraphic>());
                Assert.That(original[index].GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(original[index].GetComponentInChildren<Text>().raycastTarget, Is.False);
            }
            Assert.That(original[0].interactable, Is.False);
            Assert.That(((NavigationCompassSectorGraphic)original[2].targetGraphic).HeadingSelected, Is.True);
            original[2].onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            WoodButtonSkin.SetSelected(original[2], false);
            WoodButtonSkin.SetSelected(original[4], true);
            NavigationCompassControl.Attach(panel);
            Assert.That(((NavigationCompassSectorGraphic)original[2].targetGraphic).HeadingSelected, Is.False);
            Assert.That(((NavigationCompassSectorGraphic)original[4].targetGraphic).HeadingSelected, Is.True,
                "The existing selection API must continue to work after changing the visual target.");
        }

        private RectTransform CreatePanel()
        {
            host = new GameObject("Navigation Controls", typeof(RectTransform));
            RectTransform panel = host.GetComponent<RectTransform>();
            panel.sizeDelta = new Vector2(660, 470);
            foreach (string direction in Directions)
            {
                var go = new GameObject("Heading " + direction, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panel, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(84, 44);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(go.transform, false);
                label.GetComponent<Text>().text = direction;
                label.GetComponent<Text>().fontSize = 20;
                WoodButtonSkin.Apply(go.GetComponent<Button>(), Color.white);
            }
            return panel;
        }
    }
}
