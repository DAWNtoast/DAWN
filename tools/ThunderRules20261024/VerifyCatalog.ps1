param(
    [string]$CatalogDirectory = 'D:/Game/outputs/thunder-rules-20261024/VerifiedCatalog',
    [string]$Manuscript = 'D:/Game/outputs/thunder-rules-20261024/Inputs/全部卡牌清单.user.md'
)
$ErrorActionPreference = 'Stop'
$thunderData = Get-Content -LiteralPath (Join-Path $CatalogDirectory 'card-list-data.json') -Encoding utf8 -Raw | ConvertFrom-Json
$thunderManuscript = Get-Content -LiteralPath $Manuscript -Encoding utf8 -Raw
$thunderSections = [regex]::Matches($thunderManuscript, '(?ms)^### (?<Number>\d+) (?<Name>.*?)｜[^\r\n]+\r?\n(?<Body>.*?)(?=^### |^## |\z)')
$thunderByNumber = @{}
foreach ($thunderSection in $thunderSections) { $thunderByNumber[$thunderSection.Groups['Number'].Value] = $thunderSection }
function Compact-ThunderText([string]$value) {
    $value = ([regex]::Replace($value, '\s+', '')).Replace('*', '×').Replace('＋', '+')
    $value = [regex]::Replace($value, '恢复(\d+)点能量', '获得$1点能量')
    return $value.TrimEnd('。')
}
$thunderProblems = [System.Collections.Generic.List[string]]::new()
$thunderCount = 0
foreach ($thunderCard in @($thunderData.Cards | Where-Object SchoolId -eq 'thunder')) {
    $thunderCount++
    $thunderSection = $thunderByNumber[$thunderCard.Number]
    if ($null -eq $thunderSection) { $thunderProblems.Add("Missing manuscript card $($thunderCard.Number)"); continue }
    if ($thunderSection.Groups['Name'].Value -ne $thunderCard.Name) { $thunderProblems.Add("Name changed: $($thunderCard.Number)") }
    if (@($thunderCard.StageVariants).Count -ne 1) { $thunderProblems.Add("Unexpected multiple stage variants: $($thunderCard.Number)"); continue }
    $thunderVariant = $thunderCard.StageVariants[0]
    $thunderBody = $thunderSection.Groups['Body'].Value
    $thunderExpectedCost = [regex]::Match($thunderBody, '(?m)^- \*\*消耗\*\*：([^\r\n]*)').Groups[1].Value
    $thunderOther = if ($thunderVariant.OtherCost -eq '—') { '无其他消耗' } else { $thunderVariant.OtherCost }
    $thunderActualCost = '能量 ' + $thunderVariant.Energy + '；' + $thunderOther
    if ($thunderCard.Number -eq '6003' -or $thunderCard.Number -eq '6014') { $thunderExpectedCost = '能量 0；无其他消耗' }
    if ($thunderCard.Number -eq '6011') { $thunderExpectedCost = '能量 3；连锁 X；所选 2×2 区域内的己方雷栅' }
    if ((Compact-ThunderText $thunderExpectedCost) -ne (Compact-ThunderText $thunderActualCost)) { $thunderProblems.Add("Cost changed: $($thunderCard.Number): $thunderExpectedCost -> $thunderActualCost") }
    $thunderExpectedStage = [regex]::Match($thunderBody, '(?m)^\*\*阶段：([^*]+)\*\*').Groups[1].Value
    if ($thunderExpectedStage -ne ($thunderVariant.Stages -join '／')) { $thunderProblems.Add("Stage changed: $($thunderCard.Number)") }
    $thunderExpected = Compact-ThunderText ([regex]::Match($thunderBody, '(?m)^- \*\*效果\*\*：([^\r\n]*)').Groups[1].Value)
    # Confirmed follow-up clarifications supersede the draft's fire-field typo and broad immunity wording.
    if ($thunderCard.Number -eq '6011') { $thunderExpected = '范围1-4格：选择含己方雷栅的2×2区域，消耗其中己方雷栅；区域内双方受到18+8X+4×移除雷栅格数点伤害' }
    if ($thunderCard.Number -eq '9084' -or $thunderCard.Number -eq '9085') { $thunderExpected = $thunderExpected.Replace('免疫雷栅影响', '免疫雷栅伤害') }
    $thunderActual = Compact-ThunderText $thunderVariant.Effect
    if ($thunderActual -ne $thunderExpected) { $thunderProblems.Add("Effect mismatch: $($thunderCard.Number): $thunderExpected -> $thunderActual") }
}
if ($thunderCount -ne 39) { $thunderProblems.Add("Expected 39 thunder cards; found $thunderCount") }
$thunderLines = @("Compared=$thunderCount Mismatches=$($thunderProblems.Count)") + @($thunderProblems)
$thunderLines | Set-Content -LiteralPath (Join-Path $CatalogDirectory 'manuscript-match.txt') -Encoding utf8
$thunderLines | Write-Output
if ($thunderProblems.Count -gt 0) { throw 'Thunder catalogue differs from the manuscript and confirmed clarifications.' }
