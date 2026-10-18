using System.IO;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingJoinedDeckTests
    {
        [Test]
        public void JoinedDeck_LeavesSkyTransparentAndBothCombatLanesOpaque()
        {
            Texture2D texture = BoardingBattleArtCatalog.JoinedDeck();
            Assert.That(texture, Is.Not.Null);
            Assert.That(BoardingBattleArtCatalog.JoinedDeck(), Is.SameAs(texture));
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(pixels.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath, "_Project/Resources/" + BoardingBattleArtCatalog.JoinedDeckResourcePath + ".png"))), Is.True);
                Assert.That(pixels.GetPixel(texture.width / 2, Mathf.RoundToInt(texture.height * .92f)).a, Is.LessThan(.02f));
                Vector2 frame = new Vector2(1860f, 900f);
                Rect uv = BoardingBattleLayout.JoinedDeckUv(texture, frame);
                float feetY = BoardingBattleLayout.PartyPosition(false).y - BoardingBattleLayout.PartySize.y * .5f;
                float v = uv.y + uv.height * (.5f + feetY / frame.y);
                for (float x = -740f; x <= 740f; x += 20f)
                {
                    float u = uv.x + uv.width * (.5f + x / frame.x);
                    Assert.That(pixels.GetPixelBilinear(u, v).a, Is.GreaterThan(.98f), "The complete left/right lunge corridor needs solid deck at x=" + x);
                }
                Assert.That(texture.width * uv.width / (texture.height * uv.height), Is.EqualTo(frame.x / frame.y).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(pixels);
            }
        }

        [TestCase(SeaTerrainType.OpenWater)]
        [TestCase(SeaTerrainType.Beach)]
        [TestCase(SeaTerrainType.Strait)]
        [TestCase(SeaTerrainType.Reef)]
        [TestCase(SeaTerrainType.FogBank)]
        [TestCase(SeaTerrainType.StormFront)]
        public void SceneryCrop_ExcludesOldDeckAndPreservesAspectForEveryTime(SeaTerrainType terrain)
        {
            foreach (NavigationTimeOfDay time in new[] { NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night })
            {
                Texture2D texture = BoardingBattleArtCatalog.ForEnvironment(terrain, time);
                Rect uv = BoardingBattleLayout.SceneryUv(texture, new Vector2(1860f, 400f));
                Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(.61f));
                Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f));
                Assert.That(uv.xMin, Is.GreaterThan(.1f));
                Assert.That(uv.xMax, Is.LessThan(.9f));
                Assert.That(texture.width * uv.width / (texture.height * uv.height), Is.EqualTo(1860f / 400f).Within(.001f));
            }
        }

        [Test]
        public void DeckLighting_KeepsOpaqueFloorAcrossAllTimesAndWeather()
        {
            Color day = BoardingBattleArtCatalog.DeckLight(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day);
            Color dusk = BoardingBattleArtCatalog.DeckLight(SeaTerrainType.OpenWater, NavigationTimeOfDay.Dusk);
            Color night = BoardingBattleArtCatalog.DeckLight(SeaTerrainType.OpenWater, NavigationTimeOfDay.Night);
            Color storm = BoardingBattleArtCatalog.DeckLight(SeaTerrainType.StormFront, NavigationTimeOfDay.Night);
            Assert.That(day, Is.Not.EqualTo(dusk));
            Assert.That(dusk, Is.Not.EqualTo(night));
            Assert.That(night.b, Is.GreaterThan(night.r));
            Assert.That(storm.grayscale, Is.LessThan(night.grayscale));
            foreach (Color tint in new[] { day, dusk, night, storm }) Assert.That(tint.a, Is.EqualTo(1f));
        }
    }
}
