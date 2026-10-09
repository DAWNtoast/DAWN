using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared confirmation lifetime; the caller retains business validation and persistence.</summary>
    public sealed class UiConfirmationActions : MonoBehaviour
    {
        private Button confirm, cancel;
        private UnityAction confirmListener, cancelListener;
        private Func<bool> isCurrent, accept;
        private Action dismiss;
        private bool consumed;
        private int revision;

        public static UiConfirmationActions Bind(GameObject owner, Button confirm, Button cancel,
            Func<bool> isCurrent, Func<bool> accept, Action dismiss)
        {
            UiConfirmationActions view = owner.GetComponent<UiConfirmationActions>();
            if (view == null) view = owner.AddComponent<UiConfirmationActions>();
            view.Unbind();
            view.confirm = confirm; view.cancel = cancel;
            view.isCurrent = isCurrent; view.accept = accept; view.dismiss = dismiss;
            view.consumed = false;
            int binding = ++view.revision;
            view.confirmListener = () => view.Accept(binding);
            view.cancelListener = () => view.Cancel(binding);
            confirm.onClick.AddListener(view.confirmListener);
            cancel.onClick.AddListener(view.cancelListener);
            return view;
        }

        private bool Current(int binding, Button control) => binding == revision && !consumed &&
            isActiveAndEnabled && gameObject.activeInHierarchy && control != null &&
            control.isActiveAndEnabled && control.IsInteractable() && (isCurrent == null || isCurrent());

        private void Accept(int binding)
        {
            if (!Current(binding, confirm)) return;
            consumed = true; // Lock before invoking external code or a queued second click.
            bool completed = accept == null || accept();
            // An explicit false means a validation/save failure that is safe to retry.
            if (!completed && binding == revision) consumed = false;
        }

        private void Cancel(int binding)
        {
            if (!Current(binding, cancel)) return;
            consumed = true;
            dismiss?.Invoke();
        }

        private void OnEnable() => consumed = false;
        private void OnDestroy() { ++revision; Unbind(); }
        private void Unbind()
        {
            if (confirm != null && confirmListener != null) confirm.onClick.RemoveListener(confirmListener);
            if (cancel != null && cancelListener != null) cancel.onClick.RemoveListener(cancelListener);
        }
    }
}
