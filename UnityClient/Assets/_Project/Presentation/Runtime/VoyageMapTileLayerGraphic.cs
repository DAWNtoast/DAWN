using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Batches many non-interactive chart tiles that share one texture into a
    /// single UGUI mesh. This keeps the dense 96 by 64 chart practical in editor
    /// preview and during the render-heavy play-mode flow.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageMapTileLayerGraphic : MaskableGraphic
    {
        [System.Serializable]
        private struct TileQuad
        {
            public Vector2 Minimum;
            public Vector2 Maximum;
            public Vector4 Uv;
            public Color32 Color;
        }

        [SerializeField, HideInInspector]
        private List<TileQuad> tiles = new List<TileQuad>();
        [SerializeField, HideInInspector]
        private Texture sourceTexture;

        public override Texture mainTexture => sourceTexture == null ? s_WhiteTexture : sourceTexture;
        public int TileCount => tiles.Count;

        public void Initialize(Texture texture)
        {
            sourceTexture = texture;
            raycastTarget = false;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void AddTile(Vector2 center, Vector2 size, Color color, bool flipX = false, bool flipY = false)
        {
            AddTileBounds(center - size * 0.5f, center + size * 0.5f, color, flipX, flipY);
        }

        /// <summary>Uses shared cell boundaries without recomputing a far edge from its centre.</summary>
        public void AddTileBounds(Vector2 minimum, Vector2 maximum, Color color, bool flipX = false, bool flipY = false)
        {
            float uMin = flipX ? 1f : 0f;
            float uMax = flipX ? 0f : 1f;
            float vMin = flipY ? 1f : 0f;
            float vMax = flipY ? 0f : 1f;
            tiles.Add(new TileQuad
            {
                Minimum = minimum,
                Maximum = maximum,
                Uv = new Vector4(uMin, vMin, uMax, vMax),
                Color = color,
            });
        }

        public void Complete()
        {
            SetVerticesDirty();
        }

        /// <summary>
        /// Draws a thin frontier inside a cell. Horizontal strips own corner
        /// pixels; vertical strips stop at them, avoiding doubled edge alpha.
        /// Adjacent explored cells therefore cannot overlap their frontier.
        /// </summary>
        public void AddCellFrontier(Vector2 center, Vector2 size, float thickness, Color color,
            bool north, bool east, bool south, bool west)
        {
            thickness = Mathf.Clamp(thickness, 0f, Mathf.Min(size.x, size.y) * 0.5f);
            if (thickness <= 0f) return;
            Vector2 minimum = center - size * 0.5f;
            Vector2 maximum = center + size * 0.5f;
            float sideBottom = south ? minimum.y + thickness : minimum.y;
            float sideTop = north ? maximum.y - thickness : maximum.y;
            if (north)
                AddTileBounds(new Vector2(minimum.x, sideTop), maximum, color);
            if (east && sideTop > sideBottom)
                AddTileBounds(new Vector2(maximum.x - thickness, sideBottom),
                    new Vector2(maximum.x, sideTop), color);
            if (south)
                AddTileBounds(minimum, new Vector2(maximum.x, sideBottom), color);
            if (west && sideTop > sideBottom)
                AddTileBounds(new Vector2(minimum.x, sideBottom),
                    new Vector2(minimum.x + thickness, sideTop), color);
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            UIVertex vertex = UIVertex.simpleVert;
            for (int index = 0; index < tiles.Count; index += 1)
            {
                TileQuad tile = tiles[index];
                int firstVertex = vertexHelper.currentVertCount;
                vertex.color = tile.Color;

                vertex.position = new Vector3(tile.Minimum.x, tile.Minimum.y);
                vertex.uv0 = new Vector2(tile.Uv.x, tile.Uv.y);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(tile.Minimum.x, tile.Maximum.y);
                vertex.uv0 = new Vector2(tile.Uv.x, tile.Uv.w);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(tile.Maximum.x, tile.Maximum.y);
                vertex.uv0 = new Vector2(tile.Uv.z, tile.Uv.w);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(tile.Maximum.x, tile.Minimum.y);
                vertex.uv0 = new Vector2(tile.Uv.z, tile.Uv.y);
                vertexHelper.AddVert(vertex);

                vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
                vertexHelper.AddTriangle(firstVertex + 2, firstVertex + 3, firstVertex);
            }
        }
    }
}
