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
        public IEnumerator BoardingDedicatedParties_LethalCardShowsTheNamedFallenCrewUntilResolutionEnds()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = PrepareActorCrewBattle(controller, "basic_01", out _);
                var warship = CoreLegendaryWarships.Find("legendary_pirate_iron_oath");
                var encounter = warship.CreateEncounter();
                typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
                session.Battle.Profile = warship.Profile;
                session.Battle.EnemyBoardingHealth = 1;
                SetResponsiveHand(session, "card_crew_thrust", "card_crew_guard");
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                // A first encounter shows its pre-battle panels before cards accept input.
                for (int panel = 0; panel < 16 && GameObject.Find("Legendary Fleet Comic Advance") != null; panel++)
                {
                    RequiredResponsive<Button>("Legendary Fleet Comic Advance").onClick.Invoke();
                    yield return null;
                }
                Assert.That(GameObject.Find("Legendary Fleet Comic Advance"), Is.Null);
                yield return WaitResponsiveBattle(controller);
                CoreBattleState battle = session.Battle;
                Assert.That(warship.IsSparring, Is.False);
                Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(1));
                Assert.That(battle.EnemyBlock, Is.Zero);
                Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
                var enemy = RequiredResponsive<BoardingActorGraphic>("Enemy Ship Art Party Illustration");
                Assert.That(enemy.Identity, Is.EqualTo("enemy_iron_oath"));
                Assert.That(enemy.UsesDefeatAtlas, Is.False);
                var attack = RequiredResponsive<Button>("Battle Card 0");
                Assert.That(attack.interactable, Is.True);
                attack.onClick.Invoke();
                Assert.That(controller.IsBattleAnimationPlaying, Is.True, "The accepted card must start the real battle presentation.");
                BattleResolutionMotion resolution = null;
                yield return AwaitSecondBatchResolution(controller, motion => resolution = motion);
                Assert.That(resolution.Kind, Is.EqualTo(BattleResolutionKind.BoardingDefeat));
                AdvanceSecondBatchResolutionTo(resolution, .68f);
                Assert.That(enemy.UsesDefeatAtlas, Is.True);
                Assert.That(enemy.Action, Is.EqualTo(BoardingActorAction.Death));
                Assert.That(enemy.GetComponent<RawImage>().texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Defeats/enemy_iron_oath")));
                NavalBattleVisualCapture.Capture(controller, "Named_IronOath_Lethal_Contact.png", "真实出牌击败铁誓敌阵，显示其绘制的倒地序列");
                string result = SecondBatchOutcomeFingerprint(session);
                yield return WaitResponsiveBattle(controller);
                Assert.That(SecondBatchOutcomeFingerprint(session), Is.EqualTo(result));
                Assert.That(GameObject.Find("Battle Card"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BoardingDedicatedParties_ProductionArtAndSixMeasuredBootShadows()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                string[] ids = { "legendary_pirate_last_horizon", "boss_route_moon_tide_ruins_arcane" };
                string[] names = { "last_horizon", "moon_weave" };
                for (int i = 0; i < ids.Length; i++)
                {
                    CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
                    CoreEncounter encounter = CoreLegendaryWarships.Find(ids[i])?.CreateEncounter() ?? CoreRouteBossCatalog.Find(ids[i]).CreateEncounter();
                    typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
                    session.Battle.Profile = CoreLegendaryWarships.Find(ids[i])?.Profile ?? CoreRouteBossCatalog.Find(ids[i]).Profile;
                    typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(session, new object[] { NavalBattleStage.Boarding });
                    RenderResponsiveBattle(controller);
                    yield return WaitResponsiveBattle(controller);
                    RawImage figure = RequiredResponsive<RawImage>("Enemy Ship Art Party Illustration");
                    Assert.That(figure.texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Characters/boarding_enemy_" + names[i] + "_v1")));
                    var shadow = RequiredResponsive<BoardingContactShadowGraphic>("Enemy Ship Art Party Illustration Deck Contact Shadow");
                    Assert.That(shadow.ContactCount, Is.EqualTo(6));
                    AssertGroundShadowContacts(shadow, figure);
                    Assert.That(RequiredResponsive<RectTransform>("Enemy Ship Art").GetComponentsInChildren<RawImage>(), Has.Length.EqualTo(1));
                    Assert.That(Object.FindObjectsByType<TutorialBoardingBossAttack>(FindObjectsSortMode.None), Is.Empty);
                    NavalBattleVisualCapture.Capture(controller, "DedicatedParty_" + names[i] + ".png",
                        "专属静态三人阵容，保留生产站位与每只靴子的实测接触阴影。");
                    WriteGroundShadowCloseup("DedicatedParty_" + names[i] + ".png", "DedicatedParty_" + names[i] + "_Feet.png");
                }
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
