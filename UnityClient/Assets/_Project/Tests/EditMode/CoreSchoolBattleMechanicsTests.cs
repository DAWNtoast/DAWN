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
            int card = Index("card_school_fire_b03");
            CoreBattleCard zero = session.GetBattleCardAtLevel(card, 0);
            CoreBattleCard one = session.GetBattleCardAtLevel(card, 1);
            CoreBattleCard two = session.GetBattleCardAtLevel(card, 2);
            Assert.That(zero.Damage, Is.GreaterThan(0));
            Assert.That(one.Damage, Is.EqualTo(CoreBattleUpgradeBalance.ScaleNumerical(zero.Damage, 1)));
            Assert.That(one.SchoolEffect.Power, Is.EqualTo(CoreCardUpgradeDesign.ScaleEffect(zero, zero.SchoolEffect.Power, 1)));
            Assert.That(one.SchoolEffect.Duration, Is.EqualTo(2));
            Assert.That(two.Damage, Is.EqualTo(CoreBattleUpgradeBalance.ScaleNumerical(zero.Damage, 2)));
            Assert.That(two.Energy, Is.EqualTo(zero.Energy));
            Assert.That(session.GetBattleCardEffects(zero, NavalBattleStage.Naval).Damage, Is.EqualTo(zero.Damage));
            Assert.That(session.GetBattleCardEffects(session.GetBattleCardAtLevel(Index("card_chain_shot"), 0), NavalBattleStage.Naval).Damage, Is.EqualTo(10));
            Assert.That(session.GetBattleCardAtLevel(Index(Art("card_school_fire_e02")), 1).SchoolEffect.Power, Is.EqualTo(2));
        }

        [TestCase(0, 5, 5)]
        [TestCase(1, 7, 7)]
        [TestCase(2, 9, 9)]
        [TestCase(3, 9, 9)]
        public void ArcaneBaseGuardKeepsItsNextGainReserveAndManaCost(int rank, int baseGuard, int paidBonus)
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
            Assert.That(s.GetBattleCardEffects(ward, NavalBattleStage.Naval).Block, Is.EqualTo(baseGuard),
                "Funding the card prepares a later bonus instead of changing this card's own guard.");
            Play(s, ward.Id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(baseGuard));
            Assert.That(s.Battle.Schools.ArcaneNextBlockBonus, Is.EqualTo(paidBonus));
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
        }

        [Test]
        public void NegativeMovement_SurvivesUpgrade_AndGrantsPlannedMovementPoints()
        {
            CoreVoyageSession s = Battle();
            CoreBattleCard original = Card(s, "card_school_fire_b06");
            CoreBattleCard one = s.GetBattleCardAtLevel(Index("card_school_fire_b06"), 1);
            Assert.That(original.SchoolEffect.Movement, Is.LessThan(0));
            Assert.That(one.SchoolEffect.Movement, Is.EqualTo(original.SchoolEffect.Movement));
            Assert.That(one.MovementMeters, Is.EqualTo(-original.SchoolEffect.Movement));
            s.Battle.DistanceMeters = 100;
            s.Battle.PlayerMovingTowardEnemy = true;
            int points = s.Battle.NavalActionPoints;
            Play(s, "card_school_fire_b06");
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(100f));
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(points + 1));
            Assert.That(s.Battle.PlayerMovingTowardEnemy, Is.True, "A movement card cannot silently change the current pose.");
        }

        [Test]
        public void FireAlias_UsesFourBurnAtLowCrew_AndNewLayersJoinTheRemainingTotal()
        {
            CoreVoyageSession s = Battle(true, 7); s.Battle.Profile = Profile(0);
            int powder = s.Cargo.Powder;
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(4));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(4));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(3));
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(7));
            int hp = s.Battle.EnemyBoardingHealth;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 7));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(6));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(6), "All additions share the same decaying total.");
            Assert.That(s.Cargo.Powder, Is.EqualTo(powder - 2));
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
        public void BoardingFireGuard_ReactsOnceToActualGuardAbsorption_AndSelfBurnExpires()
        {
            CoreVoyageSession s = Battle(true); s.Battle.Profile = Profile(10);
            Play(s, "card_boarding_fire_13");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(8));
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(8));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before - 2));
            Assert.That(s.Battle.Schools.NaturalFireGuardTurns, Is.Zero, "The legacy natural-tick ward must not also run.");
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before - 2), "The player-end tick reduces self-burn before the enemy attacks.");
            Assert.That(s.Battle.Boarding.SelfBurn, Is.Empty);
        }

        [Test]
        public void FieldCannonWithoutAMatchingFieldDoesNotSettleOrExtendExistingBurn()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_school_fire_b03");
            int before = s.Battle.EnemyHull;
            CoreBattleCard card = Card(s, Art("card_school_fire_l03"));
            int burn = s.Battle.EnemyBurnTurns;
            Play(s, Art("card_school_fire_l03"));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - card.Damage));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(burn));
            Assert.That(s.Battle.Schools.ExtraFireTickUsed, Is.False);
            s.Battle.Energy = 20;
            before = s.Battle.EnemyHull;
            Play(s, Art("card_school_fire_l03"), false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - card.Damage));
        }

        [Test]
        public void FireFieldMasteryAndExtensionDoNotInstallRetiredPassiveBurnOrLoading()
        {
            CoreVoyageSession s = Battle();
            s.Battle.EnemyBurnDamage = s.Battle.EnemyBurnTurns = 0;
            Play(s, Art("card_school_fire_l05"));
            Assert.That(s.Battle.SpecialCards.IsActive("fire"), Is.False);
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
            Play(s, Art("card_school_fire_e02"));
            Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            Assert.That(s.Battle.Schools.OverflowContribution, Is.Zero);
            s.Battle.Energy = 20;
            Play(s, Art("card_school_fire_e02"), false);
            Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero, "The sail extends matching naval fields only.");
            Assert.That(s.Battle.Schools.OverflowContribution, Is.Zero);
        }

        [Test]
        public void FireFieldMasteryDoesNotChangeLegacyLoadingOnEitherKindOfCannon()
        {
            CoreVoyageSession s = Battle();
            int load = Card(s, "card_load_shot").StrategyAmount;
            CoreBattleCard schoolCannon = Card(s, "card_school_fire_b03"), legacyCannon = Card(s, "card_chain_shot");
            Play(s, "card_load_shot");
            Assert.That(s.GetBattleCardDamage(schoolCannon, NavalBattleStage.Naval), Is.EqualTo(schoolCannon.Damage + load));
            Assert.That(s.GetBattleCardDamage(legacyCannon, NavalBattleStage.Naval), Is.EqualTo(legacyCannon.Damage + load));
            Play(s, Art("card_school_fire_l05"));
            Play(s, Art("card_school_fire_e02"));
            Assert.That(s.GetBattleCardDamage(schoolCannon, NavalBattleStage.Naval), Is.EqualTo(schoolCannon.Damage + load));
            Assert.That(s.GetBattleCardDamage(legacyCannon, NavalBattleStage.Naval), Is.EqualTo(legacyCannon.Damage + load));
            Assert.That(s.Battle.Schools.OverflowContribution, Is.Zero);
        }

        [Test]
        public void FireFieldMasteryDoesNotLeaveAPassiveTriggerOnTheNextNavalTurn()
        {
            CoreVoyageSession s = Battle();
            Play(s, Art("card_school_fire_l05"));
            Play(s, Art("card_school_fire_e02"));
            Assert.That(s.Battle.SpecialCards.HasPlayed("fire"), Is.True);
            s.Battle.DistanceMeters = 300;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.SpecialCards.IsActive("fire"), Is.False);
            Assert.That(s.Battle.SpecialCards.HasPlayed("fire"), Is.False);
            s.Battle.EnemyBurnDamage = 0; s.Battle.EnemyBurnTurns = 0;
            Play(s, Art("card_school_fire_e02"));
            Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
        }

        [Test]
        public void LegacyLoadAfterFireFieldMasteryUsesRemainingRoomAndKeepsAuthoredDrawAtCap()
        {
            CoreVoyageSession s = Battle();
            Play(s, Art("card_school_fire_l05"));
            Play(s, Art("card_school_fire_e02"));
            CoreBattleCard load = s.GetBattleCard(Index("card_load_shot"));
            Assert.That(s.GetBattleLoadedShotLimit(load), Is.EqualTo(12));
            foreach (int increase in new[] { 5, 5, 2 })
            {
                Assert.That(s.GetBattleLoadedShotIncrease(load), Is.EqualTo(increase));
                Assert.That(s.BattleCardDescription(load), Does.Contain("本次实际增加 +" + increase));
                Play(s, load.Id);
            }
            Assert.That(s.PlayerLoadedShotDamageBonus, Is.EqualTo(12));
            s.Battle.Energy = 20; Put(s, load.Id);
            Assert.That(load.Draw, Is.EqualTo(1));
            Assert.That(s.GetBattleLoadedShotIncrease(load), Is.Zero);
            Assert.That(s.PlayCard(0), Is.True, "Capped loading remains useful for its explicitly authored draw.");
            Assert.That(s.Battle.PlayerLoadedShot, Is.EqualTo(12));
            Assert.That(s.Battle.Schools.OverflowContribution, Is.Zero);
        }

        [Test]
        public void NavalFireParts_DoNotSeedBurnOrCoverWhenThePlayerBoards()
        {
            CoreVoyageSession s = Battle(false, 28);
            s.Battle.DistanceMeters = 100;
            Play(s, Art("card_school_fire_l01"));
            Play(s, Art("card_school_fire_l02"));
            s.Battle.DistanceMeters = 100;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.BoardingCover, Is.Zero);
            Assert.That(s.Battle.Schools.BoardingBurn, Is.Zero);
        }

        [Test]
        public void BoardingFireExhaust_BurnsAnotherAttackForFiveLayers_WithoutLegacyCharges()
        {
            CoreVoyageSession s = Battle(true, 28);
            int burned = Index("card_crew_thrust"), before = s.Battle.EnemyBoardingHealth;
            s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(new[] { Index("card_boarding_fire_06"), burned });
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { SelectionIndexes = new List<int> { 1 } }), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before - 8));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(5));
            Assert.That(s.Battle.Boarding.ExhaustedCards, Does.Contain(burned));
            Assert.That(s.Battle.Schools.ScorchCharges, Is.Zero);
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
            int damage = release.Damage;
            Assert.That(s.GetBattleCardDamage(release, NavalBattleStage.Naval), Is.EqualTo(damage));
            int before = s.Battle.EnemyHull;
            Play(s, release.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(mana - release.SchoolEffect.Threshold));
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.EqualTo(release.SchoolEffect.Power));
        }

        [Test]
        public void Boarding_ClearsTemporaryManaAndTargetEffects()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_school_arcane_b06");
            typeof(CoreSchoolBattleState).GetProperty("TemporaryMana").SetValue(s.Battle.Schools, 3);
            typeof(CoreSchoolBattleState).GetProperty("CurseMarks").SetValue(s.Battle.Schools, 4);
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void BoardingAndNavalCurseBothTickTheirLayersAndDecayAtEnemyEnd()
        {
            CoreVoyageSession s = Battle(true); s.Battle.Profile = Profile(20);
            int initial = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_curse_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(initial - 8));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(initial - 9));
            }
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            s = Battle(); Play(s, "card_school_curse_b03");
            int before = s.Battle.EnemyHull, marks = s.Battle.Schools.CurseMarks;
            CoreBattleCard detonate = Card(s, "card_school_curse_b05");
            int damage = marks + detonate.SchoolEffect.Power;
            Play(s, detonate.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(marks));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
        }

        [Test]
        public void TideAliases_RequireActualAbsorptionButNoWood_AndSpendAllMomentum()
        {
            CoreVoyageSession s = Battle(true); s.Battle.Profile = Profile(20); s.Cargo.Wood = 0;
            Play(s, "card_school_tide_b08");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(4));
            Assert.That(s.Cargo.Wood, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(4));
            int before = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_tide_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before - 10));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Play(s, "card_school_tide_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before - 16), "Zero momentum still permits the base six damage.");
        }

        [Test]
        public void TideFieldPulsePreservesEnemyCannonLoadAndGrantsOnlyItsConditionalMomentum()
        {
            CoreVoyageSession s = Battle();
            s.Battle.EnemyLoadedShot = 10;
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(s.Battle.Schools, 8);
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { true, new VoyagePoint(0, 0), 0f });
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Tide, true, 0, 0, false, 1, 0), Is.True);
            Play(s, Art("card_school_tide_e05"));
            Assert.That(s.Battle.EnemyLoadedShot, Is.EqualTo(10));
            CoreBattleCard card = Card(s, Art("card_school_tide_e05"));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(8 + card.SchoolEffect.Power));
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
            int cost = 3;
            s.Battle.PlayerBoardingHealth = cost;
            Put(s, "card_school_blood_b07");
            Assert.That(s.CardUnavailableReason(0), Does.Contain("至少剩1生命"));
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
            int healing = Math.Min(2, 5);
            Play(s, "card_school_blood_b08");
            Assert.That(completed.PlayerBoardingHealth, Is.EqualTo(500 + healing), "Drain is capped at two and never exceeds actual enemy health lost.");
            Assert.That(s.Cargo.Medicine, Is.EqualTo(3));
        }

        [Test]
        public void BloodMendAlias_QuotesFixedMedicineHealing_WithoutDoubleCrewScaling()
        {
            CoreVoyageSession s = Battle(true, 7);
            s.Battle.PlayerBoardingHealth = 980; s.Cargo.Medicine = 3;
            CoreBattleCard card = s.GetBattleCard(Index("card_school_blood_b09"));
            Assert.That(card.BoardingSpec.Key, Is.EqualTo("blood_09"));
            Assert.That(s.GetBattleCardEffects(card, NavalBattleStage.Boarding).ActualRepair, Is.EqualTo(7));
            Assert.That(s.GetBattleCardEffects(card, NavalBattleStage.Boarding).MedicineCost, Is.EqualTo(1));
            Play(s, card.Id);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(987));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(2));
            Assert.That(s.GetBattleCardAtLevel(Index(card.Id), 1).Repair, Is.EqualTo(9));
        }

        [TestCase(7)] [TestCase(28)] [TestCase(56)]
        public void EnemyRam_NavalFirePartsDoNotGrantRetiredBoardingCoverOrSeed(int ordinary)
        {
            CoreVoyageSession s = Battle(ordinary: ordinary);
            s.Battle.DistanceMeters = 100;
            Play(s, Art("card_school_fire_l01"));
            Play(s, Art("card_school_fire_l02"));
            s.Battle.DistanceMeters = 100;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, 90f);
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Raider,
                1000, 0, 20, 0, 0, 28, 1, 1, ramChance: 1, healthMultiplier: 1);
            Assert.That(s.EnemyWillRam, Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(s.Battle.BoardingInitiatedByEnemy, Is.True);
            int carried = s.Battle.PlayerMaxBoardingHealth - (int)Math.Floor(s.Battle.PlayerMaxBoardingHealth * 13m / (2m * s.MaxHull));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(carried - 20));
            Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
            Assert.That(s.Battle.Schools.BoardingCover, Is.Zero);
        }

        [TestCase(7)] [TestCase(28)] [TestCase(56)]
        public void BoardingBurnTicksButCurseMustBeSpent_WithoutCrewScaling(int ordinary)
        {
            CoreVoyageSession s = Battle(true, ordinary); s.Battle.Profile = Profile(0);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(4));
            Play(s, "card_school_curse_b07");
            Play(s, "card_school_curse_b10");
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8 - 8 - 28));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8 - 8 - 28 - 4));
        }

        [TestCase(7)] [TestCase(28)] [TestCase(56)]
        public void BoardingAliasBonusesAndCostsStayFixedAcrossCrewCounts(int ordinary)
        {
            CoreVoyageSession s = Battle(true, ordinary); s.Battle.Profile = Profile(20);
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, 1);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "card_school_arcane_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 6));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(2));
            typeof(CoreSchoolBattleState).GetProperty("ThunderChain").SetValue(s.Battle.Schools, 3);
            Play(s, "card_school_thunder_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 6 - 11));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(4));
            int own = s.Battle.PlayerBoardingHealth;
            Play(s, "card_school_blood_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 6 - 11 - 16));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(own - 3));
            Play(s, "card_school_tide_b08");
            Assert.That(s.EndBattleTurn(), Is.True);
            Play(s, "card_school_tide_b07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 6 - 11 - 16 - 10));
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
                    Assert.That(upgraded.SchoolEffect != null || upgraded.BoardingSpec != null ||
                        CoreNavalSchoolExpansionCatalog.FindSpec(upgraded.Id) != null, Is.True, card.Id);
                    if (upgraded.BoardingSpec != null)
                        Assert.That(upgraded.BoardingSpec.Key, Is.EqualTo(CoreVoyageSession.CardLibrary[Index(card.Id)].BoardingSpec.Key));
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
                s.Battle.DistanceMeters = 100;
                typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
                typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
                Assert.That(s.TryBoard(), Is.True);
                s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
                s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            }
            s.Battle.EnemyBlock = 0;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
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
