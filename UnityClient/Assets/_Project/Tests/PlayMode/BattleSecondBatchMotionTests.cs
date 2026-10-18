using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private static readonly string[] SecondBatchSchools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };

        [UnityTest]
        public IEnumerator BattleSecondBatch_SixBoardingPlayerSchoolsUseCommittedDamageAndRetainTheHand()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string[] cards = { "fire_01", "arcane_01", "curse_01", "tide_04", "thunder_03", "blood_01" };
            for (int index = 0; index < cards.Length; index++)
            {
                CoreVoyageSession session = CreateSecondBatchBattle(controller, true);
                CoreBattleState battle = session.Battle;
                SetResponsiveHand(session, "card_boarding_" + cards[index], "card_crew_guard");
                SetResponsiveDrawPile(session, "card_crew_guard", "card_crew_guard");
                battle.Energy = battle.MaxEnergy = 20;
                RenderResponsiveBattle(controller);
                yield return null;
                int stageId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
                int retainedCard = RequiredResponsive<Button>("Battle Card 1").GetInstanceID();
                int health = battle.EnemyBoardingHealth, energy = battle.Energy;
                int cost = session.GetBattleCardEnergyCost(battle.Hand[0]);
                UnityEvent stale = RequiredResponsive<Button>("Battle Card 0").onClick;
                Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
                stale.Invoke(); stale.Invoke();
                yield return ObserveSecondBatchAttacks(controller, SecondBatchKind(SecondBatchSchools[index]),
                    BattleSchoolAttackOutcome.Hit, () => 1,
                    "BattleSecondBatch_Player_Boarding_" + SecondBatchSchools[index] + ".png",
                    "真实接舷出牌：" + SecondBatchSchools[index] + "轨迹与命中纹样；伤害取自已提交结果",
                    attack => Assert.That(SecondBatchResult(attack), Is.EqualTo("-" + (health - battle.EnemyBoardingHealth))));
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(1));
                Assert.That(battle.EnemyBoardingHealth, Is.LessThan(health));
                Assert.That(battle.Energy, Is.EqualTo(energy - cost));
                Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(stageId));
                Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(retainedCard));
                AssertResponsiveHandMatchesCore(session);
                Assert.That(RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track").DisplayedHull,
                    Is.EqualTo(battle.EnemyBoardingHealth));
                int committedHealth = battle.EnemyBoardingHealth;
                stale.Invoke();
                yield return null;
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(1));
                Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(committedHealth));
                Assert.That(ActiveSecondBatchAttacks(), Is.Empty);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_NavalDamageSchoolsUseRealResourcesAndCurseSetupDoesNotFakeAnAttack()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            // Naval curse b03 places marks; it is deliberately not counted as a direct attack.
            foreach (string school in new[] { "fire", "arcane", "tide", "thunder", "blood" })
            {
                CoreVoyageSession session = CreateSecondBatchBattle(controller, false);
                CoreBattleState battle = session.Battle;
                SetResponsiveHand(session, "card_school_" + school + "_b03", "card_brace");
                SetResponsiveDrawPile(session);
                battle.Energy = battle.MaxEnergy = 20;
                typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(battle.Schools, battle.Schools.ManaLimit);
                typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(battle.Schools, 4);
                RenderResponsiveBattle(controller);
                yield return null;
                int health = battle.EnemyHull, energy = battle.Energy;
                int cost = session.GetBattleCardEnergyCost(battle.Hand[0]);
                Assert.That(session.CanPlayCard(0), Is.True, school + ": " + session.CardUnavailableReason(0));
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                yield return ObserveSecondBatchAttacks(controller, SecondBatchKind(school), BattleSchoolAttackOutcome.Hit,
                    () => 1, "BattleSecondBatch_Player_Naval_" + school + ".png",
                    "真实海战出牌：" + school + "攻击辨识；余额与生命条保持权威结果",
                    attack => Assert.That(SecondBatchResult(attack), Is.EqualTo("-" + (health - battle.EnemyHull))),
                    school == "fire" ? "BattleSecondBatch_Player_Naval_fire_Trajectory.png" : null);
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(1));
                Assert.That(battle.Energy, Is.EqualTo(energy - cost));
                Assert.That(battle.EnemyHull, Is.LessThan(health));
                Assert.That(RequiredResponsive<BattleResourceChangeTracker>("Resource Energy").LastDelta, Is.EqualTo(-cost));
                AssertResponsiveHandMatchesCore(session);
            }

            CoreVoyageSession curse = CreateSecondBatchBattle(controller, false);
            SetResponsiveHand(curse, "card_school_curse_b03", "card_brace");
            RenderResponsiveBattle(controller);
            yield return null;
            int before = curse.Battle.EnemyHull;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return AssertNoSecondBatchAttackDuringAction(controller);
            Assert.That(curse.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(curse.Battle.Schools.CurseMarks, Is.GreaterThan(0));
            Assert.That(curse.Battle.EnemyHull, Is.EqualTo(before));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_SixEnemySchoolTurnsPresentEveryActualAttackOnce()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (string school in SecondBatchSchools)
            {
                CoreVoyageSession session = CreateSecondBatchBattle(controller, true);
                CoreBattleState battle = session.Battle;
                typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool(school));
                battle.PhaseTurn = school == "arcane" || school == "tide" ? 2 : 1;
                SetResponsiveHand(session, "card_crew_guard", "card_crew_guard");
                SetResponsiveDrawPile(session, "card_crew_guard", "card_crew_guard", "card_crew_guard");
                RenderResponsiveBattle(controller);
                yield return null;
                int health = battle.PlayerBoardingHealth, turn = battle.Turn;
                Assert.That(session.GetBoardingEnemyPreview().Actions.Any(action => action.RawAttack > 0), Is.True, school);
                UnityEvent stale = RequiredResponsive<Button>("End Battle Turn").onClick;
                stale.Invoke();
                yield return ObserveSecondBatchAttacks(controller, SecondBatchKind(school), BattleSchoolAttackOutcome.Hit,
                    () => battle.BoardingEnemy.LastActions.Count(action => action.RawAttack > 0),
                    "BattleSecondBatch_Enemy_" + school + ".png", "真实敌方" + school + "行动：逐项读取 LastActions 的实际伤害",
                    attack => Assert.That(battle.BoardingEnemy.LastActions.Any(action => action.RawAttack > 0 &&
                        SecondBatchResult(attack) == "-" + action.HealthLost), Is.True));
                Assert.That(battle.Turn, Is.EqualTo(turn + 1));
                Assert.That(health - battle.PlayerBoardingHealth,
                    Is.EqualTo(battle.BoardingEnemy.LastActions.Sum(action => action.HealthLost)));
                AssertResponsiveHandMatchesCore(session);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
                Assert.That(ActiveSecondBatchAttacks(), Is.Empty);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_FullyGuardedHitsStayGuardedAndRejectedRangeCreatesNoAttack()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateSecondBatchBattle(controller, true);
            SetResponsiveHand(session, "card_boarding_arcane_01", "card_crew_guard");
            session.Battle.EnemyBlock = 50;
            RenderResponsiveBattle(controller);
            yield return null;
            int health = session.Battle.EnemyBoardingHealth, guard = session.Battle.EnemyBlock;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveSecondBatchAttacks(controller, BattleSchoolAttackKind.Arcane, BattleSchoolAttackOutcome.Guarded,
                () => 1, "BattleSecondBatch_Guarded.png", "真实攻击完全被格挡：显示格挡接触，不显示生命伤害",
                attack => Assert.That(SecondBatchResult(attack), Is.EqualTo("格挡")));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health));
            Assert.That(session.Battle.EnemyBlock, Is.LessThan(guard));

            session = CreateSecondBatchBattle(controller, false);
            SetResponsiveHand(session, "card_school_fire_b03", "card_brace");
            session.Battle.DistanceMeters = 1000f;
            RenderResponsiveBattle(controller);
            yield return null;
            int energy = session.Battle.Energy, powder = session.Cargo.Powder;
            Button rejected = RequiredResponsive<Button>("Battle Card 0");
            Assert.That(rejected.interactable, Is.False);
            rejected.onClick.Invoke();
            yield return null;
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(ActiveSecondBatchAttacks(), Is.Empty,
                "Current rules reject out-of-range attacks; they do not produce a fake missed projectile.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_NavalAndBoardingVictoriesAndDefeatsUseTheDisplayedActors()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (bool boarding in new[] { false, true })
                foreach (bool playerWins in new[] { true, false })
                {
                    CoreVoyageSession session = PrepareSecondBatchTerminal(controller, boarding, playerWins);
                    yield return null;
                    string side = playerWins ? "Enemy" : "Player";
                    RectTransform loser = RequiredResponsive<RectTransform>(side + " Ship Art");
                    Vector2 formationPosition = loser.anchoredPosition;
                    UnityEvent stale = RequiredResponsive<Button>(playerWins ? "Battle Card 0" : "End Battle Turn").onClick;
                    stale.Invoke();
                    BattleResolutionMotion resolution = null;
                    yield return AwaitSecondBatchResolution(controller, motion => resolution = motion);
                    Assert.That(resolution.Target, Is.SameAs(loser));
                    Assert.That(resolution.Kind, Is.EqualTo(boarding ? BattleResolutionKind.BoardingDefeat : BattleResolutionKind.NavalSinking));
                    Assert.That(session.Phase, Is.EqualTo(playerWins ? CoreDemoPhase.Sailing : CoreDemoPhase.Settlement),
                        "The terminal rule must already be committed while the old battlefield plays its result.");
                    if (!playerWins) Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                    string committed = SecondBatchOutcomeFingerprint(session);
                    string name = "BattleSecondBatch_Resolution_" + (boarding ? "Boarding_" : "Naval_") + (playerWins ? "Victory" : "Defeat");
                    AdvanceSecondBatchResolutionTo(resolution, .32f);
                    NavalBattleVisualCapture.Capture(controller, name + "_Start.png", "真实终局已提交：" + (boarding ? "接舷队败退" : "舰船开始沉没") + "，保留旧战场演出");
                    AdvanceSecondBatchResolutionTo(resolution, .72f);
                    if (boarding) Assert.That(loser.anchoredPosition, Is.EqualTo(formationPosition),
                        "Only defeated crew figures move; the formation container and its labels stay anchored.");
                    NavalBattleVisualCapture.Capture(controller, name + "_Finish.png", "真实" + (playerWins ? "胜利" : "失败") + "关键帧：" + (boarding ? "角色落下、旗帜低垂" : "舰身下沉、水花烟雾"));
                    yield return WaitResponsiveBattle(controller);
                    Assert.That(GameObject.Find("Battle Card"), Is.Null);
                    Assert.That(Object.FindObjectsByType<BattleResolutionMotion>(FindObjectsSortMode.None), Is.Empty);
                    Assert.That(GameObject.Find("Battle Resolution Banner"), Is.Null);
                    Assert.That(ActiveSecondBatchAttacks(), Is.Empty);
                    stale.Invoke();
                    yield return null;
                    Assert.That(SecondBatchOutcomeFingerprint(session), Is.EqualTo(committed), "A retired callback must not pay or settle twice.");
                    Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_DisablingBeforeAndAfterSchoolCommitNeverReplaysPaymentOrDamage()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateSecondBatchBattle(controller, true);
            SetResponsiveHand(session, "card_boarding_blood_01", "card_crew_guard");
            RenderResponsiveBattle(controller);
            yield return null;
            int health = session.Battle.EnemyBoardingHealth, playerHealth = session.Battle.PlayerBoardingHealth;
            int energy = session.Battle.Energy;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
            controller.enabled = false;
            yield return null;
            controller.enabled = true;
            yield return null; yield return null;
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(playerHealth));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(ActiveSecondBatchAttacks(), Is.Empty);

            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (ActiveSecondBatchAttacks().Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ActiveSecondBatchAttacks().Length, Is.EqualTo(1));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            int committedHealth = session.Battle.EnemyBoardingHealth, committedPlayer = session.Battle.PlayerBoardingHealth;
            int committedEnergy = session.Battle.Energy;
            controller.gameObject.SetActive(false);
            yield return null;
            controller.gameObject.SetActive(true);
            yield return null; yield return null;
            Assert.That(ActiveSecondBatchAttacks(), Is.Empty);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(committedHealth));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(committedPlayer));
            Assert.That(session.Battle.Energy, Is.EqualTo(committedEnergy));
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_DisablingTerminalPresentationRestoresActorsAndDoesNotSettleAgain()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (bool boarding in new[] { false, true })
            {
                CoreVoyageSession session = PrepareSecondBatchTerminal(controller, boarding, true);
                yield return null;
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                BattleResolutionMotion resolution = null;
                yield return AwaitSecondBatchResolution(controller, motion => resolution = motion);
                AdvanceSecondBatchResolutionTo(resolution, .55f);
                string committed = SecondBatchOutcomeFingerprint(session);
                controller.enabled = false;
                Assert.That(resolution == null || resolution.HasRestoredPose, Is.True,
                    "Controller suspension must restore any still-live actor immediately, before deferred destruction.");
                yield return null;
                controller.enabled = true;
                yield return null; yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(Object.FindObjectsByType<BattleResolutionMotion>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(GameObject.Find("Battle Resolution Banner"), Is.Null);
                Assert.That(GameObject.Find("Battle Card"), Is.Null);
                Assert.That(SecondBatchOutcomeFingerprint(session), Is.EqualTo(committed));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_QueuedThunderWaitsForItsRealTurnAndKeepsItsSourceSchool()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateSecondBatchBattle(controller, true);
            SetResponsiveHand(session, "card_boarding_thunder_05", "card_crew_guard");
            SetResponsiveDrawPile(session, "card_crew_guard", "card_crew_guard");
            RenderResponsiveBattle(controller);
            yield return null;
            CoreBattleState battle = session.Battle;
            int health = battle.EnemyBoardingHealth;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return AssertNoSecondBatchAttackDuringAction(controller);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(health));
            Assert.That(battle.Boarding.DelayedDamage.Count, Is.EqualTo(1));
            int scheduled = battle.Boarding.DelayedDamage[0].Amount;
            long sequence = battle.Boarding.ResolvedAttackSequence;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return ObserveSecondBatchAttacks(controller, BattleSchoolAttackKind.Thunder, BattleSchoolAttackOutcome.Hit,
                () => 1, "BattleSecondBatch_Delayed_Thunder.png", "真实迟雷在下个己方回合开始结算：保留雷霆来源，只提示一次",
                attack => Assert.That(SecondBatchResult(attack), Is.EqualTo("-" + scheduled)));
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(health - scheduled));
            Assert.That(battle.Boarding.DelayedDamage, Is.Empty);
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.EqualTo(sequence + 1));
            Assert.That(battle.Boarding.ResolvedAttacks.Last().SourceSchoolId, Is.EqualTo("thunder"));
            int resolved = battle.EnemyBoardingHealth;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return AssertNoSecondBatchAttackDuringAction(controller);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(resolved));
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.EqualTo(sequence + 1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSecondBatch_LethalFieldStopsTheRealRouteAndSinksOnlyAtItsCommittedCell()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetNavalFieldTestPose(session, false, 4, 0);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 1, 0, false, 0, 0), Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, 2);
            SetResponsiveHand(session, "card_brace");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            CoreBattleState battle = session.Battle;
            NavalTacticalBoardView board = RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board");
            yield return DragTacticalRoute(controller, new[] { new VoyagePoint(100f, 0f), new VoyagePoint(200f, 0f) });
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(2));
            Assert.That(session.NavalMovementFieldPreview.Sinks, Is.True);
            UnityEvent stale = RequiredResponsive<Button>("Commit Naval Movement").onClick;
            stale.Invoke(); stale.Invoke();
            BattleResolutionMotion resolution = null;
            yield return AwaitSecondBatchResolution(controller, motion => resolution = motion);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Hull, Is.Zero);
            Assert.That(battle.PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(board.DisplayedPlayerPositionMeters.X, Is.EqualTo(100f).Within(.01f),
                "The real move must stop at the lethal entry cell before the sinking presentation.");
            Assert.That(resolution.Kind, Is.EqualTo(BattleResolutionKind.NavalSinking));
            Assert.That(resolution.Target.name, Is.EqualTo("Player Ship Art"));
            string committed = SecondBatchOutcomeFingerprint(session);
            AdvanceSecondBatchResolutionTo(resolution, .65f);
            NavalBattleVisualCapture.Capture(controller, "BattleSecondBatch_LethalField_Sinking.png",
                "真实两格路线在第一格火场致死：显示舰位停在规则落点，只结算一次失败");
            yield return WaitResponsiveBattle(controller);
            stale.Invoke();
            yield return null;
            Assert.That(SecondBatchOutcomeFingerprint(session), Is.EqualTo(committed));
            Assert.That(battle.PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(GameObject.Find("Battle Card"), Is.Null);
            Assert.That(Object.FindObjectsByType<BattleResolutionMotion>(FindObjectsSortMode.None), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        private static CoreVoyageSession CreateSecondBatchBattle(CoreGameplayDemoController controller, bool boarding)
        {
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            session.Cargo.Powder = session.Cargo.Cannonballs = 30;
            session.Cargo.Medicine = 10;
            session.Battle.DistanceMeters = 250f;
            session.Battle.EnemyBlock = session.Battle.PlayerBlock = 0;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull;
            if (boarding)
            {
                ConfigureCloseNavalTestPose(session.Battle, false);
                Assert.That(session.TryBoard(), Is.True, "Fixture enters boarding through the actual connection rules.");
                session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 200;
                session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 200;
                session.Battle.EnemyBlock = session.Battle.PlayerBlock = 0;
                typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool("neutral"));
                session.Battle.PhaseTurn = 1;
            }
            return session;
        }

        private static CoreVoyageSession PrepareSecondBatchTerminal(CoreGameplayDemoController controller, bool boarding, bool playerWins)
        {
            CoreVoyageSession session = CreateSecondBatchBattle(controller, boarding);
            SetResponsiveHand(session, playerWins ? boarding ? "card_boarding_fire_01" : "card_school_fire_b03" :
                boarding ? "card_crew_guard" : "card_brace", boarding ? "card_crew_guard" : "card_brace");
            SetResponsiveDrawPile(session, boarding ? "card_crew_guard" : "card_brace");
            session.Battle.Energy = session.Battle.MaxEnergy = 20;
            session.Battle.EnemyBlock = session.Battle.PlayerBlock = 0;
            session.Battle.PhaseTurn = 1;
            if (playerWins)
            {
                if (boarding) session.Battle.EnemyBoardingHealth = 1;
                else session.Battle.EnemyHull = 1;
            }
            else if (boarding) session.Battle.PlayerBoardingHealth = 1;
            else typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            RenderResponsiveBattle(controller);
            return session;
        }

        private static BattleSchoolAttackKind SecondBatchKind(string school) =>
            (BattleSchoolAttackKind)Enum.Parse(typeof(BattleSchoolAttackKind), school, true);

        private static BattleSchoolAttackGraphic[] ActiveSecondBatchAttacks() =>
            Object.FindObjectsByType<BattleSchoolAttackGraphic>(FindObjectsSortMode.None)
                .Where(effect => effect.IsPlaying && effect.gameObject.activeInHierarchy).ToArray();

        private static string SecondBatchResult(BattleSchoolAttackGraphic attack) =>
            attack.transform.Find("School Attack Result").GetComponent<Text>().text;

        private static IEnumerator ObserveSecondBatchAttacks(CoreGameplayDemoController controller,
            BattleSchoolAttackKind kind, BattleSchoolAttackOutcome outcome, Func<int> expectedCount,
            string file, string caption, Action<BattleSchoolAttackGraphic> onFirst = null, string trajectoryFile = null)
        {
            var seen = new HashSet<int>();
            BattleSchoolAttackGraphic firstAttack = null;
            bool capturedTrajectory = false;
            bool capturedHit = false;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                foreach (BattleSchoolAttackGraphic attack in ActiveSecondBatchAttacks())
                {
                    if (!seen.Add(attack.GetInstanceID())) continue;
                    Assert.That(attack.Kind, Is.EqualTo(kind));
                    Assert.That(attack.Outcome, Is.EqualTo(outcome));
                    Assert.That(attack.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                    if (seen.Count == 1) firstAttack = attack;
                }
                if (firstAttack != null && firstAttack.IsPlaying)
                {
                    if (trajectoryFile != null && !capturedTrajectory && firstAttack.Progress >= .18f &&
                        firstAttack.Progress < BattleSchoolAttackGraphic.ContactProgress)
                    {
                        NavalBattleVisualCapture.Capture(controller, trajectoryFile, "真实出牌自然行进关键帧：轨迹尚未到达目标");
                        capturedTrajectory = true;
                    }
                    if (!capturedHit && firstAttack.Progress >= .6f)
                    {
                        onFirst?.Invoke(firstAttack);
                        NavalBattleVisualCapture.Capture(controller, file, caption);
                        capturedHit = true;
                    }
                }
                if (!controller.IsBattleAnimationPlaying && ActiveSecondBatchAttacks().Length == 0) break;
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "Real attack feedback must finish and unlock the battlefield.");
            Assert.That(seen.Count, Is.EqualTo(expectedCount()), "Each committed attack must own one school presentation.");
            Assert.That(seen.Count, Is.GreaterThan(0));
            Assert.That(capturedHit, Is.True, "The first real attack must reach its natural hit capture phase.");
            if (trajectoryFile != null)
                Assert.That(capturedTrajectory, Is.True, "The requested trajectory frame must be captured naturally before contact.");
            yield return null;
        }

        private static IEnumerator AssertNoSecondBatchAttackDuringAction(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            do
            {
                Assert.That(ActiveSecondBatchAttacks(), Is.Empty, "Setup, rejected input or an already consumed delayed result must not impersonate an attack.");
                if (!controller.IsBattleAnimationPlaying) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            yield return null;
        }

        private static IEnumerator AwaitSecondBatchResolution(CoreGameplayDemoController controller, Action<BattleResolutionMotion> observed)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                BattleResolutionMotion motion = Object.FindObjectsByType<BattleResolutionMotion>(FindObjectsSortMode.None)
                    .FirstOrDefault(item => item.IsPlaying && item.gameObject.activeInHierarchy);
                if (motion != null)
                {
                    Assert.That(motion.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                    observed(motion);
                    yield break;
                }
                if (!controller.IsBattleAnimationPlaying) break;
                yield return null;
            }
            Assert.Fail("A real terminal action must expose the result motion before rebuilding the battlefield.");
        }

        private static void AdvanceSecondBatchResolutionTo(BattleResolutionMotion motion, float progress) =>
            motion.AdvancePresentation(Mathf.Max(0f, progress - motion.Progress) * motion.Duration);

        private static string SecondBatchOutcomeFingerprint(CoreVoyageSession session) =>
            session.Phase + "|" + session.ProfileGold + "|" + session.Cargo.Gold + "|" + session.Cargo.Fame + "|" +
            session.Hull + "|" + (session.Settlement == null ? "-" : JsonUtility.ToJson(session.Settlement)) + "|" +
            JsonUtility.ToJson(session.CaptureTreasureProgress()) + "|" + JsonUtility.ToJson(session.CaptureCollectionProgress());
    }
}
