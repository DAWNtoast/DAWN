param([ValidateSet('Prepare','EditMode','PlayMode')][string]$Action='Prepare')
$ErrorActionPreference='Stop'
$taskSource='D:\Game\UnityClient'
$taskScratch=[IO.Path]::GetFullPath('D:\Game\tmp\shipyard-detail-clean-20261011')
$taskOutput='D:\Game\Artifacts\ComponentLegacyCleanup20261012\validation'
if(-not $taskScratch.StartsWith('D:\Game\tmp\') -or -not(Test-Path -LiteralPath (Join-Path $taskScratch 'Library'))){throw 'Expected cached disposable project.'}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskActive=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskScratch) }
if($taskActive){throw 'Disposable project is already in use.'}
if($Action -eq 'Prepare'){
 foreach($taskDirectory in @('Assets','Packages','ProjectSettings')){
  $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskScratch $taskDirectory))
  if(-not $taskDestination.StartsWith($taskScratch+'\')){throw 'Mirror escaped scratch.'}
  $taskMirrorLog=Join-Path $taskOutput ('prepare-'+$taskDirectory+'.log')
  & robocopy (Join-Path $taskSource $taskDirectory) $taskDestination /MIR /NFL /NDL /NJH /NJS /NP "/LOG:$taskMirrorLog"
  if($LASTEXITCODE -gt 7){throw "Scratch sync failed: $taskDirectory"}
 }
 $taskSettings=Join-Path $taskScratch 'ProjectSettings\ProjectSettings.asset'
 $taskText=[IO.File]::ReadAllText($taskSettings) -replace '(?m)^  companyName:.*$','  companyName: ComponentLegacyValidation' -replace '(?m)^  productName:.*$','  productName: ComponentLegacyCleanup20261012' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail$','    Standalone: com.validation.componentlegacycleanup20261012'
 [IO.File]::WriteAllText($taskSettings,$taskText,[Text.UTF8Encoding]::new($false))
 Write-Output 'Prepared disposable project and isolated PlayerPrefs. No package build.'
 exit 0
}
$taskLog=Join-Path $taskOutput ($Action.ToLowerInvariant()+'.log')
$taskResults=Join-Path $taskOutput ($Action.ToLowerInvariant()+'.xml')
$taskFilter=if($Action -eq 'EditMode'){
 'ComponentCatalogTests;ExpandedComponentCatalogTests;ComponentEnvironmentalEffectsTests;ComponentResaleTests;OwnedShipComponentInventoryTests;ShipyardComponentComparisonTests;ComponentShopDisplayGeometryTests;ComponentArtLazyLoadingTests'
}else{
 'ShatteredSail.Presentation.Tests.ComponentShopShowroomPaginationTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ComponentPurchase_BuyOnlyStoresOneItemWithoutChangingFlagshipAndPersists;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ComponentPurchase_BuyAndEquipReplacesOneItemAndPersists'
}
$taskArguments=@('-batchmode','-projectPath',$taskScratch,'-logFile',$taskLog,'-runTests','-testPlatform',$Action,'-testFilter',$taskFilter,'-testResults',$taskResults)
$taskProcess=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
$taskProcess.WaitForExit()
Write-Output "Unity $Action exit code: $($taskProcess.ExitCode); results: $taskResults"
exit $taskProcess.ExitCode
