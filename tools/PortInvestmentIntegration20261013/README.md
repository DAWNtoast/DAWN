# Investment scene integration validation

Run.ps1 uses the existing warm cache at D:/Game/tmp/port-investment-layout-20261011. It never launches UnityClient. Prepare first verifies the old independent PortInvestmentLayoutValidation / PortInvestmentLayout20261011 identity, registers a task marker, no longer snapshots the four retired UIPreviewsForMe scene files, mirrors the current Assets, Packages and ProjectSettings into the exact checked scratch path, and sets PortInvestmentSceneValidation / PortInvestmentSceneValidation20261013 plus a validation application identifier. Library is retained. Sync does the same refresh after subsequent source edits. Both refuse any running Unity editor and held scratch lock. No lock is removed and no process is terminated.

All launch stages are asynchronous and hidden. Each writes launch.json with PID, arguments and baseline path, and Unity.log in a fresh stage/run folder below Artifacts/PortInvestmentRedesign20261013/validation. Run one stage at a time. Wait for the recorded PID to exit before another stage or source Sync. Check for compiler errors and require results.xml to exist with result=Passed, failed=0 and total>0 for test stages. A launched process alone is not success. Capture also requires report.json to have passed=true.

From PowerShell in D:/Game:

    & ./tools/PortInvestmentIntegration20261013/Run.ps1 -Stage Prepare -RunName source01
    & ./tools/PortInvestmentIntegration20261013/Run.ps1 -Stage Compile -RunName compile01
    # After the PID exits and import/compilation succeeds:
    & ./tools/PortInvestmentIntegration20261013/Run.ps1 -Stage EditMode -RunName tests01
    & ./tools/PortInvestmentIntegration20261013/Run.ps1 -Stage PlayMode -RunName tests01
    & ./tools/PortInvestmentIntegration20261013/Run.ps1 -Stage Capture -RunName art01

The example lists stages in order; do not paste all launch lines at once. Optional -TestFilter overrides the default narrow test selections. PortInvestmentFlowTests.cs is a partial CoreGameplayDemoSceneTests file, so the PlayMode filter uses PortInvestment_ method names rather than a nonexistent fixture type. ExecuteMethod runs use -quit; -runTests does not. Graphics remain enabled for capture. No elevation verb is used by the launcher.

If sources change, wait for Unity to exit, then run Sync with a fresh RunName before recompiling/retesting. Sync mirrors current active source. The retired saved-preview stages are no longer part of this workflow.

## Saved previews retired on 2026-10-13

The user retired `UIPreviewsForMe`. `SyncSaved`, `CaptureSaved`, `CopyBack`, and the direct `InvestmentPreviewSync.Run` / `CaptureSaved` entries now stop before any filesystem or Unity operation. Existing four-scene manifests, backups and screenshots remain historical evidence. They must not be copied back or regenerated. `Prepare` and `Sync` continue to mirror active production source without requiring those removed scene files. The independent `Capture.cs` helper and PlayMode tests remain available for current UI and gameplay verification.
