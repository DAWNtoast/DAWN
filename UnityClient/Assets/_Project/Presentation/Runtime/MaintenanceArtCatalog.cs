using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Loads the expanded harbour-maintenance boathouse, its two standing
    /// boards, and the shallow-water foreground used to seat the ship in the
    /// slipway without baking gameplay state into the environment art.
    /// </summary>
    public sealed class MaintenanceArtCatalog
    {
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Func<string, Texture2D> resourceLoader;

        public MaintenanceArtCatalog() : this(Load)
        {
        }

        private MaintenanceArtCatalog(Func<string, Texture2D> loader)
        {
            resourceLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public Texture2D Background => LoadCached("maintenance_boathouse_background_v2");

        public Texture2D InspectionBoard => LoadCached("maintenance_inspection_board_v2");

        public Texture2D SupplyBoard => LoadCached("maintenance_supply_board_v2");

        public Texture2D WaterForeground => LoadCached("maintenance_water_foreground_v2");

        /// <summary>Drop cached references; the view and Resources retain ownership of borrowed images.</summary>
        public void ReleaseLoadedTextures() => textures.Clear();

        public bool IsAvailable =>
            Background != null &&
            InspectionBoard != null &&
            SupplyBoard != null &&
            WaterForeground != null;

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
            Texture2D texture = Resources.Load<Texture2D>("MaintenanceV2/" + fileName);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            return texture;
        }
    }
}
