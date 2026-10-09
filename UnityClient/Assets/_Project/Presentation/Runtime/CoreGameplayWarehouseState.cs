using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string WarehouseOwnedKey = "ShatteredSail.Warehouse.Owned.v1";
        public const string WarehouseDisplayKey = "ShatteredSail.Warehouse.Display.v1";
        public const string WarehousePortMigrationKey = "ShatteredSail.Warehouse.PortMigration.v1";
        public const string PortWarehouseCargoKey = "ShatteredSail.PortWarehouse.Cargo.v1";

        private readonly Dictionary<string, CoreWarehouseCollection> warehouseCollections =
            new Dictionary<string, CoreWarehouseCollection>(StringComparer.Ordinal);
        private CoreVoyageSession warehouseCollectionSession;
        private CoreWarehouseCollection warehouseCollection => CollectionForPort(CurrentWarehousePort);
        public CoreWarehouseCollection WarehouseCollection => warehouseCollection;

        private string CurrentWarehousePort => NormalizeWarehousePort(session == null ? null : session.CurrentPort);

        public static string WarehouseOwnedPortKey(string portName) =>
            "ShatteredSail.Warehouse.OwnedByPort.v1." + NormalizeWarehousePort(portName);

        public static string WarehouseDisplayPortKey(string portName) =>
            "ShatteredSail.Warehouse.DisplayByPort.v1." + NormalizeWarehousePort(portName);

        private static string NormalizeWarehousePort(string portName) =>
            string.IsNullOrWhiteSpace(portName) ? "断锚港" : portName.Trim();

        private CoreWarehouseCollection CollectionForPort(string portName)
        {
            // Preview rebuilds create a fresh session and must not retain another
            // port's sample collection from an earlier inspector preview.
            if (!ReferenceEquals(warehouseCollectionSession, session))
            {
                warehouseCollections.Clear();
                warehouseCollectionSession = session;
            }
            string port = NormalizeWarehousePort(portName);
            if (!warehouseCollections.TryGetValue(port, out CoreWarehouseCollection collection))
            {
                collection = new CoreWarehouseCollection();
                warehouseCollections.Add(port, collection);
            }
            return collection;
        }

        private void RestoreWarehouseCollection()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null) return;
            warehouseCollections.Clear();
            warehouseCollectionSession = session;
            var ports = new HashSet<string>(CoreVoyageSession.DestinationPorts, StringComparer.Ordinal);
            ports.Add(CurrentWarehousePort);
            foreach (string port in ports)
                CollectionForPort(port).Import(PlayerPrefs.GetString(WarehouseOwnedPortKey(port), string.Empty),
                    PlayerPrefs.GetString(WarehouseDisplayPortKey(port), string.Empty));
            session.PortWarehouse.Restore(PlayerPrefs.GetString(PortWarehouseCargoKey, string.Empty));

            if (PlayerPrefs.GetInt(WarehousePortMigrationKey, 0) != 0) return;
            // The legacy global ownership key becomes an achievement union after
            // migration. Never import that union into another port on later loads.
            var legacy = new CoreWarehouseCollection();
            legacy.Import(PlayerPrefs.GetString(WarehouseOwnedKey, string.Empty),
                PlayerPrefs.GetString(WarehouseDisplayKey, string.Empty));
            CoreWarehouseCollection destination = warehouseCollection;
            foreach (string id in legacy.OwnedIds) destination.Grant(id);
            for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
            {
                string id = legacy.DisplayedAt(slot);
                if (string.IsNullOrEmpty(id) || destination.IsDisplayed(id)) continue;
                if (string.IsNullOrEmpty(destination.DisplayedAt(slot))) destination.TryDisplay(id, slot);
            }
            // Persist the destination and migration marker together before a port
            // change, including old saves that have not performed another action.
            SaveWarehouseCollection();
            PlayerPrefs.Save();
        }

        private void SaveWarehouseCollection()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null) return;
            CoreWarehouseCollection current = warehouseCollection;
            var union = new CoreWarehouseCollection();
            foreach (KeyValuePair<string, CoreWarehouseCollection> pair in warehouseCollections)
            {
                PlayerPrefs.SetString(WarehouseOwnedPortKey(pair.Key), pair.Value.ExportOwned());
                PlayerPrefs.SetString(WarehouseDisplayPortKey(pair.Key), pair.Value.ExportDisplay());
                foreach (string id in pair.Value.OwnedIds) union.Grant(id);
            }
            PlayerPrefs.SetString(WarehouseOwnedKey, union.ExportOwned());
            PlayerPrefs.SetString(WarehouseDisplayKey, current.ExportDisplay());
            int displayedRecord = PlayerPrefs.GetInt(WarehouseDisplayRecordKey, 0);
            foreach (CoreWarehouseCollection collection in warehouseCollections.Values)
            {
                int displayed = 0;
                for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                    if (!string.IsNullOrEmpty(collection.DisplayedAt(slot))) displayed++;
                displayedRecord = Math.Max(displayedRecord, displayed);
            }
            PlayerPrefs.SetInt(WarehouseDisplayRecordKey, displayedRecord);
            PlayerPrefs.SetString(PortWarehouseCargoKey, session.PortWarehouse.Serialize());
            PlayerPrefs.SetInt(WarehousePortMigrationKey, 1);
        }

        // Only the ordinary successful display action invokes this hook. Opening
        // a preview, merely selecting an item or reloading cannot graduate it.
        private void OnWarehouseTreasureDisplayed()
        {
            if (!tutorialActive || !GuidedTutorialWarehousePending || session == null ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 0 ||
                guidedTutorialStep != GuidedTutorialStep.DisplayTreasure ||
                selectedWarehouseTreasureId != CoreWarehouseCollection.TutorialTreasureId ||
                !warehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId)) return;

            guidedTutorialCheckpoint = 5;
            guidedTutorialStep = GuidedTutorialStep.Shipyard;
            PlayerPrefs.DeleteKey(GuidedTutorialWarehousePendingKey);
            PlayerPrefs.SetInt(GuidedTutorialEquipmentPendingKey, 1);
            PlayerPrefs.SetInt(GuidedTutorialCheckpointKey, guidedTutorialCheckpoint);
            PlayerPrefs.SetInt(TutorialCompletedKey, 0);
            portNotice = "老船长的第一枚罗盘已陈列！它指向第七港口的下一段线索。返回港口，进入皇家船坞，在当前旗舰的「配件」页亲自装备白色部件。";
            SaveProgress();
        }
    }
}
