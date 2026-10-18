using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Deck actors share a floor line above the retained hand and defense HUD.</summary>
    public static class BoardingBattleLayout
    {
        public static Vector2 PartySize => new Vector2(600f, 280f);
        public static Vector2 PartyPosition(bool enemy) => new Vector2(enemy ? 480f : -480f, 205f);
        public static Vector2 HealthPosition(bool enemy) => new Vector2(enemy ? 480f : -480f, 34f);

        // Visible boot contacts measured in the original PNGs, before atlas
        // inset. Keep these normalized so import resizing does not move feet.
        // Alpha >= 64, at least 8 pixels (6 contiguous) in three adjacent rows
        // excludes isolated edge noise. Atlas entries follow profession order.
        private static readonly Dictionary<string, float[]> FootContacts = new Dictionary<string, float[]>
        {
            { "boarding_crew_broken_anchor_v2", new[] { -1.5f/627f, -2.5f/627f, -2.5f/627f, 43.5f/627f, 46.5f/627f, 43.5f/627f } },
            { "boarding_crew_bronze_bell_v2", new[] { 3.5f/627f, 2.5f/627f, 2.5f/627f, 56.5f/627f, 56.5f/627f, 56.5f/627f } },
            { "boarding_crew_gray_gull_v2", new[] { 44.5f/627f, 44.5f/627f, 44.5f/627f, 54.5f/627f, 55.5f/627f, 53.5f/627f } },
            { "boarding_crew_salt_crown_v2", new[] { 4.5f/627f, 4.5f/627f, 5.5f/627f, 42.5f/627f, 40.5f/627f, 40.5f/627f } },
            { "boarding_crew_twilight_tide_v2", new[] { 17.5f/627f, 18.5f/627f, 17.5f/627f, 56.5f/627f, 54.5f/627f, 55.5f/627f } },
            { "boarding_crew_white_shell_v2", new[] { 24.5f/627f, 22.5f/627f, 24.5f/627f, 52.5f/627f, 56.5f/627f, 54.5f/627f } },
            { "boarding_player_party_v1", new[] { 38.5f/1024f } },
            { "boarding_enemy_party_v1", new[] { 33.5f/1024f } },
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

        // The new deck has a clear, continuous floor below source y=.56.
        // A modest bottom-anchored crop keeps the combat lane under the boots.
        public static Rect JoinedDeckUv(Texture texture, Vector2 target)
        {
            if (texture == null || target.x <= 0f || target.y <= 0f) return new Rect(0f, 0f, 1f, 1f);
            float sourceAspect = (float)texture.width / texture.height;
            float targetAspect = target.x / target.y;
            float height = Mathf.Min(.9f, sourceAspect / targetAspect);
            float width = height * targetAspect / sourceAspect;
            return new Rect((1f - width) * .5f, 0f, width, height);
        }

        // Borrow only the distant sky/water from each of the 18 location paintings.
        // Their old deck and side props never become a second foreground.
        public static Rect SceneryUv(Texture texture, Vector2 target)
        {
            if (texture == null || target.x <= 0f || target.y <= 0f) return new Rect(0f, 0f, 1f, 1f);
            float sourceAspect = (float)texture.width / texture.height;
            float height = Mathf.Min(.39f, .76f * sourceAspect * target.y / target.x);
            float width = height * target.x / target.y / sourceAspect;
            return new Rect((1f - width) * .5f, .61f, width, height);
        }

        public static Rect CoverUv(Texture texture, Vector2 target)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 || target.x <= 0f || target.y <= 0f)
                return new Rect(0f, 0f, 1f, 1f);
            float sourceAspect = (float)texture.width / texture.height;
            float targetAspect = target.x / target.y;
            float width = Mathf.Min(1f, targetAspect / sourceAspect);
            float height = Mathf.Min(1f, sourceAspect / targetAspect);
            // Preserve aspect and keep the verified painted-deck row under the
            // raised actors, rather than letting their feet land on the railing.
            float floor = PartyPosition(false).y - PartySize.y * .5f;
            float floorInFrame = Mathf.Clamp01(.5f + floor / target.y);
            height = Mathf.Min(height, .35f / Mathf.Max(.01f, floorInFrame));
            width = height * targetAspect / sourceAspect;
            return new Rect((1f - width) * .5f, 0f, width, height);
        }
    }
}
