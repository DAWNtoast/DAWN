param(
 [Parameter(Mandatory=$true)][ValidateSet('EditMode','PlayMode','Capture')][string]$Stage,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name
)
$ErrorActionPreference='Stop'
$restoreProject='D:/Game/tmp/shipyard-parchment-restore-20261015'
$restoreOutput=Join-Path 'D:/Game/Artifacts/ShipyardParchmentRestore20261015' $Name
if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'Unity.exe' -and $_.ExecutablePath -match '[/\\]Editor[/\\]Unity\.exe$'}).Count){throw 'Unity Editor slot is occupied.'}
if(Test-Path -LiteralPath $restoreOutput){throw 'Use a fresh evidence directory.'}
[IO.Directory]::CreateDirectory($restoreOutput)|Out-Null
$restoreArgs=@('-batchmode','-force-d3d11','-projectPath',$restoreProject,'-logFile',(Join-Path $restoreOutput 'unity.log'))
if($Stage -eq 'Capture'){
 $restoreArgs+=@('-quit','-executeMethod','ShatteredSail.Editor.ShipyardWorkbenchCapture.CaptureFromCommandLine','-shipyardWorkbenchCapturePath',(Join-Path $restoreOutput 'captures'))
}else{
 $restoreFilter=if($Stage -eq 'EditMode'){'ShipyardGalleryTests;ShipyardComponentComparisonTests'}else{'ShipyardEquipment_;ItemIcons_EquipmentAndSettlementKeepPicturesClearOfNamesAndAmounts;DepartureShips_DossierRefitsSelectedVesselAndReturnsWithRouteSeedDecksBeforeSailing'}
 $restoreArgs+=@('-runTests','-testPlatform',$Stage,'-testFilter',$restoreFilter,'-testResults',(Join-Path $restoreOutput 'results.xml'))
}
$restoreQuoted=foreach($value in $restoreArgs){$escaped=[regex]::Replace($value,'(\\*)"','$1$1\"');'"'+[regex]::Replace($escaped,'(\\+)$','$1$1')+'"'}
$restoreProcess=Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList ($restoreQuoted -join ' ') -WorkingDirectory $restoreProject -WindowStyle Hidden -PassThru
[pscustomobject]@{Stage=$Stage;PID=$restoreProcess.Id;Arguments=$restoreArgs}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $restoreOutput 'launch.json') -Encoding utf8
Write-Output ('Started '+$Stage+' PID '+$restoreProcess.Id)
$restoreProcess.WaitForExit()
[pscustomobject]@{Stage=$Stage;ExitCode=$restoreProcess.ExitCode;CompletedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $restoreOutput 'completion.json') -Encoding utf8
if($restoreProcess.ExitCode -ne 0){throw 'Unity validation failed; evidence preserved.'}
if($Stage -ne 'Capture'){
 [xml]$restoreXml=Get-Content -LiteralPath (Join-Path $restoreOutput 'results.xml') -Raw
 $restoreRun=$restoreXml.'test-run'
 if([int]$restoreRun.failed -ne 0 -or [int]$restoreRun.passed -le 0){throw 'Test report did not pass.'}
 Write-Output ($Stage+': '+$restoreRun.passed+' passed; '+$restoreRun.failed+' failed.')
}
