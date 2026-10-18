param([switch]$ManualOnly)
$ErrorActionPreference = 'Stop'
$filters = @(
    'GuidedBossScript_',
    'GuidedTutorial_LostBossOpensNormalRecoveryServicesAndCanRetry',
    'GuidedTutorial_TwoVoyagesTeachNavigatorAmbushThenScriptedBossAndDisplayReward',
    'NavalTactics_GuidedCourseAllowsAlternateHeadingsAndBothRealContacts',
    'NavalTactics_ManualRouteUsesAllPointsAndRequiresLegalArrivalHeading',
    'NavalDragRoute_RotatedCameraMapsPointerToWorldAndLocksUntilArrivalHeading'
)
if ($ManualOnly) {
    & 'D:/Game/tools/NavalTactics20261015/RunValidation.ps1' -Stage Play `
        -Output 'D:/Game/Artifacts/NavalDragRoute20261015/PlayManualFinal' `
        -TestFilter 'NavalTactics_ManualRouteUsesAllPointsAndRequiresLegalArrivalHeading'
} else {
    & 'D:/Game/tools/NavalTactics20261015/RunValidation.ps1' -Stage Play `
        -Output 'D:/Game/Artifacts/NavalDragRoute20261015/PlayVerified' -TestFilter ($filters -join ';')
}
