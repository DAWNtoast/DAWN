using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private static readonly int[] portUnlockLevels = { 1, 1, 1, 3, 3, 5 };

        public static int PortUnlockLevel(string port)
        {
            int index = Array.IndexOf(portNames, port);
            return index >= 0 ? portUnlockLevels[index] : 0;
        }

        /// <summary>Current location and confirmed arrival history are evidence of visiting a port.</summary>
        public bool HasVisitedPort(string port) => Array.IndexOf(portNames, port) >= 0 &&
            (port == CurrentPort || collectionPorts.Contains(port));

        /// <summary>Level and arrival prerequisites, independent of the current UI or voyage phase.</summary>
        public string PortUnlockReason(string port)
        {
            int index = Array.IndexOf(portNames, port);
            if (index < 0) return "港口不存在。";
            // Existing saves may have reached a port before these gates existed.
            // Keep that destination available without inventing visits to earlier ports.
            if (HasVisitedPort(port)) return string.Empty;
            var requirements = new List<string>();
            if (PirateLevel < portUnlockLevels[index])
                requirements.Add("海盗等级达到 " + portUnlockLevels[index] + " 级");
            if (index > 0 && !HasVisitedPort(portNames[index - 1]))
                requirements.Add("先到达一次" + portNames[index - 1]);
            return requirements.Count == 0 ? string.Empty : "需" + string.Join("，并", requirements) + "后解锁" + port + "。";
        }

        public bool CanSelectDestinationPort(string port, out string reason)
        {
            string candidate = port?.Trim();
            reason = Phase != CoreDemoPhase.Port ? "只能在港口选择目的港。" :
                candidate == CurrentPort ? "当前已停靠此港。" : PortUnlockReason(candidate);
            return reason.Length == 0;
        }

        /// <summary>Call after restoring both pirate level and permanent arrival history.</summary>
        public void RefreshPortDestinationSelection()
        {
            if (Phase == CoreDemoPhase.Port) EnsureSelectedDestination();
        }
    }
}
