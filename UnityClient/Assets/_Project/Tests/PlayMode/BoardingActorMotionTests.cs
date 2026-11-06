using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
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
        public IEnumerator BoardingActors_AtSeaSpecialistsStayVisibleAfterSelectionChangesAndCaptainStandsAlone()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = PrepareActorCrewBattle(controller, "fire_01", out CrewVariantDefinition[] crew);
            yield return WaitResponsiveBattle(controller);
            AssertActorCrewIdentity(session, crew);
            var pose = SnapshotActorAnchors();
            for (int i = 0; i < 12; i++) { yield return new WaitForSecondsRealtime(.04f); AssertActorAnchors(pose); }
            string rules = SchoolCastFingerprint(session);
            var selected = (List<string>)RamComicField("selectedDepartureSpecialCrew").GetValue(controller);
            selected.Clear(); selected.Add(CrewNarrativeCatalog.ForPortAndProfession("白贝港", 3).Id);
            RenderResponsiveBattle(controller); yield return WaitResponsiveBattle(controller);
            AssertActorCrewIdentity(session, crew, false);
            Assert.That(SchoolCastFingerprint(session), Is.EqualTo(rules));
            selected.Clear();
            CoreVoyageSession ordinary = PrepareSchoolCastBattle(controller, "fire_01");
            yield return WaitResponsiveBattle(controller);
            Assert.That(PlayerActors().Select(actor => actor.Identity), Is.EquivalentTo(new[] { "captain" }));
            Assert.That(GameObject.Find("Player Ship Art Sailor 0"), Is.Null);
            AssertActorAnchors(SnapshotActorAnchors());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingActors_SixSchoolsAnimateTheActualCasterAndHitTarget()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string[] cards = { "fire_01", "arcane_01", "curse_01", "tide_04", "thunder_03", "blood_01" };
            string[] schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
            for (int i = 0; i < cards.Length; i++)
            {
                CoreVoyageSession session = PrepareActorCrewBattle(controller, cards[i], out CrewVariantDefinition[] crew);
                yield return WaitResponsiveBattle(controller);
                AssertActorCrewIdentity(session, crew);
                ActorAnchor[] pose = SnapshotActorAnchors();
                int health = session.Battle.EnemyBoardingHealth;
                var stale = RequiredResponsive<Button>("Battle Card 0").onClick;
                Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
                stale.Invoke(); stale.Invoke();
                bool sawAttack = false, sawCast = false, advanced = false, captured = false, performerMoved = false, enemyHit = false;
                float first = -1f;
                yield return ObserveActorAction(controller, pose, () =>
                {
                    performerMoved |= PlayerActors().Any(actor => actor.Identity == "captain" && actor.Action != BoardingActorAction.Idle);
                    enemyHit |= EnemyActors().Any(actor => actor.Action == BoardingActorAction.Hit);
                    sawCast |= ActiveSchoolCasts().Any(c => c.Kind == SecondBatchKind(schools[i]));
                    foreach (BattleSchoolAttackGraphic attack in ActiveSecondBatchAttacks())
                    {
                        Assert.That(attack.Kind, Is.EqualTo(SecondBatchKind(schools[i])));
                        sawAttack = true;
                        if (first < 0f) first = attack.Progress;
                        advanced |= attack.Progress > first + .08f;
                        if (!captured && (i == 0 || i == 4) && attack.Progress >= .42f)
                        {
                            NavalBattleVisualCapture.Capture(controller, "Actors_SpecialCrew_" + schools[i] + ".png",
                                "接舷参战船员与敌人 · " + schools[i] + "系出牌特效播放");
                            captured = true;
                        }
                    }
                });
                Assert.That(performerMoved, Is.True, schools[i] + " caster");
                Assert.That(enemyHit, Is.True, schools[i] + " contact");
                Assert.That(sawAttack && sawCast && advanced, Is.True, schools[i] + " must retain animated school VFX.");
                if (i == 0 || i == 4) Assert.That(captured, Is.True);
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
                Assert.That(session.Battle.EnemyBoardingHealth, Is.LessThan(health));
                string committed = SchoolCastFingerprint(session);
                stale.Invoke(); yield return null;
                Assert.That(SchoolCastFingerprint(session), Is.EqualTo(committed));
                AssertActorAnchors(pose); AssertResponsiveHandMatchesCore(session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingActors_SupportAndChargeUseTheCasterWithoutEnemyHit()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string[] cards = { "curse_02", "tide_01", "blood_09", "arcane_11" };
            BoardingSchoolCastRole[] roles = { BoardingSchoolCastRole.Affliction, BoardingSchoolCastRole.Guard, BoardingSchoolCastRole.Heal, BoardingSchoolCastRole.Charge };
            for (int i = 0; i < cards.Length; i++)
            {
                CoreVoyageSession session = PrepareActorCrewBattle(controller, cards[i], out _);
                session.Battle.PlayerBoardingHealth = 150; RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                var pose = SnapshotActorAnchors(); bool saw = false;
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                yield return ObserveActorAction(controller, pose, () => saw |= ActiveSchoolCasts().Any(c => c.Role == roles[i]));
                Assert.That(saw, Is.True, cards[i]);
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
                Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(200));
                AssertResponsiveHandMatchesCore(session);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingActors_MeleeAndDelayedDamageAnimateTheirSourceAndTarget()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = PrepareActorCrewBattle(controller, "basic_13", out _);
            SetResponsiveHand(session, "card_crew_thrust", "card_boarding_basic_13", "card_crew_guard");
            RenderResponsiveBattle(controller); yield return WaitResponsiveBattle(controller);
            var pose = SnapshotActorAnchors(); int health = session.Battle.EnemyBoardingHealth;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveActorAction(controller, pose);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.LessThan(health));
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveActorAction(controller, pose);
            Assert.That(session.Battle.Boarding.DelayedDamage, Has.Count.EqualTo(1));
            int scheduled = session.Battle.Boarding.DelayedDamage[0].Amount;
            health = session.Battle.EnemyBoardingHealth;
            int playerHealth = session.Battle.PlayerBoardingHealth;
            long sequence = session.Battle.Boarding.ResolvedAttackSequence;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return ObserveActorAction(controller, pose);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health - scheduled));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.LessThan(playerHealth), "The enemy must actually deal damage to the player formation.");
            Assert.That(session.Battle.Boarding.DelayedDamage, Is.Empty);
            Assert.That(session.Battle.Boarding.ResolvedAttackSequence, Is.EqualTo(sequence + 1));
            AssertResponsiveHandMatchesCore(session); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingActors_PauseAndDisableClearActionsAndPreserveCommittedRules()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = PrepareActorCrewBattle(controller, "arcane_11", out CrewVariantDefinition[] crew);
            yield return WaitResponsiveBattle(controller);
            var pose = SnapshotActorAnchors();
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 6f;
            while (ActiveSchoolCasts().Length == 0 && Time.realtimeSinceStartup < deadline) { AssertActorAnchors(pose); yield return null; }
            Assert.That(ActiveSchoolCasts(), Has.Length.EqualTo(1));
            string committed = SchoolCastFingerprint(session);
            var pause = typeof(CoreGameplayDemoController).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
            pause.Invoke(controller, new object[] { true }); yield return null;
            Assert.That(ActiveSchoolCasts(), Is.Empty); AssertActorAnchors(pose);
            pause.Invoke(controller, new object[] { false }); yield return null; yield return null;
            AssertActorCrewIdentity(session, crew); AssertActorAnchors(pose);
            controller.enabled = false; yield return null; controller.enabled = true; yield return null; yield return null;
            AssertActorCrewIdentity(session, crew); AssertActorAnchors(pose);
            Assert.That(SchoolCastFingerprint(session), Is.EqualTo(committed));
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingActors_VictoryAndDefeatShowTheLosingPartyDeathBeforeExit()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (bool wins in new[] { true, false })
            {
                CoreVoyageSession session = PrepareActorCrewBattle(controller, "fire_01", out _);
                if (wins) session.Battle.EnemyBoardingHealth = 1; else session.Battle.PlayerBoardingHealth = 1;
                RenderResponsiveBattle(controller); yield return WaitResponsiveBattle(controller);
                var pose = SnapshotActorAnchors();
                RequiredResponsive<Button>(wins ? "Battle Card 0" : "End Battle Turn").onClick.Invoke();
                BattleResolutionMotion resolution = null;
                yield return AwaitSecondBatchResolution(controller, m => resolution = m);
                Assert.That(resolution.Kind, Is.EqualTo(BattleResolutionKind.BoardingDefeat));
                foreach (float progress in new[] { .2f, .5f, .8f })
                {
                    AdvanceSecondBatchResolutionTo(resolution, progress); Canvas.ForceUpdateCanvases(); AssertActorAnchors(pose);
                    var defeated = wins ? EnemyActors() : PlayerActors();
                    Assert.That(defeated, Is.Not.Empty);
                    Assert.That(defeated.All(actor => actor.Action == BoardingActorAction.Death), Is.True);
                    if (progress == .5f)
                        NavalBattleVisualCapture.Capture(controller, "Actors_Defeat_" + (wins ? "enemy" : "player") + ".png", "真实胜负结算中的死亡动作");
                }
                string committed = SecondBatchOutcomeFingerprint(session);
                yield return WaitResponsiveBattle(controller);
                Assert.That(SecondBatchOutcomeFingerprint(session), Is.EqualTo(committed));
                Assert.That(GameObject.Find("Battle Card"), Is.Null);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingActors_SignatureCardsAnimateTheMatchingCrewMember()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = PrepareActorCrewBattle(controller, "basic_01", out CrewVariantDefinition[] crew);
                foreach (var member in crew)
                {
                    CoreBattleCard card = CoreSpecialCardCatalog.ForCrew(member.Id).Card;
                    session.Cargo.Medicine = 50;
                    session.Battle.PlayerBoardingHealth = 120;
                    SetResponsiveHand(session, card.Id, "card_crew_guard");
                    RenderResponsiveBattle(controller);
                    yield return WaitResponsiveBattle(controller);
                    Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
                    var action = BoardingCardActorPresentation.ForCard(card);
                    var recordedFrames = new HashSet<int>();
                    var performer = PlayerActors().Single(a => a.Identity == member.Id);
                    var poses = SnapshotActorAnchors();
                    var play = RequiredResponsive<Button>("Battle Card 0").onClick;
                    int before = session.Battle.CardsPlayedThisTurn;
                    play.Invoke(); play.Invoke();
                    yield return ObserveActorAction(controller, poses, () =>
                    {
                        if (performer.Action != action) return;
                        int sample = Mathf.Min(5, Mathf.FloorToInt(performer.ActionProgress * 6));
                        if (recordedFrames.Add(sample) && sample >= 1 && sample <= 4)
                            NavalBattleVisualCapture.Capture(controller, "Actors_Card_" + member.Id + "_" + sample + ".png", member.DisplayName + " · " + card.Name);
                    });
                    Assert.That(recordedFrames.Count, Is.GreaterThanOrEqualTo(2), member.DisplayName);
                    Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(before + 1));
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static CoreVoyageSession PrepareActorCrewBattle(CoreGameplayDemoController controller, string key, out CrewVariantDefinition[] crew)
        {
            crew = new[] { "crew_salt_crown_diver_isha", "crew_white_shell_doctor_mirei" }
                .Select(CrewNarrativeCatalog.FindById).ToArray();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, crewIds: crew.Select(c => c.Id).ToArray(), pirateLevel: 10);
            var selected = (List<string>)RamComicField("selectedDepartureSpecialCrew").GetValue(controller); selected.Clear();
            foreach (var member in crew)
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(member.Id), 1);
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(member.ProfessionIndex), 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(member.ProfessionIndex), member.Id);
                PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(member.Id), session.CurrentPort);
                selected.Add(member.Id);
            }
            typeof(CoreGameplayDemoController).GetMethod("PersistCrewAtSea", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            ConfigureCloseNavalTestPose(session.Battle, false); Assert.That(session.TryBoard(), Is.True);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 200;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 200;
            session.Battle.EnemyBlock = session.Battle.PlayerBlock = 0; session.Battle.Energy = session.Battle.MaxEnergy = 20;
            session.Cargo.Powder = session.Cargo.Medicine = 30;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool("neutral"));
            session.Battle.PhaseTurn = 1;
            SetResponsiveHand(session, "card_boarding_" + key, "card_crew_guard");
            SetResponsiveDrawPile(session, "card_crew_guard", "card_crew_guard", "card_crew_guard");
            RenderResponsiveBattle(controller); return session;
        }

        private static BoardingActorGraphic[] PlayerActors() =>
            RequiredResponsive<RectTransform>("Player Ship Art").GetComponentsInChildren<BoardingActorGraphic>();
        private static BoardingActorGraphic[] EnemyActors() =>
            RequiredResponsive<RectTransform>("Enemy Ship Art").GetComponentsInChildren<BoardingActorGraphic>();

        private static void AssertActorCrewIdentity(CoreVoyageSession session, CrewVariantDefinition[] crew, bool ordered = true)
        {
            var actors = PlayerActors();
            var captain = actors.Single(actor => actor.Identity == "captain");
            Assert.That(captain.GetComponent<RectTransform>().anchoredPosition.x, Is.Zero.Within(.01f));
            Assert.That(captain.GetComponent<RawImage>().texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Actions/captain")));
            var eligible = crew.Where(member => CoreSpecialCardCatalog.ForCrew(member.Id)?.Card.IsAllowedInStage(NavalBattleStage.Boarding) == true)
                .Take(2).ToArray();
            var active = actors.Where(actor => actor != captain).OrderBy(a => a.GetComponent<RectTransform>().anchoredPosition.x).ToArray();
            Assert.That(active.Select(actor => actor.Identity), Is.EquivalentTo(eligible.Select(member => member.Id)));
            if (ordered) CollectionAssert.AreEqual(eligible.Select(member => member.Id).ToArray(), active.Select(actor => actor.Identity).ToArray());
            foreach (var actor in active)
            {
                Assert.That(actor.CardId, Is.EqualTo(CoreSpecialCardCatalog.ForCrew(actor.Identity).Card.Id));
                Assert.That(session.GetCrewBattleCards(NavalBattleStage.Boarding).Select(index => CoreVoyageSession.CardLibrary[index].RequiredCrew),
                    Does.Contain(actor.Identity));
                Assert.That(actor.GetComponent<RawImage>().texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Actions/" + actor.Identity)));
                Assert.That(actor.UsesActionAtlas, Is.True);
            }
        }

        private sealed class ActorAnchor
        {
            public string Name;
            public Vector3 Position, PartyPosition;
        }
        private static ActorAnchor[] SnapshotActorAnchors()
        {
            return PlayerActors().Concat(EnemyActors()).Select(actor => new ActorAnchor {
                Name = actor.name, Position = actor.transform.localPosition, PartyPosition = actor.transform.parent.localPosition
            }).ToArray();
        }
        private static void AssertActorAnchors(ActorAnchor[] poses)
        {
            foreach (var pose in poses)
            {
                var actor = RequiredResponsive<BoardingActorGraphic>(pose.Name);
                Assert.That(Vector3.Distance(actor.transform.localPosition, pose.Position), Is.LessThan(.02f), pose.Name);
                Assert.That(Vector3.Distance(actor.transform.parent.localPosition, pose.PartyPosition), Is.LessThan(.02f));
                Assert.That(actor.GetComponent<RawImage>().raycastTarget, Is.False);
            }
        }
        private static IEnumerator ObserveActorAction(CoreGameplayDemoController controller, ActorAnchor[] pose, Action observe = null)
        {
            float deadline = Time.realtimeSinceStartup + 15f; int samples = 0;
            do
            {
                AssertActorAnchors(pose); observe?.Invoke(); samples++;
                if (!controller.IsBattleAnimationPlaying) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(samples, Is.GreaterThan(2), "Observe the entire animated effect, not just its start/end.");
            yield return null; AssertActorAnchors(pose);
        }
    }
}
