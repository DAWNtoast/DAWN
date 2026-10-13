param([string]$ArtifactRoot = 'D:\Game\Artifacts\NavigationMapFill20261013')
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$taskSource = 'D:\Game\UnityClient'
$taskScratch = 'D:\Game\tmp\navigation-map-label-clean-20261012'
$taskArtifacts = $ArtifactRoot
$taskReport = Get-Content -LiteralPath (Join-Path $taskArtifacts 'scene-sync.json') -Raw | ConvertFrom-Json
$taskBlocks = [regex]::new('(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)')
$taskPatches = @()
foreach ($taskScene in $taskReport.scenes) {
    $taskPath = Join-Path $taskSource $taskScene.path
    if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskScene.beforeSha256) {
        throw ('Scene changed since validation: ' + $taskScene.path)
    }
    $taskBefore = [IO.File]::ReadAllText($taskPath)
    $taskAfter = [IO.File]::ReadAllText((Join-Path $taskScratch $taskScene.path))
    $taskNames = @{}
    $taskUpdatedBlocks = @{}
    foreach ($taskMatch in $taskBlocks.Matches($taskAfter)) { $taskUpdatedBlocks[$taskMatch.Groups[2].Value] = $taskMatch.Value }
    foreach ($taskMatch in $taskBlocks.Matches($taskBefore)) {
        if ($taskMatch.Groups[1].Value -eq '1') {
            $taskNames[$taskMatch.Groups[2].Value] = [regex]::Match($taskMatch.Value, '(?m)^  m_Name: (.+)\r?$').Groups[1].Value.TrimEnd("`r")
        }
    }
    $taskChanged = [Collections.Generic.List[string]]::new()
    $taskPatched = $taskBlocks.Replace($taskBefore, [Text.RegularExpressions.MatchEvaluator]{ param($taskMatch)
        $taskBlock = $taskMatch.Value
        if ($taskMatch.Groups[1].Value -ne '224') { return $taskBlock }
        $taskObject = [regex]::Match($taskBlock, 'm_GameObject: \{fileID: (-?\d+)\}').Groups[1].Value
        $taskName = $taskNames[$taskObject]
        if ($taskName -notmatch '^(Map Layer|Procedural Sea Chart Frame|Player Ship|Event Node .+|Departure Port( Halo)?|Destination Port( Halo)?|Course Target|Direct Course Target Halo|Navigation Map Title Strip|Navigation Map Title Compass|Map Title|Voyage Supply Endurance)$') { return $taskBlock }
        $taskFields = @('m_LocalScale')
        if ($taskName -in @('Procedural Sea Chart Frame','Map Layer')) { $taskFields += @('m_LocalPosition','m_AnchoredPosition','m_SizeDelta') }
        if ($taskName -match '^(Navigation Map Title Strip|Navigation Map Title Compass|Map Title|Voyage Supply Endurance)$') { $taskFields += @('m_LocalPosition','m_AnchoredPosition','m_SizeDelta') }
        foreach ($taskField in $taskFields) {
            $taskFieldPattern = [regex]::new('(?m)^  ' + $taskField + ': [^\r\n]+')
            $taskFieldValue = $taskFieldPattern.Match($taskUpdatedBlocks[$taskMatch.Groups[2].Value]).Value
            if (-not $taskFieldValue -or -not $taskFieldPattern.IsMatch($taskBlock)) { throw 'Missing map geometry field.' }
            $taskBlock = $taskFieldPattern.Replace($taskBlock, [Text.RegularExpressions.MatchEvaluator]{ param($taskFieldMatch) $taskFieldValue })
        }
        if ($taskBlock -ne $taskMatch.Value) { $taskChanged.Add($taskName) }
        return $taskBlock
    })
    $taskPatches += [pscustomobject]@{path=$taskScene.path; absolute=$taskPath; beforeSha256=$taskScene.beforeSha256; text=$taskPatched; changedObjects=$taskChanged.ToArray()}
}
# Publish only map geometry, avoiding unrelated normalization of every UI transform.
foreach ($taskPatch in $taskPatches) {
    if ((Get-FileHash -LiteralPath $taskPatch.absolute -Algorithm SHA256).Hash -ne $taskPatch.beforeSha256) { throw 'Scene changed before write.' }
    if ($taskPatch.changedObjects.Count -gt 0) { [IO.File]::WriteAllText($taskPatch.absolute, $taskPatch.text, [Text.UTF8Encoding]::new($false)) }
}
$taskPatches | ForEach-Object { [pscustomobject]@{path=$_.path; beforeSha256=$_.beforeSha256; afterSha256=(Get-FileHash -LiteralPath $_.absolute -Algorithm SHA256).Hash; changedObjects=$_.changedObjects} } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskArtifacts 'scene-map-applied.json') -Encoding utf8
Write-Output ('Updated map geometry in ' + @($taskPatches | Where-Object { $_.changedObjects.Count -gt 0 }).Count + ' saved previews.')
