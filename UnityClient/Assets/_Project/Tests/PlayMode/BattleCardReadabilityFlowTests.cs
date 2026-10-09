using System.Collections;
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
        public IEnumerator BattleReadability_WheelReachesCompleteLargeTextThenPointerPlaysExactlyOnce()
        {
            int? previous = GameReadability.OverrideLevel;
            try
            {
                GameReadability.OverrideLevel = 2;
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
                session.Battle.Stage = NavalBattleStage.Boarding;
                session.Battle.PlayerBoardingHealth = 10;
                session.Cargo.Medicine = 1;
                SetResponsiveHand(session, "card_crew_bandage", "card_crew_guard");
                RenderResponsiveBattle(controller);
                yield return null;
                Button card = RequiredResponsive<Button>("Battle Card 0");
                CoreBattleState battle = session.Battle;
                CoreBattleCard data = session.GetBattleCard(battle.Hand[0]);
                CoreBattleCardEffects effects = session.GetBattleCardEffects(data, NavalBattleStage.Boarding);
                int beforeHealth = battle.PlayerBoardingHealth, beforeEnergy = battle.Energy;
                int beforePlayed = battle.CardsPlayedThisTurn, beforeHand = battle.Hand.Count;
                int beforeMedicine = session.Cargo.Medicine;
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, card.transform.position),
                    scrollDelta = new Vector2(0f, -12f),
                };
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.18f);
                BattleCardReadabilityScroll relay = card.GetComponent<BattleCardReadabilityScroll>();
                Assert.That(relay, Is.Not.Null);
                ScrollRect scroll = relay.Details;
                Assert.That(scroll.gameObject.activeInHierarchy, Is.True);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "The real healing explanation must exercise scrolling at the largest font.");
                Text text = scroll.content.GetComponentInChildren<Text>();
                Assert.That(text.fontSize, Is.EqualTo(33));
                Assert.That(text.resizeTextForBestFit, Is.False);
                Assert.That(scroll.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                Vector2 initial = scroll.content.anchoredPosition;
                for (int i = 0; i < 10; i++)
                {
                    ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.scrollHandler);
                    yield return null;
                }
                Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(initial.y));
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.01f));
                Assert.That(text.text, Does.Contain("药品").And.Contain("切换阶段恢复基础费用"));
                Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(beforeHealth));
                Assert.That(battle.Energy, Is.EqualTo(beforeEnergy));
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(beforePlayed));
                Assert.That(battle.Hand.Count, Is.EqualTo(beforeHand));
                Assert.That(session.Cargo.Medicine, Is.EqualTo(beforeMedicine));
                NavalBattleVisualCapture.Capture(controller, "Readability_Large_Healing_Details.png",
                    "特大字号滚轮读到真实包扎说明底部；资源尚未消耗", "Battle Card");

                ClickAcceptanceButton("Battle Card 0");
                yield return WaitResponsiveBattle(controller);
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(beforePlayed + 1));
                Assert.That(battle.Hand.Count, Is.EqualTo(beforeHand - 1));
                Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(beforeHealth + effects.ActualRepair));
                Assert.That(session.Cargo.Medicine, Is.EqualTo(beforeMedicine - effects.MedicineCost));
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { GameReadability.OverrideLevel = previous; }
        }
    }
}
