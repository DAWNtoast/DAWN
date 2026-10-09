using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Pointer-only hints. Keep EventTrigger entries available for existing hover extensions.</summary>
    public sealed class UiHoverHint : MonoBehaviour
    {
        private Button control;
        private GameObject hint;
        private Action enter, leave;
        private bool shown;
        private EventTrigger.Entry enterEntry, exitEntry;
        private UnityAction<BaseEventData> enterListener, exitListener;

        public static UiHoverHint Bind(Button control, Action enter, Action leave, GameObject hint = null)
        {
            UiHoverHint view = control.GetComponent<UiHoverHint>();
            if (view == null) view = control.gameObject.AddComponent<UiHoverHint>();
            view.Hide();
            view.Unbind();
            view.control = control; view.enter = enter; view.leave = leave; view.hint = hint;
            if (hint != null)
            {
                CanvasGroup group = hint.GetComponent<CanvasGroup>();
                if (group == null) group = hint.AddComponent<CanvasGroup>();
                group.interactable = false; group.blocksRaycasts = false;
                hint.SetActive(false);
            }
            EventTrigger trigger = control.GetComponent<EventTrigger>();
            if (trigger == null) trigger = control.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
            view.enterEntry = FindOrAdd(trigger, EventTriggerType.PointerEnter);
            view.exitEntry = FindOrAdd(trigger, EventTriggerType.PointerExit);
            view.enterListener = _ => view.Show();
            view.exitListener = _ => view.Hide();
            view.enterEntry.callback.AddListener(view.enterListener);
            view.exitEntry.callback.AddListener(view.exitListener);
            return view;
        }

        private static EventTrigger.Entry FindOrAdd(EventTrigger trigger, EventTriggerType type)
        {
            EventTrigger.Entry entry = trigger.triggers.Find(item => item.eventID == type);
            if (entry != null) return entry;
            entry = new EventTrigger.Entry { eventID = type };
            trigger.triggers.Add(entry);
            return entry;
        }

        private void Unbind()
        {
            if (enterEntry != null && enterListener != null) enterEntry.callback.RemoveListener(enterListener);
            if (exitEntry != null && exitListener != null) exitEntry.callback.RemoveListener(exitListener);
        }

        private void Show()
        {
            if (shown || !isActiveAndEnabled || control == null || !control.isActiveAndEnabled || !control.IsInteractable()) return;
            shown = true;
            if (hint != null) hint.SetActive(true);
            enter?.Invoke();
        }

        private void Hide()
        {
            if (hint != null) hint.SetActive(false);
            if (!shown) return;
            shown = false;
            leave?.Invoke();
        }

        private void Update() { if (shown && (control == null || !control.isActiveAndEnabled || !control.IsInteractable())) Hide(); }
        private void OnDisable() => Hide();
        private void OnDestroy() => Unbind();
    }
}
