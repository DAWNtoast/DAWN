Naval arrival heading arrows — 2026-11-04

Run from D:\Game. Validate.ps1 borrows the existing idle isolated project
tmp\shipyard-assembly-20261017 and its shared warm Library. It journals and
restores every changed borrowed source file, preserves isolated PlayerPrefs,
and never writes to the main UnityClient project.

Prepare:
  .\tools\NavalHeadingArrows20261104\Validate.ps1 -Stage Prepare

Run the two existing PlayMode scenarios with actual Unity screenshots:
  .\tools\NavalHeadingArrows20261104\Validate.ps1 -Stage Test -Run heading-playmode -TestPlatform PlayMode -TestFilter 'NavalTactics_(ManualRouteUsesAllPointsAndRequiresLegalArrivalHeading|GuidedCourseAllowsAlternateBasicHeadingsAndRequiresEastForRam)'

Inspect the process, test XML, Unity log, and Artifacts screenshots under
outputs\naval-heading-arrows-20261104\validation\heading-playmode.
The arrival-choice image is Artifacts\NavalAction_04_ArrivalChoice.png.
SourceSnapshot and source-manifest.csv record the executed UI and test sources.

After Unity exits:
  .\tools\NavalHeadingArrows20261104\Validate.ps1 -Stage Cleanup

Cleanup verifies borrowed-source restoration, removes redundant task backups
and task-created Temp, checks that the main project did not change during
cleanup, and retains the shared Library for existing validation tools. It
preserves logs, screenshots, hashes, the journal, and executed source snapshots.
UIPreviewsForMe is excluded. No external image service is used.
