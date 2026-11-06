using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public IEnumerator NavalVfx_CannonCommitsOnceTracksTheEnemyAndFinishesAtAuthoritativeHull()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session=CreateNavalVfxFixture(controller,"card_chain_shot");
            session.Battle.EnemyBlock=3;
            CoreBattleCard card=session.GetBattleCard(session.Battle.Hand[0]);
            int hull=session.Battle.EnemyHull,ammo=session.Cargo.Cannonballs,powder=session.Cargo.Powder,energy=session.Battle.Energy;
            int energyCost=session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
            int damage=Mathf.Max(0,session.GetBattleCardDamage(card,NavalBattleStage.Naval)-session.Battle.EnemyBlock);
            Assert.That(damage,Is.GreaterThan(0));
            RenderResponsiveBattle(controller);yield return WaitResponsiveBattle(controller);
            RectTransform stage=RequiredResponsive<RectTransform>("Battle Card");
            int retainedStage=stage.GetInstanceID();
            Vector2 target=stage.InverseTransformPoint(RequiredResponsive<RectTransform>("Enemy Ship Art").position);
            Button button=RequiredResponsive<Button>("Battle Card 0");
            Assert.That(button.interactable,Is.True,session.CardUnavailableReason(0));
            button.onClick.Invoke();button.onClick.Invoke();
            var ids=new HashSet<int>();var progress=new List<float>();
            bool capturedContact=false,sawIntermediateHull=false;
            float deadline=Time.realtimeSinceStartup+12f;
            while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
            {
                foreach(NavalCombatFxGraphic fx in stage.GetComponentsInChildren<NavalCombatFxGraphic>())
                {
                    ids.Add(fx.GetInstanceID());
                    Assert.That(fx.Role,Is.EqualTo(NavalVfxRole.Cannon));
                    Assert.That(fx.Outcome,Is.EqualTo(BattleSchoolAttackOutcome.Hit));
                    Assert.That(fx.raycastTarget,Is.False);
                    Assert.That(Vector2.Distance(fx.To,target),Is.LessThan(20f),"The shot must target the retained enemy vessel.");
                    if(progress.Count>0)Assert.That(fx.Progress,Is.GreaterThanOrEqualTo(progress[progress.Count-1]));
                    progress.Add(fx.Progress);
                    if(!capturedContact&&fx.Progress>=fx.ContactAt&&fx.Progress<.85f)
                    {
                        NavalBattleVisualCapture.Capture(controller,"NavalVfx_Cannon_Contact.png","真实炮击：新特效在敌舰处命中，结算只执行一次");
                        capturedContact=true;
                    }
                }
                BattleDefenseHud hud=RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track");
                sawIntermediateHull|=hud.DisplayedHull<hull-.01f&&hud.DisplayedHull>hull-damage+.01f;
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            yield return null;
            Assert.That(ids.Count,Is.EqualTo(1),"Duplicate UI invocation must not replay a cast.");
            Assert.That(progress.Count,Is.GreaterThan(2),"The effect needs a continuous visible timeline.");
            Assert.That(progress.Max()-progress.Min(),Is.GreaterThan(.4f));
            Assert.That(capturedContact,Is.True);
            Assert.That(sawIntermediateHull,Is.True,"The HUD must interpolate the committed hull change.");
            Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
            Assert.That(session.Battle.EnemyHull,Is.EqualTo(hull-damage));
            Assert.That(session.Cargo.Cannonballs,Is.EqualTo(ammo-card.CannonballCost));
            Assert.That(session.Cargo.Powder,Is.EqualTo(powder-card.PowderCost));
            Assert.That(session.Battle.Energy,Is.EqualTo(energy-energyCost));
            Assert.That(RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track").DisplayedHull,Is.EqualTo(session.Battle.EnemyHull).Within(.01f));
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(),Is.EqualTo(retainedStage));
            Assert.That(stage.GetComponentsInChildren<NavalCombatFxGraphic>(true),Is.Empty);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable,Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalVfx_AfflictionAndGuardUseTheirOwnTargetsWithoutInventingDamage()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach(string cardId in new[]{"card_school_fire_n03","card_school_arcane_n06"})
            {
                CoreVoyageSession session=CreateNavalVfxFixture(controller,cardId);
                CoreBattleCard card=session.GetBattleCard(session.Battle.Hand[0]);
                NavalCardVfxPlan plan=NavalCardVfxPlan.For(card);
                int hull=session.Battle.EnemyHull,guard=session.Battle.PlayerBlock;
                RenderResponsiveBattle(controller);yield return WaitResponsiveBattle(controller);
                RectTransform stage=RequiredResponsive<RectTransform>("Battle Card");
                Vector2 target=stage.InverseTransformPoint(RequiredResponsive<RectTransform>(plan.TargetsEnemy?"Enemy Ship Art":"Player Ship Art").position);
                Button button=RequiredResponsive<Button>("Battle Card 0");
                Assert.That(button.interactable,Is.True,session.CardUnavailableReason(0));button.onClick.Invoke();
                bool seen=false,captured=false;float deadline=Time.realtimeSinceStartup+12f;
                while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
                {
                    foreach(NavalCombatFxGraphic fx in stage.GetComponentsInChildren<NavalCombatFxGraphic>())
                    {
                        seen=true;Assert.That(fx.Role,Is.EqualTo(plan.Role));Assert.That(fx.raycastTarget,Is.False);
                        Assert.That(Vector2.Distance(fx.To,target),Is.LessThan(20f));
                        if(!captured&&fx.Progress>.35f&&fx.Progress<.85f)
                        {
                            NavalBattleVisualCapture.Capture(controller,"NavalVfx_"+plan.Role+"_Contact.png",card.Name+"：按真实效果在正确持有者处播放");captured=true;
                        }
                    }
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying,Is.False);yield return null;
                Assert.That(seen,Is.True,cardId);Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
                Assert.That(session.Battle.EnemyHull,Is.EqualTo(hull),"Status and guard casts cannot invent direct damage.");
                if(plan.Role==NavalVfxRole.Affliction)Assert.That(session.Battle.NavalExpansion.OilPlayerEndsRemaining,Is.GreaterThan(0));
                else Assert.That(session.Battle.PlayerBlock,Is.GreaterThan(guard));
                Assert.That(stage.GetComponentsInChildren<NavalCombatFxGraphic>(true),Is.Empty);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalVfx_DisablingDuringCommittedCastClearsEffectsAndDoesNotReplay()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session=CreateNavalVfxFixture(controller,"card_chain_shot");
            RenderResponsiveBattle(controller);yield return WaitResponsiveBattle(controller);
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+6f;
            while(!Object.FindObjectsByType<NavalCombatFxGraphic>(FindObjectsSortMode.None).Any(fx=>fx.IsPlaying)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<NavalCombatFxGraphic>(FindObjectsSortMode.None),Is.Not.Empty);
            int committedHull=session.Battle.EnemyHull,committedAmmo=session.Cargo.Cannonballs;
            controller.gameObject.SetActive(false);yield return null;
            Assert.That(Object.FindObjectsByType<NavalCombatFxGraphic>(FindObjectsSortMode.None),Is.Empty);
            controller.gameObject.SetActive(true);yield return null;yield return null;
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
            Assert.That(session.Battle.EnemyHull,Is.EqualTo(committedHull));
            Assert.That(session.Cargo.Cannonballs,Is.EqualTo(committedAmmo));
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetComponentsInChildren<NavalCombatFxGraphic>(true),Is.Empty);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable,Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalVfx_DrainAttackHealsTheDisplayedHullOnlyAfterItsContact()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session=CreateNavalVfxFixture(controller,"card_school_blood_b04");
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session,Mathf.Max(1,session.Hull-20));
            int before=session.Hull;
            RenderResponsiveBattle(controller);yield return WaitResponsiveBattle(controller);
            BattleDefenseHud hud=RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track");
            Assert.That(hud.DisplayedHull,Is.EqualTo(before));
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            bool sawBeforeContact=false,sawContact=false;
            float deadline=Time.realtimeSinceStartup+12f;
            while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
            {
                foreach(NavalCombatFxGraphic fx in RequiredResponsive<RectTransform>("Battle Card").GetComponentsInChildren<NavalCombatFxGraphic>())
                {
                    if(fx.Role!=NavalVfxRole.Strike)continue;
                    if(fx.Progress>.02f&&fx.Progress<fx.ContactAt-.04f)
                    {
                        sawBeforeContact=true;
                        Assert.That(session.Hull,Is.GreaterThan(before),"The authoritative card has committed before its feedback.");
                        Assert.That(hud.DisplayedHull,Is.EqualTo(before).Within(.01f),"Drain recovery must follow the visible hit.");
                    }
                    if(fx.Progress>=fx.ContactAt)sawContact=true;
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);yield return null;
            Assert.That(sawBeforeContact,Is.True);Assert.That(sawContact,Is.True);
            Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
            Assert.That(session.Hull,Is.GreaterThan(before));
            Assert.That(hud.DisplayedHull,Is.EqualTo(session.Hull).Within(.01f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalVfx_SelectedSelfExplosionConsumesGuardAtContactWithoutMovingToTheEnemy()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session=CreateNavalVfxFixture(controller,"card_school_fire_n06");
            SetNavalFieldTestPose(session,true,0,0);SetNavalFieldTestPose(session,false,3,0);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire,true,0,0,false,0,0),Is.True);
            session.Battle.PlayerBlock=1000;
            int hull=session.Hull,enemyHull=session.Battle.EnemyHull,guard=session.Battle.PlayerBlock;
            RenderResponsiveBattle(controller);yield return WaitResponsiveBattle(controller);
            BattleDefenseHud hud=RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track");
            NavalTacticalBoardView board=RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board");
            Vector2 selectedCenter=board.WorldToRoot(new VoyagePoint(50f,50f));
            Vector2 enemy=board.WorldToRoot(session.Battle.EnemyPositionMeters);
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();yield return null;
            Assert.That(controller.IsNavalCardChoiceOpen,Is.True);
            Object.FindFirstObjectByType<NavalCardChoiceOverlay>().SelectCell(0,0);
            Button confirm=RequiredResponsive<Button>("Confirm Naval Card Choice");
            Assert.That(confirm.interactable,Is.True);confirm.onClick.Invoke();
            bool seen=false,sawBeforeContact=false,captured=false;
            float deadline=Time.realtimeSinceStartup+12f;
            while(controller.IsBattleAnimationPlaying&&Time.realtimeSinceStartup<deadline)
            {
                foreach(NavalCombatFxGraphic fx in RequiredResponsive<RectTransform>("Battle Card").GetComponentsInChildren<NavalCombatFxGraphic>())
                {
                    if(fx.Role!=NavalVfxRole.Area)continue;
                    seen=true;Assert.That(fx.HasExplicitAreaTargets,Is.True);Assert.That(fx.AreaTargetCount,Is.EqualTo(4));
                    Assert.That(Vector2.Distance(fx.To,selectedCenter),Is.LessThan(1f));
                    Assert.That(Vector2.Distance(fx.To,enemy),Is.GreaterThan(board.CellSizePixels));
                    if(fx.Progress>.02f&&fx.Progress<fx.ContactAt-.04f)
                    {
                        sawBeforeContact=true;
                        Assert.That(hud.DisplayedGuard,Is.EqualTo(guard).Within(.01f));
                    }
                    if(!captured&&fx.Progress>fx.ContactAt+.04f&&fx.Progress<.8f)
                    {
                        NavalBattleVisualCapture.Capture(controller,"NavalVfx_SelectedSelfExplosion.png","真实选区引爆：四格特效覆盖我舰所在区域，范围外敌舰不受影响");captured=true;
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);yield return null;
            Assert.That(seen,Is.True);Assert.That(sawBeforeContact,Is.True);
            Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
            Assert.That(session.Battle.PlayerBlock,Is.LessThan(guard));
            Assert.That(session.Hull,Is.EqualTo(hull));Assert.That(session.Battle.EnemyHull,Is.EqualTo(enemyHull));
            Assert.That(hud.DisplayedGuard,Is.EqualTo(session.Battle.PlayerBlock).Within(.01f));
            Assert.That(session.Battle.NavalFields.Cells.Count,Is.EqualTo(5));
            LogAssert.NoUnexpectedReceived();
        }

        private static CoreVoyageSession CreateNavalVfxFixture(CoreGameplayDemoController controller,string cardId)
        {
            CoreVoyageSession session=CreateResponsiveBattle(controller,NavalBattleStage.Naval,pirateLevel:10);
            SetResponsiveHand(session,cardId,"card_brace");SetResponsiveDrawPile(session);
            ConfigureCloseNavalTestPose(session.Battle,false);
            session.Battle.EnemyHull=session.Battle.EnemyMaxHull=5000;
            session.Battle.Energy=session.Battle.MaxEnergy=20;
            session.Cargo.Powder=session.Cargo.Cannonballs=session.Cargo.Wood=20;
            session.Battle.NavalFields.Player.Mana=3;
            return session;
        }
    }
}
