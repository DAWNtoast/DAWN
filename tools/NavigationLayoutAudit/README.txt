Navigation layout verification — 2026-10-08

Source test change:
UnityClient/Assets/_Project/Tests/EditMode/NavigationDashboardTests.cs
Original backed up under Artifacts/NavigationLayout20261008/before/UnityClient/Assets/_Project/Tests/EditMode/.
The existing dashboard geometry test now resizes the same production preview hierarchy through
1920x1080, 1280x720, 1280x1024, 3440x1440, 3840x1080 and back to 1920x1080.
The real enabled CanvasScaler, ScreenSpaceCamera and RenderTexture drive every dimension change.
The test never invokes gameplay Render or layout Apply after setup. It checks header-to-canvas
edges, all four dashboard seams, non-overlap, summed area, retained panel/map/slider/session
identities, the map's full world-space 16:9 aspect and screen-to-map pointer conversion through
the real MapCourseInputSurface event-camera path. The callback is temporarily observed and
restored, so coordinate checks do not alter the voyage or rebuild the UI. Original interaction,
event, tutorial, journal, text and artwork assertions remain.

Capture tool:
Copy only NavigationLayoutCapture.cs into the isolated validation project's Assets/_Project/Editor.
It compiles in the existing ShatteredSail.Editor assembly and reuses UiAuditReadOnlyCapture.
Do not install this temporary tool in the main game's Assets or regenerate authored scenes.

Unity CLI entry (graphics enabled; do not pass -nographics):
-batchmode -quit -projectPath <isolated project>
-executeMethod ShatteredSail.Editor.NavigationLayoutCapture.CaptureFromCommandLine
-uiAuditPath D:/Game/Artifacts/NavigationLayout20261008/<new capture directory>
-logFile D:/Game/Artifacts/NavigationLayout20261008/<new log filename>

The output directory must be new or empty. It produces exactly ten PNGs: Navigation and
Encounter at each of the five dimensions, plus report.json. It only renders disposable fresh
editor previews, preserving the production CanvasScaler. Every frame requires actual display,
camera/canvas pixel rect, scale and logical dimensions to agree with the render target.
Each report frame includes verifiedScaler and active TextGeometry. Journal content can extend
outside its masked scroll viewport; that geometry must not automatically be labeled visible
clipping. This is visual/layout verification, not a Player performance or input-latency benchmark.

Suggested EditMode filters:
NavigationDashboardTests;PortFacilityHeaderTests;EncounterDossierLayoutTests;NavigationEndurancePreviewSceneTests
Suggested real-input PlayMode filters:
NavigationInputProtectionFlowTests methods;NavigationSupplyEnduranceFlowTests methods;
NavigationPresentationLifecycleTests methods;TreasureCore_ContinueExploringLetsNearbyWaterEscapeAndMarkerRevisit
(Several test files are partial CoreGameplayDemoSceneTests, so use the actual method-name
filters or that containing fixture, not filenames, with Unity -testFilter.)

Static compile only (does not start Unity):
& 'D:/Game/Tools/NavigationLayoutAudit/Compile.ps1'
Reads Unity's existing response files, compiles current main sources and the temporary capture
entry with bundled Roslyn, and writes outputs exclusively to Artifacts/NavigationLayout20261008/static.
Actual Unity execution and visual acceptance are owned by the root agent.
