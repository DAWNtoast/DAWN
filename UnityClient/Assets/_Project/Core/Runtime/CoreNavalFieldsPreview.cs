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
        public int FinalThunderChain { get; internal set; }
        public int AttackBonus { get; internal set; }
        public bool RuneClampConsumed { get; internal set; }
        public bool StopsAtThunder { get; internal set; }
        public bool Sinks { get; internal set; }
        public bool EntersBoarding { get; internal set; }
        public VoyagePoint FinalPositionMeters { get; internal set; }
        public VoyagePoint StopPositionMeters { get; internal set; }
        public VoyagePoint EndTurnPositionMeters { get; internal set; }
        internal HashSet<string> TriggeredFields;
        internal CoreNavalFieldShipState FinalShip;
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
            internal VoyagePoint PlayerPosition;
            internal bool RuneClampAvailable;
        }

        private static CoreNavalFieldCell PreviewFieldAt(CoreNavalFieldState state, VoyagePoint point) =>
            state.Cells.Find(cell => cell.X == FieldGrid(point.X) && cell.Y == FieldGrid(point.Y));

        private NavalFieldPreviewContext CreateNavalFieldPreviewContext(bool player)
        {
            var context = new NavalFieldPreviewContext { State = Battle.NavalFields.Copy(),
                Block = player ? Battle.PlayerBlock : Battle.EnemyBlock, Hull = player ? Hull : Battle.EnemyHull,
                RuneClampAvailable = Battle.NavalExpansion.RuneClamp, PlayerPosition = NavalPlayerPositionForEnemyPlan };
            context.State.Player.ThunderChain = Battle.Schools.ThunderChain;
            if (CoreCurseRules.ReachesExecutionThreshold(player ? Battle.PlayerCurseMarks : Battle.Schools.CurseMarks,
                context.Hull, player ? CoreCurseRules.DefaultExecutionThresholdPercent : Battle.Schools.EnemyCurseExecutionThresholdPercent)) context.Hull = 0;
            if (player || !context.State.PlayerActing) return context;
            // The visible enemy intent is quoted during the player's turn. Use the same
            // next-turn guard/resource reset and underfoot benefits as actual execution.
            if (context.State.LastEndedPlayerTurn != context.State.PlayerTurn)
            {
                context.State.LastEndedPlayerTurn = context.State.PlayerTurn;
                context.State.CompletedPlayerTurns = SchoolSum(context.State.CompletedPlayerTurns, 1);
                foreach (CoreNavalFieldShipState affected in new[] { context.State.Player, context.State.Enemy })
                    if (affected.TideImmunityExpiresAfterPlayerTurnEnd <= context.State.PlayerTurn) affected.TideImmunityExpiresAfterPlayerTurnEnd = 0;
                context.State.Cells.RemoveAll(cell => !cell.PlayerOwned &&
                    cell.ExpiresAtOwnerTurn <= context.State.CompletedPlayerTurns);
            }
            context.State.PlayerActing = false;
            context.State.EnemyTurn++;
            CoreNavalFieldShipState ship = context.State.Enemy;
            ApplyEnemyNavalBossFieldImmunity(ship);
            ship.Mana = ship.ManaLimit;
            ship.ArcanePiercing = ship.TemporaryMana = 0;
            if (ship.FireImmunityExpires <= context.State.EnemyTurn) ship.FireImmunityCharges = 0;
            if (ship.FieldImmunityExpires <= context.State.EnemyTurn) ship.FieldImmunityMask = ship.FieldImmunityExpires = 0;
            if (ship.AnchorExpires <= context.State.EnemyTurn) ship.AnchorCharges = 0;
            context.Block = 0;
            CoreNavalFieldCell field = PreviewFieldAt(context.State, Battle.EnemyPositionMeters);
            if (field == null || field.Kind != CoreNavalFieldKind.Thunder) ship.ThunderChain = 0;
            if (field != null && field.Kind == CoreNavalFieldKind.Arcane &&
                !context.State.TriggerLedger.Contains(FieldTriggerKey(false, field)))
            {
                context.State.TriggerLedger.Add(FieldTriggerKey(false, field));
                ship.Mana = SchoolSum(ship.Mana, Math.Max(0, field.ExpiresAtOwnerTurn - FieldDurationClock(context.State, field.PlayerOwned)));
            }
            return context;
        }

        private CoreNavalMovementFieldPreview PreviewNavalFields(IReadOnlyList<CoreNavalMovementNode> path, bool player,
            NavalFieldPreviewContext context = null)
        {
            var result = new CoreNavalMovementFieldPreview();
            if (Battle == null) return result;
            result.FinalPositionMeters = result.StopPositionMeters = result.EndTurnPositionMeters = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            if (!NavalFieldsActive) return result;
            context = context ?? CreateNavalFieldPreviewContext(player);
            CoreNavalFieldState state = context.State;
            CoreNavalFieldShipState ship = (player ? state.Player : state.Enemy).Copy();
            var ledger = new HashSet<string>(state.TriggerLedger);
            int block = context.Block;
            int hp = context.Hull;
            int initialHp = hp;
            bool runeAvailable = context.RuneClampAvailable, runeConsumed = false;
            int nextBlockBonus = player ? Battle.Schools.ArcaneNextBlockBonus : 0;
            result.FinalBlock = block;
            result.FinalThunderChain = ship.ThunderChain;
            result.TriggeredFields = ledger;
            result.FinalShip = ship;
            result.AttackBonus = 0;
            result.RuneClampConsumed = runeConsumed;
            if (hp <= 0) { result.Sinks = true; return result; }
            if (path == null || path.Count < 2) return result;
            for (int index = 1; index < path.Count; index++)
            {
                // The occupied endpoint is a ram, not ordinary field entry.
                if (player && NavalMovementTargetsEnemy(path[index].PositionMeters)) break;
                if (NavalTideBlocksMovement(state, player, result.FinalPositionMeters, path[index].PositionMeters)) break;
                result.FinalPositionMeters = path[index].PositionMeters;
                result.ActiveSteps++;
                if (PreviewNavalFieldEntry(player, result, state, ship, ledger, ref block, ref hp, ref runeAvailable, ref runeConsumed, ref nextBlockBonus, true)) break;
            }
            result.StopPositionMeters = result.FinalPositionMeters;
            result.EndTurnPositionMeters = result.FinalPositionMeters;
            result.FinalBlock = block;
            result.FinalThunderChain = ship.ThunderChain;
            result.AttackBonus = 0;
            result.RuneClampConsumed = runeConsumed;
            result.HullDamage = initialHp - hp;
            result.Sinks = hp <= 0;
            if (result.Events.Count > 0)
            {
                var parts = new List<string>();
                foreach (CoreNavalFieldPreviewEvent entry in result.Events) parts.Add(entry.Description);
                result.Summary = string.Join("；", parts) + "；最终落点（" + FieldGrid(result.FinalPositionMeters.X) + "，" + FieldGrid(result.FinalPositionMeters.Y) + "）" +
                    (result.Sinks ? UsesSparringBattleText
                        ? "，" + (player ? "己方" : "对方") + "达到认输条件，结束切磋"
                        : "，船体归零，后续航线终止" : string.Empty);
            }
            return result;
        }

        private bool PreviewFieldHarmful(bool player, VoyagePoint point, CoreNavalFieldState state,
            CoreNavalFieldShipState ship, HashSet<string> ledger)
        {
            CoreNavalFieldCell cell = PreviewFieldAt(state, point);
            return cell != null && cell.Kind == CoreNavalFieldKind.Thunder &&
                !NavalFieldImmune(ship, cell.Kind, player ? state.PlayerTurn : state.EnemyTurn);
        }

        private bool PreviewNavalFieldEntry(bool player, CoreNavalMovementFieldPreview result, CoreNavalFieldState state,
            CoreNavalFieldShipState ship, HashSet<string> ledger, ref int block, ref int hp,
            ref bool runeAvailable, ref bool runeConsumed, ref int nextBlockBonus, bool active)
        {
            CoreNavalFieldCell cell = PreviewFieldAt(state, result.FinalPositionMeters);
            if (cell == null) return false;
            if (cell.Kind == CoreNavalFieldKind.Thunder)
            {
                bool immune = NavalFieldImmune(ship, cell.Kind, player ? state.PlayerTurn : state.EnemyTurn);
                int added = Math.Max(0, cell.ExpiresAtOwnerTurn - FieldDurationClock(state, cell.PlayerOwned));
                int previousChain = ship.ThunderChain;
                ship.ThunderChain = SchoolSum(ship.ThunderChain, added);
                bool overloaded = CoreThunderRules.TriggersOverload(ship.ThunderChain, ship.ThunderChain - previousChain);
                int raw = overloaded && !immune ? CoreThunderRules.OverloadDamage : 0;
                int absorbed = Math.Min(block, raw);
                int damage = raw - absorbed;
                block -= absorbed;
                hp = Math.Max(0, hp - damage);
                bool curseExecutes = CoreCurseRules.ReachesExecutionThreshold(
                    player ? Battle.PlayerCurseMarks : Battle.Schools.CurseMarks, hp,
                    player ? CoreCurseRules.DefaultExecutionThresholdPercent : Battle.Schools.EnemyCurseExecutionThresholdPercent);
                if (curseExecutes) hp = 0;
                result.Damage = SchoolSum(result.Damage, raw);
                result.Events.Add(new CoreNavalFieldPreviewEvent { PositionMeters = cell.PositionMeters, Damage = raw,
                    Description = "雷栅" + "（" + cell.X + "，" + cell.Y + "）" +
                        "连锁 +" + added + "（共 " + ship.ThunderChain + "）" +
                            (raw > 0 ? "，过载伤害 " + raw + "（格挡 " + absorbed + "，船体 " + damage + "）" :
                             overloaded && immune ? "，免疫过载伤害" : string.Empty) +
                        (curseExecutes ? UsesSparringBattleText
                            ? "，咒印使" + (player ? "己方" : "对方") + "立即达到认输条件"
                            : "，咒印立即消灭" : string.Empty) });
                return hp <= 0;
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
                Summary = UsesSparringBattleText ? "对方将在行动前认输，结束切磋，不再航行" :
                    "敌舰将在行动前沉没，不再航行" };
            var path = BuildNavalFieldEnemyPath(meters, context);
            CoreNavalMovementFieldPreview preview = PreviewNavalFields(path, false, context);
            if (EnemyPreviewEntersBoarding(preview, context))
            {
                preview.EntersBoarding = true;
                return preview;
            }
            PreviewNavalBossPlacedField(context.State, preview.FinalPositionMeters, context.PlayerPosition);
            CoreNavalFieldCell ending = PreviewFieldAt(context.State, preview.FinalPositionMeters);
            int curseLayers = EnemyEndFieldCurse(ending, context);
            int fireLayers = EnemyEndFieldBurn(ending, context);
            int bloodLoss = EnemyEndFieldBloodLoss(ending, context);
            if (bloodLoss > 0 && !preview.Sinks)
            {
                int health = EnemyHealthAfterEndBurn(preview, context);
                int afterLoss = Math.Max(0, health - bloodLoss);
                string description = health <= 0 ? (UsesSparringBattleText
                    ? "对方在回合末燃烧结算时达到认输条件，结束切磋，不再结算血池"
                    : "回合末燃烧结算时被消灭，不再结算血池") :
                    "停留血池回合末流失 " + Math.Min(health, bloodLoss) + " 点船体（无视格挡，不返格挡）" +
                    (afterLoss <= 0 || CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks, afterLoss,
                        Battle.Schools.EnemyCurseExecutionThresholdPercent)
                        ? (UsesSparringBattleText ? "，对方达到认输条件，结束切磋" : "，被消灭") : string.Empty);
                preview.Summary += (preview.Summary.Length > 0 ? "；" : string.Empty) + description;
            }
            if ((curseLayers > 0 || fireLayers > 0) && !preview.Sinks)
            {
                string description = EnemyHealthAfterEndBurn(preview, context) <= 0 ? (UsesSparringBattleText
                    ? "对方在回合末燃烧结算时达到认输条件，结束切磋，不再结算所在场地"
                    : "回合末燃烧结算时被消灭，不再结算所在场地") :
                    ending.Kind == CoreNavalFieldKind.Curse ? "停留禁域回合末获得 " + curseLayers + " 层咒印" +
                    (EnemyEndFieldRisk(ending, preview, context) > 0
                        ? (UsesSparringBattleText ? "，咒印使对方达到认输条件，结束切磋" : "，触发咒印消灭") : string.Empty) :
                    "停留火场回合末获得 " + fireLayers + " 层燃烧";
                preview.Summary += (preview.Summary.Length > 0 ? "；" : string.Empty) + description;
            }
            CoreNavalMovementFieldPreview endCurrent = PreviewEnemyEndCurrent(preview, context);
            preview.EndTurnPositionMeters = endCurrent.FinalPositionMeters;
            if (endCurrent.Summary.Length > 0)
                preview.Summary += (preview.Summary.Length > 0 ? "；" : string.Empty) + endCurrent.Summary;
            return preview;
        }

        private CoreNavalMovementFieldPreview PreviewEnemyEndCurrent(CoreNavalMovementFieldPreview movement, NavalFieldPreviewContext context)
        {
            var result = new CoreNavalMovementFieldPreview { FinalPositionMeters = movement.FinalPositionMeters };
            CoreNavalFieldCell field = PreviewFieldAt(context.State, movement.FinalPositionMeters);
            if (movement.Sinks || field == null || field.Kind != CoreNavalFieldKind.Tide ||
                context.State.TriggerLedger.Contains(FieldTriggerKey(false, field))) return result;
            int hp = EnemyHealthAfterEndBurn(movement, context);
            if (hp <= 0)
            {
                result.Sinks = true;
                result.Summary = UsesSparringBattleText
                    ? "对方在回合末燃烧结算时达到认输条件，结束切磋，不再受水流推动"
                    : "回合末燃烧结算时被消灭，不再受水流推动";
                return result;
            }
            CoreNavalFieldShipState ship = (movement.FinalShip ?? context.State.Enemy).Copy();
            var ledger = new HashSet<string>(movement.TriggeredFields ?? new HashSet<string>(context.State.TriggerLedger));
            var target = new VoyagePoint(movement.FinalPositionMeters.X + field.DirectionX * NavalGridCellMeters,
                movement.FinalPositionMeters.Y + field.DirectionY * NavalGridCellMeters);
            bool blocked = !NavalForcedTargetValid(target, context.PlayerPosition);
            bool immune = NavalFieldImmune(ship, field.Kind, context.State.EnemyTurn);
            bool refuse = ship.AnchorCharges > 0 && (NavalGridDistance(target, context.PlayerPosition) >
                NavalGridDistance(movement.FinalPositionMeters, context.PlayerPosition) || PreviewFieldHarmful(false, target, context.State, ship, ledger));
            result.Summary = immune ? "回合末免疫水流推动" : blocked ? "回合末水流推动受阻" : refuse ? "回合末定锚拒绝水流推动" :
                "回合末顺流移动至（" + FieldGrid(target.X) + "，" + FieldGrid(target.Y) + "）";
            if (immune || blocked || refuse) return result;
            result.FinalPositionMeters = target;
            int initialHp = hp;
            EnemyBattlePlan plan = BuildEnemyPlan();
            int block = Math.Max(0, SchoolSum(movement.FinalBlock, movement.RuneClampConsumed ?
                Math.Max(0, plan.Guard) : NavalExpansionEnemyBlockGain(plan.Guard)) - Battle.EnemyBurnTurns);
            bool runeAvailable = context.RuneClampAvailable && !movement.RuneClampConsumed && plan.Guard <= 0;
            bool runeConsumed = !runeAvailable;
            int nextBlockBonus = 0;
            PreviewNavalFieldEntry(false, result, context.State, ship, ledger, ref block, ref hp,
                ref runeAvailable, ref runeConsumed, ref nextBlockBonus, false);
            result.HullDamage = initialHp - hp;
            result.FinalThunderChain = ship.ThunderChain;
            result.FinalBlock = block;
            result.Sinks = hp <= 0;
            foreach (CoreNavalFieldPreviewEvent entry in result.Events) result.Summary += "；" + entry.Description;
            return result;
        }

        private int EnemyEndFieldCurse(CoreNavalFieldCell cell, NavalFieldPreviewContext context)
        {
            if (cell == null || cell.Kind != CoreNavalFieldKind.Curse ||
                context.State.TriggerLedger.Contains(FieldTriggerKey(false, cell))) return 0;
            if (NavalFieldImmune(context.State.Enemy, cell.Kind, context.State.EnemyTurn)) return 0;
            return Math.Max(0, cell.ExpiresAtOwnerTurn - FieldDurationClock(context.State, cell.PlayerOwned));
        }

        private int EnemyHealthAfterEndBurn(CoreNavalMovementFieldPreview preview, NavalFieldPreviewContext context)
        {
            int health = Math.Max(0, context.Hull - preview.HullDamage);
            if (health <= 0) return 0;
            EnemyBattlePlan plan = BuildEnemyPlan();
            int guard = NavalEnemyActionGuard(plan, preview, context);
            bool canAct = plan.Action != EnemyBattleAction.Cannon ||
                NavalCannonInRange(context.PlayerPosition, preview.FinalPositionMeters);
            int bloodCost = canAct ? plan.BossMove?.BloodCost ?? 0 : 0;
            if (bloodCost > 0 && health > bloodCost)
            {
                health -= bloodCost;
                if (CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks, health,
                    Battle.Schools.EnemyCurseExecutionThresholdPercent)) return 0;
            }
            int repair = Math.Min(Math.Max(0, Battle.EnemyMaxHull - health), Math.Max(0, plan.Repair));
            if (IsBattleHealingBlocked(false)) repair = 0;
            health = SchoolSum(health, repair);
            int lifeSteal = plan.BossMove?.LifeStealPercent ?? 0;
            if (lifeSteal > 0 && !IsBattleHealingBlocked(false) && plan.Action == EnemyBattleAction.Cannon &&
                NavalCannonInRange(context.PlayerPosition, preview.FinalPositionMeters))
            {
                int attack = EnemyCannonDamage(NavalBossAttack(plan, preview), false, preview);
                int piercing = NavalBossPiercing(plan, attack);
                int actual = Math.Min(Hull, Math.Max(0, attack - Math.Min(Battle.PlayerBlock, attack - piercing)));
                int restored = (int)Math.Min(int.MaxValue, (long)actual * lifeSteal / 100);
                health = (int)Math.Min(Battle.EnemyMaxHull, (long)health + restored);
            }
            health = Math.Max(0, health - Math.Max(0, Battle.EnemyBurnTurns - guard));
            return CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks, health,
                Battle.Schools.EnemyCurseExecutionThresholdPercent) ? 0 : health;
        }

        private bool EnemyPreviewEntersBoarding(CoreNavalMovementFieldPreview preview, NavalFieldPreviewContext context)
        {
            if (preview.Sinks || BuildEnemyPlan().Action != EnemyBattleAction.Ram || !EnemyPlannedRamGeometryIsLegal ||
                NavalPointDistance(preview.FinalPositionMeters, Battle.EnemyPositionMeters) > .01f) return false;
            RamDamage(Battle.EnemyBowQuality, EnemyPlannedRamTargetQuality, out int damage, out int recoil);
            damage = NavalExpansionEnemyAttackDamage(NavalFieldAttackDamage(ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage)),
                false, true, context.State, preview.FinalPositionMeters, context.PlayerPosition));
            int playerHealth = Math.Max(0, Hull - damage);
            int enemyHealth = Math.Max(0, context.Hull - preview.HullDamage - recoil);
            return playerHealth > 0 && enemyHealth > 0 &&
                !CoreCurseRules.ReachesExecutionThreshold(Battle.PlayerCurseMarks, playerHealth) &&
                !CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks, enemyHealth,
                    Battle.Schools.EnemyCurseExecutionThresholdPercent);
        }

        private int EnemyEndFieldRisk(CoreNavalFieldCell cell, CoreNavalMovementFieldPreview preview, NavalFieldPreviewContext context)
        {
            if (EnemyPreviewEntersBoarding(preview, context)) return 0;
            int health = EnemyHealthAfterEndBurn(preview, context);
            int bloodLoss = Math.Min(health, EnemyEndFieldBloodLoss(cell, context));
            health -= bloodLoss;
            int marks = SchoolSum(Battle.Schools.CurseMarks, EnemyEndFieldCurse(cell, context));
            if (health <= 0 || CoreCurseRules.ReachesExecutionThreshold(marks, health,
                Battle.Schools.EnemyCurseExecutionThresholdPercent)) return Math.Max(0, context.Hull - preview.HullDamage);
            CoreNavalMovementFieldPreview current = PreviewEnemyEndCurrent(preview, context);
            return current.Sinks ? Math.Max(0, context.Hull - preview.HullDamage) : SchoolSum(bloodLoss, current.HullDamage);
        }

        private int EnemyEndFieldBloodLoss(CoreNavalFieldCell cell, NavalFieldPreviewContext context)
        {
            if (cell == null || cell.Kind != CoreNavalFieldKind.Blood ||
                NavalFieldImmune(context.State.Enemy, CoreNavalFieldKind.Blood, context.State.EnemyTurn) ||
                context.State.TriggerLedger.Contains(FieldTriggerKey(false, cell))) return 0;
            return Math.Max(0, cell.ExpiresAtOwnerTurn - FieldDurationClock(context.State, cell.PlayerOwned));
        }

        private int EnemyEndFieldBurn(CoreNavalFieldCell cell, NavalFieldPreviewContext context)
        {
            if (cell == null || cell.Kind != CoreNavalFieldKind.Fire ||
                context.State.TriggerLedger.Contains(FieldTriggerKey(false, cell))) return 0;
            CoreNavalFieldShipState enemy = context.State.Enemy;
            if (NavalFieldImmune(enemy, CoreNavalFieldKind.Fire, context.State.EnemyTurn)) return 0;
            if (enemy.BurnPreventionExpiresAtOwnerTurn > context.State.EnemyTurn ||
                (enemy.BurnPreventionExpiresAtTurnEnd && enemy.BurnPreventionExpiresAtOwnerTurn == context.State.EnemyTurn)) return 0;
            int remaining = Math.Max(0, cell.ExpiresAtOwnerTurn - FieldDurationClock(context.State, cell.PlayerOwned));
            return Battle.NavalExpansion.OilPlayerEndsRemaining > 0 ? SchoolSum(remaining, remaining) : remaining;
        }

        // These two planning hooks never call BuildEnemyPlan: authored boss decisions use them.
        private bool NavalBossShouldAvoidOccupiedField()
        {
            if (!NavalFieldsActive) return false;
            NavalFieldPreviewContext context = CreateNavalFieldPreviewContext(false);
            CoreNavalFieldCell field = PreviewFieldAt(context.State, Battle.EnemyPositionMeters);
            int bloodLoss = EnemyEndFieldBloodLoss(field, context);
            if (bloodLoss > 0)
                return EnemyBloodPoolBenefit(field, context, context.Hull, false) <= bloodLoss * 1.8f;
            return EnemyFieldDeferredPenalty(field, context, context.State.Enemy.ThunderChain) > 0f;
        }

        private bool NavalBossShouldHoldOccupiedField(float distance)
        {
            if (!NavalFieldsActive || distance > NavalCannonRangeCells * NavalGridCellMeters) return false;
            NavalFieldPreviewContext context = CreateNavalFieldPreviewContext(false);
            CoreNavalFieldCell field = PreviewFieldAt(context.State, Battle.EnemyPositionMeters);
            float benefit = EnemyFieldPositionBenefit(field, context, context.State.Enemy.ThunderChain, context.Hull, false);
            return EnemyFieldDeferredPenalty(field, context, context.State.Enemy.ThunderChain) <= 0f &&
                benefit > EnemyEndFieldBloodLoss(field, context) * 1.8f;
        }

        private float EnemyFieldDeferredPenalty(CoreNavalFieldCell field, NavalFieldPreviewContext context, int chain)
        {
            if (field == null || NavalFieldImmune(context.State.Enemy, field.Kind, context.State.EnemyTurn)) return 0f;
            switch (field.Kind)
            {
                case CoreNavalFieldKind.Fire: return EnemyEndFieldBurn(field, context) * 1.8f;
                case CoreNavalFieldKind.Curse: return EnemyEndFieldCurse(field, context) * 2f;
                case CoreNavalFieldKind.Arcane: return 3f; // Extra incoming active-attack damage.
                case CoreNavalFieldKind.Thunder:
                    int gained = NavalBossExpectedChainGain();
                    return CoreThunderRules.TriggersOverload(SchoolSum(chain, gained), gained) ?
                        CoreThunderRules.OverloadDamage * 1.8f : 0f;
                case CoreNavalFieldKind.Tide:
                    var target = new VoyagePoint(field.PositionMeters.X + field.DirectionX * NavalGridCellMeters,
                        field.PositionMeters.Y + field.DirectionY * NavalGridCellMeters);
                    return Math.Max(0, NavalGridDistance(target, context.PlayerPosition) -
                        NavalGridDistance(field.PositionMeters, context.PlayerPosition)) * 4f;
                default: return 0f;
            }
        }

        private float EnemyFieldPositionBenefit(CoreNavalFieldCell field, NavalFieldPreviewContext context, int chain,
            int health, bool runeClampConsumed)
        {
            if (field == null) return 0f;
            int remaining = Math.Max(0, field.ExpiresAtOwnerTurn - FieldDurationClock(context.State, field.PlayerOwned));
            if (remaining == 0) return 0f;
            switch (field.Kind)
            {
                case CoreNavalFieldKind.Fire: return 4f;
                case CoreNavalFieldKind.Arcane: return Math.Min(6f, remaining * 1.5f);
                case CoreNavalFieldKind.Curse: return 4f;
                case CoreNavalFieldKind.Thunder: return Math.Min(6f, 2f + chain * .1f);
                case CoreNavalFieldKind.Blood:
                    return EnemyBloodPoolBenefit(field, context, health, runeClampConsumed);
                default: return 0f;
            }
        }

        private float EnemyBloodPoolBenefit(CoreNavalFieldCell field, NavalFieldPreviewContext context,
            int health, bool runeClampConsumed)
        {
            if (field == null || field.Kind != CoreNavalFieldKind.Blood ||
                !NavalCannonInRange(context.PlayerPosition, field.PositionMeters)) return 0f;
            int cost = NavalBossExpectedBloodCost();
            if (cost <= 0 || health <= cost || CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks,
                health - cost, Battle.Schools.EnemyCurseExecutionThresholdPercent)) return 0f;
            // Guard cannot prevent the pool's hull loss or a resulting curse execution.
            int afterPool = Math.Max(0, health - cost - EnemyEndFieldBloodLoss(field, context));
            if (afterPool <= 0 || CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks,
                afterPool, Battle.Schools.EnemyCurseExecutionThresholdPercent)) return 0f;
            int guard = CoreFieldTradeoffRules.BloodGuard(cost);
            return context.RuneClampAvailable && !runeClampConsumed ? NavalExpansionEnemyBlockGain(guard) : guard;
        }

        // Enumerate short cardinal routes and trade approach/contact value against actual hull losses.
        // Fields remain traversable; sufficiently valuable contact can outweigh damage.
        private List<CoreNavalMovementNode> BuildNavalFieldEnemyPath(float meters, NavalFieldPreviewContext context = null)
        {
            context = context ?? CreateNavalFieldPreviewContext(false);
            var start = new CoreNavalMovementNode(Battle.EnemyPositionMeters, Battle.EnemyHeadingDegrees);
            var best = new List<CoreNavalMovementNode> { start };
            int requested = Math.Max(0, (int)(QuantizeNavalEnemyTravel(meters) / NavalGridCellMeters));
            int steps = EnemyNavalBossRushThisTurn ? Math.Min(NavalBossRushMaxCells, requested) :
                Math.Min(8, NavalActiveMovementLimit(context.State.Enemy, requested));
            float bestScore = EnemyFieldPathScore(best, context);
            var path = new List<CoreNavalMovementNode> { start };
            SearchNavalFieldEnemyPath(path, steps, ref best, ref bestScore, context);
            return best;
        }

        private float EnemyFieldPathScore(List<CoreNavalMovementNode> path, NavalFieldPreviewContext context)
        {
            CoreNavalMovementFieldPreview preview = PreviewNavalFields(path, false, context);
            if (preview.Sinks) return -100000f;
            int endingRisk = EnemyEndFieldRisk(PreviewFieldAt(context.State, preview.FinalPositionMeters), preview, context);
            if (preview.HullDamage + endingRisk >= context.Hull) return -100000f;
            int distance = NavalGridDistance(preview.FinalPositionMeters, context.PlayerPosition);
            CoreNavalFieldCell ending = PreviewFieldAt(context.State, preview.FinalPositionMeters);
            // Once in firing range, a safe useful field can outweigh one or two cells of approach.
            // Outside range, the approach value prevents terrain bonuses from causing a stalemate.
            bool bossStrategy = NavalBossFieldCycleActive;
            float score = bossStrategy ?
                -Math.Max(0, distance - NavalCannonRangeCells) * 4f - Math.Min(distance, NavalCannonRangeCells) * .5f +
                    (distance <= NavalCannonRangeCells ? 6f : 0f) + (distance == 1 ? 1f : 0f) :
                -distance * 3f + (distance <= 4 ? 3f : 0f) + (distance == 1 ? 6f : 0f);
            score -= (preview.HullDamage + endingRisk) * 1.8f + Math.Max(0, preview.Damage - preview.HullDamage) * .35f;
            if (bossStrategy)
            {
                score -= EnemyFieldDeferredPenalty(ending, context, preview.FinalThunderChain);
                if (distance <= NavalCannonRangeCells)
                    score += EnemyFieldPositionBenefit(ending, context, preview.FinalThunderChain,
                        Math.Max(0, context.Hull - preview.HullDamage), preview.RuneClampConsumed);
            }
            if (path.Count > 1)
            {
                float bearing = NavalBearing(preview.FinalPositionMeters, context.PlayerPosition);
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
            foreach (float turn in EnemyNavalBossRushThisTurn ? new[] { 0f, 90f, -90f, 180f } : new[] { 0f, 90f, -90f })
            {
                float heading = NormalizeNavalHeading(last.HeadingDegrees + turn);
                float radians = heading * NavalDegreesToRadians;
                var next = new VoyagePoint(last.PositionMeters.X + (float)Math.Round(Math.Cos(radians)) * NavalGridCellMeters,
                    last.PositionMeters.Y + (float)Math.Round(Math.Sin(radians)) * NavalGridCellMeters);
                if (!NavalForcedTargetValid(next, context.PlayerPosition) || NavalTideBlocksMovement(context.State, false, last.PositionMeters, next)) continue;
                if (path.Exists(node => NavalPointDistance(node.PositionMeters, next) < .01f)) continue;
                path.Add(new CoreNavalMovementNode(next, heading));
                float score = EnemyFieldPathScore(path, context);
                if (score > bestScore + .001f) { bestScore = score; best = new List<CoreNavalMovementNode>(path); }
                CoreNavalMovementFieldPreview preview = PreviewNavalFields(path, false, context);
                if (!preview.Sinks) SearchNavalFieldEnemyPath(path, remaining - 1, ref best, ref bestScore, context);
                path.RemoveAt(path.Count - 1);
            }
        }
    }
}



