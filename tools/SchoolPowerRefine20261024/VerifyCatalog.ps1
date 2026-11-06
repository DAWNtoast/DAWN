param(
    [string]$CatalogDirectory = 'D:/Game/outputs/school-power-refine-20261024/VerifiedCatalog',
    [string]$FixedCatalogDirectory = 'D:/Game/outputs/card-list-by-school-20261023',
    [string]$BaselineProbeDirectory = 'D:/Game/outputs/school-power-20261024/final-v3',
    [switch]$RequireFixedSync
)
$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditInputs = Join-Path $auditWorkspace 'outputs/school-power-refine-20261024/Inputs'
$auditCatalog = [IO.Path]::GetFullPath($CatalogDirectory)
$auditExpectedAdded = @()
$auditOld = Get-Content -LiteralPath (Join-Path $auditInputs 'Catalog/card-list-data.json') -Raw -Encoding utf8 | ConvertFrom-Json
$auditNew = Get-Content -LiteralPath (Join-Path $auditCatalog 'card-list-data.json') -Raw -Encoding utf8 | ConvertFrom-Json
$auditLines = [Collections.Generic.List[string]]::new()

function Assert-Audit([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function ConvertTo-AuditJson($Value) { ConvertTo-Json -InputObject $Value -Depth 40 -Compress }
function Get-AuditLibrary([string]$AssemblyPath) {
    # The JSON deliberately exposes public catalogue numbers, not save-file indexes.
    # Read indexes from the exact compiled Core used by the verified source snapshot.
    $assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
    Assert-Audit ($assembly.Location -eq [IO.Path]::GetFullPath($AssemblyPath)) 'A different assembly with this identity is already loaded; run the verifier in a fresh PowerShell process.'
    $sessionType = $assembly.GetType('ShatteredSail.Core.Gameplay.CoreVoyageSession', $true)
    $numberType = $assembly.GetType('ShatteredSail.Core.Gameplay.CoreBattleCardNumbering', $true)
    $numberMethod = $numberType.GetMethod('Number', [type[]]@([string]))
    $library = $sessionType.GetProperty('CardLibrary').GetValue($null)
    for ($index = 0; $index -lt $library.Count; $index++) {
        $card = $library[$index]
        [pscustomobject]@{ LibraryIndex = $index; Id = $card.Id; CardNumber = [int]$numberMethod.Invoke($null, [object[]]@($card.Id)) }
    }
}

# The last calibrated baseline is reused without compiling or changing any production source.
$auditBeforeSources = @(Get-ChildItem -LiteralPath (Join-Path $auditInputs 'CoreSnapshot') -File -Filter '*.cs')
$auditBaselineSources = @(Get-ChildItem -LiteralPath (Join-Path $BaselineProbeDirectory 'CoreSnapshot') -File -Filter '*.cs')
Assert-Audit ($auditBeforeSources.Count -eq $auditBaselineSources.Count) 'The reusable baseline snapshot does not match this round input snapshot.'
foreach ($source in $auditBeforeSources) {
    $baseline = Join-Path (Join-Path $BaselineProbeDirectory 'CoreSnapshot') $source.Name
    Assert-Audit ((Test-Path -LiteralPath $baseline) -and
        (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash -eq
        (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash) ('Baseline snapshot mismatch: ' + $source.Name)
}
foreach ($row in Import-Csv -LiteralPath (Join-Path $BaselineProbeDirectory 'source-hashes.csv')) {
    Assert-Audit ((Get-FileHash -LiteralPath $row.Snapshot -Algorithm SHA256).Hash -eq $row.SHA256) ('Baseline snapshot hash changed: ' + $row.Snapshot)
}
foreach ($row in Import-Csv -LiteralPath (Join-Path $auditCatalog 'source-hashes.csv')) {
    Assert-Audit ((Get-FileHash -LiteralPath $row.Source -Algorithm SHA256).Hash -eq $row.SHA256) ('Export is stale against its source: ' + $row.Source)
}
$auditOldAssembly = Join-Path $BaselineProbeDirectory 'SchoolPowerProbe.exe'
$auditNewAssembly = Join-Path $auditCatalog 'Build/ExportCardList.exe'
$auditOldLibrary = @(Get-AuditLibrary $auditOldAssembly)
$auditNewLibrary = @(Get-AuditLibrary $auditNewAssembly)
Assert-Audit ($auditNewLibrary.Count -eq $auditOldLibrary.Count + $auditExpectedAdded.Count) 'Numeric refinement must preserve the save library size.'
foreach ($old in $auditOldLibrary) {
    $current = $auditNewLibrary[$old.LibraryIndex]
    Assert-Audit ($current.Id -ceq $old.Id -and $current.CardNumber -eq $old.CardNumber) ('Saved slot or public number changed: ' + $old.LibraryIndex + '/' + $old.Id)
}
for ($index = 0; $index -lt $auditExpectedAdded.Count; $index++) {
    Assert-Audit ($auditNewLibrary[$auditOldLibrary.Count + $index].Id -ceq $auditExpectedAdded[$index]) ('Unexpected appended save slot at offset ' + $index)
}
$auditLines.Add('PASS every historic save-library index and card number is unchanged; no card identities added or removed.')

$auditOldCards = @{}; $auditNewCards = @{}; $auditNumbers = @{}
foreach ($card in $auditOld.Cards) {
    Assert-Audit (-not $auditOldCards.ContainsKey([string]$card.Id)) ('Duplicate input card ID: ' + $card.Id)
    $auditOldCards[[string]$card.Id] = $card
}
foreach ($card in $auditNew.Cards) {
    Assert-Audit (-not $auditNewCards.ContainsKey([string]$card.Id)) ('Duplicate export card ID: ' + $card.Id)
    Assert-Audit (-not $auditNumbers.ContainsKey([string]$card.Number)) ('Duplicate export card number: ' + $card.Number)
    $auditNewCards[[string]$card.Id] = $card; $auditNumbers[[string]$card.Number] = $true
}
Assert-Audit ($auditNewCards.Count -eq $auditOldCards.Count + $auditExpectedAdded.Count) 'Numeric refinement must preserve the published card count.'
$auditChanges = [Collections.Generic.List[object]]::new()
foreach ($card in $auditOld.Cards) {
    Assert-Audit ($auditNewCards.ContainsKey([string]$card.Id)) ('Published card removed: ' + $card.Id)
    $current = $auditNewCards[[string]$card.Id]
    Assert-Audit ($current.Number -ceq $card.Number) ('Published card number changed: ' + $card.Id)
    if ((ConvertTo-AuditJson $card) -cne (ConvertTo-AuditJson $current)) {
        $auditChanges.Add([pscustomobject]@{ Id=$card.Id; Number=$card.Number; Name=$current.Name; Change='modified'; Before=(ConvertTo-AuditJson $card.StageVariants); After=(ConvertTo-AuditJson $current.StageVariants) })
    }
}
foreach ($id in $auditExpectedAdded) {
    Assert-Audit (-not $auditOldCards.ContainsKey($id) -and $auditNewCards.ContainsKey($id)) ('Expected new published ID missing or already historic: ' + $id)
    $card=$auditNewCards[$id]
    $auditChanges.Add([pscustomobject]@{ Id=$id; Number=$card.Number; Name=$card.Name; Change='added'; Before=''; After=(ConvertTo-AuditJson $card.StageVariants) })
}
foreach ($entry in $auditNewLibrary) {
    if ($auditNewCards.ContainsKey([string]$entry.Id)) {
        Assert-Audit ([int]$auditNewCards[[string]$entry.Id].Number -eq $entry.CardNumber) ('JSON and runtime number disagree: ' + $entry.Id)
    }
}
foreach ($property in @('Glossary', 'Rules')) {
    Assert-Audit ((ConvertTo-AuditJson $auditOld.$property) -ceq (ConvertTo-AuditJson $auditNew.$property)) ('Keyword/field inheritance definition changed: ' + $property)
}
Assert-Audit ($auditNew.StageCount -eq $auditOld.StageCount + $auditExpectedAdded.Count -and
    $auditNew.CrewCount -eq $auditOld.CrewCount -and $auditNew.ComponentCount -eq $auditOld.ComponentCount) 'Unexpected stage/source-card count change.'
$auditLines.Add('PASS published IDs/numbers, glossary and boarding-field inheritance rules agree with the input; no new keywords.')

# Verify rendered values in their own card blocks, not merely a global substring.
$auditHtml = Get-Content -LiteralPath (Join-Path $auditCatalog '全部卡牌清单.html') -Raw -Encoding utf8
$auditMd = Get-Content -LiteralPath (Join-Path $auditCatalog '全部卡牌清单.md') -Raw -Encoding utf8
Assert-Audit ([regex]::Matches($auditHtml, 'data-card-id="').Count -eq $auditNew.Cards.Count) 'HTML card count differs from JSON.'
Assert-Audit ([regex]::Matches($auditMd, '(?m)^### ').Count -eq $auditNew.Cards.Count) 'Markdown card count differs from JSON.'
$auditGlossary = @{}; foreach ($entry in $auditNew.Glossary) { $auditGlossary[[string]$entry.Id] = $entry }
foreach ($card in $auditNew.Cards) {
    $htmlMatch = [regex]::Match($auditHtml, '(?s)<tbody class="card-block" data-card-id="' + [regex]::Escape([Net.WebUtility]::HtmlEncode([string]$card.Id)) + '".*?</tbody>')
    $mdMatch = [regex]::Match($auditMd, '(?ms)^### ' + [regex]::Escape([string]$card.Number + ' ' + $card.Name + '｜' + $card.Source) + '\r?\n.*?(?=^### |^## |\z)')
    Assert-Audit ($htmlMatch.Success -and $mdMatch.Success) ('Rendered card block missing: ' + $card.Id)
    $block = [Net.WebUtility]::HtmlDecode($htmlMatch.Value)
    Assert-Audit ($block.Contains('>#' + $card.Number + '</span>') -and $block.Contains([string]$card.Name)) ('HTML identity mismatch: ' + $card.Id)
    foreach ($variant in $card.StageVariants) {
        foreach ($value in @($variant.Effect, $variant.OtherCost, $variant.Name, $variant.Energy)) {
            Assert-Audit (-not [regex]::IsMatch([string]$value, '[{}]|\bTODO\b|\bTBD\b')) ('Unresolved card text: ' + $card.Id)
        }
        Assert-Audit ($block.Contains('<div class="effect">' + $variant.Effect + '</div>') -and
            $block.Contains('<span class="energy">能量：' + $variant.Energy + '</span>') -and
            $block.Contains('<div class="other-cost">' + $variant.OtherCost + '</div>')) ('HTML effect/cost differs from JSON: ' + $card.Id)
        $mdEffect = '- **效果**：' + ([string]$variant.Effect).Replace("`r", '').Replace("`n", ' ')
        $mdCost = '- **消耗**：能量 ' + $variant.Energy + $(if ($variant.OtherCost -eq '—') { '；无其他消耗' } else { '；' + $variant.OtherCost })
        Assert-Audit ($mdMatch.Value.Contains($mdEffect) -and $mdMatch.Value.Contains($mdCost)) ('Markdown effect/cost differs from JSON: ' + $card.Id)
        foreach ($keyword in $variant.Keywords) {
            Assert-Audit ($auditGlossary.ContainsKey([string]$keyword.Id) -and
                $auditGlossary[[string]$keyword.Id].Meaning -ceq $keyword.Meaning -and
                $auditGlossary[[string]$keyword.Id].Names -ccontains $keyword.Name) ('Undeclared/changed keyword: ' + $card.Id + '/' + $keyword.Id)
        }
    }
}
foreach ($rule in $auditNew.Rules) {
    Assert-Audit ($auditHtml.Contains([Net.WebUtility]::HtmlEncode([string]$rule)) -and $auditMd.Contains([string]$rule)) 'Rendered field inheritance rule missing.'
}
$auditLines.Add('PASS all HTML/Markdown card effects and costs match JSON; no unresolved placeholders; keyword references use the unchanged glossary.')

$auditSync = foreach ($name in @('全部卡牌清单.html', '全部卡牌清单.md', 'card-list-data.json')) {
    $verifiedPath = Join-Path $auditCatalog $name
    $fixedPath = Join-Path $FixedCatalogDirectory $name
    $verifiedHash = (Get-FileHash -LiteralPath $verifiedPath -Algorithm SHA256).Hash
    $fixedHash = if (Test-Path -LiteralPath $fixedPath) { (Get-FileHash -LiteralPath $fixedPath -Algorithm SHA256).Hash } else { '' }
    [pscustomobject]@{ File=$name; VerifiedPath=$verifiedPath; VerifiedSHA256=$verifiedHash; FixedPath=$fixedPath; FixedSHA256=$fixedHash; Synced=($verifiedHash -eq $fixedHash) }
}
$auditOldLibrary | Export-Csv -LiteralPath (Join-Path $auditCatalog 'library-identity-before.csv') -NoTypeInformation -Encoding utf8
$auditNewLibrary | Export-Csv -LiteralPath (Join-Path $auditCatalog 'library-identity-after.csv') -NoTypeInformation -Encoding utf8
$auditChanges | Export-Csv -LiteralPath (Join-Path $auditCatalog 'catalog-changes.csv') -NoTypeInformation -Encoding utf8
$auditSync | Export-Csv -LiteralPath (Join-Path $auditCatalog 'catalog-sync-hashes.csv') -NoTypeInformation -Encoding utf8
$auditSynced = @($auditSync | Where-Object { -not $_.Synced }).Count -eq 0
$auditLines.Add($(if ($auditSynced) { 'PASS fixed HTML/Markdown/JSON are byte-identical to the verified export.' } else { 'PENDING fixed HTML/Markdown/JSON synchronization; see catalog-sync-hashes.csv.' }))
$auditSummary = [ordered]@{
    Utc=[DateTime]::UtcNow.ToString('o'); InputDirectory=$auditInputs; VerifiedDirectory=$auditCatalog
    OldLibraryCount=$auditOldLibrary.Count; NewLibraryCount=$auditNewLibrary.Count
    OldPublishedCount=$auditOld.Cards.Count; NewPublishedCount=$auditNew.Cards.Count
    AddedIds=$auditExpectedAdded; ChangedOldIds=@($auditChanges | Where-Object Change -eq 'modified' | ForEach-Object Id)
    GlossaryCount=$auditNew.Glossary.Count; InheritanceRuleCount=$auditNew.Rules.Count
    FixedCatalogSynchronized=$auditSynced
    BaselineAssemblySHA256=(Get-FileHash -LiteralPath $auditOldAssembly -Algorithm SHA256).Hash
    ExportAssemblySHA256=(Get-FileHash -LiteralPath $auditNewAssembly -Algorithm SHA256).Hash
}
$auditSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $auditCatalog 'catalog-integrity.json') -Encoding utf8
[IO.File]::WriteAllLines((Join-Path $auditCatalog 'audit-validation.txt'), $auditLines, [Text.UTF8Encoding]::new($false))
if ($RequireFixedSync -and -not $auditSynced) { throw 'Fixed catalogue does not match verified export; hash audit was written without modifying the fixed files.' }
$auditLines
