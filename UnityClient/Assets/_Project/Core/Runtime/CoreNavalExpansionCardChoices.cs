using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreNavalCardPlayOptions
    {
        public int AnchorX, AnchorY, SourceX, SourceY;
        public bool HasAnchor, HasSource, Vertical;
        public int DirectionX, DirectionY, ResourceAmount, FieldInstanceId;
    }

    public sealed class CoreNavalCardChoice
    {
        public string Title, ResourceLabel;
        public bool RequiresAnchor, RequiresSource, RequiresDirection, AllowsVertical, RequiresFieldInstance;
        public int ShapeWidth = 1, ShapeHeight = 1, ResourceMinimum, ResourceMaximum;
        public bool RequiresChoice => RequiresAnchor || RequiresSource || RequiresDirection || RequiresFieldInstance || ResourceMaximum > ResourceMinimum;
    }

    public sealed partial class CoreVoyageSession
    {
        public CoreNavalCardChoice GetNavalCardChoice(int handIndex)
        {
            CoreBattleCard card = NavalExpansionHandCard(handIndex);
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card?.Id);
            if (spec == null) return null;
            var choice = new CoreNavalCardChoice { Title = card.Name, ResourceLabel = spec.ResourceLabel,
                ResourceMinimum = spec.ResourceMinimum, ResourceMaximum = NavalExpansionResourceMaximum(spec) };
            CoreNavalExpansionMechanic kind = spec.Mechanic;
            choice.RequiresAnchor = spec.IsField || kind == CoreNavalExpansionMechanic.HarborExplosion ||
                kind == CoreNavalExpansionMechanic.MoveFire || kind == CoreNavalExpansionMechanic.GreatWave ||
                kind == CoreNavalExpansionMechanic.ThunderCollapse;
            choice.RequiresSource = kind == CoreNavalExpansionMechanic.MoveFire;
            choice.RequiresDirection = kind == CoreNavalExpansionMechanic.TideField || kind == CoreNavalExpansionMechanic.RecedingWave || kind == CoreNavalExpansionMechanic.GreatWave;
            choice.AllowsVertical = kind == CoreNavalExpansionMechanic.ThunderField;
            choice.RequiresFieldInstance = kind == CoreNavalExpansionMechanic.ThunderCollapse;
            if ((spec.IsField && !choice.AllowsVertical) || kind == CoreNavalExpansionMechanic.HarborExplosion)
                choice.ShapeWidth = choice.ShapeHeight = 2;
            if (choice.AllowsVertical || kind == CoreNavalExpansionMechanic.GreatWave) choice.ShapeWidth = 3;
            return choice.RequiresChoice ? choice : null;
        }

        private CoreBattleCard NavalExpansionHandCard(int handIndex) => Battle != null && handIndex >= 0 && handIndex < Battle.Hand.Count
            ? GetBattleCard(Battle.Hand[handIndex]) : null;

        public int NavalExpansionResourceAmount(CoreNavalExpansionCardSpec spec, CoreNavalCardPlayOptions options = null)
        {
            if (spec == null) return 0;
            if (!spec.ConsumesAllResource) return options?.ResourceAmount ?? spec.ResourceMinimum;
            if (Battle == null) return 0;
            if (spec.ResourceLabel == "潮势") return Math.Max(0, Battle.Schools.TidePower);
            if (spec.ResourceLabel == "咒印") return Math.Max(0, Battle.Schools.CurseMarks);
            if (spec.ResourceLabel == "连锁") return Math.Max(0, Battle.Schools.ThunderChain);
            return 0;
        }

        private int NavalExpansionResourceMaximum(CoreNavalExpansionCardSpec spec)
        {
            if (Battle == null) return spec.ResourceMaximum;
            int maximum = spec.ResourceMaximum;
            if (spec.ResourceLabel == "潮势") maximum = Math.Min(maximum, Battle.Schools.TidePower);
            if (spec.ResourceLabel == "咒印") maximum = Math.Min(maximum, Battle.Schools.CurseMarks);
            if (spec.ResourceLabel == "连锁") maximum = Math.Min(maximum, Battle.Schools.ThunderChain);
            return Math.Max(0, maximum);
        }

        public string NavalExpansionCardUnavailableReason(int handIndex)
        {
            if (!NavalExpansionActive) return "该牌只能在炮战阶段使用";
            if (Battle.NavalAwaitingHeadingSelection) return "请先确定到达朝向";
            CoreBattleCard card = NavalExpansionHandCard(handIndex);
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card?.Id);
            if (spec == null) return "不是新增海战牌";
            string reason = GetBattleCardUnlockReason(card);
            if (reason.Length > 0) return reason;
            reason = GuidedBossCardRestriction(card);
            if (reason.Length > 0) return reason;
            if (GetBattleCardEnergyCost(Battle.Hand[handIndex]) > Battle.Energy) return "能量不足";
            if (card.PowderCost > Cargo.Powder) return "火药不足";
            if (card.CannonballCost > Cargo.Cannonballs) return "炮弹不足";
            if (spec.ManaCost > Battle.Schools.AvailableMana) return "法力不足";
            int lifeCost = CoreSpecialCardRules.HealthCost(card, Battle);
            if (lifeCost >= Hull && lifeCost > 0) return "支付船体后须至少剩余 1 点";
            if (spec.ResourceMinimum > NavalExpansionResourceMaximum(spec)) return spec.ResourceLabel + "不足";
            if (spec.OncePerTurn && Battle.SuccessfulCardUses(BattleCardUseIdentity(card)) > 0) return "同名牌本回合已使用";
            if (spec.NeedsEnemyTarget && NavalGridDistance(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters) > 4)
                return "目标须在 4 格曼哈顿范围内";
            if (spec.Mechanic == CoreNavalExpansionMechanic.MoveFire && !NavalExpansionHasMovableFire()) return "没有可移至相邻空格的己方火场";
            if (spec.Mechanic == CoreNavalExpansionMechanic.HarborExplosion && !NavalExpansionHasExplodableFire()) return "没有完整落在 4 格内的己方火场引爆区域";
            if (spec.Mechanic == CoreNavalExpansionMechanic.ThunderCollapse && !NavalExpansionHasCollapsibleThunder()) return "没有含敌舰且剩余格全在 4 格内的己方雷栅";
            return string.Empty;
        }

        private bool NavalExpansionHasMovableFire()
        {
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Fire && NavalExpansionCellInRange(cell.X, cell.Y))
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int dx = direction == 0 ? 1 : direction == 1 ? -1 : 0;
                        int dy = direction == 2 ? 1 : direction == 3 ? -1 : 0;
                        if (CanMoveNavalFireCell(true, cell.X, cell.Y, cell.X + dx, cell.Y + dy).Length == 0) return true;
                    }
            return false;
        }

        private bool NavalExpansionHasExplodableFire()
        {
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Fire)
                    for (int dx = -1; dx <= 0; dx++) for (int dy = -1; dy <= 0; dy++)
                    {
                        int x = cell.X + dx, y = cell.Y + dy;
                        if (NavalExpansionCellInRange(x, y) && NavalExpansionCellInRange(x + 1, y) &&
                            NavalExpansionCellInRange(x, y + 1) && NavalExpansionCellInRange(x + 1, y + 1)) return true;
                    }
            return false;
        }

        private bool NavalExpansionHasCollapsibleThunder()
        {
            CoreNavalFieldCell target = GetNavalFieldAt(NavalExpansionCell(Battle.EnemyPositionMeters.X), NavalExpansionCell(Battle.EnemyPositionMeters.Y));
            if (target == null || !target.PlayerOwned || target.Kind != CoreNavalFieldKind.Thunder) return false;
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.InstanceId == target.InstanceId && !NavalExpansionCellInRange(cell.X, cell.Y)) return false;
            return true;
        }

        public string GetNavalChoiceUnavailableReason(int handIndex, CoreNavalCardPlayOptions options)
        {
            string reason = NavalExpansionCardUnavailableReason(handIndex);
            if (reason.Length > 0) return reason;
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(NavalExpansionHandCard(handIndex).Id);
            CoreNavalCardChoice choice = GetNavalCardChoice(handIndex);
            options = options ?? new CoreNavalCardPlayOptions { ResourceAmount = spec.ResourceMinimum };
            if (choice != null && choice.RequiresAnchor && !options.HasAnchor && !(choice.RequiresFieldInstance && options.FieldInstanceId > 0))
                return "请选择海格";
            if (choice != null && choice.RequiresSource && !options.HasSource) return "请选择己方火场源格";
            if (choice != null && choice.RequiresDirection && Math.Abs((long)options.DirectionX) + Math.Abs((long)options.DirectionY) != 1)
                return "请选择上下左右方向";
            if (spec.ResourceMaximum > 0 && (options.ResourceAmount < spec.ResourceMinimum || options.ResourceAmount > NavalExpansionResourceMaximum(spec)))
                return "所选" + spec.ResourceLabel + "数量不合法";
            if (spec.IsField) return CanPlaceNavalField(NavalExpansionFieldKind(spec.Mechanic), true,
                options.AnchorX, options.AnchorY, options.Vertical, options.DirectionX, options.DirectionY);
            switch (spec.Mechanic)
            {
                case CoreNavalExpansionMechanic.MoveFire:
                    return CanMoveNavalFireCell(true, options.SourceX, options.SourceY, options.AnchorX, options.AnchorY);
                case CoreNavalExpansionMechanic.HarborExplosion:
                case CoreNavalExpansionMechanic.GreatWave:
                    List<VoyagePoint> shape = NavalExpansionShape(spec.Mechanic, options);
                    bool fire = false;
                    foreach (VoyagePoint point in shape)
                    {
                        int x = NavalExpansionCell(point.X), y = NavalExpansionCell(point.Y);
                        if (!NavalExpansionCellInRange(x, y)) return "区域的每一格都须是 4 格内合法海格";
                        CoreNavalFieldCell cell = GetNavalFieldAt(x, y);
                        fire |= cell != null && cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Fire;
                    }
                    return spec.Mechanic == CoreNavalExpansionMechanic.HarborExplosion && !fire ? "区域内须至少有 1 格己方火场" : string.Empty;
                case CoreNavalExpansionMechanic.ThunderCollapse:
                    List<CoreNavalFieldCell> cells = NavalExpansionSelectedThunder(options);
                    if (cells.Count == 0) return "请选择己方雷栅实例";
                    bool enemy = false;
                    foreach (CoreNavalFieldCell cell in cells)
                    {
                        if (!NavalExpansionCellInRange(cell.X, cell.Y)) return "所选雷栅全部剩余格均须在 4 格内";
                        enemy |= NavalExpansionShipAt(false, cell.X, cell.Y);
                    }
                    return enemy ? string.Empty : "所选雷栅须至少有一格包含敌舰";
                case CoreNavalExpansionMechanic.RecedingWave:
                    int enemyX = NavalExpansionCell(Battle.EnemyPositionMeters.X), enemyY = NavalExpansionCell(Battle.EnemyPositionMeters.Y);
                    int playerX = NavalExpansionCell(Battle.PlayerPositionMeters.X), playerY = NavalExpansionCell(Battle.PlayerPositionMeters.Y);
                    int before = Math.Abs(enemyX - playerX) + Math.Abs(enemyY - playerY);
                    int after = Math.Abs(enemyX + options.DirectionX - playerX) + Math.Abs(enemyY + options.DirectionY - playerY);
                    return after > before ? string.Empty : "推动方向必须使双方格距增加";
            }
            return string.Empty;
        }

        private static int NavalExpansionCell(float meters) => (int)Math.Round(meters / NavalGridCellMeters);
        private bool NavalExpansionShipAt(bool player, int x, int y)
        {
            VoyagePoint position = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            return NavalExpansionCell(position.X) == x && NavalExpansionCell(position.Y) == y;
        }
        private bool NavalExpansionCellInRange(int x, int y) =>
            IsNavalFieldSeaCell(x, y) && Math.Abs((long)x - NavalExpansionCell(Battle.PlayerPositionMeters.X)) + Math.Abs((long)y - NavalExpansionCell(Battle.PlayerPositionMeters.Y)) <= 4;

        private static CoreNavalFieldKind NavalExpansionFieldKind(CoreNavalExpansionMechanic mechanic)
        {
            switch (mechanic)
            {
                case CoreNavalExpansionMechanic.FireField: return CoreNavalFieldKind.Fire;
                case CoreNavalExpansionMechanic.ArcaneField: return CoreNavalFieldKind.Arcane;
                case CoreNavalExpansionMechanic.CurseField: return CoreNavalFieldKind.Curse;
                case CoreNavalExpansionMechanic.TideField: return CoreNavalFieldKind.Tide;
                case CoreNavalExpansionMechanic.ThunderField: return CoreNavalFieldKind.Thunder;
                default: return CoreNavalFieldKind.Blood;
            }
        }

        private static List<VoyagePoint> NavalExpansionShape(CoreNavalExpansionMechanic mechanic, CoreNavalCardPlayOptions options)
        {
            int width = 2, height = 2;
            if (mechanic == CoreNavalExpansionMechanic.GreatWave)
            { width = options.DirectionX != 0 ? 1 : 3; height = options.DirectionX != 0 ? 3 : 1; }
            var result = new List<VoyagePoint>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result.Add(new VoyagePoint((options.AnchorX + x) * NavalGridCellMeters, (options.AnchorY + y) * NavalGridCellMeters));
            return result;
        }

        private List<CoreNavalFieldCell> NavalExpansionSelectedThunder(CoreNavalCardPlayOptions options)
        {
            int instance = options.FieldInstanceId;
            CoreNavalFieldCell selected = options.HasAnchor ? GetNavalFieldAt(options.AnchorX, options.AnchorY) : null;
            if (selected != null && selected.PlayerOwned && selected.Kind == CoreNavalFieldKind.Thunder) instance = selected.InstanceId;
            var result = new List<CoreNavalFieldCell>();
            if (instance <= 0) return result;
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Thunder && cell.InstanceId == instance) result.Add(cell);
            return result;
        }
    }
}
