using System;
using System.IO;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Persistence;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    [Serializable]
    internal sealed class VoyageSuspensionRecord
    {
        public string format;
        public int version;
        public string profileScope;
        public CoreVoyageCheckpoint voyage;
        public bool tutorialActive;
        public int tutorialCheckpoint;
        public int navigationInputStep;
        public int selectedOrdinaryCrew;
        public int departureCrewShipIndex;
        public string departureCrewPort;
        public string[] selectedSpecialCrew;
        public string[] standbyCrew;
    }

    // The voyage is kept separate from the last settled port profile: parking a
    // ship must never bank its unclaimed cargo or repeat its departure costs.
    internal static class VoyageSuspensionPersistence
    {
        internal const string Format = "ShatteredSail.VoyageSuspension";
        private static string directoryOverride;
        internal static string PendingNotice;
        private static ProtectedCheckpointStore Store => new ProtectedCheckpointStore(
            directoryOverride ?? Path.Combine(Application.persistentDataPath, "VoyageCheckpoints"));

        internal static bool HasCheckpoint
        {
            get
            {
                if (!Application.isPlaying) return false;
                return !TryRead(out VoyageSuspensionRecord record, out _) || record != null;
            }
        }

        // True + null is an empty slot; false is an unreadable slot that must not
        // silently fall back to a writable port game and overwrite progress.
        internal static bool TryRead(out VoyageSuspensionRecord record, out string notice)
        {
            record = null;
            if (!Store.TryLoad(out string json, out notice)) return string.IsNullOrEmpty(notice);
            try
            {
                VoyageSuspensionRecord candidate = JsonUtility.FromJson<VoyageSuspensionRecord>(json);
                if (!Validate(candidate, out string reason)) { notice = reason; return false; }
                record = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                notice = "航程内容无法解析，原存档已保留，请先备份后检查存档文件。";
                return false;
            }
        }

        internal static bool TrySave(VoyageSuspensionRecord record, out string reason)
        {
            if (!Validate(record, out reason)) return false;
            // Also reject a semantically invalid existing payload. The lower
            // layer checks envelopes; it deliberately knows nothing about games.
            if (!TryRead(out VoyageSuspensionRecord previous, out reason)) return false;
            string json = JsonUtility.ToJson(record);
            ProtectedCheckpointStore store = Store;
            // For an empty slot, keep a durable empty marker until both new
            // copies commit. A failed first write must not expose a half-save.
            if (previous == null && !store.TryClear(out reason)) return false;
            if (!store.TrySave(json, out reason)) return false;
            if (!store.TryLoad(out string verified, out reason) || verified != json)
            {
                reason = "写入后的航程校验失败，请保留当前游戏并重试。";
                return false;
            }
            reason = null;
            return true;
        }

        internal static bool TryClear(out string reason) => Store.TryClear(out reason);

        internal static bool Validate(VoyageSuspensionRecord record, out string reason)
        {
            reason = "航程内容损坏或版本不兼容，已停止读取和覆盖，原存档已保留。";
            if (record == null || record.format != Format || record.version != 1 ||
                record.voyage == null || record.profileScope == null ||
                record.tutorialCheckpoint < 0 || record.tutorialCheckpoint > 5 ||
                record.navigationInputStep < 0 || record.navigationInputStep > 2 ||
                record.selectedOrdinaryCrew < -1 || record.selectedOrdinaryCrew > 1000 ||
                record.departureCrewShipIndex < -1 || record.departureCrewShipIndex >= CoreVoyageSession.Ships.Count ||
                record.departureCrewPort == null || !ValidCrew(record.selectedSpecialCrew) || !ValidCrew(record.standbyCrew)) return false;
            string scope = PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty);
            if (record.profileScope != scope)
            {
                reason = "航程与当前角色存档不匹配，已停止载入，原存档已保留。";
                return false;
            }
            return new CoreVoyageSession().TryRestoreVoyageCheckpoint(record.voyage, out reason);
        }

        private static bool ValidCrew(string[] crew)
        {
            if (crew == null || crew.Length > 64) return false;
            foreach (string id in crew) if (string.IsNullOrWhiteSpace(id) || id.Length > 256) return false;
            return true;
        }
    }
}
