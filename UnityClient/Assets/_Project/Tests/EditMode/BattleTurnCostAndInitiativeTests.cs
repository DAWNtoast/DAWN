using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleTurnCostAndInitiativeTests
    {
        [Test]
        public void DuplicateCardsShareCostButOtherIdsAndFailedAttemptsDoNot()
        {
            CoreVoyageSession session = Start(false, upgrades: new Dictionary<string, int> { { "card_full_sail_turn", 2 } });
            SetHand(session, 1, 1, 7);
            Assert.That(session.GetBattleCardEnergyCost(1), Is.Zero);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(1), Is.EqualTo(1));
            Assert.That(session.GetBattleCardEnergyCost(7), Is.Zero);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(2));
            Assert.That(session.GetBattleCardEnergyCost(1), Is.EqualTo(2));
            SetHand(session, 1);
            session.Battle.Energy = 1;
            int distance = (int)session.Battle.DistanceMeters;
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.GetBattleCardEnergyCost(1), Is.EqualTo(2));
            Assert.That(session.Battle.Hand, Is.EqualTo(new[] { 1 }));
            Assert.That(session.Battle.Energy, Is.EqualTo(1));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(distance));
        }

        [Test]
        public void UnaffordableAmmoDoesNotChargeEnergyOrIncreaseNextCopyCost()
        {
            CoreVoyageSession session = Start(true);
            session.Battle.DistanceMeters = 200;
            session.Cargo.Cannonballs = 0;
            SetHand(session, 0);
            int energy = session.Battle.Energy;
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.GetBattleCardEnergyCost(0), Is.EqualTo(session.GetBattleCard(0).Energy));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            session.Cargo.Cannonballs = 1;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Cargo.Cannonballs, Is.Zero);
            Assert.That(session.GetBattleCardEnergyCost(0), Is.EqualTo(session.GetBattleCard(0).Energy + 1));
        }

        [Test]
        public void PreparationToNavalAndTrueNextTurnRestoreBaseCosts()
        {
            CoreVoyageSession session = Start(false);
            SetHand(session, 7);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(7), Is.EqualTo(1));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.GetBattleCardEnergyCost(7), Is.Zero);
            SetHand(session, 7);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(7), Is.EqualTo(1));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(7), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlayerBoardingAndRamResetPhaseCostAndKeepPlayerInitiative(bool ram)
        {
            CoreVoyageSession session = Start(true, cook: true);
            SetHand(session, 23);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(23), Is.EqualTo(session.GetBattleCard(23).Energy + 1));
            session.Battle.DistanceMeters = 40;
            session.Battle.Alignment = ram ? NavalBattleAlignment.HeadOn : NavalBattleAlignment.Parallel;
            session.Battle.PlayerMovingTowardEnemy = ram;
            int health = session.Battle.PlayerBoardingHealth, turn = session.Battle.Turn;
            Assert.That(ram ? session.TryRam() : session.TryBoard(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            Assert.That(session.GetBattleCardEnergyCost(23), Is.EqualTo(session.GetBattleCard(23).Energy));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.False);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health), "Player connection grants the first card action, with no hidden enemy strike.");
        }

        [TestCase(NavalBattleAlignment.HeadOn)]
        [TestCase(NavalBattleAlignment.Parallel)]
        public void EnemyRamTelegraphsConnectionAndExecutesExactlyOneMeleeOpening(NavalBattleAlignment alignment)
        {
            CoreVoyageSession session = Start(true);
            session.Battle.Profile = RamProfile(1d);
            session.Battle.DistanceMeters = 90;
            session.Battle.Alignment = alignment;
            session.Battle.PlayerBlock = 999;
            int health = session.Battle.PlayerBoardingHealth, turn = session.Battle.Turn;
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(session.EnemyRamFirstStrikeDamage, Is.EqualTo(7));
            Assert.That(session.GetEnemyIntent(), Does.Contain("冲撞→接舷").And.Contain("敌方先手").And.Contain("7"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - 7), "Ship guard cannot cancel the announced crew opening.");
            Assert.That(session.Battle.Turn, Is.EqualTo(turn + 1));
            Assert.That(session.Battle.PhaseTurn, Is.EqualTo(1));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(5));
            Assert.That(session.Battle.Hand.All(i => session.GetBattleCard(i).IsAllowedInStage(NavalBattleStage.Boarding)), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(session.Battle.MaxEnergy));
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.EnemyWillRam, Is.False);
            int beforeNextTurn = session.Battle.PlayerBoardingHealth;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(beforeNextTurn - 7), "Only a further explicit end turn permits another enemy action.");
        }

        [Test]
        public void PullingAwayCancelsForecastRamWithoutConsumingItsDeterministicRoll()
        {
            CoreVoyageSession session = Start(true);
            session.Battle.Profile = RamProfile(1d);
            session.Battle.DistanceMeters = 90;
            Assert.That(session.EnemyWillRam, Is.True);
            SetHand(session, 11);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.GreaterThan(session.EnemyRamRangeMeters));
            Assert.That(session.EnemyWillRam, Is.False);
            Assert.That(session.GetEnemyIntent(), Does.Not.Contain("冲撞"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
        }

        [Test]
        public void RepeatedIntentReadsDoNotChangeRamDecisionOrDeckRandomness()
        {
            bool sawRam = false, sawNonRam = false;
            for (int seed = 0; seed < 24; seed++)
            {
                CoreVoyageSession observed = Start(true, seed: "RAM-DETERMINISM-" + seed);
                CoreVoyageSession control = Start(true, seed: "RAM-DETERMINISM-" + seed);
                foreach (CoreVoyageSession session in new[] { observed, control })
                { session.Battle.Profile = RamProfile(.5d); session.Battle.DistanceMeters = 90; }
                bool decision = observed.EnemyWillRam;
                sawRam |= decision; sawNonRam |= !decision;
                string intent = observed.GetEnemyIntent();
                for (int read = 0; read < 40; read++)
                {
                    Assert.That(observed.GetEnemyIntent(), Is.EqualTo(intent));
                    Assert.That(observed.EnemyWillRam, Is.EqualTo(decision));
                }
                Assert.That(control.EnemyWillRam, Is.EqualTo(decision));
                observed.EndBattleTurn(); control.EndBattleTurn();
                Assert.That(observed.Battle.Stage, Is.EqualTo(control.Battle.Stage));
                Assert.That(observed.Hull, Is.EqualTo(control.Hull));
                Assert.That(observed.Battle.PlayerBoardingHealth, Is.EqualTo(control.Battle.PlayerBoardingHealth));
                Assert.That(observed.Battle.Hand, Is.EqualTo(control.Battle.Hand));
                Assert.That(observed.Battle.DrawPile, Is.EqualTo(control.Battle.DrawPile));
            }
            Assert.That(sawRam && sawNonRam, Is.True, "Battle seeds produce different fixed decisions; intent reads never reroll them.");
        }

        [Test]
        public void FatalEnemyOpeningSettlesDefeatBeforePlayerCanAct()
        {
            CoreVoyageSession session = Start(true);
            session.Battle.Profile = RamProfile(1d, 500);
            session.Battle.DistanceMeters = 90;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.PlayCard(0), Is.False);
        }

        [Test]
        public void LethalBurnCancelsBothRamForecastAndItsOpeningAttack()
        {
            CoreVoyageSession session = Start(true);
            session.Battle.Profile = RamProfile(1d);
            session.Battle.DistanceMeters = 90;
            session.Battle.EnemyHull = 5;
            session.Battle.EnemyBurnDamage = 5;
            session.Battle.EnemyBurnTurns = 1;
            int hull = session.Hull;
            Assert.That(session.EnemyWillRam, Is.False);
            Assert.That(session.EnemyRamFirstStrikeDamage, Is.Zero);
            Assert.That(session.GetEnemyIntent(), Does.Contain("敌方无法行动"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Hull, Is.EqualTo(hull));
        }

        [Test]
        public void ShortStaffedShipUsesReducedEffectiveArmorForEnemyCannonDamage()
        {
            CoreVoyageSession staffed = Start(true, ordinary: 8, armorBonus: 10);
            CoreVoyageSession shortStaffed = Start(true, ordinary: 0, armorBonus: 10);
            Assert.That(shortStaffed.Armor, Is.EqualTo(staffed.Armor));
            Assert.That(shortStaffed.EffectiveArmor, Is.LessThan(staffed.EffectiveArmor));
            int[] damage = new int[2];
            CoreVoyageSession[] sessions = { staffed, shortStaffed };
            for (int i = 0; i < sessions.Length; i++)
            {
                CoreVoyageSession session = sessions[i];
                session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 500, 20, 7, 0, 0, 10, 1f, 1f);
                session.Battle.DistanceMeters = 200;
                session.Battle.EnemyLoadedShot = 0;
                int hull = session.Hull;
                Assert.That(session.EndBattleTurn(), Is.True);
                damage[i] = hull - session.Hull;
                Assert.That(damage[i], Is.EqualTo(System.Math.Max(1, 20 - System.Math.Max(0, (session.EffectiveArmor - 4) / 2))));
            }
            Assert.That(damage[1], Is.GreaterThan(damage[0]));
        }

        [Test]
        public void SpecialistsDoNotReplaceOrdinarySailorBoardingHealth()
        {
            CoreVoyageSession small = Start(false, ordinary: 4, specialists: 0);
            CoreVoyageSession experts = Start(false, ordinary: 4, specialists: 3);
            CoreVoyageSession sailors = Start(false, ordinary: 7, specialists: 0);
            Assert.That(experts.EffectiveCrewCount, Is.EqualTo(sailors.EffectiveCrewCount));
            Assert.That(small.Battle.PlayerMaxBoardingHealth, Is.EqualTo(experts.Battle.PlayerMaxBoardingHealth));
            Assert.That(sailors.Battle.PlayerMaxBoardingHealth - experts.Battle.PlayerMaxBoardingHealth,
                Is.EqualTo(3 * CoreBattleBalance.BoardingHealthPerSailor));
            Assert.That(small.Battle.PlayerMaxBoardingHealth, Is.EqualTo(CoreBattleBalance.BoardingHealthForOrdinaryCrew(4)));
            Assert.That(small.Battle.EnemyMaxBoardingHealth,
                Is.EqualTo(CoreBattleBalance.BoardingHealthForOrdinaryCrew(small.Battle.EnemyOrdinaryCrew)));
        }

        private static CoreBattleProfile RamProfile(double chance, int melee = 7) =>
            new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider, 500, 1, melee, 0, 0, 30, 1f, 1f, chance, 120f);

        private static CoreVoyageSession Start(bool naval, string seed = "TURN-COST-INITIATIVE", bool cook = false,
            int ordinary = 8, int specialists = 0, Dictionary<string, int> upgrades = null, int armorBonus = 0)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, cook, false);
            session.ConfigureDepartureCrew(ordinary, specialists);
            if (armorBonus != 0) session.ConfigureComponentBonuses(0, armorBonus, 0, 0, 0);
            if (upgrades != null) session.ConfigureCardUpgrades(upgrades);
            session.SetSeed(seed);
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { new CoreEncounter("turn_cost_fixture", "机制测试敌舰", "海战", "隔离核心夹具", true, false, 1, 70), false });
            if (naval) Assert.That(session.EndBattleTurn(), Is.True);
            return session;
        }

        private static void SetHand(CoreVoyageSession session, params int[] indexes)
        { session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(indexes); }
    }
}
