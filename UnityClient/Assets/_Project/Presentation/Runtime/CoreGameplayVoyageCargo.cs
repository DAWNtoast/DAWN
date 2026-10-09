using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform voyageCargoOverlay;
        private CanvasGroup voyageCargoPageGroup;
        private bool voyageCargoPreviousInteractable, voyageCargoPreviousRaycasts;
        private GameObject voyageCargoPreviousSelection;
        private bool voyageCargoSpecial;
        private string voyageCargoSelectedKey;
        private int voyageCargoRevision;
        private InputField voyageCargoQuantity;
        private Text voyageCargoNotice;

        private bool CanOpenVoyageCargo => isActiveAndEnabled && !profileSaveBlocked && !navigationMotionPlaying &&
            !voyageMenuTransition && session != null && (session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter) &&
            contentRoot != null && contentRoot.parent != null && portSettingsOverlay == null && gameMasterOverlay == null;

        private void OpenVoyageCargo()
        {
            if (!CanOpenVoyageCargo) return;
            if (voyageCargoOverlay != null) { voyageCargoOverlay.SetAsLastSibling(); return; }
            voyageCargoPreviousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            voyageCargoPageGroup = contentRoot.GetComponent<CanvasGroup>();
            if (voyageCargoPageGroup == null) voyageCargoPageGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            voyageCargoPreviousInteractable = voyageCargoPageGroup.interactable;
            voyageCargoPreviousRaycasts = voyageCargoPageGroup.blocksRaycasts;
            voyageCargoPageGroup.interactable = false;
            voyageCargoPageGroup.blocksRaycasts = false;
            voyageCargoOverlay = CreateStretchPanel("Voyage Cargo Overlay", contentRoot.parent, new Color(.03f,.02f,.01f,.8f));
            voyageCargoOverlay.SetAsLastSibling();
            BuildVoyageCargoView();
        }

        private bool VoyageCargoViewCurrent(RectTransform owner, int revision, CoreVoyageSession expectedSession,
            object expectedMap, CoreDemoPhase phase) => CanOpenVoyageCargo && owner != null && owner == voyageCargoOverlay &&
            owner.gameObject.activeInHierarchy && voyageCargoRevision == revision && ReferenceEquals(session, expectedSession) &&
            ReferenceEquals(session.Map, expectedMap) && session.Phase == phase;

        private void BuildVoyageCargoView()
        {
            if (voyageCargoOverlay == null) return;
            int revision = ++voyageCargoRevision;
            RectTransform owner = voyageCargoOverlay;
            CoreVoyageSession capturedSession = session;
            object map = session.Map;
            CoreDemoPhase phase = session.Phase;
            Func<bool> current = () => VoyageCargoViewCurrent(owner, revision, capturedSession, map, phase);
            ClearChildren(owner);
            Color ink = Html("#382819"), muted = Html("#695039");
            RectTransform sheet = CreateFixedPanel("Voyage Cargo Sheet", owner, new Vector2(1280,880), Vector2.zero, Html("#DFC591"));
            CargoLabel("Voyage Cargo Title", sheet, new Vector2(820,50), new Vector2(-80,380), 32, "船舱", ink);
            CargoLabel("Voyage Cargo Capacity", sheet, new Vector2(1120,40), new Vector2(0,330), 22,
                "船舱占用 " + session.Cargo.UsedVolume.ToString("0.##", CultureInfo.InvariantCulture) + " / " +
                session.CargoCapacityValue.ToString("0.##", CultureInfo.InvariantCulture) + "　·　随船金币 " + session.Cargo.Gold, ink);
            Button close = CargoButton("Close Voyage Cargo", sheet, new Vector2(96,46), new Vector2(542,380), "关闭", current,
                () => CloseVoyageCargo(true));
            Button basic = CargoButton("Voyage Cargo Basic Tab", sheet, new Vector2(220,46), new Vector2(-380,274), "普通物品", current,
                () => { voyageCargoSpecial = false; voyageCargoSelectedKey = null; BuildVoyageCargoView(); });
            Button special = CargoButton("Voyage Cargo Special Tab", sheet, new Vector2(220,46), new Vector2(-136,274), "特殊物品", current,
                () => { voyageCargoSpecial = true; voyageCargoSelectedKey = null; BuildVoyageCargoView(); });
            WoodButtonSkin.SetSelected(basic, !voyageCargoSpecial);
            WoodButtonSkin.SetSelected(special, voyageCargoSpecial);
            CargoLabel("Voyage Cargo Name Header", sheet, new Vector2(270,28), new Vector2(-382,220), 18, "物品", muted);
            CargoLabel("Voyage Cargo Amount Header", sheet, new Vector2(150,28), new Vector2(-119,220), 18, "随船数量", muted);
            CargoLabel("Voyage Cargo Volume Header", sheet, new Vector2(126,28), new Vector2(59,220), 18, "占用空间", muted);
            RectTransform list = CreateRect("Voyage Cargo List", sheet, new Vector2(790,520), new Vector2(-200,-52));
            var items = voyageCargoSpecial ? CoreWarehouseItemCatalog.RareItems : CoreWarehouseItemCatalog.BasicItems;
            for (int i = 0; i < items.Count; i++)
            {
                CoreWarehouseItemDefinition item = items[i];
                string key = item.Key;
                int amount = session.GetWarehouseCargoAmount(key);
                Button row = CargoButton("Voyage Cargo Item " + key, list, new Vector2(766,48), new Vector2(0,224-i*56), string.Empty,
                    current, () => { voyageCargoSelectedKey = key; BuildVoyageCargoView(); });
                WoodButtonSkin.SetSelected(row, key == voyageCargoSelectedKey);
                CreateItemIcon("Voyage Cargo Item Icon " + key, row.transform, ItemIconCatalog.ForCargo(key), new Vector2(-343,0),40);
                CargoLabel("Voyage Cargo Item Name " + key, row.transform, new Vector2(260,40), new Vector2(-182,0),20,item.Name,Ink);
                CargoLabel("Voyage Cargo Item Amount " + key, row.transform, new Vector2(150,40), new Vector2(81,0),20,
                    amount.ToString(CultureInfo.InvariantCulture),Ink);
                CargoLabel("Voyage Cargo Item Volume " + key, row.transform, new Vector2(126,40), new Vector2(259,0),18,
                    (amount*item.UnitVolume).ToString("0.##",CultureInfo.InvariantCulture),Ink);
            }
            RectTransform details = CreateFixedPanel("Voyage Cargo Details", sheet, new Vector2(350,520), new Vector2(415,-52),Html("#D2B582"));
            CoreWarehouseItemDefinition selected = CoreWarehouseItemCatalog.Find(voyageCargoSelectedKey);
            if (selected != null && selected.IsRare == voyageCargoSpecial)
                BuildVoyageCargoSelection(details, selected, current, revision);
            else
            {
                voyageCargoSelectedKey = null;
                CargoLabel("Voyage Cargo Selection Hint", details, new Vector2(306,110), new Vector2(0,32),23,
                    "选择物品",ink);
            }
            voyageCargoNotice = CargoLabel("Voyage Cargo Notice", sheet, new Vector2(1120,60), new Vector2(0,-374),18,
                Application.isPlaying && !editorPreviewBuilding ? "" :
                "编辑预览 · 舍弃与保存已禁用",muted);
            foreach (Selectable control in sheet.GetComponentsInChildren<Selectable>())
                control.gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested = () => { if (current()) CloseVoyageCargo(true); };
            if (!Application.isPlaying) SetEditorPreviewFlags(owner);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        private void BuildVoyageCargoSelection(RectTransform details, CoreWarehouseItemDefinition item, Func<bool> current, int revision)
        {
            Color ink = Html("#382819"), muted = Html("#695039");
            int available = session.GetWarehouseCargoAmount(item.Key);
            CreateItemIcon("Voyage Cargo Selected Icon",details,ItemIconCatalog.ForCargo(item.Key),new Vector2(0,206),64);
            CargoLabel("Voyage Cargo Selected Item",details,new Vector2(310,36),new Vector2(0,153),25,item.Name,ink);
            CargoLabel("Voyage Cargo Selected Amount",details,new Vector2(310,30),new Vector2(0,114),20,"随船数量："+available,ink);
            CargoLabel("Voyage Cargo Unit Volume",details,new Vector2(310,26),new Vector2(0,83),18,
                "每件占用："+item.UnitVolume.ToString("0.##",CultureInfo.InvariantCulture),muted);
            CargoLabel("Voyage Cargo Item Description",details,new Vector2(310,72),new Vector2(0,25),17,item.Description,muted);
            CargoLabel("Voyage Cargo Quantity Label",details,new Vector2(310,26),new Vector2(0,-35),20,"舍弃数量",ink);
            RectTransform field = CreateRect("Voyage Cargo Quantity",details,new Vector2(166,44),new Vector2(0,-69));
            Image background = field.gameObject.AddComponent<Image>(); background.color = Html("#F8E7BF");
            InputField quantity = field.gameObject.AddComponent<InputField>();
            voyageCargoQuantity = quantity;
            Text inputText = CargoLabel("Voyage Cargo Quantity Text",field,new Vector2(150,36),Vector2.zero,23,string.Empty,ink);
            quantity.targetGraphic = background;
            quantity.textComponent = inputText;
            quantity.contentType = InputField.ContentType.IntegerNumber;
            quantity.lineType = InputField.LineType.SingleLine;
            quantity.characterLimit = 10;
            quantity.SetTextWithoutNotify(available > 0 ? "1" : "0");
            Action<long> setQuantity = value =>
            {
                if (!current()) return;
                quantity.text = Math.Max(0L,Math.Min(available,value)).ToString(CultureInfo.InvariantCulture);
            };
            CargoButton("Voyage Cargo Quantity Decrease",details,new Vector2(60,44),new Vector2(-121,-69),"−",current,
                () => setQuantity(ReadCargoQuantity(quantity.text)-1L));
            CargoButton("Voyage Cargo Quantity Increase",details,new Vector2(60,44),new Vector2(121,-69),"+",current,
                () => setQuantity((long)ReadCargoQuantity(quantity.text)+1));
            CargoButton("Voyage Cargo Discard All",details,new Vector2(264,38),new Vector2(0,-122),"全部数量",current,
                () => setQuantity(available));
            Text warning = CargoLabel("Voyage Cargo Discard Warning",details,new Vector2(310,48),new Vector2(0,-174),17,
                item.Key == "water" || item.Key == "food" ? "舍弃后无法取回\n补给减少会缩短航行续航" : "舍弃后无法取回",Html("#843B27"));
            bool consumed = false;
            Button confirm = null;
            confirm = CargoButton("Confirm Voyage Cargo Discard",details,new Vector2(310,52),new Vector2(0,-226),"确认舍弃",current,() =>
            {
                if (consumed || !Application.isPlaying || editorPreviewBuilding || voyageCargoRevision != revision) return;
                int amount = ReadCargoQuantity(quantity.text);
                if (amount <= 0 || amount > available) return;
                if (session.GetWarehouseCargoAmount(item.Key) != available)
                {
                    voyageCargoSelectedKey = null;
                    BuildVoyageCargoView();
                    voyageCargoNotice.text = "数量已变化，请重新选择。";
                    return;
                }
                if (!session.TryDiscardVoyageCargo(item.Key,amount))
                {
                    voyageCargoNotice.text = "无法舍弃，请重新选择物品。";
                    return;
                }
                consumed = true;
                bool special = voyageCargoSpecial;
                string key = item.Key;
                // Voyage cargo stays in the active voyage until the ordinary
                // suspension/settlement save. Never write it into the port profile.
                CloseVoyageCargo(false);
                Render();
                voyageCargoSpecial = special;
                voyageCargoSelectedKey = key;
                OpenVoyageCargo();
                if (voyageCargoNotice != null) voyageCargoNotice.text = "已舍弃 "+item.Name+" ×"+amount+"。";
            });
            Action refresh = () =>
            {
                if (!current()) return;
                int amount = ReadCargoQuantity(quantity.text);
                bool valid = amount > 0 && amount <= available;
                confirm.interactable = valid && Application.isPlaying && !editorPreviewBuilding;
                confirm.GetComponentInChildren<Text>().text = valid ? "确认舍弃 ×"+amount : "确认舍弃";
                warning.text = available <= 0 ? "船舱内未携带此物品" : !valid ? "请输入 1 至 "+available+" 之间的整数" :
                    item.Key == "water" || item.Key == "food" ? "舍弃后无法取回\n补给减少会缩短航行续航" : "舍弃后无法取回";
            };
            quantity.onValueChanged.AddListener(_ => refresh());
            refresh();
        }

        private static int ReadCargoQuantity(string value) => int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int quantity)
            ? quantity : 0;

        private Text CargoLabel(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, string value, Color color)
        {
            Text label = CreateText(name,parent,size,position,fontSize,FontStyle.Normal,TextAnchor.MiddleCenter,color);
            label.text = value;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button CargoButton(string name, Transform parent, Vector2 size, Vector2 position, string caption, Func<bool> current, Action action)
        {
            Button button = CreateButton(name,parent,size,position,Html("#765133"),Ink,20);
            button.GetComponentInChildren<Text>().text = caption;
            button.onClick.AddListener(() =>
            {
                if (!current() || button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return;
                action();
            });
            return button;
        }

        private void CloseVoyageCargo(bool restoreFocus)
        {
            if (voyageCargoOverlay == null && voyageCargoPageGroup == null) return;
            ++voyageCargoRevision;
            if (voyageCargoPageGroup != null)
            {
                voyageCargoPageGroup.interactable = voyageCargoPreviousInteractable;
                voyageCargoPageGroup.blocksRaycasts = voyageCargoPreviousRaycasts;
            }
            voyageCargoPageGroup = null;
            RectTransform overlay = voyageCargoOverlay;
            voyageCargoOverlay = null;
            voyageCargoQuantity = null;
            voyageCargoNotice = null;
            if (overlay != null)
            {
                overlay.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
            }
            if (restoreFocus && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(voyageCargoPreviousSelection != null && voyageCargoPreviousSelection.activeInHierarchy
                    ? voyageCargoPreviousSelection : null);
            voyageCargoPreviousSelection = null;
        }
    }
}
