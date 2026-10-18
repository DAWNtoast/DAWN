using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private BattleResolutionMotion battleResolutionMotion;
        private bool HasBattleResolutionPresentation => battleResolutionMotion != null;

        private IEnumerator AnimateBattleResolutionVisual(RectTransform stage, RectTransform target, string message)
        {
            CancelBattleResolutionPresentation();
            if (stage == null || target == null || !isActiveAndEnabled) yield break;
            BattleResolutionKind kind = BattleResolutionMotion.DisplayedKind(target);
            bool victory = message.Contains("胜利") || message.Contains("敌舰");
            PlaySfx(victory ? "battle_victory" : kind == BattleResolutionKind.NavalSinking ? "battle_sunk" : "battle_melee_impact", .9f);
            RectTransform host = CreateRect("Battle Resolution Effects", stage, Vector2.one, Vector2.zero);
            BattleResolutionMotion motion = host.gameObject.AddComponent<BattleResolutionMotion>();
            battleResolutionMotion = motion;
            string side = target.name.Contains("Enemy") ? "Enemy" : "Player";
            RectTransform water = stage.Find(side + " Ship Water Contact") as RectTransform;
            motion.Configure(stage, target, this, kind, water);
            float bannerY = kind == BattleResolutionKind.NavalSinking ? -130f : 70f;
            Text banner = CreateText("Battle Resolution Banner", host,
                new Vector2(760f, message.Contains("\n") ? 96f : 72f), new Vector2(0f, bannerY) - host.anchoredPosition,
                32, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            banner.raycastTarget = false;
            banner.text = message;
            AddSceneLabelOutline(banner);
            motion.AttachBanner(banner);
            while (motion != null && motion.IsPlaying)
            {
                motion.AdvancePresentation(Mathf.Min(.05f, Time.unscaledDeltaTime));
                yield return null;
            }
            // The already-committed result is rendered by the existing caller. Keep the
            // final hidden target until that render, so it cannot pop back for one frame.
        }

        private void CancelBattleResolutionPresentation()
        {
            BattleResolutionMotion motion = battleResolutionMotion;
            battleResolutionMotion = null;
            if (motion == null) return;
            motion.CancelAndRestore();
            motion.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(motion.gameObject);
            else DestroyImmediate(motion.gameObject);
        }
    }
}
