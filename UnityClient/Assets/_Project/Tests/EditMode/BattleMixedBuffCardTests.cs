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
        [TestCase("plating_suncoin_rampart", NavalBattleCardAction.Polish)]
        [TestCase("card_gunner_precision", NavalBattleCardAction.PrecisionLoad)]
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
        public void RedesignedRokan_AtFullFireReserveKeepsItsAuthoredBlockAndDraw()
        {
            CoreVoyageSession session = CreateSpecialBattle("crew_salt_crown_gunner_rokan");
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            Assert.That(card.Action, Is.EqualTo(NavalBattleCardAction.FireRounds));
            Assert.That(card.Draw, Is.EqualTo(1));
            session.Battle.PlayerFireRounds = card.StrategyLimit;
            int block = session.Battle.PlayerBlock;
            int energy = session.Battle.Energy;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(card.StrategyLimit));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(block + card.Block));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1), "The redesigned source explicitly draws one replacement.");
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - card.Energy));
        }

        [Test]
        public void CappedSchoolFireReserve_WithDraw_RemainsPlayable()
        {
            CoreVoyageSession session = CreateSpecialBattle("crew_twilight_tide_gunner_yilan");
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            session.Battle.PlayerFireRounds = card.SchoolEffect.Limit;
            Assert.That(card.Draw, Is.EqualTo(1));
            Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(card.SchoolEffect.Limit));
        }

        [Test]
        public void CappedMana_WithAuthoredDraw_PaysEnergyAndDrawsWithoutIncreasingMana()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            EnterBattle(session, "card_school_arcane_b01");
            int manaLimit = session.Battle.Schools.ManaLimit;
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, manaLimit);
            int energy = session.Battle.Energy;
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            int index = session.Battle.Hand[0];
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();

            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
            Assert.That(card.Draw, Is.EqualTo(1));
            Assert.That(session.CanPlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - card.Energy));
            Assert.That(session.Battle.Hand, Is.EqualTo(new[] { index }));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(manaLimit));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void CapacityUpgrade_AtFullManaAddsCapacityWithoutRestoringMana()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            EnterBattle(session, "card_school_arcane_b02");
            int capacity = session.Battle.Schools.ManaLimit;
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, capacity);
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(card.Block));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(capacity));
            Assert.That(session.Battle.Schools.ManaLimit, Is.EqualTo(capacity + card.SchoolEffect.Power));
        }

        [TestCase("card_deck_polish")]
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

        [TestCase("plating_suncoin_rampart", 1)]
        [TestCase("card_gunner_precision", 2)]
        public void MasteredMixedBuff_AtCapDoesNotInventDrawAndCopiesPayDynamicCosts(string sourceId, int maximum)
        {
            CoreVoyageSession session = CreateSpecialBattle(sourceId, 840);
            int index = session.Battle.Hand[0];
            CoreBattleCard card = session.GetBattleCard(index);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(maximum));
            Assert.That(card.UpgradeLevel, Is.EqualTo(maximum));
            Assert.That(session.GetSpecialCardExperience(card.Id), Is.EqualTo(840), "The lower effective ceiling preserves earned source experience.");
            Assert.That(card.Energy, Is.EqualTo(1));
            Assert.That(card.Draw, Is.Zero);
            SetStrategy(session, card.Action, card.StrategyLimit);
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            int energy = session.Battle.Energy = 6;
            session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(new[] { index, index, index, index });

            for (int count = 1; count <= 3; count++)
            {
                Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(count));
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                energy -= count;
                Assert.That(session.Battle.Hand, Is.EqualTo(Enumerable.Repeat(index, 4 - count)), "No unauthored draw can recycle the played copy.");
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
            if (sourceId == "card_gunner_precision")
            {
                var legacy = new CoreVoyageSession();
                legacy.ConfigureCrewCapabilities(false, false, false, true, false, false);
                if (experience > 0) legacy.RestoreSpecialCardExperience(new Dictionary<string, int> { { sourceId, experience } });
                EnterBattle(legacy, sourceId);
                return legacy;
            }
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
            CoreBattleCard definition = CoreVoyageSession.CardLibrary.First(card => card.Id == cardId);
            if (!definition.IsAllowedInStage(NavalBattleStage.Preparation))
                Assert.That(session.EndBattleTurn(), Is.True, "Naval-only components enter their legal combat stage.");
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
