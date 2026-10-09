using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class NauticalSurfaceSkinTests
    {
        private GameObject host;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void MaterialsHaveDetailAndMinificationLevels()
        {
            foreach (Sprite sprite in new[] { NauticalSurfaceSkin.Canvas, NauticalSurfaceSkin.Paper })
            {
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.texture.width, Is.GreaterThanOrEqualTo(1024));
                Assert.That(sprite.texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(sprite.texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            }
        }

        [TestCase("Warehouse Ledger", true)]
        [TestCase("Navigation Controls", true)]
        [TestCase("Settings Card", true)]
        [TestCase("Battle Overlay", false)]
        [TestCase("Settings Panel", false)]
        [TestCase("Achievements Panel", false)]
        [TestCase("New Game Confirmation", false)]
        [TestCase("Ship Contact Shadow", false)]
        public void MaterialKeepsBoundsAlphaAndHitTesting(string name, bool expected)
        {
            host = new GameObject(name, typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(620, 380);
            image.rectTransform.anchoredPosition = new Vector2(25, 30);
            image.raycastTarget = false;
            Color original = new Color(.05f, .1f, .12f, .86f);
            image.color = original;
            Assert.That(NauticalSurfaceSkin.ApplyPanel(image, original), Is.EqualTo(expected));
            Assert.That(image.color.a, Is.EqualTo(original.a));
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(620, 380)));
            Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(25, 30)));
            if (expected)
            {
                Assert.That(image.sprite, Is.Not.Null);
                if (name != "Warehouse Ledger") Assert.That(image.sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
            }
            else Assert.That(image.sprite, Is.Null);
        }

        [Test]
        public void PaperRetainsExistingDarkBrownInkAndMakesLightInkReadable()
        {
            host = new GameObject("Reading Paper", typeof(RectTransform), typeof(Image));
            NauticalSurfaceSkin.ApplyPanel(host.GetComponent<Image>(), new Color(.05f, .1f, .12f));
            Color brown = new Color(.22f, .13f, .06f);
            Assert.That(NauticalSurfaceSkin.ReadableTextColor(host.transform, brown), Is.EqualTo(brown));
            Assert.That(NauticalSurfaceSkin.ReadableTextColor(host.transform, Color.white).grayscale, Is.LessThan(.25f));
        }

        [Test]
        public void ClearSceneTargetsRemainClear()
        {
            host = new GameObject("Scene Target", typeof(RectTransform), typeof(Image));
            Assert.That(NauticalSurfaceSkin.ApplyPanel(host.GetComponent<Image>(), Color.clear), Is.False);
            Assert.That(host.GetComponent<Image>().sprite, Is.Null);
        }

        [TestCase("Handle")]
        [TestCase("Background")]
        [TestCase("Fill")]
        [TestCase("Track")]
        [TestCase("Toggle Box")]
        public void ControlPartsHaveMaterial(string name)
        {
            host = new GameObject(name, typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            NauticalSurfaceSkin.ApplyControlPart(image);
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.raycastTarget, Is.True);
        }

        [TestCase("Dispatch Crew Toggle navigator")]
        [TestCase("Add Deck Card 0")]
        public void PaperChoicesRetainInput(string name)
        {
            host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Button button = host.GetComponent<Button>();
            button.targetGraphic = host.GetComponent<Image>();
            Assert.That(NauticalSurfaceSkin.ApplySpecialButton(button, new Color(.1f, .4f, .35f, .25f)), Is.True);
            Assert.That(((Image)button.targetGraphic).sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
            Assert.That(button.interactable, Is.True);
        }

        [Test]
        public void MainMenuDialogsUseMaterialsButKeepDimmers()
        {
            host = new GameObject("Surface Main Menu Test");
            host.AddComponent<MainMenuController>();
            host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements").onClick.Invoke();
            Image[] images = host.GetComponentsInChildren<Image>(true);
            foreach (string name in new[] { "Settings Card", "Achievements Card", "Confirmation Card", "Left Menu Veil" })
            {
                Image surface = images.Single(image => image.name == name);
                Assert.That(surface.sprite, Is.Not.Null, name);
                if (name != "Left Menu Veil")
                    Assert.That(surface.sprite, Is.SameAs(NauticalSurfaceSkin.Paper), name);
            }
            foreach (string name in new[] { "Settings Panel", "Achievements Panel", "New Game Confirmation" })
                Assert.That(images.Single(image => image.name == name).sprite, Is.Null, name);
            foreach (Image emblem in images.Where(image => image.name == "Emblem"))
                Assert.That(emblem.sprite, Is.SameAs(WoodButtonSkin.Face));
        }

        [Test]
        public void CardCostsHaveLightPaperBehindDarkNumbers()
        {
            host = new GameObject("Card Cost Contrast Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
            Image[] badges = host.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("Card Energy Cost ")).ToArray();
            Assert.That(badges.Length, Is.GreaterThan(0));
            foreach (Image badge in badges)
            {
                Assert.That(badge.sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
                Assert.That(badge.color.grayscale - badge.GetComponentInChildren<Text>().color.grayscale, Is.GreaterThan(.4f));
            }
        }

        [Test]
        public void AllPreviewActionButtonsHaveAuthoredOrMaterialFaces()
        {
            host = new GameObject("Surface Preview Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            foreach (CoreGameplayPreviewScreen screen in Enum.GetValues(typeof(CoreGameplayPreviewScreen)))
            {
                controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                foreach (Button button in host.GetComponentsInChildren<Button>(true))
                {
                    Image face = button.GetComponent<Image>();
                    if (face == null || face.color.a <= .01f || button.name.StartsWith("Event Node ")) continue;
                    if (button.GetComponentInChildren<BattleCardSurfaceGraphic>(true) != null) continue;
                    Assert.That(face.sprite, Is.Not.Null, screen + ": " + button.name);
                }
            }
        }

        [Test]
        public void TutorialPanelsUseSameMaterialsWithoutStartingSession()
        {
            host = new GameObject("Surface Tutorial Test"); host.SetActive(false);
            var controller = host.AddComponent<TutorialVoyageController>();
            MethodInfo factory = typeof(TutorialVoyageController).GetMethod("CreateFixedPanel", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            var rect = (RectTransform)factory.Invoke(controller, new object[] { "Guide Panel", host.transform, new Vector2(500, 700), Vector2.zero, new Color(.05f, .1f, .12f) });
            Assert.That(rect.GetComponent<Image>().sprite, Is.Not.Null);
        }
    }
}
