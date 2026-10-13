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
        private enum Skin { Wood, Paper, ReadingPaper, Canvas, InvestmentPaper, CollectionPaper }
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
                binding.Image.sprite = binding.Skin switch
                {
                    Skin.Wood => WoodButtonSkin.Face,
                    Skin.Paper => NauticalSurfaceSkin.Paper,
                    Skin.ReadingPaper => NauticalSurfaceSkin.ReadingPaper,
                    Skin.Canvas => NauticalSurfaceSkin.Canvas,
                    Skin.InvestmentPaper => NauticalSurfaceSkin.InvestmentPaper,
                    Skin.CollectionPaper => NauticalSurfaceSkin.CollectionPaper,
                    _ => null
                };
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
                    NauticalSurfaceSkin.PaperPath => Skin.Paper,
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
