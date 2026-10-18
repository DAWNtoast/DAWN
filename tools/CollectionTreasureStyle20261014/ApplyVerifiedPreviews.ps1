$ErrorActionPreference = 'Stop'
$styleSource = 'D:\Game\UnityClient'
$styleProject = 'D:\Game\tmp\departure-dock-scene-20261012'
$styleOutput = 'D:\Game\Artifacts\CollectionTreasureStyle20261014\validation'
$styleManifest = Get-Content -Raw -LiteralPath (Join-Path $styleOutput 'scene-production-before.json') | ConvertFrom-Json
$styleKnown = @{}
foreach ($styleEntry in $styleManifest) {
    $styleKnown[$styleEntry.path] = $styleEntry.sha256
    $styleCurrent = Join-Path $styleSource $styleEntry.path
    if (-not (Test-Path -LiteralPath $styleCurrent) -or (Get-FileHash -LiteralPath $styleCurrent -Algorithm SHA256).Hash -ne $styleEntry.sha256) {
        throw "Production file changed during validation: $($styleEntry.path)"
    }
}
$styleLog = Get-Content -Raw -LiteralPath (Join-Path $styleOutput 'scenes01\PreviewSync.log')
if (-not $styleLog.Contains('COLLECTION_TREASURE_PREVIEWS_OK')) { throw 'Scene synchronization did not finish.' }
$stylePaths = @('Assets/_Project/Scenes/UIPreviews/UI_Warehouse.unity','Assets/_Project/Scenes/UIPreviews/UI_TreasureMap.unity')
foreach ($stylePrefix in @('UI_Warehouse','UI_TreasureMap')) {
    $stylePaths += Get-ChildItem -LiteralPath (Join-Path $styleProject 'Assets/_Project/Presentation/EditablePreviewAssets') -Filter ($stylePrefix + '*') -File |
        ForEach-Object { 'Assets/_Project/Presentation/EditablePreviewAssets/' + $_.Name }
}
$styleChanges = @()
foreach ($styleRelative in $stylePaths) {
    if ($styleRelative.Contains('UIPreviewsForMe')) { throw 'Retired preview destination is forbidden.' }
    $styleFrom = Join-Path $styleProject $styleRelative
    $styleTo = Join-Path $styleSource $styleRelative
    $styleHash = (Get-FileHash -LiteralPath $styleFrom -Algorithm SHA256).Hash
    if (Test-Path -LiteralPath $styleTo) {
        if ((Get-FileHash -LiteralPath $styleTo -Algorithm SHA256).Hash -eq $styleHash) { continue }
        if (-not $styleKnown.ContainsKey($styleRelative)) { throw "Untracked destination appeared during validation: $styleRelative" }
    }
    $styleChanges += [pscustomobject]@{path=$styleRelative;sha256=$styleHash}
}
$styleBackup = Join-Path $styleOutput 'ProductionSceneBackup'
if (Test-Path -LiteralPath $styleBackup) { throw 'Preview copy has already been attempted; inspect backup first.' }
[IO.Directory]::CreateDirectory($styleBackup) | Out-Null
foreach ($styleChange in $styleChanges) {
    $styleFrom = Join-Path $styleProject $styleChange.path
    $styleTo = Join-Path $styleSource $styleChange.path
    if (Test-Path -LiteralPath $styleTo) {
        $styleSaved = Join-Path $styleBackup $styleChange.path
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($styleSaved)) | Out-Null
        Copy-Item -LiteralPath $styleTo -Destination $styleSaved
    }
    Copy-Item -LiteralPath $styleFrom -Destination $styleTo -Force
    if ((Get-FileHash -LiteralPath $styleTo -Algorithm SHA256).Hash -ne $styleChange.sha256) { throw 'Post-copy hash mismatch.' }
}
$styleChanges | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $styleOutput 'applied-previews.json') -Encoding utf8
Write-Output ("Applied and verified " + $styleChanges.Count + " changed scene/asset files; originals backed up.")
