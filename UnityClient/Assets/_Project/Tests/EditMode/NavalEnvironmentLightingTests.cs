using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalEnvironmentLightingTests
    {
        private static readonly SeaTerrainType[] Terrains = { SeaTerrainType.OpenWater, SeaTerrainType.Beach,
            SeaTerrainType.Reef, SeaTerrainType.Strait, SeaTerrainType.FogBank, SeaTerrainType.StormFront };

        [Test]
        public void PersistentClock_UsesLookoutTransitionsAcrossMidnightAndInvalidInput()
        {
            foreach (double hours in new[] { 0d, 9d, 10.5d, 12d, 14d, 21d, 23d, 38d,
                -1d, double.NaN, double.PositiveInfinity })
            {
                NavalEnvironmentLight light = NavalEnvironmentLighting.ForVoyageHours(SeaTerrainType.OpenWater, hours);
                NavigationLookoutBlend lookout = NavigationLookoutArtCatalog.BlendForVoyageHours(hours);
                Assert.That(light.Phase, Is.EqualTo(NavigationTimeOfDayUtility.FromVoyageHours(hours)));
                Assert.That(light.Blend.From, Is.EqualTo(lookout.From));
                Assert.That(light.Blend.To, Is.EqualTo(lookout.To));
                Assert.That(light.Blend.Amount, Is.EqualTo(lookout.Amount));
                Assert.That(float.IsNaN(light.SeaTint.r), Is.False);
            }
            Assert.That(NavalEnvironmentLighting.ForVoyageHours(SeaTerrainType.OpenWater, 14d).SeaTint,
                Is.EqualTo(NavalEnvironmentLighting.ForVoyageHours(SeaTerrainType.OpenWater, 38d).SeaTint));
        }

        [Test]
        public void EveryTransition_IsContinuousForSeaHullsAndReflection()
        {
            foreach (SeaTerrainType terrain in Terrains)
            foreach (double localHour in new[] { 0d, 5d, 7d, 16.5d, 18d, 19d, 20.5d, 24d })
            {
                double hours = localHour + 16d; // Next day's local clock, keeping elapsed time non-negative.
                NavalEnvironmentLight before = NavalEnvironmentLighting.ForVoyageHours(terrain, hours - .00001d);
                NavalEnvironmentLight after = NavalEnvironmentLighting.ForVoyageHours(terrain, hours + .00001d);
                Assert.That(Vector4.Distance(before.SeaTint, after.SeaTint), Is.LessThan(.0001f), terrain + " sea " + localHour);
                Assert.That(Vector4.Distance(before.ShipTint, after.ShipTint), Is.LessThan(.0001f), terrain + " hull " + localHour);
                Assert.That(Vector4.Distance(before.ReflectionColor, after.ReflectionColor), Is.LessThan(.0001f), terrain + " reflection " + localHour);
            }
        }

        [Test]
        public void SixTerrainPaintings_RetainDayAppearanceAndReadableOpaqueNightHulls()
        {
            foreach (SeaTerrainType terrain in Terrains)
            {
                NavalEnvironmentLight day = NavalEnvironmentLighting.ForVoyageHours(terrain, 0d);
                NavalEnvironmentLight dusk = NavalEnvironmentLighting.ForVoyageHours(terrain, 10.5d);
                NavalEnvironmentLight night = NavalEnvironmentLighting.ForVoyageHours(terrain, 14d);
                Assert.That(day.SeaTint, Is.EqualTo(terrain == SeaTerrainType.StormFront ? new Color(.8f, .8f, .8f, 1f) : Color.white));
                Assert.That(day.ReflectionColor.a, Is.Zero);
                Assert.That(dusk.SeaTint.r, Is.GreaterThan(dusk.SeaTint.b));
                Assert.That(night.SeaTint.b, Is.GreaterThan(night.SeaTint.r));
                Assert.That(night.ShipTint.r, Is.GreaterThan(.75f), "Small dark hulls must retain detail: " + terrain);
                Assert.That(night.ShipTint.g, Is.GreaterThan(night.SeaTint.g));
                Assert.That(night.SeaTint.a, Is.EqualTo(1f));
                Assert.That(night.ShipTint.a, Is.EqualTo(1f));
                Assert.That(dusk.ReflectionColor.a, Is.InRange(0f, .13f));
            }
        }

        [Test]
        public void RetainedBoard_RefreshesClockTerrainAndReenableWithoutChangingPoseOrAccumulatingTint()
        {
            var host = new GameObject("Naval lighting fixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                RectTransform root = (RectTransform)host.transform;
                RectTransform boardRect = Child("Board", root);
                var board = boardRect.gameObject.AddComponent<NavalTacticalBoardView>();
                RectTransform player = Child("Player", root), enemy = Child("Enemy", root);
                Image playerImage = player.gameObject.AddComponent<Image>(), enemyImage = enemy.gameObject.AddComponent<Image>();
                var session = new CoreVoyageSession();
                Assert.That(session.RestoreGameClock(0d), Is.True);
                var battle = new CoreBattleState { DistanceMeters = 600f };
                board.Configure(battle, player, enemy, Child("Player hull", root), Child("Enemy hull", root),
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), session);
                board.ExtendSeaToBattleSurface();
                RawImage sea = host.GetComponentsInChildren<RawImage>().Single(item => item.name == "Shared tactical sea");
                Texture originalSea = sea.texture;
                Sprite originalPlayer = playerImage.sprite, originalEnemy = enemyImage.sprite;
                Vector3 position = player.localPosition;
                Quaternion rotation = player.localRotation;
                Rect uv = sea.uvRect;
                int objects = host.GetComponentsInChildren<Transform>(true).Length;
                foreach (double hours in new[] { 10.5d, 14d, 0d, 14d, 0d })
                {
                    Assert.That(session.RestoreGameClock(hours), Is.True);
                    board.SnapToBattle(battle); // Retained battle refresh uses this existing path.
                    Assert.That(sea.color, Is.EqualTo(board.EnvironmentLight.SeaTint));
                    Assert.That(playerImage.color, Is.EqualTo(board.EnvironmentLight.ShipTint));
                    Assert.That(enemyImage.color, Is.EqualTo(playerImage.color));
                    Assert.That(playerImage.sprite, Is.SameAs(originalPlayer));
                    Assert.That(enemyImage.sprite, Is.SameAs(originalEnemy));
                    Assert.That(sea.texture, Is.SameAs(originalSea));
                    Assert.That(player.localPosition, Is.EqualTo(position));
                    Assert.That(player.localRotation, Is.EqualTo(rotation));
                    Assert.That(sea.uvRect, Is.EqualTo(uv));
                }
                board.gameObject.SetActive(false);
                InvokeRuntimeLifecycle(board, "OnDisable");
                Assert.That(session.RestoreGameClock(14d), Is.True);
                SetTerrain(session, SeaTerrainType.StormFront);
                board.gameObject.SetActive(true);
                // This runtime-only MonoBehaviour has no ExecuteAlways attribute.
                // EditMode drives the lifecycle explicitly; the companion PlayMode
                // test asserts real OnEnable before LateUpdate can mask a failure.
                InvokeRuntimeLifecycle(board, "OnEnable");
                Assert.That(sea.texture, Is.SameAs(NavalTacticalArtCatalog.ForSea(SeaTerrainType.StormFront)));
                Assert.That(board.EnvironmentLight.Phase, Is.EqualTo(NavigationTimeOfDay.Night));
                Assert.That(playerImage.color, Is.EqualTo(board.EnvironmentLight.ShipTint));
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                NavalSeaLightGraphic reflection = host.GetComponentsInChildren<NavalSeaLightGraphic>(true).Single();
                Assert.That(reflection.transform.parent, Is.SameAs(sea.transform));
                Assert.That(reflection.raycastTarget, Is.False);
                Assert.That(sea.raycastTarget, Is.False);
                Assert.That(reflection.color, Is.EqualTo(board.EnvironmentLight.ReflectionColor));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void InvokeRuntimeLifecycle(NavalTacticalBoardView board, string name)
        {
            MethodInfo lifecycle = typeof(NavalTacticalBoardView).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lifecycle, Is.Not.Null, "Expected runtime lifecycle: " + name);
            lifecycle.Invoke(board, null);
        }

        private static void SetTerrain(CoreVoyageSession session, SeaTerrainType terrain) =>
            typeof(CoreVoyageSession).GetProperty("BattleTerrain", BindingFlags.Instance | BindingFlags.Public)
                .GetSetMethod(true).Invoke(session, new object[] { terrain });

        private static RectTransform Child(string name, RectTransform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
