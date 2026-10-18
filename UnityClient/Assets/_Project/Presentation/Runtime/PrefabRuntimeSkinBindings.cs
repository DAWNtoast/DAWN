using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Reuses the same shared skin objects as procedural views without changing authored geometry.</summary>
    [DisallowMultipleComponent]
    public sealed class PrefabRuntimeSkinBindings : MonoBehaviour
    {
        private enum Skin { Wood, Paper, ReadingPaper, Canvas, InvestmentPaper, CollectionPaper, MarketPaper }
        [Serializable] private struct Binding
        {
            public Image Image;
            public Skin Skin;
        }
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        public void Apply()
        {
            foreach (Binding binding in bindings)
            {
                if (binding.Image == null) continue;
                Image image = binding.Image;
                switch (binding.Skin)
                {
                    case Skin.MarketPaper:
                        float paperScale = image.pixelsPerUnitMultiplier;
                        NauticalSurfaceSkin.ApplyReadingPanel(image);
                        image.pixelsPerUnitMultiplier = paperScale;
                        break;
                    case Skin.ReadingPaper:
                    case Skin.InvestmentPaper:
                    case Skin.CollectionPaper:
                        NauticalSurfaceSkin.ApplyReadingPanel(image);
                        break;
                    case Skin.Paper:
                        Sprite fill = NauticalSurfaceSkin.Paper;
                        if (fill == null) break;
                        // Old exported fills use a larger sprite rect. Keep
                        // their tiling density and semantic tint when migrating.
                        if (image.sprite != null && image.sprite.rect.width > 0f)
                            image.pixelsPerUnitMultiplier *= fill.rect.width / image.sprite.rect.width;
                        image.sprite = fill;
                        image.overrideSprite = null;
                        break;
                    case Skin.Wood:
                        image.sprite = WoodButtonSkin.Face;
                        break;
                    case Skin.Canvas:
                        image.sprite = NauticalSurfaceSkin.Canvas;
                        break;
                }
            }
        }

#if UNITY_EDITOR
        public void CaptureBindings()
        {
            var result = new List<Binding>();
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                Skin? skin = image.sprite.name switch
                {
                    "Carved Walnut Button Face" => Skin.Wood,
                    NauticalSurfaceSkin.PaperFillName => Skin.Paper,
                    NauticalSurfaceSkin.PaperPath => Skin.Paper,
                    MarketShopSkin.PaperResourcePath => Skin.MarketPaper,
                    NauticalSurfaceSkin.ReadingPaperPath => Skin.ReadingPaper,
                    NauticalSurfaceSkin.CanvasPath => Skin.Canvas,
                    NauticalSurfaceSkin.InvestmentPaperPath => Skin.InvestmentPaper,
                    NauticalSurfaceSkin.CollectionPaperPath => Skin.CollectionPaper,
                    _ => null
                };
                if (skin.HasValue) result.Add(new Binding { Image = image, Skin = skin.Value });
            }
            bindings = result.ToArray();
        }
#endif
    }
}
