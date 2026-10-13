$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$shipyardIsolated = 'D:\Game\tmp\shipyard-detail-clean-20261011'
$shipyardProduction = 'D:\Game\UnityClient'
$shipyardArtifacts = 'D:\Game\Artifacts\ShipyardDetailClean20261011'
$shipyardFiles = @(
    Get-Content -LiteralPath (Join-Path $shipyardArtifacts 'synced-scenes.txt')
    Get-Content -LiteralPath (Join-Path $shipyardArtifacts 'preview-dependencies.txt')
) | Where-Object { $_ } | Sort-Object -Unique
$shipyardCopies = @()
foreach ($shipyardRelative in $shipyardFiles) {
    if ($shipyardRelative -notmatch '^Assets/_Project/(Scenes/UIPreviewsForMe/|Presentation/EditablePreviewAssets/)') { throw "Unexpected preview file: $shipyardRelative" }
    $shipyardFrom = [IO.Path]::GetFullPath((Join-Path $shipyardIsolated $shipyardRelative))
    $shipyardTo = [IO.Path]::GetFullPath((Join-Path $shipyardProduction $shipyardRelative))
    if (-not $shipyardFrom.StartsWith($shipyardIsolated + '\Assets\_Project\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $shipyardTo.StartsWith($shipyardProduction + '\Assets\_Project\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Preview path escaped selected project.' }
    if (-not (Test-Path -LiteralPath $shipyardFrom)) { throw "Missing preview dependency: $shipyardFrom" }
    if ((Test-Path -LiteralPath ($shipyardTo + '.meta')) -and (Test-Path -LiteralPath ($shipyardFrom + '.meta'))) {
        $shipyardSourceGuid = [regex]::Match([IO.File]::ReadAllText($shipyardFrom + '.meta'), '(?m)^guid: ([a-fA-F0-9]{32})\r?$').Groups[1].Value
        $shipyardDestinationGuid = [regex]::Match([IO.File]::ReadAllText($shipyardTo + '.meta'), '(?m)^guid: ([a-fA-F0-9]{32})\r?$').Groups[1].Value
        if (-not $shipyardSourceGuid -or $shipyardSourceGuid -ne $shipyardDestinationGuid) { throw "Existing preview GUID differs: $shipyardRelative" }
    }
    $shipyardCopies += [pscustomobject]@{ Relative=$shipyardRelative; From=$shipyardFrom; To=$shipyardTo }
}
$shipyardChanged = @()
foreach ($shipyardCopy in $shipyardCopies) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($shipyardCopy.To)) | Out-Null
    foreach ($shipyardExtension in @('', '.meta')) {
        $shipyardFrom = $shipyardCopy.From + $shipyardExtension
        $shipyardTo = $shipyardCopy.To + $shipyardExtension
        if (-not (Test-Path -LiteralPath $shipyardFrom)) { continue }
        if ((Test-Path -LiteralPath $shipyardTo) -and (Get-FileHash -LiteralPath $shipyardFrom).Hash -eq (Get-FileHash -LiteralPath $shipyardTo).Hash) { continue }
        Copy-Item -LiteralPath $shipyardFrom -Destination $shipyardTo -Force
        $shipyardChanged += $shipyardCopy.Relative + $shipyardExtension
    }
}
$shipyardChanged | Set-Content -LiteralPath (Join-Path $shipyardArtifacts 'preview-copyback.txt') -Encoding utf8
[pscustomobject]@{ Result='PASS'; SelectedFiles=$shipyardFiles.Count; CopiedFiles=$shipyardChanged.Count; ExistingGuidsPreserved=$true } | ConvertTo-Json |
    Tee-Object -FilePath (Join-Path $shipyardArtifacts 'preview-copyback.json')
