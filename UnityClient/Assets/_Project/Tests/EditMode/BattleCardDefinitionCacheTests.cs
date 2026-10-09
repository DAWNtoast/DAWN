using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleCardDefinitionCacheTests
    {
        [Test]
        public void PreviewingOtherLevelsCannotChangeEitherCaptainsSavedUpgradeOrExistingHandDefinition()
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i =>
                CoreVoyageSession.CardLibrary[i].Damage > 0 && !CoreVoyageSession.IsSpecialBattleCard(CoreVoyageSession.CardLibrary[i].Id));
            string id = CoreVoyageSession.CardLibrary[index].Id;
            var upgraded = new CoreVoyageSession();
            var newCaptain = new CoreVoyageSession();
            upgraded.ConfigureCardUpgrades(new Dictionary<string, int> { [id] = 2 });
            CoreBattleCard handDefinition = upgraded.GetBattleCard(index);
            int damage = handDefinition.Damage, energy = handDefinition.Energy;
            string description = handDefinition.Description;
            for (int level = 0; level <= 3; level++)
            {
                CoreBattleCard preview = newCaptain.GetBattleCardAtLevel(index, level);
                Assert.That(preview.UpgradeLevel, Is.EqualTo(level));
                Assert.That(newCaptain.GetBattleCard(index).UpgradeLevel, Is.Zero);
                Assert.That(upgraded.GetBattleCard(index), Is.SameAs(handDefinition));
            }
            upgraded.ConfigureCardUpgrades(new Dictionary<string, int> { [id] = 1 });
            Assert.That(upgraded.GetBattleCard(index).UpgradeLevel, Is.EqualTo(1));
            Assert.That(handDefinition.UpgradeLevel, Is.EqualTo(2));
            Assert.That(handDefinition.Damage, Is.EqualTo(damage));
            Assert.That(handDefinition.Energy, Is.EqualTo(energy));
            Assert.That(handDefinition.Description, Is.EqualTo(description));
            Assert.That(newCaptain.CardUpgradeLevels, Is.Empty);
        }

        [Test]
        public void SharedSpecialCardDefinitionDoesNotGrantAnotherCaptainsSourceOrVoyageExperience()
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i =>
                CoreVoyageSession.IsSpecialBattleCard(CoreVoyageSession.CardLibrary[i].Id) &&
                CoreVoyageSession.CardLibrary[i].AllowedStages != NavalBattleStages.None);
            var first = new CoreVoyageSession();
            var second = new CoreVoyageSession();
            second.ConfigureSpecialCardSources(System.Array.Empty<string>(), System.Array.Empty<string>());
            CoreBattleCard preview = first.GetBattleCardAtLevel(index, 3);
            Assert.That(second.GetBattleCardAtLevel(index, 3), Is.SameAs(preview));
            Assert.That(second.GetBattleCard(index).UpgradeLevel, Is.Zero);
            Assert.That(second.AcquiredSpecialCardIds, Is.Empty);
            Assert.That(second.GetSpecialCardExperience(preview.Id), Is.Zero);
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                CollectionAssert.DoesNotContain(second.GetBonusBattleCards(stage), index);
        }
    }
}
