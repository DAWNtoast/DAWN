using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingBossPacingTests
    {
        private static IEnumerable<TestCaseData> Bosses()
        {
            yield return Boss("boss_route_shattered_sail_reef", "fire", 3, 2, 4);
            yield return Boss("boss_route_shattered_sail_reef_tide", "tide", 2, 4, 2, 4);
            yield return Boss("boss_route_golden_tide_trade", "blood", 5, 3, 4);
            yield return Boss("boss_route_golden_tide_trade_thunder", "thunder", 4, 2, 6);
            yield return Boss("boss_route_moon_tide_ruins", "curse", 3, 3, 6);
            yield return Boss("boss_route_moon_tide_ruins_arcane", "arcane", 2, 5, 3, 6);
            yield return Boss("legendary_pirate_iron_oath", "curse", 5, 2, 3, 6);
            yield return Boss("legendary_pirate_thunder_crown", "thunder", 6, 2, 6, 2);
            yield return Boss("legendary_pirate_red_widow", "blood", 5, 2, 4, 5);
            yield return Boss("legendary_pirate_ember_ark", "fire", 4, 2, 6);
            yield return Boss("legendary_pirate_tide_requiem", "tide", 4, 3, 5);
            yield return Boss("legendary_pirate_last_horizon", "arcane", 2, 3, 7);
        }

        private static TestCaseData Boss(string id, string school, params int[] energy) => new TestCaseData(id, school, energy);

        [TestCase("boss_route_shattered_sail_reef", 2, 119)]
        [TestCase("boss_route_shattered_sail_reef_tide", 3, 105)]
        public void FirstChartBossesLeaveStarterHealthRoomAcrossTheCycleBoundary(string id, int recoveryTurn, int maximumHealth)
        {
            CoreVoyageSession session = Battle(id);
            CoreBoardingEnemyDeck deck = session.Battle.BoardingEnemy.Deck;
            // The default unupgraded sloop has 60 boarding health. Even an
            // unblocked peak must leave time to respond, including DOT and the
            // last-to-first transition that used to chain two lethal fire peaks.
            Assert.That(session.Battle.Profile.BoardingHealth, Is.LessThanOrEqualTo(maximumHealth));
            int previousPressure = 0;
            for (int turn = 1; turn <= deck.Turns.Count * 2; turn++)
            {
                if (deck.SchoolId == "tide")
                    SetProperty(session.Battle.BoardingEnemy, "TidePower", CoreBoardingEnemyState.TidePowerLimit);
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                int pressure = preview.PlayerHealthLoss + preview.PlayerBurnDamage;
                Assert.That(pressure, Is.LessThanOrEqualTo(32), id + " turn " + turn);
                Assert.That(previousPressure + pressure, Is.LessThanOrEqualTo(45), "Adjacent peaks, including DOT, must leave 15 starter health to respond.");
                if (turn == 1) Assert.That(pressure, Is.LessThanOrEqualTo(20));
                if ((turn - 1) % deck.Turns.Count + 1 == recoveryTurn)
                {
                    Assert.That(preview.AttackDamage, Is.Zero);
                    Assert.That(pressure, Is.LessThanOrEqualTo(2), "Recovery must remain a real breathing window after DOT.");
                }
                previousPressure = pressure;
                Assert.That(session.EndBattleTurn(), Is.True);
            }
        }

        [Test]
        public void AllTwelveBossesHaveIndependentDecksAndOtherEncountersKeepTheirFallbacks()
        {
            string[] ids = CoreRouteBossCatalog.All.Select(boss => boss.Id)
                .Concat(CoreLegendaryWarships.All.Select(boss => boss.Id)).ToArray();
            Assert.That(ids.Length, Is.EqualTo(12));
            var decks = ids.Select(CoreBoardingBossCatalog.ForEncounter).ToArray();
            Assert.That(decks.Distinct().Count(), Is.EqualTo(12));
            foreach (CoreBoardingEnemyDeck deck in decks)
            {
                Assert.That(deck, Is.Not.Null);
                Assert.That(deck, Is.Not.SameAs(CoreBoardingEnemyCatalog.ForSchool(deck.SchoolId)));
            }
            Assert.That(CoreBoardingBossCatalog.ForEncounter(null), Is.Null);
            Assert.That(CoreBoardingBossCatalog.ForEncounter("unknown"), Is.Null);
            Assert.That(CoreBoardingBossCatalog.ForEncounter(CoreBattleBalance.TreasureBossId), Is.Null);
            Assert.That(CoreBoardingEnemyCatalog.ForEncounter("unknown"), Is.SameAs(CoreBoardingEnemyCatalog.ForSchool("neutral")));
            Assert.That(CoreBoardingEnemyCatalog.ForEncounter(CoreBattleBalance.TreasureBossId), Is.SameAs(CoreBoardingEnemyCatalog.ForSchool("curse")));
        }

        [TestCase("boss_route_golden_tide_trade", true)]
        [TestCase("boss_route_golden_tide_trade_thunder", false)]
        public void SecondChartPeaksLeaveRoomForPreparedCrewEvenWhenDesperate(string id, bool desperate)
        {
            CoreVoyageSession session = Battle(id);
            int previous = 0;
            for (int turn = 1; turn <= 9; turn++)
            {
                if (desperate)
                {
                    session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth * 3 / 10;
                    SetProperty(session.Battle.BoardingEnemy, "BloodLoss", CoreBoardingEnemyState.BloodLossLimit);
                }
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                int pressure = preview.PlayerHealthLoss + preview.PlayerBurnDamage;
                // Includes full blood resources below the 40% desperation threshold.
                Assert.That(pressure, Is.LessThanOrEqualTo(32), id + " turn " + turn);
                Assert.That(previous + pressure, Is.LessThanOrEqualTo(56));
                if (turn == 1) Assert.That(pressure, Is.LessThanOrEqualTo(30));
                if ((turn - 1) % 3 == 1) Assert.That(pressure, Is.Zero);
                int health = session.Battle.PlayerBoardingHealth;
                Assert.That(session.EndBattleTurn(), Is.True);
                Assert.That(health - session.Battle.PlayerBoardingHealth, Is.EqualTo(pressure));
                previous = pressure;
            }
        }

        [TestCaseSource(nameof(Bosses))]
        public void AuthoredBudgetMatchesChartProgressionWithSchoolIdentityAndBasicSupport(string id, string school, int[] energy)
        {
            CoreBoardingEnemyDeck deck = CoreBoardingBossCatalog.ForEncounter(id);
            Assert.That(deck, Is.Not.Null);
            Assert.That(deck.SchoolId, Is.EqualTo(school));
            Assert.That(CoreBoardingEnemyCatalog.ForEncounter(id), Is.SameAs(deck));
            CollectionAssert.AreEqual(energy, deck.Turns.Select(turn => turn.TotalEnergy));
            int average = id.StartsWith("boss_route_shattered_sail_reef", StringComparison.Ordinal) ? 3 : 4;
            Assert.That(energy.Sum(), Is.EqualTo(average * energy.Length));
            Assert.That(energy.Min(), Is.LessThan(average));
            Assert.That(energy.Max(), Is.GreaterThan(average));
            Assert.That(deck.Cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(deck.Cards.Count));
            foreach (CoreBoardingEnemyCard card in deck.Cards)
                Assert.That(card.Energy, Is.InRange(1, 3), card.Id);
            Assert.That(deck.Cards.Any(card => card.Mechanic == CoreBoardingEnemyMechanic.Basic &&
                (card.AttackPercent > 0 || card.HealPercent > 0)), Is.True);
            Assert.That(deck.Cards.Any(card => card.Mechanic != CoreBoardingEnemyMechanic.Basic &&
                card.Mechanic.ToString().StartsWith(school, StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [TestCaseSource(nameof(Bosses))]
        public void BasicOneEnergyEffectsUseTheFixedRecommendedPlayerScale(string id, string school, int[] energy)
        {
            CoreVoyageSession session = Battle(id);
            CoreBoardingEnemyDeck deck = session.Battle.BoardingEnemy.Deck;
            Assert.That(deck.SchoolId, Is.EqualTo(school));
            // Fixed recommended ranks 0 / 1 / 2; profile damage cannot redefine the target.
            decimal multiplier = id.StartsWith("boss_route_shattered_sail_reef", StringComparison.Ordinal) ? 1m :
                id.StartsWith("boss_route_golden_tide_trade", StringComparison.Ordinal) ? 1.2m : 1.5m;
            int checkedEffects = 0;
            for (int turn = 1; turn <= energy.Length; turn++)
            {
                session.Battle.PhaseTurn = turn;
                session.Battle.PlayerBlock = 0;
                session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth - 1000;
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                foreach (CoreBoardingEnemyCard card in deck.TurnAt(turn).Cards.Where(card =>
                    card.Energy == 1 && card.Mechanic == CoreBoardingEnemyMechanic.Basic))
                {
                    CoreBoardingEnemyActionResult action = preview.Actions.Single(result => result.CardId == card.Id);
                    if (card.AttackPercent > 0)
                    {
                        Assert.That(Math.Abs(action.AttackDamage - 8m * multiplier), Is.LessThanOrEqualTo(1m), card.Id);
                        checkedEffects++;
                    }
                    if (card.HealPercent > 0)
                    {
                        Assert.That(Math.Abs(action.Healed - 6m * multiplier), Is.LessThanOrEqualTo(1m), card.Id);
                        checkedEffects++;
                    }
                }
            }
            Assert.That(checkedEffects, Is.GreaterThan(0), "Every boss must exercise the actual one-energy scale.");
        }

        [TestCaseSource(nameof(Bosses))]
        public void ActualBossExecutesSixCyclesWithAccuratePreviewAndBoundedBreathingTurns(string id, string school, int[] energy)
        {
            CoreVoyageSession session = Battle(id);
            CoreBoardingEnemyDeck deck = session.Battle.BoardingEnemy.Deck;
            Assert.That(deck, Is.SameAs(CoreBoardingBossCatalog.ForEncounter(id)));
            Assert.That(deck.SchoolId, Is.EqualTo(school));
            var settledDamage = new List<int>();
            for (int turn = 1; turn <= energy.Length * 6; turn++)
            {
                Assert.That(session.Battle.PhaseTurn, Is.EqualTo(turn));
                session.Battle.PlayerBlock = turn % energy.Length == 0 ? 1000 : 0;
                CoreBoardingEnemyTurn scripted = deck.TurnAt(turn);
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                CollectionAssert.AreEqual(scripted.Cards.Select(card => card.Id), preview.Actions.Select(action => action.CardId));
                string expected = Describe(preview.Actions);
                string before = Snapshot(session);
                Assert.That(session.GetEnemyIntent(), Does.Contain(scripted.Name));
                Assert.That(session.GetEnemyIntent(), Does.Contain(deck.TurnAt(turn + 1).Name));
                Assert.That(Describe(session.GetBoardingEnemyPreview().Actions), Is.EqualTo(expected));
                Assert.That(Snapshot(session), Is.EqualTo(before), "Reading intent must not consume a boss resource.");
                if (scripted.TotalEnergy <= 3)
                    Assert.That(preview.AttackDamage, Is.LessThanOrEqualTo(session.Battle.Profile.MeleeDamage * 2),
                        id + " turn " + turn + " must retain its lower-pressure attack window.");
                int playerHealth = session.Battle.PlayerBoardingHealth;
                Assert.That(session.EndBattleTurn(), Is.True);
                Assert.That(session.Battle, Is.Not.Null);
                Assert.That(Describe(session.Battle.BoardingEnemy.LastActions), Is.EqualTo(expected));
                Assert.That(playerHealth - session.Battle.PlayerBoardingHealth,
                    Is.EqualTo(preview.PlayerHealthLoss + preview.PlayerBurnDamage + preview.PlayerCurseDamage));
                AssertCaps(session.Battle.BoardingEnemy, session.Battle.Profile.BoardingGuard);
                foreach (CoreBoardingEnemyActionResult action in session.Battle.BoardingEnemy.LastActions)
                {
                    AssertCaps(action.ResourcesAfter, session.Battle.Profile.BoardingGuard);
                    Assert.That(action.EnemyHealthAfter, Is.InRange(1, session.Battle.EnemyMaxBoardingHealth));
                }
                Assert.That(session.Battle.BoardingEnemy.ThunderChain, Is.Zero);
                settledDamage.Add(preview.AttackDamage + preview.PlayerBurnDamage + preview.PlayerCurseDamage);
            }
            CollectionAssert.AreEqual(settledDamage.Skip(energy.Length * 4).Take(energy.Length),
                settledDamage.Skip(energy.Length * 5), "Established cycles must not accumulate permanent damage growth.");
        }

        [TestCaseSource(nameof(Bosses))]
        public void ActualHealingCardsNeverExceedTheBossMaximum(string id, string school, int[] energy)
        {
            CoreVoyageSession session = Battle(id);
            CoreBoardingEnemyDeck deck = session.Battle.BoardingEnemy.Deck;
            Assert.That(deck.SchoolId, Is.EqualTo(school));
            bool sawHealing = false;
            for (int turn = 1; turn <= energy.Length; turn++)
            {
                session.Battle.PhaseTurn = turn;
                session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth - 1;
                SetProperty(session.Battle.BoardingEnemy, "TidePower", CoreBoardingEnemyState.TidePowerLimit);
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                Assert.That(session.EndBattleTurn(), Is.True);
                Assert.That(Describe(session.Battle.BoardingEnemy.LastActions), Is.EqualTo(Describe(preview.Actions)));
                foreach (CoreBoardingEnemyActionResult action in session.Battle.BoardingEnemy.LastActions)
                {
                    sawHealing |= action.Healed > 0;
                    Assert.That(action.EnemyHealthAfter, Is.LessThanOrEqualTo(session.Battle.EnemyMaxBoardingHealth));
                }
            }
            Assert.That(sawHealing, Is.True, "Every authored boss has an effective recovery action.");
        }

        private static void AssertCaps(CoreBoardingEnemyState state, int guard)
        {
            Assert.That(state.PlayerBurnDamage, Is.InRange(0, CoreBoardingEnemyState.BurnLimit));
            Assert.That(state.PlayerBurnTurns, Is.EqualTo(state.PlayerBurnDamage));
            Assert.That(state.SelfBurnDamage, Is.InRange(0, CoreBoardingEnemyState.BurnLimit));
            Assert.That(state.SelfBurnTurns, Is.EqualTo(state.SelfBurnDamage));
            Assert.That(state.Mana, Is.InRange(0, CoreBoardingEnemyState.ManaLimit));
            Assert.That(state.ThunderChain, Is.InRange(0, CoreBoardingEnemyState.ThunderChainLimit));
            Assert.That(state.CurseMarks, Is.InRange(0, CoreBoardingEnemyState.CurseMarkLimit));
            Assert.That(state.TidePower, Is.InRange(0, CoreBoardingEnemyState.TidePowerLimit));
            Assert.That(state.RetainedGuard, Is.InRange(0, guard));
            Assert.That(state.BloodLoss, Is.InRange(0, CoreBoardingEnemyState.BloodLossLimit));
        }

        private static string Describe(IEnumerable<CoreBoardingEnemyActionResult> actions) => string.Join(";", actions.Select(action =>
            action.CardId + ":" + string.Join(",", typeof(CoreBoardingEnemyActionResult).GetProperties()
                .Where(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(string))
                .OrderBy(property => property.Name).Select(property => property.Name + "=" + property.GetValue(action))) +
            ":" + Resources(action.ResourcesAfter)));

        private static string Resources(CoreBoardingEnemyState state) => string.Join(",", typeof(CoreBoardingEnemyState).GetProperties()
            .Where(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(bool))
            .OrderBy(property => property.Name).Select(property => property.Name + "=" + property.GetValue(state)));

        private static string Snapshot(CoreVoyageSession session) => string.Join("|", new object[] {
            Resources(session.Battle.BoardingEnemy), Describe(session.Battle.BoardingEnemy.LastActions),
            session.Battle.PhaseTurn, session.Battle.Turn, session.Battle.PlayerBoardingHealth,
            session.Battle.EnemyBoardingHealth, session.Battle.PlayerBlock, session.Battle.EnemyBlock
        });

        private static CoreVoyageSession Battle(string id)
        {
            CoreEncounter encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.Find(id).CreateEncounter();
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("BOARDING-BOSS-PACING");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, true });
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.DistanceMeters = 100f;
            SetProperty(session.Battle, "EnemyHeadingDegrees", 90f);
            SetProperty(session.Battle, "PlayerHeadingDegrees", session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            // Preserve each boss's real damage and guard baselines; only remove fixture deaths.
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 100000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 100000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Cargo.Curse = 0;
            SetProperty(session, "Morale", 0);
            return session;
        }

        private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
