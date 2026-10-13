using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSchoolRebalanceRulesTests
    {
        [Test]
        public void ManaStartsEmptyAtThree_AndCapacityDoesNotFillIt()
        {
            CoreVoyageSession s = Battle(preparation: true);
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(3));
            Play(s, "card_school_arcane_b02");
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            Play(s, "card_school_arcane_b01");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
        }

        [Test]
        public void TemporaryManaIsPaidFirst_AndUnusedTemporaryManaExpires()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_school_arcane_b06");
            Play(s, Art("card_school_arcane_e02"));
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(5));
            Play(s, "card_school_arcane_b04");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.TemporaryMana, Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            // Ordinary mana's next-turn policy is deliberately not asserted here.
        }

        [TestCase("card_school_arcane_b04")]
        [TestCase("card_school_curse_b08")]
        [TestCase("card_school_curse_e04")]
        [TestCase("card_school_tide_b07")]
        [TestCase("card_school_tide_b09")]
        [TestCase("card_school_tide_e05")]
        [TestCase("card_school_blood_b07")]
        [TestCase("card_school_fire_b03")]
        public void InsufficientRequiredResourceRejectsBeforeAnyPayment(string artId)
        {
            string id = artId.Contains("_e") ? Art(artId) : artId;
            CoreBattleCard definition = CoreSchoolCardCatalog.Find(id);
            CoreVoyageSession s = Battle(definition.IsAllowedInStage(NavalBattleStage.Boarding));
            CoreBattleCard card = s.GetBattleCard(Index(id));
            s.Cargo.Powder = card.PowderCost > 0 ? card.PowderCost - 1 : 10;
            if (card.SchoolId == "blood") s.Battle.PlayerBoardingHealth = card.SchoolEffect.Threshold;
            Put(s, id); s.Battle.Energy = 20;
            int hull = s.Hull, health = s.Battle.PlayerBoardingHealth, enemy = s.Battle.EnemyHull;
            int powder = s.Cargo.Powder, balls = s.Cargo.Cannonballs, wood = s.Cargo.Wood, medicine = s.Cargo.Medicine;
            Assert.That(s.CardUnavailableReason(0), Is.Not.Empty);
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(20));
            Assert.That(s.Battle.Hand, Has.Count.EqualTo(1));
            Assert.That(new[] { s.Hull, s.Battle.PlayerBoardingHealth, s.Battle.EnemyHull, s.Cargo.Powder,
                s.Cargo.Cannonballs, s.Cargo.Wood, s.Cargo.Medicine },
                Is.EqualTo(new[] { hull, health, enemy, powder, balls, wood, medicine }));
        }

        [Test]
        public void CurseMarkAndDetonationWaitForEnemyEnd_AndIgnoreEnemyBlock()
        {
            CoreVoyageSession s = Battle();
            int before = s.Battle.EnemyHull;
            Play(s, "card_school_curse_b03");
            Play(s, "card_school_curse_b05");
            int marks = s.Battle.Schools.CurseMarks;
            int normal = marks * s.Battle.Schools.CurseDamagePerMark;
            int enhanced = normal + marks * s.Battle.Schools.CurseNextTickBonus;
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before));
            s.Battle.EnemyBlock = 9999;
            s.Battle.PlayerBlock = 9999;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - enhanced));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(marks));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
            s.Battle.PlayerBlock = 9999;
            Assert.That(s.EndBattleTurn(), Is.True);
            int afterRepair = Math.Min(s.Battle.EnemyMaxHull, before - enhanced + s.Battle.Profile.TrainingTarget.Repair);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(afterRepair - normal));
        }

        [Test]
        public void StrongCurseWardUsesPrePaymentLayers_HasNoAuthoredCap_AndExpires()
        {
            CoreVoyageSession s = Battle(true);
            Set(s, "CurseMarks", 1000);
            CoreBattleCard ward = s.GetBattleCard(Index(Art("card_school_curse_e04")));
            int quoted = 1000 * ward.SchoolEffect.Power;
            Assert.That(s.GetBattleCardEffects(ward, NavalBattleStage.Boarding).Block, Is.EqualTo(quoted));
            Play(s, ward.Id);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1000 - ward.SchoolEffect.Threshold));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(quoted));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "Unspent curse guard cannot protect a later turn.");
            Assert.That(s.Battle.Schools.CurseGuard, Is.Zero);
        }

        [Test]
        public void PreparationCurseGuardDoesNotCarryIntoNavalCombat()
        {
            CoreVoyageSession s = Battle(preparation: true);
            // No authored preparation ward is needed: this is the guard-lifetime boundary.
            s.Battle.PlayerBlock = 40; Set(s, "CurseGuard", 40);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.CurseGuard, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryGuardAndUnpaidTideGuardNeverGenerateTide(bool unpaidTide)
        {
            CoreVoyageSession s = Battle(true); s.Cargo.Wood = 0;
            Play(s, unpaidTide ? "card_school_tide_b08" : "card_crew_guard");
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.GreaterThan(0));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [TestCase(0, 7)]
        [TestCase(3, 14)]
        public void PaidTideWardNeedsActualAbsorption_ThenRiposteSpendsEntireReserve(int rank, int paidGuard)
        {
            CoreVoyageSession s = Battle(upgradedCard: "card_school_tide_b04", rank: rank);
            int wood = s.Cargo.Wood;
            Play(s, "card_school_tide_b04");
            Assert.That(s.Cargo.Wood, Is.EqualTo(wood - 1));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(paidGuard));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.EqualTo(paidGuard));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            int tide = s.Battle.Schools.TidePower;
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(paidGuard));
            Assert.That(tide, Is.EqualTo(paidGuard));
            CoreBattleCard attack = s.GetBattleCard(Index("card_school_tide_b06"));
            Assert.That(s.GetBattleCardEffects(attack, NavalBattleStage.Naval).Damage,
                Is.EqualTo(attack.Damage + tide * attack.SchoolEffect.Power / 100));
            Play(s, attack.Id);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Put(s, attack.Id); s.Battle.Energy = 20;
            Assert.That(s.CanPlayCard(0), Is.False);
        }

        [TestCase("card_school_tide_b05", 10, "靠近 10 米")]
        [TestCase("card_school_tide_e02", -10, "远离 10 米")]
        public void UnpaidTideMovementMatchesQuoteAndFace_WithoutSpendingWoodOrBuildingTide(
            string artId, int movement, string movementText)
        {
            CoreVoyageSession s = Battle();
            string id = artId.Contains("_e") ? Art(artId) : artId;
            s.Cargo.Wood = 0; s.Battle.DistanceMeters = 300;
            CoreBattleCard adjusted = s.GetSupplyAdjustedBattleCard(Index(id));
            Assert.That(adjusted.MovementMeters, Is.EqualTo(Math.Abs(movement)));
            Assert.That(adjusted.SchoolEffect.Movement, Is.EqualTo(movement));
            Assert.That(s.GetBattleCardEffects(adjusted, NavalBattleStage.Naval).Block, Is.EqualTo(2));
            Assert.That(s.BattleCardFaceDescription(s.GetBattleCard(Index(id))), Does.Contain(movementText));
            Assert.That(s.GetBattleCardWoodCost(Index(id)), Is.Zero);
            Play(s, id);
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(300 - movement));
            Assert.That(s.Cargo.Wood, Is.Zero);
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero, "Fallback guard does not become paid guard after moving.");
        }

        [Test]
        public void OrdinaryGuardAbsorbsFirst_OnlyPaidShareGeneratesTide()
        {
            CoreVoyageSession s = Battle(true);
            s.Battle.PlayerBlock = 40;
            Play(s, "card_school_tide_b08");
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(44));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(4), "40 ordinary + 4 paid guard absorbed.");
        }

        [Test]
        public void TideHasALimit_AndUnusedReserveExpiresAtTheNextEndTurn()
        {
            CoreVoyageSession s = Battle(true);
            Play(s, "card_school_tide_b08"); Play(s, "card_school_tide_b08");
            Play(s, Art("card_school_tide_e04"));
            Play(s, "card_school_tide_b08"); Play(s, "card_school_tide_b08"); // Four five-point wards plus the four-point source ward exceed the unchanged twenty-point reserve cap.
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.GreaterThan(s.Battle.Schools.TideLimit));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(s.Battle.Schools.TideLimit));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [Test]
        public void FireFallbackNeverIgnitesExtendsOrTriggersScorch_AndDoesNotSpendPowder()
        {
            CoreVoyageSession s = Battle(true); s.Cargo.Powder = 0;
            s.Battle.EnemyBurnDamage = 3; s.Battle.EnemyBurnTurns = 2;
            Set(s, "ScorchDamage", 30); Set(s, "ScorchCharges", 2);
            Play(s, "card_school_fire_b10");
            Assert.That(s.Cargo.Powder, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.ScorchCharges, Is.EqualTo(2));
            s.Battle.EnemyBurnDamage = 0; s.Battle.EnemyBurnTurns = 0;
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
            for (int rank = 0; rank <= 3; rank++)
            {
                CoreBattleCard c = s.GetBattleCardAtLevel(Index("card_school_fire_b07"), rank);
                Assert.That(s.GetBattleCardEffects(c, NavalBattleStage.Boarding).Damage, Is.EqualTo(5));
            }
        }

        [Test]
        public void ThunderIgnoresOrdinaryAttacksDrawAndEnergyCards_AndReadyDoesNotBuildChain()
        {
            CoreVoyageSession s = Battle(true);
            Play(s, "card_crew_thrust"); Play(s, "card_tactical_draw"); Play(s, "card_energy_order");
            Play(s, "card_school_thunder_b08");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.GreaterThan(0));
            Play(s, "card_school_thunder_b07");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.Zero);
        }

        [TestCase(0, 1)]
        [TestCase(3, 2)]
        public void StormOracleDrawFitsTheNormalThreeActionWindow(int rank, int expectedDraw)
        {
            const string echoId = "card_special_instrument_stormoracle";
            CoreVoyageSession s = ResourceWindowSession("instrument_stormoracle", rank);
            SetTwoCopyDeck(s, NavalBattleStage.Boarding, "card_school_thunder_b07", "card_school_thunder_b09",
                "card_crew_guard", "card_tactical_draw", "card_energy_order");
            StartResourceWindowBattle(s);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.DistanceMeters = 60; s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            ArrangeResourceWindowHand(s, "card_school_thunder_b07", "card_school_thunder_b09", echoId);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.GetBattleCard(Index(echoId)).UpgradeLevel, Is.EqualTo(rank));

            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
            int available = s.Battle.DrawPile.Count;
            Assert.That(s.Battle.Hand, Has.Count.EqualTo(1));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Energy, Is.EqualTo(1), "The conditional zero-action echo preserves the remaining action.");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2), "The draw does not advance or consume the chain.");
            Assert.That(s.Battle.Schools.ThunderEchoUsed, Is.True);
            Assert.That(s.Battle.Hand, Has.Count.EqualTo(expectedDraw));
            Assert.That(s.Battle.DrawPile, Has.Count.EqualTo(available - expectedDraw));
        }

        [Test]
        public void CapacityAndBankedManaMakeWorldsEndVolleyPayableOnANormalTurn()
        {
            const string attackId = "card_special_cannon_worlds_end_volley";
            CoreVoyageSession s = ResourceWindowSession("cannon_worlds_end_volley", 0);
            SetTwoCopyDeck(s, NavalBattleStage.Preparation, "card_school_arcane_b01", "card_school_arcane_b02",
                "card_brace", "card_tactical_draw", "card_energy_order");
            SetTwoCopyDeck(s, NavalBattleStage.Naval, "card_school_arcane_b03", "card_school_arcane_b06",
                "card_brace", "card_tactical_draw", "card_energy_order");
            StartResourceWindowBattle(s);
            ArrangeResourceWindowHand(s, "card_school_arcane_b02", "card_school_arcane_b01");
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(2));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(2), "Preparation mana carries into naval combat.");

            ArrangeResourceWindowHand(s, "card_school_arcane_b06");
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(4));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(4), "Ordinary mana remains banked across turns.");
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            ArrangeResourceWindowHand(s, attackId);
            int powder = s.Cargo.Powder, balls = s.Cargo.Cannonballs, enemy = s.Battle.EnemyHull;
            Assert.That(s.CanPlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            Assert.That(s.Cargo.Powder, Is.EqualTo(powder - 1));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(balls - 1));
            Assert.That(s.Battle.EnemyHull, Is.LessThan(enemy));
        }

        [Test]
        public void ThunderGrowthIsLinearAndStopsAtSix_WithoutGrantingGuard()
        {
            CoreVoyageSession s = Battle(true);
            CoreBattleCard c = s.GetBattleCard(Index("card_school_thunder_b07"));
            for (int i = 0; i < 8; i++)
            {
                Assert.That(s.GetBattleCardEffects(c, NavalBattleStage.Boarding).Damage,
                    Is.EqualTo(c.Damage + c.SchoolEffect.Power * Math.Min(i, 6)));
                Play(s, c.Id);
                Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(Math.Min(i + 1, 6)));
                Assert.That(s.Battle.PlayerBlock, Is.Zero);
            }
        }

        [Test]
        public void EnergyRecoveryDoesNotAvoidSameIdEscalatingCosts()
        {
            CoreVoyageSession s = Battle(true);
            int index = Index("card_energy_order");
            Assert.That(s.GetBattleCard(index).Block, Is.Zero);
            s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(new[] { index, index, index });
            s.Battle.Energy = 0;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.GetBattleCardEnergyCost(index), Is.EqualTo(1));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.GetBattleCardEnergyCost(index), Is.EqualTo(2));
            Assert.That(s.PlayCard(0), Is.False);
        }

        [Test]
        public void BloodPaymentIgnoresGuardAndLeavesOne_WhileCostsDoNotUpgrade()
        {
            CoreVoyageSession s = Battle(true);
            CoreBattleCard c = s.GetBattleCard(Index("card_school_blood_b07"));
            int payment = c.SchoolEffect.Threshold;
            Assert.That(payment, Is.EqualTo(3));
            s.Battle.PlayerBoardingHealth = payment + 1; s.Battle.PlayerBlock = 999;
            Play(s, c.Id);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(999));
            for (int rank = 0; rank <= 3; rank++)
                Assert.That(s.GetBattleCardAtLevel(Index(c.Id), rank).SchoolEffect.Threshold, Is.EqualTo(payment));
        }

        private static CoreVoyageSession ResourceWindowSession(string component, int rank)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.ConfigureSpecialCardSources(Array.Empty<string>(), new[] { component });
            s.ConfigureCardUpgrades(new Dictionary<string, int>
            {
                { "card_school_thunder_b07", rank }, { "card_school_thunder_b09", rank }
            });
            s.RestoreSpecialCardExperience(new Dictionary<string, int> { { "card_special_" + component, rank == 3 ? 840 : 0 } });
            s.SetSeed("SCHOOL-RESOURCE-WINDOW");
            return s;
        }

        private static void SetTwoCopyDeck(CoreVoyageSession s, NavalBattleStage stage, params string[] ids)
        {
            Assert.That(s.SetBattleDeck(stage, ids.SelectMany(id => new[] { Index(id), Index(id) }).ToArray()), Is.True);
        }

        private static void StartResourceWindowBattle(CoreVoyageSession s)
        {
            s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
        }

        // Arrange only the legal deck's draw order; keep its card multiset and all combat resources intact.
        private static void ArrangeResourceWindowHand(CoreVoyageSession s, params string[] ids)
        {
            var remaining = s.Battle.Hand.Concat(s.Battle.DrawPile).Concat(s.Battle.DiscardPile).ToList();
            int[] hand = ids.Select(Index).ToArray();
            foreach (int index in hand) Assert.That(remaining.Remove(index), Is.True, "Card must already be in this stage's legal deck.");
            s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(hand);
            s.Battle.DrawPile.Clear(); s.Battle.DrawPile.AddRange(remaining);
            s.Battle.DiscardPile.Clear();
        }

        private static CoreVoyageSession Battle(bool boarding = false, bool preparation = false, string upgradedCard = null, int rank = 0)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            if (upgradedCard != null) s.ConfigureCardUpgrades(new Dictionary<string, int> { { upgradedCard, rank } });
            s.ConfigureSpecialCardSources(CoreSchoolCardCatalog.All.Select(c => c.RequiredCrew).Where(v => v.Length > 0).ToArray(),
                CoreSchoolCardCatalog.All.Select(c => c.RequiredComponent).Where(v => v.Length > 0).ToArray());
            s.SetSeed("SCHOOL-RESOURCE-REWORK");
            s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 1000);
            if (!preparation) Assert.That(s.EndBattleTurn(), Is.True);
            if (boarding)
            {
                s.Battle.DistanceMeters = 60; s.Battle.Alignment = NavalBattleAlignment.Parallel;
                Assert.That(s.TryBoard(), Is.True);
                typeof(CoreBattleState).GetProperty("PlayerOrdinaryCrew").SetValue(s.Battle, 28);
                s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            }
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            s.Battle.EnemyBlock = 0; s.Battle.PlayerBlock = 0;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100; s.Cargo.Medicine = 0;
            return s;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static string Art(string art) => CoreSchoolCardCatalog.All.Single(c => c.ArtId == art).Id;
        private static void Set(CoreVoyageSession s, string name, int value) => typeof(CoreSchoolBattleState).GetProperty(name).SetValue(s.Battle.Schools, value);
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id)
        {
            Put(s, id); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
        }
    }
}
