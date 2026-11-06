using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Measured shoe contacts in the original transparent art, before UV cropping.</summary>
    public static class BoardingFootShadowProfiles
    {
        // Source PNGs are 1536 x 1024. x is the center of the sole's final 13-pixel band;
        // y is its own lowest opaque pixel center measured upward from the image bottom.
        // Alpha >= 96; neighboring boots were separated and visually checked on 2026-11-03.
        // In particular, the player's right sailor's left boot is above the central sailor's
        // right boot. They must never be merged or forced onto the same ground row.
        private static readonly Dictionary<string, Vector3[]> Contacts = new Dictionary<string, Vector3[]>
        {
            { "boarding_player_party_v1", new[]
                {
                    Pixel(92.5f, 39.5f, 85f), Pixel(476f, 71.5f, 136f),
                    Pixel(628f, 68.5f, 58f), Pixel(1037f, 38.5f, 70f),
                    Pixel(1089f, 89.5f, 68f), Pixel(1447f, 68.5f, 144f)
                }
            },
            { "boarding_enemy_party_v1", new[]
                {
                    Pixel(191.5f, 44.5f, 65f), Pixel(579.5f, 71.5f, 103f),
                    Pixel(675f, 37.5f, 72f), Pixel(1029f, 65.5f, 114f),
                    Pixel(1131.5f, 33.5f, 75f), Pixel(1451.5f, 68.5f, 131f)
                }
            },
            // Native 1536 x 1024 alpha measurements, 2026-11-04. Each pair belongs
            // to one scholar/guard; preserve the distinct sole heights.
            { "boarding_enemy_last_horizon_v1", new[]
                {
                    Pixel(143f, 16.5f, 78f), Pixel(514.5f, 45.5f, 143f),
                    Pixel(653.5f, 15.5f, 79f), Pixel(982.5f, 35.5f, 141f),
                    Pixel(1123f, 15.5f, 70f), Pixel(1449f, 34.5f, 136f)
                }
            },
            { "boarding_enemy_moon_weave_v1", new[]
                {
                    Pixel(141.5f, 21.5f, 77f), Pixel(522.5f, 52.5f, 127f),
                    Pixel(631f, 14.5f, 74f), Pixel(981f, 32.5f, 132f),
                    Pixel(1120.5f, 17.5f, 69f), Pixel(1453f, 37.5f, 130f)
                }
            }
        };

        private static Vector3 Pixel(float x, float y, float width) => new Vector3(x / 1536f, y / 1024f, width / 1536f);

        /// <summary>Current sprite-local normalized x/y plus sole width; unknown art uses the caller's fallback.</summary>
        public static Vector3[] For(Texture texture, Rect uv)
        {
            if (texture == null || Mathf.Abs(uv.width) < .00001f || Mathf.Abs(uv.height) < .00001f ||
                !Contacts.TryGetValue(texture.name, out Vector3[] source)) return Array.Empty<Vector3>();
            var result = new List<Vector3>(source.Length);
            foreach (Vector3 point in source)
            {
                float x = (point.x - uv.x) / uv.width;
                if (x < 0f || x > 1f) continue;
                result.Add(new Vector3(x, (point.y - uv.y) / uv.height, point.z / Mathf.Abs(uv.width)));
            }
            return result.ToArray();
        }
    }
}
