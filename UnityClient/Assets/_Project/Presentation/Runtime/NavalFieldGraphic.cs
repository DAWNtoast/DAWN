using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public readonly struct NavalFieldVisualCell
    {
        public readonly VoyagePoint Position;
        public readonly CoreNavalFieldKind Kind;
        public readonly Color Fill, Border;
        public readonly int DirectionX, DirectionY;
        public readonly bool Pattern, Invalid;

        public NavalFieldVisualCell(VoyagePoint position, CoreNavalFieldKind kind, Color fill, Color border,
            bool pattern = false, int directionX = 0, int directionY = 0, bool invalid = false)
        {
            Position = position; Kind = kind; Fill = fill; Border = border;
            Pattern = pattern; DirectionX = directionX; DirectionY = directionY; Invalid = invalid;
        }
    }

    /// <summary>World-anchored field tiles and placement outlines; never owns input or game state.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalFieldGraphic : MaskableGraphic
    {
        private NavalTacticalBoardView board;
        private readonly List<NavalFieldVisualCell> cells = new List<NavalFieldVisualCell>();
        private IReadOnlyList<NavalFieldMotionCue> cues;
        private float motionTime;
        private bool hadMotionGeometry;
        public IReadOnlyList<NavalFieldVisualCell> Cells => cells;

        public void SetMotion(float seconds, IReadOnlyList<NavalFieldMotionCue> effects)
        {
            motionTime = seconds; cues = effects;
            bool hasGeometry = cells.Count > 0 || (effects != null && effects.Count > 0);
            if (!hasGeometry && !hadMotionGeometry) return;
            hadMotionGeometry = hasGeometry;
            SetVerticesDirty();
        }

        public void Configure(NavalTacticalBoardView view, IEnumerable<NavalFieldVisualCell> source)
        {
            board = view; cells.Clear();
            if (source != null) cells.AddRange(source);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (board == null) return;
            foreach (NavalFieldVisualCell cell in cells)
            {
                Vector2 a = Project(cell.Position, -.46f, -.46f), b = Project(cell.Position, -.46f, .46f);
                Vector2 c = Project(cell.Position, .46f, .46f), d = Project(cell.Position, .46f, -.46f);
                int start = helper.currentVertCount;
                helper.AddVert(a, cell.Fill, Vector2.zero); helper.AddVert(b, cell.Fill, Vector2.zero);
                helper.AddVert(c, cell.Fill, Vector2.zero); helper.AddVert(d, cell.Fill, Vector2.zero);
                helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
                Line(helper, a, b, cell.Border); Line(helper, b, c, cell.Border);
                Line(helper, c, d, cell.Border); Line(helper, d, a, cell.Border);
                if (cell.Invalid) { Line(helper, a, c, cell.Border); Line(helper, b, d, cell.Border); }
                if (cell.Pattern) DrawPattern(helper, cell);
                if (cell.DirectionX != 0 || cell.DirectionY != 0)
                {
                    Vector2 from = Project(cell.Position, -cell.DirectionX * .22f, -cell.DirectionY * .22f);
                    Vector2 to = Project(cell.Position, cell.DirectionX * .28f, cell.DirectionY * .28f);
                    Vector2 direction = (to - from).normalized, side = new Vector2(-direction.y, direction.x);
                    Line(helper, from, to, cell.Border, 3f);
                    Line(helper, to, to - direction * 12f + side * 7f, cell.Border, 3f);
                    Line(helper, to, to - direction * 12f - side * 7f, cell.Border, 3f);
                }
            }
            if (cues != null)
                for (int i = 0; i < cues.Count; i++) DrawCue(helper, cues[i]);
        }

        private void DrawPattern(VertexHelper helper, NavalFieldVisualCell cell)
        {
            Color tint = cell.Border; tint.a *= .52f;
            float phase = cell.Position.X * .013f + cell.Position.Y * .019f;
            float time = motionTime;
            switch (cell.Kind)
            {
                case CoreNavalFieldKind.Fire:
                    for (int i = -1; i <= 1; i++)
                    {
                        float sway = Mathf.Sin(time * 2.8f + phase + i * 1.8f) * .035f;
                        float rise = .04f * Mathf.Sin(time * 3.2f + phase + i);
                        Stroke(helper, cell, i * .23f - .08f, -.23f, i * .23f + .06f + sway, 0f, tint);
                        Stroke(helper, cell, i * .23f + .06f + sway, 0f, i * .23f + sway, .23f + rise, tint);
                        float ember = Mathf.Repeat(time * .24f + i * .31f + .5f, 1f);
                        Color spark = tint; spark.a *= Mathf.Sin(ember * Mathf.PI);
                        Stroke(helper, cell, i * .2f, -.12f + ember * .44f, i * .2f + .018f, -.08f + ember * .44f, spark);
                    }
                    break;
                case CoreNavalFieldKind.Arcane:
                    Ring(helper, cell.Position, .29f, time * .23f + phase, 4, tint, 2f);
                    Ring(helper, cell.Position, .17f, -time * .31f - phase, 4, tint, 2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI * .5f + time * .23f + phase;
                        Stroke(helper, cell, Mathf.Cos(angle) * .32f, Mathf.Sin(angle) * .32f,
                            Mathf.Cos(angle) * .37f, Mathf.Sin(angle) * .37f, tint);
                    }
                    break;
                case CoreNavalFieldKind.Curse:
                    Stroke(helper, cell, -.26f, -.25f, .26f, .25f, tint);
                    Stroke(helper, cell, -.26f, .25f, .26f, -.25f, tint);
                    Stroke(helper, cell, -.30f, .10f, .30f, .10f, tint);
                    for (int i = 0; i < 3; i++)
                    {
                        float inward = Mathf.Repeat(time * .14f + i / 3f + phase, 1f);
                        float radius = .39f - inward * .22f;
                        float angle = i * Mathf.PI * 2f / 3f + phase;
                        Color wisp = tint; wisp.a *= Mathf.Sin(inward * Mathf.PI);
                        Stroke(helper, cell, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                            Mathf.Cos(angle + .2f) * (radius - .04f), Mathf.Sin(angle + .2f) * (radius - .04f), wisp);
                    }
                    break;
                case CoreNavalFieldKind.Thunder:
                    tint.a *= .48f + .52f * Mathf.Pow(.5f + .5f * Mathf.Sin(time * 3.7f + phase), 8f);
                    for (int i = -1; i <= 1; i++)
                    {
                        float jitter = .015f * Mathf.Sin(time * 9f + phase + i * 2f);
                        Stroke(helper, cell, i * .23f + .08f, .30f, i * .23f - .05f + jitter, .02f, tint);
                        Stroke(helper, cell, i * .23f - .05f + jitter, .02f, i * .23f + .06f - jitter, -.02f, tint);
                        Stroke(helper, cell, i * .23f + .06f - jitter, -.02f, i * .23f - .08f, -.30f, tint);
                    }
                    break;
                case CoreNavalFieldKind.Tide:
                    Vector2 direction = new Vector2(cell.DirectionX, cell.DirectionY);
                    if (direction.sqrMagnitude < .01f) direction = Vector2.right;
                    Vector2 side = new Vector2(-direction.y, direction.x);
                    for (int row = 0; row < 3; row++)
                    {
                        float travel = Mathf.Repeat(time * .2f + row / 3f + phase, 1f);
                        Vector2 center = direction * (-.32f + travel * .64f);
                        Color wave = tint; wave.a *= Mathf.Sin(travel * Mathf.PI);
                        Vector2 previous = center - side * .30f;
                        for (int i = 1; i <= 6; i++)
                        {
                            Vector2 next = center + side * (-.30f + i * .1f) + direction * (.025f * Mathf.Sin(i * 1.2f + time));
                            Stroke(helper, cell, previous.x, previous.y, next.x, next.y, wave);
                            previous = next;
                        }
                    }
                    break;
                case CoreNavalFieldKind.Blood:
                    float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 2.4f + phase)), 6f);
                    float scale = 1f + .07f * beat;
                    tint.a *= .75f + .25f * beat;
                    Stroke(helper, cell, 0f, .31f * scale, -.20f * scale, -.05f, tint);
                    Stroke(helper, cell, -.20f * scale, -.05f, -.13f * scale, -.24f * scale, tint);
                    Stroke(helper, cell, -.13f * scale, -.24f * scale, .13f * scale, -.24f * scale, tint);
                    Stroke(helper, cell, .13f * scale, -.24f * scale, .20f * scale, -.05f, tint);
                    Stroke(helper, cell, .20f * scale, -.05f, 0f, .31f * scale, tint);
                    Stroke(helper, cell, -.07f, -.09f, 0f, -.01f - beat * .03f, tint);
                    Stroke(helper, cell, 0f, -.01f - beat * .03f, .07f, -.09f, tint);
                    break;
            }
        }

        private void DrawCue(VertexHelper helper, NavalFieldMotionCue cue)
        {
            CoreNavalFieldPresentationEvent fact = cue.Event;
            VoyagePoint position = new VoyagePoint(fact.X * CoreVoyageSession.NavalGridCellMeters,
                fact.Y * CoreVoyageSession.NavalGridCellMeters);
            float t = cue.Progress;
            Color tint = Tint(fact.Kind);
            if (fact.Change == CoreNavalFieldChangeKind.Triggered)
            {
                tint = Color.Lerp(tint, Color.white, .32f);
                tint.a = Mathf.Sin(Mathf.PI * t) * .92f;
                Ring(helper, position, .10f + .30f * t, 0f, 12, tint, 3f);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    float radius = .12f + .22f * t;
                    Line(helper, Project(position, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                        Project(position, Mathf.Cos(angle) * (radius + .055f), Mathf.Sin(angle) * (radius + .055f)), tint, 2.8f);
                }
            }
            else if (fact.Change == CoreNavalFieldChangeKind.Placed)
            {
                tint.a = (1f - t) * .8f;
                Ring(helper, position, .06f + .36f * (1f - Mathf.Pow(1f - t, 3f)), Mathf.PI * .25f, 4, tint, 3f);
            }
            else
            {
                tint.a = (1f - t) * .62f;
                float radius = fact.Change == CoreNavalFieldChangeKind.Overwritten ? .38f * (1f - t) : .27f + .12f * t;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * .25f;
                    Line(helper, Project(position, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                        Project(position, Mathf.Cos(angle + .27f) * radius, Mathf.Sin(angle + .27f) * radius), tint, 2f);
                }
            }
        }

        private void Ring(VertexHelper helper, VoyagePoint position, float radius, float rotation, int sides, Color tint, float width)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = rotation + i * Mathf.PI * 2f / sides, b = rotation + (i + 1) * Mathf.PI * 2f / sides;
                Line(helper, Project(position, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius),
                    Project(position, Mathf.Cos(b) * radius, Mathf.Sin(b) * radius), tint, width);
            }
        }

        private void Stroke(VertexHelper helper, NavalFieldVisualCell cell, float x1, float y1, float x2, float y2, Color tint) =>
            Line(helper, Project(cell.Position, x1, y1), Project(cell.Position, x2, y2), tint);
        private Vector2 Project(VoyagePoint center, float x, float y) => board.WorldToBoard(new VoyagePoint(
            center.X + x * CoreVoyageSession.NavalGridCellMeters, center.Y + y * CoreVoyageSession.NavalGridCellMeters));
        private static void Line(VertexHelper helper, Vector2 a, Vector2 b, Color tint, float width = 2f) =>
            NavalTacticalTrackGraphic.AddLine(helper, a, b, width, tint);

        public static Color Tint(CoreNavalFieldKind kind)
        {
            switch (kind)
            {
                case CoreNavalFieldKind.Fire: return new Color(1f, .39f, .12f);
                case CoreNavalFieldKind.Arcane: return new Color(.75f, .57f, 1f);
                case CoreNavalFieldKind.Curse: return new Color(.67f, .85f, .25f);
                case CoreNavalFieldKind.Tide: return new Color(.16f, .85f, .95f);
                case CoreNavalFieldKind.Thunder: return new Color(1f, .88f, .28f);
                default: return new Color(1f, .25f, .47f);
            }
        }

        public static string ShortName(CoreNavalFieldKind kind)
        {
            switch (kind)
            {
                case CoreNavalFieldKind.Fire: return "火";
                case CoreNavalFieldKind.Arcane: return "阵";
                case CoreNavalFieldKind.Curse: return "咒";
                case CoreNavalFieldKind.Tide: return "流";
                case CoreNavalFieldKind.Thunder: return "雷";
                default: return "血";
            }
        }
    }
}
