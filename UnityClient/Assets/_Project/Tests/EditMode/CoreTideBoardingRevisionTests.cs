using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTideBoardingRevisionTests
    {
        [Test]
        public void DeepTideGrantsThirtyGuardAndCashoutPaysFourPower()
        {
            var s = Battle(); Play(s, "tide_02");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(30));
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(30));
            BlockDamage(s, 6);
            s.Battle.Hand.Clear();
            s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_tide_15"));
            Assert.That(s.GetBoardingPlayChoice(0).MinResource, Is.EqualTo(4));
            Assert.That(s.GetBoardingPlayChoice(0).MaxResource, Is.EqualTo(4));
            Play(s, "tide_15");
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(2));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(40));
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(24), "Cashout grants ordinary guard until Leave Tide converts it.");
        }

        [Test]
        public void LeaveTideConvertsCurrentAndFutureGuardWithoutErasingRetention()
        {
            var s = Battle(); Play(s, "basic_05"); Play(s, "arcane_08"); Play(s, "tide_01");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(48));
            Play(s, "tide_05");
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero, "Conversion does not manufacture tide power.");
            Assert.That(s.Battle.Boarding.TideGuardConversionActive, Is.True);
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(48));
            Assert.That(s.Battle.Boarding.ArcaneRetainedGuard, Is.EqualTo(14));
            Play(s, "basic_04");
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(58));
            BlockDamage(s, 7);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(7));
            Assert.That(s.Battle.Boarding.ArcaneRetainedGuard, Is.EqualTo(14));
            s.Battle.Turn++;
            s.Battle.PlayerBlock = (int)Invoke(s, "BRemainingGuardAtNewTurn");
            Invoke(s, "CTBPlayerStart");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(14));
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(14));
            BlockDamage(s, 14);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(21));
            Assert.That(s.Battle.Boarding.ArcaneRetainedGuard, Is.Zero);
            Play(s, "basic_04");
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(10), "The conversion survives the turn boundary.");
        }

        [Test]
        public void OrdinaryGuardIsSpentFirstAndAttacksChargeOnlyOnce()
        {
            var s = Battle(); Play(s, "basic_04"); Play(s, "tide_02");
            s.Battle.PlayerBlock -= 13;
            Invoke(s, "BAfterEnemyAttack", 13, 0);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(3));
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(27));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(3));
        }

        [Test]
        public void BurningChargesTideGuardWithoutTriggeringAttackOnlyRetaliation()
        {
            var s = Battle(); Play(s, "tide_02");
            SetField(s.Battle.Boarding, "FireRetaliations", 1);
            Invoke(s, "BApplyPlayerBurn", 5, false);
            int enemy = s.Battle.EnemyBoardingHealth, player = s.Battle.PlayerBoardingHealth;
            Invoke(s, "TickBoardingEnemyFire");
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(5));
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(25));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(player));
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(4));
        }

        [Test]
        public void TidePersistsAcrossBothTurnEndsAndNextTurnStart()
        {
            var s = Battle(); Play(s, "tide_02"); BlockDamage(s, 8);
            Invoke(s, "CTBPlayerEnd"); Invoke(s, "CTBEnemyEnd");
            s.Battle.PlayerBlock = 0; Invoke(s, "CTBPlayerStart");
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(8));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(8));
        }

        [TestCase(18)]
        [TestCase(int.MaxValue)]
        public void BoardingImportsLargeNavalReserveWithoutClampingAndQuotesSpendItSafely(int carried)
        {
            var s = Battle(carried);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(carried));
            Play(s, "tide_02"); BlockDamage(s, 2);
            int afterGain = (int)Math.Min(int.MaxValue, (long)carried + 2);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(afterGain), "Every blocked point is retained, saturating only at the storage boundary.");
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey("tide_08");
            int expected = (int)Math.Min(int.MaxValue, 60L + (long)afterGain * 3 / 2);
            Assert.That(s.GetBoardingCardDamageQuote(card), Is.EqualTo(expected));
            s.Battle.EnemyBlock = int.MaxValue;
            Play(s, "tide_08");
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(int.MaxValue - expected));
        }

        [Test]
        public void EnemyOrdinaryGuardDoesNotGenerateTideEvenForTideSchool()
        {
            var s = Battle(); Set(s.Battle.BoardingEnemy, "Deck", CoreBoardingEnemyCatalog.ForSchool("tide"));
            s.Battle.EnemyBlock = 10;
            Invoke(s, "BDeal", 5, false, false);
            Assert.That(s.Battle.BoardingEnemy.TidePower, Is.Zero);
            s.Battle.EnemyBlock = 10; Set(s.Battle.BoardingEnemy, "TideGuard", 6);
            Invoke(s, "BDeal", 5, false, false);
            Assert.That(s.Battle.BoardingEnemy.TidePower, Is.EqualTo(1));
            Assert.That(s.Battle.BoardingEnemy.TideGuard, Is.EqualTo(5));
        }

        [Test]
        public void EnemyPreviewTracksOnlyTideGuardBurnAndPersistentPowerWithoutMutation()
        {
            var s = Battle();
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 20, 10, 0, 28, 1, 1, healthMultiplier: 1);
            var deck = new CoreBoardingEnemyDeck("tide", new CoreBoardingEnemyTurn("混合格挡",
                new CoreBoardingEnemyCard("tidal", "蓄潮格挡", mechanic: CoreBoardingEnemyMechanic.TideGuard, guardPercent: 100),
                new CoreBoardingEnemyCard("normal", "普通格挡", guardPercent: 100)));
            Set(s.Battle.BoardingEnemy, "Deck", deck);
            Set(s.Battle.BoardingEnemy, "SelfBurnDamage", 15); Set(s.Battle.BoardingEnemy, "SelfBurnTurns", 15);
            CoreBoardingEnemyPreview preview = null;
            for (int query = 0; query < 3; query++)
            {
                preview = (CoreBoardingEnemyPreview)Invoke(s, "RunBoardingEnemyTurn", false, true);
                Assert.That(preview.ResourcesAfter.TidePower, Is.EqualTo(5));
                Assert.That(preview.ResourcesAfter.TideGuard, Is.EqualTo(5));
                Assert.That(s.Battle.BoardingEnemy.TidePower, Is.Zero);
                Assert.That(s.Battle.BoardingEnemy.TideGuard, Is.Zero);
            }
            Invoke(s, "BPlayerEnd"); Invoke(s, "RunBoardingEnemyTurn", true, false); Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.BoardingEnemy.TidePower, Is.EqualTo(preview.ResourcesAfter.TidePower));
            Assert.That(s.Battle.BoardingEnemy.TideGuard, Is.EqualTo(preview.ResourcesAfter.TideGuard));
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.EqualTo(14));
        }

        [TestCase("card_school_tide_b07", 0)]
        [TestCase("card_school_tide_b09", 6)]
        [TestCase("compat_tide_cleanse", 6)]
        public void LegacySchoolPaymentsAndGuardShareTheNewBoardingReserve(string id, int remaining)
        {
            var s = Battle(); Play(s, "tide_05");
            Invoke(s, "AddSchoolGuard", 10);
            Assert.That(s.Battle.Boarding.TideGuard, Is.EqualTo(10));
            BlockDamage(s, 10);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(10));
            // Current library aliases replace these old cards with new boarding
            // specs; test the compatibility payment entry against its source definition.
            CoreBattleCard legacy = id == "compat_tide_cleanse"
                ? new CoreBattleCard(id, "旧净潮", "策略", 1, 0, 0, 0, 0, 0, "", NavalBattleStages.Boarding,
                    schoolId: "tide", schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.TideCleanse, threshold: 4))
                : CoreSchoolCardCatalog.All.Single(c => c.Id == id);
            Invoke(s, "PaySchoolCardCost", legacy);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(remaining));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(remaining));
            Play(s, "basic_04"); BlockDamage(s, 4);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(remaining + 4));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(remaining + 4));
        }

        private static CoreVoyageSession Battle(int carried = 0)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("TIDE-BOARDING-20261024");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True); Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000; s.Battle.DistanceMeters = 100;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            Set(s.Battle.Schools, "TidePower", carried);
            s.Battle.Alignment = NavalBattleAlignment.Parallel; Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; Set(s, "Morale", 0);
            return s;
        }
        private static void BlockDamage(CoreVoyageSession s, int amount)
        { s.Battle.PlayerBlock -= amount; Invoke(s, "BRecordBlockedDamage", amount); }
        private static void Play(CoreVoyageSession s, string key)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key);
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(index); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static object Invoke(CoreVoyageSession s, string name, params object[] values) => typeof(CoreVoyageSession)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, values);
    }
}
