using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class StagedNavalBattleTests
    {
        private static readonly NavalBattleStage[] Stages =
        {
            NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding,
        };

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void StageDeck_ValidatesSizeCopiesAndMembershipWithoutReplacingValidDeck(NavalBattleStage stage)
        {
            CoreVoyageSession session = PortSession();
            List<int> original = session.GetBattleDeck(stage).ToList();
            Assert.That(original.Count, Is.InRange(10, 20));
            Assert.That(original.All(index => session.GetAvailableBattleCards(stage).Contains(index) ||
                stage == NavalBattleStage.Boarding && CoreBoardingCardCatalog.IsLegacyAlias(CoreVoyageSession.CardLibrary[index]) &&
                CoreVoyageSession.CardLibrary[index].IsAllowedInStage(stage) && session.IsBattleCardUnlocked(CoreVoyageSession.CardLibrary[index])), Is.True,
                "Historic starter slots remain usable aliases even though new construction offers canonical boarding cards.");
            Assert.That(original.GroupBy(index => index).All(group => group.Count() <= 2), Is.True);
            Assert.That(session.SetBattleDeck(stage, original), Is.True);

            Assert.That(session.SetBattleDeck(stage, original.Take(9).ToList()), Is.False);
            Assert.That(session.SetBattleDeck(stage, original.Concat(original).Concat(original).Take(21).ToList()), Is.False);
            List<int> threeCopies = Enumerable.Repeat(original[0], 3)
                .Concat(original.Where(index => index != original[0]).Take(7)).ToList();
            Assert.That(threeCopies.Count, Is.EqualTo(10), "The fixture must reject duplicate count independently of deck size.");
            Assert.That(session.SetBattleDeck(stage, threeCopies), Is.False);
            List<int> invalidCard = original.ToList();
            invalidCard[0] = CoreVoyageSession.CardLibrary.Count;
            Assert.That(session.SetBattleDeck(stage, invalidCard), Is.False);
            Assert.That(session.GetBattleDeck(stage), Is.EqualTo(original));
        }

        [Test]
        public void DeckEdits_RejectWrongStageAndSpecialistCardsAndArePortOnly()
        {
            CoreVoyageSession session = PortSession();
            List<int> original = session.GetBattleDeck(NavalBattleStage.Preparation).ToList();
            List<int> changed = original.ToList();
            changed[0] = CardIndex("card_cutlass_raid");
            Assert.That(session.SetBattleDeck(NavalBattleStage.Preparation, changed), Is.False);

            session.ConfigureSpecialCardSources(new[] { "crew_twilight_tide_gunner_yilan" }, null);
            changed[0] = CardIndex("card_special_crew_twilight_tide_gunner_yilan");
            Assert.That(session.SetBattleDeck(NavalBattleStage.Preparation, changed), Is.False,
                "Carried specialists contribute cards automatically, outside the editable base deck.");
            Assert.That(session.GetBattleDeck(NavalBattleStage.Preparation), Is.EqualTo(original));

            session.StartVoyage();
            foreach (NavalBattleStage stage in Stages)
            {
                List<int> deck = session.GetBattleDeck(stage).ToList();
                Assert.That(session.SetBattleDeck(stage, deck), Is.False);
                Assert.That(session.GetBattleDeck(stage), Is.EqualTo(deck));
            }
        }

        [Test]
        public void Preparation_UsesStrategyDeckAndEnemySetupDoesNotDamageEitherHull()
        {
            CoreVoyageSession session = StartBattle();
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            AssertFreshStageDeck(session, NavalBattleStage.Preparation);
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            int turn = session.Battle.Turn;

            SetHand(session, "card_chain_shot");
            AssertRejectedCardLeavesResourcesUntouched(session, 0);
            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.Turn, Is.GreaterThan(turn));
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(session.Battle.Energy, Is.EqualTo(session.Battle.MaxEnergy));
            AssertFreshStageDeck(session, NavalBattleStage.Naval);
        }

        [Test]
        public void Preparation_LoadingAndDeckPreparationPersistIntoNavalCombat()
        {
            CoreVoyageSession session = StartBattle();
            SetHand(session, "card_load_shot", "card_deck_polish");
            int enemyHull = session.Battle.EnemyHull;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            int loadedShot = session.Battle.PlayerLoadedShot;
            int deckBuff = session.Battle.PlayerDeckBuff;
            Assert.That(loadedShot, Is.GreaterThan(0));
            Assert.That(deckBuff, Is.GreaterThan(0));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));

            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(loadedShot));
            Assert.That(session.Battle.PlayerDeckBuff, Is.EqualTo(deckBuff));
            AssertFreshStageDeck(session, NavalBattleStage.Naval);
        }

        [Test]
        public void Maneuvers_GrantPointsWithoutChangingPoseUntilAPlannedMoveCommits()
        {
            CoreVoyageSession session = StartNavalBattle();
            Pose(session, -300f, 0f, 0f, 0f, 0f, 180f);
            SetHand(session, "card_approach", "card_circle_enemy", "card_parallel_course");
            int points = session.Battle.NavalActionPoints;
            session.Battle.Energy = 10;
            for (int card = 0; card < 3; card++)
            {
                Assert.That(session.PlayCard(0), Is.True);
                Assert.That(session.Battle.DistanceMeters, Is.EqualTo(300f));
                Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f));
                Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + card + 1));
            }
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(-200f, 0f), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(200f));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(session.ConfirmNavalArrivalHeading(90f, out reason), Is.True, reason);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(90f));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
        }

        private static IEnumerable<TestCaseData> ApproachingCardCases()
        {
            foreach (string id in new[] { "card_approach", "card_full_sail_turn", "card_circle_enemy", "card_parallel_course", "card_navigator_course" })
                foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval })
                    foreach (int upgrade in new[] { 0, 1 })
                        yield return new TestCaseData(id, stage, upgrade);
        }

        [TestCaseSource(nameof(ApproachingCardCases))]
        public void ManeuverCardsPreservePoseAndGrantAuthoredPointsInBothShipStages(string id, NavalBattleStage stage, int upgrade)
        {
            CoreVoyageSession session = StartBattle(configure: value =>
            {
                value.ConfigureCrewCapabilities(true, false, false, false, false, false);
                value.ConfigureCardUpgrades(new Dictionary<string, int> { { id, upgrade } });
            });
            if (stage == NavalBattleStage.Naval) Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DistanceMeters = 40f;
            int points = session.Battle.NavalActionPoints;
            int gain = upgrade == 0 ? 1 : 2;
            session.Battle.Energy = 10;
            SetHand(session, id);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(40f));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + gain));
            Assert.That(session.Battle.Stage, Is.EqualTo(stage), "Approaching never initiates boarding.");

            SetHand(session, id);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(40f), "A repeated maneuver only grants a future movement point.");
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points + 2 * gain));
        }

        [TestCase(0f)]
        [TestCase(-10f)]
        public void ApproachingAnInvalidRangeDoesNotPushTheShipsApartOrEnableRam(float distance)
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = distance;
            SetHand(session, "card_approach");
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(Math.Max(0f, distance)));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.CanRam(out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("冲撞需要与敌舰上下左右相邻（100 米）"));
        }

        [Test]
        public void ZeroRangeCannotBeRepairedByPlayingMovementCardsWithoutAPlannedRoute()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = 0f;
            SetHand(session, "card_retreat", "card_approach");
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.Zero);
            Assert.That(session.Battle.PlayerMovingTowardEnemy, Is.False);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3));
            Assert.That(session.CanRam(out string reason), Is.False);
            Assert.That(reason, Does.Contain("上下左右相邻"));
        }

        [TestCase(NavalBattleStage.Preparation, 1, 100f, CoreBattleArchetype.Balanced, 0f)]
        [TestCase(NavalBattleStage.Preparation, 1, 300f, CoreBattleArchetype.Balanced, 0f)]
        [TestCase(NavalBattleStage.Preparation, 1, 600f, CoreBattleArchetype.Balanced, 0f)]
        [TestCase(NavalBattleStage.Naval, 2, 100f, CoreBattleArchetype.Balanced, 0f)]
        [TestCase(NavalBattleStage.Naval, 2, 200f, CoreBattleArchetype.Balanced, 100f)]
        [TestCase(NavalBattleStage.Naval, 2, 600f, CoreBattleArchetype.Balanced, 200f)]
        [TestCase(NavalBattleStage.Naval, 1, 100f, CoreBattleArchetype.Balanced, 0f)]
        [TestCase(NavalBattleStage.Naval, 1, 300f, CoreBattleArchetype.Balanced, 100f)]
        [TestCase(NavalBattleStage.Naval, 1, 400f, CoreBattleArchetype.Tempest, 100f)]
        [TestCase(NavalBattleStage.Naval, 1, 600f, CoreBattleArchetype.Tempest, 200f)]
        public void EnemyApproachUsesWholeCellsAndStopsBeforeAnOccupiedCell(
            NavalBattleStage stage, int phaseTurn, float distance, CoreBattleArchetype archetype, float movement)
        {
            CoreVoyageSession session = StartBattle();
            if (stage == NavalBattleStage.Naval) Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, archetype, 70, 2, 2, 3, 0, 8, 1f, 1f);
            Pose(session, -distance, 0f, 0f, 0f, 0f, 180f);
            session.Battle.PhaseTurn = phaseTurn;
            string intent = session.GetEnemyIntent();
            if (stage == NavalBattleStage.Naval)
                Assert.That(intent, Does.Contain("整格机动，最多 " + (distance > 400f ? 200 : 100) + " 米"));
            else Assert.That(intent, Does.Not.Contain("机动"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(distance - movement));
            Assert.That(session.Battle.DistanceMeters, Is.GreaterThanOrEqualTo(100f));
            Assert.That(session.Battle.EnemyPositionMeters.X % 100f, Is.EqualTo(0f));
            Assert.That(session.Battle.EnemyPositionMeters.Y % 100f, Is.EqualTo(0f));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
        }

        [TestCase(101f, 0f, 90f, 1)]
        [TestCase(0f, 0f, 90f, 1)]
        [TestCase(-10f, 0f, 90f, 1)]
        [TestCase(30f, 0f, 90f, 1)]
        [TestCase(60f, 0f, 90f, 1)]
        [TestCase(100f, 180f, 90f, 1)]
        [TestCase(100f, 90f, 0f, 1)]
        [TestCase(100f, 0f, 90f, 0)]
        public void Ram_RejectsMissingAdjacentCellCorrectHeadingsOrActionPointWithoutSpending(
            float distance, float playerHeading, float enemyHeading, int points)
        {
            CoreVoyageSession session = StartNavalBattle();
            Pose(session, -Math.Max(0f, distance), 0f, playerHeading, 0f, 0f, enemyHeading);
            session.Battle.PlayerMovingTowardEnemy = true;
            SetPrivateProperty(session.Battle, "NavalActionPoints", points);
            int energy = session.Battle.Energy;
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            int morale = session.Morale;
            int[] hand = session.Battle.Hand.ToArray();

            Assert.That(session.CanRam(out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(session.TryRam(), Is.False);
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Morale, Is.EqualTo(morale));
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(Math.Max(0f, distance)));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.Hand, Is.EqualTo(hand));
            Assert.That(session.Battle.PlayerMovingTowardEnemy, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points));
        }

        [TestCase(NavalBattleAlignment.HeadOn, 100f)]
        [TestCase(NavalBattleAlignment.EnemyBroadside, 100f)]
        public void Ram_AtRangeBoundaryDamagesBothHullsAndStartsBoarding(NavalBattleAlignment alignment, float distance)
        {
            CoreVoyageSession session = StartNavalBattle();
            ArrangeRam(session, alignment);
            session.Battle.DistanceMeters = distance;
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            Assert.That(session.CanRam(out string reason), Is.True, reason);

            Assert.That(session.TryRam(), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Hull, Is.LessThan(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.LessThan(enemyHull));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.Energy, Is.EqualTo(session.Battle.MaxEnergy),
                "Entering melee refreshes the action points for the boarding deck.");
            Assert.That(session.Battle.PlayerMovingTowardEnemy, Is.False);
            Assert.That(session.CanRam(out string reasonAfterRam), Is.False);
            Assert.That(reasonAfterRam, Does.Contain("炮战"));
        }

        [Test]
        public void Ram_UsesActualBroadsideArmorRegardlessOfLegacyAlignmentMetadata()
        {
            int headOnBaseline = RamDamage(NavalBattleAlignment.HeadOn, 1f, 1f);
            Assert.That(RamDamage(NavalBattleAlignment.HeadOn, 5f, 1f), Is.EqualTo(headOnBaseline));
            Assert.That(RamDamage(NavalBattleAlignment.HeadOn, 1f, 5f), Is.LessThan(headOnBaseline));

            int broadsideBaseline = RamDamage(NavalBattleAlignment.EnemyBroadside, 1f, 1f);
            Assert.That(RamDamage(NavalBattleAlignment.EnemyBroadside, 1f, 5f), Is.LessThan(broadsideBaseline));
            Assert.That(RamDamage(NavalBattleAlignment.EnemyBroadside, 5f, 1f), Is.EqualTo(broadsideBaseline));
        }

        [Test]
        public void Ram_SinkingEnemyBeforeBoardingEndsBattleAndCannotPayAgain()
        {
            CoreVoyageSession session = StartNavalBattle();
            ArrangeRam(session, NavalBattleAlignment.EnemyBroadside);
            session.Battle.EnemyHull = 1;
            int gold = session.Cargo.Gold;

            Assert.That(session.TryRam(), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Hull, Is.GreaterThan(0));
            Assert.That(session.Cargo.Gold, Is.GreaterThan(gold));
            int paidGold = session.Cargo.Gold;
            Assert.That(session.TryRam(), Is.False);
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(paidGold));
        }

        [Test]
        public void Ram_MutualSinkingResolvesAsFailureWithoutVictoryLoot()
        {
            CoreVoyageSession session = StartNavalBattle();
            ArrangeRam(session, NavalBattleAlignment.HeadOn);
            SetPrivateProperty(session, "Hull", 1);
            session.Battle.EnemyHull = 1;

            Assert.That(session.TryRam(), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedGold, Is.Zero);
            Assert.That(session.Battle, Is.Null);
        }

        [TestCase(61f, NavalBattleAlignment.Parallel)]
        [TestCase(50f, NavalBattleAlignment.HeadOn)]
        [TestCase(50f, NavalBattleAlignment.EnemyBroadside)]
        public void Boarding_RequiresCloseParallelShipsWithoutSpendingOnRejection(
            float distance, NavalBattleAlignment alignment)
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = distance;
            session.Battle.Alignment = alignment;
            int energy = session.Battle.Energy;
            int[] hand = session.Battle.Hand.ToArray();

            Assert.That(session.CanBoard(out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(session.TryBoard(), Is.False);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Hand, Is.EqualTo(hand));
        }

        [TestCase(0f)]
        [TestCase(90f)]
        public void Boarding_AtCardinalNeighborReplacesAllPilesAndRestoresEnergy(float heading)
        {
            CoreVoyageSession session = StartNavalBattle();
            Pose(session, heading == 0f ? 0f : -100f, heading == 0f ? -100f : 0f, heading, 0f, 0f, heading);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.Energy = 0;
            session.Battle.DiscardPile.Add(CardIndex("card_chain_shot"));

            Assert.That(session.CanBoard(out string reason), Is.True, reason);
            Assert.That(session.TryBoard(), Is.True);

            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.GreaterThan(0));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(session.Battle.EnemyMaxBoardingHealth));
            Assert.That(session.Battle.Energy, Is.EqualTo(session.Battle.MaxEnergy));
            AssertFreshStageDeck(session, NavalBattleStage.Boarding);
            Assert.That(session.CanRam(out _), Is.False);
            Assert.That(session.TryBoard(), Is.False);
        }

        [Test]
        public void BoardingHealthCarriesNavalHullDamageAtHalfRateAndRoundsUp()
        {
            CoreVoyageSession session = StartNavalBattle();
            int playerMaxHull = session.MaxHull;
            int enemyMaxHull = session.Battle.EnemyMaxHull;
            int playerDamage = playerMaxHull / 2;
            int enemyDamage = enemyMaxHull / 2;
            SetPrivateProperty(session, "Hull", playerMaxHull - playerDamage);
            session.Battle.EnemyHull = enemyMaxHull - enemyDamage;
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;

            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(
                ExpectedBoardingHealth(session.Battle.PlayerMaxBoardingHealth, playerMaxHull, playerDamage)));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(
                ExpectedBoardingHealth(session.Battle.EnemyMaxBoardingHealth, enemyMaxHull, enemyDamage)));
        }

        [Test]
        public void Boarding_MeleeAndEnemyRetaliationUseCrewHealthInsteadOfShipHull()
        {
            CoreVoyageSession session = StartBoardingBattle();
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            int playerHealth = session.Battle.PlayerBoardingHealth;
            int enemyHealth = session.Battle.EnemyBoardingHealth;
            session.Battle.EnemyBlock = 0;
            session.Battle.PlayerBlock = 0;
            SetHand(session, "card_crew_thrust");

            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.LessThan(enemyHealth));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            for (int turn = 0; turn < 3 && session.Battle.PlayerBoardingHealth == playerHealth; turn++)
                Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.LessThan(playerHealth));
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
        }

        [Test]
        public void Boarding_CannonsAreUnavailableAndCrewDefeatWinsWithEnemyHullStillAfloat()
        {
            CoreVoyageSession session = StartBoardingBattle();
            SetHand(session, "card_chain_shot");
            AssertRejectedCardLeavesResourcesUntouched(session, 0);
            session.Battle.EnemyBoardingHealth = 1;
            session.Battle.EnemyBlock = 0;
            CoreBattleState battle = session.Battle;
            int enemyHull = battle.EnemyHull;
            SetHand(session, "card_crew_thrust");

            Assert.That(session.PlayCard(0), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
            Assert.That(battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(battle.EnemyHull, Is.GreaterThan(0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void OnlyCarriedCapableSpecialistsAddCardsAndRemovingThemRemovesTheirCards(int career)
        {
            CoreVoyageSession session = PortSession();
            Assert.That(Stages.SelectMany(session.GetCrewBattleCards), Is.Empty);
            session.ConfigureCrewCapabilities(career == 0, career == 1, career == 2,
                career == 3, career == 4, career == 5);
            if (career == 6) session.ConfigureSpecialCardSources(new[] { "crew_twilight_tide_gunner_yilan" }, null);
            int[] contributed = Stages.SelectMany(session.GetCrewBattleCards).ToArray();
            Assert.That(contributed, Is.Not.Empty);
            foreach (NavalBattleStage stage in Stages)
            {
                IReadOnlyList<int> crewCards = session.GetCrewBattleCards(stage);
                Assert.That(crewCards.Distinct().Count(), Is.EqualTo(crewCards.Count));
                Assert.That(crewCards.Intersect(session.GetBattleDeck(stage)), Is.Empty);
            }

            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureSpecialCardSources(null, null);
            Assert.That(Stages.SelectMany(session.GetCrewBattleCards), Is.Empty);
        }

        [Test]
        public void OrdinaryCannonDamageDoesNotReceiveAnInvisibleGunnerPassiveBonus()
        {
            CoreVoyageSession plain = StartNavalBattle();
            CoreVoyageSession gunner = StartBattle(configure: session =>
                session.ConfigureCrewCapabilities(false, false, false, true, false, false));
            Assert.That(gunner.EndBattleTurn(), Is.True);
            int plainDamage = FireOrdinaryCannon(plain);
            int gunnerDamage = FireOrdinaryCannon(gunner);
            Assert.That(plainDamage, Is.GreaterThan(0));
            Assert.That(gunnerDamage, Is.EqualTo(plainDamage),
                "Specialist advantages must come from their contributed cards.");
        }

        [Test]
        public void OrdinaryCannon_RejectsInsufficientAmmunitionAndWrongRangeWithoutSpending()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.DistanceMeters = 200f;
            SetHand(session, "card_cannon_barrage");
            session.Cargo.Powder = 0;
            AssertRejectedCardLeavesResourcesUntouched(session, 0);
            session.Cargo.Powder = 10;
            session.Cargo.Cannonballs = 0;
            AssertRejectedCardLeavesResourcesUntouched(session, 0);
            session.Cargo.Cannonballs = 10;
            session.Battle.DistanceMeters = 10000f;
            AssertRejectedCardLeavesResourcesUntouched(session, 0);
        }

        [Test]
        public void InvalidHandIndexesAndInsufficientCardEnergyLeavePilesUntouched()
        {
            CoreVoyageSession session = StartBattle();
            AssertRejectedCardLeavesResourcesUntouched(session, -1);
            AssertRejectedCardLeavesResourcesUntouched(session, session.Battle.Hand.Count);
            SetHand(session, "card_load_shot");
            session.Battle.Energy = 0;
            AssertRejectedCardLeavesResourcesUntouched(session, 0);
        }

        [Test]
        public void YilanCard_RequiresCarriedGunnerAndItsFireRoundsCauseDelayedHullDamage()
        {
            CoreVoyageSession missingGunner = StartBattle();
            SetHand(missingGunner, "card_special_crew_twilight_tide_gunner_yilan");
            AssertRejectedCardLeavesResourcesUntouched(missingGunner, 0);

            CoreVoyageSession session = StartBattle(configure: value => value.ConfigureSpecialCardSources(new[] { "crew_twilight_tide_gunner_yilan" }, null));
            AssertFreshStageDeck(session, NavalBattleStage.Preparation);
            Assert.That(session.GetCrewBattleCards(NavalBattleStage.Preparation),
                Does.Contain(CardIndex("card_special_crew_twilight_tide_gunner_yilan")));
            SetHand(session, "card_special_crew_twilight_tide_gunner_yilan");
            int beforeCasting = session.Battle.EnemyHull;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(beforeCasting));
            int fireRounds = session.Battle.PlayerFireRounds;
            Assert.That(fireRounds, Is.GreaterThan(0));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(fireRounds));

            Assert.That(FireOrdinaryCannon(session), Is.GreaterThan(0));
            Assert.That(session.Battle.PlayerFireRounds, Is.LessThan(fireRounds));
            Assert.That(session.Battle.EnemyBurnTurns, Is.GreaterThan(0));
            int hullBeforeBurn = session.Battle.EnemyHull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.EnemyHull, Is.LessThan(hullBeforeBurn));
        }

        [Test]
        public void EnemyGuardAbsorption_RecordsOnlyTheActualHit_NotExpiredGuard_AndResetsOnNonAttackingTurn()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.PhaseTurn = 1;
            session.Battle.EnemyLoadedShot = 0;
            session.Battle.PlayerBlock = 20;
            int hullBefore = session.Hull;
            int expectedDamage = Math.Max(1, session.Battle.Profile.CannonDamage + session.Battle.PhaseTurn / 4 +
                session.Battle.EnemyLoadedShot - Math.Max(0, (session.EffectiveArmor - 4) / 2));
            Assert.That(expectedDamage, Is.LessThan(20), "This regression needs leftover guard that expires after absorbing the actual hit.");
            Assert.That(session.GetEnemyIntent(), Does.Contain("炮击：伤害 " + expectedDamage));

            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.Hull, Is.EqualTo(hullBefore));
            Assert.That(session.Battle.PlayerBlock, Is.Zero, "Unused guard expires at the end of the turn.");
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(expectedDamage),
                "Only the configured cannon damage is absorbed; leftover guard expires without hit/armor-break feedback.");
            Assert.That(session.Battle.PhaseTurn, Is.EqualTo(2));

            session.Battle.PlayerBlock = 11;
            Assert.That(session.GetEnemyIntent(), Does.Contain("防御"));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.Zero,
                "A non-attacking turn cannot replay a previous hit or mistake expiring guard for damage.");
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.Hull, Is.EqualTo(hullBefore));
        }

        [Test]
        public void EnemyGuardAbsorption_ResetsOnGuardTurnBeforeLethalEndOfTurnBurn()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.PhaseTurn = 1;
            session.Battle.EnemyLoadedShot = 0;
            session.Battle.PlayerBlock = 20;
            int expectedDamage = Math.Max(1, session.Battle.Profile.CannonDamage + session.Battle.PhaseTurn / 4 +
                session.Battle.EnemyLoadedShot - Math.Max(0, (session.EffectiveArmor - 4) / 2));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(Math.Min(20, expectedDamage)));

            CoreBattleState battle = session.Battle;
            battle.EnemyHull = 5;
            battle.EnemyBurnDamage = battle.EnemyBurnTurns = 5;
            battle.PlayerBlock = 12;
            Assert.That(session.GetEnemyIntent(), Does.Contain("防御"));
            int playerHullBefore = session.Hull;
            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
            Assert.That(battle.EnemyHull, Is.Zero);
            Assert.That(battle.LastEnemyGuardAbsorbed, Is.Zero,
                "The final guard turn cannot retain the previous attack's absorption after burn settles.");
            Assert.That(battle.LastAction, Is.EqualTo("敌舰沉没。"), "Victory replaces the action text with its terminal outcome.");
            Assert.That(session.Hull, Is.EqualTo(playerHullBefore));
        }

        [Test]
        public void FullBattle_WithSameSeedAndDecisionsHasSameVictoryWithinSixtyTurns()
        {
            string first = PlayFullBattle("STAGED-SMOKE-71");
            string second = PlayFullBattle("STAGED-SMOKE-71");
            Assert.That(second, Is.EqualTo(first));
        }

        private static string PlayFullBattle(string seed)
        {
            CoreVoyageSession session = StartBattle(seed, configure: value => value.SelectShip(2));
            int turns = 0;
            int played = 0;
            while (session.Phase == CoreDemoPhase.Battle && turns < 60)
            {
                for (int action = 0; action < 30 && session.Phase == CoreDemoPhase.Battle; action++)
                {
                    int candidate = Enumerable.Range(0, session.Battle.Hand.Count)
                        .Where(session.CanPlayCard)
                        .OrderByDescending(index => CoreVoyageSession.CardLibrary[session.Battle.Hand[index]].Damage)
                        .DefaultIfEmpty(-1).First();
                    if (candidate < 0) break;
                    Assert.That(session.PlayCard(candidate), Is.True);
                    played++;
                }
                if (session.Phase == CoreDemoPhase.Battle) Assert.That(session.EndBattleTurn(), Is.True);
                turns++;
            }
            Assert.That(played, Is.GreaterThan(0));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing),
                "The deterministic baseline captain must finish a normal enemy, without an unbounded combat loop.");
            Assert.That(session.Battle, Is.Null);
            return turns + ":" + played + ":" + session.Hull + ":" + session.Cargo.Gold + ":" + session.Cargo.Powder;
        }

        private static CoreVoyageSession PortSession()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            return session;
        }

        private static CoreVoyageSession StartBattle(string seed = "STAGED-BATTLE-TEST", Action<CoreVoyageSession> configure = null)
        {
            CoreVoyageSession session = PortSession();
            configure?.Invoke(session);
            session.SetSeed(seed);
            session.StartVoyage();
            CoreEncounter encounter = new CoreEncounter("staged_test", "测试敌舰", "海战", "阶段规则测试", true, false, 1, 70);
            MethodInfo method = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(session, new object[] { encounter, false });
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            return session;
        }

        private static CoreVoyageSession StartNavalBattle()
        {
            CoreVoyageSession session = StartBattle();
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            return session;
        }

        private static CoreVoyageSession StartBoardingBattle()
        {
            CoreVoyageSession session = StartNavalBattle();
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            return session;
        }

        private static void ArrangeRam(CoreVoyageSession session, NavalBattleAlignment alignment)
        {
            Pose(session, -100f, 0f, 0f, 0f, 0f, 90f);
            session.Battle.Alignment = alignment;
            session.Battle.PlayerMovingTowardEnemy = true;
            session.Battle.EnemyBlock = 0;
        }

        private static void Pose(CoreVoyageSession session, float px, float py, float ph, float ex, float ey, float eh)
        {
            MethodInfo method = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(session.Battle, new object[] { true, new VoyagePoint(px, py), ph });
            method.Invoke(session.Battle, new object[] { false, new VoyagePoint(ex, ey), eh });
        }

        private static int RamDamage(NavalBattleAlignment alignment, float enemyBowQuality, float enemyHullQuality)
        {
            CoreVoyageSession session = StartBattle(configure: value => value.ConfigureBattleEquipment(2f, 2f));
            Assert.That(session.Battle.PlayerBowQuality, Is.EqualTo(2f));
            Assert.That(session.Battle.PlayerHullQuality, Is.EqualTo(2f));
            Assert.That(session.EndBattleTurn(), Is.True);
            ArrangeRam(session, alignment);
            session.Battle.EnemyBowQuality = enemyBowQuality;
            session.Battle.EnemyHullQuality = enemyHullQuality;
            session.Battle.EnemyHull = 200;
            session.Battle.EnemyMaxHull = 200;
            Assert.That(session.TryRam(), Is.True);
            Assert.That(session.Battle, Is.Not.Null);
            return 200 - session.Battle.EnemyHull;
        }

        private static void AssertFreshStageDeck(CoreVoyageSession session, NavalBattleStage stage)
        {
            int[] expected = session.GetBattleDeck(stage).Concat(session.GetCrewBattleCards(stage)).ToArray();
            int[] actual = session.Battle.Hand.Concat(session.Battle.DrawPile).Concat(session.Battle.DiscardPile).ToArray();
            CollectionAssert.AreEquivalent(expected, actual,
                "Changing stage must replace every old pile and deal only the newly selected deck and carried crew cards.");
            Assert.That(session.Battle.Hand, Is.Not.Empty);
            Assert.That(session.Battle.DiscardPile, Is.Empty);
        }

        private static void AssertRejectedCardLeavesResourcesUntouched(CoreVoyageSession session, int index)
        {
            int powder = session.Cargo.Powder;
            int cannonballs = session.Cargo.Cannonballs;
            int playerHull = session.Hull;
            int enemyHull = session.Battle.EnemyHull;
            int energy = session.Battle.Energy;
            int[] hand = session.Battle.Hand.ToArray();
            int[] drawPile = session.Battle.DrawPile.ToArray();
            int[] discardPile = session.Battle.DiscardPile.ToArray();
            Assert.That(session.CanPlayCard(index), Is.False);
            Assert.That(session.CardUnavailableReason(index), Is.Not.Empty);
            Assert.That(session.PlayCard(index), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(cannonballs));
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(session.Battle.Hand, Is.EqualTo(hand));
            Assert.That(session.Battle.DrawPile, Is.EqualTo(drawPile));
            Assert.That(session.Battle.DiscardPile, Is.EqualTo(discardPile));
        }

        private static int FireOrdinaryCannon(CoreVoyageSession session)
        {
            session.Battle.DistanceMeters = 200f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.EnemyBlock = 0;
            SetHand(session, "card_chain_shot");
            int hull = session.Battle.EnemyHull;
            Assert.That(session.PlayCard(0), Is.True);
            return hull - session.Battle.EnemyHull;
        }

        private static int CardIndex(string id)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Where(value => CoreVoyageSession.CardLibrary[value].Id == id).DefaultIfEmpty(-1).First();
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Missing battle card " + id);
            return index;
        }

        private static void SetHand(CoreVoyageSession session, params string[] cardIds)
        {
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(cardIds.Select(CardIndex));
        }

        private static void SetPrivateProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            property.SetValue(target, value);
        }

        private static int ExpectedBoardingHealth(int maxBoardingHealth, int maxHull, int damage)
        {
            return (int)Math.Ceiling(maxBoardingHealth * (1d - Math.Max(0, damage) / (2d * maxHull)));
        }
    }
}
