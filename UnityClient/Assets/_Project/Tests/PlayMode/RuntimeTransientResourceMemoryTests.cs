using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator RuntimeMemory_BothRamInitiatorsReleaseClosedPagesWithoutLeavingBattle()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (bool enemy in new[] { false, true })
            {
                CoreVoyageSession session = ArrangeRamComicBattle(controller, enemy, NavalBattleAlignment.EnemyBroadside);
                RenderResponsiveBattle(controller);
                yield return AwaitTransientMemoryCollection(controller);
                int before = MemoryCollectionCount(controller);
                ClickAcceptanceButton(enemy ? "End Battle Turn" : "Battle Ram");
                yield return WaitForRamComic(controller);
                int[] pageIds = ActiveMemoryComicPageIds(controller);
                Assert.That(pageIds.Length, Is.EqualTo(4));
                string committed = RamComicCoreState(session);
                yield return AdvanceMemoryComic(controller);
                yield return AwaitTransientMemoryCollection(controller);
                Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(before));
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
                AssertMemoryComicPagesCollected(pageIds);
                Assert.That(((Texture2D[])RamComicField("ramComicTextures").GetValue(controller)).All(texture => texture == null), Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_RapidComicReopenCoalescesCleanupAndPreservesEveryActivePage()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ArrangeRamComicBattle(controller, false, NavalBattleAlignment.EnemyBroadside);
            RenderResponsiveBattle(controller);
            yield return AwaitTransientMemoryCollection(controller);
            int before = MemoryCollectionCount(controller);
            var retired = new List<int>();
            for (int sequence = 0; sequence < RamComicCatalog.SequenceCount; sequence++)
            {
                OpenMemoryComic(controller, sequence);
                if (sequence < RamComicCatalog.SequenceCount - 1)
                    retired.AddRange(ActiveMemoryComicPageIds(controller));
            }
            int[] active = ActiveMemoryComicPageIds(controller);
            yield return AwaitTransientMemoryCollection(controller);
            Assert.That(MemoryCollectionCount(controller), Is.EqualTo(before + 1), "Same-frame closes share one delayed scan.");
            AssertMemoryComicPagesCollected(retired);
            CollectionAssert.AreEqual(active, ActiveMemoryComicPageIds(controller));
            for (int page = 0; page < RamComicCatalog.PagesPerSequence; page++)
            {
                InvokeTransientMemory(controller, "SetRamComicPanel", page);
                var graphic = GameObject.Find("Battle Ram Comic Page").GetComponent<RawImage>();
                Assert.That(graphic.texture, Is.Not.Null);
                Assert.That(graphic.texture.GetInstanceID(), Is.EqualTo(active[page]));
            }
            InvokeTransientMemory(controller, "CloseRamComic");
            yield return AwaitTransientMemoryCollection(controller);
            AssertMemoryComicPagesCollected(active);
            int completed = MemoryCollectionCount(controller);
            InvokeTransientMemory(controller, "CloseRamComic");
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(MemoryCollectionCount(controller), Is.EqualTo(completed), "Closing an absent overlay must not scan resources.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_ApplicationPauseDuringComicRestartsCancelledCollection()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = ArrangeRamComicBattle(controller, false, NavalBattleAlignment.EnemyBroadside);
            RenderResponsiveBattle(controller);
            yield return AwaitTransientMemoryCollection(controller);
            int before = MemoryCollectionCount(controller);
            ClickAcceptanceButton("Battle Ram");
            yield return WaitForRamComic(controller);
            int[] pages = ActiveMemoryComicPageIds(controller);
            string committed = RamComicCoreState(session);
            // Unlike SetActive(false), application pause does not call OnDisable.
            InvokeTransientMemory(controller, "OnApplicationPause", true);
            Assert.That(RamComicField("runtimeArtCollection").GetValue(controller), Is.Null);
            InvokeTransientMemory(controller, "OnApplicationPause", false);
            yield return AwaitTransientMemoryCollection(controller);
            Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(before));
            Assert.That(RamComicCoreState(session), Is.EqualTo(committed));
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            AssertMemoryComicPagesCollected(pages);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_FacilityCyclesDropOldAmbienceCachesWithoutInterruptingCurrentAudio()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return new WaitForSecondsRealtime(1f);
            foreach (string facility in new[] { "Market", "Tavern", "Shipyard", "Treasure", "Market", "Tavern", "Shipyard", "Treasure" })
            {
                ActiveButton("Port Hotspot " + facility).onClick.Invoke();
                yield return new WaitForSecondsRealtime(1f);
                yield return AwaitTransientMemoryCollection(controller);
                var cache = (Dictionary<string, AudioClip>)RamComicField("ambienceClipCache").GetValue(controller);
                Assert.That(cache, Is.Empty, "View collection must not retain all previously visited sound beds.");
                var source = (AudioSource)RamComicField("ambienceSource").GetValue(controller);
                Assert.That(source.clip, Is.Not.Null);
                Assert.That(source.isPlaying, Is.True);
                Assert.That(Resources.FindObjectsOfTypeAll<AudioClip>().Count(clip => clip.name.EndsWith("_ambience", StringComparison.Ordinal)),
                    Is.LessThanOrEqualTo(2), "Only the live bed and an outgoing crossfade may survive this view's scan.");
                ActiveButton("Return To Port Hub").onClick.Invoke();
                yield return new WaitForSecondsRealtime(1f);
                yield return AwaitTransientMemoryCollection(controller);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator AwaitTransientMemoryCollection(CoreGameplayDemoController controller)
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (RamComicField("runtimeArtCollection").GetValue(controller) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(RamComicField("runtimeArtCollection").GetValue(controller), Is.Null, "Deferred production cleanup must finish.");
            yield return null;
        }

        private static IEnumerator AdvanceMemoryComic(CoreGameplayDemoController controller)
        {
            int advanced = -1;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                Button button = GameObject.Find("Battle Ram Comic Advance")?.GetComponent<Button>();
                int page = (int)RamComicField("ramComicPanelIndex").GetValue(controller);
                if (button != null && button.IsInteractable() && page > advanced)
                {
                    button.onClick.Invoke();
                    advanced = page;
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(advanced, Is.EqualTo(3));
        }

        private static void OpenMemoryComic(CoreGameplayDemoController controller, int sequence)
        {
            bool enemy = sequence >= 2, headOn = sequence % 2 == 0;
            var alignment = headOn ? NavalBattleAlignment.HeadOn : NavalBattleAlignment.EnemyBroadside;
            var texture = (Texture2D)InvokeTransientMemory(controller, "RamComicTexture", enemy, alignment);
            Assert.That(texture, Is.Not.Null);
            Assert.That(InvokeTransientMemory(controller, "BuildRamComicOverlay", texture, enemy, headOn, string.Empty), Is.Not.Null);
        }

        private static int[] ActiveMemoryComicPageIds(CoreGameplayDemoController controller) =>
            ((Texture2D[])RamComicField("activeRamComicPages").GetValue(controller)).Select(texture => texture.GetInstanceID()).ToArray();

        private static void AssertMemoryComicPagesCollected(IEnumerable<int> ids)
        {
            var loaded = new HashSet<int>(Resources.FindObjectsOfTypeAll<Texture2D>().Select(texture => texture.GetInstanceID()));
            Assert.That(ids.Any(loaded.Contains), Is.False, "Closed full-resolution comic pages must be collectible within the same battle phase.");
        }

        private static object InvokeTransientMemory(CoreGameplayDemoController controller, string method, params object[] arguments) =>
            typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, arguments);
    }
}
