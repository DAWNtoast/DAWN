# Ship art redraw validation

2026-10-13: The user retired `UIPreviewsForMe`. Saved-scene sync, saved-scene capture and `finish-preview-copyback.ps1` now stop with an explicit retirement error. The saved-preview paragraphs below record completed historical work only. Ordinary production UI capture and tests remain available.

`ShipArtRedrawCapture.cs` is installed only in a disposable Unity project's Editor folder. It renders actual production UI in fresh Editor PreviewScenes and records target/scaler geometry, ship texture resource paths, UVs, runtime import dimensions, sampling and shader. It does not save scenes/assets, enter PlayMode, invoke buying/equipping callbacks or build/package the game.

The capture set consists of 29 full 1920 × 1080 frames: twelve shipyard galleries, twelve performance dossiers, three actual route boss naval battles, and the retained legacy tutorial route/battle builders. Each player ship uses its home-port backdrop. Legacy tutorial battle follows actual first-leg and advanced-leg progression in a new in-memory TutorialVoyageSession, then invokes the production tutorial builder.

The cached scratch project remains at `D:\Game\tmp\shipyard-detail-clean-20261011`, so earlier audit references keep working. Current evidence is under `D:\Game\Artifacts\ShipArtRedraw20261012\validation`: `editmode.xml` has 69 passing tests, `final/playmode.xml` has 7 passing tests, and `final/ui-review/index.html` links all 29 final screenshots. The final report records captured shader and source texture SHA256 hashes. The three boss fixtures create fresh unlocked sessions and assert three distinct selected-route enemy UVs.

`Prepare` mirrors the current source only into scratch, installs the capture helper, and assigns isolated company/product identifiers so PlayMode fixtures do not access the game's player save identity. Prepare must run only when scratch Unity is stopped. Use a fresh `-OutputRoot` or capture directory name for a new review; preserve existing evidence.

Commands from PowerShell after the production atlas/code changes are ready:

```powershell
& D:\Game\tools\ShipArtRedraw\run-validation.ps1 Prepare
& D:\Game\tools\ShipArtRedraw\run-validation.ps1 Capture
& D:\Game\tools\ShipArtRedraw\run-validation.ps1 EditMode
& D:\Game\tools\ShipArtRedraw\run-validation.ps1 PlayMode
```

Unity requires graphics for the capture/GPU checks; do not pass `-nographics`. Local license service access may require the same approved unsandboxed execution used for earlier Unity validation.

EditMode filters cover all twelve water-contact/layout/dossier cases and cutout material/grid/alpha invariants plus ship aspect geometry. PlayMode filters exercise both imported physical atlases, all twelve mirrored/turned edges, navigation vessel markers, all twelve hulls after refits/departure, and the three enemy boss art regions and sea contacts.

Both imported atlases use BC7 with true alpha and Bilinear sampling. The validated cutout path uses native texture sampling for authored alpha; the existing keyed, premultiplied filtering path remains for matte sources. GPU tests verify all twelve cells, mirrored/turned cutouts, foreground parity, retained black sails and synthetic filtered edge behavior without relaxing thresholds.

`ShipArtRedrawPreviewSync.cs` runs only in the cached scratch project. It reuses the existing private `SyncRegionalShipyard` for 25 configured shipyard/dossier scenes and repairs five saved naval atlas images in place. Three boarding previews have no references to either changed atlas and remain byte-identical. `saved-preview-sync/report.json` records settings, UV/aspect/water contacts and referenced editable assets. `finish-preview-copyback.ps1` restores unrelated Unity serialization from the production originals before reporting exact changed paths and verifying copies. The 35 referenced editable assets and all metas are byte-identical, so copyback writes only 30 `.unity` files. Original scenes and their metas are backed up under `D:\Game\Artifacts\ShipArtRedraw20261012\preview-scene-backup\Assets`. The existing `UI_Battle` zero canvas scale is repaired to one for a visible saved preview.

Six saved samples were reopened without UI rebuilding or saving; evidence is `validation/saved-preview-reopened-final/index.html`. `validation/saved-preview-sync/copyback-report.json` and `changed-paths.txt` provide source/scratch scene hashes and the final copy status. No game package is produced.
