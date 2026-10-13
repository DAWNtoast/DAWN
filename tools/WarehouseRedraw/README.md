# Warehouse redraw validation

`run-validation.ps1` uses only the dedicated project at `D:/Game/tmp/warehouse-redraw-20261012`. `Prepare` refuses an existing directory; it copies Assets, Packages and ProjectSettings and assigns the independent company/product `WarehouseRedrawValidation` / `WarehouseRedrawValidation20261012`. Unity 6000.3.23f1 always starts hidden. The production Unity project is never launched by these tools.

Actions: `Prepare`, `Sync`, `Compile`, `Capture`, `EditMode`, `PlayMode`. `SyncSaved` and `CaptureSaved` are retired and stop before any work. Supply a fresh `-RunName` for every run so logs and evidence are retained. If Windows sandboxing prevents Unity's licensing IPC connection, run the same hidden scratch action with the approved unrestricted process environment; do not redirect it to the production project.

`WarehouseRedrawCapture.cs` renders the production editor fixture into a private PreviewScene and outputs 31 frames: six ports × three tabs, seven broken-anchor dialog/catalog views, and the three tabs at 1280×720 and 1280×1024. CanvasScaler Expand geometry is reproduced by fitting the 1920×1080 logical canvas. The report records all status bars and relevant UI bounds, verifies six exhibit hotspots and nine/six inventory rows, and requires unmodified full-frame room UVs. Values are isolated preview fixtures, not earned gameplay.

## Saved previews retired on 2026-10-13

The user retired `UIPreviewsForMe`. `WarehouseRedrawPreviewSync.cs` and `copy-saved-previews.ps1` now reject execution before scene or dependency writes. `Sync` excludes this directory. Historical migration reports, backups and captures listed below remain evidence; do not refresh or copy the former 13 warehouse scenes back. Ordinary `Capture` still renders private production UI fixtures.

Latest evidence is under `Artifacts/WarehouseRedraw20261012/validation`:

- `final-review/ui-review`: 31 actual Unity screenshots and shared status-bar measurements.
- `aligned-saved-review/saved-preview-review`: 13 saved-scene screenshots.
- `aligned-saved-sync/saved-sync`: property migration report and original scene backups.
- `aligned-final-tests/editmode.xml`: 441 EditMode checks after final exhibit alignment.
- `final-play/playmode.xml`: 29 Warehouse PlayMode flows, including transfers, sales, rare inventory, retained controls, sorting and reload persistence.
- `aligned-click-test/playmode.xml`: final-position pointer/icon reachability and real cargo transfer check, 1/1 passed after the exhibit column adjustment.

No test changes relax inventory, sale, save, callback or object reuse assertions. Updated legacy visual assertions reflect the requested replacement of parchment, larger item icons, and the new empty-selection/help labels.
