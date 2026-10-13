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

        [Test]
        public void GameplayPaperRelease_AllowsCollectionWithoutDestroyingLiveUiOrSharedMenuPaper()
        {
            Sprite menuPaper = NauticalSurfaceSkin.ReadingPaper;
            Sprite investment = NauticalSurfaceSkin.InvestmentPaper;
            Sprite collection = NauticalSurfaceSkin.CollectionPaper;
            host = new GameObject("Retained Investment Paper", typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.sprite = investment;
            try
            {
                NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
                NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
                Assert.That(investment != null && collection != null, Is.True,
                    "Releasing the cache must let the engine decide whether any UI still needs a sprite.");
                Assert.That(image.sprite, Is.SameAs(investment));
                Assert.That(investment.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None));
                Assert.That(collection.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None));
                foreach (string field in new[] { "investmentPaper", "collectionPaper" })
                    Assert.That(typeof(NauticalSurfaceSkin).GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
                Assert.That(NauticalSurfaceSkin.ReadingPaper, Is.SameAs(menuPaper));
                Assert.That(NauticalSurfaceSkin.IsPaperSurface(image.sprite), Is.True);
                Assert.That(NauticalSurfaceSkin.ReadableTextColor(image.transform, Color.white).grayscale, Is.LessThan(.25f));
                Sprite reopened = NauticalSurfaceSkin.InvestmentPaper;
                Assert.That(reopened, Is.Not.SameAs(investment));
                Assert.That(reopened.texture, Is.SameAs(investment.texture));
                Assert.That(reopened.border, Is.EqualTo(investment.border));
            }
            finally
            {
                image.sprite = null;
                Object.DestroyImmediate(investment);
                Object.DestroyImmediate(collection);
            }
        }

        [TestCase("UISurfaces/aged_reading_parchment_v1")]
        [TestCase("UISurfaces/investment_charter_parchment_v1")]
        [TestCase("UISurfaces/collection_archive_parchment_v1")]
        public void DedicatedPapersUseTheirResourcesAndHaveUsableNineSliceBorders(string path)
        {
            Sprite sprite = DedicatedPaper(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(sprite.texture, Is.SameAs(Resources.Load<Texture2D>(path)));
            Assert.That(sprite.texture.width, Is.GreaterThanOrEqualTo(1024));
            Assert.That(sprite.texture.height, Is.GreaterThanOrEqualTo(512));
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(sprite), Is.True);
            Assert.That(sprite.border.x, Is.GreaterThan(0f));
            Assert.That(sprite.border.y, Is.GreaterThan(0f));
            Assert.That(sprite.border.z, Is.GreaterThan(0f));
            Assert.That(sprite.border.w, Is.GreaterThan(0f));
            Assert.That(sprite.border.x + sprite.border.z, Is.LessThan(sprite.rect.width));
            Assert.That(sprite.border.y + sprite.border.w, Is.LessThan(sprite.rect.height));
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(NauticalSurfaceSkin.Paper), Is.True,
                "The original paper remains a supported small-card and control surface.");
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(null), Is.False);
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(NauticalSurfaceSkin.Canvas), Is.False);
        }

        [TestCase("Warehouse Ledger", false)]
        [TestCase("Navigation Controls", true)]
        [TestCase("Settings Card", true)]
        [TestCase("Port Investment Summary", true)]
        [TestCase("Port Investment Ledger", true)]
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
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(620, 380)));
            Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(25, 30)));
            if (expected)
            {
                Assert.That(image.sprite, Is.Not.Null);
                Sprite expectedPaper = name == "Warehouse Ledger" ? NauticalSurfaceSkin.CollectionPaper :
                    name.StartsWith("Port Investment", StringComparison.Ordinal) ? NauticalSurfaceSkin.InvestmentPaper : NauticalSurfaceSkin.ReadingPaper;
                AssertDedicatedPaperStyle(image, expectedPaper);
            }
            else
            {
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.color.a, Is.EqualTo(original.a), "Atmosphere and dimmers keep their original alpha.");
            }
        }

        [TestCase(449f, 180f, false)]
        [TestCase(450f, 179f, false)]
        [TestCase(450f, 180f, true)]
        public void OnlyLargeReadingSurfacesReplaceTheOriginalSmallCardPaper(float width, float height, bool large)
        {
            host = new GameObject("Reading Details Panel", typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(width, height);
            Assert.That(NauticalSurfaceSkin.ApplyPanel(image, new Color(.05f, .1f, .12f, .86f)), Is.True);
            Assert.That(image.sprite, Is.SameAs(large ? NauticalSurfaceSkin.ReadingPaper : NauticalSurfaceSkin.Paper));
            if (large) AssertDedicatedPaperStyle(image, NauticalSurfaceSkin.ReadingPaper);
            else
            {
                Assert.That(image.type, Is.EqualTo(Image.Type.Tiled));
                Assert.That(image.color.a, Is.EqualTo(.86f));
            }
        }

        [TestCase(.08f, false)]
        [TestCase(.64f, false)]
        [TestCase(.65f, true)]
        public void LargeTransparentReadingSurfacesKeepTheirOriginalAlphaAndPaper(float alpha, bool opaque)
        {
            host = new GameObject("Route Selection", typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(620f, 380f);
            image.raycastTarget = false;
            Assert.That(NauticalSurfaceSkin.ApplyPanel(image, new Color(.9f, .82f, .68f, alpha)), Is.True);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(620f, 380f)));
            if (opaque) AssertDedicatedPaperStyle(image, NauticalSurfaceSkin.ReadingPaper);
            else
            {
                Assert.That(image.sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
                Assert.That(image.type, Is.EqualTo(Image.Type.Tiled));
                Assert.That(image.color.a, Is.EqualTo(alpha), "A translucent chart frame must not become an opaque reading panel.");
            }
        }

        [TestCase("Port Investment Summary", "Port Investment Current Quality Label", true)]
        [TestCase("Port Investment Ledger", "Port Investment Ledger Title", false)]
        public void InvestmentScene_TransparentReadingAreasKeepTheirContrastAndBoundsAfterRefresh(
            string surfaceName, string labelName, bool lightLettering)
        {
            host = new GameObject("Investment Scene Material Preview Test"); host.SetActive(false);
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Vector2? size = null, position = null;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass != 0) controller.RefreshEditorPreview();
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                Image surface = host.GetComponentsInChildren<Image>(true).Single(image => image.name == surfaceName);
                Assert.That(surface.sprite, Is.Null, "The reviewed scene already contains the reading surface.");
                Assert.That(surface.color.a, Is.Zero);
                Assert.That(surface.raycastTarget, Is.False);
                if (size.HasValue)
                {
                    Assert.That(surface.rectTransform.sizeDelta, Is.EqualTo(size.Value));
                    Assert.That(surface.rectTransform.anchoredPosition, Is.EqualTo(position.Value));
                }
                else { size = surface.rectTransform.sizeDelta; position = surface.rectTransform.anchoredPosition; }
                Text label = host.GetComponentsInChildren<Text>(true).Single(text => text.name == labelName);
                Assert.That(label.text, Is.Not.Empty);
                if (lightLettering) Assert.That(label.color.grayscale, Is.GreaterThan(.5f));
                else Assert.That(label.color.grayscale, Is.LessThan(.4f));
                Assert.That(label.raycastTarget, Is.False);
            }
        }

        [TestCase(CoreGameplayPreviewScreen.Market, "Market Item Detail", "UISurfaces/aged_reading_parchment_v1", "Market Shop Title")]
        public void RealPreviewPapersKeepTheirStyleReadableInkAndBoundsAfterRefresh(CoreGameplayPreviewScreen screen,
            string surfaceName, string resourcePath, string labelName)
        {
            host = new GameObject("Dedicated Paper Preview Test"); host.SetActive(false);
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            if (screen == CoreGameplayPreviewScreen.Market)
                typeof(CoreGameplayDemoController).GetMethod("OpenMarketShop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller,new object[] {MarketShopKind.Supplies});
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Image surface = host.GetComponentsInChildren<Image>(true).Single(image => image.name == surfaceName);
            Sprite paper = DedicatedPaper(resourcePath);
            Vector2 size = surface.rectTransform.sizeDelta, position = surface.rectTransform.anchoredPosition;
            bool hitTesting = surface.raycastTarget;
            AssertDedicatedPaperStyle(surface, paper);
            Text label = host.GetComponentsInChildren<Text>(true).Single(text => text.name == labelName);
            Assert.That(label.text, Is.Not.Empty);
            Assert.That(label.color.grayscale, Is.LessThan(.4f), "Reading paper needs dark ink.");
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(host.GetComponentsInChildren<Button>(true).Length, Is.GreaterThan(0), "The styled facility retains its controls.");
            controller.RefreshEditorPreview();
            if (screen == CoreGameplayPreviewScreen.Market)
                typeof(CoreGameplayDemoController).GetMethod("OpenMarketShop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller,new object[] {MarketShopKind.Supplies});
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            surface = host.GetComponentsInChildren<Image>(true).Single(image => image.name == surfaceName);
            AssertDedicatedPaperStyle(surface, paper);
            Assert.That(surface.raycastTarget, Is.EqualTo(hitTesting));
            Assert.That(surface.rectTransform.sizeDelta, Is.EqualTo(size));
            Assert.That(surface.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(host.GetComponentsInChildren<Text>(true).Single(text => text.name == labelName).color.grayscale, Is.LessThan(.4f));
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
            Assert.That(host.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "Main Menu Background").texture, Is.Not.Null);
            foreach (string name in new[] { "Settings Card", "Achievements Card", "Confirmation Card" })
            {
                Image surface = images.Single(image => image.name == name);
                Assert.That(surface.sprite, Is.Not.Null, name);
                AssertDedicatedPaperStyle(surface, NauticalSurfaceSkin.ReadingPaper);
            }
            foreach (string name in new[] { "Settings Panel", "Achievements Panel", "New Game Confirmation" })
                Assert.That(images.Single(image => image.name == name).sprite, Is.Null, name);
            foreach (Image emblem in images.Where(image => image.name == "Emblem"))
                Assert.That(emblem.sprite, Is.SameAs(WoodButtonSkin.Face));
        }

        [Test]
        public void CardCostsUseEnergySymbolsWithReadableOutlinedNumbers()
        {
            host = new GameObject("Card Cost Contrast Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
            CardOrnamentGraphic[] badges = host.GetComponentsInChildren<CardOrnamentGraphic>(true)
                .Where(i => i.name.StartsWith("Card Energy Cost ")).ToArray();
            Assert.That(badges.Length, Is.GreaterThan(0));
            foreach (CardOrnamentGraphic badge in badges)
            {
                Assert.That(badge.Kind, Is.EqualTo(CardOrnamentKind.Energy));
                Assert.That(badge.raycastTarget, Is.False);
                Text number = badge.GetComponentInChildren<Text>();
                Assert.That(number, Is.Not.Null);
                Assert.That(number.color.grayscale, Is.GreaterThan(.8f));
                Outline outline = number.GetComponent<Outline>();
                Assert.That(outline, Is.Not.Null);
                Assert.That(number.color.grayscale - outline.effectColor.grayscale, Is.GreaterThan(.4f));
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
                    if (button.name.StartsWith("Market Row "))
                    {
                        RawImage icon = button.GetComponentsInChildren<RawImage>(true)
                            .Single(image => image.name.StartsWith("Market Item Icon "));
                        Assert.That(icon.texture, Is.Not.Null, "Catalog tabs use the cargo icon over the shared paper.");
                        Assert.That(icon.GetComponent<Button>().targetGraphic, Is.SameAs(icon));
                        Assert.That(button.targetGraphic, Is.SameAs(face));
                        continue;
                    }
                    Image warehouseRim = button.transform.Find("Warehouse Inventory Rim")?.GetComponent<Image>();
                    if (warehouseRim != null)
                    {
                        Assert.That(warehouseRim.sprite, Is.Not.Null, "Inventory rows use authored brass rims over the painted lining.");
                        Assert.That(warehouseRim.fillCenter, Is.False);
                        Assert.That(warehouseRim.raycastTarget, Is.False);
                        continue;
                    }
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

        private static Sprite DedicatedPaper(string path)
        {
            if (path == NauticalSurfaceSkin.ReadingPaperPath) return NauticalSurfaceSkin.ReadingPaper;
            if (path == NauticalSurfaceSkin.InvestmentPaperPath) return NauticalSurfaceSkin.InvestmentPaper;
            if (path == NauticalSurfaceSkin.CollectionPaperPath) return NauticalSurfaceSkin.CollectionPaper;
            Assert.Fail("Unexpected dedicated paper path: " + path);
            return null;
        }

        private static void AssertDedicatedPaperStyle(Image surface, Sprite paper)
        {
            Assert.That(paper, Is.Not.Null);
            Assert.That(surface.sprite, Is.SameAs(paper), surface.name);
            Assert.That(surface.type, Is.EqualTo(Image.Type.Sliced), surface.name);
            Assert.That(surface.color.r, Is.EqualTo(1f));
            Assert.That(surface.color.g, Is.EqualTo(1f));
            Assert.That(surface.color.b, Is.EqualTo(1f));
            Assert.That(surface.color.a, Is.EqualTo(1f));
            Assert.That(surface.transform.Find("Material Timber Rim"), Is.Null, "The dedicated paper edge must not receive a second material frame.");
            Color brown = new Color(.22f, .13f, .06f);
            Assert.That(NauticalSurfaceSkin.ReadableTextColor(surface.transform, brown), Is.EqualTo(brown));
            Assert.That(NauticalSurfaceSkin.ReadableTextColor(surface.transform, Color.white).grayscale, Is.LessThan(.25f));
        }
    }
}
