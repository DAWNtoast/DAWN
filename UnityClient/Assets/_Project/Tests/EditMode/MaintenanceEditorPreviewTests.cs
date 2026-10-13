using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
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
                    Does.Contain("port_investment_scene_internal_v1"));
                Assert.That(Find<Text>(preview, "Port Investment Project Name").text,
                    Does.Contain("盐冠港").And.Contain(PortInvestmentCatalog.NameFor("盐冠港")));
                Assert.That(Find<Text>(preview, "Port Investment Current Level").text, Is.EqualTo("Lv.0"));
                Assert.That(Find<Text>(preview, "Port Investment Next Level").text, Is.EqualTo("Lv.1"));
                Assert.That(Find<Text>(preview, "Port Investment Current Quality Value").text, Is.EqualTo("13.0%"));
                Assert.That(Find<Text>(preview, "Port Investment Next Quality Value").text, Is.EqualTo("14.5%"));
                Assert.That(Find<Text>(preview, "Port Investment Quality Increase").text, Is.EqualTo("+1.5 个百分点"));
                Assert.That(Find<Text>(preview, "Port Investment Current Component Value").text, Is.EqualTo("6 件"));
                Assert.That(Find<Text>(preview, "Port Investment Next Component Value").text, Is.EqualTo("8 件"));
                Assert.That(Find<Text>(preview, "Port Investment Component Increase").text, Is.EqualTo("+2 件"));
                Assert.That(Find<Text>(preview, "Port Investment Current Quota Value").text, Is.EqualTo("200"));
                Assert.That(Find<Text>(preview, "Port Investment Next Quota Value").text, Is.EqualTo("300"));
                Assert.That(Find<Text>(preview, "Port Investment Quota Increase").text, Is.EqualTo("+100"));
                RectTransform arrow = Find<RectTransform>(preview, "Port Investment Comparison Arrow");
                Assert.That(arrow.GetComponentsInChildren<Image>(true).Count(image => image.transform != arrow), Is.EqualTo(3));
                Assert.That(arrow.GetComponent<Image>().raycastTarget, Is.False);
                foreach (string name in new[] { "Port Investment Arrow Shaft", "Port Investment Arrow Upper", "Port Investment Arrow Lower" })
                {
                    Image segment = Find<Image>(arrow, name);
                    Assert.That(segment.gameObject.activeInHierarchy && segment.enabled, Is.True, name);
                    Assert.That(segment.color.a, Is.GreaterThan(0f), name);
                    Assert.That(segment.sprite, Is.Null, name);
                    Assert.That(segment.raycastTarget, Is.False, name);
                }
                Assert.That(Find<Text>(preview, "Port Investment Ledger Title").text, Is.EqualTo("升级至 Lv.1 所需材料"));
                Assert.That(preview.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name.StartsWith("Port Investment Tier ")), Is.False);
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel("盐冠港", 1);
                string requiredKey = PortMarketCatalog.Key(cost.RequiredItem);
                Transform ledger = Find<RectTransform>(preview, "Port Investment Ledger");
                CollectionAssert.AreEquivalent(new[] { "gold", "wood", "iron", requiredKey },
                    ledger.GetComponentsInChildren<Transform>(true)
                        .Where(item => item.parent == ledger && item.name.StartsWith("Port Investment Requirement "))
                        .Select(item => item.name.Substring("Port Investment Requirement ".Length)));
                AssertRequirement(preview, controller.Session, "gold", "金币", cost.Gold);
                AssertRequirement(preview, controller.Session, "wood", "木材", cost.Wood);
                AssertRequirement(preview, controller.Session, "iron", "铁矿", cost.Iron);
                AssertRequirement(preview, controller.Session, requiredKey, PortMarketCatalog.Name(cost.RequiredItem), cost.RequiredQuantity);
                Assert.That(Find<Text>(preview, "Port Investment Quantity Header").text, Is.EqualTo("拥有 / 需求"));
                Assert.That(Find<Button>(preview, "Confirm Port Investment").GetComponentInChildren<Text>().text,
                    Is.EqualTo("升级至 Lv.1"));
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
                    "Port Investment Ledger Title", "Port Investment Quantity Header"
                })
                {
                    Text label = Find<Text>(preview, name);
                    Outline outline = label.GetComponent<Outline>();
                    Assert.That(outline, Is.Not.Null, name);
                    Assert.That(outline.enabled, Is.False, name + " should not duplicate dark glyphs on parchment");
                    Assert.That(label.GetComponents<Shadow>().Count(shadow => shadow.GetType() == typeof(Shadow) && shadow.enabled),
                        Is.EqualTo(0), name + " must not carry a second offset shadow");
                    Assert.That(label.color.grayscale, Is.LessThan(.4f), name + " uses dark ink on parchment");
                }
                foreach (Text label in Find<RectTransform>(preview, "Port Investment Summary").GetComponentsInChildren<Text>())
                    Assert.That(label.color.grayscale, Is.GreaterThan(.5f),
                        label.name + " uses readable light lettering on the dark wooden board");
                foreach (Text requirement in preview.GetComponentsInChildren<Text>(true)
                    .Where(text => text.name.StartsWith("Port Investment Requirement ")))
                {
                    Outline outline = requirement.GetComponent<Outline>();
                    Assert.That(outline, Is.Not.Null, requirement.name);
                    Assert.That(outline.enabled, Is.False, requirement.name + " should not duplicate dark glyphs on parchment");
                    Assert.That(requirement.GetComponents<Shadow>().Count(shadow => shadow.GetType() == typeof(Shadow) && shadow.enabled),
                        Is.EqualTo(0), requirement.name + " must not carry a second offset shadow");
                }
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

        private static void AssertRequirement(Transform preview, CoreVoyageSession session, string key, string name, int required)
        {
            Assert.That(Find<Text>(preview, "Port Investment Requirement Name " + key).text, Is.EqualTo(name));
            string owned = key == "gold"
                ? session.ProfileGold.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
                : ((long)session.GetWarehouseCargoAmount(key) + session.PortWarehouse.GetAmount(session.CurrentPort, key))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(Find<Text>(preview, "Port Investment Requirement Quantity " + key).text,
                Is.EqualTo(owned + "/" + required.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            RawImage icon = Find<RawImage>(preview, "Port Investment Requirement Icon " + key);
            ItemIconArt art = key == "gold" ? ItemIconCatalog.ForSpecial(key) : ItemIconCatalog.ForCargo(key);
            Assert.That(art.Texture, Is.Not.Null, key + " catalog icon");
            Assert.That(icon.texture, Is.SameAs(art.Texture), key + " icon texture");
            Assert.That(icon.uvRect, Is.EqualTo(art.Uv), key + " icon atlas cell");
            Assert.That(icon.raycastTarget, Is.False, key + " icon");
            Assert.That(icon.gameObject.activeInHierarchy && icon.enabled, Is.True, key + " icon");
            Assert.That(icon.color.a, Is.GreaterThan(0f), key + " icon");
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
