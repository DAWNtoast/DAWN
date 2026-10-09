using System;
using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private IEnumerator RefreshDispatchDeadline(RectTransform panel, long endsAt)
        {
            Text status = panel.Find("Active Dispatch Status")?.GetComponent<Text>();
            string summary = status == null ? string.Empty : status.text.Substring(0, status.text.LastIndexOf('\n') + 1);
            while (panel != null && session != null && session.Phase == CoreDemoPhase.Port && activePortFacility == 5)
            {
                long remaining = Math.Max(0L, endsAt - DateTime.UtcNow.Ticks);
                if (remaining == 0L)
                {
                    Render();
                    yield break;
                }
                if (status != null) status.text = summary + "剩余约 " + FormatDispatchRemaining(remaining);
                yield return new WaitForSecondsRealtime(1f);
            }
        }
    }
}
