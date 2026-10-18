using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalTacticsTests
    {
        [Test]
        public void NewBattle_StartsSixHundredMetresApartFacingEachOther()
        {
            CoreBattleState battle = NewBattle().Battle;
            Assert.That(CoreVoyageSession.NavalGridCellMeters, Is.EqualTo(100f));
            AssertPoint(battle.PlayerPositionMeters, -600f, 0f);
            AssertPoint(battle.EnemyPositionMeters, 0f, 0f);
            Assert.That(battle.PlayerHeadingDegrees, Is.EqualTo(0f));
            Assert.That(battle.EnemyHeadingDegrees, Is.EqualTo(180f));
            Assert.That(battle.DistanceMeters, Is.EqualTo(600f));
        }

        [Test]
        public void PreparationWithoutMovement_EntersNavalAtTheOriginalSixHundredMetres()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(600f));
            AssertPoint(session.Battle.PlayerPositionMeters, -600f, 0f);
            AssertPoint(session.Battle.EnemyPositionMeters, 0f, 0f);
        }

        [Test]
        public void DuplicateNavigationCards_ResolveToApproachWithoutEnteringStarterDecks()
        {
            var session = new CoreVoyageSession();
            foreach (string id in new[] { "card_turn_port", "card_turn_starboard", "card_turn_about", "card_sail_forward" })
            {
                int index = CardIndex(id);
                CoreBattleCard card = session.GetBattleCard(index);
                Assert.That(session.GetBattleDeck(NavalBattleStage.Preparation).Contains(index), Is.False);
                Assert.That(session.GetBattleDeck(NavalBattleStage.Naval).Contains(index), Is.False);
                Assert.That(session.GetBattleDeck(NavalBattleStage.Boarding).Contains(index), Is.False);
                Assert.That(card.Id, Is.EqualTo("card_approach"));
                Assert.That(card.ArtId, Is.EqualTo("card_full_sail_turn"));
                Assert.That(card.Energy, Is.Zero);
            }
            Assert.That(CoreVoyageSession.CardLibrary[0].Id, Is.EqualTo("card_chain_shot"));
            Assert.That(CoreVoyageSession.CardLibrary[10].Id, Is.EqualTo("card_approach"));
            Assert.That(session.GetBattleDeck(NavalBattleStage.Naval).Count, Is.LessThanOrEqualTo(CoreVoyageSession.MaximumBaseBattleDeckSize));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void LegacyStarterDeck_MergesNavigationDuplicatesWhileCanonicalCustomOrderIsPreserved(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            int[] legacy = (stage == NavalBattleStage.Preparation
                ? new[] { 8, 8, 9, 9, 10, 10, 11, 12, 13, 7 }
                : new[] { 0, 0, 6, 10, 11, 13, 12, 9, 2, 5 }).Concat(new[]
                { "card_energy_order", "card_tactical_draw", "card_turn_port", "card_turn_starboard", "card_turn_about", "card_sail_forward" }
                .Select(CardIndex)).ToArray();
            int[] snapshot = (int[])legacy.Clone();
            Assert.That(session.RestoreBattleDeck(stage, legacy), Is.True);
            Assert.That(session.GetBattleDeck(stage).All(index => index == CoreVoyageSession.CanonicalBattleCardIndex(index)), Is.True);
            Assert.That(session.GetBattleDeck(stage).Count(index => index == CardIndex("card_approach")), Is.EqualTo(2));
            int[] custom = session.GetBattleDeck(stage).Reverse().ToArray();
            Assert.That(session.RestoreBattleDeck(stage, custom), Is.True);
            CollectionAssert.AreEqual(custom, session.GetBattleDeck(stage));
            CollectionAssert.AreEqual(snapshot, legacy, "Input save data is never mutated.");
        }

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            session.SetSeed("NAVAL-TACTICS-TEST");
            session.StartVoyage();
            var encounter = new CoreEncounter("tactics_test", "测试敌舰", "海战", "战棋测试", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            return session;
        }

        private static int CardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static void Play(CoreVoyageSession session, string id)
        {
            session.Battle.Energy = 100;
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(CardIndex(id));
            Assert.That(session.PlayCard(0), Is.True, id);
        }

        private static void AssertPoint(VoyagePoint point, float x, float y)
        {
            Assert.That(point.X, Is.EqualTo(x).Within(0.01f));
            Assert.That(point.Y, Is.EqualTo(y).Within(0.01f));
        }
    }
}
