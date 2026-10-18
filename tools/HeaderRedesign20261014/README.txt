Header redesign verification — 2026-10-14

Run.ps1 launches one graphics-enabled Unity process against the main project. It refuses an existing Unity process for the same project or a reused log; unrelated isolated projects are left alone. Capture outputs stay below D:/Game/Artifacts/HeaderRedesign20261014. The helper is copied into Assets/_Project/Editor for compilation only; Cleanup.ps1 removes only that exact hash-matching helper and its .meta after all Unity runs finish.

No persistent scene is loaded for screenshot creation. Every frame uses EditorSceneManager.NewPreviewScene with the production CoreGameplayDemoController.ConfigureEditorPreview. No scene/asset saves, gameplay UI actions, PlayerPrefs writes, or retired preview sync calls are performed. UI fixture state changes for boarding and tutorial height are limited to these temporary controllers. No external image service is called.

Capture matrix: six port hubs at 1920x1080 / 1280x720 / 3440x1440; one representative facility in each port at 1920x1080; BrokenAnchor departure and component shop at three sizes; GrayGull navigation, encounter, battle and boarding at three sizes; navigation and battle tutorial-height bands at 1280x720. Total: 44 PNGs, report.json and index.html.

For default GrayGull navigation, encounter and battle, CaptureMenu is invoked directly through reflection from UiAuditReadOnlyCapture. Its renderer is reused in HeaderRedesignCapture20261014.cs for custom-port/boarding samples. The production CanvasScaler stays enabled; actual renderingDisplaySize, canvas/camera pixelRect, logical size and scale factor must match requested dimensions. The tool never fabricates multi-resolution results by manually resizing the canvas.

Header text geometry warnings include missing glyph meshes and bounds beyond the render target. They do not replace visual inspection; body text outside scroll masks may exist in the raw frame text record. Existing relevant EditMode tests separately check controls, overlap, tutorial bands and callback behavior.

Example:
  & D:/Game/tools/HeaderRedesign20261014/Run.ps1 -Mode Capture -OutputRoot D:/Game/Artifacts/HeaderRedesign20261014/Attempt01
  & D:/Game/tools/HeaderRedesign20261014/Run.ps1 -Mode EditMode -OutputRoot D:/Game/Artifacts/HeaderRedesign20261014/Tests01 -TestFilter 'PortFacilityHeaderTests;NavigationDashboardTests;BattleHudLayoutTests;GameTypographyTests;CompactHeaderLayoutTests;VoyageSettingsModalTests;ComponentShopShowroomLayoutTests'
  & D:/Game/tools/HeaderRedesign20261014/Run.ps1 -Mode PlayMode -OutputRoot D:/Game/Artifacts/HeaderRedesign20261014/Tests01
  & D:/Game/tools/HeaderRedesign20261014/Cleanup.ps1

Wait for each launched PID to exit and inspect logs/results before starting the next mode. The PlayMode default is the existing PlayerPrefs-preserving VoyageHeader_ReusesTimelineAndControlsAndReleasesInactiveArt fixture. Do not launch old ForMe capture/synchronization entries.