using System;
using System.Collections.Generic;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private CoreBoardingBattleState BState => Battle.Boarding;
        private bool BActive => Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == NavalBattleStage.Boarding;
        private static string BIdentity(CoreBattleCard card) => card?.BoardingSpec?.Key ?? card?.Id ?? string.Empty;
        private int BValue(CoreBattleCard card, int value) => CoreCardUpgradeDesign.ScaleEffect(card, value, card?.UpgradeLevel ?? 0);
        private bool BOnceUsed(string key, bool battle = false) => (battle ? BState.BattleUses : BState.TurnUses).Contains(key);
        private void BMarkOnce(string key, bool battle = false) => (battle ? BState.BattleUses : BState.TurnUses).Add(key);
        private static int BStackTotal(List<CoreBoardingTimedStacks> stacks)
        { int total = 0; foreach (var stack in stacks) total = SchoolSum(total, stack.Amount); return total; }

        private void BInitialize()
        {
            if (!BActive || BState.Initialized) return;
            BState.Initialized = true;
            Battle.Schools.TideLimit = 12;
            Battle.Schools.ManaLimit = 3;
            Battle.Schools.Mana = Math.Min(3, Battle.Schools.Mana);
            Battle.Schools.TemporaryMana = Math.Min(Math.Max(0, 3 - Battle.Schools.Mana), Battle.Schools.TemporaryMana);
            Battle.Schools.CurseDamagePerMark = 1;
            Battle.Schools.CurseNextTickBonus = 0;
            if (Battle.EnemyBurnDamage > 0 && Battle.EnemyBurnTurns > 0)
                BState.EnemyBurn.Add(new CoreBoardingTimedStacks(Battle.EnemyBurnDamage, Battle.EnemyBurnDamage));
            BSyncBurn();
            BSyncHeldCards();
        }

        private void BSyncHeldCards()
        {
            if (BState.HeldCards.Count > Battle.Hand.Count) BState.HeldCards.Clear();
            for (int i = 0; i < Battle.Hand.Count; i++)
            {
                if (i == BState.HeldCards.Count) BState.HeldCards.Add(new CoreBoardingHeldCard { CardIndex = Battle.Hand[i] });
                else if (BState.HeldCards[i].CardIndex != Battle.Hand[i])
                    BState.HeldCards[i] = new CoreBoardingHeldCard { CardIndex = Battle.Hand[i] };
            }
        }

        private void BRemoveHand(int slot)
        { Battle.Hand.RemoveAt(slot); BState.HeldCards.RemoveAt(slot); }

        private int BHandEnergyCost(int handIndex)
        {
            BSyncHeldCards();
            CoreBattleCard card = GetBattleCard(Battle.Hand[handIndex]);
            CoreBoardingHeldCard held = BState.HeldCards[handIndex];
            int discount = held.DiscountTurn == Battle.Turn ? held.Discount : 0;
            return SchoolSum(Math.Max(0, card.Energy - discount), Battle.SuccessfulCardUses(BIdentity(card)));
        }

        public int GetBoardingHandEnergyCost(int handIndex) => BActive && handIndex >= 0 && handIndex < Battle.Hand.Count
            ? BHandEnergyCost(handIndex) : 0;

        public bool PlayCard(int handIndex, CoreBoardingPlayOptions options)
        {
            if (!BActive || handIndex < 0 || handIndex >= Battle.Hand.Count || GetBattleCard(Battle.Hand[handIndex])?.BoardingSpec == null)
                return PlayCard(handIndex);
            return BPlayCard(handIndex, options);
        }

        private bool BPlayCard(int handIndex, CoreBoardingPlayOptions suppliedOptions)
        {
            BInitialize();
            CoreBoardingPlayOptions options = suppliedOptions ?? BDefaultOptions(handIndex);
            if (GetBoardingChoiceUnavailableReason(handIndex, options).Length > 0) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
            string key = card.BoardingSpec.Key;
            options.ResourceAmount = BFixedAmount(card, options);
            int amount = options.ResourceAmount;
            int cost = BHandEnergyCost(handIndex);
            int previousEnemy = Battle.EnemyBoardingHealth, previousHealth = Battle.PlayerBoardingHealth;
            int previousGuard = Battle.PlayerBlock, previousEnergy = Battle.Energy;
            int previousMedicine = Cargo.Medicine, previousPowder = Cargo.Powder;
            int echoes = card.SchoolId == "arcane" && card.BoardingSpec.IsAttack && BState.ArcaneEchoReady
                ? Math.Max(1, BState.ArcaneEchoCharges) : 0;
            if (echoes > 0) { BState.ArcaneEchoReady = false; BState.ArcaneEchoCharges = 0; }
            int residual = card.SchoolId == "thunder" && card.BoardingSpec.IsAttack && BState.ThunderResidualCharges > 0
                ? BState.ThunderResidualDamage : 0;
            if (residual > 0) BState.ThunderResidualCharges--;
            BState.SelectedHeldCards.Clear();
            foreach (int slot in options.SelectionIndexes)
                if ((key == "arcane_12" || key == "fire_06" || key == "fire_07" || key == "fire_08" || key == "fire_09") && slot >= 0 && slot < BState.HeldCards.Count)
                    BState.SelectedHeldCards.Add(BState.HeldCards[slot]);
            if (key == "arcane_09") BPrepareReading(cardIndex);
            BRemoveHand(handIndex);
            Battle.Energy -= cost;
            Battle.RecordSuccessfulCardUse(BIdentity(card));
            Battle.CardsPlayedThisTurn = SchoolSum(Battle.CardsPlayedThisTurn, 1);
            bool exhausted = key == "fire_10" || key == "blood_14";
            if (exhausted) BState.ExhaustedCards.Add(cardIndex); else Battle.DiscardPile.Add(cardIndex);
            BState.BurnedAttackCount = 0;
            if (key == "fire_06" || key == "fire_07" || key == "fire_08" || key == "fire_09")
            {
                foreach (CoreBoardingHeldCard selected in BState.SelectedHeldCards)
                {
                    int at = BState.HeldCards.IndexOf(selected);
                    if (at < 0) continue;
                    if (GetBattleCard(selected.CardIndex)?.BoardingSpec?.IsAttack == true) BState.BurnedAttackCount++;
                    BState.ExhaustedCards.Add(selected.CardIndex); BRemoveHand(at);
                }
            }
            int powder = BRequiredPowder(card);
            if (powder > 0)
            {
                if (BState.FreePowderUses > 0) BState.FreePowderUses--;
                else Cargo.Powder -= powder;
            }
            CTBPayLifeCost(card, amount);
            FATPayLifeCost(card);
            BState.ActiveCard = card;
            BState.ActiveAttack = card.BoardingSpec.IsAttack;
            BState.ActiveCritical = card.BoardingSpec.IsAttack && !card.BoardingSpec.IsDelayed && RollBoardingCritical();
            BState.ActiveAuthoredDamage = 0;
            BState.ActiveAttackBonus = card.BoardingSpec.IsAttack && !card.BoardingSpec.IsDelayed ? CTBBeforeDirectAttack() : 0;
            if (card.SchoolId == "thunder" && card.BoardingSpec.IsAttack)
            { BState.ActiveAttackBonus = SchoolSum(BState.ActiveAttackBonus, BState.ThunderReady); BState.ThunderReady = 0; }

            if (!BExecuteBasic(card) && !FATExecute(card, options)) CTBExecute(card, amount);
            if (card.BoardingSpec.IsAttack)
            {
                CTBAfterActiveAttack();
                if (card.SchoolId == "thunder" && key != "thunder_13" && key != "thunder_14" && key != "thunder_15")
                    Battle.Schools.ThunderChain = Math.Min(6, Battle.Schools.ThunderChain + 1);
                if (residual > 0 && Battle.EnemyBoardingHealth > 0)
                {
                    if (card.BoardingSpec.IsDelayed) BQueueDamage(residual, authored: false);
                    else BDeal(residual, critical: false);
                }
                if (echoes > 0 && Battle.EnemyBoardingHealth > 0)
                    for (int index = 0; index < echoes; index++) BQueueDamage(BState.ActiveAuthoredDamage / 2, authored: false);
            }
            BState.ActiveAttack = false; BState.ActiveCritical = false; BState.ActiveAttackBonus = 0; BState.ActiveCard = null;
            BState.Revision++;
            BSyncBurn();
            BSyncHeldCards();
            Battle.LastAction = "打出「" + card.Name + "」" +
                (previousEnemy > Battle.EnemyBoardingHealth ? "；伤害 " + (previousEnemy - Battle.EnemyBoardingHealth) : string.Empty) +
                (Battle.PlayerBlock > previousGuard ? "；格挡 +" + (Battle.PlayerBlock - previousGuard) : string.Empty) +
                (Battle.PlayerBoardingHealth > previousHealth ? "；恢复 " + (Battle.PlayerBoardingHealth - previousHealth) : Battle.PlayerBoardingHealth < previousHealth ? "；生命 -" + (previousHealth - Battle.PlayerBoardingHealth) : string.Empty) +
                (Battle.Energy > previousEnergy - cost ? "；能量 +" + (Battle.Energy - previousEnergy + cost) : string.Empty) +
                (Cargo.Medicine < previousMedicine ? "；药品 -" + (previousMedicine - Cargo.Medicine) : string.Empty) +
                (Cargo.Powder < previousPowder ? "；火药 -" + (previousPowder - Cargo.Powder) : string.Empty) + "。";
            AdvanceGuidedBossCard();
            ResolveBattleOutcome();
            return true;
        }

        private bool BExecuteBasic(CoreBattleCard card)
        {
            switch (card.BoardingSpec.Key)
            {
                case "basic_01": BDeal(BValue(card, 8)); break;
                case "basic_02": BDeal(BValue(card, 20)); break;
                case "basic_03": BDeal(BValue(card, 35)); break;
                case "basic_04": BGuard(BValue(card, 4)); break;
                case "basic_05": BGuard(BValue(card, 10)); break;
                case "basic_06":
                    if (Cargo.Medicine > 0)
                    { if (Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth > 2) Cargo.Medicine--; BHeal(BValue(card, 6)); }
                    else BHeal(2);
                    break;
                case "basic_07":
                    int heal = 2; if (Cargo.Medicine > 0) { Cargo.Medicine--; heal = BValue(card, 5); }
                    BQueueHeal(heal, 2); break;
                case "basic_08": BDraw(card.Draw); break;
                case "basic_09":
                case "basic_10": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); break;
                case "basic_11": BDeal(BValue(card, 4)); BDraw(card.Draw); break;
                case "basic_12": BGuard(BValue(card, 2)); BDraw(card.Draw); break;
                case "basic_13": BQueueDamage(BValue(card, 12)); break;
                default: return false;
            }
            return true;
        }

        private int BDeal(int amount, bool ignoreBlock = false, bool critical = true)
        {
            if (Battle.EnemyBoardingHealth <= 0 || Battle.PlayerBoardingHealth <= 0) return 0;
            amount = Math.Max(0, amount);
            if (critical && BState.ActiveAttack)
            {
                amount = SchoolSum(amount, BState.ActiveAttackBonus); BState.ActiveAttackBonus = 0;
                BState.ActiveAuthoredDamage = SchoolSum(BState.ActiveAuthoredDamage, amount);
                if (BState.ActiveCritical) amount = DoubleBattleDamage(amount);
            }
            int blocked = 0;
            if (!ignoreBlock)
            { blocked = Math.Min(Battle.EnemyBlock, amount); Battle.EnemyBlock -= blocked; amount -= blocked; }
            int actual = Math.Min(Battle.EnemyBoardingHealth, amount);
            Battle.EnemyBoardingHealth -= actual;
            OnBoardingEnemyDamaged(blocked, actual);
            return actual;
        }

        private int BHeal(int amount)
        { int actual = Math.Min(Math.Max(0, Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth), Math.Max(0, amount)); Battle.PlayerBoardingHealth += actual; return actual; }
        private void BGuard(int amount) => Battle.PlayerBlock = SchoolSum(Battle.PlayerBlock, amount);
        private void BDraw(int amount) { DrawCards(amount); BSyncHeldCards(); }
        private void BQueueDamage(int amount, bool ignoreBlock = false, bool authored = true)
        {
            if (authored && BState.ActiveAttack && BState.ActiveCard?.BoardingSpec?.IsDelayed == true)
            {
                amount = SchoolSum(amount, BState.ActiveAttackBonus); BState.ActiveAttackBonus = 0;
                BState.ActiveAuthoredDamage = SchoolSum(BState.ActiveAuthoredDamage, amount);
            }
            BState.DelayedDamage.Add(new CoreBoardingDelayedDamage
            {
                Amount = amount, IgnoreBlock = ignoreBlock, Label = BState.ActiveCard?.Name ?? "延迟攻击",
                SourceSchoolId = BState.ActiveCard?.SchoolId ?? string.Empty,
                SourceCardId = BState.ActiveCard?.Id ?? string.Empty,
            });
        }
        private void BQueueHeal(int amount, int ticks)
        {
            int total = SchoolSum(BState.ContinuousRecovery, Math.Max(0, amount));
            BState.HealingTicks.Clear();
            if (total > 0) BState.HealingTicks.Add(new CoreBoardingHealingTick { Amount = total, Remaining = total });
        }
        private void BSpendMana(int amount) { PayMana(amount); }
        private void BAddEnemyBurn(int amount, int remaining = 2)
        { if (amount > 0 && Battle.EnemyBoardingHealth > 0) BSetStacks(BState.EnemyBurn, SchoolSum(BStackTotal(BState.EnemyBurn), amount)); BSyncBurn(); }
        private void BAddSelfBurn(int amount)
        {
            if (amount <= 0) return;
            BSetStacks(BState.SelfBurn, SchoolSum(BStackTotal(BState.SelfBurn), amount));
            if (BState.FirePactTurns > 0) BAddEnemyBurn(amount);
        }
        private void BTransferSelfBurn(int maximum)
        {
            int total = BStackTotal(BState.SelfBurn), moved = Math.Min(maximum, total);
            BSetStacks(BState.SelfBurn, total - moved); BAddEnemyBurn(moved);
        }
        private static void BSetStacks(List<CoreBoardingTimedStacks> stacks, int amount)
        {
            stacks.Clear();
            if (amount > 0) stacks.Add(new CoreBoardingTimedStacks(amount, amount));
        }
        private void BSyncBurn()
        {
            Battle.EnemyBurnDamage = BStackTotal(BState.EnemyBurn);
            Battle.EnemyBurnTurns = Battle.EnemyBurnDamage;
        }

        private bool BPlayerEnd()
        {
            BInitialize();
            int recovery = BState.ContinuousRecovery;
            BHeal(recovery); BState.HealingTicks.Clear();
            if (recovery > 1) BState.HealingTicks.Add(new CoreBoardingHealingTick { Amount = recovery - 1, Remaining = recovery - 1 });
            int burning = BStackTotal(BState.SelfBurn);
            if (burning > 0) Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - burning);
            BSetStacks(BState.SelfBurn, burning - 1);
            TickBoardingEnemyFire();
            CTBPlayerEnd();
            Battle.Schools.TemporaryMana = 0;
            BState.FreePowderUses = 0;
            BState.ThunderReady = 0; BState.ThunderResidualCharges = 0;
            Battle.Schools.ThunderChain = 0;
            if (BState.FirePactTurns > 0) BState.FirePactTurns--;
            BState.Revision++;
            return ResolveBattleOutcome();
        }

        private bool BEnemyEnd()
        {
            if (BEnemySchoolEnd()) return true;
            BDeal(BStackTotal(BState.EnemyBurn), ignoreBlock: true, critical: false);
            BSetStacks(BState.EnemyBurn, BStackTotal(BState.EnemyBurn) - 1);
            CTBEnemyEnd();
            if (BState.ThunderWeaknessTurns > 0) BState.ThunderWeaknessTurns--;
            if (BState.ThunderWeaknessTurns <= 0) BState.ThunderWeakness = 0;
            BState.FireRetaliations = 0;
            BSyncBurn();
            BState.Revision++;
            return ResolveBattleOutcome();
        }

        private void BDiscardHandAtTurnEnd()
        {
            BSyncHeldCards();
            for (int i = Battle.Hand.Count - 1; i >= 0; i--)
            {
                var held = BState.HeldCards[i];
                if (held.Retain && !held.RetainUsed)
                { held.RetainUsed = true; held.Retain = false; continue; }
                Battle.DiscardPile.Add(Battle.Hand[i]); BRemoveHand(i);
            }
        }

        private bool BPlayerStart()
        {
            BInitialize(); BState.TurnUses.Clear(); BState.FirePactUsed = false;
            CTBPlayerStart();
            foreach (var pot in BState.FirePots) { BAddEnemyBurn(pot.Amount); pot.Remaining--; }
            BState.FirePots.RemoveAll(pot => pot.Remaining <= 0);
            for (int i = 0; i < BState.DelayedDamage.Count;)
            {
                var delayed = BState.DelayedDamage[i]; delayed.Remaining--;
                if (delayed.Remaining > 0) { i++; continue; }
                BState.DelayedDamage.RemoveAt(i);
                BResolveDelayedDamage(delayed);
                if (ResolveBattleOutcome()) return true;
            }
            BState.Revision++;
            return ResolveBattleOutcome();
        }

        private int BEnemyAttack(int damage, bool consume)
        {
            if (consume && damage > 0) CTBBeforeEnemyAttack();
            if (Battle.EnemyBoardingHealth <= 0) return 0;
            int afterCtb = CTBEnemyAttackReduction(damage, consume);
            int reduction = damage - afterCtb;
            if (BState.ThunderWeaknessTurns > 0) reduction = Math.Max(reduction, Math.Min(damage / 2, BState.ThunderWeakness));
            if (consume) { BState.ThunderWeaknessTurns = 0; BState.ThunderWeakness = 0; }
            return Math.Max(0, damage - reduction);
        }

        private void BAfterEnemyAttack(int absorbed, int lost)
        {
            Battle.Schools.LastGuardAbsorbed = absorbed;
            int before = SchoolSum(Battle.PlayerBlock, absorbed);
            int retainedBefore = Math.Min(before, BState.ArcaneRetainedGuard);
            int ordinaryBefore = Math.Max(0, before - retainedBefore - BState.TideGuard);
            int retainedSpent = Math.Min(retainedBefore, Math.Max(0, absorbed - ordinaryBefore));
            BState.ArcaneRetainedGuard = retainedBefore - retainedSpent;
            // Ordinary guard, then the oldest retained guard, then tide guard.
            foreach (var batch in BState.RetainedGuardBatches)
            { int spent = Math.Min(retainedSpent, batch.Amount); batch.Amount -= spent; retainedSpent -= spent; }
            BState.RetainedGuardBatches.RemoveAll(batch => batch.Amount <= 0);
            CTBAfterEnemyAttack(absorbed, lost);
            if (absorbed > 0 && BState.FireRetaliations > 0 && Battle.PlayerBoardingHealth > 0)
            { BDeal(SchoolProduct(BState.FireRetaliations, BStackTotal(BState.SelfBurn) * 2), critical: false); }
        }

        private void BResolveOldestDelayed()
        {
            if (BState.DelayedDamage.Count == 0) return;
            var delayed = BState.DelayedDamage[0]; BState.DelayedDamage.RemoveAt(0);
            BResolveDelayedDamage(delayed);
        }

        private void BResolveDelayedDamage(CoreBoardingDelayedDamage delayed)
        {
            int health = Battle.EnemyBoardingHealth, guard = Battle.EnemyBlock;
            bool attempted = health > 0 && Battle.PlayerBoardingHealth > 0 && delayed.Amount > 0;
            BDeal(delayed.Amount, delayed.IgnoreBlock, critical: false);
            // Observe the existing settlement once; this record never changes its rules.
            if (attempted) BState.RecordResolvedAttack(delayed, health, Battle.EnemyBoardingHealth, guard, Battle.EnemyBlock);
        }

        public string GetBoardingStateSummary()
        {
            if (!BActive) return string.Empty;
            return "魔力 " + Battle.Schools.AvailableMana + "/" + Battle.Schools.ManaLimit +
                "\n咒痕 " + BState.CurseMarks + "/6（不自动伤害）" +
                "\n潮势 " + BState.TidePower + "/12（本轮可用）" +
                "\n连锁 " + Battle.Schools.ThunderChain + "/6（回合末清空）" +
                "\n敌方灼烧 " + BStackTotal(BState.EnemyBurn) + " 层" +
                "\n自身燃烧 " + BStackTotal(BState.SelfBurn) + " 层" +
                (Battle.BoardingEnemy.PlayerBurnTurns > 0 ? "\n敌方点燃 " + Battle.BoardingEnemy.PlayerBurnDamage + "（剩余 " + Battle.BoardingEnemy.PlayerBurnTurns + " 次己方回合末）" : string.Empty) +
                "\n待结算攻击 " + BState.DelayedDamage.Count +
                "\n燃烧移除 " + BState.ExhaustedCards.Count + " 张" +
                (BState.FreePowderUses > 0 ? "\n免火药 " + BState.FreePowderUses + " 次" : string.Empty);
        }
    }
}
