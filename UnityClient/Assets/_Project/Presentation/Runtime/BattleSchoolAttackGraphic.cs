using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BattleSchoolAttackKind { Fire, Arcane, Curse, Tide, Thunder, Blood }
    public enum BattleSchoolAttackOutcome { Hit, Guarded, Missed }

    /// <summary>Deterministic school trajectories and contact glyphs; no textures, rules, or input ownership.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BattleSchoolAttackGraphic : MaskableGraphic
    {
        public const float Duration = .62f;
        public const float ContactProgress = .32f;
        private BattleSchoolAttackKind kind;
        private BattleSchoolAttackOutcome outcome;
        private Vector2 start, end;
        private float progress, size = 1f;
        private Behaviour owner;
        private bool hadOwner, retiring;
        private Text resultLabel;
        private Vector2 labelOrigin;
        private Rect meshBounds;
        public BattleSchoolAttackKind Kind => kind;
        public BattleSchoolAttackOutcome Outcome => outcome;
        public float Progress => progress;
        public bool IsPlaying { get; private set; }
        public Vector2 ContactPoint => outcome == BattleSchoolAttackOutcome.Missed ? end + Normal * 70f * size : end;
        private Vector2 Direction => (end - start).sqrMagnitude > .01f ? (end - start).normalized : Vector2.right;
        private Vector2 Normal => new Vector2(-Direction.y, Direction.x);

        public void Configure(BattleSchoolAttackKind school, BattleSchoolAttackOutcome result, Vector2 from, Vector2 to, bool boarding)
        {
            kind = school; outcome = result; start = Safe(from); end = Safe(to); size = boarding ? .8f : 1f;
            progress = 0f; IsPlaying = false; retiring = false; raycastTarget = false;
            SetVerticesDirty();
        }

        public void BindResult(Text label)
        {
            resultLabel = label;
            labelOrigin = ContactPoint + Vector2.up * 82f * size;
            if (label != null) { label.raycastTarget = false; label.gameObject.SetActive(false); }
        }

        public void Play(Behaviour presentationOwner)
        {
            owner = presentationOwner; hadOwner = owner != null; IsPlaying = true;
            SetProgress(0f);
        }

        public void SetProgress(float value)
        {
            progress = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            if (resultLabel != null)
            {
                float t = Mathf.InverseLerp(ContactProgress, 1f, progress);
                resultLabel.gameObject.SetActive(t > 0f && t < 1f);
                resultLabel.rectTransform.anchoredPosition = labelOrigin + Vector2.up * (24f * t);
                Color c = resultLabel.color; c.a = Mathf.Min(1f, t * 8f) * Mathf.Clamp01((1f - t) * 3f); resultLabel.color = c;
            }
            SetVerticesDirty();
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

        public static Color Tint(BattleSchoolAttackKind school)
        {
            switch (school)
            {
                case BattleSchoolAttackKind.Fire: return new Color(1f, .43f, .12f);
                case BattleSchoolAttackKind.Arcane: return new Color(.64f, .53f, 1f);
                case BattleSchoolAttackKind.Curse: return new Color(.60f, .88f, .26f);
                case BattleSchoolAttackKind.Tide: return new Color(.23f, .87f, 1f);
                case BattleSchoolAttackKind.Thunder: return new Color(1f, .88f, .28f);
                default: return new Color(1f, .20f, .38f);
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            meshBounds = rectTransform.rect;
            if (progress >= 1f || meshBounds.width <= 0f || meshBounds.height <= 0f) return;
            float travel = Mathf.Clamp01(progress / ContactProgress);
            Color ink = Tint(kind); ink.a = Mathf.Clamp01((1f - progress) * 3f);
            DrawTrajectory(mesh, travel, ink);
            if (progress < ContactProgress) return;
            float t = Mathf.InverseLerp(ContactProgress, 1f, progress);
            Vector2 center = ContactPoint;
            if (outcome == BattleSchoolAttackOutcome.Missed)
            {
                // A miss dissolves beyond the hull; never draw a hit seal on the target.
                for (int i = 0; i < 5; i++)
                {
                    Vector2 at = center + Direction * (12f + t * 45f + i * 7f) + Normal * (i - 2) * 8f;
                    Line(mesh, at, at + Direction * 9f, 1.8f, WithAlpha(ink, (1f - t) * .7f));
                }
                return;
            }
            float contactSize = outcome == BattleSchoolAttackOutcome.Guarded ? .55f : 1f;
            DrawContact(mesh, center, (30f + t * 40f) * size * contactSize, t, ink);
            if (outcome == BattleSchoolAttackOutcome.Guarded)
            {
                Color shield = new Color(.80f, .94f, 1f, Mathf.Sin(t * Mathf.PI));
                Vector2 a = center + new Vector2(-36, 28) * size, b = center + new Vector2(36, 28) * size;
                Vector2 c = center + new Vector2(28, -18) * size, d = center + new Vector2(0, -40) * size;
                Vector2 e = center + new Vector2(-28, -18) * size;
                Line(mesh, a, b, 4f, shield); Line(mesh, b, c, 4f, shield); Line(mesh, c, d, 4f, shield);
                Line(mesh, d, e, 4f, shield); Line(mesh, e, a, 4f, shield);
            }
        }

        private void DrawTrajectory(VertexHelper mesh, float travel, Color tint)
        {
            float tail = Mathf.Max(0f, travel - .42f);
            for (int lane = 0; lane < (kind == BattleSchoolAttackKind.Tide ? 3 : 1); lane++)
            {
                Vector2 previous = Path(tail, lane);
                for (int i = 1; i <= 24; i++)
                {
                    float u = Mathf.Lerp(tail, travel, i / 24f);
                    Vector2 current = Path(u, lane);
                    float weight = i / 24f;
                    Line(mesh, previous, current, (kind == BattleSchoolAttackKind.Blood ? 5f : 3f) * size,
                        WithAlpha(tint, tint.a * weight * .8f));
                    if (kind == BattleSchoolAttackKind.Fire && i % 3 == 0)
                        Triangle(mesh, current, current - Direction * 18f + Normal * 9f * size,
                            current - Direction * 24f - Normal * 4f * size, WithAlpha(tint, weight * tint.a));
                    if (kind == BattleSchoolAttackKind.Curse && i % 5 == 0)
                        Ring(mesh, current, 7f * size, 4, progress * 2f, tint, 2f);
                    previous = current;
                }
            }
            Vector2 head = Path(travel, 1);
            if (kind == BattleSchoolAttackKind.Arcane)
            {
                Ring(mesh, head, 18f * size, 4, progress * 7f, tint, 3f);
                Ring(mesh, head, 9f * size, 4, -progress * 9f, Color.white, 2f);
            }
            else if (kind == BattleSchoolAttackKind.Thunder)
            {
                Line(mesh, head - Normal * 14f, head + Normal * 14f, 2f, Color.white);
                Line(mesh, head - Direction * 9f, head + Direction * 15f, 3f, tint);
            }
        }

        private Vector2 Path(float u, int lane)
        {
            Vector2 point = Vector2.Lerp(start, ContactPoint, u);
            float envelope = Mathf.Sin(u * Mathf.PI);
            switch (kind)
            {
                case BattleSchoolAttackKind.Fire: return point + Normal * (Mathf.Sin(u * 15f) * 8f + 20f) * envelope * size;
                case BattleSchoolAttackKind.Arcane: return point;
                case BattleSchoolAttackKind.Curse: return point + Normal * Mathf.Sin(u * 18f + .4f) * 22f * envelope * size;
                case BattleSchoolAttackKind.Tide: return point + Normal * (Mathf.Sin(u * 12f - progress * 9f) * 15f + (lane - 1) * 13f) * envelope * size;
                case BattleSchoolAttackKind.Thunder:
                    float segment = u * 10f; int step = Mathf.FloorToInt(segment);
                    float zig = Mathf.Lerp(step % 2 == 0 ? -1f : 1f, step % 2 == 0 ? 1f : -1f, segment - step);
                    return point + Normal * zig * envelope * 23f * size;
                default: return point + Normal * Mathf.Sin(u * Mathf.PI) * -36f * size;
            }
        }

        private void DrawContact(VertexHelper mesh, Vector2 center, float radius, float t, Color tint)
        {
            tint.a *= Mathf.Sin(Mathf.Clamp01(t * 1.2f) * Mathf.PI);
            switch (kind)
            {
                case BattleSchoolAttackKind.Fire:
                    for (int i = 0; i < 9; i++)
                    {
                        Vector2 direction = Radial(i * Mathf.PI * 2f / 9f);
                        Vector2 side = new Vector2(-direction.y, direction.x);
                        Triangle(mesh, center + direction * radius, center + direction * 12f + side * 8f,
                            center + direction * 7f - side * 8f, tint);
                    }
                    Ring(mesh, center, radius * .35f, 12, 0f, Color.Lerp(tint, Color.white, .3f), 3f);
                    break;
                case BattleSchoolAttackKind.Arcane:
                    Ring(mesh, center, radius, 6, t * 2f, tint, 3f);
                    Ring(mesh, center, radius * .65f, 3, -t * 3f, tint, 3f);
                    Ring(mesh, center, radius * .30f, 4, t * 5f, tint, 2f);
                    break;
                case BattleSchoolAttackKind.Curse:
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 point = center + Radial(i * Mathf.PI / 3f + t) * radius;
                        Ring(mesh, point, radius * .14f, 4, -t, tint, 2f);
                        Line(mesh, point, center + Radial(i * Mathf.PI / 3f + t) * radius * .4f, 2f, tint);
                    }
                    Line(mesh, center - Vector2.right * 20f, center + Vector2.up * 12f, 3f, tint);
                    Line(mesh, center + Vector2.up * 12f, center + Vector2.right * 20f, 3f, tint);
                    Line(mesh, center + Vector2.right * 20f, center - Vector2.up * 12f, 3f, tint);
                    Line(mesh, center - Vector2.up * 12f, center - Vector2.right * 20f, 3f, tint);
                    Line(mesh, center - Vector2.up * 10f, center + Vector2.up * 10f, 4f, tint);
                    break;
                case BattleSchoolAttackKind.Tide:
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 18; i++)
                        {
                            float x = (i / 18f - .5f) * radius * 2f, next = ((i + 1) / 18f - .5f) * radius * 2f;
                            Line(mesh, center + new Vector2(x, Mathf.Sin(x / 14f + t * 8f) * 9f + (row - 1) * 17f),
                                center + new Vector2(next, Mathf.Sin(next / 14f + t * 8f) * 9f + (row - 1) * 17f), 3f, tint);
                        }
                    break;
                case BattleSchoolAttackKind.Thunder:
                    for (int i = 0; i < 7; i++)
                    {
                        Vector2 d = Radial(i * Mathf.PI * 2f / 7f + .2f), n = new Vector2(-d.y, d.x);
                        Vector2 bend = center + d * radius * .4f + n * 10f;
                        Line(mesh, center, bend, 4f, tint);
                        Line(mesh, bend, center + d * radius, 2.5f, tint);
                    }
                    break;
                case BattleSchoolAttackKind.Blood:
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 3f + t * 1.5f;
                        Vector2 a = center + Radial(angle) * radius, b = center + Radial(angle + .65f) * radius * .35f;
                        Triangle(mesh, a, b, center + Radial(angle + .25f) * radius * .6f, tint);
                    }
                    Ring(mesh, center, radius * .4f, 3, -t, tint, 3f);
                    break;
            }
        }

        private void Ring(VertexHelper mesh, Vector2 center, float radius, int sides, float rotation, Color tint, float width)
        {
            for (int i = 0; i < sides; i++)
                Line(mesh, center + Radial(rotation + i * Mathf.PI * 2f / sides) * radius,
                    center + Radial(rotation + (i + 1) * Mathf.PI * 2f / sides) * radius, width, tint);
        }

        private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < .001f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
            int index = mesh.currentVertCount;
            Vertex(mesh, from - n, tint); Vertex(mesh, from + n, tint); Vertex(mesh, to + n, tint); Vertex(mesh, to - n, tint);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int index = mesh.currentVertCount; Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        private void Vertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            point.x = Mathf.Clamp(point.x, meshBounds.xMin, meshBounds.xMax);
            point.y = Mathf.Clamp(point.y, meshBounds.yMin, meshBounds.yMax);
            mesh.AddVert(point, tint, Vector2.zero);
        }
        private static Vector2 Radial(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        private static Color WithAlpha(Color value, float alpha) { value.a = alpha; return value; }
        private static Vector2 Safe(Vector2 value) => new Vector2(Finite(value.x), Finite(value.y));
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
