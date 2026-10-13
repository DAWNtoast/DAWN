using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>A physical item. Identity and appraisal survive every unload, refit and save.</summary>
    [Serializable]
    public sealed class OwnedShipComponent
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string componentId;
        [SerializeField] private float quality;
        [SerializeField] private int shipIndex = -1;

        public string InstanceId => instanceId;
        public string ComponentId => componentId;
        public float Quality => quality;
        public int ShipIndex => shipIndex;

        internal OwnedShipComponent(string instance, string component, float appraisal, int ship)
        {
            instanceId = instance;
            componentId = component;
            quality = float.IsNaN(appraisal) || float.IsInfinity(appraisal) ? 1f : Mathf.Clamp(appraisal, 0.9f, 1.1f);
            shipIndex = ship;
        }

        internal void MoveTo(int ship) => shipIndex = ship;
    }

    /// <summary>One instance can occupy at most one vessel slot; empty slots remain empty on reload.</summary>
    public sealed class OwnedShipComponentInventory
    {
        [Serializable]
        private sealed class SaveData
        {
            public int version = 2;
            public List<int> initializedShips = new List<int>();
            public List<OwnedShipComponent> items = new List<OwnedShipComponent>();
        }

        private readonly ComponentArtCatalog catalog;
        private readonly Func<string> createIdentity;
        private readonly List<OwnedShipComponent> items = new List<OwnedShipComponent>();
        private readonly HashSet<int> initializedShips = new HashSet<int>();
        private readonly IReadOnlyList<OwnedShipComponent> readOnlyItems;
        public IReadOnlyList<OwnedShipComponent> Items => readOnlyItems;

        public OwnedShipComponentInventory(ComponentArtCatalog catalog, Func<string> createIdentity = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.createIdentity = createIdentity ?? (() => Guid.NewGuid().ToString("N"));
            readOnlyItems = items.AsReadOnly();
        }

        public bool Import(string json, Func<int, bool> ownsShip)
        {
            if (string.IsNullOrWhiteSpace(json) || ownsShip == null) return false;
            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (ArgumentException) { return false; }
            if (data == null || data.version != 2 || data.items == null) return false;
            // Commit only a complete import. Dropping an unknown or duplicate item
            // would permanently erase it when the controller next saves the list.
            var importedItems = new List<OwnedShipComponent>();
            var importedShips = new HashSet<int>();
            if (data.initializedShips != null)
                foreach (int ship in data.initializedShips)
                    if (ownsShip(ship)) importedShips.Add(ship);
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (OwnedShipComponent item in data.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || !identities.Add(item.InstanceId)) return false;
                ComponentArtEntry entry = catalog.GetById(item.ComponentId);
                if (entry == null) return false;
                int ship = item.ShipIndex >= 0 && ownsShip(item.ShipIndex) ? item.ShipIndex : -1;
                if (ship >= 0)
                {
                    importedShips.Add(ship);
                    if (!occupied.Add(ship + ":" + entry.Slot)) ship = -1;
                }
                importedItems.Add(new OwnedShipComponent(item.InstanceId, entry.Id, item.Quality, ship));
            }
            items.Clear();
            items.AddRange(importedItems);
            initializedShips.Clear();
            initializedShips.UnionWith(importedShips);
            return true;
        }

        public string Export()
        {
            List<int> ships = new List<int>(initializedShips);
            ships.Sort();
            return JsonUtility.ToJson(new SaveData { initializedShips = ships, items = items });
        }

        public void EnsureLoadout(int ship, Func<ComponentSlot, ComponentArtEntry> initialItem = null,
            Func<ComponentSlot, float> initialQuality = null)
        {
            if (ship < 0 || !initializedShips.Add(ship)) return;
            for (int index = 0; index < ComponentArtCatalog.SlotCount; index++)
            {
                ComponentSlot slot = (ComponentSlot)index;
                ComponentArtEntry entry = initialItem?.Invoke(slot);
                if (entry == null || entry.Slot != slot) entry = catalog.Get(index * ComponentArtCatalog.EntriesPerSlot);
                items.Add(new OwnedShipComponent(createIdentity(), entry.Id, initialQuality?.Invoke(slot) ?? 1f, ship));
            }
        }

        /// <summary>
        /// Marks a vessel as initialized while leaving every component slot empty.
        ///
        /// A new player's starter hull is deliberately bare.  Keeping the
        /// initialized marker means a later load/refresh cannot repopulate the
        /// slots with the catalogue's free baseline fittings.
        /// </summary>
        public void InitializeEmptyLoadout(int ship)
        {
            if (ship < 0) return;
            initializedShips.Add(ship);
        }

        public OwnedShipComponent Grant(ComponentArtEntry entry, float quality)
        {
            if (entry == null || catalog.GetById(entry.Id) == null) return null;
            OwnedShipComponent item = new OwnedShipComponent(createIdentity(), entry.Id, quality, -1);
            items.Add(item);
            return item;
        }

        public OwnedShipComponent Installed(int ship, ComponentSlot slot)
        {
            if (ship < 0 || (int)slot < 0 || (int)slot >= ComponentArtCatalog.SlotCount) return null;
            foreach (OwnedShipComponent item in items)
                if (item.ShipIndex == ship && catalog.GetById(item.ComponentId)?.Slot == slot) return item;
            return null;
        }

        public bool TryEquip(int ship, string instanceId)
        {
            if (!initializedShips.Contains(ship) || string.IsNullOrEmpty(instanceId)) return false;
            OwnedShipComponent candidate = items.Find(item => item.InstanceId == instanceId);
            if (candidate == null || candidate.ShipIndex != -1) return false;
            ComponentArtEntry entry = catalog.GetById(candidate.ComponentId);
            if (entry == null) return false;
            Installed(ship, entry.Slot)?.MoveTo(-1);
            candidate.MoveTo(ship);
            return true;
        }

        public bool TryUnequip(int ship, ComponentSlot slot)
        {
            OwnedShipComponent installed = Installed(ship, slot);
            if (installed == null) return false;
            installed.MoveTo(-1);
            return true;
        }

        public bool TryRemoveUninstalled(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            int index = items.FindIndex(item => item.InstanceId == instanceId && item.ShipIndex == -1);
            if (index < 0) return false;
            items.RemoveAt(index);
            // Keep initializedShips: selling an unloaded starter fitting must
            // never grant another free copy when that vessel is loaded again.
            return true;
        }
    }
}
