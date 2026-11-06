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
        public bool IsFieldPlacement;
        public CoreNavalFieldKind FieldKind;
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
            choice.IsFieldPlacement = spec.IsField;
            if (spec.IsField) choice.FieldKind = NavalExpansionFieldKind(kind);
            choice.RequiresAnchor = spec.IsField || kind == CoreNavalExpansionMechanic.HarborExplosion ||
                kind == CoreNavalExpansionMechanic.ThunderCollapse;
            choice.RequiresDirection = kind == CoreNavalExpansionMechanic.TideField;
            choice.AllowsVertical = false;
            choice.RequiresFieldInstance = false;
            if ((spec.IsField && !choice.AllowsVertical) || kind == CoreNavalExpansionMechanic.HarborExplosion || kind == CoreNavalExpansionMechanic.ThunderCollapse)
                choice.ShapeWidth = choice.ShapeHeight = 2;
            if (spec.IsField) choice.ShapeWidth = choice.ShapeHeight = GetNavalFieldSize(choice.FieldKind);
            return choice.RequiresChoice ? choice : null;
        }

        private CoreBattleCard NavalExpansionHandCard(int handIndex) => Battle != null && handIndex >= 0 && handIndex < Battle.Hand.Count
            ? GetBattleCard(Battle.Hand[handIndex]) : null;

        public int NavalExpansionResourceAmount(CoreNavalExpansionCardSpec spec, CoreNavalCardPlayOptions options = null)
        {
            if (spec == null) return 0;
            if (spec.Mechanic == CoreNavalExpansionMechanic.LoadSeal) return 0;
            if (!spec.ConsumesAllResource) return options?.ResourceAmount ?? spec.ResourceMinimum;
            if (Battle == null) return 0;
            if (spec.ResourceLabel == "法力") return Battle.Schools.AvailableMana;
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
            int lifeCost = GetBattleCardHealthCost(card);
            if (lifeCost >= Hull && lifeCost > 0) return "支付船体后须至少剩余 1 点";
            if (spec.Mechanic == CoreNavalExpansionMechanic.LoadSeal)
            {
                if (Battle.Schools.CurseMarks < 1) return "敌方咒印层数不足 1";
            }
            else if (spec.ResourceMinimum > NavalExpansionResourceMaximum(spec)) return spec.ResourceLabel + "不足";
            if (spec.OncePerTurn && Battle.SuccessfulCardUses(BattleCardUseIdentity(card)) > 0) return "同名牌本回合已使用";
            reason = NavalThunderPowerUnavailableReason(card);
            if (reason.Length > 0) return reason;
            if (spec.NeedsEnemyTarget && NavalGridDistance(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters) > 4)
                return "目标须在 4 格曼哈顿范围内";
            if (spec.Mechanic == CoreNavalExpansionMechanic.ManaBurst &&
                NavalGridDistance(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters) < 1)
                return "目标须在 1–4 格范围内";
            if (spec.Mechanic == CoreNavalExpansionMechanic.MoveFire && !Battle.NavalFields.Cells.Exists(cell => cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Fire)) return "没有己方火场";
            if (spec.Mechanic == CoreNavalExpansionMechanic.HarborExplosion && !NavalExpansionHasExplodableFire()) return "没有完整落在 4 格内的己方火场引爆区域";
            if (spec.Mechanic == CoreNavalExpansionMechanic.ThunderCollapse && !NavalExpansionHasCollapsibleThunder()) return "没有完整落在 4 格内且含己方雷栅的 2×2 区域";
            return string.Empty;
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
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Thunder)
                    for (int dx = -1; dx <= 0; dx++) for (int dy = -1; dy <= 0; dy++)
                    {
                        int x = cell.X + dx, y = cell.Y + dy;
                        if (NavalExpansionCellInRange(x, y) && NavalExpansionCellInRange(x + 1, y) &&
                            NavalExpansionCellInRange(x, y + 1) && NavalExpansionCellInRange(x + 1, y + 1)) return true;
                    }
            return false;
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
                case CoreNavalExpansionMechanic.HarborExplosion:
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
                    if (cells.Count == 0) return "区域内须至少有 1 格己方雷栅";
                    foreach (VoyagePoint point in NavalExpansionShape(spec.Mechanic, options))
                        if (!NavalExpansionCellInRange(NavalExpansionCell(point.X), NavalExpansionCell(point.Y)))
                            return "区域的每一格都须是 4 格内合法海格";
                    return string.Empty;
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
            var result = new List<VoyagePoint>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result.Add(new VoyagePoint((options.AnchorX + x) * NavalGridCellMeters, (options.AnchorY + y) * NavalGridCellMeters));
            return result;
        }

        private List<CoreNavalFieldCell> NavalExpansionSelectedThunder(CoreNavalCardPlayOptions options)
        {
            var result = new List<CoreNavalFieldCell>();
            if (!options.HasAnchor) return result;
            foreach (VoyagePoint point in NavalExpansionShape(CoreNavalExpansionMechanic.ThunderCollapse, options))
            {
                CoreNavalFieldCell cell = GetNavalFieldAt(NavalExpansionCell(point.X), NavalExpansionCell(point.Y));
                if (cell != null && cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Thunder) result.Add(cell);
            }
            return result;
        }
    }
}
