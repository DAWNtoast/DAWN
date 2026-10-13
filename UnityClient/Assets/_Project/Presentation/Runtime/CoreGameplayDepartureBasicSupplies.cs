using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureBasicSupplyOverlay, departureBasicSupplySetup;
        private CoreVoyageSession departureBasicSupplySession;
        private string departureBasicSupplyPort;
        private int departureBasicSupplyShip;
        private object departureBasicSupplyIdentity;
        private DateTime departureBasicSupplyDisplayedDate;
        private Button departureBasicSupplyPurchase;
        private Text departureBasicSupplyHint;
        private readonly Dictionary<PortMarketItem, int> departureBasicSupplyQuantities = new Dictionary<PortMarketItem, int>();
        private readonly Dictionary<PortMarketItem, Text> departureBasicSupplyHeld = new Dictionary<PortMarketItem, Text>();
        private readonly Dictionary<PortMarketItem, Text> departureBasicSupplyQuantityLabels = new Dictionary<PortMarketItem, Text>();
        private readonly Dictionary<PortMarketItem, Text> departureBasicSupplyEndurance = new Dictionary<PortMarketItem, Text>();
        private readonly Dictionary<PortMarketItem, Button> departureBasicSupplyDecrease = new Dictionary<PortMarketItem, Button>();
        private readonly Dictionary<PortMarketItem, Button> departureBasicSupplyIncrease = new Dictionary<PortMarketItem, Button>();

        private void OpenDepartureBasicSupplies(RectTransform setupPanel)
        {
            if (!IsDepartureShipView() || departureCrewConfigurationOpen || setupPanel == null ||
                !setupPanel.gameObject.activeInHierarchy || contentRoot == null || tutorialActive) return;
            CloseDepartureBasicSupplies();
            departureBasicSupplyIdentity = new object();
            object identity = departureBasicSupplyIdentity;
            departureBasicSupplySetup = setupPanel;
            departureBasicSupplySession = session;
            departureBasicSupplyPort = session.CurrentPort;
            departureBasicSupplyShip = session.SelectedShipIndex;
            departureBasicSupplyDisplayedDate = MarketMonth(MarketNow);
            departureBasicSupplyOverlay = CreateFixedPanel("Departure Basic Supplies Overlay", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(0f, 0f, 0f, .76f));
            Image shade = departureBasicSupplyOverlay.GetComponent<Image>();
            shade.sprite = shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(0f, 0f, 0f, .76f);
            shade.raycastTarget = true;
            departureBasicSupplyOverlay.SetAsLastSibling();
            RectTransform card = CreateFixedPanel("Departure Basic Supplies", departureBasicSupplyOverlay,
                new Vector2(1080f, 660f), Vector2.zero, PanelRaised);
            NauticalSurfaceSkin.ApplyReadingPanel(card.GetComponent<Image>());
            Color ink = Html("#352415");
            CreateText("Departure Basic Supplies Title", card, new Vector2(940f, 42f),
                new Vector2(0f, 264f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ink).text = "购买基础物资";
            for (int index = 0; index < PortMarketCatalog.AllItems.Count; index++)
            {
                PortMarketItem item = PortMarketCatalog.AllItems[index];
                string key = PortMarketCatalog.Key(item);
                departureBasicSupplyQuantities[item] = 0;
                RectTransform row = CreateRect("Departure Basic Supply Row " + key, card,
                    new Vector2(316f, 144f), new Vector2(-336f + index % 3 * 336f, 165f - index / 3 * 150f));
                CreateItemIcon("Departure Basic Supply Icon " + key, row,
                    ItemIconCatalog.ForCargo(key), new Vector2(-107f, 13f), 72f);
                CreateText("Departure Basic Supply Name " + key, row, new Vector2(196f, 28f),
                    new Vector2(28f, 43f), 22, FontStyle.Bold, TextAnchor.MiddleLeft, ink).text = PortMarketCatalog.Name(item);
                departureBasicSupplyHeld[item] = BasicSupplyText("Departure Basic Supply Held " + key, row,
                    new Vector2(196f, 26f), new Vector2(28f, 13f), 18, ink);
                departureBasicSupplyQuantityLabels[item] = BasicSupplyText("Departure Basic Supply Quantity " + key, row,
                    new Vector2(68f, 38f), new Vector2(30f, -25f), 22, ink, TextAnchor.MiddleCenter);
                Button decrease = CreateButton("Departure Basic Supply Decrease " + key, row,
                    new Vector2(38f, 38f), new Vector2(-34f, -25f), Gold, DeepSea, 22);
                decrease.GetComponentInChildren<Text>().text = "−";
                decrease.onClick.AddListener(() => AdjustDepartureBasicSupply(identity, item, -1));
                departureBasicSupplyDecrease[item] = decrease;
                Button increase = CreateButton("Departure Basic Supply Increase " + key, row,
                    new Vector2(38f, 38f), new Vector2(94f, -25f), Gold, DeepSea, 22);
                increase.GetComponentInChildren<Text>().text = "+";
                increase.onClick.AddListener(() => AdjustDepartureBasicSupply(identity, item, 1));
                departureBasicSupplyIncrease[item] = increase;
                if (item == PortMarketItem.Water || item == PortMarketItem.Food)
                    departureBasicSupplyEndurance[item] = BasicSupplyText("Departure Basic Supply Endurance " + key, row,
                        new Vector2(288f, 26f), new Vector2(2f, -61f), 16, ink, TextAnchor.MiddleCenter);
            }
            departureBasicSupplyHint = BasicSupplyText("Departure Basic Supplies Hint", card,
                new Vector2(960f, 26f), new Vector2(0f, -202f), 18, ink, TextAnchor.MiddleCenter);
            Button recommend = CreateButton("Departure Basic Supplies Recommend", card,
                new Vector2(280f, 54f), new Vector2(-336f, -250f), Gold, DeepSea, 20);
            recommend.GetComponentInChildren<Text>().text = "设定为推荐数量";
            recommend.interactable = Application.isPlaying && !editorPreviewBuilding;
            recommend.onClick.AddListener(() => RecommendDepartureBasicSupplies(identity));
            Button close = CreateButton("Departure Basic Supplies Close", card,
                new Vector2(200f, 54f), new Vector2(0f, -250f), Gold, DeepSea, 20);
            close.GetComponentInChildren<Text>().text = "返回出航";
            close.onClick.AddListener(() => { if (IsDepartureBasicSupplyViewLive(identity)) CloseDepartureBasicSupplies(); });
            departureBasicSupplyPurchase = CreateButton("Departure Basic Supplies Purchase", card,
                new Vector2(280f, 54f), new Vector2(336f, -250f), Gold, DeepSea, 20);
            departureBasicSupplyPurchase.onClick.AddListener(() => PurchaseDepartureBasicSupplies(identity));
            RefreshDepartureBasicSupplies(string.Empty);
        }

        private Text BasicSupplyText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize,
            Color ink, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, ink);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(12, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private bool IsDepartureBasicSupplyViewLive(object identity) => identity != null &&
            ReferenceEquals(identity, departureBasicSupplyIdentity) && departureBasicSupplyOverlay != null &&
            departureBasicSupplyOverlay.gameObject.activeInHierarchy && departureBasicSupplySetup != null &&
            departureBasicSupplySetup.gameObject.activeInHierarchy && ReferenceEquals(session, departureBasicSupplySession) &&
            IsDepartureShipView() && !departureCrewConfigurationOpen && session.CurrentPort == departureBasicSupplyPort &&
            session.SelectedShipIndex == departureBasicSupplyShip;

        private bool CanEditDepartureBasicSupplies(object identity) => Application.isPlaying && !editorPreviewBuilding &&
            !tutorialActive && IsDepartureBasicSupplyViewLive(identity);

        private int DepartureBasicSupplyLimit(PortMarketItem item, DateTime now) => Math.Max(0, Math.Min(
            int.MaxValue - Math.Max(0, session.GetMarketCargoAmount(PortMarketCatalog.Key(item))),
            MarketRemaining(session.CurrentPort, item, now, PortInvestmentLevel(session.CurrentPort))));

        private void AdjustDepartureBasicSupply(object identity, PortMarketItem item, int delta)
        {
            if (!CanEditDepartureBasicSupplies(identity)) return;
            int current = departureBasicSupplyQuantities[item];
            departureBasicSupplyQuantities[item] = delta < 0 ? Math.Max(0, current - 1) :
                (int)Math.Min((long)current + 1, DepartureBasicSupplyLimit(item, MarketNow));
            RefreshDepartureBasicSupplies(string.Empty);
        }

        private void RecommendDepartureBasicSupplies(object identity)
        {
            if (!CanEditDepartureBasicSupplies(identity)) return;
            EnsureDepartureCrewSelection();
            int days = session.SelectedTreasureMap != null ? session.SelectedTreasureMap.Rank.RecommendedVoyageDays :
                CoreVoyageSession.Routes[session.SelectedRouteIndex].RecommendedVoyageDays;
            double daily = DepartureBasicSupplyDailyConsumption();
            int rationTarget = (int)Math.Min(int.MaxValue, Math.Ceiling(daily * (days + 2) - 1e-9d));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                int target = item == PortMarketItem.Water || item == PortMarketItem.Food ? rationTarget :
                    item == PortMarketItem.Medicine ? 2 : item == PortMarketItem.Wood ? 8 : item == PortMarketItem.Powder ? 12 :
                    item == PortMarketItem.Cannonballs ? 10 : 0;
                departureBasicSupplyQuantities[item] = ProvisionShortfall(target, session.GetMarketCargoAmount(PortMarketCatalog.Key(item)));
            }
            RefreshDepartureBasicSupplies("已设定推荐购买数量，可继续调整。");
        }

        private double DepartureBasicSupplyDailyConsumption() => session.EstimateDailyRationConsumption(
            selectedDepartureOrdinaryCrew + selectedDepartureSpecialCrew.Count, HasSelectedDepartureProfession(4));

        private decimal QuoteDepartureBasicSupplies(DateTime now, out string reason)
        {
            reason = string.Empty;
            decimal cost = 0m;
            double volume = session.Cargo.UsedVolume;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                int amount = departureBasicSupplyQuantities[item];
                cost += PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, amount, session.Cargo.Fame);
                volume += amount * PortMarketCatalog.CargoVolume(item);
                if (amount < 0 || amount > DepartureBasicSupplyLimit(item, now))
                    reason = "本月" + PortMarketCatalog.Name(item) + "库存或购买额度不足，请调整数量。";
            }
            if (!CoreEconomyBalance.HasNonNegativeCargo(session.Cargo)) reason = "货物数量异常，请先检查船舱。";
            else if (reason.Length == 0 && cost > session.ProfileGold)
                reason = "金币不足，还缺 " + PortMarketCatalog.FormatPrice(cost - session.ProfileGold) + " 金币。";
            else if (reason.Length == 0 && (double.IsNaN(volume) || double.IsInfinity(volume) ||
                volume > session.CargoCapacityValue + .001d)) reason = "船舱空间不足，请减少购买数量或前往市场卸货。";
            return cost;
        }

        private void RefreshDepartureBasicSupplies(string notice)
        {
            if (!IsDepartureBasicSupplyViewLive(departureBasicSupplyIdentity)) return;
            EnsureDepartureCrewSelection();
            DateTime now = MarketNow;
            double daily = DepartureBasicSupplyDailyConsumption();
            bool editable = CanEditDepartureBasicSupplies(departureBasicSupplyIdentity);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                int held = session.GetMarketCargoAmount(PortMarketCatalog.Key(item));
                int quantity = departureBasicSupplyQuantities[item];
                departureBasicSupplyHeld[item].text = "船舱中 " + held;
                departureBasicSupplyQuantityLabels[item].text = quantity.ToString();
                departureBasicSupplyDecrease[item].interactable = editable && quantity > 0;
                departureBasicSupplyIncrease[item].interactable = editable && quantity < DepartureBasicSupplyLimit(item, now);
                if (departureBasicSupplyEndurance.TryGetValue(item, out Text endurance))
                    endurance.text = daily > 0 ? "可供 " + (held / daily).ToString("0.#") + " 天　购后 " +
                        (((long)held + quantity) / daily).ToString("0.#") + " 天" : "请先配置出航船员";
            }
            decimal cost = QuoteDepartureBasicSupplies(now, out string reason);
            departureBasicSupplyPurchase.GetComponentInChildren<Text>().text = "购买 · " + PortMarketCatalog.FormatPrice(cost) + " 金";
            departureBasicSupplyPurchase.interactable = editable && cost > 0m && reason.Length == 0;
            departureBasicSupplyHint.text = reason.Length > 0 ? reason : notice;
        }

        private bool RefreshDepartureBasicSupplyMonth()
        {
            if (!IsDepartureBasicSupplyViewLive(departureBasicSupplyIdentity)) return false;
            departureBasicSupplyDisplayedDate = MarketMonth(MarketNow);
            RefreshDepartureBasicSupplies("新月报价已刷新，请确认金额后购买。");
            return true;
        }

        private void PurchaseDepartureBasicSupplies(object identity)
        {
            if (!CanEditDepartureBasicSupplies(identity)) return;
            DateTime now = MarketNow;
            if (departureBasicSupplyDisplayedDate != MarketMonth(now))
            {
                marketLastObservedMonth = MarketMonth(now);
                RefreshDepartureBasicSupplyMonth();
                return;
            }
            decimal cost = QuoteDepartureBasicSupplies(now, out string reason);
            if (cost <= 0m || reason.Length > 0)
            {
                RefreshDepartureBasicSupplies(reason);
                return;
            }
            var order = new Dictionary<string, int>();
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) order[PortMarketCatalog.Key(item)] = departureBasicSupplyQuantities[item];
            if (!session.TryPurchaseMarketBasicSupplies(order, cost))
            {
                RefreshDepartureBasicSupplies("采购未完成，请检查金币和船舱；未扣款。");
                return;
            }
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                int quantity = departureBasicSupplyQuantities[item];
                if (quantity > 0) PlayerPrefs.SetInt(MarketPurchaseKey(session.CurrentPort, now, item),
                    MarketPurchased(session.CurrentPort, item, now) + quantity);
                departureBasicSupplyQuantities[item] = 0;
            }
            SaveProgress();
            portNotice = "基础物资已装入船舱，花费 " + PortMarketCatalog.FormatPrice(cost) + " 金币。";
            UpdatePortHeaderReadouts();
            RefreshDepartureBasicSupplies(portNotice);
        }

        private void CloseDepartureBasicSupplies()
        {
            RectTransform overlay = departureBasicSupplyOverlay;
            ReleaseDepartureBasicSupplyView();
            if (overlay == null) return;
            overlay.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(overlay.gameObject);
            else DestroyImmediate(overlay.gameObject);
        }

        private void ReleaseDepartureBasicSupplyView()
        {
            departureBasicSupplyIdentity = null;
            departureBasicSupplyOverlay = departureBasicSupplySetup = null;
            departureBasicSupplySession = null;
            departureBasicSupplyPurchase = null;
            departureBasicSupplyHint = null;
            departureBasicSupplyQuantities.Clear();
            departureBasicSupplyHeld.Clear();
            departureBasicSupplyQuantityLabels.Clear();
            departureBasicSupplyEndurance.Clear();
            departureBasicSupplyDecrease.Clear();
            departureBasicSupplyIncrease.Clear();
        }
    }
}
