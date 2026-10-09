using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored keel contacts, independent of atlas padding and painted wakes.</summary>
    public static class ShipWaterlineLayout
    {
        // Measured at each source cell's horizontal midpoint, from its bottom.
        // These points sit in the lower hull timber (or the ghost hull itself),
        // above the white foam/green glow already painted into some ships.
        private static readonly float[] RosterContacts = { 0.19f, 0.17f, 0.21f, 0.23f };
        private static readonly float[] RegionalContacts = { 0.085f, 0.095f, 0.095f, 0.10f, 0.255f, 0.22f, 0.225f, 0.255f };

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
