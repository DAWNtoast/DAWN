using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComicAutoAdvanceTests
    {
        [UnityTest]
        public IEnumerator AutoAdvance_UsesRealTimeAndClickAtDeadlineStartsOneFreshPage()
        {
            GameObject host = CreateFixture(out Button advance, out ComicAutoAdvance timer);
            float originalTimeScale = Time.timeScale;
            int pages = 0;
            try
            {
                advance.onClick.AddListener(() => pages++);
                timer.Configure(advance, 1f);
                Time.timeScale = 0f;
                yield return null;
                Assert.That(pages, Is.Zero, "A fresh page cannot advance in its creation frame.");
                timer.RestoreRemainingSeconds(.06f);
                yield return WaitFor(() => pages == 1);
                Assert.That(timer.RemainingSeconds, Is.GreaterThan(.5f), "An automatic page starts a fresh reading interval.");

                timer.RestoreRemainingSeconds(0f);
                ExecuteEvents.Execute(advance.gameObject, new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Assert.That(pages, Is.EqualTo(2), "A pointer click immediately advances the expired page.");
                Assert.That(timer.RemainingSeconds, Is.EqualTo(1f).Within(.001f));
                yield return null;
                Assert.That(pages, Is.EqualTo(2), "LateUpdate must not also advance the page clicked in the same frame.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                RestoreApplicationState(timer);
                Object.Destroy(host);
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AutoAdvance_PauseAndFocusLossFreezeAndResumeRemainingTime()
        {
            GameObject host = CreateFixture(out Button advance, out ComicAutoAdvance timer);
            int pages = 0;
            try
            {
                advance.onClick.AddListener(() => pages++);
                timer.Configure(advance, 1f);
                timer.RestoreRemainingSeconds(.08f);
                Lifecycle(timer, "OnApplicationPause", true);
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(pages, Is.Zero);
                Assert.That(timer.RemainingSeconds, Is.EqualTo(.08f).Within(.001f));
                Lifecycle(timer, "OnApplicationPause", false);
                yield return WaitFor(() => pages == 1);

                timer.RestoreRemainingSeconds(.08f);
                Lifecycle(timer, "OnApplicationFocus", false);
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(pages, Is.EqualTo(1));
                Assert.That(timer.RemainingSeconds, Is.EqualTo(.08f).Within(.001f));
                Lifecycle(timer, "OnApplicationFocus", true);
                yield return WaitFor(() => pages == 2);
            }
            finally
            {
                RestoreApplicationState(timer);
                Object.Destroy(host);
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AutoAdvance_DisabledOrRetiredPageCannotAdvanceAndResumeKeepsRemainingTime()
        {
            GameObject host = CreateFixture(out Button advance, out ComicAutoAdvance timer);
            int pages = 0;
            bool currentPage = true;
            try
            {
                advance.onClick.AddListener(() => pages++);
                timer.Configure(advance, 1f, () => currentPage);
                timer.RestoreRemainingSeconds(.08f);
                timer.enabled = false;
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(pages, Is.Zero);
                Assert.That(timer.RemainingSeconds, Is.EqualTo(.08f).Within(.001f));
                timer.enabled = true;
                currentPage = false;
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(pages, Is.Zero, "An obsolete overlay must not submit an automatic callback.");
                Assert.That(timer.RemainingSeconds, Is.EqualTo(.08f).Within(.001f));
                currentPage = true;
                advance.interactable = false;
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(pages, Is.Zero);
                advance.interactable = true;
                yield return WaitFor(() => pages == 1);
                host.SetActive(false);
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(pages, Is.EqualTo(1), "Closing the overlay stops its timer.");
            }
            finally
            {
                RestoreApplicationState(timer);
                Object.Destroy(host);
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static GameObject CreateFixture(out Button advance, out ComicAutoAdvance timer)
        {
            var host = new GameObject("Comic Timer Fixture", typeof(RectTransform), typeof(Image), typeof(Button));
            advance = host.GetComponent<Button>();
            advance.targetGraphic = host.GetComponent<Image>();
            timer = host.AddComponent<ComicAutoAdvance>();
            RestoreApplicationState(timer);
            return host;
        }

        private static IEnumerator WaitFor(Func<bool> completed)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!completed() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(completed(), Is.True, "The short remaining interval must expire through real player frames.");
        }

        private static void RestoreApplicationState(ComicAutoAdvance timer)
        {
            if (timer == null) return;
            Lifecycle(timer, "OnApplicationPause", false);
            Lifecycle(timer, "OnApplicationFocus", true);
        }

        private static void Lifecycle(ComicAutoAdvance timer, string method, bool value) =>
            typeof(ComicAutoAdvance).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(timer, new object[] { value });
    }
}
