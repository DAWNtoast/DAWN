using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator RamComic_PlayerHeadOn_ClicksFourFullscreenPanelsBeforeBoarding() =>
            ExerciseRamComic(false, NavalBattleAlignment.HeadOn);

        [UnityTest]
        public IEnumerator RamComic_PlayerBroadside_ClicksKeepOneCommittedRamAndPlayerInitiative() =>
            ExerciseRamComic(false, NavalBattleAlignment.EnemyBroadside);

        [UnityTest]
        public IEnumerator RamComic_BossHeadOn_ClicksFourPanelsAndAppliesExactlyOneOpening() =>
            ExerciseRamComic(true, NavalBattleAlignment.HeadOn);

        [UnityTest]
        public IEnumerator RamComic_BossBroadside_ClicksKeepOneOpeningAndEnemyInitiative() =>
            ExerciseRamComic(true, NavalBattleAlignment.EnemyBroadside);

        [Test]
        public void RamComic_BossParallel_SelectsTheBroadsideResource()
        {
            var host = new GameObject("Ram Comic Classification Fixture");
            host.SetActive(false); // No gameplay Awake, scene load or save is needed for resource classification.
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                MethodInfo select = typeof(CoreGameplayDemoController).GetMethod("RamComicTexture", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(select, Is.Not.Null);
                var expected = Resources.Load<Texture2D>(RamComicCatalog.ImageResource(true, NavalBattleAlignment.EnemyBroadside, 0));
                Assert.That(expected, Is.Not.Null, "The authored enemy broadside page is required.");
                Assert.That(select.Invoke(controller, new object[] { true, NavalBattleAlignment.Parallel }), Is.SameAs(expected));
                Assert.That(select.Invoke(controller, new object[] { true, NavalBattleAlignment.EnemyBroadside }), Is.SameAs(expected));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator RamComic_DisableAfterCommit_CleansOverlayAndRestoresBoardingWithoutReplay()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = ArrangeRamComicBattle(controller, false, NavalBattleAlignment.HeadOn);
                RenderResponsiveBattle(controller);
                yield return null;
                UnityEvent oldRam = RequiredResponsive<Button>("Battle Ram").onClick;
                ClickAcceptanceButton("Battle Ram");
                yield return WaitForRamComic(controller);
                UnityEvent oldAdvance = RequiredResponsive<Button>("Battle Ram Comic Advance").onClick;
                string committed = RamComicCoreState(session);

                controller.enabled = false;
                yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                controller.enabled = true;
                yield return null;
                yield return null;
                oldAdvance.Invoke();
                oldRam.Invoke();
                yield return null;

                Assert.That(RamComicCoreState(session), Is.EqualTo(committed), "Cancelled presentation must never commit the ram again.");
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                BoardingCinematicAssertions.AssertScene(session);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
                AssertResponsiveHandMatchesCore(session);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator RamComic_LethalRamAndFatalEnemyOpening_KeepTheRealVictoryOrFailure()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                foreach (bool enemy in new[] { false, true })
                {
                    yield return LoadResponsiveBattleScene();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    CoreVoyageSession session = ArrangeRamComicBattle(controller, enemy, NavalBattleAlignment.HeadOn, enemy ? 500 : 7);
                    if (!enemy) session.Battle.EnemyHull = 1;
                    RenderResponsiveBattle(controller);
                    yield return null;
                    ClickAcceptanceButton(enemy ? "End Battle Turn" : "Battle Ram");
                    yield return WaitForRamComic(controller);
                    Assert.That(session.Battle, Is.Null, "A lethal outcome must be committed before the cosmetic page is shown.");
                    Assert.That(session.Phase, Is.EqualTo(enemy ? CoreDemoPhase.Settlement : CoreDemoPhase.Sailing));
                    if (enemy) Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                    string committed = RamComicCoreState(session);
                    int panelCount = enemy ? 4 : 2;
                    for (int panel = 0; panel < panelCount; panel++)
                    {
                        Texture2D expected = Resources.Load<Texture2D>(RamComicCatalog.ImageResource(enemy, NavalBattleAlignment.HeadOn, panel));
                        AssertRamComicPresentation(controller, expected, panel);
                        yield return new WaitForSecondsRealtime(1f);
                        AssertRamComicPresentation(controller, expected, panel);
                        Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                        ClickRamComicScreen(new Vector2(.5f, .5f));
                        if (panel + 1 < panelCount) yield return WaitForRamComicPanel(controller, panel + 1);
                    }
                    yield return WaitResponsiveBattle(controller);
                    Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                    Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                    Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Null, "The comic must not manufacture a boarding transition after a lethal result.");
                    Assert.That(GameObject.Find("Player Ship Art Party Illustration"), Is.Null);
                    Assert.That(session.TryRam(), Is.False);
                    LogAssert.NoUnexpectedReceived();
                }
            }
        }

        [UnityTest]
        public IEnumerator RamComic_MissingResource_UsesPhysicalFallbackAndStillFinishesBothInitiators()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                foreach (bool enemy in new[] { false, true })
                {
                    yield return LoadResponsiveBattleScene();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    CoreVoyageSession session = ArrangeRamComicBattle(controller, enemy, NavalBattleAlignment.HeadOn);
                    // Cover a missing first page and a missing later page: both
                    // must reject the sequence before displaying any artwork.
                    int index = RamComicCatalog.SequenceIndex(enemy, NavalBattleAlignment.HeadOn) * RamComicCatalog.PagesPerSequence + (enemy ? 2 : 0);
                    ((bool[])RamComicField("ramComicLoadAttempted").GetValue(controller))[index] = true;
                    ((Texture2D[])RamComicField("ramComicTextures").GetValue(controller))[index] = null;
                    RenderResponsiveBattle(controller);
                    yield return null;
                    RectTransform ship = RequiredResponsive<RectTransform>(enemy ? "Enemy Ship Art" : "Player Ship Art");
                    Vector2 start = ship.anchoredPosition;
                    ClickAcceptanceButton(enemy ? "End Battle Turn" : "Battle Ram");
                    Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                    bool sawMotion = false;
                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                    {
                        Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null, "A cached missing page must use the existing physical feedback.");
                        if (ship != null) sawMotion |= Vector2.Distance(start, ship.anchoredPosition) > .5f;
                        yield return null;
                    }
                    Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                    Assert.That(sawMotion, Is.True, "Missing art must keep an actual physical collision animation.");
                    Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                    Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.EqualTo(enemy));
                    BoardingCinematicAssertions.AssertScene(session);
                    Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
                    LogAssert.NoUnexpectedReceived();
                }
            }
        }

        private static IEnumerator ExerciseRamComic(bool enemy, NavalBattleAlignment alignment)
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = ArrangeRamComicBattle(controller, enemy, alignment);
                CoreBattleState battle = session.Battle;
                int hull = session.Hull, enemyHull = battle.EnemyHull, crew = battle.PlayerBoardingHealth, turn = battle.Turn;
                int opening = enemy ? session.EnemyRamFirstStrikeDamage : 0;
                RenderResponsiveBattle(controller);
                yield return null;
                Button underlyingTurn = RequiredResponsive<Button>("End Battle Turn");
                UnityEvent oldTurn = underlyingTurn.onClick;
                UnityEvent oldRam = RequiredResponsive<Button>("Battle Ram").onClick;
                UnityEvent oldCard = RequiredResponsive<Button>("Battle Card 0").onClick;
                ClickAcceptanceButton(enemy ? "End Battle Turn" : "Battle Ram");
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                yield return WaitForRamComic(controller);
                bool headOn = alignment == NavalBattleAlignment.HeadOn;
                string path = RamComicCatalog.ImageResource(enemy, alignment, 0);
                Texture2D expected = Resources.Load<Texture2D>(path);
                Assert.That(expected, Is.Not.Null, path);
                AssertRamComicPresentation(controller, expected, 0);
                Assert.That(session.Hull, Is.LessThan(hull));
                Assert.That(battle.EnemyHull, Is.LessThan(enemyHull));
                int expectedBoardingHealth = (int)System.Math.Ceiling(crew *
                    (1d - System.Math.Max(0, hull - session.Hull) / (2d * session.MaxHull)));
                Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(expectedBoardingHealth - opening));
                Assert.That(battle.Turn, Is.EqualTo(turn + (enemy ? 1 : 0)));
                Assert.That(battle.BoardingInitiatedByEnemy, Is.EqualTo(enemy));
                Assert.That(battle.BoardingConnection, Is.EqualTo(headOn ? NavalBoardingConnection.RamBowToBow : NavalBoardingConnection.RamBowToBroadside));
                string committed = RamComicCoreState(session);
                AssertRamComicControlsLocked(underlyingTurn);
                oldTurn.Invoke(); oldRam.Invoke(); oldCard.Invoke(); oldTurn.Invoke();
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed), "Callback guards must work immediately, before the new canvas has rendered.");
                // Enemy intent finishes on a coroutine continuation. The comic
                // may exist here with native Graphic.depth == -1 until this
                // frame renders, so perform the real pointer test next frame.
                yield return null;
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed), "Already queued callbacks cannot submit a second Core action under the comic.");
                AssertRamComicInputBlocked(underlyingTurn);
                UnityEvent oldAdvance = RequiredResponsive<Button>("Battle Ram Comic Advance").onClick;
                // Pages remain readable before the four-second timeout. Alternate
                // real pointer clicks with short remaining intervals to cover both paths.
                Vector2[] clickPositions = { new Vector2(.01f, .01f), new Vector2(.99f, .01f),
                    new Vector2(.01f, .99f), new Vector2(.99f, .99f) };
                for (int panel = 0; panel < 4; panel++)
                {
                    expected = Resources.Load<Texture2D>(RamComicCatalog.ImageResource(enemy, alignment, panel));
                    AssertRamComicPresentation(controller, expected, panel);
                    ComicAutoAdvance timer = RequiredResponsive<ComicAutoAdvance>("Battle Ram Comic Overlay");
                    Assert.That(timer.RemainingSeconds, Is.InRange(2.5f, 4f));
                    yield return new WaitForSecondsRealtime(.15f);
                    AssertRamComicPresentation(controller, expected, panel);
                    Assert.That(controller.IsBattleAnimationPlaying, Is.True, "The page remains open before its reading interval expires.");
                    Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Null);
                    Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                    if (!enemy && headOn && panel == 1)
                    {
                        typeof(CoreGameplayDemoController).GetMethod("ReleaseArtReferences", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(controller, null);
                        Assert.That(((Texture2D[])RamComicField("ramComicTextures").GetValue(controller)).All(texture => texture == null), Is.True);
                        // Subsequent pages must still display when routine memory
                        // collection drops the borrowed resource cache mid-comic.
                        AssertRamComicPresentation(controller, expected, panel);
                    }
                    if ((panel == 1 || panel == 3) && !enemy && headOn)
                    {
                        timer.RestoreRemainingSeconds(.06f);
                        float autoDeadline = Time.realtimeSinceStartup + 2f;
                        while (!(bool)RamComicField("ramComicAdvanceRequested").GetValue(controller) &&
                            (int)RamComicField("ramComicPanelIndex").GetValue(controller) == panel &&
                            Time.realtimeSinceStartup < autoDeadline) yield return null;
                        Assert.That((bool)RamComicField("ramComicAdvanceRequested").GetValue(controller) ||
                            (int)RamComicField("ramComicPanelIndex").GetValue(controller) != panel, Is.True,
                            "A page, including the final panel, must finish without a click when its reading time expires.");
                    }
                    else
                    {
                        // A deadline and pointer event in the same frame must
                        // produce one page advance, with a fresh four seconds.
                        timer.RestoreRemainingSeconds(0f);
                        ClickRamComicScreen(clickPositions[panel]);
                    }
                    if (panel + 1 < 4)
                    {
                        yield return WaitForRamComicPanel(controller, panel + 1);
                        yield return null;
                        AssertRamComicPresentation(controller,
                            Resources.Load<Texture2D>(RamComicCatalog.ImageResource(enemy, alignment, panel + 1)), panel + 1);
                    }
                }

                bool sawTransition = false, sawCoveredFrame = false, sawRevealWithLockedInput = false;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    GameObject overlay = GameObject.Find("Battle Ram Comic Overlay");
                    if (overlay != null)
                    {
                        RawImage page = RequiredResponsive<RawImage>("Battle Ram Comic Page");
                        Assert.That(page.texture, Is.SameAs(expected));
                        Assert.That((int)RamComicField("ramComicPanelIndex").GetValue(controller), Is.EqualTo(3),
                            "Finishing the last page must not show an extra page.");
                        Assert.That(underlyingTurn.interactable, Is.False);
                    }
                    GameObject transition = GameObject.Find("Boarding Scene Transition");
                    if (transition != null)
                    {
                        sawTransition = true;
                        BoardingSceneWipeGraphic wipe = RequiredResponsive<BoardingSceneWipeGraphic>("Boarding Transition Wipe");
                        Assert.That(wipe.Progress, Is.InRange(0f, 1f));
                        sawCoveredFrame |= wipe.Progress == 1f;
                        if (overlay == null)
                        {
                            sawRevealWithLockedInput = true;
                            AssertBoardingTransitionInputBlocked(transition.transform);
                            // The real boarding hierarchy already exists here,
                            // but neither old nor new callbacks may act before
                            // the brief reveal has finished.
                            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
                            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                            oldTurn.Invoke(); oldRam.Invoke(); oldCard.Invoke(); oldAdvance.Invoke();
                        }
                    }
                    Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The existing 12 second animation watchdog must remain sufficient.");
                Assert.That(sawTransition, Is.True, "Finishing the final page enters the diagonal scene transition.");
                Assert.That(sawCoveredFrame, Is.True, "The scene swap must occur behind one fully covered frame.");
                Assert.That(sawRevealWithLockedInput, Is.True, "The comic must retire while the actual boarding scene is still locked beneath the reveal.");
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
                Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Null);
                BoardingCinematicAssertions.AssertScene(session);
                Assert.That(GameObject.Find("Battle Ram"), Is.Null);
                yield return null; // Finish deferred destruction of the old overlay.
                FieldInfo playingField = RamComicField("battleAnimationPlaying");
                bool previousPlaying = (bool)playingField.GetValue(controller);
                try
                {
                    // A later non-comic animation may hold this same lock. The
                    // retired Unity object must still be rejected as fake-null.
                    playingField.SetValue(controller, true);
                    Assert.DoesNotThrow(() => oldAdvance.Invoke(),
                        "A destroyed comic's stale advance callback must stay harmless while another battle animation is active.");
                }
                finally { playingField.SetValue(controller, previousPlaying); }
                yield return null;
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed), "A stale advance cannot replay the collision.");
                Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
                AssertResponsiveHandMatchesCore(session);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static CoreVoyageSession ArrangeRamComicBattle(CoreGameplayDemoController controller, bool enemy,
            NavalBattleAlignment alignment, int melee = 7)
        {
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            CoreBattleState battle = session.Battle;
            var profile = new CoreBattleProfile(enemy ? CoreBossKind.Legendary : CoreBossKind.None, CoreBattleArchetype.Raider,
                battle.EnemyMaxHull, 1, melee, 0, 0, battle.EnemyOrdinaryCrew, 1f, 1f, enemy ? 1d : 0d, 120f);
            typeof(CoreBattleState).GetProperty("Profile").SetValue(battle, profile);
            battle.Alignment = alignment;
            battle.DistanceMeters = 80f;
            battle.PlayerMovingTowardEnemy = !enemy;
            battle.EnemyBurnDamage = 0;
            battle.EnemyBurnTurns = 0;
            SetResponsiveHand(session, "card_brace", "card_emergency_repair");
            Assert.That(enemy ? session.EnemyWillRam : session.CanRam(out _), Is.True);
            return session;
        }

        private static IEnumerator WaitForRamComic(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (GameObject.Find("Battle Ram Comic Overlay") == null && controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Not.Null, "A valid ram with installed art must show the comic.");
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
        }

        private static void AssertRamComicControlsLocked(Button underneath)
        {
            Assert.That(underneath.interactable, Is.False);
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Assert.That(RequiredResponsive<Button>("Battle Ram Comic Advance").IsInteractable(), Is.True);
        }

        private static void AssertBoardingTransitionInputBlocked(Transform transition)
        {
            Button endTurn = RequiredResponsive<Button>("End Battle Turn");
            Assert.That(endTurn.interactable, Is.False);
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, endTurn.transform.position),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform == transition || hits[0].gameObject.transform.IsChildOf(transition), Is.True,
                "The rendered transition must intercept actual pointer input while the boarding controls are underneath it. First hit: " + hits[0].gameObject.name);
        }

        private static void AssertRamComicInputBlocked(Button underneath)
        {
            AssertRamComicControlsLocked(underneath);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = underneath.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)underneath.transform;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Transform overlay = RequiredResponsive<RectTransform>("Battle Ram Comic Overlay");
            Assert.That(hits[0].gameObject.transform == overlay || hits[0].gameObject.transform.IsChildOf(overlay), Is.True,
                "The full-page comic must intercept real pointer input above the retained battle HUD.\n" +
                RamComicRaycastDiagnostic(underneath, overlay, pointer.position, hits));
        }

        private static string RamComicRaycastDiagnostic(Button underneath, Transform overlay,
            Vector2 pointer, List<RaycastResult> hits)
        {
            Image image = overlay.GetComponent<Image>();
            var lines = new List<string>
            {
                "frame=" + Time.frameCount + "; realtime=" + Time.realtimeSinceStartup + "; pointer=" + pointer +
                "; screen=" + Screen.width + "x" + Screen.height,
                "underneath=" + RamComicObjectPath(underneath.transform) + "; " + RamComicCanvasDiagnostic(underneath.transform),
                "overlay=" + RamComicObjectPath(overlay) + "; activeSelf=" + overlay.gameObject.activeSelf +
                "; activeInHierarchy=" + overlay.gameObject.activeInHierarchy + "; " + RamComicCanvasDiagnostic(overlay),
                "overlayImage enabled=" + image.enabled + "; depth=" + image.depth +
                "; absoluteDepth=" + image.canvasRenderer.absoluteDepth + "; relativeDepth=" + image.canvasRenderer.relativeDepth +
                "; cull=" + image.canvasRenderer.cull + "; raycastTarget=" + image.raycastTarget +
                "; rect=" + image.rectTransform.rect + "; worldPosition=" + overlay.position + "; color=" + image.color,
            };
            for (int i = 0; i < hits.Count; i++)
            {
                RaycastResult hit = hits[i];
                Graphic graphic = hit.gameObject.GetComponent<Graphic>();
                lines.Add("hit[" + i + "] name=" + hit.gameObject.name + "; path=" + RamComicObjectPath(hit.gameObject.transform) +
                    "; sortingLayer=" + hit.sortingLayer + "; sortingOrder=" + hit.sortingOrder + "; depth=" + hit.depth +
                    "; index=" + hit.index + "; distance=" + hit.distance + "; module=" + (hit.module == null ? "null" : hit.module.GetType().Name) +
                    "; graphicDepth=" + (graphic == null ? "none" : graphic.depth.ToString()) +
                    "; graphicCull=" + (graphic == null ? "none" : graphic.canvasRenderer.cull.ToString()) +
                    "; " + RamComicCanvasDiagnostic(hit.gameObject.transform));
            }
            return string.Join("\n", lines);
        }

        private static string RamComicCanvasDiagnostic(Transform target) => string.Join(" | ",
            target.GetComponentsInParent<Canvas>(true).Select(canvas => "canvas=" + canvas.name +
                ", active=" + canvas.isActiveAndEnabled + ", mode=" + canvas.renderMode +
                ", overrideSorting=" + canvas.overrideSorting + ", sortingLayer=" + canvas.sortingLayerID +
                ", sortingOrder=" + canvas.sortingOrder + ", renderOrder=" + canvas.renderOrder));

        private static string RamComicObjectPath(Transform target)
        {
            var names = new List<string>();
            for (Transform current = target; current != null; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static IEnumerator WaitForRamComicPanel(CoreGameplayDemoController controller, int expectedPanel)
        {
            float deadline = Time.realtimeSinceStartup + 1f;
            while ((int)RamComicField("ramComicPanelIndex").GetValue(controller) != expectedPanel &&
                GameObject.Find("Battle Ram Comic Overlay") != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That((int)RamComicField("ramComicPanelIndex").GetValue(controller), Is.EqualTo(expectedPanel),
                "One click must advance exactly one page.");
        }

        private static void AssertRamComicPresentation(CoreGameplayDemoController controller, Texture2D expectedTexture, int panel)
        {
            Assert.That(expectedTexture, Is.Not.Null, "Each collision panel must have its own installed full-resolution image.");
            Canvas.ForceUpdateCanvases();
            RectTransform overlay = RequiredResponsive<RectTransform>("Battle Ram Comic Overlay");
            RawImage page = RequiredResponsive<RawImage>("Battle Ram Comic Page");
            Button advance = RequiredResponsive<Button>("Battle Ram Comic Advance");
            Assert.That(page.texture, Is.SameAs(expectedTexture));
            Assert.That((int)RamComicField("ramComicPanelIndex").GetValue(controller), Is.EqualTo(panel));
            Assert.That(overlay.GetComponentsInChildren<Text>(true), Is.Empty, "The comic must contain only artwork, without captions, title or page count.");
            Assert.That(overlay.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1));
            foreach (string retired in new[] { "Battle Ram Comic Title", "Battle Ram Comic Caption", "Battle Ram Comic Step", "Battle Ram Comic Skip" })
                Assert.That(GameObject.Find(retired), Is.Null, retired + " must be removed.");
            Assert.That(advance.IsInteractable(), Is.True);
            AssertFullscreenComicRect(overlay);
            AssertFullscreenComicRect(page.rectTransform);
            AssertFullscreenComicRect((RectTransform)advance.transform);

            Rect source = new Rect(0f, 0f, 1f, 1f);
            float targetAspect = page.rectTransform.rect.width / page.rectTransform.rect.height;
            float sourceAspect = expectedTexture.width * source.width / (expectedTexture.height * source.height);
            Vector2 uvSize = source.size;
            if (sourceAspect > targetAspect) uvSize.x *= targetAspect / sourceAspect;
            else uvSize.y *= sourceAspect / targetAspect;
            Rect expected = new Rect(source.center - uvSize * .5f, uvSize);
            Rect actual = page.uvRect;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.0001f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(.0001f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(.0001f));
            Assert.That(expectedTexture.width * actual.width / (expectedTexture.height * actual.height),
                Is.EqualTo(targetAspect).Within(.0001f), "Cover cropping must fill the screen without stretching the artwork.");
        }

        private static void AssertFullscreenComicRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            FixedGameViewport.Bounds(canvas).GetWorldCorners(corners);
            Vector2 viewportMinimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 viewportMaximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            Assert.That(minimum.x, Is.EqualTo(viewportMinimum.x).Within(1f), rect.name);
            Assert.That(minimum.y, Is.EqualTo(viewportMinimum.y).Within(1f), rect.name);
            Assert.That(maximum.x, Is.EqualTo(viewportMaximum.x).Within(1f), rect.name);
            Assert.That(maximum.y, Is.EqualTo(viewportMaximum.y).Within(1f), rect.name);
        }

        private static void ClickRamComicScreen(Vector2 normalizedPosition)
        {
            Button advance = RequiredResponsive<Button>("Battle Ram Comic Advance");
            Assert.That(advance.IsInteractable(), Is.True);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = advance.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform viewport = FixedGameViewport.Bounds(canvas);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 localPoint = viewport.rect.min + Vector2.Scale(viewport.rect.size, normalizedPosition);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(localPoint)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "Every point on the comic screen must accept a click.");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(advance),
                "The full-screen advance target must intercept the pointer at " + normalizedPosition + "; first hit=" + hits[0].gameObject.name);
            ExecuteEvents.Execute(advance.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        // Ordinary route tests still need to supply the player's new page clicks
        // while waiting for their battle action. The dedicated comic assertions
        // above separately verify actual pointer raycasts at all screen corners.
        internal static void AdvanceRamComicIfWaiting(CoreGameplayDemoController controller)
        {
            GameObject host = GameObject.Find("Battle Ram Comic Advance");
            if (host == null || (bool)RamComicField("ramComicAdvanceRequested").GetValue(controller)) return;
            Button advance = host.GetComponent<Button>();
            if (advance != null && advance.IsInteractable()) advance.onClick.Invoke();
        }

        private static IEnumerator ClickThroughRamComic(CoreGameplayDemoController controller, int panelCount = 4)
        {
            for (int panel = 0; panel < panelCount; panel++)
            {
                Assert.That((int)RamComicField("ramComicPanelIndex").GetValue(controller), Is.EqualTo(panel));
                ClickRamComicScreen(new Vector2(.5f, .5f));
                if (panel + 1 < panelCount) yield return WaitForRamComicPanel(controller, panel + 1);
            }
        }

        private static string RamComicCoreState(CoreVoyageSession session)
        {
            CoreBattleState battle = session.Battle;
            string state = session.Phase + ":" + session.Hull + ":" + session.Cargo.Gold + ":" + session.Cargo.Cannonballs + ":" + session.Cargo.Powder;
            return battle == null ? state + ":no-battle" : state + ":" + battle.Stage + ":" + battle.Turn + ":" + battle.PhaseTurn +
                ":" + battle.Energy + ":" + battle.EnemyHull + ":" + battle.PlayerBoardingHealth + ":" + battle.EnemyBoardingHealth +
                ":" + battle.PlayerBlock + ":" + battle.CardsPlayedThisTurn + ":" + string.Join(",", battle.Hand) +
                ":" + string.Join(",", battle.DrawPile) + ":" + string.Join(",", battle.DiscardPile);
        }

        private static FieldInfo RamComicField(string name)
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }
    }
}
