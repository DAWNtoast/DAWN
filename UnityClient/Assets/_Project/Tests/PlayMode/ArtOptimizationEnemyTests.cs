using System.Collections;
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
        public IEnumerator ArtOptimization03_DedicatedEnemyCrewsKeepSixBootShadowsAndStaticPoses()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                string[] ids = { "legendary_pirate_last_horizon", "boss_route_moon_tide_ruins_arcane" };
                string[] keys = { "last_horizon", "moon_weave" };
                for (int index = 0; index < ids.Length; index++)
                {
                    CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation, gameHours: index == 0 ? 0d : 14d);
                    CoreEncounter encounter = CoreLegendaryWarships.Find(ids[index])?.CreateEncounter() ??
                        CoreRouteBossCatalog.Find(ids[index]).CreateEncounter();
                    var node = (CoreMapNode)typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    node.Encounter = encounter;
                    session.Battle.Profile = CoreLegendaryWarships.Find(ids[index])?.Profile ?? CoreRouteBossCatalog.Find(ids[index]).Profile;
                    session.Battle.EnemyName = encounter.Name;
                    typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(session, new object[] { NavalBattleStage.Boarding });
                    RenderResponsiveBattle(controller);
                    yield return ArtOptimizationWaitForStableBattle(controller);
                    RawImage figure = RequiredResponsive<RawImage>("Enemy Ship Art Party Illustration");
                    Assert.That(figure.texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Characters/boarding_enemy_" + keys[index] + "_v1")));
                    BoardingCinematicAssertions.AssertFullBody(figure, figure.uvRect);
                    var shadow = RequiredResponsive<BoardingContactShadowGraphic>(figure.name + " Deck Contact Shadow");
                    Assert.That(shadow.ContactCount, Is.EqualTo(6));
                    AssertGroundShadowContacts(shadow, figure);
                    var poses = SnapshotGroundShadowFigures();
                    string rules = GroundShadowRules(session);
                    NavalBattleVisualCapture.Capture(controller, "Art03_" + keys[index] + ".png",
                        "Production static dedicated formation, six measured boot shadows, original deck and HUD.");
                    yield return null;
                    foreach (var pose in poses) pose.AssertUnchanged();
                    Assert.That(GroundShadowRules(session), Is.EqualTo(rules));
                }
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
