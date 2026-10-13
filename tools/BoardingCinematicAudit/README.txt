Temporary boarding cinematic capture and scoped preview synchronization

2026-10-13: UIPreviewsForMe is retired. SyncBoardingCinematicFromCommandLine is disabled before any writes. The scoped-sync details below describe historical evidence only; do not regenerate or copy those scene files back. Production UI capture remains available.

Do not run in the main UnityClient project. Root coordinates Unity execution.
Import BoardingCinematicAuditCapture.cs and BoardingCinematicPreviewSync.cs into
the isolated project's Assets/_Project/Editor directory. The updated permanent
Editor/BoardingCrewPreviewSceneSync.cs must be synced too.

Capture entry:
  -executeMethod ShatteredSail.Editor.BoardingCinematicAuditCapture.CaptureFromCommandLine
  -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIRECTORY
Use a real graphics device (omit -nographics). Output must be new/empty.

37 frames: six terrain groups x day/dusk/night at 1920x1080 (18), open day and
storm night at 1280x720 (2), open day at 2560x1080 (1), normal naval defense HUD
(1), and three static production wipe frames at 20/65/100% (3). The fully covered
wipe frame is intentionally near-uniform; the normal blank-frame check remains
for every other frame. Wipe snapshots demonstrate geometry only, not timing.
Twelve further 1920x1080 open-day boarding views cover the ordinary raider,
imperial patrol, ghost ship, three route bosses, treasure boss and five named
legendary warships. They call the actual authored encounter factories and
StartBattle in the disposable session, then enter the real boarding stage.
Independent expected encounter/resource pairs hard-fail if any art is missing,
wrong or falling back. Report records actual encounter ID/name/resource and
verifiedEnemyIdentity alongside full actor bounds, UV and aspect checks.

Every frame uses ConfigureEditorPreview, the real Core EnterBattleStage and the
real controller Render; only the disposable session's terrain, clock, health,
guard and hand are explicit fixture data. Max HP comes from the real battle.
No PlayerPrefs writes, recruitment spoofing, manual UI recreation or scene saves
occur in Capture. Special-crew art is instead verified/captured by PlayMode tests
with a real recruitment scope. Ordinary-party capture requires a clean isolated
editor identity because the existing roster fallback reads saved recruitment.

The rendering implementation is derived from UiAuditReadOnlyCapture and retains
its actual SRP/camera/RenderTexture/CanvasScaler checks and all-text geometry.
Additional hard checks verify selected background, texture/UV aspect, full-body
actor bounds and non-raycast state, ordinary-group identities, no old tiny deck,
and the real segmented-health component's maximum. report.json includes raw
geometry and errors; any failed frame makes the command fail. Text geometry flags
are evidence for visual review, not a claim of occlusion-aware accessibility.

Historical scoped sync entry (disabled):
  -executeMethod ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncBoardingCinematicFromCommandLine
  -boardingSyncProject ABSOLUTE_ISOLATED_PROJECT_DIRECTORY
  -boardingSyncReport NEW_ABSOLUTE_JSON_PATH
  -boardingSyncBackup NEW_EMPTY_ABSOLUTE_BACKUP_DIRECTORY
The tool refuses D:/Game/UnityClient and any project other than the explicit
argument. It calls the repaired permanent SyncBoardingCrewScene for only:
  Assets/_Project/Scenes/UIPreviewsForMe/UIBattle/UIBattle_BoardingHook.unity
  Assets/_Project/Scenes/UIPreviewsForMe/UIBattle/UIBattle_BoardingLadder.unity
  Assets/_Project/Scenes/UIPreviewsForMe/UIBattle/UIBattle_BoardingRam.unity

Scene/root/controller identities, preview fields, Battle Card root geometry,
and unrelated graphics are checked after sync; transient Unity instance IDs are
normalized while GlobalObjectIds and asset paths are checked separately. Scene
meta hashes must not change. Original scene+meta backups and before/after SHA256
are recorded. changedEditableAssets lists all newly created/modified/deleted
EditablePreviewAssets files including meta; referencedEditableAssetsAndMeta lists
the complete relevant dependency set. Those historical scene/dependency copies must no longer be applied to UIPreviewsForMe.

Regenerate the capture source with create-capture.ps1 after changing the fixture
include. .cs.inc is generator input, not a Unity source file; only copy the two .cs
files. Static logs: Artifacts/BoardingCinematic20261009/static-editor/.
