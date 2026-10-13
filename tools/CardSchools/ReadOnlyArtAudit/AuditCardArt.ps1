param([switch]$Prepare, [switch]$IncludeLegacy, [string[]]$ExcludeArtId = @(), [string[]]$ExcludeLegacyCardId = @())
$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$auditAssets = Join-Path $auditWorkspace 'UnityClient/Assets'
$auditResourceRoot = Join-Path $auditAssets '_Project/Resources'
$auditArtRoot = Join-Path $auditResourceRoot 'CardSchools/Art'
$auditArtifacts = Join-Path $auditWorkspace 'Artifacts/CardSchools20261011'
$auditOutput = Join-Path $auditArtifacts 'ArtAudit'
$auditBuild = Join-Path $auditWorkspace 'tmp/CardSchoolArtAudit20261011'
$auditUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditBaselinePath = Join-Path $auditOutput 'resource-reference-baseline.json'
$auditLedgerPath = Join-Path $auditArtifacts 'generated-art-western-v2.json'
$auditLegacyLedgerPath = Join-Path $auditArtifacts 'generated-legacy-art-western.json'
$auditWesternRoot = Join-Path $auditResourceRoot 'CardArt/Western'
New-Item -ItemType Directory -Path $auditOutput,$auditBuild -Force | Out-Null

function FileHash([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function MetaGuid([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $text = Get-Content -LiteralPath $Path -Raw
    $match = [regex]::Match($text, '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$')
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value.ToLowerInvariant()
}
function ImportPolicy([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $meta = Get-Content -LiteralPath $Path -Raw
    $standalone = [regex]::Match($meta, '(?ms)^\s+buildTarget:\s*Standalone\s*$.*?(?=^\s+- serializedVersion:|^\s{2}spriteSheet:|\z)').Value
    return $meta -match '(?m)^\s{2}maxTextureSize:\s*512\s*$' -and
        $meta -match '(?m)^\s{4}enableMipMap:\s*0\s*$' -and
        $standalone -match '(?m)^\s+maxTextureSize:\s*512\s*$' -and
        $standalone -match '(?m)^\s+textureFormat:\s*25\s*$' -and
        $standalone -match '(?m)^\s+overridden:\s*1\s*$'
}
function PngInfo([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $bytes = [byte[]]::new(33)
        if ($stream.Read($bytes, 0, 33) -ne 33) { throw 'PNG header is truncated.' }
        $signature = [BitConverter]::ToString($bytes[0..7])
        if ($signature -ne '89-50-4E-47-0D-0A-1A-0A' -or [Text.Encoding]::ASCII.GetString($bytes, 12, 4) -ne 'IHDR') {
            throw 'File is not a PNG with an IHDR header.'
        }
        $width = [uint32]$bytes[16] * 16777216 + [uint32]$bytes[17] * 65536 + [uint32]$bytes[18] * 256 + $bytes[19]
        $height = [uint32]$bytes[20] * 16777216 + [uint32]$bytes[21] * 65536 + [uint32]$bytes[22] * 256 + $bytes[23]
        if ($width -le 0 -or $height -le 0) { throw 'PNG has invalid dimensions.' }
        return [pscustomobject]@{ width=$width; height=$height; bitDepth=$bytes[24]; colorType=$bytes[25]; byteLength=$stream.Length }
    } finally { $stream.Dispose() }
}
function RuntimeSnapshot {
    $coreRoot = Join-Path $auditAssets '_Project/Core/Runtime'
    $sources = @(Get-ChildItem -LiteralPath $coreRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
    $sources += Join-Path $PSScriptRoot 'RuntimeCardArtSnapshot.cs'
    $exe = Join-Path $auditBuild 'RuntimeCardArtSnapshot.exe'
    $compileArguments = @((Join-Path $auditUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$exe")
    foreach ($assembly in @('mscorlib.dll','System.dll','System.Core.dll')) {
        $compileArguments += '-r:' + (Join-Path $auditUnity ('MonoBleedingEdge/lib/mono/4.7.2-api/' + $assembly))
    }
    $compileArguments += $sources
    & (Join-Path $auditUnity 'NetCoreRuntime/dotnet.exe') @compileArguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Read-only Core art snapshot compilation failed.' }
    $snapshotPath = Join-Path $auditOutput 'runtime-card-art-snapshot.json'
    & (Join-Path $auditUnity 'MonoBleedingEdge/bin/mono.exe') $exe $snapshotPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Read-only Core art snapshot execution failed.' }
    return @(Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json)
}
function LegacyContract {
    $path = Join-Path $auditAssets '_Project/Presentation/Runtime/LegacyDemoArtCatalog.cs'
    $text = Get-Content -LiteralPath $path -Raw
    $block = [regex]::Match($text, 'cardCells\s*=\s*new Dictionary<string, Vector2Int>\s*\{(?<body>[\s\S]*?)\};')
    $cells = @([regex]::Matches($block.Groups['body'].Value, '\{\s*"(?<id>[^"]+)"\s*,\s*new Vector2Int\((?<x>\d+),\s*(?<y>\d+)\)\s*\}') |
        ForEach-Object { [pscustomobject]@{ artId=$_.Groups['id'].Value; x=[int]$_.Groups['x'].Value; y=[int]$_.Groups['y'].Value } } | Sort-Object artId)
    $atlas = [regex]::Match($text, 'Texture2D CardSheet\s*=>\s*Load\("(?<key>[^"]+)"\)')
    $prefix = [regex]::Match($text, 'resourceLoader\("(?<prefix>[^"]+)"\s*\+\s*name\)')
    return [pscustomobject]@{ atlasKey=($prefix.Groups['prefix'].Value + $atlas.Groups['key'].Value); cells=$cells; source=$path }
}
function ResourceIndex {
    $index = @{}
    foreach ($file in (Get-ChildItem -LiteralPath $auditAssets -Recurse -File)) {
        if ($file.Extension -notin @('.png','.jpg','.jpeg','.tga','.psd')) { continue }
        $path = $file.FullName.Replace('\','/')
        $at = $path.LastIndexOf('/Resources/', [StringComparison]::OrdinalIgnoreCase)
        if ($at -lt 0) { continue }
        $key = $path.Substring($at + 11)
        $key = $key.Substring(0, $key.Length - $file.Extension.Length)
        if (-not $index.ContainsKey($key)) { $index[$key] = @() }
        $index[$key] += $file.FullName
    }
    return $index
}
function GuidIndex {
    $index = @{}
    foreach ($file in (Get-ChildItem -LiteralPath $auditAssets -Recurse -File -Filter '*.meta')) {
        $guid = MetaGuid $file.FullName
        if ($null -eq $guid) { continue }
        if (-not $index.ContainsKey($guid)) { $index[$guid] = @() }
        $index[$guid] += $file.FullName
    }
    return $index
}

$auditExpected = @()
$auditExpected += @(Get-Content -LiteralPath (Join-Path $auditArtifacts 'art-manifest.json') -Raw | ConvertFrom-Json)
$auditExpected += @(Get-Content -LiteralPath (Join-Path $auditArtifacts 'neutral-art-manifest.json') -Raw | ConvertFrom-Json)
$auditExpectedIds = @($auditExpected | ForEach-Object artId | Sort-Object)
foreach ($excluded in $ExcludeArtId) {
    if ($excluded -notin $auditExpectedIds) { throw ('Unknown excluded art identity: ' + $excluded) }
}
$auditPartial = $ExcludeArtId.Count -gt 0 -or $ExcludeLegacyCardId.Count -gt 0
$auditReportStem = if ($IncludeLegacy) {
    if ($auditPartial) { 'art-integration-all-cards-partial' } else { 'art-integration-all-cards-audit' }
} else { if ($auditPartial) { 'art-integration-partial-before-final-png' } else { 'art-integration-audit' } }
$auditRuntimeCards = @(RuntimeSnapshot)
$auditActiveCards = @($auditRuntimeCards | Where-Object allowedStages -gt 0)
$auditLegacyCards = @($auditActiveCards | Where-Object { -not $_.artId.StartsWith('card_school_') })
$auditLegacyIds = @($auditLegacyCards.id | Sort-Object)
if ($ExcludeLegacyCardId.Count -gt 0 -and -not $IncludeLegacy) { throw 'ExcludeLegacyCardId requires IncludeLegacy.' }
foreach ($excluded in $ExcludeLegacyCardId) {
    if ($excluded -notin $auditLegacyIds) { throw ('Unknown excluded legacy CardId: ' + $excluded) }
}
$auditLegacy = LegacyContract
$auditResourceIndex = ResourceIndex
$auditGuidIndex = GuidIndex
$auditDuplicates = @($auditGuidIndex.Keys | Where-Object { $auditGuidIndex[$_].Count -gt 1 } | Sort-Object |
    ForEach-Object { [pscustomobject]@{ guid=$_; paths=$auditGuidIndex[$_] } })

if ($Prepare) {
    if (Test-Path -LiteralPath $auditBaselinePath) {
        Write-Output ('Read-only baseline already exists; preserved: ' + $auditBaselinePath)
        exit 0
    }
    $oldResources = @()
    if ($auditResourceIndex.ContainsKey($auditLegacy.atlasKey)) {
        foreach ($path in $auditResourceIndex[$auditLegacy.atlasKey]) {
            $oldResources += [pscustomobject]@{ role='legacy-card-atlas'; key=$auditLegacy.atlasKey; path=$path; sha256=(FileHash $path); guid=(MetaGuid ($path + '.meta')) }
        }
    }
    foreach ($school in @('neutral','fire','arcane','curse','tide','thunder','blood')) {
        $key = 'CardSchools/Frames/frame-' + $school + '-v1'
        if ($auditResourceIndex.ContainsKey($key)) {
            foreach ($path in $auditResourceIndex[$key]) {
                $oldResources += [pscustomobject]@{ role='confirmed-card-frame'; key=$key; path=$path; sha256=(FileHash $path); guid=(MetaGuid ($path + '.meta')) }
            }
        }
    }
    $artGuids = @($auditExpectedIds | ForEach-Object {
        $path = Join-Path $auditArtRoot ($_ + '.png')
        [pscustomobject]@{ artId=$_; guid=(MetaGuid ($path + '.meta')) }
    })
    $baseline = [pscustomobject]@{
        utc=[DateTime]::UtcNow.ToString('o')
        mode='Read-only pre-final-art baseline; current production Core compiled with Roslyn/Mono, no Unity or PlayerPrefs.'
        expectedArtIds=$auditExpectedIds
        runtimeCards=$auditRuntimeCards
        oldResources=$oldResources
        legacyContract=$auditLegacy
        illustrationGuids=$artGuids
        guidDuplicates=$auditDuplicates
    }
    $baseline | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $auditBaselinePath -Encoding utf8
    $selected = @(Get-Content -LiteralPath $auditLedgerPath -Raw | ConvertFrom-Json)
    [pscustomobject]@{
        status='PREPARED_NOT_FINAL'; expected=136; selectedWesternArt=$selected.Count
        runtimeCards=$auditRuntimeCards.Count; activeLegacyCardReferences=@($auditRuntimeCards | Where-Object { $_.allowedStages -gt 0 -and -not $_.artId.StartsWith('card_school_') }).Count
        preservedAtlasAndFrames=$oldResources.Count; guidGroups=$auditGuidIndex.Count; existingGuidDuplicateGroups=$auditDuplicates.Count
        baseline=$auditBaselinePath; limitation='Final source/target hash audit will run only after the parent confirms all136 selected assets are complete.'
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $auditOutput 'prepared.json') -Encoding utf8
    Write-Output ('Prepared only: expected136, selected' + $selected.Count + ', runtime' + $auditRuntimeCards.Count + ', atlas/frames' + $oldResources.Count)
    exit 0
}

if (-not (Test-Path -LiteralPath $auditBaselinePath -PathType Leaf)) { throw 'Prepare the baseline before the final audit.' }
$auditBaseline = Get-Content -LiteralPath $auditBaselinePath -Raw | ConvertFrom-Json
$auditLedger = @(Get-Content -LiteralPath $auditLedgerPath -Raw | ConvertFrom-Json)
$auditChecks = [Collections.Generic.List[object]]::new()
$auditAssetResults = [Collections.Generic.List[object]]::new()
function Check([string]$Category, [string]$Identity, [bool]$Passed, [string]$Detail) {
    $auditChecks.Add([pscustomobject]@{ category=$Category; identity=$Identity; status=$(if ($Passed) {'PASS'} else {'FAIL'}); detail=$Detail })
}
Check 'manifest' 'expected-count' ($auditExpected.Count -eq 136) ('Expected definitions=' + $auditExpected.Count)
Check 'manifest' 'expected-unique' (@($auditExpectedIds | Select-Object -Unique).Count -eq 136) '132 school + four new neutral cook illustrations.'
Check 'manifest' 'selected-count' ($auditLedger.Count -eq 136) ('Selected Western rows=' + $auditLedger.Count)
Check 'manifest' 'selected-unique' (@($auditLedger | Group-Object artId | Where-Object Count -gt 1).Count -eq 0) 'Each ArtId has one selected original.'
$auditSelectedIds = @($auditLedger | ForEach-Object artId | Sort-Object)
Check 'manifest' 'exact-art-set' (@(Compare-Object $auditExpectedIds $auditSelectedIds).Count -eq 0) 'Selected set equals current132+4 design art definitions.'
$auditById = @{}
foreach ($row in $auditLedger) { $auditById[$row.artId] = $row }
$auditBaseGuids = @{}
foreach ($row in $auditBaseline.illustrationGuids) { $auditBaseGuids[$row.artId] = $row.guid }

foreach ($artId in $auditExpectedIds) {
    if ($artId -in $ExcludeArtId) { continue }
    if (-not $auditById.ContainsKey($artId)) { Check 'asset' $artId $false 'Missing from final Western-v2 selected ledger.'; continue }
    $row = $auditById[$artId]
    $expectedTarget = [IO.Path]::GetFullPath((Join-Path $auditArtRoot ($artId + '.png')))
    $targetValid = $null -ne $row.target -and [IO.Path]::GetFullPath($row.target) -eq $expectedTarget
    Check 'identity' ($artId + ':version') ($row.version -eq 2) ('Selected revision=' + $row.version)
    Check 'identity' ($artId + ':target') $targetValid ('Expected resource target=' + $expectedTarget)
    $sourceExists = $null -ne $row.source -and (Test-Path -LiteralPath $row.source -PathType Leaf)
    $targetExists = Test-Path -LiteralPath $expectedTarget -PathType Leaf
    Check 'file' ($artId + ':source') $sourceExists ('Generated original=' + $row.source)
    Check 'file' ($artId + ':target') $targetExists 'Dedicated resource PNG exists.'
    $sourceHash = if ($sourceExists) { FileHash $row.source } else { $null }
    $targetHash = if ($targetExists) { FileHash $expectedTarget } else { $null }
    Check 'hash' $artId ($sourceExists -and $targetExists -and $sourceHash -eq $targetHash) 'Source and selected target SHA256 are identical.'
    $guid = MetaGuid ($expectedTarget + '.meta')
    Check 'metadata' ($artId + ':meta') ($null -ne $guid) 'PNG.meta has a valid32-hex Unity GUID.'
    Check 'metadata' ($artId + ':guid-unique') ($null -ne $guid -and $auditGuidIndex.ContainsKey($guid) -and $auditGuidIndex[$guid].Count -eq 1) 'GUID is unique across current UnityClient/Assets, including folders.'
    Check 'metadata' ($artId + ':import-policy') (ImportPolicy ($expectedTarget + '.meta')) '512px, mipmaps disabled, Standalone override uses BC7 (format25).'
    if ($auditBaseGuids.ContainsKey($artId) -and $null -ne $auditBaseGuids[$artId]) {
        Check 'metadata' ($artId + ':guid-preserved') ($guid -eq $auditBaseGuids[$artId]) 'Western redraw retains the pre-audit imported asset identity.'
    }
    $key = 'CardSchools/Art/' + $artId
    Check 'resource-key' $artId ($auditResourceIndex.ContainsKey($key) -and $auditResourceIndex[$key].Count -eq 1) ('Resources.Load<Texture2D> key=' + $key)
    $runtimeMatches = @($auditRuntimeCards | Where-Object { $_.allowedStages -gt 0 -and $_.artId -eq $artId })
    Check 'runtime-reference' $artId ($runtimeMatches.Count -eq 1) ('Active production cards referencing this dedicated art=' + $runtimeMatches.Count)
    $info = $null
    if ($targetExists) {
        try {
            $info = PngInfo $expectedTarget
            # The image-generation backend returns native1254px assets even when the prompt asks for1024.
            # Preserve native source pixels; the import policy separately sizes the in-game texture.
            Check 'png' $artId ($info.width -eq $info.height) ('Valid square PNG; native dimensions=' + $info.width + 'x' + $info.height)
        } catch { Check 'png' $artId $false $_.Exception.Message }
    }
    $auditAssetResults.Add([pscustomobject]@{ artId=$artId; source=$row.source; target=$expectedTarget; sourceSha256=$sourceHash; targetSha256=$targetHash; guid=$guid; png=$info; runtimeCardIds=@($runtimeMatches | ForEach-Object id) })
}
$auditLegacyLedger = @()
$auditLegacyAssetResults = [Collections.Generic.List[object]]::new()
if ($IncludeLegacy) {
    Check 'legacy-manifest' 'ledger-exists' (Test-Path -LiteralPath $auditLegacyLedgerPath -PathType Leaf) $auditLegacyLedgerPath
    if (Test-Path -LiteralPath $auditLegacyLedgerPath -PathType Leaf) {
        $auditLegacyLedger = @(Get-Content -LiteralPath $auditLegacyLedgerPath -Raw | ConvertFrom-Json)
    }
    Check 'legacy-manifest' 'runtime-legacy-count' ($auditLegacyIds.Count -eq 80) ('Active old CardIds=' + $auditLegacyIds.Count)
    Check 'legacy-manifest' 'selected-count' ($auditLegacyLedger.Count -eq 80) ('Selected independent old-card PNG rows=' + $auditLegacyLedger.Count)
    Check 'legacy-manifest' 'selected-unique' (@($auditLegacyLedger | Group-Object cardId | Where-Object Count -gt 1).Count -eq 0) 'Every old card has its own selected CardId row.'
    $selectedLegacyIds = @($auditLegacyLedger.cardId | Sort-Object)
    Check 'legacy-manifest' 'exact-card-set' ($selectedLegacyIds.Count -eq $auditLegacyIds.Count -and @(Compare-Object $auditLegacyIds $selectedLegacyIds).Count -eq 0) 'Legacy ledger equals all active non-card_school_ art references; reserved slots are excluded.'
    $legacyById = @{}
    foreach ($row in $auditLegacyLedger) { $legacyById[$row.cardId] = $row }
    foreach ($cardId in $auditLegacyIds) {
        if ($cardId -in $ExcludeLegacyCardId) { continue }
        if (-not $legacyById.ContainsKey($cardId)) { Check 'legacy-asset' $cardId $false 'Missing independent CardId illustration in selected legacy ledger.'; continue }
        $row = $legacyById[$cardId]
        $expectedTarget = [IO.Path]::GetFullPath((Join-Path $auditWesternRoot ($cardId + '.png')))
        $targetValid = $null -ne $row.target -and [IO.Path]::GetFullPath($row.target) -eq $expectedTarget
        Check 'legacy-identity' ($cardId + ':target') $targetValid ('Expected override resource target=' + $expectedTarget)
        $sourceExists = $null -ne $row.source -and (Test-Path -LiteralPath $row.source -PathType Leaf)
        $targetExists = Test-Path -LiteralPath $expectedTarget -PathType Leaf
        Check 'legacy-file' ($cardId + ':source') $sourceExists ('Generated original=' + $row.source)
        Check 'legacy-file' ($cardId + ':target') $targetExists 'Per-card Western resource PNG exists.'
        $sourceHash = if ($sourceExists) { FileHash $row.source } else { $null }
        $targetHash = if ($targetExists) { FileHash $expectedTarget } else { $null }
        Check 'legacy-hash' $cardId ($sourceExists -and $targetExists -and $sourceHash -eq $targetHash) 'Generated original and installed CardId target have identical SHA256.'
        $guid = MetaGuid ($expectedTarget + '.meta')
        Check 'legacy-metadata' ($cardId + ':meta') ($null -ne $guid) 'PNG.meta has a valid32-hex Unity GUID.'
        Check 'legacy-metadata' ($cardId + ':guid-unique') ($null -ne $guid -and $auditGuidIndex.ContainsKey($guid) -and $auditGuidIndex[$guid].Count -eq 1) 'CardId illustration GUID is unique across all Assets.'
        Check 'legacy-metadata' ($cardId + ':import-policy') (ImportPolicy ($expectedTarget + '.meta')) '512px, mipmaps disabled, Standalone override uses BC7 (format25).'
        $key = 'CardArt/Western/' + $cardId
        Check 'legacy-resource-key' $cardId ($auditResourceIndex.ContainsKey($key) -and $auditResourceIndex[$key].Count -eq 1) ('Unique override Resources key=' + $key)
        $runtimeMatches = @($auditLegacyCards | Where-Object id -eq $cardId)
        Check 'legacy-runtime-reference' $cardId ($runtimeMatches.Count -eq 1) 'This independent image resolves one active old CardId; old shared ArtId is preserved.'
        $info = $null
        if ($targetExists) {
            try {
                $info = PngInfo $expectedTarget
                Check 'legacy-png' $cardId ($info.width -eq $info.height) ('Valid square PNG; native dimensions=' + $info.width + 'x' + $info.height)
            } catch { Check 'legacy-png' $cardId $false $_.Exception.Message }
        }
        $result = [pscustomobject]@{ cardId=$cardId; artId=$cardId; oldArtId=$runtimeMatches[0].artId; source=$row.source; target=$expectedTarget; sourceSha256=$sourceHash; targetSha256=$targetHash; guid=$guid; png=$info; runtimeCardIds=@($cardId) }
        $auditAssetResults.Add($result)
        $auditLegacyAssetResults.Add($result)
    }
    Check 'runtime-reference' 'active-card-count' ($auditActiveCards.Count -eq 216) '216 active cards; three retained save-index slots have no stage/art requirement.'
    Check 'metadata' 'all-assets-guid-unique' ($auditDuplicates.Count -eq 0) ('Global duplicate GUID groups=' + $auditDuplicates.Count)
}
$auditDuplicateImageHashes = @($auditAssetResults | Where-Object { $null -ne $_.targetSha256 } | Group-Object targetSha256 | Where-Object Count -gt 1 |
    ForEach-Object { [pscustomobject]@{ sha256=$_.Name; artIds=@($_.Group | ForEach-Object artId) } })
Check 'hash' 'illustrations-unique' ($auditDuplicateImageHashes.Count -eq 0) ('Duplicate selected illustration hash groups=' + $auditDuplicateImageHashes.Count)
$dedicatedCards = @($auditRuntimeCards | Where-Object { $_.allowedStages -gt 0 -and $_.artId.StartsWith('card_school_') })
Check 'runtime-reference' 'all-dedicated-art-covered' (@(Compare-Object $auditExpectedIds @($dedicatedCards | ForEach-Object artId | Sort-Object)).Count -eq 0) 'Every active dedicated card art reference is in the selected136-art set.'
Check 'runtime-reference' 'school-art-count' (@($auditRuntimeCards | Where-Object { $_.allowedStages -gt 0 -and $_.schoolId.Length -gt 0 }).Count -eq 132) 'Current production has132 school art references.'
$loader = Get-Content -LiteralPath (Join-Path $auditAssets '_Project/Presentation/Runtime/CardSchoolArtCatalog.cs') -Raw
Check 'resource-key' 'dedicated-loader' ($loader.Contains('Load("CardSchools/Art/" + card.ArtId)') -and $loader.Contains('card.ArtId.StartsWith("card_school_", StringComparison.Ordinal)')) 'Current presentation loader matches audited PNG resource names.'
if ($IncludeLegacy) {
    $individual = [regex]::Match($loader, '(?s)private static Texture2D IndividualIllustration\(CoreBattleCard card\).*?(?=public static Texture2D Illustration)').Value
    $westernPosition = $individual.IndexOf('Load(western)', [StringComparison]::Ordinal)
    $schoolPosition = $individual.IndexOf('Load("CardSchools/Art/" + card.ArtId)', [StringComparison]::Ordinal)
    Check 'resource-key' 'cardid-override-loader' ($loader.Contains('"CardArt/Western/" + card.Id') -and $westernPosition -ge 0 -and $schoolPosition -gt $westernPosition -and $individual.Contains('if (replacement != null) return replacement;')) 'CardId override is loaded before school ArtId, then original atlas fallback.'
    $resolved = [Collections.Generic.List[object]]::new()
    foreach ($card in $auditActiveCards) {
        if ($card.artId -in $ExcludeArtId -or $card.id -in $ExcludeLegacyCardId) { continue }
        $westernKey = 'CardArt/Western/' + $card.id
        $schoolKey = 'CardSchools/Art/' + $card.artId
        $key = if ($auditResourceIndex.ContainsKey($westernKey)) { $westernKey } elseif ($card.artId.StartsWith('card_school_')) { $schoolKey } else { $null }
        $expectedKey = if ($card.id -in $auditLegacyIds) { $westernKey } else { $schoolKey }
        $exists = $null -ne $key -and $auditResourceIndex.ContainsKey($key) -and $auditResourceIndex[$key].Count -eq 1
        Check 'resolved-art' $card.id ($exists -and $key -eq $expectedKey) ('Actual loader resource selection=' + $key + '; expected=' + $expectedKey + '; atlas fallback is not a dedicated illustration.')
        if ($exists) { $resolved.Add([pscustomobject]@{ cardId=$card.id; resourceKey=$key; path=$auditResourceIndex[$key][0] }) }
    }
    $expectedAuditedCount = 216 - $ExcludeArtId.Count - $ExcludeLegacyCardId.Count
    Check 'resolved-art' 'independent-card-paths' ($resolved.Count -eq $expectedAuditedCount -and @($resolved | Group-Object path | Where-Object Count -gt 1).Count -eq 0) ('Audited active cards=' + $resolved.Count + '; expected=' + $expectedAuditedCount + '; each resolves a different PNG.')
}

$baselineByCard = @{}
foreach ($row in $auditBaseline.runtimeCards) { $baselineByCard[$row.id] = $row }
foreach ($card in $auditRuntimeCards) {
    $old = $baselineByCard[$card.id]
    $same = $null -ne $old -and $old.index -eq $card.index -and $old.artId -eq $card.artId -and $old.schoolId -eq $card.schoolId -and $old.allowedStages -eq $card.allowedStages -and $old.requiredCrew -eq $card.requiredCrew -and $old.requiredComponent -eq $card.requiredComponent
    Check 'reference-preservation' $card.id $same 'Card index, stable ID, ArtId, school, allowed stages and exact required source identity retained since the pre-final-art baseline.'
}
Check 'reference-preservation' 'library-count' ($auditRuntimeCards.Count -eq $auditBaseline.runtimeCards.Count) 'No card reference was removed or appended during the art-only redraw.'
$auditSourceCards = @($auditActiveCards | Where-Object { -not [string]::IsNullOrEmpty($_.sourceId) })
$auditCrewSourceCards = @($auditSourceCards | Where-Object sourceKind -eq 'Crew')
$auditComponentSourceCards = @($auditSourceCards | Where-Object sourceKind -eq 'Component')
Check 'source-identity' 'exact-source-count' ($auditSourceCards.Count -eq 126 -and $auditCrewSourceCards.Count -eq 36 -and $auditComponentSourceCards.Count -eq 90) ('Fresh CoreSpecialCardCatalog references: '+$auditSourceCards.Count+'; crew '+$auditCrewSourceCards.Count+'; components '+$auditComponentSourceCards.Count)
Check 'source-identity' 'one-card-per-source' (@($auditSourceCards | Group-Object sourceId | Where-Object Count -gt 1).Count -eq 0) 'Each of36 crew and90 component identities has exactly one stable primary source card.'
Check 'source-identity' 'school-and-neutral-source-count' (@($auditSourceCards | Where-Object { $_.schoolId.Length -gt 0 }).Count -eq 72 -and @($auditSourceCards | Where-Object { $_.schoolId.Length -eq 0 }).Count -eq 54) 'Sourced cards:72 school (12crew+60components),54 neutral (24crew+30bluecomponents).'
foreach ($card in $auditSourceCards) {
    $required = if ($card.sourceKind -eq 'Crew') {$card.requiredCrew} else {$card.requiredComponent}
    Check 'source-identity' $card.id ($required -eq $card.sourceId) ('Actual catalog SourceId='+$card.sourceId+'; exact card requirement='+$required)
}
Check 'runtime-reference' 'retired-slot-count' (@($auditRuntimeCards | Where-Object allowedStages -eq 0).Count -eq 3) '219 library slots include three retired index-compatible entries with no active art.'
foreach ($resource in $auditBaseline.oldResources) {
    if ($resource.role -eq 'legacy-card-atlas') {
        Check 'legacy-file' ($resource.key + ':png') (Test-Path -LiteralPath $resource.path -PathType Leaf) ('Old atlas retained as compatible fallback; its hash is not frozen after the authorized redraw=' + $resource.path)
    } else {
        Check 'legacy-file' ($resource.key + ':png') ((FileHash $resource.path) -eq $resource.sha256) ('Approved frame preserved=' + $resource.path)
    }
    Check 'legacy-file' ($resource.key + ':guid') ((MetaGuid ($resource.path + '.meta')) -eq $resource.guid) 'Imported identity retained.'
    Check 'legacy-file' ($resource.key + ':resource-key') ($auditResourceIndex.ContainsKey($resource.key) -and $auditResourceIndex[$resource.key].Count -eq 1) 'Legacy/approved resource still resolves uniquely.'
}
Check 'legacy-contract' 'atlas-and-cell-map' (($auditLegacy | ConvertTo-Json -Depth 5 -Compress) -eq ($auditBaseline.legacyContract | ConvertTo-Json -Depth 5 -Compress)) 'LegacyCardSheet resource key and original eight UV aliases are unchanged.'

$auditFailures = @($auditChecks | Where-Object status -eq 'FAIL')
$auditSummary = [pscustomobject]@{
    utc=[DateTime]::UtcNow.ToString('o')
    mode='Independent read-only asset integration audit; actual current Core compiled with Roslyn/Mono; no Unity, game or PlayerPrefs.'
    scope=$(if ($auditPartial) { 'Partial selected-asset audit; excluded originals are not hash-validated.' } elseif ($IncludeLegacy) { 'All216 active cards:132 school +4 new neutral +80 independent legacy CardId overrides.' } else { '132 six-school illustrations + four new neutral cook illustrations.' })
    complete136Audit=(-not $auditPartial); excludedArtIds=$ExcludeArtId
    includesLegacy=[bool]$IncludeLegacy; complete216Audit=([bool]$IncludeLegacy -and -not $auditPartial); excludedLegacyCardIds=$ExcludeLegacyCardId
    status=$(if ($auditFailures.Count -eq 0) {'PASS'} else {'FAIL'})
    expected=$(if ($IncludeLegacy) {216} else {136}); selected=$auditLedger.Count+$auditLegacyLedger.Count; auditedAssets=$auditAssetResults.Count
    schoolAndNewNeutralSelected=$auditLedger.Count; legacySelected=$auditLegacyLedger.Count; legacyAuditedAssets=$auditLegacyAssetResults.Count
    checks=$auditChecks.Count; passed=$auditChecks.Count-$auditFailures.Count; failed=$auditFailures.Count
    manifestSha256=(FileHash $auditLedgerPath); expectedManifestSha256=(FileHash (Join-Path $auditArtifacts 'art-manifest.json'))
    neutralManifestSha256=(FileHash (Join-Path $auditArtifacts 'neutral-art-manifest.json'))
    legacyManifestSha256=$(if ($IncludeLegacy) {FileHash $auditLegacyLedgerPath} else {$null})
    runtimeCardCount=$auditRuntimeCards.Count; dedicatedArtCount=$dedicatedCards.Count
    activeCardCount=$auditActiveCards.Count; retiredSlotCount=@($auditRuntimeCards | Where-Object allowedStages -eq 0).Count
    sourceCardCount=$auditSourceCards.Count; crewSourceCardCount=$auditCrewSourceCards.Count; componentSourceCardCount=$auditComponentSourceCards.Count
    schoolSourceCardCount=@($auditSourceCards | Where-Object { $_.schoolId.Length -gt 0 }).Count; neutralSourceCardCount=@($auditSourceCards | Where-Object { $_.schoolId.Length -eq 0 }).Count
    activeLegacyCardReferenceCount=@($auditRuntimeCards | Where-Object { $_.allowedStages -gt 0 -and -not $_.artId.StartsWith('card_school_') }).Count
    allAssetGuidGroups=$auditGuidIndex.Count; allAssetGuidDuplicates=$auditDuplicates
    duplicateImageHashes=$auditDuplicateImageHashes
    baseline=$auditBaselinePath; failures=$auditFailures; assets=$auditAssetResults; results=$auditChecks
    limitations=@('PNG signature/IHDR, square aspect,512/BC7 import metadata and file hashes are checked; native generated size is recorded rather than enforcing the prompt1024 size. No Unity import/render or visual-style judgment is claimed.', 'Card identities and approved frames are compared against the existing working-tree baseline. The old atlas remains fallback, but its hash is not a permanent acceptance requirement.', 'Without IncludeLegacy the report covers only136 school/new-neutral assets. IncludeLegacy additionally requires80 CardId PNGs,216 separate resolved resources, and no duplicate GUIDs anywhere in Assets.')
}
$auditSummary | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $auditOutput ($auditReportStem + '.json')) -Encoding utf8
$auditChecks | Export-Csv -LiteralPath (Join-Path $auditOutput ($auditReportStem + '-checks.csv')) -NoTypeInformation -Encoding utf8
$lines = @('西方中世纪卡图独立接入审计', '运行方式：当前Core只读编译＋文件/元数据审计；未启动Unity、游戏或玩家存档。',
    ('结果：' + $auditSummary.status + '；' + $auditSummary.passed + ' 项通过，' + $auditSummary.failed + ' 项失败。'),
    ('目标插画 '+$auditSummary.expected+'：132体系＋4新增普通厨师'+$(if ($IncludeLegacy) {'＋80旧牌独立CardId图'} else {''})+'；已审计 ' + $auditAssetResults.Count + '。'),
    ('当前生产牌库 ' + $auditRuntimeCards.Count + ' 槽；原专用ArtId ' + $dedicatedCards.Count + '，旧ArtId溯源 ' + $auditSummary.activeLegacyCardReferenceCount + $(if ($IncludeLegacy) {'（现均按CardId加载独立Western图）'} else {''}) + '。'),
    ('实际来源 '+$auditSourceCards.Count+'：角色 '+$auditCrewSourceCards.Count+'、部件 '+$auditComponentSourceCards.Count+'；一源一卡，既有身份与库索引保留。'),
    '逐图检查：ArtId或CardId／目标路径／体系v2清单／PNG／.meta／全Assets GUID唯一／源目标SHA256一致／Resources唯一键／真实Core引用。',
    '旧资源检查：牌库索引与引用稳定；七框及GUID保持准备快照；旧图集保留fallback且不永久锁定hash；旧UV映射未改变。',
    '范围限制：本报告不替代Unity导入、真实卡面截图或西方美术风格的视觉验收。')
foreach ($failure in $auditFailures) { $lines += ('失败：' + $failure.category + ' / ' + $failure.identity + ' / ' + $failure.detail) }
$textName = if ($IncludeLegacy) { if ($auditPartial) {'全卡素材接入部分审计.txt'} else {'全卡素材接入审计.txt'} } else { if ($auditPartial) { '素材接入部分审计-待最终PNG.txt' } else { '素材接入审计.txt' } }
if ($auditPartial) { $lines += ('此为部分审计，尚未完成全部目标；排除ArtId：' + ($ExcludeArtId -join ', ')+'；排除旧CardId：'+($ExcludeLegacyCardId -join ', ')) }
$lines | Set-Content -LiteralPath (Join-Path $auditOutput $textName) -Encoding utf8
$runLabel = if ($auditPartial) { 'Partial asset audit' } else { 'Final asset audit' }
Write-Output ($runLabel + ': ' + $auditSummary.status + '; ' + $auditSummary.passed + ' passed, ' + $auditSummary.failed + ' failed.')
if ($auditFailures.Count -gt 0) { exit 1 }
