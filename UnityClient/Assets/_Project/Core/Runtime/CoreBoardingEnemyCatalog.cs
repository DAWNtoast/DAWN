using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreBoardingEnemyMechanic
    {
        Basic, FireSelfBurn, FireTransfer, FireConsume, ArcaneGain, ArcaneSpend,
        ThunderStrike, ThunderFinisher, CurseMark, CurseDetonate,
        TideGuard, TideStrike, TideHeal, BloodStrike, BloodHarvest
    }
    /// <summary>
    /// Enemy-only boarding cards. Their names and percentage effects describe the
    /// authored enemy script; they do not impersonate cards in the player's library.
    /// Attack, burn, healing, drain and self-damage use the encounter's boarding
    /// attack baseline; guard uses its boarding guard baseline. Pierce describes
    /// the share of an attack that bypasses guard. Burn deals its current layers at
    /// each target turn end and then loses one layer.
    /// </summary>
    public sealed class CoreBoardingEnemyCard
    {
        public CoreBoardingEnemyCard(string id, string name, int attackPercent = 0,
            int guardPercent = 0, int burnPercent = 0, int healPercent = 0,
            int drainPercent = 0, int selfDamagePercent = 0, int piercePercent = 0,
            CoreBoardingEnemyMechanic mechanic = CoreBoardingEnemyMechanic.Basic,
            int resourceAmount = 0, int bonusPercent = 0, int energy = 0)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            AttackPercent = Math.Max(0, attackPercent);
            GuardPercent = Math.Max(0, guardPercent);
            BurnPercent = Math.Max(0, burnPercent);
            HealPercent = Math.Max(0, healPercent);
            DrainPercent = Math.Max(0, drainPercent);
            SelfDamagePercent = Math.Max(0, selfDamagePercent);
            PiercePercent = Math.Max(0, Math.Min(100, piercePercent));
            Mechanic = mechanic;
            ResourceAmount = Math.Max(0, resourceAmount);
            BonusPercent = Math.Max(0, bonusPercent);
            Energy = Math.Max(0, energy);
        }

        public string Id { get; }
        public string Name { get; }
        public int AttackPercent { get; }
        public int GuardPercent { get; }
        public int BurnPercent { get; }
        public int HealPercent { get; }
        public int DrainPercent { get; }
        public int SelfDamagePercent { get; }
        public int PiercePercent { get; }
        public CoreBoardingEnemyMechanic Mechanic { get; }
        public int ResourceAmount { get; }
        public int BonusPercent { get; }
        // Equivalent player energy used when authoring boss scripts, not a live resource.
        public int Energy { get; }
    }

    public sealed class CoreBoardingEnemyTurn
    {
        public CoreBoardingEnemyTurn(string name, params CoreBoardingEnemyCard[] cards)
        {
            if (cards == null || cards.Length == 0)
                throw new ArgumentException("An enemy boarding turn requires at least one card.", nameof(cards));
            var copied = new CoreBoardingEnemyCard[cards.Length];
            for (int index = 0; index < cards.Length; index++)
            {
                copied[index] = cards[index] ?? throw new ArgumentException("Enemy boarding cards cannot be null.", nameof(cards));
                TotalEnergy += copied[index].Energy;
            }
            Name = name ?? string.Empty;
            Cards = Array.AsReadOnly(copied);
        }

        public string Name { get; }
        public IReadOnlyList<CoreBoardingEnemyCard> Cards { get; }
        public int TotalEnergy { get; }
    }

    public sealed class CoreBoardingEnemyDeck
    {
        public CoreBoardingEnemyDeck(string schoolId, params CoreBoardingEnemyTurn[] turns)
        {
            if (turns == null || turns.Length == 0)
                throw new ArgumentException("An enemy boarding deck requires at least one turn.", nameof(turns));
            var copied = new CoreBoardingEnemyTurn[turns.Length];
            var cards = new List<CoreBoardingEnemyCard>();
            for (int index = 0; index < turns.Length; index++)
            {
                CoreBoardingEnemyTurn turn = turns[index] ??
                    throw new ArgumentException("Enemy boarding turns cannot be null.", nameof(turns));
                copied[index] = turn;
                foreach (CoreBoardingEnemyCard card in turn.Cards) cards.Add(card);
            }
            SchoolId = schoolId ?? "neutral";
            Turns = Array.AsReadOnly(copied);
            Cards = cards.AsReadOnly();
        }

        public string SchoolId { get; }
        public IReadOnlyList<CoreBoardingEnemyTurn> Turns { get; }
        public IReadOnlyList<CoreBoardingEnemyCard> Cards { get; }

        public CoreBoardingEnemyTurn TurnAt(int phaseTurn) => Turns[(Math.Max(1, phaseTurn) - 1) % Turns.Count];
    }

    /// <summary>Fixed, previewable enemy scripts selected from the ship's authored school.</summary>
    public static class CoreBoardingEnemyCatalog
    {
        private static readonly CoreBoardingEnemyDeck neutral = new CoreBoardingEnemyDeck("neutral",
            Turn("短兵突击", "neutral_01", attack: 100),
            Turn("架盾防线", "neutral_02", guard: 100),
            Turn("登船追击", "neutral_03", attack: 100));

        private static readonly CoreBoardingEnemyDeck fire = new CoreBoardingEnemyDeck("fire",
            new CoreBoardingEnemyTurn("引火突袭",
                Card("引火入体", "fire_01a", CoreBoardingEnemyMechanic.FireSelfBurn, resource: 2),
                Card("焰刃突袭", "fire_01b", attack: 70, burn: 15)),
            new CoreBoardingEnemyTurn("共燃守势",
                Card("炉卫列阵", "fire_02a", CoreBoardingEnemyMechanic.FireSelfBurn, resource: 2, guard: 100)),
            new CoreBoardingEnemyTurn("移焰焚尽",
                Card("移焰", "fire_03a", CoreBoardingEnemyMechanic.FireTransfer, resource: 4),
                Card("焚尽强攻", "fire_03b", CoreBoardingEnemyMechanic.FireConsume, resource: 4, attack: 105, bonus: 15)),
            new CoreBoardingEnemyTurn("持盾火刃", Card("持盾火刃", "fire_04", attack: 80, guard: 50, burn: 15)));

        private static readonly CoreBoardingEnemyDeck arcane = new CoreBoardingEnemyDeck("arcane",
            new CoreBoardingEnemyTurn("星轨蓄能", Card("蓄能护阵", "arcane_01", CoreBoardingEnemyMechanic.ArcaneGain, resource: 2, guard: 100)),
            new CoreBoardingEnemyTurn("秘术齐击", Card("秘术齐击", "arcane_02", CoreBoardingEnemyMechanic.ArcaneSpend, resource: 1, attack: 70, bonus: 30)),
            new CoreBoardingEnemyTurn("月环校准", Card("月环聚能", "arcane_03", CoreBoardingEnemyMechanic.ArcaneGain, resource: 2, guard: 80)),
            new CoreBoardingEnemyTurn("星轨释放", Card("星轨释放", "arcane_04", CoreBoardingEnemyMechanic.ArcaneSpend, resource: 3, attack: 90, bonus: 25)));

        private static readonly CoreBoardingEnemyDeck thunder = new CoreBoardingEnemyDeck("thunder",
            ThunderTurn("雷光试锋", "thunder_01", 20, 25, 15, 10),
            ThunderTurn("霆锋追击", "thunder_02", 25, 30, 20, 15),
            ThunderTurn("电弧续流", "thunder_03", 15, 15, 15, 10),
            ThunderTurn("雷冠决击", "thunder_04", 25, 30, 40, 20));

        private static readonly CoreBoardingEnemyDeck blood = new CoreBoardingEnemyDeck("blood",
            new CoreBoardingEnemyTurn("赤誓抢攻", Card("赤誓抢攻", "blood_01", CoreBoardingEnemyMechanic.BloodStrike, resource: 6, attack: 90, selfDamage: 15, bonus: 3)),
            new CoreBoardingEnemyTurn("血契护阵", Card("血契护阵", "blood_02", guard: 80, selfDamage: 15)),
            new CoreBoardingEnemyTurn("偿血追猎", Card("偿血追猎", "blood_03", CoreBoardingEnemyMechanic.BloodHarvest, resource: 6, attack: 85, drain: 20, bonus: 3)),
            new CoreBoardingEnemyTurn("决死赤刃", Card("决死赤刃", "blood_04", CoreBoardingEnemyMechanic.BloodStrike, resource: 12, attack: 110, selfDamage: 20, bonus: 3)));

        private static readonly CoreBoardingEnemyDeck tide = new CoreBoardingEnemyDeck("tide",
            new CoreBoardingEnemyTurn("潮壁列阵", Card("留潮壁", "tide_01", CoreBoardingEnemyMechanic.TideGuard, guard: 120)),
            new CoreBoardingEnemyTurn("退潮反击", Card("退潮反击", "tide_02", CoreBoardingEnemyMechanic.TideStrike, resource: 8, attack: 60, bonus: 5)),
            new CoreBoardingEnemyTurn("回潮援护",
                Card("回潮治疗", "tide_03a", CoreBoardingEnemyMechanic.TideHeal, resource: 6, heal: 25),
                Card("留潮守势", "tide_03b", CoreBoardingEnemyMechanic.TideGuard, guard: 80)),
            new CoreBoardingEnemyTurn("巨浪推进",
                Card("巨浪反击", "tide_04a", CoreBoardingEnemyMechanic.TideStrike, resource: 12, attack: 75, bonus: 5),
                Card("留潮护卫", "tide_04b", CoreBoardingEnemyMechanic.TideGuard, guard: 50)));

        private static readonly CoreBoardingEnemyDeck curse = new CoreBoardingEnemyDeck("curse",
            new CoreBoardingEnemyTurn("蚀魂施印", Card("蚀魂施印", "curse_01", CoreBoardingEnemyMechanic.CurseMark, resource: 2, attack: 55)),
            new CoreBoardingEnemyTurn("咒卫结阵", Card("咒卫叠印", "curse_02", CoreBoardingEnemyMechanic.CurseMark, resource: 2, guard: 100)),
            new CoreBoardingEnemyTurn("月蚀刻痕", Card("月蚀刻痕", "curse_03", CoreBoardingEnemyMechanic.CurseMark, resource: 1, attack: 65)),
            new CoreBoardingEnemyTurn("咒痕引爆", Card("咒痕引爆", "curse_04", CoreBoardingEnemyMechanic.CurseDetonate, resource: 6, attack: 45, bonus: 20)));

        public static CoreBoardingEnemyDeck ForSchool(string schoolId)
        {
            switch (schoolId)
            {
                case "fire": return fire;
                case "arcane": return arcane;
                case "thunder": return thunder;
                case "blood": return blood;
                case "tide": return tide;
                case "curse": return curse;
                default: return neutral;
            }
        }

        public static CoreBoardingEnemyDeck ForEncounter(string encounterId) =>
            CoreBoardingBossCatalog.ForEncounter(encounterId) ?? ForSchool(ResolveSchool(encounterId));

        public static string ResolveSchool(string encounterId)
        {
            CoreRouteBoss route = CoreRouteBossCatalog.Find(encounterId);
            if (route != null) return route.SchoolId;
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(encounterId);
            if (legendary != null) return legendary.SchoolId;
            // The vault guardian has no historical ship school. Its boarding
            // script explicitly defaults to curse, matching the moon-vault theme.
            if (encounterId == CoreBattleBalance.TreasureBossId) return "curse";
            return "neutral";
        }

        private static CoreBoardingEnemyTurn Turn(string name, string key, int attack = 0,
            int guard = 0, int burn = 0, int heal = 0, int drain = 0, int selfDamage = 0, int pierce = 0) =>
            new CoreBoardingEnemyTurn(name, new CoreBoardingEnemyCard("enemy_boarding_" + key, name,
                attack, guard, burn, heal, drain, selfDamage, pierce));

        private static CoreBoardingEnemyCard Card(string name, string key,
            CoreBoardingEnemyMechanic mechanic = CoreBoardingEnemyMechanic.Basic, int resource = 0,
            int attack = 0, int guard = 0, int burn = 0, int heal = 0, int drain = 0,
            int selfDamage = 0, int bonus = 0) => new CoreBoardingEnemyCard("enemy_boarding_" + key,
                name, attack, guard, burn, heal, drain, selfDamage, 0, mechanic, resource, bonus);

        private static CoreBoardingEnemyTurn ThunderTurn(string name, string key, int first, int second, int finish, int bonus) =>
            new CoreBoardingEnemyTurn(name,
                Card("电火花", key + "a", CoreBoardingEnemyMechanic.ThunderStrike, attack: first),
                Card("电弧连击", key + "b", CoreBoardingEnemyMechanic.ThunderStrike, attack: second),
                Card("耗链终结", key + "c", CoreBoardingEnemyMechanic.ThunderFinisher, resource: 6, attack: finish, bonus: bonus));
    }
}
