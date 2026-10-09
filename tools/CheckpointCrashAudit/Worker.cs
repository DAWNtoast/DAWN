using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ShatteredSail.Persistence;

// The parent compiles this harness twice: once with an instrumented source copy,
// and once with the untouched source copy. Readers always use the latter binary.
internal static class CheckpointCrashWorker
{
    internal static AuditRequest Current;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("Expected request path and run identity.");
            string requestPath = Path.GetFullPath(args[0]);
            Current = ReadJson<AuditRequest>(requestPath);
            if (!Guid.TryParseExact(Current.token, "N", out _) || Current.token != args[1])
                throw new InvalidDataException("Request identity mismatch.");
            string root = Path.GetFullPath(Current.directory);
            if (!SamePath(root, Directory.GetCurrentDirectory()) || !SamePath(root, Path.GetDirectoryName(requestPath)))
                throw new InvalidDataException("Worker must run inside its owned scenario directory.");
            if (File.ReadAllText(Path.Combine(root, "owner.txt")) != Current.token + "|" + Current.scenario)
                throw new InvalidDataException("Scenario owner mismatch.");
            RequireChild(root, Current.resultPath);
            if (!string.IsNullOrEmpty(Current.handshakePath)) RequireChild(root, Current.handshakePath);
            if (File.Exists(Current.resultPath)) throw new IOException("Result already exists.");
            var store = new ProtectedCheckpointStore(Path.Combine(root, "data"));
            var result = new AuditResult { operation = Current.operation, scenario = Current.scenario,
                token = Current.token, pid = Process.GetCurrentProcess().Id, utc = DateTime.UtcNow.ToString("O") };
            string error;
            switch (Current.operation)
            {
                case "seed":
                    if (Directory.Exists(Path.Combine(root, "data"))) throw new IOException("Seed data already exists.");
                    for (int generation = 1; generation <= 3; generation++)
                        if (!store.TrySave(Payload(generation), out error)) throw new IOException(error);
                    result.success = true;
                    break;
                case "save":
                    if (!store.TrySave(Payload(Current.generation), out error)) throw new IOException(error);
                    result.success = true;
                    break;
                case "clear":
                    if (!store.TryClear(out error)) throw new IOException(error);
                    result.success = true;
                    break;
                case "read":
                    result.loaded = store.TryLoad(out string payload, out string notice);
                    result.notice = notice;
                    result.payloadSha256 = payload == null ? null : Hash(Encoding.UTF8.GetBytes(payload));
                    result.payloadBytes = payload == null ? 0 : Encoding.UTF8.GetByteCount(payload);
                    if (Current.generation == 0)
                    {
                        if (result.loaded || payload != null || notice != null)
                            throw new InvalidDataException("Cleared store resurrected a payload or reported a storage error: " + notice);
                    }
                    else
                    {
                        string expected = Payload(Current.generation);
                        result.expectedSha256 = Hash(Encoding.UTF8.GetBytes(expected));
                        if (!result.loaded || payload != expected || notice != null)
                            throw new InvalidDataException("Expected exactly generation " + Current.generation + "; got a different, partial, absent, or fallback record: " + notice);
                    }
                    result.generation = Current.generation;
                    result.success = true;
                    break;
                default: throw new InvalidDataException("Unsupported operation.");
            }
            WriteNewJson(Current.resultPath, result);
            Console.WriteLine(Current.operation + " verified for " + Current.scenario);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    internal static string Payload(int generation)
    {
        var text = new StringBuilder();
        text.Append("BEGIN|").Append(Current.token).Append('|').Append(Current.scenario).Append('|').Append(generation).Append('\n');
        for (int row = 0; row < 4096; row++)
            text.Append(generation).Append('|').Append(row.ToString("D6")).Append('|').Append(Current.token)
                .Append("|航程完整代|gold=123456|cargo=water,food,wood|map=earned|\n");
        text.Append("END|").Append(generation).Append('|').Append(Current.token);
        return text.ToString();
    }

    internal static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }

    internal static T ReadJson<T>(string path)
    {
        using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }

    internal static void WriteNewJson<T>(string path, T value)
    {
        string temporary = path + ".writing";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
            stream.Flush(true);
        }
        File.Move(temporary, path);
    }

    internal static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    internal static void RequireChild(string root, string path)
    {
        if (!SamePath(root, Path.GetDirectoryName(Path.GetFullPath(path))))
            throw new InvalidDataException("Request path escaped the owned scenario directory.");
    }
}

internal static class CheckpointAuditStop
{
    // Calls to this method exist only in the generated audit copy of the store.
    internal static void Reach(string point, string destination, string temporary)
    {
        AuditRequest request = CheckpointCrashWorker.Current;
        if (request == null || request.stopPoint != point) return;
        if ((point == "after_temp_flush" || point == "after_primary_commit") &&
            Path.GetFileName(destination) != ProtectedCheckpointStore.FileName) return;
        var process = Process.GetCurrentProcess();
        var handshake = new AuditHandshake { token = request.token, scenario = request.scenario,
            point = point, pid = process.Id, processStartTicks = process.StartTime.ToUniversalTime().Ticks,
            directory = Directory.GetCurrentDirectory(), destination = Path.GetFullPath(destination),
            temporary = temporary, primaryExists = File.Exists(Path.Combine(request.directory, "data", ProtectedCheckpointStore.FileName)),
            clearedExists = File.Exists(Path.Combine(request.directory, "data", ProtectedCheckpointStore.FileName + ".cleared")),
            utc = DateTime.UtcNow.ToString("O") };
        CheckpointCrashWorker.WriteNewJson(request.handshakePath, handshake);
        Console.WriteLine("AUDIT STOP " + point);
        Console.Out.Flush();
        // Never resume the write. An absent parent results in a bounded process
        // failure, which the parent cannot misclassify as a successful forced kill.
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < request.holdTimeoutMs) Thread.Sleep(20);
        Environment.Exit(91);
    }
}

// Request members are populated by DataContractJsonSerializer.
#pragma warning disable CS0649
[DataContract] internal sealed class AuditRequest
{
    [DataMember] public string token;
    [DataMember] public string scenario;
    [DataMember] public string directory;
    [DataMember] public string operation;
    [DataMember] public int generation;
    [DataMember] public string stopPoint;
    [DataMember] public string handshakePath;
    [DataMember] public string resultPath;
    [DataMember] public int holdTimeoutMs;
}
#pragma warning restore CS0649

[DataContract] internal sealed class AuditHandshake
{
    [DataMember] public string token;
    [DataMember] public string scenario;
    [DataMember] public string point;
    [DataMember] public int pid;
    [DataMember] public long processStartTicks;
    [DataMember] public string directory;
    [DataMember] public string destination;
    [DataMember] public string temporary;
    [DataMember] public bool primaryExists;
    [DataMember] public bool clearedExists;
    [DataMember] public string utc;
}

[DataContract] internal sealed class AuditResult
{
    [DataMember] public string token;
    [DataMember] public string scenario;
    [DataMember] public string operation;
    [DataMember] public int pid;
    [DataMember] public string utc;
    [DataMember] public bool success;
    [DataMember] public bool loaded;
    [DataMember] public int generation;
    [DataMember] public string notice;
    [DataMember] public string payloadSha256;
    [DataMember] public string expectedSha256;
    [DataMember] public int payloadBytes;
}
