$ErrorActionPreference='Stop'
$taskRoot='D:\Game\Artifacts\EquipmentUniqueRedraw20261012'
$taskScratch=[IO.Path]::GetFullPath('D:\Game\tmp\shipyard-detail-clean-20261011')
if(-not $taskScratch.StartsWith('D:\Game\tmp\')){throw 'Baseline check must use disposable workspace.'}
$taskFiles=(Get-Content -LiteralPath (Join-Path $taskRoot 'baseline.json') -Raw | ConvertFrom-Json).files | Where-Object { $_.path.StartsWith('UnityClient/') -and -not $_.path.Contains('/Tests/') }
try {
 foreach($taskFile in $taskFiles){
  $taskRelative=$taskFile.path.Substring('UnityClient/'.Length)
  Copy-Item -LiteralPath (Join-Path (Join-Path $taskRoot 'baseline') $taskFile.path) -Destination (Join-Path $taskScratch $taskRelative) -Force
 }
 $taskArguments=@('-batchmode','-projectPath',$taskScratch,'-logFile',(Join-Path $taskRoot 'validation\baseline-cards.log'),'-runTests','-testPlatform','EditMode','-testFilter','ShatteredSail.Core.Tests.SpecialCardCatalogTests','-testResults',(Join-Path $taskRoot 'validation\baseline-cards.xml'))
 $taskProcess=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
 $taskProcess.WaitForExit()
 Write-Output "Baseline card suite exit code: $($taskProcess.ExitCode)"
} finally {
 foreach($taskFile in $taskFiles){
  $taskRelative=$taskFile.path.Substring('UnityClient/'.Length)
  Copy-Item -LiteralPath (Join-Path 'D:\Game' $taskFile.path) -Destination (Join-Path $taskScratch $taskRelative) -Force
 }
 Write-Output 'Restored disposable project files from current production; production was read-only.'
}
