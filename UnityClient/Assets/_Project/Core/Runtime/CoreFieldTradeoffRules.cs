using System;

namespace ShatteredSail.Core.Gameplay
{
    public static class CoreFieldTradeoffRules
    {
        // Compose all applicable fields, then round once; a positive hit never rounds to zero.
        public static int ApplyAttackDamage(int damage, bool fireAttacker, bool arcaneTarget, bool curseTarget)
        {
            if (damage <= 0) return 0;
            long numerator = damage, denominator = 1;
            if (fireAttacker) { numerator *= 125; denominator *= 100; }
            if (arcaneTarget) { numerator *= 125; denominator *= 100; }
            if (curseTarget) { numerator *= 75; denominator *= 100; }
            return (int)Math.Min(int.MaxValue, (numerator + denominator - 1) / denominator);
        }

        public static int BloodGuard(int paid) => (int)Math.Min(int.MaxValue, 2L * Math.Max(0, paid));
    }

    public sealed partial class CoreVoyageSession
    {
        private bool NavalTradeoffField(bool player, CoreNavalFieldKind kind, CoreNavalFieldState state = null,
            VoyagePoint? position = null)
        {
            if (!NavalFieldsActive) return false;
            state = state ?? Battle.NavalFields;
            CoreNavalFieldCell field = PreviewFieldAt(state, position ??
                (player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters));
            return field != null && field.Kind == kind &&
                field.ExpiresAtOwnerTurn > FieldDurationClock(state, field.PlayerOwned);
        }

        private int NavalFieldAttackDamage(int damage, bool attackerPlayer, bool targetPlayer,
            CoreNavalFieldState state = null, VoyagePoint? attackerPosition = null, VoyagePoint? targetPosition = null,
            bool? fireAttacker = null) => CoreFieldTradeoffRules.ApplyAttackDamage(damage,
                fireAttacker ?? NavalTradeoffField(attackerPlayer, CoreNavalFieldKind.Fire, state, attackerPosition),
                NavalArcaneVulnerability(targetPlayer, state, targetPosition),
                NavalTradeoffField(targetPlayer, CoreNavalFieldKind.Curse, state, targetPosition));

        private bool NavalArcaneVulnerability(bool player, CoreNavalFieldState state = null, VoyagePoint? position = null)
        {
            if (!NavalTradeoffField(player, CoreNavalFieldKind.Arcane, state, position)) return false;
            state = state ?? Battle.NavalFields;
            return !NavalFieldImmune(player ? state.Player : state.Enemy, CoreNavalFieldKind.Arcane,
                player ? state.PlayerTurn : state.EnemyTurn);
        }

        private int EnemyNavalFieldAttackDamage(int damage, bool consume = false, CoreNavalMovementFieldPreview preview = null)
        {
            if (!NavalFieldsActive || consume) return NavalFieldAttackDamage(damage, false, true);
            NavalFieldPreviewContext context = CreateNavalFieldPreviewContext(false);
            VoyagePoint position = preview?.FinalPositionMeters ?? Battle.EnemyPositionMeters;
            PreviewNavalBossPlacedField(context.State, position, context.PlayerPosition);
            return NavalFieldAttackDamage(damage, false, true, context.State,
                position, context.PlayerPosition);
        }

        // Active cards opt in explicitly. Passive field damage and overload use the unmodified damage path.
        private int NavalActiveCardDamage(bool targetPlayer, int damage, int piercing, bool? fireAttacker = null)
        {
            bool fullyPiercing = damage > 0 && piercing >= damage;
            damage = NavalFieldAttackDamage(damage, true, targetPlayer, fireAttacker: fireAttacker);
            return NavalExpansionApplyDirectDamage(targetPlayer, damage, fullyPiercing ? damage : piercing, !targetPlayer);
        }

        private string NavalFieldAttackCardDescription(CoreBattleCard card)
        {
            if (!NavalFieldsActive || card == null || (card.Damage <= 0 &&
                card.SchoolEffect?.Mechanic != CoreSchoolMechanic.CurseDetonate &&
                card.SchoolEffect?.Mechanic != CoreSchoolMechanic.FireExploit)) return string.Empty;
            string text = string.Empty;
            if (NavalTradeoffField(true, CoreNavalFieldKind.Fire)) text += "火场：主动攻击伤害 +25%。";
            if (NavalArcaneVulnerability(false)) text += "敌舰折射阵：对其主动攻击伤害 +25%。";
            if (NavalTradeoffField(false, CoreNavalFieldKind.Curse)) text += "敌舰禁域：对其主动攻击伤害 -25%。";
            return text.Length == 0 ? string.Empty : "\n场地修正（应用于上述完整伤害，合并后向上取整）：" + text;
        }
    }
}
