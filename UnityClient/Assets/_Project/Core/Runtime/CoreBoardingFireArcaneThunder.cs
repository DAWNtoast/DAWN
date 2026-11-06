using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreBoardingRetainedGuard
    {
        public int Amount;
        public int Expiry;
    }

    public sealed partial class CoreBoardingBattleState
    {
        public List<CoreBoardingRetainedGuard> RetainedGuardBatches { get; } = new List<CoreBoardingRetainedGuard>();
    }
    public sealed partial class CoreVoyageSession
    {
        private string FATUnavailableReason(CoreBattleCard card, int amount)
        {
            string key = card?.BoardingSpec?.Key;
            int mana = Battle.Schools.AvailableMana;
            switch (key)
            {
                case "fire_05":
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    break;
                case "fire_15": if (BStackTotal(BState.SelfBurn) == 0) return "没有可以转移的燃烧"; break;
                case "arcane_05":
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    if (Cargo.Medicine > 0 && mana < CoreBoardingCardBalance.Cost(key)) return "有药治疗固定需要4魔力";
                    break;
                case "arcane_07":
                case "arcane_08":
                case "arcane_09":
                case "arcane_12":
                case "arcane_14":
                    if (mana < CoreBoardingCardBalance.Cost(key)) return "需要2魔力";
                    break;
                case "arcane_10":
                    if (mana < CoreBoardingCardBalance.Cost(key)) return "需要4魔力";
                    break;
                case "arcane_13":
                    if (mana < CoreBoardingCardBalance.Cost(key)) return "需要2魔力";
                    break;
                case "arcane_15":
                    if (mana < CoreBoardingCardBalance.Cost(key)) return "需要4魔力";
                    if (BState.DelayedDamage.Count == 0) return "没有待结算的延迟攻击";
                    break;
                case "thunder_06": if (BState.ThunderReady > 0) return "电弧导流仍在等待攻击"; break;
                case "thunder_10": if (BState.ThunderResidualCharges > 0) return "雷网残响仍有未用次数"; break;
            }
            return string.Empty;
        }

        private bool FATExecute(CoreBattleCard card, CoreBoardingPlayOptions options)
        {
            string key = card?.BoardingSpec?.Key;
            int amount = BFixedAmount(key, options);
            int chain = Math.Max(0, Battle.Schools.ThunderChain);
            int mana, total, guard;
            switch (key)
            {
                case "fire_01": BDeal(BLinear(card, 28)); break;
                case "fire_02": BDeal(BLinear(card, 8)); BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel)); break;
                case "fire_03": BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel)); break;
                case "fire_04":
                    BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel));
                    BState.FirePots.Add(new CoreBoardingTimedStacks(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel), 1));
                    break;
                case "fire_05":
                    // The shared play pipeline pays the powder (or consumes a free-powder charge).
                    if (Cargo.Medicine > 0) { Cargo.Medicine--; BHeal(BLinear(card, 10)); }
                    else BHeal(2);
                    break;
                case "fire_06":
                    BDeal(BLinear(card, 8)); BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel, BState.BurnedAttackCount > 0));
                    break;
                case "fire_07": BAddEnemyBurn(CoreBoardingCardBalance.BurnFuel(amount, card.UpgradeLevel)); break;
                case "fire_08": BGuard(BFormula(card, 4, 8, amount)); break;
                case "fire_09": BDraw(card.Draw); break;
                case "fire_10": BState.FreePowderUses = SchoolSum(BState.FreePowderUses, 2); break;
                case "fire_11":
                    BAddSelfBurn(3);
                    total = BStackTotal(BState.SelfBurn);
                    BDeal(CoreBoardingCardBalance.SelfBurnDamage(total, card.UpgradeLevel));
                    break;
                case "fire_12":
                    BState.FirePacts.Add(new CoreBoardingEffectWindow(card, 2));
                    BState.FirePactTurns = 2; BState.FirePactUsed = false; break;
                case "fire_13": BAddSelfBurn(2); BGuard(BGuardValue(card, 8)); BState.FireRetaliations++; break;
                case "fire_14":
                    BAddSelfBurn(2); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key);
                    break;
                case "fire_15": BTransferSelfBurn(BStackTotal(BState.SelfBurn)); break;

                case "arcane_01": BDeal(BLinear(card, 6)); FATGainMana(CoreBoardingCardBalance.Mana(key, card.UpgradeLevel)); break;
                case "arcane_02": BGuard(BGuardValue(card, 3)); FATGainMana(CoreBoardingCardBalance.Mana(key, card.UpgradeLevel)); break;
                case "arcane_03": FATGainMana(CoreBoardingCardBalance.Mana(key, card.UpgradeLevel)); break;
                case "arcane_04":
                    mana = Battle.Schools.AvailableMana; BSpendMana(mana);
                    BDeal(BFormula(card, 48, 6, mana));
                    break;
                case "arcane_05":
                    if (Cargo.Medicine > 0) { BSpendMana(CoreBoardingCardBalance.Cost(key)); Cargo.Medicine--; BHeal(BLinear(card, 13)); }
                    else BHeal(2);
                    break;
                case "arcane_06":
                    mana = Battle.Schools.AvailableMana; BSpendMana(mana);
                    BDealArcaneMissiles(BValue(card, 5), SchoolSum(4, mana));
                    break;
                case "arcane_07":
                    BSpendMana(CoreBoardingCardBalance.Cost(key)); Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BLinear(card, 8));
                    BDeal(BLinear(card, 10));
                    break;
                case "arcane_08":
                    BSpendMana(CoreBoardingCardBalance.Cost(key)); guard = BGuardValue(card, 5); BGuard(guard);
                    BAddRetainedGuard(guard);
                    break;
                case "arcane_09": BSpendMana(CoreBoardingCardBalance.Cost(key)); BReadSelected(options); break;
                case "arcane_10":
                    BSpendMana(CoreBoardingCardBalance.Cost(key)); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key);
                    break;
                case "arcane_11":
                    mana = Battle.Schools.AvailableMana; BSpendMana(mana);
                    BQueueDamage(BFormula(card, 72, 8, mana));
                    break;
                case "arcane_12": BSpendMana(CoreBoardingCardBalance.Cost(key)); BSealSelected(options, true); break;
                case "arcane_13":
                    BSpendMana(CoreBoardingCardBalance.Cost(key)); BState.ArcaneEchoReady = true; BState.ArcaneEchoCard = card;
                    BState.ArcaneEchoCards.Add(card);
                    BState.ArcaneEchoCharges = SchoolSum(BState.ArcaneEchoCharges, 1); break;
                case "arcane_14": BSpendMana(CoreBoardingCardBalance.Cost(key)); BRecoverSelected(options); break;
                case "arcane_15": BSpendMana(CoreBoardingCardBalance.Cost(key)); BResolveSelectedDelayed(options.SelectionIndexes[0]); break;

                case "thunder_01": BDeal(BLinear(card, 3)); break;
                case "thunder_02": BDeal(BFormula(card, 16, 2, chain)); break;
                case "thunder_03": BDeal(BLinear(card, 4)); BDraw(card.Draw); break;
                case "thunder_04":
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BLinear(card, 6)); BDeal(BLinear(card, 7));
                    break;
                case "thunder_05": BQueueDamage(BFormula(card, 28, 2, chain)); break;
                case "thunder_06": BDraw(card.Draw); BState.ThunderReady = BLinear(card, 8); break;
                case "thunder_07": if (chain >= 2) BDraw(card.Draw); break;
                case "thunder_08": BDraw(card.Draw + (chain >= 3 ? 1 : 0)); break;
                case "thunder_09":
                    if (chain >= 2) Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card));
                    break;
                case "thunder_10": BState.ThunderResidualCharges = 3; BState.ThunderResidualDamage = BLinear(card, 4); break;
                case "thunder_11":
                    BDeal(BFormula(card, 6, 3, chain));
                    if (SchoolEnemyHealth > 0) BDeal(BFormula(card, 6, 3, chain));
                    break;
                case "thunder_12":
                    BDeal(BLinear(card, 5));
                    if (BCurseExecutionEnded) break;
                    if (chain >= 2)
                    {
                        BState.ThunderWeakness = Math.Max(BState.ThunderWeakness, BLinear(card, 10));
                        BState.ThunderWeaknessTurns = 1;
                    }
                    break;
                case "thunder_13":
                    ConsumeThunderChain(true, chain); BDeal(BFormula(card, 40, 7, chain));
                    break;
                case "thunder_14":
                    ConsumeThunderChain(true, chain); BDeal(BFormula(card, 32, 6, chain), ignoreBlock: true);
                    break;
                case "thunder_15":
                    ConsumeThunderChain(true, chain); BDeal(BThunderFinaleDamage(card, chain));
                    break;
                default: return false;
            }
            return true;
        }

        private void FATGainMana(int amount) { if (!BCurseExecutionEnded) GainBattleMana(amount); }

        private int BThunderFinaleDamage(CoreBattleCard card, int chain)
        {
            int damage = BFormula(card, 56, 8, chain);
            if (chain >= 10) damage = DoubleBattleDamage(damage);
            if (chain >= 20) damage = DoubleBattleDamage(damage);
            return damage;
        }

        private void BDealArcaneMissiles(int amount, int hits)
        {
            if (hits <= 0 || amount <= 0 || SchoolEnemyHealth <= 0 || SchoolPlayerHealth <= 0) return;
            // The first hit consumes the one-use attack bonus. Remaining hits have
            // identical damage and only contribute guard, health and school resources.
            // Settle those totals directly so an uncapped mana pool cannot cause an
            // arbitrarily long loop; stop accounting at the same lethal hit.
            BDeal(amount);
            if (--hits <= 0 || SchoolEnemyHealth <= 0) return;
            int resolved = BState.ActiveAttack && BState.ActiveCritical ? DoubleBattleDamage(amount) : amount;
            if (BState.ActiveAttack) resolved = BBoardingFieldAttackDamage(resolved);
            int threshold = Battle.Schools.EnemyCurseExecutionThresholdPercent;
            long executionFloor = BState.CurseMarks > 0 && threshold > 0
                ? Math.Max(0, (long)BState.CurseMarks * 100 / threshold) : 0;
            long targetRemaining = (long)Battle.EnemyBlock + Math.Max(1, Battle.EnemyBoardingHealth - executionFloor);
            int resolvedHits = (int)Math.Min(hits, (targetRemaining + resolved - 1) / resolved);
            if (BState.ActiveAttack)
                BState.ActiveAuthoredDamage = SchoolSum(BState.ActiveAuthoredDamage, SchoolProduct(amount, resolvedHits));
            long damage = (long)resolved * resolvedHits;
            int blocked = (int)Math.Min(Battle.EnemyBlock, damage);
            Battle.EnemyBlock -= blocked;
            int lost = (int)Math.Min(Battle.EnemyBoardingHealth, damage - blocked);
            Battle.EnemyBoardingHealth -= lost;
            OnBoardingEnemyDamaged(blocked, lost);
            BResolveCurseExecutions();
        }
    }
}

