using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A lightweight, opaque nautical card back for animated deck transfers.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BattleCardFlightBackGraphic : MaskableGraphic
    {
        private static readonly Color Edge = new Color32(9, 18, 25, 255);
        private static readonly Color PaperBottom = new Color32(16, 37, 51, 255);
        private static readonly Color PaperTop = new Color32(29, 62, 76, 255);
        private static readonly Color Brass = new Color32(194, 156, 87, 255);
        private static readonly Color BrassLight = new Color32(231, 204, 143, 255);
        private static readonly Color BrassShade = new Color32(119, 105, 72, 255);
        private static readonly Color Engraving = new Color32(66, 100, 111, 255);

        public void Configure(Color tint)
        {
            tint.a = 1f;
            color = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float unit = Mathf.Min(rect.width, rect.height);
            if (unit <= 0f) return;

            // Each inset remains a closed opaque face, so fast rotation never exposes holes.
            CutFace(mesh, rect, unit * .065f, Edge, Edge);
            Rect outer = Inset(rect, unit * .018f);
            CutFace(mesh, outer, unit * .055f, BrassShade, BrassLight);
            Rect paper = Inset(rect, unit * .032f);
            CutFace(mesh, paper, unit * .048f, PaperBottom, PaperTop);
            Rect inner = Inset(rect, unit * .067f);
            CutBorder(mesh, inner, unit * .045f, unit * .007f, Brass);

            Vector2 center = rect.center;
            float compass = unit * .255f;
            Ring(mesh, center, compass, unit * .008f, BrassShade, 40);
            Ring(mesh, center, compass * .82f, unit * .005f, Engraving, 32);

            // Four long compass points and four short points remain legible during flight.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                Vector2 axis = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                Vector2 side = new Vector2(-axis.y, axis.x);
                float length = compass * (i % 2 == 0 ? .96f : .62f);
                float halfWidth = unit * (i % 2 == 0 ? .037f : .024f);
                Vector2 tip = center + axis * length;
                Vector2 root = center - axis * unit * .018f;
                Triangle(mesh, root, tip, center + side * halfWidth, BrassLight);
                Triangle(mesh, root, center - side * halfWidth, tip, BrassShade);
            }

            Diamond(mesh, center, unit * .049f, unit * .062f, Edge);
            Diamond(mesh, center, unit * .025f, unit * .035f, BrassLight);

            float decorationY = Mathf.Min(rect.height * .325f, rect.height * .5f - unit * .13f);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 at = center + Vector2.up * decorationY * sign;
                Diamond(mesh, at, unit * .025f, unit * .044f, Brass);
                Line(mesh, at + Vector2.left * unit * .065f,
                    at + Vector2.left * unit * .2f, unit * .006f, BrassShade);
                Line(mesh, at + Vector2.right * unit * .065f,
                    at + Vector2.right * unit * .2f, unit * .006f, BrassShade);
            }

            // Small corner chevrons evoke engraved chart borders without dense texture.
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector2 corner = center + new Vector2(
                    x * (rect.width * .5f - unit * .105f),
                    y * (rect.height * .5f - unit * .105f));
                Vector2 inwardX = new Vector2(-x * unit * .095f, 0f);
                Vector2 inwardY = new Vector2(0f, -y * unit * .095f);
                Line(mesh, corner + inwardX, corner, unit * .008f, Brass);
                Line(mesh, corner, corner + inwardY, unit * .008f, Brass);
                Diamond(mesh, corner + (inwardX + inwardY) * .44f,
                    unit * .012f, unit * .016f, BrassShade);
            }
        }

        private void CutFace(VertexHelper mesh, Rect rect, float cut, Color bottom, Color top)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i < 8; i++)
            {
                Vector2 point = CutCorner(rect, cut, i);
                Color tint = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                Vertex(mesh, point, tint);
            }
            for (int i = 1; i < 7; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private void CutBorder(VertexHelper mesh, Rect rect, float cut, float width, Color tint)
        {
            Rect inner = Inset(rect, width);
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                Quad(mesh, CutCorner(rect, cut, i), CutCorner(rect, cut, next),
                    CutCorner(inner, Mathf.Max(0f, cut - width * .586f), next),
                    CutCorner(inner, Mathf.Max(0f, cut - width * .586f), i), tint);
            }
        }

        private void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color tint, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, center + u * radius, center + v * radius,
                    center + v * (radius - width), center + u * (radius - width), tint);
            }
        }

        private void Diamond(VertexHelper mesh, Vector2 at, float halfWidth, float halfHeight, Color tint)
        {
            Quad(mesh, at + Vector2.up * halfHeight, at + Vector2.right * halfWidth,
                at + Vector2.down * halfHeight, at + Vector2.left * halfWidth, tint);
        }

        private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            Quad(mesh, from - side, from + side, to + side, to - side, tint);
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint); Vertex(mesh, d, tint);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private void Vertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            mesh.AddVert(new Vector3(point.x, point.y), tint * color, Vector2.zero);
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return Rect.MinMaxRect(rect.xMin + amount, rect.yMin + amount,
                rect.xMax - amount, rect.yMax - amount);
        }

        private static Vector2 CutCorner(Rect rect, float cut, int index)
        {
            cut = Mathf.Min(cut, Mathf.Min(rect.width, rect.height) * .5f);
            switch (index)
            {
                case 0: return new Vector2(rect.xMin + cut, rect.yMax);
                case 1: return new Vector2(rect.xMax - cut, rect.yMax);
                case 2: return new Vector2(rect.xMax, rect.yMax - cut);
                case 3: return new Vector2(rect.xMax, rect.yMin + cut);
                case 4: return new Vector2(rect.xMax - cut, rect.yMin);
                case 5: return new Vector2(rect.xMin + cut, rect.yMin);
                case 6: return new Vector2(rect.xMin, rect.yMin + cut);
                default: return new Vector2(rect.xMin, rect.yMax - cut);
            }
        }
    }
}
