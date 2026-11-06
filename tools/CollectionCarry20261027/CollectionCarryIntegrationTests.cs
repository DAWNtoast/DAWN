using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CollectionCarryValidation
{
    // A resource-free integration fixture: the real controller stays inactive so
    // none of its scene/UI startup runs. Actual persistence methods are exercised.
    public sealed class CollectionCarryIntegrationTests
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string Origin = "断锚港", Destination = "白贝港";
        private const string Compass = CoreWarehouseCollection.TutorialTreasureId;
        private const string Bell = CoreWarehouseCollection.BoatswainBellTreasureId;
        private readonly List<GameObject> hosts = new List<GameObject>();

        [SetUp]
        public void UseDedicatedProjectState()
        {
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(Application.companyName, Is.EqualTo("CodexCollectionCarryValidation"));
            Assert.That(Application.productName, Does.StartWith("CollectionCarry20261027"));
            PlayerPrefs.DeleteAll();
            foreach (FieldInfo field in typeof(ProfileSaveJournal).GetFields(Static))
                if (!field.IsInitOnly && !field.IsLiteral) field.SetValue(null, null);
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestState/" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            typeof(ProfileSaveJournal).GetField("directoryOverride", Static).SetValue(null, directory);
            typeof(CoreGameplayDemoController).GetMethod("ResetWarehouseIncomeLoginScopes", Static).Invoke(null, null);
            PlayerPrefs.SetInt(CoreGameplayDemoController.WarehousePortMigrationKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, Origin);
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, Guid.NewGuid().ToString("N"));
            ProfileGoldPersistence.Save(0m);
        }

        [UnityTearDown]
        public IEnumerator ReleaseFixtureState()
        {
            foreach (GameObject host in hosts) if (host != null) Object.Destroy(host);
            hosts.Clear();
            yield return null;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ConfirmedArrivalAtZeroGoldCarriesRemovedTreasureAndPreservesRemoteExhibit()
        {
            SeedOrigin(Compass + "," + Bell, Compass + ",," + Bell + ",,,");
            CoreGameplayDemoController controller = CreateController();
            InitializeIncome(controller);
            Assert.That(controller.WarehouseCollection.TryRemoveDisplay(0), Is.True);
            Invoke(controller, "SaveProgress");
            CoreVoyageSession session = controller.Session;
            session.Cargo.Gold = 0;
            session.StartVoyage(Destination);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Destination)), Is.Empty);
            typeof(CoreVoyageSession).GetMethod("FinishVoyage", Instance).Invoke(session, new object[] { VoyageOutcome.ArrivedAtPort });
            Assert.That(session.CurrentPort, Is.EqualTo(Origin));
            session.ReturnToPortAfterSettlement();
            Invoke(controller, "SaveProgress");
            Assert.That(session.CurrentPort, Is.EqualTo(Destination));
            Assert.That(session.ProfileGold, Is.Zero);
            string[] arrivalOwned = controller.WarehouseCollection.OwnedIds.ToArray();
            Assert.That(arrivalOwned, Does.Contain(Compass));
            Assert.That(arrivalOwned, Does.Not.Contain(Bell), "The existing exhibit stays at its port even when arrival awards new keepsakes.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Origin)), Is.EqualTo(Bell));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayPortKey(Origin)), Is.EqualTo(",," + Bell + ",,,"));
            Assert.That(controller.WarehouseCollection.TryDisplay(Compass, 5), Is.True);
            Invoke(controller, "SaveProgress");
            Assert.That(session.ProfileGold, Is.Zero, "No gold is required to restart exhibition income.");
            var income = new CoreWarehouseIncome();
            Assert.That(income.Restore(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey)), Is.True);
            Assert.That(income.CurrentDisplayedCount, Is.EqualTo(2));
            Assert.That(income.Claim(new DateTime(income.LastUtcTicks, DateTimeKind.Utc).AddHours(1)), Is.EqualTo(20));
            CoreGameplayDemoController restored = CreateController();
            InitializeIncome(restored);
            Assert.That(restored.Session.CurrentPort, Is.EqualTo(Destination));
            Assert.That(restored.WarehouseCollection.DisplayedAt(5), Is.EqualTo(Compass));
            CollectionAssert.AreEquivalent(arrivalOwned, restored.WarehouseCollection.OwnedIds);
            CollectionAssert.AreEquivalent(arrivalOwned.Concat(new[] { Bell }), PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey).Split(','));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SafeOrFailedReturnUsesActualPortAndDoesNotLoseCarriedTreasure(bool failure)
        {
            SeedOrigin(Compass, ",,,,,");
            CoreGameplayDemoController controller = CreateController();
            InitializeIncome(controller);
            CoreVoyageSession session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 20;
            session.StartVoyage(Destination);
            if (failure) session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(failure ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn));
            session.ReturnToPortAfterSettlement();
            Invoke(controller, "SaveProgress");
            Assert.That(session.CurrentPort, Is.EqualTo(Origin));
            CollectionAssert.AreEqual(new[] { Compass }, controller.WarehouseCollection.OwnedIds);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Origin)), Is.EqualTo(Compass));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Destination)), Is.Empty);
        }

        [Test]
        public void ResumedVoyageDoesNotMoveKeepsakesBeforeArrivalOrCopyVoyageCargoToPort()
        {
            SeedOrigin(Compass, ",,,,,");
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 91);
            var session = new CoreVoyageSession();
            session.RestoreProfile(0m, Origin);
            session.StartVoyage(Destination);
            session.Cargo.Water = 73;
            CoreGameplayDemoController controller = CreateController(session);
            InitializeIncome(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.CurrentPort, Is.EqualTo(Origin));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Origin)), Is.EqualTo(Compass));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Destination)), Is.Empty);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(91));
            Assert.That(session.Cargo.Water, Is.EqualTo(73));
        }

        private static void SeedOrigin(string owned, string display)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedPortKey(Origin), owned);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayPortKey(Origin), display);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, owned);
        }

        private CoreGameplayDemoController CreateController(CoreVoyageSession session = null)
        {
            if (session == null)
            {
                session = new CoreVoyageSession();
                session.RestoreProfile(ProfileGoldPersistence.Load(), PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey, Origin));
            }
            var host = new GameObject("Inactive Collection Carry Integration Controller");
            host.SetActive(false);
            hosts.Add(host);
            var controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null);
            typeof(CoreGameplayDemoController).GetField("session", Instance).SetValue(controller, session);
            Invoke(controller, "RestoreWarehouseCollection");
            return controller;
        }

        private static void InitializeIncome(CoreGameplayDemoController controller)
        {
            object[] args = { DateTime.UtcNow, null };
            Assert.That((bool)Invoke(controller, "TryInitializeWarehouseIncome", args), Is.True, (string)args[1]);
        }

        private static object Invoke(CoreGameplayDemoController controller, string method, params object[] args)
        {
            // Keep achievement persistence active while initializing, rather
            // than displaying, its unrelated UI notification in this no-UI fixture.
            if (method == "SaveProgress")
                typeof(CoreGameplayDemoController).GetField("knownAchievementStates", Instance).SetValue(controller, null);
            return typeof(CoreGameplayDemoController).GetMethod(method, Instance).Invoke(controller, args);
        }
    }
}
