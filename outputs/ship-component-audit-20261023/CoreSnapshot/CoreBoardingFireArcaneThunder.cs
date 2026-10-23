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
                case "fire_14":
                    if (SchoolPlayerHealth <= 2) return "支付2生命后须至少剩1生命";
                    break;
                case "fire_15": if (BStackTotal(BState.SelfBurn) == 0) return "没有可以转移的自燃"; break;
                case "arcane_05":
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    if (Cargo.Medicine > 0 && mana < 2) return "有药治疗固定需要2魔力";
                    break;
                case "arcane_07":
                case "arcane_08":
                case "arcane_09":
                case "arcane_12":
                case "arcane_14":
                    if (mana < 1) return "需要1魔力";
                    break;
                case "arcane_10":
                    if (mana < 2) return "需要2魔力";
                    break;
                case "arcane_13":
                    if (mana < 1) return "需要1魔力";
                    break;
                case "arcane_15":
                    if (mana < 2) return "需要2魔力";
                    if (BState.DelayedDamage.Count == 0) return "没有待结算的延迟攻击";
                    break;
                case "thunder_01": if (BOnceUsed(key)) return "电火花每回合只能使用一次"; break;
                case "thunder_06": if (BState.ThunderReady > 0) return "电弧导流仍在等待攻击"; break;
                case "thunder_07":
                    if (Battle.Schools.ThunderChain < 2) return "至少需要2连锁";
                    if (BOnceUsed(key)) return "风暴续令每回合只能使用一次";
                    break;
                case "thunder_09":
                    if (Battle.Schools.ThunderChain < 2) return "需要2连锁";
                    if (BOnceUsed(key)) return "导电回流每回合只能使用一次";
                    break;
                case "thunder_10": if (BState.ThunderResidualCharges > 0) return "雷网残响仍有未用次数"; break;
            }
            return string.Empty;
        }

        // Called once after the complete play has passed validation, before any effects or attacks.
        private void FATPayLifeCost(CoreBattleCard card)
        {
            if (card?.BoardingSpec?.Key == "fire_14") Battle.PlayerBoardingHealth -= 2;
        }

        private bool FATExecute(CoreBattleCard card, CoreBoardingPlayOptions options)
        {
            string key = card?.BoardingSpec?.Key;
            int amount = BFixedAmount(key, options);
            int chain = Math.Max(0, Math.Min(6, Battle.Schools.ThunderChain));
            int mana, total, guard;
            switch (key)
            {
                case "fire_01": BDeal(BValue(card, 28)); break;
                case "fire_02": BDeal(BValue(card, 8)); BAddEnemyBurn(BValue(card, 4)); break;
                case "fire_03": BAddEnemyBurn(BValue(card, 8)); break;
                case "fire_04":
                    BAddEnemyBurn(BValue(card, 6));
                    BState.FirePots.Add(new CoreBoardingTimedStacks(BValue(card, 6), 2));
                    break;
                case "fire_05":
                    // The shared play pipeline pays the powder (or consumes a free-powder charge).
                    if (Cargo.Medicine > 0) { Cargo.Medicine--; BHeal(BValue(card, 10)); }
                    else BHeal(2);
                    break;
                case "fire_06":
                    BDeal(BValue(card, 8)); BAddEnemyBurn(BValue(card, BState.BurnedAttackCount > 0 ? 5 : 3));
                    break;
                case "fire_07": BAddEnemyBurn(BValue(card, 4 + 3 * amount)); break;
                case "fire_08": BGuard(BValue(card, 2 + 4 * amount)); break;
                case "fire_09": BDraw(card.Draw); break;
                case "fire_10": BState.FreePowderUses = SchoolSum(BState.FreePowderUses, 2); break;
                case "fire_11":
                    total = BStackTotal(BState.SelfBurn);
                    BAddSelfBurn(3);
                    BDeal(BValue(card, 30 + total * 2));
                    break;
                case "fire_12": BState.FirePactTurns = 2; BState.FirePactUsed = false; break;
                case "fire_13": BAddSelfBurn(2); BGuard(BValue(card, 8)); BState.FireRetaliations++; break;
                case "fire_14":
                    BAddSelfBurn(2); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key);
                    break;
                case "fire_15": BTransferSelfBurn(4); break;

                case "arcane_01": BDeal(BValue(card, 6)); FATGainMana(1); break;
                case "arcane_02": BGuard(BValue(card, 3)); FATGainMana(1); break;
                case "arcane_03": FATGainMana(3); break;
                case "arcane_04":
                    mana = Math.Min(3, Battle.Schools.AvailableMana); BSpendMana(mana);
                    BDeal(BValue(card, 24 + 6 * mana));
                    break;
                case "arcane_05":
                    if (Cargo.Medicine > 0) { BSpendMana(2); Cargo.Medicine--; BHeal(BValue(card, 13)); }
                    else BHeal(2);
                    break;
                case "arcane_06":
                    mana = Math.Min(3, Battle.Schools.AvailableMana); BSpendMana(mana);
                    for (int hit = 0; hit < 2 + mana && SchoolEnemyHealth > 0; hit++) BDeal(BValue(card, 5));
                    break;
                case "arcane_07":
                    BSpendMana(1); Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BValue(card, 8));
                    BDeal(BValue(card, 10));
                    break;
                case "arcane_08":
                    BSpendMana(1); guard = BValue(card, 5); BGuard(guard);
                    BAddRetainedGuard(guard);
                    break;
                case "arcane_09": BSpendMana(1); BReadSelected(options); break;
                case "arcane_10":
                    BSpendMana(2); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key);
                    break;
                case "arcane_11":
                    mana = Math.Min(3, Battle.Schools.AvailableMana); BSpendMana(mana);
                    BQueueDamage(BValue(card, 36 + 8 * mana));
                    break;
                case "arcane_12": BSpendMana(1); BSealSelected(options, true); break;
                case "arcane_13":
                    BSpendMana(1); BState.ArcaneEchoReady = true; BState.ArcaneEchoCard = card;
                    BState.ArcaneEchoCharges = SchoolSum(BState.ArcaneEchoCharges, 1); break;
                case "arcane_14": BSpendMana(1); BRecoverSelected(options); break;
                case "arcane_15": BSpendMana(2); BResolveOldestDelayed(); break;

                case "thunder_01": BDeal(BValue(card, 3)); BMarkOnce(key); break;
                case "thunder_02": BDeal(BValue(card, 8 + chain)); break;
                case "thunder_03": BDeal(BValue(card, 4)); BDraw(card.Draw); break;
                case "thunder_04":
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BValue(card, 6)); BDeal(BValue(card, 7));
                    break;
                case "thunder_05": BQueueDamage(BValue(card, 14 + chain)); break;
                case "thunder_06": BDraw(card.Draw); BState.ThunderReady = BValue(card, 8); break;
                case "thunder_07": BDraw(card.Draw); BMarkOnce(key); break;
                case "thunder_08": BDraw(card.Draw + (chain >= 3 ? 1 : 0)); break;
                case "thunder_09":
                    Battle.Schools.ThunderChain -= 2; Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key);
                    break;
                case "thunder_10": BState.ThunderResidualCharges = 3; BState.ThunderResidualDamage = BValue(card, 4); break;
                case "thunder_11":
                    BDeal(BValue(card, 3 + chain));
                    if (SchoolEnemyHealth > 0) BDeal(BValue(card, 3 + chain));
                    break;
                case "thunder_12":
                    BDeal(BValue(card, 5));
                    BState.ThunderWeakness = Math.Max(BState.ThunderWeakness, BValue(card, 2 + chain / 2));
                    BState.ThunderWeaknessTurns = 1;
                    break;
                case "thunder_13":
                    Battle.Schools.ThunderChain = 0; BDeal(BValue(card, 20 + 5 * chain));
                    break;
                case "thunder_14":
                    Battle.Schools.ThunderChain = 0; BDeal(BValue(card, 16 + 4 * chain), ignoreBlock: true);
                    break;
                case "thunder_15":
                    Battle.Schools.ThunderChain = 0; BDeal(BValue(card, 28 + 6 * chain));
                    break;
                default: return false;
            }
            return true;
        }

        private void FATGainMana(int amount) => Battle.Schools.Mana = Math.Min(Battle.Schools.ManaLimit,
            SchoolSum(Battle.Schools.Mana, amount));
    }
}

