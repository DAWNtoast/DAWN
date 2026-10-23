using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public static bool IsNavalFieldSeaCell(int x, int y) => FieldCellValid(x, y);

        private void AdvanceNavalEnemyThroughFields(float meters, float? arrivalHeading)
        {
            List<CoreNavalMovementNode> path = BuildNavalFieldEnemyPath(meters);
            var frames = new List<CoreNavalTacticalFrame> { Battle.TacticalFrame };
            float initialRange = Battle.DistanceMeters;
            bool moved = false;
            for (int index = 1; index < path.Count; index++)
            {
                CoreNavalMovementNode node = path[index];
                TurnNavalShip(false, node.HeadingDegrees, frames);
                AdvanceNavalShip(false, NavalGridCellMeters, frames);
                Battle.SetTacticalPose(false, node.PositionMeters, node.HeadingDegrees);
                frames[frames.Count - 1] = Battle.TacticalFrame;
                moved = true;
                FieldShip(false).MovementSlow = 0;
                bool interrupted = NavalFieldsEntered(false, true);
                if (!NavalFieldsActive) return;
                if (interrupted) break;
            }
            if (moved) NavalFieldsMovementEnded(false, frames);
            if (!NavalFieldsActive) return;
            if (arrivalHeading.HasValue) TurnNavalShip(false, arrivalHeading.Value, frames);
            FinishNavalMovement(frames, false, moved && Battle.DistanceMeters < initialRange - .01f);
        }

        public void NavalFieldsBeginTurn(bool player)
        {
            if (!NavalFieldsActive) return;
            CoreNavalFieldState state = Battle.NavalFields;
            state.PlayerActing = player;
            if (player)
            {
                state.PlayerTurn++;
                state.TriggerLedger.Clear();
                state.Player.FireImmunityUsed = state.Enemy.FireImmunityUsed = 0;
            }
            else state.EnemyTurn++;
            state.Cells.RemoveAll(cell =>
            {
                bool expired = cell.PlayerOwned == player && cell.ExpiresAtOwnerTurn <= FieldOwnerTurn(player);
                if (expired) state.RecordPresentation(CoreNavalFieldChangeKind.Expired, cell);
                return expired;
            });
            CoreNavalFieldShipState ship = FieldShip(player);
            if (ship.FireImmunityExpires <= FieldOwnerTurn(player)) ship.FireImmunityCharges = 0;
            if (ship.AnchorExpires <= FieldOwnerTurn(player)) ship.AnchorCharges = 0;
            TriggerNavalBeneficialField(player, NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters));
        }

        public bool NavalFieldsEndTurn(bool player)
        {
            if (!NavalFieldsActive) return false;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell != null && cell.Kind == CoreNavalFieldKind.Fire && TriggerNavalFieldDamage(player, cell)) return true;
            CoreNavalFieldShipState ship = FieldShip(player);
            ship.ArcanePiercing = ship.BloodAttackBonus = ship.TemporaryMana = 0;
            if (ship.SlowExpires <= FieldOwnerTurn(player)) ship.MovementSlow = 0;
            return false;
        }

        public int NavalFieldCurseBonus(bool player, int marks)
        {
            if (!NavalFieldsActive || marks <= 0) return 0;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell == null || cell.Kind != CoreNavalFieldKind.Curse || cell.PlayerOwned == player || !RecordNavalFieldTrigger(player, cell)) return 0;
            return (int)Math.Min(8L, 2L * marks);
        }

        private bool RecordNavalFieldTrigger(bool player, CoreNavalFieldCell cell, bool movementTrigger = false)
        {
            string key = FieldTriggerKey(player, cell);
            if (Battle.NavalFields.TriggerLedger.Contains(key)) return false;
            Battle.NavalFields.TriggerLedger.Add(key);
            Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Triggered, cell, player, movementTrigger);
            return true;
        }

        private void TriggerNavalBeneficialField(bool player, CoreNavalFieldCell cell, bool movementTrigger = false)
        {
            if (cell == null || cell.PlayerOwned != player || Battle.NavalFields.PlayerActing != player ||
                (cell.Kind != CoreNavalFieldKind.Arcane && cell.Kind != CoreNavalFieldKind.Blood) || !RecordNavalFieldTrigger(player, cell, movementTrigger)) return;
            CoreNavalFieldShipState ship = FieldShip(player);
            if (cell.Kind == CoreNavalFieldKind.Arcane)
            {
                if (player) Battle.Schools.TemporaryMana = SchoolSum(Battle.Schools.TemporaryMana, 1);
                else ship.TemporaryMana = SchoolSum(ship.TemporaryMana, 1);
                ship.ArcanePiercing = 4;
            }
            else
            {
                if (player) AddSchoolGuard(6);
                else Battle.EnemyBlock = SchoolSum(Battle.EnemyBlock, NavalExpansionEnemyBlockGain(6, true));
                ship.BloodAttackBonus = 4;
            }
        }

        // Passive field damage deliberately bypasses all active-hit, tide gain, leech and counter hooks.
        // Paid tide guard still gets consumed, after ordinary guard, without earning tide power.
        private bool TriggerNavalFieldDamage(bool player, CoreNavalFieldCell cell, bool movementTrigger = false)
        {
            if (!RecordNavalFieldTrigger(player, cell, movementTrigger)) return false;
            CoreNavalFieldShipState ship = FieldShip(player);
            if (cell.Kind == CoreNavalFieldKind.Fire && ship.FireImmunityCharges > 0)
            {
                ship.FireImmunityCharges--;
                ship.FireImmunityUsed = Math.Min(2, ship.FireImmunityUsed + 1);
                return false;
            }
            int damage = cell.Kind == CoreNavalFieldKind.Fire ? 2 : 3;
            int block = player ? Battle.PlayerBlock : Battle.EnemyBlock;
            int absorbed = Math.Min(Math.Max(0, block), damage);
            if (player)
            {
                int paid = Math.Min(block, Battle.Schools.TidePaidBlock);
                int paidAbsorbed = Math.Min(paid, Math.Max(0, absorbed - (block - paid)));
                Battle.Schools.TidePaidBlock = Math.Max(0, Battle.Schools.TidePaidBlock - paidAbsorbed);
                Battle.PlayerBlock -= absorbed;
                Hull = Math.Max(0, Hull - (damage - absorbed));
            }
            else
            {
                Battle.EnemyBlock -= absorbed;
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - (damage - absorbed));
            }
            Battle.LastAction = (player ? "己舰" : "敌舰") + "进入" + (cell.Kind == CoreNavalFieldKind.Fire ? "火场" : "雷栅") +
                "，格挡吸收 " + absorbed + "，船体损失 " + (damage - absorbed) + "。";
            return ResolveBattleOutcome();
        }

        // Returns true only when the current active route must stop. Forced movement ignores that stop result.
        private bool NavalFieldsEntered(bool player, bool activeMovement)
        {
            if (!NavalFieldsActive) return false;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell == null) return false;
            if (cell.Kind == CoreNavalFieldKind.Fire || cell.Kind == CoreNavalFieldKind.Thunder)
            {
                bool unused = !Battle.NavalFields.TriggerLedger.Contains(FieldTriggerKey(player, cell));
                if (TriggerNavalFieldDamage(player, cell, true)) return true;
                return unused && activeMovement && cell.Kind == CoreNavalFieldKind.Thunder;
            }
            TriggerNavalBeneficialField(player, cell, true);
            return false;
        }

        private bool NavalForcedTargetValid(bool player, VoyagePoint target)
        {
            if (!FieldCellValid(FieldGrid(target.X), FieldGrid(target.Y))) return false;
            VoyagePoint other = player ? Battle.EnemyPositionMeters : Battle.PlayerPositionMeters;
            return NavalGridDistance(target, other) > 0 && NavalPointDistance(target, other) >= MinimumNavalDistanceMeters;
        }

        private bool ShouldRefuseNavalPush(bool player, VoyagePoint target, bool explicitlyRefuse)
        {
            if (FieldShip(player).AnchorCharges <= 0) return false;
            if (explicitlyRefuse || (player && Battle.NavalFields.PlayerRefuseForcedMovement)) return true;
            if (player) return false;
            CoreNavalFieldCell cell = NavalFieldAt(target);
            bool danger = cell != null && (cell.Kind == CoreNavalFieldKind.Fire || cell.Kind == CoreNavalFieldKind.Thunder) &&
                !Battle.NavalFields.TriggerLedger.Contains(FieldTriggerKey(false, cell));
            return danger || NavalGridDistance(target, Battle.PlayerPositionMeters) > NavalDistanceCells;
        }

        public bool PushNavalShip(bool player, int dx, int dy, bool refuseAnchor = false)
        {
            if (!NavalFieldsActive || !FieldDirectionValid(dx, dy)) return false;
            var frames = new List<CoreNavalTacticalFrame> { Battle.TacticalFrame };
            bool moved = PushNavalShipCore(player, dx, dy, refuseAnchor, frames);
            if (!moved) return false;
            if (!NavalFieldsActive) return moved;
            if (moved) NavalFieldsMovementEnded(player, frames);
            if (!NavalFieldsActive) return moved;
            FinishNavalMovement(frames, player, false);
            if (player) Battle.PlayerMovingTowardEnemy = false;
            else Battle.EnemyMovingTowardPlayer = false;
            if (player && !Battle.NavalAwaitingHeadingSelection) ClearNavalMovementPath();
            return moved;
        }

        private bool PushNavalShipCore(bool player, int dx, int dy, bool refuseAnchor, List<CoreNavalTacticalFrame> frames)
        {
            VoyagePoint from = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            var target = new VoyagePoint(from.X + dx * NavalGridCellMeters, from.Y + dy * NavalGridCellMeters);
            // An attempted, blocked push still consumes the field trigger, but no anchor is spent on an impossible move.
            if (!NavalForcedTargetValid(player, target)) return false;
            if (ShouldRefuseNavalPush(player, target, refuseAnchor))
            {
                FieldShip(player).AnchorCharges--;
                return false;
            }
            Battle.SetTacticalPose(player, target, player ? Battle.PlayerHeadingDegrees : Battle.EnemyHeadingDegrees);
            frames.Add(Battle.TacticalFrame);
            NavalFieldsEntered(player, false);
            return true;
        }

        // Called exactly once after one active movement or a card's initial push. A water push never re-enters this method.
        private void NavalFieldsMovementEnded(bool player, List<CoreNavalTacticalFrame> frames)
        {
            if (!NavalFieldsActive) return;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell == null || cell.Kind != CoreNavalFieldKind.Tide || !RecordNavalFieldTrigger(player, cell, true)) return;
            PushNavalShipCore(player, cell.DirectionX, cell.DirectionY, false, frames);
        }
    }
}



