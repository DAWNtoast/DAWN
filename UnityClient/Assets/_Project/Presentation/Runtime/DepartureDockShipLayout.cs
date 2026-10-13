using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Fits the selected vessel to a fixed berth without using atlas padding as its silhouette.</summary>
    public static class DepartureDockShipLayout
    {
        public static readonly Vector2 MaxVisibleSize = new Vector2(950f, 445f);
        public static readonly Vector2 ShipRootSize = new Vector2(900f, 460f);

        // Alpha >= 16 bounds measured offline in each 1536 x 1024 source cell.
        // Coordinates use the PNG's top-left origin; runtime textures need not be readable.
        private static readonly Vector4[] VisiblePixelBounds =
        {
            new Vector4(49f, 17f, 1511f, 1005f),
            new Vector4(123f, 74f, 1426f, 948f),
            new Vector4(191f, 68f, 1377f, 955f),
            new Vector4(169f, 69f, 1392f, 954f),
            new Vector4(184f, 74f, 1422f, 941f),
            new Vector4(127f, 78f, 1408f, 918f),
            new Vector4(136f, 73f, 1419f, 946f),
            new Vector4(142f, 77f, 1405f, 946f),
            new Vector4(128f, 178f, 1411f, 864f),
            new Vector4(169f, 79f, 1391f, 938f),
            new Vector4(131f, 72f, 1404f, 945f),
            new Vector4(104f, 94f, 1431f, 913f),
        };

        private static readonly int[] RegionalCells = { 2, 0, 1, 5, 6, 3, 4, 7 };

        /// <param name="localWaterlineY">Berth waterline in the ship parent's local coordinates.</param>
        public static void Apply(RectTransform ship, RawImage hull, int shipIndex, float localWaterlineY)
        {
            if (ship == null || hull == null || ship.parent == null || hull.texture == null) return;
            Rect uv = hull.uvRect;
            float sampledWidth = Mathf.Abs(uv.width * hull.texture.width);
            float sampledHeight = Mathf.Abs(uv.height * hull.texture.height);
            if (sampledWidth <= 0f || sampledHeight <= 0f) return;

            Rect visibleUv = VisibleSourceUv(Mathf.Clamp(shipIndex, 0, VisiblePixelBounds.Length - 1));
            float visibleWidth = visibleUv.width * hull.texture.width;
            float visibleHeight = visibleUv.height * hull.texture.height;
            float scale = Mathf.Min(MaxVisibleSize.x / visibleWidth, MaxVisibleSize.y / visibleHeight);

            // Derive the complete sampled image from its source every time. Reapplying
            // layout never compounds a previous scale, including after a vessel switch.
            ship.anchorMin = ship.anchorMax = ship.pivot = new Vector2(0.5f, 0.5f);
            ship.sizeDelta = ShipRootSize;
            ship.localScale = Vector3.one;
            ship.localRotation = Quaternion.identity;
            ship.anchoredPosition = Vector2.zero;
            RectTransform hullRect = hull.rectTransform;
            hullRect.anchorMin = hullRect.anchorMax = hullRect.pivot = new Vector2(0.5f, 0.5f);
            hullRect.sizeDelta = new Vector2(sampledWidth, sampledHeight) * scale;
            hullRect.anchoredPosition = Vector2.zero;
            hullRect.localScale = Vector3.one;
            hullRect.localRotation = Quaternion.identity;

            // Map the visible center through the real inset/mirrored UV window. The
            // independently authored keel contact controls height, not the image frame.
            Vector2 normalizedCenter = new Vector2(
                (visibleUv.center.x - uv.x) / uv.width,
                (visibleUv.center.y - uv.y) / uv.height);
            Vector2 visibleCenter = hullRect.rect.min + Vector2.Scale(hullRect.rect.size, normalizedCenter);
            Vector3 center = ship.parent.InverseTransformPoint(hullRect.TransformPoint(visibleCenter));
            Vector3 contact = ship.parent.InverseTransformPoint(
                hullRect.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            ship.anchoredPosition = new Vector2(-center.x, localWaterlineY - contact.y);
        }

        private static Rect VisibleSourceUv(int shipIndex)
        {
            Vector4 pixels = VisiblePixelBounds[shipIndex];
            int columns = shipIndex == 0 ? 1 : shipIndex < 4 ? 2 : 4;
            int rows = shipIndex == 0 ? 1 : 2;
            int cell = shipIndex < 4 ? shipIndex : RegionalCells[shipIndex - 4];
            int column = cell % columns;
            int rowFromBottom = rows - 1 - cell / columns;
            return Rect.MinMaxRect(
                (column + pixels.x / 1536f) / columns,
                (rowFromBottom + 1f - pixels.w / 1024f) / rows,
                (column + pixels.z / 1536f) / columns,
                (rowFromBottom + 1f - pixels.y / 1024f) / rows);
        }
    }
}
