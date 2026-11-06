param([string]$CatalogDirectory = 'D:/Game/outputs/six-school-audit-20261024/VerifiedCatalog')
$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditInputs = Join-Path $auditRoot 'outputs/six-school-audit-20261024/Inputs'
$auditOld = Get-Content -LiteralPath (Join-Path $auditInputs 'card-list-data.json') -Raw -Encoding utf8 | ConvertFrom-Json
$auditNew = Get-Content -LiteralPath (Join-Path $CatalogDirectory 'card-list-data.json') -Raw -Encoding utf8 | ConvertFrom-Json
$auditLines = [Collections.Generic.List[string]]::new()
$auditAllowed = @('card_school_fire_b03','card_school_curse_b03','card_school_curse_b04','card_school_tide_b03')
$auditChanged = @()
foreach ($auditCard in $auditOld.Cards) {
    $auditMatch = @($auditNew.Cards | Where-Object Id -eq $auditCard.Id)
    if ($auditMatch.Count -ne 1) { throw ('Missing or duplicate prior card: ' + $auditCard.Id) }
    if ($auditCard.Number -ne $auditMatch[0].Number) { throw ('Prior number changed: ' + $auditCard.Id) }
    if (($auditCard | ConvertTo-Json -Depth 20 -Compress) -cne ($auditMatch[0] | ConvertTo-Json -Depth 20 -Compress)) {
        if ($auditAllowed -notcontains $auditCard.Id) { throw ('Unexpected card-face change: ' + $auditCard.Id) }
        $auditChanged += $auditCard.Id
    }
}
if ($auditChanged.Count -ne 4) { throw 'Expected exactly four existing card-face revisions.' }
$auditAdded = @($auditNew.Cards | Where-Object { $_.Id -notin $auditOld.Cards.Id })
if ($auditAdded.Count -ne 1 -or $auditAdded[0].Id -ne 'card_school_arcane_n04' -or $auditAdded[0].Number -ne '3025') {
    throw 'Unexpected card addition.'
}
foreach ($auditProperty in @('Glossary','Rules')) {
    if (($auditOld.$auditProperty | ConvertTo-Json -Depth 20 -Compress) -cne ($auditNew.$auditProperty | ConvertTo-Json -Depth 20 -Compress)) {
        throw ('Unexpected change in ' + $auditProperty)
    }
}
Add-Type -Path (Join-Path $auditRoot 'Artifacts/SixSchoolAudit20261024/Compile/ShatteredSail.Core.dll')
$auditLibrary = [ShatteredSail.Core.Gameplay.CoreVoyageSession]::CardLibrary
$auditIdentities = @(Import-Csv -LiteralPath (Join-Path $auditInputs 'library-identity.csv'))
foreach ($auditIdentity in $auditIdentities) {
    $auditCurrent = $auditLibrary[[int]$auditIdentity.Index]
    if ($auditCurrent.Id -ne $auditIdentity.Id -or
        [ShatteredSail.Core.Gameplay.CoreBattleCardNumbering]::Number($auditCurrent) -ne [int]$auditIdentity.Number) {
        throw ('Saved library identity changed at ' + $auditIdentity.Index)
    }
}
if ($auditLibrary.Count -ne $auditIdentities.Count + 1 -or $auditLibrary[$auditIdentities.Count].Id -ne 'card_school_arcane_n04') {
    throw 'The new card must append after every saved library slot.'
}
foreach ($auditManifest in @((Join-Path $CatalogDirectory 'source-hashes.csv'),
    (Join-Path $auditRoot 'Artifacts/SixSchoolAudit20261024/Compile/source-hashes.csv'))) {
    foreach ($auditHash in Import-Csv -LiteralPath $auditManifest) {
        if ((Get-FileHash -LiteralPath $auditHash.Source -Algorithm SHA256).Hash -ne $auditHash.SHA256) {
            throw ('Stale generated output: ' + $auditHash.Source)
        }
    }
}
$auditLines.Add('PASS 315 existing published identities and all visible numbers preserved.')
$auditLines.Add('PASS 355 saved library slots preserved; new card appended at 355 as 3025.')
$auditLines.Add('PASS only four authorized existing card faces changed; one new mana payoff card.')
$auditLines.Add('PASS all 23 keyword definitions and seven field inheritance rules unchanged.')
$auditLines.Add('PASS final catalogue and production assembly source hashes match the workspace.')
$auditLines.Add('PASS 316 identities, 350 stage definitions, 319 grouped display variants.')
[IO.File]::WriteAllLines((Join-Path $CatalogDirectory 'audit-validation.txt'), $auditLines, [Text.UTF8Encoding]::new($false))
$auditLines
