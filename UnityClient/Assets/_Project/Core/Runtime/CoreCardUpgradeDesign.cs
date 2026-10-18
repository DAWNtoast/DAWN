using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Authored cumulative rewards; rank zero and all costs remain the base definition.</summary>
    public sealed class CoreCardUpgradeProfile
    {
        private readonly int[] numbers, block, repair, effect, draw, energy, actionPoints;
        internal CoreCardUpgradeProfile(int maximum, string numerical, string draws, string effects,
            string blocks, string repairs, string energies, string note, string actionPoints = "0")
        {
            if (maximum < 0 || maximum > CoreBattleUpgradeBalance.MaximumLevel) throw new ArgumentOutOfRangeException(nameof(maximum));
            MaximumLevel = maximum; Note = note ?? string.Empty;
            numbers = Parse(numerical, 100); block = Parse(blocks ?? numerical, 100);
            repair = Parse(repairs ?? numerical, 100); effect = Parse(effects ?? numerical, 100);
            draw = Parse(draws, 0); energy = Parse(energies, 0);
            this.actionPoints = Parse(actionPoints, 0);
        }
        public int MaximumLevel { get; }
        public string Note { get; }
        private int Rank(int level) => Math.Max(0, Math.Min(MaximumLevel, level));
        public decimal Multiplier(int level) => numbers[Rank(level)] / 100m;
        public decimal BlockMultiplier(int level) => block[Rank(level)] / 100m;
        public decimal RepairMultiplier(int level) => repair[Rank(level)] / 100m;
        public decimal EffectMultiplier(int level) => effect[Rank(level)] / 100m;
        public int DrawBonus(int level) => draw[Rank(level)];
        public int EnergyBonus(int level) => energy[Rank(level)];
        public int ActionPointBonus(int level) => actionPoints[Rank(level)];
        public int LastDrawGrowthLevel => LastGrowth(draw);
        public int LastEnergyGrowthLevel => LastGrowth(energy);
        public int LastActionPointGrowthLevel => LastGrowth(actionPoints);
        private int LastGrowth(int[] values)
        {
            for (int i = MaximumLevel; i > 0; i--) if (values[i] != values[i - 1]) return i;
            return 0;
        }
        private static int[] Parse(string specification, int baseline)
        {
            var values = new int[4]; values[0] = baseline;
            string[] parts = (specification ?? baseline.ToString(CultureInfo.InvariantCulture)).Split('/');
            if (parts.Length > 3) throw new ArgumentException("A growth curve has at most three ranks.");
            for (int i = 1; i < values.Length; i++)
            {
                values[i] = int.Parse(parts[Math.Min(i - 1, parts.Length - 1)], CultureInfo.InvariantCulture);
                if (values[i] < values[i - 1]) throw new ArgumentException("Upgrade rewards must not decrease.");
            }
            return values;
        }
    }

    /// <summary>One explicit profile per published card. No library access during static initialization.</summary>
    public static partial class CoreCardUpgradeDesign
    {
        private static readonly IReadOnlyDictionary<string, CoreCardUpgradeProfile> profiles = Create();
        public static IReadOnlyDictionary<string, CoreCardUpgradeProfile> All => profiles;
        public static CoreCardUpgradeProfile Find(CoreBattleCard card) => Find(card?.Id);
        public static CoreCardUpgradeProfile Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            id = CoreBattleCardAliases.CanonicalId(id);
            return profiles.TryGetValue(id, out CoreCardUpgradeProfile profile) ? profile : null;
        }
        public static decimal Multiplier(CoreBattleCard card, int level) =>
            Find(card)?.Multiplier(level) ?? CoreBattleUpgradeBalance.MultiplierForLevel(Math.Max(0, level));
        public static decimal EffectMultiplier(CoreBattleCard card, int level) =>
            Find(card)?.EffectMultiplier(level) ?? CoreBattleUpgradeBalance.MultiplierForLevel(Math.Max(0, level));
        private static int Scaled(int value, decimal multiplier) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue,
            decimal.Round(value * multiplier, 0, MidpointRounding.AwayFromZero)));
        public static int Scale(CoreBattleCard card, int value, int level) => Scaled(value, Multiplier(card, level));
        public static int ScaleBlock(CoreBattleCard card, int value, int level) =>
            Scaled(value, Find(card)?.BlockMultiplier(level) ?? Multiplier(card, level));
        public static int ScaleRepair(CoreBattleCard card, int value, int level) =>
            Scaled(value, Find(card)?.RepairMultiplier(level) ?? Multiplier(card, level));
        public static int ScaleEffect(CoreBattleCard card, int value, int level) => Scaled(value, EffectMultiplier(card, level));
        public static int DrawBonus(CoreBattleCard card, int level) => Find(card)?.DrawBonus(level) ?? LegacyUtilityBonus(card, level);
        public static int EnergyBonus(CoreBattleCard card, int level) => Find(card)?.EnergyBonus(level) ?? LegacyUtilityBonus(card, level);
        public static int ActionPointBonus(CoreBattleCard card, int level) => Find(card)?.ActionPointBonus(level) ?? 0;
        private static int LegacyUtilityBonus(CoreBattleCard card, int level) => card == null ? 0 :
            Math.Max(0, Math.Min(level, card.Energy <= 1 ? 1 : card.Energy == 2 ? 2 : 3));

        public static CoreSchoolCardEffect UpgradeSchoolEffect(CoreBattleCard card, int level)
        {
            CoreSchoolCardEffect effect = card?.SchoolEffect;
            if (effect == null || level <= 0) return effect;
            int Grow(int value, CoreSchoolUpgradeFields field) => (effect.Upgrades & field) == 0 ? value : ScaleEffect(card, value, level);
            bool echo = effect.Mechanic == CoreSchoolMechanic.ArcaneEcho || effect.Mechanic == CoreSchoolMechanic.ThunderEcho;
            int power = echo ? (int)Math.Min(int.MaxValue, (long)effect.Power +
                (card.Draw == 0 ? DrawBonus(card, level) : 0)) : Grow(effect.Power, CoreSchoolUpgradeFields.Power);
            return new CoreSchoolCardEffect(effect.Mechanic, power,
                Grow(effect.Secondary, CoreSchoolUpgradeFields.Secondary), Grow(effect.Threshold, CoreSchoolUpgradeFields.Threshold),
                Grow(effect.Duration, CoreSchoolUpgradeFields.Duration), Grow(effect.Limit, CoreSchoolUpgradeFields.Limit),
                Grow(effect.Movement, CoreSchoolUpgradeFields.Movement), CoreSchoolUpgradeFields.None,
                effect.FallbackDamage, effect.FallbackBlock, effect.WoodCost);
        }

        private static IReadOnlyDictionary<string, CoreCardUpgradeProfile> Create()
        {
            var entries = new Dictionary<string, CoreCardUpgradeProfile>(StringComparer.Ordinal);
            RegisterStandard(entries);
            RegisterSpecial(entries);
            return new ReadOnlyDictionary<string, CoreCardUpgradeProfile>(entries);
        }
        private static void Add(Dictionary<string, CoreCardUpgradeProfile> entries, string id, int max, string numbers,
            string draw = "0", string effect = null, string block = null, string repair = null, string energy = "0", string note = "", string actionPoints = "0") =>
            entries.Add(id, new CoreCardUpgradeProfile(max, numbers, draw, effect, block, repair, energy, note, actionPoints));
    }
}
