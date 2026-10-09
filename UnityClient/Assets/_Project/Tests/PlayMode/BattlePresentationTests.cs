using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            // Stop Update/coroutines before returning the real roster and active
            // dispatch to PlayerPrefs; test controllers must not process them.
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
            yield return CoreGameplayDemoController.DispatchMissionKey;
            yield return CoreGameplayDemoController.DispatchDurationHoursKey;
            yield return CoreGameplayDemoController.DispatchBalanceVersionKey;
            yield return CoreGameplayDemoController.DispatchGameClockKey;
            yield return CoreGameplayDemoController.DispatchRealClockKey;
            for (int profession = 0; profession < 6; profession++)
                yield return CoreGameplayDemoController.CrewRecruitmentKey(profession);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                yield return CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id);
        }

        private static IEnumerable<string> CrewStringKeys()
        {
            yield return CoreGameplayDemoController.DispatchEndsAtKey;
            yield return CoreGameplayDemoController.DispatchCrewKey;
            yield return CoreGameplayDemoController.DispatchCrewIdentityKey;
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
        public IEnumerator BattleOverlay_UsesLocalTerrainAndFireEmblemStyleComposition()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            controller.Session.StartVoyage();
            EnterBattle(controller.Session, EventTerrainConstraint.Reef);
            ArrangeOneCardPerType(controller.Session.Battle);
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
            RawImage backdrop = GameObject.Find("Battle Scene Art").GetComponent<RawImage>();
            Assert.That(backdrop.texture.name, Does.Contain("battle_coral_reef"));
            Rect expectedBackdropUv = BattleViewLayout.BackdropUv(
                controller.Session.Battle.DistanceMeters,
                backdrop.rectTransform.rect.size);
            Assert.That(backdrop.uvRect.x, Is.EqualTo(expectedBackdropUv.x).Within(0.0001f));
            Assert.That(backdrop.uvRect.y, Is.EqualTo(expectedBackdropUv.y).Within(0.0001f));
            Assert.That(backdrop.uvRect.width, Is.EqualTo(expectedBackdropUv.width).Within(0.0001f));
            Assert.That(backdrop.uvRect.height, Is.EqualTo(expectedBackdropUv.height).Within(0.0001f));
            Assert.That(GameObject.Find("Demo Route Map"), Is.Null, "The battle overlay must use a local environment rather than the full voyage chart.");

            RectTransform playerShip = GameObject.Find("Player Ship Art").GetComponent<RectTransform>();
            RectTransform enemyShip = GameObject.Find("Enemy Ship Art").GetComponent<RectTransform>();
            RectTransform hand = GameObject.Find("Battle Hand").GetComponent<RectTransform>();
            RectTransform intent = GameObject.Find("Enemy Intent HUD").GetComponent<RectTransform>();
            Assert.That(WorldRect(hand).yMax, Is.LessThan(WorldRect(playerShip).yMin), "The resting hand must sit below the ships.");
            Assert.That(WorldRect(hand).yMax, Is.LessThan(WorldRect(enemyShip).yMin));
            Assert.That(WorldRect(intent).xMin, Is.GreaterThan(WorldRect(enemyShip).xMax), "Enemy intent stays beside the enemy rather than covering its art.");
            Assert.That(WorldRect(intent).Overlaps(WorldRect(hand)), Is.False, "Intent must not intercept the hand area.");
            Assert.That(WorldRect(intent).Overlaps(WorldRect(GameObject.Find("Enemy Battle Hull Bar Track").GetComponent<RectTransform>())),
                Is.False, "Intent stays adjacent to the segmented defense bar without covering it.");
            Assert.That(GameObject.Find("Player Resource HUD").GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(0f));
            Assert.That(GameObject.Find("Enemy Resource HUD").GetComponent<RectTransform>().anchoredPosition.x, Is.GreaterThan(0f));
            AssertBattleSurfaces(controller);
            Assert.That(controller.Session.Battle.Hand.Select(index => CoreVoyageSession.CardLibrary[index].Type).Distinct().Count(), Is.EqualTo(5));
            Assert.That(controller.Session.Battle.Hand.Select(index => BattleCardStylePalette.ForType(CoreVoyageSession.CardLibrary[index].Type).Material).Distinct().Count(), Is.EqualTo(5));
            Assert.That(controller.Session.Battle.Hand.Select(index => BattleCardStylePalette.ForType(CoreVoyageSession.CardLibrary[index].Type).Surface).Distinct().Count(), Is.EqualTo(5));

            Button firstCard = GameObject.Find("Battle Card 0").GetComponent<Button>();
            RectTransform cardRect = firstCard.GetComponent<RectTransform>();
            RectTransform artRect = GameObject.Find("Card Art 0").GetComponent<RectTransform>();
            Assert.That(cardRect.sizeDelta.x, Is.LessThan(cardRect.sizeDelta.y), "Battle cards should use a vertical rectangular frame.");
            Assert.That(artRect.sizeDelta.x, Is.EqualTo(artRect.sizeDelta.y).Within(0.01f), "The card illustration area should remain square.");
            Assert.That(cardRect.anchoredPosition.y, Is.LessThan(playerShip.anchoredPosition.y));

            RectTransform bubble = GameObject.Find("Card Keyword Bubble 0")?.GetComponent<RectTransform>();
            Assert.That(bubble, Is.Null, "Inactive tooltip should stay hidden before hover.");
            Vector2 restingPosition = cardRect.anchoredPosition;
            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.16f);
            bubble = FindInactive("Card Keyword Bubble 0").GetComponent<RectTransform>();
            Assert.That(cardRect.anchoredPosition.y, Is.GreaterThan(restingPosition.y + 20f));
            Assert.That(bubble.gameObject.activeSelf, Is.True);
            Assert.That(bubble.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.5f));
            Assert.That(GameObject.Find("Keyword Bubble Text").GetComponent<Text>().text, Does.Contain("能量"));
            Text tooltipTitle = FindInactive("Card Keyword Bubble 0").transform.Find("Keyword Bubble Title").GetComponent<Text>();
            CoreBattleCard hoveredCard = controller.Session.GetBattleCard(controller.Session.Battle.Hand[0]);
            Assert.That(tooltipTitle.text, Does.StartWith(hoveredCard.Name));
            Assert.That(tooltipTitle.text, Does.Contain("\n<size=18>分类 · " + hoveredCard.Type + "</size>"));
            Assert.That(tooltipTitle.fontSize, Is.GreaterThanOrEqualTo(24), "The card name must remain larger than its category.");
            Assert.That(!tooltipTitle.resizeTextForBestFit || tooltipTitle.resizeTextMinSize >= 22, Is.True,
                "Best-fit must not shrink the tooltip name below its readable minimum.");
            Text tooltipBody = bubble.Find("Keyword Bubble Text Viewport/Content/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(tooltipBody.fontSize, Is.GreaterThanOrEqualTo(20));
            Assert.That(!tooltipBody.resizeTextForBestFit || tooltipBody.resizeTextMinSize >= 18, Is.True,
                "Long explanations must preserve the 18px readability floor.");
            AssertBattleSurfaces(controller);

            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.That(cardRect.anchoredPosition.y, Is.EqualTo(restingPosition.y).Within(4f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AllRouteBosses_UseTransparentCutoutAndMaintainSeaContact()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            HashSet<Rect> bossUvRegions = new HashSet<Rect>();
            for (int routeIndex = 0; routeIndex < CoreVoyageSession.Routes.Count; routeIndex += 1)
            {
                // Each route uses fresh in-memory state, not profile mutation or a saved voyage.
                CoreVoyageSession session = new CoreVoyageSession();
                session.SelectRoute(routeIndex);
                session.StartVoyage();
                typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
                EnterBattle(session, routeIndex == 2 ? EventTerrainConstraint.Storm : EventTerrainConstraint.OpenSea, true);
                ArrangeOneCardPerType(session.Battle);
                InvokePrivate(controller, "Render");
                yield return null;
                Assert.That(session.Battle.IsBoss, Is.True);
                RawImage enemy = GameObject.Find("Enemy Ship Art").GetComponent<RawImage>();
                Assert.That(enemy.texture, Is.Not.Null);
                Assert.That(enemy.GetComponent("BattleShipCutout"), Is.Not.Null, "Every route boss needs the non-destructive navy-background cutout.");
                Assert.That(enemy.material.shader.name, Is.EqualTo("Shattered Sail/UI/Battle Ship Cutout"));
                bossUvRegions.Add(enemy.uvRect);
                AssertBattleSurfaces(controller);
            }
            Assert.That(bossUvRegions.Count, Is.EqualTo(CoreVoyageSession.Routes.Count), "Each route should retain its own boss ship artwork.");
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
            AssertBattleSurfaces(controller);

            int playableIndex = Enumerable.Range(0, controller.Session.Battle.Hand.Count)
                .First(index => controller.Session.CanPlayCard(index) &&
                    CoreVoyageSession.CardLibrary[controller.Session.Battle.Hand[index]].Energy > 0);
            Button firstCard = GameObject.Find("Battle Card " + playableIndex).GetComponent<Button>();
            int energyBefore = controller.Session.Battle.Energy;
            Assert.That(firstCard.interactable, Is.True, "The animation fixture must exercise an available card in the active stage.");
            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.18f);
            AssertBattleSurfaces(controller);
            ExecuteEvents.Execute(firstCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.24f);
            firstCard.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True, "Card click must still start the real presentation coroutine.");
            yield return WaitForBattleAnimation(controller);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(controller.Session.Battle.Energy, Is.LessThan(energyBefore));
            AssertBattleSurfaces(controller);

            int turnBefore = controller.Session.Battle.Turn;
            Button endTurn = GameObject.Find("End Battle Turn").GetComponent<Button>();
            Assert.That(endTurn.interactable, Is.True, "End turn must unlock when the card animation completes.");
            endTurn.onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            yield return WaitForBattleAnimation(controller);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(controller.Session.Battle.Turn, Is.GreaterThan(turnBefore));
            AssertBattleSurfaces(controller);
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

            session.Battle.DistanceMeters = 80f;
            session.Battle.Alignment = NavalBattleAlignment.EnemyBroadside;
            session.Battle.PlayerMovingTowardEnemy = true;
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
            session.Battle.DistanceMeters = 50f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
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
        public IEnumerator EightCardHand_StaysInsideHandAreaAndRaisesHoveredCardAboveTheFan()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            CoreVoyageSession session = UseFreshBattleSession(controller);
            EnterBattle(session, EventTerrainConstraint.OpenSea);
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(session.GetBattleDeck(NavalBattleStage.Naval).Take(8));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(8));
            InvokePrivate(controller, "Render");
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform hand = GameObject.Find("Battle Hand").GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hand);
            AssertHandFitsArea(hand, 8);

            Button hovered = GameObject.Find("Battle Card 3").GetComponent<Button>();
            Canvas hoverCanvas = hovered.GetComponent<Canvas>();
            Assert.That(hoverCanvas, Is.Not.Null);
            Assert.That(hoverCanvas.overrideSorting, Is.False);
            ExecuteEvents.Execute(hovered.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.That(hoverCanvas.overrideSorting, Is.True);
            Assert.That(hoverCanvas.sortingOrder, Is.GreaterThan(hand.GetComponentInParent<Canvas>().sortingOrder),
                "An overlapped card must render above its neighboring cards while inspected.");
            Assert.That(FindInactive("Card Keyword Bubble 3").activeSelf, Is.True);
            ExecuteEvents.Execute(hovered.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(hoverCanvas.overrideSorting, Is.False);
            AssertHandFitsArea(hand, 8);
            LogAssert.NoUnexpectedReceived();
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
            for (int index = 0; index < count; index++)
            {
                RectTransform card = GameObject.Find("Battle Card " + index).GetComponent<RectTransform>();
                Assert.That(card.IsChildOf(hand), Is.True);
                Rect bounds = WorldRect(card);
                const float tolerance = 1f;
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(handBounds.xMin - tolerance));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(handBounds.xMax + tolerance));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(handBounds.yMin - tolerance));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(handBounds.yMax + tolerance));
            }
        }

        private static IEnumerator WaitForBattleAnimation(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The battle animation should complete and restore the UI.");
            yield return null;
        }

        private static void ArrangeOneCardPerType(CoreBattleState battle)
        {
            battle.Hand.Clear();
            // These five cards exercise the original five visual materials. The melee
            // sample deliberately remains unavailable during the naval stage.
            foreach (string type in new[] { "炮击", "机动", "登船", "维修", "防御" })
            {
                int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .First(index => CoreVoyageSession.CardLibrary[index].Type == type);
                battle.Hand.Add(card);
            }
        }

        private static void AssertBattleSurfaces(CoreGameplayDemoController controller)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform player = GameObject.Find("Player Ship Art").GetComponent<RectTransform>();
            RectTransform enemy = GameObject.Find("Enemy Ship Art").GetComponent<RectTransform>();
            foreach (string side in new[] { "Player", "Enemy" })
            {
                GameObject contactObject = GameObject.Find(side + " Ship Water Contact");
                Assert.That(contactObject, Is.Not.Null, side + " ship needs a local waterline/wake rather than hovering over the background.");
                Graphic contact = contactObject.GetComponent<Graphic>();
                Assert.That(contact, Is.Not.Null);
                Assert.That(contact.canvasRenderer, Is.Not.Null, "Water meshes must actually render on the Canvas.");
                Assert.That(contact.raycastTarget, Is.False, "Sea contact decorations cannot intercept cards or sailing controls.");
                Rect shipBounds = WorldRect(side == "Player" ? player : enemy);
                Rect contactBounds = WorldRect(contact.rectTransform);
                Assert.That(contactBounds.center.y, Is.LessThan(shipBounds.center.y));
                Assert.That(contactBounds.Overlaps(shipBounds), Is.True, "The contact effect must meet the lower part of its ship.");
            }
            RawImage enemyArt = enemy.GetComponent<RawImage>();
            Assert.That(enemyArt.material.shader.name, Is.EqualTo("Shattered Sail/UI/Battle Ship Cutout"));
            Assert.That(enemyArt.raycastTarget, Is.False);

            Rect endTurn = WorldRect(GameObject.Find("End Battle Turn").GetComponent<RectTransform>());
            Rect enemyStatus = WorldRect(GameObject.Find("Enemy Resource HUD").GetComponent<RectTransform>());
            Assert.That(endTurn.yMin, Is.GreaterThan(enemyStatus.yMax), "End turn belongs above the enemy status frame, not over the hand.");
            Assert.That(endTurn.xMin, Is.LessThan(enemyStatus.xMax));
            Assert.That(endTurn.xMax, Is.GreaterThan(enemyStatus.xMin));
            for (int index = 0; index < controller.Session.Battle.Hand.Count; index += 1)
            {
                CoreBattleCard data = CoreVoyageSession.CardLibrary[controller.Session.Battle.Hand[index]];
                Button card = GameObject.Find("Battle Card " + index).GetComponent<Button>();
                BattleCardSurfaceGraphic surface = card.GetComponentInChildren<BattleCardSurfaceGraphic>();
                Assert.That(surface, Is.Not.Null, "Each card needs its category-specific material/lettering panel.");
                Assert.That(surface.CardType, Is.EqualTo(data.Type));
                Assert.That(surface.raycastTarget, Is.False);
                Assert.That(card.colors.normalColor, Is.EqualTo(Color.white), "Selectable must not multiply the category palette into black.");
                Rect cardBounds = WorldRect(card.GetComponent<RectTransform>());
                Assert.That(endTurn.Overlaps(cardBounds), Is.False, "The end-turn button cannot cover cards, including lifted cards.");
                Assert.That(cardBounds.Overlaps(WorldRect(player)), Is.False, "Cards must not cover the player ship.");
                Assert.That(cardBounds.Overlaps(WorldRect(enemy)), Is.False, "Cards must not cover the enemy ship.");
            }
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
