param([switch]$SkipCache)
$ErrorActionPreference = 'Stop'
$assemblyRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$assemblySource = Join-Path $assemblyRoot 'UnityClient'
$assemblyProject = Join-Path $assemblyRoot 'tmp/shipyard-assembly-20261017'
$assemblyCache = Join-Path $assemblyRoot 'tmp/shipyard-detail-clean-20261011'
if ($assemblyProject -ne 'D:\Game\tmp\shipyard-assembly-20261017') { throw 'Only the named disposable project is allowed.' }
foreach ($assemblyCheck in @($assemblyProject, $assemblyCache)) {
    if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($assemblyCheck)
    }).Count) { throw ('Project is occupied: ' + $assemblyCheck) }
    $assemblyLock = Join-Path $assemblyCheck 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $assemblyLock) {
        try { $assemblyHandle = [IO.File]::Open($assemblyLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $assemblyHandle.Dispose() }
        catch { throw ('Project is open: ' + $assemblyCheck) }
    }
}
[IO.Directory]::CreateDirectory($assemblyProject) | Out-Null
if (-not $SkipCache -and -not (Test-Path -LiteralPath (Join-Path $assemblyProject 'Library'))) {
    $assemblyCacheArgs = @((Join-Path $assemblyCache 'Library'),(Join-Path $assemblyProject 'Library'),'/E','/R:1','/W:1','/MT:8','/NFL','/NDL','/NP','/NJH','/NJS')
    & robocopy @assemblyCacheArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
}
foreach ($assemblyPart in @('Assets','Packages','ProjectSettings')) {
    $assemblyCopy = @((Join-Path $assemblySource $assemblyPart), (Join-Path $assemblyProject $assemblyPart), '/E','/R:1','/W:1','/MT:8','/NFL','/NDL','/NP','/NJH','/NJS')
    if ($assemblyPart -eq 'Assets') {
        $assemblyCopy += @('/XD',(Join-Path $assemblySource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta')
    }
    & robocopy @assemblyCopy | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ('Source copy failed: ' + $assemblyPart) }
}
if ((Test-Path -LiteralPath (Join-Path $assemblyProject 'Assets/_Project/Scenes/UIPreviewsForMe')) -or
    (Test-Path -LiteralPath (Join-Path $assemblyProject 'Assets/_Project/Scenes/UIPreviewsForMe.meta'))) {
    throw 'Retired preview content must never be present in this validation snapshot.'
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ShipyardAssemblyCapture.cs') -Destination (Join-Path $assemblyProject 'Assets/_Project/Editor/ShipyardAssemblyCapture.cs') -Force
$assemblySettingsPath = Join-Path $assemblyProject 'ProjectSettings/ProjectSettings.asset'
$assemblySettings = [IO.File]::ReadAllText($assemblySettingsPath)
$assemblySettings = [regex]::Replace($assemblySettings, '(?m)^  companyName:.*$', '  companyName: CodexShipyardAssemblyValidation')
$assemblySettings = [regex]::Replace($assemblySettings, '(?m)^  productName:.*$', '  productName: ShipyardAssemblyValidation20261017')
[IO.File]::WriteAllText($assemblySettingsPath, $assemblySettings, [Text.UTF8Encoding]::new($false))
$assemblyHashes = foreach ($assemblyFile in @('CoreGameplayShipyardController.cs','CoreGameplayShipyardEquipmentView.cs','CoreGameplayShipyardWorkbench.cs','CoreGameplayEscapeNavigation.cs','CoreGameplayDemoController.cs','CoreGameplayEditorPreviewController.cs','CoreGameplayPortSettingsController.cs')) {
    $assemblyRelative = 'Assets/_Project/Presentation/Runtime/' + $assemblyFile
    [ordered]@{ file = $assemblyRelative; sourceSha256 = (Get-FileHash -LiteralPath (Join-Path $assemblySource $assemblyRelative) -Algorithm SHA256).Hash; snapshotSha256 = (Get-FileHash -LiteralPath (Join-Path $assemblyProject $assemblyRelative) -Algorithm SHA256).Hash }
}
[ordered]@{ copiedAtUtc = [DateTime]::UtcNow.ToString('O'); source = $assemblySource; project = $assemblyProject; cache = $assemblyCache; retiredPreviewExcluded = $true; profileIdentity = 'CodexShipyardAssemblyValidation/ShipyardAssemblyValidation20261017'; sourceFiles = @($assemblyHashes) } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $assemblyProject '.shipyard-assembly-validation.json') -Encoding utf8
Write-Output ('Synced current production files into ' + $assemblyProject + '. No scenes generated; retired previews excluded; profile identity isolated.')
