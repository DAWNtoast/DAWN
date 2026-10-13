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
            CoreBattleCard bandage = session.GetBattleCard(ResponsiveCardIndex("card_crew_bandage"));
            const int enhanced = 6;
            Assert.That(bandage.Repair, Is.EqualTo(enhanced), "The authored full treatment is unchanged by the ten-sailor roster.");
            int baseline = 2;
            BoardingCinematicAssertions.AssertScene(session);
            int gridId = RequiredResponsive<Image>("Player Battle Hull Bar").GetComponentInChildren<BattleHealthSegmentsGraphic>().GetInstanceID();
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Player Ammunition").text,
                Is.EqualTo("火药 " + session.Cargo.Powder + " · 木板 " + session.Cargo.Wood));
            Button retained = RequiredResponsive<Button>("Battle Card 1");
            int retainedId = retained.GetInstanceID();
            Text face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, enhanced, 1);
            string enhancedStructure = MedicineFaceWords(face.text);
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_03_Medicine_Enhanced.png", "有药时固定卡面恢复" + enhanced + "、消耗1药品", "Battle Card");
            yield return HoverMedicineCard(0);
            Text explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("本次实际恢复 " + enhanced).And.Contain("药品 -1").And.Contain("无药保底 " + baseline));
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("卡牌效果不受人数影响")
                .And.Contain("有药完整 " + enhanced).And.Not.Contain("35.7%").And.Not.Contain("人为基准"));
            AssertCalculationTooltipOmitsGeneralKeywords(explanation.text);
            AssertBattleRuleTooltipFits(explanation, 11);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1), "Pointer preview must not spend medicine.");
            NavalBattleVisualCapture.Capture(controller, "Medicine_01_Enhanced_Hover.png", "有药接舷包扎的上限、实际治疗与药品成本", "Battle Card");

            ClickAcceptanceButton("Battle Card 0");
            yield return WaitBoardingFeedback(controller, "BoardingFX", BoardingCombatFxKind.Heal);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10 + enhanced));
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(RequiredResponsive<Image>("Player Battle Hull Bar").GetComponentInChildren<BattleHealthSegmentsGraphic>().GetInstanceID(), Is.EqualTo(gridId));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(RequiredResponsive<Text>("Player Ammunition").text,
                Is.EqualTo("火药 " + session.Cargo.Powder + " · 木板 " + session.Cargo.Wood));
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo("0"));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(retainedId));
            face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, baseline, 0);
            Assert.That(MedicineFaceWords(face.text), Is.EqualTo(enhancedStructure), "The retained card changes values, not its words.");
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_04_Medicine_Empty.png", "药品耗尽后同一卡面仅改恢复" + baseline + "、消耗0药品", "Battle Card");
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("2"));
            yield return HoverMedicineCard(0);
            explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("本次实际恢复 " + baseline).And.Contain("不耗药")
                .And.Contain("无药保底 " + baseline).And.Not.Contain("药品增疗 +"));
            AssertCalculationTooltipOmitsGeneralKeywords(explanation.text);
            AssertBattleRuleTooltipFits(explanation, 11);
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10 + enhanced + baseline));
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
            Text face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, 0, 0);
            string fullHealthStructure = MedicineFaceWords(face.text);
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_05_Medicine_Full.png", "满血时同一卡面仅改恢复0、消耗0药品", "Battle Card");
            yield return HoverMedicineCard(0);
            Text explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("本次实际恢复 0").And.Contain("不耗药"));
            AssertCalculationTooltipOmitsGeneralKeywords(explanation.text);
            AssertBattleRuleTooltipFits(explanation, 11);
            NavalBattleVisualCapture.Capture(controller, "Medicine_03_Full_Health.png", "满血卡面固定显示恢复0与药品0，规则气泡保留详细解释", "Battle Card");
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));

            session.Battle.PlayerBoardingHealth -= 1;
            RenderResponsiveBattle(controller);
            yield return null;
            face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, 1, 0);
            Assert.That(MedicineFaceWords(face.text), Is.EqualTo(fullHealthStructure), "Taking damage changes only the same numeric slots.");
            yield return HoverMedicineCard(0);
            explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(CompactMedicineText(explanation.text), Does.Contain("本次实际恢复 1").And.Contain("不耗药"));
            AssertCalculationTooltipOmitsGeneralKeywords(explanation.text);
            AssertBattleRuleTooltipFits(explanation, 11);
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo("2"));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertStableMedicineFace(Text face, int healing, int medicine)
        {
            Assert.That(ForgeVisibleText(face.text), Is.EqualTo("恢复" + healing + "点己方船员生命消耗" + medicine + "药品"));
            Assert.That(face.text, Does.Not.Contain("实际").And.Not.Contain("本次恢复").And.Not.Contain("无药基础"));
            Assert.That(face.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize));
            Assert.That(face.preferredHeight, Is.LessThanOrEqualTo(face.rectTransform.rect.height + .5f));
        }
        private static string MedicineFaceWords(string text) =>
            System.Text.RegularExpressions.Regex.Replace(ForgeVisibleText(text), @"\d+", "#");
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
