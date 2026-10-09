using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Owns a lightweight data texture and a continuous coast material.
    /// Artwork is borrowed; only resources created here are destroyed.</summary>
    [DisallowMultipleComponent]
    public sealed class VoyageChartSurfaceGraphic : RawImage
    {
        private Texture2D coastTexture;
        private Material coastMaterial;
        public VoyageChartCoastField Field { get; private set; }

        public void Initialize(CoreVoyageMap map, VoyageMapArtCatalog art, int route)
        {
            ReleaseOwnedResources();
            Field = art.GetCoastField(map);
            coastTexture = new Texture2D(Field.Width, Field.Height, TextureFormat.RGBA32, false, true)
            {
                name = "Seed Coast Field " + Field.GeometryFingerprint,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            coastTexture.SetPixels32(Field.Pixels);
            coastTexture.Apply(false, true);
            texture = coastTexture;
            Shader shader = Resources.Load<Shader>("VoyageMapV2/VoyageChartCoast");
            if (shader == null)
            {
                Debug.LogError("Missing continuous voyage coast shader.");
                enabled = false;
                return;
            }
            coastMaterial = new Material(shader) { name = "Voyage Coast " + Field.GeometryFingerprint, hideFlags = HideFlags.HideAndDontSave };
            coastMaterial.SetTexture("_LandArt", art.GroundDetail != null ? art.GroundDetail : art.TerrainFor(SeaTerrainType.Island));
            coastMaterial.SetTexture("_WaterArt", art.ChartBase);
            coastMaterial.SetVector("_Grid", new Vector4(CoreVoyageMap.GridColumns, CoreVoyageMap.GridRows, 1f / Field.Width, 1f / Field.Height));
            coastMaterial.SetVector("_Seed", new Vector4((map.Hash & 1023u) * .0137f, ((map.Hash >> 10) & 1023u) * .0191f, route, 0f));
            material = coastMaterial;
            color = Color.white;
            raycastTarget = false;
        }

        protected override void OnDestroy()
        {
            ReleaseOwnedResources();
            base.OnDestroy();
        }

        private void ReleaseOwnedResources()
        {
            material = null;
            texture = null;
            if (coastMaterial != null) Release(coastMaterial);
            if (coastTexture != null) Release(coastTexture);
            coastMaterial = null;
            coastTexture = null;
            Field = null;
        }

        private static void Release(Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
