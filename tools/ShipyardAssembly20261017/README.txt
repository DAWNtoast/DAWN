Shipyard fullscreen assembly validation, 2026-10-17

The helpers only run against D:/Game/tmp/shipyard-assembly-20261017.
Sync.ps1 copies current production Assets, Packages and ProjectSettings into
that independent project. The initial sync copies a warm Library cache from
tmp/shipyard-detail-clean-20261011; it never shares writable files with it.
The retired Assets/_Project/Scenes/UIPreviewsForMe directory and its meta are
explicitly excluded. No old preview generator or synchronizer is invoked.
Company/product names are isolated to protect normal game saves. The sync
manifest records source/snapshot SHA256 for the shipyard controller, equipment
view, workbench, Escape navigation, main controller and Editor preview controller.

Run sequentially from D:/Game, waiting for every Unity process to exit:

  & tools/ShipyardAssembly20261017/Sync.ps1
  & tools/ShipyardAssembly20261017/Run.ps1 -Stage Capture -Output D:/Game/Artifacts/ShipyardAssembly20261017/Capture01
  & tools/ShipyardAssembly20261017/Run.ps1 -Stage Edit -Output D:/Game/Artifacts/ShipyardAssembly20261017/Edit01
  & tools/ShipyardAssembly20261017/Run.ps1 -Stage Play -Output D:/Game/Artifacts/ShipyardAssembly20261017/Play01

Use a new output directory for each repeated run. Capture launches Unity with
graphics enabled and also compiles the current snapshot. The wrapper returns
the PID; completion must be checked in the log and result files.

Capture builds the production UI inside disposable Editor PreviewScenes.
It does not save scenes, assets, PlayerPrefs or runtime profiles. Ships 0 and 4
are captured at 1920x1080, 1280x1024 and 2560x1080 for Empty, Inventory and
Selected fixture states (18 frames). The second ship retains the production
unowned preview. All fixture inventory is memory-only. The Editor selected
state is arranged directly; interaction behavior must be tested in PlayMode.

report.json records actual scaler geometry, full-canvas opaque solid-color
backing, market-paper asset/coverage, original port-header geometry, centered
detail container, ship-art containment, screen bounds and texts. All visible
page graphics and controls must fit inside the paper below the original header;
the page's blank reference rectangle may extend beyond its visible content.
Paper/header visibility is additionally verified using changed rendered pixels
when each is temporarily hidden. All changes are restored inside disposable
PreviewScenes. index.html is a gallery for visual inspection. No original
ship-position or page-scale assertions remain; layout is validated by fit.
