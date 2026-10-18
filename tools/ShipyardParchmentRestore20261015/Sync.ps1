$ErrorActionPreference='Stop'
$restoreSource='D:/Game/UnityClient'
$restoreProject='D:/Game/tmp/shipyard-parchment-restore-20261015'
$restoreCache='D:/Game/tmp/shipyard-detail-clean-20261011/Library'
if(Test-Path -LiteralPath $restoreProject){throw 'Use a new isolated project; do not overwrite previous evidence.'}
if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'Unity.exe' -and $_.ExecutablePath -match '[/\\]Editor[/\\]Unity\.exe$'}).Count){throw 'Unity Editor slot occupied.'}
[IO.Directory]::CreateDirectory($restoreProject)|Out-Null
[pscustomobject]@{Purpose='Shipyard parchment layout restoration validation';Source=$restoreSource;CreatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath ($restoreProject+'/.shipyard-parchment-validation.json') -Encoding utf8
foreach($part in @('Assets','Packages','ProjectSettings')){
 $copyArgs=@((Join-Path $restoreSource $part),(Join-Path $restoreProject $part),'/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
 if($part -eq 'Assets'){$copyArgs+=@('/XD',(Join-Path $restoreSource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta')}
 & robocopy @copyArgs | Out-Null
 if($LASTEXITCODE -ge 8){throw ('Source copy failed: '+$part)}
}
# Reuse only cached import data; no old scenes or source assets are copied.
& robocopy $restoreCache (Join-Path $restoreProject 'Library') /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
if($LASTEXITCODE -ge 8){throw 'Library cache copy failed.'}
Copy-Item -LiteralPath 'D:/Game/tools/ShipyardWorkbench20261014/ShipyardWorkbenchCapture.cs' -Destination ($restoreProject+'/Assets/_Project/Editor/ShipyardWorkbenchCapture.cs')
$settingsPath=$restoreProject+'/ProjectSettings/ProjectSettings.asset'
$settings=[IO.File]::ReadAllText($settingsPath)
$settings=[regex]::Replace($settings,'(?m)^  companyName:.*$','  companyName: CodexShipyardParchmentValidation')
$settings=[regex]::Replace($settings,'(?m)^  productName:.*$','  productName: ShipyardParchmentRestore20261015')
[IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
if(Test-Path -LiteralPath ($restoreProject+'/Assets/_Project/Scenes/UIPreviewsForMe')){throw 'Retired preview content must not be copied.'}
foreach($path in @('Assets/_Project/Presentation/Runtime/CoreGameplayShipyardController.cs','Assets/_Project/Presentation/Runtime/CoreGameplayShipyardEquipmentView.cs','Assets/_Project/Tests/EditMode/ShipyardGalleryTests.cs')){
 if((Get-FileHash -LiteralPath (Join-Path $restoreSource $path)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $restoreProject $path)).Hash){throw ('Source changed during sync: '+$path)}
}
Write-Output ('Isolated parchment validation project ready: '+$restoreProject)
