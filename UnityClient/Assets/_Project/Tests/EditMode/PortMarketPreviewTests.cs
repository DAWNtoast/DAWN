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
        public void MarketPreview_ShowsGameMonthRefreshAndNoRealTimeCopy()
        {
            GameObject host = new GameObject("Monthly Market Preview Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market,
                    CoreGameplayPreviewPort.WhiteShell, 0);
                Text[] labels = host.GetComponentsInChildren<Text>(true);
                Assert.That(labels.Single(label => label.name == "Market Summary").text,
                    Does.Contain("下次补货 " + PortMarketCatalog.NextRefreshLabel(controller.Session.GameDateTime)));
                Assert.That(labels.Single(label => label.name == "Market Column Header Quota").text,
                    Is.EqualTo("本月余量 / 额度"));
                Assert.That(labels.Single(label => label.name == "Market Investment Notice").text,
                    Does.Contain("每月首日"));
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
                foreach (string name in new[] { "Market Title", "Market Summary", "Market Column Header Item", "Market Column Header Buy", "Market Column Header Sell", "Market Column Header Quota", "Market Column Header Quantity", "Market Column Header Trade" })
                {
                    Text label = preview.GetComponentsInChildren<Text>(true)
                        .FirstOrDefault(item => item.name == name);
                    Assert.That(label, Is.Not.Null, name);
                    Outline outline = label.GetComponent<Outline>();
                    Assert.That(outline, Is.Not.Null, name);
                    Assert.That(outline.enabled, Is.False, name + " should not duplicate dark glyphs on parchment");
                    Assert.That(label.GetComponents<Shadow>().Count(shadow => shadow.GetType() == typeof(Shadow) && shadow.enabled),
                        Is.EqualTo(0), name + " must not carry a second offset shadow");
                }
                Text itemName = preview.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(item => item.name == "Market Item Name water");
                Assert.That(itemName, Is.Not.Null);
                Outline itemOutline = itemName.GetComponent<Outline>();
                Assert.That(itemOutline, Is.Not.Null);
                Assert.That(itemOutline.enabled, Is.True);
                Assert.That(itemOutline.effectDistance.magnitude, Is.LessThanOrEqualTo(1.1f));
                Assert.That(itemOutline.effectColor.a, Is.LessThanOrEqualTo(0.35f));
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
                    ShowMarketPreviewItem(host, key);
                    labels = host.GetComponentsInChildren<Text>(true);
                    int buyStep = PortMarketCatalog.PurchaseStep(item);
                    int sellStep = PortMarketCatalog.SaleStep(item);
                    int quoteQuantity = System.Math.Max(buyStep, sellStep);
                    decimal cost = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item, controller.Session.GameDateTime, quoteQuantity);
                    decimal revenue = PortMarketCatalog.CalculateSellRevenue(controller.Session.CurrentPort, item, controller.Session.GameDateTime, sellStep);
                    Assert.That(labels.Single(label => label.name == "Market Buy Price " + key).text,
                        Is.EqualTo(quoteQuantity > 1 ? "每" + quoteQuantity + "单位 " + PortMarketCatalog.FormatPrice(cost) + " 金" : PortMarketCatalog.FormatPrice(cost) + " 金 / 个"), key);
                    Assert.That(labels.Single(label => label.name == "Market Sell Price " + key).text,
                        Is.EqualTo(sellStep > 1 ? "每" + sellStep + "单位 " + PortMarketCatalog.FormatPrice(revenue) + " 金" : PortMarketCatalog.FormatPrice(revenue) + " 金 / 个"), key);
                    Assert.That(host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Buy " + key)
                        .GetComponentInChildren<Text>().text, Is.EqualTo("<size=22>购入 " + buyStep + "</size>\n<size=22>−" +
                            PortMarketCatalog.FormatPrice(PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item, controller.Session.GameDateTime, buyStep)) + " 金</size>"), key);
                    Button purchase = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Buy " + key);
                    Button sale = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Sell " + key);
                    Assert.That(purchase.GetComponent<WoodButtonRoleState>().Role, Is.EqualTo(WoodButtonRole.Primary), key);
                    Assert.That(sale.GetComponent<WoodButtonRoleState>().Role, Is.EqualTo(WoodButtonRole.Secondary), key);
                    foreach (Button trade in new[] { purchase, sale })
                    {
                        RectTransform rect = trade.GetComponent<RectTransform>();
                        RectTransform row = (RectTransform)trade.transform.parent;
                        Assert.That(rect.anchoredPosition.y + rect.rect.yMin, Is.GreaterThanOrEqualTo(row.rect.yMin), key);
                        Assert.That(rect.anchoredPosition.y + rect.rect.yMax, Is.LessThanOrEqualTo(row.rect.yMax), key);
                        Text label = trade.GetComponentInChildren<Text>();
                        Assert.That(label.text.Count(character => character == '\n'), Is.EqualTo(1), key);
                        Assert.That(label.raycastTarget, Is.False, key);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), key);
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
        public void MarketHeaderAndRows_HaveSeparateAlignedLedgerBands()
        {
            GameObject host = new GameObject("Market Layout Preview Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell, 0);
                Canvas.ForceUpdateCanvases();
                Text[] labels = host.GetComponentsInChildren<Text>(true);
                RectTransform board = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Board");
                var corners = new Vector3[4];
                float previousBottom = float.PositiveInfinity;
                foreach (string name in new[] { "Market Title", "Market Summary", "Market Column Header Item", "Market Item Name water" })
                {
                    Text label = labels.Single(text => text.name == name);
                    label.rectTransform.GetWorldCorners(corners);
                    float top = board.InverseTransformPoint(corners[1]).y;
                    float bottom = board.InverseTransformPoint(corners[0]).y;
                    Assert.That(top, Is.LessThan(previousBottom), name + " overlaps the prior ledger band");
                    Assert.That(top, Is.LessThanOrEqualTo(board.rect.yMax), name);
                    previousBottom = bottom;
                }
                foreach (var column in new[] { new[] { "Item", "Market Item Name water" }, new[] { "Buy", "Market Buy Price water" },
                    new[] { "Sell", "Market Sell Price water" }, new[] { "Quota", "Market Quota water" }, new[] { "Quantity", "Market Quantity water" } })
                    Assert.That(board.InverseTransformPoint(labels.Single(label => label.name == "Market Column Header " + column[0]).rectTransform.position).x,
                        Is.EqualTo(board.InverseTransformPoint(labels.Single(label => label.name == column[1]).rectTransform.position).x), column[0]);
                foreach (string side in new[] { "Buy", "Sell" })
                {
                    Text heading = labels.Single(label => label.name == "Market Column Header " + side);
                    Assert.That(heading.alignment, Is.EqualTo(TextAnchor.MiddleRight), side);
                    float headingRight = board.InverseTransformPoint(heading.rectTransform.TransformPoint(
                        new Vector3(heading.rectTransform.rect.xMax, 0f, 0f))).x;
                    foreach (Text price in labels.Where(label => label.name.StartsWith("Market " + side + " Price ", System.StringComparison.Ordinal)))
                    {
                        Assert.That(price.alignment, Is.EqualTo(TextAnchor.MiddleRight), price.name);
                        float priceRight = board.InverseTransformPoint(price.rectTransform.TransformPoint(
                            new Vector3(price.rectTransform.rect.xMax, 0f, 0f))).x;
                        Assert.That(priceRight, Is.EqualTo(headingRight).Within(.1f), price.name + " / aligned number column");
                    }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void MarketInvestmentShortcut_OpensInvestmentWithoutSpendingOrChangingQuota()
        {
            GameObject host = new GameObject("Market Investment Shortcut Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell, 0);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var order = (ShatteredSail.Core.Gameplay.VoyageSupplyOrder)typeof(CoreGameplayDemoController)
                    .GetField("pendingSupplyOrder", flags).GetValue(controller);
                order[ShatteredSail.Core.Gameplay.VoyageSupplyType.Water] = 100;
                decimal gold = controller.Session.ProfileGold;
                int water = controller.Session.Cargo.Water;
                string key = CoreGameplayDemoController.MarketPurchaseKey(controller.Session.CurrentPort,
                    controller.Session.GameDateTime, PortMarketItem.Water);
                int quota = PlayerPrefs.GetInt(key, 0);
                Button shortcut = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Open Investment");
                RectTransform board = (RectTransform)shortcut.transform.parent;
                RectTransform buttonRect = (RectTransform)shortcut.transform;
                RectTransform finalRow = host.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect.name.StartsWith("Market Row ", System.StringComparison.Ordinal))
                    .OrderBy(rect => rect.anchoredPosition.y).First();
                Assert.That(buttonRect.anchoredPosition.y + buttonRect.rect.yMax,
                    Is.LessThan(finalRow.anchoredPosition.y + finalRow.rect.yMin));
                Assert.That(buttonRect.anchoredPosition.y + buttonRect.rect.yMin, Is.GreaterThan(board.rect.yMin));
                shortcut.onClick.Invoke();
                Assert.That(typeof(CoreGameplayDemoController).GetField("activePortFacility", flags).GetValue(controller), Is.EqualTo(7));
                Assert.That(order.TotalUnits, Is.Zero);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                Assert.That(PlayerPrefs.GetInt(key, 0), Is.EqualTo(quota));
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Port Investment Panel"), Is.True);
                Assert.That((string)typeof(CoreGameplayDemoController).GetField("portNotice", flags).GetValue(controller), Does.Contain("增加每月库存和额度"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void ShowMarketPreviewItem(GameObject host, string key)
        {
            for (int page = 0; !host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Market Row " + key) && page < 9; page++)
            {
                Button next = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Next Page");
                Assert.That(next.interactable, Is.True, key);
                next.onClick.Invoke();
            }
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Market Row " + key), Is.True, key);
        }

        [TestCase(0, 4)]
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void MarketReadability_EveryItemFitsItsPageAtTheSelectedFontSize(int level, int rowsPerPage)
        {
            int? previous = GameReadability.OverrideLevel;
            GameObject host = new GameObject("Market Reading Size Test");
            try
            {
                GameReadability.OverrideLevel = level;
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.WhiteShell, 0);
                int pages = (PortMarketCatalog.AllItems.Count + rowsPerPage - 1) / rowsPerPage;
                var observed = new System.Collections.Generic.List<string>();
                var roleTypography = new System.Collections.Generic.Dictionary<string, string>();
                for (int page = 0; page < pages; page++)
                {
                    Canvas.ForceUpdateCanvases();
                    Text title = host.GetComponentsInChildren<Text>(true).Single(label => label.name == "Market Title");
                    Assert.That(title.fontSize, Is.EqualTo(GameReadability.FontSize(32)));
                    Assert.That(title.resizeTextForBestFit, Is.False);
                    AssertMarketTextComplete(title);
                    RectTransform board = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Board");
                    RectTransform[] rows = board.GetComponentsInChildren<RectTransform>(true)
                        .Where(rect => rect.name.StartsWith("Market Row ", System.StringComparison.Ordinal))
                        .OrderByDescending(rect => rect.anchoredPosition.y).ToArray();
                    Assert.That(rows.Length, Is.EqualTo(System.Math.Min(rowsPerPage, PortMarketCatalog.AllItems.Count - page * rowsPerPage)));
                    Rect footer = MarketBounds(host.GetComponentsInChildren<Text>(true).Single(label => label.name == "Market Page Label").rectTransform, board);
                    Rect column = MarketBounds(host.GetComponentsInChildren<Text>(true).Single(label => label.name == "Market Column Header Item").rectTransform, board);
                    for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                    {
                        RectTransform row = rows[rowIndex];
                        Rect bounds = MarketBounds(row, board);
                        Assert.That(bounds.yMax, Is.LessThan(column.yMin), row.name);
                        Assert.That(bounds.yMin, Is.GreaterThan(footer.yMax), row.name);
                        if (rowIndex > 0) Assert.That(bounds.yMax, Is.LessThan(MarketBounds(rows[rowIndex - 1], board).yMin - 8f));
                        string key = row.name.Substring("Market Row ".Length);
                        observed.Add(key);
                        foreach (Text label in row.GetComponentsInChildren<Text>(true).Where(text => text.gameObject.activeInHierarchy))
                        {
                            Assert.That(label.font, Is.SameAs(GameTypography.BodyFont), label.name);
                            Assert.That(label.resizeTextForBestFit, Is.False, label.name);
                            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)), label.name);
                            Assert.That(label.resizeTextMinSize, Is.EqualTo(label.fontSize), label.name);
                            Assert.That(label.resizeTextMaxSize, Is.EqualTo(label.fontSize), label.name);
                            string role = (label.name == "Label" ? label.transform.parent.name : label.name).Replace(" " + key, "");
                            string typography = label.font.GetInstanceID() + ":" + label.fontSize + ":" +
                                label.resizeTextMinSize + ":" + label.resizeTextMaxSize + ":" + label.resizeTextForBestFit;
                            if (roleTypography.TryGetValue(role, out string firstPageTypography))
                                Assert.That(typography, Is.EqualTo(firstPageTypography), role + " must retain the same typography across every item/page, including the final page.");
                            else roleTypography.Add(role, typography);
                            Rect labelBounds = MarketBounds(label.rectTransform, row);
                            Assert.That(labelBounds.xMin, Is.GreaterThanOrEqualTo(row.rect.xMin), label.name);
                            Assert.That(labelBounds.xMax, Is.LessThanOrEqualTo(row.rect.xMax), label.name);
                            Assert.That(labelBounds.yMin, Is.GreaterThanOrEqualTo(row.rect.yMin), label.name);
                            Assert.That(labelBounds.yMax, Is.LessThanOrEqualTo(row.rect.yMax), label.name);
                            AssertMarketTextComplete(label);
                        }
                        var controls = row.GetComponentsInChildren<Selectable>(true);
                        for (int i = 0; i < controls.Length; i++)
                            for (int j = i + 1; j < controls.Length; j++)
                                Assert.That(MarketBounds((RectTransform)controls[i].transform, row).Overlaps(
                                    MarketBounds((RectTransform)controls[j].transform, row)), Is.False, controls[i].name + " / " + controls[j].name);
                    }
                    foreach (string name in new[] { "Market Trade Summary", "Market Summary", "Market Storage Mode Hint", "Market Page Label", "Market Investment Notice" })
                        AssertMarketTextComplete(host.GetComponentsInChildren<Text>(true).Single(label => label.name == name));
                    Rect titleBounds = MarketBounds(title.rectTransform, board);
                    foreach (string name in new[] { "Market Trade Summary", "Market Summary", "Market Storage Mode Hint" })
                        Assert.That(MarketBounds(host.GetComponentsInChildren<Text>(true).Single(label => label.name == name).rectTransform,
                            board).Overlaps(titleBounds), Is.False, name + " overlaps title");
                    foreach (Text header in board.GetComponentsInChildren<Text>(true).Where(label => label.name.StartsWith("Market Column Header ")))
                        AssertMarketTextComplete(header);
                    foreach (string name in new[] { "Market Previous Page", "Market Next Page", "Market Open Investment", "Market Storage Mode" })
                    {
                        Text label = host.GetComponentsInChildren<Button>(true).Single(button => button.name == name).GetComponentInChildren<Text>();
                        Assert.That(label.font, Is.SameAs(GameTypography.BodyFont), name);
                        Assert.That(label.fontSize, Is.EqualTo(GameReadability.FontSize(22)), name);
                        AssertMarketTextComplete(label);
                    }
                    Button next = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Next Page");
                    Button back = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Market Previous Page");
                    Assert.That(next.interactable, Is.EqualTo(page + 1 < pages));
                    Assert.That(back.interactable, Is.EqualTo(page > 0));
                    Assert.That(host.GetComponentsInChildren<Text>(true).Single(label => label.name == "Market Page Label").text,
                        Is.EqualTo("第 " + (page + 1) + " / " + pages + " 页"));
                    if (page + 1 < pages) next.onClick.Invoke();
                }
                Assert.That(observed, Is.EqualTo(PortMarketCatalog.AllItems.Select(PortMarketCatalog.Key).ToArray()));
            }
            finally { Object.DestroyImmediate(host); GameReadability.OverrideLevel = previous; }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MarketPurchaseChoice_AllReadingSizesKeepQuoteAndDestinationsComplete(int level)
        {
            int? previous = GameReadability.OverrideLevel;
            var host = new GameObject("Market Purchase Choice Reading Size Test");
            try
            {
                GameReadability.OverrideLevel = level;
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.GrayGull, 0);
                decimal gold = controller.Session.ProfileGold;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var quotes = (System.Collections.IDictionary)typeof(CoreGameplayDemoController).GetField("marketPurchaseQuotes", flags).GetValue(controller);
                object quote = quotes[PortMarketItem.Water];
                typeof(CoreGameplayDemoController).GetMethod("BuildMarketPurchaseChoice", flags).Invoke(controller, new[] { quote });
                Canvas.ForceUpdateCanvases();
                RectTransform dialog = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Market Purchase Choice");
                Text[] labels = dialog.GetComponentsInChildren<Text>(true);
                foreach (Text label in labels)
                {
                    Assert.That(label.resizeTextForBestFit, Is.False, label.name);
                    bool title = label.name == "Market Purchase Choice Title";
                    Assert.That(label.fontSize, Is.EqualTo(GameReadability.FontSize(title ? 32 :
                        label.name.EndsWith("Status", System.StringComparison.Ordinal) ? 18 : 22)), label.name);
                    if (!title) Assert.That(label.font, Is.SameAs(GameTypography.BodyFont), label.name);
                    AssertMarketTextComplete(label);
                    Rect bounds = MarketBounds(label.rectTransform, dialog);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(dialog.rect.xMin), label.name);
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(dialog.rect.xMax), label.name);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(dialog.rect.yMin), label.name);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(dialog.rect.yMax), label.name);
                }
                for (int i = 0; i < labels.Length; i++)
                    for (int j = i + 1; j < labels.Length; j++)
                        Assert.That(MarketBounds(labels[i].rectTransform, dialog).Overlaps(MarketBounds(labels[j].rectTransform, dialog)),
                            Is.False, labels[i].name + " overlaps " + labels[j].name);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Read-only purchase-choice preview cannot charge the account.");
                Assert.That(labels.Single(label => label.name == "Market Purchase Choice Quote").text, Does.Contain("取消不扣款"));
            }
            finally { Object.DestroyImmediate(host); GameReadability.OverrideLevel = previous; }
        }

        private static Rect MarketBounds(RectTransform rect, Transform relative)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 min = relative.InverseTransformPoint(corners[0]);
            Vector3 max = relative.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertMarketTextComplete(Text label)
        {
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), label.name);
            TextGenerationSettings visibleSettings = label.GetGenerationSettings(label.rectTransform.rect.size);
            TextGenerationSettings fullSettings = visibleSettings;
            fullSettings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var visible = new TextGenerator())
            using (var full = new TextGenerator())
            {
                visible.Populate(label.text, visibleSettings); full.Populate(label.text, fullSettings);
                // Quantity fields and +/- controls intentionally contain a
                // single glyph. Four vertices is a complete rendered glyph;
                // completeness is checked by matching the full generator
                // character/vertex counts below.
                Assert.That(visible.vertexCount, Is.GreaterThanOrEqualTo(4), label.name);
                Assert.That(visible.characterCount, Is.EqualTo(full.characterCount), label.name);
                Assert.That(visible.vertexCount, Is.EqualTo(full.vertexCount), label.name);
            }
        }

    }
}
