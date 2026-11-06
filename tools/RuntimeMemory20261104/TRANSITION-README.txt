Transition-v2 follow-on resource hypothesis check — 2026-11-04

Purpose
The v1 diagnostic can show retired resource objects disappearing while OS/private/GPU counters remain
at their high-water mark. That does not prove whether allocator/driver capacity is reusable. V2 retains
the exact original six phases and captures, then adds one fixed real production workload: the four pages
of the player/head-on ram comic. It does not choose an artificial allocation size or assume an improvement.
Results describe this synthetic resource transition only, not whole-game peak memory or a full battle.

Files and identity
RuntimeMemory20261104TransitionProbe.cs is a separate source variant defining the SAME namespace/class/
Bootstrap as V1. NEVER place both source files in one Unity assembly. It is compiled alone into the same
diagnostic assembly name from the actual baseline Player Managed dependencies. Neither production
UnityClient nor the borrowed Unity project's code is changed by the package script.
V1 source, Measure.ps1 and Compare.ps1 remain unchanged. V2 uses MeasureTransition.ps1 and
CompareTransition.ps1 and reports probeVersion=transition-v2 with eight completed windows.

Procedure
  ./tools/RuntimeMemory20261104/TransitionProbePackage.ps1 -Stage Compile
  # Review compile-verification.json and bootstrap-registrations-before.json, then authorize install.
  ./tools/RuntimeMemory20261104/TransitionProbePackage.ps1 -Stage Install
  ./tools/RuntimeMemory20261104/MeasureTransition.ps1 -Executable <baseline-exe> -OutputDirectory <new-calibration-directory>
  # Inspect successful 8-window calibration and real ram_open PNG before accepting formal A/B trials.
  # Run B1/O1/B2/O2/B3/O3 with MeasureTransition and compare using:
  ./tools/RuntimeMemory20261104/CompareTransition.ps1 -TrialsRoot <root-with-B1-O1-B2-O2-B3-O3> -OutputDirectory <new-comparison-directory>

Compile is a standalone C# compiler invocation only, not Unity or Player. It records source, compiler,
all baseline Managed reference hashes, and new PE metadata. It verifies each package's existing
RuntimeInitializeOnLoads entry matches the exact assembly/namespace/type/Bootstrap method with
BeforeSceneLoad=1, and the new DLL actually contains static void Bootstrap(). This static check does not
replace a real calibration run. Install refuses active diagnostic processes, backs up each old canonical
DLL, creates full before/after build manifests, and allows exactly the diagnostic DLL to change in each
package. It installs the same byte-identical DLL in both. Production assemblies, resources, executable,
settings and initialization registrations must remain untouched. All operations stay in task Artifacts.

The new ram fixture uses RamComicCatalog.ImageResource(0,page) for the existing four-page sequence, holds
the same full group before displaying the first page, and releases both array/UI references on close.
The already existing production menu collector performs reclamation. ram_open must report four pages;
ram_closed and every earlier window must report zero. The original 15 target textures/sprites keep their
separate counts, making it possible to distinguish the retired first group from the follow-on content.

Measurement uses the same 250ms CPU and optional Windows process GPU counters. Compare keeps three-trial
medians and ranges and never combines CPU PrivateBytes/WorkingSet with GPU Dedicated/Shared gauges.
The two original PNG captures occur before ram_open, and the ram_open screenshot occurs after its stable
window; this common allocation history is retained and explicitly limits the inference. Both positive
and negative differences are reported. Equal counters do not become a claimed saving.

After evidence is complete, the optional -Stage Restore reinstalls the backed-up V1 canonical DLLs and
verifies complete package manifests match their pre-v2 values. Do not run Restore after the diagnostic
packages were deliberately removed. Preserve sources, hashes, manifest logs, calibration/trial JSON/CSV
and required screenshots. The compiled v2 canonical DLL and previous canonical backups remain required
until measurements/restoration finish; they are not disposable caches. No Library/Temp is created here.
