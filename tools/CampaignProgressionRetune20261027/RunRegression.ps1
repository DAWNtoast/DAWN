param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag='ProgressionRetune01')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot ('Artifacts/BossCalibration20261024/'+$RunTag)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new run tag to preserve evidence.'}
$taskFixtures=@(
 'CoreNavalBossCalibrationTests','CoreBoardingBossPacingTests','CoreNavalBossBehaviorTests',
 'CoreNavalBossExtendedChainTests','CoreNavalBossFieldBehavior20261026Tests','CoreNavalBossImmunityIntentTests',
 'CoreNavalBossRush20261026Tests','LegendaryWarshipTests','CoreGuidedBossScriptTests',
 'CoreTrainingBattleTests','CoreTrainingBattleContinuityTests','CoreCardSchoolUnlockTests','CoreWarshipRosterTests',
 'CoreBattleRewardConversionTests','CoreBattleRewardDescriptionTests','BattleRewardOverflowTests',
 'CoreEventChoiceCatalogAuditTests','CoreSeaChartProgressTests','CoreBoardingLootTests',
 'CoreVoyageExperienceEligibilityTests','SpecialCardProgressionTests','SpecialCardExperienceAuditTests'
)
$taskPassed=$false
try {
 & (Join-Path $taskRoot 'tools/BossCalibration20261024/RunCoreTests.ps1') -RunTag $RunTag -Fixtures $taskFixtures
 $taskPassed=$true
} finally {
 $taskBuild=[IO.Path]::GetFullPath((Join-Path $taskOutput 'Build'))
 $taskAllowed=[IO.Path]::GetFullPath((Join-Path $taskRoot 'Artifacts/BossCalibration20261024'))+[IO.Path]::DirectorySeparatorChar
 if(!$taskBuild.StartsWith($taskAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe cleanup target.'}
 $taskFreed=0
 if(Test-Path -LiteralPath $taskBuild){
  foreach($ancestor in @($taskRoot,(Join-Path $taskRoot 'Artifacts'),(Join-Path $taskRoot 'Artifacts/BossCalibration20261024'),$taskOutput,$taskBuild)){
   if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refuse linked cleanup ancestor.'}
  }
  $taskBusy=@(Get-CimInstance Win32_Process|Where-Object {$_.ProcessId -ne $PID -and $_.CommandLine -and ($_.CommandLine.IndexOf($taskBuild,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $_.CommandLine.IndexOf($taskBuild.Replace('\','/'),[StringComparison]::OrdinalIgnoreCase) -ge 0)})
  if($taskBusy.Count){throw 'A process still references the build; preserve it for diagnosis.'}
  foreach($entry in Get-ChildItem -LiteralPath $taskBuild -Force){
   if($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $entry.Extension -notin @('.exe','.dll','.pdb','.mdb')){throw 'Unexpected build artifact; preserve it.'}
   $taskFreed+=$entry.Length
   Remove-Item -LiteralPath $entry.FullName -Force
  }
  Remove-Item -LiteralPath $taskBuild
 }
 [pscustomobject]@{Passed=$taskPassed;ReleasedBytes=$taskFreed;BuildRemaining=(Test-Path -LiteralPath $taskBuild);Preserved='Fixtures, hashes, compile/test logs and results';MainProjectCleanup=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'cleanup.json') -Encoding utf8
}
