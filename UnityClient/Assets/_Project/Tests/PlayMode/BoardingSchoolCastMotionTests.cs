using System;
using System.Collections;
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
        public IEnumerator BoardingSchoolCast_SixSchoolsPlayFromRealCardsWithoutDoubleCommit()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string[] cards={"fire_01","arcane_01","curse_01","tide_04","thunder_03","blood_01"};
            string[] schools={"fire","arcane","curse","tide","thunder","blood"};
            for(int i=0;i<cards.Length;i++)
            {
                CoreVoyageSession session=PrepareSchoolCastBattle(controller,cards[i]);
                yield return null;
                int energy=session.Battle.Energy,cost=session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                var stale=RequiredResponsive<Button>("Battle Card 0").onClick;
                Assert.That(session.CanPlayCard(0),Is.True,session.CardUnavailableReason(0));
                stale.Invoke();stale.Invoke();
                yield return ObserveSchoolCast(controller,(BattleSchoolAttackKind)Enum.Parse(typeof(BattleSchoolAttackKind),schools[i],true),
                    BoardingSchoolCastRole.Attack,"Cast_Attack_"+schools[i]+".png",true);
                Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
                Assert.That(session.Battle.Energy,Is.EqualTo(energy-cost));
                string committed=SchoolCastFingerprint(session);
                stale.Invoke();yield return null;
                Assert.That(SchoolCastFingerprint(session),Is.EqualTo(committed));
                AssertResponsiveHandMatchesCore(session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingSchoolCast_SetupGuardHealAndChargeUseRolesWithoutFakeHits()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string[] cards={"fire_03","curse_02","arcane_03","tide_01","blood_09","arcane_11"};
            BattleSchoolAttackKind[] schools={BattleSchoolAttackKind.Fire,BattleSchoolAttackKind.Curse,BattleSchoolAttackKind.Arcane,
                BattleSchoolAttackKind.Tide,BattleSchoolAttackKind.Blood,BattleSchoolAttackKind.Arcane};
            BoardingSchoolCastRole[] roles={BoardingSchoolCastRole.Affliction,BoardingSchoolCastRole.Affliction,BoardingSchoolCastRole.Empower,
                BoardingSchoolCastRole.Guard,BoardingSchoolCastRole.Heal,BoardingSchoolCastRole.Charge};
            for(int i=0;i<cards.Length;i++)
            {
                CoreVoyageSession session=PrepareSchoolCastBattle(controller,cards[i]);
                session.Battle.PlayerBoardingHealth=150;
                RenderResponsiveBattle(controller);
                yield return null;
                int health=session.Battle.EnemyBoardingHealth;
                Assert.That(session.CanPlayCard(0),Is.True,cards[i]+": "+session.CardUnavailableReason(0));
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                yield return ObserveSchoolCast(controller,schools[i],roles[i],"Cast_Setup_"+cards[i]+".png",false);
                Assert.That(session.Battle.CardsPlayedThisTurn,Is.EqualTo(1));
                Assert.That(session.Battle.EnemyBoardingHealth,Is.EqualTo(health),"Setup/charge must not masquerade as damage.");
                AssertResponsiveHandMatchesCore(session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingSchoolCast_DisablingControllerCancelsCastAndPreservesCommittedState()
        {
            yield return LoadResponsiveBattleScene();
            var controller=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session=PrepareSchoolCastBattle(controller,"arcane_11");
            yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+4f;
            while(ActiveSchoolCasts().Length==0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(ActiveSchoolCasts().Length,Is.EqualTo(1));
            string committed=SchoolCastFingerprint(session);
            controller.enabled=false;
            yield return null;
            Assert.That(ActiveSchoolCasts(),Is.Empty);
            controller.enabled=true;
            yield return null;yield return null;
            Assert.That(SchoolCastFingerprint(session),Is.EqualTo(committed));
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable,Is.True);
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        private static CoreVoyageSession PrepareSchoolCastBattle(CoreGameplayDemoController controller,string key)
        {
            CoreVoyageSession session=CreateSecondBatchBattle(controller,true);
            session.Battle.Energy=session.Battle.MaxEnergy=20;
            session.Cargo.Powder=session.Cargo.Medicine=30;
            SetResponsiveHand(session,"card_boarding_"+key,"card_crew_guard");
            SetResponsiveDrawPile(session,"card_crew_guard","card_crew_guard");
            RenderResponsiveBattle(controller);
            return session;
        }
        private static BoardingSchoolCastGraphic[] ActiveSchoolCasts()=>Object.FindObjectsByType<BoardingSchoolCastGraphic>(FindObjectsSortMode.None)
            .Where(c=>c.IsPlaying&&c.gameObject.activeInHierarchy).ToArray();
        private static string SchoolCastFingerprint(CoreVoyageSession session)=>session.Battle.CardsPlayedThisTurn+"|"+session.Battle.Energy+"|"+
            session.Battle.PlayerBoardingHealth+"|"+session.Battle.EnemyBoardingHealth+"|"+session.Battle.PlayerBlock+"|"+session.Battle.EnemyBlock+"|"+
            session.Battle.Boarding.ResolvedAttackSequence+"|"+session.Battle.Boarding.DelayedDamage.Count+"|"+session.Cargo.Powder+"|"+session.Cargo.Medicine;
        private static IEnumerator ObserveSchoolCast(CoreGameplayDemoController controller,BattleSchoolAttackKind school,BoardingSchoolCastRole role,string file,bool attackExpected)
        {
            bool saw=false,captured=false; int id=0; string committed=null; float deadline=Time.realtimeSinceStartup+10f;
            do
            {
                foreach(BoardingSchoolCastGraphic cast in ActiveSchoolCasts())
                {
                    if(!saw){saw=true;id=cast.GetInstanceID();committed=SchoolCastFingerprint(controller.Session);}
                    Assert.That(cast.GetInstanceID(),Is.EqualTo(id),"Only one cast belongs to a committed card.");
                    Assert.That(cast.Kind,Is.EqualTo(school));Assert.That(cast.Role,Is.EqualTo(role));
                    Assert.That(cast.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
                    if(!captured&&cast.Progress>=.24f)
                    {
                        NavalBattleVisualCapture.Capture(controller,file,"真实接舷出牌 · "+school+" · "+role);
                        captured=true;
                    }
                }
                if(!attackExpected)Assert.That(ActiveSecondBatchAttacks(),Is.Empty,"Setup card may cast but has no attack hit.");
                if(!controller.IsBattleAnimationPlaying)break;
                yield return null;
            }while(Time.realtimeSinceStartup<deadline);
            Assert.That(saw,Is.True,"A successful school card must have its cast identity.");
            Assert.That(captured,Is.True);
            Assert.That(controller.IsBattleAnimationPlaying,Is.False);
            Assert.That(ActiveSchoolCasts(),Is.Empty);
            Assert.That(SchoolCastFingerprint(controller.Session),Is.EqualTo(committed),"Visual playback must not change the already committed rules result.");
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable,Is.True);
            yield return null;
        }
    }
}