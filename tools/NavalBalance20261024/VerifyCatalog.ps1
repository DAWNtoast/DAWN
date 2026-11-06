param(
    [string]$CatalogDirectory = 'D:/Game/outputs/naval-balance-20261024/VerifiedCatalog',
    [string[]]$AllowedCardIds = @('card_school_fire_b04', 'card_school_blood_b06', 'card_school_arcane_n03')
)
$ErrorActionPreference = 'Stop'
$balanceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$balanceInputs = Join-Path $balanceRoot 'outputs/naval-balance-20261024/Inputs'
$balanceOld = Get-Content -LiteralPath (Join-Path $balanceInputs 'card-list-data.json') -Raw -Encoding utf8 | ConvertFrom-Json
$balanceNew = Get-Content -LiteralPath (Join-Path $CatalogDirectory 'card-list-data.json') -Raw -Encoding utf8 | ConvertFrom-Json
$balanceLines = [Collections.Generic.List[string]]::new()
$balanceChanged = @()
if ($balanceNew.Cards.Count -ne $balanceOld.Cards.Count) { throw 'Card count changed during a numerical calibration.' }
foreach ($balanceCard in $balanceOld.Cards) {
    $balanceMatches = @($balanceNew.Cards | Where-Object Id -eq $balanceCard.Id)
    if ($balanceMatches.Count -ne 1) { throw ('Missing or duplicate card: ' + $balanceCard.Id) }
    $balanceMatch = $balanceMatches[0]
    if ($balanceCard.Number -ne $balanceMatch.Number) { throw ('Card number changed: ' + $balanceCard.Id) }
    if (($balanceCard | ConvertTo-Json -Depth 20 -Compress) -cne ($balanceMatch | ConvertTo-Json -Depth 20 -Compress)) {
        if ($AllowedCardIds -notcontains $balanceCard.Id) { throw ('Unexpected card-face change: ' + $balanceCard.Id) }
        $balanceChanged += $balanceCard.Id
    }
}
foreach ($balanceProperty in @('Glossary', 'Rules')) {
    if (($balanceOld.$balanceProperty | ConvertTo-Json -Depth 20 -Compress) -cne ($balanceNew.$balanceProperty | ConvertTo-Json -Depth 20 -Compress)) {
        throw ('Unexpected change in ' + $balanceProperty)
    }
}
Add-Type -Path (Join-Path $balanceRoot 'Artifacts/NavalBalance20261024/Compile/ShatteredSail.Core.dll')
$balanceLibrary = [ShatteredSail.Core.Gameplay.CoreVoyageSession]::CardLibrary
$balanceIdentities = @(Import-Csv -LiteralPath (Join-Path $balanceRoot 'outputs/six-school-audit-20261024/Inputs/library-identity.csv'))
foreach ($balanceIdentity in $balanceIdentities) {
    $balanceCurrent = $balanceLibrary[[int]$balanceIdentity.Index]
    if ($balanceCurrent.Id -ne $balanceIdentity.Id -or
        [ShatteredSail.Core.Gameplay.CoreBattleCardNumbering]::Number($balanceCurrent) -ne [int]$balanceIdentity.Number) {
        throw ('Saved library identity changed at ' + $balanceIdentity.Index)
    }
}
if ($balanceLibrary.Count -ne $balanceIdentities.Count + 1 -or
    $balanceLibrary[$balanceIdentities.Count].Id -ne 'card_school_arcane_n04' -or
    [ShatteredSail.Core.Gameplay.CoreBattleCardNumbering]::Number($balanceLibrary[$balanceIdentities.Count]) -ne 3025) {
    throw 'Saved appended mana card identity changed.'
}
foreach ($balanceManifest in @((Join-Path $CatalogDirectory 'source-hashes.csv'),
    (Join-Path $balanceRoot 'Artifacts/NavalBalance20261024/Compile/source-hashes.csv'))) {
    foreach ($balanceHash in Import-Csv -LiteralPath $balanceManifest) {
        if ((Get-FileHash -LiteralPath $balanceHash.Source -Algorithm SHA256).Hash -ne $balanceHash.SHA256) {
            throw ('Stale generated output: ' + $balanceHash.Source)
        }
    }
}
$balanceLines.Add('PASS all published card IDs/numbers and saved library positions unchanged.')
$balanceLines.Add('PASS no cards or keywords added or removed; field inheritance rules unchanged.')
$balanceLines.Add('PASS permitted card-face changes: ' + ($balanceChanged -join ', '))
$balanceLines.Add('PASS catalogue and all four production assemblies match current source hashes.')
[IO.File]::WriteAllLines((Join-Path $CatalogDirectory 'audit-validation.txt'), $balanceLines, [Text.UTF8Encoding]::new($false))
$balanceLines
