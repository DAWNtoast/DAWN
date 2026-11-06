using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A reversible placement draft; only a complete selection can commit the card.</summary>
    public sealed class NavalCardChoiceOverlay : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private CoreVoyageSession session;
        private CoreBattleState battle;
        private CoreBattleCard card;
        private CoreNavalCardChoice choice;
        private CoreNavalCardPlayOptions options;
        private NavalTacticalBoardView board;
        private NavalFieldGraphic graphic;
        private Action<CoreNavalCardPlayOptions> confirm;
        private Action cancel;
        private Text resourceLabel;
        private Button confirmButton, rotate, reselectAnchor;
        private RectTransform controls;
        private readonly Button[] directions = new Button[4];
        private readonly List<NavalFieldVisualCell> visualCells = new List<NavalFieldVisualCell>();
        private int handIndex, hoverX, hoverY;
        private bool hasHover, submitted;
        private string notice;

        public CoreNavalCardPlayOptions Draft => options;
        public bool IsSelectingDirection => choice != null && choice.IsFieldPlacement && choice.RequiresDirection && options.HasAnchor;

        public void Configure(CoreVoyageSession model, NavalTacticalBoardView view, Font font, int index,
            CoreBattleCard data, CoreNavalCardChoice selection, Action<CoreNavalCardPlayOptions> onConfirm, Action onCancel)
        {
            session = model; battle = model.Battle; board = view; handIndex = index; card = data;
            choice = selection; confirm = onConfirm; cancel = onCancel;
            options = new CoreNavalCardPlayOptions { ResourceAmount = choice.ResourceMinimum,
                DirectionX = choice.IsFieldPlacement ? 0 : 1 };
            Image input = gameObject.AddComponent<Image>(); input.color = Color.clear; input.raycastTarget = true;
            RectTransform root = (RectTransform)transform;
            graphic = MakeRect("Naval choice shape", root, root.rect.size, Vector2.zero).gameObject.AddComponent<NavalFieldGraphic>();
            RectTransform panel = MakeRect("Naval choice controls", root, new Vector2(760f, 112f), new Vector2(0f, -302f));
            controls = panel;
            Image background = panel.gameObject.AddComponent<Image>(); background.color = new Color(.025f, .065f, .09f, .97f);
            Outline border = panel.gameObject.AddComponent<Outline>(); border.effectColor = new Color(.82f, .7f, .4f); border.effectDistance = Vector2.one * 2f;
            Text title = MakeText("Naval choice title", panel, font, 23, new Vector2(720f, 32f), new Vector2(0f, -28f));
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1f);
            title.text = card.Name + (choice.IsFieldPlacement ? " · 选择铺设位置" : " · 选择施放参数");
            if (!string.IsNullOrEmpty(choice.ResourceLabel) && choice.ResourceMaximum > choice.ResourceMinimum)
            {
                resourceLabel = MakeText("Naval selected resource", panel, font, 18, new Vector2(230f, 34f), Vector2.zero);
                MakeButton("Naval resource minus", panel, font, "−", new Vector2(42f, 38f), new Vector2(-145f, 0f),
                    () => { options.ResourceAmount = Mathf.Max(choice.ResourceMinimum, options.ResourceAmount - 1); Refresh(); });
                MakeButton("Naval resource plus", panel, font, "+", new Vector2(42f, 38f), new Vector2(145f, 0f),
                    () => { options.ResourceAmount = Mathf.Min(choice.ResourceMaximum, options.ResourceAmount + 1); Refresh(); });
            }
            if (choice.RequiresDirection)
            {
                string[] captions = { "世界向东", "世界向北", "世界向西", "世界向南" };
                int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
                for (int i = 0; i < 4; i++)
                {
                    int slot = i;
                    directions[i] = MakeButton("Naval field direction " + i, panel, font, captions[i], new Vector2(111f, 38f),
                        new Vector2(-177f + 118f * i, 0f), () => SelectDirection(dx[slot], dy[slot]));
                }
            }
            if (choice.IsFieldPlacement && choice.RequiresDirection)
                reselectAnchor = MakeActionButton("Reselect Naval Field Anchor", panel, font, "返回选择位置", 280f, ReturnToAnchorSelection);
            if (choice.AllowsVertical)
                rotate = MakeButton("Rotate Naval Field", panel, font, "横排 1×3", new Vector2(170f, 38f), new Vector2(270f, 0f),
                    () => { options.Vertical = !options.Vertical; Refresh(); });
            if (choice.RequiresSource)
                MakeButton("Reselect Naval Field Source", panel, font, "重选火格", new Vector2(150f, 38f), new Vector2(270f, 0f),
                    () => { options.HasSource = options.HasAnchor = false; hasHover = false; Refresh(); });
            MakeActionButton("Cancel Naval Card Choice", panel, font, "取消 · 不支付费用", 20f, () => cancel?.Invoke());
            confirmButton = MakeActionButton("Confirm Naval Card Choice", panel, font, "确认出牌", 280f, ConfirmSelection);
            confirmButton.gameObject.SetActive(!choice.IsFieldPlacement);
            Refresh();
        }

        private bool Current => session != null && ReferenceEquals(session.Battle, battle) && session.Phase == CoreDemoPhase.Battle &&
            battle.Stage != NavalBattleStage.Boarding && handIndex >= 0 && handIndex < battle.Hand.Count &&
            session.GetBattleCard(battle.Hand[handIndex]).Id == card.Id;

        private void Update()
        {
            if (submitted) return;
            if (!Current) { cancel?.Invoke(); return; }
            if (UiEscapeInput.IsPressed && UiEscapeInput.TryConsume()) BackOrCancel();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!Current || submitted || IsSelectingDirection) return;
            if (IsControlPoint(eventData) || !board.TryScreenToWorldCell(eventData.position, eventData.enterEventCamera, out VoyagePoint point))
            { ClearHover(); return; }
            int x = Mathf.RoundToInt(point.X / CoreVoyageSession.NavalGridCellMeters), y = Mathf.RoundToInt(point.Y / CoreVoyageSession.NavalGridCellMeters);
            HoverCell(x, y);
        }

        public void HoverCell(int x, int y)
        {
            if (!Current || submitted || IsSelectingDirection) return;
            if (hasHover && hoverX == x && hoverY == y) return;
            hoverX = x; hoverY = y; hasHover = true;
            RefreshGraphic();
        }

        public void OnPointerExit(PointerEventData eventData) => ClearHover();

        private void ClearHover()
        {
            if (!hasHover) return;
            hasHover = false; RefreshGraphic();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (submitted) return;
            if (eventData.button == PointerEventData.InputButton.Right) { BackOrCancel(); return; }
            if (eventData.button != PointerEventData.InputButton.Left || !Current || IsControlPoint(eventData) ||
                !board.TryScreenToWorldCell(eventData.position, eventData.pressEventCamera, out VoyagePoint point)) return;
            SelectCell(Mathf.RoundToInt(point.X / CoreVoyageSession.NavalGridCellMeters), Mathf.RoundToInt(point.Y / CoreVoyageSession.NavalGridCellMeters));
        }

        public void SelectCell(int x, int y)
        {
            if (!Current || submitted || IsSelectingDirection) return;
            if (choice.IsFieldPlacement)
            {
                notice = FieldAnchorUnavailableReason(x, y);
                if (!string.IsNullOrEmpty(notice)) { HoverCell(x, y); Refresh(); return; }
                options.AnchorX = x; options.AnchorY = y; options.HasAnchor = true;
                options.DirectionX = options.DirectionY = 0;
                hasHover = false; Refresh();
                if (!choice.RequiresDirection) ConfirmSelection();
                return;
            }
            if (choice.RequiresSource && !options.HasSource)
            { options.SourceX = x; options.SourceY = y; options.HasSource = true; }
            else if (choice.RequiresAnchor || choice.RequiresFieldInstance)
            { options.AnchorX = x; options.AnchorY = y; options.HasAnchor = true; }
            hasHover = false; notice = null; Refresh();
        }

        private bool IsControlPoint(PointerEventData data) => controls != null &&
            RectTransformUtility.RectangleContainsScreenPoint(controls, data.position, data.enterEventCamera);

        public void SelectDirection(int x, int y)
        {
            if (!Current || submitted || !choice.RequiresDirection || Math.Abs((long)x) + Math.Abs((long)y) != 1) return;
            if (choice.IsFieldPlacement && !IsSelectingDirection) return;
            options.DirectionX = x; options.DirectionY = y; notice = null; Refresh();
            if (choice.IsFieldPlacement) ConfirmSelection();
        }

        public void ReturnToAnchorSelection()
        {
            if (!Current || submitted || !IsSelectingDirection) return;
            options.HasAnchor = false; options.DirectionX = options.DirectionY = 0;
            hasHover = false; notice = null; Refresh();
        }

        private void BackOrCancel()
        {
            if (IsSelectingDirection) ReturnToAnchorSelection();
            else cancel?.Invoke();
        }

        public void ConfirmSelection()
        {
            if (submitted) return;
            if (!Current) { cancel?.Invoke(); return; }
            notice = session.GetNavalChoiceUnavailableReason(handIndex, options);
            if (!string.IsNullOrEmpty(notice)) { Refresh(); return; }
            submitted = true;
            confirm?.Invoke(Copy(options));
        }

        private void Refresh()
        {
            if (!Current) return;
            string reason = session.GetNavalChoiceUnavailableReason(handIndex, options);
            confirmButton.interactable = string.IsNullOrEmpty(reason);
            if (resourceLabel != null) resourceLabel.text = choice.ResourceLabel + " " + options.ResourceAmount + "（" + choice.ResourceMinimum + "–" + choice.ResourceMaximum + "）";
            if (rotate != null) rotate.GetComponentInChildren<Text>().text = options.Vertical ? "竖排 3×1" : "横排 1×3";
            if (reselectAnchor != null) reselectAnchor.gameObject.SetActive(IsSelectingDirection);
            int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
            for (int i = 0; i < directions.Length; i++)
                if (directions[i] != null)
                {
                    directions[i].gameObject.SetActive(!choice.IsFieldPlacement || IsSelectingDirection);
                    WoodButtonSkin.SetSelected(directions[i], options.DirectionX == dx[i] && options.DirectionY == dy[i]);
                }
            bool showParameters = resourceLabel != null || rotate != null || choice.RequiresSource ||
                (choice.RequiresDirection && (!choice.IsFieldPlacement || IsSelectingDirection));
            float height = showParameters ? 170f : 112f;
            controls.sizeDelta = new Vector2(760f, height);
            controls.anchoredPosition = new Vector2(0f, -358f + height * .5f);
            RefreshGraphic();
        }

        private void RefreshGraphic()
        {
            if (graphic == null || !Current) return;
            visualCells.Clear();
            int centerX = Mathf.RoundToInt(battle.PlayerPositionMeters.X / 100f), centerY = Mathf.RoundToInt(battle.PlayerPositionMeters.Y / 100f);
            if (!IsSelectingDirection && (choice.RequiresAnchor || choice.RequiresSource || choice.RequiresFieldInstance))
                for (int x = centerX - 4; x <= centerX + 4; x++)
                    for (int y = centerY - 4; y <= centerY + 4; y++)
                    {
                        CoreNavalCardPlayOptions candidate = Copy(options);
                        if (choice.RequiresSource && !options.HasSource)
                        {
                            bool ownFire = false;
                            foreach (CoreNavalFieldCell field in battle.NavalFields.Cells)
                                if (field.X == x && field.Y == y && field.PlayerOwned && field.Kind == CoreNavalFieldKind.Fire) ownFire = true;
                            if (!ownFire || Math.Abs(x - centerX) + Math.Abs(y - centerY) > 4) continue;
                        }
                        else
                        {
                            candidate.AnchorX = x; candidate.AnchorY = y; candidate.HasAnchor = true;
                            string reason = choice.IsFieldPlacement ? FieldAnchorUnavailableReason(x, y) : session.GetNavalChoiceUnavailableReason(handIndex, candidate);
                            if (!string.IsNullOrEmpty(reason)) continue;
                        }
                        AddTile(x, y, new Color(.2f, .95f, .82f), .09f, false);
                    }
            if (options.HasSource) AddTile(options.SourceX, options.SourceY, new Color(1f, .85f, .25f), .3f, false);
            if (options.HasAnchor) AddShape(options.AnchorX, options.AnchorY, false);
            if (hasHover && (choice.RequiresAnchor || choice.RequiresSource || choice.RequiresFieldInstance)) AddShape(hoverX, hoverY, true);
            graphic.Configure(board, visualCells);
        }

        private void AddShape(int x, int y, bool hovering)
        {
            if (choice.IsFieldPlacement)
            {
                bool invalid = !string.IsNullOrEmpty(FieldAnchorUnavailableReason(x, y));
                foreach (VoyagePoint point in session.GetNavalFieldShape(choice.FieldKind, x, y, false))
                    AddTile(Mathf.RoundToInt(point.X / CoreVoyageSession.NavalGridCellMeters),
                        Mathf.RoundToInt(point.Y / CoreVoyageSession.NavalGridCellMeters),
                        new Color(1f, .86f, .33f), hovering ? .16f : .35f, invalid, true);
                return;
            }
            if (choice.RequiresFieldInstance)
            {
                int instance = -1;
                foreach (CoreNavalFieldCell field in battle.NavalFields.Cells)
                    if (field.X == x && field.Y == y && field.PlayerOwned && field.Kind == CoreNavalFieldKind.Thunder) instance = field.InstanceId;
                if (instance >= 0)
                {
                    foreach (CoreNavalFieldCell field in battle.NavalFields.Cells)
                        if (field.InstanceId == instance) AddTile(field.X, field.Y, new Color(1f, .85f, .25f), hovering ? .18f : .35f, !InRange(field.X, field.Y));
                    return;
                }
            }
            int width = Math.Max(1, choice.ShapeWidth), height = Math.Max(1, choice.ShapeHeight);
            if (choice.AllowsVertical && options.Vertical) { int swap = width; width = height; height = swap; }
            if (choice.RequiresSource) width = height = 1;
            for (int ix = 0; ix < width; ix++)
                for (int iy = 0; iy < height; iy++)
                    AddTile(x + ix, y + iy, new Color(1f, .86f, .33f), hovering ? .16f : .35f, !InRange(x + ix, y + iy), true);
        }

        private bool InRange(int x, int y) => CoreVoyageSession.NavalGridDistance(battle.PlayerPositionMeters, new VoyagePoint(x * 100f, y * 100f)) <= 4;

        private string FieldAnchorUnavailableReason(int x, int y)
        {
            CoreNavalCardPlayOptions candidate = Copy(options);
            candidate.AnchorX = x; candidate.AnchorY = y; candidate.HasAnchor = true;
            // Direction is selected only after the anchor. A temporary cardinal direction
            // validates costs and the footprint without choosing one in the actual draft.
            candidate.DirectionX = 1; candidate.DirectionY = 0;
            return session.GetNavalChoiceUnavailableReason(handIndex, candidate);
        }
        private void AddTile(int x, int y, Color tint, float alpha, bool invalid, bool showDirection = false)
        {
            if (invalid) tint = new Color(1f, .22f, .18f);
            Color fill = tint; fill.a = alpha;
            visualCells.Add(new NavalFieldVisualCell(new VoyagePoint(x * 100f, y * 100f), CoreNavalFieldKind.Tide,
                fill, tint, false, choice.RequiresDirection && showDirection ? options.DirectionX : 0,
                choice.RequiresDirection && showDirection ? options.DirectionY : 0, invalid));
        }

        private static CoreNavalCardPlayOptions Copy(CoreNavalCardPlayOptions original) => new CoreNavalCardPlayOptions
        {
            AnchorX = original.AnchorX, AnchorY = original.AnchorY, HasAnchor = original.HasAnchor,
            SourceX = original.SourceX, SourceY = original.SourceY, HasSource = original.HasSource,
            Vertical = original.Vertical, DirectionX = original.DirectionX, DirectionY = original.DirectionY,
            ResourceAmount = original.ResourceAmount, FieldInstanceId = original.FieldInstanceId
        };

        internal static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        internal static Text MakeText(string name, Transform parent, Font font, int size, Vector2 bounds, Vector2 position)
        {
            Text label = MakeRect(name, parent, bounds, position).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(.98f, .92f, .77f); label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
        private static Button MakeActionButton(string name, Transform parent, Font font, string caption, float rightInset, Action action)
        {
            Button button = MakeButton(name, parent, font, caption, new Vector2(240f, 40f), new Vector2(-rightInset - 120f, 36f), action);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            return button;
        }

        internal static Button MakeButton(string name, Transform parent, Font font, string caption, Vector2 size, Vector2 position, Action action)
        {
            RectTransform rect = MakeRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            MakeText(name + " label", rect, font, 18, size - Vector2.one * 4f, Vector2.zero).text = caption;
            WoodButtonSkin.ApplyAction(button, WoodButtonSkin.DefaultRoleFor(name));
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }
    }
}
