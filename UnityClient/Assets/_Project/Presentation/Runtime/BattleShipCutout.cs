using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Removes only the known navy backgrounds of the two ship atlases at
    /// draw time. The source texture, atlas UVs and shared materials are never
    /// edited. Known atlas separators are removed independently of the narrow
    /// color key so mirrored/turning ships cannot reveal the black cell frame.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RawImage))]
    public sealed class BattleShipCutout : MonoBehaviour
    {
        public const string ShaderResourcePath = "Shaders/BattleShipCutout";
        private static readonly Vector4 KeyGamma = new Vector4(0.5f / 255f, 12.5f / 255f, 32f / 255f, 0f);
        private static readonly Vector4 KeyTolerance = new Vector4(1.5f / 255f, 1.5f / 255f, 2f / 255f, 1.75f);
        // LegacyDemo's 4x2 sheet has a different matte: source PNG samples are
        // R0-1/G9-11/B24-27. BC compression quantizes green to 8/12 and blue to
        // 24/27; this still excludes black sails, brown timber and green wakes.
        private static readonly Vector4 LegacyKeyGamma = new Vector4(0.5f / 255f, 10f / 255f, 25.5f / 255f, 0f);
        private static readonly Vector4 LegacyKeyTolerance = new Vector4(1.5f / 255f, 3f / 255f, 3f / 255f, 1.75f);
        private RawImage target;
        private Material previousMaterial;
        private Material ownedMaterial;
        private Texture configuredTexture;
        private FilterMode configuredFilterMode;
        public Material OwnedMaterial => ownedMaterial;

        public static BattleShipCutout Attach(RawImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            BattleShipCutout cutout = image.GetComponent<BattleShipCutout>();
            if (cutout == null) cutout = image.gameObject.AddComponent<BattleShipCutout>();
            cutout.Configure(image);
            return cutout;
        }

        public void Configure(RawImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.gameObject != gameObject)
                throw new ArgumentException("A battle ship cutout must belong to its RawImage's GameObject.", nameof(image));
            ReleaseOwnedMaterial();
            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                Debug.LogError("Missing battle ship cutout shader: Resources/" + ShaderResourcePath, this);
                return;
            }
            target = image;
            previousMaterial = image.material;
            ownedMaterial = new Material(shader)
            {
                name = "Battle Ship Navy Cutout (Instance)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            // Vector properties are authored sRGB values, not Color properties
            // that Unity might automatically convert for a linear project.
            image.material = ownedMaterial;
            ConfigureAtlasBounds();
            image.SetMaterialDirty();
        }

        private void LateUpdate()
        {
            // Some callers attach this component before assigning the atlas.
            // A texture identity check avoids rebuilding materials during turns.
            if (target != null && (target.texture != configuredTexture ||
                (target.texture != null && target.texture.filterMode != configuredFilterMode))) ConfigureAtlasBounds();
        }

        private void ConfigureAtlasBounds()
        {
            if (target == null || ownedMaterial == null) return;
            configuredTexture = target.texture;
            configuredFilterMode = configuredTexture == null ? FilterMode.Point : configuredTexture.filterMode;
            // ship_roster_sheet is a 2x2 atlas with opaque black separators and
            // compression bleed. Keep the same safe ten-texel margin as the
            // battle's inset UVs, without changing the requested UV rectangle.
            bool roster = configuredTexture != null && configuredTexture.name == "ship_roster_sheet";
            bool legacy = configuredTexture != null && configuredTexture.name == "ship_sprite_sheet";
            // Editable previews persist materials by name. Keep the two atlas
            // keys in separate assets instead of reusing the old navy material.
            ownedMaterial.name = legacy ? "Regional Ship Navy Cutout (Instance)" : "Battle Ship Navy Cutout (Instance)";
            ownedMaterial.SetVector("_KeyGamma", legacy ? LegacyKeyGamma : KeyGamma);
            ownedMaterial.SetVector("_KeyTolerance", legacy ? LegacyKeyTolerance : KeyTolerance);
            // Legacy artwork also reaches across a cell boundary (the ghost
            // flag enters the sloop cell). Isolate cells before interpolation
            // so mirrored turns cannot borrow the neighbouring ship's pixels.
            ownedMaterial.SetVector("_AtlasGrid", roster ? new Vector4(2f, 2f, 10f, 10f) :
                legacy ? new Vector4(4f, 2f, 10f, 10f) : Vector4.zero);
            ownedMaterial.SetFloat("_SmoothCutout", configuredFilterMode == FilterMode.Point ? 0f : 1f);
        }

        /// <summary>CPU reference for the exact narrow key used by the shader.</summary>
        public static float CoverageForGammaColor(Color encoded)
        {
            return CoverageForGammaColor(encoded, false);
        }

        public static float CoverageForGammaColor(Color encoded, bool legacyAtlas)
        {
            Vector4 key = legacyAtlas ? LegacyKeyGamma : KeyGamma;
            Vector4 tolerance = legacyAtlas ? LegacyKeyTolerance : KeyTolerance;
            float distance = Mathf.Max(Mathf.Abs(encoded.r - key.x) / tolerance.x,
                Mathf.Abs(encoded.g - key.y) / tolerance.y,
                Mathf.Abs(encoded.b - key.z) / tolerance.z);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, tolerance.w, distance)) * encoded.a;
        }

        private void OnDestroy()
        {
            ReleaseOwnedMaterial();
        }

        private void ReleaseOwnedMaterial()
        {
            if (target != null && target.material == ownedMaterial)
            {
                target.material = previousMaterial;
                target.SetMaterialDirty();
            }
            if (ownedMaterial != null)
            {
                if (Application.isPlaying) Destroy(ownedMaterial);
                else DestroyImmediate(ownedMaterial);
            }
            ownedMaterial = null;
            previousMaterial = null;
            target = null;
            configuredTexture = null;
        }
    }
}
