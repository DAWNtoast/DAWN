using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DispatchDeploymentLayoutTests
    {
        private GameObject host;

        [TestCase(0, 2)]
        [TestCase(1, 6)]
        [TestCase(2, 10)]
        public void DurationUsesTransparentSquareHelmAndKeepsWholeHourRewards(int mission, int hours)
        {
            host = new GameObject("Dispatch Helm Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DispatchDeployment, CoreGameplayPreviewPort.WhiteShell, mission);
            Slider slider = Find<Slider>(host.transform, "Dispatch Duration Slider");
            RawImage helm = Find<RawImage>(host.transform, "Dispatch Duration Helm");
            Text label = Find<Text>(host.transform, "Dispatch Duration Value");
            Text reward = Find<Text>(host.transform, "Dispatch Reward Preview");
            Assert.That(Find<Text>(host.transform, "Dispatch Deployment Mission Rule").text,
                Does.Contain("现实小时").And.Contain("离线继续计时").And.Contain("不推进游戏日历"));
            string originalReward = reward.text;
            Assert.That(helm.texture, Is.SameAs(Resources.Load<Texture2D>(DispatchDurationHelm.ResourcePath)));
            Assert.That(helm.texture, Is.Not.Null);
            Assert.That(helm.rectTransform.sizeDelta, Is.EqualTo(new Vector2(40, 40)));
            Assert.That(slider.targetGraphic, Is.SameAs(helm));
            Assert.That(slider.handleRect.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(slider.handleRect.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(slider.minValue, Is.EqualTo(hours));
            Assert.That(slider.maxValue, Is.EqualTo(hours * 2));
            Assert.That(slider.wholeNumbers, Is.True);
            slider.value = hours * 1.5f;
            Assert.That(label.text, Is.EqualTo((hours * 3 / 2) + " 现实小时"));
            Assert.That(Mathf.DeltaAngle(helm.rectTransform.localEulerAngles.z, -67.5f), Is.EqualTo(0f).Within(0.01f));
            slider.value = hours * 3;
            Assert.That(label.text, Is.EqualTo(hours * 2 + " 现实小时"));
            Assert.That(reward.text, Is.Not.EqualTo(originalReward));
            slider.value = 0;
            Assert.That(label.text, Is.EqualTo(hours + " 现实小时"));
            Assert.That(reward.text, Is.EqualTo(originalReward));
            Canvas.ForceUpdateCanvases();
            Assert.That(slider.handleRect.rect.size, Is.EqualTo(Vector2.one * 44f));
            Assert.That(slider.handleRect.localRotation, Is.EqualTo(Quaternion.identity));
            DispatchDurationHelm.Apply(slider);
            Assert.That(slider.handleRect.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DurationHelmKeepsTrackClickDragAndKeyboardSelection(int mission)
        {
            host = new GameObject("Dispatch Helm Pointer Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DispatchDeployment, CoreGameplayPreviewPort.WhiteShell, mission);
            Slider slider = Find<Slider>(host.transform, "Dispatch Duration Slider");
            var eventsObject = new GameObject("Helm Test Event System", typeof(EventSystem));
            eventsObject.transform.SetParent(host.transform);
            var events = eventsObject.GetComponent<EventSystem>();
            var canvas = slider.GetComponentInParent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.ForceUpdateCanvases();
            var area = (RectTransform)slider.handleRect.parent;
            Vector2 middle = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(Vector3.zero));
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, position = middle };
            slider.OnPointerDown(pointer);
            Assert.That(slider.normalizedValue, Is.EqualTo(0.5f).Within(0.01f));
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(new Vector3(area.rect.xMax, 0, 0)));
            slider.OnDrag(pointer);
            Assert.That(slider.value, Is.EqualTo(slider.maxValue));
            slider.OnPointerUp(pointer);
            slider.OnMove(new AxisEventData(events) { moveDir = MoveDirection.Left });
            Assert.That(slider.value, Is.EqualTo(slider.maxValue - 1f));
        }

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DeploymentActionsStayInsideUndecoratedPaperAndClearOfReward(int mission)
        {
            host = new GameObject("Dispatch Deployment Footer Test");
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DispatchDeployment, CoreGameplayPreviewPort.WhiteShell, mission);
            Transform root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            RectTransform deployment = Find<RectTransform>(root, "Dispatch Deployment Panel");
            Find<RawImage>(root, "Dispatch Deployment Frame " + mission);
            // Measured common clear area across all three frame PNGs. Excludes the
            // salvage gem/treasure chest, not merely the parchment texture rectangle.
            Rect clearPaper = Rect.MinMaxRect(54f, -203f, 597f, -153f);
            Rect back = BoundsIn(deployment, Find<Button>(root, "Back To Dispatch Table").GetComponent<RectTransform>());
            Rect confirm = BoundsIn(deployment, Find<Button>(root, "Confirm Dispatch Deployment").GetComponent<RectTransform>());
            foreach (Rect action in new[] { back, confirm })
            {
                Assert.That(action.xMin, Is.GreaterThanOrEqualTo(clearPaper.xMin));
                Assert.That(action.xMax, Is.LessThanOrEqualTo(clearPaper.xMax));
                Assert.That(action.yMin, Is.GreaterThanOrEqualTo(clearPaper.yMin));
                Assert.That(action.yMax, Is.LessThanOrEqualTo(clearPaper.yMax));
                Assert.That(action.height, Is.GreaterThanOrEqualTo(43.9f));
            }
            Assert.That(confirm.xMin - back.xMax, Is.GreaterThanOrEqualTo(19.9f));
            Rect reward = BoundsIn(deployment, Find<Text>(root, "Dispatch Reward Preview").rectTransform);
            Rect duration = BoundsIn(deployment, Find<Slider>(root, "Dispatch Duration Slider").GetComponent<RectTransform>());
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Dispatch Deployment Investment"), Is.False,
                "The deployment preview removes the separate investment text block.");
            Rect selectedCrew = BoundsIn(deployment, Find<Text>(root, "Dispatch Selected Crew").rectTransform);
            Assert.That(selectedCrew.center.x, Is.EqualTo(500f).Within(0.1f));
            Assert.That(selectedCrew.center.y, Is.EqualTo(205f).Within(0.1f));
            Rect durationHeading = BoundsIn(deployment, Find<Text>(root, "Dispatch Duration Heading").rectTransform);
            Assert.That(durationHeading.center.x, Is.EqualTo(11.948f).Within(0.1f));
            Assert.That(durationHeading.center.y, Is.EqualTo(-62.3838f).Within(0.1f));
            Assert.That(reward.yMin - back.yMax, Is.GreaterThanOrEqualTo(7.9f));
            Assert.That(duration.yMin - reward.yMax, Is.GreaterThanOrEqualTo(4.9f));
            for (int index = 0; index < 6; index++)
                Assert.That(Find<Button>(root, "Dispatch Crew Toggle " + index).interactable, Is.True);
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
    }
}
