using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>
        /// Read-only direct (or scheduled) attack amount before critical hits and guard.
        /// Burn, counterattacks, residual hits and future echoes are separate effects.
        /// Quoting never spends resources, changes a timer, or selects a fuel card.
        /// </summary>
        public int GetBoardingCardDamageQuote(CoreBattleCard card)
        {
            card = CoreBoardingCardCatalog.ForBoarding(card);
            if (card?.BoardingSpec == null) return card?.Damage ?? 0;
            if (!BActive) return card.Damage;
            string key = card.BoardingSpec.Key;
            int mana = Math.Min(3, Battle.Schools.AvailableMana);
            int marks = BState.CurseMarks, tide = BState.TidePower;
            int chain = Math.Min(6, Math.Max(0, Battle.Schools.ThunderChain));
            int damage = card.Damage;
            int bonus = BQuotedAttackBonus(card);
            switch (key)
            {
                case "fire_11": damage = BValue(card, 30 + BStackTotal(BState.SelfBurn) * 2); break;
                case "arcane_04": damage = BValue(card, 24 + 6 * mana); break;
                case "arcane_06": damage = SchoolProduct(BValue(card, 5), 2 + mana); break;
                case "arcane_11": damage = BValue(card, 36 + 8 * mana); break;
                case "curse_06": damage = BValue(card, 6 + marks); break;
                case "curse_11": damage = BValue(card, 20 + 8 * marks); break;
                case "tide_06": damage = BValue(card, 6 + tide); break;
                case "tide_07": damage = BValue(card, 16 + tide); break;
                case "tide_08": damage = BValue(card, 30 + tide * 3 / 2); break;
                case "thunder_02": damage = BValue(card, 8 + chain); break;
                case "thunder_05": damage = BValue(card, 14 + chain); break;
                case "thunder_11": damage = SchoolProduct(BValue(card, 3 + chain), 2); break;
                case "thunder_13": damage = BValue(card, 20 + 5 * chain); break;
                case "thunder_14": damage = BValue(card, 16 + 4 * chain); break;
                case "thunder_15": damage = BValue(card, 28 + 6 * chain); break;
                case "blood_07":
                    damage = SchoolSum(BValue(card, 12), BValue(card,
                        SchoolSum(BValue(card, 12), bonus) > Battle.EnemyBlock ? 16 : 12));
                    break;
                case "blood_11": damage = BValue(card, CTBDesperate ? 14 : 10); break;
                case "blood_13": damage = BValue(card, CTBDesperate ? 18 : 12); break;
            }
            return SchoolSum(damage, bonus);
        }

        private int BQuotedAttackBonus(CoreBattleCard card)
        {
            if (!BActive || card?.BoardingSpec?.IsAttack != true) return 0;
            int bonus = card.SchoolId == "thunder" ? BState.ThunderReady : 0;
            if (card.BoardingSpec.IsDelayed || BState.BloodOathPlayerEnds <= 0 || BState.BloodOathTriggered) return bonus;
            int payment = card.BoardingSpec.Key == "blood_01" ? 3 : card.BoardingSpec.Key == "blood_02" ? 4 : 0;
            int remainingHealth = Battle.PlayerBoardingHealth - payment;
            if ((long)remainingHealth * 100 <= (long)Battle.PlayerMaxBoardingHealth * 40)
                bonus = SchoolSum(bonus, BValue(BState.BloodOathCard, 8));
            return bonus;
        }
    }
}
