using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class WarehouseCargoQuote
        {
            public readonly CoreVoyageSession Session;
            public readonly RectTransform View;
            public readonly int Revision;
            public readonly string Port;
            public readonly PortMarketItem Item;
            public readonly CoreWarehouseItemDefinition Definition;
            public readonly string Key;
            public readonly int Stored;
            public readonly int Carried;
            public readonly int MaxWithdrawal;
            public readonly decimal Gold;
            public readonly float Capacity;
            public readonly float UsedVolume;
            public readonly double UnitVolume;
            public readonly DateTime Date;
            public readonly decimal SellUnitPrice;
            public readonly bool Preview;
            public bool Consumed;

            public WarehouseCargoQuote(CoreVoyageSession owner, RectTransform view, int revision,
                PortMarketItem item, DateTime date, bool preview)
                : this(owner, view, revision, PortMarketCatalog.Key(item), date, preview)
            {
                Item = item;
                SellUnitPrice = PortMarketCatalog.SellUnitPrice(Port, item, Date);
            }

            public WarehouseCargoQuote(CoreVoyageSession owner, RectTransform view, int revision,
                string key, DateTime date, bool preview)
            {
                Session = owner;
                View = view;
                Revision = revision;
                Port = owner.CurrentPort;
                Definition = CoreWarehouseItemCatalog.Find(key);
                if (Definition == null) throw new ArgumentException("Unknown warehouse item", nameof(key));
                Key = Definition.Key;
                Stored = owner.PortWarehouse.GetAmount(Port, Key);
                Carried = owner.GetWarehouseCargoAmount(Key);
                MaxWithdrawal = owner.GetMaxPortWarehouseWithdrawal(Key);
                Gold = owner.ProfileGold;
                Capacity = owner.CargoCapacityValue;
                UsedVolume = owner.Cargo.UsedVolume;
                UnitVolume = Definition.UnitVolume;
                Date = MarketMonth(date);
                SellUnitPrice = CoreRareItemEconomy.SaleUnitPrice(Key);
                Preview = preview;
            }
        }

        private WarehouseCargoQuote activeWarehouseCargoQuote;
        private object warehouseCargoDialogIdentity;
        private RectTransform warehouseCargoModal;

        private WarehouseCargoQuote CreateWarehouseCargoQuote(PortMarketItem item, bool preview = false)
        {
            return new WarehouseCargoQuote(session, portWarehouseInventoryView, warehouseViewRevision,
                item, preview ? new DateTime(2026, 10, 1) : MarketNow, preview);
        }

        private WarehouseCargoQuote CreateWarehouseCargoQuote(string key, bool preview = false) =>
            new WarehouseCargoQuote(session, portWarehouseInventoryView, warehouseViewRevision,
                key, preview ? new DateTime(2026, 10, 1) : MarketNow, preview);

        private void InvalidateWarehouseCargoDialog()
        {
            if (activeWarehouseCargoQuote != null) activeWarehouseCargoQuote.Consumed = true;
            activeWarehouseCargoQuote = null;
            warehouseCargoDialogIdentity = null;
            warehouseCargoModal = null;
        }

        private void OpenWarehouseCargoActions(PortMarketItem item, int carried, int stored)
        {
            if (!Application.isPlaying || editorPreviewBuilding || !WarehouseCargoActionsAllowed || session == null ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 0 || !warehouseStorageOpen || warehouseRareStorageOpen ||
                portWarehouseInventoryView == null || !portWarehouseInventoryView.gameObject.activeInHierarchy ||
                activeWarehouseCargoQuote != null) return;
            string key = PortMarketCatalog.Key(item);
            if (session.GetMarketCargoAmount(key) != carried ||
                session.PortWarehouse.GetAmount(CurrentWarehousePort, key) != stored)
            {
                portWarehouseNotice = "库存已变化，请重新选择物资。";
                Render();
                return;
            }
            WarehouseCargoQuote quote = CreateWarehouseCargoQuote(item);
            activeWarehouseCargoQuote = quote;
            BuildWarehouseCargoActions(quote);
        }

        private void BuildWarehouseCargoActions(WarehouseCargoQuote quote)
        {
            RectTransform dialog = BeginWarehouseCargoDialog("Warehouse Item Actions", new Vector2(780f, 520f));
            object identity = warehouseCargoDialogIdentity;
            CreateItemIcon("Warehouse Item Actions Icon", dialog, ItemIconCatalog.ForCargo(quote.Key), new Vector2(-325f, 201f), 40f);
            WarehouseDialogLabel("Warehouse Item Actions Title", dialog, new Vector2(648f, 52f),
                new Vector2(28f, 201f), 28, quote.Definition.Name + " · " + quote.Port);
            WarehouseDialogLabel("Warehouse Item Actions Inventory", dialog, new Vector2(704f, 84f),
                new Vector2(0f, 128f), 21, "本港库存 " + quote.Stored + "　随船数量 " + quote.Carried +
                "\n随船占用 " + WarehouseVolume(quote.Carried * (double)quote.UnitVolume) + " 舱位");
            WarehouseDialogLabel("Warehouse Item Description", dialog, new Vector2(704f, 76f),
                new Vector2(0f, 40f), 21, quote.Definition.Description);
            WarehouseDialogButton("Warehouse Adjust Cargo", dialog, new Vector2(310f, 62f),
                new Vector2(-172f, -64f), "存入 / 取出", () =>
                {
                    if (WarehouseQuoteIdentityMatches(quote, identity)) BuildWarehouseCargoAdjustment(quote, 0);
                });
            WarehouseDialogButton("Warehouse Sell Cargo", dialog, new Vector2(310f, 62f),
                new Vector2(172f, -64f), "出售", () =>
                {
                    if (WarehouseQuoteIdentityMatches(quote, identity)) BuildWarehouseCargoSale(quote, 0, 0);
                });
            WarehouseDialogButton("Cancel Warehouse Item Actions", dialog, new Vector2(300f, 54f),
                new Vector2(0f, -197f), "返回库存", () => CloseWarehouseCargoDialog(quote, identity));
        }

        private RectTransform BeginWarehouseCargoDialog(string name, Vector2 size)
        {
            if (warehouseCargoModal != null)
            {
                warehouseCargoModal.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(warehouseCargoModal.gameObject);
                else DestroyImmediate(warehouseCargoModal.gameObject);
            }
            warehouseCargoDialogIdentity = new object();
            if (portWarehouseInventoryView != null && portWarehouseInventoryView.parent != null)
            {
                GameObject warehouse = portWarehouseInventoryView.parent.gameObject;
                CanvasGroup background = warehouse.GetComponent<CanvasGroup>();
                if (background == null) background = warehouse.AddComponent<CanvasGroup>();
                background.interactable = false;
                background.blocksRaycasts = false;
            }
            warehouseCargoModal = CreateFixedPanel("Warehouse Storage Modal", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(0f, 0f, 0f, .76f));
            Image shade = warehouseCargoModal.GetComponent<Image>();
            shade.sprite = null;
            shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(0f, 0f, 0f, .68f);
            shade.raycastTarget = true;
            warehouseCargoModal.SetAsLastSibling();
            RectTransform dialog = CreateFixedPanel(name, warehouseCargoModal, size,
                new Vector2(0f, -12f), new Color(.91f, .82f, .65f));
            dialog.GetComponent<Image>().raycastTarget = true;
            return dialog;
        }

        private Text WarehouseDialogLabel(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, string value)
        {
            Text label = WarehouseLabel(name, parent, size, position, fontSize, value, WarehouseInk);
            label.raycastTarget = false;
            // Modal prose and live quote/status rows share one clipped paragraph
            // path. Larger reading modes therefore scroll the complete value instead
            // of shrinking it into the fixed dialog frame.
            int standard = name.EndsWith("Title", StringComparison.Ordinal) ? Mathf.Max(32, fontSize) : Mathf.Max(22, fontSize);
            return ReadableParagraph(label, parent, size, position, standard, true).content.GetComponentInChildren<Text>();
        }

        private static void SetWarehouseDialogText(Text label, string value)
        {
            if (label == null) return;
            label.text = value;
            RefreshReadableParagraph(label);
        }

        private Button WarehouseDialogButton(string name, Transform parent, Vector2 size,
            Vector2 position, string caption, Action action)
        {
            size.y = Mathf.Max(size.y, 42f * GameReadability.Scale);
            Button button = CreateButton(name, parent, size, position, Gold, DeepSea, GameReadability.FontSize(22));
            button.GetComponentInChildren<Text>().text = caption;
            GameReadability.Body(button.GetComponentInChildren<Text>(), 22);
            button.onClick.AddListener(() => action());
            return button;
        }

        private Slider WarehouseQuantitySlider(string name, Transform parent, Vector2 position,
            int minimum, int maximum, int initial)
        {
            return UiKit.CreateQuantitySlider(name, parent, new Vector2(650f, 44f), position,
                minimum, maximum, initial, Html("#071C24"), Gold, circleSprite);
        }

        private static int WarehouseSliderQuantity(Slider slider, int minimum, int maximum)
        {
            return UiKit.ReadQuantity(slider, minimum, maximum);
        }

        private bool WarehouseQuoteIdentityMatches(WarehouseCargoQuote quote, object identity)
        {
            return Application.isPlaying && !editorPreviewBuilding && WarehouseCargoActionsAllowed &&
                quote != null && !quote.Preview && !quote.Consumed &&
                ReferenceEquals(activeWarehouseCargoQuote, quote) &&
                ReferenceEquals(warehouseCargoDialogIdentity, identity) &&
                ReferenceEquals(session, quote.Session) && session.Phase == CoreDemoPhase.Port &&
                session.CurrentPort == quote.Port && activePortFacility == 0 && warehouseStorageOpen &&
                warehouseRareStorageOpen == quote.Definition.IsRare &&
                warehouseViewRevision == quote.Revision &&
                ReferenceEquals(portWarehouseInventoryView, quote.View) &&
                quote.View != null && quote.View.gameObject.activeInHierarchy &&
                warehouseCargoModal != null && warehouseCargoModal.gameObject.activeInHierarchy;
        }

        private bool WarehouseQuoteInventoryUnchanged(WarehouseCargoQuote quote)
        {
            return session.GetWarehouseCargoAmount(quote.Key) == quote.Carried &&
                session.PortWarehouse.GetAmount(quote.Port, quote.Key) == quote.Stored &&
                session.CargoCapacityValue.Equals(quote.Capacity) &&
                session.Cargo.UsedVolume.Equals(quote.UsedVolume) &&
                CoreWarehouseItemCatalog.Find(quote.Key).UnitVolume.Equals(quote.UnitVolume) &&
                session.GetMaxPortWarehouseWithdrawal(quote.Key) == quote.MaxWithdrawal;
        }

        private void CloseWarehouseCargoDialog(WarehouseCargoQuote quote, object identity)
        {
            if (!WarehouseQuoteIdentityMatches(quote, identity)) return;
            quote.Consumed = true;
            activeWarehouseCargoQuote = null;
            warehouseCargoDialogIdentity = null;
            Render();
        }

        private void FinishWarehouseCargoDialog(WarehouseCargoQuote quote, string notice, bool saved)
        {
            quote.Consumed = true;
            activeWarehouseCargoQuote = null;
            warehouseCargoDialogIdentity = null;
            portWarehouseNotice = notice;
            if (saved) SaveProgress();
            Render();
        }

        private void BuildWarehouseCargoAdjustment(WarehouseCargoQuote quote, int initial)
        {
            RectTransform dialog = BeginWarehouseCargoDialog("Warehouse Cargo Adjustment", new Vector2(860f, 600f));
            object identity = warehouseCargoDialogIdentity;
            CreateItemIcon("Warehouse Cargo Adjustment Icon", dialog, ItemIconCatalog.ForCargo(quote.Key), new Vector2(-365f, 241f), 40f);
            WarehouseDialogLabel("Warehouse Cargo Adjustment Title", dialog, new Vector2(728f, 54f),
                new Vector2(28f, 241f), 28, "存入 / 取出 · " + quote.Definition.Name);
            WarehouseDialogLabel("Warehouse Cargo Adjustment Inventory", dialog, new Vector2(784f, 70f),
                new Vector2(0f, 166f), 20, quote.Port + "　本港 " + quote.Stored + " / 随船 " + quote.Carried +
                "\n单件占用 " + WarehouseVolume(quote.UnitVolume) + " 舱位");
            Slider slider = WarehouseQuantitySlider("Warehouse Cargo Adjustment Slider", dialog,
                new Vector2(0f, 81f), -quote.Carried, quote.MaxWithdrawal, initial);
            WarehouseDialogLabel("Warehouse Cargo Adjustment Endpoints", dialog, new Vector2(744f, 36f),
                new Vector2(0f, 28f), 18, "← 全部存仓 " + quote.Carried + "　　　　　　　　尽量装船 " + quote.MaxWithdrawal + " →");
            Text preview = WarehouseDialogLabel("Warehouse Cargo Adjustment Preview", dialog,
                new Vector2(784f, 132f), new Vector2(0f, -70f), 23, string.Empty);
            Text status = WarehouseDialogLabel("Warehouse Cargo Adjustment Status", dialog,
                new Vector2(784f, 50f), new Vector2(0f, -171f), 18, string.Empty);
            Button confirm = null;
            Action update = () =>
            {
                int delta = WarehouseSliderQuantity(slider, -quote.Carried, quote.MaxWithdrawal);
                long shipAfter = (long)quote.Carried + delta;
                long storedAfter = (long)quote.Stored - delta;
                double change = delta * (double)quote.UnitVolume;
                double after = quote.UsedVolume + change;
                SetWarehouseDialogText(preview,  (delta > 0 ? "装入船舱 " + delta : delta < 0 ? "存入本港 " + -(long)delta : "尚未调整") +
                    "\n随船 " + quote.Carried + " → " + shipAfter + "　　本港 " + quote.Stored + " → " + storedAfter +
                    "\n总船舱 " + WarehouseVolume(quote.UsedVolume) + " → " + WarehouseVolume(after) +
                    " / " + WarehouseVolume(quote.Capacity) +
                    (change < 0 ? "　释放 " : "　增加 ") + WarehouseVolume(Math.Abs(change)) + " 舱位");
                string failure = WarehouseAdjustmentFailure(quote, delta);
                SetWarehouseDialogText(status, failure.Length > 0 ? failure : "确认后立即转移；取消不会改变库存。");
                if (confirm != null) confirm.interactable = !quote.Preview && Application.isPlaying && failure.Length == 0;
            };
            confirm = WarehouseDialogButton("Confirm Warehouse Cargo Adjustment", dialog,
                new Vector2(332f, 60f), new Vector2(185f, -242f), "确认调整", () =>
                {
                    if (!WarehouseQuoteIdentityMatches(quote, identity)) return;
                    int delta = WarehouseSliderQuantity(slider, -quote.Carried, quote.MaxWithdrawal);
                    if (!WarehouseQuoteInventoryUnchanged(quote))
                    {
                        FinishWarehouseCargoDialog(quote, "库存或货舱状态已变化，请重新调整。", false);
                        return;
                    }
                    if (WarehouseAdjustmentFailure(quote, delta).Length > 0) return;
                    quote.Consumed = true;
                    bool success = session.TryAdjustPortWarehouseCargo(quote.Key, delta);
                    FinishWarehouseCargoDialog(quote, success ? "已调整" + quote.Definition.Name +
                        "：" + (delta > 0 ? "装船 " + delta : "入仓 " + -(long)delta) + "。" : "转移未完成，请核对库存与货舱空间。", success);
                });
            WarehouseDialogButton("Cancel Warehouse Cargo Adjustment", dialog,
                new Vector2(332f, 60f), new Vector2(-185f, -242f), "取消", () => CloseWarehouseCargoDialog(quote, identity));
            slider.onValueChanged.AddListener(_ => update());
            update();
        }

        private static string WarehouseAdjustmentFailure(WarehouseCargoQuote quote, int delta)
        {
            if (delta == 0) return quote.Carried == 0 && quote.MaxWithdrawal == 0
                ? "没有可转移的物资，或船舱已满。" : "拖动滑条选择转移数量。";
            if (delta < -quote.Carried || delta > quote.MaxWithdrawal) return "转移数量超过库存或可用舱位。";
            if ((long)quote.Stored - delta > int.MaxValue) return "本港库存已达数量上限，请减少存入数量。";
            if ((long)quote.Carried + delta > int.MaxValue) return "随船数量已达上限，请减少装船数量。";
            if (delta > 0 && quote.UsedVolume + delta * (double)quote.UnitVolume > quote.Capacity + .001d)
                return "船舱空间不足，请减少装船数量。";
            return string.Empty;
        }

        private void BuildWarehouseCargoSale(WarehouseCargoQuote quote, int initialStored, int initialShip)
        {
            RectTransform dialog = BeginWarehouseCargoDialog("Warehouse Cargo Sale", new Vector2(900f, 760f));
            object identity = warehouseCargoDialogIdentity;
            CreateItemIcon("Warehouse Cargo Sale Icon", dialog, ItemIconCatalog.ForCargo(quote.Key), new Vector2(-385f, 325f), 40f);
            WarehouseDialogLabel("Warehouse Cargo Sale Title", dialog, new Vector2(768f, 52f),
                new Vector2(28f, 325f), 28, "出售 · " + quote.Definition.Name + " · " + quote.Port);
            WarehouseDialogLabel("Warehouse Cargo Sale Date", dialog, new Vector2(824f, 36f),
                new Vector2(0f, 277f), 18, quote.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                (quote.Definition.IsRare ? "　各港固定回收价 " : "　卖价 ") +
                quote.SellUnitPrice.ToString("0.####", CultureInfo.InvariantCulture) + " 金币 / 件");
            Text storedLabel = WarehouseDialogLabel("Warehouse Stored Sale Amount", dialog, new Vector2(824f, 36f),
                new Vector2(0f, 226f), 21, string.Empty);
            Slider storedSlider = WarehouseQuantitySlider("Warehouse Stored Sale Slider", dialog,
                new Vector2(0f, 184f), 0, quote.Stored, initialStored);
            Text shipLabel = WarehouseDialogLabel("Warehouse Ship Sale Amount", dialog, new Vector2(824f, 36f),
                new Vector2(0f, 96f), 21, string.Empty);
            Slider shipSlider = WarehouseQuantitySlider("Warehouse Ship Sale Slider", dialog,
                new Vector2(0f, 54f), 0, quote.Carried, initialShip);
            Text summary = WarehouseDialogLabel("Warehouse Cargo Sale Quote", dialog, new Vector2(824f, 180f),
                new Vector2(0f, -94f), 23, string.Empty);
            Text status = WarehouseDialogLabel("Warehouse Cargo Sale Status", dialog, new Vector2(824f, 62f),
                new Vector2(0f, -238f), 18, string.Empty);
            Button confirm = null;
            Action update = () =>
            {
                int stored = WarehouseSliderQuantity(storedSlider, 0, quote.Stored);
                int carried = WarehouseSliderQuantity(shipSlider, 0, quote.Carried);
                long total = (long)stored + carried;
                decimal revenue = quote.SellUnitPrice * total;
                SetWarehouseDialogText(storedLabel, "本港出售 " + stored + " / " + quote.Stored);
                SetWarehouseDialogText(shipLabel, "随船出售 " + carried + " / " + quote.Carried);
                double freed = carried * (double)quote.UnitVolume;
                SetWarehouseDialogText(summary,  "合计出售 " + total + " 件　获得 " + PortMarketCatalog.FormatPrice(revenue) + " 金币" +
                    "\n剩余：本港 " + (quote.Stored - stored) + "　随船 " + (quote.Carried - carried) +
                    "\n总船舱 " + WarehouseVolume(quote.UsedVolume) + " → " + WarehouseVolume(quote.UsedVolume - freed) +
                    " / " + WarehouseVolume(quote.Capacity) + "　释放 " + WarehouseVolume(freed) + " 舱位" +
                    "\n金币 " + PortMarketCatalog.FormatPrice(quote.Gold) + " → " + PortMarketCatalog.FormatPrice(quote.Gold + revenue));
                string failure = WarehouseSaleFailure(quote, stored, carried, revenue);
                SetWarehouseDialogText(status,  failure.Length > 0 ? failure : quote.Definition.IsRare
                    ? "珍稀物品可用于高阶捐献与海上事件。"
                    : "出售不占采购额度。");
                if (confirm != null) confirm.interactable = !quote.Preview && Application.isPlaying && failure.Length == 0;
            };
            confirm = WarehouseDialogButton("Confirm Warehouse Cargo Sale", dialog, new Vector2(346f, 60f),
                new Vector2(192f, -315f), "确认出售", () =>
                {
                    if (!WarehouseQuoteIdentityMatches(quote, identity)) return;
                    if (!WarehouseQuoteInventoryUnchanged(quote) || session.ProfileGold != quote.Gold ||
                        MarketMonth(MarketNow) != quote.Date ||
                        CurrentWarehouseSaleUnitPrice(quote) != quote.SellUnitPrice)
                    {
                        FinishWarehouseCargoDialog(quote, "库存、金币或价格已变化，请重新选择。", false);
                        return;
                    }
                    int stored = WarehouseSliderQuantity(storedSlider, 0, quote.Stored);
                    int carried = WarehouseSliderQuantity(shipSlider, 0, quote.Carried);
                    decimal revenue = quote.SellUnitPrice * ((long)stored + carried);
                    if (WarehouseSaleFailure(quote, stored, carried, revenue).Length > 0) return;
                    quote.Consumed = true;
                    bool success = session.TrySellPortWarehouseCargo(quote.Key, stored, carried, revenue);
                    FinishWarehouseCargoDialog(quote, success ? "已出售" + quote.Definition.Name +
                        " " + ((long)stored + carried) + " 件，获得 " + PortMarketCatalog.FormatPrice(revenue) +
                        " 金币。" : "出售未完成，货物与金币未变。请重新核对。", success);
                });
            WarehouseDialogButton("Cancel Warehouse Cargo Sale", dialog, new Vector2(346f, 60f),
                new Vector2(-192f, -315f), "取消", () => CloseWarehouseCargoDialog(quote, identity));
            storedSlider.onValueChanged.AddListener(_ => update());
            shipSlider.onValueChanged.AddListener(_ => update());
            update();
        }

        private decimal CurrentWarehouseSaleUnitPrice(WarehouseCargoQuote quote) => quote.Definition.IsRare
            ? CoreRareItemEconomy.SaleUnitPrice(quote.Key)
            : PortMarketCatalog.SellUnitPrice(quote.Port, quote.Item, MarketNow.Date);

        private static string WarehouseSaleFailure(WarehouseCargoQuote quote, int stored, int carried, decimal revenue)
        {
            if (stored < 0 || carried < 0 || stored > quote.Stored || carried > quote.Carried)
                return "出售数量超过库存，请重新选择。";
            if ((long)stored + carried == 0) return "拖动两条滑条，选择本港与随船出售数量。";
            if (revenue <= 0) return "请选择可出售的物品数量。";
            if (revenue > CoreEconomyBalance.MaximumProfileGold - quote.Gold)
                return "出售后超过金币账户上限，请减少数量；不会扣除未结算的货物。";
            return string.Empty;
        }
    }
}
