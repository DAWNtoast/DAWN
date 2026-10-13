param(
    [string]$Workspace = 'D:\Game',
    [string]$EvidenceDirectory = 'D:\Game\Artifacts\NavigationMotionCleanup20261012\PreviewCleanup'
)

$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$sceneRoot = Join-Path $Workspace 'UnityClient\Assets\_Project\Scenes\UIPreviewsForMe'
$sceneRoot = [IO.Path]::GetFullPath($sceneRoot)
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
if (-not $evidenceRoot.StartsWith(([IO.Path]::GetFullPath($Workspace) + '\'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence directory must be inside the workspace.'
}
if (Test-Path -LiteralPath $evidenceRoot) { throw "Evidence directory already exists: $evidenceRoot" }

# A one-byte encoding keeps the original byte sequence (including BOM and line endings).
$byteEncoding = [Text.Encoding]::GetEncoding(28591)
$blockHeader = [regex]'(?m)^--- !u!(?<type>\d+) &(?<id>-?\d+)[^\r\n]*(?:\r?\n|$)'
$fileReference = [regex]'\{fileID: (?<id>-?\d+)\}'

function Read-Blocks([string]$Source) {
    $matches = $blockHeader.Matches($Source)
    $result = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new()
    for ($i = 0; $i -lt $matches.Count; $i++) {
        $match = $matches[$i]
        $end = if ($i + 1 -lt $matches.Count) { $matches[$i + 1].Index } else { $Source.Length }
        $id = $match.Groups['id'].Value
        if (-not $seen.Add($id)) { throw "Duplicate object ID: $id" }
        $result.Add([pscustomobject]@{
            Id = $id
            Type = $match.Groups['type'].Value
            Text = $Source.Substring($match.Index, $end - $match.Index)
        })
    }
    return ,$result
}

function Hash-Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$plans = [Collections.Generic.List[object]]::new()
foreach ($file in (Get-ChildItem -LiteralPath $sceneRoot -Filter '*.unity' -Recurse | Sort-Object FullName)) {
    $originalBytes = [IO.File]::ReadAllBytes($file.FullName)
    $source = $byteEncoding.GetString($originalBytes)
    if (-not [regex]::IsMatch($source, '(?m)^  m_Name: Ship Heading\r?$')) { continue }

    $blocks = Read-Blocks $source
    $byId = @{}
    foreach ($block in $blocks) { $byId[$block.Id] = $block }
    $headings = @($blocks | Where-Object { $_.Type -eq '1' -and [regex]::IsMatch($_.Text, '(?m)^  m_Name: Ship Heading\r?$') })
    if ($headings.Count -ne 1) { throw "Expected exactly one heading in $($file.FullName), found $($headings.Count)" }

    $heading = $headings[0]
    $componentIds = @([regex]::Matches($heading.Text, '(?m)^  - component: \{fileID: (-?\d+)\}\r?$') | ForEach-Object { $_.Groups[1].Value })
    if ($componentIds.Count -ne 3) { throw "Unexpected heading components in $($file.FullName)" }
    $removedIds = [Collections.Generic.HashSet[string]]::new()
    [void]$removedIds.Add($heading.Id)
    foreach ($id in $componentIds) {
        if (-not $byId.ContainsKey($id)) { throw "Missing component $id in $($file.FullName)" }
        if (-not [regex]::IsMatch($byId[$id].Text, "(?m)^  m_GameObject: \{fileID: $($heading.Id)\}\r?`$")) {
            throw "Component $id belongs to a different object in $($file.FullName)"
        }
        [void]$removedIds.Add($id)
    }
    $transforms = @($componentIds | Where-Object { $byId[$_].Type -eq '224' })
    $renderers = @($componentIds | Where-Object { $byId[$_].Type -eq '222' })
    $images = @($componentIds | Where-Object { $byId[$_].Type -eq '114' -and $byId[$_].Text.Contains('UnityEngine.UI::UnityEngine.UI.Image') })
    if ($transforms.Count -ne 1 -or $renderers.Count -ne 1 -or $images.Count -ne 1) {
        throw "Heading components are not exactly RectTransform, CanvasRenderer and Image in $($file.FullName)"
    }
    $transform = $byId[$transforms[0]]
    if (-not [regex]::IsMatch($transform.Text, '(?m)^  m_Children: \[\]\r?$')) {
        throw "Heading has children in $($file.FullName)"
    }
    $parentMatch = [regex]::Match($transform.Text, '(?m)^  m_Father: \{fileID: (-?\d+)\}\r?$')
    if (-not $parentMatch.Success -or -not $byId.ContainsKey($parentMatch.Groups[1].Value)) {
        throw "Heading parent is missing in $($file.FullName)"
    }
    $parent = $byId[$parentMatch.Groups[1].Value]
    if ($parent.Type -ne '224') { throw "Unexpected heading parent type in $($file.FullName)" }
    $childPattern = '(?m)^  - \{fileID: ' + [regex]::Escape($transform.Id) + '\}\r?\n'
    $childMatches = [regex]::Matches($parent.Text, $childPattern)
    if ($childMatches.Count -ne 1) { throw "Expected exactly one parent child reference in $($file.FullName)" }
    $childrenStart = $parent.Text.IndexOf('  m_Children:')
    $fatherStart = $parent.Text.IndexOf('  m_Father:')
    if ($childMatches[0].Index -le $childrenStart -or $childMatches[0].Index -ge $fatherStart) {
        throw "Parent reference is outside its child list in $($file.FullName)"
    }
    $updatedParent = [regex]::Replace($parent.Text, $childPattern, '')

    foreach ($block in $blocks) {
        if ($removedIds.Contains($block.Id)) { continue }
        $remaining = if ($block.Id -eq $parent.Id) { $updatedParent } else { $block.Text }
        foreach ($reference in $fileReference.Matches($remaining)) {
            if ($removedIds.Contains($reference.Groups['id'].Value)) {
                throw "Unexpected external reference from object $($block.Id) to heading in $($file.FullName)"
            }
        }
    }

    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append($source.Substring(0, $blockHeader.Match($source).Index))
    foreach ($block in $blocks) {
        if ($removedIds.Contains($block.Id)) { continue }
        [void]$builder.Append($(if ($block.Id -eq $parent.Id) { $updatedParent } else { $block.Text }))
    }
    $updated = $builder.ToString()
    $afterBlocks = Read-Blocks $updated
    if ($afterBlocks.Count -ne $blocks.Count - $removedIds.Count) { throw 'Unexpected object count after removal.' }
    foreach ($block in $afterBlocks) {
        $expected = if ($block.Id -eq $parent.Id) { $updatedParent } else { $byId[$block.Id].Text }
        if ($block.Text -cne $expected) { throw "Unexpected block mutation: $($block.Id)" }
    }

    $relativePath = $file.FullName.Substring($sceneRoot.Length + 1)
    $metaPath = $file.FullName + '.meta'
    if (-not (Test-Path -LiteralPath $metaPath)) { throw "Missing meta: $metaPath" }
    $metaBytes = [IO.File]::ReadAllBytes($metaPath)
    $updatedBytes = $byteEncoding.GetBytes($updated)
    $plans.Add([pscustomobject]@{
        Path = $file.FullName
        RelativePath = $relativePath
        OriginalBytes = $originalBytes
        UpdatedBytes = $updatedBytes
        MetaBytes = $metaBytes
        Record = [pscustomobject]@{
            scene = $relativePath.Replace('\', '/')
            removedGameObjectId = $heading.Id
            removedObjectIds = @($removedIds | Sort-Object)
            parentTransformId = $parent.Id
            originalObjectCount = $blocks.Count
            remainingObjectCount = $afterBlocks.Count
            originalSha256 = Hash-Bytes $originalBytes
            updatedSha256 = Hash-Bytes $updatedBytes
            metaSha256 = Hash-Bytes $metaBytes
            otherBlocksPreservedByteForByte = $true
            onlyParentChildReferenceRemoved = $true
            noChildObjectsOrUnexpectedExternalReferences = $true
        }
    })
}

if ($plans.Count -ne 21) { throw "Expected 21 saved previews before cleanup, found $($plans.Count). No scenes were written." }
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
foreach ($plan in $plans) {
    $backupPath = Join-Path (Join-Path $evidenceRoot 'before') $plan.RelativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backupPath)) | Out-Null
    [IO.File]::WriteAllBytes($backupPath, $plan.OriginalBytes)
    [IO.File]::WriteAllBytes(($backupPath + '.meta'), $plan.MetaBytes)
}

# Preflight every source again before committing any changes, to avoid replacing concurrent edits.
foreach ($plan in $plans) {
    if ((Hash-Bytes ([IO.File]::ReadAllBytes($plan.Path))) -ne $plan.Record.originalSha256) {
        throw "Scene changed during preflight: $($plan.Path). No scenes were written."
    }
}
foreach ($plan in $plans) { [IO.File]::WriteAllBytes($plan.Path, $plan.UpdatedBytes) }
foreach ($plan in $plans) {
    if ((Hash-Bytes ([IO.File]::ReadAllBytes($plan.Path))) -ne $plan.Record.updatedSha256) { throw "Written scene verification failed: $($plan.Path)" }
    if ((Hash-Bytes ([IO.File]::ReadAllBytes($plan.Path + '.meta'))) -ne $plan.Record.metaSha256) { throw "Meta changed: $($plan.Path)" }
}
$report = [pscustomobject]@{
    result = 'PASS'
    sceneCount = $plans.Count
    removedHeadingCount = $plans.Count
    removedYamlObjectCount = $plans.Count * 4
    method = 'Remove only Ship Heading GameObject and its three components, plus the single parent child reference. Preserve all other serialized bytes and every meta file.'
    scenes = @($plans | ForEach-Object { $_.Record })
}
$reportPath = Join-Path $evidenceRoot 'report.json'
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "PASS: removed $($plans.Count) headings / $($plans.Count * 4) YAML objects; every other block and meta preserved. Report: $reportPath"
