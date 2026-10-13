param([string]$Suffix = 'Verified')
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
if ($Suffix -notmatch '^[A-Za-z0-9_-]*$') { throw 'Suffix must be a simple identifier.' }
$navigationProject = 'D:\Game\tmp\shipyard-detail-clean-20261011'
$navigationSource = 'D:\Game\UnityClient'
$navigationArtifacts = 'D:\Game\Artifacts\NavigationJournal20261011'
$navigationReport = Get-Content -LiteralPath (Join-Path $navigationArtifacts ('scene-sync' + $Suffix + '.json')) -Raw | ConvertFrom-Json
$navigationCopies = @()
foreach ($navigationScene in $navigationReport.updated) {
    $navigationRelative = $navigationScene.path
    if ($navigationRelative -notmatch '^Assets/_Project/Scenes/UIPreviewsForMe/.+\.unity$') { throw 'Unexpected scene outside navigation previews.' }
    $navigationFrom = [IO.Path]::GetFullPath((Join-Path $navigationProject $navigationRelative))
    $navigationTo = [IO.Path]::GetFullPath((Join-Path $navigationSource $navigationRelative))
    $navigationOriginal = [IO.Path]::GetFullPath((Join-Path $navigationReport.backupRoot $navigationRelative))
    if (-not $navigationFrom.StartsWith($navigationProject + '\Assets\_Project\Scenes\UIPreviewsForMe\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $navigationTo.StartsWith($navigationSource + '\Assets\_Project\Scenes\UIPreviewsForMe\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Preview path escaped selected project.' }
    if ((Get-FileHash -LiteralPath $navigationTo).Hash -ne (Get-FileHash -LiteralPath $navigationOriginal).Hash) { throw "Production scene changed since validation sync: $navigationRelative" }
    if ((Get-FileHash -LiteralPath ($navigationFrom + '.meta')).Hash -ne (Get-FileHash -LiteralPath ($navigationTo + '.meta')).Hash) { throw "Preview identity changed: $navigationRelative" }
    $navigationCopies += [pscustomobject]@{ Path=$navigationRelative; From=$navigationFrom; To=$navigationTo }
}
foreach ($navigationCopy in $navigationCopies) {
    Copy-Item -LiteralPath $navigationCopy.From -Destination $navigationCopy.To -Force
}
[pscustomobject]@{ Result='PASS'; Scenes=$navigationCopies.Count; OriginalSourcesVerified=$true; MetadataPreserved=$true } |
    ConvertTo-Json | Tee-Object -FilePath (Join-Path $navigationArtifacts 'preview-copyback.json')
