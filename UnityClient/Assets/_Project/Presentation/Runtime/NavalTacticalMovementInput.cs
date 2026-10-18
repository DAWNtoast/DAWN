using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Records the cells crossed by the pointer; reachability never substitutes an automatic route.</summary>
    [DisallowMultipleComponent]
    public sealed class NavalTacticalMovementInput : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private CoreVoyageSession session;
        private CoreBattleState battle;
        private NavalTacticalBoardView board;
        private Func<bool> inputBlocked;
        private Action commit;
        private Action<float> chooseHeading;
        private NavalTacticalRouteGraphic route;
        private RectTransform controls, headingControls;
        private Text instruction;
        private Button sail, clear;
        private readonly Button[] headings = new Button[3];
        private readonly float[] headingValues = new float[3];
        private string message;
        private int lastPoints = -1, lastSteps = -1, lastRevision = -1;
        private bool lastBlocked, lastAwaiting, initialized;
        private int lastContext, lastPresentation;
        private bool reachDirty = true, dragArmed, dragFromShip, suppressClick, dragNeedsAnchor;
        private int dragPointer;
        private VoyagePoint dragWorld;
        private readonly List<VoyagePoint> reachable = new List<VoyagePoint>();
        private static readonly VoyagePoint[] NoCells = Array.Empty<VoyagePoint>();

        public bool IsShipSelected { get; private set; }
        public bool IsDraggingRoute { get; private set; }
        public IReadOnlyList<VoyagePoint> ReachableCells => reachable.AsReadOnly();
        public bool IsMovementRangeVisible => CanPlan && IsShipSelected && route != null && route.gameObject.activeInHierarchy;
        private NavalAttackRangePreview AttackRange => GetComponent<NavalAttackRangePreview>();

        public void Configure(CoreVoyageSession model, NavalTacticalBoardView view, RectTransform root, Font font,
            Func<bool> blocked, Action onCommit, Action<float> onHeading)
        {
            session = model; battle = model.Battle; board = view; inputBlocked = blocked; commit = onCommit; chooseHeading = onHeading;
            var routeRect = MakeRect("Naval selected route", (RectTransform)transform, ((RectTransform)transform).rect.size, Vector2.zero);
            routeRect.anchorMin = Vector2.zero; routeRect.anchorMax = Vector2.one;
            routeRect.offsetMin = routeRect.offsetMax = Vector2.zero;
            route = routeRect.gameObject.AddComponent<NavalTacticalRouteGraphic>();
            route.raycastTarget = false;
            controls = MakeRect("Naval movement controls", root, new Vector2(230f, 44f), new Vector2(735f, -247f));
            instruction = MakeText("Naval movement instruction", controls, font, 16, new Vector2(680f, 36f), new Vector2(-735f, 43f));
            sail = MakeButton("Commit Naval Movement", controls, "确定航行", font, new Vector2(111f, 44f), new Vector2(-59.5f, 0f));
            clear = MakeButton("Clear Naval Movement", controls, "清空路线", font, new Vector2(111f, 44f), new Vector2(59.5f, 0f));
            sail.onClick.AddListener(CommitSelection);
            clear.onClick.AddListener(() => { if (CanPlan) { session.ClearNavalMovementPath(); message = null; RefreshNow(); } });
            headingControls = MakeRect("Naval arrival headings", root, Vector2.zero, Vector2.zero);
            for (int i = 0; i < headings.Length; i++)
            {
                int slot = i;
                headings[i] = MakeButton("Naval Arrival Heading " + i, headingControls, string.Empty, font, new Vector2(92f, 44f), Vector2.zero);
                headings[i].onClick.AddListener(() => ChooseHeading(headingValues[slot]));
            }
            RefreshNow();
        }

        private bool Blocked => inputBlocked == null || inputBlocked() || board == null || board.IsAnimating;
        private bool CurrentBattle => session != null && ReferenceEquals(session.Battle, battle) && session.NavalActionMovementActive;
        private bool CanPlan => isActiveAndEnabled && CurrentBattle && !Blocked && !battle.NavalAwaitingHeadingSelection;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (suppressClick) return;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (CanPlan && session.UndoNavalMovementStep()) { message = null; RefreshNow(); }
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left || !CanPlan) return;
            if (!board.TryScreenToWorldCell(eventData.position, eventData.pressEventCamera, out VoyagePoint cell)) return;
            if (SameCell(cell, battle.PlayerPositionMeters)) HandleShipClick(eventData);
            else
            {
                message = "从我舰或航线末端拖动规划，松开保留预览";
                RefreshNow();
            }
        }

        public void HandleShipClick(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left || suppressClick) return;
            RefreshNow();
            if (!CanPlan) { AttackRange?.ToggleShipRange(); return; }
            SetShipSelected(!IsShipSelected);
            RefreshNow();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = true;

        public void OnPointerDown(PointerEventData eventData)
        {
            suppressClick = false;
            EndGesture();
            RefreshNow();
            if (eventData.button != PointerEventData.InputButton.Left || !CanPlan ||
                !board.TryScreenToWorldPoint(eventData.position, eventData.pressEventCamera, out dragWorld)) return;
            VoyagePoint cell = CellAt(dragWorld);
            var path = session.NavalMovementPath;
            dragFromShip = SameCell(cell, battle.PlayerPositionMeters);
            dragArmed = dragFromShip || path.Count > 1 && SameCell(cell, path[path.Count - 1].PositionMeters);
            dragPointer = eventData.pointerId;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!dragArmed || eventData.pointerId != dragPointer || !CanPlan) return;
            suppressClick = true;
            IsDraggingRoute = true;
            dragNeedsAnchor = false;
            SetShipSelected(true);
            if (dragFromShip) session.ClearNavalMovementPath();
            message = null;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDraggingRoute || eventData.pointerId != dragPointer) return;
            if (!CanPlan) { EndGesture(); RefreshNow(); return; }
            if (!IsRoutePointerSurface(eventData) ||
                !board.TryScreenToWorldPoint(eventData.position, eventData.pressEventCamera, out VoyagePoint world))
            {
                dragNeedsAnchor = true;
                dragArmed = false;
                message = "回到已选航线后继续拖动";
                RefreshNow();
                return;
            }
            if (!dragArmed)
            {
                // Returning from a panel or outside the chart must not draw a
                // hidden segment across the area the player could not see.
                if (!TryReturnToPath(CellAt(world))) { dragWorld = world; return; }
                dragArmed = true;
                dragWorld = world;
            }
            TracePointerSegment(dragWorld, world);
            dragWorld = world;
            RefreshNow();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.pointerId != dragPointer) return;
            if (IsDraggingRoute) OnDrag(eventData);
            EndGesture();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != dragPointer) return;
            if (IsDraggingRoute) OnDrag(eventData);
            EndGesture();
        }

        private void EndGesture() { dragArmed = false; IsDraggingRoute = false; dragNeedsAnchor = false; }

        private void OnEnable() { initialized = false; reachDirty = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) EndGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) EndGesture(); }

        private void OnDisable()
        {
            EndGesture();
            IsShipSelected = false;
            reachable.Clear();
            AttackRange?.RefreshNow();
            if (route != null) route.gameObject.SetActive(false);
            if (headingControls != null) headingControls.gameObject.SetActive(false);
            if (controls != null) controls.gameObject.SetActive(false);
        }

        private bool IsRoutePointerSurface(PointerEventData data)
        {
            GameObject hit = data.pointerCurrentRaycast.gameObject;
            return hit == null || hit == gameObject || hit.transform.IsChildOf(transform) ||
                hit.GetComponent<NavalAttackRangeShipClick>() != null;
        }

        private void SetShipSelected(bool selected, bool updateAttackPin = true)
        {
            IsShipSelected = selected;
            if (!selected) reachable.Clear();
            else reachDirty = true;
            if (updateAttackPin) AttackRange?.SetShipRangePinned(selected);
            else AttackRange?.RefreshNow();
        }

        private static VoyagePoint CellAt(VoyagePoint point)
        {
            const float size = CoreVoyageSession.NavalGridCellMeters;
            return new VoyagePoint(Mathf.Floor(point.X / size + .5f) * size, Mathf.Floor(point.Y / size + .5f) * size);
        }

        private static bool SameCell(VoyagePoint left, VoyagePoint right) =>
            Mathf.Abs(left.X - right.X) < .01f && Mathf.Abs(left.Y - right.Y) < .01f;

        private void TracePointerSegment(VoyagePoint from, VoyagePoint to)
        {
            // Amanatides-Woo traversal in world-grid space. Enumerate only cells
            // crossed by this pointer segment, including every fast skipped frame.
            const double size = CoreVoyageSession.NavalGridCellMeters;
            if (dragNeedsAnchor) TryReturnToPath(CellAt(from));
            double x = from.X / size + .5, y = from.Y / size + .5;
            double dx = (to.X - from.X) / size, dy = (to.Y - from.Y) / size;
            int cellX = (int)Math.Floor(x), cellY = (int)Math.Floor(y);
            int endX = (int)Math.Floor(to.X / size + .5), endY = (int)Math.Floor(to.Y / size + .5);
            int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
            double tx = stepX == 0 ? double.PositiveInfinity : ((stepX > 0 ? cellX + 1 : cellX) - x) / dx;
            double ty = stepY == 0 ? double.PositiveInfinity : ((stepY > 0 ? cellY + 1 : cellY) - y) / dy;
            double deltaX = stepX == 0 ? double.PositiveInfinity : Math.Abs(1d / dx);
            double deltaY = stepY == 0 ? double.PositiveInfinity : Math.Abs(1d / dy);
            long crossings = Math.Abs((long)endX - cellX) + Math.Abs((long)endY - cellY);
            for (long crossed = 0; crossed < crossings && (cellX != endX || cellY != endY); crossed++)
            {
                if (Math.Abs(tx - ty) < .000001d)
                {
                    // A corner has no unique cardinal cell. Do not invent an L
                    // or steer around an enemy on behalf of the player.
                    if (!TryReturnToPath(CellAt(to)))
                    {
                        message = "请沿格子转弯，不要从格角斜穿；可拖回已选航线修正";
                        dragNeedsAnchor = true;
                    }
                    return;
                }
                if (tx < ty) { cellX += stepX; tx += deltaX; }
                else { cellY += stepY; ty += deltaY; }
                VisitDraggedCell(new VoyagePoint((float)(cellX * size), (float)(cellY * size)));
            }
        }

        private void VisitDraggedCell(VoyagePoint cell)
        {
            if (TryReturnToPath(cell)) return;
            if (dragNeedsAnchor) return;
            if (session.TryAppendNavalMovementStep(cell, out string reason)) message = null;
            else
            {
                message = reason + "；拖回已选航线可修正";
                dragNeedsAnchor = true;
            }
        }

        private bool TryReturnToPath(VoyagePoint cell)
        {
            var path = session.NavalMovementPath;
            for (int index = path.Count - 1; index >= 0; index--)
                if (SameCell(cell, path[index].PositionMeters))
                {
                    if (index < path.Count - 1 && !session.TryTruncateNavalMovementPath(index, out _)) return false;
                    dragNeedsAnchor = false;
                    message = null;
                    return true;
                }
            return false;
        }

        public bool SelectCell(VoyagePoint cell)
        {
            if (!CanPlan) return false;
            bool accepted = session.TryAppendNavalMovementStep(cell, out string reason);
            message = accepted ? null : reason;
            if (accepted) SetShipSelected(true);
            RefreshNow();
            return accepted;
        }

        public void CommitSelection()
        {
            if (!CanPlan || session.NavalMovementPlannedSteps == 0) return;
            EndGesture();
            SetShipSelected(false, false);
            message = null;
            commit?.Invoke();
            RefreshNow();
        }

        public void ChooseHeading(float angle)
        {
            if (!isActiveAndEnabled || !CurrentBattle || Blocked || !battle.NavalAwaitingHeadingSelection) return;
            bool allowed = false;
            foreach (float value in session.GetNavalArrivalHeadings())
                if (Mathf.Abs(Mathf.DeltaAngle(value, angle)) < .01f) allowed = true;
            if (!allowed) return;
            message = null;
            chooseHeading?.Invoke(angle);
            RefreshNow();
        }

        private void Update()
        {
            if (session == null || board == null) return;
            if (!CurrentBattle)
            {
                EndGesture();
                IsShipSelected = false;
                reachable.Clear();
                controls.gameObject.SetActive(false);
                headingControls.gameObject.SetActive(false);
                route.gameObject.SetActive(false);
                return;
            }
            if (!initialized || lastPoints != battle.NavalActionPoints || lastSteps != session.NavalMovementPlannedSteps ||
                lastRevision != battle.TacticalRevision || lastBlocked != Blocked || lastAwaiting != battle.NavalAwaitingHeadingSelection ||
                lastContext != ContextSignature() || lastPresentation != PresentationSignature())
            { message = null; RefreshNow(); }
        }

        private int ContextSignature()
        {
            unchecked
            {
                int value = battle.Turn;
                value = value * 31 + battle.PhaseTurn;
                value = value * 31 + (int)battle.Stage;
                value = value * 31 + battle.CardsPlayedThisTurn;
                value = value * 31 + battle.NavalActionPoints;
                value = value * 31 + battle.NavalArrivalActionPoints;
                value = value * 31 + battle.TacticalRevision;
                value = value * 31 + battle.PlayerPositionMeters.X.GetHashCode();
                value = value * 31 + battle.PlayerPositionMeters.Y.GetHashCode();
                value = value * 31 + battle.PlayerHeadingDegrees.GetHashCode();
                value = value * 31 + battle.EnemyPositionMeters.X.GetHashCode();
                value = value * 31 + battle.EnemyPositionMeters.Y.GetHashCode();
                value = value * 31 + battle.EnemyHeadingDegrees.GetHashCode();
                return value * 31 + (int)session.GuidedBossStep;
            }
        }

        private int PresentationSignature()
        {
            unchecked
            {
                int value = ((RectTransform)board.transform).rect.GetHashCode();
                value = value * 31 + ((RectTransform)board.transform).anchoredPosition.GetHashCode();
                value = value * 31 + board.CameraRotationDegrees.GetHashCode();
                value = value * 31 + board.PlayerScreenPosition.GetHashCode();
                return value * 31 + board.CellSizePixels.GetHashCode();
            }
        }

        public void RefreshNow()
        {
            if (!isActiveAndEnabled || !CurrentBattle || board == null || controls == null) return;
            int context = ContextSignature();
            if (initialized && context != lastContext)
            {
                EndGesture();
                SetShipSelected(false, false);
                if (!battle.NavalAwaitingHeadingSelection) session.ClearNavalMovementPath();
                reachDirty = true;
                message = null;
            }
            lastContext = context;
            lastPresentation = PresentationSignature();
            lastPoints = battle.NavalActionPoints;
            lastSteps = session.NavalMovementPlannedSteps;
            lastRevision = battle.TacticalRevision;
            lastBlocked = Blocked;
            lastAwaiting = battle.NavalAwaitingHeadingSelection;
            if (lastBlocked || lastAwaiting) EndGesture();
            initialized = true;
            controls.gameObject.SetActive(true);
            bool planning = !lastBlocked && !lastAwaiting;
            instruction.text = !string.IsNullOrEmpty(message) ? message :
                lastBlocked ? board.IsAnimating ? "航行与行动结算中…" : "请完成当前操作" : lastAwaiting ?
                (battle.NavalArrivalActionPoints > 0
                    ? "到达剩余 " + battle.NavalArrivalActionPoints + " 点 · 选向停船清空余量，或使用可用接触动作"
                    : "选择船只周围的到达朝向 · 不能直接掉头") :
                lastPoints == 0 ? "行动点已用尽 · 使用机动卡获得行动点" :
                !IsShipSelected && lastSteps == 0 ? "点击我舰查看可达海域 · 按住我舰拖动航线" :
                "拖动规划，松开预览 · 已选 " + lastSteps + " / " + lastPoints + " 格";
            instruction.gameObject.SetActive(IsShipSelected || lastAwaiting || !string.IsNullOrEmpty(message));
            sail.interactable = clear.interactable = planning && lastSteps > 0;
            route.gameObject.SetActive(planning && (IsShipSelected || lastSteps > 0));
            if (planning)
            {
                if (IsShipSelected && reachDirty)
                {
                    reachable.Clear();
                    reachable.AddRange(session.GetNavalReachableCells());
                    reachDirty = false;
                }
                route.Configure(board, session.NavalMovementPath,
                    IsShipSelected ? session.GetNavalLegalNextSteps() : NoCells, reachable);
            }
            // Selection updates the attack pin before the reachable cells are rebuilt.
            // Reapply its visual exclusion after the movement layer is final for this frame.
            AttackRange?.RefreshNow();
            headingControls.gameObject.SetActive(lastAwaiting && !lastBlocked);
            if (!lastAwaiting || lastBlocked) return;
            var options = session.GetNavalArrivalHeadings();
            for (int i = 0; i < headings.Length; i++)
            {
                headings[i].gameObject.SetActive(i < options.Count);
                if (i >= options.Count) continue;
                headingValues[i] = options[i];
                float radians = (options[i] + board.CameraRotationDegrees) * Mathf.Deg2Rad;
                RectTransform headingRect = (RectTransform)headings[i].transform;
                Vector2 position = board.PlayerScreenPosition + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 132f;
                Rect bounds = ((RectTransform)headingControls.parent).rect;
                Vector2 inset = headingRect.sizeDelta * .5f + Vector2.one * 12f;
                position.x = Mathf.Clamp(position.x, bounds.xMin + inset.x, bounds.xMax - inset.x);
                position.y = Mathf.Clamp(position.y, bounds.yMin + inset.y, bounds.yMax - inset.y);
                headingRect.anchoredPosition = position;
                headings[i].interactable = true;
                headings[i].GetComponentInChildren<Text>().text = "朝" + DirectionName(options[i]);
            }
        }

        private static string DirectionName(float angle)
        {
            int quadrant = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 90f) % 4;
            return new[] { "东", "北", "西", "南" }[quadrant];
        }

        private static RectTransform MakeRect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static Text MakeText(string name, RectTransform parent, Font font, int fontSize, Vector2 size, Vector2 position)
        {
            Text text = MakeRect(name, parent, size, position).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.96f, .89f, .68f); text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(string name, RectTransform parent, string caption, Font font, Vector2 size, Vector2 position)
        {
            RectTransform rect = MakeRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.035f, .12f, .14f, .97f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.72f, .60f, .34f, .95f); outline.effectDistance = Vector2.one;
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            MakeText(name + " Label", rect, font, 19, size - new Vector2(8f, 4f), Vector2.zero).text = caption;
            WoodButtonSkin.Apply(button, image.color);
            return button;
        }
    }
}
