using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShatteredSail.Persistence
{
    /// <summary>
    /// Stores an opaque voyage snapshot. A save is committed only by an atomic rename;
    /// callers must stay in the game when TrySave fails. The payload's gameplay schema
    /// and semantic validation remain the caller's responsibility.
    /// </summary>
    public sealed class ProtectedCheckpointStore
    {
        public const string FileName = "voyage.checkpoint";
        public const string Identifier = "shattered-sail-voyage-checkpoint";
        public const int SchemaVersion = 1;
        private const int MaximumFileBytes = 64 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string directory;
        private readonly object gate = new object();

        public ProtectedCheckpointStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A save directory is required.", "directory");
            this.directory = Path.GetFullPath(directory);
        }

        public string PrimaryPath { get { return Path.Combine(directory, FileName); } }
        private string ClearedPath { get { return PrimaryPath + ".cleared"; } }

        /// <summary>False with no notice means there is no active checkpoint.</summary>
        public bool TryLoad(out string payload, out string notice)
        {
            payload = null;
            notice = null;
            lock (gate)
            {
                try
                {
                    if (!Directory.Exists(directory)) return false;
                    using (AcquireLock())
                    {
                        Candidate cleared = ReadCandidate(ClearedPath, "cleared");
                        if (cleared.Status == ReadStatus.Valid) return false;
                        if (cleared.Status != ReadStatus.Missing)
                        {
                            notice = FailureMessage(cleared);
                            return false;
                        }

                        List<Candidate> candidates = ReadCandidates();
                        // Never downgrade an unsupported format to an older backup.
                        foreach (Candidate candidate in candidates)
                        {
                            if (candidate.Status != ReadStatus.Unsupported) continue;
                            notice = FailureMessage(candidate);
                            return false;
                        }

                        for (int index = 0; index < candidates.Count; index += 1)
                        {
                            Candidate candidate = candidates[index];
                            if (candidate.Status != ReadStatus.Valid) continue;
                            payload = candidate.Payload;
                            if (index > 0) notice = "航程主存档缺失或损坏，已读取最近的有效备份。损坏文件已保留。";
                            return true;
                        }

                        foreach (Candidate candidate in candidates)
                        {
                            if (candidate.Status == ReadStatus.Missing) continue;
                            notice = "航程存档及备份无法通过完整性校验，已停止读取和覆盖，原文件已保留。";
                            break;
                        }
                        return false;
                    }
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    notice = "无法读取航程存档，原文件已保留：" + exception.Message;
                    return false;
                }
            }
        }

        public bool TrySave(string payload, out string error)
        {
            error = null;
            if (payload == null)
            {
                error = "航程存档内容为空，未执行保存。";
                return false;
            }

            lock (gate)
            {
                try
                {
                    byte[] next = Encode(payload, "active");
                    Directory.CreateDirectory(directory);
                    using (AcquireLock())
                    {
                        Candidate cleared = ReadCandidate(ClearedPath, "cleared");
                        if (cleared.Status != ReadStatus.Valid && cleared.Status != ReadStatus.Missing)
                        {
                            error = FailureMessage(cleared);
                            return false;
                        }

                        List<Candidate> candidates = ReadCandidates();
                        if (cleared.Status == ReadStatus.Valid)
                        {
                            // A durable clear marker suppresses every old generation. Commit the
                            // new file and remove old backups before removing that marker last.
                            PreserveInvalidCandidates(candidates);
                            WriteAtomic(PrimaryPath, next);
                            WriteAtomic(BackupPath(1), next);
                            for (int index = 2; index < candidates.Count; index += 1) DeleteIfPresent(candidates[index].Path);
                            File.Delete(ClearedPath);
                            return true;
                        }

                        bool hasExisting = false;
                        var previous = new List<Candidate>();
                        var distinctPayloads = new HashSet<string>(StringComparer.Ordinal);
                        foreach (Candidate candidate in candidates)
                        {
                            if (candidate.Status == ReadStatus.Unsupported)
                            {
                                error = FailureMessage(candidate);
                                return false;
                            }
                            hasExisting |= candidate.Status != ReadStatus.Missing;
                            if (candidate.Status == ReadStatus.Valid && distinctPayloads.Add(candidate.Payload)) previous.Add(candidate);
                        }

                        if (hasExisting && previous.Count == 0)
                        {
                            error = "航程存档及备份均已损坏，为保护原记录已拒绝覆盖。";
                            return false;
                        }
                        // Repeated saves should not consume all three useful generations.
                        bool hasValidBackup = false;
                        for (int index = 1; index < candidates.Count; index += 1) hasValidBackup |= candidates[index].Status == ReadStatus.Valid;
                        if (candidates[0].Status == ReadStatus.Valid && candidates[0].Payload == payload && hasValidBackup) return true;

                        PreserveInvalidCandidates(candidates);
                        // The primary remains intact throughout backup rotation. Every backup
                        // is itself replaced atomically, so a failed rotation leaves recoverable data.
                        for (int index = SaveFormat.BackupCount; index >= 1; index -= 1)
                        {
                            string backupPath = BackupPath(index);
                            if (previous.Count == 0 && index == 1) WriteAtomic(backupPath, next);
                            else if (index <= previous.Count) WriteAtomic(backupPath, previous[index - 1].Bytes);
                            else DeleteIfPresent(backupPath);
                        }
                        WriteAtomic(PrimaryPath, next);
                        return true;
                    }
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    error = "保存航程失败，请留在当前游戏并重试：" + exception.Message;
                    return false;
                }
            }
        }

        /// <summary>
        /// Explicitly retires this voyage. A flushed clear marker is committed before
        /// deleting anything, preventing stale backup resurrection after interrupted cleanup.
        /// </summary>
        public bool TryClear(out string error)
        {
            error = null;
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    using (AcquireLock())
                    {
                        List<Candidate> candidates = ReadCandidates();
                        PreserveInvalidCandidates(candidates);
                        Candidate oldMarker = ReadCandidate(ClearedPath, "cleared");
                        if (oldMarker.Status == ReadStatus.Corrupt || oldMarker.Status == ReadStatus.Unsupported) PreserveCandidate(oldMarker);
                        WriteAtomic(ClearedPath, Encode(string.Empty, "cleared"));
                        // Cleanup is optional after the marker has committed. If an external
                        // process has a file open, the marker still prevents it being loaded.
                        foreach (Candidate candidate in candidates)
                        {
                            try { DeleteIfPresent(candidate.Path); }
                            catch (Exception exception) when (IsStorageException(exception)) { }
                        }
                        return true;
                    }
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    error = "无法安全结束航程存档：" + exception.Message;
                    return false;
                }
            }
        }

        private FileStream AcquireLock()
        {
            return new FileStream(PrimaryPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private string BackupPath(int generation) { return PrimaryPath + ".bak" + generation.ToString(CultureInfo.InvariantCulture); }

        private List<Candidate> ReadCandidates()
        {
            var candidates = new List<Candidate> { ReadCandidate(PrimaryPath, "active") };
            for (int generation = 1; generation <= SaveFormat.BackupCount; generation += 1) candidates.Add(ReadCandidate(BackupPath(generation), "active"));
            return candidates;
        }

        private static Candidate ReadCandidate(string path, string expectedKind)
        {
            var result = new Candidate { Path = path, Status = ReadStatus.Missing };
            if (!File.Exists(path)) return result;
            if (new FileInfo(path).Length > MaximumFileBytes)
            {
                result.Status = ReadStatus.Corrupt;
                return result;
            }
            result.Bytes = File.ReadAllBytes(path);
            result.Status = ReadStatus.Corrupt;
            try
            {
                string text = Utf8.GetString(result.Bytes);
                string[] lines = text.Split('\n');
                if (lines.Length < 2 || lines[0] != Identifier) return result;
                int schema;
                if (!int.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out schema)) return result;
                if (schema != SchemaVersion)
                {
                    result.Status = ReadStatus.Unsupported;
                    return result;
                }
                if (lines.Length != 5 || lines[2] != expectedKind) return result;
                if (lines[3] != Digest(lines[0] + "\n" + lines[1] + "\n" + lines[2] + "\n" + lines[4])) return result;
                result.Payload = Utf8.GetString(Convert.FromBase64String(lines[4]));
                if (expectedKind == "cleared" && result.Payload.Length != 0) return result;
                result.Status = ReadStatus.Valid;
            }
            catch (FormatException) { }
            catch (DecoderFallbackException) { }
            return result;
        }

        private static byte[] Encode(string payload, string kind)
        {
            string encoded = Convert.ToBase64String(Utf8.GetBytes(payload));
            string prefix = Identifier + "\n" + SchemaVersion.ToString(CultureInfo.InvariantCulture) + "\n" + kind + "\n";
            byte[] bytes = Utf8.GetBytes(prefix + Digest(prefix + encoded) + "\n" + encoded);
            if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("航程存档超出支持的大小。");
            return bytes;
        }

        private static string Digest(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Utf8.GetBytes(text));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static void PreserveInvalidCandidates(List<Candidate> candidates)
        {
            foreach (Candidate candidate in candidates)
                if (candidate.Status == ReadStatus.Corrupt || candidate.Status == ReadStatus.Unsupported) PreserveCandidate(candidate);
        }

        private static void PreserveCandidate(Candidate candidate)
        {
            string preserved = candidate.Path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            // Stream the original rather than decoding it, preserving even oversized files.
            using (var source = new FileStream(candidate.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destination = new FileStream(preserved, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(destination);
                destination.Flush(true);
            }
        }

        private static void WriteAtomic(string destination, byte[] bytes)
        {
            string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Verify the complete bytes before crossing the commit point.
                byte[] written = File.ReadAllBytes(temporary);
                if (written.Length != bytes.Length) throw new IOException("临时存档写入不完整。");
                for (int index = 0; index < written.Length; index += 1)
                    if (written[index] != bytes[index]) throw new IOException("临时存档校验失败。");
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                try { DeleteIfPresent(temporary); }
                catch (Exception exception) when (IsStorageException(exception)) { }
            }
        }

        private static void DeleteIfPresent(string path) { if (File.Exists(path)) File.Delete(path); }

        private static string FailureMessage(Candidate candidate)
        {
            return candidate.Status == ReadStatus.Unsupported
                ? "航程存档版本不受当前游戏支持。为防止进度丢失，已停止读取和覆盖；请使用匹配的游戏版本。"
                : "航程存档保护记录损坏，已停止读取和覆盖，原文件已保留。";
        }

        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException
                || exception is NotSupportedException || exception is ArgumentException || exception is CryptographicException;
        }

        private enum ReadStatus { Missing, Valid, Corrupt, Unsupported }

        private sealed class Candidate
        {
            public string Path;
            public ReadStatus Status;
            public byte[] Bytes;
            public string Payload;
        }
    }
}
