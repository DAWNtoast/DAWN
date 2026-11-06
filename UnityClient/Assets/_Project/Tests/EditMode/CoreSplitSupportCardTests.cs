using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSplitSupportCardTests
    {
        [TestCase("card_energy_order", "card_boarding_basic_09", 219, 1001, 1013)]
        [TestCase("card_tactical_draw", "card_boarding_basic_08", 220, 1003, 1014)]
        public void SplitCards_KeepTheirIdentitiesAndPurchaseUpgradesIndependently(string shipId, string boardingId,
            int shipIndex, int shipNumber, int boardingNumber)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(2000, "断锚港");
            int boardingIndex = Index(boardingId);
            Assert.That(Index(shipId), Is.EqualTo(shipIndex));
            Assert.That(CoreBattleCardNumbering.Number(shipId), Is.EqualTo(shipNumber));
            Assert.That(CoreBattleCardNumbering.Number(boardingId), Is.EqualTo(boardingNumber));
            Assert.That(CoreVoyageSession.CardLibrary[shipIndex].AllowedStages, Is.EqualTo(NavalBattleStages.Ship));
            Assert.That(CoreVoyageSession.CardLibrary[boardingIndex].AllowedStages, Is.EqualTo(NavalBattleStages.Boarding));
            Assert.That(session.GetAvailableBattleCards(NavalBattleStage.Boarding), Has.No.Member(shipIndex));
            Assert.That(session.GetAvailableBattleCards(NavalBattleStage.Boarding), Has.Member(boardingIndex));
            Assert.That(session.GetBattleDeck(NavalBattleStage.Boarding), Has.No.Member(shipIndex));
            Assert.That(session.GetBattleDeck(NavalBattleStage.Boarding), Has.Member(boardingIndex));

            Assert.That(session.TryUpgradeBattleCard(boardingIndex, out string reason), Is.True, reason);
            Assert.That(session.GetCardUpgradeLevel(boardingId), Is.EqualTo(1));
            Assert.That(session.GetCardUpgradeLevel(shipId), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(shipIndex, out reason), Is.True, reason);
            Assert.That(session.GetCardUpgradeLevel(shipId), Is.EqualTo(1));
            Assert.That(session.GetCardUpgradeLevel(boardingId), Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(1760));
        }

        [TestCase("card_energy_order", "card_boarding_basic_09")]
        [TestCase("card_tactical_draw", "card_boarding_basic_08")]
        public void RestoreLegacyBoardingDeck_ReplacesOldIdentityAndEnforcesSharedCopyLimit(string shipId, string boardingId)
        {
            var session = new CoreVoyageSession();
            int oldIndex = Index(shipId), newIndex = Index(boardingId);
            int[] oldDeck = session.GetBattleDeck(NavalBattleStage.Boarding)
                .Select(index => index == newIndex ? oldIndex : index)
                .Concat(new[] { oldIndex, oldIndex, newIndex, newIndex }).ToArray();
            Assert.That(session.RestoreBattleDeck(NavalBattleStage.Boarding, oldDeck), Is.True);
            var restored = session.GetBattleDeck(NavalBattleStage.Boarding);
            Assert.That(restored, Has.No.Member(oldIndex));
            Assert.That(restored.Count(index => index == newIndex), Is.EqualTo(CoreVoyageSession.BaseBattleCardCopies));
            Assert.That(restored.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Boarding)), Is.True);
            Assert.That(session.SetBattleDeck(NavalBattleStage.Boarding, restored), Is.True);
        }

        [Test]
        public void SharedInvestment_MigratesOnceAndNeverDowngradesAnExistingBoardingUpgrade()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(new Dictionary<string, int>
            {
                { "card_energy_order", 1 }, { "card_boarding_basic_09", 3 },
                { "card_tactical_draw", 2 }, { "card_boarding_basic_08", 1 },
            });
            session.RestoreSplitSupportCardUpgrades(0);
            Assert.That(session.CardUpgradeLevels["card_boarding_basic_09"], Is.EqualTo(3));
            Assert.That(session.CardUpgradeLevels["card_boarding_basic_08"], Is.EqualTo(2));
            var changed = new Dictionary<string, int>(session.CardUpgradeLevels.ToDictionary(pair => pair.Key, pair => pair.Value));
            changed["card_tactical_draw"] = 3;
            session.ConfigureCardUpgrades(changed);
            session.RestoreSplitSupportCardUpgrades(0);
            Assert.That(session.CardUpgradeLevels["card_boarding_basic_08"], Is.EqualTo(2), "A second call in the same session must not reshare investment.");
            var restored = new CoreVoyageSession();
            restored.ConfigureCardUpgrades(session.CardUpgradeLevels);
            restored.RestoreSplitSupportCardUpgrades(CoreVoyageSession.CurrentSplitSupportCardVersion);
            Assert.That(restored.CardUpgradeLevels["card_boarding_basic_08"], Is.EqualTo(2), "The saved migration marker keeps later upgrades independent.");
        }

        [Test]
        public void LegacyVoyageCheckpoint_MigratesBoardingDeckAndSharedInvestmentOnlyOnce()
        {
            var source = new CoreVoyageSession();
            source.ConfigureCardUpgrades(new Dictionary<string, int>
            {
                { "card_energy_order", 1 }, { "card_tactical_draw", 2 }, { "card_boarding_basic_08", 1 },
            });
            source.StartVoyage();
            CoreVoyageCheckpoint old = source.CaptureVoyageCheckpoint();
            old.splitSupportCardVersion = 0;
            old.battleDecks[(int)NavalBattleStage.Boarding].cards = old.battleDecks[(int)NavalBattleStage.Boarding].cards
                .Select(index => index == Index("card_boarding_basic_08") ? Index("card_tactical_draw") :
                    index == Index("card_boarding_basic_09") ? Index("card_energy_order") : index).ToArray();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(old, out string reason), Is.True, reason);
            Assert.That(restored.CardUpgradeLevels["card_boarding_basic_08"], Is.EqualTo(2));
            Assert.That(restored.CardUpgradeLevels["card_boarding_basic_09"], Is.EqualTo(1));
            Assert.That(restored.GetBattleDeck(NavalBattleStage.Boarding), Has.No.Member(Index("card_tactical_draw")));
            CoreVoyageCheckpoint current = restored.CaptureVoyageCheckpoint();
            Assert.That(current.splitSupportCardVersion, Is.EqualTo(CoreVoyageSession.CurrentSplitSupportCardVersion));
            current.cardUpgradeLevels.Single(item => item.id == "card_tactical_draw").value = 3;
            var nextLoad = new CoreVoyageSession();
            Assert.That(nextLoad.TryRestoreVoyageCheckpoint(current, out reason), Is.True, reason);
            Assert.That(nextLoad.CardUpgradeLevels["card_tactical_draw"], Is.EqualTo(3));
            Assert.That(nextLoad.CardUpgradeLevels["card_boarding_basic_08"], Is.EqualTo(2));
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
