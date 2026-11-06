using System;
using System.Collections;
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
        public IEnumerator RuntimeMemory_StoryComicOpeningReleasesRetiredPanelsAndReopensUnreadProgress()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return VerifyStoryComicMemory(controller, OpeningPortComicProgress.OpeningKey);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_StoryComicArrivalsReleaseRetiredPanelsAndReopenAtEveryPort()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
                yield return VerifyStoryComicMemory(controller, OpeningPortComicProgress.ArrivalKey(story.Id));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_StoryComicLegendaryCloseCollectsItsPageAndCanReopen()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return AwaitTransientMemoryCollection(controller);
            string resource = LegendaryFleetComicCatalog.Find("legendary_pirate_iron_oath").Before.ImageResource;
            // Build and close the production modal directly, without arranging a
            // battle or changing its rewards merely to exercise resource ownership.
            InvokeTransientMemory(controller, "BuildTreasureStoryOverlay", resource, "memory-regression-legendary");
            int first = StoryComicTextureId(controller, "treasureStoryOverlay");
            StoryComicDisplay displayed = CaptureStoryComicDisplay(controller, "treasureStoryOverlay", resource, 0);
            int beforeClose = MemoryCollectionCount(controller);
            InvokeTransientMemory(controller, "CloseTreasureStoryOverlay");
            yield return AwaitTransientMemoryCollection(controller);
            Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(beforeClose));
            AssertMemoryComicPagesCollected(new[] { first });

            InvokeTransientMemory(controller, "BuildTreasureStoryOverlay", resource, "memory-regression-legendary");
            int reopened = StoryComicTextureId(controller, "treasureStoryOverlay");
            // Editor importers may reuse an asset's instance ID after it was
            // absent from the loaded-object inventory. Verify the displayed art.
            AssertStoryComicDisplay(CaptureStoryComicDisplay(controller, "treasureStoryOverlay", resource, 0), displayed);
            yield return null;
            Assert.That(StoryComicTextureId(controller, "treasureStoryOverlay"), Is.EqualTo(reopened));
            AssertStoryComicDisplay(CaptureStoryComicDisplay(controller, "treasureStoryOverlay", resource, 0), displayed);
            InvokeTransientMemory(controller, "CloseTreasureStoryOverlay");
            yield return AwaitTransientMemoryCollection(controller);
            AssertMemoryComicPagesCollected(new[] { reopened });
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator VerifyStoryComicMemory(CoreGameplayDemoController controller, string key)
        {
            InvokeTransientMemory(controller, "CloseOpeningComic");
            yield return AwaitTransientMemoryCollection(controller);
            string json = string.Empty;
            var progress = new OpeningPortComicProgress(Guid.NewGuid().ToString("N"), () => json, value => json = value);
            RamComicField("openingPortComicProgress").SetValue(controller, progress);
            Assert.That(progress.TryQueue(key), Is.True, key);
            InvokeTransientMemory(controller, "ResumeOpeningPortComic");
            int first = StoryComicTextureId(controller, "openingComicOverlay");
            yield return AwaitTransientMemoryCollection(controller);
            int beforeChange = MemoryCollectionCount(controller);
            int current = first;

            // A real production button advances the reading record. No test-side
            // resource load, forced GC or UnloadUnusedAssets can cause the release.
            // Original four-panel sheets retain one texture for their UV changes;
            // optional independent HD panels retire a texture at every advance.
            for (int panel = 0; panel < ComicPanelCatalog.QuadPanelCount; panel++)
            {
                int beforeAdvance = MemoryCollectionCount(controller);
                AdvanceStoryMemoryComic(controller);
                int next = StoryComicTextureId(controller, "openingComicOverlay");
                yield return AwaitTransientMemoryCollection(controller);
                if (next != current)
                {
                    Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(beforeAdvance), key);
                    AssertMemoryComicPagesCollected(new[] { current });
                }
                else
                    Assert.That(MemoryCollectionCount(controller), Is.EqualTo(beforeAdvance),
                        "Changing a crop within one sheet must not scan resources: " + key);
                current = next;
                Assert.That(StoryComicTextureId(controller, "openingComicOverlay"), Is.EqualTo(current),
                    "The visible page must survive the production scan: " + key);
            }
            Assert.That(current, Is.Not.EqualTo(first), "Advancing to the next source page replaces its texture: " + key);
            Assert.That(progress.PendingPage, Is.EqualTo(1), key);
            Assert.That(progress.PendingPanel, Is.Zero, key);
            Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(beforeChange), key);
            AssertMemoryComicPagesCollected(new[] { first });

            int beforeSamePage = MemoryCollectionCount(controller);
            InvokeTransientMemory(controller, "SetOpeningComicPage", 1, 0);
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(MemoryCollectionCount(controller), Is.EqualTo(beforeSamePage),
                "Rebinding the same texture must not schedule another scan: " + key);
            Assert.That(StoryComicTextureId(controller, "openingComicOverlay"), Is.EqualTo(current));
            string currentResource = key == OpeningPortComicProgress.OpeningKey
                ? OpeningComicCatalog.Pages[1].ImageResource
                : ArrivalComicCatalog.FindById(key.Substring("arrival:".Length)).Pages[1];
            StoryComicDisplay displayed = CaptureStoryComicDisplay(controller, "openingComicOverlay", currentResource, 0);

            InvokeTransientMemory(controller, "CloseOpeningComic");
            yield return AwaitTransientMemoryCollection(controller);
            Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(beforeSamePage), key);
            AssertMemoryComicPagesCollected(new[] { current });
            Assert.That(progress.HasRead(key), Is.False, "Closing does not acknowledge an unread story.");
            Assert.That(progress.PendingPage, Is.EqualTo(1));
            Assert.That(progress.PendingPanel, Is.Zero);

            InvokeTransientMemory(controller, "ResumeOpeningPortComic");
            int reopened = StoryComicTextureId(controller, "openingComicOverlay");
            AssertStoryComicDisplay(CaptureStoryComicDisplay(controller, "openingComicOverlay", currentResource, 0), displayed);
            Assert.That((int)RamComicField("openingComicPageIndex").GetValue(controller), Is.EqualTo(1));
            Assert.That((int)RamComicField("openingComicPanelIndex").GetValue(controller), Is.Zero);
            Assert.That(progress.HasRead(key), Is.False);
            Assert.That(progress.PendingStoryKey, Is.EqualTo(key));
            Assert.That(progress.PendingPage, Is.EqualTo(1));
            Assert.That(progress.PendingPanel, Is.Zero);
            yield return null;
            Assert.That(StoryComicTextureId(controller, "openingComicOverlay"), Is.EqualTo(reopened));
            AssertStoryComicDisplay(CaptureStoryComicDisplay(controller, "openingComicOverlay", currentResource, 0), displayed);
            InvokeTransientMemory(controller, "CloseOpeningComic");
            yield return AwaitTransientMemoryCollection(controller);
            AssertMemoryComicPagesCollected(new[] { reopened });
            int afterClose = MemoryCollectionCount(controller);
            InvokeTransientMemory(controller, "CloseOpeningComic");
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(MemoryCollectionCount(controller), Is.EqualTo(afterClose),
                "Closing an absent story overlay must not scan resources: " + key);
        }

        private static void AdvanceStoryMemoryComic(CoreGameplayDemoController controller)
        {
            var overlay = (RectTransform)RamComicField("openingComicOverlay").GetValue(controller);
            Button advance = overlay.GetComponentInChildren<Button>();
            Assert.That(advance != null && advance.IsInteractable(), Is.True);
            advance.onClick.Invoke();
        }

        private static int StoryComicTextureId(CoreGameplayDemoController controller, string overlayField)
        {
            // This non-iterator method keeps the texture and graphic out of the
            // coroutine state machine: only their integer identity crosses yields.
            var overlay = (RectTransform)RamComicField(overlayField).GetValue(controller);
            Assert.That(overlay != null && overlay.gameObject.activeInHierarchy, Is.True, overlayField);
            ComicPageGraphic page = overlay.GetComponentInChildren<ComicPageGraphic>();
            Assert.That(page != null && page.texture != null, Is.True, overlayField);
            Assert.That(page.texture.width, Is.GreaterThan(1));
            Assert.That(page.texture.height, Is.GreaterThan(1));
            return page.texture.GetInstanceID();
        }

        private struct StoryComicDisplay
        {
            // Keep only values across yields; retaining a Texture/Graphic here
            // would itself prevent the production collector from releasing it.
            public string TextureName;
            public int Width;
            public int Height;
            public Rect Uv;
        }

        private static StoryComicDisplay CaptureStoryComicDisplay(CoreGameplayDemoController controller,
            string overlayField, string resource, int panel)
        {
            var overlay = (RectTransform)RamComicField(overlayField).GetValue(controller);
            Assert.That(overlay != null && overlay.gameObject.activeInHierarchy, Is.True, overlayField);
            ComicPageGraphic page = overlay.GetComponentInChildren<ComicPageGraphic>();
            Assert.That(page != null && page.isActiveAndEnabled && page.texture != null, Is.True, overlayField);
            string sourceName = resource.Substring(resource.LastIndexOf('/') + 1);
            string independentName = sourceName + "_panel" + (panel + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            bool independent = page.texture.name == independentName;
            Assert.That(page.texture.name, Is.EqualTo(independent ? independentName : sourceName),
                "The reopened page must display the authored story resource.");
            Rect expectedUv = independent ? new Rect(0f, 0f, 1f, 1f) : ComicPanelCatalog.PanelsFor(resource)[panel];
            Assert.That(page.uvRect, Is.EqualTo(expectedUv), "The unread panel's crop must be restored.");
            Assert.That(page.texture.width, Is.GreaterThan(1));
            Assert.That(page.texture.height, Is.GreaterThan(1));
            Canvas.ForceUpdateCanvases();
            Assert.That(page.rectTransform.rect.width, Is.GreaterThan(0f));
            Assert.That(page.rectTransform.rect.height, Is.GreaterThan(0f));
            Assert.That(page.rectTransform.rect.width / page.rectTransform.rect.height,
                Is.EqualTo(page.texture.width * expectedUv.width / (page.texture.height * expectedUv.height)).Within(.01f),
                "The visible comic must preserve the authored panel's proportions.");
            return new StoryComicDisplay
            {
                TextureName = page.texture.name,
                Width = page.texture.width,
                Height = page.texture.height,
                Uv = page.uvRect
            };
        }

        private static void AssertStoryComicDisplay(StoryComicDisplay actual, StoryComicDisplay expected)
        {
            Assert.That(actual.TextureName, Is.EqualTo(expected.TextureName));
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            Assert.That(actual.Uv, Is.EqualTo(expected.Uv));
        }
    }
}
