using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingManuscriptRulesTests
    {
        [Test]
        public void Burn_NewLayersJoinRemainingStackAndDecayOnlyOnce()
        {
            var s = Battle();
            Invoke(s, "BAddEnemyBurn", 2, 2);
            int hp = s.Battle.EnemyBoardingHealth;
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 2));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(1));
            Invoke(s, "BAddEnemyBurn", 3, 2);
            foreach (int damage in new[] { 4, 3, 2, 1 })
            {
                hp = s.Battle.EnemyBoardingHealth;
                Invoke(s, "BEnemyEnd");
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - damage));
                Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(damage - 1));
                Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(damage - 1));
            }
        }

        [Test]
        public void SelfBurn_DecaysAndTransferAddsToEnemyStack()
        {
            var s = Battle(); Play(s, "fire_14");
            int hp = s.Battle.PlayerBoardingHealth;
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp - 2));
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(1));
            Play(s, "fire_14");
            Invoke(s, "BAddEnemyBurn", 2, 2); Play(s, "fire_15");
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.Zero);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(5));
        }

        [Test]
        public void Curse_ExtraSettlementDoesNotConsumeLayersOrCreateAnExpiry()
        {
            var s = Battle(); Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_02");
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(9));
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_10");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 9));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(9));
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 18));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(8));
        }

        [Test]
        public void Curse_BonusDamageDoesNotIncreaseItsRemainingLayers()
        {
            var s = Battle(); Play(s, "curse_02");
            typeof(CoreSchoolBattleState).GetProperty("CurseNextTickBonus").SetValue(s.Battle.Schools, 2);
            int hp = s.Battle.EnemyBoardingHealth;
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 5));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(2));
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 7));
        }

        [Test]
        public void ContinuousRecovery_MedicineAddsToOneDecayingPool()
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = 500;
            Play(s, "basic_07"); Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(502));
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(1));
            s.Cargo.Medicine = 1; Play(s, "basic_07");
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(6));
            Invoke(s, "BPlayerEnd"); Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(513));
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(4));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void UpgradedTreatment_DescriptionAndAppliedLayersAgree(int rank)
        {
            var s = Battle("basic_07", rank); string id = "card_boarding_basic_07";
            int maximum = s.GetCardMaximumUpgradeLevel(id); rank = Math.Min(rank, maximum);
            CoreBattleCard card = s.GetBattleCard(Index("basic_07"));
            int expected = CoreCardUpgradeDesign.ScaleEffect(card, 5, rank);
            s.Cargo.Medicine = 1; Play(s, "basic_07");
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(expected));
            Assert.That(card.Description, Does.Contain("额外获得持续恢复：" + (expected - 2)));
        }

        [Test]
        public void TideBath_CanStackWithoutBlockingAndPaysEachPlay()
        {
            var s = Battle(); Play(s, "tide_05"); Play(s, "tide_05");
            s.Cargo.Medicine = 1; Play(s, "tide_12");
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(4));
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(8));
            Play(s, "tide_12");
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(10));
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(4));
        }

        [Test]
        public void ManuscriptBaseDamageAndUncappedSelfBurnBonusApply()
        {
            var s = Battle(); int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_01"); Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 8));
            Play(s, "curse_06"); Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 15));
            Invoke(s, "BAddSelfBurn", 8);
            CoreBattleCard card = s.GetBattleCard(Index("fire_11"));
            Assert.That(s.GetBoardingCardDamageQuote(card), Is.EqualTo(46));
            hp = s.Battle.EnemyBoardingHealth; Play(s, "fire_11");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 46));
        }

        [Test]
        public void FirePot_ContinuesAfterTheOldTwoTriggerLimit()
        {
            var s = Battle(); s.Cargo.Powder = 1; Play(s, "fire_04");
            for (int i = 0; i < 4; i++) Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(30));
            Assert.That(s.Battle.Boarding.FirePots.Count, Is.EqualTo(1));
        }

        [Test]
        public void FirePacts_StackAndExpireInTheirOwnWindows()
        {
            var s = Battle(); Play(s, "fire_12"); Invoke(s, "BPlayerEnd");
            Play(s, "fire_12"); Invoke(s, "BAddSelfBurn", 5);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(10));
            Invoke(s, "BPlayerEnd"); Invoke(s, "BAddSelfBurn", 5);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(15));
        }

        [Test]
        public void Weakness_StacksPersistsAndExpiresPerApplication()
        {
            var s = Battle(); Play(s, "curse_02"); Play(s, "curse_07");
            Assert.That((int)Invoke(s, "BEnemyAttack", 10, true), Is.EqualTo(4));
            Assert.That((int)Invoke(s, "BEnemyAttack", 10, true), Is.EqualTo(4));
            Invoke(s, "BEnemyEnd"); Play(s, "curse_07");
            Assert.That((int)Invoke(s, "BEnemyAttack", 10, true), Is.Zero);
            Invoke(s, "BEnemyEnd");
            Assert.That((int)Invoke(s, "BEnemyAttack", 10, true), Is.EqualTo(6));
            Invoke(s, "BEnemyEnd");
            Assert.That((int)Invoke(s, "BEnemyAttack", 10, true), Is.EqualTo(10));
        }

        [Test]
        public void ArmorReduction_PersistsAcrossRepeatedGuardGrants()
        {
            var s = Battle(); Play(s, "curse_02"); Play(s, "curse_08");
            Assert.That((int)Invoke(s, "CTBEnemyBlockGain", 10, true), Is.EqualTo(5));
            Assert.That((int)Invoke(s, "CTBEnemyBlockGain", 10, true), Is.EqualTo(5));
            Play(s, "curse_08");
            Assert.That((int)Invoke(s, "CTBEnemyBlockGain", 10, true), Is.Zero);
        }

        [Test]
        public void Counter_TriggersBeforeEveryAttackAndCanPreventLethalHit()
        {
            var s = Battle(); Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_09");
            int hp = s.Battle.EnemyBoardingHealth;
            Invoke(s, "BEnemyAttack", 20, true); Invoke(s, "BEnemyAttack", 20, true);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 24));
            s.Battle.EnemyBoardingHealth = 8;
            Assert.That((int)Invoke(s, "BEnemyAttack", 20, true), Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.Zero);
        }

        [Test]
        public void ArcaneRetain_KeepsAllGrantedGuardAndEchoHasNoOldDamageCap()
        {
            var s = Battle(); Play(s, "arcane_03"); Play(s, "arcane_08");
            Assert.That(s.Battle.Boarding.ArcaneRetainedGuard, Is.EqualTo(5));
            Play(s, "arcane_13"); Invoke(s, "BPlayerEnd"); Play(s, "arcane_03");
            Play(s, "arcane_04");
            Assert.That(s.Battle.Boarding.DelayedDamage.Last().Amount, Is.EqualTo(21));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EchoUpgrade_ScalesTheFinalHalfDamageWithoutASeparateCap(int rank)
        {
            var s = Battle("arcane_13", rank); Play(s, "arcane_03"); Play(s, "arcane_13");
            Play(s, "arcane_03"); Play(s, "arcane_04");
            CoreBattleCard echo = s.GetBattleCard(Index("arcane_13"));
            int expected = CoreCardUpgradeDesign.ScaleEffect(echo, 21, echo.UpgradeLevel);
            Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(expected));
            Assert.That(echo.Description, Does.Not.Contain("最多"));
            Assert.That(echo.Description, Does.Contain("50%"));
        }

        [TestCase("arcane_04", 0)] [TestCase("arcane_04", 1)]
        [TestCase("arcane_06", 0)] [TestCase("arcane_06", 1)]
        [TestCase("arcane_11", 0)] [TestCase("arcane_11", 1)]
        public void AllManaAttacks_UseExtendedCapacityAndTemporaryManaInPaymentQuoteAndDamage(string key, int rank)
        {
            var s = Battle(key, rank);
            typeof(CoreSchoolBattleState).GetProperty("ManaLimit").SetValue(s.Battle.Schools, 5);
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, 5);
            typeof(CoreSchoolBattleState).GetProperty("TemporaryMana").SetValue(s.Battle.Schools, 2);
            var card = s.GetBattleCard(Index(key));
            int expected = key == "arcane_06"
                ? 9 * CoreCardUpgradeDesign.ScaleEffect(card, 5, card.UpgradeLevel)
                : CoreCardUpgradeDesign.ScaleEffect(card, key == "arcane_04" ? 66 : 92, card.UpgradeLevel);
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(key)); s.Battle.Energy = 100;
            Assert.That(s.GetBoardingPlayChoice(0).MaxResource, Is.EqualTo(7));
            Assert.That(s.GetBoardingCardDamageQuote(card), Is.EqualTo(expected));
            int hp = s.Battle.EnemyBoardingHealth;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            if (key == "arcane_11")
            {
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp));
                Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(expected));
                Invoke(s, "BPlayerStart");
            }
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - expected));
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        public void DelayedEcho_WaitsForPrimarySettlementThenStartsItsOwnDelayEvenWhenFullyGuarded(int rank, bool early)
        {
            var s = Battle("arcane_13", rank); Play(s, "arcane_03"); Play(s, "arcane_13");
            Play(s, "arcane_11"); // Two remaining mana lock the main delayed attack at 52.
            var echoCard = s.GetBattleCard(Index("arcane_13"));
            int echo = CoreCardUpgradeDesign.ScaleEffect(echoCard, 26, echoCard.UpgradeLevel);
            Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(52));
            Assert.That(s.Battle.Boarding.ArcaneEchoReady, Is.False);
            Assert.That(s.Battle.Boarding.ResolvedAttacks, Is.Empty);
            int hp = s.Battle.EnemyBoardingHealth; s.Battle.EnemyBlock = 100;
            if (early)
            {
                Play(s, "arcane_03");
                s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index("arcane_15")); s.Battle.Energy = 100;
                var choice = s.GetBoardingPlayChoice(0);
                Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { SnapshotToken = choice.SnapshotToken,
                    SelectionIndexes = new List<int> { 0 } }), Is.True);
            }
            else Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(48));
            Assert.That(s.Battle.Boarding.ResolvedAttacks.Single().GuardAbsorbed, Is.EqualTo(52));
            var queuedEcho = s.Battle.Boarding.DelayedDamage.Single();
            Assert.That(queuedEcho.Amount, Is.EqualTo(echo));
            Assert.That(queuedEcho.Remaining, Is.EqualTo(1));
            Assert.That(queuedEcho.SourceCardId, Is.EqualTo("card_boarding_arcane_11"));
            s.Battle.EnemyBlock = 0; Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - echo));
            Assert.That(s.Battle.Boarding.DelayedDamage, Is.Empty);
            Assert.That(s.Battle.Boarding.ResolvedAttackSequence, Is.EqualTo(2));
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - echo), "An echo must not produce another echo.");
        }

        [Test]
        public void RemovedEnergyCardOnceLimits_DoNotBlockSecondPlay()
        {
            var s = Battle(); Play(s, "fire_14"); Play(s, "fire_14");
            Play(s, "arcane_03"); Play(s, "arcane_10"); Play(s, "arcane_03"); Play(s, "arcane_10");
            Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_14"); Play(s, "curse_14");
            Play(s, "tide_05"); Play(s, "tide_05"); Play(s, "tide_14");
            Play(s, "tide_05"); Play(s, "tide_14");
        }

        [Test]
        public void DelayedChoice_CanResolveTheSelectedLaterEffectAndRejectsStaleChoice()
        {
            var s = Battle(); Play(s, "basic_13"); Play(s, "arcane_03"); Play(s, "arcane_11");
            Play(s, "arcane_03");
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index("arcane_15")); s.Battle.Energy = 100;
            CoreBoardingPlayChoice choice = s.GetBoardingPlayChoice(0);
            Assert.That(choice.Kind, Is.EqualTo(CoreBoardingChoiceKind.DelayedEffect));
            Assert.That(choice.Candidates.Count, Is.EqualTo(2));
            int hp = s.Battle.EnemyBoardingHealth;
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { SnapshotToken = choice.SnapshotToken,
                SelectionIndexes = new List<int> { 99 } }), Is.False);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(100));
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { SnapshotToken = choice.SnapshotToken,
                SelectionIndexes = new List<int> { 1 } }), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 60));
            Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(12));
        }

        [Test]
        public void EnemyAppliedBurnAndCurse_DecayAtPlayerEndAndAddDirectly()
        {
            var s = Battle(); var enemy = s.Battle.BoardingEnemy;
            typeof(CoreBoardingEnemyState).GetProperty("PlayerBurnDamage").SetValue(enemy, 2);
            typeof(CoreBoardingEnemyState).GetProperty("PlayerBurnTurns").SetValue(enemy, 2);
            var marks = (List<int>)typeof(CoreBoardingEnemyState).GetField("CurseDurations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
            marks.AddRange(new[] { 1, 1 });
            int hp = s.Battle.PlayerBoardingHealth; Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp - 4));
            Assert.That(enemy.PlayerBurnDamage, Is.EqualTo(1)); Assert.That(enemy.CurseMarks, Is.EqualTo(1));
            typeof(CoreVoyageSession).GetMethod("AddHostileBurn", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { enemy, 3, 2 });
            marks.AddRange(new[] { 1, 1, 1 });
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp - 12));
            Assert.That(enemy.PlayerBurnDamage, Is.EqualTo(3)); Assert.That(enemy.CurseMarks, Is.EqualTo(3));
        }

        [Test]
        public void EnemyPreview_UsesPersistentDebuffsAndBeforeAttackCountersWithoutMutating()
        {
            var s = Battle();
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 20, 10, 0, 28, 1, 1, healthMultiplier: 1);
            var deck = new CoreBoardingEnemyDeck("fire", new CoreBoardingEnemyTurn("双击",
                new CoreBoardingEnemyCard("a", "第一击", attackPercent: 100, burnPercent: 10),
                new CoreBoardingEnemyCard("b", "第二击", attackPercent: 100, guardPercent: 100)));
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(s.Battle.BoardingEnemy, deck);
            Play(s, "curse_02"); Play(s, "curse_04"); Play(s, "curse_07"); Play(s, "curse_08"); Play(s, "curse_09");
            int player = s.Battle.PlayerBoardingHealth, enemy = s.Battle.EnemyBoardingHealth;
            var preview = (CoreBoardingEnemyPreview)Invoke(s, "RunBoardingEnemyTurn", false, true);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(player));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Invoke(s, "BPlayerEnd");
            var actual = (CoreBoardingEnemyPreview)Invoke(s, "RunBoardingEnemyTurn", true, false);
            Assert.That(actual.AttackDamage, Is.EqualTo(preview.AttackDamage));
            Assert.That(actual.PlayerHealthLoss, Is.EqualTo(preview.PlayerHealthLoss));
            Assert.That(actual.Actions.Count, Is.EqualTo(preview.Actions.Count));
            for (int i = 0; i < actual.Actions.Count; i++)
            {
                Assert.That(actual.Actions[i].EnemyHealthAfter, Is.EqualTo(preview.Actions[i].EnemyHealthAfter));
                Assert.That(actual.Actions[i].AttackDamage, Is.EqualTo(preview.Actions[i].AttackDamage));
                Assert.That(actual.Actions[i].GuardGained, Is.EqualTo(preview.Actions[i].GuardGained));
            }
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(preview.ResourcesAfter.PlayerBurnDamage));
        }

        private static CoreVoyageSession Battle(string upgradeKey = null, int rank = 0)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            if (upgradeKey != null) s.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_boarding_" + upgradeKey, rank } });
            s.SetSeed("BOARDING-MANUSCRIPT-TEST");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000; s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel; Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000; s.Battle.EnemyBlock = 0;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Medicine = 0; s.Cargo.Wood = 0; return s;
        }
        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);
        private static void Play(CoreVoyageSession s, string key)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(key)); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions()), Is.True, key);
        }
        private static object Invoke(CoreVoyageSession s, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
    }
}
