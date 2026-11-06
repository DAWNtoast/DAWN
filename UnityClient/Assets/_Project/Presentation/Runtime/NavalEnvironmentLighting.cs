using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public readonly struct NavalEnvironmentLight
    {
        public NavalEnvironmentLight(NavigationTimeOfDay phase, NavigationLookoutBlend blend,
            Color sea, Color ship, Color reflection)
        { Phase = phase; Blend = blend; SeaTint = sea; ShipTint = ship; ReflectionColor = reflection; }

        public NavigationTimeOfDay Phase { get; }
        public NavigationLookoutBlend Blend { get; }
        public Color SeaTint { get; }
        public Color ShipTint { get; }
        public Color ReflectionColor { get; }
    }

    /// <summary>Uses the persistent voyage clock and lookout transitions without replacing terrain art.</summary>
    public static class NavalEnvironmentLighting
    {
        public static NavalEnvironmentLight ForVoyageHours(SeaTerrainType terrain, double elapsedGameHours)
        {
            NavigationLookoutBlend blend = NavigationLookoutArtCatalog.BlendForVoyageHours(elapsedGameHours);
            Color sea = Color.Lerp(SeaLight(blend.From), SeaLight(blend.To), blend.Amount);
            // The paintings already contain their weather. Preserve the existing storm attenuation.
            if (terrain == SeaTerrainType.StormFront) sea *= new Color(.8f, .8f, .8f, 1f);
            Color deck = Color.Lerp(BoardingBattleArtCatalog.DeckLight(terrain, blend.From),
                BoardingBattleArtCatalog.DeckLight(terrain, blend.To), blend.Amount);
            // Match the boarding figures' restrained illumination: small tactical hulls must stay legible.
            Color ship = Color.Lerp(Color.white, deck, .35f);
            Color reflection = Color.Lerp(Reflection(blend.From), Reflection(blend.To), blend.Amount);
            reflection.a *= terrain == SeaTerrainType.StormFront ? .2f : terrain == SeaTerrainType.FogBank ? .4f : 1f;
            sea.a = ship.a = 1f;
            return new NavalEnvironmentLight(NavigationTimeOfDayUtility.FromVoyageHours(elapsedGameHours),
                blend, sea, ship, reflection);
        }

        private static Color SeaLight(NavigationTimeOfDay phase) => phase == NavigationTimeOfDay.Dusk
            ? new Color(1f, .73f, .52f, 1f) : phase == NavigationTimeOfDay.Night
            ? new Color(.36f, .46f, .64f, 1f) : Color.white;

        private static Color Reflection(NavigationTimeOfDay phase) => phase == NavigationTimeOfDay.Dusk
            ? new Color(1f, .65f, .3f, .13f) : phase == NavigationTimeOfDay.Night
            ? new Color(.55f, .72f, 1f, .08f) : new Color(1f, 1f, 1f, 0f);
    }
}
