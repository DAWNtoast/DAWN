using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    /// <summary>Exercise normal play/turn/choice entry points, rather than calling private executors.</summary>
    public sealed class CoreBoardingFireArcaneThunderTests
    {
        [Test]
        public void ThunderCombo_FromThreeEnergyAndZeroChain_DealsFortyTwoAndFinishesAtZero()
        {
            var s = Battle("thunder_01", "thunder_02", "thunder_13");
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "thunder_01"); Play(s, "thunder_02"); Play(s, "thunder_13");
            Assert.That(enemy - s.Battle.EnemyBoardingHealth, Is.EqualTo(42));
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [TestCase("arcane_04", 42)]
        [TestCase("arcane_06", 25)]
        public void ArcaneImmediateAttacks_ConsumeAllMana_EvenWhenOptionsRequestZero(string key, int expectedDamage)
        {
            var s = Battle(key); SetMana(s, 3);
            var choice = s.GetBoardingPlayChoice(0);
            Assert.That(choice.RequiresChoice, Is.False);
            Assert.That(choice.MinResource, Is.EqualTo(choice.MaxResource), "No resource amount slider is allowed.");
            int enemy = s.Battle.EnemyBoardingHealth;
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { ResourceAmount = 0, SnapshotToken = choice.SnapshotToken }), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(enemy - s.Battle.EnemyBoardingHealth, Is.EqualTo(expectedDamage));
        }

        [Test]
        public void ArcaneDelayedAttack_ConsumesAllManaAndLocksSixty()
        {
            var s = Battle("arcane_11"); SetMana(s, 3);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "arcane_11");
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(60));
        }

        [Test]
        public void BurnPayment_UsesSelectedCardsAndBurnedAttackCannotBeRecovered()
        {
            var s = Battle("fire_06", "arcane_01", "arcane_14"); SetMana(s, 1);
            int burned = Index("arcane_01");
            s.Battle.DiscardPile.Add(Index("arcane_07"));
            var choice = s.GetBoardingPlayChoice(0);
            Assert.That(choice.Kind, Is.EqualTo(CoreBoardingChoiceKind.HandBurn));
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions
            {
                SnapshotToken = choice.SnapshotToken,
                SelectionIndexes = new List<int> { 1 },
            }), Is.True);
            Assert.That(s.Battle.Boarding.EnemyBurn.Sum(batch => batch.Amount), Is.EqualTo(5));
            Assert.That(s.Battle.Boarding.ExhaustedCards, Does.Contain(burned));
            Assert.That(s.Battle.Hand.Concat(s.Battle.DrawPile).Concat(s.Battle.DiscardPile), Has.No.Member(burned));
            int recoverSlot = s.Battle.Hand.IndexOf(Index("arcane_14"));
            var recover = s.GetBoardingPlayChoice(recoverSlot);
            Assert.That(recover.Candidates.Select(candidate => candidate.CardIndex), Has.No.Member(burned));
            Assert.That(recover.Candidates.Select(candidate => candidate.CardIndex), Does.Contain(Index("arcane_07")));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Hand.Concat(s.Battle.DrawPile).Concat(s.Battle.DiscardPile), Has.No.Member(burned));
        }

        [Test]
        public void BurnVariableAmount_ComesFromSelectedCards_NotResourceAmount()
        {
            var s = Battle("fire_07", "basic_01", "basic_04", "basic_08");
            var choice = s.GetBoardingPlayChoice(0);
            Assert.That(choice.MinSelection, Is.EqualTo(1));
            Assert.That(choice.MaxSelection, Is.EqualTo(3));
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions
            {
                SnapshotToken = choice.SnapshotToken, ResourceAmount = 99,
                SelectionIndexes = new List<int> { 1, 2 },
            }), Is.True);
            Assert.That(s.Battle.Boarding.EnemyBurn.Sum(batch => batch.Amount), Is.EqualTo(10));
            Assert.That(s.Battle.Boarding.ExhaustedCards.Count, Is.EqualTo(2));
        }

        [Test]
        public void SelfBurnTransfer_MovesAtMostFourLongestLivedLayersWithoutRefreshingDuration()
        {
            var s = Battle("fire_15");
            s.Battle.Boarding.SelfBurn.Add(new CoreBoardingTimedStacks(2, 1));
            s.Battle.Boarding.SelfBurn.Add(new CoreBoardingTimedStacks(3, 2));
            int enemy = s.Battle.EnemyBoardingHealth;
            var choice = s.GetBoardingPlayChoice(0);
            Assert.That(choice.RequiresChoice, Is.False);
            Play(s, "fire_15");
            Assert.That(s.Battle.Boarding.SelfBurn.Sum(batch => batch.Amount), Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.SelfBurn.Single().Remaining, Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.EnemyBurn.Where(batch => batch.Remaining == 2).Sum(batch => batch.Amount), Is.EqualTo(3));
            Assert.That(s.Battle.Boarding.EnemyBurn.Where(batch => batch.Remaining == 1).Sum(batch => batch.Amount), Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(enemy - s.Battle.EnemyBoardingHealth, Is.EqualTo(4));
            Assert.That(s.Battle.Boarding.SelfBurn, Is.Empty);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(enemy - s.Battle.EnemyBoardingHealth, Is.EqualTo(7));
            Assert.That(s.Battle.Boarding.EnemyBurn, Is.Empty);
        }

        [Test]
        public void ResolveOldestDelayed_RemovesQueueEntryAndNeverTriggersAgain()
        {
            var s = Battle("arcane_11", "arcane_03", "arcane_15");
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "arcane_11"); // no mana: 36 delayed
            Play(s, "arcane_03"); // gains 3 mana
            int collectSlot = s.Battle.Hand.IndexOf(Index("arcane_15"));
            Assert.That(s.GetBoardingPlayChoice(collectSlot).RequiresChoice, Is.False);
            Play(s, "arcane_15");
            Assert.That(enemy - s.Battle.EnemyBoardingHealth, Is.EqualTo(36));
            Assert.That(s.Battle.Boarding.DelayedDamage, Is.Empty);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(enemy - s.Battle.EnemyBoardingHealth, Is.EqualTo(36));
            Assert.That(s.Battle.Boarding.DelayedDamage, Is.Empty);
        }

        [Test]
        public void ReadingPreviewAndCancellation_DoNotPayOrMoveAnyCards()
        {
            var s = Battle("arcane_09"); SetMana(s, 1);
            s.Battle.DrawPile.AddRange(new[] { Index("basic_01"), Index("basic_02"), Index("basic_03"), Index("basic_04"), Index("basic_05") });
            s.Battle.DiscardPile.Add(Index("basic_06"));
            int[] hand = s.Battle.Hand.ToArray(), draw = s.Battle.DrawPile.ToArray(), discard = s.Battle.DiscardPile.ToArray();
            int energy = s.Battle.Energy, mana = s.Battle.Schools.AvailableMana;
            var first = s.GetBoardingPlayChoice(0);
            var reopened = s.GetBoardingPlayChoice(0); // Dismissing the first chooser makes no session call.
            Assert.That(first.Kind, Is.EqualTo(CoreBoardingChoiceKind.DrawChoose));
            Assert.That(first.Candidates.Count, Is.EqualTo(5));
            Assert.That(reopened.Candidates.Select(candidate => candidate.CardIndex), Is.EqualTo(first.Candidates.Select(candidate => candidate.CardIndex)));
            Assert.That(s.Battle.Hand, Is.EqualTo(hand)); Assert.That(s.Battle.DrawPile, Is.EqualTo(draw));
            Assert.That(s.Battle.DiscardPile, Is.EqualTo(discard));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy)); Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(mana));
            int[] chosen = reopened.Candidates.Take(3).Select(candidate => candidate.Value).ToArray();
            int[] expected = reopened.Candidates.Take(3).Select(candidate => candidate.CardIndex).ToArray();
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions
            {
                SnapshotToken = reopened.SnapshotToken, SelectionIndexes = chosen.ToList(),
            }), Is.True);
            Assert.That(s.Battle.Hand, Is.EquivalentTo(expected));
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(s.Battle.Energy, Is.EqualTo(energy - 2));
        }

        [Test]
        public void FireHealing_RequiresPowderWithMedicine_ButNoPowderWithoutMedicine()
        {
            var s = Battle("fire_05"); s.Battle.PlayerBoardingHealth -= 50;
            s.Cargo.Medicine = 1; s.Cargo.Powder = 0;
            int energy = s.Battle.Energy, health = s.Battle.PlayerBoardingHealth;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(energy)); Assert.That(s.Cargo.Medicine, Is.EqualTo(1));
            s.Cargo.Powder = 1; Play(s, "fire_05");
            Assert.That(s.Cargo.Powder, Is.Zero); Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth - health, Is.EqualTo(10));

            var fallback = Battle("fire_05"); fallback.Battle.PlayerBoardingHealth -= 50;
            fallback.Cargo.Medicine = 0; fallback.Cargo.Powder = 0;
            health = fallback.Battle.PlayerBoardingHealth; Play(fallback, "fire_05");
            Assert.That(fallback.Battle.PlayerBoardingHealth - health, Is.EqualTo(2));
            Assert.That(fallback.Cargo.Powder, Is.Zero);
        }

        [Test]
        public void SpellSeal_RetainsOnlySelectedPhysicalCopy_AndDoesNotResetSameNameSurcharge()
        {
            var s = Battle("arcane_12", "basic_03"); SetMana(s, 1);
            s.Battle.DrawPile.Add(Index("basic_03"));
            var choice = s.GetBoardingPlayChoice(0);
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions
            {
                SnapshotToken = choice.SnapshotToken,
                SelectionIndexes = new List<int> { 1 },
            }), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            int heavy = Index("basic_03");
            int[] slots = Enumerable.Range(0, s.Battle.Hand.Count).Where(slot => s.Battle.Hand[slot] == heavy).ToArray();
            Assert.That(slots.Length, Is.EqualTo(2));
            Assert.That(slots.Select(s.GetBoardingHandEnergyCost).OrderBy(cost => cost), Is.EqualTo(new[] { 2, 3 }));
            int discounted = slots.Single(slot => s.GetBoardingHandEnergyCost(slot) == 2);
            s.Battle.Energy = 10;
            Assert.That(s.PlayCard(discounted), Is.True);
            int other = s.Battle.Hand.IndexOf(heavy);
            Assert.That(s.GetBoardingHandEnergyCost(other), Is.EqualTo(4), "The unselected copy pays its full base 3 plus the same-name surcharge.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThunderWeakness_CannotCarryItsOldStrengthAfterConsumptionOrExpiry(bool expiresWithoutAttack)
        {
            var s = Battle("thunder_12");
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 20, 0, 0, 28, 1, 1, healthMultiplier: 1);
            s.Battle.PhaseTurn = expiresWithoutAttack ? 2 : 1;
            typeof(CoreSchoolBattleState).GetProperty("ThunderChain").SetValue(s.Battle.Schools, 6);
            Play(s, "thunder_12");
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.EqualTo(5));
            int before = s.Battle.PlayerBoardingHealth;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(before - (expiresWithoutAttack ? 0 : 15)));
            Assert.That(s.Battle.Boarding.ThunderWeaknessTurns, Is.Zero);
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.Zero);
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            s.Battle.PhaseTurn = 1;
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index("thunder_12"));
            Play(s, "thunder_12");
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.EqualTo(2), "A fresh zero-chain strike cannot inherit the spent five-point debuff.");
            before = s.Battle.PlayerBoardingHealth;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(before - 18));
        }
        private static CoreVoyageSession Battle(params string[] keys)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.SetSeed("BOARDING-FIRE-ARCANE-THUNDER");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 1000);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.Hand.AddRange(keys.Select(Index));
            s.Battle.Energy = 3;
            s.Cargo.Powder = 20; s.Cargo.Medicine = 0;
            SetMana(s, 0);
            return s;
        }

        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);

        private static void SetMana(CoreVoyageSession s, int amount)
        {
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, amount);
            typeof(CoreSchoolBattleState).GetProperty("TemporaryMana").SetValue(s.Battle.Schools, 0);
        }

        private static void Play(CoreVoyageSession s, string key)
        {
            int slot = s.Battle.Hand.IndexOf(Index(key));
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), key + " must be in hand");
            Assert.That(s.PlayCard(slot), Is.True, key + ": " + s.CardUnavailableReason(slot));
        }
    }
}


