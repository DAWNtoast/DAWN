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
        public void EveryCard_UsesItsAuthoredChannelsAndKeepsCostsFixed(int level)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
                if (original.AllowedStages == NavalBattleStages.None) continue;
                CoreBattleCard effective = session.GetBattleCardAtLevel(index, level);
                int effectiveLevel = Math.Min(level, session.GetCardMaximumUpgradeLevel(index));
                CoreCardUpgradeProfile profile = CoreCardUpgradeDesign.Find(original);
                Assert.That(profile, Is.Not.Null, original.Id);
                Assert.That(effective.Id, Is.EqualTo(original.Id));
                Assert.That(effective.UpgradeLevel, Is.EqualTo(effectiveLevel), original.Id);
                Assert.That(effective.Damage, Is.EqualTo(Scaled(original.Damage, profile.Multiplier(effectiveLevel))), original.Id);
                Assert.That(effective.Block, Is.EqualTo(Scaled(original.Block, profile.BlockMultiplier(effectiveLevel))), original.Id);
                Assert.That(effective.Repair, Is.EqualTo(Scaled(original.Repair, profile.RepairMultiplier(effectiveLevel))), original.Id);
                Assert.That(effective.MovementMeters, Is.EqualTo(original.MovementMeters), original.Id);
                Assert.That(effective.NavalActionPointGain, Is.EqualTo(original.NavalActionPointGain + profile.ActionPointBonus(effectiveLevel)), original.Id);
                Assert.That(effective.StrategyAmount, Is.EqualTo(original.Action == NavalBattleCardAction.FireRounds ? original.StrategyAmount : Scaled(original.StrategyAmount, profile.EffectMultiplier(effectiveLevel))), original.Id);
                Assert.That(effective.StrategyLimit, Is.EqualTo(original.StrategyLimit), original.Id);
                Assert.That(effective.Energy, Is.EqualTo(original.Energy), original.Id);
                Assert.That(effective.Draw, Is.EqualTo(original.Draw + (original.Draw > 0 ? profile.DrawBonus(effectiveLevel) : 0)), original.Id);
                if (original.Action == NavalBattleCardAction.FireRounds)
                    Assert.That(effective.BurnDamage, Is.EqualTo(Scaled(original.BurnDamage, profile.EffectMultiplier(effectiveLevel))), original.Id);
                Assert.That(effective.BurnDuration, Is.EqualTo(original.BurnDuration), original.Id);
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
            Assert.That(first.GetBattleCard(index).Damage, Is.EqualTo(19));
            Assert.That(first.GetBattleCard(index).Draw, Is.Zero);
            Assert.That(first.GetBattleCard(index).Energy, Is.EqualTo(1));
            Assert.That(first.GetBattleDeck(NavalBattleStage.Naval).Count(value => value == index), Is.EqualTo(2));
            Assert.That(CoreVoyageSession.CardLibrary[index].Damage, Is.EqualTo(10));
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

        [TestCase("card_boarding_arcane_12")]
        public void FixedFunctionCards_CannotChargeGoldForRanksWithNoBenefit(string id)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(5000, session.CurrentPort);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            int index = Find(id);
            decimal gold = session.ProfileGold;
            Assert.That(session.HasBattleCardUpgradeBenefit(index), Is.False, id);
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero, id);
            Assert.That(session.BattleCardUpgradeUnavailableReason(index), Does.Contain("固定功能牌").And.Contain("不支持金币强化"));
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.GetCardUpgradeLevel(id), Is.Zero);
        }

        [TestCase("card_school_arcane_b01")]
        [TestCase("card_school_arcane_b06")]
        [TestCase("card_school_arcane_b02")]
        public void OneCostUtilityDraw_GainsOneImmediatelyAndCannotSellRedundantRanks(string id)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(5000, session.CurrentPort);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            int index = Find(id);
            Assert.That(session.HasBattleCardUpgradeBenefit(index), Is.True);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(1));
            Assert.That(session.GetCardUpgradeCost(index), Is.GreaterThan(0));
            Assert.That(session.TryUpgradeBattleCard(index, out string reason), Is.True, reason);
            Assert.That(session.GetBattleCard(index).Draw, Is.EqualTo(CoreVoyageSession.CardLibrary[index].Draw + 1));
            decimal gold = session.ProfileGold;
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void Components_AddOneAutomaticCardPerTypeWithoutPollutingBaseDecks()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureComponentBattleCards(new[] { "bow", "sail", "cannon", "plating", "instrument", "bow", "invalid" });
            Assert.That(session.GetComponentBattleCards(NavalBattleStage.Naval).Count, Is.EqualTo(5));
            Assert.That(session.GetComponentBattleCards(NavalBattleStage.Preparation), Is.Empty);
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

        [TestCase("card_deck_polish", 3, 6)]
        [TestCase("card_load_shot", 6, 12)]
        [TestCase("card_gunner_precision", 7, 12)]
        [TestCase("card_special_crew_salt_crown_gunner_rokan", 2, 4)]
        public void FirstUpgrade_ScalesDirectStrategyAmountsButKeepsCapsAndFireRoundCount(string id, int amount, int cap)
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
        public void FirstUpgrade_IncreasesManeuverActionPointsAndScalesGunnerBurnDamage()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, null);
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_approach", 1 }, { "card_special_crew_salt_crown_gunner_rokan", 1 } });
            EnterCombat(session);
            SetHand(session, Find("card_approach"), Find("card_special_crew_salt_crown_gunner_rokan"));
            int actionPoints = session.Battle.NavalActionPoints;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(600f));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(actionPoints + 2));
            Assert.That(session.PlayCard(0), Is.True);
            int expectedBurn = 5;
            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(expectedBurn));
            session.EndBattleTurn();
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            session.Battle.EnemyBlock = 0;
            session.Battle.DistanceMeters = 300;
            session.Cargo.Cannonballs = 10;
            SetHand(session, Find("card_chain_shot"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(expectedBurn));
            int before = session.Battle.EnemyHull;
            session.EndBattleTurn();
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(before - expectedBurn));
        }

        [TestCase("card_load_shot")]
        [TestCase("card_component_instrument")]
        public void ThirdUpgrade_RedrawnCopiesPayEscalatingCostAndResetNextTurn(string id)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            if (id == "card_special_crew_salt_crown_gunner_rokan")
                session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, null);
            session.ConfigureComponentBattleCards(new[] { "instrument" });
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 3 } });
            EnterCombat(session);
            if (id == "card_component_instrument") Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            int index = Find(id);
            SetHand(session, index);
            int baseCost = session.GetBattleCard(index).Energy;
            int energy = session.Battle.Energy = baseCost * 3 + 3;
            for (int count = 0; count < 3; count++)
            {
                Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(baseCost + count));
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                energy -= baseCost + count;
                Assert.That(session.Battle.Hand, Is.EqualTo(new[] { index }));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(count + 1), "Drawing cannot execute a card.");
                Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            }
            Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(baseCost + 3));
            Assert.That(session.PlayCard(0), Is.False, "Redrawing does not bypass the per-ID surcharge.");
            Assert.That(session.Battle.Hand, Is.EqualTo(new[] { index }));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(3));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(index), Is.EqualTo(baseCost));
        }

        [TestCase(5, 1, 6)] [TestCase(5, 2, 8)] [TestCase(5, 3, 10)]
        [TestCase(-5, 2, -8)] [TestCase(int.MaxValue, 3, int.MaxValue)]
        public void NumericalScaling_RoundsHalfAwayFromZeroAndSaturates(int value, int level, int expected) =>
            Assert.That(CoreBattleUpgradeBalance.ScaleNumerical(value, level), Is.EqualTo(expected));

        [TestCase(CoreSchoolMechanic.ArcaneEcho)] [TestCase(CoreSchoolMechanic.ThunderEcho)]
        public void EchoDraw_KeepsItsConditionAndUsesOnlyOneCappedDrawRoute(CoreSchoolMechanic mechanic)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard[] originals = CoreVoyageSession.CardLibrary.Where(card => card.SchoolEffect?.Mechanic == mechanic).ToArray();
            // These legacy mechanics can have no live sources after the field-component redesign.
            foreach (CoreBattleCard original in originals)
                for (int level = 0; level <= 3; level++)
                {
                    CoreBattleCard upgraded = session.GetBattleCardAtLevel(Find(original.Id), level);
                    int bonus = CoreCardUpgradeDesign.Find(original).DrawBonus(level);
                    Assert.That(upgraded.Draw, Is.EqualTo(original.Draw + (original.Draw > 0 ? bonus : 0)));
                    Assert.That(upgraded.SchoolEffect.Power, Is.EqualTo(original.SchoolEffect.Power + (original.Draw == 0 ? bonus : 0)));
                    Assert.That(upgraded.SchoolEffect.Threshold, Is.EqualTo(original.SchoolEffect.Threshold));
                    Assert.That(upgraded.SchoolEffect.Mechanic, Is.EqualTo(mechanic));
                }
            var mixed = new CoreBattleCard("mixed_draw_fixture", "抽牌防重", "策略", 1, 0, 0, 0, 1, 0,
                "抽 {draw}；满足门槛后抽 {schoolPower}", schoolEffect: new CoreSchoolCardEffect(mechanic, power: 2, threshold: 3, upgrades: CoreSchoolUpgradeFields.None));
            var result = (CoreBattleCard)typeof(CoreVoyageSession).GetMethod("BuildUpgradedCardDefinition", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { mixed, 3 });
            Assert.That(result.Draw, Is.EqualTo(2));
            Assert.That(result.SchoolEffect.Power, Is.EqualTo(2), "A card with both draw routes receives the upgrade on only one route.");
            var conditionalOnly = new CoreBattleCard("legacy_conditional_draw", "条件抽牌", "策略", 1, 0, 0, 0, 0, 0,
                "抽 {schoolPower}", schoolEffect: new CoreSchoolCardEffect(mechanic, power: 2, threshold: 3, upgrades: CoreSchoolUpgradeFields.None));
            var conditionalUpgrade = (CoreBattleCard)typeof(CoreVoyageSession).GetMethod("BuildUpgradedCardDefinition", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { conditionalOnly, 3 });
            Assert.That(conditionalUpgrade.Draw, Is.Zero);
            Assert.That(conditionalUpgrade.SchoolEffect.Power, Is.EqualTo(3));
            Assert.That(conditionalUpgrade.SchoolEffect.Threshold, Is.EqualTo(3));
        }

        private static int Scaled(int value, decimal multiplier) =>
            (int)decimal.Round(value * multiplier, 0, MidpointRounding.AwayFromZero);

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
