param([ValidateSet('Edit','Play','References')][string]$Stage = 'Edit', [string]$Version = 'v1', [string]$EditFilter = '',
    [string]$Filter = 'CurseRisk_;DepartureCrew_;MonthlyCalendar_;MainMenuSceneTests;RareWarehouse_;TreasureMap_;BattleTactics_;CoreGameplayScene_PortFacilities')
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('References') -contains $Stage) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before isolated QA.' }
$project = 'D:\Game\UnityClient'
$output = Join-Path $project ('Artifacts\CurseRisk20261003\' + $Stage + '-' + $Version)
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null
$launch = @('-batchmode','-projectPath',$project,'-logFile',(Join-Path $output 'Unity.log'))
if ($Stage -eq 'References') {
    $launch += @('-quit','-executeMethod','ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncAndCaptureCurseRiskFromCommandLine',
        '-navalVisualCapturePath',(Join-Path $output 'captures'))
} else {
    $platform = if ($Stage -eq 'Play') { 'PlayMode' } else { 'EditMode' }
    $launch += @('-runTests','-testPlatform',$platform,'-testResults',(Join-Path $output 'Results.xml'))
    if ($Stage -eq 'Play') { $launch += @('-testFilter',$Filter,'-navalVisualCapturePath',(Join-Path $output 'captures')) }
    elseif ($EditFilter) { $launch += @('-testFilter',$EditFilter) }
}
$process = Start-Process 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $launch -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output "Unity exit: $($process.ExitCode)"
if (Test-Path -LiteralPath (Join-Path $output 'Results.xml')) {
    [xml]$result = Get-Content -LiteralPath (Join-Path $output 'Results.xml') -Raw
    $result.'test-run' | Select-Object result,total,passed,failed,skipped,duration
}
exit $process.ExitCode
