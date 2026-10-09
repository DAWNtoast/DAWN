using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Loads the merchant and slot-specific display furniture used by the component shop.</summary>
    public sealed class ComponentShopArtCatalog
    {
        private readonly Texture2D[] mounts;

        public ComponentShopArtCatalog()
        {
            Merchant = Load("merchant_shipwright_v1");
            Plaque = Load("blank_name_plaque_v1");
            mounts = new[]
            {
                Load("mount_bow_v1"),
                Load("mount_sail_v1"),
                Load("mount_cannon_v1"),
                Load("mount_plating_v1"),
                Load("mount_instrument_v1"),
            };
        }

        public Texture2D Merchant { get; }
        public Texture2D Plaque { get; }

        public bool IsAvailable
        {
            get
            {
                if (Merchant == null || Plaque == null)
                {
                    return false;
                }

                for (int index = 0; index < mounts.Length; index += 1)
                {
                    if (mounts[index] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Texture2D Mount(ComponentSlot slot)
        {
            return mounts[Mathf.Clamp((int)slot, 0, mounts.Length - 1)];
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
