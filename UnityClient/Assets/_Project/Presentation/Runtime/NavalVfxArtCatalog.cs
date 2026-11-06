using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Naval-only texture layout: nine eight-frame strips above sixteen reusable accents.
    /// A single texture keeps every layer on the same UI draw surface. Boarding retains v1.
    /// </summary>
    public static class NavalVfxArtCatalog
    {
        public const string TextureResource = "BattleVfx/NavalV2/naval_fx_remaster_v2";
        public const int Columns = 8;
        public const int Rows = 11;
        public const int CellPixels = 256;
        public const int FrameCount = 8;
        public const int SequenceCount = 9;
        public const int StaticCount = 16;
        public const int Muzzle = 0, HullImpact = 1, WaterSplash = 2;
        public const int Fire = 3, Arcane = 4, Curse = 5, Tide = 6, Thunder = 7, Blood = 8;

        private static Texture2D texture;

        public static Texture2D Load()
        {
            if (texture == null) texture = Resources.Load<Texture2D>(TextureResource);
            return texture;
        }

        // Existing graphics own their live reference until their animation has finished.
        public static void ReleaseLoadedTextures() => texture = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => ReleaseLoadedTextures();

        public static int SequenceFrame(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress)) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * FrameCount), 0, FrameCount - 1);
        }

        public static int SchoolSequence(BattleSchoolAttackKind school)
        {
            switch (school)
            {
                case BattleSchoolAttackKind.Fire: return Fire;
                case BattleSchoolAttackKind.Arcane: return Arcane;
                case BattleSchoolAttackKind.Curse: return Curse;
                case BattleSchoolAttackKind.Tide: return Tide;
                case BattleSchoolAttackKind.Thunder: return Thunder;
                default: return Blood;
            }
        }

        /// <summary>Original 0-15 accent identity, repacked across the final two rows.</summary>
        public static Rect StaticUv(int index)
        {
            index = Mathf.Clamp(index, 0, StaticCount - 1);
            return CellUv(index % Columns, SequenceCount + index / Columns);
        }

        /// <summary>Clamped single playback; a completed strip never wraps to its first frame.</summary>
        public static Rect SequenceUv(int sequence, float progress) =>
            CellUv(SequenceFrame(progress), Mathf.Clamp(sequence, 0, SequenceCount - 1));

        private static Rect CellUv(int column, int row)
        {
            const float padX = 2f / (Columns * CellPixels), padY = 2f / (Rows * CellPixels);
            return Rect.MinMaxRect(column / (float)Columns + padX, (Rows - row - 1f) / Rows + padY,
                (column + 1f) / Columns - padX, (Rows - row) / (float)Rows - padY);
        }
    }
}
