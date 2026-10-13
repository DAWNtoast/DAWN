using System;
using System.Collections.Generic;
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
        private Text voyageCargoNotice, voyageCargoCapacity;
        private RectTransform voyageCargoList, voyageCargoSelectionRoot;
        private Text voyageCargoSelectionHint;
        private Button voyageCargoBasicTab, voyageCargoSpecialTab;
        private Func<bool> voyageCargoCurrent;
        private Action<CoreWarehouseItemDefinition, Func<bool>> voyageCargoBindSelection;
        private int voyageCargoGridRevision, voyageCargoSelectionRevision, voyageCargoSelectedAmount;
        private readonly Dictionary<string, Button> voyageCargoRows = new Dictionary<string, Button>();

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
            voyageCargoCurrent = current;
            ClearChildren(owner);
            Color ink = Html("#382819"), muted = Html("#695039");
            RectTransform sheet = CreateFixedPanel("Voyage Cargo Sheet", owner, new Vector2(1280,920), Vector2.zero, Html("#DFC591"));
            CargoLabel("Voyage Cargo Title", sheet, new Vector2(820,50), new Vector2(-80,380), 32, "船舱", ink);
            voyageCargoCapacity = CargoLabel("Voyage Cargo Capacity", sheet, new Vector2(1120,40), new Vector2(0,330), 22,"",ink);
            Button close = CargoButton("Close Voyage Cargo", sheet, new Vector2(96,46), new Vector2(542,380), "关闭", current,
                () => CloseVoyageCargo(true));
            voyageCargoBasicTab = CargoButton("Voyage Cargo Basic Tab", sheet, new Vector2(220,46), new Vector2(-322,274), "普通物品", current,
                () => SwitchVoyageCargoTab(false));
            voyageCargoSpecialTab = CargoButton("Voyage Cargo Special Tab", sheet, new Vector2(220,46), new Vector2(-78,274), "特殊物品", current,
                () => SwitchVoyageCargoTab(true));
            WoodButtonSkin.SetSelected(voyageCargoBasicTab, !voyageCargoSpecial);
            WoodButtonSkin.SetSelected(voyageCargoSpecialTab, voyageCargoSpecial);
            voyageCargoList = CreateRect("Voyage Cargo List", sheet, new Vector2(790,548), new Vector2(-200,-36));
            BuildVoyageCargoGrid();
            RectTransform divider = CreateRect("Voyage Cargo Details Divider", sheet, new Vector2(2,548), new Vector2(217,-36));
            Image dividerInk = divider.gameObject.AddComponent<Image>();
            dividerInk.color = new Color(.38f,.27f,.15f,.24f);
            dividerInk.raycastTarget = false;
            RectTransform details = CreateRect("Voyage Cargo Details", sheet, new Vector2(350,548), new Vector2(415,-36));
            voyageCargoSelectionHint = CargoLabel("Voyage Cargo Selection Hint", details, new Vector2(306,110), new Vector2(0,32),23,
                "选择物品",ink);
            voyageCargoSelectionRoot = CreateRect("Voyage Cargo Selection", details, details.sizeDelta, Vector2.zero);
            BuildVoyageCargoSelection(voyageCargoSelectionRoot, current);
            voyageCargoNotice = CargoLabel("Voyage Cargo Notice", sheet, new Vector2(1120,60), new Vector2(0,-374),18,
                Application.isPlaying && !editorPreviewBuilding ? "" :
                "编辑预览 · 舍弃与保存已禁用",muted);
            SelectVoyageCargoItem(voyageCargoSelectedKey, true);
            if (!Application.isPlaying) SetEditorPreviewFlags(owner);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        private void SwitchVoyageCargoTab(bool special)
        {
            if (voyageCargoCurrent == null || !voyageCargoCurrent() || voyageCargoSpecial == special) return;
            voyageCargoSpecial = special;
            WoodButtonSkin.SetSelected(voyageCargoBasicTab, !special);
            WoodButtonSkin.SetSelected(voyageCargoSpecialTab, special);
            BuildVoyageCargoGrid();
            SelectVoyageCargoItem(null, true);
            if (!Application.isPlaying) SetEditorPreviewFlags(voyageCargoOverlay);
        }

        private void BuildVoyageCargoGrid()
        {
            RefreshVoyageCargoCapacity();
            int gridRevision = ++voyageCargoGridRevision;
            Func<bool> viewCurrent = voyageCargoCurrent;
            Func<bool> current = () => viewCurrent() && voyageCargoGridRevision == gridRevision;
            ClearChildren(voyageCargoList);
            voyageCargoRows.Clear();
            Color ink = Html("#382819");
            var items = voyageCargoSpecial ? CoreWarehouseItemCatalog.RareItems : CoreWarehouseItemCatalog.BasicItems;
            for (int i = 0; i < 9; i++)
            {
                Vector2 slotPosition = new Vector2((i % 3 - 1) * 266, (1 - i / 3) * 184);
                Vector2 slotSize = new Vector2(250,172);
                if (i >= items.Count)
                {
                    RectTransform empty = CreateRect("Voyage Cargo Empty Slot " + i, voyageCargoList, slotSize, slotPosition);
                    FrameVoyageCargoSlot(empty, false, true);
                    continue;
                }
                CoreWarehouseItemDefinition item = items[i];
                string key = item.Key;
                int amount = session.GetWarehouseCargoAmount(key);
                Button row = CargoButton("Voyage Cargo Item " + key, voyageCargoList, slotSize, slotPosition, string.Empty,
                    current, () => SelectVoyageCargoItem(key));
                voyageCargoRows.Add(key, row);
                StyleVoyageCargoSlot(row, key == voyageCargoSelectedKey);
                CreateItemIcon("Voyage Cargo Item Icon " + key, row.transform, ItemIconCatalog.ForCargo(key), new Vector2(0,27),80);
                CargoLabel("Voyage Cargo Item Name " + key, row.transform, new Vector2(224,24), new Vector2(0,-28),18,item.Name,ink);
                Text amountLabel = CargoLabel("Voyage Cargo Item Amount " + key, row.transform, new Vector2(112,24), new Vector2(-63,-62),18,
                    amount.ToString(CultureInfo.InvariantCulture),ink);
                Text volumeLabel = CargoLabel("Voyage Cargo Item Volume " + key, row.transform, new Vector2(112,24), new Vector2(63,-62),18,
                    (amount*item.UnitVolume).ToString("0.##",CultureInfo.InvariantCulture),ink);
                amountLabel.alignment = TextAnchor.MiddleLeft;
                volumeLabel.alignment = TextAnchor.MiddleRight;
                foreach (Text metric in new[] { amountLabel, volumeLabel })
                {
                    metric.horizontalOverflow = HorizontalWrapMode.Overflow;
                    metric.resizeTextForBestFit = true;
                    metric.resizeTextMinSize = 12;
                    metric.resizeTextMaxSize = 18;
                }
            }
        }

        private void RefreshVoyageCargoCapacity()
        {
            voyageCargoCapacity.text = "船舱占用 " + session.Cargo.UsedVolume.ToString("0.##", CultureInfo.InvariantCulture) + " / " +
                session.CargoCapacityValue.ToString("0.##", CultureInfo.InvariantCulture) + "　·　随船金币 " + session.Cargo.Gold;
        }

        private void SelectVoyageCargoItem(string key, bool force = false)
        {
            if (voyageCargoCurrent == null || !voyageCargoCurrent()) return;
            CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.Find(key);
            if (item != null && item.IsRare != voyageCargoSpecial) item = null;
            int available = item == null ? 0 : session.GetWarehouseCargoAmount(item.Key);
            if (!force && item != null && voyageCargoSelectedKey == item.Key &&
                voyageCargoSelectionRoot.gameObject.activeSelf && voyageCargoSelectedAmount == available) return;
            int selectionRevision = ++voyageCargoSelectionRevision;
            voyageCargoSelectedKey = item?.Key;
            voyageCargoSelectedAmount = available;
            foreach (KeyValuePair<string, Button> row in voyageCargoRows)
                StyleVoyageCargoSlot(row.Value, row.Key == voyageCargoSelectedKey);
            voyageCargoSelectionHint.gameObject.SetActive(item == null);
            voyageCargoSelectionRoot.gameObject.SetActive(item != null);
            if (item != null)
            {
                Func<bool> viewCurrent = voyageCargoCurrent;
                // Controls stay alive; callbacks captured for a previous item
                // must still expire after switching away and back to that item.
                Func<bool> current = () => viewCurrent() && voyageCargoSelectionRevision == selectionRevision;
                voyageCargoBindSelection(item, current);
            }
            voyageCargoNotice.text = Application.isPlaying && !editorPreviewBuilding ? "" : "编辑预览 · 舍弃与保存已禁用";
        }

        private void StyleVoyageCargoSlot(Button button, bool selected)
        {
            Image face = button.GetComponent<Image>();
            face.overrideSprite = null;
            face.sprite = null;
            face.type = Image.Type.Simple;
            face.color = Color.white;
            WoodButtonRoleState woodRole = button.GetComponent<WoodButtonRoleState>();
            if (woodRole != null) woodRole.enabled = false;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(.38f,.23f,.10f, selected ? .12f : .025f);
            colors.highlightedColor = new Color(.38f,.23f,.10f, selected ? .18f : .10f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.38f,.23f,.10f,.23f);
            colors.disabledColor = Color.clear;
            button.colors = colors;
            FrameVoyageCargoSlot((RectTransform)button.transform, selected, false);
        }

        private void FrameVoyageCargoSlot(RectTransform slot, bool selected, bool empty)
        {
            float width = slot.sizeDelta.x, height = slot.sizeDelta.y;
            float line = selected ? 2f : 1f;
            Color color = new Color(.38f,.23f,.10f, empty ? .24f : selected ? .85f : .48f);
            Vector2[] sizes = { new Vector2(width,line), new Vector2(width,line),
                new Vector2(line,height), new Vector2(line,height) };
            Vector2[] positions = { new Vector2(0,(height-line)/2), new Vector2(0,-(height-line)/2),
                new Vector2(-(width-line)/2,0), new Vector2((width-line)/2,0) };
            for (int edge = 0; edge < sizes.Length; edge++)
            {
                string name = "Voyage Cargo Slot Border " + edge;
                RectTransform rect = slot.Find(name) as RectTransform;
                if (rect == null) rect = CreateRect(name, slot, sizes[edge], positions[edge]);
                rect.sizeDelta = sizes[edge];
                rect.anchoredPosition = positions[edge];
                Image border = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
                border.color = color;
                border.raycastTarget = false;
            }
        }

        private void BuildVoyageCargoSelection(RectTransform details, Func<bool> viewCurrent)
        {
            Color ink = Html("#302012"), muted = Html("#4D321E");
            RawImage icon = CreateItemIcon("Voyage Cargo Selected Icon",details,ItemIconCatalog.ForCargo("water"),new Vector2(0,216),90);
            Text name = CargoLabel("Voyage Cargo Selected Item",details,new Vector2(310,40),new Vector2(0,144),26,"",ink);
            Text amountLabel = CargoLabel("Voyage Cargo Selected Amount",details,new Vector2(310,30),new Vector2(0,101),22,"",ink);
            Text unitVolume = CargoLabel("Voyage Cargo Unit Volume",details,new Vector2(310,28),new Vector2(0,70),20,"",muted);
            Text description = CargoLabel("Voyage Cargo Item Description",details,new Vector2(310,84),new Vector2(0,10),18,"",muted);
            CargoLabel("Voyage Cargo Quantity Label",details,new Vector2(310,26),new Vector2(0,-57),20,"舍弃数量",ink);
            RectTransform field = CreateRect("Voyage Cargo Quantity",details,new Vector2(166,44),new Vector2(0,-94));
            Image background = field.gameObject.AddComponent<Image>(); background.color = new Color(.38f,.27f,.15f,.12f);
            InputField quantity = field.gameObject.AddComponent<InputField>();
            voyageCargoQuantity = quantity;
            Text inputText = CargoLabel("Voyage Cargo Quantity Text",field,new Vector2(150,36),Vector2.zero,23,string.Empty,ink);
            quantity.targetGraphic = background;
            quantity.textComponent = inputText;
            quantity.contentType = InputField.ContentType.IntegerNumber;
            quantity.lineType = InputField.LineType.SingleLine;
            quantity.characterLimit = 10;
            quantity.gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested =
                () => { if (viewCurrent()) CloseVoyageCargo(true); };
            Button decrease = CargoButton("Voyage Cargo Quantity Decrease",details,new Vector2(60,44),new Vector2(-121,-94),"−",viewCurrent, null);
            Button increase = CargoButton("Voyage Cargo Quantity Increase",details,new Vector2(60,44),new Vector2(121,-94),"+",viewCurrent, null);
            Button all = CargoButton("Voyage Cargo Discard All",details,new Vector2(264,38),new Vector2(0,-149),"全部数量",viewCurrent, null);
            Text warning = CargoLabel("Voyage Cargo Discard Warning",details,new Vector2(310,44),new Vector2(0,-193),18,
                "",Html("#6C2818"));
            Button confirm = CargoButton("Confirm Voyage Cargo Discard",details,new Vector2(310,52),new Vector2(0,-246),"确认舍弃",viewCurrent, null);
            voyageCargoBindSelection = (item, current) =>
            {
                int available = session.GetWarehouseCargoAmount(item.Key);
                var art = ItemIconCatalog.ForCargo(item.Key);
                icon.texture = art.Texture;
                icon.uvRect = art.Uv;
                icon.enabled = art.IsAvailable;
                UiImageAspect.Fit(icon, new Vector2(90f,90f));
                name.text = item.Name;
                amountLabel.text = "随船数量：" + available;
                unitVolume.text = "每件占用：" + item.UnitVolume.ToString("0.##", CultureInfo.InvariantCulture);
                description.text = item.Description;
                quantity.SetTextWithoutNotify(available > 0 ? "1" : "0");
                Action<long> setQuantity = value =>
                {
                    if (!current()) return;
                    quantity.text = Math.Max(0L,Math.Min(available,value)).ToString(CultureInfo.InvariantCulture);
                };
                BindVoyageCargoButton(decrease, current, () => setQuantity(ReadCargoQuantity(quantity.text)-1L));
                BindVoyageCargoButton(increase, current, () => setQuantity((long)ReadCargoQuantity(quantity.text)+1));
                BindVoyageCargoButton(all, current, () => setQuantity(available));
                bool consumed = false;
                BindVoyageCargoButton(confirm, current, () =>
                {
                    if (consumed || !Application.isPlaying || editorPreviewBuilding) return;
                    int amount = ReadCargoQuantity(quantity.text);
                    if (amount <= 0 || amount > available) return;
                    if (session.GetWarehouseCargoAmount(item.Key) != available)
                    {
                        BuildVoyageCargoGrid();
                        SelectVoyageCargoItem(null, true);
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
                    // An actual discard also updates the voyage forecast and endurance.
                    // Cargo stays in the voyage until its ordinary suspension/settlement save.
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
                quantity.onValueChanged = new InputField.OnChangeEvent();
                quantity.onValueChanged.AddListener(_ => refresh());
                refresh();
            };
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
            BindVoyageCargoButton(button, current, action);
            button.gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested =
                () => { if (current()) CloseVoyageCargo(true); };
            return button;
        }

        private static void BindVoyageCargoButton(Button button, Func<bool> current, Action action)
        {
            // Replace the event, not its listeners: previously queued event
            // objects must keep their expired selection guard and old action.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (!current() || button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return;
                action?.Invoke();
            });
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
            voyageCargoNotice = voyageCargoCapacity = null;
            voyageCargoList = voyageCargoSelectionRoot = null;
            voyageCargoSelectionHint = null;
            voyageCargoBasicTab = voyageCargoSpecialTab = null;
            voyageCargoCurrent = null;
            voyageCargoBindSelection = null;
            voyageCargoRows.Clear();
            ++voyageCargoGridRevision;
            ++voyageCargoSelectionRevision;
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
