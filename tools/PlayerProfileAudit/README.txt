PlayerPrefs profile commit interruption audit

Scope
  Diagnostic tools only; these files are not in the shipping Assets tree.
  Source changes occur only in an isolated workspace/tmp project during build.
  Run.ps1 backs up and hashes the source, adds one gated hook immediately after
  SaveProgress's single SaveSeaChartProgress() or QueueSeaChartProgress() call,
  automatically selected from the source and recorded in the manifest and each
  stage report, then restores the original source
  byte-for-byte in finally. Production source under UnityClient is never edited.
  Uses the existing PlayerPerformanceAuditBuilder without changing it.

Identity and scheduling
  ShatteredSailAudit / Optimization20261008 is required inside every process.
  Seed clears that audit identity's PlayerPrefs, never the normal product identity.
  Do not overlap this experiment with the Player performance run: they deliberately
  share the diagnostic identity. Checkpoints use fresh per-stage audit directories.
  Do not run this script while another Unity process owns the isolated project.

Run after root authorizes Unity execution
  & 'D:/Game/Tools/PlayerProfileAudit/Run.ps1'
  Optional -Project, -Output, -Unity accept absolute paths; project must be under
  workspace/tmp, output must be inside the workspace. Use fresh output for reruns.
  -BuildOnly creates the instrumented Player, then restores isolated source.
  -RunOnly uses an already-built instrumented Player and its source manifest.
  -RunOnly -ExistingBuild <absolute old build/ProfileCommitAudit.exe> uses that
  build in place with a fresh -Output. Every original recorded EXE/DLL hash is
  verified, and reused-build.json links the original source/binary manifests.
  Example retry without rebuilding or copying the Player:
    & 'D:/Game/Tools/PlayerProfileAudit/Run.ps1' -RunOnly `
      -ExistingBuild 'D:/Game/Artifacts/SequentialOptimization20261008/02-Stability/ProfileCommit/build/ProfileCommitAudit.exe' `
      -Output 'D:/Game/Artifacts/SequentialOptimization20261008/02-Stability/ProfileCommit-retry'
  Default output: Artifacts/SequentialOptimization20261008/02-Stability/ProfileCommit

Processes
  01 seed: explicit profile 1000 gold, 120 water/food, 8 fragments, pirate XP 100.
  02 inspect-seed: read PlayerPrefs before scene Awake, exit without Unity savers.
  03 crash: real session craft (4 fragments), fixed-chart start, safe return and
     return settlement; explicit 137-gold fixture grant; invoke same-source
     SaveProgress. Do not call Render between mutations and that call.
     Audit-only hook writes the stopped PlayerPrefs cache report, atomically
     publishes crash-stop.txt, then blocks the main thread without further saves.
     Runner confirms marker PID matches its own child, snapshots the audit registry,
     then kills only that recorded child. No name-based or global process killing.
  04 inspect-crash: independent process reads raw PlayerPrefs before scene Awake.
  05 control-seed: independently reset and recommit the same seed profile.
  06 complete: same real session operations and SaveProgress without arming hook.
  07 inspect-complete: independent process checks full commit survived restart.
  Non-crash stages call Environment.Exit only after writing their evidence; this
  prevents OnApplicationQuit or a later Awake from silently flushing more fields.
  On the observed Mono Player, Environment.Exit can stall after the complete
  report. Runner verifies matching PID, mode, audit identity, passed status and
  completedUtc, allows one second to exit, then terminates only its owned child.
  processes.json records this as audit termination after a completed report,
  never a clean shutdown. Failed, malformed or foreign reports cannot pass.

Evidence and interpretation
  source-and-stop-manifest.json records source hashes, exact hook and stop site.
  build-binary-hashes.json records executable/managed assembly hashes.
  before/ preserves every existing file changed by runner; instrumented source is
  also retained as a text artifact. Logs and processes.json identify all children.
  registry-*.json captures every value from the audit identity's Windows registry
  key, including native value kind and Base64 binary values. These reads do not
  modify the registry. Logical stage reports include both complete chart/treasure
  JSON and gold/cargo values, session revision and saved chart revision.
  result.json classifies changed observed fields after kill as old, pending, mixed,
  or other inconsistent values, and independently validates the complete-save control.
  Raw chart.lastRandomEvent has intentional production entropy: compare each stage
  to its own expected session snapshot, not to another process's random event.
  The baseline stops after SaveSeaChartProgress's inner flush. The repaired build
  stops at the equivalent source position after QueueSeaChartProgress returns,
  before gold/cargo are staged and before the final flush. This does not claim
  that PlayerPrefs provides general transactions or that every other writer was
  audited. Existing standalone SaveSeaChartProgress callers still flush.
  This is one interrupted SaveProgress call, not all crash points, and not a claim
  that the seeded reward fixture was earned through a player's normal journey.

Status
  Tools were written and statically compiled without starting Unity.
  Runtime reproduction is pending root execution and must not be reported as proven.
