# Tavern redraw validation

This task uses only `D:\Game\tmp\tavern-redraw-20261012`; it never changes production files or another scratch project. `Prepare` copies Assets, Packages and ProjectSettings without a Library cache, isolates company/product identifiers, and installs the helper only in the new scratch project. It refuses an existing destination. Unity is not launched by Prepare.

After the seven new production PNGs and their preserved metas are installed:

```powershell
& D:\Game\tools\TavernRedraw\run-validation.ps1 SyncArt
& D:\Game\tools\TavernRedraw\run-validation.ps1 Capture -RunName final
& D:\Game\tools\TavernRedraw\run-validation.ps1 EditMode -RunName final
```

`Compile -RunName initial-import` can warm the independent project's imports and compile the helper before the replacement art is ready. It performs no capture or tests; SyncArt remains necessary after installation.

Use a fresh RunName on repeat runs. Capture uses a private non-Play PreviewScene and actual production UI builders. It writes six 1920×1080 screenshots, an HTML gallery with optional measured hit-area overlays, and JSON records of seven source/imported textures and all 36 character buttons/names/status labels. It checks BC7, bilinear sampling, clamp addressing, non-readable textures, no mipmaps, and retained source dimensions (allowing the existing four-pixel compression alignment). Neither capture nor testing builds the game. Do not use `-nographics`; graphics are required. The first run imports the independent project, which may take several minutes.

EditMode uses the existing regional art, tavern roster, scene hotspot, lazy loading, and background compression tests. The production project remains read-only. Scripts neither save production scenes nor synchronize generated scenes back.

## Saved roster previews retired on 2026-10-13

The user retired `UIPreviewsForMe`. `sync-saved-previews.cjs` and `CaptureSaved` now stop before any work. `SyncArt` copies only the seven PNGs, two runtime source files, the regional interaction test, their metas, and the capture helper; it no longer copies roster scenes. Existing saved-scene reports and screenshots below are historical evidence. Do not apply or regenerate those scenes. Ordinary production UI `Capture` and EditMode checks remain available.

`SyncCode -RunName <fresh-name>` is an explicitly authorized scratch consistency repair for concurrent project work: it copies the current `Assets/_Project` `.cs` / `.asmdef` files and available metas, removes code absent from production only inside this task's verified scratch path, preserves `TavernRedrawCapture.cs`, and records a hash manifest. It does not write production. The final run required this after parallel dispatch removal changed partial-controller dependencies.

## Completed evidence

- `Artifacts/TavernRedraw20261012/validation/final-runtime-current-source/ui-review/index.html`: six real production UI screenshots, 36 recorded character rectangles, seven verified imported textures, zero capture errors. All seven source PNG hashes match production; sources are 1672×941 and BC7 imports are 1672×940 with the established four-pixel alignment.
- `Artifacts/TavernRedraw20261012/validation/final-saved-v2/saved-preview-review/index.html`: three saved roster screenshots and one unchanged detail page; 4/4 captured, no Canvas rebuilt, all scene hashes retained.
- `Artifacts/TavernRedraw20261012/validation/final-editmode-transform-tolerance/editmode.xml`: 144 tests, 143 passed, one unrelated whole-project resource-completeness failure. All 36 new regional interaction cases pass, each checking body and name raycasts into the correct dossier. The remaining test expects the parallel Warehouse/Voyage task's newly added resources, outside this art synchronization scope; those new images were not mirrored into this scratch, and the production warehouse set was still incomplete at inspection.
- The new interaction test uses the same 0.05 UI-unit tolerance for overlaps as containment. This corrects shared authored edges that world/canvas float transforms separate by about 0.00006 UI units; no production layout or runtime code was changed for the test fix.
- Saved scene apply evidence is under `validation/saved-preview-sync`; 72 vector fields changed across three scenes, with all other YAML blocks unchanged. A repeat dry-run reported zero edits.
