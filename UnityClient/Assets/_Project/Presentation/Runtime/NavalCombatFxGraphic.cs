using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Atlas-backed naval effects. One UI draw surface owns the complete deterministic
    /// particle timeline; it never owns combat, input, ship transforms or random state.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class NavalCombatFxGraphic : MaskableGraphic
    {
        // Public compatibility path for boarding effects that still sample the v1 4x4 atlas.
        public const string AtlasResource = "BattleVfx/Naval/naval_fx_atlas_v1";
        private static Texture2D atlas;
        private Texture2D liveAtlas;

        /// <summary>Visible effects keep their atlas while the shared lookup is retired.</summary>
        public static void ReleaseLoadedTextures()
        {
            atlas = null;
            NavalVfxArtCatalog.ReleaseLoadedTextures();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => ReleaseLoadedTextures();
        private NavalVfxRole role;
        private BattleSchoolAttackKind school;
        private BattleSchoolAttackOutcome outcome;
        private bool hasSchool, hadOwner, retiring;
        private Behaviour owner;
        private Vector2 start, end;
        private float progress;
        private int shots = 1;
        private Rect bounds;
        private Vector2[] areaTargets;
        private float areaCellSize = 80f;
        // Rotated sprites are clipped with their UVs, not clamped into distorted strips.
        private readonly ClipVertex[] polygonA = new ClipVertex[12];
        private readonly ClipVertex[] polygonB = new ClipVertex[12];

        private struct ClipVertex
        {
            public Vector2 Position, Uv;
            public ClipVertex(Vector2 position, Vector2 uv) { Position = position; Uv = uv; }
        }

        public NavalVfxRole Role => role;
        public BattleSchoolAttackKind School => school;
        public bool HasSchool => hasSchool;
        public BattleSchoolAttackOutcome Outcome => outcome;
        public Vector2 From => start;
        public Vector2 To => end;
        public bool HasExplicitAreaTargets => areaTargets != null;
        public int AreaTargetCount => areaTargets == null ? 0 : areaTargets.Length;
        public float Progress => progress;
        public float Duration => (role == NavalVfxRole.Area ? 1.08f : .94f) + (shots - 1) * .09f;
        // Aggregated damage labels/bars wait until the last visual salvo makes contact.
        public float ContactAt => ContactTime(role, shots) / Duration;
        public static float ContactTime(NavalVfxRole role, int salvoCount = 1) =>
            (role == NavalVfxRole.Cannon || role == NavalVfxRole.Strike ? .36f :
                role == NavalVfxRole.Affliction || role == NavalVfxRole.Push ? .32f : .22f)
            + (Mathf.Clamp(salvoCount, 1, 4) - 1) * .09f;
        public bool IsPlaying { get; private set; }
        public Vector2 ContactPoint => outcome == BattleSchoolAttackOutcome.Missed && IsAttack
            ? end + Normal * 46f : end;
        public override Texture mainTexture
        {
            get
            {
                if (liveAtlas == null) liveAtlas = LoadAtlas();
                return liveAtlas != null ? liveAtlas : Texture2D.whiteTexture;
            }
        }

        private static Texture2D LoadAtlas()
        {
            if (atlas == null) atlas = NavalVfxArtCatalog.Load();
            return atlas;
        }

        private bool IsAttack => role == NavalVfxRole.Cannon || role == NavalVfxRole.Strike;
        private float ContactSeconds => IsAttack ? .36f : role == NavalVfxRole.Affliction || role == NavalVfxRole.Push ? .32f : .22f;
        private Vector2 Direction => (end - start).sqrMagnitude > .01f ? (end - start).normalized : Vector2.right;
        private Vector2 Normal => new Vector2(-Direction.y, Direction.x);
        private float Heading => Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        private int Element => !hasSchool ? 0 : school == BattleSchoolAttackKind.Fire ? 4 :
            school == BattleSchoolAttackKind.Arcane ? 5 : school == BattleSchoolAttackKind.Curse ? 6 :
            school == BattleSchoolAttackKind.Tide ? 7 : school == BattleSchoolAttackKind.Thunder ? 8 : 9;

        public void Configure(NavalVfxRole effectRole, BattleSchoolAttackKind effectSchool, bool usesSchool,
            BattleSchoolAttackOutcome result, Vector2 from, Vector2 to, int salvoCount = 1)
        {
            role = effectRole; school = effectSchool; hasSchool = usesSchool; outcome = result;
            start = Safe(from); end = Safe(to); shots = Mathf.Clamp(salvoCount, 1, 4);
            areaTargets = null;
            progress = 0f; IsPlaying = false; retiring = false; owner = null; hadOwner = false;
            raycastTarget = false; color = Color.white;
            // Load only on configure/first access, never allocate textures or materials per cast.
            if (liveAtlas == null) liveAtlas = LoadAtlas();
            SetAllDirty();
        }

        /// <summary>Exact committed field cells. An explicitly empty region remains empty.</summary>
        public void ConfigureAreaTargets(IReadOnlyList<Vector2> points, float cellSize = 80f)
        {
            int count = points == null ? 0 : Mathf.Min(128, points.Count);
            areaTargets = new Vector2[count];
            for (int i = 0; i < count; i++) areaTargets[i] = Safe(points[i]);
            areaCellSize = float.IsNaN(cellSize) || float.IsInfinity(cellSize) ? 80f : Mathf.Clamp(cellSize, 8f, 1000f);
            SetVerticesDirty();
        }

        public void SetProgress(float value)
        {
            progress = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            SetVerticesDirty();
        }

        public void Play(Behaviour presentationOwner)
        {
            owner = presentationOwner; hadOwner = owner != null; IsPlaying = true;
            SetProgress(0f);
        }

        public void AdvancePresentation(float seconds)
        {
            if (!IsPlaying) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Cancel(); return; }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            SetProgress(progress + Mathf.Max(0f, seconds) / Duration);
            if (progress >= 1f) Cancel();
        }

        public void Cancel()
        {
            IsPlaying = false; SetProgress(1f);
            if (Application.isPlaying && !retiring)
            {
                retiring = true;
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private void Update() { if (Application.isPlaying) AdvancePresentation(Time.unscaledDeltaTime); }
        private void OnApplicationPause(bool paused) { if (paused && IsPlaying) Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused && IsPlaying) Cancel(); }
        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        protected override void OnDisable() { if (IsPlaying) Cancel(); base.OnDisable(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            bounds = rectTransform.rect;
            if (progress <= 0f || progress >= 1f || bounds.width <= 0f || bounds.height <= 0f || liveAtlas == null) return;
            float time = progress * Duration;
            if ((role == NavalVfxRole.Field || role == NavalVfxRole.Area) && areaTargets != null)
            {
                for (int i = 0; i < areaTargets.Length; i++) DrawFieldCell(mesh, areaTargets[i], time, i);
                return;
            }
            switch (role)
            {
                case NavalVfxRole.Cannon:
                case NavalVfxRole.Strike:
                    for (int shot = 0; shot < shots; shot++) DrawAttack(mesh, time - shot * .09f, shot);
                    break;
                case NavalVfxRole.Affliction: DrawAffliction(mesh, time); break;
                case NavalVfxRole.Guard: DrawGuard(mesh, time); break;
                case NavalVfxRole.Heal: DrawHealing(mesh, time); break;
                case NavalVfxRole.Field: DrawField(mesh, time, false); break;
                case NavalVfxRole.Area: DrawField(mesh, time, true); break;
                case NavalVfxRole.Dispel: DrawDispel(mesh, time); break;
                case NavalVfxRole.Push: DrawPush(mesh, time); break;
                default: DrawEmpower(mesh, time); break;
            }
        }

        private void DrawAttack(VertexHelper mesh, float time, int shot)
        {
            if (time <= 0f || time >= .94f) return;
            float spread = (shot - (shots - 1) * .5f) * 7f;
            Vector2 launch = start + Direction * 20f + Normal * spread;
            Vector2 contact = ContactPoint + Normal * spread * .35f;
            bool cannon = role == NavalVfxRole.Cannon;
            float cast = Pulse(time / .24f);
            if (cannon)
            {
                Sprite(mesh, 2, launch - Direction * (8f + time * 20f), new Vector2(64f, 52f) * (1f + time * 1.1f), -Heading * .2f,
                    White(Envelope(time, 0f, .10f, .44f) * .55f));
                if (time < .24f)
                    Sequence(mesh, NavalVfxArtCatalog.Muzzle, time / .24f, launch + Direction * 7f,
                        new Vector2(74f, 62f), Heading, White(Envelope(time, 0f, .035f, .24f)));
            }
            else
            {
                Sprite(mesh, Element, launch, Vector2.one * (24f + cast * 32f), time * 90f, White(cast * .65f));
            }
            float travel = Mathf.InverseLerp(.12f, .36f, time);
            if (time > .10f && time < .43f)
            {
                float flightAlpha = Envelope(time, .10f, .14f, .43f);
                if (!cannon && hasSchool && school == BattleSchoolAttackKind.Thunder)
                {
                    // A quick leader followed by two offset discharges; no random flicker.
                    Vector2 leader = Vector2.Lerp(launch, contact, Smooth(travel));
                    Beam(mesh, launch, leader, 8, 32f + 12f * Mathf.Sin(travel * Mathf.PI), flightAlpha);
                    Beam(mesh, launch + Normal * 9f, Vector2.Lerp(launch, contact, Mathf.Clamp01(travel * 1.13f)) - Normal * 6f,
                        8, 21f, flightAlpha * .5f);
                }
                else
                {
                    int trailSprite = cannon ? 2 : hasSchool && school == BattleSchoolAttackKind.Tide ? 15 : Element;
                    for (int i = 7; i >= 1; i--)
                    {
                        float trailT = travel - i * .035f;
                        if (trailT <= 0f) continue;
                        Vector2 p = Flight(launch, contact, trailT, cannon, shot);
                        float weight = (1f - i / 8f) * flightAlpha;
                        Sprite(mesh, trailSprite, p, Vector2.one * (cannon ? 18f + i * 2f : 25f + i * 2.5f),
                            Heading + i * 13f + time * 80f, White(weight * (cannon ? .22f : .35f)));
                    }
                    Vector2 head = Flight(launch, contact, travel, cannon, shot);
                    if (cannon)
                        Sprite(mesh, 12, head, new Vector2(56f, 23f), Heading, White(flightAlpha));
                    else
                        Sprite(mesh, Element, head, Vector2.one * (school == BattleSchoolAttackKind.Tide ? 62f : 47f),
                            school == BattleSchoolAttackKind.Tide ? -time * 250f : time * 150f, White(flightAlpha));
                    if (hasSchool && school == BattleSchoolAttackKind.Arcane && !cannon)
                        Orbit(mesh, head, 12, 23f, 4, time * 5f, 13f, flightAlpha * .8f);
                }
            }
            if (time >= .36f) DrawContact(mesh, contact, (time - .36f) / .58f, cannon);
        }

        private Vector2 Flight(Vector2 a, Vector2 b, float t, bool cannon, int shot)
        {
            t = Mathf.Clamp01(t);
            float arc = Mathf.Sin(t * Mathf.PI);
            float curve = cannon ? 13f : hasSchool && school == BattleSchoolAttackKind.Curse ? 36f : 21f;
            Vector2 p = Vector2.Lerp(a, b, t) + Normal * arc * curve;
            if (!cannon && hasSchool && (school == BattleSchoolAttackKind.Tide || school == BattleSchoolAttackKind.Blood))
                p += Normal * Mathf.Sin(t * Mathf.PI * 3f + shot) * arc * 11f;
            return p;
        }

        private void DrawContact(VertexHelper mesh, Vector2 at, float t, bool cannon)
        {
            if (t < 0f || t >= 1f) return;
            float bloom = Smooth(Mathf.Clamp01(t * 5f));
            float fade = Mathf.Pow(1f - t, 1.35f);
            float sequenceAlpha = Envelope(t, 0f, .075f, 1f);
            Sprite(mesh, 15, at + Vector2.down * 9f, new Vector2(92f, 51f) * (.55f + t), 0f, White(Pulse(t) * .48f));
            if (outcome == BattleSchoolAttackOutcome.Missed)
            {
                Sequence(mesh, NavalVfxArtCatalog.WaterSplash, t, at + Vector2.up * 9f,
                    new Vector2(116f, 118f), 0f, White(sequenceAlpha));
                Motes(mesh, at, 15, t, 5, 47f, .42f, false);
                return;
            }
            if (outcome == BattleSchoolAttackOutcome.Guarded)
            {
                Sprite(mesh, 10, at, Vector2.one * (77f + bloom * 20f), 0f, White(fade * bloom));
                // A shield catches the projectile without showing damaged hull wood.
                Sprite(mesh, 0, at - Direction * 22f, Vector2.one * (34f + t * 18f), Heading,
                    new Color(.68f, .9f, 1f, fade * .65f));
                Motes(mesh, at, 12, t, 7, 48f, .6f, false);
                return;
            }
            if (!hasSchool)
            {
                Sequence(mesh, NavalVfxArtCatalog.HullImpact, t, at,
                    new Vector2(cannon ? 125f : 108f, cannon ? 116f : 100f), 0f, White(sequenceAlpha));
                Sprite(mesh, 2, at + Vector2.up * (t * 23f), Vector2.one * (46f + t * 58f), -t * 13f,
                    White(Envelope(t, .22f, .45f, 1f) * .25f));
                Motes(mesh, at, 13, t, 5, cannon ? 56f : 43f, .33f, false);
                return;
            }

            // Each school owns a distinct authored eight-frame burst. Keep it upright;
            // rotating the entire plume makes smoke and water read as a spinning sticker.
            Sequence(mesh, NavalVfxArtCatalog.SchoolSequence(school), t, at,
                school == BattleSchoolAttackKind.Tide ? new Vector2(137f, 108f) : Vector2.one * 125f,
                0f, White(sequenceAlpha));
            if (cannon)
                Motes(mesh, at, 13, t, 4, 48f, .25f, false);
            switch (school)
            {
                case BattleSchoolAttackKind.Fire:
                    Sprite(mesh, 2, at + Vector2.up * t * 28f, Vector2.one * (50f + t * 55f), -t * 15f,
                        White(Envelope(t, .25f, .48f, 1f) * .28f));
                    Motes(mesh, at, 4, t, 6, 60f, .45f, false);
                    break;
                case BattleSchoolAttackKind.Arcane:
                    Orbit(mesh, at, 5, 30f + t * 32f, 4, -t * 3f, 16f * fade, fade * .45f);
                    break;
                case BattleSchoolAttackKind.Curse:
                    Orbit(mesh, at, 6, 23f + t * 22f, 3, t * 1.6f, 26f, fade * .25f);
                    break;
                case BattleSchoolAttackKind.Tide:
                    Motes(mesh, at, 15, t, 6, 61f, .38f, false);
                    break;
                case BattleSchoolAttackKind.Thunder:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 spoke = Polar(i * Mathf.PI * 2f / 3f + .2f);
                        Beam(mesh, at + spoke * 15f, at + spoke * (41f + bloom * 20f), 8, 14f, fade * bloom * .55f);
                    }
                    break;
                case BattleSchoolAttackKind.Blood:
                    // Orbit the struck hull only; return trails would imply unearned life steal.
                    Orbit(mesh, at, 9, 20f + t * 29f, 4, t * 3.5f, 15f, fade * .48f);
                    break;
            }
        }

        private void DrawAffliction(VertexHelper mesh, float time)
        {
            float t = time / .94f;
            float travel = Mathf.InverseLerp(.06f, .32f, time);
            int glyph = hasSchool ? Element : 6;
            if (time < .43f)
            {
                for (int i = 5; i >= 0; i--)
                {
                    float u = travel - i * .055f;
                    if (u <= 0f) continue;
                    Vector2 p = Flight(start, end, u, false, 0);
                    Sprite(mesh, glyph, p, Vector2.one * (32f - i * 2f), time * 90f + i * 18f,
                        White(Envelope(time, 0f, .12f, .43f) * (1f - i / 7f) * .75f));
                }
            }
            if (time < .25f) return;
            float arrival = Mathf.InverseLerp(.25f, .94f, time), alpha = Pulse(arrival);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f + t * 5f;
                Sprite(mesh, glyph, end + Polar(angle) * (33f - arrival * 10f),
                    new Vector2(42f, 62f), angle * Mathf.Rad2Deg - 90f, White(alpha * .7f));
            }
            Sprite(mesh, 14, end, Vector2.one * (78f + arrival * 26f), -t * 60f, new Color(.55f, .76f, .7f, alpha * .28f));
        }

        private void DrawGuard(VertexHelper mesh, float time)
        {
            float t = time / .94f, alpha = Envelope(time, 0f, .2f, .94f);
            float expand = .65f + Smooth(Mathf.Clamp01(time / .25f)) * .4f;
            Sprite(mesh, 15, end + Vector2.down * 14f, new Vector2(111f, 49f) * expand, 0f, White(alpha * .8f));
            Sprite(mesh, 10, end, Vector2.one * 102f * expand, 0f, White(alpha * .86f));
            if (hasSchool) Orbit(mesh, end, Element, 46f, 4, t * 2.5f, 23f, alpha * .55f);
            for (int i = 0; i < 4; i++)
            {
                float u = Mathf.Clamp01(t * 1.4f - i * .13f);
                Sprite(mesh, 12, end + new Vector2(-31f + i * 21f, -24f + u * 61f),
                    new Vector2(28f, 10f), 90f, White(Pulse(u) * .55f));
            }
        }

        private void DrawHealing(VertexHelper mesh, float time)
        {
            float t = time / .94f, alpha = Pulse(t);
            Sprite(mesh, 11, end + Vector2.up * (t * 12f), new Vector2(96f, 125f) * (.75f + t * .3f), 0f, White(alpha * .9f));
            Sprite(mesh, 15, end + Vector2.down * 20f, new Vector2(105f, 43f) * (.75f + t * .5f), 0f,
                new Color(.8f, 1f, .65f, alpha * .55f));
            for (int i = 0; i < 11; i++)
            {
                float phase = Hash(i + 23), u = Mathf.Clamp01(t * 1.6f - phase * .5f);
                Vector2 p = end + new Vector2(Mathf.Sin(i * 2.4f + t) * (18f + phase * 27f), -26f + u * 91f);
                Sprite(mesh, 0, p, Vector2.one * (9f + phase * 6f), t * 65f + i * 30f,
                    new Color(.85f, 1f, .55f, Pulse(u) * .75f));
            }
        }

        private void DrawEmpower(VertexHelper mesh, float time)
        {
            float t = time / .94f, alpha = Pulse(t);
            int glyph = hasSchool ? Element : 0;
            Sprite(mesh, glyph, end + Vector2.up * 9f, Vector2.one * (49f + alpha * 23f), t * 80f, White(alpha * .7f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f + t * 1.8f;
                Vector2 p = end + Polar(angle) * Mathf.Lerp(56f, 8f, Smooth(t));
                Sprite(mesh, hasSchool ? glyph : 12, p, Vector2.one * 20f, angle * Mathf.Rad2Deg, White(alpha * .65f));
            }
            Sprite(mesh, 15, end + Vector2.down * 12f, new Vector2(93f, 43f) * (.6f + t * .6f), 0f, White(alpha * .45f));
        }

        private void DrawField(VertexHelper mesh, float time, bool burst)
        {
            float t = time / (burst ? 1.08f : .94f), alpha = Pulse(t);
            float radius = burst ? 78f : 49f;
            int glyph = hasSchool ? Element : 14;
            Sprite(mesh, 14, end, Vector2.one * radius * (1.3f + t * .7f), -t * 100f, White(alpha * .45f));
            Sprite(mesh, 15, end + Vector2.down * 3f, new Vector2(radius * 2.15f, radius * 1.3f) * (.55f + Smooth(t) * .65f),
                0f, White(alpha * .55f));
            if (burst && time >= ContactSeconds)
            {
                float u = Mathf.InverseLerp(ContactSeconds, 1.08f, time);
                Sequence(mesh, hasSchool ? NavalVfxArtCatalog.SchoolSequence(school) : NavalVfxArtCatalog.HullImpact,
                    u, end, Vector2.one * 160f, 0f, White(Envelope(u, 0f, .08f, 1f) * .9f));
                Motes(mesh, end, hasSchool && school == BattleSchoolAttackKind.Tide ? 15 : glyph, u, 6, 85f, .45f, false);
            }
            else
            {
                Sprite(mesh, glyph, end, Vector2.one * (42f + alpha * 25f), t * 100f, White(alpha * .55f));
                Orbit(mesh, end, glyph, radius * (.7f + t * .25f), 6, -t * 2f, 31f, alpha * .6f);
            }
        }

        private void DrawFieldCell(VertexHelper mesh, Vector2 at, float time, int index)
        {
            // Five or fewer sprites per committed cell, even for the full 128-cell
            // event budget. Cells never acquire false hits through a centroid fallback.
            float t = time / Duration, alpha = Pulse(t);
            Rect surfaceBounds = bounds;
            float half = areaCellSize * .5f;
            bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, at.x - half), Mathf.Max(bounds.yMin, at.y - half),
                Mathf.Min(bounds.xMax, at.x + half), Mathf.Min(bounds.yMax, at.y + half));
            if (bounds.width <= 0f || bounds.height <= 0f) { bounds = surfaceBounds; return; }
            float spriteScale = role == NavalVfxRole.Area ? 1.2f : 1.6f;
            float diameter = areaCellSize * .89f / spriteScale;
            float expand = .64f + Smooth(t) * .35f;
            int glyph = hasSchool ? Element : 14;
            Sprite(mesh, 14, at, Vector2.one * diameter * expand, -t * 65f + index * 17f, White(alpha * .28f));
            Sprite(mesh, 15, at, new Vector2(diameter, diameter * .62f) * expand, 0f, White(alpha * .65f));
            if (role == NavalVfxRole.Area && time >= ContactSeconds)
            {
                float u = Mathf.InverseLerp(ContactSeconds, Duration, time);
                Sequence(mesh, hasSchool ? NavalVfxArtCatalog.SchoolSequence(school) : NavalVfxArtCatalog.HullImpact,
                    u, at, Vector2.one * diameter, 0f, White(Envelope(u, 0f, .08f, 1f) * .94f));
            }
            else Sprite(mesh, glyph, at, Vector2.one * diameter * .73f, t * 70f, White(alpha * .65f));
            bounds = surfaceBounds;
        }

        private void DrawDispel(VertexHelper mesh, float time)
        {
            float t = time / .94f;
            Sprite(mesh, 10, end, Vector2.one * (92f + t * 38f), 0f, White((1f - t) * Pulse(Mathf.Clamp01(t * 2.2f)) * .7f));
            Sprite(mesh, 7, end, new Vector2(110f, 62f) * (.75f + t * .6f), -t * 120f, White(Pulse(t) * .55f));
            Motes(mesh, end, 12, t, 12, 84f, .8f, false);
        }

        private void DrawPush(VertexHelper mesh, float time)
        {
            float t = time / .94f, travel = Mathf.InverseLerp(.05f, .32f, time), alpha = Pulse(t);
            Vector2 p = Vector2.Lerp(start, end, Smooth(travel));
            Sprite(mesh, 3, p, new Vector2(92f, 72f), Heading - 90f, White(alpha * .85f));
            for (int i = 0; i < 4; i++)
            {
                float u = Mathf.Clamp01(travel - i * .12f);
                Sprite(mesh, 15, Vector2.Lerp(start, end, Smooth(u)) - Direction * i * 8f,
                    new Vector2(83f - i * 8f, 43f - i * 4f), Heading - 90f, White(alpha * (1f - i / 5f) * .65f));
            }
            if (time >= .32f)
            {
                float arrival = Mathf.InverseLerp(.32f, .94f, time);
                Sequence(mesh, NavalVfxArtCatalog.Tide, arrival, end, new Vector2(112f, 84f), 0f,
                    White(Envelope(arrival, 0f, .09f, 1f) * .72f));
            }
        }

        private void Motes(VertexHelper mesh, Vector2 center, int sprite, float t, int count, float reach, float opacity, bool inward)
        {
            for (int i = 0; i < count; i++)
            {
                float seed = Hash(i + 11), u = Mathf.Clamp01(t * 1.25f - seed * .16f);
                Vector2 direction = Polar(i * 2.399963f);
                float distance = (inward ? 1f - Smooth(u) : Smooth(u)) * reach * (.55f + seed * .45f);
                Vector2 p = center + direction * distance + Vector2.down * u * u * 13f;
                Sprite(mesh, sprite, p, Vector2.one * (11f + seed * 16f) * (1f - u * .5f),
                    i * 37f + u * 120f, White(Pulse(u) * opacity));
            }
        }

        private void Orbit(VertexHelper mesh, Vector2 center, int sprite, float radius, int count, float spin, float size, float alpha)
        {
            for (int i = 0; i < count; i++)
            {
                float a = spin + i * Mathf.PI * 2f / count;
                Sprite(mesh, sprite, center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * .66f),
                    Vector2.one * size, a * Mathf.Rad2Deg + 90f, White(alpha));
            }
        }

        private void Beam(VertexHelper mesh, Vector2 a, Vector2 b, int sprite, float width, float alpha)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .01f) return;
            Sprite(mesh, sprite, (a + b) * .5f, new Vector2(delta.magnitude * 1.1f, width),
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, White(alpha));
        }

        private void Sprite(VertexHelper mesh, int cell, Vector2 center, Vector2 size, float degrees, Color tint)
        {
            DrawQuad(mesh, NavalVfxArtCatalog.StaticUv(cell), center, size, degrees, tint);
        }

        private void Sequence(VertexHelper mesh, int sequence, float position, Vector2 center, Vector2 size, float degrees, Color tint)
        {
            if (position < 0f || position >= 1f) return;
            DrawQuad(mesh, NavalVfxArtCatalog.SequenceUv(sequence, position), center, size, degrees, tint);
        }

        private void DrawQuad(VertexHelper mesh, Rect uv, Vector2 center, Vector2 size, float degrees, Color tint)
        {
            if (tint.a <= .003f || size.x <= .01f || size.y <= .01f || mesh.currentVertCount > 6000) return;
            // Production tactical ships are approximately 170 design units tall. Keep
            // individual impacts legible at that scale, with wider area casts controlled.
            size *= role == NavalVfxRole.Area ? 1.2f : 1.6f;
            float radians = degrees * Mathf.Deg2Rad, c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            Vector2 x = new Vector2(c, s) * size.x * .5f, y = new Vector2(-s, c) * size.y * .5f;
            float u0 = uv.xMin, u1 = uv.xMax, v0 = uv.yMin, v1 = uv.yMax;
            polygonA[0] = new ClipVertex(center - x - y, new Vector2(u0, v0));
            polygonA[1] = new ClipVertex(center - x + y, new Vector2(u0, v1));
            polygonA[2] = new ClipVertex(center + x + y, new Vector2(u1, v1));
            polygonA[3] = new ClipVertex(center + x - y, new Vector2(u1, v0));
            int count = 4;
            ClipVertex[] input = polygonA, output = polygonB;
            for (int edge = 0; edge < 4 && count > 0; edge++)
            {
                int nextCount = 0;
                ClipVertex previous = input[count - 1];
                float previousDistance = EdgeDistance(previous.Position, edge);
                for (int i = 0; i < count; i++)
                {
                    ClipVertex current = input[i];
                    float currentDistance = EdgeDistance(current.Position, edge);
                    if ((currentDistance >= 0f) != (previousDistance >= 0f))
                    {
                        float amount = previousDistance / (previousDistance - currentDistance);
                        output[nextCount++] = new ClipVertex(Vector2.LerpUnclamped(previous.Position, current.Position, amount),
                            Vector2.LerpUnclamped(previous.Uv, current.Uv, amount));
                    }
                    if (currentDistance >= 0f) output[nextCount++] = current;
                    previous = current; previousDistance = currentDistance;
                }
                count = nextCount;
                ClipVertex[] swap = input; input = output; output = swap;
            }
            if (count < 3) return;
            int first = mesh.currentVertCount;
            tint *= color;
            for (int i = 0; i < count; i++) mesh.AddVert(input[i].Position, tint, input[i].Uv);
            for (int i = 1; i < count - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private float EdgeDistance(Vector2 p, int edge) => edge == 0 ? p.x - bounds.xMin : edge == 1 ? bounds.xMax - p.x :
            edge == 2 ? p.y - bounds.yMin : bounds.yMax - p.y;
        private static Vector2 Safe(Vector2 p) => new Vector2(Finite(p.x), Finite(p.y));
        private static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : Mathf.Clamp(v, -100000f, 100000f);
        private static Vector2 Polar(float radians) => new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        private static Color White(float alpha) => new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float Pulse(float t) => t <= 0f || t >= 1f ? 0f : Mathf.Sin(Mathf.PI * t);
        private static float Hash(int value) => Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);
        private static float Envelope(float time, float startAt, float peak, float endAt) => time <= startAt || time >= endAt ? 0f :
            time < peak ? Smooth(Mathf.InverseLerp(startAt, peak, time)) : 1f - Smooth(Mathf.InverseLerp(peak, endAt, time));
    }
}
