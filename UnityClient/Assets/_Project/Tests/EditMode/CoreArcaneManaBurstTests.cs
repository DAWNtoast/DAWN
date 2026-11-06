using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreArcaneManaBurstTests
    {
        private const string Id = "card_school_arcane_n04";

        [TestCase(0, 10)]
        [TestCase(3, 25)]
        [TestCase(5, 35)]
        [TestCase(8, 50)]
        public void ManaBurstSpendsWholePoolAndQuotesTheSingleBlockableHit(int mana, int expected)
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", mana); Put(s);
            s.Cargo.Powder = s.Cargo.Cannonballs = 0;
            s.Battle.EnemyBlock = 7;
            int hull = s.Battle.EnemyHull, energy = s.Battle.Energy;
            CoreBattleCard card = s.GetBattleCard(Index());
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expected));
            Assert.That(s.GetNavalCardChoice(0), Is.Null, "X spends the current pool without a resource selection dialog.");
            Assert.That(s.NavalExpansionResourceAmount(CoreNavalSchoolExpansionCatalog.FindSpec(Id)), Is.EqualTo(mana));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.Energy, Is.EqualTo(energy - 2));
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - expected + 7));
            Assert.That(s.Cargo.Powder + s.Cargo.Cannonballs, Is.Zero);
        }

        [TestCase(0, 10, 5)]
        [TestCase(1, 10, 9)]
        [TestCase(2, 10, 20)]
        public void UpgradesGrowManaPayoutWhileTheAuthoredBaseStaysFixedWithoutDoubleCounting(int level, int basis, int coefficient)
        {
            CoreVoyageSession s = Start(level); SetSchool(s, "Mana", 8); Put(s);
            CoreBattleCard card = s.GetBattleCard(Index());
            Assert.That(card.Damage, Is.EqualTo(basis));
            Assert.That(s.GetCardMaximumUpgradeLevel(Index()), Is.EqualTo(2));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(basis + 8 * coefficient));
            foreach (string text in new[] { card.Description,
                s.BattleCardFaceDescription(card, NavalBattleStage.Naval),
                s.BattleCardEffectDescription(card, NavalBattleStage.Naval) })
            {
                Assert.That(Compact(text), Does.Contain(basis + "+" + coefficient + "X")
                    .And.Not.Contain((basis + 8 * coefficient) + "+" + coefficient + "X"));
                Assert.That(CoreBattleCardKeywordText.Build(card, text), Is.Empty, "Mana remains a resource, not a new keyword.");
            }
            int hull = s.Battle.EnemyHull;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - basis - 8 * coefficient));
        }

        [TestCase(1, 10, 100)]
        [TestCase(2, 20, 410)]
        public void MatureManaPoolQuotesAndExecutesTheFinalCoefficientBeforePayment(int level, int mana, int expected)
        {
            CoreVoyageSession s = Start(level); SetSchool(s, "Mana", mana); Put(s);
            s.Battle.EnemyBlock = 20;
            CoreBattleCard card = s.GetBattleCard(Index());
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expected));
            int hull = s.Battle.EnemyHull;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - expected + 20));
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
        }

        [Test]
        public void PreviewCancellationAndInvalidAttemptsLeaveManaCardsAndUsesUntouched()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 8); Put(s);
            CoreBattleCard card = s.GetBattleCard(Index());
            int energy = s.Battle.Energy, hull = s.Battle.EnemyHull, discard = s.Battle.DiscardPile.Count;
            for (int preview = 0; preview < 3; preview++)
            {
                Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(50));
                Assert.That(s.BattleCardFaceDescription(card), Does.Contain("X"));
                Assert.That(s.GetNavalChoiceUnavailableReason(0, null), Is.Empty);
                Assert.That(s.GetNavalCardChoice(0), Is.Null);
            }
            // A cancelled interaction performs no play call; repeated queries are read-only.
            AssertUnchanged();
            Pose(s, false, 3, 2);
            Assert.That(s.PlayCard(0), Is.False); AssertUnchanged();
            Pose(s, false, 0, 0);
            Assert.That(s.PlayCard(0), Is.False); AssertUnchanged();
            Pose(s, false, 4, 0); s.Battle.Energy = 1;
            Assert.That(s.PlayCard(0), Is.False);
            s.Battle.Energy = energy; AssertUnchanged();
            Assert.That(s.PlayCard(0), Is.True, "A target exactly four grid cells away is legal.");

            void AssertUnchanged()
            {
                Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(8));
                Assert.That(s.Battle.Energy, Is.EqualTo(energy));
                Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
                Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index() }));
                Assert.That(s.Battle.DiscardPile.Count, Is.EqualTo(discard));
                Assert.That(s.GetBattleCardEnergyCost(Index()), Is.EqualTo(2));
            }
        }

        [Test]
        public void ExplicitOptionsCannotUnderpayAndRepeatedCopiesEscalateEnergy()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 5); Put(s);
            int energy = s.Battle.Energy, hull = s.Battle.EnemyHull;
            Assert.That(s.PlayNavalCard(0, new CoreNavalCardPlayOptions { ResourceAmount = 0 }), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 35));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy - 2));
            SetSchool(s, "Mana", 4); Put(s);
            Assert.That(s.GetBattleCardEnergyCost(Index()), Is.EqualTo(3));
            Assert.That(s.PlayNavalCard(0, new CoreNavalCardPlayOptions { ResourceAmount = -100 }), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 65));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy - 5));
        }

        [Test]
        public void ManaGainImmediatelyRaisesBurstAndNextTurnStillRefillsOnlyToLimit()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 3);
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index("card_school_arcane_b06"));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(5));
            Put(s);
            Assert.That(s.GetBattleCardDamage(s.GetBattleCard(Index()), NavalBattleStage.Naval), Is.EqualTo(35));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            SetSchool(s, "Mana", 99);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(s.Battle.Schools.ManaLimit));
        }

        [Test]
        public void OrdinaryAttackBonusesAreAppliedOnceAndNoCannonLoadingIsSpent()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 3); SetSchool(s, "ArcaneNextAttackDamage", 7); Put(s);
            s.Battle.PlayerLoadedShot = 11; s.Battle.PlayerFireRounds = 2; s.Battle.EnemyBlock = 32;
            CoreBattleCard card = s.GetBattleCard(Index());
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(32));
            Assert.That(Compact(s.BattleCardFaceDescription(card)), Does.Contain("17+5X"));
            int hull = s.Battle.EnemyHull;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.Zero);
            Assert.That(s.Battle.PlayerLoadedShot, Is.EqualTo(11));
            Assert.That(s.Battle.PlayerFireRounds, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2), "It still counts as one ordinary active attack.");
        }

        [Test]
        public void ExtremeManaSaturatesQuoteAndExecutionWithoutOverflowOrLoop()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", int.MaxValue); Put(s);
            s.Battle.EnemyBlock = int.MaxValue;
            CoreBattleCard card = s.GetBattleCard(Index());
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(int.MaxValue));
            Assert.That(Compact(s.BattleCardFaceDescription(card)), Does.Contain("10+5X"));
            int hull = s.Battle.EnemyHull;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
        }

        private static CoreVoyageSession Start(int level = 0)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureCardUpgrades(new Dictionary<string, int> { { Id, level } });
            s.SetSeed("ARCANE-MANA-BURST-20261024"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 1000);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            return s;
        }

        private static int Index(string id = Id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession s) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index()); }
        private static void SetSchool(CoreVoyageSession s, string name, int value) => typeof(CoreSchoolBattleState).GetProperty(name).SetValue(s.Battle.Schools, value);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
    }
}
