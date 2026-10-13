using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Removes the authored blue screen without reading or rewriting artwork pixels.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class CrewArtChromaKey : MonoBehaviour
    {
        public const string ShaderResourcePath = "Shaders/CrewPortraitChromaKey";
        public const float DefaultKeyTolerance = .035f;
        private static readonly int KeyColorId = Shader.PropertyToID("_KeyColorSrgb");
        private static readonly int ToleranceId = Shader.PropertyToID("_KeyTolerance");
        private static readonly int ForegroundToleranceId = Shader.PropertyToID("_ForegroundTolerance");
        private static readonly int DespillId = Shader.PropertyToID("_Despill");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");

        [SerializeField, Range(0f, .25f)] private float keyTolerance = DefaultKeyTolerance;
        [SerializeField, Range(0f, .2f)] private float foregroundTolerance = .05f;
        [SerializeField, Range(0f, 1f)] private float despill = 1f;
        private RawImage target;
        private Material previousMaterial;
        private Material ownedMaterial;
        private Rect appliedUv;
        private float appliedTolerance = -1f;
        private float appliedForegroundTolerance = -1f;
        private float appliedDespill = -1f;
        private bool warnedMissingShader;

        public Material OwnedMaterial => ownedMaterial;
        public float KeyTolerance => keyTolerance;
        public float ForegroundTolerance => foregroundTolerance;
        public float Despill => despill;

        public static CrewArtChromaKey Attach(RawImage image,
            float tolerance = DefaultKeyTolerance, float edgeDespill = 1f, float foregroundBlueTolerance = .05f)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            CrewArtChromaKey key = image.GetComponent<CrewArtChromaKey>();
            if (key == null) key = image.gameObject.AddComponent<CrewArtChromaKey>();
            key.keyTolerance = Mathf.Clamp(tolerance, 0f, .25f);
            key.foregroundTolerance = Mathf.Clamp(foregroundBlueTolerance, 0f, .2f);
            key.despill = Mathf.Clamp01(edgeDespill);
            if (!key.enabled) key.enabled = true;
            key.EnsureMaterial();
            return key;
        }

        private void OnEnable() => EnsureMaterial();

        private void LateUpdate()
        {
            if (ownedMaterial != null) EnsureMaterial();
        }

        private void OnValidate()
        {
            keyTolerance = Mathf.Clamp(keyTolerance, 0f, .25f);
            foregroundTolerance = Mathf.Clamp(foregroundTolerance, 0f, .2f);
            despill = Mathf.Clamp01(despill);
        }

        private void EnsureMaterial()
        {
            if (target == null) target = GetComponent<RawImage>();
            if (target == null) return;
            if (ownedMaterial == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                {
                    if (!warnedMissingShader)
                        Debug.LogWarning("Missing crew chroma-key shader: Resources/" + ShaderResourcePath, this);
                    warnedMissingShader = true;
                    return;
                }
                ownedMaterial = new Material(shader)
                {
                    name = "Crew Art Chroma Key (Instance)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                // A vector deliberately avoids color-property conversions. The
                // shader compares both inputs in encoded sRGB in either project
                // color space. #0080FF is exactly (0, 128/255, 1).
                ownedMaterial.SetVector(KeyColorId, new Vector4(0f, 128f / 255f, 1f, 1f));
                appliedTolerance = -1f;
            }

            bool changed = false;
            if (target.material != ownedMaterial)
            {
                previousMaterial = target.material;
                target.material = ownedMaterial;
                changed = true;
            }
            Rect uv = target.uvRect;
            if (appliedUv != uv || appliedTolerance != keyTolerance ||
                appliedForegroundTolerance != foregroundTolerance || appliedDespill != despill)
            {
                ownedMaterial.SetVector(UvRectId, new Vector4(uv.x, uv.y, uv.width, uv.height));
                ownedMaterial.SetFloat(ToleranceId, keyTolerance);
                ownedMaterial.SetFloat(ForegroundToleranceId, foregroundTolerance);
                ownedMaterial.SetFloat(DespillId, despill);
                appliedUv = uv;
                appliedTolerance = keyTolerance;
                appliedForegroundTolerance = foregroundTolerance;
                appliedDespill = despill;
                changed = true;
            }
            if (changed) target.SetMaterialDirty();
        }

        private void OnDisable()
        {
            // Keep the small owned material across A/B visibility toggles.
            // Disabling this component restores the borrowed original material.
            RestorePreviousMaterial();
        }

        private void OnDestroy()
        {
            RestorePreviousMaterial();
            if (ownedMaterial != null)
            {
                if (Application.isPlaying) Destroy(ownedMaterial);
                else DestroyImmediate(ownedMaterial);
            }
            ownedMaterial = null;
            previousMaterial = null;
        }

        private void RestorePreviousMaterial()
        {
            if (target == null || ownedMaterial == null || target.material != ownedMaterial) return;
            target.material = previousMaterial;
            target.SetMaterialDirty();
        }
    }
}
