Naval VFX remaster validation — 2026-11-05

CompileProduction.ps1 uses existing read-only compiler references and removes its own DLL/PDB outputs.
Validate.ps1 leases tmp/shipyard-assembly-20261017, checks process occupancy and directory links,
journals every changed source/resource, excludes retired previews, and restores the borrowed project.
The main UnityClient project is never opened by these tools. Its Library/Temp are never modified or cleaned.

Prepare only after all final production source and the remastered atlas have landed.
  ./tools/NavalVfxRemaster20261105/CompileProduction.ps1 -Run compile1 -IncludeEditMode -IncludePlayMode -IncludeCapture
  ./tools/NavalVfxRemaster20261105/Validate.ps1 -Stage Prepare
  ./tools/NavalVfxRemaster20261105/Validate.ps1 -Stage Test -Run edit1 -TestPlatform EditMode
  ./tools/NavalVfxRemaster20261105/Validate.ps1 -Stage Test -Run play1 -TestPlatform PlayMode -TestFilter 'NavalVfx_;RuntimeMemory_BattleArtKeepsLiveOwnersThenCollectsAndReopens;AdditionalBattleVfx_'
  ./tools/NavalVfxRemaster20261105/Validate.ps1 -Stage Capture -Run capture1 -FrameCount 48
  ./tools/NavalVfxRemaster20261105/BuildPreview.ps1 -Run capture1
  ./tools/NavalVfxRemaster20261105/Validate.ps1 -Stage Cleanup

Wait for the launched Unity process to exit before starting another stage.
Status reports the active Unity process and log tail. Fresh run names preserve prior evidence.
The capture uses the production naval UI and renderer; it samples neutral cannon hit/guard/miss,
all six schools and support/field/area intentions. These are visual captures, not fresh battle simulation.
PlayMode tests independently verify real card commits, resource consumption, hit timing and cancellation.
BuildPreview validates complete MP4 decoding before deleting its manifest-listed intermediate JPG frames.
Retain representative PNG keyframes, source hashes, logs, screenshots and validated videos.
Cleanup restores the leased project and removes only task-owned additions and newly-created Temp.
The shared warm Library remains required by existing screenshot and validation tools.

No image-generation or external API call is made by these validation tools.
