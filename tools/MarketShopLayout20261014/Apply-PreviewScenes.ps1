param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$marketProduction = [IO.Path]::GetFullPath('D:/Game/UnityClient').TrimEnd('\','/')
$marketIsolated = [IO.Path]::GetFullPath('D:/Game/tmp/departure-dock-scene-20261012').TrimEnd('\','/')
$marketOutput = 'D:/Game/Artifacts/MarketShopLayout20261014/validation'
$marketManifest = Get-Content -Raw -LiteralPath (Join-Path $marketOutput 'scene-production-before.json') | ConvertFrom-Json
$marketOriginals = @{}
foreach ($marketEntry in $marketManifest) { $marketOriginals[$marketEntry.path] = $marketEntry.sha256 }
$marketPaths = @(
    'Assets/_Project/Scenes/UIPreviews/UI_Market.unity'
)
$marketPaths += @(Get-ChildItem -LiteralPath (Join-Path $marketIsolated 'Assets/_Project/Presentation/EditablePreviewAssets') -Filter 'UI_Market*' -File |
    ForEach-Object { 'Assets/_Project/Presentation/EditablePreviewAssets/' + $_.Name })
$marketPrepared = @()
foreach ($marketPath in $marketPaths) {
    $marketSource = [IO.Path]::GetFullPath((Join-Path $marketIsolated $marketPath))
    $marketDestination = [IO.Path]::GetFullPath((Join-Path $marketProduction $marketPath))
    if (-not $marketSource.StartsWith($marketIsolated + '\',[StringComparison]::OrdinalIgnoreCase) -or
        -not $marketDestination.StartsWith($marketProduction + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Asset escaped allowed root.' }
    $marketSha = (Get-FileHash -LiteralPath $marketSource -Algorithm SHA256).Hash
    $marketBefore = $marketOriginals[$marketPath]
    if ($marketBefore) {
        if ((Get-FileHash -LiteralPath $marketDestination -Algorithm SHA256).Hash -ne $marketBefore) { throw "Production was edited concurrently: $marketPath" }
        if ($marketSha -eq $marketBefore) { continue }
    } elseif (Test-Path -LiteralPath $marketDestination) { throw "New asset collides: $marketPath" }
    $marketPrepared += [pscustomobject]@{ path=$marketPath; source=$marketSource; destination=$marketDestination; beforeSha256=$marketBefore; sha256=$marketSha }
}
$marketPrepared | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $marketOutput 'scene-copyback-plan.json') -Encoding utf8
Write-Output ('Prepared ' + $marketPrepared.Count + ' market-only changed files against original production hashes.')
if (-not $Apply) { return }
$marketBackup = Join-Path $marketOutput 'ProductionSceneBackup'
if (Test-Path -LiteralPath $marketBackup) { throw 'Copy-back backup already exists; inspect previous application.' }
foreach ($marketEntry in $marketPrepared) {
    if ($marketEntry.beforeSha256) {
        $marketCopy = Join-Path $marketBackup $marketEntry.path
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($marketCopy)) | Out-Null
        Copy-Item -LiteralPath $marketEntry.destination -Destination $marketCopy
    }
}
foreach ($marketEntry in $marketPrepared) {
    if ((Get-FileHash -LiteralPath $marketEntry.source -Algorithm SHA256).Hash -ne $marketEntry.sha256) { throw 'Isolated file changed during copy-back.' }
    if ($marketEntry.beforeSha256 -and (Get-FileHash -LiteralPath $marketEntry.destination -Algorithm SHA256).Hash -ne $marketEntry.beforeSha256) { throw 'Production changed during copy-back.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($marketEntry.destination)) | Out-Null
    Copy-Item -LiteralPath $marketEntry.source -Destination $marketEntry.destination -Force
    if ((Get-FileHash -LiteralPath $marketEntry.destination -Algorithm SHA256).Hash -ne $marketEntry.sha256) { throw 'Copy-back hash mismatch.' }
}
Write-Output ('Copied and verified ' + $marketPrepared.Count + ' files; originals backed up at ' + $marketBackup)
