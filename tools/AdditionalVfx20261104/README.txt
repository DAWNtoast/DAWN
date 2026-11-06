Additional game VFX validation, 2026-11-04

CompileProduction.ps1 compiles current production sources and optional tests against read-only Unity references.
Validate.ps1 leases the existing tmp/prefab-runtime-20261014 project, journals every changed file,
retains its isolated PlayerPrefs identity, and restores its sources during Cleanup.
Never run the main UnityClient project for these tests. Never enter retired UIPreviewsForMe.

Run from D:/Game:
  ./tools/AdditionalVfx20261104/CompileProduction.ps1 -Run compile1 -IncludeEditMode -IncludePlayMode
  ./tools/AdditionalVfx20261104/Validate.ps1 -Stage Prepare
  ./tools/AdditionalVfx20261104/Validate.ps1 -Stage Test -Run edit1 -TestPlatform EditMode -TestFilter 'AdditionalBattleVfxTests'
  ./tools/AdditionalVfx20261104/Validate.ps1 -Stage Test -Run play1 -TestPlatform PlayMode -TestFilter 'AdditionalVfx_;AdditionalPortVfx_;AdditionalBattleVfx_'
  ./tools/AdditionalVfx20261104/Validate.ps1 -Stage Cleanup

Each run name must be fresh. Wait for the launched Unity process to exit before launching another stage.
For a subsequent full validation, choose a fresh -EvidenceSet and pass it to every Validate stage.
Completed journals are retained as evidence and deliberately cannot be overwritten or reused.
Use Status to inspect logs. Sync accepts exact source/meta paths relative to UnityClient via -SyncFiles.
PlayMode tests emit production screenshots to validation/<run>/Artifacts using -navalVisualCapturePath.
Test filtering accepts semicolon-separated name patterns supported by the installed Unity Test Framework.

No image-generation or external image API is used. Effects reuse existing art and code-native UGUI graphics.
No animation adds a skip button. Completion is automatic; view destruction or owner suspension safely cancels presentation.
Compiler DLL/PDB outputs are removed automatically. Shared Library remains for PrefabRuntimeAudit,
DepartureChartInline, DepartureCrewModal and existing UI validation tools; it is not disposable task data.
