Checkpoint process-crash audit (Windows, no Unity launch)

Run from PowerShell:
  powershell -NoProfile -ExecutionPolicy Bypass -File D:/Game/Tools/CheckpointCrashAudit/Run.ps1

Optional: -Output <new child directory beneath
  D:/Game/Artifacts/SequentialOptimization20261008/02-Stability/CheckpointCrashAudit>
  -ProcessTimeoutSeconds 30 -UnityData D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data

Every run refuses an existing output directory and reparse-point ancestors.
It copies the production ProtectedCheckpointStore.cs and SaveFormat.cs, checks
four unique source anchors, and compiles two console binaries with Unity's
bundled C# compiler/Mono runtime. It never starts Unity or modifies Assets.
Original.exe contains byte-identical source copies and performs all seed,
restart-read, and retry operations. Instrumented.exe adds deterministic stop
calls only in the audit source copy. Both binaries use the same harness.

Six cases cover existing-save temp flush / primary commit, clear-marker commit,
and first-save-after-clear temp flush / primary commit / before marker removal.
Each starts with three complete generations written by the original class.
After the requested stop the worker publishes a flushed, atomically renamed
handshake carrying its token, scenario, PID, start time, directory, and stop.
The parent verifies it and kills only its own retained Start-Process handle.
Timeout cleanup also targets only that owned process. No name-wide process
termination, process-tree kill, external save directory, or Unity process access.

A newly started Original.exe reader verifies exact payload equality (including
4096 numbered rows and a run/scenario identity), or a clean absent state while
the durable clear marker exists. It then retries a new save/read with the
original class, proving the killed process did not leave an unrecoverable lock.
The clear-commit case kills before cleanup, leaving actual old generations on
disk; the reader must not resurrect them. No fabricated corruption is required.

Evidence: source-provenance.json, source-after.json, file-hashes.json,
per-child request/process/handshake/termination/result JSON and stdout/stderr,
per-case AfterKill byte snapshots and filename/length/hash inventory,
summary.json or failure.json.
Crash leftovers remain in their isolated run directory for inspection.

Limits: deterministic Windows process termination with flushed files, not power
loss or filesystem/hardware durability. This tests the checkpoint store only,
not Unity SaveProgress, PlayerPrefs atomicity, gameplay payload validation, or
standalone application recovery/input. A passed run is tied to recorded hashes.
