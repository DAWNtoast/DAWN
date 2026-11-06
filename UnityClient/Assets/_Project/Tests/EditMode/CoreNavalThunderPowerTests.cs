using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalThunderPowerTests
    {
        [TestCase(0, 4)] [TestCase(1, 6)] [TestCase(2, 8)]
        public void RelayDealsFixedDamageDrawsAndBuildsChainWithoutAField(int rank, int damage)
        {
            var s = Start(CoreNavalThunderPower.RelayId, rank);
            int health = s.Battle.EnemyHull;
            Set(s.Battle.Schools, "ThunderChain", 2);
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(health - s.Battle.EnemyHull, Is.EqualTo(damage));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Cargo.Powder + s.Cargo.Cannonballs, Is.EqualTo(40));
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Energy, Is.EqualTo(2), "Copies share the same escalating energy cost.");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(4));
        }

        [TestCase(0, 1)] [TestCase(1, 2)]
        public void CircuitRequiresChainAndReturnsEnergyOnlyOnceWithoutSpendingChain(int rank, int recovery)
        {
            var s = Start(CoreNavalThunderPower.CircuitId, rank);
            Set(s.Battle.Schools, "ThunderChain", 2);
            Hand(s, CoreNavalThunderPower.CircuitId);
            Assert.That(s.CanPlayCard(0), Is.False);
            Assert.That(s.CardUnavailableReason(0), Does.Contain("连锁"));
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Set(s.Battle.Schools, "ThunderChain", 3);
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(3 + recovery));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Hand(s, CoreNavalThunderPower.CircuitId);
            Assert.That(s.CanPlayCard(0), Is.False);
            Assert.That(s.CardUnavailableReason(0), Does.Contain("本回合"));
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(3 + recovery));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.EndBattleTurn(), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 3);
            s.Battle.Energy = 3;
            Play(s, CoreNavalThunderPower.CircuitId);
            Assert.That(s.Battle.Energy, Is.EqualTo(3 + recovery), "The once-per-turn gate resets normally.");
        }

        [TestCase(0, 0, 12)] [TestCase(0, 4, 12)] [TestCase(0, 5, 24)] [TestCase(0, 9, 24)] [TestCase(0, 10, 42)]
        [TestCase(1, 0, 12)] [TestCase(1, 4, 12)] [TestCase(1, 5, 24)] [TestCase(1, 9, 24)] [TestCase(1, 10, 48)]
        [TestCase(2, 0, 12)] [TestCase(2, 4, 12)] [TestCase(2, 5, 112)] [TestCase(2, 9, 112)] [TestCase(2, 10, 276)]
        [TestCase(3, 0, 12)] [TestCase(3, 4, 12)] [TestCase(3, 5, 112)] [TestCase(3, 9, 112)] [TestCase(3, 10, 288)]
        public void PayoffReadsThresholdBeforeTheAttackAndAppliesEachRewardOnce(int rank, int chain, int damage)
        {
            var s = Start(CoreNavalThunderPower.PayoffId, rank);
            Set(s.Battle.Schools, "ThunderChain", chain);
            var card = s.GetBattleCard(Index(CoreNavalThunderPower.PayoffId));
            int health = s.Battle.EnemyHull;
            Assert.That(card.Damage, Is.EqualTo(12), "Upgrades strengthen chain rewards rather than unconditional damage.");
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(damage));
            Assert.That(s.BattleCardFaceDescription(card), Does.Contain("造成 " + card.Damage + " 点")
                .And.Contain(CoreNavalThunderPower.PayoffFive(rank).ToString())
                .And.Contain(CoreNavalThunderPower.PayoffTen(rank).ToString()).And.Not.Contain("{"));
            Assert.That(card.Description, Does.Not.Contain("{"));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(chain), "Preview is read-only.");
            Play(s, CoreNavalThunderPower.PayoffId);
            Assert.That(health - s.Battle.EnemyHull, Is.EqualTo(damage));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero, "The finisher clears after normal attack-chain processing.");
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
        }

        [Test]
        public void FixedPrechargeBuildsAShortChainThatResetsOutsideAFieldAtTheNextTurn()
        {
            const string id = "card_school_thunder_n03";
            var s = Start(id, 0);
            var card = s.GetBattleCard(Index(id));
            Assert.That(card.Description, Does.Not.Contain("{"));
            Assert.That(s.BattleCardFaceDescription(card), Does.Not.Contain("{"));
            Play(s, id);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.Battle.NavalExpansion.PrechargeBonus, Is.EqualTo(2));
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.Battle.NavalExpansion.Precharge, Is.False);
            Assert.That(s.Battle.NavalExpansion.PrechargeBonus, Is.Zero);
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void ThunderFieldCarriesReserveAcrossTurnsAndAllowsRepeatedHighChainFinishers()
        {
            var s = Start(CoreNavalThunderPower.PayoffId, 3);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            for (int turn = 1; turn <= 3; turn++)
            {
                Play(s, "card_school_thunder_n03");
                Play(s, CoreNavalThunderPower.RelayId);
                Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(turn * 3));
                if (turn == 3) break;
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(turn * 3), "The field preserves the reserve at the next turn start.");
            }
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(10));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            int before = s.Battle.EnemyHull, guard = s.Battle.EnemyBlock;
            Play(s, CoreNavalThunderPower.PayoffId);
            Assert.That(before - s.Battle.EnemyHull + guard - s.Battle.EnemyBlock, Is.EqualTo(288));
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(11));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(11));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0)), Is.EqualTo(2));
            before = s.Battle.EnemyHull; guard = s.Battle.EnemyBlock;
            Play(s, CoreNavalThunderPower.PayoffId);
            Assert.That(before - s.Battle.EnemyHull + guard - s.Battle.EnemyBlock, Is.EqualTo(288));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(12));
        }

        [Test]
        public void PayoffBaseFaceKeepsExternalAttackBonusSeparateFromThresholdRewards()
        {
            var s = Start(CoreNavalThunderPower.PayoffId, 0);
            Set(s.Battle.Schools, "ThunderChain", 10);
            Set(s.Battle.Schools, "ArcaneNextAttackDamage", 7);
            var card = s.GetBattleCard(Index(CoreNavalThunderPower.PayoffId));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(49));
            Assert.That(s.BattleCardFaceDescription(card), Does.Contain("造成 19 点").And.Not.Contain("造成 49 点"));
            int health = s.Battle.EnemyHull;
            Play(s, CoreNavalThunderPower.PayoffId);
            Assert.That(health - s.Battle.EnemyHull, Is.EqualTo(49));
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void FinalThunderRequiresTwoEnergyEvenWhenItsChainConsumptionIsExempt(bool inField)
        {
            const string id = "card_school_thunder_b06";
            var s = Start(id, 0);
            if (inField)
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 49);
            s.Battle.NavalExpansion.Precharge = true;
            s.Battle.NavalExpansion.PrechargeBonus = 2;
            s.Battle.PlayerBlock = 1000;
            s.Battle.Energy = 1;
            Hand(s, id);
            int health = s.Hull, enemy = s.Battle.EnemyHull, played = s.Battle.CardsPlayedThisTurn;
            int cells = s.Battle.NavalFields.Cells.Count;
            int[] held = s.Battle.Hand.ToArray(), discarded = s.Battle.DiscardPile.ToArray();
            Assert.That(s.GetBattleCard(Index(id)).Energy, Is.EqualTo(2));
            Assert.That(s.CanPlayCard(0), Is.False);
            Assert.That(s.CardUnavailableReason(0), Does.Contain("能量不足"));
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(49));
            Assert.That(s.Battle.NavalExpansion.Precharge, Is.True);
            Assert.That(s.Battle.NavalExpansion.PrechargeBonus, Is.EqualTo(2));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(1000), "A rejected attack cannot trigger overload.");
            Assert.That(s.Hull, Is.EqualTo(health));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy));
            Assert.That(s.Battle.CardsPlayedThisTurn, Is.EqualTo(played));
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(cells));
            CollectionAssert.AreEqual(held, s.Battle.Hand);
            CollectionAssert.AreEqual(discarded, s.Battle.DiscardPile);
        }

        [TestCase("card_school_thunder_b06")]
        [TestCase("card_school_thunder_n06")]
        [TestCase("card_school_thunder_n09")]
        public void EachFinisherClearsEvenTheChainAddedByItsAttackAndPrechargeOutsideAField(string id)
        {
            var s = Start(id, 0);
            if (id == "card_school_thunder_n06")
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 2, 0, false, 0, 0), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 5);
            s.Battle.NavalExpansion.Precharge = true;
            s.Battle.NavalExpansion.PrechargeBonus = 2;
            Play(s, id, id == "card_school_thunder_n06"
                ? new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 2, AnchorY = 0 } : null);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.Battle.NavalExpansion.Precharge, Is.False);
            Assert.That(s.Battle.NavalExpansion.PrechargeBonus, Is.Zero);
        }

        [TestCase("card_school_thunder_b06")]
        [TestCase("card_school_thunder_n09")]
        public void FieldProtectedFinisherKeepsItsChainAndStillSuffersEachOverload(string id)
        {
            var s = Start(id, 0);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 49);
            s.Battle.NavalExpansion.Precharge = true;
            s.Battle.NavalExpansion.PrechargeBonus = 2;
            s.Battle.PlayerBlock = 2000;
            int health = s.Hull;
            Play(s, id);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(52));
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "The ordinary attack and the extra precharge each trigger an overload.");
            Assert.That(s.Hull, Is.EqualTo(health));
        }

        [Test]
        public void CollapseRetainsChainUsingItsStartingCellEvenWhenThatCellIsRemoved()
        {
            const string id = "card_school_thunder_n06";
            var s = Start(id, 0);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 3);
            s.Battle.NavalExpansion.Precharge = true;
            s.Battle.NavalExpansion.PrechargeBonus = 2;
            Play(s, id, new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 0, AnchorY = 0 });
            Assert.That(s.GetNavalFieldAt(0, 0), Is.Null);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(6), "The starting field grants this play's spending exemption.");
        }

        [TestCase("card_school_thunder_b06")]
        [TestCase("card_school_thunder_n06")]
        [TestCase("card_school_thunder_n09")]
        public void FinisherClearsAfterALethalEarlyReturnAndInvalidPlayNeverClears(string id)
        {
            var s = Start(id, 0);
            Set(s.Battle.Schools, "ThunderChain", 10);
            Hand(s, id);
            s.Battle.Energy = 0;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(10));
            s.Battle.Energy = 3;
            if (id == "card_school_thunder_n06")
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 2, 0, false, 0, 0), Is.True);
            s.Battle.EnemyHull = 3;
            s.ApplyNavalEnemyCurseMarks(1);
            Play(s, id, id == "card_school_thunder_n06"
                ? new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 2, AnchorY = 0 } : null);
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void RelayStillTriggersEveryOverloadWhileCircuitDoesNotInventAnAttack()
        {
            var s = Start(CoreNavalThunderPower.RelayId, 0);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            Set(s.Battle.Schools, "ThunderChain", 49);
            s.Battle.PlayerBlock = 1000;
            int health = s.Hull;
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(50));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Hull, Is.EqualTo(health));
            Play(s, CoreNavalThunderPower.CircuitId);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(50));
            Assert.That(s.Hull, Is.EqualTo(health));
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(51));
            Assert.That(s.Hull, Is.EqualTo(health - 1000));
        }

        [TestCase(0, 2)] [TestCase(1, 3)]
        public void GroundingDrawGrowthDoesNotDuplicateTheOncePerTurnEnergyReturn(int rank, int draw)
        {
            const string id = "card_school_thunder_n05";
            var s = Start(id, rank);
            Play(s, id);
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(draw));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Play(s, id);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Play(s, CoreNavalThunderPower.RelayId);
            Assert.That(s.Battle.Energy, Is.Zero, "The second hit cannot earn a second Grounding return.");
        }

        private static CoreVoyageSession Start(string id, int rank)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            s.ConfigureComponentBonuses(2000, 0, 49, 0, 0);
            s.SetSeed("THUNDER-POWER-20261024");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.PlayerLoadedShot = 0;
            s.Battle.Energy = 3;
            s.Cargo.Powder = s.Cargo.Cannonballs = 20;
            s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            for (int i = 0; i < 30; i++) s.Battle.DrawPile.Add(Index("card_brace"));
            Set(s, "Morale", 0);
            return s;
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Hand(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id));
        }
        private static void Play(CoreVoyageSession s, string id, CoreNavalCardPlayOptions options = null)
        {
            Hand(s, id);
            bool played = CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? s.PlayCard(0) : s.PlayNavalCard(0, options);
            Assert.That(played, Is.True, id + ": " + s.CardUnavailableReason(0));
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
