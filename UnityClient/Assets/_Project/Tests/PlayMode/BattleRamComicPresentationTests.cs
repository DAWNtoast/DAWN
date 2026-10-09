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
        public IEnumerator RamComic_PlayerHeadOn_ShowsFourPanelsFullPageAndDiagonalTransitionBeforeBoarding() =>
            ExerciseRamComic(false, NavalBattleAlignment.HeadOn, false);

        [UnityTest]
        public IEnumerator RamComic_PlayerBroadside_SkipKeepsOneCommittedRamAndPlayerInitiative() =>
            ExerciseRamComic(false, NavalBattleAlignment.EnemyBroadside, true);

        [UnityTest]
        public IEnumerator RamComic_BossHeadOn_ShowsFourPanelsAndAppliesExactlyOneOpening() =>
            ExerciseRamComic(true, NavalBattleAlignment.HeadOn, false);

        [UnityTest]
        public IEnumerator RamComic_BossBroadside_SkipKeepsOneOpeningAndEnemyInitiative() =>
            ExerciseRamComic(true, NavalBattleAlignment.EnemyBroadside, true);

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
                var expected = Resources.Load<Texture2D>("BattleComics/ram_boss_broadside_v1");
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
                UnityEvent oldSkip = RequiredResponsive<Button>("Battle Ram Comic Skip").onClick;
                string committed = RamComicCoreState(session);

                controller.enabled = false;
                yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                controller.enabled = true;
                yield return null;
                yield return null;
                oldSkip.Invoke();
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
                    string resultStep = enemy ? "4 / 4" : "撞击结算";
                    var shown = new List<string>();
                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (GameObject.Find("Battle Ram Comic Overlay") != null && Time.realtimeSinceStartup < deadline)
                    {
                        string step = RequiredResponsive<Text>("Battle Ram Comic Step").text;
                        if (step == resultStep) break;
                        if (shown.Count == 0 || shown[shown.Count - 1] != step) shown.Add(step);
                        yield return null;
                    }
                    Assert.That(RequiredResponsive<Text>("Battle Ram Comic Step").text, Is.EqualTo(resultStep));
                    if (!enemy) Assert.That(shown, Is.EqualTo(new[] { "1 / 4", "2 / 4" }),
                        "Hull sinking must not display the later boarding panels or the complete boarding page.");
                    Assert.That(RequiredResponsive<Text>("Battle Ram Comic Caption").text,
                        Does.Contain(enemy ? "我方失去战斗力" : "敌舰沉没").And.Not.Contain("转入接舷近战"));
                    ClickAcceptanceButton("Battle Ram Comic Skip");
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
                    int index = enemy ? 2 : 0;
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

        private static IEnumerator ExerciseRamComic(bool enemy, NavalBattleAlignment alignment, bool skip)
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
                string path = "BattleComics/ram_" + (enemy ? "boss_" : "player_") + (headOn ? "head_on_v1" : "broadside_v1");
                Texture2D expected = Resources.Load<Texture2D>(path);
                Assert.That(expected, Is.Not.Null, path);
                Assert.That(RequiredResponsive<RawImage>("Battle Ram Comic Page").texture, Is.SameAs(expected));
                Assert.That(RequiredResponsive<Text>("Battle Ram Comic Title").text,
                    Does.Contain(enemy ? "敌舰冲撞" : "主动冲撞").And.Contain(headOn ? "舰艏对舰艏" : "舰艏对侧舷"));
                Assert.That(session.Hull, Is.LessThan(hull));
                Assert.That(battle.EnemyHull, Is.LessThan(enemyHull));
                Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(crew - opening));
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
                UnityEvent oldSkip = RequiredResponsive<Button>("Battle Ram Comic Skip").onClick;
                if (skip) ClickAcceptanceButton("Battle Ram Comic Skip");

                var observed = new List<string>();
                bool sawTransition = false, sawCoveredFrame = false, sawRevealWithLockedInput = false;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    GameObject overlay = GameObject.Find("Battle Ram Comic Overlay");
                    if (overlay != null)
                    {
                        string step = RequiredResponsive<Text>("Battle Ram Comic Step").text;
                        if (observed.Count == 0 || observed[observed.Count - 1] != step) observed.Add(step);
                        RawImage page = RequiredResponsive<RawImage>("Battle Ram Comic Page");
                        Assert.That(page.texture, Is.SameAs(expected));
                        AssertRamComicUv(step, page.uvRect);
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
                            oldTurn.Invoke(); oldRam.Invoke(); oldCard.Invoke(); oldSkip.Invoke();
                        }
                    }
                    Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The existing 12 second animation watchdog must remain sufficient.");
                Assert.That(sawTransition, Is.True, "Natural playback and skip both enter the diagonal scene transition.");
                Assert.That(sawCoveredFrame, Is.True, "The scene swap must occur behind one fully covered frame.");
                Assert.That(sawRevealWithLockedInput, Is.True, "The comic must retire while the actual boarding scene is still locked beneath the reveal.");
                if (!skip) Assert.That(observed, Is.EqualTo(new[] { "1 / 4", "2 / 4", "3 / 4", "4 / 4", "完整漫画" }));
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
                    Assert.DoesNotThrow(() => oldSkip.Invoke(),
                        "A destroyed comic's stale Skip callback must stay harmless while another battle animation is active.");
                }
                finally { playingField.SetValue(controller, previousPlaying); }
                yield return null;
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed), "A stale skip may only retire presentation, never replay the collision.");
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
            Assert.That(RequiredResponsive<Button>("Battle Ram Comic Skip").IsInteractable(), Is.True);
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

        private static void AssertRamComicUv(string step, Rect actual)
        {
            Rect expected;
            switch (step)
            {
                case "1 / 4": expected = new Rect(.01f, .51f, .48f, .48f); break;
                case "2 / 4": expected = new Rect(.51f, .51f, .48f, .48f); break;
                case "3 / 4": expected = new Rect(.01f, .01f, .48f, .48f); break;
                case "4 / 4": expected = new Rect(.51f, .01f, .48f, .48f); break;
                case "完整漫画": expected = new Rect(0, 0, 1, 1); break;
                default: Assert.Fail("Unexpected comic step: " + step); return;
            }
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.0001f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(.0001f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(.0001f));
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
