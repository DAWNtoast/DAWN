using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class MaintenanceEditorPreviewTests
    {
        [Test]
        public void PortInvestmentScene_BuildsCompletePreviewOutsidePlayMode()
        {
            Assert.That(Application.isPlaying, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/_Project/Scenes/UIPreviews/UI_Maintenance.unity"), Is.Not.Null);

            GameObject host = new GameObject("Port Investment Preview Test Host");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance,
                    CoreGameplayPreviewPort.SaltCrown, 0);

                Assert.That(controller.HasEditorPreview, Is.True);
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                Assert.That(preview, Is.Not.Null);

                RectTransform panel = Find<RectTransform>(preview, "Port Investment Panel");
                Assert.That(panel.rect.width, Is.GreaterThanOrEqualTo(1800f));
                Assert.That(Find<RawImage>(preview, "Maintenance Background").texture, Is.Not.Null);
                Assert.That(Find<RawImage>(preview, "Maintenance Background").texture.name,
                    Does.Contain("port_investment_background_v2"));
                Assert.That(Find<Text>(preview, "Port Investment Summary Title").text, Is.Not.Empty);
                Assert.That(Find<Text>(preview, "Port Investment Level").text, Does.Contain("Lv.0 / 5"));
                Assert.That(Find<Text>(preview, "Port Investment Quality Chance").text, Does.Contain("13.0%"));
                Assert.That(Find<Text>(preview, "Port Investment Ledger Title").text, Is.EqualTo("港口建设计划"));
                Assert.That(preview.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("Port Investment Tier ") && !item.name.Contains("Label")), Is.EqualTo(5));
                Assert.That(Find<Button>(preview, "Confirm Port Investment"), Is.Not.Null);
                Assert.That(Find<Button>(preview, "Open Port Market"), Is.Not.Null);
                Assert.That(FindTransform(preview, "Maintenance Supply Panel"), Is.Null);
                Assert.That(FindTransform(preview, "Confirm Supply Purchase"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PortInvestmentText_DarkInkDoesNotReceiveOffsetShadow()
        {
            GameObject host = new GameObject("Port Investment Typography Preview Test Host");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance,
                    CoreGameplayPreviewPort.SaltCrown, 0);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                foreach (string name in new[]
                {
                    "Port Investment Summary Title", "Port Investment Port", "Port Investment Quality Chance",
                    "Port Investment Resources", "Port Investment Benefit", "Port Investment Ledger Title",
                    "Port Investment Ledger Hint", "Port Investment Notice"
                })
                {
                    Text label = Find<Text>(preview, name);
                    Outline outline = label.GetComponent<Outline>();
                    Assert.That(outline, Is.Not.Null, name);
                    Assert.That(outline.enabled, Is.False, name + " should not duplicate dark glyphs on parchment");
                    Assert.That(label.GetComponents<Shadow>().Count(shadow => shadow.GetType() == typeof(Shadow) && shadow.enabled),
                        Is.EqualTo(0), name + " must not carry a second offset shadow");
                }
                Text tier = Find<Text>(preview, "Port Investment Tier Label 1");
                Outline tierOutline = tier.GetComponent<Outline>();
                Assert.That(tierOutline, Is.Not.Null);
                Assert.That(tierOutline.enabled, Is.True);
                Assert.That(tierOutline.effectDistance.magnitude, Is.LessThanOrEqualTo(1.1f));
                Assert.That(tierOutline.effectColor.a, Is.LessThanOrEqualTo(0.35f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Transform FindTransform(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform item = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
            Assert.That(item, Is.Not.Null, name);
            return item.GetComponent<T>();
        }
    }
}
