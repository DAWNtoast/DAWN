using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator AdditionalBattleVfx_AfflictionsFollowHolderAndDangerCrossingDoesNotReplay()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session=CreateNavalVfxFixture(controller,"card_brace");
            session.Battle.PlayerBurnStacks=3; session.Battle.EnemyBurnTurns=session.Battle.EnemyBurnDamage=4;
            typeof(CoreSchoolBattleState).GetProperty("CurseMarks").SetValue(session.Battle.Schools,2);
            RenderResponsiveBattle(controller); yield return WaitResponsiveBattle(controller); yield return null;
            var stage=RequiredResponsive<RectTransform>("Battle Card");
            var enemy=RequiredResponsive<RectTransform>("Enemy Ship Art");
            var effect=stage.Find("Enemy Affliction Effects").GetComponent<BattleAfflictionMotion>();
            Assert.That(effect.HasBurn&&effect.HasCurse&&effect.IsVisible,Is.True);
            Assert.That(effect.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
            Vector2 old=enemy.anchoredPosition; enemy.anchoredPosition+=new Vector2(17f,9f);
            effect.AdvancePresentation(.1f);
            Assert.That(Vector2.Distance(((RectTransform)effect.transform).anchoredPosition,stage.InverseTransformPoint(enemy.position)),Is.LessThan(.1f));
            enemy.anchoredPosition=old;
            var hud=RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track");
            var danger=hud.GetComponent<BattleDangerPulse>();
            hud.SetHull(hud.MaximumHull*.8f,hud.MaximumHull*.8f);
            hud.SetHull(hud.MaximumHull*.2f,hud.MaximumHull*.2f);
            Assert.That(danger.IsPlaying,Is.True); int plays=danger.PlayCount;
            hud.SetHull(hud.MaximumHull*.2f,hud.MaximumHull*.2f);
            Assert.That(danger.PlayCount,Is.EqualTo(plays));
            NavalBattleVisualCapture.Capture(controller,"AdditionalBattleVfx_StatusAndDanger.png","燃烧火星、咒印暗纹与低船体局部脉冲");
            danger.AdvancePresentation(BattleDangerPulse.Duration);
            Assert.That(danger.IsPlaying,Is.False);
            hud.SetHull(hud.MaximumHull*.19f,hud.MaximumHull*.19f);
            Assert.That(danger.PlayCount,Is.EqualTo(plays));
            hud.SetHull(hud.MaximumHull*.6f,hud.MaximumHull*.6f);
            hud.SetHull(hud.MaximumHull*.2f,hud.MaximumHull*.2f);
            Assert.That(danger.PlayCount,Is.EqualTo(plays+1));
            effect.SendMessage("OnApplicationPause",true); Assert.That(effect.IsVisible,Is.False);
            effect.SendMessage("OnApplicationPause",false); Assert.That(effect.IsVisible,Is.True);
            controller.enabled=false; yield return null;
            Assert.That(effect.IsVisible,Is.False); Assert.That(danger.IsPlaying,Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalBattleVfx_EnemySupportCastsOnceWithoutMovingTheFormation()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach(string school in new[]{"arcane","curse","fire"})
            {
                var session=CreateSecondBatchBattle(controller,true); var battle=session.Battle;
                typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(battle.BoardingEnemy,CoreBoardingEnemyCatalog.ForSchool(school));
                battle.PhaseTurn=school=="curse"?2:1;
                SetResponsiveHand(session,"card_crew_guard","card_crew_guard");
                SetResponsiveDrawPile(session,"card_crew_guard","card_crew_guard");
                RenderResponsiveBattle(controller); yield return null;
                var stage=RequiredResponsive<RectTransform>("Battle Card");
                var actor=RequiredResponsive<RectTransform>("Enemy Ship Art"); Vector2 origin=actor.anchoredPosition;
                int turn=battle.Turn; var seen=new HashSet<int>(); bool captured=false;
                var end=RequiredResponsive<Button>("End Battle Turn"); end.onClick.Invoke(); end.onClick.Invoke();
                float deadline=Time.realtimeSinceStartup+20f;
                while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
                {
                    foreach(var cast in stage.GetComponentsInChildren<BoardingSchoolCastGraphic>())
                    {
                        seen.Add(cast.GetInstanceID());
                        Assert.That(cast.Role,Is.Not.EqualTo(BoardingSchoolCastRole.Attack));
                        Assert.That(cast.raycastTarget,Is.False);
                        if(!captured&&cast.Progress>.35f&&cast.Progress<.75f)
                        {
                            NavalBattleVisualCapture.Capture(controller,"AdditionalBattleVfx_Enemy_"+school+".png","敌方非攻击流派施法保持原地站位");
                            captured=true;
                        }
                    }
                    Assert.That(Vector2.Distance(actor.anchoredPosition,origin),Is.LessThan(.01f));
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying,Is.False);
                Assert.That(battle.Turn,Is.EqualTo(turn+1));
                Assert.That(seen.Count,Is.EqualTo(1),school);
                Assert.That(captured,Is.True,school);
                Assert.That(stage.GetComponentsInChildren<BoardingSchoolCastGraphic>(),Is.Empty);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable,Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalBattleVfx_RealTurnSourcesAreConsumedOnceAndCancelCleanly()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session=CreateNavalVfxFixture(controller,"card_brace");
            session.Battle.PlayerBurnStacks=5; session.Battle.PlayerBlock=2;
            SetResponsiveDrawPile(session,"card_brace","card_brace");
            RenderResponsiveBattle(controller); yield return null;
            var stage=RequiredResponsive<RectTransform>("Battle Card");
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+12f; bool captured=false;
            while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
            {
                if(stage.GetComponentsInChildren<Text>().Any(t=>t.name.StartsWith("Battle Settlement ")&&t.text.Contains("燃烧")))
                {
                    NavalBattleVisualCapture.Capture(controller,"AdditionalBattleVfx_BurnSource.png","实际燃烧先扣格挡再扣船体，来源独立呈现");
                    captured=true; break;
                }
                yield return null;
            }
            Assert.That(captured,Is.True);
            int turn=session.Battle.Turn; long sequence=session.Battle.SettlementSequence;
            controller.enabled=false; yield return null;
            Assert.That(stage.GetComponentsInChildren<BattleFxGraphic>().Any(f=>f.name.StartsWith("Battle Settlement Source")),Is.False);
            Assert.That(stage.GetComponentsInChildren<BattleAfflictionMotion>().Any(f=>f.IsVisible),Is.False);
            controller.enabled=true; yield return null; yield return null;
            Assert.That(session.Battle.Turn,Is.EqualTo(turn)); Assert.That(session.Battle.SettlementSequence,Is.EqualTo(sequence));
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Battle Settlement ")),Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalBattleVfx_LethalPlayerEndNeverPlaysAnUnexecutedRam()
        {
            foreach (bool blood in new[] {false,true})
            {
                yield return LoadResponsiveBattleScene();
                var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session=ArrangeRamComicBattle(controller,true,NavalBattleAlignment.HeadOn);
                var battle=session.Battle; long ram=battle.EnemyRamSequence;
                if(blood) Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood,true,-1,0,false,0,0),Is.True);
                else battle.PlayerBurnStacks=1;
                battle.PlayerBlock=0;
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session,1);
                Assert.That(session.EnemyWillRam,Is.True,"The warning may remain visible until the player's end-window actually resolves.");
                RenderResponsiveBattle(controller); yield return null;
                RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
                float deadline=Time.realtimeSinceStartup+12f;
                do
                {
                    Assert.That(GameObject.Find("Battle Ram Comic Overlay"),Is.Null,"A predicted ram must not play after the player died before enemy execution.");
                    Assert.That(Object.FindObjectsByType<NavalCombatFxGraphic>(FindObjectsSortMode.None),Is.Empty);
                    yield return null;
                } while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline);
                Assert.That(controller.IsBattleAnimationPlaying,Is.False);
                Assert.That(battle.EnemyRamSequence,Is.EqualTo(ram));
                Assert.That(battle.LastNavalAttack,Is.Null);
                Assert.That(battle.BoardingEnemy.LastAttackExecuted,Is.False);
                Assert.That(session.Phase,Is.EqualTo(CoreDemoPhase.Settlement));
                Assert.That(session.Settlement.Outcome,Is.EqualTo(VoyageOutcome.Failure));
                Assert.That(battle.SettlementFacts.Any(f=>f.Player&&f.Kind==(blood?CoreBattleSettlementKind.BloodField:CoreBattleSettlementKind.Burn)),Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalBattleVfx_ActualLethalHullRamStillPlaysItsCommittedCollision()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session=ArrangeRamComicBattle(controller,true,NavalBattleAlignment.HeadOn);
            var battle=session.Battle; long ram=battle.EnemyRamSequence;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session,1);
            RenderResponsiveBattle(controller); yield return null;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return WaitForRamComic(controller);
            Assert.That(battle.EnemyRamSequence,Is.EqualTo(ram+1));
            Assert.That(battle.LastEnemyRamConnection,Is.EqualTo(NavalBoardingConnection.RamBowToBow));
            Assert.That(battle.Stage,Is.EqualTo(NavalBattleStage.Naval),"Lethal hull impact never entered boarding.");
            Assert.That(session.Phase,Is.EqualTo(CoreDemoPhase.Settlement));
            float deadline=Time.realtimeSinceStartup+12f;
            while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
            { AdvanceRamComicIfWaiting(controller); yield return null; }
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            Assert.That(battle.EnemyRamSequence,Is.EqualTo(ram+1),"Reading the collision cannot execute another ram.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalBattleVfx_DelayedCurseExecutionKeepsOneContactWithoutHealthRewind()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session=CreateSecondBatchBattle(controller,true);
            SetResponsiveHand(session,"card_boarding_thunder_05","card_crew_guard");
            SetResponsiveDrawPile(session,"card_crew_guard","card_crew_guard");
            RenderResponsiveBattle(controller); yield return null;
            var battle=session.Battle;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return AssertNoSecondBatchAttackDuringAction(controller);
            Assert.That(battle.Boarding.DelayedDamage.Count,Is.EqualTo(1));
            int scheduled=battle.Boarding.DelayedDamage[0].Amount;
            Assert.That(scheduled,Is.GreaterThan(1));
            battle.EnemyBoardingHealth=scheduled+2;
            battle.EnemyBlock=0;
            typeof(CoreVoyageSession).GetMethod("CTBAddMarks",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{3});
            Assert.That(battle.Boarding.EnemyExecutedByCurse,Is.False);
            RenderResponsiveBattle(controller); yield return null;
            var stage=RequiredResponsive<RectTransform>("Battle Card");
            var enemyHud=RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track");
            float previous=enemyHud.DisplayedHull; int expectedBefore=scheduled+2;
            long sequence=battle.SettlementSequence;
            var attacks=new HashSet<int>(); var executionLabels=new HashSet<int>();
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+16f;
            while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
            {
                if(stage!=null)
                {
                    Assert.That(stage.GetComponentsInChildren<Text>().Any(t=>t.name.StartsWith("Battle Settlement ")&&t.text.Contains("咒印消灭")),Is.False,
                        "The passive source player must not repeat a delayed attack's curse execution.");
                    foreach(var attack in stage.GetComponentsInChildren<BattleSchoolAttackGraphic>())
                    {
                        attacks.Add(attack.GetInstanceID());
                        if(attack.Progress<attack.ContactAt-.04f&&enemyHud!=null)
                            Assert.That(enemyHud.DisplayedHull,Is.EqualTo(expectedBefore).Within(.01f),"Health must wait for the delayed attack's actual contact.");
                        foreach(var label in attack.GetComponentsInChildren<Text>())
                            if(label.text.Contains("咒印消灭")) executionLabels.Add(label.GetInstanceID());
                    }
                }
                if(enemyHud!=null)
                {
                    Assert.That(enemyHud.DisplayedHull,Is.LessThanOrEqualTo(previous+.01f),"The health bar must never rewind for a second copy of the execution.");
                    previous=enemyHud.DisplayedHull;
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            Assert.That(attacks.Count,Is.EqualTo(1)); Assert.That(executionLabels.Count,Is.EqualTo(1));
            var facts=battle.SettlementFacts.Where(f=>f.Sequence>sequence&&f.Kind==CoreBattleSettlementKind.CurseExecution).ToArray();
            Assert.That(facts.Length,Is.EqualTo(1));
            Assert.That(facts[0].Window,Is.EqualTo(CoreBattleSettlementWindow.DelayedAttack));
            Assert.That(facts[0].HealthLost,Is.EqualTo(2));
            Assert.That(battle.EnemyBoardingHealth,Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
