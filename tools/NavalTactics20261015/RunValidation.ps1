param(
    [ValidateSet('Edit','Play','Capture','SaveScene')][string]$Stage,
    [string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011',
    [string]$Output = 'D:/Game/Artifacts/NavalTactics20261015/validation',
    [string]$TestFilter,
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$navalWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$navalProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$navalTmp = Join-Path $navalWorkspace 'tmp'
if (-not $navalProject.StartsWith($navalTmp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Only isolated workspace/tmp projects are accepted.' }
$navalRetired = Join-Path $navalProject 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $navalRetired) -and @(Get-ChildItem -LiteralPath $navalRetired -Force).Count) { throw 'Refuse to run a validation project containing retired preview outputs.' }
$navalRunning = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($navalProject.Replace('\','/'))
})
if ($navalRunning.Count) { throw 'The isolated project is occupied by Unity.' }
[IO.Directory]::CreateDirectory($Output) | Out-Null
$navalLog = Join-Path $Output ($Stage.ToLowerInvariant() + '.log')
$navalArgs = @('-batchmode','-projectPath',('"' + $navalProject + '"'),'-logFile',('"' + $navalLog + '"'))
if ($Stage -eq 'SaveScene') {
    $navalArgs += @('-quit','-executeMethod','ShatteredSail.Editor.NavalTacticsSceneReview.SaveBattlePreviewFromCommandLine')
} elseif ($Stage -eq 'Capture') {
    $navalArgs += @('-quit','-executeMethod','ShatteredSail.Editor.NavalTacticsSceneReview.CaptureFromCommandLine',
        '-navalTacticsCapturePath',('"' + (Join-Path $Output 'captures') + '"'))
} else {
    if (-not $TestFilter) {
        $TestFilter = if ($Stage -eq 'Play') { 'ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalTactics;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalActionHud_ImmediateCardsRefreshPointsAndLockThroughArrivalHeading;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalFanHand_HoverKeepsOriginalPointerRevealsCardAndRestoresFanAfterPlay;ShatteredSail.Presentation.Tests.BattlePresentationTests.EightCardHand_FansWithinBottomAreaAndRevealsHoveredCard;ShatteredSail.Presentation.Tests.BattlePresentationTests.NavalFanCostCorners_VisibleTabsAndLiftedCostsAddressTheSameCard' } else { 'ShatteredSail.Core.Tests.CoreNavalTacticsTests;ShatteredSail.Core.Tests.CoreNavalActionMovementTests;ShatteredSail.Core.Tests.CoreGuidedBossScriptTests;ShatteredSail.Core.Tests.NavalTacticalBoardViewTests' }
    }
    if ($Stage -eq 'Edit') { $navalArgs += '-nographics' }
    $navalArgs += @('-runTests','-testPlatform',($Stage + 'Mode'),'-testFilter',('"' + $TestFilter + '"'),
        '-testResults',('"' + (Join-Path $Output ($Stage.ToLowerInvariant() + '-results.xml')) + '"'))
    if ($Stage -eq 'Play') { $navalArgs += @('-navalVisualCapturePath',('"' + (Join-Path $Output 'play-captures') + '"')) }
}
$navalProcess = Start-Process -FilePath $UnityEditor -ArgumentList $navalArgs -WorkingDirectory $navalProject -WindowStyle Hidden -PassThru
Write-Output ("Started naval {0}: PID {1}; log {2}" -f $Stage,$navalProcess.Id,$navalLog)
