using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class NavalBattleVisualFeedbackTests
    {
        private bool hadTutorialCompleted;
        private int savedTutorialCompleted;
        private string savedCardUpgrades;
        private string savedSpecialCardExperience;
        private string savedAcquiredSpecialCards;
        private string savedComponentInventory;
        private readonly Dictionary<string, int?> savedShipOwnership = new Dictionary<string, int?>();
        private readonly Dictionary<NavalBattleStage, string> savedDecks = new Dictionary<NavalBattleStage, string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedComponentInventory = PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey)
                ? PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey) : null;
            savedShipOwnership.Clear();
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                string key = CoreGameplayDemoController.ShipOwnedKey(ship);
                savedShipOwnership[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            }
            hadTutorialCompleted = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey);
            savedTutorialCompleted = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            savedCardUpgrades = PlayerPrefs.HasKey(CoreGameplayDemoController.BattleCardUpgradeKey) ? PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey) : null;
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleCardUpgradeKey);
            savedSpecialCardExperience = PlayerPrefs.HasKey(CoreGameplayDemoController.SpecialCardExperienceKey) ? PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey) : null;
            savedAcquiredSpecialCards = PlayerPrefs.HasKey(CoreGameplayDemoController.AcquiredSpecialCardsKey) ? PlayerPrefs.GetString(CoreGameplayDemoController.AcquiredSpecialCardsKey) : null;
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            savedDecks.Clear();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                string key = CoreGameplayDemoController.StageBattleDeckKey(stage);
                if (PlayerPrefs.HasKey(key)) savedDecks[stage] = PlayerPrefs.GetString(key);
            }
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (CoreGameplayDemoController controller in Object.FindObjectsByType<CoreGameplayDemoController>(FindObjectsSortMode.None))
                Object.Destroy(controller.gameObject);
            yield return null;
            if (savedComponentInventory == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, savedComponentInventory);
            foreach (KeyValuePair<string, int?> ownership in savedShipOwnership)
                if (ownership.Value.HasValue) PlayerPrefs.SetInt(ownership.Key, ownership.Value.Value);
                else PlayerPrefs.DeleteKey(ownership.Key);
            if (savedCardUpgrades == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleCardUpgradeKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.BattleCardUpgradeKey, savedCardUpgrades);
            if (savedSpecialCardExperience == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, savedSpecialCardExperience);
            if (savedAcquiredSpecialCards == null) PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            else PlayerPrefs.SetString(CoreGameplayDemoController.AcquiredSpecialCardsKey, savedAcquiredSpecialCards);
            if (hadTutorialCompleted) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, savedTutorialCompleted);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                string key = CoreGameplayDemoController.StageBattleDeckKey(stage);
                if (savedDecks.TryGetValue(stage, out string value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
            // Restore keys flushed by scene initialization, including absent keys.
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CannonVolley_ShowsMuzzleImpactAndWhiteGuard_ConsumesOneCardAndAnimatesRealHealth()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = UseFreshNavalBattle(controller, 250f);
            session.Battle.EnemyBlock = 6;
            session.Battle.PlayerFireRounds = 1;
            SetSingleCard(session, "card_chain_shot");
            foreach (string id in new[] { "card_full_sail_turn", "card_evasive_maneuver", "card_emergency_repair", "card_brace" })
                session.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(index => CoreVoyageSession.CardLibrary[index].Id == id));
            int[] expectedRemainingHand = session.Battle.Hand.Skip(1).ToArray();
            Render(controller);
            yield return null;
            Transform runtimeCanvas = controller.transform.Find("Core Gameplay Canvas");
            Assert.That(runtimeCanvas, Is.Not.Null);
            RectTransform viewport = FixedGameViewport.Bounds(runtimeCanvas.GetComponent<Canvas>());
            Assert.That(viewport.Find("Header"), Is.Null,
                "Battle runtime should remove the obsolete generic Header.");
            Assert.That(viewport.Find("Ocean Backdrop"), Is.Null,
                "Battle runtime should remove the obsolete Ocean Backdrop.");            CoreBattleCard card = CoreVoyageSession.CardLibrary[session.Battle.Hand[0]];
            BattleDefenseHud enemyHud = Find<BattleDefenseHud>("Enemy Battle Hull Bar Track");
            BattleDefenseHud playerHud = Find<BattleDefenseHud>("Player Battle Hull Bar Track");
            Assert.That(enemyHud.DisplayedGuard, Is.EqualTo(6f));
            Assert.That(Find<BattleStatusRow>("Enemy Battle Status Row").transform.Find("Status guard/Amount").GetComponent<Text>().text,
                Is.EqualTo("6"), "Current guard is labeled by the status icon below the health bar.");
            Assert.That(enemyHud.transform.Find("Enemy Armor Bar"), Is.Null);
            Assert.That(enemyHud.HullBar.sprite, Is.Not.Null, "UGUI ignores fillAmount when a Filled Image has no sprite.");
            Assert.That(enemyHud.ChipBar.sprite, Is.Not.Null);
            Assert.That(enemyHud.transform.Find("Enemy Armor Frame 0"), Is.Null);
            Assert.That(enemyHud.transform.Find("Enemy Armor Frame 1"), Is.Null);
            int permanentArmor = session.Armor;
            Assert.That(playerHud.PermanentArmor, Is.EqualTo(permanentArmor));
            int hullBefore = session.Battle.EnemyHull;
            int maxHull = session.Battle.EnemyMaxHull;
            int ammoBefore = session.Cargo.Cannonballs;
            int energyBefore = session.Battle.Energy;
            int enemyBlockBefore = session.Battle.EnemyBlock;
            int expectedDamage = Mathf.Max(0, session.GetBattleCardDamage(card, session.Battle.Stage) - enemyBlockBefore);
            Assert.That(expectedDamage, Is.GreaterThan(0), "Fixture must break guard and visibly damage the hull.");
            NavalBattleVisualCapture.Capture(controller, "07_Cannon_GuardBefore.png", "炮击前：无外框细血条、格挡状态图标与船体生命");

            Button source = Find<Button>("Battle Card 0");
            source.onClick.Invoke();
            source.onClick.Invoke(); // Also exercise direct duplicate UI invocation, not merely disabled pointer input.
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(Find<Button>("End Battle Turn").interactable, Is.False);
            Assert.That(source.interactable, Is.False);
            HashSet<BattleFxKind> observed = new HashSet<BattleFxKind>();
            bool capturedCard = false, capturedMuzzle = false, capturedImpact = false, capturedHealth = false;
            bool capturedCardTypeStamp = false;
            int maximumMuzzleFlashCount = 0;
            bool sawDamageChip = false, sawIntermediateHull = false, sawIntermediateGuard = false;
            int cardBoundsChecks = 0;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                GameObject root = GameObject.Find("Battle Card");
                BattleFxGraphic[] effects = root == null ? Array.Empty<BattleFxGraphic>() : root.GetComponentsInChildren<BattleFxGraphic>();
                maximumMuzzleFlashCount = Mathf.Max(maximumMuzzleFlashCount, effects.Count(effect => effect.Kind == BattleFxKind.MuzzleFlash));
                foreach (BattleFxGraphic effect in effects)
                {
                    observed.Add(effect.Kind);
                    Assert.That(effect.raycastTarget, Is.False, "Particles cannot swallow ship/card pointer input.");
                }
                if (source != null && source.gameObject.activeInHierarchy && effects.Any(effect => effect.Kind == BattleFxKind.CardShine && effect.Progress > 0f))
                {
                    AssertCardInsideStage(source.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform, root.GetComponent<RectTransform>());
                    cardBoundsChecks++;
                }
                if (!capturedCard && effects.Any(effect => effect.Kind == BattleFxKind.CardShine && effect.Progress > 0.1f))
                {
                    NavalBattleVisualCapture.Capture(controller, "08_Card_Release.png", "出牌蓄势：卡面扫光与释放反馈");
                    capturedCard = true;
                }
                if (!capturedMuzzle && effects.Any(effect => effect.Kind == BattleFxKind.MuzzleFlash && effect.Progress > 0.1f))
                {
                    NavalBattleVisualCapture.Capture(controller, "09_Cannon_Muzzle.png", "开炮瞬间：炮口焰、烟雾与炮弹轨迹");
                    capturedMuzzle = true;
                }
                if (!capturedCardTypeStamp && GameObject.Find("Battle Card Cast Stamp") != null)
                    capturedCardTypeStamp = true;

                if (!capturedImpact && effects.Any(effect => effect.Kind == BattleFxKind.Impact && effect.Progress > 0.08f))
                {
                    NavalBattleVisualCapture.Capture(controller, "10_Cannon_Impact.png", "命中瞬间：敌舰受击与白色护甲破裂反馈");
                    capturedImpact = true;
                }
                if (enemyHud != null)
                {
                    sawIntermediateGuard |= enemyHud.DisplayedGuard > 0.01f && enemyHud.DisplayedGuard < 5.99f;
                    sawIntermediateHull |= enemyHud.DisplayedHull < hullBefore - 0.001f && enemyHud.DisplayedHull > hullBefore - expectedDamage + 0.001f;
                    sawDamageChip |= enemyHud.ChipBar.fillAmount > enemyHud.HullBar.fillAmount + 0.001f;
                    if (!capturedHealth && enemyHud.ChipBar.fillAmount > enemyHud.HullBar.fillAmount + 0.001f)
                    {
                        NavalBattleVisualCapture.Capture(controller, "11_Hull_DamageTrail.png", "血条下降：数值同步递减并保留短暂伤害残条");
                        capturedHealth = true;
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "A single card must complete its feedback and unlock the next input.");
            yield return null;
            Assert.That(observed, Does.Contain(BattleFxKind.MuzzleFlash));
            Assert.That(capturedCardTypeStamp, Is.True, "A played card should briefly label its category while the action resolves.");
            Assert.That(maximumMuzzleFlashCount, Is.GreaterThanOrEqualTo(3), "Loaded fire rounds should read as a staggered three-shot salvo.");
            Assert.That(observed, Does.Contain(BattleFxKind.Smoke));
            Assert.That(observed, Does.Contain(BattleFxKind.Trail));
            Assert.That(observed, Does.Contain(BattleFxKind.Impact));
            Assert.That(observed, Does.Contain(BattleFxKind.ArmorBreak));
            Assert.That(cardBoundsChecks, Is.GreaterThan(0), "The card must remain inside the battle frame while its real release animation runs.");
            Assert.That(sawIntermediateGuard, Is.True, "Guard should visually deplete through intermediate values.");
            Assert.That(sawIntermediateHull, Is.True, "Health should decline through intermediate values, not snap on UI rebuild.");
            Assert.That(sawDamageChip, Is.True, "A delayed damage trail must make the amount of lost hull legible.");
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hullBefore - expectedDamage));
            Assert.That(session.Battle.EnemyBlock, Is.Zero);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(energyBefore - card.Energy));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(ammoBefore - card.CannonballCost));
            CollectionAssert.AreEqual(expectedRemainingHand, session.Battle.Hand,
                "Only the selected cannon card is consumed; the other four cards remain in their original order.");
            BattleDefenseHud updated = Find<BattleDefenseHud>("Enemy Battle Hull Bar Track");
            Assert.That(updated.DisplayedGuard, Is.Zero);
            Assert.That(updated.DisplayedHull, Is.EqualTo(hullBefore - expectedDamage));
            Assert.That(updated.HullBar.fillAmount, Is.EqualTo((hullBefore - expectedDamage) / (float)maxHull).Within(0.001f));
            Assert.That(updated.ChipBar.fillAmount, Is.EqualTo(updated.HullBar.fillAmount).Within(0.001f));
            AssertFilledMeshWidth(updated.HullBar);
            AssertFilledMeshWidth(updated.ChipBar);
            Assert.That(updated.transform.Find("Enemy Armor Bar"), Is.Null);
            Assert.That(Find<Text>("Enemy Hull Numeric").text, Does.StartWith((hullBefore - expectedDamage).ToString()));
            Assert.That(session.Armor, Is.EqualTo(permanentArmor), "Visual guard depletion must not consume permanent ship armor.");
            Assert.That(Find<BattleDefenseHud>("Player Battle Hull Bar Track").PermanentArmor, Is.EqualTo(permanentArmor));
            Assert.That(Find<Button>("End Battle Turn").interactable, Is.True);
            Assert.That(GameObject.Find("Battle Card").GetComponentsInChildren<BattleFxGraphic>(), Is.Empty,
                "The completed action must remove its transient effect objects.");
            NavalBattleVisualCapture.Capture(controller, "12_Cannon_Resolved.png", "命中结算完成：仅扣除一次弹药、能量与敌舰生命，操作解锁");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ApproachAndRetreat_GrantActionPointsAndAnimateCommittedRoutesOnTheRetainedBoard()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (bool retreat in new[] { false, true })
            {
                CoreVoyageSession session = UseFreshNavalBattle(controller, 600f);
                SetTacticalPose(session.Battle, true, new VoyagePoint(0f, 0f), retreat ? 180f : 0f);
                SetTacticalPose(session.Battle, false, new VoyagePoint(600f, 0f), 180f);
                SetSingleCard(session, retreat ? "card_retreat" : "card_approach");
                int points = session.Battle.NavalActionPoints;
                Render(controller);
                yield return null;
                var board = Find<NavalTacticalBoardView>("Naval Tactical Board");
                var sea = Find<RawImage>("Shared tactical sea");
                int boardId = board.GetInstanceID(), seaId = sea.GetInstanceID();
                float before = Find<RectTransform>("Player Ship Art").anchoredPosition.x;
                Find<Button>("Battle Card 0").onClick.Invoke();
                yield return WaitForAnimation(controller);
                Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + 1));
                Assert.That(session.Battle.DistanceMeters, Is.EqualTo(600f));
                Assert.That(Find<RectTransform>("Player Ship Art").anchoredPosition.x, Is.EqualTo(before).Within(.01f));
                var destination = new VoyagePoint(retreat ? -100f : 100f, 0f);
                yield return DragRoute(controller, destination);
                Assert.That(session.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0f, 0f)), "Dragging only previews the route.");
                Find<Button>("Commit Naval Movement").onClick.Invoke();
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                var samples = new List<float>();
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    samples.Add(Find<RectTransform>("Player Ship Art").anchoredPosition.x);
                    AssertTacticalShipMarkers();
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                float after = Find<RectTransform>("Player Ship Art").anchoredPosition.x;
                Assert.That(Mathf.Abs(after - before), Is.GreaterThan(10f));
                Assert.That(samples.Any(x => Mathf.Abs(x - before) > 1f && Mathf.Abs(x - after) > 1f), Is.True,
                    "Committed movement must render intermediate ship positions.");
                Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(destination.X).Within(.01f));
                Assert.That(session.Battle.PlayerPositionMeters.Y, Is.EqualTo(destination.Y).Within(.01f));
                Assert.That(session.Battle.DistanceMeters, Is.EqualTo(retreat ? 700f : 500f).Within(.01f));
                Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
                Assert.That(session.Battle.NavalActionPoints, Is.Zero);
                Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(points));
                Assert.That(Find<NavalTacticalBoardView>("Naval Tactical Board").GetInstanceID(), Is.EqualTo(boardId));
                Assert.That(Find<RawImage>("Shared tactical sea").GetInstanceID(), Is.EqualTo(seaId));
                Assert.That(board.CellSizePixels, Is.EqualTo(75f).Within(.01f));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CirclingEnemy_GrantsActionPointThenBentRouteRotatesTheTacticalCameraThroughIntermediatePoses()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = UseFreshNavalBattle(controller, 600f);
            SetTacticalPose(session.Battle, true, new VoyagePoint(0f, 0f), 0f);
            SetTacticalPose(session.Battle, false, new VoyagePoint(600f, 0f), 180f);
            SetSingleCard(session, "card_circle_enemy");
            Render(controller);
            yield return null;
            RectTransform enemy = Find<RectTransform>("Enemy Ship Art");
            NavalTacticalBoardView board = Find<NavalTacticalBoardView>("Naval Tactical Board");
            float startYaw = enemy.localEulerAngles.z;
            List<float> displayedYaw = new List<float>();
            int points = session.Battle.NavalActionPoints;
            Find<Button>("Battle Card 0").onClick.Invoke();
            yield return WaitForAnimation(controller);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + 1));
            Assert.That(Mathf.DeltaAngle(startYaw, enemy.localEulerAngles.z), Is.EqualTo(0f).Within(.01f));
            yield return DragRoute(controller, new VoyagePoint(100f, 0f), new VoyagePoint(100f, 100f));
            Find<Button>("Commit Naval Movement").onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            float deadline = Time.realtimeSinceStartup + 12f;
            bool capturedTurn = false;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                if (enemy != null)
                {
                    displayedYaw.Add(enemy.localEulerAngles.z);
                    AssertTacticalShipMarkers();
                    if (!capturedTurn && Mathf.Abs(Mathf.DeltaAngle(startYaw, enemy.localEulerAngles.z)) > 8f)
                    {
                        NavalBattleVisualCapture.Capture(controller, "05_Tactical_Camera_Turning.png", "折线航行途中：镜头旋转与敌舰屏幕投影连续变化");
                        capturedTurn = true;
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            yield return null;
            float finalYaw = Find<RectTransform>("Enemy Ship Art").localEulerAngles.z;
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(100f).Within(.01f));
            Assert.That(session.Battle.PlayerPositionMeters.Y, Is.EqualTo(100f).Within(.01f));
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(90f));
            Assert.That(session.Battle.EnemyHeadingDegrees, Is.EqualTo(180f), "Camera rotation must preserve the enemy's world heading.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(startYaw, finalYaw)), Is.GreaterThan(10f));
            Assert.That(displayedYaw.Any(yaw => Mathf.Abs(Mathf.DeltaAngle(startYaw, yaw)) > 3f &&
                Mathf.Abs(Mathf.DeltaAngle(finalYaw, yaw)) > 3f), Is.True,
                "The ship marker must rotate through intermediate screen poses during the real movement animation.");
            Assert.That(Mathf.DeltaAngle(finalYaw, session.Battle.EnemyHeadingDegrees + board.CameraRotationDegrees - 90f), Is.EqualTo(0f).Within(.01f));
            NavalBattleVisualCapture.Capture(controller, "06_BentRoute_Arrival.png", "折线航行完成：世界朝向与屏幕投影保持一致");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingApproach_AdjacentSameHeadingShipsEnterTheFullBodyDeckScene()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (NavalBoardingApproach expected in new[] { NavalBoardingApproach.Alongside, NavalBoardingApproach.Quarter })
            {
                CoreVoyageSession session = UseFreshNavalBattle(controller, 100f);
                CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, false);
                session.Battle.PlannedBoardingApproach = expected;
                SetSingleCard(session, "card_parallel_course");
                Render(controller);
                yield return null;
                Find<Button>("Battle Card 0").onClick.Invoke();
                yield return WaitForAnimation(controller);
                Assert.That(session.Battle.DistanceMeters, Is.EqualTo(100f));
                Assert.That(session.Battle.Alignment, Is.EqualTo(NavalBattleAlignment.Parallel));
                Assert.That(session.Battle.PlannedBoardingApproach, Is.EqualTo(expected));
                Assert.That(Find<Button>("Battle Board").interactable, Is.True);
                Find<Button>("Battle Board").onClick.Invoke();
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                yield return WaitForAnimation(controller);
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(session.Battle.BoardingApproach, Is.EqualTo(expected));
                BoardingCinematicAssertions.AssertScene(session);
                Assert.That(Find<Button>("End Battle Turn").interactable, Is.True);
                NavalBattleVisualCapture.Capture(controller,
                    expected == NavalBoardingApproach.Quarter ? "14_Boarding_Quarter.png" : "13_Boarding_Alongside.png",
                    expected == NavalBoardingApproach.Quarter ? "斜后接舷规则保留，进入完整甲板与双方全身立绘" : "并舷登船规则保留，进入完整甲板与双方全身立绘");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AllTwelveFlagships_RenderTacticalMarkersAndRetainInstalledComponentsAtContactRange()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ResetAuditComponentInventory(controller);
            for (int shipIndex = 0; shipIndex < CoreVoyageSession.Ships.Count; shipIndex++)
            {
                CoreVoyageSession session = UseFreshNavalBattle(controller, 100f, shipIndex, true);
                session.Battle.Alignment = NavalBattleAlignment.HeadOn;
                SetSingleCard(session, "card_approach");
                Render(controller);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(session.SelectedShip, Is.SameAs(CoreVoyageSession.Ships[shipIndex]));
                AssertTacticalShipMarkers();
                var board = Find<NavalTacticalBoardView>("Naval Tactical Board");
                Assert.That(Vector2.Distance(board.PlayerScreenPosition, board.EnemyScreenPosition), Is.EqualTo(75f).Within(.01f),
                    "Contact-range markers preserve the exact one-cell separation.");
                AssertInstalledBattleComponentsWithoutOverlays(controller, shipIndex);
                NavalBattleVisualCapture.Capture(controller, "ShipAudit_Naval_" + shipIndex.ToString("00") + ".png",
                    session.SelectedShip.Name + "：一格接触距离，验证战术船标、投影位置及装备保留");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RegionalFreighter_PreservesTacticalMarkersAndRealActionFeedbackWithInstalledComponents()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ResetAuditComponentInventory(controller);
            foreach (string action in new[] { "card_approach", "card_retreat", "card_circle_enemy", "card_chain_shot", "enemy_fire", "ram" })
            {
                CoreVoyageSession session = UseFreshNavalBattle(controller, action == "ram" ? 100f : 250f, 8, true);
                session.Battle.Alignment = NavalBattleAlignment.HeadOn;
                session.Battle.PlayerMovingTowardEnemy = true;
                if (action == "ram") CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, true);
                if (action.StartsWith("card_")) SetSingleCard(session, action);
                Render(controller);
                yield return null;
                AssertInstalledBattleComponentsWithoutOverlays(controller, 8);
                AssertTacticalShipMarkers();
                RectTransform player = Find<RectTransform>("Player Ship Art");
                RectTransform enemy = Find<RectTransform>("Enemy Ship Art");
                Vector2 playerStart = player.anchoredPosition;
                Quaternion enemyStart = enemy.localRotation;
                int hullBefore = session.Hull;
                int enemyHullBefore = session.Battle.EnemyHull;
                int actionPointsBefore = session.Battle.NavalActionPoints;
                VoyagePoint playerWorldBefore = session.Battle.PlayerPositionMeters;
                bool actionPointCard = action == "card_approach" || action == "card_retreat" || action == "card_circle_enemy";
                Button command = Find<Button>(action == "ram" ? "Battle Ram" : action == "enemy_fire" ? "End Battle Turn" : "Battle Card 0");
                Assert.That(command.interactable, Is.True, action);
                command.onClick.Invoke();
                Assert.That(controller.IsBattleAnimationPlaying, Is.True, action);
                bool sawMotion = false, capturedMotion = false, sawRamComic = false, sawEffects = false;
                int checkedFrames = 0;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    GameObject comic = action == "ram" ? GameObject.Find("Battle Ram Comic Page") : null;
                    if (comic != null)
                    {
                        int pageIndex = (int)typeof(CoreGameplayDemoController).GetField("ramComicPanelIndex",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                        Assert.That(comic.GetComponent<RawImage>().texture,
                            Is.SameAs(Resources.Load<Texture2D>(RamComicCatalog.ImageResource(false, NavalBattleAlignment.EnemyBroadside, pageIndex))));
                        sawRamComic = true;
                        CoreGameplayDemoSceneTests.AdvanceRamComicIfWaiting(controller);
                    }
                    if (player != null && enemy != null)
                    {
                        Assert.That(player.GetComponent<Image>().sprite, Is.Not.Null);
                        Assert.That(enemy.GetComponent<Image>().sprite, Is.Not.Null);
                        Assert.That(float.IsNaN(player.anchoredPosition.x) || float.IsNaN(enemy.anchoredPosition.x), Is.False);
                        checkedFrames++;
                        sawEffects |= GameObject.Find("Battle Card").GetComponentsInChildren<BattleFxGraphic>().Length > 0;
                        sawMotion |= Vector2.Distance(player.anchoredPosition, playerStart) > 0.5f ||
                            Quaternion.Angle(enemy.localRotation, enemyStart) > 1f;
                        if (sawMotion && !capturedMotion)
                        {
                            NavalBattleVisualCapture.Capture(controller, "ShipAudit_SaltFreighter_" + action + ".png",
                                "盐山真实动作中途：" + action + "；验证战术船标和行动反馈");
                            capturedMotion = true;
                        }
                    }
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, action);
                Assert.That(checkedFrames, Is.GreaterThan(1), action + " must render intermediate frames.");
                if (action == "ram" && Resources.Load<Texture2D>(RamComicCatalog.ImageResource(false, NavalBattleAlignment.EnemyBroadside, 0)) != null)
                    Assert.That(sawRamComic, Is.True, "With installed art, ram feedback must display its authored comic.");
                else Assert.That(sawEffects, Is.True, action + " must display transient feedback, including action-point-only cards.");
                if (actionPointCard)
                {
                    Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(actionPointsBefore + 1), action);
                    Assert.That(session.Battle.PlayerPositionMeters, Is.EqualTo(playerWorldBefore), "Cards grant movement for a later route commitment.");
                    Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
                }
                if (action == "enemy_fire") Assert.That(session.Hull, Is.LessThan(hullBefore));
                if (action == "card_chain_shot" || action == "ram") Assert.That(session.Battle.EnemyHull, Is.LessThan(enemyHullBefore));
                if (action == "ram")
                {
                    Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                    Assert.That(session.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.RamBowToBroadside));
                    Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.False);
                    BoardingCinematicAssertions.AssertScene(session);
                }
                else AssertTacticalShipMarkers();
                yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RegionalBoarding_UsesTheSameFullBodyDeckCompositionForBothFlagships()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ResetAuditComponentInventory(controller);
            foreach (int shipIndex in new[] { 5, 8 })
            {
                CoreVoyageSession session = UseFreshNavalBattle(controller, 100f, shipIndex, true);
                CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, false);
                session.Battle.PlannedBoardingApproach = shipIndex == 5 ? NavalBoardingApproach.Alongside : NavalBoardingApproach.Quarter;
                Render(controller);
                yield return null;
                Button board = Find<Button>("Battle Board");
                Assert.That(board.interactable, Is.True);
                board.onClick.Invoke();
                yield return WaitForAnimation(controller);
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Canvas.ForceUpdateCanvases();
                BoardingCinematicAssertions.AssertScene(session);
                NavalBattleVisualCapture.Capture(controller, "ShipAudit_Boarding_" + shipIndex.ToString("00") + ".png",
                    session.SelectedShip.Name + "：真实接舷后显示完整甲板、正常比例双方立绘与分格血条");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void ResetAuditComponentInventory(CoreGameplayDemoController controller)
        {
            typeof(CoreGameplayDemoController).GetField("componentInventory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new OwnedShipComponentInventory(new ComponentArtCatalog()));
        }

        private static void SetTacticalPose(CoreBattleState battle, bool player, VoyagePoint point, float heading)
        {
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(battle, new object[] { player, point, heading });
        }

        private static IEnumerator DragRoute(CoreGameplayDemoController controller, params VoyagePoint[] cells)
        {
            var input = Find<NavalTacticalMovementInput>("Naval Tactical Board");
            var board = input.GetComponent<NavalTacticalBoardView>();
            Canvas canvas = input.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 Screen(VoyagePoint point) => RectTransformUtility.WorldToScreenPoint(camera,
                input.transform.TransformPoint(board.WorldToBoard(point)));
            RaycastResult Hit(Vector2 screen)
            {
                Canvas.ForceUpdateCanvases();
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(hits[0].gameObject == input.gameObject || hits[0].gameObject.name == "Player Ship Art", Is.True,
                    "The real route pointer must reach the board or player ship: " + hits[0].gameObject.name);
                return hits[0];
            }
            Vector2 start = Screen(controller.Session.Battle.PlayerPositionMeters);
            RaycastResult startHit = Hit(start);
            GameObject target = ExecuteEvents.GetEventHandler<IDragHandler>(startHit.gameObject);
            Assert.That(target, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = start, pressPosition = start, button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = startHit, pointerPressRaycast = startHit, pointerDrag = target,
                useDragThreshold = false, eligibleForClick = false
            };
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(startHit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
            Assert.That(input.IsDraggingRoute, Is.True);
            for (int index = 0; index < cells.Length; index++)
            {
                Vector2 screen = Screen(cells[index]);
                pointer.delta = screen - pointer.position;
                pointer.position = screen;
                pointer.pointerCurrentRaycast = Hit(screen);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
                yield return null;
                Assert.That(controller.Session.NavalMovementPlannedSteps, Is.EqualTo(index + 1));
            }
            ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
            yield return null;
            Assert.That(input.IsDraggingRoute, Is.False);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(Find<Button>("Commit Naval Movement").interactable, Is.True);
        }

        private static void AssertTacticalShipMarkers()
        {
            var board = Find<NavalTacticalBoardView>("Naval Tactical Board");
            foreach (string side in new[] { "Player", "Enemy" })
            {
                Image marker = Find<Image>(side + " Ship Art");
                CoreVoyageSession artSession = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
                Assert.That(marker.sprite, Is.SameAs(side == "Player" ? NavalTacticalArtCatalog.ForPlayer(artSession.SelectedShip.Id) :
                    NavalTacticalArtCatalog.ForEnemy(artSession.Battle, artSession.ActiveEncounter, artSession.SelectedRouteIndex)));
                Assert.That(marker.preserveAspect, Is.True);
                Assert.That(marker.raycastTarget, Is.EqualTo(side == "Player"));
                Assert.That(Vector2.Distance(marker.rectTransform.anchoredPosition,
                    side == "Player" ? board.PlayerScreenPosition : board.EnemyScreenPosition), Is.LessThan(.1f));
                Assert.That(marker.rectTransform.localEulerAngles.x, Is.EqualTo(0f).Within(.01f));
                Assert.That(marker.rectTransform.localEulerAngles.y, Is.EqualTo(0f).Within(.01f));
            }
            Assert.That(board.PlayerScreenPosition.x, Is.LessThan(board.EnemyScreenPosition.x));
            Assert.That(board.PlayerScreenPosition.y, Is.EqualTo(board.EnemyScreenPosition.y).Within(.01f));
        }

        private static void AssertInstalledBattleComponentsWithoutOverlays(CoreGameplayDemoController controller, int shipIndex)
        {
            RawImage[] images = Find<RectTransform>("Player Ship Art").GetComponentsInChildren<RawImage>(true);
            Assert.That(images.Count(image => image.name.StartsWith("Player Ship Art Layer ")), Is.Zero,
                "All ships retain their base hull appearance when equipped, including the starting sloop.");
            for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
            {
                ComponentArtEntry installed = controller.ShipComponent(shipIndex, (ComponentSlot)slot);
                Assert.That(installed, Is.Not.Null, "Missing installed slot " + slot + " on ship " + shipIndex);
                Assert.That(controller.InstalledShipComponent(shipIndex, (ComponentSlot)slot), Is.Not.Null,
                    "Equipment remains installed even when its visual layer is omitted.");
            }
        }

        private static IEnumerator WaitForAnimation(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The battle action should complete and restore input.");
            yield return null;
        }

        private static CoreVoyageSession UseFreshNavalBattle(CoreGameplayDemoController controller, float distance, int shipIndex = 0, bool fitOwnedComponents = false)
        {
            Assert.That(controller, Is.Not.Null);
            CoreVoyageSession session = new CoreVoyageSession();
            Assert.That(session.SelectShip(shipIndex), Is.True);
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.SetSeed("NAVAL-VISUAL-FEEDBACK");
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
            if (fitOwnedComponents)
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(shipIndex), 1);
                controller.EnsureShipComponentLoadout(shipIndex);
            }
            session.StartVoyage();
            CoreEncounter encounter = new CoreEncounter("naval_visual_feedback", "视觉验收敌舰", "海战", "动态画面回归。", true, false, 2, 180);
            CoreMapNode node = new CoreMapNode
            {
                Id = 991,
                Position = session.Position,
                Encounter = encounter,
                TerrainConstraint = EventTerrainConstraint.OpenSea,
            };
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { encounter, false });
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DistanceMeters = distance;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.PlayerLoadedShot = 0;
            session.Battle.PlayerFireRounds = 0;
            return session;
        }

        private static void SetSingleCard(CoreVoyageSession session, string cardId)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(card => CoreVoyageSession.CardLibrary[card].Id == cardId);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            session.Battle.Energy = session.Battle.MaxEnergy;
            session.Battle.CardsPlayedThisTurn = 0;
            Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
        }

        private static void AssertCardInsideStage(RectTransform card, RectTransform stage)
        {
            Canvas.ForceUpdateCanvases();
            Vector3[] corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector2[] local = corners.Select(corner => (Vector2)stage.InverseTransformPoint(corner)).ToArray();
            Rect bounds = stage.rect;
            const float tolerance = 2f;
            Assert.That(local.Min(corner => corner.x), Is.GreaterThanOrEqualTo(bounds.xMin - tolerance),
                "Raised/cast cards must not jump left of the battle frame after leaving the hand layout.");
            Assert.That(local.Max(corner => corner.x), Is.LessThanOrEqualTo(bounds.xMax + tolerance));
            Assert.That(local.Min(corner => corner.y), Is.GreaterThanOrEqualTo(bounds.yMin - tolerance));
            Assert.That(local.Max(corner => corner.y), Is.LessThanOrEqualTo(bounds.yMax + tolerance),
                "Reparented hand cards need centered anchors; an upper-left layout anchor otherwise launches them off-screen.");
        }

        private static void AssertFilledMeshWidth(Image image)
        {
            Assert.That(image.sprite, Is.Not.Null, image.name + " needs a sprite for UGUI Filled geometry.");
            image.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            // Unity 6 returns the renderer-owned mesh; do not allocate a destination
            // mesh or destroy the live Canvas geometry after inspecting it.
            Mesh mesh = image.canvasRenderer.GetMesh();
            Vector3[] vertices = mesh == null ? Array.Empty<Vector3>() : mesh.vertices;
            float renderedWidth = vertices.Length == 0 ? 0f : vertices.Max(vertex => vertex.x) - vertices.Min(vertex => vertex.x);
            Assert.That(renderedWidth, Is.EqualTo(image.rectTransform.rect.width * image.fillAmount).Within(1f),
                image.name + " must change its rendered geometry, not only expose a changed fillAmount property.");
        }

        private static T Find<T>(string name) where T : Component
        {
            GameObject item = GameObject.Find(name);
            Assert.That(item, Is.Not.Null, "Missing battle UI: " + name);
            T component = item.GetComponent<T>();
            Assert.That(component, Is.Not.Null, name + " needs " + typeof(T).Name);
            return component;
        }

        private static void Render(CoreGameplayDemoController controller)
        {
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        }
    }
}
