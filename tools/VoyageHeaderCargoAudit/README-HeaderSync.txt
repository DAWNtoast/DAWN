Voyage Header scoped preview synchronization

Import Tools/VoyageHeaderCargoAudit/VoyageHeaderPreviewSceneSync.cs into the isolated validation project's Assets/_Project/Editor only. Do not add it to the main production Editor tree.

Unity entry:
  -executeMethod ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncVoyageHeaderFromCommandLine
  -voyageHeaderBackupPath D:/Game/Artifacts/VoyageHeaderCargo20261008/Attempt01/scene-before
  -voyageHeaderReportPath D:/Game/Artifacts/VoyageHeaderCargo20261008/Attempt01/scene-sync.json

Requires stopped Editor. Refuses an existing report file and unsaved target scene edits. Existing backup files are never overwritten. Report records each migrated scene, retained Graphic count, clock date/hours/source, and added action buttons.

Selection: GameplayPreviewSceneBuilder.NavigationEnduranceReferencePaths, the 21 explicit navigation, encounter and tutorial references under Assets/_Project/Scenes/UIPreviewsForMe. No whole-scene regeneration. A disposable matching preview supplies only the new clock; original clock text/hours are retained together when available. Existing map/event/journal/cargo text and art Graphic objects are verified unchanged. New Cargo button reuses the original Settings art without inheriting its persistent action. The production runtime binds OpenVoyageCargo when its UI is built.

Header contract: 148 high; title 260x60 at left+180; clock 620x68 at center-266; status 530x60 at right-577; Cargo 128x58 at right-234; Settings 128x58 at right-94. All at y0, leaving tutorial objective and instruction bands clear. Time clock retains Lookout Time Ribbon/Track/Readout/Legend names under Voyage Header. Lookout Time Overlay remains in the lookout. Lookout Heading shows direction only.

Updated production Editor monthly clock synchronization now targets Voyage Header and permits an encounter without a lookout overlay. Full source search found no other stale Lookout Time Ribbon copy entry.

Static validation:
  & D:/Game/Tools/VoyageHeaderCargoAudit/Compile.ps1
Outputs: Artifacts/VoyageHeaderCargo20261008/static (Core, Presentation, full Edit, full Editor plus this tool). All four stages passed after final changes. Unity execution and screenshots are coordinated by root; static validation is not a substitute for runtime acceptance.

Related capture and PlayMode work are owned separately in README-Cargo.txt and CompileCargoFlow.ps1. Do not overwrite their scripts or static-cargo-flow output.