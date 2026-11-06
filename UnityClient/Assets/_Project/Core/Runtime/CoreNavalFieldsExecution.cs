using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public static bool IsNavalFieldSeaCell(int x, int y) => FieldCellValid(x, y);

        private void AdvanceNavalEnemyThroughFields(float meters, float? arrivalHeading)
        {
            CoreBattleState committedBattle = Battle;
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
                FieldShip(false).MovementSlow = FieldShip(false).NextMovementMaxCells = 0;
                bool interrupted = NavalFieldsEntered(false, true);
                if (!NavalFieldsActive)
                {
                    committedBattle.CommitTacticalMovement(frames);
                    return;
                }
                if (interrupted) break;
            }
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
            CoreNavalFieldShipState ship = FieldShip(player);
            if (!player) ApplyEnemyNavalBossFieldImmunity(ship);
            else ClearEnemyNavalBossFieldImmunity(state.Enemy);
            if (!player) ship.Mana = ship.ManaLimit;
            ship.ArcanePiercing = ship.TemporaryMana = 0;
            if (ship.BurnPreventionExpiresAtOwnerTurn < FieldOwnerTurn(player) ||
                (!ship.BurnPreventionExpiresAtTurnEnd && ship.BurnPreventionExpiresAtOwnerTurn <= FieldOwnerTurn(player)))
            {
                ship.BurnPreventionExpiresAtOwnerTurn = 0;
                ship.BurnPreventionExpiresAtTurnEnd = false;
            }
            if (ship.FireImmunityExpires <= FieldOwnerTurn(player)) ship.FireImmunityCharges = 0;
            if (ship.FieldImmunityExpires <= FieldOwnerTurn(player)) ship.FieldImmunityMask = ship.FieldImmunityExpires = 0;
            if (ship.AnchorExpires <= FieldOwnerTurn(player)) ship.AnchorCharges = 0;
            ResetNavalThunderChain(player);
            CoreNavalFieldCell current = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            TriggerNavalArcaneField(player, current);
        }

        public bool NavalFieldsEndTurn(bool player)
        {
            if (!NavalFieldsActive) return false;
            CoreNavalFieldState state = Battle.NavalFields;
            if (player ? state.LastEndedPlayerTurn == state.PlayerTurn : state.LastEndedEnemyTurn == state.EnemyTurn) return false;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell != null && cell.Kind == CoreNavalFieldKind.Fire && !NavalFieldImmune(FieldShip(player), CoreNavalFieldKind.Fire, FieldOwnerTurn(player)) && RecordNavalFieldTrigger(player, cell))
            {
                int layers = GetNavalFieldRemainingTurns(cell);
                if (layers > 0 && !IsFireBurnPrevented(player))
                {
                    int before = player ? Battle.PlayerBurnStacks : Battle.EnemyBurnTurns;
                    ApplyFireBurnFromSource(player, layers, cell.PlayerOwned);
                    int added = (player ? Battle.PlayerBurnStacks : Battle.EnemyBurnTurns) - before;
                    string action = (player ? "己舰" : "敌舰") + "停留火场，获得 " + added + " 层燃烧。";
                    Battle.LastAction = string.IsNullOrEmpty(Battle.LastAction) ? action : Battle.LastAction + " " + action;
                }
            }
            CoreNavalFieldShipState ship = FieldShip(player);
            if (cell != null && cell.Kind == CoreNavalFieldKind.Blood &&
                !NavalFieldImmune(ship, CoreNavalFieldKind.Blood, FieldOwnerTurn(player)) &&
                RecordNavalFieldTrigger(player, cell))
            {
                int loss = Math.Min(player ? Hull : Battle.EnemyHull, GetNavalFieldRemainingTurns(cell));
                if (player) Hull = Math.Max(0, Hull - loss);
                else Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - loss);
                Battle.RecordSettlement(CoreBattleSettlementKind.BloodField, player, loss, healthAfter: player ? Hull : Battle.EnemyHull);
                string action = (player ? "己舰" : "敌舰") + "停留血池，流失 " + loss + " 点船体（无视格挡，不触发付血收益）。";
                Battle.LastAction = string.IsNullOrEmpty(Battle.LastAction) ? action : Battle.LastAction + " " + action;
                if (ResolveBattleOutcome()) return true;
            }
            if (cell != null && cell.Kind == CoreNavalFieldKind.Curse && RecordNavalFieldTrigger(player, cell) &&
                !NavalFieldImmune(ship, CoreNavalFieldKind.Curse, FieldOwnerTurn(player)))
            {
                int layers = GetNavalFieldRemainingTurns(cell);
                if (player) ApplyNavalPlayerCurseMarks(layers);
                else ApplyNavalEnemyCurseMarks(layers);
                string action = (player ? "己舰" : "敌舰") + "停留禁域，获得 " + layers + " 层咒印。";
                Battle.LastAction = string.IsNullOrEmpty(Battle.LastAction) ? action : Battle.LastAction + " " + action;
                if (ResolveBattleOutcome()) return true;
            }
            if (cell != null && cell.Kind == CoreNavalFieldKind.Tide && RecordNavalFieldTrigger(player, cell) &&
                !NavalFieldImmune(ship, cell.Kind, FieldOwnerTurn(player)))
            {
                CoreBattleState committedBattle = Battle;
                var frames = new List<CoreNavalTacticalFrame> { committedBattle.TacticalFrame };
                bool moved = PushNavalShipCore(player, cell.DirectionX, cell.DirectionY, false, frames);
                if (!NavalFieldsActive)
                {
                    committedBattle.CommitTacticalMovement(frames);
                    return true;
                }
                if (moved)
                {
                    FinishNavalMovement(frames, player, false);
                    if (player) Battle.PlayerMovingTowardEnemy = false;
                    else Battle.EnemyMovingTowardPlayer = false;
                }
                string action = (player ? "己舰" : "敌舰") + "停留定向水流，回合末" + (moved ? "顺流移动 1 格。" : "推动受阻。");
                Battle.LastAction = string.IsNullOrEmpty(Battle.LastAction) ? action : Battle.LastAction + " " + action;
            }
            if (ship.BurnPreventionExpiresAtTurnEnd && ship.BurnPreventionExpiresAtOwnerTurn <= FieldOwnerTurn(player))
            {
                ship.BurnPreventionExpiresAtOwnerTurn = 0;
                ship.BurnPreventionExpiresAtTurnEnd = false;
            }
            ship.ArcanePiercing = ship.BloodAttackBonus = ship.TemporaryMana = 0;
            if (ship.SlowExpires <= FieldOwnerTurn(player)) ship.MovementSlow = 0;
            if (player)
                foreach (CoreNavalFieldShipState target in new[] { Battle.NavalFields.Player, Battle.NavalFields.Enemy })
                    if (target.TideImmunityExpiresAfterPlayerTurnEnd <= Battle.NavalFields.PlayerTurn)
                        target.TideImmunityExpiresAfterPlayerTurnEnd = 0;
            AdvanceNavalFieldDurations(player);
            if (!player) ClearEnemyNavalBossFieldImmunity(ship);
            return false;
        }

        private void AdvanceNavalFieldDurations(bool player)
        {
            CoreNavalFieldState state = Battle.NavalFields;
            if (player)
            {
                if (state.LastEndedPlayerTurn == state.PlayerTurn) return;
                state.LastEndedPlayerTurn = state.PlayerTurn;
                state.CompletedPlayerTurns = SchoolSum(state.CompletedPlayerTurns, 1);
            }
            else
            {
                if (state.LastEndedEnemyTurn == state.EnemyTurn) return;
                state.LastEndedEnemyTurn = state.EnemyTurn;
                state.CompletedEnemyTurns = SchoolSum(state.CompletedEnemyTurns, 1);
            }
            state.Cells.RemoveAll(cell =>
            {
                if (cell.PlayerOwned == player || cell.ExpiresAtOwnerTurn > FieldDurationClock(state, cell.PlayerOwned)) return false;
                state.RecordPresentation(CoreNavalFieldChangeKind.Expired, cell);
                return true;
            });
        }

        // Compatibility for older callers: forbidden fields add marks, never tick damage.
        public int NavalFieldCurseBonus(bool player, int marks) => 0;

        private bool RecordNavalFieldTrigger(bool player, CoreNavalFieldCell cell, bool movementTrigger = false)
        {
            string key = FieldTriggerKey(player, cell);
            if (Battle.NavalFields.TriggerLedger.Contains(key)) return false;
            Battle.NavalFields.TriggerLedger.Add(key);
            Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Triggered, cell, player, movementTrigger);
            return true;
        }

        private void TriggerNavalArcaneField(bool player, CoreNavalFieldCell cell)
        {
            if (cell == null || cell.Kind != CoreNavalFieldKind.Arcane || !RecordNavalFieldTrigger(player, cell)) return;
            int amount = GetNavalFieldRemainingTurns(cell);
            if (player) GainBattleMana(amount);
            else FieldShip(false).Mana = SchoolSum(FieldShip(false).Mana, amount);
        }

        private void ApplyNavalBloodPoolGuard(bool player, int consumedHealth)
        {
            if (!NavalFieldsActive || consumedHealth <= 0) return;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell == null || cell.Kind != CoreNavalFieldKind.Blood) return;
            int guard = CoreFieldTradeoffRules.BloodGuard(consumedHealth);
            if (player) AddSchoolGuard(guard);
            else Battle.EnemyBlock = SchoolSum(Battle.EnemyBlock, NavalExpansionEnemyBlockGain(guard, true));
            // Every health payment can trigger the pool, independently of movement and turn ledgers.
            Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Triggered, cell, player);
        }

        // Every movement landing in a thunder field adds its remaining duration.
        // These entries deliberately bypass the once-per-turn field ledger.
        private bool TriggerNavalFieldDamage(bool player, CoreNavalFieldCell cell, bool movementTrigger = false)
        {
            int amount = GetNavalFieldRemainingTurns(cell);
            if (amount <= 0) return false;
            Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Triggered, cell, player, movementTrigger);
            Battle.LastAction = (player ? "己舰" : "敌舰") + "进入雷栅，获得 " + amount + " 层连锁。";
            return GainNavalThunderChain(player, amount);
        }

        // Returns true only when the current active route must stop. Forced movement ignores that stop result.
        private bool NavalFieldsEntered(bool player, bool activeMovement)
        {
            if (!NavalFieldsActive) return false;
            CoreNavalFieldCell cell = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            if (cell == null) return false;
            if (cell.Kind == CoreNavalFieldKind.Thunder)
                return TriggerNavalFieldDamage(player, cell, true);
            return false;
        }

        private bool NavalForcedTargetValid(bool player, VoyagePoint target)
        {
            VoyagePoint other = player ? Battle.EnemyPositionMeters : Battle.PlayerPositionMeters;
            return NavalForcedTargetValid(target, other);
        }

        private static bool NavalForcedTargetValid(VoyagePoint target, VoyagePoint other)
        {
            if (!FieldCellValid(FieldGrid(target.X), FieldGrid(target.Y))) return false;
            return NavalGridDistance(target, other) > 0 && NavalPointDistance(target, other) >= MinimumNavalDistanceMeters;
        }

        private VoyagePoint NavalPlayerPositionForEnemyPlan
        {
            get
            {
                VoyagePoint position = Battle.PlayerPositionMeters;
                if (!NavalFieldsActive || !Battle.NavalFields.PlayerActing ||
                    Battle.NavalFields.LastEndedPlayerTurn == Battle.NavalFields.PlayerTurn) return position;
                CoreNavalFieldCell field = NavalFieldAt(position);
                if (field == null || field.Kind != CoreNavalFieldKind.Tide ||
                    Battle.NavalFields.TriggerLedger.Contains(FieldTriggerKey(true, field)) ||
                    NavalFieldImmune(FieldShip(true), field.Kind, FieldOwnerTurn(true))) return position;
                var target = new VoyagePoint(position.X + field.DirectionX * NavalGridCellMeters,
                    position.Y + field.DirectionY * NavalGridCellMeters);
                return NavalForcedTargetValid(true, target) && !ShouldRefuseNavalPush(true, target, false) ? target : position;
            }
        }

        private bool EnemyPlannedRamGeometryIsLegal => NavalPositionsAreOrthogonallyAdjacent(NavalPlayerPositionForEnemyPlan, Battle.EnemyPositionMeters) &&
            Math.Abs(NavalAngleDelta(Battle.EnemyHeadingDegrees, NavalBearing(Battle.EnemyPositionMeters, NavalPlayerPositionForEnemyPlan))) < NavalContactTolerance;

        private float EnemyPlannedRamTargetQuality => Math.Abs(NavalAngleDelta(Battle.PlayerHeadingDegrees,
            NavalBearing(NavalPlayerPositionForEnemyPlan, Battle.EnemyPositionMeters))) < NavalContactTolerance ? Battle.PlayerBowQuality : Battle.PlayerHullQuality;

        private bool ShouldRefuseNavalPush(bool player, VoyagePoint target, bool explicitlyRefuse)
        {
            if (FieldShip(player).AnchorCharges <= 0) return false;
            if (explicitlyRefuse || (player && Battle.NavalFields.PlayerRefuseForcedMovement)) return true;
            if (player) return false;
            CoreNavalFieldCell cell = NavalFieldAt(target);
            bool danger = cell != null && cell.Kind == CoreNavalFieldKind.Thunder &&
                !NavalFieldImmune(FieldShip(player), cell.Kind, FieldOwnerTurn(player));
            return danger || NavalGridDistance(target, Battle.PlayerPositionMeters) > NavalDistanceCells;
        }

        public bool PushNavalShip(bool player, int dx, int dy, bool refuseAnchor = false)
        {
            if (!NavalFieldsActive || !FieldDirectionValid(dx, dy)) return false;
            CoreBattleState committedBattle = Battle;
            var frames = new List<CoreNavalTacticalFrame> { Battle.TacticalFrame };
            bool moved = PushNavalShipCore(player, dx, dy, refuseAnchor, frames);
            if (!moved) return false;
            if (!NavalFieldsActive)
            {
                committedBattle.CommitTacticalMovement(frames);
                return moved;
            }
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
            if (!NavalForcedTargetValid(player, target) || NavalTideBlocksMovement(Battle.NavalFields, player, from, target)) return false;
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

    }
}



