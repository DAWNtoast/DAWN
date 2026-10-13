using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Fills the viewport with a comic panel, updating its framing after window resizing.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ComicPageGraphic : RawImage
    {
        private Rect sourceUv = new Rect(0f, 0f, 1f, 1f);
        private bool cropToFill = true;

        public void SetPanel(Rect source, bool cropToFill = true)
        {
            sourceUv = source;
            this.cropToFill = cropToFill;
            FitPanel();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            FitPanel();
        }

        private void FitPanel()
        {
            // Lettered full pages must keep edge balloons visible at every window aspect.
            if (!cropToFill)
            {
                uvRect = sourceUv;
                return;
            }
            if (texture == null || rectTransform.rect.width <= 0f || rectTransform.rect.height <= 0f) return;
            float sourceAspect = texture.width * sourceUv.width / (texture.height * sourceUv.height);
            float targetAspect = rectTransform.rect.width / rectTransform.rect.height;
            Vector2 size = sourceUv.size;
            if (sourceAspect > targetAspect) size.x *= targetAspect / sourceAspect;
            else size.y *= sourceAspect / targetAspect;
            uvRect = new Rect(sourceUv.center - size * .5f, size);
        }
    }
}
