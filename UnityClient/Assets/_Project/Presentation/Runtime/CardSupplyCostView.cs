using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Live supply amounts overlay the resource painted into the complete frame.</summary>
    [DisallowMultipleComponent]
    public sealed class CardSupplyCostView : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(58f, 66f);
        public static readonly Vector2 Position = new Vector2(101f, 108f);
        [SerializeField] private RawImage icon;
        [SerializeField] private Text amount;
        public string ResourceType { get; private set; }
        public Text Amount => amount;

        public void Initialize(RawImage resourceIcon, Text number) { icon = resourceIcon; amount = number; }

        public void Configure(string resource, string value, Vector2 position, bool bakedIcon = false)
        {
            ResourceType = resource;
            ItemIconArt art = ItemIconCatalog.ForCargo(resource);
            icon.texture = art.Texture;
            icon.uvRect = art.Uv;
            icon.enabled = art.IsAvailable && !bakedIcon;
            amount.text = value;
            amount.color = Color.white;
            // Select the same authored font as the energy label, not just its size.
            GameTypography.Apply(amount, bakedIcon ? 24 : 18, FontStyle.Bold);
            amount.rectTransform.anchoredPosition = bakedIcon ? Vector2.zero : new Vector2(0f, -13f);
            RectTransform root = (RectTransform)transform;
            root.sizeDelta = Size;
            root.anchoredPosition = position;
            if (art.IsAvailable)
            {
                float aspect = art.Texture.width * art.Uv.width / (art.Texture.height * art.Uv.height);
                icon.rectTransform.sizeDelta = aspect > 1f
                    ? new Vector2(23f, 23f / aspect) : new Vector2(23f * aspect, 23f);
            }
        }
    }
}
