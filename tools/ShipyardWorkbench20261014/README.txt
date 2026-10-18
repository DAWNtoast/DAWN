Shipyard equipment workbench review — 2026-10-14

Sync.ps1 copies current workspace Assets, Packages and ProjectSettings into the existing isolated project D:/Game/tmp/shipyard-detail-clean-20261011. It refuses an occupied project, excludes the retired UIPreviewsForMe directory, removes only stale .cs/.meta files inside the isolated Assets/_Project tree, installs this capture helper, and assigns a separate company/product identity. It does not generate or copy back preview scenes.

Run.ps1 launches a hidden graphics-enabled Unity 6000.3.23f1 process and returns its PID. Wait for that process to exit and inspect its log/results before another mode or synchronization. Never overlap this project's older Fleet or ShipDetail validation runners.

Example commands (PowerShell):
  & D:/Game/tools/ShipyardWorkbench20261014/Sync.ps1
  & D:/Game/tools/ShipyardWorkbench20261014/Run.ps1 -Stage Capture -Output D:/Game/Artifacts/ShipyardWorkbench20261014/Review01
  & D:/Game/tools/ShipyardWorkbench20261014/Run.ps1 -Stage Edit -Output D:/Game/Artifacts/ShipyardWorkbench20261014/Tests01
  & D:/Game/tools/ShipyardWorkbench20261014/Run.ps1 -Stage Play -Output D:/Game/Artifacts/ShipyardWorkbench20261014/Tests01

Capture matrix: ship 4 (打捞船·拾潮), then ship 0; Empty, Inventory and Selected equipment states at 1920x1080 and 1280x1024. Fixture items and selected IDs exist only in memory. The renderer uses production UI, a temporary PreviewScene and the enabled production CanvasScaler. It refuses mismatched display/camera/canvas geometry. It also compares the ship window and main artwork local rectangles against measured pre-redesign coordinates. Reports include screen rectangles and the local geometry of every graphic.

Editor preview intentionally disables equip/unload; ship 4 keeps the production unowned-vessel preview. Its selected-comparison frame is arranged directly in memory to inspect layout, not to pretend a valid purchase or installation occurred. PlayMode tests separately exercise real pointer hits, install, replace, unload, persistence and unauthorized vessel protections.

Edit mode filter: ShipyardGalleryTests;ShipyardComponentComparisonTests.
Play mode filter: ShipyardEquipment_;ItemIcons_EquipmentAndSettlementKeepPicturesClearOfNamesAndAmounts.

Success criteria: capture log contains SHIPYARD_WORKBENCH_CAPTURE_OK; all 12 frames have no error, scalerVerified=true and shipGeometryVerified=true; test XML root result is Passed. Visual inspection of PNGs remains required.
