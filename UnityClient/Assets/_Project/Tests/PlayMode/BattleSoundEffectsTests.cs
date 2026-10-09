using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleSoundEffectsTests
    {
        private bool hadTutorialCompleted;
        private int savedTutorialCompleted;

        [SetUp]
        public void SaveProgress()
        {
            hadTutorialCompleted = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey);
            savedTutorialCompleted = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
        }

        [TearDown]
        public void RestoreProgress()
        {
            if (hadTutorialCompleted) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, savedTutorialCompleted);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator BattleAudio_UsesOnlyDedicatedBattleCues()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;

            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(CoreGameplayDemoController.SoundEffectsEnabled, Is.True);
            Assert.That(Resources.Load<AudioClip>("Audio/Sfx/card_play"), Is.Null);

            AudioSource source = ReadField<AudioSource>(controller, "battleSfxSource");
            AudioSource worldSource = ReadField<AudioSource>(controller, "worldSfxSource");
            AudioSource interfaceSource = ReadField<AudioSource>(controller, "interfaceSfxSource");
            AudioSource ambienceSource = ReadField<AudioSource>(controller, "ambienceSource");
            Dictionary<string, AudioClip> cache = ReadField<Dictionary<string, AudioClip>>(controller, "battleSfxClipCache");
            Dictionary<string, AudioClip> interfaceCache = ReadField<Dictionary<string, AudioClip>>(controller, "interfaceSfxClipCache");
            Assert.That(source, Is.Not.Null);
            Assert.That(source.name, Is.EqualTo("Battle Sound Effects Source"));
            Assert.That(worldSource, Is.Not.Null);
            Assert.That(interfaceSource, Is.Not.Null);
            Assert.That(ambienceSource, Is.Not.Null);
            Assert.That(cache, Is.Empty, "Battle cues should be loaded on demand.");

            // The presentation flag is set by each battle animation coroutine. It
            // lets the lethal impact and victory cue finish after the core has
            // already moved into settlement, while port/UI calls remain silent.
            FieldInfo presentation = typeof(CoreGameplayDemoController).GetField(
                "battleAnimationPlaying", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presentation, Is.Not.Null);
            presentation.SetValue(controller, true);
            Invoke(controller, "PlaySfx", "battle_card_play", 1f);
            Assert.That(cache.ContainsKey("battle_card_play"), Is.True);
            presentation.SetValue(controller, false);

            Invoke(controller, "PlaySfx", "ui_click", 1f);
            Assert.That(cache.ContainsKey("ui_click"), Is.False,
                "Unknown legacy cues must remain disabled after battle SFX are restored.");

            Invoke(controller, "PlaySfx", "course_plot", 0.4f);
            Assert.That(interfaceCache.ContainsKey("course_plot"), Is.True,
                "The interface channel should load a non-battle cue during port navigation.");
        }

        private static T ReadField<T>(CoreGameplayDemoController controller, string name)
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(controller);
        }

        private static void Invoke(CoreGameplayDemoController controller, string name, params object[] arguments)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(controller, arguments);
        }
    }
}
