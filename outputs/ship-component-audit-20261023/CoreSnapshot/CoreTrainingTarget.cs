using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreTrainingBattleOutcome { None, InProgress, Victory, Defeat }

    /// <summary>Final training stats, independent of encounter health multipliers.</summary>
    public sealed class CoreTrainingTarget
    {
        internal CoreTrainingTarget(string id, string name, int hull, int boardingHealth,
            int cannonDamage, int meleeDamage, int guard, int repair)
        {
            Id = id;
            Name = name;
            Hull = hull;
            BoardingHealth = boardingHealth;
            CannonDamage = cannonDamage;
            MeleeDamage = meleeDamage;
            Guard = guard;
            Repair = repair;
        }

        public string Id { get; }
        public string Name { get; }
        public int Hull { get; }
        public int BoardingHealth { get; }
        public int CannonDamage { get; }
        public int MeleeDamage { get; }
        public int Guard { get; }
        public int Repair { get; }
    }

    public static class CoreTrainingTargets
    {
        public static IReadOnlyList<CoreTrainingTarget> All { get; } = Array.AsReadOnly(new[]
        {
            new CoreTrainingTarget("training_target_1", "一档木桩", 120, 80, 10, 8, 2, 0),
            new CoreTrainingTarget("training_target_2", "二档木桩", 220, 140, 16, 12, 5, 2),
            new CoreTrainingTarget("training_target_3", "三档木桩", 360, 240, 24, 20, 9, 4),
            new CoreTrainingTarget("training_target_4", "四档木桩", 600, 400, 36, 30, 14, 7),
            new CoreTrainingTarget("training_target_5", "五档木桩", 1000, 680, 50, 44, 20, 10),
        });
    }
}
