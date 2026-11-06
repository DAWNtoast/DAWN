using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Complete transparent field paintings; never trim their authored sea gaps.</summary>
    public static class NavalFieldArtCatalog
    {
        private static readonly Texture2D[] textures = new Texture2D[6];

        /// <summary>Drop borrowed lookups; live field paintings retain their own textures.</summary>
        public static void ReleaseLoadedTextures() => System.Array.Clear(textures, 0, textures.Length);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => ReleaseLoadedTextures();

        public static string ResourcePath(CoreNavalFieldKind kind)
        {
            string name;
            switch (kind)
            {
                case CoreNavalFieldKind.Fire: name = "fire"; break;
                case CoreNavalFieldKind.Arcane: name = "arcane"; break;
                case CoreNavalFieldKind.Curse: name = "curse"; break;
                case CoreNavalFieldKind.Tide: name = "tide"; break;
                case CoreNavalFieldKind.Thunder: name = "thunder"; break;
                case CoreNavalFieldKind.Blood: name = "blood"; break;
                default: return string.Empty;
            }
            return "NavalTactics/Fields/field_" + name + "_v2";
        }

        public static Texture2D ForField(CoreNavalFieldKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= textures.Length) return null;
            if (textures[index] == null) textures[index] = Resources.Load<Texture2D>(ResourcePath(kind));
            return textures[index];
        }
    }
}
