using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalCannonRangeTests
    {
        [TestCase(4, 0, true)]
        [TestCase(0, 4, true)]
        [TestCase(3, 1, true)]
        [TestCase(2, 2, true)]
        [TestCase(-3, 1, true)]
        [TestCase(2, -2, true)]
        [TestCase(-4, 0, true)]
        [TestCase(0, -4, true)]
        [TestCase(3, 2, false)]
        [TestCase(-3, 2, false)]
        [TestCase(3, -2, false)]
        [TestCase(-3, -2, false)]
        [TestCase(2, 3, false)]
        [TestCase(4, 1, false)]
        [TestCase(5, 0, false)]
        public void RangeCountsHorizontalPlusVerticalCellsAcrossAllDirections(int columns, int rows, bool inRange)
        {
            // Translate away from the origin so this checks relative cell coordinates.
            var player = new VoyagePoint(1100f, -700f);
            var enemy = new VoyagePoint(player.X + columns * 100f, player.Y + rows * 100f);
            Assert.That(CoreVoyageSession.NavalGridDistance(player, enemy), Is.EqualTo(Math.Abs(columns) + Math.Abs(rows)));
            Assert.That(CoreVoyageSession.NavalCannonInRange(player, enemy), Is.EqualTo(inRange));
            Assert.That(CoreVoyageSession.NavalCannonInRange(enemy, player), Is.EqualTo(inRange));
        }

        [TestCase("card_chain_shot", 4, 0, true)]
        [TestCase("card_chain_shot", 2, 2, true)]
        [TestCase("card_chain_shot", 3, 2, false)]
        [TestCase("card_cannon_barrage", 3, 1, true)]
        [TestCase("card_cannon_barrage", 3, 2, false)]
        [TestCase("card_school_fire_b03", 3, 1, true)]
        [TestCase("card_school_fire_b03", 3, 2, false)]
        public void CannonCardsUseGridRangeAndFailedShotsSpendNothing(string id, int columns, int rows, bool inRange)
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session.Battle, columns * 100f, rows * 100f);
            int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(card);
            session.Battle.PlayerLoadedShot = 7;
            int energy = session.Battle.Energy, powder = session.Cargo.Powder, balls = session.Cargo.Cannonballs;
            int hull = session.Battle.EnemyHull, actions = session.Battle.NavalActionPoints, discard = session.Battle.DiscardPile.Count;
            Assert.That(session.NavalDistanceCells, Is.EqualTo(Math.Abs(columns) + Math.Abs(rows)));
            Assert.That(session.CanPlayCard(0), Is.EqualTo(inRange), session.CardUnavailableReason(0));
            if (!inRange)
            {
                Assert.That(session.Battle.DistanceMeters, Is.LessThan(400f), "The forbidden three-by-two position is inside the old circular 400 m range.");
                Assert.That(session.CardUnavailableReason(0), Is.EqualTo(CoreVoyageSession.NavalCannonOutOfRangeReason));
            }
            Assert.That(session.PlayCard(0), Is.EqualTo(inRange));
            if (inRange)
            {
                Assert.That(session.Battle.EnemyHull, Is.LessThan(hull));
                Assert.That(session.Battle.PlayerLoadedShot, Is.Zero);
                Assert.That(session.Cargo.Cannonballs, Is.EqualTo(balls - 1));
            }
            else
            {
                Assert.That(session.Battle.Energy, Is.EqualTo(energy));
                Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
                Assert.That(session.Cargo.Cannonballs, Is.EqualTo(balls));
                Assert.That(session.Battle.EnemyHull, Is.EqualTo(hull));
                Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(7));
                Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(actions));
                Assert.That(session.Battle.Hand, Is.EqualTo(new[] { card }));
                Assert.That(session.Battle.DiscardPile.Count, Is.EqualTo(discard));
                Assert.That(session.GetBattleCardEnergyCost(card), Is.EqualTo(session.GetBattleCard(card).Energy));
            }
        }

        [Test]
        public void EnemyOutsideFourCellsAdvancesWithoutFiringEvenWithinFourHundredMetres()
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session.Battle, 300f, 200f);
            int hull = session.Hull;
            Assert.That(session.GetEnemyIntent(), Does.Contain("追击"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull));
        }

        [Test]
        public void EnemyAtFourCellsCanFire()
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session.Battle, 400f, 0f);
            int hull = session.Hull;
            Assert.That(session.GetEnemyIntent(), Does.Contain("炮击"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Hull, Is.LessThan(hull));
        }

        [Test]
        public void EnemyMovementOutOfRangeCancelsTheShotAndPreservesLoadedDamage()
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session.Battle, 400f, 0f);
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { false, new VoyagePoint(400f, 0f), 0f });
            session.Battle.EnemyLoadedShot = 7;
            int hull = session.Hull;
            Assert.That(session.GetEnemyIntent(), Does.Contain("机动后仍在4格内时开炮"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.NavalDistanceCells, Is.EqualTo(5));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.EnemyLoadedShot, Is.EqualTo(7));
            Assert.That(session.Battle.LastAction, Does.Contain("未开炮"));
        }

        [Test]
        public void StationaryTrainingCannonAlsoRespectsFourCellRange()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            SetPose(session.Battle, 300f, 200f);
            int hull = session.Hull;
            Assert.That(session.GetEnemyIntent(), Does.Contain("超出4格"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull));
            SetPose(session.Battle, 200f, 200f);
            Assert.That(session.GetEnemyIntent(), Does.Contain("炮击伤害"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Hull, Is.LessThan(hull));
        }

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("NAVAL-FOUR-CELL-RANGE");
            session.StartVoyage();
            var encounter = new CoreEncounter("cannon_range", "射程测试敌舰", "海战", "四格炮击测试", true, false, 1, 1000);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.PhaseTurn = 1;
            session.Battle.Energy = 10;
            session.Battle.EnemyBlock = session.Battle.PlayerBlock = 0;
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 1000, 30, 2, 3, 0, 8, 1f, 1f);
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            return session;
        }

        private static void SetPose(CoreBattleState battle, float x, float y)
        {
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            pose.Invoke(battle, new object[] { true, new VoyagePoint(0f, 0f), 0f });
            pose.Invoke(battle, new object[] { false, new VoyagePoint(x, y), 180f });
        }
    }
}
