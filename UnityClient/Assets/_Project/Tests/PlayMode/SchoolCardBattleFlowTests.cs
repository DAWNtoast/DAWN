using System.Collections;
using System.Linq;
using System.Reflection;
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
        public IEnumerator SchoolOverflowAndLegacyLoad_UseFinalDamageInInitialHudRetainedHudAndAnimation()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            SetResponsiveHand(session, "card_load_shot", "card_load_shot", "card_school_fire_b03");
            SetResponsiveDrawPile(session);
            session.Battle.DistanceMeters = 100f;
            session.Battle.Energy = session.Battle.MaxEnergy = 20;
            session.Cargo.Powder = session.Cargo.Cannonballs = 20;
            // Model an earned school contribution. Core tests cover earning and
            // capping it; this exercises every production display after mixing it.
            const int schoolReserve = 2;
            typeof(CoreSchoolBattleState).GetProperty("OverflowContribution").SetValue(session.Battle.Schools, schoolReserve);
            CoreBattleCard loading = session.GetBattleCard(ResponsiveCardIndex("card_load_shot"));
            int firstIncrease = System.Math.Min(loading.StrategyAmount, loading.StrategyLimit - schoolReserve);
            int finalIncrease = System.Math.Min(loading.StrategyAmount, loading.StrategyLimit - schoolReserve - firstIncrease);
            Assert.That(finalIncrease, Is.GreaterThan(0), "The second action must exercise a partial positive increase and its animation.");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            AssertSchoolLoadedHud(schoolReserve);
            // Let the first rebuilt HUD finish its font/mesh update before capturing pixels.
            yield return new WaitForSecondsRealtime(.1f);
            NavalBattleVisualCapture.Capture(controller, "ResourceRules_01_Naval_Load.png", "炮战火药主图标、炮弹与木板储备、装填加伤同时可见", "Battle Card");
            foreach (int increment in new[] { firstIncrease, finalIncrease })
            {
                int before = session.PlayerLoadedShotDamageBonus;
                int playedBefore = session.Battle.CardsPlayedThisTurn;
                int energyBefore = session.Battle.Energy;
                int quotedEnergy = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                Button load = RequiredResponsive<Button>("Battle Card 0");
                Assert.That(load.interactable, Is.True, "Loading input must be ready: played=" + playedBefore +
                    "; bonus=" + before + "; core=" + session.CanPlayCard(0) + "; reason=" + session.CardUnavailableReason(0) +
                    "; animation=" + controller.IsBattleAnimationPlaying + "; tutorial=" + controller.IsTutorialActive +
                    "; parent=" + load.transform.parent.name + "; active=" + controller.isActiveAndEnabled +
                    "; stage=" + session.Battle.Stage + "; buttons=" + string.Join(" | ",
                        Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(button => button.name == "Battle Card 0")
                            .Select(button => button.transform.parent.name + ":" + button.interactable)));
                Assert.That(load.transform.Find("Label").GetComponent<Text>().text,
                    Does.Contain("下次炮击+" + increment).And.Not.Contain("本次实际增加"));
                load.onClick.Invoke();
                Assert.That(controller.IsBattleAnimationPlaying, Is.True, "The real loading button must begin feedback before its transient caption is observed.");
                string seenFloat = null;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    GameObject caption = GameObject.Find("Battle Strategy Float");
                    if (caption != null) seenFloat = caption.GetComponent<Text>().text;
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(seenFloat, Is.EqualTo("装填加伤 +" + increment));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(playedBefore + 1));
                Assert.That(session.Battle.Energy, Is.EqualTo(energyBefore - quotedEnergy));
                Assert.That(session.PlayerLoadedShotDamageBonus, Is.EqualTo(before + increment));
                AssertSchoolLoadedHud(before + increment);
                // Completion rebinds the retained hand while the animation lock
                // is still held; the HUD observer releases it in LateUpdate.
                // Let that real frame finish before clicking the next copy.
                yield return null;
            }
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.EqualTo(loading.StrategyLimit));
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.Zero);
            AssertSchoolLoadedHud(0);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertSchoolLoadedHud(int finalBonus)
        {
            CoreVoyageSession session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
            Assert.That(RequiredResponsive<RectTransform>("Resource Cannonball").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo(session.Cargo.Cannonballs.ToString()));
            Assert.That(RequiredResponsive<RectTransform>("Resource Planks").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo(session.Cargo.Wood.ToString()));
            Transform slot = RequiredResponsive<BattleStatusRow>("Player Battle Status Row").transform.Find("Status loaded_shot");
            if (finalBonus == 0) Assert.That(slot, Is.Null, "Spent loading must leave the visible status row.");
            else
            {
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.Find("Amount").GetComponent<Text>().text, Is.EqualTo(finalBonus.ToString()));
                Assert.That(NavalHudTooltipText("Status loaded_shot"), Does.Contain("下次炮击增加 " + finalBonus + " 点伤害"));
            }
        }

        [UnityTest]
        public IEnumerator SchoolCards_RealButtonsRefreshStateAndRejectLethalLifePaymentWithoutSpending()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            SetResponsiveHand(session, "card_school_fire_b03", "card_school_arcane_b03",
                "card_school_curse_b03", "card_school_thunder_b03", "card_school_blood_b03");
            SetResponsiveDrawPile(session);
            session.Battle.DistanceMeters = 100f;
            session.Battle.Energy = session.Battle.MaxEnergy = 20;
            session.Cargo.Powder = session.Cargo.Cannonballs = 20;
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, session.Battle.Schools.ManaLimit);
            int startingMana = session.Battle.Schools.Mana;
            int manaCost = session.GetBattleCard(ResponsiveCardIndex("card_school_arcane_b03")).SchoolEffect.Threshold;
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            RectTransform battlefield = RequiredResponsive<RectTransform>("Battle Card");
            RectTransform resources = RequiredResponsive<RectTransform>("Battle Compact Resources");
            int originalBattlefield = battlefield.GetInstanceID(), originalResources = resources.GetInstanceID();
            foreach (string school in new[] { "fire", "arcane", "curse", "thunder" })
            {
                Button card = RequiredResponsive<Button>("Battle Card 0");
                Assert.That(card.GetComponentInChildren<CardSchoolFrameGraphic>().SchoolId, Is.EqualTo(school));
                Assert.That(card.interactable, Is.True, session.CardUnavailableReason(0));
                card.onClick.Invoke();
                yield return WaitResponsiveBattle(controller);
                Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(originalBattlefield));
                Assert.That(RequiredResponsive<RectTransform>("Battle Compact Resources").GetInstanceID(), Is.EqualTo(originalResources));
                Assert.That(RequiredResponsive<RectTransform>("Resource Mana").Find("Amount").GetComponent<Text>().text,
                    Is.EqualTo(session.Battle.Schools.Mana + "/" + session.Battle.Schools.ManaLimit));
                Assert.That(RequiredResponsive<RectTransform>("Resource ThunderChain").Find("Amount").GetComponent<Text>().text,
                    Is.EqualTo(session.Battle.Schools.ThunderChain + "/" + session.Battle.Schools.ThunderLimit));
                Assert.That(RequiredResponsive<RectTransform>("Resource CurseMark").Find("Amount").GetComponent<Text>().text,
                    Is.EqualTo(session.Battle.Schools.CurseMarks.ToString()));
                CoreBattleStatus burn = CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn");
                Assert.That(RequiredResponsive<BattleStatusRow>("Enemy Battle Status Row").transform
                    .Find("Status burn/Amount").GetComponent<Text>().text, Is.EqualTo(burn.Value));
                Assert.That(System.Text.RegularExpressions.Regex.Replace(NavalHudTooltipText("Status burn"), "<[^>]*>", string.Empty),
                    Does.Contain(burn.Tooltip));
            }
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(4));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(startingMana - manaCost));
            Assert.That(session.Battle.Schools.CurseMarks, Is.GreaterThan(0));
            Assert.That(session.Battle.Schools.ThunderChain, Is.GreaterThan(0));
            NavalBattleVisualCapture.Capture(controller, "ResourceRules_02_School_States.png", "四体系出牌后保留界面，法力扣减、咒层和雷链显示实值", "Battle Card");

            CoreBattleCard blood = session.GetBattleCard(session.Battle.Hand.Single());
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, blood.SchoolEffect.Threshold);
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            Button blocked = RequiredResponsive<Button>("Battle Card 0");
            int energy = session.Battle.Energy, health = session.Hull;
            int powder = session.Cargo.Powder, ammunition = session.Cargo.Cannonballs;
            int[] hand = session.Battle.Hand.ToArray();
            Assert.That(blocked.interactable, Is.False);
            blocked.onClick.Invoke();
            yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Hull, Is.EqualTo(health));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(ammunition));
            CollectionAssert.AreEqual(hand, session.Battle.Hand);
            ExecuteEvents.Execute(blocked.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.3f);
            Transform bubble = blocked.transform.Find("Card Keyword Bubble 0");
            Assert.That(bubble.gameObject.activeInHierarchy, Is.True);
            Assert.That(bubble.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.8f));
            string explanation = bubble.Find("Keyword Bubble Text").GetComponent<Text>().text;
            Assert.That(explanation, Does.Contain("生命支付预告").And.Contain("生命不足，不能出牌"));
            Assert.That(explanation, Does.Contain(session.CardUnavailableReason(0)));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
