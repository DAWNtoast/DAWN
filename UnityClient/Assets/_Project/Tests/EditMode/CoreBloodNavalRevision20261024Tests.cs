using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBloodNavalRevision20261024Tests
    {
        [TestCase(20, 17, 1)] [TestCase(4, 1, 1)] [TestCase(3, 3, 0)] [TestCase(1, 1, 0)]
        public void PreparationBloodDrawAlwaysGuardsEightAndOnlyDrawsWhenThreeHullCanBePaid(int health, int remaining, int draws)
        {
            CoreVoyageSession s = Start(false); SetHull(s, health);
            s.Battle.DrawPile.Add(Index("card_brace"));
            Play(s, "card_school_blood_b02");
            Assert.That(s.Hull, Is.EqualTo(remaining));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(8));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(draws));
        }

        [TestCase("card_school_blood_b01", false, 20, 15, 13)]
        [TestCase("card_school_blood_b01", false, 6, 1, 13)]
        [TestCase("card_school_blood_b01", false, 5, 5, 5)]
        [TestCase("card_school_blood_b01", false, 1, 1, 5)]
        [TestCase("card_school_blood_b05", true, 20, 15, 13)]
        [TestCase("card_school_blood_b05", true, 6, 1, 13)]
        [TestCase("card_school_blood_b05", true, 5, 5, 5)]
        [TestCase("card_school_blood_b05", true, 1, 1, 5)]
        public void BloodGuardPaysAllFiveOrFallsBackToItsFiveBaseGuard(string id, bool naval, int health, int remaining, int guard)
        {
            CoreVoyageSession s = Start(naval); SetHull(s, health);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Assert.That(s.GetBattleCardEffects(card, s.Battle.Stage).Block, Is.EqualTo(guard));
            Assert.That(s.GetBattleCardHealthCost(card), Is.EqualTo(health - remaining));
            Play(s, id);
            Assert.That(s.Hull, Is.EqualTo(remaining));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard));
        }

        [TestCase(20, 15, 14)] [TestCase(6, 1, 14)] [TestCase(5, 5, 0)] [TestCase(1, 1, 0)]
        public void BloodCannonPaysAllFiveForFourteenExtraDamageOrUsesOnlyBaseDamage(int health, int remaining, int bonus)
        {
            CoreVoyageSession s = Start(); SetHull(s, health);
            int expected = CoreNavalStatRules.ScaleCannonDamage(10, s.EffectiveFirepower) + bonus;
            int enemy = s.Battle.EnemyHull;
            Play(s, "card_school_blood_b03");
            Assert.That(s.Hull, Is.EqualTo(remaining));
            Assert.That(enemy - s.Battle.EnemyHull, Is.EqualTo(expected));
            Assert.That(s.Cargo.Powder, Is.EqualTo(99));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(99));
        }

        [TestCase(true)] [TestCase(false)]
        public void DesperateCannonChecksFortyPercentBeforePaymentAndOnlyPaysWhileDesperate(bool desperate)
        {
            CoreVoyageSession s = Start();
            int health = s.MaxHull * 40 / 100 + (desperate ? 0 : 1);
            Assert.That(health, Is.GreaterThan(5)); SetHull(s, health);
            CoreBattleCard card = s.GetBattleCard(Index("card_school_blood_n03"));
            Assert.That(s.GetBattleCardHealthCost(card), Is.EqualTo(desperate ? 5 : 0));
            int enemy = s.Battle.EnemyHull;
            Play(s, card.Id);
            Assert.That(s.Hull, Is.EqualTo(health - (desperate ? 5 : 0)));
            Assert.That(enemy - s.Battle.EnemyHull, Is.EqualTo(CoreNavalStatRules.ScaleCannonDamage(35, s.EffectiveFirepower) + (desperate ? 16 : 0)));
        }

        [TestCase(1)] [TestCase(5)] [TestCase(6)]
        public void DesperateCannonNeverPartiallyPaysWhenTheFiveHullCostCannotLeaveOne(int health)
        {
            CoreVoyageSession s = Start(); SetHull(s, health);
            int enemy = s.Battle.EnemyHull;
            Play(s, "card_school_blood_n03");
            Assert.That(s.Hull, Is.EqualTo(health > 5 ? health - 5 : health));
            Assert.That(enemy - s.Battle.EnemyHull, Is.EqualTo(CoreNavalStatRules.ScaleCannonDamage(35, s.EffectiveFirepower) + (health > 5 ? 16 : 0)));
        }

        [TestCase(100, 0, 5)] [TestCase(100, 2, 3)] [TestCase(100, 5, 0)]
        [TestCase(100, 50, 0)] [TestCase(2, 0, 2)] [TestCase(2, 1, 2)]
        public void LifestealRestoresOnlyActualHullDamageIncludingTheLethalHit(int enemyHealth, int block, int healing)
        {
            CoreVoyageSession s = Start(); SetHull(s, 20);
            s.Battle.EnemyHull = enemyHealth; s.Battle.EnemyBlock = block;
            Play(s, "card_school_blood_b04");
            Assert.That(s.Hull, Is.EqualTo(20 + healing), "Blocked damage and excess overkill must not heal.");
        }

        [Test]
        public void HealingBanSuppressesLifestealAndRepairQuotesWithoutSpendingWood()
        {
            CoreVoyageSession s = Start(); SetHull(s, 20);
            s.ApplyBattleHealingBan(true, 2);
            Play(s, "card_school_blood_b04");
            Assert.That(s.Hull, Is.EqualTo(20));
            CoreBattleCard repair = CoreVoyageSession.CardLibrary.First(c => c.Repair > 0 && c.IsAllowedInStage(NavalBattleStage.Naval) &&
                c.SchoolId != "blood" && c.SchoolEffect == null);
            CoreBattleCardEffects quote = s.GetBattleCardEffects(repair, NavalBattleStage.Naval);
            Assert.That(quote.ActualRepair, Is.Zero);
            Assert.That(quote.WoodCost, Is.Zero);
            int wood = s.Cargo.Wood;
            Play(s, repair.Id);
            Assert.That(s.Hull, Is.EqualTo(20));
            Assert.That(s.Cargo.Wood, Is.EqualTo(wood));
        }

        [Test]
        public void EnemyHealingBanLastsExactlyTwoTargetTurnEndsAndIntentQuotesActualRepair()
        {
            CoreVoyageSession s = Start();
            s.Battle.EnemyHull = 100;
            Play(s, "card_school_blood_n02");
            Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(2));
            Assert.That(s.GetEnemyIntent(), Does.Contain("回复 0"));
            for (int remaining = 1; remaining >= 0; remaining--)
            {
                s.Battle.PlayerBlock = 1000;
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.EnemyHull, Is.EqualTo(100));
                Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(remaining));
            }
            Assert.That(s.GetEnemyIntent(), Does.Contain("回复 2"));
            s.Battle.PlayerBlock = 1000;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(102));
        }

        [Test]
        public void ReapplyingBanRefreshesTwoTargetTurnsWithoutAddingThemAndOwnTurnEndDoesNotReduceIt()
        {
            CoreVoyageSession s = Start();
            Play(s, "card_school_blood_n02");
            s.NavalExpansionEndPlayerTurn();
            Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(2));
            s.NavalExpansionEndEnemyTurn();
            Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(1));
            Play(s, "card_school_blood_n02");
            Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(2));
        }

        [TestCase(true)] [TestCase(false)]
        public void InvalidBanPlayNeverPaysHullOrChangesCounters(bool outOfRange)
        {
            CoreVoyageSession s = Start(); SetHull(s, outOfRange ? 20 : 3);
            if (outOfRange) Pose(s, false, 5, 0);
            Put(s, "card_school_blood_n02");
            int health = s.Hull, energy = s.Battle.Energy, played = s.Battle.CardsPlayedThisTurn;
            Assert.That(s.PlayNavalCard(0, null), Is.False);
            Assert.That(s.Hull, Is.EqualTo(health)); Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.CardsPlayedThisTurn, Is.EqualTo(played));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.Zero);
        }

        [TestCase("card_school_blood_b03", 5, 0)]
        [TestCase("card_school_blood_b05", 5, 13)]
        [TestCase("card_school_blood_n02", 3, 0)]
        public void ActualBloodCostsGrantDoublePoolGuardInAdditionToAuthoredCardGuard(string id, int cost, int cardGuard)
        {
            CoreVoyageSession s = Start(); SetHull(s, 20); Pool(s, false);
            Play(s, id);
            Assert.That(s.Hull, Is.EqualTo(20 - cost));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(cardGuard + 2 * cost), "Only the field rebate doubles; authored card guard remains unchanged.");
        }

        [TestCase(true)] [TestCase(false)]
        public void BloodFieldCostOnlyUsesThePoolPresentBeforePlacement(bool existingPool)
        {
            CoreVoyageSession s = Start(); SetHull(s, 20);
            if (existingPool) Pool(s, false);
            Play(s, "card_school_blood_n01", Anchor(0, 0));
            Assert.That(s.Hull, Is.EqualTo(16));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(existingPool ? 8 : 0), "The four-point cost rebates only through a pool already present when it is paid.");
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            Assert.That(s.GetNavalFieldAt(0, 0).PlayerOwned, Is.True);
        }

        [Test]
        public void BloodPoolNeverRewardsZeroCostsFallbackOrWaivedCosts()
        {
            CoreVoyageSession s = Start(); SetHull(s, 20); Pool(s, true);
            Play(s, "card_school_blood_b04");
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            SetHull(s, 5);
            Play(s, "card_school_blood_b05");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(5));
            Assert.That(s.Hull, Is.EqualTo(5));
            s.Battle.PlayerBlock = 0;
            typeof(CoreSpecialCardBattleState).GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle.SpecialCards, new object[] { "blood" });
            typeof(CoreSpecialCardBattleState).GetMethod("BeginTurn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle.SpecialCards, null);
            Play(s, "card_school_blood_b05");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(13));
            Assert.That(s.Hull, Is.EqualTo(5));
        }

        [Test]
        public void AttackAndBurnLossesNeverTriggerBloodPoolGuard()
        {
            CoreVoyageSession s = Start(); SetHull(s, 30); Pool(s, true);
            typeof(CoreVoyageSession).GetMethod("NavalExpansionDirectDamage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { true, 7, 0 });
            Assert.That(s.Hull, Is.EqualTo(23)); Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Pose(s, false, 6, 0);
            s.Battle.PlayerBurnStacks = 4;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(14), "Four burn damage and five passive blood-pool loss follow the earlier seven-point hit.");
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "Neither attack damage, burn nor passive field loss can generate pool guard.");
        }

        [Test]
        public void DirtyLegacyBloodAttackChargesAffectNeitherQuotesNorActualAttacks()
        {
            CoreVoyageSession s = Start();
            CoreBattleCard attack = s.GetBattleCard(Index("card_school_blood_b04"));
            int quoted = s.GetBattleCardDamage(attack, NavalBattleStage.Naval);
            string enemyIntent = s.GetEnemyIntent();
            s.Battle.NavalFields.Player.BloodAttackBonus = s.Battle.NavalFields.Enemy.BloodAttackBonus = 4;
            Assert.That(s.GetBattleCardDamage(attack, NavalBattleStage.Naval), Is.EqualTo(quoted));
            Assert.That(s.GetEnemyIntent(), Is.EqualTo(enemyIntent));
            int enemy = s.Battle.EnemyHull;
            Play(s, attack.Id);
            Assert.That(enemy - s.Battle.EnemyHull, Is.EqualTo(quoted));
        }

        [Test]
        public void DirtyEnemyBloodAttackChargeCannotStrengthenItsResolvedCannon()
        {
            CoreVoyageSession baseline = Start();
            int baselineHull = baseline.Hull;
            Assert.That(baseline.EndBattleTurn(), Is.True);
            int damage = baselineHull - baseline.Hull;
            CoreVoyageSession s = Start();
            s.Battle.NavalFields.Enemy.BloodAttackBonus = 4;
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(damage));
        }

        [Test]
        public void EnemyBloodPoolDoesNotRewardHullDamageAndLegacyHealingReductionIsRetired()
        {
            CoreVoyageSession s = Start();
            s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell { X = 2, Y = 0, Kind = CoreNavalFieldKind.Blood,
                PlayerOwned = false, InstanceId = s.Battle.NavalFields.NextInstanceId++, ExpiresAtOwnerTurn = 5 });
            s.Battle.EnemyHull = 100;
            Play(s, "card_school_blood_b04");
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            s.Battle.NavalExpansion.EnemyHealingHalved = true;
            Assert.That(s.NavalExpansionHealing(false, 8), Is.EqualTo(8));
            s.ApplyBattleHealingBan(false, 2);
            Assert.That(s.NavalExpansionHealing(false, 8), Is.Zero);
        }

        private static CoreVoyageSession Start(bool naval = true)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("BLOOD-NAVAL-REVISION"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[1]), Is.True);
            if (naval) Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            SetHull(s, s.MaxHull);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            return s;
        }
        private static void Pool(CoreVoyageSession s, bool owned) => s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell
        { X = 0, Y = 0, Kind = CoreNavalFieldKind.Blood, PlayerOwned = owned, InstanceId = s.Battle.NavalFields.NextInstanceId++,
            ExpiresAtOwnerTurn = 5 + (owned ? s.Battle.NavalFields.CompletedEnemyTurns : s.Battle.NavalFields.CompletedPlayerTurns) });
        private static CoreNavalCardPlayOptions Anchor(int x, int y) => new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = x, AnchorY = y };
        private static void SetHull(CoreVoyageSession s, int hull) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, hull);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id, CoreNavalCardPlayOptions options = null)
        {
            Put(s, id);
            Assert.That(CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? s.PlayCard(0) : s.PlayNavalCard(0, options),
                Is.True, id + ": " + s.CardUnavailableReason(0));
        }
    }
}
