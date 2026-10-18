using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class MaintenanceGroundingLayoutTests
    {
        private static IEnumerable<TestCaseData> Ports()
        {
            yield return new TestCaseData(CoreGameplayPreviewPort.BrokenAnchor);
            yield return new TestCaseData(CoreGameplayPreviewPort.WhiteShell);
            yield return new TestCaseData(CoreGameplayPreviewPort.BronzeBell);
            yield return new TestCaseData(CoreGameplayPreviewPort.SaltCrown);
            yield return new TestCaseData(CoreGameplayPreviewPort.TwilightTide);
            yield return new TestCaseData(CoreGameplayPreviewPort.GrayGull);
        }

        [TestCaseSource(nameof(Ports))]
        public void InvestmentPreview_UsesDedicatedBackdropForEveryPort(CoreGameplayPreviewPort port)
        {
            GameObject host = new GameObject("Port Investment Grounding Preview Test");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, port, 0);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                RawImage background = Find<RawImage>(preview, "Maintenance Background");
                Assert.That(background.texture, Is.Not.Null);
                Assert.That(background.texture.name, Does.Contain("port_investment_scene_internal_v1"));
                Assert.That(background.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(background.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                    "The approved scene's ledger and benefit board must align with the complete painting.");
                Assert.That(background.raycastTarget, Is.False);
                Assert.That(FindTransform(preview, "Maintenance Supply Panel"), Is.Null);
                Assert.That(FindTransform(preview, "Maintenance Modular Ship"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InvestmentLedger_ContainsOnlyNextLevelMaterialsAndAccessibleControls()
        {
            GameObject host = new GameObject("Port Investment Ledger Preview Test");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance,
                    CoreGameplayPreviewPort.SaltCrown, 0);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                RectTransform panel = Find<RectTransform>(preview, "Port Investment Panel");
                RectTransform summary = Find<RectTransform>(preview, "Port Investment Summary");
                RectTransform ledger = Find<RectTransform>(preview, "Port Investment Ledger");
                Assert.That(summary.rect.width, Is.GreaterThan(800f));
                Assert.That(ledger.rect.width, Is.GreaterThan(400f));
                Assert.That(panel.rect.width, Is.GreaterThan(summary.rect.width + ledger.rect.width));
                Assert.That(summary.GetComponent<Image>().sprite, Is.Null);
                Assert.That(summary.GetComponent<Image>().color.a, Is.Zero,
                    "The bottom values sit directly on the approved scene's wooden board.");
                Assert.That(ledger.GetComponent<Image>().sprite, Is.Null);
                Assert.That(ledger.GetComponent<Image>().color.a, Is.Zero,
                    "The material rows use the parchment painted into the scene.");
                Assert.That(ledger.anchoredPosition.x, Is.GreaterThan(summary.anchoredPosition.x));
                Assert.That(summary.anchoredPosition.y, Is.LessThan(ledger.anchoredPosition.y));
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel("盐冠港", 1);
                string[] keys = { "gold", "wood", "iron", PortMarketCatalog.Key(cost.RequiredItem) };
                CollectionAssert.AreEquivalent(keys, ledger.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.parent == ledger && item.name.StartsWith("Port Investment Requirement "))
                    .Select(item => item.name.Substring("Port Investment Requirement ".Length)));
                Assert.That(preview.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name.StartsWith("Port Investment Tier ")), Is.False);
                foreach (string key in keys)
                {
                    RectTransform row = Find<RectTransform>(preview, "Port Investment Requirement " + key);
                    Assert.That(row.parent, Is.SameAs(ledger));
                    Assert.That(row.rect.height, Is.GreaterThanOrEqualTo(40f));
                    AssertContained(ledger, row);
                    RawImage icon = Find<RawImage>(preview, "Port Investment Requirement Icon " + key);
                    ItemIconArt art = key == "gold" ? ItemIconCatalog.ForSpecial(key) : ItemIconCatalog.ForCargo(key);
                    Assert.That(art.Texture, Is.Not.Null, key + " catalog icon");
                    Assert.That(icon.texture, Is.SameAs(art.Texture), key + " icon texture");
                    Assert.That(icon.uvRect, Is.EqualTo(art.Uv), key + " icon atlas cell");
                    Assert.That(icon.raycastTarget, Is.False, key + " icon");
                    AssertContained(row, icon.rectTransform);
                    RectTransform name = Find<Text>(preview, "Port Investment Requirement Name " + key).rectTransform;
                    AssertContained(row, name);
                    AssertContained(row, Find<Text>(preview, "Port Investment Requirement Quantity " + key).rectTransform);
                    Assert.That(icon.rectTransform.anchoredPosition.x + icon.rectTransform.rect.xMax,
                        Is.LessThan(name.anchoredPosition.x + name.rect.xMin), key + " icon is left of the item name");
                }
                AssertContained(ledger, Find<Text>(preview, "Port Investment Quantity Header").rectTransform);
                Assert.That(Find<Text>(preview, "Port Investment Quantity Header").text, Is.EqualTo("拥有 / 需求"));
                Assert.That(Find<Button>(preview, "Confirm Port Investment").GetComponentInChildren<Text>().text,
                    Is.EqualTo("材料不足"));
                Assert.That(Find<Button>(preview, "Open Port Market").GetComponentInChildren<Text>().text,
                    Does.Contain("港口市场"));
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

        private static void AssertContained(RectTransform parent, RectTransform item)
        {
            Vector3[] corners = new Vector3[4];
            item.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - 1f, parent.rect.xMax + 1f));
                Assert.That(local.y, Is.InRange(parent.rect.yMin - 1f, parent.rect.yMax + 1f));
            }
        }
    }
}
