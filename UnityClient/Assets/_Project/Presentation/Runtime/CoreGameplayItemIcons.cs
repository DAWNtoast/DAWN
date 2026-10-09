using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // The catalog owns shared textures; each view owns only its UI graphic.
        // Sample the atlas cell's aspect, rather than stretching the whole sheet.
        private RawImage CreateItemIcon(string name, Transform parent, ItemIconArt art,
            Vector2 position, float size)
        {
            RawImage icon = CreateRawImage(name, parent, new Vector2(size, size), position,
                art.Texture, art.Uv);
            icon.raycastTarget = false;
            icon.enabled = art.IsAvailable;
            UiImageAspect.Fit(icon, new Vector2(size, size));
            return icon;
        }
    }
}
