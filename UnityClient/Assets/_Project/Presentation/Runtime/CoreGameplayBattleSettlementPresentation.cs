using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Claims committed facts before yielding so a redraw, cancellation or duplicate callback cannot replay them.</summary>
    public sealed class BattleSettlementCueCursor
    {
        private CoreBattleState state;
        private long consumed;
        public List<CoreBattleSettlementFact> Claim(CoreBattleState current, long afterSequence)
        {
            var result = new List<CoreBattleSettlementFact>();
            if (current == null) return result;
            if (!ReferenceEquals(state, current)) { state = current; consumed = afterSequence; }
            long threshold = System.Math.Max(consumed, afterSequence);
            foreach (var fact in current.SettlementFacts) if (fact.Sequence > threshold) result.Add(fact);
            consumed = System.Math.Max(consumed, current.SettlementSequence);
            return result;
        }
    }

    public sealed partial class CoreGameplayDemoController
    {
        private readonly BattleSettlementCueCursor battleSettlementCursor = new BattleSettlementCueCursor();
        private readonly List<BattleFxGraphic> battleSettlementSources = new List<BattleFxGraphic>();

        private BattleAfflictionMotion ConfigureBattleAffliction(RectTransform root, bool player, bool boarding)
        {
            string side = player ? "Player" : "Enemy";
            RectTransform target = root.Find(side + " Ship Art") as RectTransform;
            BattleAfflictionMotion motion = root.Find(side + " Affliction Effects")?.GetComponent<BattleAfflictionMotion>();
            if (motion == null)
            {
                var rect = CreateRect(side + " Affliction Effects", root, Vector2.one, Vector2.zero);
                motion = rect.gameObject.AddComponent<BattleAfflictionMotion>();
                if (target != null) rect.SetSiblingIndex(target.GetSiblingIndex() + 1);
            }
            motion.Configure(this, target, boarding);
            return motion;
        }

        private void CancelBattleSettlementPresentation()
        {
            foreach (var source in battleSettlementSources)
                if (source != null) { source.gameObject.SetActive(false); Destroy(source.gameObject); }
            battleSettlementSources.Clear();
            if (retainedBattleRoot == null) return;
            foreach (var motion in retainedBattleRoot.GetComponentsInChildren<BattleAfflictionMotion>(true)) motion.Suspend();
            foreach (var danger in retainedBattleRoot.GetComponentsInChildren<BattleDangerPulse>(true)) danger.Cancel();
        }

        private IEnumerator AnimateBattleSettlementSources(RectTransform stage, List<CoreBattleSettlementFact> facts,
            bool boarding, CoreBattleSettlementWindow? window = null)
        {
            foreach (var fact in facts)
            {
                if (window.HasValue && fact.Window != window.Value) continue;
                if (!isActiveAndEnabled || stage == null || !stage.gameObject.activeInHierarchy) yield break;
                string side = fact.Player ? "Player" : "Enemy";
                RectTransform target = stage.Find(side + " Ship Art") as RectTransform;
                if (target == null) continue;
                string id = fact.Kind == CoreBattleSettlementKind.Burn ? "burn" : fact.Kind == CoreBattleSettlementKind.CurseExecution ? "curse_marks" :
                    fact.Kind == CoreBattleSettlementKind.Recovery ? "healing" : "field_blood";
                Transform source = stage.Find(side + " Battle Status Row/Status " + id) ?? stage.Find(side + " Battle Hull Bar Track");
                Vector2 end = BattleEffectPosition(stage, target);
                Vector2 start = source == null ? end + Vector2.down * 45f : (Vector2)stage.InverseTransformPoint(source.position);
                Color tint = fact.Kind == CoreBattleSettlementKind.Burn ? Coral : fact.Kind == CoreBattleSettlementKind.CurseExecution ? new Color(.73f,.43f,1f) :
                    fact.Kind == CoreBattleSettlementKind.Recovery ? Green : new Color(.93f,.2f,.34f);
                BattleFxGraphic cue = CreateBattleFx("Battle Settlement Source " + fact.Kind, stage, BattleFxKind.RuneRing,
                    new Vector2(42f,42f), start, tint);
                battleSettlementSources.Add(cue);
                try
                {
                    const float travel = .18f;
                    for (float time = 0f; time < travel; time += Time.unscaledDeltaTime)
                    {
                        if (cue == null || target == null || !isActiveAndEnabled) yield break;
                        cue.rectTransform.anchoredPosition = Vector2.Lerp(start, BattleEffectPosition(stage, target), time/travel);
                        cue.Progress = .2f + time/travel * .25f; yield return null;
                    }
                    ConfigureBattleAffliction(stage, fact.Player, boarding).Trigger(fact.Kind);
                    Coroutine health = null, guard = null;
                    // Active attacks already own their contact timeline. End-window effects have their own exact health endpoints.
                    if (fact.Window != CoreBattleSettlementWindow.EnemyAction)
                    {
                        if (boarding && (fact.HealthLost > 0 || fact.GuardLost > 0))
                            target.GetComponent<BoardingPartyPresentation>()?.React(fact.HealthLost > 0 ?
                                BattleSchoolAttackOutcome.Hit : BattleSchoolAttackOutcome.Guarded);
                        BattleDefenseHud hud = stage.Find(side + " Battle Hull Bar Track")?.GetComponent<BattleDefenseHud>();
                        if (hud != null)
                        {
                            if (fact.HealthBefore >= 0 && fact.HealthBefore != fact.HealthAfter)
                                health = StartCoroutine(AnimateHealthBar(hud.HullBar, fact.HealthBefore, fact.HealthAfter,
                                    hud.MaximumHull, fact.Healed > 0 ? Green : Coral));
                            if (fact.GuardLost > 0 && fact.GuardBefore >= 0)
                                guard = StartCoroutine(AnimateBattleGuard(hud.HullBar, fact.GuardBefore, fact.GuardAfter, stage));
                        }
                    }
                    yield return AnimateFloatingText(stage, BattleEffectPosition(stage, target) + Vector2.up * (boarding ? 155f : 90f),
                        BattleSettlementText(fact, boarding), tint, "Battle Settlement " + fact.Sequence);
                    if (health != null) yield return health;
                    if (guard != null) yield return guard;
                }
                finally
                {
                    battleSettlementSources.Remove(cue);
                    if (cue != null) Destroy(cue.gameObject);
                }
            }
        }

        public static string BattleSettlementText(CoreBattleSettlementFact fact, bool boarding = false)
        {
            if (fact.Kind == CoreBattleSettlementKind.CurseExecution) return "咒印消灭";
            if (fact.Kind == CoreBattleSettlementKind.Recovery) return "持续恢复 +" + fact.Healed;
            string text = fact.Kind == CoreBattleSettlementKind.BloodField ? "血池" : "燃烧";
            if (fact.GuardLost > 0) text += " · 格挡 -" + fact.GuardLost;
            if (fact.HealthLost > 0) text += (boarding ? " · 生命 -" : " · 船体 -") + fact.HealthLost;
            return text;
        }

        private static int SettlementHealthDelta(List<CoreBattleSettlementFact> facts, bool player, CoreBattleSettlementWindow? window = null)
        {
            if (facts == null) return 0;
            long delta = 0;
            foreach (var fact in facts)
                if (fact.Player == player && (!window.HasValue || fact.Window == window.Value)) delta += (long)fact.Healed - fact.HealthLost;
            return (int)System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, delta));
        }
        private static bool SettlementCovers(List<CoreBattleSettlementFact> facts, bool player, int before, int after, CoreBattleSettlementWindow? window = null)
        { return before != after && (long)after - before == SettlementHealthDelta(facts, player, window); }
    }
}
