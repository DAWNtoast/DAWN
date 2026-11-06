param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$dualRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dualOutput = Join-Path $dualRoot 'outputs/dual-fields-20261026'
$dualCatalog = Join-Path $dualRoot 'outputs/card-list-by-school-20261023'
$dualBefore = Join-Path $dualOutput 'BeforeCatalog'
$dualFields = @('fire_field','arcane_field','curse_field','blood_field')
function Json-Dual($value) { ConvertTo-Json -InputObject $value -Depth 100 -Compress }
function Compact-Dual([string]$value) { [regex]::Replace([Net.WebUtility]::HtmlDecode([regex]::Replace($value,'<[^>]*>','')), '\s+', '') }
if (!(Test-Path -LiteralPath (Join-Path $dualBefore 'card-list-data.json'))) { throw 'Prior catalogue backup required' }
$dualTextHash = (Get-FileHash -LiteralPath (Join-Path $dualBefore '全部卡牌清单.txt')).Hash
if (!$VerifyOnly) {
    foreach ($path in @('Build/ExportCardList.exe','UpgradeBuild/ExportUpgradeList.exe')) {
        if (Test-Path -LiteralPath (Join-Path $dualCatalog $path)) { throw "Existing shared compile output needs inspection: $path" }
    }
}
try {
    if (!$VerifyOnly) {
        & (Join-Path $dualRoot 'tools/CardListBySchool20261023/ExportCardList.ps1') -Output $dualCatalog
    }
    $old = Get-Content -LiteralPath (Join-Path $dualBefore 'card-list-data.json') -Raw | ConvertFrom-Json
    $new = Get-Content -LiteralPath (Join-Path $dualCatalog 'card-list-data.json') -Raw | ConvertFrom-Json
    $oldUp = Get-Content -LiteralPath (Join-Path $dualBefore 'card-upgrade-data.json') -Raw | ConvertFrom-Json
    $newUp = Get-Content -LiteralPath (Join-Path $dualCatalog 'card-upgrade-data.json') -Raw | ConvertFrom-Json
    if ($old.Cards.Count -ne $new.Cards.Count) { throw 'Card count changed' }
    $changed = [Collections.Generic.List[string]]::new()
    foreach ($card in $new.Cards) {
        $prior = @($old.Cards | Where-Object Id -eq $card.Id)
        if ($prior.Count -ne 1) { throw "Card identity changed: $($card.Id)" }
        $clone = (Json-Dual $card) | ConvertFrom-Json
        if ((Json-Dual $prior[0]) -ne (Json-Dual $card)) { $changed.Add($card.Id) }
        for ($i=0; $i -lt $clone.StageVariants.Count; $i++) {
            foreach ($keyword in $clone.StageVariants[$i].Keywords) {
                if ($keyword.Id -in $dualFields) {
                    $beforeTerm = @($prior[0].StageVariants[$i].Keywords | Where-Object Id -eq $keyword.Id)
                    if ($beforeTerm.Count -ne 1) { throw 'Keyword identity changed' }
                    $keyword.Meaning = $beforeTerm[0].Meaning
                }
            }
        }
        if ((Json-Dual $prior[0]) -ne (Json-Dual $clone)) { throw "Unexpected non-keyword card change: $($card.Id)" }
    }
    if ((Json-Dual $oldUp.Cards) -ne (Json-Dual $newUp.Cards)) { throw 'Upgrade card values or costs changed' }
    $changedTerms = @($new.Glossary | Where-Object {
        $priorTerm = $old.Glossary | Where-Object Id -eq $_.Id
        (Json-Dual $priorTerm) -ne (Json-Dual $_)
    } | Select-Object -ExpandProperty Id)
    if ((($changedTerms | Sort-Object) -join ',') -ne (($dualFields | Sort-Object) -join ',')) { throw 'Unexpected glossary changes' }
    foreach ($name in @('StageCount','CrewCount','ComponentCount','Basis')) {
        if ((Json-Dual $old.$name) -ne (Json-Dual $new.$name)) { throw "Base metadata changed: $name" }
    }
    foreach ($name in @('Rules','CardCount','StageCount','LevelCount','Basis')) {
        if ((Json-Dual $oldUp.$name) -ne (Json-Dual $newUp.$name)) { throw "Upgrade metadata changed: $name" }
    }
    $html = Get-Content -LiteralPath (Join-Path $dualCatalog '全部卡牌清单.html') -Raw
    $upHtml = Get-Content -LiteralPath (Join-Path $dualCatalog '全部卡牌强化清单.html') -Raw
    foreach ($term in $new.Glossary | Where-Object Id -in $dualFields) {
        if (!(Compact-Dual $html).Contains((Compact-Dual $term.Meaning))) { throw "HTML lacks new meaning: $($term.Id)" }
    }
    foreach ($card in $new.Cards) {
        $block = [regex]::Match($html, '(?s)<tbody class="card-block" data-card-id="'+[regex]::Escape($card.Id)+'".*?</tbody>').Value
        if (!$block) { throw "Missing base card block: $($card.Id)" }
        foreach ($variant in $card.StageVariants) {
            if (!(Compact-Dual $block).Contains((Compact-Dual $variant.Effect))) { throw "Base HTML mismatch: $($card.Id)" }
        }
    }
    foreach ($card in $newUp.Cards) {
        $article = [regex]::Match($upHtml, '(?s)<article id="card-'+$card.Number+'">.*?</article>').Value
        if (!$article) { throw "Missing upgrade article: $($card.Id)" }
        foreach ($level in @($card.Stages | ForEach-Object Levels)) {
            if (!(Compact-Dual $article).Contains((Compact-Dual $level.Effect))) { throw "Upgrade HTML mismatch: $($card.Id)" }
        }
    }
    if ((Get-FileHash -LiteralPath (Join-Path $dualCatalog '全部卡牌清单.txt')).Hash -ne $dualTextHash) { throw 'User TXT input changed' }
    foreach ($hashFile in @('source-hashes.csv','upgrade-source-hashes.csv')) {
        foreach ($source in Import-Csv -LiteralPath (Join-Path $dualCatalog $hashFile)) {
            if ((Get-FileHash -LiteralPath $source.Source).Hash -ne $source.SHA256) { throw "Source changed during export: $($source.Source)" }
        }
    }
    [pscustomobject]@{ Result='PASS'; Cards=$new.Cards.Count; StageDefinitions=$new.StageCount;
        UpgradeRows=$newUp.LevelCount; ChangedGlossaryIds=$changedTerms; ChangedBaseCardKeywordCount=$changed.Count;
        CardValuesAndCostsUnchanged=$true; AllUpgradeRowsUnchanged=$true; HtmlMatchesFinalDefinitions=$true;
        UserTxtUnchanged=$true; FixedHtml=(Join-Path $dualCatalog '全部卡牌清单.html') } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dualOutput 'catalog-verification.json') -Encoding utf8
    & (Join-Path $PSScriptRoot 'AuditBalance.ps1') -Output $dualOutput
    Write-Output 'PASS: four field glossary definitions refreshed; all card costs, values and upgrade rows preserved.'
} finally {
    if (!$VerifyOnly) {
    foreach ($path in @($dualRoot,(Join-Path $dualRoot 'outputs'),$dualCatalog,(Join-Path $dualCatalog 'Build'),(Join-Path $dualCatalog 'UpgradeBuild'))) {
        if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Cleanup refused directory link: $path" }
    }
    $using = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe' OR Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and ($_.CommandLine.Contains($dualCatalog) -or $_.CommandLine.Contains($dualCatalog.Replace('\','/'))) })
    if ($using.Count) { throw 'Export files still in use; cleanup deferred' }
    $freed = 0L
    foreach ($pair in @(@('Build','ExportCardList.exe'),@('UpgradeBuild','ExportUpgradeList.exe'))) {
        $dir = Join-Path $dualCatalog $pair[0]
        $file = [IO.Path]::GetFullPath((Join-Path $dir $pair[1]))
        if ([IO.Path]::GetDirectoryName($file) -ne [IO.Path]::GetFullPath($dir)) { throw 'Unexpected cleanup path' }
        if (Test-Path -LiteralPath $file) {
            $item = Get-Item -LiteralPath $file
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Cleanup refused file link: $file" }
            $freed += $item.Length
            Remove-Item -LiteralPath $file -Force
            if (Test-Path -LiteralPath $file) { throw "Cleanup failed: $file" }
        }
        if ((Test-Path -LiteralPath $dir) -and @(Get-ChildItem -LiteralPath $dir -Force).Count -eq 0) { Remove-Item -LiteralPath $dir }
    }
    if (!$VerifyOnly -or $freed -gt 0) {
        [pscustomobject]@{ ReleasedBytes=$freed; MainProjectCleaned=$false; Preserved='User input, backups, reports, fixed HTML and source hashes' } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dualOutput 'catalog-cleanup.json') -Encoding utf8
    }
    }
}
