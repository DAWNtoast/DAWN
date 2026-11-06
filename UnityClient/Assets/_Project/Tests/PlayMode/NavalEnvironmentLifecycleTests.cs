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
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavalEnvironmentLifecycle_RealOnEnableRefreshesBeforeLateUpdateAndPreservesRotatedLayout()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, gameHours: 0d);
            MethodInfo setPose = typeof(CoreBattleState).GetMethod("SetTacticalPose",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setPose, Is.Not.Null);
            // Legal grid coordinates and cardinal headings give a 33.69-degree
            // camera/ship rotation. Only battle state is arranged; production
            // Render creates the ordinary battlefield, hand and card layout.
            setPose.Invoke(session.Battle, new object[] { true, new VoyagePoint(-600f, 400f), 90f });
            setPose.Invoke(session.Battle, new object[] { false, new VoyagePoint(0f, 0f), 270f });
            SetNavalLifecycleSessionProperty(session, "BattleTerrain", SeaTerrainType.OpenWater);
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying), Is.False);
            Canvas.ForceUpdateCanvases();

            NavalTacticalBoardView board = RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board");
            RawImage sea = RequiredResponsive<RawImage>("Shared tactical sea");
            Image player = RequiredResponsive<Image>("Player Ship Art");
            Image enemy = RequiredResponsive<Image>("Enemy Ship Art");
            RectTransform hand = RequiredResponsive<RectTransform>("Battle Hand");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(34f, board.CameraRotationDegrees)), Is.LessThan(.5f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(34f, player.rectTransform.localEulerAngles.z)), Is.LessThan(.5f));
            // Ordinary naval rendering runs ArrangeBattleHand after creating
            // the shared hand; preserve that production fan layout as rendered.
            Assert.That(hand.sizeDelta, Is.EqualTo(new Vector2(1120f, 200f)));
            Assert.That(hand.anchoredPosition, Is.EqualTo(new Vector2(0f, -350f)));
            AssertResponsiveHandMatchesCore(session);
            Assert.That(sea.texture, Is.SameAs(NavalTacticalArtCatalog.ForSea(SeaTerrainType.OpenWater)));
            Assert.That(board.EnvironmentLight.Phase, Is.EqualTo(NavigationTimeOfDay.Day));

            Sprite playerSprite = player.sprite, enemySprite = enemy.sprite;
            Vector3 playerPosition = player.transform.localPosition, enemyPosition = enemy.transform.localPosition;
            Quaternion playerRotation = player.transform.localRotation, enemyRotation = enemy.transform.localRotation;
            Vector2 playerSize = player.rectTransform.sizeDelta, enemySize = enemy.rectTransform.sizeDelta;
            Rect seaUv = sea.uvRect;
            float cameraRotation = board.CameraRotationDegrees;
            int boardObjects = board.GetComponentsInChildren<Transform>(true).Length;
            VoyagePoint playerWorld = session.Battle.PlayerPositionMeters, enemyWorld = session.Battle.EnemyPositionMeters;
            int[] handCards = session.Battle.Hand.ToArray();
            RectTransform[] handRects = hand.GetComponentsInChildren<RectTransform>(true);
            Vector2[] handPositions = handRects.Select(rect => rect.anchoredPosition).ToArray();
            Vector2[] handSizes = handRects.Select(rect => rect.sizeDelta).ToArray();
            Quaternion[] handRotations = handRects.Select(rect => rect.localRotation).ToArray();
            Vector3[] handScales = handRects.Select(rect => rect.localScale).ToArray();
            NavalSeaLightGraphic reflection = sea.GetComponentInChildren<NavalSeaLightGraphic>(true);
            Assert.That(reflection, Is.Not.Null);
            Assert.That(reflection.transform.parent, Is.SameAs(sea.transform));
            Assert.That(reflection.raycastTarget, Is.False);
            Assert.That(sea.raycastTarget, Is.False);
            NavalBattleVisualCapture.Capture(controller, "NavalLifecycle_Rotated34_Day.png",
                "普通运行时海战；船体与海面约34度旋转；原始生产手牌布局");

            SeaTerrainType[] terrains = { SeaTerrainType.StormFront, SeaTerrainType.OpenWater, SeaTerrainType.StormFront };
            double[] hours = { 14d, 0d, 14d };
            for (int cycle = 0; cycle < terrains.Length; cycle++)
            {
                Texture previousTexture = sea.texture;
                Color previousTint = sea.color;
                board.gameObject.SetActive(false);
                Assert.That(board.isActiveAndEnabled, Is.False);
                SetNavalLifecycleSessionProperty(session, "BattleTerrain", terrains[cycle]);
                SetNavalLifecycleSessionProperty(session, "ElapsedGameHours", hours[cycle]);
                Assert.That(sea.texture, Is.SameAs(previousTexture), "Disabled board must retain its last presented environment until re-enabled.");
                Assert.That(sea.color, Is.EqualTo(previousTint));

                // No Render, SnapToBattle, RefreshEnvironment, reflected lifecycle
                // invocation or yield may occur between SetActive and assertions.
                // A missing OnEnable must fail before LateUpdate can refresh it.
                board.gameObject.SetActive(true);
                Assert.That(board.isActiveAndEnabled, Is.True);
                NavalEnvironmentLight expected = NavalEnvironmentLighting.ForVoyageHours(terrains[cycle], hours[cycle]);
                Assert.That(sea.texture, Is.SameAs(NavalTacticalArtCatalog.ForSea(terrains[cycle])), "Real OnEnable must swap the texture synchronously.");
                Assert.That(board.EnvironmentLight.Phase, Is.EqualTo(expected.Phase));
                Assert.That(sea.color, Is.EqualTo(expected.SeaTint));
                Assert.That(player.color, Is.EqualTo(expected.ShipTint));
                Assert.That(enemy.color, Is.EqualTo(expected.ShipTint));
                Assert.That(reflection.color, Is.EqualTo(expected.ReflectionColor));
                Assert.That(player.sprite, Is.SameAs(playerSprite));
                Assert.That(enemy.sprite, Is.SameAs(enemySprite));
                Assert.That(player.transform.localPosition, Is.EqualTo(playerPosition));
                Assert.That(enemy.transform.localPosition, Is.EqualTo(enemyPosition));
                Assert.That(player.transform.localRotation, Is.EqualTo(playerRotation));
                Assert.That(enemy.transform.localRotation, Is.EqualTo(enemyRotation));
                Assert.That(player.rectTransform.sizeDelta, Is.EqualTo(playerSize));
                Assert.That(enemy.rectTransform.sizeDelta, Is.EqualTo(enemySize));
                Assert.That(sea.uvRect, Is.EqualTo(seaUv));
                Assert.That(board.CameraRotationDegrees, Is.EqualTo(cameraRotation));
                Assert.That(board.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(boardObjects));
                Assert.That(session.Battle.PlayerPositionMeters, Is.EqualTo(playerWorld));
                Assert.That(session.Battle.EnemyPositionMeters, Is.EqualTo(enemyWorld));
                CollectionAssert.AreEqual(handCards, session.Battle.Hand);
                Assert.That(RequiredResponsive<RectTransform>("Battle Hand"), Is.SameAs(hand));
                CollectionAssert.AreEqual(handRects, hand.GetComponentsInChildren<RectTransform>(true));
                for (int index = 0; index < handRects.Length; index++)
                {
                    Assert.That(handRects[index].anchoredPosition, Is.EqualTo(handPositions[index]), handRects[index].name);
                    Assert.That(handRects[index].sizeDelta, Is.EqualTo(handSizes[index]), handRects[index].name);
                    Assert.That(handRects[index].localRotation, Is.EqualTo(handRotations[index]), handRects[index].name);
                    Assert.That(handRects[index].localScale, Is.EqualTo(handScales[index]), handRects[index].name);
                }
                AssertResponsiveHandMatchesCore(session);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(sea.color, Is.EqualTo(expected.SeaTint), "Later frames must not accumulate tint.");
                Assert.That(player.color, Is.EqualTo(expected.ShipTint));
                Assert.That(enemy.color, Is.EqualTo(expected.ShipTint));
                NavalBattleVisualCapture.Capture(controller, "NavalLifecycle_Rotated34_Reenable_" + cycle + ".png",
                    "真实启停第" + (cycle + 1) + "次；" + terrains[cycle] + " / " + expected.Phase + "；未重排手牌或重建海战");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetNavalLifecycleSessionProperty(CoreVoyageSession session, string property, object value)
        {
            MethodInfo setter = typeof(CoreVoyageSession).GetProperty(property,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, "Expected fixture state setter: " + property);
            setter.Invoke(session, new[] { value });
        }
    }
}
