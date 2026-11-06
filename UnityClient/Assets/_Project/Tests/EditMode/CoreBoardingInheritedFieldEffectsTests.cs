using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingInheritedFieldEffectsTests
    {
        [Test]
        public void FireSettlesOldBurnThenAddsCurrentDurationBeforeDecayForBothActors()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Fire, 5, true);
            Play(s, "fire_12");
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000));
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(5));
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.Zero,
                "Environmental burn must not activate a pact for self-applied card burn.");
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(5));
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000));
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.EqualTo(5));
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(4));
            s.Battle.PlayerBlock = 2;
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(997));
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(8));
        }

        [Test]
        public void LastEnemyOwnedFireTurnStillAppliesBurnBeforeItExpires()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Fire, 1, false);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(1));
            Assert.That(s.Battle.BoardingFields.Contains(CoreNavalFieldKind.Fire), Is.False);
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.Zero);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(999));
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.Zero);
        }

        [Test]
        public void ArcaneAddsAfterManaResetAndUsesDurationAfterEnemyEnd()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Arcane, 3, true);
            s.Battle.Schools.Mana = 99;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Arcane), Is.EqualTo(2));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(s.Battle.Schools.ManaLimit + 2));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(s.Battle.Schools.ManaLimit + 1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(s.Battle.Schools.ManaLimit));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CurseAppliesToEndingActorAndExecutesImmediately(bool player)
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Curse, 6, !player);
            if (player) s.Battle.PlayerBoardingHealth = 6;
            else s.Battle.EnemyBoardingHealth = 6;
            Assert.That(Invoke(s, player ? "BPlayerEnd" : "BEnemyEnd"), Is.True);
            Assert.That(player ? s.Battle.PlayerBoardingHealth : s.Battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(player ? s.Battle.Boarding.PlayerExecutedByCurse : s.Battle.Boarding.EnemyExecutedByCurse, Is.True);
        }

        [Test]
        public void CurseUsesPreDecayDurationAndMarksRemainAfterFieldExpires()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Curse, 1, false);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(1));
            Assert.That(s.Battle.BoardingFields.Contains(CoreNavalFieldKind.Curse), Is.False);
            Invoke(s, "BEnemyEnd"); Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
        }

        [TestCase("blood_01", 6)]
        [TestCase("blood_02", 8)]
        [TestCase("blood_04", 6)]
        [TestCase("blood_05", 6)]
        [TestCase("fire_14", 0)]
        public void BloodPoolRewardsOnlyTheActualActiveLifePayment(string key, int cost)
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Blood, 5, false);
            Play(s, key);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000 - cost));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(2 * cost));
        }

        [Test]
        public void BloodPoolGuardUsesExistingAutomaticTideGuardConversion()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Blood, 5, true);
            typeof(CoreBoardingBattleState).GetProperty("TideGuardConversionActive").SetValue(s.Battle.Boarding, true);
            Play(s, "blood_03");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(34));
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(34));
        }

        [Test]
        public void BloodPoolDoesNotGrantGuardForBurnDamageOrOrdinaryAttacks()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Blood, 5, true);
            Invoke(s, "BApplyPlayerBurn", 4, false);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(991));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
        }

        [Test]
        public void ThunderPreservesChainOnOrdinaryCardsAndStartsWithoutPassiveGainOrDamage()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Thunder, 5, true);
            s.Battle.Schools.ThunderChain = 50;
            Play(s, "basic_04");
            Invoke(s, "BPlayerStart");
            Invoke(s, "BBoardingFieldsBeginTurn", false);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(50));
            Assert.That(s.Battle.BoardingEnemy.ThunderChain, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(10));
        }

        [TestCase("thunder_13", 110)]
        [TestCase("thunder_14", 92)]
        [TestCase("thunder_15", 272)]
        public void ThunderExemptsFinisherConsumptionWhileDamageStillUsesPrePlayChain(string key, int damage)
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Thunder, 5, true);
            s.Battle.Schools.ThunderChain = 10;
            Play(s, key);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(11));
        }

        [Test]
        public void ThunderRepeatsBlockableOverloadForEachChainIncreaseWithoutBloodPoolOrAttackReactions()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Thunder, 5, true); Add(s, CoreNavalFieldKind.Blood, 5, true);
            Play(s, "blood_08");
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 2500;
            s.Battle.PlayerBlock = 400;
            s.Battle.Schools.ThunderChain = 49;
            Play(s, "thunder_01");
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1900));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(50));
            Play(s, "thunder_01");
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(900));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(51));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(988), "Environmental damage must not trigger blood debt retaliation.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThunderLethalOverloadStopsCardCompletionWhenBattleExits(bool voyage)
        {
            var s = Battle(voyage); Add(s, CoreNavalFieldKind.Thunder, 5, true);
            CoreBattleState battle = s.Battle;
            battle.Schools.ThunderChain = 49;
            battle.PlayerBoardingHealth = 100;
            Assert.DoesNotThrow(() => Play(s, "thunder_01"));
            Assert.That(battle.PlayerBoardingHealth, Is.Zero);
            Assert.That(battle.Schools.ThunderChain, Is.EqualTo(50));
            Assert.That(s.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            if (voyage) Assert.That(s.Battle, Is.Null);
            else Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
        }

        [Test]
        public void ExpiredThunderRestoresFinisherConsumptionAndNormalResetRules()
        {
            var s = Battle(); Add(s, CoreNavalFieldKind.Thunder, 1, true);
            s.Battle.Schools.ThunderChain = 10;
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.BoardingFields.Contains(CoreNavalFieldKind.Thunder), Is.False);
            Play(s, "thunder_13");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(890));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Play(s, "basic_04");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            s.Battle.Schools.ThunderChain = 7;
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        private static void Add(CoreVoyageSession s, CoreNavalFieldKind kind, int turns, bool owner)
            => s.Battle.BoardingFields.Fields.Add(new CoreBoardingField { Kind = kind, PlayerOwned = owner, RemainingTurns = turns });

        private static CoreVoyageSession Battle(bool voyage = false)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("BOARDING-INHERITED-FIELD-EFFECTS");
            if (voyage) s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            if (voyage) s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 10, 0, 0, 28, 1, 1, healthMultiplier: 1);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Medicine = 0;
            return s;
        }

        private static void Play(CoreVoyageSession s, string key)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key);
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(index); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True, key);
        }

        private static object Invoke(CoreVoyageSession s, string method, params object[] args)
            => typeof(CoreVoyageSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s, args);
    }
}
