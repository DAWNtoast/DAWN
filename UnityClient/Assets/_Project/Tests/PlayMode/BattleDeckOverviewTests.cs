using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BattleDeckOverview_AllStagesShowLivePilesInFiveColumnsWithoutChangingBattle()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            CoreBattleState battle = session.Battle;
            // Use explicit duplicates and more than one row to verify individual copies and scrolling.
            battle.DrawPile.Clear();
            for (int i = 0; i < 11; i++) battle.DrawPile.Add(ResponsiveCardIndex(i % 2 == 0 ? "card_brace" : "card_full_sail_turn"));
            battle.DiscardPile.Add(ResponsiveCardIndex("card_brace"));
            battle.DiscardPile.Add(ResponsiveCardIndex("card_brace"));
            RenderResponsiveBattle(controller);
            yield return null;
            int sceneId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int handId = RequiredResponsive<RectTransform>("Battle Hand").GetInstanceID();
            int[] hand = battle.Hand.ToArray();
            int turn = battle.Turn;
            int[][] draw = new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding }
                .Select(stage => battle.GetDrawPile(stage).ToArray()).ToArray();
            NavalBattleVisualCapture.Capture(controller, "01-battle-header.png", "Battle header with deck and settings icons");
            Button opener = RequiredResponsive<Button>("Battle Deck");
            EventSystem.current.SetSelectedGameObject(opener.gameObject);
            opener.onClick.Invoke();
            yield return null;
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.False);
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);

            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
            {
                RequiredResponsive<Button>("Battle Deck Tab " + stage).onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertBattleDeckGroup("Undrawn", battle.GetDrawPile(stage).Count);
                AssertBattleDeckGroup("Discard", battle.GetDiscardPile(stage).Count);
                Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Preparation), "Browsing a tab cannot change combat stage.");
                NavalBattleVisualCapture.Capture(controller, "02-deck-" + stage + ".png", "Live deck inspection: " + stage);
            }
            RequiredResponsive<Button>("Battle Deck Tab Preparation").onClick.Invoke();
            yield return null;
            ScrollRect scroll = RequiredResponsive<ScrollRect>("Battle Deck Viewport");
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            NavalBattleVisualCapture.Capture(controller, "03-deck-discard.png", "Scrolled to separate discard group");
            Button.ButtonClickedEvent retiredClose = RequiredResponsive<Button>("Close Battle Deck").onClick;
            retiredClose.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(opener.gameObject));
            opener.onClick.Invoke();
            retiredClose.Invoke();
            Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Not.Null, "A retired callback cannot close a new opening.");
            RequiredResponsive<Button>("Close Battle Deck").onClick.Invoke();
            yield return null;
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(sceneId));
            Assert.That(RequiredResponsive<RectTransform>("Battle Hand").GetInstanceID(), Is.EqualTo(handId));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
            Assert.That(battle.Turn, Is.EqualTo(turn));
            CollectionAssert.AreEqual(hand, battle.Hand);
            for (int i = 0; i < 3; i++)
                CollectionAssert.AreEqual(draw[i], battle.GetDrawPile(new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding }[i]));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleDeckOverview_EscapeEmptyPilesAndSettingsPreserveBattle()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
                ConfigureCloseNavalTestPose(session.Battle, false);
                Assert.That(session.TryBoard(), Is.True);
                session.Battle.DrawPile.Clear();
                session.Battle.DiscardPile.Clear();
                RenderResponsiveBattle(controller);
                yield return null;
                int sceneId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
                RequiredResponsive<Button>("Battle Deck").onClick.Invoke();
                yield return null;
                AssertBattleDeckGroup("Undrawn", 0);
                AssertBattleDeckGroup("Discard", 0);
                Assert.That(GameObject.Find("Battle Deck Undrawn Empty"), Is.Not.Null);
                Assert.That(GameObject.Find("Battle Deck Discard Empty"), Is.Not.Null);
                RequiredResponsive<Button>("Battle Settings").onClick.Invoke();
                Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null);
                EventSystem.current.SetSelectedGameObject(null);
                yield return input.Press();
                Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Null);
                RequiredResponsive<Button>("Battle Settings").onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Port Settings Overlay"), Is.Not.Null);
                Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.False);
                RequiredResponsive<Button>("Battle Deck").onClick.Invoke();
                Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Null);
                NavalBattleVisualCapture.Capture(controller, "04-battle-settings.png", "Existing settings modal opened from battle header");
                yield return input.Press();
                Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null);
                Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(sceneId));
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator BattleDeckOverview_StageTransitionReopensCurrentDeckAndRenderClosesStaleOverlay()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            RenderResponsiveBattle(controller);
            yield return null;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            RequiredResponsive<Button>("Battle Deck").onClick.Invoke();
            RequiredResponsive<Button>("Battle Settings").onClick.Invoke();
            Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Null);
            Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            RequiredResponsive<Button>("Battle Deck").onClick.Invoke();
            yield return null;
            AssertBattleDeckGroup("Undrawn", session.Battle.DrawPile.Count);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Battle Deck Tab Naval"));
            RenderResponsiveBattle(controller);
            yield return null;
            Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Null);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBattleDeckGroup(string key, int expected)
        {
            RectTransform group = RequiredResponsive<RectTransform>("Battle Deck " + key + " Group");
            RectTransform[] faces = group.Cast<Transform>().Where(t => t.name.EndsWith(" Face")).Cast<RectTransform>().ToArray();
            Assert.That(faces.Length, Is.EqualTo(expected), "Every copy in the live pile must have its own card face.");
            Assert.That(group.GetComponentsInChildren<Button>(), Is.Empty, "Inspection cards cannot play or edit cards.");
            for (int index = 0; index < faces.Length; index++)
            {
                Assert.That(faces[index].anchoredPosition.x, Is.EqualTo((index % 5 - 2) * 244f).Within(.01f));
                if (index >= 5) Assert.That(faces[index].anchoredPosition.y,
                    Is.EqualTo(faces[index - 5].anchoredPosition.y - 364f).Within(.01f));
            }
        }
    }
}
