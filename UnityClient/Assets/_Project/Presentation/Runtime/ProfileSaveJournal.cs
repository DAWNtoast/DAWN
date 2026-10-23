using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Persistence;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    // A before-image for the keys written INSIDE CoreGameplayDemoController.SaveProgress.
    // It does not transact earlier gameplay writes, standalone chart updates or voyage checkpoints.
    public static class ProfileSaveJournal
    {
        private const string Format = "ShatteredSail.ProfileSaveJournal";
        private const int CurrentVersion = 6;
        private const string VoyageThreeEventsKey = "ShatteredSail.Achievements.Unlocked.v1.voyage_three_events";
        private static readonly string[] VersionThreeAchievementKeys =
        {
            "ShatteredSail.Achievements.Unlocked.v1.voyage_half_chart",
            "ShatteredSail.Achievements.Unlocked.v1.voyage_event_arrival",
        };
        private static string directoryOverride;
        private static string activePayload;
        private static string activeDirectory;
        // Read compatibility only: retired records are validated but never restored.
        private static readonly string[] RetiredAchievementKeys =
        {
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_prepare",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_long",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_resolved",
        };
        private static readonly KeySpec[] Specs = BuildSpecs();
        private static readonly KeySpec[] VersionFiveSpecs = Specs.Where(spec => !AchievementCatalog.ChallengeEvidenceKeys.Contains(spec.Key)).ToArray();
        private static readonly KeySpec[] VersionFourSpecs = VersionFiveSpecs.Where(spec => spec.Key != CoreGameplayDemoController.WarehouseIncomeKey).ToArray();
        private static readonly KeySpec[] VersionThreeSpecs = VersionFourSpecs.Where(spec => spec.Key != CoreGameplayDemoController.ShipPortProgressKey).ToArray();
        private static readonly KeySpec[] VersionTwoSpecs = VersionThreeSpecs.Where(spec => !VersionThreeAchievementKeys.Contains(spec.Key)).ToArray();
        private static readonly KeySpec[] VersionOneSpecs = VersionTwoSpecs.Where(spec => spec.Key != VoyageThreeEventsKey).ToArray();
        public static IReadOnlyList<string> ProtectedKeys { get; } = Array.AsReadOnly(Specs.Select(spec => spec.Key).ToArray());
        private static string DirectoryPath => Path.GetFullPath(directoryOverride ?? Path.Combine(Application.persistentDataPath, "ProfileSaveJournal"));

        [Serializable] private sealed class Record
        {
            public string format, company, product, profileScope;
            public int version;
            public Entry[] entries;
        }

        [Serializable] private sealed class Entry
        {
            public string key, type, stringValue;
            public bool exists;
            public int intValue;
        }

        private readonly struct KeySpec
        {
            public readonly string Key, Type;
            public KeySpec(string key, string type) { Key = key; Type = type; }
        }

        public static bool TryBegin(out string notice)
        {
            notice = null;
            try
            {
                var store = new ProtectedCheckpointStore(DirectoryPath);
                if (!TryRead(store, out Record pending, out _, out notice)) return false;
                if (pending != null || activePayload != null)
                {
                    notice = "上次角色保存尚未完成，请先恢复上次存档，未覆盖保存日志。";
                    return false;
                }
                string port = PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
                if (!CoreVoyageSession.DestinationPorts.Contains(port))
                {
                    notice = "角色当前港口无法识别，已停止保存并保留原档案。";
                    return false;
                }
                var record = new Record
                {
                    format = Format, version = CurrentVersion, company = Application.companyName, product = Application.productName,
                    profileScope = PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty),
                    entries = Specs.Select(Capture).ToArray(),
                };
                string payload = JsonUtility.ToJson(record);
                // Keep the empty marker until both copies exist. No profile key
                // may be changed if journaling fails or is interrupted here.
                if (!store.TryClear(out notice) || !store.TrySave(payload, out notice)) return false;
                if (!TryRead(store, out Record verified, out string verifiedPayload, out notice) || verified == null || verifiedPayload != payload)
                {
                    notice = notice ?? "角色保存日志写入后校验失败，已停止保存。";
                    return false;
                }
                activeDirectory = DirectoryPath;
                activePayload = payload;
                return true;
            }
            catch (Exception error) { notice = Failure("建立", error); return false; }
        }

        // The caller must first finish every profile write and PlayerPrefs.Save.
        // A crash before this clear safely rolls the whole protected group back.
        public static bool TryComplete(out string notice)
        {
            notice = null;
            try
            {
                var store = new ProtectedCheckpointStore(DirectoryPath);
                if (activePayload == null || activeDirectory != DirectoryPath)
                {
                    notice = "当前进程没有可完成的角色保存，原日志已保留。";
                    return false;
                }
                if (!TryRead(store, out Record record, out string payload, out notice)) return false;
                if (record == null || payload != activePayload)
                {
                    notice = "角色保存日志已改变，已停止完成保存并保留原文件。";
                    return false;
                }
                if (!store.TryClear(out notice)) return false;
                ForgetActive();
                return true;
            }
            catch (Exception error) { notice = Failure("完成", error); return false; }
        }

        public static bool TryRecover(out string notice)
        {
            notice = null;
            try
            {
                var store = new ProtectedCheckpointStore(DirectoryPath);
                if (!TryRead(store, out Record record, out _, out notice)) return false;
                if (record == null) { ForgetActive(); return true; }
                // TryRead validates the ENTIRE set before the first mutation.
                // Keep the journal until every replayed key and its flush finish;
                // replaying this loop after another interruption is idempotent.
                foreach (Entry entry in record.entries)
                {
                    if (!RetiredAchievementKeys.Contains(entry.key)) RestoreEntry(entry);
                }
                // Old before-images cannot retain unlocks introduced in a
                // later version and produced by the interrupted save.
                if (record.version == 1) PlayerPrefs.DeleteKey(VoyageThreeEventsKey);
                if (record.version < 3)
                    foreach (string key in VersionThreeAchievementKeys) PlayerPrefs.DeleteKey(key);
                // A pre-location journal predates migration, so an interrupted
                // newer save must not leave the fleet at its uncommitted ports.
                if (record.version < 4) PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
                // An older journal cannot commit part of a newly introduced
                // collection-income claim independently of the account balance.
                if (record.version < 5) PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseIncomeKey);
                if (record.version < 6)
                    foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                if (!store.TryClear(out notice)) return false;
                ForgetActive();
                notice = "上次角色保存被中断，已恢复到该次保存之前的完整记录。";
                return true;
            }
            catch (Exception error) { notice = Failure("恢复", error); return false; }
        }

        // Only the explicitly confirmed new-game path may retire a damaged or
        // unfinished journal without replay. Preserve even semantically invalid
        // payloads before the store commits its clear marker.
        public static bool TryReset(out string notice)
        {
            notice = null;
            try
            {
                PreserveResetEvidence();
                if (!new ProtectedCheckpointStore(DirectoryPath).TryClear(out notice)) return false;
                ForgetActive();
                return true;
            }
            catch (Exception error) { notice = Failure("重置", error); return false; }
        }

        private static bool TryRead(ProtectedCheckpointStore store, out Record record, out string payload, out string notice)
        {
            record = null;
            if (!store.TryLoad(out payload, out notice)) return string.IsNullOrEmpty(notice);
            Record candidate = JsonUtility.FromJson<Record>(payload);
            if (candidate == null || candidate.format != Format || candidate.version < 1 || candidate.version > CurrentVersion ||
                candidate.company != Application.companyName || candidate.product != Application.productName ||
                candidate.profileScope == null || candidate.profileScope != PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty) ||
                candidate.entries == null)
            {
                notice = "角色保存日志损坏、版本不兼容或不属于当前角色，已停止读取和覆盖，原文件已保留。";
                return false;
            }
            KeySpec[] expected = candidate.version == 1 ? VersionOneSpecs : candidate.version == 2 ? VersionTwoSpecs :
                candidate.version == 3 ? VersionThreeSpecs : candidate.version == 4 ? VersionFourSpecs :
                candidate.version == 5 ? VersionFiveSpecs : Specs;
            bool includesRetired = candidate.version == 1 &&
                candidate.entries.Length == VersionOneSpecs.Length + RetiredAchievementKeys.Length;
            // Each version requires its exact manifest. Missing newer keys
            // never turns a damaged current record into a valid older one.
            if (candidate.entries.Length != expected.Length && !includesRetired)
            {
                notice = "角色保存日志键或类型不完整，已停止恢复，原文件已保留。";
                return false;
            }
            var remaining = new Dictionary<string, string>(expected.Length, StringComparer.Ordinal);
            foreach (KeySpec spec in expected) remaining.Add(spec.Key, spec.Type);
            if (includesRetired)
                foreach (string key in RetiredAchievementKeys) remaining.Add(key, "int");
            foreach (Entry entry in candidate.entries)
            {
                if (entry == null || entry.key == null || !remaining.TryGetValue(entry.key, out string type) ||
                    entry.type != type || entry.stringValue == null)
                {
                    notice = "角色保存日志键或类型不完整，已停止恢复，原文件已保留。";
                    return false;
                }
                remaining.Remove(entry.key);
            }
            if (remaining.Count != 0)
            {
                notice = "角色保存日志键或类型不完整，已停止恢复，原文件已保留。";
                return false;
            }
            record = candidate;
            return true;
        }

        private static Entry Capture(KeySpec spec)
        {
            bool exists = PlayerPrefs.HasKey(spec.Key);
            // A wrong native value type must not turn into a default value in
            // the before-image. Different fallback values reveal a type miss.
            if (exists && ((spec.Type == "string" && PlayerPrefs.GetString(spec.Key, "journal-type-a") != PlayerPrefs.GetString(spec.Key, "journal-type-b")) ||
                (spec.Type == "int" && PlayerPrefs.GetInt(spec.Key, 0) != PlayerPrefs.GetInt(spec.Key, 1))))
                throw new InvalidDataException("角色存档字段类型不兼容：" + spec.Key);
            return new Entry
            {
                key = spec.Key, type = spec.Type, exists = exists,
                stringValue = spec.Type == "string" ? PlayerPrefs.GetString(spec.Key, string.Empty) : string.Empty,
                intValue = spec.Type == "int" ? PlayerPrefs.GetInt(spec.Key, 0) : 0,
            };
        }

        private static void RestoreEntry(Entry entry)
        {
            if (!entry.exists) PlayerPrefs.DeleteKey(entry.key);
            else if (entry.type == "string") PlayerPrefs.SetString(entry.key, entry.stringValue);
            else PlayerPrefs.SetInt(entry.key, entry.intValue);
        }

        private static void ForgetActive() { activePayload = null; activeDirectory = null; }
        private static string Failure(string operation, Exception error) => "无法" + operation + "角色保存日志，已停止档案读写；请保留文件并修复后重试：" + error.Message;

        private static void PreserveResetEvidence()
        {
            string directory = DirectoryPath;
            // A healthy retired/empty journal needs no new archive on every
            // ordinary new game. Active or unreadable records retain all bytes.
            if (!new ProtectedCheckpointStore(directory).TryLoad(out _, out string notice) && string.IsNullOrEmpty(notice)) return;
            string[] names = { ProtectedCheckpointStore.FileName, ProtectedCheckpointStore.FileName + ".bak1",
                ProtectedCheckpointStore.FileName + ".bak2", ProtectedCheckpointStore.FileName + ".bak3",
                ProtectedCheckpointStore.FileName + ".cleared" };
            string archive = null;
            foreach (string name in names)
            {
                string path = Path.Combine(directory, name);
                if (!File.Exists(path)) continue;
                if (archive == null)
                {
                    archive = Path.Combine(directory, "ResetHistory", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(archive);
                }
                using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destination = new FileStream(Path.Combine(archive, name), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(destination);
                    destination.Flush(true);
                }
            }
        }

        private static KeySpec[] BuildSpecs()
        {
            var specs = new List<KeySpec>();
            foreach (string key in new[]
            {
                CoreGameplayDemoController.ComponentInventoryKey, CoreGameplayDemoController.WarehouseOwnedKey,
                CoreGameplayDemoController.WarehouseDisplayKey, CoreGameplayDemoController.PortWarehouseCargoKey,
                CoreGameplayDemoController.WarehouseIncomeKey,
                CoreGameplayDemoController.CollectionProgressKey, CoreGameplayDemoController.TreasureProgressKey,
                CoreGameplayDemoController.SeaChartProgressKey, CoreGameplayDemoController.GameClockKey,
                ProfileGoldPersistence.DecimalKey, CoreGameplayDemoController.CurrentPortKey,
                CoreGameplayDemoController.ShipPortProgressKey,
                CoreGameplayDemoController.LastVoyageExplorationCoverageKey,
                CoreGameplayDemoController.BattleDeckKey, CoreGameplayDemoController.BattleCardUpgradeKey,
                CoreGameplayDemoController.SpecialCardExperienceKey, CoreGameplayDemoController.AcquiredSpecialCardsKey,
            }) specs.Add(new KeySpec(key, "string"));
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                specs.Add(new KeySpec(CoreGameplayDemoController.WarehouseOwnedPortKey(port), "string"));
                specs.Add(new KeySpec(CoreGameplayDemoController.WarehouseDisplayPortKey(port), "string"));
            }
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                specs.Add(new KeySpec(CoreGameplayDemoController.StageBattleDeckKey(stage), "string"));
            foreach (string key in new[]
            {
                CoreGameplayDemoController.WarehouseDisplayRecordKey, CoreGameplayDemoController.WarehousePortMigrationKey,
                CoreGameplayDemoController.ProfileGoldKey, CoreGameplayDemoController.SelectedShipKey,
                CoreGameplayDemoController.BattleDeckEditedKey,
            }) specs.Add(new KeySpec(key, "int"));
            foreach (string cargo in new[] { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" })
                specs.Add(new KeySpec(CoreGameplayDemoController.CargoKey(cargo), "int"));
            foreach (string key in AchievementCatalog.PersistenceKeys) specs.Add(new KeySpec(key, "int"));
            return specs.OrderBy(spec => spec.Key, StringComparer.Ordinal).ToArray();
        }
    }

    public sealed class ProfileSaveException : Exception
    {
        public ProfileSaveException(string message, Exception inner = null) : base(message, inner) { }
    }
}
