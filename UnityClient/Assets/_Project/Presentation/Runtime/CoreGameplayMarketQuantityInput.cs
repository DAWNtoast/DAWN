using System;
using System.Collections.Generic;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly Dictionary<PortMarketItem, InputField> marketQuantityInputs =
            new Dictionary<PortMarketItem, InputField>();

        private readonly Dictionary<PortMarketItem, string> marketQuantityDrafts =
            new Dictionary<PortMarketItem, string>();

        private static string MarketTradeButtonText(bool buying, int quantity, decimal total)
        {
            // Keep the direction and lot size separate from the amount charged or received.
            return "<size=" + GameReadability.FontSize(22) + ">" + (buying ? "购入 " : "出售 ") + quantity + "</size>\n<size=" + GameReadability.FontSize(22) + ">" +
                (buying ? "−" : "+") + PortMarketCatalog.FormatPrice(total) + " 金</size>";
        }

        private InputField CreateMarketQuantityInput(RectTransform row, PortMarketItem item,
            int quantity, int limit, Color ink)
        {
            string key = PortMarketCatalog.Key(item);
            RectTransform root = CreateRect("Market Quantity Input " + key, row,
                new Vector2(196f, 56f * GameReadability.Scale), new Vector2(306f, 0f));
            Image surface = root.gameObject.AddComponent<Image>();
            surface.color = new Color(.015f, .035f, .045f, .92f);
            Text amount = CreateText("Market Quantity " + key, root, new Vector2(188f, 52f * GameReadability.Scale),
                Vector2.zero, 17, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            SetMarketBodyFont(amount, 22);
            amount.raycastTarget = false;
            amount.horizontalOverflow = HorizontalWrapMode.Overflow;
            amount.verticalOverflow = VerticalWrapMode.Truncate;
            Text placeholder = CreateText("Market Quantity Placeholder " + key, root, new Vector2(188f, 52f * GameReadability.Scale),
                Vector2.zero, 15, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
            SetMarketBodyFont(placeholder, 18);
            placeholder.text = "输入数量";
            placeholder.raycastTarget = false;
            InputField input = root.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            input.textComponent = amount;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 10;
            input.caretColor = ink;
            input.customCaretColor = true;
            input.selectionColor = new Color(.78f, .61f, .29f, .6f);
            input.SetTextWithoutNotify(marketQuantityDrafts.TryGetValue(item, out string draft)
                ? draft : quantity.ToString(CultureInfo.InvariantCulture));
            input.interactable = Application.isPlaying && !editorPreviewBuilding && limit > 0;
            marketQuantityInputs[item] = input;
            return input;
        }

        private void BindMarketQuantityInput(InputField input, PortMarketItem item, Button buy, Button sell,
            DateTime date, int level, int purchased, int remaining)
        {
            RectTransform view = marketPurchaseView;
            CoreVoyageSession owner = session;
            int revision = marketPurchaseRevision;
            string port = session.CurrentPort;
            int cargo = session.GetMarketCargoAmount(PortMarketCatalog.Key(item));
            int limit = Math.Max(remaining, cargo);
            bool Current() => Application.isPlaying && !editorPreviewBuilding && input != null &&
                input.gameObject.activeInHierarchy && view != null && view.gameObject.activeInHierarchy &&
                marketPurchaseView == view && marketPurchaseRevision == revision && ReferenceEquals(session, owner) &&
                session.Phase == CoreDemoPhase.Port && session.CurrentPort == port &&
                activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex && MarketMonth(MarketNow) == MarketMonth(date) &&
                pendingMarketPurchase == null;

            void Refresh(string text)
            {
                if (!Current()) return;
                marketQuantityDrafts[item] = text;
                if (marketPurchaseQuotes.TryGetValue(item, out MarketPurchaseQuote previous)) previous.Consumed = true;
                // Replace the event object as well as its listeners: an already queued
                // click keeps the old quote and cannot purchase a newly typed quantity.
                buy.onClick = new Button.ButtonClickedEvent();
                sell.onClick = new Button.ButtonClickedEvent();
                bool valid = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) &&
                    quantity > 0 && quantity <= limit;
                if (!valid)
                {
                    buy.interactable = false;
                    sell.interactable = false;
                    buy.GetComponentInChildren<Text>().text = limit > 0 ? "数量范围 1–" + limit : "本月已售罄";
                    sell.GetComponentInChildren<Text>().text = "请输入有效数量";
                    return;
                }
                marketQuantities[item] = quantity;
                decimal cost = PortMarketCatalog.CalculateBuyCost(port, item, date, quantity);
                MarketPurchaseQuote quote = CreateMarketPurchaseQuote(item, quantity, cost, date, level, purchased, remaining);
                buy.GetComponentInChildren<Text>().text = MarketTradeButtonText(true, quantity, cost);
                buy.interactable = quantity <= remaining && cost > 0m && session.ProfileGold >= cost && MarketPurchaseFitsMode(quote);
                buy.onClick.AddListener(() => BuyQuotedMarketItem(quote));
                int saleQuantity = MarketSaleQuantity(item);
                decimal revenue = PortMarketCatalog.CalculateSellRevenue(port, item, date, saleQuantity);
                sell.GetComponentInChildren<Text>().text = MarketTradeButtonText(false, saleQuantity, revenue);
                sell.interactable = saleQuantity > 0 && revenue > 0m && session.GetMarketCargoAmount(PortMarketCatalog.Key(item)) >= saleQuantity;
                AddMarketViewAction(sell, () =>
                {
                    SellQuotedMarketItem(quote, saleQuantity, revenue, cargo);
                });
            }
            input.onValueChanged.AddListener(Refresh);
            if (marketQuantityDrafts.ContainsKey(item)) Refresh(input.text);
            input.onEndEdit.AddListener(text =>
            {
                if (!Current()) return;
                long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long requested);
                // Empty, zero and negative input stays non-purchasable. In
                // particular, losing focus on a buy click must never turn 0
                // into an unintended purchase of one item.
                int quantity = limit <= 0 ? 0 : (int)Math.Max(0L, Math.Min(limit, requested));
                string normalized = quantity.ToString(CultureInfo.InvariantCulture);
                input.SetTextWithoutNotify(normalized);
                Refresh(normalized);
            });
        }
    }
}
