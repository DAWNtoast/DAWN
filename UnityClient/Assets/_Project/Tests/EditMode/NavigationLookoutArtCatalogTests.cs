using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class NavigationLookoutArtCatalogTests
    {
        [TestCase(1920, 1080)]
        [TestCase(1280, 720)]
        [TestCase(1280, 1024)]
        [TestCase(3440, 1440)]
        [TestCase(3840, 1080)]
        public void PanoramaCropFillsViewportWithoutDistortingPixelsOrMovingHorizon(int width, int height)
        {
            var textureSize = new Vector2(1672, 941);
            Rect uv = NavigationLookoutArtCatalog.PanoramaUv(new Vector2(width, height), textureSize);
            Assert.That(uv.xMin, Is.EqualTo(0), "Keep the authored left-side sun and moon when cropping horizontally.");
            Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(uv.xMax, Is.LessThanOrEqualTo(1));
            Assert.That(uv.yMax, Is.LessThanOrEqualTo(1));
            Assert.That(uv.width > 0 && uv.height > 0, Is.True);
            Assert.That(Mathf.Max(uv.width, uv.height), Is.EqualTo(1), "Cover uses the full image in at least one dimension.");
            float horizontalPixelSize = width / (textureSize.x * uv.width);
            float verticalPixelSize = height / (textureSize.y * uv.height);
            Assert.That(horizontalPixelSize / verticalPixelSize, Is.EqualTo(1f).Within(.00001f), "A round sun must stay round.");
            Assert.That((.612f - uv.yMin) / uv.height, Is.EqualTo(.612f).Within(.00001f), "The horizon must stay aligned with projected scenery.");
        }

        [Test]
        public void PanoramaCropUsesWholeImageBeforeLayoutOrWhenArtIsMissing()
        {
            Assert.That(NavigationLookoutArtCatalog.PanoramaUv(Vector2.zero, new Vector2(1672, 941)), Is.EqualTo(new Rect(0, 0, 1, 1)));
            Assert.That(NavigationLookoutArtCatalog.PanoramaUv(new Vector2(956, 626), (Texture)null), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [TestCase(0d, NavigationTimeOfDay.Night, NavigationTimeOfDay.Night, 0f)]
        [TestCase(5d, NavigationTimeOfDay.Night, NavigationTimeOfDay.Day, 0f)]
        [TestCase(6d, NavigationTimeOfDay.Night, NavigationTimeOfDay.Day, .5f)]
        [TestCase(7d, NavigationTimeOfDay.Day, NavigationTimeOfDay.Day, 0f)]
        [TestCase(16.5d, NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, 0f)]
        [TestCase(17.25d, NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, .5f)]
        [TestCase(18d, NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Dusk, 0f)]
        [TestCase(19d, NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night, 0f)]
        [TestCase(19.75d, NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night, .5f)]
        [TestCase(20.5d, NavigationTimeOfDay.Night, NavigationTimeOfDay.Night, 0f)]
        [TestCase(24d, NavigationTimeOfDay.Night, NavigationTimeOfDay.Night, 0f)]
        [TestCase(30d, NavigationTimeOfDay.Night, NavigationTimeOfDay.Day, .5f)]
        public void TimeBlendUsesAuthoredMorningEveningAndNightIntervals(double hour, NavigationTimeOfDay from, NavigationTimeOfDay to, float amount)
        {
            NavigationLookoutBlend blend = NavigationLookoutArtCatalog.BlendForLocalHour(hour);
            Assert.That(blend.From, Is.EqualTo(from));
            Assert.That(blend.To, Is.EqualTo(to));
            Assert.That(blend.Amount, Is.EqualTo(amount).Within(.0001f));
        }

        [Test]
        public void SceneryLightingIsContinuousAtAllBlendEdgesAndUsesPersistentClock()
        {
            foreach (double boundary in new[] { 0d, 5d, 7d, 16.5d, 18d, 19d, 20.5d, 24d })
            {
                Color before = NavigationLookoutArtCatalog.EnvironmentTint(SeaTerrainType.OpenWater,
                    NavigationLookoutArtCatalog.BlendForLocalHour(boundary - .00001d));
                Color after = NavigationLookoutArtCatalog.EnvironmentTint(SeaTerrainType.OpenWater,
                    NavigationLookoutArtCatalog.BlendForLocalHour(boundary + .00001d));
                Assert.That(Vector4.Distance(before, after), Is.LessThan(.0001f), "Lighting boundary " + boundary);
            }
            Assert.That(NavigationLookoutArtCatalog.BlendForVoyageHours(0).From, Is.EqualTo(NavigationTimeOfDay.Day));
            Assert.That(NavigationLookoutArtCatalog.BlendForVoyageHours(9).Amount, Is.EqualTo(1f / 3f).Within(.0001f));
            Assert.That(NavigationLookoutArtCatalog.BlendForVoyageHours(12).Amount, Is.EqualTo(2f / 3f).Within(.0001f));
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Assert.That(NavigationLookoutArtCatalog.BlendForLocalHour(invalid).From, Is.EqualTo(NavigationTimeOfDay.Day));
            Color night = NavigationLookoutArtCatalog.EnvironmentTint(SeaTerrainType.OpenWater, NavigationLookoutArtCatalog.BlendForLocalHour(22));
            Color dawn = NavigationLookoutArtCatalog.EnvironmentTint(SeaTerrainType.OpenWater, NavigationLookoutArtCatalog.BlendForLocalHour(6));
            Assert.That(dawn.r, Is.EqualTo((night.r + 1f) * .5f).Within(.0001f));
            Assert.That(dawn.g, Is.EqualTo((night.g + 1f) * .5f).Within(.0001f));
            Assert.That(dawn.b, Is.EqualTo((night.b + 1f) * .5f).Within(.0001f));
            Assert.That(dawn.a, Is.EqualTo(1f), "Lighting must not make solid scenery transparent.");
        }

        [Test]
        public void OnlyLocalFogAndStormTerrainChooseWeatherPanoramas()
        {
            foreach (SeaTerrainType terrain in Enum.GetValues(typeof(SeaTerrainType)))
                Assert.That(NavigationLookoutArtCatalog.WeatherForTerrain(terrain), Is.EqualTo(
                    terrain == SeaTerrainType.FogBank ? NavigationLookoutWeather.Fog :
                    terrain == SeaTerrainType.StormFront ? NavigationLookoutWeather.Storm : NavigationLookoutWeather.Clear), terrain.ToString());
            Assert.That(NavigationLookoutArtCatalog.WeatherForTerrain(SeaTerrainType.Reef), Is.EqualTo(NavigationLookoutWeather.Clear));
            Assert.That(NavigationLookoutArtCatalog.ShouldSkipLocalWeather(new LookoutObservation { Kind = LookoutSceneryKind.Storm, DistanceNm = 30 }, SeaTerrainType.StormFront), Is.False);
            Assert.That(NavigationLookoutArtCatalog.ShouldSkipLocalWeather(new LookoutObservation { Kind = LookoutSceneryKind.Storm, DistanceNm = 0 }, SeaTerrainType.OpenWater), Is.False);
            Assert.That(NavigationLookoutArtCatalog.ShouldSkipLocalWeather(new LookoutObservation { Kind = LookoutSceneryKind.Fog, DistanceNm = 0 }, SeaTerrainType.StormFront), Is.False);
            Assert.That(NavigationLookoutArtCatalog.ShouldSkipLocalWeather(new LookoutObservation { Kind = LookoutSceneryKind.Storm, DistanceNm = 0 }, SeaTerrainType.StormFront), Is.True);
            Assert.That(NavigationLookoutArtCatalog.ShouldSkipLocalWeather(new LookoutObservation { Kind = LookoutSceneryKind.Fog, DistanceNm = 0 }, SeaTerrainType.FogBank), Is.True);
        }

        [TestCase(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day, "NavigationLookout/lookout_sea_v1")]
        [TestCase(SeaTerrainType.OpenWater, NavigationTimeOfDay.Dusk, "NavigationLookout/Environments/lookout_clear_dusk_v2")]
        [TestCase(SeaTerrainType.OpenWater, NavigationTimeOfDay.Night, "NavigationLookout/Environments/lookout_clear_night_v2")]
        [TestCase(SeaTerrainType.FogBank, NavigationTimeOfDay.Day, "NavigationLookout/Environments/lookout_fog_day_v2")]
        [TestCase(SeaTerrainType.FogBank, NavigationTimeOfDay.Dusk, "NavigationLookout/Environments/lookout_fog_dusk_v2")]
        [TestCase(SeaTerrainType.FogBank, NavigationTimeOfDay.Night, "NavigationLookout/Environments/lookout_fog_night_v2")]
        [TestCase(SeaTerrainType.StormFront, NavigationTimeOfDay.Day, "NavigationLookout/Environments/lookout_storm_day_v2")]
        [TestCase(SeaTerrainType.StormFront, NavigationTimeOfDay.Dusk, "NavigationLookout/Environments/lookout_storm_dusk_v2")]
        [TestCase(SeaTerrainType.StormFront, NavigationTimeOfDay.Night, "NavigationLookout/Environments/lookout_storm_night_v2")]
        public void EveryNormalStateLoadsItsRealArtInsteadOfTheMissingArtFallback(SeaTerrainType terrain, NavigationTimeOfDay time, string path)
        {
            Assert.That(NavigationLookoutArtCatalog.EnvironmentResourcePath(terrain, time), Is.EqualTo(path));
            Texture2D actual = Resources.Load<Texture2D>(path);
            Assert.That(actual, Is.Not.Null, "All nine normal states require actual art, including clear/day's retained original.");
            Assert.That(NavigationLookoutArtCatalog.ForEnvironment(terrain, time), Is.SameAs(actual));
            Assert.That(actual.width, Is.GreaterThanOrEqualTo(1024));
        }
    }
}
