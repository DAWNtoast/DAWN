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
            internal int PlayerHealth, PlayerGuard, EnemyHealth, EnemyGuard, AttackBase, GuardBase;
            internal int PlayerMarks, FireRetaliations, PlayerSelfBurn;
            internal bool Weak, Armor, ContractTriggered, CounterTriggered, Ambush, DebtTriggered;
        }

        private BoardingEnemyContext NewBoardingEnemyContext(bool commit)
        {
            return new BoardingEnemyContext {
                State = commit ? Battle.BoardingEnemy : Battle.BoardingEnemy.CopyResources(),
                PlayerHealth = Battle.PlayerBoardingHealth, PlayerGuard = Battle.PlayerBlock,
                EnemyHealth = Battle.EnemyBoardingHealth, EnemyGuard = Battle.EnemyBlock,
                AttackBase = Battle.Profile?.MeleeDamage ?? (Battle.IsBoss ? 12 : 8),
                GuardBase = Battle.Profile?.BoardingGuard ?? (Battle.IsBoss ? 8 : 4),
                PlayerMarks = BState.CurseMarks, FireRetaliations = BState.FireRetaliations, PlayerSelfBurn = BStackTotal(BState.SelfBurn),
                Weak = true, Armor = BState.CurseArmorTurns > 0,
                ContractTriggered = BState.CurseContractTriggered, CounterTriggered = BState.CurseCounterTriggered,
                Ambush = BState.TideAmbushTurns > 0, DebtTriggered = BState.BloodDebtTriggered
            };
        }

        private CoreBoardingEnemyPreview RunBoardingEnemyTurn(bool commit, bool includePlayerEnd)
        {
            var preview = new CoreBoardingEnemyPreview();
            BoardingEnemyContext c = NewBoardingEnemyContext(commit);
            if (includePlayerEnd)
            {
                foreach (var heal in BState.HealingTicks)
                    c.PlayerHealth = Math.Min(Battle.PlayerMaxBoardingHealth, SchoolSum(c.PlayerHealth, heal.Amount));
                c.PlayerHealth = Math.Max(0, c.PlayerHealth - BStackTotal(BState.SelfBurn));
                c.PlayerSelfBurn = Math.Max(0, BStackTotal(BState.SelfBurn) - 1);
                if (c.State.PlayerBurnTurns > 0)
                {
                    preview.PlayerBurnDamage = Math.Min(c.PlayerHealth, c.State.PlayerBurnDamage);
                    c.PlayerHealth -= preview.PlayerBurnDamage;
                    if (preview.PlayerBurnDamage > 0 && c.PlayerHealth > 0) c.PlayerMarks = SchoolSum(c.PlayerMarks, BCurseContractAmount);
                    c.State.PlayerBurnDamage = Math.Max(0, c.State.PlayerBurnDamage - 1);
                    c.State.PlayerBurnTurns = c.State.PlayerBurnDamage;
                }
                int curseLoss = Math.Min(c.PlayerHealth, c.State.CurseMarks);
                preview.PlayerCurseDamage = curseLoss;
                c.PlayerHealth -= curseLoss;
                if (curseLoss > 0 && c.PlayerHealth > 0) c.PlayerMarks = SchoolSum(c.PlayerMarks, BCurseContractAmount);
                if (c.State.CurseMarks > 0) c.State.CurseDurations.RemoveAt(0);
            }
            c.EnemyGuard = Math.Min(c.EnemyGuard, c.State.RetainedGuard);
            c.State.RetainedGuard = 0;
            c.State.ThunderChain = 0;
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
                preview.EnemySelfBurnDamage = Math.Min(c.EnemyHealth, c.State.SelfBurnTurns > 0 ? c.State.SelfBurnDamage : 0);
                c.EnemyHealth -= preview.EnemySelfBurnDamage;
                NoteBoardingEnemyLoss(c.State, 0, preview.EnemySelfBurnDamage);
                EnemySchoolEndResources(c.State);
                if (c.EnemyHealth > 0)
                {
                    int burn = Math.Min(c.EnemyHealth, BStackTotal(BState.EnemyBurn));
                    c.EnemyHealth -= burn; NoteBoardingEnemyLoss(c.State, 0, burn);
                }
                if (c.EnemyHealth > 0)
                {
                    int curse = Math.Min(c.EnemyHealth, SchoolSum(c.PlayerMarks, Battle.Schools.CurseNextTickBonus));
                    c.EnemyHealth -= curse; NoteBoardingEnemyLoss(c.State, 0, curse);
                }
            }
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
            NoteBoardingEnemyLoss(c.State, 0, r.SelfDamage);
            int paid = 0;
            switch (card.Mechanic)
            {
                case CoreBoardingEnemyMechanic.FireSelfBurn:
                    c.State.SelfBurnDamage = SchoolSum(c.State.SelfBurnDamage, card.ResourceAmount);
                    c.State.SelfBurnTurns = c.State.SelfBurnDamage;
                    r.Detail = "自燃 +" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.FireTransfer:
                    paid = Math.Min(card.ResourceAmount, c.State.SelfBurnDamage);
                    int duration = c.State.SelfBurnTurns;
                    c.State.SelfBurnDamage -= paid;
                    c.State.SelfBurnTurns = c.State.SelfBurnDamage;
                    AddHostileBurn(c.State, paid, duration);
                    r.Detail = "转移自燃 " + paid; break;
                case CoreBoardingEnemyMechanic.FireConsume:
                    paid = Math.Min(card.ResourceAmount, c.State.PlayerBurnDamage);
                    c.State.PlayerBurnDamage -= paid;
                    c.State.PlayerBurnTurns = c.State.PlayerBurnDamage;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = "消耗敌施灼烧 " + paid; break;
                case CoreBoardingEnemyMechanic.ArcaneGain:
                    int beforeMana = c.State.Mana;
                    c.State.Mana = Math.Min(CoreBoardingEnemyState.ManaLimit, SchoolSum(c.State.Mana, card.ResourceAmount));
                    r.Detail = "魔力 +" + (c.State.Mana - beforeMana); break;
                case CoreBoardingEnemyMechanic.ArcaneSpend:
                    paid = Math.Min(card.ResourceAmount, c.State.Mana); c.State.Mana -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = "消耗魔力 " + paid + "/" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.ThunderFinisher:
                    paid = Math.Min(card.ResourceAmount, c.State.ThunderChain); c.State.ThunderChain -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = "消耗连锁 " + paid; break;
                case CoreBoardingEnemyMechanic.CurseMark:
                    for (int i = 0; i < card.ResourceAmount; i++)
                    { c.State.CurseDurations.Add(1); paid++; }
                    r.Detail = "对玩家施加咒印 " + paid; break;
                case CoreBoardingEnemyMechanic.CurseDetonate:
                    paid = Math.Min(card.ResourceAmount, c.State.CurseMarks);
                    c.State.CurseDurations.Sort(); c.State.CurseDurations.RemoveRange(0, paid);
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    piercePercent = paid > 0 ? 100 : 0;
                    r.Detail = "引爆咒痕 " + paid + (paid > 0 ? "（无视格挡）" : "（无咒痕，普通攻击）"); break;
                case CoreBoardingEnemyMechanic.TideStrike:
                    paid = Math.Min(card.ResourceAmount, c.State.TidePower); c.State.TidePower -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    r.Detail = "消耗潮势 " + paid + "/" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.TideHeal:
                    paid = Math.Min(card.ResourceAmount, c.State.TidePower); c.State.TidePower -= paid;
                    healPercent = card.ResourceAmount == 0 ? 0 : healPercent * paid / card.ResourceAmount;
                    r.Detail = "消耗潮势 " + paid + "/" + card.ResourceAmount; break;
                case CoreBoardingEnemyMechanic.BloodStrike:
                case CoreBoardingEnemyMechanic.BloodHarvest:
                    paid = Math.Min(card.ResourceAmount, c.State.BloodLoss); c.State.BloodLoss -= paid;
                    attackPercent = SchoolSum(attackPercent, paid * card.BonusPercent);
                    bool desperate = (long)c.EnemyHealth * 100 <= (long)Battle.EnemyMaxBoardingHealth * 40;
                    if (desperate) attackPercent = SchoolSum(attackPercent, 25);
                    r.Detail = "消耗失血 " + paid + (desperate ? "；绝境强化" : string.Empty); break;
            }
            int guard = BoardingEnemyScale(c.GuardBase, card.GuardPercent);
            if (guard > 0)
            {
                if (commit) guard = CTBEnemyBlockGain(guard, true);
                else if (c.Armor)
                { guard = Math.Max(0, guard - BCurseArmorAmount(c.PlayerMarks)); }
                int newGuard = Math.Min(SchoolSum(c.GuardBase, c.GuardBase), SchoolSum(c.EnemyGuard, guard));
                r.GuardGained = newGuard - c.EnemyGuard; c.EnemyGuard = newGuard;
            }
            if (card.Mechanic == CoreBoardingEnemyMechanic.TideGuard)
            {
                c.State.RetainedGuard = Math.Min(c.GuardBase, c.EnemyGuard);
                r.Detail = "留盾 " + c.State.RetainedGuard + "；实际吸收转为潮势";
            }
            r.Healed = Math.Min(Math.Max(0, Battle.EnemyMaxBoardingHealth - c.EnemyHealth), BoardingEnemyScale(c.AttackBase, healPercent));
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
                int drain = Math.Min(Math.Min(r.HealthLost, BoardingEnemyScale(c.AttackBase, card.DrainPercent)), Math.Max(0, Battle.EnemyMaxBoardingHealth - c.EnemyHealth));
                c.EnemyHealth += drain; r.Healed = SchoolSum(r.Healed, drain);
                if (r.HealthLost > 0 && c.PlayerHealth > 0)
                    AddHostileBurn(c.State, BoardingEnemyScale(c.AttackBase, card.BurnPercent), 2);
                if (card.Mechanic == CoreBoardingEnemyMechanic.ThunderStrike)
                {
                    c.State.ThunderChain = Math.Min(CoreBoardingEnemyState.ThunderChainLimit, c.State.ThunderChain + 1);
                    r.Detail = "连锁 +1 → " + c.State.ThunderChain;
                }
                if (commit)
                {
                    CommitEnemyVitals(c);
                    c.State.RecordAttack(r.PlayerHealthBefore, r.HealthLost);
                    Battle.LastEnemyGuardAbsorbed = SchoolSum(Battle.LastEnemyGuardAbsorbed, r.AbsorbedDamage);
                    if (c.PlayerHealth > 0) BAfterEnemyAttack(r.AbsorbedDamage, r.HealthLost);
                    c.PlayerHealth = Battle.PlayerBoardingHealth; c.PlayerGuard = Battle.PlayerBlock;
                    c.EnemyHealth = Battle.EnemyBoardingHealth; c.EnemyGuard = Battle.EnemyBlock;
                }
                else PreviewPlayerReactions(c, r.AbsorbedDamage, r.HealthLost);
            }
            if (commit) CommitEnemyVitals(c);
            r.PlayerHealthAfter = c.PlayerHealth; r.PlayerGuardAfter = c.PlayerGuard;
            r.EnemyHealthAfter = c.EnemyHealth; r.EnemyGuardAfter = c.EnemyGuard;
            r.ResourcesAfter = c.State.CopyResources();
            return r;
        }

        private void CommitEnemyVitals(BoardingEnemyContext c)
        {
            Battle.PlayerBoardingHealth = c.PlayerHealth; Battle.PlayerBlock = c.PlayerGuard;
            Battle.EnemyBoardingHealth = c.EnemyHealth; Battle.EnemyBlock = c.EnemyGuard;
        }

        private int PreviewEnemyAttack(BoardingEnemyContext c, int attack)
        {
            int reduction = BContinuousAttackReduction(c.PlayerMarks);
            if (c.Weak && BState.ThunderWeaknessTurns > 0) reduction = Math.Max(reduction, Math.Min(attack / 2, BState.ThunderWeakness));
            c.Weak = false;
            return c.EnemyHealth <= 0 ? 0 : Math.Max(0, attack - reduction);
        }

        private void PreviewPlayerReactions(BoardingEnemyContext c, int absorbed, int lost)
        {
            if (c.PlayerHealth <= 0 || c.EnemyHealth <= 0) return;
            if (lost > 0 && BState.CurseContractTurns > 0) c.PlayerMarks = SchoolSum(c.PlayerMarks, BCurseContractAmount);
            if (c.EnemyHealth > 0 && c.Ambush)
            { c.Ambush = false; PreviewEnemyReceives(c, BTideAmbushAmount, false); }
            if (c.EnemyHealth > 0 && lost > 0 && BState.BloodDebtTurns > 0 && !c.DebtTriggered)
            { c.DebtTriggered = true; PreviewEnemyReceives(c, BValue(BState.BloodDebtCard, 8), false); }
            if (absorbed > 0 && c.FireRetaliations > 0 && c.EnemyHealth > 0)
            { PreviewEnemyReceives(c, SchoolProduct(c.FireRetaliations, c.PlayerSelfBurn * 2), false); }
        }

        private static void PreviewEnemyReceives(BoardingEnemyContext c, int damage, bool pierce)
        {
            int blocked = pierce ? 0 : Math.Min(c.EnemyGuard, damage);
            c.EnemyGuard -= blocked;
            int lost = Math.Min(c.EnemyHealth, Math.Max(0, damage - blocked)); c.EnemyHealth -= lost;
            NoteBoardingEnemyLoss(c.State, blocked, lost);
            c.State.RetainedGuard = Math.Min(c.State.RetainedGuard, c.EnemyGuard);
        }

        private static void AddHostileBurn(CoreBoardingEnemyState state, int amount, int turns)
        {
            if (amount <= 0 || turns <= 0) return;
            state.PlayerBurnDamage = SchoolSum(state.PlayerBurnDamage, amount);
            state.PlayerBurnTurns = state.PlayerBurnDamage;
        }

        private static void NoteBoardingEnemyLoss(CoreBoardingEnemyState state, int absorbed, int lost)
        {
            if (state.Deck?.SchoolId == "tide")
                state.TidePower = Math.Min(CoreBoardingEnemyState.TidePowerLimit, SchoolSum(state.TidePower, absorbed));
            if (state.Deck?.SchoolId == "blood")
                state.BloodLoss = Math.Min(CoreBoardingEnemyState.BloodLossLimit, SchoolSum(state.BloodLoss, lost));
        }

        private void OnBoardingEnemyDamaged(int absorbed, int lost)
        {
            if (!BActive || Battle.Profile?.TrainingTarget != null) return;
            NoteBoardingEnemyLoss(Battle.BoardingEnemy, absorbed, lost);
            Battle.BoardingEnemy.RetainedGuard = Math.Min(Battle.BoardingEnemy.RetainedGuard, Battle.EnemyBlock);
        }

        private static void EnemySchoolEndResources(CoreBoardingEnemyState state)
        {
            state.ThunderChain = 0; state.TidePower = 0;
            state.SelfBurnDamage = Math.Max(0, state.SelfBurnDamage - 1);
            state.SelfBurnTurns = state.SelfBurnDamage;
        }

        private bool BEnemySchoolEnd()
        {
            CoreBoardingEnemyState state = Battle.BoardingEnemy;
            int lost = Math.Min(Battle.EnemyBoardingHealth, state.SelfBurnTurns > 0 ? state.SelfBurnDamage : 0);
            Battle.EnemyBoardingHealth -= lost; state.LastSelfBurnHealthLost = lost;
            NoteBoardingEnemyLoss(state, 0, lost);
            EnemySchoolEndResources(state);
            if (lost > 0) AddLog("敌方自燃结算：船员生命 -" + lost + "。");
            return ResolveBattleOutcome();
        }
    }
}
