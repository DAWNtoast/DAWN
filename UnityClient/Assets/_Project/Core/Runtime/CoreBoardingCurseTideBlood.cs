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
        public int TidePower => TidePoints.Count;
        public int ExtendableTidePower => TidePoints.FindAll(point => !point.Extended).Count;
        public int TideGuard { get; internal set; }
        internal int CurseContractTurns, CurseLinkCharges, CurseWeakTurns, CurseArmorTurns, CurseCounterTurns;
        internal bool CurseContractTriggered, CurseCounterTriggered;
        internal CoreBattleCard CurseWeakCard, CurseArmorCard, CurseCounterCard;
        internal int TideAmbushTurns, TideWeakTurns, TideBathPlayerEnds;
        internal CoreBattleCard TideAmbushCard, TideWeakCard;
        internal int BloodDebtTurns, BloodOathPlayerEnds;
        internal bool BloodDebtTriggered, BloodOathTriggered;
        internal CoreBattleCard BloodDebtCard, BloodOathCard;
    }

    public sealed partial class CoreVoyageSession
    {
        private bool CTBDesperate => Battle != null &&
            (long)Battle.PlayerBoardingHealth * 100 <= (long)Battle.PlayerMaxBoardingHealth * 40;

        private string CTBUnavailableReason(CoreBattleCard card, int amount)
        {
            string key = card?.BoardingSpec?.Key;
            switch (key)
            {
                case "curse_10":
                    if (BState.CurseMarks <= 0) return "没有可以结算的咒印";
                    break;
                case "curse_11": if (BState.CurseMarks < 1) return "至少需要1层咒痕"; break;
                case "curse_12":
                    if (Cargo.Medicine > 0 && BState.CurseMarks < 2) return "有药治疗需要2层咒痕";
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    break;
                case "curse_13": if (BState.CurseMarks < 1) return "需要1层咒痕"; break;
                case "curse_14":
                    if (BState.CurseMarks < 2) return "需要2层咒痕";
                    break;
                case "curse_15":
                    if (BState.CurseMarks < 2) return "需要2层咒痕";
                    break;
                case "tide_09":
                    if (BState.TidePower < 4) return "需要4点潮势";
                    break;
                case "tide_10": if (BState.TidePower < 2) return "需要2点潮势"; break;
                case "tide_11":
                    if (Cargo.Medicine > 0 && BState.TidePower < 6) return "有药治疗需要6点潮势";
                    if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
                    break;
                case "tide_12":
                    if (Cargo.Medicine > 0 && BState.TidePower < 8) return "有药药浴需要8点潮势";
                    break;
                case "tide_13": if (BState.TidePower < 8) return "需要8点潮势"; break;
                case "tide_14":
                    if (BState.TidePower < 8) return "需要8点潮势";
                    break;
                case "tide_15": if (BState.TidePower < 6) return "需要6点潮势"; break;
                case "blood_01":
                case "blood_03":
                case "blood_04":
                case "blood_05":
                    if (SchoolPlayerHealth <= 3) return "支付3生命后须至少剩1生命";
                    if (key == "blood_05" && BOnceUsed(key)) return "血祭汲能每回合只能使用一次";
                    break;
                case "blood_02":
                    if (SchoolPlayerHealth <= 4) return "支付4生命后须至少剩1生命";
                    break;
                case "blood_08": if (BState.BloodDebtTurns > 0) return "血债追偿仍在生效"; break;
                case "blood_09": if (SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满"; break;
                case "blood_14":
                    if (!CTBDesperate) return "仅生命不高于40%时可使用";
                    if (BOnceUsed(key, true)) return "逆命一搏每场战斗只能使用一次";
                    break;
                case "blood_15": if (BState.BloodOathPlayerEnds > 0) return "绝境誓约仍在生效"; break;
            }
            return string.Empty;
        }

        // Life is a paid cost, so the common pipeline invokes this before checking
        // desperate-condition attack bonuses. It never dispatches damage reactions.
        private void CTBPayLifeCost(CoreBattleCard card, int amount)
        {
            string key = card?.BoardingSpec?.Key;
            if (key == "blood_02") Battle.PlayerBoardingHealth -= 4;
            else if (key == "blood_01" || key == "blood_03" || key == "blood_04" || key == "blood_05")
                Battle.PlayerBoardingHealth -= 3;
        }

        private bool CTBExecute(CoreBattleCard card, int amount)
        {
            string key = card.BoardingSpec.Key;
            int paid, actual;
            switch (key)
            {
                case "curse_01": BDeal(BValue(card, 8)); CTBAddMarks(1); break;
                case "curse_02": CTBAddMarks(3); break;
                case "curse_03": BGuard(BValue(card, 3)); CTBAddMarks(1); break;
                case "curse_04":
                    BState.CurseContracts.Add(new CoreBoardingEffectWindow(card, 2));
                    BState.CurseContractTurns = 2; BState.CurseContractTriggered = false; break;
                case "curse_05": BState.CurseLinkCharges = SchoolSum(BState.CurseLinkCharges, 2); break;
                case "curse_06": BDeal(BValue(card, 6 + BState.CurseMarks)); break;
                case "curse_07":
                    BState.CurseWeaknesses.Add(new CoreBoardingEffectWindow(card, 2));
                    BState.CurseWeakTurns = 2; BState.CurseWeakCard = card; break;
                case "curse_08":
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BValue(card, 4 + 2 * BState.CurseMarks));
                    BState.CurseArmorTurns = 2; BState.CurseArmorCard = card;
                    BState.CurseArmors.Add(new CoreBoardingEffectWindow(card, 2));
                    break;
                case "curse_09":
                    BState.CurseCounterTurns = 2; BState.CurseCounterCard = card; BState.CurseCounterTriggered = false;
                    BState.CurseCounters.Add(new CoreBoardingEffectWindow(card, 2));
                    break;
                case "curse_10":
                    CTBTickMarks(false);
                    break;
                case "curse_11":
                    paid = BState.CurseMarks; CTBConsumeMarks(paid);
                    BDeal(BValue(card, 20 + 8 * paid), ignoreBlock: true, critical: false);
                    break;
                case "curse_12":
                    if (Cargo.Medicine > 0)
                    { Cargo.Medicine--; CTBConsumeMarks(2); BHeal(BValue(card, 13)); }
                    else BHeal(2);
                    break;
                case "curse_13": CTBConsumeMarks(1); BDraw(card.Draw); break;
                case "curse_14": CTBConsumeMarks(2); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key); break;
                case "curse_15": CTBConsumeMarks(2); BGuard(BValue(card, 10)); break;
                case "tide_01": CTBAddTideGuard(BValue(card, 4)); break;
                case "tide_02": CTBAddTideGuard(BValue(card, 10)); break;
                case "tide_03": CTBAddTideGuard(BValue(card, 2)); BDraw(card.Draw); break;
                case "tide_04":
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - BValue(card, 4)); BDeal(BValue(card, 6));
                    break;
                case "tide_05":
                    for (int index = 0; index < BValue(card, 6) && BState.TidePower < 12; index++)
                        BState.TidePoints.Add(new CoreBoardingTidePoint());
                    break;
                case "tide_06":
                    paid = BState.TidePower; CTBConsumeTide(paid); BDeal(BValue(card, 6 + paid)); break;
                case "tide_07":
                    paid = BState.TidePower; CTBConsumeTide(paid); BDeal(BValue(card, 16 + paid)); break;
                case "tide_08":
                    paid = BState.TidePower; CTBConsumeTide(paid); BDeal(BValue(card, 30 + paid * 3 / 2));
                    break;
                case "tide_09":
                    CTBConsumeTide(4); BState.TideAmbushTurns = 2; BState.TideAmbushCard = card;
                    BState.TideAmbushes.Add(new CoreBoardingEffectWindow(card, 2)); break;
                case "tide_10":
                    CTBConsumeTide(2); BDeal(BValue(card, 6)); BState.TideWeakTurns = 2; BState.TideWeakCard = card;
                    BState.TideWeaknesses.Add(new CoreBoardingEffectWindow(card, 2));
                    break;
                case "tide_11":
                    if (Cargo.Medicine > 0)
                    { Cargo.Medicine--; CTBConsumeTide(6); BHeal(BValue(card, 10)); }
                    else BHeal(2);
                    break;
                case "tide_12":
                    paid = 2;
                    if (Cargo.Medicine > 0)
                    { Cargo.Medicine--; CTBConsumeTide(8); paid = BValue(card, 8); }
                    BQueueHeal(paid, 2); BState.TideBathPlayerEnds = 2;
                    break;
                case "tide_13": CTBConsumeTide(8); BDraw(card.Draw); break;
                case "tide_14":
                    CTBConsumeTide(8); Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BDraw(card.Draw); BMarkOnce(key);
                    break;
                case "tide_15": CTBConsumeTide(6); BGuard(BValue(card, 8)); break;
                case "blood_01": BDeal(BValue(card, 16)); break;
                case "blood_02": BDeal(BValue(card, 34)); break;
                case "blood_03": BGuard(BValue(card, 11)); break;
                case "blood_04": BDraw(card.Draw); break;
                case "blood_05": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BMarkOnce(key); break;
                case "blood_06":
                    actual = BDeal(BValue(card, 9));
                    if (!BOnceUsed(key) && actual > 0 && BHeal(Math.Min(actual, BValue(card, 2))) > 0) BMarkOnce(key);
                    break;
                case "blood_07":
                    actual = BDeal(BValue(card, 12));
                    if (SchoolEnemyHealth > 0) BDeal(BValue(card, actual > 0 ? 16 : 12));
                    break;
                case "blood_08":
                    BState.BloodDebtTurns = 2; BState.BloodDebtCard = card; BState.BloodDebtTriggered = false;
                    break;
                case "blood_09":
                    paid = CTBDesperate ? 9 : 7;
                    if (Cargo.Medicine > 0) { Cargo.Medicine--; BHeal(BValue(card, paid)); }
                    else BHeal(CTBDesperate ? 3 : 2);
                    break;
                case "blood_10": if (BDeal(BValue(card, 6)) > 0) BDraw(card.Draw); break;
                case "blood_11": BDeal(BValue(card, CTBDesperate ? 14 : 10)); break;
                case "blood_12": BGuard(BValue(card, CTBDesperate ? 8 : 5)); break;
                case "blood_13": BQueueDamage(BValue(card, CTBDesperate ? 18 : 12)); break;
                case "blood_14": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); BDraw(card.Draw); BMarkOnce(key, true); break;
                case "blood_15":
                    BState.BloodOathPlayerEnds = 2; BState.BloodOathCard = card; BState.BloodOathTriggered = false;
                    break;
                default: return false;
            }
            CTBSyncResources();
            return true;
        }

        private void CTBAddMarks(int amount)
        {
            if (SchoolEnemyHealth <= 0) return;
            for (int index = 0; index < amount; index++)
                BState.CurseLayers.Add(new CoreBoardingCurseLayer());
            CTBSyncResources();
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
            for (int used = 0; used < amount && BState.TidePoints.Count > 0; used++)
            {
                int earliest = 0;
                for (int index = 1; index < BState.TidePoints.Count; index++)
                    if (BState.TidePoints[index].PlayerEnds < BState.TidePoints[earliest].PlayerEnds) earliest = index;
                BState.TidePoints.RemoveAt(earliest);
            }
            CTBSyncResources();
        }

        private void CTBAddTideGuard(int amount)
        {
            int before = Battle.PlayerBlock;
            BGuard(amount);
            BState.TideGuard = SchoolSum(BState.TideGuard, Battle.PlayerBlock - before);
            CTBSyncResources();
        }

        private void CTBSyncResources()
        {
            CoreSchoolBattleState legacy = Battle.Schools;
            legacy.CurseMarks = BState.CurseMarks;
            legacy.CurseTurns = BState.CurseMarks;
            legacy.CurseDamagePerMark = 1;
            legacy.TidePower = BState.TidePower;
            legacy.TideLimit = 12;
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

        private void CTBTickMarks(bool decay = true)
        {
            int layers = BState.CurseMarks;
            if (layers <= 0) return;
            BDeal(SchoolSum(layers, Battle.Schools.CurseNextTickBonus), ignoreBlock: true, critical: false);
            Battle.Schools.CurseNextTickBonus = 0;
            if (decay) CTBConsumeMarks(1);
        }

        private void CTBAfterEnemyAttack(int absorbed, int actualLoss)
        {
            if (SchoolPlayerHealth <= 0 || SchoolEnemyHealth <= 0) return;
            int beforeBlock = SchoolSum(Battle.PlayerBlock, absorbed);
            int tidalBefore = Math.Min(beforeBlock, BState.TideGuard);
            int ordinaryBefore = beforeBlock - tidalBefore;
            int tidalAbsorbed = Math.Min(tidalBefore, Math.Max(0, absorbed - ordinaryBefore));
            BState.TideGuard = Math.Max(0, BState.TideGuard - tidalAbsorbed);
            for (int index = 0; index < tidalAbsorbed && BState.TidePower < 12; index++)
                BState.TidePoints.Add(new CoreBoardingTidePoint());
            if (actualLoss > 0 && BState.CurseContractTurns > 0) CTBAddMarks(BCurseContractAmount);
            if (SchoolEnemyHealth <= 0) { CTBSyncResources(); return; }
            if (BState.TideAmbushTurns > 0)
            { int ambush = BTideAmbushAmount; BState.TideAmbushes.Clear(); BState.TideAmbushTurns = 0; BDeal(ambush, critical: false); }
            if (SchoolEnemyHealth <= 0) { CTBSyncResources(); return; }
            if (actualLoss > 0 && BState.BloodDebtTurns > 0 && !BState.BloodDebtTriggered)
            { BState.BloodDebtTriggered = true; BDeal(BValue(BState.BloodDebtCard, 8), critical: false); }
            CTBSyncResources();
        }

        private void CTBPlayerEnd()
        {
            BState.CurseLinkCharges = 0;
            foreach (CoreBoardingTidePoint point in BState.TidePoints) point.PlayerEnds--;
            BState.TidePoints.RemoveAll(point => point.PlayerEnds <= 0);
            if (BState.TideBathPlayerEnds > 0) BState.TideBathPlayerEnds--;
            if (BState.BloodOathPlayerEnds > 0) BState.BloodOathPlayerEnds--;
            CTBSyncResources();
        }

        private void CTBEnemyEnd()
        {
            CTBTickMarks();
            BAdvanceManuscriptEnemyWindows();
            if (BState.BloodDebtTurns > 0) BState.BloodDebtTurns--;
            CTBSyncResources();
        }

        private void CTBPlayerStart()
        {
            BState.CurseContractTriggered = false;
            BState.CurseCounterTriggered = false;
            BState.BloodDebtTriggered = false;
            BState.BloodOathTriggered = false;
            BState.TideGuard = 0;
            CTBSyncResources();
        }

        private int CTBBeforeDirectAttack()
        {
            if (BState.BloodOathPlayerEnds <= 0 || BState.BloodOathTriggered || !CTBDesperate) return 0;
            BState.BloodOathTriggered = true;
            return BValue(BState.BloodOathCard, 8);
        }

        private void CTBAfterActiveAttack()
        {
            if (BState.CurseLinkCharges <= 0) return;
            BState.CurseLinkCharges--;
            CTBAddMarks(1);
        }
    }
}
