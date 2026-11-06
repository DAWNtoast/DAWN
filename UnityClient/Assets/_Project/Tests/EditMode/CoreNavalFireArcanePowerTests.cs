using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalFireArcanePowerTests
    {
        [TestCase("card_school_fire_n07", 0, 18, 6)]
        [TestCase("card_school_fire_n07", 1, 30, 10)]
        [TestCase("card_school_fire_n07", 2, 36, 24)]
        [TestCase("card_school_fire_n07", 3, 40, 32)]
        [TestCase("card_school_fire_n08", 0, 8, 3)]
        [TestCase("card_school_fire_n08", 1, 11, 5)]
        [TestCase("card_school_fire_n08", 2, 14, 12)]
        [TestCase("card_school_fire_n08", 3, 14, 16)]
        public void MixedCannonsScaleTheCannonButKeepBurnInAuthoredLayers(string id, int rank, int damage, int burn)
        {
            var s = Start(id, rank);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            s.Battle.EnemyBlock = 11;
            int hull = s.Battle.EnemyHull;
            int expected = CoreNavalStatRules.ScaleCannonDamage(damage, 55);
            Assert.That(card.Damage, Is.EqualTo(damage));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expected));
            AssertFaces(s, card, "施加 " + burn + " 层燃烧");
            Play(s, id);
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(expected - 11));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(burn));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(burn));
            Assert.That(s.Cargo.Powder, Is.EqualTo(19));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(19));
            Assert.That(s.Battle.Energy, Is.EqualTo(100 - card.Energy));
            if (id == "card_school_fire_n08") Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
        }

        [TestCase("card_school_fire_n07", 0, 6)]
        [TestCase("card_school_fire_n07", 3, 32)]
        [TestCase("card_school_fire_n08", 0, 3)]
        [TestCase("card_school_fire_n08", 3, 16)]
        public void BurnWorksThroughGuardAndOilDoublesOnlyItsFirstApplication(string id, int rank, int burn)
        {
            var s = Start(id, rank);
            Play(s, "card_school_fire_n03");
            s.Battle.EnemyBlock = 10000;
            int hull = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(2 * burn));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Play(s, id);
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(3 * burn));
        }

        [TestCase(0, 4, 1)] [TestCase(1, 6, 2)]
        [TestCase(2, 10, 4)] [TestCase(3, 16, 6)]
        public void CapacityGuardPaysNowAndRefillsItsLargerPoolOnlyNextTurn(int rank, int guard, int capacity)
        {
            const string id = "card_school_arcane_n05";
            var s = Start(id, rank);
            Set(s.Battle.Schools, "Mana", 1);
            var card = s.GetBattleCard(Index(id));
            AssertFaces(s, card, "法力上限提高 " + capacity + " 点");
            Play(s, id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(3 + capacity));
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3 + capacity));
        }

        [TestCase(0, 6, 4)] [TestCase(1, 9, 5)]
        [TestCase(2, 14, 7)] [TestCase(3, 22, 10)]
        public void ChargeGuardCanExceedCapacityButExcessExpiresAtNextRefill(int rank, int guard, int mana)
        {
            const string id = "card_school_arcane_n06";
            var s = Start(id, rank);
            var card = s.GetBattleCard(Index(id));
            Set(s.Battle.Schools, "Mana", 3);
            AssertFaces(s, card, "获得 " + mana + " 点法力");
            Play(s, id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard));
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3 + mana));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(3));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3));
        }

        [TestCase(0, 100)] [TestCase(1, 130)] [TestCase(2, 400)] [TestCase(3, 440)]
        public void FinaleRequiresEightManaAndConsumesExactlyEightWithoutCannonScaling(int rank, int damage)
        {
            const string id = "card_school_arcane_n07";
            var s = Start(id, rank);
            Set(s.Battle.Schools, "Mana", 7);
            Put(s, id);
            int hull = s.Battle.EnemyHull, energy = s.Battle.Energy, discard = s.Battle.DiscardPile.Count;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(7));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index(id) }));
            Assert.That(s.Battle.DiscardPile.Count, Is.EqualTo(discard));
            Assert.That(s.GetBattleCardEnergyCost(Index(id)), Is.EqualTo(3));
            Set(s.Battle.Schools, "Mana", 11);
            s.Battle.EnemyBlock = 17;
            var card = s.GetBattleCard(Index(id));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(damage));
            AssertFaces(s, card, "造成 " + damage + " 点船体伤害");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(damage - 17));
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy - 3));
            Assert.That(s.Cargo.Powder + s.Cargo.Cannonballs, Is.EqualTo(40));
        }

        [TestCase("card_school_fire_n07")] [TestCase("card_school_fire_n08")]
        public void MissingAmmunitionCannotApplyBurnOrDrawOrSpendEnergy(string id)
        {
            var s = Start(id, 0);
            s.Cargo.Cannonballs = 0;
            Put(s, id);
            int hull = s.Battle.EnemyHull, discard = s.Battle.DiscardPile.Count;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(100));
            Assert.That(s.Cargo.Powder, Is.EqualTo(20));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index(id) }));
            Assert.That(s.Battle.DiscardPile.Count, Is.EqualTo(discard));
        }

        private static CoreVoyageSession Start(string id, int rank)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            s.ConfigureComponentBonuses(1000, 0, 49, 0, 0);
            s.SetSeed("NAVAL-FIRE-ARCANE-POWER");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.PlayerLoadedShot = 0;
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 0;
            s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = 20;
            Set(s, "Morale", 0);
            return s;
        }
        private static void AssertFaces(CoreVoyageSession s, CoreBattleCard card, string clause)
        {
            foreach (string text in new[] { card.Description, s.BattleCardFaceDescription(card),
                s.BattleCardEffectDescription(card, NavalBattleStage.Naval) })
                Assert.That(text, Does.Contain(clause).And.Not.Contain("{"));
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id)
        {
            Put(s, id);
            Assert.That(s.PlayCard(0), Is.True, id + ": " + s.CardUnavailableReason(0));
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
