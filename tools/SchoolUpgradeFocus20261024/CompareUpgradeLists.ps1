param(
    [Parameter(Mandatory=$true)][string]$Before,
    [Parameter(Mandatory=$true)][string]$After,
    [Parameter(Mandatory=$true)][string]$Output,
    [string[]]$ExpectedChangedIds = @()
)
$ErrorActionPreference = 'Stop'
$curveBeforePath = [IO.Path]::GetFullPath($Before)
$curveAfterPath = [IO.Path]::GetFullPath($After)
$curveOutput = [IO.Path]::GetFullPath($Output)
$curveBefore = Get-Content -LiteralPath $curveBeforePath -Raw | ConvertFrom-Json
$curveAfter = Get-Content -LiteralPath $curveAfterPath -Raw | ConvertFrom-Json
if ($curveBefore.CardCount -ne $curveAfter.CardCount -or $curveBefore.StageCount -ne $curveAfter.StageCount) { throw 'Published card or stage counts changed.' }
function CurveMap($cards) {
    $map = @{}
    foreach ($card in $cards) {
        if ($map.ContainsKey($card.Id)) { throw ('Duplicate card identity: ' + $card.Id) }
        $map[$card.Id] = $card
    }
    return $map
}
function CurveText($text) { ([string]$text).Replace("`r", '').Replace("`n", '<br>').Replace('|', '\|') }
$curveOldMap = CurveMap $curveBefore.Cards
$curveNewMap = CurveMap $curveAfter.Cards
if (@($curveOldMap.Keys | Where-Object { -not $curveNewMap.ContainsKey($_) }).Count -gt 0) { throw 'A published identity disappeared.' }
$curveRows = [System.Collections.Generic.List[object]]::new()
$curveChangedIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($card in $curveAfter.Cards) {
    $old = $curveOldMap[$card.Id]
    if (-not $old -or $card.Number -cne $old.Number -or $card.Name -cne $old.Name -or $card.SchoolId -cne $old.SchoolId -or
        $card.Source -cne $old.Source -or $card.UsesSourceExperience -ne $old.UsesSourceExperience) { throw ('Card identity/source changed: ' + $card.Id) }
    if ((@($card.Stages.Stage | Sort-Object) -join '|') -cne (@($old.Stages.Stage | Sort-Object) -join '|')) { throw ('Playable stages changed: ' + $card.Id) }
    foreach ($stage in $card.Stages) {
        $oldStage = @($old.Stages | Where-Object Stage -eq $stage.Stage)[0]
        foreach ($entry in @($oldStage, $stage)) {
            $ranks = @($entry.Levels.Level)
            if ($entry.MaximumLevel -lt 0 -or $entry.MaximumLevel -gt 3 -or $ranks.Count -ne 1 + $entry.MaximumLevel -or
                ($ranks -join ',') -cne ((0..$entry.MaximumLevel) -join ',')) { throw ('Illegal rank range: ' + $card.Id + '/' + $stage.Stage) }
        }
        if ($oldStage.Levels[0].Effect -cne $stage.Levels[0].Effect -or $oldStage.Levels[0].Energy -ne $stage.Levels[0].Energy -or
            $oldStage.Levels[0].OtherCost -cne $stage.Levels[0].OtherCost) { throw ('Base effect/cost changed in upgrade-only revision: ' + $card.Id + '/' + $stage.Stage) }
        $max = [Math]::Max($oldStage.MaximumLevel, $stage.MaximumLevel)
        for ($rank = 0; $rank -le $max; $rank++) {
            $oldRank = @($oldStage.Levels | Where-Object Level -eq $rank) | Select-Object -First 1
            $newRank = @($stage.Levels | Where-Object Level -eq $rank) | Select-Object -First 1
            if ($oldRank -and $newRank -and ($oldRank.Energy -ne $newRank.Energy -or $oldRank.OtherCost -cne $newRank.OtherCost)) {
                throw ('Play costs changed: ' + $card.Id + '/' + $stage.Stage + '/+' + $rank)
            }
            $changed = (-not $oldRank) -or (-not $newRank) -or $oldRank.Effect -cne $newRank.Effect -or $oldRank.Name -cne $newRank.Name
            if ($changed -or $oldStage.MaximumLevel -ne $stage.MaximumLevel) { [void]$curveChangedIds.Add($card.Id) }
            $curveRows.Add([pscustomobject]@{
                Id=$card.Id; Number=$card.Number; Name=$card.Name; School=$card.School; Stage=$stage.Stage; Level=$rank
                BeforeMaximum=$oldStage.MaximumLevel; AfterMaximum=$stage.MaximumLevel; Changed=$changed
                Energy=$(if ($newRank) {$newRank.Energy} else {$oldRank.Energy}); OtherCost=$(if ($newRank) {$newRank.OtherCost} else {$oldRank.OtherCost})
                BeforeEffect=$(if ($oldRank) {$oldRank.Effect} else {'该等级不可用'}); AfterEffect=$(if ($newRank) {$newRank.Effect} else {'该等级不可用'})
                BeforeUpgradeCost=$(if ($oldRank) {$oldRank.UpgradeCost} else {''}); AfterUpgradeCost=$(if ($newRank) {$newRank.UpgradeCost} else {''})
            })
        }
    }
}
if ($ExpectedChangedIds.Count -gt 0) {
    if ((@($curveChangedIds | Sort-Object) -join '|') -cne (@($ExpectedChangedIds | Sort-Object) -join '|')) {
        throw ('Changed identities differ from expected set: ' + (@($curveChangedIds | Sort-Object) -join ','))
    }
}
New-Item -ItemType Directory -Path $curveOutput -Force | Out-Null
$curveChangedRows = @($curveRows | Where-Object { $curveChangedIds.Contains($_.Id) })
$curveChangedRows | Export-Csv -LiteralPath (Join-Path $curveOutput '强化曲线变化.csv') -Encoding utf8 -NoTypeInformation
$curveText = [Text.StringBuilder]::new()
[void]$curveText.AppendLine('# 强化曲线精确对照')
[void]$curveText.AppendLine()
[void]$curveText.AppendLine(('以实际导出的卡面逐等级比较，共 {0} 个卡牌身份发生强化变化。基础效果、使用费用、身份编号和可用阶段均保持一致。' -f $curveChangedIds.Count))
[void]$curveText.AppendLine()
foreach ($group in ($curveChangedRows | Group-Object Id | Sort-Object { [int]$_.Group[0].Number })) {
    $row = $group.Group[0]
    [void]$curveText.AppendLine(('## {0} {1}｜{2}' -f $row.Number, $row.Name, $row.School))
    [void]$curveText.AppendLine()
    [void]$curveText.AppendLine(('身份：`{0}`。基础版本只列为核对参照。' -f $row.Id))
    [void]$curveText.AppendLine()
    [void]$curveText.AppendLine('| 阶段／等级 | 使用消耗 | 修改前 | 修改后 |')
    [void]$curveText.AppendLine('| --- | --- | --- | --- |')
    foreach ($entry in $group.Group) {
        $cost = '能量 ' + $entry.Energy + $(if ($entry.OtherCost -ne '—') {'；' + $entry.OtherCost} else {''})
        [void]$curveText.AppendLine(('| {0} +{1} | {2} | {3} | {4} |' -f $entry.Stage, $entry.Level, (CurveText $cost), (CurveText $entry.BeforeEffect), (CurveText $entry.AfterEffect)))
    }
    [void]$curveText.AppendLine()
}
[IO.File]::WriteAllText((Join-Path $curveOutput '强化曲线变化.md'), $curveText.ToString(), [Text.UTF8Encoding]::new($false))
[pscustomobject]@{
    Before=$curveBeforePath; BeforeSHA256=(Get-FileHash -LiteralPath $curveBeforePath).Hash
    After=$curveAfterPath; AfterSHA256=(Get-FileHash -LiteralPath $curveAfterPath).Hash
    ChangedCount=$curveChangedIds.Count; ChangedIds=@($curveChangedIds | Sort-Object)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $curveOutput '强化曲线对照输入.json') -Encoding utf8
('PASS {0} identities, {1} stage definitions; {2} changed identities; base faces and all common-rank play costs unchanged.' -f $curveAfter.CardCount, $curveAfter.StageCount, $curveChangedIds.Count) |
    Tee-Object -FilePath (Join-Path $curveOutput '强化曲线对照验证.txt')
