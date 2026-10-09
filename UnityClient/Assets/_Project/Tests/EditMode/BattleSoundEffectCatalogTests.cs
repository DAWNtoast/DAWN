using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleSoundEffectCatalogTests
    {
        [Test]
        public void BattleSoundEffectCatalog_HasUniqueImportedClips()
        {
            IReadOnlyList<string> keys = CoreGameplayDemoController.BattleSoundEffectKeys;
            Assert.That(keys, Is.Not.Null);
            Assert.That(keys.Count, Is.EqualTo(14));

            HashSet<string> distinct = new HashSet<string>();
            foreach (string key in keys)
            {
                Assert.That(string.IsNullOrEmpty(key), Is.False);
                Assert.That(key.StartsWith("battle_"), Is.True, key);
                Assert.That(distinct.Add(key), Is.True, "Duplicate battle cue: " + key);
                Assert.That(Resources.Load<AudioClip>("Audio/BattleSfx/" + key), Is.Not.Null, key);
            }
        }

        [Test]
        public void BattleSoundEffects_UseDedicatedResourceFolder()
        {
            Assert.That(Resources.Load<AudioClip>("Audio/BattleSfx/battle_card_play"), Is.Not.Null);
            Assert.That(Resources.Load<AudioClip>("Audio/Sfx/card_play"), Is.Null,
                "The battle channel must not depend on the removed legacy SFX folder.");
        }

        [Test]
        public void NonBattleSoundEffectCatalog_HasImportedWorldAndInterfaceClips()
        {
            AssertCatalog(CoreGameplayDemoController.WorldSoundEffectKeys, "Audio/WorldSfx/", "world");
            AssertCatalog(CoreGameplayDemoController.InterfaceSoundEffectKeys, "Audio/InterfaceSfx/", "interface");
            AssertCatalog(CoreGameplayDemoController.AmbienceSoundEffectKeys, "Audio/Ambience/", "ambience");
        }

        private static void AssertCatalog(IReadOnlyList<string> keys, string prefix, string label)
        {
            Assert.That(keys, Is.Not.Null);
            Assert.That(keys.Count, Is.GreaterThan(0), label);
            HashSet<string> distinct = new HashSet<string>();
            foreach (string key in keys)
            {
                Assert.That(string.IsNullOrEmpty(key), Is.False);
                Assert.That(distinct.Add(key), Is.True, "Duplicate " + label + " cue: " + key);
                Assert.That(Resources.Load<AudioClip>(prefix + key), Is.Not.Null, key);
            }
        }
    }
}
