using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored keel contacts, independent of atlas padding and painted wakes.</summary>
    public static class ShipWaterlineLayout
    {
        // Measured at each redrawn source cell's central keel, from its bottom.
        // Contact sits 1.5% above the median central hull edge, independently
        // of the 6% packing margin. See ShipArtRedraw20261012/contacts.json.
        private static readonly float[] RosterContacts = { 0.1039f, 0.1048f, 0.1039f, 0.1039f };
        private static readonly float[] RegionalContacts = { 0.1400f, 0.1273f, 0.1224f, 0.1146f, 0.1576f, 0.1136f, 0.2298f, 0.1839f };

        public static Vector2 HullContactLocal(RawImage hull)
        {
            if (hull == null) return Vector2.zero;
            Rect rect = hull.rectTransform.rect;
            Rect uv = hull.uvRect;
            Vector2 sourceContact = new Vector2(0.5f, 0.08f);
            string textureName = hull.texture == null ? string.Empty : hull.texture.name;
            if (textureName == "ship_roster_sheet" || textureName == "ship_sprite_sheet")
            {
                int columns = textureName == "ship_roster_sheet" ? 2 : 4;
                float[] contacts = columns == 2 ? RosterContacts : RegionalContacts;
                int column = Mathf.Clamp(Mathf.FloorToInt(uv.center.x * columns), 0, columns - 1);
                int rowFromBottom = Mathf.Clamp(Mathf.FloorToInt(uv.center.y * 2f), 0, 1);
                int cell = (1 - rowFromBottom) * columns + column;
                sourceContact = new Vector2((column + 0.5f) / columns, (rowFromBottom + contacts[cell]) * 0.5f);
            }
            // Convert the authored texture point through the actual UV window.
            // Inset, deck crops and negative-width mirrored UVs all preserve it.
            float x = Mathf.Abs(uv.width) < 0.00001f ? 0.5f : (sourceContact.x - uv.x) / uv.width;
            float y = Mathf.Abs(uv.height) < 0.00001f ? 0.08f : (sourceContact.y - uv.y) / uv.height;
            return new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y);
        }
    }
}
