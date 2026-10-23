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
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.PlayerOrdinaryCrew, Is.EqualTo(10));
            CoreBattleCard bandage = session.GetBattleCard(ResponsiveCardIndex("card_crew_bandage"));
            const int enhanced = 6;
            Assert.That(bandage.Repair, Is.EqualTo(enhanced), "The authored full treatment is unchanged by the ten-sailor roster.");
            int baseline = 2;
            BoardingCinematicAssertions.AssertScene(session);
            int healthBarId = RequiredResponsive<Image>("Player Battle Hull Bar").GetInstanceID();
            AssertBoardingMedicineResources(session, 1);
            Button retained = RequiredResponsive<Button>("Battle Card 1");
            int retainedId = retained.GetInstanceID();
            Text face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, bandage);
            string authoredFace = ForgeVisibleText(face.text);
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_03_Medicine_Enhanced.png", "固定卡面完整展示有药恢复6与无药恢复2的两条规则", "Battle Card");
            yield return HoverMedicineCard(0);
            AssertKeywordOnlyCardHover(RequiredResponsive<Button>("Battle Card 0"), 0, session, bandage, "medicine available hover");
            AssertMedicineQuoteIsReadOnly(session, bandage, enhanced, 1);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1), "Pointer preview must not spend medicine.");
            NavalBattleVisualCapture.Capture(controller, "Medicine_01_Enhanced_Hover.png", "包扎卡面保留治疗规则，普通恢复不生成关键词浮窗且悬停不消耗药物", "Battle Card");

            ClickAcceptanceButton("Battle Card 0");
            yield return WaitBoardingFeedback(controller, "BoardingFX", BoardingCombatFxKind.Heal);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10 + enhanced));
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(RequiredResponsive<Image>("Player Battle Hull Bar").GetInstanceID(), Is.EqualTo(healthBarId));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            AssertBoardingMedicineResources(session, 0);
            Assert.That(RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), Is.EqualTo(retainedId));
            face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, bandage);
            Assert.That(ForgeVisibleText(face.text), Is.EqualTo(authoredFace), "The retained card keeps both fixed supply branches visible.");
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_04_Medicine_Empty.png", "药品耗尽后保留同一完整卡面，HUD药品刷新为0", "Battle Card");
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("2"));
            yield return HoverMedicineCard(0);
            AssertKeywordOnlyCardHover(RequiredResponsive<Button>("Battle Card 0"), 0, session, bandage, "medicine exhausted hover");
            AssertMedicineQuoteIsReadOnly(session, bandage, baseline, 0);
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
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth;
            SetResponsiveHand(session, "card_crew_bandage", "card_crew_bandage");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            CoreBattleCard bandage = session.GetBattleCard(ResponsiveCardIndex("card_crew_bandage"));
            Text face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, bandage);
            string fullHealthFace = ForgeVisibleText(face.text);
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_05_Medicine_Full.png", "满血时保留固定卡牌规则，实际出牌不消耗药物", "Battle Card");
            yield return HoverMedicineCard(0);
            AssertKeywordOnlyCardHover(RequiredResponsive<Button>("Battle Card 0"), 0, session, bandage, "full health hover");
            AssertMedicineQuoteIsReadOnly(session, bandage, 0, 0);
            NavalBattleVisualCapture.Capture(controller, "Medicine_03_Full_Health.png", "满血包扎仍可悬停查看卡面，不弹出计算或禁用原因浮窗", "Battle Card");
            Button fullHealthCard = RequiredResponsive<Button>("Battle Card 0");
            Assert.That(fullHealthCard.IsInteractable(), Is.False, "A full-health bandage cannot be played.");
            Assert.That(session.CardUnavailableReason(0), Is.EqualTo("当前生命已满"));
            int fullHealthEnergy = session.Battle.Energy;
            int[] fullHealthHand = session.Battle.Hand.ToArray();
            // Exercise the disabled control through an actual raycast without
            // bypassing Button's interactability guard via onClick.Invoke.
            PointerEventData blockedPointer = DeckBuilderPointer(fullHealthCard);
            ExecuteEvents.Execute(fullHealthCard.gameObject, blockedPointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            CollectionAssert.AreEqual(fullHealthHand, session.Battle.Hand, "A rejected click cannot discard the card.");
            Assert.That(session.Battle.Energy, Is.EqualTo(fullHealthEnergy));
            Assert.That(session.GetBoardingHandEnergyCost(0), Is.EqualTo(bandage.Energy), "A rejected click cannot increase repeat fees.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));

            session.Battle.PlayerBoardingHealth -= 1;
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            face = RequiredResponsive<Button>("Battle Card 0").transform.Find("Label").GetComponent<Text>();
            AssertStableMedicineFace(face, bandage);
            Assert.That(ForgeVisibleText(face.text), Is.EqualTo(fullHealthFace), "A small wound does not rewrite the authored card rules.");
            yield return HoverMedicineCard(0);
            AssertKeywordOnlyCardHover(RequiredResponsive<Button>("Battle Card 0"), 0, session, bandage, "small wound hover");
            AssertMedicineQuoteIsReadOnly(session, bandage, 1, 0);
            Assert.That(session.CardUnavailableReason(0), Is.Empty, "A real wound makes the bandage playable again.");
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(fullHealthHand.Length - 1));
            Assert.That(session.Battle.Energy, Is.EqualTo(fullHealthEnergy - bandage.Energy));
            Assert.That(session.GetBoardingHandEnergyCost(0), Is.EqualTo(bandage.Energy + 1), "Only the successful treatment increases repeat fees.");
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
            AssertBoardingMedicineResources(session, 2);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBoardingMedicineResources(CoreVoyageSession session, int medicine)
        {
            Assert.That(RequiredResponsive<RectTransform>("Resource Medicine").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo(medicine.ToString()));
            Assert.That(RequiredResponsive<RectTransform>("Resource Powder").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo(session.Cargo.Powder.ToString()));
            Assert.That(RequiredResponsive<RectTransform>("Resource Planks").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo(session.Cargo.Wood.ToString()));
        }

        private static void AssertStableMedicineFace(Text face, CoreBattleCard bandage)
        {
            Assert.That(bandage.BoardingSpec.Key, Is.EqualTo("basic_06"));
            Assert.That(bandage.Repair, Is.EqualTo(6));
            Assert.That(ForgeVisibleText(face.text), Is.EqualTo("恢复2点生命值消耗1药物额外恢复4点生命值"));
            Assert.That(ForgeVisibleText(face.text), Is.EqualTo(ForgeVisibleText(CardDescriptionText.Compact(bandage.Description))));
            Assert.That(face.text, Does.Not.Contain("实际").And.Not.Contain("本次恢复").And.Not.Contain("无药基础"));
            Assert.That(face.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize));
            Assert.That(face.preferredHeight, Is.LessThanOrEqualTo(face.rectTransform.rect.height + .5f));
        }
        private static void AssertMedicineQuoteIsReadOnly(CoreVoyageSession session, CoreBattleCard bandage, int healing, int medicine)
        {
            int beforeHealth = session.Battle.PlayerBoardingHealth, beforeMedicine = session.Cargo.Medicine;
            CoreBattleCardEffects quote = session.GetBattleCardEffects(bandage, NavalBattleStage.Boarding);
            Assert.That(quote.FullRepair, Is.EqualTo(6));
            Assert.That(quote.FallbackRepair, Is.EqualTo(2));
            Assert.That(quote.ActualRepair, Is.EqualTo(healing));
            Assert.That(quote.MedicineCost, Is.EqualTo(medicine));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(beforeHealth));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(beforeMedicine));
        }
        private static IEnumerator HoverMedicineCard(int index)
        {
            Button card = RequiredResponsive<Button>("Battle Card " + index);
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, card.transform.position) };
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(card.GetComponent<Canvas>().overrideSorting, Is.True,
                "A basic treatment card still supports inspection without a keyword popup.");
        }
    }
}
