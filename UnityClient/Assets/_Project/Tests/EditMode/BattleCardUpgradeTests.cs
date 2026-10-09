using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleCardUpgradeTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryCard_UsesTheSameCumulativeThreeUpgradeRules(int level)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
                if (original.AllowedStages == NavalBattleStages.None) continue;
                CoreBattleCard effective = session.GetBattleCardAtLevel(index, level);
                int multiplier = level == 0 ? 1 : 2;
                Assert.That(effective.Id, Is.EqualTo(original.Id));
                Assert.That(effective.UpgradeLevel, Is.EqualTo(level));
                Assert.That(effective.Damage, Is.EqualTo(original.Damage * multiplier), original.Id);
                Assert.That(effective.Block, Is.EqualTo(original.Block * multiplier), original.Id);
                Assert.That(effective.Repair, Is.EqualTo(original.Repair * multiplier), original.Id);
                Assert.That(effective.MovementMeters, Is.EqualTo(original.MovementMeters * multiplier), original.Id);
                Assert.That(effective.StrategyAmount, Is.EqualTo(original.StrategyAmount * multiplier), original.Id);
                Assert.That(effective.StrategyLimit, Is.EqualTo(original.StrategyLimit * multiplier), original.Id);
                Assert.That(effective.Energy, Is.EqualTo(Math.Max(0, original.Energy - (level >= 2 ? 1 : 0))), original.Id);
                Assert.That(effective.Draw, Is.EqualTo(original.Draw + (level == 3 ? 1 : 0)), original.Id);
                Assert.That(effective.PowderCost, Is.EqualTo(original.PowderCost));
                Assert.That(effective.CannonballCost, Is.EqualTo(original.CannonballCost));
                Assert.That(effective.Description, Is.Not.Empty);
            }
        }

        [Test]
        public void Upgrades_ArePerStableIdSharedByCopiesAndNeverMutateLibraryOrOtherSessions()
        {
            int index = Find("card_chain_shot");
            CoreVoyageSession first = new CoreVoyageSession();
            first.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_chain_shot", 3 } });
            Assert.That(first.GetBattleCard(index).Damage, Is.EqualTo(16));
            Assert.That(first.GetBattleCard(index).Draw, Is.EqualTo(1));
            Assert.That(first.GetBattleCard(index).Energy, Is.Zero);
            Assert.That(first.GetBattleDeck(NavalBattleStage.Naval).Count(value => value == index), Is.EqualTo(2));
            Assert.That(CoreVoyageSession.CardLibrary[index].Damage, Is.EqualTo(8));
            Assert.That(new CoreVoyageSession().GetBattleCard(index).UpgradeLevel, Is.Zero);
            Assert.That(first.GetBattleCardAtLevel(index, 1).UpgradeLevel, Is.EqualTo(1));
            Assert.That(first.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(3), "Previews cannot modify progress.");
        }

        [Test]
        public void Configure_ClampsKnownIdsCopiesItsInputAndRejectsMutationAtSea()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            Dictionary<string, int> input = new Dictionary<string, int>
            {
                { "card_chain_shot", 99 }, { "card_brace", -3 }, { "invalid_card", 3 },
            };
            session.ConfigureCardUpgrades(input);
            input["card_chain_shot"] = 0;
            Assert.That(session.CardUpgradeLevels.Count, Is.EqualTo(1));
            Assert.That(session.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(3));
            session.ConfigureCardUpgrades(session.CardUpgradeLevels);
            Assert.That(session.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(3));
            session.StartVoyage();
            session.ConfigureCardUpgrades(null);
            Assert.That(session.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(3));
        }

        [Test]
        public void Purchase_PaysOnlyProfileGoldExactlyOnceAndStopsAfterThreeUpgrades()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreProfile(5000, session.CurrentPort);
            int index = Find("card_chain_shot");
            int cash = session.Cargo.Gold;
            decimal gold = session.ProfileGold;
            for (int level = 1; level <= 3; level++)
            {
                int cost = session.GetCardUpgradeCost(index);
                Assert.That(cost, Is.GreaterThan(0));
                Assert.That(session.TryUpgradeBattleCard(index, out string reason), Is.True, reason);
                gold -= cost;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Gold, Is.EqualTo(cash));
                Assert.That(session.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(level));
                Assert.That(reason, Is.Not.Empty);
            }
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void Purchase_RequiresPortMoneyAndNeverAllowsSpecialistOrComponentCards()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.RestoreProfile(5000, session.CurrentPort);
            Assert.That(session.TryUpgradeBattleCard(Find("card_special_crew_twilight_tide_gunner_yilan"), out _), Is.False);
            Assert.That(session.TryUpgradeBattleCard(Find("card_component_cannon"), out _), Is.False);
            session.ConfigureSpecialCardSources(new[] { "crew_twilight_tide_gunner_yilan" }, null);
            session.ConfigureComponentBattleCards(new[] { "cannon" });
            Assert.That(session.TryUpgradeBattleCard(Find("card_special_crew_twilight_tide_gunner_yilan"), out _), Is.False);
            Assert.That(session.TryUpgradeBattleCard(Find("card_component_cannon"), out _), Is.False);
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_component_cannon", 1 } });
            session.ConfigureComponentBattleCards(null);
            Assert.That(session.GetCardUpgradeLevel("card_component_cannon"), Is.EqualTo(1), "Unequipping must not erase upgrades.");
            session.RestoreProfile(0, session.CurrentPort);
            Assert.That(session.TryUpgradeBattleCard(0, out _), Is.False);
            session.RestoreProfile(5000, session.CurrentPort);
            session.StartVoyage();
            Assert.That(session.TryUpgradeBattleCard(0, out _), Is.False);
        }

        [Test]
        public void Components_AddOneAutomaticCardPerTypeWithoutPollutingBaseDecks()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureComponentBattleCards(new[] { "bow", "sail", "cannon", "plating", "instrument", "bow", "invalid" });
            Assert.That(session.GetComponentBattleCards(NavalBattleStage.Naval).Count, Is.EqualTo(5));
            Assert.That(session.GetComponentBattleCards(NavalBattleStage.Preparation).Count, Is.EqualTo(3));
            Assert.That(session.GetComponentBattleCards(NavalBattleStage.Boarding), Is.Empty);
            foreach (int index in session.GetComponentBattleCards(NavalBattleStage.Naval))
                Assert.That(session.GetAvailableBattleCards(NavalBattleStage.Naval), Has.No.Member(index));
            int[] baseDeck = session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            baseDeck[0] = Find("card_component_cannon");
            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, baseDeck), Is.False);
            EnterCombat(session);
            session.EndBattleTurn();
            int[] pile = session.Battle.Hand.Concat(session.Battle.DrawPile).Concat(session.Battle.DiscardPile).ToArray();
            foreach (int index in session.GetComponentBattleCards(NavalBattleStage.Naval))
                Assert.That(pile.Count(value => value == index), Is.EqualTo(1));
        }

        [TestCase("card_deck_polish", 4, 12)]
        [TestCase("card_load_shot", 8, 24)]
        [TestCase("card_gunner_precision", 16, 24)]
        [TestCase("card_special_crew_salt_crown_gunner_rokan", 4, 8)]
        public void FirstUpgrade_ActuallyDoublesHardCodedStrategyEffects(string id, int amount, int cap)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            if (id == "card_special_crew_salt_crown_gunner_rokan")
                session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, null);
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 1 } });
            EnterCombat(session);
            SetHand(session, Find(id));
            Assert.That(session.PlayCard(0), Is.True);
            int actual = id == "card_deck_polish" ? session.Battle.PlayerDeckBuff : session.GetBattleCard(Find(id)).Action == NavalBattleCardAction.FireRounds ? session.Battle.PlayerFireRounds : session.Battle.PlayerLoadedShot;
            Assert.That(actual, Is.EqualTo(amount));
            Assert.That(session.GetBattleCard(Find(id)).StrategyLimit, Is.EqualTo(cap));
        }

        [Test]
        public void FirstUpgrade_DoublesManeuverAndGunnerBurnDamage()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, null);
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_approach", 1 }, { "card_special_crew_salt_crown_gunner_rokan", 1 } });
            EnterCombat(session);
            SetHand(session, Find("card_approach"), Find("card_special_crew_salt_crown_gunner_rokan"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(300f));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(10));
            session.EndBattleTurn();
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            session.Battle.EnemyBlock = 0;
            session.Cargo.Cannonballs = 10;
            SetHand(session, Find("card_chain_shot"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(10));
            int before = session.Battle.EnemyHull;
            session.EndBattleTurn();
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(before - 10));
        }

        [TestCase("card_brace")]
        [TestCase("card_deck_polish")]
        [TestCase("card_load_shot")]
        [TestCase("card_special_crew_salt_crown_gunner_rokan")]
        [TestCase("card_component_instrument")]
        public void ThirdUpgrade_RedrawnCopiesPayEscalatingCostAndResetNextTurn(string id)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            if (id == "card_special_crew_salt_crown_gunner_rokan")
                session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, null);
            session.ConfigureComponentBattleCards(new[] { "instrument" });
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 3 } });
            EnterCombat(session);
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            int index = Find(id);
            SetHand(session, index);
            int energy = session.Battle.Energy;
            for (int count = 0; count < 3; count++)
            {
                Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(count));
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                energy -= count;
                Assert.That(session.Battle.Hand, Is.EqualTo(new[] { index }));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(count + 1), "Drawing cannot execute a card.");
                Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            }
            Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(3));
            Assert.That(session.PlayCard(0), Is.False, "Redrawing does not bypass the per-ID surcharge.");
            Assert.That(session.Battle.Hand, Is.EqualTo(new[] { index }));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(3));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(index), Is.Zero);
        }

        private static int Find(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static void SetHand(CoreVoyageSession session, params int[] cards) { session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(cards); }
        private static void EnterCombat(CoreVoyageSession session)
        {
            session.SetSeed("UPGRADE-CORE-TEST");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("upgrade_test", "强化测试敌舰", "海战", "测试", true, false, 1, 70), false });
        }
    }
}
