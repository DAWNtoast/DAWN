using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    [Serializable] public sealed class CoreShipPortLocation
    {
        public int shipIndex;
        public string port;
    }

    [Serializable] public sealed class CoreShipPortProgress
    {
        public int version = 1;
        public CoreShipPortLocation[] ships;
    }

    public sealed partial class CoreVoyageSession
    {
        // A sailing flagship retains its departure berth until settlement.
        // Other vessels remain where they were purchased or last disembarked.
        private readonly Dictionary<int, string> shipPorts = new Dictionary<int, string>();

        public string ShipPort(int shipIndex) => shipPorts.TryGetValue(shipIndex, out string port) ? port : string.Empty;
        public bool IsShipAtCurrentPort(int shipIndex) => shipPorts.TryGetValue(shipIndex, out string port) &&
            string.Equals(port, CurrentPort, StringComparison.Ordinal);

        /// <summary>Registers a newly acquired hull without relocating an existing remote vessel.</summary>
        public bool RegisterShipAtCurrentPort(int shipIndex)
        {
            if (Phase != CoreDemoPhase.Port || shipIndex < 0 || shipIndex >= ships.Count ||
                !portNames.Contains(CurrentPort) || shipPorts.ContainsKey(shipIndex) && !IsShipAtCurrentPort(shipIndex)) return false;
            shipPorts[shipIndex] = CurrentPort;
            return true;
        }

        public CoreShipPortProgress CaptureShipPortProgress() => new CoreShipPortProgress
        {
            ships = shipPorts.OrderBy(pair => pair.Key).Select(pair =>
                new CoreShipPortLocation { shipIndex = pair.Key, port = pair.Value }).ToArray(),
        };

        /// <summary>Legacy profiles place inactive owned hulls at their home shipyards.
        /// The saved flagship travels with the player. Ownership itself remains in the profile.</summary>
        public bool RestoreShipPortProgress(CoreShipPortProgress progress, IEnumerable<int> ownedShipIndices, int activeShipIndex)
        {
            if (Phase != CoreDemoPhase.Port || activeShipIndex < 0 || activeShipIndex >= ships.Count ||
                !portNames.Contains(CurrentPort) || !TryReadShipPorts(progress, out Dictionary<int, string> saved)) return false;
            var owned = new HashSet<int>(ownedShipIndices ?? Array.Empty<int>());
            if (owned.Any(index => index < 0 || index >= ships.Count)) return false;
            owned.Add(0);
            owned.Add(activeShipIndex);
            var restored = new Dictionary<int, string>();
            foreach (int index in owned)
            {
                int home = CoreShipMarketCatalog.HomePortIndex(index);
                restored[index] = saved.TryGetValue(index, out string port) ? port : portNames[home];
            }
            restored[activeShipIndex] = CurrentPort;
            shipPorts.Clear();
            foreach (var pair in restored) shipPorts.Add(pair.Key, pair.Value);
            return true;
        }

        private static bool HasShipPortProgress(CoreShipPortProgress progress) => progress?.ships != null && progress.ships.Length > 0;

        private static bool TryReadShipPorts(CoreShipPortProgress progress, out Dictionary<int, string> restored)
        {
            restored = new Dictionary<int, string>();
            if (progress == null) return true;
            // Unity can deserialize a missing inline object as its empty, version-zero shape.
            if (progress.version != 1 && !(progress.version == 0 && !HasShipPortProgress(progress))) return false;
            if (progress.ships == null) return true;
            if (progress.ships.Length > ships.Count) return false;
            foreach (CoreShipPortLocation entry in progress.ships)
            {
                if (entry == null || entry.shipIndex < 0 || entry.shipIndex >= ships.Count ||
                    !portNames.Contains(entry.port) || restored.ContainsKey(entry.shipIndex)) return false;
                restored.Add(entry.shipIndex, entry.port);
            }
            return true;
        }

        private bool RestoreCheckpointShipPorts(CoreShipPortProgress progress)
        {
            if (!TryReadShipPorts(progress, out Dictionary<int, string> restored)) return false;
            if (!HasShipPortProgress(progress)) return true;
            if (!restored.TryGetValue(SelectedShipIndex, out string activePort) || activePort != CurrentPort) return false;
            shipPorts.Clear();
            foreach (var pair in restored) shipPorts.Add(pair.Key, pair.Value);
            return true;
        }
    }
}
