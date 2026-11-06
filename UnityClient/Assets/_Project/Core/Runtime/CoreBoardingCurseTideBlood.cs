using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    internal sealed class CoreBoardingCurseLayer
    {
        internal int EnemyEnds = 3;
        internal bool Extended = false;
    }

    internal sealed class CoreBoardingTidePoint
    {
        internal int PlayerEnds = 1;
        internal bool Extended = false;
    }

    public sealed partial class CoreBoardingBattleState
    {
        internal readonly List<CoreBoardingCurseLayer> CurseLayers = new List<CoreBoardingCurseLayer>();
        internal readonly List<CoreBoardingTidePoint> TidePoints = new List<CoreBoardingTidePoint>();
        public int CurseMarks => CurseLayers.Count;
        public bool PlayerExecutedByCurse { get; internal set; }
        public bool EnemyExecutedByCurse { get; internal set; }
        internal int EnemyHealthRemovedByCurse;
        internal int TideOverflow;
        public int TidePower => (int)Math.Min(int.MaxValue, (long)TidePoints.Count + TideOverflow);
        public int ExtendableTidePower => TidePower;
        public int TideGuard { get; internal set; }
        public bool TideGuardConversionActive { get; internal set; }
        internal int CurseContractTurns, CurseLinkCharges, CurseWeakTurns, CurseArmorTurns, CurseCounterTurns;
        internal bool CurseContractTriggered, CurseCounterTriggered;
        internal CoreBattleCard CurseWeakCard, CurseArmorCard, CurseCounterCard;
        internal int TideAmbushTurns, TideWeakTurns, TideBathPlayerEnds;
        internal CoreBattleCard TideAmbushCard, TideWeakCard;
        internal int BloodDebtTurns, BloodOathPlayerEnds;
        internal CoreBattleCard BloodDebtCard, BloodOathCard;
    }

    public sealed partial class CoreVoyageSession
    {
        private bool BCurseExecutionEnded => BState.PlayerExecutedByCurse || BState.EnemyExecutedByCurse;
        // Execution is a state rule, not damage: it neither spends guard nor
        // grants damage-triggered resources, retaliation or life drain.
        private bool BResolveCurseExecutions()
        {
            if (!BActive) return false;
            if (CoreCurseRules.ReachesExecutionThreshold(Battle.BoardingEnemy.CurseMarks, Battle.PlayerBoardingHealth))
            {
                int executedHealth = Battle.PlayerBoardingHealth;
                Battle.PlayerBoardingHealth = 0;
                Battle.RecordSettlement(CoreBattleSettlementKind.CurseExecution, true, executedHealth, healthAfter: 0);
                BState.PlayerExecutedByCurse = true;
                AddLog("我方咒印达到剩余生命的" + CoreCurseRules.DefaultExecutionThresholdPercent + "%，立即被消灭。");
            }
            if (CoreCurseRules.ReachesExecutionThreshold(BState.CurseMarks, Battle.EnemyBoardingHealth,
                Battle.Schools.EnemyCurseExecutionThresholdPercent))
            {
                BState.EnemyHealthRemovedByCurse = SchoolSum(BState.EnemyHealthRemovedByCurse, Battle.EnemyBoardingHealth);
                int executedHealth = Battle.EnemyBoardingHealth;
                Battle.EnemyBoardingHealth = 0;
                Battle.RecordSettlement(CoreBattleSettlementKind.CurseExecution, false, executedHealth, healthAfter: 0);
                BState.EnemyExecutedByCurse = true;
                AddLog("敌方咒印达到剩余生命的" + Battle.Schools.EnemyCurseExecutionThresholdPercent + "%，立即被消灭。");
            }
            return Battle.PlayerBoardingHealth <= 0 || Battle.EnemyBoardingHealth <= 0;
        }

        private bool CTBDesperate => Battle != null &&
            CoreBloodRules.IsDesperate(Battle.PlayerBoardingHealth, Battle.PlayerMaxBoardingHealth);

        private string CTBUnavailableReason(CoreBattleCard card, int amount)
        {
            string key = card?.BoardingSpec?.Key;
            switch (key)
            {
                case "curse_11": if (BState.CurseMarks < 2) return "至少需要2层咒印"; break;
                case "curse_12":
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    break;
                case "curse_13": if (BState.CurseMarks < 2) return "需要2层咒印"; break;
                case "curse_14":
                    if (BState.CurseMarks < 4) return "需要4层咒印";
                    break;
                case "curse_15":
                    if (BState.CurseMarks < 4) return "需要4层咒印";
                    break;
                case "tide_09":
                    if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要8点潮势";
                    break;
                case "tide_10": if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要4点潮势"; break;
                case "tide_11":
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    break;
                case "tide_13": if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要16点潮势"; break;
                case "tide_14":
                    if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要16点潮势";
                    break;
                case "tide_15": if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要4点潮势"; break;
                case "blood_01":
                case "blood_03":
                case "blood_04":
                case "blood_05":
                    if (SchoolPlayerHealth <= CoreBoardingCardBalance.Cost(key)) return "支付6生命后须至少剩1生命";
                    break;
                case "blood_02":
                    if (SchoolPlayerHealth <= CoreBoardingCardBalance.Cost(key)) return "支付8生命后须至少剩1生命";
                    break;
                case "blood_09": if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满"; break;
                case "blood_14":
                    if (!CTBDesperate) return "仅生命不高于40%时可使用";
                    break;
                case "blood_15":
                    if (!CTBDesperate) return "仅生命不高于40%时可使用";
                    break;
            }
            return string.Empty;
        }

        // Life is a paid cost, so the common pipeline invokes this before checking
        // desperate-condition attack bonuses. It never dispatches damage reactions.
        private void CTBPayLifeCost(CoreBattleCard card, int amount)
        {
            string key = card?.BoardingSpec?.Key;
            if (key == "blood_02") BPayLifeCost(CoreBoardingCardBalance.Cost(key));
            else if (key == "blood_01" || key == "blood_03" || key == "blood_04" || key == "blood_05")
                BPayLifeCost(CoreBoardingCardBalance.Cost(key));
        }

        private bool CTBExecute(CoreBattleCard card, int amount)
        {
            string key = card.BoardingSpec.Key;
            int paid, actual;
            switch (key)
            {
                case "curse_01": BDeal(BLinear(card, 8)); CTBAddMarks(CoreBoardingCardBalance.Marks(key, card.UpgradeLevel)); break;
                case "curse_02": CTBAddMarks(CoreBoardingCardBalance.Marks(key, card.UpgradeLevel)); break;
                case "curse_03": BGuard(BGuardValue(card, 3)); CTBAddMarks(CoreBoardingCardBalance.Marks(key, card.UpgradeLevel)); break;
                case "curse_04":
                    BState.CurseContracts.Add(new CoreBoardingEffectWindow(card, 2));
                    BState.CurseContractTurns = 2; BState.CurseContractTriggered = false; break;
                case "curse_05": BState.CurseLinkCharges = SchoolSum(BState.CurseLinkCharges, 2); break;
                case "curse_06": BDeal(BFormula(card, 12, 1, BState.CurseMarks)); break;
                case "curse_07":
                    BState.CurseWeaknesses.Add(new CoreBoardingEffectWindow(card, 2));
                    BState.CurseWeakTurns = 2; BState.CurseWeakCard = card; break;
                case "curse_08":
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BFormula(card, 8, 2, BState.CurseMarks));
                    BState.CurseArmorTurns = 2; BState.CurseArmorCard = card;
                    BState.CurseArmors.Add(new CoreBoardingEffectWindow(card, 2));
                    break;
                case "curse_09":
                    BState.CurseCounterTurns = 2; BState.CurseCounterCard = card; BState.CurseCounterTriggered = false;
                    BState.CurseCounters.Add(new CoreBoardingEffectWindow(card, 2));
                    break;
                case "curse_10":
                    Battle.Schools.EnemyCurseExecutionThresholdPercent = (int)Math.Max(int.MinValue,
                        (long)Battle.Schools.EnemyCurseExecutionThresholdPercent - BValue(card, CoreCurseRules.EntangleThresholdReductionPercent));
                    BResolveCurseExecutions();
                    break;
                case "curse_11":
                    paid = BState.CurseMarks; CTBConsumeMarks(paid);
                    BDeal(BBoardingFieldAttackDamage(BFormula(card, 40, 8, paid)), ignoreBlock: true, critical: false);
                    break;
                case "curse_12":
                    if (Cargo.Medicine > 0 && BState.CurseMarks >= 4)
                    { Cargo.Medicine--; BHeal(BLinear(card, 13)); }
                    else BHeal(2);
                    break;
                case "curse_13": CTBConsumeMarks(CoreBoardingCardBalance.Cost(key)); BDraw(card.Draw); break;
                case "curse_14": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key); break;
                case "curse_15": BGuard(BGuardValue(card, 10)); break;
                case "tide_01": CTBAddTideGuard(BGuardValue(card, 4)); break;
                case "tide_02": CTBAddTideGuard(BGuardValue(card, 15)); break;
                case "tide_03": CTBAddTideGuard(BGuardValue(card, 2)); BDraw(card.Draw); break;
                case "tide_04":
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BLinear(card, 4)); BDeal(BLinear(card, 6));
                    break;
                case "tide_05":
                    BState.TideGuardConversionActive = true;
                    BState.TideGuard = Math.Max(0, Battle.PlayerBlock);
                    break;
                case "tide_06":
                    paid = BState.TidePower; CTBConsumeTide(paid); BDeal(BFormula(card, 12, 1, paid)); break;
                case "tide_07":
                    paid = BState.TidePower; CTBConsumeTide(paid); BDeal(BFormula(card, 32, 1, paid)); break;
                case "tide_08":
                    paid = BState.TidePower; CTBConsumeTide(paid); BDeal(BFormula(card, 60, 1.5m, paid));
                    break;
                case "tide_09":
                    CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); BState.TideAmbushTurns = 2; BState.TideAmbushCard = card;
                    BState.TideAmbushes.Add(new CoreBoardingEffectWindow(card, 2)); break;
                case "tide_10":
                    CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); BDeal(BLinear(card, 6));
                    if (BCurseExecutionEnded) break;
                    BState.TideWeakTurns = 2; BState.TideWeakCard = card;
                    BState.TideWeaknesses.Add(new CoreBoardingEffectWindow(card, 2));
                    break;
                case "tide_11":
                    if (Cargo.Medicine > 0 && BState.TidePower >= CoreBoardingCardBalance.Cost(key))
                    { Cargo.Medicine--; CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); BHeal(BLinear(card, 10)); }
                    else BHeal(2);
                    break;
                case "tide_12":
                    paid = 2;
                    if (Cargo.Medicine > 0 && BState.TidePower >= CoreBoardingCardBalance.Cost(key))
                    { Cargo.Medicine--; CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); paid = CoreBoardingCardBalance.Sustain(key, card.UpgradeLevel); }
                    BQueueHeal(paid, 2); BState.TideBathPlayerEnds = 2;
                    break;
                case "tide_13": CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); BDraw(card.Draw); break;
                case "tide_14":
                    CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BDraw(card.Draw); BMarkOnce(key);
                    break;
                case "tide_15": CTBConsumeTide(CoreBoardingCardBalance.Cost(key)); BGuard(BGuardValue(card, 8)); break;
                case "blood_01": BDeal(BLinear(card, 16)); break;
                case "blood_02": BDeal(BLinear(card, 34)); break;
                case "blood_03": BGuard(BGuardValue(card, 11)); break;
                case "blood_04": BDraw(card.Draw); break;
                case "blood_05": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); break;
                case "blood_06":
                    actual = BDeal(BLinear(card, 9));
                    BRestoreHealth(actual, afterEnemyExecution: true);
                    break;
                case "blood_07":
                    actual = BDeal(BLinear(card, 12));
                    if (SchoolEnemyHealth > 0) BDeal(BLinear(card, actual > 0 ? 16 : 12));
                    break;
                case "blood_08":
                    BState.BloodDebtTurns = 2; BState.BloodDebtCard = card;
                    break;
                case "blood_09":
                    bool doctor = card.Id == "card_special_crew_broken_anchor_doctor_hector";
                    paid = CTBDesperate ? (doctor ? 16 : 13) : (doctor ? 7 : 6);
                    if (Cargo.Medicine > 0) { Cargo.Medicine--; BHeal(BLinear(card, paid)); }
                    else BHeal(CTBDesperate ? (doctor ? 4 : 5) : 2);
                    break;
                case "blood_10": if (BDeal(BLinear(card, 6)) > 0) BDraw(card.Draw); break;
                case "blood_11": BDeal(BLinear(card, CTBDesperate ? 17 : 7)); break;
                case "blood_12": BGuard(BGuardValue(card, CTBDesperate ? 12 : 5)); break;
                case "blood_13": BQueueDamage(BLinear(card, CTBDesperate ? 22 : 12)); break;
                case "blood_14": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BDraw(card.Draw); break;
                case "blood_15":
                    BState.BloodOathPlayerEnds = 2; BState.BloodOathCard = card;
                    break;
                default: return false;
            }
            CTBSyncResources();
            return true;
        }

        private void CTBAddMarks(int amount)
        {
            if (BResolveCurseExecutions()) return;
            for (int index = 0; index < amount; index++)
                BState.CurseLayers.Add(new CoreBoardingCurseLayer());
            CTBSyncResources();
            BResolveCurseExecutions();
        }

        private void CTBConsumeMarks(int amount)
        {
            // Find the earliest expiration each time; equal deadlines retain their insertion order.
            for (int used = 0; used < amount && BState.CurseLayers.Count > 0; used++)
            {
                int earliest = 0;
                for (int index = 1; index < BState.CurseLayers.Count; index++)
                    if (BState.CurseLayers[index].EnemyEnds < BState.CurseLayers[earliest].EnemyEnds) earliest = index;
                BState.CurseLayers.RemoveAt(earliest);
            }
            CTBSyncResources();
        }

        private void CTBConsumeTide(int amount)
        {
            int fromPoints = Math.Min(Math.Max(0, amount), BState.TidePoints.Count);
            BState.TidePoints.RemoveRange(0, fromPoints);
            BState.TideOverflow = Math.Max(0, BState.TideOverflow - Math.Max(0, amount - fromPoints));
            CTBSyncResources();
        }

        private void CTBGainTide(int amount)
        {
            // New reserves are aggregated; the legacy point list is only kept for
            // existing states. Large absorbed hits must not allocate one object per point.
            int gained = Math.Min(Math.Max(0, amount), int.MaxValue - BState.TidePower);
            BState.TideOverflow = SchoolSum(BState.TideOverflow, gained);
            CTBSyncResources();
        }

        private void BRecordBlockedDamage(int absorbed)
        {
            absorbed = Math.Max(0, absorbed);
            int before = SchoolSum(Battle.PlayerBlock, absorbed);
            int tidal = Math.Min(before, BState.TideGuard);
            int spent = Math.Min(tidal, Math.Max(0, absorbed - (before - tidal)));
            BConsumeRetainedGuard(absorbed);
            BState.TideGuard = Math.Min(Battle.PlayerBlock, tidal - spent);
            CTBGainTide(spent);
        }

        private void CTBAddTideGuard(int amount)
        {
            int before = Battle.PlayerBlock;
            BGuard(amount);
            if (!BState.TideGuardConversionActive)
                BState.TideGuard = SchoolSum(BState.TideGuard, Battle.PlayerBlock - before);
            CTBSyncResources();
        }

        private void CTBSyncResources()
        {
            CoreSchoolBattleState legacy = Battle.Schools;
            legacy.CurseMarks = BState.CurseMarks;
            legacy.CurseTurns = BState.CurseMarks;
            legacy.CurseDamagePerMark = 0;
            legacy.TidePower = BState.TidePower;
            legacy.TidePaidBlock = BState.TideGuard;
        }

        // Return the resulting total attack, not the reduction. Preview calls cannot consume effects.
        private int CTBEnemyAttackReduction(int damage, bool consume)
        {
            int reduction = BContinuousAttackReduction(BState.CurseMarks);
            return Math.Max(0, damage - reduction);
        }

        private int CTBEnemyBlockGain(int guard, bool consume)
        {
            if (guard <= 0 || BState.CurseArmorTurns <= 0) return Math.Max(0, guard);
            int adjusted = Math.Max(0, guard - BCurseArmorAmount(BState.CurseMarks));
            return adjusted;
        }

        private void CTBBeforeEnemyAttack()
        {
            if (BState.CurseCounterTurns > 0)
                BDeal(BCurseCounterAmount(BState.CurseMarks), ignoreBlock: true, critical: false);
        }

        private void CTBAfterEnemyAttack(int absorbed, int actualLoss)
        {
            BRecordBlockedDamage(absorbed);
            if (SchoolPlayerHealth <= 0 || SchoolEnemyHealth <= 0) return;
            if (actualLoss > 0 && BState.CurseContractTurns > 0) CTBAddMarks(BCurseContractAmount);
            if (SchoolEnemyHealth <= 0) { CTBSyncResources(); return; }
            if (BState.TideAmbushTurns > 0)
            { int ambush = BTideAmbushAmount; BState.TideAmbushes.Clear(); BState.TideAmbushTurns = 0; BDeal(ambush, critical: false); }
            if (SchoolEnemyHealth <= 0) { CTBSyncResources(); return; }
            if ((actualLoss > 0 || absorbed > 0) && BState.BloodDebtTurns > 0)
                BDeal(BLinear(BState.BloodDebtCard, 8), critical: false);
            CTBSyncResources();
        }

        private void CTBPlayerEnd()
        {
            BState.CurseLinkCharges = 0;
            if (BState.TideBathPlayerEnds > 0) BState.TideBathPlayerEnds--;
            if (BState.BloodOathPlayerEnds > 0) BState.BloodOathPlayerEnds--;
            CTBSyncResources();
        }

        private void CTBEnemyEnd()
        {
            BAdvanceManuscriptEnemyWindows();
            if (BState.BloodDebtTurns > 0) BState.BloodDebtTurns--;
            CTBSyncResources();
        }

        private void CTBPlayerStart()
        {
            BState.CurseContractTriggered = false;
            BState.CurseCounterTriggered = false;
            BState.TideGuard = BState.TideGuardConversionActive ? Math.Max(0, Battle.PlayerBlock) : 0;
            CTBSyncResources();
        }

        private int CTBBeforeDirectAttack()
        {
            if (BState.BloodOathPlayerEnds <= 0) return 0;
            return BLinear(BState.BloodOathCard, 8);
        }

        private void CTBAfterActiveAttack()
        {
            if (BState.CurseLinkCharges <= 0) return;
            BState.CurseLinkCharges--;
            CTBAddMarks(2);
        }
    }
}
