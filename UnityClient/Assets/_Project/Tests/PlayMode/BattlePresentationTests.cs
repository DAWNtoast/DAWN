using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattlePresentationTests
    {
        private bool hadTutorialCompleted;
        private int savedTutorialCompleted;
        private string savedCardUpgrades;
        private string savedSpecialCardExperience;
        private string savedAcquiredSpecialCards;
        private readonly Dictionary<NavalBattleStage, string> savedStageDecks = new Dictionary<NavalBattleStage, string>();
        private readonly Dictionary<string, int?> savedCrewIntPrefs = new Dictionary<string, int?>();
        private readonly Dictionary<string, string> savedCrewStringPrefs = new Dictionary<string, string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PreserveAndClearCrewPrefs();
            hadTutorialCompleted = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey);
            savedTutorialCompleted = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            savedCardUpgrades = PlayerPrefs.HasKey(CoreGameplayDemoController.BattleCardUpgradeKey) ? PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey) : null;
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleCardUpgradeKey);
            savedSpecialCardExperience = PlayerPrefs.HasKey(CoreGameplayDemoController.SpecialCardExperienceKey) ? PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey) : null;
            savedAcquiredSpecialCards = PlayerPrefs.HasKey(CoreGameplayDemoController.AcquiredSpecialCardsKey) ? PlayerPrefs.GetString(CoreGameplayDemoController.AcquiredSpecialCardsKey) : null;
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            savedStageDecks.Clear();
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                string key = CoreGameplayDemoController.StageBattleDeckKey(stage);
                if (PlayerPrefs.HasKey(key)) savedStageDecks[stage] = PlayerPrefs.GetString(key);
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Stop Update/coroutines before restoring the real roster to PlayerPrefs;
            // test controllers must not process it.
            foreach (CoreGameplayDemoController controller in Object.FindObjectsByType<CoreGameplayDemoController>(FindObjectsSortMode.None))
                Object.Destroy(controller.gameObject);
            yield return null;
            RestoreCrewPrefs();
            if (savedCardUpgrades == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleCardUpgradeKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.BattleCardUpgradeKey, savedCardUpgrades);
            if (savedSpecialCardExperience == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, savedSpecialCardExperience);
            if (savedAcquiredSpecialCards == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.AcquiredSpecialCardsKey, savedAcquiredSpecialCards);
            if (hadTutorialCompleted) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, savedTutorialCompleted);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                string key = CoreGameplayDemoController.StageBattleDeckKey(stage);
                if (savedStageDecks.TryGetValue(stage, out string saved)) PlayerPrefs.SetString(key, saved);
                else PlayerPrefs.DeleteKey(key);
            }
            // Scene initialization flushes the new progression keys; restore the
            // user's originals on disk as well as in this test process's cache.
            PlayerPrefs.Save();
            yield return null;
        }

        private void PreserveAndClearCrewPrefs()
        {
            savedCrewIntPrefs.Clear();
            savedCrewStringPrefs.Clear();
            foreach (string key in CrewIntKeys())
            {
                savedCrewIntPrefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }
            foreach (string key in CrewStringKeys())
            {
                savedCrewStringPrefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                PlayerPrefs.DeleteKey(key);
            }
        }

        private void RestoreCrewPrefs()
        {
            foreach (KeyValuePair<string, int?> entry in savedCrewIntPrefs)
                if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
            foreach (KeyValuePair<string, string> entry in savedCrewStringPrefs)
                if (entry.Value != null) PlayerPrefs.SetString(entry.Key, entry.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
        }

        private static IEnumerable<string> CrewIntKeys()
        {
            yield return CoreGameplayDemoController.LegacyWitchRecruitedKey;
            for (int profession = 0; profession < 6; profession++)
                yield return CoreGameplayDemoController.CrewRecruitmentKey(profession);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                yield return CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id);
        }

        private static IEnumerable<string> CrewStringKeys()
        {
            yield return CoreGameplayDemoController.CrewAtSeaKey;
            for (int profession = 0; profession < 6; profession++)
                yield return CoreGameplayDemoController.ActiveCrewVariantKey(profession);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                yield return CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id);
            // Departure's ordinary/special/standby selections are controller
            // fields, so loading a new scene resets them with the cleared roster.
        }

        [Test]
        public void BattleEnvironmentCatalog_LoadsDistinctTerrainArtwork()
        {
            BattleEnvironmentArtCatalog catalog = new BattleEnvironmentArtCatalog();
            Texture2D open = catalog.ForTerrain(SeaTerrainType.OpenWater);
            Texture2D beach = catalog.ForTerrain(SeaTerrainType.Beach);
            Texture2D strait = catalog.ForTerrain(SeaTerrainType.Strait);
            Texture2D reef = catalog.ForTerrain(SeaTerrainType.Reef);
            Texture2D fog = catalog.ForTerrain(SeaTerrainType.FogBank);
            Texture2D storm = catalog.ForTerrain(SeaTerrainType.StormFront);

            Assert.That(catalog.Count, Is.EqualTo(6));
            Assert.That(new[] { open, beach, strait, reef, fog, storm }, Has.None.Null);
            Assert.That(open.name, Is.Not.EqualTo(beach.name));
            Assert.That(strait.name, Is.Not.EqualTo(reef.name));
            Assert.That(fog.name, Is.Not.EqualTo(storm.name));
        }

        [Test]
        public void VoyageMapArtCatalog_LoadsAllDetailedChartTextures()
        {
            VoyageMapArtCatalog catalog = new VoyageMapArtCatalog();

            Assert.That(catalog.IsAvailable, Is.True);
            Assert.That(catalog.LoadedTextureCount, Is.EqualTo(12));
            Assert.That(catalog.GroundDetail, Is.Not.Null, "Continuous coasts need their opaque ground material.");
            Assert.That(catalog.ChartBase, Is.Not.Null);
            Assert.That(catalog.ChartBase.name, Is.EqualTo("water_chart_base_v2"));
            Assert.That(catalog.WaterAt(1u, 0, 0), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.Shoal), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.Island), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.Reef), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.KelpField), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.FogBank), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.StormFront), Is.Not.Null);
            Assert.That(catalog.TerrainFor(SeaTerrainType.Strait), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BattleOverlay_UsesTacticalSeaAndKeepsTerrainAndCardComposition()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            controller.Session.StartVoyage();
            EnterBattle(controller.Session, EventTerrainConstraint.Reef);
            ArrangeOneCardPerType(controller.Session.Battle);
            Assert.That(controller.Session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            controller.Session.Battle.Energy = 0; // Exercise the real unavailable-card tooltip in the active naval stage.
            InvokePrivate(controller, "Render");
            yield return null;

            RectTransform battleSurface = GameObject.Find("Battle Card").GetComponent<RectTransform>();
            RectTransform overlay = GameObject.Find("Battle Overlay").GetComponent<RectTransform>();
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(overlay.sizeDelta, Is.EqualTo(new Vector2(1920f, 972f)).Within(0.1f));
            Assert.That(overlay.anchoredPosition, Is.EqualTo(new Vector2(0f, -54f)).Within(0.1f));
            Assert.That(battleSurface.localScale.x, Is.EqualTo(972f / 900f).Within(0.001f));
            Assert.That(battleSurface.localScale.y, Is.EqualTo(972f / 900f).Within(0.001f));
            Assert.That(controller.Session.BattleTerrain, Is.EqualTo(SeaTerrainType.Reef));
            RawImage backdrop = GameObject.Find("Shared tactical sea").GetComponent<RawImage>();
            Assert.That(backdrop.texture, Is.SameAs(NavalTacticalArtCatalog.ForSea(controller.Session.BattleTerrain)));
            Assert.That(backdrop.raycastTarget, Is.False);
            Assert.That(GameObject.Find("Naval Tactical Board").GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(GameObject.Find("100 metre grid").GetComponent<NavalTacticalGridGraphic>(), Is.Not.Null);
            Assert.That(GameObject.Find("Demo Route Map"), Is.Null, "The battle overlay must use a local environment rather than the full voyage chart.");

            RectTransform playerShip = GameObject.Find("Player Ship Art").GetComponent<RectTransform>();
            RectTransform enemyShip = GameObject.Find("Enemy Ship Art").GetComponent<RectTransform>();
            RectTransform hand = GameObject.Find("Battle Hand").GetComponent<RectTransform>();
            Assert.That(hand.sizeDelta.y, Is.LessThan(CardFaceLayout.HandSize.y), "The naval fan returns space to the tactical sea.");
            Assert.That(hand.anchoredPosition.y, Is.LessThan(CardFaceLayout.HandPosition.y));
            RectTransform intent = GameObject.Find("Enemy Intent Icons").GetComponent<RectTransform>();
            Assert.That(WorldRect(hand).yMax, Is.LessThan(WorldRect(playerShip).yMin), "The resting hand must sit below the ships.");
            Assert.That(WorldRect(hand).yMax, Is.LessThan(WorldRect(enemyShip).yMin));
            Assert.That(WorldRect(intent).Overlaps(WorldRect(enemyShip)), Is.False, "Enemy intent remains separate from the enemy ship art.");
            Assert.That(WorldRect(intent).Overlaps(WorldRect(hand)), Is.False, "Intent must not intercept the hand area.");
            Assert.That(WorldRect(intent).Overlaps(WorldRect(GameObject.Find("Enemy Battle Hull Bar Track").GetComponent<RectTransform>())),
                Is.False, "Intent stays above the health bar without covering it.");
            Assert.That(GameObject.Find("Battle Compact Resources").GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(0f));
            Assert.That(GameObject.Find("Battle Discard Pile").GetComponent<RectTransform>().anchoredPosition.x, Is.GreaterThan(0f));
            yield return AssertBattleSurfaces(controller);
            Assert.That(controller.Session.Battle.Hand.Select(index => CoreVoyageSession.CardLibrary[index].Type).Distinct().Count(), Is.EqualTo(6));
            for (int slot = 0; slot < controller.Session.Battle.Hand.Count; slot++)
            {
                CoreBattleCard data = CoreVoyageSession.CardLibrary[controller.Session.Battle.Hand[slot]];
                CardSchoolFrameGraphic schoolFrame = GameObject.Find("Battle Card " + slot).GetComponentInChildren<CardSchoolFrameGraphic>();
                Assert.That(schoolFrame, Is.Not.Null);
                Assert.That(schoolFrame.SchoolId, Is.EqualTo(CardSchoolArtCatalog.NormalizeSchool(data.SchoolId)),
                    "Frame identity comes from the school, independently of the six functional categories.");
            }
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "card-frames-six-types.png",
                "Live ordinary-card hand: six functional categories retain their shared wooden school-neutral frame.");

            Button firstCard = GameObject.Find("Battle Card 0").GetComponent<Button>();
            RectTransform cardRect = firstCard.GetComponent<RectTransform>();
            RectTransform artRect = GameObject.Find("Card Art 0").GetComponent<RectTransform>();
            Assert.That(cardRect.sizeDelta.x, Is.LessThan(cardRect.sizeDelta.y), "Battle cards should use a vertical rectangular frame.");
            Assert.That(artRect.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(artRect.GetComponent<RawImage>()).size), "Every card keeps the same illustration window.");
            Assert.That(cardRect.anchoredPosition.y, Is.LessThan(playerShip.anchoredPosition.y));

            RectTransform bubble = GameObject.Find("Card Keyword Bubble 0")?.GetComponent<RectTransform>();
            Assert.That(bubble, Is.Null, "Inactive tooltip should stay hidden before hover.");
            Vector2 restingPosition = cardRect.anchoredPosition;
            float restingCardScale = cardRect.localScale.x;
            Rect restingFrame = WorldRect(firstCard.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.16f);
            Rect inspectedFrame = WorldRect(firstCard.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            Assert.That(cardRect.localScale.x, Is.GreaterThan(restingCardScale));
            Assert.That(inspectedFrame.yMax, Is.GreaterThan(restingFrame.yMax + 1f), "The inspected upper edge rises while the whole taller frame stays visible");
            string unavailableReason = controller.Session.CardUnavailableReason(0);
            Assert.That(firstCard.interactable, Is.False, "The naval cannon requires energy before it can be played.");
            Assert.That(unavailableReason, Does.Contain("能量不足"), "The fixture deliberately has zero energy.");
            CoreGameplayDemoSceneTests.AssertKeywordOnlyCardHover(firstCard, 0, controller.Session,
                controller.Session.GetBattleCard(controller.Session.Battle.Hand[0]), "unaffordable cannon card");
            yield return AssertBattleSurfaces(controller);

            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.That(cardRect.anchoredPosition.y, Is.EqualTo(restingPosition.y).Within(4f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReferenceCardLayouts_KeepTheirFixedWindowsForUpgradedAndLongTextCards()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            string[] cardIds =
            {
                "card_full_sail_turn", "card_navigator_course", "card_cannon_barrage",
                "card_special_cannon_dragonbreath_battery", "card_boarding_curse_08"
            };
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.ConfigureCardUpgrades(cardIds.ToDictionary(id => id, id => 3));
            session.SetSeed("REFERENCE-CARD-LAYOUTS");
            session.StartVoyage();
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
            EnterBattle(session, EventTerrainConstraint.Reef);
            session.Battle.Hand.Clear();
            foreach (string id in cardIds)
            {
                int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .Single(candidate => CoreVoyageSession.CardLibrary[candidate].Id == id);
                Assert.That(session.GetBattleCard(index).UpgradeLevel, Is.EqualTo(session.GetCardMaximumUpgradeLevel(index)),
                    id + " applies its authored upgrade cap to the requested rank");
                session.Battle.Hand.Add(index);
            }
            InvokePrivate(controller, "Render");
            yield return new WaitForSecondsRealtime(.7f);
            yield return AssertBattleSurfaces(controller);
            Text referenceName = GameObject.Find("Card Name 1").GetComponent<Text>();
            Text referenceEffect = GameObject.Find("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            Text dragonbreathEffect = GameObject.Find("Battle Card 3").transform.Find("Label").GetComponent<Text>();
            Text longEffect = GameObject.Find("Battle Card 4").transform.Find("Label").GetComponent<Text>();
            CoreBattleCard navigator = session.GetBattleCard(session.Battle.Hand[1]);
            Assert.That(referenceName.text, Is.EqualTo(navigator.Name + (navigator.UpgradeLevel > 0 ? "+" + navigator.UpgradeLevel : string.Empty)));
            AssertVisibleWordsAndClauseBoundaries(CardFaceLayout.FormatDescription(
                session.BattleCardFaceDescription(session.GetBattleCard(session.Battle.Hand[0]), session.Battle.Stage)),
                referenceEffect.text, "reference card");
            Assert.That(referenceEffect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
            Assert.That(dragonbreathEffect.fontSize, Is.InRange(12, CardFaceLayout.EffectFontSize),
                "The conditional dragonbreath effect fits its fixed window at a readable adaptive size.");
            Assert.That(longEffect.fontSize, Is.InRange(12, CardFaceLayout.EffectFontSize),
                "Current manuscript wording may fit at the shared size; only overflowing text needs to shrink.");
            foreach (Text effect in new[] { referenceEffect, dragonbreathEffect, longEffect })
                AssertCompleteEffectFitsFixedWindow(effect);
            Assert.That(GameObject.Find("Card Powder Cost 2").GetComponent<Text>().text, Is.EqualTo("1"));
            Assert.That(GameObject.Find("Battle Card 2").transform.Find("Label").GetComponent<Text>().text,
                Does.Contain("<b>范围1-4格</b>"), "The actual cannon card emphasizes its complete range condition.");
            NavalBattleVisualCapture.Capture(controller, "reference-card-layouts.png",
                "Actual capped upgrades with corner-aligned blue/red square crystals, matching resource HUD crystals, bold cannon range and a long strategy effect in fixed windows.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AllRouteBosses_UseTacticalShipMarkersAndKeepBossState()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            HashSet<int> checkedRoutes = new HashSet<int>();
            for (int routeIndex = 0; routeIndex < CoreVoyageSession.Routes.Count; routeIndex += 1)
            {
                // Each route uses fresh in-memory state, not profile mutation or a saved voyage.
                CoreVoyageSession session = new CoreVoyageSession();
                session.RestoreTreasureProgress(new CoreTreasureProgress
                    { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                session.SelectRoute(routeIndex);
                Assert.That(session.SelectedRouteIndex, Is.EqualTo(routeIndex), "The art fixture must actually select each unlocked route.");
                session.StartVoyage();
                typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
                EnterBattle(session, routeIndex == 2 ? EventTerrainConstraint.Storm : EventTerrainConstraint.OpenSea, true);
                ArrangeOneCardPerType(session.Battle);
                InvokePrivate(controller, "Render");
                yield return null;
                Assert.That(session.Battle.IsBoss, Is.True);
                Image enemy = GameObject.Find("Enemy Ship Art").GetComponent<Image>();
                Assert.That(enemy.sprite, Is.SameAs(NavalTacticalArtCatalog.ForEnemy(session.Battle, session.ActiveEncounter, session.SelectedRouteIndex)));
                Assert.That(enemy.preserveAspect, Is.True);
                Assert.That(enemy.GetComponent<Outline>().effectColor.r, Is.GreaterThan(.9f), "Enemy markers remain visually distinct from the player's marker.");
                checkedRoutes.Add(session.SelectedRouteIndex);
                yield return AssertBattleSurfaces(controller);
            }
            Assert.That(checkedRoutes.Count, Is.EqualTo(CoreVoyageSession.Routes.Count), "Every unlocked route's real boss state must render on the tactical board.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleSurfaces_PersistAfterHoverCardPlayAndEndTurnAnimations()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            controller.Session.StartVoyage();
            EnterBattle(controller.Session, EventTerrainConstraint.Beach);
            ArrangeOneCardPerType(controller.Session.Battle);
            InvokePrivate(controller, "Render");
            yield return null;
            yield return AssertBattleSurfaces(controller);

            int playableIndex = Enumerable.Range(0, controller.Session.Battle.Hand.Count)
                .First(index => controller.Session.CanPlayCard(index) &&
                    CoreVoyageSession.CardLibrary[controller.Session.Battle.Hand[index]].Energy > 0);
            Button firstCard = GameObject.Find("Battle Card " + playableIndex).GetComponent<Button>();
            int energyBefore = controller.Session.Battle.Energy;
            Assert.That(firstCard.interactable, Is.True, "The animation fixture must exercise an available card in the active stage.");
            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.18f);
            yield return AssertBattleSurfaces(controller);
            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.24f);
            firstCard.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True, "Card click must still start the real presentation coroutine.");
            yield return WaitForBattleAnimation(controller);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(controller.Session.Battle.Energy, Is.LessThan(energyBefore));
            yield return AssertBattleSurfaces(controller);

            int turnBefore = controller.Session.Battle.Turn;
            Button endTurn = GameObject.Find("End Battle Turn").GetComponent<Button>();
            Assert.That(endTurn.interactable, Is.True, "End turn must unlock when the card animation completes.");
            endTurn.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            yield return WaitForBattleAnimation(controller);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(controller.Session.Battle.Turn, Is.GreaterThan(turnBefore));
            yield return AssertBattleSurfaces(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleStageButtons_AdvancePreparationAndRamDirectlyIntoBoardingWithCrewHealth()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            CoreVoyageSession session = UseFreshBattleSession(controller);
            EnterBattle(session, EventTerrainConstraint.OpenSea, advanceToNaval: false);
            InvokePrivate(controller, "Render");
            yield return null;

            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            Assert.That(session.Battle.Hand, Is.Not.Empty);
            Assert.That(session.Battle.Hand.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Preparation)), Is.True);
            Assert.That(session.Battle.Hand.All(index => CoreVoyageSession.CardLibrary[index].Damage == 0), Is.True,
                "Preparation must offer setup cards before either ship can attack.");
            Assert.That(GameObject.Find("Battle Stage Progress"), Is.Null);
            Assert.That(GameObject.Find("Battle Stage Instructions"), Is.Null);
            Assert.That(GameObject.Find("Battle Ram"), Is.Null);
            Assert.That(GameObject.Find("Battle Board"), Is.Null);
            int playerHullBeforePreparation = session.Hull;
            int enemyHullBeforePreparation = session.Battle.EnemyHull;
            Button endTurn = GameObject.Find("End Battle Turn").GetComponent<Button>();
            Assert.That(endTurn.interactable, Is.True);
            endTurn.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            yield return WaitForBattleAnimation(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Hull, Is.EqualTo(playerHullBeforePreparation));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHullBeforePreparation));
            Assert.That(session.Battle.Hand.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Naval)), Is.True);
            Assert.That(GameObject.Find("Battle Stage Progress"), Is.Null);
            Assert.That(GameObject.Find("Battle Stage Instructions"), Is.Null);

            CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, true);
            InvokePrivate(controller, "Render");
            yield return null;
            Button ram = GameObject.Find("Battle Ram").GetComponent<Button>();
            Assert.That(ram.interactable, Is.True);
            int playerHullBeforeRam = session.Hull;
            int enemyHullBeforeRam = session.Battle.EnemyHull;
            ram.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(GameObject.Find("End Battle Turn").GetComponent<Button>().interactable, Is.False);
            yield return WaitForBattleAnimation(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Hull, Is.LessThan(playerHullBeforeRam));
            Assert.That(session.Battle.EnemyHull, Is.LessThan(enemyHullBeforeRam));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.RamBowToBroadside));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Quarter));
            Assert.That(GameObject.Find("Battle Ram"), Is.Null);
            Assert.That(GameObject.Find("Battle Board"), Is.Null,
                "The ram itself must connect the ships; the player must not need a second boarding action.");
            Assert.That(GameObject.Find("Battle Alignment").GetComponent<Text>().text, Does.Contain("角对侧舷"));
            AssertBoardingView(session);
            Assert.That(GameObject.Find("Player Battle Hull Bar").GetComponent<Image>().fillAmount,
                Is.GreaterThan(session.Hull / (float)session.MaxHull), "The fresh crew health bar must not continue displaying the ram-damaged hull.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleBoardButton_ConnectsParallelShipsWithoutRamDamage()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            CoreVoyageSession session = UseFreshBattleSession(controller);
            EnterBattle(session, EventTerrainConstraint.OpenSea);
            CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, false);
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            InvokePrivate(controller, "Render");
            yield return null;
            Button board = GameObject.Find("Battle Board").GetComponent<Button>();
            Assert.That(board.interactable, Is.True);
            board.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            yield return WaitForBattleAnimation(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.Planned));
            Assert.That(session.Battle.BoardingApproach, Is.EqualTo(NavalBoardingApproach.Alongside));
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            AssertBoardingView(session);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBoardingView(CoreVoyageSession session)
        {
            Assert.That(GameObject.Find("Battle Stage Progress"), Is.Null);
            Assert.That(GameObject.Find("Battle Stage Instructions"), Is.Null);
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(GameObject.Find("Player Battle Name").GetComponent<Text>().text, Does.Contain("登船队"));
            Assert.That(GameObject.Find("Enemy Battle Name").GetComponent<Text>().text, Does.Contain("守备队"));
            Assert.That(GameObject.Find("Player Battle Hull Bar").GetComponent<Image>().fillAmount,
                Is.EqualTo(session.Battle.PlayerBoardingHealth / (float)session.Battle.PlayerMaxBoardingHealth).Within(0.001f));
            Assert.That(GameObject.Find("Enemy Battle Hull Bar").GetComponent<Image>().fillAmount,
                Is.EqualTo(session.Battle.EnemyBoardingHealth / (float)session.Battle.EnemyMaxBoardingHealth).Within(0.001f));
            Assert.That(session.Battle.Hand, Is.Not.Empty);
            Assert.That(session.Battle.Hand.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Boarding)), Is.True);
            CollectionAssert.AreEquivalent(session.GetBattleDeck(NavalBattleStage.Boarding).Concat(session.GetCrewBattleCards(NavalBattleStage.Boarding)),
                session.Battle.Hand.Concat(session.Battle.DrawPile).Concat(session.Battle.DiscardPile));
            Assert.That(GameObject.Find("End Battle Turn").GetComponent<Button>().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator EightCardHand_FansWithinBottomAreaAndRevealsHoveredCard()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            CoreVoyageSession session = UseFreshBattleSession(controller);
            EnterBattle(session, EventTerrainConstraint.OpenSea);
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(session.GetBattleDeck(NavalBattleStage.Naval).Take(8));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(8));
            InvokePrivate(controller, "Render");
            yield return WaitForBattleAnimation(controller);
            Canvas.ForceUpdateCanvases();
            RectTransform hand = GameObject.Find("Battle Hand").GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hand);
            AssertHandFitsArea(hand, 8);

            Button hovered = GameObject.Find("Battle Card 3").GetComponent<Button>();
            Canvas hoverCanvas = hovered.GetComponent<Canvas>();
            Vector3 restingScale = hovered.transform.localScale;
            Assert.That(restingScale.x, Is.InRange(.55f, .70f), "Overlapping cards retain readable artwork in a compact fan.");
            Assert.That(hoverCanvas, Is.Not.Null);
            Assert.That(hoverCanvas.overrideSorting, Is.False);
            ExecuteEvents.Execute(hovered.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return WaitForInspectionConvergence(hovered.GetComponent<RectTransform>());
            Assert.That(hoverCanvas.overrideSorting, Is.True);
            AssertInspectionFitsClearSpace(hovered.GetComponent<RectTransform>(), restingScale.x);
            Assert.That(hoverCanvas.sortingOrder, Is.GreaterThan(hand.GetComponentInParent<Canvas>().sortingOrder),
                "An overlapped card must render above its neighboring cards while inspected.");
            CoreGameplayDemoSceneTests.AssertKeywordOnlyCardHover(hovered, 3, controller.Session,
                controller.Session.GetBattleCard(controller.Session.Battle.Hand[3]), "overlapped naval card");
            ExecuteEvents.Execute(hovered.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(hoverCanvas.overrideSorting, Is.False);
            Assert.That(hovered.transform.localScale.x, Is.EqualTo(restingScale.x).Within(.01f));
            AssertHandFitsArea(hand, 8);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFanCostCorners_VisibleTabsAndLiftedCostsAddressTheSameCard()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = UseFreshBattleSession(controller);
            EnterBattle(session, EventTerrainConstraint.OpenSea);
            int shot = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_chain_shot");
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.DistanceMeters = 200f;
            session.Battle.Energy = 30;
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            float minimumInspectionScale = float.PositiveInfinity;
            foreach (int count in new[] { 4, 6, 8 })
            {
                session.Battle.Hand.Clear();
                for (int index = 0; index < count; index++) session.Battle.Hand.Add(shot);
                InvokePrivate(controller, "Render");
                yield return WaitForBattleAnimation(controller);
                Canvas.ForceUpdateCanvases();
                RectTransform hand = GameObject.Find("Battle Hand").GetComponent<RectTransform>();
                AssertHandFitsArea(hand, count);
                NavalBattleVisualCapture.Capture(controller, "integrated-frame-hand-" + count + "-rest.png",
                    "Actual " + count + "-card naval fan; overlapping existing card faces leave the tactical sea visible.");

                foreach (int index in new[] { 0, count / 2, count - 1 })
                {
                    Button button = GameObject.Find("Battle Card " + index).GetComponent<Button>();
                    Assert.That(button.interactable, Is.True);
                    RectTransform card = button.GetComponent<RectTransform>();
                    foreach (CardOrnamentKind kind in new[] { CardOrnamentKind.Energy, CardOrnamentKind.Cannonball, CardOrnamentKind.Gunpowder })
                    {
                        CardOrnamentGraphic marker = button.GetComponentsInChildren<CardOrnamentGraphic>().Single(item => item.Kind == kind);
                        // Use the outer quarter of the integrated emblem: it is
                        // visible outside the content rectangle and used to miss
                        // the button when only the 206-by-300 body was clickable.
                        Vector2 direction = kind == CardOrnamentKind.Energy ? new Vector2(-1f, 1f) :
                            kind == CardOrnamentKind.Cannonball ? new Vector2(-1f, -1f) : new Vector2(1f, -1f);
                        Vector2 localPoint = marker.rectTransform.anchoredPosition +
                            Vector2.Scale(direction, marker.rectTransform.sizeDelta) * .25f;
                        Assert.That(card.rect.Contains(localPoint), Is.False, kind + " must exercise the expanded frame hit area");
                        Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
                        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                        RectTransform energyTab = button.GetComponentsInChildren<CardOrnamentGraphic>()
                            .Single(item => item.Kind == CardOrnamentKind.Energy).rectTransform;
                        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, energyTab.position);
                        string context = count + "-card hand / " + index + " / " + kind;
                        AssertTopRaycastButton(screen, button, "resting " + context);
                        var pointer = new PointerEventData(EventSystem.current) { position = screen };
                        float restScale = card.localScale.x;
                        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                        yield return WaitForInspectionConvergence(card);
                        minimumInspectionScale = Mathf.Min(minimumInspectionScale, InspectionTargetScale(card));
                        AssertInspectionFitsClearSpace(card, restScale, enforcePreferredScale: false);
                        Assert.That(button.GetComponent<Canvas>().sortingOrder, Is.EqualTo(30));
                        AssertFrameInsideRootCanvas(card, context);
                        AssertTopRaycastButton(screen, button, "original pointer after lift " + context);
                        Vector2 liftedCost = RectTransformUtility.WorldToScreenPoint(camera, card.TransformPoint(localPoint));
                        AssertTopRaycastButton(liftedCost, button, "lifted cost corner " + context);
                        if (index == count / 2 && kind == CardOrnamentKind.Energy)
                            NavalBattleVisualCapture.Capture(controller, "integrated-frame-hand-" + count + "-hover.png",
                                "Naval fan inspection straightens and raises one complete card above its neighbors while preserving the original pointer target.");
                        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                        yield return new WaitForSecondsRealtime(.4f);
                    }
                }
                AssertHandFitsArea(hand, count);
                Assert.That(session.Battle.Hand.Count, Is.EqualTo(count), "Hovering and hit testing never play the card");
            }
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(8), "Hovering and hit testing never play the card");
            Assert.That(session.Cargo.Powder, Is.EqualTo(100));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(100));
            LogAssert.NoUnexpectedReceived();
            TestContext.WriteLine("Minimum inspection target across all 4/6/8-card corner checks: " + minimumInspectionScale);
            // Keep the readability target, but do not let it prevent the full
            // input, overlap, viewport and no-payment checks from executing.
            Assert.That(minimumInspectionScale, Is.GreaterThanOrEqualTo(.60f),
                "Inspection preserves legibility while keeping the upper tactical sea unobstructed");
        }

        private static IEnumerator WaitForInspectionConvergence(RectTransform card)
        {
            // Keep the existing initial hover interval, then observe the
            // exponential animation until it reaches the required precision.
            yield return new WaitForSecondsRealtime(.35f);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (Mathf.Abs(card.localScale.x - InspectionTargetScale(card)) > .0002f &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(card.localScale.x, Is.EqualTo(InspectionTargetScale(card)).Within(.0002f),
                "The hover animation must converge within the bounded interval.");
        }

        private static float InspectionTargetScale(RectTransform card)
        {
            Component hover = card.GetComponent("BattleCardHoverFeedback");
            Assert.That(hover, Is.Not.Null);
            return (float)hover.GetType().GetField("inspectionScale", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hover);
        }

        private static void AssertInspectionFitsClearSpace(RectTransform card, float restingScale, bool enforcePreferredScale = true)
        {
            Assert.That(card.localScale.x, Is.GreaterThan(restingScale + .05f), "Inspection must substantially enlarge the resting card");
            float inspectionScale = InspectionTargetScale(card);
            if (enforcePreferredScale)
                Assert.That(inspectionScale, Is.InRange(.60f, 1.01f), "Inspection fits the complete card within the reserved bottom area");
            else Assert.That(inspectionScale, Is.LessThanOrEqualTo(1.01f), "Inspection must not enlarge beyond its preferred full size");
            // Exponential hover easing approaches the target without reaching
            // it exactly after 0.35 seconds. Keep the exact design-size guard
            // above and allow only a subpixel residual on the rendered frame.
            Assert.That(card.localScale.x, Is.EqualTo(inspectionScale).Within(.002f), "The visible inspection must converge to its authored target size");
            AssertFrameInsideRootCanvas(card, "inspected card");
            Rect frame = WorldRect(card.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art" })
            {
                Rect actor = WorldRect(GameObject.Find(name).GetComponent<RectTransform>());
                Assert.That(frame.Overlaps(actor), Is.False, "The complete inspected frame cannot cover " + name);
            }
            // A scaled inspection must use the available vertical clearance,
            // rather than silently shrinking every card to its resting size.
            if (card.localScale.x < .98f)
            {
                Rect screen = WorldRect(FixedGameViewport.Bounds(card.GetComponentInParent<Canvas>()));
                Assert.That(frame.yMin - screen.yMin, Is.LessThan(16f), "Reduced inspections use the game viewport's bottom clearance");
            }
        }

        private static void AssertFrameInsideRootCanvas(RectTransform card, string context)
        {
            Canvas.ForceUpdateCanvases();
            CardSchoolFrameGraphic frame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null, context);
            Canvas root = card.GetComponentInParent<Canvas>().rootCanvas;
            Rect complete = WorldRect(frame.rectTransform);
            Rect screen = WorldRect(FixedGameViewport.Bounds(root));
            const float tolerance = .5f;
            Assert.That(complete.xMin, Is.GreaterThanOrEqualTo(screen.xMin - tolerance), context + " full frame left edge remains on screen");
            Assert.That(complete.xMax, Is.LessThanOrEqualTo(screen.xMax + tolerance), context + " full frame right edge remains on screen");
            Assert.That(complete.yMin, Is.GreaterThanOrEqualTo(screen.yMin - tolerance), context + " full frame bottom costs remain on screen");
            Assert.That(complete.yMax, Is.LessThanOrEqualTo(screen.yMax + tolerance), context + " full frame AP corner remains on screen");
        }

        private static void AssertTopRaycastButton(Vector2 position, Button expected, string context)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current, Is.Not.Null);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty, context);
            Assert.That(hits[0].module, Is.InstanceOf<GraphicRaycaster>(), context + " requires the actual graphic raycaster");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(expected.gameObject), context);
        }

        private static CoreVoyageSession UseFreshBattleSession(CoreGameplayDemoController controller)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.SetSeed("STAGED-PRESENTATION");
            session.StartVoyage();
            FieldInfo field = typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(controller, session);
            return session;
        }

        private static void AssertHandFitsArea(RectTransform hand, int count)
        {
            Canvas.ForceUpdateCanvases();
            Rect handBounds = WorldRect(hand);
            Rect viewport = WorldRect(FixedGameViewport.Bounds(hand.GetComponentInParent<Canvas>()));
            for (int index = 0; index < count; index++)
            {
                RectTransform card = GameObject.Find("Battle Card " + index).GetComponent<RectTransform>();
                Assert.That(card.IsChildOf(hand), Is.True);
                CardSchoolFrameGraphic frame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
                Assert.That(frame, Is.Not.Null);
                Rect bounds = WorldRect(frame.rectTransform);
                const float tolerance = 1f;
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(handBounds.xMin - tolerance));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(handBounds.xMax + tolerance));
                Assert.That(bounds.yMax, Is.GreaterThan(viewport.yMin + 60f), "Every resting card exposes an interactive upper portion.");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.yMin + viewport.height * .26f),
                    "The resting fan leaves the large tactical chart unobstructed.");
                if (index + 1 < count)
                {
                    CardSchoolFrameGraphic other = GameObject.Find("Battle Card " + (index + 1)).GetComponentInChildren<CardSchoolFrameGraphic>();
                    Assert.That(bounds.Overlaps(WorldRect(other.rectTransform)), Is.True, "Adjacent naval cards form an overlapping fan.");
                }
            }
        }

        private static IEnumerator WaitForBattleAnimation(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                CoreGameplayDemoSceneTests.AdvanceRamComicIfWaiting(controller);
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The battle animation should complete and restore the UI.");
            yield return null;
        }

        private static void ArrangeOneCardPerType(CoreBattleState battle)
        {
            battle.Hand.Clear();
            // These six cards exercise every category frame. The melee
            // sample deliberately remains unavailable during the naval stage.
            foreach (string type in new[] { "炮击", "机动", "登船", "维修", "防御", "策略" })
            {
                int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .First(index => CoreVoyageSession.CardLibrary[index].Type == type);
                battle.Hand.Add(card);
            }
        }

        private static IEnumerator AssertBattleSurfaces(CoreGameplayDemoController controller)
        {
            yield return WaitForBattleAnimation(controller);
            Canvas.ForceUpdateCanvases();
            AssertPlayerResourceCrystals(controller.Session.Battle.Stage);
            RectTransform player = GameObject.Find("Player Ship Art").GetComponent<RectTransform>();
            RectTransform enemy = GameObject.Find("Enemy Ship Art").GetComponent<RectTransform>();
            NavalTacticalBoardView board = GameObject.Find("Naval Tactical Board").GetComponent<NavalTacticalBoardView>();
            Assert.That(board, Is.Not.Null);
            foreach (string side in new[] { "Player", "Enemy" })
            {
                RectTransform ship = side == "Player" ? player : enemy;
                Image marker = ship.GetComponent<Image>();
                CoreVoyageSession artSession = controller.Session;
                Assert.That(marker.sprite, Is.SameAs(side == "Player" ? NavalTacticalArtCatalog.ForPlayer(artSession.SelectedShip.Id) :
                    NavalTacticalArtCatalog.ForEnemy(artSession.Battle, artSession.ActiveEncounter, artSession.SelectedRouteIndex)));
                Assert.That(marker.preserveAspect, Is.True);
                Assert.That(marker.raycastTarget, Is.EqualTo(side == "Player"), "Only the player marker captures route selection.");
                Vector2 expected = side == "Player" ? board.PlayerScreenPosition : board.EnemyScreenPosition;
                Assert.That(Vector2.Distance(ship.anchoredPosition, expected), Is.LessThan(.1f));
            }
            Assert.That(player.anchoredPosition.x, Is.LessThan(enemy.anchoredPosition.x));
            Assert.That(player.anchoredPosition.y, Is.EqualTo(enemy.anchoredPosition.y).Within(.1f));

            Rect endTurn = WorldRect(GameObject.Find("End Battle Turn").GetComponent<RectTransform>());
            Rect discard = WorldRect(GameObject.Find("Battle Discard Pile").GetComponent<RectTransform>());
            Assert.That(endTurn.yMin, Is.GreaterThan(discard.yMax), "End turn stays in its separate row above the discard pile.");
            Assert.That(endTurn.xMin, Is.LessThan(discard.xMax));
            Assert.That(endTurn.xMax, Is.GreaterThan(discard.xMin));
            RectTransform hand = GameObject.Find("Battle Hand").GetComponent<RectTransform>();
            Button priorInspection = hand.GetComponentsInChildren<Button>()
                .SingleOrDefault(button => button.GetComponent<Canvas>().overrideSorting);
            var pointer = new PointerEventData(EventSystem.current);
            if (priorInspection != null)
            {
                ExecuteEvents.Execute(priorInspection.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
            }
            int[] originalHand = controller.Session.Battle.Hand.ToArray();
            int originalEnergy = controller.Session.Battle.Energy;
            int originalPowder = controller.Session.Cargo.Powder;
            int originalCannonballs = controller.Session.Cargo.Cannonballs;
            for (int index = 0; index < controller.Session.Battle.Hand.Count; index += 1)
            {
                CoreBattleCard data = CoreVoyageSession.CardLibrary[controller.Session.Battle.Hand[index]];
                Button card = GameObject.Find("Battle Card " + index).GetComponent<Button>();
                BattleCardSurfaceGraphic surface = card.GetComponentInChildren<BattleCardSurfaceGraphic>();
                Assert.That(surface, Is.Not.Null, "Each card needs its category-specific material/lettering panel.");
                Assert.That(surface.CardType, Is.EqualTo(data.Type));
                Assert.That(surface.raycastTarget, Is.False);
                Assert.That(card.colors.normalColor, Is.EqualTo(Color.white), "Selectable must not multiply the category palette into black.");
                RectTransform cardRect = card.GetComponent<RectTransform>();
                CardOrnamentGraphic energyTab = card.GetComponentsInChildren<CardOrnamentGraphic>()
                    .Single(marker => marker.Kind == CardOrnamentKind.Energy);
                Canvas root = card.GetComponentInParent<Canvas>().rootCanvas;
                Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                pointer.position = RectTransformUtility.WorldToScreenPoint(camera, energyTab.rectTransform.position);
                AssertTopRaycastButton(pointer.position, card, "resting energy tab " + index);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return WaitForInspectionConvergence(cardRect);
                Assert.That(card.GetComponent<Canvas>().overrideSorting, Is.True);
                Assert.That(card.GetComponent<Canvas>().sortingOrder, Is.EqualTo(30));
                foreach (Button other in hand.GetComponentsInChildren<Button>())
                    if (other != card) Assert.That(other.GetComponent<Canvas>().overrideSorting, Is.False);
                Assert.That(Quaternion.Angle(cardRect.localRotation, Quaternion.identity), Is.LessThan(.1f),
                    "Inspection straightens each fan card for reading.");
                AssertFrameInsideRootCanvas(cardRect, "inspected card " + index);
                Assert.That(WorldRect(card.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform).yMax,
                    Is.LessThanOrEqualTo(WorldRect(FixedGameViewport.Bounds(root)).yMin +
                        WorldRect(FixedGameViewport.Bounds(root)).height * .32f), "Inspection preserves the tactical chart area.");
                AssertCardFaceFields(controller.Session, card, index);
                Rect cardBounds = WorldRect(cardRect);
                Assert.That(endTurn.Overlaps(cardBounds), Is.False, "The end-turn button cannot cover cards, including lifted cards.");
                Assert.That(cardBounds.Overlaps(WorldRect(player)), Is.False, "Cards must not cover the player ship.");
                Assert.That(cardBounds.Overlaps(WorldRect(enemy)), Is.False, "Cards must not cover the enemy ship.");
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(card.GetComponent<Canvas>().overrideSorting, Is.False);
            }
            CollectionAssert.AreEqual(originalHand, controller.Session.Battle.Hand, "Inspection cannot play or replace any card.");
            Assert.That(controller.Session.Battle.Energy, Is.EqualTo(originalEnergy));
            Assert.That(controller.Session.Cargo.Powder, Is.EqualTo(originalPowder));
            Assert.That(controller.Session.Cargo.Cannonballs, Is.EqualTo(originalCannonballs));
            if (priorInspection != null)
            {
                ExecuteEvents.Execute(priorInspection.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return WaitForInspectionConvergence(priorInspection.GetComponent<RectTransform>());
            }
        }

        private static void AssertCardFaceFields(CoreVoyageSession session, Button card, int index)
        {
            CoreBattleCard data = session.GetBattleCard(session.Battle.Hand[index]);
            Text[] fields = card.GetComponentsInChildren<Text>();
            Text name = fields.Single(item => item.name == "Card Name " + index);
            Text effect = card.transform.Find("Label").GetComponent<Text>();
            Assert.That(name.text, Is.EqualTo(data.Name + (data.UpgradeLevel > 0 ? "+" + data.UpgradeLevel : string.Empty)));
            Assert.That(name.transform.parent.name, Is.EqualTo("Card Name Plate " + index));
            Assert.That(fields.Any(item => item.name == "Card Type " + index), Is.False);
            Assert.That(card.transform.Find("Card Type Plate " + index), Is.Null);
            Assert.That(card.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.CardSize));
            RectTransform art = card.transform.Find("Card Art " + index) as RectTransform;
            Assert.That(art.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).size));
            Assert.That(art.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).center));
            Assert.That(name.rectTransform.sizeDelta, Is.EqualTo(new Vector2(174f, 26f)));
            Assert.That(((RectTransform)name.transform.parent).sizeDelta, Is.EqualTo(CardFaceLayout.NamePlateSize));
            Assert.That(((RectTransform)name.transform.parent).anchoredPosition, Is.EqualTo(CardFaceLayout.NamePlateLayout((RectTransform)name.transform.parent)));
            Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(new Vector2(176f, 94f)));
            Assert.That(effect.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.EffectPosition));
            Assert.That(effect.resizeTextForBestFit, Is.False);
            Assert.That(effect.supportRichText, Is.True, "Usage conditions render as emphasis without exposing markup.");
            Assert.That(name.resizeTextForBestFit, Is.False);
            Assert.That(name.preferredWidth, Is.LessThanOrEqualTo(name.rectTransform.rect.width + .01f), data.Id);
            Assert.That(name.preferredHeight, Is.LessThanOrEqualTo(name.rectTransform.rect.height + .01f), data.Id);
            Assert.That(name.font, Is.SameAs(GameTypography.InterfaceFont), data.Id);
            Canvas.ForceUpdateCanvases();
            CardRibbonTitleEffect ribbonTitle = name.GetComponent<CardRibbonTitleEffect>();
            Assert.That(ribbonTitle, Is.Not.Null, data.Id);
            Assert.That(ribbonTitle.FitsAuthoredRibbon, Is.True, data.Id + " actual rendered title mesh must fit the curved paper band");
            Assert.That(ribbonTitle.RenderedGlyphCount, Is.GreaterThan(0), data.Id);
            Assert.That(name.fontSize, Is.GreaterThanOrEqualTo(15), data.Id + " Canvas scaling must not shrink the logical title font");
            Assert.That(ribbonTitle.RenderedBounds.height, Is.GreaterThanOrEqualTo(16f), data.Id + " actual title strokes remain comparable to the body text");
            Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(effect.rectTransform.rect.height + .01f), data.Id);
            Assert.That(effect.text, Does.Not.Contain("<size=20>"), "The name belongs on its own ribbon, outside the effect body.");
            AssertVisibleWordsAndClauseBoundaries(CardFaceLayout.FormatDescription(
                session.BattleCardFaceDescription(data, session.Battle.Stage)), effect.text, data.Id);
            Assert.That(effect.text, Does.Not.Contain("实际伤害").And.Not.Contain("当前炮击").And.Not.Contain("无药基础"), data.Id);

            CardOrnamentGraphic[] ornaments = card.GetComponentsInChildren<CardOrnamentGraphic>();
            CardOrnamentGraphic energy = ornaments.Single(item => item.Kind == CardOrnamentKind.Energy);
            Text energyNumber = fields.Single(item => item.name == "Card Energy Cost Text " + index);
            AssertCornerCostMarker(energy, card.GetComponent<RectTransform>(), data);
            int actualEnergy = session.GetBattleCardEnergyCost(session.Battle.Hand[index]);
            Assert.That(energyNumber.text, Is.EqualTo(actualEnergy.ToString()));
            Assert.That(energyNumber.color, Is.EqualTo(actualEnergy > data.Energy ? new Color(1f, .24f, .2f, 1f) : Color.white));
            CardOrnamentGraphic[] powder = ornaments.Where(item => item.Kind == CardOrnamentKind.Gunpowder).ToArray();
            Assert.That(powder.Length, Is.EqualTo(data.PowderCost > 0 ? 1 : 0), data.Id + " shows a powder icon only when it has a powder cost.");
            if (data.PowderCost > 0)
            {
                Assert.That(fields.Single(item => item.name == "Card Powder Cost " + index).text, Is.EqualTo(data.PowderCost.ToString()));
            }
            CardOrnamentGraphic[] cannonballs = ornaments.Where(item => item.Kind == CardOrnamentKind.Cannonball).ToArray();
            Assert.That(cannonballs.Length, Is.EqualTo(data.CannonballCost > 0 ? 1 : 0), data.Id + " shows a cannonball icon only when it has a cannonball cost.");
            if (data.CannonballCost > 0)
                Assert.That(fields.Single(item => item.name == "Card Cannonball Cost " + index).text, Is.EqualTo(data.CannonballCost.ToString()));
            var specialKinds = new[] { CardOrnamentKind.Mana, CardOrnamentKind.Health, CardOrnamentKind.Curse, CardOrnamentKind.Chain };
            CardOrnamentGraphic[] specialCosts = ornaments.Where(item => specialKinds.Contains(item.Kind)).ToArray();
            Assert.That(specialCosts, Has.Length.EqualTo(CardSpecialCostPresentation.ResourceType(data).Length > 0 ? 1 : 0), data.Id);
            if (specialCosts.Length > 0)
            {
                Assert.That(specialCosts[0].GetComponentInChildren<Text>().text,
                    Is.EqualTo(CardSpecialCostPresentation.CostText(data, session.Battle)), data.Id);
                AssertCornerCostMarker(specialCosts[0], card.GetComponent<RectTransform>(), data);
            }
            CardSchoolFrameGraphic integratedFrame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(integratedFrame.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(data)), data.Id);
            Assert.That(integratedFrame.rectTransform.sizeDelta, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(data).size), data.Id);
            Assert.That(integratedFrame.rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(data).center), data.Id);
            AssertResourceCrystals(card.GetComponent<RectTransform>(), energy, powder.Concat(cannonballs).Concat(specialCosts).ToArray(), index, data);
            foreach (CardOrnamentGraphic ornament in ornaments)
                Assert.That(ornament.raycastTarget, Is.False, ornament.name + " must not intercept card hover or clicks.");
        }

        private static void AssertResourceCrystals(RectTransform card, CardOrnamentGraphic energy,
            CardOrnamentGraphic[] resources, int index, CoreBattleCard data)
        {
            Assert.That(energy.rectTransform.localScale, Is.EqualTo(Vector3.one), "The complete frame and all numbers scale with their card together");
            Assert.That(energy.rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Energy)));
            Assert.That(card.GetComponent<Canvas>().overrideSorting, Is.True, "Cost visibility is checked on the actual inspected card.");
            var costs = new List<RectTransform> { energy.rectTransform };
            for (int slot = 0; slot < resources.Length; slot++)
            {
                CardOrnamentGraphic resource = resources[slot];
                RectTransform rect = resource.rectTransform;
                Assert.That(rect.parent, Is.SameAs(card));
                AssertCornerCostMarker(resource, card, data);
                costs.Add(rect);
            }
            CardOrnamentGraphic cannonball = resources.SingleOrDefault(item => item.Kind == CardOrnamentKind.Cannonball);
            CardOrnamentGraphic powder = resources.SingleOrDefault(item => item.Kind == CardOrnamentKind.Gunpowder);
            if (cannonball != null && powder != null)
                Assert.That(ScaledCardLocalRect(cannonball.rectTransform).xMax,
                    Is.LessThan(ScaledCardLocalRect(powder.rectTransform).xMin), "Cannonballs occupy the lower-left corner; powder occupies the lower-right");
            Assert.That(card.Find("Card Repeat Cost Plate " + index), Is.Null,
                "Increased costs use red numbers without an adjacent surcharge label.");
            foreach (RectTransform cost in costs)
            {
                foreach (RectTransform other in costs)
                    if (other != cost)
                        Assert.That(ScaledCardLocalRect(cost).Overlaps(ScaledCardLocalRect(other)), Is.False,
                            cost.name + " must not overlap another fee on the same card");
                Rect backing = WorldRect(cost);
                Text costText = cost.GetComponentInChildren<Text>();
                Assert.That(costText, Is.Not.Null);
                Assert.That(costText.text, Is.Not.Empty);
                Rect number = WorldRect(costText.rectTransform);
                Canvas root = card.GetComponentInParent<Canvas>().rootCanvas;
                Rect viewport = WorldRect(FixedGameViewport.Bounds(root));
                foreach (Rect visible in new[] { backing, number })
                {
                    Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(viewport.xMin - .5f), cost.name);
                    Assert.That(visible.xMax, Is.LessThanOrEqualTo(viewport.xMax + .5f), cost.name);
                    Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - .5f), cost.name);
                    Assert.That(visible.yMax, Is.LessThanOrEqualTo(viewport.yMax + .5f), cost.name);
                }
                Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                AssertTopRaycastButton(RectTransformUtility.WorldToScreenPoint(camera, costText.rectTransform.position),
                    card.GetComponent<Button>(), cost.name + " inspected number remains above neighboring cards");
            }
        }

        private static Rect ScaledCardLocalRect(RectTransform rect)
        {
            Vector2 scale = new Vector2(rect.localScale.x, rect.localScale.y);
            return new Rect(rect.anchoredPosition + Vector2.Scale(rect.rect.position, scale), Vector2.Scale(rect.rect.size, scale));
        }

        private static void AssertCornerCostMarker(CardOrnamentGraphic crystal, RectTransform card, CoreBattleCard data)
        {
            RectTransform rect = crystal.rectTransform;
            Assert.That(rect.parent, Is.SameAs(card));
            Vector2 position = CardSchoolArtCatalog.CostPosition(data, crystal.Kind);
            Vector2 size = crystal.Kind == CardOrnamentKind.Energy ? CardFaceLayout.EnergyIconSize :
                crystal.Kind == CardOrnamentKind.Cannonball ? CardFaceLayout.CannonballIconSize :
                crystal.Kind == CardOrnamentKind.Gunpowder ? CardFaceLayout.PowderIconSize : CardFaceLayout.SpecialCostIconSize;
            Assert.That(rect.sizeDelta, Is.EqualTo(size), crystal.name);
            Assert.That(rect.anchoredPosition, Is.EqualTo(position), crystal.name);
            Assert.That(position, Is.Not.EqualTo(Vector2.zero), crystal.name + " must stay calibrated to a corner");
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one), crystal.name + " must not compress separately from its complete frame");
            Assert.That(rect.rect.width, Is.EqualTo(rect.rect.height).Within(.01f), crystal.name);
            Rect bounds = rect.rect;
            bounds.position += rect.anchoredPosition;
            Rect frame = CardSchoolArtCatalog.FrameBounds(data);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(frame.xMin), crystal.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(frame.xMax), crystal.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(frame.yMin), crystal.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(frame.yMax), crystal.name);
            Assert.That(crystal.IsMarkerOnly, Is.True, crystal.name + " image is already part of the complete PNG");
            Assert.That(crystal.GetComponent<Image>(), Is.Null, crystal.name + " contains only its actual number");
        }

        private static void AssertPlayerResourceCrystals(NavalBattleStage stage)
        {
            Transform resources = GameObject.Find("Battle Compact Resources").transform;
            BattleCommandIconGraphic energy = resources.Find("Battle Resource Line 1/Resource Energy/Icon").GetComponent<BattleCommandIconGraphic>();
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Kind, Is.EqualTo(BattleCommandIconKind.Energy));
            AssertHudCrystal(energy);
            BattleCommandIconGraphic powder = resources.Find("Battle Resource Line 3/Resource Powder/Icon").GetComponent<BattleCommandIconGraphic>();
            BattleCommandIconGraphic medicine = resources.Find("Battle Resource Line 3/Resource Medicine/Icon").GetComponent<BattleCommandIconGraphic>();
            Assert.That(powder.Kind, Is.EqualTo(BattleCommandIconKind.Powder));
            Assert.That(medicine.Kind, Is.EqualTo(BattleCommandIconKind.Medicine),
                "Boarding medicine retains its own symbol and must not be mislabelled as gunpowder.");
            AssertHudCrystal(powder);
            AssertHudCrystal(medicine);
        }

        private static void AssertHudCrystal(BattleCommandIconGraphic crystal)
        {
            Assert.That(crystal.rectTransform.rect.width, Is.EqualTo(crystal.rectTransform.rect.height).Within(.01f), crystal.name);
            Assert.That(crystal.GetComponent<Image>(), Is.Null, crystal.name + " replaces the old circular badge");
            Assert.That(crystal.GetComponentInChildren<Text>(), Is.Null, crystal.name + " needs no circular text glyph");
            Assert.That(crystal.raycastTarget, Is.False, crystal.name);
        }

        private static string CompactCardText(string value) => Regex.Replace(value, @"</?(?:b|i|size|color|material)(?:=[^>]*)?>", string.Empty)
            .Replace("\u00a0", " ").Replace("\n", string.Empty).Replace("\r", string.Empty);

        private static void AssertCompleteEffectFitsFixedWindow(Text effect)
        {
            Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize), effect.transform.parent.name);
            Rect bounds = effect.rectTransform.rect;
            TextGenerationSettings settings = effect.GetGenerationSettings(bounds.size);
            settings.scaleFactor = 1f;
            settings.fontSize = effect.fontSize;
            settings.resizeTextForBestFit = false;
            // Measure every explicit rendered line without allowing automatic wrapping
            // or truncation to conceal a line wider/taller than the fixed effect window.
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var generator = new TextGenerator())
            {
                Assert.That(generator.GetPreferredWidth(effect.text, settings),
                    Is.LessThanOrEqualTo(bounds.width + .01f), effect.transform.parent.name + " full line width");
                Assert.That(generator.GetPreferredHeight(effect.text, settings),
                    Is.LessThanOrEqualTo(bounds.height + .01f), effect.transform.parent.name + " complete text height");
            }
        }

        internal static void AssertVisibleWordsAndClauseBoundaries(string source, string rendered, string context)
        {
            string visibleSource = Regex.Replace(source, "<[^>]*>", string.Empty).Replace("\r", string.Empty);
            string pattern = "\\A" + string.Join("(?:\\n+|；\\n*)", visibleSource.Split('\n')
                .Select(paragraph => string.Join("\\n*", paragraph.Select(character => Regex.Escape(character.ToString()))))) + "\\z";
            Assert.That(Regex.Replace(rendered, "<[^>]*>", string.Empty), Does.Match(pattern),
                context + " preserves every visible character and every original clause boundary as a newline or semicolon");
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
                corners.Max(point => point.x), corners.Max(point => point.y));
        }

        private static void EnterBattle(CoreVoyageSession session, EventTerrainConstraint terrain, bool boss = false, bool advanceToNaval = true)
        {
            CoreEncounter encounter = new CoreEncounter(
                "presentation_test_battle",
                "测试敌舰",
                "海战",
                "用于验证战斗界面。",
                true,
                false,
                2,
                90);
            CoreMapNode node = new CoreMapNode
            {
                Id = 999,
                Position = session.Position,
                Encounter = encounter,
                TerrainConstraint = terrain,
                IsBoss = boss,
            };
            FieldInfo activeNode = typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo startBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeNode, Is.Not.Null);
            Assert.That(startBattle, Is.Not.Null);
            activeNode.SetValue(session, node);
            startBattle.Invoke(session, new object[] { encounter, boss });
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            if (!advanceToNaval) return;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            session.Battle.DistanceMeters = 350f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static GameObject FindInactive(string name)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int index = 0; index < transforms.Length; index += 1)
            {
                if (transforms[index].name == name && transforms[index].gameObject.scene.IsValid())
                {
                    return transforms[index].gameObject;
                }
            }
            return null;
        }
    }
}
