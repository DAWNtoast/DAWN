using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void ConfigureBattleDefenses(RectTransform stage, CoreBattleState battle)
        {
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            ConfigureBattleDefense("Player", stage, boarding ? battle.PlayerBoardingHealth : session.Hull,
                boarding ? battle.PlayerMaxBoardingHealth : session.MaxHull, battle.PlayerBlock, boarding ? 0 : session.Armor, boarding);
            ConfigureBattleDefense("Enemy", stage, boarding ? battle.EnemyBoardingHealth : battle.EnemyHull,
                boarding ? battle.EnemyMaxBoardingHealth : battle.EnemyMaxHull, battle.EnemyBlock, 0, boarding);
        }

        private void ConfigureBattleDefense(string side, RectTransform stage, int current, int maximum, int guard, int armor, bool boarding)
        {
            Transform track = stage.Find(side + " Battle Hull Bar Track");
            if (track == null) return;
            Image hull = track.Find(side + " Battle Hull Bar")?.GetComponent<Image>();
            if (hull == null) return;
            Vector2 size = hull.rectTransform.sizeDelta;
            Image chip = CreateImage(side + " Hull Damage Trail", track, size, Vector2.zero, new Color(1f, 0.78f, 0.43f));
            chip.sprite = BattleDefenseHud.FillSprite;
            chip.type = Image.Type.Filled; chip.fillMethod = Image.FillMethod.Horizontal;
            chip.raycastTarget = false; chip.transform.SetSiblingIndex(hull.transform.GetSiblingIndex());
            Image[] frame = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                Vector2 edgeSize = horizontal ? new Vector2(size.x + 20f, 2f) : new Vector2(2f, size.y + 20f);
                Vector2 edgePosition = horizontal ? new Vector2(0f, (i == 0 ? 1f : -1f) * (size.y * 0.5f + 10f))
                    : new Vector2((i == 2 ? -1f : 1f) * (size.x * 0.5f + 10f), 0f);
                frame[i] = CreateImage(side + " Armor Frame " + i, track, edgeSize, edgePosition, Color.white);
                frame[i].raycastTarget = false;
            }
            Image top = CreateImage(side + " Armor Bar", track, new Vector2(size.x + 10f, 4f), new Vector2(0f, size.y * 0.5f + 5f), Color.white);
            Image bottom = CreateImage(side + " Armor Bar Lower", track, new Vector2(size.x + 10f, 4f), new Vector2(0f, -size.y * 0.5f - 5f), Color.white);
            foreach (Image rail in new[] { top, bottom })
            {
                rail.sprite = BattleDefenseHud.FillSprite;
                rail.type = Image.Type.Filled; rail.fillMethod = Image.FillMethod.Horizontal; rail.raycastTarget = false;
            }
            Text hp = CreateText(side + " Hull Numeric", track, new Vector2(120f, 24f),
                new Vector2(-size.x * .5f - 68f, 0f), 14, FontStyle.Bold, TextAnchor.MiddleRight, Color.white);
            hp.raycastTarget = false;
            hp.color = Color.white; // This label sits outside the track, directly over the scenery.
            AddSceneLabelOutline(hp);
            Text defense = CreateText(side + " Armor Numeric", track,
                boarding ? new Vector2(140f, 24f) : new Vector2(310f, 24f),
                boarding ? new Vector2(size.x * .5f + 78f, 0f) : new Vector2(0f, 34f),
                boarding ? 14 : 16, FontStyle.Bold, boarding ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Color.white);
            defense.raycastTarget = false;
            AddSceneLabelOutline(defense);
            track.gameObject.AddComponent<BattleDefenseHud>().Configure(hull, chip, top, bottom, frame, hp, defense, current, maximum, guard, armor);
        }

        private BattleFxGraphic CreateBattleFx(string name, Transform parent, BattleFxKind kind, Vector2 size, Vector2 position, Color tint, int seed = 7)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            BattleFxGraphic fx = rect.gameObject.AddComponent<BattleFxGraphic>();
            fx.Configure(kind, tint, seed);
            return fx;
        }

        private IEnumerator PlayBattleFx(BattleFxGraphic fx, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (fx == null) yield break;
                fx.Progress = elapsed / duration;
                yield return null;
            }
            if (fx != null) Destroy(fx.gameObject);
        }

        private IEnumerator AnimateBattleGuard(Image hull, int before, int after, RectTransform stage, bool absorbed = true)
        {
            BattleDefenseHud hud = hull == null ? null : hull.GetComponentInParent<BattleDefenseHud>();
            if (hud == null) yield break;
            Coroutine fracture = null;
            if (before > after && absorbed)
            {
                RectTransform party = stage.Find(hull.name.StartsWith("Enemy", System.StringComparison.Ordinal)
                    ? "Enemy Ship Art" : "Player Ship Art") as RectTransform;
                if (IsBoardingVisualTarget(party))
                {
                    BoardingCombatFxGraphic shards = CreateBoardingCombatFx(hull.name + " Boarding Armor Break",
                        stage, party, BoardingCombatFxKind.GuardBreak, Color.white);
                    fracture = StartCoroutine(PlayBoardingCombatFx(shards, .42f));
                }
                else
                {
                    BattleFxGraphic shards = CreateBattleFx(hull.name + " Armor Break", stage, BattleFxKind.ArmorBreak,
                        new Vector2(380f, 100f), BattleEffectPosition(stage, hull.rectTransform), Color.white);
                    fracture = StartCoroutine(PlayBattleFx(shards, 0.42f));
                }
            }
            const float duration = 0.22f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (hud == null) yield break;
                hud.SetGuard(Mathf.Lerp(before, after, EaseOutCubic(elapsed / duration)));
                yield return null;
            }
            hud.SetGuard(after);
            if (fracture != null) yield return fracture;
        }

        private IEnumerator AnimateBattleDamage(Image hull, int before, int after, int maximum,
            int guardBefore, int guardAfter, RectTransform stage)
        {
            Coroutine guard = guardBefore != guardAfter ? StartCoroutine(AnimateBattleGuard(hull, guardBefore, guardAfter, stage)) : null;
            // Let the white layer absorb the first instant of the impact, then
            // drain hull while its shards and the target's hit effect are alive.
            if (guard != null)
                for (float delay = 0f; delay < 0.12f; delay += Time.unscaledDeltaTime) yield return null;
            if (before != after) yield return AnimateHealthBar(hull, before, after, maximum, Coral);
            if (guard != null) yield return guard;
        }

        private IEnumerator ReleaseBattleCard(RectTransform card, RectTransform stage, RectTransform target, Color accent)
        {
            if (card == null) yield break;
            Vector2 origin = BattleEffectPosition(stage, card);
            Vector2 end = BattleEffectPosition(stage, target) + Vector2.down * 35f;
            Vector2 bend = (origin + end) * 0.5f + Vector2.up * 100f;
            CanvasGroup group = GetOrAddCanvasGroup(card.gameObject);
            BattleFxGraphic trail = CreateBattleFx("Battle Card Cast Trail", stage, BattleFxKind.Trail, new Vector2(180f, 65f), origin, accent);
            for (float elapsed = 0f; elapsed < 0.12f; elapsed += Time.unscaledDeltaTime)
            {
                if (card == null || trail == null) yield break;
                float t = Mathf.Clamp01(elapsed / 0.12f);
                float travel = t * t;
                Vector2 pos = Vector2.Lerp(Vector2.Lerp(origin, bend, travel), Vector2.Lerp(bend, end, travel), travel);
                card.anchoredPosition = pos;
                card.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.13f, travel);
                card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, end.x < origin.x ? 16f : -16f, t));
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                Vector2 dir = (end - origin).normalized;
                trail.rectTransform.anchoredPosition = pos - dir * 65f;
                trail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                trail.Progress = t * 0.85f;
                yield return null;
            }
            if (trail != null) Destroy(trail.gameObject);
            if (card != null) card.gameObject.SetActive(false);
        }

        private IEnumerator AnimateCannonVolley(RectTransform stage, RectTransform source, RectTransform target, string name, bool fireRounds = false)
        {
            BattleShipIdleMotion idle = source.GetComponent<BattleShipIdleMotion>();
            bool wasIdle = idle != null && idle.enabled;
            if (idle != null) idle.enabled = false;
            Vector2 origin = source.anchoredPosition;
            Quaternion heading = source.localRotation;
            float direction = DirectionToward(source, target);
            Vector2 from = BattleEffectPosition(stage, source) + new Vector2(direction * source.rect.width * Mathf.Abs(source.localScale.x) * 0.23f, -source.rect.height * source.localScale.y * 0.24f);
            Vector2 to = BattleEffectPosition(stage, target) + Vector2.down * (target.rect.height * target.localScale.y * 0.23f);

            // A loaded fire round is shown as a short staggered salvo. The core
            // still resolves one card and one ammo cost; this is visual only,
            // with the first projectile carrying the cannon-fire sound.
            int salvoCount = fireRounds ? 3 : 1;
            Coroutine[] shots = new Coroutine[salvoCount];
            for (int index = 0; index < salvoCount; index++)
            {
                float spread = (index - (salvoCount - 1) * 0.5f) * 16f;
                shots[index] = StartCoroutine(AnimateProjectile(stage,
                    from + Vector2.up * spread,
                    to + Vector2.up * spread * 0.45f,
                    fireRounds ? Gold : Coral,
                    name + " " + (index + 1), index == 0));
                if (index + 1 < salvoCount)
                    yield return new WaitForSecondsRealtime(0.055f);
            }

            // Recoil settles with a damped kick, then the final shot finishes;
            // this keeps cannon fire responsive without freezing the battle UI.
            for (float elapsed = 0f; elapsed < 0.32f; elapsed += Time.unscaledDeltaTime)
            {
                if (source == null) yield break;
                float t = elapsed / 0.32f;
                float recoil = t < 0.2f ? t / 0.2f : 1f - EaseOutCubic((t - 0.2f) / 0.8f);
                source.anchoredPosition = origin - Vector2.right * direction * 12f * recoil;
                source.localRotation = heading * Quaternion.Euler(0f, 0f, -direction * 2.5f * recoil);
                FollowBattleShipWaterHorizontal(source);
                yield return null;
            }
            source.anchoredPosition = origin;
            source.localRotation = heading;
            FollowBattleShipWaterHorizontal(source);
            if (idle != null) idle.enabled = wasIdle;
            for (int index = 0; index < shots.Length; index++)
                if (shots[index] != null) yield return shots[index];
        }
        private IEnumerator AnimateBoardingApproach(RectTransform stage, RectTransform player, RectTransform enemy, NavalBoardingApproach approach)
        {
            PlaySfx("battle_melee", 0.84f);
            // The normal ship-bob component writes anchoredPosition every frame.
            // Suspend it while the connection owns both hull transforms; the
            // final boarding render replaces these objects with the deck scene.
            BattleShipIdleMotion playerIdle = player == null ? null : player.GetComponent<BattleShipIdleMotion>();
            BattleShipIdleMotion enemyIdle = enemy == null ? null : enemy.GetComponent<BattleShipIdleMotion>();
            bool restorePlayerIdle = playerIdle != null && playerIdle.enabled;
            bool restoreEnemyIdle = enemyIdle != null && enemyIdle.enabled;
            if (playerIdle != null) playerIdle.enabled = false;
            if (enemyIdle != null) enemyIdle.enabled = false;
            Vector2 playerStart = player.anchoredPosition, enemyStart = enemy.anchoredPosition;
            float offset = approach == NavalBoardingApproach.Quarter ? 38f : 0f;
            Vector2 playerEnd = new Vector2(-195f, playerStart.y - offset * 0.5f);
            Vector2 enemyEnd = new Vector2(195f, enemyStart.y + offset * 0.5f);
            Image[] ropes = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                ropes[i] = CreateLine(stage, Vector2.zero, Vector2.one, new Color(0.83f, 0.69f, 0.40f), 3f);
                ropes[i].name = "Boarding Approach Rope " + i;
                ropes[i].raycastTarget = false;
            }
            for (float elapsed = 0f; elapsed < 0.85f; elapsed += Time.unscaledDeltaTime)
            {
                if (player == null || enemy == null) yield break;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.85f);
                player.anchoredPosition = Vector2.Lerp(playerStart, playerEnd, t);
                enemy.anchoredPosition = Vector2.Lerp(enemyStart, enemyEnd, t);
                foreach (string side in new[] { "Player", "Enemy" })
                {
                    RawImage hull = (side == "Player" ? player : enemy).GetComponentInChildren<RawImage>();
                    Vector2 contact = stage.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
                    foreach (string effect in new[] { " Shadow", " Contact" })
                    {
                        RectTransform water = stage.Find(side + " Ship Water" + effect) as RectTransform;
                        if (water != null) water.anchoredPosition = contact;
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector2 start = player.anchoredPosition + new Vector2(125f, -60f + i * 25f);
                    Vector2 end = enemy.anchoredPosition + new Vector2(-125f, -60f + i * 25f);
                    RectTransform rope = ropes[i].rectTransform;
                    Vector2 delta = end - start;
                    rope.anchoredPosition = (start + end) * 0.5f;
                    rope.sizeDelta = new Vector2(delta.magnitude * Mathf.Clamp01(t * 3f), 3f);
                    rope.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
                yield return null;
            }
            if (playerIdle != null) playerIdle.enabled = restorePlayerIdle;
            if (enemyIdle != null) enemyIdle.enabled = restoreEnemyIdle;
        }
    }
}
