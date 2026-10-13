param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode','Capture')][string]$Action='Prepare',
    [string]$CaptureName='ui-review-final',
    [switch]$Smoke
)
$ErrorActionPreference='Stop'
$taskWorkspace=[IO.Path]::GetFullPath('D:\Game')
$taskSource=[IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskScratch=[IO.Path]::GetFullPath('D:\Game\tmp\shipyard-detail-clean-20261011')
$taskOutput=[IO.Path]::GetFullPath('D:\Game\Artifacts\ArtPolishBatch1_20261012\validation')
if(-not $taskScratch.StartsWith($taskWorkspace+'\tmp\') -or -not $taskOutput.StartsWith($taskWorkspace+'\Artifacts\')){throw 'Scratch/output must stay within workspace.'}
if(-not(Test-Path -LiteralPath (Join-Path $taskScratch 'Library'))){throw 'Expected cached disposable project missing.'}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if($Action -eq 'Prepare'){
 foreach($taskDirectory in @('Assets','Packages','ProjectSettings')){
  $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskScratch $taskDirectory))
  if(-not $taskDestination.StartsWith($taskScratch+'\')){throw 'Mirror escaped scratch.'}
  & robocopy (Join-Path $taskSource $taskDirectory) $taskDestination /MIR /NFL /NDL /NJH /NJS /NP
  if($LASTEXITCODE -gt 7){throw "Scratch sync failed: $taskDirectory"}
 }
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ArtPolishBatch1Capture.cs') -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\ArtPolishBatch1Capture.cs')
 $taskSettings=Join-Path $taskScratch 'ProjectSettings\ProjectSettings.asset'
 $taskText=[IO.File]::ReadAllText($taskSettings)
 $taskText=$taskText -replace '(?m)^  companyName:.*$','  companyName: ArtPolishValidation' -replace '(?m)^  productName:.*$','  productName: ArtPolishBatch120261012' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail$','    Standalone: com.validation.artpolishbatch120261012'
 [IO.File]::WriteAllText($taskSettings,$taskText,[Text.UTF8Encoding]::new($false))
 Write-Output 'Prepared disposable project; no package build.'
 exit 0
}
$taskLog=Join-Path $taskOutput ($Action.ToLowerInvariant()+'.log')
$taskArguments=@('-batchmode','-projectPath',$taskScratch,'-logFile',$taskLog)
if($Action -eq 'Compile'){$taskArguments+=@('-quit')}
elseif($Action -eq 'Capture'){
 $taskCapture=Join-Path $taskOutput $CaptureName
 if(Test-Path -LiteralPath $taskCapture){throw 'Use a new capture directory.'}
 $taskArguments+=@('-quit','-executeMethod','ShatteredSail.Editor.ArtPolishBatch1Capture.CaptureFromCommandLine','-artPolishCapturePath',$taskCapture)
 if($Smoke){$taskArguments+='-artPolishSmoke'}
}else{
 $taskResults=Join-Path $taskOutput ($Action.ToLowerInvariant()+'.xml')
 $taskFilter=if($Action -eq 'EditMode'){
  'ShatteredSail.Core.Tests.ComponentShopDisplayGeometryTests;ShatteredSail.Presentation.Tests.ComponentShopShowroomLayoutTests;ShatteredSail.Core.Tests.ComponentArtLazyLoadingTests;ShatteredSail.Presentation.Tests.ArtCatalogLazyLoadingTests;ShatteredSail.Core.Tests.EncounterDossierLayoutTests'
 }else{
  'ShatteredSail.Presentation.Tests.ComponentShopShowroomPaginationTests;ShatteredSail.Presentation.Tests.ShipComponentInventoryFlowTests'
 }
 $taskArguments+=@('-runTests','-testPlatform',$Action,'-testFilter',$taskFilter,'-testResults',$taskResults)
}
$taskProcess=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
# Wait only for this Editor, not the persistent licensing helper it may launch.
$taskProcess.WaitForExit()
Write-Output "Unity $Action exit code: $($taskProcess.ExitCode); log: $taskLog"
exit $taskProcess.ExitCode
