using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingTransactionTests
    {
        [Test]
        public void StaleSelection_RejectsBeforeCostsOrCardMovement()
        {
            CoreVoyageSession s = Battle();
            Hand(s, "fire_06", "basic_01", "basic_02");
            CoreBoardingPlayChoice choice = s.GetBoardingPlayChoice(0);
            var options = new CoreBoardingPlayOptions { SnapshotToken = choice.SnapshotToken };
            options.SelectionIndexes.Add(1);
            s.Battle.Energy--; // The UI snapshot must now be invalid, even though the play is still affordable.
            string before = Snapshot(s);
            Assert.That(s.GetBoardingChoiceUnavailableReason(0, options), Does.Contain("变化"));
            Assert.That(s.PlayCard(0, options), Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.GetBattleCardEnergyCost(Index("fire_06")), Is.EqualTo(1));
        }

        [Test]
        public void DuplicateBurnSelections_RejectBeforePayingOrExhausting()
        {
            CoreVoyageSession s = Battle();
            Hand(s, "fire_07", "basic_01", "basic_02");
            var options = new CoreBoardingPlayOptions { SnapshotToken = s.GetBoardingPlayChoice(0).SnapshotToken };
            options.SelectionIndexes.AddRange(new[] { 1, 1 });
            string before = Snapshot(s);
            Assert.That(s.PlayCard(0, options), Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.Battle.Boarding.ExhaustedCards, Is.Empty);
        }

        [Test]
        public void BurnSelectionsOnBothSidesOfPlayedSlot_ExhaustExactlyThosePhysicalCards()
        {
            CoreVoyageSession s = Battle();
            Hand(s, "basic_01", "fire_07", "basic_02", "basic_03");
            var options = new CoreBoardingPlayOptions { SnapshotToken = s.GetBoardingPlayChoice(1).SnapshotToken };
            options.SelectionIndexes.AddRange(new[] { 0, 3 });
            int[] beforeCards = PhysicalCards(s);
            Assert.That(s.PlayCard(1, options), Is.True);
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index("basic_02") }));
            Assert.That(s.Battle.Boarding.ExhaustedCards, Is.EquivalentTo(new[] { Index("basic_01"), Index("basic_03") }));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(10));
            Assert.That(PhysicalCards(s), Is.EqualTo(beforeCards));
        }

        [Test]
        public void ReadingPreview_IsRepeatableAndDoesNotConsumeRandomnessOrDuplicateCards()
        {
            CoreVoyageSession s = Battle();
            Hand(s, "arcane_09");
            Mana(s, 1);
            s.Battle.DrawPile.Clear(); s.Battle.DrawPile.Add(Index("basic_01"));
            s.Battle.DiscardPile.Clear();
            s.Battle.DiscardPile.AddRange(new[] { Index("basic_02"), Index("basic_03"), Index("basic_04") });
            int[] beforeCards = PhysicalCards(s);
            string before = Snapshot(s);
            uint random = RandomState(s);
            CoreBoardingPlayChoice first = s.GetBoardingPlayChoice(0);
            for (int preview = 0; preview < 5; preview++)
            {
                CoreBoardingPlayChoice next = s.GetBoardingPlayChoice(0);
                Assert.That(next.Candidates.Select(c => c.CardIndex), Is.EqualTo(first.Candidates.Select(c => c.CardIndex)));
                Assert.That(next.SnapshotToken, Is.EqualTo(first.SnapshotToken));
                Assert.That(Snapshot(s), Is.EqualTo(before));
            }
            Assert.That(first.Candidates.Count, Is.EqualTo(5));
            var options = new CoreBoardingPlayOptions { SnapshotToken = first.SnapshotToken };
            options.SelectionIndexes.AddRange(new[] { 0, 2, 4 });
            Assert.That(s.PlayCard(0, options), Is.True);
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(3));
            Assert.That(PhysicalCards(s), Is.EqualTo(beforeCards));
            Assert.That(PhysicalCards(s).Count(value => value == Index("arcane_09")), Is.EqualTo(1));
            Assert.That(RandomState(s), Is.Not.EqualTo(random), "Only the committed recycling shuffle advances the stream.");
        }

        [Test]
        public void Reading_UnselectedCardsGoBelowUntouchedDeckInTheirOriginalOrder()
        {
            CoreVoyageSession s = Battle();
            Hand(s, "arcane_09"); Mana(s, 1);
            s.Battle.DrawPile.Clear();
            s.Battle.DrawPile.AddRange(new[] { Index("basic_01"), Index("basic_02"), Index("basic_03"),
                Index("basic_04"), Index("basic_05"), Index("basic_06") });
            s.Battle.DiscardPile.Clear();
            int[] beforeCards = PhysicalCards(s);
            CoreBoardingPlayChoice choice = s.GetBoardingPlayChoice(0);
            Assert.That(choice.Candidates.Select(c => c.CardIndex), Is.EqualTo(new[] {
                Index("basic_06"), Index("basic_05"), Index("basic_04"), Index("basic_03"), Index("basic_02") }));
            var options = new CoreBoardingPlayOptions { SnapshotToken = choice.SnapshotToken };
            options.SelectionIndexes.AddRange(new[] { 1, 3, 4 });
            // A caller cannot reorder the remainder: the design now specifies the original order.
            options.BottomOrderIndexes.AddRange(new[] { 2, 0 });
            Assert.That(s.PlayCard(0, options), Is.True);
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index("basic_05"), Index("basic_03"), Index("basic_02") }));
            Assert.That(s.Battle.DrawPile, Is.EqualTo(new[] { Index("basic_04"), Index("basic_06"), Index("basic_01") }));
            Assert.That(s.Battle.DiscardPile, Is.EqualTo(new[] { Index("arcane_09") }));
            Assert.That(PhysicalCards(s), Is.EqualTo(beforeCards));
        }

        [Test]
        public void OldAliasAndCanonicalCard_ShareEscalatingCostsAndResetTogether()
        {
            CoreVoyageSession s = Battle();
            int canonical = Index("basic_01"), alias = IndexById("card_crew_thrust");
            s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(new[] { canonical, alias }); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.GetBoardingHandEnergyCost(0), Is.EqualTo(2));
            Assert.That(s.GetBattleCardEnergyCost(canonical), Is.EqualTo(2));
            Assert.That(s.GetBattleCardEnergyCost(alias), Is.EqualTo(2));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(97));
            Assert.That(s.GetBattleCardEnergyCost(canonical), Is.EqualTo(3));
            s.Battle.PhaseTurn = 1; Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.GetBattleCardEnergyCost(canonical), Is.EqualTo(1));
            Assert.That(s.GetBattleCardEnergyCost(alias), Is.EqualTo(1));
        }

        [Test]
        public void All103CanonicalDesigns_AreAvailableAndOnlySevenRequireOtherCardSelection()
        {
            CoreVoyageSession s = Battle();
            var available = new HashSet<int>(s.GetAvailableBattleCards(NavalBattleStage.Boarding));
            var selectedKeys = new HashSet<string> { "fire_06", "fire_07", "fire_08", "fire_09", "arcane_09", "arcane_12", "arcane_14" };
            Assert.That(CoreBoardingCardCatalog.All.Count, Is.EqualTo(103));
            Assert.That(CoreBoardingCardCatalog.All.Select(card => card.Id).Distinct().Count(), Is.EqualTo(103));
            foreach (CoreBattleCard card in CoreBoardingCardCatalog.All)
            {
                string key = card.BoardingSpec.Key;
                int index = Index(key);
                Assert.That(available.Contains(index), Is.True, key);
                Assert.That(s.GetBaseBattleCardOwnedCopies(index), Is.GreaterThan(0), key);
                Hand(s, key, "basic_01");
                CoreBoardingPlayChoice choice = s.GetBoardingPlayChoice(0);
                Assert.That(choice, Is.Not.Null, key);
                Assert.That(choice.RequiresChoice, Is.EqualTo(selectedKeys.Contains(key)), key);
                Assert.That(choice.MinResource, Is.EqualTo(choice.MaxResource), key + " must not expose resource amount choices");
                Assert.That(choice.AllowedResourceAmounts, Is.Empty, key);
            }
        }

        [Test]
        public void RetainedGuardBatches_ExpireIndependentlyWithoutRefreshingOlderShield()
        {
            CoreVoyageSession s = Battle();
            Mana(s, 3); Hand(s, "arcane_08"); Assert.That(s.PlayCard(0), Is.True);
            s.Battle.PhaseTurn = 1; Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(4));
            Hand(s, "arcane_08"); Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(9));
            s.Battle.PhaseTurn = 1; Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(4), "Only the second cast survives this reset.");
            Assert.That(s.Battle.Boarding.RetainedGuardBatches.Count, Is.EqualTo(1));
            s.Battle.PhaseTurn = 1; Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Boarding.RetainedGuardBatches, Is.Empty);
        }

        [Test]
        public void MissingSelectionList_IsRejectedWithoutThrowingOrPaying()
        {
            CoreVoyageSession s = Battle(); Hand(s, "basic_01");
            var options = new CoreBoardingPlayOptions { SelectionIndexes = null };
            string before = Snapshot(s);
            Assert.That(s.GetBoardingChoiceUnavailableReason(0, options), Is.Not.Empty);
            Assert.That(s.PlayCard(0, options), Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        private static CoreVoyageSession Battle()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.SetSeed("BOARDING-TRANSACTION-TEST");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.EnemyBlock = 0;
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 0, 0, 0, 28, 1, 1, healthMultiplier: 1);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Powder = 100; s.Cargo.Medicine = 0;
            return s;
        }

        private static int Index(string key) => IndexById("card_boarding_" + key);
        private static int IndexById(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static void Hand(CoreVoyageSession s, params string[] keys)
        { s.Battle.Hand.Clear(); s.Battle.Hand.AddRange(keys.Select(Index)); s.Battle.Energy = 100; }
        private static void Mana(CoreVoyageSession s, int amount) => typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, amount);
        private static uint RandomState(CoreVoyageSession s) => ((Mulberry32Random)typeof(CoreVoyageSession)
            .GetField("voyageRandom", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s)).State;
        private static int[] PhysicalCards(CoreVoyageSession s) => s.Battle.Hand.Concat(s.Battle.DrawPile)
            .Concat(s.Battle.DiscardPile).Concat(s.Battle.Boarding.ExhaustedCards).OrderBy(value => value).ToArray();
        private static string Snapshot(CoreVoyageSession s) => string.Join("|", new[] {
            s.Battle.Energy.ToString(), s.Battle.PlayerBoardingHealth.ToString(), s.Battle.EnemyBoardingHealth.ToString(),
            s.Battle.PlayerBlock.ToString(), s.Battle.EnemyBlock.ToString(), s.Cargo.Powder.ToString(), s.Cargo.Medicine.ToString(),
            s.Battle.Schools.AvailableMana.ToString(), s.Battle.Boarding.CurseMarks.ToString(), s.Battle.Boarding.TidePower.ToString(),
            s.Battle.Schools.ThunderChain.ToString(), RandomState(s).ToString(),
            string.Join(",", s.Battle.Hand), string.Join(",", s.Battle.DrawPile), string.Join(",", s.Battle.DiscardPile),
            string.Join(",", s.Battle.Boarding.ExhaustedCards), s.Battle.CardsPlayedThisTurn.ToString() });
    }
}
