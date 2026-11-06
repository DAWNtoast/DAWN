using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Boarding spells grow from the actor, deck or sky; never from a shared cast emblem.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BoardingSchoolCastGraphic : MaskableGraphic
    {
        public const float Duration = 1.15f;
        public BattleSchoolAttackKind Kind { get; private set; }
        public BoardingSchoolCastRole Role { get; private set; }
        public float Progress { get; private set; }
        public bool IsPlaying { get; private set; }
        private Vector2 origin, target, ground;
        private float skyY;
        private Behaviour owner;
        private bool hadOwner, retiring;
        private Rect bounds;
        private RawImage schoolArt;

        public void Configure(BattleSchoolAttackKind school, BoardingSchoolCastRole role, Vector2 from, Vector2 to)
        {
            Kind = school; Role = role; origin = Safe(from); target = Safe(to);
            ground = target + Vector2.down * 140f; skyY = target.y + 260f;
            Progress = 0f; IsPlaying = false; retiring = false; raycastTarget = false;
            if (Role != BoardingSchoolCastRole.Attack && schoolArt == null)
                schoolArt = BoardingRemasterFxArt.CreateLayer(rectTransform, "Boarding School Cast Art");
            RefreshSchoolArt();
            SetVerticesDirty();
        }

        public void ConfigureEnvironment(Vector2 deckGround, float overheadY)
        {
            ground = Safe(deckGround); skyY = Finite(overheadY); RefreshSchoolArt(); SetVerticesDirty();
        }

        public void Play(Behaviour presentationOwner)
        {
            owner = presentationOwner; hadOwner = owner != null; IsPlaying = true; SetProgress(0f);
        }

        public void SetProgress(float value)
        {
            Progress = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            RefreshSchoolArt();
            SetVerticesDirty();
        }

        public void AdvancePresentation(float seconds)
        {
            if (!IsPlaying) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Cancel(); return; }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            SetProgress(Progress + Mathf.Max(0f, seconds) / Duration);
            if (Progress >= 1f) Cancel();
        }

        public void Cancel()
        {
            IsPlaying = false; SetProgress(1f);
            if (Application.isPlaying && !retiring)
            {
                retiring = true; gameObject.SetActive(false); Destroy(gameObject);
            }
        }

        private void Update() { if (Application.isPlaying) AdvancePresentation(Time.unscaledDeltaTime); }
        private void OnApplicationPause(bool paused) { if (paused && IsPlaying) Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused && IsPlaying) Cancel(); }
        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            if (schoolArt != null) schoolArt.enabled = true;
        }

        protected override void OnDisable()
        {
            if (IsPlaying) Cancel();
            if (schoolArt != null) schoolArt.enabled = false;
            base.OnDisable();
        }

        private void RefreshSchoolArt()
        {
            if (schoolArt == null) return;
            float life = Mathf.Clamp01(Progress * 8f) * Mathf.Clamp01((1f - Progress) * 4f);
            float side = target.x < 0f ? 1f : -1f;
            Vector2 position = target;
            Vector2 extent;
            switch (Role)
            {
                case BoardingSchoolCastRole.Field:
                    extent = new Vector2(291f, 138f);
                    position = ground + Vector2.up * 55f;
                    break;
                case BoardingSchoolCastRole.Affliction:
                    extent = new Vector2(168f, 216f);
                    if (Kind == BattleSchoolAttackKind.Curse || Kind == BattleSchoolAttackKind.Tide ||
                        Kind == BattleSchoolAttackKind.Fire) position = ground + Vector2.up * 83f;
                    break;
                case BoardingSchoolCastRole.Guard:
                    extent = new Vector2(128f, 167f);
                    position += new Vector2(side * 55f, 8f);
                    life *= .74f;
                    break;
                case BoardingSchoolCastRole.Heal:
                    extent = new Vector2(105f, 150f);
                    position += new Vector2(-side * 37f, -24f + Progress * 39f);
                    life *= .68f;
                    break;
                case BoardingSchoolCastRole.Charge:
                    extent = Vector2.one * Mathf.Lerp(73f, 120f, Ease(Progress));
                    position += new Vector2(side * 51f, 32f);
                    break;
                case BoardingSchoolCastRole.Empower:
                    extent = new Vector2(116f, 170f);
                    position += new Vector2(side * 22f, -45f + Ease(Progress) * 72f);
                    break;
                default:
                    schoolArt.color = Color.clear;
                    return;
            }
            BoardingRemasterFxArt.Pose(schoolArt, BoardingRemasterFxArt.SchoolRow(Kind),
                Progress, position, extent, life, side < 0f);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); bounds = rectTransform.rect;
            if (Role == BoardingSchoolCastRole.Attack || Progress >= 1f || bounds.width <= 0f || bounds.height <= 0f) return;
            float life = Mathf.Clamp01(Progress * 7f) * Mathf.Clamp01((1f - Progress) * 3.5f);
            switch (Kind)
            {
                case BattleSchoolAttackKind.Fire: Fire(mesh, life); break;
                case BattleSchoolAttackKind.Arcane: Arcane(mesh, life); break;
                case BattleSchoolAttackKind.Curse: Curse(mesh, life); break;
                case BattleSchoolAttackKind.Tide: Tide(mesh, life); break;
                case BattleSchoolAttackKind.Thunder: Thunder(mesh, life); break;
                case BattleSchoolAttackKind.Blood: Blood(mesh, life); break;
            }
        }

        private void Fire(VertexHelper mesh, float life)
        {
            bool guarded = Role == BoardingSchoolCastRole.Guard, gentle = Role == BoardingSchoolCastRole.Heal;
            float spread = Role == BoardingSchoolCastRole.Field ? 180f : guarded ? 92f : gentle ? 39f : 68f;
            float rise = Ease(Progress * 2.5f);
            Color flame = new Color(1f, .28f, .035f, life * .82f), hot = new Color(1f, .83f, .22f, life * .92f);
            // Unequal tongues ignite at deck level and peel upward, leaving smoke above them.
            for (int i = 0; i < 7; i++)
            {
                float phase = Progress * 5f + i * 1.7f;
                Vector2 foot = ground + new Vector2((i - 3) * spread / 3f, 3f + Mathf.Sin(i * 2f) * 4f);
                float height = (gentle ? 45f : guarded ? 175f : 92f) * rise * (.7f + .3f * Mathf.Sin(i * 2.1f + .6f));
                Vector2 tip = foot + new Vector2(Mathf.Sin(phase) * 19f, height);
                Vector2 bend = foot + new Vector2(Mathf.Sin(phase + 1f) * 25f, height * .50f);
                Ribbon(mesh, foot, foot + Vector2.up * height * .3f, bend, tip, gentle ? 14f : 23f, .4f, flame, 9);
                Ribbon(mesh, foot + Vector2.up * 3f, foot + Vector2.up * height * .18f,
                    bend * .7f + foot * .3f, Vector2.Lerp(foot, tip, .67f), 8f, .2f, hot, 7);
                if (i % 2 == 0 && !gentle)
                    Ribbon(mesh, tip, tip + new Vector2(-13f, 18f), tip + new Vector2(25f, 54f),
                        tip + new Vector2(12f + Progress * 24f, 85f), 12f, 24f,
                        new Color(.22f, .17f, .16f, life * .17f), 8);
            }
            for (int i = 0; i < 9; i++)
            {
                float u = Mathf.Repeat(Progress * .9f + i * .119f, 1f);
                Vector2 ember = ground + new Vector2(Mathf.Sin(i * 2.8f) * spread + Mathf.Sin(u * 8f + i) * 9f,
                    18f + u * (gentle ? 115f : 205f));
                Stroke(mesh, ember, ember + new Vector2(-3f, 6f), 2.5f, Alpha(hot, life * (1f - u)));
            }
        }

        private void Arcane(VertexHelper mesh, float life)
        {
            Color violet = new Color(.62f, .43f, 1f, life * .82f), edge = new Color(.86f, .87f, 1f, life);
            if (Role == BoardingSchoolCastRole.Guard)
            {
                // Separate panes lock into place from the side, leaving the character readable.
                float build = Ease(Progress * 3f);
                for (int i = 0; i < 5; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    Vector2 at = target + new Vector2(side * (68f + (1f - build) * 65f), (i - 2) * 38f);
                    Vector2 a = at + new Vector2(-20f, -30f), b = at + new Vector2(23f, -22f);
                    Vector2 c = at + new Vector2(20f, 28f), d = at + new Vector2(-15f, 36f);
                    Quad(mesh, a, b, c, d, Alpha(violet, life * .12f));
                    Stroke(mesh, a, d, 2f, edge); Stroke(mesh, d, c, 2f, violet); Stroke(mesh, c, b, 2f, edge);
                }
                return;
            }
            Vector2 focus = Role == BoardingSchoolCastRole.Field ? ground + Vector2.up * 22f : target + Vector2.up * 20f;
            for (int i = 0; i < 11; i++)
            {
                float u = Mathf.Clamp01(Progress * 1.6f - i * .047f);
                Vector2 from = focus + new Vector2((i % 2 == 0 ? -1f : 1f) * (105f + i % 4 * 24f), (i % 5 - 2) * 57f);
                Vector2 at = Vector2.Lerp(from, focus + new Vector2((i % 3 - 1) * 8f, 0f), Ease(u));
                Vector2 direction = (focus - from).normalized;
                float fade = life * (1f - Mathf.Clamp01((u - .82f) / .18f));
                Stroke(mesh, at - direction * (19f + u * 17f), at, 1.4f, Alpha(violet, fade * .45f));
                Shard(mesh, at, direction, 8f + (i % 3) * 3f, Alpha(violet, fade), Alpha(edge, fade));
            }
            if (Role == BoardingSchoolCastRole.Heal)
                for (int i = 0; i < 3; i++)
                {
                    Vector2 a = target + new Vector2((i - 1) * 18f, -35f + i * 10f);
                    Stroke(mesh, a, a + new Vector2(9f, 51f * Ease(Progress * 2f)), 2f, edge);
                }
            else
                Stroke(mesh, focus - Vector2.up * 13f, focus + Vector2.up * (12f + 22f * Progress),
                    3f + Mathf.Sin(Progress * Mathf.PI) * 4f, Alpha(edge, life * .7f));
        }

        private void Curse(VertexHelper mesh, float life)
        {
            bool bind = Role == BoardingSchoolCastRole.Affliction, field = Role == BoardingSchoolCastRole.Field;
            float rise = Ease(Progress * 1.9f);
            Color shadow = new Color(.12f, .18f, .07f, life * .8f), poison = new Color(.54f, .78f, .18f, life * .85f);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector2 root = ground + new Vector2((i - 2.5f) * (field ? 46f : 25f), 0f);
                float height = (bind ? 225f : field ? 85f : 160f) * rise * (.72f + i % 3 * .12f);
                Vector2 last = root;
                // Roots climb from below; lateral bends cross the silhouette at different heights.
                for (int step = 1; step <= 13; step++)
                {
                    float u = step / 13f;
                    Vector2 next = root + new Vector2(Mathf.Sin(u * 7f + i * 1.4f - Progress) * (bind ? 39f : 22f) * u - side * u * 14f, height * u);
                    Stroke(mesh, last, next, 10f * (1f - u) + 2f, shadow);
                    Stroke(mesh, last + Vector2.right * 1.7f, next + Vector2.right * 1.7f, 2f, poison);
                    if (step % 4 == 0)
                        Triangle(mesh, next, next + new Vector2(side * 18f, -7f), next + new Vector2(side * 4f, -17f), poison);
                    last = next;
                }
                Stroke(mesh, last, last + new Vector2(-side * 10f, 13f), 2.5f, Alpha(poison, life));
            }
            if (Role == BoardingSchoolCastRole.Heal || Role == BoardingSchoolCastRole.Empower)
                for (int i = 0; i < 4; i++)
                {
                    Vector2 at = target + new Vector2((i % 2 == 0 ? -1f : 1f) * (35f + i * 8f), 15f + Progress * 38f);
                    Stroke(mesh, at, at + new Vector2(4f, 17f), 2f, poison);
                    Stroke(mesh, at + new Vector2(4f, 10f), at + new Vector2(12f, 13f), 1.5f, poison);
                }
        }

        private void Tide(VertexHelper mesh, float life)
        {
            bool guard = Role == BoardingSchoolCastRole.Guard, field = Role == BoardingSchoolCastRole.Field;
            float rise = Ease(Progress * 2.3f);
            float width = guard ? 160f : field ? 310f : 100f;
            float height = (guard ? 235f : Role == BoardingSchoolCastRole.Affliction ? 75f : 150f) * rise;
            Color water = new Color(.08f, .55f, .80f, life * (guard ? .22f : .12f));
            Color foam = new Color(.60f, .96f, 1f, life * .83f);
            Vector2 previousTop = ground + new Vector2(-width * .5f, height);
            for (int i = 1; i <= 18; i++)
            {
                float x = (i / 18f - .5f) * width;
                Vector2 nextTop = ground + new Vector2(x, height + Mathf.Sin(i * .75f + Progress * 8f) * (guard ? 11f : 17f));
                Vector2 bottomA = new Vector2(previousTop.x, ground.y), bottomB = new Vector2(nextTop.x, ground.y);
                Quad(mesh, bottomA, bottomB, nextTop, previousTop, water);
                Stroke(mesh, previousTop, nextTop, guard ? 4f : 3f, foam);
                if (i % 3 == 0)
                {
                    Vector2 low = new Vector2(x + ground.x + Mathf.Sin(i) * 7f, ground.y + 14f);
                    Ribbon(mesh, low, low + new Vector2(15f, height * .32f), nextTop + new Vector2(-12f, -28f),
                        nextTop, 4f, 1f, Alpha(foam, life * .5f), 8);
                    Vector2 drop = nextTop + new Vector2(Mathf.Sin(i * 2f) * 8f, 12f + Progress * 18f);
                    Stroke(mesh, drop, drop + new Vector2(2f, 7f), 2.5f, foam);
                }
                previousTop = nextTop;
            }
            // Healing water runs down the body after it has risen; it never forms a spell ring.
            if (Role == BoardingSchoolCastRole.Heal)
                for (int i = 0; i < 3; i++)
                {
                    Vector2 top = target + new Vector2((i - 1) * 27f, 55f), bottom = target + new Vector2((i - 1) * 32f, -60f);
                    Ribbon(mesh, top, top + new Vector2(14f, -30f), bottom + new Vector2(-10f, 30f), bottom,
                        5f, 1f, Alpha(foam, life * .7f), 8);
                }
        }

        private void Thunder(VertexHelper mesh, float life)
        {
            bool field = Role == BoardingSchoolCastRole.Field, guard = Role == BoardingSchoolCastRole.Guard;
            float top = Mathf.Min(skyY - 15f, target.y + 145f);
            Color amber = new Color(.51f, .65f, 1f, life * .34f), core = new Color(.88f, .94f, 1f, life * .94f);
            // A sparse electrical canopy charges overhead, separate from the eventual sky strike.
            for (int branch = 0; branch < 5; branch++)
            {
                float side = branch % 2 == 0 ? -1f : 1f;
                Vector2 a = field ? ground + new Vector2((branch - 2) * 45f, 7f) :
                    new Vector2(target.x + (branch - 2) * 32f, top + Mathf.Sin(branch * 2f) * 17f);
                float reach = Ease(Progress * 3f) * (guard ? 120f : 57f);
                Vector2 b = a + new Vector2(side * 26f, -reach * .34f);
                Vector2 c = a + new Vector2(-side * 9f, -reach * .62f), d = a + new Vector2(side * 18f, -reach);
                if (field) { b.y = a.y + 17f; c.y = a.y - 3f; d.y = a.y + 9f; d.x += side * 42f; }
                Stroke(mesh, a, b, 11f, amber); Stroke(mesh, b, c, 11f, amber); Stroke(mesh, c, d, 9f, amber);
                Stroke(mesh, a, b, 2.7f, core); Stroke(mesh, b, c, 2.7f, core); Stroke(mesh, c, d, 1.8f, core);
                Stroke(mesh, b, b + new Vector2(side * 19f, 9f), 1.4f, core);
            }
            if (Role == BoardingSchoolCastRole.Empower || Role == BoardingSchoolCastRole.Charge || Role == BoardingSchoolCastRole.Heal)
                for (int i = 0; i < 5; i++)
                {
                    float u = Mathf.Repeat(Progress + i * .17f, 1f);
                    Vector2 at = target + new Vector2((i % 2 == 0 ? -1f : 1f) * 36f, -40f + u * 120f);
                    Stroke(mesh, at, at + new Vector2(7f, 10f), 1.8f, Alpha(core, life * (1f - u)));
                    Stroke(mesh, at + new Vector2(7f, 10f), at + new Vector2(2f, 16f), 1.8f, Alpha(core, life * (1f - u)));
                }
        }

        private void Blood(VertexHelper mesh, float life)
        {
            Color dark = new Color(.52f, .025f, .10f, life * .58f), red = new Color(1f, .12f, .24f, life * .87f);
            Vector2 heart = target + new Vector2(0f, 18f);
            if (Role == BoardingSchoolCastRole.Heal)
            {
                float side = Mathf.Abs(origin.x - target.x) > 1f ? Mathf.Sign(origin.x - target.x) : target.x <= bounds.center.x ? 1f : -1f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 source = heart + new Vector2(side * (215f + i * 18f), (i - 1.5f) * 32f);
                    Vector2 b = source + new Vector2(-side * 80f, -35f - i * 9f), c = heart + new Vector2(side * 76f, -54f + i * 21f);
                    float head = Mathf.Clamp01(Progress * 1.6f - i * .065f), tail = Mathf.Max(0f, head - .36f);
                    Vector2 previous = Bezier(source, b, c, heart, tail);
                    for (int j = 1; j <= 14; j++)
                    {
                        Vector2 next = Bezier(source, b, c, heart, Mathf.Lerp(tail, head, j / 14f));
                        Stroke(mesh, previous, next, 2f + j * .28f, Alpha(red, life * j / 14f)); previous = next;
                    }
                    Vector2 direction = (heart - previous).sqrMagnitude > .01f ? (heart - previous).normalized : Vector2.left * side;
                    Triangle(mesh, previous + direction * 8f, previous + new Vector2(-direction.y, direction.x) * 4f,
                        previous - direction * 8f, red);
                }
                return;
            }
            if (Role == BoardingSchoolCastRole.Guard || Role == BoardingSchoolCastRole.Affliction)
            {
                for (int i = 0; i < 5; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    Vector2 root = ground + new Vector2(side * (37f + i * 8f), 8f);
                    Vector2 top = target + new Vector2(side * (44f - i * 4f), 80f * Ease(Progress * 2f));
                    Ribbon(mesh, root, root + new Vector2(-side * 30f, 65f), top + new Vector2(side * 30f, -60f), top,
                        12f, 2f, dark, 12);
                    Stroke(mesh, top, top + new Vector2(-side * 24f, -17f), 3f, red);
                }
                return;
            }
            if (Role == BoardingSchoolCastRole.Field)
                for (int i = 0; i < 8; i++)
                {
                    Vector2 a = ground + new Vector2((i - 4) * 27f, Mathf.Sin(i * 2f) * 9f);
                    Triangle(mesh, a, a + new Vector2(44f, 12f), a + new Vector2(25f, -9f), dark);
                }
            // Loose blood flecks contract into the chest, without an enclosing orbit or emblem.
            for (int i = 0; i < 10; i++)
            {
                float u = Mathf.Clamp01(Progress * 1.5f - i * .04f);
                Vector2 from = target + new Vector2(Mathf.Sin(i * 2.3f) * 95f, (i % 5 - 2) * 42f);
                Vector2 at = Vector2.Lerp(from, heart, Ease(u)), d = (heart - from).normalized;
                float alpha = life * (1f - Mathf.Clamp01((u - .9f) * 10f));
                Stroke(mesh, at - d * 13f, at, 3f + i % 3, Alpha(red, alpha));
            }
            Stroke(mesh, heart - Vector2.right * 13f, heart + Vector2.right * 10f,
                8f * Mathf.Sin(Progress * Mathf.PI), Alpha(dark, life * .7f));
        }

        private void Ribbon(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float startWidth, float endWidth, Color tint, int steps)
        {
            Vector2 previous = a;
            for (int i = 1; i <= steps; i++)
            {
                float u = i / (float)steps;
                Vector2 next = Bezier(a, b, c, d, u);
                Stroke(mesh, previous, next, Mathf.Lerp(startWidth, endWidth, u), tint); previous = next;
            }
        }

        private void Shard(VertexHelper mesh, Vector2 center, Vector2 direction, float length, Color tint, Color light)
        {
            Vector2 side = new Vector2(-direction.y, direction.x) * length * .30f;
            Vector2 tip = center + direction * length, tail = center - direction * length * .64f;
            Triangle(mesh, tip, center + side, tail, tint); Triangle(mesh, tip, tail, center - side, light);
        }

        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < .001f || tint.a <= .001f || width <= .01f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
            Quad(mesh, a - n, a + n, b + n, b - n, tint);
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            if (tint.a <= .001f) return;
            int index = mesh.currentVertCount;
            Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint); Vertex(mesh, d, tint);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            if (tint.a <= .001f) return;
            int index = mesh.currentVertCount; Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        private void Vertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            if (schoolArt != null) tint.a *= .36f;
            point.x = Mathf.Clamp(point.x, bounds.xMin, bounds.xMax); point.y = Mathf.Clamp(point.y, bounds.yMin, bounds.yMax);
            mesh.AddVert(point, tint, Vector2.zero);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float v = 1f - t; return v * v * v * a + 3f * v * v * t * b + 3f * v * t * t * c + t * t * t * d;
        }
        private static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
        private static float Ease(float p) { p = Mathf.Clamp01(p); return 1f - (1f - p) * (1f - p); }
        private static Vector2 Safe(Vector2 p) => new Vector2(Finite(p.x), Finite(p.y));
        private static float Finite(float n) => float.IsNaN(n) || float.IsInfinity(n) ? 0f : n;
    }
}
