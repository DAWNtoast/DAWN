using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BoardingSchoolCastRole { Attack, Affliction, Empower, Guard, Heal, Charge, Field }

    /// <summary>Presentation classification only. Call after a successful, authoritative action commit.</summary>
    public static class BattleSchoolAttackPresentation
    {
        public static bool TrySchool(string id, out BattleSchoolAttackKind school)
        {
            switch (id)
            {
                case "fire": school = BattleSchoolAttackKind.Fire; return true;
                case "arcane": school = BattleSchoolAttackKind.Arcane; return true;
                case "curse": school = BattleSchoolAttackKind.Curse; return true;
                case "tide": school = BattleSchoolAttackKind.Tide; return true;
                case "thunder": school = BattleSchoolAttackKind.Thunder; return true;
                case "blood": school = BattleSchoolAttackKind.Blood; return true;
                default: school = default; return false;
            }
        }

        public static BattleSchoolAttackOutcome Outcome(int healthLost, int guardAbsorbed) => healthLost > 0
            ? BattleSchoolAttackOutcome.Hit : guardAbsorbed > 0 ? BattleSchoolAttackOutcome.Guarded : BattleSchoolAttackOutcome.Missed;

        /// <summary>Authored cast intent, independent of passive damage and localized card text.</summary>
        public static bool TryBoardingCast(CoreBattleCard card, out BattleSchoolAttackKind school, out BoardingSchoolCastRole role)
        {
            role = BoardingSchoolCastRole.Empower;
            school = default;
            if (card == null || !card.IsAllowedInStage(NavalBattleStage.Boarding)) return false;
            card = CoreBoardingCardCatalog.ForBoarding(card);
            if (!TrySchool(card.SchoolId, out school)) return false;
            if (card.BoardingSpec?.IsDelayed == true)
                role = BoardingSchoolCastRole.Charge;
            else if (CoreComponentFieldCards.IsFieldCard(card))
                role = BoardingSchoolCastRole.Field;
            else if (card.BoardingSpec != null)
            {
                if (card.BoardingSpec.IsAttack || card.BoardingSpec.Key == "curse_11")
                    role = BoardingSchoolCastRole.Attack;
                else
                {
                    switch (card.BoardingSpec.Key)
                    {
                        case "fire_03": case "fire_04": case "fire_07": case "fire_15":
                        case "curse_02": case "curse_03": case "curse_07": case "curse_08":
                        case "curse_09": case "curse_10": case "tide_09":
                            role = BoardingSchoolCastRole.Affliction;
                            break;
                        default:
                            role = card.Repair > 0 ? BoardingSchoolCastRole.Heal :
                                (card.BoardingBlock ?? card.Block) > 0 ? BoardingSchoolCastRole.Guard : BoardingSchoolCastRole.Empower;
                            break;
                    }
                }
            }
            else if (card.Damage > 0)
                role = BoardingSchoolCastRole.Attack;
            else
            {
                switch (card.SchoolEffect?.Mechanic)
                {
                    case CoreSchoolMechanic.FireExploit: case CoreSchoolMechanic.ArcaneRelease:
                    case CoreSchoolMechanic.CurseDetonate: case CoreSchoolMechanic.CurseExploit:
                    case CoreSchoolMechanic.TideRiposte: case CoreSchoolMechanic.ThunderPrime:
                    case CoreSchoolMechanic.ThunderChain: case CoreSchoolMechanic.ThunderFinish:
                    case CoreSchoolMechanic.BloodRisk: case CoreSchoolMechanic.BloodExecute:
                        role = BoardingSchoolCastRole.Attack;
                        break;
                    case CoreSchoolMechanic.FireIgnite: case CoreSchoolMechanic.FireIgniteOrExtend:
                    case CoreSchoolMechanic.FireExtend: case CoreSchoolMechanic.FireExtraTick:
                    case CoreSchoolMechanic.CurseMark:
                        role = BoardingSchoolCastRole.Affliction;
                        break;
                    case CoreSchoolMechanic.FireHeal: case CoreSchoolMechanic.ArcaneMend:
                    case CoreSchoolMechanic.CurseDrain: case CoreSchoolMechanic.TideRenew:
                    case CoreSchoolMechanic.BloodMend:
                        role = BoardingSchoolCastRole.Heal;
                        break;
                    case CoreSchoolMechanic.FireGuard: case CoreSchoolMechanic.FirePrepareGuard:
                    case CoreSchoolMechanic.FireNaturalGuard: case CoreSchoolMechanic.FireBoardingCover:
                    case CoreSchoolMechanic.ArcaneWard: case CoreSchoolMechanic.CurseWard:
                    case CoreSchoolMechanic.TideWard: case CoreSchoolMechanic.TideCleanse:
                    case CoreSchoolMechanic.ThunderWard: case CoreSchoolMechanic.BloodWard:
                        role = BoardingSchoolCastRole.Guard;
                        break;
                    default:
                        role = card.Repair > 0 ? BoardingSchoolCastRole.Heal :
                            (card.BoardingBlock ?? card.Block) > 0 ? BoardingSchoolCastRole.Guard : BoardingSchoolCastRole.Empower;
                        break;
                }
            }
            return true;
        }

        public static bool TargetsEnemy(BoardingSchoolCastRole role) =>
            role == BoardingSchoolCastRole.Attack || role == BoardingSchoolCastRole.Affliction;

        public static bool TryPlayer(CoreBattleCard card, bool boarding, int healthLost, int guardLost,
            out BattleSchoolAttackKind school, out BattleSchoolAttackOutcome outcome)
        {
            outcome = Outcome(healthLost, guardLost);
            if (!TrySchool(card?.SchoolId, out school)) return false;
            if (boarding && card.BoardingSpec != null)
            {
                // Queued attacks do not land on play. Explicit resolution has its own
                // committed event, whose school belongs to the queued source card.
                return !card.BoardingSpec.IsDelayed && (card.BoardingSpec.IsAttack ||
                    card.BoardingSpec.Key == "curse_11" && (healthLost > 0 || guardLost > 0));
            }
            if (card.Damage > 0) return true;
            if (healthLost <= 0 && guardLost <= 0) return false;
            // Resource-priced attacks can have zero printed base damage. Do not infer an
            // attack from an unrelated passive proc on a resource, guard or field card.
            switch (card.SchoolEffect?.Mechanic)
            {
                case CoreSchoolMechanic.FireExploit:
                case CoreSchoolMechanic.ArcaneRelease:
                case CoreSchoolMechanic.CurseExploit:
                case CoreSchoolMechanic.CurseDetonate:
                case CoreSchoolMechanic.TideRiposte:
                case CoreSchoolMechanic.ThunderPrime:
                case CoreSchoolMechanic.ThunderChain:
                case CoreSchoolMechanic.ThunderFinish:
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodExecute: return true;
                default: return false;
            }
        }

        public static bool TryEnemy(string schoolId, CoreBoardingEnemyActionResult action,
            out BattleSchoolAttackKind school, out BattleSchoolAttackOutcome outcome)
        {
            outcome = Outcome(action?.HealthLost ?? 0, action?.AbsorbedDamage ?? 0);
            return TrySchool(schoolId, out school) && action != null && action.RawAttack > 0;
        }

        public static bool TryResolved(CoreBoardingResolvedAttack attack,
            out BattleSchoolAttackKind school, out BattleSchoolAttackOutcome outcome)
        {
            outcome = Outcome(attack.HealthLost, attack.GuardAbsorbed);
            return TrySchool(attack.SourceSchoolId, out school);
        }
    }

    public sealed partial class CoreGameplayDemoController
    {
        private BattleSchoolAttackGraphic activeBattleSchoolAttack;
        private CoreBoardingBattleState observedResolvedAttackState;
        private long consumedResolvedAttackSequence;

        private void CancelBattleSchoolAttackPresentation()
        {
            if (activeBattleSchoolAttack != null) activeBattleSchoolAttack.Cancel();
            activeBattleSchoolAttack = null;
        }

        // Pass the sequence captured immediately before the successful PlayCard/EndBattleTurn.
        // Claim the whole committed batch before yielding, so cancellation cannot replay it.
        private IEnumerator AnimateResolvedSchoolAttacks(RectTransform stage, RectTransform source, RectTransform target,
            Image targetHealth, CoreBoardingBattleState state, long afterSequence, int maximumHealth)
        {
            if (state == null) yield break;
            if (observedResolvedAttackState != state)
            {
                observedResolvedAttackState = state;
                consumedResolvedAttackSequence = afterSequence;
            }
            long threshold = System.Math.Max(afterSequence, consumedResolvedAttackSequence);
            var attacks = new List<CoreBoardingResolvedAttack>();
            foreach (CoreBoardingResolvedAttack attack in state.ResolvedAttacks)
                if (attack.Sequence > threshold) attacks.Add(attack);
            consumedResolvedAttackSequence = System.Math.Max(consumedResolvedAttackSequence, state.ResolvedAttackSequence);
            foreach (CoreBoardingResolvedAttack attack in attacks)
            {
                if (!isActiveAndEnabled || stage == null || !stage.gameObject.activeInHierarchy || source == null || target == null) yield break;
                string result = attack.HealthLost > 0 ? "-" + attack.HealthLost : attack.GuardAbsorbed > 0 ? "格挡" : "未命中";
                if (attack.HealthBefore - attack.HealthAfter > attack.HealthLost)
                    result = attack.HealthLost > 0 ? "-" + attack.HealthLost + " · 咒印消灭" : "咒印消灭";
                bool schoolAttack = BattleSchoolAttackPresentation.TryResolved(attack, out var school, out var outcome);
                source.GetComponent<BoardingPartyPresentation>()?.PlaySource(attack.SourceCardId, null,
                    BoardingCardActorPresentation.ForResolved(attack.SourceCardId, attack.SourceSchoolId));
                Coroutine bars = StartCoroutine(schoolAttack
                    ? AnimateSchoolContactDamage(targetHealth, attack.HealthBefore, attack.HealthAfter, maximumHealth, attack.GuardBefore, attack.GuardAfter, stage, true)
                    : AnimateBattleDamage(targetHealth, attack.HealthBefore, attack.HealthAfter, maximumHealth, attack.GuardBefore, attack.GuardAfter, stage));
                if (schoolAttack)
                    yield return AnimateSchoolAttack(stage, source, target, school, outcome, true, result);
                else
                {
                    yield return AnimateImpact(stage, target, Coral, result, "Delayed Attack Hit");
                }
                yield return bars;
            }
        }

        // Replaces the generic attack contact sequence; damage-bar animation can run alongside it.
        // The caller supplies the already committed result text. This routine never reads rules.
        private IEnumerator AnimateSchoolAttack(RectTransform stage, RectTransform source, RectTransform target,
            BattleSchoolAttackKind school, BattleSchoolAttackOutcome outcome, bool boarding, string resultLabel)
        {
            if (!isActiveAndEnabled || stage == null || !stage.gameObject.activeInHierarchy || source == null || target == null) yield break;
            CancelBattleSchoolAttackPresentation();
            RectTransform root = CreateRect("Battle School Attack " + school, stage, stage.rect.size, Vector2.zero);
            var graphic = root.gameObject.AddComponent<BattleSchoolAttackGraphic>();
            activeBattleSchoolAttack = graphic;
            graphic.Configure(school, outcome, boarding ? BoardingActionOrigin(stage, source) : BattleEffectPosition(stage, source),
                BattleEffectPosition(stage, target), boarding);
            if (boarding) graphic.ConfigureBoardingEnvironment(BoardingSchoolGround(stage, source), BoardingSchoolGround(stage, target), stage.rect.yMax - 12f);
            Text result = CreateText("School Attack Result", root, new Vector2(320f, 56f), Vector2.zero,
                30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.Lerp(BattleSchoolAttackGraphic.Tint(school), Color.white, .3f));
            result.text = resultLabel ?? string.Empty;
            AddSceneLabelOutline(result);
            graphic.BindResult(result);
            graphic.Play(this);
            bool sounded = false;
            try
            {
                while (graphic != null && graphic.IsPlaying)
                {
                    if (!sounded && graphic.Progress >= graphic.ContactAt)
                    {
                        sounded = true;
                        if (boarding) target.GetComponent<BoardingPartyPresentation>()?.React(outcome);
                        if (outcome != BattleSchoolAttackOutcome.Missed)
                        {
                            PlaySfx(outcome == BattleSchoolAttackOutcome.Guarded ? "battle_guard" : boarding ? "battle_melee_impact" : "battle_impact", .78f);
                        }
                    }
                    yield return null;
                }
            }
            finally
            {
                if (graphic != null) graphic.Cancel();
                if (activeBattleSchoolAttack == graphic) activeBattleSchoolAttack = null;
            }
        }
    }
}
