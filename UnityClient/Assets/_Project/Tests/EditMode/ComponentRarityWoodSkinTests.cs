using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentRarityWoodSkinTests
    {
        private static readonly Dictionary<ComponentRarity, string> ResourcePaths = new Dictionary<ComponentRarity, string>
        {
            { ComponentRarity.Common, "UIWood/ComponentRarity/wood_common" },
            { ComponentRarity.Uncommon, "UIWood/ComponentRarity/wood_uncommon" },
            { ComponentRarity.Rare, "UIWood/ComponentRarity/wood_rare" },
            { ComponentRarity.Epic, "UIWood/ComponentRarity/wood_epic" },
            { ComponentRarity.Legendary, "UIWood/ComponentRarity/wood_legendary" },
        };
        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void ShowroomUsesEveryAuthoredRarityBoardAndKeepsItWhenSelectionChanges()
        {
            host = new GameObject("Component Rarity Board Showroom Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewPort.GrayGull, 0);
            var catalog = new ComponentArtCatalog();
            string prefix = "rarity-board-test|" + Guid.NewGuid().ToString("N");
            var stock = ResourcePaths.Keys.Select(rarity =>
            {
                ComponentArtEntry entry = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == rarity);
                return new PortComponentShopStockItem(prefix + rarity, controller.Session.CurrentPort,
                    PortComponentShopCatalog.MonthKey(controller.Session.GameDateTime), entry, 200);
            }).ToList();
            RectTransform panel = host.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Component Shop Panel");
            for (int index = panel.childCount - 1; index >= 0; index--) Object.DestroyImmediate(panel.GetChild(index).gameObject);
            Write(controller, "componentShopCategory", (int)ComponentSlot.Bow);
            Write(controller, "componentShopDisplayPage", 0);
            Write(controller, "componentShopShowroomOfferId", null);
            typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopShowroom", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { panel, stock, controller.Session.GameDateTime, 0 });
            Canvas.ForceUpdateCanvases();

            Button[] offers = panel.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Component Shop Offer ")).ToArray();
            Assert.That(offers.Length, Is.EqualTo(ResourcePaths.Count));
            var labels = offers.SelectMany(item => item.GetComponentsInChildren<Text>())
                .Select(item => new { Label = item, Color = item.color, Value = item.text }).ToArray();
            var textures = new HashSet<Texture>();
            foreach (Button offer in offers)
            {
                ComponentRarity rarity = stock.Single(item => offer.name == "Component Shop Offer " + item.Id).Entry.Rarity;
                Texture2D expected = RequireTexture(rarity);
                Assert.That(textures.Add(expected), Is.True, "Every rarity must have its own authored texture.");
                Assert.That(offer.GetComponent<Image>().sprite.texture, Is.SameAs(expected));
                Assert.That(offer.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(offer.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(1), "The board must reuse the button Image.");
            }

            foreach (Button selected in offers)
            {
                selected.onClick.Invoke();
                foreach (Button offer in offers)
                {
                    ComponentRarity rarity = stock.Single(item => offer.name == "Component Shop Offer " + item.Id).Entry.Rarity;
                    Assert.That(offer.GetComponent<Image>().sprite.texture, Is.SameAs(RequireTexture(rarity)), "Selection must retain the item's rarity.");
                    Assert.That(offer.GetComponent<ComponentRarityWoodSkin>().IsSelected, Is.EqualTo(offer == selected));
                    Assert.That(offer.GetComponent<WoodButtonRoleState>().enabled, Is.False, "The shared gold selection edge must stay disabled.");
                }
            }
            foreach (var label in labels)
            {
                Assert.That(label.Label.color, Is.EqualTo(label.Color));
                Assert.That(label.Label.text, Is.EqualTo(label.Value));
            }
            Assert.That(host.GetComponentsInChildren<Transform>().Any(item => item.name == "Component Shop Detail"), Is.False);
        }

        [Test]
        public void ReusingSelectedDisabledButtonReplacesItsRarityWithoutRecoloringContentOrAddingHitTargets()
        {
            host = new GameObject("Component Shop Offer Reuse Test", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(218f, 106f);
            var button = host.GetComponent<Button>();
            var labelObject = new GameObject("Price", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(host.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.text = "已装同款";
            WoodButtonSkin.Apply(button, Color.white);
            WoodButtonSkin.SetSelected(button, true);
            label.color = new Color(.9f, .6f, .2f);
            Color contentColor = label.color;
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);

            Assert.That(ComponentRarityWoodSkin.Apply(button, ComponentRarity.Rare), Is.True, "A missing authored board must not pass through fallback.");
            ComponentRarityWoodSkin.SetSelected(button, true);
            button.interactable = false;
            Assert.That(ComponentRarityWoodSkin.Apply(button, ComponentRarity.Epic), Is.True);
            WoodButtonSkin.SetSelected(button, false);
            var state = host.GetComponent<ComponentRarityWoodSkin>();
            var image = host.GetComponent<Image>();
            Assert.That(image.sprite.texture, Is.SameAs(RequireTexture(ComponentRarity.Epic)));
            Assert.That(state.Rarity, Is.EqualTo(ComponentRarity.Epic));
            Assert.That(state.IsSelected && state.enabled, Is.True);
            Assert.That(button.interactable, Is.False);
            Assert.That(host.GetComponent<WoodButtonRoleState>().enabled, Is.False);
            Assert.That(button.colors.disabledColor.r, Is.LessThan(button.colors.normalColor.r));
            foreach (Color tint in new[] { button.colors.normalColor, button.colors.highlightedColor, button.colors.selectedColor,
                button.colors.pressedColor, button.colors.disabledColor })
                Assert.That(tint.r == tint.g && tint.g == tint.b, Is.True, "Interaction feedback must not shift painted rarity colors.");

            button.interactable = true;
            ComponentRarityWoodSkin.SetSelected(button, false);
            Assert.That(state.IsSelected || state.enabled, Is.False);
            Assert.That(ComponentRarityWoodSkin.Apply(button, ComponentRarity.Epic), Is.True);
            Assert.That(image.sprite.texture, Is.SameAs(RequireTexture(ComponentRarity.Epic)));
            Assert.That(label.color, Is.EqualTo(contentColor));
            Assert.That(label.text, Is.EqualTo("已装同款"));
            Assert.That(button.targetGraphic, Is.SameAs(image));
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(host.GetComponents<ComponentRarityWoodSkin>().Length, Is.EqualTo(1));
            Assert.That(host.GetComponentsInChildren<Graphic>().Length, Is.EqualTo(2));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(218f, 106f)));
            button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
        }

        private static Texture2D RequireTexture(ComponentRarity rarity)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourcePaths[rarity]);
            Assert.That(texture, Is.Not.Null, ResourcePaths[rarity]);
            return texture;
        }

        private static void Write(CoreGameplayDemoController controller, string name, object value) =>
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
    }
}
