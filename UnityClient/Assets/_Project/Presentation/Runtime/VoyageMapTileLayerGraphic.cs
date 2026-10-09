using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Batches many non-interactive chart tiles that share one texture into a
    /// single UGUI mesh. This keeps the dense 48 by 32 map practical in editor
    /// preview and during the render-heavy play-mode flow.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageMapTileLayerGraphic : MaskableGraphic
    {
        private struct TileQuad
        {
            public Rect Rect;
            public Vector4 Uv;
            public Color32 Color;
        }

        private readonly List<TileQuad> tiles = new List<TileQuad>();
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
            float uMin = flipX ? 1f : 0f;
            float uMax = flipX ? 0f : 1f;
            float vMin = flipY ? 1f : 0f;
            float vMax = flipY ? 0f : 1f;
            tiles.Add(new TileQuad
            {
                Rect = new Rect(center - size * 0.5f, size),
                Uv = new Vector4(uMin, vMin, uMax, vMax),
                Color = color,
            });
        }

        public void Complete()
        {
            SetVerticesDirty();
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

                vertex.position = new Vector3(tile.Rect.xMin, tile.Rect.yMin);
                vertex.uv0 = new Vector2(tile.Uv.x, tile.Uv.y);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(tile.Rect.xMin, tile.Rect.yMax);
                vertex.uv0 = new Vector2(tile.Uv.x, tile.Uv.w);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(tile.Rect.xMax, tile.Rect.yMax);
                vertex.uv0 = new Vector2(tile.Uv.z, tile.Uv.w);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(tile.Rect.xMax, tile.Rect.yMin);
                vertex.uv0 = new Vector2(tile.Uv.z, tile.Uv.y);
                vertexHelper.AddVert(vertex);

                vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
                vertexHelper.AddTriangle(firstVertex + 2, firstVertex + 3, firstVertex);
            }
        }
    }
}
