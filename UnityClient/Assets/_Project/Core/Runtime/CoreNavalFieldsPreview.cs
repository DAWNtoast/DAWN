using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreNavalFieldPreviewEvent
    {
        public VoyagePoint PositionMeters { get; internal set; }
        public int Damage { get; internal set; }
        public string Description { get; internal set; }
    }
    public sealed class CoreNavalMovementFieldPreview
    {
        public int Damage { get; internal set; }
        public int HullDamage { get; internal set; }
        public int ActiveSteps { get; internal set; }
        public int FinalBlock { get; internal set; }
        public int AttackBonus { get; internal set; }
        public bool RuneClampConsumed { get; internal set; }
        public bool StopsAtThunder { get; internal set; }
        public bool Sinks { get; internal set; }
        public VoyagePoint FinalPositionMeters { get; internal set; }
        public VoyagePoint StopPositionMeters { get; internal set; }
        public List<CoreNavalFieldPreviewEvent> Events { get; } = new List<CoreNavalFieldPreviewEvent>();
        public string Summary { get; internal set; } = string.Empty;
    }

    public sealed partial class CoreVoyageSession
    {
        public CoreNavalMovementFieldPreview NavalMovementFieldPreview
        {
            get
            {
                EnsureNavalMovementPath();
                if (Battle != null && Battle.NavalAwaitingHeadingSelection)
                    return new CoreNavalMovementFieldPreview { FinalPositionMeters = Battle.PlayerPositionMeters, StopPositionMeters = Battle.PlayerPositionMeters };
                return PreviewNavalFields(navalMovementPath, true);
            }
        }

        private sealed class NavalFieldPreviewContext
        {
            internal CoreNavalFieldState State;
            internal int Block, Hull;
            internal bool RuneClampAvailable, RuneClampConsumed;
        }

        private static CoreNavalFieldCell PreviewFieldAt(CoreNavalFieldState state, VoyagePoint point) =>
            state.Cells.Find(cell => cell.X == FieldGrid(point.X) && cell.Y == FieldGrid(point.Y));

        private NavalFieldPreviewContext CreateNavalFieldPreviewContext(bool player)
        {
            var context = new NavalFieldPreviewContext { State = Battle.NavalFields.Copy(),
                Block = player ? Battle.PlayerBlock : Battle.EnemyBlock, Hull = player ? Hull : Battle.EnemyHull,
                RuneClampAvailable = Battle.NavalExpansion.RuneClamp };
            if (player || !context.State.PlayerActing) return context;
            // The visible enemy intent is quoted during the player's turn. Use the same
            // next-turn expiry, guard reset and underfoot benefits as actual execution.
            context.State.PlayerActing = false;
            context.State.EnemyTurn++;
            context.State.Cells.RemoveAll(cell => !cell.PlayerOwned && cell.ExpiresAtOwnerTurn <= context.State.EnemyTurn);
            CoreNavalFieldShipState ship = context.State.Enemy;
            if (ship.FireImmunityExpires <= context.State.EnemyTurn) ship.FireImmunityCharges = 0;
            if (ship.FieldImmunityExpires <= context.State.EnemyTurn) ship.FieldImmunityMask = ship.FieldImmunityExpires = 0;
            if (ship.AnchorExpires <= context.State.EnemyTurn) ship.AnchorCharges = 0;
            context.Block = 0;
            CoreNavalFieldCell field = PreviewFieldAt(context.State, Battle.EnemyPositionMeters);
            if (field != null && !field.PlayerOwned && (field.Kind == CoreNavalFieldKind.Blood || field.Kind == CoreNavalFieldKind.Arcane) &&
                !context.State.TriggerLedger.Contains(FieldTriggerKey(false, field)))
            {
                context.State.TriggerLedger.Add(FieldTriggerKey(false, field));
                if (field.Kind == CoreNavalFieldKind.Blood)
                {
                    context.Block = context.RuneClampAvailable ? 0 : 6;
                    context.RuneClampConsumed = context.RuneClampAvailable;
                    context.RuneClampAvailable = false;
                    ship.BloodAttackBonus = 4;
                }
                else { ship.TemporaryMana++; ship.ArcanePiercing = 4; }
            }
            return context;
        }

        private CoreNavalMovementFieldPreview PreviewNavalFields(IReadOnlyList<CoreNavalMovementNode> path, bool player,
            NavalFieldPreviewContext context = null)
        {
            var result = new CoreNavalMovementFieldPreview();
            if (Battle == null) return result;
            result.FinalPositionMeters = result.StopPositionMeters = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            if (!NavalFieldsActive) return result;
            context = context ?? CreateNavalFieldPreviewContext(player);
            CoreNavalFieldState state = context.State;
            CoreNavalFieldShipState ship = (player ? state.Player : state.Enemy).Copy();
            var ledger = new HashSet<string>(state.TriggerLedger);
            int block = context.Block;
            int hp = context.Hull;
            int initialHp = hp;
            bool runeAvailable = context.RuneClampAvailable, runeConsumed = context.RuneClampConsumed;
            int nextBlockBonus = player ? Battle.Schools.ArcaneNextBlockBonus : 0;
            result.FinalBlock = block;
            result.AttackBonus = ship.BloodAttackBonus;
            result.RuneClampConsumed = runeConsumed;
            if (hp <= 0) { result.Sinks = true; return result; }
            if (path == null || path.Count < 2) return result;
            for (int index = 1; index < path.Count; index++)
            {
                // The occupied endpoint is a ram, not ordinary field entry.
                if (player && NavalMovementTargetsEnemy(path[index].PositionMeters)) break;
                result.FinalPositionMeters = path[index].PositionMeters;
                result.ActiveSteps++;
                if (PreviewNavalFieldEntry(player, result, state, ship, ledger, ref block, ref hp, ref runeAvailable, ref runeConsumed, ref nextBlockBonus, true)) break;
            }
            result.StopPositionMeters = result.FinalPositionMeters;
            if (hp > 0 && result.ActiveSteps > 0)
            {
                CoreNavalFieldCell field = PreviewFieldAt(state, result.FinalPositionMeters);
                if (field != null && field.Kind == CoreNavalFieldKind.Tide && ledger.Add(FieldTriggerKey(player, field)))
                {
                    var target = new VoyagePoint(result.FinalPositionMeters.X + field.DirectionX * NavalGridCellMeters,
                        result.FinalPositionMeters.Y + field.DirectionY * NavalGridCellMeters);
                    bool blocked = !NavalForcedTargetValid(player, target);
                    bool immune = NavalFieldImmune(ship, field.Kind, player ? state.PlayerTurn : state.EnemyTurn);
                    bool refuse = ship.AnchorCharges > 0 && (player ? state.PlayerRefuseForcedMovement :
                        (NavalGridDistance(target, Battle.PlayerPositionMeters) > NavalGridDistance(result.FinalPositionMeters, Battle.PlayerPositionMeters) ||
                         PreviewFieldHarmful(false, target, state, ship, ledger)));
                    result.Events.Add(new CoreNavalFieldPreviewEvent { PositionMeters = result.FinalPositionMeters,
                        Description = immune ? "水流推动免疫" : blocked ? "水流推动受阻" : refuse ? "定锚拒绝水流推动" : "水流推动至（" + FieldGrid(target.X) + "，" + FieldGrid(target.Y) + "）" });
                    if (!immune && !blocked && !refuse)
                    {
                        result.FinalPositionMeters = target;
                        PreviewNavalFieldEntry(player, result, state, ship, ledger, ref block, ref hp, ref runeAvailable, ref runeConsumed, ref nextBlockBonus, false);
                    }
                }
            }
            result.FinalBlock = block;
            result.AttackBonus = ship.BloodAttackBonus;
            result.RuneClampConsumed = runeConsumed;
            result.HullDamage = initialHp - hp;
            result.Sinks = hp <= 0;
            if (result.Events.Count > 0)
            {
                var parts = new List<string>();
                foreach (CoreNavalFieldPreviewEvent entry in result.Events) parts.Add(entry.Description);
                result.Summary = string.Join("；", parts) + "；最终落点（" + FieldGrid(result.FinalPositionMeters.X) + "，" + FieldGrid(result.FinalPositionMeters.Y) + "）" +
                    (result.Sinks ? "，船体归零，后续航线终止" : string.Empty);
            }
            return result;
        }

        private bool PreviewFieldHarmful(bool player, VoyagePoint point, CoreNavalFieldState state,
            CoreNavalFieldShipState ship, HashSet<string> ledger)
        {
            CoreNavalFieldCell cell = PreviewFieldAt(state, point);
            return cell != null && (cell.Kind == CoreNavalFieldKind.Fire || cell.Kind == CoreNavalFieldKind.Thunder) &&
                !ledger.Contains(FieldTriggerKey(player, cell)) &&
                !NavalFieldImmune(ship, cell.Kind, player ? state.PlayerTurn : state.EnemyTurn) &&
                (cell.Kind != CoreNavalFieldKind.Fire || ship.FireImmunityCharges <= 0);
        }

        private bool PreviewNavalFieldEntry(bool player, CoreNavalMovementFieldPreview result, CoreNavalFieldState state,
            CoreNavalFieldShipState ship, HashSet<string> ledger, ref int block, ref int hp,
            ref bool runeAvailable, ref bool runeConsumed, ref int nextBlockBonus, bool active)
        {
            CoreNavalFieldCell cell = PreviewFieldAt(state, result.FinalPositionMeters);
            if (cell == null) return false;
            if (cell.Kind == CoreNavalFieldKind.Fire || cell.Kind == CoreNavalFieldKind.Thunder)
            {
                if (!ledger.Add(FieldTriggerKey(player, cell))) return false;
                int raw = cell.Kind == CoreNavalFieldKind.Fire ? 2 : 3;
                bool protectedField = NavalFieldImmune(ship, cell.Kind, player ? state.PlayerTurn : state.EnemyTurn);
                bool immune = protectedField || (cell.Kind == CoreNavalFieldKind.Fire && ship.FireImmunityCharges > 0);
                if (immune && !protectedField) ship.FireImmunityCharges--;
                int absorbed = immune ? 0 : Math.Min(block, raw);
                int damage = immune ? 0 : raw - absorbed;
                block -= absorbed;
                hp = Math.Max(0, hp - damage);
                result.Damage += immune ? 0 : raw;
                bool stop = active && cell.Kind == CoreNavalFieldKind.Thunder && !immune;
                result.StopsAtThunder |= stop;
                result.Events.Add(new CoreNavalFieldPreviewEvent { PositionMeters = cell.PositionMeters, Damage = immune ? 0 : raw,
                    Description = (cell.Kind == CoreNavalFieldKind.Fire ? "火场" : "雷栅") + "（" + cell.X + "，" + cell.Y + "）" +
                        (immune ? "免疫" : "伤害 " + raw + "（格挡 " + absorbed + "，船体 " + damage + "）") + (stop ? "，在此停航" : string.Empty) });
                return stop || hp <= 0;
            }
            if (cell.PlayerOwned == player && (cell.Kind == CoreNavalFieldKind.Arcane || cell.Kind == CoreNavalFieldKind.Blood) &&
                ledger.Add(FieldTriggerKey(player, cell)))
            {
                int guard = player || !runeAvailable ? 6 : 0;
                if (cell.Kind == CoreNavalFieldKind.Blood)
                {
                    guard = SchoolSum(guard, nextBlockBonus);
                    nextBlockBonus = 0;
                    block = SchoolSum(block, guard);
                    ship.BloodAttackBonus = 4;
                    if (!player && runeAvailable) { runeConsumed = true; runeAvailable = false; }
                }
                else { ship.ArcanePiercing = 4; ship.TemporaryMana++; }
                result.Events.Add(new CoreNavalFieldPreviewEvent { PositionMeters = cell.PositionMeters,
                    Description = cell.Kind == CoreNavalFieldKind.Blood ? "血誓阵：格挡 +" + guard + "、首段攻击 +4" : "折射阵：临时法力 +1、首段穿盾 4" });
            }
            return false;
        }

        public string GetEnemyNavalFieldIntent() => Battle == null ? string.Empty : GetEnemyNavalFieldIntent(BuildEnemyPlan().Move);
        public string GetEnemyNavalFieldIntent(float meters) => GetEnemyNavalFieldPreview(meters).Summary;
        public CoreNavalMovementFieldPreview GetEnemyNavalFieldPreview(float meters)
        {
            if (!NavalFieldsActive) return new CoreNavalMovementFieldPreview();
            NavalFieldPreviewContext context = CreateNavalFieldPreviewContext(false);
            if (context.Hull <= 0) return new CoreNavalMovementFieldPreview { Sinks = true,
                FinalPositionMeters = Battle.EnemyPositionMeters, StopPositionMeters = Battle.EnemyPositionMeters,
                Summary = "敌舰将在行动前沉没，不再航行" };
            var path = BuildNavalFieldEnemyPath(meters, context);
            CoreNavalMovementFieldPreview preview = PreviewNavalFields(path, false, context);
            CoreNavalFieldCell ending = PreviewFieldAt(context.State, preview.FinalPositionMeters);
            int bonus = EnemyEndFieldRisk(ending, path.Count == 1, context);
            if (bonus > 0 && !preview.Sinks)
            {
                string description = ending.Kind == CoreNavalFieldKind.Curse ? "域内回合末咒印追加 " + bonus + " 伤害" : "停留火场回合末结算 2 场地伤害";
                preview.Summary += (preview.Summary.Length > 0 ? "；" : string.Empty) + description;
            }
            return preview;
        }

        private int EnemyEndFieldRisk(CoreNavalFieldCell cell, bool unmoved, NavalFieldPreviewContext context)
        {
            if (cell == null || context.State.TriggerLedger.Contains(FieldTriggerKey(false, cell))) return 0;
            if (NavalFieldImmune(context.State.Enemy, cell.Kind, context.State.EnemyTurn)) return 0;
            if (cell.Kind == CoreNavalFieldKind.Curse && cell.PlayerOwned && Battle.Schools.CurseTurns > 0)
                return 2;
            if (unmoved && cell.Kind == CoreNavalFieldKind.Fire && context.State.Enemy.FireImmunityCharges <= 0)
                return Math.Max(0, 2 - context.Block);
            return 0;
        }

        // Enumerate short cardinal routes and trade approach/contact value against actual hull losses.
        // Fields remain traversable; sufficiently valuable contact can outweigh damage.
        private List<CoreNavalMovementNode> BuildNavalFieldEnemyPath(float meters, NavalFieldPreviewContext context = null)
        {
            context = context ?? CreateNavalFieldPreviewContext(false);
            var start = new CoreNavalMovementNode(Battle.EnemyPositionMeters, Battle.EnemyHeadingDegrees);
            var best = new List<CoreNavalMovementNode> { start };
            int steps = Math.Min(8, Math.Max(0, (int)(QuantizeNavalEnemyTravel(meters) / NavalGridCellMeters) - context.State.Enemy.MovementSlow));
            float bestScore = EnemyFieldPathScore(best, context);
            var path = new List<CoreNavalMovementNode> { start };
            SearchNavalFieldEnemyPath(path, steps, ref best, ref bestScore, context);
            return best;
        }

        private float EnemyFieldPathScore(List<CoreNavalMovementNode> path, NavalFieldPreviewContext context)
        {
            CoreNavalMovementFieldPreview preview = PreviewNavalFields(path, false, context);
            if (preview.Sinks) return -100000f;
            int endingRisk = EnemyEndFieldRisk(PreviewFieldAt(context.State, preview.FinalPositionMeters), path.Count == 1, context);
            if (preview.HullDamage + endingRisk >= context.Hull) return -100000f;
            int distance = NavalGridDistance(preview.FinalPositionMeters, Battle.PlayerPositionMeters);
            float score = -distance * 3f + (distance <= 4 ? 3f : 0f) + (distance == 1 ? 6f : 0f) - (preview.HullDamage + endingRisk) * 1.8f -
                Math.Max(0, preview.Damage - preview.HullDamage) * .35f - (preview.StopsAtThunder ? .7f : 0f);
            if (path.Count > 1)
            {
                float bearing = NavalBearing(preview.FinalPositionMeters, Battle.PlayerPositionMeters);
                score -= Math.Abs(NavalAngleDelta(path[path.Count - 1].HeadingDegrees, bearing)) / 180f;
                score -= (path.Count - 1) * .02f;
            }
            return score;
        }

        private void SearchNavalFieldEnemyPath(List<CoreNavalMovementNode> path, int remaining,
            ref List<CoreNavalMovementNode> best, ref float bestScore, NavalFieldPreviewContext context)
        {
            if (remaining <= 0) return;
            CoreNavalMovementNode last = path[path.Count - 1];
            foreach (float turn in new[] { 0f, 90f, -90f })
            {
                float heading = NormalizeNavalHeading(last.HeadingDegrees + turn);
                float radians = heading * NavalDegreesToRadians;
                var next = new VoyagePoint(last.PositionMeters.X + (float)Math.Round(Math.Cos(radians)) * NavalGridCellMeters,
                    last.PositionMeters.Y + (float)Math.Round(Math.Sin(radians)) * NavalGridCellMeters);
                if (!NavalForcedTargetValid(false, next)) continue;
                if (path.Exists(node => NavalPointDistance(node.PositionMeters, next) < .01f)) continue;
                path.Add(new CoreNavalMovementNode(next, heading));
                float score = EnemyFieldPathScore(path, context);
                if (score > bestScore + .001f) { bestScore = score; best = new List<CoreNavalMovementNode>(path); }
                CoreNavalMovementFieldPreview preview = PreviewNavalFields(path, false, context);
                if (!preview.Sinks && !preview.StopsAtThunder) SearchNavalFieldEnemyPath(path, remaining - 1, ref best, ref bestScore, context);
                path.RemoveAt(path.Count - 1);
            }
        }
    }
}



