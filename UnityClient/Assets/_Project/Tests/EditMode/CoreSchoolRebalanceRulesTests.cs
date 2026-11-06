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
        public void ManaStartsAtThree_AndCapacityDoesNotImmediatelyFillTheIncrease()
        {
            CoreVoyageSession s = Battle(preparation: true);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(3));
            Play(s, "card_school_arcane_b02");
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(3));
            Play(s, "card_school_arcane_b01");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
        }

        [Test]
        public void SingleManaPoolPaysCostsAndRefillsWithoutRetainingExcess()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_school_arcane_b06");
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(5));
            Play(s, "card_school_arcane_b04");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Play(s, "card_school_arcane_b06");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(3));
        }

        [TestCase("card_school_arcane_b04")]
        [TestCase("card_boarding_curse_15")]
        [TestCase("card_boarding_tide_09")]
        [TestCase("card_school_blood_b07")]
        [TestCase("card_school_fire_b03")]
        public void InsufficientRequiredResourceRejectsBeforeAnyPayment(string artId)
        {
            string id = artId.Contains("_e") ? Art(artId) : artId;
            CoreBattleCard definition = CoreVoyageSession.CardLibrary[Index(id)];
            CoreVoyageSession s = Battle(definition.IsAllowedInStage(NavalBattleStage.Boarding));
            CoreBattleCard card = s.GetBattleCard(Index(id));
            if (card.SchoolId == "arcane") Set(s, "Mana", 0);
            s.Cargo.Powder = card.PowderCost > 0 ? card.PowderCost - 1 : 10;
            if (card.SchoolId == "blood") s.Battle.PlayerBoardingHealth = 3;
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
        public void CurseMarkDamageImmediatelyBypassesGuardAndMarksDoNotTick()
        {
            CoreVoyageSession s = Battle();
            int before = s.Battle.EnemyHull;
            Play(s, "card_school_curse_b03");
            s.Battle.EnemyBlock = 9999;
            Play(s, "card_school_curse_b05");
            int marks = s.Battle.Schools.CurseMarks;
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - marks));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(9999));
            s.Battle.PlayerBlock = 9999;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before), "The training target repairs the hit; marks add no end-turn damage.");
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(marks));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
            s.Battle.PlayerBlock = 9999;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before));
        }

        [Test]
        public void CurseWardAlias_RequiresFourMarksWithoutSpendingThem_ForTwentyGuardThatExpires()
        {
            CoreVoyageSession s = Battle(true);
            Play(s, "card_boarding_curse_02"); Play(s, "card_boarding_curse_02"); Play(s, "card_boarding_curse_02");
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(18));
            CoreBattleCard ward = s.GetBattleCard(Index("card_boarding_curse_15"));
            Assert.That(ward.BoardingSpec.Key, Is.EqualTo("curse_15"));
            Play(s, ward.Id);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(18));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(20));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.CurseGuard, Is.Zero, "Legacy extra guard must not also be applied.");
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

        [TestCase(0)]
        [TestCase(3)]
        public void GhostSailAddsOrdinaryGuardWithoutCurseShieldOrTideConversion(int rank)
        {
            const string id = "card_school_curse_b06";
            CoreVoyageSession s = Battle(upgradedCard: id, rank: rank);
            Set(s, "CurseMarks", 5);
            s.Battle.PlayerBlock = 7;
            int quoted = s.GetBattleCardEffects(s.GetBattleCard(Index(id)), NavalBattleStage.Naval).Block;
            Play(s, id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(7 + quoted));
            Assert.That(s.Battle.Schools.CurseGuard, Is.Zero, "Ghost Sail's complete gain belongs to ordinary guard.");
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(5));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "Ordinary guard expires on the next player turn.");
            Assert.That(s.Battle.Schools.TidePower, Is.Zero, "Absorbing damage with ordinary guard does not generate tide.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OnlyTideGuardGeneratesMomentum_WithoutAnyWoodPayment(bool unpaidTide)
        {
            CoreVoyageSession s = Battle(true); s.Cargo.Wood = 0;
            Play(s, unpaidTide ? "card_school_tide_b08" : "card_crew_guard");
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.GreaterThan(0));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(unpaidTide ? 10 : 0));
            Assert.That(s.Cargo.Wood, Is.Zero);
        }

        [TestCase(0, 9)]
        [TestCase(3, 32)]
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
            Assert.That(s.CanPlayCard(0), Is.True, "This authored tide attack still deals its base damage at zero tide.");
        }

        [TestCase("card_school_tide_b05", 10, "获得 1 点行动点")]
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
            int points = s.Battle.NavalActionPoints;
            Play(s, id);
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(300f));
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(points + 1));
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
        public void TideAbsorbsAboveTheOldLimitAndUnusedReservePersistsAtTheNextEndTurn()
        {
            CoreVoyageSession s = Battle(true);
            Play(s, "card_school_tide_b08"); Play(s, "card_school_tide_b08");
            Play(s, "card_school_tide_b08");
            Play(s, "card_school_tide_b08"); Play(s, "card_school_tide_b08");
            Assert.That(s.Battle.Schools.TideLimit, Is.EqualTo(int.MaxValue));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.EqualTo(50));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(44));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(44));
        }

        [Test]
        public void FireAliasesRequirePowder_UnlessAFuelSacrificeGrantsAFreeUse()
        {
            CoreVoyageSession s = Battle(true); s.Cargo.Powder = 0;
            foreach (string id in new[] { "card_school_fire_b07", "card_school_fire_b10" })
            {
                Put(s, id); s.Battle.Energy = 3;
                int enemy = s.Battle.EnemyBoardingHealth;
                Assert.That(s.CanPlayCard(0), Is.False);
                Assert.That(s.PlayCard(0), Is.False);
                Assert.That(s.Battle.Energy, Is.EqualTo(3));
                Assert.That(s.Battle.Hand, Has.Count.EqualTo(1));
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
                Assert.That(s.Battle.EnemyBurnDamage, Is.Zero);
                Assert.That(s.Cargo.Powder, Is.Zero);
            }
            Play(s, "card_boarding_fire_10");
            Assert.That(s.Battle.Boarding.FreePowderUses, Is.EqualTo(2));
            Play(s, "card_school_fire_b07");
            Assert.That(s.Battle.Boarding.FreePowderUses, Is.EqualTo(1));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(3));
            Assert.That(s.Cargo.Powder, Is.Zero);
        }

        [Test]
        public void ThunderIgnoresOrdinaryAttacksDrawAndEnergyCards_AndReadyDoesNotBuildChain()
        {
            CoreVoyageSession s = Battle();
            Play(s, "card_cannon_barrage"); Play(s, "card_tactical_draw"); Play(s, "card_energy_order");
            Play(s, "card_school_thunder_b05");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.EqualTo(5));
            Play(s, "card_school_thunder_b03");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void AuroraFanExtendsFieldsWithoutTheRetiredFreeEchoOrManaRequirements(int rank)
        {
            const string echoId = "card_special_sail_aurora_fan";
            CoreVoyageSession s = ResourceWindowSession("sail_aurora_fan", rank);
            SetTwoCopyDeck(s, NavalBattleStage.Naval, "card_school_thunder_b03", "card_school_thunder_n02",
                "card_brace", "card_tactical_draw", "card_energy_order");
            StartResourceWindowBattle(s);
            Assert.That(s.EndBattleTurn(), Is.True);
            SetNavalRange(s, 300f);
            ArrangeResourceWindowHand(s, "card_school_thunder_b03", "card_school_thunder_n02", echoId);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Assert.That(s.GetBattleCard(Index(echoId)).UpgradeLevel, Is.EqualTo(Math.Min(rank, s.GetCardMaximumUpgradeLevel(echoId))));
            Assert.That(s.GetSpecialCardExperience(echoId), Is.EqualTo(rank == 3 ? 840 : 0),
                "The component keeps its historical experience after changing its field effect.");

            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
            int available = s.Battle.DrawPile.Count;
            Assert.That(s.Battle.Hand, Has.Count.EqualTo(1));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Energy, Is.Zero, "The field extension uses its printed energy cost.");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2), "The draw does not advance or consume the chain.");
            Assert.That(s.Battle.Hand, Has.Count.EqualTo(s.GetBattleCard(Index(echoId)).Draw));
            Assert.That(s.Battle.DrawPile, Has.Count.EqualTo(available - s.GetBattleCard(Index(echoId)).Draw));
            Put(s, echoId); s.Battle.Energy = 3;
            int guard = s.Battle.PlayerBlock;
            Assert.That(s.CanPlayCard(0), Is.True, "A second copy still grants its basic guard and draw.");
            Assert.That(s.GetBattleCardEnergyCost(Index(echoId)), Is.EqualTo(2));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard + s.GetBattleCard(Index(echoId)).Block));
            Assert.That(s.Battle.Hand, Has.Count.EqualTo(s.GetBattleCard(Index(echoId)).Draw));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
        }

        [Test]
        public void WorldsEndVolleyKeepsBankedManaAndPaysOnlyItsPrintedCannonCosts()
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
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5), "Preparation mana carries into naval combat.");

            ArrangeResourceWindowHand(s, "card_school_arcane_b06");
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(7));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(4), "The next turn refills to the increased limit and discards excess.");
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            ArrangeResourceWindowHand(s, attackId);
            SetNavalRange(s, 300f);
            int powder = s.Cargo.Powder, balls = s.Cargo.Cannonballs, enemy = s.Battle.EnemyHull;
            Assert.That(s.CanPlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(4));
            Assert.That(s.Cargo.Powder, Is.EqualTo(powder - 1));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(balls - 1));
            Assert.That(s.Battle.EnemyHull, Is.LessThan(enemy));
        }

        [Test]
        public void ThunderGrowthRemainsLinearBeyondFifty_WithoutGrantingGuardOrFieldOverloadInBoarding()
        {
            CoreVoyageSession s = Battle(true);
            CoreBattleCard c = s.GetBattleCard(Index("card_school_thunder_b07"));
            int playerHealth = s.Battle.PlayerBoardingHealth;
            for (int i = 0; i < 52; i++)
            {
                int before = s.Battle.EnemyBoardingHealth;
                Assert.That(s.GetBoardingCardDamageQuote(c), Is.EqualTo(16 + 2 * i),
                    "Each attack uses its pre-play chain without the retired six-chain cap.");
                Play(s, c.Id);
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(before - 16 - 2 * i));
                Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(i + 1));
                Assert.That(s.Battle.PlayerBlock, Is.Zero);
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(playerHealth),
                    "Fifty chain alone does not deal overload damage outside a thunder field.");
            }
        }

        [Test]
        public void EnergyRecoveryDoesNotAvoidSameIdEscalatingCosts()
        {
            CoreVoyageSession s = Battle(true);
            int index = Index("card_boarding_basic_09");
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
            for (int rank = 0; rank <= 3; rank++)
            {
                CoreVoyageSession s = Battle(true, upgradedCard: "card_school_blood_b07", rank: rank);
                CoreBattleCard c = s.GetBattleCard(Index("card_school_blood_b07"));
                Assert.That(c.BoardingSpec.Key, Is.EqualTo("blood_01"));
                s.Battle.PlayerBoardingHealth = 7; s.Battle.PlayerBlock = 999;
                Play(s, c.Id);
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1));
                Assert.That(s.Battle.PlayerBlock, Is.EqualTo(999));
            }
        }

        private static CoreVoyageSession ResourceWindowSession(string component, int rank)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
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
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            if (upgradedCard != null) s.ConfigureCardUpgrades(new Dictionary<string, int> { { upgradedCard, rank } });
            s.ConfigureSpecialCardSources(CoreSchoolCardCatalog.All.Select(c => c.RequiredCrew).Where(v => v.Length > 0).ToArray(),
                CoreSchoolCardCatalog.All.Select(c => c.RequiredComponent).Where(v => v.Length > 0).ToArray());
            s.SetSeed("SCHOOL-RESOURCE-REWORK");
            s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 1000);
            if (!preparation) Assert.That(s.EndBattleTurn(), Is.True);
            if (!boarding) SetNavalRange(s, 200f);
            if (boarding)
            {
                s.Battle.DistanceMeters = 100f;
                typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
                typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
                s.Battle.Alignment = NavalBattleAlignment.Parallel;
                Assert.That(s.TryBoard(), Is.True);
                typeof(CoreBattleState).GetProperty("PlayerOrdinaryCrew").SetValue(s.Battle, 28);
                s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            }
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            s.Battle.EnemyBlock = 0; s.Battle.PlayerBlock = 0;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100; s.Cargo.Medicine = 0;
            return s;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void SetNavalRange(CoreVoyageSession s, float distance)
        {
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            pose.Invoke(s.Battle, new object[] { true, new VoyagePoint(-distance, 0f), 0f });
            pose.Invoke(s.Battle, new object[] { false, new VoyagePoint(0f, 0f), 180f });
        }
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
