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
        private int BFormula(CoreBattleCard card, int constant, decimal coefficient, int amount) =>
            CoreBattleCardLinearFormula.For(card, constant, coefficient).Evaluate(amount);
        private int BEchoDamage(CoreBattleCard card, int damage) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, damage) * BValue(card, 50) / 100);
        private int BLinear(CoreBattleCard card, int value) => BValue(card, CoreBoardingCardBalance.Linear(value));
        private int BGuardValue(CoreBattleCard card, int value) => BValue(card, CoreBoardingCardBalance.Guard(card?.BoardingSpec?.Key, value));
        private bool BOnceUsed(string key, bool battle = false) => (battle ? BState.BattleUses : BState.TurnUses).Contains(key);
        private void BMarkOnce(string key, bool battle = false) => (battle ? BState.BattleUses : BState.TurnUses).Add(key);
        private static int BStackTotal(List<CoreBoardingTimedStacks> stacks)
        { int total = 0; foreach (var stack in stacks) total = SchoolSum(total, stack.Amount); return total; }

        private void BInitialize()
        {
            if (!BActive || BState.Initialized) return;
            BState.Initialized = true;
            int carriedTide = Math.Max(BState.TidePower, Battle.Schools.TidePower);
            BState.TideOverflow = Math.Max(0, carriedTide - BState.TidePoints.Count);
            Battle.Schools.CurseDamagePerMark = 0;
            Battle.Schools.CurseTickBonus = 0;
            Battle.Schools.CurseNextTickBonus = 0;
            // Boarding-entry effects may already have applied into the new pool
            // and synchronized these legacy mirrors before initialization runs.
            if (BState.EnemyBurn.Count == 0 && Battle.EnemyBurnDamage > 0 && Battle.EnemyBurnTurns > 0)
                BState.EnemyBurn.Add(new CoreBoardingTimedStacks(Battle.EnemyBurnDamage, Battle.EnemyBurnDamage));
            Battle.BoardingEnemy.BindBurnPools(BState.SelfBurn, BState.EnemyBurn);
            BSyncBurn();
            CTBSyncResources();
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
            if (BResolveCurseExecutions()) { ResolveBattleOutcome(); return false; }
            CoreBoardingPlayOptions options = suppliedOptions ?? BDefaultOptions(handIndex);
            if (GetBoardingChoiceUnavailableReason(handIndex, options).Length > 0) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
            string key = card.BoardingSpec.Key;
            options.ResourceAmount = BFixedAmount(card, options);
            int amount = options.ResourceAmount;
            int cost = BHandEnergyCost(handIndex);
            int previousEnemy = Battle.EnemyBoardingHealth, previousHealth = Battle.PlayerBoardingHealth;
            int previousExecutionHealth = BState.EnemyHealthRemovedByCurse;
            int previousGuard = Battle.PlayerBlock, previousEnergy = Battle.Energy;
            int previousMedicine = Cargo.Medicine, previousPowder = Cargo.Powder;
            int echoes = card.SchoolId == "arcane" && card.BoardingSpec.IsAttack && BState.ArcaneEchoReady
                ? Math.Max(1, BState.ArcaneEchoCharges) : 0;
            var echoCards = echoes > 0 ? new List<CoreBattleCard>(BState.ArcaneEchoCards) : null;
            if (echoes > 0)
            {
                if (echoCards.Count == 0) echoCards.Add(BState.ArcaneEchoCard);
                BState.ArcaneEchoReady = false; BState.ArcaneEchoCharges = 0; BState.ArcaneEchoCards.Clear();
            }
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
                if (BHasPowderDiscount(card)) { BState.FreePowderUses--; powder = Math.Max(0, powder - 1); }
                Cargo.Powder -= powder;
            }
            CTBPayLifeCost(card, amount);
            if (BResolveCurseExecutions())
            {
                BState.Revision++;
                Battle.LastAction = "支付「" + card.Name + "」的生命消耗后，咒印立即消灭我方。";
                ResolveBattleOutcome();
                return true;
            }
            BState.ActiveCard = card;
            BState.ActiveAttack = card.BoardingSpec.IsAttack;
            BState.ActiveCritical = card.BoardingSpec.IsAttack && !card.BoardingSpec.IsDelayed && RollBoardingCritical();
            BState.ActiveAuthoredDamage = 0;
            BState.ActiveDelayedEchoCards = card.BoardingSpec.IsDelayed ? echoCards : null;
            BState.ActiveAttackBonus = card.BoardingSpec.IsAttack ? CTBBeforeDirectAttack() : 0;
            if (card.SchoolId == "thunder" && card.BoardingSpec.IsAttack)
            { BState.ActiveAttackBonus = SchoolSum(BState.ActiveAttackBonus, BState.ThunderReady); BState.ThunderReady = 0; }

            if (!BExecuteBasic(card) && !FATExecute(card, options)) CTBExecute(card, amount);
            if (card.BoardingSpec.IsAttack && !BCurseExecutionEnded)
            {
                if (!card.BoardingSpec.IsDelayed) ApplyAttachedFireAfterAttack(card);
                CTBAfterActiveAttack();
                if (residual > 0 && Battle.EnemyBoardingHealth > 0)
                {
                    BDeal(residual, critical: false);
                }
                if (echoes > 0 && !card.BoardingSpec.IsDelayed && Battle.EnemyBoardingHealth > 0)
                    foreach (CoreBattleCard echoCard in echoCards)
                        BQueueDamage(BEchoDamage(echoCard, BState.ActiveAuthoredDamage), authored: false);
            }
            bool completeThunder = !BCurseExecutionEnded;
            BState.ActiveAttack = false; BState.ActiveCritical = false; BState.ActiveAttackBonus = 0; BState.ActiveCard = null;
            BState.ActiveDelayedEchoCards = null;
            if (completeThunder) CompleteThunderCard(card, card.BoardingSpec.IsAttack);
            // An inherited thunder field can end the battle as the card awards
            // its chain. FinishVoyage may release Battle before this returns.
            if (!BActive) return true;
            BState.Revision++;
            BSyncBurn();
            BSyncHeldCards();
            int executionHealth = BState.EnemyHealthRemovedByCurse - previousExecutionHealth;
            int directHealthLost = Math.Max(0, previousEnemy - Battle.EnemyBoardingHealth - executionHealth);
            Battle.LastAction = "打出「" + card.Name + "」" +
                (directHealthLost > 0 ? "；伤害 " + directHealthLost : string.Empty) +
                (executionHealth > 0 ? "；咒印消灭敌方" : string.Empty) +
                (Battle.PlayerBlock > previousGuard ? "；格挡 +" + (Battle.PlayerBlock - previousGuard) : string.Empty) +
                (Battle.PlayerBoardingHealth > previousHealth ? "；恢复 " + (Battle.PlayerBoardingHealth - previousHealth) : Battle.PlayerBoardingHealth < previousHealth ? "；生命 -" + (previousHealth - Battle.PlayerBoardingHealth) : string.Empty) +
                (Battle.Energy > previousEnergy - cost ? "；能量 +" + (Battle.Energy - previousEnergy + cost) : string.Empty) +
                (Cargo.Medicine < previousMedicine ? "；药品 -" + (previousMedicine - Cargo.Medicine) : string.Empty) +
                (Cargo.Powder < previousPowder ? "；火药 -" + (previousPowder - Cargo.Powder) : string.Empty) + "。";
            if (executionHealth > 0) AddLog(Battle.LastAction);
            AdvanceGuidedBossCard();
            ResolveBattleOutcome();
            return true;
        }

        private bool BExecuteBasic(CoreBattleCard card)
        {
            switch (card.BoardingSpec.Key)
            {
                case "basic_01": BDeal(BLinear(card, 8)); break;
                case "basic_02": BDeal(BLinear(card, 20)); break;
                case "basic_03": BDeal(BLinear(card, 35)); break;
                case "basic_04": BGuard(BGuardValue(card, 4)); break;
                case "basic_05": BGuard(BGuardValue(card, 10)); break;
                case "basic_06":
                    if (Cargo.Medicine > 0)
                    { if (Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth > 2) Cargo.Medicine--; BHeal(BLinear(card, 6)); }
                    else BHeal(2);
                    break;
                case "basic_07":
                    int heal = 2; if (Cargo.Medicine > 0) { Cargo.Medicine--; heal = CoreBoardingCardBalance.Sustain("basic_07", card.UpgradeLevel); }
                    BQueueHeal(heal, 2); break;
                case "basic_08": BDraw(card.Draw); break;
                case "basic_09":
                case "basic_10": Battle.Energy = SchoolSum(Battle.Energy, CoreBattleUpgradeBalance.EnergyRecovery(card)); break;
                case "basic_11": BDeal(BLinear(card, 4)); BDraw(card.Draw); break;
                case "basic_12": BGuard(BGuardValue(card, 2)); BDraw(card.Draw); break;
                case "basic_13": BQueueDamage(BLinear(card, 12)); break;
                default: return false;
            }
            return true;
        }

        private int BDeal(int amount, bool ignoreBlock = false, bool critical = true)
        {
            if (BResolveCurseExecutions()) return 0;
            amount = Math.Max(0, amount);
            if (critical && BState.ActiveAttack)
            {
                amount = SchoolSum(amount, BState.ActiveAttackBonus); BState.ActiveAttackBonus = 0;
                BState.ActiveAuthoredDamage = SchoolSum(BState.ActiveAuthoredDamage, amount);
                if (BState.ActiveCritical) amount = DoubleBattleDamage(amount);
                amount = BBoardingFieldAttackDamage(amount);
            }
            int blocked = 0;
            if (!ignoreBlock)
            { blocked = Math.Min(Battle.EnemyBlock, amount); Battle.EnemyBlock -= blocked; amount -= blocked; }
            int actual = Math.Min(Battle.EnemyBoardingHealth, amount);
            Battle.EnemyBoardingHealth -= actual;
            OnBoardingEnemyDamaged(blocked, actual);
            BResolveCurseExecutions();
            return actual;
        }

        private int BHeal(int amount) => BRestoreHealth(amount, false);

        private int BRestoreHealth(int amount, bool afterEnemyExecution)
        {
            BResolveCurseExecutions();
            if (Battle.PlayerBoardingHealth <= 0 || (!afterEnemyExecution && BState.EnemyExecutedByCurse) || IsBattleHealingBlocked(true)) return 0;
            int actual = Math.Min(Math.Max(0, Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth), Math.Max(0, amount));
            Battle.PlayerBoardingHealth += actual;
            return actual;
        }
        private void BGuard(int amount)
        {
            if (BCurseExecutionEnded) return;
            int before = Battle.PlayerBlock;
            Battle.PlayerBlock = SchoolSum(before, amount);
            if (BState.TideGuardConversionActive)
            {
                BState.TideGuard = SchoolSum(BState.TideGuard, Battle.PlayerBlock - before);
                CTBSyncResources();
            }
        }
        private void BDraw(int amount)
        { if (!BCurseExecutionEnded) { DrawCards(amount); BSyncHeldCards(); } }
        private void BQueueDamage(int amount, bool ignoreBlock = false, bool authored = true)
        {
            if (BCurseExecutionEnded) return;
            if (authored && BState.ActiveAttack && BState.ActiveCard?.BoardingSpec?.IsDelayed == true)
            {
                amount = SchoolSum(amount, BState.ActiveAttackBonus); BState.ActiveAttackBonus = 0;
                BState.ActiveAuthoredDamage = SchoolSum(BState.ActiveAuthoredDamage, amount);
            }
            var delayed = new CoreBoardingDelayedDamage
            {
                Amount = amount, IgnoreBlock = ignoreBlock, Label = BState.ActiveCard?.Name ?? "延迟攻击",
                SourceSchoolId = BState.ActiveCard?.SchoolId ?? string.Empty,
                SourceCardId = BState.ActiveCard?.Id ?? string.Empty,
                AppliesAttachedFire = authored && BState.ActiveCard?.BoardingSpec?.IsAttack == true,
            };
            if (authored && BState.ActiveDelayedEchoCards != null)
                delayed.EchoCards.AddRange(BState.ActiveDelayedEchoCards);
            BState.DelayedDamage.Add(delayed);
        }
        private void BQueueHeal(int amount, int ticks)
        {
            int total = SchoolSum(BState.ContinuousRecovery, Math.Max(0, amount));
            BState.HealingTicks.Clear();
            if (total > 0) BState.HealingTicks.Add(new CoreBoardingHealingTick { Amount = total, Remaining = total });
        }
        private void BSpendMana(int amount) { PayMana(amount); }
        private void BAddEnemyBurn(int amount, int remaining = 2)
        {
            if (BCurseExecutionEnded) return;
            amount = PrepareFireBurnApplication(false, amount);
            if (amount > 0 && Battle.EnemyBoardingHealth > 0)
                BSetStacks(BState.EnemyBurn, SchoolSum(BStackTotal(BState.EnemyBurn), amount));
            BSyncBurn();
        }
        private void BAddSelfBurn(int amount)
            => BApplyPlayerBurn(amount, true);

        private void BApplyPlayerBurn(int amount, bool selfApplied)
        {
            if (BCurseExecutionEnded) return;
            amount = PrepareFireBurnApplication(true, amount);
            if (amount <= 0) return;
            BSetStacks(BState.SelfBurn, SchoolSum(BStackTotal(BState.SelfBurn), amount));
            if (selfApplied && BState.FirePactTurns > 0) BAddEnemyBurn(SchoolProduct(amount, Math.Max(1, BState.FirePacts.Count)));
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
            if (BResolveCurseExecutions()) return ResolveBattleOutcome();
            int recovery = BState.ContinuousRecovery;
            int recoveryBefore = Battle.PlayerBoardingHealth;
            BHeal(recovery);
            Battle.RecordSettlement(CoreBattleSettlementKind.Recovery, true, healed: Battle.PlayerBoardingHealth - recoveryBefore,
                healthAfter: Battle.PlayerBoardingHealth);
            BState.HealingTicks.Clear();
            if (recovery > 1) BState.HealingTicks.Add(new CoreBoardingHealingTick { Amount = recovery - 1, Remaining = recovery - 1 });
            TickBoardingEnemyFire();
            if (BBoardingFieldsEndTurn(true)) return true;
            CTBPlayerEnd();
            Battle.Schools.TemporaryMana = 0;
            BState.FreePowderUses = 0;
            BState.ThunderReady = 0; BState.ThunderResidualCharges = 0;
            BAdvanceWindows(BState.FirePacts); BState.FirePactTurns = BWindowTurns(BState.FirePacts);
            TickBattleHealingBan(true);
            BState.Revision++;
            return ResolveBattleOutcome();
        }

        private bool BEnemyEnd()
        {
            if (BEnemySchoolEnd()) return true;
            if (BBoardingFieldsEndTurn(false)) return true;
            CTBEnemyEnd();
            TickBattleHealingBan(false);
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
            ResetNavalThunderChain(true);
            if (BResolveCurseExecutions()) return ResolveBattleOutcome();
            BBoardingFieldsBeginTurn(true);
            CTBPlayerStart();
            foreach (var pot in BState.FirePots) { BAddEnemyBurn(pot.Amount); if (pot.Remaining != int.MaxValue) pot.Remaining--; }
            BState.FirePots.RemoveAll(pot => pot.Remaining <= 0);
            // An echo created by a settlement starts its own delay. Only effects that
            // were queued before this turn began can become due in this pass.
            foreach (var delayed in new List<CoreBoardingDelayedDamage>(BState.DelayedDamage))
            {
                delayed.Remaining--;
                if (delayed.Remaining > 0) continue;
                BState.DelayedDamage.Remove(delayed);
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
            damage = BBoardingFieldAttackDamage(damage);
            int afterCtb = CTBEnemyAttackReduction(damage, consume);
            int reduction = damage - afterCtb;
            if (BState.ThunderWeaknessTurns > 0) reduction = Math.Max(reduction, BState.ThunderWeakness);
            if (consume) { BState.ThunderWeaknessTurns = 0; BState.ThunderWeakness = 0; }
            return Math.Max(0, damage - reduction);
        }

        private void BAfterEnemyAttack(int absorbed, int lost)
        {
            Battle.Schools.LastGuardAbsorbed = absorbed;
            CTBAfterEnemyAttack(absorbed, lost);
            if (absorbed > 0 && BState.FireRetaliations > 0 && Battle.PlayerBoardingHealth > 0)
            { BDeal(SchoolProduct(BState.FireRetaliations, BStackTotal(BState.SelfBurn)), critical: false); }
        }

        private void BConsumeRetainedGuard(int absorbed)
        {
            int before = SchoolSum(Battle.PlayerBlock, absorbed);
            int retainedBefore = Math.Min(before, BState.ArcaneRetainedGuard);
            int ordinaryBefore = Math.Max(0, before - retainedBefore - (BState.TideGuardConversionActive ? 0 : BState.TideGuard));
            int retainedSpent = Math.Min(retainedBefore, Math.Max(0, absorbed - ordinaryBefore));
            BState.ArcaneRetainedGuard = retainedBefore - retainedSpent;
            // Consume ordinary guard before tide guard. Converted retained guard
            // keeps its retention deadline and is spent after other converted guard.
            foreach (var batch in BState.RetainedGuardBatches)
            { int spent = Math.Min(retainedSpent, batch.Amount); batch.Amount -= spent; retainedSpent -= spent; }
            BState.RetainedGuardBatches.RemoveAll(batch => batch.Amount <= 0);
        }

        private void BResolveOldestDelayed()
        {
            BResolveSelectedDelayed(0);
        }
        private void BResolveSelectedDelayed(int index)
        {
            if (index < 0 || index >= BState.DelayedDamage.Count) return;
            var delayed = BState.DelayedDamage[index]; BState.DelayedDamage.RemoveAt(index);
            BResolveDelayedDamage(delayed);
        }

        private void BResolveDelayedDamage(CoreBoardingDelayedDamage delayed)
        {
            CoreBattleState resolvedBattle = Battle;
            CoreBattleSettlementWindow previous = resolvedBattle.SettlementWindow;
            // The resolved attack owns its complete contact, including an immediate curse execution.
            // Keep this attribution scoped: a later genuine turn-end effect still uses its own window.
            resolvedBattle.SettlementWindow = CoreBattleSettlementWindow.DelayedAttack;
            try { BResolveDelayedDamageCore(delayed); }
            finally { resolvedBattle.SettlementWindow = previous; }
        }

        private void BResolveDelayedDamageCore(CoreBoardingDelayedDamage delayed)
        {
            int health = Battle.EnemyBoardingHealth, guard = Battle.EnemyBlock;
            bool attempted = health > 0 && Battle.PlayerBoardingHealth > 0 && delayed.Amount > 0;
            // Only the authored scheduled attack is active damage; secondary
            // echoes and retaliation do not gain another terrain multiplier.
            int amount = delayed.AppliesAttachedFire ? BBoardingFieldAttackDamage(delayed.Amount) : delayed.Amount;
            int actualDamage = BDeal(amount, delayed.IgnoreBlock, critical: false);
            if (attempted && delayed.AppliesAttachedFire && !BCurseExecutionEnded)
            {
                foreach (CoreBattleCard source in CardLibrary)
                    if (source.Id == delayed.SourceCardId) { ApplyAttachedFireAfterAttack(source); break; }
            }
            // Observe the existing settlement once; this record never changes its rules.
            if (attempted) BState.RecordResolvedAttack(delayed, health, Battle.EnemyBoardingHealth, guard, Battle.EnemyBlock, actualDamage);
            if (attempted && Battle.EnemyBoardingHealth > 0)
                foreach (CoreBattleCard echoCard in delayed.EchoCards)
                    BState.DelayedDamage.Add(new CoreBoardingDelayedDamage
                    {
                        Amount = BEchoDamage(echoCard, delayed.Amount),
                        Label = delayed.Label, SourceSchoolId = delayed.SourceSchoolId, SourceCardId = delayed.SourceCardId,
                    });
        }

        public string GetBoardingStateSummary()
        {
            if (!BActive) return string.Empty;
            return "魔力 " + Battle.Schools.AvailableMana + "/" + Battle.Schools.ManaLimit +
                "\n敌方咒印 " + BState.CurseMarks + " 层" +
                "\n潮势 " + BState.TidePower + "（保留至战斗结束）" +
                "\n连锁 " + Battle.Schools.ThunderChain + "（回合开始时清空）" +
                "\n敌方燃烧 " + BStackTotal(BState.EnemyBurn) + " 层" +
                "\n自身燃烧 " + BStackTotal(BState.SelfBurn) + " 层" +
                "\n持续恢复 " + BState.ContinuousRecovery + " 层" +
                (Battle.BoardingEnemy.CurseMarks > 0 ? "\n自身咒印 " + Battle.BoardingEnemy.CurseMarks + " 层" : string.Empty) +
                "\n待结算攻击 " + BState.DelayedDamage.Count +
                "\n焚烧移除 " + BState.ExhaustedCards.Count + " 张" +
                (BState.FreePowderUses > 0 ? "\n免火药 " + BState.FreePowderUses + " 次" : string.Empty);
        }
    }
}
