using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Loads the foreground props and deck-building panels used by the card workshop.</summary>
    public sealed class CardWorkshopArtCatalog
    {
        private static readonly string[] ChestPaths =
        {
            "deck_chest_standard_v1", "deck_chest_artillery_v1", "deck_chest_navigation_v1", "deck_chest_captain_v1",
        };
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Func<string, Texture2D> resourceLoader;

        public CardWorkshopArtCatalog() : this(Load)
        {
        }

        private CardWorkshopArtCatalog(Func<string, Texture2D> loader)
        {
            resourceLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public int ChestCount => ChestPaths.Length;
        public Texture2D CollectionParchment => LoadCached("deck_builder_parchment_v2");
        public Texture2D DeckBuilderCollectionParchment => LoadCached("deck_builder_collection_parchment_v3");
        public Texture2D PirateDeckFrame => LoadCached("deck_builder_pirate_frame_v1");
        public Texture2D UpgradeIcon => LoadCached("workshop_upgrade_hammer_v1");

        public Texture2D StageIcon(int index)
        {
            switch (index)
            {
                case 0: return LoadCached("workshop_preparation_compass_v1");
                case 1: return LoadCached("workshop_naval_cannon_v1");
                default: return LoadCached("workshop_boarding_cutlasses_v1");
            }
        }

        /// <summary>Drop cached references; the view and Resources retain ownership of borrowed images.</summary>
        public void ReleaseLoadedTextures() => textures.Clear();

        public bool IsAvailable
        {
            get
            {
                if (CollectionParchment == null || DeckBuilderCollectionParchment == null || PirateDeckFrame == null)
                {
                    return false;
                }

                for (int index = 0; index < ChestPaths.Length; index += 1)
                {
                    if (LoadCached(ChestPaths[index]) == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Texture2D Chest(int index)
        {
            return LoadCached(ChestPaths[Mathf.Clamp(index, 0, ChestPaths.Length - 1)]);
        }

        private Texture2D LoadCached(string fileName)
        {
            if (!textures.TryGetValue(fileName, out Texture2D texture))
            {
                texture = resourceLoader(fileName);
                textures.Add(fileName, texture);
            }
            return texture;
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
