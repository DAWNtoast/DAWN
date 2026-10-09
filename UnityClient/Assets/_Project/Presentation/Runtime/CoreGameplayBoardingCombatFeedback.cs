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
            return target != null && target.Find(target.name + " Formation Label") != null;
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
            Vector2 origin = party.anchoredPosition;
            BoardingCombatFxGraphic dash = CreateBoardingCombatFx(party.name + " Boarding Dash", stage,
                party, BoardingCombatFxKind.Dash, Gold, direction);
            // Streaks emerge behind the actual figures; their proportions and
            // facing remain unchanged throughout the forward step.
            dash.transform.SetSiblingIndex(party.GetSiblingIndex());
            const float duration = .25f;
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (party == null || dash == null) yield break;
                    float t = Mathf.Clamp01(elapsed / duration);
                    party.anchoredPosition = origin + Vector2.right * direction * (74f * Mathf.Sin(Mathf.PI * t));
                    dash.rectTransform.anchoredPosition = BattleEffectPosition(stage, party);
                    dash.Progress = t;
                    yield return null;
                }
            }
            finally
            {
                if (party != null) party.anchoredPosition = origin;
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
                    float recoil = missed ? 0f : Mathf.Sin(t * 34f) * (1f - t) * (1f - t) * (blocked ? 4f : 8f);
                    target.anchoredPosition = origin + Vector2.right * recoil;
                    hit.Progress = t;
                    yield return null;
                }
            }
            finally
            {
                if (target != null) target.anchoredPosition = origin;
                if (hit != null) Destroy(hit.gameObject);
            }
            if (number != null) yield return number;
        }

        private IEnumerator AnimateBoardingAura(RectTransform stage, RectTransform target, Color accent,
            string label, string name, float labelHeight)
        {
            if (stage == null || target == null) yield break;
            bool heal = name.Contains("Repair");
            PlaySfx(heal ? "battle_repair" : "battle_guard", .78f);
            BoardingCombatFxGraphic aura = CreateBoardingCombatFx(name + " Boarding Aura", stage, target,
                heal ? BoardingCombatFxKind.Heal : BoardingCombatFxKind.Guard, heal ? Green : Color.white);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage,
                BattleEffectPosition(stage, target) + Vector2.up * labelHeight, label, accent, name + " Float"));
            yield return PlayBoardingCombatFx(aura, heal ? .56f : .42f);
            if (number != null) yield return number;
        }
    }
}
