using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingCardChoicePresentationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [SetUp]
        public void CreateBoardingPreview()
        {
            host = new GameObject("Boarding Choice Presentation Audit");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            typeof(CoreGameplayDemoController).GetField("editorPreviewSuppressed", Private).SetValue(controller, true);
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", Private).Invoke(session,
                new object[] { NavalBattleStage.Boarding });
            typeof(CoreGameplayDemoController).GetField("session", Private).SetValue(controller, session);
            typeof(CoreGameplayDemoController).GetMethod("BuildCanvas", Private).Invoke(controller, new object[] { "Boarding Choice Audit Canvas" });
            host.SetActive(true);
            controller.Session.Battle.Energy = 10;
        }

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void BurnCardChoice_CancelPreservesCardsResourcesAndLife()
        {
            CoreVoyageSession session = controller.Session;
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Index("fire_07"));
            session.Battle.Hand.Add(Index("basic_01"));
            session.Battle.Hand.Add(Index("basic_04"));
            int[] hand = session.Battle.Hand.ToArray();
            int[] draw = session.Battle.DrawPile.ToArray();
            int energy = session.Battle.Energy, powder = session.Cargo.Powder, life = session.Battle.PlayerBoardingHealth;
            GameObject source = new GameObject("Choice Source", typeof(RectTransform), typeof(Image), typeof(Button));
            source.transform.SetParent(host.transform, false);
            bool opened = (bool)typeof(CoreGameplayDemoController).GetMethod("TryBeginBoardingCardChoice", Private)
                .Invoke(controller, new object[] { source.GetComponent<Button>(), 0, session.GetBattleCard(hand[0]) });
            Assert.That(opened, Is.True);
            Assert.That(controller.IsBoardingCardChoiceOpen, Is.True);
            Button[] buttons = host.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Any(button => button.name.StartsWith("Boarding Resource ")), Is.False,
                "Resource quantities are fixed or all; only card targets are chosen.");
            buttons.Single(button => button.name == "Boarding Candidate 1").onClick.Invoke();
            buttons = host.GetComponentsInChildren<Button>(true);
            buttons.Single(button => button.name == "Boarding Candidate 2" && button.gameObject.activeInHierarchy).onClick.Invoke();
            buttons = host.GetComponentsInChildren<Button>(true);
            buttons.Single(button => button.name == "Cancel Boarding Card Choice" && button.gameObject.activeInHierarchy).onClick.Invoke();
            Assert.That(controller.IsBoardingCardChoiceOpen, Is.False);
            CollectionAssert.AreEqual(hand, session.Battle.Hand);
            CollectionAssert.AreEqual(draw, session.Battle.DrawPile);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(life));
        }

        [TestCase("arcane_04")]
        [TestCase("blood_02")]
        [TestCase("tide_06")]
        public void FixedOrAllResourceCards_DoNotOpenAChoicePanel(string key)
        {
            CoreVoyageSession session = controller.Session;
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(key));
            bool opened = (bool)typeof(CoreGameplayDemoController).GetMethod("TryBeginBoardingCardChoice", Private)
                .Invoke(controller, new object[] { null, 0, session.GetBattleCard(session.Battle.Hand[0]) });
            Assert.That(opened, Is.False);
            Assert.That(controller.IsBoardingCardChoiceOpen, Is.False);
        }

        [TestCase("arcane_04", "mana", "全")]
        [TestCase("arcane_05", "mana", "2")]
        [TestCase("curse_15", "curse", "2")]
        [TestCase("tide_06", "tide", "全")]
        [TestCase("blood_02", "health", "4")]
        public void BoardingResourceBadges_ShowFixedPaymentOrAll(string key, string resource, string value)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.EqualTo(resource));
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.EqualTo(value));
        }

        [Test]
        public void BoardingFormulaQuotes_AreCurrentAndDoNotSpendResources()
        {
            CoreVoyageSession session = controller.Session;
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 3);
            typeof(CoreSchoolBattleState).GetProperty("ThunderChain").SetValue(session.Battle.Schools, 4);
            int energy = session.Battle.Energy;
            CoreBattleCard flood = CoreBoardingCardCatalog.FindByKey("arcane_04");
            CoreBattleCard combo = CoreBoardingCardCatalog.FindByKey("thunder_11");
            Assert.That(session.GetBattleCardDamage(flood, NavalBattleStage.Boarding), Is.EqualTo(42));
            Assert.That(session.GetBattleCardEffects(combo, NavalBattleStage.Boarding).Damage, Is.EqualTo(14));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(4));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
        }

        [Test]
        public void BoardingDescriptions_KeepAuthoredResourcesAndTiming()
        {
            foreach (CoreBattleCard card in CoreBoardingCardCatalog.All)
            {
                Assert.That(controller.Session.BattleCardEffectDescription(card, NavalBattleStage.Boarding), Is.EqualTo(card.Description), card.Id);
                Assert.That(controller.Session.BattleCardFaceDescription(card, NavalBattleStage.Boarding), Is.EqualTo(card.Description), card.Id);
                Assert.That(controller.Session.BattleCardPortDescription(card), Is.EqualTo(card.Description), card.Id);
            }
        }

        private static int Index(string key)
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key) return index;
            throw new AssertionException("Missing boarding card " + key);
        }
    }
}
