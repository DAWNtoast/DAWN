param([switch]$SkipCache)
$ErrorActionPreference = 'Stop'
$flightRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$flightSource = Join-Path $flightRoot 'UnityClient'
$flightProject = Join-Path $flightRoot 'tmp/battle-card-flight-20261017'
$flightCache = Join-Path $flightRoot 'tmp/shipyard-assembly-20261017'
if ($flightProject -ne 'D:\Game\tmp\battle-card-flight-20261017') { throw 'Only this task validation project is allowed.' }
foreach ($flightCheck in @($flightProject, $flightCache)) {
    if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($flightCheck)
    }).Count) { throw ('Project is occupied: ' + $flightCheck) }
    $flightLock = Join-Path $flightCheck 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $flightLock) {
        try { $flightHandle = [IO.File]::Open($flightLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $flightHandle.Dispose() }
        catch { throw ('Project is open: ' + $flightCheck) }
    }
}
[IO.Directory]::CreateDirectory($flightProject) | Out-Null
if (-not $SkipCache -and -not (Test-Path -LiteralPath (Join-Path $flightProject 'Library'))) {
    $flightCacheArgs = @((Join-Path $flightCache 'Library'),(Join-Path $flightProject 'Library'),'/E','/R:1','/W:1','/MT:8','/NFL','/NDL','/NP','/NJH','/NJS')
    & robocopy @flightCacheArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
}
foreach ($flightPart in @('Assets','Packages','ProjectSettings')) {
    $flightCopy = @((Join-Path $flightSource $flightPart),(Join-Path $flightProject $flightPart),'/E','/R:1','/W:1','/MT:8','/NFL','/NDL','/NP','/NJH','/NJS')
    if ($flightPart -eq 'Assets') {
        $flightCopy += @('/XD',(Join-Path $flightSource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta')
    }
    & robocopy @flightCopy | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ('Source copy failed: ' + $flightPart) }
}
if ((Test-Path -LiteralPath (Join-Path $flightProject 'Assets/_Project/Scenes/UIPreviewsForMe')) -or
    (Test-Path -LiteralPath (Join-Path $flightProject 'Assets/_Project/Scenes/UIPreviewsForMe.meta'))) {
    throw 'Retired preview content must remain excluded.'
}
$flightSettingsPath = Join-Path $flightProject 'ProjectSettings/ProjectSettings.asset'
$flightSettings = [IO.File]::ReadAllText($flightSettingsPath)
$flightSettings = [regex]::Replace($flightSettings,'(?m)^  companyName:.*$','  companyName: CodexBattleCardFlightValidation')
$flightSettings = [regex]::Replace($flightSettings,'(?m)^  productName:.*$','  productName: BattleCardFlightValidation20261017')
[IO.File]::WriteAllText($flightSettingsPath,$flightSettings,[Text.UTF8Encoding]::new($false))
$flightFiles = @('Presentation/Runtime/BattleHandFlightMotion.cs','Presentation/Runtime/BattleCardFlightBackGraphic.cs',
    'Presentation/Runtime/CoreGameplayBattleHandFlight.cs','Presentation/Runtime/CoreGameplayBattleAnimationController.cs',
    'Presentation/Runtime/CoreGameplayBattleViewRefresh.cs','Presentation/Runtime/CoreGameplayDemoController.cs',
    'Presentation/Runtime/CoreGameplayBattleDeckOverview.cs','Presentation/Runtime/CoreGameplayCompactBattleHud.cs',
    'Tests/PlayMode/BattleHandFlightTests.cs',
    'Tests/PlayMode/BattleHandFlightCaptureTests.cs','Tests/PlayMode/NavalFanHandTests.cs')
$flightHashes = foreach ($flightFile in $flightFiles) {
    $flightRelative = 'Assets/_Project/' + $flightFile
    [ordered]@{ file=$flightRelative; sourceSha256=(Get-FileHash -LiteralPath (Join-Path $flightSource $flightRelative)).Hash; snapshotSha256=(Get-FileHash -LiteralPath (Join-Path $flightProject $flightRelative)).Hash }
}
[ordered]@{ copiedAtUtc=[DateTime]::UtcNow.ToString('O'); source=$flightSource; project=$flightProject;
    cache=$flightCache; retiredPreviewExcluded=$true; profileIdentity='CodexBattleCardFlightValidation/BattleCardFlightValidation20261017'; sourceFiles=@($flightHashes) } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $flightProject '.battle-card-flight-validation.json') -Encoding utf8
Write-Output ('Synced current production files into ' + $flightProject + '; retired previews excluded and storage identity isolated.')
