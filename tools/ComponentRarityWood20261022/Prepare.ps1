param([switch]$SkipCache)
$ErrorActionPreference = 'Stop'
$rarityWorkspace = 'D:\Game'
$raritySource = Join-Path $rarityWorkspace 'UnityClient'
$rarityProject = [IO.Path]::GetFullPath((Join-Path $rarityWorkspace 'tmp/component-rarity-wood-20261022'))
$rarityCache = Join-Path $rarityWorkspace 'tmp/component-rarity-halo-20261019'
if ($rarityProject -ne 'D:\Game\tmp\component-rarity-wood-20261022') { throw 'Unexpected validation destination.' }
foreach ($check in @($rarityProject, $rarityCache)) {
    if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($check) }).Count) { throw ('Project occupied: ' + $check) }
    $lock = Join-Path $check 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lock) {
        try { $handle = [IO.File]::Open($lock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $handle.Dispose() }
        catch { throw ('Project locked: ' + $check) }
    }
}
[IO.Directory]::CreateDirectory($rarityProject) | Out-Null
if (-not $SkipCache -and -not (Test-Path -LiteralPath (Join-Path $rarityProject 'Library'))) {
    $library = Join-Path $rarityCache 'Library'
    if (Test-Path -LiteralPath $library) {
        $bytes = (Get-ChildItem -LiteralPath $library -File -Recurse | Measure-Object Length -Sum).Sum
        if ([IO.DriveInfo]::new('D:\').AvailableFreeSpace -lt ($bytes * 1.2 + 15GB)) { throw 'Insufficient disk space for warm validation copy.' }
        & robocopy $library (Join-Path $rarityProject 'Library') /E /R:1 /W:1 /MT:8 /NFL /NDL /NP /NJH /NJS
        if ($LASTEXITCODE -ge 8) { throw 'Warm cache copy failed.' }
    }
}
foreach ($part in @('Assets', 'Packages', 'ProjectSettings')) {
    $target = [IO.Path]::GetFullPath((Join-Path $rarityProject $part))
    if (-not $target.StartsWith($rarityProject + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Snapshot target escaped project.' }
    $copyArgs = @((Join-Path $raritySource $part), $target, '/E', '/R:1', '/W:1', '/MT:8', '/NFL', '/NDL', '/NP', '/NJH', '/NJS')
    if ($part -eq 'Assets') { $copyArgs += @('/XD', (Join-Path $raritySource 'Assets/_Project/Scenes/UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta') }
    & robocopy @copyArgs
    if ($LASTEXITCODE -ge 8) { throw ('Snapshot copy failed: ' + $part) }
}
$settingsPath = Join-Path $rarityProject 'ProjectSettings/ProjectSettings.asset'
$settings = [IO.File]::ReadAllText($settingsPath)
$settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: CodexComponentRarityWoodValidation')
$settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: ComponentRarityWoodValidation20261022')
[IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
$retired = Join-Path $rarityProject 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $retired) -or (Test-Path -LiteralPath ($retired + '.meta'))) { throw 'Retired preview path must not be present.' }
Write-Output ('Prepared ' + $rarityProject)
