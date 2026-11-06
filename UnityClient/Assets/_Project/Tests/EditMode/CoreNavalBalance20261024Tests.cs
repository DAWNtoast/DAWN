using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBalance20261024Tests
    {
        [TestCase("card_school_fire_b04", 0, 20, 10)]
        [TestCase("card_school_fire_b04", 1, 26, 13)]
        [TestCase("card_school_fire_b04", 2, 34, 18)]
        [TestCase("card_school_blood_b06", 0, 20, 10)]
        [TestCase("card_school_blood_b06", 1, 26, 14)]
        [TestCase("card_school_blood_b06", 2, 34, 19)]
        public void ConditionalCannonsKeepTheirBonusAtHighFirepowerWithoutChangingCosts(string id, int rank, int damage, int bonus)
        {
            foreach (bool active in new[] { false, true })
            {
                var s = Start(id, rank);
                var card = s.GetBattleCard(Index(id));
                Assert.That(card.Damage, Is.EqualTo(damage));
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(bonus));
                Assert.That(card.Energy, Is.EqualTo(2));
                Assert.That(card.PowderCost, Is.EqualTo(id.Contains("fire") ? 2 : 1));
                Assert.That(card.CannonballCost, Is.EqualTo(1));
                if (id.Contains("fire")) s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = active ? 1 : 0;
                else s.Battle.EnemyHull = active ? 3500 : 3600;
                int expected = CoreNavalStatRules.ScaleCannonDamage(damage, 55) + (active ? bonus : 0);
                int before = s.Battle.EnemyHull;
                Play(s, id);
                Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(expected));
                Assert.That(s.Battle.Energy, Is.EqualTo(1));
                Assert.That(s.Cargo.Powder, Is.EqualTo(20 - card.PowderCost));
                Assert.That(s.Cargo.Cannonballs, Is.EqualTo(19));
            }
        }

        [TestCase(0, 26)] [TestCase(1, 42)] [TestCase(2, 90)]
        public void StarBeamUsesUpgradedNormalHitAndFixedPiercingWithoutFirepowerScaling(int rank, int normal)
        {
            const string id = "card_school_arcane_n03";
            var s = Start(id, rank);
            var card = s.GetBattleCard(Index(id));
            Set(s.Battle.Schools, "Mana", 2);
            s.Battle.EnemyBlock = 20;
            int before = s.Battle.EnemyHull;
            Assert.That(card.Damage, Is.EqualTo(normal));
            Assert.That(card.Description, Does.Contain("10 点无视格挡伤害").And.Contain("造成 " + normal + " 点伤害"));
            Assert.That(s.BattleCardFaceDescription(card), Does.Contain("造成 " + normal + " 点伤害"));
            Play(s, id);
            Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(10 + normal - 20));
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Cargo.Powder + s.Cargo.Cannonballs, Is.EqualTo(40));
        }

        [TestCase(0, 18, 8)] [TestCase(1, 23, 16)] [TestCase(2, 30, 24)]
        public void CollapseUsesOneCoefficientForBothFacesAndAreaExecution(int rank, int baseDamage, int coefficient)
        {
            const string id = "card_school_thunder_n06";
            var s = Start(id, rank);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 2, 0, false, 0, 0), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 3);
            var card = s.GetBattleCard(Index(id));
            string formula = baseDamage + "+" + coefficient + "X+4×";
            Assert.That(card.Description, Does.Contain(formula));
            Assert.That(s.BattleCardFaceDescription(card), Does.Contain(formula).And.Not.Contain("{"));
            Assert.That(CoreNavalExpansionBalance.ThunderCollapseChainCoefficient(rank), Is.EqualTo(coefficient));
            int before = s.Battle.EnemyHull;
            int count = s.Battle.NavalFields.Cells.Count;
            Play(s, id, new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 2, AnchorY = 0 });
            Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(baseDamage + coefficient * 3 + 4 * 4));
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(count - 4));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero, "Outside the grid, the finisher clears after attack-chain rewards resolve.");
            Assert.That(s.Battle.Energy, Is.Zero);
        }

        [Test]
        public void CollapseLargeResourceQuoteSaturatesWithoutOverflow()
        {
            Assert.That(CoreNavalExpansionBalance.ThunderCollapseDamage(30, 2, int.MaxValue, 4), Is.EqualTo(int.MaxValue));
        }

        [TestCase("card_school_arcane_b03", 0, 10, 1)]
        [TestCase("card_school_arcane_b03", 1, 12, 1)]
        [TestCase("card_school_arcane_b03", 2, 28, 1)]
        [TestCase("card_school_arcane_b03", 3, 40, 1)]
        [TestCase("card_school_arcane_b04", 0, 25, 2)]
        [TestCase("card_school_arcane_b04", 1, 33, 2)]
        [TestCase("card_school_arcane_b04", 2, 70, 2)]
        public void FixedArcaneDamageGrowsByItsAuthoredRankWithoutFirepowerOrCostChanges(string id, int rank, int damage, int mana)
        {
            var s = Start(id, rank);
            Set(s.Battle.Schools, "Mana", 3);
            s.Battle.EnemyBlock = 5;
            var card = s.GetBattleCard(Index(id));
            Assert.That(card.Damage, Is.EqualTo(damage));
            Assert.That(card.SchoolEffect.Threshold, Is.EqualTo(mana));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(damage));
            Assert.That(s.BattleCardFaceDescription(card), Does.Contain("造成 " + damage + " 点船体伤害"));
            int before = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(damage - 5));
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3 - mana));
            Assert.That(s.Battle.Energy, Is.EqualTo(3 - card.Energy));
        }

        [TestCase(0, 6, 2, 0)] [TestCase(0, 6, 2, 25)]
        [TestCase(1, 12, 4, 0)] [TestCase(1, 12, 4, 25)]
        [TestCase(2, 15, 6, 0)] [TestCase(2, 15, 6, 25)]
        [TestCase(3, 18, 7, 0)] [TestCase(3, 18, 7, 25)]
        public void TideStrikeScalesOnlyTheAuthoredResourceCoefficientAndPaysTheWholeReserve(int rank, int damage, int coefficient, int tide)
        {
            const string id = "card_school_tide_n02";
            var s = Start(id, rank);
            var card = s.GetBattleCard(Index(id));
            Set(s.Battle.Schools, "TidePower", tide);
            s.Battle.PlayerLoadedShot = 7;
            s.Battle.EnemyBlock = 20;
            int cannon = CoreNavalStatRules.ScaleCannonDamage(damage, 55) + 7;
            Assert.That(card.Damage, Is.EqualTo(damage));
            Assert.That(CoreNavalExpansionBalance.TideStrikeDamageCoefficient(rank), Is.EqualTo(coefficient));
            Assert.That(card.Description, Does.Contain(damage + "＋" + coefficient + "X"));
            Assert.That(s.BattleCardFaceDescription(card), Does.Contain(cannon + "＋" + coefficient + "X").And.Not.Contain("{"));
            Assert.That(s.BattleCardEffectDescription(card, NavalBattleStage.Naval), Does.Contain("＋" + coefficient + "X").And.Not.Contain("{"));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(cannon), "The cannon quote is the fixed slot; the formula adds X only during execution.");
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(tide), "All previews are read-only.");
            int before = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(before - s.Battle.EnemyHull, Is.EqualTo(cannon + coefficient * tide - 20));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.Cargo.Powder, Is.EqualTo(19));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(19));
        }

        [Test]
        public void TideStrikeHugeReserveDoesNotOverflowAndStillPaysIt()
        {
            const string id = "card_school_tide_n02";
            var s = Start(id, 3);
            Set(s.Battle.Schools, "TidePower", int.MaxValue);
            Play(s, id);
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        private static CoreVoyageSession Start(string id, int rank)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            s.ConfigureComponentBonuses(1000, 0, 49, 0, 0);
            s.SetSeed("NAVAL-BALANCE-20261024");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.PlayerLoadedShot = 0;
            s.Battle.Energy = 3;
            s.Cargo.Powder = s.Cargo.Cannonballs = 20;
            Set(s, "Morale", 0);
            Assert.That(s.EffectiveFirepower, Is.EqualTo(55));
            return s;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Play(CoreVoyageSession s, string id, CoreNavalCardPlayOptions options = null)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id));
            bool success = CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? s.PlayCard(0) : s.PlayNavalCard(0, options);
            Assert.That(success, Is.True, id + ": " + s.CardUnavailableReason(0));
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
