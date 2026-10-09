using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private int marketPage;
        private string marketPagePort;

        private static int MarketRowsPerPage => GameReadability.Scale >= 1.49f ? 2 :
            GameReadability.Scale >= 1.24f ? 3 : 4;
        private int MarketPageCount => Math.Max(1,
            (PortMarketCatalog.AllItems.Count + MarketRowsPerPage - 1) / MarketRowsPerPage);

        private void PrepareMarketPage()
        {
            if (!string.Equals(marketPagePort, session.CurrentPort, StringComparison.Ordinal))
            {
                marketPagePort = session.CurrentPort;
                marketPage = 0;
            }
            marketPage = Mathf.Clamp(marketPage, 0, MarketPageCount - 1);
        }

        private static void SetMarketBodyFont(Text text, int standardSize)
        {
            text.font = GameTypography.BodyFont;
            text.fontSize = GameReadability.FontSize(standardSize);
            text.fontStyle = FontStyle.Normal;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = text.resizeTextMaxSize = text.fontSize;
            text.lineSpacing = 1f;
        }

        private void BuildMarketPagination(RectTransform board, Color ink)
        {
            Text page = CreateText("Market Page Label", board, new Vector2(240f, 52f),
                new Vector2(260f, -309f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
            SetMarketBodyFont(page, 18);
            page.text = "第 " + (marketPage + 1) + " / " + MarketPageCount + " 页";
            Button previous = CreateButton("Market Previous Page", board, new Vector2(120f, 58f),
                new Vector2(70f, -309f), PanelRaised, Ink, 22);
            Button next = CreateButton("Market Next Page", board, new Vector2(120f, 58f),
                new Vector2(450f, -309f), PanelRaised, Ink, 22);
            previous.GetComponentInChildren<Text>().text = "上一页";
            next.GetComponentInChildren<Text>().text = "下一页";
            SetMarketBodyFont(previous.GetComponentInChildren<Text>(), 22);
            SetMarketBodyFont(next.GetComponentInChildren<Text>(), 22);
            previous.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(116f, 54f);
            next.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(116f, 54f);
            previous.interactable = marketPage > 0;
            next.interactable = marketPage + 1 < MarketPageCount;
            AddMarketViewAction(previous, () => ChangeMarketPage(-1));
            AddMarketViewAction(next, () => ChangeMarketPage(1));
        }

        private void ChangeMarketPage(int direction)
        {
            int next = Mathf.Clamp(marketPage + direction, 0, MarketPageCount - 1);
            if (next == marketPage) return;
            marketPage = next;
            // Render retires the view revision and its quotes. Quantities, purchase
            // destination, account balances and the monthly ledger belong to the owner.
            Render();
        }
    }
}
