using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class NavalTacticalArtFlowTests
    {
        private readonly NavalBattleVisualFeedbackTests fixture = new NavalBattleVisualFeedbackTests();
        private CoreGameplayDemoController controller;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return fixture.SetUp();
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
        }
        [UnityTearDown] public IEnumerator TearDown() { yield return fixture.TearDown(); }

        [UnityTest]
        public IEnumerator ProductionBattle_UsesSelectedHullNamedEnemyAndAllSixSeas_WithoutChangingPoses()
        {
            SeaTerrainType[] terrains = { SeaTerrainType.OpenWater, SeaTerrainType.Beach, SeaTerrainType.Reef,
                SeaTerrainType.Strait, SeaTerrainType.FogBank, SeaTerrainType.StormFront };
            int[] players = { 0, 1, 2, 8, 3, 11 };
            for (int index = 0; index < terrains.Length; index++)
            {
                var fresh = typeof(NavalBattleVisualFeedbackTests).GetMethod("UseFreshNavalBattle", BindingFlags.Static | BindingFlags.NonPublic);
                var session = (CoreVoyageSession)fresh.Invoke(null, new object[] { controller, 600f, players[index], false });
                CoreEncounter encounter = index % 2 == 0 ? CoreRouteBossCatalog.All[index].CreateEncounter() :
                    CoreLegendaryWarships.All[index].CreateEncounter();
                Set(session, "ActiveEncounter", encounter);
                Set(session, "BattleTerrain", terrains[index]);
                session.Battle.Profile = CoreBattleBalance.Resolve(encounter, session.SelectedRouteIndex);
                session.Battle.EnemyName = encounter.Name;
                session.Battle.IsBoss = session.Battle.Profile.IsBoss;
                session.Battle.EnemyHull = session.Battle.EnemyMaxHull = session.Battle.Profile.EnemyHull;
                VoyagePoint beforePlayer = session.Battle.PlayerPositionMeters, beforeEnemy = session.Battle.EnemyPositionMeters;
                typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(controller, null);
                yield return null;
                yield return null;
                float deadline = Time.realtimeSinceStartup + 5f;
                while (controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying), Is.False);
                Canvas.ForceUpdateCanvases();
                var board = Named<NavalTacticalBoardView>("Naval Tactical Board");
                var player = Named<Image>("Player Ship Art");
                var enemy = Named<Image>("Enemy Ship Art");
                Assert.That(player.sprite, Is.SameAs(Resources.Load<Sprite>(NavalTacticalArtCatalog.PlayerShipResourcePath(session.SelectedShip.Id))));
                Assert.That(enemy.sprite, Is.SameAs(Resources.Load<Sprite>(NavalTacticalArtCatalog.EnemyShipResourcePath(session.Battle, encounter, session.SelectedRouteIndex))));
                Assert.That(player.sprite, Is.Not.SameAs(enemy.sprite));
                Assert.That(player.sprite, Is.Not.Null);
                Assert.That(enemy.sprite, Is.Not.Null);
                Assert.That(player.rectTransform.sizeDelta, Is.EqualTo(new Vector2(112f, 168f)));
                Assert.That(enemy.rectTransform.sizeDelta, Is.EqualTo(new Vector2(112f, 168f)));
                Assert.That(session.Battle.PlayerPositionMeters, Is.EqualTo(beforePlayer));
                Assert.That(session.Battle.EnemyPositionMeters, Is.EqualTo(beforeEnemy));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.rectTransform.localEulerAngles.z,
                    session.Battle.TacticalFrame.PlayerHeadingDegrees + board.CameraRotationDegrees - 90f)), Is.LessThan(.001f));
                RawImage sea = Named<RawImage>("Shared tactical sea");
                Assert.That(sea.texture, Is.SameAs(Resources.Load<Texture2D>(NavalTacticalArtCatalog.SeaResourcePath(terrains[index]))));
                Assert.That(sea.texture, Is.Not.Null);
                Assert.That(sea.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(sea.raycastTarget, Is.False);
                NavalBattleVisualCapture.Capture(controller, "TacticalArt_" + index + "_" + terrains[index] + ".png",
                    session.SelectedShip.Name + " / " + encounter.Name + " / " + terrains[index]);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LegendaryIronOath_KeepsItsCurseSpriteWhenTheEncounterPointerIsOmitted()
        {
            var fresh = typeof(NavalBattleVisualFeedbackTests).GetMethod("UseFreshNavalBattle", BindingFlags.Static | BindingFlags.NonPublic);
            var session = (CoreVoyageSession)fresh.Invoke(null, new object[] { controller, 600f, 7, false });
            CoreLegendaryWarship ironOath = CoreLegendaryWarships.Find("legendary_pirate_iron_oath");
            CoreEncounter encounter = ironOath.CreateEncounter();
            Set(session, "ActiveEncounter", encounter);
            Set(session, "BattleTerrain", SeaTerrainType.FogBank);
            session.Battle.Profile = ironOath.Profile;
            session.Battle.EnemyName = encounter.Name;
            session.Battle.IsBoss = true;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = ironOath.Profile.EnemyHull;
            typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(controller, null);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying), Is.False);
            Canvas.ForceUpdateCanvases();
            Sprite expected = Resources.Load<Sprite>("NavalTactics/Ships/legendary_curse_v2");
            Assert.That(expected, Is.Not.Null);
            Assert.That(Named<Image>("Enemy Ship Art").sprite, Is.SameAs(expected));
            Assert.That(Named<Image>("Player Ship Art").sprite, Is.SameAs(Resources.Load<Sprite>("NavalTactics/Ships/gunship_v2")));
            NavalBattleVisualCapture.Capture(controller, "TacticalArt_IronOath.png",
                session.SelectedShip.Name + " / " + encounter.Name + " / named curse identity retained for Ironclad profile");

            Set(session, "ActiveEncounter", null);
            typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(controller, null);
            yield return null;
            Assert.That(Named<Image>("Enemy Ship Art").sprite, Is.SameAs(expected),
                "A checkpoint without an encounter pointer must retain the named legend, not switch to an ordinary Ironclad hull.");
            LogAssert.NoUnexpectedReceived();
        }

        private T Named<T>(string name) where T : Component => controller.GetComponentsInChildren<T>(false).Single(item => item.name == name);
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetSetMethod(true).Invoke(target, new[] { value });
    }
}
