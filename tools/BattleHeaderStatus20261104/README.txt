Battle header status layout — 2026-11-04

Uses the existing isolated tmp\shipyard-assembly-20261017 project and warm
Library. Every changed borrowed file is journaled and restored. The main
UnityClient project is only read; UIPreviewsForMe is excluded.

From D:\Game:
  .\tools\BattleHeaderStatus20261104\Validate.ps1 -Stage Prepare
  .\tools\BattleHeaderStatus20261104\Validate.ps1 -Stage Test -Run header-layout -TestPlatform EditMode -TestFilter 'Battle(Callouts_UseRegionalHeaderAndRemoveStageBanners|Header_ReservesItsActualHeightAndSeparatesReadoutsFromStatusAndTeaching)|VoyageSettings_OnlyAppearsOnSupportedScreensAndLeavesHeaderSpace'

After that Unity process exits:
  .\tools\BattleHeaderStatus20261104\Validate.ps1 -Stage Capture -Run header-capture

Capture.cs renders production UI in disposable PreviewScenes with the actual
1920x1080 CanvasScaler. It records ordinary naval status and an in-memory
boarding sample with long values, measuring font size, preferred text width,
line count, and separation from title, boarding readout, and card-deck button.
Screenshots are unmodified Unity output. No player save or authored scene is
created or changed.

After Unity exits, inspect XML/report.json and screenshots, then run:
  .\tools\BattleHeaderStatus20261104\Validate.ps1 -Stage Cleanup

Cleanup restores borrowed sources, removes verified redundant backups and
task-created Temp, and checks main-project hashes before/after cleanup. Keep
the shared Library for its existing validation-tool dependents. Final source
snapshots, hashes, logs, test results and screenshots remain under
outputs\battle-header-status-20261104\validation.
