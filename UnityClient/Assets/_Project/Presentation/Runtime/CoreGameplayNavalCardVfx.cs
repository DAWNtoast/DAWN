using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly List<NavalCombatFxGraphic> activeNavalCardVfx = new List<NavalCombatFxGraphic>(6);

        private sealed class NavalCardVfxContext
        {
            public NavalCardVfxPlan Plan;
            public Vector2 From, To, ReleasePoint;
            public float CellSize;
            public bool DeferEnemyResult;
            public readonly List<Vector2> Cells = new List<Vector2>();
        }

        // Freeze the authored target before committing. Explosions can remove their own
        // fields, and movement can change the camera, so post-commit inference is too late.
        private NavalCardVfxContext CaptureNavalCardVfx(RectTransform stage, CoreBattleCard card, int handIndex,
            CoreNavalCardPlayOptions options, CoreBattleState battle, RectTransform player, RectTransform enemy)
        {
            var context = new NavalCardVfxContext
            {
                Plan = NavalCardVfxPlan.For(card, battle.Schools.ThunderChain),
                From = BattleEffectPosition(stage, player), CellSize = 80f,
            };
            context.To = BattleEffectPosition(stage, context.Plan.TargetsEnemy ? enemy : player);
            NavalTacticalBoardView board = stage.GetComponentInChildren<NavalTacticalBoardView>();
            if (board != null)
            {
                context.CellSize = board.CellSizePixels;
                if (context.Plan.UsesSelectedArea && options != null && options.HasAnchor)
                {
                    CoreNavalCardChoice choice = session.GetNavalCardChoice(handIndex);
                    int width = choice?.ShapeWidth ?? 2, height = choice?.ShapeHeight ?? 2;
                    for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
                        context.Cells.Add(board.WorldToRoot(new VoyagePoint((options.AnchorX + x) * CoreVoyageSession.NavalGridCellMeters,
                            (options.AnchorY + y) * CoreVoyageSession.NavalGridCellMeters)));
                }
                else if (context.Plan.Role == NavalVfxRole.Area || context.Plan.Role == NavalVfxRole.Field)
                {
                    bool greatWave = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic == CoreNavalExpansionMechanic.GreatWave;
                    foreach (CoreNavalFieldCell cell in battle.NavalFields.Cells)
                    {
                        if (greatWave ? cell.Kind != CoreNavalFieldKind.Tide :
                            !cell.PlayerOwned || !context.Plan.HasSchool || (int)cell.Kind != (int)context.Plan.School) continue;
                        context.Cells.Add(board.WorldToRoot(cell.PositionMeters));
                    }
                }
            }
            if (context.Cells.Count > 0)
            {
                Vector2 center = Vector2.zero;
                foreach (Vector2 cell in context.Cells) center += cell;
                context.To = center / context.Cells.Count;
            }
            context.ReleasePoint = context.To;
            return context;
        }

        private NavalCombatFxGraphic CreateNavalCardVfx(RectTransform stage, NavalVfxRole role,
            BattleSchoolAttackKind school, bool hasSchool, BattleSchoolAttackOutcome outcome, Vector2 from, Vector2 to,
            int salvoCount = 1)
        {
            RectTransform root = CreateRect("Naval VFX " + role + (hasSchool ? " " + school : string.Empty), stage, stage.rect.size, Vector2.zero);
            NavalCombatFxGraphic graphic = root.gameObject.AddComponent<NavalCombatFxGraphic>();
            graphic.Configure(role, school, hasSchool, outcome, from, to, salvoCount);
            activeNavalCardVfx.Add(graphic);
            return graphic;
        }

        private void CancelNavalCardVfx()
        {
            for (int i = activeNavalCardVfx.Count - 1; i >= 0; i--)
                if (activeNavalCardVfx[i] != null) activeNavalCardVfx[i].Cancel();
            activeNavalCardVfx.Clear();
        }

        private IEnumerator PlayNavalCardVfx(NavalCombatFxGraphic graphic, bool sound = false)
        {
            if (graphic == null) yield break;
            graphic.Play(this);
            bool contacted = false;
            if (sound && graphic.Role == NavalVfxRole.Cannon) PlaySfx("battle_cannon_fire", .72f);
            try
            {
                while (graphic != null && graphic.IsPlaying)
                {
                    if (!contacted && graphic.Progress >= graphic.ContactAt)
                    {
                        contacted = true;
                        if (sound && graphic.Outcome != BattleSchoolAttackOutcome.Missed)
                            PlaySfx(graphic.Outcome == BattleSchoolAttackOutcome.Guarded ? "battle_guard" : "battle_impact", .65f);
                    }
                    yield return null;
                }
            }
            finally
            {
                activeNavalCardVfx.Remove(graphic);
                if (graphic != null) graphic.Cancel();
            }
        }

        private IEnumerator AnimateNavalCardVfx(RectTransform stage, RectTransform player, RectTransform enemy, Image enemyBar,
            NavalCardVfxContext context, BattleAnimationSnapshot before, BattleAnimationSnapshot after)
        {
            int damage = Mathf.Max(0, before.EnemyHull - after.EnemyHull);
            int guardLost = Mathf.Max(0, before.EnemyBlock - after.EnemyBlock);
            NavalCardVfxPlan plan = context.Plan;
            // A blank patch of sea still receives the authored area effect. Its damage
            // outcome must not move the explosion back onto an unrelated enemy vessel.
            BattleSchoolAttackOutcome outcome = plan.IsAttack && plan.Role != NavalVfxRole.Area
                ? BattleSchoolAttackPresentation.Outcome(damage, guardLost) : BattleSchoolAttackOutcome.Hit;
            bool loadedFire = !plan.HasSchool && plan.Role == NavalVfxRole.Cannon && before.PlayerFireRounds > 0;
            NavalCombatFxGraphic graphic = CreateNavalCardVfx(stage, plan.Role, loadedFire ? BattleSchoolAttackKind.Fire : plan.School, plan.HasSchool || loadedFire,
                outcome, context.From, context.To, plan.SalvoCount);
            var areaEffects = new List<Coroutine>();
            if (plan.Role == NavalVfxRole.Area || plan.Role == NavalVfxRole.Field)
            {
                // The event ring's 128 entries are not a limit on live fields. Cull only
                // offscreen cells and split visible regions into bounded draw surfaces.
                var visible = new List<Vector2>();
                Rect viewport = stage.rect;
                viewport.xMin -= context.CellSize; viewport.xMax += context.CellSize;
                viewport.yMin -= context.CellSize; viewport.yMax += context.CellSize;
                foreach (Vector2 cell in context.Cells) if (viewport.Contains(cell)) visible.Add(cell);
                graphic.ConfigureAreaTargets(visible, context.CellSize);
                for (int offset = 128; offset < visible.Count; offset += 128)
                {
                    NavalCombatFxGraphic more = CreateNavalCardVfx(stage, plan.Role, plan.School, plan.HasSchool,
                        outcome, context.From, context.To, plan.SalvoCount);
                    more.ConfigureAreaTargets(visible.GetRange(offset, Mathf.Min(128, visible.Count - offset)), context.CellSize);
                    areaEffects.Add(StartCoroutine(PlayNavalCardVfx(more)));
                }
            }
            float contactDelay = graphic.Duration * graphic.ContactAt;
            string result = after.EnemyExecutedByCurse && !before.EnemyExecutedByCurse ? "咒印消灭" :
                damage > 0 ? "-" + damage : guardLost > 0 ? (plan.IsAttack ? "格挡" : "格挡 -" + guardLost) :
                plan.IsAttack && plan.TargetsEnemy ? "未命中" : string.Empty;
            Coroutine numbers = !context.DeferEnemyResult && (result.Length > 0 || before.EnemyBlock != after.EnemyBlock)
                ? StartCoroutine(AnimateNavalContactResult(stage, enemy, enemyBar, before.EnemyHull, after.EnemyHull,
                    before.EnemyMaxHull, before.EnemyBlock, after.EnemyBlock, contactDelay, result, plan.IsAttack)) : null;
            Coroutine ownNumbers = null;
            if (plan.Role == NavalVfxRole.Area && (before.PlayerHull > after.PlayerHull || before.PlayerBlock > after.PlayerBlock))
            {
                Image playerBar = GameObject.Find("Player Battle Hull Bar")?.GetComponent<Image>();
                int ownDamage = Mathf.Max(0, before.PlayerHull - after.PlayerHull);
                ownNumbers = StartCoroutine(AnimateNavalContactResult(stage, player, playerBar, before.PlayerHull, after.PlayerHull,
                    before.PlayerMaxHull, before.PlayerBlock, after.PlayerBlock, contactDelay, ownDamage > 0 ? "船体 -" + ownDamage : "格挡"));
            }
            Coroutine execution = null;
            if (after.EnemyExecutedByCurse && !before.EnemyExecutedByCurse &&
                !(plan.IsAttack && plan.HasSchool && plan.School == BattleSchoolAttackKind.Curse))
            {
                Vector2 point = BattleEffectPosition(stage, enemy);
                execution = StartCoroutine(PlayNavalCardVfx(CreateNavalCardVfx(stage, NavalVfxRole.Affliction,
                    BattleSchoolAttackKind.Curse, true, BattleSchoolAttackOutcome.Hit, point, point)));
            }
            yield return PlayNavalCardVfx(graphic, plan.IsAttack);
            foreach (Coroutine areaEffect in areaEffects) yield return areaEffect;
            if (numbers != null) yield return numbers;
            if (ownNumbers != null) yield return ownNumbers;
            if (execution != null) yield return execution;
        }

        private IEnumerator AnimateNavalContactResult(RectTransform stage, RectTransform target, Image bar, int before, int after,
            int maximum, int guardBefore, int guardAfter, float delay, string result, bool absorbed = true)
        {
            for (float elapsed = 0f; elapsed < delay; elapsed += Time.unscaledDeltaTime) yield return null;
            if (!isActiveAndEnabled || stage == null || target == null) yield break;
            Coroutine guard = guardBefore != guardAfter ? StartCoroutine(AnimateBattleGuard(bar, guardBefore, guardAfter, stage, absorbed)) : null;
            Coroutine health = before != after ? StartCoroutine(AnimateHealthBar(bar, before, after, maximum, Coral)) : null;
            if (!string.IsNullOrEmpty(result))
                yield return AnimateFloatingText(stage, BattleEffectPosition(stage, target) + Vector2.up * 115f,
                    result, Color.white, "Naval VFX Result");
            if (guard != null) yield return guard;
            if (health != null) yield return health;
        }

        private IEnumerator AnimateNavalSupportVfx(RectTransform stage, RectTransform target, NavalVfxRole role,
            NavalCardVfxPlan plan, string result, bool extraGraphic)
        {
            Coroutine effect = null;
            Vector2 point = BattleEffectPosition(stage, target);
            PlaySfx(role == NavalVfxRole.Heal ? "battle_repair" : "battle_guard", .65f);
            if (extraGraphic)
                effect = StartCoroutine(PlayNavalCardVfx(CreateNavalCardVfx(stage, role, plan.School, plan.HasSchool,
                    BattleSchoolAttackOutcome.Hit, point, point)));
            // Healing can start after the guard label has begun rising. Keep it on the lower row
            // so the later label never crosses the earlier guard or strategy feedback.
            yield return AnimateFloatingText(stage, point + Vector2.up * (role == NavalVfxRole.Heal ? 55f : 110f),
                result, role == NavalVfxRole.Heal ? Green : Teal, "Naval VFX Support Result");
            if (effect != null) yield return effect;
        }

        private IEnumerator AnimateNavalSelfDamage(RectTransform stage, RectTransform player, Image playerBar,
            int before, int after, int maximum, NavalCardVfxPlan plan)
        {
            Coroutine effect = null;
            Vector2 point = BattleEffectPosition(stage, player);
            if (plan.HasSchool && plan.School == BattleSchoolAttackKind.Blood)
                effect = StartCoroutine(PlayNavalCardVfx(CreateNavalCardVfx(stage, NavalVfxRole.Empower, plan.School, true,
                    BattleSchoolAttackOutcome.Hit, point, point)));
            yield return AnimateNavalContactResult(stage, player, playerBar, before, after, maximum, 0, 0, .12f,
                "船体 -" + (before - after), false);
            if (effect != null) yield return effect;
        }

        private IEnumerator AnimateNavalHealingResult(RectTransform stage, RectTransform player, Image bar,
            int before, int after, int maximum, NavalCardVfxPlan plan)
        {
            // Attacks and enemy-targeted effects restore hull after contact; self support starts together.
            if (plan.IsAttack || plan.TargetsEnemy)
                yield return new WaitForSecondsRealtime(NavalCombatFxGraphic.ContactTime(plan.Role, plan.SalvoCount));
            Coroutine numbers = StartCoroutine(AnimateHealthBar(bar, before, after, maximum, Green));
            yield return AnimateNavalSupportVfx(stage, player, NavalVfxRole.Heal, plan, "船体 +" + (after - before),
                plan.Role != NavalVfxRole.Heal);
            yield return numbers;
        }

        private IEnumerator AnimateNavalPushResult(RectTransform stage, RectTransform target, Image bar,
            BattleAnimationSnapshot before, BattleAnimationSnapshot after)
        {
            int damage = Mathf.Max(0, before.EnemyHull - after.EnemyHull);
            if (damage <= 0 && before.EnemyBlock == after.EnemyBlock) yield break;
            yield return AnimateNavalContactResult(stage, target, bar, before.EnemyHull, after.EnemyHull, before.EnemyMaxHull,
                before.EnemyBlock, after.EnemyBlock, 0f, damage > 0 ? "-" + damage : "格挡");
        }

        private IEnumerator AnimateNavalEnemyCannon(RectTransform stage, RectTransform source, RectTransform target, Image targetBar,
            int before, int after, int maximum, int guardBefore, int guardAfter)
        {
            int damage = Mathf.Max(0, before - after), guardLost = Mathf.Max(0, guardBefore - guardAfter);
            BattleSchoolAttackOutcome outcome = BattleSchoolAttackPresentation.Outcome(damage, guardLost);
            NavalCombatFxGraphic graphic = CreateNavalCardVfx(stage, NavalVfxRole.Cannon, default, false, outcome,
                BattleEffectPosition(stage, source), BattleEffectPosition(stage, target));
            Coroutine numbers = StartCoroutine(AnimateNavalContactResult(stage, target, targetBar, before, after, maximum,
                guardBefore, guardAfter, graphic.Duration * graphic.ContactAt, damage > 0 ? "-" + damage : guardLost > 0 ? "格挡" : "未命中"));
            yield return PlayNavalCardVfx(graphic, true);
            yield return numbers;
        }
    }
}
