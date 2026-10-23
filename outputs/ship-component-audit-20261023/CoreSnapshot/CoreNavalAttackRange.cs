using System;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreNavalAttackRangeKind { None, CannonCells, MetricMeters, Unlimited }

    /// <summary>
    /// Geographic reach of an enemy-damaging card, independent of the resources needed to play it.
    /// Unlimited ranges must be clipped to the visible board by the presentation layer.
    /// </summary>
    public readonly struct CoreNavalAttackRange
    {
        internal CoreNavalAttackRange(CoreNavalAttackRangeKind kind, float meters = 0f, int cells = 0)
        {
            Kind = kind;
            Meters = meters;
            Cells = cells > 0 ? cells : CoreVoyageSession.NavalCannonRangeCells;
        }

        public CoreNavalAttackRangeKind Kind { get; }
        public float Meters { get; }
        public int Cells { get; }
        public bool IsDamageCard => Kind != CoreNavalAttackRangeKind.None;
        public bool IsUnlimited => Kind == CoreNavalAttackRangeKind.Unlimited;
        /// <summary>Bounding square radius for finite ranges; unlimited ranges have no finite scan radius.</summary>
        public int ScanRadiusCells => Kind == CoreNavalAttackRangeKind.CannonCells
            ? Cells : Kind == CoreNavalAttackRangeKind.MetricMeters
                ? (int)Math.Ceiling(Meters / CoreVoyageSession.NavalGridCellMeters) : 0;
        public static CoreNavalAttackRange Cannon => new CoreNavalAttackRange(CoreNavalAttackRangeKind.CannonCells);

        public bool Contains(VoyagePoint origin, VoyagePoint target)
        {
            int cells = CoreVoyageSession.NavalGridDistance(origin, target);
            // The player-occupied cell is never an enemy target cell. Invalid points are not paintable.
            if (!IsDamageCard || cells == 0 || cells == int.MaxValue) return false;
            if (Kind == CoreNavalAttackRangeKind.CannonCells)
                return cells <= Cells;
            if (Kind == CoreNavalAttackRangeKind.Unlimited) return true;
            double dx = (double)target.X - origin.X;
            double dy = (double)target.Y - origin.Y;
            return dx * dx + dy * dy <= (double)Meters * Meters;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        /// <summary>
        /// Returns the same geographic restrictions as CardUnavailableReason without checking costs,
        /// unlocks, crew, equipment, or school resources. Preparation can preview naval cards read-only.
        /// </summary>
        public CoreNavalAttackRange GetNavalCardAttackRange(CoreBattleCard card)
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage == NavalBattleStage.Boarding ||
                card == null || !NavalCardDealsEnemyDamage(card)) return default;
            if (!card.IsAllowedInStage(NavalBattleStage.Naval) &&
                !(Battle.Stage == NavalBattleStage.Preparation && card.IsAllowedInStage(NavalBattleStage.Preparation)))
                return default;
            int maximum = CoreNavalCardRangeRules.MaximumCells(card);
            if (maximum > 0) return new CoreNavalAttackRange(CoreNavalAttackRangeKind.CannonCells, cells: maximum);
            return new CoreNavalAttackRange(CoreNavalAttackRangeKind.Unlimited);
        }

        public bool NavalCardCanTargetCell(CoreBattleCard card, VoyagePoint target) =>
            Battle != null && GetNavalCardAttackRange(card).Contains(Battle.PlayerPositionMeters, target);

        private static bool NavalCardDealsEnemyDamage(CoreBattleCard card)
        {
            if (card.Damage > 0) return true;
            CoreSchoolCardEffect effect = card.SchoolEffect;
            if (effect == null) return false;
            if (effect.FallbackDamage > 0) return true;
            switch (effect.Mechanic)
            {
                // These effects damage the enemy directly, even when their base damage is zero.
                case CoreSchoolMechanic.FireExploit:
                case CoreSchoolMechanic.ArcaneRelease:
                case CoreSchoolMechanic.TideRiposte:
                case CoreSchoolMechanic.ThunderPrime:
                case CoreSchoolMechanic.ThunderChain:
                case CoreSchoolMechanic.ThunderFinish:
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodExecute:
                // These act on the enemy's damage-over-time state, rather than buffing a future attack.
                case CoreSchoolMechanic.FireExtend:
                case CoreSchoolMechanic.CurseDetonate:
                    return effect.Power > 0;
                case CoreSchoolMechanic.CurseMark:
                    return effect.Power > 0 && effect.Secondary > 0;
                default:
                    return false;
            }
        }
    }
}
