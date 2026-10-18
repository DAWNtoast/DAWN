using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Persistent field ownership, expiry and direction, plus the real route's field consequences.</summary>
    public sealed class NavalFieldBoardOverlay : MonoBehaviour
    {
        private CoreVoyageSession session;
        private CoreBattleState battle;
        private NavalTacticalBoardView board;
        private NavalFieldGraphic graphic, routeGraphic;
        private NavalFieldMotion motion;
        private Font font;
        private readonly List<Text> labels = new List<Text>();
        private readonly List<NavalFieldVisualCell> cells = new List<NavalFieldVisualCell>();
        private readonly List<NavalFieldVisualCell> routeCells = new List<NavalFieldVisualCell>();
        private Text legend, routeSummary;
        private Button anchorChoice;
        private int lastSignature = int.MinValue;

        public void Configure(CoreVoyageSession model, NavalTacticalBoardView view, Font labelFont, Behaviour motionOwner = null)
        {
            session = model; battle = model.Battle; board = view; font = labelFont;
            RectTransform root = (RectTransform)view.transform;
            RectTransform fieldRoot = NavalCardChoiceOverlay.MakeRect("Persistent Naval Fields", root, root.rect.size, Vector2.zero);
            fieldRoot.SetSiblingIndex(1);
            graphic = fieldRoot.gameObject.AddComponent<NavalFieldGraphic>();
            motion = fieldRoot.gameObject.AddComponent<NavalFieldMotion>();
            motion.Configure(graphic, battle.NavalFields, motionOwner, board);
            routeGraphic = NavalCardChoiceOverlay.MakeRect("Naval field route consequences", root, root.rect.size, Vector2.zero).gameObject.AddComponent<NavalFieldGraphic>();
            legend = NavalCardChoiceOverlay.MakeText("Naval field legend", root, font, 15, new Vector2(1000f, 30f), new Vector2(0f, 380f));
            legend.text = "火场 · 折射阵 · 禁域 · 水流 · 雷栅 · 血誓阵　我/敌 = 归属　数字 = 距所属方回合开始移除次数";
            routeSummary = NavalCardChoiceOverlay.MakeText("Naval route field preview", root, font, 17, new Vector2(1020f, 50f), new Vector2(0f, -132f));
            anchorChoice = NavalCardChoiceOverlay.MakeButton("Naval anchor response", root, font, string.Empty, new Vector2(215f, 38f),
                new Vector2(-420f, 279f), () => { session.PlayerRefuseNavalForcedMovement = !session.PlayerRefuseNavalForcedMovement; RefreshNow(); });
            RefreshNow();
        }

        private void LateUpdate()
        {
            if (session == null || board == null) return;
            int signature = Signature();
            if (signature == lastSignature) return;
            RefreshNow();
            lastSignature = signature;
        }

        private int Signature()
        {
            unchecked
            {
                int value = battle.TacticalRevision * 31 + battle.Turn;
                value = value * 31 + (int)battle.Stage;
                value = value * 31 + battle.NavalFields.GetHashCode();
                value = value * 31 + battle.NavalFields.PresentationSequence.GetHashCode();
                value = value * 31 + battle.CardsPlayedThisTurn;
                value = value * 31 + board.CameraRotationDegrees.GetHashCode();
                value = value * 31 + board.CellSizePixels.GetHashCode();
                value = value * 31 + board.PlayerScreenPosition.GetHashCode();
                value = value * 31 + board.EnemyScreenPosition.GetHashCode();
                value = value * 31 + session.NavalMovementPlannedSteps;
                value = value * 31 + session.Hull;
                value = value * 31 + battle.EnemyHull;
                value = value * 31 + battle.PlayerBlock;
                value = value * 31 + battle.NavalFields.PlayerTurn;
                value = value * 31 + battle.NavalFields.EnemyTurn;
                value = value * 31 + battle.NavalFields.Player.AnchorCharges;
                value = value * 31 + battle.NavalFields.Player.FireImmunityCharges;
                value = value * 31 + (session.PlayerRefuseNavalForcedMovement ? 1 : 0);
                value = value * 31 + (ReferenceEquals(session.Battle, battle) ? 1 : 0);
                foreach (CoreNavalMovementNode node in session.NavalMovementPath)
                { value = value * 31 + node.PositionMeters.X.GetHashCode(); value = value * 31 + node.PositionMeters.Y.GetHashCode(); }
                foreach (CoreNavalFieldCell field in battle.NavalFields.Cells)
                {
                    value = value * 31 + field.X; value = value * 31 + field.Y;
                    value = value * 31 + field.InstanceId; value = value * 31 + field.ExpiresAtOwnerTurn;
                    value = value * 31 + (int)field.Kind;
                    value = value * 31 + field.DirectionX; value = value * 31 + field.DirectionY;
                }
                return value;
            }
        }

        public void RefreshNow()
        {
            if (graphic == null) return;
            bool active = ReferenceEquals(session.Battle, battle) && battle.Stage != NavalBattleStage.Boarding;
            cells.Clear(); routeCells.Clear();
            int index = 0;
            if (active)
                foreach (CoreNavalFieldCell field in battle.NavalFields.Cells)
                {
                    Color border = NavalFieldGraphic.Tint(field.Kind), fill = border; fill.a = .23f;
                    var position = new VoyagePoint(field.X * CoreVoyageSession.NavalGridCellMeters, field.Y * CoreVoyageSession.NavalGridCellMeters);
                    bool water = field.Kind == CoreNavalFieldKind.Tide;
                    cells.Add(new NavalFieldVisualCell(position, field.Kind, fill, border, true,
                        water ? field.DirectionX : 0, water ? field.DirectionY : 0));
                    while (labels.Count <= index)
                    {
                        Text label = NavalCardChoiceOverlay.MakeText("Naval field cell " + labels.Count, graphic.transform, font, 15, new Vector2(96f, 24f), Vector2.zero);
                        Outline shadow = label.gameObject.AddComponent<Outline>(); shadow.effectColor = new Color(0f, 0f, 0f, .9f); shadow.effectDistance = Vector2.one;
                        labels.Add(label);
                    }
                    Text text = labels[index++]; text.gameObject.SetActive(true);
                    int remaining = Mathf.Max(0, field.ExpiresAtOwnerTurn - (field.PlayerOwned ? battle.NavalFields.PlayerTurn : battle.NavalFields.EnemyTurn));
                    text.text = (field.PlayerOwned ? "我·" : "敌·") + NavalFieldGraphic.ShortName(field.Kind) + remaining;
                    text.color = border;
                    Vector2 center = board.WorldToBoard(position);
                    Vector2 labelPosition = center + Vector2.down * board.CellSizePixels * .26f;
                    if (OverlapsHullReadout(labelPosition)) labelPosition = center + Vector2.up * board.CellSizePixels * .28f;
                    text.rectTransform.anchoredPosition = labelPosition;
                    text.fontSize = Mathf.Clamp(Mathf.RoundToInt(board.CellSizePixels * .19f), 10, 16);
                }
            for (; index < labels.Count; index++) labels[index].gameObject.SetActive(false);
            graphic.Configure(board, cells);
            motion.Synchronize(battle.NavalFields, active, battle.TacticalRevision);
            legend.gameObject.SetActive(active && cells.Count > 0);
            routeSummary.text = string.Empty;
            if (active && session.NavalMovementPlannedSteps > 0)
            {
                CoreNavalMovementFieldPreview preview = session.NavalMovementFieldPreview;
                routeSummary.text = preview.Summary;
                if (preview.StopsAtThunder) AddRouteMarker(preview.StopPositionMeters, new Color(1f, .72f, .16f), true);
                foreach (CoreNavalFieldPreviewEvent item in preview.Events)
                    if (item.Damage > 0) AddRouteMarker(item.PositionMeters, new Color(1f, .37f, .22f), false);
                if (preview.ActiveSteps > 0) AddRouteMarker(preview.FinalPositionMeters, new Color(.4f, 1f, .85f), preview.Sinks);
            }
            routeGraphic.Configure(board, routeCells);
            anchorChoice.gameObject.SetActive(active && battle.NavalFields.Player.AnchorCharges > 0);
            anchorChoice.GetComponentInChildren<Text>().text = session.PlayerRefuseNavalForcedMovement ? "定锚：拒绝一次推动" : "定锚：允许推动";
            anchorChoice.interactable = active && !board.IsAnimating;
        }

        private void AddRouteMarker(VoyagePoint position, Color tint, bool cross)
        {
            Color fill = tint; fill.a = .16f;
            routeCells.Add(new NavalFieldVisualCell(position, CoreNavalFieldKind.Tide, fill, tint, false, 0, 0, cross));
        }

        private bool OverlapsHullReadout(Vector2 position)
        {
            foreach (string name in new[] { "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" })
            {
                RectTransform bar = board.transform.parent.Find(name) as RectTransform;
                if (bar == null) continue;
                var corners = new Vector3[4]; bar.GetWorldCorners(corners);
                Vector2 a = board.transform.InverseTransformPoint(corners[0]), b = board.transform.InverseTransformPoint(corners[2]);
                Rect bounds = Rect.MinMaxRect(a.x - 38f, a.y - 14f, b.x + 38f, b.y + 35f);
                if (bounds.Contains(position)) return true;
            }
            return false;
        }
    }
}
