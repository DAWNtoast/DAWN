using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
        public void SharedMaterialsKeepCanvasDetailAndUseTheMarketPaperTexture()
        {
            foreach (Sprite sprite in new[] { NauticalSurfaceSkin.Canvas })
            {
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.texture.width, Is.GreaterThanOrEqualTo(1024));
                Assert.That(sprite.texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(sprite.texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            }
            Sprite paper = NauticalSurfaceSkin.Paper;
            Assert.That(paper, Is.Not.Null);
            Assert.That(paper.texture, Is.SameAs(MarketShopSkin.Paper.texture));
            Assert.That(paper.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(paper.name, Is.EqualTo(NauticalSurfaceSkin.PaperFillName));
            Assert.That(paper.rect.size, Is.EqualTo(new Vector2(512f, 512f)));
            Assert.That(paper.rect.center, Is.EqualTo(new Vector2(paper.texture.width, paper.texture.height) * .5f));
            Assert.That(paper.border, Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void GameplayPaperRelease_UnpinsSharedPaperWithoutDestroyingLiveUi()
        {
            Sprite menuPaper = NauticalSurfaceSkin.ReadingPaper;
            Sprite investment = NauticalSurfaceSkin.InvestmentPaper;
            Sprite collection = NauticalSurfaceSkin.CollectionPaper;
            Sprite fill = NauticalSurfaceSkin.Paper;
            Assert.That(investment, Is.SameAs(menuPaper));
            Assert.That(collection, Is.SameAs(menuPaper));
            host = new GameObject("Retained Investment Paper", typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.sprite = investment;
            var fillHost = new GameObject("Retained Fill", typeof(RectTransform), typeof(Image));
            fillHost.transform.SetParent(host.transform, false);
            Image fillImage = fillHost.GetComponent<Image>();
            fillImage.sprite = fill;
            NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
            NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
            Assert.That(investment != null && collection != null, Is.True,
                "Releasing the cache must let the engine decide whether any UI still needs a sprite.");
            Assert.That(image.sprite, Is.SameAs(investment));
            Assert.That(fillImage.sprite, Is.SameAs(fill));
            Assert.That(investment.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None));
            Assert.That(collection.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None));
            Assert.That(fill.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None));
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(image.sprite), Is.True);
            Assert.That(NauticalSurfaceSkin.ReadableTextColor(image.transform, Color.white).grayscale, Is.LessThan(.25f));
            Sprite reopened = NauticalSurfaceSkin.InvestmentPaper;
            Assert.That(reopened, Is.Not.SameAs(investment));
            Assert.That(reopened, Is.SameAs(NauticalSurfaceSkin.ReadingPaper));
            Assert.That(reopened, Is.SameAs(NauticalSurfaceSkin.CollectionPaper));
            Assert.That(reopened.texture, Is.SameAs(investment.texture));
            Assert.That(reopened.border, Is.EqualTo(investment.border));
            Assert.That(NauticalSurfaceSkin.Paper, Is.Not.SameAs(fill));
            Assert.That(NauticalSurfaceSkin.Paper.texture, Is.SameAs(fill.texture));
            Assert.That(NauticalSurfaceSkin.Paper.rect, Is.EqualTo(fill.rect));
        }

        [TestCase("UISurfaces/aged_reading_parchment_v1")]
        [TestCase("UISurfaces/investment_charter_parchment_v1")]
        [TestCase("UISurfaces/collection_archive_parchment_v1")]
        public void LegacyPaperEntrypointsShareMarketPaperAndUsableNineSliceBorders(string path)
        {
            Sprite sprite = DedicatedPaper(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(sprite, Is.SameAs(MarketShopSkin.Paper));
            Assert.That(sprite.texture, Is.SameAs(Resources.Load<Texture2D>(MarketShopSkin.PaperResourcePath)));
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
                "The market paper centre remains a supported small-card and control surface.");
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(null), Is.False);
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(NauticalSurfaceSkin.Canvas), Is.False);
        }

        [Test]
        public void LivePaperEntrypointsDoNotLoadLegacyPaperTextures()
        {
            string[] legacyNames = { "journal_parchment_v1", "aged_reading_parchment_v1",
                "investment_charter_parchment_v1", "collection_archive_parchment_v1" };
            int[] loadedBefore = Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(texture => legacyNames.Contains(texture.name)).Select(texture => texture.GetInstanceID()).ToArray();
            NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
            foreach (Sprite paper in new[] { NauticalSurfaceSkin.Paper, NauticalSurfaceSkin.ReadingPaper,
                NauticalSurfaceSkin.InvestmentPaper, NauticalSurfaceSkin.CollectionPaper })
                Assert.That(paper.texture, Is.SameAs(MarketShopSkin.Paper.texture));
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>().Where(texture => legacyNames.Contains(texture.name)))
                Assert.That(loadedBefore, Does.Contain(texture.GetInstanceID()), texture.name + " must not be loaded by the live paper API.");
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        public void SerializedLegacyReadingBindingsMigrateTheCompleteStyle(int serializedSkin)
        {
            host = new GameObject("Legacy Paper Binding", typeof(RectTransform), typeof(Image), typeof(PrefabRuntimeSkinBindings));
            Image image = host.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(620f, 380f);
            image.rectTransform.anchoredPosition = new Vector2(17f, -23f);
            image.raycastTarget = false;
            image.sprite = NauticalSurfaceSkin.Canvas;
            image.overrideSprite = WoodButtonSkin.Face;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 7f;
            image.color = new Color(.65f, .5f, .35f, .8f);
            PrefabRuntimeSkinBindings bindings = host.GetComponent<PrefabRuntimeSkinBindings>();
            SetSerializedSkinBinding(bindings, image, serializedSkin);
            for (int pass = 0; pass < 2; pass++)
            {
                bindings.Apply();
                AssertDedicatedPaperStyle(image, MarketShopSkin.Paper);
                Assert.That(image.overrideSprite, Is.SameAs(MarketShopSkin.Paper));
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(620f, 380f)));
                Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(17f, -23f)));
                Assert.That(image.raycastTarget, Is.False);
            }
        }

        [TestCase(Image.Type.Simple)]
        [TestCase(Image.Type.Tiled)]
        public void SerializedPaperFillBindingPreservesTintGeometryAndTilingDensity(Image.Type type)
        {
            host = new GameObject("Legacy Fill Binding", typeof(RectTransform), typeof(Image), typeof(PrefabRuntimeSkinBindings));
            Image image = host.GetComponent<Image>();
            Texture2D texture = MarketShopSkin.Paper.texture;
            Sprite legacy = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100f);
            legacy.name = NauticalSurfaceSkin.PaperPath;
            try
            {
                image.sprite = legacy;
                image.overrideSprite = WoodButtonSkin.Face;
                image.type = type;
                image.rectTransform.sizeDelta = new Vector2(240f, 32f);
                image.color = new Color(.35f, .65f, .48f, .4f);
                Color tint = image.color;
                image.raycastTarget = false;
                image.pixelsPerUnitMultiplier = legacy.rect.width / 160f;
                PrefabRuntimeSkinBindings bindings = host.GetComponent<PrefabRuntimeSkinBindings>();
                SetSerializedSkinBinding(bindings, image, 1);
                for (int pass = 0; pass < 3; pass++)
                {
                    bindings.Apply();
                    Assert.That(image.sprite, Is.SameAs(NauticalSurfaceSkin.Paper));
                    Assert.That(image.overrideSprite, Is.SameAs(NauticalSurfaceSkin.Paper));
                    Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(NauticalSurfaceSkin.Paper.rect.width / 160f).Within(.0001f));
                    Assert.That(image.type, Is.EqualTo(type));
                    Assert.That(image.color, Is.EqualTo(tint));
                    Assert.That(image.raycastTarget, Is.False);
                    Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(240f, 32f)));
                }
            }
            finally { Object.DestroyImmediate(legacy); }
        }

        [TestCase(NauticalSurfaceSkin.PaperPath, 1)]
        [TestCase(NauticalSurfaceSkin.PaperFillName, 1)]
        [TestCase(NauticalSurfaceSkin.ReadingPaperPath, 2)]
        [TestCase(NauticalSurfaceSkin.InvestmentPaperPath, 4)]
        [TestCase(NauticalSurfaceSkin.CollectionPaperPath, 5)]
        [TestCase(MarketShopSkin.PaperResourcePath, 6)]
        public void PrefabCaptureRecognizesLegacyNamesAndCurrentMarketSkins(string name, int serializedSkin)
        {
            host = new GameObject("Paper Capture", typeof(RectTransform), typeof(Image), typeof(PrefabRuntimeSkinBindings));
            Image image = host.GetComponent<Image>();
            Sprite example = MarketShopSkin.Paper;
            Sprite named = Sprite.Create(example.texture, example.rect, Vector2.one * .5f, 100f);
            named.name = name;
            try
            {
                image.sprite = named;
                PrefabRuntimeSkinBindings bindings = host.GetComponent<PrefabRuntimeSkinBindings>();
                bindings.CaptureBindings();
                var serialized = new SerializedObject(bindings);
                SerializedProperty captured = serialized.FindProperty("bindings");
                Assert.That(captured.arraySize, Is.EqualTo(1));
                Assert.That(captured.GetArrayElementAtIndex(0).FindPropertyRelative("Skin").intValue, Is.EqualTo(serializedSkin));
                image.sprite = null;
                bindings.Apply();
                Assert.That(image.sprite, Is.SameAs(serializedSkin == 1 ? NauticalSurfaceSkin.Paper : MarketShopSkin.Paper));
            }
            finally { Object.DestroyImmediate(named); }
        }

        [Test]
        public void CapturedMarketPaperPreservesItsCustomFourTimesBorderScale()
        {
            host = new GameObject("Market Paper Capture", typeof(RectTransform), typeof(Image), typeof(PrefabRuntimeSkinBindings));
            Image image = host.GetComponent<Image>();
            MarketShopSkin.ApplyPanel(image);
            image.pixelsPerUnitMultiplier = 4f;
            PrefabRuntimeSkinBindings bindings = host.GetComponent<PrefabRuntimeSkinBindings>();
            bindings.CaptureBindings();
            image.sprite = null;
            image.type = Image.Type.Simple;
            for (int pass = 0; pass < 2; pass++)
            {
                bindings.Apply();
                Assert.That(image.sprite, Is.SameAs(MarketShopSkin.Paper));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(4f),
                    "A current market panel must keep its authored border scale across prefab binding replay.");
            }
        }

        [TestCase("UI/MainMenuSettings", "Settings Card")]
        [TestCase("UI/MainMenuAchievements", "Achievements Card")]
        [TestCase("UI/MainMenuNewGameConfirmation", "Confirmation Card")]
        [TestCase("UI/MainMenuSaveNotice", "Voyage Save Notice Card")]
        [TestCase("UI/PortSettings", "Port Settings Sheet")]
        public void CachedDialogPrefabsReplayTheMarketSkinWithoutChangingLayout(string resource, string surfaceName)
        {
            GameObject prefab = Resources.Load<GameObject>(resource);
            Assert.That(prefab, Is.Not.Null, resource);
            host = Object.Instantiate(prefab);
            PrefabRuntimeSkinBindings bindings = host.GetComponent<PrefabRuntimeSkinBindings>();
            Assert.That(bindings, Is.Not.Null, resource);
            Image surface = host.GetComponentsInChildren<Image>(true).Single(image => image.name == surfaceName);
            Vector2 size = surface.rectTransform.sizeDelta, position = surface.rectTransform.anchoredPosition;
            bool hitTesting = surface.raycastTarget;
            for (int pass = 0; pass < 2; pass++)
            {
                bindings.Apply();
                AssertDedicatedPaperStyle(surface, MarketShopSkin.Paper);
                Assert.That(surface.rectTransform.sizeDelta, Is.EqualTo(size));
                Assert.That(surface.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(surface.raycastTarget, Is.EqualTo(hitTesting));
                foreach (Image image in host.GetComponentsInChildren<Image>(true))
                    if (NauticalSurfaceSkin.IsPaperSurface(image.sprite))
                        Assert.That(image.sprite.texture, Is.SameAs(MarketShopSkin.Paper.texture), resource + " / " + image.name);
            }
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

        [TestCase(CoreGameplayPreviewScreen.Market, "Market Item Detail", MarketShopSkin.PaperResourcePath, "Market Shop Title")]
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
                AssertDedicatedPaperStyle(surface, MarketShopSkin.Paper);
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
                    if (!button.gameObject.activeInHierarchy) continue;
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

        private static void SetSerializedSkinBinding(PrefabRuntimeSkinBindings bindings, Image image, int skin)
        {
            var serialized = new SerializedObject(bindings);
            SerializedProperty entries = serialized.FindProperty("bindings");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("Image").objectReferenceValue = image;
            entry.FindPropertyRelative("Skin").intValue = skin;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite DedicatedPaper(string path)
        {
            if (path == MarketShopSkin.PaperResourcePath) return MarketShopSkin.Paper;
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
            Assert.That(surface.pixelsPerUnitMultiplier, Is.EqualTo(2f), surface.name);
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
