using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BoardingSpatialSchool_PlayerEffectsBindToActualFeetAndSkyForAllSchools()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string[] cards={"fire_01","arcane_01","curse_01","tide_04","thunder_03","blood_01"};
            string[] schools={"fire","arcane","curse","tide","thunder","blood"};
            for(int i=0;i<cards.Length;i++)
            {
                CoreVoyageSession session=PrepareSchoolCastBattle(controller,cards[i]);
                yield return null;
                var stage=RequiredResponsive<RectTransform>("Battle Card");
                var source=RequiredResponsive<RectTransform>("Player Ship Art");
                var target=RequiredResponsive<RectTransform>("Enemy Ship Art");
                Assert.That(session.CanPlayCard(0),Is.True,session.CardUnavailableReason(0));
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                yield return ObserveSpatialBoardingAction(controller,stage,source,target,
                    (BattleSchoolAttackKind)Enum.Parse(typeof(BattleSchoolAttackKind),schools[i],true));
                Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
                Assert.That(RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track").DisplayedHull,
                    Is.EqualTo(session.Battle.EnemyBoardingHealth));
                AssertResponsiveHandMatchesCore(session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingSpatialSchool_EnemyThunderReversesSourceAndTargetWithoutMovingFloor()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session=CreateSecondBatchBattle(controller,true);
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy,CoreBoardingEnemyCatalog.ForSchool("thunder"));
            SetResponsiveHand(session,"card_crew_guard","card_crew_guard");
            SetResponsiveDrawPile(session,"card_crew_guard","card_crew_guard");
            RenderResponsiveBattle(controller);
            yield return null;
            var stage=RequiredResponsive<RectTransform>("Battle Card");
            var source=RequiredResponsive<RectTransform>("Enemy Ship Art");
            var target=RequiredResponsive<RectTransform>("Player Ship Art");
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return ObserveSpatialBoardingAction(controller,stage,source,target,BattleSchoolAttackKind.Thunder);
            Assert.That(session.Battle.BoardingEnemy.LastActions.Any(a=>a.RawAttack>0),Is.True);
            Assert.That(RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").DisplayedHull,
                Is.EqualTo(session.Battle.PlayerBoardingHealth));
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator ObserveSpatialBoardingAction(CoreGameplayDemoController controller,RectTransform stage,
            RectTransform source,RectTransform target,BattleSchoolAttackKind school)
        {
            var seen=new HashSet<int>();
            var targetHud=RequiredResponsive<BattleDefenseHud>(target.name.StartsWith("Enemy",StringComparison.Ordinal)?"Enemy Battle Hull Bar Track":"Player Battle Hull Bar Track");
            float expectedBeforeContact=targetHud.DisplayedHull;
            float deadline=Time.realtimeSinceStartup+15f;
            do
            {
                Assert.That(stage.Find("Battle Card Activation Glow"),Is.Null,"School reveal must not keep the old shared center ring.");
                Assert.That(stage.Find("Battle Card Cast Stamp Ring"),Is.Null);
                foreach(var attack in ActiveSecondBatchAttacks())
                {
                    if(seen.Add(attack.GetInstanceID())&&seen.Count>1)expectedBeforeContact=targetHud.DisplayedHull;
                    if(attack.Progress<attack.ContactAt-.04f)
                        Assert.That(targetHud.DisplayedHull,Is.EqualTo(expectedBeforeContact).Within(.01f),"The health display must wait for the actual spatial contact.");
                    Assert.That(attack.Kind,Is.EqualTo(school));
                    Vector2 sourceFoot=stage.InverseTransformPoint(source.TransformPoint(new Vector3(0,source.rect.yMin,0)));
                    Vector2 targetFoot=stage.InverseTransformPoint(target.TransformPoint(new Vector3(0,target.rect.yMin,0)));
                    Assert.That(Vector2.Distance(attack.SourceDeck,sourceFoot),Is.LessThan(.1f),"Use the actor's actual boots, not its chest or a guessed constant.");
                    Assert.That(Vector2.Distance(attack.TargetDeck,targetFoot),Is.LessThan(.1f));
                    Assert.That(attack.SkyY,Is.EqualTo(stage.rect.yMax-12f).Within(.1f));
                    Assert.That(attack.SkyY,Is.GreaterThan(targetFoot.y+target.rect.height),"Sky strike must start above the full figure.");
                    Assert.That(attack.PresentationDuration,Is.GreaterThanOrEqualTo(1f));
                    Assert.That(attack.ContactAt,Is.InRange(.3f,.45f));
                    Assert.That(attack.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
                }
                if(!controller.IsBattleAnimationPlaying)break;
                yield return null;
            }while(Time.realtimeSinceStartup<deadline);
            Assert.That(seen.Count,Is.GreaterThan(0),"Expected a real committed attack.");
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            Assert.That(ActiveSecondBatchAttacks(),Is.Empty);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable,Is.True);
            yield return null;
        }
    }
}