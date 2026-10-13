# Departure dock scene capture

`DepartureDockSceneCapture.cs` is an editor-only verification helper. Copy it into the disposable project's `Assets/_Project/Editor/` folder. It refuses to run outside `D:/Game/tmp/departure-dock-scene-20261012`.

Run the graphics-enabled Unity editor with:

```text
-batchmode -quit -projectPath D:/Game/tmp/departure-dock-scene-20261012 -executeMethod ShatteredSail.Editor.DepartureDockSceneCapture.CaptureFromCommandLine -departureDockSceneCapturePath <absolute-new-output-directory> -logFile <absolute-log-file>
```

Do not pass `-nographics`. The helper uses actual production preview construction, a private PreviewScene and a RenderTexture camera. It reproduces the production CanvasScaler `ScaleWithScreenSize/Expand` ratio at 16:9 rather than stretching the UI to fit a render target.

The output is thirteen PNGs plus `report.json`:

- Broken Anchor normal departure with vessels 0, 5, 7, 8 and 11 at 1920×1080.
- White Shell normal departure as the regional fallback.
- Broken Anchor normal departure at 1280×720 and 2560×1440.
- Broken Anchor valid chart, invalid chart, treasure, crew configuration and basic supply basket at 1920×1080.

The helper checks the existing function controls, valid/invalid chart selection, departure blocking, non-consumption of treasure maps, screen bounds, button overlap, fitting labels, expected regional art, independent hull texture, authored waterline contact and unchanged fixed control positions across vessel fixtures. Only ordinary preparation and the basic supplies overlay use the independent berth; chart, treasure and crew editing retain the shared paper layout. Old column containment is skipped only for berth states.

The scene contract is centralized in `InspectIndependentShip`: `Inspect Departure Vessel` contains `Starter Modular Ship`; the waterline marker `Departure Dock Water Contact` uses the shared `DepartureDockSceneLayout` ship center and waterline constants; the hull contact has local y=0 relative to that marker. The hit area uses the same layout constants, and the background resolves through the regional catalog to `PortFacilities/Regional/Voyage/voyage_broken_anchor_scene_v3`. Static button artwork is painted into this background; the existing controls remain as transparent hotspots with independent feedback and live values.

`InspectPointerReachability` sends no clicks. It queries the production `GraphicRaycaster` at every relevant control center after drawing, checks which selectable receives the first hit, and records ancestor graphics/canvas groups for diagnosing blockers. Under modals it checks the modal controls, whose shade is expected to cover the underlying scene. It also rejects a full-screen setup Image that still has `raycastTarget=true`.

Every page includes foreground diagnostics for `Departure Dock Foreground`: presence, active/enabled state, Graphic/CanvasRenderer/inherited alpha, culling, depth, sibling order relative to the ship hit area, sampled texture, screen and mesh bounds, mesh vertex/index counts, vertex alpha range and ancestor graphics. These are recorded after the camera render, so a zero mesh or culling flag can explain missing pier occlusion without modifying its geometry.

Only the helper's in-memory fixture is selected with `Session.SelectShip()`. No saved ownership flags are written. `ConfigureEditorShipyard()` must not be used for this because it changes facilities and rebuilds the session. No start or purchase callbacks run, and no scene or asset is saved.

This is visual/layout verification. Live interaction still requires PlayMode coverage. Existing relevant tests are methods on `ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests`, including the departure ship flow, market/deck return, chart selection, crew and supply flow suites. The ship flow covers owned-vessel cycling, single-vessel disabled arrows, runtime cargo eligibility, equipment return and final departure.

## Retired saved-scene workflow (2026-10-13)

The user retired `Assets/_Project/Scenes/UIPreviewsForMe`. `DepartureDockSceneSave.SaveFromCommandLine` now exits with an explicit retirement error before creating any scene or visual asset. Its previous implementation is retained as disabled source for historical evidence. Existing manifests and captures are historical records; do not regenerate or copy them back. The production UI capture workflow above remains available.
