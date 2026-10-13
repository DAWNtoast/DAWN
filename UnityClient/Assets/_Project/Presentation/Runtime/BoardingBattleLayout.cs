using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Deck actors share a floor line above the retained hand and defense HUD.</summary>
    public static class BoardingBattleLayout
    {
        public static Vector2 PartySize => new Vector2(600f, 330f);
        public static Vector2 PartyPosition(bool enemy) => new Vector2(enemy ? 480f : -480f, 115f);
        public static Vector2 HealthPosition(bool enemy) => new Vector2(enemy ? 480f : -480f, -100f);

        // Visible boot contacts measured in the original PNGs, before atlas
        // inset. Keep these normalized so import resizing does not move feet.
        // Alpha >= 64, at least 8 pixels (6 contiguous) in three adjacent rows
        // excludes isolated edge noise. Atlas entries follow profession order.
        private static readonly Dictionary<string, float[]> FootContacts = new Dictionary<string, float[]>
        {
            { "boarding_crew_broken_anchor_v2", new[] { 11f/627f, 11f/627f, 11f/627f, 52f/627f, 62f/627f, 50f/627f } },
            { "boarding_crew_bronze_bell_v2", new[] { 15f/627f, 16f/627f, 19f/627f, 65f/627f, 62f/627f, 64f/627f } },
            { "boarding_crew_gray_gull_v2", new[] { 32f/627f, 35f/627f, 31f/627f, 56f/627f, 64f/627f, 53f/627f } },
            { "boarding_crew_salt_crown_v2", new[] { 28f/627f, 28f/627f, 26f/627f, 74f/627f, 69f/627f, 71f/627f } },
            { "boarding_crew_twilight_tide_v2", new[] { 31f/627f, 30f/627f, 31f/627f, 61f/627f, 61f/627f, 61f/627f } },
            { "boarding_crew_white_shell_v2", new[] { 44f/627f, 46f/627f, 46f/627f, 65f/627f, 68f/627f, 65f/627f } },
            { "boarding_player_party_v1", new[] { 42f/1024f } },
            { "boarding_enemy_party_v1", new[] { 29f/1024f } },
            { "boarding_enemy_ember_ark_v1", new[] { 18f/1024f } },
            { "boarding_enemy_ghost_v1", new[] { 13f/1024f } },
            { "boarding_enemy_imperial_v1", new[] { 26f/1024f } },
            { "boarding_enemy_iron_oath_v1", new[] { 14f/1024f } },
            { "boarding_enemy_red_widow_v1", new[] { 24f/1024f } },
            { "boarding_enemy_route_gold_v1", new[] { 11f/1024f } },
            { "boarding_enemy_route_moon_v1", new[] { 15f/1024f } },
            { "boarding_enemy_route_reef_v1", new[] { 10f/1024f } },
            { "boarding_enemy_thunder_crown_v1", new[] { 14f/1024f } },
            { "boarding_enemy_tide_requiem_v1", new[] { 47f/1024f } },
            { "boarding_enemy_treasure_v1", new[] { 28f/1024f } },
            { "crew_navigator_evelyn_fullbody", new[] { 6f/1536f } },
            { "crew_carpenter_barlow_fullbody", new[] { 15f/1536f } },
            { "crew_doctor_moer_fullbody", new[] { 6f/1536f } },
            { "crew_gunner_leisa_fullbody", new[] { 10f/1536f } },
            { "crew_cook_pepe_fullbody", new[] { 4f/1536f } },
            { "crew_diver_noya_fullbody", new[] { 10f/1536f } },
        };

        public static void GroundFigure(RawImage figure)
        {
            if (figure == null) return;
            RectTransform rect = figure.rectTransform;
            Vector2 position = rect.anchoredPosition;
            position.y = -PartySize.y * .5f - rect.rect.yMin - rect.rect.height * FootContact(figure.texture, figure.uvRect);
            rect.anchoredPosition = position;
        }

        private static float FootContact(Texture texture, Rect uv)
        {
            if (texture == null || Mathf.Abs(uv.height) < .00001f || !FootContacts.TryGetValue(texture.name, out float[] contacts))
                return 0f;
            float sourceY = contacts[0];
            if (contacts.Length == 6)
            {
                int column = Mathf.Clamp(Mathf.FloorToInt(uv.center.x * 3f), 0, 2);
                int rowFromBottom = Mathf.Clamp(Mathf.FloorToInt(uv.center.y * 2f), 0, 1);
                sourceY = (rowFromBottom + contacts[(1 - rowFromBottom) * 3 + column]) * .5f;
            }
            return (sourceY - uv.y) / uv.height;
        }

        public static Rect CoverUv(Texture texture, Vector2 target)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 || target.x <= 0f || target.y <= 0f)
                return new Rect(0f, 0f, 1f, 1f);
            float sourceAspect = (float)texture.width / texture.height;
            float targetAspect = target.x / target.y;
            float width = Mathf.Min(1f, targetAspect / sourceAspect);
            float height = Mathf.Min(1f, sourceAspect / targetAspect);
            // Keep the foreground deck in frame. Centering the vertical crop
            // pushes the rail/deck seam below the actors in the taller paintings.
            return new Rect((1f - width) * .5f, 0f, width, height);
        }
    }
}
