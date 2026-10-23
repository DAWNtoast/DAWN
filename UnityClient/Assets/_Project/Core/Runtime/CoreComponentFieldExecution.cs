using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private bool ComponentFieldUnderShip(bool player, CoreNavalFieldKind kind)
        {
            if (!NavalFieldsActive) return false;
            CoreNavalFieldCell field = NavalFieldAt(player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters);
            return field != null && field.PlayerOwned && field.Kind == kind;
        }

        private int ComponentFieldDamageBonus(CoreBattleCard card) => CoreComponentFieldCards.IsFieldCard(card) &&
            card.SchoolEffect.Mechanic == CoreSchoolMechanic.ComponentFieldStrike &&
            ComponentFieldUnderShip(false, CoreComponentFieldCards.Kind(card)) ? card.SchoolEffect.Power : 0;

        private int ComponentFieldBlockBonus(CoreBattleCard card) => CoreComponentFieldCards.IsFieldCard(card) &&
            card.SchoolEffect.Mechanic == CoreSchoolMechanic.ComponentFieldShelter &&
            ComponentFieldUnderShip(true, CoreComponentFieldCards.Kind(card)) ? card.SchoolEffect.Power : 0;

        private int ComponentFieldRepairBonus(CoreBattleCard card)
        {
            if (!CoreComponentFieldCards.IsFieldCard(card) || card.SchoolId != "blood" ||
                !ComponentFieldUnderShip(true, CoreNavalFieldKind.Blood)) return 0;
            return card.SchoolEffect.Mechanic == CoreSchoolMechanic.ComponentFieldShelter ? card.SchoolEffect.Secondary :
                card.SchoolEffect.Mechanic == CoreSchoolMechanic.ComponentFieldPulse ? card.SchoolEffect.Power : 0;
        }

        // Damage, guard and supply-free repair are quoted by the ordinary card pipeline. This hook applies only
        // the remaining field actions, keeping their costs and successful-use counters shared.
        private bool ApplyComponentFieldCardEffect(CoreBattleCard card)
        {
            if (!CoreComponentFieldCards.IsFieldCard(card)) return false;
            if (!NavalFieldsActive) return true;
            CoreSchoolCardEffect effect = card.SchoolEffect;
            CoreNavalFieldKind kind = CoreComponentFieldCards.Kind(card);
            switch (effect.Mechanic)
            {
                case CoreSchoolMechanic.ComponentFieldExpand:
                    ExpandComponentField(kind, effect.Power);
                    break;
                case CoreSchoolMechanic.ComponentFieldExtend:
                    ExtendComponentField(kind, effect.Power);
                    break;
                case CoreSchoolMechanic.ComponentFieldMastery:
                    ExpandComponentField(kind, effect.Power);
                    ExtendComponentField(kind, effect.Secondary);
                    break;
                case CoreSchoolMechanic.ComponentFieldShelter:
                    int protection = (1 << (int)kind) & NavalFieldProtectionMask;
                    if (protection != 0)
                    {
                        CoreNavalFieldShipState ship = FieldShip(true);
                        if (ship.FieldImmunityExpires <= FieldOwnerTurn(true)) ship.FieldImmunityMask = 0;
                        ship.FieldImmunityMask |= protection;
                        ship.FieldImmunityExpires = FieldOwnerTurn(true) + 1;
                    }
                    else if (ComponentFieldUnderShip(true, kind))
                    {
                        if (kind == CoreNavalFieldKind.Arcane) RestoreComponentFieldMana(effect.Secondary);
                    }
                    break;
                case CoreSchoolMechanic.ComponentFieldPulse:
                    bool enemyCondition = kind == CoreNavalFieldKind.Fire || kind == CoreNavalFieldKind.Curse || kind == CoreNavalFieldKind.Thunder;
                    if (!ComponentFieldUnderShip(!enemyCondition, kind)) break;
                    CoreSchoolBattleState school = Battle.Schools;
                    switch (kind)
                    {
                        case CoreNavalFieldKind.Fire: ApplySchoolBurn(effect.Power, 0); break;
                        case CoreNavalFieldKind.Arcane: RestoreComponentFieldMana(effect.Power); break;
                        case CoreNavalFieldKind.Curse:
                            school.CurseMarks = SchoolSum(school.CurseMarks, effect.Power);
                            school.CurseDamagePerMark = school.CurseWeaknessPercent = 0;
                            school.CurseTurns = school.CurseMarks;
                            break;
                        case CoreNavalFieldKind.Tide:
                            school.TidePower = Math.Min(school.TideLimit, SchoolSum(school.TidePower, effect.Power));
                            break;
                        case CoreNavalFieldKind.Thunder:
                            school.ThunderChain = Math.Min(school.ThunderLimit, SchoolSum(school.ThunderChain, effect.Power));
                            break;
                    }
                    break;
            }
            return true;
        }

        private void RestoreComponentFieldMana(int amount) => Battle.Schools.Mana =
            Math.Min(Battle.Schools.ManaLimit, SchoolSum(Battle.Schools.Mana, amount));

        private bool ComponentFieldInRange(CoreNavalFieldCell cell) =>
            NavalGridDistance(Battle.PlayerPositionMeters, cell.PositionMeters) <= NavalCannonRangeCells;

        private void ExpandComponentField(CoreNavalFieldKind kind, int count)
        {
            if (count <= 0) return;
            // Only the pre-cast boundary can supply new cells. A newly created cell cannot
            // grow again during the same action, and every placement preserves its instance.
            var sources = new List<CoreNavalFieldCell>();
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == kind && ComponentFieldInRange(cell)) sources.Add(cell.Copy());
            sources.Sort((left, right) =>
            {
                int order = left.InstanceId.CompareTo(right.InstanceId);
                if (order == 0) order = left.X.CompareTo(right.X);
                return order == 0 ? left.Y.CompareTo(right.Y) : order;
            });
            var candidates = new List<CoreNavalFieldCell>();
            var coordinates = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreNavalFieldCell source in sources)
                foreach (var offset in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 0, 1 }, new[] { 1, 0 } })
                {
                    CoreNavalFieldCell candidate = source.Copy();
                    candidate.X += offset[0]; candidate.Y += offset[1];
                    if (!FieldCellValid(candidate.X, candidate.Y) || !ComponentFieldInRange(candidate) ||
                        GetNavalFieldAt(candidate.X, candidate.Y) != null || FieldOccupied(candidate.PositionMeters) ||
                        !coordinates.Add(candidate.X + ":" + candidate.Y)) continue;
                    candidates.Add(candidate);
                }
            candidates.Sort((left, right) =>
            {
                int order = NavalGridDistance(left.PositionMeters, Battle.EnemyPositionMeters)
                    .CompareTo(NavalGridDistance(right.PositionMeters, Battle.EnemyPositionMeters));
                if (order == 0) order = left.X.CompareTo(right.X);
                return order == 0 ? left.Y.CompareTo(right.Y) : order;
            });
            for (int index = 0; index < candidates.Count && index < count; index++)
            {
                CoreNavalFieldCell cell = candidates[index];
                Battle.NavalFields.Cells.Add(cell);
                Battle.NavalFields.RecordPresentation(CoreNavalFieldChangeKind.Placed, cell);
            }
        }

        private void ExtendComponentField(CoreNavalFieldKind kind, int turns)
        {
            if (turns <= 0) return;
            var selected = new HashSet<int>();
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == kind && ComponentFieldInRange(cell)) selected.Add(cell.InstanceId);
            // Expiry belongs to the complete instance, including any cells outside range.
            foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                if (cell.PlayerOwned && cell.Kind == kind && selected.Contains(cell.InstanceId))
                    cell.ExpiresAtOwnerTurn = SchoolSum(cell.ExpiresAtOwnerTurn, turns);
        }
    }
}
