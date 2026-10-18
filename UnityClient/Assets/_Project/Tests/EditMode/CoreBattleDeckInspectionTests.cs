using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleDeckInspectionTests
    {
        private static readonly NavalBattleStage[] Stages =
        {
            NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding,
        };

        [Test]
        public void BattlePiles_IncludeEveryStageAndItsSourceCardsBeforeThatStageStarts()
        {
            CoreVoyageSession session = StartBattle(withSources: true);
            CoreBattleState battle = session.Battle;
            int[] hand = battle.Hand.ToArray();
            foreach (NavalBattleStage stage in Stages)
            {
                int[] expected = session.GetBattleDeck(stage).Concat(session.GetBonusBattleCards(stage)).ToArray();
                Assert.That(session.GetBonusBattleCards(stage), Is.Not.Empty, "Fixture includes source cards in every stage.");
                IEnumerable<int> actual = battle.GetDrawPile(stage);
                if (stage == battle.Stage) actual = actual.Concat(battle.Hand);
                CollectionAssert.AreEquivalent(expected, actual, stage.ToString());
                Assert.That(battle.GetDiscardPile(stage), Is.Empty);
            }
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            CollectionAssert.AreEqual(hand, battle.Hand, "Inspecting other stages does not draw or move cards.");
        }

        [Test]
        public void StageChanges_PreservePriorPilesAndLiveViewsWithoutMixingDecks()
        {
            CoreVoyageSession session = StartBattle();
            CoreBattleState battle = session.Battle;
            IReadOnlyList<int> preparationDraw = battle.GetDrawPile(NavalBattleStage.Preparation);
            IReadOnlyList<int> preparationDiscard = battle.GetDiscardPile(NavalBattleStage.Preparation);
            int[] remainingPreparation = preparationDraw.ToArray();
            int[] preparationHand = battle.Hand.ToArray();
            int[] boardingDeck = battle.GetDrawPile(NavalBattleStage.Boarding).ToArray();

            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(remainingPreparation, preparationDraw);
            CollectionAssert.AreEqual(preparationHand, preparationDiscard, "Existing views track actual discarded cards.");
            CollectionAssert.AreEqual(boardingDeck, battle.GetDrawPile(NavalBattleStage.Boarding));
            CollectionAssert.AreEquivalent(session.GetBattleDeck(NavalBattleStage.Naval),
                battle.GetDrawPile(NavalBattleStage.Naval).Concat(battle.Hand));

            int[] navalDraw = battle.GetDrawPile(NavalBattleStage.Naval).ToArray();
            int[] navalHand = battle.Hand.ToArray();
            battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(battle, battle.EnemyHeadingDegrees);
            battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            CollectionAssert.AreEqual(navalDraw, battle.GetDrawPile(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(navalHand, battle.GetDiscardPile(NavalBattleStage.Naval));
            CollectionAssert.AreEquivalent(boardingDeck, battle.GetDrawPile(NavalBattleStage.Boarding).Concat(battle.Hand));
            CollectionAssert.AreEqual(preparationHand, preparationDiscard);
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void InspectionViews_CannotMutateEitherPileEvenAfterCasting(NavalBattleStage stage)
        {
            CoreBattleState battle = StartBattle().Battle;
            foreach (IReadOnlyList<int> view in new[] { battle.GetDrawPile(stage), battle.GetDiscardPile(stage) })
            {
                int[] before = view.ToArray();
                var collection = view as IList<int>;
                Assert.That(collection, Is.Not.Null);
                Assert.That(collection.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => collection.Add(0));
                Assert.Throws<NotSupportedException>(() => collection.Clear());
                if (collection.Count > 0)
                    Assert.Throws<NotSupportedException>(() => collection[0] = 0);
                CollectionAssert.AreEqual(before, view);
            }
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        public void InvalidStage_ReturnsEmptyPiles(int stage)
        {
            var battle = new CoreBattleState();
            Assert.That(battle.GetDrawPile((NavalBattleStage)stage), Is.Empty);
            Assert.That(battle.GetDiscardPile((NavalBattleStage)stage), Is.Empty);
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void CardInspection_UsesRequestedStageAndUpgradeWithoutChangingActiveStage(NavalBattleStage activeStage)
        {
            int index = CardIndex("card_tactical_draw");
            CoreVoyageSession session = StartBattle(upgradedCard: index);
            if (activeStage != NavalBattleStage.Preparation) Assert.That(session.EndBattleTurn(), Is.True);
            if (activeStage == NavalBattleStage.Boarding)
            {
                session.Battle.DistanceMeters = 100f;
                typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
                session.Battle.Alignment = NavalBattleAlignment.Parallel;
                Assert.That(session.TryBoard(), Is.True);
            }
            int[] hand = session.Battle.Hand.ToArray();
            Assert.That(session.CardUpgradeLevels["card_tactical_draw"], Is.EqualTo(3), "Keep the historical purchased rank.");
            CoreBattleCard ship = session.GetBattleCard(index, NavalBattleStage.Naval);
            CoreBattleCard boarding = session.GetBattleCard(index, NavalBattleStage.Boarding);
            Assert.That(ship.BoardingSpec, Is.Null);
            Assert.That(ship.UpgradeLevel, Is.EqualTo(2), "The shared card's effective rank respects its highest stage cap.");
            Assert.That(ship.Draw, Is.EqualTo(4), "The authored ship curve draws 2/3/4 cards through its two upgrades.");
            Assert.That(boarding.BoardingSpec, Is.Not.Null);
            Assert.That(boarding.BoardingSpec.Key, Is.EqualTo("basic_08"));
            Assert.That(boarding.UpgradeLevel, Is.EqualTo(2));
            Assert.That(boarding.Draw, Is.EqualTo(5), "The two-energy boarding draw effect gains two cards at its cap.");
            Assert.That(boarding.Id, Is.EqualTo(ship.Id));
            Assert.That(session.GetBattleCard(index).Name,
                Is.EqualTo(activeStage == NavalBattleStage.Boarding ? boarding.Name : ship.Name));
            Assert.That(session.Battle.Stage, Is.EqualTo(activeStage));
            Assert.That(session.CardUpgradeLevels["card_tactical_draw"], Is.EqualTo(3), "Previewing capped definitions does not overwrite saved ranks.");
            CollectionAssert.AreEqual(hand, session.Battle.Hand);
        }

        [Test]
        public void CardInspection_PortDefaultRemainsTheShipDefinition()
        {
            var session = new CoreVoyageSession();
            int index = CardIndex("card_tactical_draw");
            Assert.That(session.GetBattleCard(index).BoardingSpec, Is.Null);
            Assert.That(session.GetBattleCard(index, NavalBattleStage.Boarding).BoardingSpec, Is.Not.Null);
            Assert.That(session.GetBattleCard(-1, NavalBattleStage.Naval), Is.Null);
            Assert.That(session.GetBattleCard(CoreVoyageSession.CardLibrary.Count, NavalBattleStage.Boarding), Is.Null);
        }

        [Test]
        public void CardInspection_GuidedBossKeepsItsAuthoredBaseCardsInEveryTab()
        {
            var session = new CoreVoyageSession();
            int index = CardIndex("card_tactical_draw");
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_tactical_draw", 3 } });
            Assert.That(session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg), Is.True);
            session.StartVoyage();
            session.SetHeading(SailingDirection.East);
            session.SetDistance(120);
            for (int step = 0; step < 5 && session.Phase == CoreDemoPhase.Sailing; step++)
                Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.IsGuidedBossBattle, Is.True);
            foreach (NavalBattleStage stage in Stages)
            {
                CoreBattleCard card = session.GetBattleCard(index, stage);
                CoreBattleCard authored = CoreVoyageSession.CardLibrary[index];
                if (stage == NavalBattleStage.Boarding) authored = CoreBoardingCardCatalog.ForBoarding(authored);
                Assert.That(card.Id, Is.EqualTo(authored.Id));
                Assert.That(card.Energy, Is.EqualTo(authored.Energy));
                Assert.That(card.Draw, Is.EqualTo(authored.Draw));
                Assert.That(card.Damage, Is.EqualTo(authored.Damage));
                Assert.That(card.UpgradeLevel, Is.Zero);
            }
            Assert.That(session.GetCardUpgradeLevel("card_tactical_draw"), Is.EqualTo(2), "The effective rank respects the current card cap.");
            Assert.That(session.CardUpgradeLevels["card_tactical_draw"], Is.EqualTo(3), "Tutorial inspection does not overwrite historical profile upgrades.");
        }

        private static CoreVoyageSession StartBattle(bool withSources = false, int upgradedCard = -1)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(withSources, withSources, withSources, withSources, withSources, withSources);
            if (withSources) session.ConfigureComponentBattleCards(new[] { "bow", "sail", "cannon", "plating", "instrument" });
            if (upgradedCard >= 0)
                session.ConfigureCardUpgrades(new Dictionary<string, int> { { CoreVoyageSession.CardLibrary[upgradedCard].Id, 3 } });
            session.SetSeed("BATTLE-DECK-INSPECTION");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            return session;
        }

        private static int CardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
