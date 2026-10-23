using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardBalanceReviewTests
    {
        [Test]
        public void ReviewPreservesEveryPublishedIdentityAndTheSixSchoolSizes()
        {
            CoreBattleCard[] cards = CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None).ToArray();
            Assert.That(CoreNavalSchoolExpansionCatalog.All.Count, Is.EqualTo(27));
            Assert.That(cards.Length, Is.EqualTo(346));
            Assert.That(cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(346));
            Assert.That(cards.Count(card => card.SchoolId.Length == 0), Is.EqualTo(97));
            Assert.That(CoreVoyageSession.CardLibrary[219].Id, Is.EqualTo("card_energy_order"));
            Assert.That(CoreVoyageSession.CardLibrary[220].Id, Is.EqualTo("card_tactical_draw"));
            Assert.That(cards.Count(card => card.SchoolId.Length > 0), Is.EqualTo(249));
            Assert.That(CoreSchoolCardCatalog.All.Count, Is.EqualTo(159));
            Assert.That(cards.Where(card => card.SchoolId.Length > 0).Select(card => card.SchoolId).Distinct(),
                Is.EquivalentTo(new[] { "fire", "arcane", "curse", "tide", "thunder", "blood" }));
            foreach (string school in new[] { "fire", "arcane", "curse", "tide", "thunder", "blood" })
                Assert.That(cards.Count(card => card.SchoolId == school),
                    Is.EqualTo(37 + (school == "fire" || school == "tide" || school == "thunder" ? 6 : 3)), school);
            Assert.That(cards.Single(card => card.Id == "card_chain_shot").Damage, Is.EqualTo(10));
        }

        private static IEnumerable<TestCaseData> BloodMendCases()
        {
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.BoardingSpec?.Key == "blood_09"))
                foreach (int rank in new[] { 0, 1, 2, 3 })
                    foreach (int ordinary in new[] { 7, 28 })
                        foreach (int missing in new[] { 20, 900 })
                            foreach (bool medicine in new[] { false, true })
                                yield return new TestCaseData(card.Id, rank, ordinary, missing, medicine);
        }

        [TestCaseSource(nameof(BloodMendCases))]
        public void BloodMendAliases_UseFixedMedicineAndDesperateHealing_WithoutCrewScaling(string id, int rank, int ordinary, int missing, bool medicine)
        {
            CoreVoyageSession session = Battle(id, rank, NavalBattleStage.Boarding, ordinary);
            CoreBattleCard card = session.GetBattleCard(Index(id));
            Assert.That(card.BoardingSpec.Key, Is.EqualTo("blood_09"));
            Assert.That(card.SchoolEffect, Is.Null, "Old percentage healing must not run as a second payload.");
            session.Battle.PlayerBoardingHealth = 1000 - missing;
            session.Cargo.Medicine = medicine ? 5 : 0;
            bool desperate = 1000 - missing <= 400;
            // All three blood_09 identities share their authored two-rank curve.
            // Keep independent values here so a wrong interpreter multiplier cannot validate itself.
            int expected = Math.Min(missing, medicine ?
                AtRank(rank, desperate ? new[] { 9, 12, 15, 15 } : new[] { 7, 9, 12, 12 }) : desperate ? 3 : 2);
            Assert.That(card.UpgradeLevel, Is.EqualTo(Math.Min(rank, 2)), "The requested third rank clamps without increasing healing.");
            CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            Assert.That(quote.ActualRepair, Is.EqualTo(expected));
            Assert.That(quote.MedicineCost, Is.EqualTo(medicine ? 1 : 0));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(1000 - missing), "Reading the quote cannot heal.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine ? 5 : 0));
            Play(session, id);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(1000 - missing + expected));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine ? 4 : 0));
        }

        private static IEnumerable<TestCaseData> BloodPaymentCases()
        {
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.SchoolEffect != null &&
                (card.SchoolEffect.Mechanic == CoreSchoolMechanic.BloodRisk || card.SchoolEffect.Mechanic == CoreSchoolMechanic.BloodWard)))
                foreach (int rank in new[] { 0, 1, 2, 3 })
                    yield return new TestCaseData(card.Id, rank);
        }

        [TestCaseSource(nameof(BloodPaymentCases))]
        public void BloodContracts_KeepLifeCostAndScaleDirectBenefit_RejectLethalPaymentAtomically(string id, int rank)
        {
            CoreBattleCard original = CoreVoyageSession.CardLibrary[Index(id)];
            NavalBattleStage stage = original.IsAllowedInStage(NavalBattleStage.Preparation) ? NavalBattleStage.Preparation :
                original.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Boarding;
            CoreVoyageSession session = Battle(id, rank, stage, 7);
            CoreBattleCard card = session.GetBattleCard(Index(id));
            int cost = original.SchoolEffect.Threshold;
            Assert.That(original.SchoolEffect.Upgrades & (CoreSchoolUpgradeFields.Power | CoreSchoolUpgradeFields.Threshold),
                Is.EqualTo(CoreSchoolUpgradeFields.Power));
            Assert.That(card.SchoolEffect.Threshold, Is.EqualTo(cost));
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(ExpectedBloodBenefit(id, rank)));
            Assert.That(cost, Is.GreaterThan(0));
            SetHealth(session, cost);
            Put(session, id);
            string before = Snapshot(session);
            Assert.That(session.CardUnavailableReason(0), Is.EqualTo("支付生命后须至少剩余 1 点"));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before));
            SetHealth(session, cost + 1);
            int energy = session.Battle.Energy, powder = session.Cargo.Powder, cannonballs = session.Cargo.Cannonballs;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(stage == NavalBattleStage.Boarding ? session.Battle.PlayerBoardingHealth : session.Hull, Is.EqualTo(1),
                "The authored life cost is paid once; boarding crew scaling must not discount it.");
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - card.Energy));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder - card.PowderCost));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(cannonballs - card.CannonballCost));
        }

        private static IEnumerable<TestCaseData> FireLoadCases()
        {
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.SchoolEffect != null && card.SchoolEffect.Mechanic == CoreSchoolMechanic.FireLoad))
                foreach (int rank in new[] { 0, 1, 2, 3 }) yield return new TestCaseData(card.Id, rank);
        }

        [TestCaseSource(nameof(FireLoadCases))]
        public void FireLoad_UpgradesLayersOnly_KeepsReserveSizeAndAppliesOneLayerBatchPerShot(string id, int rank)
        {
            CoreBattleCard original = CoreVoyageSession.CardLibrary[Index(id)];
            NavalBattleStage stage = original.IsAllowedInStage(NavalBattleStage.Preparation)
                ? NavalBattleStage.Preparation : NavalBattleStage.Naval;
            CoreVoyageSession session = Battle(id, rank, stage, 28);
            CoreBattleCard card = session.GetBattleCard(Index(id));
            if (card.RequiredComponent.Length > 0)
                Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval));
            CoreSchoolCardEffect initial = original.SchoolEffect, effect = card.SchoolEffect;
            Assert.That(initial.Upgrades, Is.EqualTo(CoreSchoolUpgradeFields.Power));
            int expectedBurn = ExpectedLoadedBurn(id, rank);
            Assert.That(effect.Power, Is.EqualTo(expectedBurn));
            Assert.That(effect.Secondary, Is.EqualTo(initial.Secondary));
            Assert.That(effect.Limit, Is.EqualTo(initial.Limit));
            Assert.That(effect.Duration, Is.EqualTo(2));
            Assert.That(effect.Power * effect.Secondary,
                Is.EqualTo(expectedBurn * initial.Secondary), "Only applied layers scale; ammunition reserve quantity stays fixed.");
            Play(session, id);
            int reserve = Math.Min(initial.Secondary, initial.Limit);
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(reserve));
            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(expectedBurn));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(reserve));
            session.Battle.DistanceMeters = 200;
            session.Battle.EnemyBlock = 0;
            Play(session, "card_chain_shot");
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(reserve - 1));
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(expectedBurn));
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(expectedBurn));
        }

        private static int AtRank(int rank, params int[] values) => values[Math.Max(0, Math.Min(values.Length - 1, rank))];

        private static int ExpectedBloodBenefit(string id, int rank)
        {
            switch (id)
            {
                case "card_school_blood_b01": return AtRank(rank, 8, 10, 13, 13);
                case "card_school_blood_b02": return AtRank(rank, 8, 10, 14, 14);
                case "card_school_blood_b03": return AtRank(rank, 5, 7, 9, 12);
                case "card_school_blood_b05": return AtRank(rank, 8, 10, 12, 15);
                case "card_special_sail_fogshroud_wing": return AtRank(rank, 9, 12, 15, 15);
                case "card_special_sail_thunderreef_wing": return AtRank(rank, 12, 16, 21, 27);
                case "card_special_cannon_cometfall_battery": return AtRank(rank, 14, 18, 23, 23);
                case "card_special_plating_fogiron_carapace": return AtRank(rank, 9, 12, 15, 15);
                default: Assert.Fail("Add an independently reviewed blood-contract curve for " + id); return 0;
            }
        }

        private static int ExpectedLoadedBurn(string id, int rank)
        {
            switch (id)
            {
                case "card_school_fire_b01": return AtRank(rank, 4, 5, 6, 8);
                case "card_special_crew_twilight_tide_gunner_yilan": return AtRank(rank, 6, 7, 8, 10);
                case "card_special_instrument_whalestar_receiver": return AtRank(rank, 5, 7, 9, 9);
                default: Assert.Fail("Add an independently reviewed fire-load curve for " + id); return 0;
            }
        }

        private static CoreVoyageSession Battle(string id, int rank, NavalBattleStage stage, int ordinary)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.ConfigureSpecialCardSources(CoreSchoolCardCatalog.All.Select(card => card.RequiredCrew).Where(value => value.Length > 0).ToArray(),
                CoreSchoolCardCatalog.All.Select(card => card.RequiredComponent).Where(value => value.Length > 0).ToArray());
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("CARD-BALANCE-REVIEW-REGRESSION");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("balance_review", "数值回归", "海战", "隔离效果", true, false, 1, 1000), false });
            session.Battle.Stage = stage;
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 1000, 0, 0, 0, 0, 28, 1, 1, healthMultiplier: 1);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1000;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            session.Battle.EnemyBlock = session.Battle.PlayerBlock = 0;
            session.Battle.Energy = 100;
            session.Battle.DistanceMeters = 100;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            typeof(CoreBattleState).GetProperty("PlayerOrdinaryCrew").SetValue(session.Battle, ordinary);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            session.Cargo.Medicine = 0;
            return session;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static void Put(CoreVoyageSession session, string id) { session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession session, string id) { session.Battle.Energy = 100; Put(session, id); Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0)); }
        private static void SetHealth(CoreVoyageSession session, int health)
        {
            if (session.Battle.Stage == NavalBattleStage.Boarding) session.Battle.PlayerBoardingHealth = health;
            else typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, health);
        }
        private static string Snapshot(CoreVoyageSession session) => string.Join(":", new object[]
        {
            session.Hull, session.Battle.PlayerBoardingHealth, session.Battle.EnemyHull, session.Battle.EnemyBoardingHealth,
            session.Battle.Energy, session.Battle.PlayerBlock, session.Battle.EnemyBlock, session.Battle.CardsPlayedThisTurn,
            session.Cargo.Powder, session.Cargo.Cannonballs, session.Cargo.Medicine, session.Battle.Schools.ThunderChain,
            string.Join(",", session.Battle.Hand), string.Join(",", session.Battle.DrawPile), string.Join(",", session.Battle.DiscardPile)
        });
    }
}
