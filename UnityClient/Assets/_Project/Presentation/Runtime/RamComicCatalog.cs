using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Full-resolution, separately authored pages for each collision sequence.</summary>
    public static class RamComicCatalog
    {
        public const int SequenceCount = 4;
        public const int PagesPerSequence = 4;
        public const int ResourceCount = SequenceCount * PagesPerSequence;
        public const int SourceWidth = 3344, SourceHeight = 1882;

        // Desktop BC7 imports append two repeated edge rows. Keep the original
        // visible pixels and aspect; other platforms retain their full source UV.
        public static Rect ImageUvRect(Texture2D texture) => texture != null && texture.format == TextureFormat.BC7 &&
            texture.width == SourceWidth && texture.height == 1884
                ? new Rect(0f, 0f, 1f, SourceHeight / 1884f)
                : new Rect(0f, 0f, 1f, 1f);

        private static readonly string[] SequenceNames =
        {
            "ram_player_head_on", "ram_player_broadside",
            "ram_boss_head_on", "ram_boss_broadside",
        };

        public static int SequenceIndex(bool enemyInitiated, NavalBattleAlignment impactAlignment) =>
            (enemyInitiated ? 2 : 0) + (impactAlignment == NavalBattleAlignment.HeadOn ? 0 : 1);

        public static string ImageResource(bool enemyInitiated, NavalBattleAlignment impactAlignment, int page) =>
            ImageResource(SequenceIndex(enemyInitiated, impactAlignment), page);

        public static string ImageResource(int sequence, int page)
        {
            if (sequence < 0 || sequence >= SequenceCount) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (page < 0 || page >= PagesPerSequence) throw new ArgumentOutOfRangeException(nameof(page));
            return "BattleComics/HD/" + SequenceNames[sequence] + "_page" + (page + 1).ToString("00");
        }
    }
}
