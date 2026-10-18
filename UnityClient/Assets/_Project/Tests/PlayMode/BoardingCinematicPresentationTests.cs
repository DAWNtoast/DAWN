using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
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
        [UnityTest]
        public IEnumerator BoardingCinematic_LegacyAssignmentDoesNotHideCommittedCrew()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CrewVariantDefinition doctor = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 2);
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation, crewId: doctor.Id);
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NavalBattleStage.Boarding });
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(doctor.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(doctor.ProfessionIndex), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(doctor.ProfessionIndex), doctor.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(doctor.Id), session.CurrentPort);
            var selected = (List<string>)RamComicField("selectedDepartureSpecialCrew").GetValue(controller);
            selected.Clear();
            selected.Add(doctor.Id);
            typeof(CoreGameplayDemoController).GetMethod("PersistCrewAtSea", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.Mission.v1", 0);
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.RealClock.v1", 1);
            PlayerPrefs.SetString("ShatteredSail.Dispatch.EndsAt.v1", System.DateTime.UtcNow.AddDays(1).Ticks.ToString());
            PlayerPrefs.SetString("ShatteredSail.Dispatch.CrewCsv.v1", "2");
            PlayerPrefs.SetString("ShatteredSail.Dispatch.CrewIdentities.v2", doctor.Id);

            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);

            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            RawImage figure = RequiredResponsive<RawImage>("Player Ship Art Sailor 0");
            Assert.That(figure.texture, Is.SameAs(BoardingBattleArtCatalog.ForCrew(doctor)));
            Assert.That(figure.uvRect, Is.EqualTo(BoardingBattleArtCatalog.CrewUv(doctor)));
            Assert.That(GameObject.Find("Player Ship Art Party Illustration"), Is.Null,
                "A retired assignment must not replace the committed doctor with ordinary sailors.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BoardingCinematic_AllEnemyLegacyAndThirtySixCrewBodiesCompensateTheirOwnTransparentFootMargins()
        {
            var enemyNames = new[] { "party", "imperial", "ghost", "route_reef", "route_gold", "route_moon", "treasure",
                "iron_oath", "thunder_crown", "red_widow", "ember_ark", "tide_requiem" };
            var legacyNames = new[] { "navigator_evelyn", "carpenter_barlow", "doctor_moer", "gunner_leisa", "cook_pepe", "diver_noya" };
            IEnumerable<string> paths = enemyNames.Select(name => "BoardingBattle/Characters/boarding_enemy_" + name + "_v1")
                .Concat(legacyNames.Select(name => "PortArt/CrewFullBody/crew_" + name + "_fullbody"));
            var party = new GameObject("Boarding foot placement fixture", typeof(RectTransform));
            try
            {
                var actor = new GameObject("Grounded actor", typeof(RectTransform), typeof(RawImage));
                actor.transform.SetParent(party.transform, false);
                RawImage figure = actor.GetComponent<RawImage>();
                figure.raycastTarget = false;
                void AssertGroundedBody(Texture2D texture, Rect uv, Vector2 bounds)
                {
                    Assert.That(texture, Is.Not.Null);
                    figure.texture = texture;
                    figure.uvRect = uv;
                    figure.rectTransform.sizeDelta = UiImageAspect.FitSize(texture, uv, bounds);
                    figure.rectTransform.anchoredPosition = new Vector2(73f, 91f);
                    BoardingBattleLayout.GroundFigure(figure);
                    BoardingCinematicAssertions.AssertFullBody(figure, uv);
                    Assert.That(figure.rectTransform.anchoredPosition.x, Is.EqualTo(73f), "Grounding must preserve the formation's horizontal spacing.");
                    Vector2 grounded = figure.rectTransform.anchoredPosition;
                    BoardingBattleLayout.GroundFigure(figure);
                    Assert.That(figure.rectTransform.anchoredPosition, Is.EqualTo(grounded), "Repeated pose restoration must not accumulate a foot offset.");
                }
                foreach (string path in paths)
                {
                    Texture2D texture = Resources.Load<Texture2D>(path);
                    Assert.That(texture, Is.Not.Null, path);
                    AssertGroundedBody(texture, new Rect(0f, 0f, 1f, 1f), BoardingBattleLayout.PartySize);
                }
                Assert.That(CrewNarrativeCatalog.AllCrew.Count, Is.EqualTo(36));
                foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                    AssertGroundedBody(BoardingBattleArtCatalog.ForCrew(crew), BoardingBattleArtCatalog.CrewUv(crew), new Vector2(230f, 330f));
            }
            finally { Object.DestroyImmediate(party); }
        }

        [UnityTest]
        public IEnumerator BoardingCinematic_AllEighteenDecksGroundOrdinaryAndSpecialistBodies()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var terrains = new[] { SeaTerrainType.OpenWater, SeaTerrainType.Beach, SeaTerrainType.Strait,
                    SeaTerrainType.Reef, SeaTerrainType.FogBank, SeaTerrainType.StormFront };
                var ports = new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
                var hours = new[] { 0d, 10d, 14d };
                var times = new[] { NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night };
                var backgrounds = new HashSet<Texture>();
                var identities = new HashSet<string>();
                var selected = (List<string>)RamComicField("selectedDepartureSpecialCrew").GetValue(controller);
                PropertyInfo terrain = typeof(CoreVoyageSession).GetProperty("BattleTerrain");
                MethodInfo persistAtSea = typeof(CoreGameplayDemoController).GetMethod("PersistCrewAtSea", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo enterStage = typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic);

                for (int environment = 0; environment < terrains.Length; environment++)
                    for (int time = 0; time < times.Length; time++)
                    {
                        selected.Clear();
                        CoreVoyageSession ordinary = CreateResponsiveBattle(controller, NavalBattleStage.Preparation, gameHours: hours[time]);
                        terrain.SetValue(ordinary, terrains[environment]);
                        enterStage.Invoke(ordinary, new object[] { NavalBattleStage.Boarding });
                        SetResponsiveHand(ordinary, "card_crew_thrust", "card_crew_guard");
                        RenderResponsiveBattle(controller);
                        yield return WaitResponsiveBattle(controller);
                        Assert.That(NavigationTimeOfDayUtility.FromVoyageHours(ordinary.ElapsedGameHours), Is.EqualTo(times[time]));
                        BoardingCinematicAssertions.AssertScene(ordinary);
                        Assert.That(backgrounds.Add(RequiredResponsive<RawImage>("Battle Scene Art").texture), Is.True,
                            "Each environment/time fixture must inspect its own installed painting.");
                        if (environment == 0 && time == 0)
                            NavalBattleVisualCapture.Capture(controller, "Boarding_Grounded_OpenDay.png", "开阔海域白天：普通船员真实脚底落在绘制甲板上");
                        if (environment == 2 && time == 2)
                            NavalBattleVisualCapture.Capture(controller, "Boarding_Grounded_StraitNight.png", "海峡夜晚：双队脚底位于栏杆前的甲板区域");

                        // Cooks do not provide combat cards and are not selected
                        // by the live boarding roster. The direct grounding test
                        // above still covers their complete authored atlas cells.
                        var crew = (time == 0 ? new[] { 0, 1, 2 } : new[] { 3, 5, 0 })
                            .Select(profession => CrewNarrativeCatalog.ForPortAndProfession(ports[environment], profession)).ToArray();
                        CoreVoyageSession specialists = CreateResponsiveBattle(controller, NavalBattleStage.Preparation,
                            crewIds: crew.Select(member => member.Id).ToArray(), gameHours: hours[time]);
                        terrain.SetValue(specialists, terrains[environment]);
                        enterStage.Invoke(specialists, new object[] { NavalBattleStage.Boarding });
                        SetResponsiveHand(specialists, "card_crew_thrust", "card_crew_guard");
                        foreach (CrewVariantDefinition member in crew)
                        {
                            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(member.Id), 1);
                            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(member.ProfessionIndex), 1);
                            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(member.ProfessionIndex), member.Id);
                            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(member.Id), specialists.CurrentPort);
                            selected.Add(member.Id);
                            identities.Add(member.Id);
                        }
                        persistAtSea.Invoke(controller, null);
                        RenderResponsiveBattle(controller);
                        yield return WaitResponsiveBattle(controller);
                        BoardingCinematicAssertions.AssertScene(specialists, false);
                        Assert.That(GameObject.Find("Player Ship Art Party Illustration"), Is.Null);
                        Assert.That(RequiredResponsive<RectTransform>("Player Ship Art").GetComponentsInChildren<RawImage>(), Has.Length.EqualTo(3),
                            ports[environment] + " / " + times[time] + " / " + string.Join(", ", crew.Select(member => member.Id)));
                        for (int index = 0; index < crew.Length; index++)
                        {
                            RawImage figure = RequiredResponsive<RawImage>("Player Ship Art Sailor " + index);
                            Assert.That(figure.texture, Is.SameAs(BoardingBattleArtCatalog.ForCrew(crew[index])));
                            BoardingCinematicAssertions.AssertFullBody(figure, BoardingBattleArtCatalog.CrewUv(crew[index]));
                        }
                        if (environment == 2 && time == 2)
                            NavalBattleVisualCapture.Capture(controller, "Boarding_Grounded_LowerAtlasCrew.png", "海峡夜晚：图集下排炮手、潜水员与领航员按不透明脚底落地");
                    }
                Assert.That(backgrounds.Count, Is.EqualTo(18));
                Assert.That(identities.Count, Is.EqualTo(30));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator BoardingCinematic_SelectedAtSeaSpecialistsKeepTheirIdentityAndCardsStillCommitOnce()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                string departurePort = new CoreVoyageSession().CurrentPort;
                var crew = new[] { 2, 0, 1 }.Select(p => CrewNarrativeCatalog.ForPortAndProfession(departurePort, p)).ToArray();
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval,
                    crewIds: crew.Select(c => c.Id).ToArray(), gameHours: 34d);
                var selected = (List<string>)RamComicField("selectedDepartureSpecialCrew").GetValue(controller);
                selected.Clear();
                foreach (CrewVariantDefinition member in crew)
                {
                    Assert.That(member, Is.Not.Null);
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(member.Id), 1);
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(member.ProfessionIndex), 1);
                    PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(member.ProfessionIndex), member.Id);
                    PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(member.Id), session.CurrentPort);
                    selected.Add(member.Id);
                }
                // Use the real departure persistence path: staying at the port
                // in this fixture would conceal an at-sea roster regression.
                typeof(CoreGameplayDemoController).GetMethod("PersistCrewAtSea", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                ConfigureCloseNavalTestPose(session.Battle, false);
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                ClickAcceptanceButton("Battle Board");
                yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours), Is.EqualTo(NavigationTimeOfDay.Dusk));
                Assert.That(NavigationTimeOfDayUtility.FromVoyageHours(session.TotalHours), Is.EqualTo(NavigationTimeOfDay.Day),
                    "The scene must use the persistent campaign clock, not a new voyage's reset timer.");
                BoardingCinematicAssertions.AssertScene(session, false);
                Assert.That(GameObject.Find("Player Ship Art Party Illustration"), Is.Null, "Recruited specialists must not be replaced by unnamed ordinary sailors.");
                Assert.That(RequiredResponsive<RectTransform>("Player Ship Art").GetComponentsInChildren<RawImage>(), Has.Length.EqualTo(3));
                for (int index = 0; index < crew.Length; index++)
                {
                    RawImage figure = RequiredResponsive<RawImage>("Player Ship Art Sailor " + index);
                    Assert.That(figure.texture, Is.SameAs(BoardingBattleArtCatalog.ForCrew(crew[index])));
                    BoardingCinematicAssertions.AssertFullBody(figure, BoardingBattleArtCatalog.CrewUv(crew[index]));
                    Assert.That(RequiredResponsive<Text>("Player Ship Art Boarding Crew Role " + index).text, Is.EqualTo(crew[index].DisplayName));
                }

                session.Battle.PlayerBoardingHealth = System.Math.Min(27, session.Battle.PlayerMaxBoardingHealth - 1);
                session.Battle.EnemyBoardingHealth = System.Math.Min(37, session.Battle.EnemyMaxBoardingHealth - 1);
                session.Battle.PlayerBlock = 9;
                session.Battle.EnemyBlock = 1;
                typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
                Assert.That(session.BoardingCriticalChancePercent, Is.Zero, "Isolate one authored hit from the separate critical mechanic.");
                SetResponsiveHand(session, "card_crew_thrust", "card_crew_guard");
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                BoardingCinematicAssertions.AssertScene(session, false);
                NavalBattleVisualCapture.Capture(controller, "Boarding_SpecialCrew.png", "真实海上三名已招募船员：全身身份、非十倍生命值与白色格挡外壳");

                int enemyHealth = session.Battle.EnemyBoardingHealth;
                int damage = session.GetBattleCardEffects(session.Battle.Hand[0], NavalBattleStage.Boarding).Damage;
                int blocked = System.Math.Min(session.Battle.EnemyBlock, damage);
                int energy = session.Battle.Energy;
                int cost = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                int played = session.Battle.CardsPlayedThisTurn;
                int healthBarId = RequiredResponsive<Image>("Enemy Battle Hull Bar").GetInstanceID();
                UnityEvent attack = RequiredResponsive<Button>("Battle Card 0").onClick;
                ClickAcceptanceButton("Battle Card 0");
                attack.Invoke();
                yield return WaitBoardingFeedback(controller, "BoardingFX", BoardingCombatFxKind.Dash, BoardingCombatFxKind.Slash, BoardingCombatFxKind.GuardBreak);
                Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemyHealth - damage + blocked));
                Assert.That(session.Battle.Energy, Is.EqualTo(energy - cost));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(played + 1));
                Assert.That(RequiredResponsive<Image>("Enemy Battle Hull Bar").GetInstanceID(), Is.EqualTo(healthBarId));
                BoardingCinematicAssertions.AssertScene(session, false);
                int guardBefore = session.Battle.PlayerBlock;
                int block = session.GetBattleCardEffects(session.Battle.Hand[0], NavalBattleStage.Boarding).Block;
                ClickAcceptanceButton("Battle Card 0");
                yield return WaitBoardingFeedback(controller, "BoardingFX", BoardingCombatFxKind.Guard);
                Assert.That(session.Battle.PlayerBlock, Is.EqualTo(guardBefore + block));
                Assert.That(RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").DisplayedGuard, Is.EqualTo(session.Battle.PlayerBlock));
                BoardingCinematicAssertions.AssertScene(session, false);
                AssertResponsiveHandMatchesCore(session);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator BoardingCinematic_DisableDuringRevealRestoresTheCommittedSceneWithoutReplaying() => InterruptBoardingReveal(false);

        [UnityTest]
        public IEnumerator BoardingCinematic_ApplicationPauseDuringRevealRestoresTheCommittedSceneWithoutReplaying() => InterruptBoardingReveal(true);

        [UnityTest]
        public IEnumerator BoardingCinematic_DisableDuringCommittedAttackRestoresPoseAndRemovesEffectsWithoutReplaying()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
                session.Battle.Stage = NavalBattleStage.Boarding;
                session.Battle.EnemyBlock = 1;
                typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
                Assert.That(session.BoardingCriticalChancePercent, Is.Zero, "Isolate cancellation from the separate critical mechanic.");
                SetResponsiveHand(session, "card_crew_thrust", "card_crew_guard");
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                Vector2 playerPosition = RequiredResponsive<RectTransform>("Player Ship Art").anchoredPosition;
                Vector2 enemyPosition = RequiredResponsive<RectTransform>("Enemy Ship Art").anchoredPosition;
                Vector3 playerScale = RequiredResponsive<RectTransform>("Player Ship Art").localScale;
                Vector3 enemyScale = RequiredResponsive<RectTransform>("Enemy Ship Art").localScale;
                int enemyHealth = session.Battle.EnemyBoardingHealth;
                int damage = session.GetBattleCardEffects(session.Battle.Hand[0], NavalBattleStage.Boarding).Damage;
                int energy = session.Battle.Energy;
                int cost = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                UnityEvent oldAttack = RequiredResponsive<Button>("Battle Card 0").onClick;
                ClickAcceptanceButton("Battle Card 0");
                float deadline = Time.realtimeSinceStartup + 12f;
                bool sawCommittedHit = false;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    sawCommittedHit = Object.FindObjectsByType<BoardingCombatFxGraphic>(FindObjectsSortMode.None)
                        .Any(fx => fx.Kind == BoardingCombatFxKind.Slash && fx.Progress > 0f && fx.Progress < 1f);
                    if (sawCommittedHit) break;
                    yield return null;
                }
                Assert.That(sawCommittedHit, Is.True, "Interrupt the real post-commit sword impact, not preparation or a mocked effect.");
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
                Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemyHealth - damage + System.Math.Min(1, damage)));
                Assert.That(session.Battle.Energy, Is.EqualTo(energy - cost));
                string committed = RamComicCoreState(session);
                controller.enabled = false;
                yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                oldAttack.Invoke();
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                controller.enabled = true;
                yield return null;
                yield return null;
                oldAttack.Invoke();
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed), "Restoring interrupted hit feedback cannot play the consumed card again.");
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(Object.FindObjectsByType<BoardingCombatFxGraphic>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(Vector2.Distance(RequiredResponsive<RectTransform>("Player Ship Art").anchoredPosition, playerPosition), Is.LessThanOrEqualTo(.01f));
                Assert.That(Vector2.Distance(RequiredResponsive<RectTransform>("Enemy Ship Art").anchoredPosition, enemyPosition), Is.LessThanOrEqualTo(.01f));
                Assert.That(Vector3.Distance(RequiredResponsive<RectTransform>("Player Ship Art").localScale, playerScale), Is.LessThanOrEqualTo(.0001f));
                Assert.That(Vector3.Distance(RequiredResponsive<RectTransform>("Enemy Ship Art").localScale, enemyScale), Is.LessThanOrEqualTo(.0001f));
                BoardingCinematicAssertions.AssertScene(session);
                AssertResponsiveHandMatchesCore(session);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
                int block = session.GetBattleCardEffects(session.Battle.Hand[0], NavalBattleStage.Boarding).Block;
                ClickAcceptanceButton("Battle Card 0");
                yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(2), "The restored remaining hand must still accept one normal action.");
                Assert.That(session.Battle.PlayerBlock, Is.EqualTo(block));
                Assert.That(Object.FindObjectsByType<BoardingCombatFxGraphic>(FindObjectsSortMode.None), Is.Empty);
                BoardingCinematicAssertions.AssertScene(session);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static IEnumerator WaitBoardingFeedback(CoreGameplayDemoController controller, string capturePrefix, params BoardingCombatFxKind[] expected)
        {
            var seen = new HashSet<BoardingCombatFxKind>();
            var captured = new HashSet<BoardingCombatFxKind>();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                foreach (BoardingPartyPresentation body in Object.FindObjectsByType<BoardingPartyPresentation>(FindObjectsSortMode.None))
                {
                    RectTransform pose = (RectTransform)body.transform;
                    Assert.That(pose.anchoredPosition.y, Is.EqualTo(BoardingBattleLayout.PartyPosition(false).y).Within(.01f),
                        "Live sword feedback must keep both parties on the deck.");
                    Assert.That(Vector3.Distance(pose.localScale, Vector3.one), Is.LessThan(.0001f),
                        "A boarding character must not inherit the ship squash animation.");
                    Assert.That(Quaternion.Angle(pose.localRotation, Quaternion.identity), Is.LessThan(.01f),
                        "A boarding character must not inherit ship roll during sword feedback.");
                }
                foreach (BoardingCombatFxGraphic fx in Object.FindObjectsByType<BoardingCombatFxGraphic>(FindObjectsSortMode.None))
                {
                    Assert.That(fx.raycastTarget, Is.False);
                    seen.Add(fx.Kind);
                    if (expected.Contains(fx.Kind) && fx.Progress >= .05f && fx.Progress <= .85f && captured.Add(fx.Kind))
                    {
                        NavalBattleVisualCapture.Capture(controller, capturePrefix + "_" + fx.Kind + ".png",
                            "真实卡牌接舷特效进行中：" + fx.Kind + "；归一化进度 " + fx.Progress.ToString("0.000"));
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "Boarding effects must finish within the existing animation watchdog.");
            foreach (BoardingCombatFxKind kind in expected) Assert.That(seen.Contains(kind), Is.True, "The real card action did not display " + kind);
            foreach (BoardingCombatFxKind kind in expected) Assert.That(captured.Contains(kind), Is.True,
                "The real effect must display a visible intermediate frame for capture: " + kind);
            yield return null;
            Assert.That(Object.FindObjectsByType<BoardingCombatFxGraphic>(FindObjectsSortMode.None), Is.Empty,
                "Finished actions must remove their transient deck effects.");
        }

        private static IEnumerator InterruptBoardingReveal(bool pause)
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = ArrangeRamComicBattle(controller, false, NavalBattleAlignment.EnemyBroadside);
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);
                UnityEvent oldRam = RequiredResponsive<Button>("Battle Ram").onClick;
                ClickAcceptanceButton("Battle Ram");
                yield return WaitForRamComic(controller);
                yield return null;
                UnityEvent oldAdvance = RequiredResponsive<Button>("Battle Ram Comic Advance").onClick;
                yield return ClickThroughRamComic(controller);
                string committed = RamComicCoreState(session);
                float deadline = Time.realtimeSinceStartup + 12f;
                while ((GameObject.Find("Boarding Scene Transition") == null || GameObject.Find("Battle Ram Comic Overlay") != null) &&
                    controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Not.Null);
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.False);
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                MethodInfo pauseMessage = typeof(CoreGameplayDemoController).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pause) pauseMessage.Invoke(controller, new object[] { true });
                else controller.enabled = false;
                yield return null;
                Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Null);
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                if (pause) pauseMessage.Invoke(controller, new object[] { false });
                else controller.enabled = true;
                yield return null;
                yield return null;
                Assert.DoesNotThrow(() => oldAdvance.Invoke());
                oldRam.Invoke();
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
                BoardingCinematicAssertions.AssertScene(session);
                AssertResponsiveHandMatchesCore(session);
                LogAssert.NoUnexpectedReceived();
            }
        }
    }

    internal static class BoardingCinematicAssertions
    {
        private static readonly Dictionary<Texture, Dictionary<Rect, Vector2>> VisibleFeet =
            new Dictionary<Texture, Dictionary<Rect, Vector2>>();

        public static void AssertScene(CoreVoyageSession session, bool ordinaryPlayer = true)
        {
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            RawImage background = Required<RawImage>("Battle Scene Art");
            Texture2D expected = BoardingBattleArtCatalog.ForEnvironment(session.BattleTerrain,
                NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours));
            Assert.That(expected, Is.Not.Null, "The actual battle's environment and campaign time require an installed painting.");
            Assert.That(background.texture, Is.SameAs(expected));
            Assert.That(background.uvRect, Is.EqualTo(BoardingBattleLayout.SceneryUv(expected, background.rectTransform.rect.size)));
            RawImage joinedDeck = Required<RawImage>("Boarding Joined Deck");
            Assert.That(joinedDeck.texture, Is.Not.Null, "The shared boarding floor must use its installed deck painting.");
            Assert.That(joinedDeck.texture, Is.Not.SameAs(background.texture), "Environment scenery and joined deck have independent framing.");
            Assert.That(joinedDeck.raycastTarget, Is.False);
            Assert.That(joinedDeck.color.a, Is.GreaterThanOrEqualTo(.99f));
            Assert.That(joinedDeck.transform.parent, Is.SameAs(background.transform.parent));
            Assert.That(joinedDeck.transform.GetSiblingIndex(), Is.GreaterThan(background.transform.GetSiblingIndex()),
                "The opaque deck must cover the old scenery floor while its transparent horizon retains the environment.");
            foreach (bool enemy in new[] { false, true })
            {
                string side = enemy ? "Enemy" : "Player";
                RectTransform party = Required<RectTransform>(side + " Ship Art");
                Assert.That(party.GetComponent<BoardingPartyPresentation>(), Is.Not.Null,
                    "Boarding animation dispatch must survive removal of the decorative formation label.");
                Assert.That(party.parent, Is.SameAs(joinedDeck.transform.parent));
                Assert.That(party.GetSiblingIndex(), Is.GreaterThan(joinedDeck.transform.GetSiblingIndex()));
                // The optional live capture changes the canvas to world space
                // and restores it; subpixel transform round trips are expected.
                Assert.That(Vector2.Distance(party.anchoredPosition, BoardingBattleLayout.PartyPosition(enemy)), Is.LessThanOrEqualTo(.01f));
                Assert.That(Vector3.Distance(party.localScale, Vector3.one), Is.LessThanOrEqualTo(.0001f));
                Assert.That(party.rect.height, Is.EqualTo(BoardingBattleLayout.PartySize.y).Within(.01f),
                    "Full-body actors follow the shared layout while reserving room for health and status icons.");
                Assert.That(party.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                string role = enemy == session.Battle.BoardingInitiatedByEnemy ? "登船队" : "守备队";
                Assert.That(Required<Text>(side + " Battle Name").text, Does.Contain(role),
                    "The compact name board carries the formation role without duplicating a label over the actors.");
                Assert.That(GameObject.Find(side + " Ship Art Formation Label"), Is.Null);
                if (enemy || ordinaryPlayer)
                {
                    RawImage image = Required<RawImage>(side + " Ship Art Party Illustration");
                    Assert.That(image.texture, Is.SameAs(enemy
                        ? BoardingBattleArtCatalog.ForEnemy(session.Battle, session.ActiveEncounter, session.SelectedRouteIndex)
                        : BoardingBattleArtCatalog.ForParty(false)));
                    AssertFullBody(image);
                    Assert.That(party.GetComponentsInChildren<RawImage>(), Has.Length.EqualTo(1));
                }
                foreach (RawImage figure in party.GetComponentsInChildren<RawImage>())
                {
                    AssertFeetOnDeck(figure);
                    Graphic contact = Required<Graphic>(figure.name + " Deck Contact Shadow");
                    Assert.That(contact.transform.parent, Is.SameAs(figure.transform.parent), "Contact shadows must travel with their own party.");
                    Assert.That(contact.transform.GetSiblingIndex(), Is.LessThan(figure.transform.GetSiblingIndex()));
                    Assert.That(contact.raycastTarget, Is.False);
                }
                BattleDefenseHud hud = Required<BattleDefenseHud>(side + " Battle Hull Bar Track");
                int maximum = enemy ? session.Battle.EnemyMaxBoardingHealth : session.Battle.PlayerMaxBoardingHealth;
                int current = enemy ? session.Battle.EnemyBoardingHealth : session.Battle.PlayerBoardingHealth;
                Assert.That(hud.MaximumHull, Is.EqualTo(maximum));
                Assert.That(hud.DisplayedHull, Is.EqualTo(current));
                Assert.That(hud.HullBar.fillAmount, Is.EqualTo(current / (float)maximum).Within(.0001f));
                Assert.That(hud.DisplayedGuard, Is.EqualTo(enemy ? session.Battle.EnemyBlock : session.Battle.PlayerBlock));
                Assert.That(hud.PermanentArmor, Is.Zero, "Ship armor must not become crew health or crew armor.");
                BattleHealthSegmentsGraphic[] grid = hud.HullBar.GetComponentsInChildren<BattleHealthSegmentsGraphic>();
                Assert.That(grid, Is.Empty, "The continuous health bar must not create ten-health divider overlays.");
                Assert.That(hud.HullBar.rectTransform.rect.height, Is.GreaterThanOrEqualTo(20f));
            }
            Assert.That(GameObject.Find("Player Boarding Deck"), Is.Null);
            Assert.That(GameObject.Find("Boarding Gangplank"), Is.Null);
            Assert.That(GameObject.Find("Player Ship Art Boarding Crew Token 0"), Is.Null);
        }

        public static void AssertFullBody(RawImage figure, Rect? expectedUv = null)
        {
            Assert.That(figure.texture, Is.Not.Null);
            Rect source = expectedUv ?? new Rect(0f, 0f, 1f, 1f);
            Assert.That(figure.uvRect, Is.EqualTo(source), "Every figure must use the whole authored body or its complete atlas cell.");
            Assert.That(figure.rectTransform.rect.width / figure.rectTransform.rect.height,
                Is.EqualTo(figure.texture.width * source.width / (figure.texture.height * source.height)).Within(.0001f));
            Assert.That(figure.raycastTarget, Is.False);
            AssertFeetOnDeck(figure, false);
        }

        private static void AssertFeetOnDeck(RawImage figure, bool verifyPaintedDeck = true)
        {
            // Inspect visible source pixels independently of the production
            // placement metadata: a transparent image's rectangle is not its foot.
            Vector2 foot = VisibleFoot(figure.texture, figure.uvRect);
            Rect bounds = figure.rectTransform.rect;
            Vector3 world = figure.rectTransform.TransformPoint(new Vector3(
                Mathf.Lerp(bounds.xMin, bounds.xMax, foot.x), Mathf.Lerp(bounds.yMin, bounds.yMax, foot.y), 0f));
            Vector3 partyLocal = figure.transform.parent.InverseTransformPoint(world);
            Assert.That(partyLocal.y, Is.EqualTo(-BoardingBattleLayout.PartySize.y * .5f).Within(2f),
                figure.name + " / " + figure.texture.name + " / " + figure.uvRect +
                " must place its opaque feet on the shared floor, including atlas-cell padding.");
            if (!verifyPaintedDeck) return;

            RawImage background = Required<RawImage>("Boarding Joined Deck");
            Rect deck = background.rectTransform.rect;
            Vector3 deckLocal = background.rectTransform.InverseTransformPoint(world);
            Assert.That(deck.Contains(new Vector2(deckLocal.x, deckLocal.y)), Is.True,
                figure.name + " must remain inside the shared deck plane.");
            Vector2 source = new Vector2(
                Mathf.Lerp(background.uvRect.xMin, background.uvRect.xMax, (deckLocal.x - deck.xMin) / deck.width),
                Mathf.Lerp(background.uvRect.yMin, background.uvRect.yMax, (deckLocal.y - deck.yMin) / deck.height));
            Assert.That(PaintedDeckAlpha(background.texture, source), Is.GreaterThanOrEqualTo(220),
                figure.name + " must stand on opaque painted deck, not the transparent horizon or the seam between ships.");
        }

        private static readonly Dictionary<Texture, Color32[]> DeckPixels = new Dictionary<Texture, Color32[]>();

        private static byte PaintedDeckAlpha(Texture texture, Vector2 uv)
        {
            Assert.That(texture, Is.Not.Null);
            if (!DeckPixels.TryGetValue(texture, out Color32[] pixels))
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                try
                {
                    Graphics.Blit(texture, copy);
                    RenderTexture.active = copy;
                    readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                    readable.Apply();
                    pixels = readable.GetPixels32();
                    DeckPixels.Add(texture, pixels);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(copy);
                    Object.DestroyImmediate(readable);
                }
            }
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * texture.width), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * texture.height), 0, texture.height - 1);
            byte lowestAlpha = 255;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                    lowestAlpha = System.Math.Min(lowestAlpha, pixels[
                        Mathf.Clamp(y + offsetY, 0, texture.height - 1) * texture.width + Mathf.Clamp(x + offsetX, 0, texture.width - 1)].a);
            return lowestAlpha;
        }

        private static Vector2 VisibleFoot(Texture texture, Rect uv)
        {
            if (!VisibleFeet.TryGetValue(texture, out Dictionary<Rect, Vector2> cells))
            {
                cells = new Dictionary<Rect, Vector2>();
                VisibleFeet.Add(texture, cells);
            }
            if (cells.TryGetValue(uv, out Vector2 cached)) return cached;
            RenderTexture previous = RenderTexture.active;
            RenderTexture copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, copy);
                RenderTexture.active = copy;
                readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readable.Apply();
                Color32[] pixels = readable.GetPixels32();
                int left = Mathf.Clamp(Mathf.CeilToInt(uv.xMin * texture.width - .5f), 0, texture.width - 1);
                int right = Mathf.Clamp(Mathf.CeilToInt(uv.xMax * texture.width - .5f), left + 1, texture.width);
                int bottom = Mathf.Clamp(Mathf.CeilToInt(uv.yMin * texture.height - .5f), 0, texture.height - 1);
                int top = Mathf.Clamp(Mathf.CeilToInt(uv.yMax * texture.height - .5f), bottom + 1, texture.height);
                for (int y = bottom; y < top - 2; y++)
                {
                    int first = -1, last = -1;
                    int count = 0, run = 0, longest = 0;
                    for (int x = left; x < right; x++)
                    {
                        if (pixels[y * texture.width + x].a < 64) { run = 0; continue; }
                        if (first < 0) first = x;
                        last = x;
                        count++;
                        longest = Mathf.Max(longest, ++run);
                    }
                    // Tiny disconnected alpha flecks below boots are not body
                    // contact. Require a supported footprint across three rows.
                    if (count < 8 || longest < 6 || !HasBootRow(pixels, texture.width, left, right, y + 1) ||
                        !HasBootRow(pixels, texture.width, left, right, y + 2)) continue;
                    var foot = new Vector2(((first + last + 1f) * .5f / texture.width - uv.xMin) / uv.width,
                        ((y + .5f) / texture.height - uv.yMin) / uv.height);
                    cells.Add(uv, foot);
                    return foot;
                }
                Assert.Fail(texture.name + " contains no visible feet within " + uv);
                return Vector2.zero;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(copy);
                Object.DestroyImmediate(readable);
            }
        }

        private static bool HasBootRow(Color32[] pixels, int width, int left, int right, int y)
        {
            int count = 0, run = 0, longest = 0;
            for (int x = left; x < right; x++)
            {
                if (pixels[y * width + x].a < 64) { run = 0; continue; }
                count++;
                longest = Mathf.Max(longest, ++run);
            }
            return count >= 8 && longest >= 6;
        }

        private static T Required<T>(string name) where T : Component
        {
            GameObject host = GameObject.Find(name);
            Assert.That(host, Is.Not.Null, name);
            T component = host.GetComponent<T>();
            Assert.That(component, Is.Not.Null, name + " / " + typeof(T).Name);
            return component;
        }
    }
}
