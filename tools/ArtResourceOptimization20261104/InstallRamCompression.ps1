param(
    [Parameter(Mandatory = $true)][string]$Report,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($workspace -ne 'D:\Game') { throw 'This task installer is scoped to D:\Game.' }
$reportPath = (Resolve-Path -LiteralPath $Report).Path
if (-not $reportPath.StartsWith((Join-Path $workspace 'outputs\'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a preserved validation report under D:\Game\outputs.'
}
$evidence = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($evidence.scope -ne 'all' -or $evidence.alignment -ne 'pad' -or @($evidence.pages).Count -ne 16 -or -not $evidence.temporaryAssetsRemoved) {
    throw 'A completed all-16 candidate report with successful temporary asset cleanup is required.'
}
if ($evidence.minimumPsnrDb -lt 38 -or $evidence.maximumMeanAbsoluteChannelError -gt 2) {
    throw 'The report does not satisfy the agreed quality thresholds.'
}
$policyRelative = 'UnityClient/Assets/_Project/Editor/DesktopBackgroundCompressionPolicy.cs'
$policyPath = Join-Path $workspace $policyRelative
$postprocessorPath = Join-Path $workspace 'UnityClient/Assets/_Project/Editor/DesktopBackgroundCompressionPostprocessor.cs'
$catalogPath = Join-Path $workspace 'UnityClient/Assets/_Project/Presentation/Runtime/RamComicCatalog.cs'
$presentationPath = Join-Path $workspace 'UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayRamComicPresentation.cs'
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
if ((Hash $policyPath) -ne $evidence.policySourceSha256 -or (Hash $postprocessorPath) -ne $evidence.postprocessorSourceSha256) {
    throw 'The import policy or postprocessor changed since the validated candidate run; rerun validation.'
}
if ((Hash $catalogPath) -ne $evidence.catalogSourceSha256 -or (Hash $presentationPath) -ne $evidence.presentationSourceSha256) {
    throw 'The ram UV mapping changed since the validated candidate run; rerun validation.'
}
$expected = @(
    foreach ($sequence in 'ram_player_head_on', 'ram_player_broadside', 'ram_boss_head_on', 'ram_boss_broadside') {
        foreach ($page in 1..4) { 'BattleComics/HD/{0}_page{1:00}' -f $sequence, $page }
    }
)
if (@($evidence.pages.resource | Sort-Object -Unique).Count -ne 16 -or
    @(Compare-Object ($expected | Sort-Object) ($evidence.pages.resource | Sort-Object)).Count -ne 0) {
    throw 'The report must cover exactly the sixteen current ram comic resources.'
}
$pending = @()
foreach ($page in $evidence.pages) {
    if (-not $page.qualityPassed -or $page.error -or -not $page.sourcePngUnchanged -or -not $page.sourceMetaUnchanged -or
        $page.candidateFormat -ne 'BC7' -or $page.candidateReadable -or $page.baselineReadable -or
        $page.sourceWidth -ne 3344 -or $page.sourceHeight -ne 1882 -or $page.importedWidth -ne 3344 -or $page.importedHeight -ne 1884 -or
        $page.originalToCandidate.psnrDb -lt 38 -or $page.originalToCandidate.meanAbsoluteChannelError -gt 2 -or
        $page.importedBaselineToCandidate.psnrDb -lt 38 -or $page.importedBaselineToCandidate.meanAbsoluteChannelError -gt 2 -or
        $page.candidateRuntimeBytes -gt $page.baselineRuntimeBytes * 0.5) {
        throw "Candidate result is not acceptable: $($page.resource)"
    }
    $relative = 'UnityClient/Assets/_Project/Resources/' + $page.resource + '.png'
    $png = Join-Path $workspace $relative
    $meta = $png + '.meta'
    if ((Hash $png) -ne $page.sourceSha256 -or (Hash $meta) -ne $page.sourceMetaSha256) {
        throw "Production source PNG/meta changed since the validated copy: $($page.resource)"
    }
    $old = [IO.File]::ReadAllText($meta)
    $pattern = '(?ms)^  - serializedVersion: 4\r?\n    buildTarget: Standalone\r?\n.*?(?=^  - serializedVersion:|^  spriteSheet:)'
    $standaloneBlocks = [regex]::Matches($old, $pattern)
    if ($standaloneBlocks.Count -ne 1) { throw "Expected one Standalone metadata block: $meta" }
    $block = $standaloneBlocks[0].Value
    if ($block -notmatch '(?m)^    maxTextureSize: 4096\r?$' -or $old -notmatch '(?m)^  isReadable: 0\r?$' -or
        $old -notmatch '(?m)^    enableMipMap: 0\r?$' -or $old -notmatch '(?m)^  nPOTScale: 0\r?$') {
        throw "Unexpected protected import settings: $meta"
    }
    foreach ($setting in @(@('textureFormat', '25'), @('textureCompression', '2'), @('compressionQuality', '100'), @('overridden', '1'))) {
        $line = '(?m)^(    ' + $setting[0] + ': )-?\d+'
        if ([regex]::Matches($block, $line).Count -ne 1) { throw "Missing metadata setting: $($setting[0])" }
        $block = [regex]::Replace($block, $line, '${1}' + $setting[1])
    }
    $new = $old.Substring(0, $standaloneBlocks[0].Index) + $block + $old.Substring($standaloneBlocks[0].Index + $standaloneBlocks[0].Length)
    $pending += [pscustomobject]@{ Path = $meta; Relative = $relative + '.meta'; Before = $old; After = $new; Png = $png; PngSha256 = $page.sourceSha256 }
}
$policyBefore = [IO.File]::ReadAllText($policyPath)
$marker = '            return result.ToArray();'
if ([regex]::Matches($policyBefore, [regex]::Escape($marker)).Count -ne 1) { throw 'Cannot identify the unique explicit whitelist insertion point.' }
if ($policyBefore.Substring(0, $policyBefore.IndexOf($marker)).Contains('BattleComics/HD/')) { throw 'Ram comics are already present in the whitelist; review the installed state.' }
$addition = @(
    '            // Sixteen opaque ram pages validated at original display size against source PNGs.'
    '            foreach (string sequence in new[] { "ram_player_head_on", "ram_player_broadside", "ram_boss_head_on", "ram_boss_broadside" })'
    '                for (int page = 1; page <= 4; page++)'
    '                    result.Add(Root + "BattleComics/HD/" + sequence + "_page" + page.ToString("00") + ".png");'
) -join [Environment]::NewLine
$policyAfter = $policyBefore.Replace($marker, $addition + [Environment]::NewLine + $marker)
$pending += [pscustomobject]@{ Path = $policyPath; Relative = $policyRelative; Before = $policyBefore; After = $policyAfter; Png = $null; PngSha256 = $null }
$receiptPath = Join-Path $workspace 'outputs/art-resource-optimization-20261104/installed-ram-compression.json'
if (Test-Path -LiteralPath $receiptPath) { throw 'An installation receipt already exists; do not overwrite historical evidence.' }
Write-Output "Validated 16/16 results. Plan: 16 Standalone overrides and 16 explicit whitelist entries; source PNGs, authored settings, runtime paging unchanged."
if (-not $Apply) { Write-Output 'Preview only. After reviewing the preserved GPU A/B images, pass -Apply to install.'; exit 0 }
$backupRoot = Join-Path $workspace 'outputs/art-resource-optimization-20261104/source-before/10-ram-compression'
foreach ($change in $pending) {
    $backup = Join-Path $backupRoot $change.Relative
    if (Test-Path -LiteralPath $backup) {
        if ((Hash $backup) -ne (Hash $change.Path)) { throw "Existing backup differs; preserved without overwrite: $backup" }
    } else {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
        [IO.File]::Copy($change.Path, $backup, $false)
    }
}
$utf8 = [Text.UTF8Encoding]::new($false)
$written = @()
$temporaryReceipt = $receiptPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    foreach ($change in $pending) {
        $written += $change
        [IO.File]::WriteAllText($change.Path, $change.After, $utf8)
        if ($change.Png -and (Hash $change.Png) -ne $change.PngSha256) { throw "Source PNG changed: $($change.Png)" }
    }
    $receipt = [ordered]@{
        installedUtc = [DateTime]::UtcNow.ToString('O')
        candidateReport = $reportPath
        candidateReportSha256 = Hash $reportPath
        note = 'Installed desktop BC7 metadata and explicit policy entries after candidate quality validation. PNG bytes are unchanged. Imported dimensions are 3344x1884 from 3344x1882 sources, with repeated edge padding excluded by display UV. Production-path import and gameplay screenshots still require verification; no runtime paging or load ordering changed.'
        files = @($pending | ForEach-Object { [ordered]@{ path = $_.Relative; installedSha256 = Hash $_.Path; pngSha256 = $_.PngSha256 } })
    }
    [IO.File]::WriteAllText($temporaryReceipt, ($receipt | ConvertTo-Json -Depth 8), $utf8)
    [IO.File]::Move($temporaryReceipt, $receiptPath)
} catch {
    foreach ($change in $written) { [IO.File]::Copy((Join-Path $backupRoot $change.Relative), $change.Path, $true) }
    throw
} finally {
    if ([IO.File]::Exists($temporaryReceipt)) { [IO.File]::Delete($temporaryReceipt) }
}
Write-Output "Installed validated settings and preserved backups. Receipt: $receiptPath"
