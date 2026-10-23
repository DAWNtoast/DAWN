using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Tests.EditMode
{
    public sealed class CoreBoardingCardCatalogTests
    {
        [Test]
        public void CanonicalDesigns_HaveThirteenNeutralAndFifteenPerSchool()
        {
            Assert.That(CoreBoardingCardCatalog.All.Count, Is.EqualTo(103));
            Assert.That(CoreBoardingCardCatalog.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(103));
            Assert.That(CoreBoardingCardCatalog.All.Select(c => c.BoardingSpec.Key).Distinct().Count(), Is.EqualTo(103));
            Assert.That(CoreBoardingCardCatalog.All.Count(c => c.SchoolId.Length == 0), Is.EqualTo(13));
            foreach (string school in new[] { "fire", "arcane", "curse", "tide", "thunder", "blood" })
                Assert.That(CoreBoardingCardCatalog.All.Count(c => c.SchoolId == school), Is.EqualTo(15), school);
            foreach (CoreBattleCard card in CoreBoardingCardCatalog.All)
            {
                Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Boarding), card.Id);
                Assert.That(card.UpgradeLevel, Is.Zero, card.Id);
                Assert.That(card.RequiredCrew + card.RequiredComponent, Is.Empty, card.Id);
                Assert.That(card.ArtId, Is.Not.Empty, card.Id);
                Assert.That(card.Description, Does.Not.Contain("{"), card.Id);
                Assert.That(CoreBoardingCardCatalog.Find(card.Id), Is.SameAs(card));
            }
        }

        [TestCase("basic_01", 1, 8, 0, 0, 0)]
        [TestCase("basic_02", 2, 20, 0, 0, 0)]
        [TestCase("basic_03", 3, 35, 0, 0, 0)]
        [TestCase("basic_04", 1, 0, 4, 0, 0)]
        [TestCase("basic_05", 2, 0, 10, 0, 0)]
        [TestCase("basic_06", 1, 0, 0, 6, 0)]
        [TestCase("basic_07", 2, 0, 0, 5, 0)]
        [TestCase("basic_08", 2, 0, 0, 0, 3)]
        [TestCase("basic_09", 0, 0, 0, 0, 0)]
        [TestCase("basic_10", 2, 0, 0, 0, 0)]
        [TestCase("basic_11", 1, 4, 0, 0, 1)]
        [TestCase("basic_12", 1, 0, 2, 0, 1)]
        [TestCase("basic_13", 1, 12, 0, 0, 0)]
        public void Baseline_UsesAgreedUnupgradedNumbers(string key, int energy, int damage, int block, int repair, int draw)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(new[] { card.Energy, card.Damage, card.Block, card.Repair, card.Draw },
                Is.EqualTo(new[] { energy, damage, block, repair, draw }));
        }

        [Test]
        public void RegisteringDesigns_PreservesHistoricPositionsAndIsIdempotent()
        {
            CoreBattleCard old = new CoreBattleCard("card_school_fire_b07", "旧火把", "登船", 1, 8, 0, 0, 0, 1,
                "旧规则", NavalBattleStages.Boarding, artId: "card_school_fire_b07", schoolId: "fire");
            CoreBattleCard marker = new CoreBattleCard("saved_marker", "炮战占位", "策略", 1, 0, 0, 0, 0, 0,
                "保留", NavalBattleStages.Naval);
            var cards = new List<CoreBattleCard> { old, marker };
            CoreBoardingCardCatalog.ApplyToLibrary(cards);
            Assert.That(cards.Count, Is.EqualTo(105));
            Assert.That(cards[0].Id, Is.EqualTo(old.Id));
            Assert.That(cards[0].BoardingSpec.Key, Is.EqualTo("fire_02"));
            Assert.That(cards[1], Is.SameAs(marker));
            Assert.That(cards[2].Id, Is.EqualTo("card_boarding_basic_01"));
            CoreBoardingCardCatalog.ApplyToLibrary(cards);
            Assert.That(cards.Count, Is.EqualTo(105));
        }

        [Test]
        public void SharedSupport_ChangesOnlyItsBoardingQuote()
        {
            CoreBattleCard shared = new CoreBattleCard("card_tactical_draw", "战术筹划", "策略", 1, 0, 0, 0, 2, 0,
                "抽2", NavalBattleStages.All, artId: "card_full_sail_turn");
            Assert.That(CoreBoardingCardCatalog.ReplaceLegacyBoarding(shared), Is.SameAs(shared));
            CoreBattleCard boarding = CoreBoardingCardCatalog.ForBoarding(shared);
            Assert.That(boarding.Id, Is.EqualTo(shared.Id));
            Assert.That(boarding.Energy, Is.EqualTo(2));
            Assert.That(boarding.Draw, Is.EqualTo(3));
            Assert.That(shared.Energy, Is.EqualTo(1));
            Assert.That(shared.Draw, Is.EqualTo(2));
            Assert.That(boarding.BoardingSpec.Key, Is.EqualTo("basic_08"));
        }

        [Test]
        public void NavalSourceCards_AreNeverOverwrittenByRetiredBoardingAliases()
        {
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_special_plating_starseal_panels");
            CoreBattleCard original = session.GetBattleCardAtLevel(index, 2);
            CoreBattleCard alias = CoreBoardingCardCatalog.ReplaceLegacyBoarding(original);
            Assert.That(alias, Is.SameAs(original));
            Assert.That(alias.Id, Is.EqualTo(original.Id));
            Assert.That(alias.ArtId, Is.EqualTo(original.ArtId));
            Assert.That(alias.RequiredComponent, Is.EqualTo(original.RequiredComponent));
            Assert.That(alias.UpgradeLevel, Is.EqualTo(2));
            Assert.That(alias.AllowedStages, Is.EqualTo(NavalBattleStages.Naval));
            Assert.That(alias.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ComponentFieldShelter));
            Assert.That(alias.BoardingSpec, Is.Null);
        }

        [Test]
        public void CurseDamage_IsNotAnAttack_AndThunderHasNoHealOrGuard()
        {
            Assert.That(CoreBoardingCardCatalog.FindByKey("curse_11").BoardingSpec.IsAttack, Is.False);
            Assert.That(CoreBoardingCardCatalog.FindByKey("curse_09").BoardingSpec.IsAttack, Is.False);
            foreach (CoreBattleCard card in CoreBoardingCardCatalog.All.Where(c => c.SchoolId == "thunder"))
                Assert.That(card.Block + card.Repair, Is.Zero, card.Id);
        }

        [Test]
        public void FixedPaymentRevision_UsesCommittedHealGuardAndLifeCostValues()
        {
            Assert.That(CoreBoardingCardCatalog.FindByKey("fire_05").Repair, Is.EqualTo(10));
            Assert.That(CoreBoardingCardCatalog.FindByKey("arcane_05").Repair, Is.EqualTo(13));
            Assert.That(CoreBoardingCardCatalog.FindByKey("curse_12").Repair, Is.EqualTo(13));
            Assert.That(CoreBoardingCardCatalog.FindByKey("curse_15").Block, Is.EqualTo(10));
            Assert.That(CoreBoardingCardCatalog.FindByKey("tide_11").Repair, Is.EqualTo(10));
            Assert.That(CoreBoardingCardCatalog.FindByKey("tide_12").Repair, Is.EqualTo(8));
            Assert.That(CoreBoardingCardCatalog.FindByKey("blood_02").Damage, Is.EqualTo(34));
            foreach (string key in new[] { "arcane_04", "arcane_06", "arcane_11", "tide_06", "tide_07" })
                Assert.That(CoreBoardingCardCatalog.FindByKey(key).Description, Does.Contain("X").And.Not.Contain("X为").And.Not.Contain("消耗全部"), key);
            Assert.That(CoreBoardingCardCatalog.FindByKey("fire_15").Description, Does.Contain("转移至多 4 层自燃"));
            Assert.That(CoreBoardingCardCatalog.FindByKey("arcane_15").Description, Is.EqualTo("立即结算一个延迟效果。"));
            Assert.That(CoreBoardingCardCatalog.FindByKey("tide_05").Description, Does.Contain("获得 6 点潮势"));
        }
    }
}
