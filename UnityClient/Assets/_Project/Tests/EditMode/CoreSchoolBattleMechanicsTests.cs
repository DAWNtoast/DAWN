using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSchoolBattleMechanicsTests
    {
        [Test]
        public void SchoolQuotes_UseFinalValuesOnce_AndSelectiveUpgradeFields()
        {
            var session = new CoreVoyageSession();
            int card = Index("card_school_fire_b07");
            CoreBattleCard zero = session.GetBattleCardAtLevel(card, 0);
            CoreBattleCard one = session.GetBattleCardAtLevel(card, 1);
            CoreBattleCard two = session.GetBattleCardAtLevel(card, 2);
            Assert.That(zero.Damage, Is.GreaterThan(0));
            Assert.That(one.Damage, Is.EqualTo(CoreBattleUpgradeBalance.ScaleNumerical(zero.Damage, 1)));
            Assert.That(one.SchoolEffect.Power, Is.EqualTo(CoreBattleUpgradeBalance.ScaleNumerical(zero.SchoolEffect.Power, 1)));
            Assert.That(one.SchoolEffect.Duration, Is.EqualTo(2));
            Assert.That(two.Damage, Is.EqualTo(CoreBattleUpgradeBalance.ScaleNumerical(zero.Damage, 2)));
            Assert.That(two.Energy, Is.EqualTo(zero.Energy));
            Assert.That(session.GetBattleCardEffects(zero, NavalBattleStage.Naval).Damage, Is.EqualTo(zero.Damage));
            Assert.That(session.GetBattleCardEffects(session.GetBattleCardAtLevel(Index("card_chain_shot"), 0), NavalBattleStage.Naval).Damage, Is.EqualTo(10));
            Assert.That(session.GetBattleCardAtLevel(Index("card_school_fire_b10"), 1).SchoolEffect.Power, Is.EqualTo(1));
        }

        [TestCase(0, 5, 10)]
        [TestCase(3, 10, 20)]
        public void ReducedArcaneBaseGuardKeepsItsPaidBonusAndManaCost(int rank, int baseGuard, int paidBonus)
        {
            CoreVoyageSession s = Battle(upgradedCard: "card_school_arcane_b05", rank: rank);
            CoreBattleCard ward = s.GetBattleCard(Index("card_school_arcane_b05"));
            Assert.That(ward.Block, Is.EqualTo(baseGuard));
            Assert.That(ward.SchoolEffect.Power, Is.EqualTo(paidBonus));
            Assert.That(ward.SchoolEffect.Threshold, Is.EqualTo(2));
            Assert.That(s.GetBattleCardEffects(ward, NavalBattleStage.Naval).Block, Is.EqualTo(baseGuard),
                "An unfunded quote cannot include the mana-paid shield.");
            Play(s, "card_school_arcane_b06");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(2));
            Assert.That(s.GetBattleCardEffects(ward, NavalBattleStage.Naval).Block, Is.EqualTo(baseGuard + paidBonus),
                "The authored basic shield and paid bonus each keep their own upgrade scaling.");
            Play(s, ward.Id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(baseGuard + paidBonus));
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
        }

        [Test]
        public void NegativeMovement_SurvivesUpgrade_AndActuallyRetreats()
        {
            CoreVoyageSession s = Battle();
            CoreBattleCard original = Card(s, "card_school_fire_b06");
            CoreBattleCard one = s.GetBattleCardAtLevel(Index("card_school_fire_b06"), 1);
            Assert.That(original.SchoolEffect.Movement, Is.LessThan(0));
            Assert.That(one.SchoolEffect.Movement, Is.EqualTo(original.SchoolEffect.Movement));
            Assert.That(one.MovementMeters, Is.EqualTo(-original.SchoolEffect.Movement));
            s.Battle.DistanceMeters = 100;
            s.Battle.PlayerMovingTowardEnemy = true;
            Play(s, "card_school_fire_b06");
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(100 + original.MovementMeters));
            Assert.That(s.Battle.PlayerMovingTowardEnemy, Is.False);
        }

        [Test]
        public void Fire_UsesAuthoredValuesAtLowCrew_AndExtendsToThree()
        {
            CoreVoyageSession s = Battle(true, 7);
            int burn = Card(s, "card_school_fire_b07").SchoolEffect.Power;
            CoreBattleCard guard = Card(s, "card_school_fire_b09");
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(burn));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
            Play(s, "card_school_fire_b09");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard.Block + guard.SchoolEffect.Power));
            Play(s, "card_school_fire_b10");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(3));
            int hp = s.Battle.EnemyBoardingHealth;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - burn));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
        }

        [Test]
        public void SameIdCopies_KeepEscalatingCosts_AndStopWithinThreeAp()
        {
            CoreVoyageSession s = Battle(true, 28, "card_school_fire_b07");
            int id = Index("card_school_fire_b07");
            s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(new[] { id, id, id, id });
            Assert.That(s.GetBattleCardEnergyCost(id), Is.EqualTo(1), "Upgrades no longer discount energy.");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.GetBattleCardEnergyCost(id), Is.EqualTo(2));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.GetBattleCardEnergyCost(id), Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(2));
        }

        [Test]
        public void FireNaturalGuard_ReactsOnlyToNaturalTick_AndExpiresAfterTwoEnemyTurns()
        {
            CoreVoyageSession s = Battle(true);
            Play(s, "card_school_fire_b07");
            Play(s, Art("card_school_fire_e04"));
            s.Battle.PlayerBlock = 0;
            s.Battle.Profile = Profile(10);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.NaturalFireGuardTurns, Is.EqualTo(1));
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(Math.Min(10, Card(s, Art("card_school_fire_e04")).SchoolEffect.Power)));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.NaturalFireGuardTurns, Is.Zero);
        }

        [Test]
        public void ExtraTick_DoesNotConsumeDuration_OrRepeatInSameWindow()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_school_fire_b03");
            int before = s.Battle.EnemyHull;
            CoreBattleCard card = Card(s, Art("card_school_fire_l03"));
            int tick = Math.Max(s.Battle.EnemyBurnDamage, card.SchoolEffect.Power);
            Play(s, Art("card_school_fire_l03"));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - card.Damage - tick));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.ExtraFireTickUsed, Is.True);
            s.Battle.Energy = 20;
            before = s.Battle.EnemyHull;
            Play(s, Art("card_school_fire_l03"), false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - card.Damage));
        }

        [Test]
        public void Overflow_OnlyRewardsAlreadyFullExtension_AndClearsContributionOnShot()
        {
            CoreVoyageSession s = Battle();
            CoreSchoolCardEffect overflow = Card(s, Art("card_school_fire_l05")).SchoolEffect;
            int gain = Math.Min(overflow.Power, Math.Min(overflow.Limit, overflow.Threshold));
            Play(s, Art("card_school_fire_l05"));
            s.Battle.EnemyBurnDamage = 6; s.Battle.EnemyBurnTurns = 3;
            Play(s, Art("card_school_fire_e02"));
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            Assert.That(s.PlayerLoadedShotDamageBonus, Is.EqualTo(gain));
            Assert.That(s.Battle.Schools.OverflowContribution, Is.EqualTo(gain));
            s.Battle.Energy = 20;
            Play(s, Art("card_school_fire_e02"), false);
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            Assert.That(s.PlayerLoadedShotDamageBonus, Is.EqualTo(gain));
            s.Battle.DistanceMeters = 200;
            Play(s, "card_school_fire_b03");
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            Assert.That(s.Battle.Schools.OverflowContribution, Is.Zero);
        }

        [Test]
        public void LegacyLoadAndSchoolOverflow_KeepTheirOwnFinalValuesOnBothKindsOfCannon()
        {
            CoreVoyageSession s = Battle();
            int load = Card(s, "card_load_shot").StrategyAmount;
            CoreBattleCard schoolCannon = Card(s, "card_school_fire_b03"), legacyCannon = Card(s, "card_chain_shot");
            CoreSchoolCardEffect overflow = Card(s, Art("card_school_fire_l05")).SchoolEffect;
            int gain = Math.Min(overflow.Power, Math.Min(overflow.Limit, Math.Max(0, overflow.Threshold - load)));
            Play(s, "card_load_shot");
            Assert.That(s.GetBattleCardDamage(schoolCannon, NavalBattleStage.Naval), Is.EqualTo(schoolCannon.Damage + load));
            Assert.That(s.GetBattleCardDamage(legacyCannon, NavalBattleStage.Naval), Is.EqualTo(legacyCannon.Damage + load));
            Play(s, Art("card_school_fire_l05"));
            s.Battle.EnemyBurnDamage = 6; s.Battle.EnemyBurnTurns = 3;
            Play(s, Art("card_school_fire_e02"));
            Assert.That(s.GetBattleCardDamage(schoolCannon, NavalBattleStage.Naval), Is.EqualTo(schoolCannon.Damage + load + gain));
            Assert.That(s.GetBattleCardDamage(legacyCannon, NavalBattleStage.Naval), Is.EqualTo(legacyCannon.Damage + load + gain));
        }

        [Test]
        public void OverflowTotalCap_ComparesFinalDamageBonusRatherThanMixedRawStorage()
        {
            CoreVoyageSession s = Battle();
            CoreSchoolCardEffect overflow = Card(s, Art("card_school_fire_l05")).SchoolEffect;
            int loaded = overflow.Threshold - 2;
            Assert.That(loaded, Is.GreaterThanOrEqualTo(0));
            s.Battle.PlayerLoadedShot = loaded; // Leave only two points of the authored total cap.
            Play(s, Art("card_school_fire_l05"));
            s.Battle.EnemyBurnDamage = 6; s.Battle.EnemyBurnTurns = 3;
            Play(s, Art("card_school_fire_e02"));
            int gain = Math.Min(2, Math.Min(overflow.Power, overflow.Limit));
            Assert.That(s.Battle.Schools.OverflowContribution, Is.EqualTo(gain));
            Assert.That(s.GetBattleCardDamage(Card(s, "card_school_fire_b03"), NavalBattleStage.Naval), Is.EqualTo(Card(s, "card_school_fire_b03").Damage + loaded + gain));
            Assert.That(s.Battle.PlayerLoadedShot, Is.EqualTo(loaded), "Overflow never changes legacy storage.");
        }

        [Test]
        public void LegacyLoadAfterSchoolOverflow_UsesFinalRemainingRoomAndNeverLowersEitherContribution()
        {
            CoreVoyageSession s = Battle();
            CoreSchoolCardEffect overflow = Card(s, Art("card_school_fire_l05")).SchoolEffect;
            int gain = Math.Min(overflow.Power, Math.Min(overflow.Limit, overflow.Threshold));
            Play(s, Art("card_school_fire_l05"));
            s.Battle.EnemyBurnDamage = 6; s.Battle.EnemyBurnTurns = 3;
            Play(s, Art("card_school_fire_e02"));
            s.Battle.DistanceMeters = 300;
            Assert.That(s.EndBattleTurn(), Is.True);
            Play(s, Art("card_school_fire_e02"), false); // two remaining ticks -> three
            Assert.That(s.PlayerLoadedShotDamageBonus, Is.EqualTo(gain));
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            CoreBattleCard load = s.GetBattleCard(Index("card_load_shot"));
            Assert.That(s.GetBattleLoadedShotLimit(load), Is.EqualTo(12));
            int increase = Math.Min(load.StrategyAmount, Math.Max(0, load.StrategyLimit - gain));
            Assert.That(s.GetBattleLoadedShotIncrease(load), Is.EqualTo(increase));
            Play(s, load.Id);
            Assert.That(s.PlayerLoadedShotDamageBonus, Is.EqualTo(gain + increase));
            int remaining = Math.Min(load.StrategyAmount, Math.Max(0, load.StrategyLimit - gain - increase));
            Assert.That(s.GetBattleLoadedShotIncrease(load), Is.EqualTo(remaining));
            Assert.That(s.BattleCardDescription(load), Does.Contain("本次实际增加 +" + remaining));
            Play(s, load.Id);
            Assert.That(s.PlayerLoadedShotDamageBonus, Is.EqualTo(Math.Max(load.StrategyLimit, gain)));
            Assert.That(s.Battle.PlayerLoadedShot, Is.EqualTo(increase + remaining));
            Assert.That(s.Battle.Schools.OverflowContribution, Is.EqualTo(gain));
            s.Battle.Energy = 3; Put(s, load.Id);
            Assert.That(load.Draw, Is.EqualTo(1));
            Assert.That(s.GetBattleLoadedShotIncrease(load), Is.Zero);
            Assert.That(s.PlayCard(0), Is.True, "Capped loading remains useful for its explicitly authored draw.");
            Assert.That(s.Battle.PlayerLoadedShot, Is.EqualTo(increase + remaining));
            Assert.That(s.Battle.Schools.OverflowContribution, Is.EqualTo(gain));
        }

        [Test]
        public void BoardingSeedAndCover_AreAppliedAfterTargetCleanupAndNavalCarry()
        {
            CoreVoyageSession s = Battle(false, 28);
            s.Battle.DistanceMeters = 100;
            Play(s, Art("card_school_fire_l01"));
            Play(s, Art("card_school_fire_l02"));
            s.Battle.DistanceMeters = 60;
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(Card(s, Art("card_school_fire_l01")).SchoolEffect.Secondary));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(Card(s, Art("card_school_fire_l02")).SchoolEffect.Power));
            Assert.That(s.Battle.Schools.BoardingCover, Is.Zero);
            Assert.That(s.Battle.Schools.BoardingBurn, Is.Zero);
        }

        [Test]
        public void Scorch_UsesChargesWithoutRecursiveHitEvents()
        {
            CoreVoyageSession s = Battle(true, 28);
            Play(s, "card_school_fire_b07");
            Play(s, Art("card_school_fire_l04"));
            int before = s.Battle.EnemyBoardingHealth;
            CoreBattleCard attack = Card(s, "card_school_fire_b10"), scorch = Card(s, Art("card_school_fire_l04"));
            Play(s, "card_school_fire_b10");
            int lost = before - s.Battle.EnemyBoardingHealth;
            Assert.That(lost == attack.Damage + scorch.SchoolEffect.Power || lost == 2 * attack.Damage + scorch.SchoolEffect.Power, Is.True);
            Assert.That(s.Battle.Schools.ScorchCharges, Is.EqualTo(1));
        }

        [Test]
        public void Arcane_QuotesBeforePaying_AndRejectedReleaseDoesNotSpendAnything()
        {
            CoreVoyageSession s = Battle();
            Put(s, "card_school_arcane_b04");
            int energy = s.Battle.Energy;
            Assert.That(s.CardUnavailableReason(0), Does.StartWith("法力不足"));
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Play(s, "card_school_arcane_b06");
            int mana = Math.Min(s.Battle.Schools.ManaLimit, Card(s, "card_school_arcane_b06").SchoolEffect.Power);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(mana));
            CoreBattleCard release = s.GetBattleCard(Index("card_school_arcane_b04"));
            Assert.That(mana, Is.GreaterThanOrEqualTo(release.SchoolEffect.Threshold));
            int damage = release.Damage + release.SchoolEffect.Power;
            Assert.That(s.GetBattleCardDamage(release, NavalBattleStage.Naval), Is.EqualTo(damage));
            int before = s.Battle.EnemyHull;
            Play(s, release.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(mana - release.SchoolEffect.Threshold));
        }

        [Test]
        public void Boarding_ClearsTemporaryManaAndTargetEffects()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_school_arcane_b06");
            typeof(CoreSchoolBattleState).GetProperty("TemporaryMana").SetValue(s.Battle.Schools, 3);
            typeof(CoreSchoolBattleState).GetProperty("CurseMarks").SetValue(s.Battle.Schools, 4);
            s.Battle.DistanceMeters = 60; s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void Curse_DamagesOnlyAtEnemyTurnEnd_AndDetonationEnhancesOneTick()
        {
            CoreVoyageSession s = Battle(true); s.Battle.Profile = Profile(20);
            CoreSchoolCardEffect mark = Card(s, "card_school_curse_b07").SchoolEffect;
            int initial = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_curse_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(initial));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(mark.Power));
            Assert.That(s.GetEnemyIntent(), Does.Contain("船员伤害 20"));
            for (int i = 1; i <= mark.Duration; i++)
            {
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(initial - i * mark.Power * mark.Secondary));
            }
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            s = Battle(); Play(s, "card_school_curse_b03");
            int before = s.Battle.EnemyHull, marks = s.Battle.Schools.CurseMarks;
            CoreBattleCard detonate = Card(s, "card_school_curse_b05");
            int damage = marks * (s.Battle.Schools.CurseDamagePerMark + detonate.SchoolEffect.Power);
            Play(s, detonate.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(marks));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
        }

        [Test]
        public void Tide_RequiresPaidGuardAbsorption_AndExpiresAfterTheResponseWindow()
        {
            CoreVoyageSession s = Battle(true); s.Battle.Profile = Profile(20);
            CoreBattleCard ward = Card(s, "card_school_tide_b08"), riposte = Card(s, "card_school_tide_b07");
            int wood = s.Cargo.Wood; Play(s, ward.Id);
            Assert.That(s.Cargo.Wood, Is.EqualTo(wood - 1));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.EndBattleTurn(), Is.True);
            int absorbed = Math.Min(20, ward.Block);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(absorbed));
            Assert.That(s.GetBattleCardDamage(riposte, NavalBattleStage.Boarding), Is.EqualTo(riposte.Damage + absorbed * riposte.SchoolEffect.Power / 100));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Put(s, riposte.Id); Assert.That(s.CanPlayCard(0), Is.False);
        }

        [Test]
        public void TideCleanse_ClearsTheRealEnemyCannonLoad()
        {
            CoreVoyageSession s = Battle();
            s.Battle.EnemyLoadedShot = 10;
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(s.Battle.Schools, 8);
            Play(s, Art("card_school_tide_e05"));
            Assert.That(s.Battle.EnemyLoadedShot, Is.Zero);
            CoreBattleCard card = Card(s, Art("card_school_tide_e05"));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(card.Block + card.SchoolEffect.Power));
        }

        [TestCase(false, 28)]
        [TestCase(true, 28)]
        [TestCase(true, 7)]
        public void TideReserve_DoesNotBuffOrdinaryOrOtherSchoolAttacks(bool boarding, int ordinary)
        {
            CoreVoyageSession s = Battle(boarding, ordinary);
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(s.Battle.Schools, 10);
            NavalBattleStage stage = boarding ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
            CoreBattleCard legacy = Card(s, boarding ? "card_crew_thrust" : "card_chain_shot");
            CoreBattleCard school = Card(s, boarding ? "card_school_fire_b07" : "card_school_fire_b03");
            Assert.That(s.GetBattleCardEffects(legacy, stage).Damage, Is.EqualTo(legacy.Damage));
            Assert.That(s.GetBattleCardEffects(school, stage).Damage, Is.EqualTo(school.Damage));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(10));
        }

        [Test]
        public void Thunder_CountsOnlyThunderAttacks_AndFinisherStartsANewChain()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_evasive_maneuver"); Play(s, "card_school_arcane_b06");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Play(s, "card_school_thunder_b03"); Play(s, "card_school_thunder_b04");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
            int before = s.Battle.EnemyHull;
            CoreBattleCard finisher = Card(s, "card_school_thunder_b06");
            int damage = finisher.Damage + finisher.SchoolEffect.Power * 2;
            Assert.That(s.GetBattleCardDamage(finisher, NavalBattleStage.Naval), Is.EqualTo(damage));
            Play(s, finisher.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Play(s, "card_evasive_maneuver");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void BloodCost_RejectsLethalPaymentBeforeAnyMutation()
        {
            CoreVoyageSession s = Battle(true);
            int cost = Card(s, "card_school_blood_b07").SchoolEffect.Threshold;
            s.Battle.PlayerBoardingHealth = cost;
            Put(s, "card_school_blood_b07");
            Assert.That(s.CardUnavailableReason(0), Is.EqualTo("支付生命后须至少剩余 1 点"));
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(s.GetBattleCardEnergyCost(Index("card_school_blood_b07")), Is.EqualTo(1));
            s.Battle.PlayerBoardingHealth = cost + 1;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1));
        }

        [Test]
        public void BloodDrain_UsesActualHealthLostIncludingOverkill_AndNeverSpendsMedicine()
        {
            CoreVoyageSession s = Battle(true);
            s.Battle.PlayerBoardingHealth = 500;
            s.Battle.EnemyBoardingHealth = 5;
            s.Cargo.Medicine = 3;
            CoreBattleState completed = s.Battle;
            CoreSchoolCardEffect effect = Card(s, "card_school_blood_b08").SchoolEffect;
            int healing = Math.Min(effect.Limit, 5 * Math.Min(100, effect.Power) / 100);
            Play(s, "card_school_blood_b08");
            Assert.That(completed.PlayerBoardingHealth, Is.EqualTo(500 + healing), "The authored percentage of five actual HP, irrespective of the critical roll.");
            Assert.That(s.Cargo.Medicine, Is.EqualTo(3));
        }

        [Test]
        public void BloodMend_QuotesAndHealsTheSameMissingHealthPercent_WithoutDoubleCrewScaling()
        {
            CoreVoyageSession s = Battle(true, 7);
            s.Battle.PlayerBoardingHealth = 980;
            s.Cargo.Medicine = 3;
            CoreBattleCard card = s.GetBattleCard(Index("card_school_blood_b09"));
            int healing = Math.Min(card.SchoolEffect.Limit, 20 * Math.Min(100, card.SchoolEffect.Power) / 100);
            Assert.That(s.GetBattleCardEffects(card, NavalBattleStage.Boarding).ActualRepair, Is.EqualTo(healing));
            Assert.That(s.GetBattleCardEffects(card, NavalBattleStage.Boarding).MedicineCost, Is.Zero);
            Play(s, card.Id);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(980 + healing));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(3));
            Assert.That(s.GetBattleCardAtLevel(Index(card.Id), 1).SchoolEffect.Power, Is.EqualTo(card.SchoolEffect.Power));
            Assert.That(s.GetBattleCardAtLevel(Index(card.Id), 1).SchoolEffect.Limit, Is.EqualTo(card.SchoolEffect.Limit));
        }

        [TestCase(7)] [TestCase(28)] [TestCase(56)]
        public void EnemyRam_EntryCoverAndSeedFireAreIndependentOfCrew(int ordinary)
        {
            CoreVoyageSession s = Battle(ordinary: ordinary);
            s.Battle.DistanceMeters = 100;
            Play(s, Art("card_school_fire_l01"));
            Play(s, Art("card_school_fire_l02"));
            s.Battle.DistanceMeters = 60;
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Raider,
                1000, 0, 20, 0, 0, 28, 1, 1, ramChance: 1, healthMultiplier: 1);
            Assert.That(s.EnemyWillRam, Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(s.Battle.BoardingInitiatedByEnemy, Is.True);
            int carried = s.Battle.PlayerMaxBoardingHealth - (int)Math.Floor(s.Battle.PlayerMaxBoardingHealth * 13m / (2m * s.MaxHull));
            int cover = Card(s, Art("card_school_fire_l02")).SchoolEffect.Power;
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(carried - Math.Max(0, 20 - cover)));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(Card(s, Art("card_school_fire_l01")).SchoolEffect.Secondary));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2), "The newly seeded crew fire must not tick immediately.");
            Assert.That(s.Battle.Schools.BoardingCover, Is.Zero);
        }

        [TestCase(7)] [TestCase(28)] [TestCase(56)]
        public void BoardingBurnAndCurseTickKeepAuthoredValuesWithoutCrewScaling(int ordinary)
        {
            CoreVoyageSession s = Battle(true, ordinary);
            s.Battle.Profile = Profile(0);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(2));
            Play(s, "card_school_curse_b07");
            Play(s, "card_school_curse_b10");
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.EqualTo(5));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8), "Neither curse card causes immediate damage.");
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8 - 2 - 9), "Stored burn and curse resolve once at their authored strength.");
        }

        [TestCase(7)] [TestCase(28)] [TestCase(56)]
        public void BoardingPaidBonusesAndTideConversionKeepValuesAndCostsAcrossCrewCounts(int ordinary)
        {
            CoreVoyageSession s = Battle(true, ordinary);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            Assert.That(s.BoardingCriticalChancePercent, Is.Zero, "This fixture isolates authored damage from the separate critical mechanic.");
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, 1);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_arcane_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 13));
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            typeof(CoreSchoolBattleState).GetProperty("ThunderChain").SetValue(s.Battle.Schools, 3);
            Play(s, "card_school_thunder_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 13 - 11));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(4));
            int own = s.Battle.PlayerBoardingHealth;
            Play(s, "card_school_blood_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 13 - 11 - 14));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(own - 3), "Removing effect scaling never discounts blood payment.");
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(s.Battle.Schools, 10);
            Play(s, "card_school_tide_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 13 - 11 - 14 - 15));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [Test]
        public void AllSchoolRanks_PreservePayloadAndRenderNoUnresolvedTokens()
        {
            var s = new CoreVoyageSession();
            foreach (CoreBattleCard card in CoreSchoolCardCatalog.All)
                for (int rank = 0; rank <= 3; rank++)
                {
                    CoreBattleCard upgraded = s.GetBattleCardAtLevel(Index(card.Id), rank);
                    Assert.That(upgraded.SchoolId, Is.EqualTo(card.SchoolId));
                    Assert.That(upgraded.SchoolEffect, Is.Not.Null);
                    Assert.That(upgraded.Description, Does.Not.Contain("{"));
                    foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                        if (card.IsAllowedInStage(stage)) Assert.That(s.BattleCardEffectDescription(upgraded, stage), Does.Not.Contain("{"));
                }
        }

        private static CoreVoyageSession Battle(bool boarding = false, int ordinary = 28, string upgradedCard = null, int rank = 2)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.ConfigureSpecialCardSources(CoreSchoolCardCatalog.All.Select(c => c.RequiredCrew).Where(x => x.Length > 0).ToArray(),
                CoreSchoolCardCatalog.All.Select(c => c.RequiredComponent).Where(x => x.Length > 0).ToArray());
            if (upgradedCard != null) s.ConfigureCardUpgrades(new Dictionary<string, int> { { upgradedCard, rank } });
            s.SetSeed("SCHOOL-MECHANICS-TEST");
            s.StartVoyage();
            Assert.That(s.Phase, Is.Not.EqualTo(CoreDemoPhase.Port));
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s,
                new object[] { new CoreEncounter("school_test", "机制测试", "战斗", "测试", true, false, 1, 70), false });
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100;
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            typeof(CoreBattleState).GetProperty("PlayerOrdinaryCrew").SetValue(s.Battle, ordinary);
            if (boarding)
            {
                s.Battle.DistanceMeters = 60;
                Assert.That(s.TryBoard(), Is.True);
                s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
                s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            }
            s.Battle.EnemyBlock = 0;
            s.Cargo.Powder = 100; s.Cargo.Cannonballs = 100; s.Cargo.Medicine = 0; s.Cargo.Wood = 100;
            return s;
        }
        private static CoreBattleProfile Profile(int melee) => new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
            1000, 0, melee, 0, 0, 28, 1, 1, healthMultiplier: 1);
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static CoreBattleCard Card(CoreVoyageSession session, string id) => session.GetBattleCard(Index(id));
        private static int Scale(int value, decimal scale) => (int)decimal.Round(value * scale, 0, MidpointRounding.AwayFromZero);
        private static string Art(string id) => CoreSchoolCardCatalog.All.Single(c => c.ArtId == id).Id;
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id, bool refill = true)
        {
            if (refill) s.Battle.Energy = 3;
            Put(s, id);
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
        }
    }
}
