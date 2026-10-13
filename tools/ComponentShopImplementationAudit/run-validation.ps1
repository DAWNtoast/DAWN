param([ValidateSet('Prepare','EditMode','FocusedEditMode','PlayMode','Smoke','Capture','Saved','Text','Export','Repair','RepairPort')][string]$Action='Prepare')
$ErrorActionPreference='Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('Saved','Export') -contains $Action) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$taskSource='D:\Game\UnityClient'
$taskScratch=[IO.Path]::GetFullPath('D:\Game\tmp\shipyard-detail-clean-20261011')
$taskOutput='D:\Game\Artifacts\ComponentShopImplementation20261012\validation'
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
 $taskText=[IO.File]::ReadAllText($taskSettings) -replace '(?m)^  companyName:.*$','  companyName: ComponentShopValidation' -replace '(?m)^  productName:.*$','  productName: ComponentShopImplementation20261012' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail$','    Standalone: com.validation.componentshopimplementation20261012'
 [IO.File]::WriteAllText($taskSettings,$taskText,[Text.UTF8Encoding]::new($false))
 New-Item -ItemType Directory -Path (Join-Path $taskScratch 'Assets\_Project\Editor\ComponentShopAudit') -Force | Out-Null
 Copy-Item -LiteralPath 'D:\Game\tools\ComponentShopImplementationAudit\ComponentShopImplementationCapture.cs' -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\ComponentShopAudit\ComponentShopImplementationCapture.cs')
 Copy-Item -LiteralPath 'D:\Game\tools\ComponentShopImplementationAudit\ComponentShopPreviewExport.cs' -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\ComponentShopAudit\ComponentShopPreviewExport.cs')
 Copy-Item -LiteralPath 'D:\Game\tools\ComponentShopImplementationAudit\ComponentShopResourceRepairAudit.cs' -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\ComponentShopAudit\ComponentShopResourceRepairAudit.cs')
 Write-Output 'Prepared disposable project and isolated PlayerPrefs. No package build.'
 exit 0
}
$taskLog=Join-Path $taskOutput ($Action.ToLowerInvariant()+'.log')
$taskResults=Join-Path $taskOutput ($Action.ToLowerInvariant()+'.xml')
$taskFilter=if($Action -eq 'EditMode'){
 'ComponentShopShowroomLayoutTests;ComponentShopPlaqueLayoutTests;RegionalPortFacilityArtCatalogTests;SpecialCardDossierTests;WoodButtonSkinTests;PortFacilityHeaderTests;ComponentCatalogTests;ShipyardComponentComparisonTests;ComponentShopDisplayGeometryTests'
}else{
 'ShatteredSail.Presentation.Tests.ComponentShopShowroomPaginationTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ComponentPurchase_;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ComponentShopHeader_RetainedHeaderRestoresOtherFacilitiesAndShopReadouts;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.Scene_CompletesPortVoyageReturnLoop'
}
$taskArguments=@('-batchmode','-projectPath',$taskScratch,'-logFile',$taskLog,'-runTests','-testPlatform',$Action,'-testFilter',$taskFilter,'-testResults',$taskResults)
if($Action -eq 'FocusedEditMode'){
 $taskFilter='ComponentShopShowroomLayoutTests;ComponentShopPlaqueLayoutTests;RegionalPortFacilityArtCatalogTests;ComponentCatalogTests;ShipyardComponentComparisonTests;ComponentShopDisplayGeometryTests;ComponentArtLazyLoadingTests'
 $taskArguments=@('-batchmode','-projectPath',$taskScratch,'-logFile',$taskLog,'-runTests','-testPlatform','EditMode','-testFilter',$taskFilter,'-testResults',$taskResults)
}
if($Action -eq 'Smoke' -or $Action -eq 'Capture' -or $Action -eq 'Saved'){
 $taskCaptureDir=Join-Path $taskOutput ($Action.ToLowerInvariant()+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
 $taskArguments=@('-batchmode','-quit','-projectPath',$taskScratch,'-logFile',$taskLog,'-executeMethod','ShatteredSail.Editor.ComponentShopImplementationCapture.CaptureFromCommandLine','-artPolishCapturePath',$taskCaptureDir)
 if($Action -eq 'Smoke'){$taskArguments+='-artPolishSmoke'}
 if($Action -eq 'Saved'){$taskArguments+='-artPolishSaved'}
}
if($Action -eq 'Text' -or $Action -eq 'Export'){
 $taskMethod=if($Action -eq 'Text'){'AuditComponentShopV3TextFromCommandLine'}else{'ExportComponentShopV3FromCommandLine'}
 $taskArguments=@('-batchmode','-quit','-projectPath',$taskScratch,'-logFile',$taskLog,'-executeMethod',('ShatteredSail.Editor.GameplayPreviewSceneBuilder.'+$taskMethod))
}
if($Action -eq 'Repair'){
 $taskArguments=@('-batchmode','-quit','-projectPath',$taskScratch,'-logFile',$taskLog,'-executeMethod','ShatteredSail.Editor.ComponentShopResourceRepairAudit.CheckAndRepairFromCommandLine')
}
if($Action -eq 'RepairPort'){
 $taskArguments=@('-batchmode','-quit','-projectPath',$taskScratch,'-logFile',$taskLog,'-executeMethod','ShatteredSail.Editor.ComponentShopResourceRepairAudit.CheckAndRepairPortArtFromCommandLine')
}
$taskProcess=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
$taskProcess.WaitForExit()
Write-Output "Unity $Action exit code: $($taskProcess.ExitCode); results: $taskResults"
exit $taskProcess.ExitCode
