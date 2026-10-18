using System.Globalization;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Observes numeric balances, including mana's separate temporary pool. Does not parse HUD text.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleResourceChangeTracker : MonoBehaviour
    {
        private bool initialized;
        private long amount, temporary, limit;
        private BattleHudChangeMotion motion;
        public long LastDelta { get; private set; }
        public long LastTemporaryDelta { get; private set; }
        public BattleHudChangeMotion Motion => motion;

        public void Observe(long current, long temporaryCurrent, long currentLimit, Font font, bool animateChanges,
            bool distinguishTemporary = false, Behaviour owner = null)
        {
            if (motion == null) motion = GetComponent<BattleHudChangeMotion>() ?? gameObject.AddComponent<BattleHudChangeMotion>();
            motion.SetOwner(owner);
            long change = current - amount, extra = temporaryCurrent - temporary, capacity = currentLimit - limit;
            bool baseline = !initialized;
            initialized = true; amount = current; temporary = temporaryCurrent; limit = currentLimit;
            if (baseline || !animateChanges)
            {
                LastDelta = LastTemporaryDelta = 0;
                motion.Cancel();
                return;
            }
            if (change == 0 && extra == 0 && capacity == 0) return;
            LastDelta = change; LastTemporaryDelta = extra;
            string text = change == 0 ? string.Empty : Signed(change);
            if (extra != 0)
                text += (text.Length == 0 ? string.Empty : "\n") + (distinguishTemporary ? "临" : string.Empty) + Signed(extra);
            if (text.Length == 0) text = "上限" + Signed(capacity);
            long total = change + extra;
            BattleHudChangeKind kind = total > 0 ? BattleHudChangeKind.Gained : total < 0 ? BattleHudChangeKind.Spent : BattleHudChangeKind.Changed;
            Color tint = total > 0 ? new Color(.48f, 1f, .83f) : total < 0 ? new Color(1f, .76f, .42f) : new Color(.73f, .86f, 1f);
            motion.Play(text, tint, kind, font);
        }

        private void OnDisable()
        {
            initialized = false;
            if (motion != null) motion.Cancel();
        }

        private static string Signed(long value) => (value > 0 ? "+" : "−") +
            System.Math.Abs(value).ToString(CultureInfo.InvariantCulture);
    }
}
