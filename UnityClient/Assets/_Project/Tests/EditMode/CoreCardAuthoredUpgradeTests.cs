using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardAuthoredUpgradeTests
    {
        [Test]
        public void EveryLiveCardHasAnExplicitProfileAndRankZeroLeavesItsBaselineUntouched()
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard original in LiveCards())
            {
                CoreCardUpgradeProfile profile = CoreCardUpgradeDesign.Find(original);
                Assert.That(profile, Is.Not.Null, original.Id);
                Assert.That(profile.MaximumLevel, Is.InRange(0, 3), original.Id);
                Assert.That(profile.Multiplier(0), Is.EqualTo(1m), original.Id);
                Assert.That(profile.BlockMultiplier(0), Is.EqualTo(1m), original.Id);
                Assert.That(profile.RepairMultiplier(0), Is.EqualTo(1m), original.Id);
                Assert.That(profile.EffectMultiplier(0), Is.EqualTo(1m), original.Id);
                Assert.That(profile.DrawBonus(0), Is.Zero, original.Id);
                Assert.That(profile.EnergyBonus(0), Is.Zero, original.Id);
                Assert.That(profile.ActionPointBonus(0), Is.Zero, original.Id);
                Assert.That(session.GetCardMaximumUpgradeLevel(original.Id), Is.EqualTo(profile.MaximumLevel), original.Id);
                CoreBattleCard baseline = session.GetBattleCardAtLevel(Index(original.Id), 0);
                Assert.That(baseline, Is.SameAs(CoreVoyageSession.CardLibrary[Index(baseline.Id)]), original.Id);
                Assert.That(baseline.UpgradeLevel, Is.Zero, original.Id);
            }
            Assert.That(CoreCardUpgradeDesign.Find("missing_card"), Is.Null);
            Assert.That(CoreCardUpgradeDesign.Find((CoreBattleCard)null), Is.Null);
        }

        [Test]
        public void EveryPurchasableRankAddsARealBenefitAndNeverWeakensAnotherBenefit()
        {
            var session = new CoreVoyageSession();
            var failures = new List<string>();
            foreach (CoreBattleCard original in LiveCards())
            {
                int index = Index(original.Id), maximum = session.GetCardMaximumUpgradeLevel(index);
                CoreBattleCard previous = session.GetBattleCardAtLevel(index, 0);
                for (int rank = 1; rank <= maximum; rank++)
                {
                    CoreBattleCard current = session.GetBattleCardAtLevel(index, rank);
                    int[] before = Benefits(previous), after = Benefits(current);
                    if (before.Length != after.Length || after.Where((value, i) => value < before[i]).Any())
                        failures.Add(original.Id + " +" + rank + " reduces an existing benefit.");
                    else if (before.SequenceEqual(after))
                        failures.Add(original.Id + " +" + rank + " has no actual numerical, draw, energy, action-point or hidden-effect improvement.");
                    previous = current;
                }
            }
            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void RequestedRanksClampPerCardWithoutChangingSavedIdentityOrBaseRules()
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard original in LiveCards())
            {
                int index = Index(original.Id), maximum = session.GetCardMaximumUpgradeLevel(index);
                CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
                CoreBattleCard cap = session.GetBattleCardAtLevel(index, maximum);
                Assert.That(session.GetBattleCardAtLevel(index, -1), Is.SameAs(basis), original.Id);
                Assert.That(session.GetBattleCardAtLevel(index, int.MinValue), Is.SameAs(basis), original.Id);
                Assert.That(session.GetBattleCardAtLevel(index, maximum + 1), Is.SameAs(cap), original.Id);
                Assert.That(session.GetBattleCardAtLevel(index, int.MaxValue), Is.SameAs(cap), original.Id);
                for (int rank = 0; rank <= maximum; rank++)
                {
                    CoreBattleCard current = session.GetBattleCardAtLevel(index, rank);
                    Assert.That(current.UpgradeLevel, Is.EqualTo(rank), original.Id);
                    Assert.That(current.Id, Is.EqualTo(basis.Id), original.Id);
                    Assert.That(current.ArtId, Is.EqualTo(basis.ArtId), original.Id);
                    Assert.That(current.RequiredCrew, Is.EqualTo(basis.RequiredCrew), original.Id);
                    Assert.That(current.RequiredComponent, Is.EqualTo(basis.RequiredComponent), original.Id);
                    Assert.That(current.AllowedStages, Is.EqualTo(basis.AllowedStages), original.Id);
                    Assert.That(new[] { current.Energy, current.PowderCost, current.CannonballCost },
                        Is.EqualTo(new[] { basis.Energy, basis.PowderCost, basis.CannonballCost }), original.Id);
                    Assert.That(current.SchoolEffect?.Mechanic, Is.EqualTo(basis.SchoolEffect?.Mechanic), original.Id);
                    if (basis.SchoolEffect != null)
                        Assert.That(new[] { current.SchoolEffect.Threshold, current.SchoolEffect.Duration, current.SchoolEffect.Limit,
                            current.SchoolEffect.Movement, current.SchoolEffect.WoodCost, current.SchoolEffect.FallbackDamage, current.SchoolEffect.FallbackBlock },
                            Is.EqualTo(new[] { basis.SchoolEffect.Threshold, basis.SchoolEffect.Duration, basis.SchoolEffect.Limit,
                                basis.SchoolEffect.Movement, basis.SchoolEffect.WoodCost, basis.SchoolEffect.FallbackDamage, basis.SchoolEffect.FallbackBlock }), original.Id);
                }
            }
        }

        [Test]
        public void LorneAndEvelynHaveDifferentCapsAndEvelynOvertakesOnlyAtHerFinalRank()
        {
            var session = new CoreVoyageSession();
            const string lorne = "card_special_crew_broken_anchor_navigator_lorne";
            const string evelyn = "card_special_crew_gray_gull_navigator_evelyn";
            Assert.That(session.GetCardMaximumUpgradeLevel(lorne), Is.EqualTo(2));
            Assert.That(session.GetCardMaximumUpgradeLevel(evelyn), Is.EqualTo(3));
            Assert.That(Enumerable.Range(0, 4).Select(rank => session.GetBattleCardAtLevel(Index(lorne), rank).Block),
                Is.EqualTo(new[] { 6, 8, 10, 10 }));
            Assert.That(Enumerable.Range(0, 4).Select(rank => session.GetBattleCardAtLevel(Index(evelyn), rank).Block),
                Is.EqualTo(new[] { 5, 7, 10, 14 }));
            foreach (string id in new[] { lorne, evelyn })
                for (int rank = 0; rank <= 3; rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), rank);
                    Assert.That(card.Draw, Is.EqualTo(1), id);
                    Assert.That(card.Energy, Is.EqualTo(1), id);
                    int expectedPoints = id == lorne ? new[] { 1, 2, 2, 2 }[rank] : new[] { 1, 1, 2, 2 }[rank];
                    Assert.That(card.NavalActionPointGain, Is.EqualTo(expectedPoints), id);
                }
        }

        [TestCase("crew_broken_anchor_navigator_lorne", 1, 8, 2)]
        [TestCase("crew_broken_anchor_navigator_lorne", 3, 10, 2)]
        [TestCase("crew_gray_gull_navigator_evelyn", 1, 7, 1)]
        [TestCase("crew_gray_gull_navigator_evelyn", 3, 14, 2)]
        public void NavigatorRanksActuallyGrantTheirAuthoredGuardDrawAndActionPoints(string source, int rank, int guard, int actionPoints)
        {
            string id = CoreSpecialCardCatalog.ForCrew(source).Card.Id;
            CoreVoyageSession session = Start(id, rank, false);
            int points = session.Battle.NavalActionPoints, energy = session.Battle.Energy;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(guard));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + actionPoints));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - 1));
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void LegendaryDoctrineOnlyGrowsItsDrawAndStopsAtRankTwo(string school)
        {
            CoreBattleCard basis = CoreSpecialCardCatalog.All.Select(d => d.Card)
                .Single(card => card.SchoolId == school && CoreSpecialCardRules.IsDoctrine(card));
            var session = new CoreVoyageSession();
            Assert.That(session.GetCardMaximumUpgradeLevel(basis.Id), Is.EqualTo(2));
            for (int rank = 0; rank <= 3; rank++)
            {
                CoreBattleCard current = session.GetBattleCardAtLevel(Index(basis.Id), rank);
                Assert.That(current.Draw, Is.EqualTo(new[] { 1, 2, 3, 3 }[rank]));
                Assert.That(current.Energy, Is.EqualTo(2));
                Assert.That(current.Damage + current.Block + current.Repair + current.PowderCost + current.CannonballCost, Is.Zero);
                Assert.That(current.SchoolEffect.Mechanic, Is.EqualTo(basis.SchoolEffect.Mechanic));
                Assert.That(new[] { current.SchoolEffect.Power, current.SchoolEffect.Secondary, current.SchoolEffect.Threshold,
                    current.SchoolEffect.Duration, current.SchoolEffect.Limit }, Is.EqualTo(new[] { basis.SchoolEffect.Power,
                    basis.SchoolEffect.Secondary, basis.SchoolEffect.Threshold, basis.SchoolEffect.Duration, basis.SchoolEffect.Limit }));
            }
            CoreVoyageSession battle = Start(basis.Id, 3, false);
            Assert.That(battle.PlayCard(0), Is.True, battle.CardUnavailableReason(0));
            Assert.That(battle.Battle.Hand.Count, Is.EqualTo(3));
            Assert.That(battle.Battle.SpecialCards.IsActive(school), Is.True);
            Assert.That(battle.Battle.SpecialCards.HasPlayed(school), Is.False);
        }

        [TestCase("card_boarding_fire_03", 1)]
        [TestCase("card_boarding_fire_03", 3)]
        [TestCase("card_boarding_thunder_06", 1)]
        [TestCase("card_boarding_thunder_06", 3)]
        public void HiddenBoardingEffectsUseTheAuthoredEffectCurveAtExecution(string id, int rank)
        {
            CoreVoyageSession session = Start(id, rank, true);
            CoreBattleCard card = session.GetBattleCard(Index(id));
            int expected = CoreCardUpgradeDesign.ScaleEffect(card, 8, card.UpgradeLevel);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(id.EndsWith("fire_03", StringComparison.Ordinal) ? session.Battle.EnemyBurnDamage : session.Battle.Boarding.ThunderReady,
                Is.EqualTo(expected), id);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(card.Draw));
        }

        [TestCase("card_boarding_basic_01", 3)]
        [TestCase("card_boarding_basic_04", 3)]
        [TestCase("card_boarding_basic_06", 3)]
        public void BoardingPrintedDamageGuardAndHealingMatchTheActualAuthoredRank(string id, int rank)
        {
            CoreVoyageSession session = Start(id, rank, true);
            session.Battle.PlayerBoardingHealth = 500;
            CoreBattleCard card = session.GetBattleCard(Index(id));
            int enemy = session.Battle.EnemyBoardingHealth, crew = session.Battle.PlayerBoardingHealth;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(enemy - session.Battle.EnemyBoardingHealth, Is.EqualTo(card.Damage), id);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(card.Block), id);
            Assert.That(session.Battle.PlayerBoardingHealth - crew, Is.EqualTo(card.Repair), id);
        }

        [TestCase("fire", 1, "direct-and-burn")]
        [TestCase("fire", 3, "direct-and-burn")]
        [TestCase("arcane", 1, "mana-attack")]
        [TestCase("arcane", 3, "mana-attack")]
        [TestCase("curse", 4, "mark-guard")]
        [TestCase("tide", 1, "tide-attack")]
        [TestCase("tide", 2, "paid-guard")]
        [TestCase("tide", 3, "tide-attack")]
        [TestCase("tide", 4, "paid-guard")]
        [TestCase("thunder", 4, "guard-and-next-attack")]
        [TestCase("blood", 2, "paid-life-guard")]
        public void ComparableLegendaryComponentsNeverFallBelowTheirEpicCoreEffectAtEqualOrMaximumRanks(string school, int slot, string comparison)
        {
            CoreBattleCard epic = CoreSpecialCardCatalog.All.Select(d => d.Card)
                .Single(c => c.ArtId == "card_school_" + school + "_e0" + slot);
            CoreBattleCard legendary = CoreSpecialCardCatalog.All.Select(d => d.Card)
                .Single(c => c.ArtId == "card_school_" + school + "_l0" + slot);
            var session = new CoreVoyageSession();
            int[] resources = school == "curse" ? new[] { 1, 3, 6 } : school == "tide" ? new[] { 1, 10, 20 } : new[] { 0 };
            foreach (int resource in resources)
            {
                for (int rank = 0; rank <= 3; rank++)
                {
                    CoreBattleCard lower = session.GetBattleCardAtLevel(Index(epic.Id), rank);
                    CoreBattleCard higher = session.GetBattleCardAtLevel(Index(legendary.Id), rank);
                    Assert.That(ComparableValue(higher, comparison, resource), Is.GreaterThanOrEqualTo(ComparableValue(lower, comparison, resource)),
                        school + " slot " + slot + " requested rank " + rank + " resource " + resource + ": " + lower.Id + " / " + higher.Id);
                    if (school == "thunder") Assert.That(higher.SchoolEffect.Power, Is.GreaterThanOrEqualTo(lower.SchoolEffect.Power));
                }
                CoreBattleCard lowerCap = session.GetBattleCardAtLevel(Index(epic.Id), session.GetCardMaximumUpgradeLevel(epic.Id));
                CoreBattleCard higherCap = session.GetBattleCardAtLevel(Index(legendary.Id), session.GetCardMaximumUpgradeLevel(legendary.Id));
                Assert.That(ComparableValue(higherCap, comparison, resource), Is.GreaterThanOrEqualTo(ComparableValue(lowerCap, comparison, resource)),
                    school + " slot " + slot + " at each card's own maximum rank with resource " + resource);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void SpecialistFireLoadingAndLegendaryThunderPreparationKeepTheirAuthoredTierAdvantage(int rank)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard rareFire = session.GetBattleCardAtLevel(Index("card_special_bow_gilded_mermaid"), rank);
            CoreBattleCard epicFire = session.GetBattleCardAtLevel(Index("card_special_instrument_whalestar_receiver"), rank);
            Assert.That(rareFire.Action, Is.EqualTo(NavalBattleCardAction.FireRounds));
            Assert.That(epicFire.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.FireLoad));
            Assert.That(epicFire.SchoolEffect.Power, Is.GreaterThan(rareFire.BurnDamage), "The dedicated epic loader retains its stronger burn.");
            CoreBattleCard epicThunder = session.GetBattleCardAtLevel(Index("card_special_plating_aurora_laminate"), rank);
            CoreBattleCard legendaryThunder = session.GetBattleCardAtLevel(Index("card_special_sail_skybreaker_array"), rank);
            Assert.That(epicThunder.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ThunderReady));
            Assert.That(legendaryThunder.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ThunderReady));
            Assert.That(legendaryThunder.SchoolEffect.Power, Is.GreaterThan(epicThunder.SchoolEffect.Power));
        }

        private static int ComparableValue(CoreBattleCard card, string comparison, int resource)
        {
            // These are explicitly comparable roles under the same resource state. This is absolute core
            // output, not energy efficiency; unrelated generation, drain percentages and caps are not ordered.
            switch (comparison)
            {
                case "direct-and-burn": return card.Damage + card.SchoolEffect.Power * card.SchoolEffect.Duration;
                case "mana-attack": return card.Damage + card.SchoolEffect.Power;
                case "mark-guard": return card.Block + resource * card.SchoolEffect.Power;
                case "tide-attack": return card.Damage + resource * card.SchoolEffect.Power / 100;
                case "paid-guard": return card.Block;
                case "guard-and-next-attack":
                case "paid-life-guard": return card.Block + card.SchoolEffect.Power;
                default: throw new ArgumentException("Unknown explicit component comparison: " + comparison);
            }
        }

        private static IEnumerable<CoreBattleCard> LiveCards() => CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None);

        private static int[] Benefits(CoreBattleCard card)
        {
            var result = new List<int> { card.Damage, card.Block, card.Repair, card.Draw, card.NavalActionPointGain, card.BoardingBlock ?? card.Block,
                CoreBattleUpgradeBalance.EnergyRecovery(card) };
            bool strategy = card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad ||
                card.Action == NavalBattleCardAction.Polish;
            result.Add(strategy ? Math.Min(card.StrategyAmount, card.StrategyLimit) : 0);
            result.Add(card.Action == NavalBattleCardAction.FireRounds ? card.BurnDamage : 0);
            result.Add(card.SchoolEffect?.Power ?? 0);
            result.Add(card.SchoolEffect?.Secondary ?? 0);
            // Only values consumed by real boarding rules qualify as hidden growth. Most cards expose their
            // direct result already; these cases additionally represent burns, reactions or conditional payoffs.
            foreach (int baseline in HiddenBoardingValues(card.BoardingSpec?.Key))
                result.Add(CoreCardUpgradeDesign.ScaleEffect(card, baseline, card.UpgradeLevel));
            CoreBattleCard boarding = card.IsAllowedInStage(NavalBattleStage.Boarding) ? CoreBoardingCardCatalog.ForBoarding(card) : card;
            if (!ReferenceEquals(boarding, card)) result.AddRange(Benefits(boarding));
            return result.ToArray();
        }

        private static int[] HiddenBoardingValues(string key)
        {
            switch (key)
            {
                case "fire_02": return new[] { 4 };
                case "fire_03": return new[] { 8 };
                case "fire_04": return new[] { 6 };
                case "fire_06": return new[] { 3, 5 };
                case "fire_07": return new[] { 7, 10, 13 };
                case "fire_08": return new[] { 6, 10, 14 };
                case "arcane_07": return new[] { 8 };
                case "arcane_08": return new[] { 4 };
                case "arcane_13": return new[] { 18 };
                case "curse_07": return new[] { 3, 9 };
                case "curse_08": return new[] { 4, 16, 2, 8 };
                case "curse_09": return new[] { 2, 10 };
                case "tide_04": return new[] { 4 };
                case "tide_09": return new[] { 16 };
                case "tide_10": return new[] { 3 };
                case "thunder_04": return new[] { 6 };
                case "thunder_06": return new[] { 8 };
                case "thunder_10": return new[] { 4 };
                case "thunder_12": return new[] { 2, 5 };
                case "blood_06": return new[] { 2 };
                case "blood_08": return new[] { 8 };
                case "blood_09": return new[] { 7, 9 };
                case "blood_11": return new[] { 14 };
                case "blood_12": return new[] { 8 };
                case "blood_13": return new[] { 18 };
                case "blood_15": return new[] { 8 };
                default: return Array.Empty<int>();
            }
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static CoreVoyageSession Start(string id, int rank, bool boarding)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.ConfigureSpecialCardSources(CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Crew).Select(c => c.SourceId).ToArray(),
                CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Component).Select(c => c.SourceId).ToArray());
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("AUTHORED-CARD-UPGRADES");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            if (boarding) typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NavalBattleStage.Boarding });
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.Energy = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Wood = session.Cargo.Medicine = 100;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(id));
            session.Battle.DiscardPile.Clear(); session.Battle.DrawPile.Clear();
            session.Battle.DrawPile.AddRange(Enumerable.Repeat(Index(boarding ? "card_boarding_basic_01" : "card_brace"), 20));
            return session;
        }
    }
}
