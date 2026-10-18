param([string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011')
$ErrorActionPreference = 'Stop'
$workbenchSource = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../UnityClient')).TrimEnd('\','/')
$workbenchProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$workbenchExpected = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
if ($workbenchProject -ne $workbenchExpected -or $workbenchProject -eq $workbenchSource) { throw 'Only the named isolated validation project is allowed.' }
if (-not (Test-Path -LiteralPath (Join-Path $workbenchProject 'Library'))) { throw 'Warm isolated project missing.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($workbenchProject) }).Count) { throw 'Isolated project is occupied.' }
$workbenchLock = Join-Path $workbenchProject 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $workbenchLock) {
    try { $workbenchCheck = [IO.File]::Open($workbenchLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $workbenchCheck.Dispose() }
    catch { throw 'Isolated project is open.' }
}
foreach ($workbenchPart in @('Assets','Packages','ProjectSettings')) {
    $workbenchCopy = @((Join-Path $workbenchSource $workbenchPart), (Join-Path $workbenchProject $workbenchPart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
    if ($workbenchPart -eq 'Assets') { $workbenchCopy += @('/XD', (Join-Path $workbenchSource 'Assets/_Project/Scenes/UIPreviewsForMe')) }
    & robocopy @workbenchCopy | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $workbenchPart) }
}
$workbenchScratch = [IO.Path]::GetFullPath((Join-Path $workbenchProject 'Assets/_Project')).TrimEnd('\','/')
foreach ($workbenchOld in @(Get-ChildItem -LiteralPath $workbenchScratch -Filter '*.cs' -File -Recurse)) {
    $workbenchRelative = $workbenchOld.FullName.Substring($workbenchScratch.Length + 1)
    if ($workbenchRelative.Contains('UIPreviewsForMe') -or $workbenchOld.Name -eq 'ShipyardWorkbenchCapture.cs' -or
        (Test-Path -LiteralPath (Join-Path $workbenchSource ('Assets/_Project/' + $workbenchRelative)))) { continue }
    $workbenchResolved = [IO.Path]::GetFullPath($workbenchOld.FullName)
    if (-not $workbenchResolved.StartsWith($workbenchScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source outside scratch project.' }
    Remove-Item -LiteralPath $workbenchResolved -Force
    if (Test-Path -LiteralPath ($workbenchResolved + '.meta')) { Remove-Item -LiteralPath ($workbenchResolved + '.meta') -Force }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ShipyardWorkbenchCapture.cs') -Destination (Join-Path $workbenchProject 'Assets/_Project/Editor/ShipyardWorkbenchCapture.cs') -Force
$workbenchSettingsPath = Join-Path $workbenchProject 'ProjectSettings/ProjectSettings.asset'
$workbenchSettings = [IO.File]::ReadAllText($workbenchSettingsPath)
$workbenchSettings = [regex]::Replace($workbenchSettings, '(?m)^  companyName:.*$', '  companyName: CodexShipyardWorkbenchValidation')
$workbenchSettings = [regex]::Replace($workbenchSettings, '(?m)^  productName:.*$', '  productName: ShipyardWorkbench20261014')
[IO.File]::WriteAllText($workbenchSettingsPath, $workbenchSettings, [Text.UTF8Encoding]::new($false))
Write-Output ('Synced current production files and isolated profile identity to ' + $workbenchProject + '. No preview scenes generated.')
