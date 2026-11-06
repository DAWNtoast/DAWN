$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot 'Artifacts/BossCalibration20261024/Rush20261026Regression'
$taskBuild=Join-Path $taskOutput 'Build'
$taskFixtures=@('CoreNavalBossRush20261026Tests','CoreNavalBossBehaviorTests','CoreNavalBossCalibrationTests',
    'CoreBattleIntentPresentationTests','CoreTideFieldRevision20261024Tests','CoreNavalFieldsTests',
    'CoreFireRules20261024Tests','CoreThunderFieldRevision20261024Tests','CoreBloodFieldRevision20261024Tests',
    'CoreNavalMovementRamTests','CoreGuidedBossScriptTests','CoreTrainingBattleTests','CoreTrainingBattleContinuityTests',
    'CoreBoardingFieldInheritanceTests','CoreBoardingFieldEnemyTests','CoreBattleStatusPresentationTests')
try {
    & (Join-Path $taskRoot 'tools/BossCalibration20261024/RunCoreTests.ps1') -RunTag Rush20261026Regression -Fixtures $taskFixtures
} finally {
    # The shared runner has exited. Only its two reproducible outputs belong to us.
    foreach($taskPath in @($taskRoot,(Join-Path $taskRoot 'Artifacts'),(Join-Path $taskRoot 'Artifacts/BossCalibration20261024'),$taskOutput,$taskBuild)){
        if((Test-Path -LiteralPath $taskPath) -and ((Get-Item -LiteralPath $taskPath).Attributes -band [IO.FileAttributes]::ReparsePoint)){
            throw "Cleanup refused directory link: $taskPath"
        }
    }
    $taskAllowed=[IO.Path]::GetFullPath((Join-Path $taskRoot 'Artifacts/BossCalibration20261024/Rush20261026Regression/Build'))
    if([IO.Path]::GetFullPath($taskBuild) -ne $taskAllowed){throw 'Unexpected cleanup directory'}
    $taskUsing=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -in @('mono.exe','dotnet.exe','Unity.exe') -and $_.CommandLine -and $_.CommandLine.Contains($taskBuild)})
    if($taskUsing.Count){throw 'Validation build still used by a process'}
    $taskFreed=0L
    foreach($taskName in @('NavalBalanceTests.exe','nunit.framework.dll')){
        $taskFile=Join-Path $taskBuild $taskName
        if(Test-Path -LiteralPath $taskFile){
            $taskItem=Get-Item -LiteralPath $taskFile
            if($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Cleanup refused file link: $taskFile"}
            $taskFreed+=$taskItem.Length
            Remove-Item -LiteralPath $taskFile -Force
        }
    }
    if((Test-Path -LiteralPath $taskBuild) -and @(Get-ChildItem -LiteralPath $taskBuild -Force).Count -eq 0){Remove-Item -LiteralPath $taskBuild}
    if(Test-Path -LiteralPath $taskOutput){"Removed temporary Core test binaries: $taskFreed bytes; retained logs, XML and source hashes."|Add-Content -LiteralPath (Join-Path $taskOutput 'cleanup.log')}
}
