using System.Collections;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Check the rendered object, not the already-committed Core stage: a ram
        // can have entered boarding while the old ship artwork is still visible.
        private static bool IsBoardingVisualTarget(RectTransform target)
        {
            return target != null && target.GetComponent<BoardingPartyPresentation>() != null;
        }

        private static Vector2 BoardingActionOrigin(RectTransform stage, RectTransform party)
        {
            BoardingActorGraphic performer = party.GetComponent<BoardingPartyPresentation>()?.Performer;
            return performer == null ? BattleEffectPosition(stage, party) :
                (Vector2)stage.InverseTransformPoint(performer.transform.position);
        }

        private BoardingCombatFxGraphic CreateBoardingCombatFx(string name, RectTransform stage,
            RectTransform target, BoardingCombatFxKind kind, Color tint, float direction = 1f)
        {
            RectTransform rect = CreateRect(name, stage, new Vector2(400f, 390f), BattleEffectPosition(stage, target));
            BoardingCombatFxGraphic fx = rect.gameObject.AddComponent<BoardingCombatFxGraphic>();
            fx.Configure(kind, tint, direction);
            return fx;
        }

        private IEnumerator PlayBoardingCombatFx(BoardingCombatFxGraphic fx, float duration)
        {
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (fx == null) yield break;
                    fx.Progress = elapsed / duration;
                    yield return null;
                }
                if (fx != null) fx.Progress = 1f;
            }
            finally
            {
                if (fx != null) Destroy(fx.gameObject);
            }
        }

        private IEnumerator AnimateBoardingWindup(RectTransform party, float direction)
        {
            if (party == null || !(party.parent is RectTransform stage)) yield break;
            PlaySfx("battle_melee", .82f);
            BoardingCombatFxGraphic dash = CreateBoardingCombatFx(party.name + " Boarding Dash", stage,
                party, BoardingCombatFxKind.Dash, Gold, direction);
            // Streaks provide the windup while the formation remains grounded.
            dash.transform.SetSiblingIndex(party.GetSiblingIndex());
            const float duration = .25f;
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (party == null || dash == null) yield break;
                    float t = Mathf.Clamp01(elapsed / duration);
                    dash.rectTransform.anchoredPosition = BoardingActionOrigin(stage, party);
                    dash.Progress = t;
                    yield return null;
                }
            }
            finally
            {
                if (dash != null) Destroy(dash.gameObject);
            }
        }

        private IEnumerator AnimateBoardingHit(RectTransform stage, RectTransform target, Color accent, string label, string name)
        {
            if (stage == null || target == null) yield break;
            PlaySfx("battle_melee_impact", .88f);
            Vector2 origin = target.anchoredPosition;
            bool blocked = label == "格挡";
            bool missed = label == "未命中";
            target.GetComponent<BoardingPartyPresentation>()?.React(missed ? BattleSchoolAttackOutcome.Missed :
                blocked ? BattleSchoolAttackOutcome.Guarded : BattleSchoolAttackOutcome.Hit);
            // A miss only shows air streaks; it must not recoil the target
            // or masquerade as an absorbed hit.
            BoardingCombatFxGraphic hit = CreateBoardingCombatFx(name + " Boarding Impact", stage, target,
                blocked ? BoardingCombatFxKind.Guard : missed ? BoardingCombatFxKind.Dash : BoardingCombatFxKind.Slash,
                blocked ? Color.white : accent, origin.x < 0f ? -1f : 1f);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage, BattleEffectPosition(stage, target) + Vector2.up * 145f,
                label, blocked ? Color.white : accent, name + " Float"));
            const float duration = .34f;
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (target == null || hit == null) yield break;
                    float t = Mathf.Clamp01(elapsed / duration);
                    hit.Progress = t;
                    yield return null;
                }
            }
            finally
            {
                if (hit != null) Destroy(hit.gameObject);
            }
            if (number != null) yield return number;
        }

        private IEnumerator AnimateBoardingAura(RectTransform stage, RectTransform target, Color accent,
            string label, string name, float labelHeight, BoardingCombatFxKind kind)
        {
            if (stage == null || target == null) yield break;
            bool heal = kind == BoardingCombatFxKind.Heal;
            if (kind == BoardingCombatFxKind.Curse || kind == BoardingCombatFxKind.LifeCost ||
                kind == BoardingCombatFxKind.Slash)
                target.GetComponent<BoardingPartyPresentation>()?.React(BattleSchoolAttackOutcome.Hit);
            PlaySfx(heal ? "battle_repair" : kind == BoardingCombatFxKind.Guard ? "battle_guard" : "battle_melee_impact", .78f);
            BoardingCombatFxGraphic aura = CreateBoardingCombatFx(name + " Boarding Aura", stage, target,
                kind, heal ? Green : kind == BoardingCombatFxKind.Guard ? Color.white : accent);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage,
                BattleEffectPosition(stage, target) + Vector2.up * labelHeight, label, accent, name + " Float"));
            yield return PlayBoardingCombatFx(aura, heal ? .56f : .42f);
            if (number != null) yield return number;
        }
    }
}
