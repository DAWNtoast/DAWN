using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator ComponentInventory_UnreadableRecordReturnsToMenuWithoutOverwritingProgress()
        {
            foreach (string invalid in new[] { "", "{broken", "{\"version\":99,\"items\":[]}", "{\"version\":2,\"items\":[null]}" })
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, invalid);
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(invalid));
                Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>(), Is.Null);
            }
            // Removing the invalid record is the same state as an explicitly reset
            // new game; normal legacy migration must still work afterwards.
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().OwnedComponents.Count, Is.EqualTo(5));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentInventory_MigratesLegacyFlagshipThenPreservesEmptySlotsAndQualityAcrossReload()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry rare = catalog.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Cannon), rare.Id);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentConditionKey(ComponentSlot.Cannon), 93);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(10));
            OwnedShipComponent item = controller.InstalledShipComponent(1, ComponentSlot.Cannon);
            string identity = item.InstanceId;
            Assert.That(item.ComponentId, Is.EqualTo(rare.Id));
            Assert.That(item.Quality, Is.EqualTo(0.93f).Within(0.0001f));
            Assert.That(controller.ShipComponent(0, ComponentSlot.Cannon).Id, Is.Not.EqualTo(rare.Id));
            Assert.That(controller.TryUnequipShipComponent(1, ComponentSlot.Cannon), Is.True);
            Assert.That(controller.TryUnequipShipComponent(1, ComponentSlot.Cannon), Is.False);
            Assert.That(controller.TryEquipOwnedComponent(0, identity), Is.True);
            Assert.That(controller.TryEquipOwnedComponent(1, identity), Is.False);
            Assert.That(controller.TryUnequipShipComponent(3, ComponentSlot.Bow), Is.False);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(10));
            Assert.That(controller.EquippedComponent(ComponentSlot.Cannon), Is.Null);
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Cannon).InstanceId, Is.EqualTo(identity));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Cannon).Quality, Is.EqualTo(0.93f).Within(0.0001f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentInventory_RefitsRefreshSpecialCardsAndRejectSailingAndUnownedShips()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtEntry rare = new ComponentArtCatalog().Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent first = controller.GrantOwnedComponent(rare, 0.96f);
            OwnedShipComponent second = controller.GrantOwnedComponent(rare, 1.09f);
            Assert.That(controller.TryEquipOwnedComponent(3, first.InstanceId), Is.False);
            Assert.That(controller.TryEquipOwnedComponent(0, first.InstanceId), Is.True);
            Assert.That(controller.TryEquipOwnedComponent(0, second.InstanceId), Is.True);
            Assert.That(first.ShipIndex, Is.EqualTo(-1));
            Assert.That(first.Quality, Is.EqualTo(0.96f));
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(rare.Id);
            Assert.That(System.Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Any(stage => controller.Session.GetBonusBattleCards(stage).Any(index => CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id)), Is.True);
            Assert.That(controller.TryUnequipShipComponent(0, ComponentSlot.Cannon), Is.True);
            Assert.That(System.Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Any(stage => controller.Session.GetBonusBattleCards(stage).Any(index => CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id)), Is.False);
            controller.Session.StartVoyage();
            Assert.That(controller.Session.Phase, Is.Not.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.TryEquipOwnedComponent(0, first.InstanceId), Is.False);
            Assert.That(controller.TryUnequipShipComponent(0, ComponentSlot.Bow), Is.False);
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(7));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
