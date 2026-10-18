using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BattleResponsive_UpgradedManeuverDrawAndGuardResolveTogetherWithoutRebuildingTheBattlefield()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval,
                new Dictionary<string, int> { { "card_full_sail_turn", 1 } });
            SetResponsiveHand(session, "card_full_sail_turn", "card_brace", "card_brace");
            SetResponsiveDrawPile(session, "card_emergency_repair", "card_chain_shot");
            session.Battle.DistanceMeters = 500f;
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            session.Battle.Energy = 1;
            RenderResponsiveBattle(controller);
            yield return null;

            int stageId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int oceanId = RequiredResponsive<RawImage>("Shared tactical sea").GetInstanceID();
            int boardId = RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board").GetInstanceID();
            int actionPoints = session.Battle.NavalActionPoints;
            VoyagePoint playerPosition = session.Battle.PlayerPositionMeters;
            VoyagePoint enemyPosition = session.Battle.EnemyPositionMeters;
            int playerId = RequiredResponsive<RectTransform>("Player Ship Art").GetInstanceID();
            int enemyId = RequiredResponsive<RectTransform>("Enemy Ship Art").GetInstanceID();
            int defenseId = RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").GetInstanceID();
            Button played = RequiredResponsive<Button>("Battle Card 0");
            Button firstGuard = RequiredResponsive<Button>("Battle Card 1");
            Button secondGuard = RequiredResponsive<Button>("Battle Card 2");
            int firstGuardId = firstGuard.GetInstanceID();
            int secondGuardId = secondGuard.GetInstanceID();
            CoreBattleCard turn = session.GetBattleCard(session.Battle.Hand[0]);
            Assert.That(turn.UpgradeLevel, Is.EqualTo(1));
            Assert.That(turn.Energy, Is.EqualTo(1));
            Assert.That(turn.Draw, Is.EqualTo(2));
            Assert.That(turn.NavalActionPointGain, Is.EqualTo(2), "The authored +1 maneuver adds one draw and one action point to its base rewards.");
            Assert.That(played.interactable, Is.True, "The upgraded maneuver retains its authored one-energy cost.");

            FieldInfo blockingAnimation = typeof(CoreGameplayDemoController).GetField("battleAnimationPlaying", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(blockingAnimation, Is.Not.Null);
            float started = Time.realtimeSinceStartup;
            played.onClick.Invoke();
            played.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.False);
            float committed = -1f;
            float deadline = started + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                bool blocking = (bool)blockingAnimation.GetValue(controller);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.EqualTo(!blocking),
                    "Core action feedback locks input; the subsequent cosmetic hand flight remains interruptible.");
                if (committed < 0f && session.Battle.CardsPlayedThisTurn > 0)
                    committed = Time.realtimeSinceStartup - started;
                yield return null;
            }
            float elapsed = Time.realtimeSinceStartup - started;
            yield return null;
            TestContext.WriteLine("[BattleResponsive timing] upgraded turn visuals=" + elapsed.ToString("F3") +
                "s; commit=" + committed.ToString("F3") + "s; same battlefield=" +
                (RequiredResponsive<RectTransform>("Battle Card").GetInstanceID() == stageId) +
                "; same player=" + (RequiredResponsive<RectTransform>("Player Ship Art").GetInstanceID() == playerId) +
                "; same remaining card=" + (RequiredResponsive<Button>("Battle Card 0").GetInstanceID() == firstGuardId));

            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(elapsed, Is.LessThan(2f), "The full maneuver feedback, including the cosmetic hand flight, must finish within the response budget.");
            Assert.That(committed, Is.InRange(0f, 0.9f), "The card's core result should follow the short lift, not a prolonged presentation delay.");
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(stageId));
            Assert.That(RequiredResponsive<RawImage>("Shared tactical sea").GetInstanceID(), Is.EqualTo(oceanId));
            Assert.That(RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board").GetInstanceID(), Is.EqualTo(boardId));
            Assert.That(RequiredResponsive<RectTransform>("Player Ship Art").GetInstanceID(), Is.EqualTo(playerId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Ship Art").GetInstanceID(), Is.EqualTo(enemyId));
            Assert.That(RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").GetInstanceID(), Is.EqualTo(defenseId));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(firstGuardId));
            Assert.That(RequiredResponsive<Button>("Battle Card 1").GetInstanceID(), Is.EqualTo(secondGuardId));
            CollectionAssert.AreEqual(new[] { ResponsiveCardIndex("card_brace"), ResponsiveCardIndex("card_brace"),
                ResponsiveCardIndex("card_emergency_repair"), ResponsiveCardIndex("card_chain_shot") }, session.Battle.Hand);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1), "Repeated clicks during the lift cannot resolve twice.");
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(turn.Block));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(500f));
            Assert.That(session.Battle.Alignment, Is.EqualTo(NavalBattleAlignment.HeadOn));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(actionPoints + 2));
            Assert.That(session.Battle.PlayerPositionMeters, Is.EqualTo(playerPosition));
            Assert.That(session.Battle.EnemyPositionMeters, Is.EqualTo(enemyPosition));
            AssertResponsiveHandMatchesCore(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);

            // This object originally captured hand index 1. After the preceding card is
            // consumed it must play index 0, not the next duplicate or a newly drawn card.
            firstGuard.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(turn.Block + 2));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(secondGuardId));
            AssertResponsiveHandMatchesCore(session);
            Assert.That(GameObject.Find("Battle Card").GetComponentsInChildren<BattleFxGraphic>(), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleResponsive_MiddleDuplicateConsumesOnlyItsOwnViewAndStaleClicksCannotPlayTheNextCard()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_brace", "card_emergency_repair");
            RenderResponsiveBattle(controller);
            yield return null;
            Button retainedDuplicate = RequiredResponsive<Button>("Battle Card 0");
            Button middle = RequiredResponsive<Button>("Battle Card 1");
            Button retainedRepair = RequiredResponsive<Button>("Battle Card 2");
            int duplicateId = retainedDuplicate.GetInstanceID();
            int repairId = retainedRepair.GetInstanceID();
            UnityEvent obsoleteClick = middle.onClick;
            middle.onClick.Invoke();
            obsoleteClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            TestContext.WriteLine("[BattleResponsive timing] middle duplicate retained=" +
                (RequiredResponsive<Button>("Battle Card 0").GetInstanceID() == duplicateId) +
                "; following card retained=" + (RequiredResponsive<Button>("Battle Card 1").GetInstanceID() == repairId));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(duplicateId));
            Assert.That(RequiredResponsive<Button>("Battle Card 1").GetInstanceID(), Is.EqualTo(repairId));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(2));
            int[] handAfter = session.Battle.Hand.ToArray();
            obsoleteClick.Invoke();
            yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "A retained UnityEvent from the destroyed card must be inert.");
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            CollectionAssert.AreEqual(handAfter, session.Battle.Hand);
            retainedDuplicate.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(4));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(repairId));
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleResponsive_RealComponentCardRepairsGuardsAndDrawsWithoutSerialFeedbackOrLosingHover()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreSpecialCardDefinition special = CoreSpecialCardCatalog.ForComponent("instrument_deepwater_abacus");
            Assert.That(special, Is.Not.Null);
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval,
                new Dictionary<string, int> { { special.Card.Id, 2 } }, componentIds: new[] { special.SourceId });
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 25);
            SetResponsiveHand(session, special.Card.Id, "card_brace");
            SetResponsiveDrawPile(session, "card_chain_shot", "card_emergency_repair");
            RenderResponsiveBattle(controller);
            yield return null;
            CoreBattleCard specialCard = session.GetBattleCard(session.Battle.Hand[0]);
            // Authored deepwater-abacus +2: draw stays 1, repair 5 * 180% = 9, guard (2 + instrument bonus 3) * 160% = 8.
            Assert.That(specialCard.UpgradeLevel, Is.EqualTo(2));
            Assert.That(specialCard.Draw, Is.EqualTo(1));
            Assert.That(specialCard.Repair, Is.EqualTo(9));
            Assert.That(specialCard.Block, Is.EqualTo(8));
            Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
            int hullBefore = session.Hull;
            int energyBefore = session.Battle.Energy;
            int energyCost = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
            int stageId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int retainedId = RequiredResponsive<Button>("Battle Card 1").GetInstanceID();
            FieldInfo blockingAnimation = typeof(CoreGameplayDemoController).GetField("battleAnimationPlaying", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(blockingAnimation, Is.Not.Null);
            float started = Time.realtimeSinceStartup;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.False);
            bool sawSeparatedFeedback = false;
            float deadline = started + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                bool blocking = (bool)blockingAnimation.GetValue(controller);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.EqualTo(!blocking),
                    "Input stays locked for the real action; later card-deal visuals do not extend that lock.");
                Text shield = GameObject.Find("Player Shield Float")?.GetComponent<Text>();
                Text repair = GameObject.Find("Player Repair Float")?.GetComponent<Text>();
                if (blocking && shield != null && repair != null && shield.color.a > .01f && repair.color.a > .01f)
                {
                    Assert.That(Mathf.Abs(shield.rectTransform.anchoredPosition.y - repair.rectTransform.anchoredPosition.y),
                        Is.GreaterThanOrEqualTo(45f), "Concurrent shield and repair numbers must occupy separate readable lines.");
                    sawSeparatedFeedback = true;
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            yield return null;
            float elapsed = Time.realtimeSinceStartup - started;
            TestContext.WriteLine("[BattleResponsive timing] component repair/guard/draw visuals=" + elapsed.ToString("F3") +
                "s; same battlefield=" + (RequiredResponsive<RectTransform>("Battle Card").GetInstanceID() == stageId));
            Assert.That(elapsed, Is.LessThan(2f));
            Assert.That(sawSeparatedFeedback, Is.True, "Compound component effects should be shown together without overlapping their numbers.");
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(stageId));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(retainedId));
            Assert.That(session.Hull, Is.EqualTo(hullBefore + specialCard.Repair));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(specialCard.Block));
            Assert.That(session.Battle.Energy, Is.EqualTo(energyBefore - energyCost));
            CollectionAssert.AreEqual(new[] { "card_brace", "card_chain_shot" },
                session.Battle.Hand.Select(index => session.GetBattleCard(index).Id).ToArray(),
                "The authored one-card draw appends the first draw-pile card while retaining the original brace.");
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            BattleDefenseHud hud = RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track");
            Assert.That(hud.DisplayedHull, Is.EqualTo(session.Hull));
            Assert.That(hud.DisplayedGuard, Is.EqualTo(specialCard.Block));
            AssertResponsiveHandMatchesCore(session);
            foreach (Button card in RequiredResponsive<RectTransform>("Battle Hand").GetComponentsInChildren<Button>())
            {
                Behaviour hover = card.GetComponent("BattleCardHoverFeedback") as Behaviour;
                Assert.That(hover, Is.Not.Null);
                Assert.That(hover.enabled, Is.True,
                    "Retained and newly drawn cards must both keep hover presentation enabled.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleResponsive_PhaseTransitionsRebuildTheRequiredStageAndUnlockItsCorrectActions()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            RenderResponsiveBattle(controller);
            yield return null;
            int preparationId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.Not.EqualTo(preparationId));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            Assert.That(GameObject.Find("Battle Board"), Is.Not.Null);
            AssertResponsiveHandMatchesCore(session);

            ConfigureCloseNavalTestPose(session.Battle, false);
            session.Battle.Energy = 0;
            RenderResponsiveBattle(controller);
            yield return null;
            int navalId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            Button board = RequiredResponsive<Button>("Battle Board");
            Assert.That(board.interactable, Is.True);
            board.onClick.Invoke();
            board.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.Not.EqualTo(navalId));
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(GameObject.Find("Battle Board"), Is.Null);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleResponsive_LethalCannonRebuildsTheVoyageAndLeavesNoLockedBattleObjects()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_brace");
            session.Battle.DistanceMeters = 250f;
            session.Battle.EnemyHull = 1;
            session.Battle.EnemyBlock = 0;
            RenderResponsiveBattle(controller);
            yield return null;
            Button cannon = RequiredResponsive<Button>("Battle Card 0");
            UnityEvent stale = cannon.onClick;
            cannon.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
            Assert.That(GameObject.Find("Battle Card"), Is.Null);
            Assert.That(GameObject.Find("Battle Resolution Banner"), Is.Null);
            stale.Invoke();
            yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadResponsiveBattleScene()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static CoreVoyageSession CreateResponsiveBattle(CoreGameplayDemoController controller,
            NavalBattleStage stage, IReadOnlyDictionary<string, int> upgrades = null, string crewId = null,
            IReadOnlyList<string> crewIds = null, double? gameHours = null, int pirateLevel = 1,
            IReadOnlyList<string> componentIds = null)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            if (componentIds != null) session.ConfigureSpecialCardSources(crewIds ??
                (crewId == null ? Array.Empty<string>() : new[] { crewId }), componentIds);
            else if (crewIds != null) session.ConfigureSpecialCardSources(crewIds, Array.Empty<string>());
            else if (crewId != null) session.ConfigureSpecialCardSources(new[] { crewId }, Array.Empty<string>());
            if (upgrades != null) session.ConfigureCardUpgrades(upgrades);
            if (gameHours.HasValue) Assert.That(session.RestoreGameClock(gameHours.Value), Is.True);
            if (pirateLevel > 1) session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = CoreVoyageSession.PirateExperienceForLevel(pirateLevel) });
            session.SetSeed("BATTLE-RESPONSIVENESS-REGRESSION");
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
            CoreEncounter encounter = new CoreEncounter("responsive_battle", "出牌响应验收敌舰", "海战", "反馈与复用隔离验证。", true, false, 2, 180);
            CoreMapNode node = new CoreMapNode
            {
                Id = 998,
                Position = session.Position,
                Encounter = encounter,
                TerrainConstraint = EventTerrainConstraint.OpenSea,
            };
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { encounter, false });
            if (stage == NavalBattleStage.Naval) Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.PlayerBlock = 0;
            session.Battle.PlayerLoadedShot = 0;
            session.Battle.PlayerFireRounds = 0;
            session.Battle.PlayerDeckBuff = 0;
            return session;
        }

        private static void SetResponsiveHand(CoreVoyageSession session, params string[] ids)
        {
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(ids.Select(ResponsiveCardIndex));
            session.Battle.Energy = session.Battle.MaxEnergy;
            session.Battle.CardsPlayedThisTurn = 0;
        }

        private static void SetResponsiveDrawPile(CoreVoyageSession session, params string[] drawOrder)
        {
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            session.Battle.DrawPile.AddRange(drawOrder.Reverse().Select(ResponsiveCardIndex));
        }

        private static int ResponsiveCardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .First(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static T RequiredResponsive<T>(string name) where T : Component
        {
            GameObject obj = GameObject.Find(name);
            Assert.That(obj, Is.Not.Null, "Expected active object: " + name);
            T component = obj.GetComponent<T>();
            Assert.That(component, Is.Not.Null, name + " must contain " + typeof(T).Name);
            return component;
        }

        private static void RenderResponsiveBattle(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        private static IEnumerator WaitResponsiveBattle(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                AdvanceRamComicIfWaiting(controller);
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "Feedback must always finish and restore input.");
            yield return null;
        }

        private static void AssertResponsiveHandMatchesCore(CoreVoyageSession session)
        {
            RectTransform hand = RequiredResponsive<RectTransform>("Battle Hand");
            Button[] buttons = hand.GetComponentsInChildren<Button>();
            Assert.That(buttons.Length, Is.EqualTo(session.Battle.Hand.Count));
            for (int index = 0; index < session.Battle.Hand.Count; index++)
            {
                Button button = RequiredResponsive<Button>("Battle Card " + index);
                Assert.That(button.transform.parent, Is.EqualTo(hand));
                Assert.That(RequiredResponsive<Text>("Card Name " + index).text, Does.Contain(session.GetBattleCard(session.Battle.Hand[index]).Name));
                Assert.That(RequiredResponsive<Text>("Card Energy Cost Text " + index).text,
                    Is.EqualTo(session.GetBattleCardEnergyCost(session.Battle.Hand[index]).ToString()),
                    "The hand must quote the actual shared-repeat cost, not the deck's base cost.");
                Assert.That(button.interactable, Is.EqualTo(session.CanPlayCard(index)), "Availability must be refreshed for current energy, range and supplies.");
            }
        }
    }
}
