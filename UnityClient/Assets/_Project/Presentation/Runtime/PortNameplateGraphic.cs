using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// A compact brass-edged facility label. Geometry is resolution independent,
    /// while Image inheritance retains the existing UI tint and hit-area behavior.
    /// </summary>
    [AddComponentMenu("Shattered Sail/UI/Port Nameplate Graphic")]
    public sealed class PortNameplateGraphic : Image
    {
        private const int CircleSegments = 24;

        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            float scale = Mathf.Min(bounds.height / 48f, bounds.width / 80f);
            Rect body = Rect.MinMaxRect(bounds.xMin + scale, bounds.yMin + 5f * scale,
                bounds.xMax - scale, bounds.yMax - 2f * scale);
            float cut = 8.5f * scale;

            // A tiny central setting visually joins the label to its location.
            Vector2 pendant = new Vector2(bounds.center.x, body.yMin + .2f * scale);
            AddDiamond(mesh, pendant, 5f * scale, new Color(.11f, .075f, .025f, .96f));
            AddDiamond(mesh, pendant, 3.7f * scale, new Color(.68f, .49f, .22f, 1f));
            AddDiamond(mesh, pendant, 1.7f * scale, new Color(.23f, .18f, .09f, 1f));

            // Closely nested bevels provide a narrow metal edge with no wood panel.
            AddChamferedFace(mesh, body, cut,
                new Color(.075f, .071f, .047f, 1f), new Color(.035f, .038f, .031f, 1f));
            AddChamferedFace(mesh, Inset(body, .85f * scale), cut - .35f * scale,
                new Color(.79f, .66f, .40f, 1f), new Color(.37f, .24f, .095f, 1f));
            AddChamferedFace(mesh, Inset(body, 2.05f * scale), cut - .85f * scale,
                new Color(.29f, .21f, .10f, 1f), new Color(.54f, .38f, .17f, 1f));
            AddChamferedFace(mesh, Inset(body, 2.9f * scale), cut - 1.25f * scale,
                new Color(.54f, .44f, .25f, 1f), new Color(.22f, .16f, .08f, 1f));
            AddChamferedFace(mesh, Inset(body, 3.8f * scale), cut - 1.7f * scale,
                new Color(.078f, .105f, .086f, 1f), new Color(.024f, .043f, .038f, 1f));

            // The stud is deliberately small; the remaining face belongs to text.
            Vector2 stud = new Vector2(bounds.xMin + 23f * scale, body.center.y);
            AddDisc(mesh, stud, 9.5f * scale,
                new Color(.15f, .105f, .04f, 1f), new Color(.028f, .027f, .015f, 1f),
                new Color(.07f, .065f, .035f, 1f));
            AddDisc(mesh, stud, 8.3f * scale,
                new Color(.93f, .78f, .42f, 1f), new Color(.40f, .23f, .065f, 1f),
                new Color(.73f, .49f, .16f, 1f));
            AddDisc(mesh, stud, 6.8f * scale,
                new Color(.34f, .22f, .065f, 1f), new Color(.69f, .47f, .17f, 1f),
                new Color(.25f, .15f, .03f, 1f));
            AddDisc(mesh, stud, 5.4f * scale,
                new Color(.95f, .77f, .38f, 1f), new Color(.52f, .29f, .055f, 1f),
                new Color(.95f, .67f, .22f, 1f));
            AddDisc(mesh, stud + new Vector2(-1.35f, 1.8f) * scale, 2.25f * scale,
                new Color(1f, .92f, .65f, .75f), new Color(1f, .81f, .37f, 0f),
                new Color(1f, .9f, .61f, .6f));
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return Rect.MinMaxRect(rect.xMin + amount, rect.yMin + amount,
                rect.xMax - amount, rect.yMax - amount);
        }

        private void AddChamferedFace(VertexHelper mesh, Rect rect, float cut,
            Color top, Color bottom)
        {
            cut = Mathf.Clamp(cut, 0f, Mathf.Min(rect.width, rect.height) * .5f);
            int start = mesh.currentVertCount;
            AddVertex(mesh, rect.center, Color.Lerp(bottom, top, .5f));
            for (int corner = 0; corner < 8; corner++)
            {
                Vector2 point = ChamferedCorner(rect, cut, corner);
                AddVertex(mesh, point, Color.Lerp(bottom, top,
                    Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)));
            }
            for (int corner = 0; corner < 8; corner++)
                mesh.AddTriangle(start, start + corner + 1, start + (corner + 1) % 8 + 1);
        }

        private static Vector2 ChamferedCorner(Rect rect, float cut, int corner)
        {
            switch (corner)
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

        private void AddDisc(VertexHelper mesh, Vector2 center, float radius,
            Color top, Color bottom, Color middle)
        {
            int start = mesh.currentVertCount;
            AddVertex(mesh, center, middle);
            for (int step = 0; step < CircleSegments; step++)
            {
                float angle = -step * Mathf.PI * 2f / CircleSegments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(mesh, center + direction * radius,
                    Color.Lerp(bottom, top, direction.y * .5f + .5f));
            }
            for (int step = 0; step < CircleSegments; step++)
                mesh.AddTriangle(start, start + step + 1, start + (step + 1) % CircleSegments + 1);
        }

        private void AddDiamond(VertexHelper mesh, Vector2 center, float radius, Color shade)
        {
            int start = mesh.currentVertCount;
            AddVertex(mesh, center + Vector2.up * radius, shade);
            AddVertex(mesh, center + Vector2.right * radius, shade);
            AddVertex(mesh, center + Vector2.down * radius, shade);
            AddVertex(mesh, center + Vector2.left * radius, shade);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddVertex(VertexHelper mesh, Vector2 point, Color shade)
        {
            mesh.AddVert(new Vector3(point.x, point.y, 0f), shade * color, Vector2.zero);
        }
    }
}
