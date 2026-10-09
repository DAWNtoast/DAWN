Voyage cargo regression and visual capture — 2026-10-08

New PlayMode source:
UnityClient/Assets/_Project/Tests/PlayMode/VoyageCargoFlowTests.cs
The containing fixture is the existing partial CoreGameplayDemoSceneTests, so use
-testFilter VoyageCargo_ to select its four actual UnityTest methods:
VoyageCargo_RealBasicAndSpecialDiscardsValidateQuantityAndLeavePortProfileUntouched
VoyageCargo_RetiredRowsConfirmationsAndInventoryQuotesCannotDiscardAgain
VoyageCargo_NavigationMotionDisableAndRenderRetireAllCargoCallbacks
VoyageCargo_SaveReturnAndResumePreservesDiscardsWithoutBankingCargo

These tests use the existing PlayerPrefs-preserving fixture. Each test additionally
redirects both voyage checkpoints and the profile write journal to fresh temporary
directories, clears/restores associated static state, and retires scene controllers
before restoring the caller's paths. Only these verified private directories are
removed. Run in the isolated validation project, never against a user's live game.
Core cargo amounts are arranged as an explicit fixture. Discards then use actual
UI raycasts and callbacks, not direct Core discard calls. Saved voyages return to
MainMenu and continue through real UI. Rejected old callbacks are invoked directly
to simulate queued events. Navigation locking uses real sail motion, with an
explicit empty-water test map; the save/resume case retains the generated map.

Coverage includes all 9 ordinary/6 special rows including zero inventory; gold is
read-only; quantity zero, excessive, negative and malformed input; select-all only
fills quantity; actual wood/gem quantity and capacity changes; port cargo/warehouse/
treasure/gold prefs stay unchanged; modal input blocking and restoration; no travel
change on open; consumed views, switched tabs, closed/reopened dialogs, changed
inventory, navigation motion, disable and Render invalidate stale actions.

Read-only screenshots:
Copy VoyageHeaderCargoCapture.cs AND its .meta to the isolated validation project's
Assets/_Project/Editor. The existing ShatteredSail.Editor assembly supplies
UiAuditReadOnlyCapture.MenuFrame/MenuReport/TextGeometry; no production source edits
are required. Do not install the wrapper in the main game's Assets.

Unity CLI entry (root agent owns execution; graphics enabled):
-batchmode -quit -projectPath <isolated project>
-executeMethod ShatteredSail.Editor.VoyageHeaderCargoCapture.CaptureFromCommandLine
-uiAuditPath D:/Game/Artifacts/VoyageHeaderCargo20261008/<new capture directory>
-logFile D:/Game/Artifacts/VoyageHeaderCargo20261008/<new log filename>
Do not use -nographics. The output directory must be new/empty beneath Artifacts.

The capture produces 15 PNGs plus report.json: Navigation, Encounter, CargoBasic,
CargoSpecial and CargoSelected at 1920x1080, 1280x720 and 3440x1440. Navigation and
Encounter use fresh production editor preview scenes. Cargo uses the same navigation
preview with clearly isolated in-memory quantities, then only opens the production
modal and chooses a tab/item. The discard confirmation is asserted disabled in the
selected state and is never invoked. Snapshot checks ensure preview navigation and
cargo values do not change while opening/browsing. No player prefs or checkpoint
operations are performed by this tool.

The rendering and scaler-verification body is adapted from
UnityClient/Assets/_Project/Editor/UiAuditReadOnlyCapture.cs. It keeps the production
CanvasScaler enabled and verifies actual display size, camera/canvas pixel rect,
scale and logical canvas dimensions for each target. It does not fake multi-size
captures by manually resizing a canvas. Text geometry includes labels under the
modal and outside masked scroll viewports, so those bounds alone are not proof of
visible clipping. Screenshots require human visual review, not just exit zero.

Static compile only, without starting Unity:
& 'D:/Game/Tools/VoyageHeaderCargoAudit/CompileCargoFlow.ps1'
Stages: Core, Presentation, Play, Editor. Bundled Roslyn compiles current main-project
sources, plus the temporary capture wrapper, using existing isolated project refs.
Artifacts go only to Artifacts/VoyageHeaderCargo20261008/static-cargo-flow. The other
Compile.ps1/static path in this folder belongs to the header preview scene audit.
No production source or existing tests were changed for this subtask; new .meta GUIDs
are 32 hexadecimal characters. There are no before-images for newly created files.
