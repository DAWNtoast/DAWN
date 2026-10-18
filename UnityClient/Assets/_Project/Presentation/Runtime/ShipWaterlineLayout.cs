using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Per-vessel water contact measured from the current paintings, independent of atlas padding.</summary>
    public static class ShipWaterlineLayout
    {
        // Packed full-cell coordinates. FleetRedesign20261014/install.py records the measurements.
        private static readonly float[] RosterContacts = { 0.132812f, 0.137695f, 0.135742f, 0.133789f };
        private static readonly float[] RegionalContacts = { 0.152344f, 0.151367f, 0.139648f, 0.149414f, 0.179688f, 0.139648f, 0.244141f, 0.180664f };
        private static readonly float[] RosterCenters = { 0.495443f, 0.501628f, 0.531576f, 0.531250f };
        private static readonly float[] RegionalCenters = { 0.533854f, 0.588216f, 0.553711f, 0.541016f, 0.640299f, 0.557292f, 0.642904f, 0.557617f };
        private static readonly float[] RosterWidths = { 0.393880f, 0.458333f, 0.377604f, 0.376953f };
        private static readonly float[] RegionalWidths = { 0.393880f, 0.535156f, 0.453125f, 0.397786f, 0.433594f, 0.487630f, 0.453125f, 0.347656f };
        private const float StarterContact = 0.132812f;
        private const float StarterCenter = 0.495443f;
        private const float StarterWidth = 0.393880f;

        private static Vector3 CellProfile(RawImage hull, out int columns, out int rows, out int column, out int rowFromBottom)
        {
            columns = rows = 1;
            column = rowFromBottom = 0;
            string textureName = hull.texture == null ? string.Empty : hull.texture.name;
            if (textureName == "ship_roster_sheet" || textureName == "ship_sprite_sheet")
            {
                columns = textureName == "ship_roster_sheet" ? 2 : 4;
                rows = 2;
                Rect uv = hull.uvRect;
                column = Mathf.Clamp(Mathf.FloorToInt(uv.center.x * columns), 0, columns - 1);
                rowFromBottom = Mathf.Clamp(Mathf.FloorToInt(uv.center.y * rows), 0, rows - 1);
                int cell = (1 - rowFromBottom) * columns + column;
                return columns == 2
                    ? new Vector3(RosterCenters[cell], RosterContacts[cell], RosterWidths[cell])
                    : new Vector3(RegionalCenters[cell], RegionalContacts[cell], RegionalWidths[cell]);
            }
            return textureName == "light_sloop_base"
                ? new Vector3(StarterCenter, StarterContact, StarterWidth)
                : new Vector3(.5f, .08f, .8f);
        }

        public static Vector2 HullContactLocal(RawImage hull)
        {
            if (hull == null) return Vector2.zero;
            Vector3 profile = CellProfile(hull, out int columns, out int rows, out int column, out int rowFromBottom);
            Vector2 sourceContact = new Vector2((column + profile.x) / columns, (rowFromBottom + profile.y) / rows);
            Rect rect = hull.rectTransform.rect;
            Rect uv = hull.uvRect;
            // UV conversion preserves inset cells, deck crops and mirrored vessels.
            float x = Mathf.Abs(uv.width) < .00001f ? .5f : (sourceContact.x - uv.x) / uv.width;
            float y = Mathf.Abs(uv.height) < .00001f ? .08f : (sourceContact.y - uv.y) / uv.height;
            return new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y);
        }

        /// <summary>The actual immersed hull width, not the flag/bowsprit or full atlas-cell width.</summary>
        public static float HullWidthLocal(RawImage hull)
        {
            if (hull == null) return 0f;
            Vector3 profile = CellProfile(hull, out int columns, out _, out _, out _);
            return hull.rectTransform.rect.width * profile.z / Mathf.Max(.00001f, Mathf.Abs(hull.uvRect.width) * columns);
        }
    }
}
