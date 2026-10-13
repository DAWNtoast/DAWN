using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTrainingBattleContinuityTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void StartPreservesWoundsSuppliesAndConfiguredCrewMaximum(bool fromSailing)
        {
            CoreVoyageSession s = Session(fromSailing);
            SetHull(s, s.MaxHull - 17);
            s.Cargo.Powder = 75; s.Cargo.Cannonballs = 120; s.Cargo.Wood = 54; s.Cargo.Medicine = 30;
            int maxCrew = CrewMaximum(s), retained = maxCrew - 11, hull = s.Hull;
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0], retained, "bout-1"), Is.True);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(retained));
            Assert.That(s.Battle.PlayerMaxBoardingHealth, Is.EqualTo(maxCrew));
            Assert.That(s.Battle.TrainingStartingCrewHealth, Is.EqualTo(retained));
            Assert.That(s.Battle.TrainingBoardingCarryover, Is.Zero);
            Assert.That(s.Hull, Is.EqualTo(hull));
            Assert.That(s.Battle.NavalStartPlayerHull, Is.EqualTo(hull));
            Assert.That(Supplies(s), Is.EqualTo(new[] { 75, 120, 54, 30 }));
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            AssertRealDecks(s);
        }

        [Test]
        public void NavalVictoryAndNextBoutKeepLossesButResetPerBattleState()
        {
            CoreVoyageSession s = Session();
            int retained = CrewMaximum(s) - 19;
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0], retained, "first"), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True); // Real target attack produces this bout's hull loss.
            Put(s, "card_chain_shot");
            int cardIndex = s.Battle.Hand[0];
            int baseCost = s.GetBattleCardEnergyCost(cardIndex);
            int[] beforeSupplies = Supplies(s);
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Cargo.Powder, Is.EqualTo(beforeSupplies[0] - 1));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(beforeSupplies[1] - 1));
            Assert.That(s.GetBattleCardEnergyCost(cardIndex), Is.EqualTo(baseCost + 1));
            // Distinct state fixtures ensure the next bout creates a fresh school state.
            SetSchool(s, "Mana", 2); SetSchool(s, "ManaLimit", 5);
            SetSchool(s, "TemporaryMana", 1); SetSchool(s, "ThunderChain", 2);
            SetSchool(s, "TidePower", 7); SetSchool(s, "CurseMarks", 3);
            CoreBattleState previous = s.Battle;
            int hull = s.Hull, morale = s.Morale;
            int[] remainingSupplies = Supplies(s);
            WinByTerminalFixture(s);
            Assert.That(previous.PlayerBoardingHealth, Is.EqualTo(retained), "A naval-only win must not rebuild crew HP.");
            Assert.That(previous.TrainingBoardingCarryover, Is.Zero);
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[1], previous.PlayerBoardingHealth, "second"), Is.True);
            Assert.That(s.Battle, Is.Not.SameAs(previous));
            Assert.That(s.Battle.Schools, Is.Not.SameAs(previous.Schools));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(retained));
            Assert.That(s.Battle.PlayerMaxBoardingHealth, Is.EqualTo(CrewMaximum(s)));
            Assert.That(s.Hull, Is.EqualTo(hull));
            Assert.That(s.Morale, Is.EqualTo(morale));
            Assert.That(Supplies(s), Is.EqualTo(remainingSupplies));
            Assert.That(s.Battle.NavalStartPlayerHull, Is.EqualTo(hull));
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(3));
            Assert.That(new[] { s.Battle.Schools.Mana, s.Battle.Schools.TemporaryMana, s.Battle.Schools.ThunderChain,
                s.Battle.Schools.TidePower, s.Battle.Schools.CurseMarks }, Is.All.Zero);
            Assert.That(s.GetBattleCardEnergyCost(cardIndex), Is.EqualTo(baseCost));
            AssertRealDecks(s);
        }

        [TestCase(0)]
        [TestCase(31)]
        public void BoardingDeductsOnlyThisBoutsNavalDamage(int oldHullWound)
        {
            CoreVoyageSession s = Session();
            SetHull(s, s.MaxHull - oldHullWound);
            int startingHull = s.Hull, retained = CrewMaximum(s) - 20;
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0], retained, "new-damage"), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            int freshDamage = startingHull - s.Hull;
            Assert.That(freshDamage, Is.GreaterThan(0));
            int expectedCarry = (int)Math.Floor(CrewMaximum(s) * freshDamage / (2m * s.MaxHull));
            Board(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(retained - expectedCarry));
            Assert.That(s.Battle.TrainingBoardingCarryover, Is.EqualTo(expectedCarry));
            Assert.That(s.Battle.PlayerMaxBoardingHealth, Is.EqualTo(CrewMaximum(s)));
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.InProgress));
        }

        [Test]
        public void RepeatedBoardingWithoutNewHullDamageDoesNotChargeOldWoundsAgain()
        {
            CoreVoyageSession s = Session();
            SetHull(s, s.MaxHull - 25);
            int retained = CrewMaximum(s) - 19, hull = s.Hull;
            for (int bout = 0; bout < 2; bout++)
            {
                Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0], retained, "no-new-damage-" + bout), Is.True);
                Assert.That(s.EndBattleTurn(), Is.True); // Preparation never attacks.
                Board(s);
                Assert.That(s.Hull, Is.EqualTo(hull));
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(retained));
                Assert.That(s.Battle.TrainingBoardingCarryover, Is.Zero);
                WinByTerminalFixture(s);
            }
        }

        [TestCase("board")]
        [TestCase("ram")]
        [TestCase("enemy-ram")]
        public void CarryoverExhaustionImmediatelyEndsTrainingAndRejectsZeroHpContinuation(string entry)
        {
            CoreVoyageSession s = Session();
            Mulberry32Random originalRandom = RandomStream(s);
            uint randomState = originalRandom.State;
            int gold = s.Cargo.Gold, experience = s.PirateExperience;
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0], 1, "exhaust-" + entry), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            if (entry == "board")
            {
                Assert.That(s.EndBattleTurn(), Is.True);
                Board(s);
            }
            else
            {
                s.Battle.DistanceMeters = 50f;
                s.Battle.Alignment = NavalBattleAlignment.HeadOn;
                if (entry == "ram")
                {
                    s.Battle.PlayerMovingTowardEnemy = true;
                    Assert.That(s.TryRam(), Is.True);
                }
                else ExecuteEnemyRam(s);
            }
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.Zero);
            Assert.That(s.Battle.TrainingBoardingCarryover, Is.EqualTo(1), "Report only the crew HP actually lost.");
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(s.Battle.LastAction, Does.Not.Contain("敌方先手"), "The enemy must not continue its boarding first strike after terminal carryover.");
            Assert.That(s.Settlement, Is.Null);
            Assert.That(s.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(s.PirateExperience, Is.EqualTo(experience));
            Assert.That(RandomStream(s), Is.SameAs(originalRandom));
            Assert.That(originalRandom.State, Is.EqualTo(randomState));
            CoreBattleState terminal = s.Battle;
            Assert.That(s.EndBattleTurn(), Is.False);
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[1], 0, "invalid"), Is.False);
            Assert.That(s.Battle, Is.SameAs(terminal));
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
            Assert.That(RandomStream(s), Is.SameAs(originalRandom));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MaxValue)]
        public void InvalidCrewHealthIsRejectedWithoutChangingState(int crewHealth)
        {
            CoreVoyageSession s = Session();
            AssertRejectedWithoutMutation(s, () => s.StartTrainingBattle(CoreTrainingTargets.All[0], crewHealth, "rejected"));
        }

        [Test]
        public void NullTargetDestroyedHullAndActiveBattleRejectAtomically()
        {
            CoreVoyageSession s = Session();
            AssertRejectedWithoutMutation(s, () => s.StartTrainingBattle(null, CrewMaximum(s), "null"));
            SetHull(s, 0);
            AssertRejectedWithoutMutation(s, () => s.StartTrainingBattle(CoreTrainingTargets.All[0], CrewMaximum(s), "hull-zero"));
            SetHull(s, s.MaxHull);
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0], CrewMaximum(s), "active"), Is.True);
            AssertRejectedWithoutMutation(s, () => s.StartTrainingBattle(CoreTrainingTargets.All[1], CrewMaximum(s), "replace"));
        }

        [Test]
        public void SequenceKeyControlsBothRealDeckShuffleAndCriticalDecisionStream()
        {
            CoreVoyageSession a = Session(), repeat = Session(), different = Session();
            Assert.That(a.StartTrainingBattle(CoreTrainingTargets.All[0], CrewMaximum(a) - 7, "round-A"), Is.True);
            Assert.That(repeat.StartTrainingBattle(CoreTrainingTargets.All[0], CrewMaximum(repeat) - 7, "round-A"), Is.True);
            Assert.That(different.StartTrainingBattle(CoreTrainingTargets.All[0], CrewMaximum(different) - 7, "round-B"), Is.True);
            Assert.That(DeckOrder(a), Is.EqualTo(DeckOrder(repeat)));
            Assert.That(RandomStream(a).State, Is.EqualTo(RandomStream(repeat).State));
            Assert.That(a.Battle.DecisionSeed, Is.EqualTo(repeat.Battle.DecisionSeed));
            Assert.That(RandomStream(a).State, Is.Not.EqualTo(RandomStream(different).State));
            Assert.That(DeckOrder(a), Is.Not.EqualTo(DeckOrder(different)));
            Assert.That(a.Battle.DecisionSeed, Is.Not.EqualTo(different.Battle.DecisionSeed));
            a.EndBattleTurn(); repeat.EndBattleTurn(); different.EndBattleTurn();
            Board(a); Board(repeat); Board(different);
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Morale)).SetValue(a, 15);
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Morale)).SetValue(repeat, 15);
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Morale)).SetValue(different, 15);
            bool[] rolls = CriticalRolls(a);
            Assert.That(rolls, Is.EqualTo(CriticalRolls(repeat)));
            Assert.That(rolls, Is.Not.EqualTo(CriticalRolls(different)), "The explicit key must reach critical decisions as well as shuffle.");
        }

        [Test]
        public void LegacyEntryAndFullHealthContinuationWithoutKeyHaveIdenticalRandomStreams()
        {
            CoreVoyageSession legacy = Session(), continuation = Session();
            Assert.That(legacy.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(continuation.StartTrainingBattle(CoreTrainingTargets.All[0], CrewMaximum(continuation)), Is.True);
            Assert.That(legacy.Battle.TrainingStartingCrewHealth, Is.Null);
            Assert.That(legacy.Battle.PlayerBoardingHealth, Is.EqualTo(continuation.Battle.PlayerBoardingHealth));
            Assert.That(legacy.Battle.DecisionSeed, Is.EqualTo(continuation.Battle.DecisionSeed));
            Assert.That(DeckOrder(legacy), Is.EqualTo(DeckOrder(continuation)));
            Assert.That(RandomStream(legacy).State, Is.EqualTo(RandomStream(continuation).State));
            legacy.EndBattleTurn(); continuation.EndBattleTurn();
            legacy.EndBattleTurn(); continuation.EndBattleTurn();
            Board(legacy); Board(continuation);
            Assert.That(legacy.Battle.PlayerBoardingHealth, Is.EqualTo(continuation.Battle.PlayerBoardingHealth));
            Assert.That(legacy.Hull, Is.EqualTo(continuation.Hull));
            Assert.That(legacy.Battle.TrainingBoardingCarryover, Is.Zero, "The old entry does not enable continuation accounting.");
        }

        private static CoreVoyageSession Session(bool sailing = true)
        {
            var s = new CoreVoyageSession();
            s.ConfigureCrewCapabilities(false, false, false, false, false, false);
            s.SetSeed("TRAINING-CONTINUITY-TEST");
            if (sailing) s.StartVoyage();
            return s;
        }

        private static int CrewMaximum(CoreVoyageSession s) => CoreBattleBalance.BoardingHealthForOrdinaryCrew(s.SelectedOrdinaryCrew);
        private static int[] Supplies(CoreVoyageSession s) => new[] { s.Cargo.Powder, s.Cargo.Cannonballs, s.Cargo.Wood, s.Cargo.Medicine };
        private static void SetHull(CoreVoyageSession s, int hull) => typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Hull)).SetValue(s, hull);
        private static void SetSchool(CoreVoyageSession s, string name, int value) => typeof(CoreSchoolBattleState).GetProperty(name).SetValue(s.Battle.Schools, value);
        private static Mulberry32Random RandomStream(CoreVoyageSession s) => (Mulberry32Random)typeof(CoreVoyageSession)
            .GetField("voyageRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);

        private static void Put(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear();
            s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id));
        }

        private static void Board(CoreVoyageSession s)
        {
            s.Battle.DistanceMeters = 50f;
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
        }

        private static void WinByTerminalFixture(CoreVoyageSession s)
        {
            // Only the enemy terminal health is arranged: the real outcome path must preserve
            // all player wounds, cargo, morale and voyage RNG without rewards or restocking.
            if (s.Battle.Stage == NavalBattleStage.Boarding) s.Battle.EnemyBoardingHealth = 0;
            else s.Battle.EnemyHull = 0;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
        }

        private static IEnumerable<int> DrawPile(CoreVoyageSession s, NavalBattleStage stage) =>
            (IEnumerable<int>)typeof(CoreBattleState).GetMethod("DrawPileFor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { stage });

        private static int[] DeckOrder(CoreVoyageSession s) => s.Battle.Hand.Concat(Enum.GetValues(typeof(NavalBattleStage))
            .Cast<NavalBattleStage>().SelectMany(stage => DrawPile(s, stage))).ToArray();

        private static void AssertRealDecks(CoreVoyageSession s)
        {
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(DrawPile(s, stage).Concat(stage == NavalBattleStage.Preparation ? s.Battle.Hand : Enumerable.Empty<int>()).OrderBy(i => i),
                    Is.EqualTo(s.GetBattleDeck(stage).OrderBy(i => i)));
        }

        private static bool[] CriticalRolls(CoreVoyageSession s)
        {
            MethodInfo roll = typeof(CoreVoyageSession).GetMethod("RollBoardingCritical", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(s.BoardingCriticalChancePercent, Is.InRange(1, 99));
            return Enumerable.Range(0, 64).Select(_ => (bool)roll.Invoke(s, null)).ToArray();
        }

        private static void ExecuteEnemyRam(CoreVoyageSession s)
        {
            // Targets intentionally never choose ram; exercise the shared entry guard directly.
            Type planType = typeof(CoreVoyageSession).GetNestedType("EnemyBattlePlan", BindingFlags.NonPublic);
            Type actionType = typeof(CoreVoyageSession).GetNestedType("EnemyBattleAction", BindingFlags.NonPublic);
            object plan = Activator.CreateInstance(planType);
            planType.GetField("Action").SetValue(plan, Enum.Parse(actionType, "Ram"));
            planType.GetField("Attack").SetValue(plan, 9999);
            typeof(CoreVoyageSession).GetMethod("ExecuteEnemyPlan", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { plan, "test ram" });
        }

        private static void AssertRejectedWithoutMutation(CoreVoyageSession s, Func<bool> start)
        {
            CoreBattleState battle = s.Battle;
            CoreDemoPhase phase = s.Phase;
            CoreTrainingBattleOutcome outcome = s.TrainingOutcome;
            Mulberry32Random random = RandomStream(s);
            uint state = random.State;
            int hull = s.Hull, morale = s.Morale;
            int[] supplies = Supplies(s);
            Assert.That(start(), Is.False);
            Assert.That(s.Battle, Is.SameAs(battle));
            Assert.That(s.Phase, Is.EqualTo(phase));
            Assert.That(s.TrainingOutcome, Is.EqualTo(outcome));
            Assert.That(s.Hull, Is.EqualTo(hull));
            Assert.That(s.Morale, Is.EqualTo(morale));
            Assert.That(Supplies(s), Is.EqualTo(supplies));
            Assert.That(RandomStream(s), Is.SameAs(random));
            Assert.That(random.State, Is.EqualTo(state));
        }
    }
}
