using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentShopShowroomLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private ComponentArtCatalog catalog;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }
        private static IEnumerable<TestCaseData> StockCases()
        {
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                foreach (int count in new[] { 1, 5, 6, 16 })
                    yield return new TestCaseData(count, slot).SetName("ShopCatalogue_" + slot + "_Stock" + count);
        }
        [TestCaseSource(nameof(StockCases))]
        public void EveryCategory_PagesAllOffersWithOneLargeAspectFittedHero(int count, ComponentSlot slot)
        {
            CreatePreview();
            var stock = CreateStock(count, slot);
            var seen = new HashSet<string>();
            decimal gold = controller.Session.ProfileGold;
            for (int page = 0; page < ComponentShopShowroomLayout.PageCount(count); page++)
            {
                BuildStock(stock, slot, page);
                var offers = Offers();
                Assert.That(offers.Length, Is.EqualTo(Math.Min(5, count - page * 5)));
                foreach (Button button in offers)
                {
                    var offer = stock.Single(item => button.name == "Component Shop Offer " + item.Id);
                    Assert.That(seen.Add(offer.Id), Is.True);
                    Assert.That(button.GetComponentsInChildren<RawImage>().Single().texture, Is.SameAs(offer.Entry.Texture));
                }
                var hero = host.GetComponentsInChildren<RawImage>().Single(item => item.name.StartsWith("Component Shop Hero "));
                var selected = stock[page * 5].Entry;
                Assert.That(hero.texture, Is.SameAs(selected.ShipLayer));
                Assert.That(hero.raycastTarget, Is.True);
                Assert.That(hero.GetComponent<Button>().targetGraphic, Is.SameAs(hero));
                float aspect = hero.texture.width * hero.uvRect.width / (hero.texture.height * hero.uvRect.height);
                Assert.That(hero.rectTransform.sizeDelta.x / hero.rectTransform.sizeDelta.y, Is.EqualTo(aspect).Within(.001f));
                Assert.That(Mathf.Max(hero.rectTransform.sizeDelta.x, hero.rectTransform.sizeDelta.y), Is.GreaterThan(slot == ComponentSlot.Instrument ? 300f : 530f));
                Assert.That(hero.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0f, 25f)));
                Assert.That(hero.rectTransform.sizeDelta.x, Is.LessThanOrEqualTo(slot == ComponentSlot.Instrument ? 390.01f : 1100.01f));
                Assert.That(hero.rectTransform.sizeDelta.y, Is.LessThanOrEqualTo(slot == ComponentSlot.Instrument ? 330.01f : 610.01f));
                Assert.That(host.GetComponentsInChildren<Transform>().Any(item => item.name.Contains("Component Shop Summary") ||
                    item.name.Contains("Scale Support") || item.name.Contains("Contact Shadow")), Is.False);
                Assert.That(host.GetComponentsInChildren<Button>().Any(item => item.name.StartsWith("Purchase")), Is.False);
                Assert.That(host.GetComponentsInChildren<Transform>().Any(item => item.name.Contains("Merchant")), Is.False);
            }
            CollectionAssert.AreEquivalent(stock.Select(item => item.Id), seen);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }
        [Test]
        public void CategoryPageAndSelection_DoNotChangeStockOrSpendGold_EmptyCategoryIsExplicit()
        {
            CreatePreview();
            var bows = CreateStock(6, ComponentSlot.Bow);
            var sails = CreateStock(2, ComponentSlot.Sail);
            var stock = bows.Concat(sails).ToList();
            BuildStock(stock, ComponentSlot.Bow, 0);
            decimal gold = controller.Session.ProfileGold;
            Offers().First().onClick.Invoke();
            AssertNoDossier();
            Find<Button>("Component Shop Next Display").onClick.Invoke();
            CollectionAssert.AreEqual(bows.Skip(5).Select(item => "Component Shop Offer " + item.Id), Offers().Select(item => item.name));
            Assert.That(host.GetComponentsInChildren<RawImage>().Single(item => item.name.StartsWith("Component Shop Hero ")).texture,
                Is.SameAs(bows[5].Entry.ShipLayer));
            Assert.That(Read<int>("componentShopDisplayPage"), Is.EqualTo(1));
            Offers().First().onClick.Invoke();
            AssertNoDossier();
            Find<Button>("Component Shop Previous Display").onClick.Invoke();
            Offers().First().onClick.Invoke();
            AssertNoDossier();
            Find<Button>("Component Shop Category Sail").onClick.Invoke();
            Assert.That(Read<int>("componentShopDisplayPage"), Is.Zero);
            CollectionAssert.AreEqual(sails.Select(item => "Component Shop Offer " + item.Id), Offers().Select(item => item.name));
            Find<Button>("Component Shop Category Cannon").onClick.Invoke();
            Assert.That(Offers(), Is.Empty);
            Assert.That(Find<Text>("Component Shop Empty Category").text, Does.Contain("暂无"));
            Assert.That(host.GetComponentsInChildren<Button>().Any(item => item.name.StartsWith("Purchase")), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>().Any(item => item.name.Contains("Summary") || item.name.Contains("Parchment")), Is.False);
            Assert.That(Find<Button>("Component Shop Previous Display").interactable, Is.False);
            Assert.That(Find<Button>("Component Shop Next Display").interactable, Is.False);
            Find<Button>("Component Shop Category Bow").onClick.Invoke();
            Assert.That(Offers().Length, Is.EqualTo(5));
            Offers().First().onClick.Invoke();
            AssertNoDossier();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (var item in stock) Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(item.Id), 0), Is.Zero);
        }
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        public void DossierRetainsCategoryPageAndSelectionOnReturn(CoreGameplayPreviewPort port)
        {
            CreatePreview(port);
            Offers().Last().onClick.Invoke();
            string selected = Read<string>("componentShopShowroomOfferId");
            int page = Read<int>("componentShopDisplayPage"), category = Read<int>("componentShopCategory");
            AssertNoDossier();
            Offers().Last().onClick.Invoke();
            Assert.That(Find<RectTransform>("Component Shop Detail"), Is.Not.Null);
            Assert.That(Read<string>("selectedComponentShopOfferId"), Is.EqualTo(selected));
            Assert.That(Read<int>("componentDossierPage"), Is.Zero);
            Assert.That(Find<RectTransform>("Component Dossier Overview Page").gameObject.activeInHierarchy, Is.True);
            Find<Button>("Return To Port Hub").onClick.Invoke();
            Assert.That(Read<string>("componentShopShowroomOfferId"), Is.EqualTo(selected));
            Assert.That(Read<int>("componentShopDisplayPage"), Is.EqualTo(page));
            Assert.That(Read<int>("componentShopCategory"), Is.EqualTo(category));
            Assert.That(Find<Button>("Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            Find<Button>("Component Shop Offer " + selected).onClick.Invoke();
            AssertNoDossier();
        }
        [TestCase(EventTriggerType.PointerEnter, 0)]
        [TestCase(EventTriggerType.Select, 0)]
        [TestCase(EventTriggerType.PointerEnter, 1)]
        [TestCase(EventTriggerType.Select, 1)]
        public void CatalogueHoverAndFocusLeaveHeroUnchanged_FirstClickPreviewsWithoutOpeningDossier(EventTriggerType triggerType, int page)
        {
            CreatePreview();
            var stock = CreateStock(10, ComponentSlot.Bow);
            BuildStock(stock, ComponentSlot.Bow, page);
            Button[] offers = Offers();
            PortComponentShopStockItem selected = stock[page * 5 + 3];
            decimal gold = controller.Session.ProfileGold;
            string dossier = Read<string>("selectedComponentShopOfferId");
            string initialSelection = Read<string>("componentShopShowroomOfferId");
            RawImage initialHero = Hero();
            if (triggerType == EventTriggerType.PointerEnter)
                ExecuteEvents.Execute(offers[3].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            else
                ExecuteEvents.Execute(offers[3].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            Assert.That(Read<string>("componentShopShowroomOfferId"), Is.EqualTo(initialSelection));
            Assert.That(Hero(), Is.SameAs(initialHero), "Hover and keyboard focus must not change the displayed component.");
            AssertNoDossier();
            offers[3].onClick.Invoke();
            CollectionAssert.AreEqual(offers, Offers(), "Preview must preserve the same live catalogue controls.");
            Assert.That(Read<string>("componentShopShowroomOfferId"), Is.EqualTo(selected.Id));
            Assert.That(Read<int>("componentShopDisplayPage"), Is.EqualTo(page));
            Assert.That(Read<string>("selectedComponentShopOfferId"), Is.EqualTo(dossier));
            Assert.That(host.GetComponentsInChildren<RawImage>().Single(item => item.name.StartsWith("Component Shop Hero ")).texture,
                Is.SameAs(selected.Entry.ShipLayer));
            AssertNoDossier();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (var item in stock) Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(item.Id), 0), Is.Zero);
        }
        [Test]
        public void InitiallyDisplayedOfferRequiresFirstClickBeforeSecondClickOpensDossier()
        {
            CreatePreview();
            Button offer = Offers().First();
            string selected = Read<string>("componentShopShowroomOfferId");
            RawImage initialHero = Hero();
            decimal gold = controller.Session.ProfileGold;
            offer.onClick.Invoke();
            Assert.That(Hero(), Is.SameAs(initialHero));
            AssertNoDossier();
            offer.onClick.Invoke();
            Assert.That(Find<RectTransform>("Component Shop Detail"), Is.Not.Null);
            Assert.That(Read<string>("selectedComponentShopOfferId"), Is.EqualTo(selected));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }
        [Test]
        public void ChangingCatalogueSelectionRequiresAnotherClickBeforeOpeningDossier()
        {
            CreatePreview();
            var stock = CreateStock(3, ComponentSlot.Bow);
            BuildStock(stock, ComponentSlot.Bow, 0);
            Button[] offers = Offers();
            decimal gold = controller.Session.ProfileGold;
            foreach (int index in new[] { 0, 1, 0, 2 })
            {
                offers[index].onClick.Invoke();
                Assert.That(Read<string>("componentShopShowroomOfferId"), Is.EqualTo(stock[index].Id));
                Assert.That(Hero().texture, Is.SameAs(stock[index].Entry.ShipLayer));
                AssertNoDossier();
                CollectionAssert.AreEqual(offers, Offers());
            }
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (var item in stock) Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(item.Id), 0), Is.Zero);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ClickingHeroOpensTheDisplayedOfferImmediately(bool selectAnotherOffer)
        {
            CreatePreview();
            if (selectAnotherOffer) Offers().Last().onClick.Invoke();
            string selected = Read<string>("componentShopShowroomOfferId");
            RawImage hero = Hero();
            Texture modelTexture = hero.texture;
            decimal gold = controller.Session.ProfileGold;
            AssertNoDossier();
            Assert.That(hero.raycastTarget, Is.True);
            Button button = hero.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.targetGraphic, Is.SameAs(hero));
            button.onClick.Invoke();
            Assert.That(Find<RectTransform>("Component Shop Detail"), Is.Not.Null);
            Assert.That(Read<string>("selectedComponentShopOfferId"), Is.EqualTo(selected));
            Assert.That(Find<RawImage>("Component Detail Model").texture, Is.SameAs(modelTexture));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }
        [Test]
        public void SoldOfferIsInspectableWithBothPurchaseButtonsDisabled()
        {
            CreatePreview(); var stock = CreateStock(1, ComponentSlot.Cannon);
            string key = CoreGameplayDemoController.ComponentShopPurchaseKey(stock[0].Id);
            try
            {
                PlayerPrefs.SetInt(key, 1); BuildStock(stock, ComponentSlot.Cannon, 0);
                Assert.That(Offers()[0].interactable, Is.True);
                Assert.That(Offers()[0].GetComponentsInChildren<Text>().Any(item => item.text.Contains("已售")), Is.True);
                var panel = Find<RectTransform>("Component Shop Panel");
                for (int index = panel.childCount - 1; index >= 0; index--) Object.DestroyImmediate(panel.GetChild(index).gameObject);
                typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopDetail", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { panel, stock[0] });
                Assert.That(Find<Button>("Purchase Component " + stock[0].Id).interactable, Is.False);
                Assert.That(Find<Button>("Purchase And Equip Component " + stock[0].Id).interactable, Is.False);
            }
            finally { PlayerPrefs.DeleteKey(key); }
        }
        [Test]
        public void CompactHeader_UsesTheSameGeometryAcrossShopAndOtherFacilities()
        {
            CreatePreview();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, CoreGameplayPreviewPort.GrayGull, 0);
            RectTransform header = Find<RectTransform>("Port Facility Header");
            RectTransform title = Find<RectTransform>("Port Facility Title");
            RectTransform next = Find<RectTransform>("Next Port Facility");
            Vector2 titlePosition = title.anchoredPosition, titleSize = title.sizeDelta, nextPosition = next.anchoredPosition;
            MethodInfo apply = typeof(CoreGameplayDemoController).GetMethod("ApplyComponentShopHeaderLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            apply.Invoke(controller, new object[] { header, true });
            Assert.That(title.anchoredPosition, Is.EqualTo(titlePosition));
            Assert.That(next.anchoredPosition.x, Is.GreaterThan(title.anchoredPosition.x));
            apply.Invoke(controller, new object[] { header, false });
            Assert.That(title.anchoredPosition, Is.EqualTo(titlePosition));
            Assert.That(title.sizeDelta, Is.EqualTo(titleSize));
            Assert.That(next.anchoredPosition, Is.EqualTo(nextPosition));
        }
        private void CreatePreview(CoreGameplayPreviewPort port = CoreGameplayPreviewPort.GrayGull)
        {
            host = new GameObject("Component Shop Showroom Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShop, port, 0);
            catalog = new ComponentArtCatalog();
        }
        private List<PortComponentShopStockItem> CreateStock(int count, ComponentSlot slot)
        {
            string prefix = "showroom-test|" + Guid.NewGuid().ToString("N");
            var entries = catalog.ExpandedEntries.Where(item => item.Slot == slot).Take(count).ToArray();
            return entries.Select((entry, index) => new PortComponentShopStockItem(prefix + index, controller.Session.CurrentPort,
                PortComponentShopCatalog.MonthKey(controller.Session.GameDateTime), entry, 200)).ToList();
        }
        private void BuildStock(IReadOnlyList<PortComponentShopStockItem> stock, ComponentSlot slot, int page)
        {
            Write("componentShopCategory", (int)slot); Write("componentShopDisplayPage", page); Write("componentShopShowroomOfferId", null);
            var panel = Find<RectTransform>("Component Shop Panel");
            for (int index = panel.childCount - 1; index >= 0; index--) Object.DestroyImmediate(panel.GetChild(index).gameObject);
            typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopShowroom", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { panel, stock, controller.Session.GameDateTime, 0 });
            Canvas.ForceUpdateCanvases();
        }
        private Button[] Offers() => host.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Component Shop Offer ")).ToArray();
        private RawImage Hero() => host.GetComponentsInChildren<RawImage>().Single(item => item.name.StartsWith("Component Shop Hero "));
        private void AssertNoDossier()
        {
            Assert.That(host.GetComponentsInChildren<Transform>().Any(item => item.name == "Component Shop Detail"), Is.False);
            Assert.That(Read<string>("selectedComponentShopOfferId"), Is.Null.Or.Empty);
        }
        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name); return result;
        }
        private T Read<T>(string name) => (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        private void Write(string name, object value) => typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
    }
}
