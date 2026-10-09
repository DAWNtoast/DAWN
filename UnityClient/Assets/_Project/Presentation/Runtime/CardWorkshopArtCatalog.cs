using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Loads the foreground props and deck-building panels used by the card workshop.</summary>
    public sealed class CardWorkshopArtCatalog
    {
        private readonly Texture2D[] chests;

        public CardWorkshopArtCatalog()
        {
            chests = new[]
            {
                Load("deck_chest_standard_v1"),
                Load("deck_chest_artillery_v1"),
                Load("deck_chest_navigation_v1"),
                Load("deck_chest_captain_v1"),
            };

            CollectionParchment = Load("deck_builder_parchment_v2");
            PirateDeckFrame = Load("deck_builder_pirate_frame_v1");
        }

        public int ChestCount => chests.Length;
        public Texture2D CollectionParchment { get; }
        public Texture2D PirateDeckFrame { get; }

        public bool IsAvailable
        {
            get
            {
                if (CollectionParchment == null || PirateDeckFrame == null)
                {
                    return false;
                }

                for (int index = 0; index < chests.Length; index += 1)
                {
                    if (chests[index] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Texture2D Chest(int index)
        {
            return chests[Mathf.Clamp(index, 0, chests.Length - 1)];
        }

        private static Texture2D Load(string fileName)
        {
            Texture2D texture = Resources.Load<Texture2D>("CardWorkshopV2/" + fileName);
            if (texture != null)
            {
                texture.filterMode = fileName.StartsWith("deck_chest_") ? FilterMode.Trilinear : FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            return texture;
        }
    }
}
