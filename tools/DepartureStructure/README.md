# Departure structure verification

`DepartureStructureCapture.cs` is an editor helper for the disposable project at `D:/Game/tmp/departure-structure-20261012`. It deliberately refuses to run in the working Unity project.

Copy the helper to the disposable project's `Assets/_Project/Editor/` directory, together with the current production source/assets. Invoke Unity with graphics enabled (do not pass `-nographics`):

```powershell
& $unityEditor -batchmode -quit `
  -projectPath 'D:/Game/tmp/departure-structure-20261012' `
  -executeMethod ShatteredSail.Editor.DepartureStructureCapture.CaptureFromCommandLine `
  -departureStructureCapturePath 'D:/Game/Artifacts/DepartureStructure20261012/captures' `
  -logFile 'D:/Game/Artifacts/DepartureStructure20261012/capture.log'
```

The capture output must be a new or empty absolute directory. The helper produces 15 PNGs and `report.json`:

- All six ports at 1920 × 1080 in ordinary departure mode.
- Broken Anchor and White Shell at 1280 × 720 and 2560 × 1440.
- Broken Anchor at 1920 × 1080 with a selected recorded chart, invalid chart warning, treasure departure, crew configuration, and basic supplies overlay.

The production canvas uses `ScaleWithScreenSize` with reference resolution 1920 × 1080 and `Expand`. Captures compute that same uniform scale and use a camera/render texture to sample the authored canvas. At the three requested 16:9 sizes, the logical canvas stays 1920 × 1080 and the camera stays at orthographic size 540; only output pixel density changes. This avoids a smaller-resolution capture cropping the UI or a larger-resolution capture spreading the same controls apart.

The report checks required controls and visibility, screen bounds, route/setup column boundaries, sibling button overlap, and whether key text fits at its allowed minimum font size. It records active control states and every nonempty active text label. Invalid charts must clear the selected chart and disable departure. Captures must remain in the port phase and preserve the number of owned treasure maps.

All pages are private, stopped-editor `PreviewScene` fixtures. Recorded/invalid charts and treasure departure use the public editor preview APIs. Crew configuration and the supplies dialog use reflection only to stage the existing view. The helper never invokes a start-voyage, purchase, or save-progress button and never saves scenes or assets.

Editor fixtures intentionally retain production non-Play gates: purchasing and some crew controls may be disabled. This helper validates presentation and state gating; live interaction regression remains the responsibility of the existing PlayMode tests.

## Retired saved-preview synchronization (2026-10-13)

The user retired `UIPreviewsForMe`. `DepartureStructurePreviewSync.SyncFromCommandLine` and `Apply-DepartureStructurePreviews.ps1` now stop before any filesystem changes. The former 14-scene synchronization implementation and existing reports remain historical evidence. Do not refresh or copy those scenes back. The ordinary production UI capture workflow above remains available.
