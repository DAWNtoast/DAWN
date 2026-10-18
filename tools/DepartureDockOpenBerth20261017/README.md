# Departure dock open-berth validation

This runner stages the current Unity source and resources into the cached disposable project under `D:/Game/tmp`, never into the live project. It does not generate or restore `UIPreviewsForMe`.

```powershell
./tools/DepartureDockOpenBerth20261017/Run-Validation.ps1 -Mode Stage
./tools/DepartureDockOpenBerth20261017/Run-Validation.ps1 -Mode EditMode
./tools/DepartureDockOpenBerth20261017/Run-Validation.ps1 -Mode PlayMode
./tools/DepartureDockOpenBerth20261017/Run-Validation.ps1 -Mode Capture
./tools/DepartureDockOpenBerth20261017/Run-Validation.ps1 -Mode Capture -CapturePorts 0 -CaptureShips '0,8,11' -Output D:/Game/Artifacts/DepartureDockOpenBerth20261017/WaterContactFocus
```

Every command synchronizes current sources and resources before running. `stage-manifest.json` records source and staged hashes for the dock runtime, relevant tests, capture utility and all regional voyage textures. A failed or missing report is never treated as success.

The first run found an unrelated existing compilation failure in `CoreBattleCardDeduplicationTests.cs:273` (two CS1503 int-to-string errors). The optional `-IsolateUnrelatedTestCompileFailure` switch moves only this file out of the temporary project's Assets and records its hash and exclusion reason. Parallel work subsequently fixed that source error. Final validation ran without this switch: 98 EditMode tests, 7 PlayMode tests, and all 72 port/vessel captures passed.

During parallel settlement work, `SettlementFireworksGraphic.cs.meta` contained a 33-character GUID that made Unity ignore the script. If that exact malformed GUID is still present, the runner normalizes only the temporary copy to a stable 32-character value and records both values in `temporaryMetadataRepairs`. It never edits the source metadata. Parallel work subsequently corrected the source GUID, so the final run has empty `exclusions` and `temporaryMetadataRepairs` arrays.

Capture produces all 12 vessels in all 6 ports at 1920x1080, plus a 4-by-3 contact sheet per port and a JSON report of vessel waterline error and UI placement. It uses temporary preview scenes only; no scene or asset is saved.

The six new background source images are 1672x941. The 1920x1080 captures use the current production UI builder in a disposable Unity Editor project, not a packaged release build. Both water-contact layers are checked on every capture for their current vessel contact point, regional texture, draw order and non-interactive behavior.

Batch capture initially showed blurred shared-header text because it rendered immediately after world-space canvas conversion. Two explicit text/canvas rebuild passes in the capture utility removed that artifact in a focused 00/08 check; final captures include that refresh. No production typography code was changed.

Final results and six-port overview links are recorded in `Artifacts/DepartureDockOpenBerth20261017/Validation/验证报告.md`. The initial pre-immersion captures remain in `ValidationPass1` for comparison. The 98 EditMode and 7 PlayMode tests include the water-contact node/state checks; only feather width, opacity and capture text refresh were adjusted after that successful test run.

The runner refuses to start if a real Unity Editor process is already running. Unity Hub's `unity.exe serve` helper is not mistaken for an editor. Launches are hidden, and the source project's player preferences are separated by an isolated project product name.
