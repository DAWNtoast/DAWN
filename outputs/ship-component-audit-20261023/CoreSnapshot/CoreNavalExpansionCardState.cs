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
        public int PrechargeBonus;
        public int RuneClampReduction;
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
            int bonus = SchoolSum(Battle.NavalFields.Player.BloodAttackBonus, Battle.Schools.ArcaneNextAttackDamage);
            if (consume)
            {
                Battle.NavalFields.Player.BloodAttackBonus = 0;
                Battle.Schools.ArcaneNextAttackDamage = 0;
            }
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
                ApplySchoolBurn(2, 2);
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
                int extra = Battle.NavalExpansion.PrechargeBonus > 0 ? Battle.NavalExpansion.PrechargeBonus : 2;
                Battle.NavalExpansion.PrechargeBonus = 0;
                Battle.Schools.ThunderChain = Math.Min(Battle.Schools.ThunderLimit, SchoolSum(Battle.Schools.ThunderChain, extra));
            }
        }

        public int NavalExpansionEnemyBlockGain(int amount, bool consume = false)
        {
            if (!NavalExpansionActive || !Battle.NavalExpansion.RuneClamp) return Math.Max(0, amount);
            int reduction = Battle.NavalExpansion.RuneClampReduction > 0 ? Battle.NavalExpansion.RuneClampReduction : 6;
            if (consume && amount > 0)
            {
                Battle.NavalExpansion.RuneClamp = false;
                Battle.NavalExpansion.RuneClampReduction = 0;
            }
            return Math.Max(0, amount - reduction);
        }

        public int NavalExpansionEnemyLoadGain(int amount, bool consume = false)
        {
            return Math.Max(0, amount);
        }

        public int NavalExpansionEnemyAttackDamage(int total, bool consume = false)
        {
            total = Math.Max(0, total);
            if (!NavalExpansionActive) return total;
            return Math.Max(0, total - Battle.NavalExpansion.PulseReduction);
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
            state.PrechargeBonus = 0;
        }

        public void NavalExpansionEndEnemyTurn()
        {
            if (Battle == null) return;
            CoreNavalExpansionBattleState state = Battle.NavalExpansion;
            state.LoadSeal = state.EnemyHealingHalved = false;
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
            state.CurseTurns = state.CurseMarks;
            if (state.CurseMarks > 0) return;
            state.CurseTurns = state.CurseDamagePerMark = state.CurseNextTickBonus = state.CurseWeaknessPercent = 0;
        }
    }
}
