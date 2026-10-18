using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A pinned ship range is temporarily replaced by the hovered damage card's range.</summary>
    [DisallowMultipleComponent]
    public sealed class NavalAttackRangePreview : MonoBehaviour
    {
        private CoreVoyageSession session;
        private CoreBattleState battle;
        private NavalTacticalBoardView board;
        private NavalTacticalAttackRangeGraphic graphic;
        private Text caption;
        private Func<bool> inputBlocked;
        private Component hoverOwner;
        private Func<CoreBattleCard> hoveredCard;
        private readonly List<VoyagePoint> cells = new List<VoyagePoint>();
        private readonly List<CoreNavalAttackRange> ranges = new List<CoreNavalAttackRange>();
        private IReadOnlyList<VoyagePoint> visibleMovementCells;
        private int? previousSignature;

        public bool ShipRangePinned { get; private set; }
        public string ShowingCardId { get; private set; }

        public void Configure(CoreVoyageSession model, NavalTacticalBoardView view, Font font, Func<bool> blocked)
        {
            session = model; battle = model.Battle; board = view; inputBlocked = blocked;
            RectTransform parent = (RectTransform)view.transform;
            RectTransform rangeRect = MakeRect("Naval attack range", parent, parent.rect.size);
            // Behind the course/route and ships, above the sea grid. The overlay never receives clicks.
            rangeRect.SetSiblingIndex(1);
            graphic = rangeRect.gameObject.AddComponent<NavalTacticalAttackRangeGraphic>();
            graphic.raycastTarget = false;
            RectTransform label = MakeRect("Naval attack range caption", parent, new Vector2(440f, 64f));
            label.anchorMin = label.anchorMax = new Vector2(0f, .5f);
            label.anchoredPosition = new Vector2(315f, 124f);
            caption = label.gameObject.AddComponent<Text>();
            caption.font = font; caption.fontSize = 18; caption.alignment = TextAnchor.MiddleLeft;
            caption.color = new Color(1f, .81f, .74f);
            caption.raycastTarget = false;
            RefreshNow();
        }

        public void ToggleShipRange()
        {
            if (!CanPreview || Blocked) return;
            SetShipRangePinned(!ShipRangePinned);
        }

        public void SetShipRangePinned(bool pinned)
        {
            if (pinned && (!CanPreview || Blocked)) return;
            ShipRangePinned = pinned;
            RefreshNow();
        }

        public bool IsPlayerCell(Vector2 screenPoint, Camera eventCamera) => CanPreview &&
            board.TryScreenToWorldCell(screenPoint, eventCamera, out VoyagePoint cell) &&
            CoreVoyageSession.NavalGridDistance(battle.PlayerPositionMeters, cell) == 0;

        public void BeginCardHover(Component owner, Func<CoreBattleCard> getCard)
        {
            if (!CanPreview) return;
            hoverOwner = owner; hoveredCard = getCard;
            RefreshNow();
        }

        public void EndCardHover(Component owner)
        {
            // A late exit from a neighbouring/recycled card must not erase the new card's preview.
            if (hoverOwner != owner) return;
            hoverOwner = null; hoveredCard = null;
            RefreshNow();
        }

        private bool CanPreview => session != null && board != null && session.Phase == CoreDemoPhase.Battle &&
            ReferenceEquals(session.Battle, battle) && battle.Stage != NavalBattleStage.Boarding;
        private bool Blocked => board == null || board.IsAnimating || (inputBlocked != null && inputBlocked());

        private void LateUpdate()
        {
            if (board == null || session == null || graphic == null) return;
            int signature;
            unchecked
            {
                signature = CanPreview ? 1 : 0;
                signature = signature * 31 + (Blocked ? 1 : 0);
                signature = signature * 31 + board.CameraRotationDegrees.GetHashCode();
                signature = signature * 31 + board.CellSizePixels.GetHashCode();
                signature = signature * 31 + board.PlayerScreenPosition.GetHashCode();
                signature = signature * 31 + board.EnemyScreenPosition.GetHashCode();
                signature = signature * 31 + ((RectTransform)board.transform).rect.GetHashCode();
                NavalTacticalMovementInput movement = GetComponent<NavalTacticalMovementInput>();
                bool movementVisible = movement != null && movement.IsMovementRangeVisible;
                signature = signature * 31 + (movementVisible ? 1 : 0);
                if (movementVisible)
                    foreach (VoyagePoint cell in movement.ReachableCells)
                    {
                        signature = signature * 31 + cell.X.GetHashCode();
                        signature = signature * 31 + cell.Y.GetHashCode();
                    }
                if (battle != null)
                {
                    signature = signature * 31 + battle.PlayerPositionMeters.X.GetHashCode();
                    signature = signature * 31 + battle.PlayerPositionMeters.Y.GetHashCode();
                    signature = signature * 31 + battle.EnemyPositionMeters.X.GetHashCode();
                    signature = signature * 31 + battle.EnemyPositionMeters.Y.GetHashCode();
                    signature = signature * 31 + (int)battle.Stage;
                    foreach (int id in battle.Hand) signature = signature * 31 + id;
                }
            }
            if (previousSignature == signature) return;
            RefreshNow();
            previousSignature = signature;
        }

        public void RefreshNow()
        {
            if (graphic == null) return;
            previousSignature = null;
            cells.Clear(); ranges.Clear(); ShowingCardId = null;
            visibleMovementCells = null;
            if (!CanPreview || Blocked)
            {
                graphic.Configure(board, cells);
                caption.text = string.Empty;
                return;
            }
            CoreBattleCard card = hoverOwner != null && hoverOwner.gameObject.activeInHierarchy ? hoveredCard?.Invoke() : null;
            CoreNavalAttackRange cardRange = card == null ? default : session.GetNavalCardAttackRange(card);
            NavalTacticalMovementInput movement = GetComponent<NavalTacticalMovementInput>();
            if (movement != null && movement.IsMovementRangeVisible) visibleMovementCells = movement.ReachableCells;
            string movementLegend = visibleMovementCells != null ? "\n青格：可达海域优先 · 金线：已选航线" : string.Empty;
            if (cardRange.IsDamageCard)
            {
                ShowingCardId = card.Id;
                ranges.Add(cardRange);
                caption.text = "红格：" + card.Name + " · " + RangeCaption(cardRange) + movementLegend;
            }
            else if (ShipRangePinned)
            {
                ranges.Add(CoreNavalAttackRange.Cannon);
                foreach (int id in battle.Hand)
                {
                    CoreNavalAttackRange range = session.GetNavalCardAttackRange(session.GetBattleCard(id));
                    if (range.IsDamageCard) ranges.Add(range);
                }
                caption.text = "红格：炮击与当前手牌的攻击范围" + movementLegend + "\n再次点击我舰收起范围";
            }
            else caption.text = "点击我舰：查看移动海域与攻击范围\n按住我舰拖动航线 · 悬停伤害牌查看范围";

            if (ranges.Count > 0 && CollectCells()) return;
            graphic.Configure(board, cells, visibleMovementCells);
        }

        private bool CollectCells()
        {
            const float size = CoreVoyageSession.NavalGridCellMeters;
            VoyagePoint origin = battle.PlayerPositionMeters;
            int centerX = Mathf.RoundToInt(origin.X / size), centerY = Mathf.RoundToInt(origin.Y / size);
            int radius = 0;
            bool unlimited = false;
            foreach (CoreNavalAttackRange range in ranges)
            { radius = Math.Max(radius, range.ScanRadiusCells); unlimited |= range.IsUnlimited; }
            int minX = centerX - radius, maxX = centerX + radius, minY = centerY - radius, maxY = centerY + radius;
            Rect visible = ((RectTransform)board.transform).rect;
            if (unlimited)
            {
                VoyagePoint a = board.BoardToWorld(visible.min), b = board.BoardToWorld(visible.max);
                VoyagePoint c = board.BoardToWorld(new Vector2(visible.xMin, visible.yMax));
                VoyagePoint d = board.BoardToWorld(new Vector2(visible.xMax, visible.yMin));
                minX = Mathf.FloorToInt(Mathf.Min(a.X, b.X, c.X, d.X) / size);
                maxX = Mathf.CeilToInt(Mathf.Max(a.X, b.X, c.X, d.X) / size);
                minY = Mathf.FloorToInt(Mathf.Min(a.Y, b.Y, c.Y, d.Y) / size);
                maxY = Mathf.CeilToInt(Mathf.Max(a.Y, b.Y, c.Y, d.Y) / size);
                if (((long)maxX - minX + 1L) * ((long)maxY - minY + 1L) > 2000L)
                {
                    // At distant zoom levels paint the same visible area by rows/columns,
                    // rather than constructing thousands of separate cell meshes.
                    graphic.ConfigureUnlimited(board, origin, visibleMovementCells);
                    return true;
                }
                // Include partially visible edge cells; RectMask2D clips their painted corners.
                float margin = board.CellSizePixels * .71f;
                visible = Rect.MinMaxRect(visible.xMin - margin, visible.yMin - margin, visible.xMax + margin, visible.yMax + margin);
            }
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    var target = new VoyagePoint(x * size, y * size);
                    if (unlimited && !visible.Contains(board.WorldToBoard(target))) continue;
                    foreach (CoreNavalAttackRange range in ranges)
                        if (range.Contains(origin, target)) { cells.Add(target); break; }
                }
            return false;
        }

        private static string RangeCaption(CoreNavalAttackRange range) => range.Kind == CoreNavalAttackRangeKind.CannonCells
            ? "横纵相加4格" : range.IsUnlimited ? "无距离限制" : range.Meters.ToString("0") + "米";

        private static RectTransform MakeRect(string name, RectTransform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }
    }
}
