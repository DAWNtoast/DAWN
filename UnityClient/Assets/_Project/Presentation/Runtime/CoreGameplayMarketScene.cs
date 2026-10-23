using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private MarketShopKind? marketSelectedShop;
        private readonly Dictionary<MarketShopKind, Button> marketShopButtons = new Dictionary<MarketShopKind, Button>();
        private readonly Dictionary<MarketShopKind, Text> marketShopLabels = new Dictionary<MarketShopKind, Text>();
        private Button marketSceneEmptySpace, marketCloseShop;
        private Text marketShopTitle, marketShopHint;
        private Image marketSceneNoticeBackground;
        private CanvasGroup marketChoiceHeaderGroup;
        private bool marketChoiceHeaderInteractable, marketChoiceHeaderRaycasts;

        private void ResetMarketSceneView()
        {
            RestoreMarketNavigationAfterChoice();
            marketSelectedShop = null;
            marketShopButtons.Clear();
            marketShopLabels.Clear();
            marketSceneEmptySpace = marketCloseShop = null;
            marketShopTitle = marketShopHint = null;
            marketSceneNoticeBackground = null;
        }

        private void BuildMarketSceneShops(RectTransform board)
        {
            RectTransform ground = CreateRect("Market Scene Empty Space", board,
                PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition);
            Image hit = ground.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            marketSceneEmptySpace = ground.gameObject.AddComponent<Button>();
            marketSceneEmptySpace.targetGraphic = hit;
            marketSceneEmptySpace.transition = Selectable.Transition.None;
            Texture2D signArt = Resources.Load<Texture2D>("PortFacilities/Market/market_shop_sign_v2");
            Texture2D backdrop = facilityArt == null ? null : facilityArt.Background(PortFacilityArtCatalog.MarketFacilityIndex);
            Vector2 sourceSize = backdrop == null ? new Vector2(1920f, 1080f) : new Vector2(backdrop.width, backdrop.height);
            foreach (MarketShopKind shop in Enum.GetValues(typeof(MarketShopKind)))
            {
                Vector2 position = MarketSceneLayout.HotspotPosition(shop, sourceSize);
                RectTransform root = CreateRect("Market Shop " + shop, board, MarketSceneLayout.HotspotSize(shop), position);
                Image surface = root.gameObject.AddComponent<Image>();
                surface.color = Color.clear;
                surface.raycastTarget = true;
                Button button = root.gameObject.AddComponent<Button>();
                bool front = shop == MarketShopKind.Supplies || shop == MarketShopKind.Ordnance;
                Vector2 signSize = front ? new Vector2(354f, 354f / 2.917f) : new Vector2(280f, 280f / 2.917f);
                Vector2 signPosition = MarketSceneLayout.SignPosition(shop, sourceSize) - position;
                RectTransform sign = CreateRect("Market Shop Sign " + shop, root, signSize, signPosition);
                Graphic face;
                if (signArt != null)
                {
                    RawImage art = CreateRawImage("Painted Shop Sign", sign, signSize, Vector2.zero, signArt, new Rect(0, 0, 1, 1));
                    art.raycastTarget = false;
                    face = art;
                }
                else
                {
                    Image fallback = CreateImage("Shop Sign Wood", sign, signSize, Vector2.zero, Html("#342518"));
                    NauticalSurfaceSkin.ApplyPanel(fallback, Html("#342518"));
                    fallback.raycastTarget = false;
                    face = fallback;
                }
                button.targetGraphic = face;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, .94f, .70f);
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(.84f, .73f, .51f);
                colors.disabledColor = Color.white;
                colors.fadeDuration = .12f;
                button.colors = colors;
                RawImage emblem = CreateItemIcon("Shop Emblem", sign,
                    ItemIconCatalog.ForCargo(PortMarketCatalog.Key(MarketSceneLayout.ItemsForShop(shop)[0])),
                    new Vector2(-signSize.x * .328f, -signSize.y * .10f), front ? 66f : 50f);
                emblem.raycastTarget = false;
                Text caption = CreateText("Market Shop Label " + shop, sign,
                    new Vector2(signSize.x * .62f, 56f), new Vector2(signSize.x * .10f, -signSize.y * .10f),
                    front ? 31 : 26, FontStyle.Bold, TextAnchor.MiddleCenter, Html("#F4DCA6"));
                caption.text = MarketSceneLayout.ShopName(shop);
                caption.raycastTarget = false;
                marketShopButtons[shop] = button;
                marketShopLabels[shop] = caption;
            }
            marketSceneNoticeBackground = CreateImage("Market Trade Notice Backing", board,
                new Vector2(688f, 68f), new Vector2(0f, -424f), new Color(.10f, .067f, .035f, .82f));
            marketSceneNoticeBackground.raycastTarget = false;
            marketShopHint = CreateText("Market Scene Hint", board, new Vector2(660f, 56f),
                new Vector2(0f, -424f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Html("#F4E4C2"));
            marketShopHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            marketShopHint.verticalOverflow = VerticalWrapMode.Truncate;
            marketShopHint.resizeTextForBestFit = true;
            marketShopHint.resizeTextMinSize = 16;
            marketShopHint.resizeTextMaxSize = 18;
            marketShopHint.text = string.Empty;
            marketShopHint.raycastTarget = false;
            Outline hintShadow = marketShopHint.gameObject.AddComponent<Outline>();
            hintShadow.effectColor = new Color(.1f, .07f, .04f, .9f);
            hintShadow.effectDistance = new Vector2(1f, -1f);
        }

        private void RefreshMarketSceneShops()
        {
            marketSceneEmptySpace.onClick = new Button.ButtonClickedEvent();
            AddMarketViewAction(marketSceneEmptySpace, CloseMarketShop);
            foreach (var pair in marketShopButtons)
            {
                MarketShopKind shop = pair.Key;
                pair.Value.onClick = new Button.ButtonClickedEvent();
                AddMarketViewAction(pair.Value, () => OpenMarketShop(shop));
                bool selected = marketSelectedShop == shop;
                marketShopLabels[shop].color = selected ? Html("#FFF2B9") : Html("#F4DCA6");
                ColorBlock colors = pair.Value.colors;
                colors.normalColor = selected ? new Color(1f, .93f, .67f) : Color.white;
                pair.Value.colors = colors;
            }
            string feedback = MarketSceneTradeFeedback();
            marketShopHint.text = feedback;
            marketShopHint.gameObject.SetActive(feedback.Length > 0);
            marketSceneNoticeBackground.gameObject.SetActive(feedback.Length > 0);
        }

        private string MarketSceneTradeFeedback()
        {
            if (string.IsNullOrEmpty(portNotice)) return string.Empty;
            bool trade = portNotice.StartsWith("已购入", StringComparison.Ordinal) ||
                portNotice.StartsWith("已在", StringComparison.Ordinal) && portNotice.Contains("出售") ||
                portNotice.StartsWith("市场采购失败", StringComparison.Ordinal) ||
                portNotice.StartsWith("市场出售失败", StringComparison.Ordinal) ||
                portNotice.StartsWith("采购报价", StringComparison.Ordinal) ||
                portNotice.StartsWith("采购未完成", StringComparison.Ordinal) ||
                portNotice.StartsWith("出售报价", StringComparison.Ordinal) ||
                portNotice.StartsWith("本批货物", StringComparison.Ordinal) ||
                portNotice.StartsWith("已取消购买", StringComparison.Ordinal) ||
                portNotice.StartsWith("新月行情", StringComparison.Ordinal) ||
                portNotice.StartsWith("新游戏月", StringComparison.Ordinal);
            return trade ? portNotice : string.Empty;
        }
        private void OpenMarketShop(MarketShopKind shop)
        {
            if (session == null || session.Phase != CoreDemoPhase.Port || pendingMarketPurchase != null ||
                activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex ||
                marketPurchaseView == null || !marketPurchaseView.gameObject.activeInHierarchy ||
                !Enum.IsDefined(typeof(MarketShopKind), shop) || marketSelectedShop == shop) return;
            marketSelectedShop = shop;
            marketSelectedItem = MarketSceneLayout.ItemsForShop(shop)[0];
            marketTradeSelecting = marketTradeBuying = true;
            marketInventoryExpanded = false;
            marketQuantities[marketSelectedItem.Value] = PortMarketCatalog.PurchaseStep(marketSelectedItem.Value);
            RefreshMarketItemDetailPage();
        }

        private void CloseMarketShop()
        {
            if (pendingMarketPurchase != null || !marketSelectedShop.HasValue) return;
            marketSelectedShop = null;
            marketSelectedItem = null;
            marketTradeSelecting = marketTradeBuying = false;
            marketInventoryExpanded = false;
            RefreshMarketItemDetailPage();
        }

        private void CancelPendingMarketPurchase()
        {
            if (pendingMarketPurchase == null) return;
            pendingMarketPurchase.Consumed = true;
            pendingMarketPurchase = null;
            portNotice = "已取消购买，金币、物品与本月额度均未扣除。";
            Render();
        }

        private void LockMarketNavigationForChoice()
        {
            Transform header = contentRoot.Find("Port Facility Header");
            if (header == null) return;
            marketChoiceHeaderGroup = header.GetComponent<CanvasGroup>();
            if (marketChoiceHeaderGroup == null) marketChoiceHeaderGroup = header.gameObject.AddComponent<CanvasGroup>();
            marketChoiceHeaderInteractable = marketChoiceHeaderGroup.interactable;
            marketChoiceHeaderRaycasts = marketChoiceHeaderGroup.blocksRaycasts;
            marketChoiceHeaderGroup.interactable = false;
            marketChoiceHeaderGroup.blocksRaycasts = false;
        }

        private void RestoreMarketNavigationAfterChoice()
        {
            if (marketChoiceHeaderGroup == null) return;
            marketChoiceHeaderGroup.interactable = marketChoiceHeaderInteractable;
            marketChoiceHeaderGroup.blocksRaycasts = marketChoiceHeaderRaycasts;
            marketChoiceHeaderGroup = null;
        }
    }
}


