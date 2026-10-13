using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Loads the merchant and slot-specific display furniture used by the component shop.</summary>
    public sealed class ComponentShopArtCatalog
    {
        private static readonly string[] MountPaths =
        {
            "mount_bow_v1", "mount_sail_v1", "mount_cannon_v1", "mount_plating_v1", "mount_instrument_v1",
        };
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Func<string, Texture2D> resourceLoader;

        public ComponentShopArtCatalog() : this(Load)
        {
        }

        private ComponentShopArtCatalog(Func<string, Texture2D> loader)
        {
            resourceLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public Texture2D Merchant => LoadCached("merchant_shipwright_v1");
        public Texture2D Plaque => LoadCached("blank_name_plaque_v1");

        /// <summary>Drop cached references; the view and Resources retain ownership of borrowed images.</summary>
        public void ReleaseLoadedTextures() => textures.Clear();

        public bool IsAvailable
        {
            get
            {
                if (Merchant == null || Plaque == null)
                {
                    return false;
                }

                for (int index = 0; index < MountPaths.Length; index += 1)
                {
                    if (LoadCached(MountPaths[index]) == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Texture2D Mount(ComponentSlot slot)
        {
            return LoadCached(MountPaths[Mathf.Clamp((int)slot, 0, MountPaths.Length - 1)]);
        }

        public static string MountName(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Bow: return "船首船台";
                case ComponentSlot.Sail: return "高桅索具架";
                case ComponentSlot.Cannon: return "重炮陈列台";
                case ComponentSlot.Plating: return "船壳龙骨托架";
                default: return "航海仪器台";
            }
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
            Texture2D texture = Resources.Load<Texture2D>("ComponentShopV2/" + fileName);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            return texture;
        }
    }
}
