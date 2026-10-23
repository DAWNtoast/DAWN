using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingEnemyTests
    {
        private static readonly string[] Schools = { "fire", "arcane", "thunder", "blood", "tide", "curse" };

        private static IEnumerable<TestCaseData> Bosses()
        {
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All)
                yield return new TestCaseData(boss.Id, boss.SchoolId).SetName("BoardingDeck_Route_" + boss.SchoolId);
            foreach (CoreLegendaryWarship boss in CoreLegendaryWarships.All)
                yield return new TestCaseData(boss.Id, boss.SchoolId).SetName("BoardingDeck_Legendary_" + boss.SchoolId);
            yield return new TestCaseData(CoreBattleBalance.TreasureBossId, "curse").SetName("BoardingDeck_Treasure_Curse");
        }

        [TestCaseSource(nameof(Bosses))]
        public void ActualEncounterStart_SelectsItsShipsSchool(string encounterId, string school)
        {
            CoreEncounter encounter = CoreRouteBossCatalog.Find(encounterId)?.CreateEncounter() ??
                CoreLegendaryWarships.Find(encounterId)?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
            var session = new CoreVoyageSession();
            session.SetSeed("BOARDING-ENEMY-MAPPING");
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, true });
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle.BoardingEnemy.Deck.SchoolId, Is.EqualTo(school));
            Assert.That(session.Battle.BoardingEnemy.Deck, Is.SameAs(CoreBoardingEnemyCatalog.ForEncounter(encounterId)));
            Assert.That(session.Battle.EnemyMaxBoardingHealth, Is.EqualTo(session.Battle.Profile.BoardingHealth));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnTurns, Is.Zero);
        }

        [Test]
        public void BothBossRosters_CoverAllSixSchools()
        {
            Assert.That(CoreRouteBossCatalog.All.Select(boss => boss.SchoolId), Is.EquivalentTo(Schools));
            Assert.That(CoreLegendaryWarships.All.Select(boss => boss.SchoolId), Is.EquivalentTo(Schools));
        }

        [TestCaseSource(nameof(Schools))]
        public void SchoolTurns_RepeatActualFourTurnCardOrderWithoutUnboundedAttackOrGuardGrowth(string school)
        {
            CoreVoyageSession session = Battle(school);
            CoreBoardingEnemyDeck deck = session.Battle.BoardingEnemy.Deck;
            Assert.That(deck.Turns.Count, Is.EqualTo(4));
            Assert.That(deck.Cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(deck.Cards.Count));
            for (int turn = 1; turn <= 12; turn++)
            {
                Assert.That(session.Battle.PhaseTurn, Is.EqualTo(turn));
                Assert.That(session.EnemyBoardingAttackDamage, Is.InRange(0, 100), school + " turn " + turn);
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                CollectionAssert.AreEqual(deck.TurnAt(turn).Cards.Select(card => card.Id), preview.Actions.Select(action => action.CardId));
                string intent = session.GetEnemyIntent();
                Assert.That(intent, Does.Contain(deck.TurnAt(turn).Name));
                Assert.That(intent, Does.Contain("下轮：" + deck.TurnAt(turn + 1).Name));
                session.Battle.PlayerBlock = 1000;
                session.Battle.EnemyBlock = 999;
                End(session);
                Assert.That(session.Battle.EnemyBlock, Is.InRange(0, 22), "Only bounded retained tide guard can cross enemy turns.");
            }
            session.Battle.PhaseTurn = 4001;
            CollectionAssert.AreEqual(deck.TurnAt(1).Cards.Select(card => card.Id), session.GetBoardingEnemyPreview().Actions.Select(action => action.CardId));
            Assert.That(session.EnemyBoardingAttackDamage, Is.InRange(0, 100));
        }

        private static IEnumerable<TestCaseData> PreviewCases()
        {
            foreach (string school in Schools)
                for (int turn = 1; turn <= 4; turn++)
                    foreach (int guard in new[] { 0, 7, 100 })
                        yield return new TestCaseData(school, turn, guard);
        }

        [TestCaseSource(nameof(PreviewCases))]
        public void ActualHealthLoss_AgreesWithRepeatedStructuredPreview(string school, int turn, int guard)
        {
            CoreVoyageSession session = Battle(school, turn);
            session.Battle.PlayerBlock = guard;
            session.Cargo.Curse = 2;
            int attack = session.EnemyBoardingAttackDamage;
            int piercing = session.EnemyBoardingPiercingDamage;
            string intent = session.GetEnemyIntent();
            string before = Snapshot(session);
            for (int query = 0; query < 5; query++)
            {
                Assert.That(session.GetEnemyIntent(), Is.EqualTo(intent));
                Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(attack));
                Assert.That(session.EnemyBoardingPiercingDamage, Is.EqualTo(piercing));
                Assert.That(Snapshot(session), Is.EqualTo(before));
            }
            int absorbed = Math.Min(guard, attack - piercing);
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(absorbed));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - attack + absorbed));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Preview_DoesNotConsumeWeaknessArmorSealOrRandomness(int turn)
        {
            CoreVoyageSession session = Battle("curse", turn);
            SetDeck(session, LegacyCurseDeck());
            CoreBoardingBattleState state = session.Battle.Boarding;
            state.ThunderWeakness = 5;
            state.ThunderWeaknessTurns = 2;
            SetField(state, "CurseWeakTurns", 2);
            SetField(state, "CurseWeakCard", Card("curse_07"));
            SetField(state, "CurseArmorTurns", 2);
            SetField(state, "CurseArmorCard", Card("curse_08"));
            AddMarks(session, 1);
            string before = Snapshot(session);
            int attack = session.EnemyBoardingAttackDamage;
            for (int query = 0; query < 10; query++)
            {
                session.GetEnemyIntent();
                Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(attack));
                Assert.That(session.EnemyBoardingPiercingDamage, Is.InRange(0, attack));
                Assert.That(Snapshot(session), Is.EqualTo(before));
            }
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - attack));
            if (turn == 1)
            {
                Assert.That(attack, Is.EqualTo(10));
                Assert.That(state.ThunderWeaknessTurns, Is.Zero);
                Assert.That(ReadField(state, "CurseWeakTurns"), Is.EqualTo(0));
            }
            else
            {
                Assert.That(session.Battle.EnemyBlock, Is.EqualTo(7));
                Assert.That(ReadField(state, "CurseArmorTurns"), Is.EqualTo(0));
            }
        }

        [Test]
        public void BoardingGuard_UsesItsOwnBaselineInsteadOfNavalGuard()
        {
            CoreVoyageSession session = Battle("arcane");
            Assert.That(session.Battle.Profile.Guard, Is.EqualTo(79));
            Assert.That(session.Battle.Profile.BoardingGuard, Is.EqualTo(10));
            End(session);
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(10));
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.PhaseTurn = 2;
            session.Battle.DistanceMeters = 300;
            session.Battle.EnemyLoadedShot = 0;
            End(session);
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(79), "Naval turns still use the original hull guard value.");
        }

        [TestCase(1, 1)] [TestCase(1, 2)] [TestCase(4, 1)] [TestCase(4, 3)]
        public void BloodSelfDamage_CannotKillItsOwnCrew(int turn, int health)
        {
            CoreVoyageSession session = Battle("blood", turn);
            SetDeck(session, new CoreBoardingEnemyDeck("blood", new CoreBoardingEnemyTurn("支付生命",
                new CoreBoardingEnemyCard("self-cost", "支付生命", selfDamagePercent: turn == 1 ? 15 : 20))));
            session.Battle.EnemyBoardingHealth = health;
            End(session);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(1));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
        }

        [TestCase(21, 50, 0)]
        [TestCase(19, 50, 2)]
        [TestCase(0, 50, 4)]
        [TestCase(0, 99, 1)]
        [TestCase(0, 100, 0)]
        public void BloodDrain_UsesActualHealthLostAndNeverExceedsMaximum(int guard, int enemyHealth, int healed)
        {
            CoreVoyageSession session = Battle("blood", 3);
            SetDeck(session, new CoreBoardingEnemyDeck("blood", new CoreBoardingEnemyTurn("吸血攻击",
                new CoreBoardingEnemyCard("drain", "吸血攻击", attackPercent: 105, drainPercent: 20))));
            session.Battle.EnemyMaxBoardingHealth = 100;
            session.Battle.EnemyBoardingHealth = enemyHealth;
            session.Battle.PlayerBlock = guard;
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - Math.Max(0, 21 - guard)));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemyHealth + healed));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.LessThanOrEqualTo(session.Battle.EnemyMaxBoardingHealth));
        }

        [TestCase(50, 55)] [TestCase(98, 100)] [TestCase(100, 100)]
        public void TideRecovery_StopsAtMaximumHealth(int health, int expected)
        {
            CoreVoyageSession session = Battle("tide", 3);
            SetDeck(session, new CoreBoardingEnemyDeck("tide", new CoreBoardingEnemyTurn("基础治疗",
                new CoreBoardingEnemyCard("heal", "基础治疗", healPercent: 25))));
            session.Battle.EnemyBoardingHealth = health;
            session.Battle.EnemyMaxBoardingHealth = 100;
            End(session);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(expected));
        }

        [Test]
        public void FireBreakthrough_DecaysAtEachLaterPlayerEndUntilZero()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, LegacyFireDeck());
            int initial = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(initial - 14), "Ignition must not tick on the application turn.");
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(3));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnTurns, Is.EqualTo(3));
            Assert.That(session.Battle.Boarding.SelfBurn, Is.Empty, "Hostile ignition is separate from voluntary self-burn.");
            session.Battle.PlayerBlock = 1000;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(initial - 17));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnTurns, Is.EqualTo(2));
            session.Battle.PlayerBlock = 1000;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(initial - 19), "The second tick deals the two remaining layers through guard.");
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(1));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnTurns, Is.EqualTo(1));
            session.Battle.PlayerBlock = 1000;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(initial - 20));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.Zero);
        }

        [Test]
        public void FireThirdTurn_ReportsHostileBurnAndActualAttackAsSeparateHealthLosses()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, LegacyFireDeck());
            End(session); // Turn one applies three layers of ignition.
            End(session); // Turn two consumes its first tick while the enemy guards.
            Assert.That(session.Battle.PhaseTurn, Is.EqualTo(3));
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            CoreBoardingEnemyState result = session.Battle.BoardingEnemy;
            Assert.That(result.LastBurnPlayerHealth, Is.EqualTo(health));
            Assert.That(result.LastBurnHealthLost, Is.EqualTo(2));
            Assert.That(result.LastAttackExecuted, Is.True);
            Assert.That(result.LastAttackPlayerHealth, Is.EqualTo(health - 2));
            Assert.That(result.LastAttackHealthLost, Is.EqualTo(29), "The melee result must exclude the earlier burn tick.");
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - 2 - 29));
        }

        [Test]
        public void LethalHostileBurn_ReportsOnlyTheTickAndCancelsTheScheduledAttack()
        {
            // Defeat must settle a real voyage. A training session whose profile
            // was replaced for numerical fixtures has no voyage map to settle.
            var session = new CoreVoyageSession();
            session.SetSeed("BOARDING-HOSTILE-BURN-DEFEAT");
            session.StartVoyage();
            CoreEncounter encounter = CoreRouteBossCatalog.All.Single(boss => boss.SchoolId == "fire").CreateEncounter();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, true });
            End(session);
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            SetDeck(session, LegacyFireDeck());
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 10000;
            End(session);
            int burn = session.Battle.BoardingEnemy.PlayerBurnDamage;
            Assert.That(burn, Is.GreaterThan(0));
            End(session);
            Assert.That(session.Battle.PhaseTurn, Is.EqualTo(3));
            Assert.That(session.EnemyBoardingAttackDamage, Is.GreaterThan(0), "An attack was scheduled before the player's lethal end-of-turn burn.");
            burn = session.Battle.BoardingEnemy.PlayerBurnDamage;
            session.Battle.PlayerBoardingHealth = burn;
            CoreBoardingEnemyState result = session.Battle.BoardingEnemy;
            End(session);
            Assert.That(result.LastBurnPlayerHealth, Is.EqualTo(burn));
            Assert.That(result.LastBurnHealthLost, Is.EqualTo(burn));
            Assert.That(result.LastAttackExecuted, Is.False);
            Assert.That(result.LastAttackPlayerHealth, Is.Zero);
            Assert.That(result.LastAttackHealthLost, Is.Zero);
            Assert.That(session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
        }

        [Test]
        public void FireGuardTurn_ReportsBurnWithoutReusingThePreviousAttackResult()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, LegacyFireDeck());
            End(session);
            CoreBoardingEnemyState result = session.Battle.BoardingEnemy;
            Assert.That(result.LastAttackExecuted, Is.True);
            Assert.That(result.LastAttackHealthLost, Is.EqualTo(14));
            Assert.That(session.Battle.PhaseTurn, Is.EqualTo(2));
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(result.LastBurnPlayerHealth, Is.EqualTo(health));
            Assert.That(result.LastBurnHealthLost, Is.EqualTo(3));
            Assert.That(result.LastAttackExecuted, Is.False);
            Assert.That(result.LastAttackPlayerHealth, Is.Zero);
            Assert.That(result.LastAttackHealthLost, Is.Zero);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - 3));
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(10));
        }

        [TestCase(14)] [TestCase(100)]
        public void FullyBlockedFireStrike_DoesNotIgnite(int guard)
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, LegacyFireDeck());
            session.Battle.PlayerBlock = guard;
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.Zero);
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnTurns, Is.Zero);
        }

        [TestCase(0, 15, 0)] [TestCase(7, 8, 7)] [TestCase(12, 3, 12)] [TestCase(100, 3, 12)]
        public void CurseAttack_OnlyItsPiercingShareBypassesGuard(int guard, int lost, int absorbed)
        {
            CoreVoyageSession session = Battle("curse");
            SetDeck(session, LegacyCurseDeck());
            session.Battle.PlayerBlock = guard;
            Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(15));
            Assert.That(session.EnemyBoardingPiercingDamage, Is.EqualTo(3));
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - lost));
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(absorbed));
        }

        [Test]
        public void ScriptedAttack_StillTriggersThePlayersCurseCounter()
        {
            CoreVoyageSession session = Battle("thunder");
            SetDeck(session, new CoreBoardingEnemyDeck("thunder", new CoreBoardingEnemyTurn("单次攻击",
                new CoreBoardingEnemyCard("counter-trigger", "单次攻击", attackPercent: 75))));
            AddMarks(session, 2);
            SetField(session.Battle.Boarding, "CurseCounterTurns", 2);
            SetField(session.Battle.Boarding, "CurseCounterCard", Card("curse_09"));
            session.Battle.PlayerBlock = 100;
            int enemyHealth = session.Battle.EnemyBoardingHealth;
            End(session);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemyHealth - 6));
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(15));
        }

        private static CoreVoyageSession Battle(string school, int turn = 1)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("BOARDING-ENEMY-TEST");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            End(session);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                10000, 20, 20, 79, 0, 28, 1, 1, healthMultiplier: 1, boardingGuard: 10);
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool(school));
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 10000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.PhaseTurn = turn;
            session.Cargo.Curse = 0;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            return session;
        }

        private static void End(CoreVoyageSession session) => Assert.That(session.EndBattleTurn(), Is.True);
        // Focused engine contracts keep independent numerical baselines. The authored
        // six-school decks and their resource rotations are exercised above and in
        // CoreBoardingEnemySchoolTests, so changing a rotation cannot weaken these checks.
        private static CoreBoardingEnemyDeck LegacyFireDeck() => new CoreBoardingEnemyDeck("fire",
            new CoreBoardingEnemyTurn("点燃", new CoreBoardingEnemyCard("ignite", "点燃", attackPercent: 70, burnPercent: 15)),
            new CoreBoardingEnemyTurn("防御", new CoreBoardingEnemyCard("guard", "防御", guardPercent: 100)),
            new CoreBoardingEnemyTurn("强攻", new CoreBoardingEnemyCard("strike", "强攻", attackPercent: 145)),
            new CoreBoardingEnemyTurn("防卫攻击", new CoreBoardingEnemyCard("guard-strike", "防卫攻击", attackPercent: 80, guardPercent: 50)));
        private static CoreBoardingEnemyDeck LegacyCurseDeck() => new CoreBoardingEnemyDeck("curse",
            new CoreBoardingEnemyTurn("部分穿盾", new CoreBoardingEnemyCard("pierce", "部分穿盾", attackPercent: 75, piercePercent: 25)),
            new CoreBoardingEnemyTurn("护甲封印", new CoreBoardingEnemyCard("armor", "护甲封印", guardPercent: 100)));
        private static void SetDeck(CoreVoyageSession session, CoreBoardingEnemyDeck deck) =>
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy, deck);
        private static CoreBattleCard Card(string key) => CoreVoyageSession.CardLibrary.Single(card => card.Id == "card_boarding_" + key);
        private static void SetField(CoreBoardingBattleState state, string name, object value) =>
            typeof(CoreBoardingBattleState).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, value);
        private static object ReadField(CoreBoardingBattleState state, string name) =>
            typeof(CoreBoardingBattleState).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
        private static void AddMarks(CoreVoyageSession session, int count) => typeof(CoreVoyageSession)
            .GetMethod("CTBAddMarks", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { count });
        private static uint RandomState(CoreVoyageSession session) => ((Mulberry32Random)typeof(CoreVoyageSession)
            .GetField("voyageRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session)).State;
        private static string Snapshot(CoreVoyageSession session)
        {
            CoreBattleState battle = session.Battle;
            CoreBoardingBattleState state = battle.Boarding;
            return string.Join("|", new object[] {
                battle.PhaseTurn, battle.Turn, battle.Energy, battle.PlayerBoardingHealth, battle.EnemyBoardingHealth,
                battle.PlayerBlock, battle.EnemyBlock, battle.BoardingEnemy.Deck.SchoolId,
                battle.BoardingEnemy.PlayerBurnDamage, battle.BoardingEnemy.PlayerBurnTurns,
                state.ThunderWeakness, state.ThunderWeaknessTurns, ReadField(state, "CurseWeakTurns"), ReadField(state, "CurseArmorTurns"),
                ReadField(state, "CurseCounterTurns"), ReadField(state, "CurseCounterTriggered"), state.CurseMarks, state.Revision,
                RandomState(session), string.Join(",", battle.Hand), string.Join(",", battle.DrawPile),
                string.Join(",", battle.DiscardPile), battle.LastAction, session.Cargo.Curse
            });
        }
    }
}
