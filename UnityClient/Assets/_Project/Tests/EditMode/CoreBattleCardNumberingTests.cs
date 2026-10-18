using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardNumberingTests
    {
        [TestCase("", 1)]
        [TestCase("fire", 2)]
        [TestCase("arcane", 3)]
        [TestCase("curse", 4)]
        [TestCase("tide", 5)]
        [TestCase("thunder", 6)]
        [TestCase("blood", 7)]
        public void EveryCurrentOrdinaryCardUsesItsSchoolNumberRange(string school, int thousands)
        {
            CoreBattleCard[] cards = CoreVoyageSession.CardLibrary.Where(card =>
                card.AllowedStages != NavalBattleStages.None && card.SchoolId == school &&
                card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0).ToArray();
            Assert.That(cards, Is.Not.Empty);
            foreach (CoreBattleCard card in cards)
            {
                Assert.That(CoreBattleCardNumbering.Number(card) / 1000, Is.EqualTo(thousands), card.Id);
                Assert.That(CoreBattleCardNumbering.Label(card).Length, Is.EqualTo(4), card.Id);
            }
        }

        [Test]
        public void SpecialSourcesKeepTheirOwnRangesEvenWhenTheyBelongToASchool()
        {
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
                Assert.That(CoreBattleCardNumbering.Number(definition.Card) / 1000,
                    Is.EqualTo(definition.SourceKind == CoreSpecialCardSourceKind.Crew ? 8 : 9), definition.Card.Id);
            Assert.That(CoreSpecialCardCatalog.All.Any(definition => definition.Card.SchoolId == "blood"), Is.True);
        }

        [Test]
        public void EveryCollectibleIdentityHasAUniqueFourDigitNumber()
        {
            var used = new HashSet<int>();
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
            {
                if (card.AllowedStages == NavalBattleStages.None ||
                    (card.AllowedStages == NavalBattleStages.Boarding && card.RequiredCrew.Length == 0 &&
                    card.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(card))) continue;
                int number = CoreBattleCardNumbering.Number(card);
                Assert.That(number, Is.InRange(1001, 9999), card.Id);
                Assert.That(used.Add(number), Is.True, "Duplicate number " + number + ": " + card.Id);
            }
            Assert.That(CoreBattleCardNumbering.Number("missing_card"), Is.Zero);
            Assert.That(CoreBattleCardNumbering.Number((CoreBattleCard)null), Is.Zero);
        }

        [Test]
        public void CompatibilityAliasesAndUpgradeLevelsKeepTheSameVisibleIdentity()
        {
            var session = new CoreVoyageSession();
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                Assert.That(CoreBattleCardNumbering.Number(session.GetBattleCardAtLevel(index, 3)),
                    Is.EqualTo(CoreBattleCardNumbering.Number(card)), card.Id);
                if (card.AllowedStages != NavalBattleStages.Boarding || card.RequiredCrew.Length > 0 ||
                    card.RequiredComponent.Length > 0 || !CoreBoardingCardCatalog.IsLegacyAlias(card)) continue;
                CoreBattleCard current = CoreBoardingCardCatalog.FindByKey(card.BoardingSpec.Key);
                Assert.That(CoreBattleCardNumbering.Number(card), Is.EqualTo(CoreBattleCardNumbering.Number(current)), card.Id);
            }
            Assert.That(CoreBattleCardNumbering.Number("card_parallel_course"),
                Is.EqualTo(CoreBattleCardNumbering.Number("card_approach")));
        }

        [Test]
        public void BrowsingOrderGroupsSchoolsWithoutChangingSavedLibraryOrDeckOrder()
        {
            var session = new CoreVoyageSession();
            string[] originalIds = CoreVoyageSession.CardLibrary.Select(card => card.Id).ToArray();
            int[][] originalDecks = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Select(stage => session.GetBattleDeck(stage).ToArray()).ToArray();
            var browsing = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Where(index => CoreBattleCardNumbering.Number(CoreVoyageSession.CardLibrary[index]) > 0).ToList();
            browsing.Reverse();
            browsing.Sort(CoreBattleCardNumbering.CompareLibraryIndices);
            int[] numbers = browsing.Select(index => CoreBattleCardNumbering.Number(CoreVoyageSession.CardLibrary[index])).ToArray();
            Assert.That(numbers, Is.Ordered);
            CollectionAssert.AreEqual(originalIds, CoreVoyageSession.CardLibrary.Select(card => card.Id));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                CollectionAssert.AreEqual(originalDecks[(int)stage], session.GetBattleDeck(stage));
            Assert.That(originalIds[0], Is.EqualTo("card_chain_shot"));
            Assert.That(originalIds[27], Is.EqualTo("card_component_bow"));
        }
    }
}
