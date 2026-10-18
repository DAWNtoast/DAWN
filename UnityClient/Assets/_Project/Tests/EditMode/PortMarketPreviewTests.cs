using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class PortMarketPreviewTests
    {
        [Test]
        public void MarketPreview_HasNoFooterAndRetainsEarnedPurchaseDiscountsInQuotes()
        {
            GameObject host = new GameObject("Monthly Market Preview Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market,
                    CoreGameplayPreviewPort.WhiteShell, 0);
                Text[] labels = host.GetComponentsInChildren<Text>(true);
                foreach (string name in new[] { "Market Footer", "Market Title", "Market Summary",
                    "Market Storage Mode", "Market Open Investment" })
                    Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name),
                        Is.False, name + " was removed from the market scene.");
                var refresh = typeof(CoreGameplayDemoController).GetMethod("RefreshMarketFacilityContents",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                foreach (int fame in new[] { 0, 1000 })
                {
                    controller.Session.Cargo.Fame = fame;
                    refresh.Invoke(controller, null);
                    foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    {
                        int lot = System.Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
                        decimal cost = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item,
                            controller.Session.GameDateTime, lot, fame);
                        Assert.That(labels.Single(label => label.name == "Market Buy Price " + PortMarketCatalog.Key(item)).text,
                            Is.EqualTo(PortMarketCatalog.FormatPrice(cost) + " 金"), item + " / fame " + fame);
                    }
                }
                Assert.That(labels.Any(label => label.name == "Market Storage Mode Hint"), Is.False);
                Assert.That(labels.Any(label => label.name.StartsWith("Market Column Header ")),
                    Is.False, "The icon catalog has no legacy ledger columns.");
                Assert.That(host.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Market Item Detail").gameObject.activeSelf,
                    Is.False, "Stock and quota belong to the unopened item detail.");
                Assert.That(string.Join("\n", labels.Select(label => label.text)),
                    Does.Not.Contain("每日").And.Not.Contain("本机时间").And.Not.Contain("本地时间"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void MarketRemaining_UsesThisMonthPurchasesWithoutLegacyOrPriorMonth()
        {
            var date = new System.DateTime(2026, 9, 23);
            const string port = "白贝港";
            const PortMarketItem item = PortMarketItem.Water;
            string todayKey = CoreGameplayDemoController.MarketPurchaseKey(port, date, item);
            string yesterdayKey = CoreGameplayDemoController.MarketPurchaseKey(port, date.AddMonths(-1), item);
            string legacyKey = "ShatteredSail.PortMarketPurchase.v1.白贝港.2026-09.water";
            string[] keys = { todayKey, yesterdayKey, legacyKey };
            bool[] existed = keys.Select(PlayerPrefs.HasKey).ToArray();
            int[] values = keys.Select(key => PlayerPrefs.GetInt(key, 0)).ToArray();
            GameObject host = new GameObject("Monthly Market Ledger Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                var method = typeof(CoreGameplayDemoController).GetMethod("MarketRemaining",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                PlayerPrefs.SetInt(legacyKey, 1000);
                PlayerPrefs.SetInt(yesterdayKey, 1000);
                PlayerPrefs.DeleteKey(todayKey);
                Assert.That(method.Invoke(controller, new object[] { port, item, date, 0 }), Is.EqualTo(PortMarketCatalog.DayState(port, item, date).Stock));
                PlayerPrefs.SetInt(todayKey, 300);
                Assert.That(method.Invoke(controller, new object[] { port, item, date, 0 }), Is.EqualTo(PortMarketCatalog.DayState(port, item, date).Stock - 300));
                Assert.That(method.Invoke(controller, new object[] { port, item, date.AddHours(23), 0 }), Is.EqualTo(PortMarketCatalog.DayState(port, item, date).Stock - 300));
                Assert.That(method.Invoke(controller, new object[] { port, item, date, 1 }), Is.EqualTo(PortMarketCatalog.DayState(port, item, date, 1).Stock - 300));
                Assert.That(PlayerPrefs.GetInt(legacyKey), Is.EqualTo(1000));
                Assert.That(PlayerPrefs.GetInt(yesterdayKey), Is.EqualTo(1000));
            }
            finally
            {
                for (int index = 0; index < keys.Length; index++)
                {
                    if (existed[index]) PlayerPrefs.SetInt(keys[index], values[index]);
                    else PlayerPrefs.DeleteKey(keys[index]);
                }
                PlayerPrefs.Save();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EditorPreview_MarketBuildsWithDedicatedBackgroundAndPanel()
        {
            GameObject host = new GameObject("Market Preview Test Host");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(
                    CoreGameplayPreviewScreen.Market,
                    CoreGameplayPreviewPort.WhiteShell,
                    0);

                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                Assert.That(preview, Is.Not.Null);
                Transform panel = preview.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => item.name == "Market Panel");
                Assert.That(panel, Is.Not.Null);

                Transform background = preview.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => item.name == "Market Background");
                Assert.That(background, Is.Not.Null);
                RawImage image = background.GetComponent<RawImage>();
                Assert.That(image, Is.Not.Null);
                Assert.That(image.texture, Is.Not.Null);
                Assert.That(image.texture.width, Is.GreaterThanOrEqualTo(1024));
                Assert.That(image.texture.height, Is.GreaterThanOrEqualTo(512));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EditorPreview_MarketDarkInkLabelsDoNotUseOffsetShadow()
        {
            GameObject host = new GameObject("Market Typography Preview Test Host");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market,
                    CoreGameplayPreviewPort.WhiteShell, 0);
                Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                string[] names = new[] { "Market Shop Title", "Market Detail Buy Price", "Market Detail Sell Price" }
                    .Concat(PortMarketCatalog.AllItems.SelectMany(item => new[]
                    {
                        "Market Item Name " + PortMarketCatalog.Key(item),
                        "Market Buy Price " + PortMarketCatalog.Key(item),
                        "Market Buy Unit " + PortMarketCatalog.Key(item),
                    })).ToArray();
                foreach (string name in names)
                {
                    Text label = preview.GetComponentsInChildren<Text>(true)
                        .FirstOrDefault(item => item.name == name);
                    Assert.That(label, Is.Not.Null, name);
                    Outline outline = label.GetComponent<Outline>();
                    Assert.That(outline == null || !outline.enabled, Is.True,
                        name + " should not duplicate dark glyphs on parchment");
                    Assert.That(label.GetComponents<Shadow>().Count(shadow => shadow.GetType() == typeof(Shadow) && shadow.enabled),
                        Is.EqualTo(0), name + " must not carry a second offset shadow");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
        [Test]
        public void MarketStepPriceLabels_MatchActualTransactionQuotes()
        {
            GameObject host = new GameObject("Market Price Preview Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell, 0);
                Text[] labels = host.GetComponentsInChildren<Text>(true);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    int buyStep = PortMarketCatalog.PurchaseStep(item);
                    int sellStep = PortMarketCatalog.SaleStep(item);
                    int quoteQuantity = System.Math.Max(buyStep, sellStep);
                    decimal cost = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item,
                        controller.Session.GameDateTime, quoteQuantity, controller.Session.Cargo.Fame);
                    Text purchase = labels.Single(label => label.name == "Market Buy Price " + key);
                    Text unit = labels.Single(label => label.name == "Market Buy Unit " + key);
                    Assert.That(purchase.text, Is.EqualTo(PortMarketCatalog.FormatPrice(cost) + " 金"), key + " / exact buy lot price");
                    Assert.That(unit.text, Is.EqualTo("每" + quoteQuantity + "单位"), key);
                    Assert.That(purchase.gameObject.activeInHierarchy, Is.False, key + " / catalog prices are folded into the selected item's detail");
                    Assert.That(purchase.font, Is.Not.Null, key);
                    Assert.That(unit.fontSize, Is.EqualTo(18), key + " / secondary unit information");
                    Assert.That(unit.font, Is.SameAs(GameTypography.BodyFont), key);
                    Assert.That(labels.Any(label => label.name == "Market Buy Title " + key ||
                        label.name == "Market Sell Title " + key || label.name == "Market Sell Price " + key), Is.False,
                        key + " / catalog has only one purchase quote");
                    foreach (Text label in new[] { purchase, unit })
                    {
                        Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), key);
                        Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter), key);
                        Assert.That(label.text, Does.Not.Contain("\n"), key);
                        Assert.That(label.raycastTarget, Is.False, key);
                        // Retained row quotes stay hidden; the selected drawer's
                        // visible quote glyphs are verified by ParchmentFrameLayoutTests.
                        if (!label.gameObject.activeInHierarchy) continue;
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), key);
                        Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f), key);
                        TextGenerationSettings visibleSettings = label.GetGenerationSettings(label.rectTransform.rect.size);
                        TextGenerationSettings completeSettings = visibleSettings;
                        completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
                        using (var visible = new TextGenerator())
                        using (var complete = new TextGenerator())
                        {
                            visible.Populate(label.text, visibleSettings);
                            complete.Populate(label.text, completeSettings);
                            Assert.That(visible.vertexCount, Is.GreaterThan(4), key);
                            Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), key + " / no clipped total");
                            Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), key + " / no clipped total glyphs");
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void MarketScene_FourShopsExposeAllNineGoodsInsideOppositeDrawers()
        {
            GameObject host = new GameObject("Market Layout Preview Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell, 0);
                Canvas.ForceUpdateCanvases();
                RectTransform board = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Board");
                RectTransform detail = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Item Detail");
                Assert.That(detail.gameObject.activeInHierarchy, Is.False, "A complete market is visible on entry.");
                Assert.That(board.GetComponent<Image>() == null || !board.GetComponent<Image>().raycastTarget, Is.True,
                    "The scene board itself does not consume pointer input.");
                var open = typeof(CoreGameplayDemoController).GetMethod("OpenMarketShop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var goods = new System.Collections.Generic.HashSet<PortMarketItem>();
                foreach (MarketShopKind shop in System.Enum.GetValues(typeof(MarketShopKind)))
                {
                    Assert.That(host.GetComponentsInChildren<Button>(true).Count(button => button.name == "Market Shop " + shop), Is.EqualTo(1));
                    open.Invoke(controller, new object[] {shop});
                    Canvas.ForceUpdateCanvases();
                    Assert.That(detail.gameObject.activeInHierarchy, Is.True);
                    Assert.That(detail.anchoredPosition.x > 0, Is.EqualTo(shop == MarketShopKind.Supplies || shop == MarketShopKind.Medicine));
                    foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    {
                        string key = PortMarketCatalog.Key(item);
                        RectTransform row = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Row " + key);
                        bool belongs = MarketSceneLayout.ItemsForShop(shop).Contains(item);
                        Assert.That(row.gameObject.activeInHierarchy, Is.EqualTo(belongs), key + " only appears in its own shop");
                        if (!belongs) continue;
                        Assert.That(goods.Add(item), Is.True, key + " occurs in exactly one shop");
                        RawImage icon = row.GetComponentsInChildren<RawImage>(true).Single();
                        Assert.That(icon.texture, Is.Not.Null);
                        Assert.That(icon.GetComponent<Button>().targetGraphic, Is.SameAs(icon));
                        Rect bounds = BoundsIn(detail,row);
                        Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(detail.rect.xMin));
                        Assert.That(bounds.xMax, Is.LessThanOrEqualTo(detail.rect.xMax));
                    }
                    RectTransform delivery = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Detail Storage Mode");
                    Assert.That(delivery.gameObject.activeInHierarchy, Is.True, "Delivery selection belongs inside the open shop.");
                    Rect deliveryBounds = BoundsIn(detail, delivery);
                    Assert.That(deliveryBounds.xMin, Is.GreaterThanOrEqualTo(detail.rect.xMin));
                    Assert.That(deliveryBounds.xMax, Is.LessThanOrEqualTo(detail.rect.xMax));
                    Assert.That(deliveryBounds.yMin, Is.GreaterThanOrEqualTo(detail.rect.yMin));
                    Assert.That(deliveryBounds.yMax, Is.LessThanOrEqualTo(detail.rect.yMax));
                }
                CollectionAssert.AreEquivalent(PortMarketCatalog.AllItems,goods);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test]
        public void Market_ReturnToPortThenOpenInvestmentWithoutSpendingOrChangingQuota()
        {
            GameObject host = new GameObject("Market Return To Port Investment Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell, 0);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                decimal gold = controller.Session.ProfileGold;
                int water = controller.Session.Cargo.Water;
                string key = CoreGameplayDemoController.MarketPurchaseKey(controller.Session.CurrentPort,
                    controller.Session.GameDateTime, PortMarketItem.Water);
                int quota = PlayerPrefs.GetInt(key, 0);
                Button back = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Return To Port Hub");
                Assert.That(back.gameObject.activeInHierarchy, Is.True);
                back.onClick.Invoke();
                Button investment = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Port Hotspot Maintenance");
                Assert.That(investment.gameObject.activeInHierarchy, Is.True, "Investment remains available from the port's main entrance.");
                investment.onClick.Invoke();
                Assert.That(typeof(CoreGameplayDemoController).GetField("activePortFacility", flags).GetValue(controller),
                    Is.EqualTo(PortFacilityArtCatalog.MaintenanceFacilityIndex));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                Assert.That(PlayerPrefs.GetInt(key, 0), Is.EqualTo(quota));
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Port Investment Panel"), Is.True);
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                local.Max(point => point.x), local.Max(point => point.y));
        }
    }
}
