using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Tests runtime destruction and pagination on an independent active canvas.
    /// The gameplay controller stays inactive, so Awake, audio, profile restoration
    /// and gameplay updates never run. Only showroom page callbacks are invoked.
    /// </summary>
    public sealed class ComponentShopShowroomPaginationTests
    {
        private static readonly DateTime FixtureDate = new DateTime(2026, 9, 1);
        private GameObject host;
        private GameObject canvasObject;
        private CoreGameplayDemoController controller;
        private CoreVoyageSession session;
        private ComponentArtCatalog componentArt;
        private RectTransform panel;
        private PreferenceSnapshot profileGoldBefore;
        private PreferenceSnapshot[] purchasePreferences;

        [SetUp]
        public void CreateIsolatedShowroom()
        {
            Assert.That(Application.isPlaying, Is.True);
            profileGoldBefore = PreferenceSnapshot.Read(CoreGameplayDemoController.ProfileGoldKey);
            host = new GameObject("Inactive Component Shop Pagination Test Controller");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null, "The gameplay controller's Awake must not run in this test.");

            session = new CoreVoyageSession();
            session.RestoreProfile(1250, "灰鸥港");
            componentArt = new ComponentArtCatalog();
            WriteField("session", session);
            WriteField("componentArt", componentArt);
            WriteField("componentShopArt", new ComponentShopArtCatalog());
            // The font is a borrowed Resources asset; only the circle is generated.
            WriteField("interfaceFont", Invoke("CreateInterfaceFont"));
            WriteField("circleSprite", Invoke("CreateCircleSprite"));
            Invoke("BuildCanvas", "Isolated Component Shop Pagination Canvas");
            RectTransform content = ReadField<RectTransform>("contentRoot");
            canvasObject = content.parent.gameObject;
            canvasObject.transform.SetParent(null, false);

            GameObject panelObject = new GameObject("Component Shop Panel", typeof(RectTransform));
            panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(content, false);
            panel.sizeDelta = new Vector2(1820f, 760f);
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(host.activeSelf, Is.False);
        }

        [UnityTearDown]
        public IEnumerator DisposeIsolatedShowroom()
        {
            // An object that has never been active need not receive OnDestroy.
            // Explicitly release its generated circle without touching shared art/font.
            if (controller != null) Invoke("ReleaseGeneratedVisualResources");
            if (canvasObject != null) Object.Destroy(canvasObject);
            if (host != null) Object.Destroy(host);
            yield return null;
            if (profileGoldBefore != null) profileGoldBefore.AssertUnchanged();
            if (purchasePreferences != null)
                foreach (PreferenceSnapshot preference in purchasePreferences) preference.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator FourOffers_PageRoundTripDisablesOldGroupBeforeDeferredDestruction()
        {
            return VerifyPageRoundTrip(4);
        }

        [UnityTest]
        public IEnumerator FiveOffers_PageRoundTripDisablesOldGroupBeforeDeferredDestruction()
        {
            return VerifyPageRoundTrip(5);
        }

        private IEnumerator VerifyPageRoundTrip(int count)
        {
            IReadOnlyList<PortComponentShopStockItem> stock = CreateStock(count);
            PortComponentShopStockItem[] originalStock = stock.ToArray();
            purchasePreferences = stock.Select(item => PreferenceSnapshot.Read(
                CoreGameplayDemoController.ComponentShopPurchaseKey(item.Id))).ToArray();
            decimal gold = session.ProfileGold;
            Invoke("BuildComponentShopShowroom", panel, stock, FixtureDate, 0);
            AssertOffers(stock.Take(3), false);
            Button[] firstOffers = Offers(false);
            GameObject[] firstGroup = DirectChildren();
            Assert.That(RequireActiveButton("Component Shop Previous Display").interactable, Is.False);
            Button next = RequireActiveButton("Component Shop Next Display");
            Assert.That(next.interactable, Is.True);

            next.onClick.Invoke();
            Assert.That(ReadField<int>("componentShopDisplayPage"), Is.EqualTo(1));
            Assert.That(firstGroup.All(item => item != null && !item.activeSelf), Is.True,
                "Old showroom roots must be inactive immediately, before Destroy completes at frame end.");
            Assert.That(firstOffers.All(item => !item.gameObject.activeInHierarchy && !item.IsActive()), Is.True,
                "A pending-destruction offer must stop rendering and accepting UI input in the same frame.");
            AssertOffers(stock.Skip(3), false);
            AssertUnchanged(stock, originalStock, gold);

            yield return null;

            Assert.That(firstGroup.All(item => item == null), Is.True, "The first group must be destroyed after the frame ends.");
            AssertOffers(stock.Skip(3), true);
            Assert.That(RequireActiveButton("Component Shop Next Display").interactable, Is.False);
            Button previous = RequireActiveButton("Component Shop Previous Display");
            Assert.That(previous.interactable, Is.True);
            GameObject[] secondGroup = DirectChildren();

            previous.onClick.Invoke();
            Assert.That(ReadField<int>("componentShopDisplayPage"), Is.Zero);
            Assert.That(secondGroup.All(item => item != null && !item.activeSelf), Is.True,
                "Returning to the first page must immediately disable the outgoing group too.");
            AssertOffers(stock.Take(3), false);
            AssertUnchanged(stock, originalStock, gold);

            yield return null;

            Assert.That(secondGroup.All(item => item == null), Is.True);
            AssertOffers(stock.Take(3), true);
            Assert.That(RequireActiveButton("Component Shop Previous Display").interactable, Is.False);
            Assert.That(RequireActiveButton("Component Shop Next Display").interactable, Is.True);
            AssertUnchanged(stock, originalStock, gold);
        }

        private IReadOnlyList<PortComponentShopStockItem> CreateStock(int count)
        {
            string fixtureId = "runtime-pagination-test|" + Guid.NewGuid().ToString("N");
            ComponentSlot[] slots = { ComponentSlot.Bow, ComponentSlot.Sail, ComponentSlot.Instrument, ComponentSlot.Plating, ComponentSlot.Cannon };
            return Enumerable.Range(0, count).Select(index =>
            {
                ComponentArtEntry entry = componentArt.Entries.First(item => item.Slot == slots[index]);
                return new PortComponentShopStockItem(fixtureId + "|" + index, session.CurrentPort,
                    PortComponentShopCatalog.MonthKey(FixtureDate), entry, 1f, 200 + index * 10);
            }).ToList().AsReadOnly();
        }

        private void AssertUnchanged(IReadOnlyList<PortComponentShopStockItem> stock, PortComponentShopStockItem[] originalStock, decimal gold)
        {
            CollectionAssert.AreEqual(originalStock, stock, "Pagination must retain the exact same stock objects and order.");
            Assert.That(controller.Session, Is.SameAs(session));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(host.activeSelf, Is.False, "No page operation may activate the gameplay controller.");
            profileGoldBefore.AssertUnchanged();
            foreach (PreferenceSnapshot preference in purchasePreferences) preference.AssertUnchanged();
        }

        private void AssertOffers(IEnumerable<PortComponentShopStockItem> expected, bool includeInactive)
        {
            CollectionAssert.AreEqual(expected.Select(item => "Component Shop Offer " + item.Id),
                Offers(includeInactive).Select(button => button.name),
                includeInactive ? "After destruction only the current page may remain in the hierarchy." : "Only the current page may be active.");
            Assert.That(panel.GetComponentsInChildren<Transform>(false).Any(item =>
                item.name.StartsWith("Component Shop Merchant") || item.name.StartsWith("Component Shop Mount") ||
                item.name.Contains("Furniture Foot") || item.name.Contains("Load Contact")), Is.False);
            foreach (Button offer in Offers(includeInactive))
            {
                var model = offer.GetComponentsInChildren<RawImage>().Single(item => item.name.StartsWith("Component Shop Model "));
                var aura = offer.GetComponentsInChildren<RawImage>().Single(item => item.name.StartsWith("Component Shop Rarity Aura "));
                var plaque = offer.GetComponentsInChildren<RawImage>().Single(item =>
                    item.name.StartsWith("Component Shop Plaque ") && item.texture == RegionalShopFurniture.For(session.CurrentPort).Atlas);
                Assert.That(aura.color, Is.EqualTo(Color.clear), "Offers must not render a persistent rarity halo.");
                RectTransform modelRect = model.rectTransform, plaqueRect = plaque.rectTransform;
                Assert.That(modelRect.anchoredPosition.y - modelRect.sizeDelta.y * 0.5f,
                    Is.GreaterThan(plaqueRect.anchoredPosition.y + plaqueRect.sizeDelta.y * 0.5f + 19f));
                Assert.That(modelRect.localRotation, Is.EqualTo(Quaternion.identity), "Do not rotate a part to fit a removed rack.");
                foreach (RawImage target in new[] { model, plaque })
                {
                    Assert.That(target.raycastTarget, Is.True);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target.gameObject), Is.SameAs(offer.gameObject));
                }
                var position = modelRect.anchoredPosition;
                var scale = modelRect.localScale;
                var normal = plaque.color;
                var trigger = offer.GetComponent<EventTrigger>();
                trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
                Assert.That(plaque.color, Is.EqualTo(normal), "Hover must not repaint the component plaque.");
                Assert.That(modelRect.anchoredPosition, Is.EqualTo(position));
                Assert.That(modelRect.localScale, Is.EqualTo(scale));
                trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerExit).callback.Invoke(new BaseEventData(null));
                Assert.That(plaque.color, Is.EqualTo(normal));
            }
        }

        private Button[] Offers(bool includeInactive)
        {
            return panel.GetComponentsInChildren<Button>(includeInactive)
                .Where(button => button.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal)).ToArray();
        }

        private GameObject[] DirectChildren()
        {
            return Enumerable.Range(0, panel.childCount).Select(index => panel.GetChild(index).gameObject).ToArray();
        }

        private Button RequireActiveButton(string name)
        {
            Button button = panel.GetComponentsInChildren<Button>(false).SingleOrDefault(item => item.name == name);
            Assert.That(button, Is.Not.Null, name);
            return button;
        }

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(controller, arguments);
        }

        private T ReadField<T>(string name)
        {
            return (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        }

        private void WriteField(string name, object value)
        {
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
        }

        private sealed class PreferenceSnapshot
        {
            private string key;
            private bool existed;
            private int value;

            public static PreferenceSnapshot Read(string key)
            {
                return new PreferenceSnapshot { key = key, existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, int.MinValue) };
            }

            public void AssertUnchanged()
            {
                Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(existed), key + ": preference existence changed.");
                Assert.That(PlayerPrefs.GetInt(key, int.MinValue), Is.EqualTo(value), key + ": preference value changed.");
            }
        }
    }
}
