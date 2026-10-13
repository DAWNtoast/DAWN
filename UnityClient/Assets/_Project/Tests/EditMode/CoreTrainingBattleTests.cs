using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTrainingBattleTests
    {
        [TestCase(0, 120, 80, 10, 8, 2, 0)]
        [TestCase(1, 220, 140, 16, 12, 5, 2)]
        [TestCase(2, 360, 240, 24, 20, 9, 4)]
        [TestCase(3, 600, 400, 36, 30, 14, 7)]
        [TestCase(4, 1000, 680, 50, 44, 20, 10)]
        public void FiveTargetsHaveExactFinalStatsAndPassivePreparation(int index, int hull, int crew,
            int cannon, int melee, int guard, int repair)
        {
            Assert.That(CoreTrainingTargets.All.Count, Is.EqualTo(5));
            CoreTrainingTarget target = CoreTrainingTargets.All[index];
            Assert.That(new[] { target.Hull, target.BoardingHealth, target.CannonDamage, target.MeleeDamage, target.Guard, target.Repair },
                Is.EqualTo(new[] { hull, crew, cannon, melee, guard, repair }));
            CoreVoyageSession s = Start(index);
            Assert.That(s.Battle.Profile.TrainingTarget, Is.SameAs(target));
            Assert.That(s.Battle.Profile.HealthMultiplier, Is.EqualTo(1m));
            Assert.That(s.Battle.Profile.BoardingHealthMultiplier, Is.EqualTo(1m));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyMaxHull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(crew));
            Assert.That(s.Battle.EnemyMaxBoardingHealth, Is.EqualTo(crew));
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(300f));
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.InProgress));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(5));
            Assert.That(s.Battle.Hand.All(i => s.GetBattleCard(i).IsAllowedInStage(NavalBattleStage.Preparation)), Is.True);
            int playerHull = s.Hull;
            s.Battle.EnemyHull -= 1;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(playerHull));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 1), "Preparation never heals.");
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(300f));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(guard));
            Assert.That(s.Battle.EnemyLoadedShot, Is.Zero);
            Assert.That(s.Battle.Hand.All(i => s.GetBattleCard(i).IsAllowedInStage(NavalBattleStage.Naval)), Is.True);
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, true)]
        public void AttacksAndRefreshedGuardNeverGrowAndRepairOnlyCurrentPool(int index, bool boarding)
        {
            CoreVoyageSession s = Start(index);
            s.EndBattleTurn();
            if (boarding) Board(s);
            CoreTrainingTarget target = CoreTrainingTargets.All[index];
            int damage = boarding ? target.MeleeDamage : Math.Max(1, target.CannonDamage - Math.Max(0, (s.EffectiveArmor - 4) / 2));
            foreach (int turn in new[] { 1, 2, 10, 40 })
            {
                s.Battle.PhaseTurn = turn;
                s.Battle.Turn = turn + 1;
                s.Battle.DistanceMeters = boarding ? 30f : 500f;
                s.Battle.EnemyBlock = 999;
                s.Battle.EnemyLoadedShot = 12;
                s.Battle.PlayerBlock = 999;
                s.Battle.EnemyHull = target.Hull - 20;
                s.Battle.EnemyBoardingHealth = target.BoardingHealth - 20;
                int playerHull = s.Hull, playerCrew = s.Battle.PlayerBoardingHealth;
                Assert.That(s.EnemyWillRam, Is.False);
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(damage), "Fixed nominal attack, without turn growth or loaded ammunition.");
                Assert.That(s.Hull, Is.EqualTo(playerHull));
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(playerCrew));
                Assert.That(s.Battle.EnemyBlock, Is.EqualTo(target.Guard), "Refresh, not accumulation.");
                Assert.That(s.Battle.EnemyLoadedShot, Is.Zero);
                Assert.That(s.Battle.DistanceMeters, Is.EqualTo(boarding ? 30f : 500f));
                Assert.That(s.Battle.EnemyMovingTowardPlayer, Is.False);
                Assert.That(s.Battle.EnemyHull, Is.EqualTo(target.Hull - 20 + (boarding ? 0 : target.Repair)));
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(target.BoardingHealth - 20 + (boarding ? target.Repair : 0)));
            }
            s.Battle.PlayerBlock = 999;
            s.Battle.EnemyHull = target.Hull - 1;
            s.Battle.EnemyBoardingHealth = target.BoardingHealth - 1;
            s.EndBattleTurn();
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(target.Hull - (boarding || target.Repair == 0 ? 1 : 0)));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(target.BoardingHealth - (!boarding || target.Repair == 0 ? 1 : 0)));
        }

        [Test]
        public void EnemyAttackStillUsesArmor_WithoutRetiredCurseWeaknessOrTideStance()
        {
            CoreVoyageSession s = Start(4);
            s.EndBattleTurn();
            SetSchool(s, "CurseMarks", 3); SetSchool(s, "CurseWeaknessPercent", 4); SetSchool(s, "CurseTurns", 2);
            SetSchool(s, "TideStance", 2); SetSchool(s, "TideStancePower", 6); SetSchool(s, "TideStanceTurns", 2);
            s.Battle.PlayerBlock = 999;
            int expected = Math.Max(1, 50 - Math.Max(0, (s.EffectiveArmor - 4) / 2));
            Assert.That(s.GetEnemyIntent(), Does.Contain("炮击伤害 " + expected));
            s.EndBattleTurn();
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(expected));
            Assert.That(s.Battle.Schools.CurseTurns, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.TideStanceTurns, Is.EqualTo(1));
        }

        [Test]
        public void BoardingRetainsRealHullDamageCarryoverAndUsesRealMedicine()
        {
            CoreVoyageSession s = Start(1);
            s.EndBattleTurn();
            s.Battle.EnemyHull -= 44;
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Hull)).SetValue(s, s.Hull - 20);
            int maxCrew = s.Battle.PlayerMaxBoardingHealth;
            Board(s);
            Assert.That(s.Battle.EnemyMaxBoardingHealth, Is.EqualTo(140));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(126), "Half the naval damage proportion carries into boarding.");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(maxCrew - (int)Math.Floor(maxCrew * 20m / (2m * s.MaxHull))));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(5));
            Assert.That(s.Battle.Hand.All(i => s.GetBattleCard(i).IsAllowedInStage(NavalBattleStage.Boarding)), Is.True);
            s.Battle.PlayerBoardingHealth = 1;
            s.Cargo.Medicine = 2;
            Put(s, "card_crew_bandage");
            CoreBattleCardEffects effects = s.GetBattleCardEffects(s.GetBattleCard(s.Battle.Hand[0]), NavalBattleStage.Boarding);
            Assert.That(effects.MedicineCost, Is.EqualTo(1));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1 + effects.ActualRepair));
        }

        [TestCase(false)] [TestCase(true)]
        public void VictoryRetainsRealAmmunitionDeductionsWithoutRewardsOrProgress(bool fromSailing)
        {
            CoreVoyageSession s = Start(0, fromSailing);
            int gold = s.Cargo.Gold, fame = s.Cargo.Fame, fragments = s.VoyageTreasureFragments, experience = s.PirateExperience;
            decimal profileGold = s.ProfileGold;
            int[] visited = s.Map == null ? Array.Empty<int>() : s.Map.Nodes.Where(n => n.Visited).Select(n => n.Id).ToArray();
            s.EndBattleTurn();
            s.Battle.EnemyHull = 1; s.Battle.EnemyBlock = 0;
            s.Cargo.Powder = 2; s.Cargo.Cannonballs = 2;
            Put(s, "card_chain_shot");
            CoreBattleState terminal = s.Battle;
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Phase, Is.EqualTo(fromSailing ? CoreDemoPhase.Sailing : CoreDemoPhase.Port));
            Assert.That(s.Battle, Is.SameAs(terminal));
            Assert.That(s.Cargo.Powder, Is.EqualTo(1));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(1));
            Assert.That(s.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(s.Cargo.Fame, Is.EqualTo(fame));
            Assert.That(s.VoyageTreasureFragments, Is.EqualTo(fragments));
            Assert.That(s.PirateExperience, Is.EqualTo(experience));
            Assert.That(s.ProfileGold, Is.EqualTo(profileGold));
            Assert.That(s.Settlement, Is.Null);
            Assert.That(s.LastBattleWasCaptured, Is.False);
            if (s.Map != null) Assert.That(s.Map.Nodes.Where(n => n.Visited).Select(n => n.Id), Is.EqualTo(visited));
            Assert.That(s.EndBattleTurn(), Is.False);
            Assert.That(s.PlayCard(0), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void DefeatKeepsTerminalBattleAndSuppliesWithoutVoyageFailureSettlement(bool boarding)
        {
            CoreVoyageSession s = Start(4, true);
            s.EndBattleTurn();
            if (boarding) { Board(s); s.Battle.PlayerBoardingHealth = 1; }
            else typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Hull)).SetValue(s, 1);
            s.Cargo.Powder = 3; s.Cargo.Cannonballs = 4; s.Cargo.Medicine = 5;
            int gold = s.Cargo.Gold;
            CoreBattleState terminal = s.Battle;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(s.Battle, Is.SameAs(terminal));
            Assert.That(s.Settlement, Is.Null);
            Assert.That(new[] { s.Cargo.Powder, s.Cargo.Cannonballs, s.Cargo.Medicine, s.Cargo.Gold }, Is.EqualTo(new[] { 3, 4, 5, gold }));
        }

        [Test]
        public void BurningTargetDiesBeforeRepairAndMutualDeathRemainsDefeat()
        {
            CoreVoyageSession s = Start(4);
            s.EndBattleTurn();
            s.Battle.EnemyHull = 1; s.Battle.EnemyBurnDamage = 2; s.Battle.EnemyBurnTurns = 1;
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Hull, Is.EqualTo(hull));
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            s.Battle.EnemyHull = 0;
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Hull)).SetValue(s, 0);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
        }

        [Test]
        public void EntryRejectsInvalidStateAndSeedProducesReproducibleRealDecks()
        {
            var s = new CoreVoyageSession();
            Assert.That(s.StartTrainingBattle(null), Is.False);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.None));
            CoreVoyageSession first = Start(0), second = Start(0);
            Assert.That(first.Battle.Hand, Is.EqualTo(second.Battle.Hand));
            Assert.That(first.Battle.DrawPile, Is.EqualTo(second.Battle.DrawPile));
            CoreBattleState original = first.Battle;
            Assert.That(first.StartTrainingBattle(CoreTrainingTargets.All[1]), Is.False);
            Assert.That(first.Battle, Is.SameAs(original));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                var pile = (System.Collections.Generic.IEnumerable<int>)typeof(CoreBattleState)
                    .GetMethod("DrawPileFor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first.Battle, new object[] { stage });
                int[] actual = pile.Concat(stage == NavalBattleStage.Preparation ? first.Battle.Hand : Enumerable.Empty<int>()).OrderBy(i => i).ToArray();
                Assert.That(actual, Is.EqualTo(first.GetBattleDeck(stage).OrderBy(i => i)), "The seam must reuse the configured real decks.");
            }
        }

        [Test]
        public void TrainingDoesNotAdvanceTheVoyagesRandomStream()
        {
            var s = new CoreVoyageSession();
            s.SetSeed("TRAINING-STREAM-ISOLATION");
            s.StartVoyage();
            FieldInfo field = typeof(CoreVoyageSession).GetField("voyageRandom", BindingFlags.Instance | BindingFlags.NonPublic);
            var voyageRandom = (ShatteredSail.Core.Random.Mulberry32Random)field.GetValue(s);
            uint before = voyageRandom.State;
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(field.GetValue(s), Is.Not.SameAs(voyageRandom));
            s.EndBattleTurn();
            s.Battle.PlayerBlock = 999;
            s.EndBattleTurn();
            s.Battle.EnemyHull = 0;
            s.EndBattleTurn();
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(field.GetValue(s), Is.SameAs(voyageRandom));
            Assert.That(voyageRandom.State, Is.EqualTo(before));
        }

        private static CoreVoyageSession Start(int index, bool fromSailing = false)
        {
            var s = new CoreVoyageSession();
            s.ConfigureCrewCapabilities(false, false, false, false, false, false);
            s.SetSeed("FIXED-TRAINING-TEST");
            if (fromSailing) s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[index]), Is.True);
            return s;
        }

        private static void Board(CoreVoyageSession s)
        {
            s.Battle.DistanceMeters = 50f;
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
        }

        private static void Put(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear();
            s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id));
        }

        private static void SetSchool(CoreVoyageSession s, string name, int value) =>
            typeof(CoreSchoolBattleState).GetProperty(name).SetValue(s.Battle.Schools, value);
    }
}
