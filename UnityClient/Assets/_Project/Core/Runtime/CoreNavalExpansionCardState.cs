using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Serializable naval-only card state; per-ID play counts remain in CoreBattleState.</summary>
    [Serializable]
    public sealed class CoreNavalExpansionBattleState
    {
        public int OilPlayerEndsRemaining;
        public bool RuneClamp;
        public bool LoadSeal;
        public int PulseReduction;
        public bool Precharge;
        public bool GroundingReady;
        public bool GroundingUsed;
        public bool EnemyHealingHalved;
    }

    public sealed partial class CoreBattleState
    {
        public CoreNavalExpansionBattleState NavalExpansion { get; internal set; } = new CoreNavalExpansionBattleState();
    }

    public sealed partial class CoreVoyageSession
    {
        private bool NavalExpansionActive => Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == NavalBattleStage.Naval;

        public int NavalExpansionFirstDamageBonus(CoreBattleCard card, bool consume = false)
        {
            if (!NavalExpansionActive || card == null || card.Damage <= 0) return 0;
            int bonus = Battle.NavalFields.Player.BloodAttackBonus;
            if (consume) Battle.NavalFields.Player.BloodAttackBonus = 0;
            return bonus;
        }

        public int NavalExpansionFirstPiercing(CoreBattleCard card, int innate = 0, bool consume = false)
        {
            if (!NavalExpansionActive || card == null || card.Damage <= 0 || card.SchoolId != "arcane") return innate;
            int piercing = Math.Max(innate, Battle.NavalFields.Player.ArcanePiercing);
            if (consume) Battle.NavalFields.Player.ArcanePiercing = 0;
            return piercing;
        }

        public void NavalExpansionOnDirectHit(CoreBattleCard card, int actual, bool targetOnThunder)
        {
            if (!NavalExpansionActive || card == null || actual <= 0) return;
            CoreNavalExpansionBattleState state = Battle.NavalExpansion;
            if (card.SchoolId == "fire" && state.OilPlayerEndsRemaining > 0)
            {
                // Remove before derived damage, preventing recursive active-hit rewards.
                state.OilPlayerEndsRemaining = 0;
                int absorbed = Math.Min(Battle.EnemyBlock, 4);
                Battle.EnemyBlock -= absorbed;
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - (4 - absorbed));
                ApplySchoolBurn(3, 2);
            }
            if (card.SchoolId == "thunder" && targetOnThunder && state.GroundingReady && !state.GroundingUsed)
            {
                state.GroundingReady = false;
                state.GroundingUsed = true;
                Battle.Energy = SchoolSum(Battle.Energy, 1);
            }
        }

        public void NavalExpansionCompleteAttack(CoreBattleCard card)
        {
            if (!NavalExpansionActive || card == null || card.Damage <= 0 || card.SchoolId != "thunder") return;
            if (Battle.NavalExpansion.Precharge)
            {
                Battle.NavalExpansion.Precharge = false;
                Battle.Schools.ThunderChain = Math.Min(Battle.Schools.ThunderLimit, SchoolSum(Battle.Schools.ThunderChain, 2));
            }
        }

        public int NavalExpansionEnemyBlockGain(int amount, bool consume = false)
        {
            if (!NavalExpansionActive || !Battle.NavalExpansion.RuneClamp) return Math.Max(0, amount);
            if (consume && amount > 0) Battle.NavalExpansion.RuneClamp = false;
            return Math.Max(0, amount - 6);
        }

        public int NavalExpansionEnemyLoadGain(int amount, bool consume = false)
        {
            if (!NavalExpansionActive || !Battle.NavalExpansion.LoadSeal) return Math.Max(0, amount);
            if (consume && amount > 0) Battle.NavalExpansion.LoadSeal = false;
            return Math.Max(0, amount - 6);
        }

        public int NavalExpansionEnemyAttackDamage(int total, bool consume = false)
        {
            total = Math.Max(0, total);
            if (!NavalExpansionActive) return total;
            int reduction = Math.Min(total / 2, Battle.NavalExpansion.PulseReduction);
            if (consume) Battle.NavalExpansion.PulseReduction = 0;
            return total - reduction;
        }

        public int NavalExpansionHealing(bool player, int intended)
        {
            if (!NavalExpansionActive) return Math.Max(0, intended);
            int missing = player ? Math.Max(0, MaxHull - Hull) : Math.Max(0, Battle.EnemyMaxHull - Battle.EnemyHull);
            int actual = Math.Min(missing, Math.Max(0, intended));
            return !player && Battle.NavalExpansion.EnemyHealingHalved ? actual / 2 : actual;
        }

        public void NavalExpansionBeginPlayerTurn()
        {
            if (Battle != null) Battle.NavalExpansion.GroundingUsed = false;
        }

        public void NavalExpansionEndPlayerTurn()
        {
            if (!NavalExpansionActive) return;
            CoreNavalExpansionBattleState state = Battle.NavalExpansion;
            state.OilPlayerEndsRemaining = Math.Max(0, state.OilPlayerEndsRemaining - 1);
            state.Precharge = state.GroundingReady = false;
        }

        public void NavalExpansionEndEnemyTurn()
        {
            if (Battle == null) return;
            CoreNavalExpansionBattleState state = Battle.NavalExpansion;
            state.RuneClamp = state.LoadSeal = state.EnemyHealingHalved = false;
            state.PulseReduction = 0;
        }

        public void NavalExpansionClear()
        {
            if (Battle == null) return;
            Battle.NavalExpansion = new CoreNavalExpansionBattleState();
            Battle.SpecialCards.Clear();
        }

        private void NavalExpansionConsumeCurse(int count)
        {
            CoreSchoolBattleState state = Battle.Schools;
            state.CurseMarks = Math.Max(0, state.CurseMarks - Math.Max(0, count));
            if (state.CurseMarks > 0) return;
            state.CurseTurns = state.CurseDamagePerMark = state.CurseNextTickBonus = state.CurseWeaknessPercent = 0;
        }
    }
}
