using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly List<NavalSupplementFxGraphic> navalSupplementEffects = new List<NavalSupplementFxGraphic>();

        private NavalSupplementFxGraphic CreateNavalSupplementFx(RectTransform stage, string name,
            NavalSupplementFxKind kind, Vector2 from, Vector2 to, float duration, Color tint)
        {
            RectTransform root = CreateRect(name + " Remastered Naval VFX", stage, stage.rect.size, Vector2.zero);
            NavalSupplementFxGraphic graphic = root.gameObject.AddComponent<NavalSupplementFxGraphic>();
            graphic.Configure(kind, from, to, duration, tint);
            navalSupplementEffects.Add(graphic);
            return graphic;
        }

        private void ReleaseNavalSupplementFx(NavalSupplementFxGraphic graphic)
        {
            navalSupplementEffects.Remove(graphic);
            if (graphic != null) { graphic.gameObject.SetActive(false); Destroy(graphic.gameObject); }
        }

        private void CancelNavalSupplementVfx()
        {
            for (int i = navalSupplementEffects.Count - 1; i >= 0; i--)
                if (navalSupplementEffects[i] != null)
                {
                    navalSupplementEffects[i].gameObject.SetActive(false);
                    Destroy(navalSupplementEffects[i].gameObject);
                }
            navalSupplementEffects.Clear();
        }

        private IEnumerator AnimateNavalSupplementProjectile(RectTransform stage, Vector2 from, Vector2 to,
            Color tint, string name, bool playSound)
        {
            if (stage == null) yield break;
            if (playSound) PlaySfx("battle_cannon_fire", .92f);
            // Preserve the tutorial/legacy travel interval: its action commits after this flight.
            float duration = Mathf.Lerp(.30f, .46f, Mathf.InverseLerp(350f, 1100f, Vector2.Distance(from, to)));
            NavalSupplementFxGraphic graphic = CreateNavalSupplementFx(stage, name, NavalSupplementFxKind.Projectile,
                from, to, duration, tint);
            bool dispersing = false;
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (stage == null || graphic == null) yield break;
                    graphic.SetElapsed(elapsed);
                    yield return null;
                }
                if (graphic != null)
                {
                    graphic.SetElapsed(duration);
                    StartCoroutine(DisperseNavalSupplementSmoke(graphic, duration));
                    dispersing = true;
                }
            }
            finally { if (!dispersing) ReleaseNavalSupplementFx(graphic); }
        }

        private IEnumerator DisperseNavalSupplementSmoke(NavalSupplementFxGraphic graphic, float flightDuration)
        {
            try
            {
                for (float elapsed = 0f; elapsed < .26f; elapsed += Time.unscaledDeltaTime)
                {
                    if (graphic == null) yield break;
                    graphic.SetElapsed(flightDuration + elapsed);
                    yield return null;
                }
            }
            finally { ReleaseNavalSupplementFx(graphic); }
        }

        private IEnumerator AnimateNavalSupplementImpact(RectTransform stage, RectTransform target,
            Color tint, string label, string name)
        {
            if (stage == null || target == null) yield break;
            string impactSound = name.Contains("Melee") ? "battle_melee_impact" :
                name.Contains("Ram") ? "battle_ram_impact" : "battle_impact";
            PlaySfx(impactSound, .88f);
            BattleShipIdleMotion idle = target.GetComponent<BattleShipIdleMotion>();
            bool idleWasEnabled = idle != null && idle.enabled;
            if (idle != null) idle.enabled = false;
            Vector2 targetOrigin = target.anchoredPosition;
            Vector2 origin = BattleEffectPosition(stage, target) + Vector2.down *
                (name.Contains("Melee") ? 10f : target.rect.height * target.localScale.y * .23f);
            if (stage.GetComponentInChildren<NavalTacticalBoardView>() != null) origin = BattleEffectPosition(stage, target);
            NavalSupplementFxKind kind = label == "未命中" ? NavalSupplementFxKind.Missed :
                label == "格挡" ? NavalSupplementFxKind.Guarded : NavalSupplementFxKind.Impact;
            const float duration = .26f;
            NavalSupplementFxGraphic graphic = CreateNavalSupplementFx(stage, name, kind, origin, origin, duration, tint);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage, origin + Vector2.up * 95f, label, tint, name + " Float"));
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (target == null || graphic == null) yield break;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float shake = (1f - t) * (1f - t) * 9f;
                    target.anchoredPosition = targetOrigin + Vector2.right * (Mathf.Sin(t * 48f) * shake);
                    FollowBattleShipWaterHorizontal(target);
                    graphic.SetElapsed(elapsed);
                    yield return null;
                }
            }
            finally
            {
                if (target != null) { target.anchoredPosition = targetOrigin; FollowBattleShipWaterHorizontal(target); }
                if (idle != null) idle.enabled = idleWasEnabled;
                ReleaseNavalSupplementFx(graphic);
            }
            yield return number;
        }

        private IEnumerator AnimateNavalSupplementAura(RectTransform stage, RectTransform target, Color tint,
            string label, string name, float labelHeight)
        {
            if (stage == null || target == null) yield break;
            bool repair = name.Contains("Repair");
            PlaySfx(repair ? "battle_repair" : "battle_guard", .78f);
            Vector2 origin = BattleEffectPosition(stage, target);
            NavalSupplementFxKind kind = repair ? NavalSupplementFxKind.Heal : name.Contains("Shield")
                ? NavalSupplementFxKind.Guard : name.Contains("Curse") ? NavalSupplementFxKind.Curse : NavalSupplementFxKind.Aura;
            const float duration = .36f;
            NavalSupplementFxGraphic graphic = CreateNavalSupplementFx(stage, name, kind, origin, origin, duration, tint);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage, origin + Vector2.up * labelHeight, label, tint, name + " Float"));
            try
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    if (graphic == null || target == null) yield break;
                    graphic.SetElapsed(elapsed);
                    yield return null;
                }
            }
            finally { ReleaseNavalSupplementFx(graphic); }
            yield return number;
        }
    }

    public enum NavalSupplementFxKind { Projectile, Impact, Missed, Guarded, Guard, Heal, Curse, Aura }

    /// <summary>Shared remastered art for legacy naval cues; the existing coroutine owns all timing.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class NavalSupplementFxGraphic : MaskableGraphic
    {
        private Texture2D atlas;
        private NavalSupplementFxKind kind;
        private Vector2 from, to;
        private float elapsed, duration;
        private Color accent;
        private struct ClippedVertex
        {
            public Vector2 Position, Uv;
            public ClippedVertex(Vector2 position, Vector2 uv) { Position = position; Uv = uv; }
        }
        private readonly ClippedVertex[] clipA = new ClippedVertex[12];
        private readonly ClippedVertex[] clipB = new ClippedVertex[12];
        public override Texture mainTexture => atlas != null ? atlas : Texture2D.whiteTexture;

        public void Configure(NavalSupplementFxKind effectKind, Vector2 source, Vector2 target, float seconds, Color tint)
        {
            atlas = NavalVfxArtCatalog.Load();
            kind = effectKind; from = source; to = target; duration = Mathf.Max(.01f, seconds); accent = tint;
            elapsed = 0f; raycastTarget = false; color = Color.white;
            SetAllDirty();
        }

        public void SetElapsed(float seconds)
        {
            elapsed = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0f, seconds);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (atlas == null || elapsed <= 0f) return;
            if (kind == NavalSupplementFxKind.Projectile) { Projectile(mesh); return; }
            float t = Mathf.Clamp01(elapsed / duration);
            if (t >= 1f) return;
            float pulse = Mathf.Sin(t * Mathf.PI), fade = Mathf.Pow(1f - t, 1.35f);
            if (kind == NavalSupplementFxKind.Impact || kind == NavalSupplementFxKind.Missed || kind == NavalSupplementFxKind.Guarded)
            {
                Quad(mesh, NavalVfxArtCatalog.StaticUv(15), to + Vector2.down * 28f,
                    new Vector2(180f, 85f) * (.65f + t * .65f), 0f, White(pulse * .7f));
                if (kind == NavalSupplementFxKind.Missed)
                    Quad(mesh, NavalVfxArtCatalog.SequenceUv(2, t), to + Vector2.down * 20f,
                        new Vector2(190f, 165f), 0f, White(fade));
                else if (kind == NavalSupplementFxKind.Guarded)
                {
                    Quad(mesh, NavalVfxArtCatalog.StaticUv(10), to, Vector2.one * (160f + t * 25f), 0f, White(fade));
                    // A blocked projectile flashes against the shield without hull splinters.
                    Quad(mesh, NavalVfxArtCatalog.StaticUv(0), to, Vector2.one * (80f + t * 40f), 12f,
                        new Color(.68f, .9f, 1f, fade * .85f));
                }
                else
                {
                    Quad(mesh, NavalVfxArtCatalog.SequenceUv(1, t), to, new Vector2(210f, 180f), 0f, White(fade));
                    Quad(mesh, NavalVfxArtCatalog.StaticUv(13), to, Vector2.one * (110f + t * 55f), 18f, White(fade * .6f));
                    Quad(mesh, NavalVfxArtCatalog.SequenceUv(2, t), to + Vector2.down * 36f,
                        new Vector2(185f, 100f), 0f, White(pulse * .6f));
                }
                return;
            }
            Quad(mesh, NavalVfxArtCatalog.StaticUv(15), to + Vector2.down * 45f,
                new Vector2(230f, 95f) * (.7f + t * .4f), 0f, new Color(accent.r, accent.g, accent.b, pulse * .65f));
            if (kind == NavalSupplementFxKind.Curse)
                Quad(mesh, NavalVfxArtCatalog.SequenceUv(5, t), to, Vector2.one * 205f, 0f, White(pulse * .9f));
            else
            {
                int slot = kind == NavalSupplementFxKind.Heal ? 11 : kind == NavalSupplementFxKind.Guard ? 10 : 14;
                Quad(mesh, NavalVfxArtCatalog.StaticUv(slot), to + Vector2.up * (kind == NavalSupplementFxKind.Heal ? t * 18f : 0f),
                    new Vector2(190f, 205f) * (.78f + t * .22f), kind == NavalSupplementFxKind.Aura ? -t * 35f : 0f, White(pulse * .9f));
                if (kind == NavalSupplementFxKind.Heal)
                    for (int i = 0; i < 5; i++)
                    {
                        float u = Mathf.Clamp01(t * 1.5f - i * .12f);
                        Quad(mesh, NavalVfxArtCatalog.StaticUv(12), to + new Vector2(-50f + i * 25f, -45f + u * 105f),
                            new Vector2(27f, 9f), 90f, White(Mathf.Sin(u * Mathf.PI) * .55f));
                    }
            }
        }

        private void Projectile(VertexHelper mesh)
        {
            Vector2 direction = (to - from).normalized;
            float heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float t = Mathf.Clamp01(elapsed / duration);
            float smokeAlpha = elapsed <= duration ? .52f * Mathf.Clamp01(elapsed / .06f) :
                .52f * (1f - Mathf.Clamp01((elapsed - duration) / .26f));
            Quad(mesh, NavalVfxArtCatalog.StaticUv(2), from - direction * (t * 28f) + Vector2.up * t * 25f,
                new Vector2(140f, 105f) * (.55f + Mathf.Min(1.4f, elapsed / duration) * .6f), -heading * .15f, White(smokeAlpha));
            if (elapsed >= duration) return;
            if (elapsed < .19f)
                Quad(mesh, NavalVfxArtCatalog.SequenceUv(0, elapsed / .19f), from + direction * 40f,
                    new Vector2(150f, 105f), heading, White(1f - elapsed / .19f));
            Vector2 midpoint = (from + to) * .5f + Vector2.up * 34f;
            Vector2 first = Vector2.Lerp(from, midpoint, t), second = Vector2.Lerp(midpoint, to, t);
            Vector2 position = Vector2.Lerp(first, second, t);
            Vector2 travelDirection = (second - first).normalized;
            float angle = Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;
            Quad(mesh, NavalVfxArtCatalog.StaticUv(12), position - travelDirection * 32f,
                new Vector2(90f, 23f), angle, White(.9f));
        }

        private void Quad(VertexHelper mesh, Rect uv, Vector2 center, Vector2 size, float degrees, Color tint)
        {
            if (tint.a <= .002f) return;
            float angle = degrees * Mathf.Deg2Rad;
            Vector2 x = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size.x * .5f;
            Vector2 y = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * size.y * .5f;
            clipA[0] = new ClippedVertex(center - x - y, new Vector2(uv.xMin, uv.yMin));
            clipA[1] = new ClippedVertex(center - x + y, new Vector2(uv.xMin, uv.yMax));
            clipA[2] = new ClippedVertex(center + x + y, new Vector2(uv.xMax, uv.yMax));
            clipA[3] = new ClippedVertex(center + x - y, new Vector2(uv.xMax, uv.yMin));
            Rect bounds = rectTransform.rect;
            int count = 4;
            ClippedVertex[] input = clipA, output = clipB;
            for (int edge = 0; edge < 4 && count > 0; edge++)
            {
                int nextCount = 0;
                ClippedVertex previous = input[count - 1];
                float previousDistance = ClipDistance(previous.Position, bounds, edge);
                for (int i = 0; i < count; i++)
                {
                    ClippedVertex current = input[i];
                    float currentDistance = ClipDistance(current.Position, bounds, edge);
                    if ((currentDistance >= 0f) != (previousDistance >= 0f))
                    {
                        float amount = previousDistance / (previousDistance - currentDistance);
                        output[nextCount++] = new ClippedVertex(Vector2.LerpUnclamped(previous.Position, current.Position, amount),
                            Vector2.LerpUnclamped(previous.Uv, current.Uv, amount));
                    }
                    if (currentDistance >= 0f) output[nextCount++] = current;
                    previous = current; previousDistance = currentDistance;
                }
                count = nextCount;
                ClippedVertex[] swap = input; input = output; output = swap;
            }
            if (count < 3) return;
            int first = mesh.currentVertCount;
            tint *= color;
            for (int i = 0; i < count; i++) mesh.AddVert(input[i].Position, tint, input[i].Uv);
            for (int i = 1; i < count - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private static float ClipDistance(Vector2 point, Rect bounds, int edge) => edge == 0 ? point.x - bounds.xMin :
            edge == 1 ? bounds.xMax - point.x : edge == 2 ? point.y - bounds.yMin : bounds.yMax - point.y;
        private static Color White(float alpha) => new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    }
}
