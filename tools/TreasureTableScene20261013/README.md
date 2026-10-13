# Treasure table scene validation — 2026-10-13

The helper runs only in `D:/Game/tmp/treasure-table-scene-20261013`. It does not launch or edit the production Unity project. Prepare and Sync do not start Unity; the other actions launch a hidden batch Editor with graphics. Run one action at a time, after the previous process exits. Never delete a Unity lock or terminate another task's Unity process to proceed.

From PowerShell in `D:/Game`:

```powershell
$runner = 'D:/Game/tools/TreasureTableScene20261013/run-validation.ps1'
& $runner -Action Prepare
& $runner -Action Compile -RunName import01
# After all source edits settle and the scratch Editor has exited:
& $runner -Action Sync
& $runner -Action SyncPreview -RunName preview01
& $runner -Action CheckSaved -RunName saved01
& $runner -Action EditMode -RunName tests01
& $runner -Action PlayMode -RunName tests01
& $runner -Action Capture -RunName capture01
& $runner -Action Build -RunName build01
```

The graphics capture writes 25 actual production-preview frames and `report.json` under `D:/Game/Artifacts/TreasureTableScene20261013/validation/capture01/screens`. It covers unopened, independent 4/4–2/4–1/4 progress, unused folio and used folio states at 1280×720, 1920×1080, 2560×1440, 3840×2160 and 1280×800, plus the other five ports. It records text fit, physical font-size lower bounds, on-screen positions and live GraphicRaycaster hits at three tool hotspots and five samples per projected map. Screenshots still require visual review of art alignment and legibility. These are read-only Editor previews, not proof of gameplay callbacks; the selected PlayMode tests exercise identification, crafting, archive paging, ordinary-route preservation and treasure departure.

SyncPreview calls `ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncTreasureWorktableFromCommandLine` inside the isolated project. Since the 2026-10-13 retirement of UIPreviewsForMe, it updates only `Assets/_Project/Scenes/UIPreviews/UI_TreasureMap.unity`. Review that scene and its visual dependencies before any production copyback. Do not call broader adventure/flow synchronizers for this visual change. Sync copies source scenes again, so perform it before SyncPreview, not after.

CheckSaved reopens that one ordinary UIPreviews scene without refreshing their controllers and verifies v3 background references, three projected papers and separate plaques, the transparent workbench hotspots, state-specific fragments and folio, fonts and textures, and Canvas/scaler/raycaster. Its JSON report compares SHA256 before and after for every `.unity` file under Assets. It does not save scenes or produce duplicate screenshots. The runner installs the latest helper for each action, so CheckSaved can run directly after SyncPreview without Sync overwriting the newly generated scenes.

The Windows build uses `ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine` and writes a new `windows/ShatteredSail.exe` plus its data directories and build report. It keeps the shipping storage identity, so do not launch the built player against the user's saves for a smoke test. Package only after the build report says `Succeeded`:

```powershell
& 'D:/Game/tools/ReleaseBuild/Package.ps1' `
  -BuildDirectory 'D:/Game/Artifacts/TreasureTableScene20261013/validation/build01/windows' `
  -ZipPath 'D:/Game/Artifacts/TreasureTableScene20261013/treasure-table-windows-20261013.zip'
```

Direct Unity invocation equivalent:

```powershell
& 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -quit `
  -projectPath 'D:/Game/tmp/treasure-table-scene-20261013' `
  -executeMethod ShatteredSail.Editor.TreasureTableSceneCapture.Run `
  -treasureTableCapturePath 'D:/Game/Artifacts/TreasureTableScene20261013/validation/manual01/screens' `
  -logFile 'D:/Game/Artifacts/TreasureTableScene20261013/validation/manual01/capture.log'
```

Do not add `-nographics` for capture. For `-runTests`, do not add `-quit`; Unity Test Framework exits on completion. The runner rejects zero-test XML, failures, re-used evidence log paths, an open scratch lock and attempts to overwrite an existing scratch project.
