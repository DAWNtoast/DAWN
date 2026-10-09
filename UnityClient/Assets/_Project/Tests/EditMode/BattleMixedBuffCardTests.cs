using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleMixedBuffCardTests
    {
        [TestCase("plating_freedom_ark", NavalBattleCardAction.Polish)]
        [TestCase("plating_dragonwake_armor", NavalBattleCardAction.Load)]
        [TestCase("crew_bronze_bell_gunner_helen", NavalBattleCardAction.PrecisionLoad)]
        [TestCase("crew_salt_crown_gunner_rokan", NavalBattleCardAction.FireRounds)]
        public void CappedMixedBuffAtFullHull_StillGrantsBlockWithoutIncreasingStrategy(string sourceId, NavalBattleCardAction action)
        {
            CoreVoyageSession session = CreateSpecialBattle(sourceId);
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            Assert.That(card.Action, Is.EqualTo(action));
            Assert.That(card.Draw, Is.Zero, "This regression must not be masked by a draw effect.");
            Assert.That(card.Block, Is.GreaterThan(0));
            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
            SetStrategy(session, card.Action, card.StrategyLimit);
            int beforeBlock = session.Battle.PlayerBlock;
            int beforeEnergy = session.Battle.Energy;
            int expectedBlock = card.Block + session.Battle.PlayerDeckBuff;

            Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(beforeBlock + expectedBlock));
            Assert.That(session.Battle.Energy, Is.EqualTo(beforeEnergy - card.Energy));
            Assert.That(session.Hull, Is.EqualTo(session.MaxHull), "Unused repair must not disable the block effect.");
            Assert.That(GetStrategy(session, card.Action), Is.EqualTo(card.StrategyLimit));
        }

        [Test]
        public void CappedRepairBuff_WhenHullIsDamaged_RepairsWithoutIncreasingStrategy()
        {
            CoreVoyageSession session = CreateSpecialBattle("crew_bronze_bell_carpenter_osser");
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            SetStrategy(session, card.Action, card.StrategyLimit);
            SetHull(session, session.MaxHull - 9);
            int beforeHull = session.Hull;

            Assert.That(card.Block, Is.Zero);
            Assert.That(card.Draw, Is.Zero);
            Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Hull, Is.EqualTo(beforeHull + card.Repair));
            Assert.That(GetStrategy(session, card.Action), Is.EqualTo(card.StrategyLimit));
        }

        [Test]
        public void CappedRepairBuff_AtFullHullWithoutOtherBenefits_IsStillUnavailable()
        {
            CoreVoyageSession session = CreateSpecialBattle("crew_bronze_bell_carpenter_osser");
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            SetStrategy(session, card.Action, card.StrategyLimit);
            int energy = session.Battle.Energy;

            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
            Assert.That(session.CanPlayCard(0), Is.False);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
        }

        [Test]
        public void RepairBuff_AtFullHullWithRoomForStrategy_RemainsPlayable()
        {
            CoreVoyageSession session = CreateSpecialBattle("crew_bronze_bell_carpenter_osser");
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            SetStrategy(session, card.Action, 0);

            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(GetStrategy(session, card.Action), Is.EqualTo(card.StrategyAmount));
            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
        }

        [TestCase("card_deck_polish")]
        [TestCase("card_load_shot")]
        public void PureBuff_AtCapWithoutOtherBenefits_RemainsUnavailable(string cardId)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            EnterBattle(session, cardId);
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            SetStrategy(session, card.Action, card.StrategyLimit);
            int energy = session.Battle.Energy;

            Assert.That(session.CanPlayCard(0), Is.False);
            Assert.That(session.CardUnavailableReason(0), Does.Contain("已达"));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(GetStrategy(session, card.Action), Is.EqualTo(card.StrategyLimit));
        }

        [TestCase("plating_freedom_ark")]
        [TestCase("plating_dragonwake_armor")]
        [TestCase("crew_bronze_bell_gunner_helen")]
        [TestCase("crew_salt_crown_gunner_rokan")]
        [TestCase("crew_bronze_bell_carpenter_osser")]
        public void MasteredMixedBuff_AtCapCyclesOnlyWhileDynamicCostIsAffordable(string sourceId)
        {
            CoreVoyageSession session = CreateSpecialBattle(sourceId, 840);
            int index = session.Battle.Hand[0];
            CoreBattleCard card = session.GetBattleCard(index);
            Assert.That(card.UpgradeLevel, Is.EqualTo(3));
            Assert.That(card.Energy, Is.Zero);
            Assert.That(card.Draw, Is.EqualTo(1));
            SetStrategy(session, card.Action, card.StrategyLimit);
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            int energy = session.Battle.Energy;

            for (int count = 1; count <= 3; count++)
            {
                Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(count - 1));
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                energy -= count - 1;
                Assert.That(session.Battle.Hand, Is.EqualTo(new[] { index }));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(count));
                Assert.That(session.Battle.Energy, Is.EqualTo(energy));
                Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
                Assert.That(GetStrategy(session, card.Action), Is.EqualTo(card.StrategyLimit));
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            }
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(3));
            Assert.That(GetStrategy(session, card.Action), Is.EqualTo(card.StrategyLimit));
        }

        private static CoreVoyageSession CreateSpecialBattle(string sourceId, int experience = 0)
        {
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(sourceId) ?? CoreSpecialCardCatalog.ForComponent(sourceId);
            Assert.That(definition, Is.Not.Null, sourceId);
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(
                definition.SourceKind == CoreSpecialCardSourceKind.Crew ? new[] { sourceId } : Array.Empty<string>(),
                definition.SourceKind == CoreSpecialCardSourceKind.Component ? new[] { sourceId } : Array.Empty<string>());
            if (experience > 0)
                session.RestoreSpecialCardExperience(new Dictionary<string, int> { { definition.Card.Id, experience } });
            EnterBattle(session, definition.Card.Id);
            return session;
        }

        private static void EnterBattle(CoreVoyageSession session, string cardId)
        {
            session.SetSeed("MIXED-BUFF-CARD-TEST");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("mixed_buff_test", "混合增益测试敌舰", "海战", "测试", true, false, 1, 70), false });
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .First(index => CoreVoyageSession.CardLibrary[index].Id == cardId));
        }

        private static void SetHull(CoreVoyageSession session, int hull) =>
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, hull);

        private static void SetStrategy(CoreVoyageSession session, NavalBattleCardAction action, int amount)
        {
            if (action == NavalBattleCardAction.Polish) session.Battle.PlayerDeckBuff = amount;
            else if (action == NavalBattleCardAction.FireRounds) session.Battle.PlayerFireRounds = amount;
            else session.Battle.PlayerLoadedShot = amount;
        }

        private static int GetStrategy(CoreVoyageSession session, NavalBattleCardAction action) =>
            action == NavalBattleCardAction.Polish ? session.Battle.PlayerDeckBuff :
            action == NavalBattleCardAction.FireRounds ? session.Battle.PlayerFireRounds : session.Battle.PlayerLoadedShot;
    }
}
