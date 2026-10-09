using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Landing marks measured on the six side-view Maintenance backgrounds, not
    /// interchangeable screen-space panel positions. Coordinates use the whole
    /// background image: (0,0) is its top-left, (1,1) its bottom-right.
    /// </summary>
    public static class MaintenanceSceneLayoutCatalog
    {
        public const float RegionalWaterlineOpacity = 0.16f;
        public static readonly Vector2 ShipAvailableSize = new Vector2(750f, 430f);
        // The source is 1448 x 1086. Its full width includes the two load-bearing
        // feet; the old 17%-83% crop removed both and made the board float.
        public static readonly Vector2 SupplyBoardSize = new Vector2(960f, 720f);
        // Painted writing surfaces, in each board's unscaled local coordinates.
        // Timber, bunting and the transparent margin are not available UI space.
        public static readonly Rect SupplyPaperSafeRect = new Rect(-265.5f, -171f, 531f, 348f);
        public static readonly Rect SupplyNoticeSafeRect = new Rect(-234f, -234f, 468f, 42f);
        public static readonly Vector2 SupplyLeftFootUv = new Vector2(0.06f, 0.98f);
        public static readonly Vector2 SupplyRightFootUv = new Vector2(0.945f, 0.98f);

        public static MaintenanceSceneLayout ForPort(string portName)
        {
            switch (portName)
            {
                // A large purchasing board stands on the right foreground quay.
                // The freed left berth accommodates a 25% larger side-view ship.
                case "断锚港": return new MaintenanceSceneLayout(
                    new Vector2(0.745f, 0.944f), 1f,
                    new Vector2(0.252f, 0.766f), 1.38f);
                case "白贝港": return new MaintenanceSceneLayout(
                    new Vector2(0.743f, 0.942f), 1f,
                    new Vector2(0.250f, 0.762f), 1.38f);
                case "铜钟港": return new MaintenanceSceneLayout(
                    new Vector2(0.744f, 0.945f), 1f,
                    new Vector2(0.248f, 0.760f), 1.38f);
                case "盐冠港": return new MaintenanceSceneLayout(
                    new Vector2(0.742f, 0.943f), 1f,
                    new Vector2(0.251f, 0.760f), 1.38f);
                case "暮潮港": return new MaintenanceSceneLayout(
                    new Vector2(0.744f, 0.941f), 1f,
                    new Vector2(0.250f, 0.764f), 1.38f);
                case "灰鸥港":
                default: return new MaintenanceSceneLayout(
                    new Vector2(0.744f, 0.944f), 1f,
                    new Vector2(0.250f, 0.760f), 1.38f);
            }
        }

        // Maintenance Panel is centred 55 px below the phase content root.
        // Keep the source-image landing marks aligned with the shared background crop.
        public static Vector2 BackgroundToPanel(Vector2 topLeftNormalized)
        {
            return PhaseSurfaceLayout.SourceUvToContent(topLeftNormalized, new Vector2(1672f, 941f)) -
                new Vector2(0f, PhaseSurfaceLayout.StagePositionY);
        }

        public static Vector2 PanelToBackground(Vector2 panelPosition)
        {
            return PhaseSurfaceLayout.ContentToSourceUv(
                panelPosition + new Vector2(0f, PhaseSurfaceLayout.StagePositionY), new Vector2(1672f, 941f));
        }

        public static Vector2 FootOffset(Vector2 sourceUv, Vector2 size)
        {
            return new Vector2((sourceUv.x - 0.5f) * size.x, (0.5f - sourceUv.y) * size.y);
        }
    }

    public sealed class MaintenanceSceneLayout
    {
        public MaintenanceSceneLayout(Vector2 supplyLanding, float supplyScale, Vector2 waterline, float shipScale)
        {
            SupplyLanding = supplyLanding;
            SupplyScale = supplyScale;
            Waterline = waterline;
            ShipScale = shipScale;
        }

        public Vector2 SupplyLanding { get; }
        public float SupplyScale { get; }
        public Vector2 Waterline { get; }
        public float ShipScale { get; }
        // Existing modular artwork already shows the starboard side, bow +X.
        // Change the room camera, not the equipment transform or UVs.
        public Vector2 BowDirection => Vector2.right;

        public Vector2 SupplyPosition => BoardPosition(SupplyLanding,
            MaintenanceSceneLayoutCatalog.SupplyBoardSize, SupplyScale,
            MaintenanceSceneLayoutCatalog.SupplyLeftFootUv.y);
        public Vector2 WaterlinePosition => MaintenanceSceneLayoutCatalog.BackgroundToPanel(Waterline);
        public Vector2 ShipPosition => WaterlinePosition + new Vector2(0f, 133f * ShipScale);

        private static Vector2 BoardPosition(Vector2 landing, Vector2 size, float scale, float footV)
        {
            return MaintenanceSceneLayoutCatalog.BackgroundToPanel(landing) +
                new Vector2(0f, (footV - 0.5f) * size.y * scale);
        }
    }
}
