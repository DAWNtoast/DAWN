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
        public IEnumerator BoardingMedicine_RealCardsConsumeOneBottleAndRefreshRetainedHealingAndHud()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.PlayerBoardingHealth = 10;
            session.Cargo.Medicine = 1;
            SetResponsiveHand(session, "card_crew_bandage", "card_crew_bandage");
            RenderResponsiveBattle(controller);
            yield return null;
            Assert.That(session.Battle.PlayerOrdinaryCrew, Is.EqualTo(10));
            BoardingCinematicAssertions.AssertScene(session);
            int gridId = RequiredResponsive<Image>("Player Battle Hull Bar").GetComponentInChildren<BattleHealthSegmentsGraphic>().GetInstanceID();
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo("1"));
            Button retained = RequiredResponsive<Button>("Battle Card 1");
            int retainedId = retained.GetInstanceID();
            Text face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label Viewport/Content/Label").GetComponent<Text>();
            Assert.That(CompactMedicineText(face.text), Does.Contain("本次恢复 5").And.Contain("药品 -1").And.Contain("无药基础 3"));
            yield return HoverMedicineCard(0);
            Text explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text Viewport/Content/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("本次实际恢复 5").And.Contain("药品 -1").And.Contain("无药基础 3"));
            AssertBattleRuleTooltipFits(explanation, 11);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1), "Pointer preview must not spend medicine.");
            NavalBattleVisualCapture.Capture(controller, "Medicine_01_Enhanced_Hover.png", "有药接舷包扎的上限、实际治疗与药品成本", "Battle Card");

            ClickAcceptanceButton("Battle Card 0");
            yield return WaitBoardingFeedback(controller, "BoardingFX", BoardingCombatFxKind.Heal);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(15));
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(RequiredResponsive<Image>("Player Battle Hull Bar").GetComponentInChildren<BattleHealthSegmentsGraphic>().GetInstanceID(), Is.EqualTo(gridId));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo("0"));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(retainedId));
            face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label Viewport/Content/Label").GetComponent<Text>();
            Assert.That(CompactMedicineText(face.text), Does.Contain("本次恢复 3").And.Contain("不耗药").And.Contain("无药基础 3"));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("2"));
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(18));
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.Battle.Energy, Is.Zero);
            NavalBattleVisualCapture.Capture(controller, "Medicine_02_Exhausted.png", "最后一瓶只增强一次，下一张恢复基础治疗且重复加费保留", "Battle Card");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingMedicine_FullHealthAndSmallWoundCardsClearlyPreserveSupplies()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Cargo.Medicine = 2;
            SetResponsiveHand(session, "card_crew_bandage", "card_crew_bandage");
            RenderResponsiveBattle(controller);
            yield return null;
            Text face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label Viewport/Content/Label").GetComponent<Text>();
            Assert.That(CompactMedicineText(face.text), Does.Contain("本次恢复 0").And.Contain("满血不耗药").And.Contain("有药上限 5"));
            yield return HoverMedicineCard(0);
            Text explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text Viewport/Content/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("本次实际恢复 0").And.Contain("不耗药"));
            AssertBattleRuleTooltipFits(explanation, 11);
            NavalBattleVisualCapture.Capture(controller, "Medicine_03_Full_Health.png", "满血卡面区分当前恢复0与有药潜在上限，明确不耗药", "Battle Card");
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));

            session.Battle.PlayerBoardingHealth -= 1;
            RenderResponsiveBattle(controller);
            yield return null;
            face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label Viewport/Content/Label").GetComponent<Text>();
            Assert.That(CompactMedicineText(face.text), Does.Contain("本次恢复 1").And.Contain("不耗药"));
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo("2"));
            LogAssert.NoUnexpectedReceived();
        }

        private static string CompactMedicineText(string text) => text.Replace("\n", string.Empty).Replace('\u00a0', ' ');
        private static IEnumerator HoverMedicineCard(int index)
        {
            Button card = RequiredResponsive<Button>("Battle Card " + index);
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, card.transform.position) };
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(card.transform.Find("Card Keyword Bubble " + index).gameObject.activeInHierarchy, Is.True);
        }
    }
}
