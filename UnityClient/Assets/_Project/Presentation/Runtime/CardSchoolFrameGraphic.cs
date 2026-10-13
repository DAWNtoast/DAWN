using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// A complete authored transparent frame, including its corner resource
    /// emblems. One quad preserves the artwork's proportions and connections.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardSchoolFrameGraphic : MaskableGraphic
    {
        [SerializeField] private string schoolId;
        [SerializeField] private Texture2D frameTexture;
        [SerializeField] private bool available = true;
        public string SchoolId => CardSchoolArtCatalog.NormalizeSchool(schoolId);
        public override Texture mainTexture => frameTexture != null ? frameTexture : Texture2D.whiteTexture;
        public bool HasAuthoredTexture => frameTexture != null;

        public void Configure(string school, bool canPlay, int powderCost = 0, int cannonballCost = 0, string specialType = "")
        {
            schoolId = school;
            available = canPlay;
            frameTexture = CardSchoolArtCatalog.Frame(school, powderCost, cannonballCost, specialType);
            raycastTarget = false;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (frameTexture == null) return;
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            Color tint = color * (available ? Color.white : new Color(.72f, .72f, .72f, 1f));
            Quad(mesh, rect, new Rect(0f, 0f, 1f, 1f), tint);
        }

        private static void Quad(VertexHelper mesh, Rect box, Rect uv, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(box.xMin, box.yMin), tint, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(new Vector3(box.xMin, box.yMax), tint, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(new Vector3(box.xMax, box.yMax), tint, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(new Vector3(box.xMax, box.yMin), tint, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
