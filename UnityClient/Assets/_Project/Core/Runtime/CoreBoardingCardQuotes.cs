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
            int mana = Battle.Schools.AvailableMana;
            int marks = BState.CurseMarks, tide = BState.TidePower;
            int chain = Math.Max(0, Battle.Schools.ThunderChain);
            int damage = card.Damage;
            int bonus = BQuotedAttackBonus(card);
            switch (key)
            {
                case "fire_11": damage = CoreBoardingCardBalance.SelfBurnDamage(SchoolSum(BStackTotal(BState.SelfBurn), PrepareFireBurnApplication(true, 3, false)), card.UpgradeLevel); break;
                case "arcane_04": damage = BFormula(card, 48, 6, mana); break;
                case "arcane_06": return BQuotedFieldMultiHit(BValue(card, 5), SchoolSum(4, mana), bonus);
                case "arcane_11": damage = BFormula(card, 72, 8, mana); break;
                case "curse_06": damage = BFormula(card, 12, 1, marks); break;
                case "curse_11": damage = BFormula(card, 40, 8, marks); break;
                case "tide_06": damage = BFormula(card, 12, 1, tide); break;
                case "tide_07": damage = BFormula(card, 32, 1, tide); break;
                case "tide_08": damage = BFormula(card, 60, 1.5m, tide); break;
                case "thunder_02": damage = BFormula(card, 16, 2, chain); break;
                case "thunder_05": damage = BFormula(card, 28, 2, chain); break;
                case "thunder_11": return BQuotedFieldMultiHit(BFormula(card, 6, 3, chain), 2, bonus);
                case "thunder_13": damage = BFormula(card, 40, 7, chain); break;
                case "thunder_14": damage = BFormula(card, 32, 6, chain); break;
                case "thunder_15": damage = BThunderFinaleDamage(card, chain); break;
                case "blood_07":
                    int first = BBoardingFieldAttackDamage(SchoolSum(BLinear(card, 12), bonus));
                    return SchoolSum(first, BBoardingFieldAttackDamage(BLinear(card,
                        first > Battle.EnemyBlock ? 16 : 12)));
                case "blood_11": damage = BLinear(card, CTBDesperate ? 17 : 7); break;
                case "blood_13": damage = BLinear(card, CTBDesperate ? 22 : 12); break;
            }
            damage = SchoolSum(damage, bonus);
            return card.BoardingSpec.IsAttack || key == "curse_11" ? BBoardingFieldAttackDamage(damage) : damage;
        }

        private int BQuotedFieldMultiHit(int perHit, int hits, int bonus)
            => hits <= 0 ? 0 : SchoolSum(BBoardingFieldAttackDamage(SchoolSum(perHit, bonus)),
                SchoolProduct(BBoardingFieldAttackDamage(perHit), hits - 1));

        private int BQuotedAttackBonus(CoreBattleCard card)
        {
            if (!BActive || card?.BoardingSpec?.IsAttack != true) return 0;
            int bonus = card.SchoolId == "thunder" ? BState.ThunderReady : 0;
            if (BState.BloodOathPlayerEnds > 0)
                bonus = SchoolSum(bonus, BLinear(BState.BloodOathCard, 8));
            return bonus;
        }
    }
}
