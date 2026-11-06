using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreRetiredSourceCardTests
    {
        [TestCase(19, "card_navigator_course")]
        [TestCase(20, "card_carpenter_shoring")]
        [TestCase(21, "card_doctor_sutures")]
        [TestCase(22, "card_gunner_precision")]
        [TestCase(23, "card_cook_rations")]
        [TestCase(24, "card_diver_sabotage")]
        [TestCase(27, "card_component_bow")]
        [TestCase(28, "card_component_sail")]
        [TestCase(29, "card_component_cannon")]
        [TestCase(30, "card_component_plating")]
        [TestCase(31, "card_component_instrument")]
        public void RetiredGenericCards_KeepSavedSlotsButCannotBeAcquiredConfiguredOrPlayed(int index, string id)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
            Assert.That(card.Id, Is.EqualTo(id));
            Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.None));
            Assert.That(card.Name + card.Description + card.ArtId + card.RequiredCrew + card.RequiredComponent, Is.Empty);
            Assert.That(CoreVoyageSession.IsSpecialBattleCard(id), Is.False);
            Assert.That(CoreBattleCardNumbering.Number(id), Is.Zero);
            Assert.That(session.GetBattleCardAtLevel(index, 3), Is.Null);
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            session.AcquireSpecialCards(new[] { id });
            Assert.That(session.AcquiredSpecialCardIds, Has.No.Member(id));
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            session.ConfigureComponentBattleCards(new[] { "bow", "sail", "cannon", "plating", "instrument" });
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                Assert.That(session.GetAvailableBattleCards(stage), Has.No.Member(index));
                Assert.That(session.GetBonusBattleCards(stage), Has.No.Member(index));
                int[] invalid = session.GetBattleDeck(stage).ToArray();
                invalid[0] = index;
                Assert.That(session.SetBattleDeck(stage, invalid), Is.False);
                Assert.That(session.RestoreBattleDeck(stage, invalid), Is.True);
                Assert.That(session.GetBattleDeck(stage), Has.No.Member(index));
            }
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("retired_source_test", "测试船", "海战", "测试", true, false, 1, 70), false });
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            int energy = session.Battle.Energy;
            Assert.That(session.CanPlayCard(0), Is.False);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
        }

        [TestCase("card_navigator_course", "navigator", true)]
        [TestCase("card_carpenter_shoring", "carpenter", true)]
        [TestCase("card_doctor_sutures", "doctor", true)]
        [TestCase("card_gunner_precision", "gunner", true)]
        [TestCase("card_cook_rations", "cook", true)]
        [TestCase("card_diver_sabotage", "diver", true)]
        [TestCase("card_component_bow", "bow", false)]
        [TestCase("card_component_sail", "sail", false)]
        [TestCase("card_component_cannon", "cannon", false)]
        [TestCase("card_component_plating", "plating", false)]
        [TestCase("card_component_instrument", "instrument", false)]
        public void LegacyExperienceAndPaidRanks_MigrateWithoutReacquiringDeletedCards(string retiredId, string legacySource, bool crew)
        {
            CoreSpecialCardDefinition current = CoreSpecialCardCatalog.All.First(definition =>
                definition.SourceKind == (crew ? CoreSpecialCardSourceKind.Crew : CoreSpecialCardSourceKind.Component) &&
                definition.LegacySourceKey == legacySource);
            var session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { retiredId, 360 } });
            Assert.That(session.GetSpecialCardExperience(retiredId), Is.EqualTo(360));
            Assert.That(session.AcquiredSpecialCardIds, Has.No.Member(retiredId));
            session.ConfigureSpecialCardSources(crew ? new[] { current.SourceId } : null,
                crew ? null : new[] { current.SourceId });
            Assert.That(session.GetSpecialCardExperience(current.Card.Id), Is.EqualTo(360));
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { retiredId, 3 } });
            Assert.That(session.GetSpecialCardExperience(current.Card.Id), Is.EqualTo(840));
            Assert.That(session.GetSpecialCardExperience(retiredId), Is.EqualTo(840));
            Assert.That(session.AcquiredSpecialCardIds, Is.EquivalentTo(new[] { current.Card.Id }));
            Assert.That(session.CardUpgradeLevels, Is.Empty);
            Assert.That(session.GetSpecialCardNextThreshold(retiredId), Is.Zero);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { retiredId, 120 } });
            Assert.That(session.GetSpecialCardExperience(current.Card.Id), Is.EqualTo(840));

            var paidFirst = new CoreVoyageSession();
            paidFirst.ConfigureCardUpgrades(new Dictionary<string, int> { { retiredId, 3 } });
            Assert.That(paidFirst.AcquiredSpecialCardIds, Is.Empty);
            paidFirst.ConfigureSpecialCardSources(crew ? new[] { current.SourceId } : null,
                crew ? null : new[] { current.SourceId });
            Assert.That(paidFirst.GetSpecialCardExperience(current.Card.Id), Is.EqualTo(840));
        }

        [TestCase("card_navigator_course", "navigator", true)]
        [TestCase("card_carpenter_shoring", "carpenter", true)]
        [TestCase("card_doctor_sutures", "doctor", true)]
        [TestCase("card_gunner_precision", "gunner", true)]
        [TestCase("card_cook_rations", "cook", true)]
        [TestCase("card_diver_sabotage", "diver", true)]
        [TestCase("card_component_bow", "bow", false)]
        [TestCase("card_component_sail", "sail", false)]
        [TestCase("card_component_cannon", "cannon", false)]
        [TestCase("card_component_plating", "plating", false)]
        [TestCase("card_component_instrument", "instrument", false)]
        public void LegacyVoyageCheckpoint_AcceptsRetiredHistoryWithoutRestoringDeletedCards(string retiredId, string legacySource, bool crew)
        {
            CoreSpecialCardDefinition current = CoreSpecialCardCatalog.All.First(definition =>
                definition.SourceKind == (crew ? CoreSpecialCardSourceKind.Crew : CoreSpecialCardSourceKind.Component) &&
                definition.LegacySourceKey == legacySource);
            var source = new CoreVoyageSession();
            source.ConfigureSpecialCardSources(crew ? new[] { current.SourceId } : null, crew ? null : new[] { current.SourceId });
            source.StartVoyage();
            CoreVoyageCheckpoint old = source.CaptureVoyageCheckpoint();
            old.acquiredSpecialCards = old.acquiredSpecialCards.Concat(new[] { retiredId }).ToArray();
            old.voyageSpecialCards = old.voyageSpecialCards.Concat(new[] { retiredId }).ToArray();
            old.specialCardExperience = new[] { new CoreVoyageValueCheckpoint { id = retiredId, value = 360 } };
            old.cardUpgradeLevels = new[] { new CoreVoyageValueCheckpoint { id = retiredId, value = 3 } };
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(old, out string reason), Is.True, reason);
            Assert.That(restored.AcquiredSpecialCardIds, Has.No.Member(retiredId));
            Assert.That(restored.GetSpecialCardExperience(retiredId), Is.EqualTo(840));
            Assert.That(restored.GetSpecialCardExperience(current.Card.Id), Is.EqualTo(840));
            Assert.That(restored.CardUpgradeLevels, Is.Empty);
            CoreVoyageCheckpoint saved = restored.CaptureVoyageCheckpoint();
            Assert.That(saved.voyageSpecialCards, Has.No.Member(retiredId));
            var nextLoad = new CoreVoyageSession();
            Assert.That(nextLoad.TryRestoreVoyageCheckpoint(saved, out reason), Is.True, reason);
            Assert.That(nextLoad.GetSpecialCardExperience(current.Card.Id), Is.EqualTo(840));
        }
    }
}
