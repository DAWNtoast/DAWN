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
        private readonly Dictionary<PortMarketItem, InputField> departureBasicSupplyInputs = new Dictionary<PortMarketItem, InputField>();
        private readonly Dictionary<PortMarketItem, Text> departureBasicSupplyPrices = new Dictionary<PortMarketItem, Text>();
        private readonly Dictionary<PortMarketItem, Text> departureBasicSupplySubtotals = new Dictionary<PortMarketItem, Text>();
        private readonly HashSet<PortMarketItem> departureBasicSupplyInvalidInputs = new HashSet<PortMarketItem>();
        private readonly Dictionary<PortMarketItem, Button> departureBasicSupplyDecrease = new Dictionary<PortMarketItem, Button>();
        private readonly Dictionary<PortMarketItem, Button> departureBasicSupplyIncrease = new Dictionary<PortMarketItem, Button>();
        private readonly Dictionary<PortMarketItem, Button> departureBasicSupplyDecreaseTen = new Dictionary<PortMarketItem, Button>();
        private readonly Dictionary<PortMarketItem, Button> departureBasicSupplyIncreaseTen = new Dictionary<PortMarketItem, Button>();

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
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(.025f, .018f, .012f, .68f));
            Image shade = departureBasicSupplyOverlay.GetComponent<Image>();
            shade.sprite = shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(.025f, .018f, .012f, .68f);
            shade.raycastTarget = true;
            departureBasicSupplyOverlay.SetAsLastSibling();
            RectTransform card = CreateFixedPanel("Departure Basic Supplies", departureBasicSupplyOverlay,
                new Vector2(1080f, 800f), Vector2.zero, Html("#F0E6CF"));
            MarketShopSkin.ApplyPanel(card.GetComponent<Image>());
            Color ink = MarketShopSkin.Ink, muted = MarketShopSkin.MutedInk;
            BasicSupplyText("Departure Basic Supplies Eyebrow", card, new Vector2(940f, 20f),
                new Vector2(0f, 368f), 15, muted, TextAnchor.MiddleCenter).text = "BASIC SUPPLIES";
            CreateText("Departure Basic Supplies Title", card, new Vector2(940f, 42f),
                new Vector2(0f, 334f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, ink).text = "购买基础物资";
            BasicSupplyRule("Departure Basic Supplies Header Rule", card, new Vector2(976f, 1f), new Vector2(0f, 298f));
            for (int column = 0; column < 2; column++)
                BasicSupplyRule("Departure Basic Supplies Column Rule " + column, card,
                    new Vector2(1f, 568f), new Vector2(-168f + column * 336f, 10f));
            for (int index = 0; index < PortMarketCatalog.AllItems.Count; index++)
            {
                PortMarketItem item = PortMarketCatalog.AllItems[index];
                string key = PortMarketCatalog.Key(item);
                departureBasicSupplyQuantities[item] = 0;
                RectTransform row = CreateRect("Departure Basic Supply Row " + key, card,
                    new Vector2(316f, 192f), new Vector2(-336f + index % 3 * 336f, 210f - index / 3 * 200f));
                CreateItemIcon("Departure Basic Supply Icon " + key, row,
                    ItemIconCatalog.ForCargo(key), new Vector2(-107f, 42f), 72f);
                CreateText("Departure Basic Supply Name " + key, row, new Vector2(196f, 28f),
                    new Vector2(28f, 64f), 22, FontStyle.Bold, TextAnchor.MiddleLeft, ink).text = PortMarketCatalog.Name(item);
                departureBasicSupplyHeld[item] = BasicSupplyText("Departure Basic Supply Held " + key, row,
                    new Vector2(196f, 24f), new Vector2(28f, 35f), 18, muted);
                departureBasicSupplyPrices[item] = BasicSupplyText("Departure Basic Supply Unit Price " + key, row,
                    new Vector2(196f, 24f), new Vector2(28f, 9f), 17, muted);
                BuildDepartureBasicSupplyQuantityInput(row, item, key, identity);
                departureBasicSupplyDecreaseTen[item] = BuildDepartureBasicSupplyStep(row, item, key, identity, -10, -132f);
                departureBasicSupplyDecrease[item] = BuildDepartureBasicSupplyStep(row, item, key, identity, -1, -78f);
                departureBasicSupplyIncrease[item] = BuildDepartureBasicSupplyStep(row, item, key, identity, 1, 78f);
                departureBasicSupplyIncreaseTen[item] = BuildDepartureBasicSupplyStep(row, item, key, identity, 10, 132f);
                departureBasicSupplySubtotals[item] = BasicSupplyText("Departure Basic Supply Subtotal " + key, row,
                    new Vector2(288f, 26f), new Vector2(0f, -64f), 18, ink, TextAnchor.MiddleCenter);
                if (item == PortMarketItem.Water || item == PortMarketItem.Food)
                    departureBasicSupplyEndurance[item] = BasicSupplyText("Departure Basic Supply Endurance " + key, row,
                        new Vector2(300f, 20f), new Vector2(0f, -88f), 16, muted, TextAnchor.MiddleCenter);
                if (index < 6)
                    BasicSupplyRule("Departure Basic Supply Row Rule " + key, row,
                        new Vector2(292f, 1f), new Vector2(0f, -100f));
            }
            departureBasicSupplyHint = BasicSupplyText("Departure Basic Supplies Hint", card,
                new Vector2(960f, 28f), new Vector2(0f, -303f), 17, muted, TextAnchor.MiddleCenter);
            BasicSupplyRule("Departure Basic Supplies Footer Rule", card, new Vector2(976f, 1f), new Vector2(0f, -328f));
            Button recommend = CreateButton("Departure Basic Supplies Recommend", card,
                new Vector2(280f, 44f), new Vector2(-336f, -365f), Gold, DeepSea, 20);
            recommend.GetComponentInChildren<Text>().text = "设定为推荐数量";
            MarketShopSkin.ApplyLightButton(recommend);
            GameTypography.Apply(recommend.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            recommend.interactable = Application.isPlaying && !editorPreviewBuilding;
            recommend.onClick.AddListener(() => RecommendDepartureBasicSupplies(identity));
            Button close = CreateButton("Departure Basic Supplies Close", card,
                new Vector2(200f, 44f), new Vector2(0f, -365f), Gold, DeepSea, 20);
            close.GetComponentInChildren<Text>().text = "返回出航";
            MarketShopSkin.ApplyTextButton(close);
            GameTypography.Apply(close.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            close.onClick.AddListener(() => { if (IsDepartureBasicSupplyViewLive(identity)) CloseDepartureBasicSupplies(); });
            departureBasicSupplyPurchase = CreateButton("Departure Basic Supplies Purchase", card,
                new Vector2(280f, 44f), new Vector2(336f, -365f), Gold, DeepSea, 20);
            MarketShopSkin.SetMode(departureBasicSupplyPurchase, true);
            GameTypography.Apply(departureBasicSupplyPurchase.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            departureBasicSupplyPurchase.onClick.AddListener(() => PurchaseDepartureBasicSupplies(identity));
            RefreshDepartureBasicSupplies(string.Empty);
        }

        private void BasicSupplyRule(string name, Transform parent, Vector2 size, Vector2 position)
        {
            Image rule = CreateImage(name, parent, size, position, new Color(.60f, .47f, .30f, .32f));
            rule.raycastTarget = false;
        }

        private Text BasicSupplyText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize,
            Color ink, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, ink);
            text.color = ink;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(12, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private Button BuildDepartureBasicSupplyStep(Transform parent, PortMarketItem item, string key,
            object identity, int delta, float x)
        {
            bool batch = Math.Abs(delta) == 10;
            string action = delta < 0 ? "Decrease" : "Increase";
            Button button = CreateButton("Departure Basic Supply " + action + (batch ? " Ten " : " ") + key,
                parent, new Vector2(batch ? 48f : 38f, 38f), new Vector2(x, -30f), Gold, DeepSea, 20);
            MarketShopSkin.ApplyLightButton(button);
            Text label = button.GetComponentInChildren<Text>();
            label.text = delta < 0 ? batch ? "−10" : "−" : batch ? "+10" : "+";
            label.rectTransform.sizeDelta = new Vector2(batch ? 42f : 32f, 30f);
            GameTypography.Apply(label, batch ? 18 : 22, FontStyle.Normal);
            button.onClick.AddListener(() => AdjustDepartureBasicSupply(identity, item, delta));
            return button;
        }

        private void BuildDepartureBasicSupplyQuantityInput(Transform parent, PortMarketItem item, string key, object identity)
        {
            RectTransform field = CreateRect("Departure Basic Supply Quantity Input " + key, parent,
                new Vector2(100f, 38f), new Vector2(0f, -30f));
            Image background = field.gameObject.AddComponent<Image>();
            background.color = new Color(.98f, .95f, .88f, 1f);
            Outline border = field.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.60f, .47f, .30f, .7f);
            border.effectDistance = new Vector2(1f, -1f);
            InputField input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            Text quantity = BasicSupplyText("Departure Basic Supply Quantity " + key, field,
                new Vector2(88f, 32f), Vector2.zero, 22, MarketShopSkin.Ink, TextAnchor.MiddleCenter);
            quantity.supportRichText = false;
            quantity.horizontalOverflow = HorizontalWrapMode.Overflow;
            input.textComponent = quantity;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 10;
            input.SetTextWithoutNotify("0");
            departureBasicSupplyInputs[item] = input;
            departureBasicSupplyQuantityLabels[item] = quantity;
            // Both typing and ending an edit update only the basket. Invalid
            // input remains visible and blocks buying; losing focus must never
            // silently replace it with an old valid quantity and buy that draft.
            input.onValueChanged.AddListener(value => EditDepartureBasicSupplyQuantity(identity, item, value));
            input.onEndEdit.AddListener(value => EditDepartureBasicSupplyQuantity(identity, item, value));
        }

        private void EditDepartureBasicSupplyQuantity(object identity, PortMarketItem item, string value)
        {
            if (!CanEditDepartureBasicSupplies(identity)) return;
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long requested) || requested < 0)
            {
                departureBasicSupplyInvalidInputs.Add(item);
                RefreshDepartureBasicSupplies(string.Empty);
                return;
            }
            departureBasicSupplyInvalidInputs.Remove(item);
            int maximum = DepartureBasicSupplyLimit(item, MarketNow);
            departureBasicSupplyQuantities[item] = (int)Math.Min(requested, maximum);
            RefreshDepartureBasicSupplies(requested > maximum
                ? "本月" + PortMarketCatalog.Name(item) + "最多可购 " + maximum + "，已调整数量。" : string.Empty);
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
            departureBasicSupplyQuantities[item] = (int)Math.Max(0L,
                Math.Min((long)current + delta, DepartureBasicSupplyLimit(item, MarketNow)));
            departureBasicSupplyInvalidInputs.Remove(item);
            departureBasicSupplyInputs[item].SetTextWithoutNotify(departureBasicSupplyQuantities[item].ToString(CultureInfo.InvariantCulture));
            RefreshDepartureBasicSupplies(string.Empty);
        }

        private void RecommendDepartureBasicSupplies(object identity)
        {
            if (!CanEditDepartureBasicSupplies(identity)) return;
            EnsureDepartureCrewSelection();
            int crew = selectedDepartureOrdinaryCrew + selectedDepartureSpecialCrew.Count;
            bool cook = HasSelectedDepartureProfession(4);
            VoyageSupplyOrder recommended = session.CreateRecommendedSupplyOrder(crew, cook);
            var available = new VoyageSupplyOrder();
            DateTime now = MarketNow;
            departureBasicSupplyInvalidInputs.Clear();
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                departureBasicSupplyQuantities[item] = 0;
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
            {
                PortMarketItem item = MaintenanceMarketItem(type);
                available[type] = Math.Min(recommended[type], DepartureBasicSupplyLimit(item, now));
                departureBasicSupplyQuantities[item] = available[type];
            }
            RefreshDepartureBasicSupplies(HasRecommendedSupplyShortfall(available, crew, cook)
                ? "舱容或本月库存不足，未达到完整推荐储备；请检查续航与弹药。"
                : "已按航程与战斗储备设定推荐数量，可继续调整。");
        }

        private bool HasRecommendedSupplyShortfall(VoyageSupplyOrder order, int crew, bool cook)
        {
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                if ((long)session.GetSupplyAmount(type) + order[type] < session.RecommendedSupplyTarget(type, crew, cook)) return true;
            return false;
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
                if (departureBasicSupplyInvalidInputs.Contains(item))
                    reason = "请输入" + PortMarketCatalog.Name(item) + "的非负整数数量。";
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
                bool invalid = departureBasicSupplyInvalidInputs.Contains(item);
                departureBasicSupplyHeld[item].text = "船舱中 " + held;
                InputField input = departureBasicSupplyInputs[item];
                if (!invalid) input.SetTextWithoutNotify(quantity.ToString(CultureInfo.InvariantCulture));
                input.interactable = editable;
                departureBasicSupplyQuantityLabels[item].color = invalid ? new Color(.55f, .23f, .16f, 1f) : MarketShopSkin.Ink;
                departureBasicSupplyDecrease[item].interactable = departureBasicSupplyDecreaseTen[item].interactable = editable && (quantity > 0 || invalid);
                departureBasicSupplyIncrease[item].interactable = departureBasicSupplyIncreaseTen[item].interactable =
                    editable && (quantity < DepartureBasicSupplyLimit(item, now) || invalid);
                departureBasicSupplyPrices[item].text = "单价 " + PortMarketCatalog.FormatPrice(
                    PortMarketCatalog.BuyUnitPrice(session.CurrentPort, item, now, session.Cargo.Fame)) + " 金";
                departureBasicSupplySubtotals[item].text = invalid ? "小计 —" : "小计 " + PortMarketCatalog.FormatPrice(
                    PortMarketCatalog.CalculateBuyCost(session.CurrentPort, item, now, quantity, session.Cargo.Fame)) + " 金";
                if (departureBasicSupplyEndurance.TryGetValue(item, out Text endurance))
                    endurance.text = daily > 0 ? "可供 " + (held / daily).ToString("0.#") + " 天　购后 " +
                        (((long)held + quantity) / daily).ToString("0.#") + " 天" : "请先配置出航船员";
            }
            decimal cost = QuoteDepartureBasicSupplies(now, out string reason);
            departureBasicSupplyPurchase.GetComponentInChildren<Text>().text = "购买 · " + PortMarketCatalog.FormatPrice(cost) + " 金";
            departureBasicSupplyPurchase.interactable = editable && cost > 0m && reason.Length == 0;
            departureBasicSupplyHint.text = reason.Length > 0 ? reason : notice;
            departureBasicSupplyHint.color = reason.Length > 0 ? new Color(.55f, .23f, .16f, 1f) : MarketShopSkin.MutedInk;
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
            RefreshDepartureChartControls();
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
            departureBasicSupplyInputs.Clear();
            departureBasicSupplyPrices.Clear();
            departureBasicSupplySubtotals.Clear();
            departureBasicSupplyInvalidInputs.Clear();
            departureBasicSupplyDecrease.Clear();
            departureBasicSupplyIncrease.Clear();
            departureBasicSupplyDecreaseTen.Clear();
            departureBasicSupplyIncreaseTen.Clear();
        }
    }
}
