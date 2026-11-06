param(
    [string]$CatalogDirectory = 'D:/Game/outputs/tide-rules-20261024/VerifiedCatalog',
    [string]$Manuscript = 'D:/Game/outputs/tide-rules-20261024/Inputs/全部卡牌清单.user.md'
)
$ErrorActionPreference = 'Stop'
$tideData = Get-Content -LiteralPath (Join-Path $CatalogDirectory 'card-list-data.json') -Encoding utf8 -Raw | ConvertFrom-Json
$tideManuscript = Get-Content -LiteralPath $Manuscript -Encoding utf8 -Raw
$tideSections = [regex]::Matches($tideManuscript, '(?ms)^### (?<Number>\d+) (?<Name>.*?)｜[^\r\n]+\r?\n(?<Body>.*?)(?=^### |^## |\z)')
$tideByNumber = @{}
foreach ($tideSection in $tideSections) { $tideByNumber[$tideSection.Groups['Number'].Value] = $tideSection }
function Compact-TideText([string]$value) {
    return ([regex]::Replace($value, '\s+', '')).Replace('4x4', '4×4').Replace('＋', '+')
}
$tideProblems = [System.Collections.Generic.List[string]]::new()
$tideCount = 0
foreach ($tideCard in @($tideData.Cards | Where-Object SchoolId -eq 'tide')) {
    $tideCount++
    $tideSection = $tideByNumber[$tideCard.Number]
    if ($null -eq $tideSection) { $tideProblems.Add("Missing manuscript card $($tideCard.Number)"); continue }
    if ($tideSection.Groups['Name'].Value -ne $tideCard.Name) { $tideProblems.Add("Name changed: $($tideCard.Number)") }
    if (@($tideCard.StageVariants).Count -ne 1) { $tideProblems.Add("Unexpected multiple stage variants: $($tideCard.Number)"); continue }
    $tideVariant = $tideCard.StageVariants[0]
    $tideBody = $tideSection.Groups['Body'].Value
    $tideExpectedCost = [regex]::Match($tideBody, '(?m)^- \*\*消耗\*\*：([^\r\n]*)').Groups[1].Value
    $tideOther = if ($tideVariant.OtherCost -eq '—') { '无其他消耗' } else { $tideVariant.OtherCost }
    $tideActualCost = '能量 ' + $tideVariant.Energy + '；' + $tideOther
    if ((Compact-TideText $tideExpectedCost) -ne (Compact-TideText $tideActualCost)) { $tideProblems.Add("Cost changed: $($tideCard.Number): $tideExpectedCost -> $tideActualCost") }
    $tideExpectedStage = [regex]::Match($tideBody, '(?m)^\*\*阶段：([^*]+)\*\*').Groups[1].Value
    if ($tideExpectedStage -ne ($tideVariant.Stages -join '／')) { $tideProblems.Add("Stage changed: $($tideCard.Number)") }
    $tideExpected = Compact-TideText ([regex]::Match($tideBody, '(?m)^- \*\*效果\*\*：([^\r\n]*)').Groups[1].Value)
    # Explicit user clarifications replace the original ambiguous draft wording.
    $tideExpected = $tideExpected.Replace('将格挡变为蓄潮格挡', '将当前全部格挡变为蓄潮格挡')
    if ($tideCard.Number -eq '5004') { $tideExpected = '范围1-4格：' + $tideExpected }
    if ($tideCard.Number -eq '5011') { $tideExpected = $tideExpected.Replace('直到下个我方回合结束，', '直到下个己方回合结束时，') }
    if ($tideCard.Number -eq '5013') { $tideExpected = '本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。' }
    $tideActual = Compact-TideText $tideVariant.Effect
    if ($tideActual -ne $tideExpected) { $tideProblems.Add("Effect mismatch: $($tideCard.Number): $tideExpected -> $tideActual") }
}
if ($tideCount -ne 39) { $tideProblems.Add("Expected 39 tide cards; found $tideCount") }
$tideLines = @("Compared=$tideCount Mismatches=$($tideProblems.Count)") + @($tideProblems)
$tideLines | Set-Content -LiteralPath (Join-Path $CatalogDirectory 'manuscript-match.txt') -Encoding utf8
$tideLines | Write-Output
if ($tideProblems.Count -gt 0) { throw 'Tide catalogue differs from the manuscript and confirmed clarifications.' }
