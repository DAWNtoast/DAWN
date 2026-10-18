using System;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreSchoolMechanic
    {
        None, FireLoad, FireIgnite, FireExploit, FireIgniteOrExtend, FireExtend, FireGuard, FireHeal,
        FirePrepareGuard, FireNaturalGuard, FireBoardingSeed, FireBoardingCover, FireExtraTick, FireScorch, FireOverflow,
        ArcaneCharge, ArcaneRelease, ArcaneWard, ArcaneMend, ArcaneEcho,
        CurseMark, CurseDetonate, CurseExploit, CurseWard, CurseDrain,
        TideWard, TideRiposte, TideShift, TideCleanse, TideRenew,
        ThunderPrime, ThunderChain, ThunderFinish, ThunderWard, ThunderEcho,
        BloodRisk, BloodDrain, BloodWard, BloodMend, BloodExecute,
        ArcaneCapacity, ArcaneTemporary, ThunderReady,
        SpecialFireDoctrine, SpecialArcaneDoctrine, SpecialCurseDoctrine,
        SpecialTideDoctrine, SpecialThunderDoctrine, SpecialBloodDoctrine,
    }

    [Flags]
    public enum CoreSchoolUpgradeFields
    {
        None = 0, Power = 1, Secondary = 2, Threshold = 4, Duration = 8, Limit = 16, Movement = 32,
    }

    /// <summary>Immutable, directly attached effect data. No catalog lookup is required during construction.</summary>
    public sealed class CoreSchoolCardEffect
    {
        public CoreSchoolCardEffect(CoreSchoolMechanic mechanic, int power = 0, int secondary = 0,
            int threshold = 0, int duration = 0, int limit = 0, int movement = 0,
            CoreSchoolUpgradeFields upgrades = CoreSchoolUpgradeFields.Power | CoreSchoolUpgradeFields.Secondary,
            int fallbackDamage = 0, int fallbackBlock = 0, int woodCost = 0)
        {
            Mechanic = mechanic; Power = Math.Max(0, power); Secondary = Math.Max(0, secondary);
            Threshold = Math.Max(0, threshold); Duration = Math.Max(0, duration); Limit = Math.Max(0, limit);
            Movement = movement; Upgrades = upgrades;
            FallbackDamage = Math.Max(0, fallbackDamage); FallbackBlock = Math.Max(0, fallbackBlock);
            WoodCost = Math.Max(0, woodCost);
        }
        public CoreSchoolMechanic Mechanic { get; }
        public int Power { get; }
        public int Secondary { get; }
        public int Threshold { get; }
        public int Duration { get; }
        public int Limit { get; }
        public int Movement { get; }
        public CoreSchoolUpgradeFields Upgrades { get; }
        public int FallbackDamage { get; }
        public int FallbackBlock { get; }
        public int WoodCost { get; }
        public CoreSchoolCardEffect AtLevel(int level, bool allowExtraDraw = true, int utilityMaximumLevel = 3)
        {
            if (level < 1) return this;
            int power = Upgrade(Power, CoreSchoolUpgradeFields.Power, level);
            if (Mechanic == CoreSchoolMechanic.ArcaneEcho || Mechanic == CoreSchoolMechanic.ThunderEcho)
                power = (int)Math.Min(int.MaxValue, (long)Power +
                    (allowExtraDraw ? Math.Max(0, Math.Min(level, utilityMaximumLevel)) : 0));
            return new CoreSchoolCardEffect(Mechanic, power,
                Upgrade(Secondary, CoreSchoolUpgradeFields.Secondary, level), Upgrade(Threshold, CoreSchoolUpgradeFields.Threshold, level),
                Upgrade(Duration, CoreSchoolUpgradeFields.Duration, level), Upgrade(Limit, CoreSchoolUpgradeFields.Limit, level),
                Upgrade(Movement, CoreSchoolUpgradeFields.Movement, level), CoreSchoolUpgradeFields.None,
                FallbackDamage, FallbackBlock, WoodCost);
        }
        private int Upgrade(int value, CoreSchoolUpgradeFields field, int level) => (Upgrades & field) == 0 ? value :
            CoreBattleUpgradeBalance.ScaleNumerical(value, level);
    }

    /// <summary>Battle-only state. Target effects clear on boarding; mana is carried without refilling.</summary>
    public sealed class CoreSchoolBattleState
    {
        public int Mana { get; internal set; }
        public int ManaLimit { get; internal set; } = 3;
        public int TemporaryMana { get; internal set; }
        public int AvailableMana => (int)Math.Min(int.MaxValue, (long)Mana + TemporaryMana);
        public int CurseMarks { get; internal set; }
        public int CurseTurns { get; internal set; }
        public int CurseWeaknessPercent { get; internal set; }
        public int CurseDamagePerMark { get; internal set; }
        public int CurseNextTickBonus { get; internal set; }
        public int CurseGuard { get; internal set; }
        public int ThunderChain { get; internal set; }
        public int ThunderLimit { get; internal set; } = 6;
        public bool ThunderEchoUsed { get; internal set; }
        public int ThunderReadyDamage { get; internal set; }
        public int TidePower { get; internal set; }
        public int TideLimit { get; internal set; } = 20;
        public int TidePaidBlock { get; internal set; }
        public int TideCounter { get; internal set; }
        public int TideCounterMinimum { get; internal set; }
        public int TideCounterTurns { get; internal set; }
        public int TideStance { get; internal set; }
        public int TideStancePower { get; internal set; }
        public int TideStanceTurns { get; internal set; }
        public int LastGuardAbsorbed { get; internal set; }
        public int PrepareFireGuard { get; internal set; }
        public int NaturalFireGuard { get; internal set; }
        public int NaturalFireGuardTurns { get; internal set; }
        public int BoardingBurn { get; internal set; }
        public int BoardingBurnTurns { get; internal set; }
        public int BoardingCover { get; internal set; }
        public int ScorchDamage { get; internal set; }
        public int ScorchCharges { get; internal set; }
        public int OverflowAmount { get; internal set; }
        public int OverflowOwnLimit { get; internal set; }
        public int OverflowTotalLimit { get; internal set; }
        public int OverflowContribution { get; internal set; }
        public bool OverflowUsed { get; internal set; }
        public bool ExtraFireTickUsed { get; internal set; }
        internal void BeginWindow()
        {
            ThunderChain = 0; ThunderEchoUsed = false; OverflowUsed = false; ExtraFireTickUsed = false;
        }
        internal void ClearTarget()
        {
            CurseMarks = 0; CurseTurns = 0; CurseWeaknessPercent = 0;
            CurseDamagePerMark = 0; CurseNextTickBonus = 0; CurseGuard = 0;
            TemporaryMana = 0; TidePower = 0; TidePaidBlock = 0;
            TideCounter = 0; TideCounterTurns = 0; TideStance = 0; TideStancePower = 0; TideStanceTurns = 0;
            LastGuardAbsorbed = 0; NaturalFireGuard = 0; NaturalFireGuardTurns = 0;
            ScorchDamage = 0; ScorchCharges = 0;
            PrepareFireGuard = 0;
            OverflowAmount = 0; OverflowOwnLimit = 0; OverflowTotalLimit = 0; OverflowContribution = 0; OverflowUsed = false;
        }
    }
}
