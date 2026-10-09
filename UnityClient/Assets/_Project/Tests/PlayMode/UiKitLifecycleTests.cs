using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiKitLifecycleTests
    {
        private GameObject host;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            host = new GameObject("UI lifecycle fixture", typeof(RectTransform));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConfirmationReopenStartsFreshLifetimeAfterCancel()
        {
            Button accept = PlainButton("Accept"), cancel = PlainButton("Cancel");
            int attempts = 0, cancellations = 0;
            bool complete = false;
            UiConfirmationActions.Bind(host, accept, cancel, () => true,
                () => { attempts++; return complete; }, () => { cancellations++; host.SetActive(false); });
            accept.onClick.Invoke(); accept.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2), "An explicitly unsuccessful action can be retried.");
            cancel.onClick.Invoke(); cancel.onClick.Invoke(); accept.onClick.Invoke();
            Assert.That(cancellations, Is.EqualTo(1));
            Assert.That(attempts, Is.EqualTo(2));
            yield return null;
            host.SetActive(true);
            yield return null;
            complete = true;
            accept.onClick.Invoke(); accept.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(3), "OnEnable reopens the lifetime; a successful action is accepted only once.");
        }

        [UnityTest]
        public IEnumerator HoverClearsForInactiveObjectAndDisabledButtonComponent()
        {
            Button button = PlainButton("Hover source");
            // A plain hint without a CanvasGroup exercises the binder's required-component path.
            GameObject hint = UiKit.CreateRect("Hint", host.transform, new Vector2(200f, 80f), Vector2.zero).gameObject;
            int entered = 0, exited = 0;
            UiHoverHint.Bind(button, () => entered++, () => exited++, hint);
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            var enter = trigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerEnter);
            Assert.That(hint.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            enter.callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.True);
            button.gameObject.SetActive(false);
            yield return null;
            Assert.That(hint.activeSelf, Is.False, "Actual OnDisable clears an external hint.");
            Assert.That(exited, Is.EqualTo(1));
            button.gameObject.SetActive(true);
            yield return null;
            Assert.That(hint.activeSelf, Is.False, "Re-enable does not revive the old hover.");
            enter.callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.True);
            button.enabled = false;
            yield return null;
            Assert.That(hint.activeSelf, Is.False, "Actual Update clears a shown hint when its Button is disabled.");
            Assert.That(entered, Is.EqualTo(2));
            Assert.That(exited, Is.EqualTo(2));
            enter.callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ToastExpiresOnUnscaledTimeAndClearsOnOwnerRetirementOrDisable()
        {
            GameObject card = UiKit.CreateRect("Toast", host.transform, new Vector2(200f, 80f), Vector2.zero).gameObject;
            bool current = true;
            UiToastLifetime toast = UiToastLifetime.Bind(host, card, () => current);
            Assert.That(host.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                toast.Show(.04f);
                Assert.That(card.activeSelf, Is.True);
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(card.activeSelf, Is.False, "The real Update expires the toast even while gameplay time is paused.");
            }
            finally { Time.timeScale = previousTimeScale; }
            toast.Show(4f);
            current = false;
            yield return null;
            Assert.That(card.activeSelf, Is.False);
            toast.Show(4f);
            Assert.That(card.activeSelf, Is.False, "A retired owner cannot show another toast.");
            current = true;
            toast.Show(4f);
            host.SetActive(false);
            yield return null;
            Assert.That(card.activeSelf, Is.False, "Actual OnDisable clears the notification's active state.");
            host.SetActive(true);
            yield return null;
            Assert.That(card.activeSelf, Is.False);
        }

        private Button PlainButton(string name)
        {
            return UiKit.CreateRect(name, host.transform, new Vector2(100f, 40f), Vector2.zero).gameObject.AddComponent<Button>();
        }
    }
}
