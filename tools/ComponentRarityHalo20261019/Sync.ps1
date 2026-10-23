param([switch]$SkipCache)
$ErrorActionPreference = 'Stop'
$haloRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$haloSource = Join-Path $haloRoot 'UnityClient'
$haloProject = [IO.Path]::GetFullPath((Join-Path $haloRoot 'tmp/component-rarity-halo-20261019'))
$haloCache = [IO.Path]::GetFullPath((Join-Path $haloRoot 'tmp/shipyard-assembly-20261017'))
if ($haloProject -ne 'D:\Game\tmp\component-rarity-halo-20261019') { throw 'Only the named disposable project is allowed.' }
foreach ($haloCheck in @($haloProject,$haloCache)) {
    if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($haloCheck) }).Count) { throw ('Project occupied: ' + $haloCheck) }
    $haloLock = Join-Path $haloCheck 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $haloLock) {
        try { $haloHandle=[IO.File]::Open($haloLock,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); $haloHandle.Dispose() }
        catch { throw ('Project is open: ' + $haloCheck) }
    }
}
[IO.Directory]::CreateDirectory($haloProject) | Out-Null
if (-not $SkipCache -and -not (Test-Path -LiteralPath (Join-Path $haloProject 'Library'))) {
    $haloCacheLibrary = Join-Path $haloCache 'Library'
    if (-not (Test-Path -LiteralPath $haloCacheLibrary)) { throw 'Expected warm cache is missing; use -SkipCache for a cold import.' }
    $haloCacheBytes=(Get-ChildItem -LiteralPath $haloCacheLibrary -File -Recurse | Measure-Object Length -Sum).Sum
    $haloDisk=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($haloProject))
    if ($haloDisk.AvailableFreeSpace -lt ($haloCacheBytes * 1.3 + 20GB)) { throw 'Insufficient space for the warm cache plus validation assets.' }
    & robocopy $haloCacheLibrary (Join-Path $haloProject 'Library') /E /R:1 /W:1 /MT:8 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
}
foreach ($haloPart in @('Assets','Packages','ProjectSettings')) {
    $haloDestination=[IO.Path]::GetFullPath((Join-Path $haloProject $haloPart))
    if (-not $haloDestination.StartsWith($haloProject+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Snapshot escaped intended workspace.' }
    $haloCopy=@((Join-Path $haloSource $haloPart),$haloDestination,'/E','/R:1','/W:1','/MT:8','/NFL','/NDL','/NP','/NJH','/NJS')
    if ($haloPart -eq 'Assets') { $haloCopy += @('/XD',(Join-Path $haloSource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta') }
    & robocopy @haloCopy | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ('Source copy failed: ' + $haloPart) }
}
if ((Test-Path -LiteralPath (Join-Path $haloProject 'Assets/_Project/Scenes/UIPreviewsForMe')) -or (Test-Path -LiteralPath (Join-Path $haloProject 'Assets/_Project/Scenes/UIPreviewsForMe.meta'))) { throw 'Retired previews must never be present in the snapshot.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $haloProject 'Assets/_Project/Editor/ComponentRarityHaloCapture.cs') -Force
$haloSettingsPath=Join-Path $haloProject 'ProjectSettings/ProjectSettings.asset'
$haloSettings=[IO.File]::ReadAllText($haloSettingsPath)
$haloSettings=[regex]::Replace($haloSettings,'(?m)^  companyName:.*$','  companyName: CodexComponentRarityValidation')
$haloSettings=[regex]::Replace($haloSettings,'(?m)^  productName:.*$','  productName: ComponentRarityHaloValidation20261019')
[IO.File]::WriteAllText($haloSettingsPath,$haloSettings,[Text.UTF8Encoding]::new($false))
$haloHashes=foreach ($haloFile in @('ComponentRarityHaloGraphic.cs','CoreGameplayItemIcons.cs','CoreGameplayComponentShopShowroom.cs','CoreGameplayPortController.cs','CoreGameplayComponentResale.cs','CoreGameplayShipyardEquipmentView.cs','CoreGameplayGuidedTutorialController.cs')) {
    $haloRelative='Assets/_Project/Presentation/Runtime/'+$haloFile
    if (Test-Path -LiteralPath (Join-Path $haloSource $haloRelative)) {
        [ordered]@{ file=$haloRelative; sourceSha256=(Get-FileHash -LiteralPath (Join-Path $haloSource $haloRelative) -Algorithm SHA256).Hash; snapshotSha256=(Get-FileHash -LiteralPath (Join-Path $haloProject $haloRelative) -Algorithm SHA256).Hash }
    }
}
[ordered]@{ copiedAtUtc=[DateTime]::UtcNow.ToString('O'); source=$haloSource; project=$haloProject; cache=$haloCache; retiredPreviewExcluded=$true; profileIdentity='CodexComponentRarityValidation/ComponentRarityHaloValidation20261019'; files=@($haloHashes) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $haloProject '.rarity-halo-validation.json') -Encoding utf8
Write-Output ('Synced production files into '+$haloProject+'. No Unity launch, saved scenes or player-profile changes.')