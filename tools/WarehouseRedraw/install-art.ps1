param([Parameter(Mandatory=$true)][string]$MapPath)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath('D:\Game')
$taskOutput = Join-Path $taskRoot 'Artifacts\WarehouseRedraw20261012'
$taskResource = Join-Path $taskRoot 'UnityClient\Assets\_Project\Resources\PortFacilities\Regional\Warehouse'
$taskSources = @(Get-Content -LiteralPath $MapPath -Raw | ConvertFrom-Json)
$taskSlugs = @('broken_anchor','white_shell','bronze_bell','salt_crown','twilight_tide','gray_gull')
$taskEncoding = [Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Path $taskOutput,$taskResource,(Join-Path $taskOutput 'generated') -Force | Out-Null
if (-not (Test-Path -LiteralPath ($taskResource + '.meta'))) {
    $taskFolderGuid = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText(($taskResource + '.meta'), "fileFormatVersion: 2`nguid: $taskFolderGuid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n", $taskEncoding)
}
$taskTemplate = [IO.File]::ReadAllText((Join-Path $taskRoot 'UnityClient\Assets\_Project\Resources\PortFacilities\warehouse_background.png.meta'))
$taskRecords = @()
foreach ($taskEntry in $taskSources) {
    if ($taskEntry.slug -notin $taskSlugs) { throw 'Unknown warehouse port slug.' }
    $taskSourcePath = [IO.Path]::GetFullPath($taskEntry.source)
    if (-not (Test-Path -LiteralPath $taskSourcePath -PathType Leaf)) { throw "Missing generated image: $taskSourcePath" }
    $taskFilename = 'warehouse_' + $taskEntry.slug + '_v1.png'
    $taskDestination = Join-Path $taskResource $taskFilename
    $taskSourceHash = (Get-FileHash -LiteralPath $taskSourcePath -Algorithm SHA256).Hash
    if ((Test-Path -LiteralPath $taskDestination) -and ((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash -ne $taskSourceHash)) {
        throw "Refusing to overwrite a differing installed image: $taskDestination"
    }
    Copy-Item -LiteralPath $taskSourcePath -Destination $taskDestination
    Copy-Item -LiteralPath $taskSourcePath -Destination (Join-Path (Join-Path $taskOutput 'generated') $taskFilename)
    if (-not (Test-Path -LiteralPath ($taskDestination + '.meta'))) {
        $taskGuid = [Guid]::NewGuid().ToString('N')
        $taskMeta = $taskTemplate -replace '(?m)^guid:.*$', ('guid: ' + $taskGuid)
        $taskMeta = $taskMeta -replace 'maxTextureSize: 2048','maxTextureSize: 4096'
        [IO.File]::WriteAllText(($taskDestination + '.meta'), $taskMeta, $taskEncoding)
    }
    $taskRecords += [PSCustomObject]@{slug=$taskEntry.slug; source=$taskSourcePath; installed=$taskDestination; sha256=$taskSourceHash; bytes=(Get-Item -LiteralPath $taskDestination).Length}
}
$taskRecords | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'installed-art.json') -Encoding utf8
$taskRecords | Select-Object slug,bytes,installed
