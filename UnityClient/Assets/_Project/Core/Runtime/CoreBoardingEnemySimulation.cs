using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        // Each run owns its prediction state. Only commit runs call the existing
        // player reaction methods; previews never swap or mutate the live Battle.
        private sealed class BoardingEnemyContext
        {
            internal CoreBoardingEnemyState State;
            internal CoreBoardingFieldState Fields;
            internal int PlayerHealth, PlayerGuard, EnemyHealth, EnemyGuard, AttackBase, GuardBase;
            internal int PlayerMarks, FireRetaliations;
            internal bool PlayerOilConsumed, EnemyOilConsumed;
            internal bool PlayerExecutedByCurse, EnemyExecutedByCurse;
            internal bool Weak, Armor, ContractTriggered, CounterTriggered, Ambush;
        }

        private BoardingEnemyContext NewBoardingEnemyContext(bool commit)
        {
            return new BoardingEnemyContext {
                State = commit ? Battle.BoardingEnemy : Battle.BoardingEnemy.CopyResources(),
                Fields = commit ? Battle.BoardingFields : Battle.BoardingFields.Copy(),
                PlayerHealth = Battle.PlayerBoardingHealth, PlayerGuard = Battle.PlayerBlock,
                EnemyHealth = Battle.EnemyBoardingHealth, EnemyGuard = Battle.EnemyBlock,
                AttackBase = Battle.Profile?.MeleeDamage ?? (Battle.IsBoss ? 12 : 8),
                GuardBase = Battle.Profile?.BoardingGuard ?? (Battle.IsBoss ? 8 : 4),
                PlayerMarks = BState.CurseMarks, FireRetaliations = BState.FireRetaliations,
                Weak = true, Armor = BState.CurseArmorTurns > 0,
                ContractTriggered = BState.CurseContractTriggered, CounterTriggered = BState.CurseCounterTriggered,
                Ambush = BState.TideAmbushTurns > 0
            };
        }

        private CoreBoardingEnemyPreview RunBoardingEnemyTurn(bool commit, bool includePlayerEnd)
        {
            var preview = new CoreBoardingEnemyPreview();
            BoardingEnemyContext c = NewBoardingEnemyContext(commit);
            ResolveBoardingContextCurse(c);
            if (includePlayerEnd && c.PlayerHealth > 0 && c.EnemyHealth > 0)
            {
                if (!IsBattleHealingBlocked(true))
                    foreach (var heal in BState.HealingTicks)
                        c.PlayerHealth = Math.Min(Battle.PlayerMaxBoardingHealth, SchoolSum(c.PlayerHealth, heal.Amount));
                if (c.State.PlayerBurnTurns > 0)
                {
                    int absorbed = Math.Min(c.PlayerGuard, c.State.PlayerBurnDamage);
                    c.PlayerGuard -= absorbed;
                    preview.PlayerBurnDamage = Math.Min(c.PlayerHealth, c.State.PlayerBurnDamage - absorbed);
                    c.PlayerHealth -= preview.PlayerBurnDamage;
                    ResolveBoardingContextCurse(c);
                    if (preview.PlayerBurnDamage > 0 && c.PlayerHealth > 0) c.PlayerMarks = SchoolSum(c.PlayerMarks, BCurseContractAmount);
                    ResolveBoardingContextCurse(c);
                    c.State.PlayerBurnDamage = Math.Max(0, c.State.PlayerBurnDamage - 1);
                    c.State.PlayerBurnTurns = c.State.PlayerBurnDamage;
                }
                preview.PlayerBloodPoolLoss = PreviewBoardingContextFieldEnd(c, true);
            }
            if (c.PlayerHealth <= 0 || c.EnemyHealth <= 0)
            {
                if (commit) CommitEnemyVitals(c);
                preview.PlayerExecutedByCurse = c.PlayerExecutedByCurse;
                preview.EnemyExecutedByCurse = c.EnemyExecutedByCurse;
                preview.ResourcesAfter = c.State.CopyResources();
                return preview;
            }
            c.EnemyGuard = Math.Min(c.EnemyGuard, c.State.RetainedGuard);
            c.State.TideGuard = Math.Min(c.State.TideGuard, c.EnemyGuard);
            c.State.RetainedGuard = 0;
            if (!c.Fields.Contains(CoreNavalFieldKind.Thunder)) c.State.ThunderChain = 0;
            c.State.Mana = SchoolSum(CoreBoardingEnemyState.ManaLimit, c.Fields.Remaining(CoreNavalFieldKind.Arcane));
            CoreBoardingEnemyTurn turn = (c.State.Deck ?? CoreBoardingEnemyCatalog.ForSchool("neutral")).TurnAt(Battle.PhaseTurn);
            foreach (CoreBoardingEnemyCard card in turn.Cards)
            {
                if (c.PlayerHealth <= 0 || c.EnemyHealth <= 0) break;
                CoreBoardingEnemyActionResult step = RunBoardingEnemyAction(c, card, commit);
                preview.Steps.Add(step);
                preview.AttackDamage = SchoolSum(preview.AttackDamage, step.AttackDamage);
                preview.PiercingDamage = SchoolSum(preview.PiercingDamage, step.PiercingDamage);
                preview.PlayerHealthLoss = SchoolSum(preview.PlayerHealthLoss, step.HealthLost);
                if (commit) c.State.ExecutedActions.Add(step);
            }
            if (!commit && c.PlayerHealth > 0 && c.EnemyHealth > 0)
            {
                int burn = c.State.SelfBurnTurns > 0 ? c.State.SelfBurnDamage : 0;
                int absorbed = Math.Min(c.EnemyGuard, burn);
                c.EnemyGuard -= absorbed;
                preview.EnemySelfBurnDamage = Math.Min(c.EnemyHealth, burn - absorbed);
                c.EnemyHealth -= preview.EnemySelfBurnDamage;
                NoteBoardingEnemyLoss(c.State, absorbed, preview.EnemySelfBurnDamage, c.EnemyGuard);
                ResolveBoardingContextCurse(c);
                c.State.RetainedGuard = Math.Min(c.State.RetainedGuard, c.EnemyGuard);
                EnemySchoolEndResources(c.State);
                preview.EnemyBloodPoolLoss = PreviewBoardingContextFieldEnd(c, false);
            }
            preview.PlayerExecutedByCurse = c.PlayerExecutedByCurse;
            preview.EnemyExecutedByCurse = c.EnemyExecutedByCurse;
            preview.ResourcesAfter = c.State.CopyResources();
            return preview;
        }

        private CoreBoardingEnemyActionResult RunBoardingEnemyAction(BoardingEnemyContext c, CoreBoardingEnemyCard card, bool commit)
        {
            var r = new CoreBoardingEnemyActionResult {
                CardId = card.Id, Name = card.Name, Detail = string.Empty,
                PlayerHealthBefore = c.PlayerHealth, PlayerGuardBefore = c.PlayerGuard,
                EnemyHealthBefore = c.EnemyHealth, EnemyGuardBefore = c.EnemyGuard
            };
            int attackPercent = card.AttackPercent, healPercent = card.HealPercent, piercePercent = card.PiercePercent;
            r.SelfDamage = Math.Min(Math.Max(0, c.EnemyHealth - 1), BoardingEnemyScale(c.AttackBase, card.SelfDamagePercent));
            c.EnemyHealth -= r.SelfDamage;
            NoteBoardingEnemyLoss(c.State, 0, r.SelfDamage, c.EnemyGuard);
            if (r.SelfDamage > 0 && c.Fields.Contains(CoreNavalFieldKind.Blood))
            {
                int poolGuard = CoreFieldTradeoffRules.BloodGuard(r.SelfDamage);
                poolGuard = commit ? CTBEnemyBlockGain(poolGuard, true) : c.Armor
                    ? Math.Max(0, poolGuard - BCurseArmorAmount(c.PlayerMarks)) : poolGuard;
                c.EnemyGuard = SchoolSum(c.EnemyGuard, poolGuard);
                r.GuardGained = poolGuard;
            }
            if (ResolveBoardingContextCurse(c)) return FinishBoardingEnemyAction(c, r, commit);
            int paid = 0;
            switch (card.Mechanic)
            {
                case CoreBoardingEnemyMechanic.FireSelfBurn:
                    AddBoardingContextBurn(c, false, card.ResourceAmount, commit);
                    r.Detail = "自身燃烧 +" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.FireTransfer:
                    paid = Math.Min(card.ResourceAmount, c.State.SelfBurnDamage);
                    c.State.SelfBurnDamage -= paid;
                    c.State.SelfBurnTurns = c.State.SelfBurnDamage;
                    AddBoardingContextBurn(c, true, paid, commit);
                    r.Detail = "转移燃烧 " + paid; break;
                case CoreBoardingEnemyMechanic.FireConsume:
                    paid = Math.Min(card.ResourceAmount, c.State.PlayerBurnDamage);
                    c.State.PlayerBurnDamage -= paid;
                    c.State.PlayerBurnTurns = c.State.PlayerBurnDamage;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = "消耗目标燃烧 " + paid; break;
                case CoreBoardingEnemyMechanic.ArcaneGain:
                    int beforeMana = c.State.Mana;
                    c.State.Mana = SchoolSum(c.State.Mana, card.ResourceAmount);
                    r.Detail = "魔力 +" + (c.State.Mana - beforeMana); break;
                case CoreBoardingEnemyMechanic.ArcaneSpend:
                    paid = Math.Min(card.ResourceAmount, c.State.Mana); c.State.Mana -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = "消耗魔力 " + paid + "/" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.ThunderFinisher:
                    paid = Math.Min(card.ResourceAmount, c.State.ThunderChain);
                    if (!c.Fields.Contains(CoreNavalFieldKind.Thunder)) c.State.ThunderChain -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = c.Fields.Contains(CoreNavalFieldKind.Thunder)
                        ? "连锁增伤 " + paid + "（雷栅中免除消耗）" : "消耗连锁 " + paid; break;
                case CoreBoardingEnemyMechanic.CurseMark:
                    for (int i = 0; i < card.ResourceAmount; i++)
                    { c.State.CurseDurations.Add(1); paid++; }
                    r.Detail = "对玩家施加咒印 " + paid; break;
                case CoreBoardingEnemyMechanic.CurseDetonate:
                    paid = Math.Min(card.ResourceAmount, c.State.CurseMarks);
                    c.State.CurseDurations.Sort(); c.State.CurseDurations.RemoveRange(0, paid);
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    piercePercent = paid > 0 ? 100 : 0;
                    r.Detail = "引爆咒印 " + paid + (paid > 0 ? "（无视格挡）" : "（无咒印，普通攻击）"); break;
                case CoreBoardingEnemyMechanic.TideStrike:
                    paid = Math.Min(card.ResourceAmount, c.State.TidePower); c.State.TidePower -= paid;
                    attackPercent = SchoolSum(attackPercent, SchoolProduct(paid, card.BonusPercent));
                    r.Detail = "消耗潮势 " + paid + "/" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.TideHeal:
                    paid = Math.Min(card.ResourceAmount, c.State.TidePower); c.State.TidePower -= paid;
                    healPercent = card.ResourceAmount == 0 ? 0 : (int)Math.Min(int.MaxValue, (long)healPercent * paid / card.ResourceAmount);
                    r.Detail = "消耗潮势 " + paid + "/" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.BloodStrike:
                case CoreBoardingEnemyMechanic.BloodHarvest:
                    paid = Math.Min(card.ResourceAmount, c.State.BloodLoss); c.State.BloodLoss -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    bool desperate = CoreBloodRules.IsDesperate(c.EnemyHealth, Battle.EnemyMaxBoardingHealth);
                    if (desperate) attackPercent = SchoolSum(attackPercent, 25);
                    r.Detail = "消耗失血 " + paid + (desperate ? "；绝境强化" : string.Empty); break;
            }
            if (ResolveBoardingContextCurse(c)) return FinishBoardingEnemyAction(c, r, commit);
            int guard = BoardingEnemyScale(c.GuardBase, card.GuardPercent);
            int ordinaryGuardGained = 0;
            if (guard > 0)
            {
                if (commit) guard = CTBEnemyBlockGain(guard, true);
                else if (c.Armor)
                { guard = Math.Max(0, guard - BCurseArmorAmount(c.PlayerMarks)); }
                int newGuard = Math.Max(c.EnemyGuard,
                    Math.Min(SchoolSum(c.GuardBase, c.GuardBase), SchoolSum(c.EnemyGuard, guard)));
                ordinaryGuardGained = newGuard - c.EnemyGuard;
                r.GuardGained = SchoolSum(r.GuardGained, ordinaryGuardGained); c.EnemyGuard = newGuard;
            }
            if (card.Mechanic == CoreBoardingEnemyMechanic.TideGuard)
            {
                c.State.TideGuard = Math.Min(c.EnemyGuard, SchoolSum(c.State.TideGuard, ordinaryGuardGained));
                c.State.RetainedGuard = Math.Min(c.GuardBase, c.State.TideGuard);
                r.Detail = "留盾 " + c.State.RetainedGuard + "；实际吸收转为潮势";
            }
            r.Healed = IsBattleHealingBlocked(false) ? 0 : Math.Min(Math.Max(0, Battle.EnemyMaxBoardingHealth - c.EnemyHealth), BoardingEnemyScale(c.AttackBase, healPercent));
            c.EnemyHealth += r.Healed;
            r.RawAttack = BoardingEnemyScale(c.AttackBase, attackPercent);
            if (r.RawAttack > 0)
            {
                int incoming = ApplyCurseBattleDamage(r.RawAttack);
                if (commit) CommitEnemyVitals(c);
                else if (BState.CurseCounterTurns > 0)
                    PreviewEnemyReceives(c, BCurseCounterAmount(c.PlayerMarks), true);
                r.AttackDamage = commit ? BEnemyAttack(incoming, true) : PreviewEnemyAttack(c, incoming);
                if (commit) { c.EnemyHealth = Battle.EnemyBoardingHealth; c.EnemyGuard = Battle.EnemyBlock; }
                if (c.EnemyHealth <= 0) r.AttackDamage = 0;
                r.PiercingDamage = (int)((long)r.AttackDamage * piercePercent / 100);
                r.AbsorbedDamage = Math.Min(c.PlayerGuard, r.AttackDamage - r.PiercingDamage);
                r.HealthLost = Math.Min(c.PlayerHealth, r.AttackDamage - r.AbsorbedDamage);
                c.PlayerGuard -= r.AbsorbedDamage; c.PlayerHealth -= r.HealthLost;
                ResolveBoardingContextCurse(c);
                int drain = card.DrainPercent > 0 && c.EnemyHealth > 0 && !IsBattleHealingBlocked(false)
                    ? Math.Min(r.HealthLost, Math.Max(0, Battle.EnemyMaxBoardingHealth - c.EnemyHealth)) : 0;
                c.EnemyHealth += drain; r.Healed = SchoolSum(r.Healed, drain);
                if (r.HealthLost > 0 && c.PlayerHealth > 0)
                    AddBoardingContextBurn(c, true, BoardingEnemyScale(c.AttackBase, card.BurnPercent), commit);
                if (commit)
                {
                    CommitEnemyVitals(c);
                    c.State.RecordAttack(r.PlayerHealthBefore, r.HealthLost);
                    Battle.LastEnemyGuardAbsorbed = SchoolSum(Battle.LastEnemyGuardAbsorbed, r.AbsorbedDamage);
                    BAfterEnemyAttack(r.AbsorbedDamage, r.HealthLost);
                    c.PlayerHealth = Battle.PlayerBoardingHealth; c.PlayerGuard = Battle.PlayerBlock;
                    c.EnemyHealth = Battle.EnemyBoardingHealth; c.EnemyGuard = Battle.EnemyBlock;
                    c.PlayerMarks = BState.CurseMarks;
                    c.PlayerExecutedByCurse |= BState.PlayerExecutedByCurse;
                    c.EnemyExecutedByCurse |= BState.EnemyExecutedByCurse;
                }
                else PreviewPlayerReactions(c, r.AbsorbedDamage, r.HealthLost);
            }
            if (card.Mechanic == CoreBoardingEnemyMechanic.ThunderStrike || card.Mechanic == CoreBoardingEnemyMechanic.ThunderFinisher)
            {
                int previousChain = c.State.ThunderChain;
                c.State.ThunderChain = SchoolSum(c.State.ThunderChain, 1);
                r.Detail += (r.Detail.Length > 0 ? "；" : string.Empty) + "连锁 +1 → " + c.State.ThunderChain;
                if (c.Fields.Contains(CoreNavalFieldKind.Thunder) &&
                    CoreThunderRules.TriggersOverload(c.State.ThunderChain, c.State.ThunderChain - previousChain))
                {
                    PreviewEnemyReceives(c, CoreThunderRules.OverloadDamage, false);
                    r.Detail += "；雷栅造成 " + CoreThunderRules.OverloadDamage + " 点伤害";
                }
            }
            else if (!c.Fields.Contains(CoreNavalFieldKind.Thunder))
                c.State.ThunderChain = 0;
            return FinishBoardingEnemyAction(c, r, commit);
        }

        private CoreBoardingEnemyActionResult FinishBoardingEnemyAction(BoardingEnemyContext c, CoreBoardingEnemyActionResult r, bool commit)
        {
            if (commit) { CommitEnemyVitals(c); BSyncBurn(); }
            r.PlayerExecutedByCurse = c.PlayerExecutedByCurse;
            r.EnemyExecutedByCurse = c.EnemyExecutedByCurse;
            r.PlayerHealthAfter = c.PlayerHealth; r.PlayerGuardAfter = c.PlayerGuard;
            r.EnemyHealthAfter = c.EnemyHealth; r.EnemyGuardAfter = c.EnemyGuard;
            r.ResourcesAfter = c.State.CopyResources();
            return r;
        }

        private bool ResolveBoardingContextCurse(BoardingEnemyContext c)
        {
            if (CoreCurseRules.ReachesExecutionThreshold(c.State.CurseMarks, c.PlayerHealth))
            { c.PlayerHealth = 0; c.PlayerExecutedByCurse = true; }
            if (CoreCurseRules.ReachesExecutionThreshold(c.PlayerMarks, c.EnemyHealth, Battle.Schools.EnemyCurseExecutionThresholdPercent))
            { c.EnemyHealth = 0; c.EnemyExecutedByCurse = true; }
            return c.PlayerHealth <= 0 || c.EnemyHealth <= 0;
        }

        private void CommitEnemyVitals(BoardingEnemyContext c)
        {
            Battle.PlayerBoardingHealth = c.PlayerHealth; Battle.PlayerBlock = c.PlayerGuard;
            Battle.EnemyBoardingHealth = c.EnemyHealth; Battle.EnemyBlock = c.EnemyGuard;
            BState.PlayerExecutedByCurse |= c.PlayerExecutedByCurse;
            BState.EnemyExecutedByCurse |= c.EnemyExecutedByCurse;
        }

        private int PreviewEnemyAttack(BoardingEnemyContext c, int attack)
        {
            attack = BBoardingFieldAttackDamage(c.Fields, attack);
            int reduction = BContinuousAttackReduction(c.PlayerMarks);
            if (c.Weak && BState.ThunderWeaknessTurns > 0) reduction = Math.Max(reduction, BState.ThunderWeakness);
            c.Weak = false;
            return c.EnemyHealth <= 0 ? 0 : Math.Max(0, attack - reduction);
        }

        private void PreviewPlayerReactions(BoardingEnemyContext c, int absorbed, int lost)
        {
            if (c.PlayerHealth <= 0 || c.EnemyHealth <= 0) return;
            if (lost > 0 && BState.CurseContractTurns > 0) c.PlayerMarks = SchoolSum(c.PlayerMarks, BCurseContractAmount);
            if (ResolveBoardingContextCurse(c)) return;
            if (c.EnemyHealth > 0 && c.Ambush)
            { c.Ambush = false; PreviewEnemyReceives(c, BTideAmbushAmount, false); }
            if (c.EnemyHealth > 0 && (lost > 0 || absorbed > 0) && BState.BloodDebtTurns > 0)
                PreviewEnemyReceives(c, BLinear(BState.BloodDebtCard, 8), false);
            if (absorbed > 0 && c.FireRetaliations > 0 && c.EnemyHealth > 0)
            { PreviewEnemyReceives(c, SchoolProduct(c.FireRetaliations, c.State.PlayerBurnDamage), false); }
        }

        private void AddBoardingContextBurn(BoardingEnemyContext c, bool targetPlayer, int amount, bool commit)
        {
            if (amount <= 0) return;
            if (commit)
            {
                if (targetPlayer) BApplyPlayerBurn(amount, false);
                else BAddEnemyBurn(amount);
                return;
            }
            int adjusted = PrepareFireBurnApplication(targetPlayer, amount, false);
            // A detached preview consumes its own one-shot oil flag, never live oil.
            if (adjusted > amount)
            {
                bool consumed = targetPlayer ? c.PlayerOilConsumed : c.EnemyOilConsumed;
                if (consumed) adjusted = amount;
                else if (targetPlayer) c.PlayerOilConsumed = true;
                else c.EnemyOilConsumed = true;
            }
            if (targetPlayer)
            {
                c.State.PlayerBurnDamage = SchoolSum(c.State.PlayerBurnDamage, adjusted);
                c.State.PlayerBurnTurns = c.State.PlayerBurnDamage;
            }
            else
            {
                c.State.SelfBurnDamage = SchoolSum(c.State.SelfBurnDamage, adjusted);
                c.State.SelfBurnTurns = c.State.SelfBurnDamage;
            }
        }

        private void PreviewEnemyReceives(BoardingEnemyContext c, int damage, bool pierce)
        {
            int blocked = pierce ? 0 : Math.Min(c.EnemyGuard, damage);
            c.EnemyGuard -= blocked;
            int lost = Math.Min(c.EnemyHealth, Math.Max(0, damage - blocked)); c.EnemyHealth -= lost;
            NoteBoardingEnemyLoss(c.State, blocked, lost, c.EnemyGuard);
            c.State.RetainedGuard = Math.Min(c.State.RetainedGuard, c.EnemyGuard);
            ResolveBoardingContextCurse(c);
        }

        private int PreviewBoardingContextFieldEnd(BoardingEnemyContext c, bool player)
        {
            if (c.PlayerHealth <= 0 || c.EnemyHealth <= 0) return 0;
            AddBoardingContextBurn(c, player, c.Fields.Remaining(CoreNavalFieldKind.Fire), false);
            int marks = c.Fields.Remaining(CoreNavalFieldKind.Curse);
            if (player)
                for (int i = 0; i < marks; i++) c.State.CurseDurations.Add(int.MaxValue);
            else c.PlayerMarks = SchoolSum(c.PlayerMarks, marks);
            if (ResolveBoardingContextCurse(c)) return 0;
            int lost = Math.Min(player ? c.PlayerHealth : c.EnemyHealth, c.Fields.Remaining(CoreNavalFieldKind.Blood));
            if (player) c.PlayerHealth -= lost;
            else
            {
                c.EnemyHealth -= lost;
                NoteBoardingEnemyLoss(c.State, 0, lost, c.EnemyGuard);
            }
            if (ResolveBoardingContextCurse(c)) return lost;
            c.Fields.EndTurn(player);
            return lost;
        }

        private static void AddHostileBurn(CoreBoardingEnemyState state, int amount, int turns)
        {
            if (amount <= 0 || turns <= 0) return;
            state.PlayerBurnDamage = SchoolSum(state.PlayerBurnDamage, amount);
            state.PlayerBurnTurns = state.PlayerBurnDamage;
        }

        private static void NoteBoardingEnemyLoss(CoreBoardingEnemyState state, int absorbed, int lost, int remainingGuard)
        {
            int before = SchoolSum(Math.Max(0, remainingGuard), Math.Max(0, absorbed));
            int tideBefore = Math.Min(before, state.TideGuard);
            int tideAbsorbed = Math.Min(tideBefore, Math.Max(0, absorbed - (before - tideBefore)));
            state.TideGuard = Math.Min(remainingGuard, tideBefore - tideAbsorbed);
            state.TidePower = SchoolSum(state.TidePower, tideAbsorbed);
            if (state.Deck?.SchoolId == "blood")
                state.BloodLoss = Math.Min(CoreBoardingEnemyState.BloodLossLimit, SchoolSum(state.BloodLoss, lost));
        }

        private void OnBoardingEnemyDamaged(int absorbed, int lost)
        {
            if (!BActive) return;
            NoteBoardingEnemyLoss(Battle.BoardingEnemy, absorbed, lost, Battle.EnemyBlock);
            Battle.BoardingEnemy.RetainedGuard = Math.Min(Battle.BoardingEnemy.RetainedGuard, Battle.EnemyBlock);
        }

        private static void EnemySchoolEndResources(CoreBoardingEnemyState state)
        {
            state.SelfBurnDamage = Math.Max(0, state.SelfBurnDamage - 1);
            state.SelfBurnTurns = state.SelfBurnDamage;
        }

        private bool BEnemySchoolEnd()
        {
            CoreBoardingEnemyState state = Battle.BoardingEnemy;
            int burn = state.SelfBurnTurns > 0 ? state.SelfBurnDamage : 0;
            int absorbed = Math.Min(Battle.EnemyBlock, burn);
            Battle.EnemyBlock -= absorbed;
            int lost = Math.Min(Battle.EnemyBoardingHealth, burn - absorbed);
            Battle.EnemyBoardingHealth -= lost; state.LastSelfBurnHealthLost = lost;
            Battle.RecordSettlement(CoreBattleSettlementKind.Burn, false, lost, absorbed, healthAfter: Battle.EnemyBoardingHealth);
            NoteBoardingEnemyLoss(state, absorbed, lost, Battle.EnemyBlock);
            BResolveCurseExecutions();
            state.RetainedGuard = Math.Min(state.RetainedGuard, Battle.EnemyBlock);
            EnemySchoolEndResources(state);
            BSyncBurn();
            if (burn > 0) AddLog("敌方燃烧结算：格挡 -" + absorbed + "；船员生命 -" + lost + "。");
            return ResolveBattleOutcome();
        }
    }
}
