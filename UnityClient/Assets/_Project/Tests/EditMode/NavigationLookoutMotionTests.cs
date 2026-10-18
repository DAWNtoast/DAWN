using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class NavigationLookoutMotionTests
    {
        [TestCase(1920f, 1080f)]
        [TestCase(3840f, 2160f)]
        [TestCase(1280f, 1024f)]
        [TestCase(3440f, 1440f)]
        public void LookoutWaterMotion_PinsSkyHorizonAndEdgesInsideEveryCoverCrop(float width, float height)
        {
            Rect crop = NavigationLookoutArtCatalog.PanoramaUv(new Vector2(width, height), new Vector2(1672, 941));
            foreach (float time in new[] { 0f, 1.2f, 3.7f, 9f })
            {
                Assert.That(NavigationLookoutOceanGraphic.WaterUvOffset(new Vector2(crop.center.x, .612f), crop, time), Is.EqualTo(Vector2.zero));
                Assert.That(NavigationLookoutOceanGraphic.WaterUvOffset(new Vector2(crop.center.x, .85f), crop, time), Is.EqualTo(Vector2.zero));
                Assert.That(NavigationLookoutOceanGraphic.WaterUvOffset(new Vector2(crop.xMin, .3f), crop, time), Is.EqualTo(Vector2.zero));
                Assert.That(NavigationLookoutOceanGraphic.WaterUvOffset(new Vector2(crop.xMax, .3f), crop, time), Is.EqualTo(Vector2.zero));
                Assert.That(NavigationLookoutOceanGraphic.WaterUvOffset(new Vector2(crop.center.x, crop.yMin), crop, time), Is.EqualTo(Vector2.zero));
                for (int y = 0; y <= 24; y++)
                    for (int x = 0; x <= 24; x++)
                    {
                        Vector2 uv = new Vector2(Mathf.Lerp(crop.xMin, crop.xMax, x / 24f), Mathf.Lerp(crop.yMin, crop.yMax, y / 24f));
                        Vector2 moved = uv + NavigationLookoutOceanGraphic.WaterUvOffset(uv, crop, time);
                        Assert.That(moved.x, Is.InRange(crop.xMin - .00001f, crop.xMax + .00001f));
                        Assert.That(moved.y, Is.InRange(crop.yMin - .00001f, crop.yMax + .00001f));
                    }
            }
            Vector2 water = new Vector2(crop.center.x, Mathf.Lerp(crop.yMin, .612f, .3f));
            Assert.That(Vector2.Distance(NavigationLookoutOceanGraphic.WaterUvOffset(water, crop, 0f),
                NavigationLookoutOceanGraphic.WaterUvOffset(water, crop, 1.2f)), Is.GreaterThan(.0001f));
        }

        [TestCase(LookoutSceneryKind.Island, -.32f)]
        [TestCase(LookoutSceneryKind.Vessel, -.362305f)]
        [TestCase(LookoutSceneryKind.PirateShip, -.445f)]
        public void LookoutProjection_ApproachPreservesAspectAndMeasuredWaterContact(LookoutSceneryKind kind, float contact)
        {
            var far = new LookoutObservation { Kind = kind, Horizontal = .1f, Scale = .3f, Depth = .2f, Proximity = .2f, Visibility = 1f };
            var near = new LookoutObservation { Kind = kind, Horizontal = .4f, Scale = .75f, Depth = .8f, Proximity = .8f, Visibility = 1f };
            NavigationLookoutPose a = NavigationLookoutMotion.ProjectPose(far, 1.75f, contact, Color.white);
            NavigationLookoutPose b = NavigationLookoutMotion.ProjectPose(near, 1.75f, contact, Color.white);
            Assert.That(b.Size.x, Is.GreaterThan(a.Size.x));
            Assert.That(b.Position.x, Is.GreaterThan(a.Position.x));
            Assert.That(a.Position.y + a.Size.y * contact, Is.EqualTo(64f - .2f * .2f * 192f).Within(.0001f));
            Assert.That(b.Position.y + b.Size.y * contact, Is.EqualTo(64f - .8f * .8f * 192f).Within(.0001f));
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                NavigationLookoutPose pose = NavigationLookoutPose.Lerp(a, b, t);
                Assert.That(pose.Size.x / pose.Size.y, Is.EqualTo(1.75f).Within(.0001f));
                Assert.That(pose.Position.y + pose.Size.y * contact,
                    Is.EqualTo(Mathf.Lerp(a.Position.y + a.Size.y * contact, b.Position.y + b.Size.y * contact, t)).Within(.0001f));
            }
        }

        [Test]
        public void LookoutWeatherAndDaylight_CrossfadeKeepsExactWeightsAndOpaqueCoverage()
        {
            GameObject host = new GameObject("Lookout test viewport", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(980, 650);
                var start = new NavigationLookoutFrame { Terrain = SeaTerrainType.OpenWater,
                    Light = new NavigationLookoutBlend(NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, .35f), Observations = Array.Empty<LookoutObservation>() };
                var end = new NavigationLookoutFrame { Terrain = SeaTerrainType.StormFront,
                    Light = new NavigationLookoutBlend(NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night, .6f), Observations = Array.Empty<LookoutObservation>() };
                var ocean = Ocean(host.transform, "Lookout Ocean", NavigationLookoutArtCatalog.ForEnvironment(start.Terrain, start.Light.From));
                var transition = Ocean(host.transform, "Lookout Ocean Transition", NavigationLookoutArtCatalog.ForEnvironment(start.Terrain, start.Light.To));
                var motion = ocean.gameObject.AddComponent<NavigationLookoutMotion>();
                motion.Initialize(null, ocean, transition);
                motion.PrepareVoyage(new[] { start, end }, (item, color) => throw new InvalidOperationException("No scenery expected."));
                Texture[] textures = { ocean.texture, transition.texture,
                    NavigationLookoutArtCatalog.ForEnvironment(end.Terrain, end.Light.From), NavigationLookoutArtCatalog.ForEnvironment(end.Terrain, end.Light.To) };
                foreach (float t in new[] { 0f, .1f, .35f, .5f, .9f, 1f })
                {
                    motion.ApplyVoyageProgress(t);
                    var images = host.GetComponentsInChildren<NavigationLookoutOceanGraphic>();
                    float[] effective = new float[4];
                    float uncovered = 1f;
                    for (int i = images.Length - 1; i >= 0; i--)
                    {
                        int index = Array.IndexOf(textures, images[i].texture);
                        effective[index] += images[i].color.a * uncovered;
                        uncovered *= 1f - images[i].color.a;
                        Assert.That(images[i].raycastTarget, Is.False);
                    }
                    Assert.That(uncovered, Is.LessThan(.00001f), "Weather transitions must not reveal the dark backing.");
                    Assert.That(effective[0], Is.EqualTo((1f - t) * .65f).Within(.0001f));
                    Assert.That(effective[1], Is.EqualTo((1f - t) * .35f).Within(.0001f));
                    Assert.That(effective[2], Is.EqualTo(t * .4f).Within(.0001f));
                    Assert.That(effective[3], Is.EqualTo(t * .6f).Within(.0001f));
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static NavigationLookoutOceanGraphic Ocean(Transform parent, string name, Texture texture)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            var image = host.AddComponent<NavigationLookoutOceanGraphic>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }
    }
}
