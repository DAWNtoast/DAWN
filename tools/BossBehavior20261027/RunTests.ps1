param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'Final')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskTag = 'BossBehavior20261027-' + $RunTag
$taskOutput = Join-Path $taskRoot ('Artifacts/BossCalibration20261024/' + $taskTag)
$taskBuild = [IO.Path]::GetFullPath((Join-Path $taskOutput 'Build'))
if (Test-Path -LiteralPath $taskOutput) { throw 'Choose a fresh run tag; preserve prior validation evidence.' }
$taskFixtures = @(
 'CoreNavalBossExtendedChainTests','CoreNavalBossImmunityIntentTests',
 'CoreNavalBossFieldBehavior20261026Tests','CoreNavalBossBehaviorTests','CoreNavalBossRush20261026Tests',
 'CoreNavalBossCalibrationTests','CoreNavalDualField20261026Tests','CoreNavalFieldsTests',
 'CoreNavalExpansionCardTests','CoreTideFieldRevision20261024Tests','CoreThunderFieldRevision20261024Tests',
 'CoreBloodFieldRevision20261024Tests','CoreFireRules20261024Tests','CoreArcaneNavalRevision20261024Tests',
 'CoreCurseNavalRevision20261024Tests','CoreBattleIntentPresentationTests','CoreBattleStatusPresentationTests',
 'CoreNavalMovementRamTests','CoreNavalMovementReachTests','CoreNavalSixSchoolIntegrationTests',
 'CoreBoardingFieldInheritanceTests','CoreBoardingFieldEnemyTests','CoreBoardingInheritedFieldEffectsTests',
 'CoreBoardingDualField20261026Tests','CoreGuidedBossScriptTests','CoreTrainingBattleTests',
 'CoreTrainingBattleContinuityTests','CoreNavalFireArcanePowerTests','CoreNavalThunderPowerTests',
 'CoreNavalStatImpactTests',
 'CoreComponentFieldCardsTests','CoreBloodNavalRevision20261024Tests','CoreThunderNavalRevision20261024Tests',
 'CoreTideNavalRevision20261024Tests','CoreNavalBalance20261024Tests','CoreSchoolPowerRolesTests','LegendaryWarshipTests'
)
function Assert-TaskBuildPath {
 $expected = [IO.Path]::GetFullPath((Join-Path $taskRoot ('Artifacts/BossCalibration20261024/' + $taskTag + '/Build')))
 if ($taskBuild -ne $expected) { throw 'Unexpected task build path.' }
 foreach ($p in @($taskRoot,(Join-Path $taskRoot 'Artifacts'),(Join-Path $taskRoot 'Artifacts/BossCalibration20261024'),$taskOutput,$taskBuild)) {
  if ((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked build path: ' + $p) }
 }
}
Assert-TaskBuildPath
try {
 & (Join-Path $taskRoot 'tools/BossCalibration20261024/RunCoreTests.ps1') -RunTag $taskTag -Fixtures $taskFixtures
} finally {
 Assert-TaskBuildPath
 $busy = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and
  ($_.CommandLine.IndexOf($taskBuild,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
   $_.CommandLine.IndexOf($taskBuild.Replace('\','/'),[StringComparison]::OrdinalIgnoreCase) -ge 0) })
 if ($busy.Count) { throw 'A process still references the task build; cleanup deferred.' }
 $freed = 0L
 foreach ($name in @('NavalBalanceTests.exe','nunit.framework.dll')) {
  $file = [IO.Path]::GetFullPath((Join-Path $taskBuild $name))
  if ([IO.Path]::GetDirectoryName($file) -ne $taskBuild) { throw 'Unexpected cleanup target.' }
  if (Test-Path -LiteralPath $file) {
   $item = Get-Item -LiteralPath $file -Force
   if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected compiler artifact.' }
   $freed += $item.Length
   Remove-Item -LiteralPath $file -Force
   if (Test-Path -LiteralPath $file) { throw 'Cleanup failed.' }
  }
 }
 if ((Test-Path -LiteralPath $taskBuild) -and @(Get-ChildItem -LiteralPath $taskBuild -Force).Count -eq 0) { Remove-Item -LiteralPath $taskBuild }
 if (Test-Path -LiteralPath $taskOutput) {
  [pscustomobject]@{FreedBytes=$freed;BuildRemoved=(-not(Test-Path -LiteralPath $taskBuild));MainProjectCleanup=$false;Preserved='Sources hashes, fixtures, compile log and test results';SharedDependenciesRetained='Unity compiler/framework and main Library NUnit package read only'} |
   ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'cleanup.json') -Encoding utf8
 }
 Write-Output ('Task test binaries cleaned: ' + $freed + ' bytes.')
}
