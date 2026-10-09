using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public enum NavigationLookoutWeather { Clear, Fog, Storm }

    public readonly struct NavigationLookoutBlend
    {
        public NavigationLookoutBlend(NavigationTimeOfDay from, NavigationTimeOfDay to, float amount)
        { From = from; To = to; Amount = Mathf.Clamp01(amount); }
        public NavigationTimeOfDay From { get; }
        public NavigationTimeOfDay To { get; }
        public float Amount { get; }
    }

    /// <summary>Painted local-weather panoramas; shared resources load only when their time band is displayed.</summary>
    public static class NavigationLookoutArtCatalog
    {
        private static readonly Texture2D[] textures = new Texture2D[9];
        private static readonly bool[] attempted = new bool[9];
        private static readonly string[] weatherNames = { "clear", "fog", "storm" };
        private static readonly string[] timeNames = { "day", "dusk", "night" };

        public static NavigationLookoutWeather WeatherForTerrain(SeaTerrainType terrain) =>
            terrain == SeaTerrainType.FogBank ? NavigationLookoutWeather.Fog :
            terrain == SeaTerrainType.StormFront ? NavigationLookoutWeather.Storm : NavigationLookoutWeather.Clear;

        private static int TimeIndex(NavigationTimeOfDay time) =>
            time == NavigationTimeOfDay.Dusk ? 1 : time == NavigationTimeOfDay.Night ? 2 : 0;

        public static string EnvironmentResourcePath(SeaTerrainType terrain, NavigationTimeOfDay time)
        {
            int weather = (int)WeatherForTerrain(terrain), band = TimeIndex(time);
            return weather == 0 && band == 0 ? "NavigationLookout/lookout_sea_v1" :
                "NavigationLookout/Environments/lookout_" + weatherNames[weather] + "_" + timeNames[band] + "_v2";
        }

        public static Texture2D ForEnvironment(SeaTerrainType terrain, NavigationTimeOfDay time)
        {
            int index = (int)WeatherForTerrain(terrain) * 3 + TimeIndex(time);
            if (!attempted[index])
            {
                string path = EnvironmentResourcePath(terrain, time);
                textures[index] = Resources.Load<Texture2D>(path);
                attempted[index] = true;
                if (textures[index] == null)
                    Debug.LogWarning("Missing lookout environment Resources/" + path + "; using the legacy sea only as a missing-art fallback.");
            }
            if (textures[index] != null || index == 0) return textures[index];
            return ForEnvironment(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day);
        }

        public static Rect PanoramaUv(Vector2 viewportSize, Texture texture) =>
            PanoramaUv(viewportSize, texture == null ? Vector2.zero : new Vector2(texture.width, texture.height));

        /// <summary>Cover without distortion, retaining the left sky and the painted horizon at 61.2% from the bottom.</summary>
        public static Rect PanoramaUv(Vector2 viewportSize, Vector2 textureSize)
        {
            if (!PositiveFinite(viewportSize.x) || !PositiveFinite(viewportSize.y) ||
                !PositiveFinite(textureSize.x) || !PositiveFinite(textureSize.y)) return new Rect(0, 0, 1, 1);
            float viewportAspect = viewportSize.x / viewportSize.y;
            float textureAspect = textureSize.x / textureSize.y;
            if (viewportAspect <= textureAspect)
                return new Rect(0, 0, viewportAspect / textureAspect, 1);
            float height = textureAspect / viewportAspect;
            return new Rect(0, .612f * (1f - height), 1, height);
        }

        private static bool PositiveFinite(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);

        public static NavigationLookoutBlend BlendForVoyageHours(double elapsedHours) =>
            BlendForLocalHour(NavigationTimeOfDayUtility.LocalHour(elapsedHours));

        public static NavigationLookoutBlend BlendForLocalHour(double hour)
        {
            if (double.IsNaN(hour) || double.IsInfinity(hour)) hour = NavigationTimeOfDayUtility.VoyageStartHour;
            hour = (hour % 24d + 24d) % 24d;
            if (hour < 5d || hour >= 20.5d) return Still(NavigationTimeOfDay.Night);
            if (hour < 7d) return new NavigationLookoutBlend(NavigationTimeOfDay.Night, NavigationTimeOfDay.Day, (float)((hour - 5d) / 2d));
            if (hour < 16.5d) return Still(NavigationTimeOfDay.Day);
            if (hour < 18d) return new NavigationLookoutBlend(NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, (float)((hour - 16.5d) / 1.5d));
            if (hour < 19d) return Still(NavigationTimeOfDay.Dusk);
            return new NavigationLookoutBlend(NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night, (float)((hour - 19d) / 1.5d));
        }

        private static NavigationLookoutBlend Still(NavigationTimeOfDay time) => new NavigationLookoutBlend(time, time, 0f);

        public static Color EnvironmentTint(SeaTerrainType terrain, NavigationLookoutBlend blend)
        {
            Color time = Color.Lerp(Lighting(blend.From), Lighting(blend.To), blend.Amount);
            Color weather = WeatherForTerrain(terrain) == NavigationLookoutWeather.Fog ? new Color(.83f, .88f, .92f, 1f) :
                WeatherForTerrain(terrain) == NavigationLookoutWeather.Storm ? new Color(.68f, .75f, .83f, 1f) : Color.white;
            return time * weather;
        }

        private static Color Lighting(NavigationTimeOfDay time) => time == NavigationTimeOfDay.Dusk ? new Color(.91f, .76f, .64f, 1f) :
            time == NavigationTimeOfDay.Night ? new Color(.36f, .46f, .64f, 1f) : Color.white;

        public static bool ShouldSkipLocalWeather(LookoutObservation observation, SeaTerrainType terrain) =>
            observation != null && Mathf.Abs(observation.DistanceNm) <= .001f &&
            ((terrain == SeaTerrainType.FogBank && observation.Kind == LookoutSceneryKind.Fog) ||
             (terrain == SeaTerrainType.StormFront && observation.Kind == LookoutSceneryKind.Storm));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            // Resources owns the textures. Reset references without destroying shared art.
            Array.Clear(textures, 0, textures.Length);
            Array.Clear(attempted, 0, attempted.Length);
        }
    }
}
