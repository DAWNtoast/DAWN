using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A reversible selection draft. The only state-changing callback is Confirm.</summary>
    public sealed class NavalCardChoiceOverlay : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler
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
        private Text instruction, status, resourceLabel;
        private Button confirmButton, rotate;
        private RectTransform controls;
        private readonly Button[] directions = new Button[4];
        private readonly List<NavalFieldVisualCell> visualCells = new List<NavalFieldVisualCell>();
        private int handIndex, hoverX, hoverY;
        private bool hasHover;
        private string notice;

        public CoreNavalCardPlayOptions Draft => options;

        public void Configure(CoreVoyageSession model, NavalTacticalBoardView view, Font font, int index,
            CoreBattleCard data, CoreNavalCardChoice selection, Action<CoreNavalCardPlayOptions> onConfirm, Action onCancel)
        {
            session = model; battle = model.Battle; board = view; handIndex = index; card = data;
            choice = selection; confirm = onConfirm; cancel = onCancel;
            options = new CoreNavalCardPlayOptions { ResourceAmount = choice.ResourceMinimum, DirectionX = 1 };
            Image input = gameObject.AddComponent<Image>(); input.color = Color.clear; input.raycastTarget = true;
            RectTransform root = (RectTransform)transform;
            graphic = MakeRect("Naval choice shape", root, root.rect.size, Vector2.zero).gameObject.AddComponent<NavalFieldGraphic>();
            RectTransform panel = MakeRect("Naval choice controls", root, new Vector2(1060f, 238f), new Vector2(0f, -239f));
            controls = panel;
            Image background = panel.gameObject.AddComponent<Image>(); background.color = new Color(.025f, .065f, .09f, .97f);
            Outline border = panel.gameObject.AddComponent<Outline>(); border.effectColor = new Color(.82f, .7f, .4f); border.effectDistance = Vector2.one * 2f;
            MakeText("Naval choice title", panel, font, 23, new Vector2(1000f, 32f), new Vector2(0f, 94f)).text = card.Name + " · 选择施放参数";
            instruction = MakeText("Naval choice instruction", panel, font, 18, new Vector2(1000f, 28f), new Vector2(0f, 59f));
            status = MakeText("Naval choice validation", panel, font, 18, new Vector2(1000f, 42f), new Vector2(0f, 17f));
            if (!string.IsNullOrEmpty(choice.ResourceLabel) && choice.ResourceMaximum > choice.ResourceMinimum)
            {
                resourceLabel = MakeText("Naval selected resource", panel, font, 18, new Vector2(230f, 34f), new Vector2(-295f, -34f));
                MakeButton("Naval resource minus", panel, font, "−", new Vector2(42f, 38f), new Vector2(-440f, -34f),
                    () => { options.ResourceAmount = Mathf.Max(choice.ResourceMinimum, options.ResourceAmount - 1); Refresh(); });
                MakeButton("Naval resource plus", panel, font, "+", new Vector2(42f, 38f), new Vector2(-150f, -34f),
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
                        new Vector2(15f + 118f * i, -34f), () => { options.DirectionX = dx[slot]; options.DirectionY = dy[slot]; Refresh(); });
                }
            }
            if (choice.AllowsVertical)
                rotate = MakeButton("Rotate Naval Field", panel, font, "横排 1×3", new Vector2(170f, 38f), new Vector2(270f, -34f),
                    () => { options.Vertical = !options.Vertical; Refresh(); });
            if (choice.RequiresSource)
                MakeButton("Reselect Naval Field Source", panel, font, "重选火格", new Vector2(150f, 38f), new Vector2(270f, -34f),
                    () => { options.HasSource = options.HasAnchor = false; hasHover = false; Refresh(); });
            MakeButton("Cancel Naval Card Choice", panel, font, "取消 · 不支付费用", new Vector2(240f, 43f), new Vector2(-250f, -88f), () => cancel?.Invoke());
            confirmButton = MakeButton("Confirm Naval Card Choice", panel, font, "确认出牌", new Vector2(240f, 43f), new Vector2(250f, -88f), ConfirmSelection);
            Refresh();
        }

        private bool Current => session != null && ReferenceEquals(session.Battle, battle) && session.Phase == CoreDemoPhase.Battle &&
            battle.Stage != NavalBattleStage.Boarding && handIndex >= 0 && handIndex < battle.Hand.Count &&
            session.GetBattleCard(battle.Hand[handIndex]).Id == card.Id;

        private void Update()
        {
            if (!Current || (UiEscapeInput.IsPressed && UiEscapeInput.TryConsume())) { cancel?.Invoke(); return; }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!Current || IsControlPoint(eventData) || !board.TryScreenToWorldCell(eventData.position, eventData.enterEventCamera, out VoyagePoint point)) return;
            int x = Mathf.RoundToInt(point.X / CoreVoyageSession.NavalGridCellMeters), y = Mathf.RoundToInt(point.Y / CoreVoyageSession.NavalGridCellMeters);
            if (hasHover && hoverX == x && hoverY == y) return;
            hoverX = x; hoverY = y; hasHover = true;
            RefreshGraphic();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) { cancel?.Invoke(); return; }
            if (eventData.button != PointerEventData.InputButton.Left || !Current || IsControlPoint(eventData) ||
                !board.TryScreenToWorldCell(eventData.position, eventData.pressEventCamera, out VoyagePoint point)) return;
            SelectCell(Mathf.RoundToInt(point.X / CoreVoyageSession.NavalGridCellMeters), Mathf.RoundToInt(point.Y / CoreVoyageSession.NavalGridCellMeters));
        }

        public void SelectCell(int x, int y)
        {
            if (!Current) return;
            if (choice.RequiresSource && !options.HasSource)
            { options.SourceX = x; options.SourceY = y; options.HasSource = true; }
            else if (choice.RequiresAnchor || choice.RequiresFieldInstance)
            { options.AnchorX = x; options.AnchorY = y; options.HasAnchor = true; }
            hasHover = false; notice = null; Refresh();
        }

        private bool IsControlPoint(PointerEventData data) => controls != null &&
            RectTransformUtility.RectangleContainsScreenPoint(controls, data.position, data.enterEventCamera);

        public void ConfirmSelection()
        {
            if (!Current) { cancel?.Invoke(); return; }
            notice = session.GetNavalChoiceUnavailableReason(handIndex, options);
            if (!string.IsNullOrEmpty(notice)) { Refresh(); return; }
            confirm?.Invoke(options);
        }

        private void Refresh()
        {
            if (!Current) return;
            string reason = session.GetNavalChoiceUnavailableReason(handIndex, options);
            confirmButton.interactable = string.IsNullOrEmpty(reason);
            instruction.text = choice.RequiresSource && !options.HasSource ? "先点击己方火格，再点击相邻空海格" :
                choice.RequiresFieldInstance ? "点击一处己方雷栅 · 只收束该实例仍存的格子" :
                choice.RequiresAnchor ? "点击海格选定区域 · 青格可放置 · 红叉格无效 · 全部格子须在横纵相加 4 格内" :
                "选择资源数量与方向；确认后支付费用并结算";
            status.text = !string.IsNullOrEmpty(notice) ? notice : !string.IsNullOrEmpty(reason) ? reason :
                (options.HasAnchor ? "已选世界格 (" + options.AnchorX + ", " + options.AnchorY + ") · " : string.Empty) + "选择合法，可以确认";
            status.color = string.IsNullOrEmpty(reason) ? new Color(.5f, 1f, .8f) : new Color(1f, .72f, .49f);
            if (resourceLabel != null) resourceLabel.text = choice.ResourceLabel + " " + options.ResourceAmount + "（" + choice.ResourceMinimum + "–" + choice.ResourceMaximum + "）";
            if (rotate != null) rotate.GetComponentInChildren<Text>().text = options.Vertical ? "竖排 3×1" : "横排 1×3";
            int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
            for (int i = 0; i < directions.Length; i++)
                if (directions[i] != null) directions[i].image.color = options.DirectionX == dx[i] && options.DirectionY == dy[i]
                    ? new Color(.23f, .48f, .50f) : new Color(.08f, .17f, .21f);
            RefreshGraphic();
        }

        private void RefreshGraphic()
        {
            if (graphic == null || !Current) return;
            visualCells.Clear();
            int centerX = Mathf.RoundToInt(battle.PlayerPositionMeters.X / 100f), centerY = Mathf.RoundToInt(battle.PlayerPositionMeters.Y / 100f);
            if (choice.RequiresAnchor || choice.RequiresSource || choice.RequiresFieldInstance)
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
                            if (!string.IsNullOrEmpty(session.GetNavalChoiceUnavailableReason(handIndex, candidate))) continue;
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
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Key == "T06")
            { width = options.DirectionX == 0 ? 3 : 1; height = options.DirectionX == 0 ? 1 : 3; }
            if (choice.RequiresSource) width = height = 1;
            for (int ix = 0; ix < width; ix++)
                for (int iy = 0; iy < height; iy++)
                    AddTile(x + ix, y + iy, new Color(1f, .86f, .33f), hovering ? .16f : .35f, !InRange(x + ix, y + iy), true);
        }

        private bool InRange(int x, int y) => CoreVoyageSession.NavalGridDistance(battle.PlayerPositionMeters, new VoyagePoint(x * 100f, y * 100f)) <= 4;
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
        internal static Button MakeButton(string name, Transform parent, Font font, string caption, Vector2 size, Vector2 position, Action action)
        {
            RectTransform rect = MakeRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.08f, .17f, .21f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            MakeText(name + " label", rect, font, 18, size - Vector2.one * 4f, Vector2.zero).text = caption;
            button.onClick.AddListener(() => action?.Invoke()); return button;
        }
    }
}
