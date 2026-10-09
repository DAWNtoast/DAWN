using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class TreasureProgressReadProtectionTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private bool existed;
        private string original;

        [SetUp]
        public void CreateIsolatedControllerAndPreserveTreasurePreference()
        {
            Assert.That(Application.isPlaying, Is.False);
            existed = PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey);
            original = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, string.Empty);
            host = new GameObject("Treasure Read Protection Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Set("session", new CoreVoyageSession());
        }

        [TearDown]
        public void RestoreOriginalPreference()
        {
            if (existed) PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, original);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.TreasureProgressKey);
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
        }

        [TestCase("{broken-json")]
        [TestCase("null")]
        [TestCase("")]
        public void UnreadableRecord_StaysByteForByteIntactWhenProgressIsSaved(string json)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, json);
            Invoke("RestoreTreasureProgress");
            Assert.That(Read<bool>("treasureProgressReadFailed"), Is.True);
            Assert.That(Read<string>("portNotice"), Does.Contain("原始档案已保留"));

            // Simulate valid in-memory rewards arriving after a failed load.
            // Saving that default/replacement state must never erase the source.
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { fragments = 17, pirateExperience = 100 });
            Invoke("SaveTreasureProgress");
            Invoke("SaveTreasureProgress");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(json));
            Assert.That(Read<bool>("treasureProgressReadFailed"), Is.True);
        }

        [Test]
        public void SuccessfullyReloadedRepair_ReenablesNormalTreasureSaves()
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, "{broken-json");
            Invoke("RestoreTreasureProgress");
            Assert.That(Read<bool>("treasureProgressReadFailed"), Is.True);
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey,
                JsonUtility.ToJson(new CoreTreasureProgress { fragments = 12, pirateExperience = 100 }));
            Invoke("RestoreTreasureProgress");
            Assert.That(Read<bool>("treasureProgressReadFailed"), Is.False);
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(12));
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { fragments = 16, pirateExperience = 120 });
            Invoke("SaveTreasureProgress");
            CoreTreasureProgress saved = JsonUtility.FromJson<CoreTreasureProgress>(
                PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey));
            Assert.That(saved.fragments, Is.EqualTo(16));
            Assert.That(saved.pirateExperience, Is.EqualTo(120));
        }

        [Test]
        public void MissingRecord_IsANewProfileAndCanBeSaved()
        {
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TreasureProgressKey);
            Invoke("RestoreTreasureProgress");
            Assert.That(Read<bool>("treasureProgressReadFailed"), Is.False);
            Invoke("SaveTreasureProgress");
            Assert.That(JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey)), Is.Not.Null);
        }

        [Test]
        public void SettingsEntrypoints_AreAllowedDuringEveryTutorialStep()
        {
            MethodInfo allows = typeof(CoreGameplayDemoController).GetMethod("GuidedTutorialAllowsControl", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (GuidedTutorialStep step in Enum.GetValues(typeof(GuidedTutorialStep)))
            {
                Set("guidedTutorialStep", step);
                foreach (string name in new[] { "Voyage Settings", "Port Hub Settings", "Port Facility Settings" })
                    Assert.That((bool)allows.Invoke(controller, new object[] { name }), Is.True, step + ": " + name);
            }
        }

        private void Invoke(string name) => typeof(CoreGameplayDemoController)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        private T Read<T>(string name) => (T)typeof(CoreGameplayDemoController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        private void Set(string name, object value) => typeof(CoreGameplayDemoController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
    }
}
