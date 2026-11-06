using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreNavalFieldKind { Fire, Arcane, Curse, Tide, Thunder, Blood }

    public enum CoreNavalFieldChangeKind { Placed, Overwritten, Removed, Expired, Triggered, Extended }

    /// <summary>A read-only runtime presentation fact; never serialized or used to decide battle rules.</summary>
    public readonly struct CoreNavalFieldPresentationEvent
    {
        public readonly long Sequence;
        public readonly CoreNavalFieldChangeKind Change;
        public readonly CoreNavalFieldKind Kind;
        public readonly int X, Y, InstanceId, DirectionX, DirectionY;
        public readonly bool PlayerOwned, TargetPlayer, MovementTrigger;
        public CoreNavalFieldPresentationEvent(long sequence, CoreNavalFieldChangeKind change, CoreNavalFieldCell cell, bool targetPlayer,
            bool movementTrigger = false)
        {
            Sequence = sequence; Change = change; Kind = cell.Kind; X = cell.X; Y = cell.Y;
            InstanceId = cell.InstanceId; DirectionX = cell.DirectionX; DirectionY = cell.DirectionY;
            PlayerOwned = cell.PlayerOwned; TargetPlayer = targetPlayer; MovementTrigger = movementTrigger;
        }
    }

    [Serializable]
    public sealed class CoreNavalFieldCell
    {
        // ExpiresAtOwnerTurn retains its serialized name; expiry uses the opposing side's completed turns.
        public int X, Y, InstanceId, ExpiresAtOwnerTurn, DirectionX, DirectionY;
        public CoreNavalFieldKind Kind;
        public bool PlayerOwned;
        public VoyagePoint PositionMeters => new VoyagePoint(X * CoreVoyageSession.NavalGridCellMeters, Y * CoreVoyageSession.NavalGridCellMeters);
        public CoreNavalFieldCell Copy() => (CoreNavalFieldCell)MemberwiseClone();
    }

    [Serializable]
    public sealed class CoreNavalFieldShipState
    {
        // Retained serialized name; now protects only against negative field effects.
        // Boss protection is scoped to the current enemy turn, separate from card protection.
        public bool AllFieldEffectsImmune;
        public int BossFieldImmunityMask;
        public int Mana = 3, ManaLimit = 3;
        public int ArcanePiercing, BloodAttackBonus, TemporaryMana, ThunderChain;
        public int FireImmunityCharges, FireImmunityUsed, FireImmunityExpires;
        public int BurnPreventionExpiresAtOwnerTurn;
        public bool BurnPreventionExpiresAtTurnEnd;
        public int FieldImmunityMask, FieldImmunityExpires;
        public int AnchorCharges, AnchorExpires, MovementSlow, SlowExpires, NextMovementMaxCells;
        public int TideImmunityExpiresAfterPlayerTurnEnd;
        public CoreNavalFieldShipState Copy() => (CoreNavalFieldShipState)MemberwiseClone();
    }

    [Serializable]
    public sealed class CoreNavalFieldState
    {
        public List<CoreNavalFieldCell> Cells = new List<CoreNavalFieldCell>();
        // Coordinate entries for fire; kind entries for once-per-turn fields. Thunder entry never uses this ledger.
        public List<string> TriggerLedger = new List<string>();
        public CoreNavalFieldShipState Player = new CoreNavalFieldShipState();
        public CoreNavalFieldShipState Enemy = new CoreNavalFieldShipState();
        public int PlayerTurn = 1, EnemyTurn, NextInstanceId = 1;
        public int CompletedPlayerTurns, CompletedEnemyTurns, LastEndedPlayerTurn = -1, LastEndedEnemyTurn = -1;
        public bool PlayerActing = true, PlayerRefuseForcedMovement = true;

        public const int PresentationCapacity = 128;
        [NonSerialized] private CoreNavalFieldPresentationEvent[] presentationEvents;
        [NonSerialized] private int presentationNext, presentationCount;
        [NonSerialized] private long presentationSequence;
        public long PresentationSequence => presentationSequence;
        public int PresentationEventCount => presentationCount;
        public CoreNavalFieldPresentationEvent PresentationEventAt(int index)
        {
            if (index < 0 || index >= presentationCount) throw new ArgumentOutOfRangeException(nameof(index));
            return presentationEvents[(presentationNext - presentationCount + index + PresentationCapacity) % PresentationCapacity];
        }
        internal void RecordPresentation(CoreNavalFieldChangeKind change, CoreNavalFieldCell cell, bool targetPlayer = false,
            bool movementTrigger = false)
        {
            if (cell == null) return;
            if (presentationEvents == null) presentationEvents = new CoreNavalFieldPresentationEvent[PresentationCapacity];
            presentationEvents[presentationNext] = new CoreNavalFieldPresentationEvent(++presentationSequence, change, cell, targetPlayer, movementTrigger);
            presentationNext = (presentationNext + 1) % PresentationCapacity;
            presentationCount = Math.Min(PresentationCapacity, presentationCount + 1);
        }

        public CoreNavalFieldState Copy()
        {
            // Checkpoints and prediction copies carry rules, never presentation history.
            var result = new CoreNavalFieldState
            {
                PlayerTurn = PlayerTurn, EnemyTurn = EnemyTurn, NextInstanceId = NextInstanceId,
                CompletedPlayerTurns = CompletedPlayerTurns, CompletedEnemyTurns = CompletedEnemyTurns,
                LastEndedPlayerTurn = LastEndedPlayerTurn, LastEndedEnemyTurn = LastEndedEnemyTurn,
                PlayerActing = PlayerActing, PlayerRefuseForcedMovement = PlayerRefuseForcedMovement,
                Player = (Player ?? new CoreNavalFieldShipState()).Copy(), Enemy = (Enemy ?? new CoreNavalFieldShipState()).Copy()
            };
            if (Cells != null) foreach (CoreNavalFieldCell cell in Cells) if (cell != null) result.Cells.Add(cell.Copy());
            if (TriggerLedger != null) result.TriggerLedger.AddRange(TriggerLedger);
            return result;
        }
    }

    public sealed partial class CoreBattleState
    {
        public CoreNavalFieldState NavalFields { get; internal set; } = new CoreNavalFieldState();
    }

    public sealed partial class CoreVoyageSession
    {
        private bool NavalFieldsActive => Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == NavalBattleStage.Naval;
        private static int FieldGrid(float meters) => (int)Math.Round(meters / NavalGridCellMeters);
        private CoreNavalFieldShipState FieldShip(bool player) => player ? Battle.NavalFields.Player : Battle.NavalFields.Enemy;
        private int FieldOwnerTurn(bool player) => player ? Battle.NavalFields.PlayerTurn : Battle.NavalFields.EnemyTurn;
        public int GetNavalFieldRemainingTurns(CoreNavalFieldCell cell) => cell == null || Battle == null ? 0 :
            Math.Max(0, cell.ExpiresAtOwnerTurn - FieldDurationClock(Battle.NavalFields, cell.PlayerOwned));
        private static int FieldDurationClock(CoreNavalFieldState state, bool playerOwned) =>
            playerOwned ? state.CompletedEnemyTurns : state.CompletedPlayerTurns;
        private static bool FieldDirectionValid(int dx, int dy) => Math.Abs((long)dx) + Math.Abs((long)dy) == 1;
        private static bool FieldCellValid(int x, int y) => Math.Abs((long)x) <= 100000 && Math.Abs((long)y) <= 100000;
        private const int NavalFieldProtectionMask = (1 << (int)CoreNavalFieldKind.Fire) | (1 << (int)CoreNavalFieldKind.Curse) |
            (1 << (int)CoreNavalFieldKind.Tide) | (1 << (int)CoreNavalFieldKind.Thunder) | (1 << (int)CoreNavalFieldKind.Blood);
        private static bool NavalFieldImmune(CoreNavalFieldShipState ship, CoreNavalFieldKind kind, int ownerTurn) =>
            ship.AllFieldEffectsImmune || (ship.BossFieldImmunityMask & (1 << (int)kind)) != 0 ||
            kind == CoreNavalFieldKind.Tide && ship.TideImmunityExpiresAfterPlayerTurnEnd > 0 ||
            ship.FieldImmunityExpires > ownerTurn && (ship.FieldImmunityMask & (1 << (int)kind)) != 0;
        private static string FieldTriggerKey(bool player, CoreNavalFieldCell cell) => (player ? "P:" : "E:") +
            (cell.Kind == CoreNavalFieldKind.Fire ? "F:" + cell.X + ":" + cell.Y : "K:" + (int)cell.Kind);

        public bool PlayerRefuseNavalForcedMovement
        {
            get => Battle != null && Battle.NavalFields.PlayerRefuseForcedMovement;
            set { if (Battle != null) Battle.NavalFields.PlayerRefuseForcedMovement = value; }
        }

        public IReadOnlyList<VoyagePoint> GetNavalFieldShape(CoreNavalFieldKind kind, int x, int y, bool vertical)
        {
            var result = new List<VoyagePoint>();
            int width = GetNavalFieldSize(kind), height = width;
            for (int ix = 0; ix < width; ix++) for (int iy = 0; iy < height; iy++)
                result.Add(new VoyagePoint((x + ix) * NavalGridCellMeters, (y + iy) * NavalGridCellMeters));
            return result.AsReadOnly();
        }

        public static int GetNavalFieldSize(CoreNavalFieldKind kind) =>
            kind == CoreNavalFieldKind.Thunder ? 7 : kind == CoreNavalFieldKind.Tide ? 4 : kind == CoreNavalFieldKind.Fire ? 3 : 2;

        public string CanPlaceNavalFieldAnchor(CoreNavalFieldKind kind, bool playerOwned, int x, int y, bool vertical = false)
        {
            if (!NavalFieldsActive) return "场地只能在炮战阶段放置";
            if (!Enum.IsDefined(typeof(CoreNavalFieldKind), kind) || !FieldCellValid(x, y)) return "场地格无效";
            VoyagePoint caster = playerOwned ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            if (NavalGridDistance(caster, new VoyagePoint(x * NavalGridCellMeters, y * NavalGridCellMeters)) > NavalCannonRangeCells)
                return "场地左下角须在 4 格曼哈顿范围内";
            foreach (VoyagePoint point in GetNavalFieldShape(kind, x, y, vertical))
                if (!FieldCellValid(FieldGrid(point.X), FieldGrid(point.Y))) return "场地范围包含无效海格";
            return string.Empty;
        }

        public string CanPlaceNavalField(CoreNavalFieldKind kind, bool playerOwned, int x, int y, bool vertical, int dx, int dy)
        {
            string reason = CanPlaceNavalFieldAnchor(kind, playerOwned, x, y, vertical);
            if (reason.Length > 0) return reason;
            if (kind == CoreNavalFieldKind.Tide && !FieldDirectionValid(dx, dy)) return "请选择水流的上下左右方向";
            return string.Empty;
        }

        public bool PlaceNavalField(CoreNavalFieldKind kind, bool playerOwned, int x, int y, bool vertical, int dx, int dy)
        {
            if (CanPlaceNavalField(kind, playerOwned, x, y, vertical, dx, dy).Length > 0) return false;
            CoreNavalFieldState state = Battle.NavalFields;
            int instance = state.NextInstanceId++;
            foreach (VoyagePoint point in GetNavalFieldShape(kind, x, y, vertical))
            {
                int px = FieldGrid(point.X), py = FieldGrid(point.Y);
                state.Cells.RemoveAll(cell =>
                {
                    bool replaced = cell.X == px && cell.Y == py;
                    if (replaced) state.RecordPresentation(CoreNavalFieldChangeKind.Overwritten, cell);
                    return replaced;
                });
                var placed = new CoreNavalFieldCell { X = px, Y = py, Kind = kind, PlayerOwned = playerOwned,
                    InstanceId = instance, ExpiresAtOwnerTurn = SchoolSum(FieldDurationClock(state, playerOwned),
                        kind == CoreNavalFieldKind.Arcane ? 3 : 5),
                    DirectionX = dx, DirectionY = dy };
                state.Cells.Add(placed);
                state.RecordPresentation(CoreNavalFieldChangeKind.Placed, placed);
            }
            return true;
        }

        public CoreNavalFieldCell GetNavalFieldAt(int x, int y) => Battle?.NavalFields.Cells.Find(cell => cell.X == x && cell.Y == y);
        public CoreNavalFieldCell NavalFieldAt(VoyagePoint point) => GetNavalFieldAt(FieldGrid(point.X), FieldGrid(point.Y));
        public int RemoveNavalFieldCells(IEnumerable<CoreNavalFieldCell> cells)
        {
            if (Battle == null || cells == null) return 0;
            var selected = new List<CoreNavalFieldCell>(cells);
            return Battle.NavalFields.Cells.RemoveAll(cell =>
            {
                bool removed = selected.Exists(other => other != null &&
                    other.InstanceId == cell.InstanceId && other.X == cell.X && other.Y == cell.Y);
                if (removed) Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Removed, cell);
                return removed;
            });
        }

        public string CanMoveNavalFireCell(bool owner, int sourceX, int sourceY, int targetX, int targetY)
        {
            if (!NavalFieldsActive) return "当前不是炮战";
            CoreNavalFieldCell source = GetNavalFieldAt(sourceX, sourceY);
            if (source == null || source.Kind != CoreNavalFieldKind.Fire || source.PlayerOwned != owner) return "请选择己方火场";
            if (!FieldDirectionValid(targetX - sourceX, targetY - sourceY) || !FieldCellValid(targetX, targetY)) return "只能移到正交相邻的海格";
            VoyagePoint origin = owner ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            var target = new VoyagePoint(targetX * NavalGridCellMeters, targetY * NavalGridCellMeters);
            if (NavalGridDistance(origin, source.PositionMeters) > 4) return "源格须在 4 格内";
            if (GetNavalFieldAt(targetX, targetY) != null || FieldOccupied(target)) return "落点必须没有船只和场地";
            return string.Empty;
        }
        public bool MoveNavalFireCell(bool owner, int sourceX, int sourceY, int targetX, int targetY)
        {
            if (CanMoveNavalFireCell(owner, sourceX, sourceY, targetX, targetY).Length > 0) return false;
            CoreNavalFieldCell source = GetNavalFieldAt(sourceX, sourceY);
            Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Removed, source);
            source.X = targetX; source.Y = targetY;
            Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Placed, source);
            return true;
        }
        private bool FieldOccupied(VoyagePoint point) => NavalGridDistance(point, Battle.PlayerPositionMeters) == 0 ||
            NavalGridDistance(point, Battle.EnemyPositionMeters) == 0;

        public void GrantNavalFireImmunity(bool player)
        {
            GrantFireBurnPrevention(player, false);
        }

        private void GrantFireBurnPrevention(bool player, bool expiresAtTurnEnd)
        {
            if (Battle == null) return;
            CoreNavalFieldShipState ship = FieldShip(player);
            int expires = SchoolSum(FieldOwnerTurn(player), 1);
            if (expires > ship.BurnPreventionExpiresAtOwnerTurn)
            {
                ship.BurnPreventionExpiresAtOwnerTurn = expires;
                ship.BurnPreventionExpiresAtTurnEnd = expiresAtTurnEnd;
            }
            else if (expires == ship.BurnPreventionExpiresAtOwnerTurn)
                ship.BurnPreventionExpiresAtTurnEnd |= expiresAtTurnEnd;
        }

        private bool IsFireBurnPrevented(bool player)
        {
            if (Battle == null) return false;
            CoreNavalFieldShipState ship = FieldShip(player);
            int turn = FieldOwnerTurn(player);
            return ship.BurnPreventionExpiresAtOwnerTurn > turn ||
                (ship.BurnPreventionExpiresAtTurnEnd && ship.BurnPreventionExpiresAtOwnerTurn == turn);
        }
        public void GrantNavalAnchor(bool player)
        {
            if (Battle == null) return;
            FieldShip(player).AnchorCharges = 1;
            FieldShip(player).AnchorExpires = FieldOwnerTurn(player) + 1;
        }
        public void ApplyNavalMovementSlow(bool player)
        {
            if (Battle == null) return;
            FieldShip(player).MovementSlow = SchoolSum(FieldShip(player).MovementSlow, 1);
            FieldShip(player).SlowExpires = int.MaxValue;
        }
        public void ApplyNavalMovementCap(bool player, int maximum = 1)
        {
            if (Battle == null || maximum <= 0) return;
            CoreNavalFieldShipState ship = FieldShip(player);
            ship.NextMovementMaxCells = ship.NextMovementMaxCells > 0 ? Math.Min(ship.NextMovementMaxCells, maximum) : maximum;
        }

        public void GrantNavalTideImmunity(bool player)
        {
            if (Battle == null) return;
            CoreNavalFieldShipState ship = FieldShip(player);
            if (ship.FieldImmunityExpires <= FieldOwnerTurn(player)) ship.FieldImmunityMask = 0;
            ship.FieldImmunityMask |= 1 << (int)CoreNavalFieldKind.Tide;
            ship.FieldImmunityExpires = SchoolSum(FieldOwnerTurn(player), 1);
        }

        // Retained for older callers/snapshots. Current cards use next-owner-start protection above.
        public void GrantNavalTideImmunityThroughNextPlayerTurnEnd(bool player)
        {
            if (Battle == null) return;
            FieldShip(player).TideImmunityExpiresAfterPlayerTurnEnd = SchoolSum(Battle.NavalFields.PlayerTurn, 1);
        }

        private static int NavalActiveMovementLimit(CoreNavalFieldShipState ship, int normal)
        {
            int available = Math.Max(0, normal - ship.MovementSlow);
            return ship.NextMovementMaxCells > 0 ? Math.Min(available, ship.NextMovementMaxCells) : available;
        }
        private int NavalActiveMovementLimit(bool player, int normal) => Battle == null ? Math.Max(0, normal) : NavalActiveMovementLimit(FieldShip(player), normal);

        private static bool NavalTideBlocksMovement(CoreNavalFieldState state, bool player, VoyagePoint from, VoyagePoint to)
        {
            CoreNavalFieldCell cell = state.Cells.Find(field => field.X == FieldGrid(from.X) && field.Y == FieldGrid(from.Y));
            if (cell == null || cell.Kind != CoreNavalFieldKind.Tide ||
                NavalFieldImmune(player ? state.Player : state.Enemy, cell.Kind, player ? state.PlayerTurn : state.EnemyTurn)) return false;
            return (to.X - from.X) * cell.DirectionX + (to.Y - from.Y) * cell.DirectionY < -.01f;
        }

        public CoreNavalFieldState CaptureNavalFields() => Battle?.NavalFields.Copy();
        public bool RestoreNavalFields(CoreNavalFieldState saved)
        {
            if (!NavalFieldsActive || saved == null || saved.Cells == null || saved.TriggerLedger == null ||
                saved.Player == null || saved.Enemy == null || saved.PlayerTurn < 0 || saved.EnemyTurn < 0 || saved.NextInstanceId < 1 || saved.NextInstanceId == int.MaxValue || saved.PlayerTurn > int.MaxValue - 2 || saved.EnemyTurn > int.MaxValue - 2 ||
                saved.CompletedEnemyTurns < 0 || saved.CompletedEnemyTurns > int.MaxValue - 2 || saved.LastEndedEnemyTurn < -1 || saved.LastEndedEnemyTurn > saved.EnemyTurn ||
                saved.CompletedPlayerTurns < 0 || saved.CompletedPlayerTurns > int.MaxValue - 2 || saved.LastEndedPlayerTurn < -1 || saved.LastEndedPlayerTurn > saved.PlayerTurn) return false;
            var coordinates = new HashSet<string>();
            var instances = new Dictionary<int, CoreNavalFieldCell>();
            foreach (CoreNavalFieldCell cell in saved.Cells)
            {
                if (cell == null || !FieldCellValid(cell.X, cell.Y) || !Enum.IsDefined(typeof(CoreNavalFieldKind), cell.Kind) ||
                    cell.InstanceId < 1 || cell.InstanceId >= saved.NextInstanceId ||
                    cell.ExpiresAtOwnerTurn <= FieldDurationClock(saved, cell.PlayerOwned) ||
                    (cell.Kind == CoreNavalFieldKind.Tide && !FieldDirectionValid(cell.DirectionX, cell.DirectionY)) ||
                    !coordinates.Add(cell.X + ":" + cell.Y)) return false;
                if (instances.TryGetValue(cell.InstanceId, out CoreNavalFieldCell previous) &&
                    (cell.Kind != previous.Kind || cell.PlayerOwned != previous.PlayerOwned ||
                     cell.ExpiresAtOwnerTurn != previous.ExpiresAtOwnerTurn || cell.DirectionX != previous.DirectionX || cell.DirectionY != previous.DirectionY)) return false;
                instances[cell.InstanceId] = cell;
            }
            foreach (string key in saved.TriggerLedger)
            {
                if (key == null) return false;
                string[] pieces = key.Split(':');
                if (pieces.Length < 3 || (pieces[0] != "P" && pieces[0] != "E")) return false;
                if (pieces[1] == "F")
                {
                    if (pieces.Length != 4 || !int.TryParse(pieces[2], out int x) || !int.TryParse(pieces[3], out int y) || !FieldCellValid(x, y)) return false;
                }
                else if (pieces[1] != "K" || pieces.Length != 3 || !int.TryParse(pieces[2], out int kind) || kind < 1 || kind > 5) return false;
            }
            foreach (CoreNavalFieldShipState ship in new[] { saved.Player, saved.Enemy })
                if (ship.Mana < 0 || ship.ManaLimit < 0 || ship.ThunderChain < 0 || ship.ArcanePiercing < 0 || ship.ArcanePiercing > 4 || ship.BloodAttackBonus < 0 || ship.BloodAttackBonus > 4 ||
                    ship.FireImmunityCharges < 0 || ship.FireImmunityUsed < 0 ||
                    ship.BurnPreventionExpiresAtOwnerTurn < 0 ||
                    (ship.BurnPreventionExpiresAtOwnerTurn == 0 && ship.BurnPreventionExpiresAtTurnEnd) ||
                    (ship.BossFieldImmunityMask & ~NavalBossAllFieldImmunityMask) != 0 ||
                    (ReferenceEquals(ship, saved.Player) && (ship.BossFieldImmunityMask != 0 || ship.AllFieldEffectsImmune)) ||
                    (saved.PlayerActing && (ship.BossFieldImmunityMask != 0 || ship.AllFieldEffectsImmune)) ||
                    (ship.BossFieldImmunityMask != 0 && (ship.BossFieldImmunityMask != EnemyNavalBossFieldImmunityMaskThisTurn ||
                        ship.AllFieldEffectsImmune != (ship.BossFieldImmunityMask == NavalBossAllFieldImmunityMask))) ||
                    (ship.AllFieldEffectsImmune && EnemyNavalBossFieldImmunityMaskThisTurn != NavalBossAllFieldImmunityMask) ||
                    (ship.FieldImmunityMask & ~NavalFieldProtectionMask) != 0 || ship.FieldImmunityExpires < 0 ||
                    (ship.FieldImmunityMask == 0 && ship.FieldImmunityExpires != 0) ||
                    (ship.FieldImmunityMask != 0 && ship.FieldImmunityExpires !=
                        (ReferenceEquals(ship, saved.Player) ? saved.PlayerTurn : saved.EnemyTurn) + 1) ||
                    ship.AnchorCharges < 0 || ship.AnchorCharges > 1 || ship.MovementSlow < 0 || ship.NextMovementMaxCells < 0 ||
                    ship.TideImmunityExpiresAfterPlayerTurnEnd < 0 ||
                    ship.TemporaryMana < 0 || ship.FireImmunityExpires < 0 || ship.AnchorExpires < 0 || ship.SlowExpires < 0) return false;
            Battle.NavalFields = saved.Copy();
            if (Battle.NavalFields.Enemy.AllFieldEffectsImmune)
                Battle.NavalFields.Enemy.BossFieldImmunityMask = NavalBossAllFieldImmunityMask;
            // The retired blood-field attack charge is retained only for save compatibility.
            Battle.NavalFields.Player.BloodAttackBonus = Battle.NavalFields.Enemy.BloodAttackBonus = 0;
            return true;
        }
        public void ClearNavalFields()
        {
            if (Battle != null) Battle.NavalFields = new CoreNavalFieldState();
        }
    }
}


