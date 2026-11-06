using UnityEngine;
using UnityEngine.UI;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>High-resolution accents on the existing, once-consumed field event timeline.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalFieldVfxGraphic : MaskableGraphic
    {
        private NavalFieldMotion motion;
        private NavalTacticalBoardView board;
        private Texture2D atlas;
        private bool hadCues;
        public override Texture mainTexture => atlas != null ? atlas : Texture2D.whiteTexture;

        public void Configure(NavalFieldMotion timeline, NavalTacticalBoardView view)
        {
            motion = timeline; board = view;
            atlas = NavalVfxArtCatalog.Load();
            raycastTarget = false;
            SetAllDirty();
        }

        private void LateUpdate()
        {
            bool hasCues = motion != null && !motion.Suspended && motion.ActiveCues.Count > 0;
            if (hasCues || hadCues) SetVerticesDirty();
            hadCues = hasCues;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (atlas == null || board == null || motion == null || motion.Suspended || !motion.isActiveAndEnabled) return;
            float cellSize = board.CellSizePixels;
            foreach (NavalFieldMotionCue cue in motion.ActiveCues)
            {
                float t = cue.Progress;
                if (t <= 0f || t >= 1f) continue;
                var fact = cue.Event;
                Vector2 center = board.WorldToBoard(new VoyagePoint(fact.X * CoreVoyageSession.NavalGridCellMeters,
                    fact.Y * CoreVoyageSession.NavalGridCellMeters));
                bool triggered = fact.Change == CoreNavalFieldChangeKind.Triggered;
                bool retiring = fact.Change == CoreNavalFieldChangeKind.Removed || fact.Change == CoreNavalFieldChangeKind.Expired ||
                    fact.Change == CoreNavalFieldChangeKind.Overwritten;
                float fade = Mathf.Sin(t * Mathf.PI) * (triggered ? .88f : .56f);
                float scale = retiring ? Mathf.Lerp(.8f, .3f, t) : Mathf.Lerp(.35f, .9f, Mathf.SmoothStep(0f, 1f, t));
                int sprite = fact.Kind == CoreNavalFieldKind.Fire ? 4 : fact.Kind == CoreNavalFieldKind.Arcane ? 5 :
                    fact.Kind == CoreNavalFieldKind.Curse ? 6 : fact.Kind == CoreNavalFieldKind.Tide ? 7 :
                    fact.Kind == CoreNavalFieldKind.Thunder ? 8 : 9;
                // Field events keep their once-consumed timeline and exact cell center.
                // All six schools now evolve through authored frames instead of rotating one still.
                int sequenceRow = 3 + (int)fact.Kind;
                Quad(mesh, NavalVfxArtCatalog.SequenceUv(sequenceRow, retiring ? Mathf.Lerp(.55f, 1f, t) : t), center,
                    cellSize * scale, 0f, fade);
                if (triggered)
                    Quad(mesh, NavalVfxArtCatalog.StaticUv(fact.Kind == CoreNavalFieldKind.Fire ? 1 : fact.Kind == CoreNavalFieldKind.Tide ? 3 : sprite),
                        center, cellSize * (.3f + .55f * t), 0f, fade * .55f);
                else Quad(mesh, NavalVfxArtCatalog.StaticUv(15), center, cellSize * (.35f + .55f * t), 0f, fade * .5f);
            }
        }

        private void Quad(VertexHelper mesh, Rect uv, Vector2 center, float size, float degrees, float alpha)
        {
            float angle = degrees * Mathf.Deg2Rad;
            Vector2 x = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * .5f;
            Vector2 y = new Vector2(-x.y, x.x);
            Color tint = new Color(1f, 1f, 1f, alpha);
            int first = mesh.currentVertCount;
            mesh.AddVert(center - x - y, tint, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(center - x + y, tint, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(center + x + y, tint, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(center + x - y, tint, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
