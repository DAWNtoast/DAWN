using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiKitControlsTests
    {
        private GameObject host;

        [SetUp] public void SetUp() => host = new GameObject("Shared UI fixture", typeof(RectTransform));
        [TearDown] public void TearDown() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void SliderKeepsAuthoredGeometryAndNotifiesOnlyRealValueChanges()
        {
            Slider slider = UiKit.CreateSlider("Volume", host.transform, new Vector2(410f, 42f), Vector2.one,
                0f, 1f, .25f, Color.black, Color.yellow, fillInset: 20f, handleWidth: 26f);
            Assert.That(slider.value, Is.EqualTo(.25f));
            Assert.That(slider.handleRect.sizeDelta, Is.EqualTo(new Vector2(26f, 0f)));
            Assert.That(((RectTransform)slider.fillRect.parent).sizeDelta, Is.EqualTo(new Vector2(390f, 8f)));
            Assert.That(((RectTransform)slider.fillRect.parent).anchoredPosition, Is.EqualTo(new Vector2(-10f, 0f)));
            Assert.That(slider.targetGraphic, Is.SameAs(slider.handleRect.GetComponent<Image>()));
            int notifications = 0;
            slider.onValueChanged.AddListener(_ => notifications++);
            slider.SetValueWithoutNotify(.5f);
            Assert.That(notifications, Is.Zero);
            slider.value = .75f;
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(UiId.Find(host.transform, "Volume"), Is.SameAs(slider.transform));
        }

        [Test]
        public void QuantityClampsAndPreservesIntMaxEndpointWithoutOverflow()
        {
            Slider slider = UiKit.CreateQuantitySlider("Cargo amount", host.transform, new Vector2(650f, 44f),
                Vector2.zero, 1, int.MaxValue, -1, Color.black, Color.yellow);
            Assert.That(UiKit.ReadQuantity(slider, 1, int.MaxValue), Is.EqualTo(1));
            slider.value = slider.maxValue;
            Assert.That(UiKit.ReadQuantity(slider, 1, int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(slider.handleRect.sizeDelta, Is.EqualTo(new Vector2(28f, 36f)));
            Assert.That(slider.GetComponent<Image>().raycastTarget, Is.True);
            Slider fixedAmount = UiKit.CreateQuantitySlider("Fixed amount", host.transform, new Vector2(650f, 44f),
                Vector2.zero, 7, 7, 100, Color.black, Color.yellow);
            Assert.That(fixedAmount.interactable, Is.False);
            Assert.That(UiKit.ReadQuantity(fixedAmount, 7, 7), Is.EqualTo(7));
        }

        [Test]
        public void TabSelectionAndDisabledStateRemainIndependent()
        {
            Button tab = UiKit.CreateTab("Upgrade Stage Naval", host.transform, new Vector2(228f, 42f), Vector2.zero,
                "远距", Color.yellow, Color.white, 18, selected: true, interactable: false);
            WoodButtonRoleState state = tab.GetComponent<WoodButtonRoleState>();
            Assert.That(state, Is.Not.Null);
            Assert.That(state.IsSelected, Is.True);
            Assert.That(tab.IsInteractable(), Is.False);
            WoodButtonSkin.SetRole(tab, WoodButtonRole.Secondary);
            Assert.That(state.IsSelected, Is.True);
            tab.interactable = true;
            Assert.That(state.IsSelected, Is.True);
            WoodButtonSkin.SetSelected(tab, false);
            Assert.That(tab.IsInteractable(), Is.True);
            Assert.That(state.IsSelected, Is.False);
        }

        [Test]
        public void ProgressUsesRenderableSpriteAndClampsInvalidValues()
        {
            Image fill = UiKit.CreateProgressFill("Fill", host.transform, new Vector2(200f, 20f), Vector2.zero,
                Color.green, BattleDefenseHud.FillSprite, .6f);
            Assert.That(fill.sprite, Is.SameAs(BattleDefenseHud.FillSprite));
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillAmount, Is.EqualTo(.6f));
            Assert.That(fill.raycastTarget, Is.False);
            UiKit.SetProgress(fill, float.PositiveInfinity);
            Assert.That(fill.fillAmount, Is.EqualTo(1f));
            UiKit.SetProgress(fill, float.NaN);
            Assert.That(fill.fillAmount, Is.Zero);
            UiKit.SetProgress(fill, -5f);
            Assert.That(fill.fillAmount, Is.Zero);
        }

        [Test]
        public void ModalBlocksPointerWhileHintsRemainNonBlocking()
        {
            RectTransform modal = UiKit.CreateModalBackdrop("New Game Confirmation", host.transform, new Color(0f, 0f, 0f, .8f));
            Assert.That(modal.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(modal.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(modal.GetComponent<Image>().raycastTarget, Is.True);
            Text hint = UiKit.CreateHint("Hover explanation", host.transform, new Vector2(300f, 80f), Vector2.zero,
                "不可出牌原因", Color.white, Color.black);
            Assert.That(hint.raycastTarget, Is.False);
            Assert.That(hint.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [Test]
        public void ConfirmationAcceptsOnceAndRejectsStaleOrDisabledOwners()
        {
            Button accept = PlainButton("Accept"), cancel = PlainButton("Cancel");
            bool current = false;
            int accepted = 0;
            UiConfirmationActions.Bind(host, accept, cancel, () => current, () => { accepted++; return true; }, () => { });
            accept.onClick.Invoke();
            Assert.That(accepted, Is.Zero);
            current = true;
            accept.interactable = false;
            accept.onClick.Invoke();
            Assert.That(accepted, Is.Zero);
            accept.interactable = true;
            accept.enabled = false;
            accept.onClick.Invoke();
            Assert.That(accepted, Is.Zero, "A disabled Button component cannot accept queued/direct callbacks.");
            accept.enabled = true;
            accept.onClick.Invoke(); accept.onClick.Invoke();
            Assert.That(accepted, Is.EqualTo(1));
            host.SetActive(false);
            accept.onClick.Invoke();
            Assert.That(accepted, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmationCanRetryExplicitFailureAndCancelWithoutAccepting()
        {
            Button accept = PlainButton("Accept"), cancel = PlainButton("Cancel");
            int attempts = 0, cancellations = 0;
            UiConfirmationActions.Bind(host, accept, cancel, () => true,
                () => { attempts++; return false; }, () => cancellations++);
            accept.onClick.Invoke(); accept.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            cancel.onClick.Invoke(); cancel.onClick.Invoke(); accept.onClick.Invoke();
            Assert.That(cancellations, Is.EqualTo(1));
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void RebindingConfirmationDoesNotAccumulateOldCallbacks()
        {
            Button accept = PlainButton("Accept"), cancel = PlainButton("Cancel");
            int oldCalls = 0, newCalls = 0;
            UiConfirmationActions.Bind(host, accept, cancel, () => true, () => { oldCalls++; return true; }, () => { });
            UiConfirmationActions.Bind(host, accept, cancel, () => true, () => { newCalls++; return true; }, () => { });
            accept.onClick.Invoke();
            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
        }

        [Test]
        public void HoverRejectsDisabledControlsAndHidesOnPointerExit()
        {
            Button button = PlainButton("Hover source");
            GameObject hint = UiKit.CreateHint("Hint", host.transform, new Vector2(200f, 80f), Vector2.zero,
                "详情", Color.white, Color.black).transform.parent.gameObject;
            int entered = 0, exited = 0;
            UiHoverHint.Bind(button, () => entered++, () => exited++, hint);
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            var enter = trigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerEnter);
            button.interactable = false;
            enter.callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.False);
            button.interactable = true;
            button.enabled = false;
            enter.callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.False);
            button.enabled = true;
            enter.callback.Invoke(null); enter.callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.True);
            Assert.That(entered, Is.EqualTo(1));
            trigger.triggers.Single(entry => entry.eventID == EventTriggerType.PointerExit).callback.Invoke(null);
            Assert.That(hint.activeSelf, Is.False);
            Assert.That(exited, Is.EqualTo(1));
        }

        [Test]
        public void HoverRebindPreservesOtherEventsAndExternalHoverExtensions()
        {
            Button button = PlainButton("Hover source");
            EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
            int external = 0, clicked = 0, oldHover = 0, newHover = 0;
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => external++);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => clicked++);
            trigger.triggers.Add(enter); trigger.triggers.Add(click);
            UiHoverHint.Bind(button, () => oldHover++, () => { });
            UiHoverHint.Bind(button, () => newHover++, () => { });
            enter.callback.Invoke(null); click.callback.Invoke(null);
            Assert.That(external, Is.EqualTo(1));
            Assert.That(clicked, Is.EqualTo(1));
            Assert.That(oldHover, Is.Zero);
            Assert.That(newHover, Is.EqualTo(1));
            Assert.That(trigger.triggers.Count(entry => entry.eventID == EventTriggerType.PointerEnter), Is.EqualTo(1));
        }

        [Test]
        public void ToastBindingCreatesNonBlockingGroupAndRejectsRetiredOwner()
        {
            GameObject card = UiKit.CreateRect("Notification", host.transform, new Vector2(200f, 80f), Vector2.zero).gameObject;
            bool current = true;
            UiToastLifetime toast = UiToastLifetime.Bind(host, card, () => current);
            toast.Show(4f);
            Assert.That(card.activeSelf, Is.True);
            Assert.That(host.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            toast.Hide();
            Assert.That(card.activeSelf, Is.False);
            current = false;
            toast.Show(4f);
            Assert.That(card.activeSelf, Is.False);
        }

        private Button PlainButton(string name)
        {
            return UiKit.CreateRect(name, host.transform, new Vector2(100f, 40f), Vector2.zero).gameObject.AddComponent<Button>();
        }
    }
}
