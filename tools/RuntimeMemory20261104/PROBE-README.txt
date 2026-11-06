Runtime memory resource-retention Player probe — 2026-11-04

These four probe/build files are tools only. Never install them in UnityClient/Assets or a delivered game.
The parent Validate.ps1 leases tmp/prefab-runtime-20261014; preserve its lease and journal.
While the borrowed project is idle, copy RuntimeMemory20261104Probe.cs and its runtime asmdef to
Assets/RuntimeMemory20261104Probe. Copy the Builder and Editor asmdef to that folder's Editor subdirectory.
Record each added file and its generated .meta so it can be safely removed after the two builds.

Build command (Unity hidden, -batchmode; -nographics is allowed only for the build Editor):
  -executeMethod ShatteredSail.Editor.RuntimeMemory20261104Builder.BuildFromCommandLine
  -runtimeMemory20261104BuildOutput D:/Game/Artifacts/RuntimeMemory20261104/<fresh-build>/MemoryProbe.exe
The builder contains only MainMenu in its scene list and temporarily sets
ShatteredSailAudit / RuntimeMemory20261104 / Mono / non-Development, restoring PlayerSettings in finally.
The diagnostic is not a playable release package. All Resources assets still follow the real build rules.

Run command (hidden graphical Player, no -nographics):
  -force-d3d11 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
  -runtimeMemory20261104Output D:/Game/Artifacts/RuntimeMemory20261104/<fresh-trial>/probe.json
  -logFile D:/Game/Artifacts/RuntimeMemory20261104/<fresh-trial>/player.log

Probe verifies non-Development isolated company/product/path before beginning; it never writes PlayerPrefs
or player saves. It builds a synthetic Canvas using production resource getters and Configure methods:
six field textures, naval VFX atlas, two boarding school textures, battle symbols, five rarity boards.
After loading MainMenu it renders: initial menu, open art, live art after production menu cleanup,
closed art after production cleanup, reopened art, second close after production cleanup.
It calls only production RuntimeArtMemory.CollectAfterReturningToMenu for collection, never GC.Collect,
UnloadUnusedAssets or UnloadAsset directly. Success proves the probe ran with all 15 targets and 5
generated wood sprites intact while visible; it deliberately does not require closed counts to be zero,
so the same diagnostic runs against baseline and optimization.

Each stable window is 2 seconds, preceded by 0.5 seconds; UTC start/end and real render callbacks are
recorded. Inventories run after windows and store only strings/integers. The loaded/reopened PNG readbacks
also occur after their windows; both builds must run the identical capture sequence. Compare screenshots.
An external sampler should read process PrivateMemorySize64 and WorkingSet64 every 250 ms, align its UTC
samples to these windows, and alternate B1/O1/B2/O2/B3/O3. Verify the two probe DLL SHA256 values match.
Do not add PrivateBytes and WorkingSet together. Unity runtime texture-byte counters may be unavailable
in a release Player; report their availability and use resource counts plus OS samples. This is a
controlled 1920x1080 SDR offscreen resource lifecycle fixture with common RT, probe and driver overhead;
it is neither whole-game peak RAM nor normal-window absolute RAM. Texture dimensions are never presented
as measured GPU memory; the external Windows process GPU counters described below are separate evidence.

The included Measure.ps1 performs that external sampling and validation, with a maximum 120-second run:
  ./tools/RuntimeMemory20261104/Measure.ps1 -Executable <diagnostic.exe> -OutputDirectory <fresh-trial-dir>
It requires no concurrent Unity/diagnostic Player. Timeout cleanup checks the owned PID, process start
time, executable path and exact output argument; it never kills an unrelated process. Logs, raw samples,
launch hashes and failed/partial trial summaries are preserved. Every successful window requires at least
four OS samples and 30 actual rendered frames; all three visible-art windows require 15 textures/5 sprites.
UTC parsing preserves DateTime.Kind on PowerShell versions that deserialize JSON ISO dates automatically.

Measure also uses System.Diagnostics.PerformanceCounter to read GPU Process Memory / Dedicated Usage and
Shared Usage for instances matching this exact pid_<PID>_luid_..._phys_... prefix. They are NumberOfItems64
instantaneous byte gauges; RawValue is read in the same 250ms loop, without Get-Counter's rate-sampling
wait. Instance discovery is refreshed at most once per second and matching adapter instances are summed.
No ready instance or a failed read produces null values with GpuValid=false, never a fabricated zero.
Each window records GpuValidSamples and separate dedicated/shared medians only with at least four valid
readings. These Windows process GPU-memory counters are distinct from CPU private commitment and working
set; never add CPU and GPU counters into a total or label theoretical texture payload as measured VRAM.

Run B1/O1/B2/O2/B3/O3 sequentially, then compare:
  ./tools/RuntimeMemory20261104/Compare.ps1 -TrialsRoot <root-containing-B1-O1-B2-O2-B3-O3> -OutputDirectory <fresh-comparison-dir>
Alternatively pass the six directories explicitly through -Trials, in that order. Compare verifies actual
nonoverlapping alternating timestamps, identical probe DLL hashes, and fixed per-variant executable/
production assembly hashes. It preserves every trial and range, comparing three-trial medians for each
OS metric separately; positive ReductionMiB means lower optimized usage. Overlapping ranges are flagged.
GPU dedicated/shared rows are included only when all six trials have enough valid readings for that
window; otherwise UnavailableGpuMetrics records the missing trials. Earlier CPU-only trials remain readable.

Before building, freeze source/resources/Packages/ProjectSettings and hashes. Only the memory patch may
differ between A/B; do not compare today's source with an older release. Generated diagnostic objects
and renderer are retired on exit. Stop both Player/Editor before removing the exact task-owned probe
folder and generated metas (reject links), then run parent Validate Cleanup to restore borrowed sources.
Keep source snapshots, manifests, logs, reports, trial samples and required images. Keep shared Library;
delete only independently verified, no-longer-needed task build/cache intermediates after archiving evidence.
