using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CorePortWarehouse
    {
        private Dictionary<string, int> amounts = new Dictionary<string, int>(StringComparer.Ordinal);

        public int GetAmount(string port, string material)
        {
            if (!TryNormalizePort(port, out string portKey) || !TryNormalizeMaterial(material, out string itemKey)) return 0;
            return amounts.TryGetValue(Key(portKey, itemKey), out int amount) ? amount : 0;
        }

        public string Serialize()
        {
            var result = new StringBuilder("v2");
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
            {
                string material = item.Key;
                int amount = GetAmount(port, material);
                if (amount > 0) result.Append(';').Append(port).Append(',').Append(material).Append(',')
                    .Append(amount.ToString(CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        public bool Restore(string serialized)
        {
            var restored = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(serialized))
            {
                amounts = restored;
                return true;
            }
            string[] records = serialized.Split(';');
            bool legacy = records[0] == "v1";
            if ((!legacy && records[0] != "v2") || records.Length - 1 > CoreVoyageSession.DestinationPorts.Count *
                (legacy ? CoreWarehouseItemCatalog.BasicItems.Count : CoreWarehouseItemCatalog.All.Count))
                return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < records.Length; index++)
            {
                string[] fields = records[index].Split(',');
                if (fields.Length != 3 || !TryNormalizePort(fields[0], out string port) ||
                    !TryNormalizeMaterial(fields[1], out string material) ||
                    (legacy && CoreWarehouseItemCatalog.Find(material).IsRare) ||
                    !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int amount))
                    return false;
                string key = Key(port, material);
                // Restoring duplicate records must never multiply saved stock.
                if (!seen.Add(key)) return false;
                if (amount > 0) restored.Add(key, amount);
            }
            amounts = restored;
            return true;
        }

        internal bool TryAdd(string port, string material, int quantity)
        {
            if (quantity <= 0 || !TryNormalizePort(port, out string portKey) ||
                !TryNormalizeMaterial(material, out string itemKey)) return false;
            int current = GetAmount(portKey, itemKey);
            if (quantity > int.MaxValue - current) return false;
            amounts[Key(portKey, itemKey)] = current + quantity;
            return true;
        }

        internal bool TryRemove(string port, string material, int quantity)
        {
            if (quantity <= 0 || !TryNormalizePort(port, out string portKey) ||
                !TryNormalizeMaterial(material, out string itemKey)) return false;
            int current = GetAmount(portKey, itemKey);
            if (current < quantity) return false;
            string key = Key(portKey, itemKey);
            if (current == quantity) amounts.Remove(key);
            else amounts[key] = current - quantity;
            return true;
        }

        internal static bool TryNormalizePort(string value, out string port)
        {
            port = (value ?? string.Empty).Trim();
            foreach (string known in CoreVoyageSession.DestinationPorts)
                if (port == known) return true;
            return false;
        }

        internal static bool TryNormalizeMaterial(string value, out string material)
        {
            material = (value ?? string.Empty).Trim().ToLowerInvariant();
            return CoreWarehouseItemCatalog.Find(material) != null;
        }

        private static string Key(string port, string material) => port + ":" + material;
    }
}
