param(
    [string]$CatalogDirectory = 'D:/Game/outputs/blood-rules-20261024/VerifiedCatalog',
    [string]$Manuscript = 'D:/Game/outputs/blood-rules-20261024/Inputs/全部卡牌清单.user.md'
)
$ErrorActionPreference = 'Stop'
$bloodData = Get-Content -LiteralPath (Join-Path $CatalogDirectory 'card-list-data.json') -Encoding utf8 -Raw | ConvertFrom-Json
$bloodManuscript = Get-Content -LiteralPath $Manuscript -Encoding utf8 -Raw
$bloodSections = [regex]::Matches($bloodManuscript, '(?ms)^### (?<Number>\d+) (?<Name>.*?)｜[^\r\n]+\r?\n(?<Body>.*?)(?=^### |^## |\z)')
$bloodByNumber = @{}
foreach ($bloodSection in $bloodSections) { $bloodByNumber[$bloodSection.Groups['Number'].Value] = $bloodSection }
function Compact-BloodText([string]$value) {
    $value = ([regex]::Replace($value, '\s+', '')).Replace('*', '×').Replace('＋', '+')
    $value = [regex]::Replace($value, '恢复(\d+)点能量', '获得$1点能量')
    return $value.TrimEnd('。')
}
$bloodProblems = [System.Collections.Generic.List[string]]::new()
$bloodCount = 0
foreach ($bloodCard in @($bloodData.Cards | Where-Object SchoolId -eq 'blood')) {
    $bloodCount++
    $bloodSection = $bloodByNumber[$bloodCard.Number]
    if ($null -eq $bloodSection) { $bloodProblems.Add("Missing manuscript card $($bloodCard.Number)"); continue }
    if ($bloodSection.Groups['Name'].Value -ne $bloodCard.Name) { $bloodProblems.Add("Name changed: $($bloodCard.Number)") }
    if (@($bloodCard.StageVariants).Count -ne 1) { $bloodProblems.Add("Unexpected multiple stage variants: $($bloodCard.Number)"); continue }
    $bloodVariant = $bloodCard.StageVariants[0]
    $bloodBody = $bloodSection.Groups['Body'].Value
    $bloodExpectedCost = [regex]::Match($bloodBody, '(?m)^- \*\*消耗\*\*：([^\r\n]*)').Groups[1].Value
    # Confirmed follow-up: these four blood costs enhance their free base effect;
    # Desperate Cannon only pays its five hull while in desperation.
    if ($bloodCard.Number -in @('7001','7002','7005','7008')) {
        $bloodExpectedCost = $bloodExpectedCost -replace '；船体 ', '；增效消耗：船体 '
    }
    if ($bloodCard.Number -eq '7007') { $bloodExpectedCost = $bloodExpectedCost -replace '；船体 ', '；绝境消耗：船体 ' }
    $bloodOther = if ($bloodVariant.OtherCost -eq '—') { '无其他消耗' } else { $bloodVariant.OtherCost }
    $bloodActualCost = '能量 ' + $bloodVariant.Energy + '；' + $bloodOther
    if ((Compact-BloodText $bloodExpectedCost) -ne (Compact-BloodText $bloodActualCost)) { $bloodProblems.Add("Cost changed: $($bloodCard.Number): $bloodExpectedCost -> $bloodActualCost") }
    $bloodExpectedStage = [regex]::Match($bloodBody, '(?m)^\*\*阶段：([^*]+)\*\*').Groups[1].Value
    if ($bloodExpectedStage -ne ($bloodVariant.Stages -join '／')) { $bloodProblems.Add("Stage changed: $($bloodCard.Number)") }
    $bloodExpected = Compact-BloodText ([regex]::Match($bloodBody, '(?m)^- \*\*效果\*\*：([^\r\n]*)').Groups[1].Value)
    if ($bloodCard.Number -eq '7002') { $bloodExpected = $bloodExpected.Replace('消耗5点船体获得8点格挡', '消耗5点船体额外获得8点格挡') }
    $bloodActual = Compact-BloodText $bloodVariant.Effect
    if ($bloodActual -ne $bloodExpected) { $bloodProblems.Add("Effect mismatch: $($bloodCard.Number): $bloodExpected -> $bloodActual") }
    $bloodExpectedKeywords = [regex]::Match($bloodBody, '(?m)^- \*\*字段\*\*：([^\r\n]*)').Groups[1].Value
    $bloodActualKeywords = @($bloodVariant.Keywords | ForEach-Object { $_.Name }) -join '、'
    if (-not $bloodActualKeywords) { $bloodActualKeywords = '—' }
    if ((Compact-BloodText $bloodActualKeywords) -ne (Compact-BloodText $bloodExpectedKeywords)) { $bloodProblems.Add("Keyword mismatch: $($bloodCard.Number): $bloodExpectedKeywords -> $bloodActualKeywords") }
}
if ($bloodCount -ne 36) { $bloodProblems.Add("Expected 36 blood cards; found $bloodCount") }
$bloodLines = @("Compared=$bloodCount Mismatches=$($bloodProblems.Count)") + @($bloodProblems)
$bloodLines | Set-Content -LiteralPath (Join-Path $CatalogDirectory 'manuscript-match.txt') -Encoding utf8
$bloodLines | Write-Output
if ($bloodProblems.Count -gt 0) { throw 'Blood catalogue differs from the manuscript and confirmed clarifications.' }
