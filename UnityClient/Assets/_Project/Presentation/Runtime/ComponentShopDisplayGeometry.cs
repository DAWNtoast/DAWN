using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Support-free component displays. Preserve each silhouette's native aspect
    /// and direction, with a separate regional sign below its complete image bounds.
    /// The clearance includes transparent padding and the enlarged rarity aura.
    /// </summary>
    public sealed class ComponentShopDisplayGeometry
    {
        public Rect ModelUv { get; private set; }
        public Vector2 ModelSize { get; private set; }
        public Vector2 ModelPosition { get; private set; }
        public Rect ModelBounds => new Rect(ModelPosition - ModelSize * 0.5f, ModelSize);
        public Rect PlaqueBounds => new Rect(PlaquePosition - PlaqueSize * 0.5f, PlaqueSize);
        // Keep this convenience property aspect-safe as well. Callers that
        // supply a custom fit box should use ShowroomModelPositionFor instead;
        // the default showroom placement must never assume the old fixed
        // 300x194.4 RawImage rectangle.
        public Vector2 ShowroomModelPosition => ShowroomModelPositionFor(FitShowroomModelSize(ModelUv));
        public Vector2 PlaqueSize { get; private set; } = new Vector2(252f, 168f);
        public Vector2 PlaquePosition { get; private set; }
        public const float PlaqueFloorY = -245f;
        public const float MinimumPlaqueClearance = 20f;
        public const float MaximumModelTop = 225f;
        // UIPreviewsForMe uses the left-hand component display as the authored
        // showroom reference. This is a *fit box*, not the final image size:
        // every source keeps its own UV aspect ratio inside the box. The old
        // runtime assigned this box directly to RawImage.rectTransform, which
        // stretched narrow instruments and tall sails horizontally.
        public static readonly Vector2 ShowroomModelSize = new Vector2(300f, 194.4f);
        // The dossier is intentionally larger than a shop card while still
        // fitting the same native UV region without a grey backing panel.
        public static readonly Vector2 DetailModelSize = new Vector2(360f, 300f);

        /// <summary>
        /// Fits a cropped atlas region into the authored showroom box without
        /// changing the region's aspect ratio. RawImage has no preserveAspect
        /// flag, so callers must use this result for the RectTransform size.
        /// </summary>
        public static Vector2 FitShowroomModelSize(Rect uv)
        {
            float width = Mathf.Max(0.0001f, uv.width);
            float height = Mathf.Max(0.0001f, uv.height);
            float scale = Mathf.Min(ShowroomModelSize.x / width, ShowroomModelSize.y / height);
            return new Vector2(width * scale, height * scale);
        }

        public static Vector2 FitDetailModelSize(Rect uv)
        {
            float width = Mathf.Max(0.0001f, uv.width);
            float height = Mathf.Max(0.0001f, uv.height);
            float scale = Mathf.Min(DetailModelSize.x / width, DetailModelSize.y / height);
            return new Vector2(width * scale, height * scale);
        }

        public Vector2 ShowroomModelPositionFor(Vector2 modelSize)
        {
            return new Vector2(ModelPosition.x,
                PlaqueBounds.yMax + MinimumPlaqueClearance + modelSize.y * 0.5f);
        }
        public static Rect PlaqueTextRect => new Rect(-93f, 9f, 186f, 46f);
        public Rect WritingArea { get; private set; } = PlaqueTextRect;
        public Rect PlaqueUv { get; private set; } = new Rect(0f, 0f, 1f, 1f);
        public RegionalShopFurniture RegionalFurniture { get; private set; }

        public static ComponentShopDisplayGeometry For(ComponentArtEntry entry) => For(entry, null);

        public static ComponentShopDisplayGeometry For(ComponentArtEntry entry, string port)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var result = new ComponentShopDisplayGeometry();
            // Keep measured feet on the same floor. Only the regional sign is used:
            // no rack, plinth, front lip or load-bearing contact is added to the part.
            result.PlaquePosition = new Vector2(0f,
                PlaqueFloorY - (42f / 1024f - 0.5f) * result.PlaqueSize.y);
            var regional = RegionalShopFurniture.For(port);
            if (regional != null)
            {
                result.RegionalFurniture = regional;
                var sign = regional.Sign;
                result.PlaqueSize = sign.Size;
                result.PlaqueUv = sign.Uv;
                float lowestFoot = float.MaxValue;
                foreach (Vector2 foot in sign.Feet)
                    lowestFoot = Mathf.Min(lowestFoot, sign.Local(foot).y);
                result.PlaquePosition = new Vector2(0f, PlaqueFloorY - lowestFoot);
                Vector2 tl = sign.Local(sign.WritingPixels.min), br = sign.Local(sign.WritingPixels.max);
                result.WritingArea = Rect.MinMaxRect(tl.x + 3f, br.y + 3f, br.x - 3f, tl.y - 3f);
            }

            // UIPreviewsForMe uses one enlarged writing bay for every component
            // plaque. Keep each regional sign's authored center, but expose the
            // shared 300x100 inner bay to runtime layout and validation.
            Vector2 writingCenter = result.WritingArea.center;
            result.WritingArea = new Rect(writingCenter - new Vector2(150f, 50f), new Vector2(300f, 100f));

            Rect opaque = OpaquePixels(entry.ArtSourceId);
            result.ModelUv = Rect.MinMaxRect(Mathf.Max(0f, opaque.xMin - 12f) / 512f,
                Mathf.Max(0f, 512f - opaque.yMax - 12f) / 512f,
                Mathf.Min(512f, opaque.xMax + 12f) / 512f,
                Mathf.Min(512f, 512f - opaque.yMin + 12f) / 512f);
            float bottom = result.PlaqueBounds.yMax + MinimumPlaqueClearance;
            float height = Mathf.Min(260f, MaximumModelTop - bottom);
            float width = entry.Slot == ComponentSlot.Instrument ? 230f : 300f;
            float scale = Mathf.Min(width / result.ModelUv.width, height / result.ModelUv.height);
            result.ModelSize = result.ModelUv.size * scale;
            result.ModelPosition = new Vector2(0f, bottom + result.ModelSize.y * 0.5f);
            return result;
        }

        private static Rect OpaquePixels(string id)
        {
            // Top-left source pixels at alpha >= 220, ignoring the surrounding aura.
            switch (id)
            {
                case "bow_oak_ram": return Rect.MinMaxRect(40f, 96f, 484f, 380f);
                case "bow_reef_cutter": return Rect.MinMaxRect(20f, 90f, 496f, 390f);
                case "bow_gilded_mermaid": return Rect.MinMaxRect(22f, 56f, 474f, 422f);
                case "bow_abyssal_fang": return Rect.MinMaxRect(22f, 64f, 494f, 388f);
                case "bow_leviathan_ram": return Rect.MinMaxRect(18f, 56f, 510f, 440f);
                case "sail_patched_lateen": return Rect.MinMaxRect(46f, 14f, 478f, 496f);
                case "sail_storm_canvas": return Rect.MinMaxRect(44f, 12f, 478f, 498f);
                case "sail_guild_silk": return Rect.MinMaxRect(30f, 12f, 494f, 498f);
                case "sail_moonweave": return Rect.MinMaxRect(80f, 10f, 450f, 496f);
                case "sail_tempest_wing": return Rect.MinMaxRect(18f, 12f, 484f, 496f);
                case "cannon_deck_swivel": return Rect.MinMaxRect(16f, 116f, 448f, 410f);
                case "cannon_bronze_longgun": return Rect.MinMaxRect(8f, 132f, 504f, 370f);
                case "cannon_chain_repeater": return Rect.MinMaxRect(24f, 140f, 498f, 398f);
                case "cannon_starfire_battery": return Rect.MinMaxRect(14f, 98f, 494f, 372f);
                case "cannon_queens_broadside": return Rect.MinMaxRect(10f, 56f, 504f, 404f);
                case "plating_braced_ribs": return Rect.MinMaxRect(18f, 82f, 498f, 388f);
                case "plating_iron_band": return Rect.MinMaxRect(16f, 80f, 494f, 388f);
                case "plating_gilded_bulwark": return Rect.MinMaxRect(8f, 82f, 494f, 390f);
                case "plating_moonsilver_skin": return Rect.MinMaxRect(16f, 66f, 500f, 390f);
                case "plating_living_hull": return Rect.MinMaxRect(16f, 66f, 494f, 384f);
                case "instrument_brass_compass": return Rect.MinMaxRect(66f, 12f, 452f, 500f);
                case "instrument_tideglass": return Rect.MinMaxRect(88f, 14f, 434f, 500f);
                case "instrument_guild_engine": return Rect.MinMaxRect(50f, 24f, 472f, 500f);
                case "instrument_lunar_astrolabe": return Rect.MinMaxRect(70f, 24f, 462f, 500f);
                case "instrument_allseeing_sextant": return Rect.MinMaxRect(36f, 8f, 476f, 502f);
                // Expanded market sidegrades reuse the same component art family as
                // the authored prototypes.  Keep their silhouette bounds stable so
                // newly-added entries cannot fall back to a stretched 512 px square
                // or disappear from the shop layout.  The family bounds intentionally
                // match the closest existing prototype in each slot.
                default:
                    if (id.StartsWith("bow_", StringComparison.Ordinal))
                        return Rect.MinMaxRect(18f, 56f, 510f, 440f);
                    if (id.StartsWith("sail_", StringComparison.Ordinal))
                        return Rect.MinMaxRect(18f, 12f, 494f, 498f);
                    if (id.StartsWith("cannon_", StringComparison.Ordinal))
                        return Rect.MinMaxRect(10f, 56f, 504f, 410f);
                    if (id.StartsWith("plating_", StringComparison.Ordinal))
                        return Rect.MinMaxRect(8f, 66f, 500f, 390f);
                    if (id.StartsWith("instrument_", StringComparison.Ordinal))
                        return Rect.MinMaxRect(36f, 8f, 476f, 502f);
                    throw new ArgumentException("Missing measured component prototype: " + id, nameof(id));
            }
        }
    }
}
