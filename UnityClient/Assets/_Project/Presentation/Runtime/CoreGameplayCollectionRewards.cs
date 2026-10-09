using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string CollectionProgressKey = "ShatteredSail.Collection.Progress.v1";
        public const string WarehouseDisplayRecordKey = "ShatteredSail.Warehouse.DisplayRecord.v1";
        private CoreVoyageSession collectionProgressOwner;
        private int savedCollectionRevision = -1;

        private void RestoreCollectionRewards()
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked || session == null) return;
            try
            {
                string json = PlayerPrefs.GetString(CollectionProgressKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                    session.RestoreCollectionProgress(JsonUtility.FromJson<CoreCollectionProgress>(json));
            }
            catch (ArgumentException) { }
            // Migrate only evidence already present in a player's save. A past
            // tutorial-completed flag alone is not proof of winning its battle.
            session.ImportCollectionHistory(session.CaptureSeaChartProgress(), session.CaptureTreasureProgress());
            session.RecordCollectionPortVisit(session.CurrentPort);
            collectionProgressOwner = null;
            savedCollectionRevision = -1;
        }

        private string WarehouseTreasureOwnerPort(string id)
        {
            CoreWarehouseCollection current = warehouseCollection;
            if (current.Owns(id)) return CurrentWarehousePort;
            foreach (var pair in warehouseCollections)
                if (pair.Value.Owns(id)) return pair.Key;
            return string.Empty;
        }

        private HashSet<string> GlobalWarehouseTreasureIds()
        {
            CoreWarehouseCollection current = warehouseCollection;
            return new HashSet<string>(warehouseCollections.Values.SelectMany(collection => collection.OwnedIds), StringComparer.Ordinal);
        }

        private int GlobalWarehouseTreasureCount => GlobalWarehouseTreasureIds().Count;

        private bool GrantWarehouseTreasureOnce(string id) =>
            string.IsNullOrEmpty(WarehouseTreasureOwnerPort(id)) && warehouseCollection.Grant(id);

        private void RefreshCollectionRewards()
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked || session == null) return;
            bool changed = false;
            if (session.Phase == CoreDemoPhase.Port)
            {
                session.RecordCollectionPortVisit(session.CurrentPort);
                var facts = new CoreCollectionFacts
                {
                    recruitedCrewIds = CrewNarrativeCatalog.AllCrew.Where(IsCrewRecruited).Select(crew => crew.Id).ToArray(),
                };
                CoreCollectionProgress progress = session.CaptureCollectionProgress();
                HashSet<string> owned = GlobalWarehouseTreasureIds();
                var granted = new List<string>();
                // Collection milestones may unlock a later milestone in this
                // same claim. Stop at a fixed point; ownership is global even
                // though the physical exhibit stays in its receiving port.
                bool advanced;
                do
                {
                    advanced = false;
                    foreach (CoreCollectionRewardRule rule in CoreCollectionRewardCatalog.All)
                    {
                        if (owned.Contains(rule.Id) || !rule.IsSatisfied(progress, facts, owned) ||
                            !GrantWarehouseTreasureOnce(rule.Id)) continue;
                        owned.Add(rule.Id);
                        granted.Add(FindWarehouseTreasure(rule.Id).Name);
                        advanced = changed = true;
                    }
                } while (advanced);
                if (granted.Count > 0)
                {
                    SaveWarehouseCollection();
                    string names = string.Join("、", granted.Take(3));
                    portNotice = "珍藏入库（" + CurrentWarehousePort + "）：" + names +
                        (granted.Count > 3 ? "等 " + granted.Count + " 件" : string.Empty) +
                        "。全存档收藏 " + owned.Count + " / 40，可到仓库查看获取图鉴。";
                }
            }
            if (!ReferenceEquals(collectionProgressOwner, session) || savedCollectionRevision != session.CollectionProgressRevision)
            {
                PlayerPrefs.SetString(CollectionProgressKey, JsonUtility.ToJson(session.CaptureCollectionProgress()));
                collectionProgressOwner = session;
                savedCollectionRevision = session.CollectionProgressRevision;
                changed = true;
            }
            if (changed) PlayerPrefs.Save();
        }
    }
}
