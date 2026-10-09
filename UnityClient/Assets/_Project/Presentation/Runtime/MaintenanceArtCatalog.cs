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
        public MaintenanceArtCatalog()
        {
            Background = Load("maintenance_boathouse_background_v2");
            InspectionBoard = Load("maintenance_inspection_board_v2");
            SupplyBoard = Load("maintenance_supply_board_v2");
            WaterForeground = Load("maintenance_water_foreground_v2");
        }

        public Texture2D Background { get; }

        public Texture2D InspectionBoard { get; }

        public Texture2D SupplyBoard { get; }

        public Texture2D WaterForeground { get; }

        public bool IsAvailable =>
            Background != null &&
            InspectionBoard != null &&
            SupplyBoard != null &&
            WaterForeground != null;

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
